module Informedica.GenPRES.Client.Core.Tests.StateMachines.OrderPlanMachineTests

open Informedica.GenPRES.Client.Core.Policies
open Informedica.GenPRES.Client.Core.StateMachines
open System
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api
open PlanWorkPolicy
open OrderPlanMachine
open Informedica.GenPRES.Client.Core.Tests.OrderFixtures


open OrderPlanFixtures


[<Tests>]
let tests =
    testList
        "OrderPlanState.transition"
        [
            testList
                "the patient"
                [
                    test "the same patient data given again does not touch the plan" {
                        transition (OrderPlanMsg.PatientDataChanged(Some patient, "r-2")) (settled one None)
                        |> Expect.equal "as it was, no request" (settled one None, [])
                    }

                    test "new patient data tells the summary with the answer" {
                        let busy, _ = transition (OrderPlanMsg.PatientDataChanged(Some other, "r-2")) (settled one None)

                        transition (OrderPlanMsg.Answered("r-2", Ok one)) busy
                        |> snd
                        |> List.contains (
                            OrderPlanEffect.TellPatientDataChanged
                                {
                                    Orders = 1
                                    Marked = 0
                                }
                        )
                        |> Expect.isTrue "one order, none marked"
                    }

                    test "the same data after a failed update is sent again, and the plan stays on the old patient" {
                        let busy, _ = transition (OrderPlanMsg.PatientDataChanged(Some other, "r-2")) (settled one None)
                        let failed, _ = transition (OrderPlanMsg.Answered("r-2", Error [| "no" |])) busy

                        OrderPlanState.patient failed |> Expect.equal "the old patient" (Some patient)

                        transition (OrderPlanMsg.PatientDataChanged(Some other, "r-3")) failed
                        |> snd
                        |> Expect.equal
                            "sent again"
                            [ OrderPlanEffect.CallPlan(OrderPlanCommand.UpdatePatient(other, one), "r-3") ]
                    }

                    test "new patient data with a request out is dropped, since the panel is disabled then" {
                        let busy, _ = transition (OrderPlanMsg.PatientDataChanged(Some other, "r-2")) (settled one None)

                        transition (OrderPlanMsg.PatientDataChanged(Some patient, "r-3")) busy
                        |> Expect.equal "the state stays, no request" (busy, [])
                    }

                    test "a failed update tells no summary, and that the plan did not follow" {
                        let busy, _ = transition (OrderPlanMsg.PatientDataChanged(Some other, "r-2")) (settled one None)

                        transition (OrderPlanMsg.Answered("r-2", Error [| "no" |])) busy
                        |> snd
                        |> Expect.equal
                            "the error, and the plan behind"
                            [ OrderPlanEffect.TellError [| "no" |]; OrderPlanEffect.PatientDataNotFollowed ]
                    }

                    test "an update with the same drugs starts no interaction check" {
                        let busy, _ = transition (OrderPlanMsg.PatientDataChanged(Some other, "r-2")) (settled one None)

                        transition (OrderPlanMsg.Answered("r-2", Ok one)) busy
                        |> snd
                        |> List.exists (
                            function
                            | OrderPlanEffect.CheckInteractions _ -> true
                            | _ -> false
                        )
                        |> Expect.isFalse "no check"
                    }

                    test "an update that brings a new drug starts the interaction check" {
                        let busy, _ = transition (OrderPlanMsg.PatientDataChanged(Some other, "r-2")) (settled one None)

                        transition (OrderPlanMsg.Answered("r-2", Ok two)) busy
                        |> snd
                        |> List.contains (OrderPlanEffect.CheckInteractions [ "paracetamol"; "ibuprofen" ])
                        |> Expect.isTrue "the new drug is checked"
                    }

                    test "a patient set opens the empty plan under the request; the answer shows it" {
                        let asked, effects =
                            transition (OrderPlanMsg.PatientDataChanged(Some patient, "r-1")) noPatientData

                        asked |> Expect.equal "asked" (loading patient [||] "r-1")

                        effects
                        |> Expect.equal "open" [ OrderPlanEffect.CallPlan(OrderPlanCommand.Open(draft, [||]), "r-1") ]

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) asked
                        |> Expect.equal
                            "shown, its one drug checked"
                            (settled one None,
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol" ]
                                 OrderPlanEffect.TellAnswered
                             ])
                    }

                    test "a patient changed recalculates the plan in the cart over the new patient, the dialog closed" {
                        let state, effects =
                            transition (OrderPlanMsg.PatientDataChanged(Some other, "r-2")) (settled one (Some "c-1"))

                        let update = OrderPlanCommand.UpdatePatient(otherDraft, one)

                        state
                        |> Expect.equal
                            "the plan in the cart, the patient update in the command"
                            (recalculatingFor patient one None "r-2" update)

                        effects
                        |> Expect.equal "the patient update" [ OrderPlanEffect.CallPlan(update, "r-2") ]

                        OrderPlanState.view state
                        |> Expect.equal
                            "the plan shown for the new patient meanwhile"
                            (OrderPlanView.Changing({ one with Patient = otherDraft }, None))
                    }

                    test "no patient: no plan, whatever was in flight answers to nothing" {
                        let busy = recalculating one None "r-1" (OrderPlanCommand.FilterRows(one.Filtered, one))

                        let state, effects = transition (OrderPlanMsg.PatientDataChanged(None, "r-2")) busy
                        state |> Expect.equal "no patient" noPatientData
                        effects |> Expect.isEmpty "nothing to do"

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) state
                        |> Expect.equal "the answer dropped" (noPatientData, [])
                    }
                ]

            testList
                "the cart"
                [
                    test "the signed version opens over the current patient" {
                        let opening, effects =
                            transition (OrderPlanMsg.OpenSignedPlan(head, "r-0")) shown
                            |> fst
                            |> transition (OrderPlanMsg.PatientDataChanged(Some patient, "r-1"))

                        opening
                        |> Expect.equal "loading the version" (loading patient two.OrderContexts "r-1")

                        effects
                        |> Expect.equal
                            "open with the contexts"
                            [
                                OrderPlanEffect.CallPlan(OrderPlanCommand.Open(draft, two.OrderContexts), "r-1")
                            ]

                        transition (OrderPlanMsg.Answered("r-1", Ok two)) opening
                        |> Expect.equal
                            "the newer shown, two drugs checked"
                            (settled two None,
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol"; "ibuprofen" ]
                                 OrderPlanEffect.TellAnswered
                             ])
                    }

                    test "the version arriving before its patient is stored and opened when the patient does" {
                        // a Session opens: the plan machine hears the version first, the patient it
                        // was opened with one message later
                        let stored, effects = transition (OrderPlanMsg.OpenSignedPlan(head, "r-1")) noPatientData

                        (stored, effects)
                        |> Expect.equal
                            "stored, nothing asked yet"
                            (OrderPlanState.withSignedVersion head.Patient two.OrderContexts, [])

                        transition (OrderPlanMsg.PatientDataChanged(Some patient, "r-2")) stored
                        |> Expect.equal
                            "the version's contexts opened for the patient"
                            (loading patient two.OrderContexts "r-2",
                             [
                                 OrderPlanEffect.CallPlan(OrderPlanCommand.Open(draft, two.OrderContexts), "r-2")
                             ])

                        transition (OrderPlanMsg.PatientDataChanged(None, "r-3")) stored
                        |> Expect.equal "no patient after all: the version is let go" (noPatientData, [])
                    }
                ]

            testList
                "a change"
                [
                    test "sent over the plan in the cart" {
                        let stale = { one with Filtered = [| "c-9" |] }
                        let cmd = OrderPlanCommand.RemoveOrderContexts(stale, [| "c-1" |])
                        let busy, effects = transition (pageMsg (cmd, "r-1")) shown
                        let rebased = OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |])

                        busy
                        |> Expect.equal
                            "in flight over the plan in the cart, one change of work"
                            (recalculating one None "r-1" rebased |> OrderPlanState.withWork PlanWork.Changed)

                        effects
                        |> Expect.equal "the rebased command" [ OrderPlanEffect.CallPlan(rebased, "r-1") ]
                    }

                    test
                        "a row filter goes over the plan in the cart, the rows in the command; the plan in the cart stays" {
                        let filtered = { one with Filtered = [| "c-1" |] }
                        let rows = OrderPlanCommand.FilterRows([| "c-1" |], one)

                        let busy, _ = transition (pageMsg (OrderPlanCommand.FilterRows([| "c-1" |], two), "r-1")) shown

                        busy
                        |> Expect.equal
                            "the plan in the cart, the rows in the command"
                            (recalculating one None "r-1" rows)

                        OrderPlanState.meanwhile one rows
                        |> Expect.equal "the pages show the rows chosen meanwhile" filtered

                        OrderPlanState.meanwhile one (OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |]))
                        |> Expect.equal "and the plan in the cart for any other change" one
                    }

                    test "the answer lands on its request: shown, the selection stays while its context is still there" {
                        let cmd = OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])
                        let busy = recalculating two (Some "c-2") "r-1" cmd

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) busy
                        |> Expect.equal
                            "the selection's context went with the change"
                            (settled one None |> OrderPlanState.withOpened two.OrderContexts,
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol" ]
                                 OrderPlanEffect.TellAnswered
                             ])

                        let recalculatingTwo = recalculating two (Some "c-1") "r-1" cmd

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) recalculatingTwo
                        |> Expect.equal
                            "the selection still holds"
                            (settled one (Some "c-1") |> OrderPlanState.withOpened two.OrderContexts,
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol" ]
                                 OrderPlanEffect.TellAnswered
                             ])

                        transition (OrderPlanMsg.Answered("r-9", Ok one)) recalculatingTwo
                        |> Expect.equal "a stale answer dropped" (recalculatingTwo, [])
                    }

                    test "a failed filter change goes back to the original, and says why" {
                        // the plan sent differs from the one in the cart, so that the test can tell which
                        // one a failed change leaves behind
                        let busy, _ =
                            transition (OrderPlanMsg.Change(OrderPlanChange.FilterRows [| "c-1" |], "r-1")) shown

                        transition (OrderPlanMsg.Answered("r-1", Error [| "no dose rules" |])) busy
                        |> Expect.equal
                            "the plan in the cart, rows and totals in step"
                            (settled one None, [ OrderPlanEffect.TellError [| "no dose rules" |] ])

                        // a page's change, the dialog open: back to the plan in the cart, the dialog stays open
                        let recalculating, _ =
                            transition
                                (OrderPlanMsg.Change(OrderPlanChange.Remove [| "c-9" |], "r-1"))
                                (settled one (Some "c-1"))

                        transition (OrderPlanMsg.Answered("r-1", Error [| "no dose rules" |])) recalculating
                        |> Expect.equal
                            "the plan in the cart, the selection stays, the removal counted as it went out"
                            (settled one (Some "c-1") |> OrderPlanState.withWork PlanWork.Changed,
                             [ OrderPlanEffect.TellError [| "no dose rules" |] ])

                        let opening = loading patient [||] "r-1"

                        transition (OrderPlanMsg.Answered("r-1", Error [| "not loaded" |])) opening
                        |> Expect.equal
                            "a refused open: the empty plan"
                            (settled (plan [||]) None, [ OrderPlanEffect.TellError [| "not loaded" |] ])
                    }

                    test "an order prescribed: the answer is shown and checked, and the order told added" {
                        let workbench = context "" "ibuprofen"

                        let busy = recalculating one None "r-1" (OrderPlanCommand.AddOrderContext(one, workbench, [||]))

                        transition (OrderPlanMsg.Answered("r-1", Ok two)) busy
                        |> Expect.equal
                            "shown, checked and added"
                            (settled two None |> OrderPlanState.withOpened one.OrderContexts,
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol"; "ibuprofen" ]
                                 OrderPlanEffect.TellAnswered
                                 OrderPlanEffect.OrderAdded None
                             ])
                    }

                    test "an order prescribed that fails: the error told, no order added" {
                        let workbench = context "" "ibuprofen"

                        let busy = recalculating one None "r-1" (OrderPlanCommand.AddOrderContext(one, workbench, [||]))

                        transition (OrderPlanMsg.Answered("r-1", Error [| "down" |])) busy
                        |> Expect.equal
                            "the plan as before"
                            (settled one None, [ OrderPlanEffect.TellError [| "down" |] ])
                    }

                    test "an answer to an earlier prescription: no order added" {
                        let workbench = context "" "ibuprofen"

                        let busy = recalculating one None "r-2" (OrderPlanCommand.AddOrderContext(one, workbench, [||]))

                        transition (OrderPlanMsg.Answered("r-1", Ok two)) busy
                        |> snd
                        |> Expect.isEmpty "the answer is dropped"
                    }

                    for name, sent in
                        [
                            "a removal", OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |])
                            "a row filter", OrderPlanCommand.FilterRows([| "c-1" |], one)
                            "a patient update", OrderPlanCommand.UpdatePatient(patient, one)
                            "a command from the order dialog",
                            OrderPlanCommand.EvaluateContext(
                                one,
                                "c-1",
                                OrderViewCommand.ResetOrderScenario,
                                context "c-1" "paracetamol"
                            )
                        ] do
                        test $"%s{name} that lands: no order added" {
                            recalculating one None "r-1" sent
                            |> transition (OrderPlanMsg.Answered("r-1", Ok one))
                            |> snd
                            |> List.exists (
                                function
                                | OrderPlanEffect.OrderAdded _ -> true
                                | _ -> false
                            )
                            |> Expect.isFalse "no add"
                        }

                    test "an open that lands: no order added" {
                        loading patient one.OrderContexts "r-1"
                        |> transition (OrderPlanMsg.Answered("r-1", Ok one))
                        |> snd
                        |> List.exists (
                            function
                            | OrderPlanEffect.OrderAdded _ -> true
                            | _ -> false
                        )
                        |> Expect.isFalse "no add"
                    }
                ]

            testList
                "the selection and the filter"
                [
                    test "the selection is the client's own, stored next to a request in flight" {
                        transition (OrderPlanMsg.SelectContext(Some "c-1")) shown
                        |> Expect.equal "selected" (settled one (Some "c-1"), [])

                        let busy = recalculating one None "r-1" (OrderPlanCommand.FilterRows(one.Filtered, one))

                        transition (OrderPlanMsg.SelectContext(Some "c-1")) busy
                        |> Expect.equal
                            "selected while busy"
                            (recalculating one (Some "c-1") "r-1" (OrderPlanCommand.FilterRows(one.Filtered, one)), [])

                        transition (OrderPlanMsg.SelectContext None) noPatientData
                        |> Expect.equal "nothing to select" (noPatientData, [])
                    }

                    test "the filter recalculates the totals over the rows chosen, the dialog closed" {
                        let selected = settled one (Some "c-1")
                        let filtered = { one with Filtered = [| "c-1" |] }
                        let state, effects =
                            transition (OrderPlanMsg.Change(OrderPlanChange.FilterRows [| "c-1" |], "r-1")) selected

                        state
                        |> Expect.equal
                            "recalculating over the plan in the cart, the rows in the command, the dialog closed"
                            (recalculating one None "r-1" (OrderPlanCommand.FilterRows([| "c-1" |], one)))

                        effects
                        |> Expect.equal
                            "the row filter"
                            [
                                OrderPlanEffect.CallPlan(OrderPlanCommand.FilterRows([| "c-1" |], one), "r-1")
                            ]
                    }
                ]
        ]


