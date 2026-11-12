namespace Informedica.GenForm.Lib


module SolutionLimit =

    open Informedica.GenCore.Lib.Ranges


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
