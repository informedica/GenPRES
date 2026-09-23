module Informedica.GenPRES.Client.Core.Tests.OrderContextMachineTests

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Api
open OrderContextMachine


module Fixtures =

    let ten = { Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }

    /// The data a context carries, and the patient it is: an age makes the draft a patient.
    let draft = { Shared.Models.Patient.empty with Age = Some ten }

    let asPatient (dto: Patient) =
        match dto |> Shared.Models.Patient.validate with
        | Ok pat -> pat
        | Error err -> invalidOp $"the fixture is no patient: %A{err}"

    let patient = draft |> asPatient
    let otherDraft = { draft with Department = Some "other" }
    let other = otherDraft |> asPatient

    let empty = OrderContextState.emptyFor patient

    let paracetamol = { empty with OrderContext.Filter.Generic = Some "paracetamol" }

    let noPatient = OrderContextState.noPatient

    let heldFor = OrderContextState.held
    let held = heldFor patient

    /// A command under way over the context sent; the one held is what a failed change goes back to.
    let inFlightFor = OrderContextState.changing
    let inFlight = inFlightFor patient

    /// An evaluation under way.
    let evaluatingFor pat = inFlightFor pat OrderContextCommand.UpdateOrderContext

    let evaluating = evaluatingFor patient

    let opening = OrderContextState.opening

    let shown = held paracetamol

    /// A refusal: told, and the pages restored to the context's filter.
    let restored (ctx: OrderContext) errs =
        [
            OrderContextEffect.TellError errs
            OrderContextEffect.SyncFormulary ctx.Filter
            OrderContextEffect.SyncParenteralia ctx.Filter
        ]

    /// An evaluation of the context: the call and the two syncs.
    let evaluated (ctx: OrderContext) request =
        [
            OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, ctx, request)
            OrderContextEffect.SyncFormulary ctx.Filter
            OrderContextEffect.SyncParenteralia ctx.Filter
        ]

    let transition = OrderContextState.transition


open Fixtures


