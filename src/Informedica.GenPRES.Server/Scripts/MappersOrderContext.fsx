// Step 3.2 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #752):
// the order context mapper. The contract model's order context becomes the domain's
// `PlanContext.Dto` in one total step (`ofModel`: the id and the category the plan gave it,
// the filter, the patient, every scenario with its order, and the intake), the Dto becomes the
// contract model again (`toModel`, the demo flag the server's), and a command verb from the
// wire becomes the domain's command (`Command.toDomain`).
//
// Today the mapper in `ServerApi.Mappers.fs` rebuilds scenarios from the contract model and
// drops the ones whose order cannot be created, and `mapToShared` merges the answer into the
// request. Here nothing is dropped: an order that cannot be created is a `fromDto` error at
// the next step, and the way back is built from the Dto alone.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. A new src/Informedica.GenPRES.Server/ServerApi.Mappers.OrderContext.fs, after
//      ServerApi.Mappers.Patient.fs in the fsproj: the modules below under `Mappers`.
//   2. tests/Informedica.GenPRES.Server.Tests/MappersOrderContextTests.fs: the tests below,
//      which need tests/Informedica.GenORDER.Tests/Scenarios.fs linked into the server tests.
//
// Run from this directory: dotnet fsi MappersOrderContext.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"
#load "../../../tests/Informedica.GenORDER.Tests/Scenarios.fs"

open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Shared.Types
open ServerApi


/// The marked-up text the domain holds, from the items the client shows: bold between `#`,
/// italic between `|`, the rest as it is. The inverse of `Mappers.parseTextItem` on items
/// with text.
module TextItem =

    let render (items: TextItem[]) =
        items
        |> Array.map (
            function
            | Normal s -> s
            | Bold s -> $"#{s}#"
            | Italic s -> $"|{s}|"
        )
        |> String.concat ""


module OrderCategory =

    let toDomain =
        function
        | OrderCategory.Drug -> Informedica.GenOrder.Lib.Types.OrderCategory.Drug
        | OrderCategory.Nutrition c ->
            match c with
            | NutritionCategory.EnteralFeeding -> Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralFeeding
            | NutritionCategory.EnteralSupplement -> Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralSupplement
            | NutritionCategory.TPN -> Informedica.GenOrder.Lib.Types.NutritionCategory.TPN
            | NutritionCategory.Lipid -> Informedica.GenOrder.Lib.Types.NutritionCategory.Lipid
            | NutritionCategory.ElectrolyteGlucose ->
                Informedica.GenOrder.Lib.Types.NutritionCategory.ElectrolyteGlucose
            |> Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition


    let ofDomain =
        function
        | Informedica.GenOrder.Lib.Types.OrderCategory.Drug -> OrderCategory.Drug
        | Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition c ->
            match c with
            | Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralFeeding -> NutritionCategory.EnteralFeeding
            | Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralSupplement -> NutritionCategory.EnteralSupplement
            | Informedica.GenOrder.Lib.Types.NutritionCategory.TPN -> NutritionCategory.TPN
            | Informedica.GenOrder.Lib.Types.NutritionCategory.Lipid -> NutritionCategory.Lipid
            | Informedica.GenOrder.Lib.Types.NutritionCategory.ElectrolyteGlucose ->
                NutritionCategory.ElectrolyteGlucose
            |> OrderCategory.Nutrition


