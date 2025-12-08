namespace Informedica.GenUnits.Lib


module Array =

    open Informedica.Utils.Lib.BCL
    open MathNet.Numerics

    /// <summary>
    /// Remove all BigRationals that are multiples of the smallest BigRationals in the array.
    /// Uses an efficient sieve algorithm similar to the Sieve of Eratosthenes.
    /// </summary>
    /// <param name="xs">The array of BigRationals</param>
    /// <returns>The array without multiples</returns>
    /// <example>
    /// <code>
    /// [| 2N; 3N; 4N; 5N; 6N; 7N; 8N; 9N; 10N |] |> removeBigRationalMultiples
    /// // Returns: [| 2N; 3N; 5N; 7N |]
    /// </code>
    /// </example>
    let removeBigRationalMultiples xs =
        if xs |> Array.isEmpty then
            xs
        else
            let candidates =
                xs
                |> Array.filter (fun x -> x > 0N)
                |> Array.sort
                |> Array.distinct

            // Use a mutable set for efficiency
            let remaining = System.Collections.Generic.HashSet<BigRational>(candidates)
            let result = System.Collections.Generic.List<BigRational>()

            for current in candidates do
                if remaining.Contains(current) then
                    result.Add(current)
                    // Remove all multiples of current (except current itself)
                    let toRemove =
                        remaining
                        |> Seq.filter (fun x -> x > current && x |> BigRational.isMultiple current)
                        |> Seq.toArray

                    for multiple in toRemove do
                        remaining.Remove(multiple) |> ignore

            result.ToArray()