[<Tests>]
let viewTests =
    testList
        "OrderPlanState.view"
        [
            test "no patient; an open under way; the plan in the cart with its selection" {
                noPatientData
                |> OrderPlanState.view
                |> Expect.equal "no patient" OrderPlanView.NoPatientData

                loading patient [||] "r-1"
                |> OrderPlanState.view
                |> Expect.equal "the empty plan shown while the open runs" (OrderPlanView.Changing(plan [||], None))

                settled one (Some "c-1")
                |> OrderPlanState.view
                |> Expect.equal "the plan in the cart, the selection with it" (OrderPlanView.Settled(one, Some "c-1"))
            }

            test "a change under way: the plan the page changed for a recalculation, the plan in the cart otherwise" {
                let filtered = { one with Filtered = [| "c-1" |] }

                recalculating one (Some "c-1") "r-1" (OrderPlanCommand.FilterRows([| "c-1" |], one))
                |> OrderPlanState.view
                |> Expect.equal "the rows chosen show at once" (OrderPlanView.Changing(filtered, Some "c-1"))

                recalculating one None "r-1" (OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |]))
                |> OrderPlanState.view
                |> Expect.equal "the plan in the cart" (OrderPlanView.Changing(one, None))
            }

            test "hasOrder: an order in the plan, by its id, settled or changing; nothing without a plan" {
                settled two None
                |> OrderPlanState.view
                |> OrderPlanView.hasOrder "o-c-2"
                |> Expect.isTrue "in the plan"

                recalculating two None "r-1" (OrderPlanCommand.RemoveOrderContexts(two, [| "c-1" |]))
                |> OrderPlanState.view
                |> OrderPlanView.hasOrder "o-c-1"
                |> Expect.isTrue "still shown while the change is under way"

                settled two None
                |> OrderPlanState.view
                |> OrderPlanView.hasOrder "c-1"
                |> Expect.isFalse "a context id is not an order id"

                noPatientData
                |> OrderPlanState.view
                |> OrderPlanView.hasOrder "o-c-1"
                |> Expect.isFalse "no plan"
            }
        ]


