namespace Informedica.GenPRES.Client.Core.Policies

/// Decides from the patient draft alone what the pages can do, and what each page says when the draft
/// is not enough. The emergency list and the continuous list, which have no order context, get the same
/// message as prescribe, nutrition and the plan.
module PatientReadinessPolicy =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// Data that the calculations of a patient lack. The calculations continue without it, so this is a
    /// warning, not a refusal: an entry that depends on age is left out, and a weight-based dose has no
    /// estimate to fall back on.
    [<RequireQualifiedAccess>]
    type Missing =
        /// No age: the patient is valid through a measured weight and height, and everything that
        /// depends on age is left out.
        | Age
        /// No weight, measured or estimated.
        | Weight
        /// No height, measured or estimated.
        | Height
        /// No gestational age for a patient younger than 28 days, whose calculations depend on it.
        | GestationalAge


    /// What the draft is ready for.
    [<RequireQualifiedAccess>]
    type Readiness =
        /// Nothing entered: no draft, or a draft equal to the empty one.
        | NoData
        /// Data entered, but no age and no measured weight and height, so no patient yet.
        | NoPatient
        /// A patient, with the data its calculations lack; an empty list when complete.
        | Patient of Missing list


    /// What a page needs the patient for.
    [<RequireQualifiedAccess>]
    type Needs =
        /// The page calculates for the patient: the emergency list, the continuous list, prescribe,
        /// nutrition and the order plan. It says what is missing while it calculates.
        | Calculation
        /// The interactions page: it checks manually entered medications without a patient, but the
        /// medications of the plan need one.
        | PlanMedication


    /// Whether a draft equals the empty patient data: no age, no measurement, and none of the other panel
    /// data such as the gender, renal function, department or an access. A draft whose fields were all
    /// cleared is blank.
    let isBlank (p: PatientData) = p = PatientData.empty


    /// Returns the data the calculations of a patient lack, in notice order. A weight or height counts
    /// when measured or estimated. An adult needs no age, because the rules match an adult without one.
    let missingOf (p: PatientData) : Missing list =
        let adult = p |> PatientData.isAdult

        [
            if not adult && p.Age.IsNone then
                Missing.Age
            if (p |> PatientData.getWeight).IsNone then
                Missing.Weight
            if (p |> PatientData.getHeight).IsNone then
                Missing.Height
            if p.GestationalAge |> GestationalAge.isMissing then
                Missing.GestationalAge
        ]


    /// Returns what the draft is ready for. PatientData.validate, which needs an age or a measured weight
    /// and height, decides whether the draft is a patient; a patient carries the data it lacks.
    let readiness (draft: PatientData option) : Readiness =
        match draft with
        | None -> Readiness.NoData
        | Some p when p |> isBlank -> Readiness.NoData
        | Some p ->
            match p |> PatientData.validate with
            | Error _ -> Readiness.NoPatient
            | Ok p -> Readiness.Patient(missingOf p)


    /// The English of the readiness terms, used when the sheet has no row for a term or has not
    /// loaded. Any other term shows its name.
    let english (term: Terms) =
        match term with
        | Terms.``Patient enter patient data`` -> "Enter patient data"
        | Terms.``Patient enter age or weight and height`` -> "Enter an age, or a weight AND a height"
        | Terms.``Patient Age unknown`` -> "Age unknown: enter it, what depends on the age is left out"
        | Terms.``Prescribe Weight and height unknown`` -> "Weight and height unknown: enter them, no estimate is used"
        | Terms.``Prescribe Weight unknown`` -> "Weight unknown: enter it, no estimate is used"
        | Terms.``Prescribe Height unknown`` -> "Height unknown: enter it, no estimate is used"
        | Terms.``Patient Gestational age unknown`` ->
            "Gestational age unknown: enter it, the patient is younger than 28 days"
        | Terms.``Patient Needed Plan Medication`` -> "The medication of the order plan needs a patient"
        | term -> $"{term}"


    /// The panel message for a draft that is not a patient; None for a patient.
    let missing (readiness: Readiness) : Terms option =
        match readiness with
        | Readiness.NoData -> Some Terms.``Patient enter patient data``
        | Readiness.NoPatient -> Some Terms.``Patient enter age or weight and height``
        | Readiness.Patient _ -> None


    /// The sentences for the data a patient lacks: one per item, with weight and height combined when
    /// both are missing.
    let sentences (missing: Missing list) : Terms list =
        [
            if missing |> List.contains Missing.Age then
                Terms.``Patient Age unknown``
            match missing |> List.contains Missing.Weight, missing |> List.contains Missing.Height with
            | true, true -> Terms.``Prescribe Weight and height unknown``
            | true, false -> Terms.``Prescribe Weight unknown``
            | false, true -> Terms.``Prescribe Height unknown``
            | false, false -> ()
            if missing |> List.contains Missing.GestationalAge then
                Terms.``Patient Gestational age unknown``
        ]


    /// Returns the notice terms for a page with the given needs; None when the draft has what the page
    /// needs. A page that works without a patient shows a notice only while there is no patient. A page
    /// that calculates says what is missing, also while it calculates without it.
    let notice (needs: Needs) (readiness: Readiness) : Terms list option =
        let browsing needed =
            readiness |> missing |> Option.map (fun term -> [ term; needed ])

        match needs with
        | Needs.PlanMedication -> browsing Terms.``Patient Needed Plan Medication``
        | Needs.Calculation ->
            match readiness with
            | Readiness.NoData -> Some [ Terms.``Patient enter patient data`` ]
            | Readiness.NoPatient -> Some [ Terms.``Patient enter age or weight and height`` ]
            | Readiness.Patient [] -> None
            | Readiness.Patient missing -> Some(sentences missing)


    /// The text of a notice: its sentences translated and joined.
    let message (tr: Terms -> string) (sentences: Terms list) = sentences |> List.map tr |> TermText.sentences
