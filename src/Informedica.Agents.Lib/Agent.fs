namespace Informedica.Agents.Lib


open System
open System.Threading
open System.Threading.Tasks

/// <summary>
/// Represents an asynchronous agent that processes messages of type 'T.
/// This is a wrapper around FSharp's MailboxProcessor, providing a unified API for agent-based concurrency.
/// </summary>
type Agent<'T>(body: Agent<'T> -> Async<unit>) as self =
    let cts = new CancellationTokenSource()

    // Track the lifetime of the mailbox processing loop without starting a second loop
    let tcs = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

    let errEvent = Event<_>()

    // The main difference with the original MailboxProcessor is that we handle errors in the body function
    // by triggering an error event instead of throwing exceptions directly.
    // This allows the agent to continue processing messages even after an error occurs.
    // The error event can be subscribed to by the user to handle errors gracefully.
    // The agent will keep running and processing messages, but will notify subscribers of any unhandled exceptions.
    let mbox = new MailboxProcessor<'T>(
        (fun _ ->
            let rec loop () = async {
                try
                    // Run user body; for typical helpers this loops on Receive()
                    do! body self
                    // If body returns normally, keep processing
                    return! loop ()
                with
                // Treat cancellation as a clean shutdown: stop looping and complete the TCS
                | :? OperationCanceledException
                | :? TaskCanceledException ->
                    tcs.TrySetCanceled(cts.Token) |> ignore
                    return ()
                | :? ObjectDisposedException ->
                    tcs.TrySetCanceled() |> ignore
                    return ()
                // For other errors, publish and continue processing
                | e ->
                    errEvent.Trigger(e)
                    return! loop ()
            }
            async {
                do! loop ()
                // If we exit the loop without cancellation, complete gracefully
                tcs.TrySetResult(()) |> ignore
            }
        ), true, cts.Token)

    /// <summary>
    /// Event that is triggered when an unhandled exception occurs in the agent's processing loop.
    /// </summary>
    member _.OnError = errEvent.Publish

    /// <summary>
    /// Gets the error event for the underlying MailboxProcessor.
    /// </summary>
    member _.Error = mbox.Error

    /// <summary>
    /// Posts a message to the agent asynchronously.
    /// </summary>
    member _.Post(message) = mbox.Post(message)


    /// <summary>
    /// Posts a message to the agent and synchronously waits for a reply.
    /// </summary>
    member _.PostAndReply(messageBuilder: AsyncReplyChannel<'Reply> -> 'T, ?timeout: int) : 'Reply =
        match timeout with
        | Some t -> mbox.PostAndReply(messageBuilder, t)
        | None -> mbox.PostAndReply(messageBuilder)

    /// <summary>
    /// Posts a message to the agent and synchronously waits for a reply, with a timeout.
    /// </summary>
    member _.TryPostAndReply(messageBuilder, timeout) = mbox.TryPostAndReply(messageBuilder, timeout)

    /// <summary>
    /// Posts a message to the agent and asynchronously waits for a reply.
    /// </summary>
    member _.PostAndAsyncReply(messageBuilder) = mbox.PostAndAsyncReply(messageBuilder)

    /// <summary>
    /// Posts a message to the agent and asynchronously waits for a reply, with a timeout.
    /// </summary>
    member _.PostAndTryAsyncReply(messageBuilder, timeout) = mbox.PostAndTryAsyncReply(messageBuilder, timeout)

    /// <summary>
    /// Starts the agent's processing loop.
    /// </summary>
    member _.Start() =
        mbox.Start()

    /// <summary>
    /// Starts the agent's processing loop immediately on the current thread.
    /// </summary>
    member _.StartImmediate () =
            mbox.StartImmediate()

    /// <summary>
    /// Receives the next message from the agent's queue asynchronously.
    /// </summary>
    member _.Receive() = mbox.Receive()

    /// <summary>
    /// Receives the next message from the agent's queue asynchronously, with a timeout.
    /// </summary>
    member _.Receive(timeOut) = mbox.Receive(timeOut)

    /// <summary>
    /// Tries to receive a message from the agent's queue within the specified timeout.
    /// </summary>
    member _.TryReceive(timeout) = mbox.TryReceive(timeout)

    /// <summary>
    /// Scans the agent's queue for a message matching the given scanner function.
    /// </summary>
    member _.Scan(scanner) = mbox.Scan(scanner)

    /// <summary>
    /// Scans the agent's queue for a message matching the given scanner function, with a timeout.
    /// </summary>
    member _.TryScan(scanner, timeout) = mbox.TryScan(scanner, timeout)

    /// <summary>
    /// Gets the current number of messages in the agent's queue.
    /// </summary>
    member _.CurrentQueueLength = mbox.CurrentQueueLength

    /// <summary>
    /// Gets or sets the default timeout for reply operations.
    /// </summary>
    member _.DefaultTimeout
        with get() = mbox.DefaultTimeout
        and set value = mbox.DefaultTimeout <- value

    /// <summary>
    /// Gets the cancellation token associated with the agent.
    /// </summary>
    member _.CancellationToken = cts.Token

    /// <summary>
    /// Gets a value indicating whether cancellation has been requested for the agent.
    /// </summary>
    member _.IsCancellationRequested = cts.Token.IsCancellationRequested

    /// <summary>
    /// Creates and starts a new agent with the specified body.
    /// </summary>
    static member Start(body) =
        let mbox = new Agent<'T>(body)

        mbox.Start ()
        mbox

    /// <summary>
    /// Creates and starts a new agent immediately on the current thread.
    /// </summary>
    static member StartImmediate(body) =
        let mbox = new Agent<'T>(body)
        mbox.StartImmediate ()
        mbox

    interface IDisposable with
        /// <summary>
        /// Disposes the agent and cancels its processing.
        /// </summary>
        member _.Dispose() =
            cts.Cancel()
            cts.Dispose()
            (mbox :> IDisposable).Dispose()


