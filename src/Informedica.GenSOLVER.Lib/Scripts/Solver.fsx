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


    let printEqs =
        function
        | Ok eqs -> eqs |> Solver.printEqs true procss
        | Error _ -> invalidOp "errors"


    let printEqsWithUnits =
        function
        | Ok eqs -> eqs |> Solver.printEqs false procss
        | Error _ -> invalidOp "errors"


    let setProp n p eqs =
        let n = n |> Name.createExc

        match eqs |> Api.setVariableValues n p with
        | Some var -> eqs |> List.map (fun e -> e |> Equation.replace var)
        | None -> eqs

    let create c u v = [| v |] |> ValueUnit.create u |> c

    let createMinIncl = create (Minimum.create true)
    let createMinExcl = create (Minimum.create false)
    let createMaxIncl = create (Maximum.create true)
    let createMaxExcl = create (Maximum.create false)
    let createIncr = create Increment.create

    let createValSet u v =
        v |> Array.ofSeq |> ValueUnit.create u |> ValueSet.create

    let setIncr u n vals =
        vals |> createIncr u |> IncrProp |> setProp n

    let setMinIncl u n min =
        min |> createMinIncl u |> MinProp |> setProp n

    let setMinExcl u n min =
        min |> createMinExcl u |> MinProp |> setProp n

    let setMaxIncl u n max =
        max |> createMaxIncl u |> MaxProp |> setProp n

    let setMaxExcl u n max =
        max |> createMaxExcl u |> MaxProp |> setProp n

    let setValues u n vals =
        vals |> createValSet u |> ValsProp |> setProp n

    let logger =
        fun (s: string) -> printfn $"{s}"
        |> SolverLogging.create

    let solve n p eqs =
        let n = n |> Name.createExc
        Api.solve true (fun _ eqs -> eqs |> List.mapi (fun i e -> (i, e))) logger n p eqs

    let solveAll = Api.solveAll false logger

    let solveMinMax = Api.solveAll true logger

    let solveMinIncl u n min =
        solve n (min |> createMinIncl u |> MinProp)

    let solveMinExcl u n min =
        solve n (min |> createMinExcl u |> MinProp)

    let solveMaxIncl u n max =
        solve n (max |> createMaxIncl u |> MaxProp)

    let solveMaxExcl u n max =
        solve n (max |> createMaxExcl u |> MaxProp)

    let solveIncr u n incr =
        solve n (incr |> createIncr u |> IncrProp)

    let solveValues u n vals =
        solve n (vals |> createValSet u |> ValsProp)

    let init = Api.init
    let nonZeroNegative = Api.nonZeroNegative


    let solveCountMinIncl = solveMinIncl Units.Count.times
    let solveCountMaxExcl = solveMaxExcl Units.Count.times
    let solveCountValues u n vals = solveValues Units.Count.times u n vals


open Informedica.Utils.Lib.BCL

open Informedica.GenSolver.Lib
open Informedica.GenUnits.Lib

let eqs = [ "a = b + c"; "d = e * a"; "d = f * b" ] |> Api.init


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
        |> TestSolver.setValues Units.Count.times "a" [| 1N .. 2N .. 10_000N |]
        |> TestSolver.setValues Units.Count.times "b" [| 1N .. 2N .. 10_000N |]
        |> Solver.solveAll false TestSolver.logger
        |> function
            | Ok eqs -> eqs |> Solver.printEqs true (fun s -> printfn $"{s}") |> ignore
            | Error _ -> invalidOp "errors"
    | Error _ -> invalidOp "errors"


let min =
    Units.Volume.milliLiter
    |> ValueUnit.singleWithValue 1N
    |> Variable.ValueRange.Minimum.create true
    |> Some

let incr =
    Units.Volume.milliLiter
    |> ValueUnit.singleWithValue 1N
    |> Variable.ValueRange.Increment.create
    |> Some

let max =
    Units.Volume.milliLiter
    |> ValueUnit.singleWithValue 1000N
    |> Variable.ValueRange.Maximum.create true
    |> Some

