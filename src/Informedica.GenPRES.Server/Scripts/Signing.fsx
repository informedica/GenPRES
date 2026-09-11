// UC-3 prescribe and sign against server-hosted stubs (plan 622), PR 2: the signing challenge
// (uc-03 step 2; Rules 20, 33, 34, 43, 44).
//
// Script-first draft (script-only policy) of:
//   - the wire: `SigningRefusal`, `SigningResponse` → `Shared/Types.fs` (the port answers
//     it, like `LaunchOutcome`); `SigningCommand.RequestSignChallenge`,
//     `IServerApi.processSigning` → `Shared/Api.fs`;
//   - the port: `SessionPort.challenge` → `Ports.fs`;
//   - `Hop`: `Challenge`, `State.Challenges` (one per Session, two minutes), `challenge` as
//     the ladder of uc-03 step 2 → `Adapters.fs`; `sessionDisabled` refusing;
//   - the composition root: `processSigning` over the session cookie → `CompositionRoot.fs`.
//
// PR 1 (merged, #624) put the record, the head at open and the credential lock in place; this
// script re-states only what changes: a `State` of the fields the challenge reads, over the
// source's `SessionRecord`. The Submission (step 3) is PR 3. Run: `dotnet fsi Signing.fsx`
// from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models
open ServerApi


// ---------------------------------------------------------------------------------------------
// Wire (→ Shared/Types.fs, Shared/Api.fs)
// ---------------------------------------------------------------------------------------------

/// Why a signature did not proceed (uc-03 steps 2 and 3). The first eight end the signing and
/// are told once; `PinWrong` and `Locked` keep the PIN dialog open; `PinLimit` ends the
/// Session (Rule 28). The PIN cases arrive with the Submission (PR 3).
[<RequireQualifiedAccess>]
type SigningRefusal =
    // no Session for the cookie, or none at all
    | NoSession
    // the Session has no Patient, or the plan names other patient data (Rule 33)
    | NoPatient
    // nobody to sign as, or the Role is not Prescriber (Concept 7, Rule 38)
    | NotPrescriber
    // Rule 20: the record moved on; whose version, and when
    | Blocked of OrderPlanHead
    // Rule 44: the patient data changed since the Session opened, or cannot be read
    | DataChanged
    // Rule 34: not the OpenedToken this Session holds
    | StaleToken
    // Rule 43: not the plan the challenge was issued over, or no challenge
    | ChallengeMismatch
    | ChallengeExpired
    // Rule 28
    | PinWrong of attemptsLeft: int
    | PinLimit
    | Locked of until: DateTime


/// The signing command family (uc-03 steps 2 and 3): always cookie-authenticated, like the
/// session family. Room to grow: the Submission (PR 3).
[<RequireQualifiedAccess>]
type SigningCommand =
    // step 2: the plan as shown, and the OpenedToken the Session holds (Rule 34)
    | RequestSignChallenge of OrderPlan * OpenedToken


[<RequireQualifiedAccess>]
type SigningResponse =
    // the challenge over exactly this plan (Rule 43); comes back with the PIN
    | ChallengeIssued of challenge: string
    | Refused of SigningRefusal


module SigningCommand =

    /// For the log. Never the plan (long) or, later, the PIN.
    let toString cmd =
        match cmd with
        | SigningCommand.RequestSignChallenge _ -> "RequestSignChallenge"


// ---------------------------------------------------------------------------------------------
// Ports (→ ServerApi.Ports.fs)
// ---------------------------------------------------------------------------------------------

type SessionPort =
    {
        present: Launch * PublicKey -> Async<LaunchResult>
        callback: Callback -> Async<CallbackResult>
        find: string -> Async<SessionLookup>
        close: string -> Async<unit>
        findEnrolment: string -> Async<EnrolmentPending option>
        supplyPin: string -> string -> string -> Async<SupplyPinResult>
        dropEnrolment: string -> Async<unit>
        // UC-3 step 2: a challenge over the plan as shown, for the Session the cookie names
        challenge: string -> OrderPlan * OpenedToken -> Async<SigningResponse>
    }


// ---------------------------------------------------------------------------------------------
// The challenge (→ ServerApi.Adapters.fs, `Hop`)
// ---------------------------------------------------------------------------------------------

