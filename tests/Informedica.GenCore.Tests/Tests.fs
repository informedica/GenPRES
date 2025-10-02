namespace Informedica.GenCore.Tests




module Tests =

    open System

    open Expecto
    open Expecto.Flip

    open MathNet.Numerics

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
                        ]

                    testList
                        "renal function"
                        [

                            test "renal function using creat 2009 formula" {
                                let creat =
                                    181.<microMol/L>
                                    |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                let race = Calculations.Renal.Black
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcCreatinine09 gend race age creat
                                |> float
                                |> Expect.floatClose "" Accuracy.low 30.46
                            }

                            test "renal function using creat 2021 formula" {
                                let creat =
                                    181.<microMol/L>
                                    |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcCreatinine21 gend age creat
                                |> float
                                |> Expect.floatClose "" Accuracy.low 28.
                            }

                            test "renal function using cystatin creatinine 2012 formula" {
                                let creat =
                                    181.<microMol/L>
                                    |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let cystatin =
                                    1.5<mg/L>
                                    |> Calculations.Renal.CystatinMilligramPerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                let race = Calculations.Renal.Other
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcCystatinCreatinine12 gend race age creat cystatin
                                |> float
                                |> Expect.floatClose "" Accuracy.low 32.99
                            }

                            test "renal function using cystatin creatinine 2021 formula" {
                                let creat =
                                    181.<microMol/L>
                                    |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let cystatin =
                                    1.5<mg/L>
                                    |> Calculations.Renal.CystatinMilligramPerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcCystatinCreatinine21 gend age creat cystatin
                                |> float
                                |> Double.fixPrecision 2
                                |> Expect.floatClose "" Accuracy.low 36.
                            }

                            test "renal function using cystatin only formula" {
                                let cystatin =
                                    1.5<mg/L>
                                    |> Calculations.Renal.CystatinMilligramPerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcCystatin12 gend age cystatin
                                |> float
                                |> Double.fixPrecision 2
                                |> Expect.floatClose "" Accuracy.low 43.
                            }

                            test "renal function using MDRD" {
                                let creat =
                                    181.<microMol/L>
                                    |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let age = 57.<year>
                                let gend = Calculations.Renal.Female
                                let race = Calculations.Renal.Black
                                // printfn $"{181.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                Calculations.Renal.calcMDRD gend race age creat
                                |> float
                                |> Expect.floatClose "" Accuracy.low 30.30
                            }

                            test "renal function using pediatric Schwartz" {
                                let creat =
                                    181.<microMol/L>
                                    |> Calculations.Renal.CreatinineMicroMolePerLiter

                                let height = 100.<cm>

                                Calculations.Renal.calcPediatricScharz height creat
                                |> float
                                |> Expect.floatClose "" Accuracy.low 20.171
                            }

                            test "renal function using KCID" {
                                let creat =
                                    100.<microMol/L>
                                    |> Calculations.Renal.CreatinineMicroMolePerLiter
                                // printfn $"{100.<microMol/L> |>  Conversions.Creatinine.toMilliGramPerDeciLiter} mg/dl"
                                let cyst =
                                    1.5<mg/L>
                                    |> Calculations.Renal.CystatinMilligramPerLiter

                                let bun =
                                    15.<mmol/L>
                                    |> Calculations.Renal.UreaMilliMolePerLiter
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


        let minGTmax (maxIncl, max) (minIncl, min) =
            if minIncl && maxIncl then
                min > max
            else
                min >= max

        let validate (min : (bool * BigRational) option) max =
            Calculator.validate minGTmax min max

        let tests =
            testList
                "MinMaxTests.validate2"
                [
                    fun min max ->
                        let min =
                            min
                            |> Option.map (fun (minIncl, min) -> minIncl, min |> BigRational.fromInt)

                        let max =
                            max
                            |> Option.map (fun (maxIncl, max) -> maxIncl, max |> BigRational.fromInt)

                        try
                            validate min max
                            |> function
                                | Error _ -> ()
                                | Ok (min, max) ->
                                    let toBrStr =
                                        BigRational.toFloat
                                        >> Double.toStringNumberNLWithoutTrailingZerosFixPrecision 3

                                    Calculator.toString toBrStr min max |> ignore //printfn "Pass: %s"

                            true
                        with
                        | _ -> false
                    |> Generators.testProp "with simple integers never throws an exception"


                    fun min max ->
                        let min =
                            min
                            |> Option.map (fun (minIncl, min) -> minIncl, min |> BigRational.fromInt)

                        let max =
                            max
                            |> Option.map (fun (maxIncl, max) -> maxIncl, max |> BigRational.fromInt)

                        try
                            validate min max
                            |> function
                                | Error _ -> ()
                                | Ok (min, max) ->
                                    let toBrStr =
                                        BigRational.toFloat
                                        >> Double.toStringNumberNLWithoutTrailingZerosFixPrecision 3

                                    Calculator.toStringNL toBrStr min max
                                    |> ignore // printfn "Pass: %s"

                            true
                        with
                        | _ -> false
                    |> Generators.testProp "with simple integers dutch version never throws an exception"


                    fun min incr max ->
                        try
                            Calculator.validate min incr max |> ignore
                            true
                        with
                        | _ -> false
                    |> Generators.testProp "never throws an exception"


                    fun min max ->
                        validate min max
                        |> function
                            | Ok (Some min, Some max) ->
                                let min = min |> snd
                                let max = max |> snd
                                min <= max
                            | _ -> true
                    |> Generators.testProp "min always is equal or less than max"
                ]



    module MinMaxTests2 =

        module MinMax = MinMax.Optics

        let mmToStr =
            MinMax.toString "van (incl) " "van (incl) " "tot (incl) " "tot (excl) "


        let createValueUnit (d: decimal) u =
            let v = d |> float

            match u |> Units.fromString with
            | None -> None
            | Some u ->
                match v |> BigRational.fromFloat with
                | None -> None
                | Some v -> ValueUnit.createSingle u v |> Some


        let mg10, mg20 =
            createValueUnit 10.m "mg[Mass]" |> Option.get, createValueUnit 20.m "mg[Mass]" |> Option.get

        let mgIncl10, mgIncl20 =
            mg10 |> Limit.inclusive, mg20 |> Limit.inclusive

        let mgExcl10, mgExcl20 =
            mg10 |> Limit.exclusive, mg20 |> Limit.exclusive

        let mg30, mg40 =
            createValueUnit 30.m "mg[Mass]" |> Option.get, createValueUnit 40.m "mg[Mass]" |> Option.get

        let mgIncl30, mgIncl40 =
            mg30 |> Limit.inclusive, mg40 |> Limit.inclusive


        let toString () =
            MinMax.empty
            |> MinMax.setMin (
                createValueUnit 1.m "mg[Mass]"
                |> Option.get
                |> Inclusive
            )
            |> MinMax.setMax (
                createValueUnit 10.m "mg[Mass]"
                |> Option.get
                |> Inclusive
            )
            |> mmToStr


        let fromDecimal (v: decimal) u =
            v
            |> BigRational.fromDecimal
            |> ValueUnit.createSingle u


        let ageInMo =
            (fun n -> fromDecimal n Units.Time.month)


        let ageInYr =
            (fun n -> fromDecimal n Units.Time.year)


        let ageInclOneMo, ageExclOneYr =
            1m |> ageInMo |> Inclusive, 1m |> ageInYr |> Exclusive


        let ageRange =
            MinMax.empty
            |> MinMax.setMin ageInclOneMo
            |> MinMax.setMax ageExclOneYr


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
                    MinMax.empty
                    |> MinMax.setMin mgIncl10
                    |> MinMax.setMax mgIncl30
                    MinMax.empty
                    |> MinMax.setMin mgIncl20
                    |> MinMax.setMax mgIncl30
                    MinMax.empty
                    |> MinMax.setMin mgIncl30
                    |> MinMax.setMax mgIncl30
                    MinMax.empty
                    |> MinMax.setMin mgIncl40
                    |> MinMax.setMax mgIncl40
                ]

            mms |> maximize |> mmToStr, mms |> minimize |> mmToStr


        let inRange =
            let mm1 = MinMax.empty

            let mm2 =
                MinMax.empty |> MinMax.setMin mgIncl10

            let mm3 =
                MinMax.empty |> MinMax.setMax mgIncl40

            let mm4 =
                MinMax.empty
                |> MinMax.setMin mgIncl20
                |> MinMax.setMax mgIncl30

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
            testList "MinMax2" [
                    test "minGTmax" {
                        mgIncl20
                        |> Limit.minGTmax mgIncl10
                        |> Expect.isTrue $"{mgIncl20} > {mgIncl10}"
                    }

                    test "toString" {
                        toString ()
                        |> Expect.equal "should equal" "1 mg - 10 mg"
                    }

                    testList
                        "Validate"
                        [
                            test "cannot set have limits with different unit groups" {
                                { ageRange with Max = Some mgIncl10 }
                                |> MinMax.validate
                                |> function
                                    | Ok mm ->
                                        false
                                        |> Expect.isTrue $"{mm |> mmToStr} is not valid!"
                                    | Error msg -> true |> Expect.isTrue $"{msg}"
                            }
                        ]

                    testList
                        "valueComparison"
                        [

                            for v1, v2, cp, exp in valueComp do
                                test
                                    $"comparing {v1 |> Limit.toString true} {cp |> Limit.cmpToStr} {v2 |> Limit.toString false}" {
                                    v1 |> cp <| v2
                                    |> Expect.equal $"should be {exp}" exp
                                }
                        ]

                    test "minimize, maximize" {
                        testFold ()
                        |> Expect.equal
                            "should equal"
                            ("10 mg - 40 mg", "30 mg")
                    }

                    testList
                        "in range"
                        [
                            for v, mm, b in inRange do
                                test
                                    $"%s{v |> ValueUnit.toStringDecimalDutchShortWithPrec 0} in range: %s{mm |> mmToStr} = %A{MinMax.inRange v mm}" {
                                    MinMax.inRange v mm
                                    |> Expect.equal $"should be {b}" b
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
                                | Some mm ->
                                    mm
                                    |> Expect.equal "should be an empty mm" MinMax.empty
                                | None ->
                                    false
                                    |> Expect.isTrue "could not create an empty dto"
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
                                | Some _ ->
                                    true
                                    |> Expect.isTrue "can create dto with min and max"
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
                                | Some _ ->
                                    false
                                    |> Expect.isTrue "can create dto with min and max"
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

                        for f in deps do
                            test $"can create without a name {noName |> f}" {
                                let s = noName |> f |> Department.toString

                                s
                                |> Department.fromString
                                |> Expect.isOk "should be ok"
                            }

                        for f in deps do
                            test $"can create with a name {name |> f}" {
                                let s = name |> f |> Department.toString

                                s
                                |> Department.fromString
                                |> Expect.isOk "should be ok"
                            }

                    ]


        module EnteralAccessTests =

            let enteralAccessGenerator _ =
                Arb.generate<EnteralAccess> |> Gen.sample 0 10

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
                                ea
                                |> EnteralAccess.fromString
                                |> Expect.isOk "should be ok"
                            }

                        test "cannot create enteral access from xxx" {
                            "xxx"
                            |> EnteralAccess.fromString
                            |> Expect.isError "should not be ok"
                        }
                    ]


        module VenousAccessTests =

            let venousAccessGenerator _ =
                Arb.generate<VenousAccess> |> Gen.sample 0 10

            let samples =
                venousAccessGenerator 10
                |> List.mapi (fun i ea ->
                    i,
                    if ea = UnknownVenous then
                        ""
                    else
                        $"{ea}" |> String.toLower
                )

            let tests =
                testList
                    "venousAccess"
                    [
                        for i, va in samples do
                            test $"{i} can create venous access {va}" {
                                va
                                |> VenousAccess.fromString
                                |> Expect.isOk "should be ok"
                            }

                        test "cannot create venous access from xxx" {
                            "xxx"
                            |> VenousAccess.fromString
                            |> Expect.isError "should not be ok"
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
                                | Ok a ->
                                    a.Years.Value <= AgeValue.Validation.maxYear
                                    && a.Years.Value >= 0<year>
                                | Error _ -> true
                        |> Generators.testProp "years should never be > 120 years"

                        fun x ->
                            let dto = AgeValue.Dto.dto ()
                            dto.Months <- Some x

                            dto
                            |> AgeValue.Dto.fromDto
                            |> function
                                | Ok a ->
                                    a.Months.Value <= 11<month>
                                    && a.Months.Value >= 0<month>
                                | Error _ -> true
                        |> Generators.testProp "months should never be > 11"

                        fun x ->
                            let dto = AgeValue.Dto.dto ()
                            dto.Weeks <- Some x

                            dto
                            |> AgeValue.Dto.fromDto
                            |> function
                                | Ok a ->
                                    a.Weeks.Value <= 4<week>
                                    && a.Weeks.Value >= 0<week>
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
                                    |> fun n ->
                                        n
                                        <= (Constants.daysInYear * 120
                                            |> Conversions.dayFromInt)
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
                                                dto.Month <- Some m
                                                dto.Day <- Some d

                                                dto
                                ]
                                |> List.forall (fun dto ->
                                    match dto |> BirthDate.Dto.fromDto with
                                    | Ok ymd ->
                                        try
                                            let y = ymd.Year |> int

                                            let m =
                                                ymd.Month |> Option.defaultValue 1<month> |> int

                                            let d =
                                                ymd.Day |> Option.defaultValue 1<day> |> int

                                            DateTime(y, m, d) |> ignore
                                            true
                                        with
                                        | _ ->
                                            printfn $"cannot create datetime with {dto.Year}-{dto.Month}-{dto.Day}"
                                            false
                                    | Error _ -> true

                                )
                            with
                            | _ -> true
                            |> Expect.isTrue "should never fail"
                        }

                        fun (dto: BirthDate.Dto.Dto) ->
                            let y =
                                intGenerator 2200 1
                                |> List.filter (fun x -> x >= 1900)

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

                        test "birthDay cannot be from person older than 120" {
                            let dto = BirthDate.Dto.dto ()
                            dto.Year <- (DateTime.Now |> DateTime.addYears -121).Year

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

                    ]

        let tests =
            testList
                "Patient tests"
                [

                    test "unknown patient" {
                        Patient.unknown
                        |> Patient.Dto.toDto
                        |> Patient.Dto.fromDto
                        |> function
                            | Ok pat -> pat = Patient.unknown
                            | Error errs ->
                                printfn $"{errs}"
                                false
                        |> Expect.isTrue "there and back again"
                    }

                    test "newborn patient" {
                        let newBorn =
                            NewBorn
                            |> Patient.fromAgeType UnknownGender DateTime.Now

                        newBorn
                        |> Patient.Dto.toDto
                        |> Patient.Dto.fromDto
                        |> function
                            | Ok pat -> pat = newBorn
                            | Error errs ->
                                printfn $"{errs}"
                                false
                        |> Expect.isTrue "there and back again"
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
            PatientTests.AgeWeekDaysTests.tests
            PatientTests.EnteralAccessTests.tests
            PatientTests.VenousAccessTests.tests
            PatientTests.DepartmentTests.tests
            PatientTests.tests
        ]
        //|> List.skip 4
        //|> List.take 1
        |> testList "GenCore"