namespace Informedica.GenUnits.Tests


module Unquote =


    open Swensen.Unquote
    open Informedica.Utils.Lib.BCL
    //open Informedica.GenUnits.Lib
    open Informedica.GenUnits.Lib
    open Informedica.GenUnits.Lib.ValueUnit
    open Informedica.GenUnits.Lib.ValueUnit.Operators
    //open Informedica.GenUnits.Lib.ValueUnit
    //open Informedica.GenUnits.Lib.ValueUnit.Operators
    module Units = Units

    // Test Array.removeBigRationalMultiples
    let testRemoveBigRationalMultiples removeF =
        let test (act: BigRational[]) (exp: BigRational[]) =
            let testName = $"removeBigRationalMultiples [|{act |> Array.toList}|]"
            let act = act |> removeF

            try
                test <@ act = exp @>
            with ex ->
                printfn $"FAILED: {testName}"
                printfn $"  Expected: [|{exp |> Array.toList}|]"
                printfn $"  Actual:   [|{act |> Array.toList}|]"
                reraise ()

        // Basic tests - existing
        test [| 1N; 2N; 3N; 4N; 5N |] [| 1N |]
        test [| 2N; 3N; 4N; 5N |] [| 2N; 3N; 5N |]
        test [| 2N; 3N |] [| 2N; 3N |]
        test [| 2N; 3N; 4N |] [| 2N; 3N |]
        test [||] [||]

        // Edge cases
        test [| 1N |] [| 1N |] // Single element with 1N
        test [| 2N |] [| 2N |] // Single element not 1N
        test [| 4N; 2N |] [| 2N |] // Out of order
        test [| 6N; 4N; 3N; 2N |] [| 2N; 3N |] // Reverse order

        // Prime numbers (should remain unchanged)
        test [| 2N; 3N; 5N; 7N; 11N; 13N |] [| 2N; 3N; 5N; 7N; 11N; 13N |]

        // Powers of 2
        test [| 2N; 4N; 8N; 16N; 32N |] [| 2N |]

        // Powers of 3
        test [| 3N; 9N; 27N |] [| 3N |]

        // Mixed multiples
        test [| 6N; 12N; 18N; 24N |] [| 6N |] // All multiples of 6
        test [| 2N; 3N; 6N; 12N; 18N |] [| 2N; 3N |] // 6, 12, 18 are multiples of 2 and 3

        // Complex case with multiple prime factors
        test [| 2N; 3N; 4N; 6N; 8N; 9N; 12N; 18N |] [| 2N; 3N |]

        // Fractional BigRationals
        test [| 1N / 2N; 1N / 4N; 1N / 8N |] [| 1N / 8N |] // Powers of 1/2
        test [| 1N / 3N; 1N / 6N; 1N / 12N |] [| 1N / 12N |] // 1/6 = (1/3) * (1/2), 1/12 = (1/3) * (1/4)
        test [| 1N / 2N; 1N / 3N; 1N / 6N |] [| 1N / 6N |] // 1/6 is multiple of both 1/2 and 1/3

        // Mixed integers and fractions
        test [| 1N; 1N / 2N; 1N / 3N |] [| 1N / 3N; 1N / 2N |] // 1N is removed (multiple of both 1/2 and 1/3); the fractions are kept
        test [| 2N; 1N; 3N |] [| 1N |] // 1N (mid-array, out of order) is the smallest; 2 and 3 are multiples of it

        // Negative numbers (should be filtered out by the function)
        test [| -2N; -4N; 2N; 4N |] [| 2N |]
        test [| -1N; 1N; 2N |] [| 1N |]

        // Zero (should be filtered out as it's not > 0N)
        test [| 0N; 2N; 4N |] [| 2N |]

        // Duplicates (should be handled by Array.distinct)
        test [| 2N; 2N; 4N; 4N |] [| 2N |]
        test [| 3N; 6N; 3N; 9N; 6N |] [| 3N |]

        // Large numbers
        test [| 100N; 200N; 300N; 400N; 500N |] [| 100N |]
        test [| 7N; 14N; 21N; 28N; 35N |] [| 7N |]

        // Non-obvious multiples
        test [| 15N; 30N; 45N; 3N; 5N |] [| 3N; 5N |] // 15=3*5, 30=3*5*2, 45=3*5*3
        test [| 12N; 18N; 24N; 36N; 2N; 3N |] [| 2N; 3N |] // All others are multiples

        // Special fractional cases
        test [| 3N / 4N; 3N / 8N; 3N / 12N |] [| 3N / 12N; 3N / 8N |] // 3/4 = 3 × (1/4), but 3/8 is not an integer multiple of 1/4
        test [| 5N / 6N; 5N / 12N; 5N / 18N |] [| 5N / 18N; 5N / 12N |] // 5/6 = 3 × (5/18) and 2 × (5/12), but 5/12 ≠ integer × (5/18)

        // Co-prime numbers (no multiples)
        test [| 3N; 5N; 7N; 11N |] [| 3N; 5N; 7N; 11N |]
        test [| 2N; 3N; 5N; 7N; 11N; 13N; 17N; 19N |] [| 2N; 3N; 5N; 7N; 11N; 13N; 17N; 19N |]


    let x = create Units.Count.times
    let mg = create Units.Mass.milliGram
    let mcg = create Units.Mass.microGram
    let ml = create Units.Volume.milliLiter
    let kg = create Units.Mass.kiloGram
    let hr = create Units.Time.hour
    let day = create Units.Time.day
    let day2 = create (Units.Time.nDay 2N)
    let min = create Units.Time.minute
    let week2 = create (Units.Time.nWeek 2N)
    let day14 = create (Units.Time.nDay 14N)
    let dr20 = create Units.Volume.droplet

    let dr40 = create (Units.Volume.dropletWithDropsPerMl 40N)


    let tests () =
        [

            // Test that 10 ml / 5 ml = 2
            test <@ ([| 10N |] |> ml) /? ([| 5N |] |> ml) =? ([| 2N |] |> x) @>

            // Test that 1 kg / 1000 mg = 1000
            test <@ ([| 1N |] |> kg) /? ([| 1000N |] |> mg) =? ([| 1000N |] |> x) @>

            // Test that 10 * 2 mg = 20 mg
            test <@ ([| 10N |] |> x) *? ([| 1N |] |> mg) =? ([| 10N |] |> mg) @>

            // Test that 10 mg * (2 x / day) = 20 mg / day
            test
                <@
                    ([| 10N |] |> mg) *? (([| 2N |] |> x) /? ([| 1N |] |> day))
                    =? (([| 20N |] |> mg) /? ([| 1N |] |> day))
                @>

            // Test that 20 mg * (1 x / 2 day) = 10 mg / day
            test
                <@
                    ([| 20N |] |> mg) *? (([| 1N |] |> x) /? ([| 2N |] |> day))
                    =? (([| 10N |] |> mg) /? ([| 1N |] |> day))
                @>


            // Test that 20 mg * (1 x / 2 day) = 10 mg / day
            test
                <@
                    ([| 20N |] |> mg) *? (([| 1N |] |> x) /? ([| 1N |] |> day2))
                    =? (([| 10N |] |> mg) /? ([| 1N |] |> day))
                @>

            // Test that 2 day / 1 day = 2
            test <@ ([| 1N |] |> day2) /? ([| 1N |] |> day) =? ([| 2N |] |> x) @>


            // Test that 1 x / 2 week divide by 1 x / 14 days yields 1
            test
                <@
                    (([| 1N |] |> x) /? ([| 1N |] |> week2))
                    /? (([| 1N |] |> x) /? ([| 1N |] |> day14))
                    =? ([| 1N |] |> x)
                @>


            test <@ ([| 1000N |] |> mcg) =? ([| 1N |] |> mg) @>

            test <@ ([| 20N |] |> dr20) =? ([| 1N |] |> ml) @>

            test <@ ([| 40N |] |> dr40) =? ([| 1N |] |> ml) @>

        ]


