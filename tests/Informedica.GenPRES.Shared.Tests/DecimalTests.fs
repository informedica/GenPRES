/// The precision of a decimal: the decimals a number of significant digits needs, and the decimal
/// rounded to them.
module Informedica.GenPRES.Shared.Tests.DecimalTests

open System.Globalization
open Expecto
open Expecto.Flip
open FsCheck

open Informedica.GenPRES.Shared


/// The Math of .NET, which the Math module of the contract hides in this file.
type SystemMath = System.Math


let config = { FsCheckConfig.defaultConfig with maxTest = 1000 }


/// The place of the first significant digit of a float, read from the shortest text that gives
/// the float back: 1 for 99.99999999999999, -6 for 1E-06 and -2 for 0.066.
let firstDigitPlace (f: float) =
    let text = (abs f).ToString("R", CultureInfo.InvariantCulture)

    match text.Split 'E' with
    | [| _; exponent |] -> int exponent
    | _ ->
        match text.Split '.' with
        | [| "0"; fraction |] -> -(fraction |> Seq.takeWhile ((=) '0') |> Seq.length) - 1
        | parts -> parts[0].Length - 1


/// The number of decimals that the place of the first digit gives for n significant digits.
let expectedDecimals n f = if n <= 0 then 0 else max 0 (n - 1 - firstDigitPlace f)


/// The float that lies the given number of floats above a float, or below it when negative.
let rec neighbour steps (f: float) =
    if steps = 0 then
        f
    elif steps > 0 then
        f |> SystemMath.BitIncrement |> neighbour (steps - 1)
    else
        f |> SystemMath.BitDecrement |> neighbour (steps + 1)


