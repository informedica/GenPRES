
#load "load.fsx"

open System


module List =

    /// <summary>
    /// Reorder a liat according to a permutation. Such that a list
    /// of lists is returned such that [a1; a2; a3; ..; an] becomes
    /// [|
    ///     [ a1; a2; a3; ..; an ]
    ///     [ a2; a1; a3; ..; an ]
    ///     ...
    ///     [ an; a1; a2; ..; an-1 ]
    /// |]
    /// </summary>
    let reorder xs =
        let n = xs |> List.length
        if n <= 2 then [ xs ]
        else
            let y = xs[0]
            let xs = xs |> List.tail
            let n = n - 2
            [
                y::xs
                for i in 0..n do
                    match i with
                    | 0            -> xs[0]::y::xs[1..n]
                    | _ when i = n -> xs[n]::y::xs[0..n-1]
                    | _            ->
                        xs[i]::y::xs[0..i-1] @ xs[i+1..n]
            ]



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



module Utils =

    open MathNet.Numerics

    let inline allPairs min incr max =
        let x1 = [|min..incr..max|]
        let x2 = [|min..incr..max|]

        x1, x2


    let randomNums seed n max =
        let rnd = Random(seed)

        Array.init n (fun _ -> rnd.Next(1, max))


    let randomBigRationals seed n max =
        let nums = randomNums seed n max
        let denums = randomNums (seed + 1) n max
        Array.zip nums denums
        |> Array.map (fun (n, d) ->
            let n = BigRational.FromInt(n)
            let d = BigRational.FromInt(d)
            n / d
        )


    let getTwoRandomLists n max =
        let xs1 = randomBigRationals 1 n max
        let xs2 = randomBigRationals 2 n max
        xs1, xs2



module Equation =


    open Informedica.Utils.Lib
    open Informedica.GenSolver.Lib
    open Variable.Operators
    open Equation



    let printVars vars =
            vars
    //        |> List.sortBy fst
            |> List.iter (fun (i, vars) ->
                vars
                |> List.map (Variable.toString true)
                |> String.concat " "
                |> printfn "%i. %s" i
            )
            vars


    let solve2 onlyMinIncrMax log eq =

        let reorder = List.reorder >> List.mapi (fun i x -> (i, x))

        let calc op1 op2 xs =
            match xs with
            | []    -> None
            | [ x ] -> Some x
            | y::xs ->
                y |> op2 <| (xs |> List.reduce op1)
                |> Some

        if eq |> isSolved then eq, Unchanged
        else
            // log starting the equation solve
            eq
            |> Events.EquationStartedSolving
            |> Logging.logInfo log

            let (<==) = if onlyMinIncrMax then (@<-) else (^<-)
            let vars, op1, op2 =
                match eq with
                | ProductEquation (y, xs) ->
                    if onlyMinIncrMax then
                        y::xs, (@*), (@/)
                    else
                        y::xs, (^*), (^/)
                | SumEquation (y, xs) ->
                    if onlyMinIncrMax then
                        y::xs, (@+), (@-)
                    else
                        y::xs, (^+), (^-)

            let vars = vars |> reorder

            let calc vars =
                vars
                |> List.fold (fun acc vars ->
                    if acc |> Option.isSome then acc
                    else
                        match vars with
                        | _, []
                        | _, [ _ ]   -> acc
                        | i, y::xs ->
                            let op2 = if i = 0 then op1 else op2
                            // log starting the calculation
                            (op1, op2, y, xs)
                            |> Events.EquationStartCalculation
                            |> Logging.logInfo log

                            xs
                            |> calc op1 op2
                            |> function
                                | None ->
                                    // log finishing the calculation
                                    (y::xs, false)
                                    |> Events.EquationFinishedCalculation
                                    |> Logging.logInfo log

                                    None
                                | Some var ->
                                    let yNew = y <== var

                                    if yNew <> y then
                                        // log finishing the calculation
                                        ([yNew], true)
                                        |> Events.EquationFinishedCalculation
                                        |> Logging.logInfo log

                                        Some yNew
                                    else
                                        // log finishing the calculation
                                        ([], false)
                                        |> Events.EquationFinishedCalculation
                                        |> Logging.logInfo log

                                        None
                ) None

            let rec loop acc vars =
                let vars =
                    vars
                    |> List.sortBy(fun (_, xs) ->
                        xs
                        |> List.tail
                        |> List.sumBy Variable.count
                    )

                match calc vars with
                | None -> acc, vars
                | Some var ->
                    vars
                    |> List.map (fun (i, xs) ->
                        i,
                        xs |> List.replace (Variable.eqName var) var
                    )
                    |> List.sortBy(fun (_, xs) ->
                        xs
                        |> List.tail
                        |> List.sumBy Variable.count
                    )
                    |> loop (acc |> List.replaceOrAdd (Variable.eqName var) var)

            vars
            |> loop []
            |> fun (c, vars) ->
                if c |> List.isEmpty then eq, Unchanged
                else
                    let c =
                        let vars = eq |> toVars
                        c
                        |> List.map (fun v2 ->
                            vars
                            |> List.tryFind (Variable.eqName v2)
                            |> function
                            | Some v1 ->
                                v2, v2.Values
                                |> Variable.ValueRange.diffWith v1.Values
                            | None ->
                                $"cannot find {v2}! in {vars}!"
                                |> failwith
                        )
                        |> List.filter (snd >> Set.isEmpty >> not)
                        |> Changed
                    let y, xs =
                        let vars = vars |> List.find (fst >> (=) 0) |> snd
                        vars |> List.head,
                        vars |> List.tail

                    (match eq with
                    | ProductEquation _ -> createProductEqExc (y, xs)
                    | SumEquation _ -> createSumEqExc (y, xs)
                    , c)
                    |> fun (eq, sr) ->
                        // log finishing equation solving
                        (eq, sr)
                        |> Events.EquationFinishedSolving
                        |> Logging.logInfo log

                        eq, sr



