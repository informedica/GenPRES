/// The patient mapper: the contract model's patient to the domain's Dto and back, and what a
/// patient without a department matches before and after the default went.
module Informedica.GenPRES.Server.Tests.MappersPatientTests

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Patient.AgeParts
// after GenForm, so that the contract model's cases win unqualified
open Informedica.GenPRES.Shared.Types
// the units of an age in GenCORE's measures, after the contract model's
open Informedica.GenCore.Lib


module Lib = Informedica.GenForm.Lib.Types
module GA = Informedica.GenPRES.Shared.Models.GestationalAge
module ContractPatientData = Informedica.GenPRES.Shared.Models.PatientData
module Measures = Informedica.GenPRES.Shared.Measures
module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData
module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
module CoreWeight = Informedica.GenCore.Lib.Patients.Weight
module CoreHeight = Informedica.GenCore.Lib.Patients.Height

type CoreDepartment = Informedica.GenCore.Lib.Patients.Department
type CoreSource<'v> = Informedica.GenCore.Lib.Patients.Source<'v>
type CoreGender = Informedica.GenCore.Lib.Patients.Gender
type CoreAccess = Informedica.GenCore.Lib.Patients.Access
type CoreError = Informedica.GenCore.Lib.Patients.PatientDataError
type CoreDto = Informedica.GenCore.Lib.Patients.PatientData.Dto.Dto


open Informedica.GenPRES.Server.Tests.TestDates


let stub = ServerApi.StubEhrPatient.contractPatient

/// Every field the mapping carries, in the shape the contract model gives back: a patient of 20
/// days, so that the gestational age is needed.
let full: Informedica.GenPRES.Shared.Types.PatientData =
    { stub with
        Age = Some(ServerApi.Age.ofDays 20)
        Gender = Female
        Access = [ PVL; EnteralTube ]
        RenalFunction = Some(EGFR(Some 30, Some 50))
        GestationalAge =
            GestationalAge.RequiredGestationalAge(
                Some(
                    {
                        Weeks = Informedica.GenPRES.Shared.Measures.toWeek 36
                        Days = Informedica.GenPRES.Shared.Measures.toDay 3
                    }
                    : AgeWeeksDays
                )
            )
        Location = Some "UMCU"
        Department = Some "ICK"
    }
    |> fun pat -> { pat with PostMenstrualAge = pat.GestationalAge |> ServerApi.Age.postMenstrualAge pat.Age }


/// The patient the Formulary filter is made for.
let formularyPatient model =
    { Informedica.GenPRES.Shared.Models.Formulary.empty with PatientData = model }
    |> ServerApi.FormularyService.mapFormularyToFilter readAt
    |> Result.map _.PatientData
    |> Result.defaultWith (fun errs -> failtest $"no filter: %A{errs}")


let domain model =
    model
    |> ServerApi.Patient.parse readAt
    |> Result.defaultWith (fun e -> failtest $"no patient: %A{e}")


/// The single value of a value with a unit.
let singleValue vu = vu |> ValueUnit.getValue |> Array.tryExactlyOne


/// The weight in kg as a number.
let weightKg pat = pat |> CorePatientData.weightInKg |> Option.bind singleValue


/// The height in cm as a number.
let heightCm pat = pat |> CorePatientData.heightInCm |> Option.bind singleValue


/// The post-menstrual age of a Dto in days.
let pmaDays (dto: CoreDto) =
    dto.PostMenstrualAge |> Option.map (fun pma -> pma.Weeks * 7 + pma.Days)


[<Tests>]
let tests =
    testList
        "the patient mapper"
        [
            test "the stub patient parses and comes back as it was" {
                let pat = domain stub
                pat.Age |> Option.isSome |> Expect.isTrue "an age"
                pat.Weight |> CoreWeight.isMeasured |> Expect.isTrue "measured"
                pat |> PatientData.department |> Expect.equal "no department" None

                pat |> ServerApi.Patient.ofPatientData |> Expect.equal "the same model" stub
            }

            test "L3: to the Dto and back is the identity on what the Dto carries" {
                full
                |> ServerApi.Patient.ofModel
                |> ServerApi.Patient.toModel
                |> Expect.equal "measured" full

                let estimated =
                    { stub with
                        Weight =
                            { stub.Weight with
                                Measured = None
                                Estimated = Some(Informedica.GenPRES.Shared.Measures.toGram 32000)
                            }
                        Height =
                            { stub.Height with
                                Measured = None
                                Estimated = Some(Informedica.GenPRES.Shared.Measures.toCm 140)
                            }
                    }

                estimated
                |> ServerApi.Patient.ofModel
                |> ServerApi.Patient.toModel
                |> Expect.equal "estimated" estimated
            }

            test "outside L3: the other estimates are not on the Dto and come back empty" {
                let withP3 =
                    { stub with
                        Weight =
                            { stub.Weight with EstimatedP3 = Some(Informedica.GenPRES.Shared.Measures.toGram 30000) }
                    }

                withP3
                |> ServerApi.Patient.ofModel
                |> ServerApi.Patient.toModel
                |> Expect.equal "the stub" stub
            }

            test "the enteral tube survives, and so does everything else" {
                let pat = domain full
                pat |> PatientData.access |> Expect.equal "both" [ Lib.PVL; Lib.EnteralTube ]
                pat |> PatientData.gender |> Expect.equal "gender" Lib.Female

                pat
                |> PatientData.renalFunction
                |> Expect.equal "renal" (Some(Lib.RenalFunction.EGFR(Some 30, Some 50)))

                pat |> PatientData.department |> Expect.equal "department" (Some "ICK")
                pat.Location |> Expect.equal "location" (Some "UMCU")

                pat
                |> CorePatientData.gestationalAgeInDays
                |> Expect.equal
                    "gestational age"
                    (Some(ValueUnit.singleWithUnit Units.Time.day (BigRational.fromInt 255)))

                pat
                |> CorePatientData.postMenstrualAgeInDays
                |> Expect.equal
                    "the post-menstrual age the server put on the wire"
                    (Some(ValueUnit.singleWithUnit Units.Time.day (BigRational.fromInt 275)))
            }

            test "the Formulary patient has the post-menstrual age the patient mapper gives" {
                (Some full |> formularyPatient).PostMenstrualAge
                |> Expect.equal "the same days" (domain full).PostMenstrualAge
            }

            test "the Formulary patient has no post-menstrual age without a gestational age" {
                // as the server answers the patient change that removed the gestational age
                let withoutGest = { full with GestationalAge = GestationalAge.RequiredGestationalAge None }

                (Some
                    { withoutGest with
                        PostMenstrualAge = withoutGest.GestationalAge |> ServerApi.Age.postMenstrualAge withoutGest.Age
                    }
                 |> formularyPatient)
                    .PostMenstrualAge
                |> Expect.isNone "none"
            }

            test "the Formulary patient is the patient every request uses, with the enteral tube and the location" {
                let pat = Some full |> formularyPatient

                pat |> Expect.equal "the same patient" (domain full)

                (pat |> PatientData.access, pat.Location)
                |> Expect.equal "the PVL and the enteral tube, and UMCU" ([ Lib.PVL; Lib.EnteralTube ], Some "UMCU")
            }

            test "no patient is the empty Formulary patient" {
                None |> formularyPatient |> Expect.equal "empty" PatientData.unknown
            }

            test "the server's post-menstrual age is the age in days plus the gestational age" {
                let gestOf w : AgeWeeksDays =
                    {
                        Weeks = Informedica.GenPRES.Shared.Measures.toWeek w
                        Days = Informedica.GenPRES.Shared.Measures.toDay 3
                    }

                [
                    for days in 0..200 do
                        for weeks in 24..42 do
                            { stub with
                                Age = Some(ServerApi.Age.ofDays days)
                                GestationalAge = GestationalAge.OptionalGestationalAge(Some(gestOf weeks))
                            }
                ]
                |> List.filter (fun pat ->
                    (pat.GestationalAge
                     |> ServerApi.Age.postMenstrualAge pat.Age
                     |> Option.map (ServerApi.Age.weeksDaysInDays >> int))
                    <> (pat.Age
                        |> Option.map ServerApi.Age.inDays
                        |> Option.map2
                            (fun ga age -> age + (ServerApi.Age.weeksDaysInDays ga |> int))
                            (pat.GestationalAge |> GA.value))
                )
                |> Expect.isEmpty "the same days"
            }

            test "no gestational age, or no age: no post-menstrual age" {
                (GestationalAge.NoGestationalAge |> ServerApi.Age.postMenstrualAge stub.Age,
                 full.GestationalAge |> ServerApi.Age.postMenstrualAge None)
                |> Expect.equal "none, none" (None, None)
            }

            test "ofModel reads the post-menstrual age from the wire, not from the age parts" {
                let pma: AgeWeeksDays =
                    {
                        Weeks = Informedica.GenPRES.Shared.Measures.toWeek 30
                        Days = Informedica.GenPRES.Shared.Measures.toDay 0
                    }

                { stub with PostMenstrualAge = Some pma }
                |> ServerApi.Patient.ofModel
                |> pmaDays
                |> Expect.equal "210 days" (Some 210)
            }

            test "toModel puts the body surface area of the Dto on the wire" {
                let dto = stub |> ServerApi.Patient.ofModel
                dto.BodySurfaceArea <- Some 1.1m

                dto
                |> ServerApi.Patient.toModel
                |> _.BodySurfaceArea
                |> Expect.equal "1.1 m2" (Some 1.1m)
            }

            test "the body surface area is the one GenCORE calculates, none without a height or a patient" {
                [
                    stub
                    { stub with AdultMark = AdultMark.MarkableAdult true }
                    { stub with Height = { stub.Height with Measured = None } }
                    ContractPatientData.empty
                ]
                |> List.map (ServerApi.Patient.bodySurfaceArea readAt)
                |> Expect.equal
                    "32 kg and 140 cm, also marked as an adult under 16; no height; no patient"
                    [ Some 1.1m; Some 1.1m; None; None ]
            }

            test "toModel puts the post-menstrual age on the wire, and ofModel reads it back" {
                let gest: AgeWeeksDays =
                    {
                        Weeks = Informedica.GenPRES.Shared.Measures.toWeek 26
                        Days = Informedica.GenPRES.Shared.Measures.toDay 0
                    }

                let model =
                    { stub with
                        Age = Some(ServerApi.Age.ofDays 59)
                        GestationalAge = GestationalAge.RequiredGestationalAge(Some gest)
                    }

                let dto =
                    { model with PostMenstrualAge = model.GestationalAge |> ServerApi.Age.postMenstrualAge model.Age }
                    |> ServerApi.Patient.ofModel

                let back = dto |> ServerApi.Patient.toModel

                (back.PostMenstrualAge |> Option.map (ServerApi.Age.weeksDaysInDays >> int),
                 back |> ServerApi.Patient.ofModel |> pmaDays)
                |> Expect.equal "241 days, 241 days" (Some 241, Some 241)
            }

            test "the gestational age in days and back, as GenCORE counts and splits them" {
                let gestOf w d : AgeWeeksDays =
                    {
                        Weeks = Informedica.GenPRES.Shared.Measures.toWeek w
                        Days = Informedica.GenPRES.Shared.Measures.toDay d
                    }

                { full with GestationalAge = GestationalAge.RequiredGestationalAge(Some(gestOf 28 3)) }
                |> ServerApi.Age.gestAgeInDays
                |> Expect.equal "199 days" (Some 199<Informedica.GenCore.Lib.Measures.day>)

                [ 0; 6; 199; 259 ]
                |> List.map ServerApi.Age.weeksDaysOfDays
                |> Expect.equal "weeks and days" [ gestOf 0 0; gestOf 0 6; gestOf 28 3; gestOf 37 0 ]
            }

            test "an empty department stays empty, in the Formulary mapper too: the lookup applies the default" {
                domain stub |> PatientData.department |> Expect.equal "new" None

                Some stub
                |> formularyPatient
                |> PatientData.department
                |> Expect.equal "Formulary" None
            }

            test "golden: which categories a patient without a department matches, before and after" {
                let categories =
                    [
                        { PatientCategory.empty with Department = Some "ICK" }
                        { PatientCategory.empty with Department = Some "NICU" }
                        PatientCategory.empty
                    ]

                let rows pat =
                    categories |> List.map (PatientData.matchesCategoryWithoutLocationAndGender pat)

                Some stub
                |> formularyPatient
                |> rows
                |> Expect.equal "the Formulary mapper: the same" [ false; false; true ]

                // a patient with no department matches the rules that name none and no others,
                // so the mapper's empty department is no longer every department's rules
                domain stub
                |> rows
                |> Expect.equal "after: the rules that name no department, and no others" [ false; false; true ]

                { stub with Department = Some "ICK" }
                |> domain
                |> rows
                |> Expect.equal "a department given: as before" [ true; false; true ]
            }

            test "the refusals keep the server's words" {
                ContractPatientData.empty
                |> ServerApi.Patient.parse readAt
                |> Expect.equal "no patient" (Error [| ServerApi.Patient.noPatient |])

                { ContractPatientData.empty with Age = stub.Age }
                |> ServerApi.Patient.parse readAt
                |> Expect.equal "no weight" (Error [| ServerApi.Patient.noWeight |])
            }
        ]


