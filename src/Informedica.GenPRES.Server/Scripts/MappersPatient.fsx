// Step 3.1 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #752):
// the patient mapper. The contract model's patient becomes the domain's Dto in one total step
// (`ofModel`, nothing dropped: the enteral tube is mapped, an empty department stays empty),
// the Dto becomes a patient or every reason it is none (`fromDto`, since step 1.2b), and the
// way back is `toModel`. `parse` is the two steps with the server's words on a refusal.
//
// Dosing: today an empty department becomes "ICK" before the rules are filtered, so a patient
// without a department gets the rules for ICK and the rules for every department; after this
// step such a patient reaches the rules without one and gets the rules of every department.
// The golden test below records both, on the old mapper and on the new one.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. `git mv ServerApi.Patient.fs ServerApi.Mappers.Patient.fs`; the module keeps its name
//      and its readers (`overAll`, `over`, `ofPlan`, `reading`) and gains `ofModel`, `toModel`,
//      `parse` and `words`. `patient` and `patientOption` stay until Phase 4 retires their
//      callers; the fsproj entry moves with the file.
//   2. tests/Informedica.GenPRES.Server.Tests/MappersPatientTests.fs: the tests below.
//
// Run from this directory: dotnet fsi MappersPatient.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
// after GenForm, so that the contract model's cases win unqualified
open Shared.Types


