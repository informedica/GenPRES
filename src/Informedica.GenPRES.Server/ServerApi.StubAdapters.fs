namespace ServerApi

// Stand-ins for the systems outside GenPRES: the identity provider, the user registry, the EHR patient data, the
// database, the mail service and the EHR launch script. With them the launch, enrolment and signing flow runs on
// one machine. Server.fs mounts their pages in full scope only. Development and testing only.

open System
open Informedica.GenPRES.Shared.Types
open Informedica.Utils.Lib.BCL


/// A stub identity provider and user registry for the identity choices on the stub launch page. The choice unknown
/// is a login the registry does not know; the choice none has no identity, so the server issues no code for it.
/// The active patient of a choice is the patient picked on the launch page, except for prescriber-other-patient.
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
                | "prescriber" -> "Prescriber A"
                // A second prescriber on the same patient, so the record can change under another session.
                | "prescriber-b" -> "Prescriber B"
                | "reader" -> "Stub Reader"
                | "prescriber-other-patient" -> "Stub Prescriber (other patient)"
                | "no-pin" -> "Stub Prescriber (no PIN)"
                | other -> $"Stub {other}"
        }


    /// Returns the mail address of a login: the login at stub.example.
    let mailAddressOf (identity: BrowserIdentity) = $"{identity.Login}@stub.example"


    /// Returns the registry entry of a login with the given active patient, or None for an unknown login.
    /// The registry does not say whether a PIN is set; no-pin differs from prescriber only in the credential seed.
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


    /// The stub identity provider and registry, with issue, which takes an identity choice and an active patient id
    /// and returns a one-time code. The stub authorize page calls issue with the values from the identity cookie.
    type Directory =
        {
            idp: IdentityProviderPort
            registry: UserRegistryPort
            issue: string -> string -> string
        }


    /// How long a one-time code stays valid. Expired codes are removed on the next issue.
    let codeLifetime = TimeSpan.FromMinutes 2.0


    /// Makes the stub directory. A login has one active patient, as in the EHR: a later launch of the same login
    /// for another patient makes an earlier open launch a launch for the wrong patient.
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


