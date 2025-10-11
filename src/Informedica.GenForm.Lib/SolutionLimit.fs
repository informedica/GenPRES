namespace Informedica.GenForm.Lib

module SolutionLimit =

    open Informedica.GenCore.Lib.Ranges
    open Utils

    /// An empty SolutionLimit.
    let limit =
        {
            SolutionLimitTarget = NoLimitTarget
            Quantity = MinMax.empty
            Quantities = None
            Concentration = MinMax.empty
        }


    let minMaxToString (minMax : MinMax) =
        if minMax = MinMax.empty then ""
        else
            minMax
            |> MinMax.toString
                "min "
                "min "
                "max "
                "max "


    let toString (sl: SolutionLimit) =
        [
            sl.Concentration |> minMaxToString
        ]
