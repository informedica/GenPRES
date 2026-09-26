/// The version's identity, and the age after the sign. A version signed in an identified
/// Session names its patient on its own: the record's version carries the name and the
/// birthdate in both its cases, readable and unreadable, beside the plan whose JSON is
/// unchanged; a Session opened from the head while the EHR answers none takes the head's
/// identity and is identified. Once a signature has landed, the Session's patient is the
/// merge: the identity from the EHR data, the age computed again at the clock, the user's
/// measurements on the core patient, projected and estimated; the version keeps the age it
/// was signed at. The data a notice is told over is the same merge at the date of the open.
/// The client receives the Session's patient with the fresh token.
///
/// - Ports.fs `StoredVersion.Readable` carries the identity beside the version;
///   `UnreadableVersion.Identity`; `StoredVersion.identity`. `SigningOutcome.Submitted`
///   carries the Session's patient beside the version and the token; `Persist.WriteVersion`
///   the identity beside the version.
/// - Shared/Types.fs `SignedOrderPlan.Identity: NameAndBirthDate option`, and
///   `SigningResponse.Submitted of SignedOrderPlan * OpenedToken * Patient`, both carried by the
///   session mapper.
/// - Session.fs `Opened.identity`: the EHR data's, else the head's when the EHR answered none;
///   `age` reads it; `merged`; `landed` is what `commit` does once the version has landed,
///   with the port it now takes; `challenge` tells the notice over `noticeData`.
/// - The store: a migration adds nullable patient_name, birth_year, birth_month, birth_day to
///   order_plan, all or none; a row from before reads as none; the loader fills both cases.
///
/// Run: cd src/Informedica.GenPRES.Server/Scripts && dotnet fsi VersionIdentity.fsx

#I __SOURCE_DIRECTORY__

#load "load.fsx"

#r "nuget: Expecto, 10.2.3"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

open System
open Expecto
open Expecto.Flip
// the contract before the domain, so that its measures and patient win unqualified
open Shared.Types
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open ServerApi

module GenOrder = Informedica.GenOrder.Lib.Types
module GenForm = Informedica.GenForm.Lib.Types
module EhrPatientData = Informedica.GenForm.Lib.EhrPatientData
module CoreConversions = Informedica.GenCore.Lib.Conversions
module CoreWeightValue = Informedica.GenCore.Lib.Patients.WeightValue
module CoreWeightAtDate = Informedica.GenCore.Lib.Patients.WeightAtDate
module CoreWeight = Informedica.GenCore.Lib.Patients.Weight
module CoreHeightValue = Informedica.GenCore.Lib.Patients.HeightValue
module CoreHeightAtDate = Informedica.GenCore.Lib.Patients.HeightAtDate
module CoreAgeWeeksDays = Informedica.GenCore.Lib.Patients.AgeWeeksDays
module CoreAgeValue = Informedica.GenCore.Lib.Patients.AgeValue
module CorePatientAge = Informedica.GenCore.Lib.Patients.PatientAge

type PatientIdentity = Informedica.GenCore.Lib.Patients.PatientIdentity


// ── Mappers.Measurements.fs, the core half (from Measurement.fsx) ─────────────────────────


module Measurements =

    open ServerApi.Measurements

    /// The core patient with the measurements on it: a value as a dated actual and the
    /// calculation value, so that the projection reads it as measured; a cleared one leaves
    /// the actuals as read and no calculation value, so that the projection has none and the
    /// estimate stands; one never touched leaves the patient as read.
    let onCore
        (held: Measurements)
        (core: Informedica.GenCore.Lib.Patients.Patient)
        : Informedica.GenCore.Lib.Patients.Patient
        =
        let weight =
            match held.Weight with
            | None -> core.Weight
            | Some { Value = None } -> { core.Weight with Calculation = None }
            | Some { Value = Some g; At = at } ->
                let w = CoreWeightAtDate.create at (CoreWeightValue.weightInGram (int g))

                { core.Weight with
                    Actual = core.Weight.Actual @ [ w ]
                    Calculation = Some w
                }

        let height =
            match held.Height with
            | None -> core.Height
            | Some { Value = None } -> { core.Height with Calculation = None }
            | Some { Value = Some cm; At = at } ->
                let h = CoreHeightAtDate.create at (CoreHeightValue.heightInCm (decimal cm))

                { core.Height with
                    Actual = core.Height.Actual @ [ h ]
                    Calculation = Some h
                }

        let gestAge =
            match held.GestAge with
            | None -> core.GestationalAge
            | Some { Value = None } -> None
            | Some { Value = Some ga } ->
                CoreAgeWeeksDays.create
                    (CoreConversions.weekFromInt (int ga.Weeks))
                    (CoreConversions.dayFromInt (int ga.Days))
                |> Some

        { core with
            Weight = weight
            Height = height
            GestationalAge = gestAge
        }