/// The order context mapper: the contract model to the domain's Dto and back. The Dto is the
/// plan context's, since the contract model carries the id, the category and the intake the
/// plan gave the context.
module OrderContextMapper =

    module TextBlockDto = Informedica.GenOrder.Lib.TextBlock.Dto
    module FilterDto = Informedica.GenOrder.Lib.Filter.Dto
    module ScenarioDto = Informedica.GenOrder.Lib.OrderScenario.Dto
    module ContextDto = Informedica.GenOrder.Lib.OrderContext.Dto
    module PlanContextDto = Informedica.GenOrder.Lib.PlanContext.Dto
    module TotalsDto = Informedica.GenOrder.Lib.Totals.Dto


    let doseType (dt: DoseType) =
        dt |> Mappers.mapFromSharedDoseTypeToOrderDoseType |> DoseTypeDto.toString


    /// The Dto's dose type string as the contract model's case; the Dto writes what it reads,
    /// so a string it does not read is a dose type that is none.
    let doseTypeBack (s: string) =
        s
        |> DoseTypeDto.fromString
        |> Result.defaultValue Informedica.GenForm.Lib.Types.NoDoseType
        |> Mappers.mapFromOrderDoseTypeToSharedDoseType


    let textBlock (tb: TextBlock) : TextBlockDto.Dto =
        match tb with
        | Valid items ->
            {
                Kind = "valid"
                Text = TextItem.render items
            }
        | Caution items ->
            {
                Kind = "caution"
                Text = TextItem.render items
            }
        | Warning items ->
            {
                Kind = "warning"
                Text = TextItem.render items
            }
        | Alert items ->
            {
                Kind = "alert"
                Text = TextItem.render items
            }


    /// The Dto's block as the contract model's: the kind as the Dto writes it, the text parsed
    /// into items; a kind the Dto does not write shows as valid.
    let textBlockBack (dto: TextBlockDto.Dto) : TextBlock =
        let items = dto.Text |> Mappers.parseTextItem

        match dto.Kind with
        | "caution" -> Caution items
        | "warning" -> Warning items
        | "alert" -> Alert items
        | _ -> Valid items


    let blocks (bs: TextBlock[][]) = bs |> Array.map (Array.map textBlock)

    let blocksBack (bs: TextBlockDto.Dto[][]) = bs |> Array.map (Array.map textBlockBack)


    let filter (f: Filter) : FilterDto.Dto =
        {
            Indications = f.Indications
            Generics = f.Generics
            Routes = f.Routes
            Forms = f.Forms
            DoseTypes = f.DoseTypes |> Array.map doseType
            Diluents = f.Diluents
            Components = f.Components
            Indication = f.Indication
            Generic = f.Generic
            Route = f.Route
            Form = f.Form
            DoseType = f.DoseType |> Option.map doseType
            Diluent = f.Diluent
            SelectedComponents = f.SelectedComponents
        }


    let filterBack (dto: FilterDto.Dto) : Filter =
        {
            Indications = dto.Indications
            Generics = dto.Generics
            Routes = dto.Routes
            Forms = dto.Forms
            DoseTypes = dto.DoseTypes |> Array.map doseTypeBack
            Diluents = dto.Diluents
            Components = dto.Components
            Indication = dto.Indication
            Generic = dto.Generic
            Route = dto.Route
            Form = dto.Form
            DoseType = dto.DoseType |> Option.map doseTypeBack
            Diluent = dto.Diluent
            SelectedComponents = dto.SelectedComponents
        }


    /// A scenario with its order as the domain's Dto, numbered by its place, the text blocks
    /// as marked-up text. Total: an order that cannot be created is `fromDto`'s to report.
    let scenario no (sc: OrderScenario) : ScenarioDto.Dto =
        {
            No = no
            Name = sc.Name
            Indication = sc.Indication
            Form = sc.Form
            Route = sc.Route
            DoseType = sc.DoseType |> doseType
            Diluent = sc.Diluent
            Component = sc.Component
            Item = sc.Item
            Diluents = sc.Diluents
            Components = sc.Components
            Items = sc.Items
            Prescription = sc.Prescription |> blocks
            Preparation = sc.Preparation |> blocks
            Administration = sc.Administration |> blocks
            Order = sc.Order |> Mappers.Order.mapFromSharedToOrder
            UseAdjust = sc.UseAdjust
            UseRenalRule = sc.UseRenalRule
            RenalRule = sc.RenalRule
            ProductsIds = sc.ProductIds
        }


    let scenarioBack (dto: ScenarioDto.Dto) : OrderScenario =
        Shared.Models.OrderScenario.create
            dto.Indication
            dto.Name
            dto.Form
            dto.Route
            (dto.DoseType |> doseTypeBack)
            dto.Diluent
            dto.Component
            dto.Item
            dto.Diluents
            dto.Components
            dto.Items
            (dto.Prescription |> blocksBack)
            (dto.Preparation |> blocksBack)
            (dto.Administration |> blocksBack)
            (dto.Order |> Mappers.Order.mapFromOrderToShared dto.Items)
            dto.UseAdjust
            dto.UseRenalRule
            dto.RenalRule
            dto.ProductsIds


    /// The totals as the domain's Dto: each line as marked-up text, an empty one as none.
    let totals (t: Totals) : TotalsDto.Dto =
        let line (items: TextItem[]) =
            if items |> Array.isEmpty then
                None
            else
                Some(TextItem.render items)

        {
            Volume = t.Volume |> line
            Energy = t.Energy |> line
            Protein = t.Protein |> line
            Carbohydrate = t.Carbohydrate |> line
            Fat = t.Fat |> line
            Sodium = t.Sodium |> line
            Potassium = t.Potassium |> line
            Chloride = t.Chloride |> line
            Calcium = t.Calcium |> line
            Phosphate = t.Phosphate |> line
            Magnesium = t.Magnesium |> line
            Iron = t.Iron |> line
            VitaminD = t.VitaminD |> line
            Ethanol = t.Ethanol |> line
            Propyleenglycol = t.Propyleenglycol |> line
            BenzylAlcohol = t.BenzylAlcohol |> line
            BoricAcid = t.BoricAcid |> line
        }


    let totalsBack (dto: TotalsDto.Dto) : Totals =
        let line = Option.map Mappers.parseTextItem >> Option.defaultValue [||]

        {
            Volume = dto.Volume |> line
            Energy = dto.Energy |> line
            Protein = dto.Protein |> line
            Carbohydrate = dto.Carbohydrate |> line
            Fat = dto.Fat |> line
            Sodium = dto.Sodium |> line
            Potassium = dto.Potassium |> line
            Chloride = dto.Chloride |> line
            Calcium = dto.Calcium |> line
            Phosphate = dto.Phosphate |> line
            Magnesium = dto.Magnesium |> line
            Iron = dto.Iron |> line
            VitaminD = dto.VitaminD |> line
            Ethanol = dto.Ethanol |> line
            Propyleenglycol = dto.Propyleenglycol |> line
            BenzylAlcohol = dto.BenzylAlcohol |> line
            BoricAcid = dto.BoricAcid |> line
        }


    /// Total: the contract model's order context as the plan context's Dto, nothing dropped.
    let ofModel (ctx: OrderContext) : PlanContextDto.Dto =
        {
            Id = ctx.Id
            Category = ctx.Category |> OrderCategory.toDomain |> OrderCategoryDto.toString
            Context =
                {
                    Filter = ctx.Filter |> filter
                    Patient = ctx.Patient |> Patient.ofModel
                    Scenarios = ctx.Scenarios |> Array.mapi scenario
                }
            Intake = ctx.Intake |> totals
        }


    /// The plan context's Dto as the contract model's order context, built from the Dto alone;
    /// the demo flag is the server's. A category string the Dto does not write shows as a drug.
    let toModel (demo: bool) (dto: PlanContextDto.Dto) : OrderContext =
        {
            Id = dto.Id
            Category =
                dto.Category
                |> OrderCategoryDto.fromString
                |> Result.defaultValue Informedica.GenOrder.Lib.Types.OrderCategory.Drug
                |> OrderCategory.ofDomain
            DemoVersion = demo
            Filter = dto.Context.Filter |> filterBack
            Patient = dto.Context.Patient |> Patient.toModel
            Scenarios = dto.Context.Scenarios |> Array.map scenarioBack
            Intake = dto.Intake |> totalsBack
        }


