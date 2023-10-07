
#load "load.fsx"


#time



open MathNet.Numerics
open Informedica.Utils.Lib
open Informedica.Utils.Lib.BCL
open Informedica.GenForm.Lib
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib


let vu1 =
    [|1N..1N..100N|]
    |> ValueUnit.withUnit Units.Count.times
let vu2 =
    [|1N..1N..10N|]
    |> ValueUnit.withUnit Units.Count.times

(vu1 * vu2)
|> ValueUnit.getValue
|> Array.length



module List =

    let prune maxLength xs =
        let l = xs |> List.length

        if (l - maxLength) <= 0 || l <= 2 then xs
        else
            let d = l / (maxLength - 2)
            xs
            |> List.fold (fun (i, acc) x ->
                if i = 1 || i = l || i % d = 0 then
                    printfn $"keep {x}"
                    (i + 1, [x] |> List.append acc)
                else
                    (i + 1, acc)
            ) (1, [])
            |> snd


[1..1..100]
|> List.prune 10
|> List.length



module Array =

    let prune maxLength xs =
        let l = xs |> Array.length

        if (l - maxLength) <= 0 || l <= 2 then xs
        else
            let d = l / (maxLength - 2)
            xs
            |> Array.fold (fun (i, acc) x ->
                if i = 1 || i = l || i % d = 0 then
                    printfn $"keep {x}"
                    (i + 1, [| x |] |> Array.append acc)
                else
                    (i + 1, acc)
            ) (1, [||])
            |> snd
