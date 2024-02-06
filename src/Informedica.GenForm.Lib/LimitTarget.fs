namespace Informedica.GenForm.Lib

module LimitTarget =


    /// Get the LimitTarget as a string.
    let limitTargetToString = function
        | NoLimitTarget -> ""
        | ShapeLimitTarget s
        | SubstanceLimitTarget s -> s


    /// Get the substance from the SubstanceLimitTarget.
    let substanceLimitTargetToString = function
        | SubstanceLimitTarget s -> s
        | _ -> ""


    /// Check whether the LimitTarget is a SubstanceLimitTarget.
    let isSubstanceLimit target =
        target
        |> function
        | SubstanceLimitTarget _ -> true
        | _ -> false


    /// Check whether the LimitTarget is a ShapeLimitTarget.
    let isShapeLimit target =
        target
        |> function
        | ShapeLimitTarget _ -> true
        | _ -> false