/// The stub EHR: the test patients by their patient id, and one fixed patient for any other patient id. The
/// patient id no-data has no record, so the session opens on the last signed data or on no data.
module StubEhrPatient =

    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.Patients


    /// The stub patient's name.
    let name = "Test Patient"


    /// The stub patient's birthdate: the fifteenth of March, 2016.
    let birthDate =
        BirthDate.create (Conversions.yearFromInt 2016) (Conversions.monthFromInt 3) (Conversions.dayFromInt 15)


    /// The date of the stub measurements: the day the patient is 3650 days old, 13 March 2026.
    let measuredOn = (birthDate |> BirthDate.toDate).AddDays(AgeValue.ten |> AgeValue.getAgeInDays |> float)


    /// The stub patient for a patient id, with the ages calculated at the moment of the read.
    let patient (now: DateTime) (pid: string) : CorePatient =
        let weight = WeightAtDate.create (Some measuredOn) (WeightValue.weightInKg 32m)
        let height = HeightAtDate.create (Some measuredOn) (HeightValue.heightInCm 140m)

        PatientData.create
            Department.unknown
            [||]
            UnknownGender
            None
            (Weight.create [ weight ] None [] (Some(Measured weight)))
            (Height.create [ height ] None (Some(Measured height)))
            None
        |> Patient.createAt now (PatientIdentity.create pid name birthDate |> Some)


    /// The fixed stub patient as contract patient data, as the server sends it: 10 years, 32 kg and 140 cm,
    /// nothing else known.
    let contractPatient = patient measuredOn "any" |> _.Data |> Patient.ofPatientData


    /// A patient of the stub EHR that the stub launch page offers by its PatientId.
    type TestPatient =
        {
            /// The PatientId the launch page posts.
            Id: string
            /// The name the patient panel shows.
            Name: string
            /// The ward the patient is admitted to.
            Department: Department
            /// The gender.
            Gender: Gender
            /// The age at the moment the EHR data is read.
            Age: AgeValue
            /// The weight, measured on the day the EHR data is read.
            Weight: WeightValue
            /// The height, measured on the day the EHR data is read.
            Height: HeightValue
            /// The gestational age at birth; absent when the EHR has none.
            GestationalAge: AgeWeeksDays option
        }


    /// A NEO patient of a gender, 2 days old, with a gestational age in weeks, a weight in gram and a
    /// height in cm.
    let neoPatient gender weeks gram cm =
        {
            Id = $"neo-%i{weeks}wk"
            Name = $"NEO %i{weeks} weeks"
            Department = Department.neonatalICU "NEO"
            Gender = gender
            Age = AgeValue.create None None None (Some(Conversions.dayFromInt 2))
            Weight = WeightValue.weightInGram gram
            Height = HeightValue.heightInCm cm
            GestationalAge = weeks |> Conversions.weekFromInt |> AgeWeeksDays.fromWeeks |> Some
        }


    /// The name of a GenCORE age category in a PatientId and a patient name.
    let ageTypeName =
        function
        | NewBorn -> "newborn"
        | Infant -> "infant"
        | Toddler -> "toddler"
        | Child -> "child"
        | Adolescent -> "adolescent"
        | Adult -> "adult"
        | UnknownAgeType -> "unknown"


    /// An ICK patient at the age where a GenCORE age category starts, with the normal weight and
    /// height of that category for the gender of the patient.
    let ickPatient gender ageType =
        match WeightValue.fromAgeType gender ageType, HeightValue.fromAgeType gender ageType with
        | Some weight, Some height ->
            {
                Id = $"ick-%s{ageType |> ageTypeName}"
                Name = $"ICK %s{ageType |> ageTypeName}"
                Department = Department.pediatricICU "ICK"
                Gender = gender
                Age = AgeValue.fromAgeType ageType
                Weight = weight
                Height = height
                GestationalAge = None
            }
        | _ -> invalidArg (nameof ageType) $"No normal values for the age category %A{ageType}"


    /// The test patients the stub launch page offers: the NEO patients, then the ICK patients, male and
    /// female in turn.
    let testPatients =
        [
            neoPatient Male 26 600m 35m
            neoPatient Female 28 800m 36m
            neoPatient Male 31 1100m 40m
            neoPatient Female 34 1800m 42m
            neoPatient Male 37 2400m 50m
            neoPatient Female 40 4000m 55m
            ickPatient Male NewBorn
            ickPatient Female Infant
            ickPatient Male Toddler
            ickPatient Female Child
            ickPatient Male Adolescent
            ickPatient Female Adult
        ]


    /// Creates a test patient on the date of now. The birth date gives the patient the age of the test patient on
    /// that date, and the weight and height are measured that day.
    let testPatient (now: DateTime) tp =
        let measuredOn = now.Date
        let weight = WeightAtDate.create (Some measuredOn) tp.Weight
        let height = HeightAtDate.create (Some measuredOn) tp.Height

        let identity =
            measuredOn.AddDays(-(tp.Age |> AgeValue.getAgeInDays |> float))
            |> BirthDate.fromDate
            |> PatientIdentity.create tp.Id tp.Name
            |> Some

        PatientData.create
            tp.Department
            [||]
            tp.Gender
            None
            (Weight.create [ weight ] None [] (Some(Measured weight)))
            (Height.create [ height ] None (Some(Measured height)))
            tp.GestationalAge
        |> Patient.createAt measuredOn identity


    /// Reads the EHR data for a patient id: a test patient, None for no-data, and the fixed patient for any other id.
    let read (now: unit -> DateTime) pid =
        match testPatients |> List.tryFind (fun tp -> tp.Id = pid) with
        | Some tp -> tp |> testPatient (now ()) |> Some
        | None when pid = "no-data" -> None
        | None -> Some(patient (now ()) pid)


    /// The stub EHR port on a clock. patientData returns the data of the patient read, without estimates.
    let portAt now : EhrPatientPort =
        {
            read = read now
            patientData = _.Data
        }


    /// The stub EHR port on the current local time in the default time zone.
    let port = portAt (fun () -> DateTime.UtcNow |> LocalDate.ofUtc (LocalDate.defaultZone ()))


