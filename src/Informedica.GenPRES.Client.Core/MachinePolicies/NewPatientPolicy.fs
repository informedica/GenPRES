namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// Distinguishes another patient from a patient data update. Without an identified patient nothing shows
/// the identity, so a panel edit is taken as another patient and everything is cleared. With an identified
/// patient a panel edit is a patient data update, and the orders follow it.
module NewPatientPolicy =

    open Informedica.GenPRES.Shared.Types


    /// Whether a panel edit is taken as another patient: when no patient is identified.
    let startsNewPatient session = SessionPatientPolicy.identified session |> not


    /// Whether the panel asks before an edit: the edit is taken as another patient and there is something to
    /// lose, an order in the plan or unsigned work on the workbench or in the nutrition slots.
    let asks session (plan: OrderPlan option) workbench slots =
        let orders =
            plan
            |> Option.exists (Informedica.GenPRES.Shared.Models.OrderPlan.orders >> Array.isEmpty >> not)

        startsNewPatient session
        && (orders || UnsignedWorkPolicy.unsignedOrderWork workbench slots)
