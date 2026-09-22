/// What leaving the page would lose, and what tells it. Pure F#, no React, so it runs under
/// Expecto; App.fs asks it from the browser's beforeunload listener.
module UnsignedWorkPolicy

open Shared.Types
open PlanWorkPolicy
open SigningMachine


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
