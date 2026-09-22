/// ResourceMessages.fsx
///
/// Prototype for accumulating all messages (warnings + errors) that arise
/// during resource loading and storing them in ResourceInfo.Messages.
///
/// Problem:
///   The current `loadAllResourcesWithConfig` in Api.fs uses the `result {}`
///   computation expression, which short-circuits on the first Error and then
///   hard-codes `Messages = [||]` on success.  This means:
///     1. When multiple loaders fail, only the first error is surfaced.
///     2. Even on success, any non-critical warning messages are discarded.
///     3. `logGenFormMessages` always finds an empty Messages array.
///
/// Solution prototyped here:
///   A `tryCollect` helper that drains a Result into an accumulator without
///   short-circuiting, plus a revised `loadAllResourcesWithConfig` that
///   continues loading even after a failure so all messages are gathered.
///   On return, the accumulated messages are stored in ResourceState.Messages,
///   making them visible to `logGenFormMessages`.

#load "../../../scripts/load-dependencies.fsx"

#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.Logging.Lib/bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../../Informedica.GenUNITS.Lib/bin/Debug/net10.0/Informedica.GenUNITS.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.ZForm.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "../../Informedica.GenCORE.Lib/bin/Debug/net10.0/Informedica.GenCORE.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenFORM.Lib.dll"


open System
open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources


// ─────────────────────────────────────────────────────────────────────────────
// Core accumulation helpers
// ─────────────────────────────────────────────────────────────────────────────

