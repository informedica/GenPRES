// UC-2 enrolment against server-hosted stubs (plan 615), PR 2: the launch suspends at the PIN
// question and continues when the PIN is supplied.
//
// Script-first draft (script-only policy) of:
//   - the wire: `EnrolmentPending`, `PinRefusal` → `Shared/Types.fs`;
//     `SessionCommand.SupplyPin`, `SessionResponse.EnrolmentPending | PinRefused` → `Shared/Api.fs`;
//   - the ports: `LaunchResult.Enrolling`, `CallbackResult.Enrolling`, `SupplyPinResult`,
//     `EnrolmentCookie`, `SessionPort.supplyPin / findEnrolment / dropEnrolment` → `Ports.fs`;
//   - `Pin.isValid`, `MailHint`, the two mails → `Adapters.fs`;
//   - `Hop`: `PendingCode` (one per credential, Rule 37), `Enrolment` (one per launch, with the
//     key of the browser that made it), the suspended callback, `findEnrolment`, `supplyPin` as
//     one act (Rules 37, 40, 28, 27), `dropEnrolment` → `Adapters.fs`;
//   - `sessionDisabled` refusing → `Adapters.fs`;
//   - the composition root over the enrolment cookie → `CompositionRoot.fs`.
//
// Review points folded in (#616): the code is per credential and the attempt per launch, so
// the Session opens on the key of the browser that supplied the PIN; the third wrong code
// answers a terminal `CodeVoid`; an attempt has no lifetime of its own, it lives as long as
// its code.
//
// PR 1 (merged, #617) put the credential store and the mail stub in place; this script
// re-states `Hop` over them. Run: `dotnet fsi Enrolment.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models
open Shared.Api
open ServerApi


// ---------------------------------------------------------------------------------------------
// Wire (→ Shared/Types.fs, Shared/Api.fs)
// ---------------------------------------------------------------------------------------------

/// What the client learns when its launch is waiting on a PIN (UC-2): whom to greet and where
/// the confirmation code went, hinted so that a shoulder cannot read the address.
type EnrolmentPending =
    {
        DisplayName: string
        MailHint: string
    }


/// Why a supplied PIN did not set (UC-2 ext 2b). `WrongCode` leaves the form open with the
/// tries left; `CodeVoid` and `AttemptExpired` are terminal: the launch has to start over.
[<RequireQualifiedAccess>]
type PinRefusal =
    | WrongCode of attemptsLeft: int
    | CodeVoid
    | AttemptExpired
    | PinFormat


[<RequireQualifiedAccess>]
type SessionCommand =
    | GetSession
    | CloseSession
    // UC-2: the confirmation code from the mail and the chosen PIN
    | SupplyPin of code: string * pin: string


[<RequireQualifiedAccess>]
type SessionResponse =
    | SessionResp of SessionOpened option
    | SessionClosed
    | SessionEnded of SessionEnding
    // the launch waits on a PIN (UC-2); answered to GetSession while the attempt stands
    | EnrolmentPending of EnrolmentPending
    | PinRefused of PinRefusal


// ---------------------------------------------------------------------------------------------
// Ports (→ ServerApi.Ports.fs)
// ---------------------------------------------------------------------------------------------

[<RequireQualifiedAccess>]
type LaunchResult =
    | Opened of sessionId: string * SessionOpened
    | RedirectTo of url: string * state: string
    | Refused of LaunchRefusal
    // the launch suspended into enrolment (UC-2); the browser holds the attempt in a cookie
    | Enrolling of attemptId: string


[<RequireQualifiedAccess>]
type CallbackResult =
    | Opened of sessionId: string * redirect: string
    | Refused of LaunchRefusal * redirect: string
    | Superseded of redirect: string
    // UC-2: the attempt for the enrolment cookie, and how long the code it is bound to lives
    | Enrolling of attemptId: string * redirect: string * until: DateTime


/// The answer to a supplied PIN: the Session that opened, or why not.
[<RequireQualifiedAccess>]
type SupplyPinResult =
    | Opened of sessionId: string * SessionOpened
    | Refused of PinRefusal


/// The enrolment cookie of one request: written at the callback with the code's expiry, read
/// at GetSession and SupplyPin, deleted when the attempt is spent or gone.
type EnrolmentCookie =
    {
        read: unit -> string option
        write: string -> DateTime -> unit
        delete: unit -> unit
    }


type SessionPort =
    {
        present: Launch * PublicKey -> Async<LaunchResult>
        callback: Callback -> Async<CallbackResult>
        find: string -> Async<SessionLookup>
        close: string -> Async<unit>
        // UC-2: what a browser holding an attempt is told
        findEnrolment: string -> Async<EnrolmentPending option>
        // UC-2: the code and the chosen PIN, for the attempt in the cookie
        supplyPin: string -> string -> string -> Async<SupplyPinResult>
        // an attempt the browser gave up on (CloseSession while enrolling)
        dropEnrolment: string -> Async<unit>
    }


// ---------------------------------------------------------------------------------------------
// Pure helpers (→ ServerApi.Adapters.fs)
// ---------------------------------------------------------------------------------------------

module Pin =

    /// Four to six digits. V8 names no format; this is the assumption plan 615 records.
    let isValid (pin: string) =
        not (isNull pin) && pin.Length >= 4 && pin.Length <= 6 && pin |> Seq.forall Char.IsAsciiDigit


module MailHint =

    /// `n***@stub.example`: enough for the User to know which mailbox, not enough for a
    /// shoulder to read the address.
    let ofAddress (address: string) =
        match address.IndexOf '@' with
        | i when i > 0 -> $"{address[0]}***{address.Substring i}"
        | _ -> "***"


/// The two mails of UC-2 (Rule 27). English only; the mail language is a later concern.
module Mails =

    let confirmationCode (displayName: string) (code: string) (minutes: int) : string * string =
        "GenPRES: your confirmation code",
        $"Hello {displayName},\n\nYour confirmation code is {code}. It is valid for {minutes} minutes. Enter it in GenPRES together with the PIN of your choice.\n\nIf you did not open GenPRES just now, somebody tried to enrol in your name; nothing was set."


    let pinSet (displayName: string) : string * string =
        "GenPRES: your PIN was set",
        $"Hello {displayName},\n\nA PIN was set for your GenPRES account just now. If that was not you, tell your administrator."


