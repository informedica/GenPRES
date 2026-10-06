/// The order context mapper: the contract model's order context to the plan context's Dto and
/// back, and the wire's command verbs as the domain's commands.
module Informedica.GenPRES.Server.Tests.MappersOrderContextTests

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
// after Expecto, whose FocusState has a Normal case too
open Shared.Types
open ServerApi


module Domain = Informedica.GenOrder.Lib.OrderContext


/// A paracetamol suppository order from the test scenarios, as the contract model carries
/// it: the domain's order Dto through the order mapper.
let order: Order =
    Scenarios.pcmSupp
    |> Medication.toOrderDto Scenarios.testStart
    |> Mappers.Order.mapFromOrderToShared [| "paracetamol" |]


let scenario: OrderScenario =
    Shared.Models.OrderScenario.create
        "koorts"
        "paracetamol"
        "zetpil"
        "rect"
        (Discontinuous "3-4 x/dag")
        None
        (Some "paracetamol")
        (Some "paracetamol")
        [||]
        [| "paracetamol" |]
        [| "paracetamol" |]
        [| [| Valid [| Normal "paracetamol "; Bold "240 mg"; Normal " 3 x/dag" |] |] |]
        [| [| Caution [| Normal "zetpil "; Italic "240 mg" |] |] |]
        [| [| Warning [| Normal "rectaal" |] |] |]
        order
        true
        false
        None
        [| "gpk-1" |]
        None


let context: OrderContext =
    { Shared.Models.OrderContext.empty with
        Id = "c-1"
        Category = OrderCategory.Nutrition NutritionCategory.TPN
        DemoVersion = false
        Filter =
            { Shared.Models.OrderContext.filter with
                Indications = [| "koorts"; "pijn" |]
                Generics = [| "paracetamol" |]
                DoseTypes = [| Discontinuous "3-4 x/dag"; Once "eenmalig" |]
                Indication = Some "koorts"
                Generic = Some "paracetamol"
                DoseType = Some(Discontinuous "3-4 x/dag")
                Diluents = [| "NaCl 0.9%" |]
                SelectedComponents = [| "paracetamol" |]
            }
        Patient = StubPatientData.patient
        Scenarios = [| scenario |]
        Intake =
            { Shared.Models.Totals.empty with
                Volume = [| Normal "100 ml" |]
                Energy = [| Bold "50 kcal"; Normal "/dag" |]
            }
    }


