namespace ServerApi


module Command =

    open Shared.Api


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
