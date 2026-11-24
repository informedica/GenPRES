/// The standard orders of the emergency list: the choice of the next options to evaluate, the evaluation with its
/// warnings, the texts and the advice of a standard order, the context narrowed to its scenario and the standard
/// orders of the rule sets.
module StandardOrderTests

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

module Feeding = OrderContextRuleTests.FeedingFixtures


/// Fixtures for the tests: a context for the patient of the feeding fixtures, a scenario of a paracetamol
/// suppository with a dose quantity of the item, rule sets, dose limits and a once timed order.
module Fixtures =

    let context filter =
        {
            Filter = filter
            PatientData = Feeding.patient
            Scenarios = [||]
            Argumentation = None
        }


    let rule = OrderPlanTests.EvaluateFixtures.ruleSetRow "EmergencyList" "Noodlijst"


    let ruleSet kind rules =
        {
            Kind = kind
            Label = ""
            Rows = rules
        }


    let emergencyList rules =
        [| ruleSet RuleSetKind.EmergencyList rules |]
        |> StandardOrder.emergencyList System.DateTime.UtcNow OrderLogging.noOp Feeding.provider Feeding.patient


    /// The context and the scenario of paracetamol for fever that the feeding fixtures evaluate to, with a dose
    /// rule of at most 100 mg.
    let found =
        lazy
            (match emergencyList [| rule "koorts" "paracetamol" |] with
             | [| so |], _ -> so.Context, so.Context.Scenarios[0]
             | other -> invalidOp $"fixture scenario could not be created: %A{other}")


    /// The scenario of paracetamol for fever, with an item dose quantity of 0 to 100 mg and no other values.
    let scenario = lazy (snd found.Value)


    /// The prescription rules of the scenario of paracetamol for fever, with the dose rule changed by f.
    let rulesWith f =
        let ctx, _ = found.Value

        match ctx |> OrderContext.rulesFor OrderLogging.noOp Feeding.provider ctx.PatientData with
        | Ok rules -> rules |> Array.map (fun pr -> { pr with DoseRule = pr.DoseRule |> f })
        | Error errs -> invalidOp $"fixture rules could not be found: %A{errs}"


    /// The prescription rules of the scenario of paracetamol for fever, with each component limit changed by f and
    /// the form limit set to formLimit.
    let rulesWithLimits formLimit f =
        rulesWith (fun dr ->
            { dr with
                ComponentLimits = dr.ComponentLimits |> Array.map f
                FormLimit = formLimit
            }
        )


    let mg = Units.Mass.milliGram
    let mL = Units.Volume.milliLiter
    let mgPerKg = Units.Mass.milliGram |> ValueUnit.per Units.Weight.kiloGram


    /// A range in the unit, with inclusive limits.
    let minMax unit min max : MinMax =
        let limit v = v |> ValueUnit.singleWithUnit unit |> Inclusive

        {
            Min = min |> Option.map limit
            Max = max |> Option.map limit
        }


    /// A dose limit with only a quantity per kg and a quantity.
    let limit qtyAdj qty =
        { DoseLimit.limit with
            QuantityAdjust = qtyAdj
            Quantity = qty
        }


    /// The once timed order of potassium chloride with its minimum and maximum values: a time of 60 to 120
    /// minutes and a rate of 16 to 32 mL/uur.
    let onceTimed =
        lazy
            (match
                Scenarios.kaliumchlorideOnceTimed ()
                |> CalcMinMax
                |> OrderProcessor.processPipeline OrderLogging.noOp
             with
             | Ok ord -> ord
             | Error _ -> invalidOp "fixture order could not be calculated")


    /// An evaluation that answers every plan context with the outcome.
    let answering outcome (_: PlanContext) = outcome


    /// An evaluation that evaluates every plan context to its own context with the scenarios.
    let evaluatingTo scenarios (pc: PlanContext) =
        Ok(Evaluated { pc with Context = { pc.Context with Scenarios = scenarios } })


    /// A provider whose every resource raises an exception.
    type Throwing() =
        inherit OrderPlanTests.NoRules()

        interface Resources.IResourceProvider with
            member _.Get(_: Resources.ResourceKey<'T>) : 'T = invalidOp "no resources"


open Fixtures


[<Tests>]
let tests =
    testList
        "StandardOrder"
        [
            testList
                "branches"
                [
                    let withFilter f = context f

                    test "chooses each generic first" {
                        { OrderContext.emptyFilter with
                            Generics = [| "a"; "b" |]
                            Routes = [| "r1"; "r2" |]
                        }
                        |> withFilter
                        |> StandardOrder.branches
                        |> Array.map _.Filter.Generic
                        |> Expect.equal "each generic" [| Some "a"; Some "b" |]
                    }

                    test "chooses each route when the generic is chosen" {
                        { OrderContext.emptyFilter with
                            Generic = Some "a"
                            Routes = [| "r1"; "r2" |]
                        }
                        |> withFilter
                        |> StandardOrder.branches
                        |> Array.map _.Filter.Route
                        |> Expect.equal "each route" [| Some "r1"; Some "r2" |]
                    }

                    test "chooses each dose type when the generic and the route are chosen" {
                        let dts = [| DoseType.Once ""; DoseType.Discontinuous "" |]

                        { OrderContext.emptyFilter with
                            Generic = Some "a"
                            Route = Some "r1"
                            DoseTypes = dts
                        }
                        |> withFilter
                        |> StandardOrder.branches
                        |> Array.map _.Filter.DoseType
                        |> Expect.equal "each dose type" (dts |> Array.map Some)
                    }

                    test "chooses each form when the generic, the route and the dose type are chosen" {
                        { OrderContext.emptyFilter with
                            Generic = Some "a"
                            Route = Some "r1"
                            DoseType = Some(DoseType.Once "")
                            Forms = [| "zetpil"; "tablet" |]
                        }
                        |> withFilter
                        |> StandardOrder.branches
                        |> Array.map _.Filter.Form
                        |> Expect.equal "each form" [| Some "zetpil"; Some "tablet" |]
                    }

                    test "is empty when the list of generics has one option" {
                        { OrderContext.emptyFilter with Generics = [| "a" |] }
                        |> withFilter
                        |> StandardOrder.branches
                        |> Expect.isEmpty "no choice open"
                    }

                    test "is empty when the list of routes has one option" {
                        { OrderContext.emptyFilter with
                            Generic = Some "a"
                            Routes = [| "r1" |]
                        }
                        |> withFilter
                        |> StandardOrder.branches
                        |> Expect.isEmpty "no choice open"
                    }
                ]

            testList
                "scenarios"
                [
                    let ctx =
                        { OrderContext.emptyFilter with
                            Indication = Some "koorts"
                            Generic = Some "paracetamol"
                        }
                        |> context

                    test "returns the context with each of its scenarios" {
                        ctx
                        |> StandardOrder.scenarios (evaluatingTo [| scenario.Value |]) 0
                        |> fun (found, warnings) -> found |> Array.map snd, warnings
                        |> Expect.equal "the scenario, no warning" ([| scenario.Value |], [||])
                    }

                    test "warns when no choice is open" {
                        ctx
                        |> StandardOrder.scenarios (evaluatingTo [||]) 0
                        |> snd
                        |> Expect.equal "no scenarios" [| StandardOrderWarning.NoScenarios("koorts", "paracetamol") |]
                    }

                    test "evaluates each option of an open choice, at most four choices deep" {
                        let mutable rounds = 0

                        let evaluate (pc: PlanContext) =
                            rounds <- rounds + 1

                            Ok(
                                Evaluated
                                    { pc with
                                        Context =
                                            { pc.Context with
                                                Filter =
                                                    { pc.Context.Filter with
                                                        Generic = None
                                                        Generics = [| "a"; "b" |]
                                                    }
                                            }
                                    }
                            )

                        let found, warnings = ctx |> StandardOrder.scenarios evaluate 0

                        found |> Expect.isEmpty "no scenarios"
                        // one evaluation at each depth: 1 + 2 + 4 + 8 + 16
                        rounds |> Expect.equal "31 evaluations" 31

                        warnings
                        |> Array.distinct
                        |> Expect.equal "too many choices" [| StandardOrderWarning.TooManyChoices("koorts", "") |]
                    }

                    test "warns when the context is refused" {
                        let refused (pc: PlanContext) = Ok(Refused(pc, Refusal.NoDoseRules))

                        ctx
                        |> StandardOrder.scenarios refused 0
                        |> snd
                        |> Expect.equal
                            "refused"
                            [| StandardOrderWarning.Refused("koorts", "paracetamol", Refusal.NoDoseRules) |]
                    }

                    test "warns when the evaluation fails" {
                        ctx
                        |> StandardOrder.scenarios (answering (Error "failed")) 0
                        |> snd
                        |> Expect.equal "failed" [| StandardOrderWarning.Failed("koorts", "paracetamol", "failed") |]
                    }
                ]

            testList
                "evaluate"
                [
                    test "returns the message of an exception as an error" {
                        context OrderContext.emptyFilter
                        |> PlanContext.create "" OrderCategory.Medication
                        |> StandardOrder.evaluate System.DateTime.UtcNow OrderLogging.noOp (Throwing())
                        |> Expect.equal "the message" (Error "no resources")
                    }
                ]

            testList
                "categoryOf"
                [
                    test "leaves out the prefix" {
                        "Noodlijst Reanimatie"
                        |> StandardOrder.categoryOf "Noodlijst "
                        |> Expect.equal "without prefix" "Reanimatie"
                    }

                    test "keeps an indication without the prefix" {
                        "koorts"
                        |> StandardOrder.categoryOf "Noodlijst "
                        |> Expect.equal "as it is" "koorts"
                    }
                ]

            testList
                "withAdjust"
                [
                    test "adds the quantity per kg in brackets when the order is per kg" {
                        StandardOrder.withAdjust true "10 mg" "1 mg/kg"
                        |> Expect.equal "with brackets" "10 mg (1 mg/kg)"
                    }

                    test "shows the quantity alone when the order is not per kg" {
                        StandardOrder.withAdjust false "10 mg" "1 mg/kg" |> Expect.equal "alone" "10 mg"
                    }

                    test "shows the quantity alone when the quantity per kg has no text" {
                        StandardOrder.withAdjust true "10 mg" "" |> Expect.equal "alone" "10 mg"
                    }
                ]

            testList
                "narrowed"
                [
                    let ctx = lazy (StandardOrder.narrowed (context OrderContext.emptyFilter) scenario.Value)

                    test "takes the form of the scenario" {
                        ctx.Value.Filter.Form |> Expect.equal "the form" (Some scenario.Value.Form)
                    }

                    test "keeps only the scenario" {
                        ctx.Value.Scenarios |> Expect.equal "one scenario" [| scenario.Value |]
                    }
                ]

            testList
                "ofScenario"
                [
                    let withGeneric = { OrderContext.emptyFilter with Generic = Some "paracetamol 500" } |> context

                    let reanimation = lazy { scenario.Value with Indication = "Noodlijst Reanimatie" }
                    let withItem = lazy (StandardOrder.ofScenario "max 100 mg" (withGeneric, reanimation.Value))

                    let withoutItem =
                        lazy
                            (StandardOrder.ofScenario
                                ""
                                (context OrderContext.emptyFilter, { reanimation.Value with Item = None }))

                    test "takes the category from the indication without Noodlijst" {
                        withItem.Value.Category |> Expect.equal "the category" "Reanimatie"
                    }

                    test "takes the generic from the filter" {
                        withItem.Value.Generic |> Expect.equal "the generic" "paracetamol 500"
                    }

                    test "takes the name of the scenario without a generic in the filter" {
                        withoutItem.Value.Generic |> Expect.equal "the name" reanimation.Value.Name
                    }

                    test "carries the context narrowed to the scenario" {
                        withItem.Value.Context
                        |> Expect.equal "narrowed" (StandardOrder.narrowed withGeneric reanimation.Value)
                    }

                    test "shows the dose of the item as plain text" {
                        withItem.Value.SubstanceDose |> Expect.equal "the item dose" "0 - 100 mg"
                    }

                    test "leaves the preparation empty when the orderable has no dose" {
                        withItem.Value.Preparation |> Expect.equal "no preparation" ""
                    }

                    test "shows the dose of the orderable with the name of the scenario for a scenario without item" {
                        (withoutItem.Value.SubstanceDose, withoutItem.Value.Preparation)
                        |> Expect.equal "the orderable" ("", $" %s{reanimation.Value.Name}")
                    }

                    test "takes the advice" { withItem.Value.Advice |> Expect.equal "the advice" "max 100 mg" }
                ]

            testList
                "itemOf"
                [
                    test "returns the item the scenario names" {
                        scenario.Value
                        |> StandardOrder.itemOf
                        |> Option.map (_.Name >> Name.toString)
                        |> Expect.equal "the item" (Some "paracetamol")
                    }

                    test "returns None for a scenario dosed by volume" {
                        { scenario.Value with Item = None }
                        |> StandardOrder.itemOf
                        |> Expect.isNone "no item"
                    }
                ]

            testList
                "limitText"
                [
                    test "shows the absolute limits when there are no limits per kg" {
                        limit MinMax.empty (minMax mg None (Some 100N))
                        |> StandardOrder.limitText
                        |> Expect.equal "the absolute limit" "max 100 mg"
                    }

                    test "shows the limits per kg followed by the absolute limits in brackets" {
                        limit (minMax mgPerKg (Some 1N) (Some 2N)) (minMax mg None (Some 100N))
                        |> StandardOrder.limitText
                        |> Expect.equal "per kg with absolute" "1 - 2 mg/kg (max 100 mg)"
                    }

                    test "shows a limit with an equal minimum and maximum as one value" {
                        limit MinMax.empty (minMax mg (Some 10N) (Some 10N))
                        |> StandardOrder.limitText
                        |> Expect.equal "the norm" "10 mg"
                    }

                    test "is empty without limits" {
                        limit MinMax.empty MinMax.empty
                        |> StandardOrder.limitText
                        |> Expect.equal "empty" ""
                    }
                ]

            testList
                "limitOf"
                [
                    test "takes the limit of the substance in the unit the substance is dosed in" {
                        rulesWith id
                        |> StandardOrder.limitOf scenario.Value
                        |> Option.map StandardOrder.limitText
                        |> Expect.equal "the limit in mg" (Some "max 100 mg")
                    }

                    test "takes no limit of the substance in another unit" {
                        let inMmol (dl: DoseLimit) = { dl with DoseUnit = Units.Molar.milliMole }

                        rulesWithLimits
                            None
                            (fun cl -> { cl with SubstanceLimits = cl.SubstanceLimits |> Array.map inMmol })
                        |> StandardOrder.limitOf scenario.Value
                        |> Expect.isNone "no limit"
                    }

                    test "takes the limit of the component for a scenario dosed by volume" {
                        rulesWithLimits
                            (Some(limit MinMax.empty (minMax mL None (Some 9N))))
                            (fun cl -> { cl with Limit = Some(limit MinMax.empty (minMax mL None (Some 5N))) })
                        |> StandardOrder.limitOf { scenario.Value with Item = None }
                        |> Option.map StandardOrder.limitText
                        |> Expect.equal "the component limit" (Some "max 5 mL")
                    }

                    test "takes the limit of the form for a scenario dosed by volume without a component limit" {
                        rulesWithLimits
                            (Some(limit MinMax.empty (minMax mL None (Some 9N))))
                            (fun cl -> { cl with Limit = None })
                        |> StandardOrder.limitOf { scenario.Value with Item = None }
                        |> Option.map StandardOrder.limitText
                        |> Expect.equal "the form limit" (Some "max 9 mL")
                    }
                ]

            testList
                "withTimeAndRate"
                [
                    test "adds the time and the rate of a once timed order" {
                        onceTimed.Value
                        |> StandardOrder.withTimeAndRate "max 2 mmol/kg"
                        |> Expect.equal "with time and rate" "max 2 mmol/kg in 60 - 120 min, 16 - 32 mL/uur"
                    }

                    test "shows the time and the rate alone without limits" {
                        onceTimed.Value
                        |> StandardOrder.withTimeAndRate ""
                        |> Expect.equal "time and rate" "in 60 - 120 min, 16 - 32 mL/uur"
                    }

                    test "keeps the limits for an order that is not once timed" {
                        scenario.Value.Order
                        |> StandardOrder.withTimeAndRate "max 100 mg"
                        |> Expect.equal "the limits" "max 100 mg"
                    }
                ]

            testList
                "advice"
                [
                    test "shows the limit of the dose rule of the scenario" {
                        found.Value
                        |> StandardOrder.advice OrderLogging.noOp Feeding.provider
                        |> Expect.equal "the limit" "max 100 mg"
                    }

                    test "gives no limit for a scenario of another route" {
                        let ctx, sc = found.Value

                        (ctx, { sc with Route = "ORAAL" })
                        |> StandardOrder.advice OrderLogging.noOp Feeding.provider
                        |> Expect.equal "no limit" ""
                    }
                ]

            testList
                "contextOf"
                [
                    test "takes the indication and the generic of the rule" {
                        rule "koorts" "paracetamol"
                        |> StandardOrder.contextOf Feeding.patient
                        |> fun ctx -> ctx.Filter.Indication, ctx.Filter.Generic
                        |> Expect.equal "indication and generic" (Some "koorts", Some "paracetamol")
                    }

                    test "leaves the generic open for a rule without a generic" {
                        rule "koorts" ""
                        |> StandardOrder.contextOf Feeding.patient
                        |> _.Filter.Generic
                        |> Expect.isNone "no generic"
                    }
                ]

            testList
                "emergencyList"
                [
                    test "returns a standard order for each scenario of the rules of the emergency list" {
                        emergencyList [| rule "koorts" "paracetamol" |]
                        |> fun (orders, warnings) -> orders |> Array.map _.Generic, warnings
                        |> Expect.equal "one standard order" ([| "paracetamol" |], [||])
                    }

                    test "gives each standard order its advice" {
                        emergencyList [| rule "koorts" "paracetamol" |]
                        |> fun (orders, warnings) -> orders |> Array.map _.Advice, warnings
                        |> Expect.equal "the advice" ([| "max 100 mg" |], [||])
                    }

                    test "takes only the rule sets of the kind EmergencyList" {
                        [| ruleSet RuleSetKind.StandardInfusions [| rule "koorts" "paracetamol" |] |]
                        |> StandardOrder.emergencyList
                            System.DateTime.UtcNow
                            OrderLogging.noOp
                            Feeding.provider
                            Feeding.patient
                        |> Expect.equal "nothing" ([||], [||])
                    }

                    test "returns a warning for a rule the rules refuse" {
                        emergencyList [| rule "onbekend" "paracetamol" |]
                        |> snd
                        |> Expect.equal
                            "refused"
                            [|
                                StandardOrderWarning.Refused("onbekend", "paracetamol", Refusal.NoDoseRules)
                            |]
                    }
                ]
        ]