// ---------------------------------------------------------------------------------------------
// Hop, re-stated with the suspended launch (→ ServerApi.Adapters.fs)
// ---------------------------------------------------------------------------------------------

module Hop =

    open ServerApi.Hop

    /// A confirmation code as the Database keeps it (Rule 37, "the code as a mac"): one per
    /// credential, with the address it went to, its expiry and the wrong tries so far.
    type PendingCode =
        {
            UserId: string
            MailAddress: string
            CodeMac: byte[]
            Expiry: DateTime
            Tries: int
        }


    /// One suspended launch (UC-2): what the open needs once the PIN is set, and the public key
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


    type LaunchRecord =
        {
            Nonce: string
            State: string
            PatientId: string
            PublicKey: PublicKey
            Expiry: DateTime
            Outcome: LaunchResult option
        }


    type State =
        {
            Launches: Map<string, LaunchRecord>
            Sessions: Map<string, SessionRecord>
            Endings: Map<string, SessionEnding * DateTime>
            Credentials: Map<string, Credential>
            // UC-2: the live confirmation code per person
            Codes: Map<string, PendingCode>
            // UC-2: the suspended launches by attempt
            Enrolments: Map<string, Enrolment>
        }


    let emptyState =
        {
            Launches = Map.empty
            Sessions = Map.empty
            Endings = Map.empty
            Credentials = Map.empty
            Codes = Map.empty
            Enrolments = Map.empty
        }


    let initialState (credentials: Map<string, Credential>) =
        { emptyState with
            Credentials = credentials
        }


    /// How long a confirmation code lives: a mail round trip, not Rule 30's gap. Bounds the
    /// half-finished launch too (plan 615's deviation from the model).
    let codeLifetime = TimeSpan.FromMinutes 15.0

    /// Wrong codes before the code is void (ext 2b).
    let maxTries = 3


    let credentialOf (userId: string) (state: State) =
        state.Credentials |> Map.tryFind userId |> Option.defaultValue Credential.empty


    let private dropExpired (now: DateTime) (state: State) =
        let codes = state.Codes |> Map.filter (fun _ c -> now <= c.Expiry)

        { state with
            Launches = state.Launches |> Map.filter (fun _ r -> now <= r.Expiry)
            Codes = codes
            // an attempt lives as long as its code
            Enrolments = state.Enrolments |> Map.filter (fun _ e -> codes |> Map.containsKey e.UserId)
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
        : State * LaunchResult
        =
        let state = dropExpired now state

        match verify launch with
        | Error refusal -> state, LaunchResult.Refused refusal
        | Ok claims ->
            match state.Launches |> Map.tryFind claims.Nonce with
            | Some record when record.PublicKey = key -> state, answerOf authorizeUrl record
            | Some _ -> state, LaunchResult.Refused LaunchRefusal.LaunchSpent
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

                { state with
                    Launches = state.Launches |> Map.add claims.Nonce record
                },
                answerOf authorizeUrl record


    /// Step 5.7, one act (Rule 40), from whatever carried the launch this far: a LaunchRecord
    /// at the callback, an Enrolment once the PIN is set. The Session is written, the login's
    /// other Sessions are closed and marked (Rule 8).
    let private openWith
        (now: DateTime)
        (newId: unit -> string)
        (patientData: string -> Patient option)
        (patientId: string)
        (key: PublicKey)
        (user: UserContext)
        (state: State)
        =
        let id = newId ()

        let session =
            {
                User = Some user
                PatientContext =
                    Some
                        {
                            PatientId = patientId
                            Patient = patientData patientId |> Option.defaultValue Patient.empty
                        }
                OpenedToken = Some(OpenedToken $"opened-{id}")
                KeyThumbprint = Some(PublicKey.thumbprint key)
            }

        let login = Some user.UserId

        let superseded =
            state.Sessions
            |> Map.filter (fun sid s -> sid <> id && s.Login = login)
            |> Map.toList
            |> List.map fst

        { state with
            Sessions =
                superseded
                |> List.fold (fun m sid -> Map.remove sid m) state.Sessions
                |> Map.add
                    id
                    {
                        Session = session
                        Login = login
                    }
            Endings =
                superseded
                |> List.fold (fun m sid -> Map.add sid (SessionEnding.SupersededByLaunch, now) m) state.Endings
        },
        (id, session)


    let private recordOutcome (record: LaunchRecord) outcome (state: State) =
        { state with
            Launches = state.Launches |> Map.add record.Nonce { record with Outcome = Some outcome }
        }


    let private openSession now newId patientData (record: LaunchRecord) (standing: UserStanding) (state: State) =
        let state, (id, session) =
            openWith now newId patientData record.PatientId record.PublicKey standing.User state

        recordOutcome record (LaunchResult.Opened(id, session)) state, CallbackResult.Opened(id, openedUrl)


    let private refuse (record: LaunchRecord) refusal (state: State) =
        recordOutcome record (LaunchResult.Refused refusal) state, CallbackResult.Refused(refusal, refusedUrl refusal)


    /// UC-2: the launch suspends at the PIN question. One live code per credential (Rule 37,
    /// ext 2a): a code that still stands is reused and nothing is mailed; else a fresh code is
    /// mailed to the address the registry gave on this request (Rule 27). The attempt is this
    /// launch's own, with its browser's key.
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
                let subject, body = Mails.confirmationCode identity.DisplayName code (int codeLifetime.TotalMinutes)

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

        { state with
            Enrolments = state.Enrolments |> Map.add attempt enrolment
        }
        |> recordOutcome record (LaunchResult.Enrolling attempt),
        CallbackResult.Enrolling(attempt, openedUrl, until)


    /// Step 4.5 and step 5. As before, except at 5.4: a Prescriber whose credential has no PIN
    /// is not refused, the launch suspends (Rules 7, 25). A callback reload while the attempt
    /// stands is answered with it again (Rule 45); once it is gone, a relaunch is asked for.
    let callback
        (now: DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (codeMac: string -> byte[])
        (redeem: string -> BrowserIdentity option)
        (standing: BrowserIdentity -> UserStanding option)
        (patientData: string -> Patient option)
        (send: Mail -> unit)
        (state: State)
        (cb: Callback)
        : State * CallbackResult
        =
        let state = dropExpired now state

        let byState =
            state.Launches
            |> Map.toSeq
            |> Seq.map snd
            |> Seq.tryFind (fun r -> r.State = cb.State)

        let invalid =
            CallbackResult.Refused(LaunchRefusal.LaunchInvalid, refusedUrl LaunchRefusal.LaunchInvalid)

        match cb.StateCookie, byState with
        | Some cookie, Some record when cookie = cb.State && cb.State <> "" ->
            match record.Outcome with
            | Some(LaunchResult.Opened(id, _)) when state.Sessions |> Map.containsKey id ->
                state, CallbackResult.Opened(id, openedUrl)
            | Some(LaunchResult.Opened _) -> state, CallbackResult.Superseded openedUrl
            | Some(LaunchResult.Refused refusal) -> state, CallbackResult.Refused(refusal, refusedUrl refusal)
            | Some(LaunchResult.Enrolling attempt) ->
                match state.Enrolments |> Map.tryFind attempt with
                | Some e -> state, CallbackResult.Enrolling(attempt, openedUrl, state.Codes[e.UserId].Expiry)
                | None ->
                    state,
                    CallbackResult.Refused(LaunchRefusal.EnrolmentRequired, refusedUrl LaunchRefusal.EnrolmentRequired)
            | Some(LaunchResult.RedirectTo _)
            | None ->
                let identity =
                    match cb.Error, cb.Code with
                    | None, Some code -> redeem code
                    | _ -> None

                match identity with
                | None -> refuse record LaunchRefusal.NoBrowserIdentity state
                | Some identity ->
                    match standing identity with
                    | None -> refuse record LaunchRefusal.NoRole state
                    | Some standing when standing.ActivePatientId <> Some record.PatientId ->
                        refuse record LaunchRefusal.WrongActivePatient state
                    | Some standing when
                        standing.User.Role = UserRole.Prescriber
                        && not (credentialOf standing.User.UserId state |> Credential.pinSet)
                        ->
                        suspend now newId newCode codeMac send record identity standing state
                    | Some standing -> openSession now newId patientData record standing state
        | _, None -> state, invalid
        | _ -> state, invalid


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
            let state =
                { state with
                    Enrolments = state.Enrolments |> Map.remove attempt
                }

            if state.Enrolments |> Map.exists (fun _ o -> o.UserId = e.UserId) then
                state
            else
                dropCode e.UserId state


    /// UC-2, the PIN comes back with the code. In order: the attempt (and its code) must stand;
    /// the PIN must have the format, else no try is spent; a wrong code counts, and the third
    /// voids the code for every attempt (ext 2b); else one act (Rules 37, 40): the PIN is set
    /// with a count of zero (Rule 28), the code and its attempts are dropped, the User is
    /// told (Rule 27, at the address the registry answers now, else the one the code went to),
    /// and the launch continues at 5.5 to 5.7 on the supplying attempt's key.
    let supplyPin
        (now: DateTime)
        (newId: unit -> string)
        (newSalt: int -> byte[])
        (codeMac: string -> byte[])
        (standing: BrowserIdentity -> UserStanding option)
        (patientData: string -> Patient option)
        (send: Mail -> unit)
        (attempt: string)
        (code: string)
        (pin: string)
        (state: State)
        : State * SupplyPinResult
        =
        let state = dropExpired now state

        match state.Enrolments |> Map.tryFind attempt with
        | None -> state, SupplyPinResult.Refused PinRefusal.AttemptExpired
        | Some e ->
            let pending = state.Codes[e.UserId]

            if not (Pin.isValid pin) then
                state, SupplyPinResult.Refused PinRefusal.PinFormat
            elif
                not (System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(codeMac code, pending.CodeMac))
            then
                let tries = pending.Tries + 1

                if tries >= maxTries then
                    dropCode e.UserId state, SupplyPinResult.Refused PinRefusal.CodeVoid
                else
                    { state with
                        Codes = state.Codes |> Map.add e.UserId { pending with Tries = tries }
                    },
                    SupplyPinResult.Refused(PinRefusal.WrongCode(maxTries - tries))
            else
                let identity =
                    {
                        Login = e.Login
                        DisplayName = e.DisplayName
                    }

                // Rule 27: the address, asked fresh on the request that sends the mail; the
                // code's address when the registry cannot answer (uc-02, last bullet)
                let address =
                    standing identity
                    |> Option.map _.MailAddress
                    |> Option.defaultValue pending.MailAddress

                let subject, body = Mails.pinSet e.DisplayName

                send
                    {
                        To = address
                        Subject = subject
                        Body = body
                    }

                let user =
                    {
                        UserId = e.UserId
                        DisplayName = e.DisplayName
                        Role = UserRole.Prescriber
                    }

                let state =
                    { state with
                        Credentials = state.Credentials |> Map.add e.UserId (Credential.withPin newSalt pin)
                    }
                    |> dropCode e.UserId

                let state, (id, session) =
                    openWith now newId patientData e.PatientId e.PublicKey user state

                state, SupplyPinResult.Opened(id, session)


    let find (id: string) (state: State) : State * SessionLookup =
        match state.Sessions |> Map.tryFind id with
        | Some record -> state, SessionLookup.Found record.Session
        | None ->
            match state.Endings |> Map.tryFind id with
            | Some(ending, _) -> state, SessionLookup.Ended ending
            | None -> state, SessionLookup.NotFound


    let close (id: string) (state: State) : State =
        { state with
            Sessions = state.Sessions |> Map.remove id
            Endings = state.Endings |> Map.remove id
        }


    /// Six digits from a random source (the CSPRNG in the host).
    let newCode (randomBelow: int -> int) () = (randomBelow 1_000_000).ToString "D6"


    /// The mac of a code under the host key (Rule 37).
    let codeMac (key: LaunchSeal.Key) (code: string) =
        LaunchSeal.mac key (Text.Encoding.UTF8.GetBytes code)


    let makeSessionPort
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
        (initial: State)
        : SessionPort
        =
        let gate = obj ()
        let mutable state = initial

        let update f =
            lock
                gate
                (fun () ->
                    let next, result = f state
                    state <- next
                    result
                )

        {
            present =
                fun launch -> async { return update (fun s -> present (now ()) newId verify idp.authorizeUrl s launch) }
            callback =
                fun cb ->
                    async {
                        return
                            update (fun s ->
                                callback
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
            find = fun id -> async { return update (find id) }
            close = fun id -> async { return update (fun s -> close id s, ()) }
            findEnrolment = fun attempt -> async { return update (findEnrolment (now ()) attempt) }
            supplyPin =
                fun attempt code pin ->
                    async {
                        return
                            update (fun s ->
                                supplyPin
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
            dropEnrolment = fun attempt -> async { return update (fun s -> dropEnrolment attempt s, ()) }
        }


/// The production stop-gap (until #580): nothing launches, nothing enrols.
let sessionDisabled: SessionPort =
    {
        present = fun _ -> async { return LaunchResult.Refused LaunchRefusal.LaunchInvalid }
        callback =
            fun _ ->
                async {
                    return
                        CallbackResult.Refused(LaunchRefusal.LaunchInvalid, ServerApi.Hop.refusedUrl LaunchRefusal.LaunchInvalid)
                }
        find = fun _ -> async { return SessionLookup.NotFound }
        close = fun _ -> async { return () }
        findEnrolment = fun _ -> async { return None }
        supplyPin = fun _ _ _ -> async { return SupplyPinResult.Refused PinRefusal.AttemptExpired }
        dropEnrolment = fun _ -> async { return () }
    }


// ---------------------------------------------------------------------------------------------
// Composition root (→ ServerApi.CompositionRoot.fs; over `env.session` there)
// ---------------------------------------------------------------------------------------------

module CompositionRoot =

    let processLaunch (session: SessionPort) (cookie: SessionCookie) (stateCookie: LaunchStateCookie) (cmd: LaunchCommand) =
        async {
            match cmd with
            | LaunchCommand.PresentLaunch(launch, key) ->
                match! session.present (launch, key) with
                | LaunchResult.Opened(id, opened) ->
                    cookie.write id
                    return LaunchOutcome.Opened opened
                | LaunchResult.RedirectTo(url, state) ->
                    stateCookie.write state
                    return LaunchOutcome.RedirectTo url
                | LaunchResult.Refused refusal -> return LaunchOutcome.Refused refusal
                // a retry of a launch that suspended: the browser holds the attempt already, the
                // app tells it at the next GetSession
                | LaunchResult.Enrolling _ -> return LaunchOutcome.RedirectTo ServerApi.Hop.openedUrl
        }


    let processCallback
        (session: SessionPort)
        (cookie: SessionCookie)
        (stateCookie: LaunchStateCookie)
        (enrolment: EnrolmentCookie)
        (cb: Callback)
        =
        async {
            match! session.callback { cb with StateCookie = stateCookie.read cb.State } with
            | CallbackResult.Opened(id, redirect) ->
                cookie.write id
                return redirect
            | CallbackResult.Enrolling(attempt, redirect, until) ->
                enrolment.write attempt until
                return redirect
            | CallbackResult.Refused(_, redirect)
            | CallbackResult.Superseded redirect -> return redirect
        }


    /// The session commands over both cookies. GetSession answers the Session first; without
    /// one, a standing attempt; a gone attempt loses its cookie. SupplyPin works on the attempt
    /// in the cookie, never on one the client names. CloseSession drops both.
    let processSession
        (session: SessionPort)
        (cookie: SessionCookie)
        (enrolment: EnrolmentCookie)
        (cmd: SessionCommand)
        =
        async {
            match cmd with
            | SessionCommand.GetSession ->
                let! bySession =
                    async {
                        match cookie.read () with
                        | None -> return None
                        | Some id ->
                            match! session.find id with
                            | SessionLookup.Found opened -> return Some(SessionResponse.SessionResp(Some opened))
                            | SessionLookup.NotFound -> return None
                            | SessionLookup.Ended ending -> return Some(SessionResponse.SessionEnded ending)
                    }

                match bySession, enrolment.read () with
                | Some response, _ -> return response
                | None, None -> return SessionResponse.SessionResp None
                | None, Some attempt ->
                    match! session.findEnrolment attempt with
                    | Some pending -> return SessionResponse.EnrolmentPending pending
                    | None ->
                        enrolment.delete ()
                        return SessionResponse.SessionResp None
            | SessionCommand.SupplyPin(code, pin) ->
                match enrolment.read () with
                | None -> return SessionResponse.PinRefused PinRefusal.AttemptExpired
                | Some attempt ->
                    match! session.supplyPin attempt code pin with
                    | SupplyPinResult.Opened(id, opened) ->
                        enrolment.delete ()
                        cookie.write id
                        return SessionResponse.SessionResp(Some opened)
                    | SupplyPinResult.Refused(PinRefusal.CodeVoid as refusal)
                    | SupplyPinResult.Refused(PinRefusal.AttemptExpired as refusal) ->
                        enrolment.delete ()
                        return SessionResponse.PinRefused refusal
                    | SupplyPinResult.Refused refusal -> return SessionResponse.PinRefused refusal
            | SessionCommand.CloseSession ->
                try
                    match cookie.read () with
                    | Some id -> do! session.close id
                    | None -> ()

                    match enrolment.read () with
                    | Some attempt -> do! session.dropEnrolment attempt
                    | None -> ()
                finally
                    cookie.delete ()
                    enrolment.delete ()

                return SessionResponse.SessionClosed
        }


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


let sealKey = LaunchSeal.Key(Array.init LaunchSeal.keyLength byte)
let t0 = DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc)
let lifetime = TimeSpan.FromMinutes 2.0
let verifyAt (now: DateTime) = LaunchSeal.verify now sealKey
let keyA = PublicKey "key-a"
let keyB = PublicKey "key-b"
let salts (n: int) = Array.init n byte
let codeMac = Hop.codeMac sealKey

let mintFor nonce pid =
    LaunchSeal.mint
        sealKey
        {
            PatientId = pid
            Nonce = nonce
            Expiry = t0 + lifetime
        }

let launch1 = mintFor "n-1" "patient-1"

let counter prefix =
    let n = ref 0

    fun () ->
        n.Value <- n.Value + 1
        $"{prefix}-{n.Value}"


/// Codes are numbered so that a test can name the one that was mailed.
let codes () =
    let n = ref 0

    fun () ->
        n.Value <- n.Value + 1
        $"%06d{n.Value}"


type Fixture =
    {
        ids: unit -> string
        d: StubDirectory.Directory
        outbox: StubMail.Outbox
        newCode: unit -> string
    }


let fixture () =
    {
        ids = counter "id"
        d = StubDirectory.make (fun () -> t0) (counter "code")
        outbox = StubMail.make ()
        newCode = codes ()
    }


let seeded = Hop.initialState (StubCredentials.seed salts)


let hop (f: Fixture) state launch key choice =
    let state, result =
        Hop.present t0 f.ids (verifyAt t0) f.d.idp.authorizeUrl state (launch, key)

    match result with
    | LaunchResult.RedirectTo(_, st) ->
        state,
        {
            State = st
            StateCookie = Some st
            Code = Some(f.d.issue choice "patient-1")
            Error = None
        }
    | other -> failtest $"expected RedirectTo, got {other}"


let runAt now (f: Fixture) state cb =
    Hop.callback
        now
        f.ids
        f.newCode
        codeMac
        f.d.idp.redeem
        f.d.registry.standing
        StubPatientData.port.read
        f.outbox.port.send
        state
        cb


let run f state cb = runAt t0 f state cb


/// Launches `choice` and expects the suspension; returns the state and the attempt.
let suspendVia (f: Fixture) state launch key choice =
    let state, cb = hop f state launch key choice
    let state, result = run f state cb

    match result with
    | CallbackResult.Enrolling(attempt, redirect, until) ->
        redirect |> Expect.equal "to the app" "/#/session"
        until |> Expect.equal "until the code expires" (t0 + Hop.codeLifetime)
        state, attempt
    | other -> failtest $"expected Enrolling, got {other}"


let supplyAt now (f: Fixture) state attempt code pin =
    Hop.supplyPin
        now
        f.ids
        salts
        codeMac
        f.d.registry.standing
        StubPatientData.port.read
        f.outbox.port.send
        attempt
        code
        pin
        state


let supply f state attempt code pin = supplyAt t0 f state attempt code pin


/// The code the newest confirmation mail carries, from its body.
let mailedCode (f: Fixture) =
    let body =
        (f.outbox.sent () |> List.find (fun m -> m.Subject.Contains "confirmation code")).Body

    let i = body.IndexOf "code is " + 8
    body.Substring(i, 6)


let helperTests =
    testList
        "helpers"
        [
            test "a PIN is four to six digits" {
                for ok in [ "1234"; "12345"; "123456"; "0000" ] do
                    Pin.isValid ok |> Expect.isTrue ok

                for bad in [ "123"; "1234567"; "12a4"; ""; "12 34"; "١٢٣٤" ] do
                    Pin.isValid bad |> Expect.isFalse bad
            }

            test "the mail hint keeps the first letter and the domain" {
                MailHint.ofAddress "no-pin@stub.example" |> Expect.equal "hint" "n***@stub.example"
                MailHint.ofAddress "x" |> Expect.equal "no at" "***"
            }

            test "codes are six digits" {
                Hop.newCode (fun _ -> 42) () |> Expect.equal "padded" "000042"
                Hop.newCode (fun _ -> 999_999) () |> Expect.equal "max" "999999"
            }

            test "the code mac is keyed" {
                codeMac "123456" |> Expect.notEqual "another key" (Hop.codeMac (LaunchSeal.Key(Array.zeroCreate 32)) "123456")
                codeMac "123456" |> Expect.equal "deterministic" (codeMac "123456")
            }
        ]


let suspendTests =
    testList
        "the launch suspends (uc-02)"
        [
            test "a Prescriber without a PIN is not refused: an attempt and a code, one mail (Rules 7, 25, 27)" {
                let f = fixture ()
                let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                state.Sessions |> Map.isEmpty |> Expect.isTrue "no Session yet (Rule 7)"
                let e = state.Enrolments[attempt]
                e.UserId |> Expect.equal "person" "no-pin"
                e.PatientId |> Expect.equal "patient" "patient-1"
                e.PublicKey |> Expect.equal "the browser's key" keyA
                state.Codes["no-pin"].Expiry |> Expect.equal "code lifetime" (t0 + Hop.codeLifetime)
                state.Launches["n-1"].Outcome |> Expect.equal "recorded" (Some(LaunchResult.Enrolling attempt))

                match f.outbox.sent () with
                | [ mail ] ->
                    mail.To |> Expect.equal "the registry's address" "no-pin@stub.example"
                    mail.Body |> Expect.stringContains "the code" (mailedCode f)
                    mail.Body |> Expect.stringContains "the lifetime" "15 minutes"
                | other -> failtest $"expected one mail, got {other.Length}"
            }

            test "a Prescriber with a PIN, a Reader, an unknown login: as before" {
                let f = fixture ()
                let state, cb = hop f seeded launch1 keyA "prescriber"
                let state, opened = run f state cb

                match opened with
                | CallbackResult.Opened _ -> ()
                | other -> failtest $"expected Opened, got {other}"

                let state, cb = hop f state (mintFor "n-2" "patient-1") keyB "reader"
                let state, opened = run f state cb

                match opened with
                | CallbackResult.Opened _ -> ()
                | other -> failtest $"expected Opened, got {other}"

                let state, cb = hop f state (mintFor "n-3" "patient-1") keyB "unknown"
                let _, refused = run f state cb
                refused |> Expect.equal "no role" (CallbackResult.Refused(LaunchRefusal.NoRole, "/#/session?refused=no-role"))
                f.outbox.sent () |> Expect.isEmpty "no mail for any of them"
            }

            test "a second launch while the code stands gets its own attempt and no second mail (Rule 37, ext 2a)" {
                let f = fixture ()
                let state, a1 = suspendVia f seeded launch1 keyA "no-pin"
                let state, a2 = suspendVia f state (mintFor "n-2" "patient-1") keyB "no-pin"
                a2 |> Expect.notEqual "another attempt" a1
                state.Enrolments[a1].PublicKey |> Expect.equal "first keeps its key" keyA
                state.Enrolments[a2].PublicKey |> Expect.equal "second has its own" keyB
                state.Codes |> Map.count |> Expect.equal "one code" 1
                f.outbox.sent () |> List.length |> Expect.equal "one mail" 1
            }

            test "a callback reload while the attempt stands is answered with it again (Rule 45)" {
                let f = fixture ()
                let state, cb = hop f seeded launch1 keyA "no-pin"
                let state, first = run f state cb
                let state2, again = run f state cb
                again |> Expect.equal "same answer" first
                state2 |> Expect.equal "same state" state
                f.outbox.sent () |> List.length |> Expect.equal "still one mail" 1
            }

            test "a callback reload after the attempt is gone asks for a relaunch" {
                let f = fixture ()
                let state, cb = hop f seeded launch1 keyA "no-pin"
                let state, first = run f state cb

                let attempt =
                    match first with
                    | CallbackResult.Enrolling(a, _, _) -> a
                    | other -> failtest $"{other}"

                // within the Launch lifetime, the attempt dropped: enrolment, relaunch
                let state = Hop.dropEnrolment attempt state
                let _, relaunch = run f state cb
                relaunch |> Expect.equal "enrolment" (CallbackResult.Refused(LaunchRefusal.EnrolmentRequired, "/#/session?refused=enrolment"))
                // after the code's lifetime the LaunchRecord is long gone too (Rule 29): invalid
                let late = t0 + Hop.codeLifetime + TimeSpan.FromSeconds 1.0
                let _, gone = runAt late f state cb
                gone |> Expect.equal "invalid" (CallbackResult.Refused(LaunchRefusal.LaunchInvalid, "/#/session?refused=invalid"))
            }

            test "a retry of the presentation after the suspension is sent to the app" {
                let f = fixture ()
                let state, _ = suspendVia f seeded launch1 keyA "no-pin"
                let _, again = Hop.present t0 f.ids (verifyAt t0) f.d.idp.authorizeUrl state (launch1, keyA)

                match again with
                | LaunchResult.Enrolling _ -> ()
                | other -> failtest $"expected Enrolling, got {other}"
            }
        ]


let findTests =
    testList
        "findEnrolment"
        [
            test "a standing attempt tells whom and the hinted address" {
                let f = fixture ()
                let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                let _, pending = Hop.findEnrolment t0 attempt state

                pending
                |> Expect.equal
                    "pending"
                    (Some
                        {
                            DisplayName = "Stub Prescriber (no PIN)"
                            MailHint = "n***@stub.example"
                        })
            }

            test "an unknown attempt is nothing" {
                let _, pending = Hop.findEnrolment t0 "nope" seeded
                pending |> Expect.isNone "nothing"
            }

            test "after the code's lifetime the attempt is gone with it" {
                let f = fixture ()
                let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                let late = t0 + Hop.codeLifetime + TimeSpan.FromSeconds 1.0
                let state, pending = Hop.findEnrolment late attempt state
                pending |> Expect.isNone "gone"
                state.Codes |> Map.isEmpty |> Expect.isTrue "code dropped"
                state.Enrolments |> Map.isEmpty |> Expect.isTrue "attempt dropped"
            }
        ]


let supplyTests =
    testList
        "supplyPin"
        [
            test "the right code and a PIN: set with a count of zero, both dropped, told, and open on the attempt's key (Rules 37, 40, 28, 27)" {
                let f = fixture ()
                let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                let code = mailedCode f
                let state, result = supply f state attempt code "2468"

                match result with
                | SupplyPinResult.Opened(id, session) ->
                    session.User |> Option.map _.UserId |> Expect.equal "user" (Some "no-pin")
                    session.User |> Option.map _.Role |> Expect.equal "role" (Some UserRole.Prescriber)
                    session.PatientContext |> Option.map _.PatientId |> Expect.equal "patient" (Some "patient-1")
                    session.KeyThumbprint |> Expect.equal "the attempt's key" (Some(PublicKey.thumbprint keyA))
                    state.Sessions |> Map.containsKey id |> Expect.isTrue "open"
                | other -> failtest $"expected Opened, got {other}"

                let credential = state.Credentials["no-pin"]
                credential.PinHash |> Option.map (PinHash.verify "2468") |> Expect.equal "the PIN" (Some true)
                credential.WrongCount |> Expect.equal "zero" 0
                state.Codes |> Map.isEmpty |> Expect.isTrue "code dropped"
                state.Enrolments |> Map.isEmpty |> Expect.isTrue "attempt dropped"

                match f.outbox.sent () with
                | [ second; first ] ->
                    first.Subject |> Expect.stringContains "first" "confirmation code"
                    second.Subject |> Expect.stringContains "second" "PIN was set"
                    second.To |> Expect.equal "the registry's address, fresh" "no-pin@stub.example"
                | other -> failtest $"expected two mails, got {other.Length}"
            }

            test "the next launch of that person opens directly" {
                let f = fixture ()
                let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                let state, _ = supply f state attempt (mailedCode f) "2468"
                let state, cb = hop f state (mintFor "n-2" "patient-1") keyB "no-pin"
                let _, result = run f state cb

                match result with
                | CallbackResult.Opened _ -> ()
                | other -> failtest $"expected Opened, got {other}"
            }

            test "two attempts on one code: whichever supplies opens on its own key, and the other is gone" {
                let f = fixture ()
                let state, a1 = suspendVia f seeded launch1 keyA "no-pin"
                let state, a2 = suspendVia f state (mintFor "n-2" "patient-1") keyB "no-pin"
                let state, result = supply f state a2 (mailedCode f) "2468"

                match result with
                | SupplyPinResult.Opened(_, session) ->
                    session.KeyThumbprint |> Expect.equal "the second browser's key" (Some(PublicKey.thumbprint keyB))
                | other -> failtest $"expected Opened, got {other}"

                let _, first = supply f state a1 (mailedCode f) "2468"
                first |> Expect.equal "the first attempt is gone" (SupplyPinResult.Refused PinRefusal.AttemptExpired)
            }

            test "a wrong code counts; the third voids the code for every attempt (ext 2b)" {
                let f = fixture ()
                let state, a1 = suspendVia f seeded launch1 keyA "no-pin"
                let state, a2 = suspendVia f state (mintFor "n-2" "patient-1") keyB "no-pin"
                let state, r1 = supply f state a1 "000000" "2468"
                r1 |> Expect.equal "two left" (SupplyPinResult.Refused(PinRefusal.WrongCode 2))
                let state, r2 = supply f state a2 "000000" "2468"
                r2 |> Expect.equal "one left, counted across attempts" (SupplyPinResult.Refused(PinRefusal.WrongCode 1))
                let state, r3 = supply f state a1 "000000" "2468"
                r3 |> Expect.equal "void" (SupplyPinResult.Refused PinRefusal.CodeVoid)
                state.Codes |> Map.isEmpty |> Expect.isTrue "code dropped"
                state.Enrolments |> Map.isEmpty |> Expect.isTrue "both attempts dropped"
                state.Credentials["no-pin"] |> Credential.pinSet |> Expect.isFalse "no PIN set"
                let _, r4 = supply f state a2 (mailedCode f) "2468"
                r4 |> Expect.equal "even the right code is too late" (SupplyPinResult.Refused PinRefusal.AttemptExpired)
                f.outbox.sent () |> List.length |> Expect.equal "no second mail" 1
            }

            test "a fresh launch after a void code mails a fresh one" {
                let f = fixture ()
                let state, a1 = suspendVia f seeded launch1 keyA "no-pin"
                let state, _ = supply f state a1 "000000" "2468"
                let state, _ = supply f state a1 "000000" "2468"
                let state, _ = supply f state a1 "000000" "2468"
                let state, a2 = suspendVia f state (mintFor "n-2" "patient-1") keyB "no-pin"
                f.outbox.sent () |> List.length |> Expect.equal "two mails" 2
                let _, result = supply f state a2 (mailedCode f) "2468"

                match result with
                | SupplyPinResult.Opened _ -> ()
                | other -> failtest $"expected Opened, got {other}"
            }

            test "a PIN without the format spends no try" {
                let f = fixture ()
                let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                let state, result = supply f state attempt (mailedCode f) "12"
                result |> Expect.equal "format" (SupplyPinResult.Refused PinRefusal.PinFormat)
                state.Codes["no-pin"].Tries |> Expect.equal "no try spent" 0
                let _, ok = supply f state attempt (mailedCode f) "1234"

                match ok with
                | SupplyPinResult.Opened _ -> ()
                | other -> failtest $"expected Opened, got {other}"
            }

            test "an expired code is refused and dropped with its attempts" {
                let f = fixture ()
                let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                let code = mailedCode f
                let late = t0 + Hop.codeLifetime + TimeSpan.FromSeconds 1.0
                let state, result = supplyAt late f state attempt code "2468"
                result |> Expect.equal "expired" (SupplyPinResult.Refused PinRefusal.AttemptExpired)
                state.Codes |> Map.isEmpty |> Expect.isTrue "code dropped"
                state.Enrolments |> Map.isEmpty |> Expect.isTrue "attempt dropped"
                state.Credentials["no-pin"] |> Credential.pinSet |> Expect.isFalse "no PIN set"
            }

            test "an unknown attempt is expired" {
                let _, result = supply (fixture ()) seeded "nope" "123456" "2468"
                result |> Expect.equal "expired" (SupplyPinResult.Refused PinRefusal.AttemptExpired)
            }

            test "the PIN-set mail falls back on the code's address when the registry cannot answer" {
                let f = fixture ()
                let state, attempt = suspendVia f seeded launch1 keyA "no-pin"
                let code = mailedCode f

                let _, result =
                    Hop.supplyPin t0 f.ids salts codeMac (fun _ -> None) StubPatientData.port.read f.outbox.port.send attempt code "2468" state

                match result with
                | SupplyPinResult.Opened _ -> ()
                | other -> failtest $"expected Opened, got {other}"

                (f.outbox.sent () |> List.head).To |> Expect.equal "the code's address" "no-pin@stub.example"
            }

            test "the open closes the person's other Sessions (Rule 8)" {
                let f = fixture ()
                // a PIN set by an earlier enrolment, then a Session, then a launch that... cannot
                // suspend any more. So: enrol in one browser while another Session of the same
                // person, opened before the PIN existed, cannot exist. The Rule 8 close still
                // runs in the same act; exercise it with a seeded prescriber turned no-pin.
                let state =
                    { seeded with
                        Credentials = seeded.Credentials |> Map.add "prescriber" Credential.empty
                    }

                let state, a = suspendVia f state launch1 keyA "prescriber"
                let state, _ = supply f state a (mailedCode f) "2468"
                let state, cb = hop f state (mintFor "n-2" "patient-1") keyB "prescriber"
                let state, second = run f state cb

                match second with
                | CallbackResult.Opened(id2, _) ->
                    state.Sessions |> Map.count |> Expect.equal "one Session" 1
                    state.Sessions |> Map.containsKey id2 |> Expect.isTrue "the newer"
                    state.Endings |> Map.count |> Expect.equal "the older marked" 1
                | other -> failtest $"expected Opened, got {other}"
            }
        ]


let dropTests =
    testList
        "dropEnrolment"
        [
            test "the last attempt takes the code along; another attempt keeps it" {
                let f = fixture ()
                let state, a1 = suspendVia f seeded launch1 keyA "no-pin"
                let state, a2 = suspendVia f state (mintFor "n-2" "patient-1") keyB "no-pin"
                let state = Hop.dropEnrolment a1 state
                state.Codes |> Map.containsKey "no-pin" |> Expect.isTrue "code stands for a2"
                let state = Hop.dropEnrolment a2 state
                state.Codes |> Map.isEmpty |> Expect.isTrue "code gone with the last attempt"
                Hop.dropEnrolment "nope" state |> Expect.equal "unknown is nothing" state
            }
        ]


/// In-memory cookies: what the browser would hold.
let memoryCookie (initial: string option) =
    let value = ref initial

    {
        SessionCookie.read = fun () -> value.Value
        write = fun id -> value.Value <- Some id
        delete = fun () -> value.Value <- None
    },
    value


let memoryStateCookie (initial: string option) =
    let value = ref initial

    {
        LaunchStateCookie.read = fun state -> value.Value |> Option.filter ((=) state)
        write = fun state -> value.Value <- Some state
    },
    value


let memoryEnrolmentCookie (initial: string option) =
    let value = ref initial
    let until = ref None

    {
        EnrolmentCookie.read = fun () -> value.Value
        write =
            fun attempt u ->
                value.Value <- Some attempt
                until.Value <- Some u
        delete = fun () -> value.Value <- None
    },
    value,
    until


let makePort (f: Fixture) =
    Hop.makeSessionPort
        (fun () -> t0)
        f.ids
        f.newCode
        salts
        codeMac
        (verifyAt t0)
        f.d.idp
        f.d.registry
        StubPatientData.port
        f.outbox.port
        seeded


/// Runs the hop for `choice` through the composition root, returning the redirect.
let openVia (f: Fixture) port cookie stateCookie enrolment launch key choice =
    async {
        let! outcome =
            CompositionRoot.processLaunch port cookie stateCookie (LaunchCommand.PresentLaunch(launch, key))

        match outcome with
        | LaunchOutcome.RedirectTo url ->
            let state = url.Substring(url.IndexOf "state=" + 6) |> Uri.UnescapeDataString

            let cb: Callback =
                {
                    State = state
                    StateCookie = None
                    Code = Some(f.d.issue choice "patient-1")
                    Error = None
                }

            return! CompositionRoot.processCallback port cookie stateCookie enrolment cb
        | other -> return failtest $"expected RedirectTo, got {other}"
    }


let compositionTests =
    testList
        "composition root"
        [
            testAsync "no-pin: the callback sets the enrolment cookie, GetSession tells the pending enrolment, SupplyPin opens and swaps the cookies" {
                let f = fixture ()
                let port = makePort f
                let cookie, held = memoryCookie None
                let stateCookie, _ = memoryStateCookie None
                let enrolment, attempt, until = memoryEnrolmentCookie None
                let! redirect = openVia f port cookie stateCookie enrolment launch1 keyA "no-pin"
                redirect |> Expect.equal "to the app" "/#/session"
                held.Value |> Expect.isNone "no session cookie"
                attempt.Value |> Expect.isSome "enrolment cookie"
                until.Value |> Expect.equal "until the code expires" (Some(t0 + Hop.codeLifetime))

                let! pending = CompositionRoot.processSession port cookie enrolment SessionCommand.GetSession

                pending
                |> Expect.equal
                    "pending"
                    (SessionResponse.EnrolmentPending
                        {
                            DisplayName = "Stub Prescriber (no PIN)"
                            MailHint = "n***@stub.example"
                        })

                let! wrong = CompositionRoot.processSession port cookie enrolment (SessionCommand.SupplyPin("000000", "2468"))
                wrong |> Expect.equal "wrong code" (SessionResponse.PinRefused(PinRefusal.WrongCode 2))
                attempt.Value |> Expect.isSome "cookie kept"

                let! opened =
                    CompositionRoot.processSession port cookie enrolment (SessionCommand.SupplyPin(mailedCode f, "2468"))

                match opened with
                | SessionResponse.SessionResp(Some session) ->
                    session.User |> Option.map _.UserId |> Expect.equal "user" (Some "no-pin")
                | other -> failtest $"expected SessionResp, got {other}"

                held.Value |> Expect.isSome "session cookie set"
                attempt.Value |> Expect.isNone "enrolment cookie deleted"

                let! found = CompositionRoot.processSession port cookie enrolment SessionCommand.GetSession

                match found with
                | SessionResponse.SessionResp(Some _) -> ()
                | other -> failtest $"expected the Session, got {other}"
            }

            testAsync "a void code deletes the enrolment cookie; a gone attempt at GetSession does too" {
                let f = fixture ()
                let port = makePort f
                let cookie, _ = memoryCookie None
                let stateCookie, _ = memoryStateCookie None
                let enrolment, attempt, _ = memoryEnrolmentCookie None
                let! _ = openVia f port cookie stateCookie enrolment launch1 keyA "no-pin"

                for _ in 1..2 do
                    let! _ = CompositionRoot.processSession port cookie enrolment (SessionCommand.SupplyPin("000000", "2468"))
                    ()

                let! void' = CompositionRoot.processSession port cookie enrolment (SessionCommand.SupplyPin("000000", "2468"))
                void' |> Expect.equal "void" (SessionResponse.PinRefused PinRefusal.CodeVoid)
                attempt.Value |> Expect.isNone "cookie deleted"

                let stale, attemptRef, _ = memoryEnrolmentCookie (Some "gone")
                let! nothing = CompositionRoot.processSession port cookie stale SessionCommand.GetSession
                nothing |> Expect.equal "nothing" (SessionResponse.SessionResp None)
                attemptRef.Value |> Expect.isNone "stale cookie deleted"
            }

            testAsync "SupplyPin without an enrolment cookie is expired; CloseSession while enrolling drops the attempt and the cookie" {
                let f = fixture ()
                let port = makePort f
                let cookie, _ = memoryCookie None
                let stateCookie, _ = memoryStateCookie None
                let none, _, _ = memoryEnrolmentCookie None
                let! expired = CompositionRoot.processSession port cookie none (SessionCommand.SupplyPin("123456", "2468"))
                expired |> Expect.equal "expired" (SessionResponse.PinRefused PinRefusal.AttemptExpired)

                let enrolment, attempt, _ = memoryEnrolmentCookie None
                let! _ = openVia f port cookie stateCookie enrolment launch1 keyA "no-pin"
                let! closed = CompositionRoot.processSession port cookie enrolment SessionCommand.CloseSession
                closed |> Expect.equal "closed" SessionResponse.SessionClosed
                attempt.Value |> Expect.isNone "cookie deleted"
                let stale, _, _ = memoryEnrolmentCookie (Some "id-2")
                let! nothing = CompositionRoot.processSession port cookie stale SessionCommand.GetSession
                nothing |> Expect.equal "the attempt is gone" (SessionResponse.SessionResp None)
            }

            testAsync "the Session wins over a stale enrolment cookie" {
                let f = fixture ()
                let port = makePort f
                let cookie, _ = memoryCookie None
                let stateCookie, _ = memoryStateCookie None
                let enrolment, _, _ = memoryEnrolmentCookie (Some "stale")
                let! _ = openVia f port cookie stateCookie enrolment launch1 keyA "prescriber"
                let! found = CompositionRoot.processSession port cookie enrolment SessionCommand.GetSession

                match found with
                | SessionResponse.SessionResp(Some _) -> ()
                | other -> failtest $"expected the Session, got {other}"
            }

            testAsync "the disabled port refuses to enrol" {
                let cookie, _ = memoryCookie None
                let enrolment, attempt, _ = memoryEnrolmentCookie (Some "any")

                let! refused =
                    CompositionRoot.processSession sessionDisabled cookie enrolment (SessionCommand.SupplyPin("123456", "2468"))

                refused |> Expect.equal "expired" (SessionResponse.PinRefused PinRefusal.AttemptExpired)
                attempt.Value |> Expect.isNone "cookie deleted"
            }
        ]


let tests =
    testList
        "Enrolment PR 2"
        [
            helperTests
            suspendTests
            findTests
            supplyTests
            dropTests
            compositionTests
        ]


runTestsWithCLIArgs [] [||] tests