[<Tests>]
let planTests =
    testList
        "what the plan holds"
        [
            test "a failed change keeps the plan in the cart, a landed one is checked" {
                let filtered = { one with Filtered = [| "c-1" |] }
                let rows = OrderPlanCommand.FilterRows([| "c-1" |], one)

                transition (OrderPlanMsg.Answered("r-1", Error [| "not loaded" |])) (recalculating one None "r-1" rows)
                |> Expect.equal
                    "the original, told"
                    (settled one None, [ OrderPlanEffect.TellError [| "not loaded" |] ])

                transition (OrderPlanMsg.Answered("r-1", Ok filtered)) (recalculating one None "r-1" rows)
                |> Expect.equal
                    "the plan answered, its drugs checked"
                    (settled filtered None |> OrderPlanState.withOpened one.OrderContexts,
                     [
                         OrderPlanEffect.CheckInteractions [ "paracetamol" ]
                         OrderPlanEffect.TellAnswered
                     ])
            }

            test "nothing lands without a patient; the dialog selects only over a plan in the cart" {
                transition (OrderPlanMsg.Answered("r-1", Ok one)) noPatientData
                |> Expect.equal "no patient" (noPatientData, [])

                transition (OrderPlanMsg.SelectContext(Some "c-1")) (loading patient [||] "r-1")
                |> Expect.equal "nothing to select yet" (loading patient [||] "r-1", [])
            }
        ]


