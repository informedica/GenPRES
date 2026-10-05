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

                        transition
                            (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated paracetamol)))
                            loading
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

                        transition
                            (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated paracetamol)))
                            state
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

                        transition
                            (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated paracetamol)))
                            seeded
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

                        transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated answer))) busy
                        |> Expect.equal "shown" (held answer, [])

                        transition (OrderContextMsg.Answered("r-9", Ok(OrderContextResponse.Evaluated answer))) busy
                        |> Expect.equal "stale" (busy, [])
                    }

                    test "a refused command leaves the workbench as the request found it, and says why" {
                        let busy = evaluating paracetamol paracetamol "r-1"

                        transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) busy
                        |> Expect.equal "as found" (held paracetamol, restored paracetamol [| "not loaded" |])

                        transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) (opening patient "r-1")
                        |> Expect.equal "the empty workbench" (held empty, restored empty [| "not loaded" |])
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

                transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated answer))) waiting
                |> Expect.equal
                    "the step, from the answer"
                    (inFlight step answer answer "r-2", [ OrderContextEffect.CallContext(step, answer, "r-2") ])

                let typed = { paracetamol with OrderContext.Filter.Route = Some "typed" }
                let update = OrderContextCommand.UpdateOrderScenario

                busy
                |> OrderContextState.pending update typed "r-2"
                |> transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated answer)))
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
                        Ok(OrderContextResponse.Evaluated paracetamol)
                    )

                OrderContextWorkbench.step landed OrderContextWorkbench.NoPatient
                |> Expect.equal "no patient" (OrderContextWorkbench.NoPatient, [])

                transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated paracetamol))) noPatient
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
                transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated withOrder))) busySelected
                |> fst
                |> dialog
                |> Expect.equal "the order answered" (Some(OrderContextView.Settled withOrder))

                transition
                    (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated paracetamol)))
                    busySelected
                |> fst
                |> dialog
                |> Expect.equal "the order gone from the answer" None

                transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) busySelected
                |> fst
                |> dialog
                |> Expect.equal
                    "a failed change keeps the context held, and the order"
                    (Some(OrderContextView.Settled withOrder))
            }
        ]


