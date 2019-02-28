namespace GenPres.Server

open GenPres.Shared

module App =
    open System
    open System.IO
    open Microsoft.Extensions.DependencyInjection
    open FSharp.Control.Tasks.V2
    open Giraffe
    open Saturn
    open Types

    let path =
        Path.Combine(System.Environment.CurrentDirectory, "./../../data/data/")
    let decode<'T> s = Thoth.Json.Net.Decode.Auto.unsafeFromString<'T> (s)

    let readFromFile<'T> file =
        let file = Path.Combine(path, file)
        File.ReadAllText(file) |> decode<'T>

    let processRequest (req : Request.Msg) =
        printfn "received request: %A" req
        match req with
        | Request.ConfigMsg msg ->
            match msg with
            | Request.Configuration.Get ->
                Configuration.getSettings()
                |> Types.Response.Configuration
                |> Some
        | Request.PatientMsg msg ->
            match msg with
            | Request.Patient.Init ->
                Patient.patient
                |> Response.Patient
                |> Some
            | Request.Patient.Calculate pat ->
                pat
                |> Patient.calculate
                |> Response.Patient
                |> Some
        | Request.AcuteListMsg msg ->
            match msg with
            | Request.AcuteList.Get ->
                "medicationDefs.json"
                |> readFromFile<Types.Treatment.MedicationDefs>
                |> Types.Response.MedicationDefs
                |> Some
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
            post "/api/request"
                (fun next ctx -> task { let! resp = task
                                                        {
                                                        let! req = ctx.BindJsonAsync<Request.Msg>
                                                                       ()
                                                        return req
                                                               |> processRequest }
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
