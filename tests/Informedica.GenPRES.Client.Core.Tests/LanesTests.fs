module Informedica.GenPRES.Client.Core.Tests.LanesTests

open Informedica.GenPRES.Client.Core.Helpers
open Informedica.GenPRES.Client.Core.Policies
open Informedica.GenPRES.Client.Core.StateMachines
open Informedica.GenPRES.Client.Core.MachinePolicies
open Informedica.GenPRES.Client.Core.Tests.StateMachines
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api
open PatientMachine
open SessionMachine
open SigningMachine
open OrderPlanMachine
open OrderContextMachine
open Informedica.GenPRES.Client.Core.Lanes
open Informedica.GenPRES.Client.Core.Tests.OrderFixtures
open OrderPlanFixtures

module Lanes = Informedica.GenPRES.Client.Core.Lanes


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
    OutPolicy.out false lanes.Patient lanes.OrderContext lanes.Slots lanes.OrderPlan lanes.Session lanes.Signing []


/// Messages played one after another; the lanes, effects and steps after each.
let play newId (lanes: LanesState) msgs =
    msgs
    |> List.scan (fun (lanes, _, _) msg -> Lanes.transition newId msg lanes) (lanes, [], [])
    |> List.tail


/// Whether the orders stand on other patient data than the panel.
let behind (lanes: LanesState) = OutPolicy.planBehind lanes.Patient lanes.OrderPlan


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
        | LanesEffect.Workbench(OrderContextEffect.CallContext(_, request)) -> Some request
        | _ -> None
    )


