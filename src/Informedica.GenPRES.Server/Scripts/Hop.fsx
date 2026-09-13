// The identity hop of uc-01 step 4, and step 5, over server-hosted stubs (plan 605, PR 2).
//
// Script-first draft (script-only policy) of:
//   - the ports of the actors behind the launch: IdentityProvider (Actor 8), UserRegistry,
//     PatientDataPlatform → `ServerApi.Ports.fs`;
//   - `LaunchResult.RedirectTo` carrying the `state` the edge puts in the cookie, `SessionPort`
//     growing `callback`, `LaunchStateCookie` (the request port for the state cookie) → `Ports.fs`;
//   - `Hop`: the LaunchRecord keyed by nonce (4.2), the pure `present` (→ RedirectTo, Rule 2
//     replay) and `callback` (4.5 + step 5: the refusal ladder, Rule 45 replay, the Rule 40 single
//     act with the Rule 8 closes) → `ServerApi.Adapters.fs`, replacing `SessionStub`;
//   - the stubs: `StubDirectory` (IdP + UserRegistry over one identity choice), `StubPatientData`
//     → `Adapters.fs`; `makeSessionPort` over them.
//
// Review points folded in (#606): the error redirect keeps the state; the PIN check binds
// Prescribers only (Rule 25, ext 5c); `read = None` opens without imported data (ext 6a).
//
// PR 4 (endings, Rules 8 and 11): `SessionEnding` moves to Shared (the client shows it),
// `find` answers `SessionLookup.Ended` once for a Session the server ended, and drops the mark.
//
// Run: `dotnet fsi Hop.fsx` from this directory (build the solution first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models
open ServerApi


// ---------------------------------------------------------------------------------------------
// Ports (→ ServerApi.Ports.fs)
// ---------------------------------------------------------------------------------------------

/// Who the IdentityProvider says is at the browser (Concept 4). The Session's User is derived
/// from it (Rule 4), never from the Launch.
type BrowserIdentity =
    {
        Login: string
        DisplayName: string
    }


/// What the UserRegistry says about a login at this launch (Rules 5, 6, 24): the User with the
/// Role, the Patient active in MainEHR, and whether a PIN is set.
type UserStanding =
    {
        User: UserContext
        ActivePatientId: string option
        PinSet: bool
    }


/// Actor 8. `authorizeUrl` is where the browser is sent with the `state` (4.2); `redeem`
/// exchanges the callback's code for the identity over the server's own connection (4.4, C6).
type IdentityProviderPort =
    {
        authorizeUrl: string -> string
        redeem: string -> BrowserIdentity option
    }


type UserRegistryPort = { standing: BrowserIdentity -> UserStanding option }


/// The PatientDataPlatform, read once at the launch (Concept 2). `None` is not a refusal
/// (ext 6a): the Session opens without imported data.
type PatientDataPort = { read: string -> Patient option }


/// The session adapter's answer to a presentation. `RedirectTo` carries the `state` the edge
/// writes to the state cookie next to the url that carries it to the IdentityProvider.
[<RequireQualifiedAccess>]
type LaunchResult =
    | Opened of sessionId: string * SessionOpened
    | RedirectTo of url: string * state: string
    | Refused of LaunchRefusal


/// What the callback (4.5) brings: the `state` from the url and from the cookie, and either a
/// code or the IdentityProvider's error.
type Callback =
    {
        State: string
        StateCookie: string option
        Code: string option
        Error: string option
    }


/// The answer to a callback: where the browser goes next, and the session id for the cookie
/// when a Session opened.
[<RequireQualifiedAccess>]
type CallbackResult =
    | Opened of sessionId: string * redirect: string
    | Refused of LaunchRefusal * redirect: string
    // a reload of a callback whose Session a newer launch has since replaced (Rule 8): the
    // browser goes to the app on whatever cookie it holds, which is the newer Session's
    | Superseded of redirect: string


/// Why a Session ended other than by the User closing it (Rule 11). One case now; idle and
/// absolute lifetime (Rule 10) come with their own plan. → Shared/Types.fs, next to LaunchRefusal.
[<RequireQualifiedAccess>]
type SessionEnding = SupersededByLaunch


