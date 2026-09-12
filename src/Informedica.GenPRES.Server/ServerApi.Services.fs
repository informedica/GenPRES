namespace ServerApi


module FormularyService =

    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Shared.Types
    open Shared


    let mapFormularyToFilter (form: Formulary) =
        { Filter.doseFilter with
            Generic = form.Generic
            Indication = form.Indication
            Route = form.Route
            Form = form.Form
            DoseType = form.DoseType |> Option.map Mappers.mapFromSharedDoseTypeToOrderDoseType
            Patient =
                form.Patient
                |> Option.map Mappers.mapFromSharedPatient
                |> Option.defaultValue Patient.patient
        }
        |> Filter.calcPMAge


    let selectIfOne sel xs =
        match sel, xs with
        | None, [| x |] -> Some x
        | _ -> sel


    /// Pure helpers for classifying and aggregating raw G-Standaard dose-check
    /// result strings into colored TextBlocks. Exposed for unit testing.
    module DoseCheck =

        // `Shared.Utils.String.split` (in scope via `open Shared`) returns string[].

        /// True if the line is a frequency-mismatch entry emitted by Check.fs.
        /// Expected raw shape: "{target}\t{route}\t{patientCategory}\t{message}".
        let isFrequency (s: string) =
            match s |> String.split "\t" with
            | [| _; _; _; msg |] -> msg.Contains "frequenties"
            | _ -> false

        /// Format a raw check line for display. `singleRule` drops the
        /// patient-category field when only one dose rule is in scope.
        let formatLine (singleRule: bool) (s: string) =
            match s |> String.split "\t" with
            | [| s1; _; p; s2 |] ->
                if singleRule then
                    $"%s{s1} %s{s2}"
                else
                    $"%s{s1} %s{p} %s{s2}"
            | _ -> s

        /// Map a GenFORM dose-check Severity to the Formulary's colored TextBlock
        /// case. This is what gives the UI its advisory-vs-absolute grading:
        ///   - OverAbsolute                         → red (Alert)
        ///   - AdvisoryOverNorm/UnderNorm/Frequency → orange (Warning)
        ///   - UnitMismatch/Incomparable/NotComparable/NoMonitor → blue (Caution)
        ///   - Within                               → green (Valid)
        let severityWrap (sev: Check.Severity) : TextItem[] -> TextBlock =
            match sev with
            | Check.Within -> Valid
            | Check.OverAbsolute -> Alert
            | Check.AdvisoryOverNorm
            | Check.UnderNorm
            | Check.FrequencyMismatch -> Warning
            | Check.UnitMismatch
            | Check.IncomparableUnits
            | Check.NotComparable
            | Check.NoMonitoring -> Caution

        /// Build the colored TextBlock[] for the Formulary's DoseCheck field from
        /// the graded dose-check signals.
        ///   - empty input            → single green "Ok!" block
        ///   - only "no monitoring"   → blue (Caution) info blocks; signals
        ///                              no G-Standaard rule exists for the
        ///                              selection
        ///   - otherwise              → one block per signal, colored by its
        ///                              Severity (advisory = orange, absolute =
        ///                              red, etc.)
        /// `NoMonitoring` sentinels are dropped once real violations exist so a
        /// non-violation isn't painted as a violation.
        let build
            (parseTextItem: string -> TextItem[])
            (singleRule: bool)
            (signals: (Check.Severity * string)[])
            : TextBlock[]
            =
            // Sentinel identity is carried by the Severity, not the message text.
            let violations = signals |> Array.filter (fst >> (<>) Check.NoMonitoring)

            // "no monitoring" only: signals is non-empty but every signal is a
            // NoMonitoring sentinel. Distinct from the pure pass-through
            // (signals = [||]) which keeps the green "Ok!" badge.
            let allNoMonitoring = violations |> Array.isEmpty && signals |> Array.isEmpty |> not

            if signals |> Array.isEmpty then
                [| "Ok!" |> parseTextItem |> Valid |]
            elif allNoMonitoring then
                signals
                |> Array.map (fun (_, s) -> s |> formatLine singleRule |> parseTextItem |> Caution)
            else
                violations
                |> Array.map (fun (sev, s) -> s |> formatLine singleRule |> parseTextItem |> severityWrap sev)


    let checkDoseRules provider pat (dsrs: DoseRule[]) =
        // GStand dose rules now come from the Resources provider as a
        // function-valued resource, not built ad hoc from the route mapping.
        let gstand = Api.getGStandProvider provider

        let empt, rs =
            dsrs
            |> Array.distinctBy (fun dr ->
                dr.Generic |> Generic.toString, dr.Generic.Form |> PharmaceuticalForm.toString, dr.Route, dr.DoseType
            )
            |> Array.map (Check.checkDoseRuleWithProvider gstand pat)
            |> Array.partition (fun c -> c.didPass |> Array.isEmpty && c.didNotPass |> Array.isEmpty)

        rs
        |> Array.filter (_.didNotPass >> Array.isEmpty >> not)
        |> Array.collect _.signals
        |> Array.filter (fun (sev, s) -> sev <> Check.Within && s |> String.notEmpty)
        |> Array.distinct
        |> function
            | [||] ->
                [|
                    for e in empt do
                        Check.NoMonitoring,
                        $"geen doseer bewaking gevonden voor {e.doseRule.Generic |> Generic.toString}"
                |]
                |> Array.distinct

            | xs -> xs


    let get provider (form: Formulary) =
        let filter = form |> mapFormularyToFilter

        $"""

Formulary filter:
Patient: {filter.Patient |> Patient.toString}
Indication: {filter.Indication |> Option.defaultValue ""}
Generic: {filter.Generic |> Option.defaultValue ""}
Route: {filter.Route |> Option.defaultValue ""}
Shape: {filter.Form |> Option.defaultValue ""}
DoseType : {filter.DoseType |> Option.map DoseType.toDescription |> Option.defaultValue ""}

"""
        |> writeDebugMessage

        let dsrs = Formulary.getDoseRules provider filter

        writeDebugMessage $"Found: {dsrs |> Array.length} formulary dose rules"

        let form =
            { form with
                Generics = dsrs |> DoseRule.generics
                Indications = dsrs |> DoseRule.indications
                Routes = dsrs |> DoseRule.routes
                Forms = dsrs |> DoseRule.forms
                DoseTypes =
                    dsrs
                    |> DoseRule.doseTypes
                    |> Array.map Mappers.mapFromOrderDoseTypeToSharedDoseType
                PatientCategories = dsrs |> DoseRule.patientCategories
            }
            |> fun form ->
                { form with
                    Generic = form.Generics |> selectIfOne form.Generic
                    Indication = form.Indications |> selectIfOne form.Indication
                    Route = form.Routes |> selectIfOne form.Route
                    Form = form.Forms |> selectIfOne form.Form
                    DoseType = form.DoseTypes |> selectIfOne form.DoseType
                    PatientCategory = form.PatientCategories |> selectIfOne form.PatientCategory
                }
            |> fun form ->
                match form.Generic, form.Indication, form.Route with
                | Some _, Some _, Some _ ->
                    writeDebugMessage $"start checking {dsrs |> Array.length} rules"

                    let singleRule = dsrs |> Array.length = 1

                    let doseCheck =
                        dsrs
                        |> checkDoseRules provider filter.Patient
                        |> DoseCheck.build Mappers.parseTextItem singleRule

                    writeDebugMessage $"finished checking {dsrs |> Array.length} rules"

                    { form with
                        Markdown =
                            dsrs
                            |> DoseRule.Print.toMarkdown (Informedica.GenForm.Lib.Api.getNKFLinkProvider provider)
                        DoseCheck = doseCheck
                    }

                | _ ->
                    { form with
                        Markdown = ""
                        DoseCheck = [||]
                    }

        $"""

Formulary:
Patients: {form.PatientCategories |> Array.length}
Indication: {form.Indications |> Array.length}
Generic: {form.Generics |> Array.length}
Route: {form.Routes |> Array.length}
Shapes: {form.Forms |> Array.length}
DoseTypes: {form.DoseTypes |> Array.length}

"""
        |> writeDebugMessage

        Ok form


