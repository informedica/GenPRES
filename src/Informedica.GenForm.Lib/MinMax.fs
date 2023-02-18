namespace Informedica.GenForm.Lib



module MinMax =

    open Informedica.Utils.Lib.BCL


    let fromTuple (min, max) =
        {
            Minimum = min
            Maximum = max
        }


    let none = (None, None) |> fromTuple


    let map fMin fMax (minMax : MinMax) =
        { minMax with
            Minimum = minMax.Minimum |> Option.map fMin
            Maximum = minMax.Maximum |> Option.map fMax
        }


    let isNone (minMax : MinMax) =
        minMax.Minimum |> Option.isNone &&
        minMax.Maximum |> Option.isNone


    let isBetween (minMax : MinMax) = function
        | None -> true
        | Some v ->
            match minMax.Minimum, minMax.Maximum with
            | None, None -> true
            | Some min, None -> v >= min
            | None, Some max -> v < max
            | Some min, Some max -> v >= min && v < max


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
