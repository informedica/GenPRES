module Informedica.GenPRES.Shared.Tests.OrderContextMachineTests

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Api
open OrderContextMachine


module Fixtures =

    let patient = Shared.Models.Patient.empty
    let other = { patient with Department = Some "other" }

    let empty = OrderContextState.emptyFor patient

    let paracetamol = { empty with OrderContext.Filter.Generic = Some "paracetamol" }

    let noPatient = OrderContextState.noPatient
    let seeded = OrderContextState.seeded

    /// The context held, nothing under way.
    let held (ctx: OrderContext) =
        {
            Workbench = Workbench.Evaluated ctx
            InFlight = None
        }

    /// A command under way over the context sent; the one held is what a failed change goes back to.
    let inFlight cmd (sent: OrderContext) (found: OrderContext) request =
        {
            Workbench = Workbench.Evaluated found
            InFlight = Some((cmd, sent), request)
        }

    /// An evaluation under way.
    let evaluating = inFlight OrderContextCommand.UpdateOrderContext

    /// The first evaluation for the patient under way: nothing held yet.
    let opening (pat: Patient) request =
        {
            Workbench = Workbench.Unevaluated pat
            InFlight = Some((OrderContextCommand.UpdateOrderContext, OrderContextState.emptyFor pat), request)
        }

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

                        let state, effects =
                            transition (OrderContextMsg.PatientChanged(Some other, "r-2")) busy

                        let expected = { paracetamol with Patient = other }

                        state
                        |> Expect.equal "evaluating for the new patient" (evaluating expected expected "r-2")

                        effects |> Expect.equal "the call and the syncs" (evaluated expected "r-2")

                        transition (OrderContextMsg.Answered("r-1", Ok paracetamol)) state
                        |> Expect.equal "the older answer dropped" (state, [])
                    }

                    test
                        "a patient changed while a selection is in flight keeps the selection, and what a refusal restores" {
                        let chosen = { paracetamol with OrderContext.Filter.Generic = Some "ibuprofen" }
                        let busy = evaluating chosen paracetamol "r-1"

                        let state, effects =
                            transition (OrderContextMsg.PatientChanged(Some other, "r-2")) busy

                        let sent = { chosen with Patient = other }
                        let found = { paracetamol with Patient = other }

                        state
                        |> Expect.equal "the selection evaluated for the new patient" (evaluating sent found "r-2")

                        effects |> Expect.equal "the call and the syncs" (evaluated sent "r-2")

                        transition (OrderContextMsg.Answered("r-2", Error [| "not loaded" |])) state
                        |> Expect.equal
                            "a refusal restores the last evaluated, for the new patient"
                            (held found, restored found [| "not loaded" |])
                    }

                    test "no patient: no workbench; a seed keeps waiting" {
                        transition (OrderContextMsg.PatientChanged(None, "r-1")) shown
                        |> Expect.equal "no patient" (noPatient, [])

                        transition (OrderContextMsg.PatientChanged(None, "r-1")) (seeded paracetamol)
                        |> Expect.equal "the seed waits" (seeded paracetamol, [])
                    }
                ]

            testList
                "the seed"
                [
                    test "a filter before a patient waits, and is evaluated once the patient is set" {
                        let waiting, effects =
                            transition (OrderContextMsg.Seed(paracetamol, "r-1")) noPatient

                        waiting |> Expect.equal "seeded" (seeded paracetamol)
                        effects |> Expect.isEmpty "nothing to evaluate yet"

                        let state, effects =
                            transition (OrderContextMsg.PatientChanged(Some patient, "r-2")) waiting

                        state
                        |> Expect.equal "evaluated for the patient" (evaluating paracetamol paracetamol "r-2")

                        effects |> Expect.equal "the call and the syncs" (evaluated paracetamol "r-2")
                    }

                    test "a filter with a patient held is evaluated at once, for that patient" {
                        let fromUrl = { paracetamol with Patient = other }
                        let state, effects = transition (OrderContextMsg.Seed(fromUrl, "r-1")) shown

                        state |> Expect.equal "evaluating" (evaluating paracetamol paracetamol "r-1")

                        effects |> Expect.equal "for the patient held" (evaluated paracetamol "r-1")
                    }

                    test "a command before a patient is the seed" {
                        transition
                            (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, paracetamol, "r-1"))
                            noPatient
                        |> Expect.equal "seeded" (seeded paracetamol, [])
                    }
                ]

            testList
                "a command"
                [
                    test "an update takes the formulary and the parenteralia along; a step calls alone; one at a time" {
                        let changed =
                            { paracetamol with
                                OrderContext.Filter.Generic = Some "ibuprofen"
                                Patient = other
                            }

                        let forPatient = { changed with Patient = patient }

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
                        |> Expect.equal "dropped while busy" (busy, [])

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

                        let errs =
                            [|
                                "Geen doseerregels gevonden voor het geselecteerde filter"
                            |]

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
let projectionTests =
    testList
        "OrderContextState.toDeferred"
        [
            test "no patient: not started; a seed and the context held: resolved" {
                noPatient
                |> OrderContextState.toDeferred
                |> Expect.equal "not started" HasNotStartedYet

                seeded paracetamol
                |> OrderContextState.toDeferred
                |> Expect.equal "the seed, shown while it waits" (Resolved paracetamol)

                shown
                |> OrderContextState.toDeferred
                |> Expect.equal "the context held" (Resolved paracetamol)
            }

            test "the first evaluation: in progress, nothing to show; a change under way: the context sent" {
                opening patient "r-1"
                |> OrderContextState.toDeferred
                |> Expect.equal "in progress" InProgress

                let stepped = { paracetamol with OrderContext.Filter.Route = Some "stepped" }

                inFlight OrderContextCommand.IncreaseScheduleFrequencyProperty stepped paracetamol "r-1"
                |> OrderContextState.toDeferred
                |> Expect.equal "the context sent" (Recalculating stepped)
            }
        ]


[<Tests>]
let stagesTests =
    testList
        "the two stages"
        [
            test "the workbench alone knows no request: a failed change keeps the context held" {
                let stepped = { paracetamol with OrderContext.Filter.Route = Some "stepped" }

                Workbench.step
                    (WorkbenchMsg.Landed((OrderContextCommand.UpdateOrderContext, stepped), Error [| "not loaded" |]))
                    (Workbench.Evaluated paracetamol)
                |> Expect.equal
                    "the original, told and synced"
                    (Workbench.Evaluated paracetamol,
                     [
                         WorkbenchIntent.Tell [| "not loaded" |]
                         WorkbenchIntent.Sync paracetamol.Filter
                     ])
            }

            test "nothing lands where nothing was asked: a seed, or no patient" {
                let landed =
                    WorkbenchMsg.Landed((OrderContextCommand.UpdateOrderContext, paracetamol), Ok paracetamol)

                Workbench.step landed (Workbench.Seeded paracetamol)
                |> Expect.equal "the seed" (Workbench.Seeded paracetamol, [])

                Workbench.step landed Workbench.NoPatient
                |> Expect.equal "no patient" (Workbench.NoPatient, [])

                transition (OrderContextMsg.Answered("r-1", Ok paracetamol)) (seeded paracetamol)
                |> Expect.equal "no request under way to land on" (seeded paracetamol, [])
            }

            test
                "the context shown is the one sent while a change is under way, else the one held; none before the first evaluation" {
                let stepped = { paracetamol with OrderContext.Filter.Route = Some "stepped" }

                let busy =
                    inFlight OrderContextCommand.IncreaseScheduleFrequencyProperty stepped paracetamol "r-1"

                busy |> OrderContextState.context |> Expect.equal "the one sent" (Some stepped)

                busy
                |> OrderContextState.patient
                |> Expect.equal "the patient held" (Some patient)

                shown
                |> OrderContextState.context
                |> Expect.equal "the one held" (Some paracetamol)

                opening patient "r-1"
                |> OrderContextState.context
                |> Expect.equal "none yet" None
            }
        ]
