

/// Restartable MailboxProcessor wrapper
module RestartableMailbox =
    open System
    open System.Threading
    
    type RestartableState<'T> =
        | Stopped
        | Running of MailboxProcessor<'T>
        | Disposed

    type RestartableMailboxProcessor<'T, 'Reply> = 
        {
            Post: 'T -> unit
            PostAndAsyncReply: (AsyncReplyChannel<'Reply> -> 'T) -> Async<'Reply>
            Start: unit -> unit
            Stop: unit -> unit
            Restart: unit -> unit
            IsRunning: bool
        }
        interface IDisposable with
            member this.Dispose() = this.Stop()

    let create<'T, 'Reply> (bodyFactory: unit -> MailboxProcessor<'T> -> Async<unit>) =
        let lockObj = obj()
        let mutable state = Stopped
        
        let createAgent() =
            MailboxProcessor.Start(fun inbox -> bodyFactory() inbox)
        
        let ensureRunning() =
            lock lockObj (fun () ->
                match state with
                | Running _ -> ()
                | Stopped -> 
                    let agent = createAgent()
                    state <- Running agent
                | Disposed -> invalidOp "RestartableMailboxProcessor has been disposed"
            )
        
        let getCurrentAgent() =
            lock lockObj (fun () ->
                match state with
                | Running agent -> Some agent
                | Stopped -> None
                | Disposed -> invalidOp "RestartableMailboxProcessor has been disposed"
            )
        
        {
            Post = fun msg ->
                match getCurrentAgent() with
                | Some agent -> agent.Post(msg)
                | None -> invalidOp "MailboxProcessor is not running. Call Start() first."
            
            PostAndAsyncReply = fun buildMessage ->
                match getCurrentAgent() with
                | Some agent -> agent.PostAndAsyncReply buildMessage
                | None -> async { return raise (invalidOp "MailboxProcessor is not running. Call Start() first.") }
            
            Start = fun () ->
                ensureRunning()
            
            Stop = fun () ->
                lock lockObj (fun () ->
                    match state with
                    | Running agent ->
                        (agent :> IDisposable).Dispose()
                        state <- Stopped
                    | Stopped -> ()
                    | Disposed -> ()
                )
            
            Restart = fun () ->
                lock lockObj (fun () ->
                    match state with
                    | Running agent ->
                        (agent :> IDisposable).Dispose()
                        let newAgent = createAgent()
                        state <- Running newAgent
                    | Stopped ->
                        let agent = createAgent()
                        state <- Running agent
                    | Disposed -> invalidOp "RestartableMailboxProcessor has been disposed"
                )
            
            IsRunning = 
                lock lockObj (fun () ->
                    match state with
                    | Running _ -> true
                    | _ -> false
                )
        }


// Example usage:
module Example =
    
    type LogMessage = 
        | Info of string
        | Error of string
        | Stop

    let createLogger() =
        RestartableMailbox.create (fun () inbox ->
            let rec loop() = async {
                let! msg = inbox.Receive()
                match msg with
                | Info text -> 
                    printfn "[INFO] %s" text
                    return! loop ()
                | Error text -> 
                    printfn "[ERROR] %s" text
                    return! loop ()
                | Stop -> 
                    printfn "Logger stopping..."
                    return ()  // Exit loop, agent will stop
            }
            loop ()
        )

    // Usage example:
    let demo() =
        let logger = createLogger()
        
        // Start the logger
        logger.Start()
        logger.Post(Info "Logger started")
        
        // Stop the logger
        logger.Post(Stop)
        System.Threading.Thread.Sleep(100)  // Let it stop
        
        // Restart the logger
        logger.Restart()
        logger.Post(Info "Logger restarted")
        
        // Final stop
        logger.Stop()

// Option 2: State-based restartable agent
module StatefulRestartableAgent =

    open System

    type AgentCommand<'State, 'Message> =
        | ProcessMessage of 'Message
        | Stop of AsyncReplyChannel<unit>
        | Restart of 'State * AsyncReplyChannel<unit>
        | GetState of AsyncReplyChannel<'State option>

    type RestartableAgent<'State, 'Message> =
        {
            Post: 'Message -> unit
            PostAndAsyncReply: ('Message -> AsyncReplyChannel<'Reply>) -> Async<'Reply>
            Stop: unit -> Async<unit>
            Restart: 'State -> Async<unit>
            GetState: unit -> Async<'State option>
        }
        interface IDisposable with
            member this.Dispose() = 
                this.Stop() |> Async.RunSynchronously

    let create<'State, 'Message> 
        (initialState: 'State) 
        (processor: 'State -> 'Message -> Async<'State option>) =
        
        let agent = MailboxProcessor.Start(fun inbox ->
            let rec stoppedLoop() = async {
                let! cmd = inbox.Receive()
                match cmd with
                | ProcessMessage _ -> 
                    // Ignore messages when stopped
                    return! stoppedLoop()
                | Stop replyChannel ->
                    replyChannel.Reply()
                    return! stoppedLoop()
                | Restart (newState, replyChannel) ->
                    replyChannel.Reply()
                    return! runningLoop newState
                | GetState replyChannel ->
                    replyChannel.Reply(None)
                    return! stoppedLoop()
            }
            
            and runningLoop state = async {
                let! cmd = inbox.Receive()
                match cmd with
                | ProcessMessage msg ->
                    try
                        let! newStateOpt = processor state msg
                        match newStateOpt with
                        | Some newState -> return! runningLoop newState
                        | None -> return! stoppedLoop()  // Processor requested stop
                    with
                    | ex -> 
                        eprintfn "Error processing message: %s" ex.Message
                        return! runningLoop state  // Continue with same state
                | Stop replyChannel ->
                    replyChannel.Reply()
                    return! stoppedLoop()
                | Restart (newState, replyChannel) ->
                    replyChannel.Reply()
                    return! runningLoop newState
                | GetState replyChannel ->
                    replyChannel.Reply(Some state)
                    return! runningLoop state
            }
            
            runningLoop initialState
        )
        
        {
            Post = fun msg -> agent.Post(ProcessMessage msg)
            
            PostAndAsyncReply = fun buildMessage ->
                // This is tricky - we need to wrap the reply channel
                agent.PostAndAsyncReply(fun replyChannel ->
                    let wrappedMessage = buildMessage replyChannel
                    ProcessMessage wrappedMessage
                )
            
            Stop = fun () -> agent.PostAndAsyncReply(Stop)
            
            Restart = fun newState -> 
                agent.PostAndAsyncReply(fun reply -> Restart(newState, reply))
            
            GetState = fun () -> agent.PostAndAsyncReply(GetState)
        }

// Example with stateful agent:
module StatefulExample =
    
    type CounterMessage =
        | Increment
        | Decrement  
        | GetCount of AsyncReplyChannel<int>
        | Reset

    let createCounter initialCount =
        StatefulRestartableAgent.create<int, CounterMessage> initialCount (fun state msg ->
            async {
                match msg with
                | Increment -> return Some (state + 1)
                | Decrement -> return Some (state - 1)
                | GetCount replyChannel -> 
                    replyChannel.Reply(state)
                    return Some state
                | Reset -> return Some 0
            }
        )

    let demo() = async {
        let counter = createCounter 0
        
        // Use the counter
        counter.Post(Increment)
        counter.Post(Increment)
        
        let! count1 = counter.PostAndAsyncReply(fun reply -> GetCount reply)
        printfn "Count: %d" count1  // Should be 2
        
        // Stop the counter
        do! counter.Stop()
        
        // Restart with new state
        do! counter.Restart(100)
        
        let! count2 = counter.PostAndAsyncReply(fun reply -> GetCount reply)
        printfn "Count after restart: %d" count2  // Should be 100
        
        counter.Post(Increment)
        let! count3 = counter.PostAndAsyncReply(fun reply -> GetCount reply)
        printfn "Final count: %d" count3  // Should be 101
    }