/// The command verb from the wire as the domain's command over a context: the table the
/// order context service keeps today, moved here so that the port receives the domain
/// command.
module Command =

    module Domain = Informedica.GenOrder.Lib.OrderContext


    let toDomain (cmd: Shared.Api.OrderContextCommand) : Informedica.GenOrder.Lib.Types.OrderContext -> Domain.Command =
        match cmd with
        | Shared.Api.OrderContextCommand.UpdateOrderContext -> Domain.UpdateOrderContext
        | Shared.Api.OrderContextCommand.SelectOrderScenario -> Domain.SelectOrderScenario
        | Shared.Api.OrderContextCommand.UpdateOrderScenario -> Domain.UpdateOrderScenario
        | Shared.Api.OrderContextCommand.ResetOrderScenario -> Domain.ResetOrderScenario
        // Frequency property commands
        | Shared.Api.OrderContextCommand.DecreaseScheduleFrequencyProperty -> Domain.DecreaseScheduleFrequencyProperty
        | Shared.Api.OrderContextCommand.IncreaseScheduleFrequencyProperty -> Domain.IncreaseScheduleFrequencyProperty
        | Shared.Api.OrderContextCommand.SetMinScheduleFrequencyProperty -> Domain.SetMinScheduleFrequencyProperty
        | Shared.Api.OrderContextCommand.SetMaxScheduleFrequencyProperty -> Domain.SetMaxScheduleFrequencyProperty
        | Shared.Api.OrderContextCommand.SetMedianScheduleFrequencyProperty ->
            Domain.SetMedianScheduleFrequencyProperty
        // DoseQuantity property commands
        | Shared.Api.OrderContextCommand.DecreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
            fun ctx -> Domain.DecreaseOrderableDoseQuantityProperty(ctx, ntimes, useCalc)
        | Shared.Api.OrderContextCommand.IncreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
            fun ctx -> Domain.IncreaseOrderableDoseQuantityProperty(ctx, ntimes, useCalc)
        | Shared.Api.OrderContextCommand.SetMinOrderableDoseQuantityProperty ->
            Domain.SetMinOrderableDoseQuantityProperty
        | Shared.Api.OrderContextCommand.SetMaxOrderableDoseQuantityProperty ->
            Domain.SetMaxOrderableDoseQuantityProperty
        | Shared.Api.OrderContextCommand.SetMedianOrderableDoseQuantityProperty ->
            Domain.SetMedianOrderableDoseQuantityProperty
        // DoseRate property commands
        | Shared.Api.OrderContextCommand.DecreaseOrderableDoseRateProperty(ntimes, useCalc) ->
            fun ctx -> Domain.DecreaseOrderableDoseRateProperty(ctx, ntimes, useCalc)
        | Shared.Api.OrderContextCommand.IncreaseOrderableDoseRateProperty(ntimes, useCalc) ->
            fun ctx -> Domain.IncreaseOrderableDoseRateProperty(ctx, ntimes, useCalc)
        | Shared.Api.OrderContextCommand.SetMinOrderableDoseRateProperty -> Domain.SetMinOrderableDoseRateProperty
        | Shared.Api.OrderContextCommand.SetMaxOrderableDoseRateProperty -> Domain.SetMaxOrderableDoseRateProperty
        | Shared.Api.OrderContextCommand.SetMedianOrderableDoseRateProperty ->
            Domain.SetMedianOrderableDoseRateProperty
        // Component Quantity property commands
        | Shared.Api.OrderContextCommand.DecreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc) ->
            fun ctx -> Domain.DecreaseComponentQuantityProperty(ctx, cmp, ntimes, useCalc)
        | Shared.Api.OrderContextCommand.IncreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc) ->
            fun ctx -> Domain.IncreaseComponentQuantityProperty(ctx, cmp, ntimes, useCalc)
        | Shared.Api.OrderContextCommand.SetMinComponentOrderableQuantityProperty cmp ->
            fun ctx -> Domain.SetMinComponentQuantityProperty(ctx, cmp)
        | Shared.Api.OrderContextCommand.SetMaxComponentOrderableQuantityProperty cmp ->
            fun ctx -> Domain.SetMaxComponentQuantityProperty(ctx, cmp)
        | Shared.Api.OrderContextCommand.SetMedianComponentOrderableQuantityProperty cmp ->
            fun ctx -> Domain.SetMedianComponentQuantityProperty(ctx, cmp)


