namespace Informedica.GenPRES.Client.Core.Models

/// Text blocks as the pages show them.
module TextBlockDisplay =

    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types


    /// The text block constructor of the highest severity among rows of text blocks.
    let maxTb (xs: TextBlock[][]) = xs |> Severity.ofTextBlockRows |> Severity.withItems


    /// Flatten TextBlock[][] to a single-row TextBlock[][] for compact display.
    /// Joins rows with " + " separators and uses the max severity level.
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
