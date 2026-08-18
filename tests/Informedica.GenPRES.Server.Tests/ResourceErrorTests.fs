module Informedica.GenPRES.Server.Tests.ResourceErrorTests

open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources
open Helpers

let errMsg s : Message = ErrorMsg(s, None)

let errorPropagationTests =
    testList
        "loadAllResourcesWithRegistry error propagation"
        [

            test "leaf loader (unitMappings) returns Error propagates" {
                okRegistry
                |> Map.add Keys.unitMappings.Name (fun _ -> Error [ errMsg "unit mapping failed" ])
                |> loadAllResourcesWithRegistry
                |> Result.isError
                |> Expect.isTrue "should be Error"
            }

            test "dependent loader (formRoutes) returns Error propagates" {
                okRegistry
                |> Map.add Keys.formRoutes.Name (fun _ -> Error [ errMsg "form routes failed" ])
                |> loadAllResourcesWithRegistry
                |> Result.isError
                |> Expect.isTrue "should be Error"
            }

            test "derived loader (renalRules) returns Error propagates" {
                okRegistry
                |> Map.add Keys.renalRules.Name (fun _ -> Error [ errMsg "renal rules failed" ])
                |> loadAllResourcesWithRegistry
                |> Result.isError
                |> Expect.isTrue "should be Error"
            }

            test "loader throws exception is caught and returned as Error" {
                let result =
                    okRegistry
                    |> Map.add Keys.unitMappings.Name (fun _ -> failwith "unexpected crash")
                    |> loadAllResourcesWithRegistry

                result |> Result.isError |> Expect.isTrue "should be Error"

                match result with
                | Error msgs ->
                    msgs
                    |> List.exists (fun m ->
                        match m with
                        | ErrorMsg(s, _) -> s.Contains("Failed to load resources")
                        | _ -> false
                    )
                    |> Expect.isTrue "should contain 'Failed to load resources' message"
                | Ok _ -> failwith "expected Error"
            }
        ]


let successPathTests =
    testList
        "loadAllResourcesWithRegistry success path"
        [

            test "all loaders succeed returns Ok with IsLoaded = true" {
                let result = okRegistry |> loadAllResourcesWithRegistry

                result |> Result.isOk |> Expect.isTrue "should be Ok"

                match result with
                | Ok loaded ->
                    loaded.State.IsLoaded |> Expect.isTrue "IsLoaded should be true"

                    loaded.State.Messages |> Expect.equal "Messages should be empty" [||]
                | Error _ -> failwith "expected Ok"
            }
        ]


let cachedProviderErrorStateTests =
    testList
        "CachedResourceProvider error state"
        [

            test "loader returns Error, GetResourceInfo shows IsLoaded = false" {
                let provider =
                    CachedResourceProvider((fun () -> Error [ errMsg "load failed" ]), None)

                let info = (provider :> IResourceProvider).GetResourceInfo()

                info.IsLoaded |> Expect.isFalse "IsLoaded should be false"

                info.Messages |> Array.isEmpty |> Expect.isFalse "Messages should not be empty"
            }

            test "all resource getters return empty arrays when loader failed" {
                let provider =
                    CachedResourceProvider((fun () -> Error [ errMsg "load failed" ]), None)

                (provider :> IResourceProvider).GetUnitMappings()
                |> Expect.equal "UnitMappings should be empty" [||]

                (provider :> IResourceProvider).GetDoseRules()
                |> Expect.equal "DoseRules should be empty" [||]

                (provider :> IResourceProvider).GetProducts()
                |> Expect.equal "Products should be empty" [||]

                (provider :> IResourceProvider).GetRenalRules()
                |> Expect.equal "RenalRules should be empty" [||]
            }
        ]