[<Tests>]
let tests =
    testList
        "the order context mapper"
        [
            test "L3: to the Dto and back is the identity, the demo flag the server's" {
                context
                |> OrderContextMapper.ofModel
                |> OrderContextMapper.toModel false
                |> Expect.equal "the same context" context

                (context |> OrderContextMapper.ofModel |> OrderContextMapper.toModel true).DemoVersion
                |> Expect.isTrue "demo as the server says"
            }

            for access in [ Some CVL; Some PVL; Some EnteralTube; None ] do
                test $"the scenario access %A{access} goes to the Dto and back" {
                    let sc = { scenario with Access = access }

                    sc
                    |> OrderContextMapper.scenario 0
                    |> OrderContextMapper.scenarioBack
                    |> _.Access
                    |> Expect.equal "the same access" access
                }

            test "a scenario Dto without an access reads as none" {
                { (scenario |> OrderContextMapper.scenario 0) with Access = null }
                |> OrderContextMapper.scenarioBack
                |> _.Access
                |> Expect.isNone "no access"
            }

            test "the argumentation goes to the Dto and back, the text and none" {
                let text = "Sepsis, hogere dosis in overleg met de apotheek"
                let argued = { context with Argumentation = Some text }

                (argued |> OrderContextMapper.ofModel).Context.Argumentation
                |> Expect.equal "on the Dto" (Some text)

                (argued |> OrderContextMapper.ofModel |> OrderContextMapper.toModel false).Argumentation
                |> Expect.equal "and back" (Some text)

                (context |> OrderContextMapper.ofModel |> OrderContextMapper.toModel false).Argumentation
                |> Expect.isNone "none stays none"
            }

            test "a plan context JSON from before the field, a version 1 row, reads with none" {
                let json =
                    (context |> OrderContextMapper.ofModel |> Canonical.serialize)
                        .Replace(",\"Argumentation\":null", "")

                json.Contains "Argumentation" |> Expect.isFalse "no such field in the row"

                json
                |> Canonical.deserialize<PlanContext.Dto.Dto>
                |> OrderContextMapper.toModel false
                |> Expect.equal "the context as it was, the field none" context
            }

            test "the cap: none and a text of at most 1000 characters pass, one over is refused in the server's words" {
                let atCap = String.replicate 1000 "a"

                OrderContextService.Argumentation.check None |> Expect.equal "none" (Ok None)

                OrderContextService.Argumentation.check (Some atCap)
                |> Expect.equal "at the cap" (Ok(Some atCap))

                OrderContextService.Argumentation.check (Some(atCap + "a"))
                |> Expect.equal "one over" (Error [| "De argumentatie is te lang: 1001 tekens, ten hoogste 1000" |])
            }

            test "the parse refuses a text over the cap before it maps, for a context and for a plan" {
                let over = { context with Argumentation = Some(String.replicate 1001 "a") }
                let refused = Error [| "De argumentatie is te lang: 1001 tekens, ten hoogste 1000" |]

                over
                |> OrderContextService.parse
                |> Result.map ignore
                |> Expect.equal "the context" refused

                { Shared.Models.OrderPlan.empty with OrderContexts = [| context; over |] }
                |> OrderPlanCommand.parsePlan
                |> Result.map ignore
                |> Expect.equal "the plan, on any of its contexts" refused

                match { context with Argumentation = Some "kort" } |> OrderContextService.parse with
                | Ok pc -> pc.Context.Argumentation |> Expect.equal "within the cap, parsed" (Some "kort")
                | Error e -> failtest $"refused: %A{e}"
            }

            test "outside L3: blank text items are dropped, and the scenario number is the Dto's" {
                let blank =
                    { context with
                        Scenarios =
                            [|
                                { scenario with
                                    Prescription = [| [| Valid [| Normal ""; Bold "240 mg"; Normal "  " |] |] |]
                                }
                            |]
                    }

                let back = blank |> OrderContextMapper.ofModel |> OrderContextMapper.toModel false

                back.Scenarios[0].Prescription
                |> Expect.equal "the blank ones gone" [| [| Valid [| Bold "240 mg" |] |] |]

                (context |> OrderContextMapper.ofModel).Context.Scenarios
                |> Array.map _.No
                |> Expect.equal "numbered by place" [| 0 |]
            }

            test "the Dto parses: the domain reads what the mapper writes" {
                match context |> OrderContextMapper.ofModel |> PlanContext.Dto.fromDto with
                | Ok pc ->
                    pc.Id |> Expect.equal "id" "c-1"

                    pc.Category
                    |> Expect.equal
                        "category"
                        (Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition
                            Informedica.GenOrder.Lib.Types.NutritionCategory.TPN)

                    pc.Context.Scenarios.Length |> Expect.equal "one scenario" 1
                    pc.Context.Filter.Generic |> Expect.equal "the selection" (Some "paracetamol")

                    pc.Context.Scenarios[0].Prescription
                    |> Expect.equal
                        "marked-up text"
                        [| [| Informedica.GenOrder.Lib.Types.Valid "paracetamol #240 mg# 3 x/dag" |] |]

                    pc.Intake.Energy |> Expect.equal "the intake" (Some "#50 kcal#/dag")
                | Error e -> failtest $"no plan context: %A{e}"
            }

            test "text: what the parser cut from the domain's text renders back to that text" {
                // the items the client ever holds: the parser's output over the domain's markup
                let text = "paracetamol #240 mg# per |zetpil| 3 x/dag"
                let items = text |> Mappers.parseTextItem

                items
                |> Array.forall (
                    function
                    | Normal s
                    | Bold s
                    | Italic s -> s.Contains "#" |> not && s.Contains "|" |> not
                )
                |> Expect.isTrue "no item holds a delimiter"

                items
                |> OrderContextMapper.TextItem.render
                |> Expect.equal "the domain's text again" text
            }

            test "text: rendering and parsing are inverse on items with text; a blank item is dropped" {
                let items = [| Normal "a "; Bold "b"; Normal " and "; Italic "c"; Normal " d" |]

                items
                |> OrderContextMapper.TextItem.render
                |> Expect.equal "rendered" "a #b# and |c| d"

                items
                |> OrderContextMapper.TextItem.render
                |> Mappers.parseTextItem
                |> Expect.equal "parsed back" items

                [| Bold "b"; Normal " "; Italic "c" |]
                |> OrderContextMapper.TextItem.render
                |> Mappers.parseTextItem
                |> Expect.equal "the blank item between gone" [| Bold "b"; Italic "c" |]
            }

            test "every command verb becomes the domain command over the context given" {
                let ctx: Informedica.GenOrder.Lib.Types.OrderContext =
                    {
                        Filter =
                            context.Filter
                            |> OrderContextMapper.filter
                            |> Filter.Dto.fromDto
                            |> Result.defaultWith (fun e -> failtest $"{e}")
                        Patient = Informedica.GenForm.Lib.Patient.patient
                        Scenarios = [||]
                        Argumentation = None
                    }

                let verbs =
                    [
                        Shared.Api.OrderViewCommand.ResetOrderScenario, Domain.ResetOrderScenario ctx
                        Shared.Api.OrderViewCommand.DecreaseScheduleFrequencyProperty,
                        Domain.DecreaseScheduleFrequencyProperty ctx
                        Shared.Api.OrderViewCommand.IncreaseScheduleFrequencyProperty,
                        Domain.IncreaseScheduleFrequencyProperty ctx
                        Shared.Api.OrderViewCommand.SetMinScheduleFrequencyProperty,
                        Domain.SetMinScheduleFrequencyProperty ctx
                        Shared.Api.OrderViewCommand.SetMaxScheduleFrequencyProperty,
                        Domain.SetMaxScheduleFrequencyProperty ctx
                        Shared.Api.OrderViewCommand.SetMedianScheduleFrequencyProperty,
                        Domain.SetMedianScheduleFrequencyProperty ctx
                        Shared.Api.OrderViewCommand.DecreaseOrderableDoseQuantityProperty(2, true),
                        Domain.DecreaseOrderableDoseQuantityProperty(ctx, 2, true)
                        Shared.Api.OrderViewCommand.IncreaseOrderableDoseQuantityProperty(3, false),
                        Domain.IncreaseOrderableDoseQuantityProperty(ctx, 3, false)
                        Shared.Api.OrderViewCommand.SetMinOrderableDoseQuantityProperty,
                        Domain.SetMinOrderableDoseQuantityProperty ctx
                        Shared.Api.OrderViewCommand.SetMaxOrderableDoseQuantityProperty,
                        Domain.SetMaxOrderableDoseQuantityProperty ctx
                        Shared.Api.OrderViewCommand.SetMedianOrderableDoseQuantityProperty,
                        Domain.SetMedianOrderableDoseQuantityProperty ctx
                        Shared.Api.OrderViewCommand.SetOrderableDoseQuantityPercProperty 50,
                        Domain.SetOrderableDoseQuantityPercProperty(ctx, 50)
                        Shared.Api.OrderViewCommand.DecreaseOrderableDoseRateProperty(1, true),
                        Domain.DecreaseOrderableDoseRateProperty(ctx, 1, true)
                        Shared.Api.OrderViewCommand.IncreaseOrderableDoseRateProperty(1, false),
                        Domain.IncreaseOrderableDoseRateProperty(ctx, 1, false)
                        Shared.Api.OrderViewCommand.SetMinOrderableDoseRateProperty,
                        Domain.SetMinOrderableDoseRateProperty ctx
                        Shared.Api.OrderViewCommand.SetMaxOrderableDoseRateProperty,
                        Domain.SetMaxOrderableDoseRateProperty ctx
                        Shared.Api.OrderViewCommand.SetMedianOrderableDoseRateProperty,
                        Domain.SetMedianOrderableDoseRateProperty ctx
                        Shared.Api.OrderViewCommand.DecreaseComponentOrderableQuantityProperty("cmp", 2, true),
                        Domain.DecreaseComponentQuantityProperty(ctx, "cmp", 2, true)
                        Shared.Api.OrderViewCommand.IncreaseComponentOrderableQuantityProperty("cmp", 2, false),
                        Domain.IncreaseComponentQuantityProperty(ctx, "cmp", 2, false)
                        Shared.Api.OrderViewCommand.SetMinComponentOrderableQuantityProperty "cmp",
                        Domain.SetMinComponentQuantityProperty(ctx, "cmp")
                        Shared.Api.OrderViewCommand.SetMaxComponentOrderableQuantityProperty "cmp",
                        Domain.SetMaxComponentQuantityProperty(ctx, "cmp")
                        Shared.Api.OrderViewCommand.SetMedianComponentOrderableQuantityProperty "cmp",
                        Domain.SetMedianComponentQuantityProperty(ctx, "cmp")
                        Shared.Api.OrderViewCommand.SetNthFilterProperty(Shared.Models.OrderContext.Route, 1),
                        Domain.ChangeFilter(ctx, Types.OrderCategory.Drug, FilterField.Route, Some 1)
                        Shared.Api.OrderViewCommand.ClearFilterProperty Shared.Models.OrderContext.Generic,
                        Domain.ChangeFilter(ctx, Types.OrderCategory.Drug, FilterField.Generic, None)
                        Shared.Api.OrderViewCommand.ClearAllFilterProperty, Domain.ClearAllFilter ctx
                        Shared.Api.OrderViewCommand.SetNthDiluentProperty 0,
                        Domain.ChangeFilter(ctx, Types.OrderCategory.Drug, FilterField.Diluent, Some 0)
                        Shared.Api.OrderViewCommand.ClearDiluentProperty,
                        Domain.ChangeFilter(ctx, Types.OrderCategory.Drug, FilterField.Diluent, None)
                        Shared.Api.OrderViewCommand.SetNthComponentsProperty [| 0; 2 |],
                        Domain.SetNthComponents(ctx, [| 0; 2 |])
                        Shared.Api.OrderViewCommand.SelectNthOrderScenario 1, Domain.SelectNthOrderScenario(ctx, 1)
                        Shared.Api.OrderViewCommand.SetNthScheduleProperty(ScheduleProperty.Frequency, 2),
                        Domain.SetNthOrderValue(ctx, SetNthScheduleFrequency 2)
                        Shared.Api.OrderViewCommand.ClearScheduleProperty(ScheduleProperty.Time, [| "a" |]),
                        Domain.ClearOrderValue(ctx, ClearScheduleTime, [ "a" ])
                        Shared.Api.OrderViewCommand.SetNthOrderableProperty(OrderableProperty.DoseRate, 1),
                        Domain.SetNthOrderValue(ctx, SetNthOrderableDoseRate 1)
                        Shared.Api.OrderViewCommand.ClearOrderableProperty(OrderableProperty.Quantity, [||]),
                        Domain.ClearOrderValue(ctx, ClearOrderableQuantity, [])
                        Shared.Api.OrderViewCommand.SetNthComponentProperty(
                            "cmp",
                            ComponentProperty.OrderableQuantity,
                            0
                        ),
                        Domain.SetNthOrderValue(ctx, SetNthComponentOrderableQuantity("cmp", 0))
                        Shared.Api.OrderViewCommand.ClearComponentProperty(
                            "cmp",
                            ComponentProperty.DoseQuantityAdjust,
                            [||]
                        ),
                        Domain.ClearOrderValue(ctx, ClearComponentDoseQuantityAdjust "cmp", [])
                        Shared.Api.OrderViewCommand.SetNthItemProperty("cmp", "itm", ItemProperty.DoseRate, 3),
                        Domain.SetNthOrderValue(ctx, SetNthItemDoseRate("cmp", "itm", 3))
                        Shared.Api.OrderViewCommand.ClearItemProperty(
                            "cmp",
                            "itm",
                            ItemProperty.ComponentConcentration,
                            [||]
                        ),
                        Domain.ClearOrderValue(ctx, ClearItemComponentConcentration("cmp", "itm"), [])
                        Shared.Api.OrderViewCommand.SeedFilter(
                            SeedSource.Formulary,
                            Some "pijn",
                            Some "morfine",
                            None,
                            None,
                            Some(DoseType.Discontinuous "onderhoud")
                        ),
                        Domain.SeedFilter(
                            ctx,
                            Types.SeedSource.Formulary,
                            Some "pijn",
                            Some "morfine",
                            None,
                            None,
                            Some(Informedica.GenForm.Lib.Types.Discontinuous "onderhoud")
                        )
                    ]

                verbs
                |> List.length
                |> Expect.equal "every case of the wire's union but the argumentation" 38

                for verb, expected in verbs do
                    OrderContextMapper.Command.toDomain OrderCategory.Drug verb ctx
                    |> Expect.equal $"{verb}" expected
            }

            test "the argumentation is no domain command" {
                (fun () ->
                    OrderContextMapper.Command.toDomain
                        OrderCategory.Drug
                        (Shared.Api.OrderViewCommand.SetArgumentationProperty "text")
                    |> ignore
                )
                |> Expect.throwsT<System.ArgumentException> "answered by the server"
            }
        ]


[<Tests>]
let clearMarkTests =
    let vu = Shared.Models.Order.ValueUnit.create [| "10", 10m |] "mg" "Mass" true "dutch" ""

    let variable isNonZeroPositive =
        Shared.Models.Order.Variable.create "dose" isNonZeroPositive (Some vu) true (Some vu) (Some vu) true None

    testList
        "the clear mark"
        [
            test "a variable the client cleared goes as the mark alone, also with an increment" {
                let dto = variable true |> Mappers.Order.mapFromVariable

                (dto.IsNonZeroPositive, dto.MinOpt, dto.IncrOpt, dto.MaxOpt, dto.ValsOpt)
                |> Expect.equal "the mark, no bounds" (true, None, None, None, None)
            }

            test "a range that was not cleared goes with its bounds" {
                let dto = variable false |> Mappers.Order.mapFromVariable

                dto.IsNonZeroPositive |> Expect.isFalse "no mark"

                (dto.MinOpt.IsSome, dto.IncrOpt.IsSome, dto.MaxOpt.IsSome)
                |> Expect.equal "the bounds" (true, true, true)
            }
        ]
