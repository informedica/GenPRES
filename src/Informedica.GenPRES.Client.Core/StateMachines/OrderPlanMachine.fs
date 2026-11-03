namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the order plan from no patient to an open plan, through changes and recalculations. The App
/// carries out the effects.
///
/// Four invariants:
/// - one request is in flight at a time: the pages are disabled while it is, so nothing but its
///   answer and the patient cleared reaches the machine meanwhile;
/// - an answer lands only on the request it names, so an older answer never replaces a newer
///   plan;
/// - the patient cleared drops the request in flight, and its answer lands nowhere;
/// - a failed change leaves the plan as it was, so the next action is the retry.
module OrderPlanMachine =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Api
    open PlanWorkPolicy


    /// The plan itself, without any request under way.
    [<RequireQualifiedAccess>]
    type OrderPlanCart =
        /// No patient. The session's version, the patient it was signed on and its contexts, waits
        /// here until the patient arrives, since the version arrives first.
        | NoPatient of awaiting: (Patient * OrderContext[]) option
        /// The plan as answered, which a failed change goes back to. While an open is under way, this
        /// is the empty plan on the current patient, and the version travels in the request.
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
        /// longer holds, and for a locked one, whatever page the command comes from.
        let command (tp: OrderPlan) (change: OrderPlanChange) =
            match change with
            | OrderPlanChange.Add ctx -> Some(OrderPlanCommand.AddOrderContext(tp, ctx))
            | OrderPlanChange.NewNutrition category -> Some(OrderPlanCommand.NewOrderContext(tp, category))
            | OrderPlanChange.Remove ids -> Some(OrderPlanCommand.RemoveOrderContexts(tp, ids))
            | OrderPlanChange.FilterRows ids -> Some(OrderPlanCommand.FilterRows(ids, tp))
            | OrderPlanChange.OrderDialogCommand(id, cmd) ->
                tp.OrderContexts
                |> Array.tryFind (fun c -> c.Id = id)
                |> Option.filter (PlanContextPolicy.locked >> not)
                |> Option.map (fun ctx -> OrderPlanCommand.EvaluateContext(tp, id, cmd, ctx))


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
                /// Whether the patient update under way follows a version opened, which gives no summary:
                /// the version opened is told, the orders show their notes and the sign dialog the changes.
                Following: bool
                /// The state before a recalculation from the dialog, put back when its list closes
                /// without a pick; None when no recalculation is looked at.
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
        /// The list of a recalculation closed without a pick: the plan kept is put back, with whether
        /// it had changed since the version last opened or signed, and the answer to the
        /// recalculation is dropped.
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
        /// The orders follow new patient data; tell the prescriber in one summary.
        | TellPatientDataChanged of Alert.PatientDataSummary
        /// The plan could not follow the patient data, so the same data can be given again.
        | PatientDataNotFollowed
        /// An order prescribed landed in the plan, so the workbench can be emptied.
        | OrderAdded


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
                Cart = OrderPlanCart.NoPatient None
                InFlight = None
                Selected = None
                Work = PlanWork.AsSigned
                Opened = [||]
                Following = false
                Kept = None
            }


        /// No patient yet, with the session's version waiting for it: the patient it was signed on and
        /// its contexts.
        let awaiting (signedOn: Patient) (contexts: OrderContext[]) =
            { noPatient with Cart = OrderPlanCart.NoPatient(Some(signedOn, contexts)) }


        /// An open under way, with the empty plan shown; the contexts travel in the request.
        let opening (pat: Patient) (contexts: OrderContext[]) (request: string) =
            { noPatient with
                Cart = OrderPlanCart.Opened(pat, OrderPlan.create pat [||])
                InFlight = Some(OrderPlanCommand.Open(pat, contexts), request)
            }


        /// The plan of an opened version, nothing under way.
        let held (pat: Patient) (tp: OrderPlan) (selected: string option) =
            { noPatient with
                Cart = OrderPlanCart.Opened(pat, tp)
                Selected = selected
                Opened = tp.OrderContexts
            }


        /// A change under way over the plan of an opened version, which a failure goes back to.
        let changing
            (pat: Patient)
            (tp: OrderPlan)
            (selected: string option)
            (sent: OrderPlanCommand)
            (request: string)
            =
            { held pat tp selected with InFlight = Some(sent, request) }


        /// The state with whether the plan changed since the version last opened or signed.
        let withWork (work: PlanWork) (state: OrderPlanState) = { state with Work = work }


        /// Whether the plan changed since the version last opened or signed.
        let work (state: OrderPlanState) = state.Work


        /// The state with the contexts of the version last opened or signed.
        let withOpened (opened: OrderContext[]) (state: OrderPlanState) = { state with Opened = opened }


        /// The work after new patient data landed: orders the data changed are a change in the plan, and
        /// a plan equal to the version last opened or signed, after a weight corrected back, has nothing
        /// to sign.
        let workAfterUpdate (opened: OrderContext[]) (tp: OrderPlan) =
            if PlanDifferencePolicy.differences opened tp |> Array.isEmpty then
                PlanWork.AsSigned
            else
                PlanWork.Changed


        /// The patient the plan is on once a command has landed: an update takes its patient only now,
        /// with the orders calculated on it, and a version the patient it was signed on.
        let landedOn (sent: OrderPlanCommand) (pat: Patient) =
            match sent with
            | OrderPlanCommand.UpdatePatient(updated, _)
            | OrderPlanCommand.Open(updated, _) -> updated
            | _ -> pat


        /// The current patient a version that landed must follow; none for a version on that patient
        /// and for any other command.
        let toFollow (sent: OrderPlanCommand) (current: Patient) =
            match sent with
            | OrderPlanCommand.Open(signedOn, _) when signedOn <> current -> Some current
            | _ -> None


        /// The order contexts of the plan new, changed or removed since the version last opened or
        /// signed, as the sign dialog lists them; the plan is the one the dialog shows.
        let differences (plan: OrderPlan) (state: OrderPlanState) = PlanDifferencePolicy.differences state.Opened plan


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


        /// Whether the plan before a recalculation is kept, to be put back when its list closes without a pick.
        let isKept (state: OrderPlanState) = state.Kept.IsSome


        /// Whether the plan waits for the answer to request: a change sent under request and not yet
        /// answered. Right after a change, this says whether the change went out. An answer to a request
        /// the patient cleared or a start-over has since dropped is not awaited, and the machine drops it.
        let awaits (request: string) (state: OrderPlanState) =
            match state.InFlight with
            | Some(_, underWay) -> underWay = request
            | None -> false


        /// The distinct names of the drugs in the plan.
        let drugNames (tp: OrderPlan) =
            Informedica.GenPRES.Shared.Models.OrderPlan.orders tp
            |> Array.map _.Name
            |> Array.distinct
            |> Array.toList


        /// The check of the plan's drugs for interactions, always asked, so a plan down to one drug
        /// clears the old warnings.
        let interactions (tp: OrderPlan) = drugNames tp |> OrderPlanEffect.CheckInteractions


        /// The next state and effects for a message. Only an answer, a selection, a restore, the
        /// signature and the patient cleared reach the plan while a request is under way; every
        /// other message comes only with nothing under way, since the pages that send it are
        /// disabled, and with a request out falls to the closing arm, which leaves the state as it is.
        ///
        /// The dialog closes on a patient change, a version opened and a row filter, and after an
        /// answer keeps its selection only while its context is in the plan. A recalculation keeps the state
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

            // the version opened on the patient it was signed on, so that it stays the base of the
            // differences; the empty plan is shown on the current patient while the version travels in
            // the request. The dialog closed and the look ended
            let opening pat signedOn contexts request (state: OrderPlanState) =
                send
                    (OrderPlanCommand.Open(signedOn, contexts))
                    request
                    { state with
                        Cart = OrderPlanCart.Opened(pat, OrderPlan.create pat [||])
                        Selected = None
                        Kept = None
                    }

            match msg, state.Cart, state.InFlight with
            // only an answer to the request under way lands, for the patient held; an open that
            // landed is the version opened, and a failed change leaves the plan as it was
            | OrderPlanMsg.Answered(request, result), OrderPlanCart.Opened(pat, before), Some(sent, underWay) when
                underWay = request
                ->
                let following = state.Following

                let state =
                    { state with
                        InFlight = None
                        Following = false
                    }

                let patientUpdate =
                    match sent with
                    | OrderPlanCommand.UpdatePatient _ -> true
                    | _ -> false

                match result with
                | Ok tp ->
                    let opened =
                        match sent with
                        | OrderPlanCommand.Open _ -> tp.OrderContexts
                        | _ -> state.Opened

                    let landed = landedOn sent pat
                    let follow = toFollow sent pat

                    let work =
                        if patientUpdate then
                            workAfterUpdate opened tp
                        else
                            state.Work

                    let effects =
                        [
                            // new patient data can bring a new order, from a nutrition context restarted; with
                            // the same drugs no check is asked, so its answer does not replace the summary
                            if not patientUpdate || Set.ofList (drugNames before) <> Set.ofList (drugNames tp) then
                                interactions tp
                            OrderPlanEffect.TellAnswered
                            match sent with
                            | OrderPlanCommand.AddOrderContext _ -> OrderPlanEffect.OrderAdded
                            | _ -> ()
                            yield!
                                PatientDataSummaryPolicy.summaryOf sent tp
                                |> Option.filter (fun _ -> not following)
                                |> Option.map OrderPlanEffect.TellPatientDataChanged
                                |> Option.toList
                        ]

                    let state =
                        { state with
                            Cart = OrderPlanCart.Opened(landed, tp)
                            Selected = selectionIn tp state.Selected
                            Opened = opened
                            Work = work
                        }

                    match follow with
                    | Some current ->
                        // under the id of the open, whose answer has landed: the state makes no ids
                        let state, call = state |> send (OrderPlanCommand.UpdatePatient(current, tp)) request
                        { state with Following = true }, effects @ call
                    | None -> state, effects
                | Error errs ->
                    state,
                    [
                        OrderPlanEffect.TellError errs
                        // nothing landed, so the same data can be given again
                        if patientUpdate then
                            OrderPlanEffect.PatientDataNotFollowed
                    ]
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
                let signedOn, contexts = awaiting |> Option.defaultValue (pat, [||])
                opening pat signedOn contexts request state
            // the same data given again is no change
            | OrderPlanMsg.PatientDataChanged(Some pat, _), OrderPlanCart.Opened(before, _), None when before = pat ->
                state, []
            // the plan held is recalculated for the patient updated; it stays on the patient held until
            // the answer, so the same data given again after a failure is sent again
            | OrderPlanMsg.PatientDataChanged(Some pat, request), OrderPlanCart.Opened(_, tp), None ->
                send
                    (OrderPlanCommand.UpdatePatient(pat, tp))
                    request
                    { state with
                        Selected = None
                        Kept = None
                    }

            // the signed version replaces the plan, which then holds nothing unsigned; it waits for the
            // patient, so that one request goes out at a time and it opens on the patient of now
            | OrderPlanMsg.OpenSignedPlan(head, _), _, None ->
                { state with
                    Cart = OrderPlanCart.NoPatient(Some(head.Patient, head.OrderContexts))
                    Work = PlanWork.AsSigned
                    Opened = [||]
                    Selected = None
                    Kept = None
                },
                []

            // a change from a page; a recalculation from the order dialog keeps the state before it
            | OrderPlanMsg.Change(c, request), OrderPlanCart.Opened(_, tp), None ->
                let kept =
                    match c with
                    | OrderPlanChange.OrderDialogCommand(_, cmd) when IntentPolicy.isReCalc cmd ->
                        Some { state with Kept = None }
                    | _ -> None

                change c tp request kept state

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
