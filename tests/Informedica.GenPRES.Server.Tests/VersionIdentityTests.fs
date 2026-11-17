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
open Informedica.GenPRES.Server.Tests.TestDates

module CoreBirthDate = Informedica.GenCore.Lib.Patients.BirthDate

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests
module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
module CoreWeight = Informedica.GenCore.Lib.Patients.Weight
module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
module CorePatientData = Informedica.GenCore.Lib.Patients.PatientData
module CoreConversions = Informedica.GenCore.Lib.Conversions

type CorePatientData = Informedica.GenCore.Lib.Patients.PatientData
type CoreSource<'v> = Informedica.GenCore.Lib.Patients.Source<'v>


/// The date of the open, and the sign a day later. The EHR data is read at the open, so it has the
/// ages of that day.
let today = DateTime(2026, 9, 26)

let tomorrow = today.AddDays 1.0

let port = StubEhrPatient.port

let ehr = StubEhrPatient.patient today "stub-patient"

let identity = ehr.Identity


/// The stub's EHR data with a weight the EHR has since changed.
let heavier =
    let w = CoreWeightAtDate.create tomorrow (CoreWeightValue.weightInKg 34m)

    { ehr with Data.Weight = CoreWeight.create [ w ] None [] (Some(Informedica.GenCore.Lib.Patients.Measured w)) }


/// The EHR data of an anonymous patient: an age value, no identity.
let unidentified =
    { ehr with
        Identity = None
        Data.Age = Some CoreAgeValue.ten
    }


/// The plan as signed: the projection at the open, the age the Session held.
let plan =
    Informedica.GenPRES.Shared.Models.OrderPlan.create (port.patientData ehr |> Patient.ofPatientData) [||]
    |> OrderPlanCommand.parsePlan readAt
    |> Result.defaultWith (fun e -> failtest $"no plan: %A{e}")


let version = Store.versionOf 1 Store.prescriber tomorrow plan


/// A Session opened at today on the EHR data given, with the head given, seen at today.
let opened (sid: string) (ehr: Informedica.GenCore.Lib.Patients.Patient option) (head: StoredVersion option) =
    sid,
    ({
        Opened =
            {
                User = Some Store.prescriber
                PatientId = Some "stub-patient"
                EhrPatient = ehr
                OpenedToken = Some(Store.token sid)
                KeyThumbprint = Some "t"
                Head = head
                Act = SessionAct.Opened
            }
        Login = Some Store.prescriber.UserId
        OpenedWith = head |> Option.bind StoredVersion.readableId
        OpenedAt = today
        Seen = today
    }
    : Session.SessionRecord)


/// What the Session given shows at a date, through the port whose projection moves with the clock.
let shownAt date (_, record: Session.SessionRecord) = record.Opened |> Session.sessionPatientData date port


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


let challengeOver (read: Informedica.GenCore.Lib.Patients.Patient option) : Session.Challenge =
    {
        Nonce = "c-s-1"
        Digest = StubDatabase.digest plan
        EhrPatient = read
        Expiry = tomorrow + Session.challengeLifetime
    }


/// The commit in a time zone at a moment, over the Session and the challenge given.
let signedIn zone at (record: string * Session.SessionRecord) (challenge: Session.Challenge) =
    let sid, _ = record

    let state = { Store.stateOf [ record ] [] with Challenges = Map.ofList [ sid, challenge ] }

    let state, answer, _ =
        Session.commit
            zone
            at
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


/// The commit at tomorrow, over the Session and the challenge given, through the port whose
/// projection moves with the clock.
let signedAt record challenge = signedIn TimeZoneInfo.Utc tomorrow record challenge


let ageInDays pat =
    pat
    |> CorePatientData.ageInDays
    |> Option.map (ValueUnit.convertTo Units.Time.day >> ValueUnit.getValue >> Array.head)


let weightInKg pat =
    pat
    |> CorePatientData.weightInKg
    |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram >> ValueUnit.getValue >> Array.head)


