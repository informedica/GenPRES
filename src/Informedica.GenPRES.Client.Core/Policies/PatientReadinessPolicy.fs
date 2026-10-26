namespace Informedica.GenPRES.Client.Core.Policies

/// Decides, from the patient draft alone, what the pages can do with it, and what every page
/// says when it is not enough: one message, decided here, for the emergency list and the
/// continuous list, which have no order context, as for prescribe, nutrition and the plan.
module PatientReadinessPolicy =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// What the calculations of a patient still miss. They go on without it, so this is a warning,
    /// not a refusal: an age-dependent entry is left out, a weight-based dose has no estimate to
    /// fall back on.
    [<RequireQualifiedAccess>]
    type Missing =
        /// No age: the patient is one by its measured weight and height, and everything bound to
        /// an age is left out.
        | Age
        /// No weight, measured or estimated.
        | Weight
        /// No height, measured or estimated.
        | Height
        /// No gestational age for a patient younger than 28 weeks, whose calculations follow it.
        | GestationalAge


    /// What the draft is ready for.
    [<RequireQualifiedAccess>]
    type Readiness =
        /// Nothing entered: no draft, or a draft equal to the empty one.
        | NoData
        /// Something entered, but no age and no measured weight and height: no patient yet.
        | NoPatient
        /// A patient, with what its calculations still miss; nothing when it is complete.
        | Patient of Missing list


    /// What a page needs the patient for.
    [<RequireQualifiedAccess>]
    type Needs =
        /// The page calculates for the patient: the emergency list, the continuous list, prescribe,
        /// nutrition and the order plan. It says what is missing while it calculates.
        | Calculation
        /// The page browses without a patient, and the dose check needs one: the formulary and the
        /// parenteralia.
        | DoseCheck
        /// The page checks manual drugs without a patient, and the plan's drugs need one: the
        /// interactions.
        | PlanMedication


    /// The age a patient has to be under for the gestational age to matter, in days: 28 weeks.
    let gestationalAgeMattersUnderDays = 28 * 7


    /// The age in days, counted as the age helpers count it: a year 365 days, a month 30.
    let ageInDays (age: Age) =
        int age.Years * 365 + int age.Months * 30 + int age.Weeks * 7 + int age.Days


    /// Whether a draft holds nothing at all: no age, no measurement, and none of the other data the
    /// panel edits, such as the gender, the renal function, the department or an access. A cleared
    /// field leaves a draft; this reads it as nothing entered. A draft with any of the other data
    /// is not blank, so the notice then says what makes it a patient: an age, or a weight and a
    /// height.
    let isBlank (p: Patient) = p = Patient.empty


    /// Whether the patient is younger than 28 weeks, so that the gestational age matters. Without
    /// an age it does not: the patient is one by its measurements then.
    let needsGestationalAge (p: Patient) =
        match p.Age with
        | Some age -> ageInDays age < gestationalAgeMattersUnderDays
        | None -> false


    /// What the calculations of a patient still miss, in the order the notice says it. A weight or
    /// a height counts measured or estimated, as the rules read it.
    let missingOf (p: Patient) : Missing list =
        [
            if p.Age.IsNone then
                Missing.Age
            if (p |> Patient.getWeight).IsNone then
                Missing.Weight
            if (p |> Patient.getHeight).IsNone then
                Missing.Height
            if p |> needsGestationalAge && p.GestationalAge.IsNone then
                Missing.GestationalAge
        ]


    /// What the draft is ready for. The age or a measured weight and height decide between a
    /// patient and a draft, as Patient.validate does; a patient carries what it still misses.
    let readiness (draft: Patient option) : Readiness =
        match draft with
        | None -> Readiness.NoData
        | Some p when p |> isBlank -> Readiness.NoData
        | Some p ->
            match p |> Patient.validate with
            | Error _ -> Readiness.NoPatient
            | Ok p -> Readiness.Patient(missingOf p)


    /// The English of the readiness terms, used when the sheet has no row for a term or has not
    /// loaded. Any other term shows its name.
    let english (term: Terms) =
        match term with
        | Terms.``Patient enter patient data`` -> "Enter patient data"
        | Terms.``Patient enter age or weight and height`` -> "Enter an age, or a weight AND a height"
        | Terms.``Patient Age unknown`` -> "Age unknown: enter it, what depends on the age is left out"
        | Terms.``Prescribe Weight and height unknown`` -> "Weight and height unknown: enter them, there is no estimate"
        | Terms.``Prescribe Weight unknown`` -> "Weight unknown: enter it, there is no estimate"
        | Terms.``Prescribe Height unknown`` -> "Height unknown: enter it, there is no estimate"
        | Terms.``Patient Gestational age unknown`` ->
            "Gestational age unknown: enter it, the patient is younger than 28 weeks"
        | Terms.``Patient Needed Dose Check`` -> "The dose check needs a patient"
        | Terms.``Patient Needed Plan Medication`` -> "The medication of the order plan needs a patient"
        | term -> $"{term}"


    /// What the draft misses to be a patient, as the panel says it under the summary: none for a
    /// patient.
    let missing (readiness: Readiness) : Terms option =
        match readiness with
        | Readiness.NoData -> Some Terms.``Patient enter patient data``
        | Readiness.NoPatient -> Some Terms.``Patient enter age or weight and height``
        | Readiness.Patient _ -> None


    /// The sentences for what a patient misses: one per dimension, the weight and the height as one
    /// when both are missing.
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


    /// What a page that needs the patient for `needs` says about the draft, as the terms of its
    /// sentences: none when the draft has what the page needs. A page that works without a patient
    /// says so only while there is none, with what needs the patient; a page that calculates says
    /// what is missing, and keeps saying it while it calculates without it.
    let notice (needs: Needs) (readiness: Readiness) : Terms list option =
        let browsing needed =
            readiness |> missing |> Option.map (fun term -> [ term; needed ])

        match needs with
        | Needs.DoseCheck -> browsing Terms.``Patient Needed Dose Check``
        | Needs.PlanMedication -> browsing Terms.``Patient Needed Plan Medication``
        | Needs.Calculation ->
            match readiness with
            | Readiness.NoData -> Some [ Terms.``Patient enter patient data`` ]
            | Readiness.NoPatient -> Some [ Terms.``Patient enter age or weight and height`` ]
            | Readiness.Patient [] -> None
            | Readiness.Patient missing -> Some(sentences missing)


    /// The text of a notice: its sentences translated and joined.
    let message (tr: Terms -> string) (sentences: Terms list) = sentences |> List.map tr |> TermText.sentences
