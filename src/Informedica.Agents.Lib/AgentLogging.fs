namespace Informedica.Agents.Lib

open Informedica.Logging.Lib


/// Agent-based logging system
module AgentLogging =

    open System
    open System.Threading
    open System.Threading.Tasks
    open System.Text

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime

    module W = FileWriterAgent

    type LoggerMessage =
        | Start of path: string option * Level
        | LogEvent of Event
        | Report of AsyncReplyChannel<string[]> // formatted lines
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
            ReportAsync: unit -> Async<string[]> // formatted lines
            WriteAsync: string -> Async<Result<unit, string>>
            FlushAsync: unit -> Async<unit>
            StopAsync: unit -> Async<unit>
            DisposeWorkAsync: unit -> Async<DisposeResult>
        }

        interface IDisposable with
            member this.Dispose() = this.DisposeWorkAsync() |> Async.RunSynchronously |> ignore

        interface IAsyncDisposable with
            member this.DisposeAsync() =
                // Use ValueTask for proper async disposal
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


    /// Default configurations for AgentLogger
    module AgentLoggerDefaults =


        /// Default formatter that handles basic message types
        let defaultFormatter: IMessage -> string = fun msg -> $"%A{msg}"


        /// Default error handler that prints to stderr
        let defaultErrorHandler: LoggingError -> unit =
            function
            | FormatterError(ex, msg) -> eprintfn $"Formatter error for message %s{msg.GetType().Name}: %s{ex.Message}"
            | FileWriteError(ex, operation) -> eprintfn $"File write error during %s{operation}: %s{ex.Message}"
            | AgentError ex -> eprintfn $"Agent error: %s{ex.Message}"


        /// Create a default configuration with console-only logging
        let config: AgentLoggerConfig =
            {
                Formatter = defaultFormatter
                MaxMessages = Some 1000 // Keep the last 1000 messages in memory
                DefaultLevel = Level.Informative
                FlushThreshold = 100
                FlushInterval = TimeSpan.FromSeconds(5.0) // Auto-flush every 5 seconds
                MinFlushInterval = TimeSpan.FromSeconds(1.0)
                MaxFlushInterval = TimeSpan.FromSeconds(30.)
                ErrorHandler = Some defaultErrorHandler
            }


        /// Create the default configuration for high-performance logging
        let highPerformance: AgentLoggerConfig =
            {
                Formatter = defaultFormatter
                MaxMessages = Some 5000 // Larger buffer for high throughput
                DefaultLevel = Level.Warning // Only log warnings and errors
                FlushThreshold = 1000
                FlushInterval = TimeSpan.FromSeconds(10.0)
                MinFlushInterval = TimeSpan.FromSeconds(1.0)
                MaxFlushInterval = TimeSpan.FromSeconds(30.)
                ErrorHandler = Some defaultErrorHandler
            }


        /// Create default configuration for debugging
        let debug: AgentLoggerConfig =
            {
                Formatter = defaultFormatter
                MaxMessages = None // Unlimited message storage
                DefaultLevel = Level.Debug
                FlushThreshold = 10
                FlushInterval = TimeSpan.FromSeconds(1.0)
                MinFlushInterval = TimeSpan.FromSeconds(1.0)
                MaxFlushInterval = TimeSpan.FromSeconds(30.)
                ErrorHandler = Some defaultErrorHandler
            }


        /// Create the default configuration for production use
        let production: AgentLoggerConfig =
            {
                Formatter = defaultFormatter
                MaxMessages = Some 10_000 // Large buffer for production
                DefaultLevel = Level.Error // Only log errors in production
                FlushThreshold = 10
                FlushInterval = TimeSpan.FromSeconds(1.0)
                MinFlushInterval = TimeSpan.FromSeconds(1.0)
                MaxFlushInterval = TimeSpan.FromSeconds(30.)
                ErrorHandler = Some defaultErrorHandler
            }


        /// Create a custom configuration with a specified formatter
        let withFormatter (formatter: IMessage -> string) (config: AgentLoggerConfig) =
            { config with Formatter = formatter }


        /// Create a configuration with a custom message limit
        let withMaxMessages (maxMessages: int option) (config: AgentLoggerConfig) =
            { config with MaxMessages = maxMessages }


        /// Create a configuration with a custom default level
        let withLevel (level: Level) (config: AgentLoggerConfig) = { config with DefaultLevel = level }


        /// Create a configuration with a custom flush interval
        let withFlushInterval (interval: TimeSpan) (config: AgentLoggerConfig) =
            { config with FlushInterval = interval }


        /// Create a configuration with a custom flush threshold
        let withFlushThreshold (threshold: int) (config: AgentLoggerConfig) = { config with FlushThreshold = threshold }


        /// Create a configuration with a custom minimum flush interval
        let withMinFlushInterval (interval: TimeSpan) (config: AgentLoggerConfig) =
            { config with MinFlushInterval = interval }


        /// Create a configuration with a custom maximum flush interval
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
                                // Respect cancellation during delay
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
                        let text =
                            //AgentLoggerDefaults.defaultFormatter ev.Message
                            config.Formatter ev.Message

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
                                    // If switching to a different path, flush and close the previous writer to avoid leaks
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
                                // Reset initialization state when (re)starting a path
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
                                            // Create the file lazily on first writing by prepending a header
                                            if not fileInitialized then
                                                let header =
                                                    $"Start logging %A{level}: %s{DateTime.Now.ToShortTimeString()}"

                                                Array.append [| header; "" |] lines
                                                |> fun firstBatch -> W.append p firstBatch writer |> ignore

                                                scheduleFlush ()
                                                return! loop path level true
                                            // Already initialized; just append the message lines
                                            W.append p lines writer |> ignore // async writer
                                            scheduleFlush ()
                                        // In the console case, print all lines for clarity.
                                        | None ->
                                            if level = Level.Debug then
                                                lines |> Array.iter writeDebugMessage // print all lines
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
                                // produce formatted lines (oldest -> newest)
                                let lines =
                                    iterMessages ()
                                    |> Seq.mapi (fun i (t, e) -> formatLogMessage t (i + 1) e)
                                    |> Seq.choose id
                                    |> Seq.collect id
                                    |> Array.ofSeq

                                reply.Reply(lines)
                                return! loop path level fileInitialized

                            | Write(filePath, reply) ->
                                // fire-and-forget write of a snapshot
                                let result =
                                    try
                                        let allLines =
                                            iterMessages ()
                                            |> Seq.mapi (fun i (t, e) -> formatLogMessage t (i + 1) e)
                                            |> Seq.choose id
                                            |> Seq.collect id
                                            |> Array.ofSeq

                                        W.append filePath allLines writer |> ignore // no flush here
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
                        // First, stop the logger agent gracefully
                        do! logger.PostAndAsyncReply(fun rc -> Stop rc)

                        // Then clean up the file writer
                        do! W.flushAsync writer
                        do! W.stopAsync writer

                        // Finally, clean up other resources
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


        // Create the AgentLogger with proper disposal interfaces
        {
            Start =
                fun path level ->
                    ensureNotDisposed ()
                    // TODO: this can be a problem if there is a long list of messages still
                    // being processed
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
                        // Stop the pipeline; further logging will be ignored due to isDisposed flag
                        do! disposeAsync () |> Async.Ignore
                    }

            DisposeWorkAsync = fun () -> async { return! disposeAsync () }
        }


    /// Create a console logger with default settings
    let createConsole () = createAgentLogger AgentLoggerDefaults.config

    /// Create a debug logger with verbose settings
    let createDebug () = createAgentLogger AgentLoggerDefaults.debug

    /// Create a production logger with minimal output
    let createProduction () = createAgentLogger AgentLoggerDefaults.production

    /// Create a high-performance logger for heavy workloads
    let createHighPerformance () = createAgentLogger AgentLoggerDefaults.highPerformance

    /// Create a custom logger with the specified formatter
    let createWithFormatter (formatter: IMessage -> string) =
        AgentLoggerDefaults.config
        |> AgentLoggerDefaults.withFormatter formatter
        |> createAgentLogger

    /// Create a logger with unlimited message storage
    let createUnlimited () =
        AgentLoggerDefaults.config
        |> AgentLoggerDefaults.withMaxMessages None
        |> createAgentLogger
