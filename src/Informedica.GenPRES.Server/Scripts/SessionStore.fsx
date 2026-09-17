// Step 5.1 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #773):
// the session state on domain values, the write path an inbound path.
//
//   - The records hold `StoredVersion` values: a version parsed at load, or an unreadable
//     entry that keeps the row's identity and the reason. The head is the newest whatever its
//     case; a sign is refused while the head is unreadable.
//   - A challenge keeps the digest of the order plan over its canonical form, never the plan.
//   - The signing command handler parses the plan of a challenge and of a submission with
//     `ofModel` then `OrderPlan.Dto.fromDto` and refuses on an error, so the session service
//     and the port see domain values only.
//   - `commit` stays pure and returns the write as a value; the adapter runs it under its lock
//     and assigns the state only if the write landed, else answers `StoreFailed`; a violated
//     `(patient_id, no)` constraint is another server's sign, answered as a stale sign.
//
// What moves to 5.2: the notice's data and the patient data port on the domain's patient, the
// session record on domain values. Until then the session record keeps the contract model's
// `SessionOpened`, so the service takes `toSigned` where it writes the head into it.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. Shared/Types.fs: `SigningRefusal.StoreFailed` and `SigningRefusal.PlanUnreadable`, with
//      their sentences in the client's SigningPolicy.
//   2. Ports.fs: `Signature`, `SigningOutcome`; `SessionPort.challenge` and `submit` on them.
//   3. Session.fs: `StoredVersion`, `Persist`, `StoreOutcome`; `Records`, `Challenges` and
//      `Answered` as below; `headOf`, `blockedBy`, `openVersion`, `challenge`, `commit`;
//      `openWith` writes the head through `toSigned`.
//   4. StubAdapters.fs: `digest`, the persist that does nothing, `update` running the write.
//   5. SigningCommand.fs: `processCmd` as below.
//   6. Tests: the signing suites over `Signature` and `SigningOutcome`; the builders of 5.0
//      wrap `StoredVersion.Readable`; the new tests below.
//
// Run from this directory: dotnet fsi SessionStore.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"
#load "../../../tests/Informedica.GenORDER.Tests/Scenarios.fs"

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
// 1. Ports.fs: what the signing half of the session port takes and answers
// ---------------------------------------------------------------------------

/// The signature as the session service takes it: the plan parsed, the OpenedToken the
/// Session holds, the challenge it was issued, the PIN, and the client's own key so that the
/// commit takes effect once.
type Signature =
    {
        Plan: GenOrder.OrderPlan
        Opened: OpenedToken
        Challenge: string
        Pin: string
        IdemKey: string
    }


/// The answer of the session service to a signing command, on domain values; the command
/// handler maps it to the wire's `SigningResponse`. `StoreFailed` and `PlanUnreadable` are
/// `SigningRefusal` cases once migrated; the contract model cannot change in a script.
[<RequireQualifiedAccess>]
type SigningOutcome =
    | ChallengeIssued of challenge: string
    // the data as it stands: the reading, none when it could not be read, and the token
    | DataNotice of token: string * data: Patient option
    | Submitted of GenOrder.OrderPlanVersion * OpenedToken
    | Refused of SigningRefusal
    | StoreFailed
    | PlanUnreadable


// ---------------------------------------------------------------------------
// 2. Session.fs: the store's types and the rules over them
// ---------------------------------------------------------------------------

