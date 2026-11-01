namespace ServerApi


module SessionCommand =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api


    /// Cookie-authenticated session commands over both cookies. GetSession answers the Session
    /// first; without one, a standing enrolment attempt; a gone attempt loses its cookie.
    /// SupplyPin works on the attempt in the cookie, never on one the client names. CloseSession
    /// drops both, even when nothing is found and even when the server-side close throws: an
    /// explicit close leaves the browser without a credential, unless the token is not the
    /// Session's own. The exception still propagates after the delete, so the failure stays
    /// visible.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (enrolment: EnrolmentCookie) (cmd: SessionCommand) =
        let toOpened = SessionMapper.toOpened env.demo env.session.patient

        let reopened answer =
            match answer with
            | OnOwnToken.Done opened -> SessionResponse.SessionResp(opened |> Option.map toOpened)
            | OnOwnToken.InactiveTab -> SessionResponse.InactiveTab

        async {
            match cmd with
            | SessionCommand.GetSession ->
                let! bySession =
                    async {
                        match cookie.read () with
                        | None -> return None
                        | Some id ->
                            match! env.session.find id with
                            | SessionLookup.Found opened ->
                                return Some(SessionResponse.SessionResp(Some(toOpened opened)))
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
                        return SessionResponse.SessionResp(Some(toOpened opened))
                    | SupplyPinResult.Refused(PinRefusal.CodeVoid as refusal)
                    | SupplyPinResult.Refused(PinRefusal.AttemptExpired as refusal)
                    | SupplyPinResult.Refused(PinRefusal.WrongActivePatient as refusal) ->
                        enrolment.delete ()
                        return SessionResponse.PinRefused refusal
                    | SupplyPinResult.Refused refusal -> return SessionResponse.PinRefused refusal
            // a version is taken up for the Session the cookie names; without a cookie there is
            // nothing to open. Writes no cookie.
            | SessionCommand.OpenVersion(id, token) ->
                match cookie.read () with
                | None -> return SessionResponse.SessionResp None
                | Some sid ->
                    let! answer = env.session.openVersion sid id token
                    return reopened answer
            // the EHR read again for the Session the cookie names and its head reopened on it;
            // without a cookie there is nothing to refresh. Writes no cookie.
            | SessionCommand.Refresh token ->
                match cookie.read () with
                | None -> return SessionResponse.SessionResp None
                | Some sid ->
                    let! answer = env.session.refresh sid token
                    return reopened answer
            | SessionCommand.CloseSession token ->
                let clear () =
                    cookie.delete ()
                    enrolment.delete ()

                let! closed =
                    match cookie.read () with
                    | Some id -> env.session.closeTab id token
                    | None -> async { return OnOwnToken.Done() }
                    |> Async.Catch

                match closed with
                // the Session and both cookies belong to another tab
                | Choice1Of2 OnOwnToken.InactiveTab -> return SessionResponse.InactiveTab
                | Choice1Of2(OnOwnToken.Done()) ->
                    try
                        match enrolment.read () with
                        | Some attempt -> do! env.session.dropEnrolment attempt
                        | None -> ()
                    finally
                        clear ()

                    return SessionResponse.SessionClosed
                | Choice2Of2 ex ->
                    clear ()
                    return raise ex
        }
