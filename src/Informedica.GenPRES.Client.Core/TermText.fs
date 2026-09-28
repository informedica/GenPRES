/// Builds the text a page shows from translated terms: placeholders filled in, sentences joined.
module TermText

open Shared


/// Fills {0}, {1}, ... in a translated sentence with the given values, in order.
let fill (args: string list) (s: string) =
    args
    |> List.indexed
    |> List.fold (fun s (i, a) -> s |> String.replace $"{{{i}}}" a) s


/// Joins translated sentences into one body; an empty translation adds no sentence.
let sentences (xs: string list) = xs |> List.filter String.notEmpty |> String.concat " "
