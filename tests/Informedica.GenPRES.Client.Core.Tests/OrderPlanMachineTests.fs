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

    /// The patient machine with no change under way, and with one under way.
    let noPatientChange = PatientMachine.PatientState.init (Some draft)

    let patientChanging =
        noPatientChange
        |> PatientMachine.PatientState.transition (
            PatientMachine.PatientMsg.Changed(Some otherDraft, PatientDraftPolicy.Estimates.Kept, "p-1")
        )
        |> fst

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

    /// The page's message for a wire command, the plan in it left out: the machine builds the
    /// command over the plan it holds.
    let pageMsg (cmd: OrderPlanCommand, request) =
        let change =
            match cmd with
            | OrderPlanCommand.AddOrderContext(_, ctx) -> OrderPlanChange.Add ctx
            | OrderPlanCommand.NewOrderContext(_, category) -> OrderPlanChange.NewNutrition category
            | OrderPlanCommand.RemoveOrderContexts(_, ids) -> OrderPlanChange.Remove ids
            | OrderPlanCommand.FilterRows(ids, _) -> OrderPlanChange.FilterRows ids
            | OrderPlanCommand.Navigate(_, id, ctxCmd, _) -> OrderPlanChange.OrderDialogCommand(id, ctxCmd)
            | OrderPlanCommand.UpdatePatient _
            | OrderPlanCommand.Open _ -> invalidArg (nameof cmd) "no page sends this command"

        OrderPlanMsg.Change(change, request)


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
                        let asked, effects = transition (OrderPlanMsg.PatientDataChanged(Some patient, "r-1")) noPatient

                        asked |> Expect.equal "asked" (loading patient [||] "r-1")

                        effects
                        |> Expect.equal "open" [ OrderPlanEffect.CallPlan(OrderPlanCommand.Open(draft, [||]), "r-1") ]

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) asked
                        |> Expect.equal
                            "shown, its one drug checked"
                            (held one None,
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol" ]
                                 OrderPlanEffect.TellAnswered
                             ])
                    }

                    test "a patient changed recalculates the plan held over the new patient, the dialog closed" {
                        let state, effects =
                            transition (OrderPlanMsg.PatientDataChanged(Some other, "r-2")) (held one (Some "c-1"))

                        let update = OrderPlanCommand.UpdatePatient(otherDraft, one)

                        state
                        |> Expect.equal
                            "the plan held, the patient update in the command"
                            (recalculatingFor other one None "r-2" update)

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
                        state |> Expect.equal "no patient" noPatient
                        effects |> Expect.isEmpty "nothing to do"

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) state
                        |> Expect.equal "the answer dropped" (noPatient, [])
                    }
                ]

            testList
                "the cart"
                [
                    test "the signed version opens over the patient held" {
                        let opening, effects = transition (OrderPlanMsg.OpenSignedPlan(head, "r-1")) shown

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
                            (held two None,
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol"; "ibuprofen" ]
                                 OrderPlanEffect.TellAnswered
                             ])
                    }

                    test "the version arriving before its patient is kept and opened when the patient does" {
                        // a Session opens: the plan machine hears the version first, the patient it
                        // was opened with one message later
                        let kept, effects = transition (OrderPlanMsg.OpenSignedPlan(head, "r-1")) noPatient

                        (kept, effects)
                        |> Expect.equal "kept, nothing asked yet" (OrderPlanState.awaiting two.OrderContexts, [])

                        transition (OrderPlanMsg.PatientDataChanged(Some patient, "r-2")) kept
                        |> Expect.equal
                            "the version's contexts opened for the patient"
                            (loading patient two.OrderContexts "r-2",
                             [
                                 OrderPlanEffect.CallPlan(OrderPlanCommand.Open(draft, two.OrderContexts), "r-2")
                             ])

                        transition (OrderPlanMsg.PatientDataChanged(None, "r-3")) kept
                        |> Expect.equal "no patient after all: the version is let go" (noPatient, [])
                    }
                ]

            testList
                "a change"
                [
                    test "sent over the plan held" {
                        let stale = { one with Filtered = [| "c-9" |] }
                        let cmd = OrderPlanCommand.RemoveOrderContexts(stale, [| "c-1" |])
                        let busy, effects = transition (pageMsg (cmd, "r-1")) shown
                        let rebased = OrderPlanCommand.RemoveOrderContexts(one, [| "c-1" |])

                        busy
                        |> Expect.equal
                            "in flight over the plan held, one change of work"
                            (recalculating one None "r-1" rebased |> OrderPlanState.withWork PlanWork.Changed)

                        effects
                        |> Expect.equal "the rebased command" [ OrderPlanEffect.CallPlan(rebased, "r-1") ]
                    }

                    test "a row filter goes over the plan held, the rows in the command; the plan held stays" {
                        let filtered = { one with Filtered = [| "c-1" |] }
                        let rows = OrderPlanCommand.FilterRows([| "c-1" |], one)

                        let busy, _ = transition (pageMsg (OrderPlanCommand.FilterRows([| "c-1" |], two), "r-1")) shown

                        busy
                        |> Expect.equal "the plan held, the rows in the command" (recalculating one None "r-1" rows)

                        OrderPlanState.meanwhile one rows
                        |> Expect.equal "the pages show the rows chosen meanwhile" filtered

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
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol" ]
                                 OrderPlanEffect.TellAnswered
                             ])

                        let kept = recalculating two (Some "c-1") "r-1" cmd

                        transition (OrderPlanMsg.Answered("r-1", Ok one)) kept
                        |> Expect.equal
                            "the selection still holds"
                            (held one (Some "c-1") |> OrderPlanState.withOpened two.OrderContexts,
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol" ]
                                 OrderPlanEffect.TellAnswered
                             ])

                        transition (OrderPlanMsg.Answered("r-9", Ok one)) kept
                        |> Expect.equal "a stale answer dropped" (kept, [])
                    }

                    test "a failed filter change goes back to the original, and says why" {
                        // the plan sent differs from the one held, so that the test can tell which
                        // one a failed change leaves behind
                        let busy, _ =
                            transition (OrderPlanMsg.Change(OrderPlanChange.FilterRows [| "c-1" |], "r-1")) shown

                        transition (OrderPlanMsg.Answered("r-1", Error [| "no dose rules" |])) busy
                        |> Expect.equal
                            "the plan held, rows and totals in step"
                            (held one None, [ OrderPlanEffect.TellError [| "no dose rules" |] ])

                        // a page's change, the dialog open: back to the plan held, the dialog kept
                        let recalculating, _ =
                            transition
                                (OrderPlanMsg.Change(OrderPlanChange.Remove [| "c-9" |], "r-1"))
                                (held one (Some "c-1"))

                        transition (OrderPlanMsg.Answered("r-1", Error [| "no dose rules" |])) recalculating
                        |> Expect.equal
                            "the plan held, the selection kept, the removal counted as it went out"
                            (held one (Some "c-1") |> OrderPlanState.withWork PlanWork.Changed,
                             [ OrderPlanEffect.TellError [| "no dose rules" |] ])

                        let opening = loading patient [||] "r-1"

                        transition (OrderPlanMsg.Answered("r-1", Error [| "not loaded" |])) opening
                        |> Expect.equal
                            "a refused open: the empty plan"
                            (held (plan [||]) None, [ OrderPlanEffect.TellError [| "not loaded" |] ])
                    }

                    test "an order prescribed: the answer is shown and checked, nothing more" {
                        let workbench = context "" "ibuprofen"

                        let busy = recalculating one None "r-1" (OrderPlanCommand.AddOrderContext(one, workbench))

                        transition (OrderPlanMsg.Answered("r-1", Ok two)) busy
                        |> Expect.equal
                            "shown and checked"
                            (held two None |> OrderPlanState.withOpened one.OrderContexts,
                             [
                                 OrderPlanEffect.CheckInteractions [ "paracetamol"; "ibuprofen" ]
                                 OrderPlanEffect.TellAnswered
                             ])
                    }
                ]

            testList
                "the selection and the filter"
                [
                    test "the selection is the client's own, kept next to a request in flight" {
                        transition (OrderPlanMsg.SelectContext(Some "c-1")) shown
                        |> Expect.equal "selected" (held one (Some "c-1"), [])

                        let busy = recalculating one None "r-1" (OrderPlanCommand.FilterRows(one.Filtered, one))

                        transition (OrderPlanMsg.SelectContext(Some "c-1")) busy
                        |> Expect.equal
                            "selected while busy"
                            (recalculating one (Some "c-1") "r-1" (OrderPlanCommand.FilterRows(one.Filtered, one)), [])

                        transition (OrderPlanMsg.SelectContext None) noPatient
                        |> Expect.equal "nothing to select" (noPatient, [])
                    }

                    test "the filter recalculates the totals over the rows chosen, the dialog closed" {
                        let selected = held one (Some "c-1")
                        let filtered = { one with Filtered = [| "c-1" |] }
                        let state, effects =
                            transition (OrderPlanMsg.Change(OrderPlanChange.FilterRows [| "c-1" |], "r-1")) selected

                        state
                        |> Expect.equal
                            "recalculating over the plan held, the rows in the command, the dialog closed"
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

                recalculating one (Some "c-1") "r-1" (OrderPlanCommand.FilterRows([| "c-1" |], one))
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
let planTests =
    testList
        "what the plan holds"
        [
            test "a failed change keeps the plan held, a landed one is checked" {
                let filtered = { one with Filtered = [| "c-1" |] }
                let rows = OrderPlanCommand.FilterRows([| "c-1" |], one)

                transition (OrderPlanMsg.Answered("r-1", Error [| "not loaded" |])) (recalculating one None "r-1" rows)
                |> Expect.equal "the original, told" (held one None, [ OrderPlanEffect.TellError [| "not loaded" |] ])

                transition (OrderPlanMsg.Answered("r-1", Ok filtered)) (recalculating one None "r-1" rows)
                |> Expect.equal
                    "the plan answered, its drugs checked"
                    (held filtered None |> OrderPlanState.withOpened one.OrderContexts,
                     [
                         OrderPlanEffect.CheckInteractions [ "paracetamol" ]
                         OrderPlanEffect.TellAnswered
                     ])
            }

            test "nothing lands without a patient; the dialog selects only over a plan held" {
                transition (OrderPlanMsg.Answered("r-1", Ok one)) noPatient
                |> Expect.equal "no patient" (noPatient, [])

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
            test "a plan held is as signed; a command that changes it is work, a recalculation is not" {
                held one None
                |> OrderPlanState.work
                |> Expect.equal "as signed" PlanWork.AsSigned

                transition (pageMsg (remove, "r-1")) (held one None)
                |> workOf
                |> Expect.equal "one change" PlanWork.Changed

                transition (pageMsg (filterRows, "r-1")) (held one None |> OrderPlanState.withWork PlanWork.Changed)
                |> workOf
                |> Expect.equal "still one change" PlanWork.Changed
            }

            test "a step into an order counts when it goes out" {
                let step =
                    OrderPlanCommand.Navigate(
                        one,
                        "c-1",
                        OrderViewCommand.IncreaseScheduleFrequencyProperty,
                        one.OrderContexts[0]
                    )

                transition (pageMsg (step, "r-3")) (held one None)
                |> workOf
                |> Expect.equal "the one that goes out" PlanWork.Changed
            }

            test "a version opened and a patient cleared are as signed" {
                transition
                    (OrderPlanMsg.OpenSignedPlan(version, "r-1"))
                    (held one None |> OrderPlanState.withWork PlanWork.Changed)
                |> workOf
                |> Expect.equal "a version opened" PlanWork.AsSigned

                transition
                    (OrderPlanMsg.PatientDataChanged(None, "r-1"))
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
    let changedOf (state: OrderPlanState, _: OrderPlanEffect list) = state |> OrderPlanState.changed
    let heldOf result = changedOf result |> Array.isEmpty |> not

    /// The answer to a command sent over the plan held, landed.
    let landed cmd answer state =
        transition (pageMsg (cmd, "r-1")) state
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

                transition (OrderPlanMsg.Change(OrderPlanChange.FilterRows [| "c-1" |], "r-1")) (held one None)
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

                transition (OrderPlanMsg.OpenSignedPlan(head, "r-1")) holding
                |> heldOf
                |> Expect.isFalse "the version is being opened"

                transition (OrderPlanMsg.PatientDataChanged(None, "r-1")) holding
                |> heldOf
                |> Expect.isFalse "no patient, nothing held"
            }
        ]


[<Tests>]
let signingTests =
    let add = OrderPlanCommand.AddOrderContext(one, context "" "ibuprofen")
    let transition = OrderPlanState.transition

    testList
        "the order plan signed"
        [
            test "an order added before the sign is released by the signature" {
                let added =
                    held one None
                    |> transition (pageMsg (add, "r-1"))
                    |> fst
                    |> transition (OrderPlanMsg.Answered("r-1", Ok two))
                    |> fst

                added |> OrderPlanState.changed |> Expect.isNonEmpty "the order added holds"

                added
                |> transition OrderPlanMsg.Signed
                |> fst
                |> OrderPlanState.changed
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
                    held two None
                    |> OrderPlanState.withOpened two.OrderContexts
                    |> transition (OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("c-2", argue), "r-1"))

                effects
                |> Expect.equal
                    "the text into c-2, over the plan held"
                    [
                        OrderPlanEffect.CallPlan(
                            OrderPlanCommand.Navigate(two, "c-2", argue, two.OrderContexts[1]),
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

                held two None
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
let navigateTests =
    let step = OrderViewCommand.IncreaseScheduleFrequencyProperty
    let open' = held two (Some "c-1")

    testList
        "a command from the order dialog"
        [
            test "goes over the plan held and that context as answered" {
                let sent = OrderPlanCommand.Navigate(two, "c-1", step, two.OrderContexts[0])

                open'
                |> OrderPlanState.transition (
                    OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("c-1", step), "r-1")
                )
                |> snd
                |> Expect.equal "the step over the context held" [ OrderPlanEffect.CallPlan(sent, "r-1") ]
            }

            test "goes nowhere for a context the plan does not hold" {
                open'
                |> OrderPlanState.transition (
                    OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand("gone", step), "r-1")
                )
                |> Expect.equal "nothing sent" (open', [])
            }
        ]


[<Tests>]
let reopenTests =
    // the context as the server answers a clear
    let reopened = context "c-1" "paracetamol-reopened"
    let answer = plan [| reopened; context "c-2" "ibuprofen" |]
    let open' = held two (Some "c-1")
    let reopen request =
        OrderPlanMsg.ReopenField(
            "c-1",
            OrderViewCommand.ClearScheduleProperty(ScheduleProperty.Frequency, [||]),
            request
        )
    // the clear as the machine sends it, over the plan and the context held
    let clear =
        OrderPlanCommand.Navigate(
            two,
            "c-1",
            OrderViewCommand.ClearScheduleProperty(ScheduleProperty.Frequency, [||]),
            two.OrderContexts[0]
        )
    let move = OrderPlanState.transition
    let run msgs state = msgs |> List.fold (fun s m -> move m s |> fst) state

    testList
        "OrderPlanState.transition, a reopen and a restore"
        [
            test "a reopen sends the clear over the context held and counts as a change" {
                let state, effects = open' |> move (reopen "r-1")

                effects
                |> Expect.equal "the clear goes out" [ OrderPlanEffect.CallPlan(clear, "r-1") ]

                state
                |> OrderPlanState.work
                |> Expect.equal "changed while the list is open" PlanWork.Changed
            }

            test "a restore before the answer puts the plan back as signed, and the late answer is dropped" {
                let restored = open' |> run [ reopen "r-1"; OrderPlanMsg.RestoreField ]

                restored |> Expect.equal "the state before the click" open'

                restored
                |> move (OrderPlanMsg.Answered("r-1", Ok answer))
                |> Expect.equal "the answer finds no request" (open', [])
            }

            test "a restore after the answer puts the plan back as signed" {
                let answered = open' |> run [ reopen "r-1"; OrderPlanMsg.Answered("r-1", Ok answer) ]

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
                |> run [ reopen "r-1"; OrderPlanMsg.RestoreField ]
                |> Expect.equal "as before the click" changed
            }

            test "a pick ends the look: a restore after it changes nothing" {
                let pick =
                    OrderPlanCommand.Navigate(
                        answer,
                        "c-1",
                        OrderViewCommand.SetNthScheduleProperty(ScheduleProperty.Frequency, 0),
                        reopened
                    )

                let picked =
                    open'
                    |> run [ reopen "r-1"; OrderPlanMsg.Answered("r-1", Ok answer); pageMsg (pick, "r-2") ]

                picked
                |> move OrderPlanMsg.RestoreField
                |> Expect.equal "nothing to put back" (picked, [])
            }

            test "a restore without a reopen changes nothing" {
                open'
                |> move OrderPlanMsg.RestoreField
                |> Expect.equal "nothing to put back" (open', [])
            }

            test "a reopen for a context the plan no longer holds sends nothing and keeps nothing" {
                let gone =
                    OrderPlanMsg.ReopenField(
                        "c-9",
                        OrderViewCommand.ClearScheduleProperty(ScheduleProperty.Frequency, [||]),
                        "r-1"
                    )

                let state, effects = open' |> move gone

                (state, effects) |> Expect.equal "nothing sent" (open', [])
                state |> OrderPlanState.isKept |> Expect.isFalse "nothing kept"
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
                    OrderPlanChange.Add ctx, OrderPlanCommand.AddOrderContext(two, ctx)
                    OrderPlanChange.NewNutrition NutritionCategory.TPN,
                    OrderPlanCommand.NewOrderContext(two, NutritionCategory.TPN)
                    OrderPlanChange.Remove [| "c-2" |], OrderPlanCommand.RemoveOrderContexts(two, [| "c-2" |])
                    OrderPlanChange.FilterRows [| "c-1" |], OrderPlanCommand.FilterRows([| "c-1" |], two)
                    OrderPlanChange.OrderDialogCommand("c-1", step),
                    OrderPlanCommand.Navigate(two, "c-1", step, two.OrderContexts[0])
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

            test "a prescription, a new nutrition context and a removal go out over the plan held" {
                let ctx = context "" "ibuprofen"

                [
                    OrderPlanMsg.Change(OrderPlanChange.Add ctx, "r-1"), OrderPlanCommand.AddOrderContext(one, ctx)
                    OrderPlanMsg.Change(OrderPlanChange.NewNutrition NutritionCategory.TPN, "r-1"),
                    OrderPlanCommand.NewOrderContext(one, NutritionCategory.TPN)
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
            test "a change, a reopen, a patient set and a version opened leave the state and send nothing" {
                for msg in
                    [
                        pageMsg (remove, "r-2")
                        OrderPlanMsg.ReopenField(
                            "c-1",
                            OrderViewCommand.ClearScheduleProperty(ScheduleProperty.Frequency, [||]),
                            "r-2"
                        )
                        OrderPlanMsg.PatientDataChanged(Some other, "r-2")
                        OrderPlanMsg.OpenSignedPlan(head, "r-2")
                    ] do
                    transition msg busy |> Expect.equal $"%A{msg}" (busy, [])
            }

            test "the patient cleared still resets it" {
                transition (OrderPlanMsg.PatientDataChanged(None, "r-2")) busy
                |> Expect.equal "no patient" (noPatient, [])
            }
        ]
