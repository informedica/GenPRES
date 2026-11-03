/// The order plan on new patient data: every order on the current patient, a context without an
/// order takes the patient, and the limits of a per kg solution rule follow the weight.
module PlanOnDataTests

open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip

open DataUpdateFixtures


module Fixture =

    open OrderPlanTests

    let provider () =
        let row = RuleFixtures.row Resources.Departments.defaultDepartment
        RulesOf(RuleFixtures.rules [| row |]) :> Resources.IResourceProvider

    let context provider pat =
        { OrderContext.create logger provider pat with Scenarios = [| scenario order.Value |] }

    let plan provider pat =
        OrderPlan.create pat [| PlanContext.create "c1" OrderCategory.Drug (context provider pat) |]

    /// A context without an order starts again on the patient, with its filter cleared.
    let restart pat (pc: PlanContext) =
        Ok
            { pc with
                Context =
                    { pc.Context with
                        Patient = pat
                        Filter = OrderContext.emptyFilter
                    }
            }

    let followWith restart provider pat (plan: OrderPlan) =
        { plan with Patient = pat }
        |> OrderPlan.followPatientData logger provider restart

    let follow provider pat plan = plan |> followWith restart provider pat

    let followed provider pat plan = plan |> follow provider pat

    let ml v = v |> ValueUnit.singleWithUnit Units.Volume.milliLiter

    let limit v = Some(BigRational.FromInt v)

    /// At least 15 mg/kg with a maximum of 1000 mg.
    let perKgWithMaximum () =
        let row =
            RuleFixtures.rowFor
                "koorts"
                "kg"
                (fun dl ->
                    { dl with
                        MaxQty = limit 1000
                        MinQtyAdj = limit 15
                    }
                )
                id

        RulesOf(RuleFixtures.rules [| row |]) :> Resources.IResourceProvider

    let adult w = RuleFixtures.patient (Some 12N) false (Some w) None

    /// A patient of 40 years, for whom the rules are looked up without a weight.
    let grownUp w = RuleFixtures.patient (Some 40N) false (Some w) None

    let rulesAt p w =
        OrderContext.create logger p (adult w)
        |> OrderContext.rulesFor logger p (adult w)
        |> Result.defaultWith (fun e -> failtest $"no rules: %A{e}")

    /// The context of the workbench at the weight, with the single scenario of the rule.
    let contextAt p w =
        match
            OrderContext.create logger p (adult w)
            |> OrderContext.getScenarios Scenarios.testStart logger p
        with
        | Ok(Evaluated ctx) when ctx.Scenarios.Length = 1 -> ctx
        | _ -> failtest "no single scenario"

    /// The order built with the id of ord from the rule at the weight.
    let built p w (ord: Order) =
        let (Id id) = ord.Id

        rulesAt p w
        |> OrderContext.orderOfRule Scenarios.testStart logger id
        |> Option.defaultWith (fun () -> failtest "no order")

    let limits = Order.definedLimits

    /// The order with two doses from one preparation.
    let twoDoses (ord: Order) =
        { ord with
            Order.Orderable.DoseCount =
                ord.Orderable.DoseCount
                |> OrderVariable.Count.setToNonZeroPositive
                |> OrderVariable.Count.toOrdVar
                |> OrderVariable.tryConstrain (2N |> ValueUnit.singleWithUnit Units.Count.times)
                |> Option.get
                |> Count
        }

    /// The order with a dose quantity that does not fit its other values, so it cannot be solved.
    let unsolvable (ord: Order) =
        { ord with
            Order.Orderable.Dose.Quantity =
                ord.Orderable.Dose.Quantity
                |> OrderVariable.Quantity.toOrdVar
                |> OrderVariable.setToNonZeroPositive
                |> OrderVariable.tryConstrain (ml 3333N)
                |> Option.get
                |> Quantity
        }


