// UC-3 prescribe and sign against server-hosted stubs (plan 622), PR 3: the commit (uc-03
// step 3; Rules 20, 23, 27, 28, 33, 34, 38, 42, 43, 45).
//
// Script-first draft (script-only policy) of:
//   - the wire: `Submission`, `SigningResponse.Submitted`, `SignedOrderPlan.Verified`
//     → `Shared/Types.fs`; `SigningCommand.Submit` → `Shared/Api.fs`;
//   - the port: `SessionPort.submit` → `Ports.fs`;
//   - `Mails.pinLimit` (Rule 27) → `Adapters.fs`;
//   - `Hop`: `State.Answered` (Rule 45, per Session and key), `commit` as the ladder of the
//     model's `dbCommit`, the PIN last → `Adapters.fs`; `sessionDisabled` refusing;
//   - the composition root: `Submit` over the session cookie → `CompositionRoot.fs`.
//
// PR 2 (merged, #626) issued the challenge; this script re-states only what changes: a `State`
// of the fields the commit reads, over the source's records. Run: `dotnet fsi Signing.fsx`
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

/// A signed version of an order plan, as the record holds it (the integration design's
/// TreatmentPlan): the head, the patient, the version it was signed over (`Base`, `None` for
/// the first), the orders as shown at the signature, the patient data the User saw and
/// whether it was the platform's reading at the challenge (Rule 44).
type SignedOrderPlan =
    {
        Head: OrderPlanHead
        PatientId: string
        Base: string option
        Scenarios: OrderScenario[]
        Patient: Patient
        Verified: bool
    }


/// uc-03 step 3: the plan as shown, the OpenedToken the Session holds (Rule 34), the
/// challenge it was issued (Rule 43), the PIN (Rule 23), and a key of the client's own so that
/// the commit takes effect once (Rule 45). Never logged.
type Submission =
    {
        Plan: OrderPlan
        Opened: OpenedToken
        Challenge: string
        Pin: string
        IdemKey: string
    }


[<RequireQualifiedAccess>]
type SigningCommand =
    | RequestSignChallenge of OrderPlan * OpenedToken * dataNotice: string option
    // step 3: the signature
    | Submit of Submission


[<RequireQualifiedAccess>]
type SigningResponse =
    | ChallengeIssued of challenge: string
    | DataNotice of DataNotice
    // step 3: the version committed, and a fresh OpenedToken over it (Rule 34)
    | Submitted of SignedOrderPlan * OpenedToken
    | Refused of SigningRefusal


module SigningCommand =

    /// For the log. Never the plan (long) or the PIN.
    let toString cmd =
        match cmd with
        | SigningCommand.RequestSignChallenge _ -> "RequestSignChallenge"
        | SigningCommand.Submit _ -> "Submit"


// ---------------------------------------------------------------------------------------------
// Mail (→ ServerApi.Adapters.fs, `Mails`)
// ---------------------------------------------------------------------------------------------

module Mails =

    /// Rule 27: the third wrong PIN ended a Session and locked signing.
    let pinLimit (displayName: string) : string * string =
        "GenPRES: signing is locked",
        $"Hello {displayName},\n\nThe PIN was entered wrong three times at a signature just now. Your session was ended and signing is locked for a while. If that was not you, tell your administrator."


// ---------------------------------------------------------------------------------------------
// The commit (→ ServerApi.Adapters.fs, `Hop`)
// ---------------------------------------------------------------------------------------------

