open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Saturn
open Shared

open Giraffe.Serialization

let publicPath = Path.GetFullPath "../Client/public"
let port = 8085us

let getInitialVersion() : Task<GenPres> = 
    let patient = { Age = { Years = 0; Months = 0 }; Weight = 0. }
    task { return { Name = "Genpres"; Version = "0.0.1"; Patient = patient } }

let webApp = router {
    get "/api/init" (fun next ctx ->
        task {
            let! counter = getInitialVersion()
            return! Successful.OK counter next ctx
        })
}

let configureSerialization (services:IServiceCollection) =
    let fableJsonSettings = Newtonsoft.Json.JsonSerializerSettings()
    fableJsonSettings.Converters.Add(Fable.JsonConverter())
    services.AddSingleton<IJsonSerializer>(NewtonsoftJsonSerializer fableJsonSettings)

let app = application {
    url ("http://0.0.0.0:" + port.ToString() + "/")
    use_router webApp
    memory_cache
    use_static publicPath
    service_config configureSerialization
    use_gzip
}

run app
