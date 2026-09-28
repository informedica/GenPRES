// Step 4 of the plan for #985, the machine's half: the answer the machine receives carries the
// contract's response, evaluated or refused. A refused answer keeps the workbench evaluated
// with the context as it was sent, its picks kept and its scenarios dropped, and holds the
// refusal next to the dialog's selection; the page shows it in place as a Refused view. The
// page switch to the emergency list goes, with the string match that decided it.
//
// The module below is `OrderContextMachine.fs` as it becomes, shadowing the one load.fsx
// loads; the tests at the end are the ones that migrate to `OrderContextMachineTests.fs`.
//
// Run: `dotnet fsi OrderContextRefusal.fsx` from this directory, after `dotnet run build`.

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: Expecto"
#load "../../../tests/Informedica.GenPRES.Client.Core.Tests/OrderPlanMachineTests.fs"

open Expecto
open Expecto.Flip


/// The prescribing workbench as the client holds it: an order context not yet in the plan, from
/// no patient to the empty context opened for one, shown, changed and cleared, as a pure state machine next to the
/// order plan's, with effects for the App to interpret. The machine is two stages: the workbench
/// as the clinical model has it, which knows no request, and the one request under way, which
/// knows no context beyond the one it carries. `transition` runs them in order: an answer passes
/// the request first and reaches the workbench only when it lands; a command passes the
/// workbench first and reaches the request as an intent, dropped while one is under way. The
/// interpreter completes each call from the open Session, keeps the formulary and parenteralia
/// filters in step, and puts the words of a failure on the snackbar.
///
/// Four invariants: one request is in flight at a time, and a command sent while one is under way
/// is dropped (the page greys its controls meanwhile) unless it is the dialog's, which waits as
/// the one pending, the latest replacing an earlier one, and goes out when the answer lands; an
/// answer names the request it answers
/// and lands only on that request; the workbench is always evaluated for the patient held, a
/// patient change re-evaluating it; and a filter needs a patient, since the patient is part of
/// it: one that arrives without (from the url) is dropped, and the App says so.
///
/// An answer is one of three: the context evaluated; the context refused, as it was sent, with
/// the reason no dose can be shown, which the page says in place and the user re-picks from;
/// or a failure, the server's or the call's, after which the workbench stays as the request
/// found it. The page is never taken away.
module OrderContextMachine =

    open Shared.Types
    open Shared.Models
    open Shared.Api


    /// The workbench as the clinical model has it: no request ids here.
    [<RequireQualifiedAccess>]
    type OrderContextWorkbench =
        | NoPatient
        // the patient held and the context last evaluated for it, the original a failed change goes
        // back to; the empty context while the first evaluation is under way; the context as sent
        // after a refusal
        | Evaluated of Patient * OrderContext


    /// What moves the workbench; the answer that landed brings what was sent.
    [<RequireQualifiedAccess>]
    type OrderContextWorkbenchMsg =
        | PatientChanged of Patient option
        | Seed of OrderContext
        | Command of OrderContextCommand * OrderContext
        | Landed of sent: (OrderContextCommand * OrderContext) * Result<OrderContextResponse, string[]>
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
        | Tell of string[]


    module OrderContextWorkbench =

        /// The workbench emptied for the patient.
        let emptyFor (pat: Patient) = OrderContext.empty |> OrderContext.setPatient pat


        /// A failed change: the context given, the original, kept, and the formulary and the
        /// parenteralia back on its filter, since the evaluation had taken them along.
        let private restore (pat: Patient) (ctx: OrderContext) (errs: string[]) =
            OrderContextWorkbench.Evaluated(pat, ctx),
            [
                OrderContextWorkbenchIntent.Tell errs
                OrderContextWorkbenchIntent.Sync ctx.Filter
            ]


        /// A refusal: the context as it was sent, its picks kept for the user to re-pick from, its
        /// scenarios dropped, since the server confirmed none. The pages were put on its filter
        /// when it went out, so nothing to sync, and the page says why, so nothing to tell.
        let private refused (pat: Patient) (sent: OrderContext) =
            OrderContextWorkbench.Evaluated(pat, { sent with Scenarios = [||] }), []


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
            | OrderContextWorkbenchMsg.Landed(_, Ok(OrderContextResponse.Evaluated ctx)),
              OrderContextWorkbench.Evaluated(pat, _) -> OrderContextWorkbench.Evaluated(pat, ctx), []
            | OrderContextWorkbenchMsg.Landed(_, Ok(OrderContextResponse.Refused(sent, _))),
              OrderContextWorkbench.Evaluated(pat, _) -> refused pat sent
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
    /// under way), the dialog's selection and the refusal the last answer carried. Built through
    /// the constructors below only, which admit the combinations that can occur: no patient with
    /// nothing under way, a context held, a change under way (the first evaluation over the empty
    /// context among them), with or without a command pending, with or without a scenario
    /// selected, with or without a refusal to show.
    type OrderContextState =
        private
            {
                Workbench: OrderContextWorkbench
                InFlight: ((OrderContextCommand * OrderContext) * string) option
                Pending: (OrderContextCommand * OrderContext * string) option
                // the scenario the order dialog shows, by its order's id: only one the context
                // shown holds; none while the dialog is closed
                Selected: string option
                // why the last answer refused the context held; none after any other answer, a
                // patient change, a seed or a reset
                Refusal: OrderContextRefusal option
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
        // the server's answer to the request named: the context evaluated or refused; Error = a
        // failure, the server's or the call's
        | Answered of request: string * Result<OrderContextResponse, string[]>
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
        | TellError of string[]


    /// The workbench as the page shows it: the states a page can be in, each with what is valid in
    /// it and nothing of the request. Nothing without a patient; the context the server answered,
    /// nothing under way; the context the server refused, as it was sent, with why, nothing under
    /// way; a change under way, the context sent shown meanwhile, the empty context while the
    /// first evaluation runs. A page renders from `Settled`, `Refused` and `Changing` alike, so
    /// that the screen stays populated while a request runs and the picks stay after a refusal;
    /// it steps from `Changing` too, over the context shown, and builds every other command from
    /// `Settled` and `Refused` only.
    [<RequireQualifiedAccess>]
    type OrderContextView =
        | NoPatient
        | Settled of OrderContext
        | Refused of OrderContext * OrderContextRefusal
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
                Refusal = None
            }


        let emptyFor = OrderContextWorkbench.emptyFor


        /// The first evaluation for the patient under way, over the empty context, held and shown.
        let opening (pat: Patient) (request: string) =
            {
                Workbench = OrderContextWorkbench.Evaluated(pat, emptyFor pat)
                InFlight = Some((OrderContextCommand.UpdateOrderContext, emptyFor pat), request)
                Pending = None
                Selected = None
                Refusal = None
            }


        /// The context held for the patient, nothing under way.
        let held (pat: Patient) (ctx: OrderContext) =
            {
                Workbench = OrderContextWorkbench.Evaluated(pat, ctx)
                InFlight = None
                Pending = None
                Selected = None
                Refusal = None
            }


        /// The context refused for the patient, as it was sent, with why; nothing under way.
        let refused (pat: Patient) (ctx: OrderContext) (refusal: OrderContextRefusal) =
            { held pat ctx with Refusal = Some refusal }


        /// A command under way over the context sent, always for the patient held; the context held
        /// is what a failed change goes back to.
        let changing (pat: Patient) (cmd: OrderContextCommand) (sent: OrderContext) (held: OrderContext) (request: string) =
            {
                Workbench = OrderContextWorkbench.Evaluated(pat, held)
                InFlight = Some((cmd, { sent with Patient = pat }), request)
                Pending = None
                Selected = None
                Refusal = None
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

            { state with
                Workbench = workbench
                InFlight = inFlight
                Pending = pending
            }


        /// The workbench as the page shows it: the context sent shown while a request is under way,
        /// the context refused with why while idle after a refusal.
        let view (state: OrderContextState) : OrderContextView =
            match state.Workbench, state.InFlight, state.Refusal with
            | OrderContextWorkbench.NoPatient, _, _ -> OrderContextView.NoPatient
            | OrderContextWorkbench.Evaluated _, Some((_, sent), _), _ -> OrderContextView.Changing sent
            | OrderContextWorkbench.Evaluated(_, ctx), None, Some refusal -> OrderContextView.Refused(ctx, refusal)
            | OrderContextWorkbench.Evaluated(_, ctx), None, None -> OrderContextView.Settled ctx


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
                        | OrderContextWorkbenchIntent.Tell errs -> state, [ OrderContextEffect.TellError errs ]

                    state, effects @ added
                )
                (state, [])


        /// The domain stage first, then the request stage: the workbench steps, and its intents
        /// become the request under way and the effects. The dialog follows: closed by a patient
        /// change, a seed and a reset, narrowed to what the workbench holds after an answer (a
        /// failed change keeps the context held, and the order with it; a refusal holds none),
        /// kept otherwise. The refusal follows the answer: held from the answer that refused,
        /// gone with any other answer, a patient change, a seed or a reset, kept while a command
        /// goes out. A patient cleared clears the request.
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
                    // typed over the context it was sent with; a refusal or a failure drops it
                    match result, state.Pending, landed.Workbench with
                    | Ok(OrderContextResponse.Evaluated _), Some(cmd, ctx, next), OrderContextWorkbench.Evaluated(_, answered) ->
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
            // new patient, the one held stays what a failed change goes back to, the dialog closes,
            // the refusal is gone
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


