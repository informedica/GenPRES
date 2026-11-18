namespace Informedica.GenPRES.Client.Core.Models

/// Text blocks as the pages show them.
module TextBlockDisplay =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    /// Returns the function that wraps text items in a text block of the highest severity in the rows.
    let maxTb (xs: TextBlock[][]) = xs |> Severity.ofTextBlockRows |> Severity.withItems


    /// Joins all text blocks into one text block with the highest severity, with a plus sign between the
    /// rows, for a compact display. Returns an empty array unchanged.
    let flatten (blocks: TextBlock[][]) : TextBlock[][] =
        if blocks |> Array.isEmpty then
            blocks
        else
            let getItems tb = tb |> Severity.items |> Array.append [| " " |> Normal |]

            let add xs =
                let plus = [| [| " + " |> Normal |] |]

                xs
                |> Array.fold
                    (fun acc x ->
                        if acc |> Array.isEmpty then
                            x
                        else
                            x |> Array.append plus |> Array.append acc
                    )
                    [||]
                |> Array.collect id

            blocks
            |> Array.map (Array.map getItems)
            |> add
            |> (blocks |> maxTb)
            |> Array.singleton
            |> Array.singleton
