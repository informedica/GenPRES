namespace ServerApi


module LaunchCommand =

    open Shared.Types
    open Shared.Api


    /// The presentation of a Launch over the session port. The session id goes into the cookie
    /// and nowhere else; a refusal is a value. A server exception is not a refusal: it propagates,
    /// Fable.Remoting answers 500 and the client's transport-error path retries.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (stateCookie: LaunchStateCookie) (cmd: LaunchCommand) =
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
                | LaunchResult.Enrolling _ -> return LaunchOutcome.RedirectTo Session.openedUrl
        }


    /// The callback over the session port: the browser is back from the IdentityProvider.
    /// Answers where it goes next; the session cookie is set when a Session opened, the
    /// enrolment cookie when the launch suspended at the PIN question.
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
                // would, and a Session replaced in its own browser is not told of its ending;
                // left in place, its cookie would hide the enrolment at the next GetSession
                match cookie.read () with
                | Some id -> do! env.session.close id
                | None -> ()

                cookie.delete ()
                enrolment.write attempt until
                return redirect
            | CallbackResult.Refused(_, redirect)
            | CallbackResult.Superseded redirect -> return redirect
        }
