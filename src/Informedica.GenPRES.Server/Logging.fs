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
open Serilog.Formatting


/// The log messages of the server and the functions that log and render them.
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

    /// The text of a server message; blank for a message of another type.
    let formatMessage (msg: IMessage) : string =
        match msg with
        | :? Message as m ->
            match m with
            | Request(method_, path, clientIP) -> $"%s{method_} %s{path} from %s{clientIP}"
            | Info s
            | Warning s
            | Error s -> s
        | _ -> ""


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


/// Deletes the oldest .log files in the directory of path when it holds more than MAX_LOG_FILES.
/// Runs once per logger type, when its file sink is built.
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


/// Wraps a Serilog logger as a Logger record. Each Event is passed as one scalar property and
/// rendered later on the thread of the sink; this is safe because an Event and its message are immutable.
module SerilogBridge =

    /// The name of the one property a log event from the bridge carries.
    [<Literal>]
    let EventProperty = "GenPresEvent"

    /// The message template that binds the Event to that property.
    [<Literal>]
    let Template = "{" + EventProperty + "}"

    let toSerilogLevel =
        function
        | Level.Debug -> LogEventLevel.Debug
        | Level.Informative -> LogEventLevel.Information
        | Level.Warning -> LogEventLevel.Warning
        | Level.Error -> LogEventLevel.Error

    let toLogger (serilogLogger: Serilog.ILogger) : Logger =
        {
            Log = fun ev -> serilogLogger.Write(ev.Level |> toSerilogLevel, Template, ev)
            Enabled = fun level -> serilogLogger.IsEnabled(level |> toSerilogLevel)
        }

    /// The Event a log event from the bridge carries; None for a log event from elsewhere.
    let tryEvent (logEvent: LogEvent) : Event option =
        match logEvent.Properties.TryGetValue EventProperty with
        | true, (:? ScalarValue as scalar) ->
            match scalar.Value with
            | :? Event as ev -> Some ev
            | _ -> None
        | _ -> None


/// Formats log events for the sinks. Formatting runs on the thread of the sink.
module EventFormat =

    /// Renders an order, solver, GenFORM or server message as text; any other message as "cannot format".
    let formatMessage: IMessage -> string =
        MessageFormatter.create
            [
                typeof<Informedica.GenOrder.Lib.Types.Logging.OrderMessage>,
                Informedica.GenOrder.Lib.OrderLogging.formatOrderMessage
                typeof<Informedica.GenSolver.Lib.Types.Logging.SolverMessage>,
                Informedica.GenSolver.Lib.SolverLogging.formatSolverMessage
                typeof<Informedica.GenForm.Lib.Types.Message>, Informedica.GenForm.Lib.FormLogging.formatMessage
                typeof<ServerLogging.Message>, ServerLogging.formatMessage
            ]

    /// Renders the text and the message type name of a log event once for all sinks, because rendering
    /// a solver event takes milliseconds. The text is cached as long as the Event is alive. A formatter
    /// that throws gives a line saying so, because Serilog would silently drop the event.
    type Renderer(format: IMessage -> string) =

        let texts = Runtime.CompilerServices.ConditionalWeakTable<Event, Lazy<string * string>>()

        let renderEvent (ev: Event) =
            lazy
                (let typeName = ev.Message.GetType().Name

                 try
                     format ev.Message, typeName
                 with ex ->
                     $"could not format %s{typeName}: %s{ex.Message}", typeName)

        member _.Render(logEvent: LogEvent) : string * string =
            match logEvent |> SerilogBridge.tryEvent with
            | None -> logEvent.RenderMessage(), "LogEvent"
            | Some ev -> texts.GetValue(ev, renderEvent).Value

    /// Writes each event as one JSON line with the time, the level, the message type and the text.
    /// Line feeds in the text are escaped, so one event is one line. An event with a blank text writes
    /// nothing; the order and solver formatters return a blank for the events they skip.
    type JsonLines(renderer: Renderer) =

        interface ITextFormatter with
            member _.Format(logEvent: LogEvent, output: TextWriter) =
                let text, typeName = renderer.Render logEvent

                if text |> String.notEmpty then
                    // the relaxed encoder keeps letters such as é and µ readable; quotes,
                    // backslashes and control characters are still escaped
                    let encoded =
                        Text.Json.JsonEncodedText.Encode(
                            text,
                            Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        )

                    output.Write "{\"@t\":\""
                    output.Write(logEvent.Timestamp.ToString "O")
                    output.Write "\",\"@l\":\""
                    output.Write(logEvent.Level.ToString())
                    output.Write "\",\"EventType\":\""
                    output.Write typeName
                    output.Write "\",\"Text\":\""
                    output.Write(encoded.ToString())
                    // a line feed on every platform, so that the file is the same everywhere
                    output.Write "\"}\n"

    /// Readable text for the terminal a developer is watching: the time, the level, the text.
    type ConsoleText(renderer: Renderer) =

        interface ITextFormatter with
            member _.Format(logEvent: LogEvent, output: TextWriter) =
                let text, _ = renderer.Render logEvent

                if text |> String.notEmpty then
                    output.Write(logEvent.Timestamp.ToString "HH:mm:ss")
                    output.Write " "
                    output.Write(logEvent.Level.ToString())
                    output.Write " "
                    output.WriteLine text


/// Builds one Serilog logger per LoggerType, lazily, so an unused logger type creates no file.
/// Each logger writes to a file, the record, and to the console, for the developer.
module SerilogLogging =

    /// Builds a logger that writes to the file at path and to the console. Each sink has its own queue
    /// and thread, so a slow console stalls neither the file nor a request.
    let buildLoggerAt (path: string) (level: Level) : Serilog.Core.Logger =
        // one renderer for both sinks, so that an event is rendered once
        let renderer = EventFormat.Renderer EventFormat.formatMessage

        LoggerConfiguration()
            .MinimumLevel.Is(level |> SerilogBridge.toSerilogLevel)
            .Destructure.AsScalar<Event>()
            // The file is the record and must not lose an event, so a full queue blocks the
            // caller instead of dropping. The large queue makes that rare, at the cost of memory:
            // a stalled disk can hold 100,000 events in memory.
            .WriteTo.Async(
                (fun a -> a.File(EventFormat.JsonLines renderer, path) |> ignore),
                bufferSize = 100_000,
                blockWhenFull = true
            )
            // the terminal is for watching, it may drop
            .WriteTo.Async((fun a -> a.Console(EventFormat.ConsoleText renderer) |> ignore), blockWhenFull = false)
            .CreateLogger()

    let buildLogger (level: Level) (loggerType: LoggerType) : Serilog.Core.Logger * string =
        let path = loggerType |> loggerTypeName |> Some |> getRecommendedLogPath

        pruneLogDirectory path

        buildLoggerAt path level, path

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


/// The loggers of the process, built once at the start-up level; None when GENPRES_LOG is unset or
/// not one of d, i, w or e. All callers use this value, so a process has one set of log files.
let serilogLoggers: Map<LoggerType, Lazy<Serilog.Core.Logger * string>> option =
    loggingLevel |> Option.map SerilogLogging.buildAll


/// Returns the Serilog logger for the logger type, or the no-op logger when logging is off.
let getLogger (loggerType: LoggerType) : Logger =
    match serilogLoggers with
    | None -> Informedica.Logging.Lib.Logging.noOp
    | Some loggers -> loggers |> SerilogLogging.getLogger <| loggerType


/// Disposes every logger this process forced into existence. Call from LoggerShutdown.
let disposeAll () = serilogLoggers |> Option.iter SerilogLogging.dispose
