/// The order plan as the client holds it, from no patient to a plan shown, changed and reopened: a
/// pure state machine next to the Session's and the signing's, with effects for the App to
/// interpret. The machine is two stages: the plan as the clinical model has it, which knows no
/// request, and the one request under way, which knows no plan beyond the command it carries;
/// the dialog's selection, the client's own, sits beside them. `transition` runs the stages in
/// order: an answer passes the request first and reaches the plan only when it lands; a change
/// passes the plan first and reaches the request as an intent, dropped while one is under way.
/// The interpreter completes each call from the open Session and puts words on the snackbar.
///
/// Four invariants: one request is in flight at a time, a change sent while one is under way is
/// dropped (the pages grey their controls meanwhile); an answer names the request it answers and
/// lands only on that request, so an older answer never replaces a newer plan; a patient change
/// and a reopen start a new request that supersedes whatever was in flight; and a failed change
/// leaves the plan as the request found it, so the next action is the retry.
module OrderPlanMachine

open Shared.Types
open Shared.Api


/// The plan as the clinical model has it: no request ids here.
[<RequireQualifiedAccess>]
type Plan =
    | NoPatient
    // a patient held, no plan answered yet: the contexts being opened, so that a patient change
    // meanwhile opens the same ones again and a signed version being opened is not lost
    | Unopened of Patient * opening: OrderContext[]
    // the plan as answered, the original a failed change goes back to
    | Opened of OrderPlan


/// What moves the plan; the answer that landed brings the command that was sent.
[<RequireQualifiedAccess>]
type PlanMsg =
    | PatientChanged of Patient option
    | Cart of SignedOrderPlan
    | Command of OrderPlanCommand
    | Landed of sent: OrderPlanCommand * Result<OrderPlan, string[]>
    | Filter of string[]


/// What the plan asks of the lane; the request stage turns these into calls and effects.
[<RequireQualifiedAccess>]
type PlanIntent =
    // the plan opened for the patient, empty or a signed version: supersedes whatever is under way
    | Open of Patient * OrderContext[]
    // the plan recalculated over the patient changed: supersedes whatever is under way
    | Recalculate of OrderPlan
    // a change from a page: one at a time
    | Call of OrderPlanCommand
    // the drugs of the plan, checked for interactions; fewer than two clears the warnings
    | CheckInteractions of string list
    // an order prescribed: the plan page opens on it
    | GoToPlanPage
    // and the prescribing workbench is cleared
    | ResetWorkbench
    | Tell of string[]


