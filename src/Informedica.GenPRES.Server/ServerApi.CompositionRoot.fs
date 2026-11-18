namespace ServerApi


module CompositionRoot =

    open Informedica.GenPRES.Shared.Api


    /// Builds the server api for one request. The settings and the env are built once per host, the
    /// cookies once per request. The settings get the departments as loaded at the time of the request.
    let compose
        (settings: Informedica.GenForm.Lib.Types.Departments -> ServerSettings)
        (env: AppEnv)
        (cookie: SessionCookie)
        (stateCookie: LaunchStateCookie)
        (enrolment: EnrolmentCookie)
        : IServerApi
        =
        {
            // The computing functions run through Compute.bound: logging, the session check, the
            // loaded-resources check and the exception as an Error.
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

            processPatientData =
                Compute.bound
                    env
                    cookie
                    PatientDataCommand.toString
                    PatientDataCommand.gate
                    (PatientDataCommand.processCmd env)

            // The order plan, including nutrition.
            processOrderPlan =
                Compute.bound
                    env
                    cookie
                    OrderPlanCommand.toString
                    (fun _ -> Gate.RequiresLoaded)
                    (OrderPlanCommand.processCmd env)

            // Calculates the totals of the requested orders without changing the plan.
            processTotals =
                Compute.bound
                    env
                    cookie
                    TotalsQuery.toString
                    (fun _ -> Gate.RequiresLoaded)
                    (OrderPlanCommand.processTotals env)

            // The check depends on the command: the medication names do not need the loaded resources.
            processInteraction =
                Compute.bound
                    env
                    cookie
                    InteractionCommand.toString
                    InteractionCommand.gate
                    (InteractionCommand.processCmd env)

            processLaunch =
                Compute.logged env "launch" LaunchCommand.toString (LaunchCommand.processCmd env cookie stateCookie)

            processSession =
                Compute.logged env "session" SessionCommand.toString (SessionCommand.processCmd env cookie enrolment)

            processSigning = Compute.logged env "signing" SigningCommand.toString (SigningCommand.processCmd env cookie)

            // Admin commands read no session cookie, return no notice and run before the resources are loaded.
            processAdmin = Compute.logged env "admin" AdminCommand.toString (AdminCommand.processCmd env)

            getSettings =
                fun () ->
                    async {
                        Logging.ServerLogging.Info "Processing settings"
                        |> Informedica.Logging.Lib.Logging.logInfo env.logger

                        // Reads the departments on every request, so a reload shows on the next request.
                        return env.departments () |> settings
                    }

            testApi = fun () -> async { return "Hello world!" }
        }
