/// Whether an order can be prepared: a step from the Divisible and a limit to count up to. The
/// scenarios say why when an order cannot, and an order that fits only without the Divisible
/// stays marked as far as it travels.
module StrictPreparationTests

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


/// The medications and the rule the preparation is tested on.
module Fixture =

    let withoutDivisible (m: Medication) =
        { m with
            Div = None
            Components = m.Components |> List.map (fun pc -> { pc with Divisible = None })
        }


    let divided = lazy (Scenarios.pcmDrink |> OrderValueCommandTests.built)
    let undivided = lazy (Scenarios.pcmDrink |> withoutDivisible |> OrderValueCommandTests.built)

    /// Before the first calculation the quantity has the step and no limit yet.
    let openEnded =
        lazy
            (match Scenarios.pcmDrink |> Medication.toOrder Scenarios.testStart with
             | Ok ord -> ord
             | Error e -> failtestf "no order: %A" e)


    let scenario fitsOnlyWithoutDivisible no ord =
        { FilterFixtures.scenario no Scenarios.pcmDrink with
            Order = ord
            Form = "drank"
            FitsOnlyWithoutDivisible = fitsOnlyWithoutDivisible
        }
        |> OrderScenario.setOrderTableFormat


    let warning = OrderScenario.notPreparableWarning |> Warning
    let note = OrderScenario.noPreparationNote |> Caution
    let prescribed (sc: OrderScenario) = sc.Prescription |> Array.collect id


    /// The rule of the suppository with a product of the strength in mg and the Divisible.
    let suppositoryRule strength divisible =
        let stuk = Units.General.general "stuk"
        let perStuk = Units.Mass.milliGram |> ValueUnit.per stuk

        OrderPlanTests.RuleFixtures.rules [| OrderPlanTests.RuleFixtures.row Resources.Departments.defaultDepartment |]
        |> OrderPlanTests.RuleFixtures.mapProducts (fun p ->
            { p with
                GPK = "1"
                Divisible = divisible
                FormUnit = stuk
                FormQuantities = 1N |> ValueUnit.singleWithUnit stuk
                Substances =
                    p.Substances
                    |> Array.map (fun s ->
                        { s with Concentration = strength |> ValueUnit.singleWithUnit perStuk |> Some }
                    )
            }
        )


    /// The suppository of 240 mg with a Divisible of 1: the rule allows at most 100 mg, so a whole
    /// suppository does not fit and only the retry gives an order.
    let wholeSuppository = suppositoryRule 240N (Some 1N)


    /// The rule as a rule of another form.
    let ofForm form (rules: DoseRule[]) =
        rules
        |> Array.map (fun dr -> { dr with DoseRule.Generic.Form = form |> PharmaceuticalForm.fromString })
        |> OrderPlanTests.RuleFixtures.mapProducts (fun p -> { p with Form = form })


    /// The same rule in two forms: "zetpil" with a Divisible, and "drank" without one.
    let twoForms =
        [|
            yield! suppositoryRule 100N (Some 1N) |> ofForm "zetpil"
            yield! suppositoryRule 100N None |> ofForm "drank"
        |]


    /// The paracetamol context with its order marked.
    let markedContext =
        let ctx = OrderPlanTests.EvaluateFixtures.pcmContext

        { ctx with
            Scenarios =
                ctx.Scenarios
                |> Array.map (fun sc -> { sc with FitsOnlyWithoutDivisible = true })
        }


    let child =
        { Patient.patient with
            Age = Some(ValueUnit.singleWithUnit Units.Time.day 365N)
            Weight = Some(ValueUnit.singleWithUnit Units.Weight.kiloGram 10N)
            Height = Some(ValueUnit.singleWithUnit Units.Height.centiMeter 75N)
            Department = Some Resources.Departments.defaultDepartment
        }


    let withoutDivisibleContext =
        lazy
            (let provider = OrderPlanTests.RulesOf wholeSuppository :> Resources.IResourceProvider

             child
             |> OrderContext.create OrderLogging.noOp provider
             |> fun ctx -> ctx |> OrderContext.Command.UpdateOrderContext
             |> OrderContext.evaluate Scenarios.testStart OrderLogging.noOp provider)


open Fixture


