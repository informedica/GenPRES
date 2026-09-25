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


    module GenOrderToolsTests =

        open Informedica.GenForm.Lib.Resources
        open Informedica.MCP.Lib.GenOrderTools

        let private emptyInput =
            {
                AgeMonths = Some 24.0
                WeightKg = None
                HeightCm = None
                Sex = None
                Department = None
                Generic = Some "paracetamol"
                Indication = None
                Route = None
                Form = None
            }

        /// A provider that must never be called. Used to prove that createOrderContext and
        /// getOrderScenarios refuse before reaching the resource layer when weight or height
        /// is missing — not just that requireWeightAndHeight alone reports an error, which
        /// would still pass if either function stopped calling the shared guard.
        let private unusedProvider: IResourceProvider = Unchecked.defaultof<_>

        let tests =
            testList
                "requireWeightAndHeight"
                [
                    test "both weight and height present is Ok" {
                        { emptyInput with
                            WeightKg = Some 12.0
                            HeightCm = Some 86.0
                        }
                        |> requireWeightAndHeight
                        |> Expect.isOk "should be Ok"
                    }

                    test "neither weight nor height is Error" {
                        emptyInput |> requireWeightAndHeight |> Expect.isError "should be Error"
                    }

                    test "weight without height is Error" {
                        { emptyInput with WeightKg = Some 12.0 }
                        |> requireWeightAndHeight
                        |> Expect.isError "should be Error"
                    }

                    test "height without weight is Error" {
                        { emptyInput with HeightCm = Some 86.0 }
                        |> requireWeightAndHeight
                        |> Expect.isError "should be Error"
                    }

                    test "createOrderContext refuses before touching the provider when height is missing" {
                        { emptyInput with WeightKg = Some 12.0 }
                        |> createOrderContext unusedProvider
                        |> Expect.isError "should refuse without evaluating"
                    }

                    test "getOrderScenarios refuses before touching the provider when weight is missing" {
                        { emptyInput with HeightCm = Some 86.0 }
                        |> getOrderScenarios unusedProvider
                        |> Expect.isError "should refuse without evaluating"
                    }
                ]


    module CheckDepartmentTests =

        open Informedica.GenForm.Lib.Resources
        open Informedica.GenOrder.Lib
        open Informedica.MCP.Lib.GenOrderTools

        /// The departments the tests prescribe for: two named by rules, the default among them.
        let private departments = Departments.ofNamed [ Some "NEO"; Some "ICC" ]

        /// A provider that answers the departments and nothing else: a test that reaches any
        /// rule raises, so a refusal proves that the guard ran before the rules were read.
        let private departmentsOnly: IResourceProvider =
            { new IResourceProvider with
                member _.Get(key: ResourceKey<'T>) : 'T =
                    if key.Name = Keys.departments.Name then
                        box departments :?> 'T
                    else
                        raise (NotImplementedException key.Name)

                member _.GetData() = raise (NotImplementedException())
                member _.GetUnitMappings() = raise (NotImplementedException())
                member _.GetRouteMappings() = raise (NotImplementedException())
                member _.GetValidForms() = raise (NotImplementedException())
                member _.GetFormRoutes() = raise (NotImplementedException())
                member _.GetFormularyProducts() = raise (NotImplementedException())
                member _.GetReconstitution() = raise (NotImplementedException())
                member _.GetParenteralMeds() = raise (NotImplementedException())
                member _.GetEnteralFeeding() = raise (NotImplementedException())
                member _.GetProducts() = raise (NotImplementedException())
                member _.GetDoseRules() = raise (NotImplementedException())
                member _.GetSolutionRules() = raise (NotImplementedException())
                member _.GetRenalRules() = raise (NotImplementedException())
                member _.GetTotals() = raise (NotImplementedException())
                member _.GetGStandProvider() = raise (NotImplementedException())
                member _.GetResourceInfo() = raise (NotImplementedException())
            }

        /// A provider that answers the departments and no rule at all, so an evaluation that
        /// passes the guards is refused by the domain for lack of rules and by nothing else.
        let private emptyRules: IResourceProvider =
            { new IResourceProvider with
                member _.Get(key: ResourceKey<'T>) : 'T =
                    if key.Name = Keys.departments.Name then
                        box departments :?> 'T
                    else
                        raise (NotImplementedException key.Name)

                member _.GetData() = raise (NotImplementedException())
                member _.GetUnitMappings() = raise (NotImplementedException())
                member _.GetRouteMappings() = [||]
                member _.GetValidForms() = raise (NotImplementedException())
                member _.GetFormRoutes() = raise (NotImplementedException())
                member _.GetFormularyProducts() = raise (NotImplementedException())
                member _.GetReconstitution() = [||]
                member _.GetParenteralMeds() = raise (NotImplementedException())
                member _.GetEnteralFeeding() = raise (NotImplementedException())
                member _.GetProducts() = raise (NotImplementedException())
                member _.GetDoseRules() = [||]
                member _.GetSolutionRules() = [||]
                member _.GetRenalRules() = [||]
                member _.GetTotals() = raise (NotImplementedException())
                member _.GetGStandProvider() = raise (NotImplementedException())
                member _.GetResourceInfo() = raise (NotImplementedException())
            }

        /// A provider that must never be called.
        let private unusedProvider: IResourceProvider = Unchecked.defaultof<_>

        let private measured: CreateOrderContextInput =
            {
                AgeMonths = Some 24.0
                WeightKg = Some 12.0
                HeightCm = Some 86.0
                Sex = None
                Department = None
                Generic = Some "paracetamol"
                Indication = None
                Route = None
                Form = None
            }

        let tests =
            testList
                "checkDepartment"
                [
                    test "the names are the ones the rules name and the default, sorted" {
                        departments.Names |> Expect.equal "the names" [| "ICC"; "ICK"; "NEO" |]
                    }

                    test "no department is Ok and stays none, so the default applies" {
                        measured
                        |> checkDepartment departments
                        |> Expect.equal "unchanged" (Ok measured)
                    }

                    test "a department the rules name is Ok as given" {
                        { measured with Department = Some "NEO" }
                        |> checkDepartment departments
                        |> Result.map _.Department
                        |> Expect.equal "NEO" (Ok(Some "NEO"))
                    }

                    test "a department in another case is Ok as the rules spell it" {
                        { measured with Department = Some "neo" }
                        |> checkDepartment departments
                        |> Result.map _.Department
                        |> Expect.equal "NEO" (Ok(Some "NEO"))
                    }

                    test "a department with spaces around it is Ok trimmed" {
                        { measured with Department = Some " icc " }
                        |> checkDepartment departments
                        |> Result.map _.Department
                        |> Expect.equal "ICC" (Ok(Some "ICC"))
                    }

                    test "the default itself is Ok as given" {
                        { measured with Department = Some "ICK" }
                        |> checkDepartment departments
                        |> Result.map _.Department
                        |> Expect.equal "ICK" (Ok(Some "ICK"))
                    }

                    test "a department the rules do not name is refused, naming the ones they do" {
                        match { measured with Department = Some "PICU" } |> checkDepartment departments with
                        | Ok _ -> failtest "should refuse"
                        | Error msg ->
                            msg |> Expect.stringContains "names the input" "'PICU'"
                            msg |> Expect.stringContains "names the known" "ICC, ICK, NEO"
                            msg |> Expect.stringContains "names the default" "ICK"
                    }

                    test "when the rules spell a name in two cases, the spelling given wins" {
                        let both = Departments.ofNamed [ Some "NEO"; Some "neo" ]

                        { measured with Department = Some "neo" }
                        |> checkDepartment both
                        |> Result.map _.Department
                        |> Expect.equal "neo as given" (Ok(Some "neo"))
                    }

                    test "when the rules spell a name in two cases, a third case is refused" {
                        let both = Departments.ofNamed [ Some "NEO"; Some "neo" ]

                        { measured with Department = Some "Neo" }
                        |> checkDepartment both
                        |> Expect.isError "ambiguous, so refused"
                    }

                    test "an empty department is refused, not taken as none" {
                        { measured with Department = Some "" }
                        |> checkDepartment departments
                        |> Expect.isError "should refuse"
                    }

                    test "the checked department is the one the patient is built with" {
                        { measured with Department = Some "neo" }
                        |> checkDepartment departments
                        |> Result.map (buildPatient departmentsOnly >> Patient.getDepartment)
                        |> Expect.equal "NEO on the patient" (Ok(Some "NEO"))
                    }

                    test "evaluateOrderContext with an accepted department reaches the rules" {
                        // no rule at all is loaded, so the evaluation stops for lack of rules:
                        // the domain's refusal, not the department's, which proves the guard
                        // let the department through and the patient was built with it
                        { measured with Department = Some "neo" }
                        |> evaluateOrderContext emptyRules
                        |> Result.mapError (fun msg -> msg.Contains "Unknown department")
                        |> Expect.equal "refused for lack of rules, not for the department" (Error false)
                    }

                    test "evaluateOrderContext refuses an unknown department before reading any rule" {
                        { measured with Department = Some "PICU" }
                        |> evaluateOrderContext departmentsOnly
                        |> Expect.isError "should refuse without evaluating"
                    }

                    test "evaluateOrderContext refuses a missing measure before touching the provider" {
                        { measured with
                            HeightCm = None
                            Department = Some "PICU"
                        }
                        |> evaluateOrderContext unusedProvider
                        |> Result.mapError (fun msg -> msg.Contains "HeightCm")
                        |> Expect.equal "the measure, not the department" (Error true)
                    }
                ]


    [<Tests>]
    let tests =
        testList
            "MCP"
            [
                testHelloWorld
                loggingTests
                GenOrderToolsTests.tests
                CheckDepartmentTests.tests
            ]
