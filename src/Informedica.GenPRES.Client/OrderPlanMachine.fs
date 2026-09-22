/// The order plan as the client holds it, from no patient to the empty plan opened for one,
/// shown, changed and reopened: a pure state machine next to the Session's and the signing's,
/// with effects for the App to
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
    // the plan as answered, the original a failed change goes back to; the empty plan while an
    // open is under way, the contexts being opened travelling in the request
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


/// The order dialog's commands, the same in either lane: a step, a value typed, a reset. One
/// arriving while a request is under way is not dropped but waits as the one pending, the latest
/// replacing an earlier one, and goes out when the answer lands. The two commands that are the
/// page's, the filter evaluated and a scenario selected, are dropped while busy.
module Dialog =

    let waits (cmd: OrderContextCommand) =
        match cmd with
        | OrderContextCommand.UpdateOrderContext
        | OrderContextCommand.SelectOrderScenario -> false
        | _ -> true


    /// Whether the command carries the dialog's order in its context, a value typed or a reset,
    /// so that a pending one goes out over the context it was sent with; a step carries nothing
    /// and goes out over the context answered, so that it steps from there.
    let carries (cmd: OrderContextCommand) =
        match cmd with
        | OrderContextCommand.UpdateOrderScenario
        | OrderContextCommand.ResetOrderScenario -> true
        | _ -> false


