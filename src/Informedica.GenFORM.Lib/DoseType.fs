namespace Informedica.GenForm.Lib


module DoseType =


    open Informedica.Utils.Lib.BCL


    /// Get a sort order for a dose type.
    let sortBy =
        function
        | OnceTimed _
        | Once _ -> 0
        | Timed _
        | Discontinuous _ -> 3
        | Continuous _ -> 4
        | NoDoseType -> 100


    let eqs doseType1 doseType2 =
        match doseType1, doseType2 with
        | Once txt1, Once txt2
        | OnceTimed txt1, OnceTimed txt2
        | Discontinuous txt1, Discontinuous txt2
        | Timed txt1, Timed txt2
        | Continuous txt1, Continuous txt2 -> txt1 |> String.equalsCapInsens txt2
        | NoDoseType, NoDoseType -> true
        | _ -> false


    let eqsType doseType1 doseType2 =
        match doseType1, doseType2 with
        | Once _, Once _
        | OnceTimed _, OnceTimed _
        | Discontinuous _, Discontinuous _
        | Timed _, Timed _
        | Continuous _, Continuous _
        | NoDoseType, NoDoseType -> true
        | _ -> false


    /// Parse a dose type from a string. Pure: returns the DoseType together with
    /// an optional warning message (Some when the type is non-empty but unknown).
    /// No console IO, so callers can surface the warning as data.
    let parse doseType doseText =
        let doseType = doseType |> String.toLower |> String.trim
        let withText c = doseText |> c

        match doseType with
        | "once" -> Once |> withText, None
        | "oncetimed" -> OnceTimed |> withText, None
        | "timed" -> Timed |> withText, None
        | "discontinuous" -> Discontinuous |> withText, None
        | "continuous" -> Continuous |> withText, None
        | _ ->
            let warn =
                if doseType |> String.notEmpty then
                    Some $"{doseType} is not a valid dose type!"
                else
                    None

            NoDoseType, warn


    /// <summary>
    /// Get a dose type from a string. Pure wrapper over <c>parse</c> that
    /// discards the warning.
    /// </summary>
    let fromString doseType doseText = parse doseType doseText |> fst


    let toString doseType =
        match doseType with
        | OnceTimed s -> "oncetimed", s
        | Once s -> "once", s
        | Timed s -> "timed", s
        | Discontinuous s -> "discontinuous", s
        | Continuous s -> "continuous", s
        | NoDoseType -> "", ""
        |> fun (s1, s2) -> if String.isNullOrWhiteSpace s2 then s1 else $"{s1} {s2}"


    /// Get a string representation of a dose type.
    let getText doseType =
        match doseType with
        | OnceTimed s
        | Once s
        | Timed s
        | Discontinuous s
        | Continuous s -> s
        | NoDoseType -> ""


    // category-only string; inverse of `parse`
    // and the category arm of `toString`. Keeps the mapping in one place.
    let toCategory =
        function
        | Once _ -> "once"
        | OnceTimed _ -> "oncetimed"
        | Timed _ -> "timed"
        | Discontinuous _ -> "discontinuous"
        | Continuous _ -> "continuous"
        | NoDoseType -> ""


    /// Get a string representation of a dose type.
    let toDescription doseType =
        let s = getText doseType

        if s |> String.notEmpty then
            s
        else
            match doseType with
            | OnceTimed _
            | Once _ -> "eenmalig"
            | Timed _
            | Discontinuous _ -> "onderhoud"
            | Continuous _ -> "continu"
            | NoDoseType -> ""


    let setDescription descr =
        function
        | OnceTimed _ -> OnceTimed descr
        | Once _ -> Once descr
        | Timed _ -> Timed descr
        | Discontinuous _ -> Discontinuous descr
        | Continuous _ -> Continuous descr
        | NoDoseType -> NoDoseType
