/// The signing phase of an open Session, from the challenge to the Submission: a pure state machine next to
/// the Session's, with effects for the App to interpret. The machine holds no OpenedToken: the
/// effects name what it knows (the plan, the challenge, the PIN, the request and the key), and
/// the interpreter completes each call from the open Session. The state is a lane: the signing
/// as the clinical model has it, which knows no request; the one request under way; and the key
/// of a Submission whose answer was lost. The dialog reads a view of it, with nothing of the
/// request.
///
/// Four invariants: what is submitted is the plan the challenge was issued over, held in the
/// state, never the live cart; one request is in flight at a time; a Submission whose answer
/// was lost is sent again under the same key, so the server
/// answers what it already did, landed or not; and an answer names the request it answers (the
/// request id for a challenge, the key for a Submission) and lands only on that request, so an
/// answer to a signature of an earlier Session never touches a later one.
module SigningMachine

open Shared.Types
open PlanWorkPolicy


/// The signing as the clinical model has it: no request here.
[<RequireQualifiedAccess>]
type SigningPhase =
    | Idle
    // the data as it stands, to accept or to cancel
    | Noticed of OrderPlan * DataNotice
    // the dialog asks the PIN over this plan, under the challenge the server issued; the last
    // refusal, if any
    | Challenged of challenge: string * OrderPlan * SigningRefusal option


/// The one request under way.
[<RequireQualifiedAccess>]
type SigningRequest =
    // the challenge asked over the plan under a request id; the notice token when the User
    // accepted a data notice
    | Challenge of OrderPlan * notice: string option * request: string
    // the Submission, under its key
    | Submission of key: string


/// The phase, the one request under way (none while idle, noticed or challenged), the key of a
/// Submission whose answer was lost, so that the next Confirm goes out under it and the server
/// answers what it already did, and the plan's work the signature was asked over, from the
/// Sign to the signature told, so that a change made meanwhile is told apart from what was
/// signed; none while idle. Built through the constructors below only, which admit the six
/// combinations that occur.
type SigningState =
    private
        {
            Phase: SigningPhase
            InFlight: SigningRequest option
            Unsent: string option
            AskedOver: PlanWork option
        }


/// The signing as the dialog shows it: the states the dialog can be in, each with what is valid
/// in it and nothing of the request. Closed; closed while the challenge is asked; the data as it
/// stands, to accept or to cancel; the PIN asked over the plan, with the last refusal if any; the
/// Submission under way, the plan listed and the field disabled. A lost answer shows as the PIN
/// asked again without a refusal: the kept key is the machine's, not the dialog's.
[<RequireQualifiedAccess>]
type SigningView =
    | Idle
    | Requesting
    | Noticed of OrderPlan * DataNotice
    | Challenged of OrderPlan * SigningRefusal option
    | Submitting of OrderPlan


[<RequireQualifiedAccess>]
type SigningMsg =
    // the plan as shown, the plan's work it stands at, and a request id the caller minted
    | Sign of OrderPlan * work: PlanWork * request: string
    // the answer to the challenge request with this id; Error = transport failure
    | ChallengeAnswered of request: string * Result<SigningResponse, string>
    // the data notice accepted: sign over the data as it stands
    | Accept
    // the PIN, and a fresh idempotency key the caller minted; ignored on a retry
    | Confirm of pin: string * key: string
    | Cancel
    // the answer to the Submission under this key; Error = transport failure
    | SubmitAnswered of key: string * Result<SigningResponse, string>


[<RequireQualifiedAccess>]
type SigningEffect =
    // RequestSignChallenge, completed with the open Session's OpenedToken; answered under the request id
    | CallChallenge of OrderPlan * notice: string option * request: string
    // Submit, completed with the open Session's OpenedToken; answered under the key
    | CallSubmit of OrderPlan * challenge: string * pin: string * key: string
    // the Session's token, re-minted over the new head
    | RenewToken of OpenedToken
    // the server ended the Session at the wrong-PIN limit
    | EndSession of SessionEnding
    // the patient data as the notice showed it, so the cart is over it too
    | SetPatient of Patient

    // told once; the signature carries back the work it was asked over, so that the plan's
    // work can tell what it signed from a change made meanwhile
    | TellSigned of SignedOrderPlan * askedOver: PlanWork
    | TellRefused of SigningRefusal
    | TellError of reason: string


