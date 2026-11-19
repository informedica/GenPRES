namespace Informedica.GenPRES.Client.Core.Policies

/// Turns a patient panel message into the edit that the server applies to the patient data.
module PatientDraftPolicy =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
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
        /// The gender.
        | UpdateGender of Gender
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


    /// Returns the edit a panel message makes to the draft; None for Clear, which discards the draft. A field text
    /// that is empty or not a number is None. A switch of an access adds the access when the draft does not have
    /// it, and removes it when the draft has it.
    let toEdit (draft: PatientData option) (msg: Msg) =
        let number = Option.bind PatientData.tryParse

        let addOrRemove item =
            if draft |> Option.exists (fun p -> p.Access |> List.contains item) then
                PatientDataEdit.RemoveAccess item
            else
                PatientDataEdit.AddAccess item

        match msg with
        | Msg.Clear -> None
        | Msg.UpdateYear s -> s |> number |> PatientDataEdit.SetYears |> Some
        | Msg.UpdateMonth s -> s |> number |> PatientDataEdit.SetMonths |> Some
        | Msg.UpdateWeek s -> s |> number |> PatientDataEdit.SetWeeks |> Some
        | Msg.UpdateDay s -> s |> number |> PatientDataEdit.SetDays |> Some
        | Msg.UpdateWeight s -> s |> number |> Option.map Measures.toGram |> PatientDataEdit.SetWeight |> Some
        | Msg.UpdateHeight s -> s |> number |> Option.map Measures.toCm |> PatientDataEdit.SetHeight |> Some
        | Msg.UpdateGAWeek s -> s |> number |> PatientDataEdit.SetGestationalWeeks |> Some
        | Msg.UpdateGADay s -> s |> number |> PatientDataEdit.SetGestationalDays |> Some
        | Msg.UpdateGender g -> g |> PatientDataEdit.SetGender |> Some
        | Msg.UpdateRenal s ->
            s
            |> Option.map RenalFunctionText.optionToRenal
            |> PatientDataEdit.SetRenalFunction
            |> Some
        | Msg.UpdateDepartment s -> s |> PatientDataEdit.SetDepartment |> Some
        | Msg.ToggleCVL -> addOrRemove CVL |> Some
        | Msg.TogglePVL -> addOrRemove PVL |> Some
        | Msg.ToggleET -> addOrRemove EnteralTube |> Some
        | Msg.UpdateMarkedAdult marked -> marked |> PatientDataEdit.SetMarkedAdult |> Some


    /// Whether the draft is valid patient data: it has an age, or a measured weight and height. An estimate
    /// does not count as a measurement.
    let canCalculate (draft: PatientData option) : bool =
        draft |> Option.bind (PatientData.validate >> Result.toOption) |> Option.isSome
