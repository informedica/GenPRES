
#load "load.fsx"

#time


module TestSolver =

    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib

    module Api = Api
    module Solver = Solver
    module Name = Variable.Name
    module ValueRange = Variable.ValueRange
    module Minimum = ValueRange.Minimum
    module Maximum = ValueRange.Maximum
    module Increment = ValueRange.Increment
    module ValueSet = ValueRange.ValueSet


    let (|>>) r f =
        match r with
        | Ok x -> x |> f
        | Error _ -> r


    let procss s = printfn $"%s{s} "


    let printEqs = function
        | Ok eqs -> eqs |> Solver.printEqs true procss
        | Error _ -> failwith "errors"


    let printEqsWithUnits = function
        | Ok eqs -> eqs |> Solver.printEqs false procss
        | Error _ -> failwith "errors"


    let setProp n p eqs =
        let n = n |> Name.createExc
        match eqs |> Api.setVariableValues true n p with
        | Some var ->
            eqs
            |> List.map (fun e ->
                e |> Equation.replace var
            )
        | None -> eqs

    let create c u v =
        [|v|]
        |> ValueUnit.create u
        |> c

    let createMinIncl = create (Minimum.create true)
    let createMinExcl = create (Minimum.create false)
    let createMaxIncl = create (Maximum.create true)
    let createMaxExcl = create (Maximum.create false)
    let createIncr = create Increment.create
    let createValSet u v =
        v
        |> Array.ofSeq
        |> ValueUnit.create u
        |> ValueSet.create

    let setIncr u n vals = vals |> createIncr u |> IncrProp |> setProp n
    let setMinIncl u n min = min |> createMinIncl u |> MinProp|> setProp n
    let setMinExcl u n min = min |> createMinExcl u |> MinProp |> setProp n
    let setMaxIncl u n max = max |> createMaxIncl u |> MaxProp |> setProp n
    let setMaxExcl u n max = max |> createMaxExcl u |> MaxProp |> setProp n
    let setValues u n vals = vals |> createValSet u |> ValsProp |> setProp n

    let logger =
        fun (s : string) ->
            printfn $"{s}"
        |> SolverLogging.logger

    let solve n p eqs =
        let n = n |> Name.createExc
        Api.solve true id logger n p eqs

    let solveAll = Api.solveAll false logger

    let solveMinMax = Api.solveAll true logger

    let solveMinIncl u n min = solve n (min |> createMinIncl u |> MinProp)
    let solveMinExcl u n min = solve n (min |> createMinExcl u  |> MinProp)
    let solveMaxIncl u n max = solve n (max |> createMaxIncl u |> MaxProp)
    let solveMaxExcl u n max = solve n (max |> createMaxExcl u |> MaxProp)
    let solveIncr u n incr = solve n (incr |> createIncr u |> IncrProp)
    let solveValues u n vals = solve n (vals |> createValSet u |> ValsProp)

    let init = Api.init
    let nonZeroNegative = Api.nonZeroNegative


    let solveCountMinIncl = solveMinIncl Units.Count.times
    let solveCountMaxExcl = solveMaxExcl Units.Count.times
    let solveCountValues u n vals = solveValues Units.Count.times u n vals



open MathNet.Numerics

open Informedica.GenSolver.Lib
open Informedica.GenUnits.Lib

let eqs =
    [
        "a = b + c"
        "d = e * a"
        "d = f * b"
    ]
    |> Api.init


eqs
//|> TestSolver.setValues Units.Count.times "a" [| 1N..2N..100N |]
|> TestSolver.setMinIncl Units.Count.times "b" 1N
|> TestSolver.setMaxIncl Units.Count.times "b" 1000N
|> TestSolver.setMinIncl Units.Count.times "c" 1N
|> TestSolver.setMaxIncl Units.Count.times "c" 1000N
|> Solver.solveAll true TestSolver.logger
|> function
    | Ok eqs ->
        eqs |> Solver.printEqs true (fun s -> printfn $"{s}") |> ignore

        eqs
        |> TestSolver.setValues Units.Count.times "a" [| 1N..2N..10_000N |]
        |> TestSolver.setValues Units.Count.times "b" [| 1N..2N..10_000N |]
        |> Solver.solveAll false TestSolver.logger
        |> function
            | Ok eqs ->
                eqs |> Solver.printEqs true (fun s -> printfn $"{s}") |> ignore
            | Error _ -> failwith "errors"
            |> ignore
    | Error _ -> failwith "errors"
 |> ignore


