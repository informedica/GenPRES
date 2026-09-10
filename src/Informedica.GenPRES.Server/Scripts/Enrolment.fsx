// UC-2 enrolment against server-hosted stubs (plan 615), PR 1: the credential store and the
// MailService, with no change in behaviour yet.
//
// Script-first draft (script-only policy) of:
//   - `UserStanding` without `PinSet` and with the registry's `MailAddress` (Rule 27): the PIN
//     is the Database's to know (Rule 24, uc-02 `ReadCredential`), not the registry's
//     → `ServerApi.Ports.fs`;
//   - `Mail` and `MailPort` (Actor M, edge C10) → `Ports.fs`;
//   - `PinHash` (PBKDF2-SHA256, per-credential salt, constant-time compare) and `Credential`
//     (Concept 7: the PIN and the wrong-count, keyed by the person) → `ServerApi.Adapters.fs`;
//   - `Hop.State.Credentials`, `Hop.initialState`, the callback reading the credential at 5.4,
//     `makeSessionPort` over an initial state → `Adapters.fs`;
//   - `StubMail` (an outbox behind a lock and the `/stub/mail` page) and `StubCredentials`
//     (the seed per stub login: `prescriber` and `prescriber-other-patient` with the PIN
//     `1234`, `no-pin` without one) → `Adapters.fs`; `Server.fs` mounts `GET /stub/mail`
//     behind `not IsProd`;
//   - `StubDirectory` re-stated over the new `UserStanding`.
//
// PR 2 adds the suspended launch: the enrolment attempt, the code as a mac, `supplyPin` as
// one act, and the two mails.
//
// Run: `dotnet fsi Enrolment.fsx` from this directory (build the solution first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open System.Security.Cryptography
open Shared.Types
open Shared.Models
open ServerApi


// ---------------------------------------------------------------------------------------------
// Ports (→ ServerApi.Ports.fs)
// ---------------------------------------------------------------------------------------------

/// What the UserRegistry says about a login at this launch (Rules 5, 6, 27): the User with the
/// Role, the Patient active in MainEHR, and the mail address a confirmation code goes to.
/// Whether a PIN is set is the Database's answer (Rule 24), not the registry's.
type UserStanding =
    {
        User: UserContext
        ActivePatientId: string option
        MailAddress: string
    }


type UserRegistryPort = { standing: BrowserIdentity -> UserStanding option }


/// One mail from the Server to a User (Rule 27): a confirmation code, a notice that the PIN
/// was set, a notice at the wrong-PIN limit.
type Mail =
    {
        To: string
        Subject: string
        Body: string
    }


/// Actor M, the MailService, over edge C10. Sending is fire and forget: the Server records
/// what it sent in the audit (Rule 46, later), not the outcome of delivery.
type MailPort = { send: Mail -> unit }


// ---------------------------------------------------------------------------------------------
// The credential (→ ServerApi.Adapters.fs, before `Hop`)
// ---------------------------------------------------------------------------------------------

/// A PIN as the Database keeps it: never the PIN, a PBKDF2-SHA256 derivation under a salt of
/// its own, so that two Users with the same PIN have nothing in common on disk.
type PinHash =
    {
        Salt: byte[]
        Hash: byte[]
    }


module PinHash =

    /// Enough rounds to make guessing a four-to-six-digit PIN offline slow, few enough for a
    /// signing check to feel immediate. One place to tune.
    let iterations = 100_000

    let saltLength = 16

    let hashLength = 32


    let private derive (salt: byte[]) (pin: string) =
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, hashLength)


    /// Derives the hash of a PIN under a fresh salt from `newSalt` (the CSPRNG in the host).
    let make (newSalt: int -> byte[]) (pin: string) : PinHash =
        let salt = newSalt saltLength

        {
            Salt = salt
            Hash = derive salt pin
        }


    /// Whether the PIN derives to the stored hash, compared in constant time.
    let verify (pin: string) (hash: PinHash) =
        CryptographicOperations.FixedTimeEquals(derive hash.Salt pin, hash.Hash)


/// Concept 7, the UserCredential: what the Database holds per person (keyed by UserId, not by
/// login). No PIN yet is a credential without one; the wrong-count is Rule 28's, counted across
/// Sessions and reset to zero when the PIN is set.
type Credential =
    {
        PinHash: PinHash option
        WrongCount: int
    }


