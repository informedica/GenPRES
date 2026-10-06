/// Tracks the order plan from no patient to an open plan, through changes and reopens. The App
/// carries out the effects. The machine has two stages: the plan itself, which knows no request,
/// and the one request under way. An answer passes the request stage first and reaches the plan
/// only when it lands; a change passes the plan first and reaches the request stage as an intent.
///
/// Four invariants:
/// - one request is in flight at a time; a change sent meanwhile is dropped, and the pages grey
///   their controls;
/// - an answer lands only on the request it names, so an older answer never replaces a newer
///   plan;
/// - a patient change and a reopen start a new request that replaces the one in flight;
/// - a failed change leaves the plan as it was, so the next action is the retry.
module OrderPlanMachine

open Shared.Types
open Shared.Models
open Shared.Api
open PlanWorkPolicy


/// The plan itself, without any request under way.
[<RequireQualifiedAccess>]
type OrderPlanCart =
    /// No patient. The contexts of the session's version wait here until the patient arrives,
    /// since the version arrives first; empty when there is no version.
    | NoPatient of awaiting: OrderContext[]
    /// The plan as answered, which a failed change goes back to. While an open is under way, this
    /// is the empty plan and the contexts travel in the request.
    | Opened of Patient * OrderPlan


/// What moves the plan stage.
[<RequireQualifiedAccess>]
type OrderPlanCartMsg =
    /// The patient set, changed or cleared.
    | PatientChanged of Patient option
    /// The version the session opened with.
    | Version of SignedOrderPlan
    /// A change from a page.
    | Command of OrderPlanCommand
    /// The answer that landed, with the command that was sent.
    | Landed of sent: OrderPlanCommand * Result<OrderPlan, string[]>
    /// The contexts the rows keep, by id.
    | Filter of string[]


/// What the plan stage asks of the request stage, which turns it into calls and effects.
[<RequireQualifiedAccess>]
type OrderPlanCartIntent =
    /// Open the plan for the patient, empty or with a version's contexts; replaces any request.
    | Open of Patient * OrderContext[]
    /// Recalculate the plan for a changed patient; replaces any request.
    | Recalculate of OrderPlan
    /// Send a change from a page; one at a time.
    | Call of OrderPlanCommand
    /// Check the plan's drugs for interactions; fewer than two clears the warnings.
    | CheckInteractions of string list
    /// Open the plan page, after an order was prescribed.
    | GoToPlanPage
    /// Clear the prescribing workbench, after an order was prescribed.
    | ResetWorkbench
    /// Tell the user what went wrong.
    | Tell of string[]


/// The order dialog's commands, the same in both order machines: a step, a typed value or a
/// reset. None goes out while a request is under way: the dialog's fields rest until the answer.
module Dialog =

    /// The context as the command changes it, shown while its request is under way; the context
    /// itself when the command cannot change it.
    let shown (cmd: OrderContextCommand) (ctx: OrderContext) =
        match OrderContextCommand.preview cmd ctx with
        | Ok(_, changed) -> changed
        | Error _ -> ctx


