module Informedica.GenPRES.Server.Tests.LoggingTests

open System
open System.IO
open System.Text.Json
open Expecto
open Expecto.Flip
open Serilog.Events
open Serilog.Formatting
open Serilog.Parsing

type Event = Informedica.Logging.Lib.Event
type Level = Informedica.Logging.Lib.Level
type IMessage = Informedica.Logging.Lib.IMessage


/// An order message whose text is the string given.
let orderMessage (text: string) : IMessage =
    text
    |> Informedica.GenOrder.Lib.Types.Events.OrderScenario
    |> Informedica.GenOrder.Lib.Types.Logging.OrderEventMessage
    :> IMessage


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
        ev.Level |> Logging.SerilogBridge.toSerilogLevel,
        null,
        MessageTemplateParser().Parse Logging.SerilogBridge.Template,
        [ LogEventProperty(Logging.SerilogBridge.EventProperty, ScalarValue ev) ]
    )


let formatWith (formatter: ITextFormatter) (logEvent: LogEvent) =
    use writer = new StringWriter()
    formatter.Format(logEvent, writer)
    writer.ToString()


let jsonLine = formatWith (Logging.EventFormat.JsonLines Logging.EventFormat.formatMessage)


/// What the production sink configuration writes to its file for the events logged.
let fileLinesOf (events: Event list) =
    let path = Path.Combine(Path.GetTempPath(), $"genpres_test_{Guid.NewGuid():N}.log")

    try
        let serilogLogger = Logging.SerilogLogging.buildLoggerAt path Level.Debug
        let logger = serilogLogger :> Serilog.ILogger |> Logging.SerilogBridge.toLogger

        for ev in events do
            logger.Log ev

        // disposing flushes both queues
        (serilogLogger :> IDisposable).Dispose()

        if File.Exists path then File.ReadAllLines path else [||]
    finally
        if File.Exists path then
            File.Delete path


let textOf (line: string) =
    use doc = JsonDocument.Parse line
    doc.RootElement.GetProperty("Text").GetString()


let formatTests =
    testList
        "the JSON line"
        [
            test "is pinned, tools read it" {
                let exp =
                    "{\"@t\":\"2026-09-21T19:40:03.0000000+02:00\",\"@l\":\"Debug\","
                    + "\"EventType\":\"OrderEventMessage\","
                    + "\"Text\":\"Patient: a \\\"quoted\\\" name\\nGeneric: paracetamol 10 µg\"}\n"

                "Patient: a \"quoted\" name\nGeneric: paracetamol 10 µg"
                |> orderMessage
                |> eventOf Level.Debug
                |> logEventOf
                |> jsonLine
                |> Expect.equal "the line" exp
            }

            test "a blank text writes nothing" {
                ""
                |> orderMessage
                |> eventOf Level.Debug
                |> logEventOf
                |> jsonLine
                |> Expect.equal "nothing" ""
            }

            test "a server request is rendered, not refused" {
                Logging.ServerLogging.Request("GET", "/api/x", "127.0.0.1")
                |> eventOf Level.Informative
                |> logEventOf
                |> jsonLine
                |> textOf
                |> Expect.equal "the request" "GET /api/x from 127.0.0.1"
            }

            test "a formatter that throws still yields a line" {
                let text, typeName =
                    "anything"
                    |> orderMessage
                    |> eventOf Level.Error
                    |> logEventOf
                    |> Logging.EventFormat.render (fun _ -> invalidOp "boom")

                text
                |> Expect.stringContains "says what failed" "could not format OrderEventMessage: boom"
                typeName |> Expect.equal "the type" "OrderEventMessage"
            }

            test "a log event from elsewhere keeps its own message" {
                let logEvent =
                    LogEvent(
                        DateTimeOffset.UnixEpoch,
                        LogEventLevel.Warning,
                        null,
                        MessageTemplateParser().Parse "disk almost full",
                        []
                    )

                logEvent |> jsonLine |> textOf |> Expect.equal "the message" "disk almost full"
            }
        ]


let sinkTests =
    testList
        "the production sink configuration"
        [
            test "writes one physical line per event, and the text comes back whole" {
                let text = "=== Start solving Equation ===\nx = y * z\n=== Solver Finished Solving ==="

                let lines = [ text |> orderMessage |> eventOf Level.Debug ] |> fileLinesOf

                lines |> Array.length |> Expect.equal "one line" 1
                lines[0] |> textOf |> Expect.equal "the text" text
            }

            test "skips a blank event between two others and keeps the order" {
                [ "first"; ""; "second" ]
                |> List.map (orderMessage >> eventOf Level.Informative)
                |> fileLinesOf
                |> Array.map textOf
                |> Expect.equal "two lines" [| "first"; "second" |]
            }
        ]


[<Tests>]
let tests = testList "Logging Tests" [ formatTests; sinkTests ]
