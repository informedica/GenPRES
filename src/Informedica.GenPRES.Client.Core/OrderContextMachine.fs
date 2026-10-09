/// Tracks the prescribing workbench: an order context not yet in the plan, from no patient to an
/// evaluated context, through changes and resets. The App carries out the effects. The machine
/// has two stages: the workbench itself, which knows no request, and the one request under way.
///
/// Four invariants:
/// - one request is in flight at a time; a command sent meanwhile is dropped;
/// - an answer lands only on the request it names;
/// - the workbench is always evaluated for the patient held, and again when the patient changes;
/// - a filter needs a patient; one that arrives before the patient waits for it.
///
/// An answer is the context evaluated, the context refused with the reason, or a failure, after
/// which the workbench stays as it was.
module OrderContextMachine

open Shared.Types
open Shared.Models
open Shared.Api


/// A filter's choices set from outside its own fields, by name, with where they come from: the
/// server applies them with the rule of their source.
type FilterSeed =
    {
        /// Where the choices come from: the url, a medication list, a page, or a reload.
        Source: SeedSource
        Indication: string option
        Generic: string option
        Route: string option
        Form: string option
        DoseType: DoseType option
    }


/// Functions over FilterSeed.
module FilterSeed =

    /// The order context command that sends the seed.
    let command (seed: FilterSeed) =
        OrderViewCommand.SeedFilter(seed.Source, seed.Indication, seed.Generic, seed.Route, seed.Form, seed.DoseType)


/// The workbench itself, without any request under way.
[<RequireQualifiedAccess>]
type OrderContextWorkbench =
    /// No patient, so no workbench; a seed that arrived before the patient waits for it.
    | NoPatient of awaiting: FilterSeed option
    /// The patient and the context last evaluated for it, which a failed change goes back to: the
    /// empty context during the first evaluation, and the context as sent after a refusal.
    | Evaluated of Patient * OrderContext


/// What moves the workbench stage.
[<RequireQualifiedAccess>]
type OrderContextWorkbenchMsg =
    /// The patient set, changed or cleared.
    | PatientChanged of Patient option
    /// A seed of the filter, sent as a command.
    | SeedFilter of FilterSeed
    /// A command from the page.
    | Command of OrderViewCommand
    /// The answer that landed, with what was sent.
    | Landed of sent: OrderContextCommand * Result<OrderContextResponse, string[]>
    /// Clear the workbench.
    | Reset


/// What the workbench stage asks of the request stage, which turns it into calls and effects.
[<RequireQualifiedAccess>]
type OrderContextWorkbenchIntent =
    /// Open an empty workbench for the patient; replaces any request.
    | Open of Patient
    /// Send the seed over the context; replaces any request.
    | SeedFilter of FilterSeed * OrderContext
    /// Clear the context's filter; replaces any request.
    | Clear of OrderContext
    /// Evaluate the context again for the patient changed; replaces any request.
    | PatientChanged of Patient * OrderContext
    /// Send a command over the context; one at a time.
    | Call of OrderViewCommand * OrderContext
    /// Tell the user what went wrong.
    | Tell of string[]


