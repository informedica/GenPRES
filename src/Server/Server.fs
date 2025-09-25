//module Server

open System
open Giraffe
open Saturn
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api
open ServerApi
open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open System.Threading.Tasks

open Informedica.Utils.Lib.BCL
open Informedica.Utils.Lib

open Microsoft.AspNetCore.Http



let getClientIP (context: HttpContext) =
    match context.Request.Headers.TryGetValue("X-Forwarded-For") with
    | true, values when values.Count > 0 ->
        values[0].Split(',')
        |> Array.tryHead
        |> Option.map String.trim
        |> Option.defaultValue "unknown"
    | _ ->
        match context.Connection.RemoteIpAddress with
        | null -> "unknown"
        | ip -> ip.ToString()


let tryGetEnv key = Env.getItem key


$"""

=== Environmental variables ===
GENPRES_URL_ID={tryGetEnv "GENPRES_URL_ID" |> Option.defaultValue "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"}
GENPRES_LOG={tryGetEnv "GENPRES_LOG" |> Option.defaultValue "0"}
GENPRES_PROD={tryGetEnv "GENPRES_PROD" |> Option.defaultValue "0"}
GENPRES_DEBUG={tryGetEnv "GENPRES_DEBUG" |> Option.defaultValue "1"}

=== System Info ===

{Env.getSystemInfo ()}

"""
|> writeInfoMessage


let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us


let provider =
    let logger = Logging.getSpecificLogger Logging.ResourcesLogger

    logger
    |> Logging.setComponentName (Some "Provider")
    |> Async.RunSynchronously

    tryGetEnv "GENPRES_URL_ID"
    |> Option.defaultValue "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"
    |> Informedica.GenForm.Lib.Api.getCachedProviderWithDataUrlId logger.Logger


let logClientIP : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        let logger = Logging.getSpecificLogger Logging.RequestLogger
        let clientIP = getClientIP ctx
        let path = ctx.Request.Path.ToString()
        let method = ctx.Request.Method
        
        async {
            do!
                logger 
                |> Logging.setComponentName (Some "Client_Request")           
            Logging.ServerLogging.logRequest logger method path clientIP
            return ()
        }
        |> Async.Start
        
        // Continue with the next handler
        next ctx


let webApi =
    Remoting.createApi()
    |> Remoting.fromValue (createServerApi provider)
    |> Remoting.withRouteBuilder routerPaths
    |> Remoting.buildHttpHandler


let webApp =
    choose [
        logClientIP >=> webApi
        GET >=> text "GenInteractions App. Use localhost: 8080 for the GUI"
    ]


type LoggerShutdown() =
    interface IHostedService with
        member _.StartAsync(_) =
            Task.CompletedTask

        member _.StopAsync(_) =
            let logger = Logging.getSpecificLogger Logging.RequestLogger

            writeInfoMessage "Trying to Stop Server Async"
            try
                // TODO: need to stop all loggers
                logger.StopAsync() |> Async.StartAsTask :> Task
            with ex ->
                writeDebugMessage $"Logger shutdown failed: {ex.Message}"
                Task.CompletedTask


let application = application {
    url ("http://*:" + port.ToString() + "/")
    use_mime_types [
                ".svg", "image/svg+xml"
                ".png", "image/png"
            ]
    use_static "public" //publicPath
    use_router webApp
    service_config (fun services ->
        services.AddHostedService<LoggerShutdown>() |> ignore
        services
    )
    memory_cache
    use_gzip
}

run application