namespace Informedica.Utils.Lib


[<RequireQualifiedAccess>]
module Array =


    open Informedica.Utils.Lib.BCL


    let prepend xs1 xs2 = xs1 |> Array.append xs2


    let pickArray pl xs =
        match xs with
        | [||] -> xs
        | _ ->
            [| for i in pl -> xs |> Array.item i |]


    let arrayFilter p xs =
        xs
        |> Array.filter (fun r ->
            r |> Array.exists (fun x -> p x ))


    let collectArrays p xs =
        xs
        |> Array.collect (fun r ->
            r
            |> Array.filter (fun x -> p x))


    let inline toString_ left right del xs =
        match xs with
        | [||] -> $"{left}{right}"
        | _ ->
            let del = $"{del}"
            let lng = del |> String.length
            let s =
                xs
                |> Array.fold (fun s x -> s + string x + del) left
            (s |> String.subString 0 ((s |> String.length) - lng)) + right


    let inline toString xs = xs |> toString_ "[|" "|]" ";"


    let inline toReadableString xs = xs |> toString_ "" "" ", "


    let allEqual succ fail xs =
        if xs   |> Array.length = 0 then fail
        elif xs |> Array.length = 1 then succ xs.[0]
        else
            let x = xs.[0]
            if xs |> Array.forall ((=) x) then succ x
            else fail


    let allEqualToString xs = xs |> allEqual string ""


    let allEqualToOpt xs = xs |> allEqual Some None


    let someIfOne = function
        | [|x|] -> Some x
        | _   -> None