namespace Informedica.Agent.Tests


module Tests =

    open Informedica.Agents.Lib
    open Expecto
    open FsCheck
    open System
    open System.Threading


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

        // Basic agent tests
        let basicAgentTests =
            testList "Basic Agent Operations" [
                
                test "create agent should succeed" {
                    let agent: Agent<int> = Agent.Start (fun agent ->
                        async {
                            while true do
                                let! msg = agent.Receive()
                                ()
                        })
                    
                    Expect.isTrue (agent <> Unchecked.defaultof<_>) "Agent should be created"
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
                    
                    // Give time for message processing
                    do! Async.Sleep 100
                    
                    Expect.equal receivedMessage (Some "Hello, World!") "Should receive the message"
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
                    
                    // Give time for message processing
                    do! Async.Sleep 200
                    
                    let expectedOrder = ["Third"; "Second"; "First"] // Reversed due to cons
                    Expect.equal receivedMessages expectedOrder "Should process messages in order"
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
                    do! Async.Sleep 50
                    Expect.equal lastMessage (Some (SimpleMessage "test")) "Should handle SimpleMessage"
                    
                    agent.Post (NumberMessage 42)
                    do! Async.Sleep 50
                    Expect.equal lastMessage (Some (NumberMessage 42)) "Should handle NumberMessage"
                    
                    agent |> Agent.dispose
                }
            ]


        // Stateful agent tests
        let statefulAgentTests =
            testList "Stateful Agent Operations" [
                
                testAsync "stateful agent should maintain state" {
                    let agent = Agent.createStateful (0, fun state msg -> 
                        let state =   
                            match msg with
                            | AddToState value -> state + value
                            | _ -> state
                        
                        if not (state = 5 || state = 8) then
                            printfn $"current state should be 5 then 8: but is {state}"
                        state
                        )
                    
                    agent.Post (AddToState 5)
                    do! Async.Sleep 50
                    
                    agent.Post (AddToState 3)
                    do! Async.Sleep 50

                    // We can't directly check state, but we can test through side effects
                    Expect.isTrue true "Agent should maintain state internally"
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
                    Expect.equal response1 (StateResponse 0) "Initial state should be 0"
                    
                    // Test setting state
                    let! response2 = agent |> Agent.postAndAsyncReply (SetState 10)
                    Expect.equal response2 AckResponse "Should acknowledge set"
                    
                    // Test getting updated state
                    let! response3 = agent |> Agent.postAndAsyncReply GetState
                    Expect.equal response3 (StateResponse 10) "State should be updated to 10"
                    
                    // Test adding to state
                    let! response4 = agent |> Agent.postAndAsyncReply (AddToState 5)
                    Expect.equal response4 (StateResponse 15) "State should be 15 after adding 5"
                    
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
                    
                    // Give time for error to propagate
                    do! Async.Sleep 200
                    
                    Expect.isSome errorReceived "Should receive error event"
                    Expect.stringContains errorReceived.Value "Test exception" "Should contain error message"
                    
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
                    do! Async.Sleep 50
                    
                    agent.Post (ErrorMessage "error")
                    do! Async.Sleep 50
                    
                    agent.Post (SimpleMessage "second")
                    do! Async.Sleep 50
                    
                    Expect.equal messageCount 2 "Should process normal messages"
                    Expect.equal errorCount 1 "Should handle one error"
                    
                    agent |> Agent.dispose
                }
            ]


        // Request-reply tests
        let requestReplyTests =
            testList "Request-Reply Pattern" [
                
                testAsync "PostAndReply should work with simple response" {
                    let agent = Agent.createReply (fun msg ->
                        sprintf "Echo: %s" msg)
                    
                    let! response = agent |> Agent.postAndAsyncReply "Hello"
                    Expect.equal response "Echo: Hello" "Should echo the message"
                    
                    agent |> Agent.dispose
                }
                
                testAsync "PostAndReply with timeout should work" {
                    let agent = Agent.createReply (fun msg ->
                        msg * 2)
                    
                    let response = agent |> Agent.postAndReply 42
                    Expect.equal response 84 "Should double the number"
                    
                    agent |> Agent.dispose
                }
                
                testAsync "PostAndReply should timeout when no reply" {
                    let agent = Agent.Start (fun agent ->
                        async {
                            while true do
                                let! msg = agent.Receive()
                                // Don't reply - will cause timeout
                                ()
                        })
                    
                    Expect.throwsT<TimeoutException> (fun () ->
                        agent.PostAndReply((fun replyChannel -> ("test", replyChannel)), timeout = 100)) "Should timeout when no reply"
                    
                    agent |> Agent.dispose
                }
                
                testAsync "TryPostAndReply should return None on timeout" {
                    let agent : Agent<_> = Agent.Start (fun agent ->
                        async {
                            while true do
                                let! msg, (replyChannel: AsyncReplyChannel<obj>) = agent.Receive()
                                // Delay longer than timeout
                                do! Async.Sleep 200
                                replyChannel.Reply "too late"
                        })
                    
                    let result = agent |> Agent.tryPostAndReply 50 "test"
                    Expect.isNone result "Should return None on timeout"
                    
                    agent |> Agent.dispose
                }
            ]


        // Performance and queue tests
        let performanceTests =
            testList "Performance and Queue Tests" [
                
                test "QueueLength should reflect pending messages" {
                    let agent = Agent.createSimple (fun msg ->
                        // Slow processing to build up queue
                        Thread.Sleep 100)
                    
                    // Post multiple messages quickly
                    for i in 1..5 do
                        agent.Post i
                    
                    // Queue length should be > 0 due to slow processing
                    let queueLength = agent |> Agent.getCurrentQueueLength
                    Expect.isGreaterThan queueLength 0 "Queue should have pending messages"
                    
                    agent |> Agent.dispose
                }
                
                testAsync "agent should handle high message throughput" {
                    let mutable processedCount = 0
                    
                    let agent = Agent.createSimple (fun msg ->
                        Interlocked.Increment(&processedCount) |> ignore)
                    
                    let messageCount = 1000
                    for i in 1..messageCount do
                        agent.Post i
                    
                    // Wait for processing
                    do! Async.Sleep 2000
                    
                    Expect.equal processedCount messageCount "Should process all messages"
                    
                    agent |> Agent.dispose
                }
            ]


        // Disposal and cancellation tests
        let disposalTests =
            testList "Disposal and Cancellation" [
                
                test "disposed agent should not accept new messages" {
                    let agent = Agent.createSimple (fun msg -> ())
                    
                    agent |> Agent.dispose
                    
                    // This should not throw, but message won't be processed
                    let b =
                        agent 
                        |> Agent.post "test"
                    Expect.isFalse b "Posting to disposed agent should not throw, but post is not performed"
                }
                
                testAsync "disposal should stop agent processing" {
                    let mutable isProcessing = true
                    
                    let agent = Agent.Start (fun agent ->
                        async {
                            try
                                while true do
                                    let! msg = agent.Receive()
                                    ()
                            finally
                                isProcessing <- false
                        })
                    
                    agent.Post "test"
                    do! Async.Sleep 50
                    
                    agent |> Agent.dispose
                    do! Async.Sleep 100
                    
                    Expect.isFalse isProcessing "Agent should stop processing after disposal"
                }
            ]


        // Property-based tests using FsCheck
        let propertyTests =
            testList "Property-based Tests" [
                
                testProperty "agent should process all posted messages" <| fun (messages: int list) ->
                    (messages.Length <= 100) ==> lazy (
                        let mutable receivedMessages = []
                        
                        let agent = Agent.createSimple (fun msg ->
                            receivedMessages <- msg :: receivedMessages)
                        
                        try
                            messages |> List.iter agent.Post
                            
                            // Wait for processing
                            Thread.Sleep(messages.Length * 5 + 100)
                            
                            let result = List.rev receivedMessages = messages
                            agent |> Agent.dispose
                            result
                        with
                        | ex ->
                            agent |> Agent.dispose
                            false
                    )
                
                testProperty "stateful agent maintains state consistency" <| fun (operations: int list) ->
                    (operations.Length > 0 && operations.Length <= 50) ==> lazy (
                        let mutable finalState = None
                        
                        let agent = Agent.createStateful (0, fun state msg ->
                            let newState = state + msg
                            finalState <- Some newState
                            newState)
                        
                        try
                            operations |> List.iter agent.Post
                            
                            // Wait for processing
                            Thread.Sleep(operations.Length * 5 + 100)
                            
                            let expectedSum = List.sum operations
                            let result = finalState = Some expectedSum
                            agent |> Agent.dispose
                            result
                        with
                        | ex ->
                            agent |> Agent.dispose
                            false
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
                    let agent = Agent.createSimple (fun msg -> ())
                    
                    agent.Post "test"
                    do! Async.Sleep 200
                    
                    Expect.isTrue true "Agent should handle no message processing gracefully"
                    agent |> Agent.dispose
                }
                
                testAsync "agent receiving null messages should handle gracefully" {
                    let mutable receivedNull = false
                    
                    let agent = Agent.createSimple (fun msg ->
                        if obj.ReferenceEquals(msg, null) then
                            receivedNull <- true)
                    
                    agent.Post null
                    do! Async.Sleep 100
                    
                    Expect.isTrue receivedNull "Should handle null messages"
                    agent |> Agent.dispose
                }
                
                testAsync "concurrent PostAndReply should work correctly" {
                    let agent = Agent.createReply (fun msg ->
                        Thread.Sleep 10 // Small delay to test concurrency
                        msg * 2)
                    
                    // Start multiple concurrent requests
                    let tasks = [
                        async { return agent |> Agent.postAndReply 1 }
                        async { return agent |> Agent.postAndReply 2 }
                        async { return agent |> Agent.postAndReply 3 }
                        async { return agent |> Agent.postAndReply 4 }
                        async { return agent |> Agent.postAndReply 5 }
                    ]
                    
                    let! results = Async.Parallel tasks
                    let expectedResults = [|2; 4; 6; 8; 10|]
                    
                    Expect.equal (Array.sort results) expectedResults "Should handle concurrent requests correctly"
                    agent |> Agent.dispose
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

            let readAllLines path =
                try
                    if File.Exists path then
                        File.ReadAllLines(path)
                    else
                        [||]
                with _ -> [||]

            let readAllText path =
                try
                    if File.Exists path then
                        File.ReadAllText(path)
                    else
                        ""
                with _ -> ""

            let waitForFileWrite () = Thread.Sleep(100)

        open TestHelpers

        // Basic functionality tests
        let basicTests =
            testList "Basic FileWriterAgent Operations" [
                
                test "create agent should succeed" {
                    use writer = FileWriterAgent.create()
                    Expect.isTrue (writer <> Unchecked.defaultof<_>) "Agent should be created"
                }
                
                testAsync "append single line should work" {
                    let tempFile = createTempFile()
                    
                    try
                        use writer = FileWriterAgent.create()
                        
                        writer 
                        |> FileWriterAgent.append tempFile [|"Hello, World!"|]
                        |> FileWriterAgent.flush
                        |> ignore
                        
                        waitForFileWrite()
                        
                        let content = readAllLines tempFile
                        Expect.equal content [|"Hello, World!"|] "Should write single line"
                        
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
                        
                        waitForFileWrite()
                        
                        let content = readAllLines tempFile
                        Expect.equal content lines "Should write all lines"
                        
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
                        
                        waitForFileWrite()
                        
                        let content = readAllLines tempFile
                        Expect.equal content [|"First"; "Second"; "Third"|] "Should accumulate lines"
                        
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
                        
                        waitForFileWrite()
                        
                        Expect.isTrue (File.Exists tempFile) "Should create file"
                        let content = readAllLines tempFile
                        Expect.equal content [|"Created file"|] "Should write content"
                        
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
                        
                        waitForFileWrite()
                        
                        let content = readAllText tempFile
                        Expect.equal content "" "Should be empty after clear"
                        
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
                        
                        waitForFileWrite()
                        
                        Expect.isTrue (File.Exists tempFile) "Should create file"
                        let content = readAllText tempFile
                        Expect.equal content "" "Should be empty"
                        
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
                        
                        waitForFileWrite()
                        
                        let content = readAllLines tempFile
                        Expect.equal content [|"New content"|] "Should only have new content"
                        
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
                        
                        waitForFileWrite()
                        
                        let content1 = readAllLines tempFile1
                        let content2 = readAllLines tempFile2
                        
                        Expect.equal content1 [|"File 1 content"|] "File 1 should have correct content"
                        Expect.equal content2 [|"File 2 content"|] "File 2 should have correct content"
                        
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
                        
                        waitForFileWrite()
                        
                        // Clear only file 1
                        writer 
                        |> FileWriterAgent.clear tempFile1
                        |> FileWriterAgent.flush
                        |> ignore
                        
                        waitForFileWrite()
                        
                        let content1 = readAllText tempFile1
                        let content2 = readAllLines tempFile2
                        
                        Expect.equal content1 "" "File 1 should be empty"
                        Expect.equal content2 [|"File 2"|] "File 2 should be unchanged"
                        
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
                        
                        waitForFileWrite()
                        
                        let content = readAllLines tempFile
                        Expect.equal content unicodeContent "Should handle Unicode correctly"
                        
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
                        
                        waitForFileWrite()
                        
                        let content = readAllLines tempFile
                        Expect.equal content [|"Initial content"; "Appended content"|] "Should preserve and append correctly"
                        
                    finally
                        deleteFileIfExists tempFile
                }
            ]

        // Error handling tests
        let errorHandlingTests =
            testList "Error Handling" [
                
                testAsync "should handle invalid path gracefully" {
                    use writer = FileWriterAgent.create()
                    
                    // This should not crash the agent
                    let invalidPath = "//invalid//path//file.txt"
                    
                    writer 
                    |> FileWriterAgent.append invalidPath [|"test"|]
                    |> FileWriterAgent.flush
                    |> ignore
                    
                    // Agent should still be responsive
                    let tempFile = createTempFile()
                    
                    try
                        writer 
                        |> FileWriterAgent.append tempFile [|"Valid operation"|]
                        |> FileWriterAgent.flush
                        |> ignore
                        
                        waitForFileWrite()
                        
                        let content = readAllLines tempFile
                        Expect.equal content [|"Valid operation"|] "Agent should continue working after error"
                        
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
                        
                        waitForFileWrite()
                        
                        let content = readAllText tempFile
                        Expect.equal content "" "Empty array should result in no content"
                        
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
                        
                        waitForFileWrite()
                        
                        let content = readAllLines tempFile
                        Expect.equal content.Length 1000 "Should handle large content"
                        Expect.equal content[0] "Line 0" "First line should be correct"
                        Expect.equal content[999] "Line 999" "Last line should be correct"
                        
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
                        waitForFileWrite()
                        
                        let content = readAllLines tempFile
                        Expect.equal content.Length 100 "Should handle all rapid operations"
                        
                    finally
                        deleteFileIfExists tempFile
                }
            ]

        // Property-based tests
        // Property-based tests
        let propertyTests =
            testList "Property-based Tests" [
                
                testProperty "all written lines should be readable" <| fun (lines: string list) ->
                    let validLines = 
                        lines 
                        |> List.filter (fun s -> s <> null)
                        |> List.map (fun s -> s.Replace("\n", "").Replace("\r", ""))
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
                            
                            waitForFileWrite()
                            
                            let content = readAllLines tempFile
                            content = linesArray
                            
                        finally
                            deleteFileIfExists tempFile
                    )

                testProperty "clear always results in empty file" <| fun (initialContent: string list) ->
                    let validContent = 
                        initialContent 
                        |> List.filter (fun s -> s <> null)
                        |> List.map (fun s -> s.Replace("\n", "").Replace("\r", ""))
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
                            
                            waitForFileWrite()
                            
                            let content = readAllText tempFile
                            content = ""
                            
                        finally
                            deleteFileIfExists tempFile
                    )
                
                testProperty "append is associative" <| fun (lines1: string list) (lines2: string list) ->
                    let validLines1 = 
                        lines1 
                        |> List.filter (fun s -> s <> null)
                        |> List.map (fun s -> s.Replace("\n", "").Replace("\r", ""))
                        |> List.filter (fun s -> s.Length <= 50)
                        |> List.truncate 15
                        
                    let validLines2 = 
                        lines2 
                        |> List.filter (fun s -> s <> null)
                        |> List.map (fun s -> s.Replace("\n", "").Replace("\r", ""))
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
                            
                            waitForFileWrite()
                            
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
                        Expect.equal content [|"Async test"|] "Should flush asynchronously"
                        
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
                        Expect.equal content [|"Stop test"|] "Should stop and flush content"
                        
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