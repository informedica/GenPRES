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
open Shared.Models
open Shared.Api


/// The plan as the clinical model has it: no request ids here.
[<RequireQualifiedAccess>]
type OrderPlanCart =
    // no patient: the contexts of the version the Session opened with, kept until there is a
    // patient to open them for, since the Session tells its version before the patient it
    // opened with has reached the plan; empty when there is no such version
    | NoPatient of awaiting: OrderContext[]
    // a patient held, no plan answered yet: the contexts being opened, so that a patient change
    // meanwhile opens the same ones again and a signed version being opened is not lost
    | Unopened of Patient * opening: OrderContext[]
    // the plan as answered, the original a failed change goes back to
    | Opened of Patient * OrderPlan


/// What moves the plan; the answer that landed brings the command that was sent.
[<RequireQualifiedAccess>]
type OrderPlanCartMsg =
    | PatientChanged of Patient option
    | Version of SignedOrderPlan
    | Command of OrderPlanCommand
    | Landed of sent: OrderPlanCommand * Result<OrderPlan, string[]>
    | Filter of string[]


/// What the plan asks of the lane; the request stage turns these into calls and effects.
[<RequireQualifiedAccess>]
type OrderPlanCartIntent =
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


module OrderPlanCart =

    /// The drugs of the plan, checked for interactions: always, so that a plan down to one drug
    /// or none clears the warnings of the drugs it had.
    let interactions (tp: OrderPlan) =
        Shared.Models.OrderPlan.orders tp
        |> Array.map _.Name
        |> Array.distinct
        |> Array.toList
        |> OrderPlanCartIntent.CheckInteractions
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
    let private answered (pat: Patient) (sent: OrderPlanCommand) (tp: OrderPlan) =
        let prescribed =
            match sent with
            | OrderPlanCommand.AddOrderContext _ ->
                [ OrderPlanCartIntent.GoToPlanPage; OrderPlanCartIntent.ResetWorkbench ]
            | _ -> []

        OrderPlanCart.Opened(pat, tp), interactions tp @ prescribed


    /// The domain stage: the plan changes only on an answer that landed and on the patient;
    /// everything else is an intent for the request stage.
    let step (msg: OrderPlanCartMsg) (plan: OrderPlanCart) : OrderPlanCart * OrderPlanCartIntent list =
        match msg, plan with
        // no patient, no plan
        | OrderPlanCartMsg.PatientChanged None, _ -> OrderPlanCart.NoPatient [||], []

        // the first plan for a patient: the version kept for it, or the empty one, opened
        | OrderPlanCartMsg.PatientChanged(Some pat), OrderPlanCart.NoPatient awaiting ->
            OrderPlanCart.Unopened(pat, awaiting), [ OrderPlanCartIntent.Open(pat, awaiting) ]
        // the patient changed while an open is under way: the same contexts opened again for
        // the new patient
        | OrderPlanCartMsg.PatientChanged(Some pat), OrderPlanCart.Unopened(_, opening) ->
            OrderPlanCart.Unopened(pat, opening), [ OrderPlanCartIntent.Open(pat, opening) ]
        // the plan follows the patient: its totals recomputed
        | OrderPlanCartMsg.PatientChanged(Some pat), OrderPlanCart.Opened(_, tp) ->
            let tp = { tp with Patient = pat }
            OrderPlanCart.Opened(pat, tp), [ OrderPlanCartIntent.Recalculate tp ]

        // the signed version replaces whatever plan there was; before the patient it was
        // opened with has arrived, its contexts are kept and opened when the patient does
        | OrderPlanCartMsg.Version head, OrderPlanCart.NoPatient _ -> OrderPlanCart.NoPatient head.OrderContexts, []
        | OrderPlanCartMsg.Version head, OrderPlanCart.Unopened(pat, _) ->
            OrderPlanCart.Unopened(pat, head.OrderContexts), [ OrderPlanCartIntent.Open(pat, head.OrderContexts) ]
        | OrderPlanCartMsg.Version head, OrderPlanCart.Opened(pat, _) ->
            OrderPlanCart.Unopened(pat, head.OrderContexts), [ OrderPlanCartIntent.Open(pat, head.OrderContexts) ]

        // a change from a page, over the plan held
        | OrderPlanCartMsg.Command cmd, OrderPlanCart.Opened(_, tp) -> plan, [ OrderPlanCartIntent.Call(rebase tp cmd) ]
        | OrderPlanCartMsg.Command _, _ -> plan, []

        // nothing was asked without a patient, so nothing lands there
        | OrderPlanCartMsg.Landed _, OrderPlanCart.NoPatient _ -> plan, []
        // an answer lands for the patient held
        | OrderPlanCartMsg.Landed(sent, Ok tp), OrderPlanCart.Unopened(pat, _)
        | OrderPlanCartMsg.Landed(sent, Ok tp), OrderPlanCart.Opened(pat, _) -> answered pat sent tp
        // a failed open lands on the empty plan for the patient
        | OrderPlanCartMsg.Landed(_, Error errs), OrderPlanCart.Unopened(pat, _) ->
            OrderPlanCart.Opened(pat, OrderPlan.create pat [||]), [ OrderPlanCartIntent.Tell errs ]
        // a failed change leaves the plan as the request found it
        | OrderPlanCartMsg.Landed(_, Error errs), OrderPlanCart.Opened _ -> plan, [ OrderPlanCartIntent.Tell errs ]

        // the rows chosen: the totals recomputed over them; the plan held stays what a failed
        // change goes back to, the rows travel in the command
        | OrderPlanCartMsg.Filter ids, OrderPlanCart.Opened(_, tp) ->
            plan,
            [
                OrderPlanCartIntent.Call(OrderPlanCommand.Recalculate { tp with Filtered = ids })
            ]
        | OrderPlanCartMsg.Filter _, _ -> plan, []


