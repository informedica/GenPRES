// Migrated to ArgumentationPolicy.fs, the two machines and their tests on 2026-09-28; kept as the draft.
//
// #1160, found in the browser check of step 9 of the plan for #985: a reset of the order in the
// dose dialog put the dose back within what the rules allow and left the argumentation under
// it, since the machines keep the text over every answer (#1158). The reset's answer is now the
// one that clears it: ArgumentationPolicy.clearedBy names the command, keepFor decides for a
// context and keepAllFor for a plan (the context a Navigate with a reset went into), and the two
// machines call them where they kept the text. The dialog's draft follows the context, so the
// field empties, and goes away when the reset order is no longer marked.
//
// The modules below are ArgumentationPolicy.fs, OrderPlanMachine.fs and OrderContextMachine.fs
// as they become, generated from the source files with the edits applied, shadowing the ones
// load.fsx loads. The tests at the end migrate to ArgumentationPolicyTests.fs,
// OrderPlanMachineTests.fs and OrderContextMachineTests.fs.
//
// Run: `dotnet fsi ArgumentationReset.fsx` from this directory, after `dotnet run build`.

#I __SOURCE_DIRECTORY__
#load "load.fsx"
#r "nuget: Expecto"
#load "../../../tests/Informedica.GenPRES.Client.Core.Tests/OrderPlanMachineTests.fs"

open Expecto
open Expecto.Flip


