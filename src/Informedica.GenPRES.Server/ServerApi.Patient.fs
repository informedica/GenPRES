namespace ServerApi

open Shared.Types
open Shared.Models


/// Where patient data enters from the client. The wire carries a draft, every field optional;
/// a draft becomes a patient here, or the request is refused, so that no draft reaches the
/// services and the rules below them. Every member that receives patient data runs this
/// before anything else.
module Patient =

    /// A draft that is no patient: it has no age, and no measured weight and height.
    let noPatient =
        "Geen patiënt: een leeftijd, of een gemeten gewicht en lengte, is nodig"

    /// A patient without a weight or a height, measured or estimated. Only the client estimates,
    /// so data from the platform or another host can arrive with an age alone; the rules are
    /// gated on both, and an answer of no rules would say nothing. Until the server estimates,
    /// such a patient is refused with the two named.
    let noWeightAndHeight = "Gewicht en lengte onbekend: voer ze in"


    /// The patient the data is, or why it is none.
    let patient (dto: Patient) : Result<Patient, string[]> =
        match dto |> Patient.validate with
        | Error PatientError.NoAgeOrMeasuredWeightAndHeight -> Error [| noPatient |]
        | Ok pat ->
            match dto |> Patient.getWeight, dto |> Patient.getHeight with
            | Some _, Some _ -> Ok pat
            | _ -> Error [| noWeightAndHeight |]


    /// Data that may be absent: none is no patient and no refusal.
    let patientOption (dto: Patient option) : Result<Patient option, string[]> =
        match dto with
        | None -> Ok None
        | Some dto -> dto |> patient |> Result.map Some


    /// The request run once every piece of data is a patient, else the first refusal.
    let overAll (dtos: Patient list) (run: unit -> Async<Result<'a, string[]>>) : Async<Result<'a, string[]>> =
        let refused =
            dtos
            |> List.tryPick (fun dto ->
                match dto |> patient with
                | Ok _ -> None
                | Error errs -> Some errs
            )

        match refused with
        | None -> run ()
        | Some errs -> async { return Error errs }


    /// The request run once the data is a patient, else its refusal.
    let over (dto: Patient) run = overAll [ dto ] run


    /// The patient data a plan carries: its own, and that of every context in it.
    let ofPlan (plan: OrderPlan) =
        plan.Patient :: (plan.OrderContexts |> Array.map _.Patient |> Array.toList)


    /// Whether a platform reading is a patient: one that is none counts as no reading.
    let reading (dto: Patient option) =
        dto |> Option.filter (Patient.validate >> Result.isOk)
