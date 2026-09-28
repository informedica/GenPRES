/// What the plan holds beside the order plan version last opened or signed, and how the plan
/// commands move it.
module PlanWorkPolicy

open Shared.Api


/// What the plan holds beside the order plan version last opened or signed.
[<RequireQualifiedAccess>]
type PlanWork =
    /// The plan is the version last opened or signed, or empty with none opened yet.
    | AsSigned
    /// Commands changed the plan since; nothing signed holds the changes.
    | Changed


module PlanWork =

    /// Whether a plan command changes what the plan holds. A navigation within an order, an
    /// order added, a nutrition workbench opened in the plan and contexts removed do; the
    /// totals recomputed and a signed version opened do not.
    let changedBy (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Recalculate _
        | OrderPlanCommand.Open _ -> false
        | OrderPlanCommand.Navigate _
        | OrderPlanCommand.AddOrderContext _
        | OrderPlanCommand.NewOrderContext _
        | OrderPlanCommand.RemoveOrderContexts _ -> true


    /// The plan's work once a command went out.
    let afterCommand (cmd: OrderPlanCommand) (work: PlanWork) = if changedBy cmd then PlanWork.Changed else work