// ---------------------------------------------------------------------------------------------
// Tests: the fixtures of OrderContextMachineTests.fs, the two start-over tests rewritten, and
// the refusal's own.
// ---------------------------------------------------------------------------------------------

open Shared.Types
open Shared.Api
open OrderContextMachine


module Fixtures =

    let ten = { Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }
    let draft = { Shared.Models.Patient.empty with Age = Some ten }

    let asPatient (dto: Patient) =
        match dto |> Shared.Models.Patient.validate with
        | Ok pat -> pat
        | Error err -> invalidOp $"the fixture is no patient: %A{err}"

    let patient = draft |> asPatient
    let other = { draft with Department = Some "other" } |> asPatient
    let empty = OrderContextState.emptyFor patient
    let paracetamol = { empty with OrderContext.Filter.Generic = Some "paracetamol" }

    let withOrder =
        { paracetamol with
            Scenarios =
                [|
                    Informedica.GenPRES.Client.Core.Tests.OrderPlanMachineTests.Fixtures.scenario "o-1" "paracetamol"
                |]
        }

    let held = OrderContextState.held patient
    let refused = OrderContextState.refused patient
    let evaluating = OrderContextState.changing patient OrderContextCommand.UpdateOrderContext
    let opening = OrderContextState.opening

    let restored (ctx: OrderContext) errs =
        [
            OrderContextEffect.TellError errs
            OrderContextEffect.SyncFormulary ctx.Filter
            OrderContextEffect.SyncParenteralia ctx.Filter
        ]

    let evaluated (ctx: OrderContext) request =
        [
            OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, ctx, request)
            OrderContextEffect.SyncFormulary ctx.Filter
            OrderContextEffect.SyncParenteralia ctx.Filter
        ]

    let transition = OrderContextState.transition
    let view = OrderContextState.view
    let dialog = OrderContextState.dialog

    let evaluatedAnswer ctx = Ok(OrderContextResponse.Evaluated ctx)
    let refusedAnswer ctx r = Ok(OrderContextResponse.Refused(ctx, r))