module Hop =

    type SessionRecord = ServerApi.Hop.SessionRecord


    /// A signing challenge as the store holds it (Concept 17): one per Session, over exactly
    /// the patient data and the orders shown (Rule 43), for two minutes.
    type Challenge =
        {
            Nonce: string
            Patient: Patient
            Scenarios: OrderScenario[]
            Expiry: DateTime
        }


    /// The state of PR 2: the fields the challenge reads; the rest stays as
    /// `ServerApi.Hop.State` has it.
    type State =
        {
            Sessions: Map<string, SessionRecord>
            Records: Map<string, SignedOrderPlan list>
            // UC-3: the live challenge per Session
            Challenges: Map<string, Challenge>
        }


    let emptyState =
        {
            Sessions = Map.empty
            Records = Map.empty
            Challenges = Map.empty
        }


    /// How long a challenge lives: the launch's two minutes, the time to read the modal and
    /// enter a PIN, so what is signed was checked against the platform moments ago (Rule 44).
    let challengeLifetime = TimeSpan.FromMinutes 2.0


    /// Rule 19: the most recent signed version of a patient's record, if any.
    let headOf (patientId: string) (state: State) =
        state.Records |> Map.tryFind patientId |> Option.bind List.tryHead


    let private dropExpired (now: DateTime) (state: State) =
        { state with Challenges = state.Challenges |> Map.filter (fun _ c -> now <= c.Expiry) }


    /// Rule 20: the head of the record, when it is not the version the Session opened with.
    let blockedBy (record: SessionRecord) (patientId: string) (state: State) =
        match headOf patientId state with
        | Some head when Some head.Head.Id <> record.OpenedWith -> Some head.Head
        | _ -> None


    /// uc-03 step 2, in order: the Session with a User and a Patient; the plan over this
    /// Patient's data (Rule 33); the Role Prescriber; the OpenedToken this Session holds
    /// (Rule 34); the patient data re-read and unchanged (Rule 44); the record not moved on
    /// (Rule 20). Then a challenge over exactly this plan (Rule 43), replacing the Session's
    /// earlier one. The PIN is not involved: a refusal here costs no attempt (Rule 28).
    let challenge
        (now: DateTime)
        (newId: unit -> string)
        (patientData: string -> Patient option)
        (sid: string)
        (plan: OrderPlan, opened: OpenedToken)
        (state: State)
        : State * SigningResponse
        =
        let state = dropExpired now state
        let refuse refusal = state, SigningResponse.Refused refusal

        match state.Sessions |> Map.tryFind sid with
        | None -> refuse SigningRefusal.NoSession
        | Some record ->
            match record.Session.User, record.Session.PatientContext with
            | None, _ -> refuse SigningRefusal.NotPrescriber
            | Some _, None -> refuse SigningRefusal.NoPatient
            | Some user, Some patient ->
                if plan.Patient <> patient.Patient then
                    refuse SigningRefusal.NoPatient
                elif user.Role <> UserRole.Prescriber then
                    refuse SigningRefusal.NotPrescriber
                elif record.Session.OpenedToken <> Some opened then
                    refuse SigningRefusal.StaleToken
                elif patientData patient.PatientId <> Some patient.Patient then
                    refuse SigningRefusal.DataChanged
                else
                    match blockedBy record patient.PatientId state with
                    | Some head -> refuse (SigningRefusal.Blocked head)
                    | None ->
                        let nonce = newId ()

                        { state with
                            Challenges =
                                state.Challenges
                                |> Map.add
                                    sid
                                    {
                                        Nonce = nonce
                                        Patient = plan.Patient
                                        Scenarios = plan.Scenarios
                                        Expiry = now + challengeLifetime
                                    }
                        },
                        SigningResponse.ChallengeIssued nonce


    /// The port member, as `makeSessionPort` wires it: the pure step under the state lock.
    let challengePort (now: unit -> DateTime) (newId: unit -> string) (patientData: PatientDataPort) (initial: State) =
        let gate = obj ()
        let mutable state = initial

        let update f =
            lock gate (fun () ->
                let next, result = f state
                state <- next
                result
            )

        (fun sid request -> async { return update (fun s -> challenge (now ()) newId patientData.read sid request s) }),
        (fun () -> state)


