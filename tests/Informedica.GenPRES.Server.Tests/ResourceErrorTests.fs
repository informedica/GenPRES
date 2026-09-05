module Informedica.GenPRES.Server.Tests.ResourceErrorTests

open Expecto
open Expecto.Flip
open Shared.Models
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources

let private errMsg s : Message = ErrorMsg(s, None)

/// Stub registry: every resource resolves to a typed empty value. The typed
/// empties matter — the boxed value's runtime type must match the key's `'T`
/// for the engine's downcast to succeed.
let private okRegistry: ResourceRegistry =
    Map
        [
            Keys.unitMappings.Name, ofResult (fun () -> Ok([||]: UnitMapping[]))
            Keys.routeMappings.Name, ofResult (fun () -> Ok([||]: RouteMapping[]))
            Keys.validForms.Name, ofResult (fun () -> Ok([||]: string[]))
            Keys.formRoutes.Name, ofResult (fun () -> Ok([||]: FormRoute[]))
            Keys.formularyProducts.Name, ofResult (fun () -> Ok([||]: FormularyProduct[]))
            Keys.genPresProducts.Name, ofResult (fun () -> Ok([||]: Informedica.ZIndex.Lib.Types.GenPresProduct[]))
            Keys.reconstitution.Name, ofResult (fun () -> Ok([||]: Reconstitution[]))
            Keys.parenteralMeds.Name, ofResult (fun () -> Ok([||]: ProductComponent[]))
            Keys.enteralFeeding.Name, ofResult (fun () -> Ok([||]: ProductComponent[]))
            Keys.doseRuleData.Name, ofResult (fun () -> Ok([||]: DoseRuleData[]))
            Keys.solutionRuleData.Name, ofResult (fun () -> Ok([||]: SolutionRuleData[]))
            Keys.renalRuleData.Name, ofResult (fun () -> Ok([||]: RenalRuleData[]))
            Keys.totalsData.Name, ofResult (fun () -> Ok([||]: TotalsData[]))
            Keys.products.Name, ofResult (fun () -> Ok([||]: ProductComponent[]))
            Keys.doseRules.Name, ofResult (fun () -> Ok([||]: DoseRule[]))
            Keys.solutionRules.Name, ofResult (fun () -> Ok([||]: SolutionRule[]))
            Keys.renalRules.Name, ofResult (fun () -> Ok([||]: RenalRule[]))
            Keys.gStandProvider.Name, derive (fun r -> Check.gStandProvider (r.Get Keys.routeMappings))
        ]

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

            test "getNKFLinkProvider serves FK-only links instead of throwing when loader failed" {
                // Nothing is registered on a failed load, so `Get` would raise
                // KeyNotFoundException; a decorative link must not fail a request.
                let provider =
                    CachedResourceProvider((fun () -> Error [ errMsg "load failed" ]), None)

                let getLink =
                    Informedica.GenForm.Lib.Api.getNKFLinkProvider (provider :> IResourceProvider)

                let label = GenericLabel.fromShorthand "paracetamol"

                getLink (Source.identified "FK") label
                |> Expect.isSome "FK link is built from the generic name alone"

                getLink (Source.identified "NKF") label
                |> Expect.isNone "NKF link needs the index, which did not load"
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

                let cmd = Shared.Api.FormularyCmd Formulary.empty

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

                let cmd = Shared.Api.ParenteraliaCmd Parenteralia.empty

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

                let cmd = Shared.Api.FormularyCmd Formulary.empty

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

                let cmd = Shared.Api.ParenteraliaCmd Parenteralia.empty

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
