namespace ServerApi

// The stand-ins for every party outside GenPRES, so that the whole launch, enrolment and
// signing sequence runs on one machine with nothing to install: the IdentityProvider and the
// UserRegistry, the PatientDataPlatform, the Database (credentials, sessions, the record), the
// MailService and the MainEHR LaunchScript. `Server.fs` mounts their pages in full scope only;
// `Adapters.makeAppEnvWith` wires them into the AppEnv. Development and testing only.

open System
open Informedica.GenPRES.Shared.Types
open Informedica.Utils.Lib.BCL


/// The IdentityProvider and the UserRegistry as one stub directory over an identity choice made
/// on the stub launch page: prescriber, reader, prescriber-other-patient, no-pin,
/// unknown (a login the registry does not know), none (no identity: the stub IdP's
/// /authorize never issues a code for it). The active Patient of a choice is the one the
/// launch page was given, except for prescriber-other-patient.
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
                // a second Prescriber on the same patient, so that the record can move on
                // under another Session
                | "prescriber-b" -> "Prescriber B"
                | "reader" -> "Stub Reader"
                | "prescriber-other-patient" -> "Stub Prescriber (other patient)"
                | "no-pin" -> "Stub Prescriber (no PIN)"
                | other -> $"Stub {other}"
        }


    /// The address the registry gives for a login. The stub's is derived from it.
    let mailAddressOf (identity: BrowserIdentity) = $"{identity.Login}@stub.example"


    /// The registry's answer for a login, given the Patient the launch page made active. Whether
    /// a PIN is set is not the registry's to say: no-pin differs from prescriber only in
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
    /// registry), behind a lock. issue is what /authorize calls once it read the identity
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


/// The EHR stub: a test patient for each PatientId of the test patients, one fixed patient for
/// every other PatientId, so a launch fills the patient panel from the EHR, except that no-data
/// has no record at all, so the Session opens on what was signed last, or on nothing.
module StubPatientData =

    /// The stub's reading projected at its patient's tenth birthday, as the contract model:
    /// ten years, 32 kg and 140 cm measured, nothing else known.
    let patient: Patient =
        Informedica.GenPRES.Shared.Models.Patient.create
            (Some(Informedica.GenPRES.Shared.Measures.toYear 10))
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
        |> Option.defaultValue Informedica.GenPRES.Shared.Models.Patient.empty


    open Informedica.GenCore.Lib
    open Informedica.GenCore.Lib.Patients


    /// The stub patient's name.
    let name = "Test Patient"


    /// The stub patient's birthdate: the fifteenth of March, 2016.
    let birthDate =
        BirthDate.create (Conversions.yearFromInt 2016) (Conversions.monthFromInt 3) (Conversions.dayFromInt 15)


    /// The date the stub's measurements carry: the patient's tenth birthday, so that the
    /// projection at that date is the reading above.
    let measuredOn = DateTime(2026, 3, 15)


    /// The stub's EHR data for an id: the fixed patient under that id, nothing else known.
    let data (pid: string) : Informedica.GenForm.Lib.Types.EhrPatientData =
        let weight = WeightAtDate.create measuredOn (WeightValue.weightInKg 32m)
        let height = HeightAtDate.create measuredOn (HeightValue.heightInCm 140m)

        Patient.create
            pid
            name
            Department.unknown
            [||]
            UnknownGender
            (PatientAge.birthDate birthDate)
            (Weight.create [ weight ] None [] (Some weight))
            (Height.create [ height ] None (Some height))
            None
            UnknownEnteral
            UnknownVenous
        |> fun pat -> Informedica.GenForm.Lib.EhrPatientData.create pat None []


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


    /// The birth date that gives an age on a day: the date the years, months, weeks and days of the
    /// age before that day, moved by up to three days when GenPRES would count the age in days
    /// otherwise. GenPRES counts a year as 365 days and a month as 30, so the calendar date alone is
    /// a few days off at the end of a month and on 29 February. On a day that no birth date fits,
    /// the calendar date.
    let birthDateAt (day: DateTime) age =
        let inDays birth =
            birth
            |> BirthDate.fromDate
            |> PatientAge.birthDate
            |> PatientAge.getAgeValue day
            |> Option.map (AgeValue.getAgeInDays >> int)

        let target = age |> AgeValue.getAgeInDays |> int |> Some

        age
        |> PatientAge.ageValue
        |> PatientAge.getBirthDate day
        |> Option.map (fun bd ->
            let calendar = bd |> BirthDate.toDate

            [ 0; 1; -1; 2; -2; 3; -3 ]
            |> List.map (float >> calendar.AddDays)
            |> List.tryFind (fun birth -> inDays birth = target)
            |> Option.defaultValue calendar
            |> BirthDate.fromDate
        )


    /// The EHR data of a test patient read at a moment: born the age before that day, measured that day.
    let testPatientData (now: DateTime) tp =
        let measuredOn = now.Date
        let weight = WeightAtDate.create measuredOn tp.Weight
        let height = HeightAtDate.create measuredOn tp.Height

        let age =
            tp.Age
            |> birthDateAt measuredOn
            |> Option.map PatientAge.birthDate
            |> Option.defaultValue PatientAge.unknown

        Patient.create
            tp.Id
            tp.Name
            tp.Department
            [||]
            tp.Gender
            age
            (Weight.create [ weight ] None [] (Some weight))
            (Height.create [ height ] None (Some height))
            tp.GestationalAge
            UnknownEnteral
            UnknownVenous
        |> fun pat -> Informedica.GenForm.Lib.EhrPatientData.create pat None []


    /// The EHR data for a PatientId read at a moment: a test patient's data, none for no-data,
    /// and the fixed patient for every other PatientId.
    let read (now: unit -> DateTime) pid =
        match testPatients |> List.tryFind (fun tp -> tp.Id = pid) with
        | Some tp -> tp |> testPatientData (now ()) |> Some
        | None when pid = "no-data" -> None
        | None -> Some(data pid)


    /// The EHR stub over a clock; the patient the rules see is the projection at the date,
    /// unestimated here.
    let portAt now : PatientDataPort =
        {
            read = read now
            patient = Informedica.GenForm.Lib.Patient.ofEhr
        }


    /// The EHR stub over the system clock, which only the test patients read.
    let port = portAt (fun () -> DateTime.UtcNow)


