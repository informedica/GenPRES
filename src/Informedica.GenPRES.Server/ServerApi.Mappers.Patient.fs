namespace ServerApi

open Informedica.Utils.Lib.BCL
// the contract model's cases win unqualified; the domain's are reached through the aliases
open Shared.Types
open Shared.Models


/// Where patient data enters from the client. The wire carries a draft, every field optional;
/// a draft becomes a patient here, or the request is refused, so that no draft reaches the
/// services and the rules below them. Every member that receives patient data runs this
/// before anything else. The mapping is two steps: ofModel, the contract model as the
/// domain's Dto, total; then the Dto's own fromDto, which reports every reason the data is
/// no patient. toModel is the way back.
module Patient =

    module Lib = Informedica.GenForm.Lib.Types
    module LibPatient = Informedica.GenForm.Lib.Patient
    module LibGender = Informedica.GenForm.Lib.Gender
    module LibAccess = Informedica.GenForm.Lib.AccessDevice
    module LibRenal = Informedica.GenForm.Lib.RenalFunction

    /// A draft that is no patient: it has no age, and no measured weight and height.
    let noPatient = "Geen patiënt: een leeftijd, of een gemeten gewicht en lengte, is nodig"

    /// The patient the data is, or why it is none.
    let patient (dto: Patient) : Result<Patient, string[]> =
        match dto |> Patient.validate with
        | Error PatientError.NoAgeOrMeasuredWeightAndHeight -> Error [| noPatient |]
        | Ok pat -> Ok pat


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
    /// days as the contract model computes it, the weight and height the measured ones when
    /// there are any and the estimates otherwise, with the flags saying which; the department
    /// as given, an empty one staying empty.
    let ofModel (model: Shared.Types.Patient) : LibPatient.Dto.Dto =
        {
            Location = model.Location
            Department = model.Department
            Diagnoses = [||]
            Gender = model.Gender |> gender |> LibGender.toString
            AgeDays = model |> Patient.getAgeInDays |> Option.bind BigRational.fromFloat
            WeightKg =
                model
                |> Patient.getWeight
                |> Option.map (fun g -> BigRational.fromInt (int g) / thousand)
            HeightCm = model |> Patient.getHeight |> Option.map (int >> BigRational.fromInt)
            WeightMeasured = model.Weight.Measured.IsSome
            HeightMeasured = model.Height.Measured.IsSome
            GestAgeDays = model |> Patient.getGestAgeInDays |> Option.map BigRational.fromInt
            PMAgeDays = model |> Patient.getPostConceptionalAgeInDays |> Option.map BigRational.fromInt
            Access = model.Access |> List.map (access >> LibAccess.toString) |> List.toArray
            RenalFunction = model.RenalFunction |> Option.map (renal >> LibRenal.toString)
        }


    let toInt (br: BigRational) = br |> BigRational.ToDouble |> int


    /// The Dto as the contract model's patient: the age in years, months, weeks and days as
    /// the contract model splits a number of days, the weight and height as measured or as
    /// an estimate by the flag, the strings read back. What the contract model holds beside
    /// this, the other estimates, is not on the Dto and comes back empty.
    let toModel (dto: LibPatient.Dto.Dto) : Shared.Types.Patient =
        let measured (flag: bool) v = if flag then v else None
        let estimated (flag: bool) v = if flag then None else v

        let grams =
            dto.WeightKg
            |> Option.map (fun kg -> kg * thousand |> toInt |> Shared.Measures.toGram)

        let cms = dto.HeightCm |> Option.map (toInt >> Shared.Measures.toCm)

        {
            Age = dto.AgeDays |> Option.map (toInt >> Patient.Age.fromDays)
            GestationalAge =
                dto.GestAgeDays
                |> Option.map (fun d ->
                    let d = toInt d

                    ({
                        Weeks = d / 7 |> Shared.Measures.toWeek
                        Days = d % 7 |> Shared.Measures.toDay
                    }
                    : GestAge)
                )
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


    /// The patient the contract model's data is, or why it is none: the Dto's reasons in the
    /// server's words.
    let parse (model: Shared.Types.Patient) : Result<Lib.Patient, string[]> =
        model
        |> ofModel
        |> LibPatient.Dto.fromDto
        |> Result.mapError (List.map words >> List.toArray)


    /// The domain patient with the estimates the contract model computes for its age,
    /// gestational age and sex: the one estimate, on both sides, so the server's answer and
    /// the panel's display cannot drift. A measured value is untouched, and a patient the
    /// contract model cannot read back stays as it was.
    let estimated (nv: NormalValues) (pat: Lib.Patient) : Lib.Patient =
        pat
        |> LibPatient.Dto.toDto
        |> toModel
        |> NormalValues.apply nv
        |> ofModel
        |> LibPatient.Dto.fromDto
        |> Result.defaultValue pat


    /// The platform port with every reading estimated: the inbound boundary, so no reading
    /// with an age alone reaches the rules without a weight and a height. Unestimated while
    /// the tables are not loaded.
    let estimating (nv: unit -> NormalValues option) (port: PatientDataPort) : PatientDataPort =
        { port with
            read =
                fun pid ->
                    port.read pid
                    |> Option.map (fun pat ->
                        match nv () with
                        | Some nv -> pat |> estimated nv
                        | None -> pat
                    )
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
