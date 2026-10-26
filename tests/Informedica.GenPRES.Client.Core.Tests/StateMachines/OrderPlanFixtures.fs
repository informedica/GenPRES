/// The order plan machine states and messages the machine tests share.
module Informedica.GenPRES.Client.Core.Tests.StateMachines.OrderPlanFixtures

open Informedica.GenPRES.Client.Core.Policies
open Informedica.GenPRES.Client.Core.StateMachines
open System
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api
open PlanWorkPolicy
open OrderPlanMachine
open Informedica.GenPRES.Client.Core.Tests.OrderFixtures


let noPatient = OrderPlanState.noPatient

let held = OrderPlanState.held patient

/// The patient machine with no change under way, and with one under way.
let noPatientChange = PatientMachine.PatientState.init (Some draft)

let patientChanging =
    noPatientChange
    |> PatientMachine.PatientState.transition (
        PatientMachine.PatientMsg.Changed(Some otherDraft, PatientDraftPolicy.Estimates.Kept, "p-1")
    )
    |> fst

/// A change under way over the plan held for the patient, the one a failed change goes back to.
let recalculatingFor pat (tp: OrderPlan) (selected: string option) request (sent: OrderPlanCommand) =
    OrderPlanState.changing pat tp selected sent request

let recalculating = recalculatingFor patient

let loading = OrderPlanState.opening

let shown = held one None

let transition = OrderPlanState.transition

/// The page's message for a wire command, the plan in it left out: the machine builds the
/// command over the plan it holds.
let pageMsg (cmd: OrderPlanCommand, request) =
    let change =
        match cmd with
        | OrderPlanCommand.AddOrderContext(_, ctx) -> OrderPlanChange.Add ctx
        | OrderPlanCommand.NewOrderContext(_, category) -> OrderPlanChange.NewNutrition category
        | OrderPlanCommand.RemoveOrderContexts(_, ids) -> OrderPlanChange.Remove ids
        | OrderPlanCommand.FilterRows(ids, _) -> OrderPlanChange.FilterRows ids
        | OrderPlanCommand.Navigate(_, id, ctxCmd, _) -> OrderPlanChange.OrderDialogCommand(id, ctxCmd)
        | OrderPlanCommand.UpdatePatient _
        | OrderPlanCommand.Open _ -> invalidArg (nameof cmd) "no page sends this command"

    OrderPlanMsg.Change(change, request)
