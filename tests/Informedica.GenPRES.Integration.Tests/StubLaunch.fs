/// The stub LaunchScript page a demo server serves at /stub/launch, standing in for the EHR
/// that opens GenPRES on a patient for a user signed on at the browser, and what the stub
/// directory behind it says of each identity.
module Informedica.GenPRES.Integration.Tests.StubLaunch

open System
open System.Threading.Tasks

open PuppeteerSharp


/// Who the stub IdentityProvider says is signed on at the browser: the choices of the page's
/// identity list.
[<RequireQualifiedAccess>]
type Identity =
    /// A Prescriber whose active patient is the launched one.
    | Prescriber
    /// A second Prescriber on the same patient.
    | PrescriberB
    /// A Reader, who needs no PIN.
    | Reader
    /// A Prescriber with another patient active in the EHR: the patient otherPatient.
    | PrescriberOtherPatient
    /// A Prescriber who has not set a PIN yet.
    | NoPin
    /// A login the user registry does not know.
    | Unknown
    /// Nobody signed on at the browser.
    | Nobody


/// The PatientId the stub registry keeps active for PrescriberOtherPatient, whatever the launch
/// page was given: a launch on it opens a Session, a launch on any other is wrong-patient.
let otherPatient = "other-patient"


/// The PatientId the stub patient data platform has no record for: the Session opens on what
/// was signed last for it, or on no patient data at all.
let noData = "no-data"


/// The PIN the stub credential store seeds for every Prescriber that signs.
let seededPin = "1234"


/// The stub patient, whom the platform answers for every PatientId but noData: its name and
/// its birthdate as the title bar shows them.
let patientName = "Test Patient"


/// The stub patient's birthdate, as the title bar shows it.
let patientBirthDate = "15-03-2016"


/// A PatientId no test used before, so that its record starts empty and its first signature is
/// version 1. The stub platform answers the stub patient for it.
let newPatientId (purpose: string) =
    $"""e2e-%s{purpose}-%s{Guid.NewGuid().ToString("N").Substring(0, 8)}"""


/// Functions over the stub identities.
[<RequireQualifiedAccess>]
module Identity =

    /// The value of the identity in the page's list, which is also its login.
    let value identity =
        match identity with
        | Identity.Prescriber -> "prescriber"
        | Identity.PrescriberB -> "prescriber-b"
        | Identity.Reader -> "reader"
        | Identity.PrescriberOtherPatient -> "prescriber-other-patient"
        | Identity.NoPin -> "no-pin"
        | Identity.Unknown -> "unknown"
        | Identity.Nobody -> "none"


    /// The name the stub directory gives the identity, which the session button shows.
    let displayName identity =
        match identity with
        | Identity.Prescriber -> "Prescriber A"
        | Identity.PrescriberB -> "Prescriber B"
        | Identity.Reader -> "Stub Reader"
        | Identity.PrescriberOtherPatient -> "Stub Prescriber (other patient)"
        | Identity.NoPin -> "Stub Prescriber (no PIN)"
        | Identity.Unknown -> "Stub unknown"
        | Identity.Nobody -> "Stub none"


    /// The address the stub registry gives the identity, where the stub MailService sends its
    /// mail.
    let mailAddress identity = $"%s{value identity}@stub.example"


/// Where the page is served, under the base url.
let path = "/stub/launch"


/// Opens GenPRES the way the EHR does: fills in the stub launch page for a patient and an
/// identity and submits it. Returns once the form is sent; the redirects that present the
/// launch and sign the browser on follow by themselves, so the caller waits for the outcome
/// it expects.
let launch (baseUrl: string) (patientId: string) (identity: Identity) (page: IPage) =
    task {
        do! page.GoToAsync(baseUrl + path) :> Task

        // the field comes filled in with the page's own default patient
        do! page.EvaluateFunctionAsync("id => { document.querySelector('input[name=\"pid\"]').value = id }", patientId)

        do! page.SelectAsync("select[name=\"identity\"]", Identity.value identity) :> Task
        do! page.ClickAsync "button[type=\"submit\"]"
    }
