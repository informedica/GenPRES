/// Tracks the prescribing workbench: an order context not yet in the plan, from no patient to an
/// evaluated context, through changes and resets. The App carries out the effects. The machine
/// has two stages: the workbench itself, which knows no request, and the one request under way.
///
/// Four invariants:
/// - one request is in flight at a time; a command sent meanwhile is dropped, unless it is the
///   dialog's, which waits;
/// - an answer lands only on the request it names;
/// - the workbench is always evaluated for the patient held, and again when the patient changes;
/// - a filter needs a patient; one that arrives without a patient is dropped.
///
/// An answer is the context evaluated, the context refused with the reason, or a failure, after
/// which the workbench stays as it was.
module OrderContextMachine

open Shared.Types
open Shared.Models
open Shared.Api


/// The workbench itself, without any request under way.
[<RequireQualifiedAccess>]
type OrderContextWorkbench =
    /// No patient, so no workbench.
    | NoPatient
    /// The patient and the context last evaluated for it, which a failed change goes back to: the
    /// empty context during the first evaluation, and the context as sent after a refusal.
    | Evaluated of Patient * OrderContext


/// What moves the workbench stage.
[<RequireQualifiedAccess>]
type OrderContextWorkbenchMsg =
    /// The patient set, changed or cleared.
    | PatientChanged of Patient option
    /// A filter from the url or the menu.
    | Seed of OrderContext
    /// A command from the page.
    | Command of OrderContextCommand * OrderContext
    /// The answer that landed, with what was sent.
    | Landed of sent: (OrderContextCommand * OrderContext) * Result<OrderContextResponse, string[]>
    /// Clear the workbench.
    | Reset


/// What the workbench stage asks of the request stage, which turns it into calls and effects.
[<RequireQualifiedAccess>]
type OrderContextWorkbenchIntent =
    /// Open an empty workbench for the patient; replaces any request. The formulary and
    /// parenteralia pages load for the patient themselves.
    | Open of Patient
    /// Evaluate the context and put its filter on the formulary and parenteralia pages; replaces
    /// any request.
    | Evaluate of OrderContext
    /// Send a command over the context; one at a time.
    | Call of OrderContextCommand * OrderContext
    /// Put the filter on the formulary and parenteralia pages.
    | Sync of Filter
    /// Tell the user what went wrong.
    | Tell of string[]