{ Variable.empty ("test" |> Variable.Name.createExc) with Values = Variable.ValueRange.create min incr max None }
|> Variable.minIncrMaxToValues (Some 10)


open Informedica.Utils.Lib

let prune incr n =
    let rec loop mn mx m incr (xs: BigRational[]) =
        let filtered =
            xs
            |> Array.filter (fun x -> x = mn || x = mx || (x / (incr * m)).Denominator = 1I)

        if filtered |> Array.length <= (Operators.max n 2) then
            filtered
        else
            loop mn mx (m + 1N) incr xs

    fun vu ->
        let u = vu |> ValueUnit.getUnit

        let v =
            match incr |> Option.map ValueUnit.getBaseValue with
            | Some [| incr |] ->
                let xs = vu |> ValueUnit.convertTo u |> ValueUnit.getValue |> Array.distinct
                let mn = xs |> Array.min
                let mx = xs |> Array.max
                xs |> loop mn mx 1N incr
            | _ -> vu |> ValueUnit.getValue |> Array.prune n

        vu |> ValueUnit.setValue v


let prune10 =
    Units.Volume.liter |> ValueUnit.singleWithValue (5N / 1000N) |> Some |> prune


Units.Volume.milliLiter
|> ValueUnit.withValue [| 5N .. 5N .. 1000N |]
|> prune10 50
|> ValueUnit.getValue
|> Array.length


#r "nuget: Expecto, 9.0.4"

open Expecto
open Expecto.Flip


let pruneTests =
    testList "ValueSet.prune retains min and max" [
        test "prune with increment retains min and max when they are not multiples of incr*m" {
            // values: 3, 5, 10, 15, 20, ..., 100, 107
            // min=3 and max=107 are not multiples of 5
            let values = [| yield 3N; yield! [| 5N .. 5N .. 100N |]; yield 107N |]
            let incrVu =
                Units.Volume.milliLiter
                |> ValueUnit.singleWithValue 5N
                |> Some

            let result =
                Units.Volume.milliLiter
                |> ValueUnit.withValue values
                |> prune incrVu 5
                |> ValueUnit.getValue

            result |> Array.min |> Expect.equal "min should be 3" 3N
            result |> Array.max |> Expect.equal "max should be 107" 107N
        }

        test "prune with increment keeps result within n elements" {
            let values = [| 3N; yield! [| 5N .. 5N .. 100N |]; 107N |]
            let incrVu =
                Units.Volume.milliLiter
                |> ValueUnit.singleWithValue 5N
                |> Some

            let result =
                Units.Volume.milliLiter
                |> ValueUnit.withValue values
                |> prune incrVu 5
                |> ValueUnit.getValue

            result |> Array.length <= 5
            |> Expect.isTrue "should be <= 5 elements"
        }

        test "prune without increment retains min and max" {
            let values = [| 3N; 7N; 11N; 15N; 19N; 23N; 27N; 31N; 35N; 39N |]

            let result =
                Units.Volume.milliLiter
                |> ValueUnit.withValue values
                |> prune None 4
                |> ValueUnit.getValue

            result |> Array.min |> Expect.equal "min should be 3" 3N
            result |> Array.max |> Expect.equal "max should be 39" 39N
        }

        test "prune with 2 elements returns both" {
            let values = [| 3N; 107N |]
            let incrVu =
                Units.Volume.milliLiter
                |> ValueUnit.singleWithValue 5N
                |> Some

            let result =
                Units.Volume.milliLiter
                |> ValueUnit.withValue values
                |> prune incrVu 5
                |> ValueUnit.getValue

            result |> Expect.equal "should return both elements" [| 3N; 107N |]
        }

        test "original prune10 test still works" {
            let result =
                Units.Volume.milliLiter
                |> ValueUnit.withValue [| 5N .. 5N .. 1000N |]
                |> prune10 50
                |> ValueUnit.getValue

            result |> Array.length <= 50
            |> Expect.isTrue "should be <= 50 elements"
            result |> Array.min |> Expect.equal "min should be 5" 5N
            result |> Array.max |> Expect.equal "max should be 1000" 1000N
        }
    ]


pruneTests |> runTestsWithCLIArgs [] [||]
