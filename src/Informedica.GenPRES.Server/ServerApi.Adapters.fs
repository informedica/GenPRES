namespace ServerApi


module Adapters =

    open Informedica.GenForm.Lib


    let private interactionJsonCache =
        lazy
            (let path =
                System.IO.Path.Combine(Informedica.Utils.Lib.AppPath.interactionsDir (), "Data.JSON")
                |> System.IO.Path.GetFullPath

             if System.IO.File.Exists(path) then
                 System.IO.File.ReadAllText(path) |> Some
             else
                 None)


    let loadInteractionJson () = interactionJsonCache.Value


    let toSharedDrugInteraction (di: Informedica.GenInteract.Lib.DrugInteraction) : Shared.Types.DrugInteraction =
        {
            Name = di.Name
            Drug1 = di.Drug1
            Drug2 = di.Drug2
        }


    let private resolveLogger () =
        match Logging.loggingLevel with
        | None -> None, Informedica.GenOrder.Lib.OrderLogging.noOp
        | Some level ->
            let agent = Logging.getLogger level Logging.OrderLogger
            (Some agent, agent.Logger)


    let private setComponentName name agent =
        async {
            match agent with
            | Some a -> do! a |> Logging.setComponentName (Some name)
            | None -> ()
        }


    let private makeFormularyPort (provider: Resources.IResourceProvider) : FormularyPort =
        {
            getFormulary = fun form -> async { return form |> FormularyService.get provider }

            getParenteralia =
                fun par -> async { return par |> ParenteraliaService.get provider |> Result.mapError Array.singleton }
        }


    let private makeOrderContextPort agent logger (provider: Resources.IResourceProvider) : OrderContextPort =
        {
            evaluate =
                fun ctxCmd ctx ->
                    async {
                        do! setComponentName "OrderContext" agent

                        return ctx |> OrderContextService.evaluate logger provider ctxCmd
                    }
        }


    let private makeOrderPlanPort
        agent
        (provider: Resources.IResourceProvider)
        (orderCtxPort: OrderContextPort)
        : OrderPlanPort
        =
        let totals = provider.GetTotals()

        {
            updateOrderPlan =
                fun tp cmdOpt ->
                    async {
                        do! setComponentName "OrderPlan" agent

                        let! updated = OrderPlanService.updateOrderPlan orderCtxPort tp cmdOpt
                        let totals = provider.GetTotals()
                        return updated |> OrderPlanService.calculateTotals totals |> Ok
                    }

            filterOrderPlan = fun tp -> async { return tp |> OrderPlanService.calculateTotals totals |> Ok }
        }


    let private makeNutritionPlanPort
        (orderCtxPort: OrderContextPort)
        logger
        (provider: Resources.IResourceProvider)
        : NutritionPlanPort
        =
        let totals = provider.GetTotals()

        {
            initNutritionPlan =
                fun patient -> async { return NutritionPlanService.initNutritionPlan logger totals patient }

            addNutritionContext =
                fun (plan, category) -> NutritionPlanService.addNutritionContext totals orderCtxPort (plan, category)

            removeNutritionContext =
                fun (plan, id) -> async { return NutritionPlanService.removeNutritionContext totals (plan, id) }

            updateNutritionOrderContext =
                fun (plan, label, ctx) ->
                    NutritionPlanService.updateNutritionOrderContext totals orderCtxPort (plan, label, ctx)

            selectNutritionOrderScenario =
                fun (plan, label, ctx) ->
                    NutritionPlanService.selectNutritionOrderScenario totals orderCtxPort (plan, label, ctx)

            navigateNutritionOrderContext =
                fun (plan, label, ctxCmd, ctx) ->
                    NutritionPlanService.navigateNutritionOrderContext totals orderCtxPort (plan, label, ctxCmd, ctx)
        }


    let makeAppEnv (provider: Resources.IResourceProvider) : AppEnv =
        let agent, logger = resolveLogger ()
        let orderCtxPort = makeOrderContextPort agent logger provider

        {
            formulary = makeFormularyPort provider
            orderContext = orderCtxPort
            orderPlan = makeOrderPlanPort agent provider orderCtxPort
            nutritionPlan = makeNutritionPlanPort orderCtxPort logger provider
            interaction =
                {
                    checkInteractions =
                        fun drugs ->
                            async {
                                try
                                    let result =
                                        Informedica.GenInteract.Lib.Api.checkInteractions (loadInteractionJson ()) drugs
                                        |> List.map toSharedDrugInteraction

                                    return Ok result
                                with ex ->
                                    return Error [| ex.Message |]
                            }

                    getDrugNames =
                        fun () ->
                            async {
                                try
                                    let result = Informedica.GenInteract.Lib.Api.getDrugNames (loadInteractionJson ())

                                    return Ok result
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                }
            logAnalyzer =
                {
                    listLogFiles =
                        fun () ->
                            async {
                                try
                                    return Ok(LogAnalyzer.listLogFiles ())
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                    analyzeLogFile = fun fileName -> async { return LogAnalyzer.analyzeFile fileName }
                }
            requireLoaded =
                fun () ->
                    let info = provider.GetResourceInfo()

                    if info.IsLoaded then
                        None
                    else
                        info.Messages |> Array.map (fun msg -> FormLogging.formatMessage msg) |> Some
        }