/// The seed credentials for the stub logins: the prescribers have the PIN 1234, no-pin has no PIN and must enrol,
/// and the reader has no credential because a reader never signs.
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


/// The stub mail service: an outbox behind a lock and a page that lists the mail newest first, so a tester can
/// read a confirmation code. Server.fs mounts GET /stub/mail in full scope only.
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


    /// Renders the outbox as an HTML page without script. Every field is HTML-encoded and the body keeps its line
    /// breaks.
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


/// The stub EHR launch script: a page that mints a sealed launch for a chosen patient id and opens the client with
/// it. Server.fs mounts GET /stub/launch for the page and POST /stub/launch to mint and redirect, in full scope only.
module StubLaunch =

    let path = "/stub/launch"


    /// How long a launch stays valid: long enough for a page load, the identity round trip and a retry or two.
    let lifetime = TimeSpan.FromMinutes 2.0


    /// The PatientId of the fixed stub patient, launched when no PatientId is given.
    let defaultPatientId = "123456"


    /// The test patient checked on the launch page: the ICK adolescent of 10 years.
    let checkedPatientId = "ick-adolescent"


    /// The radio button value that launches the patient id typed in the text field.
    let otherPatient = "other"


    open Informedica.GenCore.Lib.Patients


    /// Text made safe to put in the page.
    let encode (s: string) = System.Net.WebUtility.HtmlEncode s


    /// The columns of a test patient in the patient table: gender, age, gestational age, weight and height.
    let columns (tp: StubEhrPatient.TestPatient) =
        let count n unit =
            match n with
            | 0 -> None
            | 1 -> Some $"1 %s{unit}"
            | _ -> Some $"%i{n} %s{unit}s"

        let age =
            [
                tp.Age.Years |> Option.bind (fun n -> count (int n) "year")
                tp.Age.Months |> Option.bind (fun n -> count (int n) "month")
                tp.Age.Weeks |> Option.bind (fun n -> count (int n) "week")
                tp.Age.Days |> Option.bind (fun n -> count (int n) "day")
            ]
            |> List.choose id
            |> function
                | [] -> "0 days"
                | parts -> parts |> String.concat " "

        let gestationalAge =
            match tp.GestationalAge with
            | Some ga when int ga.Days = 0 -> $"%i{int ga.Weeks} weeks"
            | Some ga -> $"%i{int ga.Weeks} weeks %i{int ga.Days} days"
            | None -> "–"

        let weight =
            match tp.Weight with
            | Kilogram kg -> $"%M{decimal kg} kg"
            | Gram g -> $"%i{int g} g"

        let height = $"%M{tp.Height |> HeightValue.getHeightCm |> decimal} cm"

        let gender =
            match tp.Gender with
            | Male -> "male"
            | Female -> "female"
            | UnknownGender -> "–"

        [ gender; age; gestationalAge; weight; height ]


    /// What an identity on the launch page stands for.
    let identityHint =
        function
        | "prescriber" -> "Prescriber A, signs with PIN 1234"
        | "prescriber-b" -> "Prescriber B on the same patient, signs with PIN 1234"
        | "reader" -> "a Reader, who never signs"
        | "prescriber-other-patient" -> "a Prescriber whose active patient is another patient"
        | "no-pin" -> "a Prescriber without a PIN, who enrols first"
        | "unknown" -> "a login the registry does not know"
        | "none" -> "nobody signed on at the browser"
        | _ -> ""


    /// A row of the patient table with a radio button, checked for the default patient.
    let patientRow isChecked id name (cells: string list) =
        let checkedAttr = if isChecked then " checked" else ""

        let cells =
            cells
            |> List.mapi (fun i c ->
                let cls = if i >= 3 then " class=\"num\"" else ""
                $"<td%s{cls}>%s{encode c}</td>"
            )
            |> String.concat ""

        $"""      <tr><td><input type="radio" name="patient" id="p-%s{id}" value="%s{id}"%s{checkedAttr}></td><td><label for="p-%s{id}">%s{encode name}</label></td>%s{cells}</tr>"""


    /// The rows of the patient table: the test patients per department, and other.
    let patientRows =
        let groups =
            StubEhrPatient.testPatients
            |> List.groupBy (fun tp -> tp.Department |> Department.name |> Option.defaultValue "")

        [
            for department, tps in groups do
                $"""      <tr class="group"><th colspan="7">%s{encode department}</th></tr>"""

                for tp in tps do
                    patientRow (tp.Id = checkedPatientId) tp.Id tp.Name (columns tp)
            $"""      <tr class="other"><td><input type="radio" name="patient" id="p-%s{otherPatient}" value="%s{otherPatient}"></td><td><label for="p-%s{otherPatient}">Other</label></td><td colspan="5"><input name="pid" aria-label="PatientId" placeholder="PatientId"> <small><code>no-data</code>: a patient without imported data</small></td></tr>"""
        ]
        |> String.concat "\n"


    /// The style sheet of the launch page, inline because the content security policy allows inline styles.
    let style =
        """
:root { color-scheme: light dark; --bg: #f4f6f8; --card: #ffffff; --text: #1d2329; --muted: #5f6b76;
  --line: #dde3e8; --accent: #1565c0; --on-accent: #ffffff; --accent-soft: #e3eefb; --warn: #8a5a00; --warn-bg: #fff3d6; }
@media (prefers-color-scheme: dark) {
  :root { --bg: #14181c; --card: #1d2328; --text: #e6eaee; --muted: #9aa6b1; --line: #2e363d;
    --accent: #64a8f0; --on-accent: #0b1a2a; --accent-soft: #1d3048; --warn: #f0c46a; --warn-bg: #3a2e14; }
}
* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--text);
  font: 15px/1.45 system-ui, -apple-system, "Segoe UI", Roboto, sans-serif; }
main { max-width: 780px; margin: 32px auto; padding: 0 16px; }
.card { background: var(--card); border: 1px solid var(--line); border-radius: 12px; padding: 24px 28px; }
h1 { margin: 0 0 4px; font-size: 22px; }
.badge { display: inline-block; margin-left: 8px; padding: 2px 10px; border-radius: 999px; vertical-align: middle;
  font-size: 12px; font-weight: 600; color: var(--warn); background: var(--warn-bg); }
.lead { margin: 0 0 20px; color: var(--muted); }
fieldset { margin: 0 0 24px; padding: 0; border: 0; }
legend { margin-bottom: 8px; font-size: 16px; font-weight: 600; }
.table-wrap { overflow-x: auto; border: 1px solid var(--line); border-radius: 8px; }
table { width: 100%; border-collapse: collapse; }
th, td { padding: 7px 10px; text-align: left; white-space: nowrap; border-bottom: 1px solid var(--line); }
thead th { font-size: 12px; letter-spacing: .04em; text-transform: uppercase; color: var(--muted); }
tr.group th { background: var(--bg); font-size: 12px; letter-spacing: .06em; color: var(--muted); }
tbody tr:last-child td { border-bottom: 0; }
tbody tr:has(input[name=patient]:checked) { background: var(--accent-soft); }
td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }
label { cursor: pointer; }
input[type=radio] { accent-color: var(--accent); }
input[name=pid] { width: 12em; padding: 4px 8px; font: inherit; color: var(--text); background: var(--card);
  border: 1px solid var(--line); border-radius: 6px; }
small { color: var(--muted); }
.segments { display: flex; flex-wrap: wrap; gap: 6px; }
.segment { position: relative; padding: 5px 12px; border: 1px solid var(--line); border-radius: 999px; }
.segment input { position: absolute; opacity: 0; width: 1px; height: 1px; }
.segment:has(input:checked) { color: var(--on-accent); background: var(--accent); border-color: var(--accent); }
.segment:has(input:focus-visible) { outline: 2px solid var(--accent); outline-offset: 2px; }
dl.hints { display: grid; grid-template-columns: max-content 1fr; gap: 2px 12px; margin: 12px 0 0;
  font-size: 13px; color: var(--muted); }
dl.hints dt { font-family: ui-monospace, Menlo, monospace; }
dl.hints dd { margin: 0; }
.actions { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
button { padding: 10px 28px; font: inherit; font-weight: 600; color: var(--on-accent); background: var(--accent);
  border: 0; border-radius: 8px; cursor: pointer; }
.note { margin: 12px 0 0; }
a { color: var(--accent); }
"""


    /// Renders the launch page for a list of identity choices: a table of the test patients and a row of identities
    /// with the first one checked. A launch opens GenPRES in a new tab, as the EHR does. The page has no script.
    /// The patient id no-data opens a session without imported data.
    let pageFor (choices: string list) =
        let identities =
            choices
            |> List.mapi (fun i c ->
                let checkedAttr = if i = 0 then " checked" else ""
                $"""      <label class="segment"><input type="radio" name="identity" value="%s{c}"%s{checkedAttr}>%s{c}</label>"""
            )
            |> String.concat "\n"

        let hints =
            choices
            |> List.map (fun c -> $"""      <dt>%s{c}</dt><dd>%s{identityHint c}</dd>""")
            |> String.concat "\n"

        $"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>GenPRES Test Launch</title>
