// Prototype for docs/implementation-plans/416-standard-logging-library.md, Step 1
// (folds #378 Phase 1 steps 1+2, per 416's own renumbering): splits
// Informedica.Logging.Lib so the pure Logger/Event/Level/IMessage port stays there,
// and the IO-performing pieces (createConsole, createFile, the whole AgentLogging
// agent) move to Informedica.Agents.Lib, flipping the project reference to
// Agents.Lib -> Logging.Lib (today it is the other way round).
//
// Run: cd src/Informedica.Agents.Lib/Scripts && dotnet fsi 416-logging-split.fsx
//
// Script-only policy (AGENTS.md): this prototypes the split; the maintainer migrates
// it to source. Migration checklist once this script is reviewed:
//
//   - src/Informedica.Logging.Lib/Logging.fs keeps only the "TargetLoggingLib" section
//     below (IMessage/TimeStamp/Level/Event/Logger/Logging minus createConsole and
//     createFile/MessageFormatter). Delete createConsole, createFile and AgentLogging.
//   - New src/Informedica.Agents.Lib/ConsoleFileLogger.fs: the "ConsoleFileLogger"
//     module below (createConsole/createFile, unchanged logic, new home).
//   - New src/Informedica.Agents.Lib/AgentLogging.fs: the "AgentLogging" module below,
//     byte-for-byte the same as today's Logging.fs AgentLogging module - its only
//     cross-module dependency was Logging.levelValue, which stays in the trimmed port.
//   - Informedica.Agents.Lib.fsproj: add both new Compile items after FileDirectoryAgent.fs,
//     add a ProjectReference to Informedica.Logging.Lib.
//   - Informedica.Logging.Lib.fsproj: drop the ProjectReference to Informedica.Agents.Lib.
//   - scripts/DependencyRule.fsx: delete the allowedReferences entry
//     ("Informedica.Logging.Lib", "Informedica.Agents.Lib", ...) - the edge is now
//     Agents.Lib -> Logging.Lib (Infrastructure -> Core), which mayReference already
//     permits with no allowance needed.
//   - scripts/CheckDependencyRule.fsx: delete
//     `allowFile "src/Informedica.Logging.Lib/Logging.fs" loggingSplit` - the trimmed
//     file has no banned tokens left.
//   - Call sites, per 416 Step 1 and 378 Phase 1 step 2 ("Formatters stay in the
//     libraries"; grepped clean of other callers before writing this):
//       * src/Informedica.GenSOLVER.Lib/SolverLogging.fs: delete createLogger,
//         createFileLogger, createAgentLogger (zero production/test callers).
//         formatSolverMessage/logSolverEvent/logSolverWarning/logSolverException/
//         create/noOp stay untouched.
//       * src/Informedica.GenORDER.Lib/OrderLogging.fs: delete createLogger,
//         createFileLogger, createConsoleLogger, createAgentLogger. Three dev
//         call sites break and need a one-line fix to call
//         ConsoleFileLogger.createConsole/createFile directly with the same composed
//         formatter inline: Scripts/Medication.fsx:30, Scripts/Feeding.fsx:160,
//         Notebooks/total-parenteral-nutrition.dib:39 (all call createConsoleLogger/
//         createFileLogger). formatOrderMessage/logOrderEvent/etc. stay untouched.
//       * src/Informedica.GenFORM.Lib/FormLogging.fs: delete printLogger and
//         agentLogger - both unreferenced anywhere in the repo (only FormLogging.noOp
//         is used, in the server, MCP host and several dev scripts).
//       * src/Informedica.GenPRES.Server/Logging.fs: `getLogger` (line 91-101) does
//         `level |> getConfig |> OrderLogging.createAgentLogger`. Replace with the
//         composed formatter + AgentLogging.createAgentLogger call shown in
//         "Server/Logging.fs replacement" below - this is the inlining 378 Phase 1
//         step 2 calls for; no behaviour change, since OrderLogging.createAgentLogger
//         did exactly this composition today.
//       * tests/Informedica.Logging.Tests/Tests.fs: add a ProjectReference to
//         Informedica.Agents.Lib (it exercises AgentLogging directly today; this is a
//         missing reference, not a dependency-rule concern - the test project is
//         outside the production ring graph). Update its two calls at ~line 155 and
//         ~168 from Logging.createConsole/Logging.createFile to
//         ConsoleFileLogger.createConsole/createFile.
//
// No behaviour change is intended anywhere in this step; the smoke checks at the
// bottom exercise the same scenarios Logging.Tests already covers (noOp, console,
// file, combine, filterByLevel, filterByType, an AgentLogging round trip) so a
// regression would show here before it shows in the real test run.

