namespace ServerApi


module CompositionRoot =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
    open Shared.Types
    open Shared.Api


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
            // the Session the cookie names is marked seen and told whether the record moved on
            // or the Session ended, before the command is computed; without a cookie the
            // request computes as it always did. The token is never logged.
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
                        let! outcome = LaunchCommand.processCmd env cookie stateCookie cmd
                        writeInfoMessage $"Finished processing launch: {cmd |> LaunchCommand.toString}"
                        return outcome
                    }

            processSession =
                fun cmd ->
                    async {
                        writeInfoMessage $"Processing session: {cmd |> SessionCommand.toString}"
                        let! response = SessionCommand.processCmd env cookie enrolment cmd
                        writeInfoMessage $"Finished processing session: {cmd |> SessionCommand.toString}"
                        return response
                    }

            processSigning =
                fun cmd ->
                    async {
                        writeInfoMessage $"Processing signing: {cmd |> SigningCommand.toString}"
                        let! response = SigningCommand.processCmd env cookie cmd
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