open MathNet.Numerics

open Informedica.GenSolver.Lib
open Informedica.GenUnits.Lib

let eqs =
    [
        "a = b + c + e"
        "d = e * a"
        "d = f * b"
    ]
    |> Api.init


eqs
|> fun eqs -> eqs[2]
|> Equation.solve2 false TestSolver.logger
|> (fst >> (Equation.toString true) >> printfn "%s")



eqs
|> TestSolver.setValues Units.Count.times "a" [| 1N; 2N; 3N |]
|> fun eqs -> eqs[2]
|> Equation.solve2 false TestSolver.logger
|> (fst >> (Equation.toString true) >> printfn "%s")




eqs
|> TestSolver.setValues Units.Count.times "a" [| 1N; 2N; 3N |]
|> TestSolver.setValues Units.Count.times "b" [| 1N; 2N; 3N |]
|> fun eqs -> eqs[2]
|> Equation.solve2 false TestSolver.logger
|> (fst >> (Equation.toString true) >> printfn "%s")


eqs
|> TestSolver.setValues Units.Count.times "a" [| 1N; 2N; 3N |]
|> TestSolver.setValues Units.Count.times "b" [| 1N; 2N; 3N |]
|> TestSolver.setValues Units.Count.times "c" [| 1N..1N..3N |]
|> fun eqs -> eqs[2]
|> Equation.solve2 false TestSolver.logger
|> (fst >> (Equation.toString true) >> printfn "%s")


eqs
|> TestSolver.setValues Units.Count.times "a" [| 1N; 2N; 3N |]
|> TestSolver.setValues Units.Count.times "b" [| 1N; 2N; 3N |]
|> TestSolver.setValues Units.Count.times "c" [| 1N..1N..3N |]
|> TestSolver.setValues Units.Count.times "e" [| 1N..1N..30N |]
|> fun eqs -> eqs[2]
|> Equation.solve2 false TestSolver.logger
|> (fst >> (Equation.toString true) >> printfn "%s")