/// The workbench stage.
module OrderContextWorkbench =

    /// An empty context for the patient.
    let emptyFor (pat: Patient) = OrderContext.empty |> OrderContext.setPatient pat


    /// Whether the command changes the filter, so that the formulary and parenteralia pages follow
    /// its answer: a pick or a clear of the filter, the diluent or the components, or a seed.
    let changesFilter (cmd: OrderViewCommand) =
        match cmd with
        | OrderViewCommand.SetNthFilterProperty _
        | OrderViewCommand.ClearFilterProperty _
        | OrderViewCommand.ClearAllFilterProperty
        | OrderViewCommand.SetNthDiluentProperty _
        | OrderViewCommand.ClearDiluentProperty
        | OrderViewCommand.SetNthComponentsProperty _
        | OrderViewCommand.SeedFilter _ -> true
        | _ -> false


    /// The context a request shows while it is under way: for a command the context sent as the
    /// command changes it; for a patient update the context sent with the patient updated.
    let shown (sent: OrderContextCommand) =
        match sent with
        | OrderContextCommand.Command(cmd, ctx) -> OrderPlanMachine.Dialog.shown cmd ctx
        | OrderContextCommand.UpdatePatient(pat, ctx) -> { ctx with Patient = pat }


    /// After a failed change: the context as it was, told. The formulary and parenteralia pages
    /// still show its filter, since they follow an answer only.
    let private restore (pat: Patient) (ctx: OrderContext) (errs: string[]) =
        OrderContextWorkbench.Evaluated(pat, ctx), [ OrderContextWorkbenchIntent.Tell errs ]


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
        | OrderContextWorkbenchMsg.PatientChanged None, _ -> OrderContextWorkbench.NoPatient None, []

        // the first patient: the empty workbench is shown while it is evaluated
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.NoPatient None ->
            OrderContextWorkbench.Evaluated(pat, emptyFor pat), [ OrderContextWorkbenchIntent.Open pat ]
        // the first patient with a seed waiting: the seed is sent over the empty workbench
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.NoPatient(Some seed) ->
            OrderContextWorkbench.Evaluated(pat, emptyFor pat),
            [ OrderContextWorkbenchIntent.SeedFilter(seed, emptyFor pat) ]
        // the patient changed: the workbench keeps its filter and is evaluated again
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.Evaluated(_, ctx) ->
            OrderContextWorkbench.Evaluated(pat, { ctx with Patient = pat }),
            [ OrderContextWorkbenchIntent.PatientChanged(pat, ctx) ]

        // a seed needs a patient, so without one it waits for it; with one it is sent over the
        // context held
        | OrderContextWorkbenchMsg.SeedFilter seed, OrderContextWorkbench.NoPatient _ ->
            OrderContextWorkbench.NoPatient(Some seed), []
        | OrderContextWorkbenchMsg.SeedFilter seed, OrderContextWorkbench.Evaluated(_, held) ->
            workbench, [ OrderContextWorkbenchIntent.SeedFilter(seed, held) ]

        // a command, over the context held, for the patient held
        | OrderContextWorkbenchMsg.Command cmd, OrderContextWorkbench.Evaluated(pat, held) ->
            workbench, [ OrderContextWorkbenchIntent.Call(cmd, { held with Patient = pat }) ]
        // nothing to command without a patient
        | OrderContextWorkbenchMsg.Command _, OrderContextWorkbench.NoPatient _ -> workbench, []

        // nothing is asked without a patient, so nothing lands
        | OrderContextWorkbenchMsg.Landed _, OrderContextWorkbench.NoPatient _ -> workbench, []
        // an answer lands as the server answered it, the argumentation included
        | OrderContextWorkbenchMsg.Landed(_, Ok(OrderContextResponse.Evaluated ctx)),
          OrderContextWorkbench.Evaluated(pat, _) -> OrderContextWorkbench.Evaluated(pat, ctx), []
        | OrderContextWorkbenchMsg.Landed(_, Ok(OrderContextResponse.Refused(back, _))),
          OrderContextWorkbench.Evaluated(pat, _) -> refused pat back
        // a failed change leaves the workbench as it was, not as sent; after a failed first
        // evaluation, that is the empty workbench
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Evaluated(pat, held) ->
            restore pat held errs

        // a reset clears the workbench's filter; a failure goes back to the empty workbench;
        // nothing to reset without a patient
        | OrderContextWorkbenchMsg.Reset, OrderContextWorkbench.Evaluated(pat, held) ->
            OrderContextWorkbench.Evaluated(pat, emptyFor pat), [ OrderContextWorkbenchIntent.Clear held ]
        | OrderContextWorkbenchMsg.Reset, _ -> workbench, []


