namespace GenPres.Server

open GenPres.Shared


module App =

    open System.IO
    open Microsoft.Extensions.DependencyInjection
    open FSharp.Control.Tasks.V2
    open Giraffe
    open Saturn
    open Types


    let processRequest (req : Request.Msg) =
        match req with
        | Request.ConfigMsg msg ->
            match msg with
            | Request.Configuration.Get ->
                Configuration.getSettings ()
                |> Types.Response.Configuration
                |> Some
        | Request.PatientMsg msg ->
            match msg with
            | Request.Patient.Init ->
                Patient.patient
                |> Response.Patient  
                |> Some
            | _ -> None
        
        | _ -> None


    let tryGetEnv =
        System.Environment.GetEnvironmentVariable
        >> function
        | null
        | "" -> None
        | x -> Some x

    let publicPath = Path.GetFullPath "../Client/public"

    let port =
        "SERVER_PORT"
        |> tryGetEnv
        |> Option.map uint16
        |> Option.defaultValue 8085us


    let webApp =
        router
            {
            post "/api/request" (fun next ctx ->
                task {
                    let! resp = task {
                        let! req = ctx.BindJsonAsync<Request.Msg>()
                        return req |> processRequest }
                    return! json resp next ctx }) }

    let configureSerialization (services : IServiceCollection) =
        services.AddSingleton<Giraffe.Serialization.Json.IJsonSerializer>
            (Thoth.Json.Giraffe.ThothSerializer())

    let app =
        application {
            url ("http://0.0.0.0:" + port.ToString() + "/")
            use_router webApp
            memory_cache
            use_static publicPath
            service_config configureSerialization
            use_gzip
        }

