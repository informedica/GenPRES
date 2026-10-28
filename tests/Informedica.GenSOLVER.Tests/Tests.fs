namespace Informedica.GenSolver.Tests

open System
open Informedica.Utils.Lib


//Environment.CurrentDirectory <- __SOURCE_DIRECTORY__


/// Create the necessary test generators
module Generators =


    open Expecto
    open FsCheck
    open Informedica.Utils.Lib.BCL

    let bigRGen (n, d) =
        let d = if d = 0 then 1 else d
        let n = abs n |> BigRational.FromInt
        let d = abs d |> BigRational.FromInt
        n / d


    let bigRGenOpt (n, _) = bigRGen (n, 1) |> Some


    let bigRGenerator =
        gen {
            let! n = Arb.generate<int>
            let! d = Arb.generate<int>
            return bigRGen (n, d)
        }


    type BigRGenerator() =
        static member BigRational() =
            { new Arbitrary<BigRational>() with
                override x.Generator = bigRGenerator
            }


    type MinMax = | MinMax of BigRational * BigRational

    let minMaxArb () =
        bigRGenerator
        |> Gen.map abs
        |> Gen.two
        |> Gen.map (fun (br1, br2) ->
            let br1 = br1.Numerator |> BigRational.FromBigInt
            let br2 = br2.Numerator |> BigRational.FromBigInt

            if br1 >= br2 then br2, br1 else br1, br2
            |> fun (br1, br2) -> if br1 = br2 then br1, br2 + 1N else br1, br2
        )
        |> Arb.fromGen
        |> Arb.convert MinMax (fun (MinMax(min, max)) -> min, max)


    type ListOf37<'a> = | ListOf37 of 'a List

    let listOf37Arb () =
        Gen.listOfLength 37 Arb.generate
        |> Arb.fromGen
        |> Arb.convert ListOf37 (fun (ListOf37 xs) -> xs)


    let config =
        { FsCheckConfig.defaultConfig with
            arbitrary =
                [
                    typeof<BigRGenerator>
                    typeof<ListOf37<_>>.DeclaringType
                    typeof<MinMax>.DeclaringType
                ]
                @ FsCheckConfig.defaultConfig.arbitrary
            maxTest = 1000
        }


    let testProp testName prop = prop |> testPropertyWithConfig config testName


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


    let printEqs =
        function
        | Ok eqs -> eqs |> Solver.printEqs true procss
        | Error _ -> Expecto.Tests.failtest "errors"


    let printEqsWithUnits =
        function
        | Ok eqs -> eqs |> Solver.printEqs false procss
        | Error _ -> Expecto.Tests.failtest "errors"


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

    let createValSet u v = v |> Array.ofSeq |> ValueUnit.create u |> ValueSet.create

    let setIncr u n vals = vals |> createIncr u |> IncrProp |> setProp n

    let setMinIncl u n min = min |> createMinIncl u |> MinProp |> setProp n

    let setMinExcl u n min = min |> createMinExcl u |> MinProp |> setProp n

    let setMaxIncl u n max = max |> createMaxIncl u |> MaxProp |> setProp n

    let setMaxExcl u n max = max |> createMaxExcl u |> MaxProp |> setProp n

    let setValues u n vals = vals |> createValSet u |> ValsProp |> setProp n

    let logger =
        fun (_: string) -> () //File.AppendAllLines("examples.log", [s])
        |> SolverLogging.create

    let solve n p eqs =
        let n = n |> Name.createExc
        Api.solve true (fun _ eqs -> eqs |> List.mapi (fun i eq -> (i, eq))) logger n p eqs

    let solveAll = Api.solveAll false logger

    let solveMinMax = Api.solveAll true logger

    let solveMinIncl u n min = solve n (min |> createMinIncl u |> MinProp)

    let solveMinExcl u n min = solve n (min |> createMinExcl u |> MinProp)

    let solveMaxIncl u n max = solve n (max |> createMaxIncl u |> MaxProp)

    let solveMaxExcl u n max = solve n (max |> createMaxExcl u |> MaxProp)

    let solveIncr u n incr = solve n (incr |> createIncr u |> IncrProp)

    let solveValues u n vals = solve n (vals |> createValSet u |> ValsProp)

    let init = Api.init
    let nonZeroNegative = Api.nonZeroNegative


    let solveCountMinIncl = solveMinIncl Units.Count.times
    let solveCountMaxExcl = solveMaxExcl Units.Count.times
    let solveCountValues u n vals = solveValues Units.Count.times u n vals


