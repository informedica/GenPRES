namespace Informedica.GenPRES.Client.Core.Models

/// A dose type as text: the description a page shows, and the text a url or a list carries.
module DoseTypeText =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    /// The text of a dose type, such as avond for the evening dose of ataluren; none for a dose type without
    /// a text.
    let text doseType =
        match doseType with
        | OnceTimed s
        | Once s
        | Timed s
        | Discontinuous s
        | Continuous s when s |> String.notEmpty -> Some s
        | OnceTimed _
        | Once _
        | Timed _
        | Discontinuous _
        | Continuous _
        | NoDoseType -> None


    /// The description a page shows: the dose type's text, or the Dutch name of its kind.
    let doseTypeToDescription doseType =
        match text doseType with
        | Some s -> s
        | None ->
            match doseType with
            | OnceTimed _
            | Once _ -> "eenmalig"
            | Timed _
            | Discontinuous _ -> "onderhoud"
            | Continuous _ -> "continu"
            | NoDoseType -> ""


    /// The dose type as a url or a list carries it: its kind, then its text when it has one.
    let doseTypeToString doseType =
        match doseType with
        | OnceTimed s -> "oncetimed", s
        | Once s -> "once", s
        | Timed s -> "timed", s
        | Discontinuous s -> "discontinuous", s
        | Continuous s -> "continuous", s
        | NoDoseType -> "", ""
        |> fun (s1, s2) -> if String.isNullOrWhiteSpace s2 then s1 else $"{s1} {s2}"


    /// The dose type from the text doseTypeToString gives; no dose type for an unknown kind.
    let doseTypeFromString s =
        let matchDoseType (dt: string) dd =
            let dt = dt.ToLower().Trim()
            let withText c = dd |> c

            match dt with
            | "once" -> Once |> withText
            | "oncetimed" -> OnceTimed |> withText
            | "timed" -> Timed |> withText
            | "discontinuous" -> Discontinuous |> withText
            | "continuous" -> Continuous |> withText
            | _ -> NoDoseType

        match s |> String.split " " |> Array.toList with
        | [ dt ] -> matchDoseType dt ""
        | dt :: rest -> rest |> String.concat " " |> matchDoseType dt
        | _ -> NoDoseType