[<Tests>]
let refusalTests =
    // a context whose one scenario has an order the dialog can select
    let withOrder =
        { paracetamol with Scenarios = [| OrderPlanMachineTests.Fixtures.scenario "o-1" "paracetamol" |] }

    let refused = OrderContextState.refused patient
    let view = OrderContextState.view
    let dialog = OrderContextState.dialog
    let evaluatedAnswer ctx = Ok(OrderContextResponse.Evaluated ctx)
    let refusedAnswer ctx r = Ok(OrderContextResponse.Refused(ctx, r))

    testList
        "a refused answer"
        [
            test "an evaluated answer is shown" {
                transition
                    (OrderContextMsg.Answered("r-1", evaluatedAnswer paracetamol))
                    (evaluating paracetamol empty "r-1")
                |> Expect.equal "shown" (held paracetamol, [])
            }

            test "a refused answer keeps the picks, drops the scenarios, holds why, and tells nothing" {
                let busy = evaluating withOrder empty "r-1"

                transition
                    (OrderContextMsg.Answered("r-1", refusedAnswer withOrder OrderContextRefusal.NoDoseRules))
                    busy
                |> Expect.equal
                    "refused, as sent, without scenarios"
                    (refused paracetamol OrderContextRefusal.NoDoseRules, [])
            }

            test "a refused first evaluation holds the empty context and why" {
                transition
                    (OrderContextMsg.Answered("r-1", refusedAnswer empty OrderContextRefusal.NoProducts))
                    (opening patient "r-1")
                |> Expect.equal "the empty workbench, refused" (refused empty OrderContextRefusal.NoProducts, [])
            }

            test "the page shows the refusal while idle, a change while a request runs" {
                let shown = refused paracetamol OrderContextRefusal.NoDoseRulesForPatient

                shown
                |> view
                |> Expect.equal
                    "refused"
                    (OrderContextView.Refused(paracetamol, OrderContextRefusal.NoDoseRulesForPatient))

                let again = { paracetamol with OrderContext.Filter.Route = Some "or" }

                let busy, effects =
                    transition (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, again, "r-2")) shown

                effects |> Expect.equal "evaluated again" (evaluated again "r-2")
                busy
                |> view
                |> Expect.equal "changing meanwhile" (OrderContextView.Changing again)
            }

            test "the next evaluated answer clears the refusal" {
                let shown = refused paracetamol OrderContextRefusal.NoDoseRules
                let again = { paracetamol with OrderContext.Filter.Route = Some "or" }

                let busy, _ =
                    transition (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, again, "r-2")) shown

                transition (OrderContextMsg.Answered("r-2", evaluatedAnswer again)) busy
                |> Expect.equal "settled" (held again, [])
            }

            test "a failure after a refusal restores the context refused, the refusal gone" {
                let shown = refused paracetamol OrderContextRefusal.NoDoseRules
                let again = { paracetamol with OrderContext.Filter.Route = Some "or" }

                let busy, _ =
                    transition (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, again, "r-2")) shown

                transition (OrderContextMsg.Answered("r-2", Error [| "not loaded" |])) busy
                |> Expect.equal "as found, told" (held paracetamol, restored paracetamol [| "not loaded" |])
            }

            test "a patient change, a seed and a reset clear the refusal" {
                let shown = refused paracetamol OrderContextRefusal.NoDoseRules

                transition (OrderContextMsg.PatientChanged(Some other, "r-2")) shown
                |> fst
                |> view
                |> Expect.equal
                    "changing for the other patient"
                    (OrderContextView.Changing { paracetamol with Patient = other })

                transition (OrderContextMsg.Seed(empty, "r-2")) shown
                |> fst
                |> view
                |> Expect.equal "changing over the seed" (OrderContextView.Changing empty)

                transition (OrderContextMsg.Reset "r-2") shown
                |> fst
                |> view
                |> Expect.equal "changing over the empty context" (OrderContextView.Changing empty)

                transition (OrderContextMsg.PatientChanged(None, "r-2")) shown
                |> fst
                |> view
                |> Expect.equal "no patient" OrderContextView.NoPatient
            }

            test "a refusal drops the dialog's pending command and its selection" {
                let busy =
                    evaluating withOrder withOrder "r-1"
                    |> OrderContextState.select (Some "o-1")
                    |> OrderContextState.pending
                        OrderContextCommand.SetMedianOrderableDoseQuantityProperty
                        withOrder
                        "r-2"

                let landed, effects =
                    transition
                        (OrderContextMsg.Answered("r-1", refusedAnswer withOrder OrderContextRefusal.NoDoseRules))
                        busy

                effects |> Expect.isEmpty "nothing goes out"
                landed |> dialog |> Expect.equal "the dialog closed" None
                landed
                |> view
                |> Expect.equal "refused" (OrderContextView.Refused(paracetamol, OrderContextRefusal.NoDoseRules))
            }

            test "a failed change still keeps the context held and the order, and says why" {
                let busy = evaluating withOrder withOrder "r-1" |> OrderContextState.select (Some "o-1")

                transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) busy
                |> fst
                |> dialog
                |> Expect.equal "kept" (Some(OrderContextView.Settled withOrder))
            }

            test "a stale answer lands nowhere, refused or not" {
                let shown = held paracetamol

                transition (OrderContextMsg.Answered("r-9", refusedAnswer empty OrderContextRefusal.NoDoseRules)) shown
                |> Expect.equal "stale" (shown, [])
            }
        ]


