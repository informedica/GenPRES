namespace Informedica.Agent.Tests


module Tests =

    open Informedica.Agents.Lib
    open Expecto
    open Expecto.Flip
    open FsCheck
    open System
    open System.Threading


    // The property tests below post to MailboxProcessor agents (whose Receive
    // loops run on thread-pool threads) while blocking-waiting on the calling
    // (test) thread. On low-core CI runners the default minimum thread count
    // equals the core count, so the pool injects extra threads only every
    // ~500ms; a burst of blocking waits then starves the agents and the 5s
    // waits expire (observed only on windows-latest). Raise the worker-thread
    // floor so the agents are always schedulable. Runs at module init, which
    // also covers the `dotnet test` adapter path (Main.fs is bypassed there).
    do
        let workers, io = ThreadPool.GetMinThreads()
        ThreadPool.SetMinThreads(max workers 64, io) |> ignore


    // Test message types
    type TestMessage =
        | SimpleMessage of string
        | NumberMessage of int
        | DelayMessage of int * string
        | ErrorMessage of string

    type RequestMessage =
        | GetState
        | SetState of int
        | AddToState of int

    type ResponseMessage =
        | StateResponse of int
        | AckResponse


    module AgentTests  =

        // Poll until a condition holds or a timeout elapses, for `testAsync` bodies.
        // Used instead of a fixed Async.Sleep after Post so the tests stay
        // deterministic on slow or heavily-loaded CI runners. It MUST yield the
        // thread (Async.Sleep) rather than block it (Thread.Sleep): Expecto runs
        // tests in parallel, and the MailboxProcessor agents under test run their
        // loops on thread-pool threads. A blocking wait here would occupy those
        // threads and starve the agents — on a few-core CI runner that prevents
        // the posted messages from ever being processed.
        let waitUntilAsync (timeoutMs: int) (predicate: unit -> bool) =
            async {
                let sw = Diagnostics.Stopwatch.StartNew()

                while not (predicate ()) && sw.ElapsedMilliseconds < int64 timeoutMs do
                    do! Async.Sleep 5
            }

        // Basic agent tests
        let basicAgentTests =
            testList "Basic Agent Operations" [

                test "create agent should succeed" {
                    let agent: Agent<int> = Agent.Start (fun agent ->
                        async {
                            while true do
                                let! _ = agent.Receive()
                                ()
                        })

                    (agent <> Unchecked.defaultof<_>) |> Expect.isTrue "Agent should be created"
                    agent |> Agent.dispose
                }

                testAsync "simple message passing should work" {
                    let mutable receivedMessage = None

                    let agent = Agent.Start (fun agent ->
                        async {
                            let! msg = agent.Receive()
                            receivedMessage <- Some msg
                        })

                    agent.Post "Hello, World!"

                    do! waitUntilAsync 5000 (fun () -> receivedMessage = Some "Hello, World!")

                    receivedMessage |> Expect.equal "Should receive the message" (Some "Hello, World!")
                    agent |> Agent.dispose
                }

                testAsync "multiple messages should be processed in order" {
                    let mutable receivedMessages = []

                    let agent = Agent.Start (fun agent ->
                        async {
                            while true do
                                let! msg = agent.Receive()
                                receivedMessages <- msg :: receivedMessages
                        })

                    agent.Post "First"
                    agent.Post "Second"
                    agent.Post "Third"

                    do! waitUntilAsync 5000 (fun () -> List.length receivedMessages = 3)

                    let expectedOrder = ["Third"; "Second"; "First"] // Reversed due to cons
                    receivedMessages |> Expect.equal "Should process messages in order" expectedOrder
                    agent |> Agent.dispose
                }

                testAsync "agent should handle different message types" {
                    let mutable lastMessage = None

                    let agent = Agent.Start (fun agent ->
                        async {
                            while true do
                                let! msg = agent.Receive()
                                lastMessage <- Some msg
                        })

                    agent.Post (SimpleMessage "test")
                    do! waitUntilAsync 5000 (fun () -> lastMessage = Some (SimpleMessage "test"))
                    lastMessage |> Expect.equal "Should handle SimpleMessage" (Some (SimpleMessage "test"))

                    agent.Post (NumberMessage 42)
                    do! waitUntilAsync 5000 (fun () -> lastMessage = Some (NumberMessage 42))
                    lastMessage |> Expect.equal "Should handle NumberMessage" (Some (NumberMessage 42))

                    agent |> Agent.dispose
                }
            ]


        // Stateful agent tests
        let statefulAgentTests =
            testList "Stateful Agent Operations" [

                testAsync "stateful agent should maintain state" {
                    let mutable observedState = 0

                    let agent = Agent.createStateful (0, fun state msg ->
                        let newState =
                            match msg with
                            | AddToState value -> state + value
                            | _ -> state
                        observedState <- newState
                        newState)

                    agent.Post (AddToState 5)
                    do! waitUntilAsync 5000 (fun () -> observedState = 5)
                    observedState |> Expect.equal "State should be 5 after first add" 5

                    agent.Post (AddToState 3)
                    do! waitUntilAsync 5000 (fun () -> observedState = 8)
                    observedState |> Expect.equal "State should be 8 after second add" 8

                    agent |> Agent.dispose
                }

                testAsync "stateful agent with request-reply pattern" {
                    let agent = Agent.createStatefulReply (0, fun state msg ->
                        match msg with
                        | GetState ->
                            (StateResponse state, state)
                        | SetState newState ->
                            (AckResponse, newState)
                        | AddToState value ->
                            let newState = state + value
                            (StateResponse newState, newState))

                    // Test initial state
                    let! response1 = agent |> Agent.postAndAsyncReply GetState
                    response1 |> Expect.equal "Initial state should be 0" (StateResponse 0)

                    // Test setting state
                    let! response2 = agent |> Agent.postAndAsyncReply (SetState 10)
                    response2 |> Expect.equal "Should acknowledge set" AckResponse

                    // Test getting updated state
                    let! response3 = agent |> Agent.postAndAsyncReply GetState
                    response3 |> Expect.equal "State should be updated to 10" (StateResponse 10)

                    // Test adding to state
                    let! response4 = agent |> Agent.postAndAsyncReply (AddToState 5)
                    response4 |> Expect.equal "State should be 15 after adding 5" (StateResponse 15)

                    agent |> Agent.dispose
                }
            ]


        // Error handling tests
        let errorHandlingTests =
            testList "Error Handling" [

                testAsync "OnError event should fire when agent throws exception" {
                    let mutable errorReceived = None

                    let agent = Agent.createSimple (fun msg ->
                        match msg with
                        | ErrorMessage _ -> failwith "Test exception"
                        | _ -> ())

                    agent.OnError.Add (fun ex -> errorReceived <- Some ex.Message)

                    agent.Post (ErrorMessage "trigger error")

                    do! waitUntilAsync 5000 (fun () -> errorReceived.IsSome)

                    errorReceived |> Expect.isSome "Should receive error event"
                    errorReceived.Value |> Expect.stringContains "Should contain error message" "Test exception"

                    agent |> Agent.dispose
                }

                testAsync "agent should continue processing after recoverable error" {
                    let mutable messageCount = 0
                    let mutable errorCount = 0

                    let agent = Agent.createSimple (fun msg ->
                        try
                            match msg with
                            | ErrorMessage _ -> failwith "Recoverable error"
                            | SimpleMessage _ -> messageCount <- messageCount + 1
                            | _ -> ()
                        with
                        | ex -> errorCount <- errorCount + 1)

                    agent.Post (SimpleMessage "first")
                    agent.Post (ErrorMessage "error")
                    agent.Post (SimpleMessage "second")

                    do! waitUntilAsync 5000 (fun () -> messageCount = 2 && errorCount = 1)

                    messageCount |> Expect.equal "Should process normal messages" 2
                    errorCount |> Expect.equal "Should handle one error" 1

                    agent |> Agent.dispose
                }
            ]


        // Request-reply tests
        let requestReplyTests =
            testList "Request-Reply Pattern" [

                testAsync "PostAndReply should work with simple response" {
                    let agent = Agent.createReply (fun msg ->
                        $"Echo: %s{msg}")

                    let! response = agent |> Agent.postAndAsyncReply "Hello"
                    response |> Expect.equal "Should echo the message" "Echo: Hello"

                    agent |> Agent.dispose
                }

                testAsync "PostAndReply with timeout should work" {
                    let agent = Agent.createReply (fun msg ->
                        msg * 2)

                    let response = agent |> Agent.postAndReply 42
                    response |> Expect.equal "Should double the number" 84

                    agent |> Agent.dispose
                }

                testAsync "PostAndReply should timeout when no reply" {
                    let agent = Agent.Start (fun agent ->
                        async {
                            while true do
                                let! _ = agent.Receive()
                                // Don't reply - will cause timeout
                                ()
                        })

                    (fun () ->
                        agent.PostAndReply((fun replyChannel -> ("test", replyChannel)), timeout = 100))
                    |> Expect.throwsT<TimeoutException> "Should timeout when no reply"

                    agent |> Agent.dispose
                }

                testAsync "TryPostAndReply should return None on timeout" {
                    let agent : Agent<_> = Agent.Start (fun agent ->
                        async {
                            while true do
                                let! _, (replyChannel: AsyncReplyChannel<obj>) = agent.Receive()
                                // Delay longer than timeout
                                do! Async.Sleep 200
                                replyChannel.Reply "too late"
                        })

                    let result = agent |> Agent.tryPostAndReply 50 "test"
                    result |> Expect.isNone "Should return None on timeout"

                    agent |> Agent.dispose
                }
            ]


        // Performance and queue tests
        let performanceTests =
            testList "Performance and Queue Tests" [

                test "QueueLength should reflect pending messages" {
                    let agent = Agent.createSimple (fun _ ->
                        // Slow processing to build up queue
                        Thread.Sleep 100)

                    // Post multiple messages quickly
                    for i in 1..5 do
                        agent.Post i

                    // Queue length should be > 0 due to slow processing
                    let queueLength = agent |> Agent.getCurrentQueueLength
                    Expect.isGreaterThan "Queue should have pending messages" (queueLength, 0)

                    agent |> Agent.dispose
                }

                testAsync "agent should handle high message throughput" {
                    let mutable processedCount = 0

                    let agent = Agent.createSimple (fun _ ->
                        Interlocked.Increment(&processedCount) |> ignore)

                    let messageCount = 1000
                    for i in 1..messageCount do
                        agent.Post i

                    do! waitUntilAsync 10000 (fun () -> processedCount = messageCount)

                    processedCount |> Expect.equal "Should process all messages" messageCount

                    agent |> Agent.dispose
                }

                testAsync "slow processor should not block indefinitely with timeout" {
                    let agent = Agent.createReply (fun _ ->
                        Thread.Sleep 2_000    // keep > timeout, but avoid long ThreadPool blocking on CI
                        "done")
                    let result = agent |> Agent.tryPostAndReply 500 "test"
                    result |> Expect.isNone "should time out, not hang"
                    agent |> Agent.dispose
                }

                testAsync "agent with pending messages can be stopped cleanly" {
                    let mutable processed = 0
                    let agent = Agent.createSimple (fun _ ->
                        Thread.Sleep 50
                        Interlocked.Increment(&processed) |> ignore)
                    for _ in 1..10 do agent.Post ()
                    // Wait until at least one message has been processed before
                    // disposing, so the assertion is guaranteed to hold regardless
                    // of scheduler timing on slow CI runners.
                    do! waitUntilAsync 5000 (fun () -> processed >= 1)
                    agent |> Agent.dispose
                    Expect.isGreaterThan "some should have processed before disposal" (processed, 0)
                }
            ]


        // Disposal and cancellation tests
        // testSequenced: "Agent.post on disposed agent should be silent" redirects
        // Console.Error (process-global). Running disposal tests sequentially prevents
        // concurrent tests that also post to disposed agents from bleeding into the
        // captured stderr buffer.
        let disposalTests =
            testSequenced <| testList "Disposal and Cancellation" [

                test "disposed agent should not accept new messages" {
                    let agent = Agent.createSimple (fun _ -> ())

                    agent |> Agent.dispose

                    // This should not throw, but message won't be processed
                    let wasPosted =
                        agent
                        |> Agent.post "test"
                    wasPosted |> Expect.isFalse "Posting to disposed agent should not throw, but post is not performed"
                }

                testAsync "disposal should stop agent processing" {
                    let mutable isProcessing = true
                    let mutable started = false

                    let agent = Agent.Start (fun agent ->
                        async {
                            try
                                while true do
                                    let! _ = agent.Receive()
                                    started <- true
                            finally
                                isProcessing <- false
                        })

                    agent.Post "test"
                    // Make sure the agent is actually running its receive loop
                    // before we dispose it, so the `finally` is reached.
                    do! waitUntilAsync 5000 (fun () -> started)

                    agent |> Agent.dispose
                    // Disposal cancels the agent's Receive loop; wait for the
                    // `finally` to run rather than guessing a fixed duration.
                    do! waitUntilAsync 5000 (fun () -> not isProcessing)

                    isProcessing |> Expect.isFalse "Agent should stop processing after disposal"
                }

                test "Disposing an agent twice should be safe" {
                    let agent = Agent.createSimple (fun _ -> ())
                    agent |> Agent.dispose
                    agent |> Agent.dispose  // must not throw
                }

                test "Agent.post on disposed agent should be silent" {
                    let agent = Agent.createSimple (fun _ -> ())
                    agent |> Agent.dispose

                    let original = Console.Error
                    use buf = new System.IO.StringWriter()
                    Console.SetError(buf)
                    try
                        let _ = agent |> Agent.post "test"
                        ()
                    finally
                        Console.SetError(original)

                    buf.ToString() |> Expect.equal "should print nothing to stderr" ""
                }
            ]


        // Property-based tests using FsCheck
        let propertyTests =
            testList "Property-based Tests" [

                testProperty "agent should process all posted messages" <| fun (messages: int list) ->
                    (messages.Length <= 100) ==> lazy (
                        let mutable receivedMessages = []
                        let expected = messages.Length

                        // Set from the agent's thread once every posted message has been
                        // handled, and waited on here. A poll with Thread.Sleep 5 has ~15 ms
                        // granularity on Windows, which made these 100-case properties take
                        // up to 9 s there; the 30 s wait is a hang guard, not a timing budget.
                        let allProcessed = new ManualResetEventSlim(false)

                        let agent = Agent.createSimple (fun msg ->
                            receivedMessages <- msg :: receivedMessages
                            if List.length receivedMessages = expected then allProcessed.Set())

                        try
                            try
                                messages |> List.iter agent.Post

                                // Nothing to wait for when nothing was posted.
                                let completed = expected = 0 || allProcessed.Wait 30_000
                                completed && List.rev receivedMessages = messages
                            with _ ->
                                false
                        finally
                            agent |> Agent.dispose
                            allProcessed.Dispose()
                    )

                testProperty "stateful agent maintains state consistency" <| fun (operations: int list) ->
                    (operations.Length > 0 && operations.Length <= 50) ==> lazy (
                        let mutable finalState = None
                        let mutable processed = 0
                        let expected = operations.Length
                        let allProcessed = new ManualResetEventSlim(false)

                        // Signal on the message COUNT, not on the sum: an intermediate state
                        // can equal the expected sum (e.g. [5; 0]) before the last message
                        // has been handled, which is what the old sum-polling raced on.
                        let agent = Agent.createStateful (0, fun state msg ->
                            let newState = state + msg
                            finalState <- Some newState
                            processed <- processed + 1
                            if processed = expected then allProcessed.Set()
                            newState)

                        try
                            try
                                operations |> List.iter agent.Post

                                allProcessed.Wait 30_000 && finalState = Some (List.sum operations)
                            with _ ->
                                false
                        finally
                            agent |> Agent.dispose
                            allProcessed.Dispose()
                    )

                testProperty "request-reply should preserve message content" <| fun (msg: string) ->
                    (not (String.IsNullOrEmpty msg) && msg.Length <= 100) ==> lazy (
                        let agent = Agent.createReply id

                        try
                            let response = agent |> Agent.postAndReply msg
                            agent |> Agent.dispose
                            response = msg
                        with
                        | ex ->
                            agent |> Agent.dispose
                            false
                    )
            ]


        // Edge case tests
        let edgeCaseTests =
            testList "Edge Cases" [

                testAsync "agent with no message processing should not crash" {
                    let agent = Agent.createSimple (fun _ -> ())

                    agent.Post "test"
                    do! Async.Sleep 200

                    true |> Expect.isTrue "Agent should handle no message processing gracefully"
                    agent |> Agent.dispose
                }

                testAsync "agent receiving null messages should handle gracefully" {
                    let mutable receivedNull = false

                    let agent = Agent.createSimple (fun msg ->
                        if obj.ReferenceEquals(msg, null) then
                            receivedNull <- true)

                    agent.Post null
                    do! waitUntilAsync 5000 (fun () -> receivedNull)

                    receivedNull |> Expect.isTrue "Should handle null messages"
                    agent |> Agent.dispose
                }

                testAsync "concurrent PostAndReply should work correctly" {
                    let agent = Agent.createReply (fun msg ->
                        Thread.Sleep 10 // Small delay to test concurrency
                        msg * 2)

                    // Start multiple concurrent requests using the async variant so
                    // Async.Parallel can yield threads rather than block them.
                    let tasks = [
                        agent |> Agent.postAndAsyncReply 1
                        agent |> Agent.postAndAsyncReply 2
                        agent |> Agent.postAndAsyncReply 3
                        agent |> Agent.postAndAsyncReply 4
                        agent |> Agent.postAndAsyncReply 5
                    ]

                    let! results = Async.Parallel tasks
                    let expectedResults = [|2; 4; 6; 8; 10|]

                    (Array.sort results) |> Expect.equal "Should handle concurrent requests correctly" expectedResults
                    agent |> Agent.dispose
                }
            ]


        let private envVarName = "AGENT_REPLY_TIMEOUT_MS"

        /// Helper: run an action with a temporary env var value, restoring the previous value afterward.
        let private withEnvVar value action =
            let previous = Environment.GetEnvironmentVariable(envVarName)
            Environment.SetEnvironmentVariable(envVarName, value)
            try
                action ()
            finally
                Environment.SetEnvironmentVariable(envVarName, previous)

        // Configurable fallback timeout tests
        let fallbackTimeoutTests =
            // Tests that mutate AGENT_REPLY_TIMEOUT_MS must run sequentially
            testSequenced <| testList "Fallback Timeout (postAndReply with Infinite DefaultTimeout)" [

                test "postAndReply should succeed for fast agents with default 30s fallback" {
                    withEnvVar null (fun () ->
                        use agent = Agent.createReply<int, int>(fun n -> n * 2)
                        // DefaultTimeout is Timeout.Infinite by default, so fallback path is used
                        let result = agent |> Agent.postAndReply 21
                        result |> Expect.equal "should return doubled value" 42
                    )
                }

                test "postAndReply should succeed for slow agents within 30s fallback" {
                    withEnvVar null (fun () ->
                        use agent = Agent.createReply<string, string>(fun msg ->
                            Thread.Sleep(1200) // 1.2s — exceeds old 1s bug threshold, well within 30s fallback
                            $"done: {msg}"
                        )
                        let result = agent |> Agent.postAndReply "slow"
                        result |> Expect.equal "should complete within fallback timeout" "done: slow"
                    )
                }

                test "postAndReply should use AGENT_REPLY_TIMEOUT_MS env var when set" {
                    withEnvVar "200" (fun () ->
                        use agent = Agent.createReply<string, string>(fun msg ->
                            Thread.Sleep(800) // 800ms — exceeds the 200ms env var timeout
                            $"done: {msg}"
                        )
                        try
                            let _ = agent |> Agent.postAndReply "should-timeout"
                            failtest "should have thrown timeout"
                        with
                        | ex ->
                            ex.Message |> Expect.stringContains "should mention timeout duration" "200 ms"
                    )
                }

                test "postAndReply should ignore invalid AGENT_REPLY_TIMEOUT_MS and use 30s default" {
                    withEnvVar "not-a-number" (fun () ->
                        use agent = Agent.createReply<int, int>(fun n -> n + 1)
                        // Should still work — falls back to 30_000
                        let result = agent |> Agent.postAndReply 41
                        result |> Expect.equal "should use default 30s fallback" 42
                    )
                }

                test "postAndReply with explicit DefaultTimeout should bypass fallback" {
                    use agent = Agent.createReply<int, int>(fun n -> n * 3)
                    agent |> Agent.setDefaultTimeout 5000 // explicit 5s timeout
                    let result = agent |> Agent.postAndReply 10
                    result |> Expect.equal "should use explicit timeout path" 30
                }
            ]


        // Main test suite
        let allTests =
            testList "Informedica.Agents.Lib Agent Tests" [
                basicAgentTests
                statefulAgentTests
                errorHandlingTests
                requestReplyTests
                performanceTests
                disposalTests
                propertyTests
                edgeCaseTests
                fallbackTimeoutTests
            ]


    module FileWriterAgentTests =

        open System.IO
        open System.Text


        // Helper functions for testing
        module TestHelpers =

            let createTempFile () =
                let tempPath = Path.GetTempFileName()
                tempPath

            let createTempDirectory () =
                let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
                Directory.CreateDirectory(tempDir) |> ignore
                tempDir

            let deleteFileIfExists path =
                try
                    if File.Exists path then
                        File.Delete path
                with _ -> ()

            let deleteDirIfExists path =
                try
                    if Directory.Exists path then
                        Directory.Delete(path, true)
                with _ -> ()

            // Read with FileShare.ReadWrite so the read can coexist with the
            // FileWriterAgent's still-open write handle. On Windows a plain
            // File.ReadAllLines/ReadAllText (FileShare.Read) cannot open a file
            // that already has an open write handle and throws — which the catch
            // would otherwise turn into a misleading empty result. On Linux/macOS
            // share modes are advisory so the plain read happened to work.
            let openSharedRead (path: string) =
                let fs =
                    new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite ||| FileShare.Delete)

                new StreamReader(fs)

            let readAllLines path =
                try
                    if File.Exists path then
                        use sr = openSharedRead path
                        let lines = ResizeArray<string>()
                        let mutable line = sr.ReadLine()

                        while not (isNull line) do
                            lines.Add line
                            line <- sr.ReadLine()

                        lines.ToArray()
                    else
                        [||]
                with _ -> [||]

            let readAllText path =
                try
                    if File.Exists path then
                        use sr = openSharedRead path
                        sr.ReadToEnd()
                    else
                        ""
                with _ -> ""

            // No wait is needed between `FileWriterAgent.flush` and reading the file
            // back: flush is PostAndReply, the agent handles messages in order, and it
            // replies only after StreamWriter.Flush() on every open writer. A fixed
            // Thread.Sleep here used to cost 100 ms x 100 cases in each FsCheck
            // property below.

        open TestHelpers

        // Basic functionality tests
        let basicTests =
            testList "Basic FileWriterAgent Operations" [

                test "create agent should succeed" {
                    use writer = FileWriterAgent.create()
                    (writer <> Unchecked.defaultof<_>) |> Expect.isTrue "Agent should be created"
                }

                testAsync "append single line should work" {
                    let tempFile = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.append tempFile [|"Hello, World!"|]
                        |> FileWriterAgent.flush
                        |> ignore

                        let content = readAllLines tempFile
                        content |> Expect.equal "Should write single line" [|"Hello, World!"|]

                    finally
                        deleteFileIfExists tempFile
                }

                testAsync "append multiple lines should work" {
                    let tempFile = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        let lines = [|"Line 1"; "Line 2"; "Line 3"|]
                        writer
                        |> FileWriterAgent.append tempFile lines
                        |> FileWriterAgent.flush
                        |> ignore

                        let content = readAllLines tempFile
                        content |> Expect.equal "Should write all lines" lines

                    finally
                        deleteFileIfExists tempFile
                }

                testAsync "multiple appends should accumulate" {
                    let tempFile = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.append tempFile [|"First"|]
                        |> FileWriterAgent.append tempFile [|"Second"|]
                        |> FileWriterAgent.append tempFile [|"Third"|]
                        |> FileWriterAgent.flush
                        |> ignore

                        let content = readAllLines tempFile
                        content |> Expect.equal "Should accumulate lines" [|"First"; "Second"; "Third"|]

                    finally
                        deleteFileIfExists tempFile
                }

                testAsync "append to non-existent file should create it" {
                    let tempDir = createTempDirectory()
                    let tempFile = Path.Combine(tempDir, "newfile.txt")

                    try
                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.append tempFile [|"Created file"|]
                        |> FileWriterAgent.flush
                        |> ignore

                        (File.Exists tempFile) |> Expect.isTrue "Should create file"
                        let content = readAllLines tempFile
                        content |> Expect.equal "Should write content" [|"Created file"|]

                    finally
                        deleteDirIfExists tempDir
                }
            ]

        // Clear functionality tests
        let clearTests =
            testList "Clear Operations" [

                testAsync "clear should empty existing file" {
                    let tempFile = createTempFile()

                    try
                        // Write initial content
                        File.WriteAllLines(tempFile, [|"Initial"; "Content"|])

                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.clear tempFile
                        |> FileWriterAgent.flush
                        |> ignore

                        let content = readAllText tempFile
                        content |> Expect.equal "Should be empty after clear" ""

                    finally
                        deleteFileIfExists tempFile
                }

                testAsync "clear non-existent file should create empty file" {
                    let tempDir = createTempDirectory()
                    let tempFile = Path.Combine(tempDir, "cleartest.txt")

                    try
                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.clear tempFile
                        |> FileWriterAgent.flush
                        |> ignore

                        (File.Exists tempFile) |> Expect.isTrue "Should create file"
                        let content = readAllText tempFile
                        content |> Expect.equal "Should be empty" ""

                    finally
                        deleteDirIfExists tempDir
                }

                testAsync "clear then append should work" {
                    let tempFile = createTempFile()

                    try
                        // Write initial content
                        File.WriteAllLines(tempFile, [|"Old content"|])

                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.clear tempFile
                        |> FileWriterAgent.append tempFile [|"New content"|]
                        |> FileWriterAgent.flush
                        |> ignore

                        let content = readAllLines tempFile
                        content |> Expect.equal "Should only have new content" [|"New content"|]

                    finally
                        deleteFileIfExists tempFile
                }
            ]

        // Multiple file tests
        let multiFileTests =
            testList "Multiple File Operations" [

                testAsync "should handle multiple files independently" {
                    let tempFile1 = createTempFile()
                    let tempFile2 = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.append tempFile1 [|"File 1 content"|]
                        |> FileWriterAgent.append tempFile2 [|"File 2 content"|]
                        |> FileWriterAgent.flush
                        |> ignore

                        let content1 = readAllLines tempFile1
                        let content2 = readAllLines tempFile2

                        content1 |> Expect.equal "File 1 should have correct content" [|"File 1 content"|]
                        content2 |> Expect.equal "File 2 should have correct content" [|"File 2 content"|]

                    finally
                        deleteFileIfExists tempFile1
                        deleteFileIfExists tempFile2
                }

                testAsync "clear should only affect target file" {
                    let tempFile1 = createTempFile()
                    let tempFile2 = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        // Write to both files
                        writer
                        |> FileWriterAgent.append tempFile1 [|"File 1"|]
                        |> FileWriterAgent.append tempFile2 [|"File 2"|]
                        |> FileWriterAgent.flush
                        |> ignore

                        // Clear only file 1
                        writer
                        |> FileWriterAgent.clear tempFile1
                        |> FileWriterAgent.flush
                        |> ignore

                        let content1 = readAllText tempFile1
                        let content2 = readAllLines tempFile2

                        content1 |> Expect.equal "File 1 should be empty" ""
                        content2 |> Expect.equal "File 2 should be unchanged" [|"File 2"|]

                    finally
                        deleteFileIfExists tempFile1
                        deleteFileIfExists tempFile2
                }
            ]

        // Encoding tests
        let encodingTests =
            testList "Encoding Handling" [

                testAsync "should handle UTF-8 content correctly" {
                    let tempFile = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        let unicodeContent = [|"Hello 世界"; "Café ñoño"; "🚀 rocket"|]

                        writer
                        |> FileWriterAgent.append tempFile unicodeContent
                        |> FileWriterAgent.flush
                        |> ignore

                        let content = readAllLines tempFile
                        content |> Expect.equal "Should handle Unicode correctly" unicodeContent

                    finally
                        deleteFileIfExists tempFile
                }

                testAsync "should preserve existing file encoding" {
                    let tempFile = createTempFile()

                    try
                        // Write initial content with specific encoding
                        File.WriteAllLines(tempFile, [|"Initial content"|], Encoding.UTF8)

                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.append tempFile [|"Appended content"|]
                        |> FileWriterAgent.flush
                        |> ignore

                        let content = readAllLines tempFile
                        content |> Expect.equal "Should preserve and append correctly" [|"Initial content"; "Appended content"|]

                    finally
                        deleteFileIfExists tempFile
                }
            ]

        // Error handling tests
        let errorHandlingTests =
            testList "Error Handling" [

                testAsync "should handle invalid path gracefully" {
                    use writer = FileWriterAgent.create()

                    // This should not crash the agent. A path whose parent is a regular
                    // file fails immediately on every OS; the previous "//invalid//path//..."
                    // parsed as a UNC path on Windows and cost a ~3 s network name lookup.
                    let parentFile = createTempFile()
                    let invalidPath = Path.Combine(parentFile, "file.txt")

                    try
                        writer
                        |> FileWriterAgent.append invalidPath [|"test"|]
                        |> FileWriterAgent.flush
                        |> ignore
                    finally
                        deleteFileIfExists parentFile

                    // Agent should still be responsive
                    let tempFile = createTempFile()

                    try
                        writer
                        |> FileWriterAgent.append tempFile [|"Valid operation"|]
                        |> FileWriterAgent.flush
                        |> ignore

                        let content = readAllLines tempFile
                        content |> Expect.equal "Agent should continue working after error" [|"Valid operation"|]

                    finally
                        deleteFileIfExists tempFile
                }

                testAsync "should handle empty lines array" {
                    let tempFile = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.append tempFile [||]
                        |> FileWriterAgent.flush
                        |> ignore

                        let content = readAllText tempFile
                        content |> Expect.equal "Empty array should result in no content" ""

                    finally
                        deleteFileIfExists tempFile
                }
            ]

        // Performance tests
        let performanceTests =
            testList "Performance Tests" [

                testAsync "should handle large number of lines" {
                    let tempFile = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        let largeContent = Array.init 1000 (fun i -> $"Line {i}")

                        writer
                        |> FileWriterAgent.append tempFile largeContent
                        |> FileWriterAgent.flush
                        |> ignore

                        let content = readAllLines tempFile
                        content.Length |> Expect.equal "Should handle large content" 1000
                        content[0] |> Expect.equal "First line should be correct" "Line 0"
                        content[999] |> Expect.equal "Last line should be correct" "Line 999"

                    finally
                        deleteFileIfExists tempFile
                }

                testAsync "should handle rapid successive operations" {
                    let tempFile = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        // Rapid fire operations
                        for i in 1..100 do
                            writer
                            |> FileWriterAgent.append tempFile [|$"Rapid {i}"|]
                            |> ignore

                        writer |> FileWriterAgent.flush |> ignore

                        let content = readAllLines tempFile
                        content.Length |> Expect.equal "Should handle all rapid operations" 100

                    finally
                        deleteFileIfExists tempFile
                }
            ]

        // Property-based tests
        let propertyTests =
            testList "Property-based Tests" [

                testProperty "all written lines should be readable" <| fun (lines: string list) ->
                    let validLines =
                        lines
                        |> List.filter (fun s -> s <> null)
                        |> List.map _.Replace("\n", "").Replace("\r", "")
                        |> List.filter (fun s -> s.Length <= 50)
                        |> List.truncate 25

                    (not (List.isEmpty validLines)) ==> lazy (
                        let tempFile = createTempFile()

                        try
                            use writer = FileWriterAgent.create()

                            let linesArray = List.toArray validLines
                            writer
                            |> FileWriterAgent.append tempFile linesArray
                            |> FileWriterAgent.flush
                            |> ignore

                            let content = readAllLines tempFile
                            content = linesArray

                        finally
                            deleteFileIfExists tempFile
                    )

                testProperty "clear always results in empty file" <| fun (initialContent: string list) ->
                    let validContent =
                        initialContent
                        |> List.filter (fun s -> s <> null)
                        |> List.map _.Replace("\n", "").Replace("\r", "")
                        |> List.filter (fun s -> s.Length <= 50)
                        |> List.truncate 20

                    true ==> lazy (  // Always run, even with empty content
                        let tempFile = createTempFile()

                        try
                            use writer = FileWriterAgent.create()

                            // Write initial content if any
                            if not (List.isEmpty validContent) then
                                writer
                                |> FileWriterAgent.append tempFile (List.toArray validContent)
                                |> FileWriterAgent.flush
                                |> ignore

                            // Clear the file
                            writer
                            |> FileWriterAgent.clear tempFile
                            |> FileWriterAgent.flush
                            |> ignore

                            let content = readAllText tempFile
                            content = ""

                        finally
                            deleteFileIfExists tempFile
                    )

                testProperty "append is associative" <| fun (lines1: string list) (lines2: string list) ->
                    let validLines1 =
                        lines1
                        |> List.filter (fun s -> s <> null)
                        |> List.map _.Replace("\n", "").Replace("\r", "")
                        |> List.filter (fun s -> s.Length <= 50)
                        |> List.truncate 15

                    let validLines2 =
                        lines2
                        |> List.filter (fun s -> s <> null)
                        |> List.map _.Replace("\n", "").Replace("\r", "")
                        |> List.filter (fun s -> s.Length <= 50)
                        |> List.truncate 15

                    (not (List.isEmpty validLines1) || not (List.isEmpty validLines2)) ==> lazy (
                        let tempFile1 = createTempFile()
                        let tempFile2 = createTempFile()

                        try
                            use writer = FileWriterAgent.create()

                            // Method 1: append all at once
                            let allLines = validLines1 @ validLines2
                            if not (List.isEmpty allLines) then
                                writer
                                |> FileWriterAgent.append tempFile1 (List.toArray allLines)
                                |> FileWriterAgent.flush
                                |> ignore

                            // Method 2: append separately
                            if not (List.isEmpty validLines1) then
                                writer
                                |> FileWriterAgent.append tempFile2 (List.toArray validLines1)
                                |> ignore
                            if not (List.isEmpty validLines2) then
                                writer
                                |> FileWriterAgent.append tempFile2 (List.toArray validLines2)
                                |> ignore
                            writer |> FileWriterAgent.flush |> ignore

                            let content1 = readAllLines tempFile1
                            let content2 = readAllLines tempFile2
                            content1 = content2

                        finally
                            deleteFileIfExists tempFile1
                            deleteFileIfExists tempFile2
                    )
            ]

        // Async operation tests
        let asyncTests =
            testList "Async Operations" [

                testAsync "flushAsync should work" {
                    let tempFile = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.append tempFile [|"Async test"|]
                        |> ignore

                        do! FileWriterAgent.flushAsync writer

                        let content = readAllLines tempFile
                        content |> Expect.equal "Should flush asynchronously" [|"Async test"|]

                    finally
                        deleteFileIfExists tempFile
                }

                testAsync "stopAsync should work" {
                    let tempFile = createTempFile()

                    try
                        use writer = FileWriterAgent.create()

                        writer
                        |> FileWriterAgent.append tempFile [|"Stop test"|]
                        |> ignore

                        do! FileWriterAgent.stopAsync writer

                        let content = readAllLines tempFile
                        content |> Expect.equal "Should stop and flush content" [|"Stop test"|]

                    finally
                        deleteFileIfExists tempFile
                }
            ]

        // Main test suite
        let allTests =
            testList "FileWriterAgent Tests" [
                basicTests
                clearTests
                multiFileTests
                encodingTests
                errorHandlingTests
                performanceTests
                propertyTests
                asyncTests
            ]


    [<Tests>]
    let tests =
        testList "Informedica.Agent.Lib Tests"
            [
                AgentTests.allTests
                FileWriterAgentTests.allTests
            ]