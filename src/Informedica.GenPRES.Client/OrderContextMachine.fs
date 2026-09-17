/// The prescribing workbench as the client holds it: an order context not yet in the plan, from
/// no patient to a context shown, changed and cleared, as a pure state machine next to the
/// order plan's, with effects for the App to interpret. The machine is two stages: the workbench
/// as the clinical model has it, which knows no request, and the one request under way, which
/// knows no context beyond the one it carries. `transition` runs them in order: an answer passes
/// the request first and reaches the workbench only when it lands; a command passes the
/// workbench first and reaches the request as an intent, dropped while one is under way. The
/// interpreter completes each call from the open Session, keeps the formulary and parenteralia
/// filters in step, and puts words on the snackbar.
///
/// Four invariants: one request is in flight at a time, a command sent while one is under way
/// is dropped (the page greys its controls meanwhile); an answer names the request it answers
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
    // a patient held, nothing evaluated yet
    | Unevaluated of Patient
    // the patient held and the context last evaluated for it, the original a failed change goes
    // back to
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
    let emptyFor (pat: Patient) =
        OrderContext.empty |> OrderContext.setPatient pat


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

        // the first patient: the empty workbench opened
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.NoPatient
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.Unevaluated _ ->
            OrderContextWorkbench.Unevaluated pat, [ OrderContextWorkbenchIntent.Open pat ]
        // the patient changed: the workbench keeps its filter and is evaluated again for the new
        // patient
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.Evaluated(_, ctx) ->
            let ctx = { ctx with Patient = pat }
            OrderContextWorkbench.Evaluated(pat, ctx), [ OrderContextWorkbenchIntent.Evaluate ctx ]

        // a filter without a patient is dropped: the patient is part of it, so nothing waits
        // for one; with a patient it is evaluated at once
        | OrderContextWorkbenchMsg.Seed _, OrderContextWorkbench.NoPatient -> workbench, []
        // nothing evaluated yet: the seed is what a failed evaluation goes back to
        | OrderContextWorkbenchMsg.Seed ctx, OrderContextWorkbench.Unevaluated pat ->
            let ctx = { ctx with Patient = pat }
            OrderContextWorkbench.Evaluated(pat, ctx), [ OrderContextWorkbenchIntent.Evaluate ctx ]
        | OrderContextWorkbenchMsg.Seed ctx, OrderContextWorkbench.Evaluated(pat, _) ->
            workbench,
            [
                OrderContextWorkbenchIntent.Evaluate { ctx with Patient = pat }
            ]

        // a command over the workbench held, always for the patient held
        | OrderContextWorkbenchMsg.Command(cmd, ctx), OrderContextWorkbench.Evaluated(pat, _) ->
            workbench,
            [
                OrderContextWorkbenchIntent.Call(cmd, { ctx with Patient = pat })
            ]
        // nothing to command without a patient or before the first evaluation
        | OrderContextWorkbenchMsg.Command _, OrderContextWorkbench.NoPatient
        | OrderContextWorkbenchMsg.Command _, OrderContextWorkbench.Unevaluated _ -> workbench, []

        // nothing was asked without a patient, so nothing lands there
        | OrderContextWorkbenchMsg.Landed _, OrderContextWorkbench.NoPatient -> workbench, []
        // an answer lands for the patient held
        | OrderContextWorkbenchMsg.Landed(_, Ok ctx), OrderContextWorkbench.Unevaluated pat
        | OrderContextWorkbenchMsg.Landed(_, Ok ctx), OrderContextWorkbench.Evaluated(pat, _) ->
            OrderContextWorkbench.Evaluated(pat, ctx), []
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Unevaluated pat when noDoseRules errs ->
            startOver pat errs
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Evaluated(pat, _) when noDoseRules errs ->
            startOver pat errs
        // a failed first evaluation lands on the empty workbench; a failed change leaves the
        // workbench as the request found it, never the context sent, whose order and texts the
        // server did not confirm
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Unevaluated pat ->
            restore pat (emptyFor pat) errs
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Evaluated(pat, held) ->
            restore pat held errs

        // the workbench cleared for the patient held and evaluated empty; nothing to clear
        // without a patient
        | OrderContextWorkbenchMsg.Reset, OrderContextWorkbench.Unevaluated pat
        | OrderContextWorkbenchMsg.Reset, OrderContextWorkbench.Evaluated(pat, _) ->
            OrderContextWorkbench.Evaluated(pat, emptyFor pat), [ OrderContextWorkbenchIntent.Evaluate(emptyFor pat) ]
        | OrderContextWorkbenchMsg.Reset, _ -> workbench, []


