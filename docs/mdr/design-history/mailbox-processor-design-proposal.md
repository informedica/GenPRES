# MailboxProcessor Design Proposal for GenOrder.Lib API Integration

## Overview

This document proposes implementing a MailboxProcessor to handle all calls to the Informedica.GenOrder.Lib.API instead of directly calling the API functions. The design emphasizes resource management with reloading capabilities rather than relying on memoized functions, using a clean resource interface for dependency injection and pure function design.

## Design Goals

1. **Centralized Resource Management**: Load all necessary resources once and reuse them
2. **Thread Safety**: Ensure single-threaded access to resources using MailboxProcessor
3. **Resource Reloading**: Enable on-demand and automatic resource reloading
4. **Error Isolation**: Prevent failures in one command from affecting the processor state
5. **Monitoring**: Provide health checks and processing statistics
6. **Scalability**: Implement queue management to prevent memory issues under load
7. **Pure Functions**: Use dependency injection to make API functions pure and testable
8. **Interface Abstraction**: Abstract resource access through clean interfaces

## Core Architecture

### 1. Resource Interface Design

```fsharp
module Resources =
    
    type IResourceProvider =
        abstract member GetDoseRules : unit -> DoseRule[]
        abstract member GetSolutionRules : unit -> SolutionRule[]
        abstract member GetRenalRules : unit -> RenalRule[]
        abstract member GetProductRules : unit -> ProductRule[]
        
    type ResourceState = {
        DoseRules: DoseRule[]
        SolutionRules: SolutionRule[]
        RenalRules: RenalRule[]
        ProductRules: ProductRule[]
        LastReloaded: DateTime
        IsLoaded: bool
    }
    with
        interface IResourceProvider with
            member this.GetDoseRules() = this.DoseRules
            member this.GetSolutionRules() = this.SolutionRules
            member this.GetRenalRules() = this.RenalRules
            member this.GetProductRules() = this.ProductRules
    
    type ResourceProvider(state: ResourceState) =
        interface IResourceProvider with
            member _.GetDoseRules() = state.DoseRules
            member _.GetSolutionRules() = state.SolutionRules
            member _.GetRenalRules() = state.RenalRules
            member _.GetProductRules() = state.ProductRules
    
    // Default implementation that calls the original functions
    type DefaultResourceProvider() =
        interface IResourceProvider with
            member _.GetDoseRules() = DoseRule.get()
            member _.GetSolutionRules() = SolutionRule.get()
            member _.GetRenalRules() = RenalRule.get()
            member _.GetProductRules() = ProductRule.get()
    
    /// Create a resource provider from arrays
    let createProvider doseRules solutionRules renalRules productRules =
        let state = {
            DoseRules = doseRules
            SolutionRules = solutionRules
            RenalRules = renalRules
            ProductRules = productRules
            LastReloaded = DateTime.UtcNow
            IsLoaded = true
        }
        ResourceProvider(state) :> IResourceProvider
    
    /// Load all resources at once
    let loadAllResources () : Result<ResourceState, string> =
        try
            let doseRules = DoseRule.get()
            let solutionRules = SolutionRule.get()
            let renalRules = RenalRule.get()
            let productRules = ProductRule.get()
            
            {
                DoseRules = doseRules
                SolutionRules = solutionRules
                RenalRules = renalRules
                ProductRules = productRules
                LastReloaded = DateTime.UtcNow
                IsLoaded = true
            }
            |> Ok
        with
        | ex -> Error $"Failed to load resources: {ex.Message}"
```

### 2. Core MailboxProcessor Design