module EstimateFixtures =

    /// Two tables, one row per sex at two years: a boy of two is twelve kilograms and 87
    /// centimetres, a girl eleven and 86.
    let rows: Map<string, string[][]> =
        Map
            [
                "weight",
                [|
                    [| "sex"; "age"; "p3"; "mean"; "p97" |]
                    [| "M"; "2"; "10"; "12"; "14" |]
                    [| "F"; "2"; "9"; "11"; "13" |]
                |]
                "height",
                [|
                    [| "sex"; "age"; "p3"; "mean"; "p97" |]
                    [| "M"; "2"; "80"; "87"; "94" |]
                    [| "F"; "2"; "79"; "86"; "93" |]
                |]
                "weight neo", [| [| "sex"; "age"; "p3"; "mean"; "p97" |] |]
                "height neo", [| [| "sex"; "age"; "p3"; "mean"; "p97" |] |]
            ]


    let tables =
        rows
        |> Informedica.GenCore.Lib.Patients.NormalValueTables.ofRows
        |> Result.defaultWith (fun errs -> failtest $"%A{errs}")


open EstimateFixtures


[<Tests>]
let estimateTests =
    testList
        "the estimate at the platform boundary"
        [
            test "an age alone gets the weight and height the client shows, as estimates without a date" {
                let pat =
                    PatientData.unknown
                    |> SamplePatients.withAge [ Years 2 ]
                    |> SamplePatients.withGender CoreGender.Male
                    |> ServerApi.Patient.estimated readAt tables

                (pat |> weightKg,
                 pat |> heightCm,
                 pat.Weight |> CoreWeight.isMeasured,
                 pat.Height |> CoreHeight.isMeasured)
                |> Expect.equal "twelve kilograms, 87 centimetres, estimated" (Some 12N, Some 87N, false, false)

                match pat.Weight.Calculation, pat.Height.Calculation with
                | Some(CoreSource.Estimated w), Some(CoreSource.Estimated h) ->
                    (w.Date, h.Date) |> Expect.equal "no dates" (None, None)
                | other -> failtest $"no estimated weight and height: %A{other}"
            }

            test "the estimates give the body surface area of the estimated weight and height" {
                PatientData.unknown
                |> SamplePatients.withAge [ Years 2 ]
                |> SamplePatients.withGender CoreGender.Male
                |> ServerApi.Patient.estimated readAt tables
                |> _.BodySurfaceArea
                |> Option.map decimal
                |> Expect.equal "twelve kilograms and 87 centimetres" (Some 0.53m)
            }

            test "a measured weight is untouched, the height estimated beside it" {
                let pat =
                    PatientData.unknown
                    |> SamplePatients.withAge [ Years 2 ]
                    |> SamplePatients.withGender CoreGender.Male
                    |> SamplePatients.withWeightInKg 20m
                    |> ServerApi.Patient.estimated readAt tables

                (pat |> weightKg,
                 pat.Weight |> CoreWeight.isMeasured,
                 pat |> heightCm,
                 pat.Height |> CoreHeight.isMeasured)
                |> Expect.equal "twenty kilograms measured, the height estimated" (Some 20N, true, Some 87N, false)
            }

            test "a measured weight of the EHR keeps its date and its measurements, the height is estimated" {
                let ehr = ServerApi.StubEhrPatient.patient ServerApi.StubEhrPatient.measuredOn "any"

                let noHeight = { ehr with Data.Height = Informedica.GenCore.Lib.Patients.Height.unknown }

                let port = ServerApi.StubEhrPatient.port
                let read = port.patientData noHeight

                let estimated =
                    (port |> ServerApi.Patient.estimating (fun () -> readAt) (fun () -> Some tables)).patientData
                        noHeight

                (estimated.Weight, estimated.Height.Calculation.IsSome, estimated.Height |> CoreHeight.isMeasured)
                |> Expect.equal "the weight as read, the height an estimate" (read.Weight, true, false)
            }

            test "a measured weight and height of the EHR are returned as read" {
                let ehr = ServerApi.StubEhrPatient.patient ServerApi.StubEhrPatient.measuredOn "any"
                let port = ServerApi.StubEhrPatient.port

                (port |> ServerApi.Patient.estimating (fun () -> readAt) (fun () -> Some tables)).patientData ehr
                |> Expect.equal "as read" (port.patientData ehr)
            }

            test "the department, the gender, the access devices and the diagnoses survive the round trip" {
                let pat =
                    { PatientData.unknown with
                        Department = CoreDepartment.OtherDepartment "NEO"
                        Access = [ CoreAccess.CVL ]
                        Diagnoses = [| "sepsis" |]
                    }
                    |> SamplePatients.withAge [ Years 2 ]
                    |> SamplePatients.withGender CoreGender.Female
                    |> ServerApi.Patient.estimated readAt tables

                (pat |> PatientData.department, pat |> PatientData.gender, pat |> PatientData.access, pat.Diagnoses)
                |> Expect.equal "kept" (Some "NEO", Lib.Female, [ Lib.CVL ], [| "sepsis" |])
            }

            test "a reading that is no patient stays as it was" {
                PatientData.unknown
                |> ServerApi.Patient.estimated readAt tables
                |> Expect.equal "unchanged" PatientData.unknown
            }

            test "an identified patient keeps the age, gestational age and post-menstrual age the projection gives" {
                let ehr = ServerApi.StubEhrPatient.patient ServerApi.StubEhrPatient.measuredOn "any"

                // A patient with a gestational age, the ages as GenCORE determines them, and no weight or height.
                let premature =
                    { ehr with
                        Data =
                            { ehr.Data with
                                GestationalAge =
                                    Informedica.GenCore.Lib.Patients.AgeWeeksDays.create 26<week> 0<day> |> Some
                                Weight = CoreWeight.unknown
                                Height = CoreHeight.unknown
                            }
                            |> CorePatientData.determineGestationalAndPostMenstrualAge
                    }

                let port = ServerApi.StubEhrPatient.port
                let projected = port.patientData premature
                let estimated =
                    (port |> ServerApi.Patient.estimating (fun () -> readAt) (fun () -> Some tables)).patientData
                        premature

                (estimated.Age,
                 estimated.GestationalAge,
                 estimated.PostMenstrualAge,
                 estimated.Weight.Calculation.IsSome)
                |> Expect.equal
                    "as projected, the weight estimated"
                    (projected.Age, projected.GestationalAge, projected.PostMenstrualAge, true)
            }

            test "the port wrapped: a projection arrives estimated, and unestimated while the tables are not loaded" {
                // the stub patient with its birthdate and no measurement at all
                let ehr = ServerApi.StubEhrPatient.patient ServerApi.StubEhrPatient.measuredOn "any"

                let bare =
                    { ehr with
                        Data.Weight = Informedica.GenCore.Lib.Patients.Weight.unknown
                        Data.Height = Informedica.GenCore.Lib.Patients.Height.unknown
                    }

                let port = ServerApi.StubEhrPatient.port

                (port |> ServerApi.Patient.estimating (fun () -> readAt) (fun () -> Some tables)).patientData bare
                |> _.Weight.Calculation.IsSome
                |> Expect.isTrue "estimated"

                (port |> ServerApi.Patient.estimating (fun () -> readAt) (fun () -> None)).patientData bare
                |> Expect.equal "as projected" (port.patientData bare)
            }
        ]


