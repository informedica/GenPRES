module Informedica.GenPRES.Shared.Tests.SeverityTests

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Models


let private levels = [ IsNormal; IsCaution; IsWarning; IsAlert ]

let private severities = [ Severity.Normal; Severity.Caution; Severity.Warning; Severity.Alert ]

let private text =
    [|
        TextItem.Normal "10 mg"
        TextItem.Bold "per dag"
        TextItem.Italic "(max 20 mg)"
    |]

let private blockOf severity = Severity.withItems severity text


[<Tests>]
let tests =
    testList
        "Severity"
        [
            testList
                "Level"
                [
                    for level, severity in List.zip levels severities do
                        test $"{level} is {severity}" {
                            level |> Severity.ofLevel |> Expect.equal "of the level" severity
                        }

                    for level in levels do
                        test $"{level} survives the round trip" {
                            level
                            |> Severity.ofLevel
                            |> Severity.toLevel
                            |> Expect.equal "back to the level" level
                        }
                ]

            testList
                "TextBlock"
                [
                    for severity in severities do
                        test $"a {severity} block reads as {severity}" {
                            blockOf severity |> Severity.ofTextBlock |> Expect.equal "of the block" severity
                        }

                    for severity in severities do
                        test $"a {severity} block keeps its text" {
                            blockOf severity |> Severity.items |> Expect.equal "the items" text
                        }

                    for severity in severities do
                        test $"{severity} over text survives the round trip" {
                            blockOf severity |> Severity.ofTextBlock |> Severity.withItems <| text
                            |> Expect.equal "back to the block" (blockOf severity)
                        }

                    test "Valid is Normal, so the empty text block reads as nothing raised" {
                        Valid [||] |> Severity.ofTextBlock |> Expect.equal "normal" Severity.Normal
                    }
                ]

            testList
                "highest"
                [
                    test "none raised when there are none" {
                        Severity.highest [] |> Expect.equal "normal" Severity.Normal
                    }

                    test "the order is Normal < Caution < Warning < Alert" {
                        severities
                        |> List.pairwise
                        |> List.forall (fun (lower, higher) -> lower < higher)
                        |> Expect.isTrue "declared lowest to highest"
                    }

                    test "the highest wins whatever the order given" {
                        [ Severity.Caution; Severity.Alert; Severity.Normal; Severity.Warning ]
                        |> Severity.highest
                        |> Expect.equal "alert" Severity.Alert
                    }

                    test "isRaised is everything above Normal" {
                        severities
                        |> List.map Severity.isRaised
                        |> Expect.equal "normal is not, the rest are" [ false; true; true; true ]
                    }
                ]

            testList
                "TextBlock.maxTb"
                [
                    // maxTb answers a constructor, so it is applied to the same text as the
                    // block it should build
                    let rowsCases =
                        [
                            "no rows", [||], Severity.Normal
                            "one empty row", [| [||] |], Severity.Normal
                            "one valid row", [| [| Valid text |] |], Severity.Normal
                            "a caution among valids",
                            [| [| Valid text; Caution text |]; [| Valid text |] |],
                            Severity.Caution
                            "an alert in the second row",
                            [| [| Warning text |]; [| Alert text; Valid text |] |],
                            Severity.Alert
                            "empty rows beside a warning", [| [||]; [| Warning text |]; [||] |], Severity.Warning
                        ]

                    for name, rows, expected in rowsCases do
                        test $"{name}: the block of the highest severity" {
                            TextBlock.maxTb rows text
                            |> Expect.equal "the highest over the rows" (blockOf expected)
                        }

                    test "a flat array reads as its highest block" {
                        [| Valid text; Warning text; Caution text |]
                        |> Severity.ofTextBlocks
                        |> Expect.equal "warning" Severity.Warning
                    }
                ]
        ]