module ParenteraliaService =

    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib

    type Parenteralia = Shared.Types.Parenteralia


    let get provider (par: Parenteralia) : Result<Parenteralia, string> =
        writeInfoMessage $"getting parenteralia for {par.Generic}"

        let srs = Formulary.getSolutionRules provider par.Generic par.Form par.Route

        let gens = srs |> SolutionRule.generics
        let shps = srs |> SolutionRule.forms
        let rtes = srs |> SolutionRule.routes

        { par with
            Generics = gens
            Forms = shps
            Routes = rtes
            Generic =
                if gens |> Array.length = 1 then
                    Some gens[0]
                else
                    par.Generic
            Form = if shps |> Array.length = 1 then Some shps[0] else par.Form
            Route = if rtes |> Array.length = 1 then Some rtes[0] else par.Route

            Markdown =
                if par.Generic |> Option.isNone then
                    ""
                else
                    srs |> SolutionRule.Print.toMarkdown ""
        }
        |> Ok


module OrderService =

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenOrder.Lib

    open Shared.Types


    let getTotals (totals: Informedica.GenForm.Lib.Types.Data.TotalsData[]) age wghtInGram (ords: Order[]) =
        let wghtInKg =
            wghtInGram
            |> Option.map BigRational.fromInt
            |> Option.map (ValueUnit.singleWithUnit Units.Weight.gram)
            |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)

        let age =
            age
            |> Option.map BigRational.fromInt
            |> Option.map (ValueUnit.singleWithUnit Units.Time.day)

        ords
        |> Array.map Mappers.Order.mapFromSharedToOrder
        |> Totals.getTotals totals age wghtInKg
        |> Mappers.mapToTotals