let cachingBehaviorTests =
    testList
        "CachedResourceProvider caching behavior"
        [

            test "after error, second call does NOT re-invoke loader" {
                let mutable callCount = 0

                let provider =
                    CachedResourceProvider(
                        (fun () ->
                            callCount <- callCount + 1
                            Error [ errMsg "load failed" ]
                        ),
                        None
                    )

                (provider :> IResourceProvider).GetResourceInfo() |> ignore

                callCount |> Expect.equal "loader should be called once" 1

                (provider :> IResourceProvider).GetResourceInfo() |> ignore

                callCount |> Expect.equal "loader should still be called once" 1
            }

            test "ReloadCache re-invokes loader" {
                let mutable callCount = 0

                let provider =
                    CachedResourceProvider(
                        (fun () ->
                            callCount <- callCount + 1
                            Error [ errMsg "load failed" ]
                        ),
                        None
                    )

                (provider :> IResourceProvider).GetResourceInfo() |> ignore

                callCount |> Expect.equal "loader called once after first access" 1

                provider.ReloadCache()

                callCount |> Expect.equal "loader called twice after ReloadCache" 2
            }

            test "loader fails first then succeeds, after ReloadCache IsLoaded = true" {
                let mutable callCount = 0

                let provider =
                    CachedResourceProvider(
                        (fun () ->
                            callCount <- callCount + 1

                            if callCount = 1 then
                                Error [ errMsg "first attempt failed" ]
                            else
                                okRegistry |> loadAllResourcesWithRegistry
                        ),
                        None
                    )

                let info1 = (provider :> IResourceProvider).GetResourceInfo()

                info1.IsLoaded |> Expect.isFalse "should not be loaded after first attempt"

                provider.ReloadCache()

                let info2 = (provider :> IResourceProvider).GetResourceInfo()

                info2.IsLoaded |> Expect.isTrue "should be loaded after ReloadCache"
            }
        ]


let processCmdGuardTests =
    testList
        "processCmd IsLoaded guard"
        [

            test "FormularyCmd returns Error when provider IsLoaded = false" {
                let provider =
                    CachedResourceProvider((fun () -> Error [ errMsg "resources unavailable" ]), None)

                let cmd = Shared.Api.FormularyCmd emptyFormulary

                let result =
                    ServerApi.Command.processCmd (ServerApi.Adapters.makeAppEnv provider) cmd
                    |> Async.RunSynchronously

                result
                |> Result.isError
                |> Expect.isTrue "should return Error for FormularyCmd when not loaded"
            }

            test "ParenteraliaCmd returns Error when provider IsLoaded = false" {
                let provider =
                    CachedResourceProvider((fun () -> Error [ errMsg "resources unavailable" ]), None)

                let cmd = Shared.Api.ParenteraliaCmd emptyParenteralia

                let result =
                    ServerApi.Command.processCmd (ServerApi.Adapters.makeAppEnv provider) cmd
                    |> Async.RunSynchronously

                result
                |> Result.isError
                |> Expect.isTrue "should return Error for ParenteraliaCmd when not loaded"
            }
        ]


let agentAdapterGuardTests =
    testList
        "processCmd IsLoaded guard (AgentAdapters)"
        [

            test "FormularyCmd returns Error when provider IsLoaded = false (agent)" {
                let provider =
                    CachedResourceProvider((fun () -> Error [ errMsg "resources unavailable" ]), None)

                let cmd = Shared.Api.FormularyCmd emptyFormulary

                let result =
                    ServerApi.Command.processCmd (ServerApi.AgentAdapters.makeAppEnv provider) cmd
                    |> Async.RunSynchronously

                result
                |> Result.isError
                |> Expect.isTrue "should return Error for FormularyCmd when not loaded (agent)"
            }

            test "ParenteraliaCmd returns Error when provider IsLoaded = false (agent)" {
                let provider =
                    CachedResourceProvider((fun () -> Error [ errMsg "resources unavailable" ]), None)

                let cmd = Shared.Api.ParenteraliaCmd emptyParenteralia

                let result =
                    ServerApi.Command.processCmd (ServerApi.AgentAdapters.makeAppEnv provider) cmd
                    |> Async.RunSynchronously

                result
                |> Result.isError
                |> Expect.isTrue "should return Error for ParenteraliaCmd when not loaded (agent)"
            }
        ]


[<Tests>]
let tests =
    testList
        "Resource Error Handling Tests"
        [
            errorPropagationTests
            successPathTests
            cachedProviderErrorStateTests
            cachingBehaviorTests
            processCmdGuardTests
            agentAdapterGuardTests
        ]
