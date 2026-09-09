/// <summary>
/// The session gate's policy (plan 409, uc-01 Refusals): for every session phase, what the gate
/// says and what it offers. Pure F#, no React, so it runs under Expecto next to the machine;
/// Views/SessionGate.fs renders it.
/// </summary>
module SessionGatePolicy

open Shared.Types
open SessionMachine


/// What the gate offers besides the text.
[<RequireQualifiedAccess>]
type Action =
    | Retry
    | ContinueWithoutLaunch


/// One gate: a title, a body, whether work is in progress, and the actions.
type Gate =
    {
        Title: string
        Body: string
        Busy: bool
        Actions: Action list
    }


// English for now. These become Shared.Localization.Terms cases plus sheet rows once the
// terms are agreed; the keys below are the intended term names.
let relaunch = "Open GenPRES again from MainEHR."

let relaunchAfter = "open GenPRES again from MainEHR."

let refusalBody refusal =
    match refusal with
    | LaunchRefusal.LaunchExpired -> $"The launch has expired. {relaunch}"
    | LaunchRefusal.LaunchSpent -> $"This launch has already been used. {relaunch}"
    | LaunchRefusal.LaunchInvalid -> $"The launch is not valid. {relaunch}"
    | LaunchRefusal.NoBrowserIdentity -> "Your browser could not be identified."
    | LaunchRefusal.NoRole ->
        "You have no role in GenPRES. You can continue without a launch: no patient is carried over."
    | LaunchRefusal.WrongActivePatient ->
        $"The patient active in MainEHR is not the patient of this launch. Activate the right patient and {relaunchAfter}"
    | LaunchRefusal.EnrolmentRequired ->
        $"A PIN has to be set before prescribing. Enrolment is not available yet. {relaunch}"


/// The gate for a session phase, or None when the app is usable (anonymous, open, closing).
let gateFor (session: Session) : Gate option =
    match session with
    | Session.Launching(_, _, attempt) ->
        Some
            {
                Title = "Opening your session"
                Body = $"Presenting the launch, attempt %i{attempt} of %i{Session.maxAttempts}."
                Busy = true
                Actions = []
            }
    | Session.Resuming ->
        Some
            {
                Title = "Resuming your session"
                Body = "Checking for an open session."
                Busy = true
                Actions = []
            }
    | Session.Unreachable(_, _, attempts) ->
        Some
            {
                Title = "GenPRES could not be reached"
                Body = $"The server did not answer after %i{attempts} attempts. Try again, or {relaunchAfter}"
                Busy = false
                Actions = [ Action.Retry ]
            }
    | Session.Refused(refusal, retry) ->
        Some
            {
                Title = "GenPRES could not open your session"
                Body =
                    match refusal, retry with
                    | LaunchRefusal.NoBrowserIdentity, Some _ -> $"{refusalBody refusal} Try again."
                    | LaunchRefusal.NoBrowserIdentity, None -> $"{refusalBody refusal} {relaunch}"
                    | _ -> refusalBody refusal
                Busy = false
                Actions =
                    [
                        match refusal, retry with
                        | LaunchRefusal.NoRole, _ -> Action.ContinueWithoutLaunch
                        | _, Some _ -> Action.Retry
                        | _ -> ()
                    ]
            }
    | Session.Anonymous
    | Session.Open _
    | Session.Closing _ -> None
