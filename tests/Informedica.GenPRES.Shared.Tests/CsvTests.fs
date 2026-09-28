module Informedica.GenPRES.Shared.Tests.CsvTests

open System
open System.Globalization

open Expecto
open Expecto.Flip

open Shared
open Shared.Types
open Shared.Models


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
/// The ages and the six-day mean are those of issue #1153; every ten-year value carries a decimal
/// too, so that a parse with the current culture reads it a tenfold and the estimate hits the maxima.
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
    { Patient.empty with
        Age = Some age
        Gender = Male
    }


let sixDays = { Patient.Age.ageZero with Days = 6<day> }
let tenYears = { Patient.Age.ageZero with Years = 10<year> }


let estimates (nv: NormalValues) (pat: Patient) =
    let pat = pat |> NormalValues.apply nv
    pat.Weight.Estimated, pat.Height.Estimated


[<Tests>]
let tests =
    testList
        "Csv float parse"
        [
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
        ]
