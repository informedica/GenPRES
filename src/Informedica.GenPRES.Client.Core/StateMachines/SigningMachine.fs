namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the signing of the order plan in an open session, from the challenge to the
/// submission. The App carries out the effects and adds the session's token to each call.
///
/// Four invariants:
/// - the plan submitted is the plan the challenge was issued over, never the live cart;
/// - one request is in flight at a time;
/// - a submission whose answer was lost is sent again under the same key, so the server answers
///   what it already did;
/// - an answer lands only on the request it names, so an answer from an earlier session never
///   touches a later one.
module SigningMachine =

    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared.Types


    /// The signing itself, without any request under way.
    [<RequireQualifiedAccess>]
    type SigningPhase =
        /// Not signing.
        | Idle
        /// The patient data changed; the user accepts it or cancels.
        | DataChanged of OrderPlan * DataNotice
        /// The dialog asks for the PIN over this plan, under the server's challenge, with the last
        /// refusal if any.
        | AskingPin of challenge: string * OrderPlan * SigningRefusal option


    /// The one request under way.
    [<RequireQualifiedAccess>]
    type SigningRequest =
        /// The challenge asked over the plan, under a request id, with the notice token when the user
        /// accepted a data notice.
        | Challenge of OrderPlan * notice: string option * request: string
        /// The submission, under its key.
        | Submission of key: string
        /// The submission, under its key, after the Session ended meanwhile: its answer ends the
        /// signature with the outcome told.
        | SubmissionAfterSessionEnd of key: string


    /// Everything the signing machine holds, hidden from the dialog, which reads a SigningView of it
    /// instead.
    type SigningState =
        private
            {
                /// The signing itself.
                Phase: SigningPhase
                /// The one request under way, if any.
                InFlight: SigningRequest option
                /// The key of a submission whose answer was lost; the next Confirm goes out under it.
                Unsent: string option
                /// The orders that differ from the version last opened or signed, as they stood when
                /// the user signed; the dialog lists these, whatever reaches the order plan meanwhile.
                Differences: (OrderContext * HeldContextPolicy.Difference)[]
            }


    /// What the dialog reads of the SigningState: the signing phase combined with the request under
    /// way. A lost answer shows as the PIN asked again without a refusal.
    [<RequireQualifiedAccess>]
    type SigningView =
        /// The dialog is closed.
        | Idle
        /// The dialog is closed while the challenge is asked.
        | RequestingChallenge
        /// The patient data changed; the user accepts it or cancels.
        | DataChanged of OrderPlan * DataNotice
        /// The PIN is asked over the plan, with the last refusal if any.
        | AskingPin of OrderPlan * SigningRefusal option
        /// The submission is under way; the plan is listed and the PIN field disabled.
        | Submitting of OrderPlan


    /// What moves the signing machine.
    [<RequireQualifiedAccess>]
    type SigningMsg =
        /// Sign the plan as shown, under a request id the caller made, with the orders that differ
        /// from the version last opened or signed as they stand now.
        | Sign of OrderPlan * differences: (OrderContext * HeldContextPolicy.Difference)[] * request: string
        /// The answer to the challenge request with this id; Error is a transport failure.
        | ChallengeAnswered of request: string * Result<SigningResponse, string>
        /// The data notice is accepted. The plan is signed over the new data, unless the patient
        /// context is held; then it keeps the data its new and changed orders were composed on.
        | AcceptDataChange of held: bool
        /// The PIN, and a new idempotency key the caller made; the key is ignored on a retry.
        | ConfirmPin of pin: string * key: string
        /// The user cancels.
        | Cancel
        /// The answer to the submission under this key; Error is a transport failure.
        | SubmitAnswered of key: string * Result<SigningResponse, string>
        /// The Session is no longer open: the signature ends, unless a submission is out, whose
        /// outcome is still told when its answer lands.
        | SessionEnded


    /// What the App carries out for the signing machine.
    [<RequireQualifiedAccess>]
    type SigningEffect =
        /// Ask the server for a challenge with the session's token; the answer names the request id.
        | CallChallenge of OrderPlan * notice: string option * request: string
        /// Submit the signature with the session's token; the answer names the key.
        | CallSubmit of OrderPlan * challenge: string * pin: string * key: string
        /// Renew the session after the signature: the new token, the patient as signed, and the
        /// version signed, with the identity it names, which the EHR may have changed in the data the
        /// user accepted.
        | RenewSessionToken of OpenedToken * Patient * SignedOrderPlan
        /// End the session: the server ended it at the wrong-PIN limit.
        | EndSession of SessionEnding
        /// Tell the user the plan is signed; the plan took no change while the signature was under
        /// way, so it is the version signed.
        | TellSigned of SignedOrderPlan
        /// Tell the user why the server refused.
        | TellRefused of SigningRefusal
        /// Tell the user a request failed.
        | TellError of reason: string


    /// The constructors and the transition of the signing machine.
    module SigningState =

        /// Not signing, nothing under way.
        let idle =
            {
                Phase = SigningPhase.Idle
                InFlight = None
                Unsent = None
                Differences = [||]
            }


        /// The challenge asked over the plan; the dialog stays closed meanwhile.
        let requesting (plan: OrderPlan) (notice: string option) (request: string) =
            {
                Phase = SigningPhase.Idle
                InFlight = Some(SigningRequest.Challenge(plan, notice, request))
                Unsent = None
                Differences = [||]
            }


        /// The data notice to accept or cancel.
        let noticed (plan: OrderPlan) (notice: DataNotice) =
            {
                Phase = SigningPhase.DataChanged(plan, notice)
                InFlight = None
                Unsent = None
                Differences = [||]
            }


        /// The PIN asked over the plan, with the last refusal if any.
        let challenged (challenge: string) (plan: OrderPlan) (refusal: SigningRefusal option) =
            {
                Phase = SigningPhase.AskingPin(challenge, plan, refusal)
                InFlight = None
                Unsent = None
                Differences = [||]
            }


        /// The submission under way, under its key.
        let submitting (challenge: string) (plan: OrderPlan) (key: string) =
            {
                Phase = SigningPhase.AskingPin(challenge, plan, None)
                InFlight = Some(SigningRequest.Submission key)
                Unsent = None
                Differences = [||]
            }


        /// The submission under way after the Session ended, under its key.
        let submittingAfterSessionEnd (challenge: string) (plan: OrderPlan) (key: string) =
            { submitting challenge plan key with InFlight = Some(SigningRequest.SubmissionAfterSessionEnd key) }


        /// The submission's answer was lost: the PIN is asked again, and the key is kept for the
        /// retry.
        let unsent (challenge: string) (plan: OrderPlan) (key: string) =
            {
                Phase = SigningPhase.AskingPin(challenge, plan, None)
                InFlight = None
                Unsent = Some key
                Differences = [||]
            }


        /// What the dialog reads: the phase, and the request only while a challenge is asked or a
        /// submission is under way.
        let view (state: SigningState) : SigningView =
            match state.Phase, state.InFlight with
            | SigningPhase.Idle, Some(SigningRequest.Challenge _) -> SigningView.RequestingChallenge
            | SigningPhase.Idle, _ -> SigningView.Idle
            | SigningPhase.DataChanged(plan, notice), _ -> SigningView.DataChanged(plan, notice)
            | SigningPhase.AskingPin(_, plan, _), Some(SigningRequest.Submission _)
            | SigningPhase.AskingPin(_, plan, _), Some(SigningRequest.SubmissionAfterSessionEnd _) ->
                SigningView.Submitting plan
            | SigningPhase.AskingPin(_, plan, refusal), _ -> SigningView.AskingPin(plan, refusal)


        /// The data notice accepted: the challenge is asked again, under the notice token as request
        /// id. With new data and the patient context not held, the plan takes the new data. When held,
        /// the plan keeps the data its new and changed orders were composed on. Either way the new
        /// data reaches the patient with the signed answer. Without new data the plan stays.
        let accepted (held: bool) (plan: OrderPlan) (notice: DataNotice) =
            let plan =
                match notice.Data with
                | Some data when not held -> { plan with Patient = data }
                | _ -> plan

            requesting plan (Some notice.Token) notice.Token,
            [ SigningEffect.CallChallenge(plan, Some notice.Token, notice.Token) ]


        /// What is told of a submission's answer that lands after the Session ended: its outcome only,
        /// since there is no Session to renew or end. A challenge or a data notice is never an answer
        /// to a submission, so neither tells anything.
        let outcome (result: Result<SigningResponse, string>) =
            match result with
            | Ok(SigningResponse.Submitted(signed, _, _)) -> [ SigningEffect.TellSigned signed ]
            | Ok(SigningResponse.Refused refusal) -> [ SigningEffect.TellRefused refusal ]
            | Ok(SigningResponse.ChallengeIssued _)
            | Ok(SigningResponse.DataNotice _) -> []
            | Error reason -> [ SigningEffect.TellError reason ]


        /// The next state and effects for a message. Every new state is built through a constructor,
        /// so no field outlives the state it belongs to.
        let step (msg: SigningMsg) (state: SigningState) : SigningState * SigningEffect list =
            match msg, state.Phase, state.InFlight with
            | SigningMsg.Sign(plan, _, request), SigningPhase.Idle, None ->
                requesting plan None request, [ SigningEffect.CallChallenge(plan, None, request) ]
            // one thing at a time
            | SigningMsg.Sign _, _, _ -> state, []

            // an answer lands only on the request it answers
            | SigningMsg.ChallengeAnswered(answered, _), _, Some(SigningRequest.Challenge(_, _, request)) when
                answered <> request
                ->
                state, []
            | SigningMsg.ChallengeAnswered(_, Ok(SigningResponse.ChallengeIssued challenge)),
              SigningPhase.Idle,
              Some(SigningRequest.Challenge(plan, _, _)) -> challenged challenge plan None, []
            | SigningMsg.ChallengeAnswered(_, Ok(SigningResponse.DataNotice notice)),
              SigningPhase.Idle,
              Some(SigningRequest.Challenge(plan, _, _)) -> noticed plan notice, []
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

            | SigningMsg.AcceptDataChange held, SigningPhase.DataChanged(plan, notice), None ->
                accepted held plan notice
            | SigningMsg.AcceptDataChange _, _, _ -> state, []

            // the plan submitted is the plan challenged, never the live cart; a retry after a lost
            // answer keeps its key, so the server answers what it already did
            | SigningMsg.ConfirmPin(pin, key), SigningPhase.AskingPin(challenge, plan, _), None ->
                let key = state.Unsent |> Option.defaultValue key
                submitting challenge plan key, [ SigningEffect.CallSubmit(plan, challenge, pin, key) ]
            | SigningMsg.ConfirmPin _, _, _ -> state, []

            // a submission in flight cannot be cancelled; anything else is dropped with the dialog, and
            // the answer to a challenge asked then lands nowhere
            | SigningMsg.Cancel, _, Some(SigningRequest.Submission _)
            | SigningMsg.Cancel, _, Some(SigningRequest.SubmissionAfterSessionEnd _) -> state, []
            | SigningMsg.Cancel, _, _ -> idle, []

            // after the Session ended, the answer to the submission ends the signature with its
            // outcome told
            | SigningMsg.SubmitAnswered(answered, result), _, Some(SigningRequest.SubmissionAfterSessionEnd key) when
                answered = key
                ->
                idle, outcome result
            // an answer lands only on the submission it names
            | SigningMsg.SubmitAnswered(answered, _), _, Some(SigningRequest.Submission key) when answered <> key ->
                state, []
            | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.Submitted(signed, token, patient))),
              SigningPhase.AskingPin _,
              Some(SigningRequest.Submission _) ->
                idle,
                [
                    SigningEffect.RenewSessionToken(token, patient, signed)
                    SigningEffect.TellSigned signed
                ]
            // the dialog stays open and says what went wrong: tries left, or locked
            | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.Refused(SigningRefusal.PinWrong _ as refusal))),
              SigningPhase.AskingPin(challenge, plan, _),
              Some(SigningRequest.Submission _)
            | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.Refused(SigningRefusal.Locked _ as refusal))),
              SigningPhase.AskingPin(challenge, plan, _),
              Some(SigningRequest.Submission _) -> challenged challenge plan (Some refusal), []
            | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.Refused SigningRefusal.PinLimit)),
              SigningPhase.AskingPin _,
              Some(SigningRequest.Submission _) -> idle, [ SigningEffect.EndSession SessionEnding.WrongPinLimit ]
            | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.Refused refusal)),
              SigningPhase.AskingPin _,
              Some(SigningRequest.Submission _) -> idle, [ SigningEffect.TellRefused refusal ]
            // never an answer to a submission
            | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.ChallengeIssued _)),
              SigningPhase.AskingPin _,
              Some(SigningRequest.Submission _)
            | SigningMsg.SubmitAnswered(_, Ok(SigningResponse.DataNotice _)),
              SigningPhase.AskingPin _,
              Some(SigningRequest.Submission _) -> idle, []
            // the answer was lost and the outcome is unknown: the dialog comes back without a refusal,
            // and the next Confirm retries under the same key
            | SigningMsg.SubmitAnswered(_, Error reason),
              SigningPhase.AskingPin(challenge, plan, _),
              Some(SigningRequest.Submission key) -> unsent challenge plan key, [ SigningEffect.TellError reason ]
            | SigningMsg.SubmitAnswered _, _, _ -> state, []

            // the submission out keeps waiting for its answer, which is all that is left of the
            // signature; anything else ends with the Session, and an answer then lands nowhere
            | SigningMsg.SessionEnded, SigningPhase.AskingPin(challenge, plan, _), Some(SigningRequest.Submission key) ->
                submittingAfterSessionEnd challenge plan key, []
            | SigningMsg.SessionEnded, _, Some(SigningRequest.SubmissionAfterSessionEnd _) -> state, []
            | SigningMsg.SessionEnded, _, _ -> idle, []


        /// The next state and effects for a message, the differences carried along: taken from the
        /// sign that starts a signature, kept while it is under way, and dropped when it ends.
        let transition (msg: SigningMsg) (state: SigningState) : SigningState * SigningEffect list =
            let next, effects = step msg state

            let differences =
                match msg, state.Phase, state.InFlight, next.Phase, next.InFlight with
                | SigningMsg.Sign(_, differences, _), SigningPhase.Idle, None, _, _ -> differences
                | _, _, _, SigningPhase.Idle, None -> [||]
                | _ -> state.Differences

            { next with Differences = differences }, effects


        /// The orders that differ from the version last opened or signed, as they stood when the user
        /// signed; empty when no signature is under way.
        let differences (state: SigningState) = state.Differences
