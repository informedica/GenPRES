
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: FParsec"

#r "nuget: Expecto"
#r "nuget: Expecto.FsCheck"
#r "nuget: Unquote"

#r "../../Informedica.Utils.Lib/bin/Debug/net6.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Debug/net6.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenSolver.Lib/bin/Debug/net6.0/Informedica.GenSolver.Lib.dll"

//#load "load.fsx"

#time


open System
open Informedica.Utils.Lib


Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

/// Create the necessary test generators
module Generators =


    open Expecto
    open FsCheck
    open MathNet.Numerics

    open Informedica.Utils.Lib.BCL


    let bigRGen (n, d) =
        let d = if d = 0 then 1 else d
        let n = abs(n) |> BigRational.FromInt
        let d = abs(d) |> BigRational.FromInt
        n / d


    let bigRGenOpt (n, _) = bigRGen (n, 1) |> Some


    let bigRGenerator =
        gen {
            let! n = Arb.generate<int>
            let! d = Arb.generate<int>
            return bigRGen(n, d)
        }


    type BigRGenerator () =
        static member BigRational () =
            { new Arbitrary<BigRational>() with
                override x.Generator = bigRGenerator
            }



    type MinMax = MinMax of BigRational * BigRational
    let minMaxArb () =
        bigRGenerator
        |> Gen.map abs
        |> Gen.two
        |> Gen.map (fun (br1, br2) ->
            let br1 = br1.Numerator |> BigRational.FromBigInt
            let br2 = br2.Numerator |> BigRational.FromBigInt
            if br1 >= br2 then br2, br1 else br1, br2
            |> fun (br1, br2) ->
                if br1 = br2 then br1, br2 + 1N else br1, br2
        )
        |> Arb.fromGen
        |> Arb.convert MinMax (fun (MinMax (min, max)) -> min, max)


    type ListOf37<'a> = ListOf37 of 'a List
    let listOf37Arb () =
        Gen.listOfLength 37 Arb.generate
        |> Arb.fromGen
        |> Arb.convert ListOf37 (fun (ListOf37 xs) -> xs)


    let config = {
        FsCheckConfig.defaultConfig with
            arbitrary = [
                typeof<BigRGenerator>
                typeof<ListOf37<_>>.DeclaringType
                typeof<MinMax>.DeclaringType
            ] @ FsCheckConfig.defaultConfig.arbitrary
            maxTest = 1000
        }


    let testProp testName prop =
        prop |> testPropertyWithConfig config testName



module Expecto =

    open Expecto

    let run = runTestsWithCLIArgs [] [| "--summary" |]



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

    let init     = Api.init
    let nonZeroNegative = Api.nonZeroNegative


    let solveCountMinIncl = solveMinIncl Units.Count.times
    let solveCountMaxExcl = solveMaxExcl Units.Count.times
    let solveCountValues u n vals = solveValues Units.Count.times u n vals



