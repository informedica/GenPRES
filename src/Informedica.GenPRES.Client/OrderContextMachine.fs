/// The prescribing workbench as the client holds it: an order context not yet in the plan, from
/// no patient to the empty context opened for one, shown, changed and cleared, as a pure state machine next to the
/// order plan's, with effects for the App to interpret. The machine is two stages: the workbench
/// as the clinical model has it, which knows no request, and the one request under way, which
/// knows no context beyond the one it carries. `transition` runs them in order: an answer passes
/// the request first and reaches the workbench only when it lands; a command passes the
/// workbench first and reaches the request as an intent, dropped while one is under way. The
/// interpreter completes each call from the open Session, keeps the formulary and parenteralia
/// filters in step, and puts words on the snackbar.
///
/// Four invariants: one request is in flight at a time, and a command sent while one is under way
/// is dropped (the page greys its controls meanwhile) unless it is the dialog's, which waits as
/// the one pending, the latest replacing an earlier one, and goes out when the answer lands; an
/// answer names the request it answers
/// and lands only on that request; the workbench is always evaluated for the patient held, a
/// patient change re-evaluating it; and a filter needs a patient, since the patient is part of
/// it: one that arrives without (from the url) is dropped, and the App says so.
module OrderContextMachine

open Shared.Types
open Shared.Models
open Shared.Api


/// The workbench as the clinical model has it: no request ids here.
[<RequireQualifiedAccess>]
type OrderContextWorkbench =
    | NoPatient
    // the patient held and the context last evaluated for it, the original a failed change goes
    // back to; the empty context while the first evaluation is under way
    | Evaluated of Patient * OrderContext


/// What moves the workbench; the answer that landed brings what was sent.
[<RequireQualifiedAccess>]
type OrderContextWorkbenchMsg =
    | PatientChanged of Patient option
    | Seed of OrderContext
    | Command of OrderContextCommand * OrderContext
    | Landed of sent: (OrderContextCommand * OrderContext) * Result<OrderContext, string[]>
    | Reset


/// What the workbench asks of the lane; the request stage turns these into calls and effects.
[<RequireQualifiedAccess>]
type OrderContextWorkbenchIntent =
    // the workbench opened empty for the patient: supersedes whatever is under way; the pages
    // load for the patient themselves, so no sync
    | Open of Patient
    // the context evaluated, the filter into the formulary and the parenteralia as well:
    // supersedes whatever is under way
    | Evaluate of OrderContext
    // a command over the context: one at a time
    | Call of OrderContextCommand * OrderContext
    // the formulary and the parenteralia onto the filter
    | Sync of Filter
    // the server found no dose rules for the filter: back to the first page
    | GoToLifeSupport
    | Tell of string[]


