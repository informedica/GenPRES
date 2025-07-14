
#load "load.fsx"

#r "../bin/Debug/net9.0/Informedica.GenForm.Lib.dll"


open System
open Informedica.GenForm.Lib

Environment.SetEnvironmentVariable("GENPRES_URL_ID", "1s76xvQJXhfTpV15FuvTZfB-6pkkNTpSB30p51aAca8I")


module Resources =

    type IResourceProvider =
        abstract member GetUnitMappings : unit -> UnitMapping[]
        abstract member GetRouteMappings : unit -> RouteMapping[]
        abstract member GetValidShapes : unit -> string[]
        abstract member GetShapeRoutes : unit -> ShapeRoute[]
        abstract member GetFormularyProducts : unit -> FormularyProduct[]
        abstract member GetReconstitution : unit -> Reconstitution[]
        abstract member GetEnteralFeeding : unit -> Product[]
        abstract member GetParenteralMeds : unit -> Product[]
        abstract member GetProducts : unit -> Product[]
        abstract member GetDoseRules : unit -> DoseRule[]
        abstract member GetSolutionRules : unit -> SolutionRule[]
        abstract member GetRenalRules : unit -> RenalRule[]
        abstract member GetPrescriptionRules : unit -> PrescriptionRule[]
        abstract member GetResourceInfo : unit -> ResourceInfo

    and ResourceInfo = {
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
            EnteralFeeding: Product []
            ParenteralMeds: Product []
            Products: Product []
            DoseRules: DoseRule []
            SolutionRules: SolutionRule []
            RenalRules: RenalRule []
            PrescriptionRules: PrescriptionRule []
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
            member this.GetEnteralFeeding() = this.EnteralFeeding
            member this.GetParenteralMeds() = this.ParenteralMeds
            member this.GetProducts() = this.Products
            member this.GetDoseRules() = this.DoseRules
            member this.GetSolutionRules() = this.SolutionRules
            member this.GetRenalRules() = this.RenalRules
            member this.GetPrescriptionRules() = this.PrescriptionRules
            member this.GetResourceInfo() = {
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
            member _.GetEnteralFeeding() = state.EnteralFeeding
            member _.GetParenteralMeds() = state.ParenteralMeds
            member _.GetProducts() = state.Products
            member _.GetDoseRules() = state.DoseRules
            member _.GetSolutionRules() = state.SolutionRules
            member _.GetRenalRules() = state.RenalRules
            member _.GetPrescriptionRules() = state.PrescriptionRules
            member _.GetResourceInfo() = {
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
        enteralFeeding
        parenteralMeds
        products
        doseRules
        solutionRules
        renalRules
        prescriptionRules
        =
        let state = {
            UnitMappings = unitMappings
            RouteMappings = routeMappings
            ValidShapes = validShapes
            ShapeRoutes = shapeRoutes
            FormularyProducts = formularyProducts
            Reconstitution = reconstitution
            EnteralFeeding = enteralFeeding
            ParenteralMeds = parenteralMeds
            Products = products
            DoseRules = doseRules
            SolutionRules = solutionRules
            RenalRules = renalRules
            PrescriptionRules = prescriptionRules
            LastReloaded = DateTime.UtcNow
            IsLoaded = true
        }

        ResourceProvider state :> IResourceProvider

    type ResourceConfig =
        {
            GetUnitMappings: unit -> UnitMapping[]
            GetRouteMappings: unit -> RouteMapping[]
            GetValidShapes: unit -> string[]
            GetShapeRoutes: UnitMapping [] -> ShapeRoute[]
            GetFormularyProducts: unit -> FormularyProduct[]
            GetReconstitution: unit -> Reconstitution[]
            GetEnteralFeeding: UnitMapping[] -> Product[]
            GetParenteralMeds: UnitMapping[] -> Product[]
            GetProducts:
                UnitMapping ->
                RouteMapping ->
                string[] ->
                ShapeRoute[] ->
                Reconstitution[] ->
                Product[] ->
                Product[] ->
                FormularyProduct[] ->
                Product[]
            GetDoseRules: RouteMapping[] -> ShapeRoute[] -> DoseRule[]
            GetSolutionRules: Product[] -> SolutionRule[]
            GetRenalRules: unit -> RenalRule[]
            GetPrescriptionRules: unit -> PrescriptionRule[]
        }


    /// Default resource configuration using the standard get functions
    let defaultResourceConfig =
        let dataUrlId = Environment.GetEnvironmentVariable "GENPRES_URL_ID"
        {
            GetUnitMappings = fun () -> Mapping.getUnitMappingWithDataUrlId dataUrlId
            GetRouteMappings = fun () -> Mapping.getRouteMappingWithDataUrlId dataUrlId
            GetValidShapes = fun () -> Mapping.getValidShapesWithDataUrlId dataUrlId
            GetShapeRoutes = fun unitMapping -> Mapping.getShapeRoutesWithDataUrlId unitMapping dataUrlId
            GetFormularyProducts = fun () -> [||]
            GetReconstitution =
                fun () -> Product.Reconstitution.getWithDataUrlId dataUrlId
            GetEnteralFeeding =
                fun unitMapping -> Product.Enteral.getWithUnitMappingAndDataUrlId unitMapping dataUrlId
            GetParenteralMeds =
                fun unitMapping -> Product.Parenteral.getWithUnitMappingAndDataUrlId unitMapping dataUrlId
            GetProducts = fun () -> [||]
            GetDoseRules = fun () -> [||]
            GetSolutionRules = fun () -> [||]
            GetRenalRules = fun () -> [||]
            GetPrescriptionRules = fun () -> [||]
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
            let enteralFeeding = config.GetEnteralFeeding unitMappings
            let parenteralMeds = config.GetParenteralMeds unitMappings
            let products = config.GetProducts()
            let doseRules = config.GetDoseRules()
            let solutionRules = config.GetSolutionRules()
            let renalRules = config.GetRenalRules()
            let prescriptionRules = config.GetPrescriptionRules()

            {
                UnitMappings = unitMappings
                RouteMappings = routeMappings
                ValidShapes = validShapes
                ShapeRoutes = shapeRoutes
                FormularyProducts = formularyProducts
                Reconstitution = reconstitution
                EnteralFeeding = enteralFeeding
                ParenteralMeds = parenteralMeds
                Products = products
                DoseRules = doseRules
                SolutionRules = solutionRules
                RenalRules = renalRules
                PrescriptionRules = prescriptionRules
                LastReloaded = DateTime.UtcNow
                IsLoaded = true
            }
            |> Ok
        with
        | ex -> Error $"Failed to load resources: {ex.Message}"

    /// Load all resources at once using default configuration
    let loadAllResources () : Result<ResourceState, string> =
        loadAllResourcesWithConfig defaultResourceConfig

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
        (products: Product[])
        (doseRules: DoseRule[])
        (solutionRules: SolutionRule[])
        (renalRules: RenalRule[])
        (prescriptionRules: PrescriptionRule[])
        =
        {
            GetUnitMappings = fun () -> unitMappings
            GetRouteMappings = fun () -> routeMappings
            GetValidShapes = fun () -> validShapes
            GetShapeRoutes = fun _ -> shapeRoutes
            GetFormularyProducts = fun () -> formularyProducts
            GetReconstitution = fun () -> reconstitution
            GetEnteralFeeding = fun _ -> enteralFeeding
            GetParenteralMeds = fun _ -> parenteralMeds
            GetProducts = fun () -> products
            GetDoseRules = fun () -> doseRules
            GetSolutionRules = fun () -> solutionRules
            GetRenalRules = fun () -> renalRules
            GetPrescriptionRules = fun () -> prescriptionRules
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
                    PrescriptionRules = [||]
                    LastReloaded = DateTime.MinValue
                    IsLoaded = false
                }

        member private _.getFromCache (selector: ResourceState -> 'T) =
            lock lockObj (fun () ->
                match cachedState with
                | Some (state, timestamp) when not (isExpired timestamp) -> selector state
                | _ -> selector (loadFresh())
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
            member this.GetPrescriptionRules() = this.getFromCache _.PrescriptionRules
            member this.GetResourceInfo() = this.getFromCache (fun s -> { LastUpdated = s.LastReloaded; IsLoaded = s.IsLoaded })


open Resources


let cachedProvider : IResourceProvider = CachedResourceProvider(loadAllResources, None)


cachedProvider.GetResourceInfo()


module Api =

    open Resources

    /// Default cached API provider instance
    let cachedApiProvider : IResourceProvider = CachedResourceProvider(loadAllResources, None)

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
    let getPrescriptionRules () = cachedApiProvider.GetPrescriptionRules()
    let getResourceInfo () = cachedApiProvider.GetResourceInfo()

    // Filtering functions using cached mappings
    let filterDoseRules filter doseRules =
        let routeMappings = getRouteMappings()
        DoseRule.filterWithMapping routeMappings filter doseRules

    let filterRenalRules filter renalRules =
        let routeMappings = getRouteMappings()
        RenalRule.filterWithMapping routeMappings filter renalRules

    let filterProducts filter products =
        let routeMappings = getRouteMappings()
        Product.filterWithMapping routeMappings filter products

    let reconstituteDoseRule department location doseRule =
        let routeMappings = getRouteMappings()
        DoseRule.reconstituteWithMapping routeMappings department location doseRule