open Fixtures


let tests =
    testList
        "a refused answer"
        [
            test "an evaluated answer is shown" {
                transition (OrderContextMsg.Answered("r-1", evaluatedAnswer paracetamol)) (evaluating paracetamol empty "r-1")
                |> Expect.equal "shown" (held paracetamol, [])
            }

            test "a refused answer keeps the picks, drops the scenarios, holds why, and tells nothing" {
                let busy = evaluating withOrder empty "r-1"

                transition (OrderContextMsg.Answered("r-1", refusedAnswer withOrder OrderContextRefusal.NoDoseRules)) busy
                |> Expect.equal
                    "refused, as sent, without scenarios"
                    (refused paracetamol OrderContextRefusal.NoDoseRules, [])
            }

            test "a refused first evaluation holds the empty context and why" {
                transition (OrderContextMsg.Answered("r-1", refusedAnswer empty OrderContextRefusal.NoProducts)) (opening patient "r-1")
                |> Expect.equal "the empty workbench, refused" (refused empty OrderContextRefusal.NoProducts, [])
            }

            test "the page shows the refusal while idle, a change while a request runs" {
                let shown = refused paracetamol OrderContextRefusal.NoDoseRulesForPatient

                shown
                |> view
                |> Expect.equal "refused" (OrderContextView.Refused(paracetamol, OrderContextRefusal.NoDoseRulesForPatient))

                let again = { paracetamol with OrderContext.Filter.Route = Some "or" }

                let busy, effects =
                    transition (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, again, "r-2")) shown

                effects |> Expect.equal "evaluated again" (evaluated again "r-2")
                busy |> view |> Expect.equal "changing meanwhile" (OrderContextView.Changing again)
            }

            test "the next evaluated answer clears the refusal" {
                let shown = refused paracetamol OrderContextRefusal.NoDoseRules
                let again = { paracetamol with OrderContext.Filter.Route = Some "or" }

                let busy, _ =
                    transition (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, again, "r-2")) shown

                transition (OrderContextMsg.Answered("r-2", evaluatedAnswer again)) busy
                |> Expect.equal "settled" (held again, [])
            }

            test "a failure after a refusal restores the context refused, the refusal gone" {
                let shown = refused paracetamol OrderContextRefusal.NoDoseRules
                let again = { paracetamol with OrderContext.Filter.Route = Some "or" }

                let busy, _ =
                    transition (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, again, "r-2")) shown

                transition (OrderContextMsg.Answered("r-2", Error [| "not loaded" |])) busy
                |> Expect.equal "as found, told" (held paracetamol, restored paracetamol [| "not loaded" |])
            }

            test "a patient change, a seed and a reset clear the refusal" {
                let shown = refused paracetamol OrderContextRefusal.NoDoseRules

                transition (OrderContextMsg.PatientChanged(Some other, "r-2")) shown
                |> fst
                |> view
                |> Expect.equal "changing for the other patient" (OrderContextView.Changing { paracetamol with Patient = other })

                transition (OrderContextMsg.Seed(empty, "r-2")) shown
                |> fst
                |> view
                |> Expect.equal "changing over the seed" (OrderContextView.Changing empty)

                transition (OrderContextMsg.Reset "r-2") shown
                |> fst
                |> view
                |> Expect.equal "changing over the empty context" (OrderContextView.Changing empty)

                transition (OrderContextMsg.PatientChanged(None, "r-2")) shown
                |> fst
                |> view
                |> Expect.equal "no patient" OrderContextView.NoPatient
            }

            test "a refusal drops the dialog's pending command and its selection" {
                let busy =
                    evaluating withOrder withOrder "r-1"
                    |> OrderContextState.select (Some "o-1")
                    |> OrderContextState.pending
                        OrderContextCommand.SetMedianOrderableDoseQuantityProperty
                        withOrder
                        "r-2"

                let landed, effects =
                    transition (OrderContextMsg.Answered("r-1", refusedAnswer withOrder OrderContextRefusal.NoDoseRules)) busy

                effects |> Expect.isEmpty "nothing goes out"
                landed |> dialog |> Expect.equal "the dialog closed" None
                landed |> view |> Expect.equal "refused" (OrderContextView.Refused(paracetamol, OrderContextRefusal.NoDoseRules))
            }

            test "a failed change still keeps the context held and the order, and says why" {
                let busy = evaluating withOrder withOrder "r-1" |> OrderContextState.select (Some "o-1")

                transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) busy
                |> fst
                |> dialog
                |> Expect.equal "kept" (Some(OrderContextView.Settled withOrder))
            }

            test "a stale answer lands nowhere, refused or not" {
                let shown = held paracetamol

                transition (OrderContextMsg.Answered("r-9", refusedAnswer empty OrderContextRefusal.NoDoseRules)) shown
                |> Expect.equal "stale" (shown, [])
            }
        ]


runTestsWithCLIArgs [] [||] tests
