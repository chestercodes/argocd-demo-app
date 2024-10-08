namespace api.Shared
#nowarn "20"
open System

module Diagnostics =
    open System.Diagnostics
    open System.Diagnostics.Metrics

    let ServiceName = "todoapp"
    let ServiceNamespace = "todoapp"

    type Instance() =
        static member ActivitySource = new ActivitySource(ServiceName)
        static member ApiMeter = new Meter("ApiMeter", "1.0")
    
    type Counters() =
        static member GetAll_Called = Instance.ApiMeter.CreateCounter("getall_called")
        static member Post_Called = Instance.ApiMeter.CreateCounter("post_called")

module Env =
    let getEnvVarOrFail name =
        let v = Environment.GetEnvironmentVariable name
        if v = null then raise (Exception (sprintf "Cannot get env var '%s'" name)) else v
    let ENVNAME = getEnvVarOrFail "ENVNAME"
    let IMAGE_TAG = getEnvVarOrFail "IMAGE_TAG"
    let OTLP_ENDPOINT = getEnvVarOrFail "OTLP_ENDPOINT" |> Uri

module Postgresql =
    open Env
    let dbName = "todoapp"
    let connectionString =
        sprintf "Host=%s;Port=5432;User ID=%s;Password=%s;Database=%s"
            (getEnvVarOrFail "DB_HOST")
            (getEnvVarOrFail "DB_USER")
            (getEnvVarOrFail "DB_PASSWORD")
            dbName
