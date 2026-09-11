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
                // the new launch replaces whatever Session this browser still held, as an open
                // would (session-endings: ReplacedInBrowser owes nothing); left in place, its
                // cookie would hide the enrolment at the next GetSession
                match cookie.read () with
                | Some id -> do! env.session.close id
                | None -> ()

                cookie.delete ()
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
                    | SupplyPinResult.Refused(PinRefusal.AttemptExpired as refusal)
                    | SupplyPinResult.Refused(PinRefusal.WrongActivePatient as refusal) ->
                        enrolment.delete ()
                        return SessionResponse.PinRefused refusal
                    | SupplyPinResult.Refused refusal -> return SessionResponse.PinRefused refusal
            // UC-4 step 4: for the Session the cookie names; without a cookie there is nothing to
            // open. Writes no cookie.
            | SessionCommand.OpenVersion id ->
                match cookie.read () with
                | None -> return SessionResponse.SessionResp None
                | Some sid ->
                    let! opened = env.session.openVersion sid id
                    return SessionResponse.SessionResp opened
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


    /// A signing command for the Session the cookie names (uc-03 step 2). No cookie, no
    /// Session: refused before the port is asked. Writes no cookie.
    let processSigning (env: AppEnv) (cookie: SessionCookie) (cmd: SigningCommand) =
        async {
            match cookie.read () with
            | None -> return SigningResponse.Refused SigningRefusal.NoSession
            | Some id ->
                match cmd with
                | SigningCommand.RequestSignChallenge(plan, opened, notice) ->
                    return! env.session.challenge id (plan, opened, notice)
                | SigningCommand.Submit submission -> return! env.session.submit id submission
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
            // uc-03 step 1: the Session the cookie names is marked seen and told what it is
            // told (Rules 9, 11, 21) before the command is computed; without a cookie the
            // request computes as it always did (UC-7). The token is never logged.
            processCommand =
                fun request ->
                    async {
                        let cmd = request.Command

                        try
                            writeInfoMessage $"Processing command: {cmd |> Command.toString}"

                            let! notice =
                                match cookie.read () with
                                | None -> async { return None }
                                | Some id -> env.session.seen id request.Opened

                            let! result = Command.processCmd env cmd

                            let told =
                                match notice with
                                | Some(RecordNotice.NewerVersion _) -> ", the record moved on"
                                | Some(RecordNotice.Ended _) -> ", the Session ended"
                                | None -> ""

                            writeInfoMessage $"Finished processing command: {cmd |> Command.toString}{told}"

                            return
                                result
                                |> Result.map (fun response ->
                                    ({
                                        Response = response
                                        Notice = notice
                                    }
                                    : Reply)
                                )
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

            processSigning =
                fun cmd ->
                    async {
                        writeInfoMessage $"Processing signing: {cmd |> SigningCommand.toString}"
                        let! response = processSigning env cookie cmd
                        writeInfoMessage $"Finished processing signing: {cmd |> SigningCommand.toString}"
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
