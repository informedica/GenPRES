// Deleting the old admin paths (plan 654, step 4): the LogAnalyzerCmd family and
// OrderContextCommand.ReloadResources leave the shared contract now that the client asks
// `processAdmin`, and with them the arms that let four commands bypass `requireLoaded`. What
// remains of `Command.processCmd` is one total match: `GetDrugNames` runs open, every other
// command runs gated, and there is no arm the compiler demands but nothing reaches.
//
// Script-first draft (script-only policy) of:
//   - `Command.processCmd` as one total match with a `gated` helper → `ServerApi.Command.fs`;
//   - the deletions are stated, not drafted (a script cannot remove cases): `LogAnalyzerCmd`,
//     `LogAnalyzerCommand`, `LogAnalyzerResp`, `LogAnalyzerResponse`,
//     `OrderContextCommand.ReloadResources` and their `toString` arms → `Shared/Api.fs`; the
//     `ReloadResources` arm and the password guard of `OrderContextService.evaluate` →
//     `Services.fs`; the dead `ReloadResources` arms of the client → `App.fs`.
//
// The shared DU still has the doomed cases while this script runs, so the draft answers them
// with an error that the migration removes together with the cases. Run:
// `dotnet fsi Compute.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Shared.Types
open Shared.Api
open ServerApi


// ---------------------------------------------------------------------------------------------
// The dispatcher (→ ServerApi.Command.fs)
// ---------------------------------------------------------------------------------------------

module Command =

    /// A command that needs the formulary: refused with the provider's messages while it is not
    /// loaded, else run.
    let private gated (env: AppEnv) (run: unit -> Async<Result<Response, string[]>>) =
        match env.requireLoaded () with
        | Some msgs -> async { return Error msgs }
        | None -> run ()


    let processCmd (env: AppEnv) cmd =
        let gated = gated env

        match cmd with
        // the drug names come from the interaction source, not the formulary
        | InteractionCmd GetDrugNames ->
            async {
                let! result = env.interaction.getDrugNames ()
                return result |> Result.map (List.toArray >> DrugNamesLoaded >> InteractionResp)
            }
        | InteractionCmd(CheckInteractions drugs) ->
            gated (fun () ->
                async {
                    let! result = env.interaction.checkInteractions drugs
                    return result |> Result.map (List.toArray >> InteractionsChecked >> InteractionResp)
                }
            )
        | OrderContextCmd(ctxCmd, ctx) ->
            gated (fun () ->
                async {
                    let! result = env.orderContext.evaluate ctxCmd ctx
                    return result |> Result.map (OrderContextResult >> OrderContextResp)
                }
            )
        | OrderPlanCmd(UpdateOrderPlan(tp, cmdOpt)) ->
            gated (fun () ->
                async {
                    let! result = env.orderPlan.updateOrderPlan tp cmdOpt
                    return result |> Result.map (OrderPlanUpdated >> OrderPlanResp)
                }
            )
        | OrderPlanCmd(FilterOrderPlan tp) ->
            gated (fun () ->
                async {
                    let! result = env.orderPlan.filterOrderPlan tp
                    return result |> Result.map (OrderPlanFiltered >> OrderPlanResp)
                }
            )
        | FormularyCmd form ->
            gated (fun () ->
                async {
                    let! result = env.formulary.getFormulary form
                    return result |> Result.map FormularyResp
                }
            )
        | ParenteraliaCmd par ->
            gated (fun () ->
                async {
                    let! result = env.formulary.getParenteralia par
                    return result |> Result.map ParenteraliaResp
                }
            )
        | NutritionPlanCmd(InitNutritionPlan patient) ->
            gated (fun () ->
                async {
                    let! result = env.nutritionPlan.initNutritionPlan patient
                    return result |> Result.map (NutritionPlanInitialised >> NutritionPlanResp)
                }
            )
        | NutritionPlanCmd(UpdateNutritionOrderContext(plan, label, ctx)) ->
            gated (fun () ->
                async {
                    let! result = env.nutritionPlan.updateNutritionOrderContext (plan, label, ctx)
                    return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                }
            )
        | NutritionPlanCmd(SelectNutritionOrderScenario(plan, label, ctx)) ->
            gated (fun () ->
                async {
                    let! result = env.nutritionPlan.selectNutritionOrderScenario (plan, label, ctx)
                    return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                }
            )
        | NutritionPlanCmd(NavigateNutritionOrderContext(plan, label, ctxCmd, ctx)) ->
            gated (fun () ->
                async {
                    let! result = env.nutritionPlan.navigateNutritionOrderContext (plan, label, ctxCmd, ctx)
                    return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                }
            )
        | NutritionPlanCmd(AddNutritionContext(plan, category)) ->
            gated (fun () ->
                async {
                    let! result = env.nutritionPlan.addNutritionContext (plan, category)
                    return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                }
            )
        | NutritionPlanCmd(RemoveNutritionContext(plan, id)) ->
            gated (fun () ->
                async {
                    let! result = env.nutritionPlan.removeNutritionContext (plan, id)
                    return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                }
            )
        // gone with the migration, together with these cases
        | LogAnalyzerCmd _ -> async { return Error [| "LogAnalyzerCmd answers processAdmin" |] }


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Informedica.GenForm.Lib


/// An env over a provider whose load failed, with the ports stubbed.
let notLoaded =
    Adapters.makeAppEnv (Resources.CachedResourceProvider((fun () -> Error [ Informedica.GenForm.Lib.Types.ErrorMsg("load failed", None) ]), None))


let loaded =
    { notLoaded with
        requireLoaded = fun () -> None
        formulary =
            {
                getFormulary = fun f -> async { return Ok { f with Markdown = "stubbed" } }
                getParenteralia = fun _ -> async { return Ok Shared.Models.Parenteralia.empty }
            }
        interaction =
            {
                checkInteractions = fun _ -> async { return Ok [] }
                getDrugNames = fun () -> async { return Ok [ "paracetamol" ] }
            }
    }


let run env cmd =
    Command.processCmd env cmd |> Async.RunSynchronously


let tests =
    testList
        "Command.processCmd, total"
        [
            test "a loaded provider: the formulary answers" {
                match run loaded (FormularyCmd Shared.Models.Formulary.empty) with
                | Ok(FormularyResp f) -> f.Markdown |> Expect.equal "stubbed" "stubbed"
                | other -> failtest $"expected FormularyResp, got {other}"
            }

            test "a provider that did not load: the formulary is refused with its messages" {
                match run notLoaded (FormularyCmd Shared.Models.Formulary.empty) with
                | Error msgs -> msgs |> Array.exists (fun m -> m.Contains "load failed") |> Expect.isTrue "the messages"
                | Ok _ -> failtest "expected Error"
            }

            test "the drug names are not behind the formulary" {
                let env =
                    { notLoaded with
                        interaction =
                            {
                                checkInteractions = fun _ -> async { return Ok [] }
                                getDrugNames = fun () -> async { return Ok [ "paracetamol" ] }
                            }
                    }

                match run env (InteractionCmd GetDrugNames) with
                | Ok(InteractionResp(DrugNamesLoaded names)) -> names |> Expect.equal "the names" [| "paracetamol" |]
                | other -> failtest $"expected the names, got {other}"

                run env (InteractionCmd(CheckInteractions [ "a"; "b" ])) |> Result.isError |> Expect.isTrue "checking is"
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
