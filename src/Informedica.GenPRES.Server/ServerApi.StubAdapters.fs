namespace ServerApi

// The stand-ins for every party outside GenPRES, so that the whole launch, enrolment and
// signing sequence runs on one machine with nothing to install: the IdentityProvider and the
// UserRegistry, the PatientDataPlatform, the Database (credentials, sessions, the record), the
// MailService and the MainEHR LaunchScript. `Server.fs` mounts their pages in full scope only;
// `Adapters.makeAppEnvWith` wires them into the AppEnv. Development and testing only.

open System
open Shared.Types
open Informedica.Utils.Lib.BCL


/// The IdentityProvider and the UserRegistry as one stub directory over an identity choice made
/// on the stub launch page: `prescriber`, `reader`, `prescriber-other-patient`, `no-pin`,
/// `unknown` (a login the registry does not know), `none` (no identity: the stub IdP's
/// `/authorize` never issues a code for it). The active Patient of a choice is the one the
/// launch page was given, except for `prescriber-other-patient`.
module StubDirectory =

    let choices =
        [
            "prescriber"
            "prescriber-b"
            "reader"
            "prescriber-other-patient"
            "no-pin"
            "unknown"
            "none"
        ]


    let identityOf choice =
        {
            Login = choice
            DisplayName =
                match choice with
                | "prescriber" -> "Stub Prescriber"
                // a second Prescriber on the same patient, so that the record can move on
                // under another Session
                | "prescriber-b" -> "Stub Prescriber B"
                | "reader" -> "Stub Reader"
                | "prescriber-other-patient" -> "Stub Prescriber (other patient)"
                | "no-pin" -> "Stub Prescriber (no PIN)"
                | other -> $"Stub {other}"
        }


    /// The address the registry gives for a login. The stub's is derived from it.
    let mailAddressOf (identity: BrowserIdentity) = $"{identity.Login}@stub.example"


    /// The registry's answer for a login, given the Patient the launch page made active. Whether
    /// a PIN is set is not the registry's to say: `no-pin` differs from `prescriber` only in
    /// the credential store's seed.
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
        | "prescriber-b"
        | "no-pin" -> standing UserRole.Prescriber activePatientId
        | "reader" -> standing UserRole.Reader activePatientId
        | "prescriber-other-patient" -> standing UserRole.Prescriber "other-patient"
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


    /// A code lives as long as a Launch; older ones are pruned on the next issue.
    let codeLifetime = TimeSpan.FromMinutes 2.0


    /// One active Patient per login, as MainEHR has: a later launch of the same login for
    /// another Patient makes an earlier, still open launch wrong-patient.
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


/// The PatientDataPlatform stub: one fixed patient for every PatientId, so a launch fills the
/// patient panel from the platform, except that `no-data` has no record at all, so the
/// Session opens on what was signed last, or on nothing.
module StubPatientData =

    /// The stub's reading: ten years, 32 kg, 140 cm, nothing else known.
    let patient: Patient =
        Shared.Models.Patient.create
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
        |> Option.defaultValue Shared.Models.Patient.empty


    /// The reading parsed at the adapter: what the platform gives is a patient, or it is no
    /// reading.
    let port: PatientDataPort =
        {
            read =
                fun pid ->
                    if pid = "no-data" then
                        None
                    else
                        patient |> Patient.parse |> Result.toOption
        }


/// The credential half of the Database, seeded for the stub logins: the Prescribers that sign
/// have the PIN `1234`, `no-pin` has none and enrols, a Reader has no credential because a
/// Reader never signs. On the in-memory store these live as long as the host; on the SQLite
/// store the start writes them once per login and a PIN a User set is never written over.
module StubCredentials =

    /// The PIN every seeded stub Prescriber has. Development and test servers only.
    let stubPin = "1234"


    let seed (newSalt: int -> byte[]) : Map<string, Credential> =
        [
            "prescriber", Credential.withPin newSalt stubPin
            "prescriber-b", Credential.withPin newSalt stubPin
            "prescriber-other-patient", Credential.withPin newSalt stubPin
            "no-pin", Credential.empty
        ]
        |> Map.ofList


/// The MailService stub: an outbox behind a lock and a page that shows it, newest
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


    let private encode (s: string) = System.Net.WebUtility.HtmlEncode s


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


