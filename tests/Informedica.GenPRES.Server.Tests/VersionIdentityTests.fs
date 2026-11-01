/// The version's identity and the age after the sign: whom a Session is for, what the version
/// it signs names, and what the Session's patient is once the signature has landed.
module Informedica.GenPRES.Server.Tests.VersionIdentityTests

open System
open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open ServerApi

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests
module GenFormPatient = Informedica.GenForm.Lib.Patient
module EhrPatientData = Informedica.GenForm.Lib.EhrPatientData
module OrderPlanVersion = Informedica.GenOrder.Lib.OrderPlanVersion
module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
module CoreWeight = Informedica.GenCore.Lib.Patients.Weight
module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
module CorePatientAge = Informedica.GenCore.Lib.Patients.PatientAge


/// The date of the open, and the sign a day later. The port here projects at the date given,
/// so that the age moves with the clock.
let today = DateTime(2026, 9, 26)

let tomorrow = today.AddDays 1.0

let port = StubPatientData.port

let ehr = StubPatientData.data "stub-patient"

let identity = ehr |> EhrPatientData.identity


/// The stub's EHR data with a weight the EHR has since changed.
let heavier =
    let w = CoreWeightAtDate.create tomorrow (CoreWeightValue.weightInKg 34m)

    { ehr with Patient = { ehr.Patient with Weight = CoreWeight.create [ w ] None [] (Some w) } }


/// EHR data with an age value in place of a birthdate: no identity.
let unidentified =
    { ehr with Patient = { ehr.Patient with Age = CoreAgeValue.ten |> CorePatientAge.ageValue } }


/// The plan as signed: the projection at the open, the age the Session held.
let plan =
    Informedica.GenPRES.Shared.Models.OrderPlan.create
        (port.patient today ehr |> GenFormPatient.Dto.toDto |> Patient.toModel)
        [||]
    |> OrderPlanCommand.parsePlan
    |> Result.defaultWith (fun e -> failtest $"no plan: %A{e}")


let version = Store.versionOf 1 Store.prescriber tomorrow plan


/// A Session opened at today on the EHR data given, with the head given, seen at today.
let opened (sid: string) (ehr: Informedica.GenForm.Lib.Types.EhrPatientData option) (head: StoredVersion option) =
    sid,
    ({
        Opened =
            {
                User = Some Store.prescriber
                PatientId = Some "stub-patient"
                EhrData = ehr
                Patient =
                    ehr
                    |> Option.map (port.patient today)
                    |> Option.orElse (Session.sessionPatient today port None head)
                OpenedToken = Some(Store.token sid)
                KeyThumbprint = Some "t"
                Head = head
            }
        Login = Some Store.prescriber.UserId
        OpenedWith = head |> Option.bind StoredVersion.readableId
        OpenedAt = today
        Seen = today
    }
    : Session.SessionRecord)


let unreadable: UnreadableVersion =
    {
        Id = "plan-0"
        No = 1
        PatientId = "stub-patient"
        Base = None
        SignedBy = version.SignedBy
        SignedAt = today
        Identity = identity
        Reason = "a structure version this release does not know"
    }


let challengeOver (read: Informedica.GenForm.Lib.Types.EhrPatientData option) : Session.Challenge =
    {
        Nonce = "c-s-1"
        Digest = StubDatabase.digest plan
        Ehr = read
        Reading = read |> Option.map (port.patient today)
        Expiry = tomorrow + Session.challengeLifetime
    }


/// The commit at tomorrow, over the Session and the challenge given, through the port whose
/// projection moves with the clock.
let signedAt (record: string * Session.SessionRecord) (challenge: Session.Challenge) =
    let sid, _ = record

    let state = { Store.stateOf [ record ] [] with Challenges = Map.ofList [ sid, challenge ] }

    let state, answer, _ =
        Session.commit
            tomorrow
            (Store.counter "id")
            StubDatabase.digest
            Store.registry
            ignore
            sid
            (Store.signature sid "1234" "k-1" plan)
            state

    match answer with
    | SigningOutcome.Submitted(version, whom, token, patient) -> state.Sessions[sid], version, whom, token, patient
    | other -> failtest $"expected Submitted, got %A{other}"


