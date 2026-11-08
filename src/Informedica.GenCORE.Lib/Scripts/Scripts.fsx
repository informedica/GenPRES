#load "load.fsx"


open System

module MeasureScripts =

    open Informedica.GenCore.Lib

    3.<mg / dL>
    |> Conversions.Creatinine.toMicroMolePerLiter
    |> Conversions.Creatinine.toMilliGramPerDeciLiter

    1.<mg / dL> |> Conversions.Urea.toMilliMolePerLiter
    1.<mmol / L> |> Conversions.Urea.toMilliGramPerDeciLiter


module PatientScripts =

    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.Patients

    let fromAgeType = Patient.fromAgeType UnknownGender DateTime.Now


    let ageDto = AgeValue.Dto.dto ()

    ageDto.Years <- Some 157
    ageDto.Days <- Some 8

    ageDto |> AgeValue.Dto.fromDto


    let weightDto = WeightValue.Dto.dto ()

    weightDto.Weight <- 300m
    weightDto.WeightInKg <- false

    weightDto |> WeightValue.Dto.fromDto


    WeightValue.Validation.maxWeightGram |> decimal


    VenousAccess.Validation.validate "VenousAcces" "central"


    Patient.unknown
    |> Patient.Dto.toDto
    |> Patient.Dto.fromDto
    |> function
        | Ok pat -> pat = Patient.unknown
        | Error errs ->
            printfn $"{errs}"
            false


    let patDto = Patient.unknown |> Patient.Dto.toDto


    patDto.Weight.Calculation <-
        let w = WeightAtDate.Dto.dto ()
        w.DateTime <- DateTime.Now
        w.WeightValue.Weight <- 4.7m
        Some w


    patDto.Height.Calculation <-
        let h = HeightAtDate.Dto.dto ()
        h.DateTime <- DateTime.Now
        h.HeightValue.Height <- 57m
        h.HeightValue.HeightInMeter <- false
        Some h


    patDto
    |> Patient.Dto.fromDto
    |> function
        | Ok p -> p |> Patient.BSA.calcMosteller
        | Error _ -> None


    Department.pediatricDepartment "WKZ" |> Department.toString


    "UMCU" |> Department.adultICU |> Department.toString |> Department.fromString


    Calculations.Age.ageToStringNL None None None None

    Calculations.Age.ageToStringNL (Some 2<year>) (Some 1<month>) (Some 2<week>) None


    [ NewBorn; Infant; Toddler; Child; Adolescent; Adult ] |> List.map AgeValue.fromAgeType


    NewBorn |> fromAgeType |> Patient.BSA.calcMosteller


    Adult |> fromAgeType |> Patient.Dto.toDto |> Patient.Dto.fromDto


    37<week> |> GestationType.fromWeeks
    GestationType.max
    GestationType.min


    NewBorn
    |> fromAgeType

    |> Patient.atDate DateTime.Now
    |> _.Data.PostMenstrualAge
    //|> Patient.toString DateTime.Now


    NewBorn |> fromAgeType |> _.Data.Age |> Option.map AgeValue.toString


    AgeValue.create (Some 0<year>) None None None |> AgeValue.toString

    Calculations.Age.ageToStringNL (Some 0<year>) None None None


    let newBornDto = NewBorn |> fromAgeType |> Patient.Dto.toDto


    newBornDto.Age.Value.Years <- 0 |> Some
    newBornDto.Age.Value.Months <- 1 |> Some
    newBornDto.Age.Value.Weeks <- 1 |> Some

    newBornDto.GestationalAge <-
        let dto = AgeWeeksDays.Dto.dto ()
        dto.Weeks <- 40
        dto |> Some


    newBornDto
    |> Patient.Dto.fromDto
    |> function
        | Ok p ->
            p.Data.Age
            |> Option.map AgeValue.getAgeInDays
            |> printfn "%A"

            p |> Patient.toString DateTime.Now
        | _ -> []


    AgeType.normalValues


module ValueUnitScripts =


    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.ValueUnits


    Units.Height.centiMeter
    |> ValueUnit.singleWithValue 100N
    |> ValueUnit.Measures.toDecimalCm

    Units.Weight.kiloGram
    |> ValueUnit.singleWithValue 100N
    |> ValueUnit.Measures.toDecimalCm


    1200<gram> |> ValueUnit.Measures.fromIntGram

    let w1 = 70N |> ValueUnit.singleWithUnit Units.Weight.kiloGram
    let w2 = 70000N |> ValueUnit.singleWithUnit Units.Weight.gram

    let h1 = 180N |> ValueUnit.singleWithUnit Units.Height.centiMeter
    let h2 = (180N / 100N) |> ValueUnit.singleWithUnit Units.Height.meter

    ValueUnit.Calculations.BSA.calcDuBois w1 h1
    ValueUnit.Calculations.BSA.calcMosteller w1 h1

    ValueUnit.Calculations.BSA.calcDuBois w1 h2
    ValueUnit.Calculations.BSA.calcMosteller w1 h2

    ValueUnit.Calculations.BSA.calcDuBois w2 h2
    ValueUnit.Calculations.BSA.calcMosteller w2 h2

    ValueUnit.Calculations.BSA.calcDuBois w2 h1
    ValueUnit.Calculations.BSA.calcMosteller w2 h1



open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenCore.Lib.Ranges


let foldMaximize = MinMax.foldMaximize true true

// a Minimum

[
    MinMax.create None None
    MinMax.create None (Some(1N |> ValueUnit.singleWithUnit Units.Count.times |> Limit.exclusive))
]
|> foldMaximize


[
    MinMax.create None (Some(10N |> ValueUnit.singleWithUnit Units.Mass.gram |> Limit.exclusive))
    MinMax.create None (Some(2N |> ValueUnit.singleWithUnit Units.Mass.kiloGram |> Limit.exclusive))
    MinMax.create None (Some(1N |> ValueUnit.singleWithUnit Units.Mass.kiloGram |> Limit.exclusive))
    MinMax.create None (Some(2N |> ValueUnit.singleWithUnit Units.Mass.kiloGram |> Limit.inclusive))

]
|> foldMaximize


[
    MinMax.create
        (Some(1N |> ValueUnit.singleWithUnit Units.Mass.gram |> Limit.exclusive))
        (Some(10N |> ValueUnit.singleWithUnit Units.Mass.gram |> Limit.exclusive))
    MinMax.create
        (Some(1N |> ValueUnit.singleWithUnit Units.Mass.gram |> Limit.inclusive))
        (Some(2N |> ValueUnit.singleWithUnit Units.Mass.kiloGram |> Limit.exclusive))
    MinMax.create None (Some(1N |> ValueUnit.singleWithUnit Units.Mass.kiloGram |> Limit.exclusive))
    MinMax.create
        (Some(1N |> ValueUnit.singleWithUnit Units.Mass.kiloGram |> Limit.exclusive))
        (Some(2N |> ValueUnit.singleWithUnit Units.Mass.kiloGram |> Limit.inclusive))

]
|> foldMaximize
