namespace ServerApi


module CompositionRoot =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Shared.Api
    open Shared.Types

    let launchSession (env: AppEnv) (token: SessionLaunchToken) : Async<Result<SessionContent, string>> =
        async {
            let (SessionLaunchToken token) = token

            if token = "demo-error" then
                return Error "error: Invalid launch token"
            else
                try
                    let dummySessionContent =
                        {
                            RedeemToken = "dummy-redeem-token"
                            UserName = "John Doe"
                            UserEmail = "john@doe.com"
                            PatientId = "dummy-patient-id"
                            SessionId = "dummy"
                        }

                    return Ok dummySessionContent
                with ex ->
                    writeErrorMessage $"Error launching session with token: {token}\n{ex}"
                    return Error ex.Message
        }

    let compose (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) : IServerApi =
        let env = Adapters.makeAppEnv provider

        {
            launchSession = launchSession env
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