module Credential =

    let empty =
        {
            PinHash = None
            WrongCount = 0
        }


    /// Rule 24: whether a PIN is set.
    let pinSet (credential: Credential) = credential.PinHash.IsSome


    /// A credential with this PIN and a count of zero (Rule 28: setting the PIN resets it).
    let withPin (newSalt: int -> byte[]) (pin: string) : Credential =
        {
            PinHash = Some(PinHash.make newSalt pin)
            WrongCount = 0
        }


// ---------------------------------------------------------------------------------------------
// Hop, re-stated with the credential store (→ ServerApi.Adapters.fs)
// ---------------------------------------------------------------------------------------------

module Hop =

    open ServerApi.Hop

    /// The state gains the credential half of the Database (Concept 7): what the callback reads
    /// at 5.4 (Rule 24) and what enrolment writes (PR 2).
    type State =
        {
            Launches: Map<string, LaunchRecord>
            Sessions: Map<string, SessionRecord>
            Endings: Map<string, SessionEnding * DateTime>
            Credentials: Map<string, Credential>
        }


    let emptyState =
        {
            Launches = Map.empty
            Sessions = Map.empty
            Endings = Map.empty
            Credentials = Map.empty
        }


    /// The state a host starts from: the credentials it was seeded with (the stub's, or a
    /// store's read once the Database exists), nothing else.
    let initialState (credentials: Map<string, Credential>) =
        { emptyState with
            Credentials = credentials
        }


    /// Rule 24, uc-02 `ReadCredential`: the credential of a person, or an empty one.
    let credentialOf (userId: string) (state: State) =
        state.Credentials |> Map.tryFind userId |> Option.defaultValue Credential.empty


    let private dropExpired (now: DateTime) (state: State) =
        { state with
            Launches = state.Launches |> Map.filter (fun _ r -> now <= r.Expiry)
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

        { state with
            Launches = state.Launches |> Map.add record.Nonce { record with Outcome = Some outcome }
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
        CallbackResult.Opened(id, openedUrl)


    let private refuse (record: LaunchRecord) refusal (state: State) =
        { state with
            Launches =
                state.Launches
                |> Map.add record.Nonce { record with Outcome = Some(LaunchResult.Refused refusal) }
        },
        CallbackResult.Refused(refusal, refusedUrl refusal)


    /// Step 4.5 and step 5, as before, except that 5.4 reads the credential from the state
    /// (Rule 24) instead of asking the registry: a Prescriber whose credential has no PIN is
    /// still refused here; PR 2 suspends the launch instead.
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
                    // 5.4, Rule 24: the credential is the Database's; Rule 25 binds Prescribers
                    // only, a Reader is never asked (Rule 26, ext 5c)
                    | Some standing when
                        standing.User.Role = UserRole.Prescriber
                        && not (credentialOf standing.User.UserId state |> Credential.pinSet)
                        ->
                        refuse record LaunchRefusal.EnrolmentRequired state
                    | Some standing -> openSession now newId patientData record standing state
        | _, None -> state, invalid
        | _ -> state, invalid


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


    /// The port over a single mutable state guarded by a lock, started from `initial`.
    let makeSessionPort
        (now: unit -> DateTime)
        (newId: unit -> string)
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (idp: IdentityProviderPort)
        (registry: UserRegistryPort)
        (patientData: PatientDataPort)
        (initial: State)
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
                            update (fun s -> callback (now ()) newId idp.redeem registry.standing patientData.read s cb)
                    }
            find = fun id -> async { return update (find id) }
            close = fun id -> async { return update (fun s -> close id s, ()) }
        }


// ---------------------------------------------------------------------------------------------
// The stubs (→ ServerApi.Adapters.fs)
// ---------------------------------------------------------------------------------------------