/// The plan, the one request under way (the command sent and the id the answer must name; none
/// while idle) and the context the dialog shows, by id: the client's own, next to whatever is in
/// flight. Built through the constructors below only, which admit the five combinations that can
/// occur: no patient with nothing under way, a version awaiting its patient, an open under way,
/// a plan held, a change under way.
type OrderPlanState =
    private
        {
            Cart: OrderPlanCart
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
    | Version of SignedOrderPlan * request: string
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


/// The plan as the pages show it: the states a page can be in, each with what is valid in it
/// and nothing of the request. Nothing without a patient; an open under way with nothing to
/// show; the plan the server answered, nothing under way, with the context the dialog shows by
/// id; a change under way, the plan shown meanwhile with that selection. A selection without a
/// plan cannot be written. A page renders from `Settled` and `Changing` alike, so that the
/// screen stays populated while a request runs; it steps the dialog's order from `Changing` too,
/// over the plan shown, and builds every other command from `Settled` only.
[<RequireQualifiedAccess>]
type OrderPlanView =
    | NoPatient
    | Opening
    | Settled of OrderPlan * selected: string option
    /// The plan the command carries for a recalculation, so that the rows chosen show at once;
    /// the plan held for every other change.
    | Changing of OrderPlan * selected: string option


module OrderPlanView =

    /// Whether the plan holds the context, by id: for the prescribe button, which is not offered
    /// for an order already in the plan.
    let holds (id: string) (view: OrderPlanView) =
        match view with
        | OrderPlanView.Settled(tp, _)
        | OrderPlanView.Changing(tp, _) -> tp.OrderContexts |> Array.exists (fun c -> c.Id = id)
        | OrderPlanView.NoPatient
        | OrderPlanView.Opening -> false


module OrderPlanState =

    let noPatient =
        {
            Cart = OrderPlanCart.NoPatient [||]
            InFlight = None
            Selected = None
        }


    /// No patient yet: the contexts of the version the Session opened with, kept for the
    /// patient on its way, nothing under way, the dialog closed.
    let awaiting (contexts: OrderContext[]) =
        {
            Cart = OrderPlanCart.NoPatient contexts
            InFlight = None
            Selected = None
        }


    /// An open under way: the contexts being opened, nothing held yet, the dialog closed.
    let opening (pat: Patient) (contexts: OrderContext[]) (request: string) =
        {
            Cart = OrderPlanCart.Unopened(pat, contexts)
            InFlight = Some(OrderPlanCommand.Open(pat, contexts), request)
            Selected = None
        }


    /// The plan held for the patient with the dialog's selection, nothing under way.
    let held (pat: Patient) (tp: OrderPlan) (selected: string option) =
        {
            Cart = OrderPlanCart.Opened(pat, tp)
            InFlight = None
            Selected = selected
        }


    /// A change under way over the plan held, the one a failed change goes back to.
    let changing (pat: Patient) (tp: OrderPlan) (selected: string option) (sent: OrderPlanCommand) (request: string) =
        {
            Cart = OrderPlanCart.Opened(pat, tp)
            InFlight = Some(sent, request)
            Selected = selected
        }


    /// The plan the state holds, none before the first answer.
    let plan (state: OrderPlanState) =
        match state.Cart with
        | OrderPlanCart.NoPatient _
        | OrderPlanCart.Unopened _ -> None
        | OrderPlanCart.Opened(_, tp) -> Some tp


    /// The context the dialog shows, by id; none while it is closed or there is no plan.
    let selected (state: OrderPlanState) = state.Selected


    /// The patient the plan is for, none without one.
    let patient (state: OrderPlanState) =
        match state.Cart with
        | OrderPlanCart.NoPatient _ -> None
        | OrderPlanCart.Unopened(pat, _) -> Some pat
        | OrderPlanCart.Opened(pat, _) -> Some pat


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
        match state.Cart, state.InFlight with
        | OrderPlanCart.NoPatient _, _ -> HasNotStartedYet
        | OrderPlanCart.Unopened _, _ -> InProgress
        | OrderPlanCart.Opened(_, tp), Some(sent, _) -> Provisional(meanwhile tp sent)
        | OrderPlanCart.Opened(_, tp), None -> Resolved tp


    /// The plan as the pages show it: the plan shown meanwhile while a change is under way, the
    /// dialog's selection in both cases that carry a plan.
    let view (state: OrderPlanState) : OrderPlanView =
        match state.Cart, state.InFlight with
        | OrderPlanCart.NoPatient _, _ -> OrderPlanView.NoPatient
        | OrderPlanCart.Unopened _, _ -> OrderPlanView.Opening
        | OrderPlanCart.Opened(_, tp), Some(sent, _) -> OrderPlanView.Changing(meanwhile tp sent, state.Selected)
        | OrderPlanCart.Opened(_, tp), None -> OrderPlanView.Settled(tp, state.Selected)


    /// The request stage's check: the command sent when the answer names the request under way,
    /// none for any other answer, so that an answer lands only on its request.
    let landing (request: string) (inFlight: (OrderPlanCommand * string) option) =
        match inFlight with
        | Some(sent, underWay) when underWay = request -> Some sent
        | _ -> None


    /// The request stage: the intents applied in order, each a request under the id given or an
    /// effect; a call while a request is under way is dropped.
    let private apply (request: string) (intents: OrderPlanCartIntent list) (state: OrderPlanState) =
        let call (cmd: OrderPlanCommand) (state: OrderPlanState) =
            { state with InFlight = Some(cmd, request) }, [ OrderPlanEffect.CallPlan(cmd, request) ]

        intents
        |> List.fold
            (fun (state: OrderPlanState, effects) intent ->
                let state, added =
                    match intent with
                    | OrderPlanCartIntent.Open(pat, ctxs) -> call (OrderPlanCommand.Open(pat, ctxs)) state
                    | OrderPlanCartIntent.Recalculate tp -> call (OrderPlanCommand.Recalculate tp) state
                    | OrderPlanCartIntent.Call _ when state.InFlight.IsSome -> state, []
                    | OrderPlanCartIntent.Call cmd -> call cmd state
                    | OrderPlanCartIntent.CheckInteractions drugs -> state, [ OrderPlanEffect.CheckInteractions drugs ]
                    | OrderPlanCartIntent.GoToPlanPage -> state, [ OrderPlanEffect.GoToPlanPage ]
                    | OrderPlanCartIntent.ResetWorkbench -> state, [ OrderPlanEffect.ResetWorkbench ]
                    | OrderPlanCartIntent.Tell errs -> state, [ OrderPlanEffect.TellError errs ]

                state, effects @ added
            )
            (state, [])


    /// The domain stage first, then the request stage: the plan steps, its intents become the
    /// request under way and the effects. The dialog follows: closed by a patient change, a
    /// reopen and a filter change that goes out, narrowed to the plan answered, kept otherwise;
    /// a patient cleared clears the request.
    let private run (request: string) (msg: OrderPlanCartMsg) (state: OrderPlanState) =
        let plan, intents = OrderPlanCart.step msg state.Cart

        let selected =
            match msg, plan with
            | _, OrderPlanCart.NoPatient _ -> None
            | OrderPlanCartMsg.PatientChanged _, _
            | OrderPlanCartMsg.Version _, _ -> None
            | OrderPlanCartMsg.Filter _, OrderPlanCart.Opened _ when state.InFlight.IsNone -> None
            | OrderPlanCartMsg.Landed(_, Ok tp), _ -> selectionIn tp state.Selected
            | _ -> state.Selected

        let inFlight =
            match plan with
            | OrderPlanCart.NoPatient _ -> None
            | _ -> state.InFlight

        apply
            request
            intents
            { state with
                Cart = plan
                InFlight = inFlight
                Selected = selected
            }


    let transition (msg: OrderPlanMsg) (state: OrderPlanState) : OrderPlanState * OrderPlanEffect list =
        match msg with
        // the request stage first: only an answer to the request under way reaches the plan
        | OrderPlanMsg.Answered(request, result) ->
            match landing request state.InFlight with
            | None -> state, []
            | Some sent -> run request (OrderPlanCartMsg.Landed(sent, result)) { state with InFlight = None }

        // the selection is the client's own, kept next to whatever is in flight; nothing to
        // select before the plan is there
        | OrderPlanMsg.Select id ->
            match state.Cart with
            | OrderPlanCart.Opened _ -> { state with Selected = id }, []
            | _ -> state, []

        | OrderPlanMsg.PatientChanged(pat, request) -> run request (OrderPlanCartMsg.PatientChanged pat) state
        | OrderPlanMsg.Version(head, request) -> run request (OrderPlanCartMsg.Version head) state
        | OrderPlanMsg.Command(cmd, request) -> run request (OrderPlanCartMsg.Command cmd) state
        | OrderPlanMsg.Filter(ids, request) -> run request (OrderPlanCartMsg.Filter ids) state