module PatientMapper =

    module Lib = Informedica.GenForm.Lib.Types
    module LibPatient = Informedica.GenForm.Lib.Patient


    let noPatient = ServerApi.Patient.noPatient
    let noWeightAndHeight = ServerApi.Patient.noWeightAndHeight


    let private gender =
        function
        | Male -> Lib.Male
        | Female -> Lib.Female
        | UnknownGender -> Lib.AnyGender


    let private genderBack =
        function
        | Lib.Male -> Male
        | Lib.Female -> Female
        | Lib.AnyGender -> UnknownGender


    let private access =
        function
        | CVL -> Lib.CVL
        | PVL -> Lib.PVL
        | EnteralTube -> Lib.EnteralTube


    /// A rule's "any access" is no device a patient has; the contract model has no case for it.
    let private accessBack =
        function
        | Lib.CVL -> Some CVL
        | Lib.PVL -> Some PVL
        | Lib.EnteralTube -> Some EnteralTube
        | Lib.AnyAccess -> None


    let private renal =
        function
        | EGFR(min, max) -> Lib.RenalFunction.EGFR(min, max)
        | IntermittentHemodialysis -> Lib.RenalFunction.IntermittentHemodialysis
        | ContinuousHemodialysis -> Lib.RenalFunction.ContinuousHemodialysis
        | PeritonealDialysis -> Lib.RenalFunction.PeritonealDialysis


    let private renalBack =
        function
        | Lib.RenalFunction.EGFR(min, max) -> EGFR(min, max)
        | Lib.RenalFunction.IntermittentHemodialysis -> IntermittentHemodialysis
        | Lib.RenalFunction.ContinuousHemodialysis -> ContinuousHemodialysis
        | Lib.RenalFunction.PeritonealDialysis -> PeritonealDialysis


    let private thousand = BigRational.fromInt 1000


    /// Total: the contract model's patient as the domain's Dto, nothing dropped. The age in
    /// days as the contract model computes it, the weight and height the measured ones when
    /// there are any and the estimates otherwise, with the flags saying which; the department
    /// as given, an empty one staying empty.
    let ofModel (model: Shared.Types.Patient) : LibPatient.Dto.Dto =
        {
            Location = model.Location
            Department = model.Department
            Diagnoses = [||]
            Gender = model.Gender |> gender |> Gender.toString
            AgeDays =
                model
                |> Shared.Models.Patient.getAgeInDays
                |> Option.bind BigRational.fromFloat
            WeightKg =
                model
                |> Shared.Models.Patient.getWeight
                |> Option.map (fun g -> BigRational.fromInt (int g) / thousand)
            HeightCm =
                model
                |> Shared.Models.Patient.getHeight
                |> Option.map (int >> BigRational.fromInt)
            WeightMeasured = model.Weight.Measured.IsSome
            HeightMeasured = model.Height.Measured.IsSome
            GestAgeDays =
                model
                |> Shared.Models.Patient.getGestAgeInDays
                |> Option.map BigRational.fromInt
            PMAgeDays =
                model
                |> Shared.Models.Patient.getPostConceptionalAgeInDays
                |> Option.map BigRational.fromInt
            Access = model.Access |> List.map (access >> AccessDevice.toString) |> List.toArray
            RenalFunction = model.RenalFunction |> Option.map (renal >> RenalFunction.toString)
        }


    let private toInt (br: BigRational) = br |> BigRational.ToDouble |> int


    /// The Dto as the contract model's patient: the age in years, months, weeks and days as
    /// the contract model splits a number of days, the weight and height as measured or as
    /// an estimate by the flag, the strings read back. What the contract model holds beside
    /// this, the other estimates, is not on the Dto and comes back empty.
    let toModel (dto: LibPatient.Dto.Dto) : Shared.Types.Patient =
        let measured (flag: bool) v = if flag then v else None
        let estimated (flag: bool) v = if flag then None else v
        let grams = dto.WeightKg |> Option.map (fun kg -> kg * thousand |> toInt |> Shared.Measures.toGram)
        let cms = dto.HeightCm |> Option.map (toInt >> Shared.Measures.toCm)

        {
            Age = dto.AgeDays |> Option.map (toInt >> Shared.Models.Patient.Age.fromDays)
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
                |> Gender.tryFromString
                |> Option.map genderBack
                |> Option.defaultValue UnknownGender
            Access =
                dto.Access
                |> Array.toList
                |> List.choose (AccessDevice.tryFromString >> Option.bind accessBack)
            RenalFunction = dto.RenalFunction |> Option.bind RenalFunction.tryFromString |> Option.map renalBack
            Location = dto.Location
            Department = dto.Department
        }


    /// The server's words for a reason the Dto is no patient. Only the first can come from
    /// the contract model, since `ofModel` writes what the Dto reads; the rest are named for
    /// a Dto from elsewhere.
    let words =
        function
        | Lib.PatientError.NoAgeOrMeasuredWeightAndHeight -> noPatient
        | Lib.PatientError.UnknownGender s -> $"Onbekend geslacht: %s{s}"
        | Lib.PatientError.UnknownAccess s -> $"Onbekende toegang: %s{s}"
        | Lib.PatientError.UnknownRenalFunction s -> $"Onbekende nierfunctie: %s{s}"


    /// The patient the contract model's data is, or why it is none: the Dto's reasons in the
    /// server's words, and the server's own gate that the rules need a weight and a height,
    /// measured or estimated, until the server estimates.
    let parse (model: Shared.Types.Patient) : Result<Lib.Patient, string[]> =
        model
        |> ofModel
        |> LibPatient.Dto.fromDto
        |> Result.mapError (List.map words >> List.toArray)
        |> Result.bind (fun pat ->
            match pat.Weight, pat.Height with
            | Some _, Some _ -> Ok pat
            | _ -> Error [| noWeightAndHeight |]
        )


// ---------------------------------------------------------------------------
// Tests, for tests/Informedica.GenPRES.Server.Tests/MappersPatientTests.fs
// ---------------------------------------------------------------------------

