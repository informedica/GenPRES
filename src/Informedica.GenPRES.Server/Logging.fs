module Logging

open System
open System.IO

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.Agents.Lib
open Informedica.Logging.Lib

open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime

open Serilog
open Serilog.Events


// Server-specific logging message types and helpers
module ServerLogging =

    module Logging = Informedica.Logging.Lib.Logging

    /// Messages used by the Server that can be logged
    type Message =
        | Request of method_: string * path: string * clientIP: string
        | Info of string
        | Warning of string
        | Error of string

        interface IMessage

    /// Log a request line as Informative
    let logRequest (logger: Logger) (method_: string) (path: string) (clientIP: string) =
        Request(method_, path, clientIP) |> Logging.logInfo logger


[<Literal>]
let MAX_LOG_FILES = 10_000


let getRecommendedLogPath (componentName: string option) =
    let logDir = AppPath.logsDir ()
    Directory.CreateDirectory(logDir) |> ignore

    let componentName = componentName |> Option.defaultValue "general"
    let timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")
    let shortGuid = Guid.NewGuid().ToString("N").Substring(0, 4)
    let fileName = $"genpres_{componentName}_{timestamp}_{shortGuid}.log"

    Path.Combine(logDir, fileName)


/// Prunes old log files in `path`'s directory before a Serilog file sink is created there. Runs
/// once per LoggerType actually used per process (SerilogLogging.buildLogger calls this once, at
/// sink construction), not once per request as the AgentLogging-backed version did.
let private pruneLogDirectory (path: string) =
    let dirAgent = FileDirectoryAgent.create ()

    let dir =
        match Path.GetDirectoryName path with
        | null
        | "" -> AppPath.rootPath ()
        | d -> d

    let policedAgent = dirAgent |> FileDirectoryAgent.setPolicyWithPattern dir MAX_LOG_FILES "*.log"

    async {
        let! pruned = policedAgent |> FileDirectoryAgent.pruneAsync path

        match pruned with
        | Ok n when n > 0 -> writeInfoMessage $"🧹 Pruned {n} old log file(s)\n"
        | Ok _ -> ()
        | Error s -> writeErrorMessage $"❌ Log path prune errored with: {s}\n"

        dirAgent |> Agent.dispose
    }
    |> Async.RunSynchronously


type LoggerType =
    | RequestLogger
    | OrderLogger
    | ResourcesLogger
    | FormularyLogger
    | OrderPlanLogger
    | ParenteraliaLogger


let allLoggerTypes =
    [
        RequestLogger
        OrderLogger
        ResourcesLogger
        FormularyLogger
        OrderPlanLogger
        ParenteraliaLogger
    ]


let loggerTypeName =
    function
    | RequestLogger -> "request"
    | OrderLogger -> "order"
    | ResourcesLogger -> "resources"
    | FormularyLogger -> "formulary"
    | OrderPlanLogger -> "orderplan"
    | ParenteraliaLogger -> "parenteralia"


let loggingEnabled =
    Env.getItem "GENPRES_LOG"
    |> Option.map (fun s -> s |> String.trim |> String.notEmpty)
    |> Option.defaultValue false


let loggingLevel =
    Env.getItem "GENPRES_LOG"
    |> Option.bind (fun (s: string) ->
        match s.Trim().ToLowerInvariant() with
        | "d" -> Level.Debug |> Some
        | "i" -> Level.Informative |> Some
        | "w" -> Level.Warning |> Some
        | "e" -> Level.Error |> Some
        | _ -> None
    )


/// Turns a Serilog ILogger into the domain-facing Logger record. Every IMessage case becomes a
/// Serilog structured property for free - no change to any IMessage type.
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


/// One Serilog sink per LoggerType, built lazily so a LoggerType that never logs never creates
/// a sink or a file. Both Console and File sinks are always active - Console for the terminal a
/// developer is watching, File (wrapped in Async) for the persistent record.
module SerilogLogging =

    let buildLogger (level: Level) (loggerType: LoggerType) : Serilog.Core.Logger * string =
        let minLevel = level |> SerilogBridge.toSerilogLevel
        let path = loggerType |> loggerTypeName |> Some |> getRecommendedLogPath

        pruneLogDirectory path

        let logger =
            LoggerConfiguration()
                .MinimumLevel.Is(minLevel)
                .WriteTo.Console()
                // blockWhenFull: true, because the default (false) silently drops events once
                // the 10_000-deep queue is full instead of backing up the caller — unacceptable
                // for a stream DEVELOPMENT.md documents as the medico-legal audit trail. This
                // is a real behavior change from the AgentLogging/MailboxProcessor design it
                // replaced, which queued without a bound rather than dropping.
                .WriteTo.Async((fun a -> a.File(path) |> ignore), blockWhenFull = true)
                .CreateLogger()

        logger, path

    let buildAll (level: Level) : Map<LoggerType, Lazy<Serilog.Core.Logger * string>> =
        allLoggerTypes
        |> List.map (fun lt -> lt, lazy (buildLogger level lt))
        |> Map.ofList

    let getLogger (loggers: Map<LoggerType, Lazy<Serilog.Core.Logger * string>>) (loggerType: LoggerType) : Logger =
        loggers[loggerType].Value |> fst :> Serilog.ILogger |> SerilogBridge.toLogger

    /// Disposes only the loggers actually forced into existence.
    let dispose (loggers: Map<LoggerType, Lazy<Serilog.Core.Logger * string>>) =
        for KeyValue(_, lazyLogger) in loggers do
            if lazyLogger.IsValueCreated then
                (lazyLogger.Value |> fst :> IDisposable).Dispose()


/// Built once, at the fixed startup level; `None` when GENPRES_LOG is unset. Every call site
/// reads through this, never through `SerilogLogging.buildAll` directly, so there is exactly
/// one map - and one set of files - per process.
let serilogLoggers: Map<LoggerType, Lazy<Serilog.Core.Logger * string>> option =
    loggingLevel |> Option.map SerilogLogging.buildAll


/// `Logging.noOp` when GENPRES_LOG is unset; otherwise the Serilog-backed Logger for that type.
let getLogger (loggerType: LoggerType) : Logger =
    match serilogLoggers with
    | None -> Informedica.Logging.Lib.Logging.noOp
    | Some loggers -> loggers |> SerilogLogging.getLogger <| loggerType


/// Disposes every logger this process forced into existence. Call from LoggerShutdown.
let disposeAll () = serilogLoggers |> Option.iter SerilogLogging.dispose
