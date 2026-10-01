module Informedica.GenPRES.Client.Core.Tests.OrderPlanMachineTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Api
open PlanWorkPolicy
open OrderPlanMachine


module Fixtures =

    let ten = { Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }

    /// The data a plan carries, and the patient it is: an age makes the draft a patient.
    let draft = { Shared.Models.Patient.empty with Age = Some ten }

    let asPatient (dto: Patient) =
        match dto |> Shared.Models.Patient.validate with
        | Ok pat -> pat
        | Error err -> invalidOp $"the fixture is no patient: %A{err}"

    let patient = draft |> asPatient
    let otherDraft = { draft with Department = Some "other" }
    let other = otherDraft |> asPatient

    /// An OrderScenario with its order id and its name set, every other field a default (built by
    /// reflection: the order graph is too deep to write by hand).
    let scenario (id: string) (name: string) : OrderScenario =
        let rec defaultOf (t: Type) : obj =
            if t = typeof<string> then
                box ""
            elif t = typeof<bool> then
                box false
            elif t = typeof<int> then
                box 0
            elif t = typeof<decimal> then
                box 0m
            elif t = typeof<float> then
                box 0.0
            elif t = typeof<DateTime> then
                box DateTime.MinValue
            elif t.IsArray then
                box (Array.CreateInstance(t.GetElementType(), 0))
            elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then
                null
            elif Reflection.FSharpType.IsRecord t then
                Reflection.FSharpValue.MakeRecord(
                    t,
                    Reflection.FSharpType.GetRecordFields t
                    |> Array.map (fun f -> defaultOf f.PropertyType)
                )
            elif Reflection.FSharpType.IsUnion t then
                let case = (Reflection.FSharpType.GetUnionCases t)[0]

                Reflection.FSharpValue.MakeUnion(
                    case,
                    case.GetFields() |> Array.map (fun f -> defaultOf f.PropertyType)
                )
            else
                null

        let sc = defaultOf typeof<OrderScenario> :?> OrderScenario

        { sc with
            Name = name
            Order = { sc.Order with Id = id }
        }


    let context id name =
        { Shared.Models.OrderContext.empty with
            Id = id
            Scenarios = [| scenario $"o-{id}" name |]
        }


    let plan contexts = Shared.Models.OrderPlan.create draft contexts

    let one = plan [| context "c-1" "paracetamol" |]
    let two = plan [| context "c-1" "paracetamol"; context "c-2" "ibuprofen" |]

    let noPatient = OrderPlanState.noPatient

    let held = OrderPlanState.held patient

    /// A change under way over the plan held for the patient, the one a failed change goes back to.
    let recalculatingFor pat (tp: OrderPlan) (selected: string option) request (sent: OrderPlanCommand) =
        OrderPlanState.changing pat tp selected sent request

    let recalculating = recalculatingFor patient

    let loading = OrderPlanState.opening

    let shown = held one None

    let head: SignedOrderPlan =
        {
            Head =
                {
                    Id = "plan-1"
                    No = 1
                    By =
                        {
                            UserId = "u"
                            DisplayName = "Stub Prescriber"
                            Role = UserRole.Prescriber
                        }
                    SignedAt = DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc)
                }
            PatientId = "p"
            Base = None
            OrderContexts = two.OrderContexts
            Patient = draft
            Identity = None
            Verified = true
        }

    let transition = OrderPlanState.transition


open Fixtures