```fsharp
module OrderProcessor =

    open System
    open Informedica.GenOrder.Lib
    open Informedica.GenForm.Lib
    open Informedica.Utils.Lib
    open Shared.Api

    type ProcessorMessage =
        | OrderContextMessage of OrderContextCmd * AsyncReplyChannel<Result<OrderContextResp, string []>>
        | FormularyMessage of Formulary * AsyncReplyChannel<Result<FormularyResp, string []>>
        | ParenteraliaMessage of Parenteralia * AsyncReplyChannel<Result<ParentaraliaResp, string>>
        | TreatmentPlanMessage of TreatmentPlanCmd * AsyncReplyChannel<Result<TreatmentPlanResp, string []>>
        | ReloadResources of AsyncReplyChannel<Result<unit, string>>
        | CheckResourcesHealth of AsyncReplyChannel<Resources.ResourceState>
        | Shutdown

    type ProcessorState = {
        Resources: Resources.ResourceState
        IsProcessing: bool
        ProcessingCount: int64
        LastActivity: DateTime
    }

    let private createProcessor () =
        MailboxProcessor<ProcessorMessage>.Start(fun inbox ->
            let rec loop state =
                async {
                    let! msg = inbox.Receive()
                    
                    match msg with
                    | ReloadResources replyChannel ->
                        match Resources.loadAllResources() with
                        | Ok newResources ->
                            let newState = { state with Resources = newResources }
                            replyChannel.Reply(Ok ())
                            return! loop newState
                        | Error err ->
                            replyChannel.Reply(Error err)
                            return! loop state
                    
                    | CheckResourcesHealth replyChannel ->
                        replyChannel.Reply(state.Resources)
                        return! loop state
                    
                    | OrderContextMessage (cmd, replyChannel) ->
                        let newState = { 
                            state with 
                                IsProcessing = true
                                ProcessingCount = state.ProcessingCount + 1L
                                LastActivity = DateTime.UtcNow 
                        }
                        
                        try
                            let resourceProvider = state.Resources :> Resources.IResourceProvider
                            let result = processOrderContextCommand resourceProvider cmd
                            replyChannel.Reply(result)
                        with
                        | ex -> replyChannel.Reply(Error [| ex.Message |])
                        
                        let finalState = { newState with IsProcessing = false }
                        return! loop finalState
                    
                    | FormularyMessage (form, replyChannel) ->
                        let newState = { 
                            state with 
                                IsProcessing = true
                                ProcessingCount = state.ProcessingCount + 1L
                                LastActivity = DateTime.UtcNow 
                        }
                        
                        try
                            let resourceProvider = state.Resources :> Resources.IResourceProvider
                            let result = processFormularyCommand resourceProvider form
                            replyChannel.Reply(result)
                        with
                        | ex -> replyChannel.Reply(Error [| ex.Message |])
                        
                        let finalState = { newState with IsProcessing = false }
                        return! loop finalState
                    
                    | ParenteraliaMessage (par, replyChannel) ->
                        let newState = { 
                            state with 
                                IsProcessing = true
                                ProcessingCount = state.ProcessingCount + 1L
                                LastActivity = DateTime.UtcNow 
                        }
                        
                        try
                            let resourceProvider = state.Resources :> Resources.IResourceProvider
                            let result = processParenteraliaCommand resourceProvider par
                            replyChannel.Reply(result)
                        with
                        | ex -> replyChannel.Reply(Error ex.Message)
                        
                        let finalState = { newState with IsProcessing = false }
                        return! loop finalState
                    
                    | TreatmentPlanMessage (cmd, replyChannel) ->
                        let newState = { 
                            state with 
                                IsProcessing = true
                                ProcessingCount = state.ProcessingCount + 1L
                                LastActivity = DateTime.UtcNow 
                        }
                        
                        try
                            let resourceProvider = state.Resources :> Resources.IResourceProvider
                            let result = processTreatmentPlanCommand resourceProvider cmd
                            replyChannel.Reply(result)
                        with
                        | ex -> replyChannel.Reply(Error [| ex.Message |])
                        
                        let finalState = { newState with IsProcessing = false }
                        return! loop finalState
                    
                    | Shutdown ->
                        return () // Exit the loop
                }
            
            // Initialize with loaded resources
            async {
                match Resources.loadAllResources() with
                | Ok resources ->
                    let initialState = {
                        Resources = resources
                        IsProcessing = false
                        ProcessingCount = 0L
                        LastActivity = DateTime.UtcNow
                    }
                    return! loop initialState
                | Error err ->
                    // Log error and start with empty resources
                    let emptyResources = {
                        Resources.ResourceState.DoseRules = [||]
                        SolutionRules = [||]
                        RenalRules = [||]
                        ProductRules = [||]
                        LastReloaded = DateTime.MinValue
                        IsLoaded = false
                    }
                    let initialState = {
                        Resources = emptyResources
                        IsProcessing = false
                        ProcessingCount = 0L
                        LastActivity = DateTime.UtcNow
                    }
                    return! loop initialState
            }
        )
```

### 3. Pure Command Processing Functions

