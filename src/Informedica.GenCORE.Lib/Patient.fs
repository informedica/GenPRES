/// The patient types and the age rules: the identity, the birth date, the gender, the ages, the weights and
/// heights, the department, the accesses and the renal function.
namespace Informedica.GenCore.Lib.Patients

#nowarn "40"

open System
open Informedica.Utils.Lib.BCL
open Informedica.GenCore.Lib


type AgeType =
    | NewBorn
    | Infant
    | Toddler
    | Child
    | Adolescent
    | Adult
    | UnknownAgeType


type GestationType =
    | ExtremePreterm
    | VeryPreterm
    | ModeratePreterm
    | FullTerm
    | UnknownGestation


/// An access of a patient that a medication can be given through.
type Access =
    /// A peripheral venous line.
    | PVL
    /// A central venous line.
    | CVL
    /// An enteral feeding tube.
    | EnteralTube


/// A band of the estimated glomerular filtration rate, in mL/min/1.73 m2. A band includes its
/// maximum.
type EGFRBand =
    /// Above 50.
    | Above50
    /// Above 30, up to and including 50.
    | From30To50
    /// Above 10, up to and including 30.
    | From10To30
    /// Up to and including 10.
    | UpTo10


/// The renal function of a patient: an eGFR band, or the kind of dialysis.
type RenalFunction =
    /// The band the eGFR of the patient is in.
    | EGFR of EGFRBand
    /// Intermittent hemodialysis.
    | IntermittentHemodialysis
    /// Continuous hemodialysis.
    | ContinuousHemodialysis
    /// Peritoneal dialysis.
    | PeritonealDialysis


/// An eGFR band the user picks from, with the text the user reads.
type EGFRBandOption =
    {
        /// The band of the option.
        Band: EGFRBand
        /// The text in the Band column of the row of the band, which the user reads.
        Text: string
    }


/// A problem in the rows of the eGFR band sheet. Row numbers count the header as row 1.
[<RequireQualifiedAccess>]
type EGFRBandSheetWarning =
    /// The sheet lacks these columns; no band is read.
    | MissingColumns of columns: string list
    /// The limits of the row match no band; the row is left out.
    | NoBand of row: int * minimum: string * maximum: string
    /// The row is a second row for a band; the first row is used.
    | DuplicateBand of row: int
    /// No row matches the band; the band is left out of the options.
    | MissingBand of band: EGFRBand


/// Where a value comes from: a measurement or an estimate.
type Source<'v> =
    /// The value was measured.
    | Measured of 'v
    /// The value was estimated, from the age of the patient or by the user.
    | Estimated of 'v


type Department =
    | AdultICU of string
    | PediatricICU of string
    | NeonatalICU of string
    | AdultDepartment of string
    | PediatricDepartment of string
    /// An outpatient clinic with a name.
    | OutpatientClinic of string
    /// A department with a name, of a kind that is not known.
    | OtherDepartment of string
    | UnknownDepartment


type Gender =
    | Male
    | Female
    | UnknownGender


type AgeValue =
    {
        Years: int<year> option
        Months: int<month> option
        Weeks: int<week> option
        Days: int<day> option
    }


/// The mark of a patient as an adult, set from the age of the patient and the choice of the user.
[<RequireQualifiedAccess>]
type AdultMark =
    /// Younger than 16 years: the patient cannot be marked as an adult.
    | NotAdult
    /// From 16 years to under 18 years, or without an age: an adult when the user marks the patient as one.
    | MarkableAdult of marked: bool
    /// From 18 years: an adult without a mark.
    | Adult


/// Whether the gestational age of a patient is needed, set from the age of the patient.
[<RequireQualifiedAccess>]
type GestationalAgeNeed =
    /// From 1 year, or without an age: the gestational age is not used.
    | NoGestationalAge
    /// From 28 days and under 1 year: the gestational age may be given.
    | OptionalGestationalAge
    /// Under 28 days: the gestational age is needed.
    | RequiredGestationalAge


/// A full birth date with a year, a month and a day.
type BirthDate =
    {
        Year: int<year>
        Month: int<month>
        Day: int<day>
    }


type WeightValue =
    | Kilogram of decimal<kg>
    | Gram of int<gram>


type HeightValue =
    | Meter of decimal<m>
    | Centimeter of decimal<cm>


type Weight =
    {
        Actual: WeightAtDate list
        Birth: WeightValue option
        Admission: WeightAtDate list
        /// The weight the calculations use, measured or estimated.
        Calculation: Source<WeightAtDate> option
    }

and WeightAtDate =
    {
        /// Optional date for when measured.
        Date: DateTime option
        Weight: WeightValue
    }


type Height =
    {
        Actual: HeightAtDate list
        Birth: HeightValue option
        /// The height the calculations use, measured or estimated.
        Calculation: Source<HeightAtDate> option
    }

and HeightAtDate =
    {
        /// Optional date for when measured.
        Date: DateTime option
        Height: HeightValue
    }


type AgeWeeksDays =
    {
        Weeks: int<week>
        Days: int<day>
    }


/// A row of a normal-value table: for a gender and an age, the 3rd percentile, the mean and the
/// 97th percentile, with the unit of the age and the unit of the values.
type NormalValue<[<Measure>] 'age, [<Measure>] 'u> =
    {
        /// Male or Female.
        Gender: Gender
        /// The age of the row.
        Age: float<'age>
        /// The 3rd percentile.
        P3: float<'u>
        /// The mean.
        Mean: float<'u>
        /// The 97th percentile.
        P97: float<'u>
    }


/// The normal-value tables of weight and height: by age in years, and for newborns by
/// post-menstrual age in weeks. An empty table gives no estimate.
type NormalValueTables =
    {
        /// The weights by age in years, in kg.
        Weights: NormalValue<year, kg> list
        /// The heights by age in years, in cm.
        Heights: NormalValue<year, cm> list
        /// The weights by post-menstrual age in weeks, in gram.
        NeoWeights: NormalValue<week, gram> list
        /// The heights by post-menstrual age in weeks, in cm.
        NeoHeights: NormalValue<week, cm> list
    }


/// The weights and the heights the user picks from, each in ascending order. An estimated mean is rounded to the
/// nearest of them.
type CalculationWeightsAndHeights =
    {
        /// The weights in gram.
        Weights: int<gram>[]
        /// The heights in cm.
        Heights: int<cm>[]
    }


/// The 3rd percentile, the mean and the 97th percentile of a weight or a height: as a table gives
/// them, or as an estimate.
type Percentiles<'v> =
    {
        /// The 3rd percentile.
        P3: 'v
        /// The mean. In an estimate, the mean is rounded to the nearest weight step or height step.
        Mean: 'v
        /// The 97th percentile.
        P97: 'v
    }


/// The estimated weight and height of a patient; None when the table gives no value.
type Estimates =
    {
        /// The weight in gram.
        Weight: Percentiles<int<gram>> option
        /// The height in cm.
        Height: Percentiles<int<cm>> option
    }


/// An error in the rows of a normal-value sheet. Row numbers count the header as row 1.
[<RequireQualifiedAccess>]
type NormalValueError =
    /// No sheet has this name.
    | MissingSheet of sheet: string
    /// The sheet lacks these columns.
    | MissingColumns of sheet: string * columns: string list
    /// The row holds no number in the column.
    | NotANumber of sheet: string * row: int * column: string
    /// The row holds a sex that is not M or F.
    | UnknownSex of sheet: string * row: int * sex: string


/// An error in the rows of the sheet of the calculation weights or heights. Row numbers count the header as row 1.
[<RequireQualifiedAccess>]
type NormalValueSheetError =
    /// No sheet has this name.
    | MissingSheet of sheet: string
    /// The sheet lacks the column of the values.
    | MissingColumn of sheet: string * column: string
    /// The row holds a value that is not a positive number.
    | NotAPositiveNumber of sheet: string * row: int * value: string
    /// The row holds a positive number that rounds to 0 in whole grams or whole cm.
    | RoundsToZero of sheet: string * row: int * value: string
    /// The sheet holds no values.
    | NoValues of sheet: string


/// A problem in the sheet of the calculation weights or heights that the parse corrects. Row numbers count the
/// header as row 1.
[<RequireQualifiedAccess>]
type NormalValueSheetWarning =
    /// The row holds a value lower than a row above it; the values are sorted.
    | NotAscending of sheet: string * row: int
    /// The row holds a value that a row above it already holds; the value is used once.
    | Duplicate of sheet: string * row: int


/// The values the user picks the patient values from.
type PatientDataOptions =
    {
        /// The calculation weights in gram; empty without the resources.
        Weights: int<gram>[]
        /// The calculation heights in cm; empty without the resources.
        Heights: int<cm>[]
        /// The years of an age.
        Years: int<year> list
        /// The months of an age.
        Months: int<month> list
        /// The weeks of an age.
        Weeks: int<week> list
        /// The days of an age.
        Days: int<day> list
        /// The weeks of a gestational age.
        GestationalWeeks: int<week> list
        /// The days of a gestational age.
        GestationalDays: int<day> list
        /// The eGFR bands, each with the text of its row in the sheet; empty without the resources.
        EGFRBands: EGFRBandOption list
        /// The kinds of dialysis.
        Dialyses: RenalFunction list
    }


/// The resources that the estimates and the options of patient data come from.
type PatientDataResources =
    {
        /// The tables the weight and the height are estimated from.
        NormalValueTables: NormalValueTables
        /// The weights and heights an estimated mean is rounded to and the user picks from.
        CalculationWeightsAndHeights: CalculationWeightsAndHeights
        /// The eGFR bands the user picks from.
        EGFRBandOptions: EGFRBandOption list
    }


/// The id, the name and the birth date of an identified patient. Patient.toString never prints the identity.
type PatientIdentity =
    {
        /// The id the EHR knows the patient by.
        Id: string
        /// The full name of the patient as the EHR gives it.
        Name: string
        /// The birthdate the age of an identified patient is counted from.
        BirthDate: BirthDate
    }


/// Every patient characteristic except the identity.
type PatientData =
    {
        /// The location of the patient, with the name the rules use.
        Location: string option
        /// The ward the patient is admitted to.
        Department: Department
        /// The diagnoses of the patient.
        Diagnoses: string[]
        /// The gender.
        Gender: Gender
        /// The age in years, months, weeks and days: entered for an anonymous patient; for an
        /// identified patient calculated by Patient.atDate from the birthdate when the patient is
        /// created. None when not known.
        Age: AgeValue option
        /// Whether the patient is an adult, from the age and the mark the user set. A mark set under
        /// 16 years is ignored.
        AdultMark: AdultMark
        /// Whether the gestational age is needed at the age of the patient.
        GestationalAgeNeed: GestationalAgeNeed
        /// The weights.
        Weight: Weight
        /// The heights.
        Height: Height
        /// The gestational age at birth; absent when not known, and left out by
        /// PatientData.determineGestationalAndPostMenstrualAge when the gestational age is not used.
        GestationalAge: AgeWeeksDays option
        /// The post-menstrual age, the age corrected for the gestational age: the age plus the
        /// gestational age, set by PatientData.determineGestationalAndPostMenstrualAge. None without a
        /// gestational age or an age, and from 1 year.
        PostMenstrualAge: AgeWeeksDays option
        /// The body surface area of the calculation weight and height, set by
        /// PatientData.calculateBodySurfaceArea. None without both.
        BodySurfaceArea: decimal<bsa> option
        /// The accesses of the patient; empty when none is known.
        Access: Access list
        /// The renal function; none when not known.
        RenalFunction: RenalFunction option
        /// The estimated weight and height for the age and the gender, set by PatientData.estimate; none without
        /// the resources, without an age or from 18 years.
        Estimates: Estimates
        /// The values the user picks the patient values from, set by PatientData.estimate.
        Options: PatientDataOptions
    }