/// The draft with an age in days, the mark as an adult as the server sets it for the choice of the
/// user, and the measured weight in kg and height in cm.
let draft days marked weight height : Informedica.GenPRES.Shared.Types.PatientData =
    let empty = ContractPatientData.empty
    let age = days |> Option.map ServerApi.Age.ofDays

    { empty with
        Age = age
        AdultMark = AdultMark.MarkableAdult marked |> ServerApi.Age.adultMark age
        Weight =
            { empty.Weight with
                Measured = weight |> Option.map ((*) 1000 >> Informedica.GenPRES.Shared.Measures.toGram)
            }
        Height = { empty.Height with Measured = height |> Option.map Informedica.GenPRES.Shared.Measures.toCm }
    }


[<Tests>]
let adultTests =
    testList
        "the patient the rules are looked up for: an adult, or a child with a weight"
        [
            test "Shared and GenFORM agree on who is an adult around the 16th and the 18th birthday" {
                for years in [ 0..20 ] do
                    for d in [ -1; 0; 1 ] do
                        for marked in [ false; true ] do
                            let days = years * 365 + d

                            if days >= 0 then
                                let shared = draft (Some days) marked None None

                                match shared |> ServerApi.Patient.ofModel |> CorePatientData.Dto.fromDto readAt with
                                | Ok lib ->
                                    shared
                                    |> ContractPatientData.isAdult
                                    |> Expect.equal $"%i{days} days, marked %b{marked}" (lib |> PatientData.isAdult)
                                | Error errs -> failtest $"%i{days} days, marked %b{marked}: %A{errs}"

                for marked in [ false; true ] do
                    draft None marked None None
                    |> ContractPatientData.isAdult
                    |> Expect.equal $"no age, marked %b{marked}" marked
            }

            test "Shared and GenCORE agree on the minimum data" {
                let libError =
                    function
                    | Informedica.GenPRES.Shared.Models.PatientError.NoAgeOrMeasuredWeightAndHeight ->
                        CoreError.NoAgeOrMeasuredWeightAndHeight

                for age in [ None; Some(16 * 365 - 1); Some(16 * 365); Some(18 * 365); Some(40 * 365) ] do
                    for marked in [ false; true ] do
                        for weight in [ None; Some 70 ] do
                            for height in [ None; Some 175 ] do
                                let shared = draft age marked weight height

                                shared
                                |> ContractPatientData.validate
                                |> Result.map ignore
                                |> Result.mapError libError
                                |> Expect.equal
                                    $"%A{age} %b{marked} %A{weight} %A{height}"
                                    (shared
                                     |> ServerApi.Patient.ofModel
                                     |> CorePatientData.Dto.fromDto readAt
                                     |> Result.defaultWith (fun errs -> failtest $"%A{errs}")
                                     |> CorePatientData.checkMinimumData
                                     |> Result.map ignore)
            }

            test "an adult without a weight and a height passes the gate, and gets no estimate" {
                let adult = draft (Some(40 * 365)) false None None

                adult |> ServerApi.Patient.patient |> Expect.equal "passes" (Ok adult)

                adult
                |> ServerApi.Patient.estimate (fun () -> Some tables)
                |> ServerApi.Patient.parse readAt
                |> Result.map (fun p -> p |> Patient.getWeight, p |> Patient.getHeight)
                |> Expect.equal "no weight, no height" (Ok(None, None))
            }

            test "an adult with a weight and no height passes" {
                draft (Some(40 * 365)) false (Some 70) None
                |> ServerApi.Patient.parse readAt
                |> Result.map (fun p -> p.Weight.Calculation.IsSome, p.Height.Calculation.IsSome)
                |> Expect.equal "weight, no height" (Ok(true, false))
            }

            test "a child with a weight and no height passes" {
                let child = draft (Some(10 * 365)) false (Some 30) None

                child |> ServerApi.Patient.patient |> Expect.equal "passes" (Ok child)

                child
                |> ServerApi.Patient.parse readAt
                |> Result.map _.Height.Calculation.IsSome
                |> Expect.equal "no height" (Ok false)
            }

            test "a child without a weight is refused" {
                let child = draft (Some(10 * 365)) false None (Some 140)

                child
                |> ServerApi.Patient.patient
                |> Expect.equal "refused at the gate" (Error [| ServerApi.Patient.noWeight |])

                child
                |> ServerApi.Patient.parse readAt
                |> Expect.equal "refused" (Error [| ServerApi.Patient.noWeight |])
            }

            test "a child of 2 without measures is estimated and passes" {
                draft (Some(2 * 365)) false None None
                |> ServerApi.Patient.estimate (fun () -> Some tables)
                |> ServerApi.Patient.parse readAt
                |> Result.map (fun p -> p.Weight.Calculation.IsSome, p.Height.Calculation.IsSome)
                |> Expect.equal "both estimated" (Ok(true, true))
            }

            test "the mark is carried into the patient data, and back" {
                let marked = draft (Some(17 * 365)) true None None

                marked
                |> ServerApi.Patient.parse readAt
                |> Result.map (fun p -> p.MarkedAdult, p |> PatientData.isAdult)
                |> Expect.equal "marked, an adult" (Ok(true, true))

                marked
                |> ServerApi.Patient.ofModel
                |> ServerApi.Patient.toModel
                |> _.AdultMark
                |> Expect.equal "back" (AdultMark.MarkableAdult true)
            }

            test "the server sets the mark from the age and the choice of the user" {
                [
                    None, true
                    Some(10 * 365), true
                    Some(17 * 365), true
                    Some(17 * 365), false
                    Some(30 * 365), false
                ]
                |> List.map (fun (d, m) -> (draft d m None None).AdultMark)
                |> Expect.equal
                    "the marks"
                    [
                        AdultMark.MarkableAdult true
                        AdultMark.NotAdult
                        AdultMark.MarkableAdult true
                        AdultMark.MarkableAdult false
                        AdultMark.Adult
                    ]
            }

            test "the mark is set by the calendar: 16 years less one day, 16 years, 18 years less one day" {
                [ 5839; 16 * 365; 18 * 365 - 1; 18 * 365 ]
                |> List.map (fun d -> (draft (Some d) true None None).AdultMark)
                |> Expect.equal
                    "the marks"
                    [
                        AdultMark.NotAdult
                        AdultMark.MarkableAdult true
                        AdultMark.MarkableAdult true
                        AdultMark.Adult
                    ]
            }

            test "an age in months: 191 months not markable, 192 markable, 215 markable, 216 an adult" {
                [ 191; 192; 215; 216 ]
                |> List.map (fun months ->
                    let age =
                        ContractPatientData.Age.create
                            (Informedica.GenPRES.Shared.Measures.toYear 0)
                            (Some(Informedica.GenPRES.Shared.Measures.toMonth months))
                            None
                            None

                    AdultMark.MarkableAdult true |> ServerApi.Age.adultMark (Some age)
                )
                |> Expect.equal
                    "the marks"
                    [
                        AdultMark.NotAdult
                        AdultMark.MarkableAdult true
                        AdultMark.MarkableAdult true
                        AdultMark.Adult
                    ]
            }

            test "the server keeps the choice when the age stays markable, and sets it again on a new age" {
                let marked17 = (draft (Some(17 * 365)) true None None).AdultMark
                let mark days = ServerApi.Age.adultMark (Some(ServerApi.Age.ofDays days))

                [
                    marked17 |> mark (16 * 365)
                    marked17 |> mark (10 * 365)
                    AdultMark.Adult |> mark (17 * 365)
                ]
                |> Expect.equal
                    "kept from 16 to 18, gone below 16, none after 18"
                    [
                        AdultMark.MarkableAdult true
                        AdultMark.NotAdult
                        AdultMark.MarkableAdult false
                    ]
            }

            test "the Dto keeps the choice of the user as a bool" {
                [
                    draft (Some(10 * 365)) true None None
                    draft (Some(17 * 365)) false None None
                    draft (Some(17 * 365)) true None None
                    draft (Some(40 * 365)) true None None
                ]
                |> List.map (ServerApi.Patient.ofModel >> _.MarkedAdult)
                |> Expect.equal "only a marked patient" [ false; false; true; false ]
            }

            test "the reading of the server carries the mark it sets for the age" {
                [ Some(10 * 365), true; Some(17 * 365), true; Some(40 * 365), false ]
                |> List.map (fun (d, m) ->
                    { PatientData.unknown with
                        Age = d |> Option.map (fun days -> CoreAgeValue.fromDays (days * 1<day>))
                        MarkedAdult = m
                    }
                    |> ServerApi.Patient.ofPatientData
                    |> _.AdultMark
                )
                |> Expect.equal "the marks" [ AdultMark.NotAdult; AdultMark.MarkableAdult true; AdultMark.Adult ]
            }

            test "the EHR reading of an adult keeps the weight it has and is never marked" {
                let reading =
                    draft (Some(40 * 365)) false (Some 70) None
                    |> ServerApi.Patient.parse readAt
                    |> Result.defaultWith (fun _ -> failtest "a patient")

                let estimated = reading |> ServerApi.Patient.estimated readAt tables

                (estimated.Weight, estimated.Height, estimated.MarkedAdult)
                |> Expect.equal "as read" (reading.Weight, CoreHeight.unknown, false)
            }
        ]


