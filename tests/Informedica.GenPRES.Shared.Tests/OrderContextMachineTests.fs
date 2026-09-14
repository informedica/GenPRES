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

    let shown = OrderContextState.Shown paracetamol

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
                            transition (OrderContextMsg.PatientChanged(Some patient, "r-1")) OrderContextState.NoPatient

                        loading |> Expect.equal "loading" (OrderContextState.Loading(patient, "r-1"))

                        effects
                        |> Expect.equal
                            "the empty workbench evaluated"
                            [
                                OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, empty, "r-1")
                            ]

                        transition (OrderContextMsg.Answered("r-1", Ok paracetamol)) loading
                        |> Expect.equal "shown" (OrderContextState.Shown paracetamol, [])
                    }

                    test
                        "a patient changed keeps the filter and evaluates it for the new patient, whatever was in flight superseded" {
                        let busy = OrderContextState.Recalculating(paracetamol, "r-1")

                        let state, effects =
                            transition (OrderContextMsg.PatientChanged(Some other, "r-2")) busy

                        let expected = { paracetamol with Patient = other }

                        state
                        |> Expect.equal
                            "evaluating for the new patient"
                            (OrderContextState.Recalculating(expected, "r-2"))

                        effects |> Expect.equal "the call and the syncs" (evaluated expected "r-2")

                        transition (OrderContextMsg.Answered("r-1", Ok paracetamol)) state
                        |> Expect.equal "the older answer dropped" (state, [])
                    }

                    test "no patient: no workbench; a seed keeps waiting" {
                        transition (OrderContextMsg.PatientChanged(None, "r-1")) shown
                        |> Expect.equal "no patient" (OrderContextState.NoPatient, [])

                        transition (OrderContextMsg.PatientChanged(None, "r-1")) (OrderContextState.Seeded paracetamol)
                        |> Expect.equal "the seed waits" (OrderContextState.Seeded paracetamol, [])
                    }
                ]

            testList
                "the seed"
                [
                    test "a filter before a patient waits, and is evaluated once the patient is set" {
                        let seeded, effects =
                            transition (OrderContextMsg.Seed(paracetamol, "r-1")) OrderContextState.NoPatient

                        seeded |> Expect.equal "seeded" (OrderContextState.Seeded paracetamol)
                        effects |> Expect.isEmpty "nothing to evaluate yet"

                        let state, effects =
                            transition (OrderContextMsg.PatientChanged(Some patient, "r-2")) seeded

                        state
                        |> Expect.equal
                            "evaluated for the patient"
                            (OrderContextState.Recalculating(paracetamol, "r-2"))

                        effects |> Expect.equal "the call and the syncs" (evaluated paracetamol "r-2")
                    }

                    test "a filter with a patient held is evaluated at once, for that patient" {
                        let fromUrl = { paracetamol with Patient = other }
                        let state, effects = transition (OrderContextMsg.Seed(fromUrl, "r-1")) shown

                        state
                        |> Expect.equal "evaluating" (OrderContextState.Recalculating(paracetamol, "r-1"))

                        effects |> Expect.equal "for the patient held" (evaluated paracetamol "r-1")
                    }

                    test "a command before a patient is the seed" {
                        transition
                            (OrderContextMsg.Command(OrderContextCommand.UpdateOrderContext, paracetamol, "r-1"))
                            OrderContextState.NoPatient
                        |> Expect.equal "seeded" (OrderContextState.Seeded paracetamol, [])
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
                        |> Expect.equal
                            "in flight, for the patient held"
                            (OrderContextState.Recalculating(forPatient, "r-1"))

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
                            (OrderContextState.Recalculating(paracetamol, "r-2"),
                             [
                                 OrderContextEffect.CallContext(
                                     OrderContextCommand.IncreaseScheduleFrequencyProperty,
                                     paracetamol,
                                     "r-2"
                                 )
                             ])
                    }

                    test "the answer lands on its request; a stale one is dropped" {
                        let busy = OrderContextState.Recalculating(paracetamol, "r-1")

                        let answer =
                            { paracetamol with OrderContext.Filter.Generics = [| "paracetamol"; "ibuprofen" |] }

                        transition (OrderContextMsg.Answered("r-1", Ok answer)) busy
                        |> Expect.equal "shown" (OrderContextState.Shown answer, [])

                        transition (OrderContextMsg.Answered("r-9", Ok answer)) busy
                        |> Expect.equal "stale" (busy, [])
                    }

                    test "a refused command leaves the workbench as the request found it, and says why" {
                        let busy = OrderContextState.Recalculating(paracetamol, "r-1")

                        transition (OrderContextMsg.Answered("r-1", Error [| "not loaded" |])) busy
                        |> Expect.equal
                            "as found"
                            (OrderContextState.Shown paracetamol, [ OrderContextEffect.TellError [| "not loaded" |] ])

                        transition
                            (OrderContextMsg.Answered("r-1", Error [| "not loaded" |]))
                            (OrderContextState.Loading(patient, "r-1"))
                        |> Expect.equal
                            "the empty workbench"
                            (OrderContextState.Shown empty, [ OrderContextEffect.TellError [| "not loaded" |] ])
                    }

                    test "no dose rules for the filter: back to the first page, the empty workbench evaluated again" {
                        let busy = OrderContextState.Recalculating(paracetamol, "r-1")

                        let errs =
                            [|
                                "Geen doseerregels gevonden voor het geselecteerde filter"
                            |]

                        transition (OrderContextMsg.Answered("r-1", Error errs)) busy
                        |> Expect.equal
                            "left, told, evaluated empty"
                            (OrderContextState.Loading(patient, "r-1"),
                             [
                                 OrderContextEffect.GoToLifeSupport
                                 OrderContextEffect.TellError errs
                                 OrderContextEffect.CallContext(OrderContextCommand.UpdateOrderContext, empty, "r-1")
                             ])
                    }
                ]

            testList
                "the reset"
                [
                    test "the workbench cleared for the patient held and evaluated empty; nothing without a patient" {
                        let state, effects = transition (OrderContextMsg.Reset "r-1") shown

                        state
                        |> Expect.equal "evaluating the empty workbench" (OrderContextState.Recalculating(empty, "r-1"))

                        effects |> Expect.equal "the call and the syncs" (evaluated empty "r-1")

                        transition (OrderContextMsg.Reset "r-1") OrderContextState.NoPatient
                        |> Expect.equal "nothing" (OrderContextState.NoPatient, [])
                    }
                ]
        ]