module Store =

    /// A row the release cannot read: its identity from the plain columns, and why.
    type UnreadableVersion =
        {
            Id: string
            No: int
            PatientId: string
            Base: string option
            SignedBy: GenOrder.Signer
            SignedAt: DateTime
            Reason: string
        }


    /// A version as the record holds it once loaded: parsed, or kept by its identity when the
    /// row cannot be read, so that nothing vanishes and nothing is overtaken.
    [<RequireQualifiedAccess>]
    type StoredVersion =
        | Readable of GenOrder.OrderPlanVersion
        | Unreadable of UnreadableVersion


    module StoredVersion =

        let id =
            function
            | StoredVersion.Readable v -> v.Id
            | StoredVersion.Unreadable u -> u.Id


        let no =
            function
            | StoredVersion.Readable v -> v.No
            | StoredVersion.Unreadable u -> u.No


        /// What identifies the version to the client: whose, and when.
        let head (version: StoredVersion) : OrderPlanHead =
            match version with
            | StoredVersion.Readable v ->
                {
                    Id = v.Id
                    No = v.No
                    By = v.SignedBy |> Informedica.GenOrder.Lib.Signer.Dto.toDto |> SessionMapper.signerBack
                    SignedAt = v.SignedAt
                }
            | StoredVersion.Unreadable u ->
                {
                    Id = u.Id
                    No = u.No
                    By = u.SignedBy |> Informedica.GenOrder.Lib.Signer.Dto.toDto |> SessionMapper.signerBack
                    SignedAt = u.SignedAt
                }


    /// A signing challenge as the store holds it: one per Session, the digest of exactly the
    /// plan shown, whether the platform's reading stood at the challenge, for two minutes.
    type Challenge =
        {
            Nonce: string
            Digest: string
            // the platform's reading at the challenge, none when it could not be read
            Reading: Patient option
            Expiry: DateTime
        }


    /// The write a commit asks for, as a value: the adapter runs it.
    type Persist = WriteVersion of GenOrder.OrderPlanVersion


    /// What the adapter's write came to: landed; refused by the store because another server
    /// signed first, with the version that won; or failed.
    [<RequireQualifiedAccess>]
    type StoreOutcome =
        | Written
        | Conflict of StoredVersion
        | Failed of reason: string


    type State =
        {
            Launches: Map<string, Session.LaunchRecord>
            Sessions: Map<string, Session.SessionRecord>
            Endings: Map<string, SessionEnding * DateTime>
            Credentials: Map<string, Credential>
            Codes: Map<string, Session.PendingCode>
            Enrolments: Map<string, Session.Enrolment>
            // every version of each patient's order plan, newest first, readable or not
            Records: Map<string, StoredVersion list>
            Notices: Map<string, Session.Notice>
            Challenges: Map<string, Challenge>
            Answered: Map<string * string, SigningOutcome * DateTime>
        }


    let emptyState =
        {
            Launches = Map.empty
            Sessions = Map.empty
            Endings = Map.empty
            Credentials = Map.empty
            Codes = Map.empty
            Enrolments = Map.empty
            Records = Map.empty
            Notices = Map.empty
            Challenges = Map.empty
            Answered = Map.empty
        }


    let initialState (credentials: Map<string, Credential>) =
        { emptyState with Credentials = credentials }


    let challengeLifetime = Session.challengeLifetime


    let credentialOf (userId: string) (state: State) =
        state.Credentials |> Map.tryFind userId |> Option.defaultValue Credential.empty


    /// The newest version of a patient's record, readable or not: what a Session starts from
    /// and what a sign is checked against.
    let headOf (patientId: string) (state: State) =
        state.Records |> Map.tryFind patientId |> Option.bind List.tryHead


    let private dropExpired (now: DateTime) (state: State) =
        let codes = state.Codes |> Map.filter (fun _ c -> now <= c.Expiry)

        { state with
            Launches = state.Launches |> Map.filter (fun _ r -> now <= r.Expiry)
            Codes = codes
            Enrolments = state.Enrolments |> Map.filter (fun _ e -> codes |> Map.containsKey e.UserId)
            Notices = state.Notices |> Map.filter (fun _ n -> now <= n.Expiry)
            Challenges = state.Challenges |> Map.filter (fun _ c -> now <= c.Expiry)
            Answered = state.Answered |> Map.filter (fun _ (_, at) -> now <= at + challengeLifetime)
        }


    let touch (now: DateTime) (sid: string) (state: State) : State =
        { state with Sessions = state.Sessions |> Map.change sid (Option.map (fun r -> { r with Seen = now })) }


    /// The head of the record, when it is not the version the Session opened with: a
    /// Submission is refused as long as such a newer version exists.
    let blockedBy (record: Session.SessionRecord) (patientId: string) (state: State) =
        match headOf patientId state with
        | Some head when Some(StoredVersion.id head) <> record.OpenedWith -> Some(StoredVersion.head head)
        | _ -> None


    /// The head is a row this release cannot read: a sign is refused against it whatever base
    /// the client names, since a sign is only ever accepted against the head. Named by its
    /// identity, as a version that blocks.
    let unreadableHead (patientId: string) (state: State) =
        match headOf patientId state with
        | Some(StoredVersion.Unreadable _ as head) -> Some(StoredVersion.head head)
        | _ -> None


    let seen (now: DateTime) (sid: string) (opened: OpenedToken option) (state: State) : State * RecordNotice option =
        match state.Sessions |> Map.tryFind sid with
        | None -> state, state.Endings |> Map.tryFind sid |> Option.map (fst >> RecordNotice.Ended)
        | Some record ->
            let state = touch now sid state

            match record.Session.User, record.Session.PatientContext with
            | Some _, Some patient when opened.IsSome && opened = record.Session.OpenedToken ->
                state, blockedBy record patient.PatientId state |> Option.map RecordNotice.NewerVersion
            | _ -> state, None


    /// Version `id` becomes what the Session opened with. An id the record does not hold for
    /// the Session's Patient, and a version the release cannot read, open nothing: the Session
    /// as it is; the next request tells what the head is. The version already open: the token
    /// stands. Another: the OpenedToken is re-minted over it and the standing challenge and
    /// notice are dropped. `toSigned` writes the head as the client keeps it.
    let openVersion
        (now: DateTime)
        (newId: unit -> string)
        (toSigned: GenOrder.OrderPlanVersion -> SignedOrderPlan)
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
                    |> Option.bind (
                        List.tryPick (fun v ->
                            match v with
                            | StoredVersion.Readable v when v.Id = id -> Some v
                            | _ -> None
                        )
                    )

                match version with
                | None -> state, Some record.Session
                | Some version when record.OpenedWith = Some id ->
                    let session = { record.Session with Head = Some(toSigned version) }

                    { state with Sessions = state.Sessions |> Map.add sid { record with Session = session } },
                    Some session
                | Some version ->
                    let session =
                        { record.Session with
                            OpenedToken = Some(OpenedToken $"opened-{newId ()}")
                            Head = Some(toSigned version)
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


    /// An order appears once in a plan.
    let duplicateOrders (plan: GenOrder.OrderPlan) =
        plan
        |> Informedica.GenOrder.Lib.OrderPlan.orders
        |> Array.countBy _.Order.Id
        |> Array.exists (fun (_, n) -> n > 1)


    /// The challenge request, checked in order as before; then a challenge over the digest
    /// of exactly this plan, replacing the Session's earlier one and spending the notice. The
    /// plan is the domain's, parsed at the boundary. `digest` is the store's canonical form.
    let challenge
        (now: DateTime)
        (newId: unit -> string)
        (digest: GenOrder.OrderPlan -> string)
        (patientData: string -> Patient option)
        (sid: string)
        (plan: GenOrder.OrderPlan, opened: OpenedToken, notice: string option)
        (state: State)
        : State * SigningOutcome
        =
        let state = dropExpired now state |> touch now sid
        let refuse refusal = state, SigningOutcome.Refused refusal

        match state.Sessions |> Map.tryFind sid with
        | None -> refuse SigningRefusal.NoSession
        | Some record ->
            match record.Session.User, record.Session.PatientContext with
            | None, _ -> refuse SigningRefusal.NotPrescriber
            | Some _, None -> refuse SigningRefusal.NoPatient
            | Some user, Some patient ->
                if user.Role <> UserRole.Prescriber then
                    refuse SigningRefusal.NotPrescriber
                elif record.Session.OpenedToken <> Some opened then
                    refuse SigningRefusal.StaleToken
                else
                    let current = patientData patient.PatientId |> Patient.reading

                    let accepted =
                        notice
                        |> Option.bind (fun token ->
                            state.Notices
                            |> Map.tryFind sid
                            |> Option.filter (fun n -> n.Nonce = token && n.Data = current)
                        )

                    if (current.IsNone || current <> patient.Patient) && accepted.IsNone then
                        let nonce = newId ()

                        { state with
                            Notices =
                                state.Notices
                                |> Map.add
                                    sid
                                    {
                                        Nonce = nonce
                                        Data = current
                                        Expiry = now + challengeLifetime
                                    }
                            Challenges = state.Challenges |> Map.remove sid
                        },
                        SigningOutcome.DataNotice(nonce, current)
                    elif duplicateOrders plan then
                        refuse SigningRefusal.ChallengeMismatch
                    else
                        match unreadableHead patient.PatientId state, blockedBy record patient.PatientId state with
                        | Some head, _
                        | None, Some head -> refuse (SigningRefusal.Blocked head)
                        | None, None ->
                            let nonce = newId ()

                            { state with
                                Notices = state.Notices |> Map.remove sid
                                Challenges =
                                    state.Challenges
                                    |> Map.add
                                        sid
                                        {
                                            Nonce = nonce
                                            Digest = digest plan
                                            Reading = current
                                            Expiry = now + challengeLifetime
                                        }
                            },
                            SigningOutcome.ChallengeIssued nonce


    /// The commit of a signature, checked in order as before, the plan compared by digest
    /// with the challenge's. A pure function: on an accepted sign the returned state already
    /// holds the new head and the third value is the write for the adapter to run; on a
    /// refusal it is none and the state is unchanged but for what is remembered.
    let commit
        (now: DateTime)
        (newId: unit -> string)
        (digest: GenOrder.OrderPlan -> string)
        (toSigned: GenOrder.OrderPlanVersion -> SignedOrderPlan)
        (standing: BrowserIdentity -> UserStanding option)
        (send: Mail -> unit)
        (sid: string)
        (signature: Signature)
        (state: State)
        : State * SigningOutcome * Persist option
        =
        let state = dropExpired now state |> touch now sid
        let refuse refusal = state, SigningOutcome.Refused refusal, None

        match state.Sessions |> Map.tryFind sid with
        | None -> refuse SigningRefusal.NoSession
        | Some record ->
            match record.Session.User, record.Session.PatientContext with
            | None, _ -> refuse SigningRefusal.NotPrescriber
            | Some _, None -> refuse SigningRefusal.NoPatient
            | Some user, Some patient ->
                match state.Answered |> Map.tryFind (sid, signature.IdemKey) with
                | Some(answer, _) -> state, answer, None
                | None ->
                    let remember (state: State) answer write =
                        { state with Answered = state.Answered |> Map.add (sid, signature.IdemKey) (answer, now) },
                        answer,
                        write

                    let refuse refusal =
                        remember state (SigningOutcome.Refused refusal) None

                    let identity =
                        {
                            Login = record.Login |> Option.defaultValue user.UserId
                            DisplayName = user.DisplayName
                        }

                    match standing identity with
                    | Some fresh when fresh.User.Role = UserRole.Prescriber ->
                        if record.Session.OpenedToken <> Some signature.Opened then
                            refuse SigningRefusal.StaleToken
                        else
                            match unreadableHead patient.PatientId state, blockedBy record patient.PatientId state with
                            | Some head, _
                            | None, Some head -> refuse (SigningRefusal.Blocked head)
                            | None, None ->
                                match state.Challenges |> Map.tryFind sid with
                                | None -> refuse SigningRefusal.ChallengeExpired
                                | Some challenge when
                                    challenge.Nonce <> signature.Challenge
                                    || challenge.Digest <> digest signature.Plan
                                    || duplicateOrders signature.Plan
                                    ->
                                    refuse SigningRefusal.ChallengeMismatch
                                | Some challenge ->
                                    let credential = credentialOf user.UserId state
                                    let wasLocked = Credential.isLocked now credential
                                    let right, credential = Credential.verify now signature.Pin credential

                                    let state =
                                        { state with Credentials = state.Credentials |> Map.add user.UserId credential }

                                    if right then
                                        let id = newId ()

                                        let version: GenOrder.OrderPlanVersion =
                                            {
                                                Id = id
                                                No =
                                                    1
                                                    + (headOf patient.PatientId state
                                                       |> Option.map StoredVersion.no
                                                       |> Option.defaultValue 0)
                                                PatientId = patient.PatientId
                                                Base = record.OpenedWith
                                                SignedBy =
                                                    {
                                                        UserId = user.UserId
                                                        DisplayName = user.DisplayName
                                                    }
                                                SignedAt = now
                                                Plan = signature.Plan
                                                Verified = challenge.Reading.IsSome
                                            }

                                        let token = OpenedToken $"opened-{newId ()}"
                                        let signed = toSigned version

                                        let opened =
                                            { record with
                                                Session =
                                                    { record.Session with
                                                        OpenedToken = Some token
                                                        Head = Some signed
                                                        PatientContext =
                                                            Some
                                                                { patient with
                                                                    Patient =
                                                                        challenge.Reading
                                                                        |> Option.defaultValue signed.Patient
                                                                        |> Some
                                                                }
                                                    }
                                                OpenedWith = Some id
                                            }

                                        remember
                                            { state with
                                                Records =
                                                    state.Records
                                                    |> Map.change
                                                        patient.PatientId
                                                        (fun versions ->
                                                            Some(
                                                                StoredVersion.Readable version
                                                                :: (versions |> Option.defaultValue [])
                                                            )
                                                        )
                                                Challenges = state.Challenges |> Map.remove sid
                                                Sessions = state.Sessions |> Map.add sid opened
                                            }
                                            (SigningOutcome.Submitted(version, token))
                                            (Some(WriteVersion version))
                                    elif wasLocked then
                                        remember
                                            state
                                            (SigningOutcome.Refused(SigningRefusal.Locked credential.LockedUntil.Value))
                                            None
                                    elif credential |> Credential.attemptsLeft = 0 then
                                        let subject, body = Mails.pinLimit user.DisplayName

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
                                                Endings =
                                                    state.Endings |> Map.add sid (SessionEnding.WrongPinLimit, now)
                                                Challenges = state.Challenges |> Map.remove sid
                                            }
                                            (SigningOutcome.Refused SigningRefusal.PinLimit)
                                            None
                                    else
                                        remember
                                            state
                                            (SigningOutcome.Refused(
                                                SigningRefusal.PinWrong(credential |> Credential.attemptsLeft)
                                            ))
                                            None
                    | _ -> refuse SigningRefusal.NotPrescriber


