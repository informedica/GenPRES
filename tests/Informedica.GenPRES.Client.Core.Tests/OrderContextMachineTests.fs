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

    /// A patient change over the context: the call with the context as held, and the two syncs.
    let patientChanged pat (ctx: OrderContext) request =
        [
            OrderContextEffect.CallPatientChanged(pat, ctx, request)
            OrderContextEffect.SyncFormulary ctx.Filter
            OrderContextEffect.SyncParenteralia ctx.Filter
        ]

    /// A command that replaces any request: the call and the syncs of the filter it gives.
    let replacing cmd (ctx: OrderContext) request =
        let filter = (OrderPlanMachine.Dialog.shown cmd ctx).Filter

        [
            OrderContextEffect.CallContext(cmd, ctx, request)
            OrderContextEffect.SyncFormulary filter
            OrderContextEffect.SyncParenteralia filter
        ]

    /// The url's paracetamol.
    let urlSeed =
        {
            Source = SeedSource.Url
            Indication = None
            Generic = Some "paracetamol"
            Route = None
            Form = None
            DoseType = None
        }

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
                                OrderContextEffect.CallContext(OrderContextCommand.ClearAllFilterProperty, empty, "r-1")
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

                        effects
                        |> Expect.equal "the patient change and the syncs" (patientChanged other paracetamol "r-2")

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

                        effects
                        |> Expect.equal "the patient change and the syncs" (patientChanged other chosen "r-2")

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
                    test "a seed before a patient waits for it and is sent over the empty workbench" {
                        let waiting, effects = transition (OrderContextMsg.SeedFilter(urlSeed, "r-1")) noPatient

                        (OrderContextState.view waiting, effects)
                        |> Expect.equal "no workbench, nothing sent" (OrderContextView.NoPatient, [])

                        let emptyForOther = OrderContextState.emptyFor other
                        let cmd = FilterSeed.command urlSeed

                        transition (OrderContextMsg.PatientChanged(Some other, "r-2")) waiting
                        |> Expect.equal
                            "the seed sent for the patient, over the empty workbench"
                            (inFlightFor other cmd emptyForOther emptyForOther "r-2", replacing cmd emptyForOther "r-2")
                    }

                    test "a seed with a patient goes over the context held, and over the one shown during a request" {
                        let cmd = FilterSeed.command urlSeed

                        transition (OrderContextMsg.SeedFilter(urlSeed, "r-1")) shown
                        |> snd
                        |> Expect.equal "over the context held" (replacing cmd paracetamol "r-1")

                        let step = OrderContextCommand.IncreaseScheduleFrequencyProperty
                        let busy, _ = transition (OrderContextMsg.Command(step, "r-1")) shown

                        transition (OrderContextMsg.SeedFilter(urlSeed, "r-2")) busy
                        |> snd
                        |> Expect.equal
                            "over the context shown"
                            (replacing cmd (OrderPlanMachine.Dialog.shown step paracetamol) "r-2")
                    }

                    test "a seed waiting goes with a cleared patient" {
                        let waiting, _ = transition (OrderContextMsg.SeedFilter(urlSeed, "r-1")) noPatient

                        transition (OrderContextMsg.PatientChanged(None, "r-2")) waiting
                        |> fst
                        |> transition (OrderContextMsg.PatientChanged(Some patient, "r-3"))
                        |> fst
                        |> Expect.equal "the empty workbench opened, nothing waited" (opening patient "r-3")
                    }

                    test
                        "a seed during the first evaluation supersedes it; a failed one goes back to the empty workbench" {
                        let cmd = FilterSeed.command urlSeed

                        let seeded, effects =
                            transition (OrderContextMsg.SeedFilter(urlSeed, "r-2")) (opening patient "r-1")

                        seeded
                        |> Expect.equal "the seed sent over the empty workbench" (inFlight cmd empty empty "r-2")

                        effects |> Expect.equal "the call and the syncs" (replacing cmd empty "r-2")

                        transition
                            (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated paracetamol)))
                            seeded
                        |> Expect.equal "the first evaluation's answer is stale" (seeded, [])

                        transition (OrderContextMsg.Answered("r-2", Error [| "refused" |])) seeded
                        |> Expect.equal "back to the empty workbench" (held empty, restored empty [| "refused" |])
                    }

                    test "a command before a patient is dropped" {
                        transition (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, "r-1")) noPatient
                        |> Expect.equal "dropped" (noPatient, [])
                    }
                ]

            testList
                "a command"
                [
                    test "a filter pick goes over the context held and takes the pages along; a step calls alone" {
                        let pick = OrderContextCommand.SetNthFilterProperty(Shared.Models.OrderContext.Route, 0)

                        let busy, effects = transition (OrderContextMsg.Command(pick, "r-1")) shown

                        busy
                        |> Expect.equal "in flight over the context held" (inFlight pick paracetamol paracetamol "r-1")

                        effects
                        |> Expect.equal "the call and the syncs" (replacing pick paracetamol "r-1")

                        transition
                            (OrderContextMsg.Command(OrderContextCommand.IncreaseScheduleFrequencyProperty, "r-2"))
                            busy
                        |> Expect.equal "dropped while busy" (busy, [])

                        transition
                            (OrderContextMsg.Command(OrderContextCommand.IncreaseScheduleFrequencyProperty, "r-2"))
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

                    test "a refused step restores the context last evaluated" {
                        let busy, _ =
                            transition
                                (OrderContextMsg.Command(OrderContextCommand.IncreaseScheduleFrequencyProperty, "r-1"))
                                shown

                        busy
                        |> Expect.equal
                            "the step over the context held"
                            (inFlight
                                OrderContextCommand.IncreaseScheduleFrequencyProperty
                                paracetamol
                                paracetamol
                                "r-1")

                        transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) busy
                        |> Expect.equal "the last evaluated" (held paracetamol, restored paracetamol [| "not loaded" |])
                    }
                ]

            testList
                "the reset"
                [
                    test "the workbench's filter cleared for the patient held; nothing without a patient" {
                        let clear = OrderContextCommand.ClearAllFilterProperty
                        let state, effects = transition (OrderContextMsg.Reset "r-1") shown

                        state
                        |> Expect.equal
                            "clearing, back to the empty workbench on a failure"
                            (inFlight clear paracetamol empty "r-1")

                        effects
                        |> Expect.equal "the clear and the syncs" (replacing clear paracetamol "r-1")

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
let busyTests =
    let step = OrderContextCommand.IncreaseScheduleFrequencyProperty
    let busy = inFlight step paracetamol paracetamol "r-1"

    testList
        "a command while a request is under way"
        [
            test "is dropped, the dialog's and the page's alike" {
                for cmd in
                    [
                        step
                        OrderContextCommand.UpdateOrderScenario
                        OrderContextCommand.ResetOrderScenario
                        OrderContextCommand.SelectOrderScenario
                    ] do
                    transition (OrderContextMsg.Command(cmd, "r-2")) busy
                    |> Expect.equal $"%A{cmd} dropped, nothing sent" (busy, [])
            }

            test "the answer sends nothing more" {
                let answer = { paracetamol with OrderContext.Filter.Generics = [| "paracetamol"; "ibuprofen" |] }

                transition (OrderContextMsg.Command(step, "r-2")) busy
                |> fst
                |> transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated answer)))
                |> Expect.equal "the answer held, nothing sent" (held answer, [])
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

                OrderContextWorkbench.step landed (OrderContextWorkbench.NoPatient None)
                |> Expect.equal "no patient" (OrderContextWorkbench.NoPatient None, [])

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

                transition (OrderContextMsg.SeedFilter(urlSeed, "r-1")) selected
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

                let busy, effects =
                    transition (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, "r-2")) shown

                effects
                |> Expect.equal "the context refused, evaluated again" (evaluated paracetamol "r-2")
                busy
                |> view
                |> Expect.equal "changing meanwhile" (OrderContextView.Changing paracetamol)
            }

            test "the next evaluated answer clears the refusal" {
                let shown = refused paracetamol OrderContextRefusal.NoDoseRules
                let again = { paracetamol with OrderContext.Filter.Route = Some "or" }

                let busy, _ = transition (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, "r-2")) shown

                transition (OrderContextMsg.Answered("r-2", evaluatedAnswer again)) busy
                |> Expect.equal "settled" (held again, [])
            }

            test "a failure after a refusal restores the context refused, the refusal gone" {
                let shown = refused paracetamol OrderContextRefusal.NoDoseRules
                let again = { paracetamol with OrderContext.Filter.Route = Some "or" }

                let busy, _ = transition (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, "r-2")) shown

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

                transition (OrderContextMsg.SeedFilter(urlSeed, "r-2")) shown
                |> fst
                |> view
                |> Expect.equal
                    "changing over the seed"
                    (OrderContextView.Changing(OrderPlanMachine.Dialog.shown (FilterSeed.command urlSeed) paracetamol))

                transition (OrderContextMsg.Reset "r-2") shown
                |> fst
                |> view
                |> Expect.equal "changing over the empty context" (OrderContextView.Changing empty)

                transition (OrderContextMsg.PatientChanged(None, "r-2")) shown
                |> fst
                |> view
                |> Expect.equal "no patient" OrderContextView.NoPatient
            }

            test "a refusal drops the dialog's selection" {
                let busy = evaluating withOrder withOrder "r-1" |> OrderContextState.select (Some "o-1")

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


/// The argumentation on the workbench: a command like any other, written by the server.
[<Tests>]
let argueTests =
    let text = "Sepsis, hogere dosis in overleg met de apotheek"
    let argued = paracetamol |> Shared.Models.OrderContext.Argumentation.write text
    let argue = OrderContextCommand.SetArgumentationProperty text

    testList
        "the argumentation"
        [
            test "goes as a command over the context held, shown with the text meanwhile" {
                let state, effects = held paracetamol |> transition (OrderContextMsg.Command(argue, "r-1"))

                effects
                |> Expect.equal "the call" [ OrderContextEffect.CallContext(argue, paracetamol, "r-1") ]

                state
                |> OrderContextState.view
                |> Expect.equal "changing, with the text" (OrderContextView.Changing argued)

                state
                |> transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated argued)))
                |> Expect.equal "the answer as the server wrote it" (held argued, [])
            }

            test "a reset goes over the context held, text and all; the answer has it cleared" {
                let reset = OrderContextCommand.ResetOrderScenario
                let state, effects = held argued |> transition (OrderContextMsg.Command(reset, "r-1"))

                effects
                |> Expect.equal "the context held sent" [ OrderContextEffect.CallContext(reset, argued, "r-1") ]

                state
                |> transition (OrderContextMsg.Answered("r-1", Ok(OrderContextResponse.Evaluated paracetamol)))
                |> Expect.equal "the answer without the text" (held paracetamol, [])
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
    // the context as the server answers a clear
    let reopened = context "c-1" "paracetamol-reopened"

    let open' = held c1 |> OrderContextState.select (Some "o-c-1")

    let move = transition
    let run msgs state = msgs |> List.fold (fun s m -> move m s |> fst) state
    let view = OrderContextState.view

    let reopen = OrderContextMsg.Reopen(OrderContextCommand.ReopenOrderScenario [||], "r-1")

    let answered ctx = Ok(OrderContextResponse.Evaluated ctx)

    testList
        "OrderContextState.transition, a reopen and a restore"
        [
            test "a reopen sends the clear over the context held and shows it changing" {
                let state, effects = open' |> move reopen

                effects
                |> Expect.equal
                    "the clear goes out"
                    [
                        OrderContextEffect.CallContext(
                            OrderContextCommand.ReopenOrderScenario [||],
                            { c1 with Patient = patient },
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
                            OrderContextMsg.Command(OrderContextCommand.UpdateOrderScenario, "r-2")
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

            test "a reopen while a request is under way is dropped and keeps nothing" {
                let busy =
                    open'
                    |> move (OrderContextMsg.Command(OrderContextCommand.IncreaseScheduleFrequencyProperty, "r-1"))
                    |> fst

                busy
                |> move (OrderContextMsg.Reopen(OrderContextCommand.ReopenOrderScenario [||], "r-2"))
                |> Expect.equal "dropped, nothing sent" (busy, [])

                let settled = busy |> run [ OrderContextMsg.Answered("r-1", answered c1) ]

                settled
                |> move OrderContextMsg.Restore
                |> Expect.equal "nothing to put back" (settled, [])
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

                let state, effects = transition (OrderContextMsg.Command(cmd, "r-1")) (held twoGenerics)

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
                let state, effects = transition (OrderContextMsg.Command(pickSecond, "r-1")) (held twoFrequencies)

                effects
                |> Expect.equal
                    "the call with the context as it is"
                    [ OrderContextEffect.CallContext(pickSecond, twoFrequencies, "r-1") ]

                state
                |> OrderContextState.context
                |> Expect.equal "the pick shown while it runs" (Some secondFrequency)
            }

            test "a patient change during a pick evaluates the pick for the new patient" {
                let busy, _ = transition (OrderContextMsg.Command(pickSecond, "r-1")) (held twoFrequencies)

                transition (OrderContextMsg.PatientChanged(Some other, "r-2")) busy
                |> snd
                |> Expect.equal "the context picked, for the other patient" (patientChanged other secondFrequency "r-2")
            }
        ]


/// The workbench during a patient change: shown as a change under way, and a command from the page
/// dropped, so that nothing is ordered for the patient being replaced.
[<Tests>]
let patientChangeTests =
    let noPatientChange = PatientMachine.PatientState.init (Some draft)

    let patientChanging =
        noPatientChange
        |> PatientMachine.PatientState.transition (
            PatientMachine.PatientMsg.Changed(Some otherDraft, PatientDraftPolicy.Estimates.Kept, "p-1")
        )
        |> fst

    testList
        "the workbench during a patient change"
        [
            test "the context held shows as a change under way, and settles when the change is done" {
                (shown |> OrderContextState.viewWhile patientChanging,
                 shown |> OrderContextState.viewWhile noPatientChange)
                |> Expect.equal
                    "changing, then settled"
                    (OrderContextView.Changing paracetamol, OrderContextView.Settled paracetamol)
            }

            test "a command from the page is dropped; the patient change itself reaches the workbench" {
                shown
                |> OrderContextState.transitionWhile
                    patientChanging
                    (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, "r-1"))
                |> Expect.equal "the command is dropped" (shown, [])

                OrderContextMsg.PatientChanged(Some other, "r-1")
                |> OrderContextState.admitted patientChanging
                |> Expect.isTrue "the patient admitted"
            }
        ]
