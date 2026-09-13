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
            processOrderContext =
                Compute.bound
                    env
                    cookie
                    OrderContextCommand.toString
                    (fun _ -> Gate.RequiresLoaded)
                    (OrderContextCommand.processCmd env)

            processFormulary =
                Compute.bound
                    env
                    cookie
                    FormularyCommand.toString
                    (fun _ -> Gate.RequiresLoaded)
                    (FormularyCommand.processCmd env)

            processParenteralia =
                Compute.bound
                    env
                    cookie
                    ParenteraliaCommand.toString
                    (fun _ -> Gate.RequiresLoaded)
                    (ParenteraliaCommand.processCmd env)

            // the one plan, nutrition included
            processOrderPlan =
                Compute.bound
                    env
                    cookie
                    PlanCommand.toString
                    (fun _ -> Gate.RequiresLoaded)
                    (PlanCommand.processCmd env)

            // the one member whose gate differs per command: the drug names run open
            processInteraction =
                Compute.bound
                    env
                    cookie
                    InteractionCommand.toString
                    InteractionCommand.gate
                    (InteractionCommand.processCmd env)

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