/// What the store says about a session id from the cookie: the Session, nothing, or that the
/// server ended it (Rule 11), said as long as the browser still sends the cookie the answer
/// deletes.
[<RequireQualifiedAccess>]
type SessionLookup =
    | Found of SessionOpened
    | NotFound
    | Ended of SessionEnding


type SessionPort =
    {
        present: Launch * PublicKey -> Async<LaunchResult>
        callback: Callback -> Async<CallbackResult>
        find: string -> Async<SessionLookup>
        close: string -> Async<unit>
    }


/// The state cookie of one request (4.2): written with the redirect, read at the callback.
type LaunchStateCookie =
    {
        // the cookie of one hop, named by its state, so that two tabs can launch at once
        read: string -> string option
        write: string -> unit
    }


// ---------------------------------------------------------------------------------------------
// The hop (→ ServerApi.Adapters.fs, replacing SessionStub)
// ---------------------------------------------------------------------------------------------

module Hop =

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

    let refusedUrl refusal = $"/#/session?refused={refusalWord refusal}"


    /// One record per Launch (4.2), keyed by the nonce (Rule 2) and found by the `state` at the
    /// callback. The outcome is appended once (Rule 45); the record is dropped whole at expiry.
    type LaunchRecord =
        {
            Nonce: string
            State: string
            PatientId: string
            PublicKey: PublicKey
            Expiry: DateTime
            Outcome: LaunchResult option
        }


    /// A Session as the store holds it: what the client learns, plus the login it belongs to
    /// (Rule 8: a User has at most one open Session).
    type SessionRecord =
        {
            Session: SessionOpened
            Login: string option
        }


    type State =
        {
            Launches: Map<string, LaunchRecord>
            Sessions: Map<string, SessionRecord>
            // sessions the server ended, with the moment: told at every GetSession that still
            // carries their cookie (Rule 11), dropped when the client acknowledges with
            // CloseSession. Kept as long as the Sessions are (Rule 10 will bound both).
            Endings: Map<string, SessionEnding * DateTime>
        }


    let emptyState =
        {
            Launches = Map.empty
            Sessions = Map.empty
            Endings = Map.empty
        }


    let private dropExpired (now: DateTime) (state: State) =
        { state with Launches = state.Launches |> Map.filter (fun _ r -> now <= r.Expiry) }


    /// The answer `present` gives for a record: the recorded outcome, else the redirect again.
    let private answerOf (authorizeUrl: string -> string) (record: LaunchRecord) =
        match record.Outcome with
        | Some outcome -> outcome
        | None -> LaunchResult.RedirectTo(authorizeUrl record.State, record.State)


    /// Step 4.1 and 4.2. Pure over the state; the clock, the id source, the verifier and the
    /// IdentityProvider's url are parameters.
    ///
    /// - not sealed under the key, or past its expiry: refused, nothing recorded (Rules 3, 29);
    /// - a record under the nonce: the same public key gets the recorded answer, or the redirect
    ///   again while the hop is still open (Rule 2, uc-01 Retries); another key is LaunchSpent;
    /// - a new nonce appends the record and sends the browser to the IdentityProvider.
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

                { state with Launches = state.Launches |> Map.add claims.Nonce record },
                answerOf authorizeUrl record


    /// Step 5.7, one act (Rule 40): the Session is written, the login's other Sessions are
    /// closed and marked (Rule 8), the outcome is appended to the record.
    let private openSession
        (now: DateTime)
        (newId: unit -> string)
        (patientData: string -> Patient option)
        (record: LaunchRecord)
        (standing: UserStanding)
        (state: State)
        =
        let id = newId ()

        let session =
            {
                User = Some standing.User
                PatientContext =
                    Some
                        {
                            PatientId = record.PatientId
                            // ext 6a: no imported data is not a refusal
                            Patient = patientData record.PatientId |> Option.defaultValue Patient.empty
                        }
                OpenedToken = Some(OpenedToken $"opened-{id}")
                KeyThumbprint = Some(PublicKey.thumbprint record.PublicKey)
            }

        let login = Some standing.User.UserId

        let superseded =
            state.Sessions
            |> Map.filter (fun sid s -> sid <> id && s.Login = login)
            |> Map.toList
            |> List.map fst

        let outcome = LaunchResult.Opened(id, session)

        {
            Launches = state.Launches |> Map.add record.Nonce { record with Outcome = Some outcome }
            Sessions =
                superseded
                |> List.fold (fun m sid -> Map.remove sid m) state.Sessions
                |> Map.add id { Session = session; Login = login }
            Endings =
                superseded
                |> List.fold (fun m sid -> Map.add sid (SessionEnding.SupersededByLaunch, now) m) state.Endings
        },
        CallbackResult.Opened(id, openedUrl)


    let private refuse (record: LaunchRecord) refusal (state: State) =
        { state with
            Launches =
                state.Launches
                |> Map.add record.Nonce { record with Outcome = Some(LaunchResult.Refused refusal) }
        },
        CallbackResult.Refused(refusal, refusedUrl refusal)


    /// Step 4.5 and step 5. The ladder, in order: the state cookie must match (the browser that
    /// started the hop), the record must exist and be within its lifetime, an outcome already
    /// recorded is answered again (Rule 45), the IdentityProvider must have said who is there
    /// (ext 3c), the UserRegistry must know them (5.3, ext 5a) with the Launch's Patient active
    /// (ext 5b), and a Prescriber must have a PIN (5.4, Rule 25, ext 5d; a Reader needs none,
    /// ext 5c). Then the Session opens in one act.
    let callback
        (now: DateTime)
        (newId: unit -> string)
        (redeem: string -> BrowserIdentity option)
        (standing: BrowserIdentity -> UserStanding option)
        (patientData: string -> Patient option)
        (state: State)
        (cb: Callback)
        : State * CallbackResult
        =
        let state = dropExpired now state

        let byState =
            state.Launches |> Map.toSeq |> Seq.map snd |> Seq.tryFind (fun r -> r.State = cb.State)

        let invalid = CallbackResult.Refused(LaunchRefusal.LaunchInvalid, refusedUrl LaunchRefusal.LaunchInvalid)

        match cb.StateCookie, byState with
        | Some cookie, Some record when cookie = cb.State && cb.State <> "" ->
            match record.Outcome with
            | Some(LaunchResult.Opened(id, _)) when state.Sessions |> Map.containsKey id ->
                state, CallbackResult.Opened(id, openedUrl)
            // the recorded Session was replaced by a newer launch of the same login (Rule 8):
            // answering its id would put a dead cookie over the live one
            | Some(LaunchResult.Opened _) -> state, CallbackResult.Superseded openedUrl
            | Some(LaunchResult.Refused refusal) -> state, CallbackResult.Refused(refusal, refusedUrl refusal)
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
                    | Some standing when standing.User.Role = UserRole.Prescriber && not standing.PinSet ->
                        refuse record LaunchRefusal.EnrolmentRequired state
                    | Some standing -> openSession now newId patientData record standing state
        | _, None ->
            // no record: expired and dropped (Rule 29), or never presented
            state, invalid
        | _ -> state, invalid


    /// The lookup for a session id: the Session; else the ending the server recorded, answered
    /// as long as the cookie keeps coming; else nothing. The client acknowledges an ending with
    /// CloseSession, which deletes the cookie and drops the mark (`close`), so a lost answer is
    /// asked and told again while an acknowledged one is told once. An unacknowledged ending
    /// is kept as long as the stub keeps its Sessions: the notification is the User's only one
    /// (Rule 11). The absolute Session lifetime (Rule 10), when it lands, bounds both.
    let find (id: string) (state: State) : State * SessionLookup =
        match state.Sessions |> Map.tryFind id with
        | Some record -> state, SessionLookup.Found record.Session
        | None ->
            match state.Endings |> Map.tryFind id with
            | Some(ending, _) -> state, SessionLookup.Ended ending
            | None -> state, SessionLookup.NotFound


    /// Rule 10's explicit close, and the acknowledgement of an ending: the Session and any
    /// mark for the id are dropped together.
    let close (id: string) (state: State) : State =
        { state with
            Sessions = state.Sessions |> Map.remove id
            Endings = state.Endings |> Map.remove id
        }


    /// The port over a single mutable state guarded by a lock, over the three actor ports.
    let makeSessionPort
        (now: unit -> DateTime)
        (newId: unit -> string)
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (idp: IdentityProviderPort)
        (registry: UserRegistryPort)
        (patientData: PatientDataPort)
        =
        let gate = obj ()
        let mutable state = emptyState

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
                            update (fun s -> callback (now ()) newId idp.redeem registry.standing patientData.read s cb)
                    }
            find = fun id -> async { return update (find id) }
            close = fun id -> async { return update (fun s -> close id s, ()) }
        }


