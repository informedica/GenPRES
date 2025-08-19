namespace Informedica.Logging.Lib

open System


/// General message types
type IMessage = interface end


type TimeStamp = DateTime


[<RequireQualifiedAccess>]
type Level =
    | Informative
    | Warning
    | Debug
    | Error


type Event =
    {
        TimeStamp: TimeStamp
        Level: Level
        Message: IMessage
    }


type Logger = { Log: Event -> unit }


/// General logging module
[<RequireQualifiedAccess>]
module Logging =
    

    /// Create a message with timestamp and level
    let createMessage level (msg: IMessage) =
        {
            TimeStamp = DateTime.Now
            Level = level
            Message = msg
        }

    /// Log a message with a specific level
    let logWith level (logger: Logger) (msg: IMessage) =
        msg
        |> createMessage level
        |> logger.Log


    /// Log an informative message
    let logInfo logger msg = logWith Level.Informative logger msg


    /// Log a warning message  
    let logWarning logger msg = logWith Level.Warning logger msg


    /// Log a debug message
    let logDebug logger msg = logWith Level.Debug logger msg


    /// Log an error message
    let logError logger msg = logWith Level.Error logger msg


    /// A logger that does nothing
    let ignore : Logger = { Log = ignore }


    /// Create a logger that uses the given function to process messages
    let create (f: Event -> unit) : Logger = { Log = f }


    /// Create a logger that prints to console using a message formatter
    let createConsole (formatter: IMessage -> string) : Logger =
        create (fun msg -> 
            msg.Message 
            |> formatter 
            |> fun s -> if not (String.IsNullOrEmpty s) then printfn "%s" s
        )


    /// Create a logger that writes to file using a message formatter
    let createFile path (formatter: IMessage -> string) : Logger =
        create (fun msg ->
            msg.Message
            |> formatter
            |> fun s -> 
                if not (String.IsNullOrEmpty s) then
                    let text = [$"{msg.TimeStamp}: {msg.Level}"; s]
                    System.IO.File.AppendAllLines(path, text)
        )


    /// Combine multiple loggers into one
    let combine (loggers: Logger list) : Logger =
        create (fun msg ->
            loggers |> List.iter (fun logger -> logger.Log msg)
        )


    /// Filter messages by level
    let filterByLevel (minLevel: Level) (logger: Logger) : Logger =
        let levelValue = function
            | Level.Informative -> 0
            | Level.Debug -> 1
            | Level.Warning -> 2
            | Level.Error -> 3
        
        create (fun msg ->
            if levelValue msg.Level >= levelValue minLevel then
                logger.Log msg
        )


    /// Filter messages by type
    let filterByType<'T when 'T :> IMessage> (logger: Logger) : Logger =
        create (fun msg ->
            match msg.Message with
            | :? 'T -> logger.Log msg
            | _ -> ()
        )


/// Message formatter module
[<RequireQualifiedAccess>]
module MessageFormatter =
    
    /// Create a formatter that handles multiple message types
    let create (formatters: (Type * (IMessage -> string)) list) : IMessage -> string =
        fun msg ->
            let msgType = msg.GetType()
            //printfn $"msgType = {msgType} in {formatters |> List.map (fst >> _.FullName)}"
            
            // Try to find a formatter where the registered type is assignable from the message type
            formatters
            |> List.tryPick (fun (regType, formatter) ->
                if regType.IsAssignableFrom(msgType) then
                    Some formatter
                else None
            )
            |> Option.map (fun formatter -> formatter msg)
            |> Option.defaultValue ""


    /// Create a formatter with fallback
    let createWithFallback (formatters: (Type * (IMessage -> string)) list) (fallback: IMessage -> string) : IMessage -> string =
        fun msg ->
            let msgType = msg.GetType()
            
            // Try to find a formatter where the registered type is assignable from the message type
            formatters
            |> List.tryPick (fun (regType, formatter) ->
                if regType.IsAssignableFrom(msgType) then
                    Some formatter
                else None
            )
            |> Option.map (fun formatter -> formatter msg)
            |> Option.defaultWith (fun () -> fallback msg)


