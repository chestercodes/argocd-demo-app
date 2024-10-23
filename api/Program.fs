namespace api

#nowarn "20"

open System
open System.Collections.Generic
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open OpenTelemetry
open OpenTelemetry.Trace
open OpenTelemetry.Exporter
open OpenTelemetry.Logs
open OpenTelemetry.Resources
open Npgsql
open api.Shared
open Microsoft.AspNetCore.Http

module Startup =
    let addService (rb: ResourceBuilder) =
        rb.AddService(
            serviceName = Diagnostics.ServiceName,
            serviceNamespace = Diagnostics.ServiceNamespace,
            serviceVersion = Env.IMAGE_TAG,
            serviceInstanceId = Environment.MachineName
        )

        let attributes = Dictionary<string, Object>()
        attributes.Add("deployment.environment", box (Env.ENVNAME))
        rb.AddAttributes(attributes)

    let addMonitoring (builder: WebApplicationBuilder) =
        let configureResourceBuilder =
            Action<ResourceBuilder>(fun rb ->
                addService rb
                rb |> ignore
            )

        let resourceBuilder = ResourceBuilder.CreateDefault() |> addService

        builder.Services.AddLogging(fun addLogging ->
            addLogging.AddOpenTelemetry(fun addOpenT -> addOpenT.SetResourceBuilder(resourceBuilder) |> ignore)
            |> ignore
        )

        builder.Services.AddOpenTelemetry()
        |> fun b -> b.ConfigureResource(configureResourceBuilder)
        |> fun b ->
            b.WithMetrics(fun metrics ->
                metrics
                |> fun m -> m.AddMeter(Diagnostics.Instance.ApiMeter.Name)
                |> fun m -> m.AddInstrumentation(fun a -> ())
                |> ignore
            )
        |> fun b ->
            b.WithTracing(fun tracing ->
                tracing
                |> fun t -> t.AddSource(Diagnostics.Instance.ActivitySource.Name)
                |> fun t ->
                    t.AddAspNetCoreInstrumentation(fun a ->
                        let requestFilter (cxt: HttpContext) =
                            let path = cxt.Request.Path

                            let sendTraces =
                                if path.HasValue then
                                    // dont want to have metrics for the health path
                                    not (path.Value.ToLower() = "/health")
                                else
                                    false

                            sendTraces

                        a.Filter <- requestFilter
                        ()
                    )
                |> fun t -> t.AddNpgsql(fun x -> x |> ignore)
                |> ignore
            )
        |> fun b -> b.WithLogging(fun l -> l.SetResourceBuilder(resourceBuilder) |> ignore)
        |> fun b -> b.UseOtlpExporter(OtlpExportProtocol.Grpc, Env.OTLP_ENDPOINT)

    let getApp (args: string[]) =
        let builder = WebApplication.CreateBuilder(args)

        addMonitoring builder

        builder.Services.AddControllers()

        builder.Build()

open Microsoft.AspNetCore.Mvc

[<ApiController>]
type HealthController(logger: ILogger<HealthController>) =
    inherit ControllerBase()

    [<HttpGet>]
    [<Route("health")>]
    member _.Get() = "All is ok"

module Program =
    let exitCode = 0

    [<EntryPoint>]
    let main args =

        let app = Startup.getApp args

        app.UseHttpsRedirection()

        app.UseAuthorization()
        app.MapControllers()

        app.Run()
        0
