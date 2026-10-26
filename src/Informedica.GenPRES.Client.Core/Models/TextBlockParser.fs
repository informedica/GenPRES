namespace Informedica.GenPRES.Client.Core.Models

/// Reads the text of the two medication lists into a text block.
module TextBlockParser =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open System.Text.RegularExpressions

    /// Convert a string to a Valid TextBlock with numbers shown as Bold TextItems
    let fromString (text: string) : TextBlock =
        if text |> String.isNullOrWhiteSpace then
            Valid [| Normal "" |]
        else
            // Split text into parts where numbers (including decimals, commas, and hyphens) are separated
            let pattern = @"(\d+[\d,.\-]*\d*|\d+)"
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