/// What the production port answers until the scope switch: nothing is signed.
let challengeDisabled: string -> OrderPlan * OpenedToken -> Async<SigningResponse> =
    fun _ _ -> async { return SigningResponse.Refused SigningRefusal.NoSession }


// ---------------------------------------------------------------------------------------------
// Composition root (→ ServerApi.CompositionRoot.fs)
// ---------------------------------------------------------------------------------------------

module CompositionRoot =

    /// A signing command for the Session the cookie names. No cookie, no Session: refused
    /// before the port is asked. Writes no cookie.
    let processSigning
        (challenge: string -> OrderPlan * OpenedToken -> Async<SigningResponse>)
        (cookie: SessionCookie)
        (cmd: SigningCommand)
        =
        async {
            match cookie.read () with
            | None -> return SigningResponse.Refused SigningRefusal.NoSession
            | Some id ->
                match cmd with
                | SigningCommand.RequestSignChallenge(plan, opened) -> return! challenge id (plan, opened)
        }


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


let t0 = DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
let minutes (n: float) = TimeSpan.FromMinutes n
let seconds (n: float) = TimeSpan.FromSeconds n

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

let reader =
    {
        UserId = "reader"
        DisplayName = "Stub Reader"
        Role = UserRole.Reader
    }

let stubPatient = Patient.empty
let otherData = { Patient.empty with Department = Some "ICU" }
let token sid = OpenedToken $"opened-{sid}"


/// A Session as the store holds it after an open.
let session (sid: string) (user: UserContext option) (patient: (string * Patient) option) (openedWith: string option) =
    sid,
    ({
        Session =
            {
                User = user
                PatientContext =
                    patient
                    |> Option.map (fun (pid, data) ->
                        {
                            PatientId = pid
                            Patient = data
                        }
                    )
                OpenedToken = Some(token sid)
                KeyThumbprint = Some "t"
            }
        Login = user |> Option.map _.UserId
        OpenedWith = openedWith
    }: Hop.SessionRecord)


let signedBy (user: UserContext) no (at: DateTime) : SignedOrderPlan =
    {
        Head =
            {
                Id = $"plan-{no}"
                No = no
                By = user
                SignedAt = at
            }
        PatientId = "stub-patient"
        Base = (if no > 1 then Some $"plan-{no - 1}" else None)
        Scenarios = [||]
        Patient = stubPatient
    }


let stateOf (sessions: (string * Hop.SessionRecord) list) (records: (string * SignedOrderPlan list) list) =
    { Hop.emptyState with
        Sessions = Map.ofList sessions
        Records = Map.ofList records
    }


let counter prefix =
    let n = ref 0

    fun () ->
        n.Value <- n.Value + 1
        $"{prefix}-{n.Value}"


let plan = OrderPlan.create stubPatient [||]

/// The platform as the stub has it: every patient is `Patient.empty`, `no-data` has none.
let platform pid =
    if pid = "no-data" then None else Some stubPatient


let ask now nonces state sid (plan, opened) =
    Hop.challenge now nonces platform sid (plan, opened) state


let opened = session "s-1" (Some prescriber) (Some("stub-patient", stubPatient)) None


