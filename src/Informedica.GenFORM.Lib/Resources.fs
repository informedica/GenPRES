namespace Informedica.GenForm.Lib


module Resources =

    open System
    open Informedica.Logging.Lib

    module CoreNormalValueTables = Informedica.GenCore.Lib.Patients.NormalValueTables
    module CoreCalculationWeightsAndHeights = Informedica.GenCore.Lib.Patients.CalculationWeightsAndHeights
    module CoreEGFRBandOptions = Informedica.GenCore.Lib.Patients.EGFRBandOptions
    module CoreEGFRBand = Informedica.GenCore.Lib.Patients.EGFRBand

    type CoreNormalValueError = Informedica.GenCore.Lib.Patients.NormalValueError
    type CoreNormalValueTables = Informedica.GenCore.Lib.Patients.NormalValueTables
    type CoreCalculationWeightsAndHeights = Informedica.GenCore.Lib.Patients.CalculationWeightsAndHeights
    type CoreNormalValueSheetError = Informedica.GenCore.Lib.Patients.NormalValueSheetError
    type CoreNormalValueSheetWarning = Informedica.GenCore.Lib.Patients.NormalValueSheetWarning
    type CoreEGFRBandOption = Informedica.GenCore.Lib.Patients.EGFRBandOption
    type CoreEGFRBandSheetWarning = Informedica.GenCore.Lib.Patients.EGFRBandSheetWarning

    type Data =
        {
            UnitMappings: UnitMapping[]
            RouteMappings: RouteMapping[]
            ValidForms: string[]
            FormRoutes: FormRoute[]
            FormularyProducts: FormularyProduct[]
            GenPresProducts: Informedica.ZIndex.Lib.Types.GenPresProduct[]
            DoseRuleData: DoseRuleData[]
            SolutionRuleData: SolutionRuleData[]
            RenalRuleData: RenalRuleData[]
            TotalsData: TotalsData[]
        }


    /// The name of a resource, typed with the value type 'T of the resource.
    type ResourceKey<'T> = { Name: string }

    module ResourceKey =
        let create<'T> name : ResourceKey<'T> = { Name = name }


    /// Resolves the other resources a loader depends on while the loader runs.
    [<Interface>]
    type IResourceResolver =
        abstract member Get: ResourceKey<'T> -> 'T


    /// Takes the resolver for the dependencies and returns the boxed value with the non-fatal warnings,
    /// or the fatal errors. The value is boxed so one registry holds every resource type; the typed
    /// keys unbox it at the right type.
    type ResourceLoader = IResourceResolver -> Result<obj * Message list, Message list>

    /// The loader of every resource by name. A new resource needs only a new entry.
    type ResourceRegistry = Map<string, ResourceLoader>


    /// Gives the loaded resources, built and in memory.
    type IResourceProvider =
        /// Resolves any registered resource by its typed key.
        abstract member Get: ResourceKey<'T> -> 'T
        abstract member GetData: unit -> Data
        abstract member GetUnitMappings: unit -> UnitMapping[]
        abstract member GetRouteMappings: unit -> RouteMapping[]
        abstract member GetValidForms: unit -> string[]
        abstract member GetFormRoutes: unit -> FormRoute[]
        abstract member GetFormularyProducts: unit -> FormularyProduct[]
        abstract member GetReconstitution: unit -> Reconstitution[]
        abstract member GetParenteralMeds: unit -> ProductComponent[]
        abstract member GetEnteralFeeding: unit -> ProductComponent[]
        abstract member GetProducts: unit -> ProductComponent[]
        abstract member GetDoseRules: unit -> DoseRule[]
        abstract member GetSolutionRules: unit -> SolutionRule[]
        abstract member GetRenalRules: unit -> RenalRule[]
        abstract member GetTotals: unit -> TotalsData[]
        abstract member GetGStandProvider: unit -> Check.GStandProvider
        /// The departments the loaded rules name and the default; only the default when the
        /// rules did not load, since no rule is matched then.
        abstract member GetDepartments: unit -> Departments
        abstract member GetResourceInfo: unit -> ResourceInfo

    and ResourceInfo =
        {
            Messages: Message[]
            LastUpdated: DateTime
            IsLoaded: bool
        }


    /// The loaded resources: the data read from the sources in Data, and the products and rules built
    /// from it at the top level.
    type ResourceState =
        {
            Data: Data
            Reconstitution: Reconstitution[]
            ParenteralMeds: ProductComponent[]
            EnteralFeeding: ProductComponent[]
            Products: ProductComponent[]
            DoseRules: DoseRule[]
            SolutionRules: SolutionRule[]
            RenalRules: RenalRule[]
            Messages: Message[]
            IsLoaded: bool
            LastReloaded: DateTime
        }


    let private emptyData =
        {
            UnitMappings = [||]
            RouteMappings = [||]
            ValidForms = [||]
            FormRoutes = [||]
            FormularyProducts = [||]
            GenPresProducts = [||]
            DoseRuleData = [||]
            SolutionRuleData = [||]
            RenalRuleData = [||]
            TotalsData = [||]
        }


    let private resourceInfo (state: ResourceState) =
        {
            Messages = state.Messages
            LastUpdated = state.LastReloaded
            IsLoaded = state.IsLoaded
        }


    exception ResourceLoadError of Message list


    /// Resolves a resource on its first request, after the resources it depends on, and caches the value.
    /// Collects the warnings of every loader. Raises ResourceLoadError for a loader error, a cyclic
    /// dependency or a resource that is not registered.
    type LoadEngine(registry: ResourceRegistry) =
        let cache = System.Collections.Generic.Dictionary<string, obj>()
        let inProgress = System.Collections.Generic.HashSet<string>()
        let warnings = ResizeArray<Message>()

        member private this.ResolveObj(name: string) : obj =
            match cache.TryGetValue name with
            | true, v -> v
            | _ ->
                if not (inProgress.Add name) then
                    raise (ResourceLoadError [ ErrorMsg($"cyclic resource dependency: {name}", None) ])

                match registry.TryFind name with
                | None -> raise (ResourceLoadError [ ErrorMsg($"resource not registered: {name}", None) ])
                | Some loader ->
                    match loader (this :> IResourceResolver) with
                    | Ok(v, ws) ->
                        warnings.AddRange ws
                        cache[name] <- v
                        inProgress.Remove name |> ignore
                        v
                    | Error es -> raise (ResourceLoadError es)

        /// Resolves one resource at the type of its key, from the cache after the first time.
        member this.Resolve(key: ResourceKey<'T>) : 'T = this.ResolveObj key.Name :?> 'T

        /// Resolves every registered resource.
        member this.ForceAll() =
            registry |> Map.iter (fun name _ -> this.ResolveObj name |> ignore)

        member _.Warnings = warnings |> List.ofSeq
        member _.Resolved = cache |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq

        interface IResourceResolver with
            member this.Get key = this.Resolve key


    /// Makes a loader without dependencies from a read that returns a Result. An error fails the whole load.
    let ofResult (f: unit -> Result<'T, Message list>) : ResourceLoader =
        fun _ -> f () |> Result.map (fun v -> box v, [])

    /// Makes a loader without dependencies that passes the warnings of its result on.
    let ofResultWith (f: unit -> Result<'T * Message list, Message list>) : ResourceLoader =
        fun _ -> f () |> Result.map (fun (v, warnings) -> box v, warnings)

    /// Makes a loader that builds a value from other resources, without warnings.
    let derive (f: IResourceResolver -> 'T) : ResourceLoader = fun r -> Ok(box (f r), [])

    /// Makes a loader that builds a value from other resources, with warnings.
    let deriveWith (f: IResourceResolver -> 'T * Message list) : ResourceLoader =
        fun r ->
            let v, ws = f r
            Ok(box v, ws)


    let private messageText =
        function
        | Info s
        | Warning s -> s
        | ErrorMsg(s, _) -> s


    /// Returns the message text for an error in the normal-value rows.
    let normalValueErrorText err =
        match err with
        | CoreNormalValueError.MissingSheet sheet -> $"normal values, sheet %s{sheet}: missing"
        | CoreNormalValueError.MissingColumns(sheet, columns) ->
            let names = columns |> String.concat ", "
            $"normal values, sheet %s{sheet}: columns missing: %s{names}"
        | CoreNormalValueError.NotANumber(sheet, row, column) ->
            $"normal values, sheet %s{sheet}, row %i{row}: no number in %s{column}"
        | CoreNormalValueError.UnknownSex(sheet, row, sex) ->
            $"normal values, sheet %s{sheet}, row %i{row}: the sex '%s{sex}' is not M or F"


    /// Parses the normal-value tables from the rows with GenCORE. Returns None with a warning per error
    /// when the rows do not parse. Empty rows return None without a warning, because the failed sheet
    /// read has already warned.
    let normalValueTablesOf (rows: Map<string, string[][]>) =
        if rows.IsEmpty then
            None, []
        else
            match rows |> CoreNormalValueTables.ofRows with
            | Ok tables -> Some tables, []
            | Error errs -> None, errs |> List.map (normalValueErrorText >> Warning)


    /// Returns the message text for an error in the rows of the sheet of the calculation weights or heights.
    let normalValueSheetErrorText err =
        match err with
        | CoreNormalValueSheetError.MissingSheet sheet -> $"calculation weights and heights, sheet %s{sheet}: missing"
        | CoreNormalValueSheetError.MissingColumn(sheet, column) ->
            $"calculation weights and heights, sheet %s{sheet}: column missing: %s{column}"
        | CoreNormalValueSheetError.NotAPositiveNumber(sheet, row, value) ->
            $"calculation weights and heights, sheet %s{sheet}, row %i{row}: '%s{value}' is not a positive number"
        | CoreNormalValueSheetError.RoundsToZero(sheet, row, value) ->
            $"calculation weights and heights, sheet %s{sheet}, row %i{row}: '%s{value}' rounds to 0"
        | CoreNormalValueSheetError.NoValues sheet -> $"calculation weights and heights, sheet %s{sheet}: no values"


    /// Returns the message text for a problem in the sheet of the calculation weights or heights that the parse
    /// corrects.
    let normalValueSheetWarningText warning =
        match warning with
        | CoreNormalValueSheetWarning.NotAscending(sheet, row) ->
            $"calculation weights and heights, sheet %s{sheet}, row %i{row}: lower than a row above it, the values are sorted"
        | CoreNormalValueSheetWarning.Duplicate(sheet, row) ->
            $"calculation weights and heights, sheet %s{sheet}, row %i{row}: a value a row above it holds, the value is used once"


    /// Returns the message text for a problem in the eGFR band sheet.
    let egfrBandSheetWarningText warning =
        let sheet = CoreEGFRBandOptions.sheet

        match warning with
        | CoreEGFRBandSheetWarning.MissingColumns columns ->
            let names = columns |> String.concat ", "
            $"eGFR bands, sheet %s{sheet}: columns missing: %s{names}"
        | CoreEGFRBandSheetWarning.NoBand(row, minimum, maximum) ->
            $"eGFR bands, sheet %s{sheet}, row %i{row}: the limits '%s{minimum}' to '%s{maximum}' are no band"
        | CoreEGFRBandSheetWarning.DuplicateBand row ->
            $"eGFR bands, sheet %s{sheet}, row %i{row}: a second row for a band, the first row is used"
        | CoreEGFRBandSheetWarning.MissingBand band ->
            let minimum, maximum = band |> CoreEGFRBand.limits
            $"eGFR bands, sheet %s{sheet}: no row with MinEGFR '%s{minimum}' and MaxEGFR '%s{maximum}'"


    /// Parses the eGFR band options from the rows with GenCORE, with a warning for every problem. Empty rows give
    /// no options and no warning, because the failed sheet read has already warned.
    let egfrBandOptionsOf rows =
        let options, warnings = rows |> CoreEGFRBandOptions.ofRows
        options, warnings |> List.map (egfrBandSheetWarningText >> Warning)


    /// Parses the calculation weights and heights from the rows with GenCORE. Returns them with a warning for
    /// each correction, or an error for each problem.
    let calculationWeightsAndHeightsOf rows =
        rows
        |> CoreCalculationWeightsAndHeights.ofRows
        |> Result.map (fun (calc, warnings) -> calc, warnings |> List.map (normalValueSheetWarningText >> Warning))
        |> Result.mapError (List.map (fun err -> ErrorMsg(normalValueSheetErrorText err, None)))


    /// <summary>Makes a loader without dependencies whose failure does not fail the whole load.</summary>
    /// <remarks>
    /// On an error the loader returns <paramref name="fallback"/> and turns each error into a warning
    /// that starts with <paramref name="note"/>. The warning shows in ResourceInfo.Messages, and a
    /// reload of the resources retries the read.
    /// </remarks>
    /// <param name="note">Says what was not loaded and what that means.</param>
    /// <param name="fallback">The value returned on an error.</param>
    /// <param name="f">The read.</param>
    let ofResultOrDefault (note: string) (fallback: 'T) (f: unit -> Result<'T, Message list>) : ResourceLoader =
        fun _ ->
            match f () with
            | Ok v -> Ok(box v, [])
            | Error msgs -> Ok(box fallback, msgs |> List.map (fun m -> Warning $"%s{note}: %s{messageText m}"))


    /// The departments the loaded rules name, and the default department for a patient without one.
    /// The default is used where rules are matched and is never written on the patient, so the patient
    /// returned equals the patient sent.
    module Departments =

        /// The default department, one hospital's ward written in code. Without it a patient without a
        /// department gets no solution rules and no reconstitutions of any ward.
        let defaultDepartment = "ICK"


        /// Makes the departments from the names: trimmed, blanks ignored, sorted, and always with the
        /// default department.
        let ofNamed (named: string option seq) : Departments =
            {
                Names =
                    named
                    |> Seq.choose id
                    |> Seq.map _.Trim()
                    |> Seq.filter (String.IsNullOrWhiteSpace >> not)
                    |> Seq.append [ defaultDepartment ]
                    |> Seq.distinct
                    |> Seq.sort
                    |> Seq.toArray
                Default = defaultDepartment
            }


        /// Makes the departments from the dose-rule rows, the solution-rule rows and the
        /// reconstitutions, the three sheets that name departments.
        let ofRules (doseRows: DoseRuleData[]) (solutionRows: SolutionRuleData[]) (reconstitutions: Reconstitution[]) =
            [
                doseRows |> Seq.map (fun d -> Some d.Patient.Dep)
                solutionRows |> Seq.map _.Department
                reconstitutions |> Seq.map _.Department
            ]
            |> Seq.concat
            |> ofNamed


        /// Returns whether the department is one of the departments.
        let isKnown (departments: Departments) (department: string) = departments.Names |> Array.contains department


    /// The typed key of every resource.
    module Keys =
        let unitMappings = ResourceKey.create<UnitMapping[]> "unitMappings"
        let routeMappings = ResourceKey.create<RouteMapping[]> "routeMappings"
        let validForms = ResourceKey.create<string[]> "validForms"
        let formRoutes = ResourceKey.create<FormRoute[]> "formRoutes"
        let formularyProducts = ResourceKey.create<FormularyProduct[]> "formularyProducts"

        let genPresProducts =
            ResourceKey.create<Informedica.ZIndex.Lib.Types.GenPresProduct[]> "genPresProducts"

        let reconstitution = ResourceKey.create<Reconstitution[]> "reconstitution"
        let parenteralMeds = ResourceKey.create<ProductComponent[]> "parenteralMeds"
        let enteralFeeding = ResourceKey.create<ProductComponent[]> "enteralFeeding"
        let doseRuleData = ResourceKey.create<DoseRuleData[]> "doseRuleData"
        let solutionRuleData = ResourceKey.create<SolutionRuleData[]> "solutionRuleData"
        let departments = ResourceKey.create<Departments> "departments"
        let normalValueRows = ResourceKey.create<Map<string, string[][]>> "normalValueRows"
        // The normal-value tables GenCORE parses from the rows; None when the rows do not parse.
        let normalValueTables = ResourceKey.create<CoreNormalValueTables option> "normalValueTables"
        // The weights and heights an estimate is rounded to and the user picks from.
        let calculationWeightsAndHeights =
            ResourceKey.create<CoreCalculationWeightsAndHeights> "calculationWeightsAndHeights"
        // The rows of the eGFR band sheet; empty when the sheet fails to load.
        let egfrBandRows = ResourceKey.create<string[][]> "egfrBandRows"
        // The eGFR bands the user picks from, each with the text of its row.
        let egfrBandOptions = ResourceKey.create<CoreEGFRBandOption list> "egfrBandOptions"
        let renalRuleData = ResourceKey.create<RenalRuleData[]> "renalRuleData"
        let totalsData = ResourceKey.create<TotalsData[]> "totalsData"
        // The rows of the RuleSets sheet as written.
        let ruleSetRows = ResourceKey.create<RuleSetData[]> "ruleSetRows"
        // The rule set rows with every empty generic filled in from the dose rules.
        let ruleSetData = ResourceKey.create<RuleSetData[]> "ruleSetData"
        // Checks the rule set rows against the dose rules and the formulary; gives only warnings.
        let ruleSetChecks = ResourceKey.create<unit> "ruleSetChecks"
        // The rows of the LinkRules sheet as written, blank rows included.
        let linkRuleRows = ResourceKey.create<LinkRuleData[]> "linkRuleRows"
        // The link rules parsed from the rows; an invalid row fails the whole load.
        let linkRules = ResourceKey.create<LinkRule[]> "linkRules"
        // Checks the link rules against the dose rules; gives only warnings.
        let linkRuleChecks = ResourceKey.create<unit> "linkRuleChecks"
        let products = ResourceKey.create<ProductComponent[]> "products"
        let doseRules = ResourceKey.create<DoseRule[]> "doseRules"
        // The dose rules whose orders can be prescribed on their own, without a main order.
        let standAloneDoseRules = ResourceKey.create<DoseRule[]> "standAloneDoseRules"
        let solutionRules = ResourceKey.create<SolutionRule[]> "solutionRules"
        let renalRules = ResourceKey.create<RenalRule[]> "renalRules"
        // The renal rules refused by the unit checks, each with the reasons.
        let refusedRenalRules = ResourceKey.create<(RenalRule * string list)[]> "refusedRenalRules"
        // Checks the renal rules against the dose rules; gives only warnings.
        let renalRuleChecks = ResourceKey.create<unit> "renalRuleChecks"
        // Checks the dose rules and the solution rules for errors in the sheets; gives only warnings.
        let ruleChecks = ResourceKey.create<unit> "ruleChecks"
        // The G-Standaard dose rules, as a function.
        let gStandProvider = ResourceKey.create<Check.GStandProvider> "gStandProvider"
        // Returns whether the Formulary marks the product with a GPK as IsAdditive.
        let isAdditive = ResourceKey.create<string -> bool> "isAdditive"

        // The Nederlands Kinder Formularium link lookup, as a function.
        let nkfLinkProvider = ResourceKey.create<Source.LinkProvider> "nkfLinkProvider"


    /// The loader of the renal rule data. A renal sheet that fails to load fails the whole load,
    /// because empty renal rules would give every patient with a reduced kidney function the full dose.
    let renalRuleDataLoader fetch = ofResult fetch


    /// The default registry, which reads the sheets of dataUrlId. A loader names its dependencies with
    /// r.Get, and the engine loads each resource once. The parenteral, enteral and other products all
    /// read the formularyProducts resource, so the Formulary sheet is read once.
    let defaultRegistry logger dataUrlId : ResourceRegistry =
        Map
            [
                Keys.unitMappings.Name, ofResult (fun () -> Mapping.getUnitMapping dataUrlId)
                Keys.routeMappings.Name, ofResult (fun () -> Mapping.getRouteMapping dataUrlId)
                Keys.validForms.Name, ofResult (fun () -> Mapping.getValidForms dataUrlId)

                Keys.formRoutes.Name,
                (fun r ->
                    Mapping.getFormRoutes dataUrlId (r.Get Keys.unitMappings)
                    |> Result.map (fun v -> box v, [])
                )

                Keys.formularyProducts.Name, ofResult (fun () -> Product.getFormularyProducts dataUrlId)
                Keys.reconstitution.Name, ofResult (fun () -> Product.Reconstitution.get dataUrlId)

                // The parenteral and enteral products have no sheet of their own: they are the Formulary
                // rows of that Type, with the Formulary nutrition columns as their substances.
                Keys.parenteralMeds.Name,
                derive (fun r ->
                    r.Get Keys.formularyProducts
                    |> Product.Parenteral.get logger (r.Get Keys.unitMappings) (r.Get Keys.routeMappings)
                )

                Keys.enteralFeeding.Name,
                derive (fun r ->
                    r.Get Keys.formularyProducts
                    |> Product.Enteral.get logger (r.Get Keys.unitMappings) (r.Get Keys.routeMappings)
                )

                // Reads the G-Standaard products from ZIndex.
                Keys.genPresProducts.Name,
                ofResult (fun () ->
                    try
                        Informedica.ZIndex.Lib.GenPresProduct.get [] |> Ok
                    with exn ->
                        Utils.Result.createError "GetGenPresProducts" exn
                )

                Keys.doseRuleData.Name, ofResult (fun () -> DoseRuleLoader.getData dataUrlId)
                Keys.solutionRuleData.Name, ofResult (fun () -> SolutionRule.getData dataUrlId)
                Keys.renalRuleData.Name, renalRuleDataLoader (fun () -> RenalRule.getData dataUrlId)

                // The departments the rules name, and the default department.
                Keys.departments.Name,
                derive (fun r ->
                    Departments.ofRules
                        (r.Get Keys.doseRuleData)
                        (r.Get Keys.solutionRuleData)
                        (r.Get Keys.reconstitution)
                )

                // A normal-value sheet that fails to load gives no normal-value estimates and a warning,
                // and does not empty the rules.
                Keys.normalValueRows.Name,
                ofResultOrDefault
                    "Normal values not loaded"
                    Map.empty
                    (fun () -> Mapping.getSheetRows dataUrlId CoreNormalValueTables.sheets)

                // Rows that do not parse give no normal-value tables and a warning per error, and do not
                // empty the rules.
                Keys.normalValueTables.Name, deriveWith (fun r -> r.Get Keys.normalValueRows |> normalValueTablesOf)

                // A calculation weight or height sheet that fails to load or holds an error fails the whole
                // load, because the estimates and the options need it.
                Keys.calculationWeightsAndHeights.Name,
                ofResultWith (fun () ->
                    Mapping.getSheetRows dataUrlId CoreCalculationWeightsAndHeights.sheets
                    |> Result.bind calculationWeightsAndHeightsOf
                )

                // An eGFR band sheet that fails to load gives no eGFR band options and a warning, and does not
                // empty the rules.
                Keys.egfrBandRows.Name,
                ofResultOrDefault
                    "eGFR bands not loaded"
                    ([||]: string[][])
                    (fun () ->
                        Mapping.getSheetRows dataUrlId [ CoreEGFRBandOptions.sheet ]
                        |> Result.map (Map.find CoreEGFRBandOptions.sheet)
                    )

                Keys.egfrBandOptions.Name, deriveWith (fun r -> r.Get Keys.egfrBandRows |> egfrBandOptionsOf)

                // A Totals sheet that fails to load gives no totals and a warning, and does not empty
                // the other resources.
                Keys.totalsData.Name,
                ofResultOrDefault
                    "Totals resource not loaded"
                    ([||]: TotalsData[])
                    (fun () -> Mapping.getTotals dataUrlId)

                // A RuleSets sheet that fails to load fails the whole load, because a category without
                // a rule set is refused.
                Keys.ruleSetRows.Name, ofResult (fun () -> Mapping.getRuleSets dataUrlId)

                Keys.ruleSetData.Name,
                derive (fun r ->
                    r.Get Keys.ruleSetRows
                    |> RuleSet.expand (r.Get Keys.doseRuleData |> RuleSet.DoseRuleNames.ofData)
                )

                Keys.ruleSetChecks.Name,
                deriveWith (fun r ->
                    (),
                    r.Get Keys.ruleSetRows
                    |> RuleSet.checkData (r.Get Keys.doseRuleData) (r.Get Keys.formularyProducts)
                )

                Keys.linkRuleRows.Name, ofResult (fun () -> Mapping.getLinkRules dataUrlId)

                // An invalid link rule row fails the whole load, because a Required link that is left
                // out would let the linked order be prescribed alone.
                Keys.linkRules.Name,
                (fun r -> r.Get Keys.linkRuleRows |> LinkRule.ofRows |> Result.map (fun v -> box v, []))

                Keys.linkRuleChecks.Name,
                deriveWith (fun r ->
                    (),
                    r.Get Keys.linkRuleRows
                    |> LinkRule.checkData (r.Get Keys.routeMappings) (r.Get Keys.doseRuleData)
                )

                // Filters the ZIndex products to those the dose rules can use before building them,
                // so the other products are never built.
                Keys.products.Name,
                derive (fun r ->
                    let filtered =
                        r.Get Keys.genPresProducts
                        |> Product.filterGenPresProductsByData
                            (r.Get Keys.routeMappings)
                            (r.Get Keys.formularyProducts)
                            (r.Get Keys.doseRuleData)

                    Product.fromGenPresProducts
                        logger
                        (r.Get Keys.unitMappings)
                        (r.Get Keys.routeMappings)
                        (r.Get Keys.validForms)
                        (r.Get Keys.formRoutes)
                        (r.Get Keys.reconstitution)
                        (r.Get Keys.parenteralMeds)
                        (r.Get Keys.enteralFeeding)
                        (r.Get Keys.formularyProducts)
                        filtered
                )

                // Builds the dose rules and leaves out those a refused renal rule applies to. The warnings
                // of both steps go to ResourceState.Messages.
                Keys.doseRules.Name,
                deriveWith (fun r ->
                    let drs, msgs =
                        DoseRuleLoader.fromData
                            (r.Get Keys.routeMappings)
                            (r.Get Keys.formRoutes)
                            (r.Get Keys.products)
                            (r.Get Keys.solutionRules)
                            (r.Get Keys.doseRuleData)

                    let drs, renalMsgs =
                        drs
                        |> RenalRule.withoutRefused (r.Get Keys.routeMappings) (r.Get Keys.refusedRenalRules)

                    drs, msgs @ renalMsgs
                )

                // Leaves out the dose rules a Required link rule matches as the linked order; those orders
                // come only with their main order.
                Keys.standAloneDoseRules.Name,
                derive (fun r ->
                    r.Get Keys.doseRules
                    |> LinkRule.standAlone (r.Get Keys.routeMappings) (r.Get Keys.linkRules)
                )

                Keys.solutionRules.Name,
                (fun r ->
                    SolutionRule.map
                        (r.Get Keys.routeMappings)
                        (r.Get Keys.parenteralMeds)
                        (r.Get Keys.products)
                        (r.Get Keys.solutionRuleData)
                    |> Result.map (fun v -> box v, [])
                )

                Keys.refusedRenalRules.Name, derive (fun r -> r.Get Keys.renalRuleData |> RenalRule.refused logger)

                Keys.renalRules.Name,
                (fun r ->
                    RenalRule.map logger (r.Get Keys.renalRuleData)
                    |> Result.map (fun v -> box v, [])
                )

                // A renal rule for a reduced kidney function with a higher maximum than a dose rule
                // it applies to is reported -> ResourceState.Messages.
                Keys.renalRuleChecks.Name,
                deriveWith (fun r ->
                    (),
                    r.Get Keys.renalRules
                    |> RenalRule.doseRuleWarnings (r.Get Keys.routeMappings) (r.Get Keys.doseRules)
                )

                // Reported -> ResourceState.Messages:
                // - a rule with a minimum above its maximum
                // - a solution rule whose diluents match no parenteral product
                // - a solution rule without diluents whose concentration limit allows no strength of its products
                // - a solution rule whose dose count has no maximum and no maximum on the preparation
                // - two solution rules that apply to one dose rule and one patient
                // - a diluent that is also a component of a dose rule the solution rule applies to
                // - a diluent whose Formulary product is not marked IsDiluent
                // - a solution rule with a diluent row but no diluent
                // - a solution rule with more than one dilution limit for the same component or substance
                // - a dose rule and a solution rule whose ranges for a component no order can meet
                // - a product of a dose rule without a Divisible, or products whose Divisible disagree
                Keys.ruleChecks.Name,
                deriveWith (fun r ->
                    (),
                    (r.Get Keys.solutionRuleData
                     |> SolutionRule.missingDiluents (r.Get Keys.parenteralMeds))
                    @ (r.Get Keys.solutionRuleData
                       |> SolutionRule.unmarkedDiluents (r.Get Keys.formularyProducts))
                    @ (r.Get Keys.solutionRuleData
                       |> SolutionRule.diluentRowsWithoutDiluent (r.Get Keys.parenteralMeds))
                    @ (r.Get Keys.solutionRules
                       |> SolutionRule.diluentsThatAreComponents (r.Get Keys.routeMappings) (r.Get Keys.doseRules))
                    @ (r.Get Keys.doseRules |> DoseRule.invertedRanges)
                    @ (r.Get Keys.doseRules |> DoseRule.divisibleWarnings)
                    @ (r.Get Keys.solutionRules |> SolutionRule.invertedRanges)
                    @ (r.Get Keys.solutionRules |> SolutionRule.openDoseCounts)
                    @ (r.Get Keys.solutionRules |> SolutionRule.repeatedTargets)
                    @ (r.Get Keys.solutionRules
                       |> SolutionRule.competingRules (r.Get Keys.routeMappings) (r.Get Keys.doseRules))
                    @ (r.Get Keys.solutionRules
                       |> SolutionRule.undilutableRules (r.Get Keys.routeMappings) (r.Get Keys.doseRules))
                    @ (r.Get Keys.solutionRules
                       |> SolutionRule.conflictingLimits (r.Get Keys.routeMappings) (r.Get Keys.doseRules))
                )

                // The G-Standaard dose rules as a function that depends only on the route mappings;
                // ZIndex.Lib caches the G-Standaard data itself.
                Keys.gStandProvider.Name, derive (fun r -> Check.gStandProvider (r.Get Keys.routeMappings))

                // The lookup by GPK of the products marked IsAdditive, built once per load.
                Keys.isAdditive.Name,
                derive (fun r ->
                    let gpks =
                        r.Get Keys.formularyProducts
                        |> Array.filter _.CanBeUsedAsAdditive
                        |> Array.map _.GPKODE
                        |> Set.ofArray

                    fun gpk -> gpks |> Set.contains gpk
                )

                // The NKF link lookup is optional like totalsData: the links only annotate a printed
                // dose rule, and the index is fetched from a third party. A failed fetch gives a warning
                // in ResourceInfo, and a reload retries it. The fallback is Source.getLink [] and not
                // Source.noLinks: it loses the NKF links but keeps the Farmacotherapeutisch Kompas
                // links, which are built from the generic name alone.
                Keys.nkfLinkProvider.Name,
                ofResultOrDefault
                    "NKF links not loaded, falling back to \"*Lokaal*\""
                    (Source.getLink []: Source.LinkProvider)
                    (fun () ->
                        SourceLoader.fetchNKFMedications ()
                        |> Result.map (fun meds -> (Source.getLink meds: Source.LinkProvider))
                    )
            ]


    /// The loaded resources as a typed ResourceState, and as a map of every resolved resource by name
    /// for IResourceProvider.Get and the resources that are functions.
    type LoadedResources =
        {
            State: ResourceState
            Resolved: Map<string, obj>
        }


    /// Loads every resource of the registry. Returns the errors of the first resource that fails.
    let loadAllResourcesWithRegistry (registry: ResourceRegistry) : Result<LoadedResources, Message list> =
        try
            let eng = LoadEngine registry
            // The first fatal error stops the load.
            eng.ForceAll()

            let state =
                {
                    Data =
                        {
                            UnitMappings = eng.Resolve Keys.unitMappings
                            RouteMappings = eng.Resolve Keys.routeMappings
                            ValidForms = eng.Resolve Keys.validForms
                            FormRoutes = eng.Resolve Keys.formRoutes
                            FormularyProducts = eng.Resolve Keys.formularyProducts
                            GenPresProducts = eng.Resolve Keys.genPresProducts
                            DoseRuleData = eng.Resolve Keys.doseRuleData
                            SolutionRuleData = eng.Resolve Keys.solutionRuleData
                            RenalRuleData = eng.Resolve Keys.renalRuleData
                            TotalsData = eng.Resolve Keys.totalsData
                        }
                    Reconstitution = eng.Resolve Keys.reconstitution
                    ParenteralMeds = eng.Resolve Keys.parenteralMeds
                    EnteralFeeding = eng.Resolve Keys.enteralFeeding
                    Products = eng.Resolve Keys.products
                    DoseRules = eng.Resolve Keys.doseRules
                    SolutionRules = eng.Resolve Keys.solutionRules
                    RenalRules = eng.Resolve Keys.renalRules
                    Messages = eng.Warnings |> List.toArray
                    LastReloaded = DateTime.UtcNow
                    IsLoaded = true
                }

            Ok
                {
                    State = state
                    Resolved = eng.Resolved
                }
        with
        | ResourceLoadError es -> Error es
        | exn -> [ ("Failed to load resources", Some exn) |> ErrorMsg ] |> Error


    /// Loads every resource of the default registry.
    let loadAllResources logger dataUrlId = loadAllResourcesWithRegistry (defaultRegistry logger dataUrlId)


    /// The departments of a resolved resource set; only the default when the rules did not load.
    let departmentsOf (loaded: LoadedResources) =
        match loaded.Resolved |> Map.tryFind Keys.departments.Name with
        | Some d -> d :?> Departments
        | None -> Departments.ofNamed []


    /// A plain provider over an already-loaded resource set.
    type ResourceProvider(loaded: LoadedResources) =
        let state = loaded.State

        interface IResourceProvider with
            member _.Get(key: ResourceKey<'T>) : 'T = loaded.Resolved[key.Name] :?> 'T
            member _.GetData() = state.Data
            member _.GetUnitMappings() = state.Data.UnitMappings
            member _.GetRouteMappings() = state.Data.RouteMappings
            member _.GetValidForms() = state.Data.ValidForms
            member _.GetFormRoutes() = state.Data.FormRoutes
            member _.GetFormularyProducts() = state.Data.FormularyProducts
            member _.GetReconstitution() = state.Reconstitution
            member _.GetParenteralMeds() = state.ParenteralMeds
            member _.GetEnteralFeeding() = state.EnteralFeeding
            member _.GetProducts() = state.Products
            member _.GetDoseRules() = state.DoseRules
            member _.GetSolutionRules() = state.SolutionRules
            member _.GetRenalRules() = state.RenalRules
            member _.GetTotals() = state.Data.TotalsData

            member _.GetGStandProvider() =
                loaded.Resolved[Keys.gStandProvider.Name] :?> Check.GStandProvider

            member _.GetDepartments() = departmentsOf loaded

            member _.GetResourceInfo() = resourceInfo state


    /// A resource provider that loads the resources on first use and caches them. With a TTL in minutes
    /// it loads them again after the TTL; without one the cache never expires. A failed load is cached as
    /// empty resources.
    type CachedResourceProvider
        (logger: Logger, loadAllResources: unit -> Result<LoadedResources, Message list>, ttlMinutes: int option)
        =
        let mutable cached: (LoadedResources * DateTime) option = None
        let lockObj = obj ()

        let isExpired (timestamp: DateTime) =
            match ttlMinutes with
            | None -> false // No expiration if ttl is not set
            | Some ttlMinutes -> DateTime.UtcNow.Subtract(timestamp).TotalMinutes > float ttlMinutes

        let loadFresh () =
            match loadAllResources () with
            | Ok loaded ->
                cached <- Some(loaded, DateTime.UtcNow)
                loaded
            | Error msgs ->
                msgs
                |> List.iter (fun m ->
                    ErrorMsg($"Failed to load resources: {messageText m}", None)
                    |> Logging.logError logger
                )
                // Return empty state on error and cache it to prevent retry on every request
                let emptyLoaded =
                    {
                        State =
                            {
                                Data = emptyData
                                Reconstitution = [||]
                                ParenteralMeds = [||]
                                EnteralFeeding = [||]
                                Products = [||]
                                DoseRules = [||]
                                SolutionRules = [||]
                                RenalRules = [||]
                                Messages = [| yield! msgs |> List.toArray |]
                                LastReloaded = DateTime.MinValue
                                IsLoaded = false
                            }
                        // The rule lookup reads isAdditive, so the empty resources need a lookup that
                        // always returns false instead of a missing key.
                        Resolved = Map [ Keys.isAdditive.Name, box (fun (_: string) -> false) ]
                    }

                cached <- Some(emptyLoaded, DateTime.UtcNow)
                emptyLoaded

        member private _.getFromCache(selector: LoadedResources -> 'T) =
            lock
                lockObj
                (fun () ->
                    match cached with
                    | Some(loaded, timestamp) when not (isExpired timestamp) -> selector loaded
                    | _ -> selector (loadFresh ())
                )

        member _.ReloadCache() =
            lock
                lockObj
                (fun () ->
                    cached <- None // Invalidate cache
                    loadFresh () |> ignore // Load fresh data
                )

        interface IResourceProvider with
            member this.Get(key: ResourceKey<'T>) : 'T = this.getFromCache (fun l -> l.Resolved[key.Name] :?> 'T)

            member this.GetData() = this.getFromCache _.State.Data

            member this.GetUnitMappings() = this.getFromCache _.State.Data.UnitMappings

            member this.GetRouteMappings() = this.getFromCache _.State.Data.RouteMappings

            member this.GetValidForms() = this.getFromCache _.State.Data.ValidForms

            member this.GetFormRoutes() = this.getFromCache _.State.Data.FormRoutes

            member this.GetFormularyProducts() = this.getFromCache _.State.Data.FormularyProducts

            member this.GetReconstitution() = this.getFromCache _.State.Reconstitution

            member this.GetEnteralFeeding() = this.getFromCache _.State.EnteralFeeding

            member this.GetParenteralMeds() = this.getFromCache _.State.ParenteralMeds

            member this.GetProducts() = this.getFromCache _.State.Products
            member this.GetDoseRules() = this.getFromCache _.State.DoseRules
            member this.GetSolutionRules() = this.getFromCache _.State.SolutionRules
            member this.GetRenalRules() = this.getFromCache _.State.RenalRules

            member this.GetTotals() = this.getFromCache _.State.Data.TotalsData

            member this.GetGStandProvider() =
                this.getFromCache (fun l -> l.Resolved[Keys.gStandProvider.Name] :?> Check.GStandProvider)

            member this.GetDepartments() = this.getFromCache departmentsOf

            member this.GetResourceInfo() = this.getFromCache (fun l -> resourceInfo l.State)
