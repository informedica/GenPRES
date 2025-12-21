namespace Informedica.GenForm.Lib


module Resources =

    open System
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
    open FsToolkit.ErrorHandling.ResultCE


    type IResourceProvider =
        abstract member GetUnitMappings : unit -> UnitMapping[]
        abstract member GetRouteMappings : unit -> RouteMapping[]
        abstract member GetValidForms : unit -> string[]
        abstract member GetFormRoutes : unit -> FormRoute[]
        abstract member GetFormularyProducts : unit -> FormularyProduct[]
        abstract member GetReconstitution : unit -> Reconstitution[]
        abstract member GetParenteralMeds : unit -> Product[]
        abstract member GetEnteralFeeding : unit -> Product[]
        abstract member GetProducts : unit -> Product[]
        abstract member GetDoseRules : unit -> DoseRule[]
        abstract member GetSolutionRules : unit -> SolutionRule[]
        abstract member GetRenalRules : unit -> RenalRule[]
        abstract member GetResourceInfo : unit -> ResourceInfo

    and ResourceInfo = {
        Messages : Message []
        LastUpdated: DateTime
        IsLoaded: bool
    }


    type ResourceState =
        {
            UnitMappings: UnitMapping []
            RouteMappings: RouteMapping []
            ValidForms: string []
            FormRoutes: FormRoute []
            FormularyProducts: FormularyProduct []
            Reconstitution: Reconstitution []
            ParenteralMeds: Product []
            EnteralFeeding: Product []
            Products: Product []
            DoseRules: DoseRule []
            SolutionRules: SolutionRule []
            RenalRules: RenalRule []
            Messages: Message []
            IsLoaded : bool
            LastReloaded: DateTime
        }
    with
        interface IResourceProvider with
            member this.GetUnitMappings() = this.UnitMappings
            member this.GetRouteMappings() = this.RouteMappings
            member this.GetValidForms() = this.ValidForms
            member this.GetFormRoutes() = this.FormRoutes
            member this.GetFormularyProducts() = this.FormularyProducts
            member this.GetReconstitution() = this.Reconstitution
            member this.GetParenteralMeds() = this.ParenteralMeds
            member this.GetEnteralFeeding() = this.EnteralFeeding
            member this.GetProducts() = this.Products
            member this.GetDoseRules() = this.DoseRules
            member this.GetSolutionRules() = this.SolutionRules
            member this.GetRenalRules() = this.RenalRules
            member this.GetResourceInfo() = {
                Messages = this.Messages
                LastUpdated = this.LastReloaded
                IsLoaded = this.IsLoaded
            }


    type ResourceProvider(state: ResourceState) =
        interface IResourceProvider with
            member _.GetUnitMappings() = state.UnitMappings
            member _.GetRouteMappings() = state.RouteMappings
            member _.GetValidForms() = state.ValidForms
            member _.GetFormRoutes() = state.FormRoutes
            member _.GetFormularyProducts() = state.FormularyProducts
            member _.GetReconstitution() = state.Reconstitution
            member _.GetParenteralMeds() = state.ParenteralMeds
            member _.GetEnteralFeeding() = state.EnteralFeeding
            member _.GetProducts() = state.Products
            member _.GetDoseRules() = state.DoseRules
            member _.GetSolutionRules() = state.SolutionRules
            member _.GetRenalRules() = state.RenalRules
            member _.GetResourceInfo() = {
                Messages = state.Messages
                LastUpdated = state.LastReloaded
                IsLoaded = state.IsLoaded
            }


    let createProvider
        unitMappings
        routeMappings
        validForms
        formRoutes
        formularyProducts
        reconstitution
        parenteralMeds
        enteralFeeding
        products
        doseRules
        solutionRules
        renalRules
        msgs
        =
        let state = {
            UnitMappings = unitMappings
            RouteMappings = routeMappings
            ValidForms = validForms
            FormRoutes = formRoutes
            FormularyProducts = formularyProducts
            Reconstitution = reconstitution
            ParenteralMeds = parenteralMeds
            EnteralFeeding = enteralFeeding
            Products = products
            DoseRules = doseRules
            SolutionRules = solutionRules
            RenalRules = renalRules
            Messages = msgs
            LastReloaded = DateTime.UtcNow
            IsLoaded = true
        }

        ResourceProvider state :> IResourceProvider


    type ResourceConfig =
        {
            GetUnitMappings: unit -> GenFormResult<UnitMapping []>
            GetRouteMappings: unit -> GenFormResult<RouteMapping []>
            GetValidForms: unit -> GenFormResult<string []>
            GetFormRoutes: UnitMapping [] -> GenFormResult<FormRoute []>
            GetFormularyProducts: unit -> GenFormResult<FormularyProduct []>
            GetReconstitution: unit -> GenFormResult<Reconstitution []>
            GetParenteralMeds: UnitMapping[] -> GenFormResult<Product []>
            GetEnteralFeeding: UnitMapping[] -> GenFormResult<Product []>
            GetProducts:
                UnitMapping[] ->
                RouteMapping[] ->
                string[] ->
                FormRoute[] ->
                Reconstitution[] ->
                Product[] ->
                Product[] ->
                FormularyProduct[] ->
                Product[]
            GetDoseRules:
                RouteMapping[] ->
                FormRoute[] ->
                Product[] ->
                GenFormResult<DoseRule []>
            GetSolutionRules:
                RouteMapping[] ->
                Product[] ->
                Product[] ->
                GenFormResult<SolutionRule []>
            GetRenalRules: unit -> GenFormResult<RenalRule []>
        }


    let inline delay f = fun () -> f


    /// Default resource configuration using the standard get functions
    let defaultResourceConfig dataUrlId =
        {
            GetUnitMappings = Mapping.getUnitMapping dataUrlId |> delay
            GetRouteMappings = Mapping.getRouteMapping dataUrlId |> delay
            GetValidForms = Mapping.getValidForms dataUrlId |> delay
            GetFormRoutes = Mapping.getFormRoutes dataUrlId
            GetFormularyProducts = fun () -> Product.getFormularyProducts dataUrlId
            GetReconstitution = Product.Reconstitution.get dataUrlId |> delay
            GetParenteralMeds = Product.Parenteral.get dataUrlId
            GetEnteralFeeding = Product.Enteral.get dataUrlId
            GetProducts = Product.get
            GetDoseRules = DoseRule.get dataUrlId
            GetSolutionRules = SolutionRule.get dataUrlId
            GetRenalRules = RenalRule.get dataUrlId |> delay
        }


    /// Load all resources at once using provided configuration
    let loadAllResourcesWithConfig (config: ResourceConfig)  =
        try
            result {
                let! unitMappings, unitMsgs = config.GetUnitMappings()
                let! routeMappings, routeMsgs = config.GetRouteMappings()
                let! validForms, formMsgs = config.GetValidForms()
                let! formRoutes, formRouteMsgs = config.GetFormRoutes unitMappings
                let! formularyProducts, formularyMsgs = config.GetFormularyProducts()
                let! reconstitution, reconstitutionMsgs = config.GetReconstitution ()
                let! parenteralMeds, parenteralMsgs = config.GetParenteralMeds unitMappings
                let! enteralFeeding, enteralMsgs = config.GetEnteralFeeding unitMappings
                let products =
                    config.GetProducts
                        unitMappings
                        routeMappings
                        validForms
                        formRoutes
                        reconstitution
                        parenteralMeds
                        enteralFeeding
                        formularyProducts
                let! doseRules, doseRuleMsgs =
                    config.GetDoseRules
                        routeMappings
                        formRoutes
                        products
                let! solutionRules, solutionMsgs =
                    config.GetSolutionRules
                        routeMappings
                        parenteralMeds
                        products
                let! renalRules, renalMsgs = config.GetRenalRules ()

                return
                    {
                        UnitMappings = unitMappings
                        RouteMappings = routeMappings
                        ValidForms = validForms
                        FormRoutes = formRoutes
                        FormularyProducts = formularyProducts
                        Reconstitution = reconstitution
                        ParenteralMeds = parenteralMeds
                        EnteralFeeding = enteralFeeding
                        Products = products
                        DoseRules = doseRules
                        SolutionRules = solutionRules
                        RenalRules = renalRules
                        Messages =
                            [|
                                yield! unitMsgs
                                yield! routeMsgs
                                yield! formMsgs
                                yield! formRouteMsgs
                                yield! formularyMsgs
                                yield! reconstitutionMsgs
                                yield! parenteralMsgs
                                yield! enteralMsgs
                                yield! doseRuleMsgs
                                yield! solutionMsgs
                                yield! renalMsgs
                            |]
                        LastReloaded = DateTime.UtcNow
                        IsLoaded = true
                    }
            }
        with
        | exn ->
            [
                ("Failed to load resources", Some exn)
                |> ErrorMsg
            ]
            |> Error


    /// Load all resources at once using default configuration
    let loadAllResources dataUrlId =
        loadAllResourcesWithConfig (defaultResourceConfig dataUrlId)


    /// Create a test resource configuration with mock data
    let createTestResourceConfig
        (unitMappings: UnitMapping[])
        (routeMappings: RouteMapping[])
        (validForms: string[])
        (formRoutes: FormRoute[])
        (formularyProducts: FormularyProduct[])
        (reconstitution: Reconstitution[])
        (enteralFeeding: Product[])
        (parenteralMeds: Product[])
        =
        {
            GetUnitMappings = failwith "Not implemented"
            GetRouteMappings = failwith "Not implemented"
            GetValidForms = failwith "Not implemented"
            GetFormRoutes = failwith "Not implemented"
            GetFormularyProducts = failwith "Not implemented"
            GetReconstitution = failwith "Not implemented"
            GetEnteralFeeding = failwith "Not implemented"
            GetParenteralMeds = failwith "Not implemented"
            GetProducts = failwith "Not implemented"
            GetDoseRules = failwith "Not implemented"
            GetSolutionRules = failwith "Not implemented"
            GetRenalRules = failwith "Not implemented"
        }
        |> Ok


    /// Create a cached resource provider with TTL
    type CachedResourceProvider(loadAllResources: unit -> Result<ResourceState, Message list>, ttlMinutes: int option) =
        let mutable cachedState: (ResourceState * DateTime) option = None
        let lockObj = obj()

        let isExpired (timestamp: DateTime) =
            match ttlMinutes with
            | None -> false // No expiration if ttl is not set
            | Some ttlMinutes ->
                DateTime.UtcNow.Subtract(timestamp).TotalMinutes > float ttlMinutes

        let loadFresh () =
            match loadAllResources () with
            | Ok state ->
                cachedState <- Some (state, DateTime.UtcNow)
                state
            | Error msgs ->
                writeErrorMessage $"Failed to load resources: {msgs}"
                // Return empty state on error
                {
                    UnitMappings = [||]
                    RouteMappings = [||]
                    ValidForms = [||]
                    FormRoutes = [||]
                    FormularyProducts = [||]
                    Reconstitution = [||]
                    EnteralFeeding = [||]
                    ParenteralMeds = [||]
                    Products = [||]
                    DoseRules = [||]
                    SolutionRules = [||]
                    RenalRules = [||]
                    Messages = [| yield! msgs |> List.toArray |]
                    LastReloaded = DateTime.MinValue
                    IsLoaded = false
                }

        member private _.getFromCache (selector: ResourceState -> 'T) =
            lock lockObj (fun () ->
                match cachedState with
                | Some (state, timestamp) when not (isExpired timestamp) -> selector state
                | _ -> selector (loadFresh())
            )

        member _.ReloadCache() =
            lock lockObj (fun () ->
                cachedState <- None // Invalidate cache
                loadFresh() |> ignore // Load fresh data
            )

        interface IResourceProvider with
            member this.GetUnitMappings() = this.getFromCache _.UnitMappings
            member this.GetRouteMappings() = this.getFromCache _.RouteMappings
            member this.GetValidForms() = this.getFromCache _.ValidForms
            member this.GetFormRoutes() = this.getFromCache _.FormRoutes
            member this.GetFormularyProducts() = this.getFromCache _.FormularyProducts
            member this.GetReconstitution() = this.getFromCache _.Reconstitution
            member this.GetEnteralFeeding() = this.getFromCache _.EnteralFeeding
            member this.GetParenteralMeds() = this.getFromCache _.ParenteralMeds
            member this.GetProducts() = this.getFromCache _.Products
            member this.GetDoseRules() = this.getFromCache _.DoseRules
            member this.GetSolutionRules() = this.getFromCache _.SolutionRules
            member this.GetRenalRules() = this.getFromCache _.RenalRules
            member this.GetResourceInfo() =
                this.getFromCache (fun s -> {Messages = s.Messages; LastUpdated = s.LastReloaded; IsLoaded = s.IsLoaded })


module Api =

    open System
    open Informedica.Logging.Lib

    open Resources


    let private logGenFormMessages (logger: Logger) provider =
        (provider :> IResourceProvider).GetResourceInfo().Messages
        |> Array.iter (fun m ->
            match m with
            | Info _ -> Logging.logInfo logger m
            | Warning _ -> Logging.logWarning logger m
            | ErrorMsg _ -> Logging.logError logger m
        )


    let getCachedProviderWithDataUrlId (logger: Logger) dataUrlId =
            let provider = CachedResourceProvider((fun () -> loadAllResources dataUrlId), None)

            provider
            |> logGenFormMessages logger
            provider


    let reloadCache (logger: Logger) (provider : IResourceProvider) =
        match provider with
        | :? CachedResourceProvider as cachedProvider ->
            cachedProvider.ReloadCache()
            cachedProvider |> logGenFormMessages logger
        | _ -> failwith "Provider is not a CachedResourceProvider instance"


    let getRouteMapping (provider : IResourceProvider) = provider.GetRouteMappings()


    let getDoseRules (provider : IResourceProvider) = provider.GetDoseRules()


    let getSolutionRules (provider : IResourceProvider) = provider.GetSolutionRules()


    let getRenalRules (provider : IResourceProvider) = provider.GetRenalRules()


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


    let filterPrescriptionRules (provider: IResourceProvider) filter : GenFormResult<PrescriptionRule array> =
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
            async {
                return
                   PrescriptionRule.filter rules solutionRules renalRules routeMappings filter
            }
        )
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Utils.GenFormResult.foldResults

        //PrescriptionRule.filter doseRules solutionRules renalRules routeMappings filter
