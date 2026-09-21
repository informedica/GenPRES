namespace Informedica.MCP.Lib

open System
open System.IO

open Informedica.Utils.Lib
open Informedica.Logging.Lib

open Serilog
open Serilog.Events


/// The MCP session's own IMessage, parallel to Informedica.GenPRES.Server's ServerLogging.Message.
[<RequireQualifiedAccess>]
type McpMessage =
    | ServerStarted of transport: string
    | ToolCallStarted of tool: string * argumentKeys: string[]
    | ToolCallCompleted of tool: string * elapsedMs: float
    | ToolCallFailed of tool: string * elapsedMs: float * reason: string

    interface IMessage


/// Composition-root logging for the stdio MCP host: one Serilog-backed logger for the whole
/// session, gated by GENPRES_LOG exactly as the server's `loggingEnabled`/`loggingLevel` are
/// (unset or blank => logging off => Logging.noOp). Duplicated rather than referenced from
/// Informedica.GenPRES.Server, since Informedica.MCP.Lib/Informedica.MCP.Server are siblings
/// to that project and must not reference it.
module McpLogging =

    module Logging = Informedica.Logging.Lib.Logging

    /// Turns a Serilog ILogger into the domain-facing Logger record. Every IMessage case
    /// becomes a Serilog structured property for free - no change to any IMessage type.
    module SerilogBridge =

        let toSerilogLevel =
            function
            | Level.Debug -> LogEventLevel.Debug
            | Level.Informative -> LogEventLevel.Information
            | Level.Warning -> LogEventLevel.Warning
            | Level.Error -> LogEventLevel.Error

        let toLogger (serilogLogger: Serilog.ILogger) : Logger =
            {
                Log =
                    fun ev ->
                        serilogLogger.Write(
                            ev.Level |> toSerilogLevel,
                            "{EventType} {@Event}",
                            ev.Message.GetType().Name,
                            ev.Message
                        )
                Enabled = fun level -> serilogLogger.IsEnabled(level |> toSerilogLevel)
            }


    let getRecommendedLogPath (componentName: string option) =
        let logDir = AppPath.logsDir ()
        Directory.CreateDirectory(logDir) |> ignore

        let componentName = componentName |> Option.defaultValue "general"
        let timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")
        let shortGuid = Guid.NewGuid().ToString("N").Substring(0, 4)
        let fileName = $"genpres_{componentName}_{timestamp}_{shortGuid}.log"

        Path.Combine(logDir, fileName)


    let parseLevel (raw: string option) : Level option =
        raw
        |> Option.bind (fun (s: string) ->
            match s.Trim().ToLowerInvariant() with
            | "d" -> Some Level.Debug
            | "i" -> Some Level.Informative
            | "w" -> Some Level.Warning
            | "e" -> Some Level.Error
            | _ -> None
        )

    /// stdout is the JSON-RPC transport channel for stdio-transport MCP, so every log event
    /// must go to stderr, never stdout: `standardErrorFromLevel = Nullable LogEventLevel.Verbose`
    /// routes every event (Verbose is below Debug, the lowest level) to stderr.
    let buildLogger (level: Level) : Serilog.Core.Logger * string =
        let minLevel = level |> SerilogBridge.toSerilogLevel
        let path = getRecommendedLogPath (Some "mcp")

        let logger =
            LoggerConfiguration()
                .MinimumLevel.Is(minLevel)
                .WriteTo.Console(standardErrorFromLevel = Nullable LogEventLevel.Verbose)
                // blockWhenFull: true — see the matching comment in
                // Informedica.GenPRES.Server/Logging.fs; the default silently drops events
                // once the queue is full instead of backing up the caller.
                .WriteTo.Async((fun a -> a.File(path) |> ignore), blockWhenFull = true)
                .CreateLogger()

        logger, path

    /// Returns the `Logger` port value plus a disposer: `Program.fs`'s `main` calls the
    /// disposer after `McpServer.run` returns.
    let getLogger (getEnv: string -> string option) : Logger * (unit -> unit) =
        match getEnv "GENPRES_LOG" |> parseLevel with
        | None -> Logging.noOp, ignore
        | Some level ->
            let serilogLogger, path = buildLogger level
            eprintfn $"[MCP] Logging at level %A{level}: stderr and %s{path}"

            serilogLogger :> Serilog.ILogger |> SerilogBridge.toLogger,
            (fun () -> (serilogLogger :> IDisposable).Dispose())