module OrderContextWorkbench =

    /// The workbench emptied for the patient.
    let emptyFor (pat: Patient) = OrderContext.empty |> OrderContext.setPatient pat


    /// What the server says when the filter matches no dose rule; the page then starts over.
    let noDoseRules (errs: string[]) =
        errs |> Array.exists (fun e -> e.ToLower().Contains "geen doseerregels")


    /// A failed change: the context given, the original, kept, and the formulary and the
    /// parenteralia back on its filter, since the evaluation had taken them along.
    let private restore (pat: Patient) (ctx: OrderContext) (errs: string[]) =
        OrderContextWorkbench.Evaluated(pat, ctx),
        [
            OrderContextWorkbenchIntent.Tell errs
            OrderContextWorkbenchIntent.Sync ctx.Filter
        ]


    /// The filter matched no dose rule: the page is left, the failure said, and the empty
    /// workbench evaluated again.
    let private startOver (pat: Patient) (errs: string[]) =
        let empty = emptyFor pat

        OrderContextWorkbench.Evaluated(pat, empty),
        [
            OrderContextWorkbenchIntent.GoToLifeSupport
            OrderContextWorkbenchIntent.Tell errs
            OrderContextWorkbenchIntent.Evaluate empty
        ]


    /// The domain stage: the workbench changes only on an answer that landed and on the patient;
    /// everything else is an intent for the request stage.
    let step
        (msg: OrderContextWorkbenchMsg)
        (workbench: OrderContextWorkbench)
        : OrderContextWorkbench * OrderContextWorkbenchIntent list
        =
        match msg, workbench with
        // no patient, no workbench
        | OrderContextWorkbenchMsg.PatientChanged None, _ -> OrderContextWorkbench.NoPatient, []

        // the first patient: the empty workbench held and opened, shown while the evaluation runs
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.NoPatient ->
            OrderContextWorkbench.Evaluated(pat, emptyFor pat), [ OrderContextWorkbenchIntent.Open pat ]
        // the patient changed: the workbench keeps its filter and is evaluated again for the new
        // patient
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.Evaluated(_, ctx) ->
            let ctx = { ctx with Patient = pat }
            OrderContextWorkbench.Evaluated(pat, ctx), [ OrderContextWorkbenchIntent.Evaluate ctx ]

        // a filter without a patient is dropped: the patient is part of it, so nothing waits
        // for one; with a patient it is evaluated at once
        | OrderContextWorkbenchMsg.Seed _, OrderContextWorkbench.NoPatient -> workbench, []
        | OrderContextWorkbenchMsg.Seed ctx, OrderContextWorkbench.Evaluated(pat, _) ->
            workbench, [ OrderContextWorkbenchIntent.Evaluate { ctx with Patient = pat } ]

        // a command over the workbench held, always for the patient held
        | OrderContextWorkbenchMsg.Command(cmd, ctx), OrderContextWorkbench.Evaluated(pat, _) ->
            workbench, [ OrderContextWorkbenchIntent.Call(cmd, { ctx with Patient = pat }) ]
        // nothing to command without a patient
        | OrderContextWorkbenchMsg.Command _, OrderContextWorkbench.NoPatient -> workbench, []

        // nothing was asked without a patient, so nothing lands there
        | OrderContextWorkbenchMsg.Landed _, OrderContextWorkbench.NoPatient -> workbench, []
        // an answer lands for the patient held
        | OrderContextWorkbenchMsg.Landed(_, Ok ctx), OrderContextWorkbench.Evaluated(pat, _) ->
            OrderContextWorkbench.Evaluated(pat, ctx), []
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Evaluated(pat, _) when noDoseRules errs ->
            startOver pat errs
        // a failed change leaves the workbench as the request found it, never the context sent,
        // whose order and texts the server did not confirm; for a failed first evaluation that
        // is the empty workbench
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Evaluated(pat, held) ->
            restore pat held errs

        // the workbench cleared for the patient held and evaluated empty; nothing to clear
        // without a patient
        | OrderContextWorkbenchMsg.Reset, OrderContextWorkbench.Evaluated(pat, _) ->
            OrderContextWorkbench.Evaluated(pat, emptyFor pat), [ OrderContextWorkbenchIntent.Evaluate(emptyFor pat) ]
        | OrderContextWorkbenchMsg.Reset, _ -> workbench, []


/// The workbench, the one request under way (the command and the context sent, what the page
/// shows meanwhile, and the id the answer must name; none while idle), the dialog's command
/// waiting on it (with the context it was sent with and its own id; none but while a request is
/// under way) and the dialog's selection. Built through the constructors below only, which admit
/// the combinations that can occur: no patient with nothing under way, a context held, a change
/// under way (the first evaluation over the empty context among them), with or without a command
/// pending, with or without a scenario selected.
type OrderContextState =
    private
        {
            Workbench: OrderContextWorkbench
            InFlight: ((OrderContextCommand * OrderContext) * string) option
            Pending: (OrderContextCommand * OrderContext * string) option
            // the scenario the order dialog shows, by its order's id: only one the context
            // shown holds; none while the dialog is closed
            Selected: string option
        }


/// What moves the workbench. Every message that starts a request carries the request id, minted
/// at dispatch, so that the answer can name it.
[<RequireQualifiedAccess>]
type OrderContextMsg =
    // the patient set, changed or cleared; the workbench is evaluated for it
    | PatientChanged of Patient option * request: string
    // a filter from the url or the menu, evaluated for the patient held; dropped without one
    | Seed of OrderContext * request: string
    // an order-context command from the page, over the context as the page holds it
    | Command of OrderContextCommand * OrderContext * request: string
    // the server's answer to the request named; Error = a failure, the server's or the call's
    | Answered of request: string * Result<OrderContext, string[]>
    // the workbench cleared and evaluated empty: after an order was prescribed
    | Reset of request: string
    // the dialog's selection, a scenario by its order's id; the client's own
    | Select of string option


