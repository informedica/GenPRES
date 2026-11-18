namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the client's session from the launch url to an open session, one server request at a
/// time. The App carries out the effects.
module SessionMachine =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Shared.Types


    /// The session state as the pages see it: the phase combined with the request under way.
    [<RequireQualifiedAccess>]
    type SessionView =
        /// No session.
        | Anonymous
        /// The launch is being presented to the server, at this attempt number.
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
        /// The launch waits for a PIN, with the last PIN refusal if any.
        | Enrolling of EnrolmentPending * refusal: PinRefusal option
        /// The PIN is being sent.
        | SupplyingPin of EnrolmentPending
        /// The enrolment failed; only a new launch can open a session.
        | EnrolmentFailed of PinRefusal


    /// What the session machine does after the url brought something new, once no request is under
    /// way any more.
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
        /// The launch waits for a PIN. A cookie holds the enrolment attempt; the gate shows the PIN form,
        /// with the last refusal if any.
        | Enrolling of EnrolmentPending * refusal: PinRefusal option
        /// The enrolment failed: the code was void or expired, or another patient became active in
        /// MainEHR. Only a new launch can open a session.
        | EnrolmentFailed of PinRefusal
        /// The url brought a patient, a medication or a launch, so the session is left. A request still
        /// under way runs to its end without effect on the lanes; then the machine does what follows.
        | UrlMovedOn of SessionFollow


    /// The request under way; there is at most one at a time.
    [<RequireQualifiedAccess>]
    type SessionRequest =
        /// The launch is being presented, at this attempt number.
        | PresentingLaunch of attempt: int
        /// The session is being read again, after a reload or a login.
        | Resuming
        /// The session is closing.
        | Closing
        /// The PIN is being sent.
        | SupplyingPin


    /// A patient refresh or an open of a signed plan version under way. It is tracked apart from
    /// SessionRequest because the session stays open meanwhile, and other requests still use its token.
    [<RequireQualifiedAccess>]
    type SessionReopening =
        /// The patient data are being read again from the EHR.
        | RefreshPatient
        /// The signed plan version with this id is being opened.
        | OpenSignedPlan of id: string


    /// The private state of the session machine; the pages read it through SessionState.view.
    type SessionState =
        private
            {
                /// The session itself.
                Phase: SessionPhase
                /// The request under way, if any.
                InFlight: SessionRequest option
                /// The refresh or the open under way, if any; only while the session is open.
                Reopening: SessionReopening option
                /// The launch and its key while the launch can still be presented again: during a
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


    /// The server's answer to a supplied PIN.
    [<RequireQualifiedAccess>]
    type PinOutcome =
        /// The session opened.
        | Opened of SessionOpened
        /// The server refused the PIN or the code.
        | Refused of PinRefusal


    /// What moves the session machine.
    [<RequireQualifiedAccess>]
    type SessionMsg =
        /// Present the launch with the public key made once per page load. With a session open or a
        /// request under way, the launch is presented after those are left.
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
        /// The url brought a patient, a medication or a launch, so the session is left. A launch from
        /// the url follows as a separate PresentLaunch.
        | UrlMovedOn
        /// Close the session.
        | CloseSession
        /// The session is closed.
        | SessionClosed
        /// The close did not reach the server; the cookie, and so the session, are still there.
        | CloseFailed of reason: string
        /// A signing answer said the server ended the session at the wrong-PIN limit.
        | SignatureEndedSession of SessionEnding
        /// A signature renewed the session token. It comes with the patient data as signed and the
        /// signed plan version, which becomes the latest signed version of the session; the session
        /// takes the patient identity that version names.
        | SignatureRenewedToken of OpenedToken * PatientData * SignedOrderPlan
        /// Open the signed plan version with this id; the session then works on that version.
        | OpenSignedPlan of id: string
        /// The answer to OpenSignedPlan: the opened session, None when there was nothing to open, or
        /// Error for a transport failure. The token sent with the request makes the answer apply only
        /// to the session that asked.
        | SignedPlanOpened of from: OpenedToken option * Result<SessionOpened option, string>
        /// Read the patient data again from the EHR. The new data go to the patient panel, and the plan
        /// and the workbench update as after any patient data update.
        | RefreshPatient
        /// The answer to RefreshPatient, with the same token check as SignedPlanOpened.
        | PatientRefreshed of from: OpenedToken option * Result<SessionOpened option, string>
        /// A notice that came with a server reply: a newer plan version exists, or the server ended the
        /// session. It has the same token check as SignedPlanOpened.
        | NoticeReceived of from: OpenedToken option * RecordNotice
        /// A signature was refused because a newer version exists; this is the version to offer.
        | SignatureBlocked of OrderPlanHead
        /// A session command was refused because another browser tab has the session. It has the
        /// same token check as SignedPlanOpened.
        | InactiveTab of from: OpenedToken option


    /// What the App carries out for the session machine.
    [<RequireQualifiedAccess>]
    type SessionEffect =
        /// Present the launch to the server.
        | CallPresentLaunch of Launch * PublicKey
        /// Read the session from the server.
        | CallResume
        /// Close the session on the server, with the token the tab holds, if any.
        | CallCloseSession of OpenedToken option
        /// Send the mailed code and the PIN to the server.
        | CallSupplyPin of code: string * pin: string
        /// Go to the url the server redirected to.
        | GoToIdentityProvider of url: string
        /// Set the patient data, so everything derived from it reloads.
        | SetPatientData of PatientData option
        /// Give the patient data read from the EHR by a refresh to the patient machine.
        | SetPatientDataRead of PatientData option
        /// Give the patient data of an opened version to the patient machine, which takes only the age.
        | TakeAge of PatientData option
        /// Keep this private key and remove the others.
        | KeepBrowserKey of thumbprint: string
        /// Load the orders of the signed plan version into the cart. The patient data reach the plan
        /// machine one message later, so the plan machine waits with the orders until it has them.
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
        /// Show the alert on the snackbar.
        | Alert of Alert.Alert


    /// Tracks the newest plan version the server reported, so the user is told once per version and
    /// the bar can offer it. It is cleared when that version is opened or the session ends.
    module NewerPlan =

        /// Returns the newer of the current and the reported version, and true when the reported
        /// version is newer. Versions are compared by number, because replies can arrive out of order.
        let receive (current: OrderPlanHead option) (head: OrderPlanHead) : OrderPlanHead option * bool =
            match current with
            | Some kept when kept.No >= head.No -> current, false
            | _ -> Some head, true


        /// Returns the version to offer after a version was opened: None, unless the current version
        /// is newer than the opened one.
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


        /// The launch being presented at this attempt number, with no session.
        let launching (launch: Launch) (key: PublicKey) (attempt: int) =
            {
                Phase = SessionPhase.Anonymous
                InFlight = Some(SessionRequest.PresentingLaunch attempt)
                Reopening = None
                Presentation = Some(launch, key)
                NewerPlan = None
            }


        /// The session being read again after a reload or a login.
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


        /// The session closing; the newer version on offer is cleared.
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


        /// The state after the url brought something new: no session, a close under way, and what to
        /// do once the close answers. The close lets the server delete the cookie.
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
        /// brought something new with nothing to follow, no answer can: the session stays anonymous.
        let changing (state: SessionState) =
            match state.Phase with
            | SessionPhase.UrlMovedOn SessionFollow.Anonymous -> false
            | _ -> state.InFlight.IsSome


        /// The refresh or the open under way, if any.
        let reopening (state: SessionState) = state.Reopening


        /// Returns the view the pages read: the request under way when it has its own view, else the
        /// phase. A refusal with a stored launch shows as LaunchRetryable. After the url brought
        /// something new, the view is Anonymous, or Launching when a launch follows.
        let view (state: SessionState) : SessionView =
            match state.Phase, state.InFlight, state.Presentation with
            | SessionPhase.UrlMovedOn SessionFollow.Anonymous, _, _ -> SessionView.Anonymous
            | SessionPhase.UrlMovedOn(SessionFollow.Launch _), _, _ -> SessionView.Launching 1
            | _, Some(SessionRequest.PresentingLaunch attempt), _ -> SessionView.Launching attempt
            | _, Some SessionRequest.Resuming, _ -> SessionView.Resuming
            | SessionPhase.Open opened, Some SessionRequest.Closing, _ -> SessionView.Closing opened
            | SessionPhase.Enrolling(pending, _), Some SessionRequest.SupplyingPin, _ ->
                SessionView.SupplyingPin pending
            | SessionPhase.Anonymous, _, _ -> SessionView.Anonymous
            | SessionPhase.Open opened, _, _ -> SessionView.Open opened
            | SessionPhase.LaunchRefused refusal, _, Some _ -> SessionView.LaunchRetryable refusal
            | SessionPhase.LaunchRefused refusal, _, None -> SessionView.LaunchRefused refusal
            | SessionPhase.ServerUnreachable, _, _ -> SessionView.ServerUnreachable
            | SessionPhase.Ended ending, _, _ -> SessionView.Ended ending
            | SessionPhase.Enrolling(pending, refusal), _, _ -> SessionView.Enrolling(pending, refusal)
            | SessionPhase.EnrolmentFailed refusal, _, _ -> SessionView.EnrolmentFailed refusal


        /// Returns the state and effects for a session that just opened: set the patient data, keep the
        /// browser key of the session if it has one, and load the orders of its signed version into the
        /// cart when it has a patient context and a version.
        let onOpened (session: SessionOpened) =
            opened session None,
            [
                SessionEffect.SetPatientData(session.PatientContext |> Option.bind _.PatientData)
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


        /// Starts what follows once the url brought something new and no request is under way: stay
        /// anonymous, or present the launch.
        let follow following =
            match following with
            | SessionFollow.Anonymous -> anonymous, []
            | SessionFollow.Launch(launch, key) -> present launch key


        /// Leaves the session because the url brought something new. A launch or PIN request under way
        /// is left to finish, because its answer sets a cookie that only a later close can delete; a
        /// close under way is also left to finish. An open session is closed now, and the answer to a
        /// resume under way is ignored. With no request under way and no open session, a launch to
        /// follow is presented at once; otherwise a close is sent for a session the cookie may still
        /// hold. The newest url decides what follows.
        let moveOn following (state: SessionState) =
            match state.Phase, state.InFlight, following with
            | _, Some(SessionRequest.PresentingLaunch _ | SessionRequest.SupplyingPin), _ ->
                { state with Phase = SessionPhase.UrlMovedOn following }, []
            | _, Some SessionRequest.Closing, _ -> leaving following, []
            | SessionPhase.Open session, _, _ ->
                leaving following, [ SessionEffect.CallCloseSession session.OpenedToken ]
            | _, None, SessionFollow.Launch(launch, key) -> present launch key
            | _ -> leaving following, [ SessionEffect.CallCloseSession None ]


        /// Returns the next state and effects for a message. Most new states are built through a
        /// constructor, so a stored launch does not outlive the state it belongs to.
        let transition (msg: SessionMsg) (state: SessionState) : SessionState * SessionEffect list =
            match msg, state.Phase, state.InFlight with
            // the url brought something new, with nothing to follow
            | SessionMsg.UrlMovedOn, _, _ -> moveOn SessionFollow.Anonymous state
            // answers that arrive after the url brought something new. A launch or PIN answer that
            // opens a session closes that session again, because its cookie must be deleted; any
            // other answer starts what follows. No answer reaches the lanes
            | SessionMsg.LaunchOutcome(launch, key, result),
              SessionPhase.UrlMovedOn next,
              Some(SessionRequest.PresentingLaunch _) when state.Presentation = Some(launch, key) ->
                match result with
                | Ok(LaunchOutcome.Opened session) ->
                    leaving next, [ SessionEffect.CallCloseSession session.OpenedToken ]
                | _ -> follow next
            | SessionMsg.PinAnswered(Ok(PinOutcome.Opened session)),
              SessionPhase.UrlMovedOn next,
              Some SessionRequest.SupplyingPin -> leaving next, [ SessionEffect.CallCloseSession session.OpenedToken ]
            | SessionMsg.PinAnswered _, SessionPhase.UrlMovedOn next, Some SessionRequest.SupplyingPin
            | SessionMsg.SessionClosed, SessionPhase.UrlMovedOn next, Some SessionRequest.Closing
            | SessionMsg.CloseFailed _, SessionPhase.UrlMovedOn next, Some SessionRequest.Closing -> follow next

            // the url brings the launch that is already being presented: the machine takes the
            // outcome of that presentation as for any launch. Presenting it again would fail,
            // because the server accepts a launch only once
            | SessionMsg.PresentLaunch(launch, _), SessionPhase.UrlMovedOn _, Some(SessionRequest.PresentingLaunch _) when
                state.Presentation |> Option.exists (fun (current, _) -> current = launch)
                ->
                { state with Phase = SessionPhase.Anonymous }, []
            // a presentation under way is not replaced by a second one for the same launch
            | SessionMsg.PresentLaunch(launch, _), _, Some(SessionRequest.PresentingLaunch _) when
                state.Presentation |> Option.exists (fun (current, _) -> current = launch)
                ->
                state, []
            // any other launch is presented once no request is under way: an open session or a request
            // under way is left first, and the close deletes the old cookie before the launch answer
            // sets the new one
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
                ended ending, [ SessionEffect.CallCloseSession None ]
            // the launch waits on a PIN; the gate shows the form
            | SessionMsg.Resumed(Ok(ResumeResult.Enrolling pending)),
              SessionPhase.Anonymous,
              Some SessionRequest.Resuming -> enrolling pending None, []
            | SessionMsg.Resumed _, SessionPhase.Anonymous, Some SessionRequest.Resuming -> anonymous, []
            | SessionMsg.Resumed _, _, _ -> state, []

            // the server used up the launch; nothing is left to retry
            | SessionMsg.LaunchRefused refusal, _, _ -> refused refusal, []

            // continuing anonymously keeps nothing from the launch
            | SessionMsg.ContinueAnonymous, SessionPhase.LaunchRefused _, None
            | SessionMsg.ContinueAnonymous, SessionPhase.ServerUnreachable, None
            | SessionMsg.ContinueAnonymous, SessionPhase.Ended _, None ->
                anonymous, [ SessionEffect.SetPatientData None ]
            | SessionMsg.ContinueAnonymous, _, _ -> state, []

            // one PIN request at a time; the answer applies only to the request under way
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
            // the request never arrived: the form comes back as it was, and the user is told
            | SessionMsg.PinAnswered(Error _), SessionPhase.Enrolling(pending, _), Some SessionRequest.SupplyingPin ->
                enrolling pending None, [ SessionEffect.Alert Alert.Alert.PinNotSent ]
            | SessionMsg.PinAnswered _, _, _ -> state, []

            | SessionMsg.CloseSession, SessionPhase.Open session, None ->
                closing session, [ SessionEffect.CallCloseSession session.OpenedToken ]
            | SessionMsg.CloseSession, _, _ -> state, []

            // closing the session clears the patient data. SessionClosed applies only to a close under
            // way, so a late answer never affects a newer session
            | SessionMsg.SessionClosed, _, Some SessionRequest.Closing ->
                anonymous, [ SessionEffect.SetPatientData None ]
            | SessionMsg.SessionClosed, _, _ -> state, []

            // a close that never reached the server closed nothing: the session stays open with its
            // patient, and the user is told. A late failure, after a newer session replaced the close,
            // falls to the last case and tells nothing
            | SessionMsg.CloseFailed _, SessionPhase.Open session, Some SessionRequest.Closing ->
                opened session None, [ SessionEffect.Alert Alert.Alert.CloseFailed ]
            | SessionMsg.CloseFailed _, _, _ -> state, []

            // the server ended the session at a signature: the gate says why and the close tells the
            // server; a close under way is left to complete
            | SessionMsg.SignatureEndedSession ending, SessionPhase.Open _, None ->
                ended ending, [ SessionEffect.CallCloseSession None ]
            | SessionMsg.SignatureEndedSession _, _, _ -> state, []

            // the session takes the new token and the signed version as its latest version; its patient
            // context takes the patient data and identity of the signed version, and the patient data
            // go to the panel. A session without a patient context cannot sign, so it has none to update
            | SessionMsg.SignatureRenewedToken(token, patient, signed), SessionPhase.Open session, None ->
                let renewed =
                    { session with
                        OpenedToken = Some token
                        Head = Some signed
                        PatientContext =
                            session.PatientContext
                            |> Option.map (fun c ->
                                { c with
                                    PatientData = Some patient
                                    PatientIdentity = signed.PatientIdentity
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

            // the version opened: its orders go into the cart and the patient machine takes only the
            // age. The answer applies only to the open session that still has the token sent with the
            // request; nothing to open or a failure leaves the session as it was
            | SessionMsg.SignedPlanOpened(from, Ok(Some session)), SessionPhase.Open current, None when
                current.OpenedToken = from
                ->
                match session.PatientContext, session.Head with
                // tell the user once; a newer version reported meanwhile stays on offer
                | Some _, Some head ->
                    opened session (NewerPlan.opened state.NewerPlan head.Head),
                    [
                        // load the version first: the plan machine waits with it for the patient data
                        SessionEffect.LoadSignedPlan head
                        SessionEffect.TakeAge(session.PatientContext |> Option.bind _.PatientData)
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

            // the refreshed patient data go to the patient machine; the plan and the workbench update
            // as after any patient data update, and the plan keeps its new and changed orders. Same
            // token check as SignedPlanOpened
            | SessionMsg.PatientRefreshed(from, Ok(Some session)), SessionPhase.Open current, None when
                current.OpenedToken = from
                ->
                opened session state.NewerPlan,
                [
                    SessionEffect.SetPatientDataRead(session.PatientContext |> Option.bind _.PatientData)
                ]
            // the user asked for the refresh, so a refresh that did not happen is told
            | SessionMsg.PatientRefreshed(from, (Ok None | Error _)), SessionPhase.Open current, None when
                current.OpenedToken = from
                ->
                { state with Reopening = None }, [ SessionEffect.TellPatientRefreshFailed ]
            | SessionMsg.PatientRefreshed _, _, _ -> { state with Reopening = None }, []

            // a notice applies only when its request was sent with the current token of the open
            // session; the next reply repeats a notice that still applies
            | SessionMsg.NoticeReceived(from, notice), SessionPhase.Open current, None when current.OpenedToken = from ->
                match notice with
                // store the newest version and tell the user once per version
                | RecordNotice.NewerVersion head ->
                    let kept, news = NewerPlan.receive state.NewerPlan head

                    { state with NewerPlan = kept },
                    (if news then
                         [ SessionEffect.TellNewerSignedPlan head ]
                     else
                         [])
                // a refresh or an open under way makes a new token itself, so an inactive-tab notice on
                // a reply to an older request does not mean another tab; the lanes do the same for a
                // signature
                | RecordNotice.Ended SessionEnding.InactiveTab when state.Reopening.IsSome -> state, []
                // another tab has the session: the gate says so, and the session is not closed
                | RecordNotice.Ended SessionEnding.InactiveTab -> ended SessionEnding.InactiveTab, []
                // the server ended the session: the gate says why and the close tells the server
                | RecordNotice.Ended ending -> ended ending, [ SessionEffect.CallCloseSession None ]
            | SessionMsg.NoticeReceived _, _, _ -> state, []

            // a close refused after the url brought something new still starts what follows;
            // elsewhere the gate says that another tab has the session
            | SessionMsg.InactiveTab _, SessionPhase.UrlMovedOn next, Some SessionRequest.Closing -> follow next
            | SessionMsg.InactiveTab from, SessionPhase.Open current, _ when current.OpenedToken = from ->
                ended SessionEnding.InactiveTab, []
            | SessionMsg.InactiveTab _, _, _ -> state, []

            // a signature refused for a newer version: store the version for the bar without telling
            // the user again, because the refusal already did
            | SessionMsg.SignatureBlocked head, SessionPhase.Open _, None ->
                { state with NewerPlan = NewerPlan.receive state.NewerPlan head |> fst }, []
            | SessionMsg.SignatureBlocked _, _, _ -> state, []
