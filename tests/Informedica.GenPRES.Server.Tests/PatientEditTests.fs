/// An edit of the patient panel: GenCORE applies it to the patient data and the server completes the result with the
/// resources.
module Informedica.GenPRES.Server.Tests.PatientEditTests

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api
open ServerApi
open Informedica.GenPRES.Server.Tests.TestDates


module Models = Informedica.GenPRES.Shared.Models
module Measures = Informedica.GenPRES.Shared.Measures
module AgeOnRequest = Informedica.GenPRES.Server.Tests.AgeOnRequestTests


/// The stub patient of the EHR, ten years old, measured.
let stub = StubEhrPatient.contractPatient


/// A patient with an age in years only.
let aged years : PatientData =
    { Models.PatientData.empty with
        Age = Some { Models.PatientData.Age.ageZero with Years = years |> Measures.toYear }
    }


/// The edit applied to the patient through the server, with the resources. Fails the test on an error.
let edited edit pat =
    pat
    |> Patient.edit readAt (AgeOnRequest.loaded ()) edit
    |> Result.defaultWith (fun errs -> failtest $"%A{errs}")


[<Tests>]
let tests =
    testList
        "an edit of the patient panel"
        [
            test "a new age renews the estimated weight" {
                let five = aged 5 |> edited (PatientDataEdit.SetGender Male)
                let ten = five |> edited (PatientDataEdit.SetYears(Some 10))

                (five.Weight.Estimated, ten.Weight.Estimated)
                |> Expect.equal "18 kg at 5 years, 32 kg at 10 years" (Some 18000<gram>, Some 32000<gram>)
            }

            test "a weight given is measured and rounded to the calculation weights" {
                (aged 5 |> edited (PatientDataEdit.SetWeight(Some 12340<gram>))).Weight.Measured
                |> Expect.equal "12.5 kg" (Some 12500<gram>)
            }

            test "clearing a measured weight gives the estimate" {
                let w = (stub |> edited (PatientDataEdit.SetWeight None)).Weight
                (w.Measured, w.Estimated |> Option.isSome)
                |> Expect.equal "estimated" (None, true)
            }

            test "without the resources an edit keeps the estimates, as a change does" {
                let estimated = aged 5 |> edited (PatientDataEdit.SetGender Male)

                estimated
                |> Patient.edit readAt None (PatientDataEdit.SetDepartment(Some "ICK"))
                |> Result.map (fun p -> p.Weight.Estimated, p.Height.Estimated)
                |> Expect.equal "the estimates" (Ok(estimated.Weight.Estimated, estimated.Height.Estimated))
            }

            test "the adult mark is set for a patient of 17 and ignored for a patient of 10" {
                (aged 17 |> edited (PatientDataEdit.SetMarkedAdult true)).AdultMark
                |> Expect.equal "marked" (AdultMark.MarkableAdult true)

                (stub |> edited (PatientDataEdit.SetMarkedAdult true)).AdultMark
                |> Expect.equal "not markable" AdultMark.NotAdult
            }

            test "the gender, the renal function, the department and the access come back as edited" {
                let pat =
                    stub
                    |> edited (PatientDataEdit.SetGender Female)
                    |> edited (PatientDataEdit.SetRenalFunction(Some PeritonealDialysis))
                    |> edited (PatientDataEdit.SetDepartment(Some "ICK"))
                    |> edited (PatientDataEdit.AddAccess EnteralTube)

                (pat.Gender, pat.RenalFunction, pat.Department, pat.Access)
                |> Expect.equal "as edited" (Female, Some PeritonealDialysis, Some "ICK", stub.Access @ [ EnteralTube ])
            }

            test "the answer carries the options of the resources" {
                (aged 5 |> edited (PatientDataEdit.SetYears(Some 6))).Options
                |> Option.isSome
                |> Expect.isTrue "options"
            }

            test "an invalid edit is refused with the GenCORE message" {
                aged 5
                |> Patient.edit readAt (AgeOnRequest.loaded ()) (PatientDataEdit.SetMonths(Some 24))
                |> Result.isError
                |> Expect.isTrue "24 months refused"
            }

            test "a renal function that is not a band is refused with the message of a change" {
                let notABand = Some(EGFR(Some 20, Some 40))

                stub
                |> Patient.edit readAt (AgeOnRequest.loaded ()) (PatientDataEdit.SetRenalFunction notABand)
                |> Expect.equal
                    "the same refusal"
                    ({ stub with RenalFunction = notABand }
                     |> Patient.complete readAt (AgeOnRequest.loaded ()))

                stub
                |> Patient.edit readAt (AgeOnRequest.loaded ()) (PatientDataEdit.SetRenalFunction notABand)
                |> Result.isError
                |> Expect.isTrue "refused"
            }

            test "a renal function that is a band, and a clear, are edits" {
                let band = Some(EGFR(Some 10, Some 30))

                ((stub |> edited (PatientDataEdit.SetRenalFunction band)).RenalFunction,
                 (stub |> edited (PatientDataEdit.SetRenalFunction None)).RenalFunction)
                |> Expect.equal "the band, then none" (band, None)
            }

            test "an edit command needs the loaded resources, as a change does" {
                (PatientDataCommand.EditPatientData(PatientDataEdit.SetYears(Some 6), stub)
                 |> ServerApi.PatientDataCommand.gate,
                 PatientDataCommand.EditPatientData(PatientDataEdit.SetYears(Some 6), stub)
                 |> PatientDataCommand.toString)
                |> Expect.equal "gated and logged by name" (Gate.RequiresLoaded, "EditPatientData")
            }
        ]
