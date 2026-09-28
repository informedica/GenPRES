// The Csv float parser reads the sheet numbers the same on every host (issue #1153).
//
// Csv.tryCast in Utils.fs parses a float column with Double.TryParse(x), which uses the current
// culture. Under a culture that writes the decimal comma, the dot of the sheet is a group separator
// and is dropped: the age 0.0192 of the normal-values sheet reads as 192, the mean 3.955666803 as
// 3955666803, and every age is estimated at the maxima of the option lists, 100 kg and 220 cm.
// The browser is not affected, since Fable compiles the call to a JavaScript parse with a dot; the
// server and the MCP host are, for every launched or MCP patient without a measured weight or height.
//
// The parse below reads the number with NumberStyles.Float and the invariant culture, as the Utils
// library does in Double.tryParse; the contract project cannot reference that library, so the two
// lines are repeated here. The tests set the culture per case and restore it afterwards.
//
// Run: `dotnet fsi Csv.fsx` from this directory, or via the FSI MCP after `#I "<this directory>"`.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"

open System
open System.Globalization

open Expecto
open Expecto.Flip

open Shared
open Shared.Types


module Csv =

    open Shared.Csv


    /// The number as the sheet writes it, with a decimal point, whatever the host's culture.
    let tryParseFloat (x: string) =
        Double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture)


    let tryCast dt (x: string) =
        match dt with
        | StringData -> box (x.Trim())
        | FloatData ->
            match tryParseFloat x with
            | true, n -> n |> box
            | _ -> raise (FormatException $"cannot parse {x} to double")
        | FloatOptionData ->
            match tryParseFloat x with
            | true, n -> n |> Some |> box
            | _ -> None |> box


    // The column readers close over tryCast, so they are repeated to reach the parse above.
    let getColumn dt columns sl s =
        columns
        |> Array.tryFindIndex ((=) s)
        |> function
            | None ->
                raise (
                    System.Collections.Generic.KeyNotFoundException
                        $"""cannot find column {s} in {columns |> String.concat ", "}"""
                )
            | Some i -> sl |> Array.item i |> tryCast dt


    let getStringColumn columns sl s = getColumn StringData columns sl s |> unbox<string>


    let getFloatColumn columns sl s = getColumn FloatData columns sl s |> unbox<float>


    let getFloatOptionColumn columns sl s = getColumn FloatOptionData columns sl s |> unbox<float option>


module NormalValues =

    open Shared.Models.NormalValues


    // Repeated from Models.fs so that the sheet rows reach the parse above.
    let parse (data: string[][]) =
        match data with
        | data when data |> Array.length > 1 ->
            let cms = data |> Array.head

            data
            |> Array.skip 1
            |> Array.map (fun sl ->
                let getString n = Csv.getStringColumn cms sl n |> String.trim

                let getFloat = Csv.getFloatColumn cms sl

                create (getString "sex") (getFloat "age") (getFloat "p3") (getFloat "mean") (getFloat "p97")
            )
            |> Array.toList
        | _ -> []


    let ofRows (rows: Map<string, string[][]>) : NormalValues =
        let table sheet =
            rows |> Map.tryFind sheet |> Option.map parse |> Option.defaultValue []

        {
            Weights = table "weight"
            Heights = table "height"
            NeoWeights = table "weight neo"
            NeoHeights = table "height neo"
        }


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


/// Rows as the normal-values sheets deliver them: a header and the numbers with a decimal point.
/// The ages and the six-day mean are those of the issue; every ten-year value carries a decimal
/// too, so that the current parser reads it a tenfold and the estimate hits the maxima.
let rows =
    let weight =
        [|
            [| "sex"; "age"; "p3"; "mean"; "p97" |]
            [| "M"; "0.0192"; "3.1"; "3.955666803"; "4.7" |]
            [| "M"; "10"; "26.4"; "38.2"; "56.1" |]
            [| "F"; "0.0192"; "3.0"; "3.8"; "4.5" |]
            [| "F"; "10"; "25.3"; "37.1"; "55.2" |]
        |]

    let height =
        [|
            [| "sex"; "age"; "p3"; "mean"; "p97" |]
            [| "M"; "0.0192"; "50"; "54"; "58" |]
            [| "M"; "10"; "130.5"; "150.3"; "160.2" |]
            [| "F"; "0.0192"; "49"; "53"; "57" |]
            [| "F"; "10"; "129.4"; "149.1"; "159.3" |]
        |]

    Map.ofList [ "weight", weight; "height", height ]


let boyAged (age: Patient.Age) =
    { Models.Patient.empty with
        Age = Some age
        Gender = Male
    }


let sixDays = { Models.Patient.Age.ageZero with Days = 6<day> }
let tenYears = { Models.Patient.Age.ageZero with Years = 10<year> }


let estimates (nv: NormalValues) (pat: Patient) =
    let pat = pat |> Models.NormalValues.apply nv
    pat.Weight.Estimated, pat.Height.Estimated


let tests =
    testList
        "Csv float parse (#1153)"
        [
            testList
                "the parse reads a decimal point under every culture"
                [
                    for culture in cultures do
                        for text, expected in [ "0.0192", 0.0192; "3.955666803", 3.955666803; "38", 38.; "-1.5", -1.5; " 2.5 ", 2.5 ] do
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
                            underCulture culture (fun () ->
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

            test "reproduces the issue: the current parser reads 0.0192 as 192 under nl-NL" {
                underCulture "nl-NL" (fun () -> Shared.Csv.getFloatColumn [| "x" |] [| "0.0192" |] "x")
                |> Expect.equal "the dot is dropped as a group separator" 192.
            }

            testList
                "the estimate is the same under every culture"
                [
                    for culture in cultures do
                        test $"[%s{culture}] six days: 4.0 kg and 54 cm; ten years: 38 kg and 150 cm" {
                            let nv = underCulture culture (fun () -> NormalValues.ofRows rows)

                            (sixDays |> boyAged |> estimates nv, tenYears |> boyAged |> estimates nv)
                            |> Expect.equal
                                "the estimates of the issue"
                                ((Some 4000<gram>, Some 54<cm>), (Some 38000<gram>, Some 150<cm>))
                        }
                ]

            test "reproduces the issue: the current parser estimates 100 kg and 220 cm under nl-NL" {
                let nv = underCulture "nl-NL" (fun () -> Shared.Models.NormalValues.ofRows rows)

                (sixDays |> boyAged |> estimates nv, tenYears |> boyAged |> estimates nv)
                |> Expect.equal
                    "the maxima of the option lists"
                    ((Some 100000<gram>, Some 220<cm>), (Some 100000<gram>, Some 220<cm>))
            }
        ]


runTestsWithCLIArgs [] [||] tests
