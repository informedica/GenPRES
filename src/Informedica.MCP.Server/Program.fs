open System

open Informedica.Utils.Lib
open Informedica.GenForm.Lib

open Informedica.MCP.Lib


[<EntryPoint>]
let main _ =
    Env.loadDotEnv () |> ignore
    Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
    Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")

    // Set working directory to the resolved app root so data paths resolve
    Environment.CurrentDirectory <- AppPath.rootPath ()

    let dataUrlId =
        match Environment.GetEnvironmentVariable "GENPRES_URL_ID" with
        | null
        | "" -> invalidOp "GENPRES_URL_ID environment variable must be set before starting the MCP server."
        | value -> value

    let logger, disposeLogger =
        McpLogging.getLogger (fun name -> Environment.GetEnvironmentVariable name |> Option.ofObj)

    let provider = Api.getCachedProviderWithDataUrlId Informedica.GenOrder.Lib.OrderLogging.noOp dataUrlId

    try
        McpServer.run logger provider
        0
    finally
        disposeLogger ()
