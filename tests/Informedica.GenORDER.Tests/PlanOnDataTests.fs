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

    let follow provider pat plan =
        { plan with Patient = pat } |> OrderPlan.followPatientData logger provider

    let followed provider pat plan =
        plan
        |> follow provider pat
        |> Result.defaultWith (fun e -> failtest $"refused: %A{e}")

    let ml v = v |> ValueUnit.singleWithUnit Units.Volume.milliLiter
    let mlPerKg v =
        v
        |> ValueUnit.singleWithUnit (Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram)

    /// A solution rule with a per kg limit of 5 mL/kg on the component with the name, 100 mL at the
    /// weight of the patient.
    let componentRule name =
        (UndilutedVancomycin() :> Resources.IResourceProvider).GetSolutionRules()
        |> Array.head
        |> fun sr ->
            { sr with
                SolutionLimits =
                    sr.SolutionLimits
                    |> Array.map (fun sl ->
                        { sl with
                            SolutionLimitTarget = ComponentLimitTarget name
                            QuantityAdj = MinMax.createExact (mlPerKg 5N)
                            Quantity = MinMax.createExact (ml 100N)
                        }
                    )
            }

    let definedMax (q: Quantity) =
        (q |> OrderVariable.Quantity.toOrdVar).DefinedConstraints.Max
        |> Option.map Informedica.GenSolver.Lib.Variable.ValueRange.Maximum.toValueUnit

    let firstComponent (ord: Order) = ord.Orderable.Components[0]

    let componentName ord = (ord |> firstComponent).Name |> WrappedString.Name.toString

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

            test "an order that cannot be calculated refuses the update, by the name of the order" {
                let p = Fixture.provider ()
                let start = patient 10N
                let ctx = { Fixture.context p start with Scenarios = [| scenario (Fixture.unsolvable order.Value) |] }
                let plan = OrderPlan.create start [| PlanContext.create "c1" OrderCategory.Drug ctx |]

                match plan |> Fixture.follow p (patient 20N) with
                | Error(OrderPlanError.NotCalculated name) -> name |> Expect.equal "the name" ctx.Scenarios[0].Name
                | other -> failtest $"not refused: %A{other}"
            }

            testList
                "the limits of a per kg solution rule"
                [
                    test "a component takes the amount of the rule" {
                        let ord = order.Value
                        let rule = ord |> Fixture.componentName |> Fixture.componentRule

                        ord
                        |> Medication.OrderBuilder.withSolutionAmounts rule
                        |> Fixture.firstComponent
                        |> _.OrderableQuantity
                        |> Fixture.definedMax
                        |> Expect.equal "100 mL" (Some(Fixture.ml 100N))
                    }

                    test "the volume of the preparation takes the amount of the rule" {
                        let ord = order.Value

                        let rule =
                            { Fixture.componentRule "another" with
                                VolumeAdjust = MinMax.createExact (Fixture.mlPerKg 5N)
                                Volume = MinMax.createExact (Fixture.ml 200N)
                            }

                        ord
                        |> Medication.OrderBuilder.withSolutionAmounts rule
                        |> _.Orderable.OrderableQuantity
                        |> Fixture.definedMax
                        |> Expect.equal "200 mL" (Some(Fixture.ml 200N))
                    }

                    test "a component without a per kg limit keeps its limit" {
                        let ord = order.Value
                        let rule = Fixture.componentRule "another"

                        ord
                        |> Medication.OrderBuilder.withSolutionAmounts rule
                        |> Fixture.firstComponent
                        |> _.OrderableQuantity
                        |> Fixture.definedMax
                        |> Expect.equal
                            "as it was"
                            ((ord |> Fixture.firstComponent).OrderableQuantity |> Fixture.definedMax)
                    }

                    test "several rule rows or solution rules give no rule to take an amount from" {
                        let p = Fixture.provider ()
                        let pat = patient 10N
                        let rule = Fixture.componentRule "x"

                        let rules =
                            Fixture.context p pat
                            |> OrderContext.rulesFor logger p pat
                            |> Result.defaultWith (fun e -> failtest $"no rules: %A{e}")

                        let solved n =
                            rules |> Array.map (fun r -> { r with SolutionRules = Array.create n rule })

                        solved 1 |> PrescriptionRule.singleSolutionRule |> Expect.isSome "one"
                        solved 2
                        |> PrescriptionRule.singleSolutionRule
                        |> Expect.isNone "two solution rules"

                        Array.append (solved 1) (solved 1)
                        |> PrescriptionRule.singleSolutionRule
                        |> Expect.isNone "two rule rows"
                    }
                ]
        ]
