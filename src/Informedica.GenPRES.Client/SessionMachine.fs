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
    // no Session: the server ended it (Rule 11) and said so once; the User continues
    // anonymously or relaunches
    | Ended of SessionEnding
    // no Session yet: the launch waits on a PIN (UC-2); the browser holds the attempt in a
    // cookie, the gate shows the form, and the last refusal of the form if any
    | Enrolling of EnrolmentPending * refusal: PinRefusal option
    // SupplyPin in flight
    | SupplyingPin of EnrolmentPending
    // no Session: the enrolment ended without a PIN (the code void or expired) or, the PIN
    // set, with another Patient active (Rule 6); a relaunch is the only way on
    | EnrolmentFailed of PinRefusal


/// What GetSession answered at a resume.
[<RequireQualifiedAccess>]
type ResumeResult =
    | Found of SessionOpened
    | NotFound
    | Ended of SessionEnding
    | Enrolling of EnrolmentPending


/// What SupplyPin answered.
[<RequireQualifiedAccess>]
type PinOutcome =
    | Opened of SessionOpened
    | Refused of PinRefusal


[<RequireQualifiedAccess>]
type SessionMsg =
    // key pair made by Keys.fs, once per page load
    | Present of Launch * PublicKey
    // Error = transport failure
    | Outcome of Launch * PublicKey * Result<LaunchOutcome, string>
    // from Unreachable, or Refused with a retry
    | Retry
    | Resume
    | Resumed of Result<ResumeResult, string>
    // UC-2: the confirmation code and the chosen PIN, from the gate's form
    | SupplyPin of code: string * pin: string
    // Error = transport failure
    | PinAnswered of Result<PinOutcome, string>
    // from #/session?refused={reason}, launch step 4.5
    | RefusedAtCallback of LaunchRefusal
    | OpenAnonymous
    | Close
    | Closed
    // the close request did not reach the server: the cookie is still there, so is the Session
    | CloseFailed of reason: string
    // UC-3: a signing answer said the server ended the Session (Rule 28)
    | EndedByServer of SessionEnding
    // UC-3: a signature re-minted the OpenedToken over the new head (Rule 34)
    | TokenRenewed of OpenedToken


[<RequireQualifiedAccess>]
type SessionEffect =
    | CallPresentLaunch of Launch * PublicKey
    | CallGetSession
    | CallCloseSession
    | CallSupplyPin of code: string * pin: string
    // window.location.assign, for RedirectTo
    | GoTo of url: string
    // interpreted as UpdatePatient, so everything derived from the patient reloads
    | SetPatient of Patient option
    // Keys.keep: prune the other private keys
    | KeepKey of thumbprint: string
    // Rule 19: the orders of the version the Session opened with go into the cart, over the
    // patient as the client holds it after SetPatient (normal values applied); interpreted as
    // a FilterOrderPlan over them
    | LoadCart of SignedOrderPlan


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
    /// UpdatePatient, the key of this Session is the one to keep, and the orders of the version
    /// it opened with go into the cart (Rule 19), after the patient so that the cart is built
    /// over it.
    let opened (session: SessionOpened) =
        Session.Open session,
        [
            SessionEffect.SetPatient(session.PatientContext |> Option.map _.Patient)
            match session.KeyThumbprint with
            | Some thumbprint -> SessionEffect.KeepKey thumbprint
            | None -> ()
            match session.PatientContext, session.Head with
            | Some _, Some head -> SessionEffect.LoadCart head
            | _ -> ()
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

        | SessionMsg.Resumed(Ok(ResumeResult.Found session)), Session.Resuming -> opened session
        // told once (Rule 11): the cookie is gone, the gate says why, the User chooses
        // the close acknowledges the ending: the server deletes the cookie and drops the mark
        | SessionMsg.Resumed(Ok(ResumeResult.Ended ending)), Session.Resuming ->
            Session.Ended ending, [ SessionEffect.CallCloseSession ]
        // the launch waits on a PIN (UC-2): the gate shows the form
        | SessionMsg.Resumed(Ok(ResumeResult.Enrolling pending)), Session.Resuming ->
            Session.Enrolling(pending, None), []
        | SessionMsg.Resumed _, Session.Resuming -> Session.Anonymous, []
        | SessionMsg.Resumed _, _ -> state, []

        // the Launch was consumed server-side; nothing is left to retry with
        | SessionMsg.RefusedAtCallback refusal, _ -> Session.Refused(refusal, None), []

        // UC-2: the form is sent once at a time; the answer lands only on the request in flight
        | SessionMsg.SupplyPin(code, pin), Session.Enrolling(pending, _) ->
            Session.SupplyingPin pending, [ SessionEffect.CallSupplyPin(code, pin) ]
        | SessionMsg.SupplyPin _, _ -> state, []
        | SessionMsg.PinAnswered(Ok(PinOutcome.Opened session)), Session.SupplyingPin _ -> opened session
        // the form stays open with what went wrong (ext 2b, a try left; a PIN out of format)
        | SessionMsg.PinAnswered(Ok(PinOutcome.Refused(PinRefusal.WrongCode _ as refusal))),
          Session.SupplyingPin pending
        | SessionMsg.PinAnswered(Ok(PinOutcome.Refused(PinRefusal.PinFormat as refusal))), Session.SupplyingPin pending ->
            Session.Enrolling(pending, Some refusal), []
        // terminal: the code is void or expired, or the Patient moved (Rule 6); relaunch
        | SessionMsg.PinAnswered(Ok(PinOutcome.Refused refusal)), Session.SupplyingPin _ ->
            Session.EnrolmentFailed refusal, []
        // the request never got there: the attempt stands, the form comes back as it was
        | SessionMsg.PinAnswered(Error _), Session.SupplyingPin pending -> Session.Enrolling(pending, None), []
        | SessionMsg.PinAnswered _, _ -> state, []

        // an anonymous open carries nothing over (Rule 7)
        | SessionMsg.OpenAnonymous, Session.Refused _
        | SessionMsg.OpenAnonymous, Session.Unreachable _
        | SessionMsg.OpenAnonymous, Session.Ended _ -> Session.Anonymous, [ SessionEffect.SetPatient None ]
        | SessionMsg.OpenAnonymous, _ -> state, []

        | SessionMsg.Close, Session.Open session -> Session.Closing session, [ SessionEffect.CallCloseSession ]
        | SessionMsg.Close, _ -> state, []

        // a launched patient and everything derived from it leave with the session. Closed
        // lands only on Closing: a close that completes after a newer presentation has
        // superseded it must not touch the newer session (the same guard as Outcome)
        | SessionMsg.Closed, Session.Closing _ -> Session.Anonymous, [ SessionEffect.SetPatient None ]
        | SessionMsg.Closed, _ -> state, []

        // a close that never reached the server has closed nothing: the Session stays open,
        // with its patient, and the UI says so; the same guard as Closed
        | SessionMsg.CloseFailed _, Session.Closing session -> Session.Open session, []
        | SessionMsg.CloseFailed _, _ -> state, []

        // UC-3: the server ended the Session at a signature (Rule 28); the gate says why and
        // the close acknowledges it, as a Resumed ending does
        | SessionMsg.EndedByServer ending, Session.Open _ -> Session.Ended ending, [ SessionEffect.CallCloseSession ]
        | SessionMsg.EndedByServer _, _ -> state, []

        // UC-3: the token the next signature has to present (Rule 34)
        | SessionMsg.TokenRenewed token, Session.Open session ->
            Session.Open { session with OpenedToken = Some token }, []
        | SessionMsg.TokenRenewed _, _ -> state, []