// ---------------------------------------------------------------------------------------------
// The stubs (→ ServerApi.Adapters.fs)
// ---------------------------------------------------------------------------------------------

/// The IdentityProvider and the UserRegistry as one stub directory over an identity choice made
/// on the stub launch page: `prescriber`, `reader`, `prescriber-other-patient`, `no-pin`,
/// `unknown` (a login the registry does not know), `none` (no identity: the stub IdP's
/// `/authorize` never issues a code for it). The active Patient of a choice is the one the
/// launch page was given, except for `prescriber-other-patient`.
module StubDirectory =

    let choices = [ "prescriber"; "reader"; "prescriber-other-patient"; "no-pin"; "unknown"; "none" ]


    let identityOf choice =
        {
            Login = choice
            DisplayName =
                match choice with
                | "prescriber" -> "Stub Prescriber"
                | "reader" -> "Stub Reader"
                | "prescriber-other-patient" -> "Stub Prescriber (other patient)"
                | "no-pin" -> "Stub Prescriber (no PIN)"
                | other -> $"Stub {other}"
        }


    /// The registry's answer for a login, given the Patient the launch page made active.
    let standingOf (activePatientId: string) (identity: BrowserIdentity) : UserStanding option =
        let user role =
            {
                UserId = identity.Login
                DisplayName = identity.DisplayName
                Role = role
            }

        match identity.Login with
        | "prescriber" ->
            Some
                {
                    User = user UserRole.Prescriber
                    ActivePatientId = Some activePatientId
                    PinSet = true
                }
        | "reader" ->
            Some
                {
                    User = user UserRole.Reader
                    ActivePatientId = Some activePatientId
                    PinSet = false
                }
        | "prescriber-other-patient" ->
            Some
                {
                    User = user UserRole.Prescriber
                    ActivePatientId = Some "other-patient"
                    PinSet = true
                }
        | "no-pin" ->
            Some
                {
                    User = user UserRole.Prescriber
                    ActivePatientId = Some activePatientId
                    PinSet = false
                }
        | _ -> None


    /// The directory: one-time codes (the stub IdP) and the active Patient per login (the stub
    /// registry), behind a lock. `issue` is what `/authorize` calls once it read the identity
    /// choice and the active Patient from the stub cookie.
    type Directory =
        {
            idp: IdentityProviderPort
            registry: UserRegistryPort
            issue: string -> string -> string
        }


    /// A code lives as long as a Launch (Rule 29); older ones are pruned on the next issue.
    let codeLifetime = TimeSpan.FromMinutes 2.0


    /// One active Patient per login, as MainEHR has: a later launch of the same login for
    /// another Patient makes an earlier, still open launch wrong-patient (ext 5b).
    let make (now: unit -> DateTime) (newCode: unit -> string) : Directory =
        let gate = obj ()
        let codes = Collections.Generic.Dictionary<string, BrowserIdentity * DateTime>()
        let active = Collections.Generic.Dictionary<string, string>()

        let prune () =
            let cutoff = now () - codeLifetime

            for stale in codes |> Seq.filter (fun kv -> snd kv.Value < cutoff) |> Seq.map _.Key |> Seq.toList do
                codes.Remove stale |> ignore

        {
            idp =
                {
                    authorizeUrl = fun state -> $"/authorize?state={Uri.EscapeDataString state}"
                    redeem =
                        fun code ->
                            lock
                                gate
                                (fun () ->
                                    match codes.TryGetValue code with
                                    | true, (identity, issued) when now () - issued <= codeLifetime ->
                                        codes.Remove code |> ignore
                                        Some identity
                                    | _ -> None
                                )
                }
            registry =
                {
                    standing =
                        fun identity ->
                            lock
                                gate
                                (fun () ->
                                    match active.TryGetValue identity.Login with
                                    | true, pid -> standingOf pid identity
                                    | _ -> standingOf "" identity
                                )
                }
            issue =
                fun choice activePatientId ->
                    lock
                        gate
                        (fun () ->
                            prune ()
                            let code = newCode ()
                            codes[code] <- identityOf choice, now ()
                            active[choice] <- activePatientId
                            code
                        )
        }


