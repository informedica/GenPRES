/// <summary>
/// The client's session state machine, from the Launch in the url to an open Session: the
/// Session as the client knows it, the one request under way, the messages that move between
/// them and the effects App.fs interprets into commands. The pages read a view of it, with
/// nothing of the request. Pure F#, opens Shared.Types only, no React, so it runs under Expecto
/// in .NET as well as under Fable.
/// </summary>
/// <remarks>
/// The file module is <c>SessionMachine</c> and not <c>Session</c>: the type-first pair
/// <c>type SessionState</c> / <c>module SessionState</c> lives here beside the messages, the
/// effects and the view, and a file module named after the domain would shadow the
/// <c>Session</c> the rest of the client speaks of.
/// </remarks>
module SessionMachine

open Shared.Types


/// The client's view of its Session, one phase at a time.
/// The Session as the pages show it: the states a page can be in, each with what is valid in it
/// and nothing of the request: today's phases without the Launch, the key and the counts. A
/// refusal is Retryable when the same Launch can be presented again; the count of a server
/// unreachable is always the maximum, so the view carries none.
[<RequireQualifiedAccess>]
type SessionView =
    | Anonymous
    | Launching of attempt: int
    | Resuming
    | Open of SessionOpened
    | Closing of SessionOpened
    | Refused of LaunchRefusal
    | Retryable of LaunchRefusal
    | Unreachable
    | Ended of SessionEnding
    | Enrolling of EnrolmentPending * refusal: PinRefusal option
    | SupplyingPin of EnrolmentPending
    | EnrolmentFailed of PinRefusal


/// The Session as the client knows it, no request here: none; open; refused, the server
/// unreachable, or ended; enrolling, with the last refusal of the form if any; or the enrolment
/// failed.
[<RequireQualifiedAccess>]
type SessionPhase =
    | Anonymous
    | Open of SessionOpened
    | Refused of LaunchRefusal
    | Unreachable
    // the server ended the Session and said so once; the User continues anonymously or
    // relaunches
    | Ended of SessionEnding
    // the launch waits on a PIN; the browser holds the attempt in a cookie, the gate shows the
    // form, and the last refusal of the form if any
    | Enrolling of EnrolmentPending * refusal: PinRefusal option
    // the enrolment ended without a PIN (the code void or expired) or, the PIN set, with
    // another Patient active in MainEHR; a relaunch is the only way on
    | EnrolmentFailed of PinRefusal


/// The one request under way: the presentation, and which attempt it is; GetSession after a
/// reload or the IdentityProvider's return; CloseSession; SupplyPin.
[<RequireQualifiedAccess>]
type SessionRequest =
    | Presenting of attempt: int
    | Resuming
    | Closing
    | SupplyingPin


/// The phase, the one request under way, the Launch this page presents with its key, kept
/// while presenting it again is meaningful (during a presentation, after the server was
/// unreachable, after a refusal worth retrying), and the notice that the record moved on: the
/// newest version told while the open Session is on an older one, the pages' own beside the
/// phase, none unless the Session is open with nothing under way. Built through the
/// constructors below only, which admit the combinations that occur.
type SessionState =
    private
        {
            Phase: SessionPhase
            InFlight: SessionRequest option
            Presentation: (Launch * PublicKey) option
            MovedOn: OrderPlanHead option
        }


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
    // the confirmation code and the chosen PIN, from the gate's form
    | SupplyPin of code: string * pin: string
    // Error = transport failure
    | PinAnswered of Result<PinOutcome, string>
    // from #/session?refused={reason}, the answer to the identity callback
    | RefusedAtCallback of LaunchRefusal
    | OpenAnonymous
    | Close
    | Closed
    // the close request did not reach the server: the cookie is still there, so is the Session
    | CloseFailed of reason: string
    // a signing answer said the server ended the Session at the wrong-PIN limit
    | EndedByServer of SessionEnding
    // a signature re-minted the OpenedToken over the new head; the patient as the Session
    // holds it after the sign comes with it, and whom the signed version names, which the
    // Session is for from here
    | TokenRenewed of OpenedToken * Patient * identity: NameAndBirthDate option
    // take up the version the notice named: it becomes what the Session opened with
    | OpenVersion of id: string
    // the answer: the Session as it now is (Some), nothing to open (None), or a transport
    // failure; `from` is the OpenedToken the request started from, so that an answer lands
    // only on the Session that asked (the guard of Outcome and of the signing answers)
    | Reopened of from: OpenedToken option * Result<SessionOpened option, string>
    // what a computing reply told beside its answer: the record moved on, or the server ended
    // the Session; `from` is the OpenedToken the request started from, the same guard
    | Told of from: OpenedToken option * RecordNotice
    // a signature was refused because the record moved on: the newest version, to offer
    | Blocked of OrderPlanHead


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
    // the orders of the version the Session opened with go into the cart, over the
    // patient as the client holds it after SetPatient (normal values applied); interpreted as
    // the plan machine's Version, which keeps the version while that patient is on its way
    | LoadCart of SignedOrderPlan
    // processSession OpenVersion; `from` comes back in Reopened
    | CallOpenVersion of id: string * from: OpenedToken option
    // the version is open; told once
    | TellVersionOpened of OrderPlanHead
    // the record moved on to this version; told once per version, the bar offers it
    | TellMovedOn of OrderPlanHead