[<Tests>]
let tests =
    testList
        "OrderPlanState.transition"
        [
            testList
                "the patient"
                [
                    test "a patient set opens the empty plan under the request; the answer shows it" {
                        let asked, effects = transition (OrderPlanMsg.PatientChanged(Some patient, "r-1")) noPatient

                        asked |> Expect.equal "asked" (loading patient [||] "r-1")

                        effects
                        |> Expect.equal "open" [ OrderPlanEffect.CallPlan(OrderPlanCommand.Open(draft, [||]), "r-1") ]

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) asked
                        |> Expect.equal
                            "shown, its one drug checked"
                            (held one None, [ OrderPlanEffect.CheckInteractions [ "paracetamol" ] ])
                    }

                    test
                        "a patient changed recalculates the plan over the new patient, the dialog closed, whatever was in flight superseded" {
                        let busy = recalculating one (Some "c-1") "r-1" (OrderPlanCommand.Recalculate one)

                        let state, effects = transition (OrderPlanMsg.PatientChanged(Some other, "r-2")) busy

                        let expected = { one with Patient = otherDraft }

                        state
                        |> Expect.equal
                            "recalculating over the new patient"
                            (recalculatingFor other expected None "r-2" (OrderPlanCommand.Recalculate expected))

                        effects
                        |> Expect.equal
                            "recalculate"
                            [ OrderPlanEffect.CallPlan(OrderPlanCommand.Recalculate expected, "r-2") ]

                        transition (OrderPlanMsg.Answered("r-1", Ok two)) state
                        |> Expect.equal "the older answer dropped" (state, [])
                    }

                    test "no patient: no plan, whatever was in flight answers to nothing" {
                        let busy = recalculating one None "r-1" (OrderPlanCommand.Recalculate one)

                        let state, effects = transition (OrderPlanMsg.PatientChanged(None, "r-2")) busy
                        state |> Expect.equal "no patient" noPatient
                        effects |> Expect.isEmpty "nothing to do"

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) state
                        |> Expect.equal "the answer dropped" (noPatient, [])
                    }
                ]

            testList
                "the cart"
                [
                    test "the signed version opens over the patient held; the newest open wins" {
                        let first, _ = transition (OrderPlanMsg.Version(head, "r-1")) shown

                        first
                        |> Expect.equal "loading the version" (loading patient two.OrderContexts "r-1")

                        let second, effects = transition (OrderPlanMsg.Version(head, "r-2")) first

                        second
                        |> Expect.equal "the newer open in flight" (loading patient two.OrderContexts "r-2")

                        effects
                        |> Expect.equal
                            "open with the contexts"
                            [
                                OrderPlanEffect.CallPlan(OrderPlanCommand.Open(draft, two.OrderContexts), "r-2")
                            ]

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) second
                        |> Expect.equal "the older open's answer dropped" (second, [])

                        transition (OrderPlanMsg.Answered("r-2", Ok two)) second
                        |> Expect.equal
                            "the newer shown, two drugs checked"
                            (held two None, [ OrderPlanEffect.CheckInteractions [ "paracetamol"; "ibuprofen" ] ])
                    }

                    test "a patient changed while a version opens re-opens it for the new patient" {
                        let opening = loading patient two.OrderContexts "r-1"

                        transition (OrderPlanMsg.PatientChanged(Some other, "r-2")) opening
                        |> Expect.equal
                            "the version's contexts opened again, the older open's answer to nothing"
                            (loading other two.OrderContexts "r-2",
                             [
                                 OrderPlanEffect.CallPlan(OrderPlanCommand.Open(otherDraft, two.OrderContexts), "r-2")
                             ])
                    }

                    test "the version arriving before its patient is kept and opened when the patient does" {
                        // a Session opens: the plan machine hears the version first, the patient it
                        // was opened with one message later
                        let kept, effects = transition (OrderPlanMsg.Version(head, "r-1")) noPatient

                        (kept, effects)
                        |> Expect.equal "kept, nothing asked yet" (OrderPlanState.awaiting two.OrderContexts, [])

                        transition (OrderPlanMsg.PatientChanged(Some patient, "r-2")) kept
                        |> Expect.equal
                            "the version's contexts opened for the patient"
                            (loading patient two.OrderContexts "r-2",
                             [
                                 OrderPlanEffect.CallPlan(OrderPlanCommand.Open(draft, two.OrderContexts), "r-2")
                             ])

                        transition (OrderPlanMsg.PatientChanged(None, "r-3")) kept
                        |> Expect.equal "no patient after all: the version is let go" (noPatient, [])
                    }
                ]

            testList
                "a change"
                [
                    test "sent over the plan held, one at a time; a second while busy is dropped" {
                        let stale = { one with Filtered = [| "c-9" |] }
                        let cmd = OrderPlanCommand.RemoveOrderContexts(stale, [| "c-1" |])
                        let busy, effects = transition (OrderPlanMsg.Command(cmd, "r-1")) shown
                        let rebased = OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |])

                        busy
                        |> Expect.equal
                            "in flight over the plan held, one change of work"
                            (recalculating one None "r-1" rebased |> OrderPlanState.withWork PlanWork.Changed)

                        effects
                        |> Expect.equal "the rebased command" [ OrderPlanEffect.CallPlan(rebased, "r-1") ]

                        transition (OrderPlanMsg.Command(cmd, "r-2")) busy
                        |> Expect.equal "dropped while busy" (busy, [])
                    }

                    test "a recalculation carries the plan as the page changed it; the plan held stays" {
                        let filtered = { one with Filtered = [| "c-1" |] }

                        let busy, _ =
                            transition (OrderPlanMsg.Command(OrderPlanCommand.Recalculate filtered, "r-1")) shown

                        busy
                        |> Expect.equal
                            "the plan held, the page's plan in the command"
                            (recalculating one None "r-1" (OrderPlanCommand.Recalculate filtered))

                        OrderPlanState.meanwhile one (OrderPlanCommand.Recalculate filtered)
                        |> Expect.equal "the pages show the page's plan meanwhile" filtered

                        OrderPlanState.meanwhile one (OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |]))
                        |> Expect.equal "and the plan held for any other change" one
                    }

                    test "the answer lands on its request: shown, the selection kept while its context is still there" {
                        let cmd = OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])
                        let busy = recalculating two (Some "c-2") "r-1" cmd

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) busy
                        |> Expect.equal
                            "the selection's context went with the change"
                            (held one None |> OrderPlanState.withOpened two.OrderContexts,
                             [ OrderPlanEffect.CheckInteractions [ "paracetamol" ] ])

                        let kept = recalculating two (Some "c-1") "r-1" cmd

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) kept
                        |> Expect.equal
                            "the selection still holds"
                            (held one (Some "c-1") |> OrderPlanState.withOpened two.OrderContexts,
                             [ OrderPlanEffect.CheckInteractions [ "paracetamol" ] ])

                        transition (OrderPlanMsg.Answered("r-9", Ok one)) kept
                        |> Expect.equal "a stale answer dropped" (kept, [])
                    }

                    test "a failed filter change goes back to the original, and says why" {
                        // the plan sent differs from the one held, so that the test can tell which
                        // one a failed change leaves behind
                        let busy, _ = transition (OrderPlanMsg.Filter([| "c-1" |], "r-1")) shown

                        transition (OrderPlanMsg.Answered("r-1", Error [| "no dose rules" |])) busy
                        |> Expect.equal
                            "the plan held, rows and totals in step"
                            (held one None, [ OrderPlanEffect.TellError [| "no dose rules" |] ])

                        // a page's recalculation, the dialog open: back to the plan held, the dialog kept
                        let filtered = { one with Filtered = [| "c-1" |] }

                        let recalculating, _ =
                            transition
                                (OrderPlanMsg.Command(OrderPlanCommand.Recalculate filtered, "r-1"))
                                (held one (Some "c-1"))

                        transition (OrderPlanMsg.Answered("r-1", Error [| "no dose rules" |])) recalculating
                        |> Expect.equal
                            "the plan held, the selection kept"
                            (held one (Some "c-1"), [ OrderPlanEffect.TellError [| "no dose rules" |] ])

                        let opening = loading patient [||] "r-1"

                        transition (OrderPlanMsg.Answered("r-1", Error [| "not loaded" |])) opening
                        |> Expect.equal
                            "a refused open: the empty plan"
                            (held (plan [||]) None, [ OrderPlanEffect.TellError [| "not loaded" |] ])
                    }

                    test "an order prescribed: the plan page opens on it and the workbench is cleared" {
                        let workbench = context "" "ibuprofen"

                        let busy = recalculating one None "r-1" (OrderPlanCommand.AddOrderContext(one, workbench))

                        transition (OrderPlanMsg.Answered("r-1", Ok two)) busy
                        |> Expect.equal
                            "shown, checked, the page and the workbench"
                            (held two None |> OrderPlanState.withOpened one.OrderContexts,
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol"; "ibuprofen" ]
                                 OrderPlanEffect.GoToPlanPage
                                 OrderPlanEffect.ResetWorkbench
                             ])
                    }
                ]

            testList
                "the selection and the filter"
                [
                    test "the selection is the client's own, kept next to a request in flight" {
                        transition (OrderPlanMsg.Select(Some "c-1")) shown
                        |> Expect.equal "selected" (held one (Some "c-1"), [])

                        let busy = recalculating one None "r-1" (OrderPlanCommand.Recalculate one)

                        transition (OrderPlanMsg.Select(Some "c-1")) busy
                        |> Expect.equal
                            "selected while busy"
                            (recalculating one (Some "c-1") "r-1" (OrderPlanCommand.Recalculate one), [])

                        transition (OrderPlanMsg.Select None) noPatient
                        |> Expect.equal "nothing to select" (noPatient, [])
                    }

                    test
                        "the filter recalculates the totals over the rows chosen, the dialog closed; dropped while busy" {
                        let selected = held one (Some "c-1")
                        let filtered = { one with Filtered = [| "c-1" |] }
                        let state, effects = transition (OrderPlanMsg.Filter([| "c-1" |], "r-1")) selected

                        state
                        |> Expect.equal
                            "recalculating over the plan held, the rows in the command, the dialog closed"
                            (recalculating one None "r-1" (OrderPlanCommand.Recalculate filtered))

                        effects
                        |> Expect.equal
                            "recalculate"
                            [ OrderPlanEffect.CallPlan(OrderPlanCommand.Recalculate filtered, "r-1") ]

                        transition (OrderPlanMsg.Filter([||], "r-2")) state
                        |> Expect.equal "dropped while busy" (state, [])
                    }
                ]
        ]


