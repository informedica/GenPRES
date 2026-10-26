namespace Informedica.GenPRES.Client.Core.Models

/// The patient panel's edits: every field written to the draft, the estimates blanked so that
/// they follow the age and the gender again.
module PatientEdit =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Models.Patient


    let toggle item (p: Patient option) : Patient option =
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


    let setRenal (s: string option) (p: Patient option) : Patient option =
        let set rf (p: Patient option) =
            match p with
            | None -> p
            | Some p -> { p with RenalFunction = rf } |> Some

        match s with
        | None -> p |> set None
        | Some s ->
            let rf = s |> RenalFunctionText.optionToRenal |> Some
            p |> set rf


    /// The gender chosen: the estimates go, since they follow the gender; the measured values
    /// stay, since they do not.
    let setGender (s: string) (p: Patient option) : Patient option =
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


    /// The rule every setter follows: the draft, or the blank one, with the estimates
    /// blanked and one change applied. Nothing else on the patient is touched, so a value
    /// that was measured is never lost to an edit of another field, and an estimate is never
    /// written back as a measured value; the estimates follow the age and the gender, and the
    /// next applyNormalValues fills them again.
    let edit (change: Patient -> Patient) (p: Patient option) : Patient option =
        p |> Option.defaultValue empty |> withEstimates None None |> change |> Some


    /// One part of the age written from the field. A draft with no age gets one when the
    /// part is given and stays without one when it is not; a part cleared while the age
    /// exists reads as zero, so the age is never lost by emptying one field of it.
    let editAgePart (write: int -> Age -> Age) (s: string option) (p: Patient) =
        match p.Age, s |> Option.bind tryParse with
        | None, None -> p
        | age, v ->
            let age = age |> Option.defaultValue Age.ageZero |> write (v |> Option.defaultValue 0)

            { p with Age = Some age }


    let setYear s (p: Patient option) =
        p |> edit (editAgePart (fun v a -> { a with Years = v |> Measures.toYear }) s)


    let setMonth s (p: Patient option) =
        p |> edit (editAgePart (fun v a -> { a with Months = v |> Measures.toMonth }) s)


    let setWeek s (p: Patient option) =
        p |> edit (editAgePart (fun v a -> { a with Weeks = v |> Measures.toWeek }) s)


    let setDay s (p: Patient option) =
        p |> edit (editAgePart (fun v a -> { a with Days = v |> Measures.toDay }) s)


    /// One part of the gestational age written from the field, as the age parts are, with
    /// the term values, 37 weeks and 0 days, for a part that was never given.
    let editGestAgePart (write: int option -> GestAge -> GestAge) (s: string option) (p: Patient) =
        match p.GestationalAge, s |> Option.bind tryParse with
        | None, None -> p
        | ga, v ->
            let term: GestAge =
                {
                    Weeks = 37<week>
                    Days = 0<day>
                }

            { p with GestationalAge = ga |> Option.defaultValue term |> write v |> Some }


    let setGAWeek s (p: Patient option) =
        p
        |> edit (
            editGestAgePart
                (fun v ga -> { ga with Weeks = v |> Option.map Measures.toWeek |> Option.defaultValue 37<week> })
                s
        )


    let setGADay s (p: Patient option) =
        p
        |> edit (
            editGestAgePart
                (fun v ga -> { ga with Days = v |> Option.map Measures.toDay |> Option.defaultValue 0<day> })
                s
        )


    /// The measured weight in grams from the field; the height, measured or not, untouched.
    let setWeight s (p: Patient option) =
        p
        |> edit (fun p ->
            { p with Weight = { p.Weight with Measured = s |> Option.bind tryParse |> Option.map Measures.toGram } }
        )


    /// The measured height in centimetres from the field; the weight, measured or not, untouched.
    let setHeight s (p: Patient option) =
        p
        |> edit (fun p ->
            { p with Height = { p.Height with Measured = s |> Option.bind tryParse |> Option.map Measures.toCm } }
        )
