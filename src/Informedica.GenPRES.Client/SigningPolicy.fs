/// What the signing UI says and offers (uc-03 steps 2 and 3), decided from the Session and
/// the signing phase: every text a `Terms` case, so the view only renders.
module SigningPolicy

open Shared
open Shared.Types
open SessionMachine
open SigningMachine


/// The English default of every signing term: what the UI shows when the sheet has no row
/// yet, and what the tests assert.
let english (term: Terms) : string =
    match term with
    | Terms.``Signing Sign`` -> "Sign"
    | Terms.``Signing Dialog Title`` -> "Sign the order plan"
    | Terms.``Signing Dialog Text`` -> "Sign the orders as shown with your PIN, or cancel and edit."
    | Terms.``Signing Pin`` -> "PIN"
    | Terms.``Signing Cancel`` -> "Cancel"
    | Terms.``Signing Proceed`` -> "Continue"
    | Terms.``Signing Signed`` -> "Version {0} was signed by {1}."
    | Terms.``Signing Data Changed`` ->
        "The patient data changed since the session opened. It is shown as it stands now; continue to sign over it, or cancel."
    | Terms.``Signing Data Unverified`` ->
        "The patient data could not be verified. Continue to sign over the data the session opened with, or cancel."
    | Terms.``Signing Refusal No Session`` -> "There is no session to sign in. Open GenPRES again from MainEHR."
    | Terms.``Signing Refusal No Patient`` -> "The plan is not over this session's patient."
    | Terms.``Signing Refusal Not Prescriber`` -> "Only a Prescriber can sign."
    | Terms.``Signing Refusal Blocked`` ->
        "{0} signed a newer version at {1}. Open the patient again to continue from it."
    | Terms.``Signing Refusal Stale Token`` -> "The session is not current. Reload the page."
    | Terms.``Signing Refusal Challenge Mismatch`` -> "The plan changed since it was shown. Sign again."
    | Terms.``Signing Refusal Challenge Expired`` -> "The signature took too long. Sign again."
    | Terms.``Signing Refusal Pin Wrong`` -> "The PIN is not right. {0} tries left."
    | Terms.``Signing Refusal Pin Limit`` ->
        "The PIN was entered wrong three times. Your session was ended and signing is locked for a while."
    | Terms.``Signing Refusal Locked`` -> "Signing is locked until {0}."
    | _ -> SessionGatePolicy.english term


/// A moment as the User reads it: the local time of day.
let time (at: System.DateTime) = at.ToLocalTime().ToString "HH:mm"


/// One sentence per refusal, the numbers and names filled in.
let refusalSentence (tr: Terms -> string) (refusal: SigningRefusal) =
    match refusal with
    | SigningRefusal.NoSession -> tr Terms.``Signing Refusal No Session``
    | SigningRefusal.NoPatient -> tr Terms.``Signing Refusal No Patient``
    | SigningRefusal.NotPrescriber -> tr Terms.``Signing Refusal Not Prescriber``
    | SigningRefusal.Blocked head ->
        tr Terms.``Signing Refusal Blocked``
        |> SessionGatePolicy.fill [ head.By.DisplayName; time head.SignedAt ]
    | SigningRefusal.StaleToken -> tr Terms.``Signing Refusal Stale Token``
    | SigningRefusal.ChallengeMismatch -> tr Terms.``Signing Refusal Challenge Mismatch``
    | SigningRefusal.ChallengeExpired -> tr Terms.``Signing Refusal Challenge Expired``
    | SigningRefusal.PinWrong left -> tr Terms.``Signing Refusal Pin Wrong`` |> SessionGatePolicy.fill [ string left ]
    | SigningRefusal.PinLimit -> tr Terms.``Signing Refusal Pin Limit``
    | SigningRefusal.Locked until -> tr Terms.``Signing Refusal Locked`` |> SessionGatePolicy.fill [ time until ]


/// What the User is told once the version landed.
let signedSentence (tr: Terms -> string) (signed: SignedOrderPlan) =
    tr Terms.``Signing Signed``
    |> SessionGatePolicy.fill [ string signed.Head.No; signed.Head.By.DisplayName ]


/// Rule 44: what the notice says, with or without a reading.
let noticeSentence (tr: Terms -> string) (notice: DataNotice) =
    match notice.Data with
    | Some _ -> tr Terms.``Signing Data Changed``
    | None -> tr Terms.``Signing Data Unverified``


/// The PIN's own check before anything is sent: the enrolment's four to six digits.
let pinError (tr: Terms -> string) (pin: string) =
    if SessionGatePolicy.digits 4 6 pin then
        None
    else
        Some(tr Terms.``Session Enrolment Pin Format``)


/// Whether the plan can be signed: an open Session as Prescriber for a patient, and at least
/// one order (Rules 13, 26).
let canSign (session: Session) (plan: OrderPlan) =
    match session with
    | Session.Open opened ->
        (opened.User |> Option.map _.Role) = Some UserRole.Prescriber
        && opened.PatientContext.IsSome
        && plan.Scenarios.Length > 0
    | _ -> false


/// Whether the dialog is up: the notice, the PIN question, or the Submission in flight.
let dialogOpen (signing: Signing) =
    match signing with
    | Signing.Noticed _
    | Signing.Challenged _
    | Signing.Submitting _ -> true
    | Signing.Idle
    | Signing.Requesting _ -> false