let ladderTests =
    testList
        "Hop.challenge refuses"
        [
            test "no Session for the id" {
                stateOf [] []
                |> ask t0 (counter "n") <| "s-9" <| (plan, token "s-9")
                |> snd
                |> Expect.equal "no session" (SigningResponse.Refused SigningRefusal.NoSession)
            }

            test "the anonymous Session: nobody to sign as (Concept 7)" {
                stateOf [ session "s-1" None None None ] []
                |> ask t0 (counter "n") <| "s-1" <| (plan, token "s-1")
                |> snd
                |> Expect.equal "not a prescriber" (SigningResponse.Refused SigningRefusal.NotPrescriber)
            }

            test "a Session without a Patient (ext 1a)" {
                stateOf [ session "s-1" (Some prescriber) None None ] []
                |> ask t0 (counter "n") <| "s-1" <| (plan, token "s-1")
                |> snd
                |> Expect.equal "no patient" (SigningResponse.Refused SigningRefusal.NoPatient)
            }

            test "a plan over other patient data than the Session's (Rule 33)" {
                stateOf [ opened ] []
                |> ask t0 (counter "n") <| "s-1" <| (OrderPlan.create otherData [||], token "s-1")
                |> snd
                |> Expect.equal "no patient" (SigningResponse.Refused SigningRefusal.NoPatient)
            }

            test "a Reader (Rule 26), before the token is looked at" {
                stateOf [ session "s-1" (Some reader) (Some("stub-patient", stubPatient)) None ] []
                |> ask t0 (counter "n") <| "s-1" <| (plan, token "stale")
                |> snd
                |> Expect.equal "not a prescriber" (SigningResponse.Refused SigningRefusal.NotPrescriber)
            }

            test "not the OpenedToken this Session holds (Rule 34)" {
                stateOf [ opened ] []
                |> ask t0 (counter "n") <| "s-1" <| (plan, token "s-2")
                |> snd
                |> Expect.equal "stale" (SigningResponse.Refused SigningRefusal.StaleToken)
            }

            test "the patient data changed, or cannot be read (Rule 44)" {
                // opened with data the platform no longer answers
                stateOf [ session "s-1" (Some prescriber) (Some("no-data", stubPatient)) None ] []
                |> ask t0 (counter "n") <| "s-1" <| (plan, token "s-1")
                |> snd
                |> Expect.equal "cannot be read" (SigningResponse.Refused SigningRefusal.DataChanged)

                // opened with data the platform has since changed
                stateOf [ session "s-1" (Some prescriber) (Some("stub-patient", otherData)) None ] []
                |> ask t0 (counter "n") <| "s-1" <| (OrderPlan.create otherData [||], token "s-1")
                |> snd
                |> Expect.equal "changed" (SigningResponse.Refused SigningRefusal.DataChanged)
            }

            test "the record moved on (Rule 20): whose version, and when" {
                let byOther = signedBy other 1 (t0 - minutes 5.0)

                // opened from nothing, someone signed since
                stateOf [ opened ] [ "stub-patient", [ byOther ] ]
                |> ask t0 (counter "n") <| "s-1" <| (plan, token "s-1")
                |> snd
                |> Expect.equal "blocked" (SigningResponse.Refused(SigningRefusal.Blocked byOther.Head))

                // opened with version 1, version 2 signed since
                let v2 = signedBy other 2 (t0 - minutes 1.0)

                stateOf
                    [ session "s-1" (Some prescriber) (Some("stub-patient", stubPatient)) (Some "plan-1") ]
                    [ "stub-patient", [ v2; signedBy prescriber 1 (t0 - minutes 5.0) ] ]
                |> ask t0 (counter "n") <| "s-1" <| (plan, token "s-1")
                |> snd
                |> Expect.equal "blocked by v2" (SigningResponse.Refused(SigningRefusal.Blocked v2.Head))
            }

            test "changed data is told before the block: the ladder's order" {
                stateOf
                    [ session "s-1" (Some prescriber) (Some("no-data", stubPatient)) None ]
                    [ "no-data", [ signedBy other 1 t0 ] ]
                |> ask t0 (counter "n") <| "s-1" <| (plan, token "s-1")
                |> snd
                |> Expect.equal "data first" (SigningResponse.Refused SigningRefusal.DataChanged)
            }

            test "a refusal stores no challenge" {
                let state, _ = stateOf [ opened ] [] |> ask t0 (counter "n") <| "s-1" <| (plan, token "s-2")
                state.Challenges |> Expect.isEmpty "nothing stored"
            }
        ]


