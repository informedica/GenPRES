# MailboxProcessor Design Proposal for GenOrder.Lib API Integration

## Overview

This document proposes implementing a MailboxProcessor to handle all calls to the Informedica.GenOrder.Lib.API instead of directly calling the API functions. The design emphasizes resource management with reloading capabilities rather than relying on memoized functions.

## Design Goals

1. **Centralized Resource Management**: Load all necessary resources once and reuse them
2. **Thread Safety**: Ensure single-threaded access to resources using MailboxProcessor
3. **Resource Reloading**: Enable on-demand and automatic resource reloading
4. **Error Isolation**: Prevent failures in one command from affecting the processor state
5. **Monitoring**: Provide health checks and processing statistics
6. **Scalability**: Implement queue management to prevent memory issues under load

## Core Architecture

### 1. Core MailboxProcessor Design

```fsharp
module OrderProcessor =

    open System
    open Informedica.GenOrder.Lib
    open Informedica.Utils.Lib
    open Shared.Api

    type ResourceState = {
        DoseRules: DoseRule []
        SolutionRules: SolutionRule []
        ProductRules: ProductRule []
        LastReloaded: DateTime
        IsLoaded: bool
    }

    type ProcessorMessage =
        | OrderContextMessage of OrderContextCmd * AsyncReplyChannel<Result<OrderContextResp, string []>>
        | FormularyMessage of Formulary * AsyncReplyChannel<Result<FormularyResp, string []>>
        | ParenteraliaMessage of Parenteralia * AsyncReplyChannel<Result<ParentaraliaResp, string>>
        | TreatmentPlanMessage of TreatmentPlanCmd * AsyncReplyChannel<Result<TreatmentPlanResp, string []>>
        | ReloadResources of AsyncReplyChannel<Result<unit, string>>
        | CheckResourcesHealth of AsyncReplyChannel<ResourceState>
        | Shutdown

    type ProcessorState = {
        Resources: ResourceState
        IsProcessing: bool
        ProcessingCount: int64
        LastActivity: DateTime
    }

    let private loadResources () : Result<ResourceState, string> =
        try
            let doseRules = Formulary.getDoseRules Filter.doseFilter
            let solutionRules = Formulary.getSolutionRules "" "" ""
            // Add other resource loading as needed
            
            {
                DoseRules = doseRules
                SolutionRules = solutionRules
                ProductRules = [||] // Load as needed
                LastReloaded = DateTime.UtcNow
                IsLoaded = true
            }
            |> Ok
        with
        | ex -> Error $"Failed to load resources: {ex.Message}"

    let private createProcessor () =
        MailboxProcessor<ProcessorMessage>.Start(fun inbox ->
            let rec loop state =
                async {
                    let! msg = inbox.Receive()
                    
                    match msg with
                    | ReloadResources replyChannel ->
                        match loadResources() with
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
                            let result = processOrderContextCommand state.Resources cmd
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
                            let result = processFormularyCommand state.Resources form
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
                            let result = processParenteraliaCommand state.Resources par
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
                            let result = processTreatmentPlanCommand state.Resources cmd
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
                match loadResources() with
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
                        DoseRules = [||]
                        SolutionRules = [||]
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

### 2. Command Processing Functions

```fsharp
    let private processOrderContextCommand (resources: ResourceState) (cmd: OrderContextCmd) : Result<OrderContextResp, string []> =
        // Use resources.DoseRules instead of calling Formulary.getDoseRules directly
        match cmd with
        | Api.UpdateOrderContext ctx
        | Api.SelectOrderScenario ctx
        | Api.UpdateOrderScenario ctx
        | Api.ResetOrderScenario ctx ->
            // Process using pre-loaded resources
            OrderContext.evaluateWithResources resources.DoseRules cmd
            |> Result.map (OrderContextUpdated >> OrderContextResp)

    let private processFormularyCommand (resources: ResourceState) (form: Formulary) : Result<FormularyResp, string []> =
        // Use pre-loaded dose rules
        Formulary.getWithResources resources.DoseRules form
        |> Result.map FormularyResp

    let private processParenteraliaCommand (resources: ResourceState) (par: Parenteralia) : Result<ParentaraliaResp, string> =
        // Use pre-loaded solution rules
        Parenteralia.getWithResources resources.SolutionRules par
        |> Result.map ParentaraliaResp

    let private processTreatmentPlanCommand (resources: ResourceState) (cmd: TreatmentPlanCmd) : Result<TreatmentPlanResp, string []> =
        match cmd with
        | UpdateTreatmentPlan tp ->
            tp
            |> TreatmentPlan.updateTreatmentPlanWithResources resources.DoseRules
            |> TreatmentPlan.calculateTotals
            |> TreatmentPlanUpdated
            |> TreatmentPlanResp
            |> Ok
        | FilterTreatmentPlan tp ->
            tp
            |> TreatmentPlan.calculateTotals
            |> TreatmentPlanFiltered
            |> TreatmentPlanResp
            |> Ok