[<Tests>]
let workTests =
    let remove = OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |])
    let filterRows = OrderPlanCommand.FilterRows(one.Filtered, one)

    let version: SignedOrderPlan =
        {
            Head =
                {
                    Id = "plan-1"
                    No = 1
                    By =
                        {
                            UserId = "u"
                            DisplayName = "U"
                            Role = UserRole.Prescriber
                        }
                    SignedAt = DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
                }
            PatientId = "p"
            Base = None
            OrderContexts = one.OrderContexts
            Patient = draft
            Identity = None
            Verified = true
        }

    let workOf (state: OrderPlanState, _: OrderPlanEffect list) = state |> OrderPlanState.work

    testList
        "the plan's work"
        [
            test "a plan in the cart is as signed; a command that changes it is work, a recalculation is not" {
                settled one None
                |> OrderPlanState.work
                |> Expect.equal "as signed" PlanWork.AsSigned

                transition (pageMsg (remove, "r-1")) (settled one None)
                |> workOf
                |> Expect.equal "one change" PlanWork.Changed

                transition (pageMsg (filterRows, "r-1")) (settled one None |> OrderPlanState.withWork PlanWork.Changed)
                |> workOf
                |> Expect.equal "still one change" PlanWork.Changed
            }

            test "a step into an order counts when it goes out" {
                let step =
                    OrderPlanCommand.EvaluateContext(
                        one,
                        "c-1",
                        OrderViewCommand.IncreaseScheduleFrequencyProperty,
                        one.OrderContexts[0]
                    )

                transition (pageMsg (step, "r-3")) (settled one None)
                |> workOf
                |> Expect.equal "the one that goes out" PlanWork.Changed
            }

            test "a version opened and a patient cleared are as signed" {
                transition
                    (OrderPlanMsg.OpenSignedPlan(version, "r-1"))
                    (settled one None |> OrderPlanState.withWork PlanWork.Changed)
                |> workOf
                |> Expect.equal "a version opened" PlanWork.AsSigned

                transition
                    (OrderPlanMsg.PatientDataChanged(None, "r-1"))
                    (settled one None |> OrderPlanState.withWork PlanWork.Changed)
                |> workOf
                |> Expect.equal "no patient, nothing to sign" PlanWork.AsSigned
            }

            test "a signature is as signed" {
                transition OrderPlanMsg.Signed (settled one None |> OrderPlanState.withWork PlanWork.Changed)
                |> Expect.equal "as signed, nothing else" (settled one None, [])
            }
        ]


