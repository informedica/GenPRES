/// The nutrition slot on the order context path: a slot discovered from the rule set of its
/// category, and an evaluated nutrition context narrowed to that rule set, as the plan port does.
module Informedica.GenPRES.Server.Tests.NutritionRuleSetsTests

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


module Fixtures =

    let rowOf generic indication =
        let r = OrderPlanTests.RuleFixtures.row Resources.Departments.defaultDepartment

        { r with
            Generic = { r.Generic with Name = generic }
            Indication = indication
            ScheduleData =
                { r.ScheduleData with
                    DoseLimitData =
                        { r.ScheduleData.DoseLimitData with
                            Component = generic
                            Substance = generic
                        }
                }
        }


    // a nutrition rule for "voeding" beside a medication rule for "koorts"
    let data = [| rowOf "voedsel" "voeding"; rowOf "paracetamol" "koorts" |]

    let product g = Product.create g "zetpil" "RECTAAL" [| g |]


    let rules =
        DoseRuleLoader.fromData
            OrderPlanTests.RuleFixtures.routeMapping
            [||]
            [| product "voedsel"; product "paracetamol" |]
            data
        |> fst


    /// The rule set rows of a feeding under "voeding", expanded over the dose rules as the resource
    /// expands them.
    let feedingRows =
        [|
            OrderPlanTests.EvaluateFixtures.ruleSetRow "EnteralFeeding" "Enterale Voeding" "voeding" ""
        |]
        |> RuleSet.expand (RuleSet.DoseRuleNames.ofData data)


    /// The rules above with the rule set rows of a feeding; the totals data empty.
    type FeedingRuleSet() =
        inherit OrderPlanTests.RulesOf(rules)

        interface Resources.IResourceProvider with
            member _.Get(key: Resources.ResourceKey<'T>) : 'T =
                if key.Name = Resources.Keys.ruleSetData.Name then
                    unbox<'T>(box feedingRows)
                else
                    raise (System.NotImplementedException())

            member _.GetDoseRules() = rules
            member _.GetRouteMappings() = OrderPlanTests.RuleFixtures.routeMapping
            member _.GetTotals() = [||]


    let provider = FeedingRuleSet() :> Resources.IResourceProvider

    let patient = OrderPlanTests.RuleFixtures.patient (Some 5N) false (Some 20N) None


    /// An empty context of the category for the patient.
    let empty category =
        NutritionRuleSet.emptyContext None patient |> PlanContext.create "" category


    /// The generics an evaluation offers, evaluated or refused.
    let generics =
        Result.map (
            function
            | Evaluated(pc: PlanContext)
            | Refused(pc, _) -> pc.Context.Filter.Generics
        )


open Fixtures


[<Tests>]
let tests =
    testList
        "the nutrition slot on the order context path"
        [
            testAsync "an evaluated feeding context is narrowed to the generics of its rule set" {
                let sut = ServerApi.Adapters.makeAppEnv provider

                let! answer =
                    empty (OrderCategory.Nutrition NutritionCategory.EnteralFeeding)
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                answer
                |> generics
                |> Expect.equal "voedsel, not paracetamol" (Ok [| "voedsel" |])
            }

            testAsync "a medication context is not narrowed" {
                let sut = ServerApi.Adapters.makeAppEnv provider

                let! answer =
                    empty OrderCategory.Medication
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                answer |> generics |> Expect.equal "both" (Ok [| "paracetamol"; "voedsel" |])
            }

            testAsync "a category without a rule set is refused in words" {
                let sut = ServerApi.Adapters.makeAppEnv provider

                let! answer =
                    empty (OrderCategory.Nutrition NutritionCategory.TPN)
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                answer |> Expect.equal "no TPN" (Error [| "RuleSets: no rule set for TPN" |])
            }

            testAsync "a nutrition context the rules refuse comes back refused, its pick as sent" {
                let sut = ServerApi.Adapters.makeAppEnv provider
                let feeding = empty (OrderCategory.Nutrition NutritionCategory.EnteralFeeding)

                let! answer =
                    { feeding with
                        Context =
                            { feeding.Context with Filter = { feeding.Context.Filter with Generic = Some "onbekend" } }
                    }
                    |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                match answer with
                | Ok(Refused(pc, _)) -> pc.Context.Filter.Generic |> Expect.equal "the pick" (Some "onbekend")
                | other -> failtest $"expected a refusal, got %A{other}"
            }

            test "a slot is discovered with only the generics of its rule set" {
                ServerApi.Adapters.NutritionRuleSets.discover
                    Scenarios.testStart
                    OrderLogging.noOp
                    provider
                    "s-1"
                    NutritionCategory.EnteralFeeding
                    patient
                |> Result.map _.Filter.Generics
                |> Expect.equal "voedsel" (Ok [| "voedsel" |])
            }
        ]
