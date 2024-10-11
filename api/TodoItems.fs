namespace api
#nowarn "20"
open Microsoft.Extensions.Logging
open api.Shared
open Microsoft.AspNetCore.Mvc
open System.Threading.Tasks
open Npgsql.FSharp
open System
open FsToolkit.ErrorHandling

type ValidationError = Invalid of string

module TodoItems =
    type TodoItem = {
        Id: Guid
        Content: string
        IsComplete: bool
    }
    
    type UnvalidatedTodoItem = {
        Content: string
        IsComplete: bool option
    }
    
    module TodoItemValidation =
        type ValidatedTodoItem = {
            Content: string
            IsComplete: bool
        }

        let (|IsEmpty|_|) input =
            if String.IsNullOrWhiteSpace(input) then Some input else None

        let validate (item: UnvalidatedTodoItem): Result<ValidatedTodoItem, ValidationError> =
            match item.Content with
            | IsEmpty _ -> "Need to specify content" |> Invalid |> Error
            | _ ->
                Ok {
                    Content = item.Content
                    IsComplete = item.IsComplete |> Option.defaultValue false
                }

    module Queries =
        let readTodoItem (row: RowReader): TodoItem =
            {
                Id = row.uuid "id"
                Content = row.string "content"
                IsComplete = row.bool "is_complete"
            }
        let getAllFromDatabase (): Task<Result<TodoItem list, exn>> =
            taskResult {        
                try
                    let! items =
                        Postgresql.connectionString
                        |> Sql.connect
                        |> Sql.query " select * from public.todoitems "
                        |> Sql.executeAsync readTodoItem
                    return items
                with
                    | exn -> return! Error exn
            }
    
    module Commands =
        open TodoItemValidation
        
        let addItem (item: ValidatedTodoItem): Task<Result<Guid, exn>> =
            taskResult {        
                try
                    let! id =
                        Postgresql.connectionString
                        |> Sql.connect
                        |> Sql.query "INSERT INTO public.todoitems (content, is_complete) VALUES (@content, @is_complete) RETURNING id "
                        |> Sql.parameters [
                                "@content", Sql.string item.Content
                                "@is_complete", Sql.bool item.IsComplete
                            ]
                        |> Sql.executeRowAsync (fun read -> read.uuid "id")
                    return id
                with
                    | exn -> return! Error(exn)
            }

    module Service =
        open TodoItemValidation

        type TodoItemsGetAllError =
            | DbError of exn
        let getAll (): Task<Result<TodoItem list, TodoItemsGetAllError>> =
            Queries.getAllFromDatabase () |> TaskResult.mapError DbError

        type TodoItemsAddError =
            | ItemIsNotValid of ValidationError
            | DbError of exn
        let add (item: UnvalidatedTodoItem):Task<Result<TodoItem, TodoItemsAddError>> =
            taskResult {
                let! validItem = validate item |> Result.mapError ItemIsNotValid
                let! dbGeneratedId = Commands.addItem validItem |> TaskResult.mapError DbError
                let todoItem = {
                    Id = dbGeneratedId
                    Content = validItem.Content
                    IsComplete = validItem.IsComplete
                }
                return todoItem
            }

module TodoItemsControllerApiModels =

    type TodoItemResponse = {
        id: Guid
        content: string
        isComplete: bool
    }

    type TodoItemsPostRequest = {
        content: string
        isComplete: bool option
    }

    type TodoItemsPostResponse = TodoItemResponse

    type TodoItemsGetAllResponse = {
        items: TodoItemResponse list
        count: int
    }

open TodoItems
open TodoItems.Service
open TodoItemsControllerApiModels

module AspNetHelpers =
    let ok dto = OkObjectResult(dto) :> ActionResult
    let notValid (Invalid err) =
        let resp = UnprocessableEntityObjectResult({| error = err |})
        resp :> ActionResult
    let serverError () =
        let resp = Microsoft.AspNetCore.Mvc.StatusCodeResult(500)
        resp :> ActionResult

open AspNetHelpers

// I think this needs to be at the same namespace level as the entrypoint to be registered
[<ApiController>]
type TodoItemsController (logger : ILogger<TodoItemsController>) =
    inherit ControllerBase()
    let toDto (x: TodoItem): TodoItemResponse =
        {
            content = x.Content
            isComplete = x.IsComplete
            id = x.Id
        }

    [<HttpGet>]
    [<Route("todoitems")>]
    member _.GetAll(): Task<ActionResult> =
        logger.LogInformation "GET called"
        Diagnostics.Counters.GetAll_Called.Add 1
        task {
            match! getAll () with
            | Ok data ->
                let resp:TodoItemsGetAllResponse = {
                    items = data |> List.map toDto
                    count = data.Length
                }
                return ok resp
            | Error error ->
                match error with
                | TodoItemsGetAllError.DbError ex ->
                    logger.LogError(ex, "Error :(")
                    return serverError ()
        }
    
    [<HttpPost>]
    [<Route("todoitems")>]
    member _.Add(dto: TodoItemsPostRequest): Task<ActionResult> =
        logger.LogInformation "POST called"
        Diagnostics.Counters.Post_Called.Add 1
        task {
            let item: UnvalidatedTodoItem = { Content = dto.content; IsComplete = dto.isComplete }
            match! add item with
            | Ok item ->
                let dto: TodoItemsPostResponse = {
                    content = item.Content
                    isComplete = item.IsComplete
                    id = item.Id
                }
                return ok dto
            | Error error ->
                match error with
                | DbError ex ->
                    logger.LogError(ex, "Error :(")
                    return serverError ()
                | ItemIsNotValid err -> return notValid err
        }
