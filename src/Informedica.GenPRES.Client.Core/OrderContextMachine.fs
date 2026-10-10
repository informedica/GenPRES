/// Tracks the prescribing workbench: an order context not yet in the plan, from no patient to an
/// evaluated context, through changes and resets. The App carries out the effects.
///
/// Four invariants:
/// - one request is in flight at a time: the pages are disabled while it is, so nothing but its
///   answer and the patient cleared reaches the machine meanwhile;
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


    /// The seed of an item chosen on a medication list: its generic, and its indication, route and
    /// dose type where the list gives one.
    let ofListItem generic indication route doseType =
        let given s = if s = "" then None else Some s

        {
            Source = SeedSource.MedicationList
            Indication = indication |> given
            Generic = Some generic
            Route = route |> given
            Form = None
            DoseType = doseType |> given |> Option.map DoseType.doseTypeFromString
        }


/// The workbench itself, without any request under way.
[<RequireQualifiedAccess>]
type OrderContextWorkbench =
    /// No patient, so no workbench; the url's seed on an anonymous page load, which arrives
    /// before the patient, waits for it.
    | NoPatient of awaiting: FilterSeed option
    /// The patient and the context last evaluated for it, which a failed change goes back to: the
    /// empty context during the first evaluation and after a reset.
    | Evaluated of Patient * OrderContext
    /// The patient and the context the server refused, as sent, with its picks kept and its
    /// scenarios dropped, and the reason.
    | Refused of Patient * OrderContext * OrderContextRefusal


/// Functions over OrderContextWorkbench.
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
        | OrderContextCommand.Command(cmd, ctx) -> CommandPreview.shown cmd ctx
        | OrderContextCommand.UpdatePatient(pat, ctx) -> { ctx with Patient = pat }


    /// The patient and the context the workbench holds; None without a patient.
    let held (workbench: OrderContextWorkbench) =
        match workbench with
        | OrderContextWorkbench.NoPatient _ -> None
        | OrderContextWorkbench.Evaluated(pat, ctx)
        | OrderContextWorkbench.Refused(pat, ctx, _) -> Some(pat, ctx)


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
            /// The state before a reopen from the dialog, put back when its list closes without a
            /// pick; None when no reopen is looked at.
            Kept: OrderContextState option
        }


