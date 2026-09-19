namespace Informedica.GenForm.Lib


module Api =

    open Informedica.Logging.Lib

    open Resources


    let private logGenFormMessages (logger: Logger) (provider: IResourceProvider) =
        provider.GetResourceInfo().Messages
        |> Array.iter (fun m ->
            match m with
            | Info _ -> Logging.logInfo logger m
            | Warning _ -> Logging.logWarning logger m
            | ErrorMsg _ -> Logging.logError logger m
        )


    let getCachedProviderWithDataUrlId (logger: Logger) dataUrlId : IResourceProvider =
        let provider = CachedResourceProvider((fun () -> loadAllResources dataUrlId), None)

        (provider :> IResourceProvider) |> logGenFormMessages logger
        provider


    let reloadCache (logger: Logger) (provider: IResourceProvider) =
        match provider with
        | :? CachedResourceProvider as cachedProvider ->
            cachedProvider.ReloadCache()
            (cachedProvider :> IResourceProvider) |> logGenFormMessages logger
        | _ -> invalidArg (nameof provider) "Provider is not a CachedResourceProvider instance"


    /// Generic accessor: resolve any registered resource by its typed key.
    let get (key: ResourceKey<'T>) (provider: IResourceProvider) : 'T = provider.Get key


    let getRouteMapping (provider: IResourceProvider) = provider.GetRouteMappings()


    let getGStandProvider (provider: IResourceProvider) = provider.GetGStandProvider()


    /// <summary>
    /// The Nederlands Kinder Formularium link lookup, for <c>DoseRule.Print.toMarkdown</c>.
    /// </summary>
    /// <remarks>
    /// When only the NKF fetch failed the registry already serves <c>Source.getLink []</c>
    /// (FK links intact, NKF links gone); see <c>Keys.nkfLinkProvider</c>. When the whole
    /// resource load failed nothing is registered at all and <c>Get</c> raises
    /// <c>KeyNotFoundException</c>, so serve that same degraded provider here: a decorative
    /// link must never fail a request. One provider call, not an <c>IsLoaded</c> check followed
    /// by <c>Get</c>: a <c>CachedResourceProvider</c> takes its lock per call, so a reload failing
    /// between the two would still throw.
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


    let filterDoseRules (provider: IResourceProvider) filter doseRules =
        let routeMappings = getRouteMapping provider
        DoseRule.filter routeMappings filter doseRules


    let getPrescriptionRules (provider: IResourceProvider) =
        let doseRules = getDoseRules provider
        let solutionRules = getSolutionRules provider
        let routeMappings = getRouteMapping provider
        let renalRules = getRenalRules provider

        PrescriptionRule.getForPatient doseRules solutionRules renalRules routeMappings


    let filterPrescriptionRules (provider: IResourceProvider) filter : Result<PrescriptionRule array, Message list> =
        let doseRules = getDoseRules provider
        let solutionRules = getSolutionRules provider
        let routeMappings = getRouteMapping provider
        let renalRules = getRenalRules provider

        let chunkSize =
            let c = (doseRules |> Array.length) / 12
            if c > 0 then c else 1

        doseRules
        |> Array.chunkBySize chunkSize
        |> Array.map (fun rules ->
            async { return PrescriptionRule.filter rules solutionRules renalRules routeMappings filter }
        )
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Utils.Result.foldResults
