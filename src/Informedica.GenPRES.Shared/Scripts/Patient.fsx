// The patient a draft becomes. A `PatientDto` is what the wire carries and the panel edits, every
// field optional; a `Patient` exists only with an age, or a measured weight and a measured
// height, which is what a workbench, a plan and an evaluation are for. The type keeps its
// representation private, so `Patient.fromDto` is the one way in and `Patient.toDto` the way
// back to the wire.
//
// Script-first draft (script-only policy), → `Shared/Models.fs`, before the `Patient` module,
// whose readers stay on the draft; the type abbreviation of the rename steps goes. The type is
// drafted here under a suffix next to the loaded originals; the tests pin the minimum and the
// round trip.
//
// Run: `dotnet fsi Patient.fsx` from this directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Models


/// → `Shared/Models.fs`: a draft that meets the minimum; built by `fromDto` only.
type Patient646 = private { Dto: PatientDto }


/// → `Shared/Models.fs`: why a draft is not a patient.
[<RequireQualifiedAccess>]
type PatientError646 =
    // neither an age, nor a measured weight and a measured height
    | NoAgeOrMeasuredWeightAndHeight


/// → `Shared/Models.fs`, into the `Patient` module.
module Patient646 =

    /// The patient a draft becomes: with an age, the rest can be estimated; without one, a
    /// measured weight and a measured height are needed.
    let fromDto (dto: PatientDto) : Result<Patient646, PatientError646> =
        if dto.Age.IsSome || (dto.Weight.Measured.IsSome && dto.Height.Measured.IsSome) then
            Ok { Dto = dto }
        else
            Error PatientError646.NoAgeOrMeasuredWeightAndHeight


    /// The patient's data as the wire carries it.
    let toDto (p: Patient646) = p.Dto


let ten = { Patient.Age.ageZero with Age.Years = 10<year> }
let measured (w: int<gram>) (h: int<cm>) (dto: PatientDto) =
    { dto with
        PatientDto.Weight.Measured = Some w
        PatientDto.Height.Measured = Some h
    }


let tests =
    testList "patient from a draft" [
        test "an age alone is a patient" {
            { PatientDto.empty with Age = Some ten }
            |> Patient646.fromDto
            |> Result.isOk
            |> Expect.isTrue "a patient"
        }

        test "a measured weight and height without an age is a patient" {
            PatientDto.empty
            |> measured 32000<gram> 140<cm>
            |> Patient646.fromDto
            |> Result.isOk
            |> Expect.isTrue "a patient"
        }

        test "a weight alone is not: the height is needed too" {
            { PatientDto.empty with PatientDto.Weight.Measured = Some 32000<gram> }
            |> Patient646.fromDto
            |> Expect.equal "no patient" (Error PatientError646.NoAgeOrMeasuredWeightAndHeight)
        }

        test "an estimated weight and height do not count: the estimate follows an age" {
            { PatientDto.empty with
                PatientDto.Weight.Estimated = Some 32000<gram>
                PatientDto.Height.Estimated = Some 140<cm>
            }
            |> Patient646.fromDto
            |> Expect.equal "no patient" (Error PatientError646.NoAgeOrMeasuredWeightAndHeight)
        }

        test "the blank draft is not a patient" {
            PatientDto.empty
            |> Patient646.fromDto
            |> Expect.equal "no patient" (Error PatientError646.NoAgeOrMeasuredWeightAndHeight)
        }

        test "to the wire and back is the draft it came from" {
            let dto =
                { PatientDto.empty with Age = Some ten; Department = Some "ICK" }
                |> measured 32000<gram> 140<cm>

            dto
            |> Patient646.fromDto
            |> Result.map Patient646.toDto
            |> Expect.equal "the same draft" (Ok dto)
        }
    ]


runTestsWithCLIArgs [] [||] tests