/// The argumentation written on the workbench: the client's own, no request, kept over the
/// answer under way.
[<Tests>]
let argueTests =
    let text = "Sepsis, hogere dosis in overleg met de apotheek"
    let argued = paracetamol |> ArgumentationPolicy.write text

    testList
        "OrderContextMsg.Argue"
        [
            test "written on the context held, no effect; the view shows it" {
                let state, effects = held paracetamol |> transition (OrderContextMsg.Argue text)

                effects |> Expect.isEmpty "no call"
                state
                |> OrderContextState.view
                |> Expect.equal "settled with the text" (OrderContextView.Settled argued)
            }

            test
                "written while a request is under way: the one sent has it, the request is kept, and the answer keeps it" {
                let busy = evaluating paracetamol paracetamol "r-1"
                let state, effects = busy |> transition (OrderContextMsg.Argue text)

                effects |> Expect.isEmpty "no call"
                state
                |> OrderContextState.view
                |> Expect.equal "changing, with the text" (OrderContextView.Changing argued)

                // the answer was computed over the context without the text
                let landed, more =
                    state
                    |> transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated paracetamol)))

                more |> Expect.isEmpty "nothing more"
                landed
                |> OrderContextState.view
                |> Expect.equal "the text kept over the answer" (OrderContextView.Settled argued)

                // the same for a refusal
                let refused, _ =
                    state
                    |> transition (
                        OrderContextMsg.Answered(
                            "r-1",
                            Ok(OrderContextResponse.Refused(paracetamol, OrderContextRefusal.NoDoseRules))
                        )
                    )

                match refused |> OrderContextState.view with
                | OrderContextView.Refused(shown, _) ->
                    shown.Argumentation |> Expect.equal "kept on the refusal" (Some text)
                | other -> failtest $"expected refused, got %A{other}"
            }

            test "a seed evaluated after a text does not carry it: the text goes with the context sent" {
                let seed = { paracetamol with OrderContext.Filter.Generic = Some "ibuprofen" }

                let state, _ = held argued |> transition (OrderContextMsg.Seed(seed, "r-2"))

                let landed, _ =
                    state
                    |> transition (OrderContextMsg.Answered("r-2", Ok(OrderContextResponse.Evaluated seed)))

                landed
                |> OrderContextState.view
                |> Expect.equal "the seed, no text" (OrderContextView.Settled seed)
            }

            test "a reset takes the text with it as it goes out; a text written meanwhile survives its answer" {
                let resetting, effects =
                    held argued
                    |> transition (OrderContextMsg.Command(OrderContextCommand.ResetOrderScenario, argued, "r-1"))

                effects
                |> Expect.equal
                    "the context sent without the text"
                    [
                        OrderContextEffect.CallContext(OrderContextCommand.ResetOrderScenario, paracetamol, "r-1")
                    ]

                resetting
                |> OrderContextState.view
                |> Expect.equal "shown without the text meanwhile" (OrderContextView.Changing paracetamol)

                // the server echoes the text it was not sent: the answer keeps none
                let landed, _ =
                    resetting
                    |> transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated argued)))

                landed
                |> OrderContextState.view
                |> Expect.equal "cleared" (OrderContextView.Settled paracetamol)

                // a text written while the reset runs is the newer intent, and stays
                let newer = paracetamol |> ArgumentationPolicy.write "newer"

                let landed, _ =
                    resetting
                    |> transition (OrderContextMsg.Argue "newer")
                    |> fst
                    |> transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated paracetamol)))

                landed
                |> OrderContextState.view
                |> Expect.equal "the newer text kept" (OrderContextView.Settled newer)
            }

            test "blank clears the text; nothing without a patient" {
                let state, _ = held argued |> transition (OrderContextMsg.Argue "   ")

                state
                |> OrderContextState.view
                |> Expect.equal "cleared" (OrderContextView.Settled paracetamol)

                noPatient
                |> transition (OrderContextMsg.Argue text)
                |> Expect.equal "no patient, nothing" (noPatient, [])
            }
        ]