/// The default time zone, looked up when a test asks for it.
let amsterdam () = LocalDate.defaultZone ()


let utc (y, mo, d, h, mi) = DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc)


let localDateTests =
    testList
        "the age on the local date"
        [
            testList
                "LocalDate.ofUtc"
                [
                    test "a winter moment after local midnight is the next local day" {
                        utc (2026, 11, 7, 23, 30)
                        |> LocalDate.ofUtc (amsterdam ())
                        |> Expect.equal "8 November" (DateTime(2026, 11, 8))
                    }

                    test "a summer moment after local midnight is the next local day" {
                        utc (2026, 7, 7, 22, 30)
                        |> LocalDate.ofUtc (amsterdam ())
                        |> Expect.equal "8 July" (DateTime(2026, 7, 8))
                    }

                    test "a summer moment before local midnight stays on the day" {
                        utc (2026, 7, 7, 21, 59)
                        |> LocalDate.ofUtc (amsterdam ())
                        |> Expect.equal "7 July" (DateTime(2026, 7, 7))
                    }

                    test "the UTC zone gives the UTC date" {
                        utc (2026, 11, 7, 23, 30)
                        |> LocalDate.ofUtc TimeZoneInfo.Utc
                        |> Expect.equal "7 November" (DateTime(2026, 11, 7))
                    }

                    test "a moment without a kind is taken as UTC" {
                        DateTime(2026, 11, 7, 23, 30, 0)
                        |> LocalDate.ofUtc (amsterdam ())
                        |> Expect.equal "8 November" (DateTime(2026, 11, 8))
                    }
                ]

            testList
                "18 years just after local midnight"
                [
                    // born 12 November 2008, 6570 days before 8 November 2026; at 00:30 Dutch time on
                    // 8 November 2026 the patient is 18 years
                    let whom: Informedica.GenCore.Lib.Patients.PatientIdentity option =
                        Some
                            {
                                Id = "p-18"
                                Name = "Eighteen"
                                BirthDate = DateTime(2008, 11, 12) |> CoreBirthDate.fromDate
                            }

                    let moment = utc (2026, 11, 7, 23, 30)
                    let pat = CorePatientData.unknown

                    test "on the local date the patient is 18 years, 6570 days" {
                        pat
                        |> Session.atBirthDate (moment |> LocalDate.ofUtc (amsterdam ())) whom
                        |> ageInDays
                        |> Expect.equal "18 x 365" (Some 6570N)
                    }

                    test "on the UTC date the patient is still 17" {
                        pat
                        |> Session.atBirthDate (moment |> LocalDate.ofUtc TimeZoneInfo.Utc) whom
                        |> ageInDays
                        |> Option.map (fun d -> d < 6570N)
                        |> Expect.equal "under 18" (Some true)
                    }
                ]

            testList
                "commit and challenge just after local midnight"
                [
                    // 23:30 UTC on 26 September is 01:30 on 27 September in Amsterdam (CEST)
                    let lateEvening = today.AddHours 23.5

                    test "a sign at 23:30 UTC answers the patient a day older in Amsterdam than in UTC" {
                        let inZone zone =
                            let _, _, _, _, patient =
                                signedIn zone lateEvening (opened "s-1" (Some ehr) None) (challengeOver (Some ehr))

                            patient |> ageInDays

                        (inZone (amsterdam ()), inZone TimeZoneInfo.Utc)
                        |> Expect.equal "3848 in Amsterdam, 3847 in UTC" (Some 3848N, Some 3847N)
                    }

                    test "a data notice shows the ages of the challenge's read" {
                        // the EHR read at the challenge, on the day the stub's clock gives
                        let reading = StubEhrPatient.portAt (fun () -> today)
                        let sid, record = opened "s-1" (Some heavier) None
                        let record =
                            { record with
                                OpenedAt = lateEvening
                                Seen = lateEvening
                            }
                        let state = Store.stateOf [ sid, record ] []

                        let _, outcome, _ =
                            Session.challenge
                                lateEvening
                                (Store.counter "id")
                                StubDatabase.digest
                                reading
                                Store.registry
                                sid
                                (plan, Store.token sid, None)
                                state

                        match outcome with
                        | SigningOutcome.DataNotice(_, data) -> data |> Option.bind ageInDays
                        | other -> failtest $"expected DataNotice, got %A{other}"
                        |> Expect.equal
                            "the read's age"
                            (reading.patientData (StubEhrPatient.patient today "stub-patient") |> ageInDays)
                    }
                ]
        ]