[<Tests>]
let viewTests =
    testList
        "OrderPlanState.view"
        [
            test "no patient; an open under way; the plan held with its selection" {
                noPatient
                |> OrderPlanState.view
                |> Expect.equal "no patient" OrderPlanView.NoPatient

                loading patient [||] "r-1"
                |> OrderPlanState.view
                |> Expect.equal "the empty plan shown while the open runs" (OrderPlanView.Changing(plan [||], None))

                held one (Some "c-1")
                |> OrderPlanState.view
                |> Expect.equal "the plan held, the selection with it" (OrderPlanView.Settled(one, Some "c-1"))
            }

            test "a change under way: the plan the page changed for a recalculation, the plan held otherwise" {
                let filtered = { one with Filtered = [| "c-1" |] }

                recalculating one (Some "c-1") "r-1" (OrderPlanCommand.Recalculate filtered)
                |> OrderPlanState.view
                |> Expect.equal "the rows chosen show at once" (OrderPlanView.Changing(filtered, Some "c-1"))

                recalculating one None "r-1" (OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |]))
                |> OrderPlanState.view
                |> Expect.equal "the plan held" (OrderPlanView.Changing(one, None))
            }

            test "holds: an order in the plan, by its id, settled or changing; nothing without a plan" {
                held two None
                |> OrderPlanState.view
                |> OrderPlanView.holds "o-c-2"
                |> Expect.isTrue "in the plan"

                recalculating two None "r-1" (OrderPlanCommand.RemoveOrderContexts(two, [| "c-1" |]))
                |> OrderPlanState.view
                |> OrderPlanView.holds "o-c-1"
                |> Expect.isTrue "still shown while the change is under way"

                held two None
                |> OrderPlanState.view
                |> OrderPlanView.holds "c-1"
                |> Expect.isFalse "a context id is not an order id"

                noPatient
                |> OrderPlanState.view
                |> OrderPlanView.holds "o-c-1"
                |> Expect.isFalse "no plan"
            }
        ]


