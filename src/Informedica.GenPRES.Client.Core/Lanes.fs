/// The wiring between the machines: what one machine's effect means for another. A message runs
/// the machine it is for, and an effect meant for another machine is passed on to that machine
/// in the same transition, never left for a later update. Every effect still comes out: the
/// transition has done what it means for the other machines, and the App does the rest of it,
/// the part that leaves the client.
module Lanes

open Shared.Types
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
    | Plan of OrderPlanMsg
    /// A server answer with the notice its reply carried, if any: the answer reaches its
    /// machine and the notice the Session.
    | Answer of LanesMsg * from: OpenedToken option * RecordNotice option
    /// The prescribe click: the workbench narrowed to the order with this id goes into the plan,
    /// and the workbench is emptied under the reset request id.
    | Prescribe of orderId: string * request: string * reset: string
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
    | Plan of OrderPlanEffect
    | Session of SessionEffect
    /// Open the plan page: an order was prescribed.
    | GoToPlanPage


/// A step the transition took, for the trail: a machine's message with its new state and
/// effects, or the signing lane set idle, which no message asked for.
[<RequireQualifiedAccess>]
type LanesStep =
    | Signing of SigningMsg * SigningState * SigningEffect list
    | Patient of PatientMsg * PatientState * PatientEffect list
    | Workbench of OrderContextMsg * OrderContextState * OrderContextEffect list
    | Plan of OrderPlanMsg * OrderPlanState * OrderPlanEffect list
    | Session of SessionMsg * SessionState * SessionEffect list
    | SigningReset of SigningState
    /// The patient, the workbench, the plan and the signing lane set back to their initial
    /// state, which no message of theirs asked for.
    | StartedOver of LanesState


/// The lanes at the page load: the patient from the url, if any, and every other lane empty.
let initial draft =
    {
        Patient = PatientState.init draft
        OrderContext = OrderContextState.noPatient
        OrderPlan = OrderPlanState.noPatient
        Session = SessionState.anonymous
        Signing = SigningState.idle
    }


/// The steps one message takes: its machine's step, and after a step of the Session the signing
/// lane set idle when the Session is no longer open.
let rec step msg (lanes: LanesState) =
    match msg with
    | LanesMsg.Signing m ->
        let next, effects = SigningState.transition m lanes.Signing
        { lanes with Signing = next }, effects |> List.map LanesEffect.Signing, [ LanesStep.Signing(m, next, effects) ]
    | LanesMsg.Session m ->
        let next, effects = SessionState.transition m lanes.Session
        let lanes = { lanes with Session = next }
        let taken = LanesStep.Session(m, next, effects)

        // a signature belongs to an open Session: whatever ends the Session drops it. The plan's
        // work stays: unsigned is unsigned
        match SessionState.view next, SigningState.view lanes.Signing with
        | SessionView.Open _, _
        | _, SigningView.Idle -> lanes, effects |> List.map LanesEffect.Session, [ taken ]
        | _ ->
            { lanes with Signing = SigningState.idle },
            effects |> List.map LanesEffect.Session,
            [ taken; LanesStep.SigningReset SigningState.idle ]
    | LanesMsg.Patient m ->
        let next, effects = PatientState.transition m lanes.Patient
        { lanes with Patient = next }, effects |> List.map LanesEffect.Patient, [ LanesStep.Patient(m, next, effects) ]
    | LanesMsg.Plan m ->
        let next, effects =
            OrderPlanState.transitionWhile (SigningState.view lanes.Signing) lanes.Patient m lanes.OrderPlan

        { lanes with OrderPlan = next }, effects |> List.map LanesEffect.Plan, [ LanesStep.Plan(m, next, effects) ]
    | LanesMsg.Workbench m ->
        let next, effects = OrderContextState.transitionWhile lanes.Patient m lanes.OrderContext

        { lanes with OrderContext = next },
        effects |> List.map LanesEffect.Workbench,
        [ LanesStep.Workbench(m, next, effects) ]
    // the answer first, then the notice: the Session decides whether the notice still counts
    | LanesMsg.Answer(answer, from, notice) ->
        let lanes, effects, taken = step answer lanes

        match notice with
        | Some notice ->
            let lanes, told, toldTaken = step (LanesMsg.Session(SessionMsg.NoticeReceived(from, notice))) lanes
            lanes, effects @ told, taken @ toldTaken
        | None -> lanes, effects, taken
    // nothing to add when the workbench no longer shows the order. The workbench is emptied and
    // the plan page opened at the click, but only when the order went out: a plan that takes no
    // change now drops it, and the workbench keeps it then
    | LanesMsg.Prescribe(orderId, request, reset) ->
        match lanes.OrderContext |> OrderContextState.narrowedTo orderId with
        | Some ctx ->
            let lanes, effects, taken =
                step (LanesMsg.Plan(OrderPlanMsg.Change(OrderPlanChange.Add ctx, request))) lanes

            let sent =
                effects
                |> List.exists (
                    function
                    | LanesEffect.Plan(OrderPlanEffect.CallPlan _) -> true
                    | _ -> false
                )

            if sent then
                let lanes, emptied, resetTaken = step (LanesMsg.Workbench(OrderContextMsg.Reset reset)) lanes
                lanes, effects @ emptied @ [ LanesEffect.GoToPlanPage ], taken @ resetTaken
            else
                lanes, effects, taken
        | None -> lanes, [], []
    // an answer to a request of a lane set back finds no request and is dropped
    | LanesMsg.StartOver draft ->
        let reset = { initial draft with Session = lanes.Session }
        let lanes, effects, taken = step (LanesMsg.Session SessionMsg.UrlMovedOn) reset
        lanes, effects, LanesStep.StartedOver reset :: taken


