// Where patient data enters the server. The wire carries a draft, every field optional; the
// domain patient exists only with an age, or a measured weight and height. The client builds
// one before it acts, but the server cannot trust that: a stale client or a hand-made request
// can send a context or a plan whose data is no patient, and today the server takes it as it
// is and answers no rules, silently, or signs it into the record. A draft becomes a patient at
// the ingress, or the request is refused, before anything else runs. Until the server
// estimates, a patient must also bring a weight and a height, measured or estimated: only the
// client estimates, so data from the platform or another host can arrive with an age alone,
// and the rules are gated on both.
//
// Script-first draft (script-only policy) of `Ingress.patient`, `patientOption` and `over`
// → new `ServerApi.Ingress.fs`, run first by the order-context, plan, signing and formulary
// dispatchers; `PatientContext.Patient` optional and `sessionPatient` answering none where the
// platform and the record have nothing, in the patch.
//
// Run: `dotnet fsi Ingress.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Models


/// → `ServerApi.Ingress.fs`.
module Ingress646 =

    let noPatient = "Geen patiënt: een leeftijd, of een gemeten gewicht en lengte, is nodig"

    let noWeightAndHeight = "Gewicht en lengte onbekend: voer ze in"


    /// The patient the data is, or why it is none.
    let patient (dto: PatientDto) : Result<Patient, string[]> =
        match dto |> Patient.fromDto with
        | Error PatientError.NoAgeOrMeasuredWeightAndHeight -> Error [| noPatient |]
        | Ok pat ->
            match dto |> Patient.getWeight, dto |> Patient.getHeight with
            | Some _, Some _ -> Ok pat
            | _ -> Error [| noWeightAndHeight |]


    /// Data that may be absent: none is no patient and no refusal.
    let patientOption (dto: PatientDto option) : Result<Patient option, string[]> =
        match dto with
        | None -> Ok None
        | Some dto -> dto |> patient |> Result.map Some


    /// The request run once the data is a patient, else its refusal.
    let over (dto: PatientDto) (run: unit -> Async<Result<'a, string[]>>) : Async<Result<'a, string[]>> =
        match dto |> patient with
        | Ok _ -> run ()
        | Error errs -> async { return Error errs }


let ten = { Patient.Age.ageZero with Age.Years = 10<year> }

let estimated (dto: PatientDto) =
    { dto with
        PatientDto.Weight.Estimated = Some 32000<gram>
        PatientDto.Height.Estimated = Some 140<cm>
    }


let tests =
    testList "the ingress" [
        test "the blank draft is refused as no patient" {
            PatientDto.empty
            |> Ingress646.patient
            |> Result.mapError Array.toList
            |> Expect.equal "no patient" (Error [ Ingress646.noPatient ])
        }

        test "an age without an estimate is refused, the weight and height named" {
            { PatientDto.empty with Age = Some ten }
            |> Ingress646.patient
            |> Result.mapError Array.toList
            |> Expect.equal "weight and height" (Error [ Ingress646.noWeightAndHeight ])
        }

        test "an age with the estimate is a patient" {
            { PatientDto.empty with Age = Some ten }
            |> estimated
            |> Ingress646.patient
            |> Result.isOk
            |> Expect.isTrue "a patient"
        }

        test "a measured weight and height is a patient" {
            { PatientDto.empty with
                PatientDto.Weight.Measured = Some 32000<gram>
                PatientDto.Height.Measured = Some 140<cm>
            }
            |> Ingress646.patient
            |> Result.isOk
            |> Expect.isTrue "a patient"
        }

        test "no data is no patient and no refusal" {
            None |> Ingress646.patientOption |> Expect.equal "none" (Ok None)
        }

        test "a request runs over a patient and is refused over a draft, the port never asked" {
            let asked = ref 0

            let run () =
                async {
                    asked.Value <- asked.Value + 1
                    return Ok "answered"
                }

            Ingress646.over ({ PatientDto.empty with Age = Some ten } |> estimated) run
            |> Async.RunSynchronously
            |> Expect.equal "run" (Ok "answered")

            Ingress646.over PatientDto.empty run
            |> Async.RunSynchronously
            |> Result.mapError Array.toList
            |> Expect.equal "refused" (Error [ Ingress646.noPatient ])

            asked.Value |> Expect.equal "the port asked once" 1
        }
    ]


runTestsWithCLIArgs [] [||] tests
