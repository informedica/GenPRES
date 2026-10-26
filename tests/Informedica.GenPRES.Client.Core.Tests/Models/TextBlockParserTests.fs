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
        ]
