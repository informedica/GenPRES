namespace Informedica.GenPRES.Client.Core

/// The wiring between the machines: what one machine's effect means for another. A message runs
/// the machine it is for, and an effect meant for another machine is passed on to that machine
/// in the same transition, never left for a later update. Every effect still comes out: the
/// transition has done what it means for the other machines, and the App does the rest of it,
/// the part that leaves the client.
module Lanes =

    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Shared.Types
    open PatientMachine
    open SessionMachine
    open SigningMachine
    open OrderPlanMachine
    open OrderContextMachine


    /// The machines' states.
    type LanesState =
        {
            /// The patient, as the patient machine holds it: the draft and the change under way.
            Patient: PatientState
            /// The prescribing workbench, as the order context machine holds it.
            OrderContext: OrderContextState
            /// The nutrition slots by their slot id, each as the order context machine holds it.
            Slots: Map<string, OrderContextState>
            /// The one plan, as the order plan machine holds it.
            OrderPlan: OrderPlanState
            /// The launch Session; Anonymous is the state every url patient runs in.
            Session: SessionState
            /// The signing phase of the open Session; Idle whenever no Session is open.
            Signing: SigningState
        }


    /// What reaches the lanes: a message for one machine.
    [<RequireQualifiedAccess>]
    type LanesMsg =
        | Signing of SigningMsg
        | Session of SessionMsg
        | Patient of PatientMsg
        | Workbench of OrderContextMsg
        /// A message for the nutrition slot with the id; none reaches a slot that is gone. The patient
        /// cleared removes the slot, since another patient removes the slots.
        | Slot of slotId: string * OrderContextMsg
        /// A nutrition slot of the category started under the slot id the client minted, evaluated for
        /// the patient the workbench is evaluated for; when the workbench has no patient, no slot starts.
        | StartSlot of slotId: string * NutritionCategory * request: string
        | Plan of OrderPlanMsg
        /// A server answer with the notice its reply carried, if any: the answer reaches its
        /// machine and the notice the Session.
        | Answer of LanesMsg * from: OpenedToken option * RecordNotice option
        /// The prescribe click: the workbench narrowed to the order with this id goes into the plan;
        /// the workbench is emptied once the order has landed.
        | Prescribe of orderId: string * request: string
        /// The url brought a patient, a medication or a launch. The patient, the workbench, the plan
        /// and the signing are emptied, as when the page loads with this patient, and the Session is
        /// closed. A launch is not part of this message: the App presents it to the Session once its
        /// browser key is made, and the Session opens it after the close has answered.
        | StartOver of draft: Patient option


    /// The machines' effects, each as its machine emitted it.
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
        /// The pages ask nothing at the clear that comes now, since a patient follows it.
        | PatientExpected


    /// A step the transition took, for the trail: a machine's message with its new state and
    /// effects, or the lanes started over, which no message of theirs asked for.
    [<RequireQualifiedAccess>]
    type LanesStep =
        | Signing of SigningMsg * SigningState * SigningEffect list
        | Patient of PatientMsg * PatientState * PatientEffect list
        | Workbench of OrderContextMsg * OrderContextState * OrderContextEffect list
        | Slot of slotId: string * OrderContextMsg * OrderContextState * OrderContextEffect list
        | Plan of OrderPlanMsg * OrderPlanState * OrderPlanEffect list
        | Session of SessionMsg * SessionState * SessionEffect list
        /// The patient, the workbench, the slots, the plan and the signing lane set back to their initial
        /// state, which no message of theirs asked for.
        | StartedOver of LanesState


    /// The lanes at the page load: the patient from the url, if any, every other lane empty and no slots.
    let initial draft =
        {
            Patient = PatientState.init draft
            OrderContext = OrderContextState.noPatient
            Slots = Map.empty
            OrderPlan = OrderPlanState.noPatient
            Session = SessionState.anonymous
            Signing = SigningState.idle
        }


    /// The steps one message takes: its machine's step, and after a step of the Session the signing
    /// lane told when the Session was open and no longer is.
    let rec step msg (lanes: LanesState) =
        match msg with
        | LanesMsg.Signing m ->
            let next, effects = SigningState.transition m lanes.Signing
            { lanes with Signing = next },
            effects |> List.map LanesEffect.Signing,
            [ LanesStep.Signing(m, next, effects) ]
        // a signature under way mints the token itself, so a reply to a request the server took
        // after it says nothing about another tab; the next request tells what still holds. Every
        // notice passes here, from a lane's answer and from the drug names, which can be out during
        // a signature
        | LanesMsg.Session(SessionMsg.NoticeReceived(_, RecordNotice.Ended SessionEnding.InactiveTab)) when
            (match lanes.Signing |> SigningState.view with
             | SigningView.Submitting _ -> true
             | _ -> false)
            ->
            lanes, [], []
        | LanesMsg.Session m ->
            let next, effects = SessionState.transition m lanes.Session
            let before = SessionState.view lanes.Session

            // a session that opens over a page holding a patient clears it first, so that no version
            // opens or follows on the data of the patient before
            let opensOverPatient =
                match before, SessionState.view next with
                | SessionView.Open _, _ -> false
                | _, SessionView.Open _ -> (lanes.Patient |> PatientState.draft).IsSome
                | _ -> false

            let lanes, cleared, clearing =
                if opensOverPatient then
                    // no patient sends nothing, so the request id is never read
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
                    opensOverPatient && (opened.PatientContext |> Option.bind _.Patient).IsSome
                    ->
                    [ LanesEffect.PatientExpected ]
                | _ -> []

            let effects = expected @ cleared @ (effects |> List.map LanesEffect.Session)

            // a signature belongs to an open Session: whatever ends the Session ends it, but for a
            // submission out, whose outcome is still told. Told once, at the step that ends it. The
            // plan's work stays: unsigned is unsigned
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
        // another patient arrives as the patient cleared, and removes the slot
        | LanesMsg.Slot(id, OrderContextMsg.PatientDataChanged(None, _)) ->
            { lanes with Slots = lanes.Slots |> Map.remove id }, [], []
        // an answer to a slot that another patient or a start-over removed finds no slot and is dropped
        | LanesMsg.Slot(id, m) ->
            match lanes.Slots |> Map.tryFind id with
            | Some slot ->
                let next, effects = OrderContextState.transition m slot

                { lanes with Slots = lanes.Slots |> Map.add id next },
                effects |> List.map (fun e -> LanesEffect.Slot(id, e)),
                [ LanesStep.Slot(id, m, next, effects) ]
            | None -> lanes, [], []
        | LanesMsg.StartSlot(id, category, request) ->
            let pat = lanes.OrderContext |> OrderContextState.patient

            step
                (LanesMsg.Slot(id, OrderContextMsg.PatientDataChanged(pat, request)))
                { lanes with Slots = lanes.Slots |> Map.add id (OrderContextState.slot category) }
        // the answer first, then the notice: the Session decides whether the notice still counts
        | LanesMsg.Answer(answer, from, notice) ->
            let lanes, effects, taken = step answer lanes

            match notice with
            | Some notice ->
                let lanes, told, toldTaken = step (LanesMsg.Session(SessionMsg.NoticeReceived(from, notice))) lanes
                lanes, effects @ told, taken @ toldTaken
            | None -> lanes, effects, taken
        // nothing to add when the workbench no longer shows the order. The plan page opens at the
        // click, but only when the order went out: a plan that takes no change now drops it. The
        // workbench is emptied at the answer, so a failed add leaves the order on it; nothing reaches
        // the workbench meanwhile, since the plan page is disabled and the menu locked
        | LanesMsg.Prescribe(orderId, request) ->
            match lanes.OrderContext |> OrderContextState.narrowedTo orderId with
            | Some ctx ->
                let lanes, effects, taken =
                    step (LanesMsg.Plan(OrderPlanMsg.Change(OrderPlanChange.Add ctx, request))) lanes

                if lanes.OrderPlan |> OrderPlanState.awaits request then
                    lanes, effects @ [ LanesEffect.GoToPlanPage ], taken
                else
                    lanes, effects, taken
            | None -> lanes, [], []
        // an answer to a request of a lane set back finds no request and is dropped
        | LanesMsg.StartOver draft ->
            let reset = { initial draft with Session = lanes.Session }
            let lanes, effects, taken = step (LanesMsg.Session SessionMsg.UrlMovedOn) reset
            lanes, effects, LanesStep.StartedOver reset :: taken


    /// The messages an effect becomes for other machines, the slots of the lanes included; none for an
    /// effect that only leaves the client. The request ids come from newId.
    let route (newId: unit -> string) (lanes: LanesState) effect =
        match effect with
        // the signature's renewed token, ended Session and refusal for a newer version go to the
        // Session, which passes the patient as signed on, and the plan signed to the plan
        | LanesEffect.Signing(SigningEffect.RenewSessionToken(token, patient, signed)) ->
            [ LanesMsg.Session(SessionMsg.SignatureRenewedToken(token, patient, signed)) ]
        | LanesEffect.Signing(SigningEffect.EndSession ending) ->
            [ LanesMsg.Session(SessionMsg.SignatureEndedSession ending) ]
        | LanesEffect.Signing(SigningEffect.TellSigned _) -> [ LanesMsg.Plan OrderPlanMsg.Signed ]
        | LanesEffect.Signing SigningEffect.ReadEhrAgain -> [ LanesMsg.Session SessionMsg.RefreshPatient ]
        | LanesEffect.Patient(PatientEffect.GivePlan pat) ->
            [ LanesMsg.Plan(OrderPlanMsg.PatientDataChanged(Some pat, newId ())) ]
        | LanesEffect.Plan OrderPlanEffect.PatientDataNotFollowed -> [ LanesMsg.Patient PatientMsg.PlanNotFollowed ]
        // an order prescribed that landed empties the workbench
        | LanesEffect.Plan OrderPlanEffect.OrderAdded -> [ LanesMsg.Workbench(OrderContextMsg.Reset(newId ())) ]
        | LanesEffect.Signing(SigningEffect.TellRefused(SigningRefusal.Blocked head)) ->
            [ LanesMsg.Session(SessionMsg.SignatureBlocked head) ]
        // the Session's patient data goes to the patient machine, with the estimates renewed as for
        // any patient data given from outside, and its saved orders to the plan
        | LanesEffect.Session(SessionEffect.SetPatientData pat) ->
            [
                LanesMsg.Patient(PatientMsg.Changed(pat, PatientDraftPolicy.Estimates.Renewed, newId ()))
            ]
        | LanesEffect.Session(SessionEffect.SetPatientDataRead pat) ->
            [ LanesMsg.Patient(PatientMsg.Refreshed(pat, newId ())) ]
        | LanesEffect.Session(SessionEffect.TakeAge pat) -> [ LanesMsg.Patient(PatientMsg.AgeTaken(pat, newId ())) ]
        | LanesEffect.Session(SessionEffect.LoadSignedPlan head) ->
            [ LanesMsg.Plan(OrderPlanMsg.OpenSignedPlan(head, newId ())) ]
        // the patient data answered goes to the workbench, to every slot and to the plan; a slot starts
        // again from its rule set
        | LanesEffect.Patient(PatientEffect.SetPatientData pat) ->
            [
                LanesMsg.Workbench(OrderContextMsg.PatientDataChanged(pat, newId ()))
                for id in lanes.Slots.Keys do
                    LanesMsg.Slot(id, OrderContextMsg.PatientDataChanged(pat, newId ()))
                LanesMsg.Plan(OrderPlanMsg.PatientDataChanged(pat, newId ()))
            ]
        | _ -> []


    /// The next lanes, the effects that come out and the steps taken, for a message: the messages its
    /// effects become are run first, before the messages still waiting, so what an effect causes lands
    /// before anything that came after it; until none is left. The routes run one way, the signing
    /// lane to the Session and the plan, the Session to the patient and the plan, the patient to the
    /// workbench, the slots and the plan, the plan to the patient and the workbench. The plan passes back
    /// to the patient only a plan that did not follow, which sends nothing to the plan, and the workbench
    /// and the slots pass nothing on, so the list empties.
    let transition newId msg (lanes: LanesState) =
        let rec run queue (lanes, effects, steps) =
            match queue with
            | [] -> lanes, effects, steps
            | msg :: rest ->
                let lanes, emitted, taken = step msg lanes
                let follow = emitted |> List.collect (route newId lanes)
                run (follow @ rest) (lanes, effects @ emitted, steps @ taken)

        run [ msg ] (lanes, [], [])
