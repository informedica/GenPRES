/// Tracks the order plan from no patient to an open plan, through changes and reopens. The App
/// carries out the effects.
///
/// Four invariants:
/// - one request is in flight at a time: the pages are disabled while it is, so nothing but its
///   answer and the patient cleared reaches the machine meanwhile;
/// - an answer lands only on the request it names, so an older answer never replaces a newer
///   plan;
/// - the patient cleared drops the request in flight, and its answer lands nowhere;
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


/// What a page wants of the plan, never the plan it was shown: the machine builds the wire
/// command over the plan it holds.
[<RequireQualifiedAccess>]
type OrderPlanChange =
    /// A workbench narrowed to one scenario, into the plan as a drug context.
    | Add of OrderContext
    /// A new nutrition context for the category.
    | NewNutrition of NutritionCategory
    /// The contexts with these ids removed.
    | Remove of ids: string[]
    /// The contexts the rows keep, by id; the totals follow.
    | FilterRows of ids: string[]
    /// A command from the order dialog into the plan's context with this id.
    | OrderDialogCommand of contextId: string * OrderViewCommand


/// Functions over OrderPlanChange.
module OrderPlanChange =

    /// The wire command for the change over the plan given; None for a context the plan no
    /// longer holds.
    let command (tp: OrderPlan) (change: OrderPlanChange) =
        match change with
        | OrderPlanChange.Add ctx -> Some(OrderPlanCommand.AddOrderContext(tp, ctx))
        | OrderPlanChange.NewNutrition category -> Some(OrderPlanCommand.NewOrderContext(tp, category))
        | OrderPlanChange.Remove ids -> Some(OrderPlanCommand.RemoveOrderContexts(tp, ids))
        | OrderPlanChange.FilterRows ids -> Some(OrderPlanCommand.FilterRows(ids, tp))
        | OrderPlanChange.OrderDialogCommand(id, cmd) ->
            tp.OrderContexts
            |> Array.tryFind (fun c -> c.Id = id)
            |> Option.map (fun ctx -> OrderPlanCommand.Navigate(tp, id, cmd, ctx))


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
    /// The patient data set, changed or cleared; the plan follows.
    | PatientDataChanged of Patient option * request: string
    /// The version the session opened with; it replaces the plan and any request.
    | OpenSignedPlan of SignedOrderPlan * request: string
    /// What a page wants of the plan, built over the plan held.
    | Change of OrderPlanChange * request: string
    /// The answer to the request with this id; Error is a failure of the server or the call.
    | Answered of request: string * Result<OrderPlan, string[]>
    /// The context the dialog shows, by id.
    | SelectContext of string option
    /// A clear from the dialog that opens the field's list: the command goes out as a change, and
    /// the plan before it is kept to be put back.
    | ReopenField of contextId: string * OrderViewCommand * request: string
    /// The list of a reopen closed without a pick: the plan kept is put back, with whether it had
    /// changed since the version last opened or signed, and the answer to the clear is dropped.
    | RestoreField
    /// The plan was signed; it took no change meanwhile, so it is the version signed.
    | Signed


/// What the App carries out for the order plan machine.
[<RequireQualifiedAccess>]
type OrderPlanEffect =
    /// Send the plan command under this request id.
    | CallPlan of OrderPlanCommand * request: string
    /// Check the drugs for interactions; fewer than two clears the warnings.
    | CheckInteractions of string list
    /// Tell the user what went wrong.
    | TellError of string[]
    /// The answer the plan waited for landed; an error it raised can go.
    | TellAnswered


