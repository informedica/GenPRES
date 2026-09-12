namespace ServerApi


/// The plan member: the one plan, nutrition included, over the plan port.
module PlanCommand =

    open Shared.Api


    let processCmd (env: AppEnv) (cmd: PlanCommand) =
        match cmd with
        | PlanCommand.Recalculate plan -> env.plan.recalculate plan
        | PlanCommand.Navigate(plan, contextId, ctxCmd, ctx) -> env.plan.navigate plan contextId ctxCmd ctx
        | PlanCommand.AddContext(plan, category) -> env.plan.addContext plan category
        | PlanCommand.RemoveContext(plan, id) -> env.plan.removeContext plan id
        | PlanCommand.RemoveOrders(plan, ids) -> env.plan.removeOrders plan ids