[<Tests>]
let tests =
    testList
        "OrderContextState.transition"
        [
            testList
                "the patient"
                [
                    test "a patient set evaluates the empty workbench under the request; the answer shows it" {
                        let loading, effects =
                            transition (OrderContextMsg.PatientChanged(Some patient, "r-1")) noPatient

                        loading |> Expect.equal "loading" (opening patient "r-1")

                        effects
                        |> Expect.equal
                            "the empty workbench evaluated"
                            [
                                OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, empty, "r-1")
                            ]

                        transition (OrderContextMsg.Answered("r-1", Ok paracetamol)) loading
                        |> Expect.equal "shown" (held paracetamol, [])
                    }

                    test
                        "a patient changed keeps the filter and evaluates it for the new patient, whatever was in flight superseded" {
                        let busy = evaluating paracetamol paracetamol "r-1"

                        let state, effects = transition (OrderContextMsg.PatientChanged(Some other, "r-2")) busy

                        let expected = { paracetamol with Patient = otherDraft }

                        state
                        |> Expect.equal "evaluating for the new patient" (evaluatingFor other expected expected "r-2")

                        effects |> Expect.equal "the call and the syncs" (evaluated expected "r-2")

                        transition (OrderContextMsg.Answered("r-1", Ok paracetamol)) state
                        |> Expect.equal "the older answer dropped" (state, [])
                    }

                    test
                        "a patient changed while a selection is in flight keeps the selection, and what a refusal restores" {
                        let chosen = { paracetamol with OrderContext.Filter.Generic = Some "ibuprofen" }
                        let busy = evaluating chosen paracetamol "r-1"

                        let state, effects = transition (OrderContextMsg.PatientChanged(Some other, "r-2")) busy

                        let sent = { chosen with Patient = otherDraft }
                        let found = { paracetamol with Patient = otherDraft }

                        state
                        |> Expect.equal
                            "the selection evaluated for the new patient"
                            (evaluatingFor other sent found "r-2")

                        effects |> Expect.equal "the call and the syncs" (evaluated sent "r-2")

                        transition (OrderContextMsg.Answered("r-2", Error [| "not loaded" |])) state
                        |> Expect.equal
                            "a refusal restores the last evaluated, for the new patient"
                            (heldFor other found, restored found [| "not loaded" |])
                    }

                    test "no patient: no workbench" {
                        transition (OrderContextMsg.PatientChanged(None, "r-1")) shown
                        |> Expect.equal "no patient" (noPatient, [])
                    }
                ]

            testList
                "the seed"
                [
                    test "a filter before a patient is dropped: the patient is part of it" {
                        transition (OrderContextMsg.Seed(paracetamol, "r-1")) noPatient
                        |> Expect.equal "dropped" (noPatient, [])

                        transition (OrderContextMsg.PatientChanged(Some patient, "r-2")) noPatient
                        |> fst
                        |> Expect.equal "the empty workbench opened, nothing waited" (opening patient "r-2")
                    }

                    test
                        "a filter during the first evaluation supersedes it; a failed one goes back to the empty workbench" {
                        let seeded, effects =
                            transition (OrderContextMsg.Seed(paracetamol, "r-2")) (opening patient "r-1")

                        seeded
                        |> Expect.equal
                            "the seed evaluated over the empty workbench held"
                            (evaluating paracetamol empty "r-2")

                        effects |> Expect.equal "the call and the syncs" (evaluated paracetamol "r-2")

                        transition (OrderContextMsg.Answered("r-1", Ok paracetamol)) seeded
                        |> Expect.equal "the first evaluation's answer is stale" (seeded, [])

                        transition (OrderContextMsg.Answered("r-2", Error [| "refused" |])) seeded
                        |> Expect.equal "back to the empty workbench" (held empty, restored empty [| "refused" |])
                    }

                    test "a filter with a patient held is evaluated at once, for that patient" {
                        let fromUrl = { paracetamol with Patient = otherDraft }
                        let state, effects = transition (OrderContextMsg.Seed(fromUrl, "r-1")) shown

                        state |> Expect.equal "evaluating" (evaluating paracetamol paracetamol "r-1")

                        effects |> Expect.equal "for the patient held" (evaluated paracetamol "r-1")
                    }

                    test "a command before a patient is dropped" {
                        transition
                            (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, paracetamol, "r-1"))
                            noPatient
                        |> Expect.equal "dropped" (noPatient, [])
                    }
                ]

            testList
                "a command"
                [
                    test "an update takes the formulary and the parenteralia along; a step calls alone, or waits" {
                        let changed =
                            { paracetamol with
                                OrderContext.Filter.Generic = Some "ibuprofen"
                                Patient = otherDraft
                            }

                        let forPatient = { changed with Patient = draft }

                        let busy, effects =
                            transition
                                (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, changed, "r-1"))
                                shown

                        busy
                        |> Expect.equal "in flight, for the patient held" (evaluating forPatient paracetamol "r-1")

                        effects |> Expect.equal "the call and the syncs" (evaluated forPatient "r-1")

                        transition
                            (OrderContextMsg.Command(
                                OrderContextCommand.IncreaseScheduleFrequencyProperty,
                                changed,
                                "r-2"
                            ))
                            busy
                        |> Expect.equal
                            "waits while busy, for the patient held"
                            (busy
                             |> OrderContextState.pending
                                 OrderContextCommand.IncreaseScheduleFrequencyProperty
                                 forPatient
                                 "r-2",
                             [])

                        transition
                            (OrderContextMsg.Command(
                                OrderContextCommand.IncreaseScheduleFrequencyProperty,
                                paracetamol,
                                "r-2"
                            ))
                            shown
                        |> Expect.equal
                            "a step calls alone"
                            (inFlight
                                OrderContextCommand.IncreaseScheduleFrequencyProperty
                                paracetamol
                                paracetamol
                                "r-2",
                             [
                                 OrderContextEffect.CallContext(
                                     OrderContextCommand.IncreaseScheduleFrequencyProperty,
                                     paracetamol,
                                     "r-2"
                                 )
                             ])
                    }

                    test "the answer lands on its request; a stale one is dropped" {
                        let busy = evaluating paracetamol paracetamol "r-1"

                        let answer =
                            { paracetamol with OrderContext.Filter.Generics = [| "paracetamol"; "ibuprofen" |] }

                        transition (OrderContextMsg.Answered("r-1", Ok answer)) busy
                        |> Expect.equal "shown" (held answer, [])

                        transition (OrderContextMsg.Answered("r-9", Ok answer)) busy
                        |> Expect.equal "stale" (busy, [])
                    }

                    test "a refused command leaves the workbench as the request found it, and says why" {
                        let busy = evaluating paracetamol paracetamol "r-1"

                        transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) busy
                        |> Expect.equal "as found" (held paracetamol, restored paracetamol [| "not loaded" |])

                        transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) (opening patient "r-1")
                        |> Expect.equal "the empty workbench" (held empty, restored empty [| "not loaded" |])
                    }

                    test "no dose rules for the filter: back to the first page, the empty workbench evaluated again" {
                        let busy = evaluating paracetamol paracetamol "r-1"

                        let errs = [| "Geen doseerregels gevonden voor het geselecteerde filter" |]

                        transition (OrderContextMsg.Answered("r-1", Error errs)) busy
                        |> Expect.equal
                            "left, told, evaluated empty with the pages in step"
                            (evaluating empty empty "r-1",
                             OrderContextEffect.GoToLifeSupport
                             :: OrderContextEffect.TellError errs
                             :: evaluated empty "r-1")
                    }

                    test "a refused step restores the context last evaluated, never the one sent" {
                        let stepped = { paracetamol with OrderContext.Filter.Route = Some "stepped" }

                        let busy, _ =
                            transition
                                (OrderContextMsg.Command(
                                    OrderContextCommand.IncreaseScheduleFrequencyProperty,
                                    stepped,
                                    "r-1"
                                ))
                                shown

                        busy
                        |> Expect.equal
                            "the sent shown meanwhile, the found kept"
                            (inFlight OrderContextCommand.IncreaseScheduleFrequencyProperty stepped paracetamol "r-1")

                        transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) busy
                        |> Expect.equal "the last evaluated" (held paracetamol, restored paracetamol [| "not loaded" |])
                    }
                ]

            testList
                "the reset"
                [
                    test "the workbench cleared for the patient held and evaluated empty; nothing without a patient" {
                        let state, effects = transition (OrderContextMsg.Reset "r-1") shown

                        state
                        |> Expect.equal "evaluating the empty workbench" (evaluating empty empty "r-1")

                        effects |> Expect.equal "the call and the syncs" (evaluated empty "r-1")

                        transition (OrderContextMsg.Reset "r-1") noPatient
                        |> Expect.equal "nothing" (noPatient, [])
                    }
                ]
        ]