/// The ids of the orders new or changed since the version last opened or signed.
let changedIds (state: OrderPlanState) =
    state
    |> OrderPlanState.plan
    |> Option.map (fun plan -> OrderPlanState.differences plan state)
    |> Option.defaultValue [||]
    |> Array.filter (fun (_, difference) -> difference <> PlanDifferencePolicy.Difference.Removed)
    |> Array.map (fun (ctx, _) -> ctx.Id)


/// A version opens as signed, on the patient it was signed on, and then follows the current patient.
[<Tests>]
let versionFollowTests =
    let version = { head with Patient = otherDraft }

    let openedOver current =
        settled one None
        |> transition (OrderPlanMsg.PatientDataChanged(Some current, "r-0"))
        |> fst
        |> transition (OrderPlanMsg.Answered("r-0", Ok one))
        |> fst
        |> transition (OrderPlanMsg.OpenSignedPlan(version, "r-9"))
        |> fst
        |> transition (OrderPlanMsg.PatientDataChanged(Some current, "r-1"))

    let landed tp (state, _) = state |> transition (OrderPlanMsg.Answered("r-1", Ok tp))

    testList
        "a version opened on other data"
        [
            test "it opens on the patient it was signed on" {
                openedOver patient
                |> snd
                |> Expect.equal
                    "the version"
                    [
                        OrderPlanEffect.CallPlan(OrderPlanCommand.Open(otherDraft, version.OrderContexts), "r-1")
                    ]
            }

            test "it then follows the current patient, the version the base of the differences" {
                let state, effects = openedOver patient |> landed two

                effects
                |> List.contains (OrderPlanEffect.CallPlan(OrderPlanCommand.UpdatePatient(patient, two), "r-1"))
                |> Expect.isTrue "the follow"

                OrderPlanState.differences two state
                |> Expect.isEmpty "as opened until the follow lands"
            }

            test "a version on the current patient has nothing to follow" {
                openedOver otherDraft
                |> landed two
                |> snd
                |> List.exists (
                    function
                    | OrderPlanEffect.CallPlan _ -> true
                    | _ -> false
                )
                |> Expect.isFalse "opened as signed"
            }

            test "a follow that changes no order says nothing, and nothing is to sign" {
                let state, effects =
                    openedOver patient
                    |> landed two
                    |> fst
                    |> transition (OrderPlanMsg.Answered("r-1", Ok two))

                effects
                |> List.exists (
                    function
                    | OrderPlanEffect.TellPatientDataChanged _ -> true
                    | _ -> false
                )
                |> Expect.isFalse "quiet"

                OrderPlanState.work state |> Expect.equal "as signed" PlanWork.AsSigned
            }

            test "a follow that changes an order gives no summary, and the order is unsigned work" {
                let moved =
                    { two with
                        OrderContexts =
                            two.OrderContexts
                            |> Array.map (fun c -> { c with Argumentation = Some "moved" })
                    }

                let state, effects =
                    openedOver patient
                    |> landed two
                    |> fst
                    |> transition (OrderPlanMsg.Answered("r-1", Ok moved))

                effects
                |> List.exists (
                    function
                    | OrderPlanEffect.TellPatientDataChanged _ -> true
                    | _ -> false
                )
                |> Expect.isFalse "the version opened stays on screen"

                OrderPlanState.work state |> Expect.equal "changed" PlanWork.Changed
            }

            test "a failed follow stays on the version, and the same data can be given again" {
                let failed, effects =
                    openedOver patient
                    |> landed two
                    |> fst
                    |> transition (OrderPlanMsg.Answered("r-1", Error [| "no" |]))

                effects
                |> List.contains OrderPlanEffect.PatientDataNotFollowed
                |> Expect.isTrue "told"

                failed
                |> transition (OrderPlanMsg.PatientDataChanged(Some patient, "r-2"))
                |> snd
                |> Expect.equal
                    "sent again"
                    [
                        OrderPlanEffect.CallPlan(OrderPlanCommand.UpdatePatient(patient, two), "r-2")
                    ]
            }

            test "the version waits for the patient, one request at a time" {
                settled one None
                |> transition (OrderPlanMsg.OpenSignedPlan(version, "r-1"))
                |> Expect.equal
                    "nothing goes out"
                    (OrderPlanState.withSignedVersion otherDraft version.OrderContexts, [])
            }
        ]


/// Orders changed by new patient data are a change in the plan, shown against the version.
[<Tests>]
let workAfterUpdateTests =
    let update tp state =
        state
        |> transition (OrderPlanMsg.PatientDataChanged(Some other, "r-1"))
        |> fst
        |> transition (OrderPlanMsg.Answered("r-1", Ok tp))
        |> fst
        |> OrderPlanState.work

    // the given amounts stay; the dose per kg of the orderable moved
    let followed =
        { one with
            OrderContexts =
                one.OrderContexts
                |> Array.map (fun c ->
                    { c with
                        Scenarios =
                            c.Scenarios
                            |> Array.map (fun sc ->
                                { sc with
                                    OrderScenario.Order.Orderable.Dose.QuantityAdjust.Variable.Vals =
                                        valueIn "mg/kg" 7.5m
                                }
                            )
                    }
                )
        }

    testList
        "the work after a patient update"
        [
            test "orders the new data changed are unsigned work" {
                settled one None |> update followed |> Expect.equal "changed" PlanWork.Changed
            }

            test "an order without a dose per kg, without a notification, stays as signed" {
                settled one None |> update one |> Expect.equal "as signed" PlanWork.AsSigned
            }

            test "a plan equal to the version has nothing to sign, also after a change" {
                settled one None
                |> OrderPlanState.withWork PlanWork.Changed
                |> update one
                |> Expect.equal "as signed" PlanWork.AsSigned
            }
        ]


