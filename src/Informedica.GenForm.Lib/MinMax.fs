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


    /// Check if a value is between the minimum and maximum of a MinMax
    let isBetween (minMax : MinMax) = function
        | None -> true
        | Some v ->
            match minMax.Minimum, minMax.Maximum with
            | None, None -> true
            | Some min, None -> v >= min
            | None, Some max -> v < max
            | Some min, Some max -> v >= min && v < max


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
