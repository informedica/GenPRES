// Test script for parseTextItem function

type TextItem =
    | Normal of string
    | Bold of string
    | Italic of string

module String =
    let isNullOrWhiteSpace (s: string) = System.String.IsNullOrWhiteSpace(s)

let parseTextItem (s: string) =
    s
    |> Seq.map (id >> string)
    |> Seq.fold
        (fun acc c ->
            if s |> String.isNullOrWhiteSpace then
                acc
            else
                match c, acc |> fst with
                | s, Normal _ when s = "#" -> Bold "", (acc |> fst) :: (acc |> snd)
                | s, Italic _ when s = "#" -> Bold s, (acc |> fst) :: (acc |> snd)
                | s, Bold _ when s = "#" -> Normal "", (acc |> fst) :: (acc |> snd)
                | s, Bold b -> Bold $"{b}{s}", (acc |> snd)

                | s, Normal _ when s = "|" -> Italic "", (acc |> fst) :: (acc |> snd)
                | s, Italic _ when s = "|" -> Normal "", (acc |> fst) :: (acc |> snd)
                | s, Italic i -> Italic $"{i}{s}", (acc |> snd)

                | s2, Normal s1 -> Normal $"{s1}{s2}", acc |> snd

        )
        (Normal "", [])
    |> fun (md, acc) -> md :: acc
    |> Seq.rev
    |> Seq.filter (fun ti ->
        match ti with
        | Bold s
        | Italic s
        | Normal s -> s |> String.isNullOrWhiteSpace |> not
    )
    |> Seq.toArray

// Test the current function
let testInput = "#1# |ml|"
let result = parseTextItem testInput

printfn "Input: %s" testInput
printfn "Result: %A" result
printfn "Expected: [|Bold \"1\"; Italic \"ml\"|]"