/// The IdentityProvider and the UserRegistry as one stub directory over an identity choice made
/// on the stub launch page. Re-stated over the new `UserStanding`: the registry answers the
/// mail address (`<login>@stub.example`) and no longer whether a PIN is set.
module StubDirectory =

    open ServerApi.StubDirectory

    /// Rule 27: the address the registry gives for a login. The stub's is derived from it.
    let mailAddressOf (identity: BrowserIdentity) = $"{identity.Login}@stub.example"


    /// The registry's answer for a login, given the Patient the launch page made active.
    let standingOf (activePatientId: string) (identity: BrowserIdentity) : UserStanding option =
        let standing role activePatientId =
            Some
                {
                    User =
                        {
                            UserId = identity.Login
                            DisplayName = identity.DisplayName
                            Role = role
                        }
                    ActivePatientId = Some activePatientId
                    MailAddress = mailAddressOf identity
                }

        match identity.Login with
        | "prescriber"
        | "no-pin" -> standing UserRole.Prescriber activePatientId
        | "reader" -> standing UserRole.Reader activePatientId
        | "prescriber-other-patient" -> standing UserRole.Prescriber "other-patient"
        | _ -> None


    type Directory =
        {
            idp: IdentityProviderPort
            registry: UserRegistryPort
            issue: string -> string -> string
        }


    let make (now: unit -> DateTime) (newCode: unit -> string) : Directory =
        let gate = obj ()
        let codes = Collections.Generic.Dictionary<string, BrowserIdentity * DateTime>()
        let active = Collections.Generic.Dictionary<string, string>()

        let prune () =
            let cutoff = now () - codeLifetime

            for stale in
                codes
                |> Seq.filter (fun kv -> snd kv.Value < cutoff)
                |> Seq.map _.Key
                |> Seq.toList do
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


/// The credential half of the Database, seeded for the stub logins: the Prescribers that sign
/// have the PIN `1234`, `no-pin` has none and enrols (UC-2), a Reader has no credential
/// (Rule 26). Per host start; a PIN set by enrolment lives as long as the host.
module StubCredentials =

    /// The PIN every seeded stub Prescriber has. Development and test servers only.
    let stubPin = "1234"


    let seed (newSalt: int -> byte[]) : Map<string, Credential> =
        [
            "prescriber", Credential.withPin newSalt stubPin
            "prescriber-other-patient", Credential.withPin newSalt stubPin
            "no-pin", Credential.empty
        ]
        |> Map.ofList


/// The MailService stub (Actor M): an outbox behind a lock and a page that shows it, newest
/// first, so the tester reads a confirmation code where a User would read their mail.
/// `Server.fs` mounts `GET /stub/mail` in full scope only.
module StubMail =

    let path = "/stub/mail"


    type Outbox =
        {
            port: MailPort
            // newest first
            sent: unit -> Mail list
        }


    let make () : Outbox =
        let gate = obj ()
        let mutable mails: Mail list = []

        {
            port = { send = fun mail -> lock gate (fun () -> mails <- mail :: mails) }
            sent = fun () -> lock gate (fun () -> mails)
        }


    let private encode (s: string) = Net.WebUtility.HtmlEncode s


    /// The outbox as a page. No inline script or style, so the CSP (`default-src 'self'`)
    /// holds; every field is HTML-encoded, the body keeps its line breaks.
    let page (mails: Mail list) =
        let items =
            match mails with
            | [] -> "<p>No mail sent yet.</p>"
            | mails ->
                mails
                |> List.map (fun m ->
                    $"""<article>
<h2>{encode m.Subject}</h2>
<p>To: <code>{encode m.To}</code></p>
<pre>{encode m.Body}</pre>
</article>"""
                )
                |> String.concat "\n"

        $"""<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>GenPRES stub mail</title></head>
<body>
<h1>Stub MailService outbox</h1>
<p>Stands in for the MailService (uc-02, Rule 27): every mail the server sent since it started,
newest first. Development and test servers only.</p>
{items}
</body>
</html>"""


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

/// A deterministic salt source for the tests.
let salts (n: int) = Array.init n byte

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


let fixture () =
    let ids = counter "id"
    let directory = StubDirectory.make (fun () -> t0) (counter "code")
    ids, directory


/// The state a stub host starts from.
let seeded = Hop.initialState (StubCredentials.seed salts)


/// Presents, then plays the stub IdP for `choice`, and returns the callback the browser brings.
let hop (ids: unit -> string) (directory: StubDirectory.Directory) state launch key choice =
    let state, result =
        Hop.present t0 ids (verifyAt t0) directory.idp.authorizeUrl state (launch, key)

    match result with
    | LaunchResult.RedirectTo(_, st) ->
        state,
        {
            State = st
            StateCookie = Some st
            Code = Some(directory.issue choice "patient-1")
            Error = None
        }
    | other -> failtest $"expected RedirectTo, got {other}"


let run (ids: unit -> string) (directory: StubDirectory.Directory) state cb =
    Hop.callback t0 ids directory.idp.redeem directory.registry.standing StubPatientData.port.read state cb


