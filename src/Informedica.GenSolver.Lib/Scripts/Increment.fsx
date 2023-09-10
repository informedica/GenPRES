
#r "nuget: Unquote"

#load "load.fsx"

open MathNet.Numerics
open Swensen.Unquote

open Informedica.Utils.Lib.BCL

let x = [2N..2N..10N]
let y = [3N..3N..12N]


let calc op x y =
    x
    |> List.allPairs y
    |> List.map (fun (x1, x2) -> x1 |> op <| x2)
    |> List.sort
    |> List.distinct


let minIncrMaxToList min incr max : BigRational list =
    incr
    |> List.fold (fun acc i ->
        let min = min |> BigRational.toMinMultipleOf i
        let max = max |> BigRational.toMaxMultipleOf i
        [min..i..max] @ acc
    ) []
    |> List.sort
    |> List.distinct


let calcIncr op x y =
    let r =
        x
        |> List.allPairs y
        |> List.map (fun (x1, x2) -> x1 |> op <| x2)

    r |> List.min,
    r
    |> List.toArray
    |> Informedica.GenUnits.Lib.Array.removeBigRationalMultiples
    |> Array.toList,
    r |> List.max


let calcIncrDiv = calcIncr (/)
let calcIncrMul = calcIncr (*)
let calcIncrAdd = calcIncr (+)
let calcIncrSub = calcIncr (-)


calcIncrMul x y |> fun (min, incr, max) -> minIncrMaxToList min incr max
calcIncrDiv x y
calcIncrAdd x y
calcIncrSub x y



test <@
    let x = [3N..3N..12N]
    let y = [2N..2N..10N]
    let exp = calc (*) x y
    let act = calcIncrMul x y |> fun (min, incr, max) -> minIncrMaxToList min incr max
    exp <> act &&
    Set.isSubset (exp |> Set.ofList) (act |> Set.ofList)
@>


test <@
    let x = [3N..3N..12N]
    let y = [2N..2N..10N]
    let exp = calc (/) x y
    let act = calcIncrDiv x y |> fun (min, incr, max) -> minIncrMaxToList min incr max
    exp <> act &&
    Set.isSubset (exp |> Set.ofList) (act |> Set.ofList)
@>


test <@
    let x = [3N..3N..12N]
    let y = [2N..2N..10N]
    let exp = calc (+) x y
    let act = calcIncrAdd x y |> fun (min, incr, max) -> minIncrMaxToList min incr max
    exp <> act &&
    Set.isSubset (exp |> Set.ofList) (act |> Set.ofList)
@>

