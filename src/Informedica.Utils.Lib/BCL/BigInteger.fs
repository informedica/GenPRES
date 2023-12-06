namespace Informedica.Utils.Lib.BCL

/// Helper functions for `BigInteger`
[<RequireQualifiedAccess>]
module BigInteger =

    open System
    open MathNet.Numerics


    /// Create a `bigint` from an `int`
    let fromInt (x : int) = bigint(x)


    /// Create an `int` from a `bigint`
    let toInt (x : bigint) = int x


    /// Calculate the greatest common divisor of a sequence of `bigint`s
    /// Example: `gcdSeq [2I; 4I; 6I]` returns `2I`
    let gcdSeq (xs : bigint seq) =
        Euclid.GreatestCommonDivisor(xs |> Array.ofSeq)


    /// Calculate the least common multiple of a sequence of `bigint`s
    /// Example: `lcmSeq [2I; 4I; 6I]` returns `12I`
    let lcmSeq (xs : bigint seq) =
        Euclid.LeastCommonMultiple(xs |> Array.ofSeq)


    /// Calculate the greatest common divisor of two `bigint`s
    /// Example: `gcd 2I 4I` returns `2I`
    let gcd (a : bigint) (b : bigint) = gcdSeq [a; b]


    /// Calculate the least common multiple of two `bigint`s
    /// Example: `lcm 2I 4I` returns `4I`
    let lcm (a : bigint) (b : bigint) = lcmSeq [a; b]

    /// Calculate an ordered farey sequence
    let farey n asc =
        seq {
            let p = if asc then ref 0I else ref 1I
            let q = ref 1I
            let p2 = if asc then ref 1I else ref (n - 1I)
            let q2 = ref n
            yield (p.Value, q.Value)
            while (asc && not (p.Value = 1I && q.Value = 1I)) ||
                  (not asc && p.Value > 0I) do
                let c = (q.Value + n) / q2.Value
                let pTemp = c * p2.Value - p.Value
                let qTemp = c * q2.Value - q.Value
                p.Value <- p2.Value
                q.Value <- q2.Value
                p2.Value <- pTemp
                q2.Value <- qTemp
                yield (p.Value, q.Value) }



    module Tests =


        /// Test the farey function by generating and printing Farey sequences.
        let testFareySequence () =
            let order = 10I
            printfn $"Ascending Farey Sequence of Order %A{order}:"
            farey order true
            |> Seq.iter (fun (p, q) -> printfn "%A/%A" p q)

            printfn $"\nDescending Farey Sequence of Order %A{order}:"
            farey order false
            |> Seq.iter (fun (p, q) -> printfn "%A/%A" p q)

