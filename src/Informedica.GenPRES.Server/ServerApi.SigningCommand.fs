namespace ServerApi


module SigningCommand =

    open Shared.Types
    open Shared.Api


    /// A signing command for the Session the cookie names. No cookie, no Session: refused
    /// before the port is asked. Writes no cookie.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (cmd: SigningCommand) =
        async {
            match cookie.read () with
            | None -> return SigningResponse.Refused SigningRefusal.NoSession
            | Some id ->
                match cmd with
                // the plan's patient and that of every context in it made at the ingress: a plan
                // whose data is no patient has nothing to sign for
                | SigningCommand.RequestSignChallenge(plan, _, _) when
                    Ingress.ofPlan plan |> List.exists (Ingress.patient >> _.IsError)
                    ->
                    return SigningResponse.Refused SigningRefusal.NoPatient
                | SigningCommand.RequestSignChallenge(plan, opened, notice) ->
                    return! env.session.challenge id (plan, opened, notice)
                | SigningCommand.Submit submission -> return! env.session.submit id submission
        }