/// What moves the order context machine. A message that starts a request carries its request id,
/// so the answer can name it.
[<RequireQualifiedAccess>]
type OrderContextMsg =
    /// The patient data set, changed or cleared; the workbench is evaluated for it.
    | PatientDataChanged of Patient option * request: string
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
    /// Send the order context command, a command over a context or a patient change, under this
    /// request id.
    | CallContext of OrderContextCommand * request: string
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
            Kept = None
        }


    /// The context evaluated for the patient, nothing under way.
    let held (pat: Patient) (ctx: OrderContext) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, ctx)
            InFlight = None
            Selected = None
            Kept = None
        }


    /// The context refused for the patient, as sent, with the reason.
    let refused (pat: Patient) (ctx: OrderContext) (refusal: OrderContextRefusal) =
        { held pat ctx with Workbench = OrderContextWorkbench.Refused(pat, ctx, refusal) }


    /// A command under way over the context sent; a failure goes back to the context held.
    let changing (pat: Patient) (cmd: OrderViewCommand) (sent: OrderContext) (held: OrderContext) (request: string) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, held)
            InFlight = Some(OrderContextCommand.Command(cmd, { sent with Patient = pat }), request)
            Selected = None
            Kept = None
        }


    /// A patient change under way over the context sent; a failure goes back to the context held.
    let patientChanging (pat: Patient) (sent: OrderContext) (held: OrderContext) (request: string) =
        {
            Workbench = OrderContextWorkbench.Evaluated(pat, held)
            InFlight = Some(OrderContextCommand.UpdatePatient(pat, sent), request)
            Selected = None
            Kept = None
        }


    /// The patient the workbench is evaluated for, if any.
    let patient (state: OrderContextState) = state.Workbench |> OrderContextWorkbench.held |> Option.map fst


    /// The context the workbench holds, also while a request is under way: the last one answered,
    /// or the empty one after a reset and during the first evaluation, which a failure goes back
    /// to. None without a patient.
    let answered (state: OrderContextState) = state.Workbench |> OrderContextWorkbench.held |> Option.map snd


    /// What the page reads: while a request is under way the context sent, as its command changes
    /// it; otherwise the context held, settled or refused.
    let view (state: OrderContextState) : OrderContextView =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient _, _ -> OrderContextView.NoPatient
        | _, Some(sent, _) -> OrderContextView.Changing(OrderContextWorkbench.shown sent)
        | OrderContextWorkbench.Evaluated(_, ctx), None -> OrderContextView.Settled ctx
        | OrderContextWorkbench.Refused(_, ctx, refusal), None -> OrderContextView.Refused(ctx, refusal)


    /// The context shown, the one the view holds.
    let context (state: OrderContextState) =
        match view state with
        | OrderContextView.NoPatient -> None
        | OrderContextView.Settled ctx
        | OrderContextView.Refused(ctx, _)
        | OrderContextView.Changing ctx -> Some ctx


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


    /// The next state and effects for a message. Only an answer, a selection, a restore and the
    /// patient cleared reach the workbench while a request is under way; every other message
    /// comes only with nothing under way, since the pages that send it are disabled, and with a
    /// request out falls to the closing arm, which leaves the state as it is.
    ///
    /// The dialog closes on a patient change, a seed and a reset, and after an answer keeps its
    /// selection only while the context answered holds it. A reopen keeps the state before it,
    /// which an answer carries along and a restore puts back; any other message ends the look.
    /// The state kept has nothing under way, so the answer to the clear finds no request to land
    /// on after a restore.
    let transition (msg: OrderContextMsg) (state: OrderContextState) : OrderContextState * OrderContextEffect list =
        // the one place a request goes out
        let send sent request (state: OrderContextState) =
            { state with InFlight = Some(sent, request) }, [ OrderContextEffect.CallContext(sent, request) ]

        let call cmd ctx request state = send (OrderContextCommand.Command(cmd, ctx)) request state

        // a command from the page over the context held, for the patient held; a refusal goes,
        // so the workbench is refused only with nothing under way
        let command cmd pat ctx request kept =
            call
                cmd
                { ctx with Patient = pat }
                request
                { state with
                    Workbench = OrderContextWorkbench.Evaluated(pat, ctx)
                    Kept = kept
                }

        // a new workbench, the dialog closed and the look ended
        let renewed workbench =
            { state with
                Workbench = workbench
                Selected = None
                Kept = None
            }

        match msg, OrderContextWorkbench.held state.Workbench, state.InFlight with
        // only an answer to the request under way lands; the formulary and parenteralia pages
        // follow the answer, not the request
        | OrderContextMsg.Answered(request, result), Some(pat, held), Some(sent, underWay) when underWay = request ->
            let workbench, told =
                match result with
                | Ok(OrderContextResponse.Evaluated ctx) -> OrderContextWorkbench.Evaluated(pat, ctx), []
                | Ok(OrderContextResponse.Refused(back, refusal)) ->
                    OrderContextWorkbench.Refused(pat, { back with Scenarios = [||] }, refusal), []
                // a failed change leaves the workbench as it was, not as sent
                | Error errs -> OrderContextWorkbench.Evaluated(pat, held), [ OrderContextEffect.TellError errs ]

            let selected =
                workbench
                |> OrderContextWorkbench.held
                |> Option.bind (fun (_, ctx) -> state.Selected |> Option.filter (fun id -> ctx |> holds id))

            { state with
                Workbench = workbench
                InFlight = None
                Selected = selected
            },
            told
            @ (synced sent result |> Option.map OrderContextEffect.SyncPages |> Option.toList)
        | OrderContextMsg.Answered _, _, _ -> state, []

        // the selection needs no request, and ends a look
        | OrderContextMsg.SelectScenario id, _, _ -> select id { state with Kept = None }, []
        | OrderContextMsg.RestoreField, _, _ ->
            match state.Kept with
            | Some kept -> kept, []
            | None -> state, []

        // the patient cleared reaches every state: no workbench, and the request out is dropped
        | OrderContextMsg.PatientDataChanged(None, _), _, _ -> noPatient, []
        // the first patient: the empty workbench is shown while it is evaluated, with the seed
        // waiting, if any
        | OrderContextMsg.PatientDataChanged(Some pat, request), None, None ->
            let empty = emptyFor pat

            let cmd =
                match state.Workbench with
                | OrderContextWorkbench.NoPatient(Some seed) -> FilterSeed.command seed
                | _ -> OrderViewCommand.ClearAllFilterProperty

            call cmd empty request (renewed (OrderContextWorkbench.Evaluated(pat, empty)))
        // the patient changed: the workbench keeps its filter and is evaluated again
        | OrderContextMsg.PatientDataChanged(Some pat, request), Some(_, ctx), None ->
            send
                (OrderContextCommand.UpdatePatient(pat, ctx))
                request
                (renewed (OrderContextWorkbench.Evaluated(pat, { ctx with Patient = pat })))

        // a seed needs a patient, so without one it waits for it
        | OrderContextMsg.SeedFilter(seed, _), None, None -> renewed (OrderContextWorkbench.NoPatient(Some seed)), []
        | OrderContextMsg.SeedFilter(seed, request), Some(pat, ctx), None ->
            call (FilterSeed.command seed) ctx request (renewed (OrderContextWorkbench.Evaluated(pat, ctx)))

        // a command, over the context held, for the patient held
        | OrderContextMsg.Command(cmd, request), Some(pat, ctx), None -> command cmd pat ctx request None

        // a reset clears the filter of the context held; a failure goes back to the empty workbench
        | OrderContextMsg.Reset request, Some(pat, ctx), None ->
            call
                OrderViewCommand.ClearAllFilterProperty
                ctx
                request
                (renewed (OrderContextWorkbench.Evaluated(pat, emptyFor pat)))

        // a reopen goes as a command, with the state before it kept
        | OrderContextMsg.ReopenField(cmd, request), Some(pat, ctx), None ->
            command cmd pat ctx request (Some { state with Kept = None })

        // the closing arm: nothing else comes, since the pages that send it are disabled, and
        // there is nothing to command or reset without a patient
        | _ -> state, []
