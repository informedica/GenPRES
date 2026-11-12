namespace Informedica.GenPRES.Client.Core.Models

/// A patient as text: its age, its gestational age and the patient line of the title bar.
module PatientText =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Models.Patient
    open Informedica.GenPRES.Shared.Types.Patient


    /// The English of the patient terms, used when the sheet has no row for a term or has not
    /// loaded. Any other term shows the English of the renal function terms, or its name.
    let english term =
        match term with
        | Terms.``Patient Male`` -> "Male"
        | Terms.``Patient Female`` -> "Female"
        | Terms.``Patient Boy`` -> "Boy"
        | Terms.``Patient Girl`` -> "Girl"
        | Terms.``Patient Unknown Gender`` -> "Unknown"
        | Terms.``Patient Age`` -> "Age"
        | Terms.``Patient Weight`` -> "Weight"
        | Terms.``Patient Length`` -> "Length"
        | Terms.``Patient BSA`` -> "BSA"
        | Terms.``Patient GA Age`` -> "Gest. Age"
        | Terms.``Patient Renal Function`` -> "Renal function"
        | Terms.``Patient Age year`` -> "year"
        | Terms.``Patient Age years`` -> "years"
        | Terms.``Patient Age month`` -> "month"
        | Terms.``Patient Age months`` -> "months"
        | Terms.``Patient Age week`` -> "week"
        | Terms.``Patient Age weeks`` -> "weeks"
        | Terms.``Patient Age day`` -> "day"
        | Terms.``Patient Age days`` -> "days"
        | Terms.``Patient Age And`` -> "and"
        | Terms.``Patient Estimated`` -> "estimated"
        | _ -> RenalFunctionText.english term


    /// The text of a term in the language: the sheet's translation, else the English.
    let text terms lang term =
        LocalizationText.getTerm terms lang term |> Option.defaultValue (english term)


    module Age =

        let gestAgeToString terms lang (age: AgeWeeksDays) =
            let text = text terms lang

            $"%i{int age.Weeks} %s{text Terms.``Patient Age weeks``} %i{int age.Days} %s{text Terms.``Patient Age days``}"


        let toString terms lang (age: Age) =
            let text = text terms lang

            let inline plur s1 s2 n = if int n = 1 then $"{int n} {s1}" else $"{int n} {s2}"

            let andWord = text Terms.``Patient Age And``

            let d =
                age.Days
                |> plur (text Terms.``Patient Age day``) (text Terms.``Patient Age days``)

            let w =
                age.Weeks
                |> plur (text Terms.``Patient Age week``) (text Terms.``Patient Age weeks``)

            let m =
                age.Months
                |> plur (text Terms.``Patient Age month``) (text Terms.``Patient Age months``)

            let y =
                age.Years
                |> plur (text Terms.``Patient Age year``) (text Terms.``Patient Age years``)

            match age with
            | _ when age.Years = 0<year> && age.Months = 0<month> && age.Weeks = 0<week> -> $"{d}"
            | _ when age.Years = 0<year> && age.Months = 0<month> ->
                if age.Days = 0<day> then $"{w}" else $"{w} {andWord} {d}"
            | _ when age.Years = 0<year> ->
                match age.Weeks, age.Days with
                | ws, ds when ds > 0<day> && ws > 0<week> -> $"{m}, {w} {andWord} {d}"
                | ws, ds when ds = 0<day> && ws > 0<week> -> $"{m}, {w}"
                | ws, ds when ds > 0<day> && ws = 0<week> -> $"{m}, {d}"
                | _ -> $"{m}"
            | _ ->
                match age.Months, age.Weeks, age.Days with
                | ms, ws, ds when ms = 0<month> && ds > 0<day> && ws > 0<week> -> $"{y}, {w}, {d}"
                | ms, ws, ds when ms = 0<month> && ds = 0<day> && ws > 0<week> -> $"{y}, {w}"
                | ms, ws, ds when ms = 0<month> && ds > 0<day> && ws = 0<week> -> $"{y}, {d}"
                | ms, ws, ds when ms > 0<month> && ds > 0<day> && ws > 0<week> -> $"{y}, {m}, {w}, {d}"
                | ms, ws, ds when ms > 0<month> && ds = 0<day> && ws > 0<week> -> $"{y}, {m}, {w}"
                | ms, ws, ds when ms > 0<month> && ds > 0<day> && ws = 0<week> -> $"{y}, {m}, {d}"
                | ms, ws, ds when ms > 0<month> && ds = 0<day> && ws = 0<week> -> $"{y}, {m}"
                | _ -> $"{y}"


    let calcBSA (pat: Patient) =
        match pat.Weight.Measured, pat.Weight.Estimated, pat.Height.Measured, pat.Height.Estimated with
        | None, None, _, _
        | _, _, None, None -> None

        | Some w, _, Some h, _
        | Some w, _, None, Some h
        | None, Some w, Some h, _
        | None, Some w, None, Some h -> BodySurfaceArea.calcDuBois w h |> Some


    /// The word for the patient's gender and age group: an adult by the Shared adult test, the
    /// mark as an adult included.
    let genderWord tr (pat: Patient) =
        let adult = pat |> isAdult

        let term =
            match pat.Gender, adult with
            | Male, true -> Terms.``Patient Male``
            | Male, false -> Terms.``Patient Boy``
            | Female, true -> Terms.``Patient Female``
            | Female, false -> Terms.``Patient Girl``
            | UnknownGender, _ -> Terms.``Patient Unknown Gender``

        tr term


    /// A weight in grams as kilograms.
    let kilograms (g: int<gram>) = float g / 1000.


    /// A weight in kg, as the patient line shows it.
    let weightText g = $"%s{g |> kilograms |> Math.fixPrecision 3 |> string} kg"


    /// A height in cm, as the patient line shows it.
    let heightText (h: int<cm>) = $"%s{float h |> Math.fixPrecision 3 |> string} cm"


    /// An access device as the patient panel labels it, with the translated word for a tube.
    let accessText tube access =
        match access with
        | CVL -> "CVL"
        | PVL -> "PVL"
        | EnteralTube -> tube


    /// The estimated weight or height as the summary shows it. Without a measured value: the word for an
    /// estimate, the estimate and, when both are known, the P3 - P97 range. Otherwise the word for an
    /// estimate followed by the range, or by the estimate when the range is not known.
    let estimateText estimatedWord toNumber unit measured estimated p3 p97 =
        let number v = v |> toNumber |> Math.fixPrecision 3 |> string

        let range =
            match p3, p97 with
            | Some p3, Some p97 -> Some $"(%s{number p3} - %s{number p97} %s{unit})"
            | _ -> None

        match measured, estimated with
        | None, Some est ->
            [ Some $"%s{estimatedWord} %s{number est} %s{unit}"; range ]
            |> List.choose id
            |> String.concat " "
            |> Some
        | _ ->
            range
            |> Option.orElse (estimated |> Option.map (fun est -> $"(%s{number est} %s{unit})"))
            |> Option.map (fun s -> $"%s{estimatedWord}: %s{s}")


    let toString terms lang markDown (pat: Patient) =
        let bold s = s |> Option.map (fun s -> if markDown then $"**{s}**" else s)

        let italic s = s |> Option.map (fun s -> if markDown then $"*{s}*" else s)

        let text = text terms lang

        let estimatedWord = text Terms.``Patient Estimated``

        [
            pat |> genderWord text |> Some |> bold

            Some $"%s{text Terms.``Patient Age``}:" |> italic

            pat.Age
            |> Option.map (Age.toString terms lang)
            |> bold
            |> Option.defaultValue ""
            |> Some

            Some $"%s{text Terms.``Patient Weight``}:" |> italic

            pat.Weight.Measured |> Option.map weightText |> bold


            estimateText
                estimatedWord
                kilograms
                "kg"
                pat.Weight.Measured
                pat.Weight.Estimated
                pat.Weight.EstimatedP3
                pat.Weight.EstimatedP97


            Some $"%s{text Terms.``Patient Length``}:" |> italic

            pat.Height.Measured |> Option.map heightText |> bold


            estimateText
                estimatedWord
                float
                "cm"
                pat.Height.Measured
                pat.Height.Estimated
                pat.Height.EstimatedP3
                pat.Height.EstimatedP97


            Some $"%s{text Terms.``Patient BSA``}:" |> italic
            pat
            |> calcBSA
            |> Option.map (fun x ->
                let x = x |> float |> Math.fixPrecision 2
                $"{x} m2"
            )
            |> bold

            if pat.GestationalAge |> GestationalAge.mayBeGiven then
                (Some $", %s{text Terms.``Patient GA Age``}:") |> italic

                pat.GestationalAge
                |> GestationalAge.value
                |> Option.map (Age.gestAgeToString terms lang)
                |> Option.defaultValue ""
                |> Some

            if pat.RenalFunction |> Option.isSome then
                Some $"%s{text Terms.``Patient Renal Function``}:" |> italic
                pat.RenalFunction |> Option.map (RenalFunctionText.term >> text) |> bold

        ]
        |> List.choose id
        |> String.concat " "
        |> String.replace "  " " "
