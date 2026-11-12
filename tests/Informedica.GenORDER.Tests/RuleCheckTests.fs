/// The rule check on new patient data: the same picks looked up for the patient of the order
/// context and for the current patient; no rule, or other rules, gives the general notice.
module RuleCheckTests

open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib.Ranges
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module Fixture =

    open OrderPlanTests

    let logger = DataUpdateFixtures.logger
    let limit v = Some(BigRational.FromInt v)
    let counted = SolutionRuleComparison.Counted
    let ignored = SolutionRuleComparison.Ignored

    /// Rule A up to 10 kg and rule B from 10 kg, with another maximum dose.
    let rows maxB =
        let row max (cat: PatientCategoryData -> PatientCategoryData) =
            let r = RuleFixtures.row Resources.Departments.defaultDepartment

            { r with
                Patient = r.Patient |> cat
                ScheduleData =
                    { r.ScheduleData with DoseLimitData = { r.ScheduleData.DoseLimitData with MaxQty = limit max } }
            }

        [|
            row 100 (fun c -> { c with MaxWeight = Some(BigRational.FromInt 10000) })
            row maxB (fun c -> { c with MinWeight = Some(BigRational.FromInt 10000) })
        |]

    let provider maxB =
        RulesOf(rows maxB |> RuleFixtures.rules) :> Resources.IResourceProvider

    /// One rule of at least 15 mg/kg with a maximum of 1000 mg.
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

    let patient = DataUpdateFixtures.patient

    let order = DataUpdateFixtures.order

    let context provider pat =
        { OrderContext.create logger provider pat with Scenarios = [| DataUpdateFixtures.scenario order.Value |] }

    /// The context at 9.5 kg, with the provider of rule A and B.
    let start () =
        let p = provider 200
        p, context p (patient (19N / 2N))

    /// The rules of the context at 9.5 kg.
    let rules () =
        let p, ctx = start ()

        ctx
        |> OrderContext.rulesFor logger p ctx.Patient
        |> Result.defaultWith (fun msgs -> failtest $"no rules: %A{msgs}")

    /// A solution rule to compare with.
    let solutionRules () =
        (OrderPlanTests.UndilutedVancomycin() :> Resources.IResourceProvider).GetSolutionRules()

    let withSolutionRules rs =
        rs |> Array.map (fun r -> { r with SolutionRules = solutionRules () })

    let renalRule: RenalRule =
        {
            Generic = "paracetamol"
            Route = ""
            Indication = ""
            Source = "test"
            Age = MinMax.empty
            RenalFunction = EGFR(Some 10, Some 30)
            DoseType = NoDoseType
            Frequencies = None
            IntervalTime = MinMax.empty
            RenalLimits = [||]
        }

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

    /// Whether the rules no longer apply to the order for the current patient.
    let verdict provider current (ctx: OrderContext) =
        let rules pat = ctx |> OrderContext.rulesFor logger provider pat

        OrderContext.rulesVerdict logger order.Value (rules ctx.Patient) (rules current)

    let check provider start current =
        context provider (patient start) |> verdict provider (patient current)

    let onData = DataUpdateFixtures.onData


