/// The precision of a decimal: the decimals a number of significant digits needs, and the decimal
/// rounded to them.
module Informedica.GenPRES.Shared.Tests.DecimalTests

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Shared


let config = { FsCheckConfig.defaultConfig with maxTest = 10000 }


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
                "Math.fixPrecision"
                [
                    test "the float 3000000000.6 with 3 significant digits keeps its whole part" {
                        3000000000.6
                        |> Math.fixPrecision 3
                        |> Expect.equal "the whole number" 3000000001.
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
