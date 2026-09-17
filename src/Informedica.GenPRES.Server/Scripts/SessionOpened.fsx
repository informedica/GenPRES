// Step 5.2 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #773):
// the session record on domain values.
//
//   - What the store holds of an open Session is `OpenedSession`: the user, the patient id and
//     the patient data shown, as the domain's patient, the token, the key thumbprint and the
//     head as a `StoredVersion`, readable or not. `SessionMapper.toOpened` makes what the
//     client keeps of it; the launch, session and signing handlers map with the demo flag,
//     so the service and the port never see the contract model's session.
//   - The patient data port answers the domain's patient: the stub adapter parses its own
//     reading, and a reading that is no patient is no reading, at the adapter.
//   - A notice's data and a challenge's reading are the domain's patient too, so nothing in
//     the session state is the contract model's but the identity types (ADR-0008 R6).
//   - `SessionEnding.Unreadable` joins the contract model with its gate sentence, the ending
//     the store's adapter appends when a Session's working state cannot be read; the stub
//     never appends it.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. Shared/Types.fs: `SessionEnding.Unreadable`; Localization and the client's gate.
//   2. Ports.fs: `UnreadableVersion` and `StoredVersion` move here from Session.fs;
//      `OpenedSession`; `LaunchResult.Opened`, `SupplyPinResult.Opened`, `SessionLookup.Found`
//      and `SessionPort.openVersion` on it; `PatientDataPort.read` and
//      `SigningOutcome.DataNotice` on the domain's patient.
//   3. Mappers.Session.fs: `toOpened` replaces `opened`.
//   4. Session.fs: `SessionRecord.Opened`, `Notice.Data` and `Challenge.Reading` on the
//      domain's patient; `sessionPatient` as below; `toSigned` gone from `openWith`,
//      `callback`, `supplyPin`, `openVersion` and `commit`.
//   5. StubAdapters.fs: the stub patient port parses its reading; `makeSessionPort` loses
//      `demo`. Adapters.fs follows.
//   6. LaunchCommand.fs, SessionCommand.fs, SigningCommand.fs: `toOpened env.demo` and the
//      notice's data mapped out.
//   7. Tests: the 5.0 builders (`sessionOf`, `noticeOf`, `challengeOf`) on the new shapes;
//      the assertions over `record.Session` become `record.Opened`; the new tests below.
//
// Run from this directory: dotnet fsi SessionOpened.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Shared.Types
open ServerApi

module GenOrder = Informedica.GenOrder.Lib.Types
module GenForm = Informedica.GenForm.Lib.Types
module GenFormPatient = Informedica.GenForm.Lib.Patient


// ---------------------------------------------------------------------------
// 1. Ports.fs: what the store holds of an open Session, and the patient data port
// ---------------------------------------------------------------------------

/// What the store holds of an open Session: who, for which patient and on what data, the
/// token, the key thumbprint, and the head of the record it opened with, readable or not.
type OpenedSession =
    {
        // None = anonymous session: opened without a launch, no User, no Role
        User: UserContext option
        // None = launch without an active patient
        PatientId: string option
        // the data shown for the patient: the platform's reading, else the head's, else none
        Patient: GenForm.Patient option
        OpenedToken: OpenedToken option
        KeyThumbprint: string option
        // the head of the record it opened with; None from nothing
        Head: Session.StoredVersion option
    }


/// The PatientDataPlatform, read once at the launch and again at a challenge, as the domain's
/// patient: the adapter parses what the platform gives, and a reading that is no patient is
/// no reading. `None` is not a refusal: the Session opens without imported data.
type DomainPatientDataPort = { read: string -> GenForm.Patient option }


// ---------------------------------------------------------------------------
// 2. Mappers.Session.fs: what the client keeps of an open Session
// ---------------------------------------------------------------------------

