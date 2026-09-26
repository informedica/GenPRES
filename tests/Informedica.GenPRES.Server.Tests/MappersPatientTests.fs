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
open Shared.Types


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
    |> ServerApi.Patient.parse
    |> Result.defaultWith (fun e -> failtest $"no patient: %A{e}")


[<Tests>]
let tests =
    testList
        "the patient mapper"
        [
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
                                Estimated = Some(Shared.Measures.toGram 32000)
                            }
                        Height =
                            { stub.Height with
                                Measured = None
                                Estimated = Some(Shared.Measures.toCm 140)
                            }
                    }

                estimated
                |> ServerApi.Patient.ofModel
                |> ServerApi.Patient.toModel
                |> Expect.equal "estimated" estimated
            }

            test "outside L3: the other estimates are not on the Dto and come back empty" {
                let withP3 =
                    { stub with Weight = { stub.Weight with EstimatedP3 = Some(Shared.Measures.toGram 30000) } }

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
                    "post-menstrual age, once computed by the old mapper"
                    (Some(ValueUnit.singleWithUnit Units.Time.day (BigRational.fromInt 3905)))
            }

            test "an empty department stays empty; the old mapper made it ICK" {
                (domain stub).Department |> Expect.equal "new" None

                (ServerApi.Mappers.mapFromSharedPatient (Resources.Departments.ofNamed []) stub).Department
                |> Expect.equal "old" (Some "ICK")
            }

            test "golden: which categories a patient without a department matches, before and after" {
                let categories =
                    [
                        { PatientCategory.empty with Department = Some "ICK" }
                        { PatientCategory.empty with Department = Some "NICU" }
                        PatientCategory.empty
                    ]

                let rows pat = categories |> List.map (PatientCategory.filterPatient pat)

                stub
                |> ServerApi.Mappers.mapFromSharedPatient (Resources.Departments.ofNamed [])
                |> rows
                |> Expect.equal "before: ICK and the rules for every department" [ true; false; true ]

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
                Shared.Models.Patient.empty
                |> ServerApi.Patient.parse
                |> Expect.equal "no patient" (Error [| ServerApi.Patient.noPatient |])

                { Shared.Models.Patient.empty with Age = stub.Age }
                |> ServerApi.Patient.parse
                |> Expect.equal "no weight and height" (Error [| ServerApi.Patient.noWeightAndHeight |])
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


    let tables = rows |> Shared.Models.NormalValues.ofRows


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

            test "the department, the gender and the access devices survive the round trip" {
                let pat =
                    { Patient.patient with
                        Department = Some "NEO"
                        Access = [ Lib.CVL ]
                    }
                    |> Patient.setAge [ Years 2 ]
                    |> Patient.setGender Lib.Female
                    |> ServerApi.Patient.estimated tables

                (pat.Department, pat.Gender, pat.Access)
                |> Expect.equal "kept" (Some "NEO", Lib.Female, [ Lib.CVL ])
            }

            test "a reading that is no patient stays as it was" {
                Patient.patient
                |> ServerApi.Patient.estimated tables
                |> Expect.equal "unchanged" Patient.patient
            }

            test "the port wrapped: a projection arrives estimated, and unestimated while the tables are not loaded" {
                // the stub patient with its birthdate and no measurement at all
                let ehr = ServerApi.StubPatientData.data "any"

                let bare =
                    { ehr with
                        Patient =
                            { ehr.Patient with
                                Weight = Informedica.GenCore.Lib.Patients.Weight.unknown
                                Height = Informedica.GenCore.Lib.Patients.Height.unknown
                            }
                    }

                let port = ServerApi.StubPatientData.port
                let at = ServerApi.StubPatientData.measuredOn

                (port |> ServerApi.Patient.estimating (fun () -> Some tables)).patient at bare
                |> _.Weight.IsSome
                |> Expect.isTrue "estimated"

                (port |> ServerApi.Patient.estimating (fun () -> None)).patient at bare
                |> Expect.equal "as projected" (port.patient at bare)
            }
        ]