/// Lanes with the plan of one context and a workbench of two paracetamol orders, o-1 and o-2,
/// before the prescribe click.
let twoOrdersOnWorkbench () =
    let workbench =
        { context "" "paracetamol" with Scenarios = [| scenario "o-1" "paracetamol"; scenario "o-2" "paracetamol" |] }

    { Lanes.initial (Some draft) with
        OrderPlan = shown
        OrderContext = OrderContextState.held patient workbench
    }


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

                lanes |> out |> Expect.equal "the patient out" [ Loads.Request.Patient ]

                // the patient answered: the workbench and the plan are asked in the same transition
                let lanes, effects, steps =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered("p-1", Ok measured)))

                steps
                |> List.map (
                    function
                    | LanesStep.Patient _ -> "Patient"
                    | LanesStep.Workbench _ -> "OrderContext"
                    | LanesStep.Slot _
                    | LanesStep.SlotRemoved _ -> "Slot"
                    | LanesStep.Plan _ -> "OrderPlan"
                    | LanesStep.Session _ -> "Session"
                    | LanesStep.Signing _ -> "Signing"
                    | LanesStep.StartedOver _ -> "Lanes"
                )
                |> Expect.equal
                    "the trail's order: the patient, the workbench, the plan"
                    [ "Patient"; "OrderContext"; "OrderPlan" ]

                lanes
                |> out
                |> Expect.equal "the workbench and the plan out" [ Loads.Request.Workbench; Loads.Request.Plan ]

                effects
                |> List.contains (LanesEffect.Patient(PatientEffect.SetPatientData(Some measured)))
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
                            Ok(OrderContextResponse.Evaluated(OrderContextState.emptyMedication measured))
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
                |> Expect.equal "the close alone" [ LanesEffect.Session(SessionEffect.CallCloseSession None) ]

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
                                Ok(OrderContextResponse.Evaluated(OrderContextState.emptyMedication measured))
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
                |> Expect.equal
                    "the close alone"
                    [ LanesEffect.Session(SessionEffect.CallCloseSession(Some(OpenedToken "t"))) ]
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

            test "a sign that renews an estimate: the plan follows and is a change to sign" {
                let newId = counter ()
                let estimated (kg: int) (p: Patient) = { p with Patient.Weight.Estimated = Some(kg * 1000<gram>) }
                let young = draft |> estimated 12
                let session = SessionMachineTests.full
                let aged = { young with Age = Some { ten with Age.Years = 11<year> } }

                let lanes =
                    { Lanes.initial (Some young) with
                        Session = SessionState.opened session None
                        OrderPlan = held two None
                    }

                let lanes, _, _ =
                    lanes
                    |> Lanes.transition
                        newId
                        (LanesMsg.Patient(PatientMsg.Changed(Some young, PatientDraftPolicy.Estimates.Renewed, "p-0")))

                let lanes, _, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered("p-0", Ok young)))

                let settled = { lanes with OrderPlan = held two None }

                let lanes, effects, _ =
                    settled
                    |> Lanes.transition
                        newId
                        (LanesMsg.Session(
                            SessionMsg.SignatureRenewedToken(OpenedToken "t2", aged, SessionMachineTests.signedVersion)
                        ))

                let cleared = aged |> Informedica.GenPRES.Shared.Models.Patient.withEstimates None None

                let request =
                    effects
                    |> List.pick (
                        function
                        | LanesEffect.Patient(PatientEffect.CallPatient(p, request)) when p = cleared -> Some request
                        | _ -> None
                    )

                let lanes, effects, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered(request, Ok(aged |> estimated 13))))

                let update =
                    effects
                    |> List.pick (
                        function
                        | LanesEffect.Plan(OrderPlanEffect.CallPlan(OrderPlanCommand.UpdatePatient _, request)) ->
                            Some request
                        | _ -> None
                    )

                let moved =
                    { two with
                        OrderContexts =
                            two.OrderContexts
                            |> Array.map (fun c -> { c with Argumentation = Some "moved" })
                    }

                let lanes, _, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Plan(OrderPlanMsg.Answered(update, Ok moved)))

                lanes.OrderPlan
                |> OrderPlanState.work
                |> Expect.equal "a new version to sign" PlanWorkPolicy.PlanWork.Changed
            }

            testList
                "a version opened with a new age: one request at a time"
                [
                    let session = SessionMachineTests.full
                    let older = { ten with Age.Years = 11<year> }

                    let reopened =
                        { session with
                            Head = Some head
                            PatientContext =
                                session.PatientContext
                                |> Option.map (fun c -> { c with Patient = Some { measured with Age = Some older } })
                        }

                    let sent (effects: LanesEffect list) =
                        effects
                        |> List.choose (
                            function
                            | LanesEffect.Plan(OrderPlanEffect.CallPlan(cmd, request)) -> Some(cmd, request)
                            | _ -> None
                        )

                    let opened newId =
                        { Lanes.initial (Some measured) with
                            Session = SessionState.opened session None
                            OrderPlan = held two None
                        }
                        |> Lanes.transition
                            newId
                            (LanesMsg.Session(SessionMsg.SignedPlanOpened(session.OpenedToken, Ok(Some reopened))))

                    let request effects =
                        effects
                        |> List.pick (
                            function
                            | LanesEffect.Patient(PatientEffect.CallPatient(_, request)) -> Some request
                            | _ -> None
                        )

                    test "the age goes first, and nothing opens until it is answered" {
                        let _, effects, _ = opened (counter ())

                        effects |> sent |> Expect.isEmpty "the plan waits"
                        effects |> request |> Expect.isNotEmpty "the age is asked"
                    }

                    test "the answer opens the version" {
                        let newId = counter ()
                        let lanes, effects, _ = opened newId
                        let aged = { measured with Age = Some older }

                        let _, effects, _ =
                            lanes
                            |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered(request effects, Ok aged)))

                        match effects |> sent with
                        | [ OrderPlanCommand.Open(_, contexts), _ ] ->
                            contexts |> Expect.equal "the version" head.OrderContexts
                        | other -> failtest $"not one open: %A{other}"
                    }

                    test "a failed age call still opens the version, and the error is told" {
                        let newId = counter ()
                        let lanes, effects, _ = opened newId

                        let _, effects, _ =
                            lanes
                            |> Lanes.transition
                                newId
                                (LanesMsg.Patient(PatientMsg.Answered(request effects, Error [| "no" |])))

                        effects
                        |> List.contains (LanesEffect.Patient(PatientEffect.TellError [| "no" |]))
                        |> Expect.isTrue "told"

                        effects |> sent |> Expect.hasLength "one open" 1
                    }
                ]

            test "a Session that opens over a page holding a plan: the plan waits for the new patient" {
                let session = { SessionMachineTests.full with Head = Some head }

                let lanes, effects, _ =
                    { Lanes.initial (Some draft) with
                        Session = SessionState.resuming
                        OrderPlan = held two None
                    }
                    |> Lanes.transition
                        (counter ())
                        (LanesMsg.Session(SessionMsg.Resumed(Ok(ResumeResult.Found session))))

                effects
                |> List.exists (
                    function
                    | LanesEffect.Plan(OrderPlanEffect.CallPlan _) -> true
                    | _ -> false
                )
                |> Expect.isFalse "nothing opens on the patient before"

                lanes.OrderPlan
                |> OrderPlanState.view
                |> Expect.equal "the version waits" OrderPlanView.NoPatient

                effects
                |> List.tryHead
                |> Expect.equal "before the clear, the patient is expected" (Some LanesEffect.PatientExpected)
            }

            test "a Session that brings the patient the page holds answered: the version still opens" {
                let session =
                    { SessionMachineTests.full with
                        Head = Some head
                        PatientContext =
                            SessionMachineTests.full.PatientContext
                            |> Option.map (fun context -> { context with Patient = Some measured })
                    }

                let newId = counter ()

                let lanes =
                    play
                        newId
                        { Lanes.initial None with Session = SessionState.resuming }
                        [
                            LanesMsg.Patient(
                                PatientMsg.Changed(Some measured, PatientDraftPolicy.Estimates.Renewed, "p-1")
                            )
                            LanesMsg.Patient(PatientMsg.Answered("p-1", Ok measured))
                        ]
                    |> List.last
                    |> fun (lanes, _, _) -> lanes

                let lanes, effects, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Session(SessionMsg.Resumed(Ok(ResumeResult.Found session))))

                let request =
                    effects
                    |> List.tryPick (
                        function
                        | LanesEffect.Patient(PatientEffect.CallPatient(_, request)) -> Some request
                        | _ -> None
                    )

                request |> Expect.isSome "the same patient is asked for again"

                lanes.OrderPlan
                |> OrderPlanState.view
                |> Expect.equal "the version waits for the answer" OrderPlanView.NoPatient

                let _, effects, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered(request.Value, Ok measured)))

                effects
                |> List.tryPick (
                    function
                    | LanesEffect.Plan(OrderPlanEffect.CallPlan(OrderPlanCommand.Open(_, contexts), _)) -> Some contexts
                    | _ -> None
                )
                |> Expect.equal "the plan opens the signed orders" (Some head.OrderContexts)
            }

            test "a Session without a patient over a page holding one: the pages clear as usual" {
                let session = { SessionMachineTests.full with PatientContext = None }

                let _, effects, _ =
                    { Lanes.initial (Some draft) with Session = SessionState.resuming }
                    |> Lanes.transition
                        (counter ())
                        (LanesMsg.Session(SessionMsg.Resumed(Ok(ResumeResult.Found session))))

                effects
                |> List.contains LanesEffect.PatientExpected
                |> Expect.isFalse "no patient comes"
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

            test "the Session closed: the signing lane is told, and goes idle" {
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
                |> Expect.equal "the signing step" (LanesStep.Signing(SigningMsg.SessionEnded, SigningState.idle, []))
            }

            test "the Session ended during a submission: the outcome is told when the answer lands, then idle" {
                let submitting = SigningFixtures.submitting

                let lanes =
                    { Lanes.initial (Some draft) with
                        Session = SessionState.opened SessionMachineTests.full None
                        Signing = submitting
                    }

                let lanes, _, _ =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Session SessionMsg.CloseSession)

                lanes.Signing
                |> SigningState.view
                |> Expect.equal "still submitting" (SigningState.view submitting)

                // the close answered: told once, at the step that ended the Session
                let lanes, _, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Session SessionMsg.SessionClosed)

                steps
                |> List.exists (
                    function
                    | LanesStep.Signing _ -> true
                    | _ -> false
                )
                |> Expect.isFalse "no second signing step"

                let lanes, effects, _ =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Signing(SigningFixtures.submitted "k-1"))

                lanes.Signing |> SigningState.view |> Expect.equal "idle" SigningView.Idle

                effects
                |> List.choose (
                    function
                    | LanesEffect.Signing e -> Some e
                    | _ -> None
                )
                |> Expect.equal "the outcome alone" [ SigningEffect.TellSigned SigningFixtures.signed ]
            }

            test "reading the EHR again goes to the Session at once" {
                route (fun () -> "r-1") (Lanes.initial None) (LanesEffect.Signing SigningEffect.ReadEhrAgain)
                |> Expect.equal "the Session alone" [ LanesMsg.Session SessionMsg.RefreshPatient ]
            }

            testList
                "a new patient"
                (let another = { measured with Department = Some "other" }
                 let renewed = PatientDraftPolicy.Estimates.Renewed

                 let changed pat request = LanesMsg.Patient(PatientMsg.Changed(Some pat, renewed, request))
                 let answered request result = LanesMsg.Patient(PatientMsg.Answered(request, result))

                 /// The lanes with a new patient started over a plan with one order.
                 let started newId =
                     let lanes, _, _ = Lanes.initial (Some measured) |> Lanes.transition newId (changed measured "p-1")
                     let lanes, effects, _ = lanes |> Lanes.transition newId (answered "p-1" (Ok measured))
                     let request = effects |> planRequest |> Option.defaultWith (fun () -> failtest "no open")

                     let lanes, _, _ =
                         lanes
                         |> Lanes.transition newId (LanesMsg.Plan(OrderPlanMsg.Answered(request, Ok one)))

                     let lanes, _, _ =
                         lanes
                         |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Changed(None, renewed, "p-2")))

                     lanes |> Lanes.transition newId (changed another "p-2")

                 [
                     test "the plan empties and the patient goes out" {
                         let lanes, effects, _ = started (counter ())

                         lanes.OrderPlan
                         |> OrderPlanState.view
                         |> Expect.equal "the orders are gone" OrderPlanView.NoPatient

                         effects
                         |> List.contains (LanesEffect.Patient(PatientEffect.CallPatient(another, "p-2")))
                         |> Expect.isTrue "the patient goes out"
                     }

                     test "the answer opens an empty plan, not an update of the old one" {
                         let newId = counter ()
                         let lanes, _, _ = started newId
                         let _, effects, _ = lanes |> Lanes.transition newId (answered "p-2" (Ok another))

                         effects
                         |> List.exists (
                             function
                             | LanesEffect.Plan(OrderPlanEffect.CallPlan(OrderPlanCommand.Open(pat, [||]), _)) ->
                                 pat = another
                             | _ -> false
                         )
                         |> Expect.isTrue "an empty plan for the new patient"
                     }

                     test "after a failed patient call everything stays empty, and the data can go out again" {
                         let newId = counter ()
                         let lanes, _, _ = started newId
                         let lanes, _, _ = lanes |> Lanes.transition newId (answered "p-2" (Error [| "no" |]))

                         lanes.Patient |> PatientState.draft |> Expect.isNone "the panel is empty"

                         lanes.OrderPlan
                         |> OrderPlanState.view
                         |> Expect.equal "the plan too" OrderPlanView.NoPatient

                         let _, effects, _ = lanes |> Lanes.transition newId (changed another "p-3")

                         effects
                         |> List.contains (LanesEffect.Patient(PatientEffect.CallPatient(another, "p-3")))
                         |> Expect.isTrue "sent"
                     }
                 ])

            test "a plan that did not follow the patient data is behind until it does" {
                let newId = counter ()

                let heavier = { measured with Weight = { measured.Weight with Measured = Some 40000<gram> } }

                let start = { Lanes.initial (Some measured) with OrderPlan = OrderPlanState.held measured two None }

                start |> behind |> Expect.isFalse "plan and panel agree"

                let lanes, _, _ =
                    start
                    |> Lanes.transition
                        newId
                        (LanesMsg.Patient(PatientMsg.Changed(Some heavier, PatientDraftPolicy.Estimates.Kept, "p-1")))

                let lanes, effects, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered("p-1", Ok heavier)))

                lanes |> behind |> Expect.isFalse "not while the update is out"

                let failed, _, _ =
                    lanes
                    |> Lanes.transition
                        newId
                        (LanesMsg.Plan(OrderPlanMsg.Answered((planRequest effects).Value, Error [| "no" |])))

                failed |> behind |> Expect.isTrue "the update failed"

                failed
                |> out
                |> BusyPolicy.page Page.Page.Prescribe
                |> Expect.isTrue "every page waits for the try again"

                let lanes, effects, _ = failed |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.GiveAgain "p-2"))

                effects
                |> List.contains (LanesEffect.Patient(PatientEffect.CallPatient(heavier, "p-2")))
                |> Expect.isTrue "the data of the panel are asked again"

                let lanes, effects, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered("p-2", Ok heavier)))

                let lanes, _, _ =
                    lanes
                    |> Lanes.transition
                        newId
                        (LanesMsg.Plan(OrderPlanMsg.Answered((planRequest effects).Value, Ok two)))

                lanes |> behind |> Expect.isFalse "the plan followed"
            }

            test "a failed plan update lets the same patient data out again, up to the plan" {
                let newId = counter ()
                let changed = { measured with Department = Some "other" }

                let given request =
                    LanesMsg.Patient(PatientMsg.Changed(Some changed, PatientDraftPolicy.Estimates.Renewed, request))

                let answered request = LanesMsg.Patient(PatientMsg.Answered(request, Ok changed))
                let requestOf effects =
                    effects |> planRequest |> Option.defaultWith (fun () -> failtest "no request")

                let lanes, _, _ =
                    Lanes.initial (Some measured)
                    |> Lanes.transition
                        newId
                        (LanesMsg.Patient(
                            PatientMsg.Changed(Some measured, PatientDraftPolicy.Estimates.Renewed, "p-1")
                        ))

                let lanes, effects, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Patient(PatientMsg.Answered("p-1", Ok measured)))

                let lanes, _, _ =
                    lanes
                    |> Lanes.transition newId (LanesMsg.Plan(OrderPlanMsg.Answered(requestOf effects, Ok one)))

                let lanes, _, _ = lanes |> Lanes.transition newId (given "p-2")
                let lanes, effects, _ = lanes |> Lanes.transition newId (answered "p-2")

                let lanes, _, _ =
                    lanes
                    |> Lanes.transition
                        newId
                        (LanesMsg.Plan(OrderPlanMsg.Answered(requestOf effects, Error [| "no" |])))

                let lanes, effects, _ = lanes |> Lanes.transition newId (given "p-3")

                effects
                |> List.contains (LanesEffect.Patient(PatientEffect.CallPatient(changed, "p-3")))
                |> Expect.isTrue "the same data goes out"

                let _, effects, _ = lanes |> Lanes.transition newId (answered "p-3")

                effects |> planRequest |> Expect.isSome "and the plan is sent the update again"
            }

            test "a signature answered: the token, the patient and the signed plan land in the same transition" {
                let lanes =
                    { Lanes.initial (Some draft) with
                        Session = SessionState.opened SessionMachineTests.full None
                        Signing = SigningFixtures.submitting
                    }

                let lanes, effects, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Signing(SigningFixtures.submitted "k-1"))

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
                |> List.contains (LanesEffect.Signing(SigningEffect.TellSigned SigningFixtures.signed))
                |> Expect.isTrue "the message comes out"

                match SessionState.view lanes.Session with
                | SessionView.Open opened ->
                    opened.Head
                    |> Expect.equal "the head is the version signed" (Some SigningFixtures.signed)
                | view -> failtest $"not open: %A{view}"
            }

            test "a signature over the patient data the lanes hold sends nothing more once the token is renewed" {
                let newId = counter ()
                let pat = measured |> asPatient
                let ctx = { OrderContextMachineTests.Fixtures.paracetamol with Patient = pat }

                // the patient set and answered, the workbench and the plan answered for it
                let lanes, _, _ =
                    play
                        newId
                        { Lanes.initial (Some measured) with
                            Session = SessionState.opened SessionMachineTests.full None
                            Signing = SigningFixtures.submitting
                        }
                        [
                            LanesMsg.Patient(
                                PatientMsg.Changed(Some measured, PatientDraftPolicy.Estimates.Renewed, "p-0")
                            )
                            LanesMsg.Patient(PatientMsg.Answered("p-0", Ok pat))
                            LanesMsg.Workbench(OrderContextMsg.Answered("id-1", Ok(OrderContextResponse.Evaluated ctx)))
                            LanesMsg.Plan(OrderPlanMsg.Answered("id-2", Ok one))
                        ]
                    |> List.last

                // the signature answered with the patient as signed, the same data
                let signed = { SigningFixtures.signed with Patient = pat }

                let submitted =
                    SigningMsg.SubmitAnswered("k-1", Ok(SigningResponse.Submitted(signed, OpenedToken "t2", pat)))

                let after, effects, steps = lanes |> Lanes.transition newId (LanesMsg.Signing submitted)

                effects
                |> List.filter (
                    function
                    | LanesEffect.Patient(PatientEffect.CallPatient _)
                    | LanesEffect.Workbench(OrderContextEffect.CallContext _)
                    | LanesEffect.Plan(OrderPlanEffect.CallPlan _) -> true
                    | _ -> false
                )
                |> Expect.isEmpty "no call"

                steps
                |> List.exists (
                    function
                    | LanesStep.Workbench(OrderContextMsg.PatientDataChanged _, _, _)
                    | LanesStep.Plan(OrderPlanMsg.PatientDataChanged _, _, _) -> true
                    | _ -> false
                )
                |> Expect.isFalse "no patient data changed"

                PatientState.answered after.Patient
                |> Expect.equal "the patient as before" (PatientState.answered lanes.Patient)
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
                        | LanesStep.Patient(PatientMsg.Refreshed(Some p, request), _, _) when p = measured ->
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
                        Signing = SigningFixtures.submitting
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

            test "the inactive-tab answer during a signature says nothing; the sign's own token lands" {
                let session = SessionMachineTests.full

                let lanes =
                    { Lanes.initial (Some draft) with
                        Session = SessionState.opened session None
                        Signing = SigningState.submitting "c-1" SigningFixtures.plan "k-1"
                    }

                let answer =
                    LanesMsg.Answer(
                        LanesMsg.Patient(PatientMsg.Answered("p-1", Ok measured)),
                        session.OpenedToken,
                        Some(RecordNotice.Ended SessionEnding.InactiveTab)
                    )

                let lanes, _, _ = lanes |> Lanes.transition (counter ()) answer

                lanes.Session
                |> SessionState.view
                |> Expect.equal "still open" (SessionView.Open session)
            }

            test "a notice that comes in by itself, as the drug names' does, is held off during a signature too" {
                let session = SessionMachineTests.full

                let lanes =
                    { Lanes.initial (Some draft) with
                        Session = SessionState.opened session None
                        Signing = SigningState.submitting "c-1" SigningFixtures.plan "k-1"
                    }

                let notice =
                    LanesMsg.Session(
                        SessionMsg.NoticeReceived(session.OpenedToken, RecordNotice.Ended SessionEnding.InactiveTab)
                    )

                let lanes, effects, _ = lanes |> Lanes.transition (counter ()) notice

                (lanes.Session |> SessionState.view, effects)
                |> Expect.equal "still open, nothing to do" (SessionView.Open session, [])
            }

            test "the inactive-tab answer without a signature shows the gate" {
                let session = SessionMachineTests.full
                let lanes = { Lanes.initial (Some draft) with Session = SessionState.opened session None }

                let answer =
                    LanesMsg.Answer(
                        LanesMsg.Patient(PatientMsg.Answered("p-1", Ok measured)),
                        session.OpenedToken,
                        Some(RecordNotice.Ended SessionEnding.InactiveTab)
                    )

                let lanes, _, _ = lanes |> Lanes.transition (counter ()) answer

                lanes.Session
                |> SessionState.view
                |> Expect.equal "the gate" (SessionView.Ended SessionEnding.InactiveTab)
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

            test "the prescribe click: the order into the plan and the plan page opened, the workbench untouched" {
                let lanes = twoOrdersOnWorkbench ()

                let after, effects, steps =
                    lanes |> Lanes.transition (counter ()) (LanesMsg.Prescribe(None, "o-2", "r-1"))

                steps
                |> List.choose (
                    function
                    | LanesStep.Plan(OrderPlanMsg.Change(OrderPlanChange.Add(ctx, _), "r-1"), _, _) ->
                        Some(ctx.Scenarios |> Array.map _.Order.Id |> String.concat ",")
                    | LanesStep.Workbench _ -> Some "workbench"
                    | _ -> None
                )
                |> Expect.equal "the order o-2 alone, no reset" [ "o-2" ]

                effects
                |> List.last
                |> Expect.equal "the plan page opened" LanesEffect.GoToPlanPage

                after.OrderContext
                |> OrderContextState.narrowedTo "o-2"
                |> Expect.isSome "the order is still on the workbench"

                lanes
                |> Lanes.transition (counter ()) (LanesMsg.Prescribe(None, "o-9", "r-1"))
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
                    lanes |> Lanes.transition (counter ()) (LanesMsg.Prescribe(None, "o-1", "r-1"))

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

            test "a prescription that fails: the plan as before, the workbench narrowed to the order, no reset" {
                let clicked, _, _ =
                    twoOrdersOnWorkbench ()
                    |> Lanes.transition (counter ()) (LanesMsg.Prescribe(None, "o-2", "r-1"))

                let after, effects, steps =
                    clicked
                    |> Lanes.transition (counter ()) (LanesMsg.Plan(OrderPlanMsg.Answered("r-1", Error [| "down" |])))

                steps
                |> List.exists (
                    function
                    | LanesStep.Workbench _ -> true
                    | _ -> false
                )
                |> Expect.isFalse "no reset"

                effects
                |> List.contains (LanesEffect.Plan(OrderPlanEffect.TellError [| "down" |]))
                |> Expect.isTrue "the error told"

                after.OrderPlan
                |> OrderPlanState.view
                |> Expect.equal "the plan as before" (OrderPlanView.Settled(one, None))

                after.OrderContext
                |> OrderContextState.narrowedTo "o-2"
                |> Expect.isSome "the order is still on the workbench"

                after
                |> Lanes.transition (counter ()) (LanesMsg.Prescribe(None, "o-2", "r-2"))
                |> fun (_, effects, _) -> planRequest effects
                |> Expect.equal "the retry is the same click" (Some "r-2")
            }

            test "a prescription that lands: the plan, then the workbench reset, in the transition of the answer" {
                let clicked, _, _ =
                    twoOrdersOnWorkbench ()
                    |> Lanes.transition (counter ()) (LanesMsg.Prescribe(None, "o-2", "r-1"))

                let after, effects, steps =
                    clicked
                    |> Lanes.transition (counter ()) (LanesMsg.Plan(OrderPlanMsg.Answered("r-1", Ok two)))

                steps
                |> List.choose (
                    function
                    | LanesStep.Plan(OrderPlanMsg.Answered _, _, _) -> Some "answer"
                    | LanesStep.Workbench(OrderContextMsg.Reset "id-1", _, _) -> Some "reset"
                    | _ -> None
                )
                |> Expect.equal "the answer, then the reset" [ "answer"; "reset" ]

                workbenchRequest effects |> Expect.equal "the reset goes out" (Some "id-1")

                effects
                |> List.contains LanesEffect.GoToPlanPage
                |> Expect.isFalse "the page moved at the click"

                after.OrderContext
                |> OrderContextState.narrowedTo "o-2"
                |> Expect.isNone "the workbench no longer shows the order"
            }

            test "a message for one machine only runs that machine" {
                let lanes = Lanes.initial (Some draft)

                let _, _, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Workbench(OrderContextMsg.SelectScenario None))

                steps |> List.length |> Expect.equal "one step" 1
            }
        ]


/// The nutrition slots on the lanes: each the order context machine with a nutrition category.
module SlotLaneFixtures =

    let pat: Patient = { Informedica.GenPRES.Shared.Models.Patient.empty with Department = Some "NEO" }

    let tpn = OrderCategory.Nutrition NutritionCategory.TPN

    let empty = Lanes.initial None


    /// The lanes after a message, with its effects.
    let after msg lanes =
        let lanes, effects, _ = Lanes.step msg lanes
        lanes, effects


    /// The lanes with the workbench evaluated for the patient, the answer landed.
    let withPatient =
        let lanes, _ =
            empty
            |> after (LanesMsg.Workbench(OrderContextMsg.PatientDataChanged(Some pat, "w-1")))

        lanes
        |> after (
            LanesMsg.Workbench(
                OrderContextMsg.Answered(
                    "w-1",
                    Ok(OrderContextResponse.Evaluated(OrderContextState.emptyMedication pat))
                )
            )
        )
        |> fst


    /// The order context commands sent for the slot with the id: the command, the category and patient of
    /// its context, and the request id.
    let slotCalls id effects =
        effects
        |> List.choose (
            function
            | LanesEffect.Slot(slot, OrderContextEffect.CallContext(OrderContextCommand.Command(cmd, ctx), request)) when
                slot = id
                ->
                Some(cmd, ctx.Category, ctx.Patient, request)
            | _ -> None
        )


    let slotOf id (lanes: LanesState) = lanes |> Lanes.slotOf id |> Option.get


open SlotLaneFixtures


[<Tests>]
let slotLaneTests =
    testList
        "the slot lane"
        [
            test "a slot starts with the clear of all filters over the empty context of its category" {
                withPatient
                |> after (LanesMsg.StartSlot("s-1", NutritionCategory.TPN, "r-1"))
                |> snd
                |> slotCalls "s-1"
                |> Expect.equal "one clear, a TPN context" [ OrderViewCommand.ClearAllFilterProperty, tpn, pat, "r-1" ]
            }

            test "no slot starts while the workbench has no patient" {
                let lanes, effects = empty |> after (LanesMsg.StartSlot("s-1", NutritionCategory.TPN, "r-1"))

                effects |> Expect.isEmpty "nothing sent"
                lanes.Slots |> Expect.isEmpty "no slot"
            }

            test "new patient data starts every slot again" {
                let lanes =
                    withPatient
                    |> after (LanesMsg.StartSlot("s-1", NutritionCategory.TPN, "r-1"))
                    |> fst
                    |> after (LanesMsg.StartSlot("s-2", NutritionCategory.Lipid, "r-2"))
                    |> fst

                LanesEffect.Patient(PatientEffect.SetPatientData(Some pat))
                |> Lanes.route (fun () -> "r-3") lanes
                |> List.choose (
                    function
                    | LanesMsg.Slot(id, OrderContextMsg.PatientDataChanged _) -> Some id
                    | _ -> None
                )
                |> Expect.equal "both slots" [ "s-1"; "s-2" ]
            }

            test "another patient removes the slots, a data update starts them again" {
                let lanes =
                    withPatient
                    |> after (LanesMsg.StartSlot("s-1", NutritionCategory.TPN, "r-1"))
                    |> fst
                    |> after (LanesMsg.StartSlot("s-2", NutritionCategory.Lipid, "r-2"))
                    |> fst

                // another patient arrives as the patient cleared, then the new patient
                let cleared =
                    LanesEffect.Patient(PatientEffect.SetPatientData None)
                    |> Lanes.route (fun () -> "r-3") lanes
                    |> List.fold (fun lanes msg -> lanes |> after msg |> fst) lanes

                cleared.Slots |> Expect.isEmpty "the slots are gone"

                // the same patient with other data, once the first answer landed
                let updated = { pat with Department = Some "ICK" }
                let answered = OrderContextResponse.Evaluated(OrderContextState.emptyMedication pat)

                lanes
                |> after (LanesMsg.Slot("s-1", OrderContextMsg.Answered("r-1", Ok answered)))
                |> fst
                |> after (LanesMsg.Slot("s-1", OrderContextMsg.PatientDataChanged(Some updated, "r-4")))
                |> snd
                |> slotCalls "s-1"
                |> Expect.equal
                    "the data update restarts the slot"
                    [ OrderViewCommand.ClearAllFilterProperty, tpn, updated, "r-4" ]
            }

            test "an answer lands on its own slot, not on the workbench" {
                let evaluated =
                    { OrderContextState.emptyMedication pat with
                        Category = tpn
                        Id = "s-1"
                    }

                let lanes =
                    withPatient
                    |> after (LanesMsg.StartSlot("s-1", NutritionCategory.TPN, "r-1"))
                    |> fst
                    |> after (
                        LanesMsg.Slot(
                            "s-1",
                            OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated evaluated))
                        )
                    )
                    |> fst

                lanes
                |> slotOf "s-1"
                |> OrderContextState.view
                |> Expect.equal "settled" (OrderContextView.Settled evaluated)

                lanes.OrderContext
                |> OrderContextState.view
                |> Expect.equal
                    "the workbench as it was"
                    (OrderContextView.Settled(OrderContextState.emptyMedication pat))
            }

            test "a slot request out disables the nutrition page only" {
                let busy =
                    withPatient
                    |> after (LanesMsg.StartSlot("s-1", NutritionCategory.TPN, "r-1"))
                    |> fst
                    |> out

                busy |> Expect.equal "a slot request" [ Loads.Request.Slots ]
                busy
                |> BusyPolicy.page Page.Page.Nutrition
                |> Expect.isTrue "the nutrition page waits"
                busy
                |> BusyPolicy.page Page.Page.Prescribe
                |> Expect.isFalse "the workbench does not"
            }

            test "an answer for a slot a start-over removed is dropped" {
                let lanes =
                    withPatient
                    |> after (LanesMsg.StartSlot("s-1", NutritionCategory.TPN, "r-1"))
                    |> fst
                    |> after (LanesMsg.StartOver None)
                    |> fst

                lanes.Slots |> Expect.isEmpty "no slots"

                lanes
                |> after (
                    LanesMsg.Slot(
                        "s-1",
                        OrderContextMsg.Answered(
                            "r-1",
                            Ok(OrderContextResponse.Evaluated(OrderContextState.emptyMedication pat))
                        )
                    )
                )
                |> snd
                |> Expect.isEmpty "nothing happens"
            }

            test "the workbench stays a medication context" {
                empty
                |> after (LanesMsg.Workbench(OrderContextMsg.PatientDataChanged(Some pat, "w-1")))
                |> snd
                |> List.choose (
                    function
                    | LanesEffect.Workbench(OrderContextEffect.CallContext(OrderContextCommand.Command(_, ctx), _)) ->
                        Some ctx.Category
                    | _ -> None
                )
                |> Expect.equal "medication" [ OrderCategory.Medication ]
            }

            test "a slot closed is taken out, the other slot stays" {
                let lanes, effects, steps =
                    withPatient
                    |> after (LanesMsg.StartSlot("s-1", NutritionCategory.EnteralFeeding, "r-1"))
                    |> fst
                    |> after (LanesMsg.StartSlot("s-2", NutritionCategory.TPN, "r-2"))
                    |> fst
                    |> Lanes.step (LanesMsg.RemoveSlot "s-1")

                lanes.Slots |> List.map fst |> Expect.equal "the TPN slot" [ "s-2" ]
                effects |> Expect.isEmpty "nothing sent"
                steps |> Expect.equal "one step" [ LanesStep.SlotRemoved "s-1" ]
            }

            test "the answer still out for a slot closed is dropped" {
                let removed =
                    withPatient
                    |> after (LanesMsg.StartSlot("s-1", NutritionCategory.EnteralFeeding, "r-1"))
                    |> fst
                    |> after (LanesMsg.RemoveSlot "s-1")
                    |> fst

                let answered = OrderContextResponse.Evaluated(OrderContextState.emptyMedication pat)

                removed
                |> Lanes.step (LanesMsg.Slot("s-1", OrderContextMsg.Answered("r-1", Ok answered)))
                |> Expect.equal "nothing changes" (removed, [], [])
            }

            testList
                "in the order they were started"
                [
                    // the ids sort the other way round, so a map by id would list the TPN slot first
                    let started =
                        withPatient
                        |> after (LanesMsg.StartSlot("s-b", NutritionCategory.EnteralFeeding, "r-1"))
                        |> fst
                        |> after (LanesMsg.StartSlot("s-a", NutritionCategory.TPN, "r-2"))
                        |> fst
                        |> after (LanesMsg.StartSlot("s-c", NutritionCategory.Lipid, "r-3"))
                        |> fst

                    test "the slots keep the order they were started in" {
                        started.Slots
                        |> List.map fst
                        |> Expect.equal "feeding, TPN, lipids" [ "s-b"; "s-a"; "s-c" ]
                    }

                    test "an answer to a slot keeps the slot in its place" {
                        let answered = OrderContextResponse.Evaluated(OrderContextState.emptyMedication pat)

                        started
                        |> after (LanesMsg.Slot("s-a", OrderContextMsg.Answered("r-2", Ok answered)))
                        |> fst
                        |> _.Slots
                        |> List.map fst
                        |> Expect.equal "the same order" [ "s-b"; "s-a"; "s-c" ]
                    }

                    test "a slot closed leaves the others in their order" {
                        started
                        |> after (LanesMsg.RemoveSlot "s-a")
                        |> fst
                        |> _.Slots
                        |> List.map fst
                        |> Expect.equal "feeding, lipids" [ "s-b"; "s-c" ]
                    }
                ]
        ]


