// Compute bound to the Session (plan 635), PR 3: opening the version the notice named
// (Rules 18 to 20; UC-4 step 4, the model's `OpenOrderPlan of OrderPlanId`). A User told that
// the record moved on (Rule 21) takes up that version: it becomes what the Session opened
// with, the OpenedToken is re-minted over it, and a challenge over the old baseline is dropped.
//
// Script-first draft (script-only policy) of:
//   - `SessionCommand.OpenVersion of id: string` → `Shared/Api.fs`;
//   - `Hop.openVersion` → `Adapters.fs`, `SessionPort.openVersion` → `Ports.fs`, both answering
//     `SessionOpened option` (the adapter does not see `Shared.Api`, as `find` answers a
//     `SessionLookup`); the `processSession` arm wraps it in `SessionResp` → `CompositionRoot.fs`.
// The client side (`SessionMsg.OpenVersion`, `Reopened`, `SessionEffect.CallOpenVersion`) is
// edited directly, as UI code.
//
// Only what changes is re-stated: the state has the four fields `openVersion` touches. Run:
// `dotnet fsi Compute.fsx` from this directory (build first).
//
// A second section drafts #640, the patient a Session opens on when the platform has none.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models
open ServerApi


// ---------------------------------------------------------------------------------------------
// Opening a version (→ ServerApi.Adapters.fs, `Hop`)
// ---------------------------------------------------------------------------------------------

module Hop =

    open ServerApi.Hop

    /// The state of PR 3: only the fields `openVersion` reads or writes.
    type State =
        {
            Sessions: Map<string, SessionRecord>
            Records: Map<string, SignedOrderPlan list>
            Notices: Map<string, Notice>
            Challenges: Map<string, Challenge>
        }


    let emptyState =
        {
            Sessions = Map.empty
            Records = Map.empty
            Notices = Map.empty
            Challenges = Map.empty
        }


    /// Rule 9, as in the source.
    let touch (now: DateTime) (sid: string) (state: State) : State =
        { state with
            Sessions = state.Sessions |> Map.change sid (Option.map (fun r -> { r with Seen = now }))
        }


    /// Rules 18 to 20, UC-4 step 4: version `id` becomes what the Session opened with. No
    /// Session, an anonymous one or one without a Patient: nothing to open (Rule 13). An id the
    /// record does not hold for the Session's Patient (a stale button, a restart): nothing
    /// opens, the Session as it is; the next request tells what the head is (Rule 21). The
    /// version already open: the token stands. Another version: the OpenedToken is re-minted
    /// over it (Rule 34) and the standing challenge and notice of this Session are dropped (a
    /// challenge over the old baseline must not be answerable). Any version may be opened
    /// (Rule 18); one that is not the head leaves Submission blocked (Rule 20).
    let openVersion
        (now: DateTime)
        (newId: unit -> string)
        (sid: string)
        (id: string)
        (state: State)
        : State * SessionOpened option
        =
        match state.Sessions |> Map.tryFind sid with
        | None -> state, None
        | Some record ->
            let state = touch now sid state

            match record.Session.User, record.Session.PatientContext with
            | None, _
            | _, None -> state, None
            | Some _, Some patient ->
                let version =
                    state.Records
                    |> Map.tryFind patient.PatientId
                    |> Option.bind (List.tryFind (fun v -> v.Head.Id = id))

                match version with
                | None -> state, Some record.Session
                | Some version when record.OpenedWith = Some id ->
                    let session = { record.Session with Head = Some version }

                    { state with Sessions = state.Sessions |> Map.add sid { record with Session = session } },
                    Some session
                | Some version ->
                    let session =
                        { record.Session with
                            OpenedToken = Some(OpenedToken $"opened-{newId ()}")
                            Head = Some version
                        }

                    { state with
                        Sessions =
                            state.Sessions
                            |> Map.add
                                sid
                                { record with
                                    Session = session
                                    OpenedWith = Some id
                                }
                        Challenges = state.Challenges |> Map.remove sid
                        Notices = state.Notices |> Map.remove sid
                    },
                    Some session


// ---------------------------------------------------------------------------------------------
// The patient at open (#640) (→ ServerApi.Adapters.fs, `Hop.sessionPatient` and `StubPatientData`)
// ---------------------------------------------------------------------------------------------

// Script-first draft of #640: since #639 a Session opens with the head of the record in the
// cart, but where the PatientDataPlatform has no reading (ext 6a) the Session opened on
// `Patient.empty`, and the data the head was signed on (`SignedOrderPlan.Patient`, Rule 44)
// was not shown. At open, in this order: the platform's reading (Concept 2: the source of
// truth), else the head's patient (Rule 19: the last patient context seen), else empty. The
// change is one line in `openWith`, made a public pure function so it can be tested alone.
// `openVersion` is left as it is (plan 635: no SetPatient at a reopen), and Rule 44 is
// unaffected: the challenge re-reads the platform and compares with what the User saw.
//
// The stub platform used to answer `Patient.empty` for every PatientId but `no-data`; an
// empty record is a reading, so the fallback never applied to `stub-patient`, and a hand
// entered age was lost at every reload. It now answers a fixed patient, so the panel is filled
// from the platform at launch and the fallback is exercised with `no-data`.

