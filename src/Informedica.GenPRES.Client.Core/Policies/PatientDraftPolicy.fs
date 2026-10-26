namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what an edit of the patient panel does to the draft the App holds, and whether the
/// weight and height the user did not enter are estimated again after it.
module PatientDraftPolicy =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// One edit the panel makes to the draft.
    [<RequireQualifiedAccess>]
    type Msg =
        /// The draft discarded as a whole: the data of an anonymous patient, which is fictitious;
        /// an identified patient's data is changed, never cleared.
        | Clear
        /// The years of the age, from the field.
        | UpdateYear of string option
        /// The months of the age, from the field.
        | UpdateMonth of string option
        /// The weeks of the age, from the field.
        | UpdateWeek of string option
        /// The days of the age, from the field.
        | UpdateDay of string option
        /// The measured weight in grams, from the field; none clears it.
        | UpdateWeight of string option
        /// The measured height in centimetres, from the field; none clears it.
        | UpdateHeight of string option
        /// The weeks of the gestational age, from the field.
        | UpdateGAWeek of string option
        /// The days of the gestational age, from the field.
        | UpdateGADay of string option
        /// The gender: male, female, or anything else for unknown.
        | UpdateGender of string
        /// The renal function, as one of its options.
        | UpdateRenal of string option
        /// The department, or none for the server's default.
        | UpdateDepartment of string option
        /// A central venous line added or removed.
        | ToggleCVL
        /// A peripheral venous line added or removed.
        | TogglePVL
        /// An enteral tube added or removed.
        | ToggleET


    /// What becomes of the estimated weight and height after an edit.
    [<RequireQualifiedAccess>]
    type Estimates =
        /// Estimated again, for the weight and height the user did not enter: after an edit of the
        /// age, the gender or the gestational age, which the normal values follow.
        | Renewed
        /// Kept as they were: after any other edit, so that a weight or a height the user cleared
        /// stays cleared and is not estimated again.
        | Kept


    /// The department chosen for the draft, or none, which leaves the server's default in force.
    /// Clearing what was never a draft stays no draft.
    let setDepartment (s: string option) (p: Patient option) : Patient option =
        match p, s with
        | None, None -> None
        | _ -> { (p |> Option.defaultValue Patient.empty) with Department = s } |> Some


    /// The draft after one edit. The setters blank both estimates, so the result is what the App
    /// receives, not what it shows: keepEstimates or the App's estimate completes it.
    let update (msg: Msg) (draft: Patient option) : Patient option =
        match msg with
        | Msg.Clear -> None
        | Msg.UpdateYear s -> draft |> PatientEdit.setYear s
        | Msg.UpdateMonth s -> draft |> PatientEdit.setMonth s
        | Msg.UpdateWeek s -> draft |> PatientEdit.setWeek s
        | Msg.UpdateDay s -> draft |> PatientEdit.setDay s
        | Msg.UpdateWeight s -> draft |> PatientEdit.setWeight s
        | Msg.UpdateHeight s -> draft |> PatientEdit.setHeight s
        | Msg.UpdateGAWeek s -> draft |> PatientEdit.setGAWeek s
        | Msg.UpdateGADay s -> draft |> PatientEdit.setGADay s
        | Msg.UpdateRenal s -> draft |> PatientEdit.setRenal s
        | Msg.UpdateGender s -> draft |> PatientEdit.setGender s
        | Msg.UpdateDepartment s -> draft |> setDepartment s
        | Msg.ToggleCVL -> draft |> PatientEdit.toggleCVL
        | Msg.TogglePVL -> draft |> PatientEdit.togglePVL
        | Msg.ToggleET -> draft |> PatientEdit.toggleET


    /// Whether the App estimates the weight and height again after the edit: after an edit of the
    /// age, the gender or the gestational age, and after Clear, which leaves nothing to estimate.
    let estimates (msg: Msg) : Estimates =
        match msg with
        | Msg.Clear
        | Msg.UpdateYear _
        | Msg.UpdateMonth _
        | Msg.UpdateWeek _
        | Msg.UpdateDay _
        | Msg.UpdateGender _
        | Msg.UpdateGAWeek _
        | Msg.UpdateGADay _ -> Estimates.Renewed
        | Msg.UpdateWeight _
        | Msg.UpdateHeight _
        | Msg.UpdateRenal _
        | Msg.UpdateDepartment _
        | Msg.ToggleCVL
        | Msg.TogglePVL
        | Msg.ToggleET -> Estimates.Kept


    /// The edited draft with the weight and the height the edit does not set as they were in the
    /// draft, estimates included. The setters blank both estimates; the measure an edit sets or
    /// clears keeps none, so a cleared weight or height shows nothing.
    let keepEstimates (msg: Msg) (draft: Patient option) (edited: Patient option) : Patient option =
        match draft, edited with
        | Some d, Some e ->
            match msg with
            | Msg.UpdateWeight _ -> { e with Height = d.Height }
            | Msg.UpdateHeight _ -> { e with Weight = d.Weight }
            | _ ->
                { e with
                    Weight = d.Weight
                    Height = d.Height
                }
            |> Some
        | _ -> edited


    /// Whether the draft is a patient: an age, or a measured weight and height; an estimate does
    /// not stand in for a measurement, so the minimum decides.
    let canCalculate (draft: Patient option) : bool =
        draft |> Option.bind (Patient.validate >> Result.toOption) |> Option.isSome
