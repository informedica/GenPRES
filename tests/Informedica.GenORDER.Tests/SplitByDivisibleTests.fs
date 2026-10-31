/// A rule splits per Divisible of its products, so a product does not take the step of another
/// product. The picked products say which group was picked, until a choice of the rule lookup
/// changes.
module SplitByDivisibleTests

open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


/// The rule and the products the split is tested on.
module Fixture =

    module GenForm = Informedica.GenForm.Lib.Types

    let stuk = Units.General.general "stuk"
    let perStuk = Units.Mass.milliGram |> ValueUnit.per stuk
    let baseRule = lazy (StrictPreparationTests.Fixture.suppositoryRule 100N (Some 1N) |> Array.head)


    /// The product of the strength in mg with the GPK and the Divisible.
    let product gpk strength div : GenForm.ProductComponent =
        let p = baseRule.Value.ComponentLimits[0].Products[0]

        { p with
            GPK = gpk
            Divisible = div
            Substances =
                p.Substances
                |> Array.map (fun s -> { s with Concentration = strength |> ValueUnit.singleWithUnit perStuk |> Some })
        }


    let doseRule products : GenForm.DoseRule =
        let dr = baseRule.Value

        { dr with ComponentLimits = dr.ComponentLimits |> Array.map (fun cl -> { cl with Products = products }) }


    let rule products : GenForm.PrescriptionRule =
        {
            Patient = Filter.doseFilter.Patient
            DoseRule = doseRule products
            SolutionRules = [||]
            RenalRules = [||]
        }


    let groupsOf products =
        products
        |> rule
        |> OrderContext.Helpers.splitByDivisible id
        |> Array.map (_.DoseRule.ComponentLimits >> Array.collect (_.Products >> Array.map _.GPK))


    let provider products =
        OrderPlanTests.RulesOf [| doseRule products |] :> Resources.IResourceProvider

    /// The answer of an evaluation that is not refused.
    let contextOf cmd provider =
        match
            cmd
            |> OrderContext.evaluateOutcome Scenarios.testStart OrderLogging.noOp provider
        with
        | Ok(Evaluated answer) -> answer |> OrderContext.Command.get
        | r -> failtestf "no answer: %A" r


    /// The scenarios of the child, one per group.
    let scenarios provider =
        let ctx =
            StrictPreparationTests.Fixture.child
            |> OrderContext.create OrderLogging.noOp provider
        provider |> contextOf (ctx |> OrderContext.UpdateOrderContext)


    /// The second scenario picked, through the command of the page.
    let picked provider =
        provider
        |> contextOf (OrderContext.SelectNthOrderScenario(provider |> scenarios, 1))


    let products (ctx: OrderContext) = ctx.Scenarios |> Array.map _.ProductsIds


    let one = product "1" 100N (Some 1N)
    let two = product "2" 100N (Some 2N)
    let without = product "3" 100N None


open Fixture


