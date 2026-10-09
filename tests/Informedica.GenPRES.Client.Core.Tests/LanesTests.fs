module Informedica.GenPRES.Client.Core.Tests.LanesTests

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Api
open PatientMachine
open SessionMachine
open SigningMachine
open OrderPlanMachine
open OrderContextMachine
open Lanes
open Informedica.GenPRES.Client.Core.Tests.OrderPlanMachineTests.Fixtures


/// Request ids counted from one, as the App's random ones would be distinct.
let counter () =
    let mutable n = 0

    fun () ->
        n <- n + 1
        $"id-%i{n}"


/// A patient with a weight and a height, so the patient machine passes its answer on.
let measured =
    { draft with
        Weight = { draft.Weight with Measured = Some 30000<gram> }
        Height = { draft.Height with Measured = Some 130<cm> }
    }


/// The requests out over the lanes alone, no loads.
let out (lanes: LanesState) =
    Busy.out lanes.Patient lanes.OrderContext lanes.OrderPlan lanes.Session lanes.Signing []


/// Messages played one after another; the lanes, effects and steps after each.
let play newId (lanes: LanesState) msgs =
    msgs
    |> List.scan (fun (lanes, _, _) msg -> Lanes.transition newId msg lanes) (lanes, [], [])
    |> List.tail


let planRequest effects =
    effects
    |> List.tryPick (
        function
        | LanesEffect.Plan(OrderPlanEffect.CallPlan(_, request)) -> Some request
        | _ -> None
    )


let workbenchRequest effects =
    effects
    |> List.tryPick (
        function
        | LanesEffect.Workbench(OrderContextEffect.CallPatientChanged(_, _, request))
        | LanesEffect.Workbench(OrderContextEffect.CallContext(_, _, request)) -> Some request
        | _ -> None
    )