/// The credential half of the Database, seeded for the stub logins: the Prescribers that sign
/// have the PIN 1234, no-pin has none and enrols, a Reader has no credential because a
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
/// Server.fs mounts GET /stub/mail in full scope only.
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


    /// The outbox as a page. No inline script or style, so the CSP (default-src 'self')
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
/// sealed Launch for a chosen PatientId and opens the client on it. Pure here; Server.fs
/// mounts GET /stub/launch (the page) and POST /stub/launch (mint + redirect).
module StubLaunch =

    let path = "/stub/launch"


    /// The Launch lifetime: a page load, the identity round trip, a retry or two.
    let lifetime = TimeSpan.FromMinutes 2.0


    /// The PatientId of the fixed stub patient, launched when no PatientId is given.
    let defaultPatientId = "123456"


    /// The test patient checked on the launch page: the ICK adolescent of 10 years.
    let checkedPatientId = "ick-adolescent"


    /// The patient value that launches the PatientId typed in the field.
    let otherPatient = "other"


    open Informedica.GenCore.Lib.Patients


    /// Text made safe to put in the page.
    let encode (s: string) = System.Net.WebUtility.HtmlEncode s


    /// The columns of a test patient in the patient table: gender, age, gestational age, weight and height.
    let columns (tp: StubPatientData.TestPatient) =
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
            | AnyGender
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
            StubPatientData.testPatients
            |> List.groupBy (fun tp -> tp.Department |> Department.name |> Option.defaultValue "")

        [
            for department, tps in groups do
                $"""      <tr class="group"><th colspan="7">%s{encode department}</th></tr>"""

                for tp in tps do
                    patientRow (tp.Id = checkedPatientId) tp.Id tp.Name (columns tp)
            $"""      <tr class="other"><td><input type="radio" name="patient" id="p-%s{otherPatient}" value="%s{otherPatient}"></td><td><label for="p-%s{otherPatient}">Other</label></td><td colspan="5"><input name="pid" aria-label="PatientId" placeholder="PatientId"> <small><code>no-data</code>: a patient without imported data</small></td></tr>"""
        ]
        |> String.concat "\n"


    /// The look of the launch page; an inline style, which the CSP allows, and no script.
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


    /// The form for a list of identity choices (the stub directory's): a table of the stub
    /// patients and a row of identities, the first one checked. A launch opens GenPRES in a new
    /// tab, as the EHR does, and leaves this page open. No script, so the CSP holds.
    /// The PatientId no-data opens a Session without imported data.
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


    /// The page over the stub directory's choices.
    let page = pageFor StubDirectory.choices


    /// The PatientId a launch is for: the chosen patient, or the typed PatientId when Other or
    /// nothing is chosen, without surrounding spaces; the fixed patient when that is blank. The
    /// launch and the identity cookie both take this value, so they name the same patient.
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


    /// Where the browser goes after minting: the Launch travels in the url hash, which never
    /// reaches the server as a request.
    let launchUrl (launch: Launch) =
        match launch with
        | Launch text -> $"/#/session?launch={Uri.EscapeDataString text}"


    /// Mints the Launch for a posted PatientId; blank falls back to the stub patient.
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
/// the pure Session functions over it; the clock, the ids, the codes and the ports come in
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
    /// request answers what <c>onFailure</c> makes of the store's outcome. A step without writes
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
    /// StoreFailed, so that the next Submission is the retry.
    let submitWith persist step state =
        runWith
            persist
            (function
            | Session.StoreOutcome.Conflict winner ->
                SigningOutcome.Refused(SigningRefusal.Blocked(StoredVersion.head winner))
            | _ -> SigningOutcome.Refused SigningRefusal.StoreFailed)
            step
            state


    /// A challenge's writes that did not land: StoreFailed.
    let challengeWith persist step state =
        runWith persist (fun _ -> SigningOutcome.Refused SigningRefusal.StoreFailed) step state


    /// A member's step over a Session, the idle end first, its row ahead of the step's writes.
    /// The clock is read once, when the step runs under the port's lock, so that a request that
    /// waited for the lock never writes a heartbeat older than the one before it.
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


    /// A member that answers nothing and one that writes nothing, in the shape runWith takes.
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
        /// the request acts on what it brings: there is nothing to read for it
        | Nothing
        /// a Launch presented, by the nonce sealed in it
        | LaunchNonce of string
        /// a callback, by the state it carries
        | LaunchState of string
        /// the Sessions of a login, so that an open sees the one it supersedes
        | Login of string
        /// a Session, by the id in the cookie
        | Session of string
        /// an enrolment attempt and its person's rows
        | Enrolment of string
        /// a Submission: the Session it comes from, and the answer its key was already given,
        /// so that the same Submission sent twice is answered once
        | Submission of sessionId: string * idemKey: string


    /// <summary>
    /// Where the session state lives: <c>load</c> puts the rows a slice names into the state before
    /// a request runs, <c>persist</c> appends what it wrote. The in-memory store keeps everything in
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
    /// Every request that names a Session, a close excepted, first ends it when it has gone
    /// the idle lifetime without one.
    /// </summary>
    let makeSessionPortWith
        (store: SessionStore)
        (idle: TimeSpan)
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

        // a request that answers a store failure rather than failing: a load that throws is
        // answered with `failed`, and `f` answers its own writes that did not land
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

        // a member over one Session's rows, the idle end first
        let overSession sid step = update (Slice.Session sid) (idleFirst idle now sid step)

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
                    patientData
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
            patient = fun opened -> Session.sessionPatient (now ()) patientData opened
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
                                        patientData
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
                                            Session.challenge at newId digest patientData registry.standing sid request
                                        )))
                    }
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
                                            Session.commit at newId digest registry.standing mail.send sid signature
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
                                (fun at -> Session.whenOwnToken sid opened (Session.refresh at newId patientData sid))
                    }
        }


    /// The session port over an in-memory store.
    let makeSessionPort = makeSessionPortWith inMemory
