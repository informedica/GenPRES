// One severity type, and the conversions from what the wire carries (plan 981, step F1a).
//
// The client shows how far a value stands from what the rules allow in four places that do not
// convert to one another: `Level` on an order variable (`IsNormal | IsCaution | IsWarning |
// IsAlert`, no payload), the four cases of `TextBlock` on the formulary path (`Valid | Caution |
// Warning | Alert of TextItem[]`, the text as payload), the colour table in
// `Views/ViewHelpers.getWarning` and the one in `Mui.TypoGraphy.fromTextBlock`, and the three
// hexes `Pages/GenPres.fs` paints behind the menu. `Models.TextBlock.maxTb` orders the four
// `TextBlock` cases by hand, with integers.
//
// Script-first draft (script-only policy) of the one type and its module, → `Shared/Types.fs`
// (the type, beside `Level`) and `Shared/Models.fs` (the module, beside `TextBlock`). `Level`
// and `TextBlock` stay on the wire as they are; a renderer converts at the edge (step F1b),
// which is why every conversion has a test here. The colours are not in this script: `Shared`
// knows no palette, and F1b maps a `Severity` to the tokens of `Mui.Styles`.
//
// Run: `dotnet fsi Severity.fsx` from this directory, or via the FSI MCP after
// `#I "<this directory>"`.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"

open Shared.Types


// ---------------------------------------------------------------------------------------------
// → Shared/Types.fs, beside Level
// ---------------------------------------------------------------------------------------------

/// How far what is shown stands from what the rules allow: nothing, a note, a warning, an alert.
/// The cases are declared from lowest to highest, so the highest of a set is their maximum.
[<RequireQualifiedAccess>]
type Severity =
    | Normal
    | Caution
    | Warning
    | Alert


// ---------------------------------------------------------------------------------------------
// → Shared/Models.fs, beside the TextBlock module
// ---------------------------------------------------------------------------------------------

/// Conversions between the one severity and the two shapes the wire carries it in.
[<RequireQualifiedAccess>]
module Severity =

    /// The severity an order variable carries.
    let ofLevel (level: Level) =
        match level with
        | IsNormal -> Severity.Normal
        | IsCaution -> Severity.Caution
        | IsWarning -> Severity.Warning
        | IsAlert -> Severity.Alert


    /// The level an order variable carries for a severity.
    let toLevel (severity: Severity) =
        match severity with
        | Severity.Normal -> IsNormal
        | Severity.Caution -> IsCaution
        | Severity.Warning -> IsWarning
        | Severity.Alert -> IsAlert


    /// The severity a text block carries.
    let ofTextBlock (block: TextBlock) =
        match block with
        | Valid _ -> Severity.Normal
        | Caution _ -> Severity.Caution
        | Warning _ -> Severity.Warning
        | Alert _ -> Severity.Alert


    /// The text a text block carries, whatever its severity.
    let items (block: TextBlock) =
        match block with
        | Valid items
        | Caution items
        | Warning items
        | Alert items -> items


    /// The text block of a severity over some text.
    let withItems (severity: Severity) (items: TextItem[]) =
        match severity with
        | Severity.Normal -> Valid items
        | Severity.Caution -> Caution items
        | Severity.Warning -> Warning items
        | Severity.Alert -> Alert items


    /// The highest of some severities; nothing raised when there are none.
    let highest (severities: Severity seq) =
        if severities |> Seq.isEmpty then
            Severity.Normal
        else
            severities |> Seq.max


    /// The highest severity among some text blocks.
    let ofTextBlocks (blocks: TextBlock[]) =
        blocks |> Seq.map ofTextBlock |> highest


    /// The highest severity among rows of text blocks; an empty row counts as nothing raised.
    let ofTextBlockRows (rows: TextBlock[][]) =
        rows |> Seq.collect (Seq.map ofTextBlock) |> highest


    /// Whether a severity is anything above normal: what gets a mark.
    let isRaised (severity: Severity) = severity <> Severity.Normal


// ---------------------------------------------------------------------------------------------
// What the existing sites become, once the module is migrated. Not run here; shown so the
// migration and step F1b read from one place.
// ---------------------------------------------------------------------------------------------
//
// Models.TextBlock.maxTb xs           =  xs |> Severity.ofTextBlockRows |> Severity.withItems
// Formulary.isAllValid blocks         =  blocks |> Severity.ofTextBlocks = Severity.Normal
// GenPres.formularyBg (the three ifs) =  match form.DoseCheck |> Severity.ofTextBlocks with ...
// ViewHelpers.getWarning level        =  level |> Severity.ofLevel |> (F1b: severity -> token)
// Mui.TypoGraphy.fromTextBlock's
//     items, color, hasWarning        =  Severity.items tb, (F1b: token), Severity.isRaised


// ---------------------------------------------------------------------------------------------
// Tests: one per conversion, and the two order-preserving ones against what Models does today.
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip

let levels = [ IsNormal; IsCaution; IsWarning; IsAlert ]
let severities = [ Severity.Normal; Severity.Caution; Severity.Warning; Severity.Alert ]
let text = [| TextItem.Normal "10 mg"; TextItem.Bold "per dag"; TextItem.Italic "(max 20 mg)" |]

let blockOf severity = Severity.withItems severity text

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
                            blockOf severity
                            |> Severity.ofTextBlock
                            |> Expect.equal "of the block" severity
                        }

                    for severity in severities do
                        test $"a {severity} block keeps its text" {
                            blockOf severity |> Severity.items |> Expect.equal "the items" text
                        }

                    for severity in severities do
                        test $"{severity} over text survives the round trip" {
                            blockOf severity
                            |> Severity.ofTextBlock
                            |> Severity.withItems
                            <| text
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
                "against Models.TextBlock.maxTb"
                [
                    // maxTb answers a constructor, so both sides are applied to the same text
                    let rowsCases =
                        [
                            "no rows", [||]
                            "one empty row", [| [||] |]
                            "one valid row", [| [| Valid text |] |]
                            "a caution among valids", [| [| Valid text; Caution text |]; [| Valid text |] |]
                            "an alert in the second row", [| [| Warning text |]; [| Alert text; Valid text |] |]
                            "empty rows beside a warning", [| [||]; [| Warning text |]; [||] |]
                        ]

                    for name, rows in rowsCases do
                        test $"{name}: the same block maxTb builds" {
                            let expected = Shared.Models.TextBlock.maxTb rows text
                            rows
                            |> Severity.ofTextBlockRows
                            |> Severity.withItems
                            <| text
                            |> Expect.equal "what maxTb builds" expected
                        }

                    test "a flat array reads as its highest block" {
                        [| Valid text; Warning text; Caution text |]
                        |> Severity.ofTextBlocks
                        |> Expect.equal "warning" Severity.Warning
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
