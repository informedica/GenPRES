// The estimate stays an estimate. The normal-value tables give a draft with an age an estimated
// weight and height; today the estimate is also written into `Measured` when nothing was
// measured, so a value the user never entered reads as entered, and a gender change wipes the
// measured values along with the estimates. After this change `Measured` holds what was entered
// or read from the platform and nothing else; the readers (`getWeight`, `getHeight`, the BSA)
// already fall back to the estimate, so what is calculated does not change.
//
// Script-first draft (script-only policy), → `Shared/Models.fs`, the `PatientDto` module: the
// tail of `applyNormalValues` without the promotion, and `setGender` moved out of the panel so
// that it can be tested. The tests pin both.
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


/// → `Shared/Models.fs`, `PatientDto`.
module PatientDto646 =

    /// The estimates written, the measured values left as they are.
    let withEstimates
        (ew: (int<gram> * int<gram> * int<gram>) option)
        (eh: (int<cm> * int<cm> * int<cm>) option)
        (pat: PatientDto)
        =
        { pat with
            Weight =
                { pat.Weight with
                    EstimatedP3 = ew |> Option.map (fun (p3, _, _) -> p3)
                    Estimated = ew |> Option.map (fun (_, m, _) -> m)
                    EstimatedP97 = ew |> Option.map (fun (_, _, p97) -> p97)
                }
            Height =
                { pat.Height with
                    EstimatedP3 = eh |> Option.map (fun (p3, _, _) -> p3)
                    Estimated = eh |> Option.map (fun (_, m, _) -> m)
                    EstimatedP97 = eh |> Option.map (fun (_, _, p97) -> p97)
                }
        }


    /// The gender chosen: the estimates go, since they follow the gender; the measured values
    /// stay, since they do not.
    let setGender (s: string) (p: PatientDto option) : PatientDto option =
        let gender =
            match s with
            | "male" -> Male
            | "female" -> Female
            | _ -> UnknownGender

        p
        |> Option.defaultValue PatientDto.empty
        |> withEstimates None None
        |> fun p -> { p with Gender = gender }
        |> Some


let ten = { Patient.Age.ageZero with Age.Years = 10<year> }
let estimates = Some(25000<gram>, 32000<gram>, 40000<gram>), Some(130<cm>, 140<cm>, 150<cm>)


let tests =
    testList "the estimate stays an estimate" [
        test "nothing measured: the estimate is written, the measured value stays empty" {
            let dto =
                { PatientDto.empty with Age = Some ten }
                |> PatientDto646.withEstimates (fst estimates) (snd estimates)

            (dto.Weight.Estimated, dto.Weight.Measured, dto.Height.Estimated, dto.Height.Measured)
            |> Expect.equal "estimated, not measured" (Some 32000<gram>, None, Some 140<cm>, None)
        }

        test "a measured weight survives the estimate" {
            let dto =
                { PatientDto.empty with
                    Age = Some ten
                    PatientDto.Weight.Measured = Some 30000<gram>
                }
                |> PatientDto646.withEstimates (fst estimates) (snd estimates)

            (dto.Weight.Measured, dto.Weight.Estimated)
            |> Expect.equal "both, apart" (Some 30000<gram>, Some 32000<gram>)
        }

        test "the readers fall back to the estimate, so what is calculated does not change" {
            { PatientDto.empty with Age = Some ten }
            |> PatientDto646.withEstimates (fst estimates) (snd estimates)
            |> fun dto -> dto |> Patient.getWeight, dto |> Patient.getHeight
            |> Expect.equal "the estimate" (Some 32000<gram>, Some 140<cm>)
        }

        test "a gender chosen keeps the measured values and drops the estimates" {
            let dto =
                { PatientDto.empty with
                    Age = Some ten
                    PatientDto.Weight.Measured = Some 30000<gram>
                }
                |> PatientDto646.withEstimates (fst estimates) (snd estimates)

            Some dto
            |> PatientDto646.setGender "female"
            |> Option.map (fun p -> p.Gender, p.Weight.Measured, p.Weight.Estimated, p.Height.Estimated)
            |> Expect.equal "female, measured kept, estimates gone" (Some(Female, Some 30000<gram>, None, None))
        }

        test "a gender chosen first is a draft with the gender and nothing else" {
            None
            |> PatientDto646.setGender "male"
            |> Expect.equal "the gender alone" (Some { PatientDto.empty with Gender = Male })
        }
    ]


runTestsWithCLIArgs [] [||] tests