#I __SOURCE_DIRECTORY__
#load "load.fsx"

open System
open System.Threading
open System.Threading.Tasks
open System.Text

open Informedica.Utils.Lib
open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
open Informedica.Agents.Lib


// ===========================================================================
// Target shape of src/Informedica.Logging.Lib/Logging.fs after the split.
// Identical to today's file with createConsole, createFile and AgentLogging removed.
// ===========================================================================
module TargetLoggingLib =

    type IMessage = interface end

    type TimeStamp = DateTime

    [<RequireQualifiedAccess>]
    type Level =
        | Debug
        | Informative
        | Warning
        | Error

    type Event =
        {
            TimeStamp: TimeStamp
            Level: Level
            Message: IMessage
        }

    type Logger =
        {
            Log: Event -> unit
            Enabled: Level -> bool
        }

    [<RequireQualifiedAccess>]
    module Logging =

        let createMessage level (msg: IMessage) =
            {
                TimeStamp = DateTime.Now
                Level = level
                Message = msg
            }

        let logWith level (logger: Logger) (msg: IMessage) =
            msg |> createMessage level |> logger.Log

        let logInfo logger msg = logWith Level.Informative logger msg
        let logWarning logger msg = logWith Level.Warning logger msg
        let logDebug logger msg = logWith Level.Debug logger msg
        let logError logger msg = logWith Level.Error logger msg

        let noOp: Logger =
            {
                Log = ignore
                Enabled = fun _ -> false
            }

        let create (f: Event -> unit) : Logger =
            {
                Log = f
                Enabled = fun _ -> true
            }

        let isEnabled level (logger: Logger) = logger.Enabled level

        let logLazy level (logger: Logger) (mk: unit -> IMessage) =
            if logger.Enabled level then
                mk () |> createMessage level |> logger.Log

        let logInfoLazy logger mk = logLazy Level.Informative logger mk
        let logWarningLazy logger mk = logLazy Level.Warning logger mk
        let logDebugLazy logger mk = logLazy Level.Debug logger mk

        let combine (loggers: Logger list) : Logger =
            { create (fun msg -> loggers |> List.iter (fun logger -> logger.Log msg)) with
                Enabled = fun level -> loggers |> List.exists (fun logger -> logger.Enabled level)
            }

        let levelValue =
            function
            | Level.Debug -> 0
            | Level.Informative -> 1
            | Level.Warning -> 2
            | Level.Error -> 3

        let filterByLevel (minLevel: Level) (logger: Logger) : Logger =
            { create (fun msg ->
                  if levelValue msg.Level >= levelValue minLevel then
                      logger.Log msg
              ) with
                Enabled = fun level -> levelValue level >= levelValue minLevel && logger.Enabled level
            }

        let filterByType<'T when 'T :> IMessage> (logger: Logger) : Logger =
            { create (fun msg ->
                  match msg.Message with
                  | :? 'T -> logger.Log msg
                  | _ -> ()
              ) with
                Enabled = logger.Enabled
            }

    [<RequireQualifiedAccess>]
    module MessageFormatter =

        let create (formatters: (Type * (IMessage -> string)) list) : IMessage -> string =
            fun msg ->
                let msgType = msg.GetType()

                formatters
                |> List.tryPick (fun (regType, formatter) ->
                    if regType.IsAssignableFrom(msgType) then Some formatter else None
                )
                |> Option.map (fun formatter -> formatter msg)
                |> Option.defaultValue $"cannot format: {msg}"

        let createWithFallback
            (formatters: (Type * (IMessage -> string)) list)
            (fallback: IMessage -> string)
            : IMessage -> string
            =
            fun msg ->
                let msgType = msg.GetType()

                formatters
                |> List.tryPick (fun (regType, formatter) ->
                    if regType.IsAssignableFrom(msgType) then Some formatter else None
                )
                |> Option.map (fun formatter -> formatter msg)
                |> Option.defaultWith (fun () -> fallback msg)


