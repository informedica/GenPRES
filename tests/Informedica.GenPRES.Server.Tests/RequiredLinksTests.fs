/// The orders a link rule with Required yes matches on its linked side: they need a main order, so the
/// pick lists of the order context port leave them out, over the doses of week 1 of the hydrocortison taper.
module Informedica.GenPRES.Server.Tests.RequiredLinksTests

open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


module Fixtures =

    open Informedica.Utils.Lib.BCL
    open OrderPlanTests


    let routes: RouteMapping[] =
        [|
            {
                Long = "ORAAL"
                Short = "or"
            }
        |]


    let indication = "Afbouwschema week 1"


    /// The oral hydrocortison rule of week 1 of the taper, with the dose text of the time of day.
    let rowOf doseText : DoseRuleData =
        let r = RuleFixtures.row Resources.Departments.defaultDepartment

        { r with
            Generic = { r.Generic with Name = "hydrocortison" }
            Indication = indication
            Route = "ORAAL"
            ScheduleData =
                { r.ScheduleData with
                    DoseType = "discontinuous"
                    DoseText = doseText
                    Freqs = [| 1N |]
                    FreqUnit = "dag"
                    DoseLimitData =
                        { r.ScheduleData.DoseLimitData with
                            Component = "hydrocortison"
                            Substance = "hydrocortison"
                        }
                }
        }


    let tablet = Product.create "hydrocortison" "tablet" "ORAAL" [| "hydrocortison" |]


    /// The three doses of week 1 of the taper, as the dose rules of the sheet give them.
    let doseRules =
        [|
            rowOf "week 1 7:00 uur"
            rowOf "week 1 13:00 uur"
            rowOf "week 1 18:00 uur"
        |]
        |> DoseRuleLoader.fromData routes [||] [| tablet |]
        |> fst


    /// A LinkRules row of week 1 of the taper: the dose at the time of day linked to the dose of 7:00.
    let linkRow doseText required : LinkRuleData =
        {
            Generic = "hydrocortison"
            Route = "ORAAL"
            Indication = indication
            DoseType = "discontinuous"
            DoseText = doseText
            MainGeneric = "hydrocortison"
            MainRoute = "ORAAL"
            MainIndication = indication
            MainDoseType = "discontinuous"
            MainDoseText = "week 1 7:00 uur"
            Required = required
        }


    /// The two rows of the sheet: the doses of 13:00 and 18:00 cannot be ordered without the dose of 7:00.
    let sheetRows = [| linkRow "week 1 13:00 uur" "yes"; linkRow "week 1 18:00 uur" "yes" |]


    /// The rows of the sheet with the dose of 18:00 changed to a dose that can also be ordered on its own,
    /// a variant made for the test.
    let standAloneRows = [| linkRow "week 1 13:00 uur" "yes"; linkRow "week 1 18:00 uur" "no" |]


    let linkRules rows =
        rows |> LinkRule.ofRows |> Result.defaultWith (fun e -> failtest $"%A{e}")


    /// The dose rules above with the link rules of the rows; no rule sets and no totals data.
    type Linked(rows) =
        inherit RulesOf(doseRules)

        interface Resources.IResourceProvider with
            member _.Get(key: Resources.ResourceKey<'T>) : 'T =
                if key.Name = Resources.Keys.linkRules.Name then
                    unbox<'T>(box (linkRules rows))
                elif key.Name = Resources.Keys.standAloneDoseRules.Name then
                    unbox<'T>(box (doseRules |> LinkRule.standAlone routes (linkRules rows)))
                elif key.Name = Resources.Keys.ruleSetData.Name then
                    unbox<'T>(box Array.empty<RuleSetData>)
                else
                    raise (System.NotImplementedException())

            member _.GetDoseRules() = doseRules
            member _.GetRouteMappings() = routes
            member _.GetTotals() = [||]


    let provider rows = Linked rows :> Resources.IResourceProvider


    let doseTexts (rules: DoseRule[]) = rules |> Array.map (_.DoseType >> DoseType.getText)


    let texts doseTypes = doseTypes |> Array.map DoseType.getText


    /// The oral hydrocortison context of week 1 of the taper, its dose type not yet picked.
    let hydrocortison provider =
        let pat = RuleFixtures.patient (Some 8N) false (Some 25N) None
        let ctx = OrderContext.create OrderLogging.noOp provider pat

        { ctx with
            Filter =
                { ctx.Filter with
                    Indication = Some indication
                    Generic = Some "hydrocortison"
                    Route = Some "ORAAL"
                }
        }


    /// The dose types the oral hydrocortison context of week 1 of the taper offers to pick from.
    let offeredDoseTypes provider =
        hydrocortison provider
        |> OrderContext.getRules OrderLogging.noOp provider
        |> fst
        |> _.Filter.DoseTypes
        |> texts


open Fixtures


[<Tests>]
let tests =
    testList
        "the orders that need a main order, over the hydrocortison taper of week 1"
        [
            testList
                "LinkRule.needsMainOrder"
                [
                    test "the doses of 13:00 and 18:00 need the dose of 7:00" {
                        doseRules
                        |> Array.filter (LinkRule.needsMainOrder routes (linkRules sheetRows))
                        |> doseTexts
                        |> Expect.equal "13:00 and 18:00" [| "week 1 13:00 uur"; "week 1 18:00 uur" |]
                    }

                    test "a dose that can also be ordered on its own needs no main order" {
                        doseRules
                        |> Array.filter (LinkRule.needsMainOrder routes (linkRules standAloneRows))
                        |> doseTexts
                        |> Expect.equal "13:00 only" [| "week 1 13:00 uur" |]
                    }

                    test "without link rules no dose needs a main order" {
                        doseRules
                        |> Array.filter (LinkRule.needsMainOrder routes [||])
                        |> Expect.isEmpty "none"
                    }
                ]

            testList
                "the pick list of the dose types"
                [
                    test "a linked order that needs its main order is not offered: only 7:00" {
                        provider sheetRows
                        |> ServerApi.Adapters.withStandAloneDoseRules
                        |> offeredDoseTypes
                        |> Expect.equal "7:00" [| "week 1 7:00 uur" |]
                    }

                    test "a linked order that can also be ordered on its own is offered: 7:00 and 18:00" {
                        provider standAloneRows
                        |> ServerApi.Adapters.withStandAloneDoseRules
                        |> offeredDoseTypes
                        |> Expect.equal "7:00 and 18:00" [| "week 1 7:00 uur"; "week 1 18:00 uur" |]
                    }

                    testAsync "through the port the pick list offers 7:00, and the order of 7:00 offers 13:00 and 18:00" {
                        let sut = ServerApi.Adapters.makeAppEnv (provider sheetRows)

                        let! answer =
                            hydrocortison (provider sheetRows)
                            |> PlanContext.create "c-1" OrderCategory.Medication
                            |> sut.orderContext.evaluate OrderContext.UpdateOrderContext

                        match answer with
                        | Ok(Evaluated pc) ->
                            pc.Context.Filter.DoseTypes
                            |> texts
                            |> Expect.equal "7:00" [| "week 1 7:00 uur" |]

                            let! offers = sut.orderContext.linkedOrders pc

                            offers
                            |> Result.map (Array.collect (_.Context.Filter.DoseTypes >> texts))
                            |> Expect.equal "13:00 and 18:00" (Ok [| "week 1 13:00 uur"; "week 1 18:00 uur" |])
                        | other -> failtest $"expected an evaluated context, got %A{other}"
                    }

                    test "rules that did not load leave no dose rules" {
                        // the empty resources a failed load or reload leaves in the cache
                        (Resources.CachedResourceProvider(
                            OrderLogging.noOp,
                            (fun () -> Error [ Message.Warning "LinkRules: Required: ja is not yes or no" ]),
                            None
                        )
                        :> Resources.IResourceProvider)
                        |> ServerApi.Adapters.withStandAloneDoseRules
                        |> _.GetDoseRules()
                        |> Expect.isEmpty "none"
                    }

                    test "the provider itself still offers every dose, for the linked offers" {
                        provider sheetRows
                        |> offeredDoseTypes
                        |> Expect.equal "all three" [| "week 1 7:00 uur"; "week 1 13:00 uur"; "week 1 18:00 uur" |]
                    }
                ]
        ]
