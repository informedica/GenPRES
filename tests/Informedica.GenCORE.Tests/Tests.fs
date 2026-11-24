namespace Informedica.GenCore.Tests


module Tests =

    open System

    open Expecto
    open Expecto.Flip

    open Informedica.Utils.Lib.BCL
    open CalculationWeightsAndHeightsFixture

    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.Ranges
    open Informedica.GenCore.Lib.Patients

    open Informedica.Utils.Lib.BCL


    /// The date the Dtos are read at: no weight, height or birthdate in a test is after it.
    let readAt = DateTime(2026, 11, 17)


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


    module EmergencyTests =

        open Calculations.Emergency


        /// Returns the number of days in a number of years of 365 days.
        let yearsOld n = n * Constants.daysInYear * 1<day>


        let tests =
            testList
                "Emergency"
                [
                    testList
                        "by weight, under 3 kg"
                        [
                            for kg, expected in [ 0.5m, 2.5m; 0.99m, 2.5m; 1m, 3m; 2.99m, 3m ] do
                                test $"the tube size at %M{kg} kg is %M{expected} mm" {
                                    kg * 1m<kg> |> tubeSizeByWeight |> Expect.equal "the size" (expected * 1m<mm>)
                                }

                            for kg, expected in [ 1m, 6.63m; 2m, 7.89m; 2.99m, 8.63m ] do
                                test $"the oral tube length at %M{kg} kg is %M{expected} cm" {
                                    kg * 1m<kg>
                                    |> oralTubeLengthByWeight
                                    |> Expect.equal "the length" (expected * 1m<cm>)
                                }

                            // 2.5 kg gives 10.25 cm, which is halfway and goes to 10.2 cm.
                            for kg, expected in [ 1m, 8.14m; 2.5m, 10.2m ] do
                                test $"the nasal tube length at %M{kg} kg is %M{expected} cm" {
                                    kg * 1m<kg>
                                    |> nasalTubeLengthByWeight
                                    |> Expect.equal "the length" (expected * 1m<cm>)
                                }

                            for kg, expected in [ 1.49m, 3.5m; 1.5m, 5m ] do
                                test $"the umbilical line size at %M{kg} kg is %M{expected} French" {
                                    kg * 1m<kg>
                                    |> umbilicalLineSize
                                    |> Expect.equal "the size" (expected * 1m<french>)
                                }

                            // 1.49 kg gives 12.96 cm, which has 4 digits and is rounded to 13 cm.
                            for kg, expected in [ 0.5m, 9m; 1m, 11m; 1.234m, 11.9m; 1.49m, 13m ] do
                                test $"the umbilical artery line length at %M{kg} kg, under 1.5 kg, is %M{expected} cm" {
                                    kg * 1m<kg>
                                    |> umbilicalArteryLineLengthUnder1500Gram
                                    |> Expect.equal "the length" (expected * 1m<cm>)
                                }

                            // 1.5 kg gives 13.45 cm, which is halfway and goes to 13.4 cm.
                            for kg, expected in [ 1.5m, 13.4m; 2m, 14.7m; 2.99m, 17.2m ] do
                                test $"the umbilical artery line length at %M{kg} kg, from 1.5 kg, is %M{expected} cm" {
                                    kg * 1m<kg>
                                    |> umbilicalArteryLineLengthFrom1500Gram
                                    |> Expect.equal "the length" (expected * 1m<cm>)
                                }

                            for kg, expected in [ 1.234m, 7.35m; 2m, 8.5m ] do
                                test $"the umbilical vein line length at %M{kg} kg is %M{expected} cm" {
                                    kg * 1m<kg>
                                    |> umbilicalVeinLineLength
                                    |> Expect.equal "the length" (expected * 1m<cm>)
                                }
                        ]

                    testList
                        "by age"
                        [
                            // 1 year gives 3.75 mm and 3 years 4.25 mm: both are halfway and go to 4 mm.
                            for yrs, expected in [ 0, 3.5m; 1, 4m; 2, 4m; 3, 4m; 4, 4.5m; 14, 7m; 15, 7m; 18, 7m ] do
                                test $"the cuffed tube size at %i{yrs} years is %M{expected} mm" {
                                    yrs |> yearsOld |> cuffedTubeSize |> Expect.equal "the size" (expected * 1m<mm>)
                                }

                            for yrs, expected in [ 0, 4m; 4, 5m; 12, 7m; 13, 7m ] do
                                test $"the uncuffed tube size at %i{yrs} years is %M{expected} mm" {
                                    yrs
                                    |> yearsOld
                                    |> uncuffedTubeSize
                                    |> Expect.equal "the size" (expected * 1m<mm>)
                                }

                            test "the cuffed tube size at 6 months of 30 days is 3.5 mm" {
                                180<day> |> cuffedTubeSize |> Expect.equal "the size" 3.5m<mm>
                            }

                            for yrs, expected in [ 0, 12m; 1, 12.5m; 5, 14.5m; 18, 21m ] do
                                test $"the oral tube length at %i{yrs} years is %M{expected} cm" {
                                    yrs
                                    |> yearsOld
                                    |> oralTubeLengthByAge
                                    |> Expect.equal "the length" (expected * 1m<cm>)
                                }

                            for yrs, expected in [ 0, 15m; 1, 15.5m; 5, 17.5m; 18, 24m ] do
                                test $"the nasal tube length at %i{yrs} years is %M{expected} cm" {
                                    yrs
                                    |> yearsOld
                                    |> nasalTubeLengthByAge
                                    |> Expect.equal "the length" (expected * 1m<cm>)
                                }
                        ]

                    testList
                        "energy"
                        [
                            for kg, expected in
                                [ 0.2m, 1m; 0.25m, 1m; 3m, 20m; 5m, 20m; 5.1m, 30m; 37.5m, 150m; 100m, 150m ] do
                                test $"the defibrillation at %M{kg} kg is %M{expected} joule" {
                                    kg * 1m<kg>
                                    |> defibrillation
                                    |> Expect.equal "the energy" (expected * 1m<joule>)
                                }

                            for kg, expected in [ 0.5m, 1m; 3m, 7m; 10m, 20m; 75m, 150m; 100m, 150m ] do
                                test $"the cardioversion at %M{kg} kg is %M{expected} joule" {
                                    kg * 1m<kg> |> cardioversion |> Expect.equal "the energy" (expected * 1m<joule>)
                                }
                        ]

                    testList
                        "calculate"
                        [
                            test "a weight of 1 kg gives the six interventions by weight, also with an age" {
                                calculate (Some 10<day>) (Some 1m<kg>)
                                |> Expect.equal
                                    "the interventions"
                                    [
                                        {
                                            Intervention = Intervention.TubeSize 3m<mm>
                                            Formula = Formula.TubeSizeByWeight
                                        }
                                        {
                                            Intervention = Intervention.OralTubeLength 6.63m<cm>
                                            Formula = Formula.OralTubeLengthByWeight
                                        }
                                        {
                                            Intervention = Intervention.NasalTubeLength 8.14m<cm>
                                            Formula = Formula.NasalTubeLengthByWeight
                                        }
                                        {
                                            Intervention = Intervention.UmbilicalLineSize 3.5m<french>
                                            Formula = Formula.UmbilicalLineSizeByWeight
                                        }
                                        {
                                            Intervention = Intervention.UmbilicalArteryLineLength 11m<cm>
                                            Formula = Formula.UmbilicalArteryLineLengthUnder1500Gram
                                        }
                                        {
                                            Intervention = Intervention.UmbilicalVeinLineLength 7m<cm>
                                            Formula = Formula.UmbilicalVeinLineLengthByWeight
                                        }
                                    ]
                            }

                            test "a weight of 2 kg gives the artery line formula for a weight from 1.5 kg" {
                                calculate None (Some 2m<kg>)
                                |> List.map _.Formula
                                |> Expect.contains "the formula" Formula.UmbilicalArteryLineLengthFrom1500Gram
                            }

                            test "a weight of 3 kg and an age of 4 years give the tubes by age and the energies" {
                                calculate (4 |> yearsOld |> Some) (Some 3m<kg>)
                                |> Expect.equal
                                    "the interventions"
                                    [
                                        {
                                            Intervention = Intervention.CuffedTubeSize 4.5m<mm>
                                            Formula = Formula.CuffedTubeSizeByAge
                                        }
                                        {
                                            Intervention = Intervention.UncuffedTubeSize 5m<mm>
                                            Formula = Formula.UncuffedTubeSizeByAge
                                        }
                                        {
                                            Intervention = Intervention.OralTubeLength 14m<cm>
                                            Formula = Formula.OralTubeLengthByAge
                                        }
                                        {
                                            Intervention = Intervention.NasalTubeLength 17m<cm>
                                            Formula = Formula.NasalTubeLengthByAge
                                        }
                                        {
                                            Intervention = Intervention.Defibrillation 20m<joule>
                                            Formula = Formula.DefibrillationByWeight
                                        }
                                        {
                                            Intervention = Intervention.Cardioversion 7m<joule>
                                            Formula = Formula.CardioversionByWeight
                                        }
                                    ]
                            }

                            test "a weight of 3 kg without an age gives the two energies only" {
                                calculate None (Some 3m<kg>)
                                |> List.map _.Formula
                                |> Expect.equal
                                    "the formulas"
                                    [ Formula.DefibrillationByWeight; Formula.CardioversionByWeight ]
                            }

                            test "an age without a weight gives the four tube interventions only" {
                                calculate (Some 365<day>) None |> List.length |> Expect.equal "the count" 4
                            }

                            test "no age and no weight give no interventions" {
                                calculate None None |> Expect.isEmpty "the interventions"
                            }
                        ]

                    testList
                        "PatientData.emergencyInterventions"
                        [
                            let patientWith age weight =
                                PatientData.create
                                    Department.unknown
                                    [||]
                                    UnknownGender
                                    (age |> Option.map AgeValue.fromDays)
                                    weight
                                    Height.unknown
                                    None

                            let weight = WeightAtDate.undated (WeightValue.weightInKg 32m)

                            test "takes the age in days and the measured weight" {
                                Weight.create [ weight ] None [] (Some(Measured weight))
                                |> patientWith (Some 3650<day>)
                                |> PatientData.emergencyInterventions
                                |> Expect.equal "the interventions" (calculate (Some 3650<day>) (Some 32m<kg>))
                            }

                            test "takes the estimated weight when no weight is measured" {
                                Weight.create [] None [] (Some(Estimated weight))
                                |> patientWith None
                                |> PatientData.emergencyInterventions
                                |> Expect.equal "the interventions" (calculate None (Some 32m<kg>))
                            }

                            test "gives no interventions for a patient without an age and a weight" {
                                PatientData.unknown
                                |> PatientData.emergencyInterventions
                                |> Expect.isEmpty "the interventions"
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


        let testFold () =
            let maximize = MinMax.foldMaximize
            let minimize = MinMax.foldMinimize

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
                        "fold"
                        [
                            test "an empty list folds to the empty range" {
                                [] |> MinMax.foldMinimize |> Expect.equal "minimize" MinMax.empty
                                [] |> MinMax.foldMaximize |> Expect.equal "maximize" MinMax.empty
                            }

                            test "a range without limits keeps both current limits" {
                                [ MinMax.createInclIncl mg10 mg30; MinMax.empty ]
                                |> MinMax.foldMinimize
                                |> Expect.equal "minimize" (MinMax.createInclIncl mg10 mg30)

                                [ MinMax.createInclIncl mg10 mg30; MinMax.empty ]
                                |> MinMax.foldMaximize
                                |> Expect.equal "maximize" (MinMax.createInclIncl mg10 mg30)
                            }

                            test "a range with only a minimum keeps the current maximum" {
                                [ MinMax.createInclIncl mg10 mg30; MinMax.empty |> MinMax.setMin mgIncl20 ]
                                |> MinMax.foldMinimize
                                |> Expect.equal "minimize" (MinMax.createInclIncl mg20 mg30)

                                [ MinMax.createInclIncl mg20 mg30; MinMax.empty |> MinMax.setMin mgIncl10 ]
                                |> MinMax.foldMaximize
                                |> Expect.equal "maximize" (MinMax.createInclIncl mg10 mg30)
                            }

                            test "a range with only a maximum keeps the current minimum" {
                                [ MinMax.createInclIncl mg10 mg30; MinMax.empty |> MinMax.setMax mgIncl20 ]
                                |> MinMax.foldMinimize
                                |> Expect.equal "minimize" (MinMax.createInclIncl mg10 mg20)

                                [ MinMax.createInclIncl mg10 mg30; MinMax.empty |> MinMax.setMax mgIncl40 ]
                                |> MinMax.foldMaximize
                                |> Expect.equal "maximize" (MinMax.createInclIncl mg10 mg40)
                            }

                            test "minimize takes the largest minimum and the smallest maximum" {
                                [ MinMax.createInclIncl mg10 mg40; MinMax.createInclIncl mg20 mg30 ]
                                |> MinMax.foldMinimize
                                |> Expect.equal "narrowest" (MinMax.createInclIncl mg20 mg30)
                            }

                            test "maximize takes the smallest minimum and the largest maximum" {
                                [ MinMax.createInclIncl mg20 mg30; MinMax.createInclIncl mg10 mg40 ]
                                |> MinMax.foldMaximize
                                |> Expect.equal "widest" (MinMax.createInclIncl mg10 mg40)
                            }
                        ]

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

            let kinds =
                [
                    Department.adultICU
                    Department.pediatricICU
                    Department.neonatalICU
                    Department.adultDepartment
                    Department.pediatricDepartment
                    Department.outpatientClinic
                    Department.otherDepartment
                ]

            let deps = (fun _ -> Department.unknown) :: kinds

            let tests =
                testList
                    "Department"
                    [
                        let name = "Test"

                        for f in kinds do
                            let kind = f name

                            test $"a blank name in the Dto is an unknown department %A{kind}" {
                                for blank in [ ""; "  " ] do
                                    blank
                                    |> f
                                    |> Department.Dto.toDto
                                    |> Department.Dto.fromDto
                                    |> Expect.equal "unknown" (Ok Department.unknown)
                            }

                        for f in deps do
                            test $"comes back from its Dto with a name {name |> f}" {
                                name
                                |> f
                                |> Department.Dto.toDto
                                |> Department.Dto.fromDto
                                |> Expect.equal "comes back with the name" (Ok(name |> f))
                            }

                        test "a Dto without a field set is an unknown department" {
                            Department.Dto.dto ()
                            |> Department.Dto.fromDto
                            |> Expect.equal "unknown" (Ok Department.unknown)
                        }

                        test "a plain name in the Dto is a department of a kind that is not known" {
                            let dto = Department.Dto.dto ()
                            dto.OtherDepartment <- Some "NEO"

                            dto
                            |> Department.Dto.fromDto
                            |> Expect.equal "the name" (Ok(Department.otherDepartment "NEO"))
                        }

                        test "a name in the Dto is trimmed, so the rules match it" {
                            let dto = Department.Dto.dto ()
                            dto.OtherDepartment <- Some " ICK "

                            dto
                            |> Department.Dto.fromDto
                            |> Expect.equal "the trimmed name" (Ok(Department.otherDepartment "ICK"))
                        }

                        test "a Dto with two fields set is refused" {
                            let dto = Department.Dto.dto ()
                            dto.OtherDepartment <- Some "NEO"
                            dto.NeonatalICU <- Some "NICU"

                            dto |> Department.Dto.fromDto |> Expect.isError "two departments"
                        }

                        test "a department whose kind is not known has the string of its name after one word" {
                            Department.otherDepartment "ICK"
                            |> Department.toString
                            |> Expect.equal "the string" "Department ICK"
                        }

                        test "an outpatient clinic has its name and its kind" {
                            let clinic = Department.outpatientClinic "Poli Kinderen"

                            clinic |> Department.name |> Expect.equal "the name" (Some "Poli Kinderen")

                            clinic
                            |> Department.toString
                            |> Expect.equal "the string" "Outpatient Clinic Poli Kinderen"
                        }

                        test "the name of a department whose kind is not known is its own; none when blank" {
                            for dep, exp in
                                [
                                    Department.otherDepartment "ICK", Some "ICK"
                                    Department.otherDepartment "", None
                                    Department.otherDepartment "  ", None
                                ] do
                                dep |> Department.name |> Expect.equal "the name" exp
                        }

                    ]


        module PatientDataTests =

            let eGfr x = x * 1.<mL / min / normalM2>

            let measuredOn = DateTime(2026, 1, 15)

            let weight = WeightAtDate.create (Some measuredOn) (WeightValue.weightInKg 32m)

            let height = HeightAtDate.create (Some measuredOn) (HeightValue.heightInCm 140m)

            let renalFunctions =
                [
                    EGFR Above50, "egfr:50:"
                    EGFR From30To50, "egfr:30:50"
                    EGFR From10To30, "egfr:10:30"
                    EGFR UpTo10, "egfr::10"
                    IntermittentHemodialysis, "intermittent-hemodialysis"
                    ContinuousHemodialysis, "continuous-hemodialysis"
                    PeritonealDialysis, "peritoneal-dialysis"
                ]

            let single unit (v: decimal) = v |> BigRational.fromDecimal |> ValueUnit.singleWithUnit unit

            let withAge age dat : PatientData = { dat with Age = Some age }

            let withWeight calc dat : PatientData = { dat with Weight = Weight.create [] None [] (Some calc) }

            let withHeight calc dat : PatientData = { dat with Height = Height.create [] None (Some calc) }

            /// Patient data with an age, a measured weight, an estimated height, their body surface area,
            /// both ages of a neonate, a location, a department, an access and a renal function.
            let complete =
                { PatientData.create
                      (Department.pediatricICU "ICK")
                      [| "sepsis" |]
                      Female
                      (Some(AgeValue.fromDays 25<day>))
                      (Weight.create [ weight ] None [] (Some(Measured weight)))
                      (Height.create [ height ] None (Some(Estimated height)))
                      (Some(AgeWeeksDays.create 26<week> 3<day>)) with
                    Location = Some "UMCU"
                    PostMenstrualAge = Some(AgeWeeksDays.create 30<week> 0<day>)
                    Access = [ CVL; EnteralTube ]
                    RenalFunction = Some(EGFR From10To30)
                }
                |> PatientData.calculateBodySurfaceArea

            let identified =
                BirthDate.create 2016<year> 3<month> 15<day>
                |> PatientIdentity.create "p-1" "Test Patient"
                |> Some
                |> fun idn -> Patient.create idn complete

            let belowMinimum = Error PatientDataError.NoAgeOrMeasuredWeightAndHeight

            /// An age of a number of years.
            let years y = AgeValue.create (Some y) None None None |> Some

            /// Creates patient data with only an age.
            let createAt age =
                PatientData.create Department.unknown [||] UnknownGender age Weight.unknown Height.unknown None

            /// A patient data Dto with an age and the mark of the user.
            let dtoAt age marked =
                let dto = PatientData.Dto.dto ()
                dto.Age <- age |> Option.map AgeValue.Dto.toDto
                dto.MarkedAdult <- marked
                dto

            /// Reads a patient data Dto that is valid.
            let read dto =
                dto
                |> PatientData.Dto.fromDto readAt None
                |> Expect.wantOk "the patient data Dto reads"

            /// Patient data at an age with the mark of the user, read from its Dto.
            let markedAt age = dtoAt (Some age) true |> read


            /// A dated weight Dto of a number of kg at a date.
            let weightDto (kg: decimal) date =
                let dto = WeightAtDate.Dto.dto ()
                dto.DateTime <- Some date
                dto.WeightValue.Weight <- kg
                dto

            /// A dated height Dto of a number of cm at a date.
            let heightDto (cm: decimal) date =
                let dto = HeightAtDate.Dto.dto ()
                dto.DateTime <- Some date
                dto.HeightValue.Height <- cm
                dto.HeightValue.HeightInMeter <- false
                dto

            let later = readAt.AddDays 1.

            let weights =
                Weight.create [ weight ] (Some(WeightValue.weightInKg 3m)) [ weight ] (Some(Estimated weight))

            let heights = Height.create [ height ] (Some(HeightValue.heightInCm 50m)) (Some(Measured height))


            let tests =
                testList
                    "patient data"
                    [

                        testList
                            "Access"
                            [
                                test "every access reads back from its string" {
                                    for a in [ PVL; CVL; EnteralTube ] do
                                        a |> Access.toString |> Access.tryFromString |> Expect.equal $"%A{a}" (Some a)
                                }

                                test "the string any is no access of a patient" {
                                    "any" |> Access.tryFromString |> Expect.isNone "any is a value of a rule only"
                                }
                            ]

                        testList
                            "RenalFunction"
                            [
                                test "every renal function has its string" {
                                    for r, s in renalFunctions do
                                        r |> RenalFunction.toString |> Expect.equal $"%A{r}" s
                                }

                                test "every renal function reads back from its string" {
                                    for r, s in renalFunctions do
                                        s |> RenalFunction.tryFromString |> Expect.equal s (Some r)
                                }

                                test "an eGFR that is no band does not read" {
                                    "egfr:20:40" |> RenalFunction.tryFromString |> Expect.isNone "no band"
                                }
                            ]

                        testList
                            "EGFRBand.ofEGFR"
                            [
                                test "a band includes its maximum" {
                                    for x, band in
                                        [
                                            120., Above50
                                            50.1, Above50
                                            50., From30To50
                                            30.1, From30To50
                                            30., From10To30
                                            10.1, From10To30
                                            10., UpTo10
                                            0., UpTo10
                                        ] do
                                        x |> eGfr |> EGFRBand.ofEGFR |> Expect.equal $"an eGFR of %f{x}" (Some band)
                                }

                                test "an eGFR below zero has no band" {
                                    -1. |> eGfr |> EGFRBand.ofEGFR |> Expect.isNone "below zero"
                                }

                                test "an eGFR that is no number has no band" {
                                    nan |> eGfr |> EGFRBand.ofEGFR |> Expect.isNone "no number"
                                }

                                test "an infinite eGFR has no band" {
                                    infinity |> eGfr |> EGFRBand.ofEGFR |> Expect.isNone "infinite"
                                    -infinity |> eGfr |> EGFRBand.ofEGFR |> Expect.isNone "minus infinite"
                                }
                            ]

                        testList
                            "Source"
                            [
                                test "the value is the same measured or estimated" {
                                    Measured 1 |> Source.value |> Expect.equal "measured" 1
                                    Estimated 1 |> Source.value |> Expect.equal "estimated" 1
                                }

                                test "only a measured value is measured" {
                                    Measured 1 |> Source.isMeasured |> Expect.isTrue "measured"
                                    Estimated 1 |> Source.isMeasured |> Expect.isFalse "estimated"
                                }
                            ]

                        testList
                            "Weight and Height"
                            [
                                test "the normal weight and height of an age category are estimates" {
                                    let w = Child |> Weight.fromAgeType Male
                                    let h = Child |> Height.fromAgeType Male

                                    w.Calculation
                                    |> Option.map Source.isMeasured
                                    |> Expect.equal "weight" (Some false)
                                    h.Calculation
                                    |> Option.map Source.isMeasured
                                    |> Expect.equal "height" (Some false)

                                    w.Calculation
                                    |> Option.map (Source.value >> _.Date)
                                    |> Expect.equal "weight without a date" (Some None)
                                    h.Calculation
                                    |> Option.map (Source.value >> _.Date)
                                    |> Expect.equal "height without a date" (Some None)
                                }

                                test "a measured and an estimated weight read back from the Dto" {
                                    for calc in [ Measured weight; Estimated weight ] do
                                        let w = Weight.create [ weight ] None [] (Some calc)
                                        w
                                        |> Weight.Dto.toDto
                                        |> Weight.Dto.fromDto readAt
                                        |> Expect.equal $"%A{calc}" (Ok w)
                                }

                                test "a measured and an estimated height read back from the Dto" {
                                    for calc in [ Measured height; Estimated height ] do
                                        let h = Height.create [ height ] None (Some calc)
                                        h
                                        |> Height.Dto.toDto
                                        |> Height.Dto.fromDto readAt
                                        |> Expect.equal $"%A{calc}" (Ok h)
                                }

                                test "only a measured calculation weight or height is measured" {
                                    let w calc = Weight.create [] None [] calc
                                    let h calc = Height.create [] None calc

                                    [ Some(Measured weight); Some(Estimated weight); None ]
                                    |> List.map (w >> Weight.isMeasured)
                                    |> Expect.equal "measured, estimated, none" [ true; false; false ]

                                    [ Some(Measured height); Some(Estimated height); None ]
                                    |> List.map (h >> Height.isMeasured)
                                    |> Expect.equal "measured, estimated, none" [ true; false; false ]
                                }

                                test "a date up to and including the date passed in reads" {
                                    for date in [ measuredOn; readAt ] do
                                        weightDto 32m date
                                        |> WeightAtDate.Dto.fromDto readAt
                                        |> Expect.isOk $"weight at %A{date}"
                                        heightDto 140m date
                                        |> HeightAtDate.Dto.fromDto readAt
                                        |> Expect.isOk $"height at %A{date}"
                                }

                                test "a date after the date passed in is an error" {
                                    weightDto 32m later
                                    |> WeightAtDate.Dto.fromDto readAt
                                    |> Expect.isError "weight"
                                    heightDto 140m later
                                    |> HeightAtDate.Dto.fromDto readAt
                                    |> Expect.isError "height"
                                }

                                test "the time of day is not looked at" {
                                    let noon = readAt.Date.AddHours 12.

                                    weightDto 32m (readAt.Date.AddHours 23.)
                                    |> WeightAtDate.Dto.fromDto noon
                                    |> Expect.isOk "later on the same day"

                                    weightDto 32m (readAt.Date.AddDays 1.)
                                    |> WeightAtDate.Dto.fromDto (readAt.Date.AddHours 23.)
                                    |> Expect.isError "midnight of the next day"
                                }

                                test "weights and heights read back from their Dto" {
                                    weights
                                    |> Weight.Dto.toDto
                                    |> Weight.Dto.fromDto readAt
                                    |> Expect.equal "weights" (Ok weights)
                                    heights
                                    |> Height.Dto.toDto
                                    |> Height.Dto.fromDto readAt
                                    |> Expect.equal "heights" (Ok heights)
                                }

                                test "a weight outside the bounds is an error, wherever it is" {
                                    let tooHeavy = weightDto 300m measuredOn

                                    let inCalculation = weights |> Weight.Dto.toDto
                                    inCalculation.Calculation <- Some tooHeavy

                                    let inActual = weights |> Weight.Dto.toDto
                                    inActual.Actual <- [ tooHeavy ]

                                    let inAdmission = weights |> Weight.Dto.toDto
                                    inAdmission.Admission <- [ tooHeavy ]

                                    let inBirth = weights |> Weight.Dto.toDto
                                    inBirth.Birth <- Some tooHeavy.WeightValue

                                    for name, dto in
                                        [
                                            "calculation", inCalculation
                                            "actual", inActual
                                            "admission", inAdmission
                                            "birth", inBirth
                                        ] do
                                        dto |> Weight.Dto.fromDto readAt |> Expect.isError name
                                }

                                test "a height outside the bounds is an error, wherever it is" {
                                    let tooTall = heightDto 400m measuredOn

                                    let inCalculation = heights |> Height.Dto.toDto
                                    inCalculation.Calculation <- Some tooTall

                                    let inActual = heights |> Height.Dto.toDto
                                    inActual.Actual <- [ tooTall ]

                                    let inBirth = heights |> Height.Dto.toDto
                                    inBirth.Birth <- Some tooTall.HeightValue

                                    for name, dto in
                                        [ "calculation", inCalculation; "actual", inActual; "birth", inBirth ] do
                                        dto |> Height.Dto.fromDto readAt |> Expect.isError name
                                }

                                test "a weight dated after the date passed in is an error" {
                                    let dto = weights |> Weight.Dto.toDto
                                    dto.Calculation <- Some(weightDto 32m later)

                                    dto |> Weight.Dto.fromDto readAt |> Expect.isError "dated tomorrow"
                                }

                                test "weights without any value read as no weights" {
                                    Weight.unknown
                                    |> Weight.Dto.toDto
                                    |> Weight.Dto.fromDto readAt
                                    |> Expect.equal "nothing known" (Ok Weight.unknown)
                                }

                                test "a weight or height without a date reads as an undated value" {
                                    let w = WeightAtDate.Dto.dto ()
                                    w.WeightValue.Weight <- 32m

                                    let h = HeightAtDate.Dto.dto ()
                                    h.HeightValue.Height <- 140m
                                    h.HeightValue.HeightInMeter <- false

                                    w
                                    |> WeightAtDate.Dto.fromDto readAt
                                    |> Expect.equal "weight" (Ok(WeightAtDate.undated weight.Weight))

                                    h
                                    |> HeightAtDate.Dto.fromDto readAt
                                    |> Expect.equal "height" (Ok(HeightAtDate.undated height.Height))
                                }

                                test "undated weights and heights read back from their Dto" {
                                    let w = WeightAtDate.undated weight.Weight
                                    let h = HeightAtDate.undated height.Height
                                    let weights = Weight.create [ weight; w ] None [ w ] (Some(Measured w))
                                    let heights = Height.create [ height; h ] None (Some(Estimated h))

                                    weights
                                    |> Weight.Dto.toDto
                                    |> Weight.Dto.fromDto readAt
                                    |> Expect.equal "weights" (Ok weights)

                                    heights
                                    |> Height.Dto.toDto
                                    |> Height.Dto.fromDto readAt
                                    |> Expect.equal "heights" (Ok heights)
                                }

                                test "a dated value prints its date, an undated value prints only the value" {
                                    let ws = weight.Weight |> WeightValue.toString 2
                                    let hs = height.Height |> HeightValue.toString 2
                                    // toString writes the date in the current culture.
                                    let ds = measuredOn.ToString "dd-MMM-yy"

                                    weight
                                    |> WeightAtDate.toString 2
                                    |> Expect.equal "dated weight" $"%s{ws} (%s{ds})"

                                    WeightAtDate.undated weight.Weight
                                    |> WeightAtDate.toString 2
                                    |> Expect.equal "undated weight" ws

                                    height
                                    |> HeightAtDate.toString 2
                                    |> Expect.equal "dated height" $"%s{hs} (%s{ds})"

                                    HeightAtDate.undated height.Height
                                    |> HeightAtDate.toString 2
                                    |> Expect.equal "undated height" hs
                                }

                                test "the lists go from the latest date to the earliest, the undated values last" {
                                    let ws = weight.Weight |> WeightValue.toString 2
                                    let hs = height.Height |> HeightValue.toString 2
                                    // toString writes the date in the current culture.
                                    let early = measuredOn.ToString "dd-MMM-yy"
                                    let late = readAt.ToString "dd-MMM-yy"

                                    Weight.create
                                        [
                                            WeightAtDate.undated weight.Weight
                                            weight
                                            WeightAtDate.create (Some readAt) weight.Weight
                                        ]
                                        None
                                        []
                                        None
                                    |> Weight.actualToString 2
                                    |> Expect.equal "weights" [ $"%s{ws} (%s{late})"; $"%s{ws} (%s{early})"; ws ]

                                    Height.create
                                        [
                                            HeightAtDate.undated height.Height
                                            height
                                            HeightAtDate.create (Some readAt) height.Height
                                        ]
                                        None
                                        None
                                    |> Height.actualToString 2
                                    |> Expect.equal "heights" [ $"%s{hs} (%s{late})"; $"%s{hs} (%s{early})"; hs ]
                                }

                                test "a Dto that does not say estimated reads as measured" {
                                    let dto = Weight.Dto.dto ()
                                    dto.Calculation <- weight |> WeightAtDate.Dto.toDto |> Some

                                    dto
                                    |> Weight.Dto.fromDto readAt
                                    |> Result.map _.Calculation
                                    |> Expect.equal "measured" (Ok(Some(Measured weight)))
                                }
                            ]

                        testList
                            "PatientData"
                            [
                                test
                                    "the unknown patient data has no location, no mark as an adult, no access and no renal function" {
                                    let dat = PatientData.unknown

                                    dat.Location |> Expect.isNone "location"
                                    dat.AdultMark
                                    |> Expect.equal "no mark as an adult" (AdultMark.MarkableAdult false)
                                    dat.Access |> Expect.isEmpty "access"
                                    dat.RenalFunction |> Expect.isNone "renal function"
                                }

                                testList
                                    "single values"
                                    [
                                        test "the unknown patient data has no single value" {
                                            let dat = PatientData.unknown

                                            dat |> PatientData.ageInDays |> Expect.isNone "age"
                                            dat |> PatientData.weightInKg |> Expect.isNone "weight"
                                            dat |> PatientData.heightInCm |> Expect.isNone "height"
                                            dat |> PatientData.gestationalAgeInDays |> Expect.isNone "gestational age"
                                            dat
                                            |> PatientData.postMenstrualAgeInDays
                                            |> Expect.isNone "post-menstrual age"
                                            dat |> PatientData.bodySurfaceArea |> Expect.isNone "body surface area"
                                        }

                                        test "the age is in days, a year of 365 days" {
                                            PatientData.unknown
                                            |> withAge AgeValue.ten
                                            |> PatientData.ageInDays
                                            |> Expect.equal "ten years" (Some(single Units.Time.day 3650m))
                                        }

                                        test "the gestational and the post-menstrual age are in days" {
                                            complete
                                            |> PatientData.gestationalAgeInDays
                                            |> Expect.equal "26 weeks and 3 days" (Some(single Units.Time.day 185m))

                                            complete
                                            |> PatientData.postMenstrualAgeInDays
                                            |> Expect.equal "30 weeks" (Some(single Units.Time.day 210m))
                                        }

                                        test "the weight is the calculation weight in kg, measured or estimated" {
                                            for calc in [ Measured weight; Estimated weight ] do
                                                PatientData.unknown
                                                |> withWeight calc
                                                |> PatientData.weightInKg
                                                |> Expect.equal $"%A{calc}" (Some(single Units.Weight.kiloGram 32m))
                                        }

                                        test "a weight in gram is given in kg" {
                                            let w =
                                                WeightAtDate.create (Some measuredOn) (WeightValue.weightInGram 750m)

                                            PatientData.unknown
                                            |> withWeight (Measured w)
                                            |> PatientData.weightInKg
                                            |> Expect.equal "750 gram" (Some(single Units.Weight.kiloGram 0.75m))
                                        }

                                        test "the height is the calculation height in cm, measured or estimated" {
                                            for calc in [ Measured height; Estimated height ] do
                                                PatientData.unknown
                                                |> withHeight calc
                                                |> PatientData.heightInCm
                                                |> Expect.equal $"%A{calc}" (Some(single Units.Height.centiMeter 140m))
                                        }

                                        test
                                            "the body surface area is the Du Bois area of the weight and the height, rounded to 2 significant digits" {
                                            let expected =
                                                Calculations.BSA.calcDuBois (Some 2) 32m<kg> 140m<cm>
                                                |> decimal
                                                |> single Units.BSA.m2

                                            complete
                                            |> PatientData.bodySurfaceArea
                                            |> Expect.equal "32 kg and 140 cm" (Some expected)
                                        }

                                        test "there is no body surface area without a height" {
                                            PatientData.unknown
                                            |> withWeight (Measured weight)
                                            |> PatientData.calculateBodySurfaceArea
                                            |> PatientData.bodySurfaceArea
                                            |> Expect.isNone "a weight alone"
                                        }

                                        test "the body surface area is the one set, not calculated again" {
                                            { complete with BodySurfaceArea = Some 1m<bsa> }
                                            |> PatientData.bodySurfaceArea
                                            |> Expect.equal "the value set" (Some(single Units.BSA.m2 1m))
                                        }

                                        test
                                            "patient data of an age type has the body surface area of its normal values" {
                                            let dat = Adult |> PatientData.fromAgeType Male

                                            dat.BodySurfaceArea
                                            |> Expect.equal
                                                "the normal weight and height"
                                                (dat
                                                 |> PatientData.calcBsa (fun _ -> Calculations.BSA.calcDuBois (Some 2)))
                                        }
                                    ]

                                testList
                                    "minimum data"
                                    [
                                        test "the unknown patient data is below the minimum" {
                                            PatientData.unknown
                                            |> PatientData.checkMinimumData
                                            |> Expect.equal "nothing known" belowMinimum
                                        }

                                        test "an age is enough" {
                                            let dat = PatientData.unknown |> withAge AgeValue.ten

                                            dat |> PatientData.checkMinimumData |> Expect.equal "an age alone" (Ok dat)
                                        }

                                        test "a measured weight with a measured height is enough" {
                                            let dat =
                                                PatientData.unknown
                                                |> withWeight (Measured weight)
                                                |> withHeight (Measured height)

                                            dat |> PatientData.checkMinimumData |> Expect.equal "both measured" (Ok dat)
                                        }

                                        test "an estimated weight or height is not enough" {
                                            for w, h in
                                                [
                                                    Estimated weight, Measured height
                                                    Measured weight, Estimated height
                                                    Estimated weight, Estimated height
                                                ] do
                                                PatientData.unknown
                                                |> withWeight w
                                                |> withHeight h
                                                |> PatientData.checkMinimumData
                                                |> Expect.equal $"%A{w} with %A{h}" belowMinimum
                                        }

                                        test "a measured weight without a height is not enough" {
                                            PatientData.unknown
                                            |> withWeight (Measured weight)
                                            |> PatientData.checkMinimumData
                                            |> Expect.equal "a weight alone" belowMinimum
                                        }

                                        test "the mark as an adult is enough without an age" {
                                            let dat =
                                                { PatientData.unknown with AdultMark = AdultMark.MarkableAdult true }

                                            dat
                                            |> PatientData.checkMinimumData
                                            |> Expect.equal "marked, no age" (Ok dat)
                                        }

                                        test "the mark as an adult is ignored below 16 years" {
                                            let dat = markedAt AgeValue.ten

                                            dat.AdultMark |> Expect.equal "not an adult" AdultMark.NotAdult

                                            dat
                                            |> PatientData.checkMinimumData
                                            |> Expect.equal "the age is enough" (Ok dat)
                                        }

                                        test "the mark as an adult is accepted from 16 years" {
                                            let dat = markedAt AgeValue.sixteen

                                            dat.AdultMark |> Expect.equal "marked" (AdultMark.MarkableAdult true)

                                            dat
                                            |> PatientData.checkMinimumData
                                            |> Expect.equal "marked at sixteen years" (Ok dat)
                                        }
                                    ]

                                testList
                                    "the adult mark and the need for a gestational age"
                                    [
                                        test "create sets the mark from the age, without a mark of the user" {
                                            [ None; years 5<year>; years 17<year>; years 18<year> ]
                                            |> List.map (fun age -> (createAt age).AdultMark)
                                            |> Expect.equal
                                                "the marks"
                                                [
                                                    AdultMark.MarkableAdult false
                                                    AdultMark.NotAdult
                                                    AdultMark.MarkableAdult false
                                                    AdultMark.Adult
                                                ]
                                        }

                                        test "markAsAdult sets the mark only when the age allows it" {
                                            [ None; years 15<year>; years 17<year>; years 18<year> ]
                                            |> List.map (fun age ->
                                                (createAt age |> PatientData.markAsAdult true).AdultMark
                                            )
                                            |> Expect.equal
                                                "the marks"
                                                [
                                                    AdultMark.MarkableAdult true
                                                    AdultMark.NotAdult
                                                    AdultMark.MarkableAdult true
                                                    AdultMark.Adult
                                                ]
                                        }

                                        test "fromDto sets the mark from the age and the mark of the user" {
                                            [
                                                None, true
                                                None, false
                                                years 17<year>, true
                                                years 16<year>, false
                                                years 18<year>, false
                                            ]
                                            |> List.map (fun (age, marked) -> (dtoAt age marked |> read).AdultMark)
                                            |> Expect.equal
                                                "the marks"
                                                [
                                                    AdultMark.MarkableAdult true
                                                    AdultMark.MarkableAdult false
                                                    AdultMark.MarkableAdult true
                                                    AdultMark.MarkableAdult false
                                                    AdultMark.Adult
                                                ]
                                        }

                                        test "fromDto accepts a mark below 16 years and ignores it" {
                                            dtoAt (years 15<year>) true
                                            |> PatientData.Dto.fromDto readAt None
                                            |> Result.map _.AdultMark
                                            |> Expect.equal "not an adult" (Ok AdultMark.NotAdult)
                                        }

                                        test "fromDto does not read the mark of the Dto" {
                                            let dto = dtoAt (years 5<year>) false
                                            dto.AdultMark <- AdultMark.Adult

                                            (dto |> read).AdultMark |> Expect.equal "from the age" AdultMark.NotAdult
                                        }

                                        test "toDto marks only a patient that may be marked and is marked" {
                                            [
                                                AdultMark.NotAdult
                                                AdultMark.MarkableAdult false
                                                AdultMark.MarkableAdult true
                                                AdultMark.Adult
                                            ]
                                            |> List.map (fun mark ->
                                                let dto =
                                                    { PatientData.unknown with AdultMark = mark }
                                                    |> PatientData.Dto.toDto
                                                dto.MarkedAdult, dto.AdultMark
                                            )
                                            |> Expect.equal
                                                "the user's marks and the marks"
                                                [
                                                    false, AdultMark.NotAdult
                                                    false, AdultMark.MarkableAdult false
                                                    true, AdultMark.MarkableAdult true
                                                    false, AdultMark.Adult
                                                ]
                                        }

                                        test "fromDto sets the need from the age and toDto writes it" {
                                            [
                                                None
                                                Some(AgeValue.fromDays 10<day>)
                                                Some(AgeValue.fromDays 60<day>)
                                                years 2<year>
                                            ]
                                            |> List.map (fun age ->
                                                (dtoAt age false |> read |> PatientData.Dto.toDto).GestationalAgeNeed
                                            )
                                            |> Expect.equal
                                                "the needs"
                                                [
                                                    GestationalAgeNeed.NoGestationalAge
                                                    GestationalAgeNeed.RequiredGestationalAge
                                                    GestationalAgeNeed.OptionalGestationalAge
                                                    GestationalAgeNeed.NoGestationalAge
                                                ]
                                        }

                                        test "fromDto drops a gestational age that is not used" {
                                            let ga = AgeWeeksDays.create 30<week> 0<day>

                                            [ Some(AgeValue.fromDays 10<day>); years 2<year> ]
                                            |> List.map (fun age ->
                                                let dto = dtoAt age false
                                                dto.GestationalAge <- ga |> AgeWeeksDays.Dto.toDto |> Some
                                                (dto |> read).GestationalAge
                                            )
                                            |> Expect.equal "kept, dropped" [ Some ga; None ]
                                        }

                                        test "fromAgeType sets the mark and the need from the age" {
                                            [ NewBorn; Adult ]
                                            |> List.map (
                                                PatientData.fromAgeType Male
                                                >> fun dat -> dat.AdultMark, dat.GestationalAgeNeed
                                            )
                                            |> Expect.equal
                                                "the mark and the need"
                                                [
                                                    AdultMark.NotAdult, GestationalAgeNeed.RequiredGestationalAge
                                                    AdultMark.Adult, GestationalAgeNeed.NoGestationalAge
                                                ]
                                        }
                                    ]

                                testList
                                    "Dto"
                                    [
                                        test "patient data reads back from its Dto" {
                                            for dat in
                                                [
                                                    complete
                                                    PatientData.unknown
                                                    { PatientData.unknown with
                                                        AdultMark = AdultMark.MarkableAdult true
                                                    }
                                                    NewBorn |> PatientData.fromAgeType Male
                                                    Adult |> PatientData.fromAgeType Female
                                                ] do
                                                dat
                                                |> PatientData.Dto.toDto
                                                |> PatientData.Dto.fromDto readAt None
                                                |> Expect.equal "the same patient data" (Ok dat)
                                        }

                                        test "patient data with a weight outside the bounds is an error" {
                                            let dto = complete |> PatientData.Dto.toDto
                                            dto.Weight.Calculation <- Some(weightDto 300m measuredOn)

                                            dto |> PatientData.Dto.fromDto readAt None |> Expect.isError "300 kg"
                                        }

                                        test "a Dto with an unknown access is an error" {
                                            let dto = complete |> PatientData.Dto.toDto
                                            dto.Access <- [| "pvl"; "any" |]

                                            dto
                                            |> PatientData.Dto.fromDto readAt None
                                            |> Expect.isError "any is no access of a patient"
                                        }

                                        test "a Dto with two unknown accesses reports the first" {
                                            let dto = complete |> PatientData.Dto.toDto
                                            dto.Access <- [| "any"; "xxx" |]

                                            let text = dto |> PatientData.Dto.fromDto readAt None |> sprintf "%A"

                                            text |> Expect.stringContains "the first" "any"
                                            text.Contains "xxx" |> Expect.isFalse "not the second"
                                        }

                                        test "a Dto with an unknown renal function is an error" {
                                            let dto = complete |> PatientData.Dto.toDto
                                            dto.RenalFunction <- Some "egfr:20:40"

                                            dto |> PatientData.Dto.fromDto readAt None |> Expect.isError "no band"
                                        }

                                        test "the ages are calculated, not read as stored" {
                                            let ga = AgeWeeksDays.create 30<week> 0<day>

                                            let read age =
                                                { PatientData.unknown with
                                                    Age = age
                                                    GestationalAge = Some ga
                                                    PostMenstrualAge = Some(AgeWeeksDays.create 25<week> 0<day>)
                                                }
                                                |> PatientData.Dto.toDto
                                                |> PatientData.Dto.fromDto readAt None
                                                |> Result.map (fun dat -> dat.GestationalAge, dat.PostMenstrualAge)

                                            [ Some(AgeValue.fromDays 70<day>); Some AgeValue.one; None ]
                                            |> List.map read
                                            |> Expect.equal
                                                "at 70 days 30 weeks gestational and 40 weeks post-menstrual age; at 1 year or without an age none"
                                                [
                                                    Ok(Some ga, Some(AgeWeeksDays.create 40<week> 0<day>))
                                                    Ok(None, None)
                                                    Ok(None, None)
                                                ]
                                        }

                                        test "the body surface area is calculated, not read as stored" {
                                            let dto = complete |> PatientData.Dto.toDto
                                            dto.BodySurfaceArea <- Some 9m

                                            dto
                                            |> PatientData.Dto.fromDto readAt None
                                            |> Result.map _.BodySurfaceArea
                                            |> Expect.equal "32 kg and 140 cm" (Ok complete.BodySurfaceArea)
                                        }

                                        test "a Dto without a weight has no body surface area" {
                                            let dto = complete |> PatientData.Dto.toDto
                                            dto.Weight.Calculation <- None

                                            dto
                                            |> PatientData.Dto.fromDto readAt None
                                            |> Result.map _.BodySurfaceArea
                                            |> Expect.equal "none" (Ok None)
                                        }

                                        test "a Dto whose access a serializer left null reads without an access" {
                                            let dto = PatientData.unknown |> PatientData.Dto.toDto
                                            dto.Access <- null

                                            dto
                                            |> PatientData.Dto.fromDto readAt None
                                            |> Result.map _.Access
                                            |> Expect.equal "no access" (Ok [])
                                        }
                                    ]
                            ]

                        testList
                            "Patient"
                            [
                                test "the body surface area is calculated from an estimated value too" {
                                    complete
                                    |> Patient.create None
                                    |> Patient.BSA.calcDuBois
                                    |> Expect.isSome "measured weight, estimated height"
                                }

                                test "a patient at a date has the body surface area of its weight and height" {
                                    { complete with BodySurfaceArea = None }
                                    |> Patient.create None
                                    |> Patient.atDate readAt
                                    |> _.Data.BodySurfaceArea
                                    |> Expect.equal "32 kg and 140 cm" complete.BodySurfaceArea
                                }

                                test "a patient reads back from its Dto, identified or anonymous" {
                                    for pat in [ identified; Patient.create None complete; Patient.unknown ] do
                                        pat
                                        |> Patient.Dto.toDto
                                        |> Patient.Dto.fromDto readAt None
                                        |> Expect.equal "the same patient" (Ok pat)
                                }

                                test "patient data that does not parse is an error" {
                                    let dto = identified |> Patient.Dto.toDto
                                    dto.Data.Access <- [| "any" |]

                                    dto
                                    |> Patient.Dto.fromDto readAt None
                                    |> Expect.isError "any is no access of a patient"
                                }

                                test "a patient with a height dated after the date passed in is an error" {
                                    let dto = identified |> Patient.Dto.toDto
                                    dto.Data.Height.Calculation <- Some(heightDto 140m later)

                                    dto |> Patient.Dto.fromDto readAt None |> Expect.isError "dated tomorrow"
                                }

                                test "a patient born more than 120 years before the date passed in is an error" {
                                    let dto = identified |> Patient.Dto.toDto
                                    dto.BirthDate <-
                                        BirthDate.create 1900<year> 1<month> 1<day> |> BirthDate.Dto.toDto |> Some

                                    dto |> Patient.Dto.fromDto readAt None |> Expect.isError "126 years"
                                }

                                test "a part of an identity is an error" {
                                    let dto = identified |> Patient.Dto.toDto
                                    dto.Name <- ""

                                    dto
                                    |> Patient.Dto.fromDto readAt None
                                    |> Expect.isError "an id and a birthdate without a name"
                                }
                            ]
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
                                    a
                                    |> AgeValue.getAgeInDays
                                    |> fun n -> n <= (Constants.daysInYear * 120 |> Conversions.dayFromInt)
                                | Error _ -> true
                        |> Generators.testProp "should never be > 120 years"
                    ]

        module BirthDateTests =

            let intGenerator s n = Gen.sample s n Arb.generate<int>

            /// Today at 10:00. A birth date tomorrow is then less than one day after today, so a check on the whole
            /// number of days would accept it.
            let today = DateTime(2026, 11, 18, 10, 0, 0)

            /// The birth date Dto of the date dt.
            let ofDate (dt: DateTime) = dt |> BirthDate.fromDate |> BirthDate.Dto.toDto


            let tests =
                testList
                    "BirthDate"
                    [
                        test "a birthdate is at most 120 years before the date passed in" {
                            let dto = BirthDate.create 1900<year> 1<month> 1<day> |> BirthDate.Dto.toDto

                            dto |> BirthDate.Dto.fromDto (DateTime(2010, 1, 1)) |> Expect.isOk "110 years"
                            dto
                            |> BirthDate.Dto.fromDto (DateTime(2026, 1, 1))
                            |> Expect.isError "126 years"
                        }

                        test "a birth date tomorrow is refused" {
                            today.AddDays 1.
                            |> ofDate
                            |> BirthDate.Dto.fromDto today
                            |> Expect.isError "tomorrow"
                        }

                        test "a birth date next year is refused" {
                            today.AddYears 1
                            |> ofDate
                            |> BirthDate.Dto.fromDto today
                            |> Expect.isError "next year"
                        }

                        test "a birth date today is accepted" {
                            today |> ofDate |> BirthDate.Dto.fromDto today |> Expect.isOk "today"
                        }

                        test "a birth date yesterday is accepted" {
                            today.AddDays -1.
                            |> ofDate
                            |> BirthDate.Dto.fromDto today
                            |> Expect.isOk "yesterday"
                        }

                        test "an invalid birth date is refused without an exception" {
                            BirthDate.create 2020<year> 2<month> 30<day>
                            |> BirthDate.Dto.toDto
                            |> BirthDate.Dto.fromDto today
                            |> Expect.isError "30 February does not exist"
                        }

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
                                    match dto |> BirthDate.Dto.fromDto readAt with
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
                            |> BirthDate.Dto.fromDto readAt
                            |> function
                                | Ok ymd1 ->

                                    ymd1
                                    |> BirthDate.Dto.toDto
                                    |> BirthDate.Dto.fromDto readAt
                                    |> function
                                        | Ok ymd2 -> ymd2 = ymd1
                                        | Error _ -> false
                                | Error _ -> true
                        |> testProperty "there and back again"

                        test "a year alone, or a year and a month, is no birthdate" {
                            let dto = BirthDate.Dto.dto ()
                            dto.Year <- 2016

                            let yearAlone = dto |> BirthDate.Dto.fromDto readAt |> Result.isError

                            dto.Month <- 3
                            let noDay = dto |> BirthDate.Dto.fromDto readAt |> Result.isError

                            dto.Day <- 15
                            let complete = dto |> BirthDate.Dto.fromDto readAt |> Result.isOk

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
                            dto.Year <- (readAt |> DateTime.addYears -121).Year
                            dto.Month <- 1
                            dto.Day <- 1

                            dto
                            |> BirthDate.Dto.fromDto readAt
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
                            dto.DateTime <- Some(readAt.AddDays 7)

                            dto
                            |> WeightAtDate.Dto.fromDto readAt
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

        module AgeAtCreationTests =

            let days n =
                n |> BigRational.fromInt |> ValueUnit.singleWithUnit Units.Time.day |> Some

            let birthDate =
                BirthDate.create (Conversions.yearFromInt 2016) (Conversions.monthFromInt 3) (Conversions.dayFromInt 15)

            let tenthBirthday = DateTime(2026, 3, 15)

            /// The age in days a patient is created with at a date.
            let createdAt at pat =
                (pat |> Patient.atDate at).Data.Age |> Option.map AgeValue.toValueUnit

            let tests =
                testList
                    "The age a patient is created with"
                    [
                        test "an identified patient gets the days of its birthdate at the date" {
                            let pat =
                                PatientData.unknown
                                |> Patient.create (PatientIdentity.create "p1" "Test" birthDate |> Some)

                            [ tenthBirthday; tenthBirthday.AddDays 1.0; tenthBirthday.AddYears 1 ]
                            |> List.map (fun at -> pat |> createdAt at)
                            |> Expect.equal "3652, 3653, 4017 days" [ days 3652; days 3653; days 4017 ]
                        }

                        test "an anonymous patient keeps its entered age, at any date" {
                            let pat = { PatientData.unknown with Age = Some AgeValue.ten } |> Patient.create None

                            [ tenthBirthday; tenthBirthday.AddYears 2 ]
                            |> List.map (fun at -> pat |> createdAt at)
                            |> Expect.allEqual "ten years" (days (AgeValue.ten |> AgeValue.getAgeInDays |> int))
                        }

                        test "an anonymous patient without an age has none" {
                            Patient.unknown |> createdAt tenthBirthday |> Expect.isNone "no days"
                        }
                    ]


        module AgeInDaysLivedTests =

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


            let identified birth =
                Patient.create (PatientIdentity.create "1" "test" birth |> Some) PatientData.unknown


            let tests =
                testList
                    "The age in days lived"
                    [
                        testList
                            "an identified patient"
                            [
                                test "born 28 Feb 2026, on 31 Mar 2026: 31 days, 1 month and 1 day" {
                                    bd 2026 2 28 |> daysAt (date 2026 3 31) |> Expect.equal "31 days" 31
                                    bd 2026 2 28
                                    |> ageAt (date 2026 3 31)
                                    |> Expect.equal "1 month 1 day" (0, 1, 0, 1)
                                }

                                test "born 1 Mar 2018, on 1 Mar 2028: 3653 days, 10 years and 3 days" {
                                    bd 2018 3 1 |> daysAt (date 2028 3 1) |> Expect.equal "3653 days" 3653
                                    bd 2018 3 1
                                    |> ageAt (date 2028 3 1)
                                    |> Expect.equal "10 years 3 days" (10, 0, 0, 3)
                                }

                                test "the age in days is the days from the birth date to the date, for every birth date" {
                                    [
                                        for b in datesOf (date 2000 1 1) (date 2026 3 31) do
                                            for dt in [ date 2026 3 31; date 2026 2 28; date 2028 2 29 ] do
                                                if
                                                    b <= dt && (b |> BirthDate.fromDate |> daysAt dt) <> (dt - b).Days
                                                then
                                                    b, dt
                                    ]
                                    |> Expect.isEmpty "the days lived"
                                }

                                test "on 29 Mar 2026 every age in days from 0 to 87 has a birth date" {
                                    datesOf (date 2026 1 1) (date 2026 3 29)
                                    |> List.map (BirthDate.fromDate >> daysAt (date 2026 3 29))
                                    |> List.sort
                                    |> Expect.equal "0 to 87" [ 0..87 ]
                                }

                                test "the age goes up exactly one day a day" {
                                    [ bd 2024 2 29; bd 2026 1 31; bd 2025 8 31; bd 2008 3 1 ]
                                    |> List.forall (fun b ->
                                        datesOf (date 2026 1 31) (date 2028 12 31)
                                        |> List.map (fun dt -> b |> daysAt dt)
                                        |> List.pairwise
                                        |> List.forall (fun (d1, d2) -> d2 = d1 + 1)
                                    )
                                    |> Expect.isTrue "one day a day"
                                }

                                test "the age value round trips through the age in days" {
                                    datesOf (date 2006 1 1) (date 2026 3 31)
                                    |> List.map BirthDate.fromDate
                                    |> List.filter (fun b ->
                                        let av = b |> AgeValue.fromBirthDate (date 2026 3 31)
                                        av |> AgeValue.getAgeInDays |> AgeValue.fromDays <> av
                                    )
                                    |> Expect.isEmpty "every age round trips"
                                }

                                test "the time of day does not count" {
                                    [ DateTime(2026, 3, 1, 0, 1, 0); DateTime(2026, 3, 1, 23, 59, 0) ]
                                    |> List.map (fun dt -> bd 2008 3 1 |> daysAt dt)
                                    |> Expect.allEqual "6574 days" 6574
                                }

                                test "a birth date after the date is refused" {
                                    (fun () -> bd 2026 3 2 |> AgeValue.fromBirthDate (date 2026 3 1) |> ignore)
                                    |> Expect.throwsT<ArgumentException> "refused"
                                }

                                test "born 1 Mar 2008, 18 years on 25 Feb 2026, 4 days before the birthday" {
                                    [ date 2026 2 24; date 2026 2 25 ]
                                    |> List.map (fun dt ->
                                        bd 2008 3 1
                                        |> AgeValue.fromBirthDate dt
                                        |> fun av -> AgeValue.Operators.(>=?) av AgeValue.eighteen
                                    )
                                    |> Expect.equal "under 18, then 18" [ false; true ]
                                }

                                test "18 years is reached 4 to 5 days before the 18th birthday" {
                                    datesOf (date 2000 1 1) (date 2008 12 31)
                                    |> List.map (fun b ->
                                        let first =
                                            datesOf (b.AddYears 17) (b.AddYears 18)
                                            |> List.find (fun dt ->
                                                b
                                                |> BirthDate.fromDate
                                                |> AgeValue.fromBirthDate dt
                                                |> fun av -> AgeValue.Operators.(>=?) av AgeValue.eighteen
                                            )

                                        (b.AddYears 18 - first).Days
                                    )
                                    |> List.distinct
                                    |> List.sort
                                    |> Expect.equal "4 or 5 days" [ 4; 5 ]
                                }

                                test "one year is reached on the birthday or a day before" {
                                    datesOf (date 2020 1 1) (date 2025 12 31)
                                    |> List.map (fun b ->
                                        let first =
                                            datesOf b (b.AddYears 1)
                                            |> List.find (fun dt ->
                                                b
                                                |> BirthDate.fromDate
                                                |> AgeValue.fromBirthDate dt
                                                |> fun av -> AgeValue.Operators.(>=?) av AgeValue.one
                                            )

                                        (b.AddYears 1 - first).Days
                                    )
                                    |> List.distinct
                                    |> List.sort
                                    |> Expect.equal "0 or 1 day" [ 0; 1 ]
                                }

                                test "an identified patient at a date has the days lived" {
                                    bd 2026 2 28
                                    |> identified
                                    |> Patient.atDate (date 2026 3 31)
                                    |> _.Data.Age
                                    |> Option.map (AgeValue.getAgeInDays >> int)
                                    |> Expect.equal "31 days" (Some 31)
                                }

                                test "an anonymous patient keeps the age as entered" {
                                    let age =
                                        AgeValue.create (Some 1<year>) (Some 2<month>) (Some 1<week>) (Some 3<day>)

                                    { Patient.unknown with Patient.Data.Age = Some age }
                                    |> Patient.atDate (date 2026 3 31)
                                    |> _.Data.Age
                                    |> Expect.equal "the parts as entered" (Some age)
                                }

                                test "an anonymous age in days counts a year 365, a month 30 and a week 7 days" {
                                    AgeValue.create (Some 1<year>) (Some 2<month>) (Some 1<week>) (Some 3<day>)
                                    |> AgeValue.getAgeInDays
                                    |> Expect.equal "435 days" 435<day>
                                }
                            ]

                        testList
                            "fromDays"
                            [
                                test "365 days is one year, 60 days two months" {
                                    [ 365; 60 ]
                                    |> List.map (fun d -> d * 1<day> |> AgeValue.fromDays |> AgeValue.getDef0)
                                    |> Expect.equal
                                        "1 year, 2 months"
                                        [ 1<year>, 0<month>, 0<week>, 0<day>; 0<year>, 2<month>, 0<week>, 0<day> ]
                                }
                            ]

                        testList
                            "fromValueUnit"
                            [
                                let days n =
                                    n |> BigRational.fromInt |> ValueUnit.singleWithUnit Units.Time.day

                                for n in [ 0; 400 ] do
                                    test $"%i{n} days gives the age value of %i{n} days" {
                                        days n
                                        |> AgeValue.fromValueUnit
                                        |> Expect.equal "as fromDays" (n * 1<day> |> AgeValue.fromDays)
                                    }

                                test "a time value in weeks is converted to days" {
                                    2N
                                    |> ValueUnit.singleWithUnit Units.Time.week
                                    |> AgeValue.fromValueUnit
                                    |> Expect.equal "14 days" (14<day> |> AgeValue.fromDays)
                                }

                                test "part of a day is rounded down" {
                                    (55N / 2N)
                                    |> ValueUnit.singleWithUnit Units.Time.day
                                    |> AgeValue.fromValueUnit
                                    |> Expect.equal "27 days" (27<day> |> AgeValue.fromDays)
                                }

                                test "back to the same time value" {
                                    days 400
                                    |> AgeValue.fromValueUnit
                                    |> AgeValue.toValueUnit
                                    |> Expect.equal "400 days" (days 400)
                                }

                                test "a value that is not a time raises" {
                                    fun () ->
                                        1N
                                        |> ValueUnit.singleWithUnit Units.Weight.kiloGram
                                        |> AgeValue.fromValueUnit
                                        |> ignore
                                    |> Expect.throwsT<System.ArgumentException> "not a time"
                                }
                            ]

                        testList
                            "The post-menstrual age of a patient"
                            [
                                let gest = Some(AgeWeeksDays.create 26<week> 0<day>)

                                let created =
                                    { PatientData.unknown with GestationalAge = gest }
                                    |> Patient.create (PatientIdentity.create "p1" "Test" (bd 2026 1 31) |> Some)
                                    |> Patient.atDate (date 2026 3 30)

                                let pmaDays (pat: Patient) = pat.Data.PostMenstrualAge |> Option.map AgeWeeksDays.toDays

                                test "identified, created: the gestational age plus the age in days" {
                                    created |> pmaDays |> Expect.equal "182 plus 58 days" (Some 240<day>)
                                }

                                test
                                    "under 1 year the gestational age plus the age in days the rules see, from 1 year none" {
                                    let pat =
                                        { PatientData.unknown with GestationalAge = gest }
                                        |> Patient.create (PatientIdentity.create "p1" "Test" (bd 2016 2 29) |> Some)

                                    let underOneYear age = AgeValue.Operators.(<?) age AgeValue.one

                                    [ 0..1500 ]
                                    |> List.map (float >> (date 2016 2 29).AddDays)
                                    |> List.filter (fun dt ->
                                        let created = pat |> Patient.atDate dt

                                        (created |> pmaDays)
                                        <> (created.Data.Age
                                            |> Option.filter underOneYear
                                            |> Option.map (AgeValue.getAgeInDays >> (+) 182<day>))
                                    )
                                    |> Expect.isEmpty "no day differs"
                                }

                                test "in weeks and days" {
                                    { PatientData.unknown with GestationalAge = gest }
                                    |> Patient.create (PatientIdentity.create "p1" "Test" (bd 2016 3 15) |> Some)
                                    |> Patient.atDate (date 2016 5 10)
                                    |> _.Data.PostMenstrualAge
                                    |> Expect.equal
                                        "26 weeks plus 56 days is 34 weeks"
                                        (Some(AgeWeeksDays.create 34<week> 0<day>))
                                }

                                test "no age: no post-menstrual age" {
                                    { PatientData.unknown with GestationalAge = gest }
                                    |> Patient.create None
                                    |> Patient.atDate (date 2026 3 30)
                                    |> pmaDays
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

                        test "36 weeks 6 days < 40 weeks" {
                            AgeWeeksDays.create 36<week> 6<day> <? AgeWeeksDays.create 40<week> 0<day>
                            |> Expect.isTrue "36 weeks 6 days should be < 40 weeks"
                        }

                        test "40 weeks >= 40 weeks" {
                            AgeWeeksDays.create 40<week> 0<day> >=? AgeWeeksDays.create 40<week> 0<day>
                            |> Expect.isTrue "40 weeks should be >= 40 weeks"
                        }

                        test "36 weeks 6 days <= 36 weeks 6 days" {
                            AgeWeeksDays.create 36<week> 6<day> <=? AgeWeeksDays.create 36<week> 6<day>
                            |> Expect.isTrue "36 weeks 6 days should be <= 36 weeks 6 days"
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

                        test "the post-menstrual age of 59 days at 26 weeks is 34 weeks and 3 days" {
                            AgeWeeksDays.postMenstrualAge
                                (AgeValue.create None (Some 1<month>) (Some 4<week>) (Some 1<day>))
                                (AgeWeeksDays.create 26<week> 0<day>)
                            |> Expect.equal "241 days" (AgeWeeksDays.create 34<week> 3<day>)
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
                pat
                |> Patient.Dto.toDto
                |> Patient.Dto.fromDto readAt None
                |> Result.mapError string

            testList
                "Patient tests"
                [
                    test "the unknown patient is anonymous, of unknown age" {
                        Patient.unknown.Identity |> Expect.isNone "no identity"
                        Patient.unknown.Data.Age |> Expect.isNone "unknown age"
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
                        let newBorn = NewBorn |> Patient.fromAgeType UnknownGender

                        newBorn |> thereAndBack |> Expect.equal "the same" (Ok newBorn)
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
                        |> List.map (Patient.Dto.fromDto readAt None >> Result.isError)
                        |> Expect.allEqual "an error" true
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
                            "none, then 26 weeks plus 56 days, 34 weeks"
                            (None, Some(AgeWeeksDays.create 34<week> 0<day>))
                    }

                    test "no gestational age, no post-menstrual age at a date" {
                        { PatientData.unknown with Age = Some AgeValue.ten }
                        |> Patient.create None
                        |> Patient.atDate (date 2016 5 10)
                        |> _.Data.PostMenstrualAge
                        |> Expect.isNone "none"
                    }

                    test "createAt creates the patient with its ages" {
                        let data =
                            { PatientData.unknown with GestationalAge = Some(AgeWeeksDays.create 26<week> 0<day>) }

                        data
                        |> Patient.createAt (date 2016 5 10) (Some stub)
                        |> Expect.equal
                            "created, then its ages"
                            (data |> Patient.create (Some stub) |> Patient.atDate (date 2016 5 10))
                    }

                    test "the calculated post-menstrual age goes there and back, also above 52 weeks" {
                        let pat =
                            { PatientData.unknown with GestationalAge = Some(AgeWeeksDays.create 26<week> 0<day>) }
                            |> Patient.create (Some stub)
                            |> Patient.atDate (date 2017 3 14)

                        pat.Data.PostMenstrualAge
                        |> Expect.equal "26 weeks plus 364 days, 78 weeks" (Some(AgeWeeksDays.create 78<week> 0<day>))

                        pat |> thereAndBack |> Expect.equal "the same" (Ok pat)
                    }

                    test "the patient as strings has no name and no id" {
                        PatientData.unknown
                        |> Patient.create (Some stub)
                        |> Patient.toString (date 2026 3 15)
                        |> List.exists (fun s -> s.Contains "Stub" || s.Contains "p1")
                        |> Expect.isFalse "no identity"
                    }

                    test "the department's name is the ward's own; unknown or nameless is none" {
                        [
                            Department.pediatricICU "ICK", Some "ICK"
                            Department.neonatalICU "NEO", Some "NEO"
                            UnknownDepartment, None
                            Department.pediatricICU "", None
                            Department.pediatricICU "  ", None
                        ]
                        |> List.map (fun (dep, exp) -> dep |> Department.name, exp)
                        |> List.iter (fun (act, exp) -> act |> Expect.equal "the name" exp)
                    }
                ]


    module AdultMarkTests =

        let years n =
            AgeValue.create (Some(n |> Conversions.yearFromInt)) None None None

        let ofDays n = n |> Conversions.dayFromInt |> AgeValue.fromDays


        let tests =
            testList
                "AdultMark"
                [
                    test "no age: the choice of the user" {
                        [ true; false ]
                        |> List.map (AdultMark.ofAge None)
                        |> Expect.equal "markable" [ AdultMark.MarkableAdult true; AdultMark.MarkableAdult false ]
                    }

                    test "below 16 years not an adult, from 16 years the choice, from 18 years an adult" {
                        [ 16 * 365 - 1; 16 * 365; 18 * 365 - 1; 18 * 365; 40 * 365 ]
                        |> List.map (fun d -> AdultMark.ofAge (Some(ofDays d)) true)
                        |> Expect.equal
                            "the marks"
                            [
                                AdultMark.NotAdult
                                AdultMark.MarkableAdult true
                                AdultMark.MarkableAdult true
                                AdultMark.Adult
                                AdultMark.Adult
                            ]
                    }

                    test "the choice counts only when markable" {
                        [ years 10; years 17; years 30 ]
                        |> List.map (fun a -> AdultMark.ofAge (Some a) false)
                        |> Expect.equal
                            "the marks"
                            [ AdultMark.NotAdult; AdultMark.MarkableAdult false; AdultMark.Adult ]
                    }

                    test "an adult from 18 years or when marked" {
                        [
                            AdultMark.NotAdult
                            AdultMark.MarkableAdult false
                            AdultMark.MarkableAdult true
                            AdultMark.Adult
                        ]
                        |> List.map AdultMark.isAdult
                        |> Expect.equal "adult" [ false; false; true; true ]
                    }
                ]


    module GestationalAgeNeedTests =

        let ofDays n = n |> Conversions.dayFromInt |> AgeValue.fromDays

        let ga30 = AgeWeeksDays.create 30<week> 0<day>

        let date (y: int) (m: int) (d: int) = System.DateTime(y, m, d)

        let birthDate y m d =
            BirthDate.create (Conversions.yearFromInt y) (Conversions.monthFromInt m) (Conversions.dayFromInt d)

        let identity bd = PatientIdentity.create "1" "Test" bd

        let data age =
            { PatientData.unknown with
                Age = age
                GestationalAge = Some ga30
            }

        let ages (pat: Patient) = pat.Data.GestationalAge, pat.Data.PostMenstrualAge


        let tests =
            testList
                "GestationalAgeNeed"
                [
                    test "without an age none, under 28 days required, under 1 year optional, from 1 year none" {
                        [
                            None
                            Some(ofDays 0)
                            Some(ofDays 27)
                            Some(ofDays 28)
                            Some(ofDays 364)
                            Some(ofDays 365)
                            Some(ofDays (2 * 365))
                        ]
                        |> List.map GestationalAgeNeed.ofAge
                        |> Expect.equal
                            "the needs"
                            [
                                GestationalAgeNeed.NoGestationalAge
                                GestationalAgeNeed.RequiredGestationalAge
                                GestationalAgeNeed.RequiredGestationalAge
                                GestationalAgeNeed.OptionalGestationalAge
                                GestationalAgeNeed.OptionalGestationalAge
                                GestationalAgeNeed.NoGestationalAge
                                GestationalAgeNeed.NoGestationalAge
                            ]
                    }

                    test "an anonymous patient under 1 year keeps the gestational age and gets the post-menstrual age" {
                        Patient.createAt (date 2026 11 8) None (data (Some(ofDays 70)))
                        |> ages
                        |> Expect.equal "30 weeks, then 40 weeks" (Some ga30, Some(AgeWeeksDays.create 40<week> 0<day>))
                    }

                    test
                        "an anonymous patient of 1 year or older: the gestational age is ignored, no post-menstrual age" {
                        Patient.createAt (date 2026 11 8) None (data (Some(ofDays 365)))
                        |> ages
                        |> Expect.equal "none" (None, None)
                    }

                    test "an anonymous patient without an age: the gestational age is ignored, no post-menstrual age" {
                        Patient.createAt (date 2026 11 8) None (data None)
                        |> ages
                        |> Expect.equal "none" (None, None)
                    }

                    test "an identified patient under 1 year keeps the gestational age the EHR gives" {
                        // the birthdate gives 70 days at the date: 2 months, 1 week and 3 days
                        let idn = identity (birthDate 2026 8 30)

                        Patient.createAt (date 2026 11 8) (Some idn) (data None)
                        |> ages
                        |> Expect.equal "30 weeks, then 40 weeks" (Some ga30, Some(AgeWeeksDays.create 40<week> 0<day>))
                    }

                    test "an identified patient of 1 year or older: the gestational age the EHR gives is ignored" {
                        let idn = identity (birthDate 2024 11 8)

                        Patient.createAt (date 2026 11 8) (Some idn) (data None)
                        |> ages
                        |> Expect.equal "none" (None, None)
                    }
                ]


    module EstimatesTests =

        let row gender age p3 mean p97 =
            {
                Gender = gender
                Age = age
                P3 = p3
                Mean = mean
                P97 = p97
            }

        let tables =
            {
                Weights =
                    [
                        row Male 1.<year> 8.<kg> 10.2<kg> 12.<kg>
                        row Female 1.<year> 7.<kg> 9.6<kg> 11.<kg>
                        row Male 10.<year> 25.<kg> 32.<kg> 40.<kg>
                    ]
                Heights =
                    [
                        row Male 1.<year> 70.<cm> 76.<cm> 82.<cm>
                        row Female 1.<year> 68.<cm> 74.<cm> 80.<cm>
                        row Male 10.<year> 130.<cm> 140.<cm> 150.<cm>
                    ]
                NeoWeights =
                    [
                        row Male 40.<week> 2900.<gram> 3480.<gram> 4100.<gram>
                        row Female 40.<week> 2800.<gram> 3390.<gram> 4000.<gram>
                    ]
                NeoHeights =
                    [
                        row Male 40.<week> 48.<cm> 51.<cm> 54.<cm>
                        row Female 40.<week> 47.<cm> 50.<cm> 53.<cm>
                    ]
            }

        let years n = AgeValue.create (Some(n * 1<year>)) None None None

        let days n = n |> Conversions.dayFromInt |> AgeValue.fromDays

        let awd w d = AgeWeeksDays.create (w * 1<week>) (d * 1<day>)

        let estimateOf p3 mean p97 =
            Some
                {
                    P3 = p3
                    Mean = mean
                    P97 = p97
                }


        let tests =
            testList
                "Estimates"
                [
                    test "a boy of 10 years takes the row of 10 years: the percentiles in whole kg, the mean on a step" {
                        Estimates.ofAge tables calc Male (Some(years 10)) None
                        |> Expect.equal
                            "25 to 40 kg, 32 kg; 130 to 150 cm, 140 cm"
                            {
                                Weight = estimateOf 25000<gram> 32000<gram> 40000<gram>
                                Height = estimateOf 130<cm> 140<cm> 150<cm>
                            }
                    }

                    test "a child without a known gender takes the average of the male and the female row" {
                        (Estimates.ofAge tables calc UnknownGender (Some(years 1)) None).Weight
                        |> Expect.equal
                            "7.5 to 11.5 kg in whole kg, 9.9 kg"
                            (estimateOf 7000<gram> 9900<gram> 11000<gram>)
                    }

                    test "a newborn with a post-menstrual age the neonatal table reaches takes the neonatal row" {
                        Estimates.ofAge tables calc Female (Some(days 0)) (Some(awd 40 0))
                        |> Expect.equal
                            "2800 to 4000 g, 3400 g; 47 to 53 cm, 50 cm"
                            {
                                Weight = estimateOf 2800<gram> 3400<gram> 4000<gram>
                                Height = estimateOf 47<cm> 50<cm> 53<cm>
                            }
                    }

                    test "past the last week of the neonatal table the age table answers" {
                        (Estimates.ofAge tables calc Male (Some(years 1)) (Some(awd 92 0))).Weight
                        |> Expect.equal "the row of 1 year" (estimateOf 8000<gram> 10000<gram> 12000<gram>)
                    }

                    test "no estimate without an age, from 18 years, or without a row for the gender" {
                        [
                            Estimates.ofAge tables calc Male None None
                            Estimates.ofAge tables calc Male (Some(years 18)) None
                            Estimates.ofAge
                                { tables with
                                    Weights = []
                                    Heights = []
                                }
                                calc
                                Male
                                (Some(years 10))
                                None
                        ]
                        |> Expect.equal "none" [ Estimates.none; Estimates.none; Estimates.none ]
                    }
                ]


    module NormalValueTablesTests =

        open System.Globalization
        open FsToolkit.ErrorHandling

        /// Runs f with the given culture on the current thread, and restores the culture afterwards.
        let underCulture (name: string) f =
            let saved = CultureInfo.CurrentCulture
            CultureInfo.CurrentCulture <-
                (if name = "" then
                     CultureInfo.InvariantCulture
                 else
                     CultureInfo name)

            try
                f ()
            finally
                CultureInfo.CurrentCulture <- saved

        let cultures = [ ""; "nl-NL"; "en-NL"; "de-DE"; "en-US" ]

        let header = [| "sex"; "age"; "p3"; "mean"; "p97" |]

        let rows =
            let weight =
                [|
                    header
                    [| "M"; "0.0192"; "3.1"; "3.955666803"; "4.7" |]
                    [| "M"; "10"; "26.4"; "38.2"; "56.1" |]
                    [| "F"; "0.0192"; "3.0"; "3.8"; "4.5" |]
                    [| "F"; "10"; "25.3"; "37.1"; "55.2" |]
                |]

            let height =
                [|
                    header
                    [| "M"; "0.0192"; "50"; "54"; "58" |]
                    [| "M"; "10"; "130.5"; "150.3"; "160.2" |]
                    [| "F"; "0.0192"; "49"; "53"; "57" |]
                    [| "F"; "10"; "129.4"; "149.1"; "159.3" |]
                |]

            Map.ofList
                [
                    "norm_weight", weight
                    "norm_height", height
                    "norm_weight_neo", [| header |]
                    "norm_height_neo", [| header |]
                ]

        let means (est: Estimates) = est.Weight |> Option.map _.Mean, est.Height |> Option.map _.Mean

        let boy age tables = Estimates.ofAge tables calc Male (Some age) None

        let sixDays = AgeValue.create None None None (Some 6<day>)
        let tenYears = AgeValue.create (Some 10<year>) None None None


        let tests =
            testList
                "NormalValueTables"
                [
                    testList
                        "the estimate is the same under every culture"
                        [
                            for culture in cultures do
                                test $"[%s{culture}] six days: 4.0 kg and 54 cm; ten years: 38 kg and 150 cm" {
                                    let tables =
                                        underCulture culture (fun () -> NormalValueTables.ofRows rows)
                                        |> Result.defaultWith (fun e -> failtest $"%A{e}")

                                    (tables |> boy sixDays |> means, tables |> boy tenYears |> means)
                                    |> Expect.equal
                                        "the estimates of the issue"
                                        ((Some 4000<gram>, Some 54<cm>), (Some 38000<gram>, Some 150<cm>))
                                }
                        ]

                    test "a row is read with its sex and its numbers" {
                        [| header; [| " F "; "1"; "7"; "9.6"; "11" |] |]
                        |> NormalValueTables.parseSheet "norm_weight"
                        |> Expect.equal
                            "a female row of 1 year"
                            (Ok
                                [
                                    {
                                        Gender = Female
                                        Age = 1.<year>
                                        P3 = 7.<kg>
                                        Mean = 9.6<kg>
                                        P97 = 11.<kg>
                                    }
                                ])
                    }

                    test "a sheet with only its header is an empty table" {
                        [ "norm_weight"; "norm_height"; "norm_weight_neo"; "norm_height_neo" ]
                        |> List.map (fun sheet -> sheet, [| header |])
                        |> Map.ofList
                        |> NormalValueTables.ofRows
                        |> Expect.equal
                            "empty"
                            (Ok
                                {
                                    Weights = []
                                    Heights = []
                                    NeoWeights = []
                                    NeoHeights = []
                                })
                    }

                    test "a sheet absent from the rows is an error" {
                        rows
                        |> Map.remove "norm_weight_neo"
                        |> NormalValueTables.ofRows
                        |> Expect.equal "weight neo missing" (Error [ NormalValueError.MissingSheet "norm_weight_neo" ])
                    }

                    test "a row with an age that is not a number is refused, not given to the estimate" {
                        [| header; [| "M"; "NaN"; "7"; "9"; "Infinity" |] |]
                        |> NormalValueTables.parseSheet "norm_weight"
                        |> Expect.equal
                            "age and p97"
                            (Error
                                [
                                    NormalValueError.NotANumber("norm_weight", 2, "age")
                                    NormalValueError.NotANumber("norm_weight", 2, "p97")
                                ]
                            : Validation<NormalValue<year, kg> list, _>)
                    }

                    test "a sheet without the columns names the missing ones" {
                        [| [| "sex"; "age"; "mean" |] |]
                        |> NormalValueTables.parseSheet "norm_height"
                        |> Expect.equal
                            "p3 and p97"
                            (Error [ NormalValueError.MissingColumns("norm_height", [ "p3"; "p97" ]) ]
                            : Validation<NormalValue<year, cm> list, _>)
                    }

                    test "a sheet without a header lacks every column" {
                        [||]
                        |> NormalValueTables.parseSheet "norm_height"
                        |> Expect.equal
                            "all"
                            (Error
                                [
                                    NormalValueError.MissingColumns("norm_height", NormalValueTables.requiredColumns)
                                ]
                            : Validation<NormalValue<year, cm> list, _>)
                    }

                    test "every error is named by its sheet, row and column, of every sheet" {
                        rows
                        |> Map.add
                            "norm_weight"
                            [| header; [| "M"; "1"; "x"; "9"; "" |]; [| "B"; "1"; "7"; "9"; "11" |] |]
                        |> Map.add "norm_height_neo" [| [| "sex"; "age" |] |]
                        |> NormalValueTables.ofRows
                        |> Expect.equal
                            "the errors"
                            (Error
                                [
                                    NormalValueError.NotANumber("norm_weight", 2, "p3")
                                    NormalValueError.NotANumber("norm_weight", 2, "p97")
                                    NormalValueError.UnknownSex("norm_weight", 3, "B")
                                    NormalValueError.MissingColumns("norm_height_neo", [ "p3"; "mean"; "p97" ])
                                ])
                    }

                    test "a row shorter than the header misses the numbers at its end" {
                        [| header; [| "M"; "1"; "7" |] |]
                        |> NormalValueTables.parseSheet "norm_weight"
                        |> Expect.equal
                            "mean and p97"
                            (Error
                                [
                                    NormalValueError.NotANumber("norm_weight", 2, "mean")
                                    NormalValueError.NotANumber("norm_weight", 2, "p97")
                                ]
                            : Validation<NormalValue<year, kg> list, _>)
                    }
                ]


    module CalculationWeightsAndHeightsTests =

        let header column = [| column; ""; "" |]

        let sheetOf column values =
            Array.append [| header column |] (values |> Array.map (fun v -> [| v; ""; "" |]))

        let rowsOf weights heights =
            Map.empty
            |> Map.add CalculationWeightsAndHeights.weightSheet (sheetOf "WeightKg" weights)
            |> Map.add CalculationWeightsAndHeights.heightSheet (sheetOf "HeightCm" heights)

        let grams xs = xs |> Array.map Conversions.gramFromInt

        let cms xs = xs |> Array.map Conversions.cmFromInt


        let tests =
            testList
                "CalculationWeightsAndHeights"
                [
                    test "reads the weights in kg as whole grams and the heights as whole cm" {
                        rowsOf [| "0.4"; "0.45"; "2.5" |] [| "30"; "31" |]
                        |> CalculationWeightsAndHeights.ofRows
                        |> Expect.equal
                            "the weights and heights without warnings"
                            (Ok(
                                {
                                    Weights = grams [| 400; 450; 2500 |]
                                    Heights = cms [| 30; 31 |]
                                },
                                []
                            ))
                    }

                    test "skips a blank cell" {
                        rowsOf [| "1"; ""; "2" |] [| "30" |]
                        |> CalculationWeightsAndHeights.ofRows
                        |> Result.map (fst >> _.Weights)
                        |> Expect.equal "two weights" (Ok(grams [| 1000; 2000 |]))
                    }

                    test "sorts a list out of order and warns with the row" {
                        rowsOf [| "1" |] [| "30"; "31"; "32"; "22"; "34" |]
                        |> CalculationWeightsAndHeights.ofRows
                        |> Result.map (fun (calc, warnings) -> calc.Heights, warnings)
                        |> Expect.equal
                            "sorted, a warning for row 5"
                            (Ok(
                                cms [| 22; 30; 31; 32; 34 |],
                                [ NormalValueSheetWarning.NotAscending("calc_height", 5) ]
                            ))
                    }

                    test "uses a duplicate value once and warns with the row" {
                        rowsOf [| "1"; "2"; "1" |] [| "30" |]
                        |> CalculationWeightsAndHeights.ofRows
                        |> Result.map (fun (calc, warnings) -> calc.Weights, warnings)
                        |> Expect.equal
                            "once, a warning for row 4"
                            (Ok(grams [| 1000; 2000 |], [ NormalValueSheetWarning.Duplicate("calc_weight", 4) ]))
                    }

                    test "a value that is not a positive number is an error with the row" {
                        rowsOf [| "1"; "abc"; "0"; "-2" |] [| "30" |]
                        |> CalculationWeightsAndHeights.ofRows
                        |> Expect.equal
                            "three errors"
                            (Error
                                [
                                    NormalValueSheetError.NotAPositiveNumber("calc_weight", 3, "abc")
                                    NormalValueSheetError.NotAPositiveNumber("calc_weight", 4, "0")
                                    NormalValueSheetError.NotAPositiveNumber("calc_weight", 5, "-2")
                                ])
                    }

                    testList
                        "a weight sheet that cannot be read is an error"
                        [
                            let heights = sheetOf "HeightCm" [| "30" |]
                            let withWeights weights =
                                Map.empty |> Map.add "calc_weight" weights |> Map.add "calc_height" heights

                            for name, rows, error in
                                [
                                    "a sheet without values",
                                    withWeights (sheetOf "WeightKg" [||]),
                                    NormalValueSheetError.NoValues "calc_weight"

                                    "a sheet without the column",
                                    withWeights (sheetOf "Weight" [| "1" |]),
                                    NormalValueSheetError.MissingColumn("calc_weight", "WeightKg")

                                    "a sheet without a header",
                                    withWeights [||],
                                    NormalValueSheetError.MissingColumn("calc_weight", "WeightKg")

                                    "a missing sheet",
                                    Map.empty |> Map.add "calc_height" heights,
                                    NormalValueSheetError.MissingSheet "calc_weight"
                                ] do
                                test name {
                                    rows
                                    |> CalculationWeightsAndHeights.ofRows
                                    |> Expect.equal name (Error [ error ])
                                }
                        ]

                    test "a positive value that rounds to 0 is an error with the row" {
                        rowsOf [| "0.0004" |] [| "0.4" |]
                        |> CalculationWeightsAndHeights.ofRows
                        |> Expect.equal
                            "an error per sheet"
                            (Error
                                [
                                    NormalValueSheetError.RoundsToZero("calc_weight", 2, "0.0004")
                                    NormalValueSheetError.RoundsToZero("calc_height", 2, "0.4")
                                ])
                    }

                    test "returns the errors of both sheets" {
                        rowsOf [| "x" |] [||]
                        |> CalculationWeightsAndHeights.ofRows
                        |> Expect.equal
                            "an error per sheet"
                            (Error
                                [
                                    NormalValueSheetError.NotAPositiveNumber("calc_weight", 2, "x")
                                    NormalValueSheetError.NoValues "calc_height"
                                ])
                    }

                    test "an estimate is rounded to the nearest calculation weight and height" {
                        let calc =
                            {
                                Weights = grams [| 30000; 33000 |]
                                Heights = cms [| 139; 145 |]
                            }

                        Estimates.ofAge EstimatesTests.tables calc Male (Some(EstimatesTests.years 10)) None
                        |> fun est -> est.Weight |> Option.map _.Mean, est.Height |> Option.map _.Mean
                        |> Expect.equal "33 kg and 139 cm" (Some 33000<gram>, Some 139<cm>)
                    }
                ]


    module EGFRBandOptionsTests =

        let header = [| "MinEGFR"; "MaxEGFR"; "Band" |]

        let fullSheet =
            [|
                header
                [| "50"; ""; "> 50" |]
                [| "30"; "50"; "30 - 50" |]
                [| "10"; "30"; "10 - 30" |]
                [| ""; "10"; "< 10" |]
            |]

        let option band text =
            {
                Band = band
                Text = text
            }


        let tests =
            testList
                "EGFRBandOptions"
                [
                    test "reads every band with its text, without warnings" {
                        fullSheet
                        |> EGFRBandOptions.ofRows
                        |> Expect.equal
                            "four options"
                            ([
                                option EGFRBand.Above50 "> 50"
                                option EGFRBand.From30To50 "30 - 50"
                                option EGFRBand.From10To30 "10 - 30"
                                option EGFRBand.UpTo10 "< 10"
                             ],
                             [])
                    }

                    test "gives the options in the order of the bands, whatever the order of the rows" {
                        Array.append [| header |] (fullSheet[1..] |> Array.rev)
                        |> EGFRBandOptions.ofRows
                        |> fst
                        |> List.map _.Band
                        |> Expect.equal "high to low" EGFRBandOptions.bands
                    }

                    test "a row whose limits match no band is left out with a warning" {
                        Array.append fullSheet [| [| "20"; "40"; "20 - 40" |] |]
                        |> EGFRBandOptions.ofRows
                        |> fun (options, warnings) -> options.Length, warnings
                        |> Expect.equal "four options, one warning" (4, [ EGFRBandSheetWarning.NoBand(6, "20", "40") ])
                    }

                    test "a second row for a band is left out with a warning" {
                        Array.append fullSheet [| [| "50"; ""; "more than 50" |] |]
                        |> EGFRBandOptions.ofRows
                        |> fun (options, warnings) -> options.Head.Text, warnings
                        |> Expect.equal "the first text, one warning" ("> 50", [ EGFRBandSheetWarning.DuplicateBand 6 ])
                    }

                    test "a band without a row is left out with a warning" {
                        fullSheet[0..3]
                        |> EGFRBandOptions.ofRows
                        |> fun (options, warnings) -> options |> List.map _.Band, warnings
                        |> Expect.equal
                            "three bands, one warning"
                            ([ EGFRBand.Above50; EGFRBand.From30To50; EGFRBand.From10To30 ],
                             [ EGFRBandSheetWarning.MissingBand EGFRBand.UpTo10 ])
                    }

                    test "a sheet without the columns gives no options and one warning" {
                        [| [| "Min"; "MaxEGFR" |]; [| "50"; "" |] |]
                        |> EGFRBandOptions.ofRows
                        |> Expect.equal "no options" ([], [ EGFRBandSheetWarning.MissingColumns [ "MinEGFR"; "Band" ] ])
                    }

                    test "a sheet without rows gives no options and no warnings" {
                        [||] |> EGFRBandOptions.ofRows |> Expect.equal "nothing" ([], [])
                    }
                ]


    module PatientDataEstimateTests =

        let egfrBands: EGFRBandOption list =
            [
                {
                    Band = Above50
                    Text = "> 50"
                }
                {
                    Band = UpTo10
                    Text = "< 10"
                }
            ]

        let resources =
            {
                NormalValueTables = EstimatesTests.tables
                CalculationWeightsAndHeights = calc
                EGFRBandOptions = egfrBands
            }

        /// A Dto of a boy of 1 year, without a weight or a height.
        let boyOfOneYear () =
            let dto = PatientData.Dto.dto ()
            dto.Gender <- "male"
            dto.Age <- EstimatesTests.years 1 |> AgeValue.Dto.toDto |> Some
            dto

        /// Sets a calculation weight in gram on the Dto, measured or estimated.
        let withWeight measured grams (dto: PatientData.Dto.Dto) =
            dto.Weight <-
                grams
                |> decimal
                |> WeightValue.weightInGram
                |> WeightAtDate.undated
                |> (if measured then Measured else Estimated)
                |> Some
                |> Weight.create [] None []
                |> Weight.Dto.toDto

            dto

        let read resources dto =
            dto
            |> PatientData.Dto.fromDto readAt resources
            |> Result.defaultWith (fun errs -> failtest $"%A{errs}")

        let estimatedGram grams =
            grams |> WeightValue.weightInGram |> WeightAtDate.undated |> Estimated |> Some


        let tests =
            testList
                "PatientData estimate"
                [
                    test "without the resources nothing is estimated and only the options without resources are set" {
                        let dat = boyOfOneYear () |> read None

                        dat.Estimates |> Expect.equal "no estimates" Estimates.none
                        dat.Options
                        |> Expect.equal "the options without resources" PatientDataOptions.withoutResources
                        dat.Weight.Calculation |> Expect.isNone "no calculation weight"
                    }

                    test "without the resources an estimated calculation weight in the Dto is read as it is" {
                        (boyOfOneYear () |> withWeight false 9000 |> read None).Weight.Calculation
                        |> Expect.equal "the estimate of the Dto" (estimatedGram 9000m)
                    }

                    test "with the resources a boy of 1 year gets the estimates, and the means as calculation values" {
                        let dat = boyOfOneYear () |> read (Some resources)

                        dat.Estimates
                        |> Expect.equal
                            "8 to 12 kg, 10 kg on a step; 70 to 82 cm, 76 cm"
                            {
                                Weight = EstimatesTests.estimateOf 8000<gram> 10000<gram> 12000<gram>
                                Height = EstimatesTests.estimateOf 70<cm> 76<cm> 82<cm>
                            }

                        dat.Weight.Calculation
                        |> Expect.equal "the estimated mean, without a date" (estimatedGram 10000m)

                        dat.Height.Calculation
                        |> Expect.equal
                            "the estimated mean, without a date"
                            (76m |> HeightValue.heightInCm |> HeightAtDate.undated |> Estimated |> Some)

                        dat.BodySurfaceArea |> Expect.isSome "the body surface area of the estimates"
                    }

                    test "with the resources a measured calculation weight is kept, and the estimates are still set" {
                        let dat = boyOfOneYear () |> withWeight true 11000 |> read (Some resources)

                        dat.Weight.Calculation
                        |> Expect.equal
                            "the measured weight"
                            (11000m |> WeightValue.weightInGram |> WeightAtDate.undated |> Measured |> Some)

                        dat.Estimates.Weight |> Expect.isSome "the weight estimate"
                    }

                    test "with the resources an old estimate is replaced by the new estimate" {
                        (boyOfOneYear () |> withWeight false 3000 |> read (Some resources)).Weight.Calculation
                        |> Expect.equal "the new mean" (estimatedGram 10000m)
                    }

                    test "with the resources an old estimate is dropped when the age gives no estimate" {
                        let dto = boyOfOneYear () |> withWeight false 10000
                        dto.Age <- EstimatesTests.years 20 |> AgeValue.Dto.toDto |> Some

                        let dat = dto |> read (Some resources)

                        dat.Estimates |> Expect.equal "no estimates from 18 years" Estimates.none
                        dat.Weight.Calculation |> Expect.isNone "no calculation weight"
                        dat.BodySurfaceArea |> Expect.isNone "no body surface area"
                    }

                    test
                        "with the resources the options hold the weights, the heights and the eGFR bands of the resources" {
                        let opts = (boyOfOneYear () |> read (Some resources)).Options

                        opts.Weights |> Expect.equal "the calculation weights" calc.Weights
                        opts.Heights |> Expect.equal "the calculation heights" calc.Heights
                        opts.EGFRBands |> Expect.equal "the eGFR bands" egfrBands

                        opts.Dialyses
                        |> Expect.equal
                            "the three dialyses"
                            [ IntermittentHemodialysis; ContinuousHemodialysis; PeritonealDialysis ]
                    }

                    test "the age options are the ranges the validators accept" {
                        let o = PatientDataOptions.withoutResources

                        [
                            o.Years |> List.map int, [ 0..120 ]
                            o.Months |> List.map int, [ 0..11 ]
                            o.Weeks |> List.map int, [ 0..4 ]
                            o.Days |> List.map int, [ 0..6 ]
                            o.GestationalWeeks |> List.map int, [ 20..52 ]
                            o.GestationalDays |> List.map int, [ 0..6 ]
                        ]
                        |> List.iter (fun (actual, expected) -> actual |> Expect.equal "the range" expected)
                    }

                    test "every age option passes the validator of its part" {
                        let o = PatientDataOptions.withoutResources
                        let ok r = r |> Result.isOk

                        [
                            o.Years
                            |> List.forall (int >> Some >> AgeValue.Validation.yearValidator "years" >> ok)
                            o.Months
                            |> List.forall (int >> Some >> AgeValue.Validation.monthValidator "months" >> ok)
                            o.Weeks
                            |> List.forall (int >> Some >> AgeValue.Validation.weekValidator "weeks" >> ok)
                            o.Days
                            |> List.forall (int >> Some >> AgeValue.Validation.dayValidator "days" >> ok)
                            o.GestationalWeeks
                            |> List.forall (int >> AgeWeeksDays.Validation.validateWeeks "weeks" >> ok)
                            o.GestationalDays
                            |> List.forall (int >> AgeWeeksDays.Validation.validateDays "days" >> ok)
                        ]
                        |> List.forall id
                        |> Expect.isTrue "every option is valid"
                    }

                    test "toDto writes the estimates" {
                        let dat = boyOfOneYear () |> read (Some resources)
                        let dto = dat |> PatientData.Dto.toDto

                        dto.Estimates |> Expect.equal "the estimates" dat.Estimates
                    }

                    test "Patient.Dto.fromDto passes the resources to the patient data" {
                        let dto = Patient.Dto.dto ()
                        dto.Data <- boyOfOneYear ()

                        dto
                        |> Patient.Dto.fromDto readAt (Some resources)
                        |> Result.map _.Data.Estimates.Weight.IsSome
                        |> Expect.equal "the weight estimate" (Ok true)
                    }
                ]


    module RoundMeasuredTests =

        let resources =
            {
                NormalValueTables =
                    {
                        Weights = []
                        Heights = []
                        NeoWeights = []
                        NeoHeights = []
                    }
                CalculationWeightsAndHeights = calc
                EGFRBandOptions = []
            }

        let measuredOn = Some(DateTime(2026, 11, 10))

        let kg (v: decimal) = v |> WeightValue.weightInKg |> WeightAtDate.create measuredOn

        let cm (v: decimal) = v |> HeightValue.heightInCm |> HeightAtDate.create measuredOn

        let meter (v: decimal) = v |> HeightValue.heightInMeter |> HeightAtDate.create measuredOn

        /// Patient data of an adult with a measured calculation weight and height and the given actual weights.
        let patient actualWeights weight height =
            PatientData.create
                Department.unknown
                [||]
                Male
                (Some AgeValue.eighteen)
                (Weight.create actualWeights None [] (Some(Measured weight)))
                (Height.create [] None (Some(Measured height)))
                None

        let gram (v: int) =
            v
            |> decimal
            |> WeightValue.weightInGram
            |> WeightAtDate.create measuredOn
            |> Measured
            |> Some

        let weightOf w =
            (patient [] (kg w) (cm 180m) |> PatientData.estimate (Some resources)).Weight.Calculation

        let heightOf h =
            (patient [] (kg 12.5m) (cm h) |> PatientData.estimate (Some resources)).Height.Calculation


        let tests =
            testList
                "PatientData round measured"
                [
                    test
                        "a measured weight of 12.34 kg becomes 12.5 kg on the measurement date, the exact weight in Actual" {
                        let dat = patient [] (kg 12.34m) (cm 180m) |> PatientData.estimate (Some resources)

                        dat.Weight.Calculation
                        |> Expect.equal "12500 g, measured, with the date" (gram 12500)
                        dat.Weight.Actual |> Expect.equal "the exact weight" [ kg 12.34m ]
                    }

                    test "an exact weight that Actual already holds is not added again" {
                        let w = kg 12.34m

                        (patient [ w ] w (cm 180m) |> PatientData.estimate (Some resources)).Weight.Actual
                        |> Expect.equal "the one exact weight" [ w ]
                    }

                    test "12.34 kg is not added when Actual holds 12340 gram on the same date" {
                        let w = 12340 |> decimal |> WeightValue.weightInGram |> WeightAtDate.create measuredOn

                        (patient [ w ] (kg 12.34m) (cm 180m) |> PatientData.estimate (Some resources)).Weight.Actual
                        |> Expect.equal "the one exact weight" [ w ]
                    }

                    test "75.6 cm is not added when Actual holds 0.756 meter on the same date" {
                        let dat =
                            PatientData.create
                                Department.unknown
                                [||]
                                Male
                                (Some AgeValue.eighteen)
                                (Weight.create [] None [] (Some(Measured(kg 12.5m))))
                                (Height.create [ meter 0.756m ] None (Some(Measured(cm 75.6m))))
                                None
                            |> PatientData.estimate (Some resources)

                        dat.Height.Actual |> Expect.equal "the one exact height" [ meter 0.756m ]
                        dat.Height.Calculation |> Expect.equal "76 cm" (Some(Measured(cm 76m)))
                    }

                    test "the same weight on another date is added" {
                        let earlier =
                            12.34m
                            |> WeightValue.weightInKg
                            |> WeightAtDate.create (Some(DateTime(2026, 11, 1)))

                        (patient [ earlier ] (kg 12.34m) (cm 180m)
                         |> PatientData.estimate (Some resources))
                            .Weight.Actual
                        |> Expect.equal "both weights" [ earlier; kg 12.34m ]
                    }

                    test "a measured weight that is already on the list is left as it is" {
                        let dat = patient [] (kg 12.5m) (cm 180m) |> PatientData.estimate (Some resources)

                        dat.Weight.Calculation
                        |> Expect.equal "the measured weight" (Some(Measured(kg 12.5m)))
                        dat.Weight.Actual |> Expect.isEmpty "nothing added"
                    }

                    test "a weight halfway between two list values takes the lower value" {
                        weightOf 12.25m |> Expect.equal "12000 g" (gram 12000)
                    }

                    test "above 100 kg a weight is rounded to steps of 5 kg, the lower value on a tie" {
                        [ 123m; 122.5m ]
                        |> List.map weightOf
                        |> Expect.equal "125 kg and 120 kg" [ gram 125000; gram 120000 ]
                    }

                    test "a weight below the list takes the first value" {
                        weightOf 0.3m |> Expect.equal "400 g" (gram 400)
                    }

                    test
                        "a measured height of 75.6 cm becomes 76 cm on the measurement date, the exact height in Actual" {
                        let dat = patient [] (kg 12.5m) (cm 75.6m) |> PatientData.estimate (Some resources)

                        dat.Height.Calculation
                        |> Expect.equal "76 cm, measured, with the date" (Some(Measured(cm 76m)))
                        dat.Height.Actual |> Expect.equal "the exact height" [ cm 75.6m ]
                    }

                    test "a height is compared exactly, not first cut to whole cm" {
                        heightOf 75.9m |> Expect.equal "76 cm, not 75 cm" (Some(Measured(cm 76m)))
                    }

                    test "a height beyond the list takes the first or the last value" {
                        [ 25m; 230m ]
                        |> List.map heightOf
                        |> Expect.equal "30 cm and 220 cm" [ Some(Measured(cm 30m)); Some(Measured(cm 220m)) ]
                    }

                    test "without the resources a measured weight is read as it is" {
                        let dat = patient [] (kg 12.34m) (cm 180m) |> PatientData.estimate None

                        dat.Weight.Calculation
                        |> Expect.equal "the exact weight" (Some(Measured(kg 12.34m)))
                        dat.Weight.Actual |> Expect.isEmpty "nothing added"
                    }

                    test "the body surface area uses the rounded weight and height" {
                        let rounded =
                            patient [] (kg 12.34m) (cm 75.6m)
                            |> PatientData.estimate (Some resources)
                            |> PatientData.calculateBodySurfaceArea

                        let onList = patient [] (kg 12.5m) (cm 76m) |> PatientData.calculateBodySurfaceArea

                        rounded.BodySurfaceArea
                        |> Expect.equal "the area of 12.5 kg and 76 cm" onList.BodySurfaceArea
                    }
                ]


    module PatientDataEditTests =

        /// A patient data Dto with an age in the parts given, and nothing else.
        let agedDto years days =
            let dto = PatientData.Dto.dto ()
            let age = AgeValue.Dto.dto ()
            age.Years <- years
            age.Days <- days
            dto.Age <- Some age
            dto


        let tests =
            testList
                "PatientDataEdit"
                [
                    test "a part of the age on patient data without an age gives an age with that part" {
                        (PatientData.Dto.dto ()
                         |> PatientDataEdit.apply (PatientDataEdit.SetMonths(Some 3)))
                            .Age
                        |> Option.map (fun a -> a.Years, a.Months, a.Weeks, a.Days)
                        |> Expect.equal "3 months" (Some(None, Some 3, None, None))
                    }

                    test "an empty part of the age on patient data without an age gives no age" {
                        (PatientData.Dto.dto () |> PatientDataEdit.apply (PatientDataEdit.SetYears None)).Age
                        |> Expect.isNone "no age"
                    }

                    test "an empty part of an age counts as 0 and keeps the age" {
                        (agedDto (Some 5) None |> PatientDataEdit.apply (PatientDataEdit.SetYears None)).Age
                        |> Option.map _.Years
                        |> Expect.equal "0 years" (Some(Some 0))
                    }

                    test "the estimated weight and the measured height stay on the Dto for completing" {
                        let w = 18m |> WeightValue.weightInKg |> WeightAtDate.undated |> WeightAtDate.Dto.toDto
                        let h = 110m |> HeightValue.heightInCm |> HeightAtDate.undated |> HeightAtDate.Dto.toDto

                        let dto = agedDto (Some 5) None
                        dto.Weight.Calculation <- Some w
                        dto.Weight.CalculationEstimated <- true
                        dto.Height.Calculation <- Some h

                        let edited = dto |> PatientDataEdit.apply (PatientDataEdit.SetGender Female)

                        (edited.Weight.Calculation |> Option.isSome,
                         edited.Weight.CalculationEstimated,
                         edited.Height.Calculation |> Option.isSome)
                        |> Expect.equal "both kept" (true, true, true)
                    }

                    test "a measured weight is set without a date, and None clears it" {
                        let dto =
                            agedDto (Some 5) None
                            |> PatientDataEdit.apply (PatientDataEdit.SetWeight(Some(WeightValue.weightInKg 12.34m)))

                        (dto.Weight.Calculation
                         |> Option.map (fun w -> w.DateTime, w.WeightValue.Weight, w.WeightValue.WeightInKg),
                         dto.Weight.CalculationEstimated)
                        |> Expect.equal "12.34 kg measured" (Some(None, 12.34m, true), false)

                        (dto |> PatientDataEdit.apply (PatientDataEdit.SetWeight None)).Weight.Calculation
                        |> Expect.isNone "cleared"
                    }

                    test "an access is added and removed, and the same edit twice gives the same result" {
                        let added =
                            PatientData.Dto.dto ()
                            |> PatientDataEdit.apply (PatientDataEdit.AddAccess CVL)
                            |> PatientDataEdit.apply (PatientDataEdit.AddAccess CVL)

                        added.Access |> Expect.equal "added once" [| "cvl" |]

                        (added
                         |> PatientDataEdit.apply (PatientDataEdit.RemoveAccess CVL)
                         |> PatientDataEdit.apply (PatientDataEdit.RemoveAccess CVL))
                            .Access
                        |> Expect.isEmpty "removed"
                    }

                    test "a gestational age is written only when the age needs one, from the term" {
                        let neonate = agedDto None (Some 3)
                        neonate.GestationalAgeNeed <- GestationalAgeNeed.RequiredGestationalAge

                        (neonate |> PatientDataEdit.apply (PatientDataEdit.SetGestationalDays(Some 4))).GestationalAge
                        |> Option.map (fun ga -> ga.Weeks, ga.Days)
                        |> Expect.equal "37 weeks 4 days" (Some(37, 4))

                        (agedDto (Some 5) None
                         |> PatientDataEdit.apply (PatientDataEdit.SetGestationalWeeks(Some 30)))
                            .GestationalAge
                        |> Expect.isNone "no gestational age at 5 years"
                    }

                    test "a department edit replaces a department of another kind and leaves the rest" {
                        let neonatal () =
                            let dto = agedDto (Some 5) None
                            dto.Gender <- "female"
                            dto.Access <- [| "cvl" |]
                            dto.Department.NeonatalICU <- Some "NEO"
                            dto

                        let named = neonatal () |> PatientDataEdit.apply (PatientDataEdit.SetDepartment(Some "ICK"))

                        named
                        |> PatientData.Dto.fromDto readAt None
                        |> Result.map (_.Department >> Department.name)
                        |> Expect.equal "the other department ICK" (Ok(Some "ICK"))

                        (named.Age |> Option.map _.Years, named.Gender, named.Access)
                        |> Expect.equal "the rest as it was" (Some(Some 5), "female", [| "cvl" |])

                        let cleared = neonatal () |> PatientDataEdit.apply (PatientDataEdit.SetDepartment None)

                        (cleared.Department.NeonatalICU, cleared.Department.OtherDepartment)
                        |> Expect.equal "no department" (None, None)
                    }

                    test "SetLocation writes, replaces and clears the Location and leaves the rest as it was" {
                        let dto = agedDto (Some 5) None
                        dto.Gender <- "female"
                        dto.Department.OtherDepartment <- Some "ICK"

                        let located = dto |> PatientDataEdit.apply (PatientDataEdit.SetLocation(Some "UMCU"))

                        located
                        |> PatientData.Dto.fromDto readAt None
                        |> Result.map _.Location
                        |> Expect.equal "UMCU" (Ok(Some "UMCU"))

                        (located.Gender, located.Department.OtherDepartment)
                        |> Expect.equal "the rest as it was" ("female", Some "ICK")

                        (located |> PatientDataEdit.apply (PatientDataEdit.SetLocation(Some "AMC"))).Location
                        |> Expect.equal "replaced" (Some "AMC")

                        (located |> PatientDataEdit.apply (PatientDataEdit.SetLocation None)).Location
                        |> Expect.isNone "cleared"
                    }

                    test "the gender, the renal function, the department and the mark are written as Dto strings" {
                        let dto =
                            PatientData.Dto.dto ()
                            |> PatientDataEdit.apply (PatientDataEdit.SetGender Female)
                            |> PatientDataEdit.apply (PatientDataEdit.SetRenalFunction(Some PeritonealDialysis))
                            |> PatientDataEdit.apply (PatientDataEdit.SetDepartment(Some "ICK"))
                            |> PatientDataEdit.apply (PatientDataEdit.SetMarkedAdult true)

                        (dto.Gender, dto.RenalFunction, dto.Department.OtherDepartment, dto.MarkedAdult)
                        |> Expect.equal "as edited" ("female", Some "peritoneal-dialysis", Some "ICK", true)
                    }
                ]


    [<Tests>]
    let tests =

        [
            CalculationTests.tests
            EmergencyTests.tests
            MinMaxTests.tests
            MinMaxTests2.tests
            MinMaxTests2.DtoTests.tests
            PatientTests.AgeTests.tests
            PatientTests.BirthDateTests.tests
            PatientTests.WeightTests.tests
            PatientTests.HeightTests.tests
            PatientTests.AgeAtCreationTests.tests
            PatientTests.AgeInDaysLivedTests.tests
            PatientTests.AgeWeekDaysTests.tests
            PatientTests.PatientDataTests.tests
            PatientTests.DepartmentTests.tests
            PatientTests.tests
            AdultMarkTests.tests
            GestationalAgeNeedTests.tests
            EstimatesTests.tests
            NormalValueTablesTests.tests
            CalculationWeightsAndHeightsTests.tests
            EGFRBandOptionsTests.tests
            PatientDataEstimateTests.tests
            RoundMeasuredTests.tests
            PatientDataEditTests.tests
        ]
        //|> List.skip 4
        //|> List.take 1
        |> testList "GenCore"