/// What the machine asks the App to do.
[<RequireQualifiedAccess>]
type OrderContextEffect =
    | CallContext of OrderContextCommand * OrderContext * request: string
    // the filter chosen, into the formulary's and the parenteralia's
    | SyncFormulary of Filter
    | SyncParenteralia of Filter
    // the server found no dose rules for the filter: back to the first page
    | GoToLifeSupport
    | TellError of string[]


/// The workbench as the page shows it: the states a page can be in, each with what is valid in
/// it and nothing of the request. Nothing without a patient; the context the server answered,
/// nothing under way; a change under way, the context sent shown meanwhile, the empty context
/// while the first evaluation runs. A page renders from `Settled` and `Changing` alike,
/// so that the screen stays populated while a request runs; it steps from `Changing` too, over
/// the context shown, and builds every other command from `Settled` only.
[<RequireQualifiedAccess>]
type OrderContextView =
    | NoPatient
    | Settled of OrderContext
    | Changing of OrderContext


module OrderContextView =

    /// The context the order dialog shows, from the plan as the plan page shows it: the
    /// selected context, settled or changing as the plan is; none without a selection, or
    /// with one the plan no longer holds.
    let dialog (plan: OrderPlanMachine.OrderPlanView) : OrderContextView option =
        let pick (tp: OrderPlan) (id: string) = tp.OrderContexts |> Array.tryFind (fun c -> c.Id = id)

        match plan with
        | OrderPlanMachine.OrderPlanView.Settled(tp, Some id) -> pick tp id |> Option.map OrderContextView.Settled
        | OrderPlanMachine.OrderPlanView.Changing(tp, Some id) -> pick tp id |> Option.map OrderContextView.Changing
        | OrderPlanMachine.OrderPlanView.Settled(_, None)
        | OrderPlanMachine.OrderPlanView.Changing(_, None)
        | OrderPlanMachine.OrderPlanView.NoPatient -> None


