namespace ServerApi


module CompositionRoot =

    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime
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
            // every computing member goes through Compute.bound: the log, the Session marked seen
            // and told, the gate, the exception as an Error
            processCommand = Compute.bound env cookie Command.toString Command.gate (Command.processCmd env)

            processLaunch =
                Compute.logged "launch" LaunchCommand.toString (LaunchCommand.processCmd env cookie stateCookie)

            processSession =
                Compute.logged "session" SessionCommand.toString (SessionCommand.processCmd env cookie enrolment)

            processSigning = Compute.logged "signing" SigningCommand.toString (SigningCommand.processCmd env cookie)

            // never Session-bound: no cookie read, no notice, and not behind requireLoaded
            processAdmin = Compute.logged "admin" AdminCommand.toString (AdminCommand.processCmd env)

            getSettings =
                fun () ->
                    async {
                        writeInfoMessage "Processing settings"
                        return settings
                    }

            testApi = fun () -> async { return "Hello world!" }
        }
