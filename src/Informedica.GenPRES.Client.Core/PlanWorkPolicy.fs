/// Tracks whether the order plan has changed since the version last opened or signed.
module PlanWorkPolicy

open Shared.Api


/// Whether the order plan has changed since the version last opened or signed.
[<RequireQualifiedAccess>]
type PlanWork =
    /// The plan is the version last opened or signed, or empty when none was opened.
    | AsSigned
    /// Commands changed the plan since; the changes are not signed.
    | Changed


/// Functions over PlanWork.
module PlanWork =

    /// Whether a plan command changes the plan: navigating an order, adding an order, opening a
    /// new nutrition context and removing contexts do; a patient update, a row filter and opening
    /// a version do not.
    let changedBy (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.UpdatePatient _
        | OrderPlanCommand.FilterRows _
        | OrderPlanCommand.Open _ -> false
        | OrderPlanCommand.Navigate _
        | OrderPlanCommand.AddOrderContext _
        | OrderPlanCommand.NewOrderContext _
        | OrderPlanCommand.RemoveOrderContexts _ -> true


    /// The plan's state after a command.
    let afterCommand (cmd: OrderPlanCommand) (work: PlanWork) = if changedBy cmd then PlanWork.Changed else work
