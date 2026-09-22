namespace Informedica.MCP.Tests


module Tests =

    open System
    open System.IO
    open System.Text.Json
    open Expecto
    open Expecto.Flip
    open Serilog.Events
    open Serilog.Formatting
    open Serilog.Parsing
    open Informedica.Logging.Lib
    open Informedica.MCP.Lib

    let testHelloWorld =
        test "hello world test" { "Hello World" |> Expect.equal "Strings should be equal" "Hello World" }


    let eventOf (level: Level) (msg: IMessage) : Event =
        {
            TimeStamp = DateTime(2026, 9, 21, 19, 40, 3)
            Level = level
            Message = msg
        }


    /// The log event the bridge would hand a sink for this Event, at a fixed time.
    let logEventOf (ev: Event) =
        LogEvent(
            DateTimeOffset(2026, 9, 21, 19, 40, 3, TimeSpan.FromHours 2.),
            ev.Level |> McpLogging.SerilogBridge.toSerilogLevel,
            null,
            MessageTemplateParser().Parse McpLogging.SerilogBridge.Template,
            [ LogEventProperty(McpLogging.SerilogBridge.EventProperty, ScalarValue ev) ]
        )


    let jsonLine (logEvent: LogEvent) =
        use writer = new StringWriter()

        let renderer = McpLogging.EventFormat.Renderer McpLogging.EventFormat.formatMessage

        (McpLogging.EventFormat.JsonLines renderer :> ITextFormatter).Format(logEvent, writer)

        writer.ToString()


    let textOf (line: string) =
        use doc = JsonDocument.Parse line
        doc.RootElement.GetProperty("Text").GetString()


    /// What the production sink configuration writes to its file for the events logged.
    let fileLinesOf (events: Event list) =
        let path = Path.Combine(Path.GetTempPath(), $"genpres_mcp_test_{Guid.NewGuid():N}.log")

        try
            let serilogLogger = McpLogging.buildLoggerAt path Level.Debug
            let logger = serilogLogger :> Serilog.ILogger |> McpLogging.SerilogBridge.toLogger

            for ev in events do
                logger.Log ev

            // disposing flushes both queues
            (serilogLogger :> IDisposable).Dispose()

            if File.Exists path then File.ReadAllLines path else [||]
        finally
            if File.Exists path then
                File.Delete path


    let loggingTests =
        testList
            "logging"
            [
                test "the JSON line of a tool call is pinned, tools read it" {
                    let exp =
                        "{\"@t\":\"2026-09-21T19:40:03.0000000+02:00\",\"@l\":\"Information\","
                        + "\"EventType\":\"ToolCallCompleted\","
                        + "\"Text\":\"tool get_formulary completed in 12.5 ms\"}\n"

                    McpMessage.ToolCallCompleted("get_formulary", 12.5)
                    |> eventOf Level.Informative
                    |> logEventOf
                    |> jsonLine
                    |> Expect.equal "the line" exp
                }

                test "every MCP message is rendered, not refused" {
                    [
                        McpMessage.ServerStarted "stdio" :> IMessage, "MCP server started on stdio"
                        McpMessage.ToolCallStarted("t", [| "a"; "b" |]), "tool t started with arguments: a, b"
                        McpMessage.ToolCallFailed("t", 3., "no rule"), "tool t failed after 3.0 ms: no rule"
                    ]
                    |> List.iter (fun (msg, exp) ->
                        msg
                        |> eventOf Level.Informative
                        |> logEventOf
                        |> jsonLine
                        |> textOf
                        |> Expect.equal "the text" exp
                    )
                }

                test "the production sink configuration writes one line per event, in order" {
                    [
                        McpMessage.ToolCallStarted("t", [||]) :> IMessage
                        McpMessage.ToolCallCompleted("t", 1.)
                    ]
                    |> List.map (eventOf Level.Informative)
                    |> fileLinesOf
                    |> Array.map textOf
                    |> Expect.equal "two lines" [| "tool t started with arguments: "; "tool t completed in 1.0 ms" |]
                }
            ]


    [<Tests>]
    let tests = testList "MCP" [ testHelloWorld; loggingTests ]