/// The stub LaunchScript as a page the server serves in full scope: it mints a
/// sealed Launch for a chosen PatientId and opens the client on it. Pure here; `Server.fs`
/// mounts `GET /stub/launch` (the page) and `POST /stub/launch` (mint + redirect).
module StubLaunch =

    let path = "/stub/launch"


    /// The Launch lifetime: a page load, the identity round trip, a retry or two.
    let lifetime = TimeSpan.FromMinutes 2.0


    /// The form for a list of identity choices (the stub directory's). No inline script or
    /// style, so the CSP (`default-src 'self'`) holds. The PatientId `no-data` opens a Session
    /// without imported data.
    let pageFor (choices: string list) =
        let options =
            choices
            |> List.map (fun c -> $"""    <option value="{c}">{c}</option>""")
            |> String.concat "\n"

        $"""<!doctype html>
<html lang="en">
<head><meta charset="utf-8"><title>GenPRES stub launch</title></head>
<body>
<h1>Stub LaunchScript</h1>
<p>Stands in for the MainEHR LaunchScript (uc-01 step 1): mints a sealed Launch for the patient
below and opens GenPRES on it. Development and test servers only.</p>
<form method="post" action="{path}">
  <p><label>PatientId <input name="pid" value="stub-patient" required></label>
  <small>(<code>no-data</code>: a patient without imported data)</small></p>
  <p><label>Identity at the browser
  <select name="identity">
{options}
  </select></label>
  <small>(who the stub IdentityProvider says is there; <code>none</code>: nobody)</small></p>
  <button type="submit">Launch</button>
</form>
</body>
</html>"""


    /// The page over the stub directory's choices.
    let page = pageFor StubDirectory.choices


    /// Where the browser goes after minting: the Launch travels in the url hash, which never
    /// reaches the server as a request.
    let launchUrl (launch: Launch) =
        match launch with
        | Launch text -> $"/#/session?launch={Uri.EscapeDataString text}"


    /// Mints the Launch for a posted PatientId; blank falls back to the stub patient.
    let mint (now: DateTime) (newNonce: unit -> string) (key: LaunchSeal.Key) (pid: string) =
        let pid =
            if pid |> String.isNullOrWhiteSpace then
                "stub-patient"
            else
                pid.Trim()

        LaunchSeal.mint
            key
            {
                PatientId = pid
                Nonce = newNonce ()
                Expiry = now + lifetime
            }


    let choices = StubDirectory.choices

    let identityCookieName = "genpres_stub_identity"


    /// The stub identity cookie value for a choice and the Patient the page made active.
    let identityCookie (choice: string) (pid: string) = $"{Uri.EscapeDataString choice}.{Uri.EscapeDataString pid}"


    /// The choice and the Patient back from the cookie; None for anything else.
    let parseIdentityCookie (value: string option) : (string * string) option =
        match value with
        | Some v when v |> String.notEmpty ->
            match v.Split('.') with
            | [| choice; pid |] when choices |> List.contains (Uri.UnescapeDataString choice) ->
                Some(Uri.UnescapeDataString choice, Uri.UnescapeDataString pid)
            | _ -> None
        | _ -> None


