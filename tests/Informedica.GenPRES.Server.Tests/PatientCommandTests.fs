/// The patient command: a patient data update answered with the patient made complete, through
/// Compute.bound as the composition root wires it. The patient gets the weight and height it
/// lacks estimated.
module Informedica.GenPRES.Server.Tests.PatientCommandTests

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api
open ServerApi
open Informedica.GenPRES.Server.Tests.AgeOnRequestTests


/// The answer to a patient data update in the Session named, with the normal values given.
let changed normalValues (sid: string option) (pat: Patient) =
    let env = { envOver (portOver (ResizeArray())) with normalValues = normalValues }

    Compute.bound
        env
        (cookieOf sid)
        PatientCommand.toString
        PatientCommand.gate
        (PatientCommand.processCmd env)
        {
            Opened = sid |> Option.map (fun sid -> OpenedToken $"opened-{sid}")
            Command = PatientCommand.ChangePatient pat
        }
    |> Async.RunSynchronously
    |> Result.map _.Response
    |> Result.defaultWith (fun errs -> failtest $"%A{errs}")


let withWeight grams (pat: Patient) = { pat with Weight = { pat.Weight with Measured = Some grams } }


[<Tests>]
let tests =
    testList
        "the patient command"
        [
            test "an identified patient's measured weight is kept" {
                (changed loaded (Some identified) sent).Weight.Measured
                |> Expect.equal "as sent" sent.Weight.Measured
            }

            test "the age answered is the same whatever Session the cookie names" {
                [ None; Some identified; Some anonymous; Some entered; Some "nobody" ]
                |> List.map (fun sid -> (changed loaded sid sent).Age)
                |> Expect.allEqual "the age sent" sent.Age
            }

            test "the estimates follow the age the patient comes with" {
                for sid in [ None; Some identified; Some anonymous; Some entered ] do
                    changed loaded sid (ageOnly 5)
                    |> weightAndHeight
                    |> Expect.equal $"18 kg and 110 cm, %A{sid}" (Some 18000<gram>, Some 110<cm>)
            }

            test "a measured weight is kept, with the weight estimate next to it, and the height estimated" {
                let pat = changed loaded None (ageOnly 10 |> withWeight 28000<gram>)

                (pat.Weight.Measured, pat.Weight.Estimated, pat.Height.Estimated)
                |> Expect.equal
                    "the measured weight, the weight estimate, the height estimate"
                    (Some 28000<gram>, Some 32000<gram>, Some 140<cm>)
            }

            test "the gestational age with the need the age gives, and the post-menstrual age of it" {
                let ga30: AgeWeeksDays =
                    {
                        Weeks = 30<week>
                        Days = 0<day>
                    }

                let answer days =
                    let pat =
                        { Informedica.GenPRES.Shared.Models.Patient.empty with
                            Age = days |> Option.map ServerApi.Age.ofDays
                            GestationalAge = GestationalAge.OptionalGestationalAge(Some ga30)
                        }

                    let answered = changed loaded None pat
                    answered.GestationalAge, answered.PostMenstrualAge

                [ Some 70; Some 365; None ]
                |> List.map answer
                |> Expect.equal
                    "needed at 10 weeks with 40 weeks, none from 1 year and none without an age"
                    [
                        GestationalAge.RequiredGestationalAge(Some ga30),
                        Some
                            {
                                Weeks = 40<week>
                                Days = 0<day>
                            }
                        GestationalAge.NoGestationalAge, None
                        GestationalAge.NoGestationalAge, None
                    ]
            }

            test "every patient change waits for the normal values, a measured one too" {
                [ ageOnly 10; ageOnly 10 |> withWeight 28000<gram>; sent ]
                |> List.map (PatientCommand.ChangePatient >> PatientCommand.gate)
                |> Expect.equal "loaded for all three" [ Gate.RequiresLoaded; Gate.RequiresLoaded; Gate.RequiresLoaded ]
            }
        ]
