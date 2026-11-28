namespace Informedica.GenPRES.Client.Core.Models

/// Reads the text of the two medication lists into a text block.
module TextBlockParser =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open System.Text.RegularExpressions

    /// Converts a text to a Valid text block with the numbers in bold. A number keeps the narrow no-break spaces
    /// between its thousands. An empty text gives an empty item.
    let fromString (text: string) : TextBlock =
        if text |> String.isNullOrWhiteSpace then
            Valid [| Normal "" |]
        else
            // a number with decimals, commas and hyphens, and a narrow no-break space before each group of
            // three digits, such as 1 100
            let pattern = @"(\d+(?:\u202F\d{3})*[\d,.\-]*\d*|\d+)"
            let matches = Regex.Matches(text, pattern)

            let rec buildItems (pos: int) (matchIdx: int) (acc: TextItem list) =
                if matchIdx >= matches.Count then
                    // Add remaining text after last match
                    if pos < text.Length then
                        let remaining = text.Substring(pos)

                        if remaining |> String.notEmpty then
                            Normal remaining :: acc
                        else
                            acc
                    else
                        acc
                else
                    let m = matches[matchIdx]

                    let items =
                        // Add text before match
                        if m.Index > pos then
                            let before = text.Substring(pos, m.Index - pos)

                            if before |> String.notEmpty then
                                Bold m.Value :: Normal before :: acc
                            else
                                Bold m.Value :: acc
                        else
                            Bold m.Value :: acc

                    buildItems (m.Index + m.Length) (matchIdx + 1) items

            let items = buildItems 0 0 []

            let finalItems =
                if items |> List.isEmpty then
                    [| Normal text |]
                else
                    items |> List.rev |> List.toArray

            Valid finalItems