/// The messages an effect becomes for other machines; none for an effect that only leaves the
/// client. The request ids come from newId.
let route (newId: unit -> string) effect =
    match effect with
    // the signature's renewed token, ended Session and refusal for a newer version go to the
    // Session, the patient as signed to the patient machine, and the plan signed to the plan
    | LanesEffect.Signing(SigningEffect.RenewSessionToken(token, patient, signed)) ->
        [ LanesMsg.Session(SessionMsg.SignatureRenewedToken(token, patient, signed)) ]
    | LanesEffect.Signing(SigningEffect.EndSession ending) ->
        [ LanesMsg.Session(SessionMsg.SignatureEndedSession ending) ]
    | LanesEffect.Signing(SigningEffect.SetNoticedPatient patient) ->
        [
            LanesMsg.Patient(PatientMsg.Changed(Some patient, PatientDraftPolicy.Estimates.Renewed, newId ()))
        ]
    | LanesEffect.Signing(SigningEffect.TellSigned _) -> [ LanesMsg.Plan OrderPlanMsg.Signed ]
    | LanesEffect.Signing(SigningEffect.TellRefused(SigningRefusal.Blocked head)) ->
        [ LanesMsg.Session(SessionMsg.SignatureBlocked head) ]
    // the Session's patient goes to the patient machine, with the estimates renewed as for any
    // patient given from outside, and its saved orders to the plan
    | LanesEffect.Session(SessionEffect.SetPatient pat) ->
        [
            LanesMsg.Patient(PatientMsg.Changed(pat, PatientDraftPolicy.Estimates.Renewed, newId ()))
        ]
    | LanesEffect.Session(SessionEffect.LoadSignedPlan head) -> [ LanesMsg.Plan(OrderPlanMsg.Version(head, newId ())) ]
    // the patient answered goes to the workbench and the plan
    | LanesEffect.Patient(PatientEffect.SetPatient pat) ->
        [
            LanesMsg.Workbench(OrderContextMsg.PatientChanged(pat, newId ()))
            LanesMsg.Plan(OrderPlanMsg.PatientChanged(pat, newId ()))
        ]
    | _ -> []


/// The next lanes, the effects that come out and the steps taken, for a message: the messages its
/// effects become are run first, before the messages still waiting, so what an effect causes lands
/// before anything that came after it; until none is left. The routes run one way, the signing
/// lane to the Session, the patient and the plan, the Session to the patient and the plan, the
/// patient to the workbench and the plan, so the list empties: no machine passes an effect back
/// to one before it.
let transition newId msg (lanes: LanesState) =
    let rec run queue (lanes, effects, steps) =
        match queue with
        | [] -> lanes, effects, steps
        | msg :: rest ->
            let lanes, emitted, taken = step msg lanes
            let follow = emitted |> List.collect (route newId)
            run (follow @ rest) (lanes, effects @ emitted, steps @ taken)

    run [ msg ] (lanes, [], [])
