/// The signing phase of an open Session (uc-03 steps 2 and 3): a pure state machine next to
/// the Session's, with effects for the App to interpret. The machine holds no OpenedToken and
/// no idempotency key: the effects name what it knows (the plan, the challenge, the PIN), and
/// the interpreter completes each call from the open Session.
///
/// Two invariants (ext 3b, 3c): what is submitted is the plan the challenge was issued over,
/// held in the state, never the live cart; and one request is in flight at a time.
module SigningMachine

open Shared.Types


[<RequireQualifiedAccess>]
type Signing =
    | Idle
    // step 2 asked; the notice token when the User accepted a data notice (Rule 44)
    | Requesting of OrderPlan * notice: string option
    // Rule 44: the data as it stands, to accept or to cancel
    | Noticed of OrderPlan * DataNotice
    // step 3: the dialog asks the PIN over this plan; the last refusal, if any
    | Challenged of challenge: string * OrderPlan * SigningRefusal option
    | Submitting of challenge: string * OrderPlan


[<RequireQualifiedAccess>]
type SigningMsg =
    | Sign of OrderPlan
    // Error = transport failure
    | ChallengeAnswered of Result<SigningResponse, string>
    // the data notice accepted: sign over the data as it stands
    | Accept
    | Confirm of pin: string
    | Cancel
    // Error = transport failure
    | SubmitAnswered of Result<SigningResponse, string>


[<RequireQualifiedAccess>]
type SigningEffect =
    // RequestSignChallenge, completed with the open Session's OpenedToken
    | CallChallenge of OrderPlan * notice: string option
    // Submit, completed with the open Session's OpenedToken and a fresh idempotency key
    | CallSubmit of OrderPlan * challenge: string * pin: string
    // the Session's token, re-minted over the new head (Rule 34)
    | RenewToken of OpenedToken
    // the server ended the Session (Rule 28)
    | EndSession of SessionEnding
    // the patient data as the notice showed it, so the cart is over it too
    | SetPatient of Patient
    // told once
    | TellSigned of SignedOrderPlan
    | TellRefused of SigningRefusal
    | TellError of reason: string


module Signing =

    let transition (msg: SigningMsg) (state: Signing) : Signing * SigningEffect list =
        match msg, state with
        | SigningMsg.Sign plan, Signing.Idle ->
            Signing.Requesting(plan, None), [ SigningEffect.CallChallenge(plan, None) ]
        // one thing at a time
        | SigningMsg.Sign _, _ -> state, []

        | SigningMsg.ChallengeAnswered(Ok(SigningResponse.ChallengeIssued challenge)), Signing.Requesting(plan, _) ->
            Signing.Challenged(challenge, plan, None), []
        | SigningMsg.ChallengeAnswered(Ok(SigningResponse.DataNotice notice)), Signing.Requesting(plan, _) ->
            Signing.Noticed(plan, notice), []
        | SigningMsg.ChallengeAnswered(Ok(SigningResponse.Refused SigningRefusal.PinLimit)), Signing.Requesting _ ->
            Signing.Idle, [ SigningEffect.EndSession SessionEnding.WrongPinLimit ]
        | SigningMsg.ChallengeAnswered(Ok(SigningResponse.Refused refusal)), Signing.Requesting _ ->
            Signing.Idle, [ SigningEffect.TellRefused refusal ]
        // never an answer to a challenge request
        | SigningMsg.ChallengeAnswered(Ok(SigningResponse.Submitted _)), Signing.Requesting _ -> Signing.Idle, []
        | SigningMsg.ChallengeAnswered(Error reason), Signing.Requesting _ ->
            Signing.Idle, [ SigningEffect.TellError reason ]
        // an answer lands only on the request in flight
        | SigningMsg.ChallengeAnswered _, _ -> state, []

        // Rule 44: the User signs over the data as it stands; with a reading, the cart follows it
        | SigningMsg.Accept, Signing.Noticed(plan, notice) ->
            match notice.Data with
            | Some data ->
                let shown = { plan with Patient = data }

                Signing.Requesting(shown, Some notice.Token),
                [
                    SigningEffect.SetPatient data
                    SigningEffect.CallChallenge(shown, Some notice.Token)
                ]
            | None ->
                Signing.Requesting(plan, Some notice.Token), [ SigningEffect.CallChallenge(plan, Some notice.Token) ]
        | SigningMsg.Accept, _ -> state, []

        // step 3: the plan submitted is the plan challenged (ext 3b, 3c)
        | SigningMsg.Confirm pin, Signing.Challenged(challenge, plan, _) ->
            Signing.Submitting(challenge, plan), [ SigningEffect.CallSubmit(plan, challenge, pin) ]
        | SigningMsg.Confirm _, _ -> state, []

        // the challenge is dropped, and the dialog with it; a request in flight cannot be cancelled
        | SigningMsg.Cancel, Signing.Requesting _
        | SigningMsg.Cancel, Signing.Noticed _
        | SigningMsg.Cancel, Signing.Challenged _ -> Signing.Idle, []
        | SigningMsg.Cancel, _ -> state, []

        | SigningMsg.SubmitAnswered(Ok(SigningResponse.Submitted(signed, token))), Signing.Submitting _ ->
            Signing.Idle,
            [
                SigningEffect.RenewToken token
                SigningEffect.TellSigned signed
            ]
        // the dialog stays open with what went wrong (Rule 28: tries left, or locked)
        | SigningMsg.SubmitAnswered(Ok(SigningResponse.Refused(SigningRefusal.PinWrong _ as refusal))),
          Signing.Submitting(challenge, plan)
        | SigningMsg.SubmitAnswered(Ok(SigningResponse.Refused(SigningRefusal.Locked _ as refusal))),
          Signing.Submitting(challenge, plan) -> Signing.Challenged(challenge, plan, Some refusal), []
        | SigningMsg.SubmitAnswered(Ok(SigningResponse.Refused SigningRefusal.PinLimit)), Signing.Submitting _ ->
            Signing.Idle, [ SigningEffect.EndSession SessionEnding.WrongPinLimit ]
        | SigningMsg.SubmitAnswered(Ok(SigningResponse.Refused refusal)), Signing.Submitting _ ->
            Signing.Idle, [ SigningEffect.TellRefused refusal ]
        // never an answer to a Submission
        | SigningMsg.SubmitAnswered(Ok(SigningResponse.ChallengeIssued _)), Signing.Submitting _
        | SigningMsg.SubmitAnswered(Ok(SigningResponse.DataNotice _)), Signing.Submitting _ -> Signing.Idle, []
        // the request never got there: the challenge stands, the dialog comes back as it was
        | SigningMsg.SubmitAnswered(Error reason), Signing.Submitting(challenge, plan) ->
            Signing.Challenged(challenge, plan, None), [ SigningEffect.TellError reason ]
        | SigningMsg.SubmitAnswered _, _ -> state, []