```fsharp
    let private processOrderContextCommand (resources: Resources.IResourceProvider) (cmd: OrderContextCmd) : Result<OrderContextResp, string []> =
        // Pure function using injected resources
        match cmd with
        | Api.UpdateOrderContext ctx
        | Api.SelectOrderScenario ctx
        | Api.UpdateOrderScenario ctx
        | Api.ResetOrderScenario ctx ->
            // Process using pre-loaded resources
            OrderContext.evaluateWithResources resources cmd
            |> Result.map (OrderContextUpdated >> OrderContextResp)

    let private processFormularyCommand (resources: Resources.IResourceProvider) (form: Formulary) : Result<FormularyResp, string []> =
        // Pure function using injected resources
        try
            let prescriptionRules = PrescriptionRule.filterWithResources resources form.Filter
            { FormularyResp.PrescriptionRules = prescriptionRules }
            |> Ok
        with
        | ex -> Error [| ex.Message |]

    let private processParenteraliaCommand (resources: Resources.IResourceProvider) (par: Parenteralia) : Result<ParentaraliaResp, string> =
        // Pure function using injected resources
        try
            let solutionRules = resources.GetSolutionRules()
            let filteredRules = SolutionRule.filterWithResources solutionRules par.Filter
            { ParentaraliaResp.SolutionRules = filteredRules }
            |> Ok
        with
        | ex -> Error ex.Message

    let private processTreatmentPlanCommand (resources: Resources.IResourceProvider) (cmd: TreatmentPlanCmd) : Result<TreatmentPlanResp, string []> =
        // Pure function using injected resources
        match cmd with
        | UpdateTreatmentPlan tp ->
            try
                let doseRules = resources.GetDoseRules()
                tp
                |> TreatmentPlan.updateTreatmentPlanWithResources doseRules
                |> TreatmentPlan.calculateTotals
                |> TreatmentPlanUpdated
                |> TreatmentPlanResp
                |> Ok
            with
            | ex -> Error [| ex.Message |]
        | FilterTreatmentPlan tp ->
            try
                tp
                |> TreatmentPlan.calculateTotals
                |> TreatmentPlanFiltered
                |> TreatmentPlanResp
                |> Ok
            with
            | ex -> Error [| ex.Message |]
```

### 4. Updated API Functions with Resource Injection

```fsharp
// In PrescriptionRule.fs - Updated to support resource injection
module PrescriptionRule =
    
    /// Pure function using injected resources
    let filterWithResources (resources: Resources.IResourceProvider) (filter : DoseFilter) =
        let pat = filter.Patient

        resources.GetDoseRules()  // ✅ Now uses injected resources
        |> DoseRule.filter filter
        |> Array.map (fun dr ->
            let dr = dr |> DoseRule.reconstitute pat.Department pat.Locations

            let filter =
                { filter with
                    Indication = dr.Indication |> Some
                    Generic = dr.Generic |> Some
                    Shape = dr.Shape |> Some
                    Route = dr.Route |> Some
                    DoseType = dr.DoseType |> Some
                }

            {
                Patient = pat
                DoseRule = dr
                SolutionRules =
                    let solFilter =
                        { Filter.solutionFilter dr.Generic with
                            Patient = pat
                            Shape = dr.Shape |> Some
                            Route = dr.Route |> Some
                            Indication = dr.Indication |> Some
                            Diluent = filter.Diluent
                            DoseType = dr.DoseType |> Some
                            Dose = None
                        }

                    resources.GetSolutionRules()  // ✅ Now uses injected resources
                    |> SolutionRule.filter solFilter
                    |> Array.map (fun sr ->
                        { sr with
                            Products =
                                sr.Products
                                |> Array.filter (fun sr_p ->
                                    dr.ComponentLimits
                                    |> Array.collect _.Products
                                    |> Array.exists (fun dr_p ->
                                        sr_p.GPK = dr_p.GPK
                                    )
                                )
                        }
                    )
                RenalRules =
                    resources.GetRenalRules()  // ✅ Now uses injected resources
                    |> RenalRule.filter filter
            }
        )
        // ... rest of the processing logic remains the same ...
    
    /// Backward compatibility: Use default resource provider
    let filter (filter : DoseFilter) =
        let defaultProvider = Resources.DefaultResourceProvider()
        filterWithResources defaultProvider filter
    
    /// Get all matching PrescriptionRules for a given Patient with resources.
    let getWithResources (resources: Resources.IResourceProvider) (pat : Patient) =
        Filter.doseFilter
        |> Filter.setPatient pat
        |> filterWithResources resources
    
    /// Backward compatibility: Get all matching PrescriptionRules for a given Patient.
    let get (pat : Patient) =
        let defaultProvider = Resources.DefaultResourceProvider()
        getWithResources defaultProvider pat
```

