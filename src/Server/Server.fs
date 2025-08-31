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


open Informedica.Utils.Lib

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System.Threading.Tasks


let getClientIP (context: HttpContext) =
    match context.Request.Headers.TryGetValue("X-Forwarded-For") with
    | true, values when values.Count > 0 ->
        values.[0].Split(',').[0].Trim()
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
    tryGetEnv "GENPRES_URL_ID"
    |> Option.defaultValue "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"
    |> Informedica.GenForm.Lib.Api.getCachedProviderWithDataUrlId (Logging.getLogger ()).Logger



let logClientIP : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->

        let clientIP = getClientIP ctx
        let path = ctx.Request.Path.ToString()
        let method = ctx.Request.Method
        
        // Log synchronously first
        try            
            // If you need async logging, you could do it in a background task
            async {
                try
                    let logger = Logging.getLogger () 
                    do! logger |> Logging.setComponentName (Some "Client_Request")
                    Logging.ServerLogging.logRequest logger method path clientIP
                with ex ->
                    sprintf "Async logging error: %s" ex.Message |> writeErrorMessage
            } |> Async.Start
        with ex ->
            sprintf "Logging error: %s" ex.Message |> writeDebugMessage
        
        // Continue with the next handler
        next ctx


let webApi =
    let serverApi = 
        async {
            do! Logging.getLogger () |> Logging.setComponentName (Some "ServerApi")

            return provider |> createServerApi
        } |> Async.RunSynchronously

    Remoting.createApi()
    |> Remoting.fromValue serverApi
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
            writeInfoMessage "Trying to Stop Server Async"
            try
                let logger = Logging.getLogger()
                logger.StopAsync() |> Async.StartAsTask :> Task
            with ex ->
                eprintfn "Logger shutdown failed: %s" ex.Message
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