/// The patient as the EHR knows them: the identity, the birthdate, the gender, the dated
/// weights and heights, the gestational age, the department and the access. The rules do not
/// read this record; they read the GenFORM patient projected from it at a date, with the age
/// in days and one weight and one height.
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


type VenousAccess =
    | Peripheral
    | Central
    | AnyVenous
    | UnknownVenous


type EnteralAccess =
    | Oral
    | Gastric
    | Duodenal
    | AnyEnteral
    | UnknownEnteral


type Department =
    | AdultICU of string
    | PediatricICU of string
    | NeonatalICU of string
    | AdultDepartment of string
    | PediatricDepartment of string
    | AnyDepartment
    | UnknownDepartment


type Gender =
    | Male
    | Female
    | AnyGender
    | UnknownGender


type AgeValue =
    {
        Years: int<year> option
        Months: int<month> option
        Weeks: int<week> option
        Days: int<day> option
    }


/// A birthdate, fully specified: a year, a month and a day. A year alone, or a year and a
/// month, is no birthdate.
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
        Calculation: WeightAtDate option
    }

and WeightAtDate =
    {
        Date: DateTime
        Weight: WeightValue
    }


type EstimatedWeight =
    {
        Weight: WeightValue
        SD: WeightValue
    }


type Height =
    {
        Actual: HeightAtDate list
        Birth: HeightValue option
        Calculation: HeightAtDate option
    }

and HeightAtDate =
    {
        Date: DateTime
        Height: HeightValue
    }


type EstimatedHeight =
    {
        Height: HeightValue
        SD: HeightValue
    }


type AgeWeeksDays =
    {
        Weeks: int<week>
        Days: int<day>
    }


/// Who the patient is, when the EHR gave enough to say: the id, the name and the birthdate.
/// Shown in full to the user, kept by the Session and its store, never printed by a patient
/// printer or a log line.
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
        /// The dated weights.
        Weight: Weight
        /// The dated heights.
        Height: Height
        /// The gestational age at birth; absent when not known.
        GestationalAge: AgeWeeksDays option
        /// The post-menstrual age, the age corrected for the gestational age: the age plus the
        /// gestational age, set by calculatePostMenstrualAge, which atDate calls when the patient is
        /// created. None without a gestational age or an age.
        PostMenstrualAge: AgeWeeksDays option
        /// The enteral access.
        EnteralAccess: EnteralAccess
        /// The venous access.
        VenousAccess: VenousAccess
    }


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


    let fromBirthDate now (bd: BirthDate) =
        // the birthdate module comes later in the file, so the date is built here
        let y, m, w, d =
            now
            |> Calculations.Age.fromBirthDate (DateTime(int bd.Year, int bd.Month, int bd.Day))

        create (Some y) (Some m) (Some w) (Some d)


    let toString (av: AgeValue) =
        Calculations.Age.ageToStringNlShort av.Years av.Months av.Weeks av.Days


    module Optics =

        let years = (fun (a: AgeValue) -> a.Years), (fun y a -> { a with Years = y })

        let months = (fun (a: AgeValue) -> a.Months), (fun m a -> { a with Months = m })

        let weeks = (fun (a: AgeValue) -> a.Weeks), (fun w (a: AgeValue) -> { a with Weeks = w })

        let days = (fun (a: AgeValue) -> a.Days), (fun d (a: AgeValue) -> { a with Days = d })


    //[<AutoOpen>]
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


    /// The age value of a number of days: a year 365 days, a month 30 days, at most 11 months. The
    /// inverse of getAgeInDays for every age a birth date gives.
    let fromDays (days: int<day>) =
        let days = int days
        let ys = days / Constants.daysInYear
        let afterYears = days - ys * Constants.daysInYear
        // a plain min names the unit of measure for minutes here
        let ms = afterYears / Constants.daysInMonth |> Operators.min (Constants.monthsInYear - 1)

        let rest = afterYears - ms * Constants.daysInMonth

        create
            (ys |> Conversions.yearFromInt |> Some)
            (ms |> Conversions.monthFromInt |> Some)
            (rest / 7 |> Conversions.weekFromInt |> Some)
            (rest % 7 |> Conversions.dayFromInt |> Some)


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


        let monthValidator = Check.optional (Check.Int.between 0 11) |> mapOpt Conversions.monthFromInt


        let weekValidator = Check.optional (Check.Int.between 0 4) |> mapOpt Conversions.weekFromInt


        let dayValidator = Check.optional (Check.Int.between 0 6) |> mapOpt Conversions.dayFromInt


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


    /// The birthdate an age value gives at a date.
    let fromAgeValue dt av =
        let ys, ms, ws, ds = av |> AgeValue.getDef0
        Calculations.Age.toBirthDate dt ys ms ws ds |> fromDate


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


        let fromDto (dto: Dto) =
            validate {
                let! y = Validation.yearValidator "Year" dto.Year
                let! m = Validation.monthValidator "Month" dto.Month
                let! d = Validation.dayValidator "Day" dto.Day

                let! _ = Validators.dateValidator "Valid date" (y, m, d)

                and! _ = Validation.ageValidator "Valid Age" (DateTime.Now, y, m, d)

                return create y m d
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

    let create dt wv =
        {
            Date = dt
            Weight = wv
        }


    let fromAgeType gend dt = (WeightValue.fromAgeType gend) >> (Option.map (create dt))


    module Optics =


        let date = (fun (w: WeightAtDate) -> w.Date), (fun dt w -> { w with WeightAtDate.Date = dt })

        let weight = (fun (w: WeightAtDate) -> w.Weight), (fun wv w -> { w with WeightAtDate.Weight = wv })


    module Validation =

        open Validus

        let dateValidator dt = Check.DateTime.lessThanOrEqualTo dt


    module SetGet =

        open Informedica.Utils.Lib.Optics

        let getDate = Optics.date |> Optic.get

        let getWeight = Optics.weight |> Optic.get


    let toString fixPrec w =
        let ws = w |> SetGet.getWeight |> WeightValue.toString fixPrec
        let ds = (w |> SetGet.getDate).ToString("dd-MMM-yy")

        $"{ws} ({ds})"


    module Dto =

        open Validus

        type Dto() =
            member val DateTime = DateTime.Now with get, set
            member val WeightValue = WeightValue.Dto.dto () with get, set

        let dto () = Dto()


        let fromDto (dto: Dto) =
            validate {
                let! wv = WeightValue.Dto.fromDto dto.WeightValue
                let! dt = Validation.dateValidator DateTime.Now "Date" dto.DateTime
                return create dt wv
            }


        let toDto (w: WeightAtDate) =
            let dto = dto ()

            dto.WeightValue <- w.Weight |> WeightValue.Dto.toDto
            dto.DateTime <- w.Date

            dto


module Weight =

    let create act birth adm calc =
        {
            Actual = act
            Birth = birth
            Admission = adm
            Calculation = calc
        }


    let unknown = create [] None [] None


    let fromAgeType gend dt = (WeightAtDate.fromAgeType gend dt) >> (create [] None [])


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

        w |> SetGet.getCalculation |> Option.map (WeightAtDate.toString fixPrec)


    let actualToString fixPrec w = toString fixPrec w |> fun (s, _, _, _) -> s


    let birthToString fixPrec w = toString fixPrec w |> fun (_, s, _, _) -> s


    let admissionToString fixPrec w = toString fixPrec w |> fun (_, _, s, _) -> s


    let calculationToString fixPrec w = toString fixPrec w |> fun (_, _, _, s) -> s


    module Dto =

        type Dto() =
            member val Actual: WeightAtDate.Dto.Dto list = [] with get, set
            member val Calculation: WeightAtDate.Dto.Dto option = None with get, set
            member val Birth: WeightValue.Dto.Dto option = None with get, set
            member val Admission: WeightAtDate.Dto.Dto list = [] with get, set


        let dto () = Dto()


        let fromDto (dto: Dto) =
            let act = dto.Actual |> List.choose (WeightAtDate.Dto.fromDto >> Result.toOption)

            let calc =
                dto.Calculation
                |> Option.map WeightAtDate.Dto.fromDto
                |> Option.bind Result.toOption

            let birth = dto.Birth |> Option.map WeightValue.Dto.fromDto |> Option.bind Result.toOption

            let adm = dto.Admission |> List.choose (WeightAtDate.Dto.fromDto >> Result.toOption)


            create act birth adm calc


        let toDto (w: Weight) =
            let dto = dto ()

            dto.Actual <- w.Actual |> List.map WeightAtDate.Dto.toDto
            dto.Calculation <- w.Calculation |> Option.map WeightAtDate.Dto.toDto
            dto.Birth <- w.Birth |> Option.map WeightValue.Dto.toDto
            dto.Admission <- w.Admission |> List.map WeightAtDate.Dto.toDto

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

    let create dt wv =
        {
            Date = dt
            Height = wv
        }


    let fromAgeType gend dt = (HeightValue.fromAgeType gend) >> (Option.map (create dt))


    module Optics =


        let date = (fun (h: HeightAtDate) -> h.Date), (fun dt h -> { h with HeightAtDate.Date = dt })

        let height = (fun (h: HeightAtDate) -> h.Height), (fun hv h -> { h with HeightAtDate.Height = hv })


    module Validation =

        open Validus

        let dateValidator dt = Check.DateTime.lessThanOrEqualTo dt


    module SetGet =

        open Informedica.Utils.Lib.Optics

        let getDate = Optics.date |> Optic.get

        let getHeight = Optics.height |> Optic.get


    let toString fixPrec h =
        let hs = h |> SetGet.getHeight |> HeightValue.toString fixPrec
        let ds = (h |> SetGet.getDate).ToString("dd-MMM-yy")

        $"{hs} ({ds})"


    module Dto =

        open Validus

        type Dto() =
            member val DateTime = DateTime.Now with get, set
            member val HeightValue = HeightValue.Dto.dto () with get, set

        let dto () = Dto()


        let fromDto (dto: Dto) =
            validate {
                let! wv = HeightValue.Dto.fromDto dto.HeightValue
                let! dt = Validation.dateValidator DateTime.Now "Date" dto.DateTime
                return create dt wv
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


    let fromAgeType gend dt = (HeightAtDate.fromAgeType gend dt) >> (create [] None)


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


    let toString fixPrec h =
        h
        |> SetGet.getActual
        |> List.sortByDescending HeightAtDate.SetGet.getDate
        |> List.map (HeightAtDate.toString fixPrec),

        h |> SetGet.getBirth |> Option.map (HeightValue.toString fixPrec),

        h |> SetGet.getCalculation |> Option.map (HeightAtDate.toString fixPrec)


    let actualToString fixPrec w = toString fixPrec w |> fun (s, _, _) -> s


    let birthToString fixPrec w = toString fixPrec w |> fun (_, s, _) -> s


    let calculationToString fixPrec w = toString fixPrec w |> fun (_, _, s) -> s


    module Dto =

        type Dto() =
            member val Actual: HeightAtDate.Dto.Dto list = [] with get, set
            member val Birth: HeightValue.Dto.Dto option = None with get, set
            member val Calculation: HeightAtDate.Dto.Dto option = None with get, set


        let dto () = Dto()


        let fromDto (dto: Dto) =
            let act = dto.Actual |> List.choose (HeightAtDate.Dto.fromDto >> Result.toOption)

            let birth = dto.Birth |> Option.map HeightValue.Dto.fromDto |> Option.bind Result.toOption

            let calc =
                dto.Calculation
                |> Option.map HeightAtDate.Dto.fromDto
                |> Option.bind Result.toOption

            create act birth calc


        let toDto (w: Height) =
            let dto = dto ()

            dto.Actual <- w.Actual |> List.map HeightAtDate.Dto.toDto
            dto.Birth <- w.Birth |> Option.map HeightValue.Dto.toDto
            dto.Calculation <- w.Calculation |> Option.map HeightAtDate.Dto.toDto

            dto


module rec VenousAccess =

    let peripheral = Peripheral

    let central = Central

    let any = AnyVenous

    let unknown = UnknownVenous


    let toString ven =
        match ven with
        | UnknownVenous -> ""
        | _ -> $"{ven}" |> String.toLower


    let fromString = Validation.validate "VenousAccess"


    module Validation =

        open Validus
        open Validus.Operators

        let validate =
            Check.String.equals $"{peripheral |> toString}" *| peripheral
            <|> Check.String.equals $"{central |> toString}" *| central
            <|> Check.String.equals $"{any |> toString}" *| central
            <|> Check.String.equals $"{unknown |> toString}" *| unknown


module rec EnteralAccess =

    let oral = Oral

    let gastric = Gastric

    let duodenal = Duodenal

    let any = AnyEnteral

    let unknown = UnknownEnteral


    let toString ent =
        match ent with
        | UnknownEnteral -> ""
        | _ -> $"{ent}" |> String.toLower


    let fromString = Validation.validate "EnteralAccess"


    module Validation =

        open Validus
        open Validus.Operators

        let validate =
            Check.String.equals $"{oral |> toString}" *| oral
            <|> Check.String.equals $"{gastric |> toString}" *| gastric
            <|> Check.String.equals $"{duodenal |> toString}" *| duodenal
            <|> Check.String.equals $"{any |> toString}" *| any
            <|> Check.String.equals $"{unknown |> toString}" *| unknown


module rec Department =


    module Constants =

        [<Literal>]
        let any = "any department"

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


    let map any unknown fAdultICU fPedICU fNeoICU fAdult fPed =
        function
        | AnyDepartment -> any
        | UnknownDepartment -> unknown
        | AdultICU s -> s |> fAdultICU
        | PediatricICU s -> s |> fPedICU
        | NeonatalICU s -> s |> fNeoICU
        | AdultDepartment s -> s |> fAdult
        | PediatricDepartment s -> s |> fPed


    let apply fAdultICU fPedICU fNeoICU fAdult fPed =
        map
            AnyDepartment
            UnknownDepartment
            (fAdultICU >> AdultICU)
            (fPedICU >> PediatricICU)
            (fNeoICU >> NeonatalICU)
            (fAdult >> AdultDepartment)
            (fPed >> PediatricDepartment)


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

    let any = AnyDepartment

    let unknown = UnknownDepartment


    let toString =
        map
            "any"
            ""
            (sprintf "%s %s" Constants.adultICU)
            (sprintf "%s %s" Constants.pediatricICU)
            (sprintf "%s %s" Constants.neonatalICU)
            (sprintf "%s %s" Constants.adultDepartment)
            (sprintf "%s %s" Constants.pediatricDepartment)
        >> String.trim


    let fromString = Validation.validate "Department" >> Result.map id


    /// The name the rules match: the ward's own name; none for any ward, an unknown one or a
    /// ward without a name.
    let name =
        let named (s: string) = if s |> String.isNullOrWhiteSpace then None else Some s

        map None None named named named named named


    module Validation =

        open Validus
        open Validus.Operators

        /// The kind's words dropped from the front, where the pattern found them, and the name
        /// trimmed: a ward without a name comes back without one, a ward named after its kind
        /// keeps its name.
        let named (kind: string) (s: string) = s.Substring(kind.Length) |> String.trim


        let validate =
            Check.String.equals $"{unknown |> toString}" *| unknown
            <|> Check.String.equals $"{any |> toString}" *| any
            <|> Check.String.pattern $"^{Constants.adultICU}"
                *|* (named Constants.adultICU >> adultICU)
            <|> Check.String.pattern $"^{Constants.pediatricICU}"
                *|* (named Constants.pediatricICU >> pediatricICU)
            <|> Check.String.pattern $"^{Constants.neonatalICU}"
                *|* (named Constants.neonatalICU >> neonatalICU)
            <|> Check.String.pattern $"^{Constants.adultDepartment}"
                *|* (named Constants.adultDepartment >> adultDepartment)
            <|> Check.String.pattern $"^{Constants.pediatricDepartment}"
                *|* (named Constants.pediatricDepartment >> pediatricDepartment)


module Gender =


    let male = Male


    let female = Female


    let any = AnyGender


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
            <|> Check.String.equals $"{any |> toString}" *| any
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


    /// The post-menstrual age of an age and a gestational age: the gestational age plus the age in
    /// days, in weeks and days.
    let postMenstrualAge av ga = (av |> AgeValue.getAgeInDays) + (ga |> toDays) |> fromDays


    /// The gestational age from which a neonate is born at term: 37 weeks, where the full term
    /// gestation type starts.
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


    /// Whether a gestational age value, a time value, is below term.
    let isPretermValue vu = vu |> fromValueUnit |> isPreterm


    module Validation =

        open Validus
        open Validus.Operators

        let minWeeks = 20

        let maxWeeks = Constants.weeksInYear

        let maxDays = Constants.daysInWeek - 1


        let validateWeeks = Check.Int.between minWeeks maxWeeks *|* Conversions.weekFromInt


        let validateDays = Check.Int.between 0 maxDays *|* Conversions.dayFromInt


        /// The weeks of the oldest post-menstrual age: the maximum gestational age plus the maximum
        /// age in weeks.
        let maxPostMenstrualWeeks =
            AgeValue.Validation.maxYear
            |> Conversions.intYearsToDays
            |> Conversions.daysToWeeks
            |> fst
            |> int
            |> (+) maxWeeks


        let validatePostMenstrualWeeks =
            Check.Int.between minWeeks maxPostMenstrualWeeks *|* Conversions.weekFromInt


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


        /// The post-menstrual age of a Dto, with the bounds of a post-menstrual age.
        let fromPostMenstrualDto (dto: Dto) =
            validate {
                let! ws = dto.Weeks |> Validation.validatePostMenstrualWeeks "Weeks"
                let! ds = dto.Days |> Validation.validateDays "Days"
                return create ws ds
            }


        let toDto (awd: AgeWeeksDays) =
            let dto = dto ()

            dto.Weeks <- awd.Weeks |> int
            dto.Days <- awd.Days |> int

            dto


module PatientIdentity =

    /// The identity of an id, a name and a birthdate.
    let create id name bd : PatientIdentity =
        {
            Id = id
            Name = name
            BirthDate = bd
        }


module PatientData =

    /// The patient data of its characteristics, without a post-menstrual age until Patient.atDate
    /// calculates it.
    let create dep diagn gend age wght hght gest ent ven =
        {
            Department = dep
            Diagnoses = diagn
            Gender = gend
            Age = age
            Weight = wght
            Height = hght
            GestationalAge = gest
            PostMenstrualAge = None
            EnteralAccess = ent
            VenousAccess = ven
        }


    /// The patient data with nothing known.
    let unknown =
        create
            Department.unknown
            [||]
            UnknownGender
            None
            Weight.unknown
            Height.unknown
            None
            UnknownEnteral
            UnknownVenous


    /// The data at the start of an age category: the age of the category, and the normal weight and
    /// height of the category for a gender at a date.
    let fromAgeType gend dt at =
        { unknown with
            Age = at |> AgeValue.fromAgeType |> Some
            Weight = Weight.fromAgeType gend dt at
            Height = Height.fromAgeType gend dt at
        }


module Patient =


    /// The patient of an identity, none for an anonymous patient, and the patient data.
    let create idn dat =
        {
            Identity = idn
            Data = dat
        }


    /// The anonymous patient with nothing known.
    let unknown = create None PatientData.unknown


    /// The anonymous patient at the start of an age category, see PatientData.fromAgeType.
    let fromAgeType gend dt at = PatientData.fromAgeType gend dt at |> create None


    module BSA =

        open Calculations

        let calcBsa formula pat =
            let w =
                pat.Data.Weight.Calculation
                |> Option.map (fun w -> w.Weight |> WeightValue.getWeightInKg)

            let h =
                pat.Data.Height.Calculation
                |> Option.map (fun h -> h.Height |> HeightValue.getHeightCm)

            match w, h with
            | Some w, Some h -> formula None w h |> Some
            | _ -> None


        let calcMosteller = calcBsa BSA.calcMosteller

        let calcDuBois = calcBsa BSA.calcDuBois


    module Validation =

        open Validus


        /// An identity is an id, a name and a birthdate together, or none of the three.
        let identityValidator field value =
            let msg = sprintf "%s needs an id, a name and a birthdate, or none of the three"

            let rule (id, name, bd) =
                match id |> String.notEmpty, name |> String.notEmpty, bd with
                | true, true, Some _
                | false, false, None -> true
                | _ -> false

            Validator.create msg rule field value


    /// The patient with its post-menstrual age calculated: the gestational age plus the age in days.
    /// None without either.
    let calculatePostMenstrualAge pat =
        { pat with
            Patient.Data.PostMenstrualAge =
                Option.map2 AgeWeeksDays.postMenstrualAge pat.Data.Age pat.Data.GestationalAge
        }


    /// The patient as created at a date: for an identified patient the age value its birthdate
    /// gives at that date, for an anonymous patient the age as entered; then the post-menstrual age.
    let atDate dt pat =
        match pat.Identity with
        | Some idn -> { pat with Patient.Data.Age = idn.BirthDate |> AgeValue.fromBirthDate dt |> Some }
        | None -> pat
        |> calculatePostMenstrualAge


    /// The patient of an identity and the patient data, created at a date: with its ages.
    let createAt dt idn dat = create idn dat |> atDate dt


    /// The patient data as strings, with the ages as created at a date; the identity is never
    /// printed.
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


    /// The serializable patient, flat as stored: the id, the name and the birthdate of the identity
    /// beside the patient data.
    module Dto =

        open Validus


        type Dto() =
            member val Id: string = "" with get, set
            member val Name: string = "" with get, set
            member val Department: string = "" with get, set
            member val Diagnoses: string[] = [||] with get, set
            member val Gender = "" with get, set
            member val Age: AgeValue.Dto.Dto option = None with get, set
            member val BirthDate: BirthDate.Dto.Dto option = None with get, set
            member val Weight = Weight.Dto.dto () with get, set
            member val Height = Height.Dto.dto () with get, set
            member val GestationalAge: AgeWeeksDays.Dto.Dto option = None with get, set
            member val PostMenstrualAge: AgeWeeksDays.Dto.Dto option = None with get, set
            member val EnteralAccess = "" with get, set
            member val VenousAccess = "" with get, set


        let dto () = Dto()


        /// The patient of a Dto: identified with an id, a name and a birthdate, anonymous with none of
        /// the three; a part of an identity is an error. The post-menstrual age is read back as it was
        /// calculated, with the bounds of a post-menstrual age.
        let fromDto (dto: Dto) =
            let weeksDays fromDto (awd: AgeWeeksDays.Dto.Dto option) =
                match awd with
                | Some awd -> awd |> fromDto |> Result.map Some
                | None -> Result.Ok None

            validate {
                let! dep = dto.Department |> Department.fromString
                let! gend = dto.Gender |> Gender.Validation.validate "Gender"
                let! gest = dto.GestationalAge |> weeksDays AgeWeeksDays.Dto.fromDto
                and! pma = dto.PostMenstrualAge |> weeksDays AgeWeeksDays.Dto.fromPostMenstrualDto
                let! ent = dto.EnteralAccess |> EnteralAccess.fromString
                let! ven = dto.VenousAccess |> VenousAccess.fromString
                let! _ =
                    (dto.Id, dto.Name, dto.BirthDate)
                    |> Validation.identityValidator "Patient Identity"

                and! idn =
                    match dto.BirthDate with
                    | Some bd ->
                        bd
                        |> BirthDate.Dto.fromDto
                        |> Result.map (PatientIdentity.create dto.Id dto.Name >> Some)
                    | None -> Result.Ok None

                and! age =
                    match dto.Age with
                    | Some a -> a |> AgeValue.Dto.fromDto |> Result.map Some
                    | None -> Result.Ok None

                let wght = dto.Weight |> Weight.Dto.fromDto
                let hght = dto.Height |> Height.Dto.fromDto

                return
                    { PatientData.create dep dto.Diagnoses gend age wght hght gest ent ven with PostMenstrualAge = pma }
                    |> create idn
            }


        let toDto pat =
            let dto = dto ()

            match pat.Identity with
            | Some idn ->
                dto.Id <- idn.Id
                dto.Name <- idn.Name
                dto.BirthDate <- idn.BirthDate |> BirthDate.Dto.toDto |> Some
            | None -> ()

            dto.Department <- pat.Data.Department |> Department.toString
            dto.Diagnoses <- pat.Data.Diagnoses
            dto.Gender <- pat.Data.Gender |> Gender.toString
            dto.Age <- pat.Data.Age |> Option.map AgeValue.Dto.toDto
            dto.Weight <- pat.Data.Weight |> Weight.Dto.toDto
            dto.Height <- pat.Data.Height |> Height.Dto.toDto
            dto.GestationalAge <- pat.Data.GestationalAge |> Option.map AgeWeeksDays.Dto.toDto
            dto.PostMenstrualAge <- pat.Data.PostMenstrualAge |> Option.map AgeWeeksDays.Dto.toDto
            dto.EnteralAccess <- pat.Data.EnteralAccess |> EnteralAccess.toString
            dto.VenousAccess <- pat.Data.VenousAccess |> VenousAccess.toString

            dto