open TargetLoggingLib


// ===========================================================================
// New src/Informedica.Agents.Lib/ConsoleFileLogger.fs - moved out of Logging.fs
// unchanged; these do real IO, which is why they leave the Core-ring library.
// ===========================================================================
[<RequireQualifiedAccess>]
module ConsoleFileLogger =

    let createConsole (formatter: IMessage -> string) : Logger =
        Logging.create (fun msg ->
            msg.Message
            |> formatter
            |> fun s ->
                if not (String.IsNullOrEmpty s) then
                    printfn $"%s{s}"
        )

    let createFile path (formatter: IMessage -> string) : Logger =
        Logging.create (fun msg ->
            msg.Message
            |> formatter
            |> fun s ->
                if not (String.IsNullOrEmpty s) then
                    let text = [ $"{msg.TimeStamp}: {msg.Level}"; s ]
                    System.IO.File.AppendAllLines(path, text)
        )


// ===========================================================================
// New src/Informedica.Agents.Lib/AgentLogging.fs - moved verbatim out of Logging.fs.
// Its only cross-module reference into the old file was Logging.levelValue, which
// stays in the trimmed port, so nothing here changes besides its physical location.
// ===========================================================================
module AgentLogging =

    module W = FileWriterAgent

    type LoggerMessage =
        | Start of path: string option * Level
        | LogEvent of Event
        | Report of AsyncReplyChannel<string[]>
        | Write of string * AsyncReplyChannel<Result<unit, string>>
        | Stop of AsyncReplyChannel<unit>
        | FlushTimer

    type DisposeResult =
        | Disposed
        | AlreadyDisposed
        | DisposeError of exn

    type AgentLogger =
        {
            Start: string option -> Level -> unit
            Logger: Logger
            ReportAsync: unit -> Async<string[]>
            WriteAsync: string -> Async<Result<unit, string>>
            FlushAsync: unit -> Async<unit>
            StopAsync: unit -> Async<unit>
            DisposeWorkAsync: unit -> Async<DisposeResult>
        }

        interface IDisposable with
            member this.Dispose() =
                this.DisposeWorkAsync() |> Async.RunSynchronously |> ignore

        interface IAsyncDisposable with
            member this.DisposeAsync() =
                ValueTask(this.DisposeWorkAsync() |> Async.StartAsTask :> Task)

    type LoggingError =
        | FormatterError of exn * IMessage
        | FileWriteError of exn * string
        | AgentError of exn

    let mutable errorHandler: (LoggingError -> unit) option = None

    type AgentLoggerConfig =
        {
            Formatter: IMessage -> string
            MaxMessages: int option
            DefaultLevel: Level
            FlushThreshold: int
            FlushInterval: TimeSpan
            MinFlushInterval: TimeSpan
            MaxFlushInterval: TimeSpan
            ErrorHandler: (LoggingError -> unit) option
        }

    type MessageStorage<'T> =
        | RingBuffer of RingBuffer<'T>
        | UnlimitedList of ResizeArray<'T>

    module AgentLoggerDefaults =

        let defaultFormatter: IMessage -> string = fun msg -> $"%A{msg}"

        let defaultErrorHandler: LoggingError -> unit =
            function
            | FormatterError(ex, msg) -> eprintfn $"Formatter error for message %s{msg.GetType().Name}: %s{ex.Message}"
            | FileWriteError(ex, operation) -> eprintfn $"File write error during %s{operation}: %s{ex.Message}"
            | AgentError ex -> eprintfn $"Agent error: %s{ex.Message}"

        let config: AgentLoggerConfig =
            {
                Formatter = defaultFormatter
                MaxMessages = Some 1000
                DefaultLevel = Level.Informative
                FlushThreshold = 100
                FlushInterval = TimeSpan.FromSeconds(5.0)
                MinFlushInterval = TimeSpan.FromSeconds(1.0)
                MaxFlushInterval = TimeSpan.FromSeconds(30.)
                ErrorHandler = Some defaultErrorHandler
            }

        let highPerformance: AgentLoggerConfig =
            {
                Formatter = defaultFormatter
                MaxMessages = Some 5000
                DefaultLevel = Level.Warning
                FlushThreshold = 1000
                FlushInterval = TimeSpan.FromSeconds(10.0)
                MinFlushInterval = TimeSpan.FromSeconds(1.0)
                MaxFlushInterval = TimeSpan.FromSeconds(30.)
                ErrorHandler = Some defaultErrorHandler
            }

        let debug: AgentLoggerConfig =
            {
                Formatter = defaultFormatter
                MaxMessages = None
                DefaultLevel = Level.Debug
                FlushThreshold = 10
                FlushInterval = TimeSpan.FromSeconds(1.0)
                MinFlushInterval = TimeSpan.FromSeconds(1.0)
                MaxFlushInterval = TimeSpan.FromSeconds(30.)
                ErrorHandler = Some defaultErrorHandler
            }

        let production: AgentLoggerConfig =
            {
                Formatter = defaultFormatter
                MaxMessages = Some 10_000
                DefaultLevel = Level.Error
                FlushThreshold = 10
                FlushInterval = TimeSpan.FromSeconds(1.0)
                MinFlushInterval = TimeSpan.FromSeconds(1.0)
                MaxFlushInterval = TimeSpan.FromSeconds(30.)
                ErrorHandler = Some defaultErrorHandler
            }

        let withFormatter (formatter: IMessage -> string) (config: AgentLoggerConfig) =
            { config with Formatter = formatter }

        let withMaxMessages (maxMessages: int option) (config: AgentLoggerConfig) =
            { config with MaxMessages = maxMessages }

        let withLevel (level: Level) (config: AgentLoggerConfig) = { config with DefaultLevel = level }

        let withFlushInterval (interval: TimeSpan) (config: AgentLoggerConfig) =
            { config with FlushInterval = interval }

        let withFlushThreshold (threshold: int) (config: AgentLoggerConfig) =
            { config with FlushThreshold = threshold }

        let withMinFlushInterval (interval: TimeSpan) (config: AgentLoggerConfig) =
            { config with MinFlushInterval = interval }

        let withMaxFlushInterval (interval: TimeSpan) (config: AgentLoggerConfig) =
            { config with MaxFlushInterval = interval }

    let createAgentLogger (config: AgentLoggerConfig) =
        let cts = new CancellationTokenSource()
        let mutable isDisposed = 0L

        let writer = W.create ()

        let logger =
            Agent<LoggerMessage>.Start(fun inbox ->
                let timer = Diagnostics.Stopwatch.StartNew()

                let mutable pendingFlush = false
                let mutable lastFlushTime = DateTime.UtcNow
                let mutable messageCountSinceFlush = 0
                let minFlushInterval = config.MinFlushInterval
                let maxFlushInterval = config.MaxFlushInterval
                let flushThreshold = config.FlushThreshold

                let scheduleFlush () =
                    let now = DateTime.UtcNow

                    let interval =
                        if messageCountSinceFlush >= flushThreshold then
                            minFlushInterval
                        elif now - lastFlushTime > maxFlushInterval then
                            maxFlushInterval
                        else
                            config.FlushInterval

                    if
                        not pendingFlush
                        && interval > TimeSpan.Zero
                        && not cts.Token.IsCancellationRequested
                    then
                        pendingFlush <- true

                        async {
                            try
                                do! Async.AwaitTask(Task.Delay(interval, cts.Token))

                                if not cts.Token.IsCancellationRequested then
                                    inbox.Post(FlushTimer)
                            with
                            | :? OperationCanceledException -> ()
                            | :? ObjectDisposedException -> ()
                        }
                        |> Async.Start

                let storage =
                    match config.MaxMessages with
                    | Some n when n > 0 -> RingBuffer.create n |> RingBuffer
                    | _ -> ResizeArray<float * Event>() |> UnlimitedList

                let addMessage elapsed ev =
                    match storage with
                    | RingBuffer rb -> RingBuffer.add (elapsed, ev) rb
                    | UnlimitedList bag -> bag.Add(elapsed, ev)

                let clearMessages () =
                    match storage with
                    | RingBuffer rb -> RingBuffer.clear rb
                    | UnlimitedList bag -> bag.Clear()

                let iterMessages () : seq<float * Event> =
                    match storage with
                    | RingBuffer rb -> RingBuffer.toSeq rb
                    | UnlimitedList bag -> bag :> _

                let countMessages () =
                    match storage with
                    | RingBuffer rb -> rb.CountValue
                    | UnlimitedList bag -> bag.Count

                let sb = StringBuilder(1024)

                let formatLogMessage (elapsed: float) (count: int) (ev: Event) =
                    try
                        let text = config.Formatter ev.Message

                        if String.IsNullOrWhiteSpace text then
                            None
                        else
                            sb.Clear() |> ignore

                            let header =
                                sb.AppendFormat("{0}. {1:F3}: {2}", count, elapsed, ev.Level)
                                |> StringBuilder.toString

                            Some [| header; text |]
                    with ex ->
                        errorHandler |> Option.iter (fun h -> h (FormatterError(ex, ev.Message)))
                        Some [| $"ERROR: Failed to format message: %s{ex.Message}"; "" |]

                let rec loop (path: string option) (level: Level) (fileInitialized: bool) =
                    async {
                        let! msgOpt = inbox.TryReceive(1000)

                        match msgOpt with
                        | None ->
                            if cts.Token.IsCancellationRequested then
                                return ()
                            else
                                return! loop path level fileInitialized

                        | Some msg ->
                            match msg with
                            | Stop reply ->
                                timer.Stop()
                                do! W.flushAsync writer
                                reply.Reply(())
                                return ()

                            | Start(newPath, newLevel) ->
                                try
                                    match path, newPath with
                                    | Some oldPath, Some newP when
                                        not (String.Equals(oldPath, newP, StringComparison.Ordinal))
                                        ->

                                        W.flush writer |> ignore
                                        W.close oldPath writer |> ignore
                                        clearMessages ()

                                    | _ -> ()
                                with ex ->
                                    ConsoleWriter.writeErrorMessage
                                        $"unexpected error in logging agent:\n{ex}"
                                        true
                                        true

                                return! loop newPath newLevel false

                            | LogEvent ev ->
                                let shouldLog = Logging.levelValue ev.Level >= Logging.levelValue level

                                if shouldLog then
                                    let elapsed = timer.Elapsed.TotalSeconds
                                    addMessage elapsed ev
                                    let idx = countMessages ()

                                    match formatLogMessage elapsed idx ev with
                                    | Some lines ->
                                        match path with
                                        | Some p ->
                                            if not fileInitialized then
                                                let header =
                                                    $"Start logging %A{level}: %s{DateTime.Now.ToShortTimeString()}"

                                                Array.append [| header; "" |] lines
                                                |> fun firstBatch -> W.append p firstBatch writer |> ignore

                                                scheduleFlush ()
                                                return! loop path level true

                                            W.append p lines writer |> ignore
                                            scheduleFlush ()
                                        | None ->
                                            if level = Level.Debug then
                                                lines |> Array.iter writeDebugMessage
                                    | None -> ()

                                    messageCountSinceFlush <- messageCountSinceFlush + 1
                                    scheduleFlush ()

                                return! loop path level fileInitialized

                            | FlushTimer ->
                                pendingFlush <- false

                                try
                                    do! W.flushAsync writer
                                    lastFlushTime <- DateTime.UtcNow
                                    messageCountSinceFlush <- 0
                                with ex ->
                                    config.ErrorHandler |> Option.iter (fun h -> FileWriteError(ex, "flush") |> h)

                                return! loop path level fileInitialized

                            | Report reply ->
                                let lines =
                                    iterMessages ()
                                    |> Seq.mapi (fun i (t, e) -> formatLogMessage t (i + 1) e)
                                    |> Seq.choose id
                                    |> Seq.collect id
                                    |> Array.ofSeq

                                reply.Reply(lines)
                                return! loop path level fileInitialized

                            | Write(filePath, reply) ->
                                let result =
                                    try
                                        let allLines =
                                            iterMessages ()
                                            |> Seq.mapi (fun i (t, e) -> formatLogMessage t (i + 1) e)
                                            |> Seq.choose id
                                            |> Seq.collect id
                                            |> Array.ofSeq

                                        W.append filePath allLines writer |> ignore
                                        Ok()
                                    with ex ->
                                        Error ex.Message

                                reply.Reply(result)
                                return! loop path level fileInitialized
                    }

                loop None config.DefaultLevel false
            )

        logger.Error.Add(fun ex -> eprintfn $"Agent error: %s{ex.Message}")
        logger.OnError.Add(fun ex -> eprintfn $"Agent body error: %s{ex.Message}")

        let ensureNotDisposed () =
            if Interlocked.Read(&isDisposed) = 1L then
                invalidOp "Logger agent has been disposed"

        let disposeAsync () =
            async {
                if Interlocked.CompareExchange(&isDisposed, 1L, 0L) = 0L then
                    try
                        do! logger.PostAndAsyncReply(fun rc -> Stop rc)
                        do! W.flushAsync writer
                        do! W.stopAsync writer
                        cts.Cancel()
                        cts.Dispose()
                        writer |> Agent.dispose
                        logger |> Agent.dispose
                        return Disposed
                    with ex ->
                        eprintfn $"Error during disposal: %s{ex.Message}"
                        return DisposeError ex
                else
                    return AlreadyDisposed
            }

        {
            Start =
                fun path level ->
                    ensureNotDisposed ()
                    do logger.Post(Start(path, level))

            Logger =
                {
                    Log =
                        fun ev ->
                            if Interlocked.Read(&isDisposed) = 0L then
                                try
                                    logger.Post(LogEvent ev)
                                with
                                | :? ObjectDisposedException -> ()
                                | ex -> eprintfn $"Failed to post log event {ev} with:\n{ex.Message}"
                    Enabled = fun _ -> true
                }

            ReportAsync =
                fun () ->
                    async {
                        ensureNotDisposed ()
                        return! logger.PostAndAsyncReply(fun rc -> Report rc)
                    }

            WriteAsync =
                fun path ->
                    async {
                        ensureNotDisposed ()
                        return! logger.PostAndAsyncReply(fun rc -> Write(path, rc))
                    }

            FlushAsync =
                fun () ->
                    async {
                        ensureNotDisposed ()
                        logger.Post(FlushTimer)
                        return ()
                    }

            StopAsync =
                fun () ->
                    async {
                        do! disposeAsync () |> Async.Ignore
                    }

            DisposeWorkAsync = fun () -> async { return! disposeAsync () }
        }

    let createConsole () = createAgentLogger AgentLoggerDefaults.config
    let createDebug () = createAgentLogger AgentLoggerDefaults.debug
    let createProduction () = createAgentLogger AgentLoggerDefaults.production
    let createHighPerformance () = createAgentLogger AgentLoggerDefaults.highPerformance

    let createWithFormatter (formatter: IMessage -> string) =
        AgentLoggerDefaults.config
        |> AgentLoggerDefaults.withFormatter formatter
        |> createAgentLogger

    let createUnlimited () =
        AgentLoggerDefaults.config
        |> AgentLoggerDefaults.withMaxMessages None
        |> createAgentLogger