[<Tests>]
let viewTests =
    testList
        "OrderContextState.view"
        [
            test "no patient; the context held: settled" {
                noPatient
                |> OrderContextState.view
                |> Expect.equal "no patient" OrderContextView.NoPatient

                shown
                |> OrderContextState.view
                |> Expect.equal "the context held" (OrderContextView.Settled paracetamol)
            }

            test "the first evaluation: the empty context, changing; a change under way: the context sent" {
                opening patient "r-1"
                |> OrderContextState.view
                |> Expect.equal "the empty context shown while it is evaluated" (OrderContextView.Changing empty)

                let stepped = { paracetamol with OrderContext.Filter.Route = Some "stepped" }

                inFlight OrderContextCommand.IncreaseScheduleFrequencyProperty stepped paracetamol "r-1"
                |> OrderContextState.view
                |> Expect.equal "the context sent" (OrderContextView.Changing stepped)
            }

            test "the order dialog's context: the selected context, settled or changing as the plan is" {
                let inPlan = { paracetamol with Id = "c-1" }
                let plan = Shared.Models.OrderPlan.create patient [| inPlan |]

                OrderPlanMachine.OrderPlanState.held patient plan (Some "c-1")
                |> OrderPlanMachine.OrderPlanState.view
                |> OrderContextView.dialog
                |> Expect.equal "settled with the plan" (Some(OrderContextView.Settled inPlan))

                OrderPlanMachine.OrderPlanState.changing
                    patient
                    plan
                    (Some "c-1")
                    (OrderPlanCommand.Recalculate plan)
                    "r-1"
                |> OrderPlanMachine.OrderPlanState.view
                |> OrderContextView.dialog
                |> Expect.equal "changing with the plan" (Some(OrderContextView.Changing inPlan))

                OrderPlanMachine.OrderPlanState.held patient plan None
                |> OrderPlanMachine.OrderPlanState.view
                |> OrderContextView.dialog
                |> Expect.equal "no selection, no dialog" None

                OrderPlanMachine.OrderPlanState.held patient plan (Some "c-9")
                |> OrderPlanMachine.OrderPlanState.view
                |> OrderContextView.dialog
                |> Expect.equal "a selection the plan does not hold" None

                OrderPlanMachine.OrderPlanState.noPatient
                |> OrderPlanMachine.OrderPlanState.view
                |> OrderContextView.dialog
                |> Expect.equal "no plan" None
            }
        ]


