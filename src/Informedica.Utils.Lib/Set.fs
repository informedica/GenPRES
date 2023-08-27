namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module Set =

    open Informedica.Utils.Lib.BCL

    /// In summary, the purpose of this function is to remove all elements
    /// from a given set of BigRational numbers that are multiples of any
    /// other element in the same set. The function returns a new set containing
    /// only the non-multiple elements.
    let removeBigRationalMultiples xs =
        xs
        |> Set.fold (fun acc x1 ->
            acc
            |> Set.filter (fun x2 ->
                x1 = x2 ||
                x2 |> BigRational.isMultiple x1 |> not
            )
        ) xs


    let toString xs = xs |> Set.toList |> List.toString


    module Tests =

        open MathNet.Numerics
        open Swensen.Unquote


        // Test that the removeBigRationalMultiples function works as expected.
        let testRemoveBigRationalMultiples () =
            let xs = set [ 2N .. 1N .. 12N ]
            let ys = set [2N; 3N; 5N; 7N; 11N]
            test <@ removeBigRationalMultiples xs = ys @>