module Hop640 =

    /// #640: the patient a Session opens on. The platform's reading (Concept 2) wins; without
    /// one, the patient data of the head of the record, the last seen (Rule 19); from nothing,
    /// an empty patient (ext 6a).
    let sessionPatient (patientData: string -> Patient option) (patientId: string) (head: SignedOrderPlan option) : Patient =
        patientData patientId
        |> Option.orElse (head |> Option.map _.Patient)
        |> Option.defaultValue Patient.empty


    /// #640, at the commit: the Session's patient after a signature. With a reading at the
    /// challenge (`Verified`) the reading stands, as the Session opened on it; without one the
    /// data just signed, so a resume in this Session shows what a relaunch would.
    let commitPatient (verified: bool) (opened: Patient) (signed: Patient) : Patient =
        if verified then opened else signed


/// The PatientDataPlatform stub: a fixed patient for every PatientId, none at all for
/// `no-data` (ext 6a).
module StubPatientData640 =

    /// The stub's reading: ten years, 32 kg, 140 cm, nothing else known.
    let patient: Patient =
        Patient.create
            (Some(Shared.Measures.toYear 10))
            None
            None
            None
            (Some 32000)
            (Some 140)
            None
            None
            UnknownGender
            []
            None
            None
        |> Option.defaultValue Patient.empty


    let port: ServerApi.PatientDataPort =
        {
            read = fun pid -> if pid = "no-data" then None else Some patient
        }


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


let t0 = DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
let t1 = t0.AddMinutes 5.0

let counter prefix =
    let n = ref 0

    fun () ->
        n.Value <- n.Value + 1
        $"{prefix}-{n.Value}"

let prescriber =
    {
        UserId = "prescriber"
        DisplayName = "Stub Prescriber"
        Role = UserRole.Prescriber
    }

let other =
    { prescriber with
        UserId = "prescriber-b"
        DisplayName = "Stub Prescriber B"
    }


let signedBy (user: UserContext) no : SignedOrderPlan =
    {
        Head =
            {
                Id = $"plan-{no}"
                No = no
                By = user
                SignedAt = t0
            }
        PatientId = "pat-1"
        Base = if no > 1 then Some $"plan-{no - 1}" else None
        Scenarios = [||]
        Patient = Patient.empty
        Verified = true
    }


let session (user: UserContext option) (patientId: string option) (sid: string) (openedWith: string option) : ServerApi.Hop.SessionRecord =
    {
        Session =
            {
                User = user
                PatientContext =
                    patientId
                    |> Option.map (fun pid ->
                        {
                            PatientId = pid
                            Patient = Patient.empty
                        }
                    )
                OpenedToken = Some(OpenedToken $"opened-{sid}")
                KeyThumbprint = Some "t"
                Head = None
            }
        Login = user |> Option.map _.UserId
        OpenedWith = openedWith
        Seen = t0
    }


let challenged sid : ServerApi.Hop.Challenge =
    {
        Nonce = $"c-{sid}"
        Patient = Patient.empty
        Scenarios = [||]
        Verified = true
        Expiry = t0.AddMinutes 2.0
    }


/// A opened on plan-1; B signed plan-2 meanwhile; A has a challenge standing over plan-1.
let movedOn =
    { Hop.emptyState with
        Sessions = Map.ofList [ "s-1", session (Some prescriber) (Some "pat-1") "s-1" (Some "plan-1") ]
        Records = Map.ofList [ "pat-1", [ signedBy other 2; signedBy prescriber 1 ] ]
        Challenges = Map.ofList [ "s-1", challenged "s-1" ]
        Notices = Map.ofList [ "s-1", { Nonce = "n"; Data = None; Expiry = t0.AddMinutes 2.0 } ]
    }

let openAt sid id state =
    Hop.openVersion t1 (counter "id") sid id state


