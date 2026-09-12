namespace ServerApi


module Command =

    open Shared.Api

    // the password and token checks live in AdminCommand until the LogAnalyzerCmd family
    // leaves this dispatcher (#654)


    let processCmd (env: AppEnv) cmd =
        match cmd with
        | InteractionCmd GetDrugNames ->
            async {
                let! result = env.interaction.getDrugNames ()
                return result |> Result.map (List.toArray >> DrugNamesLoaded >> InteractionResp)
            }
        | LogAnalyzerCmd(ValidatePassword password) ->
            async {
                let secret = env.admin.secret ()

                if AdminCommand.validatePassword secret password then
                    let token = AdminCommand.generateToken secret (env.admin.now ())
                    return Ok(PasswordValidated(true, token) |> LogAnalyzerResp)
                else
                    return Ok(PasswordValidated(false, "") |> LogAnalyzerResp)
            }
        | LogAnalyzerCmd(ListLogFiles token) ->
            if not (AdminCommand.validateToken (env.admin.secret ()) (env.admin.now ()) token) then
                async { return Error [| "Invalid token" |] }
            else
                async {
                    let! result = env.admin.listLogFiles ()
                    return result |> Result.map (LogFilesListed >> LogAnalyzerResp)
                }
        | LogAnalyzerCmd(AnalyzeLogFile(token, fileName)) ->
            if not (AdminCommand.validateToken (env.admin.secret ()) (env.admin.now ()) token) then
                async { return Error [| "Invalid token" |] }
            else
                async {
                    let! result = env.admin.analyzeLogFile fileName
                    return result |> Result.map (LogFileAnalyzed >> LogAnalyzerResp)
                }
        | _ ->
            match env.requireLoaded () with
            | Some msgs -> async { return Error msgs }
            | None ->
                match cmd with
                | OrderContextCmd(ctxCmd, ctx) ->
                    async {
                        let! result = env.orderContext.evaluate ctxCmd ctx
                        return result |> Result.map (OrderContextResult >> OrderContextResp)
                    }

                | OrderPlanCmd(UpdateOrderPlan(tp, cmdOpt)) ->
                    async {
                        let! result = env.orderPlan.updateOrderPlan tp cmdOpt
                        return result |> Result.map (OrderPlanUpdated >> OrderPlanResp)
                    }

                | OrderPlanCmd(FilterOrderPlan tp) ->
                    async {
                        let! result = env.orderPlan.filterOrderPlan tp
                        return result |> Result.map (OrderPlanFiltered >> OrderPlanResp)
                    }

                | FormularyCmd form ->
                    async {
                        let! result = env.formulary.getFormulary form
                        return result |> Result.map FormularyResp
                    }

                | ParenteraliaCmd par ->
                    async {
                        let! result = env.formulary.getParenteralia par
                        return result |> Result.map ParenteraliaResp
                    }

                | NutritionPlanCmd(InitNutritionPlan patient) ->
                    async {
                        let! result = env.nutritionPlan.initNutritionPlan patient
                        return result |> Result.map (NutritionPlanInitialised >> NutritionPlanResp)
                    }

                | NutritionPlanCmd(UpdateNutritionOrderContext(plan, label, ctx)) ->
                    async {
                        let! result = env.nutritionPlan.updateNutritionOrderContext (plan, label, ctx)
                        return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                    }

                | NutritionPlanCmd(SelectNutritionOrderScenario(plan, label, ctx)) ->
                    async {
                        let! result = env.nutritionPlan.selectNutritionOrderScenario (plan, label, ctx)
                        return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                    }

                | NutritionPlanCmd(NavigateNutritionOrderContext(plan, label, ctxCmd, ctx)) ->
                    async {
                        let! result = env.nutritionPlan.navigateNutritionOrderContext (plan, label, ctxCmd, ctx)
                        return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                    }

                | NutritionPlanCmd(AddNutritionContext(plan, category)) ->
                    async {
                        let! result = env.nutritionPlan.addNutritionContext (plan, category)
                        return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                    }

                | NutritionPlanCmd(RemoveNutritionContext(plan, id)) ->
                    async {
                        let! result = env.nutritionPlan.removeNutritionContext (plan, id)
                        return result |> Result.map (NutritionPlanUpdated >> NutritionPlanResp)
                    }

                | InteractionCmd(CheckInteractions drugs) ->
                    async {
                        let! result = env.interaction.checkInteractions drugs
                        return result |> Result.map (List.toArray >> InteractionsChecked >> InteractionResp)
                    }

                | _ ->
                    async {
                        return
                            Error
                                [|
                                    $"Unexpected command after requireLoaded: {cmd |> Command.toString}"
                                |]
                    }