/// The workbench and a nutrition slot prescribed into the plan.
module SlotPrescribeFixtures =

    let twoOrders name =
        { context "" name with Scenarios = [| scenario "o-1" name; scenario "o-2" name |] }


    /// The plan shown, the workbench with two paracetamol orders and the slot s-1 with two TPN orders.
    let prescribing =
        { Lanes.initial (Some draft) with
            OrderPlan = shown
            OrderContext = OrderContextState.held patient (twoOrders "paracetamol")
            Slots = [ "s-1", OrderContextState.held patient (twoOrders "TPN") ]
        }


    /// The contexts added to the plan by the steps taken: the context id and the order ids.
    let added steps =
        steps
        |> List.choose (
            function
            | LanesStep.Plan(OrderPlanMsg.Change(OrderPlanChange.Add(ctx, _), _), _, _) ->
                Some(ctx.Id, ctx.Scenarios |> Array.map _.Order.Id)
            | _ -> None
        )


open SlotPrescribeFixtures


[<Tests>]
let slotPrescribeTests =
    testList
        "the slot prescribe"
        [
            test "a slot is prescribed with its slot id, and the nutrition page stays" {
                let _, effects, steps =
                    prescribing
                    |> Lanes.transition (counter ()) (LanesMsg.Prescribe(Some "s-1", "o-2", "r-1"))

                steps |> added |> Expect.equal "the TPN order o-2 of s-1" [ "s-1", [| "o-2" |] ]
                effects
                |> List.contains LanesEffect.GoToPlanPage
                |> Expect.isFalse "no page switch"
            }

            test "the workbench is prescribed with an empty id, and the plan page opens" {
                let _, effects, steps =
                    prescribing
                    |> Lanes.transition (counter ()) (LanesMsg.Prescribe(None, "o-2", "r-1"))

                steps |> added |> Expect.equal "the paracetamol order o-2" [ "", [| "o-2" |] ]
                effects
                |> List.last
                |> Expect.equal "the plan page opened" LanesEffect.GoToPlanPage
            }

            test "nothing is added for an order the slot does not show, or a slot that is gone" {
                prescribing
                |> Lanes.transition (counter ()) (LanesMsg.Prescribe(Some "s-1", "o-9", "r-1"))
                |> fun (_, effects, steps) -> effects, steps
                |> Expect.equal "not shown" ([], [])

                prescribing
                |> Lanes.transition (counter ()) (LanesMsg.Prescribe(Some "s-9", "o-2", "r-1"))
                |> fun (_, effects, steps) -> effects, steps
                |> Expect.equal "no slot" ([], [])
            }

            test "the answer to an add names the context sent" {
                let slot = { context "s-1" "TPN" with Category = OrderCategory.Nutrition NutritionCategory.TPN }
                let busy = recalculating one None "r-1" (OrderPlanCommand.AddOrderContext(one, slot, [||]))

                transition (OrderPlanMsg.Answered("r-1", Ok two)) busy
                |> snd
                |> List.contains (OrderPlanEffect.OrderAdded(Some "s-1"))
                |> Expect.isTrue "the slot s-1"
            }

            test "the order added empties the context it came from" {
                LanesEffect.Plan(OrderPlanEffect.OrderAdded(Some "s-1"))
                |> Lanes.route (fun () -> "r-2") prescribing
                |> Expect.equal "the slot" [ LanesMsg.Slot("s-1", OrderContextMsg.Reset "r-2") ]

                LanesEffect.Plan(OrderPlanEffect.OrderAdded None)
                |> Lanes.route (fun () -> "r-2") prescribing
                |> Expect.equal "the workbench" [ LanesMsg.Workbench(OrderContextMsg.Reset "r-2") ]
            }

            test "a slot narrowed to one order is work that leaving the page would lose" {
                let narrowed = context "s-1" "TPN"

                UnsignedWorkPolicy.hasUnsignedWork
                    None
                    [ narrowed ]
                    SigningMachine.SigningView.Idle
                    PlanWorkPolicy.PlanWork.AsSigned
                |> Expect.isTrue "a narrowed slot"

                UnsignedWorkPolicy.hasUnsignedWork
                    None
                    [ twoOrders "TPN" ]
                    SigningMachine.SigningView.Idle
                    PlanWorkPolicy.PlanWork.AsSigned
                |> Expect.isFalse "a slot with two orders"

                UnsignedWorkPolicy.hasUnsignedWork
                    None
                    []
                    SigningMachine.SigningView.Idle
                    PlanWorkPolicy.PlanWork.AsSigned
                |> Expect.isFalse "no slots"
            }

            test "another patient asks first when a slot is narrowed to one order" {
                NewPatientPolicy.asks SessionMachine.SessionView.Anonymous None None [ context "s-1" "TPN" ]
                |> Expect.isTrue "a narrowed slot"

                NewPatientPolicy.asks SessionMachine.SessionView.Anonymous None None []
                |> Expect.isFalse "nothing to lose"
            }
        ]


