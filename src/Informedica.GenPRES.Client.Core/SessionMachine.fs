/// Tracks the client's session from the launch url to an open session, one server request at a
/// time. The App carries out the effects.
module SessionMachine

open Shared.Types


/// What the pages read of the SessionState: the session's phase combined with the request under
/// way.
[<RequireQualifiedAccess>]
type SessionView =
    /// No session.
    | Anonymous
    /// The launch is being presented; this attempt.
    | Launching of attempt: int
    /// The session is being read again after a reload or a login.
    | Resuming
    /// The session is open.
    | Open of SessionOpened
    /// The session is closing.
    | Closing of SessionOpened
    /// The server refused the launch.
    | LaunchRefused of LaunchRefusal
    /// The server refused the launch, and the same launch can be presented again.
    | LaunchRetryable of LaunchRefusal
    /// The server did not answer the last attempt.
    | ServerUnreachable
    /// The server ended the session.
    | Ended of SessionEnding
    /// The launch waits on a PIN, with the last refusal of the form if any.
    | Enrolling of EnrolmentPending * refusal: PinRefusal option
    /// The PIN is being sent.
    | SupplyingPin of EnrolmentPending
    /// The enrolment failed; only a new launch goes on.
    | EnrolmentFailed of PinRefusal


/// What the session lane does once nothing is out any more, after the url moved on past the
/// session.
[<RequireQualifiedAccess>]
type SessionFollow =
    /// Stay anonymous: the url's patient or medication is used without a session.
    | Anonymous
    /// Present the launch the url brought, with the key made for this page load.
    | Launch of Launch * PublicKey


/// The session itself, without any request under way.
[<RequireQualifiedAccess>]
type SessionPhase =
    /// No session.
    | Anonymous
    /// The session is open.
    | Open of SessionOpened
    /// The server refused the launch.
    | LaunchRefused of LaunchRefusal
    /// The server did not answer the last attempt.
    | ServerUnreachable
    /// The server ended the session and said so once; the user continues anonymously or
    /// launches again.
    | Ended of SessionEnding
    /// The launch waits on a PIN. A cookie holds the attempt; the gate shows the form, with the
    /// last refusal if any.
    | Enrolling of EnrolmentPending * refusal: PinRefusal option
    /// The enrolment failed: the code was void or expired, or another patient became active in
    /// MainEHR. Only a new launch goes on.
    | EnrolmentFailed of PinRefusal
    /// The url brought a patient, a medication or a launch. The session is left: a request still
    /// out runs to its end without reaching the lanes, and then the lane does what follows.
    | UrlMovedOn of SessionFollow


/// The one request under way.
[<RequireQualifiedAccess>]
type SessionRequest =
    /// The launch is presented; this attempt.
    | PresentingLaunch of attempt: int
    /// The session read again, after a reload or a login.
    | Resuming
    /// The session closing.
    | Closing
    /// The PIN being sent.
    | SupplyingPin


/// A refresh or an open of a version under way. It is beside the request under way, not one of
/// them, since a workbench or plan request meanwhile still carries the token of the open session.
[<RequireQualifiedAccess>]
type SessionReopening =
    /// The EHR read again and the latest version reopened.
    | RefreshPatient
    /// The version with this id opened.
    | OpenSignedPlan of id: string


/// Everything the session machine holds, hidden from the pages, which read a SessionView of it
/// instead.
type SessionState =
    private
        {
            /// The session itself.
            Phase: SessionPhase
            /// The one request under way, if any.
            InFlight: SessionRequest option
            /// The refresh or the open under way, if any; only while the session is open.
            Reopening: SessionReopening option
            /// The launch and its key, kept while it can still be presented again: during a
            /// presentation, after the server was unreachable, or after a refusal worth retrying.
            Presentation: (Launch * PublicKey) option
            /// The newest order plan version, when the open session is on an older one; only
            /// while the session is open with no request under way.
            NewerPlan: OrderPlanHead option
        }


