namespace ServerApi

open System
open Shared.Types


module Adapters =

    open Informedica.GenForm.Lib
    open Informedica.Utils.Lib.BCL
    // after the contract model, so that the order context and order plan ports, on domain
    // values, read unqualified; the other ports name only what the contract model has
    open Informedica.GenOrder.Lib


    let private interactionJsonCache =
        lazy
            (let path =
                System.IO.Path.Combine(Informedica.Utils.Lib.AppPath.interactionsDir (), "Data.JSON")
                |> System.IO.Path.GetFullPath

             if System.IO.File.Exists(path) then
                 System.IO.File.ReadAllText(path) |> Some
             else
                 None)


    let loadInteractionJson () = interactionJsonCache.Value


    let toSharedDrugInteraction (di: Informedica.GenInteract.Lib.DrugInteraction) : Shared.Types.DrugInteraction =
        {
            Name = di.Name
            Drug1 = di.Drug1
            Drug2 = di.Drug2
        }


    let private resolveLogger () =
        match Logging.loggingLevel with
        | None -> None, OrderLogging.noOp
        | Some level ->
            let agent = Logging.getLogger level Logging.OrderLogger
            (Some agent, agent.Logger)


    let private setComponentName name agent =
        async {
            match agent with
            | Some a -> do! a |> Logging.setComponentName (Some name)
            | None -> ()
        }


    let private makeFormularyPort (provider: Resources.IResourceProvider) : FormularyPort =
        {
            getFormulary = fun form -> async { return form |> FormularyService.get provider }

            getParenteralia =
                fun par -> async { return par |> ParenteraliaService.get provider |> Result.mapError Array.singleton }
        }


    let private makeOrderContextPort agent logger (provider: Resources.IResourceProvider) : OrderContextPort =
        {
            evaluate =
                fun cmd pc ->
                    async {
                        do! setComponentName "OrderContext" agent

                        return pc |> OrderContextService.evaluate logger provider cmd
                    }
        }


    /// The order plan port over the domain's rules: the plan's contexts through the order
    /// context service, its totals over the provider's data, the refusals worded with the
    /// nutrition rule sets the server owns.
    let private makeOrderPlanPort
        agent
        logger
        (provider: Resources.IResourceProvider)
        (ruleSets: NutritionRuleSet[])
        (newId: unit -> string)
        : OrderPlanPort
        =
        let recalc plan =
            plan |> OrderPlan.recalculate (provider.GetTotals())

        let refused r =
            r |> Result.mapError (OrderPlanMapper.words ruleSets >> Array.singleton)

        {
            recalculate =
                fun plan ->
                    async {
                        do! setComponentName "OrderPlan" agent
                        return plan |> recalc |> Ok
                    }
            navigate =
                fun plan contextId cmd pc ->
                    async {
                        do! setComponentName "OrderPlan" agent

                        return
                            pc
                            |> OrderContextService.evaluate logger provider cmd
                            |> Result.bind (fun evaluated ->
                                plan |> OrderPlan.updateContext ruleSets contextId evaluated |> refused
                            )
                            |> Result.map recalc
                    }
            addOrderContext =
                fun plan pc ->
                    async {
                        do! setComponentName "OrderPlan" agent

                        return plan |> OrderPlan.addOrderContext newId pc |> refused |> Result.map recalc
                    }
            newOrderContext =
                fun plan category ->
                    async {
                        do! setComponentName "OrderPlan" agent

                        return
                            match plan |> OrderPlan.admits category |> refused with
                            | Error e -> Error e
                            | Ok() ->
                                let set = ruleSets |> NutritionRuleSet.tryFind category

                                // the workbench: the plan's patient, the category's indications
                                // and generics, nothing else offered yet and nothing selected;
                                // the discovery keeps of what the evaluation offers only these
                                let workbench: OrderContext =
                                    {
                                        Filter =
                                            {
                                                Indications =
                                                    set |> Option.map _.Indications |> Option.defaultValue [||]
                                                Generics = set |> Option.map _.Generics |> Option.defaultValue [||]
                                                Routes = [||]
                                                Forms = [||]
                                                DoseTypes = [||]
                                                Diluents = [||]
                                                Components = [||]
                                                Indication = None
                                                Generic = None
                                                Route = None
                                                Form = None
                                                DoseType = None
                                                Diluent = None
                                                SelectedComponents = [||]
                                            }
                                        Patient = plan.Patient
                                        Scenarios = [||]
                                    }

                                let planContext = PlanContext.create (newId ()) (OrderCategory.Nutrition category)

                                let evaluate ctx =
                                    ctx
                                    |> planContext
                                    |> OrderContextService.evaluate logger provider OrderContext.UpdateOrderContext
                                    |> Result.map _.Context

                                workbench
                                |> NutritionRuleSet.discover evaluate
                                |> Result.map (fun discovered ->
                                    let pc = discovered |> planContext

                                    { plan with
                                        Contexts =
                                            Array.append
                                                plan.Contexts
                                                [|
                                                    { pc with
                                                        Intake =
                                                            discovered |> OrderContext.intake (provider.GetTotals())
                                                    }
                                                |]
                                    }
                                    |> recalc
                                )
                    }
            removeOrderContexts =
                fun plan ids ->
                    async {
                        do! setComponentName "OrderPlan" agent

                        return plan |> OrderPlan.removeOrderContexts ids |> recalc |> Ok
                    }
            openWith =
                fun pat contexts ->
                    async {
                        do! setComponentName "OrderPlan" agent
                        return OrderPlan.create pat contexts |> recalc |> Ok
                    }
        }


    /// The session port of a server that does not launch: every Launch is refused as
    /// invalid and no session is ever found. Used in production until the scope switch (#580)
    /// decides what a production server exposes.
    let sessionDisabled: SessionPort =
        {
            present = fun _ -> async { return LaunchResult.Refused LaunchRefusal.LaunchInvalid }
            callback =
                fun _ ->
                    async {
                        return
                            CallbackResult.Refused(
                                LaunchRefusal.LaunchInvalid,
                                Session.refusedUrl LaunchRefusal.LaunchInvalid
                            )
                    }
            find = fun _ -> async { return SessionLookup.NotFound }
            close = fun _ -> async { return () }
            findEnrolment = fun _ -> async { return None }
            supplyPin = fun _ _ _ -> async { return SupplyPinResult.Refused PinRefusal.AttemptExpired }
            dropEnrolment = fun _ -> async { return () }
            challenge = fun _ _ -> async { return SigningOutcome.Refused SigningRefusal.NoSession }
            submit = fun _ _ -> async { return SigningOutcome.Refused SigningRefusal.NoSession }
            seen = fun _ _ -> async { return None }
            openVersion = fun _ _ -> async { return None }
        }


    /// The messages of a provider that did not load; None when it did.
    let private notLoaded (provider: Resources.IResourceProvider) =
        let info = provider.GetResourceInfo()

        if info.IsLoaded then
            None
        else
            info.Messages |> Array.map (fun msg -> FormLogging.formatMessage msg) |> Some


    /// <summary>
    /// The env of the server. <c>store</c> is the SQLite connection string of the session store:
    /// set, the migrations are applied and the session port runs over the record in the
    /// database; unset, over the record in memory.
    /// </summary>
    /// <summary>
    /// The session store made ready before anything is hosted: the migrations applied and the
    /// demo credentials seeded, each once. Answers what went wrong instead of raising, so that
    /// a store that cannot be written is a refused start with a message and an exit code, not
    /// a crash. A server without the setting has no store to prepare.
    /// </summary>
    let prepareStore (store: string option) : Result<unit, string> =
        match store with
        | None -> Ok()
        | Some value ->
            try
                let cs =
                    SqlDatabase.connectionString (Informedica.Utils.Lib.AppPath.rootPath ()) value

                SqlSchema.apply cs |> ignore

                // the machine reads a credential from the rows, so the demo logins need theirs
                // in the file: without them no Prescriber could sign and nothing would say why
                match
                    SqlSessions.seed
                        cs
                        DateTime.UtcNow
                        (StubCredentials.seed System.Security.Cryptography.RandomNumberGenerator.GetBytes)
                with
                | Session.StoreOutcome.Written -> Ok()
                | outcome -> Error $"the session store could not be seeded with the demo credentials: %A{outcome}"
            with e ->
                Error $"the session store could not be prepared: %s{e.Message}"


    let makeAppEnvWith
        (demo: bool)
        (store: string option)
        (launchKey: LaunchSeal.Key)
        (directory: StubDirectory.Directory)
        (mail: MailPort)
        (provider: Resources.IResourceProvider)
        : AppEnv
        =
        let agent, logger = resolveLogger ()

        {
            formulary = makeFormularyPort provider
            orderContext = makeOrderContextPort agent logger provider
            orderPlan =
                makeOrderPlanPort agent logger provider NutritionRuleSets.all (fun () -> Guid.NewGuid().ToString())
            demo = demo
            interaction =
                {
                    checkInteractions =
                        fun drugs ->
                            async {
                                try
                                    let result =
                                        Informedica.GenInteract.Lib.Api.checkInteractions (loadInteractionJson ()) drugs
                                        |> List.map toSharedDrugInteraction

                                    return Ok result
                                with ex ->
                                    return Error [| ex.Message |]
                            }

                    getDrugNames =
                        fun () ->
                            async {
                                try
                                    let result = Informedica.GenInteract.Lib.Api.getDrugNames (loadInteractionJson ())

                                    return Ok result
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                }
            admin =
                {
                    // the setting as the DMZ reads it: blank is no secret
                    secret =
                        fun () ->
                            Informedica.Utils.Lib.Env.getItem "GENPRES_PASSWORD"
                            |> Option.filter String.notEmpty
                    now = fun () -> DateTimeOffset.UtcNow
                    listLogFiles =
                        fun () ->
                            async {
                                try
                                    return Ok(LogAnalyzer.listLogFiles ())
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                    analyzeLogFile = fun fileName -> async { return LogAnalyzer.analyzeFile fileName }
                    // a reload that leaves the provider unloaded is a failure with its messages,
                    // not a success: reloadCache records the state and returns normally
                    reloadResources =
                        fun () ->
                            async {
                                try
                                    Informedica.GenForm.Lib.Api.reloadCache logger provider

                                    return
                                        match notLoaded provider with
                                        | None -> Ok()
                                        | Some msgs -> Error msgs
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                }
            requireLoaded = fun () -> notLoaded provider
            // a stub with a two-minute Launch lifetime; its sessions live as long as this
            // AppEnv, and the record as long as its store
            session =
                let makeSessionPort =
                    match store with
                    | None -> StubDatabase.makeSessionPort
                    | Some value ->
                        let cs =
                            SqlDatabase.connectionString (Informedica.Utils.Lib.AppPath.rootPath ()) value

                        SqlSessions.makeSessionPort
                            (fun msg ->
                                Informedica.Utils.Lib.ConsoleWriter.NewLineTime.writeWarningMessage
                                    $"session store: %s{msg}"
                            )
                            cs
                            (fun () -> DateTime.UtcNow)

                makeSessionPort
                    (fun () -> DateTime.UtcNow)
                    PublicKey.randomId
                    // the confirmation code and the salt from the CSPRNG, the code mac under
                    // the host key
                    (Session.newCode System.Security.Cryptography.RandomNumberGenerator.GetInt32)
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes
                    (Session.codeMac launchKey)
                    (fun launch -> LaunchSeal.verify DateTime.UtcNow launchKey launch)
                    directory.idp
                    directory.registry
                    StubPatientData.port
                    mail
                    // the credential store, seeded per stub login
                    (Session.initialState (
                        StubCredentials.seed System.Security.Cryptography.RandomNumberGenerator.GetBytes
                    ))
        }


    /// An env with its own seal key and stub directory: what tests and the MCP host build. The
    /// server builds `makeAppEnvWith` so that its stub pages share the key and the directory.
    let makeAppEnv (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) =
        // the demo flag as the server reads it, once
        let demo =
            Informedica.Utils.Lib.Env.getItem "GENPRES_PROD"
            |> Option.map (fun v -> v <> "1")
            |> Option.defaultValue true

        makeAppEnvWith
            demo
            None
            (LaunchSeal.newKey System.Security.Cryptography.RandomNumberGenerator.GetBytes)
            (StubDirectory.make (fun () -> DateTime.UtcNow) PublicKey.randomId)
            (StubMail.make ()).port
            provider
