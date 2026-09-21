// The session store on SQLite (plan 516), step 6a: every member of the machine returns its
// writes as values, and `callback` is split into `redeem` and `openAfterRedeem` (decision 1).
//
// Script-first draft (script-only policy) of the changes to `module Session` in
// `ServerApi.Session.fs` and to `module StubDatabase` in `ServerApi.StubAdapters.fs`, and of
// the tests those changes need. Both modules are copied here with their changes and shadow the
// built ones. No SQL yet: the tables of the launch and session rows, their loader and the
// transaction that runs a list arrive in 6b and 6c, so the SQL store still runs the order plan
// versions of a list and ignores the rest. Build first (`dotnet run Build`), then run
// `dotnet fsi SqlWrites.fsx` from this directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"
#r "nuget: Microsoft.Data.Sqlite, 10.0.12"
#I "../../../tests/Informedica.GenPRES.Server.Tests/bin/Debug/net10.0"
#r "Newtonsoft.Json.dll"
#r "Informedica.Utils.Lib.dll"
#r "Informedica.GenUNITS.Lib.dll"
#r "Informedica.GenSOLVER.Lib.dll"
#r "Informedica.GenCORE.Lib.dll"
#r "Informedica.GenFORM.Lib.dll"
#r "Informedica.GenORDER.Lib.dll"
#r "Informedica.GenPRES.Shared.dll"
#r "Informedica.GenPRES.Server.dll"
#r "Informedica.GenPRES.Server.Tests.dll"

open System
open Shared.Types
open ServerApi

module GenOrder = Informedica.GenOrder.Lib.Types
module GenForm = Informedica.GenForm.Lib.Types


// ---------------------------------------------------------------------------------------------
// The machine (→ ServerApi.Session.fs, module Session)
// ---------------------------------------------------------------------------------------------

