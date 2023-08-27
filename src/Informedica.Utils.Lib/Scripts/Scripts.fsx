//#I __SOURCE_DIRECTORY__

#load "load.fsx"

open System
open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL

Array.Tests.testAll()
Seq.Tests.testAll()
Set.Tests.testRemoveBigRationalMultiples()
Path.Tests.testAll ()
Csv.Tests.testAll ()
Char.Tests.testAll ()
BigInteger.Tests.testFareySequence()

BigInteger.farey 10I false |> Seq.toList

BigRational.Tests.testValueToFactorRatio()

ConsoleWriter.writeSeparator '-'


open MathNet.Numerics

/// Generic function to check whether a `divisor`
/// is a divisor of a `dividend`, i.e. the number being
/// divided
let inline isDivisor zero dividend divisor =
    dividend % divisor = zero

/// Get the greatest common divisor
/// of two BigRationals `a` and `b`
let gcd (a : BigRational) (b: BigRational) =
    let den = a.Denominator * b.Denominator
    let num = BigInteger.gcd (a.Numerator * b.Denominator) (b.Numerator * a.Denominator)
    (num |> BigRational.FromBigInt) / (den |> BigRational.FromBigInt)


/// Check whether a divisor divides a dividend
let isDivisorOfBigR  (dividend:BigRational) (divisor:BigRational) =
    isDivisor 0I dividend.Numerator divisor.Numerator


/// Split a rational number in a
/// numerator and denominator
let numDenom (v:BigRational) = (v.Numerator |> BigRational.FromBigInt, v.Denominator |> BigRational.FromBigInt)


let valueToFactorRatio v r =
    let vn, vd = numDenom v
    let toBigR = BigRational.FromBigInt

    match r with
    | Some n, false,  Some d, false ->
        (n, d)
    | Some n, true, Some d, true ->
        let r = (vn * d) / (vd * n)
        ((r.Numerator |> toBigR) * n), ((r.Denominator |> toBigR) * d)
    | None   , _ ,   Some d, false when (vd |> isDivisorOfBigR d) ->
        (vn * (d / vd), d)
    | None   , _ ,   Some d, true ->
        ((d / (gcd d vd)) * vn, (d / (gcd d vd)) * vd)
    | Some n, false,  None, _  when (vn |> isDivisorOfBigR n) ->
        (n, (n / vn) * vd)
    | Some n, true, None, _ ->
        ((n / (gcd n vn)) * vn, (n / (gcd n vn)) * vd)
    | None, _ , None, _ ->
        (vn, vd)
    | _  -> (0N, 0N)
    |> fun (n, d) ->
        if d <> 0N && n / d = v then Some (n, d)
        else None
