namespace Informedica.GenForm.Lib



module MinMax =

    open Informedica.Utils.Lib.BCL


    /// Create a MinMax from a tuple
    let fromTuple (min, max) =
        {
            Minimum = min
            Maximum = max
        }


    /// An empty MinMax
    let none = (None, None) |> fromTuple


    /// <summary>
    /// Map the minimum and maximum values of a MinMax
    /// </summary>
    /// <param name="fMin">Function to map the minimum value</param>
    /// <param name="fMax">Function to map the maximum value</param>
    /// <param name="minMax">The MinMax to map</param>
    let map fMin fMax (minMax : MinMax) =
        { minMax with
            Minimum = minMax.Minimum |> Option.map fMin
            Maximum = minMax.Maximum |> Option.map fMax
        }


    /// Check if a MinMax is empty
    let isNone (minMax : MinMax) =
        minMax.Minimum |> Option.isNone &&
        minMax.Maximum |> Option.isNone

    /// <summary>
    /// Check if a value is between the minimum and maximum of a MinMax
    /// </summary>
    /// <remarks>
    /// Assumes that the minimum is inclusive and the maximum is exclusive
    /// and if either is None, it is ignored. If both are None, the result is
    /// always true. If the optional value to check is None, the result is only
    /// true when MinMax is also None.
    /// </remarks>
    /// <example>
    /// <code>
    /// let minMax = MinMax.fromTuple (Some 1N, Some 10N)
    /// let result = MinMax.isBetween minMax (Some 5N)
    /// // result is true
    /// let result = MinMax.isBetween minMax (Some 10N)
    /// // result is false
    /// let result = MinMax.isBetween minMax (Some 1N)
    /// // result is true
    /// let result = MinMax.isBetween minMax (Some 0N)
    /// // result is false
    /// let result = MinMax.isBetween minMax None
    /// // result is false
    /// </code>
    /// </example>
    let isBetween (minMax : MinMax) = function
        | Some v ->
            match minMax.Minimum, minMax.Maximum with
            | None, None -> true
            | Some min, None -> v >= min
            | None, Some max -> v < max
            | Some min, Some max -> v >= min && v < max
        | None when minMax |> isNone -> true
        | _ -> false


    /// Get the string representation of a MinMax
    let toString { Minimum = min; Maximum = max } =
        let min = min |> Option.map BigRational.toStringNl
        let max = max |> Option.map BigRational.toStringNl

        match min, max with
        | None, None -> ""
        | Some min, None -> $"≥ {min}"
        | Some min, Some max ->
            if min = max then $"{min}"
            else
                $"{min} - {max}"
        | None, Some max -> $"< {max}"
