namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the prescribing workbench, or a nutrition slot: an order context not yet in the plan, of the
/// category of the state, from no patient data to an evaluated context, through changes and resets. The App
/// carries out the effects.
///
/// Four invariants:
/// - one request is in flight at a time: the pages are disabled while it is, so nothing but the
///   response and the patient cleared reaches the machine meanwhile;
/// - a response lands only on the request it names;
/// - the workbench is always evaluated for the patient of the state, and again after a patient data update;
/// - a filter needs patient data; the url seed, which can come before the workbench has the patient
///   data, is stored and sent with the first patient data.
///
/// A response is the context evaluated, the context refused with the reason, or a failure, after
/// which the workbench stays as it was.
module OrderContextMachine =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Api
    open FilterSeed


    /// The workbench or the nutrition slot itself; a request under way is in the InFlight field of the state.
    [<RequireQualifiedAccess>]
    type OrderContextWorkbench =
        /// No patient data, so no workbench; the url seed, which can come before the patient data, is
        /// stored here and sent with the first patient data.
        | NoPatientData of urlSeed: FilterSeed option
        /// The patient and the context last evaluated for it, which a failed change goes back to: the
        /// empty context during the first evaluation and after a reset.
        | Evaluated of Patient * OrderContext
        /// The patient and the context the server refused, as sent, with its picks and without its
        /// scenarios, and the reason.
        | Refused of Patient * OrderContext * OrderContextRefusal


    /// Functions over OrderContextWorkbench.
    module OrderContextWorkbench =

        /// An empty context of the category for the patient.
        let emptyOf category pat =
            { (OrderContext.empty |> OrderContext.setPatient pat) with Category = category }


        /// The context narrowed to the scenario and its form, as it goes into the plan.
        let narrowedToScenario (sc: OrderScenario) ctx =
            { ctx with
                OrderContext.Filter.Form = Some sc.Form
                Scenarios = [| sc |]
            }


        /// The patient and the context in the workbench; None without patient data.
        let patientAndContext workbench =
            match workbench with
            | OrderContextWorkbench.NoPatientData _ -> None
            | OrderContextWorkbench.Evaluated(pat, ctx)
            | OrderContextWorkbench.Refused(pat, ctx, _) -> Some(pat, ctx)


    /// Whether a linked offer goes into the plan with the main order.
    [<RequireQualifiedAccess>]
    type OfferSwitch =
        /// The offer goes into the plan with the main order, once it is narrowed to a single scenario.
        | On
        /// The offer stays out of the plan.
        | Off


    /// Functions over OfferSwitch.
    module OfferSwitch =

        /// The switch turned the other way.
        let flip switch =
            match switch with
            | OfferSwitch.On -> OfferSwitch.Off
            | OfferSwitch.Off -> OfferSwitch.On


    /// An order the link rules offer to link to the order of the context, composed like any order context
    /// and turned on or off.
    type LinkedOffer =
        {
            /// The context the server offered, evaluated for the same patient, as its last response left it.
            Context: OrderContext
            /// Whether the offer goes into the plan with the main order.
            Switch: OfferSwitch
        }


    /// Functions over LinkedOffer.
    module LinkedOffer =

        /// The offer of the context, turned off.
        let off ctx =
            {
                Context = ctx
                Switch = OfferSwitch.Off
            }


        /// The offers with the offer at the index changed.
        let changedAt index change offers =
            offers |> Array.mapi (fun i o -> if i = index then change o else o)


        /// The context of the offer narrowed to a single scenario, as it goes into the plan; None while
        /// the context holds several scenarios or none.
        let narrowed offer =
            offer.Context
            |> Informedica.GenPRES.Shared.Models.OrderContext.contribution
            |> Option.map (fun sc -> offer.Context |> OrderContextWorkbench.narrowedToScenario sc)


    /// What a request of the order context machine is sent for: its own context, or a linked offer.
    [<RequireQualifiedAccess>]
    type RequestFor =
        /// The context of the machine: the workbench or the slot.
        | Context
        /// The linked offer with this index in the list.
        | Offer of index: int


    /// A request of the order context machine under way.
    type RequestUnderWay =
        {
            /// The command sent, which the page shows meanwhile.
            Sent: OrderContextCommand
            /// The request id the response must name.
            Request: string
            /// What the request was sent for.
            For: RequestFor
        }


    /// Functions over RequestUnderWay.
    module RequestUnderWay =

        /// The command sent under the request id for what it was sent for.
        let create sent request target =
            {
                Sent = sent
                Request = request
                For = target
            }


        /// The context the page shows while the request is under way: the context sent as the
        /// command changes it.
        let shown request =
            let (OrderContextCommand.Command(cmd, ctx)) = request.Sent
            CommandPreview.shown cmd ctx


    /// Everything the order context machine holds, hidden from the page, which reads an
    /// OrderContextView of it instead.
    type OrderContextState =
        private
            {
                /// The category of the context: medication for the workbench, a nutrition category for a
                /// slot. An empty context is of this category, so that the server narrows it to the
                /// category's rule set.
                Category: OrderCategory
                /// The workbench itself.
                Workbench: OrderContextWorkbench
                /// The request under way. The context and its offers share it, so one request is out at a time.
                InFlight: RequestUnderWay option
                /// The scenario the order dialog shows, by its order's id; None while the dialog is
                /// closed.
                Selected: string option
                /// The state before a recalculation from the dialog, put back when its list closes
                /// without a pick; None while the recalculation list is closed.
                BeforeRecalculation: OrderContextState option
                /// The orders the link rules offer to link to the order of the context: the offers that came
                /// with the last response to a change of the choices or the scenario.
                LinkedOffers: LinkedOffer[]
            }


    /// What moves the order context machine. A message that starts a request carries its request id,
    /// so the response can name it.
    [<RequireQualifiedAccess>]
    type OrderContextMsg =
        /// The patient data set, changed or cleared; the workbench is evaluated for it.
        | PatientDataChanged of Patient option * request: string
        /// A seed of the filter, sent as a command over the context shown; without patient data the seed
        /// is stored and sent with the first patient data.
        | SeedFilter of FilterSeed * request: string
        /// A command from the page, sent over the context in the workbench.
        | Command of OrderViewCommand * request: string
        /// The response to the request with this id: the context evaluated or refused; Error is a
        /// failure of the server or the call.
        | Answered of request: string * Result<OrderContextResponse, string[]>
        /// Clear the workbench and evaluate it empty, after an order was prescribed.
        | Reset of request: string
        /// The scenario the dialog shows, by its order's id.
        | SelectScenario of string option
        /// The list of a recalculation closed without a pick: the state before the recalculation is
        /// put back, and the response to the recalculation is dropped.
        | RestoreField
        /// The linked order offer with this index in the list turned on.
        | TurnOfferOn of index: int
        /// The linked order offer with this index in the list turned off.
        | TurnOfferOff of index: int
        /// A command from the page over the linked order offer with this index in the list.
        | OfferCommand of index: int * OrderViewCommand * request: string


    /// What the App carries out for the order context machine.
    [<RequireQualifiedAccess>]
    type OrderContextEffect =
        /// Send the order context command under this request id.
        | CallContext of OrderContextCommand * request: string
        /// Put the filter on the formulary and parenteralia pages.
        | SyncPages of Filter
        /// Tell the user what went wrong.
        | TellError of string[]
        /// Tell the user the change could not be applied, and why.
        | TellNotApplied of OrderContextRefusal


    /// What the page reads of the OrderContextState. The page renders Settled, Refused and Changing
    /// alike, so the screen stays filled during a request and the picks stay after a refusal. The
    /// dialog steps from any of them; other commands are built from Settled and Refused only.
    [<RequireQualifiedAccess>]
    type OrderContextView =
        /// No patient data, so no workbench.
        | NoPatientData
        /// The context the server evaluated.
        | Settled of OrderContext
        /// The context the server refused, as sent, with the reason.
        | Refused of OrderContext * OrderContextRefusal
        /// A request under way, with the context sent; the empty context during the first
        /// evaluation.
        | Changing of OrderContext


    /// Functions over OrderContextView.
    module OrderContextView =

        /// The context the view shows; None without patient data.
        let context view =
            match view with
            | OrderContextView.NoPatientData -> None
            | OrderContextView.Settled ctx
            | OrderContextView.Refused(ctx, _)
            | OrderContextView.Changing ctx -> Some ctx


        /// The context the order dialog shows for the plan page: the selected context, settled or
        /// changing as the plan is; None without a selection the plan still holds.
        let dialog plan =
            let pick (tp: OrderPlan) id = tp.OrderContexts |> Array.tryFind (fun c -> c.Id = id)

            match plan with
            | OrderPlanMachine.OrderPlanView.Settled(tp, Some id) -> pick tp id |> Option.map OrderContextView.Settled
            | OrderPlanMachine.OrderPlanView.Changing(tp, Some id) -> pick tp id |> Option.map OrderContextView.Changing
            | OrderPlanMachine.OrderPlanView.Settled(_, None)
            | OrderPlanMachine.OrderPlanView.Changing(_, None)
            | OrderPlanMachine.OrderPlanView.NoPatientData -> None


    /// The constructors and the transition of the order context machine.
    module OrderContextState =

        /// No patient data, nothing under way: the medication workbench.
        let noPatientData =
            {
                Category = OrderCategory.Medication
                Workbench = OrderContextWorkbench.NoPatientData None
                InFlight = None
                Selected = None
                BeforeRecalculation = None
                LinkedOffers = [||]
            }


        /// A nutrition slot of the category, without patient data, with nothing under way.
        let slot category =
            { noPatientData with Category = OrderCategory.Nutrition category }


        /// An empty medication context for the patient.
        let emptyMedication pat = OrderContextWorkbench.emptyOf OrderCategory.Medication pat


        /// A medication state: the context evaluated for the patient, nothing under way.
        let evaluated pat ctx =
            { noPatientData with Workbench = OrderContextWorkbench.Evaluated(pat, ctx) }


        /// A medication state: a command under way over the context sent; a failure goes back to the
        /// context before the command.
        let changing pat cmd (sent: OrderContext) before request =
            { evaluated pat before with
                InFlight =
                    RequestUnderWay.create
                        (OrderContextCommand.Command(cmd, { sent with Patient = pat }))
                        request
                        RequestFor.Context
                    |> Some
            }


        /// A medication state: the first evaluation for the patient under way, with the empty context shown.
        let opening pat request =
            let empty = emptyMedication pat
            changing pat OrderViewCommand.ClearAllFilterProperty empty empty request


        /// A medication state: the context refused for the patient, as sent, with the reason.
        let refused pat ctx refusal =
            { noPatientData with Workbench = OrderContextWorkbench.Refused(pat, ctx, refusal) }


        /// The patient the workbench is evaluated for, if any.
        let patient state =
            state.Workbench |> OrderContextWorkbench.patientAndContext |> Option.map fst


        /// The context in the workbench, also while a request is under way: the empty context during
        /// the first evaluation and after a reset, otherwise the context of the last response. A failed
        /// request goes back to this context. None without patient data.
        let workbenchContext state =
            state.Workbench |> OrderContextWorkbench.patientAndContext |> Option.map snd


        /// What the page reads: while a request for the context is under way the context sent, as its command
        /// changes it; otherwise the context in the workbench, settled or refused.
        let view state =
            match state.Workbench, state.InFlight with
            | OrderContextWorkbench.NoPatientData _, _ -> OrderContextView.NoPatientData
            | _, Some({ For = RequestFor.Context } as r) -> OrderContextView.Changing(RequestUnderWay.shown r)
            | OrderContextWorkbench.Evaluated(_, ctx), _ -> OrderContextView.Settled ctx
            | OrderContextWorkbench.Refused(_, ctx, refusal), _ -> OrderContextView.Refused(ctx, refusal)


        /// The context shown, the one the view holds.
        let context state = state |> view |> OrderContextView.context


        /// The request id the state waits on; None while no request is under way.
        let inFlightRequest state = state.InFlight |> Option.map _.Request


        /// The scenario of the context with the order with this id, if any.
        let scenarioWith orderId (ctx: OrderContext) = ctx.Scenarios |> Array.tryFind (fun sc -> sc.Order.Id = orderId)


        /// The selection of the dialog, only while the context holds the scenario selected.
        let selectionIn ctx selected =
            selected |> Option.filter (fun id -> ctx |> scenarioWith id |> Option.isSome)


        /// The workbench narrowed to the scenario with the order with this id and its form, as it
        /// goes into the plan; None unless the workbench is settled and shows that scenario.
        let narrowedTo orderId state =
            match view state with
            | OrderContextView.Settled ctx ->
                ctx
                |> scenarioWith orderId
                |> Option.map (fun sc -> ctx |> OrderContextWorkbench.narrowedToScenario sc)
            | OrderContextView.NoPatientData
            | OrderContextView.Refused _
            | OrderContextView.Changing _ -> None


        /// The orders the link rules offer to link to the order of the context, each turned on or off; while a
        /// request for an offer is under way, that offer with the context sent, as its command changes it.
        let linkedOffers state =
            match state.InFlight with
            | Some({ For = RequestFor.Offer index } as r) ->
                state.LinkedOffers
                |> LinkedOffer.changedAt index (fun o -> { o with Context = RequestUnderWay.shown r })
            | Some { For = RequestFor.Context }
            | None -> state.LinkedOffers


        /// The linked offers turned on.
        let turnedOn state =
            state.LinkedOffers |> Array.filter (fun o -> o.Switch = OfferSwitch.On)


        /// The linked offers turned on and narrowed to a single scenario, as the totals count them; an offer
        /// turned on with several scenarios or none is left out.
        let linkedTurnedOn state = state |> turnedOn |> Array.choose LinkedOffer.narrowed


        /// The linked offers turned on, each narrowed to a single scenario, as they go into the plan with
        /// the main order; None while an offer turned on holds several scenarios or none, which refuses
        /// the prescribe.
        let linkedToPrescribe state =
            let narrowed = state |> linkedTurnedOn

            if narrowed.Length = (state |> turnedOn).Length then
                Some narrowed
            else
                None


        /// Whether the recalculation list is open, with the state before the recalculation to put back
        /// when the list closes without a pick.
        let isRecalculating state = state.BeforeRecalculation.IsSome


        /// The state with the selection of the dialog, only when the context shown holds the scenario
        /// selected; None closes the dialog.
        let select id state =
            match context state with
            | None -> state
            | Some ctx -> { state with Selected = selectionIn ctx id }


        /// What the order dialog reads: the view while a scenario is selected, None while it is
        /// closed.
        let dialog state = state.Selected |> Option.map (fun _ -> view state)


        /// The filter the formulary and parenteralia pages take from a response: the filter of the
        /// context of the response, evaluated or refused, when the command sent changed the filter; None for
        /// any other command, so a dose step fetches neither page, and for a failure, which leaves the
        /// pages as they were.
        let synced cmd result =
            // a pick or a clear of the filter, the diluent or the components, or a seed changes the filter
            let follows =
                match cmd with
                | OrderViewCommand.SetNthFilterProperty _
                | OrderViewCommand.ClearFilterProperty _
                | OrderViewCommand.ClearAllFilterProperty
                | OrderViewCommand.SetNthDiluentProperty _
                | OrderViewCommand.ClearDiluentProperty
                | OrderViewCommand.SetNthComponentsProperty _
                | OrderViewCommand.SeedFilter _ -> true
                | _ -> false

            match result with
            | Ok(OrderContextResponse.Evaluated(ctx, _))
            | Ok(OrderContextResponse.Refused(ctx, _)) when follows -> Some ctx.Filter
            | _ -> None


        /// The next state and effects for a message.
        ///
        /// While a request is under way only a response, a selection, a restore and the patient cleared
        /// reach the workbench. Every other message comes only with nothing under way, since the pages
        /// that send it are disabled; with a request out it falls to the closing arm, which leaves the
        /// state as it is.
        ///
        /// The dialog closes on a patient data update, a seed and a reset. After a response the dialog
        /// keeps its selection only while the context of the response holds the scenario selected.
        ///
        /// A recalculation stores the state before it, which the response carries along and a restore puts
        /// back; any other message closes the recalculation list. The state before the recalculation has
        /// nothing under way, so after a restore the response to the recalculation finds no request to land
        /// on.
        let transition msg state =
            // the one place a request goes out
            let send sent request target state =
                { state with InFlight = Some(RequestUnderWay.create sent request target) },
                [ OrderContextEffect.CallContext(sent, request) ]

            let call cmd ctx request state =
                send (OrderContextCommand.Command(cmd, ctx)) request RequestFor.Context state

            // the state to put back when the list of a recalculation closes without a pick; None for any
            // other command, which closes the recalculation list
            let beforeRecalculation cmd =
                if IntentPolicy.isReCalc cmd then
                    Some { state with BeforeRecalculation = None }
                else
                    None

            // the server clears the argumentation before it evaluates a reset of the order scenario, so a
            // refused reset comes back without it; the context of the response takes the argumentation the
            // context had before the request
            let withArgumentationOf (before: OrderContext) response =
                { response with Argumentation = before.Argumentation }

            // the offer with the index changed
            let withOffer index change state =
                { state with LinkedOffers = state.LinkedOffers |> LinkedOffer.changedAt index change }

            // a new workbench, the dialog and the recalculation list closed, and the linked offers gone with the
            // order
            let renewed workbench =
                { state with
                    Workbench = workbench
                    Selected = None
                    BeforeRecalculation = None
                    LinkedOffers = [||]
                }

            // the empty workbench of its category for the patient, shown while the command evaluates it
            let fromEmpty cmd pat request =
                let empty = OrderContextWorkbench.emptyOf state.Category pat
                call cmd empty request (renewed (OrderContextWorkbench.Evaluated(pat, empty)))

            match msg, OrderContextWorkbench.patientAndContext state.Workbench, state.InFlight with
            // only a response to the request under way lands. A response for an offer changes that offer only: a
            // refusal keeps the offer as the server sent it back, a failure leaves it as it was. The formulary and
            // parenteralia pages do not follow a response for an offer
            | OrderContextMsg.Answered(request, result), Some _, Some({ For = RequestFor.Offer index } as r) when
                r.Request = request
                ->
                let settled = { state with InFlight = None }

                match result with
                // an offer takes no offers of its own: a link is one level
                | Ok(OrderContextResponse.Evaluated(ctx, _)) ->
                    settled |> withOffer index (fun o -> { o with Context = ctx }), []
                | Ok(OrderContextResponse.Refused(back, refusal)) ->
                    settled
                    |> withOffer index (fun o -> { o with Context = back |> withArgumentationOf o.Context }),
                    [ OrderContextEffect.TellNotApplied refusal ]
                | Error errs -> settled, [ OrderContextEffect.TellError errs ]
            // the formulary and parenteralia pages follow the response for the context when the request
            // changed the filter
            | OrderContextMsg.Answered(request, result), Some(pat, before), Some({ For = RequestFor.Context } as r) when
                r.Request = request
                ->
                let (OrderContextCommand.Command(cmd, _)) = r.Sent

                let workbench, told =
                    match result with
                    // the offers the response carries are taken below
                    | Ok(OrderContextResponse.Evaluated(ctx, _)) -> OrderContextWorkbench.Evaluated(pat, ctx), []
                    // for a change that cannot be applied, or that sets a dose per kg the order does not
                    // have, the response is the context as it was sent, and the dialog stays open
                    | Ok(OrderContextResponse.Refused(back, refusal)) when
                        refusal |> OrderContextRefusalPolicy.refusesChange
                        ->
                        OrderContextWorkbench.Evaluated(pat, back |> withArgumentationOf before),
                        [ OrderContextEffect.TellNotApplied refusal ]
                    | Ok(OrderContextResponse.Refused(back, refusal)) ->
                        OrderContextWorkbench.Refused(pat, { back with Scenarios = [||] }, refusal), []
                    // a failed change leaves the workbench as it was, not as sent
                    | Error errs -> OrderContextWorkbench.Evaluated(pat, before), [ OrderContextEffect.TellError errs ]

                let selected =
                    workbench
                    |> OrderContextWorkbench.patientAndContext
                    |> Option.bind (fun (_, ctx) -> selectionIn ctx state.Selected)

                // a change of the choices or the scenario takes the offers the response carries, none for a
                // refusal; any other command leaves the scenario, and so the offers, as they were
                let linked =
                    match result with
                    | Ok(OrderContextResponse.Evaluated(_, linked)) when OrderViewCommand.offersLinked cmd ->
                        linked |> Array.map LinkedOffer.off
                    | Ok(OrderContextResponse.Refused _) when OrderViewCommand.offersLinked cmd -> [||]
                    | _ -> state.LinkedOffers

                { state with
                    Workbench = workbench
                    InFlight = None
                    Selected = selected
                    LinkedOffers = linked
                },
                told
                @ (synced cmd result |> Option.map OrderContextEffect.SyncPages |> Option.toList)
            | OrderContextMsg.Answered _, _, _ -> state, []

            // the selection needs no request, and closes the recalculation list
            | OrderContextMsg.SelectScenario id, _, _ -> select id { state with BeforeRecalculation = None }, []
            | OrderContextMsg.RestoreField, _, _ ->
                match state.BeforeRecalculation with
                | Some before -> before, []
                | None -> state, []
            // turning a linked offer on or off needs no request
            | OrderContextMsg.TurnOfferOn index, _, _ ->
                state |> withOffer index (fun o -> { o with Switch = OfferSwitch.On }), []
            | OrderContextMsg.TurnOfferOff index, _, _ ->
                state |> withOffer index (fun o -> { o with Switch = OfferSwitch.Off }), []

            // the patient cleared reaches every state: no workbench, and the request out is dropped
            | OrderContextMsg.PatientDataChanged(None, _), _, _ -> noPatientData, []
            // the first patient data: the empty workbench is shown while it is evaluated, with the url
            // seed stored, if any
            | OrderContextMsg.PatientDataChanged(Some pat, request), None, None ->
                let cmd =
                    match state.Workbench with
                    | OrderContextWorkbench.NoPatientData(Some seed) -> FilterSeed.command seed
                    | _ -> OrderViewCommand.ClearAllFilterProperty

                fromEmpty cmd pat request
            // the same data given again is no change, so the selection stays
            | OrderContextMsg.PatientDataChanged(Some pat, _), Some(before, _), None when before = pat -> state, []
            // the selection was made under the old data: nothing of it stays, also after a failure
            | OrderContextMsg.PatientDataChanged(Some pat, request), Some _, None ->
                fromEmpty OrderViewCommand.ClearAllFilterProperty pat request

            // a seed needs patient data, so without it the seed is stored until the first patient data
            | OrderContextMsg.SeedFilter(seed, _), None, None ->
                renewed (OrderContextWorkbench.NoPatientData(Some seed)), []
            | OrderContextMsg.SeedFilter(seed, request), Some(pat, ctx), None ->
                call (FilterSeed.command seed) ctx request (renewed (OrderContextWorkbench.Evaluated(pat, ctx)))

            // a command over the context in the workbench, for the patient of the state. Sending replaces
            // a refused workbench with an evaluated one, so the workbench is refused only while no request
            // is under way
            | OrderContextMsg.Command(cmd, request), Some(pat, ctx), None ->
                call
                    cmd
                    { ctx with Patient = pat }
                    request
                    { state with
                        Workbench = OrderContextWorkbench.Evaluated(pat, ctx)
                        BeforeRecalculation = beforeRecalculation cmd
                    }

            // a command over the offer with the index, for the patient of the state, on the request of the
            // context; a recalculation stores the state before it, as a command over the context does
            | OrderContextMsg.OfferCommand(index, cmd, request), Some(pat, _), None ->
                match state.LinkedOffers |> Array.tryItem index with
                | Some offer ->
                    { state with BeforeRecalculation = beforeRecalculation cmd }
                    |> send
                        (OrderContextCommand.Command(cmd, { offer.Context with Patient = pat }))
                        request
                        (RequestFor.Offer index)
                | None -> state, []

            // a reset clears the filter of the context in the workbench; a failure goes back to the empty
            // workbench
            | OrderContextMsg.Reset request, Some(pat, ctx), None ->
                call
                    OrderViewCommand.ClearAllFilterProperty
                    ctx
                    request
                    (renewed (OrderContextWorkbench.Evaluated(pat, OrderContextWorkbench.emptyOf state.Category pat)))

            // the closing arm: nothing else comes, since the pages that send it are disabled, and
            // there is nothing to command or reset without patient data
            | _ -> state, []
