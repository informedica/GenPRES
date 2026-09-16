namespace ServerApi

open System
open Shared.Types


module Adapters =

    open Informedica.GenForm.Lib


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
        | None -> None, Informedica.GenOrder.Lib.OrderLogging.noOp
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


    /// The contract model in and out over the same pipeline, for the order plan port until it
    /// is on domain values itself.
    let private evaluateModel demo agent logger (provider: Resources.IResourceProvider) =
        fun (cmd: Shared.Api.OrderContextCommand) (ctx: OrderContext) ->
            async {
                do! setComponentName "OrderContext" agent

                return ctx |> OrderContextService.evaluateModel demo logger provider cmd
            }


    let private makeOrderPlanPort
        agent
        (provider: Resources.IResourceProvider)
        (evaluate: Shared.Api.OrderContextCommand -> OrderContext -> Async<Result<OrderContext, string[]>>)
        : OrderPlanPort
        =
        {
            recalculate =
                fun plan ->
                    async {
                        do! setComponentName "OrderPlan" agent
                        return plan |> OrderPlanService.recalculate (provider.GetTotals()) |> Ok
                    }
            navigate =
                fun plan contextId ctxCmd ctx ->
                    async {
                        do! setComponentName "OrderPlan" agent
                        let recalc = OrderPlanService.recalculate (provider.GetTotals())
                        return! OrderPlanService.navigate recalc evaluate plan contextId ctxCmd ctx
                    }
            newOrderContext =
                fun plan category ->
                    OrderPlanService.newOrderContext
                        (OrderPlanService.recalculate (provider.GetTotals()))
                        evaluate
                        plan
                        category
            addOrderContext =
                fun plan ctx ->
                    async {
                        do! setComponentName "OrderPlan" agent

                        return
                            plan
                            |> OrderPlanService.addOrderContext (fun () -> System.Guid.NewGuid().ToString()) ctx
                            |> Result.map (OrderPlanService.recalculate (provider.GetTotals()))
                    }
            removeOrderContexts =
                fun plan ids ->
                    async {
                        return
                            plan
                            |> OrderPlanService.removeOrderContexts ids
                            |> OrderPlanService.recalculate (provider.GetTotals())
                            |> Ok
                    }
            openWith =
                fun pat contexts ->
                    async {
                        do! setComponentName "OrderPlan" agent

                        return
                            OrderPlanService.openWith pat contexts
                            |> OrderPlanService.recalculate (provider.GetTotals())
                            |> Ok
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
            challenge = fun _ _ -> async { return SigningResponse.Refused SigningRefusal.NoSession }
            submit = fun _ _ -> async { return SigningResponse.Refused SigningRefusal.NoSession }
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


    let makeAppEnvWith
        (demo: bool)
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
            orderPlan = makeOrderPlanPort agent provider (evaluateModel demo agent logger provider)
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
                            |> Option.filter (String.IsNullOrWhiteSpace >> not)
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
            // an in-memory stub with a two-minute Launch lifetime; its sessions live as long
            // as this AppEnv
            session =
                StubDatabase.makeSessionPort
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
            (LaunchSeal.newKey System.Security.Cryptography.RandomNumberGenerator.GetBytes)
            (StubDirectory.make (fun () -> DateTime.UtcNow) PublicKey.randomId)
            (StubMail.make ()).port
            provider