// ---------------------------------------------------------------------------
// Tests, for tests/Informedica.GenPRES.Server.Tests/MappersOrderContextTests.fs
// ---------------------------------------------------------------------------

module MappersOrderContextTests =

    open Expecto
    open Expecto.Flip
    // after Expecto, whose FocusState has a Normal case too
    open Shared.Types

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
                        |> Expect.equal "marked-up text" [| [| Informedica.GenOrder.Lib.Types.Valid "paracetamol #240 mg# 3 x/dag" |] |]

                        pc.Intake.Energy |> Expect.equal "the intake" (Some "#50 kcal#/dag")
                    | Error e -> failtest $"no plan context: %A{e}"
                }

                test "text: rendering and parsing are inverse on items with text; a blank item is dropped" {
                    let items = [| Normal "a "; Bold "b"; Normal " and "; Italic "c"; Normal " d" |]
                    items |> TextItem.render |> Expect.equal "rendered" "a #b# and |c| d"
                    items |> TextItem.render |> Mappers.parseTextItem |> Expect.equal "parsed back" items

                    [| Bold "b"; Normal " "; Italic "c" |]
                    |> TextItem.render
                    |> Mappers.parseTextItem
                    |> Expect.equal "the blank item between gone" [| Bold "b"; Italic "c" |]
                }

                test "every command verb becomes the domain command over the context given" {
                    let ctx: Informedica.GenOrder.Lib.Types.OrderContext =
                        {
                            Filter = context.Filter |> OrderContextMapper.filter |> Filter.Dto.fromDto |> Result.defaultWith (fun e -> failtest $"{e}")
                            Patient = Informedica.GenForm.Lib.Patient.patient
                            Scenarios = [||]
                        }

                    let verbs =
                        [
                            Shared.Api.OrderContextCommand.UpdateOrderContext, Domain.UpdateOrderContext ctx
                            Shared.Api.OrderContextCommand.SelectOrderScenario, Domain.SelectOrderScenario ctx
                            Shared.Api.OrderContextCommand.UpdateOrderScenario, Domain.UpdateOrderScenario ctx
                            Shared.Api.OrderContextCommand.ResetOrderScenario, Domain.ResetOrderScenario ctx
                            Shared.Api.OrderContextCommand.DecreaseScheduleFrequencyProperty, Domain.DecreaseScheduleFrequencyProperty ctx
                            Shared.Api.OrderContextCommand.IncreaseScheduleFrequencyProperty, Domain.IncreaseScheduleFrequencyProperty ctx
                            Shared.Api.OrderContextCommand.SetMinScheduleFrequencyProperty, Domain.SetMinScheduleFrequencyProperty ctx
                            Shared.Api.OrderContextCommand.SetMaxScheduleFrequencyProperty, Domain.SetMaxScheduleFrequencyProperty ctx
                            Shared.Api.OrderContextCommand.SetMedianScheduleFrequencyProperty, Domain.SetMedianScheduleFrequencyProperty ctx
                            Shared.Api.OrderContextCommand.DecreaseOrderableDoseQuantityProperty(2, true), Domain.DecreaseOrderableDoseQuantityProperty(ctx, 2, true)
                            Shared.Api.OrderContextCommand.IncreaseOrderableDoseQuantityProperty(3, false), Domain.IncreaseOrderableDoseQuantityProperty(ctx, 3, false)
                            Shared.Api.OrderContextCommand.SetMinOrderableDoseQuantityProperty, Domain.SetMinOrderableDoseQuantityProperty ctx
                            Shared.Api.OrderContextCommand.SetMaxOrderableDoseQuantityProperty, Domain.SetMaxOrderableDoseQuantityProperty ctx
                            Shared.Api.OrderContextCommand.SetMedianOrderableDoseQuantityProperty, Domain.SetMedianOrderableDoseQuantityProperty ctx
                            Shared.Api.OrderContextCommand.DecreaseOrderableDoseRateProperty(1, true), Domain.DecreaseOrderableDoseRateProperty(ctx, 1, true)
                            Shared.Api.OrderContextCommand.IncreaseOrderableDoseRateProperty(1, false), Domain.IncreaseOrderableDoseRateProperty(ctx, 1, false)
                            Shared.Api.OrderContextCommand.SetMinOrderableDoseRateProperty, Domain.SetMinOrderableDoseRateProperty ctx
                            Shared.Api.OrderContextCommand.SetMaxOrderableDoseRateProperty, Domain.SetMaxOrderableDoseRateProperty ctx
                            Shared.Api.OrderContextCommand.SetMedianOrderableDoseRateProperty, Domain.SetMedianOrderableDoseRateProperty ctx
                            Shared.Api.OrderContextCommand.DecreaseComponentOrderableQuantityProperty("cmp", 2, true), Domain.DecreaseComponentQuantityProperty(ctx, "cmp", 2, true)
                            Shared.Api.OrderContextCommand.IncreaseComponentOrderableQuantityProperty("cmp", 2, false), Domain.IncreaseComponentQuantityProperty(ctx, "cmp", 2, false)
                            Shared.Api.OrderContextCommand.SetMinComponentOrderableQuantityProperty "cmp", Domain.SetMinComponentQuantityProperty(ctx, "cmp")
                            Shared.Api.OrderContextCommand.SetMaxComponentOrderableQuantityProperty "cmp", Domain.SetMaxComponentQuantityProperty(ctx, "cmp")
                            Shared.Api.OrderContextCommand.SetMedianComponentOrderableQuantityProperty "cmp", Domain.SetMedianComponentQuantityProperty(ctx, "cmp")
                        ]

                    verbs |> List.length |> Expect.equal "every case of the wire's union" 24

                    for verb, expected in verbs do
                        Command.toDomain verb ctx |> Expect.equal $"{verb}" expected
                }
            ]


MappersOrderContextTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore
