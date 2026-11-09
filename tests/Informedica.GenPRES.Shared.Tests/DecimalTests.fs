/// The precision of a decimal: the decimals a number of significant digits needs, and the decimal
/// rounded to them.
module Informedica.GenPRES.Shared.Tests.DecimalTests

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Shared


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
        ]
