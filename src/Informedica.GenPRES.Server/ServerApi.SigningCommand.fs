namespace ServerApi

open Informedica.GenOrder.Lib
// Opened last, so an unqualified case name refers to the contract case.
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api


/// Handles the signing of an order plan.
module SigningCommand =

    /// Maps a signing outcome to the response. The patient data gets the options of the resources.
    let toResponse (demo: bool) resources (outcome: SigningOutcome) =
        match outcome with
        | SigningOutcome.ChallengeIssued(nonce, moved) -> SigningResponse.ChallengeIssued(nonce, moved)
        | SigningOutcome.DataNotice(token, data) ->
            SigningResponse.DataNotice
                {
                    PatientData =
                        data
                        |> Option.map (ServerApi.Patient.ofPatientData >> ServerApi.Patient.withOptions resources)
                    Token = token
                }
        | SigningOutcome.Submitted(version, whom, token, patient) ->
            SigningResponse.Submitted(
                version |> SessionMapper.toSigned demo whom,
                token,
                patient
                |> ServerApi.Patient.ofPatientData
                |> ServerApi.Patient.withOptions resources
            )
        | SigningOutcome.Refused refusal -> SigningResponse.Refused refusal


    /// Runs a signing command for the session in the cookie. Refuses it without a session,
    /// with invalid patient data or with an invalid plan. The plan is not changed, because the
    /// challenge is a digest of it.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (cmd: SigningCommand) =
        async {
            match cookie.read () with
            | None -> return SigningResponse.Refused SigningRefusal.NoSession
            | Some id ->
                let plan =
                    match cmd with
                    | SigningCommand.RequestSignChallenge(plan, _, _) -> plan
                    | SigningCommand.Submit submission -> submission.Plan

                if
                    ServerApi.Patient.ofPlan plan
                    |> List.exists (ServerApi.Patient.patient >> _.IsError)
                then
                    return SigningResponse.Refused SigningRefusal.NoPatient
                else
                    match plan |> OrderPlanCommand.parsePlan (env.today ()) with
                    | Error _ -> return SigningResponse.Refused SigningRefusal.PlanUnreadable
                    | Ok parsed ->
                        match cmd with
                        | SigningCommand.RequestSignChallenge(_, opened, notice) ->
                            let! outcome = env.session.challenge id (parsed, opened, notice)
                            return toResponse env.demo (env.patientDataResources ()) outcome
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

                            return toResponse env.demo (env.patientDataResources ()) outcome
        }