/// The argumentation a clinician writes when a dose leaves what the rules allow, on the client:
/// when the dialog asks for it, how a text is taken, and that the text is the client's own,
/// kept over every answer. Pure F#, no React, so it runs under Expecto; the two machines carry
/// the messages that write it, the dose dialog shows the field.
module ArgumentationPolicy =

    open Shared.Types
    open Shared.Models
    open Shared.Api


    /// Every order variable of an order: the adjust and the duration, the schedule's two, the
    /// orderable's four and its dose's eight, then each component's six and its dose's eight,
    /// and each item's four and its dose's eight.
    let variables (ord: Order) : OrderVariable list =
        let dose (d: Dose) =
            [
                d.Quantity
                d.PerTime
                d.Rate
                d.Total
                d.QuantityAdjust
                d.PerTimeAdjust
                d.RateAdjust
                d.TotalAdjust
            ]

        let item (i: Item) =
            [
                i.ComponentQuantity
                i.OrderableQuantity
                i.ComponentConcentration
                i.OrderableConcentration
            ]
            @ dose i.Dose

        let comp (c: Component) =
            [
                c.ComponentQuantity
                c.OrderableQuantity
                c.OrderableCount
                c.OrderQuantity
                c.OrderCount
                c.OrderableConcentration
            ]
            @ dose c.Dose
            @ (c.Items |> Array.toList |> List.collect item)

        let orb = ord.Orderable

        [ ord.Adjust; ord.Duration; ord.Schedule.Frequency; ord.Schedule.Time ]
        @ [ orb.OrderableQuantity; orb.OrderQuantity; orb.OrderCount; orb.DoseCount ]
        @ dose orb.Dose
        @ (orb.Components |> Array.toList |> List.collect comp)


    /// Whether the rules mark the order: any of its variables carries a severity reason.
    let marked (ord: Order) =
        variables ord |> List.exists (SeverityReason.ofOrderVariable >> Option.isSome)


    /// Whether the dialog asks for the argumentation: the context is narrowed to one scenario
    /// whose order the rules mark, or a text is present, which stays once written until the
    /// user clears it.
    let wanted (ctx: OrderContext) =
        ctx.Argumentation.IsSome
        || (ctx |> OrderContext.contribution |> Option.exists (fun sc -> marked sc.Order))


    /// The most characters a text keeps: the server's cap, the same number, so that a text the
    /// client holds is never one the server refuses. The dialog's field carries it too.
    let maxLength = 1000


    /// The text as the client keeps it: trimmed, empty is none, and clipped at the cap.
    let normalise (text: string) =
        match text with
        | null -> None
        | text ->
            let text = text.Trim()

            if text = "" then
                None
            elif text.Length > maxLength then
                Some(text.Substring(0, maxLength))
            else
                Some text


    /// The context with the text written, normalised.
    let write (text: string) (ctx: OrderContext) = { ctx with Argumentation = normalise text }


    /// The answered context with the argumentation as it was sent: the text is the client's own,
    /// never the server's to change.
    let keep (sent: OrderContext) (answered: OrderContext) = { answered with Argumentation = sent.Argumentation }


    /// The plan with the text written on the context named; a plan without it is unchanged.
    let writeIn (id: string) (text: string) (plan: OrderPlan) =
        { plan with OrderContexts = plan.OrderContexts |> Array.map (fun c -> if c.Id = id then write text c else c) }


    /// The answered plan with the argumentation the client holds on every context it held; a
    /// context the client did not hold keeps the answer's.
    let keepAll (held: OrderPlan) (answered: OrderPlan) =
        { answered with
            OrderContexts =
                answered.OrderContexts
                |> Array.map (fun c ->
                    held.OrderContexts
                    |> Array.tryFind (fun h -> h.Id = c.Id)
                    |> Option.map (fun h -> keep h c)
                    |> Option.defaultValue c
                )
        }


    /// The one command whose answer does not keep the text: a reset puts the order back within
    /// what the rules allow, and the text argues the deviation it undoes.
    let clearedBy (cmd: OrderContextCommand) =
        match cmd with
        | OrderContextCommand.ResetOrderScenario -> true
        | _ -> false


    /// The answered context after the command: cleared of its argumentation after a reset, else
    /// with the argumentation as it was sent.
    let keepFor (cmd: OrderContextCommand) (sent: OrderContext) (answered: OrderContext) =
        if clearedBy cmd then
            { answered with
                Argumentation = None
            }
        else
            keep sent answered


    /// The answered plan after the command: the context a reset navigated into cleared of its
    /// argumentation, every other context with the argumentation the client holds.
    let keepAllFor (sent: OrderPlanCommand) (held: OrderPlan) (answered: OrderPlan) =
        let kept = keepAll held answered

        match sent with
        | OrderPlanCommand.Navigate(_, id, cmd, _) when clearedBy cmd ->
            { kept with
                OrderContexts =
                    kept.OrderContexts
                    |> Array.map (fun c ->
                        if c.Id = id then
                            { c with
                                Argumentation = None
                            }
                        else
                            c
                    )
            }
        | _ -> kept


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
module OrderPlanMachine =

    open Shared.Types
    open Shared.Models
    open Shared.Api
    open PlanWorkPolicy


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


        /// The plan answered, with the argumentation the client holds on every context it held,
        /// since the text is the client's own and an answer computed over an earlier text does not
        /// take it back, except the context a reset navigated into, which the reset clears; its
        /// drugs checked; an order prescribed opens the plan page and clears the workbench.
        let private answered (pat: Patient) (held: OrderPlan) (sent: OrderPlanCommand) (tp: OrderPlan) =
            let tp = tp |> ArgumentationPolicy.keepAllFor sent held

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
            | OrderPlanCartMsg.Landed(sent, Ok tp), OrderPlanCart.Opened(pat, held) -> answered pat held sent tp
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
    /// request is under way), the context the dialog shows, by id, and the plan's work since the
    /// version last opened or signed: the client's own, next to whatever is in flight. Built through the constructors below only, which admit the
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
                // what the plan holds beside the version last opened or signed: stepped by the
                // commands as they go out, as signed again by a version opened or a signature; the
                // leave-page guard asks over it
                Work: PlanWork
                // the contexts of the version last opened or signed, as the open answered them: an
                // order of the plan is new or changed against these, and holds the patient context
                Opened: OrderContext[]
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
        // a signature told: the plan took no change from a page while it was under way, so it is
        // the version just signed
        | Signed
        // the argumentation written on a context of the plan, by id: the client's own, no call; a
        // text that changes is work, as a command that goes out is
        | Argue of contextId: string * text: string


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
                Work = PlanWork.AsSigned
                Opened = [||]
            }


        /// No patient yet: the contexts of the version the Session opened with, kept for the
        /// patient on its way, nothing under way, the dialog closed.
        let awaiting (contexts: OrderContext[]) =
            {
                Cart = OrderPlanCart.NoPatient contexts
                InFlight = None
                Pending = None
                Selected = None
                Work = PlanWork.AsSigned
                Opened = [||]
            }


        /// An open under way over the empty plan held and shown: the contexts being opened travel in
        /// the request, the dialog closed.
        let opening (pat: Patient) (contexts: OrderContext[]) (request: string) =
            {
                Cart = OrderPlanCart.Opened(pat, OrderPlan.create pat [||])
                InFlight = Some(OrderPlanCommand.Open(pat, contexts), request)
                Pending = None
                Selected = None
                Work = PlanWork.AsSigned
                Opened = [||]
            }


        /// The plan held for the patient with the dialog's selection, nothing under way: the
        /// version opened.
        let held (pat: Patient) (tp: OrderPlan) (selected: string option) =
            {
                Cart = OrderPlanCart.Opened(pat, tp)
                InFlight = None
                Pending = None
                Selected = selected
                Work = PlanWork.AsSigned
                Opened = tp.OrderContexts
            }


        /// A change under way over the plan held, the one a failed change goes back to: the
        /// version opened.
        let changing (pat: Patient) (tp: OrderPlan) (selected: string option) (sent: OrderPlanCommand) (request: string) =
            {
                Cart = OrderPlanCart.Opened(pat, tp)
                InFlight = Some(sent, request)
                Pending = None
                Selected = selected
                Work = PlanWork.AsSigned
                Opened = tp.OrderContexts
            }


        /// A change under way with the dialog's step waiting on its answer, under its own request
        /// id; only on a change under way.
        let pending (cmd: OrderPlanCommand) (request: string) (state: OrderPlanState) =
            { state with Pending = Some(cmd, request) }


        /// The plan's work since the version last opened or signed, on a plan held or changing.
        let withWork (work: PlanWork) (state: OrderPlanState) = { state with Work = work }


        /// What the plan holds beside the version last opened or signed.
        let work (state: OrderPlanState) = state.Work


        /// The contexts of the version last opened or signed, on a plan held or changing.
        let withOpened (opened: OrderContext[]) (state: OrderPlanState) = { state with Opened = opened }


        /// The ids of the plan's contexts that are new or changed since the version last opened or
        /// signed; none without a patient.
        let changed (state: OrderPlanState) =
            match state.Cart with
            | OrderPlanCart.NoPatient _ -> [||]
            | OrderPlanCart.Opened(_, tp) -> HeldContextPolicy.changed state.Opened tp


        /// Whether the patient context is held: the plan has a new or changed order.
        let contextHeld (state: OrderPlanState) = changed state |> Array.isEmpty |> not


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
        /// command carries, since the rows chosen show at once, with the argumentation the plan
        /// held has meanwhile, since a text written while it runs is kept; the plan held otherwise.
        let meanwhile (tp: OrderPlan) (sent: OrderPlanCommand) =
            match sent with
            | OrderPlanCommand.Recalculate shown -> shown |> ArgumentationPolicy.keepAll tp
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
                        // the one place a command goes out: a change that goes out is work until the
                        // next version is opened or signed; the step that waited counts here when it
                        // goes out on the answer, and never if it is dropped meanwhile
                        | OrderPlanCartIntent.Call cmd ->
                            call cmd { state with Work = state.Work |> PlanWork.afterCommand cmd }
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

                    // an open that landed is the version opened, as the server answered it
                    let landed =
                        match sent, result with
                        | OrderPlanCommand.Open _, Ok tp -> { landed with Opened = tp.OrderContexts }
                        | _ -> landed

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

            // a version opened is the plan as signed, and so is no plan at all: without a patient the
            // plan is dropped, nothing left to sign. A command counts as work where it goes out, in
            // the request stage
            | OrderPlanMsg.PatientChanged(None, request) ->
                run
                    request
                    (OrderPlanCartMsg.PatientChanged None)
                    { state with
                        Work = PlanWork.AsSigned
                        Opened = [||]
                    }
            | OrderPlanMsg.Version(head, request) ->
                run
                    request
                    (OrderPlanCartMsg.Version head)
                    { state with
                        Work = PlanWork.AsSigned
                        Opened = [||]
                    }
            | OrderPlanMsg.Command(cmd, request) -> run request (OrderPlanCartMsg.Command cmd) state
            | OrderPlanMsg.Filter(ids, request) -> run request (OrderPlanCartMsg.Filter ids) state
            // the argumentation written on a context of the plan: the client's own, no call, next to
            // whatever is in flight; a text that changes is work, an unchanged one nothing
            | OrderPlanMsg.Argue(id, text) ->
                match state.Cart with
                | OrderPlanCart.Opened(pat, tp) ->
                    let written = tp |> ArgumentationPolicy.writeIn id text

                    if written = tp then
                        state, []
                    else
                        { state with
                            Cart = OrderPlanCart.Opened(pat, written)
                            Work = PlanWork.Changed
                        },
                        []
                | OrderPlanCart.NoPatient _ -> state, []
            // the plan is the version just signed, its contexts the ones kept
            | OrderPlanMsg.Signed ->
                let opened =
                    match state.Cart with
                    | OrderPlanCart.Opened(_, tp) -> tp.OrderContexts
                    | OrderPlanCart.NoPatient _ -> state.Opened

                { state with
                    Work = PlanWork.AsSigned
                    Opened = opened
                },
                []


        /// Whether the message reaches the plan: a change from a page does not while a signature is
        /// under way, so that the signing act is atomic on the client too; the answer to a request
        /// under way, the patient, a version opened, the selection and the signature told do.
        let admitted (signing: SigningMachine.SigningView) (msg: OrderPlanMsg) =
            match msg with
            | OrderPlanMsg.Command _
            | OrderPlanMsg.Filter _
            | OrderPlanMsg.Argue _ -> not (SigningPolicy.underWay signing)
            | OrderPlanMsg.PatientChanged _
            | OrderPlanMsg.Version _
            | OrderPlanMsg.Answered _
            | OrderPlanMsg.Select _
            | OrderPlanMsg.Signed -> true


        /// The transition behind the signing: a message not admitted leaves the plan as it is.
        let transitionWhile (signing: SigningMachine.SigningView) (msg: OrderPlanMsg) (state: OrderPlanState) =
            if admitted signing msg then
                transition msg state
            else
                state, []


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
            // an answer lands for the patient held, with the argumentation as it was sent: the text
            // is the client's own, and an answer computed over an earlier text does not take it
            // back; a reset's answer is the one that clears it
            | OrderContextWorkbenchMsg.Landed((cmd, sent), Ok(OrderContextResponse.Evaluated ctx)),
              OrderContextWorkbench.Evaluated(pat, _) ->
                OrderContextWorkbench.Evaluated(pat, ctx |> ArgumentationPolicy.keepFor cmd sent), []
            | OrderContextWorkbenchMsg.Landed((cmd, sent), Ok(OrderContextResponse.Refused(back, _))),
              OrderContextWorkbench.Evaluated(pat, _) -> refused pat (back |> ArgumentationPolicy.keepFor cmd sent)
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
        // the argumentation written on the workbench: the client's own, no request; written on the
        // context held and on the one sent, so that the answer under way keeps it
        | Argue of string


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

            // the selection is the client's own, kept next to whatever is in flight
            | OrderContextMsg.Select id, _, _ -> select id state, []

            // the argumentation is the client's own too: written on the context held, the one sent
            // and the one pending alike, the request under way kept
            | OrderContextMsg.Argue text, _, _ -> map (ArgumentationPolicy.write text) state, []

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
// Tests
// ---------------------------------------------------------------------------------------------

