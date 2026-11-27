namespace Informedica.GenPRES.Client.Core.Models

/// Converts a dose type to the text a page shows, and to and from the text in a url or a list.
module DoseTypeText =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    /// Returns the text of a dose type, such as avond for the evening dose of ataluren, or None when the
    /// text is empty.
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


    /// Returns the text of the dose type, or else eenmalig, onderhoud or continu. Returns an empty string
    /// for NoDoseType.
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


    /// Returns the name of the dose type, such as once, followed by its text when the text is not empty.
    let doseTypeToString doseType =
        match doseType with
        | OnceTimed s -> "oncetimed", s
        | Once s -> "once", s
        | Timed s -> "timed", s
        | Discontinuous s -> "discontinuous", s
        | Continuous s -> "continuous", s
        | NoDoseType -> "", ""
        |> fun (s1, s2) -> if String.isNullOrWhiteSpace s2 then s1 else $"{s1} {s2}"


    /// Parses the text that doseTypeToString returns. Returns NoDoseType for an unknown name.
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
