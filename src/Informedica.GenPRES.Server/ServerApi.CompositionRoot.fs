namespace ServerApi


module CompositionRoot =

    open Shared.Api


    /// The api of one request: the settings and the env are built once per host, the cookie
    /// once per request; the settings take the departments as loaded when asked.
    let compose
        (settings: Informedica.GenForm.Lib.Types.Departments -> ServerSettings)
        (env: AppEnv)
        (cookie: SessionCookie)
        (stateCookie: LaunchStateCookie)
        (enrolment: EnrolmentCookie)
        : IServerApi
        =
        let noPatient _ : Shared.Types.Patient option = None

        {
            // every computing member goes through Compute.bound: the log, the Session marked seen
            // and told what a patient change measures, the gate, the exception as an Error;
            // only the patient member records a patient
            // and told, the gate, the exception as an Error
            processOrderContext =
                Compute.bound
                    env
                    cookie
                    OrderContextCommand.toString
                    (fun _ -> Gate.RequiresLoaded)
                    noPatient
                    (OrderContextCommand.processCmd env)

            processFormulary =
                Compute.bound
                    env
                    cookie
                    FormularyCommand.toString
                    (fun _ -> Gate.RequiresLoaded)
                    noPatient
                    (FormularyCommand.processCmd env)

            processParenteralia =
                Compute.bound
                    env
                    cookie
                    ParenteraliaCommand.toString
                    (fun _ -> Gate.RequiresLoaded)
                    noPatient
                    (ParenteraliaCommand.processCmd env)

            processPatient =
                Compute.bound
                    env
                    cookie
                    PatientCommand.toString
                    PatientCommand.gate
                    PatientCommand.patientOf
                    (PatientCommand.processCmd env cookie)

            // the one plan, nutrition included
            processOrderPlan =
                Compute.bound
                    env
                    cookie
                    OrderPlanCommand.toString
                    (fun _ -> Gate.RequiresLoaded)
                    noPatient
                    (OrderPlanCommand.processCmd env)

            // the one member whose gate differs per command: the drug names run open
            processInteraction =
                Compute.bound
                    env
                    cookie
                    InteractionCommand.toString
                    InteractionCommand.gate
                    noPatient
                    (InteractionCommand.processCmd env)

            processLaunch =
                Compute.logged env "launch" LaunchCommand.toString (LaunchCommand.processCmd env cookie stateCookie)

            processSession =
                Compute.logged env "session" SessionCommand.toString (SessionCommand.processCmd env cookie enrolment)

            processSigning = Compute.logged env "signing" SigningCommand.toString (SigningCommand.processCmd env cookie)

            // never Session-bound: no cookie read, no notice, and not behind requireLoaded
            processAdmin = Compute.logged env "admin" AdminCommand.toString (AdminCommand.processCmd env)

            getSettings =
                fun () ->
                    async {
                        Logging.ServerLogging.Info "Processing settings"
                        |> Informedica.Logging.Lib.Logging.logInfo env.logger

                        // the departments as loaded now, so a reload reaches the next asker
                        return env.departments () |> settings
                    }

            testApi = fun () -> async { return "Hello world!" }
        }