[<Tests>]
let tests =
    testList
        "the version's identity and the age after the sign"
        [
            localDateTests

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
                                Some StubEhrPatient.name
                                Some StubEhrPatient.name
                                Some StubEhrPatient.name
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
                    let days n = n |> CoreConversions.dayFromInt |> CoreAgeWeeksDays.fromDays
                    let openedLater head = opened "s-1" None head |> shownAt later

                    let withoutAges (pat: CorePatientData) =
                        { pat with
                            Age = None
                            PostMenstrualAge = None
                        }

                    let premature =
                        { version with
                            Plan =
                                { version.Plan with
                                    PatientData =
                                        { version.Plan.PatientData with
                                            GestationalAge = Some(days 210)
                                            PostMenstrualAge = Some(days 1)
                                        }
                                }
                        }

                    test "has the age of the date asked for, not of the sign" {
                        let age = openedLater (Some readable) |> Option.bind _.Age

                        age
                        |> Expect.equal
                            "the age an EHR read at that date gives"
                            (port.patientData (StubEhrPatient.patient later "stub-patient")).Age

                        age
                        |> Expect.notEqual "another age than the one signed" version.Plan.PatientData.Age
                    }

                    test "is the head's in every other field" {
                        openedLater (Some readable)
                        |> Option.map withoutAges
                        |> Expect.equal "the signed data" (Some(version.Plan.PatientData |> withoutAges))
                    }

                    test "has no post-menstrual age from 1 year, also with a gestational age" {
                        openedLater (Some(StoredVersion.Readable(premature, identity)))
                        |> Option.bind _.PostMenstrualAge
                        |> Expect.isNone "the gestational age is ignored from 1 year"
                    }

                    test "has no post-menstrual age without a gestational age" {
                        openedLater (Some readable)
                        |> Option.bind _.PostMenstrualAge
                        |> Expect.isNone "none"
                    }

                    test "stays as signed without an identity" {
                        openedLater (Some(StoredVersion.Readable(version, None)))
                        |> Expect.equal "as signed" (Some version.Plan.PatientData)
                    }

                    test "gives way to EHR data, whatever the head" {
                        opened "s-1" (Some ehr) (Some readable)
                        |> shownAt later
                        |> Expect.equal "the projection of the read" (Some(port.patientData ehr))
                    }

                    test "is none from nothing, and from a head that cannot be read" {
                        [ openedLater None; openedLater (Some(StoredVersion.Unreadable unreadable)) ]
                        |> Expect.allEqual "none" None
                    }
                ]

            testList
                "the latest act says which patient a Session shows, the age at the resume"
                [
                    let resume = today.AddMonths 2
                    let readable = Some(StoredVersion.Readable(version, identity))
                    let shownEhr = Some(port.patientData heavier)
                    let shownSigned = Some(version.Plan.PatientData |> Session.atBirthDate resume identity)

                    let afterAct act head =
                        let sid, record = opened "s-1" (Some heavier) head

                        (sid, { record with Opened = { record.Opened with Act = act } })
                        |> shownAt resume

                    test "the fixtures tell the EHR's patient from the signed one" {
                        shownEhr |> Expect.notEqual "another weight" shownSigned
                    }

                    test "a reload before any sign shows the EHR data" {
                        afterAct SessionAct.Opened None |> Expect.equal "the projection" shownEhr
                    }

                    test "a reload after a sign shows the patient signed" {
                        afterAct SessionAct.Signed readable
                        |> Expect.equal "the signed patient" shownSigned
                    }

                    test "signed, then refreshed, shows the EHR data" {
                        afterAct SessionAct.Refreshed readable |> Expect.equal "the projection" shownEhr
                    }

                    test "signed, then a version opened, shows the EHR data" {
                        afterAct SessionAct.VersionOpened readable
                        |> Expect.equal "the projection" shownEhr
                    }

                    test "a sign whose head cannot be read shows the EHR data" {
                        afterAct SessionAct.Signed (Some(StoredVersion.Unreadable unreadable))
                        |> Expect.equal "the projection" shownEhr
                    }
                ]

            testList
                "the record of a sign"
                [
                    let thirtyKg source =
                        CoreWeightValue.weightInKg 30m
                        |> CoreWeightAtDate.create today
                        |> source
                        |> Some
                        |> CoreWeight.create [] None []

                    let typed =
                        { version with
                            Plan =
                                { version.Plan with
                                    PatientData =
                                        { version.Plan.PatientData with Weight = thirtyKg CoreSource.Measured }
                                }
                        }

                    let signed = typed.Plan.PatientData

                    test "the typed weight stands after the sign, whatever the EHR reads" {
                        signed
                        |> Session.atBirthDate tomorrow identity
                        |> weightInKg
                        |> Expect.equal "30 kg" (Some 30N)
                    }

                    test "an estimate signed goes once the age has moved on, a measured value stays" {
                        let estimated =
                            { typed with
                                Plan =
                                    { typed.Plan with
                                        PatientData = { signed with Weight = thirtyKg CoreSource.Estimated }
                                    }
                            }

                        let _, record = opened "s-1" (Some ehr) (Some(StoredVersion.Readable(estimated, identity)))
                        let session = { record.Opened with Act = SessionAct.Signed }
                        let shown date = session |> Session.sessionPatientData date port

                        shown today
                        |> Option.bind weightInKg
                        |> Expect.equal "the age signed, the estimate stands" (Some 30N)

                        shown tomorrow
                        |> Option.bind weightInKg
                        |> Expect.isNone "a new age, no old estimate"

                        (shown tomorrow |> Option.bind CorePatientData.heightInCm, signed |> CorePatientData.heightInCm)
                        |> fun (shownHeight, signedHeight) ->
                            shownHeight |> Expect.equal "the measured height stays" signedHeight
                    }

                    test "the patient after the sign is at the age of the sign, the version keeps its own" {
                        (signed |> Session.atBirthDate tomorrow identity |> ageInDays, signed |> ageInDays)
                        |> Expect.equal "a day more" (Some 3848N, Some 3847N)
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
                            (identity,
                             identity,
                             Some version.Id,
                             Some token,
                             record.Opened |> Session.sessionPatientData tomorrow port)
                    }

                    test "the version names the patient of the read signed on: a notice accepted over another name" {
                        let renamed =
                            { ehr with
                                Identity = ehr.Identity |> Option.map (fun idn -> { idn with Name = "Renamed" })
                            }

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

                        (record.Opened
                         |> Session.sessionPatientData tomorrow port
                         |> Option.bind ageInDays,
                         version.Plan.PatientData |> ageInDays)
                        |> Expect.equal "3848 on the Session, 3847 on the version" (Some 3848N, Some 3847N)
                    }

                    test "the version keeps the EHR data of its challenge, and the Session the patient signed" {
                        let sid, record = opened "s-1" (Some ehr) None
                        let after, version, _, _, patient = signedAt (sid, record) (challengeOver (Some heavier))

                        (version.EhrPatient,
                         patient |> weightInKg,
                         after.Opened
                         |> Session.sessionPatientData tomorrow port
                         |> Option.bind weightInKg)
                        |> Expect.equal "the 34 kg read, the 32 kg signed" (Some heavier, Some 32N, Some 32N)
                    }
                ]
        ]