[<Tests>]
let reopenTests =
    let context id name =
        { paracetamol with
            Id = id
            Scenarios = [| OrderPlanMachineTests.Fixtures.scenario $"o-{id}" name |]
        }

    let c1 = context "c-1" "paracetamol"
    // the context as the dialog sends it with a field cleared, and as the server answers it
    let cleared = context "c-1" "paracetamol-cleared"
    let reopened = context "c-1" "paracetamol-reopened"

    let open' = held c1 |> OrderContextState.select (Some "o-c-1")

    let move = transition
    let run msgs state = msgs |> List.fold (fun s m -> move m s |> fst) state
    let view = OrderContextState.view

    let reopen = OrderContextMsg.Reopen(OrderContextCommand.ReopenOrderScenario [||], cleared, "r-1")

    let answered ctx = Ok(OrderContextResponse.Evaluated ctx)

    testList
        "OrderContextState.transition, a reopen and a restore"
        [
            test "a reopen sends the clear and shows the context sent" {
                let state, effects = open' |> move reopen

                effects
                |> Expect.equal
                    "the clear goes out"
                    [
                        OrderContextEffect.CallContext(
                            OrderContextCommand.ReopenOrderScenario [||],
                            { cleared with Patient = patient },
                            "r-1"
                        )
                    ]

                state |> view |> _.IsChanging |> Expect.isTrue "changing"
            }

            test "a restore before the answer puts the context back, and the late answer is dropped" {
                let restored = open' |> run [ reopen; OrderContextMsg.Restore ]

                restored |> Expect.equal "the state before the click" open'

                restored
                |> move (OrderContextMsg.Answered("r-1", answered reopened))
                |> Expect.equal "the answer finds no request" (open', [])
            }

            test "a restore after the answer puts the context back" {
                let answered = open' |> run [ reopen; OrderContextMsg.Answered("r-1", answered reopened) ]

                answered
                |> view
                |> Expect.equal "the list shows the answer" (OrderContextView.Settled reopened)

                answered
                |> move OrderContextMsg.Restore
                |> Expect.equal "the state before the click" (open', [])
            }

            test "a pick ends the look: a restore after it changes nothing" {
                let picked =
                    open'
                    |> run
                        [
                            reopen
                            OrderContextMsg.Answered("r-1", answered reopened)
                            OrderContextMsg.Command(OrderContextCommand.UpdateOrderScenario, reopened, "r-2")
                        ]

                picked
                |> move OrderContextMsg.Restore
                |> Expect.equal "nothing to put back" (picked, [])
            }

            test "a restore without a reopen changes nothing" {
                open'
                |> move OrderContextMsg.Restore
                |> Expect.equal "nothing to put back" (open', [])
            }

            test "a reopen while a request is under way keeps nothing: a restore cannot put that request back" {
                let busy =
                    open'
                    |> move (OrderContextMsg.Command(OrderContextCommand.IncreaseScheduleFrequencyProperty, c1, "r-1"))
                    |> fst

                let settled =
                    busy
                    |> run
                        [
                            OrderContextMsg.Reopen(OrderContextCommand.ReopenOrderScenario [||], cleared, "r-2")
                            OrderContextMsg.Answered("r-1", answered c1)
                            OrderContextMsg.Answered("r-2", answered reopened)
                        ]

                settled
                |> view
                |> Expect.equal "the clear answered" (OrderContextView.Settled reopened)

                settled
                |> move OrderContextMsg.Restore
                |> Expect.equal "nothing to put back, nothing in flight again" (settled, [])
            }
        ]


[<Tests>]
let specificCommandTests =
    let newCases =
        [
            OrderContextCommand.SetNthFilterProperty(Shared.Models.OrderContext.Generic, 0)
            OrderContextCommand.ClearFilterProperty Shared.Models.OrderContext.Route
            OrderContextCommand.ClearAllFilterProperty
            OrderContextCommand.SetNthDiluentProperty 0
            OrderContextCommand.ClearDiluentProperty
            OrderContextCommand.SetNthComponentsProperty [| 0 |]
            OrderContextCommand.SelectNthOrderScenario 0
            OrderContextCommand.SetNthScheduleProperty(ScheduleProperty.Frequency, 0)
            OrderContextCommand.ClearScheduleProperty(ScheduleProperty.Time, [| "pick" |])
            OrderContextCommand.SetNthOrderableProperty(OrderableProperty.DoseQuantity, 0)
            OrderContextCommand.ClearOrderableProperty(OrderableProperty.Quantity, [||])
            OrderContextCommand.SetNthComponentProperty("cmp", ComponentProperty.OrderableQuantity, 0)
            OrderContextCommand.ClearComponentProperty("cmp", ComponentProperty.OrderableQuantity, [||])
            OrderContextCommand.SetNthItemProperty("cmp", "itm", ItemProperty.DoseQuantity, 0)
            OrderContextCommand.ClearItemProperty("cmp", "itm", ItemProperty.DoseRate, [||])
        ]

    let withFrequency freq (sc: OrderScenario) =
        { sc with Order = { sc.Order with Schedule = { sc.Order.Schedule with Frequency = freq } } }

    let twoGenerics = { empty with OrderContext.Filter.Generics = [| "ibuprofen"; "paracetamol" |] }

    // a context whose one scenario offers two frequencies
    let twoFrequencies =
        let sc = OrderPlanMachineTests.Fixtures.scenario "o-1" "paracetamol"

        let freq =
            { sc.Order.Schedule.Frequency with
                OrderVariable.Variable.Vals =
                    Some
                        {
                            Value = [| "1", 1m; "2", 2m |]
                            Unit = "x/dag"
                            Group = ""
                            Short = false
                            Language = ""
                            Json = ""
                        }
            }

        { paracetamol with Scenarios = [| sc |> withFrequency freq |] }

    // the context as today's dialog sends a pick of the second frequency
    let secondFrequency =
        let sc = twoFrequencies.Scenarios[0]

        let freq =
            sc.Order.Schedule.Frequency
            |> Shared.Models.Order.OrderVariable.setOvar (Some "2")

        { twoFrequencies with Scenarios = [| sc |> withFrequency freq |] }

    let pickSecond = OrderContextCommand.SetNthScheduleProperty(ScheduleProperty.Frequency, 1)

    testList
        "the specific commands"
        [
            test "each waits and carries as the case it stands for" {
                for cmd in newCases do
                    let old = OrderContextCommand.replaced cmd |> Option.get

                    OrderPlanMachine.Dialog.waits cmd
                    |> Expect.equal $"%A{cmd} waits as %A{old}" (OrderPlanMachine.Dialog.waits old)
                    OrderPlanMachine.Dialog.carries cmd
                    |> Expect.equal $"%A{cmd} carries as %A{old}" (OrderPlanMachine.Dialog.carries old)
            }

            test "a command that cannot change the context shows it as it is" {
                twoGenerics
                |> OrderPlanMachine.Dialog.shown (
                    OrderContextCommand.SetNthFilterProperty(Shared.Models.OrderContext.Generic, 2)
                )
                |> Expect.equal "an index out of range" twoGenerics
            }

            test "a filter pick goes out as is, evaluates, and syncs the pages to the filter it makes" {
                let cmd = OrderContextCommand.SetNthFilterProperty(Shared.Models.OrderContext.Generic, 1)
                let picked = twoGenerics |> Shared.Models.OrderContext.medicationChange (Some "paracetamol")

                let state, effects = transition (OrderContextMsg.Command(cmd, twoGenerics, "r-1")) (held twoGenerics)

                effects
                |> Expect.equal
                    "the call with the context as it is, the syncs with the filter picked"
                    [
                        OrderContextEffect.CallContext(cmd, twoGenerics, "r-1")
                        OrderContextEffect.SyncFormulary picked.Filter
                        OrderContextEffect.SyncParenteralia picked.Filter
                    ]

                state
                |> OrderContextState.view
                |> Expect.equal "the pick shown while it runs" (OrderContextView.Changing picked)
            }

            test "a value pick goes out as is and shows as today's dialog sends it" {
                let state, effects =
                    transition (OrderContextMsg.Command(pickSecond, twoFrequencies, "r-1")) (held twoFrequencies)

                effects
                |> Expect.equal
                    "the call with the context as it is"
                    [ OrderContextEffect.CallContext(pickSecond, twoFrequencies, "r-1") ]

                state
                |> OrderContextState.context
                |> Expect.equal "the pick shown while it runs" (Some secondFrequency)
            }

            test "a pick that waits goes out over the context it was sent with" {
                let busy = inFlight OrderContextCommand.IncreaseScheduleFrequencyProperty paracetamol paracetamol "r-1"

                let waiting, _ = transition (OrderContextMsg.Command(pickSecond, twoFrequencies, "r-2")) busy

                transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated paracetamol))) waiting
                |> snd
                |> Expect.equal
                    "the pick over its own context"
                    [ OrderContextEffect.CallContext(pickSecond, twoFrequencies, "r-2") ]
            }

            test "a patient change during a pick evaluates the pick for the new patient" {
                let busy, _ =
                    transition (OrderContextMsg.Command(pickSecond, twoFrequencies, "r-1")) (held twoFrequencies)

                transition (OrderContextMsg.PatientChanged(Some other, "r-2")) busy
                |> snd
                |> Expect.equal
                    "the context picked, for the other patient"
                    (evaluated { secondFrequency with Patient = other } "r-2")
            }
        ]