[<Tests>]
let tests =
    testList
        "the order plan on new patient data"
        [
            test "the plan takes the patient and the order follows, the context keeps its patient" {
                let p = Fixture.provider ()
                let start = patient 10N
                let after = Fixture.plan p start |> Fixture.followed p (patient 20N)

                after.Patient.Weight |> Expect.equal "the plan" (Some(kg 20N))
                after.Contexts[0].Context.Patient |> Expect.equal "the context" start

                after.Contexts[0].Context.Scenarios[0].Order
                |> Order.adjustValue
                |> Expect.equal "the order" (Some(kg 20N))
            }

            test "a step after new patient data lands on the order, with its adjust value and its notice" {
                let p = Fixture.provider ()
                let start = patient 10N
                let sc = Scenarios.pcmDrink |> Tests.TestOrder.processed |> scenario

                let context =
                    { Fixture.context p start with Scenarios = [| sc |] }
                    |> PlanContext.create "c1" OrderCategory.Drug

                let after = OrderPlan.create start [| context |] |> Fixture.followed p (patient 20N)
                let followed = after.Contexts[0].Context
                let marked = { followed.Scenarios[0] with RulesNoLongerApply = true }
                let doseQuantity (o: Order) =
                    o.Orderable.Dose.Quantity
                    |> OrderVariable.Quantity.toOrdVar
                    |> Tests.TestOrder.valuesOf

                let stepped =
                    match
                        { followed with Scenarios = [| marked |] }
                        |> OrderContext.SetMaxOrderableDoseQuantityProperty
                        |> OrderContext.evaluateOutcome Scenarios.testStart logger p
                    with
                    | Ok(Evaluated command) -> (command |> OrderContext.Command.get).Scenarios[0]
                    | other -> failtest $"the step did not land: %A{other}"

                stepped.Order
                |> Order.adjustValue
                |> Expect.equal "the current patient" (Some(kg 20N))

                stepped.Order
                |> doseQuantity
                |> Expect.notEqual "the dose moved" (marked.Order |> doseQuantity)

                stepped.Order |> doseQuantity |> Expect.hasLength "one value" 1
                stepped.RulesNoLongerApply |> Expect.isTrue "the notice stays"
            }

            test "a context without an order takes the patient" {
                let p = Fixture.provider ()
                let start = patient 10N
                let empty = PlanContext.create "c2" OrderCategory.Drug (OrderContext.create logger p start)
                let after = OrderPlan.create start [| empty |] |> Fixture.followed p (patient 20N)

                after.Contexts[0].Context.Patient.Weight |> Expect.equal "new" (Some(kg 20N))
            }

            test "a corrected weight gives the context of before, so nothing is changed" {
                let p = Fixture.provider ()
                let start = patient 10N
                // once through, so the fixture has its text and filter, as an order from the workbench has
                let plan = Fixture.plan p start |> Fixture.followed p start
                let back = plan |> Fixture.followed p (patient 20N) |> Fixture.followed p start

                back.Contexts[0].Context
                |> Expect.equal "the context as it was" plan.Contexts[0].Context
            }

            test "a real order at another weight and back is the same order, values and units included" {
                let p = Fixture.provider ()
                let start = patient 10N
                let sc = Scenarios.pcmDrink |> Tests.TestOrder.processed |> scenario

                let context =
                    { Fixture.context p start with Scenarios = [| sc |] }
                    |> PlanContext.create "c1" OrderCategory.Drug

                let orderOf (plan: OrderPlan) = plan.Contexts[0].Context.Scenarios[0].Order

                // the order on the weight of the plan, as it is when it is signed
                let signed = OrderPlan.create start [| context |] |> Fixture.followed p start
                let moved = signed |> Fixture.followed p (patient 20N)
                let back = moved |> Fixture.followed p start

                moved
                |> orderOf
                |> Expect.notEqual "the order follows the weight" (signed |> orderOf)
                back |> orderOf |> Expect.equal "the order as it was" (signed |> orderOf)
            }

            test "an order from the workbench at another weight and back is the same order" {
                let p = Fixture.provider ()
                let start = Fixture.adult 40N
                let ctx = Fixture.contextAt p 40N
                let signed = OrderPlan.create start [| PlanContext.create "c1" OrderCategory.Drug ctx |]
                let orderOf (plan: OrderPlan) = plan.Contexts[0].Context.Scenarios[0].Order

                let moved = signed |> Fixture.followed p (Fixture.adult 60N)
                let back = moved |> Fixture.followed p start

                moved
                |> orderOf
                |> Expect.notEqual "the order follows the weight" (signed |> orderOf)
                back |> orderOf |> Expect.equal "the order as it was signed" (signed |> orderOf)
            }

            test "an order that cannot be calculated stands, marked, and the rest of the plan follows" {
                let p = Fixture.provider ()
                let start = patient 10N
                let ctx = { Fixture.context p start with Scenarios = [| scenario (Fixture.unsolvable order.Value) |] }

                let plan =
                    OrderPlan.create
                        start
                        [|
                            PlanContext.create "c1" OrderCategory.Drug ctx
                            PlanContext.create "c2" OrderCategory.Drug (Fixture.context p start)
                        |]

                let followed = plan |> Fixture.followed p (patient 20N)
                let scenarioOf i = followed.Contexts[i].Context.Scenarios[0]

                (scenarioOf 0).NotRecalculated |> Expect.isTrue "marked"

                (scenarioOf 0).Order
                |> Expect.equal "the order as it was" ctx.Scenarios[0].Order

                (scenarioOf 0).Prescription
                |> Array.concat
                |> Array.contains (OrderScenario.notRecalculatedNotice |> Warning)
                |> Expect.isTrue "the order says so"

                (scenarioOf 1).NotRecalculated |> Expect.isFalse "the other order followed"
            }

            test "an order that cannot be calculated stands, marked, and still gets the rules notice" {
                let p, ctx = RuleCheckTests.Fixture.start ()
                let sc = ctx.Scenarios[0]
                let refusing = { ctx with Scenarios = [| { sc with Order = Fixture.unsolvable sc.Order } |] }

                match
                    refusing
                    |> OrderContext.orderOnPatientData
                        RuleCheckTests.Fixture.logger
                        p
                        (RuleCheckTests.Fixture.patient (21N / 2N))
                        refusing.Scenarios[0]
                with
                | Refused(after, _) ->
                    let marked = after.Scenarios[0]

                    (marked.NotRecalculated, marked.RulesNoLongerApply)
                    |> Expect.equal "both" (true, true)

                    marked.Order |> Expect.equal "the order as it was" refusing.Scenarios[0].Order
                | other -> failtest $"expected a refusal, got %A{other}"
            }

            test "a mark stays while the patient lacks the value the order needs" {
                let p = Fixture.provider ()
                let start = patient 10N
                let ctx = Fixture.context p start

                let marked =
                    { ctx with Scenarios = ctx.Scenarios |> Array.map (fun sc -> { sc with NotRecalculated = true }) }

                let plan = OrderPlan.create start [| PlanContext.create "c1" OrderCategory.Drug marked |]
                let followed = plan |> Fixture.followed p { start with Weight = None }
                let scenario = followed.Contexts[0].Context.Scenarios[0]

                scenario.NotRecalculated |> Expect.isTrue "nothing was calculated"
                scenario.Order |> Expect.equal "the order as it was" ctx.Scenarios[0].Order
            }

            test "an adult per kg order with the weight cleared stands, with the rules notice" {
                let p = Fixture.perKgWithMaximum ()
                let adult = Fixture.grownUp 70N

                let ctx =
                    match
                        OrderContext.create logger p adult
                        |> OrderContext.getScenarios Scenarios.testStart logger p
                    with
                    | Ok(Evaluated ctx) when ctx.Scenarios.Length = 1 -> ctx
                    | other -> failtest $"no single scenario: %A{other}"

                let plan = OrderPlan.create adult [| PlanContext.create "c1" OrderCategory.Drug ctx |]

                OrderContext.selectsRules { adult with Weight = None }
                |> Expect.isTrue "the rules are looked up"

                let scenario =
                    (plan |> Fixture.followed p { adult with Weight = None }).Contexts[0].Context.Scenarios[0]

                scenario.Order |> Expect.equal "the order as it was" ctx.Scenarios[0].Order
                scenario.RulesNoLongerApply |> Expect.isTrue "the rules no longer apply"
            }

            test "a mark goes at the value of the order with the current rule known" {
                let p = Fixture.perKgWithMaximum ()
                let ctx = Fixture.contextAt p 40N

                let marked =
                    { ctx with Scenarios = ctx.Scenarios |> Array.map (fun sc -> { sc with NotRecalculated = true }) }

                let scenario = (marked |> onData p (Fixture.adult 40N)).Scenarios[0]

                scenario.NotRecalculated |> Expect.isFalse "the order stands on the patient"
                scenario.Order |> Expect.equal "the order as it was" ctx.Scenarios[0].Order
            }

            test "a mark stays at the value of the order when the current rule is not known" {
                let p, ctx = RuleCheckTests.Fixture.start ()
                let current = RuleCheckTests.Fixture.patient (19N / 2N)
                let onValue = ctx |> onData p current

                let marked =
                    { onValue with
                        Patient = RuleCheckTests.Fixture.patient (21N / 2N)
                        Scenarios = onValue.Scenarios |> Array.map (fun sc -> { sc with NotRecalculated = true })
                    }

                let scenario = (marked |> onData p current).Scenarios[0]

                (scenario.NotRecalculated, scenario.RulesNoLongerApply)
                |> Expect.equal "nothing was calculated, and other rules" (true, true)
            }

            test "a mark goes once the order can be calculated" {
                let p = Fixture.provider ()
                let start = patient 10N
                let ctx = Fixture.context p start

                let marked =
                    { ctx with Scenarios = ctx.Scenarios |> Array.map (fun sc -> { sc with NotRecalculated = true }) }

                let plan = OrderPlan.create start [| PlanContext.create "c1" OrderCategory.Drug marked |]

                let followed = plan |> Fixture.followed p (patient 20N)

                followed.Contexts[0].Context.Scenarios[0].NotRecalculated
                |> Expect.isFalse "calculated again"
            }

            test "a nutrition slot goes through restart" {
                let p = Fixture.provider ()
                let start = patient 10N
                let slot =
                    OrderContext.create logger p start
                    |> PlanContext.create "c2" (OrderCategory.Nutrition NutritionCategory.TPN)
                let after = OrderPlan.create start [| slot |] |> Fixture.followed p (patient 20N)

                after.Contexts[0].Context.Filter
                |> Expect.equal "filtered again" OrderContext.emptyFilter
            }

            test "a context that cannot start again takes the patient and does not block the update" {
                let p = Fixture.provider ()
                let start = patient 10N
                let slot = OrderContext.create logger p start |> PlanContext.create "c2" OrderCategory.Drug

                let after =
                    OrderPlan.create start [| slot |]
                    |> Fixture.followWith (fun _ _ -> Error "failed") p (patient 20N)

                after.Contexts[0].Context.Patient.Weight |> Expect.equal "new" (Some(kg 20N))
            }

            testList
                "the limits follow the current rule"
                [
                    test "an order built from its rule at the same weight has the same limits" {
                        let p = Fixture.perKgWithMaximum ()
                        let ord = (Fixture.contextAt p 40N).Scenarios[0].Order

                        ord
                        |> Order.withDefinedLimitsOf (ord |> Fixture.built p 40N)
                        |> Fixture.limits
                        |> Expect.equal "the same" (ord |> Fixture.limits)
                    }

                    test "a dose at its maximum takes the rewritten limits, with an alert and no notice" {
                        let p = Fixture.perKgWithMaximum ()
                        let ctx = Fixture.contextAt p 55N
                        let after = ctx |> onData p (Fixture.adult 70N)
                        let sc = after.Scenarios[0]
                        let dose = sc.Order.Orderable.Components[0].Items[0].Dose

                        sc.RulesNoLongerApply |> Expect.isFalse "the same rule"
                        (dose |> Order.Orderable.Dose.Dto.toDto).Quantity.Level
                        |> Expect.equal "alert" OrderVariable.Dto.IsAlert
                    }

                    test "limits at the old weight are restored with the weight unchanged" {
                        let p = Fixture.perKgWithMaximum ()
                        let ctx = Fixture.contextAt p 55N
                        let sc = ctx.Scenarios[0]
                        let stale =
                            { sc with Order = sc.Order |> Order.withDefinedLimitsOf (sc.Order |> Fixture.built p 70N) }
                        let after = { ctx with Scenarios = [| stale |] } |> onData p (Fixture.adult 55N)

                        after.Scenarios[0].Order
                        |> Fixture.limits
                        |> Expect.equal "the limits of the rule" (sc.Order |> Fixture.limits)
                    }

                    test "with more doses the whole preparation stays and the dose limits follow" {
                        let p = Fixture.perKgWithMaximum ()
                        let ord = (Fixture.contextAt p 55N).Scenarios[0].Order |> Fixture.twoDoses
                        let after = ord |> Order.withDefinedLimitsOf (ord |> Fixture.built p 70N)
                        let cmp (o: Order) = o.Orderable.Components[0]
                        let itm (o: Order) = (cmp o).Items[0]

                        let preparation (o: Order) =
                            [
                                o.Orderable.OrderableQuantity |> OrderVariable.Quantity.toOrdVar
                                (cmp o).OrderableQuantity |> OrderVariable.Quantity.toOrdVar
                                (cmp o).OrderableConcentration |> OrderVariable.Concentration.toOrdVar
                                (cmp o).OrderableCount |> OrderVariable.Count.toOrdVar
                                (itm o).OrderableQuantity |> OrderVariable.Quantity.toOrdVar
                                (itm o).OrderableConcentration |> OrderVariable.Concentration.toOrdVar
                            ]
                            |> List.map _.DefinedConstraints

                        after |> preparation |> Expect.equal "the preparation" (ord |> preparation)
                        after |> Fixture.limits |> Expect.notEqual "the dose" (ord |> Fixture.limits)
                    }

                    test "limits only land on a variable of the same name" {
                        let p = Fixture.perKgWithMaximum ()
                        let ord = (Fixture.contextAt p 55N).Scenarios[0].Order

                        ord
                        |> Order.withDefinedLimitsOf order.Value
                        |> Fixture.limits
                        |> Expect.equal "another order changes nothing" (ord |> Fixture.limits)
                    }

                    test "a picked product gives the rule of its Divisible group, no pick gives no rule" {
                        let products =
                            [|
                                SplitByDivisibleTests.Fixture.product "1" 100N (Some 1N)
                                SplitByDivisibleTests.Fixture.product "2" 200N (Some 2N)
                            |]

                        let p = SplitByDivisibleTests.Fixture.provider products
                        let rules = [| SplitByDivisibleTests.Fixture.rule products |]
                        let sc = scenario order.Value
                        let ctx (picks: string[]) : OrderContext =
                            { Fixture.context p (Fixture.adult 40N) with
                                Filter = { OrderContext.emptyFilter with PickedProducts = picks }
                            }

                        rules
                        |> OrderContext.ruleOfScenario p sc (ctx [| "1" |])
                        |> Expect.hasLength "one group" 1
                        rules
                        |> OrderContext.ruleOfScenario p sc (ctx [||])
                        |> Expect.hasLength "both groups" 2
                    }

                    test "a real order with several products keeps its limits at the same weight" {
                        let products =
                            [|
                                SplitByDivisibleTests.Fixture.product "1" 100N (Some 1N)
                                SplitByDivisibleTests.Fixture.product "2" 200N (Some 1N)
                            |]

                        let p = SplitByDivisibleTests.Fixture.provider products
                        let rules = [| SplitByDivisibleTests.Fixture.rule products |]
                        let ctx = Fixture.contextAt p 40N
                        let sc = ctx.Scenarios[0]
                        let (Id id) = sc.Order.Id
                        let built =
                            rules
                            |> OrderContext.ruleOfScenario p sc ctx
                            |> OrderContext.orderOfRule Scenarios.testStart logger id
                            |> Option.defaultWith (fun () -> failtest "no order")

                        sc.Order
                        |> Order.withDefinedLimitsOf built
                        |> Fixture.limits
                        |> Expect.equal "the same" (sc.Order |> Fixture.limits)
                    }

                    test "a TPN whose limit falls below its amount gives an answer with an alert" {
                        let ord = Scenarios.tpn |> Tests.TestOrder.processed
                        let cmp = ord.Orderable.Components[0]
                        let half =
                            (ord |> Order.adjustValue |> Option.get)
                            / (2N |> ValueUnit.singleWithUnit Units.Count.times)
                        let low = 1N |> ValueUnit.singleWithUnit Units.Volume.milliLiter

                        let tighter =
                            { ord with
                                Order.Orderable.Components =
                                    { cmp with
                                        OrderableQuantity =
                                            cmp.OrderableQuantity
                                            |> OrderVariable.Quantity.apply (
                                                OrderVariable.mapConstraints (
                                                    OrderVariable.Constraints.setMinMax false (MinMax.createExact low)
                                                )
                                            )
                                    }
                                    :: ord.Orderable.Components.Tail
                            }

                        match Readjust(tighter, half) |> OrderProcessor.processPipeline logger with
                        | Ok after ->
                            (after.Orderable.Components[0].OrderableQuantity
                             |> OrderVariable.Quantity.isWithinConstraints false)
                            |> Expect.isFalse "outside its limit"
                        | Error(_, msgs) -> failtest $"refused: %A{msgs}"
                    }

                    test "several rule rows give no order to take limits from" {
                        let p = Fixture.perKgWithMaximum ()
                        let rules = Fixture.rulesAt p 40N

                        Array.append rules rules
                        |> OrderContext.orderOfRule Scenarios.testStart logger "o"
                        |> Expect.isNone "two rows"
                    }
                ]
        ]