module Session =

    /// The refusal words of `#/session?refused=<word>`; the client's `parseRefusal` reads them.
    let refusalWord refusal =
        match refusal with
        | LaunchRefusal.LaunchExpired -> "expired"
        | LaunchRefusal.LaunchSpent -> "spent"
        | LaunchRefusal.LaunchInvalid -> "invalid"
        | LaunchRefusal.NoBrowserIdentity -> "no-identity"
        | LaunchRefusal.NoRole -> "no-role"
        | LaunchRefusal.WrongActivePatient -> "wrong-patient"
        | LaunchRefusal.EnrolmentRequired -> "enrolment"


    let openedUrl = "/#/session"

    let refusedUrl refusal =
        $"/#/session?refused={refusalWord refusal}"


    /// A Session as the store holds it: what it is open on, the login it belongs to (a User
    /// has at most one open Session), the id of the version it opened with (`None` from
    /// nothing, or from a head that cannot be read), which a Submission is checked against,
    /// and when it was last seen (nothing acts on it yet; the idle and absolute lifetimes of
    /// Rule 10 are not built).
    type SessionRecord =
        {
            Opened: OpenedSession
            Login: string option
            OpenedWith: string option
            Seen: DateTime
        }


    /// A confirmation code as the store keeps it, as a mac and never as the digits: one per
    /// credential, with the address it went to, its expiry and the wrong tries so far.
    type PendingCode =
        {
            UserId: string
            MailAddress: string
            CodeMac: byte[]
            Expiry: DateTime
            Tries: int
        }


    /// One launch suspended at the PIN question: what the open needs once the PIN is set, and the public key
    /// of the browser that made it, so the Session opens on the key the supplying browser holds.
    /// No lifetime of its own: it lives as long as the code it is bound to.
    type Enrolment =
        {
            Attempt: string
            UserId: string
            Login: string
            DisplayName: string
            PatientId: string
            PublicKey: PublicKey
        }


    /// A data notice as the store holds it: one per Session, the platform's reading it was
    /// told over (`None`: unreadable), for two minutes.
    type Notice =
        {
            Nonce: string
            Data: GenForm.Patient option
            Expiry: DateTime
        }


    /// A signing challenge as the store holds it: one per Session, the digest of exactly the
    /// plan shown over the store's canonical form, whether the platform's reading stood when it
    /// was issued, for two minutes. The plan itself is never held: the submission carries it
    /// and is compared by digest.
    type Challenge =
        {
            Nonce: string
            Digest: string
            // the platform's reading at the challenge, none when it could not be read
            Reading: GenForm.Patient option
            Expiry: DateTime
        }


    /// What the adapter's write came to: landed; refused by the store because another server
    /// signed first, with the version that won; or failed.
    [<RequireQualifiedAccess>]
    type StoreOutcome =
        | Written
        | Conflict of StoredVersion
        | Failed of reason: string


    type LaunchRecord =
        {
            Nonce: string
            State: string
            PatientId: string
            PublicKey: PublicKey
            Expiry: DateTime
            Outcome: LaunchResult option
        }


    /// How a store records the end of a Session: closed by its User, or ended by the server.
    /// A supersession is no row: a newer Session of the same login tells it.
    [<RequireQualifiedAccess>]
    type StoredEnding =
        | Closed
        | Ended of SessionEnding


    /// <summary>
    /// A write the machine asks for, as a value: one case per fact the store records. Every
    /// member returns the writes of its request next to the new state and its answer; the
    /// adapter runs them in one transaction before it assigns the state, and the in-memory
    /// store runs nothing, since its state already holds them.
    /// </summary>
    type Persist =
        // an order plan version, at a commit
        | WriteVersion of GenOrder.OrderPlanVersion
        // a presented Launch, at its first presentation
        | RecordLaunch of LaunchRecord
        // what the callback of a Launch came to, once
        | RecordLaunchOutcome of nonce: string * LaunchResult * at: DateTime
        // a Session opened
        | OpenSession of sessionId: string * SessionRecord
        // what a Session opened with, at the open, at a version opened and at a commit
        | RecordOpenedWith of sessionId: string * SessionRecord * at: DateTime
        // a request from the Session
        | RecordSeen of sessionId: string * at: DateTime
        // the end of a Session that is an act
        | EndSession of sessionId: string * StoredEnding * at: DateTime
        // the User acknowledged the ending
        | AcknowledgeEnding of sessionId: string * at: DateTime


    type State =
        {
            Launches: Map<string, LaunchRecord>
            Sessions: Map<string, SessionRecord>
            Endings: Map<string, SessionEnding * DateTime>
            Credentials: Map<string, Credential>
            // the live confirmation code per person
            Codes: Map<string, PendingCode>
            // the launches suspended at the PIN question, by attempt
            Enrolments: Map<string, Enrolment>
            // every version of each patient's order plan, newest first, readable or not
            Records: Map<string, StoredVersion list>
            // the live data notice per Session
            Notices: Map<string, Notice>
            // the live challenge per Session
            Challenges: Map<string, Challenge>
            // what a Submission was answered, by Session and by the client's key, so that a
            // retry gets the same answer
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


    /// How long a confirmation code lives: a mail round trip, not a Session's idle gap. Bounds
    /// the half-finished launch too, which lives as long as its code.
    let codeLifetime = TimeSpan.FromMinutes 15.0

    /// Wrong codes before the code is void.
    let maxTries = 3

    /// How long a challenge lives: the Launch's two minutes, the time to read the modal and
    /// enter a PIN, so what is signed was checked against the platform moments ago.
    let challengeLifetime = TimeSpan.FromMinutes 2.0


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
            // an attempt lives as long as its code
            Enrolments = state.Enrolments |> Map.filter (fun _ e -> codes |> Map.containsKey e.UserId)
            Notices = state.Notices |> Map.filter (fun _ n -> now <= n.Expiry)
            Challenges = state.Challenges |> Map.filter (fun _ c -> now <= c.Expiry)
            Answered = state.Answered |> Map.filter (fun _ (_, at) -> now <= at + challengeLifetime)
        }


    let private answerOf (authorizeUrl: string -> string) (record: LaunchRecord) =
        match record.Outcome with
        | Some outcome -> outcome
        | None -> LaunchResult.RedirectTo(authorizeUrl record.State, record.State)


    let present
        (now: DateTime)
        (newId: unit -> string)
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (authorizeUrl: string -> string)
        (state: State)
        (launch, key)
        : State * LaunchResult * Persist list
        =
        let state = dropExpired now state

        match verify launch with
        | Error refusal -> state, LaunchResult.Refused refusal, []
        | Ok claims ->
            match state.Launches |> Map.tryFind claims.Nonce with
            | Some record when record.PublicKey = key -> state, answerOf authorizeUrl record, []
            | Some _ -> state, LaunchResult.Refused LaunchRefusal.LaunchSpent, []
            | None ->
                let record =
                    {
                        Nonce = claims.Nonce
                        State = newId ()
                        PatientId = claims.PatientId
                        PublicKey = key
                        Expiry = claims.Expiry
                        Outcome = None
                    }

                { state with Launches = state.Launches |> Map.add claims.Nonce record },
                answerOf authorizeUrl record,
                [ RecordLaunch record ]


    /// The patient data a Session opens on: the PatientDataPlatform's reading, the source of
    /// truth, when there is one (the adapter answers none for a reading that is no patient);
    /// without one, the patient data the head of the record was signed on, the last seen, when
    /// the head can be read; from nothing, none, so that the User enters it and a data outage
    /// does not block prescribing.
    let sessionPatient
        (patientData: string -> GenForm.Patient option)
        (patientId: string)
        (head: StoredVersion option)
        : GenForm.Patient option
        =
        patientData patientId
        |> Option.orElse (
            head
            |> Option.bind (fun h ->
                match h with
                | StoredVersion.Readable v -> Some v.Plan.Patient
                | StoredVersion.Unreadable _ -> None
            )
        )


    /// The open, one act, from whatever carried the launch this far: a LaunchRecord at the
    /// callback, an Enrolment once the PIN is set. The Session is written from the head of the
    /// record, held whatever its case, on the platform's reading, else the head's patient
    /// data; the login's other Sessions are closed and marked, so that a User has at most one
    /// open Session. A head this release cannot read is nothing to open with: the Session
    /// opens from nothing, so the next request tells that the record has a newer version, and
    /// a sign is refused against it.
    let private openWith
        (now: DateTime)
        (newId: unit -> string)
        (patientData: string -> GenForm.Patient option)
        (patientId: string)
        (key: PublicKey)
        (user: UserContext)
        (state: State)
        =
        let id = newId ()
        let head = headOf patientId state

        let opened: OpenedSession =
            {
                User = Some user
                PatientId = Some patientId
                Patient = sessionPatient patientData patientId head
                OpenedToken = Some(OpenedToken $"opened-{id}")
                KeyThumbprint = Some(PublicKey.thumbprint key)
                Head = head
            }

        let login = Some user.UserId

        let session =
            {
                Opened = opened
                Login = login
                OpenedWith =
                    head
                    |> Option.bind (fun h ->
                        match h with
                        | StoredVersion.Readable v -> Some v.Id
                        | StoredVersion.Unreadable _ -> None
                    )
                Seen = now
            }

        let superseded =
            state.Sessions
            |> Map.filter (fun sid s -> sid <> id && s.Login = login)
            |> Map.toList
            |> List.map fst

        { state with
            Sessions =
                superseded
                |> List.fold (fun m sid -> Map.remove sid m) state.Sessions
                |> Map.add id session
            Endings =
                superseded
                |> List.fold (fun m sid -> Map.add sid (SessionEnding.SupersededByLaunch, now) m) state.Endings
        },
        (id, opened),
        // the superseded Sessions need no write: the newer Session of their login tells it
        [
            OpenSession(id, session)
            RecordOpenedWith(id, session, now)
        ]


    let private recordOutcome now (record: LaunchRecord) outcome (state: State) =
        { state with Launches = state.Launches |> Map.add record.Nonce { record with Outcome = Some outcome } },
        RecordLaunchOutcome(record.Nonce, outcome, now)


    let private openSession now newId patientData (record: LaunchRecord) (standing: UserStanding) (state: State) =
        let state, (id, session), writes =
            openWith now newId patientData record.PatientId record.PublicKey standing.User state

        let state, outcome =
            recordOutcome now record (LaunchResult.Opened(id, session)) state

        state, CallbackResult.Opened(id, openedUrl), writes @ [ outcome ]


    let private refuse now (record: LaunchRecord) refusal (state: State) =
        let state, outcome = recordOutcome now record (LaunchResult.Refused refusal) state
        state, CallbackResult.Refused(refusal, refusedUrl refusal), [ outcome ]


    /// The launch suspends at the PIN question. One live code per credential: a code that
    /// still stands is reused and nothing is mailed; else a fresh code is mailed to the
    /// address the registry gave on this request. The attempt is this launch's own, with its
    /// browser's key.
    let private suspend
        (now: DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (codeMac: string -> byte[])
        (send: Mail -> unit)
        (record: LaunchRecord)
        (identity: BrowserIdentity)
        (standing: UserStanding)
        (state: State)
        =
        let userId = standing.User.UserId

        let state =
            match state.Codes |> Map.tryFind userId with
            | Some _ -> state
            | None ->
                let code = newCode ()

                let subject, body =
                    Mails.confirmationCode identity.DisplayName code (int codeLifetime.TotalMinutes)

                send
                    {
                        To = standing.MailAddress
                        Subject = subject
                        Body = body
                    }

                { state with
                    Codes =
                        state.Codes
                        |> Map.add
                            userId
                            {
                                UserId = userId
                                MailAddress = standing.MailAddress
                                CodeMac = codeMac code
                                Expiry = now + codeLifetime
                                Tries = 0
                            }
                }

        let attempt = newId ()

        let enrolment =
            {
                Attempt = attempt
                UserId = userId
                Login = identity.Login
                DisplayName = identity.DisplayName
                PatientId = record.PatientId
                PublicKey = record.PublicKey
            }

        let until = state.Codes[userId].Expiry

        let state, outcome =
            { state with Enrolments = state.Enrolments |> Map.add attempt enrolment }
            |> recordOutcome now record (LaunchResult.Enrolling attempt)

        state, CallbackResult.Enrolling(attempt, openedUrl, until), [ outcome ]


    /// What the first half of a callback came to: an answer with its writes, or the identity
    /// and the standing a launch goes on to the open with.
    [<RequireQualifiedAccess>]
    type Redeemed =
        | Answered of State * CallbackResult * Persist list
        | Identified of LaunchRecord * BrowserIdentity * UserStanding


    /// <summary>
    /// The first half of the callback: the state against the cookie, a reloaded callback
    /// answered from the launch's outcome, else the code redeemed once for the identity and
    /// the registry asked once for the Role and the active Patient. Needs the launch record
    /// and the Session its outcome names, nothing keyed by the login: a store loads those
    /// first, and the login's rows after, for `openAfterRedeem`.
    /// </summary>
    let redeem
        (now: DateTime)
        (redeemCode: string -> BrowserIdentity option)
        (standing: BrowserIdentity -> UserStanding option)
        (state: State)
        (cb: Callback)
        : Redeemed
        =
        let state = dropExpired now state

        let byState =
            state.Launches
            |> Map.toSeq
            |> Seq.map snd
            |> Seq.tryFind (fun r -> r.State = cb.State)

        let answer result = Redeemed.Answered(state, result, [])

        let invalid =
            CallbackResult.Refused(LaunchRefusal.LaunchInvalid, refusedUrl LaunchRefusal.LaunchInvalid)

        match cb.StateCookie, byState with
        | Some cookie, Some record when cookie = cb.State && cb.State <> "" ->
            match record.Outcome with
            | Some(LaunchResult.Opened(id, _)) when state.Sessions |> Map.containsKey id ->
                answer (CallbackResult.Opened(id, openedUrl))
            | Some(LaunchResult.Opened _) -> answer (CallbackResult.Superseded openedUrl)
            | Some(LaunchResult.Refused refusal) -> answer (CallbackResult.Refused(refusal, refusedUrl refusal))
            | Some(LaunchResult.Enrolling attempt) ->
                match state.Enrolments |> Map.tryFind attempt with
                | Some e -> answer (CallbackResult.Enrolling(attempt, openedUrl, state.Codes[e.UserId].Expiry))
                | None ->
                    answer (
                        CallbackResult.Refused(LaunchRefusal.EnrolmentRequired, refusedUrl LaunchRefusal.EnrolmentRequired)
                    )
            | Some(LaunchResult.RedirectTo _)
            | None ->
                let identity =
                    match cb.Error, cb.Code with
                    | None, Some code -> redeemCode code
                    | _ -> None

                match identity with
                | None -> Redeemed.Answered(refuse now record LaunchRefusal.NoBrowserIdentity state)
                | Some identity ->
                    match standing identity with
                    | None -> Redeemed.Answered(refuse now record LaunchRefusal.NoRole state)
                    | Some standing -> Redeemed.Identified(record, identity, standing)
        | _, None -> answer invalid
        | _ -> answer invalid


    /// <summary>
    /// The second half of the callback, over the login's Sessions, the credential and the
    /// patient's record: the active Patient checked, the launch suspended at the PIN question
    /// when a Prescriber's credential has no PIN, else the open.
    /// </summary>
    let openAfterRedeem
        (now: DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (codeMac: string -> byte[])
        (patientData: string -> GenForm.Patient option)
        (send: Mail -> unit)
        (record: LaunchRecord, identity: BrowserIdentity, standing: UserStanding)
        (state: State)
        : State * CallbackResult * Persist list
        =
        let state = dropExpired now state

        if standing.ActivePatientId <> Some record.PatientId then
            refuse now record LaunchRefusal.WrongActivePatient state
        elif
            standing.User.Role = UserRole.Prescriber
            && not (credentialOf standing.User.UserId state |> Credential.pinSet)
        then
            suspend now newId newCode codeMac send record identity standing state
        else
            openSession now newId patientData record standing state


    /// The callback from the IdentityProvider and the checks that follow it, the two halves
    /// over one state: the state against the cookie, the code redeemed for the identity, the
    /// registry asked for the Role and the active Patient, the credential read. A Prescriber
    /// whose credential has no PIN is not refused: the launch suspends until the PIN is set. A
    /// callback reload while the attempt stands is answered with it again; once it is gone, a
    /// relaunch is asked for.
    let callback
        (now: DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (codeMac: string -> byte[])
        (redeemCode: string -> BrowserIdentity option)
        (standing: BrowserIdentity -> UserStanding option)
        (patientData: string -> GenForm.Patient option)
        (send: Mail -> unit)
        (state: State)
        (cb: Callback)
        : State * CallbackResult * Persist list
        =
        match redeem now redeemCode standing state cb with
        | Redeemed.Answered(state, result, writes) -> state, result, writes
        | Redeemed.Identified(record, identity, standing) ->
            openAfterRedeem now newId newCode codeMac patientData send (record, identity, standing) state


    /// What a browser holding an attempt is told at GetSession: whom the launch is for and where
    /// the code went, while the attempt (that is, its code) stands; nothing once it is gone.
    let findEnrolment (now: DateTime) (attempt: string) (state: State) : State * EnrolmentPending option =
        let state = dropExpired now state

        match state.Enrolments |> Map.tryFind attempt with
        | Some e ->
            state,
            Some
                {
                    DisplayName = e.DisplayName
                    MailHint = MailHint.ofAddress state.Codes[e.UserId].MailAddress
                }
        | None -> state, None


    /// The code and every attempt bound to it, gone (the PIN was set, the code is void, or
    /// the browser gave up).
    let private dropCode (userId: string) (state: State) =
        { state with
            Codes = state.Codes |> Map.remove userId
            Enrolments = state.Enrolments |> Map.filter (fun _ e -> e.UserId <> userId)
        }


    /// The browser gave up on its attempt (CloseSession while enrolling). The code stands for
    /// any other attempt bound to it; when this was the last one it goes too, so that the next
    /// launch mails a fresh code.
    let dropEnrolment (attempt: string) (state: State) : State =
        match state.Enrolments |> Map.tryFind attempt with
        | None -> state
        | Some e ->
            let state = { state with Enrolments = state.Enrolments |> Map.remove attempt }

            if state.Enrolments |> Map.exists (fun _ o -> o.UserId = e.UserId) then
                state
            else
                dropCode e.UserId state


    /// The PIN comes back with the code. In order: the attempt (and its code) must stand; the
    /// PIN must have the format, else no try is spent; a wrong code counts, and the third voids
    /// the code for every attempt; else one act: the PIN is set with a count of zero, the code
    /// and its attempts are dropped, the User is told (at the address the registry answers
    /// now, else the one the code went to), and the launch continues to the open on the
    /// supplying attempt's key, with the Role the registry answers now and only if the
    /// launch's Patient is still the active one.
    let supplyPin
        (now: DateTime)
        (newId: unit -> string)
        (newSalt: int -> byte[])
        (codeMac: string -> byte[])
        (standing: BrowserIdentity -> UserStanding option)
        (patientData: string -> GenForm.Patient option)
        (send: Mail -> unit)
        (attempt: string)
        (code: string)
        (pin: string)
        (state: State)
        : State * SupplyPinResult * Persist list
        =
        let state = dropExpired now state

        match state.Enrolments |> Map.tryFind attempt with
        | None -> state, SupplyPinResult.Refused PinRefusal.AttemptExpired, []
        | Some e ->
            let pending = state.Codes[e.UserId]

            if not (Pin.isValid pin) then
                state, SupplyPinResult.Refused PinRefusal.PinFormat, []
            elif
                not (
                    System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(codeMac code, pending.CodeMac)
                )
            then
                let tries = pending.Tries + 1

                if tries >= maxTries then
                    dropCode e.UserId state, SupplyPinResult.Refused PinRefusal.CodeVoid, []
                else
                    { state with Codes = state.Codes |> Map.add e.UserId { pending with Tries = tries } },
                    SupplyPinResult.Refused(PinRefusal.WrongCode(maxTries - tries)),
                    []
            else
                let identity =
                    {
                        Login = e.Login
                        DisplayName = e.DisplayName
                    }

                // the registry asked again, fresh: the address for the mail, the Role re-taken
                // and the active Patient, because the registry may have moved on during the
                // wait. When it cannot answer, the code has already proved the mailbox, so it
                // settles the PIN and the launch continues on what it had.
                let fresh = standing identity

                let address =
                    fresh |> Option.map _.MailAddress |> Option.defaultValue pending.MailAddress

                let user =
                    fresh
                    |> Option.map _.User
                    |> Option.defaultValue
                        {
                            UserId = e.UserId
                            DisplayName = e.DisplayName
                            Role = UserRole.Prescriber
                        }

                let subject, body = Mails.pinSet e.DisplayName

                send
                    {
                        To = address
                        Subject = subject
                        Body = body
                    }

                let state =
                    { state with Credentials = state.Credentials |> Map.add e.UserId (Credential.withPin newSalt pin) }
                    |> dropCode e.UserId

                match fresh with
                | Some s when s.ActivePatientId <> Some e.PatientId ->
                    // the PIN is set and told; no Session opens for a Patient that is no longer
                    // the active one: a relaunch is asked for
                    state, SupplyPinResult.Refused PinRefusal.WrongActivePatient, []
                | _ ->
                    let state, (id, session), writes =
                        openWith now newId patientData e.PatientId e.PublicKey user state

                    state, SupplyPinResult.Opened(id, session), writes


    /// A request from the Session refreshes its idle clock. Applied by every member that takes
    /// the session cookie's id, `close` excepted: a close ends the Session, it does not keep
    /// it alive. Nothing to refresh when there is no such Session.
    let touch (now: DateTime) (sid: string) (state: State) : State * Persist list =
        match state.Sessions |> Map.tryFind sid with
        | Some r -> { state with Sessions = state.Sessions |> Map.add sid { r with Seen = now } }, [ RecordSeen(sid, now) ]
        | None -> state, []


    let find (now: DateTime) (id: string) (state: State) : State * SessionLookup * Persist list =
        match state.Sessions |> Map.tryFind id with
        | Some record ->
            let state, seen = touch now id state
            state, SessionLookup.Found record.Opened, seen
        | None ->
            match state.Endings |> Map.tryFind id with
            | Some(ending, _) -> state, SessionLookup.Ended ending, []
            | None -> state, SessionLookup.NotFound, []


    /// The User closes the Session, or acknowledges its ending: an open Session ends as closed
    /// and is acknowledged at once, an ended one is acknowledged; neither is told again.
    let close (now: DateTime) (id: string) (state: State) : State * Persist list =
        let writes =
            if state.Sessions |> Map.containsKey id then
                [
                    EndSession(id, StoredEnding.Closed, now)
                    AcknowledgeEnding(id, now)
                ]
            elif state.Endings |> Map.containsKey id then
                [ AcknowledgeEnding(id, now) ]
            else
                []

        { state with
            Sessions = state.Sessions |> Map.remove id
            Endings = state.Endings |> Map.remove id
        },
        writes


    /// The head of the record, when it is not the version the Session opened with: a
    /// Submission is refused as long as such a newer version exists.
    let blockedBy (record: SessionRecord) (patientId: string) (state: State) =
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


    /// For every computing request that names a Session: no Session under this id and an
    /// ending recorded for it, the ending; a Session, touched, and, when the token is the
    /// Session's own, the head compared against the version it opened with: a newer version,
    /// whose and when. The notice informs and gates nothing; the refusal at a Submission stays
    /// the only guard. An anonymous Session, one without a Patient, no head, or a token that
    /// is not the Session's: nothing to say.
    let seen
        (now: DateTime)
        (sid: string)
        (opened: OpenedToken option)
        (state: State)
        : State * RecordNotice option * Persist list
        =
        match state.Sessions |> Map.tryFind sid with
        | None -> state, state.Endings |> Map.tryFind sid |> Option.map (fst >> RecordNotice.Ended), []
        | Some record ->
            let state, writes = touch now sid state

            match record.Opened.User, record.Opened.PatientId with
            | Some _, Some patientId when opened.IsSome && opened = record.Opened.OpenedToken ->
                state, blockedBy record patientId state |> Option.map RecordNotice.NewerVersion, writes
            | _ -> state, None, writes


    /// Version `id` becomes what the Session opened with. No Session, an anonymous one or one
    /// without a Patient: nothing to open. An id the record does not hold for the Session's
    /// Patient (a stale button, a restart), and a version the release cannot read: nothing
    /// opens, the Session as it is; the next request tells what the head is. The version
    /// already open: the token stands. Another version: the OpenedToken is re-minted over it
    /// and the standing challenge and notice of this Session are dropped (a challenge over the
    /// old baseline must not be answerable). Any readable version may be opened; one that is
    /// not the head leaves Submission blocked.
    let openVersion
        (now: DateTime)
        (newId: unit -> string)
        (sid: string)
        (id: string)
        (state: State)
        : State * OpenedSession option * Persist list
        =
        match state.Sessions |> Map.tryFind sid with
        | None -> state, None, []
        | Some record ->
            let state, seen = touch now sid state

            match record.Opened.User, record.Opened.PatientId with
            | None, _
            | _, None -> state, None, seen
            | Some _, Some patientId ->
                let version =
                    state.Records
                    |> Map.tryFind patientId
                    |> Option.bind (
                        List.tryPick (fun v ->
                            match v with
                            | StoredVersion.Readable v when v.Id = id -> Some v
                            | _ -> None
                        )
                    )

                match version with
                | None -> state, Some record.Opened, seen
                | Some version when record.OpenedWith = Some id ->
                    let opened = { record.Opened with Head = Some(StoredVersion.Readable version) }
                    let session = { state.Sessions[sid] with Opened = opened }

                    { state with Sessions = state.Sessions |> Map.add sid session },
                    Some opened,
                    seen @ [ RecordOpenedWith(sid, session, now) ]
                | Some version ->
                    let opened =
                        { record.Opened with
                            OpenedToken = Some(OpenedToken $"opened-{newId ()}")
                            Head = Some(StoredVersion.Readable version)
                        }

                    let session =
                        { state.Sessions[sid] with
                            Opened = opened
                            OpenedWith = Some id
                        }

                    { state with
                        Sessions = state.Sessions |> Map.add sid session
                        Challenges = state.Challenges |> Map.remove sid
                        Notices = state.Notices |> Map.remove sid
                    },
                    Some opened,
                    seen @ [ RecordOpenedWith(sid, session, now) ]


    /// An order appears once in a plan.
    let duplicateOrders (plan: GenOrder.OrderPlan) =
        plan
        |> Informedica.GenOrder.Lib.OrderPlan.orders
        |> Array.countBy _.Order.Id
        |> Array.exists (fun (_, n) -> n > 1)


    /// The challenge request, checked in order: the Session with a User and a Patient; the
    /// Role Prescriber; the OpenedToken this Session holds; the patient data re-read: when it
    /// is not what the Session opened with and no notice over this reading was accepted, no
    /// challenge yet but a data notice, replacing any earlier notice and dropping any earlier
    /// challenge (it was over the data before the change); the head readable and not moved
    /// on. Then a challenge over the digest of exactly this plan, replacing the Session's
    /// earlier one and spending the notice. The plan is the domain's, parsed at the boundary;
    /// its own patient data is what the User saw, entered or read, and is recorded as such;
    /// the Patient is the Session's, never the request's. The PIN is not involved: a refusal
    /// here costs no attempt.
    let challenge
        (now: DateTime)
        (newId: unit -> string)
        (digest: GenOrder.OrderPlan -> string)
        (patientData: string -> GenForm.Patient option)
        (sid: string)
        (plan: GenOrder.OrderPlan, opened: OpenedToken, notice: string option)
        (state: State)
        : State * SigningOutcome * Persist list
        =
        let state, seen = dropExpired now state |> touch now sid
        let refuse refusal = state, SigningOutcome.Refused refusal, seen

        match state.Sessions |> Map.tryFind sid with
        | None -> refuse SigningRefusal.NoSession
        | Some record ->
            match record.Opened.User, record.Opened.PatientId with
            | None, _ -> refuse SigningRefusal.NotPrescriber
            | Some _, None -> refuse SigningRefusal.NoPatient
            | Some user, Some patientId ->
                if user.Role <> UserRole.Prescriber then
                    refuse SigningRefusal.NotPrescriber
                elif record.Opened.OpenedToken <> Some opened then
                    refuse SigningRefusal.StaleToken
                else
                    // read again, as at the open; the adapter answers none for a reading that
                    // is no patient
                    let current = patientData patientId

                    let accepted =
                        notice
                        |> Option.bind (fun token ->
                            state.Notices
                            |> Map.tryFind sid
                            |> Option.filter (fun n -> n.Nonce = token && n.Data = current)
                        )

                    // no reading, or another than the Session opened on: told before the challenge
                    if (current.IsNone || current <> record.Opened.Patient) && accepted.IsNone then
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
                            // a challenge over the data before the change must not be signed
                            Challenges = state.Challenges |> Map.remove sid
                        },
                        SigningOutcome.DataNotice(nonce, current),
                        seen
                    // no challenge over a plan that names an order twice
                    elif duplicateOrders plan then
                        refuse SigningRefusal.ChallengeMismatch
                    else
                        match unreadableHead patientId state, blockedBy record patientId state with
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
                            SigningOutcome.ChallengeIssued nonce,
                            seen


    /// The commit of a signature, one act, checked in order: the Session with a User and a
    /// Patient; the answer already given to this Session's key; the Role re-taken from the
    /// registry (fails closed when it cannot answer; Rule 38's bounded grace is not built);
    /// the OpenedToken this Session holds; the head readable and not moved on; the challenge
    /// this Session was issued, over exactly this plan by digest; and last the PIN, so that a
    /// Submission that was never going to land costs no attempt. A pure function: on an
    /// accepted sign the returned state already holds the new head, with the challenge spent,
    /// the OpenedToken re-minted over it and the Session's patient set to the reading at the
    /// challenge, else the data signed, and the third value is the write for the adapter to
    /// run; on a refusal it is none. The answer is remembered under the key, refusals too.
    /// Three wrong PINs end the Session (`WrongPinLimit`), lock signing and mail the User; a
    /// wrong PIN while locked pushes the lock out; a right PIN while locked is refused and
    /// counts nothing.
    let commit
        (now: DateTime)
        (newId: unit -> string)
        (digest: GenOrder.OrderPlan -> string)
        (standing: BrowserIdentity -> UserStanding option)
        (send: Mail -> unit)
        (sid: string)
        (signature: Signature)
        (state: State)
        : State * SigningOutcome * Persist list
        =
        let state, seen = dropExpired now state |> touch now sid

        let refuse refusal =
            state, SigningOutcome.Refused refusal, seen

        match state.Sessions |> Map.tryFind sid with
        | None -> refuse SigningRefusal.NoSession
        | Some record ->
            match record.Opened.User, record.Opened.PatientId with
            | None, _ -> refuse SigningRefusal.NotPrescriber
            | Some _, None -> refuse SigningRefusal.NoPatient
            | Some user, Some patientId ->
                match state.Answered |> Map.tryFind (sid, signature.IdemKey) with
                | Some(answer, _) -> state, answer, seen
                | None ->
                    // the answer is remembered from here on, under this Session and the key
                    let remember (state: State) answer writes =
                        { state with Answered = state.Answered |> Map.add (sid, signature.IdemKey) (answer, now) },
                        answer,
                        seen @ writes

                    let refuse refusal =
                        remember state (SigningOutcome.Refused refusal) []

                    let identity =
                        {
                            Login = record.Login |> Option.defaultValue user.UserId
                            DisplayName = user.DisplayName
                        }

                    match standing identity with
                    | Some fresh when fresh.User.Role = UserRole.Prescriber ->
                        if record.Opened.OpenedToken <> Some signature.Opened then
                            refuse SigningRefusal.StaleToken
                        else
                            match unreadableHead patientId state, blockedBy record patientId state with
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
                                                    + (headOf patientId state
                                                       |> Option.map StoredVersion.no
                                                       |> Option.defaultValue 0)
                                                PatientId = patientId
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

                                        // the Session's patient is the platform's reading at the
                                        // challenge, else the data just signed, so a resume shows what
                                        // a relaunch would
                                        let opened =
                                            { record with
                                                Opened =
                                                    { record.Opened with
                                                        OpenedToken = Some token
                                                        Head = Some(StoredVersion.Readable version)
                                                        Patient =
                                                            challenge.Reading
                                                            |> Option.defaultValue version.Plan.Patient
                                                            |> Some
                                                    }
                                                OpenedWith = Some id
                                            }

                                        remember
                                            { state with
                                                Records =
                                                    state.Records
                                                    |> Map.change
                                                        patientId
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
                                            [
                                                WriteVersion version
                                                RecordOpenedWith(sid, opened, now)
                                            ]
                                    elif wasLocked then
                                        // this Session did nothing wrong; the lock is the credential's
                                        remember
                                            state
                                            (SigningOutcome.Refused(SigningRefusal.Locked credential.LockedUntil.Value))
                                            []
                                    elif credential |> Credential.attemptsLeft = 0 then
                                        // the wrong-PIN limit is reached now; the Session ends
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
                                                Endings =
                                                    state.Endings |> Map.add sid (SessionEnding.WrongPinLimit, now)
                                                Challenges = state.Challenges |> Map.remove sid
                                            }
                                            (SigningOutcome.Refused SigningRefusal.PinLimit)
                                            [ EndSession(sid, StoredEnding.Ended SessionEnding.WrongPinLimit, now) ]
                                    else
                                        remember
                                            state
                                            (SigningOutcome.Refused(
                                                SigningRefusal.PinWrong(credential |> Credential.attemptsLeft)
                                            ))
                                            []
                    | _ -> refuse SigningRefusal.NotPrescriber


    /// Six digits from a random source (the CSPRNG in the host).
    let newCode (randomBelow: int -> int) () = (randomBelow 1_000_000).ToString "D6"


    /// The mac of a code under the host key: what the store keeps instead of the digits.
    let codeMac (key: LaunchSeal.Key) (code: string) =
        LaunchSeal.mac key (Text.Encoding.UTF8.GetBytes code)


// ---------------------------------------------------------------------------------------------
// The port (→ ServerApi.StubAdapters.fs, module StubDatabase)
// ---------------------------------------------------------------------------------------------

module StubDatabase =

    /// The digest of an order plan: SHA-256 over the canonical form of its Dto, the
    /// serialization the store writes, so that two plans equal as domain values digest equal
    /// whatever the client's JSON looked like.
    let digest (plan: Informedica.GenOrder.Lib.Types.OrderPlan) =
        plan
        |> Informedica.GenOrder.Lib.OrderPlan.Dto.toDto
        |> Informedica.GenOrder.Lib.Canonical.serialize
        |> System.Text.Encoding.UTF8.GetBytes
        |> System.Security.Cryptography.SHA256.HashData
        |> Convert.ToHexString


    /// The in-memory store keeps the state itself: writes land by being in it.
    let persistNothing (_: Session.Persist list) = Session.StoreOutcome.Written


    /// <summary>
    /// The write phase of a request: the step run, its writes run as one, and the returned
    /// state kept only if they landed. When they did not, the state stays as it was and the
    /// request answers what `onFailure` makes of the store's outcome. A step without writes
    /// asks the store nothing.
    /// </summary>
    let runWith
        (persist: Session.Persist list -> Session.StoreOutcome)
        (onFailure: Session.StoreOutcome -> 'answer)
        (step: Session.State -> Session.State * 'answer * Session.Persist list)
        (state: Session.State)
        : Session.State * 'answer
        =
        let next, answer, writes = step state

        match writes with
        | [] -> next, answer
        | writes ->
            match persist writes with
            | Session.StoreOutcome.Written -> next, answer
            | outcome -> state, onFailure outcome


    /// A signature's writes that did not land: a conflict is another server's sign, the head
    /// changed, answered as a stale sign against the version that won; anything else
    /// `StoreFailed`, so that the next Submission is the retry.
    let submitWith persist step state =
        runWith
            persist
            (function
            | Session.StoreOutcome.Conflict winner -> SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head winner))
            | _ -> SigningOutcome.Refused SigningRefusal.StoreFailed)
            step
            state


    /// A challenge's writes that did not land: `StoreFailed`.
    let challengeWith persist step state =
        runWith persist (fun _ -> SigningOutcome.Refused SigningRefusal.StoreFailed) step state


    /// The writes of a request with no refusal to give did not land: the call fails.
    let failWith persist step state =
        runWith
            persist
            (fun outcome -> invalidOp $"the session store did not take the writes: %A{outcome}")
            step
            state


    /// <summary>
    /// Where the record lives: `load` puts a patient's order plan versions into the state
    /// before a request runs, `persist` runs the write of a commit. The in-memory store keeps
    /// the record in the state itself; the SQL store reads and writes the database.
    /// </summary>
    type RecordStore =
        {
            load: string -> Session.State -> Session.State
            persist: Session.Persist list -> Session.StoreOutcome
        }


    /// The record kept in the state: nothing to load, a write lands by being in it.
    let inMemory =
        {
            load = fun _ s -> s
            persist = persistNothing
        }


    /// The patient of the launch a callback returns to.
    let patientOfCallback (cb: Callback) (s: Session.State) =
        s.Launches
        |> Map.tryPick (fun _ r -> if r.State = cb.State then Some r.PatientId else None)


    /// The patient of an enrolment attempt.
    let patientOfAttempt (attempt: string) (s: Session.State) =
        s.Enrolments |> Map.tryFind attempt |> Option.map _.PatientId


    /// The patient of a Session.
    let patientOfSession (sid: string) (s: Session.State) =
        s.Sessions |> Map.tryFind sid |> Option.bind _.Opened.PatientId


    /// <summary>
    /// The session port over a record store: every request runs under one lock; a request
    /// that can touch a patient's record loads it first. A load that throws leaves the state
    /// as it was; a signing request answers it as a store failure, any other request fails.
    /// </summary>
    let makeSessionPortWith
        (store: RecordStore)
        (now: unit -> DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (newSalt: int -> byte[])
        (codeMac: string -> byte[])
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (idp: IdentityProviderPort)
        (registry: UserRegistryPort)
        (patientData: PatientDataPort)
        (mail: MailPort)
        (initial: Session.State)
        : SessionPort
        =
        let gate = obj ()
        let mutable state = initial

        let loaded patientOf =
            match patientOf state with
            | Some pid -> store.load pid state
            | None -> state

        let update patientOf f =
            lock
                gate
                (fun () ->
                    let next, result = failWith store.persist f (loaded patientOf)
                    state <- next
                    result
                )

        let signing patientOf f =
            lock
                gate
                (fun () ->
                    match
                        (try
                            Ok(loaded patientOf)
                         with _ ->
                             Error())
                    with
                    | Error() -> SigningOutcome.Refused SigningRefusal.StoreFailed
                    | Ok s ->
                        let next, result = f s
                        state <- next
                        result
                )

        let none _ = None

        {
            present =
                fun launch ->
                    async {
                        return update none (fun s -> Session.present (now ()) newId verify idp.authorizeUrl s launch)
                    }
            callback =
                fun cb ->
                    async {
                        return
                            update
                                (patientOfCallback cb)
                                (fun s ->
                                    Session.callback
                                        (now ())
                                        newId
                                        newCode
                                        codeMac
                                        idp.redeem
                                        registry.standing
                                        patientData.read
                                        mail.send
                                        s
                                        cb
                                )
                    }
            find = fun id -> async { return update none (Session.find (now ()) id) }
            close =
                fun id ->
                    async {
                        return
                            update
                                none
                                (fun s ->
                                    let s, writes = Session.close (now ()) id s
                                    s, (), writes
                                )
                    }
            findEnrolment =
                fun attempt ->
                    async {
                        return
                            update
                                none
                                (fun s ->
                                    let s, found = Session.findEnrolment (now ()) attempt s
                                    s, found, []
                                )
                    }
            supplyPin =
                fun attempt code pin ->
                    async {
                        return
                            update
                                (patientOfAttempt attempt)
                                (fun s ->
                                    Session.supplyPin
                                        (now ())
                                        newId
                                        newSalt
                                        codeMac
                                        registry.standing
                                        patientData.read
                                        mail.send
                                        attempt
                                        code
                                        pin
                                        s
                                )
                    }
            dropEnrolment = fun attempt -> async { return update none (fun s -> Session.dropEnrolment attempt s, (), []) }
            challenge =
                fun sid request ->
                    async {
                        return
                            signing
                                (patientOfSession sid)
                                (challengeWith
                                    store.persist
                                    (fun s -> Session.challenge (now ()) newId digest patientData.read sid request s))
                    }
            submit =
                fun sid signature ->
                    async {
                        return
                            signing
                                (patientOfSession sid)
                                (submitWith
                                    store.persist
                                    (fun s ->
                                        Session.commit
                                            (now ())
                                            newId
                                            digest
                                            registry.standing
                                            mail.send
                                            sid
                                            signature
                                            s
                                    ))
                    }
            seen = fun sid opened -> async { return update (patientOfSession sid) (Session.seen (now ()) sid opened) }
            openVersion =
                fun sid id -> async { return update (patientOfSession sid) (Session.openVersion (now ()) newId sid id) }
        }


    /// The session port over an in-memory store.
    let makeSessionPort = makeSessionPortWith inMemory


// ---------------------------------------------------------------------------------------------
// The tests (→ tests/Informedica.GenPRES.Server.Tests/SessionWritesTests.fs)
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests

module Store = Informedica.GenPRES.Server.Tests.SessionStoreTests

/// The state a port here starts from: the credentials of the stub, in this script's own
/// `Session.State`, which is not the one the built test assembly holds.
let seededHere = Session.initialState (StubCredentials.seed salts)


let ids () =
    let n = ref 0

    fun () ->
        n.Value <- n.Value + 1
        $"id-{n.Value}"


let verify = LaunchSeal.verify t0 sealKey

let authorize state = $"/authorize?state={state}"


/// A state with the Launch presented, the directory that issues codes, and the callback for a
/// login.
let presentedFor login nonce (directory: StubDirectory.Directory) newId state =
    let state, answer, _ =
        Session.present t0 newId verify authorize state (mintFor nonce "stub-patient", keyA)

    match answer with
    | LaunchResult.RedirectTo(_, st) ->
        state,
        {
            State = st
            StateCookie = Some st
            Code = Some(directory.issue login "stub-patient")
            Error = None
        }
    | other -> failtest $"expected RedirectTo, got %A{other}"


let directory () =
    StubDirectory.make (fun () -> t0) (fun () -> $"code-{Guid.NewGuid()}")


/// The callback over the machine, counting the redemptions.
let callbackCounting (d: StubDirectory.Directory) newId (count: int ref) state cb =
    let redeem code =
        count.Value <- count.Value + 1
        d.idp.redeem code

    Session.callback t0 newId (codes ()) codeMac redeem d.registry.standing StubPatientData.port.read ignore state cb


let caseName (w: Session.Persist) =
    match w with
    | Session.WriteVersion _ -> "WriteVersion"
    | Session.RecordLaunch _ -> "RecordLaunch"
    | Session.RecordLaunchOutcome _ -> "RecordLaunchOutcome"
    | Session.OpenSession _ -> "OpenSession"
    | Session.RecordOpenedWith _ -> "RecordOpenedWith"
    | Session.RecordSeen _ -> "RecordSeen"
    | Session.EndSession _ -> "EndSession"
    | Session.AcknowledgeEnding _ -> "AcknowledgeEnding"


let names writes = writes |> List.map caseName


/// An open Session of `prescriber` over the machine: the state and its id.
let opened () =
    let d = directory ()
    let newId = ids ()
    let state, cb = presentedFor "prescriber" "n-1" d newId seededHere

    match callbackCounting d newId (ref 0) state cb with
    | state, CallbackResult.Opened(sid, _), _ -> state, sid, newId
    | _, other, _ -> failtest $"expected Opened, got %A{other}"


let machineTests =
    testList
        "the machine returns its writes"
        [
            test "present: a new Launch is recorded, a repeat writes nothing" {
                let state, _, writes =
                    Session.present t0 (ids ()) verify authorize seededHere (mintFor "n-1" "stub-patient", keyA)

                match writes with
                | [ Session.RecordLaunch r ] -> r.Nonce |> Expect.equal "the Launch" "n-1"
                | other -> failtest $"expected RecordLaunch, got %A{other}"

                let _, _, again =
                    Session.present t0 (ids ()) verify authorize state (mintFor "n-1" "stub-patient", keyA)

                again |> Expect.isEmpty "a repeat writes nothing"
            }

            test "callback: an open writes the Session, what it opened with and the outcome; the code is redeemed once" {
                let d = directory ()
                let newId = ids ()
                let state, cb = presentedFor "prescriber" "n-1" d newId seededHere
                let count = ref 0
                let state, answer, writes = callbackCounting d newId count state cb

                match answer with
                | CallbackResult.Opened(sid, _) ->
                    writes
                    |> names
                    |> Expect.equal "the writes" [ "OpenSession"; "RecordOpenedWith"; "RecordLaunchOutcome" ]

                    match writes with
                    | [ Session.OpenSession(a, _); Session.RecordOpenedWith(b, _, _); Session.RecordLaunchOutcome(nonce, LaunchResult.Opened(c, _), _) ] ->
                        [ a; b; c ] |> Expect.equal "one Session named" [ sid; sid; sid ]
                        nonce |> Expect.equal "the Launch's outcome" "n-1"
                    | other -> failtest $"%A{other}"

                    count.Value |> Expect.equal "redeemed once" 1

                    let _, reload, again = callbackCounting d newId count state cb
                    reload |> Expect.equal "a reload is answered as the first time" answer
                    again |> Expect.isEmpty "a reload writes nothing"
                    count.Value |> Expect.equal "and redeems nothing" 1
                | other -> failtest $"expected Opened, got %A{other}"
            }

            test "callback: the two halves answer as the callback does" {
                let d = directory ()
                let newId = ids ()
                let state, cb = presentedFor "prescriber" "n-1" d newId seededHere

                match Session.redeem t0 d.idp.redeem d.registry.standing state cb with
                | Session.Redeemed.Identified(record, identity, standing) ->
                    let _, answer, writes =
                        Session.openAfterRedeem
                            t0
                            newId
                            (codes ())
                            codeMac
                            StubPatientData.port.read
                            ignore
                            (record, identity, standing)
                            state

                    match answer with
                    | CallbackResult.Opened _ ->
                        writes |> names |> Expect.contains "the outcome is written" "RecordLaunchOutcome"
                    | other -> failtest $"expected Opened, got %A{other}"
                | other -> failtest $"expected Identified, got %A{other}"
            }

            test "callback: a refusal writes the outcome only" {
                let d = directory ()
                let newId = ids ()
                let state, cb = presentedFor "prescriber" "n-1" d newId seededHere

                let _, answer, writes =
                    callbackCounting d newId (ref 0) state { cb with Code = None; Error = Some "no-identity" }

                match answer, writes with
                | CallbackResult.Refused(LaunchRefusal.NoBrowserIdentity, _),
                  [ Session.RecordLaunchOutcome(_, LaunchResult.Refused LaunchRefusal.NoBrowserIdentity, _) ] -> ()
                | other -> failtest $"%A{other}"
            }

            test "callback: a newer open of the same login writes no ending for the older" {
                let d = directory ()
                let newId = ids ()
                let state, cb = presentedFor "prescriber" "n-1" d newId seededHere
                let state, _, _ = callbackCounting d newId (ref 0) state cb
                let state, cb2 = presentedFor "prescriber" "n-2" d newId state
                let _, _, writes = callbackCounting d newId (ref 0) state cb2

                writes |> names |> Expect.equal "the open only" [ "OpenSession"; "RecordOpenedWith"; "RecordLaunchOutcome" ]
            }

            test "find writes a heartbeat; close ends and acknowledges; an unknown id writes nothing" {
                let state, sid, _ = opened ()
                let state, _, found = Session.find t0 sid state
                found |> Expect.equal "the heartbeat" [ Session.RecordSeen(sid, t0) ]

                let state, closed = Session.close t0 sid state

                closed
                |> Expect.equal
                    "closed and acknowledged"
                    [
                        Session.EndSession(sid, Session.StoredEnding.Closed, t0)
                        Session.AcknowledgeEnding(sid, t0)
                    ]

                let _, again = Session.close t0 sid state
                again |> Expect.isEmpty "nothing left to close"

                let _, _, unknown = Session.find t0 "nobody" state
                unknown |> Expect.isEmpty "no Session, no heartbeat"
            }

            test "commit: a sign writes the heartbeat, the version and what the Session opened with" {
                let state, sid, newId = opened ()

                let token =
                    state.Sessions[sid].Opened.OpenedToken.Value

                let state, challenge, _ =
                    Session.challenge
                        t0
                        newId
                        StubDatabase.digest
                        StubPatientData.port.read
                        sid
                        (Store.domainPlan.Value, token, None)
                        state

                let nonce =
                    match challenge with
                    | SigningOutcome.ChallengeIssued n -> n
                    | other -> failtest $"expected ChallengeIssued, got %A{other}"

                let signature pin key : Signature =
                    {
                        Plan = Store.domainPlan.Value
                        Opened = token
                        Challenge = nonce
                        Pin = pin
                        IdemKey = key
                    }

                let _, answer, writes =
                    Session.commit t0 newId StubDatabase.digest Store.registry ignore sid (signature "1234" "k-1") state

                match answer with
                | SigningOutcome.Submitted _ ->
                    writes |> names |> Expect.equal "the writes" [ "RecordSeen"; "WriteVersion"; "RecordOpenedWith" ]
                | other -> failtest $"expected Submitted, got %A{other}"

                // three wrong PINs: the last ends the Session
                let wrong (state, _, _) key =
                    Session.commit t0 newId StubDatabase.digest Store.registry ignore sid (signature "9999" key) state

                let _, last, writes = [ "w-2"; "w-3" ] |> List.fold wrong (wrong (state, (), []) "w-1")
                last |> Expect.equal "the limit" (SigningOutcome.Refused SigningRefusal.PinLimit)

                writes
                |> List.filter (function
                    | Session.EndSession _ -> true
                    | _ -> false)
                |> Expect.equal "the ending" [ Session.EndSession(sid, Session.StoredEnding.Ended SessionEnding.WrongPinLimit, t0) ]
            }
        ]


/// A port with the clock at t0 over a store.
let portOver (store: StubDatabase.RecordStore) =
    let d = directory ()

    let port =
        StubDatabase.makeSessionPortWith
            store
            (fun () -> t0)
            (ids ())
            (codes ())
            salts
            codeMac
            verify
            d.idp
            d.registry
            StubPatientData.port
            (StubMail.make ()).port
            seededHere

    port, d


let openVia (port: SessionPort) (d: StubDirectory.Directory) =
    async {
        match! port.present (mintFor "n-1" "stub-patient", keyA) with
        | LaunchResult.RedirectTo(_, st) ->
            match!
                port.callback
                    {
                        State = st
                        StateCookie = Some st
                        Code = Some(d.issue "prescriber" "stub-patient")
                        Error = None
                    }
            with
            | CallbackResult.Opened(sid, _) -> return sid
            | other -> return failtest $"expected Opened, got %A{other}"
        | other -> return failtest $"expected RedirectTo, got %A{other}"
    }


let portTests =
    testList
        "the port runs the writes"
        [
            testAsync "writes that do not land: find fails, challenge and submit answer StoreFailed, the state as it was" {
                let failing = ref false

                let store =
                    { StubDatabase.inMemory with
                        persist =
                            fun writes ->
                                if failing.Value then
                                    Session.StoreOutcome.Failed "the database is gone"
                                else
                                    StubDatabase.inMemory.persist writes
                    }

                let port, d = portOver store
                let! sid = openVia port d

                let! opened =
                    async {
                        match! port.find sid with
                        | SessionLookup.Found o -> return o
                        | other -> return failtest $"expected Found, got %A{other}"
                    }

                failing.Value <- true

                let! found = Async.Catch(port.find sid)

                match found with
                | Choice2Of2 _ -> ()
                | Choice1Of2 other -> failtest $"the call should fail, got %A{other}"

                let! challenge = port.challenge sid (Store.domainPlan.Value, opened.OpenedToken.Value, None)
                challenge |> Expect.equal "challenge" (SigningOutcome.Refused SigningRefusal.StoreFailed)

                failing.Value <- false

                match! port.find sid with
                | SessionLookup.Found again -> again |> Expect.equal "the Session as it was" opened
                | other -> failtest $"expected Found, got %A{other}"
            }

            testAsync "the in-memory store signs as before" {
                let port, d = portOver StubDatabase.inMemory
                let! sid = openVia port d

                let! opened =
                    async {
                        match! port.find sid with
                        | SessionLookup.Found o -> return o
                        | other -> return failtest $"expected Found, got %A{other}"
                    }

                match! port.challenge sid (Store.domainPlan.Value, opened.OpenedToken.Value, None) with
                | SigningOutcome.ChallengeIssued nonce ->
                    let! answer =
                        port.submit
                            sid
                            {
                                Plan = Store.domainPlan.Value
                                Opened = opened.OpenedToken.Value
                                Challenge = nonce
                                Pin = "1234"
                                IdemKey = "k-1"
                            }

                    match answer with
                    | SigningOutcome.Submitted(v, _) -> v.No |> Expect.equal "version 1" 1
                    | other -> failtest $"expected Submitted, got %A{other}"
                | other -> failtest $"expected ChallengeIssued, got %A{other}"
            }
        ]


runTestsWithCLIArgs [] [||] (testList "writes as values" [ machineTests; portTests ])