let pinHashTests =
    testList
        "PinHash"
        [
            test "the PIN it was made from verifies" {
                let hash = PinHash.make salts "1234"
                hash |> PinHash.verify "1234" |> Expect.isTrue "verifies"
            }

            test "another PIN does not" {
                let hash = PinHash.make salts "1234"
                hash |> PinHash.verify "1235" |> Expect.isFalse "wrong PIN"
                hash |> PinHash.verify "" |> Expect.isFalse "empty PIN"
            }

            test "the same PIN under another salt is another hash" {
                let a = PinHash.make salts "1234"
                let b = PinHash.make (fun n -> Array.init n (fun i -> byte (i + 1))) "1234"
                a.Hash |> Expect.notEqual "different hashes" b.Hash
                b |> PinHash.verify "1234" |> Expect.isTrue "still verifies"
            }

            test "the hash is never the PIN, and has the declared lengths" {
                let hash = PinHash.make salts "1234"
                hash.Salt.Length |> Expect.equal "salt" PinHash.saltLength
                hash.Hash.Length |> Expect.equal "hash" PinHash.hashLength
                Text.Encoding.UTF8.GetString hash.Hash |> Expect.notEqual "not the PIN" "1234"
            }
        ]


let credentialTests =
    testList
        "Credential"
        [
            test "an empty credential has no PIN set (Rule 24)" {
                Credential.empty |> Credential.pinSet |> Expect.isFalse "no PIN"
            }

            test "withPin sets the PIN and a count of zero (Rule 28)" {
                let c = Credential.withPin salts "1234"
                c |> Credential.pinSet |> Expect.isTrue "set"
                c.WrongCount |> Expect.equal "zero" 0
                c.PinHash |> Option.map (PinHash.verify "1234") |> Expect.equal "verifies" (Some true)
            }

            test "the stub seed: the signing Prescribers have the stub PIN, no-pin has none" {
                let seed = StubCredentials.seed salts

                for login in [ "prescriber"; "prescriber-other-patient" ] do
                    seed[login] |> Credential.pinSet |> Expect.isTrue $"{login} set"

                    seed[login].PinHash
                    |> Option.map (PinHash.verify StubCredentials.stubPin)
                    |> Expect.equal $"{login} verifies" (Some true)

                seed["no-pin"] |> Credential.pinSet |> Expect.isFalse "no-pin unset"
                seed |> Map.containsKey "reader" |> Expect.isFalse "a Reader has no credential"
            }

            test "credentialOf answers the empty credential for a person the store does not know" {
                Hop.credentialOf "nobody" seeded |> Expect.equal "empty" Credential.empty
                Hop.credentialOf "no-pin" seeded |> Expect.equal "seeded" Credential.empty
                Hop.credentialOf "prescriber" seeded |> Credential.pinSet |> Expect.isTrue "seeded with PIN"
            }
        ]


let standingTests =
    testList
        "StubDirectory.standing"
        [
            test "the registry answers the mail address and no longer the PIN" {
                let _, d = fixture ()
                let code = d.issue "prescriber" "patient-1"
                let identity = d.idp.redeem code |> Option.get

                match d.registry.standing identity with
                | Some standing ->
                    standing.MailAddress |> Expect.equal "address" "prescriber@stub.example"
                    standing.ActivePatientId |> Expect.equal "active" (Some "patient-1")
                    standing.User.Role |> Expect.equal "role" UserRole.Prescriber
                | None -> failtest "expected a standing"
            }

            test "no-pin is a Prescriber with the launch's patient active" {
                let _, d = fixture ()
                let identity = d.issue "no-pin" "patient-1" |> d.idp.redeem |> Option.get
                let standing = d.registry.standing identity |> Option.get
                standing.User.Role |> Expect.equal "role" UserRole.Prescriber
                standing.ActivePatientId |> Expect.equal "active" (Some "patient-1")
            }

            test "unknown has no standing" {
                let _, d = fixture ()
                let identity = d.issue "unknown" "patient-1" |> d.idp.redeem |> Option.get
                d.registry.standing identity |> Expect.isNone "unknown"
            }
        ]


