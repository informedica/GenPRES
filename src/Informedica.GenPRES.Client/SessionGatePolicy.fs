/// <summary>
/// The session gate's policy (plan 409, uc-01 Refusals): for every session phase, what the gate
/// says and what it offers. Pure F#, no React, so it runs under Expecto next to the machine;
/// Views/SessionGate.fs renders it. The texts are `Terms`, translated by the caller: the view
/// passes the sheet lookup, the tests pass `english`.
/// </summary>
module SessionGatePolicy

open Shared
open Shared.Types
open SessionMachine


/// What the gate offers besides the text.
[<RequireQualifiedAccess>]
type Action =
    | Retry
    | ContinueWithoutLaunch


/// The enrolment form (UC-2): the labels of its three fields and its button, and what the
/// server said about the last submission, if anything.
type EnrolmentForm =
    {
        Code: string
        Pin: string
        Repeat: string
        Submit: string
        Error: string option
    }


/// One gate: a title, a body, whether work is in progress, the actions, and the form when the
/// launch waits on a PIN.
type Gate =
    {
        Title: string
        Body: string
        Busy: bool
        Actions: Action list
        Form: EnrolmentForm option
    }


/// The English of the session terms: what the gate and the session menu show when the sheet has
/// no row for a term, or the terms have not loaded. Any other term falls back to its name, as
/// `Global.pageToString` does.
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
    | _ -> $"{term}"


/// Fills `{0}`, `{1}`, ... in a translated sentence with the given values, in order.
let fill (args: string list) (s: string) =
    args
    |> List.indexed
    |> List.fold (fun s (i, a) -> s |> String.replace $"{{{i}}}" a) s


/// Joins translated sentences into one body; an empty translation adds no sentence.
let sentences (xs: string list) =
    xs |> List.filter (String.isNullOrWhiteSpace >> not) |> String.concat " "


/// The sentences of a refusal: what happened, then what the User can do. Every sentence is one
/// term, so no translation is ever embedded in another.
let refusalBody (tr: Terms -> string) refusal =
    match refusal with
    | LaunchRefusal.LaunchExpired ->
        [
            tr Terms.``Session Refusal Expired``
            tr Terms.``Session Relaunch``
        ]
    | LaunchRefusal.LaunchSpent ->
        [
            tr Terms.``Session Refusal Spent``
            tr Terms.``Session Relaunch``
        ]
    | LaunchRefusal.LaunchInvalid ->
        [
            tr Terms.``Session Refusal Invalid``
            tr Terms.``Session Relaunch``
        ]
    | LaunchRefusal.NoBrowserIdentity -> [ tr Terms.``Session Refusal No Browser Identity`` ]
    | LaunchRefusal.NoRole -> [ tr Terms.``Session Refusal No Role`` ]
    | LaunchRefusal.WrongActivePatient -> [ tr Terms.``Session Refusal Wrong Patient`` ]
    | LaunchRefusal.EnrolmentRequired -> [ tr Terms.``Session Refusal Enrolment`` ]


/// Digits only, of a given length range: what the form checks before the server does.
let digits (min: int) (max: int) (s: string) =
    not (isNull s)
    && s.Length >= min
    && s.Length <= max
    && s |> Seq.forall (fun c -> c >= '0' && c <= '9')


/// The form's own check before a submission (UC-2): the code has six digits, the PIN four to
/// six, and the repeat agrees. The first thing wrong, as a translated sentence; None when the
/// submission can go.
let formError (tr: Terms -> string) (code: string) (pin: string) (repeat: string) : string option =
    if not (digits 6 6 code) then
        Some(tr Terms.``Session Enrolment Code Format``)
    elif not (digits 4 6 pin) then
        Some(tr Terms.``Session Enrolment Pin Format``)
    elif pin <> repeat then
        Some(tr Terms.``Session Enrolment Pins Differ``)
    else
        None


/// What the server said about a submission that left the form open.
let refusalSentence (tr: Terms -> string) (refusal: PinRefusal) =
    match refusal with
    | PinRefusal.WrongCode left -> tr Terms.``Session Enrolment Wrong Code`` |> fill [ $"%i{left}" ]
    | PinRefusal.PinFormat -> tr Terms.``Session Enrolment Pin Format``
    | PinRefusal.CodeVoid -> tr Terms.``Session Enrolment Code Void``
    | PinRefusal.AttemptExpired -> tr Terms.``Session Enrolment Expired``
    | PinRefusal.WrongActivePatient -> tr Terms.``Session Refusal Wrong Patient``


/// Whether the gate is over the app: false while the app is usable (anonymous, open, closing).
let isGated (session: Session) =
    match session with
    | Session.Anonymous
    | Session.Open _
    | Session.Closing _ -> false
    | Session.Launching _
    | Session.Resuming
    | Session.Unreachable _
    | Session.Refused _
    | Session.Ended _
    | Session.Enrolling _
    | Session.SupplyingPin _
    | Session.EnrolmentFailed _ -> true


/// The gate for a session phase, or None when the app is usable (anonymous, open, closing).
let gateFor (tr: Terms -> string) (session: Session) : Gate option =
    match session with
    | Session.Launching(_, _, attempt) ->
        Some
            {
                Title = tr Terms.``Session Gate Opening``
                Body =
                    tr Terms.``Session Gate Opening Text``
                    |> fill [ $"%i{attempt}"; $"%i{Session.maxAttempts}" ]
                Busy = true
                Actions = []
                Form = None
            }
    | Session.Resuming ->
        Some
            {
                Title = tr Terms.``Session Gate Resuming``
                Body = tr Terms.``Session Gate Resuming Text``
                Busy = true
                Actions = []
                Form = None
            }
    | Session.Unreachable(_, _, attempts) ->
        Some
            {
                Title = tr Terms.``Session Gate Unreachable``
                Body =
                    sentences
                        [
                            tr Terms.``Session Gate Unreachable Text`` |> fill [ $"%i{attempts}" ]
                            tr Terms.``Session Gate Try Again Or Relaunch``
                        ]
                Busy = false
                Actions = [ Action.Retry ]
                Form = None
            }
    | Session.Refused(refusal, retry) ->
        Some
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
                        | _, Some _ -> Action.Retry
                        | _ -> ()
                    ]
                Form = None
            }
    | Session.Ended ending ->
        Some
            {
                Title = tr Terms.``Session Gate Ended``
                Body =
                    sentences
                        [
                            match ending with
                            | SessionEnding.SupersededByLaunch -> tr Terms.``Session Ending Superseded``
                            tr Terms.``Session Relaunch``
                        ]
                Busy = false
                Actions = [ Action.ContinueWithoutLaunch ]
                Form = None
            }
    // UC-2: the launch waits on a PIN; the form asks for the mailed code and the PIN twice
    | Session.Enrolling(pending, refusal) ->
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
    | Session.SupplyingPin pending ->
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
    // the enrolment ended without a Session: the code void or expired, or the Patient moved
    | Session.EnrolmentFailed refusal ->
        Some
            {
                Title = tr Terms.``Session Gate Refused``
                Body =
                    sentences
                        [
                            refusalSentence tr refusal
                            tr Terms.``Session Relaunch``
                        ]
                Busy = false
                Actions = []
                Form = None
            }
    | Session.Anonymous
    | Session.Open _
    | Session.Closing _ -> None