[<Tests>]
let gestationalAgeTests =
    let awd w d : AgeWeeksDays =
        {
            Weeks = Informedica.GenPRES.Shared.Measures.toWeek w
            Days = Informedica.GenPRES.Shared.Measures.toDay d
        }

    let ga30 = awd 30 0
    let ofDays = Option.map ServerApi.Age.ofDays

    testList
        "the need for the gestational age"
        [
            test "the server sets the need from the age and keeps the value given while it may be given" {
                [ None; Some 0; Some 27; Some 28; Some 364; Some 365; Some 730 ]
                |> List.map (fun d -> Some ga30 |> ServerApi.Age.gestationalAge (ofDays d))
                |> Expect.equal
                    "the gestational ages"
                    [
                        GestationalAge.NoGestationalAge
                        GestationalAge.RequiredGestationalAge(Some ga30)
                        GestationalAge.RequiredGestationalAge(Some ga30)
                        GestationalAge.OptionalGestationalAge(Some ga30)
                        GestationalAge.OptionalGestationalAge(Some ga30)
                        GestationalAge.NoGestationalAge
                        GestationalAge.NoGestationalAge
                    ]
            }

            test "a patient under 28 days without a gestational age needs one" {
                None
                |> ServerApi.Age.gestationalAge (ofDays (Some 10))
                |> Expect.equal "needed, none given" (GestationalAge.RequiredGestationalAge None)
            }

            test "toModel sets the need from the age" {
                [ 20; 365 ]
                |> List.map (fun d ->
                    { full with Age = Some(ServerApi.Age.ofDays d) }
                    |> ServerApi.Patient.ofModel
                    |> ServerApi.Patient.toModel
                    |> _.GestationalAge
                )
                |> Expect.equal
                    "needed under 28 days, none from 1 year"
                    [ full.GestationalAge; GestationalAge.NoGestationalAge ]
            }
        ]


