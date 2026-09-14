/// The order plan as the client holds it, from no patient to a plan shown, changed and reopened: a
/// pure state machine next to the Session's and the signing's, with effects for the App to
/// interpret. The machine holds the plan, the dialog's selection and the one request in flight;
/// the interpreter completes each call from the open Session and puts words on the snackbar.
///
/// Four invariants: one request is in flight at a time, a change sent while one is under way is
/// dropped (the pages grey their controls meanwhile); an answer names the request it answers and
/// lands only on that request, so an older answer never replaces a newer plan; a patient change
/// and a reopen start a new request that supersedes whatever was in flight; and a refused change
/// leaves the plan as the request found it, so the next action is the retry.
module OrderPlanMachine

open Shared.Types
open Shared.Api


/// The plan, one phase at a time.
[<RequireQualifiedAccess>]
type OrderPlanState =
    | NoPatient
    // the first plan for the patient asked under a request id
    | Loading of Patient * request: string
    // the plan as answered, and the context the dialog shows, by id
    | Shown of OrderPlan * selected: string option
    // a change in flight: the plan the request found, the selection, the request id and the
    // command sent, so that the answer knows what it answers
    | Recalculating of OrderPlan * selected: string option * request: string * sent: PlanCommand


/// What moves the plan. Every message that starts a request carries the request id, minted at
/// dispatch, so that the answer can name it.
[<RequireQualifiedAccess>]
type OrderPlanMsg =
    // the patient set, changed or cleared; the plan follows
    | PatientChanged of Patient option * request: string
    // the version the Session opened with: the plan becomes it, whatever was in flight
    | Cart of SignedOrderPlan * request: string
    // a change to the plan from a page, over the plan the page saw
    | Command of PlanCommand * request: string
    // the server's answer to the request named; Error = a refusal or a transport failure
    | Answered of request: string * Result<OrderPlan, string[]>
    // the dialog's selection, a context by id; the client's own
    | Select of string option
    // the contexts the rows keep, by id; the totals follow
    | Filter of string[] * request: string


/// What the machine asks the App to do.
[<RequireQualifiedAccess>]
type OrderPlanEffect =
    | CallPlan of PlanCommand * request: string
    // the drugs of the plan, checked for interactions; fewer than two clears the warnings
    | CheckInteractions of string list
    // an order prescribed: the plan page opens on it
    | GoToPlanPage
    // and the prescribing workbench is cleared
    | ResetWorkbench
    | TellError of string[]