/// Everything the order context machine holds, hidden from the page, which reads an
/// OrderContextView of it instead.
type OrderContextState =
    private
        {
            /// The workbench itself.
            Workbench: OrderContextWorkbench
            /// The request sent, which the page shows meanwhile, and the request id its answer
            /// must name.
            InFlight: (OrderContextCommand * string) option
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
    /// A seed of the filter, sent as a command over the context shown; it waits for a patient.
    | SeedFilter of FilterSeed * request: string
    /// A command from the page, sent over the context held.
    | Command of OrderViewCommand * request: string
    /// The answer to the request with this id: the context evaluated or refused; Error is a
    /// failure of the server or the call.
    | Answered of request: string * Result<OrderContextResponse, string[]>
    /// Clear the workbench and evaluate it empty, after an order was prescribed.
    | Reset of request: string
    /// The scenario the dialog shows, by its order's id.
    | SelectScenario of string option
    /// A clear from the dialog that opens the field's list: the command goes out, and the state
    /// before it is kept to be put back.
    | ReopenField of OrderViewCommand * request: string
    /// The list of a reopen closed without a pick: the context kept is put back, and the answer to
    /// the clear is dropped.
    | RestoreField


/// What the App carries out for the order context machine.
[<RequireQualifiedAccess>]
type OrderContextEffect =
    /// Send the order context command under this request id.
    | CallContext of OrderViewCommand * OrderContext * request: string
    /// Send the patient change over the context under this request id.
    | CallPatientChanged of Patient * OrderContext * request: string
    /// Put the filter on the formulary and parenteralia pages.
    | SyncPages of Filter
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
            Workbench = OrderContextWorkbench.NoPatient None
            InFlight = None
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
            InFlight = Some(OrderContextCommand.Command(OrderViewCommand.ClearAllFilterProperty, emptyFor pat), request)
            Selected = None
            Refusal = None
            Kept = None
        }


    /// The context evaluated for the patient, nothing under way.
    let held (pat: Patient) (ctx: OrderContext) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, ctx)
            InFlight = None
            Selected = None
            Refusal = None
            Kept = None
        }


    /// The context refused for the patient, as sent, with the reason.
    let refused (pat: Patient) (ctx: OrderContext) (refusal: OrderContextRefusal) =
        { held pat ctx with Refusal = Some refusal }


    /// A command under way over the context sent; a failure goes back to the context held.
    let changing (pat: Patient) (cmd: OrderViewCommand) (sent: OrderContext) (held: OrderContext) (request: string) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, held)
            InFlight = Some(OrderContextCommand.Command(cmd, { sent with Patient = pat }), request)
            Selected = None
            Refusal = None
            Kept = None
        }


    /// A patient change under way over the context sent; a failure goes back to the context held.
    let patientChanging (pat: Patient) (sent: OrderContext) (held: OrderContext) (request: string) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, held)
            InFlight = Some(OrderContextCommand.UpdatePatient(pat, sent), request)
            Selected = None
            Refusal = None
            Kept = None
        }


    /// The patient the workbench is evaluated for, if any.
    let patient (state: OrderContextState) =
        match state.Workbench with
        | OrderContextWorkbench.NoPatient _ -> None
        | OrderContextWorkbench.Evaluated(pat, _) -> Some pat


    /// The context shown: the one sent, as its command changes it, while a request is under way;
    /// otherwise the one held.
    let context (state: OrderContextState) =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient _, _ -> None
        | OrderContextWorkbench.Evaluated _, Some(sent, _) -> Some(OrderContextWorkbench.shown sent)
        | OrderContextWorkbench.Evaluated(_, ctx), None -> Some ctx


    /// The context the workbench holds, also while a request is under way: the last one answered,
    /// or the empty one after a reset and during the first evaluation, which a failure goes back
    /// to. None without a patient.
    let answered (state: OrderContextState) =
        match state.Workbench with
        | OrderContextWorkbench.NoPatient _ -> None
        | OrderContextWorkbench.Evaluated(_, ctx) -> Some ctx


    /// What the page reads.
    let view (state: OrderContextState) : OrderContextView =
        match state.Workbench, state.InFlight, state.Refusal with
        | OrderContextWorkbench.NoPatient _, _, _ -> OrderContextView.NoPatient
        | OrderContextWorkbench.Evaluated _, Some(sent, _), _ ->
            OrderContextView.Changing(OrderContextWorkbench.shown sent)
        | OrderContextWorkbench.Evaluated(_, ctx), None, Some refusal -> OrderContextView.Refused(ctx, refusal)
        | OrderContextWorkbench.Evaluated(_, ctx), None, None -> OrderContextView.Settled ctx


    /// The request id the state waits on; None while no request is under way.
    let inFlightRequest (state: OrderContextState) = state.InFlight |> Option.map snd


    /// The workbench narrowed to the scenario with the order with this id and its form, as it
    /// goes into the plan; None unless the workbench is settled and shows that scenario.
    let narrowedTo (orderId: string) (state: OrderContextState) =
        match view state with
        | OrderContextView.Settled ctx ->
            ctx.Scenarios
            |> Array.tryFind (fun sc -> sc.Order.Id = orderId)
            |> Option.map (fun sc ->
                { ctx with
                    OrderContext.Filter.Form = Some sc.Form
                    Scenarios = [| sc |]
                }
            )
        | OrderContextView.NoPatient
        | OrderContextView.Refused _
        | OrderContextView.Changing _ -> None


    /// Whether the state before a reopen is kept, to be put back when its list closes without a pick.
    let isKept (state: OrderContextState) = state.Kept.IsSome


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
    let landing (request: string) (inFlight: (OrderContextCommand * string) option) =
        match inFlight with
        | Some(sent, underWay) when underWay = request -> Some sent
        | _ -> None


    /// The filter the formulary and parenteralia pages take from an answer: the filter of the
    /// context answered, evaluated or refused, when the request sent changed the filter or updated
    /// the patient; None for any other request, so a dose step fetches neither page, and for a
    /// failure, which leaves the pages as they were.
    let synced sent result =
        let follows =
            match sent with
            | OrderContextCommand.Command(cmd, _) -> OrderContextWorkbench.changesFilter cmd
            | OrderContextCommand.UpdatePatient _ -> true

        match result with
        | Ok(OrderContextResponse.Evaluated ctx)
        | Ok(OrderContextResponse.Refused(ctx, _)) when follows -> Some ctx.Filter
        | _ -> None


    /// The request stage: each intent becomes a request under the given id or an effect. A call
    /// while a request is under way is dropped; an evaluation replaces it.
    let private apply (request: string) (intents: OrderContextWorkbenchIntent list) (state: OrderContextState) =
        let call (cmd: OrderViewCommand) (ctx: OrderContext) (state: OrderContextState) =
            { state with InFlight = Some(OrderContextCommand.Command(cmd, ctx), request) },
            [ OrderContextEffect.CallContext(cmd, ctx, request) ]

        intents
        |> List.fold
            (fun (state: OrderContextState, effects) intent ->
                let state, added =
                    match intent with
                    | OrderContextWorkbenchIntent.Open pat ->
                        call OrderViewCommand.ClearAllFilterProperty (OrderContextWorkbench.emptyFor pat) state
                    | OrderContextWorkbenchIntent.SeedFilter(seed, ctx) -> call (FilterSeed.command seed) ctx state
                    | OrderContextWorkbenchIntent.Clear ctx -> call OrderViewCommand.ClearAllFilterProperty ctx state
                    // shown meanwhile as the context evaluated for the new patient
                    | OrderContextWorkbenchIntent.PatientChanged(pat, ctx) ->
                        { state with InFlight = Some(OrderContextCommand.UpdatePatient(pat, ctx), request) },
                        [ OrderContextEffect.CallPatientChanged(pat, ctx, request) ]
                    | OrderContextWorkbenchIntent.Call _ when state.InFlight.IsSome -> state, []
                    | OrderContextWorkbenchIntent.Call(cmd, ctx) -> call cmd ctx state
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
            | _, OrderContextWorkbench.NoPatient _ -> None
            | OrderContextWorkbenchMsg.PatientChanged _, _
            | OrderContextWorkbenchMsg.SeedFilter _, _
            | OrderContextWorkbenchMsg.Reset, _ -> None
            | OrderContextWorkbenchMsg.Landed _, OrderContextWorkbench.Evaluated(_, ctx) ->
                state.Selected |> Option.filter (fun id -> ctx |> holds id)
            | OrderContextWorkbenchMsg.Command _, _ -> state.Selected

        let refusal =
            match msg, workbench with
            | _, OrderContextWorkbench.NoPatient _ -> None
            | OrderContextWorkbenchMsg.Landed(_, Ok(OrderContextResponse.Refused(_, refusal))), _ -> Some refusal
            | OrderContextWorkbenchMsg.Landed _, _
            | OrderContextWorkbenchMsg.PatientChanged _, _
            | OrderContextWorkbenchMsg.SeedFilter _, _
            | OrderContextWorkbenchMsg.Reset, _ -> None
            | OrderContextWorkbenchMsg.Command _, _ -> state.Refusal

        let inFlight =
            match workbench with
            | OrderContextWorkbench.NoPatient _ -> None
            | _ -> state.InFlight

        apply
            request
            intents
            { state with
                Workbench = workbench
                InFlight = inFlight
                Selected = selected
                Refusal = refusal
            }


    /// The next state and effects for a message, a reopen and a restore aside.
    let private move (msg: OrderContextMsg) (state: OrderContextState) : OrderContextState * OrderContextEffect list =
        match msg, state.Workbench, state.InFlight with
        // only an answer to the request under way reaches the workbench; the formulary and
        // parenteralia pages follow the answer, not the request
        | OrderContextMsg.Answered(request, result), _, _ ->
            match landing request state.InFlight with
            | None -> state, []
            | Some sent ->
                let landed, effects =
                    run request (OrderContextWorkbenchMsg.Landed(sent, result)) { state with InFlight = None }

                landed,
                effects
                @ (synced sent result |> Option.map OrderContextEffect.SyncPages |> Option.toList)

        // the selection needs no request
        | OrderContextMsg.SelectScenario id, _, _ -> select id state, []

        // a patient change during a request: the context sent, as its command changes it, is
        // evaluated for the new patient; the dialog closes and the refusal is cleared
        | OrderContextMsg.PatientChanged(Some pat, request), OrderContextWorkbench.Evaluated _, Some(sent, _) ->
            let sent = OrderContextWorkbench.shown sent

            let workbench, _ =
                OrderContextWorkbench.step (OrderContextWorkbenchMsg.PatientChanged(Some pat)) state.Workbench

            apply
                request
                [ OrderContextWorkbenchIntent.PatientChanged(pat, sent) ]
                { state with
                    Workbench = workbench
                    Selected = None
                    Refusal = None
                }

        | OrderContextMsg.PatientChanged(pat, request), _, _ ->
            run request (OrderContextWorkbenchMsg.PatientChanged pat) state
        // a seed during a request: sent over the context sent, as its command changes it; the
        // dialog closes and the refusal is cleared
        | OrderContextMsg.SeedFilter(seed, request), OrderContextWorkbench.Evaluated _, Some(sent, _) ->
            apply
                request
                [
                    OrderContextWorkbenchIntent.SeedFilter(seed, OrderContextWorkbench.shown sent)
                ]
                { state with
                    Selected = None
                    Refusal = None
                }
        | OrderContextMsg.SeedFilter(seed, request), _, _ ->
            run request (OrderContextWorkbenchMsg.SeedFilter seed) state
        | OrderContextMsg.Command(cmd, request), _, _ -> run request (OrderContextWorkbenchMsg.Command cmd) state
        | OrderContextMsg.Reset request, _, _ -> run request OrderContextWorkbenchMsg.Reset state
        // taken by transition
        | OrderContextMsg.ReopenField _, _, _
        | OrderContextMsg.RestoreField, _, _ -> state, []


    /// The next state and effects for a message. A reopen keeps the state before it, which an
    /// answer carries along and a restore puts back; any other message ends the look, and the
    /// state kept goes. A reopen while a request is under way keeps nothing and goes as a plain
    /// command: the state kept would hold that request, and a restore would put it back in flight
    /// after its answer. So the state kept has nothing under way, and the answer to the clear
    /// finds no request to land on after a restore.
    let transition (msg: OrderContextMsg) (state: OrderContextState) : OrderContextState * OrderContextEffect list =
        match msg with
        | OrderContextMsg.ReopenField(cmd, request) when state.InFlight.IsSome ->
            move (OrderContextMsg.Command(cmd, request)) { state with Kept = None }
        | OrderContextMsg.ReopenField(cmd, request) ->
            let kept = { state with Kept = None }
            let moved, effects = move (OrderContextMsg.Command(cmd, request)) kept
            { moved with Kept = Some kept }, effects
        | OrderContextMsg.RestoreField ->
            match state.Kept with
            | Some kept -> kept, []
            | None -> state, []
        | OrderContextMsg.Answered _ -> move msg state
        | _ -> move msg { state with Kept = None }


    /// Whether the message reaches the workbench: a command from the page does not during a patient
    /// change, so that nothing is ordered for the patient being replaced.
    let admitted (patient: PatientMachine.PatientState) (msg: OrderContextMsg) =
        match msg with
        | OrderContextMsg.Command _
        | OrderContextMsg.ReopenField _ -> not (PatientMachine.PatientState.changing patient)
        | OrderContextMsg.PatientChanged _
        | OrderContextMsg.SeedFilter _
        | OrderContextMsg.Answered _
        | OrderContextMsg.Reset _
        | OrderContextMsg.SelectScenario _
        | OrderContextMsg.RestoreField -> true


    /// The transition, with messages not admitted during a patient change ignored.
    let transitionWhile (patient: PatientMachine.PatientState) (msg: OrderContextMsg) (state: OrderContextState) =
        if admitted patient msg then
            transition msg state
        else
            state, []