[<Tests>]
let withEstimatesTests =
    testList
        "the estimates GenCORE gives"
        [
            test "a weight estimate replaced where GenCORE gives one, a height estimate kept where it gives none" {
                let tables: Informedica.GenCore.Lib.Patients.NormalValueTables =
                    {
                        Weights =
                            [
                                {
                                    Gender = Informedica.GenCore.Lib.Patients.Male
                                    Age = 10.<Informedica.GenCore.Lib.Measures.year>
                                    P3 = 25.<Informedica.GenCore.Lib.Measures.kg>
                                    Mean = 32.<Informedica.GenCore.Lib.Measures.kg>
                                    P97 = 40.<Informedica.GenCore.Lib.Measures.kg>
                                }
                            ]
                        Heights = []
                        NeoWeights = []
                        NeoHeights = []
                    }

                let model =
                    { ContractPatientData.empty with
                        Age = Some(ServerApi.Age.ofDays (10 * 365))
                        Gender = Male
                        Height =
                            { ContractPatientData.empty.Height with
                                Estimated = Some(Informedica.GenPRES.Shared.Measures.toCm 140)
                            }
                    }
                    |> ServerApi.Patient.withEstimates tables

                (model.Weight.EstimatedP3, model.Weight.Estimated, model.Weight.EstimatedP97, model.Height.Estimated)
                |> Expect.equal
                    "25 to 40 kg, 32 kg; 140 cm kept"
                    (Some(Informedica.GenPRES.Shared.Measures.toGram 25000),
                     Some(Informedica.GenPRES.Shared.Measures.toGram 32000),
                     Some(Informedica.GenPRES.Shared.Measures.toGram 40000),
                     Some(Informedica.GenPRES.Shared.Measures.toCm 140))
            }
        ]