/// The PatientDataPlatform stub: nothing to import, except that `no-data` has no record at all
/// (ext 6a).
module StubPatientData =

    let port: PatientDataPort =
        {
            read = fun pid -> if pid = "no-data" then None else Some Patient.empty
        }


/// The stub launch page's identity choice, carried to the stub IdentityProvider in a cookie
/// (→ `StubLaunch` in ServerApi.Adapters.fs). The value is `choice.pid`, both url-escaped, so
/// the stub UserRegistry knows which Patient the page made active.
module StubLaunch =

    open StubLaunch

    let choices = StubDirectory.choices

    let identityCookieName = "genpres_stub_identity"


    /// The stub identity cookie value for a choice and the Patient the page made active.
    let identityCookie (choice: string) (pid: string) =
        $"{Uri.EscapeDataString choice}.{Uri.EscapeDataString pid}"


    /// The choice and the Patient back from the cookie; None for anything else.
    let parseIdentityCookie (value: string option) : (string * string) option =
        match value with
        | Some v when not (String.IsNullOrWhiteSpace v) ->
            match v.Split('.') with
            | [| choice; pid |] when choices |> List.contains (Uri.UnescapeDataString choice) ->
                Some(Uri.UnescapeDataString choice, Uri.UnescapeDataString pid)
            | _ -> None
        | _ -> None


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