module OrderPlanCart =

    /// A step into an order of the plan, from the dialog: the one plan command that waits while
    /// a request is under way instead of being dropped.
    let waits (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Navigate(_, _, ctxCmd, _) -> Dialog.waits ctxCmd
        | _ -> false


    /// The pending step over the plan answered: into the same context as it now is, or over the
    /// context it was sent with when it carries the dialog's order; none when the context is
    /// gone from the plan.
    let replay (tp: OrderPlan) (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Navigate(_, id, ctxCmd, ctx) ->
            tp.OrderContexts
            |> Array.tryFind (fun c -> c.Id = id)
            |> Option.map (fun now ->
                OrderPlanCommand.Navigate(tp, id, ctxCmd, (if Dialog.carries ctxCmd then ctx else now))
            )
        | _ -> None


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

        // the first plan for a patient: the empty plan held and shown while the version kept for
        // the patient, or the empty one, is opened
        | OrderPlanCartMsg.PatientChanged(Some pat), OrderPlanCart.NoPatient awaiting ->
            OrderPlanCart.Opened(pat, OrderPlan.create pat [||]), [ OrderPlanCartIntent.Open(pat, awaiting) ]
        // the plan follows the patient: its totals recomputed (while an open is under way the
        // composer opens the same contexts again for the new patient instead)
        | OrderPlanCartMsg.PatientChanged(Some pat), OrderPlanCart.Opened(_, tp) ->
            let tp = { tp with Patient = pat }
            OrderPlanCart.Opened(pat, tp), [ OrderPlanCartIntent.Recalculate tp ]

        // the signed version replaces whatever plan there was; before the patient it was
        // opened with has arrived, its contexts are kept and opened when the patient does
        | OrderPlanCartMsg.Version head, OrderPlanCart.NoPatient _ -> OrderPlanCart.NoPatient head.OrderContexts, []
        | OrderPlanCartMsg.Version head, OrderPlanCart.Opened(pat, _) ->
            OrderPlanCart.Opened(pat, OrderPlan.create pat [||]), [ OrderPlanCartIntent.Open(pat, head.OrderContexts) ]

        // a change from a page, over the plan held
        | OrderPlanCartMsg.Command cmd, OrderPlanCart.Opened(_, tp) -> plan, [ OrderPlanCartIntent.Call(rebase tp cmd) ]
        | OrderPlanCartMsg.Command _, _ -> plan, []

        // nothing was asked without a patient, so nothing lands there
        | OrderPlanCartMsg.Landed _, OrderPlanCart.NoPatient _ -> plan, []
        // an answer lands for the patient held
        | OrderPlanCartMsg.Landed(sent, Ok tp), OrderPlanCart.Opened(pat, _) -> answered pat sent tp
        // a failed change leaves the plan as the request found it; for a failed open that is
        // the empty plan for the patient
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
/// while idle), the dialog's step waiting on it (the command and its own id; none but while a
/// request is under way) and the context the dialog shows, by id: the client's own, next to
/// whatever is in flight. Built through the constructors below only, which admit the
/// combinations that can occur: no patient with nothing under way, a version awaiting its
/// patient, a plan held, a change under way (an open over the empty plan among them), with or
/// without a step pending.
type OrderPlanState =
    private
        {
            Cart: OrderPlanCart
            InFlight: (OrderPlanCommand * string) option
            Pending: (OrderPlanCommand * string) option
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
/// and nothing of the request. Nothing without a patient; the plan the server answered, nothing
/// under way, with the context the dialog shows by id; a change under way, the plan shown
/// meanwhile with that selection, the empty plan while an open runs. A selection without a
/// plan cannot be written. A page renders from `Settled` and `Changing` alike, so that the
/// screen stays populated while a request runs; it steps the dialog's order from `Changing` too,
/// over the plan shown, and builds every other command from `Settled` only.
[<RequireQualifiedAccess>]
type OrderPlanView =
    | NoPatient
    | Settled of OrderPlan * selected: string option
    /// The plan the command carries for a recalculation, so that the rows chosen show at once;
    /// the plan held for every other change.
    | Changing of OrderPlan * selected: string option


module OrderPlanView =

    /// Whether the plan holds the order, by the order's id, as one of its contexts' contribution:
    /// for the prescribe button, which is not offered for an order already in the plan. A
    /// context's own id is minted by the plan and is not the order's.
    let holds (orderId: string) (view: OrderPlanView) =
        match view with
        | OrderPlanView.Settled(tp, _)
        | OrderPlanView.Changing(tp, _) -> OrderPlan.orders tp |> Array.exists (fun sc -> sc.Order.Id = orderId)
        | OrderPlanView.NoPatient -> false


module OrderPlanState =

    let noPatient =
        {
            Cart = OrderPlanCart.NoPatient [||]
            InFlight = None
            Pending = None
            Selected = None
        }


    /// No patient yet: the contexts of the version the Session opened with, kept for the
    /// patient on its way, nothing under way, the dialog closed.
    let awaiting (contexts: OrderContext[]) =
        {
            Cart = OrderPlanCart.NoPatient contexts
            InFlight = None
            Pending = None
            Selected = None
        }


    /// An open under way over the empty plan held and shown: the contexts being opened travel in
    /// the request, the dialog closed.
    let opening (pat: Patient) (contexts: OrderContext[]) (request: string) =
        {
            Cart = OrderPlanCart.Opened(pat, OrderPlan.create pat [||])
            InFlight = Some(OrderPlanCommand.Open(pat, contexts), request)
            Pending = None
            Selected = None
        }


    /// The plan held for the patient with the dialog's selection, nothing under way.
    let held (pat: Patient) (tp: OrderPlan) (selected: string option) =
        {
            Cart = OrderPlanCart.Opened(pat, tp)
            InFlight = None
            Pending = None
            Selected = selected
        }


    /// A change under way over the plan held, the one a failed change goes back to.
    let changing (pat: Patient) (tp: OrderPlan) (selected: string option) (sent: OrderPlanCommand) (request: string) =
        {
            Cart = OrderPlanCart.Opened(pat, tp)
            InFlight = Some(sent, request)
            Pending = None
            Selected = selected
        }


    /// A change under way with the dialog's step waiting on its answer, under its own request
    /// id; only on a change under way.
    let pending (cmd: OrderPlanCommand) (request: string) (state: OrderPlanState) =
        { state with Pending = Some(cmd, request) }


    /// The plan the state holds, none without a patient; the empty plan while an open runs.
    let plan (state: OrderPlanState) =
        match state.Cart with
        | OrderPlanCart.NoPatient _ -> None
        | OrderPlanCart.Opened(_, tp) -> Some tp


    /// The patient the plan is for, none without one.
    let patient (state: OrderPlanState) =
        match state.Cart with
        | OrderPlanCart.NoPatient _ -> None
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


    /// The plan as the pages show it: the plan shown meanwhile while a change is under way, the
    /// dialog's selection in both cases that carry a plan.
    let view (state: OrderPlanState) : OrderPlanView =
        match state.Cart, state.InFlight with
        | OrderPlanCart.NoPatient _, _ -> OrderPlanView.NoPatient
        | OrderPlanCart.Opened(_, tp), Some(sent, _) -> OrderPlanView.Changing(meanwhile tp sent, state.Selected)
        | OrderPlanCart.Opened(_, tp), None -> OrderPlanView.Settled(tp, state.Selected)


    /// The request stage's check: the command sent when the answer names the request under way,
    /// none for any other answer, so that an answer lands only on its request.
    let landing (request: string) (inFlight: (OrderPlanCommand * string) option) =
        match inFlight with
        | Some(sent, underWay) when underWay = request -> Some sent
        | _ -> None


    /// The request stage: the intents applied in order, each a request under the id given or an
    /// effect; a call while a request is under way is dropped, but a step into an order waits as
    /// the one pending; an open or a recalculation supersedes both.
    let private apply (request: string) (intents: OrderPlanCartIntent list) (state: OrderPlanState) =
        let call (cmd: OrderPlanCommand) (state: OrderPlanState) =
            { state with
                InFlight = Some(cmd, request)
                Pending = None
            },
            [ OrderPlanEffect.CallPlan(cmd, request) ]

        intents
        |> List.fold
            (fun (state: OrderPlanState, effects) intent ->
                let state, added =
                    match intent with
                    | OrderPlanCartIntent.Open(pat, ctxs) -> call (OrderPlanCommand.Open(pat, ctxs)) state
                    | OrderPlanCartIntent.Recalculate tp -> call (OrderPlanCommand.Recalculate tp) state
                    | OrderPlanCartIntent.Call cmd when state.InFlight.IsSome ->
                        (if OrderPlanCart.waits cmd then
                             { state with Pending = Some(cmd, request) }
                         else
                             state),
                        []
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

        let inFlight, pending =
            match plan with
            | OrderPlanCart.NoPatient _ -> None, None
            | _ -> state.InFlight, state.Pending

        apply
            request
            intents
            { state with
                Cart = plan
                InFlight = inFlight
                Pending = pending
                Selected = selected
            }


    let transition (msg: OrderPlanMsg) (state: OrderPlanState) : OrderPlanState * OrderPlanEffect list =
        match msg with
        // the request stage first: only an answer to the request under way reaches the plan
        | OrderPlanMsg.Answered(request, result) ->
            match landing request state.InFlight with
            | None -> state, []
            | Some sent ->
                let landed, effects =
                    run
                        request
                        (OrderPlanCartMsg.Landed(sent, result))
                        { state with
                            InFlight = None
                            Pending = None
                        }

                // the step that waited goes out over the plan answered; a failure drops it
                match result, state.Pending, landed.Cart with
                | Ok _, Some(cmd, next), OrderPlanCart.Opened(_, tp) ->
                    match OrderPlanCart.replay tp cmd with
                    | Some cmd ->
                        let state, more = run next (OrderPlanCartMsg.Command cmd) landed
                        state, effects @ more
                    | None -> landed, effects
                | _ -> landed, effects

        // the selection is the client's own, kept next to whatever is in flight; nothing to
        // select before the plan is there, the empty one being opened included
        | OrderPlanMsg.Select id ->
            match state.Cart, state.InFlight with
            | OrderPlanCart.Opened _, Some(OrderPlanCommand.Open _, _) -> state, []
            | OrderPlanCart.Opened _, _ -> { state with Selected = id }, []
            | OrderPlanCart.NoPatient _, _ -> state, []

        // the patient changed while an open is under way: the same contexts opened again for
        // the new patient, over the empty plan held
        | OrderPlanMsg.PatientChanged(Some pat, request) ->
            match state.Cart, state.InFlight with
            | OrderPlanCart.Opened _, Some(OrderPlanCommand.Open(_, contexts), _) ->
                let plan, _ = OrderPlanCart.step (OrderPlanCartMsg.PatientChanged(Some pat)) state.Cart

                apply
                    request
                    [ OrderPlanCartIntent.Open(pat, contexts) ]
                    { state with
                        Cart = plan
                        Selected = None
                    }
            | _ -> run request (OrderPlanCartMsg.PatientChanged(Some pat)) state

        | OrderPlanMsg.PatientChanged(None, request) -> run request (OrderPlanCartMsg.PatientChanged None) state
        | OrderPlanMsg.Version(head, request) -> run request (OrderPlanCartMsg.Version head) state
        | OrderPlanMsg.Command(cmd, request) -> run request (OrderPlanCartMsg.Command cmd) state
        | OrderPlanMsg.Filter(ids, request) -> run request (OrderPlanCartMsg.Filter ids) state
