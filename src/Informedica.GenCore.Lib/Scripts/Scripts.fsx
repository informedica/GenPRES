
#load "load.fsx"

#load "../Measures.fs"
#load "../Aether.fs"
#load "../Validus.fs"
#load "../Calculations.fs"
#load "../ValueUnit.fs"
#load "../MinIncrMax.fs"
#load "../Patient.fs"



module MeasureScripts =

    open Informedica.GenCore.Lib

    3.<mg/dL> |> Conversions.Creatinine.toMicroMolePerLiter
              |> Conversions.Creatinine.toMilliGramPerDeciLiter

    1.<mg/dL> |> Conversions.Urea.toMilliMolePerLiter
    1.<mmol/L> |> Conversions.Urea.toMilliGramPerDeciLiter


module PatientScripts =

    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.Patients

    open System


    let fromAgeType = Patient.fromAgeType UnknownGender DateTime.Now



    let ageDto = AgeValue.Dto.dto ()

    ageDto.Years <- Some 157
    ageDto.Days <- Some 8

    ageDto
    |>(AgeValue.Dto.fromDto)


    let weightDto = WeightValue.Dto.dto ()

    weightDto.Weight <- 300m
    weightDto.WeightInKg <- false

    weightDto
    |> WeightValue.Dto.fromDto


    WeightValue.Validation.maxWeightGram
    |> decimal


    VenousAccess.Validation.validate "VenousAcces" "central"


    Patient.unknown
    |> Patient.Dto.toDto
    |> Patient.Dto.fromDto
    |> function
        | Ok pat -> pat = Patient.unknown
        | Error errs ->
            printfn $"{errs}"
            false



    let patDto =
        Patient.unknown
        |> Patient.Dto.toDto


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


    Department.pediatricDepartment "WKZ"
    |> Department.toString


    open Informedica.Utils.Lib.BCL

    "UMCU"
    |> Department.adultICU
    |> Department.toString
    |> Department.fromString


    Calculations.Age.ageToStringNL None None None None

    Calculations.Age.ageToStringNL (Some 2<year>) (Some 1<month>) (Some 2<week>) None


    PatientAge.newBorn
    PatientAge.infant
    PatientAge.toddler
    PatientAge.child
    PatientAge.adolescent
    PatientAge.adult


    NewBorn |> fromAgeType 
    |> Patient.BSA.calcMosteller


    Adult |> fromAgeType 
    |> Patient.Dto.toDto
    |> Patient.Dto.fromDto


    FullTerm
    |> AgeWeeksDays.fromGestation
    |> Option.map (AgeWeeksDays.SetGet.setDays 1<day>)
    |> Option.map (AgeWeeksDays.toString)
    |> Option.defaultValue ""


    FullTerm
    |> AgeWeeksDays.fromGestation
    |> Option.map (AgeWeeksDays.SetGet.getWeeks >> GestationType.fromWeeks)

    FullTerm
    |> AgeWeeksDays.fromGestation
    |> Option.map  AgeWeeksDays.SetGet.getWeeks

    37<week> |> GestationType.fromWeeks
    GestationType.max
    GestationType.min



    NewBorn
    |> fromAgeType
    //|> Patient.SetGet
    //|> Patient.SetGet.getAgeValue DateTime.Now

    |> Patient.calcPostMenstrualAge DateTime.Now
    //|> Patient.toString DateTime.Now


    NewBorn
    |> fromAgeType
    |> Patient.SetGet.getAge
    |> PatientAge.toString


    AgeValue.create (Some 0<year>) None None None
    |> AgeValue.toString

    Calculations.Age.ageToStringNL (Some 0<year>) None None None


    let newBornDto =
        NewBorn
        |> fromAgeType
        |> Patient.Dto.toDto


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
        p 
        |> Patient.SetGet.getAgeValue DateTime.Now
        |> Option.map AgeValue.getAgeInDays
        |> printfn "%A" 
        p 
        |> Patient.toString DateTime.Now
    | _  -> []


    Calculations.Age.postMenstrualAge 97<day> 40<week> 0<day>

    AgeType.normalValues



module ValueUnitScripts =


    open System

    open MathNet.Numerics
    open FsToolkit.ErrorHandling

    open Informedica.Utils.Lib.BCL
    open Informedica.GenUnits.Lib
    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.ValueUnits


    Units.Height.centiMeter
    |> ValueUnit.withSingleValue 100N 
    |> ValueUnit.Measures.toDecimalCm

    Units.Weight.kiloGram
    |> ValueUnit.withSingleValue 100N 
    |> ValueUnit.Measures.toDecimalCm


    1200<gram> |> ValueUnit.Measures.fromIntGram

    let w1 = 70N |> ValueUnit.singleWithUnit Units.Weight.kiloGram
    let w2 = 70000N |> ValueUnit.singleWithUnit Units.Weight.gram

    let h1 = 180N |> ValueUnit.singleWithUnit Units.Height.centiMeter
    let h2 = (180N/100N) |> ValueUnit.singleWithUnit Units.Height.meter

    ValueUnit.Calculations.BSA.calcDuBois w1 h1
    ValueUnit.Calculations.BSA.calcMosteller w1 h1

    ValueUnit.Calculations.BSA.calcDuBois w1 h2
    ValueUnit.Calculations.BSA.calcMosteller w1 h2

    ValueUnit.Calculations.BSA.calcDuBois w2 h2
    ValueUnit.Calculations.BSA.calcMosteller w2 h2

    ValueUnit.Calculations.BSA.calcDuBois w2 h1
    ValueUnit.Calculations.BSA.calcMosteller w2 h1

    let ds = 1N |> ValueUnit.createSingle Units.Time.day
    let ws = 34N |> ValueUnit.createSingle Units.Time.week

    let dtNow = DateTime.Now
    let dtBirth = dtNow.AddDays(-7)

    ValueUnit.Calculations.Age.adjusted (DateTime.Now.AddDays(-7)) DateTime.Now ds ws



    Calculations.Age.adjustedAge 1<day> 34<week> (DateTime.Now.AddDays(-7)) DateTime.Now 

    DateTime.dateDiff (DateTime.Now.AddDays(-7)) DateTime.Now 

    7<day> - (34<week> |> Conversions.weeksToDays)

    let y, m, w, d = ValueUnit.Calculations.Age.fromBirthDate (DateTime(1965, 12, 7)) DateTime.Now


    ValueUnit.Calculations.Age.ageToString None (Some m) (Some w) (Some d)
    ValueUnit.Calculations.Age.ageToStringNL (Some y) None (Some w) (Some d)
    ValueUnit.Calculations.Age.ageToStringNLShort (Some y) None  (Some w) (Some d)
    ValueUnit.Calculations.Age.ageToStringNLShort None (Some m) (Some w) (Some d)