/// Why patient data is not enough to select rules with.
[<RequireQualifiedAccess>]
type PatientDataError =
    /// The patient data has no age, no adult mark and no measured weight with a measured height.
    | NoAgeOrMeasuredWeightAndHeight


/// One change to the patient data: a value set or cleared, or an access added or removed.
[<RequireQualifiedAccess>]
type PatientDataEdit =
    /// The years of the age; None counts as 0 when the patient has an age.
    | SetYears of int option
    /// The months of the age; None counts as 0 when the patient has an age.
    | SetMonths of int option
    /// The weeks of the age; None counts as 0 when the patient has an age.
    | SetWeeks of int option
    /// The days of the age; None counts as 0 when the patient has an age.
    | SetDays of int option
    /// The weeks of the gestational age; None is the term of 37 weeks.
    | SetGestationalWeeks of int option
    /// The days of the gestational age; None is the days of the term, 0 days.
    | SetGestationalDays of int option
    /// The measured weight; None clears it, so the weight is estimated.
    | SetWeight of WeightValue option
    /// The measured height; None clears it, so the height is estimated.
    | SetHeight of HeightValue option
    /// The gender.
    | SetGender of Gender
    /// The renal function; None clears it.
    | SetRenalFunction of RenalFunction option
    /// The name of the department; None clears it.
    | SetDepartment of string option
    /// The location of the patient; None clears it.
    | SetLocation of string option
    /// Adds the access; the patient keeps it when the patient already has it.
    | AddAccess of Access
    /// Removes the access; nothing changes when the patient does not have it.
    | RemoveAccess of Access
    /// The mark of the user that the patient is an adult. It is used only for a patient who can be marked as
    /// an adult.
    | SetMarkedAdult of bool


/// The patient: the identity of an identified patient and the patient data. An anonymous patient
/// has no identity.
type Patient =
    {
        /// The id, the name and the birthdate the EHR gives; none for an anonymous patient.
        Identity: PatientIdentity option
        /// Every other patient characteristic.
        Data: PatientData
    }


module AgeType =


    let normalValues =
        let female =
            [
                NewBorn, 3.6m<kg>, 52.5m<cm>
                Infant, 4m<kg>, 54m<cm>
                Toddler, 9.5m<kg>, 75m<cm>
                Child, 14.5m<kg>, 97m<cm>
                Adolescent, 33.5m<kg>, 143m<cm>
                Adult, 58m<kg>, 170m<cm>
            ]

        let male =
            [
                NewBorn, 3.8m<kg>, 51m<cm>
                Infant, 4.2m<kg>, 55m<cm>
                Toddler, 10m<kg>, 76m<cm>
                Child, 15m<kg>, 98m<cm>
                Adolescent, 34m<kg>, 144m<cm>
                Adult, 68m<kg>, 182m<cm>
            ]

        let unknown =
            [
                for f, m in female |> List.zip male do
                    f |> Tuple.fstOf3,
                    List.average [ f |> Tuple.sndOf3; m |> Tuple.sndOf3 ],
                    List.average [ f |> Tuple.thrdOf3; m |> Tuple.thrdOf3 ]

            ]

        [ Female, female; Male, male; UnknownGender, unknown ]


    let getNormal gend age =
        normalValues
        |> List.find (fst >> ((=) gend))
        |> snd
        |> List.tryFind (Tuple.fstOf3 >> ((=) age))
        |> Option.map (fun (_, w, h) -> (w, h))


module AgeValue =


    let create years months weeks days =
        {
            Years = years
            Months = months
            Weeks = weeks
            Days = days
        }

    let get
        {
            Years = y
            Months = m
            Weeks = w
            Days = d
        }
        =
        y, m, w, d


    let none = create None None None None

    let zero = create (Some 0<year>) None None None

    let oneMonth = create None (Some 1<month>) None None

    let one = create (Some 1<year>) None None None

    let three = create (Some 3<year>) None None None

    let ten = create (Some 10<year>) None None None

    let sixteen = create (Some 16<year>) None None None

    let eighteen = create (Some 18<year>) None None None


    let toString (av: AgeValue) =
        Calculations.Age.ageToStringNlShort av.Years av.Months av.Weeks av.Days


    module Optics =

        let years = (fun (a: AgeValue) -> a.Years), (fun y (a: AgeValue) -> { a with Years = y })

        let months = (fun (a: AgeValue) -> a.Months), (fun m (a: AgeValue) -> { a with Months = m })

        let weeks = (fun (a: AgeValue) -> a.Weeks), (fun w (a: AgeValue) -> { a with Weeks = w })

        let days = (fun (a: AgeValue) -> a.Days), (fun d (a: AgeValue) -> { a with Days = d })


    module SetGet =

        let getYears (a: AgeValue) = a.Years

        let setYears y (a: AgeValue) = { a with Years = y }

        let getMonths (a: AgeValue) = a.Months

        let setMonths m (a: AgeValue) = { a with Months = m }

        let getWeeks (a: AgeValue) = a.Weeks

        let setWeeks w (a: AgeValue) = { a with Weeks = w }

        let getDays (a: AgeValue) = a.Days

        let setDays d (a: AgeValue) = { a with Days = d }

        let getYearsDef0 = getYears >> Option.defaultValue 0<year>

        let getMonthsDef0 = getMonths >> Option.defaultValue 0<month>

        let getWeeksDef0 = getWeeks >> Option.defaultValue 0<week>

        let getDaysDef0 = getDays >> Option.defaultValue 0<day>

        let getIntYears (a: AgeValue) = a.Years |> Option.map int |> Option.defaultValue 0

        let setIntYears i (a: AgeValue) = { a with Years = Conversions.yearFromInt i |> Some }

        let getIntMonths (a: AgeValue) = a.Months |> Option.map int |> Option.defaultValue 0

        let setIntMonths i (a: AgeValue) = { a with Months = Conversions.monthFromInt i |> Some }

        let getIntWeeks (a: AgeValue) = a.Weeks |> Option.map int |> Option.defaultValue 0

        let setIntWeeks i (a: AgeValue) = { a with Weeks = Conversions.weekFromInt i |> Some }

        let getIntDays (a: AgeValue) = a.Days |> Option.map int |> Option.defaultValue 0

        let setIntDays i (a: AgeValue) = { a with Days = Conversions.dayFromInt i |> Some }


    let getDef0 av =
        av |> SetGet.getYearsDef0, av |> SetGet.getMonthsDef0, av |> SetGet.getWeeksDef0, av |> SetGet.getDaysDef0


    let getAgeInDays av =
        let ys, ms, ws, ds = av |> getDef0
        Calculations.Age.yearsMonthsWeeksDaysToDays ys ms ws ds


    /// The age in days as a time value in days.
    let toValueUnit av = av |> getAgeInDays |> ValueUnits.ValueUnit.Measures.fromIntDay


    /// Splits a number of days into years of 365 days, months of 30 days (at most 11), weeks and days.
    /// getAgeInDays of the result returns the same number of days.
    let fromDays (days: int<day>) =
        let days = int days
        let ys = days / Constants.daysInYear
        let afterYears = days - ys * Constants.daysInYear
        // Operators.min is needed because min alone is the unit of measure for minutes here.
        let ms = afterYears / Constants.daysInMonth |> Operators.min (Constants.monthsInYear - 1)

        let rest = afterYears - ms * Constants.daysInMonth

        create
            (ys |> Conversions.yearFromInt |> Some)
            (ms |> Conversions.monthFromInt |> Some)
            (rest / 7 |> Conversions.weekFromInt |> Some)
            (rest % 7 |> Conversions.dayFromInt |> Some)


    /// A time value as an age value, rounded down to whole days.
    let fromValueUnit vu = vu |> ValueUnits.ValueUnit.Measures.toSingleIntDay |> fromDays


    /// Returns the age at a date of a patient born on the birth date, from the whole days between the two
    /// dates, split by fromDays. Raises an argument exception for a birth date after the date.
    let fromBirthDate (now: DateTime) (bd: BirthDate) =
        // The BirthDate module comes later in the file, so the date is built here.
        let birth = DateTime(int bd.Year, int bd.Month, int bd.Day)

        if birth > now.Date then
            invalidArg
                (nameof bd)
                $"the birth date %s{birth.ToShortDateString()} is after the date %s{now.Date.ToShortDateString()}"

        (now.Date - birth).Days |> Conversions.dayFromInt |> fromDays


    let comp op a1 a2 =
        let a1 = a1 |> getAgeInDays
        let a2 = a2 |> getAgeInDays
        a1 |> op <| a2

    module Operators =

        let (<?) a1 a2 = comp (<) a1 a2

        let (<=?) a1 a2 = comp (<=) a1 a2

        let (>?) a1 a2 = comp (>) a1 a2

        let (>=?) a1 a2 = comp (>=) a1 a2


    open Operators


    let toAgeType a =
        match a with
        | _ when a >=? zero && a <? oneMonth -> NewBorn
        | _ when a >=? oneMonth && a <? one -> Infant
        | _ when a >=? one && a < three -> Toddler
        | _ when a >=? three && a <? ten -> Child
        | _ when a >=? ten && a <? eighteen -> Adolescent
        | _ when a >=? eighteen -> Adult
        | _ -> UnknownAgeType


    let fromAgeType =
        function
        | NewBorn -> zero
        | Infant -> oneMonth
        | Toddler -> one
        | Child -> three
        | Adolescent -> ten
        | Adult -> eighteen
        | _ -> none


    module Validation =

        open Validus

        [<Literal>]
        let maxYear = 120<year>


        let yearValidator =
            let m = int maxYear
            Check.optional (Check.Int.between 0 m) |> mapOpt Conversions.yearFromInt


        /// The years of an age the user picks from.
        let yearOptions = [ 0 .. int maxYear ] |> List.map Conversions.yearFromInt


        /// The highest month of an age.
        let maxMonth = 11


        let monthValidator = Check.optional (Check.Int.between 0 maxMonth) |> mapOpt Conversions.monthFromInt


        /// The months of an age the user picks from.
        let monthOptions = [ 0..maxMonth ] |> List.map Conversions.monthFromInt


        /// The highest week of an age.
        let maxWeek = 4


        let weekValidator = Check.optional (Check.Int.between 0 maxWeek) |> mapOpt Conversions.weekFromInt


        /// The weeks of an age the user picks from.
        let weekOptions = [ 0..maxWeek ] |> List.map Conversions.weekFromInt


        /// The highest day of an age.
        let maxDay = 6


        let dayValidator = Check.optional (Check.Int.between 0 maxDay) |> mapOpt Conversions.dayFromInt


        /// The days of an age the user picks from.
        let dayOptions = [ 0..maxDay ] |> List.map Conversions.dayFromInt


    module Dto =

        open Validus

        type Dto() =
            member val Years: int option = None with get, set
            member val Months: int option = None with get, set
            member val Weeks: int option = None with get, set
            member val Days: int option = None with get, set


        let dto () = Dto()


        let fromDto (dto: Dto) =
            validate {
                let! y = Validation.yearValidator "Years" dto.Years
                let! m = Validation.monthValidator "Months" dto.Months
                let! w = Validation.weekValidator "Weeks" dto.Weeks
                let! d = Validation.dayValidator "Days" dto.Days
                return create y m w d
            }


        let toDto (a: AgeValue) =
            let dto = dto ()
            dto.Years <- a.Years |> Option.map int
            dto.Months <- a.Months |> Option.map int
            dto.Weeks <- a.Weeks |> Option.map int
            dto.Days <- a.Days |> Option.map int

            dto


[<RequireQualifiedAccess>]
module AdultMark =

    open AgeValue.Operators


    /// Returns the adult mark for an age and the mark the user set. Under 16 years the patient is not an
    /// adult, from 18 years the patient is an adult. From 16 to under 18 years, or without an age, the mark
    /// the user set decides.
    let ofAge age marked =
        match age with
        | None -> AdultMark.MarkableAdult marked
        | Some age when age <? AgeValue.sixteen -> AdultMark.NotAdult
        | Some age when age <? AgeValue.eighteen -> AdultMark.MarkableAdult marked
        | Some _ -> AdultMark.Adult


    /// True from 18 years, or when the user marked a patient of 16 to under 18 years or without an age.
    let isAdult mark =
        match mark with
        | AdultMark.Adult
        | AdultMark.MarkableAdult true -> true
        | AdultMark.NotAdult
        | AdultMark.MarkableAdult false -> false