/// A fresh directory and id sources per test.
let fixture () =
    let ids = counter "id"
    let directory = StubDirectory.make (fun () -> t0) (counter "code")
    ids, directory


/// Presents, then plays the stub IdP for `choice`, and returns the callback the browser brings.
let hop (ids: unit -> string) (directory: StubDirectory.Directory) state launch key choice =
    let state, result = Hop.present t0 ids (verifyAt t0) directory.idp.authorizeUrl state (launch, key)

    match result with
    | LaunchResult.RedirectTo(_, st) ->
        let cb =
            if choice = "none" then
                {
                    State = st
                    StateCookie = Some st
                    Code = None
                    Error = Some "no-identity"
                }
            else
                {
                    State = st
                    StateCookie = Some st
                    Code = Some(directory.issue choice "patient-1")
                    Error = None
                }

        state, cb
    | other -> failtest $"expected RedirectTo, got {other}"


let run (ids: unit -> string) (directory: StubDirectory.Directory) state cb =
    Hop.callback t0 ids directory.idp.redeem directory.registry.standing StubPatientData.port.read state cb


let presentTests =
    testList
        "Hop.present"
        [
            test "a sealed Launch is recorded under its nonce and sent to the IdentityProvider" {
                let ids, d = fixture ()
                let state, result = Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl Hop.emptyState (launch1, keyA)

                match result with
                | LaunchResult.RedirectTo(url, st) ->
                    url |> Expect.equal "authorize url carries the state" $"/authorize?state={st}"
                    let record = state.Launches["n-1"]
                    record.State |> Expect.equal "same state in the record" st
                    record.PatientId |> Expect.equal "patient" "patient-1"
                    record.Outcome |> Expect.isNone "no outcome yet"
                | other -> failtest $"expected RedirectTo, got {other}"
            }

            test "the same key while the hop is open gets the same redirect (Rule 2, retry)" {
                let ids, d = fixture ()
                let state, first = Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl Hop.emptyState (launch1, keyA)
                let state2, again = Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl state (launch1, keyA)
                again |> Expect.equal "same redirect" first
                state2 |> Expect.equal "same state" state
            }

            test "another key is spent" {
                let ids, d = fixture ()
                let state, _ = Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl Hop.emptyState (launch1, keyA)
                let _, other = Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl state (launch1, keyB)
                other |> Expect.equal "spent" (LaunchResult.Refused LaunchRefusal.LaunchSpent)
            }

            test "after the hop opened, the same key gets the opened Session (Rule 2)" {
                let ids, d = fixture ()
                let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
                let state, opened = run ids d state cb
                let _, again = Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl state (launch1, keyA)

                match opened, again with
                | CallbackResult.Opened(id, _), LaunchResult.Opened(id', session) ->
                    id' |> Expect.equal "same session" id
                    session.User |> Option.map _.DisplayName |> Expect.equal "user" (Some "Stub Prescriber")
                | other -> failtest $"expected Opened twice, got {other}"
            }

            test "not sealed, or expired: refused and nothing recorded" {
                let ids, d = fixture ()

                let _, invalid =
                    Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl Hop.emptyState (Launch "junk", keyA)

                invalid |> Expect.equal "invalid" (LaunchResult.Refused LaunchRefusal.LaunchInvalid)
                let late = t0 + lifetime + TimeSpan.FromSeconds 1.0

                let state, expired =
                    Hop.present late ids (verifyAt late) d.idp.authorizeUrl Hop.emptyState (launch1, keyA)

                expired |> Expect.equal "expired" (LaunchResult.Refused LaunchRefusal.LaunchExpired)
                state.Launches |> Map.isEmpty |> Expect.isTrue "nothing recorded"
            }
        ]


let callbackTests =
    testList
        "Hop.callback"
        [
            test "prescriber: the Session opens for the Launch's Patient, the outcome is recorded" {
                let ids, d = fixture ()
                let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
                let state, result = run ids d state cb

                match result with
                | CallbackResult.Opened(id, url) ->
                    url |> Expect.equal "to the app" "/#/session"
                    let record = state.Sessions[id]
                    record.Login |> Expect.equal "login" (Some "prescriber")
                    record.Session.User |> Option.map _.Role |> Expect.equal "role" (Some UserRole.Prescriber)
                    record.Session.PatientContext |> Option.map _.PatientId |> Expect.equal "patient" (Some "patient-1")
                    record.Session.KeyThumbprint |> Expect.equal "thumbprint" (Some(PublicKey.thumbprint keyA))
                    state.Launches["n-1"].Outcome |> Expect.isSome "outcome appended"
                | other -> failtest $"expected Opened, got {other}"
            }

            test "reader: opens without a PIN (ext 5c)" {
                let ids, d = fixture ()
                let state, cb = hop ids d Hop.emptyState launch1 keyA "reader"
                let state, result = run ids d state cb

                match result with
                | CallbackResult.Opened(id, _) ->
                    state.Sessions[id].Session.User |> Option.map _.Role |> Expect.equal "role" (Some UserRole.Reader)
                | other -> failtest $"expected Opened, got {other}"
            }

            testList
                "refusals, each recorded and each with its word"
                [
                    for choice, refusal in
                        [
                            "none", LaunchRefusal.NoBrowserIdentity
                            "unknown", LaunchRefusal.NoRole
                            "prescriber-other-patient", LaunchRefusal.WrongActivePatient
                            "no-pin", LaunchRefusal.EnrolmentRequired
                        ] do
                        test choice {
                            let ids, d = fixture ()
                            let state, cb = hop ids d Hop.emptyState launch1 keyA choice
                            let state, result = run ids d state cb

                            result
                            |> Expect.equal
                                "refused"
                                (CallbackResult.Refused(refusal, $"/#/session?refused={Hop.refusalWord refusal}"))

                            state.Sessions |> Map.isEmpty |> Expect.isTrue "no session (Rule 7)"

                            state.Launches["n-1"].Outcome
                            |> Expect.equal "recorded" (Some(LaunchResult.Refused refusal))
                        }
                ]

            test "a callback reload gets the same answer without a second redeem (Rule 45)" {
                let ids, d = fixture ()
                let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
                let state, first = run ids d state cb
                // the code was consumed by the first redeem; the replay must not need it
                let state2, again = run ids d state cb
                again |> Expect.equal "same answer" first
                state2 |> Expect.equal "same state" state
            }

            test "a state cookie that does not match is invalid, and nothing is redeemed" {
                let ids, d = fixture ()
                let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"

                for cookie in [ None; Some "other" ] do
                    let state2, result = run ids d state { cb with StateCookie = cookie }
                    result |> Expect.equal $"cookie {cookie}" (CallbackResult.Refused(LaunchRefusal.LaunchInvalid, "/#/session?refused=invalid"))
                    state2.Launches["n-1"].Outcome |> Expect.isNone "hop still open"
            }

            test "an unknown state is invalid" {
                let ids, d = fixture ()

                let _, result =
                    run ids d Hop.emptyState { State = "nope"; StateCookie = Some "nope"; Code = Some "c"; Error = None }

                result |> Expect.equal "invalid" (CallbackResult.Refused(LaunchRefusal.LaunchInvalid, "/#/session?refused=invalid"))
            }

            test "a code that does not redeem is no browser identity" {
                let ids, d = fixture ()
                let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
                let _, result = run ids d state { cb with Code = Some "forged" }
                result |> Expect.equal "no identity" (CallbackResult.Refused(LaunchRefusal.NoBrowserIdentity, "/#/session?refused=no-identity"))
            }

            test "after the lifetime the callback is invalid: the record is gone (Rule 29)" {
                let ids, d = fixture ()
                let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
                let late = t0 + lifetime + TimeSpan.FromSeconds 1.0

                let state2, result =
                    Hop.callback late ids d.idp.redeem d.registry.standing StubPatientData.port.read state cb

                result |> Expect.equal "invalid" (CallbackResult.Refused(LaunchRefusal.LaunchInvalid, "/#/session?refused=invalid"))
                state2.Launches |> Map.isEmpty |> Expect.isTrue "dropped"
            }

            test "no patient data (ext 6a): the Session opens with the PatientId and an empty Patient" {
                let ids, d = fixture ()
                let launch = mintFor "n-nd" "no-data"
                let state, result = Hop.present t0 ids (verifyAt t0) d.idp.authorizeUrl Hop.emptyState (launch, keyA)

                let st =
                    match result with
                    | LaunchResult.RedirectTo(_, st) -> st
                    | other -> failtest $"{other}"

                let cb =
                    {
                        State = st
                        StateCookie = Some st
                        Code = Some(d.issue "prescriber" "no-data")
                        Error = None
                    }

                let state, result = run ids d state cb

                match result with
                | CallbackResult.Opened(id, _) ->
                    let ctx = state.Sessions[id].Session.PatientContext
                    ctx |> Option.map _.PatientId |> Expect.equal "patient id" (Some "no-data")
                    ctx |> Option.map _.Patient |> Expect.equal "empty patient" (Some Patient.empty)
                | other -> failtest $"expected Opened, got {other}"
            }

            test "a second launch of the same login closes the first Session and marks it (Rule 8)" {
                let ids, d = fixture ()
                let state, cb1 = hop ids d Hop.emptyState launch1 keyA "prescriber"
                let state, first = run ids d state cb1
                let state, cb2 = hop ids d state (mintFor "n-2" "patient-1") keyB "prescriber"
                let state, second = run ids d state cb2

                match first, second with
                | CallbackResult.Opened(id1, _), CallbackResult.Opened(id2, _) ->
                    state.Sessions |> Map.containsKey id1 |> Expect.isFalse "first closed"
                    state.Sessions |> Map.containsKey id2 |> Expect.isTrue "second open"
                    state.Endings |> Map.tryFind id1 |> Option.map fst |> Expect.equal "marked" (Some SessionEnding.SupersededByLaunch)
                | other -> failtest $"expected two Opened, got {other}"
            }

            test "a callback reload after a newer launch of the same login does not hand back the dead Session" {
                let ids, d = fixture ()
                let state, cb1 = hop ids d Hop.emptyState launch1 keyA "prescriber"
                let state, _ = run ids d state cb1
                let state, cb2 = hop ids d state (mintFor "n-2" "patient-1") keyB "prescriber"
                let state, _ = run ids d state cb2
                let state2, again = run ids d state cb1
                again |> Expect.equal "superseded" (CallbackResult.Superseded "/#/session")
                state2 |> Expect.equal "unchanged" state
            }

            test "the ended Session is told at every lookup that still carries the cookie (Rule 11)" {
                let ids, d = fixture ()
                let state, cb1 = hop ids d Hop.emptyState launch1 keyA "prescriber"
                let state, first = run ids d state cb1
                let state, cb2 = hop ids d state (mintFor "n-2" "patient-1") keyB "prescriber"
                let state, _ = run ids d state cb2

                let id1 =
                    match first with
                    | CallbackResult.Opened(id, _) -> id
                    | other -> failtest $"{other}"

                let state, told = Hop.find id1 state
                told |> Expect.equal "told" (SessionLookup.Ended SessionEnding.SupersededByLaunch)
                // the answer was lost, or the tab comes back much later: the cookie came again,
                // so is the ending
                let _, again = Hop.find id1 state
                again |> Expect.equal "told again" (SessionLookup.Ended SessionEnding.SupersededByLaunch)
            }

            test "two logins keep two Sessions" {
                let ids, d = fixture ()
                let state, cb1 = hop ids d Hop.emptyState launch1 keyA "prescriber"
                let state, _ = run ids d state cb1
                let state, cb2 = hop ids d state (mintFor "n-2" "patient-1") keyB "reader"
                let state, _ = run ids d state cb2
                state.Sessions |> Map.count |> Expect.equal "two" 2
                state.Endings |> Map.isEmpty |> Expect.isTrue "none ended"
            }
        ]


let portTests =
    testList
        "makeSessionPort"
        [
            testAsync "present, callback, find, close over the port" {
                let ids, d = fixture ()

                let port =
                    Hop.makeSessionPort (fun () -> t0) ids (verifyAt t0) d.idp d.registry StubPatientData.port

                let! redirect = port.present (launch1, keyA)

                let st =
                    match redirect with
                    | LaunchResult.RedirectTo(_, st) -> st
                    | other -> failtest $"{other}"

                let! opened =
                    port.callback
                        {
                            State = st
                            StateCookie = Some st
                            Code = Some(d.issue "prescriber" "patient-1")
                            Error = None
                        }

                match opened with
                | CallbackResult.Opened(id, _) ->
                    match! port.find id with
                    | SessionLookup.Found session ->
                        session.User |> Option.map _.UserId |> Expect.equal "found" (Some "prescriber")
                    | other -> failtest $"expected Found, got {other}"

                    do! port.close id
                    let! gone = port.find id
                    gone |> Expect.equal "closed" SessionLookup.NotFound
                | other -> failtest $"expected Opened, got {other}"
            }
        ]


let identityCookieTests =
    testList
        "StubLaunch identity cookie"
        [
            test "round trip, with characters the cookie cannot carry" {
                StubLaunch.identityCookie "prescriber" "p 1;2"
                |> Some
                |> StubLaunch.parseIdentityCookie
                |> Expect.equal "same" (Some("prescriber", "p 1;2"))
            }

            test "an unknown choice, garbage or nothing is None" {
                for v in [ Some "hacker.p"; Some "prescriber"; Some ""; None ] do
                    StubLaunch.parseIdentityCookie v |> Expect.isNone $"{v}"
            }
        ]


let directoryTests =
    testList
        "StubDirectory"
        [
            test "past the lifetime a code does not redeem, and the next issue prunes it" {
                let clock = ref t0
                let d = StubDirectory.make (fun () -> clock.Value) (counter "code")
                let stale = d.issue "prescriber" "p"
                clock.Value <- t0 + StubDirectory.codeLifetime + TimeSpan.FromSeconds 1.0
                d.idp.redeem stale |> Expect.isNone "stale"
                let fresh = d.issue "reader" "p"
                d.idp.redeem fresh |> Option.map _.Login |> Expect.equal "fresh" (Some "reader")
            }

            test "a code redeems once" {
                let d = StubDirectory.make (fun () -> t0) (counter "code")
                let code = d.issue "prescriber" "p"
                d.idp.redeem code |> Option.map _.Login |> Expect.equal "first" (Some "prescriber")
                d.idp.redeem code |> Expect.isNone "second"
            }

            test "every choice but none has an identity; unknown has no standing" {
                let d = StubDirectory.make (fun () -> t0) (counter "code")

                for choice in StubDirectory.choices |> List.filter ((<>) "none") do
                    let identity = d.idp.redeem (d.issue choice "p") |> Option.get
                    identity.Login |> Expect.equal "login" choice

                    match choice, d.registry.standing identity with
                    | "unknown", None -> ()
                    | "unknown", Some _ -> failtest "unknown has standing"
                    | _, None -> failtest $"{choice} has no standing"
                    | _, Some _ -> ()
            }
        ]


runTestsWithCLIArgs
    []
    [||]
    (testList "Hop.fsx" [ presentTests; callbackTests; portTests; directoryTests; identityCookieTests ])
|> ignore