/// The prescribe with the linked offers of OrderContextMachineTests turned on.
module PrescribeLinkedFixtures =

    /// The contexts added to the plan by the steps taken: the main context id and order id, and the order ids
    /// of the linked orders.
    let addedWithLinked steps =
        steps
        |> List.choose (
            function
            | LanesStep.Plan(OrderPlanMsg.Change(OrderPlanChange.Add(ctx, linked), _), _, _) ->
                Some(
                    ctx.Id,
                    ctx.Scenarios |> Array.map _.Order.Id,
                    linked |> Array.collect (_.Scenarios >> Array.map _.Order.Id)
                )
            | _ -> None
        )


open PrescribeLinkedFixtures
open OrderContextMachineTests.LinkedOfferFixtures


[<Tests>]
let prescribeLinkedTests =
    testList
        "the prescribe with linked orders"
        [
            test "the prescribe adds ataluren with its evening dose turned on" {
                let lanes =
                    { Lanes.initial (Some draft) with
                        OrderPlan = shown
                        OrderContext = eveningOn
                    }

                let _, _, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Prescribe(None, "o-main", "r-3"))

                steps
                |> addedWithLinked
                |> Expect.equal "o-main with o-avond" [ ("", [| "o-main" |], [| "o-avond" |]) ]
            }

            test "the prescribe sends the linked orders in the add" {
                OrderPlanChange.Add(atalurenMain, eveningOn |> OrderContextState.linkedTurnedOn)
                |> OrderPlanChange.command one
                |> Option.map (
                    function
                    | OrderPlanCommand.AddOrderContext(_, _, linked) ->
                        linked |> Array.map (fun c -> c.Scenarios[0].Order.Id)
                    | _ -> [||]
                )
                |> Expect.equal "o-avond in the add" (Some [| "o-avond" |])
            }

            test "the prescribe adds ataluren alone while the evening dose is off" {
                let lanes =
                    { Lanes.initial (Some draft) with
                        OrderPlan = shown
                        OrderContext = atalurenOffered
                    }

                let _, _, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Prescribe(None, "o-main", "r-3"))

                steps
                |> addedWithLinked
                |> Expect.equal "o-main alone" [ ("", [| "o-main" |], [||]) ]
            }

            test "the prescribe adds nothing while the evening dose is on with two scenarios" {
                let lanes =
                    { Lanes.initial (Some draft) with
                        OrderPlan = shown
                        OrderContext = OrderContextMachineTests.OfferCommandFixtures.eveningOfTwoOn
                    }

                let _, _, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Prescribe(None, "o-main", "r-3"))

                steps |> addedWithLinked |> Expect.isEmpty "refused"
            }

            test "a feeding slot is prescribed with BMF turned on" {
                let slot = answered selectScenario feeding [| bmf |] |> turnOn 0

                let lanes =
                    { Lanes.initial (Some draft) with
                        OrderPlan = shown
                        Slots = [ "s-1", slot ]
                    }

                let feedingOrder = "o-feeding"

                let _, _, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Prescribe(Some "s-1", feedingOrder, "r-3"))

                steps
                |> addedWithLinked
                |> Expect.equal "the feeding of s-1 with BMF" [ ("s-1", [| feedingOrder |], [| "o-bmf" |]) ]
            }

            test "the evening dose turned on in a slot reaches that slot" {
                let lanes =
                    { Lanes.initial (Some draft) with
                        OrderPlan = shown
                        Slots = [ "s-1", atalurenOffered ]
                    }

                let lanes, _, _ =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Slot("s-1", OrderContextMsg.TurnOfferOn 0))

                (lanes |> Lanes.slotOf "s-1" |> Option.get)
                |> turnedOn
                |> Expect.equal "on in s-1" [| (Some "ataluren", Some "o-avond") |]
                lanes.OrderContext |> turnedOn |> Expect.isEmpty "the workbench has none"
            }
        ]