// ── Ports.fs, the version with its identity ───────────────────────────────────────────────


/// A row the release cannot read: its identity from the plain columns beside the JSON, which
/// are authoritative, the patient's among them, and why.
type UnreadableVersion =
    {
        Id: string
        No: int
        PatientId: string
        Base: string option
        SignedBy: GenOrder.Signer
        SignedAt: DateTime
        /// the patient's name and birthdate, from the columns; none for a version signed in
        /// a Session without an identity, and for a row from before the columns
        Identity: PatientIdentity option
        Reason: string
    }


/// A version as the record holds it once loaded, with the identity of the patient it was
/// signed for beside it in both cases, from the columns and never from the plan's JSON.
[<RequireQualifiedAccess>]
type StoredVersion =
    | Readable of GenOrder.OrderPlanVersion * identity: PatientIdentity option
    | Unreadable of UnreadableVersion


module StoredVersion =

    /// Whom the version names: the patient's name and birthdate, in both cases.
    let identity =
        function
        | StoredVersion.Readable(_, identity) -> identity
        | StoredVersion.Unreadable u -> u.Identity


/// What the store holds of an open Session, the head with its identity.
type OpenedSession =
    {
        User: UserContext option
        PatientId: string option
        EhrData: GenForm.EhrPatientData option
        Patient: GenForm.Patient option
        Measured: Measurements
        OpenedToken: OpenedToken option
        KeyThumbprint: string option
        Head: StoredVersion option
    }


module Opened =

    /// Whom the Session is for: the identity in the EHR data it opened on, or the head's
    /// when the EHR answered none, so that a Session opened from the head is identified.
    /// EHR data without an identity is anonymous, whatever the head says.
    let identity (opened: OpenedSession) : PatientIdentity option =
        match opened.EhrData with
        | Some ehr -> ehr |> EhrPatientData.identity
        | None -> opened.Head |> Option.bind StoredVersion.identity


// ── Session.fs ────────────────────────────────────────────────────────────────────────────


type SessionRecord =
    {
        Opened: OpenedSession
        OpenedWith: string option
        OpenedAt: DateTime
    }


module Session =

    /// The Session's patient from EHR data, at a date: the user's measurements on the core
    /// patient, projected there and estimated, the age the birthdate gives at that date.
    let merged (at: DateTime) (port: PatientDataPort) (measured: Measurements) (ehr: GenForm.EhrPatientData) =
        { ehr with Patient = Measurements.onCore measured ehr.Patient } |> port.patient at


    /// The data a notice is told over: the fresh read merged at the date of the open, so that
    /// the notice never moves the age and keeps what the user measured; none for no read.
    let noticeData (openedAt: DateTime) (port: PatientDataPort) (measured: Measurements) (current: GenForm.EhrPatientData option) =
        current |> Option.map (merged openedAt port measured)


    /// The age a Session holds, for one with an identity: the age of the patient it shows.
    let age (record: SessionRecord) : Age option =
        record.Opened
        |> Opened.identity
        |> Option.bind (fun _ -> record.Opened.Patient)
        |> Option.bind (Informedica.GenForm.Lib.Patient.Dto.toDto >> Patient.toModel >> _.Age)


    /// The Session once a signature has landed: opened on the version, which carries the
    /// Session's identity; the EHR data the read just signed on; the patient the merge at the
    /// clock, the age computed again, else the plan just signed when there is no EHR data; the
    /// token re-minted. The version keeps the age it was signed at.
    let landed
        (now: DateTime)
        (port: PatientDataPort)
        (token: OpenedToken)
        (challenge: ServerApi.Session.Challenge)
        (version: GenOrder.OrderPlanVersion)
        (record: SessionRecord)
        : SessionRecord
        =
        let ehr = ServerApi.Session.Reads.afterCommit challenge record.Opened.EhrData

        { record with
            Opened =
                { record.Opened with
                    OpenedToken = Some token
                    Head = Some(StoredVersion.Readable(version, Opened.identity record.Opened))
                    EhrData = ehr
                    Patient =
                        ehr
                        |> Option.map (merged now port record.Opened.Measured)
                        |> Option.defaultValue version.Plan.Patient
                        |> Some
                }
            OpenedWith = Some version.Id
        }


