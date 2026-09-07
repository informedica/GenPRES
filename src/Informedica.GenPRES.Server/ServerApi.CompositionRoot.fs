namespace ServerApi


module CompositionRoot =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Shared.Api
    open Shared.Types

    let launchSession (env: AppEnv) (token: SessionLaunchToken) : Async<Result<SessionRedeemToken, string>> =
        // TODO: implement proper session launch logic,
        // which checks the validity of the token and returns a redeem token if valid, or an error if invalid.
        let (SessionLaunchToken token) = token
        async { return Ok(SessionRedeemToken "test-redeem-token") }

    let redeemSession (env: AppEnv) (token: SessionRedeemToken) : Async<Result<SessionContent, string>> =
        async {
            let (SessionRedeemToken token) = token

            if token = "demo-error" then
                return Error "error: invalid launch token"
            else
                try
                    let patientId = "test-patient-id"

                    match! env.patient.loadPatientData (PatientId patientId) with
                    | Error error ->
                        let errorMessage =
                            $"Error loading patient data for patientId: {patientId}. Error: {error}"

                        return Error errorMessage
                    | Ok patient ->
                        let testSessionContent =
                            {
                                RedeemToken = "test-redeem-token"
                                UserName = "John Doe"
                                UserEmail = "john@doe.com"
                                PatientId = "test-patient-id"
                                SessionId = "test-session-id"
                                Patient = patient
                            }

                        return Ok testSessionContent
                with ex ->
                    writeErrorMessage $"Error launching session with token: {token}\n{ex}"
                    return Error ex.Message
        }


    let compose (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) : IServerApi =
        let env = Adapters.makeAppEnv provider

        {
            launchSession = launchSession env
            redeemSession = redeemSession env
            processCommand =
                fun cmd ->
                    async {
                        try
                            writeInfoMessage $"Processing command: {cmd |> Command.toString}"
                            let! result = Command.processCmd env cmd
                            writeInfoMessage $"Finished processing command: {cmd |> Command.toString}"
                            return result
                        with ex ->
                            writeErrorMessage $"Error processing command: {cmd |> Command.toString}\n{ex}"
                            return Error [| ex.Message |]
                    }

            testApi = fun () -> async { return "Hello world!" }
        }