let tests =
    testList
        "openVersion (Rules 18 to 20, UC-4 step 4)"
        [
            test "no Session: nothing to open" {
                Hop.emptyState |> openAt "s-9" "plan-2" |> snd |> Expect.equal "none" None
            }

            test "an anonymous Session, or one without a Patient: nothing to open (Rule 13)" {
                let state =
                    { movedOn with
                        Sessions =
                            Map.ofList
                                [
                                    "s-a", session None (Some "pat-1") "s-a" None
                                    "s-n", session (Some prescriber) None "s-n" None
                                ]
                    }

                state |> openAt "s-a" "plan-2" |> snd |> Expect.equal "anonymous" None
                state |> openAt "s-n" "plan-2" |> snd |> Expect.equal "no patient" None
            }

            test "an id the record does not hold: nothing opens, the Session as it is, token kept" {
                let state, answer = movedOn |> openAt "s-1" "plan-9"
                answer |> Expect.equal "as it is" (Some movedOn.Sessions["s-1"].Session)
                state.Sessions["s-1"].OpenedWith |> Expect.equal "unchanged" (Some "plan-1")
                state.Challenges |> Map.containsKey "s-1" |> Expect.isTrue "challenge kept"
                state.Sessions["s-1"].Seen |> Expect.equal "touched" t1
            }

            test "the version already open: the token stands, the version is answered" {
                let state, answer = movedOn |> openAt "s-1" "plan-1"

                match answer with
                | Some opened ->
                    opened.OpenedToken |> Expect.equal "kept" (Some(OpenedToken "opened-s-1"))
                    opened.Head |> Expect.equal "the version" (Some(signedBy prescriber 1))
                | other -> failtest $"expected the Session, got {other}"

                state.Challenges |> Map.containsKey "s-1" |> Expect.isTrue "challenge kept"
            }

            test "the head: opened, the token re-minted, the challenge and notice dropped (Rules 19, 34)" {
                let state, answer = movedOn |> openAt "s-1" "plan-2"

                match answer with
                | Some opened ->
                    opened.OpenedToken |> Expect.equal "re-minted" (Some(OpenedToken "opened-id-1"))
                    opened.Head |> Expect.equal "B's version" (Some(signedBy other 2))
                | other -> failtest $"expected the Session, got {other}"

                state.Sessions["s-1"].OpenedWith |> Expect.equal "opened with the head" (Some "plan-2")
                state.Sessions["s-1"].Session.OpenedToken |> Expect.equal "held" (Some(OpenedToken "opened-id-1"))
                state.Challenges |> Expect.isEmpty "challenge dropped"
                state.Notices |> Expect.isEmpty "notice dropped"

                // Rule 20 no longer blocks; the old token is stale (Rule 34)
                ServerApi.Hop.blockedBy state.Sessions["s-1"] "pat-1" { ServerApi.Hop.emptyState with Records = state.Records }
                |> Expect.isNone "not blocked"
            }

            test "an older version: opened, still blocked by the head (Rules 18, 20)" {
                let three =
                    { movedOn with
                        Records = Map.ofList [ "pat-1", [ signedBy prescriber 3; signedBy other 2; signedBy prescriber 1 ] ]
                    }

                let state, answer = three |> openAt "s-1" "plan-2"

                match answer with
                | Some opened -> opened.Head |> Expect.equal "plan-2" (Some(signedBy other 2))
                | other -> failtest $"expected the Session, got {other}"

                ServerApi.Hop.blockedBy state.Sessions["s-1"] "pat-1" { ServerApi.Hop.emptyState with Records = state.Records }
                |> Option.map _.Id
                |> Expect.equal "the head still blocks" (Some "plan-3")
            }
        ]


let patientTests =
    let signedOn (patient: Patient) = Some { signedBy prescriber 1 with Patient = patient }
    let entered = { Patient.empty with Department = Some "ICU" }
    let none (_: string) = None

    testList
        "the patient at open (#640)"
        [
            test "a reading wins over the signed patient (Concept 2)" {
                Hop640.sessionPatient StubPatientData640.port.read "stub-patient" (signedOn entered)
                |> Expect.equal "the platform's" StubPatientData640.patient
            }

            test "no reading: the patient the head was signed on (Rule 19)" {
                Hop640.sessionPatient none "no-data" (signedOn entered)
                |> Expect.equal "the signed" entered
            }

            test "no reading, no record: an empty patient (ext 6a)" {
                Hop640.sessionPatient none "no-data" None |> Expect.equal "empty" Patient.empty
            }

            test "at the commit: verified keeps the reading, unverified takes the data signed" {
                Hop640.commitPatient true StubPatientData640.patient entered
                |> Expect.equal "the reading" StubPatientData640.patient

                Hop640.commitPatient false Patient.empty entered |> Expect.equal "the signed" entered
            }

            test "the stub: a fixed patient for every id, none for no-data" {
                StubPatientData640.port.read "stub-patient"
                |> Expect.equal "a reading" (Some StubPatientData640.patient)

                StubPatientData640.patient |> Expect.notEqual "not empty" Patient.empty
                StubPatientData640.patient.Age |> Option.map _.Years |> Expect.equal "ten" (Some(Shared.Measures.toYear 10))
                StubPatientData640.port.read "no-data" |> Expect.isNone "no data"
            }
        ]


runTestsWithCLIArgs [] [||] (testList "Compute" [ tests; patientTests ]) |> ignore