let callbackTests =
    testList
        "Hop.callback over the credential store"
        [
            test "a Prescriber with a PIN opens" {
                let ids, d = fixture ()
                let state, cb = hop ids d seeded launch1 keyA "prescriber"
                let state, result = run ids d state cb

                match result with
                | CallbackResult.Opened(id, _) ->
                    state.Sessions[id].Session.User
                    |> Option.map _.UserId
                    |> Expect.equal "user" (Some "prescriber")
                | other -> failtest $"expected Opened, got {other}"
            }

            test "a Prescriber whose credential has no PIN is refused, as before PR 2 (Rule 25)" {
                let ids, d = fixture ()
                let state, cb = hop ids d seeded launch1 keyA "no-pin"
                let state, result = run ids d state cb

                result
                |> Expect.equal
                    "enrolment"
                    (CallbackResult.Refused(LaunchRefusal.EnrolmentRequired, "/#/session?refused=enrolment"))

                state.Sessions |> Map.isEmpty |> Expect.isTrue "no session (Rule 7)"
            }

            test "a Prescriber the store does not know at all has no PIN either" {
                let ids, d = fixture ()
                let state, cb = hop ids d Hop.emptyState launch1 keyA "prescriber"
                let _, result = run ids d state cb

                result
                |> Expect.equal
                    "enrolment"
                    (CallbackResult.Refused(LaunchRefusal.EnrolmentRequired, "/#/session?refused=enrolment"))
            }

            test "a Reader is never asked for a PIN (Rule 26)" {
                let ids, d = fixture ()
                let state, cb = hop ids d Hop.emptyState launch1 keyA "reader"
                let _, result = run ids d state cb

                match result with
                | CallbackResult.Opened _ -> ()
                | other -> failtest $"expected Opened, got {other}"
            }

            test "a PIN set in the store lets the next launch open" {
                let ids, d = fixture ()

                let state =
                    { seeded with
                        Credentials = seeded.Credentials |> Map.add "no-pin" (Credential.withPin salts "2468")
                    }

                let state, cb = hop ids d state launch1 keyA "no-pin"
                let _, result = run ids d state cb

                match result with
                | CallbackResult.Opened _ -> ()
                | other -> failtest $"expected Opened, got {other}"
            }
        ]


let mailTests =
    testList
        "StubMail"
        [
            test "the outbox lists what was sent, newest first" {
                let outbox = StubMail.make ()
                outbox.sent () |> Expect.isEmpty "nothing yet"

                outbox.port.send
                    {
                        To = "a@stub.example"
                        Subject = "first"
                        Body = "1"
                    }

                outbox.port.send
                    {
                        To = "b@stub.example"
                        Subject = "second"
                        Body = "2"
                    }

                outbox.sent () |> List.map _.Subject |> Expect.equal "newest first" [ "second"; "first" ]
            }

            test "the page shows every mail, HTML-encoded, and says when there is none" {
                StubMail.page [] |> Expect.stringContains "empty" "No mail sent yet"

                let page =
                    StubMail.page
                        [
                            {
                                To = "a@stub.example"
                                Subject = "Your code <b>"
                                Body = "code 123456\n& more"
                            }
                        ]

                page |> Expect.stringContains "subject encoded" "Your code &lt;b&gt;"
                page |> Expect.stringContains "body encoded" "code 123456\n&amp; more"
                page |> Expect.stringContains "address" "a@stub.example"
                page.Contains "<script" |> Expect.isFalse "no script"
            }
        ]


let portTests =
    testList
        "makeSessionPort over an initial state"
        [
            testAsync "the seeded store decides the PIN question" {
                let ids, d = fixture ()

                let port =
                    Hop.makeSessionPort (fun () -> t0) ids (verifyAt t0) d.idp d.registry StubPatientData.port seeded

                let! redirect = port.present (launch1, keyA)

                let st =
                    match redirect with
                    | LaunchResult.RedirectTo(_, st) -> st
                    | other -> failtest $"{other}"

                let! result =
                    port.callback
                        {
                            State = st
                            StateCookie = Some st
                            Code = Some(d.issue "no-pin" "patient-1")
                            Error = None
                        }

                result
                |> Expect.equal
                    "enrolment"
                    (CallbackResult.Refused(LaunchRefusal.EnrolmentRequired, "/#/session?refused=enrolment"))
            }
        ]


let tests =
    testList
        "Enrolment PR 1"
        [
            pinHashTests
            credentialTests
            standingTests
            callbackTests
            mailTests
            portTests
        ]


runTestsWithCLIArgs [] [||] tests
