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
                    | LanesStep.Plan(OrderPlanMsg.Version(v, _), _, _) -> v = head
                    | _ -> false
                )
                |> Expect.isTrue "the plan got the saved orders"

                effects
                |> List.contains (LanesEffect.Session(SessionEffect.LoadCart head))
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

            test "a prescription answered: the workbench is emptied in the same transition" {
                let workbench = context "" "ibuprofen"

                let lanes =
                    { Lanes.initial (Some draft) with
                        OrderPlan = recalculating one None "r-1" (OrderPlanCommand.AddOrderContext(one, workbench))
                        OrderContext = OrderContextState.held patient (OrderContextState.emptyFor patient)
                    }

                let _, effects, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Plan(OrderPlanMsg.Answered("r-1", Ok two)))

                steps
                |> List.exists (
                    function
                    | LanesStep.Workbench(OrderContextMsg.Reset _, _, _) -> true
                    | _ -> false
                )
                |> Expect.isTrue "the workbench reset"

                effects
                |> List.contains (LanesEffect.Plan OrderPlanEffect.GoToPlanPage)
                |> Expect.isTrue "the page switch comes out"
            }

            test "the Session closed: the signing lane is set idle, a step without a message" {
                let lanes =
                    { Lanes.initial (Some draft) with
                        Session = SessionState.opened SessionMachineTests.full None
                        Signing = SigningState.requesting one None "s-1"
                    }

                let lanes, _, steps = lanes |> Lanes.transition (counter ()) (LanesMsg.Session SessionMsg.Close)

                lanes.Signing |> SigningState.view |> Expect.equal "idle" SigningView.Idle

                steps
                |> List.last
                |> Expect.equal "the reset step" (LanesStep.SigningReset SigningState.idle)
            }

            test "a message for one machine only runs that machine" {
                let lanes = Lanes.initial (Some draft)

                let _, _, steps =
                    lanes
                    |> Lanes.transition (counter ()) (LanesMsg.Workbench(OrderContextMsg.Select None))

                steps |> List.length |> Expect.equal "one step" 1
            }
        ]
