namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// What is out: the requests of the five lanes, a field counting its step clicks and the loads.
module OutPolicy =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.StateMachines
    open Loads


    /// The requests out: a field counting its step clicks, the five lanes and the loads.
    let out counting patient orderContext orderPlan session signing loads =
        [
            if counting then
                Request.Counting
            if PatientMachine.PatientState.changing patient then
                Request.Patient
            if (OrderContextMachine.OrderContextState.inFlightRequest orderContext).IsSome then
                Request.Workbench
            if (OrderPlanMachine.OrderPlanState.inFlightRequest orderPlan).IsSome then
                Request.Plan
            if
                SessionMachine.SessionState.changing session
                || (SessionMachine.SessionState.reopening session).IsSome
            then
                Request.Session
            if signing |> SigningMachine.SigningState.view |> SigningPolicy.underWay then
                Request.Signature
            yield! loads |> List.map Request.Load
        ]
