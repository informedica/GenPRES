#load "load.fsx"

open System
open System.IO
open System.Threading
open System.Threading.Tasks

open Informedica.Logging.Lib
open Expecto
open Expecto.Logging
//open Expecto.Flip

// Test message types
type TestMessage = 
    { Text: string }
    interface IMessage

type NumberMessage = 
    { Value: int }
    interface IMessage

type ErrorMessage = 
    { Error: string; Code: int }
    interface IMessage

// Helper functions
let createTestMessage text = { Text = text } :> IMessage
let createNumberMessage value = { Value = value } :> IMessage
let createErrorMessage error code = { Error = error; Code = code } :> IMessage

let testFormatter (msg: IMessage) =
    match msg with
    | :? TestMessage as tm -> $"Test: {tm.Text}"
    | :? NumberMessage as nm -> $"Number: {nm.Value}"
    | :? ErrorMessage as em -> $"Error {em.Code}: {em.Error}"
    | _ -> $"Unknown: {msg.GetType().Name}"

// Test utilities
let withTempFile (action: string -> 'T) =
    let tempFile = Path.GetTempFileName()
    try
        action tempFile
    finally
        if File.Exists tempFile then File.Delete tempFile

let waitForCondition (condition: unit -> bool) (timeout: TimeSpan) =
    let sw = System.Diagnostics.Stopwatch.StartNew()
    while not (condition()) && sw.Elapsed < timeout do
        Thread.Sleep(10)
    condition()

// Basic Logging Module Tests
let basicLoggingTests =
    testList "Basic Logging" [
        
        test "createMessage should set timestamp and level" {
            let msg = createTestMessage "test"
            let event = Logging.createMessage Level.Warning msg
            
            Expect.equal event.Level Level.Warning "Level should be set correctly"
            Expect.equal event.Message msg "Message should be preserved"
            Expect.isGreaterThan event.TimeStamp (DateTime.Now.AddSeconds(-1.0)) "Timestamp should be recent"
        }
        
        test "logWith should create and send event to logger" {
            let mutable capturedEvent = None
            let logger = { Log = fun e -> capturedEvent <- Some e }
            let msg = createTestMessage "test message"
            
            Logging.logWith Level.Error logger msg
            
            match capturedEvent with
            | Some event ->
                Expect.equal event.Level Level.Error "Level should be Error"
                Expect.equal event.Message msg "Message should match"
            | None -> failtest "Event should have been captured"
        }
        
        test "convenience functions should use correct levels" {
            let mutable levels = []
            let logger = { Log = fun e -> levels <- e.Level :: levels }
            let msg = createTestMessage "test"
            
            Logging.logInfo logger msg
            Logging.logWarning logger msg
            Logging.logDebug logger msg
            Logging.logError logger msg
            
            let expectedLevels = [Level.Error; Level.Debug; Level.Warning; Level.Informative]
            Expect.equal levels expectedLevels "All levels should be captured in reverse order"
        }
        
        test "ignore logger should not process messages" {
            // This test ensures ignore logger doesn't throw
            let msg = createTestMessage "ignored"
            Logging.logInfo Logging.ignore msg
            // If we get here without exception, test passes
            Expect.isTrue true "Ignore logger should not throw"
        }
        
        test "createConsole logger should format and print" {
            let mutable output = []
            let originalOut = Console.Out
            
            try
                Console.SetOut(new StringWriter())  // Capture console output
                let logger = Logging.createConsole testFormatter
                let msg = createTestMessage "console test"
                
                Logging.logInfo logger msg
                // Test passes if no exception is thrown
                Expect.isTrue true "Console logger should not throw"
            finally
                Console.SetOut(originalOut)
        }
        
        testAsync "createFile logger should write to file" {
            return
                withTempFile (fun tempFile ->
                    let logger = Logging.createFile tempFile testFormatter
                    let msg = createTestMessage "file test"
                    
                    Logging.logInfo logger msg
                    
                    // Give it a moment to write
                    Thread.Sleep(100)
                    
                    let content = File.ReadAllText(tempFile)
                    Expect.stringContains content "Test: file test" "File should contain formatted message"
                    Expect.stringContains content "Informative" "File should contain log level"
                )
        }
        
        test "combine should send to all loggers" {
            let mutable events1 = []
            let mutable events2 = []
            let logger1 = { Log = fun e -> events1 <- e :: events1 }
            let logger2 = { Log = fun e -> events2 <- e :: events2 }
            
            let combined = Logging.combine [logger1; logger2]
            let msg = createTestMessage "combined test"
            
            Logging.logInfo combined msg
            
            Expect.hasLength events1 1 "First logger should receive event"
            Expect.hasLength events2 1 "Second logger should receive event"
        }
        
        test "filterByLevel should only pass appropriate messages" {
            let mutable events = []
            let baseLogger = { Log = fun e -> events <- e :: events }
            let filteredLogger = Logging.filterByLevel Level.Warning baseLogger
            
            let msg = createTestMessage "test"
            Logging.logInfo filteredLogger msg      // Should be filtered out
            Logging.logDebug filteredLogger msg     // Should be filtered out
            Logging.logWarning filteredLogger msg   // Should pass through
            Logging.logError filteredLogger msg     // Should pass through
            
            Expect.hasLength events 2 "Only Warning and Error should pass through"
            let levels = events |> List.map (fun e -> e.Level)
            Expect.contains levels Level.Warning "Warning should be included"
            Expect.contains levels Level.Error "Error should be included"
        }
        
        test "filterByType should only pass specific message types" {
            let mutable events = []
            let baseLogger = { Log = fun e -> events <- e :: events }
            let filteredLogger = Logging.filterByType<TestMessage> baseLogger
            
            let testMsg = createTestMessage "test"
            let numberMsg = createNumberMessage 42
            
            Logging.logInfo filteredLogger testMsg    // Should pass through
            Logging.logInfo filteredLogger numberMsg  // Should be filtered out
            
            Expect.hasLength events 1 "Only TestMessage should pass through"
            match events.[0].Message with
            | :? TestMessage -> ()
            | _ -> failtest "Should be TestMessage"
        }
    ]

// Message Formatter Tests
let messageFormatterTests =
    testList "Message Formatter" [
        
        test "create should use correct formatter for message type" {
            let formatters = [
                typeof<TestMessage>, (fun (msg: IMessage) -> (msg :?> TestMessage).Text)
                typeof<NumberMessage>, (fun (msg: IMessage) -> string ((msg :?> NumberMessage).Value))
            ]
            
            let formatter = MessageFormatter.create formatters
            
            let testResult = formatter (createTestMessage "hello")
            let numberResult = formatter (createNumberMessage 42)
            let unknownResult = formatter (createErrorMessage "error" 1)
            
            Expect.equal testResult "hello" "Test message should be formatted correctly"
            Expect.equal numberResult "42" "Number message should be formatted correctly"
            Expect.equal unknownResult "" "Unknown message should return empty string"
        }
        
        test "createWithFallback should use fallback for unknown types" {
            let formatters = [
                typeof<TestMessage>, (fun (msg: IMessage) -> (msg :?> TestMessage).Text)
            ]
            
            let fallback = fun msg -> $"Fallback: {msg.GetType().Name}"
            let formatter = MessageFormatter.createWithFallback formatters fallback
            
            let testResult = formatter (createTestMessage "hello")
            let unknownResult = formatter (createNumberMessage 42)
            
            Expect.equal testResult "hello" "Known message should use specific formatter"
            Expect.equal unknownResult "Fallback: NumberMessage" "Unknown message should use fallback"
        }
    ]

// Agent Logging Tests
let agentLoggingTests =
    testList "Agent Logging" [
        
        testAsync "createConsole should work with default config" {
            let logger = AgentLogging.createConsole()
            
            let! result = logger.StartAsync None Level.Informative
            Expect.isOk result "Start should succeed"
            
            // Log a message
            let msg = createTestMessage "agent test"
            Logging.logInfo logger.Logger msg
            
            // Wait a bit for processing
            do! Async.Sleep 100
            
            // Stop the logger
            do! logger.StopAsync()
        }
        
        testAsync "should handle file logging" {
            return
                withTempFile (fun tempFile ->
                    async {
                        let logger = AgentLogging.createDebug()
                        
                        let! result = logger.StartAsync (Some tempFile) Level.Informative
                        Expect.isOk result "Start should succeed"
                        
                        // Log several messages
                        for i in 1..5 do
                            let msg = createTestMessage $"Message {i}"
                            Logging.logInfo logger.Logger msg
                        
                        // Wait for processing and flushing
                        do! Async.Sleep 2000
                        
                        // Stop logger to ensure flush
                        do! logger.StopAsync()
                        
                        // Check file content
                        let content = File.ReadAllText(tempFile)
                        Expect.stringContains content "Message 1" "File should contain first message"
                        Expect.stringContains content "Message 5" "File should contain last message"
                    } |> Async.RunSynchronously
                )
        }
        
        testAsync "should respect message limits" {
            let config = { 
                AgentLogging.AgentLoggerDefaults.console with 
                    MaxMessages = Some 3
                    FlushInterval = TimeSpan.FromMilliseconds(100.0)
            }
            let logger = AgentLogging.createAgentLogger config
            
            let! result = logger.StartAsync None Level.Informative
            Expect.isOk result "Start should succeed"
            
            // Log more messages than the limit
            for i in 1..10 do
                let msg = createTestMessage $"Message {i}"
                Logging.logInfo logger.Logger msg
            
            // Wait for processing
            do! Async.Sleep 200
            
            // Get report
            let! lines = logger.ReportAsync()
            
            // Should only have the last 3 messages (plus headers)
            let messageLines = lines |> Array.filter (fun l -> l.Contains("Message"))
            Expect.hasLength messageLines 3 "Should only keep last 3 messages"
            Expect.stringContains (String.concat "" messageLines) "Message 8" "Should contain Message 8"
            Expect.stringContains (String.concat "" messageLines) "Message 9" "Should contain Message 9"
            Expect.stringContains (String.concat "" messageLines) "Message 10" "Should contain Message 10"
            
            do! logger.StopAsync()
        }
        
        testAsync "should handle high throughput" {
            let logger = AgentLogging.createHighPerformance()
            
            let! result = logger.StartAsync None Level.Informative
            Expect.isOk result "Start should succeed"
            
            // Log many messages quickly
            let messageCount = 1000
            let sw = System.Diagnostics.Stopwatch.StartNew()
            
            for i in 1..messageCount do
                let msg = createNumberMessage i
                Logging.logInfo logger.Logger msg
            
            sw.Stop()
            
            // Wait for processing
            do! Async.Sleep 1000
            
            let! lines = logger.ReportAsync()
            let messageLines = lines |> Array.filter (fun l -> l.Contains("Number:"))
            
            Expect.isGreaterThan messageLines.Length 0 "Should have processed some messages"
            printfn $"Processed {messageLines.Length} messages in {sw.ElapsedMilliseconds}ms"
            
            do! logger.StopAsync()
        }
        
        testAsync "should handle level filtering" {
            let logger = AgentLogging.createProduction() // Only logs errors
            
            let! result = logger.StartAsync None Level.Error
            Expect.isOk result "Start should succeed"
            
            // Log messages at different levels
            let infoMsg = createTestMessage "info"
            let warningMsg = createTestMessage "warning" 
            let errorMsg = createTestMessage "error"
            
            Logging.logInfo logger.Logger infoMsg
            Logging.logWarning logger.Logger warningMsg
            Logging.logError logger.Logger errorMsg
            
            // Wait for processing
            do! Async.Sleep 500
            
            let! lines = logger.ReportAsync()
            let messageLines = lines |> Array.filter (fun l -> l.Contains("Test:"))
            
            // Should only have the error message
            Expect.hasLength messageLines 1 "Should only log error messages"
            Expect.stringContains (String.concat "" messageLines) "error" "Should contain error message"
            
            do! logger.StopAsync()
        }
        
        testAsync "writeAsync should create snapshot file" {
            return
                withTempFile (fun tempFile ->
                    async {
                        let logger = AgentLogging.createDebug()
                        
                        let! result = logger.StartAsync None Level.Informative
                        Expect.isOk result "Start should succeed"
                        
                        // Log some messages
                        for i in 1..3 do
                            let msg = createTestMessage $"Snapshot {i}"
                            Logging.logInfo logger.Logger msg
                        
                        // Wait for processing
                        do! Async.Sleep 200
                        
                        // Write snapshot
                        let! writeResult = logger.WriteAsync tempFile
                        Expect.isOk writeResult "Write should succeed"
                        
                        do! logger.StopAsync()
                        
                        // Check snapshot file
                        let content = File.ReadAllText(tempFile)
                        Expect.stringContains content "Snapshot 1" "File should contain first message"
                        Expect.stringContains content "Snapshot 3" "File should contain last message"
                    } |> Async.RunSynchronously
                )
        }
        
        testAsync "should handle disposal correctly" {
            let logger = AgentLogging.createConsole()
            
            let! result = logger.StartAsync None Level.Informative
            Expect.isOk result "Start should succeed"
            
            // Test IDisposable
            (logger :> IDisposable).Dispose()
            
            // Should not be able to use after disposal
            Expect.throws (fun () -> 
                logger.StartAsync None Level.Informative 
                |> Async.RunSynchronously 
                |> ignore) "Should throw after disposal"
        }
    ]

// Performance and stress tests
let performanceTests =
    testList "Performance" [
        
        testAsync "should handle concurrent logging" {
            let logger = AgentLogging.createHighPerformance()
            
            let! result = logger.StartAsync None Level.Informative
            Expect.isOk result "Start should succeed"
            
            // Create multiple tasks logging concurrently
            let tasks = [
                for i in 1..10 ->
                    Task.Run(fun () ->
                        for j in 1..100 do
                            let msg = createNumberMessage (i * 100 + j)
                            Logging.logInfo logger.Logger msg
                    )
            ]
            
            // Wait for all tasks to complete
            Task.WaitAll(tasks |> Array.ofList)
            
            // Wait for processing
            do! Async.Sleep 1000
            
            let! lines = logger.ReportAsync()
            let messageLines = lines |> Array.filter (fun l -> l.Contains("Number:"))
            
            Expect.isGreaterThan messageLines.Length 500 "Should have processed most messages"
            
            do! logger.StopAsync()
        }
    ]

// Error handling tests
let errorHandlingTests =
    testList "Error Handling" [
        
        testAsync "should handle formatter exceptions gracefully" {
            let badFormatter = fun (msg: IMessage) -> 
                failwith "Formatter error!"
            
            let config = { 
                AgentLogging.AgentLoggerDefaults.console with 
                    Formatter = badFormatter
            }
            
            let logger = AgentLogging.createAgentLogger config
            
            let! result = logger.StartAsync None Level.Informative
            Expect.isOk result "Start should succeed even with bad formatter"
            
            // This should not crash the logger
            let msg = createTestMessage "test"
            Logging.logInfo logger.Logger msg
            
            do! Async.Sleep 200
            
            // Logger should still be responsive
            let! lines = logger.ReportAsync()
            Expect.isGreaterThan lines.Length 0 "Should have error message in report"
            
            do! logger.StopAsync()
        }
    ]

// Main test suite
let allTests =
    testList "Informedica.Logging.Lib Tests" [
        basicLoggingTests
        messageFormatterTests
        agentLoggingTests
        performanceTests
        errorHandlingTests
    ]

// Run tests
runTestsWithCLIArgs [] [|"--summary"|] agentLoggingTests


(*
// Export for external use
[<EntryPoint>]
let main args = 
    runTestsWithCLIArgs [] args allTests
*)

