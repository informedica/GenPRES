module Server

open System
open System.IO
open Giraffe
open Saturn
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Shared
open Shared.Api


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
    use_router webApp
    memory_cache
    use_gzip
    use_mime_types [".svg", "image/svg+xml"]
    use_static "public" //publicPath
    //use_iis

    //service_config serviceConfig
    //host_config Env.configureHost
}

run application