module SessionMapper =

    /// What the client keeps of an open Session, from what the store holds: the head as the
    /// signed order plan on the wire when it can be read, none when it cannot, the patient
    /// data as the contract model; the demo flag on every context.
    let toOpened (demo: bool) (opened: OpenedSession) : SessionOpened =
        {
            User = opened.User
            PatientContext =
                opened.PatientId
                |> Option.map (fun id ->
                    {
                        PatientId = id
                        Patient = opened.Patient |> Option.map (GenFormPatient.Dto.toDto >> Patient.toModel)
                    }
                )
            OpenedToken = opened.OpenedToken
            KeyThumbprint = opened.KeyThumbprint
            Head =
                opened.Head
                |> Option.bind (fun head ->
                    match head with
                    | Session.StoredVersion.Readable v -> v |> OrderPlanVersion.Dto.toDto |> SessionMapper.toSigned demo |> Some
                    | Session.StoredVersion.Unreadable _ -> None
                )
        }


// ---------------------------------------------------------------------------
// 3. Session.fs: the patient a Session opens on, over the domain's patient
// ---------------------------------------------------------------------------

module Session =

    /// The patient data a Session opens on: the PatientDataPlatform's reading, the source of
    /// truth, when there is one; without one, the patient data the head of the record was
    /// signed on, the last seen, when the head can be read; from nothing, none, so that the
    /// User enters it and a data outage does not block prescribing.
    let sessionPatient
        (patientData: string -> GenForm.Patient option)
        (patientId: string)
        (head: Session.StoredVersion option)
        : GenForm.Patient option
        =
        patientData patientId
        |> Option.orElse (
            head
            |> Option.bind (fun h ->
                match h with
                | Session.StoredVersion.Readable v -> Some v.Plan.Patient
                | Session.StoredVersion.Unreadable _ -> None
            )
        )


    /// The open, as `openWith` writes it: the head held whatever its case, opened with only a
    /// readable one, so that an unreadable head is told as the record having moved on.
    let openedOn
        (patientData: string -> GenForm.Patient option)
        (user: UserContext)
        (patientId: string)
        (token: OpenedToken)
        (thumbprint: string)
        (head: Session.StoredVersion option)
        : OpenedSession * string option
        =
        {
            User = Some user
            PatientId = Some patientId
            Patient = sessionPatient patientData patientId head
            OpenedToken = Some token
            KeyThumbprint = Some thumbprint
            Head = head
        },
        head
        |> Option.bind (fun h ->
            match h with
            | Session.StoredVersion.Readable v -> Some v.Id
            | Session.StoredVersion.Unreadable _ -> None
        )


// ---------------------------------------------------------------------------
// 4. StubAdapters.fs: the stub patient port parses its reading
// ---------------------------------------------------------------------------

module StubPatientData =

    /// The stub's reading parsed: what the platform gives is a patient, or it is no reading.
    let port: DomainPatientDataPort =
        {
            read =
                fun pid ->
                    if pid = "no-data" then
                        None
                    else
                        StubPatientData.patient |> Patient.parse |> Result.toOption
        }


    /// A platform whose reading is not a patient: no reading.
    let unreadable: DomainPatientDataPort =
        { read = fun _ -> Shared.Models.Patient.empty |> Patient.parse |> Result.toOption }


// ---------------------------------------------------------------------------
// 5. Tests
// ---------------------------------------------------------------------------

