//module Server

open System
open Giraffe
open Saturn
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared
open Shared.Api
open ServerApi
open Informedica.Utils.Lib.ConsoleWriter.NewLineTime


writeInfoMessage $"""
=== Initialized: ===
- Formulary {Models.Formulary.empty |> Formulary.get |> ignore}
- Parenteralia {Models.Parenteralia.empty |> Parenteralia.get |> ignore}
- Scenarios {Models.PrescriptionContext.empty |> PrescriptionResult.get |> ignore}
"""


let tryGetEnv key =
    match Environment.GetEnvironmentVariable key with
    | x when String.IsNullOrWhiteSpace x -> None
    | x -> Some x


let port =
    "SERVER_PORT"
    |> tryGetEnv |> Option.map uint16 |> Option.defaultValue 8085us


let webApi =
    Remoting.createApi()
    |> Remoting.fromValue serverApi
    |> Remoting.withRouteBuilder routerPaths
    |> Remoting.buildHttpHandler


let webApp =
    choose [
        webApi;
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
    //use_iis

    //service_config configureServices
    //host_config Env.configureHost
}


$"""
=== Environmental variables ===
GENPRES_URL_ID={tryGetEnv "GENPRES_URL_ID"}
GENPRES_LOG={tryGetEnv "GENPRES_LOG"}
GENPRES_PROD={tryGetEnv "GENPRES_PROD"}
"""
|> writeInfoMessage


run application