// ===========================================================================
// Server/Logging.fs replacement for the getLogger call site (line 91-101 today):
// today `level |> getConfig |> OrderLogging.createAgentLogger` builds the config
// then asks OrderLogging to overwrite its Formatter with the OrderMessage/
// SolverMessage/GenForm.Message composition and call AgentLogging.createAgentLogger.
// Once OrderLogging.createAgentLogger is deleted, Server/Logging.fs does that
// composition itself - it already references GenOrder.Lib, so this is inlining, not
// new design.
// ===========================================================================
module ServerLoggingReplacement =

    // Stand-ins for the real OrderMessage/SolverMessage/GenForm.Types.Message
    // formatters, which stay in their libraries unchanged.
    type StubOrderMessage =
        | StubOrder of string

        interface IMessage

    let formatOrderMessage (msg: IMessage) =
        match msg with
        | :? StubOrderMessage as (StubOrder s) -> s
        | _ -> "unknown"

    let composedFormatter =
        MessageFormatter.create [ (typeof<StubOrderMessage>, formatOrderMessage) ]

    let getLogger (config: AgentLogging.AgentLoggerConfig) =
        config
        |> AgentLogging.AgentLoggerDefaults.withFormatter composedFormatter
        |> AgentLogging.createAgentLogger


// ===========================================================================
// Smoke checks - same scenarios tests/Informedica.Logging.Tests/Tests.fs covers today.
// ===========================================================================

