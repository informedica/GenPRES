namespace Informedica.GenPRES.Client.Core.Models

/// Applies the edits of the patient panel fields to the patient data. Every edit clears the estimated
/// weight and height, which the server calculates again from the age and the gender.
module PatientEdit =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Models.PatientData


    let toggle item (p: PatientData option) : PatientData option =
        p
        |> Option.map (fun p ->
            { p with
                Access =
                    if p.Access |> List.exists ((=) item) then
                        p.Access |> List.filter ((<>) item)
                    else
                        p.Access |> List.append [ item ]
            }
        )


    let toggleCVL = toggle CVL


    let togglePVL = toggle PVL


    let toggleET = toggle EnteralTube


    let setRenal (s: string option) (p: PatientData option) : PatientData option =
        let set rf (p: PatientData option) =
            match p with
            | None -> p
            | Some p -> { p with RenalFunction = rf } |> Some

        match s with
        | None -> p |> set None
        | Some s ->
            let rf = s |> RenalFunctionText.optionToRenal |> Some
            p |> set rf


    /// Sets the gender and clears the estimates, which depend on the gender. Keeps the measured values.
    let setGender (s: string) (p: PatientData option) : PatientData option =
        let gender =
            match s with
            | "male" -> Male
            | "female" -> Female
            | _ -> UnknownGender

        p
        |> Option.defaultValue empty
        |> withEstimates None None
        |> fun p -> { p with Gender = gender }
        |> Some


    /// Clears the estimates when the age differs from the age in before, because the server calculates
    /// the estimates again for the new age. Keeps the measured values.
    let renewEstimates (before: PatientData) (p: PatientData) =
        if p.Age = before.Age then
            p
        else
            p |> withEstimates None None


    /// Clears the estimates and applies one change to the patient data, or to empty patient data when
    /// there is none. Measured values stay, and an estimate is never stored as a measured value; the
    /// server calculates the estimates again.
    let edit (change: PatientData -> PatientData) (p: PatientData option) : PatientData option =
        p |> Option.defaultValue empty |> withEstimates None None |> change |> Some


    /// Writes one part of the age from the field text. Patient data without an age stays without one
    /// when the text is empty or not a number. When an age exists, an empty part counts as 0, so emptying
    /// one field keeps the age. The server sets the adult mark for the new age.
    let editAgePart (write: int -> Age -> Age) (s: string option) (p: PatientData) =
        match p.Age, s |> Option.bind tryParse with
        | None, None -> p
        | age, v ->
            let age = age |> Option.defaultValue Age.ageZero |> write (v |> Option.defaultValue 0)

            { p with Age = Some age }


    let setYear s (p: PatientData option) =
        p |> edit (editAgePart (fun v a -> { a with Years = v |> Measures.toYear }) s)


    let setMonth s (p: PatientData option) =
        p |> edit (editAgePart (fun v a -> { a with Months = v |> Measures.toMonth }) s)


    let setWeek s (p: PatientData option) =
        p |> edit (editAgePart (fun v a -> { a with Weeks = v |> Measures.toWeek }) s)


    let setDay s (p: PatientData option) =
        p |> edit (editAgePart (fun v a -> { a with Days = v |> Measures.toDay }) s)


    /// Sets or clears the adult mark when the patient may be marked as an adult. Leaves any other adult
    /// mark as the server set it.
    let setMarkedAdult marked (p: PatientData option) =
        p
        |> edit (fun p ->
            match p.AdultMark with
            | AdultMark.MarkableAdult _ -> { p with AdultMark = AdultMark.MarkableAdult marked }
            | _ -> p
        )


    /// Writes one part of the gestational age from the field text, like editAgePart, starting from
    /// 37 weeks and 0 days when no gestational age was given. Only writes a gestational age the server
    /// asks for.
    let editGestAgePart (write: int option -> AgeWeeksDays -> AgeWeeksDays) (s: string option) (p: PatientData) =
        let v = s |> Option.bind tryParse

        let editValue ga =
            match ga, v with
            | None, None -> None
            | ga, v ->
                let term: AgeWeeksDays =
                    {
                        Weeks = 37<week>
                        Days = 0<day>
                    }

                ga |> Option.defaultValue term |> write v |> Some

        { p with GestationalAge = p.GestationalAge |> GestationalAge.map editValue }


    let setGAWeek s (p: PatientData option) =
        p
        |> edit (
            editGestAgePart
                (fun v ga -> { ga with Weeks = v |> Option.map Measures.toWeek |> Option.defaultValue 37<week> })
                s
        )


    let setGADay s (p: PatientData option) =
        p
        |> edit (
            editGestAgePart
                (fun v ga -> { ga with Days = v |> Option.map Measures.toDay |> Option.defaultValue 0<day> })
                s
        )


    /// Sets the measured weight in grams from the field text; an empty or invalid text clears it.
    let setWeight s (p: PatientData option) =
        p
        |> edit (fun p ->
            { p with Weight = { p.Weight with Measured = s |> Option.bind tryParse |> Option.map Measures.toGram } }
        )


    /// Sets the measured height in centimetres from the field text; an empty or invalid text clears it.
    let setHeight s (p: PatientData option) =
        p
        |> edit (fun p ->
            { p with Height = { p.Height with Measured = s |> Option.bind tryParse |> Option.map Measures.toCm } }
        )
