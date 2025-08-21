//module Server

open System
open Giraffe
open Saturn
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared.Api
open ServerApi
open Informedica.Utils.Lib.ConsoleWriter.NewLineTime



let tryGetEnv key =
    match Environment.GetEnvironmentVariable key with
    | x when String.IsNullOrWhiteSpace x -> None
    | x -> Some x


let initialize () =
    $"""

=== Environmental variables ===
GENPRES_URL_ID={tryGetEnv "GENPRES_URL_ID" |> Option.defaultValue "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"}
GENPRES_LOG={tryGetEnv "GENPRES_LOG" |> Option.defaultValue "0"}
GENPRES_PROD={tryGetEnv "GENPRES_PROD" |> Option.defaultValue "0"}
GENPRES_DEBUG={tryGetEnv "GENPRES_DEBUG" |> Option.defaultValue "1"}

"""
    |> writeInfoMessage


let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us


let webApi =
    let serverApi = 
        async {
            use logger = Informedica.GenOrder.Lib.OrderLogging.createAgentLogger Logging.config
            do! logger |> Logging.activateLogger (Some "ServerApi")
            let provider =
                tryGetEnv "GENPRES_URL_ID"
                |> Option.defaultValue "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"
                |> Informedica.GenForm.Lib.Api.getCachedProviderWithDataUrlId logger.Logger

            return provider |> createServerApi
        } |> Async.RunSynchronously

    Remoting.createApi()
    |> Remoting.fromValue serverApi
    |> Remoting.withRouteBuilder routerPaths
    |> Remoting.buildHttpHandler


let webApp =
    choose [
        webApi
        GET >=> text "GenInteractions App. Use localhost: 8080 for the GUI"
    ]


let application = application {
    url ("http://*:" + port.ToString() + "/")
    use_mime_types [
            ".svg", "image/svg+xml"
            ".png", "image/png"
        ]
    use_static "public" //publicPath
    use_router webApp
    memory_cache
    use_gzip
}


initialize ()
run application