module Plan =

    /// The drugs of the plan, checked for interactions: always, so that a plan down to one drug
    /// or none clears the warnings of the drugs it had.
    let interactions (tp: OrderPlan) =
        Shared.Models.OrderPlan.orders tp
        |> Array.map _.Name
        |> Array.distinct
        |> Array.toList
        |> PlanIntent.CheckInteractions
        |> List.singleton


    /// A command from a page, over the plan the machine holds: a page's copy may be a step
    /// behind; only a recalculation carries the plan as the page changed it (its filter).
    let rebase (tp: OrderPlan) (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Recalculate _
        | OrderPlanCommand.Open _ -> cmd
        | OrderPlanCommand.AddOrderContext(_, ctx) -> OrderPlanCommand.AddOrderContext(tp, ctx)
        | OrderPlanCommand.NewOrderContext(_, category) -> OrderPlanCommand.NewOrderContext(tp, category)
        | OrderPlanCommand.Navigate(_, contextId, ctxCmd, ctx) -> OrderPlanCommand.Navigate(tp, contextId, ctxCmd, ctx)
        | OrderPlanCommand.RemoveOrderContexts(_, ids) -> OrderPlanCommand.RemoveOrderContexts(tp, ids)


    /// The plan answered: its drugs checked; an order prescribed opens the plan page and clears
    /// the workbench.
    let private answered (sent: OrderPlanCommand) (tp: OrderPlan) =
        let prescribed =
            match sent with
            | OrderPlanCommand.AddOrderContext _ -> [ PlanIntent.GoToPlanPage; PlanIntent.ResetWorkbench ]
            | _ -> []

        Plan.Opened tp, interactions tp @ prescribed


    /// The domain stage: the plan changes only on an answer that landed and on the patient;
    /// everything else is an intent for the request stage.
    let step (msg: PlanMsg) (plan: Plan) : Plan * PlanIntent list =
        match msg, plan with
        // no patient, no plan
        | PlanMsg.PatientChanged None, _ -> Plan.NoPatient, []

        // the first plan for a patient: the empty one, opened
        | PlanMsg.PatientChanged(Some pat), Plan.NoPatient -> Plan.Unopened(pat, [||]), [ PlanIntent.Open(pat, [||]) ]
        // the patient changed while an open is under way: the same contexts opened again for
        // the new patient
        | PlanMsg.PatientChanged(Some pat), Plan.Unopened(_, opening) ->
            Plan.Unopened(pat, opening), [ PlanIntent.Open(pat, opening) ]
        // the plan follows the patient: its totals recomputed
        | PlanMsg.PatientChanged(Some pat), Plan.Opened tp ->
            let tp = { tp with Patient = pat }
            Plan.Opened tp, [ PlanIntent.Recalculate tp ]

        // the signed version replaces whatever plan there was; without a patient there is
        // nothing to open it for
        | PlanMsg.Cart _, Plan.NoPatient -> plan, []
        | PlanMsg.Cart head, Plan.Unopened(pat, _) ->
            Plan.Unopened(pat, head.OrderContexts), [ PlanIntent.Open(pat, head.OrderContexts) ]
        | PlanMsg.Cart head, Plan.Opened tp ->
            Plan.Unopened(tp.Patient, head.OrderContexts), [ PlanIntent.Open(tp.Patient, head.OrderContexts) ]

        // a change from a page, over the plan held
        | PlanMsg.Command cmd, Plan.Opened tp -> plan, [ PlanIntent.Call(rebase tp cmd) ]
        | PlanMsg.Command _, _ -> plan, []

        // nothing was asked without a patient, so nothing lands there
        | PlanMsg.Landed _, Plan.NoPatient -> plan, []
        | PlanMsg.Landed(sent, Ok tp), _ -> answered sent tp
        // a failed open lands on the empty plan for the patient
        | PlanMsg.Landed(_, Error errs), Plan.Unopened(pat, _) ->
            Plan.Opened(Shared.Models.OrderPlan.create pat [||]), [ PlanIntent.Tell errs ]
        // a failed change leaves the plan as the request found it
        | PlanMsg.Landed(_, Error errs), Plan.Opened _ -> plan, [ PlanIntent.Tell errs ]

        // the rows chosen: the totals recomputed over them; the plan held stays what a failed
        // change goes back to, the rows travel in the command
        | PlanMsg.Filter ids, Plan.Opened tp ->
            plan,
            [
                PlanIntent.Call(OrderPlanCommand.Recalculate { tp with Filtered = ids })
            ]
        | PlanMsg.Filter _, _ -> plan, []


/// The plan, the one request under way (the command sent and the id the answer must name; none
/// while idle) and the context the dialog shows, by id: the client's own, next to whatever is in
/// flight. Built through the constructors below only, which admit the four combinations that can
/// occur: no patient with nothing under way, an open under way, a plan held, a change under way.
type OrderPlanState =
    private
        {
            Plan: Plan
            InFlight: (OrderPlanCommand * string) option
            Selected: string option
        }


/// What moves the plan. Every message that starts a request carries the request id, minted at
/// dispatch, so that the answer can name it.
[<RequireQualifiedAccess>]
type OrderPlanMsg =
    // the patient set, changed or cleared; the plan follows
    | PatientChanged of Patient option * request: string
    // the version the Session opened with: the plan becomes it, whatever was in flight
    | Cart of SignedOrderPlan * request: string
    // a change to the plan from a page, over the plan the page saw
    | Command of OrderPlanCommand * request: string
    // the server's answer to the request named; Error = a failure, the server's or the call's
    | Answered of request: string * Result<OrderPlan, string[]>
    // the dialog's selection, a context by id; the client's own
    | Select of string option
    // the contexts the rows keep, by id; the totals follow
    | Filter of string[] * request: string


/// What the machine asks the App to do.
[<RequireQualifiedAccess>]
type OrderPlanEffect =
    | CallPlan of OrderPlanCommand * request: string
    // the drugs of the plan, checked for interactions; fewer than two clears the warnings
    | CheckInteractions of string list
    // an order prescribed: the plan page opens on it
    | GoToPlanPage
    // and the prescribing workbench is cleared
    | ResetWorkbench
    | TellError of string[]


module OrderPlanState =

    let noPatient =
        {
            Plan = Plan.NoPatient
            InFlight = None
            Selected = None
        }


    /// An open under way: the contexts being opened, nothing held yet, the dialog closed.
    let opening (pat: Patient) (contexts: OrderContext[]) (request: string) =
        {
            Plan = Plan.Unopened(pat, contexts)
            InFlight = Some(OrderPlanCommand.Open(pat, contexts), request)
            Selected = None
        }


    /// The plan held with the dialog's selection, nothing under way.
    let held (tp: OrderPlan) (selected: string option) =
        {
            Plan = Plan.Opened tp
            InFlight = None
            Selected = selected
        }


    /// A change under way over the plan held, the one a failed change goes back to.
    let changing (tp: OrderPlan) (selected: string option) (sent: OrderPlanCommand) (request: string) =
        {
            Plan = Plan.Opened tp
            InFlight = Some(sent, request)
            Selected = selected
        }


    /// The plan the state holds, none before the first answer.
    let plan (state: OrderPlanState) =
        match state.Plan with
        | Plan.NoPatient
        | Plan.Unopened _ -> None
        | Plan.Opened tp -> Some tp


    /// The context the dialog shows, by id; none while it is closed or there is no plan.
    let selected (state: OrderPlanState) = state.Selected


    /// The patient the plan is for, none without one.
    let patient (state: OrderPlanState) =
        match state.Plan with
        | Plan.NoPatient -> None
        | Plan.Unopened(pat, _) -> Some pat
        | Plan.Opened tp -> Some tp.Patient


    /// The dialog's selection, kept only while its context is in the plan.
    let selectionIn (tp: OrderPlan) (selected: string option) =
        selected
        |> Option.filter (fun id -> tp.OrderContexts |> Array.exists (fun c -> c.Id = id))


    /// The plan the pages show while a change is under way: for a recalculation the one the
    /// command carries, since the rows chosen show at once; the plan held otherwise.
    let meanwhile (tp: OrderPlan) (sent: OrderPlanCommand) =
        match sent with
        | OrderPlanCommand.Recalculate shown -> shown
        | _ -> tp


    /// The plan as the pages read it: while a change is under way, the plan as the page changed
    /// it.
    let toDeferred (state: OrderPlanState) : Deferred<OrderPlan> =
        match state.Plan, state.InFlight with
        | Plan.NoPatient, _ -> HasNotStartedYet
        | Plan.Unopened _, _ -> InProgress
        | Plan.Opened tp, Some(sent, _) -> Provisional(meanwhile tp sent)
        | Plan.Opened tp, None -> Resolved tp


    /// The request stage's check: the command sent when the answer names the request under way,
    /// none for any other answer, so that an answer lands only on its request.
    let landing (request: string) (inFlight: (OrderPlanCommand * string) option) =
        match inFlight with
        | Some(sent, underWay) when underWay = request -> Some sent
        | _ -> None


    /// The request stage: the intents applied in order, each a request under the id given or an
    /// effect; a call while a request is under way is dropped.
    let private apply (request: string) (intents: PlanIntent list) (state: OrderPlanState) =
        let call (cmd: OrderPlanCommand) (state: OrderPlanState) =
            { state with InFlight = Some(cmd, request) }, [ OrderPlanEffect.CallPlan(cmd, request) ]

        intents
        |> List.fold
            (fun (state: OrderPlanState, effects) intent ->
                let state, added =
                    match intent with
                    | PlanIntent.Open(pat, ctxs) -> call (OrderPlanCommand.Open(pat, ctxs)) state
                    | PlanIntent.Recalculate tp -> call (OrderPlanCommand.Recalculate tp) state
                    | PlanIntent.Call _ when state.InFlight.IsSome -> state, []
                    | PlanIntent.Call cmd -> call cmd state
                    | PlanIntent.CheckInteractions drugs -> state, [ OrderPlanEffect.CheckInteractions drugs ]
                    | PlanIntent.GoToPlanPage -> state, [ OrderPlanEffect.GoToPlanPage ]
                    | PlanIntent.ResetWorkbench -> state, [ OrderPlanEffect.ResetWorkbench ]
                    | PlanIntent.Tell errs -> state, [ OrderPlanEffect.TellError errs ]

                state, effects @ added
            )
            (state, [])


    /// The domain stage first, then the request stage: the plan steps, its intents become the
    /// request under way and the effects. The dialog follows: closed by a patient change, a
    /// reopen and a filter change that goes out, narrowed to the plan answered, kept otherwise;
    /// a patient cleared clears the request.
    let private run (request: string) (msg: PlanMsg) (state: OrderPlanState) =
        let plan, intents = Plan.step msg state.Plan

        let selected =
            match msg, plan with
            | _, Plan.NoPatient -> None
            | PlanMsg.PatientChanged _, _
            | PlanMsg.Cart _, _ -> None
            | PlanMsg.Filter _, Plan.Opened _ when state.InFlight.IsNone -> None
            | PlanMsg.Landed(_, Ok tp), _ -> selectionIn tp state.Selected
            | _ -> state.Selected

        let inFlight =
            match plan with
            | Plan.NoPatient -> None
            | _ -> state.InFlight

        apply
            request
            intents
            { state with
                Plan = plan
                InFlight = inFlight
                Selected = selected
            }


    let transition (msg: OrderPlanMsg) (state: OrderPlanState) : OrderPlanState * OrderPlanEffect list =
        match msg with
        // the request stage first: only an answer to the request under way reaches the plan
        | OrderPlanMsg.Answered(request, result) ->
            match landing request state.InFlight with
            | None -> state, []
            | Some sent -> run request (PlanMsg.Landed(sent, result)) { state with InFlight = None }

        // the selection is the client's own, kept next to whatever is in flight; nothing to
        // select before the plan is there
        | OrderPlanMsg.Select id ->
            match state.Plan with
            | Plan.Opened _ -> { state with Selected = id }, []
            | _ -> state, []

        | OrderPlanMsg.PatientChanged(pat, request) -> run request (PlanMsg.PatientChanged pat) state
        | OrderPlanMsg.Cart(head, request) -> run request (PlanMsg.Cart head) state
        | OrderPlanMsg.Command(cmd, request) -> run request (PlanMsg.Command cmd) state
        | OrderPlanMsg.Filter(ids, request) -> run request (PlanMsg.Filter ids) state
