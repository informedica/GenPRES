// For more information see https://aka.ms/fsharp-console-apps


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
        fun (_ : string) ->
            () //File.AppendAllLines("examples.log", [s])
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


module Utils =


    let inline allPairs min incr max = 
        let x1 = [|min..incr..max|]
        let x2 = [|min..incr..max|]

        x1, x2


open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open MathNet.Numerics

open TestSolver
open Informedica.GenUnits.Lib

type Benchmarks () =

    let eqs1 =
        [
            "a = b + c"
            "d = f * a"
            "d = e * b"
        ] 
        |> init 
        |> nonZeroNegative
        |> setValues Units.Count.times "b" [|1N..1N..100N|]
        |> setValues Units.Count.times "c" [|1N..1N..100N|]


    let eqs_n n =
        [
            "a = b + c"
            "d = f * a"
            "d = e * b"
        ] 
        |> init 
        |> nonZeroNegative
        |> setValues Units.Count.times "b" [|1N..1N..n|]
        |> setValues Units.Count.times "c" [|1N..1N..n|]

    let allPairsInt_100 = Utils.allPairs 1 1 100
    let allPairsInt_1_000 = Utils.allPairs 1 1 1_000

    let allPairs_100 = Utils.allPairs 1N 1N 100N
    let allPairs_1_000 = Utils.allPairs 1N 1N 1_000N


    [<Benchmark>]
    member this.AllPairesInt_100 () =
        let x1, x2 = allPairsInt_100

        Array.allPairs x1 x2
        |> Array.map (fun (x1, x2) -> x1 + x2)


    [<Benchmark>]
    member this.AllPairsInt_1_000 () =
        let x1, x2 = allPairsInt_1_000

        Array.allPairs x1 x2
        |> Array.map (fun (x1, x2) -> x1 + x2)


    [<Benchmark>]
    member this.AllPaires_100 () =
        let x1, x2 = allPairs_100

        Array.allPairs x1 x2
        |> Array.map (fun (x1, x2) -> x1 + x2)


    [<Benchmark>]
    member this.AllPairs_1_000 () =
        let x1, x2 = allPairs_1_000

        Array.allPairs x1 x2
        |> Array.map (fun (x1, x2) -> x1 + x2)


    [<Benchmark>]
    member this.SolveCountMinIncl () =
        solveCountMinIncl "a" 10N eqs1


    [<Benchmark>]
    member this.SolveAll_100 () =
        solveAll (eqs_n 100N)


    [<Benchmark>]
    member this.SolveAll_1_000 () =
        solveAll (eqs_n 1_000N)



// For more information see https://aka.ms/fsharp-console-apps
[<EntryPoint>]
let main (args: string[]) =

    let _ = BenchmarkRunner.Run<Benchmarks>()
    printfn "Finished running benchmarks"

    0


