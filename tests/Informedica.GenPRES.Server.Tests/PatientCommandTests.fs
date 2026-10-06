/// The patient command: a patient change answered with the patient made complete, through
/// Compute.bound as the composition root wires it. An identified patient gets the Session's age,
/// an anonymous one keeps the client's; both get the weight and height they lack estimated.
module Informedica.GenPRES.Server.Tests.PatientCommandTests

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Api
open ServerApi
open Informedica.GenPRES.Server.Tests.AgeOnRequestTests


/// The answer to a patient change in the Session named, with the normal values given, and the
/// patients the Session was told.
let changed normalValues (sid: string option) (pat: Patient) =
    let told = ResizeArray()
    let port = portOver (fun () -> today) (ResizeArray())

    let port =
        { port with
            seen =
                fun sid opened draft ->
                    told.Add draft
                    port.seen sid opened draft
        }

    let env = { envOver port with normalValues = normalValues }

    let answer =
        Compute.bound
            env
            (cookieOf sid)
            PatientCommand.toString
            (fun _ -> Gate.Open)
            PatientCommand.patientOf
            (PatientCommand.processCmd env (cookieOf sid))
            {
                Opened = sid |> Option.map (fun sid -> OpenedToken $"opened-{sid}")
                Command = PatientCommand.ChangePatient pat
            }
        |> Async.RunSynchronously
        |> Result.map _.Response
        |> Result.defaultWith (fun errs -> failtest $"%A{errs}")

    answer, told |> List.ofSeq


let withWeight grams (pat: Patient) = { pat with Weight = { pat.Weight with Measured = Some grams } }


[<Tests>]
let tests =
    testList
        "the patient command"
        [
            test "an identified patient gets the Session's age, its measured weight kept" {
                let pat, _ = changed loaded (Some identified) sent

                (pat.Age, pat.Weight.Measured)
                |> Expect.equal "the Session's age, the weight as measured" (Some sessionAge, Some 12000<gram>)
            }

            test "an identified patient is estimated at the Session's age, not the client's" {
                let pat, _ = changed loaded (Some identified) (ageOnly 5)

                (pat.Age, pat |> weightAndHeight)
                |> Expect.equal
                    "the Session's age, the ten-year estimate"
                    (Some sessionAge, (Some 32000<gram>, Some 140<cm>))
            }

            test "an anonymous patient keeps the client's age and gets the estimates" {
                for sid in [ None; Some anonymous; Some entered ] do
                    let pat, _ = changed loaded sid (ageOnly 10)

                    (pat.Age, pat |> weightAndHeight)
                    |> Expect.equal
                        $"the client's age, 32 kg and 140 cm, %A{sid}"
                        ((ageOnly 10).Age, (Some 32000<gram>, Some 140<cm>))
            }

            test "a measured weight is kept, the missing height estimated" {
                let pat, _ = changed loaded None (ageOnly 10 |> withWeight 28000<gram>)

                (pat.Weight.Measured, pat.Weight.Estimated, pat.Height.Estimated)
                |> Expect.equal
                    "the measured weight, no weight estimate, the height estimated"
                    (Some 28000<gram>, None, Some 140<cm>)
            }

            test "the Session is told the patient as sent" {
                let _, told = changed loaded (Some identified) sent

                told |> Expect.equal "the patient the client sent, once" [ Some sent ]
            }
        ]
