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
                | LaunchResult.Opened(id, opened) ->
                    cookie.write id
                    return LaunchOutcome.Opened opened
                | LaunchResult.RedirectTo(url, state) ->
                    stateCookie.write state
                    return LaunchOutcome.RedirectTo url
                | LaunchResult.Refused refusal -> return LaunchOutcome.Refused refusal
                // a retry of a launch that suspended: the browser holds the attempt already, the
                // app tells it at the next GetSession
                | LaunchResult.Enrolling _ -> return LaunchOutcome.RedirectTo Hop.openedUrl
        }


    /// Launch step 4.5 over the session port: the browser is back from the IdentityProvider.
    /// Answers where it goes next; the session cookie is set when a Session opened, the
    /// enrolment cookie when the launch suspended (UC-2).
    let processCallback
        (env: AppEnv)
        (cookie: SessionCookie)
        (stateCookie: LaunchStateCookie)
        (enrolment: EnrolmentCookie)
        (cb: Callback)
        =
        async {
            match! env.session.callback { cb with StateCookie = stateCookie.read cb.State } with
            | CallbackResult.Opened(id, redirect) ->
                cookie.write id
                return redirect
            | CallbackResult.Enrolling(attempt, redirect, until) ->
                enrolment.write attempt until
                return redirect
            | CallbackResult.Refused(_, redirect)
            | CallbackResult.Superseded redirect -> return redirect
        }


    /// Cookie-authenticated session commands over both cookies. GetSession answers the Session
    /// first; without one, a standing attempt (UC-2); a gone attempt loses its cookie. SupplyPin
    /// works on the attempt in the cookie, never on one the client names. CloseSession drops
    /// both, even when nothing is found and even when the server-side close throws: an explicit
    /// close (Rule 10) always leaves the browser without a credential. The exception still
    /// propagates after the delete, so the failure stays visible.
    let processSession (env: AppEnv) (cookie: SessionCookie) (enrolment: EnrolmentCookie) (cmd: SessionCommand) =
        async {
            match cmd with
            | SessionCommand.GetSession ->
                let! bySession =
                    async {
                        match cookie.read () with
                        | None -> return None
                        | Some id ->
                            match! env.session.find id with
                            | SessionLookup.Found opened -> return Some(SessionResponse.SessionResp(Some opened))
                            | SessionLookup.NotFound -> return None
                            | SessionLookup.Ended ending -> return Some(SessionResponse.SessionEnded ending)
                    }

                match bySession, enrolment.read () with
                | Some response, _ -> return response
                | None, None -> return SessionResponse.SessionResp None
                | None, Some attempt ->
                    match! env.session.findEnrolment attempt with
                    | Some pending -> return SessionResponse.EnrolmentPending pending
                    | None ->
                        enrolment.delete ()
                        return SessionResponse.SessionResp None
            | SessionCommand.SupplyPin(code, pin) ->
                match enrolment.read () with
                | None -> return SessionResponse.PinRefused PinRefusal.AttemptExpired
                | Some attempt ->
                    match! env.session.supplyPin attempt code pin with
                    | SupplyPinResult.Opened(id, opened) ->
                        enrolment.delete ()
                        cookie.write id
                        return SessionResponse.SessionResp(Some opened)
                    | SupplyPinResult.Refused(PinRefusal.CodeVoid as refusal)
                    | SupplyPinResult.Refused(PinRefusal.AttemptExpired as refusal) ->
                        enrolment.delete ()
                        return SessionResponse.PinRefused refusal
                    | SupplyPinResult.Refused refusal -> return SessionResponse.PinRefused refusal
            | SessionCommand.CloseSession ->
                try
                    match cookie.read () with
                    | Some id -> do! env.session.close id
                    | None -> ()

                    match enrolment.read () with
                    | Some attempt -> do! env.session.dropEnrolment attempt
                    | None -> ()
                finally
                    cookie.delete ()
                    enrolment.delete ()

                return SessionResponse.SessionClosed
        }


    /// The api of one request: the settings and the env are built once per host, the cookie
    /// once per request.
    let compose
        (settings: ServerSettings)
        (env: AppEnv)
        (cookie: SessionCookie)
        (stateCookie: LaunchStateCookie)
        (enrolment: EnrolmentCookie)
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
                        let! response = processSession env cookie enrolment cmd
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
