/// The medication rows of the emergency list: the texts of a row, the context of a row narrowed to its scenario, the
/// choice of the rule set rows and of the next options to evaluate, the evaluation with its warnings, and the rows for
/// a patient.
module Informedica.GenPRES.Server.Tests.EmergencyListCommandTests

open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
// Opened after Expecto and GenFORM, so that its names win over the same names there.
open Informedica.GenPRES.Shared.Types
open ServerApi
open ServerApi.EmergencyListCommand
open Informedica.GenPRES.Server.Tests.StubAdapterTests.StubAdapters


module Medication = Informedica.GenOrder.Lib.Medication
module Outcome = Informedica.GenOrder.Lib.Types
module SharedContext = Informedica.GenPRES.Shared.Models.OrderContext
module SharedFormulary = Informedica.GenPRES.Shared.Models.Formulary
module SharedOrder = Informedica.GenPRES.Shared.Models.Order
module SharedPatient = Informedica.GenPRES.Shared.Models.PatientData
module SharedScenario = Informedica.GenPRES.Shared.Models.OrderScenario

type LogEvent = Informedica.Logging.Lib.Event
type Logger = Informedica.Logging.Lib.Logger


/// Fixtures for the tests: order variables, a scenario of a paracetamol suppository and rule sets.
module Fixtures =

    let valueUnit values unit =
        SharedOrder.ValueUnit.create (values |> Array.map (fun (v: decimal) -> string v, v)) unit "" false "" ""


    let variable min max vals =
        SharedOrder.Variable.create "v" false min true None max true vals


    let orderVariable var =
        SharedOrder.OrderVariable.create "ov" var var var None Level.IsNormal


    let withVals values unit =
        variable None None (Some(valueUnit values unit)) |> orderVariable


    let withMinMax min max unit =
        variable
            (min |> Option.map (fun v -> valueUnit [| v |] unit))
            (max |> Option.map (fun v -> valueUnit [| v |] unit))
            None
        |> orderVariable


    let empty = variable None None None |> orderVariable


    /// A paracetamol suppository order from the test scenarios, as the contract carries it.
    let order =
        Scenarios.pcmSupp
        |> Medication.toOrderDto Scenarios.testStart
        |> Mappers.Order.mapFromOrderToShared [| "paracetamol" |]


    /// The order with the dose quantity of the orderable and the concentration of the paracetamol item set.
    let withPreparation qty cnc =
        let o = order.Orderable
        let c = o.Components[0]
        let i = c.Items[0]

        { order with
            Orderable =
                { o with
                    Dose = { o.Dose with Quantity = qty }
                    Components = [| { c with Items = [| { i with OrderableConcentration = cnc } |] } |]
                }
        }


    let scenarioOf ord item : OrderScenario =
        SharedScenario.create
            "Noodlijst Reanimatie"
            "paracetamol"
            "zetpil"
            "rect"
            (Once "")
            None
            (Some "paracetamol")
            item
            [||]
            [| "paracetamol" |]
            [| "paracetamol" |]
            [||]
            [||]
            [||]
            ord
            false
            false
            None
            false
            false
            false
            [| "gpk-1" |]
            None


    let scenario item = scenarioOf order item


    let context =
        { SharedContext.empty with
            Id = "c-1"
            PatientData = StubEhrPatient.contractPatient
            Filter =
                { SharedContext.filter with
                    Indication = Some "Noodlijst Reanimatie"
                    Generic = Some "paracetamol"
                }
            Scenarios = [| scenario (Some "paracetamol"); scenario None |]
        }


    let ruleSetRow category indication generic : Types.Data.RuleSetData =
        {
            Category = category
            Label = ""
            Indication = indication
            Generic = generic
            Location = None
            Department = None
        }


    let ruleSet kind rows =
        {
            Kind = kind
            Label = ""
            Rows = rows
        }


    /// The context without scenarios and with this filter.
    let withoutScenarios filter =
        { context with
            Filter = filter
            Scenarios = [||]
        }


    /// An order context port that answers every evaluation with the outcome.
    let answering outcome : OrderContextPort =
        {
            evaluate = fun _ _ -> async { return outcome }
            linkedOrders = fun _ -> async { return Ok [||] }
        }


    /// An order context port that evaluates every context to the context.
    let evaluatingTo ctx = answering (Ok(Outcome.Evaluated(portContext ctx)))


    /// The environment of the tests with the order context port and a logger that collects the events.
    let envWith port =
        let events = ResizeArray<LogEvent>()

        let logger: Logger =
            {
                Log = events.Add
                Enabled = fun _ -> true
            }

        { makeEnv (formularyAlwaysOk SharedFormulary.empty) port with logger = logger }, events


    /// The texts of the logged events.
    let texts (events: ResizeArray<LogEvent>) =
        events
        |> Seq.map (_.Message >> Logging.ServerLogging.formatMessage)
        |> Seq.toList