[<RequireQualifiedAccess>]
module GestationalAgeNeed =

    open AgeValue.Operators


    /// The age under which the gestational age is needed: 28 days.
    let requiredUnder = AgeValue.create None None None (Some 28<day>)


    /// Returns the need for the gestational age at an age: required under 28 days, optional under 1 year,
    /// not used from 1 year or without an age.
    let ofAge age =
        match age with
        | None -> GestationalAgeNeed.NoGestationalAge
        | Some age when age <? requiredUnder -> GestationalAgeNeed.RequiredGestationalAge
        | Some age when age <? AgeValue.one -> GestationalAgeNeed.OptionalGestationalAge
        | Some _ -> GestationalAgeNeed.NoGestationalAge


module BirthDate =

    let create y m d =
        {
            Year = y
            Month = m
            Day = d
        }


    let get
        {
            Year = y
            Month = m
            Day = d
        }
        =
        y, m, d


    module Optics =

        let year = (fun (ymd: BirthDate) -> ymd.Year), (fun i ymd -> { ymd with Year = i })

        let month = (fun (ymd: BirthDate) -> ymd.Month), (fun i ymd -> { ymd with Month = i })

        let day = (fun (ymd: BirthDate) -> ymd.Day), (fun i ymd -> { ymd with Day = i })


    module SetGet =

        let getYear (bd: BirthDate) = bd.Year

        let setYear y (bd: BirthDate) = { bd with Year = y }

        let getIntYear (bd: BirthDate) = bd.Year |> int

        let setIntYear i (bd: BirthDate) = { bd with Year = Conversions.yearFromInt i }


        let getMonth (bd: BirthDate) = bd.Month

        let setMonth m (bd: BirthDate) = { bd with Month = m }

        let getIntMonth (bd: BirthDate) = bd.Month |> int

        let setIntMonth i (bd: BirthDate) = { bd with Month = Conversions.monthFromInt i }


        let getDay (bd: BirthDate) = bd.Day

        let setDay d (bd: BirthDate) = { bd with Day = d }

        let getIntDay (bd: BirthDate) = bd.Day |> int

        let setIntDay i (bd: BirthDate) = { bd with Day = Conversions.dayFromInt i }


    let toDate bd =
        let y, m, d = bd |> SetGet.getIntYear, bd |> SetGet.getIntMonth, bd |> SetGet.getIntDay

        DateTime(y, m, d)


    let fromDate (dt: DateTime) =
        let y, m, d =
            dt.Year |> Conversions.yearFromInt, dt.Month |> Conversions.monthFromInt, dt.Day |> Conversions.dayFromInt

        create y m d


    module Validation =

        open Validus


        let yearValidator = Check.Int.greaterThanOrEqualTo 1900 |> map Conversions.yearFromInt

        let monthValidator = Check.Int.between 1 12 |> map Conversions.monthFromInt


        let dayValidator = Check.Int.between 1 31 |> map Conversions.dayFromInt

        let ageValidator =
            let msg = sprintf "age cannot be > 120 years %s"

            let rule (dt, y: int<year>, m: int<month>, d: int<day>) =
                let y = y |> int
                let m = m |> int
                let d = d |> int

                try
                    let tot = (dt - DateTime(y, m, d)).TotalDays |> int
                    tot < (Constants.daysInYear * 120)
                with _ ->
                    false

            Validator.create msg rule


    module Dto =

        open Validus

        type Dto() =
            member val Year = 0 with get, set
            member val Month = 0 with get, set
            member val Day = 0 with get, set


        let dto () = Dto()


        /// Validates a Dto as a birth date: a valid date from 1900 on, not after today and less than 120 years
        /// of 365 days before today.
        let fromDto (today: DateTime) (dto: Dto) =
            validate {
                let! y = Validation.yearValidator "Year" dto.Year
                let! m = Validation.monthValidator "Month" dto.Month
                let! d = Validation.dayValidator "Day" dto.Day

                let! _ = Validators.dateValidator "Valid date" (y, m, d)

                and! _ = Validation.ageValidator "Valid Age" (today, y, m, d)

                let birthDate = create y m d

                // A let! and not an and!: toDate needs a date that the date validator accepted.
                let! _ = Validators.notAfterValidator today "Birth date" (birthDate |> toDate)

                return birthDate
            }


        let toDto (ymd: BirthDate) =
            let dto = dto ()

            dto.Year <- ymd.Year |> int
            dto.Month <- ymd.Month |> int
            dto.Day <- ymd.Day |> int

            dto


module WeightValue =


    let map fKg fGram =
        function
        | Kilogram x -> x |> fKg
        | Gram x -> x |> fGram


    let apply fKg fGram = map (fKg >> Kilogram) (fGram >> Gram)


    let weightInGram x = x |> int |> Conversions.gramFromInt |> Gram

    let weightInKg x = x |> Conversions.kgFromDecimal |> Kilogram


    let getValue =
        function
        | Kilogram x -> true, decimal x
        | Gram x -> false, x |> int |> decimal


    let getWeightInKg = map id Conversions.intGramToDecKg


    let fromAgeType gend =
        (AgeType.getNormal gend) >> (Option.map fst) >> (Option.map Kilogram)


    let avgNewBorn = NewBorn |> fromAgeType UnknownGender |> Option.get

    let avgInfant = Infant |> fromAgeType UnknownGender |> Option.get

    let avgToddler = Toddler |> fromAgeType UnknownGender |> Option.get

    let avgChild = Child |> fromAgeType UnknownGender |> Option.get

    let avgAdolescent = Adolescent |> fromAgeType UnknownGender |> Option.get

    let avgAdult = Adult |> fromAgeType UnknownGender |> Option.get


    module Validation =

        open Validus
        open Validus.Operators

        let minWeightKg = 0.2m<kg>

        let maxWeightKg = 250m<kg>

        let minWeightGram = (minWeightKg |> Conversions.decKgToIntGram)

        let maxWeightGram = (maxWeightKg |> Conversions.decKgToIntGram)


        let weightKgValidator =
            let minW = decimal minWeightKg
            let maxW = decimal maxWeightKg

            Check.Decimal.between minW maxW *|* weightInKg


        let weightGramValidator =
            let minW = decimal minWeightGram
            let maxW = decimal maxWeightGram

            Check.Decimal.between minW maxW *|* weightInGram


    let toString fixPrec =
        function
        | Kilogram v -> $"{v |> decimal |> Decimal.fixPrecision fixPrec} kg"
        | Gram v -> $"{v} gram"


    module Dto =

        type Dto() =
            member val Weight = 0m with get, set
            member val WeightInKg = true with get, set

        let dto () = Dto()


        let fromDto (dto: Dto) =
            if dto.WeightInKg then
                dto.Weight |> Validation.weightKgValidator "Weight in kg"
            else
                dto.Weight |> Validation.weightGramValidator "Weight in gram"


        let toDto (wv: WeightValue) =
            let dto = dto ()
            let b, v = wv |> getValue

            dto.Weight <- v
            dto.WeightInKg <- b

            dto


module WeightAtDate =

    /// Creates a weight at a date; a date of None gives a weight without a date.
    let create dt wv =
        {
            Date = dt
            Weight = wv
        }


    /// Creates a weight without a date.
    let undated = create None


    /// Returns the normal weight of an age type for a gender as an undated weight.
    let fromAgeType gend = (WeightValue.fromAgeType gend) >> (Option.map undated)


    module Optics =


        let date = (fun (w: WeightAtDate) -> w.Date), (fun dt w -> { w with WeightAtDate.Date = dt })

        let weight = (fun (w: WeightAtDate) -> w.Weight), (fun wv w -> { w with WeightAtDate.Weight = wv })


    module SetGet =

        open Informedica.Utils.Lib.Optics

        let getDate = Optics.date |> Optic.get

        let getWeight = Optics.weight |> Optic.get


    /// Writes the weight, followed by the date in brackets when the weight has a date.
    let toString fixPrec w =
        let ws = w |> SetGet.getWeight |> WeightValue.toString fixPrec

        match w |> SetGet.getDate with
        | Some dt ->
            let ds = dt.ToString("dd-MMM-yy")
            $"%s{ws} (%s{ds})"
        | None -> ws


    module Dto =

        open Validus
        open FsToolkit.ErrorHandling

        type Dto() =
            /// The date of the weight; None for an undated weight.
            member val DateTime: DateTime option = None with get, set
            member val WeightValue = WeightValue.Dto.dto () with get, set

        let dto () = Dto()


        /// Validates a Dto as a weight: a weight within the bounds, and a date on or before today when the
        /// Dto has a date. A Dto without a date gives an undated weight.
        let fromDto (today: DateTime) (dto: Dto) =
            validate {
                let! value = WeightValue.Dto.fromDto dto.WeightValue
                let! dt =
                    dto.DateTime
                    |> Option.traverseResult (Validators.notAfterValidator today "Date")

                return create dt value
            }


        let toDto (w: WeightAtDate) =
            let dto = dto ()

            dto.WeightValue <- w.Weight |> WeightValue.Dto.toDto
            dto.DateTime <- w.Date

            dto


module Source =

    /// The value, measured or estimated.
    let value =
        function
        | Measured v
        | Estimated v -> v


    /// Whether the value was measured.
    let isMeasured =
        function
        | Measured _ -> true
        | Estimated _ -> false


    /// Returns Estimated v when estimated is true, else Measured v.
    let fromDto estimated v = if estimated then Estimated v else Measured v


module Weight =

    let create act birth adm calc =
        {
            Actual = act
            Birth = birth
            Admission = adm
            Calculation = calc
        }


    let unknown = create [] None [] None


    /// True when the calculation weight is measured; false for an estimate or without a calculation weight.
    let isMeasured (w: Weight) = w.Calculation |> Option.exists Source.isMeasured


    /// Creates a weight with the normal weight of the age type as an estimated calculation weight without a
    /// date.
    let fromAgeType gend =
        WeightAtDate.fromAgeType gend >> Option.map Estimated >> create [] None []


    module Optics =

        let actual = (fun (w: Weight) -> w.Actual), (fun act w -> { w with Weight.Actual = act })

        let calculation =
            (fun (w: Weight) -> w.Calculation), (fun calc w -> { w with Weight.Calculation = calc })

        let birth = (fun (w: Weight) -> w.Birth), (fun b w -> { w with Weight.Birth = b })

        let admission = (fun (w: Weight) -> w.Admission), (fun a w -> { w with Weight.Admission = a })


    module SetGet =

        open Informedica.Utils.Lib.Optics

        let getActual = Optic.get Optics.actual

        let getBirth = Optic.get Optics.birth

        let getAdmission = Optic.get Optics.admission

        let getCalculation = Optic.get Optics.calculation


    /// Writes the weights. The lists are sorted from the latest date to the earliest, with the undated
    /// weights last.
    let toString fixPrec w =
        w
        |> SetGet.getActual
        |> List.sortByDescending WeightAtDate.SetGet.getDate
        |> List.map (WeightAtDate.toString fixPrec),

        w |> SetGet.getBirth |> Option.map (WeightValue.toString fixPrec),

        w
        |> SetGet.getAdmission
        |> List.sortByDescending WeightAtDate.SetGet.getDate
        |> List.map (WeightAtDate.toString fixPrec),

        w
        |> SetGet.getCalculation
        |> Option.map (Source.value >> WeightAtDate.toString fixPrec)


    let actualToString fixPrec w = toString fixPrec w |> fun (s, _, _, _) -> s


    let birthToString fixPrec w = toString fixPrec w |> fun (_, s, _, _) -> s


    let admissionToString fixPrec w = toString fixPrec w |> fun (_, _, s, _) -> s


    let calculationToString fixPrec w = toString fixPrec w |> fun (_, _, _, s) -> s


    module Dto =

        open Validus
        open FsToolkit.ErrorHandling

        type Dto() =
            member val Actual: WeightAtDate.Dto.Dto list = [] with get, set
            member val Calculation: WeightAtDate.Dto.Dto option = None with get, set
            /// True when the calculation weight is an estimate; not used without a calculation weight.
            member val CalculationEstimated = false with get, set
            member val Birth: WeightValue.Dto.Dto option = None with get, set
            member val Admission: WeightAtDate.Dto.Dto list = [] with get, set


        let dto () = Dto()


        /// Validates a Dto as weights. Returns the error of the first invalid weight: a value outside the
        /// bounds, or a date after today.
        let fromDto today (dto: Dto) =
            validate {
                let! act = dto.Actual |> List.traverseResultM (WeightAtDate.Dto.fromDto today)
                let! calc = dto.Calculation |> Option.traverseResult (WeightAtDate.Dto.fromDto today)
                let! birth = dto.Birth |> Option.traverseResult WeightValue.Dto.fromDto
                let! adm = dto.Admission |> List.traverseResultM (WeightAtDate.Dto.fromDto today)

                return create act birth adm (calc |> Option.map (Source.fromDto dto.CalculationEstimated))
            }


        let toDto (w: Weight) =
            let dto = dto ()

            dto.Actual <- w.Actual |> List.map WeightAtDate.Dto.toDto
            dto.Birth <- w.Birth |> Option.map WeightValue.Dto.toDto
            dto.Admission <- w.Admission |> List.map WeightAtDate.Dto.toDto

            dto.Calculation <- w.Calculation |> Option.map (Source.value >> WeightAtDate.Dto.toDto)
            dto.CalculationEstimated <- w.Calculation |> Option.exists (Source.isMeasured >> not)

            dto