module OrderContextState =

    let noPatient =
        {
            Workbench = OrderContextWorkbench.NoPatient
            InFlight = None
            Pending = None
            Selected = None
        }


    let emptyFor = OrderContextWorkbench.emptyFor


    /// The first evaluation for the patient under way, over the empty context, held and shown.
    let opening (pat: Patient) (request: string) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, emptyFor pat)
            InFlight = Some((OrderContextCommand.UpdateOrderContext, emptyFor pat), request)
            Pending = None
            Selected = None
        }


    /// The context held for the patient, nothing under way.
    let held (pat: Patient) (ctx: OrderContext) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, ctx)
            InFlight = None
            Pending = None
            Selected = None
        }


    /// A command under way over the context sent, always for the patient held; the context held
    /// is what a failed change goes back to.
    let changing (pat: Patient) (cmd: OrderContextCommand) (sent: OrderContext) (held: OrderContext) (request: string) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, held)
            InFlight = Some((cmd, { sent with Patient = pat }), request)
            Pending = None
            Selected = None
        }


    /// A change under way with the dialog's command waiting on its answer, with the context it
    /// was sent with, under its own request id; only on a change under way.
    let pending (cmd: OrderContextCommand) (ctx: OrderContext) (request: string) (state: OrderContextState) =
        { state with Pending = Some(cmd, ctx, request) }


    /// The patient the workbench is evaluated for, none without one.
    let patient (state: OrderContextState) =
        match state.Workbench with
        | OrderContextWorkbench.NoPatient -> None
        | OrderContextWorkbench.Evaluated(pat, _) -> Some pat


    /// The context the workbench shows: the one sent while a request is under way, the one held
    /// otherwise; none without a patient.
    let context (state: OrderContextState) =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient, _ -> None
        | OrderContextWorkbench.Evaluated _, Some((_, sent), _) -> Some sent
        | OrderContextWorkbench.Evaluated(_, ctx), None -> Some ctx


    /// The context held and the one sent, changed in place: the filter kept in step with the
    /// formulary's and the parenteralia's.
    let map (f: OrderContext -> OrderContext) (state: OrderContextState) =
        let workbench =
            match state.Workbench with
            | OrderContextWorkbench.NoPatient -> state.Workbench
            | OrderContextWorkbench.Evaluated(pat, ctx) -> OrderContextWorkbench.Evaluated(pat, f ctx)

        let inFlight =
            state.InFlight
            |> Option.map (fun ((cmd, sent), request) -> (cmd, f sent), request)

        let pending = state.Pending |> Option.map (fun (cmd, ctx, request) -> cmd, f ctx, request)

        {
            Workbench = workbench
            InFlight = inFlight
            Pending = pending
            Selected = state.Selected
        }


    /// The workbench as the page shows it: the context sent shown while a request is under way.
    let view (state: OrderContextState) : OrderContextView =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient, _ -> OrderContextView.NoPatient
        | OrderContextWorkbench.Evaluated _, Some((_, sent), _) -> OrderContextView.Changing sent
        | OrderContextWorkbench.Evaluated(_, ctx), None -> OrderContextView.Settled ctx


    /// Whether the context has a scenario with the order named.
    let holds (orderId: string) (ctx: OrderContext) = ctx.Scenarios |> Array.exists (fun sc -> sc.Order.Id = orderId)


    /// The dialog's selection: a scenario by its order's id, kept only when the context shown
    /// holds it, so that nothing is selected before the first evaluation answered; none closes
    /// the dialog. Nothing to select without a patient.
    let select (id: string option) (state: OrderContextState) =
        match context state with
        | None -> state
        | Some ctx -> { state with Selected = id |> Option.filter (fun id -> ctx |> holds id) }


    /// The workbench as the order dialog shows it: the context shown while a scenario is
    /// selected, settled or changing as the workbench is; none while the dialog is closed.
    let dialog (state: OrderContextState) : OrderContextView option = state.Selected |> Option.map (fun _ -> view state)


    /// The request stage's check: the payload sent when the answer names the request under way,
    /// none for any other answer, so that an answer lands only on its request.
    let landing (request: string) (inFlight: ((OrderContextCommand * OrderContext) * string) option) =
        match inFlight with
        | Some(sent, underWay) when underWay = request -> Some sent
        | _ -> None


    /// The request stage: the intents applied in order, each a request under the id given or an
    /// effect; a call while a request is under way is dropped, but the dialog's waits as the one
    /// pending; an evaluation supersedes both.
    let private apply (request: string) (intents: OrderContextWorkbenchIntent list) (state: OrderContextState) =
        let evaluate (ctx: OrderContext) (state: OrderContextState) =
            { state with
                InFlight = Some((OrderContextCommand.UpdateOrderContext, ctx), request)
                Pending = None
            },
            [
                OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, ctx, request)
                OrderContextEffect.SyncFormulary ctx.Filter
                OrderContextEffect.SyncParenteralia ctx.Filter
            ]

        intents
        |> List.fold
            (fun (state: OrderContextState, effects) intent ->
                let state, added =
                    match intent with
                    | OrderContextWorkbenchIntent.Open pat ->
                        let ctx = OrderContextWorkbench.emptyFor pat

                        { state with
                            InFlight = Some((OrderContextCommand.UpdateOrderContext, ctx), request)
                            Pending = None
                        },
                        [
                            OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, ctx, request)
                        ]
                    | OrderContextWorkbenchIntent.Evaluate ctx -> evaluate ctx state
                    | OrderContextWorkbenchIntent.Call(cmd, ctx) when state.InFlight.IsSome ->
                        (if OrderPlanMachine.Dialog.waits cmd then
                             { state with Pending = Some(cmd, ctx, request) }
                         else
                             state),
                        []
                    | OrderContextWorkbenchIntent.Call(OrderContextCommand.UpdateOrderContext, ctx) ->
                        evaluate ctx state
                    | OrderContextWorkbenchIntent.Call(cmd, ctx) ->
                        { state with InFlight = Some((cmd, ctx), request) },
                        [ OrderContextEffect.CallContext(cmd, ctx, request) ]
                    | OrderContextWorkbenchIntent.Sync filter ->
                        state,
                        [
                            OrderContextEffect.SyncFormulary filter
                            OrderContextEffect.SyncParenteralia filter
                        ]
                    | OrderContextWorkbenchIntent.GoToLifeSupport -> state, [ OrderContextEffect.GoToLifeSupport ]
                    | OrderContextWorkbenchIntent.Tell errs -> state, [ OrderContextEffect.TellError errs ]

                state, effects @ added
            )
            (state, [])


    /// The domain stage first, then the request stage: the workbench steps, and its intents
    /// become the request under way and the effects. The dialog follows: closed by a patient
    /// change, a seed and a reset, narrowed to what the workbench holds after an answer (a
    /// failed change keeps the context held, and the order with it; a start over holds none),
    /// kept otherwise; a patient cleared clears the request.
    let private run (request: string) (msg: OrderContextWorkbenchMsg) (state: OrderContextState) =
        let workbench, intents = OrderContextWorkbench.step msg state.Workbench

        let selected =
            match msg, workbench with
            | _, OrderContextWorkbench.NoPatient -> None
            | OrderContextWorkbenchMsg.PatientChanged _, _
            | OrderContextWorkbenchMsg.Seed _, _
            | OrderContextWorkbenchMsg.Reset, _ -> None
            | OrderContextWorkbenchMsg.Landed _, OrderContextWorkbench.Evaluated(_, ctx) ->
                state.Selected |> Option.filter (fun id -> ctx |> holds id)
            | OrderContextWorkbenchMsg.Command _, _ -> state.Selected

        let inFlight, pending =
            match workbench with
            | OrderContextWorkbench.NoPatient -> None, None
            | _ -> state.InFlight, state.Pending

        apply
            request
            intents
            { state with
                Workbench = workbench
                InFlight = inFlight
                Pending = pending
                Selected = selected
            }


    let transition (msg: OrderContextMsg) (state: OrderContextState) : OrderContextState * OrderContextEffect list =
        match msg, state.Workbench, state.InFlight with
        // the request stage first: only an answer to the request under way reaches the workbench
        | OrderContextMsg.Answered(request, result), _, _ ->
            match landing request state.InFlight with
            | None -> state, []
            | Some sent ->
                let landed, effects =
                    run
                        request
                        (OrderContextWorkbenchMsg.Landed(sent, result))
                        { state with
                            InFlight = None
                            Pending = None
                        }

                // the command that waited goes out: a step over the context answered, a value
                // typed over the context it was sent with; a failure drops it
                match result, state.Pending, landed.Workbench with
                | Ok _, Some(cmd, ctx, next), OrderContextWorkbench.Evaluated(_, answered) ->
                    let over =
                        if OrderPlanMachine.Dialog.carries cmd then
                            ctx
                        else
                            answered
                    let state, more = run next (OrderContextWorkbenchMsg.Command(cmd, over)) landed
                    state, effects @ more
                | _ -> landed, effects

        // the selection is the client's own, kept next to whatever is in flight
        | OrderContextMsg.Select id, _, _ -> select id state, []

        // the patient changed while a change is under way: the context sent is evaluated for the
        // new patient, the one held stays what a failed change goes back to, the dialog closes
        | OrderContextMsg.PatientChanged(Some pat, request), OrderContextWorkbench.Evaluated _, Some((_, sent), _) ->
            let workbench, _ =
                OrderContextWorkbench.step (OrderContextWorkbenchMsg.PatientChanged(Some pat)) state.Workbench

            apply
                request
                [ OrderContextWorkbenchIntent.Evaluate { sent with Patient = pat } ]
                { state with
                    Workbench = workbench
                    Selected = None
                }

        | OrderContextMsg.PatientChanged(pat, request), _, _ ->
            run request (OrderContextWorkbenchMsg.PatientChanged pat) state
        | OrderContextMsg.Seed(ctx, request), _, _ -> run request (OrderContextWorkbenchMsg.Seed ctx) state
        | OrderContextMsg.Command(cmd, ctx, request), _, _ ->
            run request (OrderContextWorkbenchMsg.Command(cmd, ctx)) state
        | OrderContextMsg.Reset request, _, _ -> run request OrderContextWorkbenchMsg.Reset state
