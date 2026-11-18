namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// Detects pending work besides requests: a plan based on older patient data, which blocks like a pending
/// request.
module OutPolicy =

    open Informedica.GenPRES.Client.Core.StateMachines


    /// Whether the orders are based on other patient data than the panel shows while no patient or plan
    /// request is pending, as after a failed update. The orders do not fit the shown data, so nothing
    /// continues until the plan is updated.
    let planBehind patient orderPlan =
        match
            PatientMachine.PatientState.changing patient,
            OrderPlanMachine.OrderPlanState.inFlightRequest orderPlan,
            OrderPlanMachine.OrderPlanState.patient orderPlan,
            PatientMachine.PatientState.current patient
        with
        | false, None, Some onPlan, Some shown -> onPlan <> shown
        | _ -> false
