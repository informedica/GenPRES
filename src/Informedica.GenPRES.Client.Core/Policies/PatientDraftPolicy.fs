namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what a patient panel edit does to the patient data draft, and whether the weight and height
/// the user did not enter are estimated again.
module PatientDraftPolicy =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// One edit the panel makes to the draft.
    [<RequireQualifiedAccess>]
    type Msg =
        /// Discards the whole draft. Used for an anonymous patient, whose data is fictitious; the data of
        /// an identified patient is changed, never cleared.
        | Clear
        /// The years of the age, from the field.
        | UpdateYear of string option
        /// The months of the age, from the field.
        | UpdateMonth of string option
        /// The weeks of the age, from the field.
        | UpdateWeek of string option
        /// The days of the age, from the field.
        | UpdateDay of string option
        /// The measured weight in grams, from the field; None clears it.
        | UpdateWeight of string option
        /// The measured height in centimetres, from the field; None clears it.
        | UpdateHeight of string option
        /// The weeks of the gestational age, from the field.
        | UpdateGAWeek of string option
        /// The days of the gestational age, from the field.
        | UpdateGADay of string option
        /// The gender: male, female, or anything else for unknown.
        | UpdateGender of string
        /// The renal function, as one of its options.
        | UpdateRenal of string option
        /// The department; None uses the server default.
        | UpdateDepartment of string option
        /// A central venous line added or removed.
        | ToggleCVL
        /// A peripheral venous line added or removed.
        | TogglePVL
        /// An enteral tube added or removed.
        | ToggleET
        /// Sets or clears the adult mark, from the switch.
        | UpdateMarkedAdult of bool


    /// What becomes of the estimated weight and height after an edit.
    [<RequireQualifiedAccess>]
    type Estimates =
        /// The weight and height the user did not enter are estimated again, after an edit of the age,
        /// gender or gestational age, on which the normal values depend.
        | Renewed
        /// The estimates do not change after any other edit, so a weight or height the user cleared
        /// stays cleared.
        | Kept


    /// Sets the department of the draft; None uses the server default. Clearing the department when there
    /// is no draft returns no draft.
    let setDepartment (s: string option) (p: PatientData option) : PatientData option =
        match p, s with
        | None, None -> None
        | _ -> { (p |> Option.defaultValue PatientData.empty) with Department = s } |> Some


    /// Applies one edit to the draft. The setters clear both estimates; keepEstimates or a new estimate
    /// fills them in afterwards.
    let update (msg: Msg) (draft: PatientData option) : PatientData option =
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
        | Msg.UpdateMarkedAdult marked -> draft |> PatientEdit.setMarkedAdult marked


    /// Whether the weight and height are estimated again after the edit: after an edit of the age, gender
    /// or gestational age, and after Clear.
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
        | Msg.ToggleET
        | Msg.UpdateMarkedAdult _ -> Estimates.Kept


    /// Copies from the original draft, estimates included, the weight and height that the edit does not
    /// set. The weight or height that the edit sets or clears gets no estimate, so a cleared value shows
    /// nothing.
    let keepEstimates (msg: Msg) (draft: PatientData option) (edited: PatientData option) : PatientData option =
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


    /// Whether the draft is valid patient data: it has an age, or a measured weight and height. An estimate
    /// does not count as a measurement.
    let canCalculate (draft: PatientData option) : bool =
        draft |> Option.bind (PatientData.validate >> Result.toOption) |> Option.isSome