/// The plan stage.
module OrderPlanCart =

    /// The check of the plan's drugs for interactions, always asked, so a plan down to one drug
    /// clears the old warnings.
    let interactions (tp: OrderPlan) =
        Shared.Models.OrderPlan.orders tp
        |> Array.map _.Name
        |> Array.distinct
        |> Array.toList
        |> OrderPlanCartIntent.CheckInteractions
        |> List.singleton


    /// A command from a page, over the plan the machine holds, since the page's copy may be
    /// behind. A recalculation keeps the page's plan, which carries its filter.
    let rebase (tp: OrderPlan) (cmd: OrderPlanCommand) =
        match cmd with
        | OrderPlanCommand.Recalculate _
        | OrderPlanCommand.Open _ -> cmd
        | OrderPlanCommand.AddOrderContext(_, ctx) -> OrderPlanCommand.AddOrderContext(tp, ctx)
        | OrderPlanCommand.NewOrderContext(_, category) -> OrderPlanCommand.NewOrderContext(tp, category)
        | OrderPlanCommand.Navigate(_, contextId, ctxCmd, ctx) -> OrderPlanCommand.Navigate(tp, contextId, ctxCmd, ctx)
        | OrderPlanCommand.RemoveOrderContexts(_, ids) -> OrderPlanCommand.RemoveOrderContexts(tp, ids)


    /// The plan answered, with the client's argumentation kept; its drugs are checked, and a
    /// prescribed order opens the plan page and clears the workbench.
    let private answered (pat: Patient) (held: OrderPlan) (sent: OrderPlanCommand) (tp: OrderPlan) =
        let tp = tp |> ArgumentationPolicy.keepAll held

        let prescribed =
            match sent with
            | OrderPlanCommand.AddOrderContext _ ->
                [ OrderPlanCartIntent.GoToPlanPage; OrderPlanCartIntent.ResetWorkbench ]
            | _ -> []

        OrderPlanCart.Opened(pat, tp), interactions tp @ prescribed


    /// The plan stage: the plan changes only on the patient and on an answer that landed;
    /// everything else becomes an intent for the request stage.
    let step (msg: OrderPlanCartMsg) (plan: OrderPlanCart) : OrderPlanCart * OrderPlanCartIntent list =
        match msg, plan with
        // no patient, no plan
        | OrderPlanCartMsg.PatientChanged None, _ -> OrderPlanCart.NoPatient [||], []

        // the first plan for a patient: the empty plan is shown while the waiting version, or an
        // empty plan, is opened
        | OrderPlanCartMsg.PatientChanged(Some pat), OrderPlanCart.NoPatient awaiting ->
            OrderPlanCart.Opened(pat, OrderPlan.create pat [||]), [ OrderPlanCartIntent.Open(pat, awaiting) ]
        // the plan follows the patient and is recalculated; an open under way is handled in
        // transition
        | OrderPlanCartMsg.PatientChanged(Some pat), OrderPlanCart.Opened(_, tp) ->
            let tp = { tp with Patient = pat }
            OrderPlanCart.Opened(pat, tp), [ OrderPlanCartIntent.Recalculate tp ]

        // the signed version replaces the plan; before the patient arrives, its contexts wait
        | OrderPlanCartMsg.Version head, OrderPlanCart.NoPatient _ -> OrderPlanCart.NoPatient head.OrderContexts, []
        | OrderPlanCartMsg.Version head, OrderPlanCart.Opened(pat, _) ->
            OrderPlanCart.Opened(pat, OrderPlan.create pat [||]), [ OrderPlanCartIntent.Open(pat, head.OrderContexts) ]

        // a reset clears the context's argumentation when it is sent, so a text written while it
        // runs is kept
        | OrderPlanCartMsg.Command(OrderPlanCommand.Navigate(_, id, ctxCmd, ctx)), OrderPlanCart.Opened(pat, tp) when
            ArgumentationPolicy.clearedBy ctxCmd
            ->
            let tp = tp |> ArgumentationPolicy.clearIn id
            let cmd = OrderPlanCommand.Navigate(tp, id, ctxCmd, ArgumentationPolicy.clear ctx)

            OrderPlanCart.Opened(pat, tp), [ OrderPlanCartIntent.Call cmd ]
        | OrderPlanCartMsg.Command cmd, OrderPlanCart.Opened(_, tp) -> plan, [ OrderPlanCartIntent.Call(rebase tp cmd) ]
        | OrderPlanCartMsg.Command _, _ -> plan, []

        // nothing is asked without a patient, so nothing lands
        | OrderPlanCartMsg.Landed _, OrderPlanCart.NoPatient _ -> plan, []
        // an answer lands for the patient held
        | OrderPlanCartMsg.Landed(sent, Ok tp), OrderPlanCart.Opened(pat, held) -> answered pat held sent tp
        // a failed change leaves the plan as it was; after a failed open, that is the empty plan
        | OrderPlanCartMsg.Landed(_, Error errs), OrderPlanCart.Opened _ -> plan, [ OrderPlanCartIntent.Tell errs ]

        // the rows chosen: the totals are recalculated over them; the rows travel in the command
        | OrderPlanCartMsg.Filter ids, OrderPlanCart.Opened(_, tp) ->
            plan,
            [
                OrderPlanCartIntent.Call(OrderPlanCommand.Recalculate { tp with Filtered = ids })
            ]
        | OrderPlanCartMsg.Filter _, _ -> plan, []