module HeightValue =


    let map fM fCm =
        function
        | Meter x -> x |> fM
        | Centimeter x -> x |> fCm


    let apply fM fCm = map (fM >> Meter) (fCm >> Centimeter)


    let heightInMeter x = x |> Conversions.meterFromDecimal |> Meter


    let heightInCm (x: decimal) = x |> Conversions.cmFromDecimal |> Centimeter


    let getValue =
        function
        | Meter x -> true, decimal x
        | Centimeter x -> false, x |> decimal


    let getHeightCm = map Conversions.decMtoDecCm (decimal >> Conversions.cmFromDecimal)


    let fromAgeType gend =
        (AgeType.getNormal gend) >> (Option.map snd) >> (Option.map Centimeter)


    let avgNewBorn = NewBorn |> fromAgeType UnknownGender |> Option.get

    let avgInfant = Infant |> fromAgeType UnknownGender |> Option.get

    let avgToddler = Toddler |> fromAgeType UnknownGender |> Option.get

    let avgChild = Child |> fromAgeType UnknownGender |> Option.get

    let avgAdolescent = Adolescent |> fromAgeType UnknownGender |> Option.get

    let avgAdult = Adult |> fromAgeType UnknownGender |> Option.get


    module Validation =

        open Validus
        open Validus.Operators

        let minHeightMeter = 0.2m<m>

        let maxHeightMeter = 3m<m>

        let minHeightCm = (minHeightMeter |> Conversions.decMtoIntCm)

        let maxHeightCm = (maxHeightMeter |> Conversions.decMtoIntCm)


        let heightMeterValidator =
            let minW = decimal minHeightMeter
            let maxW = decimal maxHeightMeter

            Check.Decimal.between minW maxW *|* heightInMeter


        let heightCmValidator =
            let minW = decimal minHeightCm
            let maxW = decimal maxHeightCm

            Check.Decimal.between minW maxW *|* heightInCm


    let toString fixPrec =
        function
        | Meter v -> $"{v |> decimal |> Decimal.fixPrecision fixPrec} meter"
        | Centimeter v -> $"{v} cm"


    module Dto =

        type Dto() =
            member val Height = 0m with get, set
            member val HeightInMeter = true with get, set

        let dto () = Dto()


        let fromDto (dto: Dto) =
            if dto.HeightInMeter then
                dto.Height |> Validation.heightMeterValidator "Height in meter"
            else
                dto.Height |> Validation.heightCmValidator "Height in cm"


        let toDto (wv: HeightValue) =
            let dto = dto ()
            let b, v = wv |> getValue

            dto.Height <- v
            dto.HeightInMeter <- b

            dto


module HeightAtDate =

    /// Creates a height at a date; a date of None gives a height without a date.
    let create dt hv =
        {
            Date = dt
            Height = hv
        }


    /// Creates a height without a date.
    let undated = create None


    /// Returns the normal height of an age type for a gender as an undated height.
    let fromAgeType gend = (HeightValue.fromAgeType gend) >> (Option.map undated)


    module Optics =


        let date = (fun (h: HeightAtDate) -> h.Date), (fun dt h -> { h with HeightAtDate.Date = dt })

        let height = (fun (h: HeightAtDate) -> h.Height), (fun hv h -> { h with HeightAtDate.Height = hv })


    module SetGet =

        open Informedica.Utils.Lib.Optics

        let getDate = Optics.date |> Optic.get

        let getHeight = Optics.height |> Optic.get


    /// Writes the height, followed by the date in brackets when the height has a date.
    let toString fixPrec h =
        let hs = h |> SetGet.getHeight |> HeightValue.toString fixPrec

        match h |> SetGet.getDate with
        | Some dt ->
            let ds = dt.ToString("dd-MMM-yy")
            $"%s{hs} (%s{ds})"
        | None -> hs


    module Dto =

        open Validus
        open FsToolkit.ErrorHandling

        type Dto() =
            /// The date of the height; None for an undated height.
            member val DateTime: DateTime option = None with get, set
            member val HeightValue = HeightValue.Dto.dto () with get, set

        let dto () = Dto()


        /// Validates a Dto as a height: a height within the bounds, and a date on or before today when the
        /// Dto has a date. A Dto without a date gives an undated height.
        let fromDto (today: DateTime) (dto: Dto) =
            validate {
                let! value = HeightValue.Dto.fromDto dto.HeightValue
                let! dt =
                    dto.DateTime
                    |> Option.traverseResult (Validators.notAfterValidator today "Date")

                return create dt value
            }


        let toDto (w: HeightAtDate) =
            let dto = dto ()

            dto.HeightValue <- w.Height |> HeightValue.Dto.toDto
            dto.DateTime <- w.Date

            dto


module Height =

    let create act birth calc =
        {
            Actual = act
            Birth = birth
            Calculation = calc
        }


    let unknown = create [] None None


    /// True when the calculation height is measured; false for an estimate or without a calculation height.
    let isMeasured (h: Height) = h.Calculation |> Option.exists Source.isMeasured


    /// Creates a height with the normal height of the age type as an estimated calculation height without a
    /// date.
    let fromAgeType gend =
        HeightAtDate.fromAgeType gend >> Option.map Estimated >> create [] None


    module Optics =

        let actual = (fun (h: Height) -> h.Actual), (fun act h -> { h with Height.Actual = act })

        let calculation =
            (fun (h: Height) -> h.Calculation), (fun calc h -> { h with Height.Calculation = calc })

        let birth = (fun (h: Height) -> h.Birth), (fun b h -> { h with Height.Birth = b })


    module SetGet =

        open Informedica.Utils.Lib.Optics

        let getActual = Optic.get Optics.actual

        let getBirth = Optic.get Optics.birth

        let getCalculation = Optic.get Optics.calculation


    /// Writes the heights. The list is sorted from the latest date to the earliest, with the undated
    /// heights last.
    let toString fixPrec h =
        h
        |> SetGet.getActual
        |> List.sortByDescending HeightAtDate.SetGet.getDate
        |> List.map (HeightAtDate.toString fixPrec),

        h |> SetGet.getBirth |> Option.map (HeightValue.toString fixPrec),

        h
        |> SetGet.getCalculation
        |> Option.map (Source.value >> HeightAtDate.toString fixPrec)


    let actualToString fixPrec w = toString fixPrec w |> fun (s, _, _) -> s


    let birthToString fixPrec w = toString fixPrec w |> fun (_, s, _) -> s


    let calculationToString fixPrec w = toString fixPrec w |> fun (_, _, s) -> s


    module Dto =

        open Validus
        open FsToolkit.ErrorHandling

        type Dto() =
            member val Actual: HeightAtDate.Dto.Dto list = [] with get, set
            member val Birth: HeightValue.Dto.Dto option = None with get, set
            member val Calculation: HeightAtDate.Dto.Dto option = None with get, set
            /// True when the calculation height is an estimate; not used without a calculation height.
            member val CalculationEstimated = false with get, set


        let dto () = Dto()


        /// Validates a Dto as heights. Returns the error of the first invalid height: a value outside the
        /// bounds, or a date after today.
        let fromDto today (dto: Dto) =
            validate {
                let! act = dto.Actual |> List.traverseResultM (HeightAtDate.Dto.fromDto today)
                let! calc = dto.Calculation |> Option.traverseResult (HeightAtDate.Dto.fromDto today)
                let! birth = dto.Birth |> Option.traverseResult HeightValue.Dto.fromDto

                return create act birth (calc |> Option.map (Source.fromDto dto.CalculationEstimated))
            }


        let toDto (w: Height) =
            let dto = dto ()

            dto.Actual <- w.Actual |> List.map HeightAtDate.Dto.toDto
            dto.Birth <- w.Birth |> Option.map HeightValue.Dto.toDto

            dto.Calculation <- w.Calculation |> Option.map (Source.value >> HeightAtDate.Dto.toDto)
            dto.CalculationEstimated <- w.Calculation |> Option.exists (Source.isMeasured >> not)

            dto


/// Converts an access to and from its string form in the Dto.
module Access =

    /// Returns the string form of an access.
    let toString =
        function
        | PVL -> "pvl"
        | CVL -> "cvl"
        | EnteralTube -> "enteral-tube"


    /// Returns the access a string names, ignoring case and outer white space; None for any other string.
    let tryFromString s =
        match s |> String.toLower |> String.trim with
        | "pvl" -> Some PVL
        | "cvl" -> Some CVL
        | "enteral-tube" -> Some EnteralTube
        | _ -> None


module EGFRBand =

    /// Returns the band of a calculated eGFR; None for a negative, infinite or NaN eGFR.
    let ofEGFR (eGfr: float<mL / min / normalM2>) =
        match float eGfr with
        | x when x |> Double.IsFinite |> not -> None
        | x when x > 50. -> Some Above50
        | x when x > 30. -> Some From30To50
        | x when x > 10. -> Some From10To30
        | x when x >= 0. -> Some UpTo10
        | _ -> None


    /// Returns the lower and the upper limit of a band as text, with an empty text where the band has no limit.
    let limits =
        function
        | Above50 -> "50", ""
        | From30To50 -> "30", "50"
        | From10To30 -> "10", "30"
        | UpTo10 -> "", "10"


/// Converts a renal function to and from its string form in the Dto: egfr:min:max, with an empty bound where
/// the band has none, or the name of the dialysis.
module RenalFunction =

    /// Returns the string form of a renal function.
    let toString =
        function
        | EGFR band ->
            let minimum, maximum = band |> EGFRBand.limits
            $"egfr:%s{minimum}:%s{maximum}"
        | IntermittentHemodialysis -> "intermittent-hemodialysis"
        | ContinuousHemodialysis -> "continuous-hemodialysis"
        | PeritonealDialysis -> "peritoneal-dialysis"


    /// Returns the renal function a string names, ignoring case and outer white space; None for any other
    /// string, such as an eGFR range that is not a band.
    let tryFromString s =
        let s = s |> String.toLower |> String.trim

        [
            EGFR Above50
            EGFR From30To50
            EGFR From10To30
            EGFR UpTo10
            IntermittentHemodialysis
            ContinuousHemodialysis
            PeritonealDialysis
        ]
        |> List.tryFind (toString >> (=) s)


/// Reads the cells of sheet rows by the column names in the header row.
[<RequireQualifiedAccess>]
module SheetRows =

    /// Returns the required columns that the header lacks, in the order of the required columns.
    let missingColumns header required =
        required |> List.filter (fun c -> header |> Array.contains c |> not)


    /// Returns the trimmed cell of a row in a column of the header, or an empty text when the row is shorter
    /// than the header. The column must be in the header.
    let cell header (row: string[]) column =
        let i = header |> Array.findIndex ((=) column)
        if i < row.Length then row[i] |> String.trim else ""


[<RequireQualifiedAccess>]
module EGFRBandOptions =

    /// The sheet of the eGFR bands.
    let sheet = "egfr_bands"


    /// The columns the sheet must have.
    let requiredColumns = [ "MinEGFR"; "MaxEGFR"; "Band" ]


    /// The four bands, from high to low.
    let bands = [ EGFRBand.Above50; EGFRBand.From30To50; EGFRBand.From10To30; EGFRBand.UpTo10 ]


    /// Returns the band that a minimum and a maximum name, with an empty text for no limit; None when they name
    /// no band.
    let tryBand minimum maximum =
        match $"egfr:%s{minimum}:%s{maximum}" |> RenalFunction.tryFromString with
        | Some(EGFR band) -> Some band
        | _ -> None


    /// Parses the rows of the sheet, header first, into the options in the order of the bands, with a warning
    /// for every problem. A sheet without rows gives no options and no warnings.
    let ofRows (rows: string[][]) =
        match rows |> Array.tryHead with
        | None -> [], []
        | Some header ->
            let header = header |> Array.map String.trim

            match requiredColumns |> SheetRows.missingColumns header with
            | _ :: _ as missing -> [], [ EGFRBandSheetWarning.MissingColumns missing ]
            | [] ->
                let cell = SheetRows.cell header

                let folder (options, warnings) (n, row) =
                    let minimum = cell row "MinEGFR"
                    let maximum = cell row "MaxEGFR"

                    match tryBand minimum maximum with
                    | None -> options, EGFRBandSheetWarning.NoBand(n, minimum, maximum) :: warnings
                    | Some band when options |> List.exists (fun o -> o.Band = band) ->
                        options, EGFRBandSheetWarning.DuplicateBand n :: warnings
                    | Some band ->
                        {
                            Band = band
                            Text = cell row "Band"
                        }
                        :: options,
                        warnings

                let options, warnings =
                    rows[1..]
                    |> Array.toList
                    |> List.mapi (fun i row -> i + 2, row)
                    |> List.fold folder ([], [])

                let found =
                    bands
                    |> List.map (fun band -> band, options |> List.tryFind (fun o -> o.Band = band))

                let missing =
                    found
                    |> List.filter (snd >> Option.isNone)
                    |> List.map (fst >> EGFRBandSheetWarning.MissingBand)

                found |> List.choose snd, (warnings |> List.rev) @ missing


module Department =


    module Constants =

        [<Literal>]
        let unknown = ""

        [<Literal>]
        let adultICU = "Adult ICU"

        [<Literal>]
        let pediatricICU = "Pediatric ICU"

        [<Literal>]
        let neonatalICU = "Neonatal ICU"

        [<Literal>]
        let adultDepartment = "Adult Department"

        [<Literal>]
        let pediatricDepartment = "Pediatric Department"

        [<Literal>]
        let outpatientClinic = "Outpatient Clinic"

        [<Literal>]
        let otherDepartment = "Department"


    let map unknown fAdultICU fPedICU fNeoICU fAdult fPed fClinic fOther =
        function
        | UnknownDepartment -> unknown
        | AdultICU s -> s |> fAdultICU
        | PediatricICU s -> s |> fPedICU
        | NeonatalICU s -> s |> fNeoICU
        | AdultDepartment s -> s |> fAdult
        | PediatricDepartment s -> s |> fPed
        | OutpatientClinic s -> s |> fClinic
        | OtherDepartment s -> s |> fOther


    let adultICU = AdultICU

    let anyAdultICU = "" |> adultICU

    let pediatricICU = PediatricICU

    let anyPediatricICU = "" |> pediatricICU

    let neonatalICU = NeonatalICU

    let anyNeonatalICU = "" |> neonatalICU

    let adultDepartment = AdultDepartment

    let anyAdultDepartment = "" |> adultDepartment

    let pediatricDepartment = PediatricDepartment

    let anyPediatricDepartment = "" |> pediatricDepartment

    /// An outpatient clinic with a name.
    let outpatientClinic = OutpatientClinic

    /// A department with a name, of a kind that is not known.
    let otherDepartment = OtherDepartment

    let unknown = UnknownDepartment


    let toString =
        map
            ""
            (sprintf "%s %s" Constants.adultICU)
            (sprintf "%s %s" Constants.pediatricICU)
            (sprintf "%s %s" Constants.neonatalICU)
            (sprintf "%s %s" Constants.adultDepartment)
            (sprintf "%s %s" Constants.pediatricDepartment)
            (sprintf "%s %s" Constants.outpatientClinic)
            (sprintf "%s %s" Constants.otherDepartment)
        >> String.trim


    /// Returns the department name the rules match; None for an unknown department or a department without a
    /// name.
    let name =
        let named (s: string) = if s |> String.isNullOrWhiteSpace then None else Some s

        map None named named named named named named named


    /// The Dto of a department: a name for each kind of department.
    module Dto =

        open Validus

        type Dto() =
            member val AdultICU: string option = None with get, set
            member val PediatricICU: string option = None with get, set
            member val NeonatalICU: string option = None with get, set
            member val AdultDepartment: string option = None with get, set
            member val PediatricDepartment: string option = None with get, set
            member val OutpatientClinic: string option = None with get, set
            /// The name of a department of a kind that is not known.
            member val OtherDepartment: string option = None with get, set


        let dto () = Dto()


        /// Validates a Dto as a department. A name is trimmed, and a name field with a blank name counts as
        /// not set. No field set is an unknown department; more than one field set is an error.
        let fromDto (dto: Dto) =
            let named kind name =
                name
                |> Option.map String.trim
                |> Option.filter String.notEmpty
                |> Option.map kind

            let departments =
                [
                    dto.AdultICU |> named adultICU
                    dto.PediatricICU |> named pediatricICU
                    dto.NeonatalICU |> named neonatalICU
                    dto.AdultDepartment |> named adultDepartment
                    dto.PediatricDepartment |> named pediatricDepartment
                    dto.OutpatientClinic |> named outpatientClinic
                    dto.OtherDepartment |> named otherDepartment
                ]
                |> List.choose id

            match departments with
            | [] -> Ok unknown
            | [ dep ] -> Ok dep
            | _ ->
                ValidationErrors.create "Department" [ "Department has more than one field set" ]
                |> Error


        /// The Dto of a department.
        let toDto dep =
            let dto = dto ()

            match dep with
            | AdultICU s -> dto.AdultICU <- Some s
            | PediatricICU s -> dto.PediatricICU <- Some s
            | NeonatalICU s -> dto.NeonatalICU <- Some s
            | AdultDepartment s -> dto.AdultDepartment <- Some s
            | PediatricDepartment s -> dto.PediatricDepartment <- Some s
            | OutpatientClinic s -> dto.OutpatientClinic <- Some s
            | OtherDepartment s -> dto.OtherDepartment <- Some s
            | UnknownDepartment -> ()

            dto


module Gender =


    let male = Male


    let female = Female


    let unknown = UnknownGender


    let toString gend =
        match gend with
        | UnknownGender -> ""
        | _ -> $"{gend}" |> String.toLower


    module Validation =

        open Validus
        open Validus.Operators

        let validate =
            Check.String.equals $"{male |> toString}" *| male
            <|> Check.String.equals $"{female |> toString}" *| female
            <|> Check.String.equals $"{unknown |> toString}" *| unknown


module GestationType =

    let min = 20<week>

    let extreme = 28<week>

    let very = 32<week>

    let moderate = 37<week>

    let max = Constants.weeksInYear |> Conversions.weekFromInt


    let fromWeeks wks =
        match wks with
        | _ when wks < min -> UnknownGestation
        | _ when wks >= min && wks < extreme -> ExtremePreterm
        | _ when wks < very -> VeryPreterm
        | _ when wks < moderate -> ModeratePreterm
        | _ when wks >= moderate && wks <= max -> FullTerm
        | _ -> UnknownGestation


    let toString (gt: GestationType) =
        match gt with
        | UnknownGestation -> ""
        | _ -> $"{gt}"

module AgeWeeksDays =

    let create w d =
        {
            Weeks = w
            Days = d
        }

    let fromWeeks w = create w 0<day>


    module Optics =

        let weeks = (fun (awd: AgeWeeksDays) -> awd.Weeks), (fun ws awd -> { awd with AgeWeeksDays.Weeks = ws })

        let days = (fun (awd: AgeWeeksDays) -> awd.Days), (fun ds awd -> { awd with AgeWeeksDays.Days = ds })


    module SetGet =

        open Informedica.Utils.Lib.Optics

        let getWeeks = Optic.get Optics.weeks

        let getDays = Optic.get Optics.days

        let setWeeks = Optic.set Optics.weeks

        let setDays = Optic.set Optics.days


    let toGestation = SetGet.getWeeks >> GestationType.fromWeeks


    let toDays (awd: AgeWeeksDays) = (awd.Weeks |> Conversions.weeksToDays) + awd.Days


    /// A number of days as weeks and days.
    let fromDays days = days |> Conversions.daysToWeeks ||> create


    /// A time value as weeks and days, rounded down to whole days.
    let fromValueUnit vu = vu |> ValueUnits.ValueUnit.Measures.toSingleIntDay |> fromDays


    /// Weeks and days as a time value in days, the inverse of fromValueUnit.
    let toValueUnit awd = awd |> toDays |> ValueUnits.ValueUnit.Measures.fromIntDay


    /// Returns the post-menstrual age in weeks and days: the age in days plus the gestational age.
    let postMenstrualAge av ga = (av |> AgeValue.getAgeInDays) + (ga |> toDays) |> fromDays


    /// The gestational age at term: 37 weeks, where the FullTerm gestation type starts.
    let term = fromWeeks GestationType.moderate


    let comp op awd1 awd2 = awd1 |> op <| awd2


    module Operators =

        let (>?) awd1 awd2 = comp (>) awd1 awd2

        let (>=?) awd1 awd2 = comp (>=) awd1 awd2

        let (<?) awd1 awd2 = comp (<) awd1 awd2

        let (<=?) awd1 awd2 = comp (<=) awd1 awd2


    open Operators


    /// Whether a gestational age is below term.
    let isPreterm awd = awd <? term


    /// Whether a gestational age given as a time value is below term.
    let isPretermValue vu = vu |> fromValueUnit |> isPreterm


    module Validation =

        open Validus
        open Validus.Operators

        let minWeeks = 20

        let maxWeeks = Constants.weeksInYear

        let maxDays = Constants.daysInWeek - 1


        let validateWeeks = Check.Int.between minWeeks maxWeeks *|* Conversions.weekFromInt


        /// The weeks of a gestational age the user picks from.
        let weekOptions = [ minWeeks..maxWeeks ] |> List.map Conversions.weekFromInt


        let validateDays = Check.Int.between 0 maxDays *|* Conversions.dayFromInt


        /// The days of a gestational age the user picks from.
        let dayOptions = [ 0..maxDays ] |> List.map Conversions.dayFromInt


    let toString awd =
        let s = awd |> toGestation |> GestationType.toString

        if s |> String.isNullOrWhiteSpace then
            ""
        else
            let ds =
                if awd |> SetGet.getDays = 0<day> then
                    ""
                else
                    $" en {awd.Days} dagen"

            $"{s} {awd.Weeks} weken{ds}"


    module Dto =

        open Validus


        type Dto() =
            member val Weeks = 0 with get, set
            member val Days = 0 with get, set


        let dto () = Dto()


        let fromDto (dto: Dto) =
            validate {
                let! ws = dto.Weeks |> Validation.validateWeeks "Weeks"
                let! ds = dto.Days |> Validation.validateDays "Days"
                return create ws ds
            }


        let toDto (awd: AgeWeeksDays) =
            let dto = dto ()

            dto.Weeks <- awd.Weeks |> int
            dto.Days <- awd.Days |> int

            dto


[<RequireQualifiedAccess>]
module Estimates =

    open AgeValue.Operators


    /// Estimates without a weight and without a height.
    let none =
        {
            Weight = None
            Height = None
        }


    /// Truncates a value to a whole number in its unit.
    let toWhole (v: float<'u>) : int<'u> = v |> float |> int |> LanguagePrimitives.Int32WithMeasure


    /// The step nearest to a value: the lower step on a tie, and the first or last step for a value beyond the
    /// steps. The steps are in ascending order.
    let inline nearestStep (steps: _[]) v = steps[steps |> Informedica.Utils.Lib.Array.nearestIndex v]


    /// Returns the percentiles of the row nearest to the age for a gender; None when the table has no row
    /// for the gender.
    let percentilesOfGender (rows: NormalValue<'age, 'u> list) (age: float<'age>) gender =
        match rows |> List.filter (fun nv -> nv.Gender = gender) |> List.toArray with
        | [||] -> None
        | rows ->
            let nv = rows[rows |> Array.map _.Age |> Informedica.Utils.Lib.Array.nearestIndex age]

            Some
                {
                    P3 = nv.P3
                    Mean = nv.Mean
                    P97 = nv.P97
                }


    /// Returns the nearest percentiles at an age for a gender. For an unknown gender, returns the average
    /// of the male and the female percentiles.
    let nearestPercentiles gender (age: float<'age>) (rows: NormalValue<'age, 'u> list) =
        match gender with
        | Male
        | Female -> gender |> percentilesOfGender rows age
        | UnknownGender ->
            Option.map2
                (fun m f ->
                    {
                        P3 = (m.P3 + f.P3) / 2.
                        Mean = (m.Mean + f.Mean) / 2.
                        P97 = (m.P97 + f.P97) / 2.
                    }
                )
                (Male |> percentilesOfGender rows age)
                (Female |> percentilesOfGender rows age)


    /// Returns the age in years, with the months as a fraction, to read the age tables at.
    let tableYears age =
        let y, m, _, _ = age |> AgeValue.getDef0
        (float (int y) + float (int m) / 12.) * 1.<year>


    /// Returns the post-menstrual age in weeks when the neonatal table has a row at or above it; None
    /// otherwise, so an older infant gets the age table and not the value of the last neonatal row.
    let neonatalWeeks pma (rows: NormalValue<week, 'u> list) =
        pma
        |> Option.map (fun pma -> float (int (pma |> AgeWeeksDays.toDays)) / 7. * 1.<week>)
        |> Option.filter (fun weeks -> rows |> List.exists (fun nv -> nv.Age >= weeks))


    /// Truncates table percentiles to whole numbers and rounds the mean to the nearest step.
    let estimate steps (pct: Percentiles<float<'u>>) =
        {
            P3 = pct.P3 |> toWhole
            Mean = pct.Mean |> toWhole |> nearestStep steps
            P97 = pct.P97 |> toWhole
        }


    /// Estimates the weight and height from the tables for a gender, an age and a post-menstrual age, with the
    /// mean rounded to the nearest calculation weight or height; none without an age or from 18 years. Uses the
    /// neonatal table when it has a row at or above the post-menstrual age, otherwise the age table. A weight from
    /// the age table has its 3rd and 97th percentiles truncated to whole kg.
    let ofAge tables (calculationWeightsAndHeights: CalculationWeightsAndHeights) gender age pma =
        match age with
        | None -> none
        | Some age when age >=? AgeValue.eighteen -> none
        | Some age ->
            let neonatal (rows: NormalValue<week, 'u> list) =
                rows
                |> neonatalWeeks pma
                |> Option.bind (fun weeks -> rows |> nearestPercentiles gender weeks)

            let years = age |> tableYears
            let toGram (kg: int<kg>) = kg * 1000<gram / kg>

            let weight =
                match tables.NeoWeights |> neonatal with
                | Some pct -> pct |> estimate calculationWeightsAndHeights.Weights |> Some
                | None ->
                    tables.Weights
                    |> nearestPercentiles gender years
                    |> Option.map (fun pct ->
                        {
                            P3 = pct.P3 |> toWhole |> toGram
                            Mean =
                                pct.Mean * 1000.<gram / kg>
                                |> toWhole
                                |> nearestStep calculationWeightsAndHeights.Weights
                            P97 = pct.P97 |> toWhole |> toGram
                        }
                    )

            let height =
                match tables.NeoHeights |> neonatal with
                | Some pct -> pct |> estimate calculationWeightsAndHeights.Heights |> Some
                | None ->
                    tables.Heights
                    |> nearestPercentiles gender years
                    |> Option.map (estimate calculationWeightsAndHeights.Heights)

            {
                Weight = weight
                Height = height
            }


[<RequireQualifiedAccess>]
module NormalValueTables =

    open FsToolkit.ErrorHandling


    /// The sheet of the weights by age in years.
    let weightSheet = "norm_weight"


    /// The sheet of the heights by age in years.
    let heightSheet = "norm_height"


    /// The sheet of the weights of the newborn by post-menstrual age in weeks.
    let neoWeightSheet = "norm_weight_neo"


    /// The sheet of the heights of the newborn by post-menstrual age in weeks.
    let neoHeightSheet = "norm_height_neo"


    /// The names of the four normal-value sheets.
    let sheets = [ weightSheet; heightSheet; neoWeightSheet; neoHeightSheet ]


    /// The columns a normal-value sheet must have.
    let requiredColumns = [ "sex"; "age"; "p3"; "mean"; "p97" ]


    /// Parses the rows of a normal-value sheet, header first, into a table with the units the caller sets.
    /// Returns every error in the rows. A header without rows gives an empty table; no header gives
    /// MissingColumns.
    let parseSheet sheet (rows: string[][]) : Validation<NormalValue<'age, 'u> list, NormalValueError> =
        match rows |> Array.tryHead with
        | None -> Validation.error (NormalValueError.MissingColumns(sheet, requiredColumns))
        | Some header ->
            let header = header |> Array.map _.Trim()

            match requiredColumns |> SheetRows.missingColumns header with
            | _ :: _ as missing -> Validation.error (NormalValueError.MissingColumns(sheet, missing))
            | [] ->
                let cell = SheetRows.cell header

                let parseRow n (row: string[]) =
                    let number column =
                        cell row column
                        |> Double.tryParse
                        |> Result.requireSome (NormalValueError.NotANumber(sheet, n, column))

                    let gender =
                        match cell row "sex" with
                        | "M" -> Ok Male
                        | "F" -> Ok Female
                        | s -> Error(NormalValueError.UnknownSex(sheet, n, s))

                    validation {
                        let! g = gender
                        and! age = number "age"
                        and! p3 = number "p3"
                        and! mean = number "mean"
                        and! p97 = number "p97"

                        return
                            {
                                Gender = g
                                Age = LanguagePrimitives.FloatWithMeasure age
                                P3 = LanguagePrimitives.FloatWithMeasure p3
                                Mean = LanguagePrimitives.FloatWithMeasure mean
                                P97 = LanguagePrimitives.FloatWithMeasure p97
                            }
                    }

                // A fold with and! keeps the errors of a row in column order; List.sequenceValidationA
                // would reverse them.
                rows[1..]
                |> Array.toList
                |> List.mapi (fun i row -> parseRow (i + 2) row)
                |> List.fold
                    (fun acc row ->
                        validation {
                            let! xs = acc
                            and! x = row
                            return x :: xs
                        }
                    )
                    (Ok [])
                |> Validation.map List.rev


    /// Builds the tables from the rows of the four sheets, keyed by sheet name. Returns every error of every
    /// sheet, with MissingSheet for a sheet that is not in the map.
    let ofRows (rows: Map<string, string[][]>) =
        let table sheet : Validation<NormalValue<'age, 'u> list, NormalValueError> =
            match rows |> Map.tryFind sheet with
            | None -> Validation.error (NormalValueError.MissingSheet sheet)
            | Some sheetRows -> sheetRows |> parseSheet sheet

        validation {
            let! w = table weightSheet
            and! h = table heightSheet
            and! nw = table neoWeightSheet
            and! nh = table neoHeightSheet

            return
                {
                    Weights = w
                    Heights = h
                    NeoWeights = nw
                    NeoHeights = nh
                }
        }


[<RequireQualifiedAccess>]
module CalculationWeightsAndHeights =

    open FsToolkit.ErrorHandling


    /// The sheet of the calculation weights.
    let weightSheet = "calc_weight"


    /// The column of the calculation weights, in kg.
    let weightColumn = "WeightKg"


    /// The sheet of the calculation heights.
    let heightSheet = "calc_height"


    /// The column of the calculation heights, in cm.
    let heightColumn = "HeightCm"


    /// The names of the two sheets.
    let sheets = [ weightSheet; heightSheet ]


    /// Parses the rows of a sheet, header first, into the numbers of a column with their row numbers, each
    /// rounded to a whole value by toWhole. A blank cell is skipped. Returns every error in the rows: a number
    /// that is not positive, a number that rounds to 0, and NoValues when no row holds a value.
    let parseSheet sheet column (toWhole: float -> int<'u>) rows =
        let header = rows |> Array.tryHead |> Option.defaultValue [||] |> Array.map String.trim

        match header |> Array.tryFindIndex ((=) column) with
        | None -> Validation.error (NormalValueSheetError.MissingColumn(sheet, column))
        | Some i ->
            let parseRow n (row: string[]) =
                let cell = if i < row.Length then row[i] |> String.trim else ""

                if cell |> String.isNullOrWhiteSpace then
                    Ok None
                else
                    match cell |> Double.tryParse with
                    | Some v when v > 0. ->
                        match v |> toWhole with
                        | w when int w > 0 -> Ok(Some(n, w))
                        | _ -> Error [ NormalValueSheetError.RoundsToZero(sheet, n, cell) ]
                    | _ -> Error [ NormalValueSheetError.NotAPositiveNumber(sheet, n, cell) ]

            rows[1..]
            |> Array.toList
            |> List.mapi (fun i row -> parseRow (i + 2) row)
            |> List.sequenceValidationA
            |> Validation.map (List.choose id)
            |> Result.bind (fun values ->
                match values with
                | [] -> Validation.error (NormalValueSheetError.NoValues sheet)
                | _ -> Ok values
            )


    /// Returns the values in ascending order without duplicates, with a warning for each row that holds a value
    /// lower than a row above it or a value a row above it already holds.
    let ascending sheet (values: (int * int<'u>) list) =
        let folder (seen: Set<int<'u>>, warnings) (n, v) =
            if seen |> Set.contains v then
                seen, NormalValueSheetWarning.Duplicate(sheet, n) :: warnings
            elif seen |> Set.exists (fun s -> s > v) then
                seen |> Set.add v, NormalValueSheetWarning.NotAscending(sheet, n) :: warnings
            else
                seen |> Set.add v, warnings

        let seen, warnings = values |> List.fold folder (Set.empty, [])
        seen |> Set.toArray, warnings |> List.rev


    /// Builds the calculation weights and heights from the rows of the two sheets, keyed by sheet name. The
    /// weights in kg become whole grams, the heights whole cm. Returns every error of both sheets, with
    /// MissingSheet for a sheet that is not in the map; otherwise the weights and heights with the warnings of
    /// both sheets.
    let ofRows (rows: Map<string, string[][]>) =
        let values sheet column toWhole =
            match rows |> Map.tryFind sheet with
            | None -> Validation.error (NormalValueSheetError.MissingSheet sheet)
            | Some sheetRows -> sheetRows |> parseSheet sheet column toWhole

        let toGram v =
            v * 1.<kg> * 1000.<gram / kg>
            |> float
            |> Math.Round
            |> int
            |> Conversions.gramFromInt

        let toCm v =
            v * 1.<cm> |> float |> Math.Round |> int |> Conversions.cmFromInt

        validation {
            let! w = values weightSheet weightColumn toGram
            and! h = values heightSheet heightColumn toCm

            let weights, ww = w |> ascending weightSheet
            let heights, hw = h |> ascending heightSheet

            return
                {
                    Weights = weights
                    Heights = heights
                },
                ww @ hw
        }


module PatientDataOptions =

    /// The options that need no resources: the age parts, the gestational age parts and the dialyses.
    let withoutResources =
        {
            Weights = [||]
            Heights = [||]
            Years = AgeValue.Validation.yearOptions
            Months = AgeValue.Validation.monthOptions
            Weeks = AgeValue.Validation.weekOptions
            Days = AgeValue.Validation.dayOptions
            GestationalWeeks = AgeWeeksDays.Validation.weekOptions
            GestationalDays = AgeWeeksDays.Validation.dayOptions
            EGFRBands = []
            Dialyses = [ IntermittentHemodialysis; ContinuousHemodialysis; PeritonealDialysis ]
        }


    /// Returns every option, with the weights, the heights and the eGFR bands of the resources.
    let ofResources resources =
        { withoutResources with
            Weights = resources.CalculationWeightsAndHeights.Weights
            Heights = resources.CalculationWeightsAndHeights.Heights
            EGFRBands = resources.EGFRBandOptions
        }


module PatientIdentity =

    /// Creates a patient identity from an id, a name and a birth date.
    let create id name bd : PatientIdentity =
        {
            Id = id
            Name = name
            BirthDate = bd
        }


module PatientData =

    module Measures = Informedica.GenCore.Lib.ValueUnits.ValueUnit.Measures
    module BSA = Informedica.GenCore.Lib.Calculations.BSA

    /// Sets the adult mark and the need for the gestational age from the age. A patient marked as an adult
    /// keeps the mark when the age allows it.
    let determineAdultMarkAndGestationalAgeNeed (dat: PatientData) =
        { dat with
            AdultMark = AdultMark.ofAge dat.Age (dat.AdultMark = AdultMark.MarkableAdult true)
            GestationalAgeNeed = GestationalAgeNeed.ofAge dat.Age
        }


    /// Sets the adult mark from the age and the mark the user asks for. The mark is ignored when the age does
    /// not allow it.
    let markAsAdult marked (dat: PatientData) = { dat with AdultMark = AdultMark.ofAge dat.Age marked }


    /// Creates patient data without a location, an access, a renal function or a mark of the user. The adult
    /// mark and the need for the gestational age follow from the age. The post-menstrual age is None until
    /// determineGestationalAndPostMenstrualAge sets it. Without the resources, the estimates are none and the
    /// options are the options without resources.
    let create dep diagn gend age wght hght gest =
        {
            Location = None
            Department = dep
            Diagnoses = diagn
            Gender = gend
            Age = age
            AdultMark = AdultMark.MarkableAdult false
            GestationalAgeNeed = GestationalAgeNeed.NoGestationalAge
            Weight = wght
            Height = hght
            GestationalAge = gest
            PostMenstrualAge = None
            BodySurfaceArea = None
            Access = []
            RenalFunction = None
            Estimates = Estimates.none
            Options = PatientDataOptions.withoutResources
        }
        |> determineAdultMarkAndGestationalAgeNeed


    /// The patient data with nothing known.
    let unknown = create Department.unknown [||] UnknownGender None Weight.unknown Height.unknown None


    /// The age as a time value in days; none without an age.
    let ageInDays (dat: PatientData) = dat.Age |> Option.map AgeValue.toValueUnit


    /// The gestational age as a time value in days; none without one.
    let gestationalAgeInDays (dat: PatientData) = dat.GestationalAge |> Option.map AgeWeeksDays.toValueUnit


    /// The post-menstrual age as a time value in days; none without one.
    let postMenstrualAgeInDays (dat: PatientData) = dat.PostMenstrualAge |> Option.map AgeWeeksDays.toValueUnit


    /// The calculation weight in kg, measured or estimated; none without one.
    let weightInKg (dat: PatientData) =
        dat.Weight.Calculation
        |> Option.map (Source.value >> _.Weight >> WeightValue.getWeightInKg >> Measures.fromDecimalKg)


    /// The calculation height in cm, measured or estimated; none without one.
    let heightInCm (dat: PatientData) =
        dat.Height.Calculation
        |> Option.map (Source.value >> _.Height >> HeightValue.getHeightCm >> Measures.fromDecimalCm)


    /// Calculates the body surface area with a formula from the calculation weight and height, measured or
    /// estimated; None without both.
    let calcBsa formula (dat: PatientData) =
        let w =
            dat.Weight.Calculation
            |> Option.map (Source.value >> _.Weight >> WeightValue.getWeightInKg)

        let h =
            dat.Height.Calculation
            |> Option.map (Source.value >> _.Height >> HeightValue.getHeightCm)

        match w, h with
        | Some w, Some h -> formula None w h |> Some
        | _ -> None


    /// Sets the body surface area by the Du Bois formula, rounded to 2 significant digits. The body surface area
    /// is None without a calculation weight and height.
    let calculateBodySurfaceArea dat =
        { dat with BodySurfaceArea = dat |> calcBsa (fun _ -> BSA.calcDuBois (Some 2)) }


    /// Returns the body surface area in m2 that is set on the patient data.
    let bodySurfaceArea (dat: PatientData) = dat.BodySurfaceArea |> Option.map Measures.fromDecimalM2


    /// Creates patient data with the age at the start of an age type, and the normal weight and height of
    /// that age type for a gender as undated estimates.
    let fromAgeType gend at =
        let age = at |> AgeValue.fromAgeType |> Some

        create Department.unknown [||] UnknownGender age (Weight.fromAgeType gend at) (Height.fromAgeType gend at) None
        |> calculateBodySurfaceArea


    /// True when the patient data has an age, an adult mark, or a measured weight with a measured height.
    let meetsMinimumData (dat: PatientData) =
        dat.AdultMark |> AdultMark.isAdult
        || dat.Age.IsSome
        || (dat.Weight |> Weight.isMeasured && dat.Height |> Height.isMeasured)


    /// Returns the patient data when it is enough to select rules with, and NoAgeOrMeasuredWeightAndHeight
    /// otherwise.
    let checkMinimumData dat =
        if dat |> meetsMinimumData then
            Ok dat
        else
            Error PatientDataError.NoAgeOrMeasuredWeightAndHeight


    /// Removes the gestational age when the gestational age is not used.
    let ignoreGestationalAge (dat: PatientData) =
        match dat.GestationalAgeNeed with
        | GestationalAgeNeed.NoGestationalAge -> { dat with GestationalAge = None }
        | GestationalAgeNeed.OptionalGestationalAge
        | GestationalAgeNeed.RequiredGestationalAge -> dat


    /// Sets the post-menstrual age to the age in days plus the gestational age; None without either.
    let calculatePostMenstrualAge (dat: PatientData) =
        { dat with PostMenstrualAge = Option.map2 AgeWeeksDays.postMenstrualAge dat.Age dat.GestationalAge }


    /// Removes the gestational age when the gestational age is not used, then calculates the post-menstrual
    /// age.
    let determineGestationalAndPostMenstrualAge dat = dat |> ignoreGestationalAge |> calculatePostMenstrualAge


    /// Sets the values that follow from the age: the adult mark, the need for the gestational age, the
    /// gestational age when it is used, and the post-menstrual age.
    let determineAgeValues dat =
        dat
        |> determineAdultMarkAndGestationalAgeNeed
        |> determineGestationalAndPostMenstrualAge


    /// Rounds a measured calculation value to the nearest step and keeps the measurement date. The exact
    /// measurement is added to the actual values when none of them has the same date and the same value. A value
    /// that is not measured, or that is already a step, is returned unchanged.
    let roundMeasured (steps: decimal<'u>[]) valueOf dateOf ofStep actual calculation =
        match calculation with
        | Some(Measured m) ->
            let v = m |> valueOf
            let step = v |> Estimates.nearestStep steps

            // The values are compared in the unit of the steps, so 12.34 kg and 12340 gram are the same value.
            let isMeasurement a = (a |> dateOf) = (m |> dateOf) && (a |> valueOf) = v

            if step = v then
                actual, calculation
            else
                (if actual |> List.exists isMeasurement then
                     actual
                 else
                     actual @ [ m ]),
                Some(Measured(step |> ofStep (m |> dateOf)))
        | _ -> actual, calculation


    /// Sets the estimates and the options from the resources. A measured calculation weight or height is rounded
    /// to the nearest calculation value; any other becomes the estimated mean, without a date, or none without an
    /// estimate. Without the resources, the patient data is returned unchanged.
    let estimate resources (dat: PatientData) =
        match resources with
        | None -> dat
        | Some res ->
            let est =
                Estimates.ofAge
                    res.NormalValueTables
                    res.CalculationWeightsAndHeights
                    dat.Gender
                    dat.Age
                    dat.PostMenstrualAge

            // A measured calculation value, already rounded, is kept; any other becomes the estimated mean, or none
            // without an estimate.
            let calculation toValue current (pct: Percentiles<_> option) =
                match current with
                | Some(Measured _) -> current
                | _ -> pct |> Option.map (_.Mean >> toValue >> Estimated)

            let weight = int >> decimal >> WeightValue.weightInGram >> WeightAtDate.undated
            let height = int >> decimal >> HeightValue.heightInCm >> HeightAtDate.undated

            // The weights are compared in gram and the heights in cm, the units of the calculation values. The
            // step becomes a measurement on the date of the measured value.
            let weightActual, weightCalculation =
                dat.Weight.Calculation
                |> roundMeasured
                    (res.CalculationWeightsAndHeights.Weights
                     |> Array.map (decimal >> Conversions.gramFromDecimal))
                    (WeightAtDate.SetGet.getWeight
                     >> WeightValue.getWeightInKg
                     >> Conversions.decimalKgToDecimalGram)
                    WeightAtDate.SetGet.getDate
                    (fun date step -> step |> decimal |> WeightValue.weightInGram |> WeightAtDate.create date)
                    dat.Weight.Actual

            let heightActual, heightCalculation =
                dat.Height.Calculation
                |> roundMeasured
                    (res.CalculationWeightsAndHeights.Heights
                     |> Array.map (decimal >> Conversions.cmFromDecimal))
                    (HeightAtDate.SetGet.getHeight >> HeightValue.getHeightCm)
                    HeightAtDate.SetGet.getDate
                    (fun date step -> step |> decimal |> HeightValue.heightInCm |> HeightAtDate.create date)
                    dat.Height.Actual

            { dat with
                Estimates = est
                Options = PatientDataOptions.ofResources res
                Weight =
                    { dat.Weight with
                        Actual = weightActual
                        Calculation = est.Weight |> calculation weight weightCalculation
                    }
                Height =
                    { dat.Height with
                        Actual = heightActual
                        Calculation = est.Height |> calculation height heightCalculation
                    }
            }


    /// The Dto of the patient data, without the patient identity.
    module Dto =

        open Validus
        open FsToolkit.ErrorHandling


        type Dto() =
            member val Location: string option = None with get, set
            member val Department = Department.Dto.dto () with get, set
            member val Diagnoses: string[] = [||] with get, set
            member val Gender = "" with get, set
            member val Age: AgeValue.Dto.Dto option = None with get, set
            /// Whether the user marked the patient as an adult; fromDto ignores the mark under 16 years.
            member val MarkedAdult = false with get, set
            /// fromDto does not read this field; it sets the adult mark from the age and MarkedAdult.
            member val AdultMark = AdultMark.MarkableAdult false with get, set
            /// fromDto does not read this field; it sets the need from the age.
            member val GestationalAgeNeed = GestationalAgeNeed.NoGestationalAge with get, set
            member val Weight = Weight.Dto.dto () with get, set
            member val Height = Height.Dto.dto () with get, set
            member val GestationalAge: AgeWeeksDays.Dto.Dto option = None with get, set
            /// fromDto does not read this field; it calculates the post-menstrual age.
            member val PostMenstrualAge: AgeWeeksDays.Dto.Dto option = None with get, set
            /// fromDto does not read this field; it calculates the body surface area.
            member val BodySurfaceArea: decimal option = None with get, set
            member val Access: string[] = [||] with get, set
            member val RenalFunction: string option = None with get, set
            /// fromDto does not read this field; it estimates from the resources.
            member val Estimates = Estimates.none with get, set


        let dto () = Dto()


        /// Parses a string with a function. Returns an error that names the field and the string when the
        /// function returns None.
        let parseField field tryFromString s =
            match s |> tryFromString with
            | Some v -> Result.Ok v
            | None -> ValidationErrors.create field [ $"%s{field} is unknown: %s{s}" ] |> Result.Error


        /// Validates a Dto as patient data and calculates the values that follow from it. With the resources, it
        /// also sets the estimates and the options. The calculated fields of the Dto are not read. A weight or
        /// height dated after today is an error.
        let fromDto today resources (dto: Dto) =
            validate {
                let! dep = dto.Department |> Department.Dto.fromDto
                let! gend = dto.Gender |> Gender.Validation.validate "Gender"
                let! gest = dto.GestationalAge |> Option.traverseResult AgeWeeksDays.Dto.fromDto

                let! access =
                    // an access array that is null after JSON deserialization is read as no access
                    (if isNull dto.Access then [||] else dto.Access)
                    |> Array.toList
                    |> List.traverseResultM (parseField "Access" Access.tryFromString)

                let! renal =
                    dto.RenalFunction
                    |> Option.traverseResult (parseField "Renal function" RenalFunction.tryFromString)

                let! age = dto.Age |> Option.traverseResult AgeValue.Dto.fromDto

                let! wght = dto.Weight |> Weight.Dto.fromDto today
                let! hght = dto.Height |> Height.Dto.fromDto today

                return
                    { create dep dto.Diagnoses gend age wght hght gest with
                        Location = dto.Location
                        Access = access
                        RenalFunction = renal
                    }
                    |> markAsAdult dto.MarkedAdult
                    |> determineAgeValues
                    |> estimate resources
                    |> calculateBodySurfaceArea
            }


        /// The Dto of patient data. MarkedAdult is true only for a patient that may be marked and is marked.
        let toDto (dat: PatientData) =
            let dto = dto ()

            dto.Location <- dat.Location
            dto.Department <- dat.Department |> Department.Dto.toDto
            dto.Diagnoses <- dat.Diagnoses
            dto.Gender <- dat.Gender |> Gender.toString
            dto.Age <- dat.Age |> Option.map AgeValue.Dto.toDto
            dto.MarkedAdult <- dat.AdultMark = AdultMark.MarkableAdult true
            dto.AdultMark <- dat.AdultMark
            dto.GestationalAgeNeed <- dat.GestationalAgeNeed
            dto.Weight <- dat.Weight |> Weight.Dto.toDto
            dto.Height <- dat.Height |> Height.Dto.toDto
            dto.GestationalAge <- dat.GestationalAge |> Option.map AgeWeeksDays.Dto.toDto
            dto.PostMenstrualAge <- dat.PostMenstrualAge |> Option.map AgeWeeksDays.Dto.toDto
            dto.BodySurfaceArea <- dat.BodySurfaceArea |> Option.map decimal
            dto.Access <- dat.Access |> List.map Access.toString |> List.toArray
            dto.RenalFunction <- dat.RenalFunction |> Option.map RenalFunction.toString
            dto.Estimates <- dat.Estimates

            dto


/// Applies a change to the patient data Dto.
module PatientDataEdit =

    /// Writes one part of the age. Patient data without an age stays without one when the value is None.
    /// When there is an age, None counts as 0, so clearing one part keeps the age.
    let setAgePart write v (dto: PatientData.Dto.Dto) =
        match dto.Age, v with
        | None, None -> ()
        | age, v ->
            let age = age |> Option.defaultWith AgeValue.Dto.dto
            age |> write (v |> Option.defaultValue 0 |> Some)
            dto.Age <- Some age


    /// Writes one part of the gestational age, starting from the term when there is none. A value of None
    /// writes the same part of the term. Patient data without a gestational age stays without one when the value
    /// is None. Writes nothing when the age needs no gestational age.
    let setGestationalPart write termPart v (dto: PatientData.Dto.Dto) =
        let term () = AgeWeeksDays.term |> AgeWeeksDays.Dto.toDto

        match dto.GestationalAgeNeed, dto.GestationalAge, v with
        | GestationalAgeNeed.NoGestationalAge, _, _
        | _, None, None -> ()
        | _, ga, v ->
            let ga = ga |> Option.defaultWith term
            ga |> write (v |> Option.defaultValue (term () |> termPart))
            dto.GestationalAge <- Some ga


    /// Applies the edit to the Dto and returns the same Dto. Completing the Dto with the resources estimates
    /// the weight and height again for the edited patient data; a measured value stays.
    let apply edit (dto: PatientData.Dto.Dto) =
        match edit with
        | PatientDataEdit.SetYears v -> dto |> setAgePart (fun v age -> age.Years <- v) v
        | PatientDataEdit.SetMonths v -> dto |> setAgePart (fun v age -> age.Months <- v) v
        | PatientDataEdit.SetWeeks v -> dto |> setAgePart (fun v age -> age.Weeks <- v) v
        | PatientDataEdit.SetDays v -> dto |> setAgePart (fun v age -> age.Days <- v) v
        | PatientDataEdit.SetGestationalWeeks v -> dto |> setGestationalPart (fun v ga -> ga.Weeks <- v) _.Weeks v
        | PatientDataEdit.SetGestationalDays v -> dto |> setGestationalPart (fun v ga -> ga.Days <- v) _.Days v
        | PatientDataEdit.SetWeight w ->
            dto.Weight.Calculation <- w |> Option.map (WeightAtDate.undated >> WeightAtDate.Dto.toDto)
            dto.Weight.CalculationEstimated <- false
        | PatientDataEdit.SetHeight h ->
            dto.Height.Calculation <- h |> Option.map (HeightAtDate.undated >> HeightAtDate.Dto.toDto)
            dto.Height.CalculationEstimated <- false
        | PatientDataEdit.SetGender g -> dto.Gender <- g |> Gender.toString
        | PatientDataEdit.SetRenalFunction rf -> dto.RenalFunction <- rf |> Option.map RenalFunction.toString
        | PatientDataEdit.SetDepartment name ->
            // A fresh department Dto, so no department of another kind stays next to the new name.
            dto.Department <- Department.Dto.dto ()
            dto.Department.OtherDepartment <- name
        | PatientDataEdit.SetLocation location -> dto.Location <- location
        | PatientDataEdit.AddAccess a ->
            let s = a |> Access.toString

            if dto.Access |> Array.contains s |> not then
                dto.Access <- dto.Access |> Array.append [| s |]
        | PatientDataEdit.RemoveAccess a ->
            let s = a |> Access.toString
            dto.Access <- dto.Access |> Array.filter ((<>) s)
        | PatientDataEdit.SetMarkedAdult marked -> dto.MarkedAdult <- marked

        dto


module Patient =


    /// Creates a patient from an identity, None for an anonymous patient, and the patient data.
    let create idn dat =
        {
            Identity = idn
            Data = dat
        }


    /// The anonymous patient with nothing known.
    let unknown = create None PatientData.unknown


    /// Creates an anonymous patient at the start of an age type; see PatientData.fromAgeType.
    let fromAgeType gend at = PatientData.fromAgeType gend at |> create None


    module BSA =

        open Calculations

        /// Calculates the body surface area with a formula from the calculation weight and height.
        let calcBsa formula pat = pat.Data |> PatientData.calcBsa formula


        let calcMosteller = calcBsa BSA.calcMosteller

        let calcDuBois = calcBsa BSA.calcDuBois


    module Validation =

        open Validus


        /// Checks that the id, the name and the birth date are all given or all absent.
        let identityValidator field value =
            let msg = sprintf "%s needs an id, a name and a birthdate, or none of the three"

            let rule (id, name, bd) =
                match id |> String.notEmpty, name |> String.notEmpty, bd with
                | true, true, Some _
                | false, false, None -> true
                | _ -> false

            Validator.create msg rule field value


    /// Sets the ages of a patient at a date. An identified patient gets the age from the birth date; an
    /// anonymous patient keeps the entered age. Then the values that follow from the age and the body surface
    /// area are set.
    let atDate dt pat =
        let dat =
            match pat.Identity with
            | Some idn -> { pat.Data with Age = idn.BirthDate |> AgeValue.fromBirthDate dt |> Some }
            | None -> pat.Data

        { pat with Data = dat |> PatientData.determineAgeValues |> PatientData.calculateBodySurfaceArea }


    /// Creates a patient from an identity and patient data, with the ages set at a date.
    let createAt dt idn dat = create idn dat |> atDate dt


    /// Returns the patient data as strings, with the ages set at a date. The identity is never printed.
    let toString dt pat =
        let created = pat |> atDate dt

        [
            created.Data.Department |> Department.toString
            created.Data.Gender |> Gender.toString
            created.Data.Age |> Option.map AgeValue.toString |> Option.defaultValue ""

            created.Data.GestationalAge
            |> Option.map AgeWeeksDays.toString
            |> Option.defaultValue ""

            created.Data.PostMenstrualAge
            |> Option.map AgeWeeksDays.toString
            |> Option.defaultValue ""

            created.Data.Weight |> Weight.calculationToString 2 |> Option.defaultValue ""

            created.Data.Height |> Height.calculationToString 2 |> Option.defaultValue ""

            created
            |> BSA.calcDuBois
            |> Option.map (fun v -> $"{v |> decimal |> Decimal.fixPrecision 2} m2")
            |> Option.defaultValue ""
        ]


    /// The Dto of a patient: the id, the name and the birth date of the identity, and the patient data.
    module Dto =

        open Validus
        open FsToolkit.ErrorHandling


        type Dto() =
            member val Id: string = "" with get, set
            member val Name: string = "" with get, set
            member val BirthDate: BirthDate.Dto.Dto option = None with get, set
            member val Data = PatientData.Dto.dto () with get, set


        let dto () = Dto()


        /// Validates a Dto as a patient. With the resources, the patient data also gets the estimates and the
        /// options. An id, a name and a birth date make an identified patient, none of the three an anonymous
        /// patient; only some of the three is an error.
        let fromDto today resources (dto: Dto) =
            validate {
                let! dat = dto.Data |> PatientData.Dto.fromDto today resources

                let! _ =
                    (dto.Id, dto.Name, dto.BirthDate)
                    |> Validation.identityValidator "Patient Identity"

                and! idn =
                    dto.BirthDate
                    |> Option.traverseResult (
                        BirthDate.Dto.fromDto today
                        >> Result.map (PatientIdentity.create dto.Id dto.Name)
                    )

                return create idn dat
            }


        /// The Dto of a patient.
        let toDto pat =
            let dto = dto ()

            match pat.Identity with
            | Some idn ->
                dto.Id <- idn.Id
                dto.Name <- idn.Name
                dto.BirthDate <- idn.BirthDate |> BirthDate.Dto.toDto |> Some
            | None -> ()

            dto.Data <- pat.Data |> PatientData.Dto.toDto

            dto