module ResourceAccumulator =

    /// Evaluate a `Result` and collect any error messages into `acc`.
    /// Returns `Some value` on success; `None` (after collecting) on failure.
    let tryCollect (acc: ResizeArray<Message>) (result: Result<'T, Message list>) : 'T option =
        match result with
        | Ok value -> Some value
        | Error msgs ->
            acc.AddRange msgs
            None


    /// Like `tryCollect`, but keeps a human-readable label that is prepended
    /// to every collected error message for easier diagnostics.
    let tryCollectLabeled (label: string) (acc: ResizeArray<Message>) (result: Result<'T, Message list>) : 'T option =
        match result with
        | Ok value -> Some value
        | Error msgs ->
            msgs
            |> List.map (fun m ->
                match m with
                | Info s -> Info $"[{label}] {s}"
                | Warning s -> Warning $"[{label}] {s}"
                | ErrorMsg(s, exn) -> ErrorMsg($"[{label}] {s}", exn)
            )
            |> acc.AddRange

            None


    /// Revised resource loader that does NOT short-circuit.
    ///
    /// Unlike the original `result {}` CE approach, this function attempts
    /// every loader regardless of earlier failures.  All error messages are
    /// accumulated and, on success, stored in `ResourceState.Messages` so
    /// that `logGenFormMessages` can surface them.
    let loadAllResourcesWithConfig (config: ResourceConfig) : Result<ResourceState, Message list> =
        try
            let acc = ResizeArray<Message>()

            let unitMappings =
                config.GetUnitMappings() |> tryCollectLabeled "GetUnitMappings" acc

            let routeMappings =
                config.GetRouteMappings() |> tryCollectLabeled "GetRouteMappings" acc

            let validForms = config.GetValidForms() |> tryCollectLabeled "GetValidForms" acc

            let formRoutes =
                unitMappings
                |> Option.bind (fun um -> config.GetFormRoutes um |> tryCollectLabeled "GetFormRoutes" acc)

            let formularyProducts =
                config.GetFormularyProducts() |> tryCollectLabeled "GetFormularyProducts" acc

            let reconstitution =
                config.GetReconstitution() |> tryCollectLabeled "GetReconstitution" acc

            let parenteralMeds =
                unitMappings
                |> Option.bind (fun um -> config.GetParenteralMeds um |> tryCollectLabeled "GetParenteralMeds" acc)

            let enteralFeeding =
                unitMappings
                |> Option.bind (fun um -> config.GetEnteralFeeding um |> tryCollectLabeled "GetEnteralFeeding" acc)

            let products =
                match
                    unitMappings,
                    routeMappings,
                    validForms,
                    formRoutes,
                    reconstitution,
                    parenteralMeds,
                    enteralFeeding,
                    formularyProducts
                with
                | Some um, Some rm, Some vf, Some fr, Some rc, Some pm, Some ef, Some fp ->
                    config.GetProducts um rm vf fr rc pm ef fp |> Some
                | _ -> None

            let doseRules =
                match routeMappings, formRoutes, products with
                | Some rm, Some fr, Some prods ->
                    config.GetDoseRules rm fr prods |> tryCollectLabeled "GetDoseRules" acc
                | _ -> None

            let solutionRules =
                match routeMappings, parenteralMeds, products with
                | Some rm, Some pm, Some prods ->
                    config.GetSolutionRules rm pm prods |> tryCollectLabeled "GetSolutionRules" acc
                | _ -> None

            let renalRules = config.GetRenalRules() |> tryCollectLabeled "GetRenalRules" acc

            let allMessages = acc.ToArray()

            match
                unitMappings,
                routeMappings,
                validForms,
                formRoutes,
                formularyProducts,
                reconstitution,
                parenteralMeds,
                enteralFeeding,
                products,
                doseRules,
                solutionRules,
                renalRules
            with
            | Some um,
              Some rm,
              Some vf,
              Some fr,
              Some fp,
              Some rc,
              Some pm,
              Some ef,
              Some prods,
              Some dr,
              Some sr,
              Some rr ->
                Ok
                    {
                        UnitMappings = um
                        RouteMappings = rm
                        ValidForms = vf
                        FormRoutes = fr
                        FormularyProducts = fp
                        Reconstitution = rc
                        ParenteralMeds = pm
                        EnteralFeeding = ef
                        Products = prods
                        DoseRules = dr
                        SolutionRules = sr
                        RenalRules = rr
                        Messages = allMessages // ← now populated, not [||]
                        LastReloaded = DateTime.UtcNow
                        IsLoaded = true
                    }
            | _ -> Error(allMessages |> Array.toList)

        with exn ->
            Error [ ErrorMsg("Failed to load resources", Some exn) ]


    /// Drop-in `CachedResourceProvider` that uses the accumulating loader.
    ///
    /// This wraps the revised `loadAllResourcesWithConfig` so callers can
    /// keep the same `CachedResourceProvider` interface while gaining full
    /// message visibility in `GetResourceInfo().Messages`.
    let createCachedProvider (config: ResourceConfig) (ttlMinutes: int option) =
        CachedResourceProvider(
            Informedica.Logging.Lib.Logging.noOp,
            (fun () -> loadAllResourcesWithConfig config),
            ttlMinutes
        )


// ─────────────────────────────────────────────────────────────────────────────
// Helpers used in tests
// ─────────────────────────────────────────────────────────────────────────────

module Helpers =

    /// A minimal `ResourceConfig` where all loaders return empty Ok results.
    /// Override individual fields with record update syntax in each test.
    let defaultStubConfig: ResourceConfig =
        {
            GetUnitMappings = fun () -> Ok [||]
            GetRouteMappings = fun () -> Ok [||]
            GetValidForms = fun () -> Ok [||]
            GetFormRoutes = fun _ -> Ok [||]
            GetFormularyProducts = fun () -> Ok [||]
            GetReconstitution = fun () -> Ok [||]
            GetParenteralMeds = fun _ -> Ok [||]
            GetEnteralFeeding = fun _ -> Ok [||]
            GetProducts = fun _ _ _ _ _ _ _ _ -> [||]
            GetDoseRules = fun _ _ _ -> Ok [||]
            GetSolutionRules = fun _ _ _ -> Ok [||]
            GetRenalRules = fun () -> Ok [||]
        }


// ─────────────────────────────────────────────────────────────────────────────
// Expecto tests
// ─────────────────────────────────────────────────────────────────────────────

open ResourceAccumulator
open Helpers

let tests =
    testList
        "ResourceAccumulator"
        [

            test "tryCollect returns Some on Ok and does not add messages" {
                let acc = ResizeArray<Message>()
                let result: Result<int, Message list> = Ok 42
                let value = tryCollect acc result
                value |> Expect.equal "should be Some 42" (Some 42)
                acc |> Seq.length |> Expect.equal "accumulator should be empty" 0
            }

            test "tryCollect returns None on Error and collects messages" {
                let acc = ResizeArray<Message>()
                let msgs = [ Warning "w1"; Info "i1" ]
                let result: Result<int, Message list> = Error msgs
                let value = tryCollect acc result
                value |> Expect.isNone "should be None on error"
                acc |> Seq.toList |> Expect.equal "should contain both messages" msgs
            }

            test "tryCollectLabeled prepends label to collected messages" {
                let acc = ResizeArray<Message>()
                let result: Result<int, Message list> = Error [ Warning "oops" ]
                tryCollectLabeled "MyLoader" acc result |> ignore

                acc
                |> Seq.head
                |> Expect.equal "label should be prepended" (Warning "[MyLoader] oops")
            }

            test "loadAllResourcesWithConfig sets Messages=[||] when all loaders succeed" {
                let config = defaultStubConfig

                match loadAllResourcesWithConfig config with
                | Error _ -> failtest "Expected Ok"
                | Ok state -> state.Messages |> Expect.isEmpty "Messages should be empty when all succeed"
            }

            test "loadAllResourcesWithConfig collects messages from ALL failing loaders, not just the first" {
                // Both GetRouteMappings and GetRenalRules fail independently.
                // The old `result {}` CE would stop after GetRouteMappings and lose
                // the RenalRules error; the new approach surfaces both.
                let failRouteMappings () =
                    Error [ Warning "route mapping warning" ]

                let failRenalRules () = Error [ Warning "renal rules warning" ]

                let config =
                    { defaultStubConfig with
                        GetRouteMappings = failRouteMappings
                        GetRenalRules = failRenalRules
                    }

                match loadAllResourcesWithConfig config with
                | Ok _ -> failtest "Expected Error because critical loaders failed"
                | Error msgs ->
                    msgs |> List.length |> Expect.equal "both warnings should be collected" 2

                    msgs
                    |> List.exists (
                        function
                        | Warning s -> s.Contains "route mapping"
                        | _ -> false
                    )
                    |> Expect.isTrue "should contain route mapping warning"

                    msgs
                    |> List.exists (
                        function
                        | Warning s -> s.Contains "renal rules"
                        | _ -> false
                    )
                    |> Expect.isTrue "should contain renal rules warning"
            }

            test "old result {} CE only surfaces the first failure" {
                // Demonstrate the pre-fix behaviour: the result CE short-circuits
                // after GetRouteMappings, so GetRenalRules is never called.
                let renalCalled = ref false

                let failRouteMappings () =
                    Error [ Warning "route mapping warning" ]

                let failRenalRules () =
                    renalCalled.Value <- true
                    Error [ Warning "renal rules warning" ]

                let config =
                    { defaultStubConfig with
                        GetRouteMappings = failRouteMappings
                        GetRenalRules = failRenalRules
                    }

                // Simulate old `result {}` behaviour
                let oldResult =
                    FsToolkit.ErrorHandling.ResultCE.result {
                        let! _um = config.GetUnitMappings()
                        let! _rm = config.GetRouteMappings() // <-- short-circuits here
                        let! _rr = config.GetRenalRules() // <-- never reached
                        return ()
                    }

                match oldResult with
                | Ok _ -> failtest "Expected Error"
                | Error msgs ->
                    msgs |> List.length |> Expect.equal "only first error captured" 1
                    renalCalled.Value |> Expect.isFalse "renal loader should never be called"
            }

            test "loadAllResourcesWithConfig populates Messages in ResourceInfo via provider" {
                // When loading succeeds, the provider's GetResourceInfo should
                // expose the (empty) Messages array rather than a null/missing one.
                let config = defaultStubConfig
                let provider = createCachedProvider config None

                let info = (provider :> IResourceProvider).GetResourceInfo()
                info.IsLoaded |> Expect.isTrue "provider should be loaded"
                info.Messages |> Expect.isNotNull "Messages must never be null"
            }

            test "multiple independent loader failures all appear in accumulated messages" {
                let failUnitMappings () =
                    Error [ ErrorMsg("unit mapping failure", None) ]

                let failValidForms () = Error [ Warning "valid forms warning" ]
                let failRenalRules () = Error [ Info "renal info" ]

                let config =
                    { defaultStubConfig with
                        GetUnitMappings = failUnitMappings
                        GetValidForms = failValidForms
                        GetRenalRules = failRenalRules
                    }

                match loadAllResourcesWithConfig config with
                | Ok _ -> failtest "Expected Error"
                | Error msgs -> msgs |> List.length |> Expect.equal "all three messages collected" 3
            }
        ]


let runExpecto () =
    runTestsWithCLIArgs [] [| "--summary" |] tests

runExpecto ()
