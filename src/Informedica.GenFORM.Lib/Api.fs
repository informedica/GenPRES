namespace Informedica.GenForm.Lib


module Api =

    open Informedica.Logging.Lib

    open Resources

    type CorePatientDataResources = Informedica.GenCore.Lib.Patients.PatientDataResources


    let private logGenFormMessages (logger: Logger) (provider: IResourceProvider) =
        provider.GetResourceInfo().Messages
        |> Array.iter (fun m ->
            match m with
            | Info _ -> Logging.logInfo logger m
            | Warning _ -> Logging.logWarning logger m
            | ErrorMsg _ -> Logging.logError logger m
        )


    let getCachedProviderWithDataUrlId (logger: Logger) dataUrlId : IResourceProvider =
        let provider = CachedResourceProvider(logger, (fun () -> loadAllResources logger dataUrlId), None)

        (provider :> IResourceProvider) |> logGenFormMessages logger
        provider


    let reloadCache (logger: Logger) (provider: IResourceProvider) =
        match provider with
        | :? CachedResourceProvider as cachedProvider ->
            cachedProvider.ReloadCache()
            (cachedProvider :> IResourceProvider) |> logGenFormMessages logger
        | _ -> invalidArg (nameof provider) "Provider is not a CachedResourceProvider instance"


    /// Returns the registered resource for a typed key.
    let get (key: ResourceKey<'T>) (provider: IResourceProvider) : 'T = provider.Get key


    /// Returns the resources the patient data is completed with: the normal-value tables, the calculation weights
    /// and heights, and the eGFR band options. None when the normal-value rows did not parse.
    let getPatientDataResources (provider: IResourceProvider) : CorePatientDataResources option =
        provider.Get Keys.normalValueTables
        |> Option.map (fun tables ->
            {
                NormalValueTables = tables
                CalculationWeightsAndHeights = provider.Get Keys.calculationWeightsAndHeights
                EGFRBandOptions = provider.Get Keys.egfrBandOptions
            }
        )


    let getRouteMapping (provider: IResourceProvider) = provider.GetRouteMappings()


    let getGStandProvider (provider: IResourceProvider) = provider.GetGStandProvider()


    /// <summary>
    /// Returns the link lookup for the Nederlands Kinderformularium, used by DoseRule.Print.toMarkdown.
    /// </summary>
    /// <remarks>
    /// When the resource load failed, Get raises KeyNotFoundException and this returns Source.getLink [] instead,
    /// so a missing link never fails a request. A single Get call avoids the race of an IsLoaded check
    /// followed by Get while a reload fails.
    /// </remarks>
    let getNKFLinkProvider (provider: IResourceProvider) : Source.LinkProvider =
        try
            provider.Get Keys.nkfLinkProvider
        with :? System.Collections.Generic.KeyNotFoundException ->
            Source.getLink []


    let getDoseRules (provider: IResourceProvider) = provider.GetDoseRules()


    let getSolutionRules (provider: IResourceProvider) = provider.GetSolutionRules()


    let getRenalRules (provider: IResourceProvider) = provider.GetRenalRules()

    let getTotals (provider: IResourceProvider) = provider.GetTotals()


    // Filtering functions using cached mappings


    let filterDoseRules (provider: IResourceProvider) (filter: DoseFilter) doseRules =
        let routeMappings = getRouteMapping provider

        DoseRule.filter routeMappings filter doseRules


    /// Returns the solution rules that match the filter, with the route mapping of the provider.
    let filterSolutionRules (provider: IResourceProvider) filter solutionRules =
        solutionRules |> SolutionRule.filter (getRouteMapping provider) filter


    /// Returns a lookup that tells whether the Formulary marks the product with a GPK as an additive.
    let isAdditive (provider: IResourceProvider) = provider.Get Keys.isAdditive


    /// Returns the product predilutions: the predilution rules, each with the substances of its diluent.
    let getProductPredilutions (provider: IResourceProvider) = provider.Get Keys.productPredilutions


    /// Returns the prescription rules of the patient. The rules come from the cache of the load when the provider
    /// holds a cache, gives the solution rules, renal rules, route mappings, additive lookup and product
    /// predilutions the cache was built from, and gives the dose rules or the stand-alone dose rules of that load.
    /// Otherwise the rules are built on every call. A reload between the reads, or a provider that wraps another,
    /// can give other rules.
    let getPrescriptionRules (provider: IResourceProvider) =
        let doseRules = getDoseRules provider
        let solutionRules = getSolutionRules provider
        let routeMappings = getRouteMapping provider
        let renalRules = getRenalRules provider
        let isAdditive = isAdditive provider
        let predilutions = getProductPredilutions provider

        let cache =
            try
                provider.Get Keys.prescriptionRuleCache |> Some
            with :? System.Collections.Generic.KeyNotFoundException ->
                None

        cache
        |> Option.filter (fun cache ->
            obj.ReferenceEquals(cache.SolutionRules, solutionRules)
            && obj.ReferenceEquals(cache.RenalRules, renalRules)
            && obj.ReferenceEquals(cache.RouteMappings, routeMappings)
            && obj.ReferenceEquals(cache.IsAdditive, isAdditive)
            && obj.ReferenceEquals(cache.Predilutions, predilutions)
        )
        |> Option.bind (fun cache -> cache.TryRulesOf doseRules)
        |> Option.defaultWith (fun () ->
            PrescriptionRule.getForPatient isAdditive doseRules solutionRules renalRules predilutions routeMappings
        )


    let filterPrescriptionRules
        (provider: IResourceProvider)
        (filter: DoseFilter)
        : Result<PrescriptionRule array, Message list>
        =
        let doseRules = getDoseRules provider
        let solutionRules = getSolutionRules provider
        let routeMappings = getRouteMapping provider
        let renalRules = getRenalRules provider
        let isAdditive = isAdditive provider
        let predilutions = getProductPredilutions provider

        let chunkSize =
            let c = (doseRules |> Array.length) / 12
            if c > 0 then c else 1

        doseRules
        |> Array.chunkBySize chunkSize
        |> Array.map (fun rules ->
            async {
                return
                    PrescriptionRule.filter isAdditive rules solutionRules renalRules predilutions routeMappings filter
            }
        )
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Utils.Result.foldResults


    /// Creates a warning that no prescription rule matches the filter, with its generic, route and form.
    let noRulesWarning (filter: DoseFilter) =
        let show = Option.defaultValue "-"

        Warning
            $"No prescription rules for generic %s{show filter.Generic}, route %s{show filter.Route}, form %s{show filter.Form}"
