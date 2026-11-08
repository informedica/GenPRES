namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the order plan from no patient data to an open plan, through changes and recalculations. The App
/// carries out the effects.
///
/// Four invariants:
/// - one request is in flight at a time: the pages are disabled while it is, so nothing but the
///   response and the patient cleared reaches the machine meanwhile;
/// - a response lands only on the request it names, so an older response never replaces a newer
///   plan;
/// - the patient cleared drops the request in flight, and its response lands nowhere;
/// - a failed change leaves the plan as it was, so the next action is the retry.
module OrderPlanMachine =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Api
    open PlanWorkPolicy


    /// The plan itself; a request under way is in the InFlight field of the state.
    [<RequireQualifiedAccess>]
    type OrderPlanCart =
        /// No patient data. The session's version, the patient it was signed on and its contexts, is
        /// stored here and opened with the first patient data, since the version arrives first.
        | NoPatientData of signedVersion: (Patient * OrderContext[]) option
        /// The plan of the last response, which a failed change goes back to. While an open is under way, this
        /// is the empty plan on the current patient, and the version travels in the request.
        | Opened of Patient * OrderPlan


    /// What a page wants of the plan, never the plan it was shown: the machine builds the wire
    /// command over the plan it holds.
    [<RequireQualifiedAccess>]
    type OrderPlanChange =
        /// The workbench or a nutrition slot narrowed to a single scenario, into the plan with the linked orders
        /// turned on, each narrowed to the scenario turned on.
        | Add of OrderContext * linked: OrderContext[]
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
            | OrderPlanChange.Add(ctx, linked) -> Some(OrderPlanCommand.AddOrderContext(tp, ctx, linked))
            | OrderPlanChange.Remove ids -> Some(OrderPlanCommand.RemoveOrderContexts(tp, ids))
            | OrderPlanChange.FilterRows ids -> Some(OrderPlanCommand.FilterRows(ids, tp))
            | OrderPlanChange.OrderDialogCommand(id, cmd) ->
                tp.OrderContexts
                |> Array.tryFind (fun c -> c.Id = id)
                |> Option.filter (PlanContextPolicy.locked tp >> not)
                |> Option.map (fun ctx -> OrderPlanCommand.EvaluateContext(tp, id, cmd, ctx))


    /// Everything the order plan machine holds, hidden from the pages, which read an OrderPlanView of
    /// it instead.
    type OrderPlanState =
        private
            {
                /// The plan itself.
                Cart: OrderPlanCart
                /// The command under way and the request id its response must name.
                InFlight: (OrderPlanCommand * string) option
                /// The id of the context the dialog shows.
                Selected: string option
                /// Whether the plan changed since the version last opened or signed; the leave-page
                /// guard reads it.
                Work: PlanWork
                /// The contexts of the version last opened or signed; an order is new or changed
                /// against these.
                Opened: OrderContext[]
                /// Whether the patient data update under way follows a version opened, which gives no summary:
                /// the version opened is told, the orders show their notes and the sign dialog the changes.
                Following: bool
                /// The state before a recalculation from the dialog, put back when its list closes
                /// without a pick; None while the recalculation list is closed.
                BeforeRecalculation: OrderPlanState option
            }


    /// What moves the order plan machine. A message that starts a request carries its request id, so
    /// the response can name it.
    [<RequireQualifiedAccess>]
    type OrderPlanMsg =
        /// The patient data set, changed or cleared; the plan follows.
        | PatientDataChanged of Patient option * request: string
        /// The version the session opened with; it replaces the plan and any request.
        | OpenSignedPlan of SignedOrderPlan * request: string
        /// What a page wants of the plan, built over the plan in the cart.
        | Change of OrderPlanChange * request: string
        /// The response to the request with this id; Error is a failure of the server or the call.
        | Answered of request: string * Result<OrderPlan, string[]>
        /// The context the dialog shows, by id.
        | SelectContext of string option
        /// The list of a recalculation closed without a pick: the plan before the recalculation is put
        /// back, with whether it had changed since the version last opened or signed, and the response to
        /// the recalculation is dropped.
        | RestoreField
        /// The plan was signed; it took no change meanwhile, so it is the version signed.
        | Signed


    /// What the App carries out for the order plan machine.
    [<RequireQualifiedAccess>]
    type OrderPlanEffect =
        /// Send the plan command under this request id.
        | CallPlan of OrderPlanCommand * request: string
        /// Check the medications for interactions; fewer than two clears the warnings.
        | CheckInteractions of string list
        /// Tell the user what went wrong.
        | TellError of string[]
        /// The response the plan waited for landed; an error it raised can go.
        | TellAnswered
        /// The orders follow new patient data; tell the prescriber in one summary.
        | TellPatientDataChanged of Alert.PatientDataSummary
        /// The plan could not follow the patient data, so the same data can be given again.
        | PatientDataNotFollowed
        /// An order prescribed landed in the plan, so the context it came from can be emptied: the
        /// nutrition slot with the id, or the workbench without one.
        | OrderAdded of slotId: string option


    /// What the pages read of the OrderPlanState. Pages render Settled and Changing alike, so the
    /// screen stays filled during a request. The dialog steps from either; other commands are built
    /// from Settled only.
    [<RequireQualifiedAccess>]
    type OrderPlanView =
        /// No patient data, so no plan.
        | NoPatientData
        /// The plan of the last response, with the context the dialog shows.
        | Settled of OrderPlan * selected: string option
        /// A change under way, with the plan shown meanwhile: for a patient data update or a row filter
        /// the plan sent as the command changes it, so the rows chosen show at once; otherwise the plan
        /// in the cart.
        | Changing of OrderPlan * selected: string option


    /// Functions over OrderPlanView.
    module OrderPlanView =

        /// Whether the plan holds the order with this id, so the prescribe button is not offered
        /// again. A context's own id is not the order's.
        let hasOrder orderId view =
            match view with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) -> OrderPlan.orders tp |> Array.exists (fun sc -> sc.Order.Id = orderId)
            | OrderPlanView.NoPatientData -> false


    /// The constructors and the transition of the order plan machine.
    module OrderPlanState =

        /// No patient data, nothing under way.
        let noPatientData =
            {
                Cart = OrderPlanCart.NoPatientData None
                InFlight = None
                Selected = None
                Work = PlanWork.AsSigned
                Opened = [||]
                Following = false
                BeforeRecalculation = None
            }


        /// No patient data yet, with the session's version stored for the first patient data: the patient
        /// it was signed on and its contexts.
        let withSignedVersion signedOn contexts =
            { noPatientData with Cart = OrderPlanCart.NoPatientData(Some(signedOn, contexts)) }


        /// An open under way, with the empty plan shown; the contexts travel in the request.
        let opening pat contexts request =
            { noPatientData with
                Cart = OrderPlanCart.Opened(pat, OrderPlan.create pat [||])
                InFlight = Some(OrderPlanCommand.Open(pat, contexts), request)
            }


        /// The plan of an opened version, nothing under way.
        let settled pat tp selected =
            { noPatientData with
                Cart = OrderPlanCart.Opened(pat, tp)
                Selected = selected
                Opened = tp.OrderContexts
            }


        /// A change under way over the plan of an opened version, which a failure goes back to.
        let changing pat tp selected sent request = { settled pat tp selected with InFlight = Some(sent, request) }


        /// The state with whether the plan changed since the version last opened or signed.
        let withWork work (state: OrderPlanState) = { state with Work = work }


        /// Whether the plan changed since the version last opened or signed.
        let work state = state.Work


        /// The state with the contexts of the version last opened or signed.
        let withOpened opened (state: OrderPlanState) = { state with Opened = opened }


        /// The work after a patient data update landed: orders the data changed are a change in the plan, and
        /// a plan equal to the version last opened or signed, after a weight corrected back, has nothing
        /// to sign.
        let workAfterUpdate opened tp =
            if PlanDifferencePolicy.differences opened tp |> Array.isEmpty then
                PlanWork.AsSigned
            else
                PlanWork.Changed


        /// The patient the plan is on once a command has landed: a patient data update takes its patient only now,
        /// with the orders calculated on it, and a version the patient it was signed on.
        let landedOn sent pat =
            match sent with
            | OrderPlanCommand.UpdatePatient(updated, _)
            | OrderPlanCommand.Open(updated, _) -> updated
            | _ -> pat


        /// The current patient a version that landed must follow; none for a version on that patient
        /// and for any other command.
        let toFollow sent current =
            match sent with
            | OrderPlanCommand.Open(signedOn, _) when signedOn <> current -> Some current
            | _ -> None


        /// The order contexts of the plan new, changed or removed since the version last opened or
        /// signed, as the sign dialog lists them; the plan is the one the dialog shows.
        let differences plan state = PlanDifferencePolicy.differences state.Opened plan


        /// The plan, if there is patient data; the empty plan while an open runs.
        let plan state =
            match state.Cart with
            | OrderPlanCart.NoPatientData _ -> None
            | OrderPlanCart.Opened(_, tp) -> Some tp


        /// The patient the plan is for, if any.
        let patient state =
            match state.Cart with
            | OrderPlanCart.NoPatientData _ -> None
            | OrderPlanCart.Opened(pat, _) -> Some pat


        /// The selection of the dialog, only while its context is in the plan.
        let selectionIn (tp: OrderPlan) selected =
            selected
            |> Option.filter (fun id -> tp.OrderContexts |> Array.exists (fun c -> c.Id = id))


        /// The plan shown while a change is under way: for a patient data update or a row filter the
        /// plan sent as the command changes it, so the rows chosen show at once; otherwise the plan in
        /// the cart.
        let meanwhile tp sent =
            match sent with
            | OrderPlanCommand.UpdatePatient(pat, sent) -> { sent with Patient = pat }
            | OrderPlanCommand.FilterRows(ids, sent) -> { sent with Filtered = ids }
            | _ -> tp


        /// What the pages read.
        let view state =
            match state.Cart, state.InFlight with
            | OrderPlanCart.NoPatientData _, _ -> OrderPlanView.NoPatientData
            | OrderPlanCart.Opened(_, tp), Some(sent, _) -> OrderPlanView.Changing(meanwhile tp sent, state.Selected)
            | OrderPlanCart.Opened(_, tp), None -> OrderPlanView.Settled(tp, state.Selected)


        /// The id of the context an add under way sends: the slot id for a nutrition slot prescribed, the empty
        /// id for the workbench. None while no add is under way.
        let adding state =
            match state.InFlight with
            | Some(OrderPlanCommand.AddOrderContext(_, ctx, _), _) -> Some ctx.Id
            | _ -> None


        /// The request id the state waits on; None while no request is under way.
        let inFlightRequest state = state.InFlight |> Option.map snd


        /// Whether the recalculation list is open, with the state before the recalculation to put back
        /// when the list closes without a pick.
        let isRecalculating state = state.BeforeRecalculation.IsSome


        /// Whether the plan waits for the response to the request: a change sent under the request and
        /// without a response yet. Right after a change, this says whether the change went out. A response
        /// to a request the patient cleared or a start-over has since dropped is not awaited, and the
        /// machine drops it.
        let awaits request state =
            match state.InFlight with
            | Some(_, underWay) -> underWay = request
            | None -> false


        /// The distinct names of the medications in the plan.
        let medicationNames tp =
            Informedica.GenPRES.Shared.Models.OrderPlan.orders tp
            |> Array.map _.Name
            |> Array.distinct
            |> Array.toList


        /// The check of the medications in the plan for interactions, always asked, so a plan down to a
        /// single medication clears the old warnings.
        let interactions tp = medicationNames tp |> OrderPlanEffect.CheckInteractions


        /// The next state and effects for a message.
        ///
        /// While a request is under way only a response, a selection, a restore, the signature and the
        /// patient cleared reach the plan. Every other message comes only with nothing under way, since
        /// the pages that send it are disabled; with a request out it falls to the closing arm, which
        /// leaves the state as it is.
        ///
        /// The dialog closes on a patient data update, a version opened and a row filter. After a response
        /// the dialog keeps its selection only while the context selected is in the plan.
        ///
        /// A recalculation stores the state before it, which the response carries along and a restore puts
        /// back; any other message closes the recalculation list. The state before the recalculation has
        /// nothing under way, so after a restore the response to the recalculation finds no request to land
        /// on.
        let transition msg state =
            // the one place a request goes out
            let send cmd request state =
                { state with InFlight = Some(cmd, request) }, [ OrderPlanEffect.CallPlan(cmd, request) ]

            // the state to put back when the list of a recalculation from the order dialog closes without a
            // pick; None for any other change, which closes the recalculation list
            let beforeRecalculation change =
                match change with
                | OrderPlanChange.OrderDialogCommand(_, cmd) when IntentPolicy.isReCalc cmd ->
                    Some { state with BeforeRecalculation = None }
                | _ -> None

            // a change from a page, built over the plan in the cart; the one place a command counts as a
            // change to the plan. Nothing for a context that is gone
            let change change tp request beforeRecalculation state =
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
                            BeforeRecalculation = beforeRecalculation
                        }
                | None -> { state with BeforeRecalculation = None }, []

            // the version opened on the patient it was signed on, so that it stays the base of the
            // differences; the empty plan is shown on the current patient while the version travels in
            // the request. The dialog and the recalculation list closed
            let opening pat signedOn contexts request state =
                send
                    (OrderPlanCommand.Open(signedOn, contexts))
                    request
                    { state with
                        Cart = OrderPlanCart.Opened(pat, OrderPlan.create pat [||])
                        Selected = None
                        BeforeRecalculation = None
                    }

            match msg, state.Cart, state.InFlight with
            // only a response to the request under way lands, for the patient in the cart; an open that
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

                    let sameMedications = Set.ofList (medicationNames before) = Set.ofList (medicationNames tp)

                    let effects =
                        [
                            // a patient data update can bring a new order, from a nutrition context restarted;
                            // with the same medications no check is asked, so its response does not replace the
                            // summary
                            if not patientUpdate || not sameMedications then
                                interactions tp
                            OrderPlanEffect.TellAnswered
                            match sent with
                            // the workbench's context always goes with the empty id, a slot's with its slot id
                            | OrderPlanCommand.AddOrderContext(_, ctx, _) ->
                                OrderPlanEffect.OrderAdded(if ctx.Id = "" then None else Some ctx.Id)
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
                        // under the id of the open, whose response has landed: the state makes no ids
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

            // the selection needs no request, and closes the recalculation list; nothing can be
            // selected until the plan is open
            | OrderPlanMsg.SelectContext _, OrderPlanCart.Opened _, Some(OrderPlanCommand.Open _, _)
            | OrderPlanMsg.SelectContext _, OrderPlanCart.NoPatientData _, _ ->
                { state with BeforeRecalculation = None }, []
            | OrderPlanMsg.SelectContext id, OrderPlanCart.Opened _, _ ->
                { state with
                    Selected = id
                    BeforeRecalculation = None
                },
                []
            | OrderPlanMsg.RestoreField, _, _ ->
                match state.BeforeRecalculation with
                | Some before -> before, []
                | None -> state, []

            // the patient cleared reaches every state and drops the request out; no patient data leaves
            // nothing unsigned
            | OrderPlanMsg.PatientDataChanged(None, _), _, _ -> noPatientData, []
            // the first patient data: the stored version, or an empty plan, is opened
            | OrderPlanMsg.PatientDataChanged(Some pat, request), OrderPlanCart.NoPatientData signedVersion, None ->
                let signedOn, contexts = signedVersion |> Option.defaultValue (pat, [||])
                opening pat signedOn contexts request state
            // the same data given again is no change
            | OrderPlanMsg.PatientDataChanged(Some pat, _), OrderPlanCart.Opened(before, _), None when before = pat ->
                state, []
            // the plan in the cart is recalculated for the patient data updated; the cart keeps the
            // patient before the update until the response, so the same data given again after a failure
            // is sent again
            | OrderPlanMsg.PatientDataChanged(Some pat, request), OrderPlanCart.Opened(_, tp), None ->
                send
                    (OrderPlanCommand.UpdatePatient(pat, tp))
                    request
                    { state with
                        Selected = None
                        BeforeRecalculation = None
                    }

            // the signed version replaces the plan, which then holds nothing unsigned; it is stored until
            // the next patient data, so that one request goes out at a time and it opens on the current
            // patient data
            | OrderPlanMsg.OpenSignedPlan(head, _), _, None ->
                { state with
                    Cart = OrderPlanCart.NoPatientData(Some(head.Patient, head.OrderContexts))
                    Work = PlanWork.AsSigned
                    Opened = [||]
                    Selected = None
                    BeforeRecalculation = None
                },
                []

            // a change from a page; a recalculation from the order dialog stores the state before it
            | OrderPlanMsg.Change(c, request), OrderPlanCart.Opened(_, tp), None ->
                change c tp request (beforeRecalculation c) state

            // the plan is the version just signed
            | OrderPlanMsg.Signed, cart, _ ->
                let opened =
                    match cart with
                    | OrderPlanCart.Opened(_, tp) -> tp.OrderContexts
                    | OrderPlanCart.NoPatientData _ -> state.Opened

                { state with
                    Work = PlanWork.AsSigned
                    Opened = opened
                    BeforeRecalculation = None
                },
                []

            // the closing arm: nothing else comes, since the pages that send it are disabled, and
            // there is nothing to change without patient data
            | _ -> state, []