[<Tests>]
let tests =
    testList
        "the rule check on new patient data"
        [
            test "the rules are found for both sides of 10 kg" {
                let p, ctx = Fixture.start ()
                let rules w =
                    ctx |> OrderContext.rulesFor Fixture.logger p (Fixture.patient w)

                rules (19N / 2N) |> Result.map Array.length |> Expect.equal "rule A" (Ok 1)
                rules (21N / 2N) |> Result.map Array.length |> Expect.equal "rule B" (Ok 1)
            }

            test "a context gets the same rules from both lookups" {
                let p, ctx = Fixture.start ()
                let _, found = ctx |> OrderContext.getRules Fixture.logger p

                match found, ctx |> OrderContext.rulesFor Fixture.logger p ctx.Patient with
                | Ok a, Ok b -> PrescriptionRule.sameRules Fixture.counted a b |> Expect.isTrue "the same"
                | other -> failtest $"no rules: %A{other}"
            }

            test "the same rule at another weight gives no notice" {
                Fixture.check (Fixture.provider 200) (19N / 2N) 9N
                |> Expect.equal "rule A at both" (Some false)
            }

            test "9.5 to 10.5 kg crosses into rule B: notice" {
                Fixture.check (Fixture.provider 200) (19N / 2N) (21N / 2N)
                |> Expect.equal "notice" (Some true)
            }

            test "9.5 to 11 kg still has the notice, the context keeps rule A" {
                Fixture.check (Fixture.provider 200) (19N / 2N) 11N
                |> Expect.equal "notice" (Some true)
            }

            test "back to 9.5 kg clears the notice" {
                Fixture.check (Fixture.provider 200) (19N / 2N) (19N / 2N)
                |> Expect.equal "no notice" (Some false)
            }

            test "another rule row with the same limits also gives a notice" {
                Fixture.check (Fixture.provider 100) (19N / 2N) (21N / 2N)
                |> Expect.equal "notice" (Some true)
            }

            test "no rule for the current patient gives a notice" {
                let p, ctx = Fixture.start ()

                let other =
                    { Fixture.patient (19N / 2N) with
                        Weight = None
                        Age = None
                    }

                ctx |> Fixture.verdict p other |> Expect.equal "notice" (Some true)
            }

            test "a per kg rule with a maximum gives no notice while the maximum is not reached" {
                Fixture.check (Fixture.perKgWithMaximum ()) 40N 45N
                |> Expect.equal "no notice" (Some false)
            }

            test "a per kg rule rewritten at its maximum is the same rule: no notice" {
                Fixture.check (Fixture.perKgWithMaximum ()) 55N 70N
                |> Expect.equal "no notice" (Some false)
            }

            test "a lookup that fails gives no verdict, whichever lookup it is" {
                let failed = Error [ Message.Info "no rules loaded" ]
                let ok = Ok(Fixture.rules ())
                let verdict = OrderContext.rulesVerdict Fixture.logger Fixture.order.Value

                verdict failed ok |> Expect.isNone "at the context"
                verdict ok failed |> Expect.isNone "for the current patient"
            }

            test "a data update calculates the order and the context keeps its patient" {
                let p, ctx = Fixture.start ()
                let current = { ctx.Patient with Weight = Some(20N |> ValueUnit.singleWithUnit Units.Weight.kiloGram) }
                let after = ctx |> Fixture.onData p current

                after.Patient |> Expect.equal "the patient of the rules" ctx.Patient

                after.Scenarios[0].Order
                |> Order.adjustValue
                |> Option.map ValueUnit.getValue
                |> Expect.equal "the order follows" (current.Weight |> Option.map ValueUnit.getValue)
            }

            test "a data update puts the notice on the order and the text, and a new update takes it off" {
                let p, ctx = Fixture.start ()

                let notice (c: OrderContext) =
                    c.Scenarios[0].RulesNoLongerApply,
                    c.Scenarios[0].Prescription
                    |> Array.exists (Array.contains (OrderScenario.rulesNotice |> Warning))

                let moved = ctx |> Fixture.onData p (Fixture.patient (21N / 2N))

                moved |> notice |> Expect.equal "on" (true, true)

                moved
                |> Fixture.onData p (Fixture.patient (19N / 2N))
                |> notice
                |> Expect.equal "off" (false, false)
            }

            test "the order holds one dose" {
                Fixture.order.Value |> Order.isOneDose |> Expect.isTrue "one"
                Fixture.order.Value
                |> Fixture.twoDoses
                |> Order.isOneDose
                |> Expect.isFalse "two"
            }

            test "with a renal rule the whole rule counts, so another renal band is another rule" {
                let renal =
                    Fixture.rules ()
                    |> Array.map (fun r -> { r with RenalRules = [| Fixture.renalRule |] })

                let band =
                    renal
                    |> Array.map (fun r ->
                        { r with DoseRule = { r.DoseRule with RenalRuleSource = Some "another band" } }
                    )

                PrescriptionRule.sameRules Fixture.ignored renal band
                |> Expect.isFalse "other rules"
            }

            test "a renal rule that starts to apply counts as other rules" {
                let rules = Fixture.rules ()
                let renal = rules |> Array.map (fun r -> { r with RenalRules = [| Fixture.renalRule |] })

                PrescriptionRule.sameRules Fixture.ignored rules renal
                |> Expect.isFalse "other rules"
            }

            testList
                "solution rules"
                [
                    test "count with one dose, as when a central line is removed and its rule goes" {
                        let rules = Fixture.rules ()

                        PrescriptionRule.sameRules Fixture.counted (rules |> Fixture.withSolutionRules) rules
                        |> Expect.isFalse "other rules"
                    }

                    test "are ignored with more doses" {
                        let rules = Fixture.rules ()

                        PrescriptionRule.sameRules Fixture.ignored (rules |> Fixture.withSolutionRules) rules
                        |> Expect.isTrue "the same rules"
                    }

                    test "the amount a per kg limit gives at a weight does not count" {
                        let at (w: BigRational) (sr: SolutionRule) =
                            { sr with
                                SolutionLimits =
                                    sr.SolutionLimits
                                    |> Array.map (fun sl ->
                                        let perKg =
                                            5N
                                            |> ValueUnit.singleWithUnit (
                                                Units.Volume.milliLiter |> ValueUnit.per Units.Weight.kiloGram
                                            )

                                        let amount = 5N * w |> ValueUnit.singleWithUnit Units.Volume.milliLiter

                                        { sl with
                                            QuantityAdj = MinMax.createExact perKg
                                            Quantity = MinMax.createExact amount
                                        }
                                    )
                            }

                        let atWeight w =
                            Fixture.rules ()
                            |> Fixture.withSolutionRules
                            |> Array.map (fun r -> { r with SolutionRules = r.SolutionRules |> Array.map (at w) })

                        PrescriptionRule.sameRules Fixture.counted (atWeight 10N) (atWeight 20N)
                        |> Expect.isTrue "one rule"
                    }
                ]
        ]