[<Tests>]
let tests =
    testList
        "the strict preparation"
        [
            test "the fixtures are preparable, without a step, and open ended" {
                divided.Value |> OrderScenario.isPreparable |> Expect.isTrue "preparable"
                undivided.Value |> OrderScenario.hasStep |> Expect.isFalse "no step"
                undivided.Value |> OrderScenario.isBounded |> Expect.isTrue "bounded"
                openEnded.Value |> OrderScenario.hasStep |> Expect.isTrue "a step"
                openEnded.Value |> OrderScenario.isBounded |> Expect.isFalse "no limit"
            }

            test "a preparable order has its preparation and gets no line" {
                let sc = scenario false 0 divided.Value
                sc.Preparation |> Expect.isNonEmpty "the preparation"
                sc |> prescribed |> Expect.all "no line" (fun b -> b <> warning && b <> note)
            }

            test "without a Divisible there is no preparation and a note" {
                let sc = scenario false 0 undivided.Value
                sc.Preparation |> Expect.isEmpty "none"
                sc |> prescribed |> Expect.contains "the note" note
                sc |> prescribed |> Expect.all "no warning" ((<>) warning)
            }

            test "with a Divisible and no limit there is no preparation and a warning" {
                let sc = scenario false 0 openEnded.Value
                sc.Preparation |> Expect.isEmpty "none"
                sc |> prescribed |> Expect.contains "the warning" warning
                sc |> prescribed |> Expect.all "no note" ((<>) note)
            }

            test "an order that fits only without the Divisible has the warning and not the note" {
                let sc = scenario true 0 undivided.Value
                sc |> prescribed |> Expect.contains "the warning" warning
                sc |> prescribed |> Expect.all "no note" ((<>) note)
            }

            test "a preparable order that fits only without the Divisible keeps its preparation and has the warning" {
                let sc = scenario true 0 divided.Value
                sc.Preparation |> Expect.isNonEmpty "the preparation"
                sc |> prescribed |> Expect.contains "the warning" warning
            }

            test "the warning names the pharmacy and a second validation" {
                OrderScenario.notPreparableWarning
                |> Expect.stringContains "pharmacy" "apotheek"
                OrderScenario.notPreparableWarning
                |> Expect.stringContains "second" "tweede validatie"
            }

            test "printing again after a change gives the line once" {
                scenario true 0 undivided.Value
                |> OrderScenario.setOrderTableFormat
                |> prescribed
                |> Array.filter ((=) warning)
                |> Array.length
                |> Expect.equal "once" 1
            }

            testList
                "the filter"
                [
                    test "a scenario that is not preparable is dropped beside one that is" {
                        [| scenario false 0 undivided.Value; scenario false 1 divided.Value |]
                        |> OrderContext.filterScenariosByPreparation
                        |> Array.map _.No
                        |> Expect.equal "dropped" [| 1 |]
                    }

                    test "a scenario that is not preparable is dropped beside two that are, and both stay" {
                        [|
                            scenario false 0 undivided.Value
                            scenario false 1 divided.Value
                            scenario false 2 divided.Value
                        |]
                        |> OrderContext.filterScenariosByPreparation
                        |> Array.map _.No
                        |> Expect.equal "the two preparable" [| 1; 2 |]
                    }

                    test "the warning does not make a scenario preparable" {
                        [| scenario true 0 undivided.Value; scenario false 1 divided.Value |]
                        |> OrderContext.filterScenariosByPreparation
                        |> Array.map _.No
                        |> Expect.equal "dropped" [| 1 |]
                    }

                    test "the text of the preparation is not read" {
                        let sc = { scenario false 1 divided.Value with Preparation = [||] }

                        [| scenario false 0 undivided.Value; sc |]
                        |> OrderContext.filterScenariosByPreparation
                        |> Array.map _.No
                        |> Expect.equal "the preparable order stays without text" [| 1 |]
                    }

                    test "a dose type is filtered on its own" {
                        let other =
                            { scenario false 1 undivided.Value with
                                DoseType = Informedica.GenForm.Lib.Types.Once "eenmalig"
                            }

                        [| scenario false 0 divided.Value; other |]
                        |> OrderContext.filterScenariosByPreparation
                        |> Array.length
                        |> Expect.equal "both" 2
                    }

                    test "scenarios that are not preparable all stay" {
                        [| scenario false 0 undivided.Value; scenario false 1 openEnded.Value |]
                        |> OrderContext.filterScenariosByPreparation
                        |> Array.length
                        |> Expect.equal "both" 2
                    }

                ]

            test "the form of a dropped scenario can be picked through the evaluation" {
                let provider = OrderPlanTests.RulesOf twoForms :> Resources.IResourceProvider
                let evaluate cmd =
                    cmd
                    |> OrderContext.evaluateOutcome Scenarios.testStart OrderLogging.noOp provider

                match
                    child
                    |> OrderContext.create OrderLogging.noOp provider
                    |> OrderContext.UpdateOrderContext
                    |> evaluate
                with
                | Ok(Evaluated(OrderContext.UpdateOrderContext ctx)) ->
                    ctx.Scenarios
                    |> Array.map _.Form
                    |> Expect.equal "the preparable scenario is left" [| "zetpil" |]
                    ctx.Filter.Form |> Expect.equal "its form is picked" (Some "zetpil")
                    ctx.Filter.Forms
                    |> Array.sort
                    |> Expect.equal "the other form is offered" [| "drank"; "zetpil" |]

                    let n = ctx.Filter.Forms |> Array.findIndex ((=) "drank")

                    match
                        OrderContext.ChangeFilter(ctx, OrderCategory.Drug, FilterField.Form, Some n)
                        |> evaluate
                    with
                    | Ok(Evaluated cmd) ->
                        let ctx = cmd |> OrderContext.Command.get
                        ctx.Filter.Form |> Expect.equal "the picked form" (Some "drank")

                        let sc = ctx.Scenarios |> Array.exactlyOne
                        sc.Form |> Expect.equal "the dropped scenario is back" "drank"
                        sc.Preparation |> Expect.isEmpty "without a preparation"
                        sc |> prescribed |> Expect.contains "with the note" note
                    | r -> failtestf "no scenario after the pick: %A" r
                | r -> failtestf "no scenario: %A" r
            }

            testList
                "the mark of an order that fits only without the Divisible"
                [
                    test "the retry of the evaluation marks the order and warns" {
                        match withoutDivisibleContext.Value with
                        | Ok(OrderContext.Command.UpdateOrderContext ctx) ->
                            let sc = ctx.Scenarios |> Array.exactlyOne
                            sc.FitsOnlyWithoutDivisible |> Expect.isTrue "marked"
                            sc |> prescribed |> Expect.contains "the warning" warning
                        | r -> failtestf "no scenario: %A" r
                    }

                    test "an order on the first evaluation is not marked" {
                        let provider =
                            OrderPlanTests.RulesOf(
                                OrderPlanTests.RuleFixtures.rules
                                    [| OrderPlanTests.RuleFixtures.row Resources.Departments.defaultDepartment |]
                            )
                            :> Resources.IResourceProvider

                        match
                            child
                            |> OrderContext.create OrderLogging.noOp provider
                            |> OrderContext.Command.UpdateOrderContext
                            |> OrderContext.evaluate Scenarios.testStart OrderLogging.noOp provider
                        with
                        | Ok(OrderContext.Command.UpdateOrderContext ctx) ->
                            ctx.Scenarios
                            |> Array.map _.FitsOnlyWithoutDivisible
                            |> Expect.all "not marked" not
                        | r -> failtestf "no scenario: %A" r
                    }

                    test "the mark survives a change of the order" {
                        let ctx = markedContext

                        match ctx |> OrderContext.processScenarioOrder OrderLogging.noOp Ok with
                        | Evaluated ctx ->
                            ctx.Scenarios
                            |> Array.map _.FitsOnlyWithoutDivisible
                            |> Expect.equal "marked" [| true |]
                        | Refused _ -> failtest "refused"
                    }

                    test "the mark survives the Dto of a context, which a signed version stores" {
                        let ctx = markedContext

                        match ctx |> OrderContext.Dto.toDto |> OrderContext.Dto.fromDto with
                        | Ok ctx ->
                            ctx.Scenarios
                            |> Array.map _.FitsOnlyWithoutDivisible
                            |> Expect.equal "marked" [| true |]
                        | Error e -> failtestf "no round trip: %A" e
                    }

                    test "the mark stays in the plan and in the Dto a signed version stores" {
                        let workbench = markedContext |> PlanContext.create "" OrderCategory.Drug

                        let added =
                            OrderPlan.create child [||]
                            |> OrderPlan.addOrderContext (fun () -> "c-new") workbench
                            |> Result.defaultWith (fun e -> failtest $"{e}")
                            |> _.Contexts[0]

                        added.Context.Scenarios
                        |> Array.map _.FitsOnlyWithoutDivisible
                        |> Expect.equal "in the plan" [| true |]

                        match added |> PlanContext.Dto.toDto |> PlanContext.Dto.fromDto with
                        | Ok pc ->
                            pc.Context.Scenarios
                            |> Array.map _.FitsOnlyWithoutDivisible
                            |> Expect.equal "in the Dto" [| true |]
                        | Error e -> failtest $"the round trip failed: %A{e}"
                    }

                    test "a scenario stored before the mark existed is not marked" {
                        let dto =
                            OrderPlanTests.EvaluateFixtures.pcmContext.Scenarios[0]
                            |> OrderScenario.Dto.toDto

                        dto
                        |> Canonical.serialize
                        |> fun s ->
                            s
                                .Replace("\"FitsOnlyWithoutDivisible\":false,", "")
                                .Replace("\"fitsOnlyWithoutDivisible\":false,", "")
                        |> fun s ->
                            s.Contains "itsOnlyWithoutDivisible" |> Expect.isFalse "the field is out"
                            s |> Canonical.deserialize<OrderScenario.Dto.Dto>
                        |> OrderScenario.Dto.fromDto
                        |> function
                            | Ok sc -> sc.FitsOnlyWithoutDivisible |> Expect.isFalse "not marked"
                            | Error e -> failtestf "no scenario: %A" e
                    }
                ]
        ]
