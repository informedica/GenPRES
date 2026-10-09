/// <summary>
/// Decides what the session gate says and offers in every session phase. The texts are
/// <c>Terms</c>, translated by the caller.
/// </summary>
module SessionGatePolicy

open Shared
open Shared.Types
open SessionMachine
open TermText


/// What the gate offers besides the text.
[<RequireQualifiedAccess>]
type Action =
    /// Present the same launch again.
    | Retry
    /// Continue anonymously, without the launch.
    | ContinueWithoutLaunch


/// The enrolment form's labels, and the server's error on the last submission.
type EnrolmentForm =
    {
        /// The label of the mailed code field.
        Code: string
        /// The label of the PIN field.
        Pin: string
        /// The label of the field that repeats the PIN.
        Repeat: string
        /// The label of the button.
        Submit: string
        /// What the server said about the last submission, if anything.
        Error: string option
    }


/// What the gate shows.
type Gate =
    {
        /// The title.
        Title: string
        /// The text.
        Body: string
        /// Whether a request is under way.
        Busy: bool
        /// The actions offered besides the text.
        Actions: Action list
        /// The enrolment form, when the launch waits on a PIN.
        Form: EnrolmentForm option
    }


/// The English of the session terms, used when the sheet has no row for a term or has not
/// loaded. Any other term shows its name.
let english (term: Terms) =
    match term with
    | Terms.``Session Gate Opening`` -> "Opening your session"
    | Terms.``Session Gate Opening Text`` -> "Presenting the launch, attempt {0} of {1}."
    | Terms.``Session Gate Resuming`` -> "Resuming your session"
    | Terms.``Session Gate Resuming Text`` -> "Checking for an open session."
    | Terms.``Session Gate Unreachable`` -> "GenPRES could not be reached"
    | Terms.``Session Gate Unreachable Text`` -> "The server did not answer after {0} attempts."
    | Terms.``Session Gate Refused`` -> "GenPRES could not open your session"
    | Terms.``Session Gate Try Again Or Relaunch`` -> "Try again, or open GenPRES again from MainEHR."
    | Terms.``Session Relaunch`` -> "Open GenPRES again from MainEHR."
    | Terms.``Session Retry`` -> "Try again."
    | Terms.``Session Refusal Expired`` -> "The launch has expired."
    | Terms.``Session Refusal Spent`` -> "This launch has already been used."
    | Terms.``Session Refusal Invalid`` -> "The launch is not valid."
    | Terms.``Session Refusal No Browser Identity`` -> "Your browser could not be identified."
    | Terms.``Session Refusal No Role`` ->
        "You have no role in GenPRES. You can continue without a launch: no patient is carried over."
    | Terms.``Session Refusal Wrong Patient`` ->
        "The patient active in MainEHR is not the patient of this launch. Activate the right patient and open GenPRES again from MainEHR."
    | Terms.``Session Refusal Enrolment`` -> "A PIN has to be set before prescribing. Open GenPRES again from MainEHR."
    | Terms.``Session Try Again`` -> "Try again"
    | Terms.``Session Continue Without Launch`` -> "Continue without launch"
    | Terms.``Session Close`` -> "Close session"
    | Terms.``Session Role Prescriber`` -> "Prescriber"
    | Terms.``Session Role Reader`` -> "Reader"
    | Terms.``Session Gate Ended`` -> "Your session was ended"
    | Terms.``Session Ending Superseded`` -> "Another launch of yours opened a newer session, and this one was closed."
    | Terms.``Session Ending Pin Limit`` -> "The PIN was entered wrong three times, and signing is locked for a while."
    | Terms.``Session Ending Unreadable`` -> "This session could not be read back after an update of GenPRES."
    | Terms.``Session Ending Idle`` ->
        "This session was not used for too long and was closed, so that nothing is prescribed on patient data read long ago."
    | Terms.``Session Gate Enrolment`` -> "Set a PIN to continue"
    | Terms.``Session Gate Enrolment Text`` ->
        "Welcome, {0}. A confirmation code was mailed to {1}. Enter it together with the PIN of your choice: four to six digits."
    | Terms.``Session Enrolment Code`` -> "Confirmation code"
    | Terms.``Session Enrolment Pin`` -> "PIN"
    | Terms.``Session Enrolment Pin Repeat`` -> "Repeat the PIN"
    | Terms.``Session Enrolment Submit`` -> "Set PIN"
    | Terms.``Session Enrolment Code Format`` -> "The confirmation code has six digits."
    | Terms.``Session Enrolment Pin Format`` -> "The PIN has four to six digits."
    | Terms.``Session Enrolment Pins Differ`` -> "The two PINs differ."
    | Terms.``Session Enrolment Wrong Code`` -> "The code is not right. {0} tries left."
    | Terms.``Session Enrolment Code Void`` -> "The code is void after three wrong tries."
    | Terms.``Session Enrolment Expired`` -> "The enrolment has expired."
    | Terms.``Session Newer Version`` -> "{0} signed a newer version at {1}."
    | Terms.``Session Open Newest`` -> "Open the newest version"
    | Terms.``Session Open Last Signed`` -> "Open the last signed order plan"
    | Terms.``Session Version Opened`` -> "Version {0} by {1} is now open."
    | Terms.``Session Refresh Failed`` ->
        "The patient data could not be read from the EHR again. Nothing changed; the orders are as they were."
    | _ -> $"{term}"