[<Tests>]
let pendingTests =
    let remove = OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])
    let busy = recalculating two (Some "c-1") "r-1" remove
    let c1 = two.OrderContexts[0]

    let navigate ctxCmd ctx = OrderPlanCommand.Navigate(two, "c-1", ctxCmd, ctx)

    let step = navigate OrderContextCommand.IncreaseScheduleFrequencyProperty c1
    let waiting = busy |> OrderPlanState.pending step "r-2"

    // the plan answered, its context changed by the server
    let now = context "c-1" "paracetamol-now"
    let answer = plan [| now |]

    testList
        "a step into the plan while a change is under way"
        [
            test "waits as the one pending; any other change is dropped" {
                transition (OrderPlanMsg.Command(step, "r-2")) busy
                |> Expect.equal "pending under its own id, nothing sent, no work yet" (waiting, [])

                transition (OrderPlanMsg.Command(remove, "r-2")) busy
                |> Expect.equal "dropped" (busy, [])
            }

            test "goes out over the plan answered: a step into the context as it now is, a value typed as sent" {
                let sent =
                    OrderPlanCommand.Navigate(answer, "c-1", OrderContextCommand.IncreaseScheduleFrequencyProperty, now)

                transition (OrderPlanMsg.Answered("r-1", Ok answer)) waiting
                |> Expect.equal
                    "the step, into the context answered"
                    (recalculating answer (Some "c-1") "r-2" sent
                     |> OrderPlanState.withWork PlanWork.Changed
                     |> OrderPlanState.withOpened two.OrderContexts,
                     [
                         OrderPlanEffect.CheckInteractions [ "paracetamol-now" ]
                         OrderPlanEffect.CallPlan(sent, "r-2")
                     ])

                let typed = navigate OrderContextCommand.UpdateOrderScenario c1
                let sent = OrderPlanCommand.Navigate(answer, "c-1", OrderContextCommand.UpdateOrderScenario, c1)

                busy
                |> OrderPlanState.pending typed "r-2"
                |> transition (OrderPlanMsg.Answered("r-1", Ok answer))
                |> Expect.equal
                    "the value typed, into the context it was typed into"
                    (recalculating answer (Some "c-1") "r-2" sent
                     |> OrderPlanState.withWork PlanWork.Changed
                     |> OrderPlanState.withOpened two.OrderContexts,
                     [
                         OrderPlanEffect.CheckInteractions [ "paracetamol-now" ]
                         OrderPlanEffect.CallPlan(sent, "r-2")
                     ])
            }

            test "gone with its context, with a failure and with a patient change" {
                let intoGone =
                    OrderPlanCommand.Navigate(
                        two,
                        "c-2",
                        OrderContextCommand.IncreaseScheduleFrequencyProperty,
                        two.OrderContexts[1]
                    )

                busy
                |> OrderPlanState.pending intoGone "r-2"
                |> transition (OrderPlanMsg.Answered("r-1", Ok one))
                |> Expect.equal
                    "its context gone with the change"
                    (held one (Some "c-1") |> OrderPlanState.withOpened two.OrderContexts,
                     [ OrderPlanEffect.CheckInteractions [ "paracetamol" ] ])

                transition (OrderPlanMsg.Answered("r-1", Error [| "refused" |])) waiting
                |> Expect.equal
                    "the failure told, nothing sent"
                    (held two (Some "c-1"), [ OrderPlanEffect.TellError [| "refused" |] ])

                let forOther = { two with Patient = otherDraft }

                transition (OrderPlanMsg.PatientChanged(Some other, "r-3")) waiting
                |> Expect.equal
                    "recalculated over the new patient, the pending gone"
                    (recalculatingFor other forOther None "r-3" (OrderPlanCommand.Recalculate forOther),
                     [ OrderPlanEffect.CallPlan(OrderPlanCommand.Recalculate forOther, "r-3") ])
            }
        ]


[<Tests>]
let stagesTests =
    testList
        "the two stages"
        [
            test "the plan alone knows no request: a failed change keeps the plan held, a landed one is checked" {
                let filtered = { one with Filtered = [| "c-1" |] }

                OrderPlanCart.step
                    (OrderPlanCartMsg.Landed(OrderPlanCommand.Recalculate filtered, Error [| "not loaded" |]))
                    (OrderPlanCart.Opened(patient, one))
                |> Expect.equal
                    "the original, told"
                    (OrderPlanCart.Opened(patient, one), [ OrderPlanCartIntent.Tell [| "not loaded" |] ])

                OrderPlanCart.step
                    (OrderPlanCartMsg.Landed(OrderPlanCommand.Recalculate filtered, Ok filtered))
                    (OrderPlanCart.Opened(patient, one))
                |> Expect.equal
                    "the plan answered, its drugs checked"
                    (OrderPlanCart.Opened(patient, filtered),
                     [ OrderPlanCartIntent.CheckInteractions [ "paracetamol" ] ])
            }

            test "nothing lands without a patient; the dialog selects only over a plan held" {
                OrderPlanCart.step
                    (OrderPlanCartMsg.Landed(OrderPlanCommand.Open(draft, [||]), Ok one))
                    (OrderPlanCart.NoPatient [||])
                |> Expect.equal "no patient" (OrderPlanCart.NoPatient [||], [])

                transition (OrderPlanMsg.Select(Some "c-1")) (loading patient [||] "r-1")
                |> Expect.equal "nothing to select yet" (loading patient [||] "r-1", [])
            }
        ]