// Convenience module for creating and using agents with common patterns
[<RequireQualifiedAccess>]
module Agent =


    /// <summary>
    /// Creates and starts a simple agent that processes messages of type 'T in a loop.
    /// The processor function is called for each message received.
    /// This agent does not maintain state between messages and will
    /// not reply.
    /// </summary>
    /// <param name="processor">A function to process each message.</param>
    /// <returns>An Agent instance.</returns>
    let createSimple<'T> (processor: 'T -> unit) =
        Agent<'T>.Start(fun inbox ->
            let rec loop () = async {
                let! message = inbox.Receive()
                processor message
                return! loop ()
            }
            loop ()
        )


    /// <summary>
    /// Creates and starts a stateful agent that maintains state of type 'State.
    /// The processor function updates the state for each message received.
    /// This agent does not reply to messages.
    /// </summary>
    /// <param name="initialState">The initial state value.</param>
    /// <param name="processor">A function to process each message and update the state.</param>
    /// <returns>An Agent instance.</returns>
    let createStateful<'T, 'State> (initialState: 'State, processor: 'State -> 'T -> 'State) =
        Agent<'T>.Start(fun inbox ->
            let rec loop state = async {
                let! message = inbox.Receive()
                let newState = processor state message
                return! loop newState
            }
            loop initialState
        )

    /// <summary>
    /// Creates and starts an agent that supports request-reply messaging.
    /// The processor function computes a reply for each request.
    /// </summary>
    /// <param name="processor">A function to process each request and produce a reply.</param>
    /// <returns>An Agent instance.</returns>
    let createReply<'Request, 'Reply>(processor: 'Request -> 'Reply) =
        Agent<'Request * AsyncReplyChannel<'Reply>>.Start(fun inbox ->
            let rec loop () = async {
                let! request, replyChannel = inbox.Receive()
                let reply = processor request
                replyChannel.Reply(reply)
                return! loop ()
            }
            loop ()
        )

    /// <summary>
    /// Creates and starts a stateful agent that supports request-reply messaging.
    /// The processor function computes a reply and updates the state for each request.
    /// </summary>
    /// <param name="initialState">The initial state value.</param>
    /// <param name="processor">A function to process each request and update the state.</param>
    /// <returns>An Agent instance.</returns>
    let createStatefulReply<'Request, 'Reply, 'State> (initialState: 'State, processor: 'State -> 'Request -> 'Reply * 'State) =
        Agent<'Request * AsyncReplyChannel<'Reply>>.Start(fun inbox ->
            let rec loop state = async {
                let! request, replyChannel = inbox.Receive()
                let reply, newState = processor state request
                replyChannel.Reply(reply)
                return! loop newState
            }
            loop initialState
        )


    let post msg (agent: Agent<_>) =
        try
            agent.Post msg
            true
        with
        | ex ->
            eprintfn $"cannot post {msg} because:\n{ex.ToString()}"
            false


    /// <summary>
    /// Posts a request to the agent and tries to synchronously receive a reply within the specified timeout.
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="msg">The request message.</param>
    /// <param name="agent">The agent instance.</param>
    /// <returns>Some(reply) if successful, None if timed out.</returns>
    let tryPostAndReply timeout msg (agent: Agent<_>) =
        agent.TryPostAndReply((fun replyChannel ->
            msg, replyChannel
        ), timeout)

    /// <summary>
    /// Posts a request to the agent and synchronously waits for a reply.
    /// Throws if the reply times out.
    /// </summary>
    /// <param name="msg">The request message.</param>
    /// <param name="agent">The agent instance.</param>
    /// <returns>The reply value.</returns>
    let postAndReply msg (agent: Agent<_>) =
        if agent.DefaultTimeout = Timeout.Infinite then
            agent
            |> tryPostAndReply 1000 msg
            |> function
                | Some v -> v
                | None -> failwith "Timed out waiting for reply"
        else
            agent.PostAndReply(fun replyChannel ->
                msg, replyChannel
            )

    /// <summary>
    /// Posts a request to the agent and asynchronously waits for a reply.
    /// </summary>
    /// <param name="msg">The request message.</param>
    /// <param name="agent">The agent instance.</param>
    /// <returns>An Async computation returning the reply.</returns>
    let postAndAsyncReply msg (agent: Agent<_>) =
        agent.PostAndAsyncReply(fun replyChannel ->
            msg, replyChannel
        )

    /// <summary>
    /// Posts a request to the agent and asynchronously tries to receive a reply within the specified timeout.
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="msg">The request message.</param>
    /// <param name="agent">The agent instance.</param>
    /// <returns>An Async computation returning Some(reply) or None if timed out.</returns>
    let postAndTryAsyncReply timeout msg (agent: Agent<_>) =
        agent.PostAndTryAsyncReply((fun replyChannel ->
            msg, replyChannel
        ), timeout)

    /// <summary>
    /// Posts a request to the agent and returns a Task that completes with the reply.
    /// </summary>
    /// <param name="msg">The request message.</param>
    /// <param name="agent">The agent instance.</param>
    /// <returns>A Task returning the reply.</returns>
    let postAndAsyncReplyTask msg (agent: Agent<_>) =
        agent.PostAndAsyncReply(fun replyChannel ->
            msg, replyChannel
        )
        |> Async.StartAsTask

    /// <summary>
    /// Gets the default timeout for reply operations on the agent.
    /// </summary>
    /// <param name="agent">The agent instance.</param>
    /// <returns>The default timeout in milliseconds.</returns>
    let getDefaultTimeout (agent: Agent<_>) =
        agent.DefaultTimeout

    /// <summary>
    /// Sets the default timeout for reply operations on the agent.
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="agent">The agent instance.</param>
    let setDefaultTimeout timeout (agent: Agent<_>) =
        agent.DefaultTimeout <- timeout

    /// <summary>
    /// Gets the current number of messages in the agent's queue.
    /// </summary>
    /// <param name="agent">The agent instance.</param>
    /// <returns>The number of queued messages.</returns>
    let getCurrentQueueLength (agent: Agent<_>)=
        agent.CurrentQueueLength


    /// <summary>
    /// Disposes the agent and cancels its processing.
    /// </summary>
    /// <param name="agent">The agent instance.</param>
    let dispose (agent: Agent<_>) =
        (agent :> IDisposable).Dispose()


    let stopAndDispose (stopMsg: AsyncReplyChannel<unit> -> 'T) (agent: Agent<'T>) = async {
        do! agent.PostAndAsyncReply stopMsg
        (agent :> IDisposable).Dispose()
    }