// ---------------------------------------------------------------------------
// 3. StubAdapters.fs: the digest, the write that does nothing, the write phase of `update`
// ---------------------------------------------------------------------------

module StubStore =

    open Store

    /// The digest of an order plan: SHA-256 over the canonical form of its Dto, the
    /// serialization the store writes, so that two plans equal as domain values digest equal.
    let digest (plan: GenOrder.OrderPlan) =
        plan
        |> Informedica.GenOrder.Lib.OrderPlan.Dto.toDto
        |> Canonical.serialize
        |> Text.Encoding.UTF8.GetBytes
        |> Security.Cryptography.SHA256.HashData
        |> Convert.ToHexString


    /// The in-memory store keeps the state itself: a write lands by being in it.
    let persistNothing (_: Persist) = StoreOutcome.Written


    /// The write phase of the adapter's state-replacing helper: the step run, its write run,
    /// and the returned state assigned only if the write landed. A failed write leaves the
    /// state unchanged and answers `StoreFailed`; a conflict is another server's sign, the
    /// head changed, answered as a stale sign against the version that won.
    let submitWith
        (persist: Persist -> StoreOutcome)
        (step: State -> State * SigningOutcome * Persist option)
        (state: State)
        : State * SigningOutcome
        =
        let next, answer, write = step state

        match write with
        | None -> next, answer
        | Some write ->
            match persist write with
            | StoreOutcome.Written -> next, answer
            | StoreOutcome.Conflict winner -> state, SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head winner))
            | StoreOutcome.Failed _ -> state, SigningOutcome.StoreFailed