module MappersPatientTests =

    open Expecto
    open Expecto.Flip

    module Lib = Informedica.GenForm.Lib.Types
    module LibPatient = Informedica.GenForm.Lib.Patient


    let stub = ServerApi.StubPatientData.patient

    /// Every field the mapping carries, in the shape the contract model gives back.
    let full: Shared.Types.Patient =
        { stub with
            Gender = Female
            Access = [ PVL; EnteralTube ]
            RenalFunction = Some(EGFR(Some 30, Some 50))
            GestationalAge =
                Some(
                    {
                        Weeks = Shared.Measures.toWeek 36
                        Days = Shared.Measures.toDay 3
                    }
                    : GestAge
                )
            Location = Some "UMCU"
            Department = Some "ICK"
        }


    let domain model =
        model
        |> PatientMapper.parse
        |> Result.defaultWith (fun e -> failtest $"no patient: %A{e}")


    let tests =
        testList
            "the patient mapper"
            [
                test "the stub patient parses and comes back as it was" {
                    let pat = domain stub
                    pat.Age |> Option.isSome |> Expect.isTrue "an age"
                    pat.WeightMeasured |> Expect.isTrue "measured"
                    pat.Department |> Expect.equal "no department" None

                    pat |> LibPatient.Dto.toDto |> PatientMapper.toModel |> Expect.equal "the same model" stub
                }

                test "L3: to the Dto and back is the identity on what the Dto carries" {
                    full |> PatientMapper.ofModel |> PatientMapper.toModel |> Expect.equal "measured" full

                    let estimated =
                        { stub with
                            Weight =
                                { stub.Weight with
                                    Measured = None
                                    Estimated = Some(Shared.Measures.toGram 32000)
                                }
                            Height =
                                { stub.Height with
                                    Measured = None
                                    Estimated = Some(Shared.Measures.toCm 140)
                                }
                        }

                    estimated
                    |> PatientMapper.ofModel
                    |> PatientMapper.toModel
                    |> Expect.equal "estimated" estimated
                }

                test "outside L3: the other estimates are not on the Dto and come back empty" {
                    let withP3 =
                        { stub with
                            Weight = { stub.Weight with EstimatedP3 = Some(Shared.Measures.toGram 30000) }
                        }

                    withP3 |> PatientMapper.ofModel |> PatientMapper.toModel |> Expect.equal "the stub" stub
                }

                test "the enteral tube survives, and so does everything else" {
                    let pat = domain full
                    pat.Access |> Expect.equal "both" [ Lib.PVL; Lib.EnteralTube ]
                    pat.Gender |> Expect.equal "gender" Lib.Female
                    pat.RenalFunction |> Expect.equal "renal" (Some(Lib.RenalFunction.EGFR(Some 30, Some 50)))
                    pat.Department |> Expect.equal "department" (Some "ICK")
                    pat.Location |> Expect.equal "location" (Some "UMCU")

                    pat.GestAge
                    |> Expect.equal "gestational age" (Some(ValueUnit.singleWithUnit Units.Time.day (BigRational.fromInt 255)))

                    pat.PMAge
                    |> Expect.equal
                        "post-menstrual age, once computed by the old mapper"
                        (Some(ValueUnit.singleWithUnit Units.Time.day (BigRational.fromInt 3905)))
                }

                test "an empty department stays empty; the old mapper made it ICK" {
                    (domain stub).Department |> Expect.equal "new" None

                    (ServerApi.Mappers.mapFromSharedPatient stub).Department
                    |> Expect.equal "old" (Some "ICK")
                }

                test "golden: which categories a patient without a department matches, before and after" {
                    let categories =
                        [
                            { PatientCategory.empty with Department = Some "ICK" }
                            { PatientCategory.empty with Department = Some "NICU" }
                            PatientCategory.empty
                        ]

                    let rows pat =
                        categories |> List.map (PatientCategory.filterPatient pat)

                    stub
                    |> ServerApi.Mappers.mapFromSharedPatient
                    |> rows
                    |> Expect.equal "before: ICK and the rules for every department" [ true; false; true ]

                    domain stub |> rows |> Expect.equal "after: the rules of every department" [ true; true; true ]

                    { stub with Department = Some "ICK" }
                    |> domain
                    |> rows
                    |> Expect.equal "a department given: as before" [ true; false; true ]
                }

                test "the refusals keep the server's words" {
                    Shared.Models.Patient.empty
                    |> PatientMapper.parse
                    |> Expect.equal "no patient" (Error [| PatientMapper.noPatient |])

                    { Shared.Models.Patient.empty with Age = stub.Age }
                    |> PatientMapper.parse
                    |> Expect.equal "no weight and height" (Error [| PatientMapper.noWeightAndHeight |])
                }
            ]


MappersPatientTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore
