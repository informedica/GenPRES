namespace Informedica.GenUnits.Tests



module Unquote =


    open Swensen.Unquote
    open MathNet.Numerics
    open Informedica.GenUnits.Lib
    open Informedica.GenUnits.Lib.ValueUnit
    open Informedica.GenUnits.Lib.ValueUnit.Operators


    // Test Array.removeBigRationalMultiples
    let testRemoveBigRationalMultiples () =
        let test (act : BigRational[]) (exp : BigRational[]) =
            let act = act |> Array.removeBigRationalMultiples
            test <@ act = exp @>

        test [| 1N; 2N; 3N; 4N; 5N |] [| 1N |]
        test [| 2N; 3N; 4N; 5N |] [|2N; 3N; 5N|]
        test [| 2N; 3N |] [| 2N; 3N |]
        test [| 2N; 3N; 4N |] [| 2N; 3N |]
        test [| |] [|  |]


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


    let tests () =
        [

            // Test that 10 ml / 5 ml = 2
            test <@
                ([|10N|] |> ml) / ([|5N|] |> ml) =? ([|2N|] |> x)
            @>

            // Test that 1 kg / 1000 mg = 1000
            test <@
                ([|1N|] |> kg) / ([|1000N|] |> mg) =? ([|1000N|] |> x)
            @>

            // Test that 10 * 2 mg = 20 mg
            test <@
                ([|10N|] |> x) * ([|1N|] |> mg) =? ([|10N|] |> mg)
            @>

            // Test that 10 mg * (2 x / day) = 20 mg / day
            test <@
                ([|10N|] |> mg) * (([|2N|] |> x) / ([|1N|] |> day)) =? (([|20N|] |> mg) / ([|1N|] |> day))
            @>

            // Test that 20 mg * (1 x / 2 day) = 10 mg / day
            test <@
                ([|20N|] |> mg) * (([|1N|] |> x) / ([|2N|] |> day)) =? (([|10N|] |> mg) / ([|1N|] |> day))
            @>


            // Test that 20 mg * (1 x / 2 day) = 10 mg / day
            test <@
                ([|20N|] |> mg) * (([|1N|] |> x) / ([|1N|] |> day2)) =? (([|10N|] |> mg) / ([|1N|] |> day))
            @>

            // Test that 2 day / 1 day = 2
            test <@
                ([|1N|] |> day2) / ([|1N|] |> day) =? ([|2N|] |> x)
            @>



            // Test that 1 x / 2 week divide by 1 x / 14 days yields 1
            test <@
                (([|1N|] |> x ) / ([|1N|] |> week2)) / (([|1N|] |> x) / ([|1N|] |> day14)) = ([|1N|] |> x)
            @>


            test <@
                ([|1000N|] |> mcg) =? ([|1N|] |> mg)
            @>
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

    open FParsec
    open MathNet.Numerics
    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib

    open Informedica.GenUnits.Lib.ValueUnit


    let toString = toStringEngShort

    let inline (>>*) u f =
        u |> printfn "%A"
        f u

    // Some basic value units
    let mg400 = 400N |> createSingle Units.Mass.milliGram
    let gram2 = 2N   |> createSingle Units.Mass.gram
    let ml50  = 50N  |> createSingle Units.Volume.milliLiter
    let ml5  = 5N    |> createSingle Units.Volume.milliLiter
    let l5 = 5N      |> createSingle Units.Volume.liter
    let day2 = 2N    |> createSingle Units.Time.day
    let hour3 = 3N   |> createSingle Units.Time.hour
    let kg10 = 10N   |> createSingle Units.Weight.kiloGram

    // The count group is a special unit group
    // with only one unit: times.
    let times3 = 3N |> createSingle Units.Count.times
    let times100 = 100N |> createSingle Units.Count.times
    let weeks4 = 4N |> Units.Time.nWeek


    let unitTests =

        let toBase = toBaseValue >> (Array.map BigRational.toDecimal) >> Array.head

        testList "Unit" [
            test "base value of 400 mg " {
                Expect.equal "should equal 0.4 g" 0.4m (mg400 |> toBase)
            }

            test "base value of 50 ml = 0.05 l" {
                Expect.isTrue "should equal 0.05" (ml50 |> toBase = 0.05m)
            }

            test "base value of 5 ml = 0.005 l" {
                Expect.isTrue "should equal 0.005" (ml5 |> toBase = 0.005m)
            }

            test "base value of 5 l = 5 l" {
                Expect.isTrue "should equal 5" (l5 |> toBase = 5m)
            }

            test "count 3 times 5 ml results in 15 ml" {
                Expect.equal "should equal 0.015 L" 0.015m (ml5 * times3 |> toBase)
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
                    |> fun (_, u) ->
                        mg400
                        |> toBaseValue
                        |> create u
                        |> toUnitValue
                        |> create u
                Expect.equal "" vu mg400
            }

            test "there and back again 2 units" {
                let vu1 =
                    1N |> singleWithUnit (Units.Mass.milliGram |> per Units.Volume.milliLiter)
                let vu2 =
                    vu1
                    |> get
                    |> fun (_, u) ->
                        vu1
                        |> toBaseValue
                        |> create u
                        |> toUnitValue
                        |> create u
                Expect.equal "" vu1 vu2
            }

            test "there and back again 3 units" {
                let vu1 =
                    1N
                    |> singleWithUnit (Units.Mass.milliGram
                    |> per Units.Volume.milliLiter
                    |> per Units.Time.day)
                let vu2 =
                    vu1
                    |> get
                    |> fun (_, u) ->
                        vu1
                        |> toBaseValue
                        |> create u
                        |> toUnitValue
                        |> create u
                Expect.equal "" vu1 vu2
            }
        ]


    let stringTests =

        testList "Print" [
            test "4 weken unit to string should return 4 weken" {
                weeks4
                |> Units.toStringDutchShort
                |> Expect.equal "should be equal" "4 weken[Time]"
            }

            test "1 x[Count]/4 weken[Time] from string should return a combiunit" {
                "1 x[Count]/4 weken[Time]"
                |> fromString
                |> function
                | Success (vu, _, _)  -> vu
                | Failure (err, _, _) -> $"can't run this test: {err}" |> failwith
                |> toStringDutchShort
                |> Expect.equal "should equal" "1 x[Count]/4 weken[Time]"
            }

            test "unit with unit number can find group" {
                "4 weken"
                |> Units.stringWithGroup
                |> Expect.equal "should be equal" "4 weken[Time]"
            }

        ]


    let comparisonTests =

        testList "Comparison" [

            test "ml50 < l5 using normal operator < should be false" {
                ml50 < l5
                |> Expect.isFalse  ""
            }

            test "ml50 < l5 using special operator <?" {
                ml50 <? l5
                |> Expect.isTrue  ""
            }

            test "ml50 = l5 should be false" {
                ml50 =? l5
                |> Expect.isFalse  ""
            }

            test "ml50 * times 100 = l5 should be true" {
                ml50 * times100  =? l5
                |> Expect.isTrue ""
            }
        ]


    let calculationTests =

        let (>>?) res exp =
            match exp |> fromString with
            | Success (exp, _, _) ->
                res = exp
                |> Expect.isTrue  ""
            | _ ->
                false |> Expect.isTrue ""
                failwith "can't run the test"
            res


        testList "Calculation" [

            test "3 times 3 = 9" {
                times3 * times3 |> toBaseValue = [|9N|]
                 |> Expect.isTrue ""
            }

            test "3 divided by 3 = 1" {
             times3 / times3 |> toBaseValue = [|1N|]
             |> Expect.isTrue  ""
            }

            test "3 plus 3 = 6" {
             times3 + times3 |> toBaseValue = [|6N|]
             |> Expect.isTrue ""
            }

            test "3 minus 3 = 0" {
             times3 - times3 |> toBaseValue = [|0N|]
             |> Expect.isTrue ""
            }

            test "can add or subrract within the same unit group" {
             (l5 + ml50) >? l5
             |> Expect.isTrue  ""

             (l5 - ml50) <? l5
             |> Expect.isTrue ""
            }

            test "cannot add or subrract with different unit groups" {
             (fun _ -> (l5 + mg400) >? l5 |> ignore)
             |> Expect.throws ""

             (fun _ -> (l5 - mg400) <? l5 |> ignore)
             |> Expect.throws ""
            }

            test "division by unit with the same unit group results in a count" {
             // division by a simple unit
             let _, u = (l5 / ml50) |> get
             let g = u |> Group.unitToGroup

             g = Group.CountGroup
             |> Expect.isTrue ""

             // division by a more complex unit
             let vu1 = (mg400 / ml5 / day2)
             let vu2 = (mg400 / l5  / hour3)

             vu1 / vu2
             |> get
             |> snd
             |> Group.unitToGroup
             |> Expect.equal "" Group.CountGroup
            }

            test "can calculate with units" {
             (mg400 + mg400)
             // 400 mg + 400 mg = 800 mg
             >>? "800 mg[Mass]"
             |> (fun vu -> vu / ml50)
             // 800 mg / 50 ml = 16 mg/ml
             >>? "16 mg[Mass]/ml[Volume]"
             |> (fun vu -> vu * ml50)
             // 16 mg/ml * 50 ml = 800 mg
             >>? "800 mg[Mass]"
             // 800 mg - 400 mg = 400 mg
             |> (fun vu -> vu - mg400)
             >>? "400 mg[Mass]"
             |> ignore
            }

            test "division with 3 unit values" {
             let vu =
                 mg400 / (mg400 / ml50) // equals mg400 * (ml50 / mg400) = mg50

             Expect.equal "should be 50 ml" ml50 vu
            }

            test "more complicated division" {
             let vu =
                 (mg400 / ml50) / (day2 / ml50) // equals (mg400 / ml50) * (ml50 / day2) = mg400 / day2

             Expect.equal "" (mg400 / day2) vu
            }

            test "divsion resulting in combi with 3 units" {
                mg400/kg10/day2
                |> toStringEngShort
                |> Expect.equal "should be equal" "20 mg[Mass]/kg[Weight]/day[Time]"

            }

            test "multiplying with a combi with 3 units with the middle unit" {
                (mg400/kg10/day2) * kg10
                |> toStringEngShort
                |> Expect.equal "should be equal" "200 mg[Mass]/day[Time]"
            }

        ]


    let conversionTests =

        testList "Conversion" [

            test "can convert from 5 liter to 5000 ml" {
                5000N |> createSingle Units.Volume.milliLiter
                |> Expect.equal "" (l5 ==> Units.Volume.milliLiter)
            }

            test "unit group from 400 mg / day = mass per timegroup" {
                (mg400 / (1N |> createSingle Units.Time.day))
                |> get
                |> snd
                |> Group.unitToGroup
                |> Group.toString
                |> Expect.equal "" "Mass/Time"
            }

            test "the number of possible units is the permutation of the units in each unitgroup" {
                // get the number of units in the mass group
                let mc = Group.MassGroup   |> Group.getUnits |> List.length
                // get the number of units in the volume group
                let vc = Group.VolumeGroup |> Group.getUnits |> List.length

                (mg400 / ml50)
                |> get
                |> snd
                |> Group.unitToGroup
                |> Group.getUnits
                |> List.length
                // the number of units for the Mass/Volume group is
                // the multiple of mc * vc
                |> Expect.equal "" (mc* vc)
            }

        ]


    let tests = testList "ValueUnit Tests" [
            stringTests
            calculationTests
            conversionTests
            comparisonTests
            unitTests

            test "Unquote tests" {
                try
                    Unquote.tests () |> ignore
                    Unquote.testRemoveBigRationalMultiples()
                    true
                with
                | _ -> false
                |> Expect.isTrue "should all be true"
            }

        ]