/// The workbench stage.
module OrderContextWorkbench =

    /// An empty context for the patient.
    let emptyFor (pat: Patient) = OrderContext.empty |> OrderContext.setPatient pat


    /// After a failed change: the context as it was, with the formulary and parenteralia pages put
    /// back on its filter.
    let private restore (pat: Patient) (ctx: OrderContext) (errs: string[]) =
        OrderContextWorkbench.Evaluated(pat, ctx),
        [
            OrderContextWorkbenchIntent.Tell errs
            OrderContextWorkbenchIntent.Sync ctx.Filter
        ]


    /// After a refusal: the context as sent, with its picks kept and its scenarios dropped. The
    /// page shows the reason itself.
    let private refused (pat: Patient) (sent: OrderContext) =
        OrderContextWorkbench.Evaluated(pat, { sent with Scenarios = [||] }), []


    /// The workbench stage: the workbench changes only on the patient and on an answer that landed;
    /// everything else becomes an intent for the request stage.
    let step
        (msg: OrderContextWorkbenchMsg)
        (workbench: OrderContextWorkbench)
        : OrderContextWorkbench * OrderContextWorkbenchIntent list
        =
        match msg, workbench with
        // no patient, no workbench
        | OrderContextWorkbenchMsg.PatientChanged None, _ -> OrderContextWorkbench.NoPatient, []

        // the first patient: the empty workbench is shown while it is evaluated
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.NoPatient ->
            OrderContextWorkbench.Evaluated(pat, emptyFor pat), [ OrderContextWorkbenchIntent.Open pat ]
        // the patient changed: the workbench keeps its filter and is evaluated again
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.Evaluated(_, ctx) ->
            let ctx = { ctx with Patient = pat }
            OrderContextWorkbench.Evaluated(pat, ctx), [ OrderContextWorkbenchIntent.Evaluate ctx ]

        // a filter needs a patient, so without one it is dropped; with one it is evaluated
        | OrderContextWorkbenchMsg.Seed _, OrderContextWorkbench.NoPatient -> workbench, []
        | OrderContextWorkbenchMsg.Seed ctx, OrderContextWorkbench.Evaluated(pat, _) ->
            workbench, [ OrderContextWorkbenchIntent.Evaluate { ctx with Patient = pat } ]

        // a command, always for the patient held; a reset clears the argumentation when it is
        // sent, so a text written while it runs is kept
        | OrderContextWorkbenchMsg.Command(cmd, ctx), OrderContextWorkbench.Evaluated(pat, held) ->
            let sent = { ctx with Patient = pat }

            if ArgumentationPolicy.clearedBy cmd then
                OrderContextWorkbench.Evaluated(pat, ArgumentationPolicy.clear held),
                [ OrderContextWorkbenchIntent.Call(cmd, ArgumentationPolicy.clear sent) ]
            else
                workbench, [ OrderContextWorkbenchIntent.Call(cmd, sent) ]
        // nothing to command without a patient
        | OrderContextWorkbenchMsg.Command _, OrderContextWorkbench.NoPatient -> workbench, []

        // nothing is asked without a patient, so nothing lands
        | OrderContextWorkbenchMsg.Landed _, OrderContextWorkbench.NoPatient -> workbench, []
        // an answer lands with the argumentation as sent: the server never changes the text
        | OrderContextWorkbenchMsg.Landed((_, sent), Ok(OrderContextResponse.Evaluated ctx)),
          OrderContextWorkbench.Evaluated(pat, _) ->
            OrderContextWorkbench.Evaluated(pat, ctx |> ArgumentationPolicy.keep sent), []
        | OrderContextWorkbenchMsg.Landed((_, sent), Ok(OrderContextResponse.Refused(back, _))),
          OrderContextWorkbench.Evaluated(pat, _) -> refused pat (back |> ArgumentationPolicy.keep sent)
        // a failed change leaves the workbench as it was, not as sent; after a failed first
        // evaluation, that is the empty workbench
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Evaluated(pat, held) ->
            restore pat held errs

        // a reset evaluates an empty workbench for the patient; nothing to reset without one
        | OrderContextWorkbenchMsg.Reset, OrderContextWorkbench.Evaluated(pat, _) ->
            OrderContextWorkbench.Evaluated(pat, emptyFor pat), [ OrderContextWorkbenchIntent.Evaluate(emptyFor pat) ]
        | OrderContextWorkbenchMsg.Reset, _ -> workbench, []


/// Everything the order context machine holds, hidden from the page, which reads an
/// OrderContextView of it instead.
type OrderContextState =
    private
        {
            /// The workbench itself.
            Workbench: OrderContextWorkbench
            /// The command and context sent, which the page shows meanwhile, and the request id its
            /// answer must name.
            InFlight: ((OrderContextCommand * OrderContext) * string) option
            /// The dialog command waiting on the answer, with the context it was sent with and its
            /// own request id.
            Pending: (OrderContextCommand * OrderContext * string) option
            /// The scenario the order dialog shows, by its order's id; None while the dialog is
            /// closed.
            Selected: string option
            /// Why the last answer refused the context; None after any other answer, a patient
            /// change, a seed or a reset.
            Refusal: OrderContextRefusal option
            /// The state before a reopen from the dialog, put back when its list closes without a
            /// pick; None when no reopen is looked at.
            Kept: OrderContextState option
        }


