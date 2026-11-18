namespace ServerApi

open Informedica.Utils.Lib.BCL
// The cases of the contract are used without a prefix. The domain is reached through aliases.
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models


/// Maps the ages, the adult mark, the gender and the estimates of the contract patient to GenCORE
/// and back. The contract patient has no birthdate, only an age.
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


    /// Maps the age of the contract to a GenCORE age value.
    let ageValue (age: Informedica.GenPRES.Shared.Types.PatientData.Age) =
        CoreAgeValue.zero
        |> CoreAgeValue.SetGet.setIntYears (int age.Years)
        |> CoreAgeValue.SetGet.setIntMonths (int age.Months)
        |> CoreAgeValue.SetGet.setIntWeeks (int age.Weeks)
        |> CoreAgeValue.SetGet.setIntDays (int age.Days)


    /// The age of the contract in days.
    let inDays age = age |> ageValue |> CoreAgeValue.getAgeInDays |> int


    /// Maps an age in days to the age of the contract.
    let ofDays days =
        let ys, ms, ws, ds =
            days
            |> CoreConversions.dayFromInt
            |> CoreAgeValue.fromDays
            |> CoreAgeValue.getDef0
            |> fun (y, m, w, d) -> int y, int m, int w, int d

        let nonZero n = if n > 0 then Some n else None

        PatientData.Age.create
            (ys |> Measures.toYear)
            (ms |> nonZero |> Option.map Measures.toMonth)
            (ws |> nonZero |> Option.map Measures.toWeek)
            (ds |> nonZero |> Option.map Measures.toDay)


    /// Maps an age in weeks and days of the contract to GenCORE.
    let weeksDays (awd: AgeWeeksDays) =
        CoreAgeWeeksDays.create
            (int awd.Weeks |> CoreConversions.weekFromInt)
            (int awd.Days |> CoreConversions.dayFromInt)


    /// An age in weeks and days of the contract, in days.
    let weeksDaysInDays awd = awd |> weeksDays |> CoreAgeWeeksDays.toDays


    /// The gestational age of the contract patient in days.
    let gestAgeInDays (model: PatientData) =
        model.GestationalAge |> GestationalAge.value |> Option.map weeksDaysInDays


    /// Maps a number of days to an age in weeks and days of the contract.
    let weeksDaysOfDays days : AgeWeeksDays =
        let awd = days |> CoreConversions.dayFromInt |> CoreAgeWeeksDays.fromDays

        {
            Weeks = int awd.Weeks |> Measures.toWeek
            Days = int awd.Days |> Measures.toDay
        }


    /// The post-menstrual age that GenCORE calculates from the age and the gestational age. None
    /// when either is missing.
    let postMenstrualAge age ga =
        Option.map2
            (fun age ga -> CoreAgeWeeksDays.postMenstrualAge (ageValue age) (weeksDays ga))
            age
            (ga |> GestationalAge.value)
        |> Option.map (CoreAgeWeeksDays.toDays >> int >> weeksDaysOfDays)


    /// The gestational age with the need for it that GenCORE gives for the age. The gestational age
    /// is dropped from 1 year and without an age.
    let gestationalAge age ga =
        match age |> Option.map ageValue |> CoreGestationalAgeNeed.ofAge with
        | CoreNeed.NoGestationalAge -> GestationalAge.NoGestationalAge
        | CoreNeed.OptionalGestationalAge -> GestationalAge.OptionalGestationalAge ga
        | CoreNeed.RequiredGestationalAge -> GestationalAge.RequiredGestationalAge ga


    /// The adult mark that GenCORE gives for the age and the choice of the user.
    let adultMark age mark =
        let marked = mark = AdultMark.MarkableAdult true

        match CoreAdultMark.ofAge (age |> Option.map ageValue) marked with
        | CoreMark.NotAdult -> AdultMark.NotAdult
        | CoreMark.MarkableAdult m -> AdultMark.MarkableAdult m
        | CoreMark.Adult -> AdultMark.Adult


    /// Maps the gender of the contract to GenCORE.
    let coreGender g =
        match g with
        | Male -> CoreGender.Male
        | Female -> CoreGender.Female
        | UnknownGender -> CoreGender.UnknownGender


    /// Maps a GenCORE gender to the gender of the contract.
    let genderOfCore =
        function
        | CoreGender.Male -> Male
        | CoreGender.Female -> Female
        | CoreGender.UnknownGender -> UnknownGender


    /// The weight and height estimates of GenCORE for the contract patient.
    let estimates tables (model: PatientData) =
        CoreEstimates.ofAge
            tables
            (model.Gender |> coreGender)
            (model.Age |> Option.map ageValue)
            (model.PostMenstrualAge |> Option.map weeksDays)


