namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// Decides whether leaving the page would lose work.
module UnsignedWorkPolicy =

    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Shared.Types
    open PlanWorkPolicy
    open SigningMachine


    /// Whether a medication is picked on the workbench.
    let medicationPicked (workbench: OrderContext option) =
        workbench |> Option.exists (fun ctx -> ctx.Filter.Generic.IsSome)


    /// Whether a nutrition slot is narrowed to one order, picked or not, which leaving the page would lose.
    let slotNarrowed slots =
        slots
        |> List.exists (Informedica.GenPRES.Shared.Models.OrderContext.contribution >> Option.isSome)


    /// Whether the workbench or a nutrition slot holds work: a medication picked on the workbench or a
    /// nutrition slot narrowed to one order.
    let unsignedOrderWork workbench slots = medicationPicked workbench || slotNarrowed slots


    /// Whether leaving the page would lose work: the work on the workbench and the nutrition slots, a
    /// signature under way, or a plan changed since the version last opened or signed. An anonymous plan
    /// is never signed, so any order in it counts as a change.
    let hasUnsignedWork workbench slots (signing: SigningView) (plan: PlanWork) =
        let signing =
            match signing with
            | SigningView.Idle -> false
            | _ -> true

        unsignedOrderWork workbench slots || signing || plan <> PlanWork.AsSigned
