namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the prescribing workbench, or a nutrition slot: an order context not yet in the plan, of the
/// category held in the state, from no patient to an evaluated context, through changes and resets. The App
/// carries out the effects.
///
/// Four invariants:
/// - one request is in flight at a time: the pages are disabled while it is, so nothing but its
///   answer and the patient cleared reaches the machine meanwhile;
/// - an answer lands only on the request it names;
/// - the workbench is always evaluated for the patient held, and again when the patient changes;
/// - a filter needs a patient; one that arrives before the patient waits for it.
///
/// An answer is the context evaluated, the context refused with the reason, or a failure, after
/// which the workbench stays as it was.
module OrderContextMachine =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Api
    open FilterSeed


    /// The workbench itself, without any request under way.
    [<RequireQualifiedAccess>]
    type OrderContextWorkbench =
        /// No patient, so no workbench; the url's seed on an anonymous page load, which arrives
        /// before the patient, waits for it.
        | NoPatient of awaiting: FilterSeed option
        /// The patient and the context last evaluated for it, which a failed change goes back to: the
        /// empty context during the first evaluation and after a reset.
        | Evaluated of Patient * OrderContext
        /// The patient and the context the server refused, as sent, with its picks kept and its
        /// scenarios dropped, and the reason.
        | Refused of Patient * OrderContext * OrderContextRefusal


    /// Functions over OrderContextWorkbench.
    module OrderContextWorkbench =

        /// An empty context of the category for the patient.
        let emptyOf category (pat: Patient) =
            { (OrderContext.empty |> OrderContext.setPatient pat) with Category = category }


        /// An empty medication context for the patient.
        let emptyMedication (pat: Patient) = emptyOf OrderCategory.Medication pat


        /// The context narrowed to the scenario and its form, as it goes into the plan.
        let narrowedToScenario (sc: OrderScenario) (ctx: OrderContext) =
            { ctx with
                OrderContext.Filter.Form = Some sc.Form
                Scenarios = [| sc |]
            }


        /// Whether the command changes the filter, so that the formulary and parenteralia pages follow
        /// its answer: a pick or a clear of the filter, the diluent or the components, or a seed.
        let changesFilter (cmd: OrderViewCommand) =
            match cmd with
            | OrderViewCommand.SetNthFilterProperty _
            | OrderViewCommand.ClearFilterProperty _
            | OrderViewCommand.ClearAllFilterProperty
            | OrderViewCommand.SetNthDiluentProperty _
            | OrderViewCommand.ClearDiluentProperty
            | OrderViewCommand.SetNthComponentsProperty _
            | OrderViewCommand.SeedFilter _ -> true
            | _ -> false


        /// The context a request shows while it is under way: the context sent as the command
        /// changes it.
        let shown (OrderContextCommand.Command(cmd, ctx)) = CommandPreview.shown cmd ctx


        /// The patient and the context the workbench holds; None without a patient.
        let held (workbench: OrderContextWorkbench) =
            match workbench with
            | OrderContextWorkbench.NoPatient _ -> None
            | OrderContextWorkbench.Evaluated(pat, ctx)
            | OrderContextWorkbench.Refused(pat, ctx, _) -> Some(pat, ctx)


    /// Whether a linked offer goes into the plan with the main order.
    [<RequireQualifiedAccess>]
    type OfferSwitch =
        /// The offer goes into the plan with the main order, once it is narrowed to one scenario.
        | On
        /// The offer stays out of the plan.
        | Off


    /// An order the link rules offer to link to the order of the context, composed like any order context
    /// and turned on or off.
    type LinkedOffer =
        {
            /// The context the server offered, evaluated for the same patient, as its last answer left it.
            Context: OrderContext
            /// Whether the offer goes into the plan with the main order.
            Switch: OfferSwitch
        }


    /// Functions over LinkedOffer.
    module LinkedOffer =

        /// The offer of the context, turned off.
        let off (ctx: OrderContext) =
            {
                Context = ctx
                Switch = OfferSwitch.Off
            }


        /// The offers with the offer at the index changed.
        let changedAt index change (offers: LinkedOffer[]) =
            offers |> Array.mapi (fun i o -> if i = index then change o else o)


        /// The offer's context narrowed to its one scenario, as it goes into the plan; None while the
        /// context holds several scenarios or none.
        let narrowed (offer: LinkedOffer) =
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
            /// The request id the answer must name.
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
                Kept: OrderContextState option
                /// The orders the link rules offer to link to the order of the context: the offers that came
                /// with the last answer to a change of the choices or the scenario.
                LinkedOffers: LinkedOffer[]
            }


    /// What moves the order context machine. A message that starts a request carries its request id,
    /// so the answer can name it.
    [<RequireQualifiedAccess>]
    type OrderContextMsg =
        /// The patient data set, changed or cleared; the workbench is evaluated for it.
        | PatientDataChanged of Patient option * request: string
        /// A seed of the filter, sent as a command over the context shown; it waits for a patient.
        | SeedFilter of FilterSeed * request: string
        /// A command from the page, sent over the context held.
        | Command of OrderViewCommand * request: string
        /// The answer to the request with this id: the context evaluated or refused; Error is a
        /// failure of the server or the call.
        | Answered of request: string * Result<OrderContextResponse, string[]>
        /// Clear the workbench and evaluate it empty, after an order was prescribed.
        | Reset of request: string
        /// The scenario the dialog shows, by its order's id.
        | SelectScenario of string option
        /// The list of a recalculation closed without a pick: the context kept is put back, and the
        /// answer to the recalculation is dropped.
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
        /// No patient, so no workbench.
        | NoPatient
        /// The context the server evaluated.
        | Settled of OrderContext
        /// The context the server refused, as sent, with the reason.
        | Refused of OrderContext * OrderContextRefusal
        /// A request under way, with the context sent; the empty context during the first
        /// evaluation.
        | Changing of OrderContext


    /// Functions over OrderContextView.
    module OrderContextView =

        /// The context the order dialog shows for the plan page: the selected context, settled or
        /// changing as the plan is; None without a selection the plan still holds.
        let dialog (plan: OrderPlanMachine.OrderPlanView) : OrderContextView option =
            let pick (tp: OrderPlan) (id: string) = tp.OrderContexts |> Array.tryFind (fun c -> c.Id = id)

            match plan with
            | OrderPlanMachine.OrderPlanView.Settled(tp, Some id) -> pick tp id |> Option.map OrderContextView.Settled
            | OrderPlanMachine.OrderPlanView.Changing(tp, Some id) -> pick tp id |> Option.map OrderContextView.Changing
            | OrderPlanMachine.OrderPlanView.Settled(_, None)
            | OrderPlanMachine.OrderPlanView.Changing(_, None)
            | OrderPlanMachine.OrderPlanView.NoPatient -> None


    /// The constructors and the transition of the order context machine.
    module OrderContextState =

        /// No patient, nothing under way: the medication workbench.
        let noPatient =
            {
                Category = OrderCategory.Medication
                Workbench = OrderContextWorkbench.NoPatient None
                InFlight = None
                Selected = None
                Kept = None
                LinkedOffers = [||]
            }


        /// A nutrition slot of the category, without a patient and nothing under way.
        let slot category = { noPatient with Category = OrderCategory.Nutrition category }


        /// An empty medication context for the patient.
        let emptyMedication = OrderContextWorkbench.emptyMedication


        /// A medication state: the first evaluation for the patient under way, with the empty context shown.
        let opening (pat: Patient) (request: string) =
            { noPatient with
                Workbench = OrderContextWorkbench.Evaluated(pat, emptyMedication pat)
                InFlight =
                    RequestUnderWay.create
                        (OrderContextCommand.Command(OrderViewCommand.ClearAllFilterProperty, emptyMedication pat))
                        request
                        RequestFor.Context
                    |> Some
            }


        /// A medication state: the context evaluated for the patient, nothing under way.
        let held (pat: Patient) (ctx: OrderContext) =
            { noPatient with Workbench = OrderContextWorkbench.Evaluated(pat, ctx) }


        /// A medication state: the context refused for the patient, as sent, with the reason.
        let refused (pat: Patient) (ctx: OrderContext) (refusal: OrderContextRefusal) =
            { held pat ctx with Workbench = OrderContextWorkbench.Refused(pat, ctx, refusal) }


        /// A medication state: a command under way over the context sent; a failure goes back to the context
        /// held.
        let changing
            (pat: Patient)
            (cmd: OrderViewCommand)
            (sent: OrderContext)
            (held: OrderContext)
            (request: string)
            =
            { noPatient with
                Workbench = OrderContextWorkbench.Evaluated(pat, held)
                InFlight =
                    RequestUnderWay.create
                        (OrderContextCommand.Command(cmd, { sent with Patient = pat }))
                        request
                        RequestFor.Context
                    |> Some
            }


        /// The patient the workbench is evaluated for, if any.
        let patient (state: OrderContextState) = state.Workbench |> OrderContextWorkbench.held |> Option.map fst


        /// The context the workbench holds, also while a request is under way: the last one answered,
        /// or the empty one after a reset and during the first evaluation, which a failure goes back
        /// to. None without a patient.
        let answered (state: OrderContextState) = state.Workbench |> OrderContextWorkbench.held |> Option.map snd


        /// What the page reads: while a request for the context is under way the context sent, as its command
        /// changes it; otherwise the context held, settled or refused.
        let view (state: OrderContextState) : OrderContextView =
            match state.Workbench, state.InFlight with
            | OrderContextWorkbench.NoPatient _, _ -> OrderContextView.NoPatient
            | _, Some({ For = RequestFor.Context } as r) ->
                OrderContextView.Changing(OrderContextWorkbench.shown r.Sent)
            | OrderContextWorkbench.Evaluated(_, ctx), _ -> OrderContextView.Settled ctx
            | OrderContextWorkbench.Refused(_, ctx, refusal), _ -> OrderContextView.Refused(ctx, refusal)


        /// The context shown, the one the view holds.
        let context (state: OrderContextState) =
            match view state with
            | OrderContextView.NoPatient -> None
            | OrderContextView.Settled ctx
            | OrderContextView.Refused(ctx, _)
            | OrderContextView.Changing ctx -> Some ctx


        /// The request id the state waits on; None while no request is under way.
        let inFlightRequest (state: OrderContextState) = state.InFlight |> Option.map _.Request


        /// The workbench narrowed to the scenario with the order with this id and its form, as it
        /// goes into the plan; None unless the workbench is settled and shows that scenario.
        let narrowedTo (orderId: string) (state: OrderContextState) =
            match view state with
            | OrderContextView.Settled ctx ->
                ctx.Scenarios
                |> Array.tryFind (fun sc -> sc.Order.Id = orderId)
                |> Option.map (fun sc -> ctx |> OrderContextWorkbench.narrowedToScenario sc)
            | OrderContextView.NoPatient
            | OrderContextView.Refused _
            | OrderContextView.Changing _ -> None


        /// The orders the link rules offer to link to the order of the context, each turned on or off; while a
        /// request for an offer is under way, that offer with the context sent, as its command changes it.
        let linkedOffers (state: OrderContextState) =
            match state.InFlight with
            | Some({ For = RequestFor.Offer index } as r) ->
                state.LinkedOffers
                |> LinkedOffer.changedAt index (fun o -> { o with Context = OrderContextWorkbench.shown r.Sent })
            | Some { For = RequestFor.Context }
            | None -> state.LinkedOffers


        /// The linked offers turned on.
        let turnedOn (state: OrderContextState) =
            state.LinkedOffers |> Array.filter (fun o -> o.Switch = OfferSwitch.On)


        /// The linked offers turned on and narrowed to one scenario, as the totals count them; an offer turned
        /// on with several scenarios or none is left out.
        let linkedTurnedOn (state: OrderContextState) = state |> turnedOn |> Array.choose LinkedOffer.narrowed


        /// The linked offers turned on, each narrowed to its one scenario, as they go into the plan with the
        /// main order; None while an offer turned on is not narrowed to one scenario, which refuses the
        /// prescribe.
        let linkedToPrescribe (state: OrderContextState) =
            Some(state |> linkedTurnedOn)
            |> Option.filter (fun narrowed -> narrowed.Length = (state |> turnedOn).Length)


        /// Whether the state before a recalculation is kept, to be put back when its list closes without a pick.
        let isKept (state: OrderContextState) = state.Kept.IsSome


        /// Whether the context has a scenario with the order with this id.
        let holds (orderId: string) (ctx: OrderContext) =
            ctx.Scenarios |> Array.exists (fun sc -> sc.Order.Id = orderId)


        /// The state with the dialog's selection, kept only when the context shown holds the scenario;
        /// None closes the dialog.
        let select (id: string option) (state: OrderContextState) =
            match context state with
            | None -> state
            | Some ctx -> { state with Selected = id |> Option.filter (fun id -> ctx |> holds id) }


        /// What the order dialog reads: the view while a scenario is selected, None while it is
        /// closed.
        let dialog (state: OrderContextState) : OrderContextView option =
            state.Selected |> Option.map (fun _ -> view state)


        /// The filter the formulary and parenteralia pages take from an answer: the filter of the
        /// context answered, evaluated or refused, when the request sent changed the filter; None for
        /// any other request, so a dose step fetches neither page, and for a failure, which leaves the
        /// pages as they were.
        let synced (OrderContextCommand.Command(cmd, _)) result =
            let follows = OrderContextWorkbench.changesFilter cmd

            match result with
            | Ok(OrderContextResponse.Evaluated ctx)
            | Ok(OrderContextResponse.Refused(ctx, _)) when follows -> Some ctx.Filter
            | _ -> None


        /// The next state and effects for a message. Only an answer, a selection, a restore and the
        /// patient cleared reach the workbench while a request is under way; every other message
        /// comes only with nothing under way, since the pages that send it are disabled, and with a
        /// request out falls to the closing arm, which leaves the state as it is.
        ///
        /// The dialog closes on a patient change, a seed and a reset, and after an answer keeps its
        /// selection only while the context answered holds it. A recalculation keeps the state before it,
        /// which an answer carries along and a restore puts back; any other message closes the recalculation list.
        /// The state kept has nothing under way, so the answer to the clear finds no request to land
        /// on after a restore.
        let transition (msg: OrderContextMsg) (state: OrderContextState) : OrderContextState * OrderContextEffect list =
            // the one place a request goes out
            let send sent request target (state: OrderContextState) =
                { state with InFlight = Some(RequestUnderWay.create sent request target) },
                [ OrderContextEffect.CallContext(sent, request) ]

            let call cmd ctx request state =
                send (OrderContextCommand.Command(cmd, ctx)) request RequestFor.Context state

            // a command from the page over the context held, for the patient held; a refusal goes,
            // so the workbench is refused only with nothing under way
            let command cmd pat ctx request kept =
                call
                    cmd
                    { ctx with Patient = pat }
                    request
                    { state with
                        Workbench = OrderContextWorkbench.Evaluated(pat, ctx)
                        Kept = kept
                    }

            // the offer with the index changed
            let withOffer index change state =
                { state with LinkedOffers = state.LinkedOffers |> LinkedOffer.changedAt index change }

            // a new workbench, the dialog and the recalculation list closed, and the linked offers gone with the
            // order
            let renewed workbench =
                { state with
                    Workbench = workbench
                    Selected = None
                    Kept = None
                    LinkedOffers = [||]
                }

            // the empty workbench of its category for the patient, shown while the command evaluates it
            let fromEmpty cmd pat request =
                let empty = OrderContextWorkbench.emptyOf state.Category pat
                call cmd empty request (renewed (OrderContextWorkbench.Evaluated(pat, empty)))

            match msg, OrderContextWorkbench.held state.Workbench, state.InFlight with
            // only an answer to the request under way lands. An answer for an offer changes that offer only: a
            // refusal keeps the offer as the server sent it back, a failure leaves it as it was. The formulary and
            // parenteralia pages follow the answer for the context, not the request, and not an answer for an offer
            | OrderContextMsg.Answered(request, result), Some _, Some({ For = RequestFor.Offer index } as r) when
                r.Request = request
                ->
                let answered = { state with InFlight = None }

                match result with
                | Ok(OrderContextResponse.Evaluated ctx) ->
                    answered |> withOffer index (fun o -> { o with Context = ctx }), []
                // a reset is sent without the argumentation, so the one held is kept
                | Ok(OrderContextResponse.Refused(back, refusal)) ->
                    answered
                    |> withOffer
                        index
                        (fun o -> { o with Context = { back with Argumentation = o.Context.Argumentation } }),
                    [ OrderContextEffect.TellNotApplied refusal ]
                | Error errs -> answered, [ OrderContextEffect.TellError errs ]
            | OrderContextMsg.Answered(request, result), Some(pat, held), Some({ For = RequestFor.Context } as r) when
                r.Request = request
                ->
                let sent = r.Sent

                let workbench, told =
                    match result with
                    | Ok(OrderContextResponse.Evaluated ctx) -> OrderContextWorkbench.Evaluated(pat, ctx), []
                    // a change that cannot be applied, or that sets a dose per kg the order does not
                    // have, answers the context as it was sent; a reset is sent without the
                    // argumentation, so the one held is kept; the dialog stays open
                    | Ok(OrderContextResponse.Refused(back, refusal)) when
                        refusal |> OrderContextRefusalPolicy.refusesChange
                        ->
                        OrderContextWorkbench.Evaluated(pat, { back with Argumentation = held.Argumentation }),
                        [ OrderContextEffect.TellNotApplied refusal ]
                    | Ok(OrderContextResponse.Refused(back, refusal)) ->
                        OrderContextWorkbench.Refused(pat, { back with Scenarios = [||] }, refusal), []
                    // a failed change leaves the workbench as it was, not as sent
                    | Error errs -> OrderContextWorkbench.Evaluated(pat, held), [ OrderContextEffect.TellError errs ]

                let selected =
                    workbench
                    |> OrderContextWorkbench.held
                    |> Option.bind (fun (_, ctx) -> state.Selected |> Option.filter (fun id -> ctx |> holds id))

                // a change of the choices or the scenario takes the offers the answer carries, none for a
                // refusal; any other command leaves the scenario, and so the offers, as they were
                let linked =
                    let (OrderContextCommand.Command(cmd, _)) = sent

                    match result with
                    | Ok(OrderContextResponse.Evaluated ctx) when OrderViewCommand.offersLinked cmd ->
                        ctx.LinkedOrders |> Array.map LinkedOffer.off
                    | Ok(OrderContextResponse.Refused _) when OrderViewCommand.offersLinked cmd -> [||]
                    | _ -> state.LinkedOffers

                { state with
                    Workbench = workbench
                    InFlight = None
                    Selected = selected
                    LinkedOffers = linked
                },
                told
                @ (synced sent result |> Option.map OrderContextEffect.SyncPages |> Option.toList)
            | OrderContextMsg.Answered _, _, _ -> state, []

            // the selection needs no request, and closes the recalculation list
            | OrderContextMsg.SelectScenario id, _, _ -> select id { state with Kept = None }, []
            | OrderContextMsg.RestoreField, _, _ ->
                match state.Kept with
                | Some kept -> kept, []
                | None -> state, []
            // turning a linked offer on or off needs no request
            | OrderContextMsg.TurnOfferOn index, _, _ ->
                state |> withOffer index (fun o -> { o with Switch = OfferSwitch.On }), []
            | OrderContextMsg.TurnOfferOff index, _, _ ->
                state |> withOffer index (fun o -> { o with Switch = OfferSwitch.Off }), []

            // the patient cleared reaches every state: no workbench, and the request out is dropped
            | OrderContextMsg.PatientDataChanged(None, _), _, _ -> noPatient, []
            // the first patient: the empty workbench is shown while it is evaluated, with the seed
            // waiting, if any
            | OrderContextMsg.PatientDataChanged(Some pat, request), None, None ->
                let cmd =
                    match state.Workbench with
                    | OrderContextWorkbench.NoPatient(Some seed) -> FilterSeed.command seed
                    | _ -> OrderViewCommand.ClearAllFilterProperty

                fromEmpty cmd pat request
            // the same data given again is no change, so the selection stays
            | OrderContextMsg.PatientDataChanged(Some pat, _), Some(before, _), None when before = pat -> state, []
            // the selection was made under the old data: nothing of it stays, also after a failure
            | OrderContextMsg.PatientDataChanged(Some pat, request), Some _, None ->
                fromEmpty OrderViewCommand.ClearAllFilterProperty pat request

            // a seed needs a patient, so without one it waits for it
            | OrderContextMsg.SeedFilter(seed, _), None, None ->
                renewed (OrderContextWorkbench.NoPatient(Some seed)), []
            | OrderContextMsg.SeedFilter(seed, request), Some(pat, ctx), None ->
                call (FilterSeed.command seed) ctx request (renewed (OrderContextWorkbench.Evaluated(pat, ctx)))

            // a command, over the context held, for the patient held; a recalculation keeps the
            // state before it
            | OrderContextMsg.Command(cmd, request), Some(pat, ctx), None ->
                let kept =
                    if IntentPolicy.isReCalc cmd then
                        Some { state with Kept = None }
                    else
                        None

                command cmd pat ctx request kept

            // a command over the offer with the index, for the patient held, on the request of the context; a
            // recalculation keeps the state before it, as a command over the context does
            | OrderContextMsg.OfferCommand(index, cmd, request), Some(pat, _), None ->
                match state.LinkedOffers |> Array.tryItem index with
                | Some offer ->
                    let kept =
                        if IntentPolicy.isReCalc cmd then
                            Some { state with Kept = None }
                        else
                            None

                    { state with Kept = kept }
                    |> send
                        (OrderContextCommand.Command(cmd, { offer.Context with Patient = pat }))
                        request
                        (RequestFor.Offer index)
                | None -> state, []

            // a reset clears the filter of the context held; a failure goes back to the empty workbench
            | OrderContextMsg.Reset request, Some(pat, ctx), None ->
                call
                    OrderViewCommand.ClearAllFilterProperty
                    ctx
                    request
                    (renewed (OrderContextWorkbench.Evaluated(pat, OrderContextWorkbench.emptyOf state.Category pat)))

            // the closing arm: nothing else comes, since the pages that send it are disabled, and
            // there is nothing to command or reset without a patient
            | _ -> state, []
