/// <summary>
/// The client's session state machine (plan 409, uc-01 steps 2 to 6): the <c>Session</c> phases,
/// the messages that move between them and the effects App.fs interprets into commands.
/// Pure F#, opens Shared.Types only, no React, so it runs under Expecto in .NET as well as
/// under Fable.
/// </summary>
/// <remarks>
/// The file module is <c>SessionMachine</c> and not <c>Session</c>: the call sites are
/// <c>Session.Anonymous</c> (the DU, RequireQualifiedAccess) and <c>Session.transition</c> (a
/// module function), which needs the type-first pair <c>type Session</c> / <c>module Session</c>,
/// and that pair cannot live in a file module also called <c>Session</c> (FS0035 after
/// <c>open Session</c>).
/// </remarks>
module SessionMachine

open Shared.Types


/// The client's view of its Session, one phase at a time (uc-01 steps 2 to 6).
[<RequireQualifiedAccess>]
type Session =
    | Anonymous
    // the presentation in flight, and which attempt it is
    | Launching of Launch * PublicKey * attempt: int
    // GetSession in flight (reload, IdentityProvider return)
    | Resuming
    | Open of SessionOpened
    // CloseSession in flight; Closed lands here and nowhere else
    | Closing of SessionOpened
    // no Session; the Launch and key are kept only when a retry is meaningful
    | Refused of LaunchRefusal * retry: (Launch * PublicKey) option
    // ext 3a: server down after the page was served
    | Unreachable of Launch * PublicKey * attempts: int


[<RequireQualifiedAccess>]
type SessionMsg =
    // key pair made by Keys.fs, once per page load
    | Present of Launch * PublicKey
    // Error = transport failure
    | Outcome of Launch * PublicKey * Result<LaunchOutcome, string>
    // from Unreachable, or Refused with a retry
    | Retry
    | Resume
    | Resumed of Result<SessionOpened option, string>
    // from #/session?refused={reason}, launch step 4.5
    | RefusedAtCallback of LaunchRefusal
    | OpenAnonymous
    | Close
    | Closed


[<RequireQualifiedAccess>]
type SessionEffect =
    | CallPresentLaunch of Launch * PublicKey
    | CallGetSession
    | CallCloseSession
    // window.location.assign, for RedirectTo
    | GoTo of url: string
    // interpreted as UpdatePatient, so everything derived from the patient reloads
    | SetPatient of Patient option
    // Keys.keep: prune the other private keys
    | KeepKey of thumbprint: string


module Session =

    /// Presentations are attempted this many times before the UI offers Retry.
    let maxAttempts = 3


    /// Whether a refusal answered by presentLaunch itself is worth retrying with the same
    /// Launch and key: only a missing browser identity is (ext 3c).
    let retryable refusal =
        match refusal with
        | LaunchRefusal.NoBrowserIdentity -> true
        | LaunchRefusal.LaunchExpired
        | LaunchRefusal.LaunchSpent
        | LaunchRefusal.LaunchInvalid
        | LaunchRefusal.NoRole
        | LaunchRefusal.WrongActivePatient
        | LaunchRefusal.EnrolmentRequired -> false


    /// The state and effects of a Session that just opened: the patient goes through
    /// UpdatePatient, and the key of this Session is the one to keep.
    let opened (session: SessionOpened) =
        Session.Open session,
        [
            SessionEffect.SetPatient(session.PatientContext |> Option.map _.Patient)
            match session.KeyThumbprint with
            | Some thumbprint -> SessionEffect.KeepKey thumbprint
            | None -> ()
        ]


    let present launch key =
        Session.Launching(launch, key, 1), [ SessionEffect.CallPresentLaunch(launch, key) ]


    let transition (msg: SessionMsg) (state: Session) : Session * SessionEffect list =
        match msg, state with
        // an in-flight presentation is never replaced by a second one for the same Launch
        | SessionMsg.Present(launch, _), Session.Launching(current, _, _) when launch = current -> state, []
        // in every other state a Present starts a fresh presentation; a different Launch
        // supersedes the current one, whose outcome is then dropped by the guard below
        | SessionMsg.Present(launch, key), _ -> present launch key

        // the stale-request guard: an outcome lands only on the presentation that sent it
        | SessionMsg.Outcome(launch, key, result), Session.Launching(current, currentKey, attempt) when
            launch = current && key = currentKey
            ->
            match result with
            | Ok(LaunchOutcome.Opened session) -> opened session
            | Ok(LaunchOutcome.RedirectTo url) -> state, [ SessionEffect.GoTo url ]
            | Ok(LaunchOutcome.Refused refusal) ->
                let retry = if retryable refusal then Some(launch, key) else None
                Session.Refused(refusal, retry), []
            | Error _ when attempt < maxAttempts ->
                Session.Launching(launch, key, attempt + 1), [ SessionEffect.CallPresentLaunch(launch, key) ]
            | Error _ -> Session.Unreachable(launch, key, attempt), []
        | SessionMsg.Outcome _, _ -> state, []

        // a retry always carries the same Launch and the same key (Rule 2)
        | SessionMsg.Retry, Session.Unreachable(launch, key, _) -> present launch key
        | SessionMsg.Retry, Session.Refused(_, Some(launch, key)) -> present launch key
        | SessionMsg.Retry, _ -> state, []

        | SessionMsg.Resume, Session.Anonymous -> Session.Resuming, [ SessionEffect.CallGetSession ]
        | SessionMsg.Resume, _ -> state, []

        | SessionMsg.Resumed(Ok(Some session)), Session.Resuming -> opened session
        | SessionMsg.Resumed _, Session.Resuming -> Session.Anonymous, []
        | SessionMsg.Resumed _, _ -> state, []

        // the Launch was consumed server-side; nothing is left to retry with
        | SessionMsg.RefusedAtCallback refusal, _ -> Session.Refused(refusal, None), []

        // an anonymous open carries nothing over (Rule 7)
        | SessionMsg.OpenAnonymous, Session.Refused _
        | SessionMsg.OpenAnonymous, Session.Unreachable _ -> Session.Anonymous, [ SessionEffect.SetPatient None ]
        | SessionMsg.OpenAnonymous, _ -> state, []

        | SessionMsg.Close, Session.Open session -> Session.Closing session, [ SessionEffect.CallCloseSession ]
        | SessionMsg.Close, _ -> state, []

        // a launched patient and everything derived from it leave with the session. Closed
        // lands only on Closing: a close that completes after a newer presentation has
        // superseded it must not touch the newer session (the same guard as Outcome)
        | SessionMsg.Closed, Session.Closing _ -> Session.Anonymous, [ SessionEffect.SetPatient None ]
        | SessionMsg.Closed, _ -> state, []
