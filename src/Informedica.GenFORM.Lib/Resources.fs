namespace Informedica.GenForm.Lib


module Resources =

    open System
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime

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


    /// Phantom-typed handle: names a resource and carries its value type `'T`.
    type ResourceKey<'T> = { Name: string }

    module ResourceKey =
        let create<'T> name : ResourceKey<'T> = { Name = name }


    /// Available to a loader while loading: resolve other resources (its deps).
    [<Interface>]
    type IResourceResolver =
        abstract member Get: ResourceKey<'T> -> 'T


    /// A loader: given the resolver (for deps), yield the boxed value + non-fatal
    /// warnings, or fail with fatal errors. Boxing is the price of a heterogeneous
    /// registry; it is hidden behind typed keys so call sites stay type-safe.
    type ResourceLoader = IResourceResolver -> Result<obj * Message list, Message list>

    /// The registry. Adding a resource = add ONE entry; the engine never changes.
    type ResourceRegistry = Map<string, ResourceLoader>


    /// Resource provider abstraction. Returns the fully built, in-memory
    /// resource collections. Product collections are v2 `ProductComponent`s.
    type IResourceProvider =
        /// Generic seam: resolve any registered resource by its typed key.
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
        abstract member GetResourceInfo: unit -> ResourceInfo

    and ResourceInfo =
        {
            Messages: Message[]
            LastUpdated: DateTime
            IsLoaded: bool
        }


    /// Standalone v2 resource state. The raw, source-loaded data lives in the
    /// nested `Data` record; the built domain collections live at the top level.
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


    /// Lazy, memoised, dependency-ordered resolution with cycle detection and
    /// warning accumulation. The one place an unsafe downcast occurs, guarded by
    /// the typed key.
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

        /// Resolve a single resource at its static type (memoised).
        member this.Resolve(key: ResourceKey<'T>) : 'T = this.ResolveObj key.Name :?> 'T

        /// Resolve every registered resource (used to build the full snapshot).
        member this.ForceAll() =
            registry |> Map.iter (fun name _ -> this.ResolveObj name |> ignore)

        member _.Warnings = warnings |> List.ofSeq
        member _.Resolved = cache |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq

        interface IResourceResolver with
            member this.Get key = this.Resolve key


    /// IO leaf resource: a thunk returning Result (no dependencies).
    let ofResult (f: unit -> Result<'T, Message list>) : ResourceLoader =
        fun _ -> f () |> Result.map (fun v -> box v, [])

    /// Derived resource that uses dependencies, no warnings.
    let derive (f: IResourceResolver -> 'T) : ResourceLoader = fun r -> Ok(box (f r), [])

    /// Derived resource that uses dependencies and also emits warnings.
    let deriveWith (f: IResourceResolver -> 'T * Message list) : ResourceLoader =
        fun r ->
            let v, ws = f r
            Ok(box v, ws)


    let private messageText =
        function
        | Info s
        | Warning s -> s
        | ErrorMsg(s, _) -> s


    /// <summary>
    /// An <b>optional</b> IO leaf resource: one whose failure must not fail the whole
    /// load, and so empty every other resource with it.
    /// </summary>
    /// <remarks>
    /// On error the load succeeds with <paramref name="fallback"/>, and each error
    /// becomes a <c>Warning</c> prefixed with <paramref name="note"/> — so the failure
    /// still surfaces in <c>ResourceInfo.Messages</c> where an admin can see it, and an
    /// admin ReloadResources retries the fetch.
    /// </remarks>
    /// <param name="note">Prefix explaining what was not loaded and what the effect is.</param>
    /// <param name="fallback">The degraded value to serve instead.</param>
    /// <param name="f">The fetch.</param>
    let ofResultOrDefault (note: string) (fallback: 'T) (f: unit -> Result<'T, Message list>) : ResourceLoader =
        fun _ ->
            match f () with
            | Ok v -> Ok(box v, [])
            | Error msgs -> Ok(box fallback, msgs |> List.map (fun m -> Warning $"%s{note}: %s{messageText m}"))


    /// Typed keys — one per resource. Adding a resource adds a key here.
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
        let renalRuleData = ResourceKey.create<RenalRuleData[]> "renalRuleData"
        let totalsData = ResourceKey.create<TotalsData[]> "totalsData"
        let products = ResourceKey.create<ProductComponent[]> "products"
        let doseRules = ResourceKey.create<DoseRule[]> "doseRules"
        let solutionRules = ResourceKey.create<SolutionRule[]> "solutionRules"
        let renalRules = ResourceKey.create<RenalRule[]> "renalRules"
        // G-Standaard dose-rule capability, served as a function-valued resource.
        let gStandProvider = ResourceKey.create<Check.GStandProvider> "gStandProvider"

        // Nederlands Kinder Formularium link lookup, likewise function-valued.
        let nkfLinkProvider = ResourceKey.create<Source.LinkProvider> "nkfLinkProvider"


    /// Default resource registry using the standard v2 get functions.
    ///
    /// Every entry is a loader; dependencies are declared by calling `r.Get` and
    /// resolved lazily and once by the engine (replacing the old hand-ordered CE).
    /// FormularyProducts is one resource that ParenteralMeds / EnteralFeeding /
    /// Products depend on, so it is fetched exactly once (no explicit `lazy`).
    let defaultRegistry dataUrlId : ResourceRegistry =
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

                // Parenteral and enteral products are NOT loaded from sheets of
                // their own: they are the Formulary rows whose Type says so, turned
                // into ProductComponents with the Formulary nutrition columns as
                // their substances.
                Keys.parenteralMeds.Name,
                derive (fun r -> r.Get Keys.formularyProducts |> Product.Parenteral.get (r.Get Keys.unitMappings))

                Keys.enteralFeeding.Name,
                derive (fun r -> r.Get Keys.formularyProducts |> Product.Enteral.get (r.Get Keys.unitMappings))

                // IO edge: read raw source data once.
                Keys.genPresProducts.Name,
                ofResult (fun () ->
                    try
                        Informedica.ZIndex.Lib.GenPresProduct.get [] |> Ok
                    with exn ->
                        Utils.Result.createError "GetGenPresProducts" exn
                )

                Keys.doseRuleData.Name, ofResult (fun () -> DoseRuleLoader.getData dataUrlId)
                Keys.solutionRuleData.Name, ofResult (fun () -> SolutionRule.getData dataUrlId)
                Keys.renalRuleData.Name, ofResult (fun () -> RenalRule.getData dataUrlId)

                // Totals is an optional intake-reference resource: a load failure must
                // not empty the others, so swallow to [||] and surface a Warning.
                Keys.totalsData.Name,
                ofResultOrDefault
                    "Totals resource not loaded"
                    ([||]: TotalsData[])
                    (fun () -> Mapping.getTotals dataUrlId)

                // Narrow the raw ZIndex products to those referenced by dose rules
                // BEFORE building, so discarded products are never constructed.
                Keys.products.Name,
                derive (fun r ->
                    let filtered =
                        r.Get Keys.genPresProducts
                        |> Product.filterGenPresProductsByData
                            (r.Get Keys.routeMappings)
                            (r.Get Keys.formularyProducts)
                            (r.Get Keys.doseRuleData)

                    Product.fromGenPresProducts
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

                // Built rules carry product-matching warnings -> ResourceState.Messages.
                Keys.doseRules.Name,
                deriveWith (fun r ->
                    DoseRuleLoader.fromData
                        (r.Get Keys.routeMappings)
                        (r.Get Keys.formRoutes)
                        (r.Get Keys.products)
                        (r.Get Keys.doseRuleData)
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

                Keys.renalRules.Name,
                (fun r -> RenalRule.map (r.Get Keys.renalRuleData) |> Result.map (fun v -> box v, []))

                // G-Standaard dose rules served as a function-valued resource: the
                // closure depends only on routeMappings; ZIndex caches stay memoised
                // in ZIndex.Lib and patient filtering (RuleFinder) is untouched.
                Keys.gStandProvider.Name, derive (fun r -> Check.gStandProvider (r.Get Keys.routeMappings))

                // The NKF link lookup is decoration on a rendered dose rule, and it is the
                // only resource fetched from a third party rather than a sheet, so it is
                // optional in exactly the sense totalsData is. Registering it here rather
                // than caching inside DoseRule.Print keeps that module pure, puts the
                // failure in ResourceInfo where an admin can see it, and lets
                // ReloadResources retry.
                //
                // Degrade to `getLink []`, not `Source.noLinks`: an empty index costs the
                // NKF links, which is the outage, but FTK links are built from the generic
                // name alone and must survive it.
                Keys.nkfLinkProvider.Name,
                ofResultOrDefault
                    "NKF links not loaded, falling back to \"*Lokaal*\""
                    (Source.getLink []: Source.LinkProvider)
                    (fun () ->
                        SourceLoader.fetchNKFMedications ()
                        |> Result.map (fun meds -> (Source.getLink meds: Source.LinkProvider))
                    )
            ]


    /// The resolved resource set: the typed `ResourceState` snapshot (the stable
    /// facade for existing consumers) plus the boxed map of every resolved
    /// resource (serves the generic `Get` seam and function-valued resources).
    type LoadedResources =
        {
            State: ResourceState
            Resolved: Map<string, obj>
        }


    /// Load all resources at once using the provided registry.
    let loadAllResourcesWithRegistry (registry: ResourceRegistry) : Result<LoadedResources, Message list> =
        try
            let eng = LoadEngine registry
            // Resolve everything (incl. gStandProvider); fail-fast on a fatal Error.
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


    /// Load all resources at once using the default registry.
    let loadAllResources dataUrlId =
        loadAllResourcesWithRegistry (defaultRegistry dataUrlId)


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

            member _.GetResourceInfo() = resourceInfo state


    /// Create a cached resource provider with an optional TTL (in minutes).
    type CachedResourceProvider(loadAllResources: unit -> Result<LoadedResources, Message list>, ttlMinutes: int option)
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
                writeErrorMessage $"Failed to load resources: {msgs}"
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
                        Resolved = Map.empty
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
            member this.Get(key: ResourceKey<'T>) : 'T =
                this.getFromCache (fun l -> l.Resolved[key.Name] :?> 'T)

            member this.GetData() = this.getFromCache _.State.Data

            member this.GetUnitMappings() =
                this.getFromCache _.State.Data.UnitMappings

            member this.GetRouteMappings() =
                this.getFromCache _.State.Data.RouteMappings

            member this.GetValidForms() =
                this.getFromCache _.State.Data.ValidForms

            member this.GetFormRoutes() =
                this.getFromCache _.State.Data.FormRoutes

            member this.GetFormularyProducts() =
                this.getFromCache _.State.Data.FormularyProducts

            member this.GetReconstitution() =
                this.getFromCache _.State.Reconstitution

            member this.GetEnteralFeeding() =
                this.getFromCache _.State.EnteralFeeding

            member this.GetParenteralMeds() =
                this.getFromCache _.State.ParenteralMeds

            member this.GetProducts() = this.getFromCache _.State.Products
            member this.GetDoseRules() = this.getFromCache _.State.DoseRules
            member this.GetSolutionRules() = this.getFromCache _.State.SolutionRules
            member this.GetRenalRules() = this.getFromCache _.State.RenalRules

            member this.GetTotals() =
                this.getFromCache _.State.Data.TotalsData

            member this.GetGStandProvider() =
                this.getFromCache (fun l -> l.Resolved[Keys.gStandProvider.Name] :?> Check.GStandProvider)

            member this.GetResourceInfo() =
                this.getFromCache (fun l -> resourceInfo l.State)
