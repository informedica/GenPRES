namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the order plan from no patient data to an open plan, through changes and recalculations. The App
/// carries out the effects.
///
/// Four invariants:
/// - only one request runs at a time; the pages are disabled meanwhile;
/// - a response is used only when its request id matches, so an older response never replaces a newer plan;
/// - clearing the patient drops the request in progress, and its response is ignored;
/// - a failed change leaves the plan unchanged, so the user can retry.
module OrderPlanMachine =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Api
    open PlanWorkPolicy


    /// The plan; a request in progress is in the InFlight field of the state.
    [<RequireQualifiedAccess>]
    type OrderPlanCart =
        /// No patient data. The signed version of the session, with its patient data and contexts, waits here
        /// and opens with the first patient data, because the version arrives first.
        | NoPatientData of signedVersion: (PatientData * OrderContext[]) option
        /// The plan from the last response, which a failed change returns to. While a version opens, this is
        /// the empty plan for the current patient, and the version is in the request.
        | Opened of PatientData * OrderPlan


    /// A change a page asks for. It does not carry a plan; the machine builds the command from its own plan.
    [<RequireQualifiedAccess>]
    type OrderPlanChange =
        /// Adds a context from the workbench or a nutrition slot, narrowed to a single scenario, with the
        /// selected linked orders, each narrowed to its selected scenario.
        | Add of OrderContext * linked: OrderContext[]
        /// Removes the contexts with these ids.
        | Remove of ids: string[]
        /// Shows only the rows of the contexts with these ids; the totals follow the rows.
        | FilterRows of ids: string[]
        /// Applies an order dialog command to the context with this id.
        | OrderDialogCommand of contextId: string * OrderViewCommand


    /// Functions over OrderPlanChange.
    module OrderPlanChange =

        /// Builds the plan command for the change. An order dialog command gives None when the context is not
        /// in the plan or is locked.
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


    /// The state of the order plan machine. It is private; the pages read an OrderPlanView instead.
    type OrderPlanState =
        private
            {
                /// The plan.
                Cart: OrderPlanCart
                /// The command in progress and the request id its response must match.
                InFlight: (OrderPlanCommand * string) option
                /// The id of the context the dialog shows.
                Selected: string option
                /// Whether the plan changed since the version last opened or signed; the leave-page guard
                /// reads it.
                Work: PlanWork
                /// The contexts of the version last opened or signed; new and changed orders are compared
                /// against these.
                Opened: OrderContext[]
                /// Whether the patient data update in progress follows an opened version. Such an update shows
                /// no summary; the orders show their notes and the sign dialog shows the changes.
                Following: bool
                /// The state before a recalculation from the dialog, restored when the recalculation list
                /// closes without a selection; None while the list is closed.
                BeforeRecalculation: OrderPlanState option
            }


    /// A message to the order plan machine. A message that starts a request carries the request id, so the
    /// response can be matched to it.
    [<RequireQualifiedAccess>]
    type OrderPlanMsg =
        /// The patient data is set, updated or cleared; the plan is recalculated for it.
        | PatientDataChanged of PatientData option * request: string
        /// The signed version the session opened with. It replaces the plan when no request is in progress.
        | OpenSignedPlan of SignedOrderPlan * request: string
        /// A change from a page, applied to the current plan.
        | Change of OrderPlanChange * request: string
        /// The response to the request with this id; Error when the server or the call failed.
        | Answered of request: string * Result<OrderPlan, string[]>
        /// Selects the context the dialog shows, by id.
        | SelectContext of string option
        /// The recalculation list closed without a selection. Restores the state before the recalculation,
        /// so the response to the recalculation is ignored.
        | RestoreField
        /// The plan was signed; it had no change meanwhile, so it equals the signed version.
        | Signed


    /// An effect the App carries out for the order plan machine.
    [<RequireQualifiedAccess>]
    type OrderPlanEffect =
        /// Sends the plan command with this request id.
        | CallPlan of OrderPlanCommand * request: string
        /// Checks the medications for interactions; fewer than two clears the warnings.
        | CheckInteractions of string list
        /// Shows these errors to the user.
        | TellError of string[]
        /// The awaited response arrived, so an earlier error can be closed.
        | TellAnswered
        /// The orders were recalculated for new patient data; shows the prescriber one summary.
        | TellPatientDataChanged of Alert.PatientDataSummary
        /// The plan could not be recalculated for the patient data, so the same data can be sent again.
        | PatientDataNotFollowed
        /// An order was added to the plan, so its source can be cleared: the nutrition slot with this id, or
        /// the workbench when None.
        | OrderAdded of slotId: string option


    /// The view of the order plan state for the pages. Pages render Settled and Changing the same way, so the
    /// screen stays filled during a request. The dialog works in both; other commands are sent from Settled
    /// only.
    [<RequireQualifiedAccess>]
    type OrderPlanView =
        /// No patient data, so no plan.
        | NoPatientData
        /// The plan from the last response, with the context the dialog shows.
        | Settled of OrderPlan * selected: string option
        /// A change in progress, with the plan to show meanwhile. For a patient data update or a row filter,
        /// this is the sent plan with the change applied, so the selected rows show at once; otherwise the
        /// current plan.
        | Changing of OrderPlan * selected: string option


    /// Functions over OrderPlanView.
    module OrderPlanView =

        /// Whether the plan has an order with this order id, so the prescribe button is not offered again.
        /// The id is compared with order ids, not context ids.
        let hasOrder orderId view =
            match view with
            | OrderPlanView.Settled(tp, _)
            | OrderPlanView.Changing(tp, _) -> OrderPlan.orders tp |> Array.exists (fun sc -> sc.Order.Id = orderId)
            | OrderPlanView.NoPatientData -> false


    /// The constructors and the transition of the order plan machine.
    module OrderPlanState =

        /// The state with no patient data and no request.
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


        /// The state with no patient data and the signed version of the session, its patient data and
        /// contexts, waiting for the first patient data.
        let withSignedVersion signedOn contexts =
            { noPatientData with Cart = OrderPlanCart.NoPatientData(Some(signedOn, contexts)) }


        /// The state while a version opens: the empty plan is shown and the contexts are in the request.
        let opening pat contexts request =
            { noPatientData with
                Cart = OrderPlanCart.Opened(pat, OrderPlan.create pat [||])
                InFlight = Some(OrderPlanCommand.Open(pat, contexts), request)
            }


        /// The state with the plan of an opened version and no request.
        let settled pat tp selected =
            { noPatientData with
                Cart = OrderPlanCart.Opened(pat, tp)
                Selected = selected
                Opened = tp.OrderContexts
            }


        /// The state with a change in progress on the plan of an opened version; a failure returns to that
        /// plan.
        let changing pat tp selected sent request = { settled pat tp selected with InFlight = Some(sent, request) }


        /// Sets whether the plan changed since the version last opened or signed.
        let withWork work (state: OrderPlanState) = { state with Work = work }


        /// Returns whether the plan changed since the version last opened or signed.
        let work state = state.Work


        /// Sets the contexts of the version last opened or signed.
        let withOpened opened (state: OrderPlanState) = { state with Opened = opened }


        /// Returns the work after a patient data update: Changed when orders differ from the version last
        /// opened or signed, else AsSigned, for example after a weight is corrected back.
        let workAfterUpdate opened tp =
            if PlanDifferencePolicy.differences opened tp |> Array.isEmpty then
                PlanWork.AsSigned
            else
                PlanWork.Changed


        /// Returns the patient data of the plan after a response. A patient data update gives the updated data,
        /// an opened version gives its signed patient data, and other commands keep the current data.
        let landedOn sent pat =
            match sent with
            | OrderPlanCommand.UpdatePatientData(updated, _)
            | OrderPlanCommand.Open(updated, _) -> updated
            | _ -> pat


        /// Returns the current patient data when an opened version was signed with other patient data, so the
        /// plan must be updated to it; None otherwise.
        let toFollow sent current =
            match sent with
            | OrderPlanCommand.Open(signedOn, _) when signedOn <> current -> Some current
            | _ -> None


        /// Returns the order contexts of the plan that are new, changed or removed since the version last
        /// opened or signed, as the sign dialog lists them.
        let differences plan state = PlanDifferencePolicy.differences state.Opened plan


        /// Returns the plan when there is patient data; the empty plan while a version opens.
        let plan state =
            match state.Cart with
            | OrderPlanCart.NoPatientData _ -> None
            | OrderPlanCart.Opened(_, tp) -> Some tp


        /// Returns the patient data of the plan, if any.
        let patient state =
            match state.Cart with
            | OrderPlanCart.NoPatientData _ -> None
            | OrderPlanCart.Opened(pat, _) -> Some pat


        /// Returns the dialog selection when its context is in the plan, else None.
        let selectionIn (tp: OrderPlan) selected =
            selected
            |> Option.filter (fun id -> tp.OrderContexts |> Array.exists (fun c -> c.Id = id))


        /// Returns the plan to show while a change is in progress. For a patient data update or a row filter,
        /// this is the sent plan with the change applied, so the selected rows show at once; otherwise the
        /// current plan.
        let meanwhile tp sent =
            match sent with
            | OrderPlanCommand.UpdatePatientData(pat, sent) -> { sent with PatientData = pat }
            | OrderPlanCommand.FilterRows(ids, sent) -> { sent with Filtered = ids }
            | _ -> tp


        /// Returns the view the pages read.
        let view state =
            match state.Cart, state.InFlight with
            | OrderPlanCart.NoPatientData _, _ -> OrderPlanView.NoPatientData
            | OrderPlanCart.Opened(_, tp), Some(sent, _) -> OrderPlanView.Changing(meanwhile tp sent, state.Selected)
            | OrderPlanCart.Opened(_, tp), None -> OrderPlanView.Settled(tp, state.Selected)


        /// Returns the context id of the add in progress: the slot id for a nutrition slot, the empty id for
        /// the workbench. None when no add is in progress.
        let adding state =
            match state.InFlight with
            | Some(OrderPlanCommand.AddOrderContext(_, ctx, _), _) -> Some ctx.Id
            | _ -> None


        /// Returns the request id in progress; None when no request runs.
        let inFlightRequest state = state.InFlight |> Option.map snd


        /// Whether the recalculation list is open, which means a state to restore is stored.
        let isRecalculating state = state.BeforeRecalculation.IsSome


        /// Whether the request with this id is in progress. Right after a change, this tells whether the
        /// change was sent. A request dropped by clearing the patient or by a restore is not awaited, and its
        /// response is ignored.
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


        /// Returns the interaction check for the medications in the plan. It is always sent, so a plan with a
        /// single medication clears the old warnings.
        let interactions tp = medicationNames tp |> OrderPlanEffect.CheckInteractions


        /// Returns the next state and effects for a message.
        ///
        /// While a request is in progress, only a response, a selection, a restore, a signature and clearing
        /// the patient change the state. Any other message then changes nothing; the pages that send it are
        /// disabled.
        ///
        /// The dialog closes on a patient data update, an opened version and a row filter. After a response,
        /// the dialog keeps its selection only when the selected context is in the plan.
        ///
        /// A recalculation stores the state before it, and a restore brings that state back. Most other
        /// messages close the recalculation list. The stored state has no request, so after a restore the
        /// response to the recalculation is ignored.
        let transition msg state =
            // Sends a request; the only place a request is sent.
            let send cmd request state =
                { state with InFlight = Some(cmd, request) }, [ OrderPlanEffect.CallPlan(cmd, request) ]

            // The state to restore when the list of a recalculation from the order dialog closes without a
            // selection; None for any other change, which closes the recalculation list.
            let beforeRecalculation change =
                match change with
                | OrderPlanChange.OrderDialogCommand(_, cmd) when IntentPolicy.isReCalc cmd ->
                    Some { state with BeforeRecalculation = None }
                | _ -> None

            // Sends a change from a page and updates the work for the command; the only place that does so.
            // Does nothing when the command cannot be built, for example for a removed context.
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

            // Opens the version with its signed patient data, so it stays the base of the differences.
            // Meanwhile the empty plan for the current patient is shown. Closes the dialog and the
            // recalculation list.
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
            // Only the response to the request in progress is used. A successful open sets the opened version;
            // a failed change leaves the plan unchanged.
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
                    | OrderPlanCommand.UpdatePatientData _ -> true
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
                            // A patient data update can add an order from a restarted nutrition context. With the
                            // same medications no check is sent, so its alert does not replace the summary.
                            if not patientUpdate || not sameMedications then
                                interactions tp
                            OrderPlanEffect.TellAnswered
                            match sent with
                            // The workbench context has the empty id; a nutrition slot context has its slot id.
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
                        // Reuses the request id of the open, whose response is done; this state does not
                        // create ids.
                        let state, call = state |> send (OrderPlanCommand.UpdatePatientData(current, tp)) request
                        { state with Following = true }, effects @ call
                    | None -> state, effects
                | Error errs ->
                    state,
                    [
                        OrderPlanEffect.TellError errs
                        // The update failed, so the same data can be sent again.
                        if patientUpdate then
                            OrderPlanEffect.PatientDataNotFollowed
                    ]
            | OrderPlanMsg.Answered _, _, _ -> state, []

            // A selection sends no request and closes the recalculation list. Nothing can be selected until
            // the plan is open.
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

            // Clearing the patient works in every state and drops the request in progress. Without patient
            // data nothing is unsigned.
            | OrderPlanMsg.PatientDataChanged(None, _), _, _ -> noPatientData, []
            // The first patient data opens the stored version, or an empty plan.
            | OrderPlanMsg.PatientDataChanged(Some pat, request), OrderPlanCart.NoPatientData signedVersion, None ->
                let signedOn, contexts = signedVersion |> Option.defaultValue (pat, [||])
                opening pat signedOn contexts request state
            // The same data again changes nothing.
            | OrderPlanMsg.PatientDataChanged(Some pat, _), OrderPlanCart.Opened(before, _), None when before = pat ->
                state, []
            // Recalculates the plan for the updated patient data. The plan keeps the earlier patient data
            // until the response, so the same data given again after a failure is sent again.
            | OrderPlanMsg.PatientDataChanged(Some pat, request), OrderPlanCart.Opened(_, tp), None ->
                send
                    (OrderPlanCommand.UpdatePatientData(pat, tp))
                    request
                    { state with
                        Selected = None
                        BeforeRecalculation = None
                    }

            // The signed version replaces the plan, which then has nothing unsigned. The version waits for the
            // next patient data, so only one request runs at a time and it opens with the current data.
            | OrderPlanMsg.OpenSignedPlan(head, _), _, None ->
                { state with
                    Cart = OrderPlanCart.NoPatientData(Some(head.PatientData, head.OrderContexts))
                    Work = PlanWork.AsSigned
                    Opened = [||]
                    Selected = None
                    BeforeRecalculation = None
                },
                []

            // A change from a page; a recalculation from the order dialog stores the state before it.
            | OrderPlanMsg.Change(c, request), OrderPlanCart.Opened(_, tp), None ->
                change c tp request (beforeRecalculation c) state

            // The plan equals the signed version.
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

            // Any other message changes nothing: the pages that send it are disabled during a request, and
            // without patient data there is nothing to change.
            | _ -> state, []
