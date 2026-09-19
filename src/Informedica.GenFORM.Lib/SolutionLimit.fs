namespace Informedica.GenForm.Lib


module SolutionLimit =

    open System
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib.Ranges


    /// Field labels for deterministic parsing
    module FieldLabels =
        [<Literal>]
        let Quantity = "[qty]"

        [<Literal>]
        let QuantityAdjust = "[qty-adj]"

        [<Literal>]
        let Quantities = "[qts]"

        [<Literal>]
        let Concentration = "[conc]"


    /// An empty SolutionLimit.
    let limit =
        {
            SolutionLimitTarget = NoLimitTarget
            Quantity = MinMax.empty
            QuantityAdj = MinMax.empty
            Quantities = None
            Concentration = MinMax.empty
            Products = [||]
        }


    let toString (sl: SolutionLimit) =
        let mmToStr =
            MinMax.toString
                ValueUnit.toStringDecimalEngShortWithoutGroup
                ValueUnit.toStringDecimalEngShortWithoutGroup
                "min "
                "min "
                "max "
                "max "

        [
            let qty = sl.Quantity |> mmToStr

            if qty |> String.notEmpty then
                $"{FieldLabels.Quantity} {qty}"

            let qtyAdj = sl.QuantityAdj |> mmToStr

            if qtyAdj |> String.notEmpty then
                $"{FieldLabels.QuantityAdjust} {qtyAdj}"

            sl.Quantities
            |> Option.map (fun vu -> $"{FieldLabels.Quantities} {vu |> ValueUnit.toStringDecimalEngShortWithoutGroup}"

            )
            |> Option.defaultValue ""

            let conc = sl.Concentration |> mmToStr

            if conc |> String.notEmpty then
                $"{FieldLabels.Concentration} {conc}"
        ]
