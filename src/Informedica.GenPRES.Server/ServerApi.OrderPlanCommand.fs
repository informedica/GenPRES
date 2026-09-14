namespace ServerApi


/// The plan member: the one plan, nutrition included, over the plan port.
module OrderPlanCommand =

    open Shared.Api


    let processCmd (env: AppEnv) (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Recalculate plan -> env.orderPlan.recalculate plan
        | OrderPlanCommand.Navigate(plan, contextId, ctxCmd, ctx) -> env.orderPlan.navigate plan contextId ctxCmd ctx
        | OrderPlanCommand.AddOrderContext(plan, ctx) -> env.orderPlan.addOrderContext plan ctx
        | OrderPlanCommand.NewOrderContext(plan, category) -> env.orderPlan.newOrderContext plan category
        | OrderPlanCommand.RemoveOrderContexts(plan, ids) -> env.orderPlan.removeOrderContexts plan ids
        | OrderPlanCommand.Open(pat, contexts) -> env.orderPlan.openWith pat contexts
