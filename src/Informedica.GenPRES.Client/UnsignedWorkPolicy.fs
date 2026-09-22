/// What leaving the page would lose, and what tells it. Pure F#, no React, so it runs under
/// Expecto; App.fs asks it from the browser's beforeunload listener and keeps the plan's work
/// as the plan commands go out and the versions come in.
module UnsignedWorkPolicy

open Shared.Types
open Shared.Api
open SigningMachine


/// What the plan holds beside the order plan version last opened or signed.
[<RequireQualifiedAccess>]
type PlanWork =
    /// The plan is the version last opened or signed, or empty with none opened yet.
    | AsSigned
    /// A command changed the plan since; nothing signed holds the change.
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


/// Whether leaving the page would lose work: a medication on the prescribing workbench, a
/// signature under way, or a plan changed since the version last opened or signed. An
/// anonymous plan is never signed, so any order put in it is a change.
let hasUnsignedWork (workbench: OrderContext option) (signing: Signing) (plan: PlanWork) =
    let onWorkbench = workbench |> Option.exists (fun ctx -> ctx.Filter.Generic.IsSome)

    let signing =
        match signing with
        | Signing.Idle -> false
        | _ -> true

    onWorkbench || signing || plan = PlanWork.Changed