module OrderPlanState =

    /// The plan the state holds, none before the first answer.
    let plan (state: OrderPlanState) =
        match state with
        | OrderPlanState.NoPatient
        | OrderPlanState.Loading _ -> None
        | OrderPlanState.Shown(tp, _)
        | OrderPlanState.Recalculating(tp, _, _, _) -> Some tp


    /// The patient the plan is for, none without one.
    let patient (state: OrderPlanState) =
        match state with
        | OrderPlanState.NoPatient -> None
        | OrderPlanState.Loading(pat, _) -> Some pat
        | OrderPlanState.Shown(tp, _)
        | OrderPlanState.Recalculating(tp, _, _, _) -> Some tp.Patient


    /// The dialog's selection, kept only while its context is in the plan.
    let selectionIn (tp: OrderPlan) (selected: string option) =
        selected
        |> Option.filter (fun id -> tp.OrderContexts |> Array.exists (fun c -> c.Id = id))


    /// The drugs of the plan, checked for interactions: always, so that a plan down to one drug
    /// or none clears the warnings of the drugs it had.
    let interactions (tp: OrderPlan) =
        Shared.Models.OrderPlan.orders tp
        |> Array.map _.Name
        |> Array.distinct
        |> Array.toList
        |> OrderPlanEffect.CheckInteractions
        |> List.singleton


    /// A command from a page, over the plan the machine holds: a page's copy may be a step
    /// behind; only a recalculation carries the plan as the page changed it (its filter).
    let rebase (tp: OrderPlan) (cmd: PlanCommand) =
        match cmd with
        | PlanCommand.Recalculate _
        | PlanCommand.Open _ -> cmd
        | PlanCommand.AddOrderContext(_, ctx) -> PlanCommand.AddOrderContext(tp, ctx)
        | PlanCommand.NewOrderContext(_, category) -> PlanCommand.NewOrderContext(tp, category)
        | PlanCommand.Navigate(_, contextId, ctxCmd, ctx) -> PlanCommand.Navigate(tp, contextId, ctxCmd, ctx)
        | PlanCommand.RemoveOrderContexts(_, ids) -> PlanCommand.RemoveOrderContexts(tp, ids)


    /// The plan answered: shown with the selection that still holds, its drugs checked; an
    /// order prescribed opens the plan page and clears the workbench.
    let private answered (sent: PlanCommand option) (selected: string option) (tp: OrderPlan) =
        let prescribed =
            match sent with
            | Some(PlanCommand.AddOrderContext _) ->
                [
                    OrderPlanEffect.GoToPlanPage
                    OrderPlanEffect.ResetWorkbench
                ]
            | _ -> []

        OrderPlanState.Shown(tp, selectionIn tp selected), interactions tp @ prescribed


    let transition (msg: OrderPlanMsg) (state: OrderPlanState) : OrderPlanState * OrderPlanEffect list =
        match msg, state with
        // no patient, no plan; whatever was in flight answers to nothing
        | OrderPlanMsg.PatientChanged(None, _), _ -> OrderPlanState.NoPatient, []

        // the first plan for a patient: the empty one, opened
        | OrderPlanMsg.PatientChanged(Some pat, request), OrderPlanState.NoPatient
        | OrderPlanMsg.PatientChanged(Some pat, request), OrderPlanState.Loading _ ->
            OrderPlanState.Loading(pat, request),
            [
                OrderPlanEffect.CallPlan(PlanCommand.Open(pat, [||]), request)
            ]

        // the plan follows the patient: its totals recomputed, the dialog closed, whatever was
        // in flight superseded
        | OrderPlanMsg.PatientChanged(Some pat, request), OrderPlanState.Shown(tp, _)
        | OrderPlanMsg.PatientChanged(Some pat, request), OrderPlanState.Recalculating(tp, _, _, _) ->
            let tp = { tp with Patient = pat }
            let cmd = PlanCommand.Recalculate tp
            OrderPlanState.Recalculating(tp, None, request, cmd), [ OrderPlanEffect.CallPlan(cmd, request) ]

        // the signed version replaces whatever plan there was, an open in flight included: the
        // newest open wins; without a patient there is nothing to open it for
        | OrderPlanMsg.Cart _, OrderPlanState.NoPatient -> state, []
        | OrderPlanMsg.Cart(head, request), _ ->
            let pat = patient state |> Option.get

            OrderPlanState.Loading(pat, request),
            [
                OrderPlanEffect.CallPlan(PlanCommand.Open(pat, head.OrderContexts), request)
            ]

        // a change over the plan shown: one at a time
        | OrderPlanMsg.Command(cmd, request), OrderPlanState.Shown(tp, selected) ->
            let cmd = rebase tp cmd

            let tp =
                match cmd with
                | PlanCommand.Recalculate sent -> sent
                | _ -> tp

            OrderPlanState.Recalculating(tp, selected, request, cmd), [ OrderPlanEffect.CallPlan(cmd, request) ]
        | OrderPlanMsg.Command _, _ -> state, []

        // the answer lands only on the request it answers
        | OrderPlanMsg.Answered(request, result), OrderPlanState.Loading(pat, inFlight) when request = inFlight ->
            match result with
            | Ok tp -> answered None None tp
            // a refused open lands on the empty plan for the patient
            | Error errs ->
                OrderPlanState.Shown(Shared.Models.OrderPlan.create pat [||], None), [ OrderPlanEffect.TellError errs ]
        | OrderPlanMsg.Answered(request, result), OrderPlanState.Recalculating(tp, selected, inFlight, sent) when
            request = inFlight
            ->
            match result with
            | Ok answer -> answered (Some sent) selected answer
            // a refused change leaves the plan as the request found it
            | Error errs -> OrderPlanState.Shown(tp, selected), [ OrderPlanEffect.TellError errs ]
        | OrderPlanMsg.Answered _, _ -> state, []

        // the selection is the client's own, kept next to whatever is in flight
        | OrderPlanMsg.Select id, OrderPlanState.Shown(tp, _) -> OrderPlanState.Shown(tp, id), []
        | OrderPlanMsg.Select id, OrderPlanState.Recalculating(tp, _, request, sent) ->
            OrderPlanState.Recalculating(tp, id, request, sent), []
        | OrderPlanMsg.Select _, _ -> state, []

        // the rows chosen: the totals recomputed over them, the dialog closed
        | OrderPlanMsg.Filter(ids, request), OrderPlanState.Shown(tp, _) ->
            let tp = { tp with Filtered = ids }
            let cmd = PlanCommand.Recalculate tp
            OrderPlanState.Recalculating(tp, None, request, cmd), [ OrderPlanEffect.CallPlan(cmd, request) ]
        | OrderPlanMsg.Filter _, _ -> state, []
