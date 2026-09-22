module Informedica.GenPRES.Shared.Tests.OrderPlanMachineTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Api
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
                        |> Expect.equal "in flight over the plan held" (recalculating one None "r-1" rebased)

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
                            (held one None, [ OrderPlanEffect.CheckInteractions [ "paracetamol" ] ])

                        let kept = recalculating two (Some "c-1") "r-1" cmd

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) kept
                        |> Expect.equal
                            "the selection still holds"
                            (held one (Some "c-1"), [ OrderPlanEffect.CheckInteractions [ "paracetamol" ] ])

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
                            (held two None,
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
                |> Expect.equal "opening, nothing to show" OrderPlanView.Opening

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
                |> Expect.equal "pending under its own id, nothing sent" (waiting, [])

                transition (OrderPlanMsg.Command(remove, "r-2")) busy
                |> Expect.equal "dropped" (busy, [])
            }

            test "goes out over the plan answered: a step into the context as it now is, a value typed as sent" {
                let sent =
                    OrderPlanCommand.Navigate(answer, "c-1", OrderContextCommand.IncreaseScheduleFrequencyProperty, now)

                transition (OrderPlanMsg.Answered("r-1", Ok answer)) waiting
                |> Expect.equal
                    "the step, into the context answered"
                    (recalculating answer (Some "c-1") "r-2" sent,
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
                    (recalculating answer (Some "c-1") "r-2" sent,
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
                    (held one (Some "c-1"), [ OrderPlanEffect.CheckInteractions [ "paracetamol" ] ])

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