// ── The proof ─────────────────────────────────────────────────────────────────────────────


/// The date of the open, and the sign a day later.
let today = DateTime(2026, 9, 26)

let tomorrow = today.AddDays 1.0


let port = StubPatientData.port

let ehr = StubPatientData.data "p1"


/// The stub's EHR data with a weight the EHR has since changed.
let heavier =
    let w = CoreWeightAtDate.create tomorrow (CoreWeightValue.weightInKg 34m)

    { ehr with
        Patient =
            { ehr.Patient with
                Weight = CoreWeight.create [ w ] None [] (Some w)
            }
    }


/// EHR data with an age value in place of a birthdate: no identity.
let unidentified =
    { ehr with
        Patient =
            { ehr.Patient with
                Age = CoreAgeValue.ten |> CorePatientAge.ageValue
            }
    }


let identity = ehr |> EhrPatientData.identity


let prescriber: UserContext =
    {
        UserId = "prescriber"
        DisplayName = "Dr. Stub"
        Role = UserRole.Prescriber
    }


/// The plan as signed: the projection at the open, the age the Session held.
let plan =
    Shared.Models.OrderPlan.create
        (port.patient today ehr |> Informedica.GenForm.Lib.Patient.Dto.toDto |> Patient.toModel)
        [||]
    |> OrderPlanCommand.parsePlan
    |> Result.defaultWith (fun e -> failwith $"no plan: %A{e}")


let version: GenOrder.OrderPlanVersion =
    {
        Id = "v-1"
        No = 1
        PatientId = "p1"
        Base = None
        SignedBy =
            {
                UserId = prescriber.UserId
                DisplayName = prescriber.DisplayName
            }
        SignedAt = tomorrow
        Plan = plan
        Verified = true
    }


let challengeOver (read: GenForm.EhrPatientData option) : ServerApi.Session.Challenge =
    {
        Nonce = "c-1"
        Digest = "digest"
        Ehr = read
        Reading = read |> Option.map (port.patient today)
        Expiry = tomorrow.AddMinutes 2.0
    }


/// A Session opened at today on the EHR data given, with the head given.
let opened (ehr: GenForm.EhrPatientData option) (head: StoredVersion option) : SessionRecord =
    {
        Opened =
            {
                User = Some prescriber
                PatientId = Some "p1"
                EhrData = ehr
                Patient =
                    ehr
                    |> Option.map (port.patient today)
                    |> Option.orElse (
                        head
                        |> Option.bind (
                            function
                            | StoredVersion.Readable(v, _) -> Some v.Plan.Patient
                            | StoredVersion.Unreadable _ -> None
                        )
                    )
                Measured = Measurements.none
                OpenedToken = Some(OpenedToken "opened-1")
                KeyThumbprint = Some "t"
                Head = head
            }
        OpenedWith = head |> Option.map (fun _ -> "v-1")
        OpenedAt = today
    }


let unreadable: UnreadableVersion =
    {
        Id = "v-0"
        No = 1
        PatientId = "p1"
        Base = None
        SignedBy = version.SignedBy
        SignedAt = today
        Identity = identity
        Reason = "a structure version this release does not know"
    }


let ageInDays (pat: GenForm.Patient) =
    pat.Age
    |> Option.map (ValueUnit.convertTo Units.Time.day >> ValueUnit.getValue >> Array.head)


let weightInKg (pat: GenForm.Patient) =
    pat.Weight
    |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram >> ValueUnit.getValue >> Array.head)