/// Maps the patient of the contract to the GenCORE patient data and back. Every request with
/// patient data is checked here first, so invalid patient data never reaches the rules.
///
/// The way in: ofModel writes the GenCORE Dto, the Dto is read on today's date, and the
/// minimum data is checked.
module Patient =

    module Lib = Informedica.GenForm.Lib.Types
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
    type CoreGender = Informedica.GenCore.Lib.Patients.Gender
    type CoreRenalFunction = Informedica.GenCore.Lib.Patients.RenalFunction
    type CoreSource<'v> = Informedica.GenCore.Lib.Patients.Source<'v>
    type CoreError = Informedica.GenCore.Lib.Patients.PatientDataError

    /// The message for patient data without an age and without a measured weight and height.
    let noPatient = "Geen patiënt: een leeftijd, of een gemeten gewicht en lengte, is nodig"

    /// The message for a child without a weight. The rules are not looked up for a child without a
    /// weight. An adult needs no weight.
    let noWeight = "Gewicht onbekend: voer het in"


    /// The message for a patient younger than 16 years who is marked as an adult.
    let markedBelowSixteen = "Onder 16 jaar kan een patiënt niet als volwassene gelden"


    /// Checks the contract patient: an adult, or a child with a weight. Returns the message when
    /// the check fails.
    let patient (dto: PatientData) : Result<PatientData, string[]> =
        match dto |> PatientData.validate with
        | Error PatientError.NoAgeOrMeasuredWeightAndHeight -> Error [| noPatient |]
        | Ok pat ->
            if pat |> PatientData.selectsRules then
                Ok pat
            else
                Error [| noWeight |]


    /// Checks a contract patient that may be absent. None passes.
    let patientOption (dto: PatientData option) : Result<PatientData option, string[]> =
        match dto with
        | None -> Ok None
        | Some dto -> dto |> patient |> Result.map Some


    let access =
        function
        | CVL -> Lib.CVL
        | PVL -> Lib.PVL
        | EnteralTube -> Lib.EnteralTube


    /// Maps the access of a rule to the access of the contract. The contract has no case for any
    /// access.
    let accessBack =
        function
        | Lib.CVL -> Some CVL
        | Lib.PVL -> Some PVL
        | Lib.EnteralTube -> Some EnteralTube
        | Lib.AnyAccess -> None


    /// Maps the department name of the contract to a GenCORE department. The contract has no kind
    /// of department. No name gives an unknown department.
    let department (name: string option) =
        match name with
        | Some s when s |> String.notEmpty -> CoreDepartment.otherDepartment s
        | _ -> CoreDepartment.unknown


    /// Maps the access of the contract to GenCORE.
    let coreAccess =
        function
        | CVL -> CoreAccess.CVL
        | PVL -> CoreAccess.PVL
        | EnteralTube -> CoreAccess.EnteralTube


    /// The renal function of the contract as the string of the GenCORE Dto. An eGFR is written as
    /// its minimum and maximum. GenCORE refuses a pair that is not one of its bands.
    let renalString =
        let bound = Option.map string >> Option.defaultValue ""

        function
        | EGFR(min, max) -> $"egfr:%s{bound min}:%s{bound max}"
        | IntermittentHemodialysis -> CoreRenalFunction.IntermittentHemodialysis |> CoreRenal.toString
        | ContinuousHemodialysis -> CoreRenalFunction.ContinuousHemodialysis |> CoreRenal.toString
        | PeritonealDialysis -> CoreRenalFunction.PeritonealDialysis |> CoreRenal.toString


    /// Maps the contract patient to the GenCORE Dto of patient data. Nothing is checked. A measured
    /// weight or height is used when there is one, otherwise the estimate. Both get the date today.
    let ofModel today (model: PatientData) =
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
            |> PatientData.getWeight
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
            |> PatientData.getHeight
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


    /// Maps a GenCORE access to the access of the contract.
    let accessOfCore =
        function
        | CoreAccess.CVL -> CVL
        | CoreAccess.PVL -> PVL
        | CoreAccess.EnteralTube -> EnteralTube


    /// Maps a GenCORE renal function to the renal function of the contract. An eGFR band becomes
    /// its minimum and maximum.
    let renalOfCore renal =
        match renal |> LibRenal.ofPatient with
        | Lib.RenalFunction.EGFR(min, max) -> EGFR(min, max)
        | Lib.RenalFunction.IntermittentHemodialysis -> IntermittentHemodialysis
        | Lib.RenalFunction.ContinuousHemodialysis -> ContinuousHemodialysis
        | Lib.RenalFunction.PeritonealDialysis -> PeritonealDialysis


    /// Maps the GenCORE Dto of patient data to the patient of the contract. A string that GenCORE
    /// cannot read is left out. The Dto has no 3rd and 97th percentile estimates, so those are
    /// empty in the result.
    let toModel (dto: CorePatientData.Dto.Dto) : PatientData =
        let weeksDays (awd: CoreAgeWeeksDays.Dto.Dto) : AgeWeeksDays =
            {
                Weeks = awd.Weeks |> Measures.toWeek
                Days = awd.Days |> Measures.toDay
            }

        let age =
            dto.Age
            |> Option.map (fun a ->
                CoreAgeValue.zero
                |> CoreAgeValue.SetGet.setIntYears (a.Years |> Option.defaultValue 0)
                |> CoreAgeValue.SetGet.setIntMonths (a.Months |> Option.defaultValue 0)
                |> CoreAgeValue.SetGet.setIntWeeks (a.Weeks |> Option.defaultValue 0)
                |> CoreAgeValue.SetGet.setIntDays (a.Days |> Option.defaultValue 0)
                |> CoreAgeValue.getAgeInDays
                |> int
                |> Age.ofDays
            )

        let grams =
            dto.Weight.Calculation
            |> Option.map (fun w ->
                if w.WeightValue.WeightInKg then
                    w.WeightValue.Weight * 1000m
                else
                    w.WeightValue.Weight
                |> int
                |> Measures.toGram
            )

        let cms =
            dto.Height.Calculation
            |> Option.map (fun h ->
                if h.HeightValue.HeightInMeter then
                    h.HeightValue.Height * 100m
                else
                    h.HeightValue.Height
                |> int
                |> Measures.toCm
            )

        let measured isEstimated v = if isEstimated then None else v
        let estimated isEstimated v = if isEstimated then v else None

        {
            Age = age
            GestationalAge = dto.GestationalAge |> Option.map weeksDays |> Age.gestationalAge age
            PostMenstrualAge = dto.PostMenstrualAge |> Option.map weeksDays
            Weight =
                {
                    EstimatedP3 = None
                    Estimated = grams |> estimated dto.Weight.CalculationEstimated
                    EstimatedP97 = None
                    Measured = grams |> measured dto.Weight.CalculationEstimated
                }
            Height =
                {
                    EstimatedP3 = None
                    Estimated = cms |> estimated dto.Height.CalculationEstimated
                    EstimatedP97 = None
                    Measured = cms |> measured dto.Height.CalculationEstimated
                }
            Gender =
                dto.Gender
                |> CoreGender.Validation.validate "Gender"
                |> Result.map Age.genderOfCore
                |> Result.defaultValue UnknownGender
            Access =
                dto.Access
                |> Array.toList
                |> List.choose (CoreAccess.tryFromString >> Option.map accessOfCore)
            RenalFunction =
                dto.RenalFunction
                |> Option.bind CoreRenal.tryFromString
                |> Option.map renalOfCore
            Location = dto.Location
            Department =
                dto.Department
                |> CoreDepartment.fromString
                |> Result.toOption
                |> Option.bind CoreDepartment.name
            AdultMark = AdultMark.MarkableAdult dto.MarkedAdult |> Age.adultMark age
        }


    /// Maps patient data to the patient of the contract.
    let ofPatientData dat = dat |> CorePatientData.Dto.toDto |> toModel


    /// The message for patient data that is not enough to select rules.
    let notEnoughMessage =
        function
        | CoreError.NoAgeOrMeasuredWeightAndHeight -> noPatient
        | CoreError.MarkedAdultBelowSixteen -> markedBelowSixteen


    /// Maps the contract patient to GenCORE patient data on today's date. Returns the messages
    /// when a value is invalid or the data is not enough to select rules.
    let toCore today model =
        model
        |> ofModel today
        |> CorePatientData.Dto.fromDto today
        |> Result.mapError (Validus.ValidationErrors.toList >> List.toArray)
        |> Result.bind (
            CorePatientData.checkMinimumData
            >> Result.mapError (notEnoughMessage >> Array.singleton)
        )


    /// Maps the contract patient to GenCORE patient data and checks that the rules can be looked up:
    /// an adult, or a child with a weight.
    let parse today model =
        model
        |> toCore today
        |> Result.bind (fun pat ->
            if pat |> Informedica.GenOrder.Lib.OrderContext.selectsRules then
                Ok pat
            else
                Error [| noWeight |]
        )


    /// Adds the weight and height estimates of GenCORE to the contract patient. An estimate is
    /// replaced when GenCORE gives one. A measured value is kept.
    let withEstimates tables (model: PatientData) : PatientData =
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


    /// Adds the weight and height estimates of GenCORE to patient data, dated today. A measured
    /// weight or height is kept as it is, with its date and its measurements. The patient data is
    /// returned unchanged when the estimate fails.
    let estimated today tables (pat: CorePatientData) =
        pat
        |> ofPatientData
        |> withEstimates tables
        |> toCore today
        // Only the estimated values are taken over. The contract has no diagnoses, no kind of
        // department and no measurement lists, so the rest of the mapped data is incomplete.
        |> Result.map (fun est ->
            { pat with
                Weight =
                    if pat.Weight |> CoreWeight.isMeasured then
                        pat.Weight
                    else
                        { pat.Weight with Calculation = est.Weight.Calculation }
                Height =
                    if pat.Height |> CoreHeight.isMeasured then
                        pat.Height
                    else
                        { pat.Height with Calculation = est.Height.Calculation }
            }
        )
        |> Result.defaultValue pat


    /// Wraps the EHR port so that the patient data it reads gets weight and height estimates, dated
    /// today. Nothing is estimated while the normal-value tables are not loaded.
    let estimating today normalValues (port: EhrPatientPort) : EhrPatientPort =
        { port with
            patientData =
                fun ehr ->
                    let pat = port.patientData ehr

                    match normalValues () with
                    | Some tables -> pat |> estimated (today ()) tables
                    | None -> pat
        }


    /// Runs the request when every contract patient passes the check. Otherwise returns the first
    /// message.
    let overAll (dtos: PatientData list) (run: unit -> Async<Result<'a, string[]>>) : Async<Result<'a, string[]>> =
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


    /// Runs the request when the contract patient passes the check. Otherwise returns the message.
    let over (dto: PatientData) run = overAll [ dto ] run


    /// The contract patients of an order plan: the patient of the plan and of every order context.
    let ofPlan (plan: OrderPlan) =
        plan.PatientData
        :: (plan.OrderContexts |> Array.map _.PatientData |> Array.toList)


    /// Keeps a contract patient read from the EHR only when it has an age or a measured weight and height.
    let reading (dto: PatientData option) = dto |> Option.filter (PatientData.validate >> Result.isOk)


    /// Adds the weight and height estimates to the contract patient when the normal-value tables
    /// are loaded.
    let estimate normalValues (draft: PatientData) : PatientData =
        match normalValues () with
        | Some tables -> draft |> withEstimates tables
        | None -> draft