[<Tests>]
let workTests =
    let remove = OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |])
    let recalculate = OrderPlanCommand.Recalculate one

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
            test "a plan held is as signed; a command that changes it is work, a recalculation is not" {
                held one None
                |> OrderPlanState.work
                |> Expect.equal "as signed" PlanWork.AsSigned

                transition (OrderPlanMsg.Command(remove, "r-1")) (held one None)
                |> workOf
                |> Expect.equal "one change" PlanWork.Changed

                transition
                    (OrderPlanMsg.Command(recalculate, "r-1"))
                    (held one None |> OrderPlanState.withWork PlanWork.Changed)
                |> workOf
                |> Expect.equal "still one change" PlanWork.Changed
            }

            test "a command counts when it goes out: not while it waits, never when it is dropped" {
                let busy = recalculating one None "r-1" (OrderPlanCommand.Recalculate one)

                transition (OrderPlanMsg.Command(remove, "r-2")) busy
                |> workOf
                |> Expect.equal "dropped while a request is under way: nothing changed" PlanWork.AsSigned

                let step =
                    OrderPlanCommand.Navigate(
                        one,
                        "c-1",
                        OrderContextCommand.IncreaseScheduleFrequencyProperty,
                        one.OrderContexts[0]
                    )

                let waiting, _ = transition (OrderPlanMsg.Command(step, "r-2")) busy
                waiting
                |> OrderPlanState.work
                |> Expect.equal "the dialog's step waits: not yet" PlanWork.AsSigned

                // the answer lands: the step goes out, and counts once
                transition (OrderPlanMsg.Answered("r-1", Ok one)) waiting
                |> workOf
                |> Expect.equal "gone out on the answer" PlanWork.Changed

                // the request fails: the step is dropped, and counts nothing
                transition (OrderPlanMsg.Answered("r-1", Error [| "down" |])) waiting
                |> workOf
                |> Expect.equal "dropped with the failure" PlanWork.AsSigned

                // a later step replaces it: only the one that goes out counts
                let later, _ = transition (OrderPlanMsg.Command(step, "r-3")) waiting

                transition (OrderPlanMsg.Answered("r-1", Ok one)) later
                |> workOf
                |> Expect.equal "the one that goes out" PlanWork.Changed
            }

            test "a version opened and a patient cleared are as signed" {
                transition
                    (OrderPlanMsg.Version(version, "r-1"))
                    (held one None |> OrderPlanState.withWork PlanWork.Changed)
                |> workOf
                |> Expect.equal "a version opened" PlanWork.AsSigned

                transition
                    (OrderPlanMsg.PatientChanged(None, "r-1"))
                    (held one None |> OrderPlanState.withWork PlanWork.Changed)
                |> workOf
                |> Expect.equal "no patient, nothing to sign" PlanWork.AsSigned
            }

            test "a signature is as signed" {
                transition OrderPlanMsg.Signed (held one None |> OrderPlanState.withWork PlanWork.Changed)
                |> Expect.equal "as signed, nothing else" (held one None, [])
            }
        ]


[<Tests>]
let heldTests =
    let heldOf (state: OrderPlanState, _: OrderPlanEffect list) = state |> OrderPlanState.contextHeld
    let changedOf (state: OrderPlanState, _: OrderPlanEffect list) = state |> OrderPlanState.changed

    /// The answer to a command sent over the plan held, landed.
    let landed cmd answer state =
        transition (OrderPlanMsg.Command(cmd, "r-1")) state
        |> fst
        |> transition (OrderPlanMsg.Answered("r-1", answer))

    testList
        "the patient context held"
        [
            test "a version opened lands released" {
                transition (OrderPlanMsg.Answered("r-1", Ok two)) (loading patient head.OrderContexts "r-1")
                |> heldOf
                |> Expect.isFalse "the version opened holds nothing"
            }

            test "an order added holds once it lands" {
                held one None
                |> landed (OrderPlanCommand.AddOrderContext(one, context "c-2" "ibuprofen")) (Ok two)
                |> changedOf
                |> Expect.equal "the order added is new" [| "c-2" |]
            }

            test "the differences list the order added as new" {
                held one None
                |> landed (OrderPlanCommand.AddOrderContext(one, context "c-2" "ibuprofen")) (Ok two)
                |> fun (state, _) -> state |> OrderPlanState.differences two
                |> Array.map (fun (c, d) -> c.Id, d)
                |> Expect.equal "the order added is new" [| "c-2", HeldContextPolicy.Difference.New |]
            }

            test "the differences list an order of the version removed" {
                held two None
                |> landed (OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])) (Ok one)
                |> fun (state, _) -> state |> OrderPlanState.differences one
                |> Array.map (fun (c, d) -> c.Id, d)
                |> Expect.equal "the order removed is listed" [| "c-2", HeldContextPolicy.Difference.Removed |]
            }

            test "an order of the version removed does not hold" {
                held two None
                |> landed (OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])) (Ok one)
                |> heldOf
                |> Expect.isFalse "a removal is no new or changed order"
            }

            test "a failed change leaves the hold as it was" {
                held two None
                |> OrderPlanState.withOpened one.OrderContexts
                |> landed (OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])) (Error [| "down" |])
                |> changedOf
                |> Expect.equal "the order added stays new" [| "c-2" |]
            }

            test "the rows chosen do not hold" {
                let filtered = { one with Filtered = [| "c-1" |] }

                transition (OrderPlanMsg.Filter([| "c-1" |], "r-1")) (held one None)
                |> fst
                |> transition (OrderPlanMsg.Answered("r-1", Ok filtered))
                |> heldOf
                |> Expect.isFalse "the filter is no order"
            }

            test "a signature releases" {
                transition
                    OrderPlanMsg.Signed
                    (held two None
                     |> OrderPlanState.withOpened one.OrderContexts
                     |> OrderPlanState.withWork PlanWork.Changed)
                |> heldOf
                |> Expect.isFalse "the plan is the version signed"
            }

            test "a version opened and a patient cleared release" {
                let holding = held two None |> OrderPlanState.withOpened [||]

                transition (OrderPlanMsg.Version(head, "r-1")) holding
                |> heldOf
                |> Expect.isFalse "the version is being opened"

                transition (OrderPlanMsg.PatientChanged(None, "r-1")) holding
                |> heldOf
                |> Expect.isFalse "no patient, nothing held"
            }
        ]


