namespace ServerApi

open Informedica.Utils.Lib.BCL
// the contract model's cases win unqualified; the domain's are reached through the aliases
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models


/// The contract model in GenCORE's terms: the age in days and back, the gestational age in days
/// and back, and the post-menstrual age, each as GenCORE counts it; the adult mark, the need for
/// the gestational age, the gender and the estimates as GenCORE sets them. The client has no birth
/// date, so these are the rules for an age value.
module Age =

    module Measures = Informedica.GenPRES.Shared.Measures
    module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
    module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
    module CoreConversions = Informedica.GenCore.Lib.Conversions
    module CoreAdultMark = Informedica.GenCore.Lib.Patients.AdultMark
    module CoreGestationalAgeNeed = Informedica.GenCore.Lib.Patients.GestationalAgeNeed
    module CoreEstimates = Informedica.GenCore.Lib.Patients.Estimates

    type CoreMark = Informedica.GenCore.Lib.Patients.AdultMark
    type CoreNeed = Informedica.GenCore.Lib.Patients.GestationalAgeNeed
    type CoreGender = Informedica.GenCore.Lib.Patients.Gender


    /// The contract model's age as a GenCORE age value.
    let ageValue (age: Informedica.GenPRES.Shared.Types.Patient.Age) =
        CoreAgeValue.zero
        |> CoreAgeValue.SetGet.setIntYears (int age.Years)
        |> CoreAgeValue.SetGet.setIntMonths (int age.Months)
        |> CoreAgeValue.SetGet.setIntWeeks (int age.Weeks)
        |> CoreAgeValue.SetGet.setIntDays (int age.Days)


    /// The age in days of the contract model's age.
    let inDays age = age |> ageValue |> CoreAgeValue.getAgeInDays |> int


    /// The contract model's age of an age in days.
    let ofDays days =
        let ys, ms, ws, ds =
            days
            |> CoreConversions.dayFromInt
            |> CoreAgeValue.fromDays
            |> CoreAgeValue.getDef0
            |> fun (y, m, w, d) -> int y, int m, int w, int d

        let nonZero n = if n > 0 then Some n else None

        Patient.Age.create
            (ys |> Measures.toYear)
            (ms |> nonZero |> Option.map Measures.toMonth)
            (ws |> nonZero |> Option.map Measures.toWeek)
            (ds |> nonZero |> Option.map Measures.toDay)


    /// A weeks-and-days age of the contract model, a gestational or a post-menstrual age, as
    /// GenCORE's.
    let weeksDays (awd: AgeWeeksDays) =
        CoreAgeWeeksDays.create
            (int awd.Weeks |> CoreConversions.weekFromInt)
            (int awd.Days |> CoreConversions.dayFromInt)


    /// The days of a weeks-and-days age of the contract model, as GenCORE counts them.
    let weeksDaysInDays awd = awd |> weeksDays |> CoreAgeWeeksDays.toDays


    /// The gestational age in days of the contract model's patient, as GenCORE counts it.
    let gestAgeInDays (model: Patient) =
        model.GestationalAge |> GestationalAge.value |> Option.map weeksDaysInDays


    /// The contract model's weeks-and-days age of a number of days, as GenCORE splits them.
    let weeksDaysOfDays days : AgeWeeksDays =
        let awd = days |> CoreConversions.dayFromInt |> CoreAgeWeeksDays.fromDays

        {
            Weeks = int awd.Weeks |> Measures.toWeek
            Days = int awd.Days |> Measures.toDay
        }


    /// The post-menstrual age, as GenCORE calculates it from the age and the gestational age given:
    /// what the server puts on the patient it answers. None without either.
    let postMenstrualAge age ga =
        Option.map2
            (fun age ga -> CoreAgeWeeksDays.postMenstrualAge (ageValue age) (weeksDays ga))
            age
            (ga |> GestationalAge.value)
        |> Option.map (CoreAgeWeeksDays.toDays >> int >> weeksDaysOfDays)


    /// The gestational age given, with the need GenCORE sets from the age: kept when the
    /// gestational age may be given, dropped from 1 year or without an age.
    let gestationalAge age ga =
        match age |> Option.map ageValue |> CoreGestationalAgeNeed.ofAge with
        | CoreNeed.NoGestationalAge -> GestationalAge.NoGestationalAge
        | CoreNeed.OptionalGestationalAge -> GestationalAge.OptionalGestationalAge ga
        | CoreNeed.RequiredGestationalAge -> GestationalAge.RequiredGestationalAge ga


    /// The adult mark of an age, as GenCORE sets it, with the choice of the user the mark holds. A
    /// patient of 18 years or older holds no choice, so a lower age then starts unmarked.
    let adultMark age mark =
        let marked = mark = AdultMark.MarkableAdult true

        match CoreAdultMark.ofAge (age |> Option.map ageValue) marked with
        | CoreMark.NotAdult -> AdultMark.NotAdult
        | CoreMark.MarkableAdult m -> AdultMark.MarkableAdult m
        | CoreMark.Adult -> AdultMark.Adult


    /// The GenCORE gender of the contract model's gender.
    let coreGender g =
        match g with
        | Male -> CoreGender.Male
        | Female -> CoreGender.Female
        | UnknownGender -> CoreGender.UnknownGender


    /// The estimates GenCORE gives for the contract model's patient from the tables: by its age,
    /// its post-menstrual age and its gender.
    let estimates tables (model: Patient) =
        CoreEstimates.ofAge
            tables
            (model.Gender |> coreGender)
            (model.Age |> Option.map ageValue)
            (model.PostMenstrualAge |> Option.map weeksDays)


/// Where patient data enters from the client. The wire carries a draft, every field optional;
/// a draft becomes a patient here, or the request is refused, so that no draft reaches the
/// services and the rules below them. Every member that receives patient data runs this
/// before anything else. The mapping is in steps: coreDto, the contract model as the
/// GenCORE Dto of patient data, total; the fromDto of that Dto at a date, which reports every
/// reason the data does not read; the check of the minimum data; then the GenFORM patient of the
/// patient data. ofModel and toModel are the way to and from the GenFORM Dto that the Dtos of
/// an order context and an order plan carry.
module Patient =

    module Lib = Informedica.GenForm.Lib.Types
    module LibPatient = Informedica.GenForm.Lib.Patient
    module LibGender = Informedica.GenForm.Lib.Gender
    module LibAccess = Informedica.GenForm.Lib.AccessDevice
    module LibRenal = Informedica.GenForm.Lib.RenalFunction
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData
    module CoreDepartment = Informedica.GenCore.Lib.Patients.Department
    module CoreGender = Informedica.GenCore.Lib.Patients.Gender
    module CoreAccess = Informedica.GenCore.Lib.Patients.Access
    module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
    module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
    module CoreRenal = Informedica.GenCore.Lib.Patients.RenalFunction
    module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
    module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
    module CoreWeight = Informedica.GenCore.Lib.Patients.Weight
    module CoreHeightValue = Informedica.GenCore.Lib.Patients.HeightValue
    module CoreHeightAtDate = Informedica.GenCore.Lib.Patients.HeightAtDate
    module CoreHeight = Informedica.GenCore.Lib.Patients.Height
    module Measures = Informedica.GenPRES.Shared.Measures

    type CoreAccess = Informedica.GenCore.Lib.Patients.Access
    type CoreRenalFunction = Informedica.GenCore.Lib.Patients.RenalFunction
    type CoreSource<'v> = Informedica.GenCore.Lib.Patients.Source<'v>
    type CoreError = Informedica.GenCore.Lib.Patients.PatientDataError

    /// A draft that is no patient: it has no age, and no measured weight and height.
    let noPatient = "Geen patiënt: een leeftijd, of een gemeten gewicht en lengte, is nodig"

    /// A child without a weight, measured or estimated. The server estimates the drafts the client
    /// sends, the readings from the platform and the MCP host's values; a child that arrives here
    /// without a weight is refused with the weight named, since the rules are not looked up for
    /// it. An adult needs no weight, and no patient needs a height.
    let noWeight = "Gewicht onbekend: voer het in"


    /// A patient marked as an adult who is younger than 16 years.
    let markedBelowSixteen = "Onder 16 jaar kan een patiënt niet als volwassene gelden"


    /// The patient the data is, or why it is none: an adult, or a child with a weight.
    let patient (dto: Patient) : Result<Patient, string[]> =
        match dto |> Patient.validate with
        | Error PatientError.NoAgeOrMeasuredWeightAndHeight -> Error [| noPatient |]
        | Ok pat ->
            if pat |> Patient.selectsRules then
                Ok pat
            else
                Error [| noWeight |]


    /// Data that may be absent: none is no patient and no refusal.
    let patientOption (dto: Patient option) : Result<Patient option, string[]> =
        match dto with
        | None -> Ok None
        | Some dto -> dto |> patient |> Result.map Some


    let gender =
        function
        | Male -> Lib.Male
        | Female -> Lib.Female
        | UnknownGender -> Lib.AnyGender


    let genderBack =
        function
        | Lib.Male -> Male
        | Lib.Female -> Female
        | Lib.AnyGender -> UnknownGender


    let access =
        function
        | CVL -> Lib.CVL
        | PVL -> Lib.PVL
        | EnteralTube -> Lib.EnteralTube


    /// A rule's "any access" is no device a patient has; the contract model has no case for it.
    let accessBack =
        function
        | Lib.CVL -> Some CVL
        | Lib.PVL -> Some PVL
        | Lib.EnteralTube -> Some EnteralTube
        | Lib.AnyAccess -> None


    let renal =
        function
        | EGFR(min, max) -> Lib.RenalFunction.EGFR(min, max)
        | IntermittentHemodialysis -> Lib.RenalFunction.IntermittentHemodialysis
        | ContinuousHemodialysis -> Lib.RenalFunction.ContinuousHemodialysis
        | PeritonealDialysis -> Lib.RenalFunction.PeritonealDialysis


    let renalBack =
        function
        | Lib.RenalFunction.EGFR(min, max) -> EGFR(min, max)
        | Lib.RenalFunction.IntermittentHemodialysis -> IntermittentHemodialysis
        | Lib.RenalFunction.ContinuousHemodialysis -> ContinuousHemodialysis
        | Lib.RenalFunction.PeritonealDialysis -> PeritonealDialysis


    let thousand = BigRational.fromInt 1000


    /// Total: the contract model's patient as the domain's Dto, nothing dropped. The age in
    /// days as GenCORE counts it, the post-menstrual age as the server calculated it and the
    /// contract model carries it, the weight and height the measured ones when
    /// there are any and the estimates otherwise, with the flags saying which; the department
    /// as given, an empty one staying empty; the mark as an adult as given.
    let ofModel (model: Patient) : LibPatient.Dto.Dto =
        {
            Location = model.Location
            Department = model.Department
            Diagnoses = [||]
            Gender = model.Gender |> gender |> LibGender.toString
            AgeDays = model.Age |> Option.map (Age.inDays >> BigRational.fromInt)
            WeightKg =
                model
                |> Patient.getWeight
                |> Option.map (fun g -> BigRational.fromInt (int g) / thousand)
            HeightCm = model |> Patient.getHeight |> Option.map (int >> BigRational.fromInt)
            WeightMeasured = model.Weight.Measured.IsSome
            HeightMeasured = model.Height.Measured.IsSome
            MarkedAdult = model.AdultMark = AdultMark.MarkableAdult true
            GestAgeDays = model |> Age.gestAgeInDays |> Option.map (int >> BigRational.fromInt)
            PMAgeDays =
                model.PostMenstrualAge
                |> Option.map (Age.weeksDaysInDays >> int >> BigRational.fromInt)
            Access = model.Access |> List.map (access >> LibAccess.toString) |> List.toArray
            RenalFunction = model.RenalFunction |> Option.map (renal >> LibRenal.toString)
        }


    let toInt (br: BigRational) = br |> BigRational.ToDouble |> int


    /// The Dto as the contract model's patient: the age in years, months, weeks and days as
    /// GenCORE splits a number of days, the gestational and the post-menstrual age, the weight
    /// and height as measured or as an estimate by the flag, the strings read back. What the
    /// contract model holds beside this, the other estimates, is not on the Dto and comes back
    /// empty.
    let toModel (dto: LibPatient.Dto.Dto) : Patient =
        let measured (flag: bool) v = if flag then v else None
        let estimated (flag: bool) v = if flag then None else v

        let grams = dto.WeightKg |> Option.map (fun kg -> kg * thousand |> toInt |> Measures.toGram)

        let cms = dto.HeightCm |> Option.map (toInt >> Measures.toCm)

        let age = dto.AgeDays |> Option.map (toInt >> Age.ofDays)

        {
            Age = age
            GestationalAge =
                dto.GestAgeDays
                |> Option.map (toInt >> Age.weeksDaysOfDays)
                |> Age.gestationalAge age
            PostMenstrualAge = dto.PMAgeDays |> Option.map (toInt >> Age.weeksDaysOfDays)
            Weight =
                {
                    EstimatedP3 = None
                    Estimated = grams |> estimated dto.WeightMeasured
                    EstimatedP97 = None
                    Measured = grams |> measured dto.WeightMeasured
                }
            Height =
                {
                    EstimatedP3 = None
                    Estimated = cms |> estimated dto.HeightMeasured
                    EstimatedP97 = None
                    Measured = cms |> measured dto.HeightMeasured
                }
            Gender =
                dto.Gender
                |> LibGender.tryFromString
                |> Option.map genderBack
                |> Option.defaultValue UnknownGender
            Access =
                dto.Access
                |> Array.toList
                |> List.choose (LibAccess.tryFromString >> Option.bind accessBack)
            RenalFunction = dto.RenalFunction |> Option.bind LibRenal.tryFromString |> Option.map renalBack
            Location = dto.Location
            Department = dto.Department
            AdultMark = AdultMark.MarkableAdult dto.MarkedAdult |> Age.adultMark age
        }


    /// The server's words for a reason the Dto is no patient. Only the first can come from
    /// the contract model, since ofModel writes what the Dto reads; the rest are named for
    /// a Dto from elsewhere.
    let words =
        function
        | Lib.PatientError.NoAgeOrMeasuredWeightAndHeight -> noPatient
        | Lib.PatientError.UnknownGender s -> $"Onbekend geslacht: %s{s}"
        | Lib.PatientError.UnknownAccess s -> $"Onbekende toegang: %s{s}"
        | Lib.PatientError.UnknownRenalFunction s -> $"Onbekende nierfunctie: %s{s}"
        | Lib.PatientError.MarkedAdultBelowSixteen -> markedBelowSixteen


    /// The GenCORE department of the name the contract model holds: a department of a kind that
    /// is not known, since the contract model has no kind; an unknown department without a name.
    let department (name: string option) =
        match name with
        | Some s when s |> String.notEmpty -> CoreDepartment.otherDepartment s
        | _ -> CoreDepartment.unknown


    /// The GenCORE access of the contract model's access.
    let coreAccess =
        function
        | CVL -> CoreAccess.CVL
        | PVL -> CoreAccess.PVL
        | EnteralTube -> CoreAccess.EnteralTube


    /// The contract model's renal function as the string GenCORE reads. A kind of dialysis is the
    /// string of the GenCORE case. An eGFR is written here as its minimum and maximum, since a pair
    /// that is no band has no GenCORE value; such a pair is refused when the string is read.
    let renalString =
        let bound = Option.map string >> Option.defaultValue ""

        function
        | EGFR(min, max) -> $"egfr:%s{bound min}:%s{bound max}"
        | IntermittentHemodialysis -> CoreRenalFunction.IntermittentHemodialysis |> CoreRenal.toString
        | ContinuousHemodialysis -> CoreRenalFunction.ContinuousHemodialysis |> CoreRenal.toString
        | PeritonealDialysis -> CoreRenalFunction.PeritonealDialysis |> CoreRenal.toString


    /// Total: the contract model's patient as the GenCORE Dto of patient data, nothing checked.
    /// The weight and the height are the measured ones when there are any and the estimates
    /// otherwise, dated on the date passed in, since the contract model has no date for them.
    let coreDto today (model: Patient) =
        let dto = CorePatientData.Dto.dto ()

        let weeksDays = Age.weeksDays >> CoreAgeWeeksDays.Dto.toDto

        let source measured v =
            if measured then
                CoreSource.Measured v
            else
                CoreSource.Estimated v

        dto.Location <- model.Location
        dto.Department <- model.Department |> department |> CoreDepartment.toString
        dto.Gender <- model.Gender |> Age.coreGender |> CoreGender.toString
        dto.Age <- model.Age |> Option.map (Age.ageValue >> CoreAgeValue.Dto.toDto)
        dto.MarkedAdult <- model.AdultMark = AdultMark.MarkableAdult true
        dto.GestationalAge <- model.GestationalAge |> GestationalAge.value |> Option.map weeksDays
        dto.PostMenstrualAge <- model.PostMenstrualAge |> Option.map weeksDays
        dto.Access <- model.Access |> List.map (coreAccess >> CoreAccess.toString) |> List.toArray
        dto.RenalFunction <- model.RenalFunction |> Option.map renalString

        dto.Weight <-
            model
            |> Patient.getWeight
            |> Option.map (
                int
                >> decimal
                >> CoreWeightValue.weightInGram
                >> CoreWeightAtDate.create today
                >> source model.Weight.Measured.IsSome
            )
            |> CoreWeight.create [] None []
            |> CoreWeight.Dto.toDto

        dto.Height <-
            model
            |> Patient.getHeight
            |> Option.map (
                int
                >> decimal
                >> CoreHeightValue.heightInCm
                >> CoreHeightAtDate.create today
                >> source model.Height.Measured.IsSome
            )
            |> CoreHeight.create [] None
            |> CoreHeight.Dto.toDto

        dto


    /// The server's words for a reason the patient data is not enough.
    let coreWords =
        function
        | CoreError.NoAgeOrMeasuredWeightAndHeight -> noPatient
        | CoreError.MarkedAdultBelowSixteen -> markedBelowSixteen


    /// The GenCORE patient data of the contract model's patient at a date, or why it is none:
    /// the reasons of the GenCORE Dto as GenCORE words them, and the reason the data is not enough
    /// in the server's words.
    let toCore today model =
        model
        |> coreDto today
        |> CorePatientData.Dto.fromDto today
        |> Result.mapError (Validus.ValidationErrors.toList >> List.toArray)
        |> Result.bind (
            CorePatientData.checkMinimumData
            >> Result.mapError (coreWords >> Array.singleton)
        )


    /// The contract model's patient as the domain patient at a date, or why it is none.
    let toDomain today model = model |> toCore today |> Result.map LibPatient.ofPatientData


    /// The patient the contract model's data is at a date, or why it is none, with the server's
    /// own gate that the rules are looked up for an adult, and for a child with a weight, measured
    /// or estimated.
    let parse today model =
        model
        |> toDomain today
        |> Result.bind (fun pat ->
            if pat |> Informedica.GenOrder.Lib.OrderContext.selectsRules then
                Ok pat
            else
                Error [| noWeight |]
        )


    /// The contract model's patient with the estimates GenCORE gives from the tables: a weight or
    /// a height estimate replaced where GenCORE gives one, kept where it gives none, from 18
    /// years or without a row; the measured values untouched.
    let withEstimates tables (model: Patient) : Patient =
        let est = model |> Age.estimates tables

        let toGram (g: int<Informedica.GenCore.Lib.Measures.gram>) = int g |> Measures.toGram

        let toCm (c: int<Informedica.GenCore.Lib.Measures.cm>) = int c |> Measures.toCm

        { model with
            Weight =
                match est.Weight with
                | Some e ->
                    { model.Weight with
                        EstimatedP3 = Some(toGram e.P3)
                        Estimated = Some(toGram e.Mean)
                        EstimatedP97 = Some(toGram e.P97)
                    }
                | None -> model.Weight
            Height =
                match est.Height with
                | Some e ->
                    { model.Height with
                        EstimatedP3 = Some(toCm e.P3)
                        Estimated = Some(toCm e.Mean)
                        EstimatedP97 = Some(toCm e.P97)
                    }
                | None -> model.Height
        }


    /// The domain patient with the weight and height estimates GenCORE gives for its age,
    /// post-menstrual age and sex, dated on the date passed in; everything else as given. A measured
    /// value is untouched, and a patient the contract model cannot read back stays as it was.
    let estimated today tables (pat: Lib.Patient) : Lib.Patient =
        let withMeasuresOf (est: Lib.Patient) =
            { pat with
                Weight = est.Weight
                Height = est.Height
                WeightMeasured = est.WeightMeasured
                HeightMeasured = est.HeightMeasured
            }

        pat
        |> LibPatient.Dto.toDto
        |> toModel
        |> withEstimates tables
        |> toDomain today
        // only the weight and the height: the contract model has no post-menstrual age and no
        // diagnoses, so the way back would count the post-menstrual age again from the calendar
        // age and drop the diagnoses
        |> Result.map withMeasuresOf
        |> Result.defaultValue pat


    /// The EHR port with every projection estimated on the calendar date the function passed in
    /// gives: the inbound boundary, so no child's reading with an age alone reaches the rules
    /// without a weight. A reading of a patient older than 18 years is left as read. Unestimated
    /// while the tables are not loaded.
    let estimating today normalValues (port: PatientDataPort) : PatientDataPort =
        { port with
            patient =
                fun ehr ->
                    let pat = port.patient ehr

                    match normalValues () with
                    | Some tables -> pat |> estimated (today ()) tables
                    | None -> pat
        }


    /// The request run once every piece of data is a patient, else the first refusal.
    let overAll (dtos: Patient list) (run: unit -> Async<Result<'a, string[]>>) : Async<Result<'a, string[]>> =
        let refused =
            dtos
            |> List.tryPick (fun dto ->
                match dto |> patient with
                | Ok _ -> None
                | Error errs -> Some errs
            )

        match refused with
        | None -> run ()
        | Some errs -> async { return Error errs }


    /// The request run once the data is a patient, else its refusal.
    let over (dto: Patient) run = overAll [ dto ] run


    /// The patient data a plan carries: its own, and that of every context in it.
    let ofPlan (plan: OrderPlan) =
        plan.Patient :: (plan.OrderContexts |> Array.map _.Patient |> Array.toList)


    /// Whether a platform reading is a patient: one that is none counts as no reading.
    let reading (dto: Patient option) = dto |> Option.filter (Patient.validate >> Result.isOk)


    /// The draft with the full estimate from the normal values: the 3rd percentile, the mean and
    /// the 97th percentile of the weight and the height, next to a measured value too, which stays
    /// and is what the calculation reads. A patient of 18 years or older keeps what it has. Without
    /// the normal values the draft stays as it is, and the gate refuses a child without a weight.
    let estimate normalValues (draft: Patient) : Patient =
        match normalValues () with
        | Some tables -> draft |> withEstimates tables
        | None -> draft
