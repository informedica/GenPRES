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


/// The events one solve of a scenario logs at Debug level, in the order they were logged.
let solveEvents (med: Informedica.GenOrder.Lib.Types.Medication) : Event list =
    let recorded = ResizeArray<Event>()

    let collecting: Informedica.Logging.Lib.Logger =
        {
            Log = recorded.Add
            Enabled = fun _ -> true
        }

    med
    |> Informedica.GenOrder.Lib.Medication.toOrderDto Scenarios.testStart
    |> Informedica.GenOrder.Lib.Order.Dto.fromDto
    |> Result.map (fun ord ->
        Informedica.GenOrder.Lib.OrderProcessor.processPipeline
            collecting
            (Informedica.GenOrder.Lib.Types.SolveOrder ord)
    )
    |> ignore

    recorded |> Seq.toList


/// The flat file the agent logger wrote for these events: per event a header line with a
/// count, the elapsed time and the level, then the rendered text; nothing for a blank text.
let flatLinesOf (events: Event list) =
    events
    |> List.map (fun ev -> ev, Logging.EventFormat.formatMessage ev.Message)
    |> List.filter (fun (_, text) -> String.IsNullOrWhiteSpace text |> not)
    |> List.mapi (fun i (ev, text) -> $"%i{i + 1}. 0.000: %A{ev.Level}" :: (text.Split '\n' |> Array.toList))
    |> List.concat
    |> List.toArray


/// Everything the log analysis parses out of the lines of a file.
let parsed (lines: string[]) =
    LogAnalyzer.Parse.orderContext lines,
    LogAnalyzer.Parse.errors lines,
    LogAnalyzer.Parse.equationBlocks lines,
    LogAnalyzer.Parse.constraintTables lines,
    LogAnalyzer.Parse.pipelineSteps lines


/// One solve at Debug level and the file the production sink wrote for it, made once: the
/// console sink prints every event, and one solve is enough of that in a test run.
let solved =
    lazy
        (let events = Scenarios.pcmDrink |> solveEvents
         events, events |> fileLinesOf)


let analysisTests =
    testList
        "the log analysis"
        [
            test "keeps a flat line as it is" {
                let flat = [| "Patient: 3 jaar"; ""; "[a]_x <1..3> = [a]_y <1> * [a]_z <1..3>" |]

                flat |> LogAnalyzer.Parse.textLines |> Expect.equal "unchanged" flat
            }

            test "keeps a line that only looks like JSON" {
                let lines = [| "{not json"; "{\"Other\":1}" |]

                lines |> LogAnalyzer.Parse.textLines |> Expect.equal "unchanged" lines
            }

            test "unfolds the text of a JSON line, after a blank line" {
                [|
                    "{\"@t\":\"x\",\"@l\":\"Debug\",\"EventType\":\"e\",\"Text\":\"one\\ntwo\"}"
                |]
                |> LogAnalyzer.Parse.textLines
                |> Expect.equal "a blank and two lines" [| ""; "one"; "two" |]
            }

            test "reads a solve written by the production sink as it read the flat file" {
                let events, fileLines = solved.Value

                let ctx, errors, equationBlocks, tables, pipelineRuns =
                    fileLines |> LogAnalyzer.Parse.textLines |> parsed

                let flatCtx, flatErrors, flatEquationBlocks, flatTables, flatPipelineRuns =
                    events |> flatLinesOf |> parsed

                equationBlocks |> List.isEmpty |> Expect.isFalse "equations were parsed"
                pipelineRuns |> List.isEmpty |> Expect.isFalse "pipeline steps were parsed"

                ctx |> Expect.equal "the same order context" flatCtx
                errors |> Expect.equal "the same errors" flatErrors
                equationBlocks |> Expect.equal "the same equation blocks" flatEquationBlocks
                tables |> Expect.equal "the same constraint tables" flatTables
                pipelineRuns |> Expect.equal "the same pipeline runs" flatPipelineRuns
            }

            test "the report of a solve is not empty" {
                let ctx, errors, equationBlocks, tables, pipelineRuns =
                    solved.Value |> snd |> LogAnalyzer.Parse.textLines |> parsed

                LogAnalyzer.Report.generate ctx errors equationBlocks tables pipelineRuns
                |> String.IsNullOrWhiteSpace
                |> Expect.isFalse "a report"
            }
        ]


[<Tests>]
let tests = testList "Logging Tests" [ formatTests; sinkTests; analysisTests ]