let issueTests =
    testList
        "Hop.challenge issues"
        [
            test "over this exact plan, for this Session, for two minutes (Rules 43, 44)" {
                let nonces = counter "n"
                let state, answer = stateOf [ opened ] [] |> ask t0 nonces <| "s-1" <| (plan, token "s-1")
                answer |> Expect.equal "issued" (SigningResponse.ChallengeIssued "n-1")

                state.Challenges["s-1"]
                |> Expect.equal
                    "stored"
                    {
                        Nonce = "n-1"
                        Patient = stubPatient
                        Scenarios = [||]
                        Expiry = t0 + minutes 2.0
                    }
            }

            test "opened with the head: not blocked (Rule 20)" {
                stateOf
                    [ session "s-1" (Some prescriber) (Some("stub-patient", stubPatient)) (Some "plan-1") ]
                    [ "stub-patient", [ signedBy other 1 (t0 - minutes 5.0) ] ]
                |> ask t0 (counter "n") <| "s-1" <| (plan, token "s-1")
                |> snd
                |> Expect.equal "issued" (SigningResponse.ChallengeIssued "n-1")
            }

            test "a second request replaces the Session's challenge; another Session has its own" {
                let nonces = counter "n"
                let s2 = session "s-2" (Some other) (Some("stub-patient", stubPatient)) None
                let state, _ = stateOf [ opened; s2 ] [] |> ask t0 nonces <| "s-1" <| (plan, token "s-1")
                let state, again = ask (t0 + seconds 10.0) nonces state "s-1" (plan, token "s-1")
                let state, theirs = ask (t0 + seconds 20.0) nonces state "s-2" (plan, token "s-2")

                again |> Expect.equal "replaced" (SigningResponse.ChallengeIssued "n-2")
                theirs |> Expect.equal "their own" (SigningResponse.ChallengeIssued "n-3")
                state.Challenges |> Map.count |> Expect.equal "one per Session" 2
                state.Challenges["s-1"].Nonce |> Expect.equal "the newest" "n-2"
            }

            test "a challenge is gone after two minutes" {
                let nonces = counter "n"
                let s2 = session "s-2" (Some other) (Some("stub-patient", stubPatient)) None
                let state, _ = stateOf [ opened; s2 ] [] |> ask t0 nonces <| "s-1" <| (plan, token "s-1")
                let state, _ = ask (t0 + minutes 2.0) nonces state "s-2" (plan, token "s-2")
                state.Challenges |> Map.containsKey "s-1" |> Expect.isTrue "still there at two minutes"
                let state, _ = ask (t0 + minutes 2.0 + seconds 1.0) nonces state "s-2" (plan, token "s-2")
                state.Challenges |> Map.containsKey "s-1" |> Expect.isFalse "gone after"
            }
        ]


let memoryCookie (initial: string option) =
    let value = ref initial

    {
        SessionCookie.read = fun () -> value.Value
        write = fun id -> value.Value <- Some id
        delete = fun () -> value.Value <- None
    },
    value


let compositionTests =
    testList
        "processSigning"
        [
            testAsync "without a cookie the port is never asked" {
                let asked = ref false
                let port _ _ = async { asked.Value <- true; return SigningResponse.ChallengeIssued "n" }
                let cookie, _ = memoryCookie None
                let! answer = CompositionRoot.processSigning port cookie (SigningCommand.RequestSignChallenge(plan, token "s-1"))
                answer |> Expect.equal "refused" (SigningResponse.Refused SigningRefusal.NoSession)
                asked.Value |> Expect.isFalse "not asked"
            }

            testAsync "with a cookie the port is asked for that Session, and the cookie is untouched" {
                let port, stateOf' = Hop.challengePort (fun () -> t0) (counter "n") StubPatientData.port (stateOf [ opened ] [])
                let cookie, held = memoryCookie (Some "s-1")
                let! answer = CompositionRoot.processSigning port cookie (SigningCommand.RequestSignChallenge(plan, token "s-1"))
                answer |> Expect.equal "issued" (SigningResponse.ChallengeIssued "n-1")
                held.Value |> Expect.equal "cookie kept" (Some "s-1")
                (stateOf' ()).Challenges |> Map.containsKey "s-1" |> Expect.isTrue "stored under the cookie's id"

                let cookie, _ = memoryCookie (Some "s-9")
                let! answer = CompositionRoot.processSigning port cookie (SigningCommand.RequestSignChallenge(plan, token "s-9"))
                answer |> Expect.equal "unknown id" (SigningResponse.Refused SigningRefusal.NoSession)
            }

            testAsync "the production port refuses" {
                let cookie, _ = memoryCookie (Some "s-1")
                let! answer = CompositionRoot.processSigning challengeDisabled cookie (SigningCommand.RequestSignChallenge(plan, token "s-1"))
                answer |> Expect.equal "refused" (SigningResponse.Refused SigningRefusal.NoSession)
            }

            test "the log never sees the plan" {
                SigningCommand.RequestSignChallenge(plan, token "s-1")
                |> SigningCommand.toString
                |> Expect.equal "name only" "RequestSignChallenge"
            }
        ]


let tests = testList "Signing PR 2" [ ladderTests; issueTests; compositionTests ]


runTestsWithCLIArgs [] [||] tests
