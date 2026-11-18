namespace Informedica.GenPRES.Client.Core

/// Connects the state machines of the client. A message runs the machine it is for, and an effect meant
/// for another machine becomes a message for that machine in the same transition, not in a later update.
/// Every effect is also returned, so that the App can do the part that leaves the client.
module Lanes =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Client.Core.MachinePolicies
    open Informedica.GenPRES.Shared.Types
    open PatientMachine
    open SessionMachine
    open SigningMachine
    open OrderPlanMachine
    open OrderContextMachine


    /// The states of all state machines of the client.
    type LanesState =
        {
            /// The state of the patient machine: the patient data draft and the pending patient request.
            Patient: PatientState
            /// The order context state of the prescribing workbench.
            OrderContext: OrderContextState
            /// The order context state of each nutrition slot, by slot id, in the order the slots were started.
            Slots: (string * OrderContextState) list
            /// The state of the order plan machine.
            OrderPlan: OrderPlanState
            /// The session state; a patient from a url runs in the Anonymous state.
            Session: SessionState
            /// The signing phase of the open Session; Idle whenever no Session is open.
            Signing: SigningState
        }


    /// A message for one of the state machines.
    [<RequireQualifiedAccess>]
    type LanesMsg =
        | Signing of SigningMsg
        | Session of SessionMsg
        | Patient of PatientMsg
        | Workbench of OrderContextMsg
        /// A message for the nutrition slot with the id; a message for a removed slot is dropped. A
        /// PatientDataChanged message without patient data removes the slot, because another patient
        /// removes the slots.
        | Slot of slotId: string * OrderContextMsg
        /// Starts a nutrition slot of the category under a slot id made by the client, for the patient data
        /// of the workbench. No slot starts when the workbench has no patient data.
        | StartSlot of slotId: string * NutritionCategory * request: string
        /// Removes the nutrition slot with the id; a later answer for the slot finds no slot and is dropped.
        | RemoveSlot of slotId: string
        | Plan of OrderPlanMsg
        /// A server answer with an optional record notice. The answer goes to its machine and the notice
        /// goes to the session.
        | Answer of LanesMsg * from: OpenedToken option * RecordNotice option
        /// Adds the order with the order id from the workbench, or from the nutrition slot with the slot id, to
        /// the plan, with the linked orders that are turned on. The workbench or the slot is emptied when the
        /// plan has added the orders.
        | Prescribe of slotId: string option * orderId: string * request: string
        /// A url brought a patient, a medication or a launch. Sets the patient, the workbench, the slots, the
        /// plan and the signing back to their state at page load with this patient data, and closes the
        /// session. A launch is not part of this message: the App passes it to the session after the browser
        /// key is made, and the session opens it after the close is answered.
        | StartOver of draft: PatientData option


    /// An effect of one of the state machines, or of the lanes.
    [<RequireQualifiedAccess>]
    type LanesEffect =
        | Signing of SigningEffect
        | Patient of PatientEffect
        | Workbench of OrderContextEffect
        /// An effect of the nutrition slot with the id.
        | Slot of slotId: string * OrderContextEffect
        | Plan of OrderPlanEffect
        | Session of SessionEffect
        /// Open the plan page: an order was prescribed.
        | GoToPlanPage
        /// The patient data is cleared and new patient data follows, so the pages send no requests for
        /// the cleared state.
        | PatientExpected


    /// A step of a transition, for the trail: a message of a machine with its new state and effects, a
    /// removed slot, or a start-over.
    [<RequireQualifiedAccess>]
    type LanesStep =
        | Signing of SigningMsg * SigningState * SigningEffect list
        | Patient of PatientMsg * PatientState * PatientEffect list
        | Workbench of OrderContextMsg * OrderContextState * OrderContextEffect list
        | Slot of slotId: string * OrderContextMsg * OrderContextState * OrderContextEffect list
        /// The nutrition slot with the id taken out.
        | SlotRemoved of slotId: string
        | Plan of OrderPlanMsg * OrderPlanState * OrderPlanEffect list
        | Session of SessionMsg * SessionState * SessionEffect list
        /// The patient, the workbench, the slots, the plan and the signing set back to their initial state
        /// by a start-over, not by a message of their own machine.
        | StartedOver of LanesState


    /// Returns the nutrition slot with the id, or None for a removed slot.
    let slotOf id (lanes: LanesState) = lanes.Slots |> List.tryFind (fst >> (=) id) |> Option.map snd


    /// Replaces the nutrition slot with the id, keeping its position.
    let withSlot id next (lanes: LanesState) =
        { lanes with Slots = lanes.Slots |> List.map (fun (i, s) -> i, (if i = id then next else s)) }


    /// The lanes without the nutrition slot with the id.
    let withoutSlot id (lanes: LanesState) =
        { lanes with Slots = lanes.Slots |> List.filter (fst >> (<>) id) }


    /// The lanes at the page load: the patient from the url, if any, every other lane empty and no slots.
    let initial draft =
        {
            Patient = PatientState.init draft
            OrderContext = OrderContextState.noPatientData
            Slots = []
            OrderPlan = OrderPlanState.noPatientData
            Session = SessionState.anonymous
            Signing = SigningState.idle
        }


    /// Runs one message on its machine and returns the new lanes, the effects and the steps. When a
    /// session message ends an open session, also tells the signing machine that the session ended.
    let rec step msg (lanes: LanesState) =
        match msg with
        | LanesMsg.Signing m ->
            let next, effects = SigningState.transition m lanes.Signing
            { lanes with Signing = next },
            effects |> List.map LanesEffect.Signing,
            [ LanesStep.Signing(m, next, effects) ]
        // While a signature is submitted, the signature makes a new token, so a reply to another request
        // can carry an inactive tab notice that is not true. The notice is ignored; the next request shows
        // whether the tab is inactive. Every notice passes here, also one from the medication names
        // request, which can run during a signature.
        | LanesMsg.Session(SessionMsg.NoticeReceived(_, RecordNotice.Ended SessionEnding.InactiveTab)) when
            (match lanes.Signing |> SigningState.view with
             | SigningView.Submitting _ -> true
             | _ -> false)
            ->
            lanes, [], []
        | LanesMsg.Session m ->
            let next, effects = SessionState.transition m lanes.Session
            let before = SessionState.view lanes.Session

            // A session that opens while the page has patient data clears that data first, so that no
            // version opens on the data of the previous patient.
            let opensOverPatient =
                match before, SessionState.view next with
                | SessionView.Open _, _ -> false
                | _, SessionView.Open _ -> (lanes.Patient |> PatientState.draft).IsSome
                | _ -> false

            let lanes, cleared, clearing =
                if opensOverPatient then
                    // Clearing the patient data sends no request, so the request id is never used.
                    step
                        (LanesMsg.Patient(PatientMsg.Changed(None, PatientDraftPolicy.Estimates.Renewed, "cleared")))
                        lanes
                else
                    lanes, [], []

            let lanes = { lanes with Session = next }
            let taken = clearing @ [ LanesStep.Session(m, next, effects) ]

            let expected =
                match SessionState.view next with
                | SessionView.Open opened when
                    opensOverPatient && (opened.PatientContext |> Option.bind _.PatientData).IsSome
                    ->
                    [ LanesEffect.PatientExpected ]
                | _ -> []

            let effects = expected @ cleared @ (effects |> List.map LanesEffect.Session)

            // A signature belongs to an open session. The step that ends the open session tells the signing
            // machine once; a pending submission still reports its outcome. The orders in the plan stay,
            // unsigned.
            match before, SessionState.view next, SigningState.view lanes.Signing with
            | SessionView.Open _, SessionView.Open _, _
            | _, _, SigningView.Idle -> lanes, effects, taken
            | SessionView.Open _, _, _ ->
                let lanes, told, toldTaken = step (LanesMsg.Signing SigningMsg.SessionEnded) lanes
                lanes, effects @ told, taken @ toldTaken
            | _ -> lanes, effects, taken
        | LanesMsg.Patient m ->
            let next, effects = PatientState.transition m lanes.Patient
            { lanes with Patient = next },
            effects |> List.map LanesEffect.Patient,
            [ LanesStep.Patient(m, next, effects) ]
        | LanesMsg.Plan m ->
            let next, effects = OrderPlanState.transition m lanes.OrderPlan
            { lanes with OrderPlan = next }, effects |> List.map LanesEffect.Plan, [ LanesStep.Plan(m, next, effects) ]
        | LanesMsg.Workbench m ->
            let next, effects = OrderContextState.transition m lanes.OrderContext

            { lanes with OrderContext = next },
            effects |> List.map LanesEffect.Workbench,
            [ LanesStep.Workbench(m, next, effects) ]
        // Another patient arrives as cleared patient data, which removes the slot.
        | LanesMsg.Slot(id, OrderContextMsg.PatientDataChanged(None, _)) -> lanes |> withoutSlot id, [], []
        // an answer to a slot that another patient or a start-over removed finds no slot and is dropped
        | LanesMsg.Slot(id, m) ->
            match lanes |> slotOf id with
            | Some slot ->
                let next, effects = OrderContextState.transition m slot

                lanes |> withSlot id next,
                effects |> List.map (fun e -> LanesEffect.Slot(id, e)),
                [ LanesStep.Slot(id, m, next, effects) ]
            | None -> lanes, [], []
        | LanesMsg.StartSlot(id, category, request) ->
            let pat = lanes.OrderContext |> OrderContextState.patient

            step
                (LanesMsg.Slot(id, OrderContextMsg.PatientDataChanged(pat, request)))
                { lanes with Slots = lanes.Slots @ [ id, OrderContextState.slot category ] }
        | LanesMsg.RemoveSlot id -> lanes |> withoutSlot id, [], [ LanesStep.SlotRemoved id ]
        // the answer first, then the notice: the Session decides whether the notice still counts
        | LanesMsg.Answer(answer, from, notice) ->
            let lanes, effects, taken = step answer lanes

            match notice with
            | Some notice ->
                let lanes, told, toldTaken = step (LanesMsg.Session(SessionMsg.NoticeReceived(from, notice))) lanes
                lanes, effects @ told, taken @ toldTaken
            | None -> lanes, effects, taken
        // Nothing is added when the workbench or the slot no longer shows the order. For the workbench the
        // plan page opens at once, but only when the plan request was sent. A slot stays on the nutrition
        // page, so that the next slot can be composed. The context is emptied when the answer arrives, so a
        // failed add keeps the order. Nothing changes the context meanwhile: a plan request disables every
        // page except the nutrition page and locks the menu, and on the nutrition page the prescribed slot
        // waits for the answer.
        | LanesMsg.Prescribe(slotId, orderId, request) ->
            let lane =
                match slotId with
                | None -> Some lanes.OrderContext
                | Some id -> lanes |> slotOf id

            // A linked order that is turned on but not narrowed to one scenario stops the prescribe.
            let narrowed =
                lane
                |> Option.bind (fun lane ->
                    match
                        lane |> OrderContextState.narrowedTo orderId, lane |> OrderContextState.linkedToPrescribe
                    with
                    | Some ctx, Some linked -> Some(ctx, linked)
                    | _ -> None
                )

            match narrowed with
            | Some(ctx, linked) ->
                // the context goes with its slot id, so that the answer empties that slot
                let ctx = { ctx with Id = slotId |> Option.defaultValue ctx.Id }
                let add = OrderPlanChange.Add(ctx, linked)
                let lanes, effects, taken = step (LanesMsg.Plan(OrderPlanMsg.Change(add, request))) lanes

                if slotId.IsNone && lanes.OrderPlan |> OrderPlanState.awaits request then
                    lanes, effects @ [ LanesEffect.GoToPlanPage ], taken
                else
                    lanes, effects, taken
            | None -> lanes, [], []
        // an answer to a request of a lane set back finds no request and is dropped
        | LanesMsg.StartOver draft ->
            let reset = { initial draft with Session = lanes.Session }
            let lanes, effects, taken = step (LanesMsg.Session SessionMsg.UrlMovedOn) reset
            lanes, effects, LanesStep.StartedOver reset :: taken


    /// Returns the messages that an effect becomes for the other machines, including the slots, or an empty
    /// list for an effect that only leaves the client. Makes the request ids with newId.
    let route (newId: unit -> string) (lanes: LanesState) effect =
        match effect with
        // The renewed token with the signed patient data, the ended session and a refusal for a newer
        // version go to the session; a signed plan goes to the plan.
        | LanesEffect.Signing(SigningEffect.RenewSessionToken(token, patient, signed)) ->
            [ LanesMsg.Session(SessionMsg.SignatureRenewedToken(token, patient, signed)) ]
        | LanesEffect.Signing(SigningEffect.EndSession ending) ->
            [ LanesMsg.Session(SessionMsg.SignatureEndedSession ending) ]
        | LanesEffect.Signing(SigningEffect.TellSigned _) -> [ LanesMsg.Plan OrderPlanMsg.Signed ]
        | LanesEffect.Signing SigningEffect.ReadEhrAgain -> [ LanesMsg.Session SessionMsg.RefreshPatient ]
        | LanesEffect.Patient(PatientEffect.GivePlan pat) ->
            [ LanesMsg.Plan(OrderPlanMsg.PatientDataChanged(Some pat, newId ())) ]
        | LanesEffect.Plan OrderPlanEffect.PatientDataNotFollowed -> [ LanesMsg.Patient PatientMsg.PlanNotFollowed ]
        // An order added to the plan empties the context it came from: the workbench or its slot.
        | LanesEffect.Plan(OrderPlanEffect.OrderAdded None) -> [ LanesMsg.Workbench(OrderContextMsg.Reset(newId ())) ]
        | LanesEffect.Plan(OrderPlanEffect.OrderAdded(Some slotId)) ->
            [ LanesMsg.Slot(slotId, OrderContextMsg.Reset(newId ())) ]
        | LanesEffect.Signing(SigningEffect.TellRefused(SigningRefusal.Blocked head)) ->
            [ LanesMsg.Session(SessionMsg.SignatureBlocked head) ]
        // The session patient data goes to the patient machine with renewed estimates, as for any patient
        // data from outside; the signed plan goes to the plan.
        | LanesEffect.Session(SessionEffect.SetPatientData pat) ->
            [
                LanesMsg.Patient(PatientMsg.Changed(pat, PatientDraftPolicy.Estimates.Renewed, newId ()))
            ]
        | LanesEffect.Session(SessionEffect.SetPatientDataRead pat) ->
            [ LanesMsg.Patient(PatientMsg.Refreshed(pat, newId ())) ]
        | LanesEffect.Session(SessionEffect.TakeAge pat) -> [ LanesMsg.Patient(PatientMsg.AgeTaken(pat, newId ())) ]
        | LanesEffect.Session(SessionEffect.LoadSignedPlan head) ->
            [ LanesMsg.Plan(OrderPlanMsg.OpenSignedPlan(head, newId ())) ]
        // Patient data from the server goes to the workbench, to every slot and to the plan; a slot starts
        // again from its rule set.
        | LanesEffect.Patient(PatientEffect.SetPatientData pat) ->
            [
                LanesMsg.Workbench(OrderContextMsg.PatientDataChanged(pat, newId ()))
                for id, _ in lanes.Slots do
                    LanesMsg.Slot(id, OrderContextMsg.PatientDataChanged(pat, newId ()))
                LanesMsg.Plan(OrderPlanMsg.PatientDataChanged(pat, newId ()))
            ]
        | _ -> []


    /// Runs a message and every message its effects become, and returns the new lanes, all effects and all
    /// steps. The messages from an effect run before the waiting messages, so that what an effect causes
    /// comes before anything after it. The routes go one way: signing to the session and the plan, the
    /// session to the patient and the plan, the patient to the workbench, the slots and the plan, and the
    /// plan to the patient and the workbench. The plan sends the patient machine only that the plan did not
    /// follow the patient data, which sends nothing back, and the workbench and the slots send nothing on,
    /// so the queue always empties.
    let transition newId msg (lanes: LanesState) =
        let rec run queue (lanes, effects, steps) =
            match queue with
            | [] -> lanes, effects, steps
            | msg :: rest ->
                let lanes, emitted, taken = step msg lanes
                let follow = emitted |> List.collect (route newId lanes)
                run (follow @ rest) (lanes, effects @ emitted, steps @ taken)

        run [ msg ] (lanes, [], [])


    /// Returns the pending requests that make the pages busy: a field that counts step clicks, the requests
    /// of the machines and the nutrition slots, a plan calculated on older patient data, and the data loads.
    /// A plan on older patient data is not a request but counts as one.
    let out counting (lanes: LanesState) loads =
        [
            if counting then
                Loads.Request.Counting
            if PatientState.changing lanes.Patient then
                Loads.Request.Patient
            if (OrderContextState.inFlightRequest lanes.OrderContext).IsSome then
                Loads.Request.Workbench
            if
                lanes.Slots
                |> List.exists (fun (_, slot) -> (OrderContextState.inFlightRequest slot).IsSome)
            then
                Loads.Request.Slots
            if (OrderPlanState.inFlightRequest lanes.OrderPlan).IsSome then
                Loads.Request.Plan
            if OutPolicy.planBehind lanes.Patient lanes.OrderPlan then
                Loads.Request.PlanBehind
            if
                SessionState.changing lanes.Session
                || (SessionState.reopening lanes.Session).IsSome
            then
                Loads.Request.Session
            if lanes.Signing |> SigningState.view |> SigningPolicy.underWay then
                Loads.Request.Signature
            yield! loads |> List.map Loads.Request.Load
        ]