/// What the pages read of the OrderPlanState. Pages render Settled and Changing alike, so the
/// screen stays filled during a request. The dialog steps from either; other commands are built
/// from Settled only.
[<RequireQualifiedAccess>]
type OrderPlanView =
    /// No patient, so no plan.
    | NoPatient
    /// The plan the server answered, with the context the dialog shows.
    | Settled of OrderPlan * selected: string option
    /// A change under way, with the plan shown meanwhile: for a patient update or a row filter the
    /// plan sent as the command changes it, so the rows chosen show at once; otherwise the plan held.
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


    /// The plan shown while a change is under way: for a patient update or a row filter the plan
    /// sent as the command changes it, so the rows chosen show at once; otherwise the plan held.
    let meanwhile (tp: OrderPlan) (sent: OrderPlanCommand) =
        match sent with
        | OrderPlanCommand.UpdatePatient(pat, sent) -> { sent with Patient = pat }
        | OrderPlanCommand.FilterRows(ids, sent) -> { sent with Filtered = ids }
        | _ -> tp


    /// What the pages read.
    let view (state: OrderPlanState) : OrderPlanView =
        match state.Cart, state.InFlight with
        | OrderPlanCart.NoPatient _, _ -> OrderPlanView.NoPatient
        | OrderPlanCart.Opened(_, tp), Some(sent, _) -> OrderPlanView.Changing(meanwhile tp sent, state.Selected)
        | OrderPlanCart.Opened(_, tp), None -> OrderPlanView.Settled(tp, state.Selected)


    /// The request id the state waits on; None while no request is under way.
    let inFlightRequest (state: OrderPlanState) = state.InFlight |> Option.map snd


    /// Whether the plan before a reopen is kept, to be put back when its list closes without a pick.
    let isKept (state: OrderPlanState) = state.Kept.IsSome


    /// Whether an answer to request is the one the plan waits for. An answer to a request the
    /// patient cleared or a start-over has since dropped is not, and the machine drops it.
    let awaits (request: string) (state: OrderPlanState) =
        match state.InFlight with
        | Some(_, underWay) -> underWay = request
        | None -> false


    /// The check of the plan's drugs for interactions, always asked, so a plan down to one drug
    /// clears the old warnings.
    let interactions (tp: OrderPlan) =
        Shared.Models.OrderPlan.orders tp
        |> Array.map _.Name
        |> Array.distinct
        |> Array.toList
        |> OrderPlanEffect.CheckInteractions


    /// The next state and effects for a message. Only an answer, a selection, a restore, the
    /// signature and the patient cleared reach the plan while a request is under way; every
    /// other message comes only with nothing under way, since the pages that send it are
    /// disabled, and with a request out falls to the closing arm, which leaves the state as it is.
    ///
    /// The dialog closes on a patient change, a version opened and a row filter, and after an
    /// answer keeps its selection only while its context is in the plan. A reopen keeps the state
    /// before it, which an answer carries along and a restore puts back; any other message ends
    /// the look. The state kept has nothing under way, so the answer to the clear finds no
    /// request to land on after a restore.
    let transition (msg: OrderPlanMsg) (state: OrderPlanState) : OrderPlanState * OrderPlanEffect list =
        // the one place a request goes out
        let send cmd request (state: OrderPlanState) =
            { state with InFlight = Some(cmd, request) }, [ OrderPlanEffect.CallPlan(cmd, request) ]

        // a change from a page, built over the plan held; the one place a command counts as a
        // change to the plan. Nothing for a context that is gone
        let change change tp request kept (state: OrderPlanState) =
            match OrderPlanChange.command tp change with
            | Some cmd ->
                let selected =
                    match change with
                    | OrderPlanChange.FilterRows _ -> None
                    | _ -> state.Selected

                send
                    cmd
                    request
                    { state with
                        Work = state.Work |> PlanWork.afterCommand cmd
                        Selected = selected
                        Kept = kept
                    }
            | None -> { state with Kept = None }, []

        // the plan opened for the patient, the empty plan shown while the contexts travel in the
        // request; the dialog closed and the look ended
        let opening pat contexts request (state: OrderPlanState) =
            send
                (OrderPlanCommand.Open(pat, contexts))
                request
                { state with
                    Cart = OrderPlanCart.Opened(pat, OrderPlan.create pat [||])
                    Selected = None
                    Kept = None
                }

        match msg, state.Cart, state.InFlight with
        // only an answer to the request under way lands, for the patient held; an open that
        // landed is the version opened, and a failed change leaves the plan as it was
        | OrderPlanMsg.Answered(request, result), OrderPlanCart.Opened(pat, _), Some(sent, underWay) when
            underWay = request
            ->
            let state = { state with InFlight = None }

            match result with
            | Ok tp ->
                let opened =
                    match sent with
                    | OrderPlanCommand.Open _ -> tp.OrderContexts
                    | _ -> state.Opened

                { state with
                    Cart = OrderPlanCart.Opened(pat, tp)
                    Selected = selectionIn tp state.Selected
                    Opened = opened
                },
                [ interactions tp; OrderPlanEffect.TellAnswered ]
            | Error errs -> state, [ OrderPlanEffect.TellError errs ]
        | OrderPlanMsg.Answered _, _, _ -> state, []

        // the selection needs no request, and ends a look; nothing can be selected until the
        // plan is open
        | OrderPlanMsg.SelectContext _, OrderPlanCart.Opened _, Some(OrderPlanCommand.Open _, _)
        | OrderPlanMsg.SelectContext _, OrderPlanCart.NoPatient _, _ -> { state with Kept = None }, []
        | OrderPlanMsg.SelectContext id, OrderPlanCart.Opened _, _ ->
            { state with
                Selected = id
                Kept = None
            },
            []
        | OrderPlanMsg.RestoreField, _, _ ->
            match state.Kept with
            | Some kept -> kept, []
            | None -> state, []

        // the patient cleared reaches every state and drops the request out; no patient leaves
        // nothing unsigned
        | OrderPlanMsg.PatientDataChanged(None, _), _, _ -> noPatient, []
        // the first plan for a patient: the waiting version, or an empty plan, is opened
        | OrderPlanMsg.PatientDataChanged(Some pat, request), OrderPlanCart.NoPatient awaiting, None ->
            opening pat awaiting request state
        // the plan held is recalculated for the patient updated
        | OrderPlanMsg.PatientDataChanged(Some pat, request), OrderPlanCart.Opened(_, tp), None ->
            send
                (OrderPlanCommand.UpdatePatient(pat, tp))
                request
                { state with
                    Cart = OrderPlanCart.Opened(pat, tp)
                    Selected = None
                    Kept = None
                }

        // the signed version is opened in place of the plan, which then holds nothing unsigned;
        // before the patient arrives, its contexts wait
        | OrderPlanMsg.OpenSignedPlan(head, request), cart, None ->
            let state =
                { state with
                    Work = PlanWork.AsSigned
                    Opened = [||]
                    Selected = None
                    Kept = None
                }

            match cart with
            | OrderPlanCart.NoPatient _ -> { state with Cart = OrderPlanCart.NoPatient head.OrderContexts }, []
            | OrderPlanCart.Opened(pat, _) -> opening pat head.OrderContexts request state

        // a change from a page, and a reopen, which keeps the state before it
        | OrderPlanMsg.Change(c, request), OrderPlanCart.Opened(_, tp), None -> change c tp request None state
        | OrderPlanMsg.ReopenField(id, cmd, request), OrderPlanCart.Opened(_, tp), None ->
            change (OrderPlanChange.OrderDialogCommand(id, cmd)) tp request (Some { state with Kept = None }) state

        // the plan is the version just signed
        | OrderPlanMsg.Signed, cart, _ ->
            let opened =
                match cart with
                | OrderPlanCart.Opened(_, tp) -> tp.OrderContexts
                | OrderPlanCart.NoPatient _ -> state.Opened

            { state with
                Work = PlanWork.AsSigned
                Opened = opened
                Kept = None
            },
            []

        // the closing arm: nothing else comes, since the pages that send it are disabled, and
        // there is nothing to change without a patient
        | _ -> state, []
