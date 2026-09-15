namespace ServerApi


/// The plan member: the one plan, nutrition included, over the plan port.
module OrderPlanCommand =

    open Shared.Api


    /// The plan's patient and that of every context it carries, and the context's where a
    /// command carries one, made at the ingress; a draft that is none is refused.
    let processCmd (env: AppEnv) (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Recalculate plan ->
            Ingress.overAll (Ingress.ofPlan plan) (fun () -> env.orderPlan.recalculate plan)
        | OrderPlanCommand.Navigate(plan, contextId, ctxCmd, ctx) ->
            Ingress.overAll
                (Ingress.ofPlan plan @ [ ctx.Patient ])
                (fun () -> env.orderPlan.navigate plan contextId ctxCmd ctx)
        | OrderPlanCommand.AddOrderContext(plan, ctx) ->
            Ingress.overAll (Ingress.ofPlan plan @ [ ctx.Patient ]) (fun () -> env.orderPlan.addOrderContext plan ctx)
        | OrderPlanCommand.NewOrderContext(plan, category) ->
            Ingress.overAll (Ingress.ofPlan plan) (fun () -> env.orderPlan.newOrderContext plan category)
        | OrderPlanCommand.RemoveOrderContexts(plan, ids) ->
            Ingress.overAll (Ingress.ofPlan plan) (fun () -> env.orderPlan.removeOrderContexts plan ids)
        | OrderPlanCommand.Open(pat, contexts) ->
            Ingress.overAll
                (pat :: (contexts |> Array.map _.Patient |> Array.toList))
                (fun () -> env.orderPlan.openWith pat contexts)
