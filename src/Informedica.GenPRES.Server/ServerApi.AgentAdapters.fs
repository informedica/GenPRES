namespace ServerApi

/// Agent-backed adapter implementations for the AppEnv ports.
/// Each bounded domain context gets its own MailboxProcessor agent for:
///   - Per-component serialization (prevents cross-domain blocking)
///   - Debug-level console logging of command entry/completion
///   - Async boundary for all callers
module AgentAdapters =

    open Shared.Api
    open Informedica.GenForm.Lib
    open Informedica.Agents.Lib
    open Informedica.Utils.Lib.ConsoleWriter.NewLineTime
    // Open Shared.Types last so its Patient/OrderContext shadow GenForm's
    open Shared.Types


    // ---------------------------------------------------------------
    //  Formulary Component
    // ---------------------------------------------------------------

    [<RequireQualifiedAccess>]
    type FormularyCommand =
        | GetFormulary of Formulary
        | GetParenteralia of Parenteralia


    [<RequireQualifiedAccess>]
    type FormularyResponse =
        | Formulary of Result<Formulary, string[]>
        | Parenteralia of Result<Parenteralia, string[]>


    let private formularyCommandToString =
        function
        | FormularyCommand.GetFormulary _ -> "GetFormulary"
        | FormularyCommand.GetParenteralia _ -> "GetParenteralia"


    let private processFormularyCommand
        (provider: Resources.IResourceProvider)
        (cmd: FormularyCommand)
        : FormularyResponse
        =
        match cmd with
        | FormularyCommand.GetFormulary form -> form |> FormularyService.get provider |> FormularyResponse.Formulary

        | FormularyCommand.GetParenteralia par ->
            par
            |> ParenteraliaService.get provider
            |> Result.mapError Array.singleton
            |> FormularyResponse.Parenteralia

    // This helper function was extracted from createFormularyAgent (now deleted) as a step toward bypassing agent use
    // as discussed in issue 420.
    // Consider combining this with processFormularyCommand, since it having both a process and an execute function
    // might be confusing.
    // Once a decision is made about whether to keep the agent or not, remove this comment.
    let private executeFormularyCommand provider cmd =
        try
            writeDebugMessage $"[FormularyAgent] <- {cmd |> formularyCommandToString}"
            let response = processFormularyCommand provider cmd
            writeDebugMessage $"[FormularyAgent] -> {cmd |> formularyCommandToString} completed"
            response
        with ex ->
            writeErrorMessage $"[FormularyAgent] error in {cmd |> formularyCommandToString}: {ex}"

            match cmd with
            | FormularyCommand.GetFormulary _ -> FormularyResponse.Formulary(Error [| ex.Message |])
            | FormularyCommand.GetParenteralia _ -> FormularyResponse.Parenteralia(Error [| ex.Message |])

    let private extractFormulary =
        function
        | FormularyResponse.Formulary r -> r
        | other -> Error [| $"Unexpected response: {other}" |]


    let private extractParenteralia =
        function
        | FormularyResponse.Parenteralia r -> r
        | other -> Error [| $"Unexpected response: {other}" |]


    // ---------------------------------------------------------------
    //  OrderContext Component
    // ---------------------------------------------------------------

    [<RequireQualifiedAccess>]
    type OrderCtxCommand = Evaluate of OrderContextCommand * OrderContext


    [<RequireQualifiedAccess>]
    type OrderCtxResponse = OrderContext of Result<OrderContext, string[]>


    let private orderCtxCommandToString =
        function
        | OrderCtxCommand.Evaluate _ -> "EvaluateOrderContext"


    let private processOrderCtxCommand
        logAgent
        (logger: Informedica.Logging.Lib.Logger)
        (provider: Resources.IResourceProvider)
        (cmd: OrderCtxCommand)
        : OrderCtxResponse
        =
        let setComponent name =
            match logAgent with
            | Some a -> a |> Logging.setComponentName (Some name) |> Async.RunSynchronously
            | None -> ()

        match cmd with
        | OrderCtxCommand.Evaluate(ctxCmd, ctx) ->
            setComponent "OrderContext"

            ctx
            |> OrderContextService.evaluate logger provider ctxCmd
            |> OrderCtxResponse.OrderContext


    let private createOrderCtxAgent logAgent logger provider =
        Agent.createReply<OrderCtxCommand, OrderCtxResponse> (fun cmd ->
            try
                writeDebugMessage $"[OrderCtxAgent] <- {cmd |> orderCtxCommandToString}"
                let response = processOrderCtxCommand logAgent logger provider cmd
                writeDebugMessage $"[OrderCtxAgent] -> {cmd |> orderCtxCommandToString} completed"
                response
            with ex ->
                writeErrorMessage $"[OrderCtxAgent] error in {cmd |> orderCtxCommandToString}: {ex}"
                OrderCtxResponse.OrderContext(Error [| ex.Message |])
        )


    let private extractOrderContext =
        function
        | OrderCtxResponse.OrderContext r -> r


    // ---------------------------------------------------------------
    //  OrderPlan Component
    // ---------------------------------------------------------------

    [<RequireQualifiedAccess>]
    type OrderPlanCommand =
        | Update of OrderPlan * (OrderContextCommand * OrderContext) option
        | Filter of OrderPlan


    [<RequireQualifiedAccess>]
    type OrderPlanResponse = OrderPlan of Result<OrderPlan, string[]>


    let private orderPlanCommandToString =
        function
        | OrderPlanCommand.Update _ -> "UpdateOrderPlan"
        | OrderPlanCommand.Filter _ -> "FilterOrderPlan"


    let private processOrderPlanCommand
        logAgent
        (provider: Resources.IResourceProvider)
        (orderCtxPort: OrderContextPort)
        (cmd: OrderPlanCommand)
        : Async<OrderPlanResponse>
        =
        async {
            let totals = provider.GetTotals()

            let setComponent name =
                match logAgent with
                | Some a -> a |> Logging.setComponentName (Some name) |> Async.RunSynchronously
                | None -> ()

            match cmd with
            | OrderPlanCommand.Update(tp, cmdOpt) ->
                setComponent "OrderPlan"
                let! updated = OrderPlanService.updateOrderPlan orderCtxPort tp cmdOpt

                return
                    updated
                    |> OrderPlanService.calculateTotals totals
                    |> Ok
                    |> OrderPlanResponse.OrderPlan

            | OrderPlanCommand.Filter tp ->
                return
                    tp
                    |> OrderPlanService.calculateTotals totals
                    |> Ok
                    |> OrderPlanResponse.OrderPlan
        }


    let private createOrderPlanAgent logAgent provider orderCtxPort =
        Agent.createReplyAsync<OrderPlanCommand, OrderPlanResponse> (fun cmd ->
            async {
                try
                    writeDebugMessage $"[OrderPlanAgent] <- {cmd |> orderPlanCommandToString}"
                    let! response = processOrderPlanCommand logAgent provider orderCtxPort cmd
                    writeDebugMessage $"[OrderPlanAgent] -> {cmd |> orderPlanCommandToString} completed"
                    return response
                with ex ->
                    writeErrorMessage $"[OrderPlanAgent] error in {cmd |> orderPlanCommandToString}: {ex}"
                    return OrderPlanResponse.OrderPlan(Error [| ex.Message |])
            }
        )


    let private extractOrderPlan =
        function
        | OrderPlanResponse.OrderPlan r -> r


    // ---------------------------------------------------------------
    //  NutritionPlan Component
    // ---------------------------------------------------------------

    [<RequireQualifiedAccess>]
    type NutritionCommand =
        | Init of Patient
        | Add of NutritionPlan * NutritionCategory
        | Remove of NutritionPlan * string
        | UpdateOrderContext of NutritionPlan * string * OrderContext
        | SelectOrderScenario of NutritionPlan * string * OrderContext
        | Navigate of NutritionPlan * string * OrderContextCommand * OrderContext


    [<RequireQualifiedAccess>]
    type NutritionResponse = NutritionPlan of Result<NutritionPlan, string[]>


    let private nutritionCommandToString =
        function
        | NutritionCommand.Init _ -> "InitNutritionPlan"
        | NutritionCommand.Add _ -> "AddNutritionContext"
        | NutritionCommand.Remove _ -> "RemoveNutritionContext"
        | NutritionCommand.UpdateOrderContext _ -> "UpdateNutritionOrderContext"
        | NutritionCommand.SelectOrderScenario _ -> "SelectNutritionOrderScenario"
        | NutritionCommand.Navigate _ -> "NavigateNutritionOrderContext"


    let private processNutritionCommand
        (logger: Informedica.Logging.Lib.Logger)
        (provider: Resources.IResourceProvider)
        (orderCtxPort: OrderContextPort)
        (cmd: NutritionCommand)
        : Async<NutritionResponse>
        =
        async {
            let totals = provider.GetTotals()

            match cmd with
            | NutritionCommand.Init patient ->
                return
                    NutritionPlanService.initNutritionPlan logger totals patient
                    |> NutritionResponse.NutritionPlan

            | NutritionCommand.Add(plan, category) ->
                let! result = NutritionPlanService.addNutritionContext totals orderCtxPort (plan, category)
                return result |> NutritionResponse.NutritionPlan

            | NutritionCommand.Remove(plan, id) ->
                return
                    NutritionPlanService.removeNutritionContext totals (plan, id)
                    |> NutritionResponse.NutritionPlan

            | NutritionCommand.UpdateOrderContext(plan, label, ctx) ->
                let! result = NutritionPlanService.updateNutritionOrderContext totals orderCtxPort (plan, label, ctx)
                return result |> NutritionResponse.NutritionPlan

            | NutritionCommand.SelectOrderScenario(plan, label, ctx) ->
                let! result = NutritionPlanService.selectNutritionOrderScenario totals orderCtxPort (plan, label, ctx)
                return result |> NutritionResponse.NutritionPlan

            | NutritionCommand.Navigate(plan, label, ctxCmd, ctx) ->
                let! result =
                    NutritionPlanService.navigateNutritionOrderContext totals orderCtxPort (plan, label, ctxCmd, ctx)

                return result |> NutritionResponse.NutritionPlan
        }


    let private createNutritionAgent logger provider orderCtxPort =
        Agent.createReplyAsync<NutritionCommand, NutritionResponse> (fun cmd ->
            async {
                try
                    writeDebugMessage $"[NutritionAgent] <- {cmd |> nutritionCommandToString}"
                    let! response = processNutritionCommand logger provider orderCtxPort cmd
                    writeDebugMessage $"[NutritionAgent] -> {cmd |> nutritionCommandToString} completed"
                    return response
                with ex ->
                    writeErrorMessage $"[NutritionAgent] error in {cmd |> nutritionCommandToString}: {ex}"
                    return NutritionResponse.NutritionPlan(Error [| ex.Message |])
            }
        )


    let private extractNutritionPlan =
        function
        | NutritionResponse.NutritionPlan r -> r


    // ---------------------------------------------------------------
    //  Interaction Component
    // ---------------------------------------------------------------

    [<RequireQualifiedAccess>]
    type InteractionCommand =
        | Check of string list
        | GetNames


    [<RequireQualifiedAccess>]
    type InteractionResponse =
        | Checked of Result<DrugInteraction list, string[]>
        | Names of Result<string list, string[]>


    let private interactionCommandToString =
        function
        | InteractionCommand.Check _ -> "CheckInteractions"
        | InteractionCommand.GetNames -> "GetDrugNames"


    let private processInteractionCommand (cachedJson: string option) (cmd: InteractionCommand) : InteractionResponse =
        match cmd with
        | InteractionCommand.Check drugNames ->
            try
                let result =
                    Informedica.GenInteract.Lib.Api.checkInteractions cachedJson drugNames
                    |> List.map Adapters.toSharedDrugInteraction

                InteractionResponse.Checked(Ok result)
            with ex ->
                InteractionResponse.Checked(Error [| ex.Message |])

        | InteractionCommand.GetNames ->
            try
                let result = Informedica.GenInteract.Lib.Api.getDrugNames cachedJson

                InteractionResponse.Names(Ok result)
            with ex ->
                InteractionResponse.Names(Error [| ex.Message |])


    let private createInteractionAgent () =
        let cachedJson = Adapters.loadInteractionJson ()

        Agent.createReply<InteractionCommand, InteractionResponse> (fun cmd ->
            try
                writeDebugMessage $"[InteractionAgent] <- {cmd |> interactionCommandToString}"
                let response = processInteractionCommand cachedJson cmd
                writeDebugMessage $"[InteractionAgent] -> {cmd |> interactionCommandToString} completed"
                response
            with ex ->
                writeErrorMessage $"[InteractionAgent] error in {cmd |> interactionCommandToString}: {ex}"
                InteractionResponse.Checked(Error [| ex.Message |])
        )


    let private extractInteractions =
        function
        | InteractionResponse.Checked r -> r
        | _ -> Error [| "Unexpected interaction response" |]


    let private extractDrugNames =
        function
        | InteractionResponse.Names r -> r
        | _ -> Error [| "Unexpected interaction response" |]


    // ---------------------------------------------------------------
    //  Helper
    // ---------------------------------------------------------------

    /// Helper: post a command to the agent and extract the expected response case.
    let private postAsync (agent: Agent<_>) cmd extract =
        async {
            let! response = agent |> Agent.postAndAsyncReply cmd
            return extract response
        }


    // ---------------------------------------------------------------
    //  Composition: build AppEnv with per-component agents
    // ---------------------------------------------------------------

    /// Build an AppEnv backed by per-component MailboxProcessor agents.
    /// Each domain context gets its own agent, providing:
    ///   - Independent serialization per component (no cross-domain blocking)
    ///   - Thread-safe access to the provider within each component
    /// Note: requireLoaded is not routed through any agent because it only
    /// reads the thread-safe ResourceInfo and returns a different type
    /// (string[] option) than the agents' response DUs.
    let makeAppEnv (provider: Resources.IResourceProvider) : AppEnv =
        let logAgent, logger =
            match Logging.loggingLevel with
            | None -> None, Informedica.GenOrder.Lib.OrderLogging.noOp
            | Some level ->
                let a = Logging.getLogger level Logging.OrderLogger
                (Some a, a.Logger)

        let orderCtxAgent = createOrderCtxAgent logAgent logger provider

        // Build the OrderContext port backed by its agent, so OrderPlan
        // and Nutrition agents can route inter-component calls through it.
        let orderCtxPort: OrderContextPort =
            {
                evaluate =
                    fun ctxCmd ctx ->
                        postAsync orderCtxAgent (OrderCtxCommand.Evaluate(ctxCmd, ctx)) extractOrderContext
            }

        let orderPlanAgent = createOrderPlanAgent logAgent provider orderCtxPort
        let nutritionAgent = createNutritionAgent logger provider orderCtxPort
        let interactionAgent = createInteractionAgent ()

        {
            formulary =
                {
                    getFormulary =
                        FormularyCommand.GetFormulary
                        >> executeFormularyCommand provider
                        >> extractFormulary
                        >> async.Return
                    getParenteralia =
                        FormularyCommand.GetParenteralia
                        >> executeFormularyCommand provider
                        >> extractParenteralia
                        >> async.Return
                }

            orderContext = orderCtxPort

            orderPlan =
                {
                    updateOrderPlan =
                        fun tp cmdOpt -> postAsync orderPlanAgent (OrderPlanCommand.Update(tp, cmdOpt)) extractOrderPlan
                    filterOrderPlan = fun tp -> postAsync orderPlanAgent (OrderPlanCommand.Filter tp) extractOrderPlan
                }

            nutritionPlan =
                {
                    initNutritionPlan =
                        fun patient -> postAsync nutritionAgent (NutritionCommand.Init patient) extractNutritionPlan
                    addNutritionContext =
                        fun (plan, category) ->
                            postAsync nutritionAgent (NutritionCommand.Add(plan, category)) extractNutritionPlan
                    removeNutritionContext =
                        fun (plan, id) ->
                            postAsync nutritionAgent (NutritionCommand.Remove(plan, id)) extractNutritionPlan
                    updateNutritionOrderContext =
                        fun (plan, label, ctx) ->
                            postAsync
                                nutritionAgent
                                (NutritionCommand.UpdateOrderContext(plan, label, ctx))
                                extractNutritionPlan
                    selectNutritionOrderScenario =
                        fun (plan, label, ctx) ->
                            postAsync
                                nutritionAgent
                                (NutritionCommand.SelectOrderScenario(plan, label, ctx))
                                extractNutritionPlan
                    navigateNutritionOrderContext =
                        fun (plan, label, ctxCmd, ctx) ->
                            postAsync
                                nutritionAgent
                                (NutritionCommand.Navigate(plan, label, ctxCmd, ctx))
                                extractNutritionPlan
                }

            interaction =
                {
                    checkInteractions =
                        fun drugs -> postAsync interactionAgent (InteractionCommand.Check drugs) extractInteractions

                    getDrugNames = fun () -> postAsync interactionAgent InteractionCommand.GetNames extractDrugNames
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