type TestMessage =
    | TestMessage of string

    interface IMessage

let testFormatter (msg: IMessage) =
    match msg with
    | :? TestMessage as (TestMessage s) -> s
    | _ -> "?"

let assertTrue name cond =
    if cond then
        printfn $"PASS: {name}"
    else
        failwithf $"FAIL: {name}"

// noOp never logs
Logging.logInfo Logging.noOp (TestMessage "ignored")
assertTrue "noOp does not throw" true

// combine + filterByLevel
let mutable seenA = 0
let mutable seenB = 0
let loggerA = Logging.create (fun _ -> seenA <- seenA + 1)
let loggerB = Logging.create (fun _ -> seenB <- seenB + 1) |> Logging.filterByLevel Level.Warning
let combined = Logging.combine [ loggerA; loggerB ]
Logging.logInfo combined (TestMessage "info")
Logging.logWarning combined (TestMessage "warn")
assertTrue "combine: unfiltered logger sees both" (seenA = 2)
assertTrue "combine: level-filtered logger sees only the warning" (seenB = 1)

// filterByType
let mutable typedSeen = 0
type OtherMessage =
    | OtherMessage of string

    interface IMessage

let typedLogger =
    Logging.create (fun _ -> typedSeen <- typedSeen + 1)
    |> Logging.filterByType<TestMessage>