/// The sentences of a refusal: what happened, then what the user can do. Each sentence is its
/// own term, so no translation is embedded in another.
let refusalBody (tr: Terms -> string) refusal =
    match refusal with
    | LaunchRefusal.LaunchExpired -> [ tr Terms.``Session Refusal Expired``; tr Terms.``Session Relaunch`` ]
    | LaunchRefusal.LaunchSpent -> [ tr Terms.``Session Refusal Spent``; tr Terms.``Session Relaunch`` ]
    | LaunchRefusal.LaunchInvalid -> [ tr Terms.``Session Refusal Invalid``; tr Terms.``Session Relaunch`` ]
    | LaunchRefusal.NoBrowserIdentity -> [ tr Terms.``Session Refusal No Browser Identity`` ]
    | LaunchRefusal.NoRole -> [ tr Terms.``Session Refusal No Role`` ]
    | LaunchRefusal.WrongActivePatient -> [ tr Terms.``Session Refusal Wrong Patient`` ]
    | LaunchRefusal.EnrolmentRequired -> [ tr Terms.``Session Refusal Enrolment`` ]


/// Whether the text is only digits, between the given lengths.
let digits (min: int) (max: int) (s: string) =
    not (isNull s)
    && s.Length >= min
    && s.Length <= max
    && s |> Seq.forall (fun c -> c >= '0' && c <= '9')


/// The first error of the enrolment form before it is sent: the code must be six digits, the PIN
/// four to six, and the repeat must match. None when the form can be sent.
let formError (tr: Terms -> string) (code: string) (pin: string) (repeat: string) : string option =
    if not (digits 6 6 code) then
        Some(tr Terms.``Session Enrolment Code Format``)
    elif not (digits 4 6 pin) then
        Some(tr Terms.``Session Enrolment Pin Format``)
    elif pin <> repeat then
        Some(tr Terms.``Session Enrolment Pins Differ``)
    else
        None


/// The server's error on a submission that left the form open.
let refusalSentence (tr: Terms -> string) (refusal: PinRefusal) =
    match refusal with
    | PinRefusal.WrongCode left -> tr Terms.``Session Enrolment Wrong Code`` |> fill [ $"%i{left}" ]
    | PinRefusal.PinFormat -> tr Terms.``Session Enrolment Pin Format``
    | PinRefusal.CodeVoid -> tr Terms.``Session Enrolment Code Void``
    | PinRefusal.AttemptExpired -> tr Terms.``Session Enrolment Expired``
    | PinRefusal.WrongActivePatient -> tr Terms.``Session Refusal Wrong Patient``


/// Whether the gate covers the app: false while the app is usable (anonymous, open, closing).
let isGated (session: SessionView) =
    match session with
    | SessionView.Anonymous
    | SessionView.Open _
    | SessionView.Closing _ -> false
    | SessionView.Launching _
    | SessionView.Resuming
    | SessionView.ServerUnreachable
    | SessionView.LaunchRefused _
    | SessionView.LaunchRetryable _
    | SessionView.Ended _
    | SessionView.Enrolling _
    | SessionView.SupplyingPin _
    | SessionView.EnrolmentFailed _ -> true


