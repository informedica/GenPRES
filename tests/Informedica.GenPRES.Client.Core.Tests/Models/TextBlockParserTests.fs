/// The text of the two medication lists as a text block: numbers bold, the rest normal.
module Informedica.GenPRES.Client.Core.Tests.Models.TextBlockParserTests

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Client.Core.Models


[<Tests>]
let tests =
    testList
        "TextBlockParser"
        [
            test "an empty or blank text is one empty normal item" {
                for s in [ ""; "  "; null ] do
                    s
                    |> TextBlockParser.fromString
                    |> Expect.equal $"'%s{s}'" (Valid [| Normal "" |])
            }

            test "a text without numbers stays one normal item" {
                "geen getal"
                |> TextBlockParser.fromString
                |> Expect.equal "normal" (Valid [| Normal "geen getal" |])
            }

            test "numbers are bold, the text between them normal" {
                "1 mL/uur = 0,5 mg/kg/uur"
                |> TextBlockParser.fromString
                |> Expect.equal "split" (Valid [| Bold "1"; Normal " mL/uur = "; Bold "0,5"; Normal " mg/kg/uur" |])
            }

            test "a range with a hyphen is one bold item" {
                "0,1-0,5 mg"
                |> TextBlockParser.fromString
                |> Expect.equal "range" (Valid [| Bold "0,1-0,5"; Normal " mg" |])
            }

            test "a number with a narrow no-break space between its thousands is one bold item" {
                "1\u202F100 mg (100 mg/kg)"
                |> TextBlockParser.fromString
                |> Expect.equal
                    "one number"
                    (Valid [| Bold "1\u202F100"; Normal " mg ("; Bold "100"; Normal " mg/kg)" |])
            }

            test "a range of numbers with thousands is two bold items" {
                "2\u202F475 - 3\u202F025 mg"
                |> TextBlockParser.fromString
                |> Expect.equal
                    "two numbers"
                    (Valid [| Bold "2\u202F475"; Normal " - "; Bold "3\u202F025"; Normal " mg" |])
            }

            test "a number with millions keeps every group" {
                "3\u202F000\u202F000 IE"
                |> TextBlockParser.fromString
                |> Expect.equal "one number" (Valid [| Bold "3\u202F000\u202F000"; Normal " IE" |])
            }

            test "a number with thousands and decimals is one bold item" {
                "12\u202F000,5 mL"
                |> TextBlockParser.fromString
                |> Expect.equal "one number" (Valid [| Bold "12\u202F000,5"; Normal " mL" |])
            }
        ]
