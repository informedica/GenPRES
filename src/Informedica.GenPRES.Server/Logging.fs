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


/// Turns a Serilog ILogger into the domain-facing Logger record. The whole Event crosses as one
/// scalar property: the thread that logs stores a reference and enqueues, and the text is
/// rendered by the sink's formatter on the sink's own thread. That is safe because an Event and
/// every message under it is immutable.
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


/// What a sink writes for a log event. Rendering happens here, on the sink's thread.
module EventFormat =

    /// The text of a message: the order, solver and formulary renderings the log analysis reads,
    /// and the server's own messages.
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

    /// The text and the type name of what a log event holds, rendered once however many sinks
    /// ask for it: rendering is the expensive part of logging, a few milliseconds for a solver
    /// event, and the file and the console both need the text. The text is kept as long as the
    /// Event itself is alive and no longer. A formatter that throws still yields a line saying
    /// so: Serilog drops an event whose formatter throws, and on this stream a dropped event is
    /// a lost one.
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

    /// One compact JSON object per physical line: the time, the level, the type of the message
    /// and its text. A line feed inside the text is escaped, so one event is always one line,
    /// and `jq` reads the file as it is. An event whose text is blank writes nothing: the order
    /// and solver renderings return a blank for the events they leave out on purpose.
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


/// One Serilog sink per LoggerType, built lazily so a LoggerType that never logs never creates
/// a sink or a file. Both Console and File sinks are always active - Console for the terminal a
/// developer is watching, File for the persistent record.
module SerilogLogging =

    /// The logger that writes to the file at `path`. Each sink sits behind its own queue and
    /// thread, so the thread that logs only enqueues, and a slow terminal can stall neither the
    /// file nor a request.
    let buildLoggerAt (path: string) (level: Level) : Serilog.Core.Logger =
        // one renderer for both sinks, so that an event is rendered once
        let renderer = EventFormat.Renderer EventFormat.formatMessage

        LoggerConfiguration()
            .MinimumLevel.Is(level |> SerilogBridge.toSerilogLevel)
            .Destructure.AsScalar<Event>()
            // The file is the record and must not lose an event, so a full queue blocks the
            // caller instead of dropping. The queue is large, which makes that rare: the agent
            // logger this replaced queued without a bound and never blocked. The price is
            // memory: a stalled disk can hold this many events, each with the order it is about.
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