[<Tests>]
let tests =
    testList
        "Decimal precision"
        [

            testList
                "fixPrecision"
                [
                    for n, d, expected in
                        [
                            3, 1m / 24m, 0.0417m
                            2, 0.00416666m, 0.0042m
                            3, 0.0666m, 0.0666m
                            1, 0.0666m, 0.07m
                            3, 6.6666m, 6.67m
                            1, 66.666m, 67m
                            3, 105m, 105m
                            3, 0m, 0m
                        ] do
                        test $"%M{d} to %i{n} significant digits is %M{expected}" {
                            d |> Decimal.fixPrecision n |> Expect.equal "rounded" expected
                        }

                    test "a negative precision leaves the value as it is" {
                        0.041666m |> Decimal.fixPrecision -1 |> Expect.equal "unchanged" 0.041666m
                    }

                    test "a value read from text is rounded as the same value written as a number" {
                        "0.0416666666666666666666666667"
                        |> decimal
                        |> Decimal.fixPrecision 3
                        |> Expect.equal "0.0417" 0.0417m
                    }
                ]

            testList
                "Decimal.getPrecision"
                [
                    // the decimal, the number of significant digits and the expected number of decimals
                    for d, n, exp in
                        [
                            1000m, 3, 0
                            1000m, 5, 1
                            10m, 3, 1
                            1200m, 3, 0
                            100000m, 2, 0
                            -1000m, 3, 0
                            12.5m, 3, 1
                            0.0666m, 2, 3
                            6.6666m, 2, 1
                            0.001m, 2, 4
                            1e-27m, 1, 27
                            123456789012345678901234567m, 3, 0
                            0m, 3, 3
                            6.6666m, -1, 0
                        ] do
                        test $"%M{d} with %i{n} significant digits needs %i{exp} decimals" {
                            d |> Decimal.getPrecision n |> Expect.equal "the number of decimals" exp
                        }

                    testPropertyWithConfig
                        config
                        "a whole number that ends in zeros needs n decimals less its number of digits"
                    <| fun (whole: int) (zeros: byte) (n: byte) ->
                        let whole, zeros, n = 1 + abs (whole % 1000000), int zeros % 10, 1 + int n % 20
                        let digits = (string whole).Length + zeros

                        decimal whole * pown 10m zeros |> Decimal.getPrecision n = max 0 (n - digits)

                    testPropertyWithConfig
                        config
                        "a decimal below 1 needs n decimals more than the zeros after its decimal point"
                    <| fun (whole: int) (zeros: byte) (n: byte) ->
                        let whole, zeros, n = 1 + abs (whole % 1000000), int zeros % 15, 1 + int n % 10
                        // the digits of the whole number, placed after the zeros behind the decimal point
                        let d = decimal whole / pown 10m ((string whole).Length + zeros)

                        d |> Decimal.getPrecision n = zeros + n

                    testPropertyWithConfig config "the text of a decimal has no exponent"
                    <| fun (whole: int) (decimals: byte) (zeros: byte) ->
                        let d = decimal whole / pown 10m (int decimals % 29) * pown 10m (int zeros % 10)

                        not ((d |> abs |> string).ToUpper().Contains "E")
                ]

            testList
                "Math.getPrecision"
                [
                    // the float, the number of significant digits and the expected number of decimals
                    for f, n, exp in
                        [
                            0.0666, 2, 3
                            0.0666, 3, 4
                            6.6666, 2, 1
                            6.6666, 3, 2
                            66.666, 1, 0
                            0.6666, 1, 1
                            1., 3, 2
                            0.1, 1, 1
                            0.001, 2, 4
                            1000., 2, 0
                            0.00012, 2, 5
                            // written with an exponent under .NET
                            0.0000666, 2, 6
                            0.00001, 1, 5
                            // written with an exponent in the browser too
                            0.0000001234, 3, 9
                            1e-10, 2, 11
                            // a large float is written with an exponent as well
                            1e21, 3, 0
                            1.5e22, 3, 0
                            0., 3, 3
                            6.6666, -1, 0
                        ] do
                        test $"%g{f} with %i{n} significant digits needs %i{exp} decimals" {
                            f |> Math.getPrecision n |> Expect.equal "the number of decimals" exp
                        }

                    for f in [ infinity; -infinity; nan ] do
                        test $"%f{f} with 3 significant digits gives 3 and does not throw" {
                            f |> Math.getPrecision 3 |> Expect.equal "the number of digits asked for" 3
                        }

                    // each power of ten, and the values just below and above it
                    for k in -15 .. 20 do
                        for text in [ $"1e%i{k}"; $"9.999999e%i{k}"; $"1.000001e%i{k}"; $"5e%i{k}" ] do
                            test $"%s{text} with 1 significant digit needs %i{max 0 -k} decimals" {
                                text
                                |> float
                                |> Math.getPrecision 1
                                |> Expect.equal "the number of decimals" (max 0 -k)
                            }

                    testPropertyWithConfig
                        config
                        "the float gives the same number of decimals as the same value as a decimal"
                    <| fun (whole: int) (decimals: byte) (n: byte) ->
                        let d, n = decimal whole / pown 10m (int decimals % 13), int n % 10

                        d |> float |> Math.getPrecision n = (d |> Decimal.getPrecision n)

                    test "99.99999999999999 with 16 significant digits needs 14 decimals" {
                        99.99999999999999
                        |> Math.getPrecision 16
                        |> Expect.equal "the number of decimals" 14
                    }

                    testPropertyWithConfig config "any float needs the decimals that its first digit gives"
                    <| fun (f: NormalFloat) (n: byte) ->
                        let f, n = f.Get, int n % 18

                        f = 0. || f |> Math.getPrecision n = expectedDecimals n f

                    testPropertyWithConfig
                        config
                        "a float at or next to a power of ten needs the decimals that its first digit gives"
                    <| fun (k: int) (steps: int) (n: byte) ->
                        let f = float $"1e%i{k % 301}" |> neighbour (steps % 4)

                        f |> Math.getPrecision (int n % 18) = expectedDecimals (int n % 18) f

                    testPropertyWithConfig config "a negative float needs the same decimals as the positive float"
                    <| fun (f: NormalFloat) (n: byte) ->
                        f.Get |> Math.getPrecision (int n) = (-f.Get |> Math.getPrecision (int n))

                    testPropertyWithConfig config "getPrecision does not throw for any float"
                    <| fun (f: float) (n: int) ->
                        f |> Math.getPrecision n |> ignore
                        true
                ]

            testList
                "Math.fixPrecision"
                [
                    test "the float 3000000000.6 with 3 significant digits keeps its whole part" {
                        3000000000.6
                        |> Math.fixPrecision 3
                        |> Expect.equal "the whole number" 3000000001.
                    }

                    test "the float 0.0000666 with 2 significant digits rounds to 0.000067" {
                        0.0000666 |> Math.fixPrecision 2 |> Expect.equal "the rounded value" 0.000067
                    }

                    test "99.99999999999999 with 16 significant digits stays as it is" {
                        99.99999999999999
                        |> Math.fixPrecision 16
                        |> Expect.equal "the same value" 99.99999999999999
                    }

                    testPropertyWithConfig
                        config
                        "fixPrecision moves a float by at most half of the last digit that it keeps"
                    <| fun (k: int) (steps: int) (mantissa: NormalFloat) (n: byte) ->
                        // Math.Round is itself not exact near 15 digits, so the property stays below that
                        let n = 1 + int n % 10
                        // a float at or next to a power of ten, and any float of that size
                        let power = float $"1e%i{k % 13}"

                        [ power |> neighbour (steps % 4); power * (1. + abs mantissa.Get % 9.) ]
                        |> List.forall (fun f ->
                            let half = 0.5 * 10. ** float (firstDigitPlace f - n + 1)

                            f |> Math.getPrecision n > 15
                            || abs (Math.fixPrecision n f - f) <= half * 1.0000001
                        )

                    test "a negative precision leaves the float as it is" {
                        12.3456789 |> Math.fixPrecision -1 |> Expect.equal "unchanged" 12.3456789
                    }

                    test "the float -0.1 with 0 significant digits is zero, without a minus sign" {
                        -0.1 |> Math.fixPrecision 0 |> string |> Expect.equal "no minus sign" "0"
                    }
                ]

            testList
                "toStringNumberNLWithoutTrailingZerosFixPrecision"
                [
                    // the value, the number of significant digits and the expected text
                    for d, n, exp in
                        [
                            0.00123m, 3, "0,00123"
                            0.125m, 3, "0,125"
                            0.0666m, 2, "0,067"
                            0.035m, 2, "0,035"
                            0.0999m, 1, "0,1"
                            0.5m, 1, "0,5"
                            12.3456m, 3, "12,3"
                            105m, 3, "105"
                            1234.5678m, 3, "1 235"
                            0.0666m, 0, "0"
                            0m, 3, "0"
                            -0.00123m, 3, "-0,00123"
                            // a negative precision: the number in full
                            0.041666m, -1, "0,041666"
                            12.55m, -1, "12,55"
                        ] do
                        test $"%M{d} with %i{n} significant digits is %s{exp}" {
                            d
                            |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision n
                            |> Expect.equal "the Dutch text" exp
                        }

                    // the value and the expected text at 3 significant digits
                    for d, exp in
                        [
                            0.00123m, "0,00123"
                            1m / 3m, "0,333"
                            12.55m, "12,6"
                            125.55m, "126"
                            1234.6m, "1 235"
                            105m, "105"
                            0m, "0"
                        ] do
                        test $"the decimal %M{d} without a precision is %s{exp}" {
                            d
                            |> Decimal.toStringNumberNLWithoutTrailingZeros
                            |> Expect.equal "the Dutch text" exp
                        }

                    // 3 000 000 000 is above the largest 32 bit whole number
                    test "the decimal 3000000000 without a precision prints in full" {
                        3000000000m
                        |> Decimal.toStringNumberNLWithoutTrailingZeros
                        |> Expect.equal "the Dutch text" "3 000 000 000"
                    }

                    test "the decimal 3000000000.6 with 3 significant digits keeps its whole part" {
                        3000000000.6m
                        |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 3
                        |> Expect.equal "the Dutch text" "3 000 000 001"
                    }

                    test "a decimal with 29 significant digits and a negative n prints every digit" {
                        1.1234567890123456789012345678m
                        |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision -1
                        |> Expect.equal "the Dutch text" "1,1234567890123456789012345678"
                    }

                    test "the decimal 1e-27 with 3 significant digits prints with all its decimals" {
                        1e-27m
                        |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 3
                        |> Expect.equal "the Dutch text" "0,000000000000000000000000001"
                    }


                    // a decimal with at most 8 decimals, and a number of significant digits from 0 to 6
                    let value (whole: int) (decimals: byte) = decimal whole / pown 10m (int decimals % 9)
                    let digits (n: byte) = int n % 7
                    // Reads a Dutch number text back as a decimal.
                    let parse (s: string) = s.Replace(" ", "").Replace(",", ".") |> decimal

                    testPropertyWithConfig
                        config
                        "the decimal text reads back as the value rounded to n significant digits"
                    <| fun whole decimals n ->
                        let d, n = value whole decimals, digits n

                        d |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision n |> parse =
                            (d |> Decimal.fixPrecision n)

                    testPropertyWithConfig config "the decimal text has no trailing zeros after the comma"
                    <| fun whole decimals n ->
                        let s =
                            value whole decimals
                            |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision (digits n)

                        not (s.Contains ",") || not (s.EndsWith "0" || s.EndsWith ",")

                    testPropertyWithConfig config "with a negative n, the text reads back as the value itself"
                    <| fun whole decimals ->
                        let d = value whole decimals

                        d |> Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision -1 |> parse = d
                ]
        ]
