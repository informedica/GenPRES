// Test script for refactored parseTextItem function

open System

type TextItem =
    | Normal of string
    | Bold of string
    | Italic of string

module String =
    let isNullOrWhiteSpace (s: string) = String.IsNullOrWhiteSpace(s)

// New implementation
module OrderScenario =

    /// Configuration for text item delimiters
    /// Each delimiter maps to a constructor function and its delimiter character
    type private DelimiterConfig =
        {
            Delimiter: string
            Constructor: string -> TextItem
            IsActive: TextItem -> bool
        }

    let parseTextItem (s: string) =
        if s |> String.isNullOrWhiteSpace then
            [||]
        else
            // Define delimiter configurations - easy to extend with new cases
            let delimiters =
                [
                    { Delimiter = "#"; Constructor = Bold; IsActive = function Bold _ -> true | _ -> false }
                    { Delimiter = "|"; Constructor = Italic; IsActive = function Italic _ -> true | _ -> false }
                ]

            /// Get the text content from a TextItem
            let getText = function
                | Normal s | Bold s | Italic s -> s

            /// Check if a delimiter is active for the current state
            let tryFindActiveDelimiter char currentItem =
                delimiters
                |> List.tryFind (fun d -> d.Delimiter = char && d.IsActive currentItem)

            /// Check if a character is any delimiter
            let tryFindDelimiter char =
                delimiters
                |> List.tryFind (fun d -> d.Delimiter = char)

            /// Process each character through the state machine
            let processChar (currentItem, completedItems) char =
                match tryFindActiveDelimiter char currentItem with
                | Some _ ->
                    // Toggle off: return to Normal state
                    Normal "", currentItem :: completedItems
                | None ->
                    match tryFindDelimiter char with
                    | Some config ->
                        // Toggle on: switch to new state
                        config.Constructor "", currentItem :: completedItems
                    | None ->
                        // Regular character: append to current item
                        let currentText = getText currentItem
                        let newItem =
                            match currentItem with
                            | Normal _ -> Normal (currentText + char)
                            | Bold _ -> Bold (currentText + char)
                            | Italic _ -> Italic (currentText + char)
                        newItem, completedItems

            s
            |> Seq.map string
            |> Seq.fold processChar (Normal "", [])
            |> fun (lastItem, items) -> lastItem :: items
            |> List.rev
            |> List.filter (fun item -> item |> getText |> String.isNullOrWhiteSpace |> not)
            |> List.toArray

// Test cases
let testCases =
    [
        "#1# |ml|", [|Bold "1"; Italic "ml"|]
        "Normal text", [|Normal "Normal text"|]
        "#Bold text#", [|Bold "Bold text"|]
        "|Italic text|", [|Italic "Italic text"|]
        "Start #bold# middle |italic| end", [|Normal "Start "; Bold "bold"; Normal " middle "; Italic "italic"; Normal " end"|]
        "#bold |and italic|#", [|Bold "bold "; Italic "and italic"|]
        "", [||]
        "   ", [||]
    ]

printfn "Testing refactored parseTextItem function:\n"

testCases
|> List.iteri (fun i (input, expected) ->
    let result = OrderScenario.parseTextItem input
    let passed = result = expected
    let status = if passed then "✓ PASS" else "✗ FAIL"
    
    printfn "Test %d: %s" (i + 1) status
    printfn "  Input:    %A" input
    printfn "  Expected: %A" expected
    printfn "  Got:      %A" result
    if not passed then
        printfn "  ❌ Mismatch!"
    printfn ""
)

printfn "\n=== Extension Example ==="
printfn "To add a new TextItem case (e.g., Underline with '~'):"
printfn "1. Add the case to TextItem type: | Underline of string"
printfn "2. Add to delimiters list: { Delimiter = \"~\"; Constructor = Underline; IsActive = function Underline _ -> true | _ -> false }"
printfn "3. Add Underline pattern to getText function: | Underline s -> s"
printfn "4. Add Underline pattern to processChar's newItem match: | Underline _ -> Underline (currentText + char)"
