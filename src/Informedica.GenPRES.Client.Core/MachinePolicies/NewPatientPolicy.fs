namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// Tells a new patient, another identity, from a patient data update, the same patient with other
/// data. Without an identified patient nothing shows the identity, so an edit on the panel is taken
/// as a new patient and everything clears. An identified patient stays the same patient, so an edit
/// there is a patient data update and the orders follow it.
module NewPatientPolicy =

    open Informedica.GenPRES.Shared.Types


    /// Whether an edit on the panel is taken as a new patient: no patient is identified.
    let startsNewPatient session = HeldPanelPolicy.identified session |> not


    /// Whether the panel asks before an edit: the edit is taken as a new patient and there is
    /// something to lose, an order in the plan or a medication picked on the workbench.
    let asks session (plan: OrderPlan option) (workbench: OrderContext option) =
        let orders =
            plan
            |> Option.exists (Informedica.GenPRES.Shared.Models.OrderPlan.orders >> Array.isEmpty >> not)

        startsNewPatient session
        && (orders || UnsignedWorkPolicy.medicationPicked workbench)