```

### 3. Processor Manager and Public API

```fsharp
module OrderProcessorManager =

    let mutable private processor: MailboxProcessor<ProcessorMessage> option = None
    let private processorLock = obj()

    let private getOrCreateProcessor () =
        lock processorLock (fun () ->
            match processor with
            | Some p when not p.CurrentQueueLength = 0 || p.CurrentQueueLength < 1000 -> p
            | _ ->
                // Create new processor
                let newProcessor = OrderProcessor.createProcessor()
                processor <- Some newProcessor
                newProcessor
        )

    let processOrderContext (cmd: OrderContextCmd) : Async<Result<OrderContextResp, string []>> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(fun reply -> OrderContextMessage(cmd, reply))
        }

    let processFormulary (form: Formulary) : Async<Result<FormularyResp, string []>> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(fun reply -> FormularyMessage(form, reply))
        }

    let processParenteralia (par: Parenteralia) : Async<Result<ParentaraliaResp, string>> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(fun reply -> ParenteraliaMessage(par, reply))
        }

    let processTreatmentPlan (cmd: TreatmentPlanCmd) : Async<Result<TreatmentPlanResp, string []>> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(fun reply -> TreatmentPlanMessage(cmd, reply))
        }

    let reloadResources () : Async<Result<unit, string>> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(ReloadResources)
        }

    let checkHealth () : Async<ResourceState> =
        async {
            let proc = getOrCreateProcessor()
            return! proc.PostAndAsyncReply(CheckResourcesHealth)
        }

    let shutdown () =
        lock processorLock (fun () ->
            match processor with
            | Some p -> 
                p.Post(Shutdown)
                processor <- None
            | None -> ()
        )
```

### 4. Updated ServerApi Implementation

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
                            ProcessingCount = 0L // Could be tracked
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

### 5. Resource Auto-Reload with Background Service

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

## Benefits of This Design

1. **Resource Management**: All resources are loaded once and reused, avoiding repeated expensive operations
2. **Thread Safety**: MailboxProcessor ensures single-threaded access to resources
3. **Reload Capability**: Resources can be reloaded on-demand or automatically
4. **Error Isolation**: Failures in one command don't affect the processor state
5. **Monitoring**: Built-in health checks and processing statistics
6. **Scalability**: Queue management prevents memory issues under load
7. **Clean Shutdown**: Proper cleanup when the service stops

## Integration Strategy

To integrate this design:

1. **Replace Direct API Calls**: Replace current direct calls to GenOrder.Lib with MailboxProcessor calls
2. **Application Startup**: Start the auto-reload service during application initialization
3. **Add Management Endpoints**: Include endpoints for manual resource reload and health checks
4. **Proper Shutdown**: Ensure cleanup in application shutdown procedures

## Resource Loading Strategy

The design allows for different resource loading strategies:

- **Eager Loading**: Load all resources at startup (current approach)
- **Lazy Loading**: Load resources on first use
- **Selective Loading**: Load only the resources needed for specific operations
- **Cached Loading**: Implement TTL-based caching with automatic refresh

## Error Handling and Resilience

- **Graceful Degradation**: Continue operation with partial resources if some fail to load
- **Retry Logic**: Implement exponential backoff for resource reload failures
- **Circuit Breaker**: Temporarily disable resource reloading if failures persist
- **Fallback Resources**: Use cached or default resources when fresh loading fails

## Monitoring and Observability

The design provides several monitoring capabilities:

- **Resource Health**: Track resource loading status and freshness
- **Processing Metrics**: Monitor queue length, processing times, and throughput
- **Error Rates**: Track failure rates for different operations
- **Resource Usage**: Monitor memory and performance impact

## Performance Considerations

- **Queue Management**: Implement queue size limits to prevent memory issues
- **Backpressure**: Handle high load scenarios gracefully
- **Resource Optimization**: Optimize data structures for frequent access
- **Garbage Collection**: Minimize allocations in hot paths

This design provides a robust, scalable foundation for managing GenOrder.Lib API interactions while maintaining efficient resource utilization and providing operational flexibility through reloadable resources.