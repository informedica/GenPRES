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
type Workbench =
    | NoPatient
    // a filter chosen before a patient is set: evaluated once one is
    | Seeded of OrderContext
    // a patient held, nothing evaluated yet
    | Unevaluated of Patient
    // the context last evaluated for the patient held, the original a failed change goes back to
    | Evaluated of OrderContext


/// What moves the workbench; the answer that landed brings what was sent.
[<RequireQualifiedAccess>]
type WorkbenchMsg =
    | PatientChanged of Patient option
    | Seed of OrderContext
    | Command of OrderContextCommand * OrderContext
    | Landed of sent: (OrderContextCommand * OrderContext) * Result<OrderContext, string[]>
    | Reset


/// What the workbench asks of the lane; the request stage turns these into calls and effects.
[<RequireQualifiedAccess>]
type WorkbenchIntent =
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


module Workbench =

    /// The workbench emptied for the patient.
    let emptyFor (pat: Patient) =
        Shared.Models.OrderContext.empty |> Shared.Models.OrderContext.setPatient pat


    /// What the server says when the filter matches no dose rule; the page then starts over.
    let noDoseRules (errs: string[]) =
        errs |> Array.exists (fun e -> e.ToLower().Contains "geen doseerregels")


    /// A failed change: the context given, the original, kept, and the formulary and the
    /// parenteralia back on its filter, since the evaluation had taken them along.
    let private restore (ctx: OrderContext) (errs: string[]) =
        Workbench.Evaluated ctx,
        [
            WorkbenchIntent.Tell errs
            WorkbenchIntent.Sync ctx.Filter
        ]


    /// The filter matched no dose rule: the page is left, the failure said, and the empty
    /// workbench evaluated again.
    let private startOver (pat: Patient) (errs: string[]) =
        let empty = emptyFor pat

        Workbench.Evaluated empty,
        [
            WorkbenchIntent.GoToLifeSupport
            WorkbenchIntent.Tell errs
            WorkbenchIntent.Evaluate empty
        ]


    /// The domain stage: the workbench changes only on an answer that landed and on the patient;
    /// everything else is an intent for the request stage.
    let step (msg: WorkbenchMsg) (workbench: Workbench) : Workbench * WorkbenchIntent list =
        match msg, workbench with
        // no patient, no workbench; a seed keeps waiting
        | WorkbenchMsg.PatientChanged None, Workbench.Seeded _ -> workbench, []
        | WorkbenchMsg.PatientChanged None, _ -> Workbench.NoPatient, []

        // the first patient: the empty workbench opened, or the seed that waited for it evaluated
        | WorkbenchMsg.PatientChanged(Some pat), Workbench.NoPatient
        | WorkbenchMsg.PatientChanged(Some pat), Workbench.Unevaluated _ ->
            Workbench.Unevaluated pat, [ WorkbenchIntent.Open pat ]
        // the patient changed: the workbench keeps its filter and is evaluated again for the new
        // patient
        | WorkbenchMsg.PatientChanged(Some pat), Workbench.Seeded ctx
        | WorkbenchMsg.PatientChanged(Some pat), Workbench.Evaluated ctx ->
            let ctx = { ctx with Patient = pat }
            Workbench.Evaluated ctx, [ WorkbenchIntent.Evaluate ctx ]

        // a filter before a patient waits; with a patient it is evaluated at once
        | WorkbenchMsg.Seed ctx, Workbench.NoPatient
        | WorkbenchMsg.Seed ctx, Workbench.Seeded _ -> Workbench.Seeded ctx, []
        // nothing evaluated yet: the seed is what a failed evaluation goes back to
        | WorkbenchMsg.Seed ctx, Workbench.Unevaluated pat ->
            let ctx = { ctx with Patient = pat }
            Workbench.Evaluated ctx, [ WorkbenchIntent.Evaluate ctx ]
        | WorkbenchMsg.Seed ctx, Workbench.Evaluated held ->
            workbench,
            [
                WorkbenchIntent.Evaluate { ctx with Patient = held.Patient }
            ]

        // a command over the workbench held, always for the patient held
        | WorkbenchMsg.Command(cmd, ctx), Workbench.Evaluated held ->
            workbench,
            [
                WorkbenchIntent.Call(cmd, { ctx with Patient = held.Patient })
            ]
        // a filter chosen before a patient is set waits as the seed
        | WorkbenchMsg.Command(_, ctx), Workbench.NoPatient
        | WorkbenchMsg.Command(_, ctx), Workbench.Seeded _ -> Workbench.Seeded ctx, []
        | WorkbenchMsg.Command _, Workbench.Unevaluated _ -> workbench, []

        // nothing was asked without a patient or for a seed, so nothing lands there
        | WorkbenchMsg.Landed _, Workbench.NoPatient
        | WorkbenchMsg.Landed _, Workbench.Seeded _ -> workbench, []
        | WorkbenchMsg.Landed(_, Ok ctx), _ -> Workbench.Evaluated ctx, []
        | WorkbenchMsg.Landed(_, Error errs), Workbench.Unevaluated pat when noDoseRules errs -> startOver pat errs
        | WorkbenchMsg.Landed(_, Error errs), Workbench.Evaluated held when noDoseRules errs ->
            startOver held.Patient errs
        // a failed first evaluation lands on the empty workbench; a failed change leaves the
        // workbench as the request found it, never the context sent, whose order and texts the
        // server did not confirm
        | WorkbenchMsg.Landed(_, Error errs), Workbench.Unevaluated pat -> restore (emptyFor pat) errs
        | WorkbenchMsg.Landed(_, Error errs), Workbench.Evaluated held -> restore held errs

        // the workbench cleared for the patient held and evaluated empty; nothing to clear
        // without a patient
        | WorkbenchMsg.Reset, Workbench.Unevaluated pat ->
            Workbench.Evaluated(emptyFor pat), [ WorkbenchIntent.Evaluate(emptyFor pat) ]
        | WorkbenchMsg.Reset, Workbench.Evaluated held ->
            Workbench.Evaluated(emptyFor held.Patient), [ WorkbenchIntent.Evaluate(emptyFor held.Patient) ]
        | WorkbenchMsg.Reset, _ -> workbench, []


