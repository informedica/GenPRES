namespace Informedica.GenForm.Lib


module DoseType =


    open Informedica.Utils.Lib
    open Informedica.Utils.Lib.BCL
    open ConsoleWriter.NewLineNoTime


    /// Get a sort order for a dose type.
    let sortBy = function
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


    /// Get a dose type from a string.
    let fromString doseType doseText =
        let doseType = doseType |> String.toLower |> String.trim
        let withText c = doseText |> c

        match doseType with
        | "once" -> Once |> withText
        | "oncetimed" -> OnceTimed |> withText
        | "timed" -> Timed |> withText
        | "discontinuous" -> Discontinuous |> withText
        | "continuous" -> Continuous |> withText
        | _ ->
            if doseType |> String.notEmpty then
                $"{doseType} is not a valid dose type!"
                |> writeWarningMessage
            NoDoseType


    let toString doseType =
        match doseType with
        | OnceTimed s -> "oncetimed", s
        | Once s -> "once", s
        | Timed s -> "timed", s
        | Discontinuous s -> "discontinuous", s
        | Continuous s -> "continuous", s
        | NoDoseType -> "", ""
        |> fun (s1, s2) ->
            if String.isNullOrWhiteSpace(s2) then s1
            else $"{s1} {s2}"


    /// Get a string representation of a dose type.
    let toDescription doseType =
        match doseType with
        | OnceTimed s
        | Once s
        | Timed s
        | Discontinuous s
        | Continuous s ->
            if s |> String.notEmpty then s
            else
                match doseType with
                | OnceTimed _
                | Once _ -> "eenmalig"
                | Timed _
                | Discontinuous _ -> "onderhoud"
                | Continuous _ -> "continu"
                | NoDoseType -> ""

        | NoDoseType -> ""


    let setDescription descr = function
        | OnceTimed _ -> OnceTimed descr
        | Once _ -> Once descr
        | Timed _ -> Timed descr
        | Discontinuous _ -> Discontinuous descr
        | Continuous _ -> Continuous descr
        | NoDoseType -> NoDoseType