/// Everything the order plan machine holds, hidden from the pages, which read an OrderPlanView of
/// it instead.
type OrderPlanState =
    private
        {
            /// The plan itself.
            Cart: OrderPlanCart
            /// The command under way and the request id its answer must name.
            InFlight: (OrderPlanCommand * string) option
            /// The id of the context the dialog shows.
            Selected: string option
            /// Whether the plan changed since the version last opened or signed; the leave-page
            /// guard reads it.
            Work: PlanWork
            /// The contexts of the version last opened or signed; an order is new or changed
            /// against these.
            Opened: OrderContext[]
            /// The state before a reopen from the dialog, put back when its list closes without a
            /// pick; None when no reopen is looked at.
            Kept: OrderPlanState option
        }


/// What moves the order plan machine. A message that starts a request carries its request id, so
/// the answer can name it.
[<RequireQualifiedAccess>]
type OrderPlanMsg =
    /// The patient set, changed or cleared; the plan follows.
    | PatientChanged of Patient option * request: string
    /// The version the session opened with; it replaces the plan and any request.
    | Version of SignedOrderPlan * request: string
    /// A change to the plan from a page.
    | Command of OrderPlanCommand * request: string
    /// The answer to the request with this id; Error is a failure of the server or the call.
    | Answered of request: string * Result<OrderPlan, string[]>
    /// The context the dialog shows, by id.
    | Select of string option
    /// The contexts the rows keep, by id; the totals follow.
    | Filter of string[] * request: string
    /// A clear from the dialog that opens the field's list: the command goes out as a change, and
    /// the plan before it is kept to be put back.
    | Reopen of OrderPlanCommand * request: string
    /// The list of a reopen closed without a pick: the plan kept is put back, with whether it had
    /// changed since the version last opened or signed, and the answer to the clear is dropped.
    | Restore
    /// The plan was signed; it took no change meanwhile, so it is the version signed.
    | Signed
    /// The argumentation written on a context, by id. No request; a changed text counts as a
    /// change to the plan.
    | Argue of contextId: string * text: string


/// What the App carries out for the order plan machine.
[<RequireQualifiedAccess>]
type OrderPlanEffect =
    /// Send the plan command under this request id.
    | CallPlan of OrderPlanCommand * request: string
    /// Check the drugs for interactions; fewer than two clears the warnings.
    | CheckInteractions of string list
    /// Open the plan page, after an order was prescribed.
    | GoToPlanPage
    /// Clear the prescribing workbench, after an order was prescribed.
    | ResetWorkbench
    /// Tell the user what went wrong.
    | TellError of string[]


/// What the pages read of the OrderPlanState. Pages render Settled and Changing alike, so the
/// screen stays filled during a request. The dialog steps from either; other commands are built
/// from Settled only.
[<RequireQualifiedAccess>]
type OrderPlanView =
    /// No patient, so no plan.
    | NoPatient
    /// The plan the server answered, with the context the dialog shows.
    | Settled of OrderPlan * selected: string option
    /// A change under way, with the plan shown meanwhile: for a recalculation the plan sent, so
    /// the rows chosen show at once; otherwise the plan held.
    | Changing of OrderPlan * selected: string option


/// Functions over OrderPlanView.
module OrderPlanView =

    /// Whether the plan holds the order with this id, so the prescribe button is not offered
    /// again. A context's own id is not the order's.
    let holds (orderId: string) (view: OrderPlanView) =
        match view with
        | OrderPlanView.Settled(tp, _)
        | OrderPlanView.Changing(tp, _) -> OrderPlan.orders tp |> Array.exists (fun sc -> sc.Order.Id = orderId)
        | OrderPlanView.NoPatient -> false