/// The workbench and the one request under way: the command and the context sent (what the page
/// shows meanwhile) and the id the answer must name; none while idle. Built through the
/// constructors below only, which admit the combinations that can occur: no patient or a seed
/// with nothing under way, a first evaluation under way, a context held, a change under way.
type OrderContextState =
    private
        {
            Workbench: Workbench
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
            Workbench = Workbench.NoPatient
            InFlight = None
        }


    let seeded (ctx: OrderContext) =
        {
            Workbench = Workbench.Seeded ctx
            InFlight = None
        }


    let emptyFor = Workbench.emptyFor


    /// The first evaluation for the patient under way: nothing held yet.
    let opening (pat: Patient) (request: string) =
        {
            Workbench = Workbench.Unevaluated pat
            InFlight = Some((OrderContextCommand.UpdateOrderContext, emptyFor pat), request)
        }


    /// The context held, nothing under way.
    let held (ctx: OrderContext) =
        {
            Workbench = Workbench.Evaluated ctx
            InFlight = None
        }


    /// A command under way over the context sent, always for the patient held; the context held
    /// is what a failed change goes back to.
    let changing (cmd: OrderContextCommand) (sent: OrderContext) (held: OrderContext) (request: string) =
        {
            Workbench = Workbench.Evaluated held
            InFlight = Some((cmd, { sent with Patient = held.Patient }), request)
        }


    /// The patient the workbench is evaluated for, none without one.
    let patient (state: OrderContextState) =
        match state.Workbench with
        | Workbench.NoPatient
        | Workbench.Seeded _ -> None
        | Workbench.Unevaluated pat -> Some pat
        | Workbench.Evaluated ctx -> Some ctx.Patient


    /// The context the workbench shows: the one sent while a request is under way, the one held
    /// otherwise; none before the first evaluation.
    let context (state: OrderContextState) =
        match state.Workbench, state.InFlight with
        | Workbench.NoPatient, _
        | Workbench.Unevaluated _, _ -> None
        | Workbench.Seeded ctx, _ -> Some ctx
        | Workbench.Evaluated _, Some((_, sent), _) -> Some sent
        | Workbench.Evaluated ctx, None -> Some ctx


    /// The context held and the one sent, changed in place: the filter kept in step with the
    /// formulary's and the parenteralia's.
    let map (f: OrderContext -> OrderContext) (state: OrderContextState) =
        let workbench =
            match state.Workbench with
            | Workbench.NoPatient
            | Workbench.Unevaluated _ -> state.Workbench
            | Workbench.Seeded ctx -> Workbench.Seeded(f ctx)
            | Workbench.Evaluated ctx -> Workbench.Evaluated(f ctx)

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
        | Workbench.NoPatient, _ -> HasNotStartedYet
        | Workbench.Seeded ctx, _ -> Resolved ctx
        | Workbench.Unevaluated _, _ -> InProgress
        | Workbench.Evaluated _, Some((_, sent), _) -> Recalculating sent
        | Workbench.Evaluated ctx, None -> Resolved ctx


    /// The request stage's check: the payload sent when the answer names the request under way,
    /// none for any other answer, so that an answer lands only on its request.
    let landing (request: string) (inFlight: ((OrderContextCommand * OrderContext) * string) option) =
        match inFlight with
        | Some(sent, underWay) when underWay = request -> Some sent
        | _ -> None


    /// The request stage: the intents applied in order, each a request under the id given or an
    /// effect; a call while a request is under way is dropped.
    let private apply (request: string) (intents: WorkbenchIntent list) (state: OrderContextState) =
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
                    | WorkbenchIntent.Open pat ->
                        let ctx = Workbench.emptyFor pat

                        { state with InFlight = Some((OrderContextCommand.UpdateOrderContext, ctx), request) },
                        [
                            OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, ctx, request)
                        ]
                    | WorkbenchIntent.Evaluate ctx -> evaluate ctx state
                    | WorkbenchIntent.Call _ when state.InFlight.IsSome -> state, []
                    | WorkbenchIntent.Call(OrderContextCommand.UpdateOrderContext, ctx) -> evaluate ctx state
                    | WorkbenchIntent.Call(cmd, ctx) ->
                        { state with InFlight = Some((cmd, ctx), request) },
                        [ OrderContextEffect.CallContext(cmd, ctx, request) ]
                    | WorkbenchIntent.Sync filter ->
                        state,
                        [
                            OrderContextEffect.SyncFormulary filter
                            OrderContextEffect.SyncParenteralia filter
                        ]
                    | WorkbenchIntent.GoToLifeSupport -> state, [ OrderContextEffect.GoToLifeSupport ]
                    | WorkbenchIntent.Tell errs -> state, [ OrderContextEffect.TellError errs ]

                state, effects @ added
            )
            (state, [])


    /// The domain stage first, then the request stage: the workbench steps, and its intents
    /// become the request under way and the effects. A patient cleared clears the request.
    let private run (request: string) (msg: WorkbenchMsg) (state: OrderContextState) =
        let workbench, intents = Workbench.step msg state.Workbench

        let inFlight =
            match workbench with
            | Workbench.NoPatient -> None
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
            | Some sent -> run request (WorkbenchMsg.Landed(sent, result)) { state with InFlight = None }

        // the patient changed while a change is under way: the context sent is evaluated for the
        // new patient, and the one held stays what a failed change goes back to
        | OrderContextMsg.PatientChanged(Some pat, request), Workbench.Evaluated _, Some((_, sent), _) ->
            let workbench, _ =
                Workbench.step (WorkbenchMsg.PatientChanged(Some pat)) state.Workbench

            apply request [ WorkbenchIntent.Evaluate { sent with Patient = pat } ] { state with Workbench = workbench }

        | OrderContextMsg.PatientChanged(pat, request), _, _ -> run request (WorkbenchMsg.PatientChanged pat) state
        | OrderContextMsg.Seed(ctx, request), _, _ -> run request (WorkbenchMsg.Seed ctx) state
        | OrderContextMsg.Command(cmd, ctx, request), _, _ -> run request (WorkbenchMsg.Command(cmd, ctx)) state
        | OrderContextMsg.Reset request, _, _ -> run request WorkbenchMsg.Reset state
