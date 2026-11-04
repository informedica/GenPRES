namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// What is out: the requests of the lanes, a field counting its step clicks and the loads.
module OutPolicy =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.StateMachines
    open Loads


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


    /// What holds the pages: the requests out, a field counting its step clicks, the lanes, the nutrition
    /// slots and the loads, and a plan behind the patient data, which is no request but waits as one does.
    let out counting patient orderContext slots orderPlan session signing loads =
        [
            if counting then
                Request.Counting
            if PatientMachine.PatientState.changing patient then
                Request.Patient
            if (OrderContextMachine.OrderContextState.inFlightRequest orderContext).IsSome then
                Request.Workbench
            if
                slots
                |> List.exists (fun (_, slot) -> (OrderContextMachine.OrderContextState.inFlightRequest slot).IsSome)
            then
                Request.Slots
            if (OrderPlanMachine.OrderPlanState.inFlightRequest orderPlan).IsSome then
                Request.Plan
            if planBehind patient orderPlan then
                Request.PlanBehind
            if
                SessionMachine.SessionState.changing session
                || (SessionMachine.SessionState.reopening session).IsSome
            then
                Request.Session
            if signing |> SigningMachine.SigningState.view |> SigningPolicy.underWay then
                Request.Signature
            yield! loads |> List.map Request.Load
        ]