[<Tests>]
let signingTests =
    let notice: DataNotice =
        {
            Data = None
            Token = "t"
        }

    let underWay =
        [
            "requesting", SigningMachine.SigningView.Requesting
            "noticed", SigningMachine.SigningView.Noticed(one, notice)
            "challenged", SigningMachine.SigningView.Challenged(one, None)
            "submitting", SigningMachine.SigningView.Submitting one
        ]

    let add = OrderPlanCommand.AddOrderContext(one, context "" "ibuprofen")
    let transitionWhile = OrderPlanState.transitionWhile

    testList
        "the order plan while a signature is under way"
        [
            testList
                "a change from a page is dropped"
                [
                    for name, view in underWay do
                        test name {
                            transitionWhile view (OrderPlanMsg.Command(add, "r-1")) (held one None)
                            |> Expect.equal "the command is dropped" (held one None, [])

                            transitionWhile view (OrderPlanMsg.Filter([| "c-1" |], "r-1")) (held one None)
                            |> Expect.equal "the filter is dropped" (held one None, [])
                        }
                ]

            test "a change from a page goes out while idle" {
                transitionWhile SigningMachine.SigningView.Idle (OrderPlanMsg.Command(add, "r-1")) (held one None)
                |> snd
                |> Expect.equal "the command goes out" [ OrderPlanEffect.CallPlan(add, "r-1") ]
            }

            test "what is not a page's change reaches the order plan" {
                [
                    OrderPlanMsg.PatientChanged(Some patient, "r-1")
                    OrderPlanMsg.Version(head, "r-1")
                    OrderPlanMsg.Answered("r-1", Ok one)
                    OrderPlanMsg.Select(Some "c-1")
                    OrderPlanMsg.Signed
                ]
                |> List.forall (OrderPlanState.admitted SigningMachine.SigningView.Requesting)
                |> Expect.isTrue "admitted"
            }

            test "a recalculation that lands after the signature, from a data notice accepted, stays released" {
                // the notice's patient recalculates the order plan while the signature goes on;
                // a recalculation changes only the totals, so its answer holds the contexts signed
                let signing = SigningMachine.SigningView.Challenged(two, None)
                let recalculated = { two with Patient = otherDraft }

                held two None
                |> OrderPlanState.withOpened one.OrderContexts
                |> OrderPlanState.withWork PlanWork.Changed
                |> transitionWhile signing (OrderPlanMsg.PatientChanged(Some other, "r-1"))
                |> fst
                |> transitionWhile SigningMachine.SigningView.Idle OrderPlanMsg.Signed
                |> fst
                |> transitionWhile SigningMachine.SigningView.Idle (OrderPlanMsg.Answered("r-1", Ok recalculated))
                |> fst
                |> fun state ->
                    state
                    |> OrderPlanState.contextHeld
                    |> Expect.isFalse "the contexts are the ones signed"
                    state |> OrderPlanState.work |> Expect.equal "as signed" PlanWork.AsSigned
                    state
                    |> OrderPlanState.plan
                    |> Expect.equal "the answer shown" (Some recalculated)
            }

            test "an order added before the sign is released by the signature, nothing added meanwhile" {
                let added =
                    held one None
                    |> transitionWhile SigningMachine.SigningView.Idle (OrderPlanMsg.Command(add, "r-1"))
                    |> fst
                    |> transitionWhile SigningMachine.SigningView.Idle (OrderPlanMsg.Answered("r-1", Ok two))
                    |> fst

                added |> OrderPlanState.contextHeld |> Expect.isTrue "the order added holds"

                let another = OrderPlanCommand.AddOrderContext(two, context "" "amoxicilline")

                let meanwhile =
                    added
                    |> transitionWhile SigningMachine.SigningView.Requesting (OrderPlanMsg.Command(another, "r-2"))
                    |> fst

                meanwhile |> Expect.equal "nothing added meanwhile" added

                meanwhile
                |> transitionWhile SigningMachine.SigningView.Idle OrderPlanMsg.Signed
                |> fst
                |> OrderPlanState.contextHeld
                |> Expect.isFalse "the order plan is the version signed"
            }
        ]


