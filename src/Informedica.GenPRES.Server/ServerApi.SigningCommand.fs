namespace ServerApi

open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api


/// The signing member: the plan of a challenge and of a submission parsed at the inbound
/// boundary, the session port asked on domain values, its outcome mapped out.
module SigningCommand =

    /// The outcome as the wire carries it, the version and the notice's data mapped out, the
    /// version with the environment's demo flag.
    let toResponse (demo: bool) (outcome: SigningOutcome) =
        match outcome with
        | SigningOutcome.ChallengeIssued nonce -> SigningResponse.ChallengeIssued nonce
        | SigningOutcome.DataNotice(token, data) ->
            SigningResponse.DataNotice
                {
                    Data =
                        data
                        |> Option.map (Informedica.GenForm.Lib.Patient.Dto.toDto >> ServerApi.Patient.toModel)
                    Token = token
                }
        | SigningOutcome.Submitted(version, whom, token, patient) ->
            SigningResponse.Submitted(
                version |> OrderPlanVersion.Dto.toDto |> SessionMapper.toSigned demo whom,
                token,
                patient
                |> Informedica.GenForm.Lib.Patient.Dto.toDto
                |> ServerApi.Patient.toModel
            )
        | SigningOutcome.Refused refusal -> SigningResponse.Refused refusal


    /// A signing command for the Session the cookie names. No cookie, no Session: refused
    /// before the port is asked. The plan is signed as sent: the challenge is a digest of it, so
    /// no patient in it is changed. Its patient and that of every context in it made at the
    /// inbound boundary, then the plan parsed into the domain; a plan the domain does not read
    /// is refused before the port is asked. The Session is asked its age first, which marks it
    /// seen; a store that fails then answers StoreFailed. Writes no cookie.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (cmd: SigningCommand) =
        async {
            match cookie.read () with
            | None -> return SigningResponse.Refused SigningRefusal.NoSession
            | Some id ->
                let! answer = env.session.age id
                let plan =
                    match cmd with
                    | SigningCommand.RequestSignChallenge(plan, _, _) -> plan
                    | SigningCommand.Submit submission -> submission.Plan

                match answer with
                | Error refusal -> return SigningResponse.Refused refusal
                | Ok _ when
                    ServerApi.Patient.ofPlan plan
                    |> List.exists (ServerApi.Patient.patient >> _.IsError)
                    ->
                    return SigningResponse.Refused SigningRefusal.NoPatient
                | Ok _ ->
                    match plan |> OrderPlanCommand.parsePlan with
                    | Error _ -> return SigningResponse.Refused SigningRefusal.PlanUnreadable
                    | Ok parsed ->
                        match cmd with
                        | SigningCommand.RequestSignChallenge(_, opened, notice) ->
                            let! outcome = env.session.challenge id (parsed, opened, notice)
                            return toResponse env.demo outcome
                        | SigningCommand.Submit submission ->
                            let! outcome =
                                env.session.submit
                                    id
                                    {
                                        Plan = parsed
                                        Opened = submission.Opened
                                        Challenge = submission.Challenge
                                        Pin = submission.Pin
                                        IdemKey = submission.IdemKey
                                    }

                            return toResponse env.demo outcome
        }