[<Tests>]
let tests =
    testList
        "Lanes"
        [
            test "a page load with a url patient: something is out until the last answer, in either order" {
                let newId = counter ()

                let lanes, _, _ =
                    Lanes.initial (Some measured)
                    |> Lanes.transition
                        newId
                        (LanesMsg.Patient(
                            PatientMsg.Changed(Some measured, PatientDraftPolicy.Estimates.Renewed, "p-1")
                        ))

                lanes |> out |> Expect.equal "the patient out" [ Busy.Request.Patient ]

                // the patient answered: the workbench and the plan are asked in the same transition
                let lanes, effects, steps =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered("p-1", Ok measured)))

                steps
                |> List.map (
                    function
                    | LanesStep.Patient _ -> "Patient"
                    | LanesStep.Workbench _ -> "OrderContext"
                    | LanesStep.Plan _ -> "OrderPlan"
                    | LanesStep.Session _ -> "Session"
                    | LanesStep.SigningReset _ -> "Signing"
                    | LanesStep.StartedOver _ -> "Lanes"
                )
                |> Expect.equal
                    "the trail's order: the patient, the workbench, the plan"
                    [ "Patient"; "OrderContext"; "OrderPlan" ]

                lanes
                |> out
                |> Expect.equal "the workbench and the plan out" [ Busy.Request.Workbench; Busy.Request.Plan ]

                effects
                |> List.contains (LanesEffect.Patient(PatientEffect.SetPatient(Some measured)))
                |> Expect.isTrue "the client part of the patient comes out, for the page loads"

                let plan = planRequest effects |> Option.defaultWith (fun () -> failtest "no plan request")

                let workbench =
                    workbenchRequest effects
                    |> Option.defaultWith (fun () -> failtest "no workbench request")

                let planAnswer = LanesMsg.Plan(OrderPlanMsg.Answered(plan, Ok one))

                let workbenchAnswer =
                    LanesMsg.Workbench(
                        OrderContextMsg.Answered(
                            workbench,
                            Ok(OrderContextResponse.Evaluated(OrderContextState.emptyFor measured))
                        )
                    )

                for order in [ [ planAnswer; workbenchAnswer ]; [ workbenchAnswer; planAnswer ] ] do
                    let after = play newId lanes order |> List.map (fun (lanes, _, _) -> out lanes)

                    after.Head |> Expect.isNonEmpty "one answer in: the other is still out"
                    after |> List.last |> Expect.isEmpty "both in: nothing out"
            }

            test
                "the url moves on during a workbench and a plan request: the lanes as at a page load, the answers dropped" {
                let newId = counter ()

                let lanes, effects, _ =
                    Lanes.initial (Some measured)
                    |> fun lanes ->
                        play
                            newId
                            lanes
                            [
                                LanesMsg.Patient(
                                    PatientMsg.Changed(Some measured, PatientDraftPolicy.Estimates.Renewed, "p-1")
                                )
                                LanesMsg.Patient(PatientMsg.Answered("p-1", Ok measured))
                            ]
                    |> List.last

                let plan = planRequest effects |> Option.defaultWith (fun () -> failtest "no plan request")

                let workbench =
                    workbenchRequest effects
                    |> Option.defaultWith (fun () -> failtest "no workbench request")

                let started, effects, steps = lanes |> Lanes.transition newId (LanesMsg.StartOver(Some draft))

                let atLoad = { Lanes.initial (Some draft) with Session = SessionState.leaving SessionFollow.Anonymous }

                started |> Expect.equal "as at a page load, the Session leaving" atLoad

                effects
                |> Expect.equal "the close alone" [ LanesEffect.Session SessionEffect.CallCloseSession ]

                steps
                |> List.head
                |> function
                    | LanesStep.StartedOver _ -> ()
                    | _ -> failtest "the start-over is the first step"

                let answers =
                    [
                        LanesMsg.Plan(OrderPlanMsg.Answered(plan, Ok one))
                        LanesMsg.Workbench(
                            OrderContextMsg.Answered(
                                workbench,
                                Ok(OrderContextResponse.Evaluated(OrderContextState.emptyFor measured))
                            )
                        )
                    ]

                for answer in answers do
                    let after, effects, _ = started |> Lanes.transition newId answer
                    after |> Expect.equal "the answer dropped" started
                    effects |> Expect.isEmpty "nothing sent"
            }

            test "the url moves on with an open Session: the Session closes and the signing lane is idle" {
                let newId = counter ()

                let withSession =
                    { Lanes.initial None with Session = SessionState.opened SessionMachineTests.full None }

                let started, effects, _ = withSession |> Lanes.transition newId (LanesMsg.StartOver None)

                started.Signing |> Expect.equal "idle" SigningState.idle

                started.Session
                |> SessionState.view
                |> Expect.equal "anonymous" SessionView.Anonymous

                effects
                |> Expect.equal "the close alone" [ LanesEffect.Session SessionEffect.CallCloseSession ]
            }

            test "a Session resumed with saved orders: the patient and the version reach their machines at once" {
                let session = { SessionMachineTests.full with Head = Some head }

                let lanes = { Lanes.initial None with Session = SessionState.resuming }

                let _, effects, steps =
                    lanes
                    |> Lanes.transition
                        (counter ())
                        (LanesMsg.Session(SessionMsg.Resumed(Ok(ResumeResult.Found session))))

                steps
                |> List.exists (
                    function
                    | LanesStep.Patient(PatientMsg.Changed(p, PatientDraftPolicy.Estimates.Renewed, _), _, _) ->
                        p = (session.PatientContext |> Option.bind _.Patient)
                    | _ -> false
                )
                |> Expect.isTrue "the patient machine got the Session's patient"

                steps
                |> List.exists (
                    function
                    | LanesStep.Plan(OrderPlanMsg.OpenSignedPlan(v, _), _, _) -> v = head
                    | _ -> false
                )
                |> Expect.isTrue "the plan got the saved orders"

                effects
                |> List.contains (LanesEffect.Session(SessionEffect.LoadSignedPlan head))
                |> Expect.isTrue "the Session's effects still come out"
            }

            test "a Session resumed with saved orders and no patient: the plan opens them once the patient is in" {
                let session =
                    { SessionMachineTests.full with
                        PatientContext =
                            SessionMachineTests.full.PatientContext
                            |> Option.map (fun context -> { context with Patient = None })
                        Head = Some head
                    }

                let newId = counter ()

                let lanes, _, _ =
                    { Lanes.initial None with Session = SessionState.resuming }
                    |> Lanes.transition newId (LanesMsg.Session(SessionMsg.Resumed(Ok(ResumeResult.Found session))))

                // the user enters the patient, and it is answered
                let lanes, _, _ =
                    lanes
                    |> Lanes.transition
                        newId
                        (LanesMsg.Patient(
                            PatientMsg.Changed(Some measured, PatientDraftPolicy.Estimates.Renewed, "p-1")
                        ))

                let _, effects, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered("p-1", Ok measured)))

                effects
                |> List.tryPick (
                    function
                    | LanesEffect.Plan(OrderPlanEffect.CallPlan(OrderPlanCommand.Open(_, contexts), _)) -> Some contexts
                    | _ -> None
                )
                |> Expect.equal "the plan opens the saved orders" (Some head.OrderContexts)
            }

            test "a prescription answered: the plan alone, the workbench and the page already done at the click" {
                let workbench = context "" "ibuprofen"

                let lanes =
                    { Lanes.initial (Some draft) with
                        OrderPlan = recalculating one None "r-1" (OrderPlanCommand.AddOrderContext(one, workbench))
                        OrderContext = OrderContextState.held patient (OrderContextState.emptyFor patient)
                    }

                let _, effects, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Plan(OrderPlanMsg.Answered("r-1", Ok two)))

                steps |> List.length |> Expect.equal "the plan's step alone" 1

                effects
                |> List.contains LanesEffect.GoToPlanPage
                |> Expect.isFalse "no page switch"
            }

            test "the Session closed: the signing lane is set idle, a step without a message" {
                let lanes =
                    { Lanes.initial (Some draft) with
                        Session = SessionState.opened SessionMachineTests.full None
                        Signing = SigningState.requesting one None "s-1"
                    }

                let lanes, _, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Session SessionMsg.CloseSession)

                lanes.Signing |> SigningState.view |> Expect.equal "idle" SigningView.Idle

                steps
                |> List.last
                |> Expect.equal "the reset step" (LanesStep.SigningReset SigningState.idle)
            }

            test "a signature answered: the token, the patient and the signed plan land in the same transition" {
                let lanes =
                    { Lanes.initial (Some draft) with
                        Session = SessionState.opened SessionMachineTests.full None
                        Signing = SigningMachineTests.Fixtures.submitting
                    }

                let lanes, effects, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Signing(SigningMachineTests.Fixtures.submitted "k-1"))

                steps
                |> List.choose (
                    function
                    | LanesStep.Session(SessionMsg.SignatureRenewedToken _, _, _) -> Some "token"
                    | LanesStep.Patient(PatientMsg.Changed _, _, _) -> Some "patient"
                    | LanesStep.Plan(OrderPlanMsg.Signed, _, _) -> Some "signed"
                    | _ -> None
                )
                |> List.distinct
                |> Expect.equal "the Session, the patient and the plan" [ "token"; "patient"; "signed" ]

                effects
                |> List.contains (LanesEffect.Signing(SigningEffect.TellSigned SigningMachineTests.Fixtures.signed))
                |> Expect.isTrue "the message comes out"

                match SessionState.view lanes.Session with
                | SessionView.Open opened ->
                    opened.Head
                    |> Expect.equal "the head is the version signed" (Some SigningMachineTests.Fixtures.signed)
                | view -> failtest $"not open: %A{view}"
            }

            test "a refresh answered: the patient goes out, the plan keeps its orders and follows the answer" {
                let session = SessionMachineTests.full

                let refreshed =
                    { session with
                        PatientContext =
                            session.PatientContext
                            |> Option.map (fun c -> { c with Patient = Some measured })
                        Head = Some head
                    }

                let newId = counter ()

                let lanes, _, steps =
                    { Lanes.initial (Some draft) with
                        Session = SessionState.opened session None
                        OrderPlan = shown
                    }
                    |> Lanes.transition
                        newId
                        (LanesMsg.Session(SessionMsg.PatientRefreshed(session.OpenedToken, Ok(Some refreshed))))

                steps
                |> List.exists (
                    function
                    | LanesStep.Plan _ -> true
                    | _ -> false
                )
                |> Expect.isFalse "nothing to the plan"

                let request =
                    steps
                    |> List.tryPick (
                        function
                        | LanesStep.Patient(PatientMsg.Changed(Some p, _, request), _, _) when p = measured ->
                            Some request
                        | _ -> None
                    )
                    |> Option.defaultWith (fun () -> failtest "the patient read again did not go out")

                let _, effects, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered(request, Ok measured)))

                effects
                |> List.exists (
                    function
                    | LanesEffect.Plan(OrderPlanEffect.CallPlan(OrderPlanCommand.UpdatePatient _, _)) -> true
                    | _ -> false
                )
                |> Expect.isTrue "the plan updated to the patient"
            }

            test "a signature refused for a newer version: the Session keeps the version, the refusal is told" {
                let refused =
                    SigningMsg.SubmitAnswered("k-1", Ok(SigningResponse.Refused(SigningRefusal.Blocked head.Head)))

                let lanes =
                    { Lanes.initial (Some draft) with
                        Session = SessionState.opened SessionMachineTests.full None
                        Signing = SigningMachineTests.Fixtures.submitting
                    }

                let _, effects, steps = lanes |> Lanes.transition (counter ()) (LanesMsg.Signing refused)

                steps
                |> List.exists (
                    function
                    | LanesStep.Session(SessionMsg.SignatureBlocked h, _, _) -> h = head.Head
                    | _ -> false
                )
                |> Expect.isTrue "the Session got the version"

                effects
                |> List.contains (LanesEffect.Signing(SigningEffect.TellRefused(SigningRefusal.Blocked head.Head)))
                |> Expect.isTrue "the refusal comes out"
            }

            test "an answer with a newer-version notice: the answer and the notice in one transition" {
                let session = SessionMachineTests.full

                let lanes = { Lanes.initial (Some draft) with Session = SessionState.opened session None }

                let answer =
                    LanesMsg.Answer(
                        LanesMsg.Patient(PatientMsg.Answered("p-1", Ok measured)),
                        session.OpenedToken,
                        Some(RecordNotice.NewerVersion head.Head)
                    )

                let _, effects, steps = lanes |> Lanes.transition (counter ()) answer

                steps
                |> List.map (
                    function
                    | LanesStep.Patient _ -> "Patient"
                    | LanesStep.Session(SessionMsg.NoticeReceived _, _, _) -> "NoticeReceived"
                    | _ -> "other"
                )
                |> Expect.equal "the answer, then the notice" [ "Patient"; "NoticeReceived" ]

                effects
                |> List.contains (LanesEffect.Session(SessionEffect.TellNewerSignedPlan head.Head))
                |> Expect.isTrue "the newer version is told"
            }

            test "the prescribe click: the order into the plan, the workbench emptied and the plan page opened" {
                let workbench =
                    { context "" "paracetamol" with
                        Scenarios = [| scenario "o-1" "paracetamol"; scenario "o-2" "paracetamol" |]
                    }

                let lanes =
                    { Lanes.initial (Some draft) with
                        OrderPlan = shown
                        OrderContext = OrderContextState.held patient workbench
                    }

                let _, effects, steps = lanes |> Lanes.transition (counter ()) (LanesMsg.Prescribe("o-2", "r-1", "r-2"))

                steps
                |> List.choose (
                    function
                    | LanesStep.Plan(OrderPlanMsg.Change(OrderPlanChange.Add ctx, "r-1"), _, _) ->
                        Some(ctx.Scenarios |> Array.map _.Order.Id |> String.concat ",")
                    | LanesStep.Workbench(OrderContextMsg.Reset "r-2", _, _) -> Some "reset"
                    | _ -> None
                )
                |> Expect.equal "the order o-2 alone, then the reset" [ "o-2"; "reset" ]

                effects
                |> List.last
                |> Expect.equal "the plan page opened" LanesEffect.GoToPlanPage

                lanes
                |> Lanes.transition (counter ()) (LanesMsg.Prescribe("o-9", "r-1", "r-2"))
                |> fun (_, effects, steps) -> effects, steps
                |> Expect.equal "nothing for an order the workbench does not show" ([], [])
            }

            test "the prescribe click while the plan is busy: the workbench keeps the order, the page stays" {
                let workbench = { context "" "paracetamol" with Scenarios = [| scenario "o-1" "paracetamol" |] }

                let lanes =
                    { Lanes.initial (Some draft) with
                        OrderPlan = recalculating one None "r-0" (OrderPlanCommand.UpdatePatient(patient, one))
                        OrderContext = OrderContextState.held patient workbench
                    }

                let after, effects, steps =
                    lanes |> Lanes.transition (counter ()) (LanesMsg.Prescribe("o-1", "r-1", "r-2"))

                steps
                |> List.exists (
                    function
                    | LanesStep.Workbench _ -> true
                    | _ -> false
                )
                |> Expect.isFalse "no reset"

                effects
                |> List.contains LanesEffect.GoToPlanPage
                |> Expect.isFalse "no page switch"

                after.OrderContext
                |> OrderContextState.narrowedTo "o-1"
                |> Expect.isSome "the order is still on the workbench"
            }

            test "a message for one machine only runs that machine" {
                let lanes = Lanes.initial (Some draft)

                let _, _, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Workbench(OrderContextMsg.SelectScenario None))

                steps |> List.length |> Expect.equal "one step" 1
            }
        ]