### 5. Processor Manager and Public API

```fsharp
module OrderProcessorManager =

    let mutable private processor: MailboxProcessor<OrderProcessor.ProcessorMessage> option = None
    let private processorLock = obj()

    let private getOrCreateProcessor () =
        lock processorLock (fun () ->
            match processor with
            | Some p when p.CurrentQueueLength < 1000 -> p
            | _ ->
                // Create new processor
                let newProcessor = OrderProcessor.createProcessor()
                processor <- Some newProcessor
                newProcessor
        )

    let processOrderContext (cmd: OrderContextCmd) : Async<Result<OrderContextResp, string []>> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(fun reply -> OrderProcessor.OrderContextMessage(cmd, reply))
        }

    let processFormulary (form: Formulary) : Async<Result<FormularyResp, string []>> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(fun reply -> OrderProcessor.FormularyMessage(form, reply))
        }

    let processParenteralia (par: Parenteralia) : Async<Result<ParentaraliaResp, string>> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(fun reply -> OrderProcessor.ParenteraliaMessage(par, reply))
        }

    let processTreatmentPlan (cmd: TreatmentPlanCmd) : Async<Result<TreatmentPlanResp, string []>> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(fun reply -> OrderProcessor.TreatmentPlanMessage(cmd, reply))
        }

    let reloadResources () : Async<Result<unit, string>> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(OrderProcessor.ReloadResources)
        }

    let checkHealth () : Async<Resources.ResourceState> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(OrderProcessor.CheckResourcesHealth)
        }

    let shutdown () =
        lock processorLock (fun () ->
            match processor with
            | Some p -> 
                p.Post(OrderProcessor.Shutdown)
                processor <- None
            | None -> ()
        )
```

### 6. Updated ServerApi Implementation

```fsharp
[<AutoOpen>]
module ApiImpl =

    open Shared.Api

    /// An implementation of the Shared IServerApi protocol using MailboxProcessor.
    let serverApi: IServerApi =
        {
            processMessage =
                fun msg ->
                    async {
                        match msg with
                        | OrderContextCmd ctxCmd ->
                            let! result = OrderProcessorManager.processOrderContext ctxCmd
                            return result |> Result.mapError Array.singleton

                        | TreatmentPlanCmd tpCmd ->
                            let! result = OrderProcessorManager.processTreatmentPlan tpCmd
                            return result |> Result.mapError Array.singleton

                        | FormularyCmd form ->
                            let! result = OrderProcessorManager.processFormulary form
                            return result |> Result.mapError Array.singleton

                        | ParenteraliaCmd par ->
                            let! result = OrderProcessorManager.processParenteralia par
                            return result |> Result.mapError Array.singleton
                    }

            testApi =
                fun () ->
                    async {
                        return "Hello world from MailboxProcessor!"
                    }

            // Add health check endpoint
            healthCheck =
                fun () ->
                    async {
                        let! health = OrderProcessorManager.checkHealth()
                        return {
                            IsHealthy = health.IsLoaded
                            LastReloaded = health.LastReloaded
                            ProcessingCount = 0L // Could be tracked in processor state
                        }
                    }

            // Add resource reload endpoint
            reloadResources =
                fun () ->
                    async {
                        let! result = OrderProcessorManager.reloadResources()
                        return result |> Result.isOk
                    }
        }
```

### 7. Advanced Resource Management

```fsharp
module Resources =
    
    // ... previous interface definitions ...
    
    /// Create a cached resource provider with TTL
    type CachedResourceProvider(ttlMinutes: int) =
        let mutable cachedState: (ResourceState * DateTime) option = None
        let lockObj = obj()
        
        let isExpired (timestamp: DateTime) =
            DateTime.UtcNow.Subtract(timestamp).TotalMinutes > float ttlMinutes
        
        let loadFresh () =
            match loadAllResources() with
            | Ok state ->
                cachedState <- Some (state, DateTime.UtcNow)
                state
            | Error _ ->
                // Return empty state on error
                {
                    DoseRules = [||]
                    SolutionRules = [||]
                    RenalRules = [||]
                    ProductRules = [||]
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
                
            member _.GetProductRules() =
                lock lockObj (fun () ->
                    match cachedState with
                    | Some (state, timestamp) when not (isExpired timestamp) -> state.ProductRules
                    | _ -> (loadFresh()).ProductRules
                )
```