<style>%s{style}</style>
</head>
<body>
<main>
<form class="card" method="post" action="%s{path}" target="_blank" rel="noopener">
  <h1>GenPRES Test Launch <span class="badge">development and test only</span></h1>
  <p class="lead">Opens GenPRES for the chosen patient and user, the way the hospital's EHR does when a user
  starts GenPRES from a patient record.</p>
  <fieldset>
    <legend>Patient</legend>
    <div class="table-wrap">
    <table>
      <thead><tr><th></th><th>Patient</th><th>Gender</th><th>Age</th><th>GA</th><th class="num">Weight</th><th class="num">Height</th></tr></thead>
      <tbody>
%s{patientRows}
      </tbody>
    </table>
    </div>
  </fieldset>
  <fieldset>
    <legend>Identity at the browser</legend>
    <div class="segments">
%s{identities}
    </div>
    <dl class="hints">
%s{hints}
    </dl>
  </fieldset>
  <div class="actions">
    <a href="%s{StubMail.path}" target="_blank" rel="noopener">Mail outbox</a>
    <button type="submit">Launch</button>
  </div>
  <p class="note"><small>Opens GenPRES in a new tab, as the EHR does. The tabs of a browser share one
  session: a newer launch ends the session of an older tab.</small></p>
</form>
</main>
</body>
</html>"""


    /// The launch page for the identity choices of the stub directory.
    let page = pageFor StubDirectory.choices


    /// Returns the patient id to launch: the chosen patient, or the typed patient id, trimmed, when Other or nothing
    /// is chosen; the fixed patient when that is blank. The launch and the identity cookie both use this id.
    let patientIdOf (patient: string) (pid: string) =
        let pid =
            if patient |> String.isNullOrWhiteSpace || patient = otherPatient then
                pid
            else
                patient

        if pid |> String.isNullOrWhiteSpace then
            defaultPatientId
        else
            pid.Trim()


    /// Returns the client url for a launch. The launch goes in the url fragment, which the browser never sends to
    /// the server.
    let launchUrl (launch: Launch) =
        match launch with
        | Launch text -> $"/#/session?launch={Uri.EscapeDataString text}"


    /// Mints a sealed launch for a patient id, trimmed; a blank id gives the fixed stub patient.
    let mint (now: DateTime) (newNonce: unit -> string) (key: LaunchSeal.Key) (pid: string) =
        let pid =
            if pid |> String.isNullOrWhiteSpace then
                defaultPatientId
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


    /// Returns the identity cookie value for a choice and the patient id the launch page made active.
    let identityCookie (choice: string) (pid: string) = $"{Uri.EscapeDataString choice}.{Uri.EscapeDataString pid}"


    /// Parses the choice and the patient id from the identity cookie; None for a missing value or an unknown choice.
    let parseIdentityCookie (value: string option) : (string * string) option =
        match value with
        | Some v when v |> String.notEmpty ->
            match v.Split('.') with
            | [| choice; pid |] when choices |> List.contains (Uri.UnescapeDataString choice) ->
                Some(Uri.UnescapeDataString choice, Uri.UnescapeDataString pid)
            | _ -> None
        | _ -> None


/// The session port: runs the Session functions over a session store behind one lock. The in-memory store loses
/// the state on restart. The clock, ids, codes and ports are parameters so the tests can fix them.
module StubDatabase =

    /// Computes the SHA-256 digest of the canonical serialization of an order plan Dto, so two equal plans give
    /// the same digest whatever JSON the client sent.
    let digest (plan: Informedica.GenOrder.Lib.Types.OrderPlan) =
        plan
        |> Informedica.GenOrder.Lib.OrderPlan.Dto.toDto
        |> Informedica.GenOrder.Lib.Canonical.serialize
        |> System.Text.Encoding.UTF8.GetBytes
        |> System.Security.Cryptography.SHA256.HashData
        |> Convert.ToHexString


    /// The persist function of the in-memory store: there is nothing to write, so it returns Written.
    let persistNothing (_: Session.Persist list) = Session.StoreOutcome.Written


    /// <summary>
    /// Runs a step and persists its writes together. Returns the new state when the writes succeed or there are
    /// none; otherwise returns the old state with the answer <c>onFailure</c> gives for the store outcome.
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


    /// Runs a signing step. When the writes conflict, another server signed first, and the answer is a refusal
    /// blocked by the head of the winning version; any other store failure gives StoreFailed, so a new submission
    /// retries.
    let submitWith persist step state =
        runWith
            persist
            (function
            | Session.StoreOutcome.Conflict winner ->
                SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head winner))
            | _ -> SigningOutcome.Refused SigningRefusal.StoreFailed)
            step
            state


    /// Runs a challenge step; writes that fail give StoreFailed.
    let challengeWith persist step state =
        runWith persist (fun _ -> SigningOutcome.Refused SigningRefusal.StoreFailed) step state


    /// Ends the session when it has been idle too long, then runs the step; the writes of the idle end come first.
    /// The clock is read once, under the port lock, so a request that waited for the lock never writes a heartbeat
    /// older than the previous one.
    let idleFirst
        (idle: TimeSpan)
        (now: unit -> DateTime)
        (sid: string)
        (step: DateTime -> Session.State -> Session.State * 'a * Session.Persist list)
        (state: Session.State)
        : Session.State * 'a * Session.Persist list
        =
        let at = now ()
        let state, ended = Session.endIdle idle at sid state
        let state, result, writes = step at state
        state, result, ended @ writes


    /// Give a step a unit answer or an empty list of writes, in the shape runWith takes.
    let private asUnit (state, writes) = state, (), writes

    let private noWrites (state, answer) = state, answer, []


    /// Runs a step that has no refusal to give; writes that fail raise an InvalidOperationException.
    let failWith persist step state =
        runWith persist (fun outcome -> invalidOp $"the session store did not take the writes: %A{outcome}") step state


    /// The rows a request can touch. The store loads these rows before the request runs; the in-memory store
    /// ignores the slice.
    [<RequireQualifiedAccess>]
    type Slice =
        /// No rows to read: the request brings everything it needs.
        | Nothing
        /// A presented launch, by the nonce sealed in it.
        | LaunchNonce of string
        /// A callback, by the state it carries.
        | LaunchState of string
        /// The sessions of a login, so that a new session sees the session it supersedes.
        | Login of string
        /// A session, by the id in the cookie.
        | Session of string
        /// An enrolment attempt and the rows of the user who enrols.
        | Enrolment of string
        /// A submission: its session and the answer already given for its idempotency key, so that a submission
        /// sent twice is answered once.
        | Submission of sessionId: string * idemKey: string


    /// Where the session state lives. load adds the rows a slice names to the state before a request runs, and
    /// persist writes the request's changes. The in-memory store keeps everything in the state; the SQL store uses
    /// the database.
    type SessionStore =
        {
            load: Slice -> Session.State -> Session.State
            persist: Session.Persist list -> Session.StoreOutcome
        }


    /// The in-memory store: load returns the state unchanged and persist always returns Written.
    let inMemory =
        {
            load = fun _ s -> s
            persist = persistNothing
        }


    /// Makes the session port over a session store. Every request runs under one lock and loads its rows first.
    /// When a load throws, the state is unchanged: challenge and submit return StoreFailed, challengeDate returns
    /// None, and the other requests fail. find, seen, openVersion, refresh, challenge and submit first end the
    /// session when it has been idle too long.
    let makeSessionPortWith
        (store: SessionStore)
        (zone: TimeZoneInfo)
        (idle: TimeSpan)
        (now: unit -> DateTime)
        (newId: unit -> string)
        (newCode: unit -> string)
        (newSalt: int -> byte[])
        (codeMac: string -> byte[])
        (verify: Launch -> Result<LaunchSeal.Claims, LaunchRefusal>)
        (idp: IdentityProviderPort)
        (registry: UserRegistryPort)
        (ehrPatients: EhrPatientPort)
        (mailPort: MailPort)
        (initial: Session.State)
        : SessionPort
        =
        let gate = obj ()
        let mutable state = initial

        // Mail goes out only after the store has taken the writes of the request. The Session functions get a send
        // that collects the mail, and the collected mail is sent when the writes succeed; when the store refuses
        // them, the mail is dropped. A mail that throws does not fail the request.
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
                    // Reached only when the writes succeeded or there were none, so the remaining mail goes out.
                    flush true
                    result
                )

        // Like update, but a load that throws returns failed instead of raising, and f handles its own failed writes.
        let guarded failed slice f =
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
                    | Error() -> failed
                    | Ok s ->
                        let next, result = f s
                        state <- next
                        flush true
                        result
                )

        let signing slice f =
            guarded (SigningOutcome.Refused SigningRefusal.StoreFailed) slice f

        // Runs a step on the rows of one session, ending an idle session first.
        let overSession sid step = update (Slice.Session sid) (idleFirst idle now sid step)

        // A launch names its rows only when its seal verifies; an unverified launch names nothing, and
        // Session.present refuses it.
        let launchSlice (launch, _) =
            match verify launch with
            | Ok claims -> Slice.LaunchNonce claims.Nonce
            | Error _ -> Slice.Nothing

        // A callback runs in two steps with the rows of the login loaded in between: redeeming the code gives the
        // login, and opening the session then finds the newest session of that login to supersede.
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
                    ehrPatients
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
            find = fun id -> async { return overSession id (fun at -> Session.find at id) }
            patientData = fun opened -> Session.sessionPatientData (now () |> LocalDate.ofUtc zone) ehrPatients opened
            close = fun id -> async { return update (Slice.Session id) (Session.close (now ()) id >> asUnit) }
            closeTab =
                fun id opened ->
                    async {
                        return
                            update
                                (Slice.Session id)
                                (Session.whenOwnToken id opened (Session.close (now ()) id >> asUnit))
                    }
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
                                        ehrPatients
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
                                    (idleFirst
                                        idle
                                        now
                                        sid
                                        (fun at ->
                                            Session.challenge at newId digest ehrPatients registry.standing sid request
                                        )))
                    }
            challengeDate =
                fun sid ->
                    async { return guarded None (Slice.Session sid) (fun s -> s, Session.challengeDate zone sid s) }
            submit =
                fun sid signature ->
                    async {
                        return
                            signing
                                (Slice.Submission(sid, signature.IdemKey))
                                (submitWith
                                    persisting
                                    (idleFirst
                                        idle
                                        now
                                        sid
                                        (fun at ->
                                            Session.commit
                                                zone
                                                at
                                                newId
                                                digest
                                                registry.standing
                                                mail.send
                                                sid
                                                signature
                                        )))
                    }
            seen = fun sid opened -> async { return overSession sid (fun at -> Session.seen at sid opened) }
            openVersion =
                fun sid id opened ->
                    async {
                        return
                            overSession
                                sid
                                (fun at -> Session.whenOwnToken sid opened (Session.openVersion at newId sid id))
                    }
            refresh =
                fun sid opened ->
                    async {
                        return
                            overSession
                                sid
                                (fun at -> Session.whenOwnToken sid opened (Session.refresh at newId ehrPatients sid))
                    }
        }


    /// The session port over an in-memory store.
    let makeSessionPort = makeSessionPortWith inMemory
