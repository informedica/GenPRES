module Informedica.GenPRES.Shared.Tests.CsvTests

open System
open System.Globalization

open Expecto
open Expecto.Flip

open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models


/// Runs f with the given culture on the current thread, and restores the culture afterwards.
let underCulture (name: string) f =
    let saved = CultureInfo.CurrentCulture

    CultureInfo.CurrentCulture <-
        if name = "" then
            CultureInfo.InvariantCulture
        else
            CultureInfo name

    try
        f ()
    finally
        CultureInfo.CurrentCulture <- saved


/// The cultures a host may run with: the invariant one of a container without a LANG, the Dutch
/// ones of a macOS host with the Netherlands region (English or Dutch language), and two controls.
let cultures = [ ""; "nl-NL"; "en-NL"; "de-DE"; "en-US" ]


let dotnet (x: string) =
    Double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture)


/// The input, whether .NET accepts it, and the value: plain decimals, the forms the browser's
/// JavaScript parse would take and .NET refuses, and the two words .NET takes.
let parseCases =
    [
        "0.0192", true, 0.0192
        "-1.5", true, -1.5
        "+2", true, 2.
        ".5", true, 0.5
        "5.", true, 5.
        "1e3", true, 1000.
        "1E-2", true, 0.01
        " 1.5 ", true, 1.5
        "\t7\n", true, 7.
        "1,5", false, 0.
        "1,000", false, 0.
        "0x10", false, 0.
        "0b101", false, 0.
        "0o7", false, 0.
        "1_000", false, 0.
        "", false, 0.
        "   ", false, 0.
        ".", false, 0.
        "e5", false, 0.
        "1.2.3", false, 0.
        "Infinity", true, Double.PositiveInfinity
        "NaN", true, Double.NaN
    ]


/// The words .NET takes and the plain-decimal check refuses; no sheet value is either.
let words = [ "Infinity"; "NaN" ]


/// Every string of up to four characters over the characters where the .NET and the JavaScript
/// parse can differ.
let shortStrings =
    let alphabet = [ '0'; '1'; '.'; 'e'; '-'; '+'; ','; 'x'; 'b'; '_'; ' ' ]

    let rec strings n =
        if n = 0 then
            [ "" ]
        else
            [
                for s in strings (n - 1) do
                    for c in alphabet do
                        s + string c
            ]

    [ 1..4 ] |> List.collect strings


[<Tests>]
let tests =
    testList
        "Csv float parse"
        [
            testList
                "tryParseFloat gives the answers of .NET"
                [
                    for x, ok, value in parseCases do
                        test $"%A{x} gives %b{ok}" {
                            let actualOk, actual = Csv.tryParseFloat x
                            actualOk |> Expect.equal "accepted" ok

                            if ok && not (Double.IsNaN value) then
                                actual |> Expect.equal "value" value
                        }
                ]

            testList
                "isPlainDecimal"
                [
                    for x, ok, _ in parseCases do
                        let expected = ok && not (words |> List.contains x)

                        test $"%A{x} is a plain decimal: %b{expected}" {
                            Csv.isPlainDecimal x |> Expect.equal "shape" expected
                        }

                    test $"accepts exactly what .NET accepts, over %i{shortStrings.Length} short strings" {
                        shortStrings
                        |> List.filter (fun x -> Csv.isPlainDecimal x <> fst (dotnet x))
                        |> Expect.isEmpty "no string where the two differ"
                    }

                    test "refuses null" { Csv.isPlainDecimal null |> Expect.isFalse "null" }
                ]

            testList
                "the parse reads a decimal point under every culture"
                [
                    for culture in cultures do
                        for text, expected in
                            [
                                "0.0192", 0.0192
                                "3.955666803", 3.955666803
                                "38", 38.
                                "-1.5", -1.5
                                " 2.5 ", 2.5
                            ] do
                            test $"[%s{culture}] %s{text}" {
                                underCulture culture (fun () -> Csv.getFloatColumn [| "x" |] [| text |] "x")
                                |> Expect.equal "parsed with a decimal point" expected
                            }
                ]

            testList
                "the optional parse"
                [
                    for culture in cultures do
                        test $"[%s{culture}] a number is Some, an empty cell and a word are None" {
                            underCulture
                                culture
                                (fun () ->
                                    [ "0.0192"; ""; "abc" ]
                                    |> List.map (fun text -> Csv.getFloatOptionColumn [| "x" |] [| text |] "x")
                                )
                            |> Expect.equal "Some, None, None" [ Some 0.0192; None; None ]
                        }
                ]

            testList
                "the required parse raises on an empty cell and a word"
                [
                    for text in [ ""; "abc" ] do
                        test $"'%s{text}' raises FormatException" {
                            (fun () -> Csv.getFloatColumn [| "x" |] [| text |] "x" |> ignore)
                            |> Expect.throwsT<FormatException> "raises"
                        }
                ]
        ]