module SessionOpenedTests =

    open Expecto
    open Expecto.Flip
    // after Expecto, whose FocusState has a Normal case too
    open Shared.Types

    let t0 = DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc)

    let prescriber: UserContext =
        {
            UserId = "prescriber"
            DisplayName = "Stub Prescriber"
            Role = UserRole.Prescriber
        }

    let stub = StubPatientData.patient

    let domainStub =
        stub |> Patient.parse |> Result.defaultWith (fun e -> failtest $"%A{e}")

    let plan: GenOrder.OrderPlan =
        Shared.Models.OrderPlan.create stub [||]
        |> OrderPlanCommand.parsePlan
        |> Result.defaultWith (fun e -> failtest $"%A{e}")

    let version: GenOrder.OrderPlanVersion =
        {
            Id = "plan-1"
            No = 1
            PatientId = "stub-patient"
            Base = None
            SignedBy =
                {
                    UserId = "prescriber"
                    DisplayName = "Stub Prescriber"
                }
            SignedAt = t0
            Plan = plan
            Verified = true
        }

    let unreadable: Session.StoredVersion =
        Session.StoredVersion.Unreadable
            {
                Id = "plan-2"
                No = 2
                PatientId = "stub-patient"
                Base = Some "plan-1"
                SignedBy =
                    {
                        UserId = "prescriber-b"
                        DisplayName = "Stub Prescriber B"
                    }
                SignedAt = t0
                Reason = "json_version 9 is newer than this release knows"
            }

    let opened head : OpenedSession =
        {
            User = Some prescriber
            PatientId = Some "stub-patient"
            Patient = Some domainStub
            OpenedToken = Some(OpenedToken "opened-s-1")
            KeyThumbprint = Some "t"
            Head = head
        }


    let tests =
        testList
            "the session record on domain values"
            [
                test "what the client keeps: the head as the signed order plan when readable, none when not; the patient as the contract model; the demo flag the server's" {
                    let readable = SessionMapper.toOpened true (opened (Some(Session.StoredVersion.Readable version)))

                    readable.User |> Expect.equal "user" (Some prescriber)

                    readable.PatientContext
                    |> Expect.equal
                        "the patient, as the contract model"
                        (Some
                            {
                                PatientId = "stub-patient"
                                Patient = Some stub
                            })

                    readable.OpenedToken |> Expect.equal "token" (Some(OpenedToken "opened-s-1"))
                    readable.KeyThumbprint |> Expect.equal "thumbprint" (Some "t")

                    readable.Head
                    |> Expect.equal
                        "the head on the wire"
                        (Some(version |> OrderPlanVersion.Dto.toDto |> SessionMapper.toSigned true))

                    (SessionMapper.toOpened true (opened (Some unreadable))).Head
                    |> Expect.isNone "an unreadable head cannot be shown"

                    (SessionMapper.toOpened true (opened None)).Head |> Expect.isNone "from nothing"

                    // an anonymous Session without a patient maps to none of both
                    SessionMapper.toOpened
                        false
                        { opened None with
                            User = None
                            PatientId = None
                            Patient = None
                        }
                    |> Expect.equal
                        "anonymous"
                        {
                            User = None
                            PatientContext = None
                            OpenedToken = Some(OpenedToken "opened-s-1")
                            KeyThumbprint = Some "t"
                            Head = None
                        }
                }

                test "the stub patient port answers the domain's patient, none for the patient without data and none for a reading that is no patient" {
                    StubPatientData.port.read "stub-patient" |> Expect.equal "parsed" (Some domainStub)
                    StubPatientData.port.read "no-data" |> Expect.isNone "no record"
                    StubPatientData.unreadable.read "stub-patient" |> Expect.isNone "no reading"
                }

                test "the patient a Session opens on: the reading, else the readable head's, else none" {
                    let reading = StubPatientData.port.read
                    let none _ = None

                    Session.sessionPatient reading "stub-patient" (Some(Session.StoredVersion.Readable version))
                    |> Expect.equal "the reading wins" (Some domainStub)

                    let signedOn =
                        { version with
                            Plan = { plan with Patient = { domainStub with Department = Some "ICU" } }
                        }

                    Session.sessionPatient none "stub-patient" (Some(Session.StoredVersion.Readable signedOn))
                    |> Expect.equal "the head's patient" (Some { domainStub with Department = Some "ICU" })

                    Session.sessionPatient none "stub-patient" (Some unreadable)
                    |> Expect.isNone "an unreadable head has no patient to show"

                    Session.sessionPatient none "stub-patient" None |> Expect.isNone "from nothing"
                }

                test "the open holds the head whatever its case and is opened with a readable one only" {
                    let over head =
                        Session.openedOn (fun _ -> None) prescriber "stub-patient" (OpenedToken "opened-s-1") "t" head

                    let readable = Session.StoredVersion.Readable version

                    over (Some readable)
                    |> Expect.equal "readable: held and opened with" (opened (Some readable) |> fun o -> { o with Patient = Some domainStub }, Some "plan-1")

                    let o, openedWith = over (Some unreadable)
                    o.Head |> Expect.equal "held, so the client is told it cannot be shown" (Some unreadable)
                    o.Patient |> Expect.isNone "no data to show"
                    openedWith |> Expect.isNone "not opened with: the record has moved on"

                    over None |> snd |> Expect.isNone "from nothing"
                }
            ]


SessionOpenedTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore
