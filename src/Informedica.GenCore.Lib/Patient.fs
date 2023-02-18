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


type Gender = Male | Female | AnyGender | UnknownGender


type AgeValue =
    {
        Years: int<year> option
        Months: int<month> option
        Weeks: int<week> option
        Days: int<day> option
    }


type BirthDate = { Year : int<year>; Month : int<month> option; Day : int<day> option }


type PatientAge =
    | AgeValue of AgeValue
    | BirthDate of BirthDate
    | UnknownAge


type WeightValue = | Kilogram of decimal<kg> | Gram of int<gram>


type HeightValue = | Meter of decimal<m> | Centimeter of decimal<cm>


type Weight =
    {
        Actual : WeightAtDate list
        Birth : WeightValue option
        Admission : WeightAtDate list
        Calculation : WeightAtDate option
    }
and WeightAtDate = { Date : DateTime; Weight : WeightValue }


type EstimatedWeight = { Weight: WeightValue; SD : WeightValue }


type Height =
    {
        Actual : HeightAtDate list
        Birth : HeightValue option
        Calculation : HeightAtDate option
    }
and HeightAtDate = { Date : DateTime; Height : HeightValue }


type EstimatedHeight = { Height : HeightValue; SD : HeightValue }


type Patient =
    {
        Department : Department
        Diagnoses : string []
        Gender : Gender
        Age : PatientAge
        Weight : Weight
        Height : Height
        GestationalAge : AgeWeeksDays option
        EnteralAccess : EnteralAccess
        VenousAccess : VenousAccess
    }

and AgeWeeksDays = { Weeks: int<week>; Days : int<day> }





module AgeType =


    open Informedica.Utils.Lib


    let normalValues =
        let female = [
                    NewBorn, 3.6m<kg>, 52.5m<cm>
                    Infant, 4m<kg>, 54m<cm>
                    Toddler, 9.5m<kg>, 75m<cm>
                    Child, 14.5m<kg>, 97m<cm>
                    Adolescent, 33.5m<kg>, 143m<cm>
                    Adult, 58m<kg>, 170m<cm>
            ]
        let male = [
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

        [
            Female, female
            Male, male
            UnknownGender, unknown
        ]


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

    let get { Years = y; Months = m; Weeks = w; Days = d} =
        y, m, w, d


    let none = create None None None None

    let zero = create (Some 0<year>) None None None

    let oneMonth = create None (Some 1<month>) None None

    let one = create (Some 1<year>) None None None

    let three = create (Some 3<year>) None None None

    let ten = create (Some 10<year>) None None None

    let eighteen = create (Some 18<year>) None None None


    let fromBirthDate now (bd : BirthDate) =
        let y, m, d =
            bd.Year |> int,
            bd.Month |> Option.defaultValue 0<month> |> int,
            bd.Day |> Option.defaultValue 0<day> |> int

        let bd = DateTime(y, m, d)
        let y, m, w, d = now |> Calculations.Age.fromBirthDate bd
        create (Some y) (Some m) (Some w) (Some d)


    let toString (av: AgeValue) =
        Calculations.Age.ageToStringNlShort
            av.Years
            av.Months
            av.Weeks
            av.Days



    module Optics =

        let years =
            (fun (a: AgeValue) -> a.Years),
            (fun y a -> { a with Years = y })

        let months =
            (fun (a: AgeValue) -> a.Months),
            (fun m a -> { a with Months = m })

        let weeks =
            (fun (a: AgeValue) -> a.Weeks),
            (fun w (a: AgeValue) -> { a with Weeks = w })

        let days =
            (fun (a: AgeValue) -> a.Days),
            (fun d (a: AgeValue) -> { a with Days = d })



    //[<AutoOpen>]
    module SetGet =

        open Aether
        open Aether.Operators


        let getYears = Optic.get Optics.years

        let setYears = Optic.set Optics.years

        let getMonths = Optic.get Optics.months

        let setMonths = Optic.set Optics.months

        let getWeeks = Optic.get Optics.weeks

        let setWeeks = Optic.set Optics.weeks

        let getDays = Optic.get Optics.days

        let setDays = Optic.set Optics.days

        let getYearsDef0 = getYears >> Option.defaultValue 0<year>

        let getMonthsDef0 = getMonths >> Option.defaultValue 0<month>

        let getWeeksDef0 = getWeeks >> Option.defaultValue 0<week>

        let getDaysDef0 = getDays >> Option.defaultValue 0<day>

        let setIntYears = Optics.years >-> Morphisms.intYearsOpt 0 |> Optic.set

        let setIntMonths = Optics.months >-> Morphisms.intMonthsOpt 0 |> Optic.set

        let setIntWeeks = Optics.weeks >-> Morphisms.intWeeksOpt 0 |> Optic.set

        let setIntDays = Optics.days >-> Morphisms.intDaysOpt 0 |> Optic.set

        let getIntYears = Optics.years >-> Morphisms.intYearsOpt 0 |> Optic.get

        let getIntMonths = Optics.months >-> Morphisms.intMonthsOpt 0 |> Optic.get

        let getIntWeeks = Optics.weeks >-> Morphisms.intWeeksOpt 0 |> Optic.get

        let getIntDays = Optics.days >-> Morphisms.intDaysOpt 0 |> Optic.get



    let getDef0 av =
        av |> SetGet.getYearsDef0,
        av |> SetGet.getMonthsDef0,
        av |> SetGet.getWeeksDef0,
        av |> SetGet.getDaysDef0


    let getAgeInDays av =
        let ys, ms, ws, ds = av |> getDef0
        Calculations.Age.yearsMonthsWeeksDaysToDays ys ms ws ds


    let comp op a1 a2 =
        let a1 = a1 |> getAgeInDays
        let a2 = a2 |> getAgeInDays
        a1 |> op <| a2


    let addDays n av =
        let d = av |> SetGet.getDaysDef0
        let d = if d + n > 6<day> then d else d + n

        av
        |> SetGet.setDays (Some d)


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


    let fromAgeType = function
        | NewBorn -> zero
        | Infant -> oneMonth
        | Toddler -> one
        | Child -> three
        | Adolescent -> ten
        | Adult -> eighteen
        | _ -> none


    module Validation =

        open Validus

        let [<Literal>] maxYear = 120<year>


        let yearValidator =
            let m = int maxYear
            Check.optional (Check.Int.between 0 m)
            |> mapOpt Conversions.yearFromInt


        let monthValidator =
            Check.optional (Check.Int.between 0 11)
            |> mapOpt Conversions.monthFromInt


        let weekValidator =
            Check.optional (Check.Int.between 0 4)
            |> mapOpt Conversions.weekFromInt


        let dayValidator =
            Check.optional (Check.Int.between 0 6)
            |> mapOpt Conversions.dayFromInt



    module Dto =

        open Validus

        type Dto () =
            member val Years : int option = None with get, set
            member val Months : int option = None with get, set
            member val Weeks : int option = None with get, set
            member val Days : int option = None with get, set


        let dto () = Dto ()


        let fromDto (dto : Dto) =
            validate {
                let! y = Validation.yearValidator "Years" dto.Years
                let! m = Validation.monthValidator "Months" dto.Months
                let! w = Validation.weekValidator "Weeks" dto.Weeks
                let! d = Validation.dayValidator "Days" dto.Days
                return create y m w d
            }


        let toDto (a : AgeValue) =
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


    let zero = create 1900<year> None None


    let get { Year = y; Month = m; Day = d } =
        y, m, d


    module Optics =

        let year =
            (fun (ymd: BirthDate) -> ymd.Year),
            (fun i ymd -> { ymd with Year = i} )

        let month =
            (fun (ymd: BirthDate) -> ymd.Month),
            (fun i ymd -> { ymd with Month = i} )

        let day =
            (fun (ymd: BirthDate) -> ymd.Day),
            (fun i ymd -> { ymd with Day = i} )



    module SetGet =

        open Aether
        open Aether.Operators


        let getYear = Optic.get Optics.year

        let setYear = Optic.set Optics.year

        let getIntYear = Optics.year >-> Morphisms.intYears |> Optic.get

        let setIntYear = Optics.year >-> Morphisms.intYears |> Optic.set


        let getMonth = Optic.get Optics.month

        let setMonth = Optic.set Optics.month

        let getIntMonth = Optics.month >-> Morphisms.intMonthsOpt 1 |> Optic.get

        let setIntMonth = Optics.month >-> Morphisms.intMonthsOpt 1 |> Optic.set


        let getDay = Optic.get Optics.day

        let setDay = Optic.set Optics.day

        let getIntDay = Optics.day >-> Morphisms.intDaysOpt 1 |> Optic.get

        let setIntDay = Optics.day >-> Morphisms.intDaysOpt 1 |> Optic.set



    let toDate bd =
        let y, m, d =
            bd |> SetGet.getIntYear,
            bd |> SetGet.getIntMonth,
            bd |> SetGet.getIntDay
        DateTime (y, m, d)


    let fromDate (dt : DateTime) =
        let y, m, d =
            dt.Year |> Conversions.yearFromInt,
            dt.Month |> Conversions.monthFromInt |> Some,
            dt.Day |> Conversions.dayFromInt |> Some

        create y m d


    module Validation =

        open Validus


        let yearValidator =
            Check.Int.greaterThanOrEqualTo 1900
            |> map Conversions.yearFromInt

        let monthValidator =
            Check.optional (Check.Int.between 1 12)
            |> mapOpt Conversions.monthFromInt


        let dayValidator =
            Check.optional (Check.Int.between 1 31)
            |> mapOpt Conversions.dayFromInt

        let ageValidator =
            let msg = sprintf "age cannot be > 120 years %s"
            let rule (dt, y : int<year>, m : int<month> , d : int<day>) =
                let y = y |> int
                let m = m |> int
                let d = d |> int
                try
                    let tot = (dt - DateTime(y, m, d)).TotalDays |> int
                    tot  < (Constants.daysInYear * 120)
                with
                | _ -> false

            Validator.create msg rule


    module Dto =

        open Validus

        type Dto () =
            member val Year = 0 with get, set
            member val Month : int option = None with get, set
            member val Day : int option = None with get, set


        let dto () = Dto ()


        let fromDto (dto: Dto) =
            validate {
                let! y = Validation.yearValidator "Year" dto.Year
                let! m = Validation.monthValidator "Month" dto.Month
                let! d = Validation.dayValidator "Day" dto.Day

                let! _ =
                    Validators.dateValidator
                        "Valid date"
                        (y, m |> Option.defaultValue 1<month>, d |> Option.defaultValue 1<day>)
                and! _ =
                    Validation.ageValidator
                        "Valid Age"
                        (DateTime.Now, y, m |> Option.defaultValue 1<month>, d |> Option.defaultValue 1<day>)

                return create y m d
            }


        let toDto (ymd : BirthDate) =
            let dto = dto ()

            dto.Year <- ymd.Year |> int
            dto.Month <- ymd.Month |> Option.map int
            dto.Day <- ymd.Day |> Option.map int

            dto



module PatientAge =


    open Informedica.Utils.Lib


    let birthDate bd = bd |> BirthDate


    let ageValue av = av |> AgeValue


    let unknown = UnknownAge


    let map def fBd fAv = function
        | UnknownAge -> def
        | BirthDate bd -> bd |> fBd
        | AgeValue av -> av |> fAv


    let getBirthDate dt =
        let get av =
            let ys, ms, ws, ds = av |> AgeValue.getDef0
            Calculations.Age.toBirthDate dt ys ms ws ds
            |> BirthDate.fromDate
            |> Some

        map None Some get


    let getAgeValue dt =
        let get bd =
            dt
            |> Calculations.Age.fromBirthDate (bd |> BirthDate.toDate)
            |> fun (ys, ms, ws, ds) ->
                AgeValue.create (Some ys) (Some ms) (Some ws) (Some ds)
                |> Some

        map None get Some


    let fromAgeType = AgeValue.fromAgeType >> ageValue


    let toAgeType dt = (getAgeValue dt) >> (Option.map AgeValue.toAgeType)


    let newBorn = NewBorn |> fromAgeType

    let infant = Infant |> fromAgeType

    let toddler = Toddler |> fromAgeType

    let child = Child |> fromAgeType

    let adolescent = Adolescent |> fromAgeType

    let adult = Adult |> fromAgeType


    module Optics =

        let ageValue dt =
            (fun pa -> pa |> getAgeValue dt),
            (fun av _ -> av |> AgeValue)


    let toString =
        map
            ""
            ((AgeValue.fromBirthDate DateTime.Now) >> AgeValue.toString)
            AgeValue.toString



module WeightValue =


    let map fKg fGram = function
        | Kilogram x -> x |> fKg
        | Gram x -> x |> fGram


    let apply fKg fGram = map (fKg >> Kilogram) (fGram >> Gram)


    let weightInGram x =
        x
        |> int
        |> Conversions.gramFromInt
        |> Gram

    let weightInKg x =
        x
        |> Conversions.kgFromDecimal
        |> Kilogram


    let getValue = function
        | Kilogram x -> true, decimal x
        | Gram x -> false, x |> int |> decimal


    let getWeightInKg =
        map id Conversions.intGramToDecKg


    let fromAgeType gend =
        (AgeType.getNormal gend)
        >> (Option.map fst)
        >> (Option.map Kilogram)


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

            Check.Decimal.between minW maxW
            *|* weightInKg


        let weightGramValidator =
            let minW = decimal minWeightGram
            let maxW = decimal maxWeightGram

            Check.Decimal.between minW maxW
            *|* weightInGram



    let toString fixPrec = function
        | Kilogram v -> $"{v |> decimal |> Decimal.fixPrecision fixPrec} kg"
        | Gram v -> $"{v} gram"



    module Dto =

        type Dto () =
            member val Weight = 0m with get, set
            member val WeightInKg = true with get, set

        let dto () = Dto ()


        let fromDto (dto : Dto) =
            if dto.WeightInKg then
                dto.Weight
                |> Validation.weightKgValidator "Weight in kg"
            else
                dto.Weight
                |> Validation.weightGramValidator "Weight in gram"


        let toDto (wv : WeightValue) =
            let dto = dto ()
            let b, v = wv |> getValue

            dto.Weight <- v
            dto.WeightInKg <- b

            dto



module WeightAtDate =

    let create dt wv =
        {
            Date =dt
            Weight = wv
        }


    let fromAgeType gend dt = (WeightValue.fromAgeType gend) >> (Option.map (create dt))


    module Optics =


        let date =
            (fun (w : WeightAtDate) -> w.Date),
            (fun dt w -> { w with WeightAtDate.Date = dt })

        let weight =
            (fun (w : WeightAtDate) -> w.Weight),
            (fun wv w -> { w with WeightAtDate.Weight = wv })



    module Validation =

        open Validus

        let dateValidator dt =
            Check.DateTime.lessThanOrEqualTo dt



    module SetGet =

        open Aether

        let getDate = Optics.date |> Optic.get

        let getWeight = Optics.weight |> Optic.get



    let toString fixPrec w =
        let ws =
            w
            |> SetGet.getWeight
            |> WeightValue.toString fixPrec
        let ds = (w |> SetGet.getDate).ToString("dd-MMM-yy")

        $"{ws} ({ds})"



    module Dto =

        open Validus

        type Dto () =
            member val DateTime = DateTime.Now with get, set
            member val WeightValue = WeightValue.Dto.dto () with get, set

        let dto () = Dto ()


        let fromDto (dto : Dto) =
            validate {
                let! wv = WeightValue.Dto.fromDto dto.WeightValue
                let! dt = Validation.dateValidator DateTime.Now "Date" dto.DateTime
                return create dt wv
            }


        let toDto (w : WeightAtDate) =
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


    let fromAgeType gend dt =
        (WeightAtDate.fromAgeType gend dt) >> (create [] None [])


    module Optics =

        let actual =
            (fun (w: Weight) -> w.Actual),
            (fun act w -> { w with Weight.Actual = act })

        let calculation =
            (fun (w : Weight) -> w.Calculation),
            (fun calc w -> { w with Weight.Calculation = calc })

        let birth =
            (fun (w : Weight) -> w.Birth),
            (fun b w -> { w with Weight.Birth = b })

        let admission =
            (fun (w : Weight) -> w.Admission),
            (fun a w -> { w with Weight.Admission = a })



    module SetGet =

        open Aether

        let getActual = Optic.get Optics.actual

        let getBirth = Optic.get Optics.birth

        let getAdmission = Optic.get Optics.admission

        let getCalculation = Optic.get Optics.calculation



    let toString fixPrec w =
        w
        |> SetGet.getActual
        |> List.sortByDescending WeightAtDate.SetGet.getDate
        |> List.map (WeightAtDate.toString fixPrec),

        w
        |> SetGet.getBirth
        |> Option.map (WeightValue.toString fixPrec),

        w
        |> SetGet.getAdmission
        |> List.sortByDescending WeightAtDate.SetGet.getDate
        |> List.map (WeightAtDate.toString fixPrec),

        w
        |> SetGet.getCalculation
        |> Option.map (WeightAtDate.toString fixPrec)


    let actualToString fixPrec w =
        toString fixPrec w
        |> fun (s, _, _, _) -> s


    let birthToString fixPrec w =
        toString fixPrec w
        |> fun (_, s, _, _) -> s


    let admissionToString fixPrec w =
        toString fixPrec w
        |> fun (_, _, s, _) -> s


    let calculationToString fixPrec w =
        toString fixPrec w
        |> fun (_, _, _, s) -> s



    module Dto =

        type Dto () =
            member val Actual : WeightAtDate.Dto.Dto list = [] with get, set
            member val Calculation : WeightAtDate.Dto.Dto option = None with get, set
            member val Birth : WeightValue.Dto.Dto option = None with get, set
            member val Admission : WeightAtDate.Dto.Dto list = [] with get, set


        let dto () = Dto ()


        let fromDto (dto : Dto) =
            let act =
                dto.Actual
                |> List.choose (WeightAtDate.Dto.fromDto >> Result.toOption)

            let calc =
                dto.Calculation
                |> Option.map WeightAtDate.Dto.fromDto
                |> Option.bind Result.toOption

            let birth =
                dto.Birth
                |> Option.map WeightValue.Dto.fromDto
                |> Option.bind Result.toOption

            let adm =
                dto.Admission
                |> List.choose (WeightAtDate.Dto.fromDto >> Result.toOption)


            create act birth adm calc


        let toDto (w : Weight) =
            let dto = dto ()

            dto.Actual <- w.Actual |> List.map WeightAtDate.Dto.toDto
            dto.Calculation <- w.Calculation |> Option.map WeightAtDate.Dto.toDto
            dto.Birth <- w.Birth |> Option.map WeightValue.Dto.toDto
            dto.Admission <- w.Admission |> List.map WeightAtDate.Dto.toDto

            dto



module HeightValue =


    let map fM fCm = function
        | Meter x -> x |> fM
        | Centimeter x -> x |> fCm


    let apply fM fCm = map (fM >> Meter) (fCm >> Centimeter)


    let heightInMeter x =
        x
        |> Conversions.meterFromDecimal
        |> Meter


    let heightInCm (x : decimal) =
        x
        |> Conversions.cmFromDecimal
        |> Centimeter


    let getValue = function
        | Meter x -> true, decimal x
        | Centimeter x -> false, x |> decimal


    let getHeightCm = map Conversions.decMtoDecCm (decimal >> Conversions.cmFromDecimal)


    let fromAgeType gend =
        (AgeType.getNormal gend)
        >> (Option.map snd)
        >> (Option.map Centimeter)


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

            Check.Decimal.between minW maxW
            *|* heightInMeter


        let heightCmValidator =
            let minW = decimal minHeightCm
            let maxW = decimal maxHeightCm

            Check.Decimal.between minW maxW
            *|* heightInCm



    let toString fixPrec = function
        | Meter v -> $"{v |> decimal |> Decimal.fixPrecision fixPrec} meter"
        | Centimeter v -> $"{v} cm"



    module Dto =

        type Dto () =
            member val Height = 0m with get, set
            member val HeightInMeter = true with get, set

        let dto () = Dto ()


        let fromDto (dto : Dto) =
            if dto.HeightInMeter then
                dto.Height
                |> Validation.heightMeterValidator "Height in meter"
            else
                dto.Height
                |> Validation.heightCmValidator "Height in cm"


        let toDto (wv : HeightValue) =
            let dto = dto ()
            let b, v = wv |> getValue

            dto.Height <- v
            dto.HeightInMeter <- b

            dto



module HeightAtDate =

    let create dt wv =
        {
            Date =dt
            Height = wv
        }


    let fromAgeType gend dt = (HeightValue.fromAgeType gend) >> (Option.map (create dt))


    module Optics =


        let date =
            (fun (h : HeightAtDate) -> h.Date),
            (fun dt h -> { h with HeightAtDate.Date = dt })

        let height =
            (fun (h : HeightAtDate) -> h.Height),
            (fun hv h -> { h with HeightAtDate.Height = hv })




    module Validation =

        open Validus

        let dateValidator dt =
            Check.DateTime.lessThanOrEqualTo dt


    module SetGet =

        open Aether

        let getDate = Optics.date |> Optic.get

        let getHeight = Optics.height |> Optic.get



    let toString fixPrec h =
        let hs =
            h
            |> SetGet.getHeight
            |> HeightValue.toString fixPrec
        let ds = (h |> SetGet.getDate).ToString("dd-MMM-yy")

        $"{hs} ({ds})"



    module Dto =

        open Validus

        type Dto () =
            member val DateTime = DateTime.Now with get, set
            member val HeightValue = HeightValue.Dto.dto () with get, set

        let dto () = Dto ()


        let fromDto (dto : Dto) =
            validate {
                let! wv = HeightValue.Dto.fromDto dto.HeightValue
                let! dt = Validation.dateValidator DateTime.Now "Date" dto.DateTime
                return create dt wv
            }


        let toDto (w : HeightAtDate) =
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


    let fromAgeType gend dt =
        (HeightAtDate.fromAgeType gend dt) >> (create [] None)



    module Optics =

        let actual =
            (fun (h: Height) -> h.Actual),
            (fun act h -> { h with Height.Actual = act })

        let calculation =
            (fun (h : Height) -> h.Calculation),
            (fun calc h -> { h with Height.Calculation = calc })

        let birth =
            (fun (h : Height) -> h.Birth),
            (fun b h -> { h with Height.Birth = b })



    module SetGet =

        open Aether

        let getActual = Optic.get Optics.actual

        let getBirth = Optic.get Optics.birth

        let getCalculation = Optic.get Optics.calculation


    let toString fixPrec h =
        h
        |> SetGet.getActual
        |> List.sortByDescending HeightAtDate.SetGet.getDate
        |> List.map (HeightAtDate.toString fixPrec),

        h
        |> SetGet.getBirth
        |> Option.map (HeightValue.toString fixPrec),

        h
        |> SetGet.getCalculation
        |> Option.map (HeightAtDate.toString fixPrec)


    let actualToString fixPrec w =
        toString fixPrec w
        |> fun (s, _, _) -> s


    let birthToString fixPrec w =
        toString fixPrec w
        |> fun (_, s, _) -> s


    let calculationToString fixPrec w =
        toString fixPrec w
        |> fun (_, _, s) -> s




    module Dto =

        type Dto () =
            member val Actual : HeightAtDate.Dto.Dto list = [] with get, set
            member val Birth : HeightValue.Dto.Dto option = None with get, set
            member val Calculation : HeightAtDate.Dto.Dto option = None with get, set


        let dto () = Dto ()


        let fromDto (dto : Dto) =
            let act =
                dto.Actual
                |> List.choose (HeightAtDate.Dto.fromDto >> Result.toOption)

            let birth =
                dto.Birth
                |> Option.map HeightValue.Dto.fromDto
                |> Option.bind Result.toOption

            let calc =
                dto.Calculation
                |> Option.map HeightAtDate.Dto.fromDto
                |> Option.bind Result.toOption

            create act birth calc


        let toDto (w : Height) =
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


    let fromString =
        Validation.validate "VenousAccess"


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


    let fromString =
        Validation.validate "EnteralAccess"


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

            let [<Literal>] any = "any department"

            let [<Literal>] unknown = ""

            let [<Literal>] adultICU = "Adult ICU"

            let [<Literal>] pediatricICU = "Pediatric ICU"

            let [<Literal>] neonatalICU = "Neonatal ICU"

            let [<Literal>] adultDepartment = "Adult Department"

            let [<Literal>] pediatricDepartment ="Pediatric Department"



    let map any unknown fAdultICU fPedICU fNeoICU fAdult fPed = function
        | AnyDepartment -> any
        | UnknownDepartment -> unknown
        | AdultICU s -> s |> fAdultICU
        | PediatricICU s -> s |> fPedICU
        | NeonatalICU s -> s |> fNeoICU
        | AdultDepartment s -> s |> fAdult
        | PediatricDepartment s -> s |> fPed


    let appl fAdultICU fPedICU fNeoICU fAdult fPed =
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

    let anyPedictricICU = "" |> pediatricICU

    let neonatalICU = NeonatalICU

    let anyNeonatalICU = "" |> neonatalICU

    let adultDepartment = AdultDepartment

    let anyAdultDepartment = "" |> adultDepartment

    let pediatricDepartment = PediatricDepartment

    let anypediatricDepartment = "" |> pediatricDepartment

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


    let fromString =
        Validation.validate "Department"
        >> Result.map id


    module Validation =

        open Validus
        open Validus.Operators

        let validate =
            Check.String.equals $"{unknown |> toString}" *| unknown
            <|> Check.String.equals $"{any |> toString}" *| any
            <|> Check.String.pattern $"^{Constants.adultICU}" *|* ((String.replace $"{Constants.adultICU} " "") >> adultICU)
            <|> Check.String.pattern $"^{Constants.pediatricICU}" *|* ((String.replace $"{Constants.pediatricICU} " "") >> pediatricICU)
            <|> Check.String.pattern $"^{Constants.neonatalICU}" *|* ((String.replace $"{Constants.neonatalICU} " "") >> neonatalICU)
            <|> Check.String.pattern $"^{Constants.adultDepartment}" *|* ((String.replace $"{Constants.adultDepartment} " "") >> adultDepartment)
            <|> Check.String.pattern $"^{Constants.pediatricDepartment}" *|* ((String.replace $"{Constants.pediatricDepartment} " "") >> pediatricDepartment)



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


    let toWeeks = function
        | ExtremePreterm -> min |> Some
        | VeryPreterm -> extreme |> Some
        | ModeratePreterm -> very |> Some
        | FullTerm -> moderate |> Some
        | UnknownGestation -> None


    let toString (gt : GestationType) =
        match gt with
        | UnknownGestation -> ""
        | _ -> $"{gt}"


    let fromString s =
        match s with
        | _ when ExtremePreterm |> toString = s -> UnknownGestation
        | _ when VeryPreterm |> toString = s -> VeryPreterm
        | _ when ModeratePreterm |> toString = s -> ModeratePreterm
        | _ when FullTerm |> toString = s -> FullTerm
        | _ -> UnknownGestation




module AgeWeeksDays =

    let create w d =
        {
            Weeks = w
            Days = d
        }

    let fromWeeks w = create w 0<day>

    let zero = create 0<week> 0<day>

    let oneDay = create 0<week> 1<day>

    let oneWeek = create 1<week> 0<day>

    let fullTerm = create 40<week> 0<day>

    let preterm = create 36<week> 6<day>


    module Optics =

        let weeks =
            (fun (awd : AgeWeeksDays) -> awd.Weeks),
            (fun ws awd -> { awd with AgeWeeksDays.Weeks = ws })

        let days =
            (fun (awd : AgeWeeksDays) -> awd.Days),
            (fun ds awd -> { awd with AgeWeeksDays.Days = ds })


    module SetGet =

        open Aether

        let getWeeks = Optic.get Optics.weeks

        let getDays = Optic.get Optics.days

        let setWeeks = Optic.set Optics.weeks

        let setDays = Optic.set Optics.days



    let fromGestation = GestationType.toWeeks >> (Option.map fromWeeks)


    let toGestation = SetGet.getWeeks >> GestationType.fromWeeks


    let toDays (awd: AgeWeeksDays) =
        (awd.Weeks |> Conversions.weeksToDays) + awd.Days


    let comp (op) awd1 awd2 = awd1 |> op <| awd2


    module Operators =

        let (>?) awd1 awd2 = comp (>) awd1 awd2

        let (>=?) awd1 awd2 = comp (>=) awd1 awd2

        let (<?) awd1 awd2 = comp (<) awd1 awd2

        let (<=?) awd1 awd2 = comp (<=) awd1 awd2


    open Operators


    let isfullTerm awd = awd >=? fullTerm


    let isPreterm awd = awd <=? preterm



    module Validation =

        open Validus
        open Validus.Operators

        let minWeeks = 20

        let maxWeeks = Constants.weeksInYear

        let maxDays = Constants.daysInWeek - 1


        let validateWeeks =
            Check.Int.between minWeeks maxWeeks
            *|* Conversions.weekFromInt


        let validateDays =
            Check.Int.between 0 maxDays
            *|* Conversions.dayFromInt



    let toString awd =
        let s = awd |> toGestation |> GestationType.toString
        if s |> String.isNullOrWhiteSpace then ""
        else
            let ds =
                if awd |> SetGet.getDays = 0<day> then ""
                else $" en {awd.Days} dagen"

            $"{s} {awd.Weeks} weken{ds}"



    module Dto =

        open Validus


        type Dto () =
            member val Weeks = 0 with get, set
            member val Days = 0 with get, set


        let dto () = Dto ()


        let fromDto (dto : Dto) =
            validate {
                let! ws = dto.Weeks |> Validation.validateWeeks "Weeks"
                let! ds = dto.Days |> Validation.validateDays "Days"
                return create ws ds
            }


        let toDto (awd : AgeWeeksDays) =
            let dto = dto ()

            dto.Weeks <- awd.Weeks |> int
            dto.Days <- awd.Days |> int

            dto



module Patient =


    let create dep diagn gend age wght hght gest ent ven =
        {
            Department = dep
            Diagnoses = diagn
            Gender = gend
            Age = age
            Weight = wght
            Height = hght
            GestationalAge = gest
            EnteralAccess = ent
            VenousAccess = ven
        }

    let unknown =
        create
            Department.unknown
            [||]
            UnknownGender
            PatientAge.unknown
            Weight.unknown
            Height.unknown
            None
            UnknownEnteral
            UnknownVenous


    let fromAgeType gend dt at =
        create
            Department.unknown
            [||]
            UnknownGender
            (PatientAge.fromAgeType at)
            (Weight.fromAgeType gend dt at)
            (Height.fromAgeType gend dt at)
            None
            UnknownEnteral
            UnknownVenous


    module Optics =

        let department =
            (fun (pat: Patient) -> pat.Department),
            (fun dep pat -> { pat with Patient.Department = dep })

        let diagnoses =
            (fun (pat: Patient) -> pat.Diagnoses),
            (fun diagn pat -> { pat with Patient.Diagnoses = diagn })

        let Gender =
            (fun (pat: Patient) -> pat.Gender),
            (fun gend pat -> { pat with Patient.Gender = gend })

        let age =
            (fun (pat: Patient) -> pat.Age),
            (fun age pat -> { pat with Patient.Age = age })

        let weight =
            (fun (pat: Patient) -> pat.Weight),
            (fun w pat -> { pat with Patient.Weight = w })

        let height =
            (fun (pat: Patient) -> pat.Height),
            (fun h pat -> { pat with Patient.Height = h })

        let gestationalAge =
            (fun (pat: Patient) -> pat.GestationalAge),
            (fun gest pat -> { pat with Patient.GestationalAge = gest })

        let enteralAccess =
            (fun (pat: Patient) -> pat.EnteralAccess),
            (fun ent pat -> { pat with Patient.EnteralAccess = ent })

        let venousAccess =
            (fun (pat: Patient) -> pat.VenousAccess),
            (fun ven pat -> { pat with Patient.VenousAccess = ven })


    module SetGet =

        open Aether
        open Aether.Operators

        let getAge = Optic.get Optics.age

        let getWeight = Optic.get Optics.weight

        let getHeight = Optic.get Optics.height

        let getWeightActual =
            Optics.weight
            >-> Weight.Optics.actual
            |> Optic.get

        let getWeightCalc =
            Optics.weight
            >-> Weight.Optics.calculation
            |> Optic.get

        let getHeightCalc =
            Optics.height
            >-> Height.Optics.calculation
            |> Optic.get

        let getGestationalAge =
            Optic.get Optics.gestationalAge

        let ageValuePrism dt =
            Optics.age
            >-> PatientAge.Optics.ageValue dt

        let getAgeValue dt = ageValuePrism dt |> Optic.get


    module BSA =

        open Calculations

        let calcBsa formula pat =
            let w =
                pat
                |> SetGet.getWeightCalc
                |> Option.map (fun w ->
                    w.Weight
                    |> WeightValue.getWeightInKg
                )
            let h =
                pat
                |> SetGet.getHeightCalc
                |> Option.map (fun h ->
                    h.Height
                    |> HeightValue.getHeightCm
                )
            match w, h with
            | Some w, Some h -> formula None w h |> Some
            | _ -> None


        let calcMosteller = calcBsa BSA.calcMosteller

        let calcDuBois = calcBsa BSA.calcDuBois



    module Validation =

        open Validus


        let validateDepartment =
            Check.optional Check.String.notEmpty



    let calcPostMenstrualAge dt pat =
        match pat |> SetGet.getGestationalAge, pat  |> SetGet.getAgeValue dt with
        | Some ga, Some av ->
            let ad = av |> AgeValue.getAgeInDays
            let ws, ds = ga.Weeks, ga.Days

            let ws, ds = Calculations.Age.postMenstrualAge ds ws ad
            AgeWeeksDays.create ws ds
            |> Some
        | _ -> None



    let toString dt (pat: Patient) =
        [
            pat.Department |> Department.toString
            pat.Gender |> Gender.toString
            pat.Age |> PatientAge.toString

            pat.GestationalAge
            |> Option.map AgeWeeksDays.toString
            |> Option.defaultValue ""

            pat
            |> calcPostMenstrualAge dt
            |> Option.map AgeWeeksDays.toString
            |> Option.defaultValue ""

            pat.Weight
            |> Weight.calculationToString 2
            |> Option.defaultValue ""

            pat.Height
            |> Height.calculationToString 2
            |> Option.defaultValue ""

            pat
            |> BSA.calcMosteller
            |> Option.map (fun v -> $"{v |> decimal |> Decimal.fixPrecision 2} m2")
            |> Option.defaultValue ""
        ]



    module Dto =

        open Validus


        type Dto () =
            member val Department : string = "" with get, set
            member val Diagnoses : string [] = [||] with get, set
            member val Gender = "" with get, set
            member val Age : AgeValue.Dto.Dto option = None with get, set
            member val BirthDate :  BirthDate.Dto.Dto option = None with get, set
            member val Weight = Weight.Dto.dto () with get, set
            member val Height = Height.Dto.dto () with get, set
            member val GestationalAge : AgeWeeksDays.Dto.Dto option = None with get, set
            member val EnteralAccess = "" with get, set
            member val VenousAccess = "" with get, set


        let dto () = Dto ()


        let fromDto (dto : Dto) =
            validate {
//                let! dep = dto.Department |> Validation.validateDepartment "Department"
                let! gend = dto.Gender |> Gender.Validation.validate "Gender"
                let! gest =
                    match dto.GestationalAge with
                    | Some ga -> ga |> AgeWeeksDays.Dto.fromDto |> Result.map Some
                    | None -> Result.Ok None
                let! ent = dto.EnteralAccess |> EnteralAccess.fromString
                let! ven = dto.VenousAccess |> VenousAccess.fromString
                let! _ = (dto.Age, dto.BirthDate) |> Validators.onlyOneOfTwoOpt "Patient Age"
                and! age =
                    match dto.Age, dto.BirthDate with
                    | Some a, None -> a |> AgeValue.Dto.fromDto |> Result.map PatientAge.ageValue
                    | None, Some b -> b |> BirthDate.Dto.fromDto |> Result.map PatientAge.birthDate
                    | _ -> PatientAge.unknown |> Result.Ok

                let wght = dto.Weight |> Weight.Dto.fromDto
                let hght = dto.Height |> Height.Dto.fromDto

                return create Department.unknown dto.Diagnoses gend age wght hght gest ent ven
            }


        let toDto (pat : Patient) =
            let dto = dto ()

            dto.Department <- pat.Department |> Department.toString //pat.Department
            dto.Diagnoses <- pat.Diagnoses
            dto.Gender <- pat.Gender |> Gender.toString
            dto.Age <-
                match pat.Age with
                | AgeValue av -> av |> AgeValue.Dto.toDto |> Some
                | BirthDate _
                | UnknownAge -> None
            dto.BirthDate <-
                match pat.Age with
                | BirthDate bd -> bd |> BirthDate.Dto.toDto |> Some
                | AgeValue _
                | UnknownAge  -> None
            dto.Weight <- pat.Weight |> Weight.Dto.toDto
            dto.Height <- pat.Height |> Height.Dto.toDto
            dto.GestationalAge <-
                match pat.GestationalAge with
                | None -> None
                | Some ga -> ga |> AgeWeeksDays.Dto.toDto |> Some
            dto.EnteralAccess <- pat.EnteralAccess |> EnteralAccess.toString
            dto.VenousAccess <- pat.VenousAccess |> VenousAccess.toString

            dto


