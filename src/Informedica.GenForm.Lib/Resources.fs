namespace Informedica.GenForm.Lib


module Resources =

    open System


    type ResourceState =
        {
            UnitMappings: UnitMapping []
            RouteMappings: RouteMapping []
            ShapeRoutes: ShapeRoute []
            Products: Product []
            Reconstitution: Reconstitution []
            DoseRules: DoseRule []
            SolutionRules: SolutionRule []
            RenalRules: RenalRule []
            IsLoaded : bool
            LastReloaded: DateTime
        }
    with
        interface IResourceProvider with
            member this.GetDoseRules() = this.DoseRules
            member this.GetSolutionRules() = this.SolutionRules
            member this.GetRenalRules() = this.RenalRules
            member this.GetProducts() = this.Products
            member this.GetReconstitution() = this.Reconstitution 
            member this.GetShapeRoutes() = this.ShapeRoutes
            member this.GetUnitMappings() = this.UnitMappings
            member this.GetRouteMappings() = this.RouteMappings
            member this.GetResourceInfo() = {
                LastUpdated = this.LastReloaded
                IsLoaded = this.IsLoaded
            }            


    type ResourceProvider(state: ResourceState) =
        interface IResourceProvider with
            member _.GetDoseRules() = state.DoseRules
            member _.GetSolutionRules() = state.SolutionRules
            member _.GetRenalRules() = state.RenalRules
            member _.GetProducts() = state.Products
            member _.GetReconstitution() = state.Reconstitution
            member _.GetShapeRoutes() = state.ShapeRoutes
            member _.GetUnitMappings() = state.UnitMappings
            member _.GetRouteMappings() = state.RouteMappings
            member _.GetResourceInfo() = {
                LastUpdated = state.LastReloaded
                IsLoaded = state.IsLoaded
            }



    let createProvider 
        doseRules 
        solutionRules 
        renalRules 
        products 
        reconstitution
        shapeRoutes 
        unitMappings 
        routeMappings =
        let state = {
            DoseRules = doseRules
            SolutionRules = solutionRules
            RenalRules = renalRules
            Products = products
            Reconstitution = reconstitution 
            ShapeRoutes = shapeRoutes
            UnitMappings = unitMappings
            RouteMappings = routeMappings
            LastReloaded = DateTime.UtcNow
            IsLoaded = true
        }

        ResourceProvider state :> IResourceProvider


    type ResourceConfig = {
        GetDoseRules: unit -> DoseRule[]
        GetSolutionRules: unit -> SolutionRule[]
        GetRenalRules: unit -> RenalRule[]
        GetProducts: unit -> Product[]
        GetReconstitution: unit -> Reconstitution[]
        GetShapeRoutes: unit -> ShapeRoute[]
        GetUnitMappings: unit -> UnitMapping[]
        GetRouteMappings: unit -> RouteMapping[]
    }

    (*
    /// Default resource configuration using the standard get functions
    let defaultResourceConfig = {
        GetDoseRules = DoseRule.get
        GetSolutionRules = SolutionRule.get
        GetRenalRules = RenalRule.get
        GetProducts = Product.get
        GetShapeRoutes = ShapeRoute.get
        GetUnitMappings = UnitMapping.get
        GetRouteMappings = RouteMapping.get
    }
    *)


    /// Load all resources at once using provided configuration
    let loadAllResourcesWithConfig (config: ResourceConfig) : Result<ResourceState, string> =
        try
            let doseRules = config.GetDoseRules()
            let solutionRules = config.GetSolutionRules()
            let renalRules = config.GetRenalRules()
            let products = config.GetProducts()
            let reconstitution = config.GetReconstitution ()
            let shapeRoutes = config.GetShapeRoutes()
            let unitMappings = config.GetUnitMappings()
            let routeMappings = config.GetRouteMappings()
            
            {
                DoseRules = doseRules
                SolutionRules = solutionRules
                RenalRules = renalRules
                Products = products
                Reconstitution = reconstitution
                ShapeRoutes = shapeRoutes
                UnitMappings = unitMappings
                RouteMappings = routeMappings
                LastReloaded = DateTime.UtcNow
                IsLoaded = true
            }
            |> Ok
        with
        | ex -> Error $"Failed to load resources: {ex.Message}"

    (*
    /// Load all resources at once using default configuration
    let loadAllResources () : Result<ResourceState, string> =
        loadAllResourcesWithConfig defaultResourceConfig
    *)


    /// Create a test resource configuration with mock data
    let createTestResourceConfig 
        (doseRules: DoseRule[]) 
        (solutionRules: SolutionRule[])
        (renalRules: RenalRule[])
        (products: Product[])
        (reconstitution: Reconstitution[])
        (shapeRoutes: ShapeRoute[])
        (unitMappings: UnitMapping[])
        (routeMappings: RouteMapping[]) =
        {
            GetDoseRules = fun () -> doseRules
            GetSolutionRules = fun () -> solutionRules
            GetRenalRules = fun () -> renalRules
            GetProducts = fun () -> products
            GetReconstitution = fun () -> reconstitution
            GetShapeRoutes = fun () -> shapeRoutes
            GetUnitMappings = fun () -> unitMappings
            GetRouteMappings = fun () -> routeMappings
        }

    /// Create a cached resource provider with TTL
    type CachedResourceProvider(loadAllResources, ttlMinutes: int option) =
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
                    DoseRules = [||]
                    SolutionRules = [||]
                    RenalRules = [||]
                    Products = [||]
                    Reconstitution = [||]
                    ShapeRoutes = [||]
                    UnitMappings = [||]
                    RouteMappings = [||]
                    LastReloaded = DateTime.MinValue
                    IsLoaded = false
                }
        
        interface IResourceProvider with
            member _.GetDoseRules() =
                lock lockObj (fun () ->
                    match cachedState with
                    | Some (state, timestamp) when not (isExpired timestamp) -> state.DoseRules
                    | _ -> (loadFresh()).DoseRules
                )
            
            member _.GetSolutionRules() =
                lock lockObj (fun () ->
                    match cachedState with
                    | Some (state, timestamp) when not (isExpired timestamp) -> state.SolutionRules
                    | _ -> (loadFresh()).SolutionRules
                )
            
            member _.GetRenalRules() =
                lock lockObj (fun () ->
                    match cachedState with
                    | Some (state, timestamp) when not (isExpired timestamp) -> state.RenalRules
                    | _ -> (loadFresh()).RenalRules
                )
                
            member _.GetProducts() =
                lock lockObj (fun () ->
                    match cachedState with
                    | Some (state, timestamp) when not (isExpired timestamp) -> state.Products
                    | _ -> (loadFresh()).Products
                )
                
            member _.GetShapeRoutes() =
                lock lockObj (fun () ->
                    match cachedState with
                    | Some (state, timestamp) when not (isExpired timestamp) -> state.ShapeRoutes
                    | _ -> (loadFresh()).ShapeRoutes
                )
                
            member _.GetUnitMappings() =
                lock lockObj (fun () ->
                    match cachedState with
                    | Some (state, timestamp) when not (isExpired timestamp) -> state.UnitMappings
                    | _ -> (loadFresh()).UnitMappings
                )
                
            member _.GetRouteMappings() =
                lock lockObj (fun () ->
                    match cachedState with
                    | Some (state, timestamp) when not (isExpired timestamp) -> state.RouteMappings
                    | _ -> (loadFresh()).RouteMappings
                )
                
            member _.GetReconstitution () = 
                lock lockObj (fun () ->
                    match cachedState with
                    | Some (state, timestamp) when not (isExpired timestamp) -> state.Reconstitution
                    | _ -> (loadFresh()).Reconstitution
                )
                
            member _.GetResourceInfo() =
                lock lockObj (fun () ->
                    match cachedState with
                    | Some (state, timestamp) when not (isExpired timestamp) -> 
                        { LastUpdated = state.LastReloaded; IsLoaded = state.IsLoaded }
                    | _ -> 
                        let freshState = loadFresh()
                        { LastUpdated = freshState.LastReloaded; IsLoaded = freshState.IsLoaded }
                )


