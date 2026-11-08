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
module LibPatient = Informedica.GenForm.Lib.Patient


let stub = ServerApi.StubPatientData.patient

/// Every field the mapping carries, in the shape the contract model gives back.
let full: Informedica.GenPRES.Shared.Types.Patient =
    { stub with
        Gender = Female
        Access = [ PVL; EnteralTube ]
        RenalFunction = Some(EGFR(Some 30, Some 50))
        GestationalAge =
            Some(
                {
                    Weeks = Informedica.GenPRES.Shared.Measures.toWeek 36
                    Days = Informedica.GenPRES.Shared.Measures.toDay 3
                }
                : AgeWeeksDays
            )
        Location = Some "UMCU"
        Department = Some "ICK"
    }
    |> fun pat -> { pat with PostMenstrualAge = pat |> ServerApi.Age.postMenstrualAge }


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
            test "GenCORE splits every day count from 0 to 20 years as the contract model does" {
                [ 0 .. 20 * 365 ]
                |> List.filter (fun d ->
                    ServerApi.Age.ofDays d
                    <> Informedica.GenPRES.Shared.Models.Patient.Age.fromDays d
                )
                |> Expect.isEmpty "the same age"
            }

            test "GenCORE counts every age from 0 to 20 years as the contract model does" {
                [ 0 .. 20 * 365 ]
                |> List.map Informedica.GenPRES.Shared.Models.Patient.Age.fromDays
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
                    (Some(ValueUnit.singleWithUnit Units.Time.day (BigRational.fromInt 3905)))
            }

            test "the Formulary patient has the post-menstrual age the patient mapper gives" {
                (Some full |> formularyPatient).PMAge
                |> Expect.equal "the same days" (domain full).PMAge
            }

            test "the Formulary patient has no post-menstrual age without a gestational age" {
                // as the server answers the patient change that removed the gestational age
                let withoutGest = { full with GestationalAge = None }

                (Some { withoutGest with PostMenstrualAge = withoutGest |> ServerApi.Age.postMenstrualAge }
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
                                GestationalAge = Some(gestOf weeks)
                            }
                ]
                |> List.filter (fun pat ->
                    (pat
                     |> ServerApi.Age.postMenstrualAge
                     |> Option.map (ServerApi.Age.weeksDaysInDays >> int))
                    <> (pat.Age
                        |> Option.map ServerApi.Age.inDays
                        |> Option.map2
                            (fun ga age -> age + (ServerApi.Age.weeksDaysInDays ga |> int))
                            pat.GestationalAge)
                )
                |> Expect.isEmpty "the same days"
            }

            test "no gestational age, or no age: no post-menstrual age" {
                ({ stub with GestationalAge = None } |> ServerApi.Age.postMenstrualAge,
                 { stub with Age = None } |> ServerApi.Age.postMenstrualAge)
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
                        GestationalAge = Some gest
                    }

                let dto =
                    { model with PostMenstrualAge = model |> ServerApi.Age.postMenstrualAge }
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

                { full with GestationalAge = Some(gestOf 28 3) }
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


/// The draft with an age in days, the mark as an adult, and the measured weight in kg and height in cm.
let draft days marked weight height : Informedica.GenPRES.Shared.Types.Patient =
    let empty = Informedica.GenPRES.Shared.Models.Patient.empty

    { empty with
        Age = days |> Option.map Informedica.GenPRES.Shared.Models.Patient.Age.fromDays
        MarkedAdult = marked
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
                                | Error _ ->
                                    shared
                                    |> Informedica.GenPRES.Shared.Models.Patient.validate
                                    |> Expect.equal
                                        $"%i{days} days refused"
                                        (Error Informedica.GenPRES.Shared.Models.PatientError.MarkedAdultBelowSixteen)

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
                    | Informedica.GenPRES.Shared.Models.PatientError.MarkedAdultBelowSixteen ->
                        Lib.PatientError.MarkedAdultBelowSixteen

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
                |> _.MarkedAdult
                |> Expect.isTrue "back"
            }

            test "the mark at 16 years less one day is refused by the server, at exactly 16 years accepted" {
                let young = draft (Some 5839) true None None

                young
                |> ServerApi.Patient.patient
                |> Expect.equal "refused at the gate" (Error [| ServerApi.Patient.markedBelowSixteen |])

                young
                |> ServerApi.Patient.parse
                |> Expect.equal "refused" (Error [| ServerApi.Patient.markedBelowSixteen |])

                draft (Some(16 * 365)) true None None
                |> ServerApi.Patient.parse
                |> Result.isOk
                |> Expect.isTrue "accepted"
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