open Shared.Types
open Shared.Api
open Informedica.GenPRES.Client.Core.Tests.OrderPlanMachineTests.Fixtures


let text = "Sepsis, hogere dosis in overleg met de apotheek"


let policyTests =
    testList
        "ArgumentationPolicy, the reset"
        [
            test "the reset is the one command whose answer clears the text" {
                ArgumentationPolicy.clearedBy OrderContextCommand.ResetOrderScenario |> Expect.isTrue "the reset"

                [
                    OrderContextCommand.UpdateOrderContext
                    OrderContextCommand.SelectOrderScenario
                    OrderContextCommand.UpdateOrderScenario
                    OrderContextCommand.IncreaseOrderableDoseQuantityProperty(1, false)
                ]
                |> List.exists ArgumentationPolicy.clearedBy
                |> Expect.isFalse "no other"
            }

            test "keepFor: cleared after a reset, as sent after any other command" {
                let sent = { context "c-1" "paracetamol" with Argumentation = Some text }
                let answered = { sent with Argumentation = Some "the server's echo" }

                (answered |> ArgumentationPolicy.keepFor OrderContextCommand.ResetOrderScenario sent).Argumentation
                |> Expect.isNone "cleared"

                (answered |> ArgumentationPolicy.keepFor OrderContextCommand.UpdateOrderScenario sent).Argumentation
                |> Expect.equal "as sent" (Some text)
            }

            test "keepAllFor: the context a reset navigated into is cleared, the others keep the client's" {
                let held = two |> ArgumentationPolicy.writeIn "c-1" text |> ArgumentationPolicy.writeIn "c-2" "other"
                let reset = OrderPlanCommand.Navigate(held, "c-1", OrderContextCommand.ResetOrderScenario, context "c-1" "p")
                let step = OrderPlanCommand.Navigate(held, "c-1", OrderContextCommand.UpdateOrderScenario, context "c-1" "p")

                (two |> ArgumentationPolicy.keepAllFor reset held).OrderContexts
                |> Array.map _.Argumentation
                |> Expect.equal "c-1 cleared, c-2 kept" [| None; Some "other" |]

                (two |> ArgumentationPolicy.keepAllFor step held).OrderContexts
                |> Array.map _.Argumentation
                |> Expect.equal "both kept" [| Some text; Some "other" |]

                (two |> ArgumentationPolicy.keepAllFor (OrderPlanCommand.Recalculate two) held).OrderContexts
                |> Array.map _.Argumentation
                |> Expect.equal "a recalculation keeps both" [| Some text; Some "other" |]
            }
        ]


