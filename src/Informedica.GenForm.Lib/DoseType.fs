namespace Informedica.GenForm.Lib


module DoseType =


    open Informedica.Utils.Lib.BCL


    /// Get a sort order for a dose type.
    let sortBy = function
        | OnceTimed _
        | Once _ -> 0
        | Timed _
        | Discontinuous _ -> 3
        | Continuous _ -> 4
        | AnyDoseType -> 100


    let eqs doseType1 doseType2 =
        match doseType1, doseType2 with
        | Once _, Once _
        | OnceTimed _, OnceTimed _
        | Timed _, Timed _
        | Discontinuous _, Discontinuous _
        | Continuous _, Continuous _
        | AnyDoseType, AnyDoseType -> true
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
        | _ -> AnyDoseType


    /// Get a string representation of a dose type.
    let toString doseType =
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
                | AnyDoseType -> ""
        | AnyDoseType -> ""

