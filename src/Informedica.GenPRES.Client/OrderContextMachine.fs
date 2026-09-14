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
/// patient change re-evaluating it; and a filter that arrives before a patient (from the url or
/// the menu) waits as a seed and is evaluated once the patient is set.
module OrderContextMachine

open Shared.Types
open Shared.Api


/// The workbench as the clinical model has it: no request ids here.
[<RequireQualifiedAccess>]
type OrderContextWorkbench =
    | NoPatient
    // a filter chosen before a patient is set: evaluated once one is
    | Seeded of OrderContext
    // a patient held, nothing evaluated yet
    | Unevaluated of Patient
    // the context last evaluated for the patient held, the original a failed change goes back to
    | Evaluated of OrderContext


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
        Shared.Models.OrderContext.empty |> Shared.Models.OrderContext.setPatient pat


    /// What the server says when the filter matches no dose rule; the page then starts over.
    let noDoseRules (errs: string[]) =
        errs |> Array.exists (fun e -> e.ToLower().Contains "geen doseerregels")


    /// A failed change: the context given, the original, kept, and the formulary and the
    /// parenteralia back on its filter, since the evaluation had taken them along.
    let private restore (ctx: OrderContext) (errs: string[]) =
        OrderContextWorkbench.Evaluated ctx,
        [
            OrderContextWorkbenchIntent.Tell errs
            OrderContextWorkbenchIntent.Sync ctx.Filter
        ]


    /// The filter matched no dose rule: the page is left, the failure said, and the empty
    /// workbench evaluated again.
    let private startOver (pat: Patient) (errs: string[]) =
        let empty = emptyFor pat

        OrderContextWorkbench.Evaluated empty,
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
        // no patient, no workbench; a seed keeps waiting
        | OrderContextWorkbenchMsg.PatientChanged None, OrderContextWorkbench.Seeded _ -> workbench, []
        | OrderContextWorkbenchMsg.PatientChanged None, _ -> OrderContextWorkbench.NoPatient, []

        // the first patient: the empty workbench opened, or the seed that waited for it evaluated
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.NoPatient
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.Unevaluated _ ->
            OrderContextWorkbench.Unevaluated pat, [ OrderContextWorkbenchIntent.Open pat ]
        // the patient changed: the workbench keeps its filter and is evaluated again for the new
        // patient
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.Seeded ctx
        | OrderContextWorkbenchMsg.PatientChanged(Some pat), OrderContextWorkbench.Evaluated ctx ->
            let ctx = { ctx with Patient = pat }
            OrderContextWorkbench.Evaluated ctx, [ OrderContextWorkbenchIntent.Evaluate ctx ]

        // a filter before a patient waits; with a patient it is evaluated at once
        | OrderContextWorkbenchMsg.Seed ctx, OrderContextWorkbench.NoPatient
        | OrderContextWorkbenchMsg.Seed ctx, OrderContextWorkbench.Seeded _ -> OrderContextWorkbench.Seeded ctx, []
        // nothing evaluated yet: the seed is what a failed evaluation goes back to
        | OrderContextWorkbenchMsg.Seed ctx, OrderContextWorkbench.Unevaluated pat ->
            let ctx = { ctx with Patient = pat }
            OrderContextWorkbench.Evaluated ctx, [ OrderContextWorkbenchIntent.Evaluate ctx ]
        | OrderContextWorkbenchMsg.Seed ctx, OrderContextWorkbench.Evaluated held ->
            workbench,
            [
                OrderContextWorkbenchIntent.Evaluate { ctx with Patient = held.Patient }
            ]

        // a command over the workbench held, always for the patient held
        | OrderContextWorkbenchMsg.Command(cmd, ctx), OrderContextWorkbench.Evaluated held ->
            workbench,
            [
                OrderContextWorkbenchIntent.Call(cmd, { ctx with Patient = held.Patient })
            ]
        // a filter chosen before a patient is set waits as the seed
        | OrderContextWorkbenchMsg.Command(_, ctx), OrderContextWorkbench.NoPatient
        | OrderContextWorkbenchMsg.Command(_, ctx), OrderContextWorkbench.Seeded _ ->
            OrderContextWorkbench.Seeded ctx, []
        | OrderContextWorkbenchMsg.Command _, OrderContextWorkbench.Unevaluated _ -> workbench, []

        // nothing was asked without a patient or for a seed, so nothing lands there
        | OrderContextWorkbenchMsg.Landed _, OrderContextWorkbench.NoPatient
        | OrderContextWorkbenchMsg.Landed _, OrderContextWorkbench.Seeded _ -> workbench, []
        | OrderContextWorkbenchMsg.Landed(_, Ok ctx), _ -> OrderContextWorkbench.Evaluated ctx, []
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Unevaluated pat when noDoseRules errs ->
            startOver pat errs
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Evaluated held when noDoseRules errs ->
            startOver held.Patient errs
        // a failed first evaluation lands on the empty workbench; a failed change leaves the
        // workbench as the request found it, never the context sent, whose order and texts the
        // server did not confirm
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Unevaluated pat ->
            restore (emptyFor pat) errs
        | OrderContextWorkbenchMsg.Landed(_, Error errs), OrderContextWorkbench.Evaluated held -> restore held errs

        // the workbench cleared for the patient held and evaluated empty; nothing to clear
        // without a patient
        | OrderContextWorkbenchMsg.Reset, OrderContextWorkbench.Unevaluated pat ->
            OrderContextWorkbench.Evaluated(emptyFor pat), [ OrderContextWorkbenchIntent.Evaluate(emptyFor pat) ]
        | OrderContextWorkbenchMsg.Reset, OrderContextWorkbench.Evaluated held ->
            OrderContextWorkbench.Evaluated(emptyFor held.Patient),
            [
                OrderContextWorkbenchIntent.Evaluate(emptyFor held.Patient)
            ]
        | OrderContextWorkbenchMsg.Reset, _ -> workbench, []


