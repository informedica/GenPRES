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

            test "a measured weight is kept, the missing height estimated" {
                let pat = changed loaded None (ageOnly 10 |> withWeight 28000<gram>)

                (pat.Weight.Measured, pat.Weight.Estimated, pat.Height.Estimated)
                |> Expect.equal
                    "the measured weight, no weight estimate, the height estimated"
                    (Some 28000<gram>, None, Some 140<cm>)
            }

            test "a patient that lacks a weight or a height waits for the normal values" {
                [ ageOnly 10; ageOnly 10 |> withWeight 28000<gram>; sent ]
                |> List.map (PatientCommand.ChangePatient >> PatientCommand.gate)
                |> Expect.equal
                    "loaded for the two that lack a measure, open for the measured one"
                    [ Gate.RequiresLoaded; Gate.RequiresLoaded; Gate.Open ]
            }
        ]