/// The argumentation written on a context of the plan: the client's own, no call, a change of
/// the plan, kept over the answer under way.
[<Tests>]
let argueTests =
    let text = "Sepsis, hogere dosis in overleg met de apotheek"
    let transition = OrderPlanState.transition

    testList
        "OrderPlanMsg.Argue"
        [
            test "written on the context named, no call, and the plan's work is changed" {
                let state, effects =
                    held two None
                    |> OrderPlanState.withOpened two.OrderContexts
                    |> transition (OrderPlanMsg.Argue("c-2", text))

                effects |> Expect.isEmpty "no call"
                state |> OrderPlanState.work |> Expect.equal "changed" PlanWork.Changed

                state
                |> OrderPlanState.changed
                |> Expect.equal "held against the version opened" [| "c-2" |]

                match state |> OrderPlanState.view with
                | OrderPlanView.Settled(tp, _) ->
                    tp.OrderContexts
                    |> Array.map _.Argumentation
                    |> Expect.equal "c-2 only" [| None; Some text |]
                | other -> failtest $"expected settled, got %A{other}"
            }

            test "a text the plan already holds, and an id it does not, change nothing" {
                let shown = held two None

                shown
                |> transition (OrderPlanMsg.Argue("c-1", ""))
                |> Expect.equal "none written as none: as it was, work as signed" (shown, [])

                shown
                |> transition (OrderPlanMsg.Argue("c-9", text))
                |> Expect.equal "unknown id" (shown, [])
            }

            test "written while a step is under way, the answer keeps it; an added context keeps the answer's" {
                let busy = recalculating two None "r-1" (OrderPlanCommand.Recalculate two)
                let state, _ = busy |> transition (OrderPlanMsg.Argue("c-1", text))

                // shown meanwhile, though the recalculation carries the plan without it
                match state |> OrderPlanState.view with
                | OrderPlanView.Changing(shown, _) ->
                    shown.OrderContexts
                    |> Array.map _.Argumentation
                    |> Expect.equal "shown while the step runs" [| Some text; None |]
                | other -> failtest $"expected changing, got %A{other}"

                let landed, _ = state |> transition (OrderPlanMsg.Answered("r-1", Ok two))

                match landed |> OrderPlanState.view with
                | OrderPlanView.Settled(tp, _) ->
                    tp.OrderContexts
                    |> Array.map _.Argumentation
                    |> Expect.equal "kept on c-1" [| Some text; None |]
                | other -> failtest $"expected settled, got %A{other}"

                let added =
                    plan
                        [|
                            context "c-1" "paracetamol"
                            { context "c-3" "new" with Argumentation = Some "from the workbench" }
                        |]

                let adding = recalculating one None "r-2" (OrderPlanCommand.AddOrderContext(one, context "c-3" "new"))

                let landed, _ = adding |> transition (OrderPlanMsg.Answered("r-2", Ok added))

                match landed |> OrderPlanState.view with
                | OrderPlanView.Settled(tp, _) ->
                    tp.OrderContexts
                    |> Array.map _.Argumentation
                    |> Expect.equal "c-1 the client's none, c-3 the answer's" [| None; Some "from the workbench" |]
                | other -> failtest $"expected settled, got %A{other}"
            }

            test "a reset navigated into a context takes its text with it as it goes out, the other keeps its own" {
                let argued =
                    two
                    |> ArgumentationPolicy.writeIn "c-1" text
                    |> ArgumentationPolicy.writeIn "c-2" "other"

                let ctx = argued.OrderContexts[0]
                let reset = OrderPlanCommand.Navigate(argued, "c-1", OrderContextCommand.ResetOrderScenario, ctx)
                let cleared = argued |> ArgumentationPolicy.clearIn "c-1"

                let busy, effects = held argued (Some "c-1") |> transition (OrderPlanMsg.Command(reset, "r-1"))

                effects
                |> Expect.equal
                    "the plan and the context sent without the text"
                    [
                        OrderPlanEffect.CallPlan(
                            OrderPlanCommand.Navigate(
                                cleared,
                                "c-1",
                                OrderContextCommand.ResetOrderScenario,
                                ArgumentationPolicy.clear ctx
                            ),
                            "r-1"
                        )
                    ]

                let texts (state: OrderPlanState) =
                    match state |> OrderPlanState.view with
                    | OrderPlanView.Settled(tp, _)
                    | OrderPlanView.Changing(tp, _) -> tp.OrderContexts |> Array.map _.Argumentation
                    | OrderPlanView.NoPatient -> [||]

                busy
                |> texts
                |> Expect.equal "shown without the text meanwhile" [| None; Some "other" |]

                // the server echoes the text it was not sent: the answer keeps what the plan holds
                busy
                |> transition (OrderPlanMsg.Answered("r-1", Ok argued))
                |> fst
                |> texts
                |> Expect.equal "c-1 cleared, c-2 kept" [| None; Some "other" |]

                // a text written while the reset runs is the newer intent, and stays
                busy
                |> transition (OrderPlanMsg.Argue("c-1", "newer"))
                |> fst
                |> transition (OrderPlanMsg.Answered("r-1", Ok argued))
                |> fst
                |> texts
                |> Expect.equal "the newer text kept" [| Some "newer"; Some "other" |]
            }

            test "not admitted while a signature is under way" {
                let argue = OrderPlanMsg.Argue("c-1", text)

                OrderPlanState.admitted SigningMachine.SigningView.Requesting argue
                |> Expect.isFalse "a change, held back like a command"

                OrderPlanState.admitted SigningMachine.SigningView.Idle argue
                |> Expect.isTrue "admitted while idle"
            }
        ]