// ---------------------------------------------------------------------------
// 4. SigningCommand.fs: the handler parses in, maps out
// ---------------------------------------------------------------------------

/// The signing half of the session port as the handler sees it, on domain values.
type SigningPort =
    {
        challenge: string -> GenOrder.OrderPlan * OpenedToken * string option -> Async<SigningOutcome>
        submit: string -> Signature -> Async<SigningOutcome>
    }


/// The handler's answer; `StoreFailed` and `PlanUnreadable` are refusals once the contract
/// model has the cases.
[<RequireQualifiedAccess>]
type Response =
    | Contract of SigningResponse
    | StoreFailed
    | PlanUnreadable


module SigningCommand =

    /// The outcome as the wire carries it, the version and the notice's data mapped out with
    /// the environment's demo flag.
    let toResponse (demo: bool) (outcome: SigningOutcome) =
        match outcome with
        | SigningOutcome.ChallengeIssued nonce -> Response.Contract(SigningResponse.ChallengeIssued nonce)
        | SigningOutcome.DataNotice(token, data) ->
            Response.Contract(
                SigningResponse.DataNotice
                    {
                        Data = data
                        Token = token
                    }
            )
        | SigningOutcome.Submitted(version, token) ->
            Response.Contract(
                SigningResponse.Submitted(
                    version |> Informedica.GenOrder.Lib.OrderPlanVersion.Dto.toDto |> SessionMapper.toSigned demo,
                    token
                )
            )
        | SigningOutcome.Refused refusal -> Response.Contract(SigningResponse.Refused refusal)
        | SigningOutcome.StoreFailed -> Response.StoreFailed
        | SigningOutcome.PlanUnreadable -> Response.PlanUnreadable


    /// The plan's patient and that of every context in it made at the inbound boundary, then
    /// the plan parsed into the domain; a plan the domain does not read is refused before the
    /// port is asked.
    let processCmd (demo: bool) (port: SigningPort) (cookie: SessionCookie) (cmd: Shared.Api.SigningCommand) =
        async {
            match cookie.read () with
            | None -> return Response.Contract(SigningResponse.Refused SigningRefusal.NoSession)
            | Some id ->
                let plan =
                    match cmd with
                    | Shared.Api.SigningCommand.RequestSignChallenge(plan, _, _) -> plan
                    | Shared.Api.SigningCommand.Submit submission -> submission.Plan

                if Patient.ofPlan plan |> List.exists (Patient.patient >> _.IsError) then
                    return Response.Contract(SigningResponse.Refused SigningRefusal.NoPatient)
                else
                    match plan |> OrderPlanCommand.parsePlan with
                    | Error _ -> return Response.PlanUnreadable
                    | Ok parsed ->
                        match cmd with
                        | Shared.Api.SigningCommand.RequestSignChallenge(_, opened, notice) ->
                            let! outcome = port.challenge id (parsed, opened, notice)
                            return toResponse demo outcome
                        | Shared.Api.SigningCommand.Submit submission ->
                            let! outcome =
                                port.submit
                                    id
                                    {
                                        Plan = parsed
                                        Opened = submission.Opened
                                        Challenge = submission.Challenge
                                        Pin = submission.Pin
                                        IdemKey = submission.IdemKey
                                    }

                            return toResponse demo outcome
        }


