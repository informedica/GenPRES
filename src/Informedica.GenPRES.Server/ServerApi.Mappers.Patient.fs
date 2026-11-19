namespace ServerApi

// The cases of the contract are used without a prefix. The domain is reached through aliases.
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models


/// Maps the ages, the adult mark and the gender of the contract patient to GenCORE and back. The
/// contract patient has no birthdate, only an age.
module Age =

    module Measures = Informedica.GenPRES.Shared.Measures
    module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
    module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
    module CoreConversions = Informedica.GenCore.Lib.Conversions

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


    /// Maps the gestational age of the contract to the GenCORE need for a gestational age.
    let coreGestationalAgeNeed ga =
        match ga with
        | GestationalAge.NoGestationalAge -> CoreNeed.NoGestationalAge
        | GestationalAge.OptionalGestationalAge _ -> CoreNeed.OptionalGestationalAge
        | GestationalAge.RequiredGestationalAge _ -> CoreNeed.RequiredGestationalAge


    /// Maps a GenCORE need and a gestational age to the gestational age of the contract. The gestational age is
    /// dropped when it is not needed.
    let gestationalAgeOfCore need ga =
        match need with
        | CoreNeed.NoGestationalAge -> GestationalAge.NoGestationalAge
        | CoreNeed.OptionalGestationalAge -> GestationalAge.OptionalGestationalAge ga
        | CoreNeed.RequiredGestationalAge -> GestationalAge.RequiredGestationalAge ga


    /// Maps the adult mark of the contract to a GenCORE adult mark.
    let coreAdultMark mark =
        match mark with
        | AdultMark.NotAdult -> CoreMark.NotAdult
        | AdultMark.MarkableAdult m -> CoreMark.MarkableAdult m
        | AdultMark.Adult -> CoreMark.Adult


    /// Maps a GenCORE adult mark to the adult mark of the contract.
    let adultMarkOfCore mark =
        match mark with
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


