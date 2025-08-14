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
        // Create a map using Type.FullName as the key
        let formatterMap = 
            formatters 
            |> List.map (fun (t, f) -> t.FullName, f)
            |> Map.ofList
        
        fun msg ->
            let msgType = msg.GetType()
            formatterMap
            |> Map.tryFind msgType.FullName
            |> Option.map (fun formatter -> formatter msg)
            |> Option.defaultValue ""


    /// Create a formatter with fallback
    let createWithFallback (formatters: (Type * (IMessage -> string)) list) (fallback: IMessage -> string) : IMessage -> string =
        let formatterMap = 
            formatters 
            |> List.map (fun (t, f) -> t.FullName, f)
            |> Map.ofList
        
        fun msg ->
            let msgType = msg.GetType()
            formatterMap
            |> Map.tryFind msgType.FullName
            |> Option.map (fun formatter -> formatter msg)
            |> Option.defaultWith (fun () -> fallback msg)


/// Agent-based logging system
module AgentLogging =

    open System.IO
    open System.Threading

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

    type AgentLogger =
        {
            StartAsync : string option -> Level -> Async<Result<unit, string>>
            Logger     : Logger
            ReportAsync: unit -> Async<string[]>               // formatted lines
            WriteAsync : string -> Async<Result<unit, string>>
            StopAsync  : unit -> Async<unit>
        }
        interface IDisposable with
            member this.Dispose() = this.StopAsync() |> Async.RunSynchronously


    type AgentLoggerConfig = {
        Formatter: IMessage -> string
        MaxMessages: int option
        DefaultLevel: Level
        FlushInterval: TimeSpan
//        ErrorHandler: (LoggingError -> unit) option
    }


    let createAgentLogger (formatter: IMessage -> string) (maxMessages: int option) =
        let cts = new CancellationTokenSource()
        let mutable isDisposed = 0L

        let writer = W.create()

        let logger =
            Agent<LoggerMessage>.Start(fun inbox ->
                let timer = Diagnostics.Stopwatch.StartNew()
                let mutable pendingFlush = false
                
                let scheduleFlush() =
                    if not pendingFlush then
                        pendingFlush <- true
                        async {
                            do! Async.Sleep 10_000 //(int config.FlushInterval.TotalMilliseconds)
                            inbox.Post(FlushTimer)
                        } |> Async.Start

                // choose storage
                let ringOpt =
                    match maxMessages with
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

                // TODO: use SB
                let formatLogMessage (elapsed: float) (count: int) (ev: Event) =
                    try
                        let text = formatter ev.Message
                        if String.IsNullOrWhiteSpace text then None
                        else Some [|
                            // [0] – optional header/meta; [1] – body
                            sprintf "%d. %.3f: %A" count elapsed ev.Level
                            text
                        |]
                    with ex ->
                        eprintfn "Formatter error: %s" ex.Message
                        None

                let rec loop (path: string option) (level: Level) = async {
                    let! msgOpt = inbox.TryReceive(1000)
                    match msgOpt with
                    | None ->
                        if cts.Token.IsCancellationRequested then return ()
                        else return! loop path level

                    | Some msg ->
                        match msg with
                        | Stop reply ->
                            timer.Stop()
                            reply.Reply(())
                            return ()

                        | Start (newPath, newLevel, reply) ->
                            let res =
                                try
                                    match newPath with
                                    | Some p ->
                                        let header = sprintf "Start logging %A: %s\n"
                                                        newLevel (DateTime.Now.ToShortTimeString())
                                        File.WriteAllText(p, header + Environment.NewLine)
                                    | None -> ()
                                    Ok ()
                                with ex -> Error ex.Message
                            reply.Reply(res)
                            return! loop newPath newLevel

                        | LogEvent ev ->
                            let shouldLog =
                                match level with
                                | Level.Informative -> true
                                | _ -> ev.Level = level || ev.Level = Level.Error
                            if shouldLog then
                                let elapsed = timer.Elapsed.TotalSeconds
                                addMessage elapsed ev
                                let idx = countMessages()
                                match formatLogMessage elapsed idx ev with
                                | Some lines ->
                                    match path with
                                    | Some p -> W.append p lines writer      // async writer
                                    | None   -> printfn "%s" lines[1]        // print body only
                                | None -> ()
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
                                    W.append filePath allLines writer         // no flush here
                                    Ok ()
                                with ex -> Error ex.Message
                            reply.Reply(result)
                            return! loop path level

                        | FlushTimer ->
                            pendingFlush <- false
                            do! W.flushAsync writer
                            return! loop path level


                }
                loop None Level.Informative
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
                            logger |> Agent.dispose
                        with ex ->
                            eprintfn "Error stopping agent: %s" ex.Message
                }
        }