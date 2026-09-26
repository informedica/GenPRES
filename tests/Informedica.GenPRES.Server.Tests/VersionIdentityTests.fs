/// The version's identity: whom a Session is for, and what the version it signs names.
module Informedica.GenPRES.Server.Tests.VersionIdentityTests

open System
open Expecto
open Expecto.Flip
open Shared.Types
open ServerApi

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests
module GenFormPatient = Informedica.GenForm.Lib.Patient
module EhrPatientData = Informedica.GenForm.Lib.EhrPatientData
module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
module CorePatientAge = Informedica.GenCore.Lib.Patients.PatientAge


/// The date of the open, and the sign a day later. The port here projects at the date given,
/// so that the age moves with the clock.
let today = DateTime(2026, 9, 26)

let tomorrow = today.AddDays 1.0

let port = StubPatientData.port

let ehr = StubPatientData.data "stub-patient"

let identity = ehr |> EhrPatientData.identity


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
            Store.registry
            ignore
            sid
            (Store.signature sid "1234" "k-1" plan)
            state

    match answer with
    | SigningOutcome.Submitted(version, whom, token, patient) -> state.Sessions[sid], version, whom, token, patient
    | other -> failtest $"expected Submitted, got %A{other}"


[<Tests>]
let tests =
    testList
        "the version's identity"
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

                    test "none in a Session without an identity" {
                        let _, _, whom, _, _ =
                            signedAt (opened "s-1" (Some unidentified) None) (challengeOver (Some unidentified))

                        whom |> Expect.isNone "no identity to name"
                    }
                ]
        ]