// let mgPerKgPerDay =
//     (CombiUnit (Units.Mass.milliGram, OpPer, Units.Weight.kiloGram), OpPer,
//     Units.Time.day)
//     |> CombiUnit


// ([|90N|] |> create mgPerKgPerDay) >? ([|3000N|] |> create mgPerKgPerDay)
// ([|3000N|] |> create mgPerKgPerDay) >? ([|90N|] |> create mgPerKgPerDay)


module Tests =

    open Expecto
    open Expecto.Flip

    open Informedica.Utils.Lib.BCL
    //open Informedica.GenUnits.Lib

    open Informedica.GenUnits.Lib
    open Informedica.GenUnits.Lib.ValueUnit
    open Informedica.GenUnits.Lib.ValueUnit.Operators

    module Units = Units

    let toString = toStringEngShort

    let inline (>>*) u f =
        u |> printfn "%A"
        f u


    let (>>?) res exp =
        match exp |> fromString with
        // use =? (base-unit equality), NOT structural = which would compare
        // the raw value-array + unit representation instead of base values
        | Ok exp -> res =? exp |> Expect.isTrue ""
        | _ ->
            false |> Expect.isTrue ""
            failtest "can't run the test"

        res


    // Some basic value units
    let mg400 = 400N |> createSingle Units.Mass.milliGram
    let gram2 = 2N |> createSingle Units.Mass.gram
    let ml50 = 50N |> createSingle Units.Volume.milliLiter
    let ml5 = 5N |> createSingle Units.Volume.milliLiter
    let l5 = 5N |> createSingle Units.Volume.liter
    let day2 = 2N |> createSingle Units.Time.day
    let hour3 = 3N |> createSingle Units.Time.hour
    let kg10 = 10N |> createSingle Units.Weight.kiloGram

    // The count group is a special unit group
    // with only one unit: times.
    let times3 = 3N |> createSingle Units.Count.times
    let times100 = 100N |> createSingle Units.Count.times
    let weeks4 = 4N |> Units.Time.nWeek


    let unitTests =

        let toBase = toBaseValue >> (Array.map BigRational.toDecimal) >> Array.head

        testList
            "Unit"
            [
                test "base value of 400 mg " { Expect.equal "should equal 0.4 g" 0.4m (mg400 |> toBase) }

                test "base value of 50 ml = 0.05 l" { Expect.isTrue "should equal 0.05" (ml50 |> toBase = 0.05m) }

                test "base value of 5 ml = 0.005 l" { Expect.isTrue "should equal 0.005" (ml5 |> toBase = 0.005m) }

                test "base value of 5 l = 5 l" { Expect.isTrue "should equal 5" (l5 |> toBase = 5m) }

                test "count 3 times 5 ml results in 15 ml" {
                    Expect.equal "should equal 0.015 L" 0.015m (ml5 *? times3 |> toBase)
                }

                test "base value of 1 day" {
                    let vu = (1N |> singleWithUnit Units.Time.day)
                    Expect.equal "" (60m * 60m * 24m) (vu |> toBase)
                }

                test "3 days" {
                    let vu = (1N |> singleWithUnit (Units.Time.nDay 3N))
                    Expect.equal "" (3m * 60m * 60m * 24m) (vu |> toBase)
                }

                test "there and back again one unit" {
                    let vu =
                        mg400
                        |> get
                        |> fun (_, u) -> mg400 |> toBaseValue |> create u |> toUnitValue |> create u

                    Expect.equal "" vu mg400
                }

                test "there and back again 2 units" {
                    let vu1 = 1N |> singleWithUnit (Units.Mass.milliGram |> per Units.Volume.milliLiter)

                    let vu2 =
                        vu1
                        |> get
                        |> fun (_, u) -> vu1 |> toBaseValue |> create u |> toUnitValue |> create u

                    Expect.equal "" vu1 vu2
                }

                test "there and back again 3 units" {
                    let vu1 =
                        1N
                        |> singleWithUnit (Units.Mass.milliGram |> per Units.Volume.milliLiter |> per Units.Time.day)

                    let vu2 =
                        vu1
                        |> get
                        |> fun (_, u) -> vu1 |> toBaseValue |> create u |> toUnitValue |> create u

                    Expect.equal "" vu1 vu2
                }
            ]


    let stringTests =

        testList
            "Print"
            [
                test "4 weken unit to string should return 4 weken" {
                    weeks4
                    |> Units.toStringDutchShort
                    |> Expect.equal "should be equal" "4 weken[Time]"
                }

                test "1 x[Count]/4 weken[Time] from string should return a combiunit" {
                    "1 x[Count]/4 weken[Time]"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringDutchShort
                    |> Expect.equal "should equal" "1 x[Count]/4 weken[Time]"
                }

                test "unit with unit number can find group" {
                    "4 weken"
                    |> UnitsParse.stringWithGroup
                    |> Expect.equal "should be equal" "4 weken[Time]"
                }

                test "unit with invalid group should throw exception" {
                    fun () ->
                        "day[Invalid]"
                        |> UnitsParse.fromString
                        |> fun u ->
                            printfn $"{u}"
                            u
                        |> ignore
                    |> Expect.throws "should throw an exception"
                }

                test "parse single value and toString roundtrip" {
                    "1.5 mg"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "3/2 mg[Mass]"
                }

                test "parse multiple values with brackets and toString roundtrip" {
                    "[1;3;5] x[Count]"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "1;3;5 x[Count]"
                }

                test "parse multiple values without brackets and toString roundtrip" {
                    "1;3;5 x[Count]"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "1;3;5 x[Count]"
                }

                test "parse multiple values with different units" {
                    "[10;20;30] mg[Mass]"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "10;20;30 mg[Mass]"
                }

                test "parse fractional values without brackets" {
                    "0.5;1;1.5 mg"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "1/2;1;3/2 mg[Mass]"
                }

                test "parse general unit without [General] annotation" {
                    "120 stuk"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "120 stuk[General]"
                }

                test "parse general unit with [General] annotation for backward compatibility" {
                    "120 stuk[General]"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "120 stuk[General]"
                }

                test "parse single value general unit without annotation" {
                    "1 stuk"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "1 stuk[General]"
                }

                test "parse complex unit with general unit (mg/stuk)" {
                    "120;240;500 mg/stuk"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "120;240;500 mg[Mass]/stuk[General]"
                }

                test "parse general unit with multiple values without annotation" {
                    "10;20;30 tablet"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "10;20;30 tablet[General]"
                }

                test "known units still take precedence over general units" {
                    "10 mg"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> get
                    |> snd
                    |> Group.unitToGroup
                    |> Expect.equal "should be mass group" Group.MassGroup
                }

                test "complex known unit still works (mg/ml)" {
                    "10 mg/ml"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "10 mg[Mass]/ml[Volume]"
                }

                test "general unit in complex unit with known units" {
                    "5 ml/ampul"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "5 ml[Volume]/ampul[General]"
                }

                test "general unit round-trip without annotation" {
                    let original = 100N |> createSingle (Units.General.general "capsule")

                    original
                    |> toStringEngShort
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> Expect.equal "should equal original" original
                }

                test "times unit still parses correctly (not confused with general)" {
                    "1;3;5 x"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "1;3;5 x[Count]"
                }

                test "fractional general units" {
                    "0.5;1;1.5 sachet"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> toStringEngShort
                    |> Expect.equal "should equal" "1/2;1;3/2 sachet[General]"
                }

            ]


    let comparisonTests =

        testList
            "Comparison"
            [

                test "ml50 < l5 using normal operator < should be false" { ml50 < l5 |> Expect.isFalse "" }

                test "ml50 < l5 using special operator <?" { ml50 <? l5 |> Expect.isTrue "" }

                test "ml50 = l5 should be false" { ml50 =? l5 |> Expect.isFalse "" }

                test "ml50 * times 100 = l5 should be true" { ml50 *? times100 =? l5 |> Expect.isTrue "" }
            ]


    let calculationTests =


        testList
            "Calculation"
            [

                test "3 times 3 = 9" { times3 *? times3 |> toBaseValue = [| 9N |] |> Expect.isTrue "" }

                test "3 divided by 3 = 1" { times3 /? times3 |> toBaseValue = [| 1N |] |> Expect.isTrue "" }

                test "3 plus 3 = 6" { times3 +? times3 |> toBaseValue = [| 6N |] |> Expect.isTrue "" }

                test "3 minus 3 = 0" { times3 -? times3 |> toBaseValue = [| 0N |] |> Expect.isTrue "" }

                test "can add or subtract within the same unit group" {
                    (l5 +? ml50) >? l5 |> Expect.isTrue ""

                    (l5 -? ml50) <? l5 |> Expect.isTrue ""
                }

                test "cannot add or subtract with different unit groups" {
                    (fun _ -> (l5 +? mg400) >? l5 |> ignore) |> Expect.throws ""

                    (fun _ -> (l5 -? mg400) <? l5 |> ignore) |> Expect.throws ""
                }

                test "division by unit with the same unit group results in a count" {
                    // division by a simple unit
                    let _, u = (l5 /? ml50) |> get
                    let g = u |> Group.unitToGroup

                    g = Group.CountGroup |> Expect.isTrue ""

                    // division by a more complex unit
                    let vu1 = (mg400 /? ml5 /? day2)
                    let vu2 = (mg400 /? l5 /? hour3)

                    vu1 /? vu2
                    |> get
                    |> snd
                    |> Group.unitToGroup
                    |> Expect.equal "" Group.CountGroup
                }

                test "can calculate with units" {
                    (mg400 +? mg400)
                    // 400 mg + 400 mg = 800 mg
                    >>? "800 mg[Mass]"
                    |> (fun vu -> vu /? ml50)
                    // 800 mg / 50 ml = 16 mg/ml
                    >>? "16 mg[Mass]/ml[Volume]"
                    |> (fun vu -> vu *? ml50)
                    // 16 mg/ml * 50 ml = 800 mg
                    >>? "800 mg[Mass]"
                    // 800 mg - 400 mg = 400 mg
                    |> (fun vu -> vu -? mg400)
                    >>? "400 mg[Mass]"
                    |> ignore
                }

                test "division with 3 unit values" {
                    let vu = mg400 /? (mg400 /? ml50) // equals mg400 * (ml50 / mg400) = mg50

                    Expect.equal "should be 50 ml" ml50 vu
                }

                test "more complicated division" {
                    let vu = (mg400 /? ml50) /? (day2 /? ml50) // equals (mg400 / ml50) * (ml50 / day2) = mg400 / day2

                    Expect.equal "" (mg400 /? day2) vu
                }

                test "division resulting in combi with 3 units" {
                    mg400 /? kg10 /? day2
                    |> toStringEngShort
                    |> Expect.equal "should be equal" "20 mg[Mass]/kg[Weight]/day[Time]"

                }

                test "multiplying with a combi with 3 units with the middle unit" {
                    (mg400 /? kg10 /? day2) *? kg10
                    |> toStringEngShort
                    |> Expect.equal "should be equal" "200 mg[Mass]/day[Time]"
                }

            ]


    let conversionTests =

        testList
            "Conversion"
            [

                test "can convert from 5 liter to 5000 ml" {
                    5000N
                    |> createSingle Units.Volume.milliLiter
                    |> Expect.equal "" (l5 ==> Units.Volume.milliLiter)
                }

                test "unit group from 400 mg / day = mass per timegroup" {
                    (mg400 /? (1N |> createSingle Units.Time.day))
                    |> get
                    |> snd
                    |> Group.unitToGroup
                    |> Group.toString
                    |> Expect.equal "" "Mass/Time"
                }

                test "the number of possible units is the permutation of the units in each unitgroup" {
                    // get the number of units in the mass group
                    let mc = Group.MassGroup |> Group.getUnitCombinations |> List.length
                    // get the number of units in the volume group
                    let vc = Group.VolumeGroup |> Group.getUnitCombinations |> List.length

                    (mg400 /? ml50)
                    |> get
                    |> snd
                    |> Group.unitToGroup
                    |> Group.getUnitCombinations
                    |> List.length
                    // the number of units for the Mass/Volume group is
                    // the multiple of mc * vc
                    |> Expect.equal "" (mc * vc)
                }

            ]

    let groupTests =

        testList
            "Group"
            [

                (*
            didn't catch System.Exception: cannot add or subtract different units CombiUnit
              (CombiUnit (Volume (MilliLiter 1N), OpPer, Weight (WeightKiloGram 1N)), OpPer,
               Time (Hour 1N)) CombiUnit
              (CombiUnit (Volume (MilliLiter 1N), OpPer, Time (Hour 1N)), OpPer,
               Weight (WeightKiloGram 1N))
            *)
                test "failing case eqsGroup (ml/kg)/hour = (ml/hour)/kg" {
                    let un1 =
                        CombiUnit(
                            CombiUnit(Volume(MilliLiter 1N), OpPer, Weight(WeightKiloGram 1N)),
                            OpPer,
                            Time(Hour 1N)
                        )

                    let un2 =
                        CombiUnit(
                            CombiUnit(Volume(MilliLiter 1N), OpPer, Time(Hour 1N)),
                            OpPer,
                            Weight(WeightKiloGram 1N)
                        )

                    Group.eqsGroup un1 un2 |> Expect.isTrue "should be true"

                }

                test "same group, different unit is equal" {
                    Group.eqsGroup (Mass(KiloGram 1N)) (Mass(Gram 1N)) |> Expect.isTrue ""
                }

                test "different group is not equal" {
                    Group.eqsGroup (Mass(KiloGram 1N)) (Volume(Liter 1N)) |> Expect.isFalse ""
                }

                test "commutative: (mass*vol)/(wt*time) = (vol*mass)/(time*wt)" {
                    let a =
                        CombiUnit(
                            CombiUnit(Mass(KiloGram 1N), OpTimes, Volume(Liter 1N)),
                            OpPer,
                            CombiUnit(Weight(WeightKiloGram 1N), OpTimes, Time(Hour 1N))
                        )

                    let b =
                        CombiUnit(
                            CombiUnit(Volume(Liter 1N), OpTimes, Mass(KiloGram 1N)),
                            OpPer,
                            CombiUnit(Time(Hour 1N), OpTimes, Weight(WeightKiloGram 1N))
                        )

                    Group.eqsGroup a b |> Expect.isTrue "should be true"
                }

                test "associative: (mass*vol)/(wt*time) = mass*vol/wt/time" {
                    let a =
                        CombiUnit(
                            CombiUnit(Mass(KiloGram 1N), OpTimes, Volume(Liter 1N)),
                            OpPer,
                            CombiUnit(Weight(WeightKiloGram 1N), OpTimes, Time(Hour 1N))
                        )

                    let c =
                        CombiUnit(
                            CombiUnit(
                                CombiUnit(Mass(KiloGram 1N), OpTimes, Volume(Liter 1N)),
                                OpPer,
                                Weight(WeightKiloGram 1N)
                            ),
                            OpPer,
                            Time(Hour 1N)
                        )

                    Group.eqsGroup a c |> Expect.isTrue "should be true"
                }

                test "divide by quotient: vol/(wt/time) = (vol*time)/wt" {
                    let e =
                        CombiUnit(Volume(Liter 1N), OpPer, CombiUnit(Weight(WeightKiloGram 1N), OpPer, Time(Hour 1N)))

                    let f =
                        CombiUnit(CombiUnit(Volume(Liter 1N), OpTimes, Time(Hour 1N)), OpPer, Weight(WeightKiloGram 1N))

                    Group.eqsGroup e f |> Expect.isTrue "should be true"
                }

                // pins the documented behaviour: eqsGroup does NOT reduce to lowest terms
                test "does not cancel common factors: (mass*vol)/vol <> mass" {
                    let d =
                        CombiUnit(CombiUnit(Mass(KiloGram 1N), OpTimes, Volume(Liter 1N)), OpPer, Volume(Liter 1N))

                    Group.eqsGroup d (Mass(KiloGram 1N)) |> Expect.isFalse ""
                }

                test "does not treat Count as identity: mass*count <> mass" {
                    let g = CombiUnit(Mass(KiloGram 1N), OpTimes, Count(Times 1N))

                    Group.eqsGroup g (Mass(KiloGram 1N)) |> Expect.isFalse ""
                }

            ]


    let useCaseTests =
        testList
            "use case tests"
            [
                test "100 mg/mL droplet fluid with 40 droplets per mL then 1 droplet = 2.5 mg" {
                    let fluid =
                        100N |> createSingle (Units.Mass.milliGram |> per Units.Volume.milliLiter)

                    let dr = 1N |> createSingle (Units.Volume.dropletWithDropsPerMl 40N)
                    let exp = 25N / 10N |> createSingle Units.Mass.milliGram

                    (fluid *? dr) =? exp
                    |> Expect.isTrue $"should be 2.5 mg/ml, actual: {(fluid *? dr)}"

                }

            ]


    // Regression tests pinning the fixes / decisions from the code review.
    let regressionTests =
        testList
            "Review regressions"
            [
                test "opFromString maps each operator token correctly" {
                    Core.opFromString "/" |> Expect.equal "/" OpPer
                    Core.opFromString "*" |> Expect.equal "*" OpTimes
                    Core.opFromString "+" |> Expect.equal "+" OpPlus
                    Core.opFromString "-" |> Expect.equal "-" OpMinus
                }

                test "opFromString throws on an unknown token" {
                    (fun () -> Core.opFromString "?" |> ignore)
                    |> Expect.throws "should throw on unknown operator"
                }

                test "1 hour base value is 3600 seconds" {
                    let vu = 1N |> singleWithUnit Units.Time.hour
                    vu |> toBaseValue |> Expect.equal "1 hr = 3600 s" [| 3600N |]
                }

                test "60 minutes equals 1 hour" {
                    let min60 = 60N |> singleWithUnit Units.Time.minute
                    let hr1 = 1N |> singleWithUnit Units.Time.hour
                    min60 =? hr1 |> Expect.isTrue "60 min should equal 1 hr"
                }

                test "eqsUnit on General is value-sensitive (same name + value equal)" {
                    Units.eqsUnit (General("x", 1N)) (General("x", 1N))
                    |> Expect.isTrue "same name and value should be equal"
                }

                test "eqsUnit on General differs when only the value differs" {
                    Units.eqsUnit (General("x", 1N)) (General("x", 2N))
                    |> Expect.isFalse "different value should not be equal"
                }

                test "eqsUnit on General differs when the name differs" {
                    Units.eqsUnit (General("x", 1N)) (General("y", 1N))
                    |> Expect.isFalse "different name should not be equal"
                }

                test "kg parses into the Weight group, not Mass (intentional filter)" {
                    "10 kg"
                    |> fromString
                    |> function
                        | Ok vu -> vu
                        | Error err -> $"can't run this test: {err}" |> failtest
                    |> getUnit
                    |> Group.unitToGroup
                    |> Expect.equal "kg should resolve to Weight" Group.WeightGroup
                }

                test "medianValue picks the upper-middle element for an even set" {
                    [| 1N; 2N; 3N; 4N |]
                    |> create Units.Mass.gram
                    |> medianValue
                    |> Option.map getValue
                    |> Expect.equal "median of [1;2;3;4] g is 3" (Some [| 3N |])
                }

                test "medianValue picks the middle element for an odd set" {
                    [| 1N; 2N; 3N |]
                    |> create Units.Mass.gram
                    |> medianValue
                    |> Option.map getValue
                    |> Expect.equal "median of [1;2;3] g is 2" (Some [| 2N |])
                }
            ]


    // toToken builds a canonical identity for a ValueUnit: base-normalised,
    // sorted values paired with the unit's GROUP. Equal quantities (even in
    // different units of the same group, or with reordered values) collapse to
    // one token; genuinely distinct quantities stay distinct.
    let toTokenTests =

        let mg v = singleWithUnit Units.Mass.milliGram v
        let g v = singleWithUnit Units.Mass.gram v

        let mL v =
            singleWithUnit Units.Volume.milliLiter v

        let mgWith vs = vs |> withUnit Units.Mass.milliGram

        let mgPerML v =
            singleWithUnit (Units.Mass.milliGram |> per Units.Volume.milliLiter) v

        let gen name v =
            singleWithUnit (Units.General.general name) v

        testList
            "toToken"
            [
                test "is deterministic: the same ValueUnit yields the same token" {
                    let vu = mg 250N

                    vu |> toToken |> Expect.equal "two calls on the same vu match" (vu |> toToken)
                }

                test "two structurally equal ValueUnits yield the same token" {
                    let exp = mg 250N |> toToken

                    mg 250N
                    |> toToken
                    |> Expect.equal $"should equal token of an equal vu {exp}" exp
                }

                test "different values in the same unit yield different tokens" {
                    let other = mg 500N |> toToken

                    mg 250N
                    |> toToken
                    |> Expect.notEqual $"250 mg token must differ from 500 mg token {other}" other
                }

                test "different groups with the same value yield different tokens" {
                    let asMass = mg 5N |> toToken

                    mL 5N
                    |> toToken
                    |> Expect.notEqual $"5 mL token must differ from 5 mg token {asMass}" asMass
                }

                test "equal ValueUnits in different units yield the SAME token" {
                    mg 1000N =? g 1N
                    |> Expect.isTrue "precondition: 1000 mg and 1 g are equal quantities"

                    let asGram = g 1N |> toToken

                    mg 1000N
                    |> toToken
                    |> Expect.equal $"1000 mg must share the token of 1 g {asGram}" asGram
                }

                test "multi-value arrays are encoded into the token" {
                    let single = mgWith [| 1N |] |> toToken

                    mgWith [| 1N; 2N; 3N |]
                    |> toToken
                    |> Expect.notEqual $"[1;2;3] mg must differ from [1] mg {single}" single
                }

                test "value order does not affect the token (values are canonicalised by sort)" {
                    let ascending = mgWith [| 1N; 2N; 3N |] |> toToken

                    mgWith [| 3N; 2N; 1N |]
                    |> toToken
                    |> Expect.equal $"a reordered array must yield the same token {ascending}" ascending
                }

                test "distinct General units yield distinct tokens (group string keeps the specifier)" {
                    let capsule = gen "capsule" 1N |> toToken

                    gen "tablet" 1N
                    |> toToken
                    |> Expect.notEqual $"1 tablet must differ from 1 capsule {capsule}" capsule
                }

                test "same General unit yields the same token" {
                    let exp = gen "tablet" 1N |> toToken

                    gen "tablet" 1N
                    |> toToken
                    |> Expect.equal $"1 tablet must match 1 tablet {exp}" exp
                }

                test "a set of distinct quantities produces a set of distinct tokens" {
                    let vus =
                        [
                            mg 1N
                            mg 2N
                            mg 250N
                            mg 500N
                            g 1N
                            mL 1N
                            mL 5N
                            mgPerML 10N
                            mgWith [| 1N; 2N |]
                            mgWith [| 1N; 3N |]
                        ]

                    vus
                    |> List.map toToken
                    |> List.distinct
                    |> List.length
                    |> Expect.equal "every distinct quantity must map to a distinct token" (vus |> List.length)
                }
            ]


    [<Tests>]
    let tests =

        let removeBigRationalMultiples2 xs =
            if xs |> Array.isEmpty then
                xs
            else
                let xs = xs |> Array.filter (fun x -> x > 0N) |> Array.sort |> Array.distinct

                xs
                |> Array.fold
                    (fun acc x1 ->
                        acc
                        |> Array.filter (fun x2 -> x1 = x2 || x2 |> BigRational.isMultiple x1 |> not)
                    )
                    xs

        testList
            "ValueUnit Tests"
            [
                stringTests
                calculationTests
                conversionTests
                comparisonTests
                unitTests
                groupTests
                useCaseTests
                regressionTests
                toTokenTests

                test "Unquote tests" {
                    try
                        Unquote.tests () |> ignore
                        true
                    with _ ->
                        false
                    |> Expect.isTrue "should all be true"
                }

                test "Remove bigrational multiples reference tests" {
                    try
                        Unquote.testRemoveBigRationalMultiples removeBigRationalMultiples2
                        true
                    with _ ->
                        false
                    |> Expect.isTrue "should all be true"
                }

                test "Remove bigrational multiples implementation tests" {
                    try
                        Unquote.testRemoveBigRationalMultiples Array.removeBigRationalMultiples
                        true
                    with _ ->
                        false
                    |> Expect.isTrue "should all be true"
                }

            ]