### 8. Resource Auto-Reload with Background Service

```fsharp
module ResourceReloadService =

    let private reloadTimer = ref None
    let private reloadInterval = TimeSpan.FromHours(1.0) // Reload every hour

    let startAutoReload () =
        let timer = new System.Timers.Timer(reloadInterval.TotalMilliseconds)
        timer.Elapsed.Add(fun _ ->
            async {
                try
                    let! result = OrderProcessorManager.reloadResources()
                    match result with
                    | Ok _ -> printfn "Resources reloaded successfully at %A" DateTime.UtcNow
                    | Error err -> printfn "Failed to reload resources: %s" err
                with
                | ex -> printfn "Exception during resource reload: %s" ex.Message
            }
            |> Async.Start
        )
        timer.Start()
        reloadTimer := Some timer

    let stopAutoReload () =
        match !reloadTimer with
        | Some timer ->
            timer.Stop()
            timer.Dispose()
            reloadTimer := None
        | None -> ()
```

## Benefits of This Updated Design

1. **Pure Functions**: All processing functions are now pure, taking dependencies as parameters
2. **Interface Abstraction**: Clean separation between resource access and business logic
3. **Testability**: Easy to inject mock resources for comprehensive testing
4. **Resource Management**: Centralized, efficient resource loading and caching
5. **Thread Safety**: MailboxProcessor ensures single-threaded access to resources
6. **Reload Capability**: Resources can be reloaded on-demand or automatically
7. **Error Isolation**: Failures in one command don't affect the processor state
8. **Monitoring**: Built-in health checks and processing statistics
9. **Scalability**: Queue management prevents memory issues under load
10. **Backward Compatibility**: Existing functions still work with default providers
11. **Flexibility**: Multiple resource provider implementations (cached, fresh, mock)

## Testing Strategy

```fsharp
module Tests =
    
    // Create mock resource provider for testing
    let createMockResources doseRules solutionRules renalRules productRules =
        Resources.createProvider doseRules solutionRules renalRules productRules
    
    // Test pure functions with controlled data
    let testPrescriptionRuleFiltering () =
        let mockResources = createMockResources [| testDoseRule |] [||] [||] [||]
        let result = PrescriptionRule.filterWithResources mockResources testFilter
        // Assert expected results
        
    // Test MailboxProcessor behavior
    let testResourceReloading () =
        async {
            let! initialHealth = OrderProcessorManager.checkHealth()
            let! reloadResult = OrderProcessorManager.reloadResources()
            let! updatedHealth = OrderProcessorManager.checkHealth()
            // Assert health improvements
        }
```

## Integration Strategy

To integrate this design:

1. **Update API Functions**: Add resource injection parameters to all API functions
2. **Replace Direct Calls**: Use MailboxProcessor calls instead of direct API calls
3. **Application Startup**: Initialize the processor and start auto-reload service
4. **Add Management Endpoints**: Include endpoints for resource management and health checks
5. **Testing Migration**: Update tests to use pure functions with mock resources
6. **Monitoring Integration**: Connect health checks to application monitoring

## Resource Loading Strategies

The design supports multiple resource loading strategies:

- **Eager Loading**: Load all resources at startup (default)
- **Lazy Loading**: Load resources on first use per type
- **Selective Loading**: Load only required resources based on request
- **Cached Loading**: TTL-based caching with automatic refresh
- **Hybrid Loading**: Combination of strategies based on resource type

## Performance Optimizations

- **Resource Reuse**: Load once, use many times
- **Memory Efficiency**: Shared immutable data structures
- **Queue Management**: Bounded queues to prevent memory exhaustion
- **Garbage Collection**: Minimize allocations in hot paths
- **Concurrent Processing**: Non-blocking resource access where possible

This updated design provides a robust, scalable, and maintainable foundation for managing GenOrder.Lib API interactions with full support for pure functions, comprehensive testing, and operational