Logging.logInfo typedLogger (TestMessage "match")
Logging.logInfo typedLogger (OtherMessage "no match")
assertTrue "filterByType only sees the matching case" (typedSeen = 1)

// ConsoleFileLogger.createConsole
let consoleLogger = ConsoleFileLogger.createConsole testFormatter
Logging.logInfo consoleLogger (TestMessage "console smoke test")

// ConsoleFileLogger.createFile
let tempFile = IO.Path.GetTempFileName()
let fileLogger = ConsoleFileLogger.createFile tempFile testFormatter
Logging.logInfo fileLogger (TestMessage "file smoke test")
Threading.Thread.Sleep 50
let fileContents = IO.File.ReadAllText tempFile
IO.File.Delete tempFile
assertTrue "file logger wrote the formatted message" (fileContents.Contains "file smoke test")

// AgentLogging round trip: start on a temp path, log, flush, verify file content, dispose
let agentTempFile = IO.Path.GetTempFileName()

async {
    let agentLogger = AgentLogging.createWithFormatter testFormatter
    agentLogger.Start (Some agentTempFile) Level.Debug
    TestMessage "agent smoke test" |> Logging.logInfo agentLogger.Logger
    do! agentLogger.FlushAsync()
    do! Async.Sleep 200
    do! agentLogger.StopAsync()

    let contents = IO.File.ReadAllText agentTempFile
    IO.File.Delete agentTempFile
    assertTrue "AgentLogging wrote the formatted message to its file" (contents.Contains "agent smoke test")
}
|> Async.RunSynchronously

printfn "\nAll smoke checks passed - the split compiles and behaves the same as today's Logging.fs."
