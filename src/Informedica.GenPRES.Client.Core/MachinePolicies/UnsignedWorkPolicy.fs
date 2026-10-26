namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// Decides whether leaving the page would lose work.
module UnsignedWorkPolicy =

    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Shared.Types
    open PlanWorkPolicy
    open SigningMachine


    /// Whether leaving the page would lose work: a medication on the prescribing workbench, a
    /// signature under way, or a plan changed since the version last opened or signed. An
    /// anonymous plan is never signed, so any order in it counts as a change.
    let hasUnsignedWork (workbench: OrderContext option) (signing: SigningView) (plan: PlanWork) =
        let onWorkbench = workbench |> Option.exists (fun ctx -> ctx.Filter.Generic.IsSome)

        let signing =
            match signing with
            | SigningView.Idle -> false
            | _ -> true

        onWorkbench || signing || plan <> PlanWork.AsSigned