/// The constructors and the transition of the order plan machine.
module OrderPlanState =

    /// No patient, nothing under way.
    let noPatient =
        {
            Cart = OrderPlanCart.NoPatient [||]
            InFlight = None
            Selected = None
            Work = PlanWork.AsSigned
            Opened = [||]
            Kept = None
        }


    /// No patient yet, with the session's version waiting for it.
    let awaiting (contexts: OrderContext[]) =
        {
            Cart = OrderPlanCart.NoPatient contexts
            InFlight = None
            Selected = None
            Work = PlanWork.AsSigned
            Opened = [||]
            Kept = None
        }


    /// An open under way, with the empty plan shown; the contexts travel in the request.
    let opening (pat: Patient) (contexts: OrderContext[]) (request: string) =
        {
            Cart = OrderPlanCart.Opened(pat, OrderPlan.create pat [||])
            InFlight = Some(OrderPlanCommand.Open(pat, contexts), request)
            Selected = None
            Work = PlanWork.AsSigned
            Opened = [||]
            Kept = None
        }


    /// The plan of an opened version, nothing under way.
    let held (pat: Patient) (tp: OrderPlan) (selected: string option) =
        {
            Cart = OrderPlanCart.Opened(pat, tp)
            InFlight = None
            Selected = selected
            Work = PlanWork.AsSigned
            Opened = tp.OrderContexts
            Kept = None
        }


    /// A change under way over the plan of an opened version, which a failure goes back to.
    let changing (pat: Patient) (tp: OrderPlan) (selected: string option) (sent: OrderPlanCommand) (request: string) =
        {
            Cart = OrderPlanCart.Opened(pat, tp)
            InFlight = Some(sent, request)
            Selected = selected
            Work = PlanWork.AsSigned
            Opened = tp.OrderContexts
            Kept = None
        }


    /// The state with whether the plan changed since the version last opened or signed.
    let withWork (work: PlanWork) (state: OrderPlanState) = { state with Work = work }


    /// Whether the plan changed since the version last opened or signed.
    let work (state: OrderPlanState) = state.Work


    /// The state with the contexts of the version last opened or signed.
    let withOpened (opened: OrderContext[]) (state: OrderPlanState) = { state with Opened = opened }


    /// The ids of the contexts that are new or changed since the version last opened or signed.
    let changed (state: OrderPlanState) =
        match state.Cart with
        | OrderPlanCart.NoPatient _ -> [||]
        | OrderPlanCart.Opened(_, tp) -> HeldContextPolicy.changed state.Opened tp


    /// Whether the patient context is held: the plan has a new or changed order.
    let contextHeld (state: OrderPlanState) = changed state |> Array.isEmpty |> not


    /// The order contexts of the plan new, changed or removed since the version last opened or
    /// signed, as the sign dialog lists them; the plan is the one the dialog shows.
    let differences (plan: OrderPlan) (state: OrderPlanState) = HeldContextPolicy.differences state.Opened plan


    /// The plan, if there is a patient; the empty plan while an open runs.
    let plan (state: OrderPlanState) =
        match state.Cart with
        | OrderPlanCart.NoPatient _ -> None
        | OrderPlanCart.Opened(_, tp) -> Some tp


    /// The patient the plan is for, if any.
    let patient (state: OrderPlanState) =
        match state.Cart with
        | OrderPlanCart.NoPatient _ -> None
        | OrderPlanCart.Opened(pat, _) -> Some pat


    /// The dialog's selection, kept only while its context is in the plan.
    let selectionIn (tp: OrderPlan) (selected: string option) =
        selected
        |> Option.filter (fun id -> tp.OrderContexts |> Array.exists (fun c -> c.Id = id))


    /// The plan shown while a change is under way: for a recalculation the plan sent, with the
    /// argumentation written meanwhile; otherwise the plan held.
    let meanwhile (tp: OrderPlan) (sent: OrderPlanCommand) =
        match sent with
        | OrderPlanCommand.Recalculate shown -> shown |> ArgumentationPolicy.keepAll tp
        | _ -> tp


    /// What the pages read.
    let view (state: OrderPlanState) : OrderPlanView =
        match state.Cart, state.InFlight with
        | OrderPlanCart.NoPatient _, _ -> OrderPlanView.NoPatient
        | OrderPlanCart.Opened(_, tp), Some(sent, _) -> OrderPlanView.Changing(meanwhile tp sent, state.Selected)
        | OrderPlanCart.Opened(_, tp), None -> OrderPlanView.Settled(tp, state.Selected)


    /// What the pages read while the patient may be changing: during a patient change the plan held
    /// shows as a change under way, so that nothing is ordered for the patient being replaced.
    let viewWhile (patient: PatientMachine.PatientState) (state: OrderPlanState) =
        match view state with
        | OrderPlanView.Settled(tp, selected) when PatientMachine.PatientState.changing patient ->
            OrderPlanView.Changing(tp, selected)
        | shown -> shown


    /// The request id the state waits on; None while no request is under way.
    let inFlightRequest (state: OrderPlanState) = state.InFlight |> Option.map snd


    /// Whether the plan before a reopen is kept, to be put back when its list closes without a pick.
    let isKept (state: OrderPlanState) = state.Kept.IsSome


    /// The command sent, when the answer names the request under way; None otherwise, so an
    /// answer lands only on its own request.
    let landing (request: string) (inFlight: (OrderPlanCommand * string) option) =
        match inFlight with
        | Some(sent, underWay) when underWay = request -> Some sent
        | _ -> None


    /// Whether an answer to request is the one the plan waits for. An answer to a request an
    /// open or a recalculation has since replaced is not, and the machine drops it.
    let awaits (request: string) (state: OrderPlanState) = landing request state.InFlight |> Option.isSome


    /// The request stage: each intent becomes a request under the given id or an effect. A call
    /// while a request is under way is dropped; an open or a recalculation replaces it.
    let private apply (request: string) (intents: OrderPlanCartIntent list) (state: OrderPlanState) =
        let call (cmd: OrderPlanCommand) (state: OrderPlanState) =
            { state with InFlight = Some(cmd, request) }, [ OrderPlanEffect.CallPlan(cmd, request) ]

        intents
        |> List.fold
            (fun (state: OrderPlanState, effects) intent ->
                let state, added =
                    match intent with
                    | OrderPlanCartIntent.Open(pat, ctxs) -> call (OrderPlanCommand.Open(pat, ctxs)) state
                    | OrderPlanCartIntent.Recalculate tp -> call (OrderPlanCommand.Recalculate tp) state
                    | OrderPlanCartIntent.Call _ when state.InFlight.IsSome -> state, []
                    // the one place a command goes out, so the one place it counts as a change to
                    // the plan
                    | OrderPlanCartIntent.Call cmd ->
                        call cmd { state with Work = state.Work |> PlanWork.afterCommand cmd }
                    | OrderPlanCartIntent.CheckInteractions drugs -> state, [ OrderPlanEffect.CheckInteractions drugs ]
                    | OrderPlanCartIntent.GoToPlanPage -> state, [ OrderPlanEffect.GoToPlanPage ]
                    | OrderPlanCartIntent.ResetWorkbench -> state, [ OrderPlanEffect.ResetWorkbench ]
                    | OrderPlanCartIntent.Tell errs -> state, [ OrderPlanEffect.TellError errs ]

                state, effects @ added
            )
            (state, [])


    /// Runs the plan stage, then the request stage. The dialog closes on a patient change, a
    /// reopen or a filter change that goes out, and keeps its selection only while its context
    /// is in the plan. A cleared patient drops the request.
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

        let inFlight =
            match plan with
            | OrderPlanCart.NoPatient _ -> None
            | _ -> state.InFlight

        apply
            request
            intents
            { state with
                Cart = plan
                InFlight = inFlight
                Selected = selected
            }


    /// The next state and effects for a message, a reopen and a restore aside.
    let private move (msg: OrderPlanMsg) (state: OrderPlanState) : OrderPlanState * OrderPlanEffect list =
        match msg with
        // only an answer to the request under way reaches the plan
        | OrderPlanMsg.Answered(request, result) ->
            match landing request state.InFlight with
            | None -> state, []
            | Some sent ->
                let landed, effects = run request (OrderPlanCartMsg.Landed(sent, result)) { state with InFlight = None }

                // an open that landed is the version opened
                match sent, result with
                | OrderPlanCommand.Open _, Ok tp -> { landed with Opened = tp.OrderContexts }, effects
                | _ -> landed, effects

        // the selection needs no request; nothing can be selected until the plan is open
        | OrderPlanMsg.Select id ->
            match state.Cart, state.InFlight with
            | OrderPlanCart.Opened _, Some(OrderPlanCommand.Open _, _) -> state, []
            | OrderPlanCart.Opened _, _ -> { state with Selected = id }, []
            | OrderPlanCart.NoPatient _, _ -> state, []

        // a patient change during an open: the same contexts are opened again for the new
        // patient
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

        // no patient, or a version opened, leaves nothing unsigned
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
        // the argumentation needs no request; a changed text counts as a change to the plan
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
        // taken by transition
        | OrderPlanMsg.Reopen _
        | OrderPlanMsg.Restore -> state, []
        // the plan is the version just signed
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


    /// The next state and effects for a message. A reopen keeps the state before it, which an
    /// answer carries along and a restore puts back; any other message ends the look, and the
    /// state kept goes. A reopen while a request is under way keeps nothing and goes as a plain
    /// command: the state kept would hold that request, and a restore would put it back in flight
    /// after its answer. So the state kept has nothing under way, and the answer to the clear
    /// finds no request to land on after a restore.
    let transition (msg: OrderPlanMsg) (state: OrderPlanState) : OrderPlanState * OrderPlanEffect list =
        match msg with
        | OrderPlanMsg.Reopen(cmd, request) when state.InFlight.IsSome ->
            move (OrderPlanMsg.Command(cmd, request)) { state with Kept = None }
        | OrderPlanMsg.Reopen(cmd, request) ->
            let kept = { state with Kept = None }
            let moved, effects = move (OrderPlanMsg.Command(cmd, request)) kept
            { moved with Kept = Some kept }, effects
        | OrderPlanMsg.Restore ->
            match state.Kept with
            | Some kept -> kept, []
            | None -> state, []
        | OrderPlanMsg.Answered _ -> move msg state
        | _ -> move msg { state with Kept = None }


    /// Whether the message reaches the plan: changes from a page do not while a signature is under
    /// way, so the plan signed is the plan shown, nor during a patient change, so that nothing is
    /// ordered for the patient being replaced.
    let admitted (signing: SigningMachine.SigningView) (patient: PatientMachine.PatientState) (msg: OrderPlanMsg) =
        match msg with
        | OrderPlanMsg.Command _
        | OrderPlanMsg.Reopen _
        | OrderPlanMsg.Filter _
        | OrderPlanMsg.Argue _ -> not (SigningPolicy.underWay signing || PatientMachine.PatientState.changing patient)
        | OrderPlanMsg.PatientChanged _
        | OrderPlanMsg.Version _
        | OrderPlanMsg.Answered _
        | OrderPlanMsg.Select _
        | OrderPlanMsg.Restore
        | OrderPlanMsg.Signed -> true


    /// The transition, with messages not admitted during a signature or a patient change ignored.
    let transitionWhile
        (signing: SigningMachine.SigningView)
        (patient: PatientMachine.PatientState)
        (msg: OrderPlanMsg)
        (state: OrderPlanState)
        =
        if admitted signing patient msg then
            transition msg state
        else
            state, []
