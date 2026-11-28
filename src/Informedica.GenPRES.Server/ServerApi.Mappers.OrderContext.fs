namespace ServerApi

open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
// Opened last, so an unqualified case name refers to the contract case.
open Informedica.GenPRES.Shared.Types


/// Maps an order context of the contract to the plan context Dto of GenORDER and back.
module OrderContextMapper =

    /// Writes text items as marked-up text: bold between # and italic between |. The markup has
    /// no escape, so an item cannot contain # or |. The items come from Mappers.parseTextItem,
    /// so they never do.
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


    module Category =

        let toDomain =
            function
            | OrderCategory.Medication -> Informedica.GenOrder.Lib.Types.OrderCategory.Medication
            | OrderCategory.Nutrition c ->
                match c with
                | NutritionCategory.EnteralFeeding -> Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralFeeding
                | NutritionCategory.EnteralSupplement ->
                    Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralSupplement
                | NutritionCategory.TPN -> Informedica.GenOrder.Lib.Types.NutritionCategory.TPN
                | NutritionCategory.Lipid -> Informedica.GenOrder.Lib.Types.NutritionCategory.Lipid
                | NutritionCategory.ElectrolyteGlucose ->
                    Informedica.GenOrder.Lib.Types.NutritionCategory.ElectrolyteGlucose
                |> Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition


        let ofDomain =
            function
            | Informedica.GenOrder.Lib.Types.OrderCategory.Medication -> OrderCategory.Medication
            | Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition c ->
                match c with
                | Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralFeeding -> NutritionCategory.EnteralFeeding
                | Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralSupplement ->
                    NutritionCategory.EnteralSupplement
                | Informedica.GenOrder.Lib.Types.NutritionCategory.TPN -> NutritionCategory.TPN
                | Informedica.GenOrder.Lib.Types.NutritionCategory.Lipid -> NutritionCategory.Lipid
                | Informedica.GenOrder.Lib.Types.NutritionCategory.ElectrolyteGlucose ->
                    NutritionCategory.ElectrolyteGlucose
                |> OrderCategory.Nutrition


    module TextBlockDto = Informedica.GenOrder.Lib.TextBlock.Dto
    module FilterDto = Informedica.GenOrder.Lib.Filter.Dto
    module ScenarioDto = Informedica.GenOrder.Lib.OrderScenario.Dto
    module ContextDto = Informedica.GenOrder.Lib.OrderContext.Dto
    module PlanContextDto = Informedica.GenOrder.Lib.PlanContext.Dto
    module TotalsDto = Informedica.GenOrder.Lib.Totals.Dto
    module GenOrderTypes = Informedica.GenOrder.Lib.Types


    let doseType (dt: DoseType) =
        dt |> Mappers.mapFromSharedDoseTypeToOrderDoseType |> DoseTypeDto.toString


    /// Maps a dose type string to a dose type. An unknown string gives NoDoseType.
    let doseTypeBack (s: string) =
        s
        |> DoseTypeDto.fromString
        |> Result.defaultValue Informedica.GenForm.Lib.Types.NoDoseType
        |> Mappers.mapFromOrderDoseTypeToSharedDoseType


    /// Maps a component selection to the Dto. All components gives None.
    let componentSelection =
        function
        | ComponentSelection.AllComponents -> None
        | ComponentSelection.Selected cs -> cs |> List.toArray |> Some


    let componentSelectionBack =
        function
        | None -> ComponentSelection.AllComponents
        | Some cs -> cs |> Array.toList |> ComponentSelection.Selected


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


    /// Maps a text block Dto to a text block. Throws InvalidOperationException for an unknown
    /// kind, because showing the block as valid would hide a warning.
    let textBlockBack (dto: TextBlockDto.Dto) : TextBlock =
        let items = dto.Text |> Mappers.parseTextItem

        match dto.Kind with
        | "valid" -> Valid items
        | "caution" -> Caution items
        | "warning" -> Warning items
        | "alert" -> Alert items
        | kind -> invalidOp $"The text block kind '%s{kind}' is unknown"


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
            DoseRuleComponents = f.DoseRuleComponents
            Indication = f.Indication
            Generic = f.Generic
            Route = f.Route
            Form = f.Form
            DoseType = f.DoseType |> Option.map doseType
            Diluent = f.Diluent
            SelectedComponents = f.SelectedComponents |> componentSelection
            PickedProducts = f.PickedProducts
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
            DoseRuleComponents = dto.DoseRuleComponents
            Indication = dto.Indication
            Generic = dto.Generic
            Route = dto.Route
            Form = dto.Form
            DoseType = dto.DoseType |> Option.map doseTypeBack
            Diluent = dto.Diluent
            SelectedComponents = dto.SelectedComponents |> componentSelectionBack
            PickedProducts = dto.PickedProducts
        }


    /// Maps a scenario to the scenario Dto with number no. The order is not checked here;
    /// OrderScenario.Dto.fromDto returns OrderNotCreated for an invalid order.
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
            FitsOnlyWithoutDivisible = sc.FitsOnlyWithoutDivisible
            RulesNoLongerApply = sc.RulesNoLongerApply
            NotRecalculated = sc.NotRecalculated
            ProductsIds = sc.ProductIds
            Access =
                sc.Access
                |> Option.map Patient.access
                |> Option.defaultValue AnyAccess
                |> AccessDevice.toString
        }


    let scenarioBack (dto: ScenarioDto.Dto) : OrderScenario =
        Informedica.GenPRES.Shared.Models.OrderScenario.create
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
            dto.FitsOnlyWithoutDivisible
            dto.RulesNoLongerApply
            dto.NotRecalculated
            dto.ProductsIds
            (dto.Access
             |> Option.ofObj
             |> Option.bind AccessDevice.tryFromString
             |> Option.bind Patient.accessBack)
        // the scenario Dto stores neither the reconstitution nor the predilution, so both come from the order
        |> fun sc ->
            let ord = dto.Order |> Order.Dto.fromDto |> Result.toOption

            { sc with
                Reconstitution = ord |> Option.bind Order.reconstitutionText
                Predilution = ord |> Option.bind Order.predilutionText
            }


    /// Maps the totals to the Dto, each total as its text block and a state from the kind of the block: within the
    /// norm for a Valid block, outside it for any other block.
    let totals (t: Totals) : TotalsDto.Dto =
        // stop-gap until the contract carries the state of a total, issue #2092
        let state b =
            let state =
                match b with
                | Valid _ -> GenOrderTypes.TotalState.WithinNorm
                | Caution _
                | Warning _
                | Alert _ -> GenOrderTypes.TotalState.OutsideNorm

            state |> TotalsDto.stateToString

        let block =
            Option.map (fun b ->
                {
                    TotalsDto.Text = b |> textBlock
                    TotalsDto.State = b |> state
                }
            )

        {
            Volume = t.Volume |> block
            Energy = t.Energy |> block
            Protein = t.Protein |> block
            Carbohydrate = t.Carbohydrate |> block
            Fat = t.Fat |> block
            Sodium = t.Sodium |> block
            Potassium = t.Potassium |> block
            Chloride = t.Chloride |> block
            Calcium = t.Calcium |> block
            Phosphate = t.Phosphate |> block
            Magnesium = t.Magnesium |> block
            Iron = t.Iron |> block
            VitaminD = t.VitaminD |> block
            Ethanol = t.Ethanol |> block
            Propyleenglycol = t.Propyleenglycol |> block
            BenzylAlcohol = t.BenzylAlcohol |> block
            BoricAcid = t.BoricAcid |> block
        }


    /// Maps the totals Dto to the totals, each total as its text block; the state of a total is left out until the
    /// contract carries it. Throws InvalidOperationException for a block of an unknown kind, as textBlockBack does.
    let totalsBack (dto: TotalsDto.Dto) : Totals =
        let block = Option.map (fun (t: TotalsDto.TotalDto) -> t.Text |> textBlockBack)

        {
            Volume = dto.Volume |> block
            Energy = dto.Energy |> block
            Protein = dto.Protein |> block
            Carbohydrate = dto.Carbohydrate |> block
            Fat = dto.Fat |> block
            Sodium = dto.Sodium |> block
            Potassium = dto.Potassium |> block
            Chloride = dto.Chloride |> block
            Calcium = dto.Calcium |> block
            Phosphate = dto.Phosphate |> block
            Magnesium = dto.Magnesium |> block
            Iron = dto.Iron |> block
            VitaminD = dto.VitaminD |> block
            Ethanol = dto.Ethanol |> block
            Propyleenglycol = dto.Propyleenglycol |> block
            BenzylAlcohol = dto.BenzylAlcohol |> block
            BoricAcid = dto.BoricAcid |> block
        }


    /// Maps an order context to the plan context Dto.
    let ofModel (ctx: OrderContext) : PlanContextDto.Dto =
        {
            Id = ctx.Id
            Category = ctx.Category |> Category.toDomain |> OrderCategoryDto.toString
            Context =
                {
                    Filter = ctx.Filter |> filter
                    PatientData = ctx.PatientData |> Patient.ofModel
                    Scenarios = ctx.Scenarios |> Array.mapi scenario
                    Argumentation = ctx.Argumentation
                }
            Totals = ctx.Totals |> totals
            LinkedTo = ctx.LinkedTo
        }


    /// Maps a plan context Dto to an order context with the demo flag of the server. An unknown
    /// category gives Medication.
    let toModel (demo: bool) (dto: PlanContextDto.Dto) : OrderContext =
        {
            Id = dto.Id
            Category =
                dto.Category
                |> OrderCategoryDto.fromString
                |> Result.defaultValue Informedica.GenOrder.Lib.Types.OrderCategory.Medication
                |> Category.ofDomain
            DemoVersion = demo
            Filter = dto.Context.Filter |> filterBack
            PatientData = dto.Context.PatientData |> Patient.toModel
            Scenarios = dto.Context.Scenarios |> Array.map scenarioBack
            Totals = dto.Totals |> totalsBack
            Argumentation = dto.Context.Argumentation
            LinkedTo = dto.LinkedTo
        }


    /// Maps an order view command of the contract to an order context command of GenORDER.
    module Command =

        module Domain = Informedica.GenOrder.Lib.OrderContext
        module Ctx = Informedica.GenPRES.Shared.Models.OrderContext


        /// Maps a filter field to GenORDER.
        let field (f: Ctx.FilterField) =
            match f with
            | Ctx.Indication -> FilterField.Indication
            | Ctx.Generic -> FilterField.Generic
            | Ctx.Route -> FilterField.Route
            | Ctx.Form -> FilterField.Form
            | Ctx.DoseType -> FilterField.DoseType


        /// Returns the command that sets the nth value of a target and the command that
        /// recalculates it. The second is None for a value that is never recalculated.
        let propertyCommands (t: Ctx.Target) =
            match t with
            | Ctx.Target.Schedule ScheduleProperty.Frequency -> SetNthScheduleFrequency, None
            | Ctx.Target.Schedule ScheduleProperty.Time -> SetNthScheduleTime, Some ReCalcScheduleTime
            | Ctx.Target.Orderable OrderableProperty.Quantity -> SetNthOrderableQuantity, Some ReCalcOrderableQuantity
            | Ctx.Target.Orderable OrderableProperty.DoseQuantity -> SetNthOrderableDoseQuantity, None
            | Ctx.Target.Orderable OrderableProperty.DoseRate -> SetNthOrderableDoseRate, None
            | Ctx.Target.Component(c, ComponentProperty.OrderableQuantity) ->
                (fun n -> SetNthComponentOrderableQuantity(c, n)), None
            | Ctx.Target.Component(c, ComponentProperty.DoseQuantityAdjust) ->
                (fun n -> SetNthComponentDoseQuantityAdjust(c, n)), Some(ReCalcComponentDoseQuantityAdjust c)
            | Ctx.Target.Item(c, i, p) ->
                match p with
                | ItemProperty.DoseQuantity ->
                    (fun n -> SetNthItemDoseQuantity(c, i, n)), Some(ReCalcItemDoseQuantity(c, i))
                | ItemProperty.DoseQuantityAdjust ->
                    (fun n -> SetNthItemDoseQuantityAdjust(c, i, n)), Some(ReCalcItemDoseQuantityAdjust(c, i))
                | ItemProperty.DosePerTime ->
                    (fun n -> SetNthItemDosePerTime(c, i, n)), Some(ReCalcItemDosePerTime(c, i))
                | ItemProperty.DosePerTimeAdjust ->
                    (fun n -> SetNthItemDosePerTimeAdjust(c, i, n)), Some(ReCalcItemDosePerTimeAdjust(c, i))
                | ItemProperty.DoseRate -> (fun n -> SetNthItemDoseRate(c, i, n)), Some(ReCalcItemDoseRate(c, i))
                | ItemProperty.DoseRateAdjust ->
                    (fun n -> SetNthItemDoseRateAdjust(c, i, n)), Some(ReCalcItemDoseRateAdjust(c, i))
                | ItemProperty.ComponentConcentration -> (fun n -> SetNthItemComponentConcentration(c, i, n)), None
                | ItemProperty.OrderableConcentration ->
                    (fun n -> SetNthItemOrderableConcentration(c, i, n)), Some(ReCalcItemOrderableConcentration(c, i))
                | ItemProperty.OrderableQuantity ->
                    (fun n -> SetNthItemOrderableQuantity(c, i, n)), Some(ReCalcItemOrderableQuantity(c, i))


        /// Maps a seed source to GenORDER.
        let seedSource (source: SeedSource) =
            match source with
            | SeedSource.Url -> Informedica.GenOrder.Lib.Types.SeedSource.Url
            | SeedSource.Formulary -> Informedica.GenOrder.Lib.Types.SeedSource.Formulary
            | SeedSource.Parenteralia -> Informedica.GenOrder.Lib.Types.SeedSource.Parenteralia
            | SeedSource.Reload -> Informedica.GenOrder.Lib.Types.SeedSource.Reload


        /// Maps an order view command to the GenORDER command for an order context of the
        /// category. Throws ArgumentException for SetArgumentationProperty, which the server
        /// handles itself.
        let toDomain
            category
            (cmd: Informedica.GenPRES.Shared.Api.OrderViewCommand)
            : Informedica.GenOrder.Lib.Types.OrderContext -> Domain.Command
            =
            let category = category |> Category.toDomain

            let setNth t n =
                fun ctx -> Domain.SetNthOrderValue(ctx, (fst (propertyCommands t)) n)

            let reCalc t =
                fun ctx -> Domain.ReCalcOrderValue(ctx, snd (propertyCommands t))

            match cmd with
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.ResetOrderScenario -> Domain.ResetOrderScenario
            // Frequency property commands
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.DecreaseScheduleFrequencyProperty ->
                Domain.DecreaseScheduleFrequencyProperty
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.IncreaseScheduleFrequencyProperty ->
                Domain.IncreaseScheduleFrequencyProperty
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMinScheduleFrequencyProperty ->
                Domain.SetMinScheduleFrequencyProperty
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMaxScheduleFrequencyProperty ->
                Domain.SetMaxScheduleFrequencyProperty
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMedianScheduleFrequencyProperty ->
                Domain.SetMedianScheduleFrequencyProperty
            // DoseQuantity property commands
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.DecreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
                fun ctx -> Domain.DecreaseOrderableDoseQuantityProperty(ctx, ntimes, useCalc)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.IncreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
                fun ctx -> Domain.IncreaseOrderableDoseQuantityProperty(ctx, ntimes, useCalc)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMinOrderableDoseQuantityProperty ->
                Domain.SetMinOrderableDoseQuantityProperty
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMaxOrderableDoseQuantityProperty ->
                Domain.SetMaxOrderableDoseQuantityProperty
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMedianOrderableDoseQuantityProperty ->
                Domain.SetMedianOrderableDoseQuantityProperty
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetOrderableDoseQuantityPercProperty perc ->
                fun ctx -> Domain.SetOrderableDoseQuantityPercProperty(ctx, perc)
            // DoseRate property commands
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.DecreaseOrderableDoseRateProperty(ntimes, useCalc) ->
                fun ctx -> Domain.DecreaseOrderableDoseRateProperty(ctx, ntimes, useCalc)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.IncreaseOrderableDoseRateProperty(ntimes, useCalc) ->
                fun ctx -> Domain.IncreaseOrderableDoseRateProperty(ctx, ntimes, useCalc)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMinOrderableDoseRateProperty ->
                Domain.SetMinOrderableDoseRateProperty
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMaxOrderableDoseRateProperty ->
                Domain.SetMaxOrderableDoseRateProperty
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMedianOrderableDoseRateProperty ->
                Domain.SetMedianOrderableDoseRateProperty
            // Component Quantity property commands
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.DecreaseComponentOrderableQuantityProperty(cmp,
                                                                                                         ntimes,
                                                                                                         useCalc) ->
                fun ctx -> Domain.DecreaseComponentQuantityProperty(ctx, cmp, ntimes, useCalc)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.IncreaseComponentOrderableQuantityProperty(cmp,
                                                                                                         ntimes,
                                                                                                         useCalc) ->
                fun ctx -> Domain.IncreaseComponentQuantityProperty(ctx, cmp, ntimes, useCalc)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMinComponentOrderableQuantityProperty cmp ->
                fun ctx -> Domain.SetMinComponentQuantityProperty(ctx, cmp)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMaxComponentOrderableQuantityProperty cmp ->
                fun ctx -> Domain.SetMaxComponentQuantityProperty(ctx, cmp)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetMedianComponentOrderableQuantityProperty cmp ->
                fun ctx -> Domain.SetMedianComponentQuantityProperty(ctx, cmp)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthFilterProperty(f, n) ->
                fun ctx -> Domain.ChangeFilter(ctx, category, field f, Some n)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.ClearFilterProperty f ->
                fun ctx -> Domain.ChangeFilter(ctx, category, field f, None)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.ClearAllFilterProperty -> Domain.ClearAllFilter
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthDiluentProperty n ->
                fun ctx -> Domain.ChangeFilter(ctx, category, FilterField.Diluent, Some n)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.ClearDiluentProperty ->
                fun ctx -> Domain.ChangeFilter(ctx, category, FilterField.Diluent, None)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthComponentsProperty ns ->
                fun ctx -> Domain.SetNthComponents(ctx, ns)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SelectNthOrderScenario n ->
                fun ctx -> Domain.SelectNthOrderScenario(ctx, n)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthScheduleProperty(prop, n) ->
                setNth (Ctx.Target.Schedule prop) n
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.ReCalcScheduleProperty prop ->
                reCalc (Ctx.Target.Schedule prop)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthOrderableProperty(prop, n) ->
                setNth (Ctx.Target.Orderable prop) n
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.ReCalcOrderableProperty prop ->
                reCalc (Ctx.Target.Orderable prop)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthComponentProperty(cmp, prop, n) ->
                setNth (Ctx.Target.Component(cmp, prop)) n
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.ReCalcComponentProperty(cmp, prop) ->
                reCalc (Ctx.Target.Component(cmp, prop))
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetNthItemProperty(cmp, itm, prop, n) ->
                setNth (Ctx.Target.Item(cmp, itm, prop)) n
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.ReCalcItemProperty(cmp, itm, prop) ->
                reCalc (Ctx.Target.Item(cmp, itm, prop))
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SeedFilter(source, ind, gen, rte, frm, dt) ->
                fun ctx ->
                    let dt = dt |> Option.map Mappers.mapFromSharedDoseTypeToOrderDoseType
                    Domain.SeedFilter(ctx, seedSource source, ind, gen, rte, frm, dt)
            | Informedica.GenPRES.Shared.Api.OrderViewCommand.SetArgumentationProperty _ ->
                invalidArg (nameof cmd) "the argumentation is written by the server, without the domain"


    /// Returns the message for a link error.
    let linkWords =
        function
        | LinkError.NoSuchMainOrder main -> $"The main order %s{main} is not in the plan"
        | LinkError.MainOrderLinked main -> $"The main order %s{main} is itself linked to another order"
        | LinkError.NotAllowed generic -> $"No link rule links this order to the main order %s{generic}"
        | LinkError.MainOrderRequired -> "This order is only prescribed with its main order"


    /// Returns the message for a Dto error.
    let words =
        function
        | DtoError.PatientData s -> s
        | DtoError.PatientDataNotEnough e -> e |> Patient.notEnoughMessage
        | DtoError.OrderNotCreated m -> $"De order kon niet worden gemaakt: %s{m}"
        | DtoError.UnknownDoseType s -> $"Onbekend doseertype: %s{s}"
        | DtoError.UnknownTextKind s -> $"Onbekende tekstsoort: %s{s}"
        | DtoError.UnknownTotalState s -> $"Onbekende totaalstatus: %s{s}"
        | DtoError.UnknownCategory s -> $"Onbekende categorie: %s{s}"
        | DtoError.Missing f -> $"Ontbreekt: %s{f}"
        | DtoError.Unreadable m -> $"Onleesbare tekst: %s{m}"
        | DtoError.NotANumber(p, s) -> $"Geen getal in %s{p}: %s{s}"
        | DtoError.UnknownUnit(p, u) -> $"Onbekende eenheid in %s{p}: %s{u}"
        | DtoError.WrongUnit(p, r) -> $"Verkeerde eenheid in %s{p}: %s{r}"
        | DtoError.UnknownOrderType(p, s) -> $"Onbekend ordertype in %s{p}: %s{s}"
        | DtoError.NotMadeOf(p, id) -> $"Product %s{id} in %s{p} past niet bij de stoffen"
        | DtoError.BrokenLink(id, e) -> $"Context %s{id}: %s{linkWords e}"