/// What the server answered when the session was read again.
[<RequireQualifiedAccess>]
type ResumeResult =
    /// An open session.
    | Found of SessionOpened
    /// No session.
    | NotFound
    /// The server ended the session.
    | Ended of SessionEnding
    /// The launch waits on a PIN.
    | Enrolling of EnrolmentPending


/// What SupplyPin answered.
[<RequireQualifiedAccess>]
type PinOutcome =
    /// The session opened.
    | Opened of SessionOpened
    /// The server refused the PIN or the code.
    | Refused of PinRefusal


/// What moves the session machine.
[<RequireQualifiedAccess>]
type SessionMsg =
    /// Present the launch, with the key pair made once per page load. With a session open or a
    /// request out, it is presented once those are left, as for a launch url.
    | PresentLaunch of Launch * PublicKey
    /// The server's answer to a presentation; Error is a transport failure.
    | LaunchOutcome of Launch * PublicKey * Result<LaunchOutcome, string>
    /// Present the same launch again, after the server was unreachable or refused with a retry.
    | RetryLaunch
    /// Read the session again, after a reload or a login.
    | Resume
    /// The answer to Resume.
    | Resumed of Result<ResumeResult, string>
    /// Send the mailed code and the chosen PIN from the gate's form.
    | SupplyPin of code: string * pin: string
    /// The answer to SupplyPin; Error is a transport failure.
    | PinAnswered of Result<PinOutcome, string>
    /// The identity callback refused the launch (#/session?refused={reason}).
    | LaunchRefused of LaunchRefusal
    /// Continue without a session.
    | ContinueAnonymous
    /// The url brought a patient, a medication or a launch: the lane leaves the session. A launch
    /// comes after, by PresentLaunch.
    | UrlMovedOn
    /// Close the session.
    | CloseSession
    /// The session is closed.
    | SessionClosed
    /// The close did not reach the server; the cookie, and so the session, are still there.
    | CloseFailed of reason: string
    /// A signing answer said the server ended the session at the wrong-PIN limit.
    | SignatureEndedSession of SessionEnding
    /// A signature renewed the token. The patient after the signature comes with it, and the
    /// version signed, the last signed order plan from now on, with the identity it names,
    /// which the session is for from now on.
    | SignatureRenewedToken of OpenedToken * Patient * SignedOrderPlan
    /// Open the version a notice named; it becomes the version the session opened with.
    | OpenSignedPlan of id: string
    /// The answer to OpenSignedPlan: the session as it now is, None when there was nothing to open,
    /// or a transport failure. The token the request started from lets the answer land only on
    /// the session that asked.
    | SignedPlanOpened of from: OpenedToken option * Result<SessionOpened option, string>
    /// Read the EHR again and reopen the latest version on it: the way out of a held patient
    /// context that takes the EHR's data and drops the new and changed orders.
    | RefreshPatient
    /// The answer to RefreshPatient, as for SignedPlanOpened, with the patient read again.
    | PatientRefreshed of from: OpenedToken option * Result<SessionOpened option, string>
    /// What a reply said besides its answer: a newer version exists, or the server ended the
    /// session. The token the request started from guards it, as for SignedPlanOpened.
    | NoticeReceived of from: OpenedToken option * RecordNotice
    /// A signature was refused because a newer version exists; this is the version to offer.
    | SignatureBlocked of OrderPlanHead