/// The workbench and the one request under way: the command and the context sent (what the page
/// shows meanwhile) and the id the answer must name; none while idle. Built through the
/// constructors below only, which admit the combinations that can occur: no patient or a seed
/// with nothing under way, a first evaluation under way, a context held, a change under way.
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
    // a filter from the url or the menu, evaluated for the patient held or kept until there is one
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


module OrderContextState =

    let noPatient =
        {
            Workbench = OrderContextWorkbench.NoPatient
            InFlight = None
        }


    let seeded (ctx: OrderContext) =
        {
            Workbench = OrderContextWorkbench.Seeded ctx
            InFlight = None
        }


    let emptyFor = OrderContextWorkbench.emptyFor


    /// The first evaluation for the patient under way: nothing held yet.
    let opening (pat: Patient) (request: string) =
        {
            Workbench = OrderContextWorkbench.Unevaluated pat
            InFlight = Some((OrderContextCommand.UpdateOrderContext, emptyFor pat), request)
        }


    /// The context held, nothing under way.
    let held (ctx: OrderContext) =
        {
            Workbench = OrderContextWorkbench.Evaluated ctx
            InFlight = None
        }


    /// A command under way over the context sent, always for the patient held; the context held
    /// is what a failed change goes back to.
    let changing (cmd: OrderContextCommand) (sent: OrderContext) (held: OrderContext) (request: string) =
        {
            Workbench = OrderContextWorkbench.Evaluated held
            InFlight = Some((cmd, { sent with Patient = held.Patient }), request)
        }


    /// The patient the workbench is evaluated for, none without one.
    let patient (state: OrderContextState) =
        match state.Workbench with
        | OrderContextWorkbench.NoPatient
        | OrderContextWorkbench.Seeded _ -> None
        | OrderContextWorkbench.Unevaluated pat -> Some pat
        | OrderContextWorkbench.Evaluated ctx -> Some ctx.Patient


    /// The context the workbench shows: the one sent while a request is under way, the one held
    /// otherwise; none before the first evaluation.
    let context (state: OrderContextState) =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient, _
        | OrderContextWorkbench.Unevaluated _, _ -> None
        | OrderContextWorkbench.Seeded ctx, _ -> Some ctx
        | OrderContextWorkbench.Evaluated _, Some((_, sent), _) -> Some sent
        | OrderContextWorkbench.Evaluated ctx, None -> Some ctx


    /// The context held and the one sent, changed in place: the filter kept in step with the
    /// formulary's and the parenteralia's.
    let map (f: OrderContext -> OrderContext) (state: OrderContextState) =
        let workbench =
            match state.Workbench with
            | OrderContextWorkbench.NoPatient
            | OrderContextWorkbench.Unevaluated _ -> state.Workbench
            | OrderContextWorkbench.Seeded ctx -> OrderContextWorkbench.Seeded(f ctx)
            | OrderContextWorkbench.Evaluated ctx -> OrderContextWorkbench.Evaluated(f ctx)

        let inFlight =
            state.InFlight
            |> Option.map (fun ((cmd, sent), request) -> (cmd, f sent), request)

        {
            Workbench = workbench
            InFlight = inFlight
        }


    /// The workbench as the pages read it: a seed shows its filter while it waits; the context
    /// sent shows while a request is under way.
    let toDeferred (state: OrderContextState) : Deferred<OrderContext> =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient, _ -> HasNotStartedYet
        | OrderContextWorkbench.Seeded ctx, _ -> Resolved ctx
        | OrderContextWorkbench.Unevaluated _, _ -> InProgress
        | OrderContextWorkbench.Evaluated _, Some((_, sent), _) -> Provisional sent
        | OrderContextWorkbench.Evaluated ctx, None -> Resolved ctx


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