module Hop =

    type SessionRecord = ServerApi.Hop.SessionRecord
    type Challenge = ServerApi.Hop.Challenge


    /// The state of PR 3: the fields the commit reads; the rest stays as
    /// `ServerApi.Hop.State` has it.
    type State =
        {
            Sessions: Map<string, SessionRecord>
            Endings: Map<string, SessionEnding * DateTime>
            Credentials: Map<string, Credential>
            Records: Map<string, SignedOrderPlan list>
            Challenges: Map<string, Challenge>
            // UC-3, Rule 45: what a Submission was answered, by Session and by the client's key
            Answered: Map<string * string, SigningResponse * DateTime>
        }


    let emptyState =
        {
            Sessions = Map.empty
            Endings = Map.empty
            Credentials = Map.empty
            Records = Map.empty
            Challenges = Map.empty
            Answered = Map.empty
        }


    let challengeLifetime = ServerApi.Hop.challengeLifetime


    let credentialOf (userId: string) (state: State) =
        state.Credentials |> Map.tryFind userId |> Option.defaultValue Credential.empty


    let headOf (patientId: string) (state: State) =
        state.Records |> Map.tryFind patientId |> Option.bind List.tryHead


    let blockedBy (record: SessionRecord) (patientId: string) (state: State) =
        match headOf patientId state with
        | Some head when Some head.Head.Id <> record.OpenedWith -> Some head.Head
        | _ -> None


    /// Concept 10: an order appears once in a plan.
    let duplicateOrders (scenarios: OrderScenario[]) =
        scenarios |> Array.countBy _.Order.Id |> Array.exists (fun (_, n) -> n > 1)


    let private dropExpired (now: DateTime) (state: State) =
        { state with
            Challenges = state.Challenges |> Map.filter (fun _ c -> now <= c.Expiry)
            Answered = state.Answered |> Map.filter (fun _ (_, at) -> now <= at + challengeLifetime)
        }


    /// uc-03 step 3, one act in the model's order (`dbCommit`): the Session with a User and a
    /// Patient; the answer already given to this Session's key (Rule 45); the Role re-taken
    /// from the registry (Rule 38, fails closed); the OpenedToken this Session holds (Rule 34);
    /// the record not moved on (Rule 20); the challenge this Session was issued, over exactly
    /// this plan (Rule 43); and last the PIN (Rules 23, 28), so that a Submission that was
    /// never going to land costs no attempt. Then the version is appended, the challenge
    /// spent, the OpenedToken re-minted over the new head, and the answer remembered under the
    /// key, refusals too. Three wrong PINs end the Session (`WrongPinLimit`), lock signing and
    /// mail the User (Rule 27); a wrong PIN while locked pushes the lock out; a right PIN while
    /// locked is refused and counts nothing.
    let commit
        (now: DateTime)
        (newId: unit -> string)
        (standing: BrowserIdentity -> UserStanding option)
        (send: Mail -> unit)
        (sid: string)
        (submission: Submission)
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
                match state.Answered |> Map.tryFind (sid, submission.IdemKey) with
                | Some(answer, _) -> state, answer
                | None ->
                    // the answer is remembered from here on, under this Session and the key
                    let remember (state: State) answer =
                        { state with Answered = state.Answered |> Map.add (sid, submission.IdemKey) (answer, now) }, answer

                    let refuse refusal =
                        remember state (SigningResponse.Refused refusal)

                    let identity =
                        {
                            Login = record.Login |> Option.defaultValue user.UserId
                            DisplayName = user.DisplayName
                        }

                    match standing identity with
                    | Some fresh when fresh.User.Role = UserRole.Prescriber ->
                        if record.Session.OpenedToken <> Some submission.Opened then
                            refuse SigningRefusal.StaleToken
                        else
                            match blockedBy record patient.PatientId state with
                            | Some head -> refuse (SigningRefusal.Blocked head)
                            | None ->
                                match state.Challenges |> Map.tryFind sid with
                                | None -> refuse SigningRefusal.ChallengeExpired
                                | Some challenge when
                                    challenge.Nonce <> submission.Challenge
                                    || challenge.Patient <> submission.Plan.Patient
                                    || challenge.Scenarios <> submission.Plan.Scenarios
                                    || duplicateOrders submission.Plan.Scenarios
                                    ->
                                    refuse SigningRefusal.ChallengeMismatch
                                | Some challenge ->
                                    let credential = credentialOf user.UserId state
                                    let wasLocked = Credential.isLocked now credential
                                    let right, credential = Credential.verify now submission.Pin credential

                                    let state =
                                        { state with Credentials = state.Credentials |> Map.add user.UserId credential }

                                    if right then
                                        let id = newId ()

                                        let plan =
                                            {
                                                Head =
                                                    {
                                                        Id = id
                                                        No = 1 + (state.Records |> Map.tryFind patient.PatientId |> Option.map List.length |> Option.defaultValue 0)
                                                        By = user
                                                        SignedAt = now
                                                    }
                                                PatientId = patient.PatientId
                                                Base = record.OpenedWith
                                                Scenarios = challenge.Scenarios
                                                Patient = challenge.Patient
                                                Verified = challenge.Verified
                                            }

                                        let token = OpenedToken $"opened-{newId ()}"

                                        let opened =
                                            { record with
                                                Session = { record.Session with OpenedToken = Some token }
                                                OpenedWith = Some id
                                            }

                                        remember
                                            { state with
                                                Records =
                                                    state.Records
                                                    |> Map.change patient.PatientId (fun versions ->
                                                        Some(plan :: (versions |> Option.defaultValue []))
                                                    )
                                                Challenges = state.Challenges |> Map.remove sid
                                                Sessions = state.Sessions |> Map.add sid opened
                                            }
                                            (SigningResponse.Submitted(plan, token))
                                    elif wasLocked then
                                        // this Session did nothing wrong; the lock is the credential's
                                        remember state (SigningResponse.Refused(SigningRefusal.Locked credential.LockedUntil.Value))
                                    elif credential |> Credential.attemptsLeft = 0 then
                                        // Rule 28: the limit is reached now; the Session ends (Rule 10)
                                        let subject, body = Mails.pinLimit user.DisplayName

                                        // best effort (MailPort: fire and forget): the ending and the lock
                                        // land whatever the mail does
                                        try
                                            send
                                                {
                                                    To = fresh.MailAddress
                                                    Subject = subject
                                                    Body = body
                                                }
                                        with _ ->
                                            ()

                                        remember
                                            { state with
                                                Sessions = state.Sessions |> Map.remove sid
                                                Endings = state.Endings |> Map.add sid (SessionEnding.WrongPinLimit, now)
                                                Challenges = state.Challenges |> Map.remove sid
                                            }
                                            (SigningResponse.Refused SigningRefusal.PinLimit)
                                    else
                                        remember
                                            state
                                            (SigningResponse.Refused(SigningRefusal.PinWrong(credential |> Credential.attemptsLeft)))
                    | _ -> refuse SigningRefusal.NotPrescriber