/// Maps the patient of the contract to the GenCORE patient data and back. Every request with
/// patient data is checked here first, so invalid patient data never reaches the rules.
///
/// The way in: ofModel writes the GenCORE Dto, the Dto is read on today's date, and the
/// minimum data is checked.
module Patient =

    open Validus

    module Lib = Informedica.GenForm.Lib.Types
    module LibRenal = Informedica.GenForm.Lib.RenalFunction
    module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData
    module CorePatientDataOptions = Informedica.GenCore.Lib.Patients.PatientDataOptions
    module CoreDepartment = Informedica.GenCore.Lib.Patients.Department
    module CoreGender = Informedica.GenCore.Lib.Patients.Gender
    module CoreAccess = Informedica.GenCore.Lib.Patients.Access
    module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
    module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
    module CoreRenal = Informedica.GenCore.Lib.Patients.RenalFunction
    module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
    module CoreHeightAtDate = Informedica.GenCore.Lib.Patients.HeightAtDate
    module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
    module CoreHeightValue = Informedica.GenCore.Lib.Patients.HeightValue
    module CoreEdit = Informedica.GenCore.Lib.Patients.PatientDataEdit
    module Measures = Informedica.GenPRES.Shared.Measures

    type CoreAccess = Informedica.GenCore.Lib.Patients.Access
    type CoreGender = Informedica.GenCore.Lib.Patients.Gender
    type CoreRenalFunction = Informedica.GenCore.Lib.Patients.RenalFunction
    type CorePercentiles<'v> = Informedica.GenCore.Lib.Patients.Percentiles<'v>
    type CorePatientDataOptions = Informedica.GenCore.Lib.Patients.PatientDataOptions
    type CoreError = Informedica.GenCore.Lib.Patients.PatientDataError
    type CoreEdit = Informedica.GenCore.Lib.Patients.PatientDataEdit
    type ContractEdit = Informedica.GenPRES.Shared.Api.PatientDataEdit

    /// The message for patient data without an age and without a measured weight and height.
    let noPatient = "Geen patiënt: een leeftijd, of een gemeten gewicht en lengte, is nodig"

    /// The message for a child without a weight. The rules are not looked up for a child without a
    /// weight. An adult needs no weight.
    let noWeight = "Gewicht onbekend: voer het in"


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
    /// weight or height is used when there is one, otherwise the estimate, in gram and cm without a date.
    let ofModel (model: PatientData) =
        let dto = CorePatientData.Dto.dto ()

        let weeksDays = Age.weeksDays >> CoreAgeWeeksDays.Dto.toDto

        let weightDto =
            decimal
            >> CoreWeightValue.weightInGram
            >> CoreWeightAtDate.undated
            >> CoreWeightAtDate.Dto.toDto

        let heightDto =
            decimal
            >> CoreHeightValue.heightInCm
            >> CoreHeightAtDate.undated
            >> CoreHeightAtDate.Dto.toDto

        dto.Location <- model.Location
        dto.Department.OtherDepartment <- model.Department
        dto.Gender <- model.Gender |> Age.coreGender |> CoreGender.toString
        dto.Age <- model.Age |> Option.map (Age.ageValue >> CoreAgeValue.Dto.toDto)
        dto.MarkedAdult <- model.AdultMark = AdultMark.MarkableAdult true
        dto.AdultMark <- model.AdultMark |> Age.coreAdultMark
        dto.GestationalAgeNeed <- model.GestationalAge |> Age.coreGestationalAgeNeed
        dto.GestationalAge <- model.GestationalAge |> GestationalAge.value |> Option.map weeksDays
        dto.PostMenstrualAge <- model.PostMenstrualAge |> Option.map weeksDays
        dto.BodySurfaceArea <- model.BodySurfaceArea
        dto.Access <- model.Access |> List.map (coreAccess >> CoreAccess.toString) |> List.toArray
        dto.RenalFunction <- model.RenalFunction |> Option.map renalString
        dto.Weight.Calculation <- model |> PatientData.getWeight |> Option.map weightDto
        dto.Weight.CalculationEstimated <- model.Weight.Measured.IsNone
        dto.Height.Calculation <- model |> PatientData.getHeight |> Option.map heightDto
        dto.Height.CalculationEstimated <- model.Height.Measured.IsNone

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


    /// Maps the GenCORE options to the options of the contract. An eGFR band becomes its minimum and
    /// maximum.
    let optionsOfCore (opts: CorePatientDataOptions) =
        {
            Weights = opts.Weights |> Array.map (int >> Measures.toGram)
            Heights = opts.Heights |> Array.map (int >> Measures.toCm)
            Years = opts.Years |> List.map (int >> Measures.toYear)
            Months = opts.Months |> List.map (int >> Measures.toMonth)
            Weeks = opts.Weeks |> List.map (int >> Measures.toWeek)
            Days = opts.Days |> List.map (int >> Measures.toDay)
            GestationalWeeks = opts.GestationalWeeks |> List.map (int >> Measures.toWeek)
            GestationalDays = opts.GestationalDays |> List.map (int >> Measures.toDay)
            EGFRBands =
                opts.EGFRBands
                |> List.map (fun o ->
                    {
                        RenalFunction = CoreRenalFunction.EGFR o.Band |> renalOfCore
                        Text = o.Text
                    }
                )
            Dialyses = opts.Dialyses |> List.map renalOfCore
        }


    /// Maps the GenCORE Dto of patient data to the patient of the contract. A string that GenCORE
    /// cannot read is left out. The 3rd percentile, the mean and the 97th percentile come from the
    /// estimates of the Dto; without an estimate, an estimated calculation value is the mean and the
    /// percentiles are empty. The Dto has no options, so the options are None.
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

        let percentiles toUnit calc isEstimated (est: CorePercentiles<_> option) =
            match est with
            | Some e -> Some(toUnit e.P3), Some(toUnit e.Mean), Some(toUnit e.P97)
            | None -> None, (if isEstimated then calc else None), None

        let weightP3, weightMean, weightP97 =
            dto.Estimates.Weight
            |> percentiles (int >> Measures.toGram) grams dto.Weight.CalculationEstimated

        let heightP3, heightMean, heightP97 =
            dto.Estimates.Height
            |> percentiles (int >> Measures.toCm) cms dto.Height.CalculationEstimated

        {
            Age = age
            GestationalAge =
                dto.GestationalAge
                |> Option.map weeksDays
                |> Age.gestationalAgeOfCore dto.GestationalAgeNeed
            PostMenstrualAge = dto.PostMenstrualAge |> Option.map weeksDays
            BodySurfaceArea = dto.BodySurfaceArea
            Weight =
                {
                    EstimatedP3 = weightP3
                    Estimated = weightMean
                    EstimatedP97 = weightP97
                    Measured = grams |> measured dto.Weight.CalculationEstimated
                }
            Height =
                {
                    EstimatedP3 = heightP3
                    Estimated = heightMean
                    EstimatedP97 = heightP97
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
                |> CoreDepartment.Dto.fromDto
                |> Result.toOption
                |> Option.bind CoreDepartment.name
            AdultMark = dto.AdultMark |> Age.adultMarkOfCore
            Options = None
        }


    /// Maps patient data to the patient of the contract.
    let ofPatientData dat = dat |> CorePatientData.Dto.toDto |> toModel


    /// Sets the options of the contract patient from the resources; None without the resources.
    let withOptions resources (model: PatientData) =
        { model with Options = resources |> Option.map (CorePatientDataOptions.ofResources >> optionsOfCore) }


    /// The message for patient data that is not enough to select rules.
    let notEnoughMessage =
        function
        | CoreError.NoAgeOrMeasuredWeightAndHeight -> noPatient


    /// Reads a GenCORE Dto of patient data on today's date, with the resources when they are given.
    /// Returns the messages when a value is invalid.
    let readDto today resources dto =
        dto
        |> CorePatientData.Dto.fromDto today resources
        |> Result.mapError (ValidationErrors.toList >> List.toArray)


    /// Maps the contract patient to GenCORE patient data on today's date, with the resources when they
    /// are given. Returns the messages when a value is invalid.
    let toPatientData today resources model = model |> ofModel |> readDto today resources


    /// Maps the contract patient to GenCORE patient data on today's date. Returns the messages
    /// when a value is invalid or the data is not enough to select rules.
    let toCore today model =
        model
        |> toPatientData today None
        |> Result.bind (
            CorePatientData.checkMinimumData
            >> Result.mapError (notEnoughMessage >> Array.singleton)
        )


    /// Reads a GenCORE Dto of patient data on today's date with the resources, and maps it to the contract
    /// patient with the options. Returns the messages when a value is invalid.
    let completeDto today resources dto =
        dto
        |> readDto today resources
        |> Result.map (ofPatientData >> withOptions resources)


    /// Completes the contract patient through GenCORE on today's date. Returns the messages when a
    /// value is invalid.
    let complete today resources model = model |> ofModel |> completeDto today resources


    /// Maps an edit of the contract to the GenCORE edit.
    let coreEdit edit =
        match edit with
        | ContractEdit.SetYears v -> CoreEdit.SetYears v
        | ContractEdit.SetMonths v -> CoreEdit.SetMonths v
        | ContractEdit.SetWeeks v -> CoreEdit.SetWeeks v
        | ContractEdit.SetDays v -> CoreEdit.SetDays v
        | ContractEdit.SetGestationalWeeks v -> CoreEdit.SetGestationalWeeks v
        | ContractEdit.SetGestationalDays v -> CoreEdit.SetGestationalDays v
        | ContractEdit.SetWeight w -> w |> Option.map (decimal >> CoreWeightValue.weightInGram) |> CoreEdit.SetWeight
        | ContractEdit.SetHeight h -> h |> Option.map (decimal >> CoreHeightValue.heightInCm) |> CoreEdit.SetHeight
        | ContractEdit.SetGender g -> g |> Age.coreGender |> CoreEdit.SetGender
        | ContractEdit.SetRenalFunction rf ->
            rf
            |> Option.bind (renalString >> CoreRenal.tryFromString)
            |> CoreEdit.SetRenalFunction
        | ContractEdit.SetDepartment name -> CoreEdit.SetDepartment name
        | ContractEdit.AddAccess a -> a |> coreAccess |> CoreEdit.AddAccess
        | ContractEdit.RemoveAccess a -> a |> coreAccess |> CoreEdit.RemoveAccess
        | ContractEdit.SetMarkedAdult marked -> CoreEdit.SetMarkedAdult marked


    /// Applies an edit of the panel to the contract patient through GenCORE and completes the result on
    /// today's date. Returns the messages when a value is invalid.
    let edit today resources panelEdit model =
        model
        |> ofModel
        |> CoreEdit.apply (panelEdit |> coreEdit)
        |> completeDto today resources


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


    /// Reads a patient data Dto that the server wrote from valid patient data, with the resources
    /// when they are given. Raises an InvalidOperationException when GenCORE refuses it: that is a
    /// bug, not invalid input.
    let fromOwnDto today resources dto =
        dto
        |> CorePatientData.Dto.fromDto today resources
        |> Result.defaultWith (fun errs ->
            let msgs = errs |> ValidationErrors.toList |> String.concat "; "
            invalidOp $"GenCORE refuses patient data that the server wrote: %s{msgs}"
        )


    /// Wraps the EHR port so that GenCORE completes the patient data it reads with the resources.
    /// Without the resources, the patient data is read as it is. Raises an InvalidOperationException
    /// when GenCORE refuses the Dto.
    let completing today resources (port: EhrPatientPort) : EhrPatientPort =
        { port with
            patientData =
                fun ehr ->
                    let pat = port.patientData ehr

                    match resources () with
                    | Some _ as res -> pat |> CorePatientData.Dto.toDto |> fromOwnDto (today ()) res
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


    /// Keeps a contract patient read from the EHR only when it is an adult, has an age, or has a measured
    /// weight and height.
    let reading (dto: PatientData option) = dto |> Option.filter (PatientData.validate >> Result.isOk)