[<Tests>]
let unsignedChangesTests =
    let changedOf (state: OrderPlanState, _: OrderPlanEffect list) = changedIds state
    let hasChanges result = changedOf result |> Array.isEmpty |> not

    /// The answer to a command sent over the plan in the cart, landed.
    let landed cmd answer state =
        transition (pageMsg (cmd, "r-1")) state
        |> fst
        |> transition (OrderPlanMsg.Answered("r-1", answer))

    testList
        "unsigned changes"
        [
            test "a version opened lands released" {
                transition (OrderPlanMsg.Answered("r-1", Ok two)) (loading patient head.OrderContexts "r-1")
                |> hasChanges
                |> Expect.isFalse "the version opened holds nothing"
            }

            test "an order added holds once it lands" {
                settled one None
                |> landed (OrderPlanCommand.AddOrderContext(one, context "c-2" "ibuprofen", [||])) (Ok two)
                |> changedOf
                |> Expect.equal "the order added is new" [| "c-2" |]
            }

            test "the differences list the order added as new" {
                settled one None
                |> landed (OrderPlanCommand.AddOrderContext(one, context "c-2" "ibuprofen", [||])) (Ok two)
                |> fun (state, _) -> state |> OrderPlanState.differences two
                |> Array.map (fun (c, d) -> c.Id, d)
                |> Expect.equal "the order added is new" [| "c-2", PlanDifferencePolicy.Difference.New |]
            }

            test "the differences list an order of the version removed" {
                settled two None
                |> landed (OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])) (Ok one)
                |> fun (state, _) -> state |> OrderPlanState.differences one
                |> Array.map (fun (c, d) -> c.Id, d)
                |> Expect.equal "the order removed is listed" [| "c-2", PlanDifferencePolicy.Difference.Removed |]
            }

            test "an order of the version removed does not hold" {
                settled two None
                |> landed (OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])) (Ok one)
                |> hasChanges
                |> Expect.isFalse "a removal is no new or changed order"
            }

            test "a failed change leaves the hold as it was" {
                settled two None
                |> OrderPlanState.withOpened one.OrderContexts
                |> landed (OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])) (Error [| "down" |])
                |> changedOf
                |> Expect.equal "the order added stays new" [| "c-2" |]
            }

            test "the rows chosen do not hold" {
                let filtered = { one with Filtered = [| "c-1" |] }

                transition (OrderPlanMsg.Change(OrderPlanChange.FilterRows [| "c-1" |], "r-1")) (settled one None)
                |> fst
                |> transition (OrderPlanMsg.Answered("r-1", Ok filtered))
                |> hasChanges
                |> Expect.isFalse "the filter is no order"
            }

            test "a signature releases" {
                transition
                    OrderPlanMsg.Signed
                    (settled two None
                     |> OrderPlanState.withOpened one.OrderContexts
                     |> OrderPlanState.withWork PlanWork.Changed)
                |> hasChanges
                |> Expect.isFalse "the plan is the version signed"
            }

            test "a version opened and a patient cleared release" {
                let withChanges = settled two None |> OrderPlanState.withOpened [||]

                transition (OrderPlanMsg.OpenSignedPlan(head, "r-1")) withChanges
                |> hasChanges
                |> Expect.isFalse "the version is being opened"

                transition (OrderPlanMsg.PatientDataChanged(None, "r-1")) withChanges
                |> hasChanges
                |> Expect.isFalse "no patient, nothing unsigned"
            }
        ]


[<Tests>]
let signingTests =
    let add = OrderPlanCommand.AddOrderContext(one, context "" "ibuprofen", [||])
    let transition = OrderPlanState.transition

    testList
        "the order plan signed"
        [
            test "an order added before the sign is released by the signature" {
                let added =
                    settled one None
                    |> transition (pageMsg (add, "r-1"))
                    |> fst
                    |> transition (OrderPlanMsg.Answered("r-1", Ok two))
                    |> fst

                added |> changedIds |> Expect.isNonEmpty "the order added holds"

                added
                |> transition OrderPlanMsg.Signed
                |> fst
                |> changedIds
                |> Expect.isEmpty "the order plan is the version signed"
            }
        ]