/// The gate for a refused launch: what happened, what the user can do, and Retry when the same
/// launch can be presented again. A missing role always offers to continue anonymously; only a
/// missing browser identity is retried.
let refused (tr: Terms -> string) (refusal: LaunchRefusal) (retry: Action option) =
    {
        Title = tr Terms.``Session Gate Refused``
        Body =
            sentences
                [
                    yield! refusalBody tr refusal
                    match refusal, retry with
                    | LaunchRefusal.NoBrowserIdentity, Some _ -> tr Terms.``Session Retry``
                    | LaunchRefusal.NoBrowserIdentity, None -> tr Terms.``Session Relaunch``
                    | _ -> ()
                ]
        Busy = false
        Actions =
            [
                match refusal, retry with
                | LaunchRefusal.NoRole, _ -> Action.ContinueWithoutLaunch
                | _, Some action -> action
                | _ -> ()
            ]
        Form = None
    }


/// The gate for a session phase, or None when the app is usable (anonymous, open, closing).
let gateFor (tr: Terms -> string) (session: SessionView) : Gate option =
    match session with
    | SessionView.Launching attempt ->
        Some
            {
                Title = tr Terms.``Session Gate Opening``
                Body =
                    tr Terms.``Session Gate Opening Text``
                    |> fill [ $"%i{attempt}"; $"%i{SessionState.maxAttempts}" ]
                Busy = true
                Actions = []
                Form = None
            }
    | SessionView.Resuming ->
        Some
            {
                Title = tr Terms.``Session Gate Resuming``
                Body = tr Terms.``Session Gate Resuming Text``
                Busy = true
                Actions = []
                Form = None
            }
    // the server is given up on after the last attempt, so the count named is the maximum
    | SessionView.ServerUnreachable ->
        Some
            {
                Title = tr Terms.``Session Gate Unreachable``
                Body =
                    sentences
                        [
                            tr Terms.``Session Gate Unreachable Text``
                            |> fill [ $"%i{SessionState.maxAttempts}" ]
                            tr Terms.``Session Gate Try Again Or Relaunch``
                        ]
                Busy = false
                Actions = [ Action.Retry ]
                Form = None
            }
    | SessionView.LaunchRefused refusal -> Some(refused tr refusal None)
    | SessionView.LaunchRetryable refusal -> Some(refused tr refusal (Some Action.Retry))
    | SessionView.Ended ending ->
        Some
            {
                Title = tr Terms.``Session Gate Ended``
                Body =
                    sentences
                        [
                            match ending with
                            | SessionEnding.SupersededByLaunch -> tr Terms.``Session Ending Superseded``
                            | SessionEnding.WrongPinLimit -> tr Terms.``Session Ending Pin Limit``
                            | SessionEnding.Unreadable -> tr Terms.``Session Ending Unreadable``
                            | SessionEnding.Idle -> tr Terms.``Session Ending Idle``
                            tr Terms.``Session Relaunch``
                        ]
                Busy = false
                Actions = [ Action.ContinueWithoutLaunch ]
                Form = None
            }
    // the launch waits on a PIN; the form asks for the mailed code and the PIN twice
    | SessionView.Enrolling(pending, refusal) ->
        Some
            {
                Title = tr Terms.``Session Gate Enrolment``
                Body =
                    tr Terms.``Session Gate Enrolment Text``
                    |> fill [ pending.DisplayName; pending.MailHint ]
                Busy = false
                Actions = []
                Form =
                    Some
                        {
                            Code = tr Terms.``Session Enrolment Code``
                            Pin = tr Terms.``Session Enrolment Pin``
                            Repeat = tr Terms.``Session Enrolment Pin Repeat``
                            Submit = tr Terms.``Session Enrolment Submit``
                            Error = refusal |> Option.map (refusalSentence tr)
                        }
            }
    | SessionView.SupplyingPin pending ->
        Some
            {
                Title = tr Terms.``Session Gate Enrolment``
                Body =
                    tr Terms.``Session Gate Enrolment Text``
                    |> fill [ pending.DisplayName; pending.MailHint ]
                Busy = true
                Actions = []
                Form = None
            }
    // the enrolment ended without a session: the code void or expired, or the patient moved
    | SessionView.EnrolmentFailed refusal ->
        Some
            {
                Title = tr Terms.``Session Gate Refused``
                Body = sentences [ refusalSentence tr refusal; tr Terms.``Session Relaunch`` ]
                Busy = false
                Actions = []
                Form = None
            }
    | SessionView.Anonymous
    | SessionView.Open _
    | SessionView.Closing _ -> None
