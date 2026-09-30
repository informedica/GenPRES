/// Decides, from the patient draft alone, what the pages can do with it, and what every page
/// says when it is not enough: one message, decided here, for the emergency list and the
/// continuous list, which have no order context, as for prescribe, nutrition and the plan.
module PatientReadiness

open Shared
open Shared.Types
open Shared.Models


/// What the draft is ready for.
[<RequireQualifiedAccess>]
type Readiness =
    /// Nothing entered: no draft, or a draft without an age, a gestational age or a measurement.
    | NoData
    /// Something entered, but no age and no measured weight and height: no patient yet.
    | NoPatient
    /// A patient with an age, but neither a weight nor a height, measured or estimated.
    | NoWeightAndHeight
    /// A patient with a height but no weight, measured or estimated.
    | NoWeight
    /// A patient with a weight but no height, measured or estimated.
    | NoHeight
    /// A patient by its measured weight and height, but without an age: the dose rules with an
    /// age bound are not matched.
    | NoAge
    /// A patient with an age, a weight and a height.
    | Complete


/// What a page needs the patient for.
[<RequireQualifiedAccess>]
type Needs =
    /// The page calculates by the weight: the emergency list and the continuous list.
    | Weight
    /// The page matches dose rules: prescribe, nutrition and the order plan.
    | DoseRules
    /// The page browses without a patient, and the dose check needs one: the formulary and the
    /// parenteralia.
    | DoseCheck
    /// The page checks manual drugs without a patient, and the plan's drugs need one: the
    /// interactions.
    | PlanMedication


/// Whether a draft holds nothing at all: no age, no measurement, and none of the other data the
/// panel edits, such as the gender, the renal function, the department or an access. A cleared
/// field leaves a draft; this reads it as nothing entered. A draft with any of the other data
/// is not blank, so the notice then says what makes it a patient: an age, or a weight and a
/// height.
let isBlank (p: Patient) = p = Patient.empty


/// What the draft is ready for. A weight or a height counts measured or estimated, as the
/// rules read it; the age decides between a patient and a draft, as Patient.validate does.
let readiness (draft: Patient option) : Readiness =
    match draft with
    | None -> Readiness.NoData
    | Some p when p |> isBlank -> Readiness.NoData
    | Some p ->
        match p |> Patient.validate with
        | Error _ -> Readiness.NoPatient
        | Ok p ->
            match p |> Patient.getWeight, p |> Patient.getHeight with
            | None, None -> Readiness.NoWeightAndHeight
            | None, Some _ -> Readiness.NoWeight
            | Some _, None -> Readiness.NoHeight
            | Some _, Some _ when p.Age.IsNone -> Readiness.NoAge
            | Some _, Some _ -> Readiness.Complete


/// Whether the draft is a patient the pages calculate for.
let canCalculate (draft: Patient option) =
    match draft |> readiness with
    | Readiness.NoData
    | Readiness.NoPatient -> false
    | Readiness.NoWeightAndHeight
    | Readiness.NoWeight
    | Readiness.NoHeight
    | Readiness.NoAge
    | Readiness.Complete -> true


/// The English of the readiness terms, used when the sheet has no row for a term or has not
/// loaded. Any other term shows its name.
let english (term: Terms) =
    match term with
    | Terms.``Patient enter patient data`` -> "Enter patient data"
    | Terms.``Patient enter age or weight and height`` -> "Enter an age, or a weight AND a height"
    | Terms.``Prescribe Age unknown`` -> "Age unknown: only dose rules without an age bound are shown"
    | Terms.``Prescribe Weight and height unknown`` -> "Weight and height unknown: enter them, there is no estimate"
    | Terms.``Prescribe Weight unknown`` -> "Weight unknown: enter it, there is no estimate"
    | Terms.``Prescribe Height unknown`` -> "Height unknown: enter it, there is no estimate"
    | Terms.``Patient Needed Dose Check`` -> "The dose check needs a patient"
    | Terms.``Patient Needed Plan Medication`` -> "The medication of the order plan needs a patient"
    | term -> $"{term}"


/// What the draft misses to be a patient, as the panel says it under the summary: none for a
/// patient.
let missing (readiness: Readiness) : Terms option =
    match readiness with
    | Readiness.NoData -> Some Terms.``Patient enter patient data``
    | Readiness.NoPatient -> Some Terms.``Patient enter age or weight and height``
    | Readiness.NoWeightAndHeight
    | Readiness.NoWeight
    | Readiness.NoHeight
    | Readiness.NoAge
    | Readiness.Complete -> None


/// What a page that needs the patient for `needs` says about the draft, as the terms of its
/// sentences: none when the draft has what the page needs. A page that works without a patient
/// says so only while there is none, with what needs the patient; a page that calculates says
/// what is missing.
let notice (needs: Needs) (readiness: Readiness) : Terms list option =
    let browsing needed =
        readiness |> missing |> Option.map (fun term -> [ term; needed ])

    match needs with
    | Needs.DoseCheck -> browsing Terms.``Patient Needed Dose Check``
    | Needs.PlanMedication -> browsing Terms.``Patient Needed Plan Medication``
    | Needs.Weight ->
        match readiness with
        | Readiness.NoData -> Some [ Terms.``Patient enter patient data`` ]
        | Readiness.NoPatient -> Some [ Terms.``Patient enter age or weight and height`` ]
        | Readiness.NoWeightAndHeight
        | Readiness.NoWeight -> Some [ Terms.``Prescribe Weight unknown`` ]
        | Readiness.NoHeight
        | Readiness.NoAge
        | Readiness.Complete -> None
    | Needs.DoseRules ->
        match readiness with
        | Readiness.NoData -> Some [ Terms.``Patient enter patient data`` ]
        | Readiness.NoPatient -> Some [ Terms.``Patient enter age or weight and height`` ]
        | Readiness.NoWeightAndHeight -> Some [ Terms.``Prescribe Weight and height unknown`` ]
        | Readiness.NoWeight -> Some [ Terms.``Prescribe Weight unknown`` ]
        | Readiness.NoHeight -> Some [ Terms.``Prescribe Height unknown`` ]
        | Readiness.NoAge -> Some [ Terms.``Prescribe Age unknown`` ]
        | Readiness.Complete -> None


/// The text of a notice: its sentences translated and joined.
let message (tr: Terms -> string) (sentences: Terms list) = sentences |> List.map tr |> TermText.sentences
