open System
open System.Threading


type Agent<'T> = MailboxProcessor<'T>


type AutoCancelAgent<'T> private(inbox: Agent<'T>, cts: CancellationTokenSource) =
    static member Start f =
        let cts = new CancellationTokenSource()
        new AutoCancelAgent<'T>(Agent<'T>.Start(f, cancellationToken = cts.Token), cts)

    interface IDisposable with
        member _.Dispose() =
            inbox.Dispose()
            cts.Cancel()

    member _.PostAndAsyncReply f =
        inbox.PostAndAsyncReply f  


let op = async {
  // Create a local agent that is disposed when the 
  // workflow completes (using the 'use' construct)
  use agent = AutoCancelAgent.Start(fun agent -> async { 
    try 
      while true do
        // Wait for a message - note that we use timeout
        // to allow cancellation (when the operation completes)
        let! msg = agent.TryReceive 1000
        match msg with 
        | Some(n, reply:AsyncReplyChannel<unit>) ->
            // Print number and reply to the sender
            printfn "%d" n
            reply.Reply ()
        | _ -> ()
    finally 
      // Called when the agent is disposed
      printfn "agent completed" })
  
  // Do some processing using the agent...
  for i in 0 .. 10 do 
    do! agent.PostAndAsyncReply(fun r -> i, r) 
  printfn "workflow completed" }

Async.Start op


/// A wrapper for MailboxProcessor that catches all unhandled
/// exceptions and reports them via the 'OnError' event, repeatedly
/// running the provided function until it returns normally.
type ResilientMailbox<'T> private(f:ResilientMailbox<'T> -> Async<unit>) as self =
    // Create an event for reporting errors
    let event = Event<_>()
    // Start the standard MailboxProcessor
    let inbox = new MailboxProcessor<_>(fun _ ->
        // Recursivly run the user-provided function until it returns
        // normally; handle any exceptions it throws
        let rec loop() = async {
            // Run the user-provided function and handle exceptions
            try return! f self
            with e ->
                event.Trigger e
                return! loop()
            }
        loop())
    /// Triggered when an unhandled exception occurs
    member __.OnError = event.Publish
    /// Starts the mailbox processor
    member __.Start() = inbox.Start()
    /// Receive a message from the mailbox processor
    member __.Receive() = inbox.Receive()
    /// Post a message to the mailbox processor
    member __.Post(v:'T) = inbox.Post v
    /// Start the mailbox processor
    static member Start f =
        let mbox = new ResilientMailbox<_>(f)
        mbox.Start()
        mbox

// The usage is the same as with standard MailboxProcessor
let counter =
    ResilientMailbox<_>.Start(
        fun inbox ->
            async {
                while true do
                    printfn "waiting for data..."
                    let! _ = inbox.Receive()
                    // Simulate an exception
                    failwith "fail!"
            })

// Specify callback for unhandled errors and send a test message
counter.OnError.Add(eprintfn "Exception: %A")
counter.Post 42

