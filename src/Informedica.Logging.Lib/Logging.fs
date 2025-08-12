namespace Informedica.Logging.Lib

open System


/// General message types
type IMessage = interface end


type TimeStamp = DateTime


type Level =
    | Informative
    | Warning
    | Debug
    | Error


type Message =
    {
        TimeStamp: TimeStamp
        Level: Level
        Message: IMessage
    }


type Logger = { Log: Message -> unit }


/// General logging module
[<RequireQualifiedAccess>]
module Logging =
    

    /// Create a message with timestamp and level
    let private createMessage level (msg: IMessage) =
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
    let logInfo logger msg = logWith Informative logger msg


    /// Log a warning message  
    let logWarning logger msg = logWith Warning logger msg


    /// Log a debug message
    let logDebug logger msg = logWith Debug logger msg


    /// Log an error message
    let logError logger msg = logWith Error logger msg


    /// A logger that does nothing
    let ignore : Logger = { Log = ignore }


    /// Create a logger that uses the given function to process messages
    let create (f: Message -> unit) : Logger = { Log = f }


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
            | Informative -> 0
            | Debug -> 1
            | Warning -> 2
            | Error -> 3
        
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
            |> List.map (fun (t, f) -> (t.FullName, f))
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
    
    type LoggerMessage =
        | Start of path: string option * Level * AsyncReplyChannel<unit>
        | LogMessage of Message
        | Report of AsyncReplyChannel<unit>
        | Write of string * AsyncReplyChannel<unit>
        | Stop of AsyncReplyChannel<unit>

    type AgentLogger =
        {
            Start: string option -> Level -> unit
            Logger: Logger
            Report: unit -> unit
            Write: string -> unit
            Stop: unit -> unit
            StopAsync: unit -> Async<unit>
        }

    let createAgentLogger (formatter: IMessage -> string) =
        let agent = MailboxProcessor.Start(fun inbox ->
            let messages = ResizeArray<float * Message>()
            let timer = Diagnostics.Stopwatch.StartNew()
            
            let rec loop path level =
                async {
                    try
                        let! msg = inbox.Receive()
                        
                        match msg with
                        | Stop replyChannel ->  // ✅ Fix: Added missing replyChannel
                            replyChannel.Reply()
                            return ()

                        | Start (newPath, newLevel, replyChannel) ->  // ✅ Fix: Handle replyChannel
                            if newPath.IsSome then
                                $"Start logging {newLevel}: {DateTime.Now}\n\n"
                                |> fun text -> System.IO.File.WriteAllText(newPath.Value, text)
                            
                            replyChannel.Reply()  // ✅ Fix: Reply to the channel
                            return! loop newPath newLevel

                        | LogMessage logMsg ->
                            let shouldLog = 
                                match level with
                                | Informative -> true
                                | _ -> logMsg.Level = level || logMsg.Level = Error
                            
                            if shouldLog then
                                let elapsed = timer.Elapsed.TotalSeconds
                                messages.Add(elapsed, logMsg)
                                
                                let formattedMsg = formatter logMsg.Message
                                if not (String.IsNullOrEmpty formattedMsg) then
                                    match path with
                                    | Some p ->
                                        let text = [$"{messages.Count}. {elapsed}: {logMsg.Level}"; formattedMsg]
                                        System.IO.File.AppendAllLines(p, text)
                                    | None -> printfn "%s" formattedMsg
                        
                            return! loop path level
                    
                        | Report replyChannel ->
                            printfn "=== Start Report ==="
                            printfn "Total messages received: %d" messages.Count
                            messages
                            |> Seq.iteri (fun i (t, m) ->
                                let formattedMsg = formatter m.Message
                                if not (String.IsNullOrEmpty formattedMsg) then
                                    printfn "\n%d. %f: %A\n%s" i t m.Level formattedMsg
                            )
                            replyChannel.Reply()
                            return! loop path level
                        
                        | Write (filePath, replyChannel) ->
                            messages
                            |> Seq.iteri (fun i (t, m) ->
                                let formattedMsg = formatter m.Message
                                if not (String.IsNullOrEmpty formattedMsg) then
                                    let text = [$"{i}. {t}: {m.Level}"; formattedMsg]
                                    System.IO.File.AppendAllLines(filePath, text)
                            )
                            replyChannel.Reply()
                            return! loop path level
                    with
                    | ex -> 
                        eprintfn "Logger agent error: %s" ex.Message
                        return! loop path level  // ✅ Fix: Continue loop after exception
                
                }
            
            loop None Informative
        )

        // Add error handling
        agent.Error.Add(fun ex -> eprintfn "Agent error: %s" ex.Message)


        {
            Start = fun path level -> 
                agent.PostAndAsyncReply(fun reply -> Start (path, level, reply))
                |> Async.RunSynchronously  // ✅ Fix: Make synchronous
            Logger = { Log = fun msg -> agent.Post(LogMessage msg) }
            Report = fun () -> 
                agent.PostAndAsyncReply Report
                |> Async.RunSynchronously  // ✅ Fix: Make synchronous
            Write = fun path -> 
                agent.PostAndAsyncReply(fun reply -> Write (path, reply))
                |> Async.RunSynchronously  // ✅ Fix: Make synchronous
            Stop = fun () -> 
                agent.PostAndAsyncReply Stop
                |> Async.RunSynchronously  // ✅ Fix: Make synchronous
            StopAsync = fun () -> agent.PostAndAsyncReply Stop  // ✅ Keep async version
        }

        