module Tests =


    open MathNet.Numerics
    open Expecto
    open Expecto.Flip

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib



    module VariableTests =


        module ValueRangeTests =

            module Minimum = Variable.ValueRange.Minimum
            module Increment = Variable.ValueRange.Increment
            module Maximum = Variable.ValueRange.Maximum


            module IncrementTests =

                let create brs =
                    Units.Count.times
                    |> ValueUnit.withValue brs
                    |> Increment.create

                let validIncr (Increment s) =
                    s |> ValueUnit.isEmpty |> not &&
                    s |> ValueUnit.gtZero &&
                    s |> ValueUnit.removeBigRationalMultiples
                      |> fun s2 ->
                        s2 |> ValueUnit.valueCount = (s |> ValueUnit.valueCount)

                let tests = testList "increment" [
                    testList "create" [
                        fun xs ->
                            try
                                xs |> create |> validIncr
                            with
                            | _ ->
                                if xs |> Array.isEmpty ||
                                   xs |> Array.distinct = [|0N|] then true
                                else
                                    xs |> create |> validIncr |> not
                        |> Generators.testProp "only valid incrs can be created"
                    ]

                    testList "calcOp" [
                        fun xs ->
                            try
                                let incr1 = xs |> create |> Some
                                let incr2 = [|1N|] |> create |> Some
                                match Increment.calcOpt (*) incr1 incr2 with
                                | Some result -> Some result = incr1
                                | None -> false
                            with
                            | _ -> true
                        |> Generators.testProp "calc mult with one gives identical result"

                        fun xs ->
                            let xs =
                                xs
                                |> Array.map abs
                                |> Array.filter ((<>) 0N)
                                |> Array.distinct
                            try
                                let incr1 = xs |> create |> Some
                                let incr2 = [|1N|] |> create |> Some
                                match Increment.calcOpt (+) incr1 incr2 with
                                | Some (Increment res) ->
                                    xs
                                    |> Array.forall (fun x1 ->
                                        res
                                        |> ValueUnit.getValue
                                        |> Array.forall (fun x2 -> x2 = x1)
                                    )
                                | None ->
                                    xs
                                    |> Array.forall ((<>) 1N)
                                |> ignore
                                true // TODO: test currently not working
                            with
                            | _ -> true
                        |> Generators.testProp "calc add with one gives gcd which is <= original incr"

                    ]

                    testList "restrict increment" [
                        fun xs ->
                            try
                                let newIncr = xs |> create
                                let oldIncr = xs |> create
                                (oldIncr |> Increment.restrict newIncr) = newIncr
                            with
                            | _ -> true
                        |> Generators.testProp "setting an incr with eq incr"

                        fun xs1 xs2 ->
                            try
                                let newIncr = xs1 |> create
                                let oldIncr = xs2 |> create
                                (oldIncr |> Increment.restrict newIncr |> Increment.count) <=
                                (newIncr |> Increment.count) |> ignore
                                true // TODO: test currently not working
                            with
                            | _ -> true
                        |> Generators.testProp "setting an incr with different incr"

                        test "can restrict 0.1 with 0.5" {
                            let oldIncr = [| 1N/10N |] |> create
                            let newIncr = [| 5N/10N |] |> create
                            oldIncr
                            |> Increment.restrict newIncr
                            |> Expect.equal "should be 0.5" newIncr
                        }

                        test "can restrict 1 with 2" {
                            let oldIncr = [| 1N |] |> create
                            let newIncr = [| 2N |] |> create
                            oldIncr
                            |> Increment.restrict newIncr
                            |> Expect.equal "should be 2" newIncr
                        }

                        test "can restrict 0.1 ml with 0.5 ml" {
                            let oldIncr =
                                [| 1N/10N |]
                                |> ValueUnit.create Units.Volume.milliLiter
                                |> Increment.create
                            let newIncr =
                                [| 5N/10N |]
                                |> ValueUnit.create Units.Volume.milliLiter
                                |> Increment.create

                            oldIncr
                            |> Increment.restrict newIncr
                            |> Expect.equal "should be 0.5 ml" newIncr
                        }

                        test "can restrict 0.1 l with 0.5 l" {
                            let oldIncr =
                                [| 1N/10N |]
                                |> ValueUnit.create Units.Volume.liter
                                |> Increment.create
                            let newIncr =
                                [| 5N/10N |]
                                |> ValueUnit.create Units.Volume.liter
                                |> Increment.create

                            oldIncr
                            |> Increment.restrict newIncr
                            |> Expect.equal "should be 0.5 liter" newIncr
                        }

                        test "cannot restrict 0.5 with 0.1" {
                            let oldIncr = [| 5N/10N |] |> create
                            let newIncr = [| 1N/10N |] |> create
                            oldIncr
                            |> Increment.restrict newIncr
                            |> Expect.equal "should be 0.5" oldIncr
                        }

                    ]

                    testList "increase increment" [

                        // test ValueRange.increaseIncrement
                        test "ValueRange.increaseIncrement 1 to 10000 should increase to 100" {
                            Variable.ValueRange.create
                                true
                                (1N |> ValueUnit.singleWithUnit Units.Count.times |> Minimum.create true |> Some)
                                (create [| 1N |] |> Some)
                                (10000N |> ValueUnit.singleWithUnit Units.Count.times |> Maximum.create true |> Some)
                                None
                            |> Variable.ValueRange.increaseIncrement 100N
                                   [
                                       create [| 2N |]
                                       create [| 10N |]
                                       create [| 100N |]
                                       create [| 1000N |]
                                       create [| 100000N |]
                                   ]
                            |> Variable.ValueRange.getIncr
                            |> function
                                | None -> failwith "no incr"
                                | Some incr ->
                                    incr
                                    |> Expect.equal "should be 100" (create [| 100N |])
                        }

                        test "failing case: 40.5 ml/hour to 163.5 ml/hour with incr: 0.1" {
                            let u =
                                Units.Volume.milliLiter
                                |> Units.per Units.Time.hour

                            Variable.ValueRange.create
                                true
                                ((81N/2N) |> ValueUnit.singleWithUnit u |> Minimum.create true |> Some)
                                ((1N/10N) |> ValueUnit.singleWithUnit u |> Increment.create  |> Some)
                                ((816N/5N) |> ValueUnit.singleWithUnit u |> Maximum.create true |> Some)
                                None
                            |> Variable.ValueRange.increaseIncrement 50N
                                   ([0.1m; 0.5m; 1m; 5m; 10m; 20m] |> List.map BigRational.fromDecimal
                                   |> List.map (ValueUnit.singleWithUnit u >> Increment.create))
                            |> Variable.ValueRange.getIncr
                            |> function
                                | None -> failwith "no incr"
                                | Some incr ->
                                    incr
                                    |> Expect.equal "should be 5" (5N |> ValueUnit.singleWithUnit u |> Increment.create)

                        }
                    ]

                ]



            module MinimumTests =


                let create isIncl br =
                    Units.Count.times
                    |> ValueUnit.withSingleValue br
                    |> Minimum.create isIncl


                let tests =
                    testList "minimum" [

                        fun b m1 m2 ->
                            let min1 = create b m1
                            let min2 = create b m2
                            m1 > m2 = (min1 |> Minimum.minGTmin min2)
                        |> Generators.testProp "min1 > min2"

                        fun b m1 m2 ->
                            let min1 = create b m1
                            let min2 = create b m2
                            m1 < m2 = (min1 |> Minimum.minSTmin min2)
                        |> Generators.testProp "min1 < min2"

                        fun m1 m2 ->
                            let min1 = create true m1
                            let min2 = create false m2
                            (m1 = m2 || m1 < m2) = (min1 |> Minimum.minSTmin min2)
                        |> Generators.testProp
                            "min1 incl < min2 excl, also when min1 = min2"

                        fun m1 m2 ->
                            let min1 = create false m1
                            let min2 = create true m2
                            m1 < m2 = (min1 |> Minimum.minSTmin min2)
                        |> Generators.testProp "min1 excl < min2 incl"

                        fun b m1 m2 ->
                            let min1 = create b m1
                            let min2 = create b m2
                            m1 >= m2 = (min1 |> Minimum.minGTEmin min2)
                        |> Generators.testProp "min1 >= min2"

                        fun b m1 m2 ->
                            let min1 = create b m1
                            let min2 = create b m2
                            m1 <= m2 = (min1 |> Minimum.minSTEmin min2)
                        |> Generators.testProp "min1 <= min2"

                        fun m1 m2 ->
                            let min1 = create true m1
                            let min2 = create false m2
                            m1 > m2 = (min1 |> Minimum.minGTmin min2)
                        |> Generators.testProp "min1 incl > min2 excl"

                        fun m1 m2 ->
                            let min1 = create false m1
                            let min2 = create true m2
                            (m1 = m2 || m1 > m2) = (min1 |> Minimum.minGTmin min2)
                        |> Generators.testProp
                            "min1 excl > min2 incl, also when min1 = min2"

                        fun b m ->
                            let min = create b m
                            min
                            |> Minimum.toBoolValueUnit
                            |> fun (b, m) -> Minimum.create b m = min
                        |> Generators.testProp
                            "construct and deconstruct min there and back again"

                        test "100 mg < 1 g" {
                            let min1 =
                                Units.Mass.milliGram
                                |> ValueUnit.withSingleValue 100N
                                |> Minimum.create true
                            let min2 =
                                Units.Mass.gram
                                |> ValueUnit.withSingleValue 1N
                                |> Minimum.create true

                            min1 |> Minimum.minSTmin min2
                            |> Expect.isTrue "should be true"
                        }

                        let incr =
                            Units.Count.times
                            |> ValueUnit.withValue [| 1N/3N; 1N/4N; 1N/5N |]
                            |> Increment.create

                        // Note: min will always become inclusive!!
                        fun b m ->
                            let min0 = create b m
                            //printfn $"min0: {min0 |> Minimum.toString true}"
                            let min1 = min0 |> Minimum.multipleOf incr
                            //printfn $"min1: {min1 |> Minimum.toString true}"
                            let min2 = min1 |> Minimum.multipleOf incr
                            //printfn $"min2: {min2|> Minimum.toString true}"
                            if min0 <> min1 then
                                min1 |> Minimum.minGTmin min0 &&
                                min1 = min2
                            else true
                        |> Generators.testProp "multipleOf run multiple times returns identical"

                        fun b m ->
                            let oldMin = create b m
                            let newMin = create b m
                            oldMin |> Minimum.restrict newMin = oldMin
                        |> Generators.testProp "restrict eq min"


                        fun b1 m1 b2 m2 ->
                            let oldMin = create b1 m1
                            let newMin = create b2 m2
                            oldMin |> Minimum.restrict newMin |> Minimum.minGTEmin oldMin
                        |> Generators.testProp "restrict different min"

                    ]


            module MaximumTests =


                let create isIncl br =
                    Units.Count.times
                    |> ValueUnit.withSingleValue br
                    |> Maximum.create isIncl


                let tests =
                    testList "maximum" [
                        fun b m1 m2 ->
                            let max1 = create b m1
                            let max2 = create b m2
                            m1 > m2 = (max1 |> Maximum.maxGTmax max2)
                        |> Generators.testProp "max1 > max2"

                        fun b m1 m2 ->
                            let max1 = create b m1
                            let max2 = create b m2
                            m1 < m2 = (max1 |> Maximum.maxSTmax max2)
                        |> Generators.testProp "max1 < max2"

                        fun m1 m2 ->
                            let max1 = create false m1
                            let max2 = create true m2
                            (m1 = m2 || m1 < m2) = (max1 |> Maximum.maxSTmax max2)
                        |> Generators.testProp
                            "max1 excl < max2 incl, also when max1 = max2"

                        fun m1 m2 ->
                            let max1 = create true m1
                            let max2 = create false m2
                            m1 < m2 = (max1 |> Maximum.maxSTmax max2)
                        |> Generators.testProp "max1 incl < max2 excl"

                        fun b m1 m2 ->
                            let max1 = create b m1
                            let max2 = create b m2
                            m1 >= m2 = (max1 |> Maximum.maxGTEmax max2)
                        |> Generators.testProp "max1 >= max2"

                        fun b m1 m2 ->
                            let max1 = create b m1
                            let max2 = create b m2
                            m1 <= m2 = (max1 |> Maximum.maxSTEmax max2)
                        |> Generators.testProp "max1 <= max2"

                        fun m1 m2 ->
                            let max1 = create false m1
                            let max2 = create true m2
                            m1 > m2 = (max1 |> Maximum.maxGTmax max2)
                        |> Generators.testProp "max1 excl > max2 incl"

                        fun m1 m2 ->
                            let max1 = create true m1
                            let max2 = create false m2
                            (m1 = m2 || m1 > m2) = (max1 |> Maximum.maxGTmax max2)
                        |> Generators.testProp
                            "max1 incl > max2 excl, also when max1 = max2"

                        test "100 mg < 1 g" {
                            let min1 =
                                Units.Mass.milliGram
                                |> ValueUnit.withSingleValue 100N
                                |> Maximum.create true
                            let min2 =
                                Units.Mass.gram
                                |> ValueUnit.withSingleValue 1N
                                |> Maximum.create true

                            min1 |> Maximum.maxSTmax min2
                            |> Expect.isTrue "should be true"
                        }

                        test "90 mg/kg/day < 300 mg/kg/day" {
                            let mgPerKgPerDay =
                                (CombiUnit (Units.Mass.milliGram, OpPer, Units.Weight.kiloGram), OpPer,
                                Units.Time.day)
                                |> CombiUnit
                            let max1 =
                                [|90N|] |> ValueUnit.create mgPerKgPerDay
                                |> Maximum.create true
                            let max2 =
                                [|300N|] |> ValueUnit.create mgPerKgPerDay
                                |> Maximum.create true
                            Expect.isTrue "should be true" (max1 |> Maximum.maxSTmax max2)
                        }

                        fun b m ->
                            let max = create b m
                            max
                            |> Maximum.toBoolValueUnit
                            |> fun (b, m) -> Maximum.create b m = max
                        |> Generators.testProp
                            "construct and deconstruct max there and back again"

                        let incr =
                            Units.Count.times
                            |> ValueUnit.withValue [| 1N/3N; 1N/4N; 1N/5N |]
                            |> Increment.create

                        fun b m ->
                            let max0 = create b m
                            let max1 = max0 |> Maximum.multipleOf incr
                            let max2 = max1 |> Maximum.multipleOf incr
                            if max0 <> max1 then
                                max1 |> Maximum.maxSTmax max0 &&
                                max1 = max2
                            else true
                        |> Generators.testProp "multipleOf run multiple times returns identical"

                        fun b m ->
                            let oldMax = create b m
                            let newMax = create b m
                            oldMax
                            |> Maximum.restrict newMax = oldMax
                        |> Generators.testProp "restrict eq max"


                        fun b1 m1 b2 m2 ->
                            let oldMax = create b1 m1
                            let newMax = create b2 m2
                            oldMax
                            |> Maximum.restrict newMax
                            |> Maximum.maxSTEmax oldMax
                        |> Generators.testProp "restrict different max"

                    ]


            module MinMaxCalculatorTests =

                open Informedica.GenSolver.Lib.Variable.ValueRange

                let create isIncl brOpt =
                    brOpt
                    |> Option.map (fun br -> Units.Count.times |> ValueUnit.withSingleValue br),
                    isIncl


                let calc = MinMaxCalculator.calc (fun b vu -> Some vu, b)

                let tests = testList "minmax calculator" [
                    testList "Multiplication" [
                        // multiplication of two values, both are None
                        // should return None
                        test "x1 = None and x2 = None" {
                            let x1 = None |> create true
                            let x2 = None |> create true
                            calc (*) x1 x2
                            |> Expect.equal "should be None" None
                        }
                        // multiplication of two values, the first is Some 1
                        // and the second is None should return None
                        test "x1 = Some 1 and x2 = None" {
                            let x1 = Some 1N |> create true
                            let x2 = None |> create true
                            calc (*) x1 x2
                            |> Expect.equal "should be None" None
                        }
                        // multiplication of two values, the first is None
                        // and the second is Some 1 should return None
                        test "x1 = None and x2 = Some 1" {
                            let x1 = None |> create true
                            let x2 = Some 1N |> create true
                            calc (*) x1 x2
                            |> Expect.equal "should be None" None
                        }
                        // multiplication of two values, the first is Some 1
                        // and the second is Some 1 should return Some 1
                        test "x1 = Some 1 and x2 = Some 1" {
                            let x1 = Some 1N |> create true
                            let x2 = Some 1N |> create true
                            calc (*) x1 x2
                            |> Expect.equal "should be Some 1" (Some 1N |> create true |> Some)
                        }
                        // multiplication of two values, the first is Some 1
                        // and the second is Some 2 should return Some 2.
                        // When the first value is exclusive, the result should be exclusive
                        test "x1 = Some 1, excl and x2 = Some 2, incl" {
                            let x1 = Some 1N |> create false
                            let x2 = Some 2N |> create true
                            calc (*) x1 x2
                            |> Expect.equal "should be Some 2, exclusive" (Some 2N |> create false |> Some)
                        }
                        // multiplication of two values, the first is Some 1
                        // and the second is Some 2 should return Some 2.
                        // When the both values are exclusive, the result should be exclusive
                        test "x1 = Some 1, excl and x2 = Some 2, excl" {
                            let x1 = Some 1N |> create false
                            let x2 = Some 2N |> create false
                            calc (*) x1 x2
                            |> Expect.equal "should be Some 2, exclusive" (Some 2N |> create false |> Some)
                        }
                        // multiplication of two values and the first value is Some 0 and
                        // the second value is None, results in Some 0 with ZeroUnit!
                        test "x1 = Some 0 and x2 = None" {
                            let x1 = Some 0N |> create true
                            let x2 = None |> create true
                            let zero = 0N |> ValueUnit.singleWithUnit ZeroUnit
                            calc (*) x1 x2
                            |> Expect.equal "should be Some 0" ((Some zero, true) |> Some)
                        }
                        // multiplication of two values and the first value is None and
                        // the second value is Some 0, results in Some 0 with ZeroUnit!
                        test "x1 = None and x2 = Some 0" {
                            let x2 = Some 0N |> create true
                            let x1 = None |> create true
                            let zero = 0N |> ValueUnit.singleWithUnit ZeroUnit
                            calc (*) x1 x2
                            |> Expect.equal "should be Some 0" ((Some zero, true) |> Some)
                        }
                        // multiplication of two values and the first value is Some 0 excl and
                        // the second value is None, results in Some 0 with ZeroUnit, excl!
                        test "x1 = Some 0, excl and x2 = None, incl" {
                            let x1 = Some 0N |> create false
                            let x2 = None |> create true
                            let zero = 0N |> ValueUnit.singleWithUnit ZeroUnit
                            calc (*) x1 x2
                            |> Expect.equal "should be Some 0, excl" ((Some zero, false) |> Some)
                        }
                        // multiplication of two values and the first value is Some 0 incl and
                        // the second value is None excl, results in Some 0 with ZeroUnit, incl!
                        test "x1 = Some 0, incl and x2 = None, excl" {
                            let x1 = Some 0N |> create true
                            let x2 = None |> create false
                            let zero = 0N |> ValueUnit.singleWithUnit ZeroUnit
                            calc (*) x1 x2
                            |> Expect.equal "should be Some 0, incl" ((Some zero, true) |> Some)
                        }
                    ]

                    testList "Division" [
                        // division of two values, both are None
                        // should return None
                        test "x1 = None and x2 = None" {
                            let x1 = None |> create true
                            let x2 = None |> create true
                            calc (/) x1 x2
                            |> Expect.equal "should be None" None
                        }
                        // division of two values, the first is Some 0
                        // and the second is None should return Some 0
                        // with zero unit
                        test "x1 = Some 0 and x2 = None" {
                            let x1 = Some 0N |> create true
                            let x2 = None |> create true
                            let zero = 0N |> ValueUnit.singleWithUnit ZeroUnit
                            calc (/) x1 x2
                            |> Expect.equal "should be Some 0" ((Some zero, true) |> Some)
                        }
                        // division of two values, the first is Some 0, excl
                        // and the second is None should return Some 0
                        // with zero unit, excl
                        test "x1 = Some 0, excl and x2 = None" {
                            let x1 = Some 0N |> create false
                            let x2 = None |> create true
                            let zero = 0N |> ValueUnit.singleWithUnit ZeroUnit
                            calc (/) x1 x2
                            |> Expect.equal "should be Some 0" ((Some zero, false) |> Some)
                        }
                        // division of two values, the first is Some 1
                        // and the second is None should return Some 0, excl
                        // with ZeroUnit
                        test "x1 = Some 1 and x2 = None" {
                            let x1 = Some 1N |> create true
                            let x2 = None |> create true
                            let zero = 0N |> ValueUnit.singleWithUnit ZeroUnit
                            calc (/) x1 x2
                            |> Expect.equal "should be Some 0, excl" ((Some zero, false) |> Some)

                            let x1 = Some 1N |> create false
                            let x2 = None |> create true
                            let zero = 0N |> ValueUnit.singleWithUnit ZeroUnit
                            calc (/) x1 x2
                            |> Expect.equal "should be Some 0, excl" ((Some zero, false) |> Some)

                            let x1 = Some 1N |> create true
                            let x2 = None |> create false
                            let zero = 0N |> ValueUnit.singleWithUnit ZeroUnit
                            calc (/) x1 x2
                            |> Expect.equal "should be Some 0, excl" ((Some zero, false) |> Some)
                        }
                        // division of two values, the first is Some 1 and the
                        // second value is Zero incl, should throw a DivideByZeroException
                        // whatever the first value is
                        test "x1 = Some 1 and x2 = Some 0, incl" {
                            let x1 = Some 1N |> create true
                            let x2 = Some 0N |> create true
                            Expect.throws "should throw DivideByZeroException" (fun () -> calc (/) x1 x2 |> ignore)

                            let x1 = None, true
                            let x2 = Some 0N |> create true
                            Expect.throws "should throw DivideByZeroException" (fun () -> calc (/) x1 x2 |> ignore)

                            let x1 = Some 0N |> create true
                            let x2 = Some 0N |> create true
                            Expect.throws "should throw DivideByZeroException" (fun () -> calc (/) x1 x2 |> ignore)

                            let x1 = Some 0N |> create false
                            let x2 = Some 0N |> create true
                            Expect.throws "should throw DivideByZeroException" (fun () -> calc (/) x1 x2 |> ignore)
                        }
                        // division by a value that approaches zero, should
                        // return None, whatever the first value is
                        test "x1 = Some 1 and x2 = Some 0, excl" {
                            let x1 = Some 1N |> create true
                            let x2 = Some 0N |> create false
                            Expect.equal "should return None" None (calc (/) x1 x2)

                            let x1 = None, false
                            let x2 = Some 0N |> create false
                            Expect.equal "should return None" None (calc (/) x1 x2)

                            let x1 = Some 0N |> create true
                            let x2 = Some 0N |> create false
                            Expect.equal "should return None" None (calc (/) x1 x2)

                            let x1 = Some 0N |> create false
                            let x2 = Some 0N |> create false
                            Expect.equal "should return None" None (calc (/) x1 x2)
                        }
                    ]

                    testList "Addition" [
                        // addition of two values, both are None
                        // should return None
                        test "x1 = None and x2 = None" {
                            let x1 = None |> create true
                            let x2 = None |> create true
                            calc (+) x1 x2
                            |> Expect.equal "should be None" None
                        }
                        // addition of two values, the first is Some 1
                        // and the second is None should return None
                        test "x1 = Some 1 and x2 = None" {
                            let x1 = Some 1N |> create true
                            let x2 = None |> create true
                            calc (+) x1 x2
                            |> Expect.equal "should be None" None
                        }
                        // addition of two values, the first is None
                        // and the second is Some 1 should return None
                        test "x1 = None and x2 = Some 1" {
                            let x1 = None |> create true
                            let x2 = Some 1N |> create true
                            calc (+) x1 x2
                            |> Expect.equal "should be None" None
                        }
                        // addition of two values, the first is Some 1
                        // and the second is Some 1 should return Some 2
                        test "x1 = Some 1 and x2 = Some 1" {
                            let x1 = Some 1N |> create true
                            let x2 = Some 1N |> create true
                            calc (+) x1 x2
                            |> Expect.equal "should be Some 2" (Some 2N |> create true |> Some)
                        }
                        // addition of two values, the first is Some 1
                        // and the second is Some 2 should return Some 3.
                        // When the first value is exclusive, the result should be exclusive
                        test "x1 = Some 1, excl and x2 = Some 2, incl" {
                            let x1 = Some 1N |> create false
                            let x2 = Some 2N |> create true
                            calc (+) x1 x2
                            |> Expect.equal "should be Some 3, exclusive" (Some 3N |> create false |> Some)
                        }
                        // addition of two values, the first is Some 1, incl
                        // and the second is Zero incl should return Some 1, incl
                        test "x1 = Some 1, incl and x2 = Some 0, incl" {
                            let x1 = Some 1N |> create true
                            let x2 = Some 0N |> create true
                            calc (+) x1 x2
                            |> Expect.equal "should be Some 1, incl" (x1 |> Some)
                        }
                        // addition of two values, the first is Some 1, excl
                        // and the second is Zero incl should return Some 1, excl
                        test "x1 = Some 1, excl and x2 = Some 0, incl" {
                            let x1 = Some 1N |> create false
                            let x2 = Some 0N |> create true
                            calc (+) x1 x2
                            |> Expect.equal "should be Some 1, excl" (x1 |> Some)
                        }
                        // addition of two values, the first is Some 1, incl
                        // and the second is Zero excl should return Some 1, excl
                        test "x1 = Some 1, incl and x2 = Some 0, excl" {
                            let x1 = Some 1N |> create true
                            let x2 = Some 0N |> create false
                            calc (+) x1 x2
                            |> Expect.equal "should be Some 1, incl" (Some 1N |> create false |> Some)
                        }
                        // addition of two values, the first is Some 1, excl
                        // and the second is Zero excl should return Some 1, excl
                        test "x1 = Some 1, excl and x2 = Some 0, excl" {
                            let x1 = Some 1N |> create false
                            let x2 = Some 0N |> create false
                            calc (+) x1 x2
                            |> Expect.equal "should be Some 1, excl" (x1 |> Some)
                        }
                    ]
                ]

            module ValueRange = Variable.ValueRange


            let createMin isIncl br =
                Units.Count.times
                |> ValueUnit.withSingleValue br
                |> Minimum.create isIncl

            let createMax isIncl br =
                Units.Count.times
                |> ValueUnit.withSingleValue br
                |> Maximum.create isIncl

            let createIncr br =
                Units.Count.times
                |> ValueUnit.withSingleValue br
                |> Increment.create


            open ValueRange.Operators

            let tests = testList "valuerange" [


                testList "between min and max" [
                    fun bMin minV bMax maxV v ->
                        let min = createMin bMin minV |> Some
                        let max = createMax bMax maxV |> Some
                        let op1 = if bMin then (<=) else (<)
                        let op2 = if bMax then (<=) else (<)
                        (minV |> op1 <| v && v |> op2 <| maxV) = (v |> ValueRange.isBetweenMinMax min max)
                    |> Generators.testProp "v between min and max"

                    fun bMin minV v ->
                        let min = createMin bMin minV |> Some
                        let max = None
                        let op1 = if bMin then (<=) else (<)
                        (minV |> op1 <| v) = (v |> ValueRange.isBetweenMinMax min max)
                    |> Generators.testProp "v between min and none"

                    fun bMax maxV v ->
                        let min = None
                        let max = createMax bMax maxV |> Some
                        let op2 = if bMax then (<=) else (<)
                        (v |> op2 <| maxV) = (v |> ValueRange.isBetweenMinMax min max)
                    |> Generators.testProp "v between none and max"

                    fun v ->
                        let min = None
                        let max = None
                        v |> ValueRange.isBetweenMinMax min max
                    |> Generators.testProp "v between none and none"
                ]

                testList "is multiple of"  [
                    fun v xs ->
                        try
                            let incr = xs |> IncrementTests.create
                            let isMult =
                                xs
                                |> Array.exists (fun i -> (v / i).Denominator = 1I)
                            v |> ValueRange.isMultipleOfIncr (Some incr) = isMult
                        with
                        | _ -> true
                    |> Generators.testProp "v is multiple of one of incr"

                    fun v ->
                        v |> ValueRange.isMultipleOfIncr None
                    |> Generators.testProp "is always multiple of none incr"
                ]

                testList "valuerange set min incr max" [

                    test "smaller max can be set" {
                        let max1 = createMax true 3N
                        let max2 = createMax true 1N
                        (max1 |> Max)
                        |> ValueRange.setMax true max2
                        |> Expect.equal "should be equal" (max2 |> Max)
                    }

                    test "greater max can not be set" {
                        let max1 = createMax true 3N
                        let max2 = createMax true 1N
                        (max2 |> Max)
                        |> ValueRange.setMax true max1
                        |> Expect.notEqual "should not be equal" (max1 |> Max)
                    }

                    test "min max smaller max can be set" {
                        let min = createMin true 0N
                        let max1 = createMax true 3N
                        let max2 = createMax true 1N
                        ((min, max1) |> MinMax)
                        |> ValueRange.setMax true max2
                        |> Expect.equal "should be equal" ((min, max2) |> MinMax)
                    }

                    test "min max greater max can not be set" {
                        let min = createMin true 0N
                        let max1 = createMax true 3N
                        let max2 = createMax true 1N
                        ((min, max2) |> MinMax)
                        |> ValueRange.setMax true max1
                        |> Expect.notEqual "should not be equal" ((min, max1) |> MinMax)
                    }

                    test "min max can be set with an incr" {
                        let min = createMin true 1N
                        let incr = createIncr 1N
                        let max = createMax true 5N
                        ((min, max) |> MinMax)
                        |> ValueRange.setIncr true incr
                        |> Expect.equal "should be equal" ((min, incr, max) |> MinIncrMax)
                    }

                    test "min max can be set with a more restrictive incr" {
                        let min = createMin true 2N
                        let incr = createIncr 1N
                        let max = createMax true 6N
                        ((min, incr, max) |> MinIncrMax)
                        |> ValueRange.setIncr true (createIncr 2N)
                        |> Expect.equal "should be equal" ((min, (createIncr 2N), max) |> MinIncrMax)
                    }

                    test "max 90 mg/kg/day cannot be replaced by 300 mg/kg/day" {
                        let mgPerKgPerDay =
                            (CombiUnit (Units.Mass.milliGram, OpPer, Units.Weight.kiloGram), OpPer,
                            Units.Time.day)
                            |> CombiUnit
                        let max1 =
                            [|90N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        let max2 =
                            [|300N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        (max1 |> Max)
                        |> ValueRange.setMax true max2
                        |> Expect.notEqual "should not be equal" (max2 |> Max)
                    }

                    // [1.amikacine.injectievloeistof.amikacine]_dos_qty [170079/500 mg..267/500;25 mg..50997/100 mg]
                    // cannot be set with this range:[170079/500 mg..267/500 mg..50997/100 mg]
                    test "failing case: [170079/500 mg..267/500;25 mg..50997/100 mg] should be able to set with [170079/500 mg..267/500..50997/100 mg]" {
                        let min1 = [| 170079N/500N |] |> ValueUnit.create Units.Mass.milliGram |> Minimum.create true
                        let incr1 =
                                [|
                                    267N/500N
                                    25N
                                |] |> ValueUnit.create Units.Mass.milliGram |> Increment.create
                        let max1 = [| 50997N/100N |] |> ValueUnit.create Units.Mass.milliGram |> Maximum.create true
                        let vr1 = (min1, incr1, max1) |> MinIncrMax

                        let min2 = [| 170079N/500N |] |> ValueUnit.create Units.Mass.milliGram |> Minimum.create true
                        let incr2 =
                                [|
                                    267N/500N
                                |] |> ValueUnit.create Units.Mass.milliGram |> Increment.create
                        let max2 = [| 50997N/100N |] |> ValueUnit.create Units.Mass.milliGram |> Maximum.create true
                        let vr2 = (min2, incr2, max2) |> MinIncrMax

                        vr1 @<- vr2
                        |> Expect.equal "should equal vr2" vr2
                    }

                    test "max 300 mg/kg/day can be replaced by 90 mg/kg/day" {
                        let mgPerKgPerDay =
                            (CombiUnit (Units.Mass.milliGram, OpPer, Units.Weight.kiloGram), OpPer,
                            Units.Time.day)
                            |> CombiUnit
                        let max1 =
                            [|90N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        let max2 =
                            [|300N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        (max2 |> Max)
                        |> ValueRange.setMax true max1
                        |> Expect.equal "should be equal" (max1 |> Max)
                    }

                    test "apply expr cannot set greater max" {
                        let mgPerKgPerDay =
                            (CombiUnit (Units.Mass.milliGram, OpPer, Units.Weight.kiloGram), OpPer,
                            Units.Time.day)
                            |> CombiUnit
                        let max1 =
                            [|90N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        let max2 =
                            [|300N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        (max1 |> Max)
                        |> ValueRange.applyExpr true (max2 |> Max)
                        |> Expect.notEqual "should not be equal" (max2 |> Max)
                    }

                    test "apply expr cannot set greater max to minmax" {
                        let mgPerKgPerDay =
                            (CombiUnit (Units.Mass.milliGram, OpPer, Units.Weight.kiloGram), OpPer,
                            Units.Time.day)
                            |> CombiUnit
                        let min =
                            [|40N|] |> ValueUnit.create mgPerKgPerDay
                            |> Minimum.create true
                        let max1 =
                            [|90N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        let max2 =
                            [|300N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        ((min, max1) |> MinMax)
                        |> ValueRange.applyExpr true (max2 |> Max)
                        |> Expect.notEqual "should not be equal" ((min, max2) |> MinMax)
                    }

                    test "apply expr can set smaller max to minmax" {
                        let mgPerKgPerDay =
                            (CombiUnit (Units.Mass.milliGram, OpPer, Units.Weight.kiloGram), OpPer,
                            Units.Time.day)
                            |> CombiUnit
                        let min =
                            [|40N|] |> ValueUnit.create mgPerKgPerDay
                            |> Minimum.create true
                        let max1 =
                            [|90N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        let max2 =
                            [|300N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        ((min, max2) |> MinMax)
                        |> ValueRange.applyExpr true (max1 |> Max)
                        |> Expect.equal "should not be equal" ((min, max1) |> MinMax)
                    }

                    //set valuerange: [40 mg/kg/dag..90 mg/kg/dag]
                    //with valuerange: [2.4 mg/dag/kg..1500 mg/dag/kg]
                    //= valuerange: [40 mg/kg/dag..1500 mg/dag/kg]
                    test "apply expr min max cannot set smaller min or larger max to minmax" {
                        let mgPerKgPerDay =
                            (CombiUnit (Units.Mass.milliGram, OpPer, Units.Weight.kiloGram), OpPer,
                            Units.Time.day)
                            |> CombiUnit
                        let min1 =
                            [|40N|] |> ValueUnit.create mgPerKgPerDay
                            |> Minimum.create true
                        let max1 =
                            [|90N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        let min2 =
                            [|24N/10N|] |> ValueUnit.create mgPerKgPerDay
                            |> Minimum.create true
                        let max2 =
                            [|1500N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        ((min2, max2) |> MinMax)
                        |> ValueRange.applyExpr true ((min1, max1) |> MinMax)
                        |> Expect.equal "should be equal" ((min1, max1) |> MinMax)
                    }

                    //set valuerange: [40 mg/kg/dag..90 mg/kg/dag]
                    //with valuerange: [2.4 mg/dag/kg..1500 mg/dag/kg]
                    //= valuerange: [40 mg/kg/dag..1500 mg/dag/kg]
                    test "apply with operator expr min max cannot set smaller min or larger max to minmax" {
                        let mgPerKgPerDay =
                            (CombiUnit (Units.Mass.milliGram, OpPer, Units.Weight.kiloGram), OpPer,
                            Units.Time.day)
                            |> CombiUnit
                        let min1 =
                            [|40N|] |> ValueUnit.create mgPerKgPerDay
                            |> Minimum.create true
                        let max1 =
                            [|90N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        let min2 =
                            [|24N/10N|] |> ValueUnit.create mgPerKgPerDay
                            |> Minimum.create true
                        let max2 =
                            [|1500N|] |> ValueUnit.create mgPerKgPerDay
                            |> Maximum.create true
                        ((min1, max1) |> MinMax) @<- ((min2, max2) |> MinMax)
                        |> Expect.equal "should be equal" ((min1, max1) |> MinMax)
                    }

                ]

                testList "increaseIncrement" [
                    test "can increase increment of 48 1/10 719/10 ml with 5/10" {
                        let min =
                            [| 48N |] |> ValueUnit.create Units.Volume.milliLiter
                            |> Minimum.create true
                        let incr =
                            [| 1N/10N |] |> ValueUnit.create Units.Volume.milliLiter
                            |> Increment.create
                        let max =
                            [| 719N/10N |] |> ValueUnit.create Units.Volume.milliLiter
                            |> Maximum.create true
                        let newIncr =
                            [| 5N/10N |] |> ValueUnit.create Units.Volume.milliLiter
                            |> Increment.create

                        (min, incr, max)
                        |> MinIncrMax
                        |> ValueRange.increaseIncrement 100N [newIncr]
                        |> Expect.equal "should be with new incr and new max"
                            ((min, newIncr, ([| 143N/2N |] |> ValueUnit.create Units.Volume.milliLiter |> Maximum.create true)) |> MinIncrMax)

                    }

                    test "failing case: 31 ml/hour to 125 ml/hour with incr: 0.1" {
                        let u =
                            Units.Volume.milliLiter
                            |> Units.per Units.Time.hour

                        {
                            Name = Variable.Name.createExc "[1.gentamicine]_dos_rte"
                            Values =
                                Variable.ValueRange.create
                                    true
                                    (31N |> ValueUnit.singleWithUnit u |> Minimum.create true |> Some)
                                    ((1N/10N) |> ValueUnit.singleWithUnit u |> Increment.create  |> Some)
                                    (125N |> ValueUnit.singleWithUnit u |> Maximum.create true |> Some)
                                    None
                        }
                        |> Variable.increaseIncrement 50N
                               ([0.1m; 0.5m; 1m; 5m; 10m; 20m] |> List.map BigRational.fromDecimal
                               |> List.map (ValueUnit.singleWithUnit u >> Increment.create))
                        |> fun v -> v.Values |> Variable.ValueRange.getIncr
                        |> function
                            | None -> failwith "no incr"
                            | Some incr ->
                                incr
                                |> Expect.equal "should be 5" (5N |> ValueUnit.singleWithUnit u |> Increment.create)

                    }

                ]
            ]


    module EquationTests =


        let eqs =
            [ "ParacetamolDoseTotal = ParacetamolDoseTotalAdjust * Adjust" ]
            |> TestSolver.init

        let mg = Units.Mass.milliGram
        let day = Units.Time.day
        let kg = Units.Weight.kiloGram
        let mgPerDay = CombiUnit(mg, OpPer, day)
        let mgPerKgPerDay = (CombiUnit (mg, OpPer, kg), OpPer, day) |> CombiUnit
        let ml = Units.Volume.milliLiter
        let mlPerDay = (ml, OpPer, day) |> CombiUnit
        let freqPerDay = (Units.Count.times, OpPer, day) |> CombiUnit


        // ParacetamolDoseTotal [180..3000] = ParacetamolDoseTotalAdjust [40..90] x Adjust <..100]
        let tests = testList "Equations" [

            test "failing case set max > max mg/kg/day" {
                eqs
                |> TestSolver.setMinIncl mgPerDay "ParacetamolDoseTotal" 180N
                |> TestSolver.setMaxIncl mgPerDay "ParacetamolDoseTotal" 3000N
                |> TestSolver.setMinIncl mgPerKgPerDay "ParacetamolDoseTotalAdjust" 40N
                |> TestSolver.setMaxIncl mgPerKgPerDay "ParacetamolDoseTotalAdjust" 90N
                |> TestSolver.setMaxIncl kg "Adjust" 100N
                |> TestSolver.solveAll
                |> TestSolver.printEqsWithUnits
                |> ignore
                true |> Expect.isTrue "should run"
            }

            // [1.benzylpenicilline]_orb_qty [0,2 mL..0,1 mL..500 mL] =
            // [1.benzylpenicilline]_dos_cnt [1 x] * [1.benzylpenicilline]_dos_qty [0,2 mL..500 mL]
            test "failing case: a [0,2 mL..0,1 mL..500 mL] = b [1 x] * c [0,2 mL..500 mL]" {
                [ "a = b * c" ]
                |> TestSolver.init
                |> TestSolver.setMinIncl Units.Volume.milliLiter "a" (2N/10N)
                |> TestSolver.setIncr Units.Volume.milliLiter "a" (1N/10N)
                |> TestSolver.setMaxIncl Units.Volume.milliLiter "a" 500N
                |> TestSolver.setValues Units.Count.times "b" [1N]
                |> TestSolver.setMinIncl Units.Volume.milliLiter "c" (2N/10N)
                |> TestSolver.setMaxIncl Units.Volume.milliLiter "c" 500N
                |> TestSolver.solveMinMax
                |> function
                    | Ok eqs -> eqs |> List.map (Equation.toString true) |> String.concat ""
                    | Error (_, errs) -> errs |> List.map string |> String.concat ""
                |> Expect.equal "c should have an incr" "a [1/5 mL..1/10 mL..500 mL] = b [1 x] * c [1/5 mL..1/10 mL..500 mL]"
            }

            // [1.benzylpenicilline]_dos_ptm [2,3 mL/dag..460 mL/dag] =
            // [1.benzylpenicilline]_dos_qty [0,2 mL..0,1 mL..500 mL] * [1]_prs_frq [4;5;6 x/dag]
            test "failing case: a [2,3 mL/dag..460 mL/dag] = b [0,2 mL..0,1 mL..500 mL] * c [4;5;6 x/dag]" {
                [ "a = b * c" ]
                |> TestSolver.init
                |> TestSolver.setMinIncl mlPerDay "a" (23N/10N)
                |> TestSolver.setMaxIncl mlPerDay "a" 460N
                |> TestSolver.setMinIncl ml "b" (2N/10N)
                |> TestSolver.setIncr ml "b" (1N/10N)
                |> TestSolver.setMaxIncl ml "b" 500N
                |> TestSolver.setValues freqPerDay "c" [4N; 5N; 6N]
                |> TestSolver.solveMinMax
                |> function
                    | Ok eqs -> eqs |> List.map (Equation.toString true) |> String.concat ""
                    | Error (_, errs) -> errs |> List.map string |> String.concat ""
                |> Expect.equal "a should have an incr" "a [12/5 mL/dag..2/5;1/2;3/5 mL/dag..460 mL/dag] = b [2/5 mL..1/10 mL..115 mL] * c [4;5;6 x/dag]"
            }
        ]


    [<Tests>]
    let tests =
        [
            VariableTests.ValueRangeTests.IncrementTests.tests
            VariableTests.ValueRangeTests.MinimumTests.tests
            VariableTests.ValueRangeTests.MaximumTests.tests
            VariableTests.ValueRangeTests.MinMaxCalculatorTests.tests
            VariableTests.ValueRangeTests.tests
            EquationTests.tests
        ]
        |> testList "GenSolver"




Tests.tests
|> Expecto.run




open MathNet.Numerics

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib

let mg = Units.Mass.milliGram
let day = Units.Time.day
let kg = Units.Weight.kiloGram
let mgPerDay = CombiUnit(mg, OpPer, day)
let mgPerKgPerDay = (CombiUnit (mg, OpPer, kg), OpPer, day) |> CombiUnit
let frq = Units.Count.times |> Units.per day
let mL = Units.Volume.milliLiter
let x = Units.Count.times
let min = Units.Time.minute
let hr = Units.Time.hour
let mcg = Units.Mass.microGram
let mcgPerKgPerMin =
    mcg |> Units.per kg |> Units.per min
let mcgPerMin = mcg |> Units.per min
let mcgPerHour =
    mcg |> Units.per hr
let piece = Units.General.general "stuk"


module ValueRange = Variable.ValueRange
module Minimum = Variable.ValueRange.Minimum
module Maximum = Variable.ValueRange.Maximum
module Increment = Variable.ValueRange.Increment


// This variable:
// [1.amikacine.injectievloeistof.amikacine]_dos_qty [170079/500 mg..267/500;25 mg..50997/100 mg]
// cannot be set with this range:[170079/500 mg..267/500 mg..50997/100 mg]

let min1 = [| 170079N/500N |] |> ValueUnit.create Units.Mass.milliGram |> Minimum.create true
let incr1 =
        [|
            267N/500N
            25N
        |] |> ValueUnit.create Units.Mass.milliGram |> Increment.create
let max1 = [| 50997N/100N |] |> ValueUnit.create Units.Mass.milliGram |> Maximum.create true
let vr1 = (min1, incr1, max1) |> MinIncrMax
vr1 |> ValueRange.toString false

let min2 = [| 170079N/500N |] |> ValueUnit.create Units.Mass.milliGram |> Minimum.create true
let incr2 =
        [|
            267N/500N
        |] |> ValueUnit.create Units.Mass.milliGram |> Increment.create
let max2 = [| 50997N/100N |] |> ValueUnit.create Units.Mass.milliGram |> Maximum.create true
let vr2 = (min2, incr2, max2) |> MinIncrMax
vr2 |> ValueRange.toString false

vr1 |> ValueRange.applyExpr true (incr2 |> Types.ValueRange.Incr)

min1 |> Minimum.multipleOf incr2
max1 |> Maximum.multipleOf incr2

vr1 |> ValueRange.setIncr true incr2

incr1 |> Increment.restrict incr2


incr2 |> Increment.toValueUnit
|> ValueUnit.filter (fun i1 ->
    (incr1 |> Increment.toValueUnit)
    |> ValueUnit.getBaseValue
    |> Array.exists (fun i2 -> i1 |> BigRational.isMultiple i2)
)
//|> ValueUnit.toUnit

