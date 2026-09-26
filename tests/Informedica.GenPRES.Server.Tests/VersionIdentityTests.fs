/// The version's identity and the age after the sign: whom a Session is for, what the version
/// it signs names, and what the Session's patient is once the signature has landed.
module Informedica.GenPRES.Server.Tests.VersionIdentityTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open ServerApi

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests
module GenFormPatient = Informedica.GenForm.Lib.Patient
module EhrPatientData = Informedica.GenForm.Lib.EhrPatientData
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
    Shared.Models.OrderPlan.create (port.patient today ehr |> GenFormPatient.Dto.toDto |> Patient.toModel) [||]
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
                Measured = Measurements.none
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
            port
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

                    test "a Session opened from the head is identified and holds the head's age" {
                        Store.stateOf [ opened "s-1" None (Some(StoredVersion.Readable(version, identity))) ] []
                        |> Session.age "s-1"
                        |> Expect.equal
                            "the age the version was signed at"
                            (Some(Shared.Models.Patient.Age.fromDays 3841))
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

                    test "where the EHR reports another weight, the Session keeps the weight the user measured" {
                        let sid, record = opened "s-1" (Some ehr) None

                        let measured =
                            { record with
                                Opened =
                                    { record.Opened with
                                        Measured = Measurements.ofRows [ Measurement.Weight(Some 30000<gram>), today ]
                                    }
                            }

                        let after, _, _, _, _ = signedAt (sid, measured) (challengeOver (Some heavier))

                        (after.Opened.EhrData
                         |> Option.bind (fun e -> e.Patient.Weight.Calculation)
                         |> Option.map (fun w -> CoreWeightValue.getWeightInKg w.Weight |> decimal),
                         after.Opened.Patient |> Option.bind weightInKg,
                         after.Opened.Patient |> Option.map _.WeightMeasured)
                        |> Expect.equal
                            "the EHR data as read says 34 kg; the Session's patient 30 kg, measured"
                            (Some 34m, Some 30N, Some true)
                    }

                    test "the data a notice is told over is merged at the date of the open" {
                        let measured = Measurements.ofRows [ Measurement.Weight(Some 30000<gram>), today ]
                        let told = Session.noticeData today port measured (Some heavier)

                        (told |> Option.bind ageInDays,
                         told |> Option.bind weightInKg,
                         Session.noticeData today port measured None)
                        |> Expect.equal
                            "the age of the open, the measured weight; none for no read"
                            (Some 3841N, Some 30N, None)
                    }
                ]
        ]
