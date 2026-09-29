// The answers Csv.tryParseFloat must give in the browser, for TryParseFloat.check.mjs (#1176).
//
// Fable drops the NumberStyles and the culture of Double.TryParse and parses as JavaScript
// does, so Csv.tryParseFloat checks the text is a plain decimal first. The tests in
// tests/Informedica.GenPRES.Shared.Tests/CsvTests.fs hold the .NET side; the browser's side is
// only seen in the compiled JavaScript, which TryParseFloat.check.mjs runs against the answers
// this script writes to TryParseFloat.expected.json: those of .NET, bar Infinity and NaN, which
// the browser refuses. The JSON file is generated and not tracked: run this script first.
//
// Run: `dotnet fsi TryParseFloat.fsx` from this directory.

#I __SOURCE_DIRECTORY__

open System
open System.Globalization


/// What .NET answers, with NumberStyles.Float under the invariant culture.
let dotnet (x: string) =
    Double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture)


/// Plain decimals, the forms JavaScript takes and .NET refuses, and the words .NET takes.
let cases =
    [
        "0.0192"
        "-1.5"
        "+2"
        ".5"
        "5."
        "1e3"
        "1E-2"
        " 1.5 "
        "\t7\n"
        "1,5"
        "1,000"
        "0x10"
        "0b101"
        "0o7"
        "1_000"
        ""
        "   "
        "."
        "e5"
        "1.2.3"
        "Infinity"
        "-Infinity"
        "NaN"
    ]


/// Every string of up to four characters over the characters where the two parsers can differ.
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


/// The words .NET takes and the browser refuses.
let words = [ "Infinity"; "-Infinity"; "NaN" ]


/// The answers the browser must give, as [input, accepted, value] rows.
let expected =
    cases @ shortStrings
    |> List.distinct
    |> List.map (fun x ->
        match dotnet x with
        | true, value when not (words |> List.contains (x.Trim())) -> [| box x; box true; box value |]
        | _ -> [| box x; box false; box 0. |]
    )
    |> List.toArray


let path = IO.Path.Combine(__SOURCE_DIRECTORY__, "TryParseFloat.expected.json")

IO.File.WriteAllText(path, Text.Json.JsonSerializer.Serialize expected)
printfn $"%i{expected.Length} expected answers written to %s{path}"
