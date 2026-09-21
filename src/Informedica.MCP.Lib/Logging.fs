namespace Informedica.MCP.Lib

open System
open System.IO

open Informedica.Utils.Lib
open Informedica.Logging.Lib

open Serilog
open Serilog.Events
open Serilog.Formatting


/// The MCP session's own IMessage, parallel to Informedica.GenPRES.Server's ServerLogging.Message.
[<RequireQualifiedAccess>]
type McpMessage =
    | ServerStarted of transport: string
    | ToolCallStarted of tool: string * argumentKeys: string[]
    | ToolCallCompleted of tool: string * elapsedMs: float
    | ToolCallFailed of tool: string * elapsedMs: float * reason: string

    interface IMessage


module McpMessage =

    /// The text of an MCP message; blank for a message of another type.
    let formatMessage (msg: IMessage) : string =
        match msg with
        | :? McpMessage as m ->
            match m with
            | McpMessage.ServerStarted transport -> $"MCP server started on %s{transport}"
            | McpMessage.ToolCallStarted(tool, argumentKeys) ->
                let keys = argumentKeys |> String.concat ", "
                $"tool %s{tool} started with arguments: %s{keys}"
            | McpMessage.ToolCallCompleted(tool, elapsedMs) -> $"tool %s{tool} completed in %.1f{elapsedMs} ms"
            | McpMessage.ToolCallFailed(tool, elapsedMs, reason) ->
                $"tool %s{tool} failed after %.1f{elapsedMs} ms: %s{reason}"
        | _ -> ""


/// Composition-root logging for the stdio MCP host: one Serilog-backed logger for the whole
/// session, gated by GENPRES_LOG exactly as the server's `loggingEnabled`/`loggingLevel` are
/// (unset or blank => logging off => Logging.noOp). Duplicated rather than referenced from
/// Informedica.GenPRES.Server, since Informedica.MCP.Lib/Informedica.MCP.Server are siblings
/// to that project and must not reference it.
module McpLogging =

    module Logging = Informedica.Logging.Lib.Logging

    /// Turns a Serilog ILogger into the domain-facing Logger record. The whole Event crosses as
    /// one scalar property: the thread that logs stores a reference and enqueues, and the text
    /// is rendered by the sink's formatter on the sink's own thread. That is safe because an
    /// Event and every message under it is immutable. This is the server's bridge written a
    /// second time: only the two executables refer to Serilog, and neither refers to the other.
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

        /// The text of a message: the order, solver and formulary renderings, and the MCP
        /// session's own messages.
        let formatMessage: IMessage -> string =
            MessageFormatter.create
                [
                    typeof<Informedica.GenOrder.Lib.Types.Logging.OrderMessage>,
                    Informedica.GenOrder.Lib.OrderLogging.formatOrderMessage
                    typeof<Informedica.GenSolver.Lib.Types.Logging.SolverMessage>,
                    Informedica.GenSolver.Lib.SolverLogging.formatSolverMessage
                    typeof<Informedica.GenForm.Lib.Types.Message>, Informedica.GenForm.Lib.FormLogging.formatMessage
                    typeof<McpMessage>, McpMessage.formatMessage
                ]

        /// The text and the type name of what a log event holds. A formatter that throws still
        /// yields a line saying so: Serilog drops an event whose formatter throws, and on this
        /// stream a dropped event is a lost one.
        let render (format: IMessage -> string) (logEvent: LogEvent) : string * string =
            match logEvent |> SerilogBridge.tryEvent with
            | None -> logEvent.RenderMessage(), "LogEvent"
            | Some ev ->
                let typeName = ev.Message.GetType().Name

                try
                    format ev.Message, typeName
                with ex ->
                    $"could not format %s{typeName}: %s{ex.Message}", typeName

        /// One compact JSON object per physical line: the time, the level, the type of the
        /// message and its text. A line feed inside the text is escaped, so one event is always
        /// one line, and `jq` reads the file as it is. An event whose text is blank writes
        /// nothing: the order and solver renderings return a blank for the events they leave
        /// out on purpose.
        type JsonLines(format: IMessage -> string) =

            interface ITextFormatter with
                member _.Format(logEvent: LogEvent, output: TextWriter) =
                    let text, typeName = logEvent |> render format

                    if String.IsNullOrEmpty text |> not then
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

        /// Readable text for the terminal: the time, the level, the text.
        type ConsoleText(format: IMessage -> string) =

            interface ITextFormatter with
                member _.Format(logEvent: LogEvent, output: TextWriter) =
                    let text, _ = logEvent |> render format

                    if String.IsNullOrEmpty text |> not then
                        output.Write(logEvent.Timestamp.ToString "HH:mm:ss")
                        output.Write " "
                        output.Write(logEvent.Level.ToString())
                        output.Write " "
                        output.WriteLine text


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

    /// The logger that writes to the file at `path`. Each sink sits behind its own queue and
    /// thread, so the thread that logs only enqueues. stdout is the JSON-RPC transport channel
    /// of a stdio MCP session, so the console sink writes every event to stderr, never stdout:
    /// `standardErrorFromLevel = Verbose` is below Debug, the lowest level there is.
    let buildLoggerAt (path: string) (level: Level) : Serilog.Core.Logger =
        LoggerConfiguration()
            .MinimumLevel.Is(level |> SerilogBridge.toSerilogLevel)
            .Destructure.AsScalar<Event>()
            // The file is the record and must not lose an event, so a full queue blocks the
            // caller instead of dropping. The queue is large, which makes that rare. The price is
            // memory: a stalled disk can hold this many events, each with the order it is about.
            .WriteTo.Async(
                (fun a -> a.File(EventFormat.JsonLines EventFormat.formatMessage, path) |> ignore),
                bufferSize = 100_000,
                blockWhenFull = true
            )
            // stderr is for watching, it may drop
            .WriteTo.Async(
                (fun a ->
                    a.Console(
                        EventFormat.ConsoleText EventFormat.formatMessage,
                        standardErrorFromLevel = Nullable LogEventLevel.Verbose
                    )
                    |> ignore
                ),
                blockWhenFull = false
            )
            .CreateLogger()

    let buildLogger (level: Level) : Serilog.Core.Logger * string =
        let path = getRecommendedLogPath (Some "mcp")

        buildLoggerAt path level, path

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