/// What the App carries out for the session machine.
[<RequireQualifiedAccess>]
type SessionEffect =
    /// Present the launch to the server.
    | CallPresentLaunch of Launch * PublicKey
    /// Read the session from the server.
    | CallResume
    /// Close the session on the server.
    | CallCloseSession
    /// Send the mailed code and the PIN to the server.
    | CallSupplyPin of code: string * pin: string
    /// Go to the url the server redirected to.
    | GoToIdentityProvider of url: string
    /// Set the patient data, so everything derived from it reloads.
    | SetPatientData of Patient option
    /// Keep this private key and remove the others.
    | KeepBrowserKey of thumbprint: string
    /// The orders of the version the session opened with go into the cart; the patient reaches the
    /// plan machine a message later, so the plan machine keeps the orders until it has the patient.
    | LoadSignedPlan of SignedOrderPlan
    /// Ask the server to open the version; the token comes back in SignedPlanOpened.
    | CallOpenSignedPlan of id: string * from: OpenedToken option
    /// Ask the server to read the EHR again; the token comes back in PatientRefreshed.
    | CallRefreshPatient of from: OpenedToken option
    /// Tell the user the version is open.
    | TellSignedPlanOpened of OrderPlanHead
    /// Tell the user a newer version exists, once per version; the bar offers it.
    | TellNewerSignedPlan of OrderPlanHead
    /// Tell the user the refresh did not happen; the orders stay as they are.
    | TellPatientRefreshFailed


/// The newest version the session was told about, so the user is told once per version and the
/// bar can offer it. It goes when that version is opened or the session ends.
module NewerPlan =

    /// The version to keep after a notice, and whether the notice is news. Versions are compared
    /// by number, not by arrival, since replies can land out of order: only a newer version is
    /// news and replaces the kept one.
    let receive (current: OrderPlanHead option) (head: OrderPlanHead) : OrderPlanHead option * bool =
        match current with
        | Some kept when kept.No >= head.No -> current, false
        | _ -> Some head, true


    /// The version to keep after a version was opened: none, unless the kept one is newer than
    /// the one opened.
    let opened (current: OrderPlanHead option) (head: OrderPlanHead) : OrderPlanHead option =
        match current with
        | Some kept when kept.No > head.No -> current
        | _ -> None


