/// The patient mapper: the contract model's patient to the domain's Dto and back, and what a
/// patient without a department matches before and after the default went.
module Informedica.GenPRES.Server.Tests.MappersPatientTests

open Expecto
open Expecto.Flip
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Patient.Optics
// after GenForm, so that the contract model's cases win unqualified
open Informedica.GenPRES.Shared.Types
// the units of an age in GenCORE's measures, after the contract model's
open Informedica.GenCore.Lib


module Lib = Informedica.GenForm.Lib.Types
module GA = Informedica.GenPRES.Shared.Models.GestationalAge
module LibPatient = Informedica.GenForm.Lib.Patient


let stub = ServerApi.StubPatientData.patient

/// Every field the mapping carries, in the shape the contract model gives back: a patient of 59
/// days, so that the gestational age is needed.
let full: Informedica.GenPRES.Shared.Types.Patient =
    { stub with
        Age = Some(ServerApi.Age.ofDays 59)
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
    (ServerApi.FormularyService.mapFormularyToFilter
        { Informedica.GenPRES.Shared.Models.Formulary.empty with Patient = model })
        .Patient


let domain model =
    model
    |> ServerApi.Patient.parse
    |> Result.defaultWith (fun e -> failtest $"no patient: %A{e}")


[<Tests>]
let tests =
    testList
        "the patient mapper"
        [
            test "GenCORE counts every age from 0 to 20 years as the contract model does" {
                [ 0 .. 20 * 365 ]
                |> List.map ServerApi.Age.ofDays
                |> List.filter (fun age ->
                    { Informedica.GenPRES.Shared.Models.Patient.empty with Age = Some age }
                    |> Informedica.GenPRES.Shared.Models.Patient.getAgeInDays
                    |> Option.map int
                    <> Some(ServerApi.Age.inDays age)
                )
                |> Expect.isEmpty "the same days"
            }

            test "the stub patient parses and comes back as it was" {
                let pat = domain stub
                pat.Age |> Option.isSome |> Expect.isTrue "an age"
                pat.WeightMeasured |> Expect.isTrue "measured"
                pat.Department |> Expect.equal "no department" None

                pat
                |> LibPatient.Dto.toDto
                |> ServerApi.Patient.toModel
                |> Expect.equal "the same model" stub
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
                pat.Access |> Expect.equal "both" [ Lib.PVL; Lib.EnteralTube ]
                pat.Gender |> Expect.equal "gender" Lib.Female

                pat.RenalFunction
                |> Expect.equal "renal" (Some(Lib.RenalFunction.EGFR(Some 30, Some 50)))

                pat.Department |> Expect.equal "department" (Some "ICK")
                pat.Location |> Expect.equal "location" (Some "UMCU")

                pat.GestAge
                |> Expect.equal
                    "gestational age"
                    (Some(ValueUnit.singleWithUnit Units.Time.day (BigRational.fromInt 255)))

                pat.PMAge
                |> Expect.equal
                    "the post-menstrual age the server put on the wire"
                    (Some(ValueUnit.singleWithUnit Units.Time.day (BigRational.fromInt 314)))
            }

            test "the Formulary patient has the post-menstrual age the patient mapper gives" {
                (Some full |> formularyPatient).PMAge
                |> Expect.equal "the same days" (domain full).PMAge
            }

            test "the Formulary patient has no post-menstrual age without a gestational age" {
                // as the server answers the patient change that removed the gestational age
                let withoutGest = { full with GestationalAge = GestationalAge.RequiredGestationalAge None }

                (Some
                    { withoutGest with
                        PostMenstrualAge = withoutGest.GestationalAge |> ServerApi.Age.postMenstrualAge withoutGest.Age
                    }
                 |> formularyPatient)
                    .PMAge
                |> Expect.isNone "none"
            }

            test "the Formulary patient is the patient every request uses, with the enteral tube and the location" {
                let pat = Some full |> formularyPatient

                pat |> Expect.equal "the same patient" (domain full)

                (pat.Access, pat.Location)
                |> Expect.equal "the PVL and the enteral tube, and UMCU" ([ Lib.PVL; Lib.EnteralTube ], Some "UMCU")
            }

            test "no patient is the empty Formulary patient" {
                None |> formularyPatient |> Expect.equal "empty" LibPatient.patient
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
                |> _.PMAgeDays
                |> Expect.equal "210 days" (Some 210N)
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
                 (back |> ServerApi.Patient.ofModel).PMAgeDays)
                |> Expect.equal "241 days, 241 days" (Some 241, Some 241N)
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
                (domain stub).Department |> Expect.equal "new" None

                (Some stub |> formularyPatient).Department |> Expect.equal "Formulary" None
            }

            test "golden: which categories a patient without a department matches, before and after" {
                let categories =
                    [
                        { PatientCategory.empty with Department = Some "ICK" }
                        { PatientCategory.empty with Department = Some "NICU" }
                        PatientCategory.empty
                    ]

                let rows pat = categories |> List.map (PatientCategory.filterPatient pat)

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
                Informedica.GenPRES.Shared.Models.Patient.empty
                |> ServerApi.Patient.parse
                |> Expect.equal "no patient" (Error [| ServerApi.Patient.noPatient |])

                { Informedica.GenPRES.Shared.Models.Patient.empty with Age = stub.Age }
                |> ServerApi.Patient.parse
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
            ]


    let tables = rows |> Informedica.GenPRES.Shared.Models.NormalValues.ofRows


open EstimateFixtures


let dto (pat: Lib.Patient) = pat |> LibPatient.Dto.toDto


[<Tests>]
let estimateTests =
    testList
        "the estimate at the platform boundary"
        [
            test "an age alone gets the weight and height the client shows, as estimates" {
                let pat =
                    Patient.patient
                    |> Patient.setAge [ Years 2 ]
                    |> Patient.setGender Lib.Male
                    |> ServerApi.Patient.estimated tables
                    |> dto

                (pat.WeightKg, pat.HeightCm, pat.WeightMeasured, pat.HeightMeasured)
                |> Expect.equal "twelve kilograms, 87 centimetres, estimated" (Some 12N, Some 87N, false, false)
            }

            test "a measured weight is untouched, the height estimated beside it" {
                let pat =
                    Patient.patient
                    |> Patient.setAge [ Years 2 ]
                    |> Patient.setGender Lib.Male
                    |> Patient.setWeight (Some(Kilogram 20m))
                    |> ServerApi.Patient.estimated tables
                    |> dto

                (pat.WeightKg, pat.WeightMeasured, pat.HeightCm, pat.HeightMeasured)
                |> Expect.equal "twenty kilograms measured, the height estimated" (Some 20N, true, Some 87N, false)
            }

            test "the department, the gender, the access devices and the diagnoses survive the round trip" {
                let pat =
                    { Patient.patient with
                        Department = Some "NEO"
                        Access = [ Lib.CVL ]
                        Diagnoses = [| "sepsis" |]
                    }
                    |> Patient.setAge [ Years 2 ]
                    |> Patient.setGender Lib.Female
                    |> ServerApi.Patient.estimated tables

                (pat.Department, pat.Gender, pat.Access, pat.Diagnoses)
                |> Expect.equal "kept" (Some "NEO", Lib.Female, [ Lib.CVL ], [| "sepsis" |])
            }

            test "a reading that is no patient stays as it was" {
                Patient.patient
                |> ServerApi.Patient.estimated tables
                |> Expect.equal "unchanged" Patient.patient
            }

            test "an identified patient keeps the age, gestational age and post-menstrual age the projection gives" {
                let ehr = ServerApi.StubPatientData.data ServerApi.StubPatientData.measuredOn "any"

                // the gestational age known, so its post-menstrual age calculated, and no measurement
                let premature =
                    { ehr with
                        Patient =
                            { ehr.Patient with
                                Data.GestationalAge =
                                    Informedica.GenCore.Lib.Patients.AgeWeeksDays.create 26<week> 0<day> |> Some
                                Data.Weight = Informedica.GenCore.Lib.Patients.Weight.unknown
                                Data.Height = Informedica.GenCore.Lib.Patients.Height.unknown
                            }
                            |> Informedica.GenCore.Lib.Patients.Patient.calculatePostMenstrualAge
                    }

                let port = ServerApi.StubPatientData.port
                let projected = port.patient premature
                let estimated = (port |> ServerApi.Patient.estimating (fun () -> Some tables)).patient premature

                (estimated.Age, estimated.GestAge, estimated.PMAge, estimated.Weight.IsSome)
                |> Expect.equal
                    "as projected, the weight estimated"
                    (projected.Age, projected.GestAge, projected.PMAge, true)
            }

            test "the port wrapped: a projection arrives estimated, and unestimated while the tables are not loaded" {
                // the stub patient with its birthdate and no measurement at all
                let ehr = ServerApi.StubPatientData.data ServerApi.StubPatientData.measuredOn "any"

                let bare =
                    { ehr with
                        Patient.Data.Weight = Informedica.GenCore.Lib.Patients.Weight.unknown
                        Patient.Data.Height = Informedica.GenCore.Lib.Patients.Height.unknown
                    }

                let port = ServerApi.StubPatientData.port

                (port |> ServerApi.Patient.estimating (fun () -> Some tables)).patient bare
                |> _.Weight.IsSome
                |> Expect.isTrue "estimated"

                (port |> ServerApi.Patient.estimating (fun () -> None)).patient bare
                |> Expect.equal "as projected" (port.patient bare)
            }
        ]


/// The draft with an age in days, the mark as an adult as the server sets it for the choice of the
/// user, and the measured weight in kg and height in cm.
let draft days marked weight height : Informedica.GenPRES.Shared.Types.Patient =
    let empty = Informedica.GenPRES.Shared.Models.Patient.empty
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
            test "the Shared and GenFORM adult tests agree around the 16th and the 18th birthday" {
                for years in [ 0..20 ] do
                    for d in [ -1; 0; 1 ] do
                        for marked in [ false; true ] do
                            let days = years * 365 + d

                            if days >= 0 then
                                let shared = draft (Some days) marked None None

                                match shared |> ServerApi.Patient.ofModel |> LibPatient.Dto.fromDto with
                                | Ok lib ->
                                    shared
                                    |> Informedica.GenPRES.Shared.Models.Patient.isAdult
                                    |> Expect.equal $"%i{days} days, marked %b{marked}" (lib |> Adult.isAdult)
                                | Error errs -> failtest $"%i{days} days, marked %b{marked}: %A{errs}"

                for marked in [ false; true ] do
                    draft None marked None None
                    |> Informedica.GenPRES.Shared.Models.Patient.isAdult
                    |> Expect.equal $"no age, marked %b{marked}" marked
            }

            test "the Shared and GenFORM validate agree with and without the mark, an age and the measures" {
                let libError =
                    function
                    | Informedica.GenPRES.Shared.Models.PatientError.NoAgeOrMeasuredWeightAndHeight ->
                        Lib.PatientError.NoAgeOrMeasuredWeightAndHeight

                for age in [ None; Some(16 * 365 - 1); Some(16 * 365); Some(18 * 365); Some(40 * 365) ] do
                    for marked in [ false; true ] do
                        for weight in [ None; Some 70 ] do
                            for height in [ None; Some 175 ] do
                                let shared = draft age marked weight height

                                shared
                                |> Informedica.GenPRES.Shared.Models.Patient.validate
                                |> Result.map ignore
                                |> Result.mapError libError
                                |> Expect.equal
                                    $"%A{age} %b{marked} %A{weight} %A{height}"
                                    (shared
                                     |> ServerApi.Patient.ofModel
                                     |> LibPatient.Dto.fromDto
                                     |> Result.map ignore
                                     |> Result.mapError List.head)
            }

            test "an adult without a weight and a height passes the gate, and gets no estimate" {
                let adult = draft (Some(40 * 365)) false None None

                adult |> ServerApi.Patient.patient |> Expect.equal "passes" (Ok adult)

                adult
                |> ServerApi.Patient.estimate (fun () -> Some tables)
                |> ServerApi.Patient.parse
                |> Result.map (fun p -> p.Weight, p.Height)
                |> Expect.equal "no weight, no height" (Ok(None, None))
            }

            test "an adult with a weight and no height passes" {
                draft (Some(40 * 365)) false (Some 70) None
                |> ServerApi.Patient.parse
                |> Result.map (fun p -> p.Weight.IsSome, p.Height.IsSome)
                |> Expect.equal "weight, no height" (Ok(true, false))
            }

            test "a child with a weight and no height passes" {
                let child = draft (Some(10 * 365)) false (Some 30) None

                child |> ServerApi.Patient.patient |> Expect.equal "passes" (Ok child)

                child
                |> ServerApi.Patient.parse
                |> Result.map _.Height.IsSome
                |> Expect.equal "no height" (Ok false)
            }

            test "a child without a weight is refused" {
                let child = draft (Some(10 * 365)) false None (Some 140)

                child
                |> ServerApi.Patient.patient
                |> Expect.equal "refused at the gate" (Error [| ServerApi.Patient.noWeight |])

                child
                |> ServerApi.Patient.parse
                |> Expect.equal "refused" (Error [| ServerApi.Patient.noWeight |])
            }

            test "a child of 2 without measures is estimated and passes" {
                draft (Some(2 * 365)) false None None
                |> ServerApi.Patient.estimate (fun () -> Some tables)
                |> ServerApi.Patient.parse
                |> Result.map (fun p -> p.Weight.IsSome, p.Height.IsSome)
                |> Expect.equal "both estimated" (Ok(true, true))
            }

            test "the mark is carried into the GenFORM patient, and back" {
                let marked = draft (Some(17 * 365)) true None None

                marked
                |> ServerApi.Patient.parse
                |> Result.map (fun p -> p.MarkedAdult, p |> Adult.isAdult)
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
                        Informedica.GenPRES.Shared.Models.Patient.Age.create
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

            test "the GenFORM patient keeps the choice of the user as a bool" {
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
                    { Informedica.GenForm.Lib.Patient.patient with
                        Age = d |> Option.map (BigRational.fromInt >> ValueUnit.singleWithUnit Units.Time.day)
                        MarkedAdult = m
                    }
                    |> Informedica.GenForm.Lib.Patient.Dto.toDto
                    |> ServerApi.Patient.toModel
                    |> _.AdultMark
                )
                |> Expect.equal "the marks" [ AdultMark.NotAdult; AdultMark.MarkableAdult true; AdultMark.Adult ]
            }

            test "the EHR reading of an adult keeps the weight it has and is never marked" {
                let reading =
                    draft (Some(40 * 365)) false (Some 70) None
                    |> ServerApi.Patient.parse
                    |> Result.defaultWith (fun _ -> failtest "a patient")

                let estimated = reading |> ServerApi.Patient.estimated tables

                (estimated.Weight, estimated.Height, estimated.MarkedAdult)
                |> Expect.equal "as read" (reading.Weight, None, false)
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
                [ None; Some 0; Some 195; Some 196; Some 364; Some 365; Some 730 ]
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

            test "a patient under 28 weeks without a gestational age needs one" {
                None
                |> ServerApi.Age.gestationalAge (ofDays (Some 10))
                |> Expect.equal "needed, none given" (GestationalAge.RequiredGestationalAge None)
            }

            test "toModel sets the need from the age" {
                [ 59; 365 ]
                |> List.map (fun d ->
                    { full with Age = Some(ServerApi.Age.ofDays d) }
                    |> ServerApi.Patient.ofModel
                    |> ServerApi.Patient.toModel
                    |> _.GestationalAge
                )
                |> Expect.equal
                    "needed under 28 weeks, none from 1 year"
                    [ full.GestationalAge; GestationalAge.NoGestationalAge ]
            }
        ]
