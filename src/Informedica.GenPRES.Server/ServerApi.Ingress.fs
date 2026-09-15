namespace ServerApi

open Shared.Types
open Shared.Models


/// Where patient data enters from the client. The wire carries a draft, every field optional;
/// a draft becomes a patient here, or the request is refused, so that no draft reaches the
/// services and the rules below them. Every member that receives patient data runs this
/// before anything else.
module Ingress =

    /// A draft that is no patient: it has no age, and no measured weight and height.
    let noPatient =
        "Geen patiënt: een leeftijd, of een gemeten gewicht en lengte, is nodig"

    /// A patient without a weight or a height, measured or estimated. Only the client estimates,
    /// so data from the platform or another host can arrive with an age alone; the rules are
    /// gated on both, and an answer of no rules would say nothing. Until the server estimates,
    /// such a patient is refused with the two named.
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