/// The constructors and the transition of the session machine.
module SessionState =

    /// The number of attempts to present a launch before the gate offers a retry.
    let maxAttempts = 3


    /// Whether a refused presentation is worth retrying with the same launch and key: only a
    /// missing browser identity is.
    let private worthRetrying refusal =
        match refusal with
        | LaunchRefusal.NoBrowserIdentity -> true
        | LaunchRefusal.LaunchExpired
        | LaunchRefusal.LaunchSpent
        | LaunchRefusal.LaunchInvalid
        | LaunchRefusal.NoRole
        | LaunchRefusal.WrongActivePatient
        | LaunchRefusal.EnrolmentRequired -> false


    /// No session, nothing under way.
    let anonymous =
        {
            Phase = SessionPhase.Anonymous
            InFlight = None
            Reopening = None
            Presentation = None
            NewerPlan = None
        }


    /// The launch presented, at this attempt; no session meanwhile.
    let launching (launch: Launch) (key: PublicKey) (attempt: int) =
        {
            Phase = SessionPhase.Anonymous
            InFlight = Some(SessionRequest.PresentingLaunch attempt)
            Reopening = None
            Presentation = Some(launch, key)
            NewerPlan = None
        }


    /// The session read again after a reload or a login.
    let resuming =
        {
            Phase = SessionPhase.Anonymous
            InFlight = Some SessionRequest.Resuming
            Reopening = None
            Presentation = None
            NewerPlan = None
        }


    /// The session open, with the newer version to offer if any.
    let opened (session: SessionOpened) (newerPlan: OrderPlanHead option) =
        {
            Phase = SessionPhase.Open session
            InFlight = None
            Reopening = None
            Presentation = None
            NewerPlan = newerPlan
        }


    /// The session closing; the newer version to offer goes with it.
    let closing (session: SessionOpened) =
        {
            Phase = SessionPhase.Open session
            InFlight = Some SessionRequest.Closing
            Reopening = None
            Presentation = None
            NewerPlan = None
        }


    /// The launch refused, with nothing to present again.
    let refused (refusal: LaunchRefusal) =
        {
            Phase = SessionPhase.LaunchRefused refusal
            InFlight = None
            Reopening = None
            Presentation = None
            NewerPlan = None
        }


    /// The launch refused, with the launch and key kept to present again.
    let retryable (refusal: LaunchRefusal) (launch: Launch) (key: PublicKey) =
        {
            Phase = SessionPhase.LaunchRefused refusal
            InFlight = None
            Reopening = None
            Presentation = Some(launch, key)
            NewerPlan = None
        }


    /// The server did not answer the last attempt; the launch and key are kept to present again.
    let unreachable (launch: Launch) (key: PublicKey) =
        {
            Phase = SessionPhase.ServerUnreachable
            InFlight = None
            Reopening = None
            Presentation = Some(launch, key)
            NewerPlan = None
        }


    /// The session ended by the server.
    let ended (ending: SessionEnding) =
        {
            Phase = SessionPhase.Ended ending
            InFlight = None
            Reopening = None
            Presentation = None
            NewerPlan = None
        }


    /// The launch waits on a PIN; the form shows the last refusal if any.
    let enrolling (pending: EnrolmentPending) (refusal: PinRefusal option) =
        {
            Phase = SessionPhase.Enrolling(pending, refusal)
            InFlight = None
            Reopening = None
            Presentation = None
            NewerPlan = None
        }


    /// The PIN being sent; the last refusal is cleared.
    let supplyingPin (pending: EnrolmentPending) =
        {
            Phase = SessionPhase.Enrolling(pending, None)
            InFlight = Some SessionRequest.SupplyingPin
            Reopening = None
            Presentation = None
            NewerPlan = None
        }


    /// The enrolment failed.
    let enrolmentFailed (refusal: PinRefusal) =
        {
            Phase = SessionPhase.EnrolmentFailed refusal
            InFlight = None
            Reopening = None
            Presentation = None
            NewerPlan = None
        }


    /// No session, the close out, and what follows once it has answered. The url moved on past
    /// the session: the close carries the cookie, and its answer sends nothing.
    let leaving (following: SessionFollow) =
        {
            Phase = SessionPhase.UrlMovedOn following
            InFlight = Some SessionRequest.Closing
            Reopening = None
            Presentation = None
            NewerPlan = None
        }


    /// The open session, when nothing is under way; requests take their token from it.
    let session (state: SessionState) =
        match state.Phase, state.InFlight with
        | SessionPhase.Open session, None -> Some session
        | _ -> None


    /// The token of the open session, when nothing is under way.
    let token (state: SessionState) = state |> session |> Option.bind _.OpenedToken


    /// The newer version to offer, if any.
    let newerPlan (state: SessionState) = state.NewerPlan


    /// Whether a launch, a resume, a PIN or a close is under way.
    let inFlight (state: SessionState) = state.InFlight.IsSome


    /// Whether a request is under way whose answer can change what the pages show. After the url
    /// moved on with nothing to follow, none can: whatever is still out ends without reaching
    /// the lanes, and the lane stays anonymous.
    let changing (state: SessionState) =
        match state.Phase with
        | SessionPhase.UrlMovedOn SessionFollow.Anonymous -> false
        | _ -> state.InFlight.IsSome


    /// The refresh or the open under way, if any.
    let reopening (state: SessionState) = state.Reopening


    /// What the pages read: the request when it can show on its own, else the phase. A refusal
    /// with a launch kept is LaunchRetryable. After the url moved on the lane reads as anonymous, or as
    /// launching when a launch follows, whatever is still out.
    let view (state: SessionState) : SessionView =
        match state.Phase, state.InFlight, state.Presentation with
        | SessionPhase.UrlMovedOn SessionFollow.Anonymous, _, _ -> SessionView.Anonymous
        | SessionPhase.UrlMovedOn(SessionFollow.Launch _), _, _ -> SessionView.Launching 1
        | _, Some(SessionRequest.PresentingLaunch attempt), _ -> SessionView.Launching attempt
        | _, Some SessionRequest.Resuming, _ -> SessionView.Resuming
        | SessionPhase.Open opened, Some SessionRequest.Closing, _ -> SessionView.Closing opened
        | SessionPhase.Enrolling(pending, _), Some SessionRequest.SupplyingPin, _ -> SessionView.SupplyingPin pending
        | SessionPhase.Anonymous, _, _ -> SessionView.Anonymous
        | SessionPhase.Open opened, _, _ -> SessionView.Open opened
        | SessionPhase.LaunchRefused refusal, _, Some _ -> SessionView.LaunchRetryable refusal
        | SessionPhase.LaunchRefused refusal, _, None -> SessionView.LaunchRefused refusal
        | SessionPhase.ServerUnreachable, _, _ -> SessionView.ServerUnreachable
        | SessionPhase.Ended ending, _, _ -> SessionView.Ended ending
        | SessionPhase.Enrolling(pending, refusal), _, _ -> SessionView.Enrolling(pending, refusal)
        | SessionPhase.EnrolmentFailed refusal, _, _ -> SessionView.EnrolmentFailed refusal


    /// The state and effects of a session that just opened: set the patient, keep this session's
    /// key, and load the orders of its version into the cart.
    let onOpened (session: SessionOpened) =
        opened session None,
        [
            SessionEffect.SetPatientData(session.PatientContext |> Option.bind _.Patient)
            match session.KeyThumbprint with
            | Some thumbprint -> SessionEffect.KeepBrowserKey thumbprint
            | None -> ()
            match session.PatientContext, session.Head with
            | Some _, Some head -> SessionEffect.LoadSignedPlan head
            | _ -> ()
        ]


    /// A new presentation of the launch, at attempt 1.
    let present (launch: Launch) (key: PublicKey) =
        launching launch key 1, [ SessionEffect.CallPresentLaunch(launch, key) ]


    /// What follows a lane the url moved on past, now that nothing is out: anonymous, or the
    /// launch presented.
    let follow following =
        match following with
        | SessionFollow.Anonymous -> anonymous, []
        | SessionFollow.Launch(launch, key) -> present launch key


    /// The lane left for what follows. A launch or a PIN out stays out, since its answer sets the
    /// cookie that only a close sent after it can delete; a close out stays out. Whatever else is
    /// open or out is closed now, and its answer finds no request. With nothing out and no
    /// session open a launch to follow is presented at once; otherwise the close goes, for the
    /// session the cookie may still hold. The newest url decides what follows.
    let moveOn following (state: SessionState) =
        match state.Phase, state.InFlight, following with
        | _, Some(SessionRequest.PresentingLaunch _ | SessionRequest.SupplyingPin), _ ->
            { state with Phase = SessionPhase.UrlMovedOn following }, []
        | _, Some SessionRequest.Closing, _ -> leaving following, []
        | SessionPhase.Open _, _, _ -> leaving following, [ SessionEffect.CallCloseSession ]
        | _, None, SessionFollow.Launch(launch, key) -> present launch key
        | _ -> leaving following, [ SessionEffect.CallCloseSession ]


    /// The next state and effects for a message. Every new state is built through a constructor,
    /// so a launch kept to present again never outlives the state it belongs to.
    let transition (msg: SessionMsg) (state: SessionState) : SessionState * SessionEffect list =
        match msg, state.Phase, state.InFlight with
        // the url moved on past the session, with nothing to follow
        | SessionMsg.UrlMovedOn, _, _ -> moveOn SessionFollow.Anonymous state
        // the answers the lane still waits on after the url moved on. A launch or a PIN that
        // opens a session closes it again, since the cookie it set must go; any other end of the
        // request leads to what follows. Nothing reaches the lanes
        | SessionMsg.LaunchOutcome(launch, key, result),
          SessionPhase.UrlMovedOn next,
          Some(SessionRequest.PresentingLaunch _) when state.Presentation = Some(launch, key) ->
            match result with
            | Ok(LaunchOutcome.Opened _) -> leaving next, [ SessionEffect.CallCloseSession ]
            | _ -> follow next
        | SessionMsg.PinAnswered(Ok(PinOutcome.Opened _)),
          SessionPhase.UrlMovedOn next,
          Some SessionRequest.SupplyingPin -> leaving next, [ SessionEffect.CallCloseSession ]
        | SessionMsg.PinAnswered _, SessionPhase.UrlMovedOn next, Some SessionRequest.SupplyingPin
        | SessionMsg.SessionClosed, SessionPhase.UrlMovedOn next, Some SessionRequest.Closing
        | SessionMsg.CloseFailed _, SessionPhase.UrlMovedOn next, Some SessionRequest.Closing -> follow next

        // the same launch again while it is presented, after the url moved on: the presentation
        // under way is the launch the url brings, so the lane takes its outcome as at any launch.
        // Presenting it again would fail, since the server takes a launch once
        | SessionMsg.PresentLaunch(launch, _), SessionPhase.UrlMovedOn _, Some(SessionRequest.PresentingLaunch _) when
            state.Presentation |> Option.exists (fun (current, _) -> current = launch)
            ->
            { state with Phase = SessionPhase.Anonymous }, []
        // a presentation under way is not replaced by a second one for the same launch
        | SessionMsg.PresentLaunch(launch, _), _, Some(SessionRequest.PresentingLaunch _) when
            state.Presentation |> Option.exists (fun (current, _) -> current = launch)
            ->
            state, []
        // any other launch is presented once nothing is out, as a launch url is: an open session or
        // a request out is left first, and the server's close deletes the cookie before the
        // launch's answer sets the new one
        | SessionMsg.PresentLaunch(launch, key), _, _ -> moveOn (SessionFollow.Launch(launch, key)) state

        // an outcome lands only on the presentation that sent it
        | SessionMsg.LaunchOutcome(launch, key, result), _, Some(SessionRequest.PresentingLaunch attempt) when
            state.Presentation = Some(launch, key)
            ->
            match result with
            | Ok(LaunchOutcome.Opened session) -> onOpened session
            | Ok(LaunchOutcome.RedirectTo url) -> state, [ SessionEffect.GoToIdentityProvider url ]
            | Ok(LaunchOutcome.Refused refusal) ->
                (if worthRetrying refusal then
                     retryable refusal launch key
                 else
                     refused refusal),
                []
            | Error _ when attempt < maxAttempts ->
                launching launch key (attempt + 1), [ SessionEffect.CallPresentLaunch(launch, key) ]
            | Error _ -> unreachable launch key, []
        | SessionMsg.LaunchOutcome _, _, _ -> state, []

        // a retry sends the same launch and key, so the server answers as it did the first time
        | SessionMsg.RetryLaunch, SessionPhase.ServerUnreachable, None
        | SessionMsg.RetryLaunch, SessionPhase.LaunchRefused _, None ->
            match state.Presentation with
            | Some(launch, key) -> present launch key
            | None -> state, []
        | SessionMsg.RetryLaunch, _, _ -> state, []

        | SessionMsg.Resume, SessionPhase.Anonymous, None -> resuming, [ SessionEffect.CallResume ]
        | SessionMsg.Resume, _, _ -> state, []

        | SessionMsg.Resumed(Ok(ResumeResult.Found session)), SessionPhase.Anonymous, Some SessionRequest.Resuming ->
            onOpened session
        // the gate says why once and the user chooses; the close tells the server to delete the
        // cookie
        | SessionMsg.Resumed(Ok(ResumeResult.Ended ending)), SessionPhase.Anonymous, Some SessionRequest.Resuming ->
            ended ending, [ SessionEffect.CallCloseSession ]
        // the launch waits on a PIN; the gate shows the form
        | SessionMsg.Resumed(Ok(ResumeResult.Enrolling pending)), SessionPhase.Anonymous, Some SessionRequest.Resuming ->
            enrolling pending None, []
        | SessionMsg.Resumed _, SessionPhase.Anonymous, Some SessionRequest.Resuming -> anonymous, []
        | SessionMsg.Resumed _, _, _ -> state, []

        // the server used up the launch; nothing is left to retry
        | SessionMsg.LaunchRefused refusal, _, _ -> refused refusal, []

        // continuing anonymously keeps nothing from the launch
        | SessionMsg.ContinueAnonymous, SessionPhase.LaunchRefused _, None
        | SessionMsg.ContinueAnonymous, SessionPhase.ServerUnreachable, None
        | SessionMsg.ContinueAnonymous, SessionPhase.Ended _, None -> anonymous, [ SessionEffect.SetPatientData None ]
        | SessionMsg.ContinueAnonymous, _, _ -> state, []

        // the form is sent once at a time; the answer lands only on the request in flight
        | SessionMsg.SupplyPin(code, pin), SessionPhase.Enrolling(pending, _), None ->
            supplyingPin pending, [ SessionEffect.CallSupplyPin(code, pin) ]
        | SessionMsg.SupplyPin _, _, _ -> state, []
        | SessionMsg.PinAnswered(Ok(PinOutcome.Opened session)),
          SessionPhase.Enrolling _,
          Some SessionRequest.SupplyingPin -> onOpened session
        // the form stays open and says what went wrong: a wrong code with tries left, or a PIN out
        // of format
        | SessionMsg.PinAnswered(Ok(PinOutcome.Refused(PinRefusal.WrongCode _ as refusal))),
          SessionPhase.Enrolling(pending, _),
          Some SessionRequest.SupplyingPin
        | SessionMsg.PinAnswered(Ok(PinOutcome.Refused(PinRefusal.PinFormat as refusal))),
          SessionPhase.Enrolling(pending, _),
          Some SessionRequest.SupplyingPin -> enrolling pending (Some refusal), []
        // the code is void or expired, or another patient became active; only a new launch goes on
        | SessionMsg.PinAnswered(Ok(PinOutcome.Refused refusal)),
          SessionPhase.Enrolling _,
          Some SessionRequest.SupplyingPin -> enrolmentFailed refusal, []
        // the request never arrived: the form comes back as it was
        | SessionMsg.PinAnswered(Error _), SessionPhase.Enrolling(pending, _), Some SessionRequest.SupplyingPin ->
            enrolling pending None, []
        | SessionMsg.PinAnswered _, _, _ -> state, []

        | SessionMsg.CloseSession, SessionPhase.Open session, None ->
            closing session, [ SessionEffect.CallCloseSession ]
        | SessionMsg.CloseSession, _, _ -> state, []

        // the patient leaves with the session. SessionClosed lands only on a close under way, so a late
        // close never touches a newer session
        | SessionMsg.SessionClosed, _, Some SessionRequest.Closing -> anonymous, [ SessionEffect.SetPatientData None ]
        | SessionMsg.SessionClosed, _, _ -> state, []

        // a close that never reached the server closed nothing: the session stays open with its
        // patient
        | SessionMsg.CloseFailed _, SessionPhase.Open session, Some SessionRequest.Closing -> opened session None, []
        | SessionMsg.CloseFailed _, _, _ -> state, []

        // the server ended the session at a signature: the gate says why and the close tells the
        // server; a close under way is left to complete
        | SessionMsg.SignatureEndedSession ending, SessionPhase.Open _, None ->
            ended ending, [ SessionEffect.CallCloseSession ]
        | SessionMsg.SignatureEndedSession _, _, _ -> state, []

        // the new token for the next signature, the version signed as the session's head, and the
        // patient and identity the signed version names, into the session's patient context and
        // to the panel. A session without a patient context signs nothing, so there is none to fill
        | SessionMsg.SignatureRenewedToken(token, patient, signed), SessionPhase.Open session, None ->
            let renewed =
                { session with
                    OpenedToken = Some token
                    Head = Some signed
                    PatientContext =
                        session.PatientContext
                        |> Option.map (fun c ->
                            { c with
                                Patient = Some patient
                                Identity = signed.Identity
                            }
                        )
                }

            { state with Phase = SessionPhase.Open renewed }, [ SessionEffect.SetPatientData(Some patient) ]
        | SessionMsg.SignatureRenewedToken _, _, _ -> state, []

        // only an open session can open a version, one refresh or open at a time; the request
        // carries its token
        | SessionMsg.OpenSignedPlan id, SessionPhase.Open session, None when state.Reopening.IsNone ->
            { state with Reopening = Some(SessionReopening.OpenSignedPlan id) },
            [ SessionEffect.CallOpenSignedPlan(id, session.OpenedToken) ]
        | SessionMsg.OpenSignedPlan _, _, _ -> state, []

        // the session with the version opened: its orders go into the cart; the patient is
        // unchanged. The answer lands only on the open session that still holds the token the
        // request started from; nothing to open, or a failure, leaves the session as it was
        | SessionMsg.SignedPlanOpened(from, Ok(Some session)), SessionPhase.Open current, None when
            current.OpenedToken = from
            ->
            match session.PatientContext, session.Head with
            // the version is open: told once; a newer version told meanwhile stays on offer
            | Some _, Some head ->
                opened session (NewerPlan.opened state.NewerPlan head.Head),
                [
                    SessionEffect.LoadSignedPlan head
                    SessionEffect.TellSignedPlanOpened head.Head
                ]
            | _ -> opened session state.NewerPlan, []
        // any other answer ends the open too, a failure and an answer for an earlier session
        // included
        | SessionMsg.SignedPlanOpened _, _, _ -> { state with Reopening = None }, []

        // only an open session can be refreshed, one refresh or open at a time; the request
        // carries its token
        | SessionMsg.RefreshPatient, SessionPhase.Open session, None when state.Reopening.IsNone ->
            { state with Reopening = Some SessionReopening.RefreshPatient },
            [ SessionEffect.CallRefreshPatient session.OpenedToken ]
        | SessionMsg.RefreshPatient, _, _ -> state, []

        // the session refreshed: the patient read again goes to the panel, which the plan and the
        // workbench follow as after any edit; the plan keeps its new and changed orders. The same
        // guard as SignedPlanOpened
        | SessionMsg.PatientRefreshed(from, Ok(Some session)), SessionPhase.Open current, None when
            current.OpenedToken = from
            ->
            opened session state.NewerPlan,
            [
                SessionEffect.SetPatientData(session.PatientContext |> Option.bind _.Patient)
            ]
        // the user asked for the refresh, so a refresh that did not happen is told
        | SessionMsg.PatientRefreshed(from, (Ok None | Error _)), SessionPhase.Open current, None when
            current.OpenedToken = from
            ->
            { state with Reopening = None }, [ SessionEffect.TellPatientRefreshFailed ]
        | SessionMsg.PatientRefreshed _, _, _ -> { state with Reopening = None }, []

        // a notice counts only when its request started from the token the open session holds now,
        // as for SignedPlanOpened; the next request repeats what still holds
        | SessionMsg.NoticeReceived(from, notice), SessionPhase.Open current, None when current.OpenedToken = from ->
            match notice with
            // kept, and told once per version
            | RecordNotice.NewerVersion head ->
                let kept, news = NewerPlan.receive state.NewerPlan head

                { state with NewerPlan = kept },
                (if news then
                     [ SessionEffect.TellNewerSignedPlan head ]
                 else
                     [])
            // the server ended the session: the gate says why and the close tells the server
            | RecordNotice.Ended ending -> ended ending, [ SessionEffect.CallCloseSession ]
        | SessionMsg.NoticeReceived _, _, _ -> state, []

        // a signature refused for a newer version: the version is kept for the bar, not told
        // again, since the refusal said it
        | SessionMsg.SignatureBlocked head, SessionPhase.Open _, None ->
            { state with NewerPlan = NewerPlan.receive state.NewerPlan head |> fst }, []
        | SessionMsg.SignatureBlocked _, _, _ -> state, []