// ---------------------------------------------------------------------------
// 5. Tests
// ---------------------------------------------------------------------------

module SessionStoreTests =

    open Expecto
    open Expecto.Flip
    // after Expecto, whose FocusState has a Normal case too
    open Shared.Types
    open Store

    let t0 = DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc)
    let seconds (n: float) = TimeSpan.FromSeconds n
    let minutes (n: float) = TimeSpan.FromMinutes n

    let counter prefix =
        let n = ref 0

        fun () ->
            n.Value <- n.Value + 1
            $"{prefix}-{n.Value}"

    let stubPatient = StubPatientData.patient
    let token sid = OpenedToken $"opened-{sid}"

    let prescriber: UserContext =
        {
            UserId = "prescriber"
            DisplayName = "Stub Prescriber"
            Role = UserRole.Prescriber
        }

    let other: UserContext =
        {
            UserId = "prescriber-b"
            DisplayName = "Stub Prescriber B"
            Role = UserRole.Prescriber
        }

    let registry (identity: BrowserIdentity) : UserStanding option =
        match identity.Login with
        | "prescriber"
        | "prescriber-b" ->
            Some
                {
                    User =
                        {
                            UserId = identity.Login
                            DisplayName = identity.DisplayName
                            Role = UserRole.Prescriber
                        }
                    ActivePatientId = Some "stub-patient"
                    MailAddress = $"{identity.Login}@stub.example"
                }
        | _ -> None

    let salts (n: int) = Array.init n byte
    let seeded = initialState (StubCredentials.seed salts)

    let toSigned = Informedica.GenOrder.Lib.OrderPlanVersion.Dto.toDto >> SessionMapper.toSigned true

    /// A paracetamol suppository order from the test scenarios, as the contract model carries
    /// it, in a scenario whose order id is the one given.
    let scenarioWithOrder (id: string) : OrderScenario =
        let order =
            Scenarios.pcmSupp
            |> Medication.toOrderDto
            |> Mappers.Order.mapFromOrderToShared [| "paracetamol" |]

        Shared.Models.OrderScenario.create
            "koorts"
            "paracetamol"
            "zetpil"
            "rect"
            (Discontinuous "3-4 x/dag")
            None
            (Some "paracetamol")
            (Some "paracetamol")
            [||]
            [| "paracetamol" |]
            [| "paracetamol" |]
            [| [| Valid [| Normal "paracetamol "; Bold "240 mg" |] |] |]
            [||]
            [||]
            { order with Id = id }
            true
            false
            None
            [||]

    /// A plan over the scenarios given, each in a context of its own, as the contract model.
    let planOf (scenarios: OrderScenario[]) : OrderPlan =
        scenarios
        |> Array.mapi (fun i sc ->
            { Shared.Models.OrderContext.empty with
                Id = $"c-{i}"
                Patient = stubPatient
                Scenarios = [| sc |]
            }
        )
        |> Shared.Models.OrderPlan.create stubPatient

    let parsed (plan: OrderPlan) =
        plan
        |> OrderPlanCommand.parsePlan
        |> Result.defaultWith (fun e -> failtest $"no plan: %A{e}")

    let plan = planOf [| scenarioWithOrder "o-1"; scenarioWithOrder "o-2" |]
    let domainPlan = parsed plan

    let session sid (user: UserContext) openedWith : string * Session.SessionRecord =
        sid,
        {
            Session =
                {
                    User = Some user
                    PatientContext =
                        Some
                            {
                                PatientId = "stub-patient"
                                Patient = Some stubPatient
                            }
                    OpenedToken = Some(token sid)
                    KeyThumbprint = Some "t"
                    Head = None
                }
            Login = Some user.UserId
            OpenedWith = openedWith
            Seen = t0
        }

    let versionOf no (by: UserContext) (at: DateTime) (p: GenOrder.OrderPlan) : GenOrder.OrderPlanVersion =
        {
            Id = $"plan-{no}"
            No = no
            PatientId = "stub-patient"
            Base = if no > 1 then Some $"plan-{no - 1}" else None
            SignedBy =
                {
                    UserId = by.UserId
                    DisplayName = by.DisplayName
                }
            SignedAt = at
            Plan = p
            Verified = true
        }

    let unreadable no (by: UserContext) (at: DateTime) : StoredVersion =
        StoredVersion.Unreadable
            {
                Id = $"plan-{no}"
                No = no
                PatientId = "stub-patient"
                Base = if no > 1 then Some $"plan-{no - 1}" else None
                SignedBy =
                    {
                        UserId = by.UserId
                        DisplayName = by.DisplayName
                    }
                SignedAt = at
                Reason = "json_version 9 is newer than this release knows"
            }

    let stateOf sessions records =
        { seeded with
            Sessions = Map.ofList sessions
            Records = Map.ofList records
        }

    let ask now nonces state sid (p, opened) =
        challenge now nonces StubStore.digest StubPatientData.port.read sid (p, opened, None) state

    let signature sid pin key (p: GenOrder.OrderPlan) : Signature =
        {
            Plan = p
            Opened = token sid
            Challenge = $"c-{sid}"
            Pin = pin
            IdemKey = key
        }

    let challenged sid (at: DateTime) (p: GenOrder.OrderPlan) =
        sid,
        {
            Nonce = $"c-{sid}"
            Digest = StubStore.digest p
            Reading = Some stubPatient
            Expiry = at + challengeLifetime
        }

    let commitAt now ids state sid s =
        commit now ids StubStore.digest toSigned registry ignore sid s state

    let submitWith persist now ids state sid s =
        StubStore.submitWith persist (fun st -> commit now ids StubStore.digest toSigned registry ignore sid s st) state

    let submit state sid s =
        submitWith StubStore.persistNothing t0 (counter "id") state sid s

    let opened = session "s-1" prescriber None

    let ready =
        { stateOf [ opened ] [] with Challenges = Map.ofList [ challenged "s-1" t0 domainPlan ] }


    let tests =
        testList
            "the session store on domain values"
            [
                test "the challenge keeps the digest of the plan, never the plan" {
                    let state, answer = ask t0 (counter "n") (stateOf [ opened ] []) "s-1" (domainPlan, token "s-1")

                    answer |> Expect.equal "issued" (SigningOutcome.ChallengeIssued "n-1")

                    state.Challenges["s-1"].Digest
                    |> Expect.equal "the digest of the plan" (StubStore.digest domainPlan)
                }

                test "the commit over the plan as challenged signs: the version stores the whole plan, the write is the value returned" {
                    let ids = counter "id"
                    let state, answer, write = commitAt t0 ids ready "s-1" (signature "s-1" "1234" "k-1" domainPlan)

                    match answer, write with
                    | SigningOutcome.Submitted(version, token), Some(WriteVersion written) ->
                        version |> Expect.equal "the write is the version" written
                        version.No |> Expect.equal "first" 1
                        version.Plan |> Expect.equal "the plan as signed, contexts, filter and totals" domainPlan
                        version.SignedBy.UserId |> Expect.equal "by" "prescriber"
                        version.Verified |> Expect.isTrue "the reading stood"

                        headOf "stub-patient" state
                        |> Expect.equal "the head" (Some(StoredVersion.Readable version))

                        state.Sessions["s-1"].OpenedWith |> Expect.equal "opened with it" (Some version.Id)

                        state.Sessions["s-1"].Session.Head
                        |> Expect.equal "the head as the client keeps it" (Some(toSigned version))

                        state.Sessions["s-1"].Session.OpenedToken |> Expect.equal "re-minted" (Some token)
                        state.Challenges |> Expect.isEmpty "spent"
                    | other -> failtest $"expected Submitted, got %A{other}"
                }

                test "a plan changed since the challenge is a mismatch: a context re-ordered too" {
                    let changed = parsed (planOf [| scenarioWithOrder "o-1"; scenarioWithOrder "o-3" |])
                    let reordered = parsed (planOf [| scenarioWithOrder "o-2"; scenarioWithOrder "o-1" |])

                    for p in [ changed; reordered; { domainPlan with Filtered = [| "c-0" |] } ] do
                        let _, answer, write = commitAt t0 (counter "id") ready "s-1" (signature "s-1" "1234" "k-1" p)

                        answer
                        |> Expect.equal "mismatch" (SigningOutcome.Refused SigningRefusal.ChallengeMismatch)

                        write |> Expect.isNone "nothing to write"
                }

                test "two plans equal as domain values digest equal; a different plan does not" {
                    let again = parsed plan

                    let roundTripped =
                        domainPlan
                        |> Informedica.GenOrder.Lib.OrderPlan.Dto.toDto
                        |> Canonical.serialize
                        |> Canonical.deserialize<Informedica.GenOrder.Lib.OrderPlan.Dto.Dto>
                        |> Informedica.GenOrder.Lib.OrderPlan.Dto.fromDto
                        |> Result.defaultWith (fun e -> failtest $"%A{e}")

                    StubStore.digest again |> Expect.equal "built twice" (StubStore.digest domainPlan)
                    StubStore.digest roundTripped |> Expect.equal "through the store's form" (StubStore.digest domainPlan)

                    StubStore.digest (parsed (planOf [| scenarioWithOrder "o-2"; scenarioWithOrder "o-1" |]))
                    |> Expect.notEqual "re-ordered" (StubStore.digest domainPlan)
                }

                test "the same key again: the same answer, nothing twice" {
                    let ids = counter "id"
                    let state, first = submitWith StubStore.persistNothing t0 ids ready "s-1" (signature "s-1" "1234" "k-1" domainPlan)
                    let state, again = submitWith StubStore.persistNothing (t0 + seconds 5.0) ids state "s-1" (signature "s-1" "1234" "k-1" domainPlan)

                    again |> Expect.equal "the first answer" first
                    state.Records["stub-patient"] |> List.length |> Expect.equal "one version" 1
                }

                test "a failed write leaves the state unchanged and answers StoreFailed; a retry then lands" {
                    let ids = counter "id"
                    let state, answer = submitWith (fun _ -> StoreOutcome.Failed "disk full") t0 ids ready "s-1" (signature "s-1" "1234" "k-1" domainPlan)

                    answer |> Expect.equal "store failed" SigningOutcome.StoreFailed
                    state |> Expect.equal "unchanged" ready

                    let state, retried = submitWith StubStore.persistNothing t0 ids state "s-1" (signature "s-1" "1234" "k-1" domainPlan)

                    match retried with
                    | SigningOutcome.Submitted _ -> state.Records["stub-patient"] |> List.length |> Expect.equal "landed" 1
                    | other -> failtest $"expected Submitted, got %A{other}"
                }

                test "a violated constraint is another server's sign: a stale sign against the version that won, not StoreFailed" {
                    let winner = StoredVersion.Readable(versionOf 1 other (t0 - minutes 1.0) domainPlan)
                    let state, answer = submitWith (fun _ -> StoreOutcome.Conflict winner) t0 (counter "id") ready "s-1" (signature "s-1" "1234" "k-1" domainPlan)

                    answer
                    |> Expect.equal "blocked by the winner" (SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head winner)))

                    state |> Expect.equal "unchanged" ready
                }

                test "an unreadable row is the head when it is the newest; a sign against it is refused whatever the base" {
                    let readable = StoredVersion.Readable(versionOf 1 prescriber (t0 - minutes 5.0) domainPlan)
                    let records = [ "stub-patient", [ unreadable 2 other (t0 - minutes 1.0); readable ] ]

                    let head = headOf "stub-patient" (stateOf [] records)
                    head |> Expect.equal "the newest, unreadable" (Some(unreadable 2 other (t0 - minutes 1.0)))

                    for openedWith in [ Some "plan-1"; Some "plan-2"; None ] do
                        let s = session "s-1" prescriber openedWith
                        let _, answer = ask t0 (counter "n") (stateOf [ s ] records) "s-1" (domainPlan, token "s-1")

                        answer
                        |> Expect.equal $"refused over {openedWith}" (SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head head.Value)))

                        let st = { stateOf [ s ] records with Challenges = Map.ofList [ challenged "s-1" t0 domainPlan ] }
                        let _, answer, write = commitAt t0 (counter "id") st "s-1" (signature "s-1" "1234" "k-1" domainPlan)

                        answer
                        |> Expect.equal $"refused at commit over {openedWith}" (SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head head.Value)))

                        write |> Expect.isNone "nothing written"

                    // the unreadable version cannot be opened: the Session as it is
                    let s = session "s-1" prescriber (Some "plan-1")
                    let st, answer = openVersion t0 (counter "id") toSigned "s-1" "plan-2" (stateOf [ s ] records)
                    answer |> Expect.equal "as it is" (Some (snd s).Session)
                    st.Sessions["s-1"].OpenedWith |> Expect.equal "still on the readable one" (Some "plan-1")
                }

                test "after a crash between the write and the reply the row is the head: a retry from the same base is stale, and the version opens" {
                    let written = versionOf 1 prescriber t0 domainPlan
                    let s = session "s-1" prescriber None

                    let st =
                        { stateOf [ s ] [ "stub-patient", [ StoredVersion.Readable written ] ] with
                            Challenges = Map.ofList [ challenged "s-1" t0 domainPlan ]
                        }

                    let _, answer, write = commitAt (t0 + seconds 10.0) (counter "id") st "s-1" (signature "s-1" "1234" "k-2" domainPlan)

                    answer
                    |> Expect.equal "stale" (SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head (StoredVersion.Readable written))))

                    write |> Expect.isNone "nothing written"

                    let st, opened = openVersion (t0 + seconds 10.0) (counter "id") toSigned "s-1" "plan-1" st
                    opened |> Option.bind _.Head |> Expect.equal "the version signed" (Some(toSigned written))
                    st.Sessions["s-1"].OpenedWith |> Expect.equal "opened with it" (Some "plan-1")
                }

                testAsync "the handler parses the plan in, refuses one the domain does not read before the port, and maps the answer out with the demo flag" {
                    let seen = ref None

                    let port: SigningPort =
                        {
                            challenge =
                                fun _ (p, _, _) ->
                                    seen.Value <- Some p
                                    async { return SigningOutcome.ChallengeIssued "n-1" }
                            submit =
                                fun _ s ->
                                    seen.Value <- Some s.Plan
                                    async { return SigningOutcome.Submitted(versionOf 1 prescriber t0 s.Plan, token "s-2") }
                        }

                    let cookie: SessionCookie =
                        {
                            read = fun () -> Some "s-1"
                            write = ignore
                            delete = ignore
                        }

                    let! issued =
                        SigningCommand.processCmd true port cookie (Shared.Api.SigningCommand.RequestSignChallenge(plan, token "s-1", None))

                    issued |> Expect.equal "issued" (Response.Contract(SigningResponse.ChallengeIssued "n-1"))
                    seen.Value |> Expect.equal "the plan parsed" (Some domainPlan)

                    let submission: Submission =
                        {
                            Plan = plan
                            Opened = token "s-1"
                            Challenge = "n-1"
                            Pin = "1234"
                            IdemKey = "k-1"
                        }

                    let! submitted = SigningCommand.processCmd false port cookie (Shared.Api.SigningCommand.Submit submission)

                    match submitted with
                    | Response.Contract(SigningResponse.Submitted(signed, _)) ->
                        signed.Head.Id |> Expect.equal "the version" "plan-1"
                        signed.OrderContexts |> Array.map _.DemoVersion |> Expect.equal "demo as the environment says" [| false; false |]
                        signed.OrderContexts |> Array.map _.Id |> Expect.equal "the contexts as signed" [| "c-0"; "c-1" |]
                    | other -> failtest $"expected Submitted, got %A{other}"

                    // a plan the domain does not read: refused before the port, never stored
                    seen.Value <- None

                    let unreadable =
                        { plan with
                            OrderContexts =
                                plan.OrderContexts
                                |> Array.map (fun c ->
                                    { c with
                                        Scenarios =
                                            c.Scenarios
                                            |> Array.map (fun sc ->
                                                // a duration in a unit no order can hold
                                                let bogus: ValueUnit =
                                                    {
                                                        Value = [| "1", 1m |]
                                                        Unit = "bogus"
                                                        Group = "bogus"
                                                        Short = false
                                                        Language = ""
                                                        Json = ""
                                                    }

                                                { sc with
                                                    Order =
                                                        { sc.Order with
                                                            Duration =
                                                                { sc.Order.Duration with
                                                                    Variable = { sc.Order.Duration.Variable with Vals = Some bogus }
                                                                }
                                                        }
                                                }
                                            )
                                    }
                                )
                        }

                    let! refused = SigningCommand.processCmd true port cookie (Shared.Api.SigningCommand.RequestSignChallenge(unreadable, token "s-1", None))
                    refused |> Expect.equal "unreadable" Response.PlanUnreadable
                    seen.Value |> Expect.isNone "the port never asked"

                    let! refusedAtCommit = SigningCommand.processCmd true port cookie (Shared.Api.SigningCommand.Submit { submission with Plan = unreadable })
                    refusedAtCommit |> Expect.equal "unreadable at commit" Response.PlanUnreadable
                    seen.Value |> Expect.isNone "the port never asked"
                }
            ]


SessionStoreTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore
