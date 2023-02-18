namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module Set =

    open Informedica.Utils.Lib.BCL

    let removeBigRationalMultiples xs =
        if xs |> Set.isEmpty then xs
        else
            xs
            |> Set.fold (fun acc x1 ->
                acc
                |> Set.filter (fun x2 ->
                    x1 = x2 ||
                    x2 |> BigRational.isMultiple x1 |> not
                )
            ) xs


    let toString xs = xs |> Set.toList |> List.toString