/// The Database stub: the session state in memory behind one lock, forgotten at restart. Runs
/// the pure `Session` functions over it; the clock, the ids, the codes and the ports come in
/// as parameters so the tests can fix them.
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
            | Session.StoreOutcome.Conflict winner ->
                SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head winner))
            | _ -> SigningOutcome.Refused SigningRefusal.StoreFailed)
            step
            state


    /// A challenge's writes that did not land: `StoreFailed`.
    let challengeWith persist step state =
        runWith persist (fun _ -> SigningOutcome.Refused SigningRefusal.StoreFailed) step state


    /// A member that answers nothing and one that writes nothing, in the shape `runWith` takes.
    let private asUnit (state, writes) = state, (), writes

    let private noWrites (state, answer) = state, answer, []


    /// The writes of a request with no refusal to give did not land: the call fails.
    let failWith persist step state =
        runWith persist (fun outcome -> invalidOp $"the session store did not take the writes: %A{outcome}") step state


    /// <summary>
    /// What a request carries that names the rows it can touch. The port names it, the store
    /// reads the rows under it, and the pure machine runs over what came back; a store that
    /// keeps the state in memory ignores it.
    /// </summary>
    [<RequireQualifiedAccess>]
    type Slice =
        // the request acts on what it brings: there is nothing to read for it
        | Nothing
        // a Launch presented, by the nonce sealed in it
        | LaunchNonce of string
        // a callback, by the state it carries
        | LaunchState of string
        // the Sessions of a login, so that an open sees the one it supersedes
        | Login of string
        // a Session, by the id in the cookie
        | Session of string
        // an enrolment attempt and its person's rows
        | Enrolment of string
        // a Submission: the Session it comes from, and the answer its key was already given,
        // so that the same Submission sent twice is answered once
        | Submission of sessionId: string * idemKey: string


    /// <summary>
    /// Where the session state lives: `load` puts the rows a slice names into the state before
    /// a request runs, `persist` appends what it wrote. The in-memory store keeps everything in
    /// the state itself; the SQL store reads and writes the database.
    /// </summary>
    type SessionStore =
        {
            load: Slice -> Session.State -> Session.State
            persist: Session.Persist list -> Session.StoreOutcome
        }


    /// The state kept in memory: nothing to load, a write lands by being in it.
    let inMemory =
        {
            load = fun _ s -> s
            persist = persistNothing
        }


    /// <summary>
    /// The session port over a record store: every request runs under one lock; a request
    /// that can touch a patient's record loads it first. A load that throws leaves the state
    /// as it was; a signing request answers it as a store failure, any other request fails.
    /// </summary>
    let makeSessionPortWith
        (store: SessionStore)
        (now: unit -> DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (newSalt: int -> byte[])
        (codeMac: string -> byte[])
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (idp: IdentityProviderPort)
        (registry: UserRegistryPort)
        (patientData: PatientDataPort)
        (mailPort: MailPort)
        (initial: Session.State)
        : SessionPort
        =
        let gate = obj ()
        let mutable state = initial

        // Nothing reaches the MailService before the writes of the request that asked for it
        // have landed. The machine is given a `send` that collects, and what it collected is
        // sent once the store has taken the writes; when the store refuses them the request
        // answers as if it never ran, so its mail is dropped with the rest of it. A mail is
        // fire and forget, so one that throws does not take the request with it.
        let collected = Collections.Generic.List<Mail>()

        let mail = { send = fun m -> lock gate (fun () -> collected.Add m) }

        let flush landed =
            let pending = collected |> List.ofSeq
            collected.Clear()

            if landed then
                for m in pending do
                    try
                        mailPort.send m
                    with _ ->
                        ()

        let persisting writes =
            match store.persist writes with
            | Session.StoreOutcome.Written ->
                flush true
                Session.StoreOutcome.Written
            | outcome ->
                flush false
                outcome

        let update slice f =
            lock
                gate
                (fun () ->
                    collected.Clear()
                    let next, result = failWith persisting f (store.load slice state)
                    state <- next
                    // a step that asked for no writes has nothing that can be refused, so what
                    // it stands on is already in the state and its mail goes out with it
                    flush true
                    result
                )

        let signing slice f =
            lock
                gate
                (fun () ->
                    collected.Clear()

                    match
                        (try
                            Ok(store.load slice state)
                         with _ ->
                             Error())
                    with
                    | Error() -> SigningOutcome.Refused SigningRefusal.StoreFailed
                    | Ok s ->
                        let next, result = f s
                        state <- next
                        flush true
                        result
                )

        // a Launch names its rows only once its seal is read; one that does not verify names
        // nothing, and the machine refuses it in the same breath
        let launchSlice (launch, _) =
            match verify launch with
            | Ok claims -> Slice.LaunchNonce claims.Nonce
            | Error _ -> Slice.Nothing

        // the two halves of a callback, with the login's rows loaded between them: the machine
        // learns the login when the code is redeemed, and the open decides which Session it
        // supersedes from the newest of that login
        let callbackStep cb s =
            match Session.redeem (now ()) idp.redeem registry.standing s cb with
            | Session.Redeemed.Answered(s, result, writes) -> s, result, writes
            | Session.Redeemed.Identified(record, identity, standing) ->
                store.load (Slice.Login identity.Login) s
                |> Session.openAfterRedeem
                    (now ())
                    newId
                    newCode
                    codeMac
                    patientData.read
                    mail.send
                    (record, identity, standing)

        {
            present =
                fun launch ->
                    async {
                        return
                            update
                                (launchSlice launch)
                                (fun s -> Session.present (now ()) newId verify idp.authorizeUrl s launch)
                    }
            callback = fun cb -> async { return update (Slice.LaunchState cb.State) (callbackStep cb) }
            find = fun id -> async { return update (Slice.Session id) (Session.find (now ()) id) }
            close = fun id -> async { return update (Slice.Session id) (Session.close (now ()) id >> asUnit) }
            findEnrolment =
                fun attempt ->
                    async {
                        return update (Slice.Enrolment attempt) (Session.findEnrolment (now ()) attempt >> noWrites)
                    }
            supplyPin =
                fun attempt code pin ->
                    async {
                        return
                            update
                                (Slice.Enrolment attempt)
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
            dropEnrolment =
                fun attempt ->
                    async { return update (Slice.Enrolment attempt) (Session.dropEnrolment (now ()) attempt >> asUnit) }
            challenge =
                fun sid request ->
                    async {
                        return
                            signing
                                (Slice.Session sid)
                                (challengeWith
                                    persisting
                                    (fun s -> Session.challenge (now ()) newId digest patientData.read sid request s))
                    }
            submit =
                fun sid signature ->
                    async {
                        return
                            signing
                                (Slice.Submission(sid, signature.IdemKey))
                                (submitWith
                                    persisting
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
            seen = fun sid opened -> async { return update (Slice.Session sid) (Session.seen (now ()) sid opened) }
            openVersion =
                fun sid id -> async { return update (Slice.Session sid) (Session.openVersion (now ()) newId sid id) }
        }


    /// The session port over an in-memory store.
    let makeSessionPort = makeSessionPortWith inMemory