/// What moves the order context machine. A message that starts a request carries its request id,
/// so the answer can name it.
[<RequireQualifiedAccess>]
type OrderContextMsg =
    /// The patient set, changed or cleared; the workbench is evaluated for it.
    | PatientChanged of Patient option * request: string
    /// A filter from the url or the menu, evaluated for the patient; dropped without one.
    | Seed of OrderContext * request: string
    /// A command from the page, over the context the page holds.
    | Command of OrderContextCommand * OrderContext * request: string
    /// The answer to the request with this id: the context evaluated or refused; Error is a
    /// failure of the server or the call.
    | Answered of request: string * Result<OrderContextResponse, string[]>
    /// Clear the workbench and evaluate it empty, after an order was prescribed.
    | Reset of request: string
    /// The scenario the dialog shows, by its order's id.
    | Select of string option
    /// The argumentation written on the workbench. No request; the answer under way keeps it.
    | Argue of string
    /// A clear from the dialog that opens the field's list: the command goes out, and the state
    /// before it is kept to be put back.
    | Reopen of OrderContextCommand * OrderContext * request: string
    /// The list of a reopen closed without a pick: the context kept is put back, and the answer to
    /// the clear is dropped.
    | Restore


/// What the App carries out for the order context machine.
[<RequireQualifiedAccess>]
type OrderContextEffect =
    /// Send the order context command under this request id.
    | CallContext of OrderContextCommand * OrderContext * request: string
    /// Put the filter on the formulary page.
    | SyncFormulary of Filter
    /// Put the filter on the parenteralia page.
    | SyncParenteralia of Filter
    /// Tell the user what went wrong.
    | TellError of string[]


/// What the page reads of the OrderContextState. The page renders Settled, Refused and Changing
/// alike, so the screen stays filled during a request and the picks stay after a refusal. The
/// dialog steps from any of them; other commands are built from Settled and Refused only.
[<RequireQualifiedAccess>]
type OrderContextView =
    /// No patient, so no workbench.
    | NoPatient
    /// The context the server evaluated.
    | Settled of OrderContext
    /// The context the server refused, as sent, with the reason.
    | Refused of OrderContext * OrderContextRefusal
    /// A request under way, with the context sent; the empty context during the first
    /// evaluation.
    | Changing of OrderContext


/// Functions over OrderContextView.
module OrderContextView =

    /// The context the order dialog shows for the plan page: the selected context, settled or
    /// changing as the plan is; None without a selection the plan still holds.
    let dialog (plan: OrderPlanMachine.OrderPlanView) : OrderContextView option =
        let pick (tp: OrderPlan) (id: string) = tp.OrderContexts |> Array.tryFind (fun c -> c.Id = id)

        match plan with
        | OrderPlanMachine.OrderPlanView.Settled(tp, Some id) -> pick tp id |> Option.map OrderContextView.Settled
        | OrderPlanMachine.OrderPlanView.Changing(tp, Some id) -> pick tp id |> Option.map OrderContextView.Changing
        | OrderPlanMachine.OrderPlanView.Settled(_, None)
        | OrderPlanMachine.OrderPlanView.Changing(_, None)
        | OrderPlanMachine.OrderPlanView.NoPatient -> None