/// The argumentation on a context of the plan: a command into that context, written by the
/// server, and a change of the plan.
[<Tests>]
let argueTests =
    let text = "Sepsis, hogere dosis in overleg met de apotheek"
    let argue = OrderViewCommand.SetArgumentationProperty text
    let transition = OrderPlanState.transition

    testList
        "the argumentation in the plan"
        [
            test "goes as a command into the context named, and the plan's work is changed" {
                let state, effects =
                    settled two None
                    |> OrderPlanState.withOpened two.OrderContexts
                    |> transition (OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("c-2", argue), "r-1"))

                effects
                |> Expect.equal
                    "the text into c-2, over the plan in the cart"
                    [
                        OrderPlanEffect.CallPlan(
                            OrderPlanCommand.EvaluateContext(two, "c-2", argue, two.OrderContexts[1]),
                            "r-1"
                        )
                    ]

                state |> OrderPlanState.work |> Expect.equal "changed" PlanWork.Changed
            }

            test "the answer lands as the server answered it, the text included" {
                let argued =
                    { two with
                        OrderContexts =
                            two.OrderContexts
                            |> Array.map (fun c ->
                                if c.Id = "c-2" then
                                    { c with Argumentation = Some text }
                                else
                                    c
                            )
                    }

                settled two None
                |> transition (OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("c-2", argue), "r-1"))
                |> fst
                |> transition (OrderPlanMsg.Answered("r-1", Ok argued))
                |> fst
                |> OrderPlanState.plan
                |> Expect.equal "the plan answered" (Some argued)
            }
        ]


[<Tests>]
let awaitsTests =
    testList
        "OrderPlanState.awaits"
        [
            test "the request under way is awaited" {
                recalculating one None "r-1" (OrderPlanCommand.FilterRows(one.Filtered, one))
                |> OrderPlanState.awaits "r-1"
                |> Expect.isTrue "the answer to r-1 lands"
            }

            test "an answer to another request is not awaited" {
                recalculating one None "r-2" (OrderPlanCommand.FilterRows(one.Filtered, one))
                |> OrderPlanState.awaits "r-1"
                |> Expect.isFalse "the answer to r-1 is dropped"
            }

            test "nothing is awaited while no request is under way" {
                shown |> OrderPlanState.awaits "r-1" |> Expect.isFalse "no request under way"
            }
        ]


[<Tests>]
let evaluateContextTests =
    let step = OrderViewCommand.IncreaseScheduleFrequencyProperty
    let open' = settled two (Some "c-1")

    testList
        "a command from the order dialog"
        [
            test "goes over the plan in the cart and that context as answered" {
                let sent = OrderPlanCommand.EvaluateContext(two, "c-1", step, two.OrderContexts[0])

                open'
                |> OrderPlanState.transition (
                    OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("c-1", step), "r-1")
                )
                |> snd
                |> Expect.equal "the step over the context in the plan" [ OrderPlanEffect.CallPlan(sent, "r-1") ]
            }

            test "goes nowhere for a context the plan does not hold" {
                open'
                |> OrderPlanState.transition (
                    OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("gone", step), "r-1")
                )
                |> Expect.equal "nothing sent" (open', [])
            }

            test "goes nowhere for a locked context, which can still be removed" {
                let mark (c: OrderContext) =
                    { c with Scenarios = c.Scenarios |> Array.map (fun sc -> { sc with NotRecalculated = true }) }

                let locked = settled { two with OrderContexts = two.OrderContexts |> Array.map mark } None

                locked
                |> OrderPlanState.transition (
                    OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("c-1", step), "r-1")
                )
                |> Expect.equal "nothing sent" (locked, [])

                locked
                |> OrderPlanState.transition (OrderPlanMsg.Change(OrderPlanChange.Remove [| "c-1" |], "r-1"))
                |> snd
                |> Expect.hasLength "the removal goes out" 1
            }
        ]


