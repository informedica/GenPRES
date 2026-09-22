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
    /// Commands changed the plan since, this many; nothing signed holds the changes. The count
    /// tells the work a signature was asked over from work done while it was under way.
    | Changed of generation: int


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


    /// The plan's work once a change went out: one more.
    let afterChange (work: PlanWork) =
        match work with
        | PlanWork.AsSigned -> PlanWork.Changed 1
        | PlanWork.Changed n -> PlanWork.Changed(n + 1)


    /// The plan's work once a command went out.
    let afterCommand (cmd: OrderPlanCommand) (work: PlanWork) = if changedBy cmd then afterChange work else work


    /// The work a Sign is asked over: the plan's work when no signature is under way; the work
    /// kept otherwise, since a Sign while one is under way is ignored and signs nothing.
    let askedOver (signing: SigningView) (work: PlanWork) (kept: PlanWork) =
        match signing with
        | SigningView.Idle -> work
        | _ -> kept


    /// The plan's work once a signature is told: as signed when the plan is still the one the
    /// signature was asked over; a change made while the signature was under way was not
    /// signed, and stays.
    let afterSigned (atSign: PlanWork) (work: PlanWork) = if work = atSign then PlanWork.AsSigned else work


/// Whether leaving the page would lose work: a medication on the prescribing workbench, a
/// signature under way, or a plan changed since the version last opened or signed. An
/// anonymous plan is never signed, so any order put in it is a change.
let hasUnsignedWork (workbench: OrderContext option) (signing: SigningView) (plan: PlanWork) =
    let onWorkbench = workbench |> Option.exists (fun ctx -> ctx.Filter.Generic.IsSome)

    let signing =
        match signing with
        | SigningView.Idle -> false
        | _ -> true

    onWorkbench || signing || plan <> PlanWork.AsSigned