[<Tests>]
let awaitsTests =
    testList
        "OrderPlanState.awaits"
        [
            test "the request under way is awaited" {
                recalculating one None "r-1" (OrderPlanCommand.Recalculate one)
                |> OrderPlanState.awaits "r-1"
                |> Expect.isTrue "the answer to r-1 lands"
            }

            test "a request since replaced is not awaited" {
                recalculating one None "r-2" (OrderPlanCommand.Recalculate one)
                |> OrderPlanState.awaits "r-1"
                |> Expect.isFalse "the answer to r-1 is dropped"
            }

            test "nothing is awaited while no request is under way" {
                shown |> OrderPlanState.awaits "r-1" |> Expect.isFalse "no request under way"
            }
        ]


[<Tests>]
let reopenTests =
    // the context as the dialog sends it with a field cleared, and as the server answers it
    let cleared = context "c-1" "paracetamol-cleared"
    let reopened = context "c-1" "paracetamol-reopened"
    let answer = plan [| reopened; context "c-2" "ibuprofen" |]
    let open' = held two (Some "c-1")
    let clear = OrderPlanCommand.Navigate(two, "c-1", OrderContextCommand.ReopenOrderScenario [||], cleared)
    let move = OrderPlanState.transition
    let run msgs state = msgs |> List.fold (fun s m -> move m s |> fst) state

    testList
        "OrderPlanState.transition, a reopen and a restore"
        [
            test "a reopen sends the clear and counts as a change" {
                let state, effects = open' |> move (OrderPlanMsg.Reopen(clear, "r-1"))

                effects
                |> Expect.equal "the clear goes out" [ OrderPlanEffect.CallPlan(clear, "r-1") ]

                state
                |> OrderPlanState.work
                |> Expect.equal "changed while the list is open" PlanWork.Changed
            }

            test "a restore before the answer puts the plan back as signed, and the late answer is dropped" {
                let restored = open' |> run [ OrderPlanMsg.Reopen(clear, "r-1"); OrderPlanMsg.Restore ]

                restored |> Expect.equal "the state before the click" open'

                restored
                |> move (OrderPlanMsg.Answered("r-1", Ok answer))
                |> Expect.equal "the answer finds no request" (open', [])
            }

            test "a restore after the answer puts the plan back as signed" {
                let answered =
                    open'
                    |> run [ OrderPlanMsg.Reopen(clear, "r-1"); OrderPlanMsg.Answered("r-1", Ok answer) ]

                answered
                |> OrderPlanState.plan
                |> Expect.equal "the list shows the answer" (Some answer)

                answered
                |> move OrderPlanMsg.Restore
                |> Expect.equal "the state before the click" (open', [])
            }

            test "a plan changed before the click stays changed" {
                let changed = open' |> OrderPlanState.withWork PlanWork.Changed

                changed
                |> run [ OrderPlanMsg.Reopen(clear, "r-1"); OrderPlanMsg.Restore ]
                |> Expect.equal "as before the click" changed
            }

            test "a pick ends the look: a restore after it changes nothing" {
                let pick = OrderPlanCommand.Navigate(answer, "c-1", OrderContextCommand.UpdateOrderScenario, reopened)

                let picked =
                    open'
                    |> run
                        [
                            OrderPlanMsg.Reopen(clear, "r-1")
                            OrderPlanMsg.Answered("r-1", Ok answer)
                            OrderPlanMsg.Command(pick, "r-2")
                        ]

                picked
                |> move OrderPlanMsg.Restore
                |> Expect.equal "nothing to put back" (picked, [])
            }

            test "a restore without a reopen changes nothing" {
                open'
                |> move OrderPlanMsg.Restore
                |> Expect.equal "nothing to put back" (open', [])
            }

            test "a reopen while a request is under way keeps nothing: a restore cannot put that request back" {
                let remove = OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])
                let busy = open' |> move (OrderPlanMsg.Command(remove, "r-1")) |> fst

                let settled =
                    busy
                    |> run
                        [
                            OrderPlanMsg.Reopen(clear, "r-2")
                            OrderPlanMsg.Answered("r-1", Ok two)
                            OrderPlanMsg.Answered("r-2", Ok answer)
                        ]

                settled
                |> OrderPlanState.view
                |> Expect.equal "the clear answered" (OrderPlanView.Settled(answer, Some "c-1"))

                settled
                |> move OrderPlanMsg.Restore
                |> Expect.equal "nothing to put back, nothing in flight again" (settled, [])
            }

            test "a reopen is not admitted while a signature is under way; a restore is" {
                let signing = SigningMachine.SigningView.Requesting

                OrderPlanMsg.Reopen(clear, "r-1")
                |> OrderPlanState.admitted signing
                |> Expect.isFalse "reopen not admitted"

                OrderPlanMsg.Restore
                |> OrderPlanState.admitted signing
                |> Expect.isTrue "restore admitted"
            }
        ]