module Ctx = OrderContextMachine.OrderContextState


let workbenchTests =
    let ctx =
        { Shared.Models.OrderContext.empty with
            Patient = patient
            OrderContext.Filter.Generic = Some "paracetamol"
        }

    let argued = ctx |> ArgumentationPolicy.write text

    testList
        "OrderContextMsg, the reset's answer"
        [
            test "a reset's answer clears the text, though the server echoes it; a step's answer keeps it" {
                let resetting = Ctx.changing patient OrderContextCommand.ResetOrderScenario argued argued "r-1"

                let landed, _ =
                    resetting
                    |> Ctx.transition (OrderContextMachine.OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated argued)))

                landed |> Ctx.view |> Expect.equal "cleared" (OrderContextMachine.OrderContextView.Settled ctx)

                let stepping = Ctx.changing patient OrderContextCommand.UpdateOrderScenario argued argued "r-2"

                let landed, _ =
                    stepping
                    |> Ctx.transition (OrderContextMachine.OrderContextMsg.Answered("r-2", Ok(OrderContextResponse.Evaluated ctx)))

                landed |> Ctx.view |> Expect.equal "kept" (OrderContextMachine.OrderContextView.Settled argued)
            }
        ]


module Plan = OrderPlanMachine.OrderPlanState


let planTests =
    testList
        "OrderPlanMsg, the reset's answer"
        [
            test "a reset navigated into a context clears its text, the other context keeps its own" {
                let held = two |> ArgumentationPolicy.writeIn "c-1" text |> ArgumentationPolicy.writeIn "c-2" "other"
                let reset = OrderPlanCommand.Navigate(held, "c-1", OrderContextCommand.ResetOrderScenario, context "c-1" "p")
                let busy = Plan.changing patient held (Some "c-1") reset "r-1"

                let landed, _ = busy |> Plan.transition (OrderPlanMachine.OrderPlanMsg.Answered("r-1", Ok held))

                match landed |> Plan.view with
                | OrderPlanMachine.OrderPlanView.Settled(tp, _) ->
                    tp.OrderContexts |> Array.map _.Argumentation |> Expect.equal "c-1 cleared" [| None; Some "other" |]
                | other -> failtest $"expected settled, got %A{other}"
            }
        ]


runTestsWithCLIArgs [] [||] (testList "the reset clears the argumentation" [ policyTests; workbenchTests; planTests ])
