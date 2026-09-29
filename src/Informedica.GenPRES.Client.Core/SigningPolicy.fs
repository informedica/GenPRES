/// Decides what the signing dialog says and offers, from the session and the signing phase. The
/// texts are Terms, translated by the caller.
module SigningPolicy

open Shared
open Shared.Types
open SessionMachine
open SigningMachine


/// The English of the signing terms, used when the sheet has no row for a term. Other terms
/// fall back to the session gate's English.
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
    | Terms.``Signing Data Changed Held`` ->
        "The patient data changed since the session opened, but the plan has new or changed orders composed on the data as it was. Continue to sign over the data as it was; the new data applies after the sign. Or cancel."
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
    | Terms.``Signing Refusal Store Failed`` -> "The version could not be stored. Nothing changed; sign again."
    | Terms.``Signing Refusal Plan Unreadable`` -> "The plan could not be read. Reload the page and sign again."
    | Terms.``Signing Refusal Context Differs`` ->
        "An order in the plan was composed on other patient data. Remove the new and changed orders and prescribe them again."
    | Terms.``Signing Send Failed`` -> "The signature could not be sent. Try again."
    | Terms.``Signing New`` -> "New"
    | Terms.``Signing Changed`` -> "Changed"
    | Terms.``Signing Removed`` -> "Removed"
    | Terms.``Signing No Changes`` -> "No order changed since the last signed version."
    | _ -> SessionGatePolicy.english term


/// A moment as the user reads it: the local time of day.
let time (at: System.DateTime) = at.ToLocalTime().ToString "HH:mm"


/// The sentence for a refusal, with its numbers and names filled in.
let refusalSentence (tr: Terms -> string) (refusal: SigningRefusal) =
    match refusal with
    | SigningRefusal.NoSession -> tr Terms.``Signing Refusal No Session``
    | SigningRefusal.NoPatient -> tr Terms.``Signing Refusal No Patient``
    | SigningRefusal.NotPrescriber -> tr Terms.``Signing Refusal Not Prescriber``
    | SigningRefusal.Blocked head ->
        tr Terms.``Signing Refusal Blocked``
        |> TermText.fill [ head.By.DisplayName; time head.SignedAt ]
    | SigningRefusal.StaleToken -> tr Terms.``Signing Refusal Stale Token``
    | SigningRefusal.ChallengeMismatch -> tr Terms.``Signing Refusal Challenge Mismatch``
    | SigningRefusal.ChallengeExpired -> tr Terms.``Signing Refusal Challenge Expired``
    | SigningRefusal.PinWrong left -> tr Terms.``Signing Refusal Pin Wrong`` |> TermText.fill [ string left ]
    | SigningRefusal.PinLimit -> tr Terms.``Signing Refusal Pin Limit``
    | SigningRefusal.Locked until -> tr Terms.``Signing Refusal Locked`` |> TermText.fill [ time until ]
    | SigningRefusal.StoreFailed -> tr Terms.``Signing Refusal Store Failed``
    | SigningRefusal.PlanUnreadable -> tr Terms.``Signing Refusal Plan Unreadable``
    | SigningRefusal.ContextDiffers -> tr Terms.``Signing Refusal Context Differs``


/// The sentence once a version is signed.
let signedSentence (tr: Terms -> string) (signed: SignedOrderPlan) =
    tr Terms.``Signing Signed``
    |> TermText.fill [ string signed.Head.No; signed.Head.By.DisplayName ]


/// The sentence when a newer version exists: who signed it, and when.
let movedOnSentence (tr: Terms -> string) (head: OrderPlanHead) =
    tr Terms.``Session Newer Version``
    |> TermText.fill [ head.By.DisplayName; time head.SignedAt ]


/// The sentence when a version is opened.
let versionOpenedSentence (tr: Terms -> string) (head: OrderPlanHead) =
    tr Terms.``Session Version Opened``
    |> TermText.fill [ string head.No; head.By.DisplayName ]


/// The sentence of the data notice: the data changed, or could not be verified. When the patient
/// context is held, the plan is signed over the data as it was, and the notice says so.
let noticeSentence (tr: Terms -> string) (held: bool) (notice: DataNotice) =
    match notice.Data with
    | Some _ when held -> tr Terms.``Signing Data Changed Held``
    | Some _ -> tr Terms.``Signing Data Changed``
    | None -> tr Terms.``Signing Data Unverified``


/// The error for a PIN that is not four to six digits, checked before anything is sent.
let pinError (tr: Terms -> string) (pin: string) =
    if SessionGatePolicy.digits 4 6 pin then
        None
    else
        Some(tr Terms.``Session Enrolment Pin Format``)


/// Whether the plan can be signed: an open session with a patient, a user with the Prescriber
/// role, and at least one order.
let canSign (session: SessionView) (plan: OrderPlan) =
    match session with
    | SessionView.Open opened ->
        (opened.User |> Option.map _.Role) = Some UserRole.Prescriber
        && opened.PatientContext.IsSome
        && (Shared.Models.OrderPlan.orders plan).Length > 0
    | _ -> false


/// Whether a signature is under way, from the sign until it is answered or cancelled. The order
/// plan takes no change meanwhile.
let underWay (signing: SigningView) =
    match signing with
    | SigningView.Idle -> false
    | SigningView.Requesting
    | SigningView.Noticed _
    | SigningView.Challenged _
    | SigningView.Submitting _ -> true


/// Whether the signing dialog is open: for as long as a signature is under way.
let dialogOpen (signing: SigningView) = underWay signing
