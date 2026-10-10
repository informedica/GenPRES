module Informedica.GenPRES.Client.Core.Tests.PatientMachineTests

open Expecto
open Expecto.Flip
open Shared.Types
open PatientMachine


module Fixtures =

    let ten = { Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }

    /// An age makes the draft a patient.
    let draft = { Shared.Models.Patient.empty with Age = Some ten }

    /// The patient the server answers: the draft with an estimated weight and height.
    let answered =
        { draft with
            Weight = { draft.Weight with Estimated = Some 32000<gram> }
            Height = { draft.Height with Estimated = Some 140<cm> }
        }

    /// Below the minimum: no age, no measured weight and height.
    let below = Shared.Models.Patient.empty

    let renewed = PatientDraftPolicy.Estimates.Renewed
    let kept = PatientDraftPolicy.Estimates.Kept

    let none = PatientState.init None

    let transition = PatientState.transition

    /// A change under way for the draft.
    let changing estimates request =
        transition (PatientMsg.Changed(Some draft, estimates, request)) none |> fst


open Fixtures


[<Tests>]
let tests =
    testList
        "PatientState.transition"
        [
            test "a change is sent, the draft kept for the panel" {
                let state, effects = transition (PatientMsg.Changed(Some draft, renewed, "r-1")) none

                (PatientState.draft state, PatientState.inFlightRequest state, effects)
                |> Expect.equal
                    "the draft, awaiting r-1, the call"
                    (Some draft, Some "r-1", [ PatientEffect.CallPatient(draft, "r-1") ])
            }

            test "a draft below the minimum is no patient: nothing sent, the patient cleared" {
                let state, effects =
                    transition (PatientMsg.Changed(Some below, renewed, "r-2")) (changing renewed "r-1")

                (PatientState.draft state, PatientState.inFlightRequest state, effects)
                |> Expect.equal
                    "the draft, nothing awaited, the patient cleared"
                    (Some below, None, [ PatientEffect.SetPatientData None ])
            }

            test "after an edit that renews the estimates the draft takes the answered patient" {
                let state, effects = transition (PatientMsg.Answered("r-1", Ok answered)) (changing renewed "r-1")

                (PatientState.draft state, PatientState.inFlightRequest state, effects)
                |> Expect.equal
                    "the answer on the panel and on the rest"
                    (Some answered, None, [ PatientEffect.SetPatientData(Some answered) ])
            }

            test "after any other edit the draft is kept, the answered patient goes to the rest" {
                let state, effects = transition (PatientMsg.Answered("r-1", Ok answered)) (changing kept "r-1")

                (PatientState.draft state, effects)
                |> Expect.equal "the draft as typed" (Some draft, [ PatientEffect.SetPatientData(Some answered) ])
            }

            test "an answered patient without a weight or a height is held back, the pages keep theirs" {
                let unestimated = { draft with Department = Some "unestimated" }

                let state, effects =
                    transition (PatientMsg.Answered("r-1", Ok answered)) (changing renewed "r-1")
                    |> fst
                    |> transition (PatientMsg.Changed(Some unestimated, renewed, "r-2"))
                    |> fst
                    |> transition (PatientMsg.Answered("r-2", Ok unestimated))

                (PatientState.draft state, PatientState.answered state, PatientState.changing state, effects)
                |> Expect.equal
                    "the answer on the panel, the earlier patient kept, nothing set"
                    (Some unestimated, Some answered, false, [])
            }

            test "an answer to an earlier change is dropped" {
                let newer =
                    transition (PatientMsg.Changed(Some draft, kept, "r-2")) (changing renewed "r-1")
                    |> fst

                transition (PatientMsg.Answered("r-1", Ok answered)) newer
                |> Expect.equal "unchanged, nothing done" (newer, [])
            }

            test "changing while a change is under way, not once it is answered or failed" {
                [
                    none
                    changing renewed "r-1"
                    transition (PatientMsg.Answered("r-1", Ok answered)) (changing renewed "r-1")
                    |> fst
                    transition (PatientMsg.Answered("r-1", Error [| "failed" |])) (changing renewed "r-1")
                    |> fst
                ]
                |> List.map PatientState.changing
                |> Expect.equal "only under way" [ false; true; false; false ]
            }

            test "the answered patient: set by an answer, kept by a failure, cleared below the minimum" {
                let answeredOnce =
                    transition (PatientMsg.Answered("r-1", Ok answered)) (changing kept "r-1")
                    |> fst

                [
                    none
                    answeredOnce
                    answeredOnce
                    |> transition (PatientMsg.Changed(Some draft, kept, "r-2"))
                    |> fst
                    |> transition (PatientMsg.Answered("r-2", Error [| "failed" |]))
                    |> fst
                    answeredOnce |> transition (PatientMsg.Changed(Some below, kept, "r-2")) |> fst
                ]
                |> List.map PatientState.answered
                |> Expect.equal "none, the answer, the answer kept, none" [ None; Some answered; Some answered; None ]
            }

            test "a failure is told, and the draft the change started from is put back" {
                let older = { draft with Department = Some "older" }

                let state, effects =
                    PatientState.init (Some older)
                    |> transition (PatientMsg.Changed(Some draft, renewed, "r-1"))
                    |> fst
                    |> transition (PatientMsg.Answered("r-1", Error [| "failed" |]))

                (PatientState.draft state, PatientState.inFlightRequest state, effects)
                |> Expect.equal
                    "the draft before the edit, nothing awaited, told"
                    (Some older, None, [ PatientEffect.TellError [| "failed" |] ])
            }
        ]
