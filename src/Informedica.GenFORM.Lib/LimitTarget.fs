namespace Informedica.GenForm.Lib


module LimitTarget =


    /// Returns the name of a component or substance target, or an empty string for any other target.
    let toString =
        function
        | NoLimitTarget
        | OrderableLimitTarget -> ""
        | ComponentLimitTarget s
        | SubstanceLimitTarget s -> s


    /// Returns the component name of a component target, or an empty string for any other target.
    let componentTargetToString =
        function
        | ComponentLimitTarget s -> s
        | _ -> ""

    /// Returns the substance name of a substance target, or an empty string for any other target.
    let substanceTargetToString =
        function
        | SubstanceLimitTarget s -> s
        | _ -> ""


    /// Checks whether the target is the orderable.
    let isOrderableTarget target =
        target
        |> function
            | OrderableLimitTarget -> true
            | _ -> false


    /// Checks whether the target is a component.
    let isComponentTarget target =
        target
        |> function
            | ComponentLimitTarget _ -> true
            | _ -> false


    /// Checks whether the target is a substance.
    let isSubstanceTarget target =
        target
        |> function
            | SubstanceLimitTarget _ -> true
            | _ -> false
