namespace ServerApi


module LaunchCommand =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api


    /// Presents a launch and writes the session id only to the cookie. A server exception is
    /// not caught, so the client gets a 500 and retries.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (stateCookie: LaunchStateCookie) (cmd: LaunchCommand) =
        async {
            match cmd with
            | LaunchCommand.PresentLaunch(launch, key) ->
                match! env.session.present (launch, key) with
                | LaunchResult.Opened(id, opened) ->
                    cookie.write id
                    return LaunchOutcome.Opened(SessionMapper.toOpened env.demo env.session.patientData opened)
                | LaunchResult.RedirectTo(url, state) ->
                    stateCookie.write state
                    return LaunchOutcome.RedirectTo url
                | LaunchResult.Refused refusal -> return LaunchOutcome.Refused refusal
                // A retried launch that waits for a PIN: the next GetSession returns the enrolment.
                | LaunchResult.Enrolling _ -> return LaunchOutcome.RedirectTo Session.openedUrl
        }


    /// Handles the return from the identity provider and returns the redirect. Writes the session
    /// cookie when a session opens, or the enrolment cookie when a PIN is needed.
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
                // Closes the previous session of this browser; its cookie would hide the enrolment.
                match cookie.read () with
                | Some id -> do! env.session.close id
                | None -> ()

                cookie.delete ()
                enrolment.write attempt until
                return redirect
            | CallbackResult.Refused(_, redirect)
            | CallbackResult.Superseded redirect -> return redirect
        }