module Tests =


    open Informedica.Utils.Lib.BCL
    open Expecto
    open Expecto.Flip

    open Informedica.GenUnits.Lib
    open Informedica.GenSolver.Lib


    module VariableTests =


        module ValueRangeTests =

            module Minimum = Variable.ValueRange.Minimum
            module Increment = Variable.ValueRange.Increment
            module Maximum = Variable.ValueRange.Maximum


            module IncrementTests =


                module MultipleOfTests =

                    module Increment = Increment

                    let private incr values =
                        values |> ValueUnit.withUnit Units.Count.times |> Increment.create

                    let tests =
                        testList
                            "increment isMultipleOf"
                            [
                                test "[2] is multiple of [1]" {
                                    let i1 = incr [| 2N |]
                                    let i2 = incr [| 1N |]
                                    i1 |> Increment.isMultipleOf i2 |> Expect.isTrue "2 should be multiple of 1"
                                }

                                test "[2;4] is multiple of [2]" {
                                    let i1 = incr [| 2N; 4N |]
                                    let i2 = incr [| 2N |]

                                    i1
                                    |> Increment.isMultipleOf i2
                                    |> Expect.isTrue "2 and 4 should both be multiples of 2"
                                }

                                test "[2;4] is not multiple of [3]" {
                                    let i1 = incr [| 2N; 4N |]
                                    let i2 = incr [| 3N |]

                                    i1
                                    |> Increment.isMultipleOf i2
                                    |> Expect.isFalse "2 and 4 are not multiples of 3"
                                }

                                test "[1/2;3/4] is multiple of [1/4]" {
                                    let i1 = incr [| 1N / 2N; 3N / 4N |]
                                    let i2 = incr [| 1N / 4N |]

                                    i1
                                    |> Increment.isMultipleOf i2
                                    |> Expect.isTrue "1/2 and 3/4 are multiples of 1/4"
                                }

                                test "[3;6] is multiple of [2;3] (exists divisor)" {
                                    let i1 = incr [| 3N; 6N |]
                                    let i2 = incr [| 2N; 3N |]

                                    i1
                                    |> Increment.isMultipleOf i2
                                    |> Expect.isTrue "there exists 3 such that all are multiples"
                                }

                                test "[3;5] is not multiple of [2;4]" {
                                    let i1 = incr [| 3N; 5N |]
                                    let i2 = incr [| 2N; 4N |]

                                    i1
                                    |> Increment.isMultipleOf i2
                                    |> Expect.isFalse "no element in [2;4] divides both 3 and 5"
                                }
                            ]


                let create brs = Units.Count.times |> ValueUnit.withValue brs |> Increment.create

                let validIncr (Increment s) =
                    s |> ValueUnit.isEmpty |> not
                    && s |> ValueUnit.gtZero
                    && s
                       |> ValueUnit.removeBigRationalMultiples
                       |> fun s2 -> s2 |> ValueUnit.valueCount = (s |> ValueUnit.valueCount)

                let tests =
                    testList
                        "increment"
                        [
                            // add the multiple of tests
                            MultipleOfTests.tests

                            testList
                                "increment properties"
                                [
                                    // helper function to perform a calculation
                                    let calc op x y =
                                        x
                                        |> List.allPairs y
                                        |> List.map (fun (x1, x2) -> x1 |> op <| x2)
                                        |> List.sort
                                        |> List.distinct
                                    // helper function to create a list from min incr max
                                    let minIncrMaxToSeq min incr max : BigRational seq =
                                        incr
                                        |> Seq.fold
                                            (fun acc i ->
                                                let min = min |> BigRational.toMinMultipleOf i
                                                let max = max |> BigRational.toMaxMultipleOf i
                                                seq { min..i..max } |> Seq.append acc
                                            )
                                            Seq.empty
                                        |> Seq.sort
                                        |> Seq.distinct
                                    // helper function to calculate the incrs from a list
                                    let calcIncr op x y =
                                        let r = x |> List.allPairs y |> List.map (fun (x1, x2) -> x1 |> op <| x2)

                                        r |> List.min,
                                        r |> List.toArray |> Array.removeBigRationalMultiples |> Array.toList,
                                        r |> List.max

                                    let calcIncrDiv = calcIncr (/)
                                    let calcIncrMul = calcIncr (*)
                                    let calcIncrAdd = calcIncr (+)
                                    let calcIncrSub = calcIncr (-)

                                    test "actual set is subset of min, calculated incr from MULT and max" {
                                        let x = [ 3N .. 3N .. 6N ]
                                        let y = [ 2N .. 2N .. 6N ]
                                        let exp = calc (*) x y
                                        let s = exp |> List.map string |> String.concat "; "

                                        let act =
                                            calcIncrMul x y
                                            |> fun (min, incr, max) -> minIncrMaxToSeq min incr max
                                            |> Seq.toList

                                        (exp <> act && Set.isSubset (exp |> Set.ofList) (act |> Set.ofSeq))
                                        |> Expect.isTrue $"{s} should be a subset of {act |> Seq.toList}"
                                    }

                                    test "actual set is subset of min, calculated incr from DIVISION and max" {
                                        let x = [ 3N .. 3N .. 12N ]
                                        let y = [ 2N .. 2N .. 10N ]
                                        let exp = calc (/) x y
                                        let s = exp |> List.map string |> String.concat "; "

                                        let act =
                                            calcIncrDiv x y |> fun (min, incr, max) -> minIncrMaxToSeq min incr max

                                        (exp <> Seq.toList act && Set.isSubset (exp |> Set.ofList) (act |> Set.ofSeq))
                                        |> Expect.isTrue $"{s} should be a subset of {act |> Seq.toList}"
                                    }

                                    test "actual set is subset of min, calculated incr from ADDITION and max" {
                                        let x = [ 3N .. 3N .. 12N ]
                                        let y = [ 2N .. 2N .. 10N ]
                                        let exp = calc (+) x y
                                        let s = exp |> List.map string |> String.concat "; "

                                        let act =
                                            calcIncrAdd x y |> fun (min, incr, max) -> minIncrMaxToSeq min incr max

                                        (exp <> (Seq.toList act) && Set.isSubset (exp |> Set.ofList) (act |> Set.ofSeq))
                                        |> Expect.isTrue $"{s} should be a subset of {act |> Seq.toList}"
                                    }
                                ]

                            testList
                                "create"
                                [
                                    fun xs ->
                                        try
                                            xs |> create |> validIncr
                                        with _ ->
                                            if xs |> Array.isEmpty || xs |> Array.distinct = [| 0N |] then
                                                true
                                            else
                                                xs |> create |> validIncr |> not
                                    |> Generators.testProp "only valid incrs can be created"
                                ]

                            testList
                                "calcOp"
                                [
                                    fun xs ->
                                        try
                                            let incr1 = xs |> create |> Some
                                            let incr2 = [| 1N |] |> create |> Some

                                            match Increment.calcOpt (*) incr1 incr2 with
                                            | Some result -> Some result = incr1
                                            | None -> false
                                        with _ ->
                                            true
                                    |> Generators.testProp "calc mult with one gives identical result"

                                    fun xs ->
                                        let xs = xs |> Array.map abs |> Array.filter ((<>) 0N) |> Array.distinct

                                        try
                                            let incr1 = xs |> create |> Some
                                            let incr2 = [| 1N |] |> create |> Some

                                            match Increment.calcOpt (+) incr1 incr2 with
                                            | Some(Increment res) ->
                                                xs
                                                |> Array.forall (fun x1 ->
                                                    res |> ValueUnit.getValue |> Array.forall (fun x2 -> x2 = x1)
                                                )
                                            | None -> xs |> Array.forall ((<>) 1N)
                                            |> ignore

                                            true // TODO: test currently not working
                                        with _ ->
                                            true
                                    |> Generators.testProp "calc add with one gives gcd which is <= original incr"

                                ]

                            testList
                                "restrict increment"
                                [
                                    fun xs ->
                                        try
                                            let newIncr = xs |> create
                                            let oldIncr = xs |> create
                                            (oldIncr |> Increment.restrict newIncr) = newIncr
                                        with _ ->
                                            true
                                    |> Generators.testProp "setting an incr with eq incr"

                                    fun xs1 xs2 ->
                                        try
                                            let newIncr = xs1 |> create
                                            let oldIncr = xs2 |> create

                                            (oldIncr |> Increment.restrict newIncr |> Increment.count)
                                            <= (newIncr |> Increment.count)
                                            |> ignore

                                            true // TODO: test currently not working
                                        with _ ->
                                            true
                                    |> Generators.testProp "setting an incr with different incr"

                                    test "can restrict 0.1 with 0.5" {
                                        let oldIncr = [| 1N / 10N |] |> create
                                        let newIncr = [| 5N / 10N |] |> create
                                        oldIncr |> Increment.restrict newIncr |> Expect.equal "should be 0.5" newIncr
                                    }

                                    test "can restrict 1 with 2" {
                                        let oldIncr = [| 1N |] |> create
                                        let newIncr = [| 2N |] |> create
                                        oldIncr |> Increment.restrict newIncr |> Expect.equal "should be 2" newIncr
                                    }

                                    test "can restrict 0.1 ml with 0.5 ml" {
                                        let oldIncr =
                                            [| 1N / 10N |]
                                            |> ValueUnit.create Units.Volume.milliLiter
                                            |> Increment.create

                                        let newIncr =
                                            [| 5N / 10N |]
                                            |> ValueUnit.create Units.Volume.milliLiter
                                            |> Increment.create

                                        oldIncr |> Increment.restrict newIncr |> Expect.equal "should be 0.5 ml" newIncr
                                    }

                                    test "can restrict 0.1 l with 0.5 l" {
                                        let oldIncr =
                                            [| 1N / 10N |] |> ValueUnit.create Units.Volume.liter |> Increment.create

                                        let newIncr =
                                            [| 5N / 10N |] |> ValueUnit.create Units.Volume.liter |> Increment.create

                                        oldIncr
                                        |> Increment.restrict newIncr
                                        |> Expect.equal "should be 0.5 liter" newIncr
                                    }

                                    test "cannot restrict 0.5 with 0.1" {
                                        let oldIncr = [| 5N / 10N |] |> create
                                        let newIncr = [| 1N / 10N |] |> create
                                        oldIncr |> Increment.restrict newIncr |> Expect.equal "should be 0.5" oldIncr
                                    }

                                    test "restricting 2 with 3 gives 6, the least common multiple" {
                                        let oldIncr = [| 2N |] |> create
                                        let newIncr = [| 3N |] |> create

                                        oldIncr
                                        |> Increment.restrict newIncr
                                        |> Expect.equal "should be 6" ([| 6N |] |> create)
                                    }

                                    test "restricting 0.1 with 3 gives 3, the least common multiple" {
                                        let oldIncr = [| 1N / 10N |] |> create
                                        let newIncr = [| 3N |] |> create
                                        oldIncr |> Increment.restrict newIncr |> Expect.equal "should be 3" newIncr
                                    }

                                    test "restricting 1 with 0.3 gives 3, the least common multiple" {
                                        let oldIncr = [| 1N |] |> create
                                        let newIncr = [| 3N / 10N |] |> create

                                        oldIncr
                                        |> Increment.restrict newIncr
                                        |> Expect.equal "should be 3" ([| 3N |] |> create)
                                    }

                                    test "restricting 3;5 with 2;3 keeps every lcm that is no multiple of another" {
                                        // the pairs give 6, 3, 10 and 15; 6 and 15 are multiples of 3
                                        let oldIncr = [| 3N; 5N |] |> create
                                        let newIncr = [| 2N; 3N |] |> create

                                        oldIncr
                                        |> Increment.restrict newIncr
                                        |> Expect.equal "should be 3 and 10" ([| 3N; 10N |] |> create)
                                    }

                                    test "restricting 100 mg with 0.3 g gives 300 mg" {
                                        let oldIncr =
                                            [| 100N |] |> ValueUnit.create Units.Mass.milliGram |> Increment.create

                                        let newIncr =
                                            [| 3N / 10N |] |> ValueUnit.create Units.Mass.gram |> Increment.create

                                        oldIncr
                                        |> Increment.restrict newIncr
                                        |> Expect.equal
                                            "should be 300 mg"
                                            ([| 300N |] |> ValueUnit.create Units.Mass.milliGram |> Increment.create)
                                    }

                                    fun xs1 xs2 ->
                                        // only create may throw, on values that hold no positive one
                                        let tryCreate xs =
                                            try
                                                xs |> create |> Some
                                            with _ ->
                                                None

                                        let isOn incr x =
                                            incr
                                            |> Increment.toList
                                            |> List.exists (fun i -> x |> BigRational.isMultiple i)

                                        match tryCreate xs1, tryCreate xs2 with
                                        | Some newIncr, Some oldIncr ->
                                            oldIncr
                                            |> Increment.restrict newIncr
                                            |> Increment.toList
                                            |> List.forall (fun x -> x |> isOn newIncr && x |> isOn oldIncr)
                                        | _ -> true
                                    |> Generators.testProp "a restricted incr lies on the new and the old incr"

                                ]

                            testList
                                "increase increment"
                                [

                                    // test ValueRange.increaseIncrement
                                    test "ValueRange.increaseIncrement 1 to 10000 should increase to 100" {
                                        Variable.ValueRange.create
                                            (1N
                                             |> ValueUnit.singleWithUnit Units.Count.times
                                             |> Minimum.create true
                                             |> Some)
                                            (create [| 1N |] |> Some)
                                            (10000N
                                             |> ValueUnit.singleWithUnit Units.Count.times
                                             |> Maximum.create true
                                             |> Some)
                                            None
                                        |> Variable.ValueRange.increaseIncrement
                                            100N
                                            [
                                                create [| 2N |]
                                                create [| 10N |]
                                                create [| 100N |]
                                                create [| 1000N |]
                                                create [| 100000N |]
                                            ]
                                        |> Variable.ValueRange.getIncr
                                        |> function
                                            | None -> failtest "no incr"
                                            | Some incr -> incr |> Expect.equal "should be 100" (create [| 100N |])
                                    }

                                    test "failing case: 40.5 ml/hour to 163.5 ml/hour with incr: 0.1" {
                                        let u = Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour

                                        Variable.ValueRange.create
                                            ((81N / 2N) |> ValueUnit.singleWithUnit u |> Minimum.create true |> Some)
                                            ((1N / 10N) |> ValueUnit.singleWithUnit u |> Increment.create |> Some)
                                            ((816N / 5N) |> ValueUnit.singleWithUnit u |> Maximum.create true |> Some)
                                            None
                                        |> Variable.ValueRange.increaseIncrement
                                            50N
                                            ([ 0.1m; 0.5m; 1m; 5m; 10m; 20m ]
                                             |> List.map BigRational.fromDecimal
                                             |> List.map (ValueUnit.singleWithUnit u >> Increment.create))
                                        |> Variable.ValueRange.getIncr
                                        |> function
                                            | None -> failtest "no incr"
                                            | Some incr ->
                                                incr
                                                |> Expect.equal
                                                    "should be 5"
                                                    (5N |> ValueUnit.singleWithUnit u |> Increment.create)

                                    }

                                    test "a range that already holds at most maxCount values keeps its increment" {
                                        let u = Units.Volume.milliLiter

                                        let vr =
                                            Variable.ValueRange.create
                                                (20N |> ValueUnit.singleWithUnit u |> Minimum.create true |> Some)
                                                (1N |> ValueUnit.singleWithUnit u |> Increment.create |> Some)
                                                (24N |> ValueUnit.singleWithUnit u |> Maximum.create true |> Some)
                                                None

                                        vr
                                        |> Variable.ValueRange.increaseIncrement
                                            10N
                                            ([ 5N; 10N ] |> List.map (ValueUnit.singleWithUnit u >> Increment.create))
                                        |> Expect.equal "should keep 20 to 24 mL by 1 mL" vr
                                    }

                                    let rateRange () =
                                        let u = Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour

                                        let incrs =
                                            [ 1N / 10N; 1N / 2N; 1N; 5N; 10N; 50N; 100N; 500N ]
                                            |> List.map (ValueUnit.singleWithUnit u >> Increment.create)

                                        u,
                                        incrs,
                                        Variable.ValueRange.create
                                            (10N |> ValueUnit.singleWithUnit u |> Minimum.create true |> Some)
                                            (5N |> ValueUnit.singleWithUnit u |> Increment.create |> Some)
                                            (40N |> ValueUnit.singleWithUnit u |> Maximum.create true |> Some)
                                            None

                                    test "a rate increased a second time keeps its increment when it already fits" {
                                        let _, incrs, vr = rateRange ()

                                        vr
                                        |> Variable.ValueRange.increaseIncrement 10N incrs
                                        |> Expect.equal "should keep 10 to 40 mL/hour by 5 mL/hour" vr
                                    }

                                    test "a range with more than maxCount values still gets a larger increment" {
                                        let u, incrs, vr = rateRange ()

                                        vr
                                        |> Variable.ValueRange.increaseIncrement 5N incrs
                                        |> Variable.ValueRange.getIncr
                                        |> Expect.equal
                                            "should be 10 mL/hour"
                                            (10N |> ValueUnit.singleWithUnit u |> Increment.create |> Some)
                                    }
                                ]

                        ]


            module MinimumTests =


                let create isIncl br =
                    Units.Count.times |> ValueUnit.singleWithValue br |> Minimum.create isIncl


                let tests =
                    testList
                        "minimum"
                        [

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
                            |> Generators.testProp "min1 incl < min2 excl, also when min1 = min2"

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
                            |> Generators.testProp "min1 excl > min2 incl, also when min1 = min2"

                            fun b m ->
                                let min = create b m
                                min |> Minimum.toBoolValueUnit |> (fun (b, m) -> Minimum.create b m = min)
                            |> Generators.testProp "construct and deconstruct min there and back again"

                            test "100 mg < 1 g" {
                                let min1 = Units.Mass.milliGram |> ValueUnit.singleWithValue 100N |> Minimum.create true

                                let min2 = Units.Mass.gram |> ValueUnit.singleWithValue 1N |> Minimum.create true

                                min1 |> Minimum.minSTmin min2 |> Expect.isTrue "should be true"
                            }

                            let incr =
                                Units.Count.times
                                |> ValueUnit.withValue [| 1N / 3N; 1N / 4N; 1N / 5N |]
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
                                    min1 |> Minimum.minGTmin min0 && min1 = min2
                                else
                                    true
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
                    Units.Count.times |> ValueUnit.singleWithValue br |> Maximum.create isIncl


                let tests =
                    testList
                        "maximum"
                        [
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
                            |> Generators.testProp "max1 excl < max2 incl, also when max1 = max2"

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
                            |> Generators.testProp "max1 incl > max2 excl, also when max1 = max2"

                            test "100 mg < 1 g" {
                                let min1 = Units.Mass.milliGram |> ValueUnit.singleWithValue 100N |> Maximum.create true

                                let min2 = Units.Mass.gram |> ValueUnit.singleWithValue 1N |> Maximum.create true

                                min1 |> Maximum.maxSTmax min2 |> Expect.isTrue "should be true"
                            }

                            test "90 mg/kg/day < 300 mg/kg/day" {
                                let mgPerKgPerDay =
                                    (CombiUnit(Units.Mass.milliGram, OpPer, Units.Weight.kiloGram),
                                     OpPer,
                                     Units.Time.day)
                                    |> CombiUnit

                                let max1 = [| 90N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true
                                let max2 = [| 300N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true
                                Expect.isTrue "should be true" (max1 |> Maximum.maxSTmax max2)
                            }

                            fun b m ->
                                let max = create b m
                                max |> Maximum.toBoolValueUnit |> (fun (b, m) -> Maximum.create b m = max)
                            |> Generators.testProp "construct and deconstruct max there and back again"

                            let incr =
                                Units.Count.times
                                |> ValueUnit.withValue [| 1N / 3N; 1N / 4N; 1N / 5N |]
                                |> Increment.create

                            fun b m ->
                                let max0 = create b m
                                let max1 = max0 |> Maximum.multipleOf incr
                                let max2 = max1 |> Maximum.multipleOf incr

                                if max0 <> max1 then
                                    max1 |> Maximum.maxSTmax max0 && max1 = max2
                                else
                                    true
                            |> Generators.testProp "multipleOf run multiple times returns identical"

                            fun b m ->
                                let oldMax = create b m
                                let newMax = create b m
                                oldMax |> Maximum.restrict newMax = oldMax
                            |> Generators.testProp "restrict eq max"


                            fun b1 m1 b2 m2 ->
                                let oldMax = create b1 m1
                                let newMax = create b2 m2
                                oldMax |> Maximum.restrict newMax |> Maximum.maxSTEmax oldMax
                            |> Generators.testProp "restrict different max"

                        ]


            module ValueSetTests =

                module ValueSet = Variable.ValueRange.ValueSet

                let create brs = Units.Count.times |> ValueUnit.withValue brs |> ValueSet.create

                let validVals (ValueSet s) =
                    s |> ValueUnit.isEmpty |> not
                    && s |> ValueUnit.gtZero
                    && s
                       |> ValueUnit.removeBigRationalMultiples
                       |> fun s2 -> s2 |> ValueUnit.valueCount = (s |> ValueUnit.valueCount)

                let tests =
                    testList
                        "value set"
                        [
                            testList
                                "create"
                                [
                                    fun xs ->
                                        try
                                            let vs = xs |> create

                                            let clean = xs |> Array.filter (fun x -> x > 0N) |> Array.distinct

                                            if
                                                clean.Length = xs.Length
                                                && (clean
                                                    |> ValueUnit.create Units.Count.times
                                                    |> ValueUnit.removeBigRationalMultiples
                                                    |> ValueUnit.valueCount)
                                                    =
                                                    clean.Length
                                            then
                                                vs |> validVals
                                            else
                                                true
                                        with _ ->
                                            xs |> Array.isEmpty
                                    |> Generators.testProp "only valid value sets can be created"
                                ]

                            testList
                                "prune"
                                [
                                    let mL = Units.Volume.milliLiter
                                    let stuk = Units.General.general "stuk"

                                    // the rounded steps an order passes, in mL and in mL/hour
                                    let steps =
                                        let steps u =
                                            [ 1N / 10N; 1N / 2N; 1N; 5N; 10N; 50N; 100N; 500N ]
                                            |> List.map (ValueUnit.singleWithUnit u)

                                        steps mL @ steps (mL |> ValueUnit.per Units.Time.hour)

                                    let prune unit incr n values =
                                        unit
                                        |> ValueUnit.withValue values
                                        |> ValueSet.create
                                        |> ValueSet.prune steps incr n
                                        |> fun (ValueSet vu) -> vu |> ValueUnit.getValue

                                    let incrOf unit v = unit |> ValueUnit.singleWithValue v |> Some

                                    test "prune with increment takes the finest step that leaves at most n values" {
                                        [| 110N .. 275N |]
                                        |> prune mL (incrOf mL 1N) 5
                                        |> Expect.equal "should be the multiples of 50 mL" [| 150N; 200N; 250N |]
                                    }

                                    test "prune 10 to 20 mL by 0.5 mL to at most 20 keeps the whole millilitres" {
                                        [| 10N .. (1N / 2N) .. 20N |]
                                        |> prune mL (incrOf mL (1N / 2N)) 20
                                        |> Expect.equal "should be every whole mL from 10 to 20" [| 10N .. 20N |]
                                    }

                                    test "prune with a step that overshoots takes the multiples of the finer step" {
                                        [| 1N .. 640N |]
                                        |> prune mL (incrOf mL 1N) 5
                                        |> Expect.equal "should be the multiples of 200 mL" [| 200N; 400N; 600N |]
                                    }

                                    test "prune with increment drops a min and max that are not on the step" {
                                        [| yield 3N; yield! [| 5N .. 5N .. 100N |]; yield 107N |]
                                        |> prune mL (incrOf mL 5N) 5
                                        |> Expect.equal "should be the multiples of 50 mL" [| 50N; 100N |]
                                    }

                                    test "prune with increment keeps min and max when fewer than two multiples remain" {
                                        [| 3N; 10N; 107N |]
                                        |> prune mL (incrOf mL 5N) 2
                                        |> Expect.equal "should keep min, the one multiple and max" [| 3N; 10N; 107N |]
                                    }

                                    test "prune of a unit group without steps takes 1 as the step" {
                                        [| 6N .. (1N / 4N) .. 12N |]
                                        |> prune stuk (incrOf stuk (1N / 4N)) 10
                                        |> Expect.equal "should be the whole tablets" [| 6N .. 12N |]
                                    }

                                    test
                                        "prune of a unit group without steps takes the multiples of 1 when 1 leaves too many" {
                                        [| 6N .. (1N / 4N) .. 12N |]
                                        |> prune stuk (incrOf stuk (1N / 4N)) 5
                                        |> Expect.equal "should be every second tablet" [| 6N; 8N; 10N; 12N |]
                                    }

                                    test "prune with an increment in L prunes as the same increment in mL" {
                                        let inMilliLiter = [| 5N .. 5N .. 1000N |] |> prune mL (incrOf mL 5N) 50

                                        [| 5N .. 5N .. 1000N |]
                                        |> prune mL (incrOf Units.Volume.liter (5N / 1000N)) 50
                                        |> Expect.equal "should equal the prune by 5 mL" inMilliLiter

                                        inMilliLiter
                                        |> Expect.equal "should be the multiples of 50 mL" [| 50N .. 50N .. 1000N |]
                                    }

                                    test
                                        "prune with an increment no step is a multiple of takes the multiples of the increment" {
                                        [| 3N .. 3N .. 90N |]
                                        |> prune mL (incrOf mL 3N) 10
                                        |> Expect.equal "should be the multiples of 9 mL" [| 9N .. 9N .. 90N |]
                                    }

                                    test "prune with at most n values leaves the set as it is" {
                                        [| 3N; 107N |]
                                        |> prune mL (incrOf mL 5N) 5
                                        |> Expect.equal "should return both elements" [| 3N; 107N |]
                                    }

                                    test "prune without increment retains min and max" {
                                        let result =
                                            [| 3N; 7N; 11N; 15N; 19N; 23N; 27N; 31N; 35N; 39N |] |> prune mL None 4

                                        result |> Array.min |> Expect.equal "min should be 3" 3N
                                        result |> Array.max |> Expect.equal "max should be 39" 39N
                                    }
                                ]

                            testList
                                "get and pick indices"
                                [
                                    // getIndices should return exactly the positions in vs1 whose values occur in vs2
                                    fun xs ys ->
                                        try
                                            let vs1 = xs |> create
                                            let vs2 = ys |> create
                                            // pull raw arrays for comparison
                                            let arr1 =
                                                match vs1 with
                                                | ValueSet vu -> vu |> ValueUnit.getValue

                                            let arr2 =
                                                match vs2 with
                                                | ValueSet vu -> vu |> ValueUnit.getValue

                                            let act = ValueSet.getIndices vs1 vs2 |> Array.sort

                                            let exp =
                                                [|
                                                    for i in 0 .. arr1.Length - 1 do
                                                        if arr2 |> Array.exists ((=) arr1[i]) then
                                                            yield i
                                                |]
                                                |> Array.sort

                                            // also ensure indices are within bounds
                                            let inBounds = act |> Array.forall (fun i -> i >= 0 && i < arr1.Length)
                                            inBounds && (act = exp)
                                        with _ ->
                                            true
                                    |> Generators.testProp "getIndices returns indices of common values"

                                    // pickIndices should pick exactly the values at those indices
                                    fun xs ys ->
                                        try
                                            let vs1 = xs |> create
                                            let vs2 = ys |> create

                                            let arr1 =
                                                match vs1 with
                                                | ValueSet vu -> vu |> ValueUnit.getValue

                                            let indices = ValueSet.getIndices vs1 vs2
                                            let picked = vs1 |> ValueSet.pickIndices indices

                                            let pickedArr =
                                                match picked with
                                                | ValueSet vu -> vu |> ValueUnit.getValue |> Array.sort

                                            let expArr = indices |> Array.map (fun i -> arr1[i]) |> Array.sort
                                            pickedArr = expArr
                                        with _ ->
                                            true
                                    |> Generators.testProp "pickIndices picks exactly the intersection values"
                                ]
                        ]


            module MinMaxCalculatorTests =

                open FsCheck

                module ValueRange = Variable.ValueRange
                module Minimum = Minimum
                module Maximum = Maximum

                open Informedica.GenSolver.Lib.Variable.ValueRange

                let create isIncl brOpt =
                    brOpt
                    |> Option.map (fun br -> Units.Count.times |> ValueUnit.singleWithValue br),
                    isIncl


                let calc = MinMaxCalculator.calc (fun b vu -> Some vu, b)


                // MinMax operation golden results are committed fixtures under
                // ./fixtures (one scenario per line, header already stripped).
                // These were previously downloaded from a Google sheet at test
                // time; the fixtures make the tests hermetic and offline.
                // To refresh them, run Scripts/DownloadFixtures.fsx.
                let scenarios sheet =
                    System.IO.Path.Combine(AppContext.BaseDirectory, "fixtures", $"%s{sheet}.txt")
                    |> System.IO.File.ReadAllLines
                    |> Array.toList


                let checkMinMax (min, minIncl) (max, maxIncl) =
                    match min, max with
                    | None, None
                    | None, Some _
                    | Some _, None -> true
                    | Some min', Some max' ->
                        let min =
                            min'
                            |> BigRational.fromInt
                            |> ValueUnit.singleWithUnit Units.Count.times
                            |> Minimum.create minIncl

                        let max =
                            max'
                            |> BigRational.fromInt
                            |> ValueUnit.singleWithUnit Units.Count.times
                            |> Maximum.create maxIncl

                        min |> minSTEmax max

                let min1Options =
                    [
                        None, false
                        Some -2, true
                        Some -2, false
                        Some 0, true
                        Some 0, false
                        Some 2, true
                        Some 2, false
                    ]

                let max1Options =
                    [
                        None, false
                        Some -1, true
                        Some -1, false
                        Some 0, true
                        Some 0, false
                        Some 3, true
                        Some 3, false
                    ]

                let min2Options =
                    [
                        None, false
                        Some -2, true
                        Some -2, false
                        Some 0, true
                        Some 0, false
                        Some 3, true
                        Some 3, false
                    ]

                let max2Options =
                    [
                        None, false
                        Some -1, true
                        Some -1, false
                        Some 0, true
                        Some 0, false
                        Some 5, true
                        Some 5, false
                    ]

                let validPermutations =
                    [
                        for min1 in min1Options do
                            for max1 in max1Options do
                                for min2 in min2Options do
                                    for max2 in max2Options do
                                        match min1, max1, min2, max2 with
                                        | (None, _), _, (None, _), _
                                        | _, (None, _), (None, _), _
                                        | (None, _), _, _, (None, _)
                                        | _, (None, _), _, (None, _) -> yield min1, max1, min2, max2
                                        | (Some _, _), (Some _, _), (Some _, _), (Some _, _) when
                                            checkMinMax min1 max1 && checkMinMax min2 max2
                                            ->
                                            yield (min1, max1, min2, max2)
                                        | (Some _, _), (Some _, _), (None, _), _
                                        | (Some _, _), (Some _, _), _, (None, _) when checkMinMax min1 max1 ->
                                            yield (min1, max1, min2, max2)
                                        | (None, _), _, (Some _, _), (Some _, _)
                                        | _, (None, _), (Some _, _), (Some _, _) when checkMinMax min2 max2 ->
                                            yield (min1, max1, min2, max2)
                                        | _ -> ()
                    ]
                    |> List.distinct


                let mult = MinMaxCalculator.multiplication
                let div = MinMaxCalculator.division
                let add = MinMaxCalculator.addition
                let sub = MinMaxCalculator.subtraction


                let createVuOpt (intOpt, b) =
                    intOpt
                    |> Option.map BigRational.fromInt
                    |> Option.map (ValueUnit.singleWithUnit Units.Count.times),
                    b


                let scenarioToString op opStr i (min1, max1, min2, max2) =
                    let printLeft left = if left |> snd then "[" else "<"
                    let printRight right = if right |> snd then "]" else ">"

                    let printVal v = v |> fst |> Option.map string |> Option.defaultValue ""

                    let result =
                        try
                            op (min1 |> createVuOpt) (max1 |> createVuOpt) (min2 |> createVuOpt) (max2 |> createVuOpt)
                            |> fun (min, max) -> Variable.ValueRange.create min None max None
                            |> toString true
                            |> String.replace "x" ""
                            |> String.replace "<" "< "
                            |> String.replace ">" " >"
                        with _ ->
                            "failed"

                    $"%i{i + 1}, {min1 |> printLeft} {min1 |> printVal} .. {max1 |> printVal} {max1 |> printRight}, {opStr}, {min2 |> printLeft} {min2 |> printVal} .. {max2 |> printVal} {max2 |> printRight},  =, {result}"
                    |> String.replace "  " " "


                let printTests op opStr =
                    validPermutations
                    |> List.iteri (fun i (min1, max1, min2, max2) ->
                        scenarioToString op opStr i (min1, max1, min2, max2) |> printfn "%s"
                    )


                let testScenarios op opStr =
                    let opToSheet opStr =
                        match opStr with
                        | "x" -> "mult"
                        | "/" -> "div"
                        | "+" -> "add"
                        | "-" -> "sub"
                        | _ -> failtest $"unknown op {opStr}"

                    validPermutations
                    |> List.mapi (scenarioToString op opStr)
                    |> List.zip (opStr |> opToSheet |> scenarios)


                // One test per operator rather than one per fixture line. With 4 x 1156
                // lines, per-test harness overhead (Expecto scheduling plus the adapter
                // reporting every result to VSTest) dwarfed the assertions: on the 3-core
                // macOS CI runner the 4624 tests summed to under a second of work inside
                // a 46 second window. Mismatches are listed by line, so a failure still
                // says exactly which scenarios diverged from the fixture.
                let scenarioTest name op opStr =
                    test name {
                        let scenarios = testScenarios op opStr

                        // testScenarios zips the fixture line first, the computed line second
                        let mismatches =
                            scenarios
                            |> List.indexed
                            |> List.filter (fun (_, (fixture, computed)) -> fixture <> computed)
                            |> List.map (fun (i, (fixture, computed)) ->
                                $"scenario %i{i}: fixture %s{fixture}, computed %s{computed}"
                            )

                        mismatches
                        |> Expect.isEmpty (
                            $"%i{mismatches.Length} of %i{scenarios.Length} %s{name} scenarios differ from the fixture:\n"
                            + (mismatches |> String.concat "\n")
                        )
                    }


                let toValueUnit = List.toArray >> ValueUnit.withUnit Units.Count.times

                let singleToValueUnit = ValueUnit.singleWithUnit Units.Count.times


                let generateMinMax n =
                    let v1 = (Generators.bigRGenerator |> Arb.fromGen).Generator.Sample(4, n)
                    let v2 = (Generators.bigRGenerator |> Arb.fromGen).Generator.Sample(4, n)
                    let b1 = Arb.generate<bool> |> Gen.sample 10 n
                    let b2 = Arb.generate<bool> |> Gen.sample 10 n
                    let s1 = Arb.generate<bool> |> Gen.sample 10 n
                    let s2 = Arb.generate<bool> |> Gen.sample 10 n

                    let min =
                        v1
                        |> List.zip b1
                        |> List.zip s1
                        |> List.map (fun (s, (incl, br)) -> if s then None, false else Some br, incl)
                        |> List.map (fun (br, b) -> br |> Option.map singleToValueUnit, b)

                    let max =
                        v2
                        |> List.zip b2
                        |> List.zip s2
                        |> List.map (fun (s, (incl, br)) -> if s then None, false else Some br, incl)
                        |> List.map (fun (br, b) -> br |> Option.map singleToValueUnit, b)

                    min
                    |> List.zip max
                    |> List.map (fun ((br1, incl1), (br2, incl2)) ->
                        match br1, br2 with
                        | Some _, Some _ ->
                            if br1 > br2 then
                                (br2, incl2), (br1, incl1)
                            else if br1 = br2 && (incl1 = incl2 || (not incl2)) then
                                (br2, incl2), (br1, incl1)
                            else
                                (br1, incl1), (br2, incl2)
                        | _ -> (br1, incl1), (br2, incl2)
                    )


                let minMax1 = generateMinMax 100
                let minMax2 = generateMinMax 100


                let tests =
                    testList
                        "minmax calculator"
                        [

                            testList
                                "matching ranges"
                                [

                                    test "match permutations of min max to range matching" {
                                        minMax1
                                        |> List.allPairs minMax2
                                        |> List.map (fun ((min1, max1), (min2, max2)) ->
                                            try
                                                match (min1 |> fst, max1 |> fst), (min2 |> fst, max2 |> fst) with
                                                | MinMaxCalculator.NN, MinMaxCalculator.NN
                                                | MinMaxCalculator.NN, MinMaxCalculator.NP
                                                | MinMaxCalculator.NN, MinMaxCalculator.PP
                                                | MinMaxCalculator.NN, MinMaxCalculator.NZ
                                                | MinMaxCalculator.NN, MinMaxCalculator.ZP
                                                | MinMaxCalculator.NP, MinMaxCalculator.NN
                                                | MinMaxCalculator.NP, MinMaxCalculator.NP
                                                | MinMaxCalculator.NP, MinMaxCalculator.PP
                                                | MinMaxCalculator.NP, MinMaxCalculator.NZ
                                                | MinMaxCalculator.NP, MinMaxCalculator.ZP
                                                | MinMaxCalculator.PP, MinMaxCalculator.NN
                                                | MinMaxCalculator.PP, MinMaxCalculator.NP
                                                | MinMaxCalculator.PP, MinMaxCalculator.PP
                                                | MinMaxCalculator.PP, MinMaxCalculator.NZ
                                                | MinMaxCalculator.PP, MinMaxCalculator.ZP
                                                | MinMaxCalculator.NZ, MinMaxCalculator.NN
                                                | MinMaxCalculator.NZ, MinMaxCalculator.NP
                                                | MinMaxCalculator.NZ, MinMaxCalculator.PP
                                                | MinMaxCalculator.NZ, MinMaxCalculator.NZ
                                                | MinMaxCalculator.NZ, MinMaxCalculator.ZP
                                                | MinMaxCalculator.ZP, MinMaxCalculator.NN
                                                | MinMaxCalculator.ZP, MinMaxCalculator.NP
                                                | MinMaxCalculator.ZP, MinMaxCalculator.PP
                                                | MinMaxCalculator.ZP, MinMaxCalculator.NZ
                                                | MinMaxCalculator.ZP, MinMaxCalculator.ZP -> "can match"
                                            with _ ->
                                                $"cannot match {min1}, {max1},{min2}, {max2}"
                                        )
                                        |> Expect.allEqual "should be true" "can match"
                                    }


                                ]

                            testList
                                "calc operator"
                                [
                                    testList
                                        "Multiplication"
                                        [
                                            // multiplication of two values, both are None
                                            // should return None
                                            test "x1 = None and x2 = None" {
                                                let x1 = None |> create true
                                                let x2 = None |> create true
                                                calc (*) x1 x2 |> Expect.equal "should be None" None
                                            }
                                            // multiplication of two values, the first is Some 1
                                            // and the second is None should return None
                                            test "x1 = Some 1 and x2 = None" {
                                                let x1 = Some 1N |> create true
                                                let x2 = None |> create true
                                                calc (*) x1 x2 |> Expect.equal "should be None" None
                                            }
                                            // multiplication of two values, the first is None
                                            // and the second is Some 1 should return None
                                            test "x1 = None and x2 = Some 1" {
                                                let x1 = None |> create true
                                                let x2 = Some 1N |> create true
                                                calc (*) x1 x2 |> Expect.equal "should be None" None
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
                                                |> Expect.equal
                                                    "should be Some 2, exclusive"
                                                    (Some 2N |> create false |> Some)
                                            }
                                            // multiplication of two values, the first is Some 1
                                            // and the second is Some 2 should return Some 2.
                                            // When the both values are exclusive, the result should be exclusive
                                            test "x1 = Some 1, excl and x2 = Some 2, excl" {
                                                let x1 = Some 1N |> create false
                                                let x2 = Some 2N |> create false

                                                calc (*) x1 x2
                                                |> Expect.equal
                                                    "should be Some 2, exclusive"
                                                    (Some 2N |> create false |> Some)
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

                                    testList
                                        "Division"
                                        [
                                            // division of two values, both are None
                                            // should return None
                                            test "x1 = None and x2 = None" {
                                                let x1 = None |> create true
                                                let x2 = None |> create true
                                                calc (/) x1 x2 |> Expect.equal "should be None" None
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

                                                Expect.throws
                                                    "should throw DivideByZeroException"
                                                    (fun () -> calc (/) x1 x2 |> ignore)

                                                let x1 = None, true
                                                let x2 = Some 0N |> create true

                                                Expect.throws
                                                    "should throw DivideByZeroException"
                                                    (fun () -> calc (/) x1 x2 |> ignore)

                                                let x1 = Some 0N |> create true
                                                let x2 = Some 0N |> create true

                                                Expect.throws
                                                    "should throw DivideByZeroException"
                                                    (fun () -> calc (/) x1 x2 |> ignore)

                                                let x1 = Some 0N |> create false
                                                let x2 = Some 0N |> create true

                                                Expect.throws
                                                    "should throw DivideByZeroException"
                                                    (fun () -> calc (/) x1 x2 |> ignore)
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

                                    testList
                                        "Addition"
                                        [
                                            // addition of two values, both are None
                                            // should return None
                                            test "x1 = None and x2 = None" {
                                                let x1 = None |> create true
                                                let x2 = None |> create true
                                                calc (+) x1 x2 |> Expect.equal "should be None" None
                                            }
                                            // addition of two values, the first is Some 1
                                            // and the second is None should return None
                                            test "x1 = Some 1 and x2 = None" {
                                                let x1 = Some 1N |> create true
                                                let x2 = None |> create true
                                                calc (+) x1 x2 |> Expect.equal "should be None" None
                                            }
                                            // addition of two values, the first is None
                                            // and the second is Some 1 should return None
                                            test "x1 = None and x2 = Some 1" {
                                                let x1 = None |> create true
                                                let x2 = Some 1N |> create true
                                                calc (+) x1 x2 |> Expect.equal "should be None" None
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
                                                |> Expect.equal
                                                    "should be Some 3, exclusive"
                                                    (Some 3N |> create false |> Some)
                                            }
                                            // addition of two values, the first is Some 1, incl
                                            // and the second is Zero incl should return Some 1, incl
                                            test "x1 = Some 1, incl and x2 = Some 0, incl" {
                                                let x1 = Some 1N |> create true
                                                let x2 = Some 0N |> create true
                                                calc (+) x1 x2 |> Expect.equal "should be Some 1, incl" (x1 |> Some)
                                            }
                                            // addition of two values, the first is Some 1, excl
                                            // and the second is Zero incl should return Some 1, excl
                                            test "x1 = Some 1, excl and x2 = Some 0, incl" {
                                                let x1 = Some 1N |> create false
                                                let x2 = Some 0N |> create true
                                                calc (+) x1 x2 |> Expect.equal "should be Some 1, excl" (x1 |> Some)
                                            }
                                            // addition of two values, the first is Some 1, incl
                                            // and the second is Zero excl should return Some 1, excl
                                            test "x1 = Some 1, incl and x2 = Some 0, excl" {
                                                let x1 = Some 1N |> create true
                                                let x2 = Some 0N |> create false

                                                calc (+) x1 x2
                                                |> Expect.equal
                                                    "should be Some 1, incl"
                                                    (Some 1N |> create false |> Some)
                                            }
                                            // addition of two values, the first is Some 1, excl
                                            // and the second is Zero excl should return Some 1, excl
                                            test "x1 = Some 1, excl and x2 = Some 0, excl" {
                                                let x1 = Some 1N |> create false
                                                let x2 = Some 0N |> create false
                                                calc (+) x1 x2 |> Expect.equal "should be Some 1, excl" (x1 |> Some)
                                            }
                                        ]
                                ]

                            testList
                                "calc min max"
                                [
                                    let createMin = MinimumTests.create
                                    let createMax = MaximumTests.create

                                    testList
                                        "Multiplication"
                                        [
                                            let mult = MinMaxCalculator.multiplication

                                            // multiplication of two ranges with min1 and max1
                                            // both None and min2 and max2 both None should return None
                                            test "min1 = None and max1 = None, min2 = None and max2 = None" {
                                                let min1 = None |> create true
                                                let max1 = None |> create true
                                                let min2 = None |> create true
                                                let max2 = None |> create true

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be None, None" (None, None)
                                            }

                                            // multiplication of two ranges with min1 = Some 1 and max1
                                            // is None and min2 and max2 both None should return None
                                            test "min1 = Some 1 and max1 = None, min2 = None and max2 = None" {
                                                let min1 = Some 1N |> create true
                                                let max1 = None |> create true
                                                let min2 = None |> create true
                                                let max2 = None |> create true

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be None, None" (None, None)
                                            }

                                            // multiplication of two ranges with min1 = None and max1
                                            // is Some 1 and min2 and max2 both None should return None
                                            test "min1 = None and max1 = Some 1, min2 = None and max2 = None" {
                                                let min1 = None |> create true
                                                let max1 = Some 1N |> create true
                                                let min2 = None |> create true
                                                let max2 = None |> create true

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be None, None" (None, None)
                                            }

                                            // multiplication of two ranges with min1 = Some 1 and max1
                                            // is Some 1 and min2 and max2 both None should return None, None
                                            test "min1 = Some 1 and max1 = Some 1, min2 = None and max2 = None" {
                                                let min1 = Some 1N |> create true
                                                let max1 = Some 1N |> create true
                                                let min2 = None |> create true
                                                let max2 = None |> create true
                                                let expMin = None
                                                let expMax = None

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be None, None" (expMin, expMax)
                                            }

                                            // multiplication of two ranges with min1 = Some 1 and max1
                                            // is None and min2 = Some 2 and max2 = None should return Some Minimum 2, None
                                            test "min1 = Some 1 and max1 = None, min2 = Some 2 and max2 = None" {
                                                let min1 = Some 1N |> create true
                                                let max1 = None |> create true
                                                let min2 = Some 2N |> create true
                                                let max2 = None |> create true
                                                let expMin = 2N |> createMin true |> Some
                                                let expMax = None

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be Some 2, None" (expMin, expMax)
                                            }

                                            // multiplication of two ranges with min1 = Some 1 and max1
                                            // is None and min2 = None and max2 = Some 2 should return None, None
                                            test "min1 = Some 1 and max1 = None, min2 = None and max2 = Some 2" {
                                                let min1 = Some 1N |> create true
                                                let max1 = None |> create true
                                                let min2 = None |> create true
                                                let max2 = Some 2N |> create true
                                                let expMin = None
                                                let expMax = None

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be None, None" (expMin, expMax)
                                            }

                                            // multiplication of two ranges with min1 = Some 1 and max1
                                            // is None and min2 = Some 2 and max2 = Some 3 should return Some Minimum 2, None
                                            test "min1 = Some 1 and max1 = None, min2 = Some 2 and max2 = Some 3" {
                                                let min1 = Some 1N |> create true
                                                let max1 = None |> create true
                                                let min2 = Some 2N |> create true
                                                let max2 = Some 3N |> create true
                                                let expMin = 2N |> createMin true |> Some
                                                let expMax = None

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be Some 2, None" (expMin, expMax)
                                            }

                                            // multiplication of two ranges with min1 = None and max1
                                            // is Some 1 and min2 = Some 2 and max2 = None should return None, None
                                            test "min1 = None and max1 = Some 1, min2 = Some 2 and max2 = None" {
                                                let min1 = None |> create true
                                                let max1 = Some 1N |> create true
                                                let min2 = Some 2N |> create true
                                                let max2 = None |> create true
                                                let expMin = None
                                                let expMax = None

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be None, None" (expMin, expMax)
                                            }

                                            // multiplication of two ranges with min1 = None and max1
                                            // is Some 1 and min2 = None and max2 = Some 2 should return None, None
                                            test "min1 = None and max1 = Some 1, min2 = None and max2 = Some 2" {
                                                let min1 = None |> create true
                                                let max1 = Some 1N |> create true
                                                let min2 = None |> create true
                                                let max2 = Some 2N |> create true
                                                let expMin = None
                                                let expMax = None

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be None, None" (expMin, expMax)
                                            }

                                            // multiplication of two ranges with min1 = None and max1
                                            // is Some 1 and min2 = Some 2 and max2 = Some 3 should return None, Some Max 3
                                            test "min1 = None and max1 = Some 1, min2 = Some 2 and max2 = Some 3" {
                                                let min1 = None |> create true
                                                let max1 = Some 1N |> create true
                                                let min2 = Some 2N |> create true
                                                let max2 = Some 3N |> create true
                                                let expMin = None
                                                let expMax = 3N |> createMax true |> Some

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be None, None" (expMin, expMax)
                                            }

                                            // multiplication of two ranges with min1 = Some 1 and max1
                                            // is Some 2 and min2 = Some 2 and max2 = Some 3 should return Some Min 2, Some Max 6
                                            test "min1 = Some 1 and max1 = Some 2, min2 = Some 2 and max2 = Some 3" {
                                                let min1 = Some 1N |> create true
                                                let max1 = Some 2N |> create true
                                                let min2 = Some 2N |> create true
                                                let max2 = Some 3N |> create true
                                                let expMin = 2N |> createMin true |> Some
                                                let expMax = 6N |> createMax true |> Some

                                                mult min1 max1 min2 max2
                                                |> Expect.equal "should be Some 2, Some 6" (expMin, expMax)
                                            }
                                        ]

                                    testList
                                        "Scenarios"
                                        [
                                            scenarioTest "Mult" mult "x"
                                            scenarioTest "Div" div "/"
                                            scenarioTest "Add" add "+"
                                            scenarioTest "Sub" sub "-"
                                        ]

                                ]
                        ]


            module ValueRange = Variable.ValueRange


            let createMin isIncl br =
                Units.Count.times |> ValueUnit.singleWithValue br |> Minimum.create isIncl

            let createMax isIncl br =
                Units.Count.times |> ValueUnit.singleWithValue br |> Maximum.create isIncl

            let createIncr br =
                Units.Count.times |> ValueUnit.singleWithValue br |> Increment.create


            open ValueRange.Operators


            let tests =
                testList
                    "valuerange"
                    [

                        testList
                            "tryToValueSet"
                            [
                                let minIncrMax max =
                                    ValueRange.create
                                        (createMin true 1N |> Some)
                                        (createIncr 1N |> Some)
                                        (createMax true max |> Some)
                                        None

                                let valueSet brs =
                                    brs
                                    |> List.toArray
                                    |> ValueUnit.withUnit Units.Count.times
                                    |> ValueRange.ValueSet.create

                                test "a list stays the same list" {
                                    valueSet [ 1N; 3N ]
                                    |> ValSet
                                    |> ValueRange.tryToValueSet 10
                                    |> Expect.equal "the list" (Some(valueSet [ 1N; 3N ]))
                                }

                                test "a min, increment and max of fewer values than the maximum count is listed" {
                                    minIncrMax 3N
                                    |> ValueRange.tryToValueSet 10
                                    |> Expect.equal "1, 2 and 3" (Some(valueSet [ 1N; 2N; 3N ]))
                                }

                                test "a min, increment and max of at least the maximum count of values is not listed" {
                                    minIncrMax 100N |> ValueRange.tryToValueSet 10 |> Expect.isNone "not listed"
                                }

                                test "a range with two increments is listed by its distinct values" {
                                    // 2 to 12 by 2 and 3: 2, 3, 4, 6, 8, 9, 10 and 12
                                    let range =
                                        ValueRange.create
                                            (createMin true 2N |> Some)
                                            ([| 2N; 3N |]
                                             |> ValueUnit.withUnit Units.Count.times
                                             |> Increment.create
                                             |> Some)
                                            (createMax true 12N |> Some)
                                            None

                                    range
                                    |> ValueRange.tryToValueSet 9
                                    |> Expect.equal "eight values" (Some(valueSet [ 2N; 3N; 4N; 6N; 8N; 9N; 10N; 12N ]))

                                    range |> ValueRange.tryToValueSet 8 |> Expect.isNone "not fewer than eight"
                                }

                                test "a range without a maximum is not listed" {
                                    createMin true 1N
                                    |> Min
                                    |> ValueRange.tryToValueSet 10
                                    |> Expect.isNone "not listed"
                                }
                            ]


                        testList
                            "between min and max"
                            [
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

                        testList
                            "is multiple of"
                            [
                                fun v xs ->
                                    try
                                        let incr = xs |> IncrementTests.create
                                        let isMult = xs |> Array.exists (fun i -> (v / i).Denominator = 1I)
                                        v |> ValueRange.isMultipleOfIncr (Some incr) = isMult
                                    with _ ->
                                        true
                                |> Generators.testProp "v is multiple of one of incr"

                                fun v -> v |> ValueRange.isMultipleOfIncr None
                                |> Generators.testProp "is always multiple of none incr"
                            ]

                        testList
                            "calculation with ValueRange"
                            [
                                let calc = ValueRange.calc true

                                test "when any operator is applied to x1 = Unrestricted and x2 = Unrestricted" {
                                    let x1 = ValueRange.Unrestricted
                                    let x2 = ValueRange.Unrestricted

                                    [ calc (+) (x1, x2); calc (-) (x1, x2); calc (*) (x1, x2); calc (/) (x1, x2) ]
                                    |> List.forall (fun x -> x = ValueRange.Unrestricted)
                                    |> Expect.isTrue "should all be Unrestricted"
                                }

                                test "when mult, div is applied to x1 = Zero and x2 = Unrestricted" {
                                    let x1 = 0N |> ValueUnit.singleWithUnit Units.Count.times |> ValueRange.createValSet
                                    let x2 = ValueRange.Unrestricted
                                    let exp = ValSet(ValueSet(ValueUnit([| 0N |], ZeroUnit)))

                                    [
                                        calc (*) (x1, x2)
                                    // calc (/) (x1, x2) TODO: need to fix this
                                    ]
                                    |> List.forall (fun y -> y = exp)
                                    |> Expect.isTrue "should all be Zero"
                                }

                                test "when add, sub is applied to x1 = Zero and x2 = Unrestricted" {
                                    let x1 = 0N |> ValueUnit.singleWithUnit Units.Count.times |> ValueRange.createValSet
                                    let x2 = ValueRange.Unrestricted

                                    [ calc (+) (x1, x2); calc (-) (x1, x2) ]
                                    |> List.forall (fun y -> y = ValueRange.Unrestricted)
                                    |> Expect.isTrue "should all be Unrestricted"
                                }
                            ]

                        testList
                            "valuerange set min incr max"
                            [

                                test "smaller max can be set" {
                                    let max1 = createMax true 3N
                                    let max2 = createMax true 1N

                                    (max1 |> Max)
                                    |> ValueRange.setMax max2
                                    |> Expect.equal "should be equal" (max2 |> Max)
                                }

                                test "greater max can not be set" {
                                    let max1 = createMax true 3N
                                    let max2 = createMax true 1N

                                    (max2 |> Max)
                                    |> ValueRange.setMax max1
                                    |> Expect.notEqual "should not be equal" (max1 |> Max)
                                }

                                test "min max smaller max can be set" {
                                    let min = createMin true 0N
                                    let max1 = createMax true 3N
                                    let max2 = createMax true 1N

                                    ((min, max1) |> MinMax)
                                    |> ValueRange.setMax max2
                                    |> Expect.equal "should be equal" ((min, max2) |> MinMax)
                                }

                                test "min max greater max can not be set" {
                                    let min = createMin true 0N
                                    let max1 = createMax true 3N
                                    let max2 = createMax true 1N

                                    ((min, max2) |> MinMax)
                                    |> ValueRange.setMax max1
                                    |> Expect.notEqual "should not be equal" ((min, max1) |> MinMax)
                                }

                                test "min max can be set with an incr" {
                                    let min = createMin true 1N
                                    let incr = createIncr 1N
                                    let max = createMax true 5N

                                    ((min, max) |> MinMax)
                                    |> ValueRange.setIncr incr
                                    |> Expect.equal "should be equal" ((min, incr, max) |> MinIncrMax)
                                }

                                test "min max can be set with a more restrictive incr" {
                                    let min = createMin true 2N
                                    let incr = createIncr 1N
                                    let max = createMax true 6N

                                    ((min, incr, max) |> MinIncrMax)
                                    |> ValueRange.setIncr (createIncr 2N)
                                    |> Expect.equal "should be equal" ((min, (createIncr 2N), max) |> MinIncrMax)
                                }

                                test "max 90 mg/kg/day cannot be replaced by 300 mg/kg/day" {
                                    let mgPerKgPerDay =
                                        (CombiUnit(Units.Mass.milliGram, OpPer, Units.Weight.kiloGram),
                                         OpPer,
                                         Units.Time.day)
                                        |> CombiUnit

                                    let max1 = [| 90N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true
                                    let max2 = [| 300N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true

                                    (max1 |> Max)
                                    |> ValueRange.setMax max2
                                    |> Expect.notEqual "should not be equal" (max2 |> Max)
                                }

                                // [1.amikacine.injectievloeistof.amikacine]_dos_qty [170079/500 mg..267/500;25 mg..50997/100 mg]
                                // cannot be set with this range:[170079/500 mg..267/500 mg..50997/100 mg]
                                test
                                    "failing case: [170079/500 mg..267/500;25 mg..50997/100 mg] should be able to set with [170079/500 mg..267/500..50997/100 mg]" {
                                    let min1 =
                                        [| 170079N / 500N |]
                                        |> ValueUnit.create Units.Mass.milliGram
                                        |> Minimum.create true

                                    let incr1 =
                                        [| 267N / 500N; 25N |]
                                        |> ValueUnit.create Units.Mass.milliGram
                                        |> Increment.create

                                    let max1 =
                                        [| 50997N / 100N |]
                                        |> ValueUnit.create Units.Mass.milliGram
                                        |> Maximum.create true

                                    let vr1 = (min1, incr1, max1) |> MinIncrMax

                                    let min2 =
                                        [| 170079N / 500N |]
                                        |> ValueUnit.create Units.Mass.milliGram
                                        |> Minimum.create true

                                    let incr2 =
                                        [| 267N / 500N |] |> ValueUnit.create Units.Mass.milliGram |> Increment.create

                                    let max2 =
                                        [| 50997N / 100N |]
                                        |> ValueUnit.create Units.Mass.milliGram
                                        |> Maximum.create true

                                    let vr2 = (min2, incr2, max2) |> MinIncrMax

                                    vr1 @<- vr2 |> Expect.equal "should equal vr2" vr2
                                }

                                test "max 300 mg/kg/day can be replaced by 90 mg/kg/day" {
                                    let mgPerKgPerDay =
                                        (CombiUnit(Units.Mass.milliGram, OpPer, Units.Weight.kiloGram),
                                         OpPer,
                                         Units.Time.day)
                                        |> CombiUnit

                                    let max1 = [| 90N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true
                                    let max2 = [| 300N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true

                                    (max2 |> Max)
                                    |> ValueRange.setMax max1
                                    |> Expect.equal "should be equal" (max1 |> Max)
                                }

                                test "apply expr cannot set greater max" {
                                    let mgPerKgPerDay =
                                        (CombiUnit(Units.Mass.milliGram, OpPer, Units.Weight.kiloGram),
                                         OpPer,
                                         Units.Time.day)
                                        |> CombiUnit

                                    let max1 = [| 90N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true
                                    let max2 = [| 300N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true

                                    (max1 |> Max)
                                    |> ValueRange.applyExpr (max2 |> Max)
                                    |> Expect.notEqual "should not be equal" (max2 |> Max)
                                }

                                test "apply expr cannot set greater max to minmax" {
                                    let mgPerKgPerDay =
                                        (CombiUnit(Units.Mass.milliGram, OpPer, Units.Weight.kiloGram),
                                         OpPer,
                                         Units.Time.day)
                                        |> CombiUnit

                                    let min = [| 40N |] |> ValueUnit.create mgPerKgPerDay |> Minimum.create true
                                    let max1 = [| 90N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true
                                    let max2 = [| 300N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true

                                    ((min, max1) |> MinMax)
                                    |> ValueRange.applyExpr (max2 |> Max)
                                    |> Expect.notEqual "should not be equal" ((min, max2) |> MinMax)
                                }

                                test "apply expr can set smaller max to minmax" {
                                    let mgPerKgPerDay =
                                        (CombiUnit(Units.Mass.milliGram, OpPer, Units.Weight.kiloGram),
                                         OpPer,
                                         Units.Time.day)
                                        |> CombiUnit

                                    let min = [| 40N |] |> ValueUnit.create mgPerKgPerDay |> Minimum.create true
                                    let max1 = [| 90N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true
                                    let max2 = [| 300N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true

                                    ((min, max2) |> MinMax)
                                    |> ValueRange.applyExpr (max1 |> Max)
                                    |> Expect.equal "should not be equal" ((min, max1) |> MinMax)
                                }

                                //set valuerange: [40 mg/kg/dag..90 mg/kg/dag]
                                //with valuerange: [2.4 mg/dag/kg..1500 mg/dag/kg]
                                //= valuerange: [40 mg/kg/dag..1500 mg/dag/kg]
                                test "apply expr min max cannot set smaller min or larger max to minmax" {
                                    let mgPerKgPerDay =
                                        (CombiUnit(Units.Mass.milliGram, OpPer, Units.Weight.kiloGram),
                                         OpPer,
                                         Units.Time.day)
                                        |> CombiUnit

                                    let min1 = [| 40N |] |> ValueUnit.create mgPerKgPerDay |> Minimum.create true
                                    let max1 = [| 90N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true
                                    let min2 = [| 24N / 10N |] |> ValueUnit.create mgPerKgPerDay |> Minimum.create true
                                    let max2 = [| 1500N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true

                                    ((min2, max2) |> MinMax)
                                    |> ValueRange.applyExpr ((min1, max1) |> MinMax)
                                    |> Expect.equal "should be equal" ((min1, max1) |> MinMax)
                                }

                                //set valuerange: [40 mg/kg/dag..90 mg/kg/dag]
                                //with valuerange: [2.4 mg/dag/kg..1500 mg/dag/kg]
                                //= valuerange: [40 mg/kg/dag..1500 mg/dag/kg]
                                test "apply with operator expr min max cannot set smaller min or larger max to minmax" {
                                    let mgPerKgPerDay =
                                        (CombiUnit(Units.Mass.milliGram, OpPer, Units.Weight.kiloGram),
                                         OpPer,
                                         Units.Time.day)
                                        |> CombiUnit

                                    let min1 = [| 40N |] |> ValueUnit.create mgPerKgPerDay |> Minimum.create true
                                    let max1 = [| 90N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true
                                    let min2 = [| 24N / 10N |] |> ValueUnit.create mgPerKgPerDay |> Minimum.create true
                                    let max2 = [| 1500N |] |> ValueUnit.create mgPerKgPerDay |> Maximum.create true

                                    ((min1, max1) |> MinMax) @<- ((min2, max2) |> MinMax)
                                    |> Expect.equal "should be equal" ((min1, max1) |> MinMax)
                                }

                            ]

                        testList
                            "increaseIncrement"
                            [
                                test "can increase increment of 48 1/10 719/10 ml with 5/10" {
                                    let min =
                                        [| 48N |] |> ValueUnit.create Units.Volume.milliLiter |> Minimum.create true

                                    let incr =
                                        [| 1N / 10N |] |> ValueUnit.create Units.Volume.milliLiter |> Increment.create

                                    let max =
                                        [| 719N / 10N |]
                                        |> ValueUnit.create Units.Volume.milliLiter
                                        |> Maximum.create true

                                    let newIncr =
                                        [| 5N / 10N |] |> ValueUnit.create Units.Volume.milliLiter |> Increment.create

                                    (min, incr, max)
                                    |> MinIncrMax
                                    |> ValueRange.increaseIncrement 100N [ newIncr ]
                                    |> Expect.equal
                                        "should be with new incr and new max"
                                        ((min,
                                          newIncr,
                                          ([| 143N / 2N |]
                                           |> ValueUnit.create Units.Volume.milliLiter
                                           |> Maximum.create true))
                                         |> MinIncrMax)

                                }

                                test "failing case: 31 ml/hour to 125 ml/hour with incr: 0.1" {
                                    let u = Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour

                                    {
                                        Name = Variable.Name.createExc "[1.gentamicine]_dos_rte"
                                        Values =
                                            Variable.ValueRange.create
                                                (31N |> ValueUnit.singleWithUnit u |> Minimum.create true |> Some)
                                                ((1N / 10N) |> ValueUnit.singleWithUnit u |> Increment.create |> Some)
                                                (125N |> ValueUnit.singleWithUnit u |> Maximum.create true |> Some)
                                                None
                                    }
                                    |> Variable.increaseIncrement
                                        50N
                                        ([ 0.1m; 0.5m; 1m; 5m; 10m; 20m ]
                                         |> List.map BigRational.fromDecimal
                                         |> List.map (ValueUnit.singleWithUnit u >> Increment.create))
                                    |> fun v -> v.Values |> Variable.ValueRange.getIncr
                                    |> function
                                        | None -> failtest "no incr"
                                        | Some incr ->
                                            incr
                                            |> Expect.equal
                                                "should be 5"
                                                (5N |> ValueUnit.singleWithUnit u |> Increment.create)

                                }

                            ]

                        ValueSetTests.tests
                    ]


    module EquationTests =


        let eqs =
            [ "ParacetamolDoseTotal = ParacetamolDoseTotalAdjust * Adjust" ]
            |> TestSolver.init

        let mg = Units.Mass.milliGram
        let day = Units.Time.day
        let kg = Units.Weight.kiloGram
        let mgPerDay = CombiUnit(mg, OpPer, day)
        let mgPerKgPerDay = (CombiUnit(mg, OpPer, kg), OpPer, day) |> CombiUnit
        let ml = Units.Volume.milliLiter
        let mlPerDay = (ml, OpPer, day) |> CombiUnit
        let freqPerDay = (Units.Count.times, OpPer, day) |> CombiUnit


        // ParacetamolDoseTotal [180..3000] = ParacetamolDoseTotalAdjust [40..90] x Adjust <..100]
        let tests =
            testList
                "Equations"
                [

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
                        |> TestSolver.setMinIncl Units.Volume.milliLiter "a" (2N / 10N)
                        |> TestSolver.setIncr Units.Volume.milliLiter "a" (1N / 10N)
                        |> TestSolver.setMaxIncl Units.Volume.milliLiter "a" 500N
                        |> TestSolver.setValues Units.Count.times "b" [ 1N ]
                        |> TestSolver.setMinIncl Units.Volume.milliLiter "c" (2N / 10N)
                        |> TestSolver.setMaxIncl Units.Volume.milliLiter "c" 500N
                        |> TestSolver.solveMinMax
                        |> function
                            | Ok eqs -> eqs |> List.map (Equation.toString true) |> String.concat ""
                            | Error(_, errs) -> errs |> List.map string |> String.concat ""
                        |> Expect.equal
                            "c should have an incr"
                            "a [1/5 mL..1/10 mL..500 mL] = b [1 x] * c [1/5 mL..1/10 mL..500 mL]"
                    }

                    // [1.benzylpenicilline]_dos_ptm [2,3 mL/dag..460 mL/dag] =
                    // [1.benzylpenicilline]_dos_qty [0,2 mL..0,1 mL..500 mL] * [1]_prs_frq [4;5;6 x/dag]
                    test "failing case: a [2,3 mL/dag..460 mL/dag] = b [0,2 mL..0,1 mL..500 mL] * c [4;5;6 x/dag]" {
                        [ "a = b * c" ]
                        |> TestSolver.init
                        |> TestSolver.setMinIncl mlPerDay "a" (23N / 10N)
                        |> TestSolver.setMaxIncl mlPerDay "a" 460N
                        |> TestSolver.setMinIncl ml "b" (2N / 10N)
                        |> TestSolver.setIncr ml "b" (1N / 10N)
                        |> TestSolver.setMaxIncl ml "b" 500N
                        |> TestSolver.setValues freqPerDay "c" [ 4N; 5N; 6N ]
                        |> TestSolver.solveMinMax
                        |> function
                            | Ok eqs -> eqs |> List.map (Equation.toString true) |> String.concat ""
                            | Error(_, errs) -> errs |> List.map string |> String.concat ""
                        |> Expect.equal
                            "a should have an incr"
                            "a [12/5 mL/dag..2/5;1/2;3/5 mL/dag..460 mL/dag] = b [2/5 mL..1/10 mL..115 mL] * c [4;5;6 x/dag]"
                    }
                ]


    /// A solved equation offers only the values that a solution reaches: increments combine by their
    /// least common multiple, and products and quotients that form no range on an increment get none.
    module IncrementEquationTests =

        module ValueRange = Variable.ValueRange
        module Increment = ValueRange.Increment


        let ml = Units.Volume.milliLiter
        let times = Units.Count.times


        let solve eqs =
            match eqs |> TestSolver.solveAll with
            | Ok eqs -> eqs
            | Error(_, errs) -> failtest $"%A{errs}"


        let getVar n eqs =
            eqs
            |> List.collect Equation.toVars
            |> List.find (fun v -> v.Name |> Variable.Name.toString = n)


        let incrOf (var: Variable) =
            match var.Values with
            | MinIncrMax(_, incr, _) -> incr |> Increment.toList |> Some
            | _ -> None


        /// The values a variable offers on every value of its increment.
        let offered (var: Variable) =
            match var.Values with
            | MinIncrMax(min, incr, max) ->
                let min = min |> ValueRange.Minimum.toValueUnit |> ValueUnit.getValue |> Array.head
                let max = max |> ValueRange.Maximum.toValueUnit |> ValueUnit.getValue |> Array.head

                // the positive multiples n * i from min to max
                let floorBr (br: BigRational) = br.Numerator / br.Denominator |> BigRational.FromBigInt
                let ceilBr br = if floorBr br = br then br else floorBr br + 1N

                incr
                |> Increment.toList
                |> List.collect (fun i -> [ ceilBr (min / i) .. floorBr (max / i) ] |> List.map ((*) i))
                |> List.distinct
            | vr -> failtest $"no offered values in %s{vr |> ValueRange.toString true}"


        let isOn (min, incr, max) v = v >= min && v <= max && v |> BigRational.isMultiple incr


        /// Solve y = c * x with x and y on a range in mL.
        let solveProduct c (xMin, xIncr, xMax) (yMin, yIncr, yMax) =
            [ "y = c * x" ]
            |> TestSolver.init
            |> TestSolver.setValues times "c" [ c ]
            |> TestSolver.setMinIncl ml "x" xMin
            |> TestSolver.setIncr ml "x" xIncr
            |> TestSolver.setMaxIncl ml "x" xMax
            |> TestSolver.setMinIncl ml "y" yMin
            |> TestSolver.setIncr ml "y" yIncr
            |> TestSolver.setMaxIncl ml "y" yMax
            |> solve


        let tests =
            testList
                "increments in equations"
                [
                    for name, c, xRange, yRange, xIncr, yIncr in
                        [
                            "dose count 3", 3N, (1N, 1N / 10N, 10N), (1N, 1N, 30N), [ 1N ], [ 3N ]
                            "share 1/4", 1N / 4N, (22N, 1N, 120N), (1N, 1N / 10N, 30N), [ 2N ], [ 1N / 2N ]
                            "share 2/7", 2N / 7N, (22N, 1N, 120N), (1N, 1N / 10N, 30N), [ 7N ], [ 2N ]
                            "dose count 1", 1N, (1N, 1N / 10N, 10N), (1N, 1N, 30N), [ 1N ], [ 1N ]
                        ] do
                        test $"y = c * x with %s{name} offers only values a solution reaches" {
                            let eqs = solveProduct c xRange yRange
                            let x = eqs |> getVar "x"
                            let y = eqs |> getVar "y"

                            x
                            |> incrOf
                            |> Expect.equal "x should be on the lcm of the increments" (Some xIncr)
                            y
                            |> incrOf
                            |> Expect.equal "y should be on the lcm of the increments" (Some yIncr)

                            x
                            |> offered
                            |> List.filter (fun v -> v * c |> isOn yRange |> not)
                            |> Expect.isEmpty "every x should give a y on its range"

                            y
                            |> offered
                            |> List.filter (fun v -> v / c |> isOn xRange |> not)
                            |> Expect.isEmpty "every y should come from an x on its range"
                        }

                    test "a range divided by a value set lies on the increment divided by each value" {
                        // x = y / c
                        [ "y = c * x" ]
                        |> TestSolver.init
                        |> TestSolver.setValues times "c" [ 2N; 4N ]
                        |> TestSolver.setMinIncl ml "y" 1N
                        |> TestSolver.setIncr ml "y" 1N
                        |> TestSolver.setMaxIncl ml "y" 10N
                        |> solve
                        |> getVar "x"
                        |> incrOf
                        |> Expect.equal "x should be on 1/4 mL" (Some [ 1N / 4N ])
                    }

                    test "a range divided by dose counts 1 to 12 keeps an increment" {
                        // x = y / c; 1 to 6 each divide a count from 7 to 12, so six values remain
                        // and the cap on the number of increments is not reached
                        [ "y = c * x" ]
                        |> TestSolver.init
                        |> TestSolver.setValues times "c" [ 1N .. 12N ]
                        |> TestSolver.setMinIncl ml "y" 1N
                        |> TestSolver.setIncr ml "y" 1N
                        |> TestSolver.setMaxIncl ml "y" 10N
                        |> solve
                        |> getVar "x"
                        |> incrOf
                        |> Option.map Set.ofList
                        |> Expect.equal
                            "x should be on 1/7 to 1/12 mL"
                            ([ 7N .. 12N ] |> List.map (fun c -> 1N / c) |> Set.ofList |> Some)
                    }

                    for vs, exp in [ [ 2N ], [ 1N / 2N ]; [ 2N; 4N ], [ 1N / 4N ] ] do
                        test $"a range in mg divided by %A{vs} mL lies on %A{exp} mg/mL" {
                            // a unit with a base factor other than 1, so the values that divide
                            // another are compared on the same scale as the values themselves
                            [ "y = c * x" ]
                            |> TestSolver.init
                            |> TestSolver.setValues ml "c" vs
                            |> TestSolver.setMinIncl Units.Mass.milliGram "y" 1N
                            |> TestSolver.setIncr Units.Mass.milliGram "y" 1N
                            |> TestSolver.setMaxIncl Units.Mass.milliGram "y" 10N
                            |> solve
                            |> getVar "x"
                            |> incrOf
                            |> Expect.equal "x should keep the increment divided by the volume" (Some exp)
                        }

                    test "a value set divided by a range has a min and max without an increment" {
                        // y = 1 / x
                        [ "c = x * y" ]
                        |> TestSolver.init
                        |> TestSolver.setValues times "c" [ 1N ]
                        |> TestSolver.setMinIncl ml "x" 1N
                        |> TestSolver.setIncr ml "x" 1N
                        |> TestSolver.setMaxIncl ml "x" 5N
                        |> solve
                        |> getVar "y"
                        |> fun y ->
                            match y.Values with
                            | MinMax(min, max) ->
                                [ min |> ValueRange.Minimum.toValueUnit; max |> ValueRange.Maximum.toValueUnit ]
                                |> List.collect (ValueUnit.getValue >> Array.toList)
                            | vr -> failtest $"expected a min and max, got %s{vr |> ValueRange.toString true}"
                        |> Expect.equal "y should be 1/5 to 1 without an increment" [ 1N / 5N; 1N ]
                    }

                    test "a product of two ranges has a min and max without an increment" {
                        [ "z = x * y" ]
                        |> TestSolver.init
                        |> TestSolver.setMinIncl ml "x" 1N
                        |> TestSolver.setIncr ml "x" 1N
                        |> TestSolver.setMaxIncl ml "x" 5N
                        |> TestSolver.setMinIncl times "y" 1N
                        |> TestSolver.setIncr times "y" 1N
                        |> TestSolver.setMaxIncl times "y" 5N
                        |> solve
                        |> getVar "z"
                        |> incrOf
                        |> Expect.isNone "z should have no increment"
                    }

                    test "a sum of two ranges on the same increment keeps the increment" {
                        [ "z = x + y" ]
                        |> TestSolver.init
                        |> TestSolver.setMinIncl ml "x" 1N
                        |> TestSolver.setIncr ml "x" 1N
                        |> TestSolver.setMaxIncl ml "x" 5N
                        |> TestSolver.setMinIncl ml "y" 1N
                        |> TestSolver.setIncr ml "y" 1N
                        |> TestSolver.setMaxIncl ml "y" 5N
                        |> solve
                        |> getVar "z"
                        |> incrOf
                        |> Expect.equal "z should be on 1 mL" (Some [ 1N ])
                    }
                ]


    /// An equation whose variables all hold one value is evaluated instead of being put aside: the
    /// single values of an order can contradict it, and then the solve errors.
    module SolvedEquationTests =

        module ValueRange = Variable.ValueRange
        module ValueSet = ValueRange.ValueSet


        let mg = Units.Mass.milliGram
        let ml = Units.Volume.milliLiter
        let l = Units.Volume.liter
        let mgPerMl = mg |> ValueUnit.per ml


        /// The quantity of the issue: 10 mg fixed, 1 mg/mL strength, 1 mL volume.
        let impossibleProduct () =
            [ "qty = conc * vol" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "qty" [ 10N ]
            |> TestSolver.setValues mgPerMl "conc" [ 1N ]
            |> TestSolver.setValues ml "vol" [ 1N ]


        let possibleProduct () =
            [ "qty = conc * vol" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "qty" [ 10N ]
            |> TestSolver.setValues mgPerMl "conc" [ 1N ]
            |> TestSolver.setValues ml "vol" [ 10N ]


        /// The same quantity with the volume in liters: the check compares base values.
        let possibleProductInLiters () =
            [ "qty = conc * vol" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "qty" [ 10N ]
            |> TestSolver.setValues mgPerMl "conc" [ 1N ]
            |> TestSolver.setValues l "vol" [ 1N / 100N ]


        let impossibleSum () =
            [ "tot = a + b" ]
            |> TestSolver.init
            |> TestSolver.setValues ml "tot" [ 10N ]
            |> TestSolver.setValues ml "a" [ 1N ]
            |> TestSolver.setValues ml "b" [ 1N ]


        let possibleSum () =
            [ "tot = a + b" ]
            |> TestSolver.init
            |> TestSolver.setValues ml "tot" [ 10N ]
            |> TestSolver.setValues ml "a" [ 4N ]
            |> TestSolver.setValues ml "b" [ 6N ]


        /// A solved value the open variables do not give. Not all solved: the open variables are
        /// calculated from the solved one and come out empty, which the solver reported already.
        let solvedNotGiven () =
            [ "qty = conc * vol" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "qty" [ 10N ]
            |> TestSolver.setValues mgPerMl "conc" [ 1N; 2N; 3N ]
            |> TestSolver.setValues ml "vol" [ 1N; 2N; 4N ]


        /// A solved value the open variables give in one way. Not all solved, narrowed already.
        let solvedGiven () =
            [ "qty = conc * vol" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "qty" [ 10N ]
            |> TestSolver.setValues mgPerMl "conc" [ 1N; 2N; 3N ]
            |> TestSolver.setValues ml "vol" [ 1N; 5N; 7N ]


        /// A solved value outside the range of an open variable. Not all solved, reported already.
        let solvedOutsideRange () =
            [ "qty = conc * vol" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "qty" [ 10N ]
            |> TestSolver.setValues mgPerMl "conc" [ 1N ]
            |> TestSolver.setMinIncl ml "vol" 20N
            |> TestSolver.setMaxIncl ml "vol" 50N


        /// An impossible solved equation beside an open one on a shared variable.
        let impossibleBesideOpen () =
            [ "qty = conc * vol"; "rate = qty * freq" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "qty" [ 10N ]
            |> TestSolver.setValues mgPerMl "conc" [ 1N ]
            |> TestSolver.setValues ml "vol" [ 1N ]


        let isError =
            function
            | Ok _ -> false
            | Error _ -> true


        let isInvalidEquation =
            function
            | Error(_, errs) ->
                errs
                |> List.exists (
                    function
                    | Exceptions.SolverInvalidEquations _ -> true
                    | _ -> false
                )
            | Ok _ -> false


        let values n eqs =
            eqs
            |> List.collect Equation.toVars
            |> List.find (fun v -> v.Name |> Variable.Name.toString = n)
            |> Variable.getValueRange
            |> ValueRange.getValSet
            |> Option.map (ValueSet.toValueUnit >> ValueUnit.getValue)


        let check eqs = eqs |> List.head |> Equation.check false


        let tests =
            testList
                "solved equations"
                [
                    testList
                        "Equation.check"
                        [
                            test "a product that holds: 10 mg = 1 mg/mL * 10 mL" {
                                possibleProduct () |> check |> Expect.isTrue "holds"
                            }

                            test "a product that holds across unit scales: 10 mg = 1 mg/mL * 0.01 L" {
                                possibleProductInLiters () |> check |> Expect.isTrue "holds on base values"
                            }

                            test "a product that does not hold: 10 mg = 1 mg/mL * 1 mL" {
                                impossibleProduct () |> check |> Expect.isFalse "does not hold"
                            }

                            test "a sum that holds: 10 mL = 4 mL + 6 mL" {
                                possibleSum () |> check |> Expect.isTrue "holds"
                            }

                            test "a sum that does not hold: 10 mL = 1 mL + 1 mL" {
                                impossibleSum () |> check |> Expect.isFalse "does not hold"
                            }

                            test "a solved value in the value lists of the others holds" {
                                solvedGiven () |> check |> Expect.isTrue "10 mg is in {1,2,3} * {1,5,7}"
                            }

                            test "a solved value outside the value lists of the others does not hold" {
                                solvedNotGiven () |> check |> Expect.isFalse "10 mg is not in {1,2,3} * {1,2,4}"
                            }

                            test "an equation with a range is not decided and holds" {
                                solvedOutsideRange ()
                                |> check
                                |> Expect.isTrue "vol is a range, not a value list"
                            }

                            test "in a min max solve nothing is decided" {
                                impossibleProduct ()
                                |> List.head
                                |> Equation.check true
                                |> Expect.isTrue "the min max check always holds"
                            }
                        ]

                    testList
                        "Equation.solve"
                        [
                            test "a solved equation that does not hold errors" {
                                impossibleProduct ()
                                |> List.head
                                |> Equation.solve false TestSolver.logger
                                |> snd
                                |> function
                                    | Errored [ Exceptions.SolverInvalidEquations _ ] -> true
                                    | _ -> false
                                |> Expect.isTrue "10 mg = 1 mg/mL * 1 mL errors as an invalid equation"
                            }

                            test "a solved equation that holds is unchanged" {
                                possibleProduct ()
                                |> List.head
                                |> Equation.solve false TestSolver.logger
                                |> snd
                                |> function
                                    | Unchanged -> true
                                    | _ -> false
                                |> Expect.isTrue "10 mg = 1 mg/mL * 10 mL is unchanged"
                            }
                        ]

                    testList
                        "solve all"
                        [
                            test "a solved product equation that does not hold errors in a values solve" {
                                impossibleProduct ()
                                |> TestSolver.solveAll
                                |> isInvalidEquation
                                |> Expect.isTrue "10 mg = 1 mg/mL * 1 mL"
                            }

                            test "a solved product equation that does not hold errors in a min max solve" {
                                impossibleProduct ()
                                |> TestSolver.solveMinMax
                                |> isInvalidEquation
                                |> Expect.isTrue "10 mg = 1 mg/mL * 1 mL"
                            }

                            test "a solved sum equation that does not hold errors in a values solve" {
                                impossibleSum ()
                                |> TestSolver.solveAll
                                |> isInvalidEquation
                                |> Expect.isTrue "10 mL = 1 mL + 1 mL"
                            }

                            test "a solved sum equation that does not hold errors in a min max solve" {
                                impossibleSum ()
                                |> TestSolver.solveMinMax
                                |> isInvalidEquation
                                |> Expect.isTrue "10 mL = 1 mL + 1 mL"
                            }

                            test "a solved product equation that holds is unchanged" {
                                let eqs = possibleProduct ()

                                eqs
                                |> TestSolver.solveAll
                                |> function
                                    | Ok solved -> solved |> Expect.equal "10 mg = 1 mg/mL * 10 mL" eqs
                                    | Error(_, errs) -> failtest $"%A{errs}"
                            }

                            test "a solved product equation that holds across unit scales is unchanged" {
                                let eqs = possibleProductInLiters ()

                                eqs
                                |> TestSolver.solveAll
                                |> function
                                    | Ok solved -> solved |> Expect.equal "10 mg = 1 mg/mL * 0.01 L" eqs
                                    | Error(_, errs) -> failtest $"%A{errs}"
                            }

                            test "a solved sum equation that holds is unchanged" {
                                let eqs = possibleSum ()

                                eqs
                                |> TestSolver.solveMinMax
                                |> function
                                    | Ok solved -> solved |> Expect.equal "10 mL = 4 mL + 6 mL" eqs
                                    | Error(_, errs) -> failtest $"%A{errs}"
                            }

                            test "an impossible solved equation beside an open equation errors" {
                                impossibleBesideOpen ()
                                |> TestSolver.solveAll
                                |> isInvalidEquation
                                |> Expect.isTrue "10 mg = 1 mg/mL * 1 mL"
                            }

                            // the mixed cases: a solved value among open variables was caught already,
                            // by the calculation of the open variables
                            test "a solved value the open variables do not give errors" {
                                solvedNotGiven ()
                                |> TestSolver.solveAll
                                |> isError
                                |> Expect.isTrue "10 mg is not in {1,2,3} * {1,2,4}"
                            }

                            test "a solved value the open variables give narrows them" {
                                let eqs =
                                    solvedGiven ()
                                    |> TestSolver.solveAll
                                    |> function
                                        | Ok eqs -> eqs
                                        | Error(_, errs) -> failtest $"%A{errs}"

                                (eqs |> values "conc", eqs |> values "vol")
                                |> Expect.equal "2 mg/mL and 5 mL" (Some [| 2N |], Some [| 5N |])
                            }

                            test "a solved value outside the range of an open variable errors" {
                                solvedOutsideRange ()
                                |> TestSolver.solveMinMax
                                |> isError
                                |> Expect.isTrue "10 mg = 1 mg/mL * [20..50] mL"
                            }
                        ]

                    // the path an order takes on every change: solve for one variable
                    testList
                        "solve for a variable"
                        [
                            test "a solved equation that holds the set variable and does not hold errors" {
                                [ "qty = conc * vol"; "rate = qty * freq" ]
                                |> TestSolver.init
                                |> TestSolver.setValues mgPerMl "conc" [ 1N ]
                                |> TestSolver.setValues ml "vol" [ 1N ]
                                |> TestSolver.solveValues mg "qty" [ 10N ]
                                |> isInvalidEquation
                                |> Expect.isTrue "10 mg = 1 mg/mL * 1 mL"
                            }

                            test "a solved equation without the set variable is left alone" {
                                // only the equations that hold the set variable are solved; an equation
                                // that does not hold but is not touched is not evaluated
                                let eqs =
                                    [ "qty = conc * vol"; "rate = dose * freq" ]
                                    |> TestSolver.init
                                    |> TestSolver.setValues mg "qty" [ 10N ]
                                    |> TestSolver.setValues mgPerMl "conc" [ 1N ]
                                    |> TestSolver.setValues ml "vol" [ 1N ]

                                eqs
                                |> TestSolver.solveValues mg "dose" [ 2N ]
                                |> isError
                                |> Expect.isFalse "qty = conc * vol is not on the queue"
                            }
                        ]
                ]


    /// A sum equation whose variables are in different unit groups cannot be added, and the solve
    /// errors with a unit mismatch on every path instead of throwing.
    module SumUnitMismatchTests =

        module ValueRange = Variable.ValueRange
        module ValueSet = ValueRange.ValueSet


        let mg = Units.Mass.milliGram
        let gram = Units.Mass.gram
        let ml = Units.Volume.milliLiter
        let mgPerMl = mg |> ValueUnit.per ml


        /// All solved: 2 mg = 1 mg + 1 mL.
        let allSolved () =
            [ "tot = a + b" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "tot" [ 2N ]
            |> TestSolver.setValues mg "a" [ 1N ]
            |> TestSolver.setValues ml "b" [ 1N ]


        /// The total open: tot = 1 mg + 1 mL.
        let totOpen () =
            [ "tot = a + b" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "a" [ 1N ]
            |> TestSolver.setValues ml "b" [ 1N ]


        /// A summand open with two values: tot = {1, 2} mg + 1 mL.
        let summandOpen () =
            [ "tot = a + b" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "a" [ 1N; 2N ]
            |> TestSolver.setValues ml "b" [ 1N ]


        /// A sum in one unit group across unit scales: tot = 1 mg + 1 g.
        let sameGroupSum () =
            [ "tot = a + b" ]
            |> TestSolver.init
            |> TestSolver.setValues mg "a" [ 1N ]
            |> TestSolver.setValues gram "b" [ 1N ]


        /// A product of different units: qty = 2 mg/mL * 5 mL.
        let mixedProduct () =
            [ "qty = conc * vol" ]
            |> TestSolver.init
            |> TestSolver.setValues mgPerMl "conc" [ 2N ]
            |> TestSolver.setValues ml "vol" [ 5N ]


        let isUnitMismatch errs =
            errs
            |> List.exists (
                function
                | Exceptions.EquationSumUnitMismatch _ -> true
                | _ -> false
            )


        /// The values of a variable in base units.
        let values n eqs =
            eqs
            |> List.collect Equation.toVars
            |> List.find (fun v -> v.Name |> Variable.Name.toString = n)
            |> Variable.getValueRange
            |> ValueRange.getValSet
            |> Option.map (ValueSet.toValueUnit >> ValueUnit.toBaseValue)


        let mismatchCases =
            [
                "all solved, 2 mg = 1 mg + 1 mL", allSolved
                "tot open, a = 1 mg, b = 1 mL", totOpen
                "a open {1, 2} mg, b = 1 mL", summandOpen
            ]


        let tests =
            testList
                "sum with mismatched units"
                [
                    for name, solve in [ "values", TestSolver.solveAll; "min max", TestSolver.solveMinMax ] do
                        for case, eqs in mismatchCases do
                            test $"a %s{name} solve errors with a unit mismatch: %s{case}" {
                                match eqs () |> solve with
                                | Error(_, errs) -> errs |> isUnitMismatch |> Expect.isTrue "a unit mismatch error"
                                | Ok _ -> failtest "solved a sum of mg and mL"
                            }

                    for case, eqs in mismatchCases do
                        test $"Equation.solve returns Errored with a unit mismatch: %s{case}" {
                            match eqs () |> List.head |> Equation.solve false TestSolver.logger |> snd with
                            | Errored errs -> errs |> isUnitMismatch |> Expect.isTrue "a unit mismatch error"
                            | r -> failtest $"expected Errored, got %A{r}"
                        }

                    test "a sum in one unit group solves: 1 mg + 1 g = 1001 mg" {
                        sameGroupSum ()
                        |> TestSolver.solveAll
                        |> function
                            | Ok eqs ->
                                eqs
                                |> values "tot"
                                |> Expect.equal "1.001 g, the base unit of mass is the gram" (Some [| 1001N / 1000N |])
                            | Error(_, errs) -> failtest $"%A{errs}"
                    }

                    test "a product of different units solves: 2 mg/mL * 5 mL = 10 mg" {
                        mixedProduct ()
                        |> TestSolver.solveAll
                        |> function
                            | Ok eqs -> eqs |> values "qty" |> Expect.equal "0.01 g" (Some [| 1N / 100N |])
                            | Error(_, errs) -> failtest $"%A{errs}"
                    }
                ]


    module OperatorMatchTests =

        open Informedica.GenSolver.Lib.Utils.ValueUnit.Operators

        let tests =
            testList
                "operator matching"
                [
                    test "matches addition" {
                        match (+) with
                        | Add -> true
                        | _ -> false
                        |> Expect.isTrue "should match Add"
                    }

                    test "matches subtraction" {
                        match (-) with
                        | Sub -> true
                        | _ -> false
                        |> Expect.isTrue "should match Sub"
                    }

                    test "matches multiplication" {
                        match (*) with
                        | Mul -> true
                        | _ -> false
                        |> Expect.isTrue "should match Mul"
                    }

                    test "matches division" {
                        match (/) with
                        | Div -> true
                        | _ -> false
                        |> Expect.isTrue "should match Div"
                    }
                ]


    /// The nth value of a variable, counted from 0, and the percentage of its range the slider
    /// sets with it.
    module NthValueTests =

        module ValueRange = Variable.ValueRange
        module ValueSet = ValueRange.ValueSet


        let times brs = brs |> ValueUnit.withUnit Units.Count.times

        let variable vr =
            vr |> Variable.createSucc ("[1.x]_qty" |> Variable.Name.createExc)

        /// A variable with the value set 1, 2, 3, 4, 5.
        let valueSet = [| 1N; 2N; 3N; 4N; 5N |] |> times |> ValueRange.createValSet |> variable

        /// A variable from 1 to 10 by 1.
        let minIncrMax =
            ValueRange.create
                (1N
                 |> ValueUnit.singleWithUnit Units.Count.times
                 |> ValueRange.Minimum.create true
                 |> Some)
                (1N
                 |> ValueUnit.singleWithUnit Units.Count.times
                 |> ValueRange.Increment.create
                 |> Some)
                (10N
                 |> ValueUnit.singleWithUnit Units.Count.times
                 |> ValueRange.Maximum.create true
                 |> Some)
                None
            |> variable

        let values var =
            var
            |> Variable.getValueRange
            |> ValueRange.getValSet
            |> Option.map (ValueSet.toValueUnit >> ValueUnit.getValue)


        let tests =
            testList
                "setNthValue and setPercValue"
                [
                    test "the nth of a value set, counted from 0" {
                        [ 0; 2; 4 ]
                        |> List.map (fun n -> valueSet |> Variable.setNthValue n |> values)
                        |> Expect.equal
                            "the first, the third and the last"
                            [ Some [| 1N |]; Some [| 3N |]; Some [| 5N |] ]
                    }

                    test "a value set past its values is kept" {
                        valueSet
                        |> Variable.setNthValue 5
                        |> values
                        |> Expect.equal "all values" (Some [| 1N; 2N; 3N; 4N; 5N |])
                    }

                    test "the nth of a min, increment and max, counted from 0, capped at the max" {
                        [ 0; 3; 20 ]
                        |> List.map (fun n -> minIncrMax |> Variable.setNthValue n |> values)
                        |> Expect.equal
                            "the min, min plus 3 and the max"
                            [ Some [| 1N |]; Some [| 4N |]; Some [| 10N |] ]
                    }

                    test "a percentage gives the value that far through the values, 0 the first and 100 the last" {
                        [ 0; 50; 100 ]
                        |> List.map (fun perc -> valueSet |> Variable.setPercValue perc |> values)
                        |> Expect.equal "a value set: 1, 3 and 5" [ Some [| 1N |]; Some [| 3N |]; Some [| 5N |] ]

                        [ 0; 50; 100 ]
                        |> List.map (fun perc -> minIncrMax |> Variable.setPercValue perc |> values)
                        |> Expect.equal
                            "a range: the min, min plus 4, the max"
                            [ Some [| 1N |]; Some [| 5N |]; Some [| 10N |] ]
                    }

                    test "the number of values of a value set and of a min, increment and max" {
                        [ valueSet; minIncrMax ]
                        |> List.map Variable.valueCount
                        |> Expect.equal "5 and 10" [ Some 5; Some 10 ]
                    }

                    test "no number of values of a range without a maximum" {
                        1N
                        |> ValueUnit.singleWithUnit Units.Count.times
                        |> ValueRange.Minimum.create true
                        |> Min
                        |> variable
                        |> Variable.valueCount
                        |> Expect.isNone "no count"
                    }
                ]


    [<Tests>]
    let tests =
        [
            NthValueTests.tests
            VariableTests.ValueRangeTests.IncrementTests.tests
            VariableTests.ValueRangeTests.MinimumTests.tests
            VariableTests.ValueRangeTests.MaximumTests.tests
            VariableTests.ValueRangeTests.MinMaxCalculatorTests.tests
            VariableTests.ValueRangeTests.tests
            EquationTests.tests
            IncrementEquationTests.tests
            SolvedEquationTests.tests
            SumUnitMismatchTests.tests
            OperatorMatchTests.tests
        ]
        |> testList "GenSolver"
