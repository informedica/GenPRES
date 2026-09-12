namespace ServerApi


module SessionCommand =

    open Shared.Types
    open Shared.Api


    /// Cookie-authenticated session commands over both cookies. GetSession answers the Session
    /// first; without one, a standing enrolment attempt; a gone attempt loses its cookie.
    /// SupplyPin works on the attempt in the cookie, never on one the client names. CloseSession
    /// drops both, even when nothing is found and even when the server-side close throws: an
    /// explicit close always leaves the browser without a credential. The exception still
    /// propagates after the delete, so the failure stays visible.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (enrolment: EnrolmentCookie) (cmd: SessionCommand) =
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
            // a version is taken up for the Session the cookie names; without a cookie there is
            // nothing to open. Writes no cookie.
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