module OrderContextService =

    open Informedica.Utils.Lib
    open ConsoleWriter.NewLineTime
    open Informedica.GenForm.Lib
    open Informedica.GenOrder.Lib

    open Shared
    open Shared.Types
    open Mappers

    module GenOrderContext = OrderContext


    let setDemoVersion ctx =
        { ctx with
            DemoVersion =
                Env.getItem "GENPRES_PROD"
                |> Option.map (fun v -> v <> "1")
                |> Option.defaultValue true
        }


    let updateIntake (totals: TotalsData[]) (ctx: OrderContext) =
        { ctx with
            Intake =
                let w = ctx.Patient |> Models.Patient.getWeight |> Option.map int
                let a = ctx.Patient |> Models.Patient.getAgeInDays |> Option.map int

                ctx.Scenarios |> Array.map _.Order |> OrderService.getTotals totals a w
        }


    let private extractServerCtx =
        function
        | GenOrderContext.UpdateOrderContext ctx
        | GenOrderContext.SelectOrderScenario ctx
        | GenOrderContext.UpdateOrderScenario ctx
        | GenOrderContext.ResetOrderScenario ctx
        | GenOrderContext.ReloadResources ctx
        // Frequency property commands
        | GenOrderContext.DecreaseScheduleFrequencyProperty ctx
        | GenOrderContext.IncreaseScheduleFrequencyProperty ctx
        | GenOrderContext.SetMinScheduleFrequencyProperty ctx
        | GenOrderContext.SetMaxScheduleFrequencyProperty ctx
        | GenOrderContext.SetMedianScheduleFrequencyProperty ctx
        // DoseQuantity property commands
        | GenOrderContext.SetMinOrderableDoseQuantityProperty ctx
        | GenOrderContext.SetMaxOrderableDoseQuantityProperty ctx
        | GenOrderContext.SetMedianOrderableDoseQuantityProperty ctx
        // DoseRate property commands
        | GenOrderContext.SetMinOrderableDoseRateProperty ctx
        | GenOrderContext.SetMaxOrderableDoseRateProperty ctx
        | GenOrderContext.SetMedianOrderableDoseRateProperty ctx -> ctx
        | GenOrderContext.DecreaseOrderableDoseQuantityProperty(ctx, _, _)
        | GenOrderContext.IncreaseOrderableDoseQuantityProperty(ctx, _, _)
        | GenOrderContext.DecreaseOrderableDoseRateProperty(ctx, _, _)
        | GenOrderContext.IncreaseOrderableDoseRateProperty(ctx, _, _) -> ctx
        | GenOrderContext.DecreaseComponentQuantityProperty(ctx, _, _, _)
        | GenOrderContext.IncreaseComponentQuantityProperty(ctx, _, _, _) -> ctx
        | GenOrderContext.SetMinComponentQuantityProperty(ctx, _)
        | GenOrderContext.SetMaxComponentQuantityProperty(ctx, _)
        | GenOrderContext.SetMedianComponentQuantityProperty(ctx, _) -> ctx


    let evaluate
        logger
        (provider: Resources.IResourceProvider)
        (cmd: Api.OrderContextCommand)
        (ctx: OrderContext)
        : Result<OrderContext, string[]>
        =
        let map = mapToShared ctx >> updateIntake (provider.GetTotals()) >> setDemoVersion

        let pat = ctx.Patient |> mapFromSharedPatient |> Patient.calcPMAge

        let filter = ctx.Filter

        $"""

OrderContext filter:
Patient: {pat |> Patient.toString}
Indication: {filter.Indication |> Option.defaultValue ""}
Generic: {filter.Generic |> Option.defaultValue ""}
Route: {filter.Route |> Option.defaultValue ""}
Shape: {filter.Form |> Option.defaultValue ""}
DoseType : {filter.DoseType
            |> Option.map Models.DoseType.doseTypeToString
            |> Option.defaultValue ""}

"""
        |> writeDebugMessage


        let toServerCmd serverCtx =
            match cmd with
            | Api.UpdateOrderContext -> serverCtx |> GenOrderContext.UpdateOrderContext
            | Api.SelectOrderScenario -> serverCtx |> GenOrderContext.SelectOrderScenario
            | Api.UpdateOrderScenario -> serverCtx |> GenOrderContext.UpdateOrderScenario
            | Api.ResetOrderScenario -> serverCtx |> GenOrderContext.ResetOrderScenario
            // Frequency property commands
            | Api.DecreaseScheduleFrequencyProperty -> serverCtx |> GenOrderContext.DecreaseScheduleFrequencyProperty
            | Api.IncreaseScheduleFrequencyProperty -> serverCtx |> GenOrderContext.IncreaseScheduleFrequencyProperty
            | Api.SetMinScheduleFrequencyProperty -> serverCtx |> GenOrderContext.SetMinScheduleFrequencyProperty
            | Api.SetMaxScheduleFrequencyProperty -> serverCtx |> GenOrderContext.SetMaxScheduleFrequencyProperty
            | Api.SetMedianScheduleFrequencyProperty -> serverCtx |> GenOrderContext.SetMedianScheduleFrequencyProperty
            // DoseQuantity property commands
            | Api.DecreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
                GenOrderContext.DecreaseOrderableDoseQuantityProperty(serverCtx, ntimes, useCalc)
            | Api.IncreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
                GenOrderContext.IncreaseOrderableDoseQuantityProperty(serverCtx, ntimes, useCalc)
            | Api.SetMinOrderableDoseQuantityProperty -> GenOrderContext.SetMinOrderableDoseQuantityProperty serverCtx
            | Api.SetMaxOrderableDoseQuantityProperty -> GenOrderContext.SetMaxOrderableDoseQuantityProperty serverCtx
            | Api.SetMedianOrderableDoseQuantityProperty ->
                GenOrderContext.SetMedianOrderableDoseQuantityProperty serverCtx
            // DoseRate property commands
            | Api.DecreaseOrderableDoseRateProperty(ntimes, useCalc) ->
                GenOrderContext.DecreaseOrderableDoseRateProperty(serverCtx, ntimes, useCalc)
            | Api.IncreaseOrderableDoseRateProperty(ntimes, useCalc) ->
                GenOrderContext.IncreaseOrderableDoseRateProperty(serverCtx, ntimes, useCalc)
            | Api.SetMinOrderableDoseRateProperty -> serverCtx |> GenOrderContext.SetMinOrderableDoseRateProperty
            | Api.SetMaxOrderableDoseRateProperty -> serverCtx |> GenOrderContext.SetMaxOrderableDoseRateProperty
            | Api.SetMedianOrderableDoseRateProperty -> serverCtx |> GenOrderContext.SetMedianOrderableDoseRateProperty
            // Component Quantity property commands
            | Api.DecreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc) ->
                GenOrderContext.DecreaseComponentQuantityProperty(serverCtx, cmp, ntimes, useCalc)
            | Api.IncreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc) ->
                GenOrderContext.IncreaseComponentQuantityProperty(serverCtx, cmp, ntimes, useCalc)
            | Api.SetMinComponentOrderableQuantityProperty cmp ->
                GenOrderContext.SetMinComponentQuantityProperty(serverCtx, cmp)
            | Api.SetMaxComponentOrderableQuantityProperty cmp ->
                GenOrderContext.SetMaxComponentQuantityProperty(serverCtx, cmp)
            | Api.SetMedianComponentOrderableQuantityProperty cmp ->
                GenOrderContext.SetMedianComponentQuantityProperty(serverCtx, cmp)

        try
            ctx
            |> mapFromShared logger provider pat
            |> toServerCmd
            |> GenOrderContext.logOrderContext logger "start eval"
            |> GenOrderContext.evaluate logger provider
            |> Result.map (GenOrderContext.logOrderContext logger "finish eval" >> extractServerCtx >> map)
            |> Result.mapError (
                List.map OrderLogging.formatOrderMessage
                >> String.concat "\n"
                >> Array.singleton
            )
        with e ->
            writeErrorMessage $"errored:\n{e}"
            Error [| e.Message |]