let tests =
    testList
        "the version's identity and the age after the sign"
        [
            testList
                "whom a Session is for"
                [
                    test "the EHR data's identity; the head's when the EHR answered none; none from nothing" {
                        [
                            opened (Some ehr) None
                            opened None (Some(StoredVersion.Readable(version, identity)))
                            opened None (Some(StoredVersion.Unreadable unreadable))
                            opened None None
                            opened None (Some(StoredVersion.Readable(version, None)))
                        ]
                        |> List.map (fun r -> r.Opened |> Opened.identity |> Option.map _.Name)
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
                        opened (Some unidentified) (Some(StoredVersion.Readable(version, identity)))
                        |> fun r -> r.Opened |> Opened.identity
                        |> Expect.isNone "the EHR data decides"
                    }

                    test "a Session opened from the head is identified and holds the head's age" {
                        opened None (Some(StoredVersion.Readable(version, identity)))
                        |> Session.age
                        |> Expect.equal "the age the version was signed at" (Some(Shared.Models.Patient.Age.fromDays 3841))
                    }
                ]

            testList
                "once a signature has landed"
                [
                    test "the version carries the Session's identity; none in a Session without one" {
                        let signedIn (ehr: GenForm.EhrPatientData) =
                            opened (Some ehr) None
                            |> Session.landed tomorrow port (OpenedToken "opened-2") (challengeOver (Some ehr)) version
                            |> fun r -> r.Opened.Head |> Option.bind StoredVersion.identity

                        (signedIn ehr, signedIn unidentified)
                        |> Expect.equal "the stub's identity; none" (identity, None)
                    }

                    test "with the clock a day later, the Session's age is one day more and the version's is not" {
                        let after =
                            opened (Some ehr) None
                            |> Session.landed tomorrow port (OpenedToken "opened-2") (challengeOver (Some ehr)) version

                        (after.Opened.Patient |> Option.bind ageInDays,
                         after.Opened.Head
                         |> Option.bind (
                             function
                             | StoredVersion.Readable(v, _) -> v.Plan.Patient |> ageInDays
                             | StoredVersion.Unreadable _ -> None
                         ),
                         after.Opened.OpenedToken,
                         after.OpenedWith)
                        |> Expect.equal
                            "3842 on the Session, 3841 on the version; the fresh token; opened with the version"
                            (Some 3842N, Some 3841N, Some(OpenedToken "opened-2"), Some "v-1")
                    }

                    test "where the EHR reports another weight, the Session keeps the weight the user measured" {
                        let measured = Measurements.ofRows [ Measurement.Weight(Some 30000<gram>), today ]

                        let record = opened (Some ehr) None

                        let after =
                            { record with Opened = { record.Opened with Measured = measured } }
                            |> Session.landed tomorrow port (OpenedToken "opened-2") (challengeOver (Some heavier)) version

                        (after.Opened.EhrData |> Option.bind (fun e -> e.Patient.Weight.Calculation) |> Option.map (fun w -> CoreWeightValue.getWeightInKg w.Weight |> decimal),
                         after.Opened.Patient |> Option.bind weightInKg,
                         after.Opened.Patient |> Option.map _.WeightMeasured)
                        |> Expect.equal
                            "the EHR data as read says 34 kg; the Session's patient 30 kg, measured"
                            (Some 34m, Some 30N, Some true)
                    }

                    test "without EHR data the Session's patient is the plan just signed" {
                        opened None None
                        |> Session.landed tomorrow port (OpenedToken "opened-2") (challengeOver None) version
                        |> fun r -> r.Opened.Patient
                        |> Expect.equal "the plan's patient" (Some version.Plan.Patient)
                    }

                    test "the data a notice is told over is merged at the date of the open" {
                        let measured = Measurements.ofRows [ Measurement.Weight(Some 30000<gram>), today ]

                        let told = Session.noticeData today port measured (Some heavier)

                        (told |> Option.bind ageInDays, told |> Option.bind weightInKg, Session.noticeData today port measured None)
                        |> Expect.equal "the age of the open, the measured weight; none for no read" (Some 3841N, Some 30N, None)
                    }
                ]
        ]


runTestsWithCLIArgs [] [||] tests
