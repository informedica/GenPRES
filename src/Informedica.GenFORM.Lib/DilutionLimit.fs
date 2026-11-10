namespace Informedica.GenForm.Lib


module DilutionLimit =

    open Informedica.GenCore.Lib.Ranges


    /// An empty DilutionLimit.
    let limit =
        {
            DilutionLimitTarget = NoLimitTarget
            Quantity = MinMax.empty
            QuantityAdj = MinMax.empty
            Quantities = None
            Concentration = MinMax.empty
            Products = [||]
        }