[<Tests>]
let reCalcTests =
    // the context as the server answers a clear
    let recalculated = context "c-1" "paracetamol-recalculated"
    let answer = plan [| recalculated; context "c-2" "ibuprofen" |]
    let open' = settled two (Some "c-1")
    let reCalc request =
        OrderPlanMsg.Change(
            OrderPlanChange.OrderDialogCommand("c-1", OrderViewCommand.ReCalcScheduleProperty ScheduleProperty.Time),
            request
        )
    // the clear as the machine sends it, over the plan and the context in the plan
    let clear =
        OrderPlanCommand.EvaluateContext(
            two,
            "c-1",
            OrderViewCommand.ReCalcScheduleProperty ScheduleProperty.Time,
            two.OrderContexts[0]
        )
    let move = OrderPlanState.transition
    let run msgs state = msgs |> List.fold (fun s m -> move m s |> fst) state

    testList
        "OrderPlanState.transition, a recalculation and a restore"
        [
            test "a recalculation goes out over the context in the plan and counts as a change" {
                let state, effects = open' |> move (reCalc "r-1")

                effects
                |> Expect.equal "the recalculation goes out" [ OrderPlanEffect.CallPlan(clear, "r-1") ]

                state
                |> OrderPlanState.work
                |> Expect.equal "changed while the list is open" PlanWork.Changed
            }

            test "a recalculation from the order dialog keeps the state before it" {
                let cmd = OrderViewCommand.ReCalcItemProperty("cmp", "itm", ItemProperty.DoseQuantity)

                let change = OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("c-1", cmd), "r-1")
                let viaChange = open' |> move change

                viaChange
                |> fst
                |> OrderPlanState.isRecalculating
                |> Expect.isTrue "recalculating"

                open'
                |> run [ change; OrderPlanMsg.Answered("r-1", Ok answer) ]
                |> move OrderPlanMsg.RestoreField
                |> Expect.equal "a restore puts the state before it back" (open', [])
            }

            test "a pick from the order dialog is no recalculation: the recalculation list stays closed" {
                let pick = OrderViewCommand.SetNthItemProperty("cmp", "itm", ItemProperty.DoseQuantity, 0)

                open'
                |> move (OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("c-1", pick), "r-1"))
                |> fst
                |> OrderPlanState.isRecalculating
                |> Expect.isFalse "not recalculating"
            }

            test "a restore before the answer puts the plan back as signed, and the late answer is dropped" {
                let restored = open' |> run [ reCalc "r-1"; OrderPlanMsg.RestoreField ]

                restored |> Expect.equal "the state before the click" open'

                restored
                |> move (OrderPlanMsg.Answered("r-1", Ok answer))
                |> Expect.equal "the answer finds no request" (open', [])
            }

            test "a restore after the answer puts the plan back as signed" {
                let answered = open' |> run [ reCalc "r-1"; OrderPlanMsg.Answered("r-1", Ok answer) ]

                answered
                |> OrderPlanState.plan
                |> Expect.equal "the list shows the answer" (Some answer)

                answered
                |> move OrderPlanMsg.RestoreField
                |> Expect.equal "the state before the click" (open', [])
            }

            test "a plan changed before the click stays changed" {
                let changed = open' |> OrderPlanState.withWork PlanWork.Changed

                changed
                |> run [ reCalc "r-1"; OrderPlanMsg.RestoreField ]
                |> Expect.equal "as before the click" changed
            }

            test "a pick ends the look: a restore after it changes nothing" {
                let pick =
                    OrderPlanCommand.EvaluateContext(
                        answer,
                        "c-1",
                        OrderViewCommand.SetNthScheduleProperty(ScheduleProperty.Frequency, 0),
                        recalculated
                    )

                let picked =
                    open'
                    |> run [ reCalc "r-1"; OrderPlanMsg.Answered("r-1", Ok answer); pageMsg (pick, "r-2") ]

                picked
                |> move OrderPlanMsg.RestoreField
                |> Expect.equal "nothing to put back" (picked, [])
            }

            test "a restore without a recalculation changes nothing" {
                open'
                |> move OrderPlanMsg.RestoreField
                |> Expect.equal "nothing to put back" (open', [])
            }

            test "a recalculation for a context the plan no longer holds sends nothing and keeps nothing" {
                let gone =
                    OrderPlanMsg.Change(
                        OrderPlanChange.OrderDialogCommand(
                            "c-9",
                            OrderViewCommand.ReCalcScheduleProperty ScheduleProperty.Time
                        ),
                        "r-1"
                    )

                let state, effects = open' |> move gone

                (state, effects) |> Expect.equal "nothing sent" (open', [])
                state |> OrderPlanState.isRecalculating |> Expect.isFalse "not recalculating"
            }
        ]


/// What a page wants of the plan, built into the wire command over the plan the machine holds.
[<Tests>]
let changeTests =
    let step = OrderViewCommand.IncreaseScheduleFrequencyProperty

    testList
        "a page's change"
        [
            test "is built over the plan given, whatever plan the page was shown" {
                let ctx = context "" "ibuprofen"

                [
                    OrderPlanChange.Add(ctx, [||]), OrderPlanCommand.AddOrderContext(two, ctx, [||])
                    OrderPlanChange.Remove [| "c-2" |], OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])
                    OrderPlanChange.FilterRows [| "c-1" |], OrderPlanCommand.FilterRows([| "c-1" |], two)
                    OrderPlanChange.OrderDialogCommand("c-1", step),
                    OrderPlanCommand.EvaluateContext(two, "c-1", step, two.OrderContexts[0])
                ]
                |> List.iter (fun (change, expected) ->
                    change
                    |> OrderPlanChange.command two
                    |> Expect.equal $"%A{change}" (Some expected)
                )
            }

            test "into a context the plan no longer holds sends nothing" {
                OrderPlanChange.OrderDialogCommand("c-9", step)
                |> OrderPlanChange.command two
                |> Expect.isNone "no context"

                transition (OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("c-9", step), "r-1")) shown
                |> Expect.equal "nothing sent" (shown, [])
            }

            test "a prescription and a removal go out over the plan in the cart" {
                let ctx = context "" "ibuprofen"

                [
                    OrderPlanMsg.Change(OrderPlanChange.Add(ctx, [||]), "r-1"),
                    OrderPlanCommand.AddOrderContext(one, ctx, [||])
                    OrderPlanMsg.Change(OrderPlanChange.Remove [| "c-1" |], "r-1"),
                    OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |])
                ]
                |> List.iter (fun (msg, expected) ->
                    transition msg shown
                    |> snd
                    |> Expect.equal $"%A{msg}" [ OrderPlanEffect.CallPlan(expected, "r-1") ]
                )
            }
        ]


/// With a request out nothing but its answer and the patient cleared reaches the plan: its pages
/// are disabled, and whatever comes anyway falls to the closing arm.
[<Tests>]
let requestOutTests =
    let busy = recalculating one None "r-1" (OrderPlanCommand.FilterRows(one.Filtered, one))
    let remove = OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |])

    testList
        "the plan with a request out"
        [
            test "a change, a patient set and a version opened leave the state and send nothing" {
                for msg in
                    [
                        pageMsg (remove, "r-2")
                        OrderPlanMsg.PatientDataChanged(Some other, "r-2")
                        OrderPlanMsg.OpenSignedPlan(head, "r-2")
                    ] do
                    transition msg busy |> Expect.equal $"%A{msg}" (busy, [])
            }

            test "the patient cleared still resets it" {
                transition (OrderPlanMsg.PatientDataChanged(None, "r-2")) busy
                |> Expect.equal "no patient" (noPatientData, [])
            }
        ]
