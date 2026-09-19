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
    |> Medication.toOrderDto
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
                    }

                let verbs =
                    [
                        Shared.Api.OrderContextCommand.UpdateOrderContext, Domain.UpdateOrderContext ctx
                        Shared.Api.OrderContextCommand.SelectOrderScenario, Domain.SelectOrderScenario ctx
                        Shared.Api.OrderContextCommand.UpdateOrderScenario, Domain.UpdateOrderScenario ctx
                        Shared.Api.OrderContextCommand.ResetOrderScenario, Domain.ResetOrderScenario ctx
                        Shared.Api.OrderContextCommand.DecreaseScheduleFrequencyProperty,
                        Domain.DecreaseScheduleFrequencyProperty ctx
                        Shared.Api.OrderContextCommand.IncreaseScheduleFrequencyProperty,
                        Domain.IncreaseScheduleFrequencyProperty ctx
                        Shared.Api.OrderContextCommand.SetMinScheduleFrequencyProperty,
                        Domain.SetMinScheduleFrequencyProperty ctx
                        Shared.Api.OrderContextCommand.SetMaxScheduleFrequencyProperty,
                        Domain.SetMaxScheduleFrequencyProperty ctx
                        Shared.Api.OrderContextCommand.SetMedianScheduleFrequencyProperty,
                        Domain.SetMedianScheduleFrequencyProperty ctx
                        Shared.Api.OrderContextCommand.DecreaseOrderableDoseQuantityProperty(2, true),
                        Domain.DecreaseOrderableDoseQuantityProperty(ctx, 2, true)
                        Shared.Api.OrderContextCommand.IncreaseOrderableDoseQuantityProperty(3, false),
                        Domain.IncreaseOrderableDoseQuantityProperty(ctx, 3, false)
                        Shared.Api.OrderContextCommand.SetMinOrderableDoseQuantityProperty,
                        Domain.SetMinOrderableDoseQuantityProperty ctx
                        Shared.Api.OrderContextCommand.SetMaxOrderableDoseQuantityProperty,
                        Domain.SetMaxOrderableDoseQuantityProperty ctx
                        Shared.Api.OrderContextCommand.SetMedianOrderableDoseQuantityProperty,
                        Domain.SetMedianOrderableDoseQuantityProperty ctx
                        Shared.Api.OrderContextCommand.DecreaseOrderableDoseRateProperty(1, true),
                        Domain.DecreaseOrderableDoseRateProperty(ctx, 1, true)
                        Shared.Api.OrderContextCommand.IncreaseOrderableDoseRateProperty(1, false),
                        Domain.IncreaseOrderableDoseRateProperty(ctx, 1, false)
                        Shared.Api.OrderContextCommand.SetMinOrderableDoseRateProperty,
                        Domain.SetMinOrderableDoseRateProperty ctx
                        Shared.Api.OrderContextCommand.SetMaxOrderableDoseRateProperty,
                        Domain.SetMaxOrderableDoseRateProperty ctx
                        Shared.Api.OrderContextCommand.SetMedianOrderableDoseRateProperty,
                        Domain.SetMedianOrderableDoseRateProperty ctx
                        Shared.Api.OrderContextCommand.DecreaseComponentOrderableQuantityProperty("cmp", 2, true),
                        Domain.DecreaseComponentQuantityProperty(ctx, "cmp", 2, true)
                        Shared.Api.OrderContextCommand.IncreaseComponentOrderableQuantityProperty("cmp", 2, false),
                        Domain.IncreaseComponentQuantityProperty(ctx, "cmp", 2, false)
                        Shared.Api.OrderContextCommand.SetMinComponentOrderableQuantityProperty "cmp",
                        Domain.SetMinComponentQuantityProperty(ctx, "cmp")
                        Shared.Api.OrderContextCommand.SetMaxComponentOrderableQuantityProperty "cmp",
                        Domain.SetMaxComponentQuantityProperty(ctx, "cmp")
                        Shared.Api.OrderContextCommand.SetMedianComponentOrderableQuantityProperty "cmp",
                        Domain.SetMedianComponentQuantityProperty(ctx, "cmp")
                    ]

                verbs |> List.length |> Expect.equal "every case of the wire's union" 24

                for verb, expected in verbs do
                    OrderContextMapper.Command.toDomain verb ctx |> Expect.equal $"{verb}" expected
            }
        ]