module NutritionPlanService =

    open Shared
    open Shared.Types

    type NutritionDoseRuleSet =
        {
            Label: string
            Indications: string[]
            Generics: string[]
            DoseTypes: (string * string)[]
        }

    let tpnDoseRuleSet =
        {
            Label = "Totale Parenterale Voeding"
            Indications =
                [|
                    "Standaard Totale Parenterale Voeding"
                    "Variabele Totale Parenterale Voeding"
                    "Neonatale Parenterale Voeding"
                    "Totale Parenterale Voeding"
                |]
            Generics =
                [|
                    "Primene"
                    "NICU Mix"
                    "Samenstelling B"
                    "Samenstelling C"
                    "Samenstelling D"
                    "Samenstelling E"
                    "Numeta G13%E 2CZ"
                    "Numeta G13%E 3CZ"
                    "Numeta G16%E 2CZ"
                    "Numeta G16%E 3CZ"
                    "Numeta G19%E 2CZ"
                    "Numeta G19%E 3CZ"
                |]
            DoseTypes = [||]
        }


    let enteralFeedingDoseRuleSet =
        {
            Label = "Enterale Voeding"
            Indications = [| "Enterale voeding" |]
            Generics =
                [|
                    "Infatrini"
                    "Nutrini"
                    "Nutrini Energy"
                    "Nutrini Energy Multi Fibre"
                    "Nutrini Multi Fibre"
                    "Nutrison"
                    "Nutrison Energy"
                    "Nutrison Energy Multi Fibre"
                    "Nutrison Multi Fibre"
                    "Nutrison Protein Plus"
                    "Nutrison Protein Plus Multi Fibre"
                    "Peptisorb"
                    "Peptisorb Plus"
                    "Moedermelk"
                    "Nutrilon Premature"
                    "Nutrilon Nenatal Start"
                    "Nutrilon Nenatal 1"
                |]
            DoseTypes = [||]
        }


    let enteralSupplementDoseRuleSet =
        {
            Label = "Enteraal Supplement"
            Indications = [| "Enterale toevoeging" |]
            Generics =
                [|
                    "Calogen neutraal pdr"
                    "Fantomalt pdr"
                    "Hero Baby 1 NS Comfort pdr"
                    "Hero Baby 1 NS Pep pdr"
                    "Hero Baby 1 NS Standaard pdr"
                    "Hero Baby 2 NS Comfort pdr"
                    "Hero Baby 2 NS Pep pdr"
                    "Hero Baby 2 NS Standaard pdr"
                    "Liquigen pdr"
                    "Neocate Junior pdr"
                    "Neocate LCP pdr"
                    "Nutramigen 1 LGG pdr"
                    "Nutramigen 2 LGG pdr"
                    "Nutrilon 1 pdr"
                    "Nutrilon Hypoallergeen 1 pdr"
                    "Nutrilon Nenatal 1 pdr"
                    "Nutrilon Nenatal BMF pdr"
                    "Nutrilon Nenatal Protein Fortifier pdr"
                    "Nutrilon Nenatal Start pdr"
                    "Nutrilon Pepti 1 pdr"
                    "Nutrilon Pepti Junior pdr"
                    "Nutrison Advanced Peptisorb pdr"
                    "Nutriton pdr"
                |]
            DoseTypes = [||]
        }


    let lipidDoseRuleSet =
        {
            Label = "Vetten"
            Indications = [| "Parenterale vetten" |]
            Generics = [| "Intralipid 20%"; "SMOFlipid 20%" |]
            DoseTypes = [||]
        }


    let electrolyteGlucoseDoseRuleSet =
        {
            Label = "Elektrolyten/Glucose"
            Indications = [| "Parenterale suppletie" |]
            Generics =
                [|
                    "NaCl 0,9%"
                    "NaCl 3%"
                    "Glucose 5%"
                    "Glucose 10%"
                    "Glucose 20%"
                    "Glucose 50%"
                    "calciumglubionat/calciumgluconaat"
                    "KCl 7,4%"
                    "KCl"
                    "NaCl"
                    "magnesiumsulfaat"
                    "fosfaat"
                    "calciumgluconaat"
                |]
            DoseTypes = [||]
        }


    let getDoseRuleSet =
        function
        | NutritionCategory.EnteralFeeding -> enteralFeedingDoseRuleSet
        | NutritionCategory.EnteralSupplement -> enteralSupplementDoseRuleSet
        | NutritionCategory.TPN -> tpnDoseRuleSet
        | NutritionCategory.Lipid -> lipidDoseRuleSet
        | NutritionCategory.ElectrolyteGlucose -> electrolyteGlucoseDoseRuleSet


    /// Discovers available filter options for a given OrderContext.
    /// Evaluates the context via the OrderContext port and intersects
    /// the resolved options with the configured values.
    let discoverFilterOptions (orderCtxPort: OrderContextPort) ctx =
        async {
            let! result = orderCtxPort.evaluate Api.UpdateOrderContext ctx

            return
                match result with
                | Ok resolved ->
                    { resolved with
                        Filter =
                            { resolved.Filter with
                                Indications =
                                    resolved.Filter.Indications
                                    |> Array.filter (fun i -> ctx.Filter.Indications |> Array.exists ((=) i))
                                Generics =
                                    resolved.Filter.Generics
                                    |> Array.filter (fun g -> ctx.Filter.Generics |> Array.exists ((=) g))
                                DoseTypes =
                                    resolved.Filter.DoseTypes
                                    |> Array.filter (fun dt -> ctx.Filter.DoseTypes |> Array.exists ((=) dt))
                            }
                    }
                    |> Some
                | Error _ -> None
        }


    /// Filters a resolved OrderContext's filter arrays against the configured
    /// dose rule set. If a configured array is non-empty, only matching values
    /// are kept; if empty, no restriction is applied.
    let filterByDoseRuleSet (drs: NutritionDoseRuleSet) (resolved: OrderContext) =
        { resolved with
            Filter =
                { resolved.Filter with
                    Indications =
                        if drs.Indications |> Array.isEmpty then
                            resolved.Filter.Indications
                        else
                            resolved.Filter.Indications
                            |> Array.filter (fun i -> drs.Indications |> Array.contains i)
                    Generics =
                        if drs.Generics |> Array.isEmpty then
                            resolved.Filter.Generics
                        else
                            resolved.Filter.Generics
                            |> Array.filter (fun g -> drs.Generics |> Array.contains g)
                    DoseTypes = resolved.Filter.DoseTypes
                }
        }


