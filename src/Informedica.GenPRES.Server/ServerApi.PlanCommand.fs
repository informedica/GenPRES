namespace ServerApi


/// The plan member: the one plan, nutrition included, over the plan port.
module PlanCommand =

    open Shared.Api


    let processCmd (env: AppEnv) (cmd: PlanCommand) =
        match cmd with
        | PlanCommand.Recalculate plan -> env.plan.recalculate plan
        | PlanCommand.Navigate(plan, contextId, ctxCmd, ctx) -> env.plan.navigate plan contextId ctxCmd ctx
        | PlanCommand.AddOrderContext(plan, ctx) -> env.plan.addOrderContext plan ctx
        | PlanCommand.NewOrderContext(plan, category) -> env.plan.newOrderContext plan category
        | PlanCommand.RemoveOrderContexts(plan, ids) -> env.plan.removeOrderContexts plan ids
        | PlanCommand.Open(pat, contexts) -> env.plan.openWith pat contexts
