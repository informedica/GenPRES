namespace ServerApi

// The stand-ins for every party outside GenPRES, so that the whole launch, enrolment and
// signing sequence runs on one machine with nothing to install: the IdentityProvider and the
// UserRegistry, the PatientDataPlatform, the Database (credentials, sessions, the record), the
// MailService and the MainEHR LaunchScript. `Server.fs` mounts their pages in full scope only;
// `Adapters.makeAppEnvWith` wires them into the AppEnv. Development and testing only.

open System
open Shared.Types


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


    let port: PatientDataPort =
        { read = fun pid -> if pid = "no-data" then None else Some patient }


/// The credential half of the Database, seeded for the stub logins: the Prescribers that sign
/// have the PIN `1234`, `no-pin` has none and enrols, a Reader has no credential because a
/// Reader never signs. Per host start; a PIN set by enrolment lives as long as the host.
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
            if String.IsNullOrWhiteSpace pid then
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


/// The Database stub: the session state in memory behind one lock, forgotten at restart. Runs
/// the pure `Session` functions over it; the clock, the ids, the codes and the ports come in
/// as parameters so the tests can fix them.
module StubDatabase =

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
        (initial: Session.State)
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
                fun launch ->
                    async { return update (fun s -> Session.present (now ()) newId verify idp.authorizeUrl s launch) }
            callback =
                fun cb ->
                    async {
                        return
                            update (fun s ->
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
            find = fun id -> async { return update (Session.find (now ()) id) }
            close = fun id -> async { return update (fun s -> Session.close id s, ()) }
            findEnrolment = fun attempt -> async { return update (Session.findEnrolment (now ()) attempt) }
            supplyPin =
                fun attempt code pin ->
                    async {
                        return
                            update (fun s ->
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
            dropEnrolment = fun attempt -> async { return update (fun s -> Session.dropEnrolment attempt s, ()) }
            challenge =
                fun sid request ->
                    async { return update (fun s -> Session.challenge (now ()) newId patientData.read sid request s) }
            submit =
                fun sid submission ->
                    async {
                        return
                            update (fun s -> Session.commit (now ()) newId registry.standing mail.send sid submission s)
                    }
            seen = fun sid opened -> async { return update (Session.seen (now ()) sid opened) }
            openVersion = fun sid id -> async { return update (Session.openVersion (now ()) newId sid id) }
        }
