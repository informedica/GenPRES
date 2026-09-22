/// What leaving the page would lose, and what tells it. Pure F#, no React, so it runs under
/// Expecto; App.fs asks it from the browser's beforeunload listener and keeps the plan's work
/// as the plan commands go out and the versions come in.
module UnsignedWorkPolicy

open Shared.Types
open PlanWorkPolicy
open SigningMachine


/// The work a Sign is asked over, beside the plan's work: what the App keeps between the Sign
/// and the signature told.
module PlanWork =

    /// The work a Sign is asked over: the plan's work when no signature is under way; the work
    /// kept otherwise, since a Sign while one is under way is ignored and signs nothing.
    let askedOver (signing: SigningView) (work: PlanWork) (kept: PlanWork) =
        match signing with
        | SigningView.Idle -> work
        | _ -> kept


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