/// The constructors and the transition of the order context machine.
module OrderContextState =

    /// No patient, nothing under way.
    let noPatient =
        {
            Workbench = OrderContextWorkbench.NoPatient
            InFlight = None
            Pending = None
            Selected = None
            Refusal = None
            Kept = None
        }


    /// An empty context for the patient.
    let emptyFor = OrderContextWorkbench.emptyFor


    /// The first evaluation for the patient under way, with the empty context shown.
    let opening (pat: Patient) (request: string) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, emptyFor pat)
            InFlight = Some((OrderContextCommand.UpdateOrderContext, emptyFor pat), request)
            Pending = None
            Selected = None
            Refusal = None
            Kept = None
        }


    /// The context evaluated for the patient, nothing under way.
    let held (pat: Patient) (ctx: OrderContext) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, ctx)
            InFlight = None
            Pending = None
            Selected = None
            Refusal = None
            Kept = None
        }


    /// The context refused for the patient, as sent, with the reason.
    let refused (pat: Patient) (ctx: OrderContext) (refusal: OrderContextRefusal) =
        { held pat ctx with Refusal = Some refusal }


    /// A command under way over the context sent; a failure goes back to the context held.
    let changing (pat: Patient) (cmd: OrderContextCommand) (sent: OrderContext) (held: OrderContext) (request: string) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, held)
            InFlight = Some((cmd, { sent with Patient = pat }), request)
            Pending = None
            Selected = None
            Refusal = None
            Kept = None
        }


    /// The state with a dialog command waiting on the answer, with its context and request id.
    let pending (cmd: OrderContextCommand) (ctx: OrderContext) (request: string) (state: OrderContextState) =
        { state with Pending = Some(cmd, ctx, request) }


    /// The patient the workbench is evaluated for, if any.
    let patient (state: OrderContextState) =
        match state.Workbench with
        | OrderContextWorkbench.NoPatient -> None
        | OrderContextWorkbench.Evaluated(pat, _) -> Some pat


    /// The context shown: the one sent while a request is under way, otherwise the one held.
    let context (state: OrderContextState) =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient, _ -> None
        | OrderContextWorkbench.Evaluated _, Some((_, sent), _) -> Some sent
        | OrderContextWorkbench.Evaluated(_, ctx), None -> Some ctx


    /// The state with the function applied to the context held, sent and waiting alike.
    let map (f: OrderContext -> OrderContext) (state: OrderContextState) =
        let workbench =
            match state.Workbench with
            | OrderContextWorkbench.NoPatient -> state.Workbench
            | OrderContextWorkbench.Evaluated(pat, ctx) -> OrderContextWorkbench.Evaluated(pat, f ctx)

        let inFlight =
            state.InFlight
            |> Option.map (fun ((cmd, sent), request) -> (cmd, f sent), request)

        let pending = state.Pending |> Option.map (fun (cmd, ctx, request) -> cmd, f ctx, request)

        { state with
            Workbench = workbench
            InFlight = inFlight
            Pending = pending
        }


    /// What the page reads.
    let view (state: OrderContextState) : OrderContextView =
        match state.Workbench, state.InFlight, state.Refusal with
        | OrderContextWorkbench.NoPatient, _, _ -> OrderContextView.NoPatient
        | OrderContextWorkbench.Evaluated _, Some((_, sent), _), _ -> OrderContextView.Changing sent
        | OrderContextWorkbench.Evaluated(_, ctx), None, Some refusal -> OrderContextView.Refused(ctx, refusal)
        | OrderContextWorkbench.Evaluated(_, ctx), None, None -> OrderContextView.Settled ctx


    /// Whether the context has a scenario with the order with this id.
    let holds (orderId: string) (ctx: OrderContext) = ctx.Scenarios |> Array.exists (fun sc -> sc.Order.Id = orderId)


    /// The state with the dialog's selection, kept only when the context shown holds the scenario;
    /// None closes the dialog.
    let select (id: string option) (state: OrderContextState) =
        match context state with
        | None -> state
        | Some ctx -> { state with Selected = id |> Option.filter (fun id -> ctx |> holds id) }


    /// What the order dialog reads: the view while a scenario is selected, None while it is
    /// closed.
    let dialog (state: OrderContextState) : OrderContextView option = state.Selected |> Option.map (fun _ -> view state)


    /// What was sent, when the answer names the request under way; None otherwise, so an answer
    /// lands only on its own request.
    let landing (request: string) (inFlight: ((OrderContextCommand * OrderContext) * string) option) =
        match inFlight with
        | Some(sent, underWay) when underWay = request -> Some sent
        | _ -> None


    /// The request stage: each intent becomes a request under the given id or an effect. A call
    /// while a request is under way is dropped, except a dialog command, which waits; an
    /// evaluation replaces both.
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
                    | OrderContextWorkbenchIntent.Tell errs -> state, [ OrderContextEffect.TellError errs ]

                state, effects @ added
            )
            (state, [])


    /// Runs the workbench stage, then the request stage. The dialog closes on a patient change, a
    /// seed or a reset, and after an answer keeps its selection only if the workbench still holds
    /// it. The refusal is set by a refusing answer and cleared by any other answer, a patient
    /// change, a seed or a reset. A cleared patient drops the request.
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

        let refusal =
            match msg, workbench with
            | _, OrderContextWorkbench.NoPatient -> None
            | OrderContextWorkbenchMsg.Landed(_, Ok(OrderContextResponse.Refused(_, refusal))), _ -> Some refusal
            | OrderContextWorkbenchMsg.Landed _, _
            | OrderContextWorkbenchMsg.PatientChanged _, _
            | OrderContextWorkbenchMsg.Seed _, _
            | OrderContextWorkbenchMsg.Reset, _ -> None
            | OrderContextWorkbenchMsg.Command _, _ -> state.Refusal

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
                Refusal = refusal
            }


    /// The next state and effects for a message, a reopen and a restore aside.
    let private move (msg: OrderContextMsg) (state: OrderContextState) : OrderContextState * OrderContextEffect list =
        match msg, state.Workbench, state.InFlight with
        // only an answer to the request under way reaches the workbench
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

                // the waiting command goes out: a step over the context answered, a typed value over
                // the context it was sent with; a refusal or a failure drops it
                match result, state.Pending, landed.Workbench with
                | Ok(OrderContextResponse.Evaluated _),
                  Some(cmd, ctx, next),
                  OrderContextWorkbench.Evaluated(_, answered) ->
                    let over =
                        if OrderPlanMachine.Dialog.carries cmd then
                            ctx
                        else
                            answered
                    let state, more = run next (OrderContextWorkbenchMsg.Command(cmd, over)) landed
                    state, effects @ more
                | _ -> landed, effects

        // the selection needs no request
        | OrderContextMsg.Select id, _, _ -> select id state, []

        // the argumentation needs no request: it is written on the context held, sent and waiting
        // alike
        | OrderContextMsg.Argue text, _, _ -> map (ArgumentationPolicy.write text) state, []

        // a patient change during a request: the context sent is evaluated for the new patient;
        // the dialog closes and the refusal is cleared
        | OrderContextMsg.PatientChanged(Some pat, request), OrderContextWorkbench.Evaluated _, Some((_, sent), _) ->
            let workbench, _ =
                OrderContextWorkbench.step (OrderContextWorkbenchMsg.PatientChanged(Some pat)) state.Workbench

            apply
                request
                [ OrderContextWorkbenchIntent.Evaluate { sent with Patient = pat } ]
                { state with
                    Workbench = workbench
                    Selected = None
                    Refusal = None
                }

        | OrderContextMsg.PatientChanged(pat, request), _, _ ->
            run request (OrderContextWorkbenchMsg.PatientChanged pat) state
        | OrderContextMsg.Seed(ctx, request), _, _ -> run request (OrderContextWorkbenchMsg.Seed ctx) state
        | OrderContextMsg.Command(cmd, ctx, request), _, _ ->
            run request (OrderContextWorkbenchMsg.Command(cmd, ctx)) state
        | OrderContextMsg.Reset request, _, _ -> run request OrderContextWorkbenchMsg.Reset state
        // taken by transition
        | OrderContextMsg.Reopen _, _, _
        | OrderContextMsg.Restore, _, _ -> state, []


    /// The next state and effects for a message. A reopen keeps the state before it, which an
    /// answer carries along and a restore puts back; any other message ends the look, and the
    /// state kept goes. The state kept has nothing under way, since a reopen is not offered while
    /// a request is, so the answer to the clear finds no request to land on after a restore.
    let transition (msg: OrderContextMsg) (state: OrderContextState) : OrderContextState * OrderContextEffect list =
        match msg with
        | OrderContextMsg.Reopen(cmd, ctx, request) ->
            let kept = { state with Kept = None }
            let moved, effects = move (OrderContextMsg.Command(cmd, ctx, request)) kept
            { moved with Kept = Some kept }, effects
        | OrderContextMsg.Restore ->
            match state.Kept with
            | Some kept -> kept, []
            | None -> state, []
        | OrderContextMsg.Answered _ -> move msg state
        | _ -> move msg { state with Kept = None }