/// Agent-based logging system
module AgentLogging =

    open System.IO
    open System.Threading
    open System.Text

    open Informedica.Utils.Lib
    open Informedica.Agents.Lib

    module W = FileWriterAgent

    // ... your Level, IMessage, Event, Logger types here ...

    type LoggerMessage =
        | Start  of path: string option * Level * AsyncReplyChannel<Result<unit, string>>
        | LogEvent of Event
        | Report of AsyncReplyChannel<string[]>                // formatted lines
        | Write  of string * AsyncReplyChannel<Result<unit, string>>
        | Stop   of AsyncReplyChannel<unit>
        | FlushTimer 

    type DisposeResult =
        | Disposed
        | AlreadyDisposed
        | DisposeError of exn


    type AgentLogger =
        {
            StartAsync  : string option -> Level -> Async<Result<unit, string>>
            Logger      : Logger
            ReportAsync : unit -> Async<string[]>               // formatted lines
            WriteAsync  : string -> Async<Result<unit, string>>
            StopAsync   : unit -> Async<unit>
            DisposeAsync: unit -> Async<DisposeResult> 
        }
        interface IDisposable with
            member this.Dispose() = this.DisposeAsync() |> Async.RunSynchronously |> ignore


    type LoggingError =
        | FormatterError of exn * IMessage
        | FileWriteError of exn * string
        | AgentError of exn


    let mutable errorHandler : (LoggingError -> unit) option = None


    type AgentLoggerConfig = {
        Formatter: IMessage -> string
        MaxMessages: int option
        DefaultLevel: Level
        FlushInterval: TimeSpan
        ErrorHandler: (LoggingError -> unit) option
    }


    /// Default configurations for AgentLogger
    module AgentLoggerDefaults =
        

        /// Default formatter that handles basic message types
        let defaultFormatter : IMessage -> string =
            fun msg -> sprintf "%A" msg
        

        /// Default error handler that prints to stderr
        let defaultErrorHandler : LoggingError -> unit =
            function
            | FormatterError (ex, msg) ->
                eprintfn "Formatter error for message %s: %s" (msg.GetType().Name) ex.Message
            | FileWriteError (ex, operation) ->
                eprintfn "File write error during %s: %s" operation ex.Message
            | AgentError ex ->
                eprintfn "Agent error: %s" ex.Message
        

        /// Create default configuration with console-only logging
        let console : AgentLoggerConfig = {
            Formatter = defaultFormatter
            MaxMessages = Some 1000  // Keep last 1000 messages in memory
            DefaultLevel = Level.Informative
            FlushInterval = TimeSpan.FromSeconds(5.0)  // Auto-flush every 5 seconds
            ErrorHandler = Some defaultErrorHandler
        }
        

        /// Create default configuration for high-performance logging
        let highPerformance : AgentLoggerConfig = {
            Formatter = defaultFormatter
            MaxMessages = Some 5000  // Larger buffer for high throughput
            DefaultLevel = Level.Warning  // Only log warnings and errors
            FlushInterval = TimeSpan.FromSeconds(10.0)  // Less frequent flushing
            ErrorHandler = Some defaultErrorHandler
        }
        

        /// Create default configuration for debugging
        let debug : AgentLoggerConfig = {
            Formatter = defaultFormatter
            MaxMessages = None  // Unlimited message storage
            DefaultLevel = Level.Debug
            FlushInterval = TimeSpan.FromSeconds(1.0)  // Frequent flushing for immediate feedback
            ErrorHandler = Some defaultErrorHandler
        }
        

        /// Create default configuration for production use
        let production : AgentLoggerConfig = {
            Formatter = defaultFormatter
            MaxMessages = Some 10_000  // Large buffer for production
            DefaultLevel = Level.Error  // Only log errors in production
            FlushInterval = TimeSpan.FromSeconds(30.0)  // Less frequent flushing
            ErrorHandler = Some defaultErrorHandler
        }
        

        /// Create a custom configuration with specified formatter
        let withFormatter (formatter: IMessage -> string) : AgentLoggerConfig = {
            console with Formatter = formatter
        }
        

        /// Create a configuration with custom message limit
        let withMaxMessages (maxMessages: int option) : AgentLoggerConfig = {
            console with MaxMessages = maxMessages
        }
        

        /// Create a configuration with custom flush interval
        let withFlushInterval (interval: TimeSpan) : AgentLoggerConfig = {
            console with FlushInterval = interval
        }
        

        /// Create a configuration with custom default level
        let withLevel (level: Level) : AgentLoggerConfig = {
            console with DefaultLevel = level
        }


    let createAgentLogger (config: AgentLoggerConfig) =
        let cts = new CancellationTokenSource()
        let mutable isDisposed = 0L

        let writer = W.create()

        let logger =
            Agent<LoggerMessage>.Start(fun inbox ->
                let timer = Diagnostics.Stopwatch.StartNew()
                let mutable pendingFlush = false
                let mutable lastFlushTime = DateTime.UtcNow
                let mutable messageCountSinceFlush = 0
                let minFlushInterval = TimeSpan.FromSeconds(1.0)
                let maxFlushInterval = TimeSpan.FromSeconds(10.0)
                let flushThreshold = 100                

                let scheduleFlush() =
                    let now = DateTime.UtcNow
                    let interval =
                        if messageCountSinceFlush >= flushThreshold then minFlushInterval
                        elif now - lastFlushTime > maxFlushInterval then maxFlushInterval
                        else config.FlushInterval

                    if not pendingFlush && interval > TimeSpan.Zero && not cts.Token.IsCancellationRequested then
                        pendingFlush <- true
                        async {
                            try
                                do! Async.Sleep (int interval.TotalMilliseconds)
                                if not cts.Token.IsCancellationRequested then
                                    inbox.Post(FlushTimer)
                            with
                            | :? OperationCanceledException -> ()
                            | :? ObjectDisposedException -> ()
                        } |> Async.Start

                // choose storage
                let ringOpt =
                    match config.MaxMessages with
                    | Some n when n > 0 -> Some (RingBuffer.create n)
                    | _ -> None

                let bag =
                    match ringOpt with
                    | Some _ -> Unchecked.defaultof<ResizeArray<float * Event>> // unused
                    | None -> ResizeArray<float * Event>()

                let addMessage elapsed ev =
                    match ringOpt with
                    | Some rb -> RingBuffer.add (elapsed, ev) rb
                    | None -> bag.Add (elapsed, ev)

                let iterMessages () : seq<float * Event> =
                    match ringOpt with
                    | Some rb -> RingBuffer.toSeq rb
                    | None -> bag :> _

                let countMessages () =
                    match ringOpt with
                    | Some rb -> rb.CountValue
                    | None -> bag.Count

                let sb = StringBuilder(1024)

                let formatLogMessage (elapsed: float) (count: int) (ev: Event) =
                    try
                        let text = 
                            //AgentLoggerDefaults.defaultFormatter ev.Message 
                            config.Formatter ev.Message
                        if String.IsNullOrWhiteSpace text then None
                        else 
                            sb.Clear() |> ignore
                            let header =
                                sb.AppendFormat("{0}. {1:F3}: {2}", count, elapsed, ev.Level) 
                                |> StringBuilder.toString
                            Some [| header; text |]
                    with ex ->
                        errorHandler |> Option.iter (fun h -> h (FormatterError(ex, ev.Message)))
                        Some [| sprintf "ERROR: Failed to format message: %s" ex.Message; "" |]

                let rec loop (path: string option) (level: Level) = 
                    async {
                        let! msgOpt = inbox.TryReceive(1000)
                        match msgOpt with
                        | None ->
                            if cts.Token.IsCancellationRequested then return ()
                            else return! loop path level

                        | Some msg ->
                            match msg with
                            | Stop reply ->
                                timer.Stop()
                                do! W.flushAsync writer
                                reply.Reply(())
                                return ()

                            | Start (newPath, newLevel, reply) ->
                                let res =
                                    try
                                        match newPath with
                                        | Some p ->
                                            // Initialize file using FileWriterAgent to keep encoding consistent (no BOM)
                                            let header = sprintf "Start logging %A: %s" newLevel (DateTime.Now.ToShortTimeString())
                                            // Clear existing file and write header + an empty line
                                            W.clear p writer |> ignore
                                            W.append p [| header; "" |] writer |> ignore
                                        | None -> ()
                                        Ok ()
                                    with ex -> Error ex.Message
                                reply.Reply(res)
                                return! loop newPath newLevel

                            | LogEvent ev ->
                                let shouldLog =
                                    let levelValue = function
                                        | Level.Informative -> 0
                                        | Level.Debug -> 1
                                        | Level.Warning -> 2
                                        | Level.Error -> 3
                                    levelValue ev.Level >= levelValue level
                                if shouldLog then
                                    let elapsed = timer.Elapsed.TotalSeconds
                                    addMessage elapsed ev
                                    let idx = countMessages()
                                    match formatLogMessage elapsed idx ev with
                                    | Some lines ->
                                        match path with
                                        | Some p -> 
                                            W.append p lines writer |> ignore      // async writer
                                            scheduleFlush ()
                                        | None   -> printfn "%s" lines[1]        // print body only
                                    | None -> ()
                                    messageCountSinceFlush <- messageCountSinceFlush + 1
                                    scheduleFlush ()
                                return! loop path level

                            | FlushTimer ->
                                pendingFlush <- false
                                try
                                    do! W.flushAsync writer
                                    lastFlushTime <- DateTime.UtcNow
                                    messageCountSinceFlush <- 0
                                with 
                                | ex -> config.ErrorHandler |> Option.iter (fun h -> FileWriteError (ex, "flush") |> h)
                                return! loop path level

                            | Report reply ->
                                // produce formatted lines (oldest -> newest)
                                let lines =
                                    iterMessages()
                                    |> Seq.mapi (fun i (t, e) -> formatLogMessage t (i + 1) e)
                                    |> Seq.choose id
                                    |> Seq.collect id
                                    |> Array.ofSeq
                                reply.Reply(lines)
                                return! loop path level

                            | Write (filePath, reply) ->
                                // fire-and-forget write of a snapshot
                                let result =
                                    try
                                        let allLines =
                                            iterMessages()
                                            |> Seq.mapi (fun i (t, e) -> formatLogMessage t (i + 1) e)
                                            |> Seq.choose id
                                            |> Seq.collect id
                                            |> Array.ofSeq
                                        W.append filePath allLines writer |> ignore // no flush here
                                        Ok ()
                                    with ex -> Error ex.Message
                                reply.Reply(result)
                                return! loop path level
                }

                loop None config.DefaultLevel
            )

        logger.Error.Add(fun ex -> eprintfn "Agent error: %s" ex.Message)
        logger.OnError.Add(fun ex -> eprintfn "Agent body error: %s" ex.Message)

        let ensureNotDisposed () =
            if Interlocked.Read(&isDisposed) = 1L then invalidOp "Logger agent has been disposed"

        {
            StartAsync = fun path level ->
                async {
                    ensureNotDisposed()
                    return! logger.PostAndAsyncReply(fun rc -> Start(path, level, rc))
                }

            Logger =
                { Log = fun ev ->
                    if Interlocked.Read(&isDisposed) = 0L then
                        logger.Post(LogEvent ev)
                }

            ReportAsync = fun () ->
                async {
                    ensureNotDisposed()
                    return! logger.PostAndAsyncReply(fun rc -> Report rc) // string[]
                }

            WriteAsync = fun path ->
                async {
                    ensureNotDisposed()
                    return! logger.PostAndAsyncReply(fun rc -> Write(path, rc))
                }

            StopAsync = fun () ->
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
                        with ex ->
                            eprintfn "Error stopping agent: %s" ex.Message
                }

            DisposeAsync = fun () ->
                async {
                    if Interlocked.CompareExchange(&isDisposed, 1L, 0L) = 0L then
                        try
                            // First stop the logger agent gracefully
                            do! logger.PostAndAsyncReply(fun rc -> Stop rc)
                            
                            // Then cleanup the file writer
                            do! W.flushAsync writer
                            do! W.stopAsync writer
                            
                            // Finally cleanup other resources
                            cts.Cancel()
                            cts.Dispose()
                            writer |> Agent.dispose
                            logger |> Agent.dispose
                            
                            return Disposed
                        with ex ->
                            eprintfn "Error during disposal: %s" ex.Message
                            return DisposeError ex
                    else
                        return AlreadyDisposed
                }
        }

    
    /// Create a console logger with default settings
    let createConsole () = 
        createAgentLogger AgentLoggerDefaults.console
    
    /// Create a debug logger with verbose settings
    let createDebug () = 
        createAgentLogger AgentLoggerDefaults.debug
    
    /// Create a production logger with minimal output
    let createProduction () = 
        createAgentLogger AgentLoggerDefaults.production
    
    /// Create a high-performance logger for heavy workloads
    let createHighPerformance () = 
        createAgentLogger AgentLoggerDefaults.highPerformance
    
    /// Create a custom logger with the specified formatter
    let createWithFormatter (formatter: IMessage -> string) =
        AgentLoggerDefaults.withFormatter formatter
        |> createAgentLogger
    
    /// Create a logger with unlimited message storage
    let createUnlimited () =
        AgentLoggerDefaults.withMaxMessages None
        |> createAgentLogger


