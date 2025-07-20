namespace Informedica.GenForm.Lib


module Resources =

    open System
    open FsToolkit.ErrorHandling.ResultCE


    type IResourceProvider =
        abstract member GetUnitMappings : unit -> UnitMapping[]
        abstract member GetRouteMappings : unit -> RouteMapping[]
        abstract member GetValidShapes : unit -> string[]
        abstract member GetShapeRoutes : unit -> ShapeRoute[]
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
            ValidShapes: string []
            ShapeRoutes: ShapeRoute []
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
            member this.GetValidShapes() = this.ValidShapes
            member this.GetShapeRoutes() = this.ShapeRoutes
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
            member _.GetValidShapes() = state.ValidShapes
            member _.GetShapeRoutes() = state.ShapeRoutes
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
        validShapes
        shapeRoutes
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
            ValidShapes = validShapes
            ShapeRoutes = shapeRoutes
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
            GetUnitMappings: unit -> UnitMapping[]
            GetRouteMappings: unit -> Result<RouteMapping[], Message>
            GetValidShapes: unit -> string[]
            GetShapeRoutes: UnitMapping [] -> ShapeRoute[]
            GetFormularyProducts: unit -> FormularyProduct[]
            GetReconstitution: unit -> Reconstitution[]
            GetParenteralMeds: UnitMapping[] -> Product[]
            GetEnteralFeeding: UnitMapping[] -> Product[]
            GetProducts:
                UnitMapping[] ->
                RouteMapping[] ->
                string[] ->
                ShapeRoute[] ->
                Reconstitution[] ->
                Product[] ->
                Product[] ->
                FormularyProduct[] ->
                Product[]
            GetDoseRules:
                RouteMapping[] ->
                ShapeRoute[] ->
                Product[] ->
                DoseRule[]
            GetSolutionRules:
                RouteMapping[] ->
                Product[] ->
                Product[] ->
                SolutionRule[]
            GetRenalRules: unit -> RenalRule[]
        }


    /// Default resource configuration using the standard get functions
    let defaultResourceConfig dataUrlId =
        {
            GetUnitMappings = Mapping.getUnitMapping dataUrlId |> delay
            GetRouteMappings = Mapping.getRouteMappingResult dataUrlId |> delay
            GetValidShapes = Mapping.getValidShapes dataUrlId |> delay
            GetShapeRoutes = Mapping.getShapeRoutes dataUrlId
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
    let loadAllResourcesWithConfig (config: ResourceConfig) : Result<ResourceState, string> =
        try
            let unitMappings = config.GetUnitMappings()
            let routeMappings = config.GetRouteMappings()
            let validShapes = config.GetValidShapes()
            let shapeRoutes = config.GetShapeRoutes unitMappings
            let formularyProducts = config.GetFormularyProducts()
            let reconstitution = config.GetReconstitution ()
            let parenteralMeds = config.GetParenteralMeds unitMappings
            let enteralFeeding = config.GetEnteralFeeding unitMappings
            let products =
                result {
                    let! routeMappings = routeMappings
                    return
                        config.GetProducts
                            unitMappings
                            routeMappings
                            validShapes
                            shapeRoutes
                            reconstitution
                            parenteralMeds
                            enteralFeeding
                            formularyProducts
                }
            let doseRules =
                result {
                    let! routeMappings = routeMappings
                    let! products = products
                    return
                        config.GetDoseRules
                            routeMappings
                            shapeRoutes
                            products
                }
            let solutionRules =
                result {
                    let! routeMappings = routeMappings
                    let! products = products
                    return
                        config.GetSolutionRules
                            routeMappings
                            parenteralMeds
                            products
                }
            let renalRules =
                config.GetRenalRules ()

            {
                UnitMappings = unitMappings
                RouteMappings = routeMappings |> Result.defaultValue [||]
                ValidShapes = validShapes
                ShapeRoutes = shapeRoutes
                FormularyProducts = formularyProducts
                Reconstitution = reconstitution
                ParenteralMeds = parenteralMeds
                EnteralFeeding = enteralFeeding
                Products = products |> Result.defaultValue [||]
                DoseRules = doseRules |> Result.defaultValue [||]
                SolutionRules = solutionRules |> Result.defaultValue [||]
                RenalRules = renalRules
                Messages = [||]
                LastReloaded = DateTime.UtcNow
                IsLoaded = true
            }
            |> Ok
        with
        | ex -> Error $"Failed to load resources: {ex.Message}"


    /// Load all resources at once using default configuration
    let loadAllResources dataUrlId : Result<ResourceState, string> =
        loadAllResourcesWithConfig (defaultResourceConfig dataUrlId)


    /// Create a test resource configuration with mock data
    let createTestResourceConfig
        (unitMappings: UnitMapping[])
        (routeMappings: RouteMapping[])
        (validShapes: string[])
        (shapeRoutes: ShapeRoute[])
        (formularyProducts: FormularyProduct[])
        (reconstitution: Reconstitution[])
        (enteralFeeding: Product[])
        (parenteralMeds: Product[])
        =
        {
            GetUnitMappings = fun () -> unitMappings
            GetRouteMappings = routeMappings |> Ok |> delay
            GetValidShapes = fun () -> validShapes
            GetShapeRoutes = fun _ -> shapeRoutes
            GetFormularyProducts = fun () -> formularyProducts
            GetReconstitution = fun () -> reconstitution
            GetEnteralFeeding = fun _ -> enteralFeeding
            GetParenteralMeds = fun _ -> parenteralMeds
            GetProducts = failwith "Not implemented"
            GetDoseRules = failwith "Not implemented"
            GetSolutionRules = failwith "Not implemented"
            GetRenalRules = failwith "Not implemented"
        }


    /// Create a cached resource provider with TTL
    type CachedResourceProvider(loadAllResources: unit -> Result<ResourceState,string>, ttlMinutes: int option) =
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
            | Error _ ->
                // Return empty state on error
                {
                    UnitMappings = [||]
                    RouteMappings = [||]
                    ValidShapes = [||]
                    ShapeRoutes = [||]
                    FormularyProducts = [||]
                    Reconstitution = [||]
                    EnteralFeeding = [||]
                    ParenteralMeds = [||]
                    Products = [||]
                    DoseRules = [||]
                    SolutionRules = [||]
                    RenalRules = [||]
                    Messages = [||]
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
            member this.GetValidShapes() = this.getFromCache _.ValidShapes
            member this.GetShapeRoutes() = this.getFromCache _.ShapeRoutes
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
    open Resources


    let dataUrlId = Environment.GetEnvironmentVariable("GENPRES_URL_ID") |> Option.ofObj |> Option.defaultValue "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I"


    /// Default cached API provider instance
    let cachedApiProvider : IResourceProvider = CachedResourceProvider((fun () -> loadAllResources dataUrlId), None)


    let reloadCache() =
        match cachedApiProvider with
        | :? CachedResourceProvider as provider -> provider.ReloadCache()
        | _ -> failwith "Cached API provider is not a CachedResourceProvider instance"


    // Public API functions that use the cached provider
    let getUnitMappings () = cachedApiProvider.GetUnitMappings()
    let getRouteMappings () = cachedApiProvider.GetRouteMappings()
    let getValidShapes () = cachedApiProvider.GetValidShapes()
    let getShapeRoutes () = cachedApiProvider.GetShapeRoutes()
    let getFormularyProducts () = cachedApiProvider.GetFormularyProducts()
    let getReconstitution () = cachedApiProvider.GetReconstitution()
    let getEnteralFeeding () = cachedApiProvider.GetEnteralFeeding()
    let getParenteralMeds () = cachedApiProvider.GetParenteralMeds()
    let getProducts () = cachedApiProvider.GetProducts()
    let getDoseRules () = cachedApiProvider.GetDoseRules()
    let getSolutionRules () = cachedApiProvider.GetSolutionRules()
    let getRenalRules () = cachedApiProvider.GetRenalRules()
    let getResourceInfo () = cachedApiProvider.GetResourceInfo()

    // Filtering functions using cached mappings
    let filterDoseRules filter doseRules =
        let routeMappings = getRouteMappings()
        DoseRule.filter routeMappings filter doseRules

    let filterRenalRules filter renalRules =
        let routeMappings = getRouteMappings()
        RenalRule.filter routeMappings filter renalRules

    let filterProducts filter products =
        let routeMappings = getRouteMappings()
        Product.filter routeMappings filter products

    let reconstituteDoseRule department location doseRule =
        let routeMappings = getRouteMappings()
        DoseRule.reconstitute routeMappings department location doseRule

    let getPrescriptionRules =
        let doseRules = cachedApiProvider.GetDoseRules()
        let solutionRules = cachedApiProvider.GetSolutionRules()
        let routeMappings = cachedApiProvider.GetRouteMappings()
        let renalRules = cachedApiProvider.GetRenalRules()

        PrescriptionRule.getForPatient doseRules solutionRules renalRules routeMappings

    // Add to Api module
    let filterDoseRulesWithFilter filter =
        getDoseRules()
        |> filterDoseRules filter

    let filterPrescriptionRules filter =
        let doseRules = getDoseRules()
        let solutionRules = getSolutionRules()
        let routeMappings = getRouteMappings()
        let renalRules = getRenalRules()

        PrescriptionRule.filter doseRules solutionRules renalRules routeMappings filter


    let getAllRenalRules () = getRenalRules()