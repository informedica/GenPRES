namespace Informedica.Utils.Lib.BCL

/// Helper functions for `BigInteger`
[<RequireQualifiedAccess>]
module BigInteger = 

    open System
    open MathNet.Numerics


    let fromInt (x : int) = bigint(x)


    let toInt (x : bigint) = int x


    let gcdSeq (xs : bigint seq) = 
        Euclid.GreatestCommonDivisor(xs |> Array.ofSeq)


    let lcmSeq (xs : bigint seq) = 
        Euclid.LeastCommonMultiple(xs |> Array.ofSeq)


    let gcd (a : bigint) (b : bigint) = gcdSeq [a; b]


    let lcm (a : bigint) (b : bigint) = lcmSeq [a; b] 

