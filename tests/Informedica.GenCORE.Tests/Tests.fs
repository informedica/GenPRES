namespace Informedica.GenCore.Tests


module Tests =

    open System

    open Expecto
    open Expecto.Flip

    open Informedica.Utils.Lib.BCL

    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenCore.Lib.Patients

    open Informedica.Utils.Lib.BCL


    module CalculationTests =

        let bsaCalcList =
            [
                Calculations.BSA.duBois, "DuBois", 1.6949m<bsa>
                Calculations.BSA.fujimoto, "Fuijimoto", 1.6476m<bsa>
                Calculations.BSA.gehanAndGeorge, "Gehan and George", 1.6916m<bsa>
                Calculations.BSA.haycock, "Haycock", 1.6804m<bsa>
                Calculations.BSA.mosteller, "Mosteller", 1.6833m<bsa>
            ]


        let tests =
            testList
                "Calculations"
                [
                    testList
                        "BSA"
                        [
                            for f, s, e in bsaCalcList do
                                test $"{s}" {
                                    Calculations.BSA.calcBSA f (Some 5) 60m<kg> 170m<cm>
                                    |> Expect.equal $"should be {e}" e
                                }
                        ]

                    testList
                        "age"
                        [
                            test "born Dec 7, 2022 and current Jan 25, 2023" {
                                let dtNow = DateTime(2023, 1, 25)
                                let dtBd = DateTime(2022, 12, 7)

                                Calculations.Age.adjustedAge 0<day> 36<week> dtBd dtNow
                                |> Expect.equal "should be 21 days" 21<day>
                            }

                            for months, expected in [ 216, 6570; 11, 330; 14, 425 ] do
                                test $"monthsToDays: %i{months} months is %i{expected} days" {
                                    months * 1<month>
                                    |> Calculations.Age.monthsToDays
                                    |> Expect.equal "the days" (expected * 1<day>)
                                }

                            for m, d in
                                [
                                    0N, 0N
                                    1N / 2N, 15N
                                    1N, 30N
                                    11N, 330N
                                    12N, 365N
                                    25N / 2N, 380N
                                    216N, 6570N
                                    9999N, 833N * 365N + 3N * 30N
                                ] do
                                test
                                    $"fractionalMonthsToDays: %s{m |> BigRational.toString} months is %s{d |> BigRational.toString} days" {
                                    m |> Calculations.Age.fractionalMonthsToDays |> Expect.equal "the days" d
                                }

                            for d, m in
                                [
                                    0N, 0N
                                    15N, 1N / 2N
                                    30N, 1N
                                    330N, 11N
                                    359N, 359N / 30N
                                    360N, 1199N / 100N
                                    364N, 1199N / 100N
                                    365N, 12N
                                    380N, 25N / 2N
                                    6569N, 204N + 1199N / 100N
                                    6570N, 216N
                                ] do
                                test
                                    $"daysToFractionalMonths: %s{d |> BigRational.toString} days is %s{m |> BigRational.toString} months" {
                                    d |> Calculations.Age.daysToFractionalMonths |> Expect.equal "the months" m
                                }

                            test "every whole month up to 120 years goes to days and back" {
                                [ 0N .. 1440N ]
                                |> List.filter (fun m ->
                                    m
                                    |> Calculations.Age.fractionalMonthsToDays
                                    |> Calculations.Age.daysToFractionalMonths
                                    <> m
                                )
                                |> Expect.isEmpty "no month that does not come back"
                            }

                            test "the months of the days never go down from one day to the next" {
                                [ 0N .. 20N * 365N ]
                                |> List.map Calculations.Age.daysToFractionalMonths
                                |> List.pairwise
                                |> List.filter (fun (m1, m2) -> m2 < m1)
                                |> Expect.isEmpty "no day with fewer months than the day before"
                            }

                            test "an age reaches a bound in months with two decimals on the day the bound gives in days" {
                                // for each bound up to 20 years, the first whole day at or after the bound in days
                                // is at or above the bound in months, and the day before it is below
                                let toDays = Calculations.Age.fractionalMonthsToDays
                                let toMonths = Calculations.Age.daysToFractionalMonths

                                [ 0N .. 24000N ]
                                |> List.map (fun n -> n / 100N)
                                |> List.filter (fun b ->
                                    let d = b |> toDays |> BigRational.ToBigInt |> BigRational.FromBigInt
                                    let d = if d < (b |> toDays) then d + 1N else d

                                    (d |> toMonths) < b || (d > 0N && (d - 1N |> toMonths) >= b)
                                )
                                |> Expect.isEmpty "no bound reached a day early or late"
                            }

                            let days = Calculations.Age.yearsMonthsWeeksDaysToDays

                            test "216 months is 18 years of 365 days" {
                                days 0<year> 216<month> 0<week> 0<day> |> Expect.equal "6,570 days" 6570<day>
                            }

                            test "1 year and 14 months is 2 years and 2 months" {
                                days 1<year> 14<month> 0<week> 0<day>
                                |> Expect.equal "the same days" (days 2<year> 2<month> 0<week> 0<day>)
                            }

                            test "11 months is still 330 days" {
                                days 0<year> 11<month> 0<week> 0<day> |> Expect.equal "330 days" 330<day>
                            }

                            test "17 years and 11 months by the calendar stays below 18 years" {
                                days 17<year> 11<month> 4<week> 2<day> < 6570<day>
                                |> Expect.isTrue "below 6,570 days"
                            }
                        ]

                    testList
                        "renal function"
                        [

                            test "renal function using creat 2009 formula" {
                                let creat = 181.<microMol / L> |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                let race = Calculations.Renal.Black
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcCreatinine09 gend race age creat
                                |> float
                                |> Expect.floatClose "" Accuracy.low 30.46
                            }

                            test "renal function using creat 2021 formula" {
                                let creat = 181.<microMol / L> |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcCreatinine21 gend age creat
                                |> float
                                |> Expect.floatClose "" Accuracy.low 28.
                            }

                            test "renal function using cystatin creatinine 2012 formula" {
                                let creat = 181.<microMol / L> |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let cystatin = 1.5<mg / L> |> Calculations.Renal.CystatinMilligramPerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                let race = Calculations.Renal.Other
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcCystatinCreatinine12 gend race age creat cystatin
                                |> float
                                |> Expect.floatClose "" Accuracy.low 32.99
                            }

                            test "renal function using cystatin creatinine 2021 formula" {
                                let creat = 181.<microMol / L> |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let cystatin = 1.5<mg / L> |> Calculations.Renal.CystatinMilligramPerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcCystatinCreatinine21 gend age creat cystatin
                                |> float
                                |> Double.fixPrecision 2
                                |> Expect.floatClose "" Accuracy.low 36.
                            }

                            test "renal function using cystatin only formula" {
                                let cystatin = 1.5<mg / L> |> Calculations.Renal.CystatinMilligramPerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcCystatin12 gend age cystatin
                                |> float
                                |> Double.fixPrecision 2
                                |> Expect.floatClose "" Accuracy.low 43.
                            }

                            test "renal function using MDRD" {
                                let creat = 181.<microMol / L> |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                let race = Calculations.Renal.Black
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcMDRD gend race age creat
                                |> float
                                |> Expect.floatClose "" Accuracy.low 30.30
                            }

                            test "renal function using pediatric Schwartz" {
                                let creat = 181.<microMol / L> |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let height = 100.<cm>

                                Calculations.Renal.calcPediatricScharz height creat
                                |> float
                                |> Expect.floatClose "" Accuracy.low 20.171
                            }

                            test "renal function using KCID" {
                                let creat = 100.<microMol / L> |> Calculations.Renal.CreatinineMicroMolePerLiter
                                // printfn $"{100.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                let cyst = 1.5<mg / L> |> Calculations.Renal.CystatinMilligramPerLiter

                                let bun = 15.<mmol / L> |> Calculations.Renal.UreaMilliMolePerLiter
                                // printfn $"{15.<mmol/L> |> Conversions.Urea.toMilliGramPerDeciLiter}"
                                let height = 1.<m>

                                Calculations.Renal.calcPediatricCystatinCreatinineCKID
                                    Calculations.Renal.Male
                                    height
                                    creat
                                    cyst
                                    bun
                                |> float
                                |> Double.fixPrecision 2
                                |> Expect.floatClose "" Accuracy.low 40.
                            }

                        ]

                ]


    module MinMaxTests =

        module Calculator = MinMax.Calculator


        let minGTmax (maxIncl, max) (minIncl, min) = if minIncl && maxIncl then min > max else min >= max

        let validate (min: (bool * BigRational) option) max = Calculator.validate minGTmax min max

        let tests =
            testList
                "MinMaxTests.validate2"
                [
                    fun min max ->
                        let min = min |> Option.map (fun (minIncl, min) -> minIncl, min |> BigRational.fromInt)

                        let max = max |> Option.map (fun (maxIncl, max) -> maxIncl, max |> BigRational.fromInt)

                        try
                            validate min max
                            |> function
                                | Error _ -> ()
                                | Ok(min, max) ->
                                    let toBrStr =
                                        BigRational.toFloat >> Double.toStringNumberNLWithoutTrailingZerosFixPrecision 3

                                    Calculator.toString toBrStr min max |> ignore //printfn "Pass: %s"

                            true
                        with _ ->
                            false
                    |> Generators.testProp "with simple integers never throws an exception"


                    fun min max ->
                        let min = min |> Option.map (fun (minIncl, min) -> minIncl, min |> BigRational.fromInt)

                        let max = max |> Option.map (fun (maxIncl, max) -> maxIncl, max |> BigRational.fromInt)

                        try
                            validate min max
                            |> function
                                | Error _ -> ()
                                | Ok(min, max) ->
                                    let toBrStr =
                                        BigRational.toFloat >> Double.toStringNumberNLWithoutTrailingZerosFixPrecision 3

                                    Calculator.toStringNL toBrStr min max |> ignore // printfn "Pass: %s"

                            true
                        with _ ->
                            false
                    |> Generators.testProp "with simple integers dutch version never throws an exception"


                    fun min incr max ->
                        try
                            Calculator.validate min incr max |> ignore
                            true
                        with _ ->
                            false
                    |> Generators.testProp "never throws an exception"


                    fun min max ->
                        validate min max
                        |> function
                            | Ok(Some min, Some max) ->
                                let min = min |> snd
                                let max = max |> snd
                                min <= max
                            | _ -> true
                    |> Generators.testProp "min always is equal or less than max"
                ]


    module MinMaxTests2 =

        module MinMax = MinMax.Optics


        let mmToStr =
            MinMax.toString
                (ValueUnit.toStringDecimalDutchShortWithPrec 2)
                (ValueUnit.toStringDecimalDutchShortWithPrec 2)
                "van (incl) "
                "van (incl) "
                "tot (incl) "
                "tot (excl) "


        let createValueUnit (d: decimal) u =
            let v = d |> float

            match u |> UnitsParse.fromString with
            | None -> None
            | Some u ->
                match v |> BigRational.fromFloat with
                | None -> None
                | Some v -> ValueUnit.createSingle u v |> Some


        let mg10, mg20 =
            createValueUnit 10.m "mg[Mass]" |> Option.get, createValueUnit 20.m "mg[Mass]" |> Option.get

        let mgIncl10, mgIncl20 = mg10 |> Limit.inclusive, mg20 |> Limit.inclusive

        let mgExcl10, mgExcl20 = mg10 |> Limit.exclusive, mg20 |> Limit.exclusive

        let mg30, mg40 =
            createValueUnit 30.m "mg[Mass]" |> Option.get, createValueUnit 40.m "mg[Mass]" |> Option.get

        let mgIncl30, mgIncl40 = mg30 |> Limit.inclusive, mg40 |> Limit.inclusive


        let toString () =
            MinMax.empty
            |> MinMax.setMin (createValueUnit 1.m "mg[Mass]" |> Option.get |> Inclusive)
            |> MinMax.setMax (createValueUnit 10.m "mg[Mass]" |> Option.get |> Inclusive)
            |> mmToStr


        let fromDecimal (v: decimal) u = v |> BigRational.fromDecimal |> ValueUnit.createSingle u


        let ageInMo = (fun n -> fromDecimal n Units.Time.month)


        let ageInYr = (fun n -> fromDecimal n Units.Time.year)


        let ageInclOneMo, ageExclOneYr = 1m |> ageInMo |> Inclusive, 1m |> ageInYr |> Exclusive


        let ageRange = MinMax.empty |> MinMax.setMin ageInclOneMo |> MinMax.setMax ageExclOneYr


        let valueComp =

            [
                mgIncl10, mgIncl10, Limit.eq, true
                mgIncl10, mgIncl20, Limit.eq, false
                mgIncl10, mgIncl20, Limit.st true false, true
                mgIncl10, mgIncl10, Limit.st true false, false
                mgIncl10, mgIncl20, Limit.ste true false, true
                mgIncl10, mgIncl10, Limit.ste true false, true
                mgIncl10, mgIncl20, Limit.gt true false, false
                mgIncl10, mgIncl10, Limit.gt true false, false
                mgIncl10, mgIncl20, Limit.gte true false, false
                mgIncl10, mgIncl10, Limit.gte true false, true

                mgExcl10, mgExcl10, Limit.eq, false
                mgExcl10, mgExcl20, Limit.st true false, true
                mgExcl10, mgExcl10, Limit.st true false, false
                mgExcl10, mgExcl20, Limit.ste true false, true
                mgExcl10, mgExcl10, Limit.ste true false, false //Min Excl 10 mg <= Max Excl 10 mg
                mgExcl10, mgExcl20, Limit.gt true false, false
                mgExcl10, mgExcl10, Limit.gt true false, true //Min Excl 10 mg > Max Excl 10 mg
                mgExcl10, mgExcl20, Limit.gte true false, false
                mgExcl10, mgExcl10, Limit.gte true false, true

                mgIncl10, mgExcl10, Limit.eq, false
                mgIncl10, mgExcl10, Limit.gt true false, true //Min Incl 10 mg > Max Excl 10 mg
                mgIncl10, mgExcl10, Limit.st true false, false //Min Incl 10 mg < Max Excl 10 mg

                ageInclOneMo, ageExclOneYr, Limit.eq, false
                ageInclOneMo, ageExclOneYr, Limit.st true false, true
                ageInclOneMo, ageExclOneYr, Limit.ste true false, true
                ageInclOneMo, ageExclOneYr, Limit.gt true false, false
                ageInclOneMo, ageExclOneYr, Limit.gte true false, false

            ]


        // ToDo handle None cases correctly?
        let testFold () =
            let maximize = MinMax.foldMaximize true true
            let minimize = MinMax.foldMinimize true true

            let mms =
                [
                    MinMax.empty
                    MinMax.empty |> MinMax.setMin mgIncl10
                    MinMax.empty |> MinMax.setMin mgIncl20
                    MinMax.empty |> MinMax.setMax mgIncl30
                    MinMax.empty |> MinMax.setMax mgIncl40
                    MinMax.empty |> MinMax.setMin mgIncl10 |> MinMax.setMax mgIncl30
                    MinMax.empty |> MinMax.setMin mgIncl20 |> MinMax.setMax mgIncl30
                    MinMax.empty |> MinMax.setMin mgIncl30 |> MinMax.setMax mgIncl30
                    MinMax.empty |> MinMax.setMin mgIncl40 |> MinMax.setMax mgIncl40
                ]

            mms |> maximize |> mmToStr, mms |> minimize |> mmToStr


        let inRange =
            let mm1 = MinMax.empty

            let mm2 = MinMax.empty |> MinMax.setMin mgIncl10

            let mm3 = MinMax.empty |> MinMax.setMax mgIncl40

            let mm4 = MinMax.empty |> MinMax.setMin mgIncl20 |> MinMax.setMax mgIncl30

            [
                (mg10, mm1, true)
                (mg20, mm1, true)
                (mg30, mm1, true)
                (mg40, mm1, true)
                (mg10, mm2, true)
                (mg20, mm2, true)
                (mg30, mm2, true)
                (mg40, mm2, true)
                (mg10, mm3, true)
                (mg20, mm3, true)
                (mg30, mm3, true)
                (mg40, mm3, true)
                (mg10, mm4, false)
                (mg20, mm4, true)
                (mg30, mm4, true)
                (mg40, mm4, false)
            ]


        let tests =
            testList
                "MinMax2"
                [
                    test "minGTmax" { mgIncl20 |> Limit.minGTmax mgIncl10 |> Expect.isTrue $"{mgIncl20} > {mgIncl10}" }

                    test "minGTmax is false for a minimum below the maximum" {
                        mgIncl10 |> Limit.minGTmax mgIncl20 |> Expect.isFalse $"{mgIncl10} < {mgIncl20}"
                    }

                    test "toString" { toString () |> Expect.equal "should equal" "1 mg - 10 mg" }

                    testList
                        "Validate"
                        [
                            test "cannot set have limits with different unit groups" {
                                { ageRange with Max = Some mgIncl10 }
                                |> MinMax.validate
                                |> function
                                    | Ok mm -> false |> Expect.isTrue $"{mm |> mmToStr} is not valid!"
                                    | Error msg -> true |> Expect.isTrue $"{msg}"
                            }

                            for min, max, valid in
                                [
                                    mgIncl10, mgIncl10, true
                                    mgIncl10, mgExcl10, false
                                    mgExcl10, mgIncl10, false
                                    mgExcl10, mgExcl10, false
                                    mgIncl10, mgExcl20, true
                                    mgExcl10, mgExcl20, true
                                    mgIncl20, mgIncl10, false
                                    mgExcl20, mgExcl10, false
                                ] do
                                test $"{min |> Limit.toString true} {max |> Limit.toString false} is valid: {valid}" {
                                    MinMax.create (Some min) (Some max)
                                    |> MinMax.validate
                                    |> Result.isOk
                                    |> Expect.equal $"should be {valid}" valid
                                }

                            test "a range with one limit or none is valid" {
                                [
                                    MinMax.empty
                                    MinMax.create (Some mgExcl10) None
                                    MinMax.create None (Some mgExcl10)
                                ]
                                |> List.forall (MinMax.validate >> Result.isOk)
                                |> Expect.isTrue "all valid"
                            }
                        ]

                    testList
                        "intersect"
                        [
                            let day v = fromDecimal v Units.Time.day

                            test "two ranges that touch at an exclusive limit have no overlap" {
                                MinMax.create (day 30m |> Inclusive |> Some) (day 365m |> Exclusive |> Some)
                                |> MinMax.intersect (
                                    MinMax.create (day 365m |> Inclusive |> Some) (day 730m |> Exclusive |> Some)
                                )
                                |> Expect.equal "empty" MinMax.empty
                            }

                            test "two ranges that touch at inclusive limits overlap at the single value" {
                                MinMax.create (day 30m |> Inclusive |> Some) (day 365m |> Inclusive |> Some)
                                |> MinMax.intersect (
                                    MinMax.create (day 365m |> Inclusive |> Some) (day 730m |> Exclusive |> Some)
                                )
                                |> Expect.equal "365 days" (day 365m |> MinMax.createExact)
                            }

                            test "two ranges that overlap keep the overlap" {
                                MinMax.create (day 30m |> Inclusive |> Some) (day 400m |> Exclusive |> Some)
                                |> MinMax.intersect (
                                    MinMax.create (day 365m |> Inclusive |> Some) (day 730m |> Exclusive |> Some)
                                )
                                |> Expect.equal
                                    "365 to 400 days"
                                    (MinMax.create (day 365m |> Inclusive |> Some) (day 400m |> Exclusive |> Some))
                            }
                        ]

                    testList
                        "canMeet"
                        [
                            let day v = fromDecimal v Units.Time.day

                            let range min max =
                                MinMax.create (day min |> Inclusive |> Some) (day max |> Exclusive |> Some)

                            test "two empty ranges meet" {
                                MinMax.canMeet MinMax.empty MinMax.empty |> Expect.isTrue "nothing restricts"
                            }

                            test "an empty range meets a range, on either side" {
                                MinMax.canMeet MinMax.empty (range 30m 365m) |> Expect.isTrue "first empty"
                                MinMax.canMeet (range 30m 365m) MinMax.empty |> Expect.isTrue "second empty"
                            }

                            test "two equal ranges meet" {
                                MinMax.canMeet (range 30m 365m) (range 30m 365m) |> Expect.isTrue "the same"
                            }

                            test "two ranges that overlap meet" {
                                MinMax.canMeet (range 30m 400m) (range 365m 730m) |> Expect.isTrue "overlap"
                            }

                            test "two ranges that touch at an exclusive limit do not meet" {
                                MinMax.canMeet (range 30m 365m) (range 365m 730m) |> Expect.isFalse "touch"
                            }

                            test "a range with a minimum above its maximum meets no range" {
                                let inverted = range 365m 30m

                                MinMax.canMeet inverted inverted |> Expect.isFalse "itself"
                                MinMax.canMeet inverted MinMax.empty |> Expect.isFalse "an empty range"
                                MinMax.canMeet inverted (range 1m 100m) |> Expect.isFalse "another range"
                            }
                        ]

                    testList
                        "valueComparison"
                        [

                            for v1, v2, cp, exp in valueComp do
                                test
                                    $"comparing {v1 |> Limit.toString true} {cp |> Limit.cmpToStr} {v2 |> Limit.toString false}" {
                                    v1 |> cp <| v2 |> Expect.equal $"should be {exp}" exp
                                }
                        ]

                    test "minimize, maximize" { testFold () |> Expect.equal "should equal" ("10 mg - 40 mg", "30 mg") }

                    testList
                        "in range"
                        [
                            for v, mm, b in inRange do
                                test
                                    $"%s{v |> ValueUnit.toStringDecimalDutchShortWithPrec 0} in range: %s{mm |> mmToStr} = %A{MinMax.inRange v mm}" {
                                    MinMax.inRange v mm |> Expect.equal $"should be {b}" b
                                }
                        ]
                ]


        module DtoTests =

            module Dto = MinMax.Dto

            let dto () = Dto.dto ()


            let tests =
                testList
                    "Dto"
                    [
                        test "MinMax from Dto is the same as empty" {
                            dto ()
                            |> Dto.fromDto
                            |> function
                                | Some mm -> mm |> Expect.equal "should be an empty mm" MinMax.empty
                                | None -> false |> Expect.isTrue "could not create an empty dto"
                        }

                        test "MinMax dto setting min and max" {
                            // Add min and max to dto and there and back again
                            let dto = dto ()
                            dto.Min.Value <- [| 1N |]
                            dto.Min.Unit <- "mg"
                            dto.Min.Group <- "mass"
                            dto.HasMin <- true
                            dto.MinIncl <- false
                            dto.Max.Value <- [| 2N |]
                            dto.Max.Unit <- "g"
                            dto.Max.Group <- "mass"
                            dto.HasMax <- true

                            dto
                            |> Dto.fromDto
                            |> function
                                | Some _ -> true |> Expect.isTrue "can create dto with min and max"
                                | None -> false |> Expect.isTrue "cannot set min and max"
                        }

                        test "MinMax that is not valid will not return from dto" {
                            let dto = dto ()

                            dto.Min.Value <- [| 2N |]
                            dto.Min.Unit <- "g"
                            dto.Min.Group <- "mass"
                            dto.HasMin <- true
                            dto.MinIncl <- false
                            dto.Max.Value <- [| 1N |]
                            dto.Max.Unit <- "mg"
                            dto.Max.Group <- "mass"
                            dto.HasMax <- true

                            dto
                            |> Dto.fromDto
                            |> Option.bind (Dto.toDto >> Some)
                            |> function
                                | Some _ -> false |> Expect.isTrue "can create dto with min and max"
                                | None -> true |> Expect.isTrue "cannot set min > than max"
                        }
                    ]


    module PatientTests =

        open FsCheck


        module DepartmentTests =

            let deps =
                [
                    (fun _ -> Department.any)
                    (fun _ -> Department.unknown)
                    Department.adultICU
                    Department.pediatricICU
                    Department.neonatalICU
                    Department.adultDepartment
                    Department.pediatricDepartment
                ]

            let tests =
                testList
                    "Department"
                    [
                        let noName = ""
                        let name = "Test"

                        test "a ward named after its kind keeps its name" {
                            let ward = Department.adultICU "Adult ICU"

                            ward
                            |> Department.toString
                            |> Department.fromString
                            |> Expect.equal "there and back" (Ok ward)
                        }

                        for f in deps do
                            test $"can create without a name {noName |> f}" {
                                let s = noName |> f |> Department.toString

                                s |> Department.fromString |> Expect.isOk "should be ok"

                                s
                                |> Department.fromString
                                |> Expect.equal "and comes back without one" (Ok(noName |> f))
                            }

                        for f in deps do
                            test $"can create with a name {name |> f}" {
                                let s = name |> f |> Department.toString

                                s |> Department.fromString |> Expect.isOk "should be ok"
                            }

                    ]


        module EnteralAccessTests =

            let enteralAccessGenerator _ = Arb.generate<EnteralAccess> |> Gen.sample 0 10

            let samples =
                enteralAccessGenerator 10
                |> List.mapi (fun i ea ->
                    i,
                    if ea = UnknownEnteral then
                        ""
                    else
                        $"{ea}" |> String.toLower
                )

            let tests =
                testList
                    "EnteralAccess"
                    [
                        for i, ea in samples do
                            test $"{i} can create enteral access {ea}" {
                                ea |> EnteralAccess.fromString |> Expect.isOk "should be ok"
                            }

                        test "cannot create enteral access from xxx" {
                            "xxx" |> EnteralAccess.fromString |> Expect.isError "should not be ok"
                        }
                    ]


        module VenousAccessTests =

            let venousAccessGenerator _ = Arb.generate<VenousAccess> |> Gen.sample 0 10

            let samples =
                venousAccessGenerator 10
                |> List.mapi (fun i ea -> i, if ea = UnknownVenous then "" else $"{ea}" |> String.toLower)

            let tests =
                testList
                    "venousAccess"
                    [
                        for i, va in samples do
                            test $"{i} can create venous access {va}" {
                                va |> VenousAccess.fromString |> Expect.isOk "should be ok"
                            }

                        test "cannot create venous access from xxx" {
                            "xxx" |> VenousAccess.fromString |> Expect.isError "should not be ok"
                        }
                    ]


        module AgeTests =


            let tests =
                testList
                    "AgeTests"
                    [
                        fun x ->
                            let dto = AgeValue.Dto.dto ()
                            dto.Years <- Some x

                            dto
                            |> AgeValue.Dto.fromDto
                            |> function
                                | Ok a -> a.Years.Value <= AgeValue.Validation.maxYear && a.Years.Value >= 0<year>
                                | Error _ -> true
                        |> Generators.testProp "years should never be > 120 years"

                        fun x ->
                            let dto = AgeValue.Dto.dto ()
                            dto.Months <- Some x

                            dto
                            |> AgeValue.Dto.fromDto
                            |> function
                                | Ok a -> a.Months.Value <= 11<month> && a.Months.Value >= 0<month>
                                | Error _ -> true
                        |> Generators.testProp "months should never be > 11"

                        fun x ->
                            let dto = AgeValue.Dto.dto ()
                            dto.Weeks <- Some x

                            dto
                            |> AgeValue.Dto.fromDto
                            |> function
                                | Ok a -> a.Weeks.Value <= 4<week> && a.Weeks.Value >= 0<week>
                                | Error _ -> true
                        |> Generators.testProp "weeks should never be > 4"

                        fun x ->
                            let dto = AgeValue.Dto.dto ()
                            dto.Days <- Some x

                            dto
                            |> AgeValue.Dto.fromDto
                            |> function
                                | Ok a -> a.Days.Value <= 6<day> && a.Days.Value >= 0<day>
                                | Error _ -> true
                        |> Generators.testProp "days should never be > 6"


                        fun y m w d ->
                            let dto = AgeValue.Dto.dto ()
                            dto.Days <- Some d
                            dto.Weeks <- Some w
                            dto.Months <- Some m
                            dto.Years <- Some y

                            dto
                            |> AgeValue.Dto.fromDto
                            |> function
                                | Ok a ->
                                    let y, m, w, d = a |> AgeValue.get

                                    Calculations.Age.yearsMonthsWeeksToDaysOpt y m w d
                                    |> fun n -> n <= (Constants.daysInYear * 120 |> Conversions.dayFromInt)
                                | Error _ -> true
                        |> Generators.testProp "should never be > 120 years"
                    ]

        module BirthDateTests =

            let intGenerator s n = Gen.sample s n Arb.generate<int>


            let tests =
                testList
                    "BirthDate"
                    [
                        test "should always return a valid date" {
                            let ys = intGenerator 2200 100
                            let ms = intGenerator 20 100
                            let ds = intGenerator 40 100

                            try
                                [
                                    for y in ys do
                                        for m in ms do
                                            for d in ds do
                                                let dto = BirthDate.Dto.dto ()
                                                dto.Year <- y
                                                dto.Month <- m
                                                dto.Day <- d

                                                dto
                                ]
                                |> List.forall (fun dto ->
                                    match dto |> BirthDate.Dto.fromDto with
                                    | Ok ymd ->
                                        try
                                            let y = ymd.Year |> int
                                            let m = ymd.Month |> int
                                            let d = ymd.Day |> int

                                            DateTime(y, m, d) |> ignore
                                            true
                                        with _ ->
                                            printfn $"cannot create datetime with {dto.Year}-{dto.Month}-{dto.Day}"
                                            false
                                    | Error _ -> true

                                )
                            with _ ->
                                true
                            |> Expect.isTrue "should never fail"
                        }

                        fun (dto: BirthDate.Dto.Dto) ->
                            let y = intGenerator 2200 1 |> List.filter (fun x -> x >= 1900)

                            dto.Year <- y |> List.tryHead |> Option.defaultValue 2000

                            dto
                            |> BirthDate.Dto.fromDto
                            |> function
                                | Ok ymd1 ->

                                    ymd1
                                    |> BirthDate.Dto.toDto
                                    |> BirthDate.Dto.fromDto
                                    |> function
                                        | Ok ymd2 -> ymd2 = ymd1
                                        | Error _ -> false
                                | Error _ -> true
                        |> testProperty "there and back again"

                        test "a year alone, or a year and a month, is no birthdate" {
                            let dto = BirthDate.Dto.dto ()
                            dto.Year <- 2016

                            let yearAlone = dto |> BirthDate.Dto.fromDto |> Result.isError

                            dto.Month <- 3
                            let noDay = dto |> BirthDate.Dto.fromDto |> Result.isError

                            dto.Day <- 15
                            let complete = dto |> BirthDate.Dto.fromDto |> Result.isOk

                            (yearAlone, noDay, complete)
                            |> Expect.equal "refused, refused, read" (true, true, true)
                        }

                        test "a birthdate is a day, and round-trips through a date" {
                            let bd = BirthDate.create 2016<year> 3<month> 15<day>

                            (bd |> BirthDate.toDate, bd |> BirthDate.toDate |> BirthDate.fromDate)
                            |> Expect.equal "the fifteenth of March, 2016" (DateTime(2016, 3, 15), bd)
                        }

                        test "birthDay cannot be from person older than 120" {
                            let dto = BirthDate.Dto.dto ()
                            dto.Year <- (DateTime.Now |> DateTime.addYears -121).Year
                            dto.Month <- 1
                            dto.Day <- 1

                            dto
                            |> BirthDate.Dto.fromDto
                            |> function
                                | Ok ymd -> $"birthdate is too long ago: {ymd.Year}"
                                | Error _ -> ""
                            |> Expect.equal "should be an empty string" ""
                        }
                    ]


        module WeightTests =

            let tests =
                testList
                    "Weight"
                    [

                        test "value cannot exceed 300 kg" {
                            let dto = WeightValue.Dto.dto ()
                            dto.Weight <- 500m
                            dto.WeightInKg <- true

                            dto
                            |> WeightValue.Dto.fromDto
                            |> function
                                | Ok _ -> false
                                | Error _ -> true
                            |> Expect.isTrue "should not be created"
                        }

                        test "value cannot be less than 200 g" {
                            let dto = WeightValue.Dto.dto ()
                            dto.Weight <- 50m
                            dto.WeightInKg <- false

                            dto
                            |> WeightValue.Dto.fromDto
                            |> function
                                | Ok _ -> false
                                | Error _ -> true
                            |> Expect.isTrue "should not be created"
                        }

                        test "a weight at date cannot be in the future" {
                            let dto = WeightAtDate.Dto.dto ()

                            dto.WeightValue.Weight <- 10m
                            dto.DateTime <- DateTime.Now.AddDays(7)

                            dto
                            |> WeightAtDate.Dto.fromDto
                            |> function
                                | Ok _ -> false
                                | Error _ -> true
                            |> Expect.isTrue "should not be in the future"
                        }

                    ]

        module HeightTests =

            let tests =
                testList
                    "Height"
                    [

                        test "value cannot exceed 3 m" {
                            let dto = HeightValue.Dto.dto ()
                            dto.Height <- 5m
                            dto.HeightInMeter <- true

                            dto
                            |> HeightValue.Dto.fromDto
                            |> function
                                | Ok _ -> false
                                | Error _ -> true
                            |> Expect.isTrue "should not be created"
                        }

                        test "value cannot be less than 20 cm" {
                            let dto = HeightValue.Dto.dto ()
                            dto.Height <- 10m
                            dto.HeightInMeter <- false

                            dto
                            |> HeightValue.Dto.fromDto
                            |> function
                                | Ok _ -> false
                                | Error _ -> true
                            |> Expect.isTrue "should not be created"
                        }

                    ]

        module PatientAgeTests =

            let days n =
                n |> BigRational.fromInt |> ValueUnit.singleWithUnit Units.Time.day |> Some

            let birthDate =
                BirthDate.create (Conversions.yearFromInt 2016) (Conversions.monthFromInt 3) (Conversions.dayFromInt 15)
                |> PatientAge.birthDate

            let tenthBirthday = DateTime(2026, 3, 15)

            let tests =
                testList
                    "PatientAge.ageInDays"
                    [
                        test "a birthdate gives the days at the date" {
                            [ tenthBirthday; tenthBirthday.AddDays 1.0; tenthBirthday.AddYears 1 ]
                            |> List.map (fun at -> birthDate |> PatientAge.ageInDays at)
                            |> Expect.equal "3650, 3651, 4015 days" [ days 3650; days 3651; days 4015 ]
                        }

                        test "an age value gives its own days, at any date" {
                            [ tenthBirthday; tenthBirthday.AddYears 2 ]
                            |> List.map (fun at -> AgeValue.ten |> PatientAge.ageValue |> PatientAge.ageInDays at)
                            |> Expect.allEqual "ten years" (days (AgeValue.ten |> AgeValue.getAgeInDays |> int))
                        }

                        test "an unknown age is none" {
                            PatientAge.unknown
                            |> PatientAge.ageInDays tenthBirthday
                            |> Expect.isNone "no days"
                        }
                    ]


        module CalendarAgeTests =

            let date (y: int) (m: int) (d: int) = DateTime(y, m, d)


            let bd y m d = date y m d |> BirthDate.fromDate


            let daysAt dt b = b |> AgeValue.fromBirthDate dt |> AgeValue.getAgeInDays |> int


            let ageAt dt b =
                b
                |> AgeValue.fromBirthDate dt
                |> AgeValue.getDef0
                |> fun (y, m, w, d) -> int y, int m, int w, int d


            let datesOf (first: DateTime) (last: DateTime) =
                [ 0 .. (last - first).Days ] |> List.map (fun n -> first.AddDays(float n))


            /// The age as it was counted before the months counted from the birth date: the calendar
            /// age from the anniversary in the last year, with at most 29 days after the month-day.
            let previous (bd: DateTime) (dt: DateTime) =
                let y, m, w, d = DateTime.age bd.Date dt.Date
                let rest = w * 7 + d |> Operators.min 29

                Conversions.yearFromInt y,
                Conversions.monthFromInt m,
                Conversions.weekFromInt (rest / 7),
                Conversions.dayFromInt (rest % 7)


            let toInts (y: int<year>, m: int<month>, w: int<week>, d: int<day>) = int y, int m, int w, int d


            let inDays (y, m, w, d) = Calculations.Age.yearsMonthsWeeksDaysToDays y m w d |> int


            let tests =
                testList
                    "Calendar age"
                    [
                        testList
                            "a birthday on 29 February"
                            [
                                test "born 29 Feb 2024, 4 years on 29 Feb 2028, not on 28 Feb" {
                                    [ date 2028 2 27; date 2028 2 28; date 2028 2 29 ]
                                    |> List.map (Calculations.Age.fromBirthDate (date 2024 2 29) >> toInts)
                                    |> Expect.equal
                                        "3 years 11 months 29 days, twice, then 4 years"
                                        [ 3, 11, 4, 1; 3, 11, 4, 1; 4, 0, 0, 0 ]
                                }

                                test "born 29 Feb 2024, one year on 28 Feb of a year without 29 Feb" {
                                    [ date 2025 2 27; date 2025 2 28; date 2029 2 28 ]
                                    |> List.map (Calculations.Age.fromBirthDate (date 2024 2 29) >> toInts)
                                    |> Expect.equal
                                        "0 years 11 months 29 days, then 1 year, 5 years"
                                        [ 0, 11, 4, 1; 1, 0, 0, 0; 5, 0, 0, 0 ]
                                }

                                test "born 29 Feb 2024, the age never decreases and never steps more than a day" {
                                    datesOf (date 2024 2 29) (date 2032 12 31)
                                    |> List.map (Calculations.Age.fromBirthDate (date 2024 2 29) >> inDays)
                                    |> List.pairwise
                                    |> List.filter (fun (d1, d2) -> d2 < d1 || d2 - d1 > 6)
                                    |> Expect.isEmpty "no step back, no jump over a week"
                                }

                                test "every other birth date gives the age it gave before" {
                                    let dates =
                                        datesOf (date 2024 1 1) (date 2028 12 31)
                                        |> List.filter (fun d -> d.Day % 3 = 0 || d.Day >= 27)

                                    [
                                        for bd in datesOf (date 2010 1 1) (date 2024 1 1) do
                                            if not (bd.Month = 2 && bd.Day = 29) then
                                                for dt in dates do
                                                    if previous bd dt <> Calculations.Age.fromBirthDate bd dt then
                                                        bd, dt
                                    ]
                                    |> Expect.isEmpty "the same age"
                                }

                                test "born 29 Feb 2000, in 2025 a month older on the 29th, not the 28th" {
                                    [ date 2025 3 28; date 2025 3 29 ]
                                    |> List.map (Calculations.Age.fromBirthDate (date 2000 2 29) >> toInts)
                                    |> Expect.equal
                                        "25 years 4 weeks, then 25 years 1 month"
                                        [ 25, 0, 4, 0; 25, 1, 0, 0 ]
                                }

                                test "a 29 February birth date is never older than before and has no negative days" {
                                    [
                                        for bd in [ date 2000 2 29; date 2012 2 29; date 2020 2 29; date 2024 2 29 ] do
                                            for dt in datesOf bd (date 2036 12 31) do
                                                let age = Calculations.Age.fromBirthDate bd dt
                                                let _, _, _, d = age |> toInts

                                                if inDays age > inDays (previous bd dt) || d < 0 then
                                                    bd, dt
                                    ]
                                    |> Expect.isEmpty "never older, no negative days"
                                }

                                test "a birth date after the date is refused" {
                                    (fun () -> Calculations.Age.fromBirthDate (date 2026 3 2) (date 2026 3 1) |> ignore)
                                    |> Expect.throwsT<ArgumentException> "refused"
                                }
                            ]

                        testList
                            "end of month and leap day ages"
                            [
                                test "born 28 Feb 2026, on 31 Mar 2026: one month and 3 days, 33" {
                                    bd 2026 2 28 |> daysAt (date 2026 3 31) |> Expect.equal "33 days" 33
                                }

                                test "born 1 Mar 2018, on 1 Mar 2028: ten years, 3650" {
                                    bd 2018 3 1 |> daysAt (date 2028 3 1) |> Expect.equal "3650 days" 3650
                                }

                                test "on 29 Mar 2026 no birth date gives 29 or 30 days" {
                                    datesOf (date 2026 1 1) (date 2026 3 29)
                                    |> List.map (BirthDate.fromDate >> daysAt (date 2026 3 29))
                                    |> List.filter (fun d -> d = 29 || d = 30)
                                    |> Expect.isEmpty "no 29 or 30 days"
                                }
                            ]

                        testList
                            "years and months on the calendar"
                            [
                                test "18 years on the 18th birthday, not the day before" {
                                    [ date 2026 2 28; date 2026 3 1 ]
                                    |> List.map (fun dt -> bd 2008 3 1 |> ageAt dt)
                                    |> Expect.equal
                                        "17 years 11 months 3 weeks 6 days, then 18 years"
                                        [ 17, 11, 3, 6; 18, 0, 0, 0 ]
                                    bd 2008 3 1 |> daysAt (date 2026 3 1) |> Expect.equal "6570 days" 6570
                                }

                                test "one month on the same day next month, not the day before" {
                                    [ date 2026 5 14; date 2026 5 15 ]
                                    |> List.map (fun dt -> bd 2026 4 15 |> ageAt dt)
                                    |> Expect.equal "29 days then one month" [ 0, 0, 4, 1; 0, 1, 0, 0 ]
                                }

                                test "born 29 Feb, one year on 28 Feb of a year without 29 Feb" {
                                    [ date 2025 2 27; date 2025 2 28 ]
                                    |> List.map (fun dt -> bd 2024 2 29 |> ageAt dt |> fun (y, _, _, _) -> y)
                                    |> Expect.equal "0 then 1 year" [ 0; 1 ]
                                }

                                test "born 31 Jan, one month on the last day of February" {
                                    [ date 2026 2 27; date 2026 2 28 ]
                                    |> List.map (fun dt -> bd 2026 1 31 |> ageAt dt |> fun (_, m, _, _) -> m)
                                    |> Expect.equal "0 then 1 month" [ 0; 1 ]
                                }

                                test "born 31 Jan, a rest of 30 days counts 29, below two months" {
                                    [ date 2026 3 30; date 2026 3 31 ]
                                    |> List.map (fun dt -> bd 2026 1 31 |> daysAt dt)
                                    |> Expect.equal "59 then 60" [ 59; 60 ]
                                }

                                test "born 1 Jan, on 31 Jan 29 days, one month on 1 Feb" {
                                    [ date 2026 1 31; date 2026 2 1 ]
                                    |> List.map (fun dt -> bd 2026 1 1 |> daysAt dt)
                                    |> Expect.equal "29 then 30" [ 29; 30 ]
                                }

                                test "below a month the age is the days lived, up to 29" {
                                    [ 0..29 ]
                                    |> List.map (fun n ->
                                        bd 2026 3 1 |> daysAt (date 2026 3 1 |> fun d -> d.AddDays(float n))
                                    )
                                    |> Expect.equal "the days lived" [ 0..29 ]
                                }

                                test "the time of day does not count" {
                                    [ DateTime(2026, 3, 1, 0, 1, 0); DateTime(2026, 3, 1, 23, 59, 0) ]
                                    |> List.map (fun dt -> bd 2008 3 1 |> daysAt dt)
                                    |> Expect.allEqual "6570 days" 6570
                                }

                                test "the age never decreases from one day to the next" {
                                    [ bd 2024 2 29; bd 2026 1 31; bd 2025 8 31; bd 2008 3 1 ]
                                    |> List.forall (fun b ->
                                        datesOf (date 2026 1 31) (date 2028 12 31)
                                        |> List.map (fun dt -> b |> daysAt dt)
                                        |> List.pairwise
                                        |> List.forall (fun (d1, d2) -> d2 >= d1)
                                    )
                                    |> Expect.isTrue "non-decreasing"
                                }
                            ]

                        testList
                            "fromDays"
                            [
                                test "the inverse of the age in days of every birth date" {
                                    datesOf (date 2006 1 1) (date 2026 3 31)
                                    |> List.map BirthDate.fromDate
                                    |> List.filter (fun b ->
                                        let av = b |> AgeValue.fromBirthDate (date 2026 3 31)
                                        av |> AgeValue.getAgeInDays |> AgeValue.fromDays <> av
                                    )
                                    |> Expect.isEmpty "every age round trips"
                                }

                                test "365 days is one year, 60 days two months" {
                                    [ 365; 60 ]
                                    |> List.map (fun d -> d * 1<day> |> AgeValue.fromDays |> AgeValue.getDef0)
                                    |> Expect.equal
                                        "1 year, 2 months"
                                        [ 1<year>, 0<month>, 0<week>, 0<day>; 0<year>, 2<month>, 0<week>, 0<day> ]
                                }
                            ]

                        testList
                            "PatientAge"
                            [
                                test
                                    "the post-menstrual age of a birth date is the gestational age plus the age in days" {
                                    bd 2026 1 31
                                    |> PatientAge.birthDate
                                    |> PatientAge.postMenstrualAgeInDays (date 2026 3 30) 182<day>
                                    |> Expect.equal "182 plus 59 days" (Some 241<day>)
                                }

                                test "the post-menstrual age is the gestational age plus the age in days the rules see" {
                                    let birth = bd 2016 2 29 |> PatientAge.birthDate

                                    [ 0..1500 ]
                                    |> List.map (float >> (date 2016 2 29).AddDays)
                                    |> List.filter (fun dt ->
                                        (birth |> PatientAge.postMenstrualAgeInDays dt 182<day>)
                                        <> (birth
                                            |> PatientAge.ageInDays dt
                                            |> Option.map (
                                                ValueUnits.ValueUnit.Measures.toSingleIntDay >> (+) 182<day>
                                            ))
                                    )
                                    |> Expect.isEmpty "no day differs"
                                }

                                test
                                    "the post-menstrual age of an age value is the gestational age plus the age in days" {
                                    AgeValue.create (Some 0<year>) (Some 1<month>) (Some 4<week>) (Some 1<day>)
                                    |> PatientAge.ageValue
                                    |> PatientAge.postMenstrualAgeInDays (date 2026 3 30) 182<day>
                                    |> Expect.equal "182 plus 59 days" (Some 241<day>)
                                }

                                test "the post-menstrual age of a patient in weeks and days, from the age in days" {
                                    { PatientData.unknown with
                                        GestationalAge = Some(AgeWeeksDays.create 26<week> 0<day>)
                                    }
                                    |> Patient.create (PatientIdentity.create "p1" "Test" (bd 2016 3 15) |> Some)
                                    |> Patient.calcPostMenstrualAge (date 2016 5 10)
                                    |> Expect.equal
                                        "26 weeks plus 55 days is 33 weeks and 6 days"
                                        (Some(AgeWeeksDays.create 33<week> 6<day>))
                                }

                                test "an unknown age has no post-menstrual age" {
                                    PatientAge.unknown
                                    |> PatientAge.postMenstrualAgeInDays (date 2026 3 30) 182<day>
                                    |> Expect.isNone "none"
                                }
                            ]
                    ]


        module AgeWeekDaysTests =

            open AgeWeeksDays.Operators


            let tests =
                testList
                    "AgeWeekDays"
                    [
                        test "weeks cannot exceed 52" {
                            let dto = AgeWeeksDays.Dto.dto ()

                            dto.Weeks <- 53

                            dto
                            |> AgeWeeksDays.Dto.fromDto
                            |> function
                                | Ok _ -> false
                                | Error _ -> true
                            |> Expect.isTrue "doesn't exceed 52"
                        }

                        test "weeks cannot be less than 20" {
                            let dto = AgeWeeksDays.Dto.dto ()

                            dto.Weeks <- 19

                            dto
                            |> AgeWeeksDays.Dto.fromDto
                            |> function
                                | Ok _ -> false
                                | Error _ -> true
                            |> Expect.isTrue "not less than 20"
                        }


                        test "days cannot exceed 6" {
                            let dto = AgeWeeksDays.Dto.dto ()

                            dto.Weeks <- 37
                            dto.Days <- 7

                            dto
                            |> AgeWeeksDays.Dto.fromDto
                            |> function
                                | Ok _ -> false
                                | Error _ -> true
                            |> Expect.isTrue "doesn't exceed 6"
                        }

                        test "a post-menstrual age is read from 20 weeks to 52 weeks plus the maximum age" {
                            let read weeks =
                                let dto = AgeWeeksDays.Dto.dto ()
                                dto.Weeks <- weeks
                                dto |> AgeWeeksDays.Dto.fromPostMenstrualDto |> Result.isOk

                            let max = AgeWeeksDays.Validation.maxPostMenstrualWeeks

                            (max, [ 20; 53; max ] |> List.map read, [ 19; max + 1 ] |> List.map read)
                            |> Expect.equal
                                "52 plus 6257 weeks; within read, outside not"
                                (52 + 6257, [ true; true; true ], [ false; false ])
                        }

                        test "preterm < full-term" {
                            AgeWeeksDays.preterm <? AgeWeeksDays.fullTerm
                            |> Expect.isTrue "preterm should be < full-term"
                        }

                        test "full-term >= full-term" {
                            AgeWeeksDays.fullTerm >=? AgeWeeksDays.fullTerm
                            |> Expect.isTrue "full-term should be >= full-term"
                        }

                        test "preterm <= preterm" {
                            AgeWeeksDays.preterm <=? AgeWeeksDays.preterm
                            |> Expect.isTrue "preterm should be <= preterm"
                        }

                        test "term is 37 weeks" {
                            AgeWeeksDays.term
                            |> Expect.equal "37 weeks" (AgeWeeksDays.create 37<week> 0<day>)
                        }

                        test "a gestational age below term is preterm, term is not" {
                            [ AgeWeeksDays.create 36<week> 6<day>; AgeWeeksDays.term ]
                            |> List.map AgeWeeksDays.isPreterm
                            |> Expect.equal "36 weeks 6 days preterm, 37 weeks not" [ true; false ]
                        }

                        test "fromValueUnit reads a time value as weeks and days, rounded down to whole days" {
                            [
                                ValueUnit.singleWithUnit Units.Time.day 199N
                                ValueUnit.singleWithUnit Units.Time.week 32N
                                ValueUnit.singleWithUnit Units.Time.day (399N / 2N)
                            ]
                            |> List.map AgeWeeksDays.fromValueUnit
                            |> Expect.equal
                                "28 weeks 3 days, 32 weeks, 28 weeks 3 days"
                                [
                                    AgeWeeksDays.create 28<week> 3<day>
                                    AgeWeeksDays.create 32<week> 0<day>
                                    AgeWeeksDays.create 28<week> 3<day>
                                ]
                        }

                        test "fromValueUnit refuses a value that is not a time" {
                            (fun () ->
                                ValueUnit.singleWithUnit Units.Weight.kiloGram 3N
                                |> AgeWeeksDays.fromValueUnit
                                |> ignore
                            )
                            |> Expect.throwsT<ArgumentException> "not a time"
                        }

                        test "isPretermValue reads a time value and is true below term" {
                            [
                                ValueUnit.singleWithUnit Units.Time.day 258N
                                ValueUnit.singleWithUnit Units.Time.week 37N
                            ]
                            |> List.map AgeWeeksDays.isPretermValue
                            |> Expect.equal "258 days preterm, 37 weeks not" [ true; false ]
                        }

                        test "fromDays splits days into weeks and days" {
                            [ 0; 6; 7; 13; 259 ]
                            |> List.map (Conversions.dayFromInt >> AgeWeeksDays.fromDays)
                            |> Expect.equal
                                "weeks and days"
                                [
                                    AgeWeeksDays.create 0<week> 0<day>
                                    AgeWeeksDays.create 0<week> 6<day>
                                    AgeWeeksDays.create 1<week> 0<day>
                                    AgeWeeksDays.create 1<week> 6<day>
                                    AgeWeeksDays.create 37<week> 0<day>
                                ]
                        }

                        test "toDays undoes fromDays for 0 to 400 days" {
                            [ 0..400 ]
                            |> List.filter (fun d ->
                                d
                                |> Conversions.dayFromInt
                                |> AgeWeeksDays.fromDays
                                |> AgeWeeksDays.toDays
                                |> int
                                <> d
                            )
                            |> Expect.isEmpty "every day back"
                        }

                        test "fromValueUnit undoes toValueUnit for 0 to 400 days" {
                            [ 0..400 ]
                            |> List.map (Conversions.dayFromInt >> AgeWeeksDays.fromDays)
                            |> List.filter (fun awd ->
                                awd |> AgeWeeksDays.toValueUnit |> AgeWeeksDays.fromValueUnit <> awd
                            )
                            |> Expect.isEmpty "every value back"
                        }

                    ]

        let tests =
            let date (y: int) (m: int) (d: int) = DateTime(y, m, d)

            let bd y m d =
                BirthDate.create (Conversions.yearFromInt y) (Conversions.monthFromInt m) (Conversions.dayFromInt d)

            let stub = PatientIdentity.create "p1" "Stub Testpatiënt" (bd 2016 3 15)

            let thereAndBack pat =
                pat |> Patient.Dto.toDto |> Patient.Dto.fromDto |> Result.mapError string

            testList
                "Patient tests"
                [
                    test "the unknown patient is anonymous, of unknown age" {
                        Patient.unknown.Identity |> Expect.isNone "no identity"
                        Patient.unknown |> Patient.age |> Expect.equal "unknown age" PatientAge.unknown
                    }

                    test "the unknown patient goes there and back" {
                        Patient.unknown |> thereAndBack |> Expect.equal "the same" (Ok Patient.unknown)
                    }

                    test "an identified patient goes there and back" {
                        let pat =
                            { PatientData.unknown with Department = Department.pediatricICU "ICK" }
                            |> Patient.create (Some stub)

                        pat |> thereAndBack |> Expect.equal "the same" (Ok pat)
                    }

                    test "the newborn patient goes there and back" {
                        let newBorn = NewBorn |> Patient.fromAgeType UnknownGender DateTime.Now

                        newBorn |> thereAndBack |> Expect.equal "the same" (Ok newBorn)
                    }

                    test "the age of an identified patient is the birthdate of the identity" {
                        PatientData.unknown
                        |> Patient.create (Some stub)
                        |> Patient.age
                        |> Expect.equal "the birthdate" (bd 2016 3 15 |> PatientAge.birthDate)
                    }

                    test "the age of an anonymous patient is the age value of the data" {
                        { PatientData.unknown with Age = Some AgeValue.ten }
                        |> Patient.create None
                        |> Patient.age
                        |> Expect.equal "the age value" (AgeValue.ten |> PatientAge.ageValue)
                    }

                    test "a part of an identity in a Dto is an error" {
                        let withIdentity id name birth =
                            let dto = Patient.unknown |> Patient.Dto.toDto
                            dto.Id <- id
                            dto.Name <- name
                            dto.BirthDate <- birth |> Option.map BirthDate.Dto.toDto
                            dto

                        [
                            // entered by hand: a name and a birthdate, no id
                            withIdentity "" "Stub Testpatiënt" (Some(bd 2016 3 15))
                            withIdentity "p1" "" (Some(bd 2016 3 15))
                            withIdentity "p1" "Stub Testpatiënt" None
                            withIdentity "" "" (Some(bd 2016 3 15))
                        ]
                        |> List.map (Patient.Dto.fromDto >> Result.isError)
                        |> Expect.allEqual "an error" true
                    }

                    test "an age value beside a birthdate in a Dto is an error" {
                        let dto = PatientData.unknown |> Patient.create (Some stub) |> Patient.Dto.toDto

                        dto.Age <- AgeValue.ten |> AgeValue.Dto.toDto |> Some

                        dto |> Patient.Dto.fromDto |> Result.isError |> Expect.isTrue "an error"
                    }

                    test "the post-menstrual age of an anonymous patient counts the age value" {
                        { PatientData.unknown with
                            Age = AgeValue.create None None (Some 8<week>) None |> Some
                            GestationalAge = Some(AgeWeeksDays.create 26<week> 0<day>)
                        }
                        |> Patient.create None
                        |> Patient.calcPostMenstrualAge (date 2016 5 10)
                        |> Expect.equal "26 weeks plus 8 weeks is 34 weeks" (Some(AgeWeeksDays.create 34<week> 0<day>))
                    }

                    test "identified and anonymous give the same post-menstrual age over every day of two years" {
                        let birth = bd 2026 1 31
                        let gest = Some(AgeWeeksDays.create 26<week> 0<day>)

                        let identified dt =
                            { PatientData.unknown with GestationalAge = gest }
                            |> Patient.create (PatientIdentity.create "p1" "Test" birth |> Some)
                            |> Patient.atDate dt

                        let anonymous dt =
                            { PatientData.unknown with
                                GestationalAge = gest
                                Age = birth |> AgeValue.fromBirthDate dt |> Some
                            }
                            |> Patient.create None
                            |> Patient.atDate dt

                        [ 0..730 ]
                        |> List.map (float >> (date 2026 1 31).AddDays)
                        |> List.filter (fun dt ->
                            (identified dt).Data.PostMenstrualAge <> (anonymous dt).Data.PostMenstrualAge
                        )
                        |> Expect.isEmpty "no day differs"
                    }

                    test "a patient is made without a post-menstrual age; at a date it is calculated" {
                        let pat =
                            { PatientData.unknown with GestationalAge = Some(AgeWeeksDays.create 26<week> 0<day>) }
                            |> Patient.create (Some stub)

                        (pat.Data.PostMenstrualAge, (pat |> Patient.atDate (date 2016 5 10)).Data.PostMenstrualAge)
                        |> Expect.equal
                            "none, then 33 weeks and 6 days"
                            (None, Some(AgeWeeksDays.create 33<week> 6<day>))
                    }

                    test "no gestational age, no post-menstrual age at a date" {
                        { PatientData.unknown with Age = Some AgeValue.ten }
                        |> Patient.create None
                        |> Patient.atDate (date 2016 5 10)
                        |> _.Data.PostMenstrualAge
                        |> Expect.isNone "none"
                    }

                    test "the calculated post-menstrual age goes there and back, also above 52 weeks" {
                        let pat =
                            { PatientData.unknown with GestationalAge = Some(AgeWeeksDays.create 26<week> 0<day>) }
                            |> Patient.create (Some stub)
                            |> Patient.atDate (date 2017 3 15)

                        pat.Data.PostMenstrualAge
                        |> Expect.equal "26 weeks plus a year" (Some(AgeWeeksDays.create 78<week> 1<day>))

                        pat |> thereAndBack |> Expect.equal "the same" (Ok pat)
                    }

                    test "the patient as strings has no name and no id" {
                        PatientData.unknown
                        |> Patient.create (Some stub)
                        |> Patient.toString (date 2026 3 15)
                        |> List.exists (fun s -> s.Contains "Stub" || s.Contains "p1")
                        |> Expect.isFalse "no identity"
                    }

                    test "the department's name is the ward's own; any, unknown or nameless is none" {
                        [
                            Department.pediatricICU "ICK", Some "ICK"
                            Department.neonatalICU "NEO", Some "NEO"
                            AnyDepartment, None
                            UnknownDepartment, None
                            Department.pediatricICU "", None
                            Department.pediatricICU "  ", None
                        ]
                        |> List.map (fun (dep, exp) -> dep |> Department.name, exp)
                        |> List.iter (fun (act, exp) -> act |> Expect.equal "the name" exp)
                    }
                ]


    [<Tests>]
    let tests =

        [
            CalculationTests.tests
            MinMaxTests.tests
            MinMaxTests2.tests
            MinMaxTests2.DtoTests.tests
            PatientTests.AgeTests.tests
            PatientTests.BirthDateTests.tests
            PatientTests.WeightTests.tests
            PatientTests.HeightTests.tests
            PatientTests.PatientAgeTests.tests
            PatientTests.CalendarAgeTests.tests
            PatientTests.AgeWeekDaysTests.tests
            PatientTests.EnteralAccessTests.tests
            PatientTests.VenousAccessTests.tests
            PatientTests.DepartmentTests.tests
            PatientTests.tests
        ]
        //|> List.skip 4
        //|> List.take 1
        |> testList "GenCore"
