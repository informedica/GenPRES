namespace Informedica.Logging.Tests

module Tests =

    module AgentLogging =

        open System
        open System.IO
        open System.Threading
        open System.Threading.Tasks

        open Informedica.Logging.Lib
        open Expecto
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
                    Logging.logInfo Logging.noOp msg
                    // If we get here without exception, test passes
                    Expect.isTrue true "Ignore logger should not throw"
                }

                test "createConsole logger should format and print" {
                    let mutable _ = []
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
                    match events[0].Message with
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
                        typeof<NumberMessage>, (fun (msg: IMessage) -> string (msg :?> NumberMessage).Value)
                    ]

                    let formatter = MessageFormatter.create formatters

                    let testResult = formatter (createTestMessage "hello")
                    let numberResult = formatter (createNumberMessage 42)
                    let unknownResult = formatter (createErrorMessage "error" 1)

                    Expect.equal testResult "hello" "Test message should be formatted correctly"
                    Expect.equal numberResult "42" "Number message should be formatted correctly"
                    Expect.stringContains unknownResult "cannot format" "Unknown message should contain cannot format"
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

        // Discriminated Union Message Types for testing inheritance and type hierarchy
        type LogEventMessage =
            | InfoEvent of message: string
            | WarnEvent of message: string * code: int
            | ErrorEvent of message: string * code: int * details: string
            interface IMessage

        type SystemMessage =
            | StartupMessage of version: string
            | ShutdownMessage of reason: string
            | ConfigMessage of LogEventMessage  // Nested discriminated union
            interface IMessage

        type ComplexMessage =
            | Simple of string
            | WithLogEvent of LogEventMessage
            | WithSystem of SystemMessage
            | Compound of LogEventMessage * SystemMessage
            interface IMessage

        // Base type for inheritance testing
        type BaseMessage =
            abstract member GetDescription: unit -> string

        type DerivedMessage(description: string) =
            member _.GetDescription() = description
            interface IMessage
            interface BaseMessage with
                member _.GetDescription() = description

        type AnotherDerivedMessage(description: string, code: int) =
            member _.Code = code
            member _.GetDescription() = $"{description} (Code: {code})"
            interface IMessage
            interface BaseMessage with
                member _.GetDescription() = $"{description} (Code: {code})"

        // Helper functions for creating discriminated union messages
        let createLogEvent msg = InfoEvent msg :> IMessage
        let createWarnEvent msg code = WarnEvent (msg, code) :> IMessage
        let createErrorEvent msg code details = ErrorEvent (msg, code, details) :> IMessage
        let createSystemMessage version = StartupMessage version :> IMessage
        let createComplexMessage msg = Simple msg :> IMessage
        let createNestedMessage logEvent = ConfigMessage logEvent :> IMessage

        // Discriminated Union Message Formatter Tests
        let discriminatedUnionFormatterTests =
            testList "Discriminated Union Message Formatters" [

                test "should format discriminated union with simple cases" {
                    let formatters = [
                        typeof<LogEventMessage>, (fun (msg: IMessage) ->
                            match msg :?> LogEventMessage with
                            | InfoEvent message -> $"INFO: {message}"
                            | WarnEvent (message, code) -> $"WARN[{code}]: {message}"
                            | ErrorEvent (message, code, details) -> $"ERROR[{code}]: {message} - {details}"
                        )
                    ]

                    let formatter = MessageFormatter.create formatters

                    let infoResult = formatter (createLogEvent "System started")
                    let warnResult = formatter (createWarnEvent "Low memory" 1001)
                    let errorResult = formatter (createErrorEvent "Connection failed" 2001 "Network timeout")

                    Expect.equal infoResult "INFO: System started" "Info event should be formatted correctly"
                    Expect.equal warnResult "WARN[1001]: Low memory" "Warn event should be formatted correctly"
                    Expect.equal errorResult "ERROR[2001]: Connection failed - Network timeout" "Error event should be formatted correctly"
                }

                test "should format nested discriminated unions" {
                    let formatLogEvent = function
                        | InfoEvent message -> $"INFO: {message}"
                        | WarnEvent (message, code) -> $"WARN[{code}]: {message}"
                        | ErrorEvent (message, code, details) -> $"ERROR[{code}]: {message} - {details}"

                    let formatters = [
                        typeof<SystemMessage>, (fun (msg: IMessage) ->
                            match msg :?> SystemMessage with
                            | StartupMessage version -> $"STARTUP: Version {version}"
                            | ShutdownMessage reason -> $"SHUTDOWN: {reason}"
                            | ConfigMessage logEvent -> $"CONFIG: {formatLogEvent logEvent}"
                        )
                    ]

                    let formatter = MessageFormatter.create formatters

                    let startupResult = formatter (createSystemMessage "1.0.0")
                    let configResult = formatter (createNestedMessage (WarnEvent ("Invalid setting", 3001)))

                    Expect.equal startupResult "STARTUP: Version 1.0.0" "Startup message should be formatted correctly"
                    Expect.equal configResult "CONFIG: WARN[3001]: Invalid setting" "Nested discriminated union should be formatted correctly"
                }

                test "should handle complex discriminated unions with multiple types" {
                    let formatLogEvent = function
                        | InfoEvent message -> $"INFO: {message}"
                        | WarnEvent (message, code) -> $"WARN[{code}]: {message}"
                        | ErrorEvent (message, code, details) -> $"ERROR[{code}]: {message} - {details}"

                    let formatSystemMessage = function
                        | StartupMessage version -> $"STARTUP: Version {version}"
                        | ShutdownMessage reason -> $"SHUTDOWN: {reason}"
                        | ConfigMessage logEvent -> $"CONFIG: {formatLogEvent logEvent}"

                    let formatters = [
                        typeof<ComplexMessage>, (fun (msg: IMessage) ->
                            match msg :?> ComplexMessage with
                            | Simple text -> $"SIMPLE: {text}"
                            | WithLogEvent logEvent -> $"LOG: {formatLogEvent logEvent}"
                            | WithSystem sysMsg -> $"SYS: {formatSystemMessage sysMsg}"
                            | Compound (logEvent, sysMsg) -> $"COMPOUND: {formatLogEvent logEvent} | {formatSystemMessage sysMsg}"
                        )
                    ]

                    let formatter = MessageFormatter.create formatters

                    let simpleResult = formatter (Simple "Hello" :> IMessage)
                    let logResult = formatter (WithLogEvent (InfoEvent "Test") :> IMessage)
                    let compoundResult = formatter (Compound (ErrorEvent ("Failed", 500, "Details"), StartupMessage "2.0") :> IMessage)

                    Expect.equal simpleResult "SIMPLE: Hello" "Simple case should be formatted correctly"
                    Expect.equal logResult "LOG: INFO: Test" "Log event case should be formatted correctly"
                    Expect.equal compoundResult "COMPOUND: ERROR[500]: Failed - Details | STARTUP: Version 2.0" "Compound case should be formatted correctly"
                }

                test "should handle inheritance-like behavior with interfaces" {
                    let formatters = [
                        typeof<BaseMessage>, (fun (msg: IMessage) ->
                            let baseMsg = msg :?> BaseMessage
                            $"BASE: {baseMsg.GetDescription()}"
                        )
                    ]

                    let formatter = MessageFormatter.create formatters

                    let derivedResult = formatter (DerivedMessage("Test message") :> IMessage)
                    let anotherDerivedResult = formatter (AnotherDerivedMessage("Error occurred", 404) :> IMessage)

                    Expect.equal derivedResult "BASE: Test message" "Derived message should use base formatter"
                    Expect.equal anotherDerivedResult "BASE: Error occurred (Code: 404)" "Another derived message should use base formatter"
                }

                test "should use first matching formatter in list order" {
                    let formatters = [
                        typeof<BaseMessage>, (fun (msg: IMessage) ->
                            let baseMsg = msg :?> BaseMessage
                            $"BASE: {baseMsg.GetDescription()}"
                        )
                        typeof<DerivedMessage>, (fun (msg: IMessage) ->
                            let derivedMsg = msg :?> DerivedMessage
                            $"DERIVED: {derivedMsg.GetDescription()}"
                        )
                    ]

                    let formatter = MessageFormatter.create formatters

                    let derivedResult = formatter (DerivedMessage("Test message") :> IMessage)
                    let anotherDerivedResult = formatter (AnotherDerivedMessage("Error occurred", 404) :> IMessage)

                    Expect.equal derivedResult "BASE: Test message" "First matching formatter should be used for DerivedMessage"
                    Expect.equal anotherDerivedResult "BASE: Error occurred (Code: 404)" "Base formatter should be used for AnotherDerivedMessage"
                }

                test "should prioritize more specific formatters when listed first" {
                    let formatters = [
                        typeof<DerivedMessage>, (fun (msg: IMessage) ->
                            let derivedMsg = msg :?> DerivedMessage
                            $"DERIVED: {derivedMsg.GetDescription()}"
                        )
                        typeof<BaseMessage>, (fun (msg: IMessage) ->
                            let baseMsg = msg :?> BaseMessage
                            $"BASE: {baseMsg.GetDescription()}"
                        )
                    ]

                    let formatter = MessageFormatter.create formatters

                    let derivedResult = formatter (DerivedMessage("Test message") :> IMessage)
                    let anotherDerivedResult = formatter (AnotherDerivedMessage("Error occurred", 404) :> IMessage)

                    Expect.equal derivedResult "DERIVED: Test message" "More specific formatter should be used when listed first"
                    Expect.equal anotherDerivedResult "BASE: Error occurred (Code: 404)" "Base formatter should be used for non-derived types"
                }

                test "should handle multiple formatters for same discriminated union type" {
                    let formatters = [
                        typeof<LogEventMessage>, (fun (msg: IMessage) ->
                            match msg :?> LogEventMessage with
                            | InfoEvent message -> $"📝 {message}"
                            | WarnEvent (message, code) -> $"⚠️ [{code}] {message}"
                            | ErrorEvent (message, code, details) -> $"❌ [{code}] {message}: {details}"
                        )
                    ]

                    let formatter = MessageFormatter.create formatters

                    let infoResult = formatter (InfoEvent "System operational" :> IMessage)
                    let warnResult = formatter (WarnEvent ("Memory usage high", 2001) :> IMessage)
                    let errorResult = formatter (ErrorEvent ("Database error", 5001, "Connection timeout") :> IMessage)

                    Expect.equal infoResult "📝 System operational" "Info with emoji formatter should work"
                    Expect.equal warnResult "⚠️ [2001] Memory usage high" "Warning with emoji formatter should work"
                    Expect.equal errorResult "❌ [5001] Database error: Connection timeout" "Error with emoji formatter should work"
                }

                test "should handle fallback for unregistered discriminated union types" {
                    let formatters = [
                        typeof<LogEventMessage>, (fun (msg: IMessage) ->
                            match msg :?> LogEventMessage with
                            | InfoEvent message -> $"LOG: {message}"
                            | _ -> "LOG: Other event"
                        )
                    ]

                    let fallback = fun (msg: IMessage) -> $"UNKNOWN: {msg.GetType().Name}"
                    let formatter = MessageFormatter.createWithFallback formatters fallback

                    let knownResult = formatter (InfoEvent "Known message" :> IMessage)
                    let unknownResult = formatter (StartupMessage "1.0" :> IMessage)

                    Expect.equal knownResult "LOG: Known message" "Known discriminated union should use specific formatter"
                    Expect.equal unknownResult "UNKNOWN: StartupMessage" "Unknown discriminated union should use fallback"
                }

                test "should handle empty formatter list gracefully" {
                    let formatters: (Type * (IMessage -> string)) list = []
                    let formatter = MessageFormatter.create formatters

                    let result = formatter (InfoEvent "Test" :> IMessage)

                    Expect.stringContains result "cannot format" "Empty formatter list should have cannot format"
                }

                test "should handle null or exception cases in discriminated union formatters" {
                    let formatters = [
                        typeof<LogEventMessage>, (fun (msg: IMessage) ->
                            match msg :?> LogEventMessage with
                            | InfoEvent message when String.IsNullOrEmpty(message) -> "EMPTY INFO"
                            | InfoEvent message -> $"INFO: {message}"
                            | WarnEvent (_, code) when code < 0 -> "INVALID WARN CODE"
                            | WarnEvent (message, code) -> $"WARN[{code}]: {message}"
                            | ErrorEvent (message, code, details) -> $"ERROR[{code}]: {message} - {details}"
                        )
                    ]

                    let formatter = MessageFormatter.create formatters

                    let emptyResult = formatter (InfoEvent "" :> IMessage)
                    let invalidCodeResult = formatter (WarnEvent ("Test", -1) :> IMessage)
                    let normalResult = formatter (InfoEvent "Normal" :> IMessage)

                    Expect.equal emptyResult "EMPTY INFO" "Empty message should be handled"
                    Expect.equal invalidCodeResult "INVALID WARN CODE" "Invalid code should be handled"
                    Expect.equal normalResult "INFO: Normal" "Normal message should work"
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
                    do! Async.Sleep 1000

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
                        AgentLogging.AgentLoggerDefaults.config with
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
                    let messageLines = lines |> Array.filter (fun l -> l.Contains("Value ="))

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
                    let messageLines = lines |> Array.filter (fun l -> l.Contains("Text = \"error\""))

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
                    let messageLines = lines |> Array.filter (fun l -> l.Contains("Value ="))

                    Expect.isGreaterThan messageLines.Length 500 "Should have processed most messages"

                    do! logger.StopAsync()
                }
            ]

        // Error handling tests
        let errorHandlingTests =
            testList "Error Handling" [

                testAsync "should handle formatter exceptions gracefully" {
                    let badFormatter = fun (_: IMessage) ->
                        failwith "Formatter error!"

                    let config = {
                        AgentLogging.AgentLoggerDefaults.config with
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

        // Agent Logging Tests with proper disposal (use bindings)
        let agentLoggingDisposalTests =
            testList "Agent Logging with Disposal" [

                testAsync "createConsole with use binding should auto-dispose" {
                    use logger = AgentLogging.createConsole()

                    let! result = logger.StartAsync None Level.Informative
                    Expect.isOk result "Start should succeed"

                    // Log a message
                    let msg = createTestMessage "auto-dispose test"
                    Logging.logInfo logger.Logger msg

                    // Wait briefly for processing
                    do! Async.Sleep 200

                    // Stop the logger
                    do! logger.StopAsync()
                }

                testAsync "createDebug with use binding should handle quick operations" {
                    use logger = AgentLogging.createDebug()

                    let! result = logger.StartAsync None Level.Debug
                    Expect.isOk result "Start should succeed"

                    // Log a few messages quickly
                    for i in 1..3 do
                        let msg = createTestMessage $"Quick {i}"
                        Logging.logDebug logger.Logger msg

                    // Brief wait
                    do! Async.Sleep 100

                    let! lines = logger.ReportAsync()
                    let messageLines = lines |> Array.filter (fun l -> l.Contains("Text = \"Quick"))
                    Expect.isGreaterThan messageLines.Length 0 "Should have processed some messages"

                    do! logger.StopAsync()
                }

                testAsync "createHighPerformance with use binding should handle batch logging" {
                    use logger = AgentLogging.createHighPerformance()

                    let! result = logger.StartAsync None Level.Informative
                    Expect.isOk result "Start should succeed"

                    // Log batch of messages
                    for i in 1..50 do
                        let msg = createNumberMessage i
                        Logging.logInfo logger.Logger msg

                    // Wait for processing
                    do! Async.Sleep 300

                    let! lines = logger.ReportAsync()
                    let messageLines = lines |> Array.filter (fun l -> l.Contains("Value ="))
                    Expect.isGreaterThan messageLines.Length 10 "Should have processed many messages"

                    do! logger.StopAsync()
                }

                testAsync "custom config with use binding should respect settings" {
                    let config = {
                        AgentLogging.AgentLoggerDefaults.config with
                            MaxMessages = Some 2
                            FlushInterval = TimeSpan.FromMilliseconds(50.0)
                    }
                    use logger = AgentLogging.createAgentLogger config

                    let! result = logger.StartAsync None Level.Informative
                    Expect.isOk result "Start should succeed"

                    // Log more messages than limit
                    for i in 1..5 do
                        let msg = createTestMessage $"Config {i}"
                        Logging.logInfo logger.Logger msg

                    // Wait for processing
                    do! Async.Sleep 150

                    let! lines = logger.ReportAsync()
                    let messageLines = lines |> Array.filter (fun l -> l.Contains("Text = \"Config"))
                    Expect.hasLength messageLines 2 "Should only keep last 2 messages due to limit"

                    do! logger.StopAsync()
                }

                testAsync "level filtering with use binding should work correctly" {
                    use logger = AgentLogging.createProduction() // Error level only

                    let! result = logger.StartAsync None Level.Error
                    Expect.isOk result "Start should succeed"

                    // Log at different levels
                    let infoMsg = createTestMessage "info-level"
                    let errorMsg = createTestMessage "error-level"

                    Logging.logInfo logger.Logger infoMsg     // Should be filtered out
                    Logging.logError logger.Logger errorMsg   // Should pass through

                    // Wait for processing
                    do! Async.Sleep 200

                    let! lines = logger.ReportAsync()
                    let messageLines = lines |> Array.filter (fun l -> l.Contains("Text = \"error-level\""))

                    Expect.hasLength messageLines 1 "Should only log error messages"

                    do! logger.StopAsync()
                }

                testAsync "write snapshot with use binding should work quickly" {
                    return
                        withTempFile (fun tempFile ->
                            async {
                                use logger = AgentLogging.createDebug()

                                let! result = logger.StartAsync None Level.Informative
                                Expect.isOk result "Start should succeed"

                                // Log a few messages
                                for i in 1..3 do
                                    let msg = createTestMessage $"Snapshot {i}"
                                    Logging.logInfo logger.Logger msg

                                // Brief wait
                                do! Async.Sleep 100

                                // Write snapshot
                                let! writeResult = logger.WriteAsync tempFile
                                Expect.isOk writeResult "Write should succeed"

                                do! logger.StopAsync()

                                // Check snapshot file
                                let content = File.ReadAllText(tempFile)
                                Expect.stringContains content "Snapshot" "File should contain snapshot messages"
                            } |> Async.RunSynchronously
                        )
                }

                testAsync "concurrent use bindings should not interfere" {
                    // Test that multiple loggers with use bindings work independently
                    use logger1 = AgentLogging.createConsole()
                    use logger2 = AgentLogging.createDebug()

                    let! result1 = logger1.StartAsync None Level.Informative
                    let! result2 = logger2.StartAsync None Level.Debug

                    Expect.isOk result1 "Logger1 start should succeed"
                    Expect.isOk result2 "Logger2 start should succeed"

                    // Log to both
                    let msg1 = createTestMessage "logger1-msg"
                    let msg2 = createTestMessage "logger2-msg"

                    Logging.logInfo logger1.Logger msg1
                    Logging.logDebug logger2.Logger msg2

                    // Brief wait
                    do! Async.Sleep 200

                    let! lines1 = logger1.ReportAsync()
                    let! lines2 = logger2.ReportAsync()

                    Expect.isGreaterThan lines1.Length 0 "Logger1 should have messages"
                    Expect.isGreaterThan lines2.Length 0 "Logger2 should have messages"

                    do! logger1.StopAsync()
                    do! logger2.StopAsync()
                }
            ]

        // Main test suite
        [<Tests>]
        let allTests =
            testList "Informedica.Logging.Lib Tests" [
                basicLoggingTests
                messageFormatterTests
                discriminatedUnionFormatterTests
                agentLoggingTests
                agentLoggingDisposalTests
                performanceTests
                errorHandlingTests
            ]