/// The notice that the record moved on, as the Session keeps it: the newest head it was told,
/// so that it is told once per version, and the bar can offer that version. Spent when the
/// version is opened; gone with the Session.
module MovedOn =

    /// A notice arrived: the head to keep, and whether it is news. Versions are ordered by
    /// `No`, their place in the record, not by arrival: replies to concurrent requests can land out of order, so
    /// a notice of a version no newer than the one kept is not news and keeps nothing, and only
    /// a newer version replaces the kept one.
    let receive (current: OrderPlanHead option) (head: OrderPlanHead) : OrderPlanHead option * bool =
        match current with
        | Some kept when kept.No >= head.No -> current, false
        | _ -> Some head, true


    /// A version was opened: the notice is spent when the version opened is at
    /// least as new as the one kept; a newer notice, told while the request was in flight,
    /// stays, so the offer to open it stays too.
    let opened (current: OrderPlanHead option) (head: OrderPlanHead) : OrderPlanHead option =
        match current with
        | Some kept when kept.No > head.No -> current
        | _ -> None


module SessionState =

    /// Presentations are attempted this many times before the UI offers Retry.
    let maxAttempts = 3


    /// Whether a refusal answered by presentLaunch itself is worth retrying with the same
    /// Launch and key: only a missing browser identity is.
    let private worthRetrying refusal =
        match refusal with
        | LaunchRefusal.NoBrowserIdentity -> true
        | LaunchRefusal.LaunchExpired
        | LaunchRefusal.LaunchSpent
        | LaunchRefusal.LaunchInvalid
        | LaunchRefusal.NoRole
        | LaunchRefusal.WrongActivePatient
        | LaunchRefusal.EnrolmentRequired -> false


    let anonymous =
        {
            Phase = SessionPhase.Anonymous
            InFlight = None
            Presentation = None
            MovedOn = None
        }


    /// The presentation under way, the attempt it is; there is no Session meanwhile.
    let launching (launch: Launch) (key: PublicKey) (attempt: int) =
        {
            Phase = SessionPhase.Anonymous
            InFlight = Some(SessionRequest.Presenting attempt)
            Presentation = Some(launch, key)
            MovedOn = None
        }


    /// GetSession under way, after a reload or the IdentityProvider's return.
    let resuming =
        {
            Phase = SessionPhase.Anonymous
            InFlight = Some SessionRequest.Resuming
            Presentation = None
            MovedOn = None
        }


    /// The Session open, nothing under way, with the notice that the record moved on if any.
    let opened (session: SessionOpened) (movedOn: OrderPlanHead option) =
        {
            Phase = SessionPhase.Open session
            InFlight = None
            Presentation = None
            MovedOn = movedOn
        }


    /// CloseSession under way over the open Session; the notice goes with the Session it was for.
    let closing (session: SessionOpened) =
        {
            Phase = SessionPhase.Open session
            InFlight = Some SessionRequest.Closing
            Presentation = None
            MovedOn = None
        }


    /// Refused, with nothing to present again.
    let refused (refusal: LaunchRefusal) =
        {
            Phase = SessionPhase.Refused refusal
            InFlight = None
            Presentation = None
            MovedOn = None
        }


    /// Refused, with the same Launch and key kept to present again.
    let retryable (refusal: LaunchRefusal) (launch: Launch) (key: PublicKey) =
        {
            Phase = SessionPhase.Refused refusal
            InFlight = None
            Presentation = Some(launch, key)
            MovedOn = None
        }


    /// The server did not answer the last attempt; the Launch and key are kept to present again.
    let unreachable (launch: Launch) (key: PublicKey) =
        {
            Phase = SessionPhase.Unreachable
            InFlight = None
            Presentation = Some(launch, key)
            MovedOn = None
        }


    /// The server ended the Session and said so.
    let ended (ending: SessionEnding) =
        {
            Phase = SessionPhase.Ended ending
            InFlight = None
            Presentation = None
            MovedOn = None
        }


    /// The launch waits on a PIN: the form is shown, with the last refusal if any.
    let enrolling (pending: EnrolmentPending) (refusal: PinRefusal option) =
        {
            Phase = SessionPhase.Enrolling(pending, refusal)
            InFlight = None
            Presentation = None
            MovedOn = None
        }


    /// SupplyPin under way; the form is sent once at a time, and the refusal it answers is spent.
    let supplyingPin (pending: EnrolmentPending) =
        {
            Phase = SessionPhase.Enrolling(pending, None)
            InFlight = Some SessionRequest.SupplyingPin
            Presentation = None
            MovedOn = None
        }


    /// The enrolment ended without a Session.
    let enrolmentFailed (refusal: PinRefusal) =
        {
            Phase = SessionPhase.EnrolmentFailed refusal
            InFlight = None
            Presentation = None
            MovedOn = None
        }


    /// The Session held, none unless open and not closing: what a request is completed from.
    let session (state: SessionState) =
        match state.Phase, state.InFlight with
        | SessionPhase.Open session, None -> Some session
        | _ -> None


    /// The token of the Session held, none unless open and not closing.
    let token (state: SessionState) = state |> session |> Option.bind _.OpenedToken


    /// The newest version told while the open Session is on an older one; none otherwise.
    let movedOn (state: SessionState) = state.MovedOn


    /// The Session as the pages show it: the request wins when it can render on its own, the
    /// phase when the request needs the phase's payload; a stray request on any other phase
    /// shows as that phase, and the presentation is read under a refusal only.
    let view (state: SessionState) : SessionView =
        match state.Phase, state.InFlight, state.Presentation with
        | _, Some(SessionRequest.Presenting attempt), _ -> SessionView.Launching attempt
        | _, Some SessionRequest.Resuming, _ -> SessionView.Resuming
        | SessionPhase.Open opened, Some SessionRequest.Closing, _ -> SessionView.Closing opened
        | SessionPhase.Enrolling(pending, _), Some SessionRequest.SupplyingPin, _ -> SessionView.SupplyingPin pending
        | SessionPhase.Anonymous, _, _ -> SessionView.Anonymous
        | SessionPhase.Open opened, _, _ -> SessionView.Open opened
        | SessionPhase.Refused refusal, _, Some _ -> SessionView.Retryable refusal
        | SessionPhase.Refused refusal, _, None -> SessionView.Refused refusal
        | SessionPhase.Unreachable, _, _ -> SessionView.Unreachable
        | SessionPhase.Ended ending, _, _ -> SessionView.Ended ending
        | SessionPhase.Enrolling(pending, refusal), _, _ -> SessionView.Enrolling(pending, refusal)
        | SessionPhase.EnrolmentFailed refusal, _, _ -> SessionView.EnrolmentFailed refusal


    /// The state and effects of a Session that just opened: the patient goes through
    /// UpdatePatient, the key of this Session is the one to keep, and the orders of the version
    /// it opened with go into the cart. The patient reaches the plan machine a message later
    /// than the version does, so the machine keeps the version until it has.
    let onOpened (session: SessionOpened) =
        opened session None,
        [
            SessionEffect.SetPatient(session.PatientContext |> Option.bind _.Patient)
            match session.KeyThumbprint with
            | Some thumbprint -> SessionEffect.KeepKey thumbprint
            | None -> ()
            match session.PatientContext, session.Head with
            | Some _, Some head -> SessionEffect.LoadCart head
            | _ -> ()
        ]


    /// A fresh presentation of the Launch, attempt 1.
    let present (launch: Launch) (key: PublicKey) =
        launching launch key 1, [ SessionEffect.CallPresentLaunch(launch, key) ]


    /// Every arm names the phase and the request under way, and every new state is built through
    /// a constructor, so that a Launch kept to present again never outlives the state it belongs
    /// to.
    let transition (msg: SessionMsg) (state: SessionState) : SessionState * SessionEffect list =
        match msg, state.Phase, state.InFlight with
        // a presentation under way is never replaced by a second one for the same Launch; the
        // Launch kept after a refusal or the server unreachable is presented afresh
        | SessionMsg.Present(launch, _), _, Some(SessionRequest.Presenting _) when
            state.Presentation |> Option.exists (fun (current, _) -> current = launch)
            ->
            state, []
        // in every other state a Present starts a fresh presentation; a different Launch
        // supersedes the one under way, whose outcome is then dropped by the guard below
        | SessionMsg.Present(launch, key), _, _ -> present launch key

        // the stale-request guard: an outcome lands only on the presentation that sent it
        | SessionMsg.Outcome(launch, key, result), _, Some(SessionRequest.Presenting attempt) when
            state.Presentation = Some(launch, key)
            ->
            match result with
            | Ok(LaunchOutcome.Opened session) -> onOpened session
            | Ok(LaunchOutcome.RedirectTo url) -> state, [ SessionEffect.GoTo url ]
            | Ok(LaunchOutcome.Refused refusal) ->
                (if worthRetrying refusal then
                     retryable refusal launch key
                 else
                     refused refusal),
                []
            | Error _ when attempt < maxAttempts ->
                launching launch key (attempt + 1), [ SessionEffect.CallPresentLaunch(launch, key) ]
            | Error _ -> unreachable launch key, []
        | SessionMsg.Outcome _, _, _ -> state, []

        // a retry always carries the same Launch and the same key, so the server answers it
        // as it answered the first presentation
        | SessionMsg.Retry, SessionPhase.Unreachable, None
        | SessionMsg.Retry, SessionPhase.Refused _, None ->
            match state.Presentation with
            | Some(launch, key) -> present launch key
            | None -> state, []
        | SessionMsg.Retry, _, _ -> state, []

        | SessionMsg.Resume, SessionPhase.Anonymous, None -> resuming, [ SessionEffect.CallGetSession ]
        | SessionMsg.Resume, _, _ -> state, []

        | SessionMsg.Resumed(Ok(ResumeResult.Found session)), SessionPhase.Anonymous, Some SessionRequest.Resuming ->
            onOpened session
        // told once: the cookie is gone, the gate says why, the User chooses; the close
        // acknowledges the ending: the server deletes the cookie and drops the mark
        | SessionMsg.Resumed(Ok(ResumeResult.Ended ending)), SessionPhase.Anonymous, Some SessionRequest.Resuming ->
            ended ending, [ SessionEffect.CallCloseSession ]
        // the launch waits on a PIN: the gate shows the form
        | SessionMsg.Resumed(Ok(ResumeResult.Enrolling pending)), SessionPhase.Anonymous, Some SessionRequest.Resuming ->
            enrolling pending None, []
        | SessionMsg.Resumed _, SessionPhase.Anonymous, Some SessionRequest.Resuming -> anonymous, []
        | SessionMsg.Resumed _, _, _ -> state, []

        // the Launch was consumed server-side; nothing is left to retry with
        | SessionMsg.RefusedAtCallback refusal, _, _ -> refused refusal, []

        // an anonymous open carries nothing over from the launch
        | SessionMsg.OpenAnonymous, SessionPhase.Refused _, None
        | SessionMsg.OpenAnonymous, SessionPhase.Unreachable, None
        | SessionMsg.OpenAnonymous, SessionPhase.Ended _, None -> anonymous, [ SessionEffect.SetPatient None ]
        | SessionMsg.OpenAnonymous, _, _ -> state, []

        // the form is sent once at a time, and the refusal it answers is spent; the answer lands
        // only on the request in flight
        | SessionMsg.SupplyPin(code, pin), SessionPhase.Enrolling(pending, _), None ->
            supplyingPin pending, [ SessionEffect.CallSupplyPin(code, pin) ]
        | SessionMsg.SupplyPin _, _, _ -> state, []
        | SessionMsg.PinAnswered(Ok(PinOutcome.Opened session)),
          SessionPhase.Enrolling _,
          Some SessionRequest.SupplyingPin -> onOpened session
        // the form stays open with what went wrong (a wrong code with a try left; a PIN out of format)
        | SessionMsg.PinAnswered(Ok(PinOutcome.Refused(PinRefusal.WrongCode _ as refusal))),
          SessionPhase.Enrolling(pending, _),
          Some SessionRequest.SupplyingPin
        | SessionMsg.PinAnswered(Ok(PinOutcome.Refused(PinRefusal.PinFormat as refusal))),
          SessionPhase.Enrolling(pending, _),
          Some SessionRequest.SupplyingPin -> enrolling pending (Some refusal), []
        // terminal: the code is void or expired, or the active Patient moved; relaunch
        | SessionMsg.PinAnswered(Ok(PinOutcome.Refused refusal)),
          SessionPhase.Enrolling _,
          Some SessionRequest.SupplyingPin -> enrolmentFailed refusal, []
        // the request never got there: the attempt stands, the form comes back as it was
        | SessionMsg.PinAnswered(Error _), SessionPhase.Enrolling(pending, _), Some SessionRequest.SupplyingPin ->
            enrolling pending None, []
        | SessionMsg.PinAnswered _, _, _ -> state, []

        | SessionMsg.Close, SessionPhase.Open session, None -> closing session, [ SessionEffect.CallCloseSession ]
        | SessionMsg.Close, _, _ -> state, []

        // a launched patient and everything derived from it leave with the session. Closed
        // lands only on the close under way: a close that completes after a newer presentation
        // has superseded it must not touch the newer session (the same guard as Outcome)
        | SessionMsg.Closed, _, Some SessionRequest.Closing -> anonymous, [ SessionEffect.SetPatient None ]
        | SessionMsg.Closed, _, _ -> state, []

        // a close that never reached the server has closed nothing: the Session stays open,
        // with its patient, and the UI says so; the same guard as Closed
        | SessionMsg.CloseFailed _, SessionPhase.Open session, Some SessionRequest.Closing -> opened session None, []
        | SessionMsg.CloseFailed _, _, _ -> state, []

        // the server ended the Session at a signature; the gate says why and the close
        // acknowledges it, as a Resumed ending does; a close under way is left to complete
        | SessionMsg.EndedByServer ending, SessionPhase.Open _, None -> ended ending, [ SessionEffect.CallCloseSession ]
        | SessionMsg.EndedByServer _, _, _ -> state, []

        // the token the next signature has to present, and the patient as the Session now holds
        // it, with whom it is for as the signed version names them: into the context it opened
        // with, and to the panel as at a resume. A Session without a patient context signs
        // nothing, so there is no context to fill
        | SessionMsg.TokenRenewed(token, patient, identity), SessionPhase.Open session, None ->
            let renewed =
                { session with
                    OpenedToken = Some token
                    PatientContext =
                        session.PatientContext
                        |> Option.map (fun c ->
                            { c with
                                Patient = Some patient
                                Identity = identity
                            }
                        )
                }

            opened renewed state.MovedOn, [ SessionEffect.SetPatient(Some patient) ]
        | SessionMsg.TokenRenewed _, _, _ -> state, []

        // only an open Session has a version to take up; the request remembers the token it
        // started from
        | SessionMsg.OpenVersion id, SessionPhase.Open session, None ->
            state, [ SessionEffect.CallOpenVersion(id, session.OpenedToken) ]
        | SessionMsg.OpenVersion _, _, _ -> state, []

        // the Session as the server now holds it: the token over the version opened, and its
        // orders into the cart; the patient is unchanged, so no SetPatient. Nothing to open, or
        // the request never got there: the Session stays as it was, and the next request tells
        // what the head is. The stale-request guard: an answer lands only on the open Session
        // that still holds the token the request started from; a Session closed, relaunched or
        // reopened meanwhile drops it
        | SessionMsg.Reopened(from, Ok(Some session)), SessionPhase.Open current, None when current.OpenedToken = from ->
            match session.PatientContext, session.Head with
            // the version is open: told once, and the notice is spent by it; a newer notice
            // told meanwhile stays, with its offer
            | Some _, Some head ->
                opened session (MovedOn.opened state.MovedOn head.Head),
                [ SessionEffect.LoadCart head; SessionEffect.TellVersionOpened head.Head ]
            | _ -> opened session state.MovedOn, []
        | SessionMsg.Reopened _, _, _ -> state, []

        // what a reply told with its answer: the stale-request guard is the one Reopened has,
        // the notice counts only when the request started from the token the open Session
        // holds now, with nothing under way; a reply of a Session since closed, replaced or
        // re-minted says nothing about this one, and the next request repeats what still holds
        | SessionMsg.Told(from, notice), SessionPhase.Open current, None when current.OpenedToken = from ->
            match notice with
            // told once per version: kept, and said when it is news
            | RecordNotice.NewerVersion head ->
                let kept, news = MovedOn.receive state.MovedOn head
                opened current kept, (if news then [ SessionEffect.TellMovedOn head ] else [])
            // the server ended the Session; the gate says why and the close acknowledges it
            | RecordNotice.Ended ending -> ended ending, [ SessionEffect.CallCloseSession ]
        | SessionMsg.Told _, _, _ -> state, []

        // a signature refused because the record moved on: the head is kept for the bar, and
        // not told again, since the refusal already said it
        | SessionMsg.Blocked head, SessionPhase.Open current, None ->
            opened current (MovedOn.receive state.MovedOn head |> fst), []
        | SessionMsg.Blocked _, _, _ -> state, []