[<Tests>]
let tests =
    testList
        "the split by Divisible"
        [
            test "products that agree stay one rule" {
                [| one; { one with GPK = "4" } |] |> groupsOf |> Expect.hasLength "one rule" 1
            }

            test "a rule without products stays one rule" { [||] |> groupsOf |> Expect.hasLength "one rule" 1 }

            testList
                "products with 1, none and 2 give the same three groups, in every order"
                [
                    for ps in
                        [
                            [| one; without; two |]
                            [| one; two; without |]
                            [| without; one; two |]
                            [| without; two; one |]
                            [| two; one; without |]
                            [| two; without; one |]
                        ] do
                        test (ps |> Array.map _.GPK |> String.concat ",") {
                            ps
                            |> groupsOf
                            |> Expect.equal "the three groups" [| [| "3" |]; [| "1" |]; [| "2" |] |]
                        }
                ]

            test "two products with the same value stay together beside another value" {
                [| one; { one with GPK = "4" }; two |]
                |> groupsOf
                |> Expect.equal "two groups" [| [| "1"; "4" |]; [| "2" |] |]
            }

            test "the drop size is set per group" {
                let drops = Units.Volume.dropletWithDropsPerMl
                let form = "druppels voor oraal gebruik"
                let droplet gpk div = { product gpk 100N (Some div) with Form = form }

                let formRoute: Informedica.GenForm.Lib.Types.FormRoute =
                    {
                        Route = baseRule.Value.Route
                        Form = form
                        Unit = stuk
                        DoseUnit = drops 1N
                        MinDoseQty = None
                        MaxDoseQty = 10N |> ValueUnit.singleWithUnit (drops 1N) |> Some
                        MinDoseQtyPerKg = None
                        MaxDoseQtyPerKg = None
                        Divisibility = None
                        Timed = false
                        Reconstitute = false
                        IsSolution = false
                    }

                let pr = [| droplet "1" 24N; droplet "2" 36N |] |> rule

                { pr with
                    DoseRule =
                        { pr.DoseRule with
                            Generic = { pr.DoseRule.Generic with Form = form |> PharmaceuticalForm.fromString }
                        }
                }
                |> OrderContext.Helpers.splitByDivisible (
                    DoseRule.addFormLimits OrderPlanTests.RuleFixtures.routeMapping [| formRoute |]
                )
                |> Array.map (_.DoseRule.FormLimit >> Option.map _.DoseUnit)
                |> Expect.equal "the drops per mL of each group" [| Some(drops 24N); Some(drops 36N) |]
            }

            testList
                "the picked products"
                [
                    let p = lazy (provider [| one; two |])

                    test "the evaluation gives a scenario per group" {
                        p.Value
                        |> scenarios
                        |> products
                        |> Expect.equal "both groups" [| [| "1" |]; [| "2" |] |]
                    }

                    test "a pick keeps its group through a later evaluation" {
                        let ctx = p.Value |> picked
                        ctx.Filter.PickedProducts |> Expect.equal "the picked products" [| "2" |]

                        p.Value
                        |> contextOf (ctx |> OrderContext.UpdateOrderContext)
                        |> products
                        |> Expect.equal "only the picked group" [| [| "2" |] |]
                    }

                    test "a group without a Divisible can be picked" {
                        let p = provider [| without; two |]
                        let ctx = p |> scenarios

                        let picked = p |> contextOf (OrderContext.SelectNthOrderScenario(ctx, 0))

                        p
                        |> contextOf (picked |> OrderContext.UpdateOrderContext)
                        |> products
                        |> Expect.equal "only the picked group" [| picked.Filter.PickedProducts |]
                    }

                    test "a picked group that no longer fits gives the other groups, not the retry" {
                        let p = provider [| product "1" 240N (Some 1N); two |]
                        let ctx = p |> scenarios
                        ctx |> products |> Expect.equal "the group that fits" [| [| "2" |] |]

                        let answer =
                            p
                            |> contextOf (
                                { ctx with OrderContext.Filter.PickedProducts = [| "1" |] }
                                |> OrderContext.UpdateOrderContext
                            )

                        answer |> products |> Expect.equal "the other group" [| [| "2" |] |]
                        answer.Scenarios[0].FitsOnlyWithoutDivisible |> Expect.isFalse "not the retry"
                    }

                    test "products that no group holds select nothing" {
                        let ctx = p.Value |> scenarios

                        p.Value
                        |> contextOf (
                            { ctx with OrderContext.Filter.PickedProducts = [| "9" |] }
                            |> OrderContext.UpdateOrderContext
                        )
                        |> products
                        |> Expect.hasLength "all groups" 2
                    }

                    test "a command on a picked scenario keeps its group" {
                        p.Value
                        |> contextOf (p.Value |> picked |> OrderContext.SelectOrderScenario)
                        |> products
                        |> Expect.equal "the picked group" [| [| "2" |] |]
                    }

                    test "the pick survives the reconciliation of a plan row and the Dto of a signed version" {
                        let f = (p.Value |> picked).Filter

                        Filter.reconcile { f with PickedProducts = [||] } f
                        |> _.PickedProducts
                        |> Expect.equal "the reconciliation" [| "2" |]

                        match f |> Filter.Dto.toDto |> Filter.Dto.fromDto with
                        | Ok back -> back.PickedProducts |> Expect.equal "the Dto" [| "2" |]
                        | Error e -> failtestf "no round trip: %A" e
                    }

                    testList
                        "a change of a choice of the rule lookup clears the pick"
                        [
                            for field in OrderContext.filterFields do
                                test $"%A{field}" {
                                    let ctx = p.Value |> picked

                                    ctx
                                    |> OrderContext.changeFilter OrderCategory.Drug field None
                                    |> Result.map _.Filter.PickedProducts
                                    |> Expect.equal "no pick" (Ok [||])
                                }
                        ]

                    test "a seed with other choices clears the pick, a reload keeps it" {
                        let ctx = p.Value |> picked
                        let f = ctx.Filter
                        let seed source ind =
                            ctx |> OrderContext.seedFilter source ind f.Generic f.Route f.Form f.DoseType

                        (seed SeedSource.Url (Some "pijn")).Filter.PickedProducts
                        |> Expect.isEmpty "cleared"

                        (seed SeedSource.Reload f.Indication).Filter.PickedProducts
                        |> Expect.equal "the reload" [| "2" |]
                    }

                    test "a change of the diluent, the components or the patient keeps the pick" {
                        let ctx = p.Value |> picked

                        ctx
                        |> OrderContext.changeDiluent None
                        |> Result.map _.Filter.PickedProducts
                        |> Expect.equal "the diluent" (Ok [| "2" |])

                        ctx
                        |> OrderContext.setNthComponents [||]
                        |> Result.map _.Filter.PickedProducts
                        |> Expect.equal "the components" (Ok [| "2" |])

                        p.Value
                        |> contextOf (OrderContext.UpdatePatient(ctx, ctx.Patient))
                        |> products
                        |> Expect.equal "the patient" [| [| "2" |] |]
                    }
                ]
        ]
