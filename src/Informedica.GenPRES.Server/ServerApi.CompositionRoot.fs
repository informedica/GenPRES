namespace ServerApi


module CompositionRoot =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Shared.Types
    open Shared.Api


    /// Launch step 4 over the session port. The session id goes into the cookie and nowhere
    /// else (Rule 12); a refusal is a value. A server exception is not a refusal: it propagates,
    /// Fable.Remoting answers 500 and the client's transport-error path retries.
    let processLaunch (env: AppEnv) (cookie: SessionCookie) (stateCookie: LaunchStateCookie) (cmd: LaunchCommand) =
        async {
            match cmd with
            | LaunchCommand.PresentLaunch(launch, key) ->
                match! env.session.present (launch, key) with
                | LaunchResult.Opened(id, session) ->
                    cookie.write id
                    return LaunchOutcome.Opened session
                | LaunchResult.RedirectTo(url, state) ->
                    // step 4.2: the state cookie is what proves, at the callback, that the same
                    // browser started the hop
                    stateCookie.write state
                    return LaunchOutcome.RedirectTo url
                | LaunchResult.Refused refusal -> return LaunchOutcome.Refused refusal
        }


    /// Launch step 4.5 over the session port: the browser is back from the IdentityProvider.
    /// Answers where it goes next; the session cookie is set when a Session opened.
    let processCallback (env: AppEnv) (cookie: SessionCookie) (stateCookie: LaunchStateCookie) (cb: Callback) =
        async {
            match! env.session.callback { cb with StateCookie = stateCookie.read cb.State } with
            | CallbackResult.Opened(id, redirect) ->
                cookie.write id
                return redirect
            | CallbackResult.Refused(_, redirect)
            | CallbackResult.Superseded redirect -> return redirect
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
                    match! env.session.find id with
                    | SessionLookup.Found session -> return SessionResponse.SessionResp(Some session)
                    | SessionLookup.NotFound -> return SessionResponse.SessionResp None
                    | SessionLookup.Ended ending ->
                        // the cookie stays until the client acknowledges with CloseSession, which
                        // deletes it and drops the ending; a lost answer is asked and told again
                        return SessionResponse.SessionEnded ending
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
    let compose
        (settings: ServerSettings)
        (env: AppEnv)
        (cookie: SessionCookie)
        (stateCookie: LaunchStateCookie)
        : IServerApi
        =
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
                        let! outcome = processLaunch env cookie stateCookie cmd
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