/// The workbench and the one request under way: the command and the context sent (what the page
/// shows meanwhile) and the id the answer must name; none while idle. Built through the
/// constructors below only, which admit the combinations that can occur: no patient with
/// nothing under way, a first evaluation under way, a context held, a change under way.
type OrderContextState =
    private
        {
            Workbench: OrderContextWorkbench
            InFlight: ((OrderContextCommand * OrderContext) * string) option
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
/// it and nothing of the request. Nothing without a patient; the first evaluation under way
/// with nothing to show; the context the server answered, nothing under way; a change under
/// way, the context sent shown meanwhile. A page renders from `Settled` and `Changing` alike,
/// so that the screen stays populated while a request runs, and builds a command from
/// `Settled` only, since a command sent while a request is under way is dropped.
[<RequireQualifiedAccess>]
type OrderContextView =
    | NoPatient
    | Evaluating
    | Settled of OrderContext
    | Changing of OrderContext


module OrderContextView =

    /// The context the order dialog shows, from the plan as the plan page shows it: the
    /// selected context, settled or changing as the plan is; none without a selection, or
    /// with one the plan no longer holds.
    let dialog (plan: OrderPlanMachine.OrderPlanView) : OrderContextView option =
        let pick (tp: OrderPlan) (id: string) =
            tp.OrderContexts |> Array.tryFind (fun c -> c.Id = id)

        match plan with
        | OrderPlanMachine.OrderPlanView.Settled(tp, Some id) -> pick tp id |> Option.map OrderContextView.Settled
        | OrderPlanMachine.OrderPlanView.Changing(tp, Some id) -> pick tp id |> Option.map OrderContextView.Changing
        | OrderPlanMachine.OrderPlanView.Settled(_, None)
        | OrderPlanMachine.OrderPlanView.Changing(_, None)
        | OrderPlanMachine.OrderPlanView.NoPatient
        | OrderPlanMachine.OrderPlanView.Opening -> None


module OrderContextState =

    let noPatient =
        {
            Workbench = OrderContextWorkbench.NoPatient
            InFlight = None
        }


    let emptyFor = OrderContextWorkbench.emptyFor


    /// The first evaluation for the patient under way: nothing held yet.
    let opening (pat: Patient) (request: string) =
        {
            Workbench = OrderContextWorkbench.Unevaluated pat
            InFlight = Some((OrderContextCommand.UpdateOrderContext, emptyFor pat), request)
        }


    /// The context held for the patient, nothing under way.
    let held (pat: Patient) (ctx: OrderContext) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, ctx)
            InFlight = None
        }


    /// A command under way over the context sent, always for the patient held; the context held
    /// is what a failed change goes back to.
    let changing (pat: Patient) (cmd: OrderContextCommand) (sent: OrderContext) (held: OrderContext) (request: string) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, held)
            InFlight = Some((cmd, { sent with Patient = pat }), request)
        }


    /// The patient the workbench is evaluated for, none without one.
    let patient (state: OrderContextState) =
        match state.Workbench with
        | OrderContextWorkbench.NoPatient -> None
        | OrderContextWorkbench.Unevaluated pat
        | OrderContextWorkbench.Evaluated(pat, _) -> Some pat


    /// The context the workbench shows: the one sent while a request is under way, the one held
    /// otherwise; none before the first evaluation.
    let context (state: OrderContextState) =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient, _
        | OrderContextWorkbench.Unevaluated _, _ -> None
        | OrderContextWorkbench.Evaluated _, Some((_, sent), _) -> Some sent
        | OrderContextWorkbench.Evaluated(_, ctx), None -> Some ctx


    /// The context held and the one sent, changed in place: the filter kept in step with the
    /// formulary's and the parenteralia's.
    let map (f: OrderContext -> OrderContext) (state: OrderContextState) =
        let workbench =
            match state.Workbench with
            | OrderContextWorkbench.NoPatient
            | OrderContextWorkbench.Unevaluated _ -> state.Workbench
            | OrderContextWorkbench.Evaluated(pat, ctx) -> OrderContextWorkbench.Evaluated(pat, f ctx)

        let inFlight =
            state.InFlight
            |> Option.map (fun ((cmd, sent), request) -> (cmd, f sent), request)

        {
            Workbench = workbench
            InFlight = inFlight
        }


    /// The workbench as the pages read it: the context sent shows while a request is under way.
    let toDeferred (state: OrderContextState) : Deferred<OrderContext> =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient, _ -> HasNotStartedYet
        | OrderContextWorkbench.Unevaluated _, _ -> InProgress
        | OrderContextWorkbench.Evaluated _, Some((_, sent), _) -> Provisional sent
        | OrderContextWorkbench.Evaluated(_, ctx), None -> Resolved ctx


    /// The workbench as the page shows it: the context sent shown while a request is under way.
    /// A seed, a filter held before a patient is set, shows as settled for now; the seed state
    /// goes with #646 and this arm with it.
    let view (state: OrderContextState) : OrderContextView =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient, _ -> OrderContextView.NoPatient
        | OrderContextWorkbench.Seeded ctx, _ -> OrderContextView.Settled ctx
        | OrderContextWorkbench.Unevaluated _, _ -> OrderContextView.Evaluating
        | OrderContextWorkbench.Evaluated _, Some((_, sent), _) -> OrderContextView.Changing sent
        | OrderContextWorkbench.Evaluated ctx, None -> OrderContextView.Settled ctx


    /// The request stage's check: the payload sent when the answer names the request under way,
    /// none for any other answer, so that an answer lands only on its request.
    let landing (request: string) (inFlight: ((OrderContextCommand * OrderContext) * string) option) =
        match inFlight with
        | Some(sent, underWay) when underWay = request -> Some sent
        | _ -> None


    /// The request stage: the intents applied in order, each a request under the id given or an
    /// effect; a call while a request is under way is dropped.
    let private apply (request: string) (intents: OrderContextWorkbenchIntent list) (state: OrderContextState) =
        let evaluate (ctx: OrderContext) (state: OrderContextState) =
            { state with InFlight = Some((OrderContextCommand.UpdateOrderContext, ctx), request) },
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

                        { state with InFlight = Some((OrderContextCommand.UpdateOrderContext, ctx), request) },
                        [
                            OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, ctx, request)
                        ]
                    | OrderContextWorkbenchIntent.Evaluate ctx -> evaluate ctx state
                    | OrderContextWorkbenchIntent.Call _ when state.InFlight.IsSome -> state, []
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
    /// become the request under way and the effects. A patient cleared clears the request.
    let private run (request: string) (msg: OrderContextWorkbenchMsg) (state: OrderContextState) =
        let workbench, intents = OrderContextWorkbench.step msg state.Workbench

        let inFlight =
            match workbench with
            | OrderContextWorkbench.NoPatient -> None
            | _ -> state.InFlight

        apply
            request
            intents
            { state with
                Workbench = workbench
                InFlight = inFlight
            }


    let transition (msg: OrderContextMsg) (state: OrderContextState) : OrderContextState * OrderContextEffect list =
        match msg, state.Workbench, state.InFlight with
        // the request stage first: only an answer to the request under way reaches the workbench
        | OrderContextMsg.Answered(request, result), _, _ ->
            match landing request state.InFlight with
            | None -> state, []
            | Some sent -> run request (OrderContextWorkbenchMsg.Landed(sent, result)) { state with InFlight = None }

        // the patient changed while a change is under way: the context sent is evaluated for the
        // new patient, and the one held stays what a failed change goes back to
        | OrderContextMsg.PatientChanged(Some pat, request), OrderContextWorkbench.Evaluated _, Some((_, sent), _) ->
            let workbench, _ =
                OrderContextWorkbench.step (OrderContextWorkbenchMsg.PatientChanged(Some pat)) state.Workbench

            apply
                request
                [
                    OrderContextWorkbenchIntent.Evaluate { sent with Patient = pat }
                ]
                { state with Workbench = workbench }

        | OrderContextMsg.PatientChanged(pat, request), _, _ ->
            run request (OrderContextWorkbenchMsg.PatientChanged pat) state
        | OrderContextMsg.Seed(ctx, request), _, _ -> run request (OrderContextWorkbenchMsg.Seed ctx) state
        | OrderContextMsg.Command(cmd, ctx, request), _, _ ->
            run request (OrderContextWorkbenchMsg.Command(cmd, ctx)) state
        | OrderContextMsg.Reset request, _, _ -> run request OrderContextWorkbenchMsg.Reset state
