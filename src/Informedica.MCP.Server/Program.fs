open System

open Informedica.Utils.Lib
open Informedica.GenForm.Lib

open Informedica.MCP.Lib


[<EntryPoint>]
let main _ =
    // Surfaces Serilog's own internal failures (a sink erroring on disk-full,
    // a permission error, ...) to stderr. Without this, such a failure is
    // swallowed by design and the audit trail stops with no signal at all.
    // Must run before McpLogging.getLogger constructs a Serilog logger, and
    // stderr is safe here regardless of the stdout redirect below, since
    // stdout is the only channel that must stay clean of stray bytes.
    Serilog.Debugging.SelfLog.Enable(fun msg -> eprintfn $"[Serilog] {msg}")

    // stdout is the JSON-RPC transport channel: McpServer.createHostBuilder's
    // WithStdioServerTransport opens it via Console.OpenStandardOutput(), a raw stream
    // independent of this TextWriter redirect (verified against the shipped
    // ModelContextProtocol.Core 1.2.0 StdioServerTransport, decompiled 2026-09-16). Any stray
    // ConsoleWriter/printfn output during resource loading below would otherwise interleave
    // plain text with JSON-RPC frames on stdout and corrupt the transport for the life of the
    // session (confirmed bug, #416 plan "Run 2026-09-16"), so redirect the TextWriter before
    // anything else can write through it.
    Console.SetOut Console.Error

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

    let provider = Api.getCachedProviderWithDataUrlId logger dataUrlId

    try
        McpServer.run logger provider
        0
    finally
        disposeLogger ()
