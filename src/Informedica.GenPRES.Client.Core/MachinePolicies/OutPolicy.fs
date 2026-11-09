namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// What is out besides the requests: a plan behind the patient data, which waits as a request does.
module OutPolicy =

    open Informedica.GenPRES.Client.Core.StateMachines


    /// Whether the orders stand on other patient data than the panel with no request out for the
    /// patient or the plan, which a failed update leaves. The orders do not fit the data shown, so nothing goes on
    /// until the plan has followed.
    let planBehind patient orderPlan =
        match
            PatientMachine.PatientState.changing patient,
            OrderPlanMachine.OrderPlanState.inFlightRequest orderPlan,
            OrderPlanMachine.OrderPlanState.patient orderPlan,
            PatientMachine.PatientState.current patient
        with
        | false, None, Some onPlan, Some shown -> onPlan <> shown
        | _ -> false
