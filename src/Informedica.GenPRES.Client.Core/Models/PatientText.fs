namespace Informedica.GenPRES.Client.Core.Models

/// A patient as text: its age, its gestational age and the patient line of the title bar.
module PatientText =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Models.Patient
    open Informedica.GenPRES.Shared.Types.Patient


    module Age =

        let gestAgeToString terms lang (age: GestationalAge) =
            let getTerm = LocalizationText.getTerm terms

            $"""
{age.Weeks} {getTerm lang Terms.``Patient Age weeks``} {age.Days} {getTerm lang Terms.``Patient Age days``}
            """


        let toString terms lang (age: Age) =
            let getTerm = LocalizationText.getTerm terms lang

            let inline plur s1 s2 n = if int n = 1 then $"{int n} {s1}" else $"{int n} {s2}"

            let d =
                age.Days
                |> plur (getTerm Terms.``Patient Age day``) (getTerm Terms.``Patient Age days``)

            let w =
                age.Weeks
                |> plur (getTerm Terms.``Patient Age week``) (getTerm Terms.``Patient Age weeks``)

            let m =
                age.Months
                |> plur (getTerm Terms.``Patient Age month``) (getTerm Terms.``Patient Age months``)

            let y =
                age.Years
                |> plur (getTerm Terms.``Patient Age year``) (getTerm Terms.``Patient Age years``)

            match age with
            | _ when age.Years = 0<year> && age.Months = 0<month> && age.Weeks = 0<week> -> $"{d}"
            | _ when age.Years = 0<year> && age.Months = 0<month> -> if age.Days = 0<day> then $"{w}" else $"{w} en {d}"
            | _ when age.Years = 0<year> ->
                match age.Weeks, age.Days with
                | ws, ds when ds > 0<day> && ws > 0<week> -> $"{m}, {w} en {d}"
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


    let toString terms lang markDown (pat: Patient) =
        let getTerm = LocalizationText.getTerm terms lang

        let toStr s n =
            n |> Option.map (Math.fixPrecision 3 >> string >> (fun s' -> $"{s}{s'}"))

        let bold s = s |> Option.map (fun s -> if markDown then $"**{s}**" else s)

        let italic s = s |> Option.map (fun s -> if markDown then $"*{s}*" else s)

        let isAdult =
            pat.Age
            |> Option.map (fun a -> a.Years >= 18<year>)
            |> Option.defaultValue false

        [
            match pat.Gender with
            | Male -> if isAdult then Some "Man" else Some "Jongen"
            | Female -> if isAdult then Some "Vrouw" else Some "Meisje"
            | UnknownGender -> Some "Onbekend geslacht"
            |> bold

            Some $"{Terms.``Patient Age`` |> getTerm}:" |> italic

            pat.Age
            |> Option.map (Age.toString terms lang)
            |> bold
            |> Option.orElse ("" |> Some)

            Some $"{Terms.``Patient Weight`` |> getTerm}:" |> italic

            pat.Weight.Measured
            |> Option.map (fun x -> float x / 1000.)
            |> toStr ""
            |> Option.map (fun s -> $"{s} kg")
            |> bold


            match pat.Weight.EstimatedP3, pat.Weight.EstimatedP97 with
            | Some p3, Some p97 ->
                let capt = $"{Terms.``Patient Estimated`` |> getTerm}: "
                let p3 = float p3 / 1000. |> Math.fixPrecision 3
                let p97 = float p97 / 1000. |> Math.fixPrecision 3
                $"{capt}({p3} - {p97} kg)" |> Some
            | _ ->
                pat.Weight.Estimated
                |> Option.map (fun x -> float x / 1000.)
                |> toStr $"{Terms.``Patient Estimated`` |> getTerm}: "
                |> Option.map (fun s -> $"({s} kg)")


            Some $"{Terms.``Patient Length`` |> getTerm}:" |> italic

            pat.Height.Measured
            |> Option.map float
            |> toStr ""
            |> Option.map (fun s -> $"{s} cm")
            |> bold


            match pat.Height.EstimatedP3, pat.Height.EstimatedP97 with
            | Some p3, Some p97 ->
                let capt = $"{Terms.``Patient Estimated`` |> getTerm}: "
                let p3 = float p3 |> Math.fixPrecision 3
                let p97 = float p97 |> Math.fixPrecision 3
                $"{capt}({p3} - {p97} cm)" |> Some
            | _ ->
                pat.Height.Estimated
                |> Option.map float
                |> toStr $"{Terms.``Patient Estimated`` |> getTerm}: "
                |> Option.map (fun s -> $"({s} cm)")


            (Some "BSA:") |> italic
            pat
            |> calcBSA
            |> Option.map (fun x ->
                let x = x |> float |> Math.fixPrecision 2
                $"{x} m2"
            )
            |> bold

            if
                pat
                |> getAgeInDays
                |> Option.map (fun ds -> ds < 365.)
                |> Option.defaultValue false
            then
                (Some $", {Terms.``Patient GA Age`` |> getTerm}:") |> italic

                pat.GestationalAge
                |> Option.map (Age.gestAgeToString terms lang)
                |> Option.orElse ("" |> Some)

            if pat.RenalFunction |> Option.isSome then
                Some "Nierfunctie:" |> italic
                pat.RenalFunction |> Option.map RenalFunctionText.renalToOption |> bold

        ]
        |> List.choose id
        |> String.concat " "
        |> String.replace "  " " "