// ---------------------------------------------------------------------------------------------
// Composition root (→ ServerApi.CompositionRoot.fs)
// ---------------------------------------------------------------------------------------------

module CompositionRoot =

    let processSigning
        (challenge: string -> OrderPlan * OpenedToken * string option -> Async<SigningResponse>)
        (submit: string -> Submission -> Async<SigningResponse>)
        (cookie: SessionCookie)
        (cmd: SigningCommand)
        =
        async {
            match cookie.read () with
            | None -> return SigningResponse.Refused SigningRefusal.NoSession
            | Some id ->
                match cmd with
                | SigningCommand.RequestSignChallenge(plan, opened, notice) -> return! challenge id (plan, opened, notice)
                | SigningCommand.Submit submission -> return! submit id submission
        }


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


let t0 = DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc)
let minutes (n: float) = TimeSpan.FromMinutes n
let seconds (n: float) = TimeSpan.FromSeconds n
let salts (n: int) = Array.init n byte

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

let stubPatient = Patient.empty
let otherData = { Patient.empty with Department = Some "ICU" }
let token sid = OpenedToken $"opened-{sid}"
let plan = OrderPlan.create stubPatient [||]


let session (sid: string) (user: UserContext) (patientId: string) (openedWith: string option) =
    sid,
    ({
        Session =
            {
                User = Some user
                PatientContext =
                    Some
                        {
                            PatientId = patientId
                            Patient = stubPatient
                        }
                OpenedToken = Some(token sid)
                KeyThumbprint = Some "t"
            }
        Login = Some user.UserId
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
        Verified = true
    }


let challenged (sid: string) (at: DateTime) : string * Hop.Challenge =
    sid,
    {
        Nonce = $"c-{sid}"
        Patient = stubPatient
        Scenarios = [||]
        Verified = true
        Expiry = at + Hop.challengeLifetime
    }


/// The registry as the stub has it, for the logins the tests use.
let registry (identity: BrowserIdentity) =
    let standing role =
        Some
            {
                User =
                    {
                        UserId = identity.Login
                        DisplayName = identity.DisplayName
                        Role = role
                    }
                ActivePatientId = Some "stub-patient"
                MailAddress = $"{identity.Login}@stub.example"
            }

    match identity.Login with
    | "prescriber"
    | "prescriber-b" -> standing UserRole.Prescriber
    | "demoted" -> standing UserRole.Reader
    | _ -> None


let outbox () =
    let sent = ref []
    (fun (m: Mail) -> sent.Value <- m :: sent.Value), sent


/// An OrderScenario with only its order id set, every other field a default (built by
/// reflection: the order graph is too deep to write by hand), for the duplicate check.
let scenarioWithOrder (id: string) : OrderScenario =
    let rec defaultOf (t: Type) : obj =
        if t = typeof<string> then box ""
        elif t = typeof<bool> then box false
        elif t = typeof<int> then box 0
        elif t = typeof<decimal> then box 0m
        elif t = typeof<float> then box 0.0
        elif t = typeof<DateTime> then box t0
        elif t.IsArray then box (Array.CreateInstance(t.GetElementType(), 0))
        elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then null
        elif Microsoft.FSharp.Reflection.FSharpType.IsRecord t then
            Microsoft.FSharp.Reflection.FSharpValue.MakeRecord(
                t,
                Microsoft.FSharp.Reflection.FSharpType.GetRecordFields t
                |> Array.map (fun f -> defaultOf f.PropertyType)
            )
        elif Microsoft.FSharp.Reflection.FSharpType.IsUnion t then
            let case = (Microsoft.FSharp.Reflection.FSharpType.GetUnionCases t)[0]

            Microsoft.FSharp.Reflection.FSharpValue.MakeUnion(
                case,
                case.GetFields() |> Array.map (fun f -> defaultOf f.PropertyType)
            )
        else
            null

    let scenario = defaultOf typeof<OrderScenario> :?> OrderScenario
    { scenario with Order = { scenario.Order with Id = id } }


let counter prefix =
    let n = ref 0

    fun () ->
        n.Value <- n.Value + 1
        $"{prefix}-{n.Value}"


let stateOf sessions records challenges =
    { Hop.emptyState with
        Sessions = Map.ofList sessions
        Credentials = StubCredentials.seed salts |> Map.map (fun _ c -> { PinHash = c.PinHash; WrongCount = 0; LockedUntil = None })
        Records = Map.ofList records
        Challenges = Map.ofList challenges
    }


let submission sid pin key : Submission =
    {
        Plan = plan
        Opened = token sid
        Challenge = $"c-{sid}"
        Pin = pin
        IdemKey = key
    }


let submitAt now ids (send: Mail -> unit) state sid (s: Submission) =
    Hop.commit now ids registry send sid s state


let submit state sid s = submitAt t0 (counter "id") (fst (outbox ())) state sid s


let opened = session "s-1" prescriber "stub-patient" None
let ready = stateOf [ opened ] [] [ challenged "s-1" t0 ]


let happyTests =
    testList
        "Hop.commit commits"
        [
            test "the version: head, base, by and patient from the Session, the plan from the challenge, verified" {
                let ids = counter "id"
                let state, answer = submitAt t0 ids ignore ready "s-1" (submission "s-1" "1234" "k-1")

                match answer with
                | SigningResponse.Submitted(signed, fresh) ->
                    signed.Head |> Expect.equal "head" { Id = "id-1"; No = 1; By = prescriber; SignedAt = t0 }
                    signed.PatientId |> Expect.equal "the Session's patient" "stub-patient"
                    signed.Base |> Expect.isNone "from nothing"
                    signed.Verified |> Expect.isTrue "the challenge's reading"
                    fresh |> Expect.equal "re-minted" (OpenedToken "opened-id-2")

                    state.Records["stub-patient"] |> Expect.equal "appended, newest first" [ signed ]
                    state.Challenges |> Expect.isEmpty "spent"
                    state.Sessions["s-1"].OpenedWith |> Expect.equal "the new head" (Some "id-1")
                    state.Sessions["s-1"].Session.OpenedToken |> Expect.equal "held" (Some fresh)
                    state.Answered[("s-1", "k-1")] |> fst |> Expect.equal "remembered" answer
                    (Hop.credentialOf "prescriber" state).WrongCount |> Expect.equal "count zero" 0
                | other -> failtest $"expected Submitted, got {other}"
            }

            test "a second version over the first: No 2, Base the first" {
                let ids = counter "id"
                let state, _ = submitAt t0 ids ignore ready "s-1" (submission "s-1" "1234" "k-1")
                // a new challenge over the new baseline, the token as re-minted
                let state = { state with Challenges = Map.ofList [ challenged "s-1" (t0 + minutes 1.0) ] }

                let again =
                    { submission "s-1" "1234" "k-2" with Opened = state.Sessions["s-1"].Session.OpenedToken.Value }

                match submitAt (t0 + minutes 1.0) ids ignore state "s-1" again |> snd with
                | SigningResponse.Submitted(signed, _) ->
                    signed.Head.No |> Expect.equal "second" 2
                    signed.Base |> Expect.equal "over the first" (Some "id-1")
                | other -> failtest $"expected Submitted, got {other}"
            }

            test "the same key again: the same answer, nothing committed twice (Rule 45)" {
                let ids = counter "id"
                let state, first = submitAt t0 ids ignore ready "s-1" (submission "s-1" "1234" "k-1")
                let state, again = submitAt (t0 + seconds 5.0) ids ignore state "s-1" (submission "s-1" "1234" "k-1")
                again |> Expect.equal "the first answer" first
                state.Records["stub-patient"] |> List.length |> Expect.equal "one version" 1
            }

            test "a remembered refusal is answered again without counting; another Session's key finds nothing" {
                let state, first = submit ready "s-1" (submission "s-1" "0000" "k-1")
                first |> Expect.equal "wrong" (SigningResponse.Refused(SigningRefusal.PinWrong 2))
                let state, again = submit state "s-1" (submission "s-1" "0000" "k-1")
                again |> Expect.equal "the same" first
                (Hop.credentialOf "prescriber" state).WrongCount |> Expect.equal "counted once" 1

                let s2 = session "s-2" other "stub-patient" None
                let state = { state with Sessions = state.Sessions |> Map.add (fst s2) (snd s2) }

                submit state "s-2" { submission "s-2" "1234" "k-1" with Challenge = "none" }
                |> snd
                |> Expect.equal "their own ladder" (SigningResponse.Refused SigningRefusal.ChallengeExpired)
            }

            test "the old token is stale once the commit re-minted it (Rule 34)" {
                let ids = counter "id"
                let state, _ = submitAt t0 ids ignore ready "s-1" (submission "s-1" "1234" "k-1")
                let state = { state with Challenges = Map.ofList [ challenged "s-1" t0 ] }

                submitAt t0 ids ignore state "s-1" (submission "s-1" "1234" "k-2")
                |> snd
                |> Expect.equal "stale" (SigningResponse.Refused SigningRefusal.StaleToken)
            }
        ]


let refusalTests =
    testList
        "Hop.commit refuses"
        [
            test "no Session, and the anonymous Session" {
                submit ready "s-9" (submission "s-9" "1234" "k")
                |> snd
                |> Expect.equal "no session" (SigningResponse.Refused SigningRefusal.NoSession)
            }

            test "the Role withdrawn since the launch (Rule 38), and a registry that cannot answer" {
                let demoted = session "s-1" { prescriber with UserId = "demoted" } "stub-patient" None
                let state = stateOf [ demoted ] [] [ challenged "s-1" t0 ]
                let state, answer = submit state "s-1" (submission "s-1" "1234" "k")
                answer |> Expect.equal "not a prescriber" (SigningResponse.Refused SigningRefusal.NotPrescriber)
                state.Records |> Expect.isEmpty "nothing committed"

                let unknown = session "s-1" { prescriber with UserId = "gone" } "stub-patient" None
                stateOf [ unknown ] [] [ challenged "s-1" t0 ] |> submit <| "s-1" <| submission "s-1" "1234" "k"
                |> snd
                |> Expect.equal "fails closed" (SigningResponse.Refused SigningRefusal.NotPrescriber)
            }

            test "a stale token, before the head is looked at" {
                let state = stateOf [ opened ] [ "stub-patient", [ signedBy other 1 t0 ] ] [ challenged "s-1" t0 ]

                submit state "s-1" { submission "s-1" "1234" "k" with Opened = OpenedToken "old" }
                |> snd
                |> Expect.equal "stale" (SigningResponse.Refused SigningRefusal.StaleToken)
            }

            test "the record moved on (Rule 20): blocked, and the PIN never looked at" {
                let byOther = signedBy other 1 t0
                let state = stateOf [ opened ] [ "stub-patient", [ byOther ] ] [ challenged "s-1" t0 ]
                let state, answer = submit state "s-1" (submission "s-1" "0000" "k")
                answer |> Expect.equal "blocked" (SigningResponse.Refused(SigningRefusal.Blocked byOther.Head))
                (Hop.credentialOf "prescriber" state).WrongCount |> Expect.equal "not counted" 0
                state.Challenges |> Map.containsKey "s-1" |> Expect.isTrue "the challenge stands"
            }

            test "the same order twice in the plan is a mismatch (Concept 10), the PIN never looked at" {
                let twice = [| scenarioWithOrder "o-1"; scenarioWithOrder "o-1" |]
                let planted = { snd (challenged "s-1" t0) with Scenarios = twice }
                let state = stateOf [ opened ] [] [ "s-1", planted ]
                let state, answer = submit state "s-1" { submission "s-1" "0000" "k" with Plan = OrderPlan.create stubPatient twice }
                answer |> Expect.equal "mismatch" (SigningResponse.Refused SigningRefusal.ChallengeMismatch)
                (Hop.credentialOf "prescriber" state).WrongCount |> Expect.equal "not counted" 0

                let once = [| scenarioWithOrder "o-1"; scenarioWithOrder "o-2" |]
                let planted = { planted with Scenarios = once }
                let state = stateOf [ opened ] [] [ "s-1", planted ]

                match submit state "s-1" { submission "s-1" "1234" "k" with Plan = OrderPlan.create stubPatient once } |> snd with
                | SigningResponse.Submitted(signed, _) -> signed.Scenarios |> Expect.equal "two orders" once
                | other -> failtest $"expected Submitted, got {other}"
            }

            test "no challenge, an expired one, another nonce, another plan (Rule 43)" {
                stateOf [ opened ] [] [] |> submit <| "s-1" <| submission "s-1" "1234" "k"
                |> snd
                |> Expect.equal "none" (SigningResponse.Refused SigningRefusal.ChallengeExpired)

                submitAt (t0 + minutes 3.0) (counter "id") ignore ready "s-1" (submission "s-1" "1234" "k")
                |> snd
                |> Expect.equal "expired" (SigningResponse.Refused SigningRefusal.ChallengeExpired)

                submit ready "s-1" { submission "s-1" "1234" "k" with Challenge = "c-other" }
                |> snd
                |> Expect.equal "another nonce" (SigningResponse.Refused SigningRefusal.ChallengeMismatch)

                let state, answer = submit ready "s-1" { submission "s-1" "0000" "k" with Plan = OrderPlan.create otherData [||] }
                answer |> Expect.equal "another plan" (SigningResponse.Refused SigningRefusal.ChallengeMismatch)
                (Hop.credentialOf "prescriber" state).WrongCount |> Expect.equal "the PIN never looked at" 0
            }
        ]


let pinTests =
    testList
        "Hop.commit and the PIN (Rule 28)"
        [
            test "a wrong PIN counts and keeps the challenge; the next right one on the same challenge signs" {
                let state, first = submit ready "s-1" (submission "s-1" "0000" "k-1")
                first |> Expect.equal "two left" (SigningResponse.Refused(SigningRefusal.PinWrong 2))
                state.Challenges |> Map.containsKey "s-1" |> Expect.isTrue "the challenge stands"

                let state, second = submit state "s-1" (submission "s-1" "0000" "k-2")
                second |> Expect.equal "one left" (SigningResponse.Refused(SigningRefusal.PinWrong 1))

                match submit state "s-1" (submission "s-1" "1234" "k-3") |> snd with
                | SigningResponse.Submitted _ -> ()
                | other -> failtest $"expected Submitted, got {other}"
            }

            test "the third wrong PIN: the Session ends, signing locks, a mail goes out (Rules 10, 27, 28)" {
                let send, sent = outbox ()
                let state, _ = submitAt t0 (counter "id") send ready "s-1" (submission "s-1" "0000" "k-1")
                let state, _ = submitAt (t0 + seconds 10.0) (counter "id") send state "s-1" (submission "s-1" "0000" "k-2")
                let state, third = submitAt (t0 + seconds 20.0) (counter "id") send state "s-1" (submission "s-1" "0000" "k-3")

                third |> Expect.equal "the limit" (SigningResponse.Refused SigningRefusal.PinLimit)
                state.Sessions |> Map.containsKey "s-1" |> Expect.isFalse "the Session is gone"

                state.Endings |> Map.tryFind "s-1" |> Option.map fst
                |> Expect.equal "marked" (Some SessionEnding.WrongPinLimit)

                state.Challenges |> Expect.isEmpty "the challenge is gone"

                let c = Hop.credentialOf "prescriber" state
                c.WrongCount |> Expect.equal "three" 3
                c.LockedUntil |> Expect.equal "a minute" (Some(t0 + seconds 20.0 + minutes 1.0))

                sent.Value |> List.length |> Expect.equal "one mail" 1
                sent.Value.Head.To |> Expect.equal "to the registry's address" "prescriber@stub.example"
                sent.Value.Head.Subject |> Expect.equal "subject" "GenPRES: signing is locked"
            }

            test "the mail failing stops neither the ending nor the lock" {
                let broken (_: Mail) = raise (InvalidOperationException "smtp down")
                let state, _ = submitAt t0 (counter "id") broken ready "s-1" (submission "s-1" "0000" "k-1")
                let state, _ = submitAt t0 (counter "id") broken state "s-1" (submission "s-1" "0000" "k-2")
                let state, third = submitAt t0 (counter "id") broken state "s-1" (submission "s-1" "0000" "k-3")
                third |> Expect.equal "the limit" (SigningResponse.Refused SigningRefusal.PinLimit)
                state.Sessions |> Map.containsKey "s-1" |> Expect.isFalse "ended"
                (Hop.credentialOf "prescriber" state).LockedUntil |> Expect.isSome "locked"
            }

            test "after a relaunch: a right PIN while locked is refused and counts nothing; a wrong one pushes the lock out" {
                let send, _ = outbox ()
                let locked, _ = [ "k-1"; "k-2"; "k-3" ] |> List.fold (fun (s, at) k -> submitAt at (counter "id") send s "s-1" (submission "s-1" "0000" k) |> fst, at + seconds 10.0) (ready, t0)
                let until = (Hop.credentialOf "prescriber" locked).LockedUntil.Value

                // the relaunch: a new Session, a new challenge; the lock is the credential's
                let s2 = session "s-2" prescriber "stub-patient" None
                let relaunched = { locked with Sessions = Map.ofList [ s2 ]; Challenges = Map.ofList [ challenged "s-2" until ] }

                let state, answer = submitAt (until - seconds 30.0) (counter "id") send relaunched "s-2" (submission "s-2" "1234" "k-4")
                answer |> Expect.equal "locked" (SigningResponse.Refused(SigningRefusal.Locked until))
                (Hop.credentialOf "prescriber" state).WrongCount |> Expect.equal "not counted" 3
                state.Sessions |> Map.containsKey "s-2" |> Expect.isTrue "this Session did nothing wrong"

                let state, pushed = submitAt (until - seconds 30.0) (counter "id") send state "s-2" (submission "s-2" "0000" "k-5")
                pushed |> Expect.equal "locked longer" (SigningResponse.Refused(SigningRefusal.Locked(until - seconds 30.0 + minutes 2.0)))
                (Hop.credentialOf "prescriber" state).WrongCount |> Expect.equal "counted" 4
                state.Sessions |> Map.containsKey "s-2" |> Expect.isTrue "still open"

                // once the lock passed, the right PIN signs and zeroes the count
                let later = until - seconds 30.0 + minutes 2.0
                let state = { state with Challenges = Map.ofList [ challenged "s-2" later ] }

                match submitAt later (counter "id") send state "s-2" (submission "s-2" "1234" "k-6") with
                | state, SigningResponse.Submitted _ -> (Hop.credentialOf "prescriber" state).WrongCount |> Expect.equal "zeroed" 0
                | _, other -> failtest $"expected Submitted, got {other}"
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
        "processSigning Submit"
        [
            testAsync "without a cookie: refused, the port never asked" {
                let asked = ref false
                let submit _ _ = async { asked.Value <- true; return SigningResponse.Refused SigningRefusal.PinLimit }
                let cookie, _ = memoryCookie None
                let! answer = CompositionRoot.processSigning (fun _ _ -> async { return SigningResponse.Refused SigningRefusal.NoSession }) submit cookie (SigningCommand.Submit(submission "s-1" "1234" "k"))
                answer |> Expect.equal "refused" (SigningResponse.Refused SigningRefusal.NoSession)
                asked.Value |> Expect.isFalse "not asked"
            }

            testAsync "with a cookie: the port is asked for that Session, and the cookie stays whatever the answer" {
                let asked = ref None
                let submit sid (s: Submission) = async { asked.Value <- Some(sid, s.IdemKey); return SigningResponse.Refused SigningRefusal.PinLimit }
                let cookie, held = memoryCookie (Some "s-1")
                let! answer = CompositionRoot.processSigning (fun _ _ -> async { return SigningResponse.Refused SigningRefusal.NoSession }) submit cookie (SigningCommand.Submit(submission "s-1" "1234" "k"))
                answer |> Expect.equal "the port's answer" (SigningResponse.Refused SigningRefusal.PinLimit)
                asked.Value |> Expect.equal "for the cookie's Session" (Some("s-1", "k"))
                // the ending is told at the next GetSession and acknowledged by the close (Rule 11)
                held.Value |> Expect.equal "cookie kept" (Some "s-1")
            }

            test "the log never sees the PIN" {
                SigningCommand.Submit(submission "s-1" "1234" "k") |> SigningCommand.toString |> Expect.equal "name only" "Submit"
            }
        ]


let tests = testList "Signing PR 3" [ happyTests; refusalTests; pinTests; compositionTests ]


runTestsWithCLIArgs [] [||] tests