let ageInDays (pat: Informedica.GenForm.Lib.Types.Patient) =
    pat.Age
    |> Option.map (ValueUnit.convertTo Units.Time.day >> ValueUnit.getValue >> Array.head)


let weightInKg (pat: Informedica.GenForm.Lib.Types.Patient) =
    pat.Weight
    |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram >> ValueUnit.getValue >> Array.head)


[<Tests>]
let tests =
    testList
        "the version's identity and the age after the sign"
        [
            testList
                "whom a Session is for"
                [
                    test "the EHR data's identity; the head's when the EHR answered none; none from nothing" {
                        [
                            opened "s-1" (Some ehr) None
                            opened "s-1" None (Some(StoredVersion.Readable(version, identity)))
                            opened "s-1" None (Some(StoredVersion.Unreadable unreadable))
                            opened "s-1" None None
                            opened "s-1" None (Some(StoredVersion.Readable(version, None)))
                        ]
                        |> List.map (fun (_, r) -> r.Opened |> OpenedSession.identity |> Option.map _.Name)
                        |> Expect.equal
                            "the stub's name three times, then none twice"
                            [
                                Some StubPatientData.name
                                Some StubPatientData.name
                                Some StubPatientData.name
                                None
                                None
                            ]
                    }

                    test "EHR data without an identity is anonymous, whatever the head says" {
                        opened "s-1" (Some unidentified) (Some(StoredVersion.Readable(version, identity)))
                        |> snd
                        |> fun r -> r.Opened |> OpenedSession.identity
                        |> Expect.isNone "the EHR data decides"
                    }
                ]

            testList
                "without EHR data, the head's patient"
                [
                    let later = today.AddYears 2
                    let readable = StoredVersion.Readable(version, identity)
                    let days n =
                        n |> BigRational.fromInt |> ValueUnit.singleWithUnit Units.Time.day
                    let openedLater = Session.sessionPatient later port None

                    let withoutAges (pat: Informedica.GenForm.Lib.Types.Patient) =
                        { pat with
                            Age = None
                            PMAge = None
                        }

                    let premature =
                        { version with
                            Plan =
                                { version.Plan with
                                    Patient =
                                        { version.Plan.Patient with
                                            GestAge = Some(days 210)
                                            PMAge = Some(days 1)
                                        }
                                }
                        }

                    test "has the age of the date asked for, not of the sign" {
                        let age = openedLater (Some readable) |> Option.bind _.Age

                        age
                        |> Expect.equal "the age the EHR's projection gives at that date" (port.patient later ehr).Age

                        age
                        |> Expect.notEqual "another age than the one signed" version.Plan.Patient.Age
                    }

                    test "is the head's in every other field" {
                        openedLater (Some readable)
                        |> Option.map withoutAges
                        |> Expect.equal "the signed data" (Some(version.Plan.Patient |> withoutAges))
                    }

                    test "has the post-menstrual age that follows the new age" {
                        let pat =
                            openedLater (Some(StoredVersion.Readable(premature, identity)))
                            |> Option.defaultWith (fun () -> failtest "no patient")

                        pat.PMAge
                        |> Expect.equal
                            "the age plus the gestational age"
                            (pat.Age |> Option.map (fun a -> a + days 210))
                    }

                    test "has no post-menstrual age without a gestational age" {
                        openedLater (Some readable) |> Option.bind _.PMAge |> Expect.isNone "none"
                    }

                    test "stays as signed without an identity" {
                        openedLater (Some(StoredVersion.Readable(version, None)))
                        |> Expect.equal "as signed" (Some version.Plan.Patient)
                    }

                    test "gives way to EHR data, whatever the head" {
                        Session.sessionPatient later port (Some ehr) (Some readable)
                        |> Expect.equal "the projection" (Some(port.patient later ehr))
                    }

                    test "is none from nothing, and from a head that cannot be read" {
                        [ openedLater None; openedLater (Some(StoredVersion.Unreadable unreadable)) ]
                        |> Expect.allEqual "none" None
                    }
                ]

            testList
                "the record of a sign"
                [
                    let typed =
                        { version with
                            Plan =
                                { version.Plan with
                                    Patient =
                                        { version.Plan.Patient with
                                            Weight = 30N |> ValueUnit.singleWithUnit Units.Weight.kiloGram |> Some
                                        }
                                }
                        }

                    let signed = typed.Plan.Patient

                    test "the typed weight stands after the sign, whatever the EHR reads" {
                        signed
                        |> Session.atBirthDate tomorrow identity
                        |> weightInKg
                        |> Expect.equal "30 kg" (Some 30N)
                    }

                    test "the patient after the sign is at the age of the sign, the version keeps its own" {
                        (signed |> Session.atBirthDate tomorrow identity |> ageInDays, signed |> ageInDays)
                        |> Expect.equal "a day more" (Some 3842N, Some 3841N)
                    }

                    test "the EHR data of the challenge go with the version and come back as read" {
                        { typed with EhrData = Some heavier }
                        |> OrderPlanVersion.Dto.toDto
                        |> Informedica.GenOrder.Lib.Canonical.serialize
                        |> Informedica.GenOrder.Lib.Canonical.deserialize<OrderPlanVersion.Dto.Dto>
                        |> OrderPlanVersion.Dto.fromDto
                        |> Result.map _.EhrData
                        |> Expect.equal "as read" (Ok(Some heavier))
                    }

                    test "a challenge without a reading keeps none" {
                        typed
                        |> OrderPlanVersion.Dto.toDto
                        |> OrderPlanVersion.Dto.fromDto
                        |> Result.map _.EhrData
                        |> Expect.equal "none" (Ok None)
                    }
                ]

            testList
                "once a signature has landed"
                [
                    test
                        "the version carries the Session's identity, the Session opens on it, and the client gets the Session's patient" {
                        let record, version, whom, token, patient =
                            signedAt (opened "s-1" (Some ehr) None) (challengeOver (Some ehr))

                        (whom,
                         record.Opened.Head |> Option.bind StoredVersion.identity,
                         record.OpenedWith,
                         record.Opened.OpenedToken,
                         Some patient)
                        |> Expect.equal
                            "the stub's identity on the answer and on the head; opened with the version; the fresh token; the Session's patient"
                            (identity, identity, Some version.Id, Some token, record.Opened.Patient)
                    }

                    test "the version names the patient of the read signed on: a notice accepted over another name" {
                        let renamed = { ehr with Patient = { ehr.Patient with Name = "Renamed" } }

                        let record, _, whom, _, _ =
                            signedAt (opened "s-1" (Some ehr) None) (challengeOver (Some renamed))

                        (whom |> Option.map _.Name, record.Opened |> OpenedSession.identity |> Option.map _.Name)
                        |> Expect.equal
                            "the name of the read signed on, on the version and on the Session"
                            (Some "Renamed", Some "Renamed")
                    }

                    test "none in a Session without an identity" {
                        let _, _, whom, _, _ =
                            signedAt (opened "s-1" (Some unidentified) None) (challengeOver (Some unidentified))

                        whom |> Expect.isNone "no identity to name"
                    }

                    test "with the clock a day later, the Session's age is one day more and the version's is not" {
                        let record, version, _, _, _ =
                            signedAt (opened "s-1" (Some ehr) None) (challengeOver (Some ehr))

                        (record.Opened.Patient |> Option.bind ageInDays, version.Plan.Patient |> ageInDays)
                        |> Expect.equal "3842 on the Session, 3841 on the version" (Some 3842N, Some 3841N)
                    }

                    test "the version keeps the EHR data of its challenge, and the Session the patient signed" {
                        let sid, record = opened "s-1" (Some ehr) None
                        let after, version, _, _, patient = signedAt (sid, record) (challengeOver (Some heavier))

                        (version.EhrData, patient |> weightInKg, after.Opened.Patient |> Option.bind weightInKg)
                        |> Expect.equal "the 34 kg read, the 32 kg signed" (Some heavier, Some 32N, Some 32N)
                    }
                ]
        ]