/// An age in weeks and days of the contract model.
let weeksDays ws ds : AgeWeeksDays =
    {
        Weeks = Measures.toWeek ws
        Days = Measures.toDay ds
    }

/// A weight with nothing given.
let noWeight: Weight =
    {
        EstimatedP3 = None
        Estimated = None
        EstimatedP97 = None
        Measured = None
    }

/// A height with nothing given.
let noHeight: Height =
    {
        EstimatedP3 = None
        Estimated = None
        EstimatedP97 = None
        Measured = None
    }

[<Tests>]
let coreTests =
    testList
        "the contract patient as GenCORE patient data"
        [
            test "no department name, or a blank one, is an unknown department" {
                for name in [ None; Some ""; Some "  " ] do
                    { stub with Department = name }
                    |> ServerApi.Patient.toCore readAt
                    |> Result.map _.Department
                    |> Expect.equal "unknown" (Ok CoreDepartment.UnknownDepartment)
            }

            test "a blank department name reaches the rules as no department" {
                { stub with Department = Some "" }
                |> ServerApi.Patient.toCore readAt
                |> Result.map PatientData.department
                |> Expect.equal "no department" (Ok None)
            }

            test "the weight and the height have no date" {
                let dat =
                    stub
                    |> ServerApi.Patient.toCore readAt
                    |> Result.defaultWith (fun errs -> failtest $"no patient data: %A{errs}")

                match dat.Weight.Calculation, dat.Height.Calculation with
                | Some(CoreSource.Measured w), Some(CoreSource.Measured h) ->
                    (w.Date, h.Date) |> Expect.equal "no dates" (None, None)
                | other -> failtest $"no measured weight and height: %A{other}"
            }

            test "an estimated weight is an estimate in GenCORE" {
                let dat =
                    { stub with Weight = { noWeight with Estimated = Some(Measures.toGram 32000) } }
                    |> ServerApi.Patient.toCore readAt
                    |> Result.defaultWith (fun errs -> failtest $"no patient data: %A{errs}")

                match dat.Weight.Calculation with
                | Some(CoreSource.Estimated _) -> ()
                | other -> failtest $"no estimate: %A{other}"
            }

            test "a draft without an age and without a measured weight and height is no patient" {
                { stub with
                    Age = None
                    Weight = noWeight
                }
                |> ServerApi.Patient.toCore readAt
                |> Expect.equal "the server's words" (Error [| ServerApi.Patient.noPatient |])
            }

            test "a patient marked as an adult below 16 years is refused" {
                { stub with
                    Age = Some(ServerApi.Age.ofDays (10 * 365))
                    AdultMark = AdultMark.MarkableAdult true
                }
                |> ServerApi.Patient.toCore readAt
                |> Expect.equal "the server's words" (Error [| ServerApi.Patient.markedBelowSixteen |])
            }

            test "an eGFR that is no band of GenCORE is refused, in GenCORE's words" {
                { stub with RenalFunction = Some(EGFR(Some 20, Some 40)) }
                |> ServerApi.Patient.toCore readAt
                |> Expect.equal "refused" (Error [| "Renal function is unknown: egfr:20:40" |])
            }

            test "the Formulary filter of a patient GenCORE refuses is that refusal, not an exception" {
                { Informedica.GenPRES.Shared.Models.Formulary.empty with
                    PatientData = Some { stub with RenalFunction = Some(EGFR(Some 20, Some 40)) }
                }
                |> ServerApi.FormularyService.mapFormularyToFilter readAt
                |> Result.map ignore
                |> Expect.equal "refused" (Error [| "Renal function is unknown: egfr:20:40" |])
            }

            test "a weight outside the bounds of GenCORE is refused" {
                { stub with Weight = { noWeight with Measured = Some(Measures.toGram 300000) } }
                |> ServerApi.Patient.toCore readAt
                |> Expect.isError "300 kg"
            }

            test "a child without a weight is refused with the weight named" {
                { stub with
                    Age = Some(ServerApi.Age.ofDays (2 * 365))
                    Weight = noWeight
                    Height = noHeight
                }
                |> ServerApi.Patient.parse readAt
                |> Expect.equal "the weight" (Error [| ServerApi.Patient.noWeight |])
            }
        ]