[<Tests>]
let pendingTests =
    let step = OrderContextCommand.IncreaseScheduleFrequencyProperty
    let busy = inFlight step paracetamol paracetamol "r-1"
    let waiting = busy |> OrderContextState.pending step paracetamol "r-2"

    testList
        "a dialog command while a request is under way"
        [
            test "waits as the one pending, the latest replacing an earlier one; the page's commands are dropped" {
                transition (OrderContextMsg.Command(step, paracetamol, "r-2")) busy
                |> Expect.equal "pending under its own id, nothing sent" (waiting, [])

                let other = OrderContextCommand.DecreaseScheduleFrequencyProperty

                transition (OrderContextMsg.Command(other, paracetamol, "r-3")) waiting
                |> Expect.equal "the latest replaces it" (busy |> OrderContextState.pending other paracetamol "r-3", [])

                transition
                    (OrderContextMsg.Command(OrderContextCommand.SelectOrderScenario, paracetamol, "r-3"))
                    waiting
                |> Expect.equal "a selection is dropped, the pending kept" (waiting, [])
            }

            test "a step goes out over the context answered; a value typed over the context it was sent with" {
                let answer = { paracetamol with OrderContext.Filter.Generics = [| "paracetamol"; "ibuprofen" |] }

                transition (OrderContextMsg.Answered("r-1", Ok answer)) waiting
                |> Expect.equal
                    "the step, from the answer"
                    (inFlight step answer answer "r-2", [ OrderContextEffect.CallContext(step, answer, "r-2") ])

                let typed = { paracetamol with OrderContext.Filter.Route = Some "typed" }
                let update = OrderContextCommand.UpdateOrderScenario

                busy
                |> OrderContextState.pending update typed "r-2"
                |> transition (OrderContextMsg.Answered("r-1", Ok answer))
                |> Expect.equal
                    "the value typed, over what it was typed into"
                    (inFlight update typed answer "r-2", [ OrderContextEffect.CallContext(update, typed, "r-2") ])
            }

            test "a failure, a patient change and a reset drop it" {
                transition (OrderContextMsg.Answered("r-1", Error [| "refused" |])) waiting
                |> Expect.equal
                    "the failure told, nothing sent"
                    (held paracetamol, restored paracetamol [| "refused" |])

                let forOther = { paracetamol with Patient = otherDraft }

                transition (OrderContextMsg.PatientChanged(Some other, "r-3")) waiting
                |> Expect.equal
                    "evaluated for the new patient, the pending gone"
                    (evaluatingFor other forOther forOther "r-3", evaluated forOther "r-3")

                transition (OrderContextMsg.Reset "r-3") waiting
                |> Expect.equal
                    "the workbench cleared, the pending gone"
                    (evaluating empty empty "r-3", evaluated empty "r-3")
            }
        ]


[<Tests>]
let stagesTests =
    testList
        "the two stages"
        [
            test "the workbench alone knows no request: a failed change keeps the context held" {
                let stepped = { paracetamol with OrderContext.Filter.Route = Some "stepped" }

                OrderContextWorkbench.step
                    (OrderContextWorkbenchMsg.Landed(
                        (OrderContextCommand.UpdateOrderContext, stepped),
                        Error [| "not loaded" |]
                    ))
                    (OrderContextWorkbench.Evaluated(patient, paracetamol))
                |> Expect.equal
                    "the original, told and synced"
                    (OrderContextWorkbench.Evaluated(patient, paracetamol),
                     [
                         OrderContextWorkbenchIntent.Tell [| "not loaded" |]
                         OrderContextWorkbenchIntent.Sync paracetamol.Filter
                     ])
            }

            test "nothing lands where nothing was asked: no patient" {
                let landed =
                    OrderContextWorkbenchMsg.Landed(
                        (OrderContextCommand.UpdateOrderContext, paracetamol),
                        Ok paracetamol
                    )

                OrderContextWorkbench.step landed OrderContextWorkbench.NoPatient
                |> Expect.equal "no patient" (OrderContextWorkbench.NoPatient, [])

                transition (OrderContextMsg.Answered("r-1", Ok paracetamol)) noPatient
                |> Expect.equal "no request under way to land on" (noPatient, [])
            }

            test
                "the context shown is the one sent while a change is under way, else the one held; none before the first evaluation" {
                let stepped = { paracetamol with OrderContext.Filter.Route = Some "stepped" }

                let busy = inFlight OrderContextCommand.IncreaseScheduleFrequencyProperty stepped paracetamol "r-1"

                busy |> OrderContextState.context |> Expect.equal "the one sent" (Some stepped)

                busy
                |> OrderContextState.patient
                |> Expect.equal "the patient held" (Some patient)

                shown
                |> OrderContextState.context
                |> Expect.equal "the one held" (Some paracetamol)

                opening patient "r-1"
                |> OrderContextState.context
                |> Expect.equal "the empty context while the first evaluation runs" (Some empty)
            }
        ]


[<Tests>]
let selectionTests =
    // a context whose one scenario has an order the dialog can select
    let withOrder =
        { paracetamol with Scenarios = [| OrderPlanMachineTests.Fixtures.scenario "o-1" "paracetamol" |] }

    let select = OrderContextState.select
    let dialog = OrderContextState.dialog
    let selected = held withOrder |> select (Some "o-1")

    let busy = inFlight OrderContextCommand.IncreaseScheduleFrequencyProperty withOrder withOrder "r-1"

    let busySelected = busy |> select (Some "o-1")

    testList
        "OrderContextState.select"
        [
            test "selected over a context held that holds the order; none over one that does not" {
                selected
                |> dialog
                |> Expect.equal "the dialog shows the context held" (Some(OrderContextView.Settled withOrder))

                selected
                |> OrderContextState.view
                |> Expect.equal "the view is unchanged" (OrderContextView.Settled withOrder)

                held paracetamol
                |> select (Some "o-1")
                |> Expect.equal "an order the context does not hold" (held paracetamol)
            }

            test "nothing to select without a patient, nor during the first evaluation" {
                noPatient |> select (Some "o-1") |> Expect.equal "no patient" noPatient

                opening patient "r-1"
                |> select (Some "o-1")
                |> Expect.equal "the empty context under evaluation" (opening patient "r-1")
            }

            test "kept beside a request under way; none closes the dialog, whatever is in flight" {
                busySelected
                |> dialog
                |> Expect.equal "the dialog shows the context sent" (Some(OrderContextView.Changing withOrder))

                transition (OrderContextMsg.Select None) busySelected
                |> Expect.equal "closed, the request kept" (busy, [])

                transition (OrderContextMsg.Select(Some "o-1")) (held withOrder)
                |> Expect.equal "selected through the machine" (selected, [])
            }

            test "dropped by a patient change, a seed and a reset" {
                transition (OrderContextMsg.PatientChanged(Some other, "r-1")) selected
                |> fst
                |> dialog
                |> Expect.equal "a patient change" None

                transition (OrderContextMsg.PatientChanged(Some other, "r-2")) busySelected
                |> fst
                |> dialog
                |> Expect.equal "a patient change during a request" None

                transition (OrderContextMsg.Seed(paracetamol, "r-1")) selected
                |> fst
                |> dialog
                |> Expect.equal "a seed" None

                transition (OrderContextMsg.Reset "r-1") selected
                |> fst
                |> dialog
                |> Expect.equal "a reset" None
            }

            test "an answer keeps it while the context answered holds the order, and drops it otherwise" {
                transition (OrderContextMsg.Answered("r-1", Ok withOrder)) busySelected
                |> fst
                |> dialog
                |> Expect.equal "the order answered" (Some(OrderContextView.Settled withOrder))

                transition (OrderContextMsg.Answered("r-1", Ok paracetamol)) busySelected
                |> fst
                |> dialog
                |> Expect.equal "the order gone from the answer" None

                transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) busySelected
                |> fst
                |> dialog
                |> Expect.equal
                    "a failed change keeps the context held, and the order"
                    (Some(OrderContextView.Settled withOrder))

                transition (OrderContextMsg.Answered("r-1", Error [| "geen doseerregels" |])) busySelected
                |> fst
                |> dialog
                |> Expect.equal "a start over holds none" None
            }
        ]