[<Tests>]
let tests =
    testList
        "EmergencyListCommand"
        [
            testList
                "branches"
                [
                    let contextWith (f: Filter) = { SharedContext.empty with Filter = f }

                    test "chooses each generic first" {
                        { SharedContext.filter with
                            Generics = [| "a"; "b" |]
                            Routes = [| "iv"; "rect" |]
                        }
                        |> contextWith
                        |> branches
                        |> Array.map _.Filter.Generic
                        |> Expect.equal "the generics" [| Some "a"; Some "b" |]
                    }

                    test "chooses each route when the generic is chosen" {
                        { SharedContext.filter with
                            Generic = Some "a"
                            Routes = [| "iv"; "rect" |]
                        }
                        |> contextWith
                        |> branches
                        |> Array.map _.Filter.Route
                        |> Expect.equal "the routes" [| Some "iv"; Some "rect" |]
                    }

                    test "chooses each dose type when the generic and the route are chosen" {
                        { SharedContext.filter with
                            Generic = Some "a"
                            Route = Some "iv"
                            DoseTypes = [| Once ""; Continuous "" |]
                        }
                        |> contextWith
                        |> branches
                        |> Array.map _.Filter.DoseType
                        |> Expect.equal "the dose types" [| Some(Once ""); Some(Continuous "") |]
                    }

                    test "chooses each form when the generic, the route and the dose type are chosen" {
                        { SharedContext.filter with
                            Generic = Some "a"
                            Route = Some "iv"
                            DoseType = Some(Once "")
                            Forms = [| "ampul"; "flacon" |]
                        }
                        |> contextWith
                        |> branches
                        |> Array.map _.Filter.Form
                        |> Expect.equal "the forms" [| Some "ampul"; Some "flacon" |]
                    }

                    test "is empty when the list of generics has one option" {
                        { SharedContext.filter with Generics = [| "a" |] }
                        |> contextWith
                        |> branches
                        |> Expect.isEmpty "no options"
                    }

                    test "is empty when the list of routes has one option" {
                        { SharedContext.filter with
                            Generic = Some "a"
                            Routes = [| "iv" |]
                        }
                        |> contextWith
                        |> branches
                        |> Expect.isEmpty "no options"
                    }
                ]

            testList
                "categoryOf"
                [
                    test "leaves out the prefix" {
                        "Noodlijst Reanimatie"
                        |> categoryOf "Noodlijst "
                        |> Expect.equal "the category" "Reanimatie"
                    }

                    test "keeps an indication without the prefix" {
                        "Koorts" |> categoryOf "Noodlijst " |> Expect.equal "the indication" "Koorts"
                    }
                ]

            testList
                "variableText"
                [
                    test "shows every value with the unit" {
                        Fixtures.withVals [| 1m; 2.5m |] "mg"
                        |> variableText
                        |> Expect.equal "the values" "1 ; 2,5 mg"
                    }

                    test "shows the minimum and the maximum without values" {
                        Fixtures.withMinMax (Some 1m) (Some 2m) "mg"
                        |> variableText
                        |> Expect.equal "the range" "1 - 2 mg"
                    }

                    test "shows a minimum alone as min" {
                        Fixtures.withMinMax (Some 1m) None "mg"
                        |> variableText
                        |> Expect.equal "the minimum" "min 1 mg"
                    }

                    test "shows a maximum alone as max" {
                        Fixtures.withMinMax None (Some 1m) "mg"
                        |> variableText
                        |> Expect.equal "the maximum" "max 1 mg"
                    }

                    test "is empty without values and limits" {
                        Fixtures.empty |> variableText |> Expect.equal "no text" ""
                    }
                ]

            testList
                "withAdjust"
                [
                    let qty = Fixtures.withVals [| 10m |] "mg"
                    let adj = Fixtures.withVals [| 1m |] "mg/kg"

                    test "adds the quantity per kg in brackets when the order is per kg" {
                        withAdjust true qty adj |> Expect.equal "with per kg" "10 mg (1 mg/kg)"
                    }

                    test "shows the quantity alone when the order is not per kg" {
                        withAdjust false qty adj |> Expect.equal "the quantity" "10 mg"
                    }

                    test "shows the quantity alone when the quantity per kg has no text" {
                        withAdjust true qty Fixtures.empty |> Expect.equal "the quantity" "10 mg"
                    }
                ]

            testList
                "narrowed"
                [
                    let sc = Fixtures.context.Scenarios[1]
                    let ctx = narrowed Fixtures.context sc

                    test "takes the form of the scenario" { ctx.Filter.Form |> Expect.equal "the form" (Some "zetpil") }

                    test "keeps only the scenario" { ctx.Scenarios |> Expect.equal "one scenario" [| sc |] }
                ]

            testList
                "toIntervention"
                [
                    let ctx = Fixtures.context
                    let withItem = toIntervention (ctx, ctx.Scenarios[0])
                    let byVolume = toIntervention (ctx, ctx.Scenarios[1])

                    test "takes the category from the indication without Noodlijst" {
                        withItem.Category |> Expect.equal "the category" "Reanimatie"
                    }

                    test "takes the name from the generic of the filter" {
                        withItem.Name |> Expect.equal "the name" "paracetamol"
                    }

                    test "carries the context narrowed to the scenario" {
                        withItem.OrderContext
                        |> Expect.equal "the narrowed context" (Some(narrowed ctx ctx.Scenarios[0]))
                    }

                    test "shows the dose of the item" {
                        let i = Fixtures.order.Orderable.Components |> Array.collect _.Items |> Array.head

                        withItem.SubstanceDoseText
                        |> Expect.equal "the dose of the item" (withAdjust false i.Dose.Quantity i.Dose.QuantityAdjust)
                    }

                    test "shows the dose of the orderable with the name of the scenario for a scenario without item" {
                        byVolume.InterventionDoseText
                        |> Expect.equal
                            "the dose of the orderable"
                            $"%s{Fixtures.order.Orderable.Dose.Quantity |> variableText} paracetamol"
                    }

                    test "shows the dose of the orderable and the concentration of the item for a scenario with item" {
                        let ord =
                            Fixtures.withPreparation
                                (Fixtures.withVals [| 1m |] "stuk")
                                (Fixtures.withVals [| 240m |] "mg/stuk")

                        toIntervention (ctx, Fixtures.scenarioOf ord (Some "paracetamol"))
                        |> _.InterventionDoseText
                        |> Expect.equal "the preparation" "1 stuk van 240 mg/stuk"
                    }

                    test "leaves the preparation empty when the item has no concentration" {
                        let ord = Fixtures.withPreparation (Fixtures.withVals [| 1m |] "stuk") Fixtures.empty

                        toIntervention (ctx, Fixtures.scenarioOf ord (Some "paracetamol"))
                        |> _.InterventionDoseText
                        |> Expect.equal "no preparation" ""
                    }

                    test "leaves the advice empty" { withItem.Text |> Expect.equal "no advice" "" }
                ]

            testList
                "emergencyRows"
                [
                    test "takes only the rows of the rule sets of the kind EmergencyList" {
                        [|
                            Fixtures.ruleSet
                                RuleSetKind.EmergencyList
                                [| Fixtures.ruleSetRow "EmergencyList" "Noodlijst Reanimatie" "" |]
                            Fixtures.ruleSet
                                RuleSetKind.StandardInfusions
                                [| Fixtures.ruleSetRow "StandardInfusions" "Standaard infusie" "" |]
                        |]
                        |> emergencyRows
                        |> Array.map _.Indication
                        |> Expect.equal "the emergency rows" [| "Noodlijst Reanimatie" |]
                    }
                ]

            testList
                "contextOf"
                [
                    test "takes the indication and the generic of the row" {
                        Fixtures.ruleSetRow "EmergencyList" "Noodlijst Reanimatie" "adrenaline"
                        |> contextOf false SharedPatient.empty
                        |> _.Filter
                        |> fun f -> f.Indication, f.Generic
                        |> Expect.equal "the filter" (Some "Noodlijst Reanimatie", Some "adrenaline")
                    }

                    test "leaves the generic open for a row without a generic" {
                        Fixtures.ruleSetRow "EmergencyList" "Noodlijst Reanimatie" ""
                        |> contextOf false SharedPatient.empty
                        |> _.Filter.Generic
                        |> Expect.isNone "no generic"
                    }
                ]

            testList
                "scenarios"
                [
                    let request = Fixtures.withoutScenarios Fixtures.context.Filter

                    testAsync "returns the context with each of its scenarios" {
                        let env, _ = Fixtures.envWith (Fixtures.evaluatingTo Fixtures.context)
                        let! found = request |> scenarios env 0

                        found
                        |> Array.map (snd >> _.Item)
                        |> Expect.equal "the scenarios" [| Some "paracetamol"; None |]
                    }

                    testAsync "warns when no choice is open" {
                        let env, events =
                            Fixtures.envWith (Fixtures.evaluatingTo (Fixtures.withoutScenarios Fixtures.context.Filter))

                        let! found = request |> scenarios env 0
                        found |> Expect.isEmpty "no scenarios"

                        events
                        |> Fixtures.texts
                        |> Expect.equal
                            "the warning"
                            [
                                "EmergencyList: Noodlijst Reanimatie, paracetamol gives no rows: no scenarios and no open choice"
                            ]
                    }

                    testAsync "evaluates each option of an open choice, at most four choices deep" {
                        let openRoutes =
                            { Fixtures.context.Filter with
                                Route = None
                                Routes = [| "iv"; "rect" |]
                            }

                        // The port answers the same open choice again, so every branch goes four choices deep.
                        let env, events =
                            Fixtures.envWith (Fixtures.evaluatingTo (Fixtures.withoutScenarios openRoutes))
                        let! found = request |> scenarios env 0
                        found |> Expect.isEmpty "no scenarios"

                        events
                        |> Fixtures.texts
                        |> List.filter (fun s -> s.EndsWith "no scenarios after four choices")
                        |> List.length
                        |> Expect.equal "a warning for each of the 16 contexts four choices deep" 16
                    }

                    testAsync "warns when the context is refused" {
                        let env, events =
                            Fixtures.envWith (
                                Fixtures.answering (
                                    Ok(Outcome.Refused(portContext request, Outcome.Refusal.NoDoseRules))
                                )
                            )

                        let! found = request |> scenarios env 0
                        found |> Expect.isEmpty "no scenarios"

                        events
                        |> Fixtures.texts
                        |> Expect.equal
                            "the warning"
                            [
                                "EmergencyList: Noodlijst Reanimatie, paracetamol gives no rows: refused, NoDoseRules"
                            ]
                    }

                    testAsync "warns when the evaluation fails" {
                        let env, events = Fixtures.envWith (orderContextAlwaysFails [| "failed" |])
                        let! found = request |> scenarios env 0
                        found |> Expect.isEmpty "no scenarios"

                        events
                        |> Fixtures.texts
                        |> Expect.equal
                            "the warning"
                            [ "EmergencyList: Noodlijst Reanimatie, paracetamol gives no rows: failed" ]
                    }
                ]

            testList
                "processCmd"
                [
                    testAsync "returns a row for each scenario of the rule set rows of the emergency list" {
                        let env, _ = Fixtures.envWith (Fixtures.evaluatingTo Fixtures.context)

                        let env =
                            { env with
                                ruleSets =
                                    fun _ ->
                                        [|
                                            Fixtures.ruleSet
                                                RuleSetKind.EmergencyList
                                                [|
                                                    Fixtures.ruleSetRow
                                                        "EmergencyList"
                                                        "Noodlijst Reanimatie"
                                                        "paracetamol"
                                                |]
                                        |]
                            }

                        match! StubEhrPatient.contractPatient |> processCmd env with
                        | Ok rows ->
                            rows
                            |> Array.map (fun r -> r.Category, r.OrderContext |> Option.map _.Scenarios.Length)
                            |> Expect.equal "two rows of one scenario" [| "Reanimatie", Some 1; "Reanimatie", Some 1 |]
                        | Error errs -> failtest $"expected the rows, got %A{errs}"
                    }

                    testAsync "returns the messages for invalid patient data" {
                        let env, _ = Fixtures.envWith (Fixtures.evaluatingTo Fixtures.context)

                        match! SharedPatient.empty |> processCmd env with
                        | Ok _ -> failtest "expected the messages, got rows"
                        | Error errs -> errs |> Expect.isNonEmpty "the messages"
                    }
                ]
        ]
