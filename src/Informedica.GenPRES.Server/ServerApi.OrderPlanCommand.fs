namespace ServerApi


/// The plan member: the one plan, nutrition included, over the plan port.
module OrderPlanCommand =

    open Shared.Api


    /// The plan's patient, and the context's where a command carries one, made at the ingress;
    /// a draft that is none is refused.
    let processCmd (env: AppEnv) (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Recalculate plan -> Ingress.over plan.Patient (fun () -> env.orderPlan.recalculate plan)
        | OrderPlanCommand.Navigate(plan, contextId, ctxCmd, ctx) ->
            Ingress.over
                plan.Patient
                (fun () -> Ingress.over ctx.Patient (fun () -> env.orderPlan.navigate plan contextId ctxCmd ctx))
        | OrderPlanCommand.AddOrderContext(plan, ctx) ->
            Ingress.over
                plan.Patient
                (fun () -> Ingress.over ctx.Patient (fun () -> env.orderPlan.addOrderContext plan ctx))
        | OrderPlanCommand.NewOrderContext(plan, category) ->
            Ingress.over plan.Patient (fun () -> env.orderPlan.newOrderContext plan category)
        | OrderPlanCommand.RemoveOrderContexts(plan, ids) ->
            Ingress.over plan.Patient (fun () -> env.orderPlan.removeOrderContexts plan ids)
        | OrderPlanCommand.Open(pat, contexts) -> Ingress.over pat (fun () -> env.orderPlan.openWith pat contexts)
