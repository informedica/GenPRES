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
            | Api.OrderContextCommand.UpdateOrderContext -> serverCtx |> GenOrderContext.UpdateOrderContext
            | Api.OrderContextCommand.SelectOrderScenario -> serverCtx |> GenOrderContext.SelectOrderScenario
            | Api.OrderContextCommand.UpdateOrderScenario -> serverCtx |> GenOrderContext.UpdateOrderScenario
            | Api.OrderContextCommand.ResetOrderScenario -> serverCtx |> GenOrderContext.ResetOrderScenario
            // Frequency property commands
            | Api.OrderContextCommand.DecreaseScheduleFrequencyProperty ->
                serverCtx |> GenOrderContext.DecreaseScheduleFrequencyProperty
            | Api.OrderContextCommand.IncreaseScheduleFrequencyProperty ->
                serverCtx |> GenOrderContext.IncreaseScheduleFrequencyProperty
            | Api.OrderContextCommand.SetMinScheduleFrequencyProperty ->
                serverCtx |> GenOrderContext.SetMinScheduleFrequencyProperty
            | Api.OrderContextCommand.SetMaxScheduleFrequencyProperty ->
                serverCtx |> GenOrderContext.SetMaxScheduleFrequencyProperty
            | Api.OrderContextCommand.SetMedianScheduleFrequencyProperty ->
                serverCtx |> GenOrderContext.SetMedianScheduleFrequencyProperty
            // DoseQuantity property commands
            | Api.OrderContextCommand.DecreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
                GenOrderContext.DecreaseOrderableDoseQuantityProperty(serverCtx, ntimes, useCalc)
            | Api.OrderContextCommand.IncreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
                GenOrderContext.IncreaseOrderableDoseQuantityProperty(serverCtx, ntimes, useCalc)
            | Api.OrderContextCommand.SetMinOrderableDoseQuantityProperty ->
                GenOrderContext.SetMinOrderableDoseQuantityProperty serverCtx
            | Api.OrderContextCommand.SetMaxOrderableDoseQuantityProperty ->
                GenOrderContext.SetMaxOrderableDoseQuantityProperty serverCtx
            | Api.OrderContextCommand.SetMedianOrderableDoseQuantityProperty ->
                GenOrderContext.SetMedianOrderableDoseQuantityProperty serverCtx
            // DoseRate property commands
            | Api.OrderContextCommand.DecreaseOrderableDoseRateProperty(ntimes, useCalc) ->
                GenOrderContext.DecreaseOrderableDoseRateProperty(serverCtx, ntimes, useCalc)
            | Api.OrderContextCommand.IncreaseOrderableDoseRateProperty(ntimes, useCalc) ->
                GenOrderContext.IncreaseOrderableDoseRateProperty(serverCtx, ntimes, useCalc)
            | Api.OrderContextCommand.SetMinOrderableDoseRateProperty ->
                serverCtx |> GenOrderContext.SetMinOrderableDoseRateProperty
            | Api.OrderContextCommand.SetMaxOrderableDoseRateProperty ->
                serverCtx |> GenOrderContext.SetMaxOrderableDoseRateProperty
            | Api.OrderContextCommand.SetMedianOrderableDoseRateProperty ->
                serverCtx |> GenOrderContext.SetMedianOrderableDoseRateProperty
            // Component Quantity property commands
            | Api.OrderContextCommand.DecreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc) ->
                GenOrderContext.DecreaseComponentQuantityProperty(serverCtx, cmp, ntimes, useCalc)
            | Api.OrderContextCommand.IncreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc) ->
                GenOrderContext.IncreaseComponentQuantityProperty(serverCtx, cmp, ntimes, useCalc)
            | Api.OrderContextCommand.SetMinComponentOrderableQuantityProperty cmp ->
                GenOrderContext.SetMinComponentQuantityProperty(serverCtx, cmp)
            | Api.OrderContextCommand.SetMaxComponentOrderableQuantityProperty cmp ->
                GenOrderContext.SetMaxComponentQuantityProperty(serverCtx, cmp)
            | Api.OrderContextCommand.SetMedianComponentOrderableQuantityProperty cmp ->
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
            let! result = orderCtxPort.evaluate Api.OrderContextCommand.UpdateOrderContext ctx

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


    let contribution = Models.OrderContext.contribution


    /// The resolved order context into the context named, keeping the id and the category the
    /// plan gave it and filtered to its category's dose rule set. The plan's orders follow, since
    /// they are what its contexts contribute.
    let updateContext id (resolved: OrderContext) (plan: OrderPlan) =
        match plan.OrderContexts |> Array.tryFind (fun c -> c.Id = id) with
        | None -> Error [| $"The plan holds no context %s{id}" |]
        | Some ctx ->
            let updated =
                match Models.OrderContext.nutritionCategory ctx with
                | Some category ->
                    resolved
                    |> NutritionPlanService.filterByDoseRuleSet (NutritionPlanService.getDoseRuleSet category)
                | None -> resolved

            let updated =
                { updated with
                    Id = ctx.Id
                    Category = ctx.Category
                }

            { plan with OrderContexts = plan.OrderContexts |> Array.map (fun c -> if c.Id = id then updated else c) }
            |> Ok


    let private holds category (plan: OrderPlan) =
        plan.OrderContexts
        |> Array.exists (fun c -> c.Category = OrderCategory.Nutrition category)


    /// The context removed, and every enteral supplement with a feeding: the plan holds one
    /// feeding at most and a supplement only under it, so the feeding's supplements are all of
    /// them. Each takes its order with it, and leaves the filter.
    let removeContext id (plan: OrderPlan) =
        let removed = plan.OrderContexts |> Array.tryFind (fun c -> c.Id = id)

        let cascade =
            removed
            |> Option.exists (fun c -> c.Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding)

        let goes (c: OrderContext) =
            c.Id = id
            || (cascade
                && c.Category = OrderCategory.Nutrition NutritionCategory.EnteralSupplement)

        let gone, kept = plan.OrderContexts |> Array.partition goes
        let goneIds = gone |> Array.map _.Id

        { plan with
            OrderContexts = kept
            Filtered = plan.Filtered |> Array.filter (fun f -> goneIds |> Array.contains f |> not)
        }


    /// The contexts named removed, every kind, each with its order; a feeding takes its
    /// supplements with it.
    let removeContexts (ids: string[]) (plan: OrderPlan) =
        ids |> Array.fold (fun p id -> p |> removeContext id) plan


    /// The orders named removed: each with the context that contributes it.
    let removeOrders (ids: string[]) (plan: OrderPlan) =
        plan.OrderContexts
        |> Array.filter (fun c -> contribution c |> Option.exists (fun sc -> ids |> Array.contains sc.Order.Id))
        |> Array.map _.Id
        |> fun contextIds -> plan |> removeContexts contextIds


    /// The prescribing workbench into the plan as a drug context with a minted id, its one
    /// scenario the order it contributes. Refused when the workbench is not narrowed to one
    /// scenario, and when the plan already holds that order: the signing challenge would refuse
    /// the plan later, so it is said now.
    let addOrder (newId: unit -> string) (ctx: OrderContext) (plan: OrderPlan) =
        match contribution ctx with
        | None ->
            Error
                [|
                    $"The workbench holds %i{ctx.Scenarios.Length} candidates, not one order"
                |]
        | Some sc when Models.OrderPlan.orders plan |> Array.exists (fun s -> s.Order.Id = sc.Order.Id) ->
            Error [| "The plan already holds this order" |]
        | Some _ ->
            let added =
                { ctx with
                    Id = newId ()
                    Category = OrderCategory.Drug
                }

            { plan with OrderContexts = Array.append plan.OrderContexts [| added |] } |> Ok


    /// The plan opened on a signed version: the contexts as they were, nothing evaluated, so the
    /// pick lists, the candidates and the stepped values are what was signed. The patient with no
    /// contexts is the empty plan.
    let openWith (pat: Patient) (contexts: OrderContext[]) : OrderPlan = Models.OrderPlan.create pat contexts


    /// The plan with its totals recomputed over the orders of the contexts the filter keeps, all
    /// of them when it is empty.
    let recalculate (totals: Informedica.GenForm.Lib.Types.Data.TotalsData[]) (plan: OrderPlan) =
        { plan with
            Totals =
                let w = plan.Patient |> Models.Patient.getWeight |> Option.map int
                let a = plan.Patient |> Models.Patient.getAgeInDays |> Option.map int

                plan
                |> Models.OrderPlan.filtered
                |> Array.choose contribution
                |> Array.map _.Order
                |> OrderService.getTotals totals a w
        }


    /// A command into the context named, or, when none is named, into the context that
    /// contributes the evaluated order; `recalc` ends the answer in its totals (the adapter's
    /// `recalculate` over the provider's totals data). An evaluation that fails is the answer,
    /// not the plan as it was.
    let navigate
        (recalc: OrderPlan -> OrderPlan)
        (orderCtxPort: OrderContextPort)
        (plan: OrderPlan)
        (contextId: string option)
        (ctxCmd: Api.OrderContextCommand)
        (ctx: OrderContext)
        =
        async {
            let! result = orderCtxPort.evaluate ctxCmd ctx

            return
                result
                |> Result.bind (fun resolved ->
                    let id =
                        match contextId with
                        | Some id -> Some id
                        | None ->
                            contribution resolved
                            |> Option.bind (fun sc ->
                                plan.OrderContexts
                                |> Array.tryFind (fun c ->
                                    contribution c |> Option.exists (fun s -> s.Order.Id = sc.Order.Id)
                                )
                            )
                            |> Option.map _.Id

                    match id with
                    | Some id -> plan |> updateContext id resolved
                    | None -> Error [| "The plan holds no context for the order" |]
                )
                |> Result.map recalc
        }


    /// Whether the plan may take a context of the category: one context per nutrition category,
    /// except supplements (any number, each under a feeding) and electrolyte and glucose lines
    /// (any number, one per generic prescribed). The nutrition page's buttons keep the same
    /// rule; the server keeps it for every caller.
    let admits category (plan: OrderPlan) =
        match category with
        | NutritionCategory.EnteralSupplement when plan |> holds NutritionCategory.EnteralFeeding |> not ->
            Error [| "A supplement needs a feeding in the plan" |]
        | NutritionCategory.EnteralSupplement
        | NutritionCategory.ElectrolyteGlucose -> Ok()
        | _ when plan |> holds category ->
            Error
                [|
                    $"The plan already holds a %s{Models.NutritionCategory.label category} context"
                |]
        | _ -> Ok()


    /// A nutrition context for the category, its filter discovered, appended to the plan.
    let addContext
        (recalc: OrderPlan -> OrderPlan)
        (orderCtxPort: OrderContextPort)
        (plan: OrderPlan)
        (category: NutritionCategory)
        =
        async {
            match plan |> admits category with
            | Error errs -> return Error errs
            | Ok() ->
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
                        // the context says what it holds wherever it goes
                        let resolved =
                            { resolved with
                                Id = System.Guid.NewGuid().ToString()
                                Category = OrderCategory.Nutrition category
                            }

                        { plan with OrderContexts = Array.append plan.OrderContexts [| resolved |] }
                        |> recalc
                        |> Ok
                    | None ->
                        Error
                            [|
                                "Could not discover filter options for nutrition context"
                            |]
        }
