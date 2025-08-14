#load "load.fsx"


open Informedica.Logging.Lib


/// Robust agent-based logging system
module AgentLogging =
    open System
    open System.IO
    open System.Threading
    open System.Collections.Concurrent
    
    type LoggerMessage =
        | Start of path: string option * Level * AsyncReplyChannel<Result<unit, string>>
        | LogEvent of Event
        | Report of AsyncReplyChannel<unit>
        | Write of string * AsyncReplyChannel<Result<unit, string>>
        | Stop of AsyncReplyChannel<unit>

    type AgentLogger =
        {
            StartAsync: string option -> Level -> Async<Result<unit, string>>
            Logger: Logger
            ReportAsync: unit -> Async<unit>
            WriteAsync: string -> Async<Result<unit, string>>
            StopAsync: unit -> Async<unit>
        }
        interface IDisposable with
            member this.Dispose() = 
                this.StopAsync() |> Async.RunSynchronously

    let createAgentLogger (formatter: IMessage -> string) (maxMessages: int option) =
        let cts = new CancellationTokenSource()
        let mutable isDisposed = 0L  // Thread-safe flag using Interlocked
        
        // Background file writer to avoid blocking agent
        let fileWriteQueue = new ConcurrentQueue<string * string[]>()
        let fileWriteEvent = new AutoResetEvent(false)
        
        let fileWriter = async {
            while not cts.Token.IsCancellationRequested do
                try
                    let! _ = Async.AwaitWaitHandle(fileWriteEvent, 1000)
                    
                    let mutable hasWork = true
                    while hasWork && not cts.Token.IsCancellationRequested do
                        match fileWriteQueue.TryDequeue() with
                        | true, (filePath, lines) ->
                            try
                                File.AppendAllLines(filePath, lines)
                            with
                            | ex -> eprintfn "File write error: %s" ex.Message
                        | false, _ -> hasWork <- false
                with
                | :? OperationCanceledException -> ()
                | ex -> eprintfn "File writer error: %s" ex.Message
        }
        
        // Start background file writer
        Async.Start(fileWriter, cts.Token)
        
        let agent = MailboxProcessor.Start((fun inbox ->
            let messages = ResizeArray<float * Event>()
            let timer = Diagnostics.Stopwatch.StartNew()
            
            let addMessage elapsed logMsg =
                messages.Add(elapsed, logMsg)
                // Implement circular buffer if max messages specified
                match maxMessages with
                | Some max when messages.Count > max ->
                    let excess = messages.Count - max
                    for _ in 1..excess do
                        messages.RemoveAt(0)
                | _ -> ()
            
            let safeFileWrite filePath lines =
                try
                    fileWriteQueue.Enqueue(filePath, lines)
                    fileWriteEvent.Set() |> ignore
                    Ok ()
                with
                | ex -> Error ex.Message
            
            let formatLogMessage elapsed count logMsg =
                try
                    let formattedMsg = formatter logMsg.Message
                    if String.IsNullOrWhiteSpace formattedMsg then
                        None
                    else
                        Some [| sprintf "%d. %.3f: %A" count elapsed logMsg.Level; formattedMsg |]
                with
                | ex -> 
                    eprintfn "Formatter error: %s" ex.Message
                    None
            
            let rec loop path level =
                async {
                    try
                        let! msg = inbox.Receive(1000)  // Timeout to check cancellation
                        
                        match msg with
                        | Stop replyChannel ->  
                            timer.Stop()
                            replyChannel.Reply()
                            return ()

                        | Start (newPath, newLevel, replyChannel) ->  
                            let result = 
                                try
                                    match newPath with
                                    | Some p ->
                                        let initText = sprintf "Start logging %A: %s\n\n" newLevel (DateTime.Now.ToShortTimeString())
                                        File.WriteAllText(p, initText)
                                    | None -> ()
                                    Ok ()
                                with
                                | ex -> Result.Error ex.Message
                            
                            replyChannel.Reply(result)  
                            return! loop newPath newLevel

                        | LogEvent logMsg ->
                            let shouldLog = 
                                match level with
                                | Level.Informative -> true
                                | _ -> logMsg.Level = level || logMsg.Level = Level.Error
                            
                            if shouldLog then
                                let elapsed = timer.Elapsed.TotalSeconds
                                addMessage elapsed logMsg
                                
                                match formatLogMessage elapsed messages.Count logMsg with
                                | Some lines ->
                                    match path with
                                    | Some p -> safeFileWrite p lines |> ignore
                                    | None -> 
                                        // Only print the formatted message, not the metadata
                                        printfn "%s" lines.[1]
                                | None -> ()
                        
                            return! loop path level
                    
                        | Report replyChannel ->
                            printfn "=== Start Report ==="
                            printfn "Total messages received: %d" messages.Count
                            messages
                            |> Seq.iteri (fun i (t, m) ->
                                match formatLogMessage t (i + 1) m with
                                | Some lines ->
                                    printfn "\n%s\n%s" lines.[0] lines.[1]
                                | None -> ()
                            )
                            replyChannel.Reply()
                            return! loop path level
                        
                        | Write (filePath, replyChannel) ->
                            let result =
                                try
                                    let allLines = 
                                        messages
                                        |> Seq.mapi (fun i (t, m) ->
                                            formatLogMessage t (i + 1) m
                                            |> Option.map (fun lines -> lines)
                                        )
                                        |> Seq.choose id
                                        |> Seq.collect id
                                        |> Array.ofSeq
                                    
                                    File.WriteAllLines(filePath, allLines)
                                    Ok ()
                                with
                                | ex -> Result.Error ex.Message
                            
                            replyChannel.Reply(result)
                            return! loop path level
                            
                    with
                    | :? TimeoutException ->
                        // Check if we should continue
                        if cts.Token.IsCancellationRequested then
                            return ()
                        else
                            return! loop path level
                    | ex -> 
                        eprintfn "Logger agent error: %s" ex.Message
                        return! loop path level  
                }
            
            loop None Level.Informative
        ), cts.Token)

        // Add error handling
        agent.Error.Add(fun ex -> eprintfn "Agent error: %s" ex.Message)

        let ensureNotDisposed() =
            if Interlocked.Read(&isDisposed) = 1L then
                invalidOp "Logger agent has been disposed"

        {
            StartAsync = fun path level -> 
                async {
                    ensureNotDisposed()
                    return! agent.PostAndAsyncReply(fun reply -> Start (path, level, reply))
                }
                
            Logger = { 
                Log = fun msg -> 
                    if Interlocked.Read(&isDisposed) = 0L then
                        agent.Post(LogEvent msg) 
            }
            
            ReportAsync = fun () -> 
                async {
                    ensureNotDisposed()
                    return! agent.PostAndAsyncReply Report
                }
                
            WriteAsync = fun path -> 
                async {
                    ensureNotDisposed()
                    return! agent.PostAndAsyncReply(fun reply -> Write (path, reply))
                }
                
            StopAsync = fun () -> 
                async {
                    if Interlocked.CompareExchange(&isDisposed, 1L, 0L) = 0L then
                        try
                            do! agent.PostAndAsyncReply Stop
                            cts.Cancel()
                            (agent :> IDisposable).Dispose()
                        with
                        | ex -> eprintfn "Error stopping agent: %s" ex.Message
                }
        }