module SigningState =

    let idle =
        {
            Phase = SigningPhase.Idle
            InFlight = None
            Unsent = None
            AskedOver = None
        }


    /// The challenge asked over the plan, under the request id; the dialog closed meanwhile.
    let requesting (plan: OrderPlan) (notice: string option) (request: string) (work: PlanWork) =
        {
            Phase = SigningPhase.Idle
            InFlight = Some(SigningRequest.Challenge(plan, notice, request))
            Unsent = None
            AskedOver = Some work
        }


    /// The data notice to accept or to cancel, nothing under way.
    let noticed (plan: OrderPlan) (notice: DataNotice) (work: PlanWork) =
        {
            Phase = SigningPhase.Noticed(plan, notice)
            InFlight = None
            Unsent = None
            AskedOver = Some work
        }


    /// The PIN asked over the plan under the challenge, with the last refusal, nothing under way.
    let challenged (challenge: string) (plan: OrderPlan) (refusal: SigningRefusal option) (work: PlanWork) =
        {
            Phase = SigningPhase.Challenged(challenge, plan, refusal)
            InFlight = None
            Unsent = None
            AskedOver = Some work
        }


    /// The Submission under way under its key, over the plan challenged.
    let submitting (challenge: string) (plan: OrderPlan) (key: string) (work: PlanWork) =
        {
            Phase = SigningPhase.Challenged(challenge, plan, None)
            InFlight = Some(SigningRequest.Submission key)
            Unsent = None
            AskedOver = Some work
        }


    /// The answer to the Submission was lost: the PIN is asked again over the plan challenged,
    /// and the key is kept for the retry.
    let unsent (challenge: string) (plan: OrderPlan) (key: string) (work: PlanWork) =
        {
            Phase = SigningPhase.Challenged(challenge, plan, None)
            InFlight = None
            Unsent = Some key
            AskedOver = Some work
        }


    /// The signing as the dialog shows it: the phase wins whenever it holds the payload, the
    /// request only while the challenge is asked or the Submission is under way.
    let view (state: SigningState) : SigningView =
        match state.Phase, state.InFlight with
        | SigningPhase.Idle, Some(SigningRequest.Challenge _) -> SigningView.Requesting
        | SigningPhase.Idle, _ -> SigningView.Idle
        | SigningPhase.Noticed(plan, notice), _ -> SigningView.Noticed(plan, notice)
        | SigningPhase.Challenged(_, plan, _), Some(SigningRequest.Submission _) -> SigningView.Submitting plan
        | SigningPhase.Challenged(_, plan, refusal), _ -> SigningView.Challenged(plan, refusal)


    /// The work the signature under way was asked over, carried into the next state; every arm
    /// that carries it matches a phase or a request only a constructor with the work builds.
    let private carried (state: SigningState) = state.AskedOver |> Option.defaultValue PlanWork.AsSigned


    /// Every arm names the phase and the request under way, and every new state is built through
    /// a constructor, so that no field outlives the state it belongs to.
    let transition (msg: SigningMsg) (state: SigningState) : SigningState * SigningEffect list =
        match msg, state.Phase, state.InFlight with
        | SigningMsg.Sign(plan, work, request), SigningPhase.Idle, None ->
            requesting plan None request work, [ SigningEffect.CallChallenge(plan, None, request) ]
        // one thing at a time
        | SigningMsg.Sign _, _, _ -> state, []

        // an answer lands only on the request it answers
        | SigningMsg.ChallengeAnswered(answered, _), _, Some(SigningRequest.Challenge(_, _, request)) when
            answered <> request
            ->
            state, []
        | SigningMsg.ChallengeAnswered(_, Ok(SigningResponse.ChallengeIssued challenge)),
          SigningPhase.Idle,
          Some(SigningRequest.Challenge(plan, _, _)) -> challenged challenge plan None (carried state), []
        | SigningMsg.ChallengeAnswered(_, Ok(SigningResponse.DataNotice notice)),
          SigningPhase.Idle,
          Some(SigningRequest.Challenge(plan, _, _)) -> noticed plan notice (carried state), []
        | SigningMsg.ChallengeAnswered(_, Ok(SigningResponse.Refused SigningRefusal.PinLimit)),
          SigningPhase.Idle,
          Some(SigningRequest.Challenge _) -> idle, [ SigningEffect.EndSession SessionEnding.WrongPinLimit ]
        | SigningMsg.ChallengeAnswered(_, Ok(SigningResponse.Refused refusal)),
          SigningPhase.Idle,
          Some(SigningRequest.Challenge _) -> idle, [ SigningEffect.TellRefused refusal ]
        // never an answer to a challenge request
        | SigningMsg.ChallengeAnswered(_, Ok(SigningResponse.Submitted _)),
          SigningPhase.Idle,
          Some(SigningRequest.Challenge _) -> idle, []
        | SigningMsg.ChallengeAnswered(_, Error reason), SigningPhase.Idle, Some(SigningRequest.Challenge _) ->
            idle, [ SigningEffect.TellError reason ]
        | SigningMsg.ChallengeAnswered _, _, _ -> state, []

        // the User signs over the data as it stands; with a reading, the cart follows it. The
        // notice token is the request id: one notice, one acceptance. The dialog closes while the
        // challenge is asked again
        | SigningMsg.Accept, SigningPhase.Noticed(plan, notice), None ->
            match notice.Data with
            | Some data ->
                let shown = { plan with Patient = data }

                requesting shown (Some notice.Token) notice.Token (carried state),
                [
                    SigningEffect.SetPatient data
                    SigningEffect.CallChallenge(shown, Some notice.Token, notice.Token)
                ]
            | None ->
                requesting plan (Some notice.Token) notice.Token (carried state),
                [ SigningEffect.CallChallenge(plan, Some notice.Token, notice.Token) ]
        | SigningMsg.Accept, _, _ -> state, []

        // the plan submitted is the plan challenged, never the live cart, under the caller's key;
        // a retry after a lost answer goes out under the key it had, so that the server answers
        // what it already did, whether the signature landed or not
        | SigningMsg.Confirm(pin, key), SigningPhase.Challenged(challenge, plan, _), None ->
            let key = state.Unsent |> Option.defaultValue key
            submitting challenge plan key (carried state), [ SigningEffect.CallSubmit(plan, challenge, pin, key) ]
        | SigningMsg.Confirm _, _, _ -> state, []

        // a request in flight cannot be cancelled; everything else is dropped, and the dialog with
        // it, a challenge asked included: its answer then finds no request and lands nowhere
        | SigningMsg.Cancel, _, Some(SigningRequest.Submission _) -> state, []
        | SigningMsg.Cancel, _, _ -> idle, []

        // an answer lands only on the Submission it answers
        | SigningMsg.SubmitAnswered(answered, _), _, Some(SigningRequest.Submission key) when answered <> key ->
            state, []
        | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.Submitted(signed, token))),
          SigningPhase.Challenged _,
          Some(SigningRequest.Submission _) ->
            idle,
            [
                SigningEffect.RenewToken token
                SigningEffect.TellSigned(signed, carried state)
            ]
        // the dialog stays open with what went wrong (tries left, or locked)
        | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.Refused(SigningRefusal.PinWrong _ as refusal))),
          SigningPhase.Challenged(challenge, plan, _),
          Some(SigningRequest.Submission _)
        | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.Refused(SigningRefusal.Locked _ as refusal))),
          SigningPhase.Challenged(challenge, plan, _),
          Some(SigningRequest.Submission _) -> challenged challenge plan (Some refusal) (carried state), []
        | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.Refused SigningRefusal.PinLimit)),
          SigningPhase.Challenged _,
          Some(SigningRequest.Submission _) -> idle, [ SigningEffect.EndSession SessionEnding.WrongPinLimit ]
        | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.Refused refusal)),
          SigningPhase.Challenged _,
          Some(SigningRequest.Submission _) -> idle, [ SigningEffect.TellRefused refusal ]
        // never an answer to a Submission
        | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.ChallengeIssued _)),
          SigningPhase.Challenged _,
          Some(SigningRequest.Submission _)
        | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.DataNotice _)),
          SigningPhase.Challenged _,
          Some(SigningRequest.Submission _) -> idle, []
        // the answer was lost: whether the signature landed is unknown, so the dialog comes back
        // without a refusal and the next Confirm retries under the same key
        | SigningMsg.SubmitAnswered(_, Error reason),
          SigningPhase.Challenged(challenge, plan, _),
          Some(SigningRequest.Submission key) ->
            unsent challenge plan key (carried state), [ SigningEffect.TellError reason ]
        | SigningMsg.SubmitAnswered _, _, _ -> state, []