/// The one plan, nutrition included: the nutrition workbenches produce orders by category, and
/// a context narrowed to exactly one scenario has that scenario among the plan's orders, so one
/// signature covers it. Totals are computed once, over the orders.
module PlanService =

    open Shared
    open Shared.Types


    /// The order a nutrition context contributes to the plan: its scenario, once the context
    /// is narrowed to exactly one; nothing while it holds several candidates or none.
    let contribution (nc: NutritionContext) =
        nc.OrderContext.Scenarios |> Array.tryExactlyOne


    /// The plan's orders with one context's contribution replaced: the one it contributed
    /// before goes out by order id, the one it contributes now comes in; the same order in
    /// place, a different one at the end.
    let withContribution (before: OrderScenario option) (after: OrderScenario option) (scenarios: OrderScenario[]) =
        match before, after with
        | Some old, Some sc when old.Order.Id = sc.Order.Id ->
            scenarios |> Array.map (fun s -> if s.Order.Id = sc.Order.Id then sc else s)
        | _ ->
            let without =
                match before with
                | None -> scenarios
                | Some old -> scenarios |> Array.filter (fun s -> s.Order.Id <> old.Order.Id)

            match after with
            | None -> without
            | Some sc -> Array.append without [| sc |]


    /// A derived view (the filter, the selection) follows a replaced order only where it held
    /// the old one: replaced in place, or gone with it; never gains an order on its own.
    let private follow (before: OrderScenario option) (after: OrderScenario option) (view: OrderScenario[]) =
        match before with
        | Some old when view |> Array.exists (fun s -> s.Order.Id = old.Order.Id) ->
            view |> withContribution before after
        | _ -> view


    /// The plan's orders with one contribution replaced, and the filter and the selection
    /// following it, so that a replaced order keeps counting in the totals and a removed one
    /// is nowhere.
    let withOrders (before: OrderScenario option) (after: OrderScenario option) (plan: OrderPlan) =
        { plan with
            Scenarios = plan.Scenarios |> withContribution before after
            Filtered = plan.Filtered |> follow before after
            Selected =
                match plan.Selected, before with
                | Some sel, Some old when sel.Order.Id = old.Order.Id -> after
                | sel, _ -> sel
        }


    /// The resolved order context into the context named, filtered to the category's dose rule
    /// set, and its contribution into the plan's orders.
    let updateContext id (resolved: OrderContext) (plan: OrderPlan) =
        match plan.NutritionContexts |> Array.tryFind (fun nc -> nc.Id = id) with
        | None -> Error [| $"The plan holds no nutrition context %s{id}" |]
        | Some nc ->
            let drs = NutritionPlanService.getDoseRuleSet nc.Category

            let updated =
                { nc with OrderContext = resolved |> NutritionPlanService.filterByDoseRuleSet drs }

            { plan with
                NutritionContexts = plan.NutritionContexts |> Array.map (fun c -> if c.Id = id then updated else c)
            }
            |> withOrders (contribution nc) (contribution updated)
            |> Ok


    /// The context removed, and every supplement with a feeding; each takes its order with it.
    let removeContext id (plan: OrderPlan) =
        let removed = plan.NutritionContexts |> Array.tryFind (fun nc -> nc.Id = id)

        let cascade =
            removed
            |> Option.map (fun nc -> nc.Category = NutritionCategory.EnteralFeeding)
            |> Option.defaultValue false

        let goes (nc: NutritionContext) =
            nc.Id = id || (cascade && nc.Category = NutritionCategory.EnteralSupplement)

        let gone, kept = plan.NutritionContexts |> Array.partition goes

        gone
        |> Array.fold (fun p nc -> p |> withOrders (contribution nc) None) { plan with NutritionContexts = kept }


    /// The plan with its totals recomputed over its orders: the filtered ones, by order id, when
    /// a filter is set, else all of them.
    /// The orders named removed from the plan: each with the workbench that contributed it, a
    /// feeding with its supplements and theirs, the rest by order id; the filter and the
    /// selection follow.
    let removeOrders (ids: string[]) (plan: OrderPlan) =
        let contributed (nc: NutritionContext) =
            contribution nc |> Option.exists (fun sc -> ids |> Array.contains sc.Order.Id)

        let plan =
            plan.NutritionContexts
            |> Array.filter contributed
            |> Array.fold (fun (p: OrderPlan) (nc: NutritionContext) -> p |> removeContext nc.Id) plan

        ids
        |> Array.fold
            (fun (p: OrderPlan) id ->
                match p.Scenarios |> Array.tryFind (fun sc -> sc.Order.Id = id) with
                | Some sc -> p |> withOrders (Some sc) None
                | None -> p
            )
            plan


    let recalculate (totals: Informedica.GenForm.Lib.Types.Data.TotalsData[]) (plan: OrderPlan) =
        { plan with
            Totals =
                let w = plan.Patient |> Models.Patient.getWeight |> Option.map int
                let a = plan.Patient |> Models.Patient.getAgeInDays |> Option.map int

                // by order id: a scenario replaced in Scenarios still counts under its filter
                let scs =
                    if plan.Filtered |> Array.isEmpty then
                        plan.Scenarios
                    else
                        plan.Scenarios
                        |> Array.filter (fun sc -> plan.Filtered |> Array.exists (fun f -> f.Order.Id = sc.Order.Id))

                scs |> Array.map _.Order |> OrderService.getTotals totals a w
        }


    /// A command into the nutrition context named, or into the selected scenario when none is;
    /// `recalc` ends the answer in its totals (the adapter's `recalculate` over the provider's
    /// totals data).
    let navigate
        (recalc: OrderPlan -> OrderPlan)
        (orderCtxPort: OrderContextPort)
        (plan: OrderPlan)
        (contextId: string option)
        (ctxCmd: Api.OrderContextCommand)
        (ctx: OrderContext)
        =
        async {
            match contextId with
            // the selected scenario re-evaluated: the plan's copy replaced by order id, the
            // selection on the result; an evaluation that fails is the answer, not the plan as it was
            | None ->
                let! result = orderCtxPort.evaluate ctxCmd ctx

                return
                    result
                    |> Result.map (fun evaluated ->
                        match evaluated.Scenarios |> Array.tryExactlyOne with
                        | None -> { plan with Selected = None }
                        | Some sc ->
                            let before = plan.Scenarios |> Array.tryFind (fun s -> s.Order.Id = sc.Order.Id)

                            { (match before with
                               | Some _ -> plan |> withOrders before (Some sc)
                               | None -> plan) with
                                Selected = Some sc
                            }
                    )
                    |> Result.map recalc
            | Some id ->
                let! result = orderCtxPort.evaluate ctxCmd ctx

                return
                    result
                    |> Result.bind (fun resolved -> plan |> updateContext id resolved)
                    |> Result.map recalc
        }


    /// A nutrition context for the category, its filter discovered, appended to the plan with
    /// whatever it contributes.
    let addContext
        (recalc: OrderPlan -> OrderPlan)
        (orderCtxPort: OrderContextPort)
        (plan: OrderPlan)
        (category: NutritionCategory)
        =
        async {
            let drs = NutritionPlanService.getDoseRuleSet category

            let ctx =
                Models.OrderContext.empty
                |> Models.OrderContext.setPatient plan.Patient
                |> fun c ->
                    { c with
                        Filter =
                            { c.Filter with
                                Indications = drs.Indications
                                Generics = drs.Generics
                            }
                    }

            let! discovered = NutritionPlanService.discoverFilterOptions orderCtxPort ctx

            return
                match discovered with
                | Some resolved ->
                    let id = System.Guid.NewGuid().ToString()
                    let nc = Models.NutritionContext.create id drs.Label category true resolved

                    { plan with NutritionContexts = Array.append plan.NutritionContexts [| nc |] }
                    |> withOrders None (contribution nc)
                    |> recalc
                    |> Ok
                | None ->
                    Error
                        [|
                            "Could not discover filter options for nutrition context"
                        |]
        }
