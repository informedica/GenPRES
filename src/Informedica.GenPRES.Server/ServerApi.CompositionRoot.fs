namespace ServerApi


module CompositionRoot =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Shared.Types
    open Shared.Api


    /// Launch step 4 over the session port. The session id goes into the cookie and nowhere
    /// else (Rule 12); a refusal is a value. A server exception is not a refusal: it propagates,
    /// Fable.Remoting answers 500 and the client's transport-error path retries.
    let processLaunch (env: AppEnv) (cookie: SessionCookie) (cmd: LaunchCommand) =
        async {
            match cmd with
            | LaunchCommand.PresentLaunch(launch, key) ->
                match! env.session.present (launch, key) with
                | LaunchResult.Opened(id, session) ->
                    cookie.write id
                    return LaunchOutcome.Opened session
                | LaunchResult.RedirectTo url -> return LaunchOutcome.RedirectTo url
                | LaunchResult.Refused refusal -> return LaunchOutcome.Refused refusal
        }


    /// Cookie-authenticated session commands. CloseSession deletes the cookie even when no
    /// session is found, and even when the server-side close throws: an explicit close
    /// (Rule 10) always leaves the browser without a credential. The exception still
    /// propagates after the delete, so the failure stays visible.
    let processSession (env: AppEnv) (cookie: SessionCookie) (cmd: SessionCommand) =
        async {
            match cmd with
            | SessionCommand.GetSession ->
                match cookie.read () with
                | None -> return SessionResponse.SessionResp None
                | Some id ->
                    let! session = env.session.find id
                    return SessionResponse.SessionResp session
            | SessionCommand.CloseSession ->
                try
                    match cookie.read () with
                    | Some id -> do! env.session.close id
                    | None -> ()
                finally
                    cookie.delete ()

                return SessionResponse.SessionClosed
        }


    /// The api of one request: the settings and the env are built once per host, the cookie
    /// once per request.
    let compose (settings: ServerSettings) (env: AppEnv) (cookie: SessionCookie) : IServerApi =
        {
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

            processLaunch =
                fun cmd ->
                    async {
                        writeInfoMessage $"Processing launch: {cmd |> LaunchCommand.toString}"
                        let! outcome = processLaunch env cookie cmd
                        writeInfoMessage $"Finished processing launch: {cmd |> LaunchCommand.toString}"
                        return outcome
                    }

            processSession =
                fun cmd ->
                    async {
                        writeInfoMessage $"Processing session: {cmd |> SessionCommand.toString}"
                        let! response = processSession env cookie cmd
                        writeInfoMessage $"Finished processing session: {cmd |> SessionCommand.toString}"
                        return response
                    }

            getSettings =
                fun () ->
                    async {
                        writeInfoMessage "Processing settings"
                        return settings
                    }

            testApi = fun () -> async { return "Hello world!" }
        }
