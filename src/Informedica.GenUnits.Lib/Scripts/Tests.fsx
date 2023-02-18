
#load "../../../scripts/Expecto.fsx"
#load "load.fsx"


module Tests =

    open MathNet.Numerics
    open Expecto
    open Expecto.Flip

    open Informedica.GenUnits.Lib
    open Informedica.Utils.Lib.BCL
    open ValueUnit

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


    [<Tests>]
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
                        |> ValueUnit.toBaseValue
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
                        |> ValueUnit.toBaseValue
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
                        |> ValueUnit.toBaseValue
                        |> create u
                        |> toUnitValue
                        |> create u
                Expect.equal "" vu1 vu2
            }
        ]


    [<Tests>]
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


    [<Tests>]
    let calculationTests =

        let (>>?) res exp =
            res = (exp |> fromString)
            |> Expect.isTrue  ""
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


    [<Tests>]
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
            calculationTests
            conversionTests
            comparisonTests
            unitTests
        ]


open Expecto

Tests.tests
|> Expecto.run


