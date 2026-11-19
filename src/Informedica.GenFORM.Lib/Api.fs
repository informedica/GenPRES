namespace Informedica.GenForm.Lib


module Api =

    open Informedica.Logging.Lib

    open Resources

    module CoreDepartment = Informedica.GenCore.Lib.Patients.Department

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


    /// Sets the default department of the provider when the patient data has no department.
    let withDefaultDepartment (provider: IResourceProvider) (patientData: PatientData) =
        match patientData |> PatientData.department with
        | Some _ -> patientData
        | None ->
            { patientData with Department = (provider.GetDepartments()).Default |> CoreDepartment.otherDepartment }


    let filterDoseRules (provider: IResourceProvider) (filter: DoseFilter) doseRules =
        let routeMappings = getRouteMapping provider
        let filter = { filter with PatientData = filter.PatientData |> withDefaultDepartment provider }

        DoseRule.filter routeMappings filter doseRules


    /// Returns a lookup that tells whether the Formulary marks the product with a GPK as an additive.
    let isAdditive (provider: IResourceProvider) = provider.Get Keys.isAdditive


    let getPrescriptionRules (provider: IResourceProvider) =
        let doseRules = getDoseRules provider
        let solutionRules = getSolutionRules provider
        let routeMappings = getRouteMapping provider
        let renalRules = getRenalRules provider

        withDefaultDepartment provider
        >> PrescriptionRule.getForPatient (isAdditive provider) doseRules solutionRules renalRules routeMappings


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
        let filter = { filter with PatientData = filter.PatientData |> withDefaultDepartment provider }

        let chunkSize =
            let c = (doseRules |> Array.length) / 12
            if c > 0 then c else 1

        doseRules
        |> Array.chunkBySize chunkSize
        |> Array.map (fun rules ->
            async { return PrescriptionRule.filter isAdditive rules solutionRules renalRules routeMappings filter }
        )
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Utils.Result.foldResults


    /// Creates a warning that no prescription rule matches the filter, with its generic, route and form.
    let noRulesWarning (filter: DoseFilter) =
        let show = Option.defaultValue "-"

        Warning
            $"No prescription rules for generic %s{show filter.Generic}, route %s{show filter.Route}, form %s{show filter.Form}"
