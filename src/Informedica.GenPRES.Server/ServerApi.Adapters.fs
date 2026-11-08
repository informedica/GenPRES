namespace ServerApi

open System
open Informedica.GenPRES.Shared.Types


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


    /// Run an interaction query against the cached data, or say it is unavailable.
    /// A missing cache is an incomplete deployment, not a patient with nothing to
    /// report, so it fails the query rather than answering that nothing was found.
    let private withInteractionJson f =
        async {
            match loadInteractionJson () with
            | None ->
                return
                    Error
                        [|
                            "Interaction data is unavailable: data/cache/interactions/Data.JSON was not found. Interaction screening cannot run until it is restored."
                        |]
            | Some json ->
                try
                    return Ok(f json)
                with ex ->
                    return Error [| ex.Message |]
        }


    let toSharedDrugInteraction
        (di: Informedica.GenInteract.Lib.DrugInteraction)
        : Informedica.GenPRES.Shared.Types.DrugInteraction
        =
        {
            Name = di.Name
            Drug1 = di.Drug1
            Drug2 = di.Drug2
        }


    let private makeFormularyPort logger (provider: Resources.IResourceProvider) : FormularyPort =
        {
            getFormulary = fun form -> async { return form |> FormularyService.get provider }

            getParenteralia =
                fun par ->
                    async {
                        return
                            par
                            |> ParenteraliaService.get logger provider
                            |> Result.mapError Array.singleton
                    }
        }


    /// Used by the order context port and the order plan port: the nutrition rule sets from the
    /// provider, and the evaluation of a context settled to the rule set of its category.
    module NutritionRuleSets =

        /// Maps an order plan error to its words.
        let refused r = r |> Result.mapError (OrderPlanMapper.words >> Array.singleton)


        /// The rule sets from the rule set rows of the provider, read on every use so that a reload of
        /// the sheet is seen.
        let ruleSets (provider: Resources.IResourceProvider) =
            provider.Get Resources.Keys.ruleSetData
            |> NutritionRuleSet.ofData
            |> Result.mapError (List.map OrderPlanMapper.words >> List.toArray)


        /// The rule sets and the rule set of a nutrition category, refused when the rule sets have
        /// none for it; for a medication context, no rule sets and no rule set.
        let ruleSetOf provider category =
            match category with
            | Some c ->
                ruleSets provider
                |> Result.bind (fun sets ->
                    sets |> NutritionRuleSet.find c |> refused |> Result.map (fun s -> sets, Some s)
                )
            | None -> Ok([||], None)


        /// The context evaluated with the command, as an outcome. An evaluated nutrition context is
        /// narrowed to the rule set and evaluated again, as long as more than one option is left; a
        /// refusal on the way is answered as the refused context. A medication context is evaluated once.
        let evaluateSettled start logger provider set cmd pc =
            let evaluate cmd pc =
                pc |> OrderContextService.evaluateOutcome start logger provider cmd

            let narrow (set: NutritionRuleSet) =
                Outcome.map (fun (pc: PlanContext) -> { pc with Context = pc.Context |> NutritionRuleSet.narrow set })

            match pc |> evaluate cmd, set with
            | Ok(Evaluated e), Some set ->
                Evaluated e
                |> NutritionRuleSet.settle
                    (Outcome.get >> _.Context)
                    (narrow set)
                    (function
                    | Evaluated pc -> pc |> evaluate OrderContext.UpdateOrderContext
                    // a refusal ends the settling: the same filter again stops it
                    | refused -> Ok refused
                    )
            | answer, _ -> answer


        /// The order contexts the link rules offer to link to the main context, when it is narrowed to
        /// one order. There is one offer per rule, and for a rule without a generic one per generic that
        /// the dose rules offer. Each offer is evaluated for the main context's patient, with its dose
        /// types narrowed to the side, its category from the rule sets and, for a nutrition category,
        /// its lists narrowed to that rule set. When the narrowing leaves one dose type of several, the
        /// offer is evaluated once more, so that it comes with the scenarios of that dose type. An offer is
        /// left out when the rules refuse it, when its evaluation fails, or when its evaluation finds no dose
        /// type of the side.
        let linkedOffers routeMapping rules sets evaluate (main: PlanContext) =
            let offer side (ctx: OrderContext) =
                let category = NutritionRuleSet.orderCategory sets ctx.Filter.Indication ctx.Filter.Generic

                let set =
                    match category with
                    | OrderCategory.Nutrition c -> sets |> NutritionRuleSet.tryFind c
                    | OrderCategory.Medication -> None

                match ctx |> PlanContext.create "" category |> evaluate set with
                | Ok(Evaluated pc) ->
                    // the evaluation picks a dose type when it finds only one, so an offer without a
                    // dose type of the side is left out rather than offered with another dose
                    let narrowed = pc.Context |> OrderContext.narrowToLinkSide side

                    match narrowed.Filter.DoseTypes with
                    | [||] -> None
                    | [| _ |] when pc.Context.Filter.DoseTypes.Length > 1 ->
                        // without a picked dose type the evaluation finds no scenarios; evaluated once more,
                        // the one dose type of the side left in the list is read as the pick, and the lookup
                        // filters on it, so the answer lists that dose type only and needs no narrowing
                        match { pc with Context = narrowed } |> evaluate set with
                        | Ok(Evaluated pc) -> Some pc
                        | _ -> None
                    | _ -> Some { pc with Context = narrowed }
                | _ -> None

            let offers (rule: LinkRule) =
                let unpicked = OrderContext.ofLinkSide rule.Linked main.Context.Patient

                match unpicked.Filter.Generic with
                | Some _ -> [| unpicked |]
                | None ->
                    // a rule without a generic offers every generic the dose rules give under its other
                    // fields; a refusal offers none
                    match unpicked |> PlanContext.create "" OrderCategory.Medication |> evaluate None with
                    | Ok(Evaluated pc) ->
                        pc.Context.Filter.Generics
                        |> Array.map (fun g -> { unpicked with OrderContext.Filter.Generic = Some g })
                    | _ -> [||]
                |> Array.choose (offer rule.Linked)

            main.Context
            |> OrderContext.rulesForMainOrder routeMapping rules
            |> Array.collect offers


    /// The dose rules of the context as sent, before a command changes it: the stand-alone dose rules, and
    /// the dose rules that need a main order when the context selects them. The context selects them with
    /// its picked indication and generic, a list of one option counting as its pick, and with a dose type
    /// list of one dose type. The lists are the server's answer, which a pick or a seed does not change,
    /// so only a linked offer, whose dose types the server narrowed to its link side, selects such a dose
    /// rule. The prescribe still refuses an order that needs a main order and comes without one. When
    /// the dose rules did not load there are none.
    let doseRulesOf (provider: Resources.IResourceProvider) (ctx: OrderContext) =
        let picks = ctx |> OrderContext.filterPicks

        // a reload between two reads can leave a resource out of the cache; then there are no dose rules
        try
            let standAlone = provider.Get Resources.Keys.standAloneDoseRules

            match picks.Indication, picks.Generic, ctx.Filter.DoseTypes with
            | Some _, Some _, [| doseType |] ->
                let routes = provider.GetRouteMappings()

                // every form of the dose, so that a clear of the form lists the other forms again; the lookup
                // after the command applies the form picked
                provider.GetDoseRules()
                |> DoseRule.filter
                    routes
                    { picks with
                        Form = None
                        DoseType = Some doseType
                    }
                |> Array.filter (LinkRule.needsMainOrder routes (provider.Get Resources.Keys.linkRules))
                |> Array.append standAlone
            | _ -> standAlone
        with :? System.Collections.Generic.KeyNotFoundException ->
            [||]


    /// The provider whose dose rules are the dose rules of the context, as doseRulesOf gives them.
    /// Every dose rule lookup of the order context port reads them, for the pick lists, the scenarios and
    /// the refusal alike.
    let withDoseRulesOf (ctx: OrderContext) (provider: Resources.IResourceProvider) =
        { new Resources.IResourceProvider with
            member _.Get key = provider.Get key
            member _.GetData() = provider.GetData()
            member _.GetUnitMappings() = provider.GetUnitMappings()
            member _.GetRouteMappings() = provider.GetRouteMappings()
            member _.GetValidForms() = provider.GetValidForms()
            member _.GetFormRoutes() = provider.GetFormRoutes()
            member _.GetFormularyProducts() = provider.GetFormularyProducts()
            member _.GetReconstitution() = provider.GetReconstitution()
            member _.GetParenteralMeds() = provider.GetParenteralMeds()
            member _.GetEnteralFeeding() = provider.GetEnteralFeeding()
            member _.GetProducts() = provider.GetProducts()

            member _.GetDoseRules() = ctx |> doseRulesOf provider

            member _.GetSolutionRules() = provider.GetSolutionRules()
            member _.GetRenalRules() = provider.GetRenalRules()
            member _.GetTotals() = provider.GetTotals()
            member _.GetGStandProvider() = provider.GetGStandProvider()
            member _.GetDepartments() = provider.GetDepartments()
            member _.GetResourceInfo() = provider.GetResourceInfo()
        }


    /// The order context port over the domain's rules: an evaluated nutrition context narrowed and
    /// settled to the rule set of the category it carries, a refused context unchanged. The evaluation leaves
    /// out the orders that need a main order unless the context as sent selects them, as a linked offer
    /// does; the linked offers are evaluated over every dose rule.
    let private makeOrderContextPort
        logger
        (provider: Resources.IResourceProvider)
        (now: unit -> DateTime)
        : OrderContextPort
        =
        {
            evaluate =
                fun cmd pc ->
                    async {
                        return
                            NutritionRuleSets.ruleSetOf provider (pc |> PlanContext.nutritionCategory)
                            |> Result.bind (fun (_, set) ->
                                pc
                                |> NutritionRuleSets.evaluateSettled
                                    (now ())
                                    logger
                                    (provider |> withDoseRulesOf pc.Context)
                                    set
                                    cmd
                            )
                    }
            linkedOrders =
                fun pc ->
                    async {
                        let start = now ()

                        let evaluate set pc =
                            pc
                            |> NutritionRuleSets.evaluateSettled
                                start
                                logger
                                provider
                                set
                                OrderContext.UpdateOrderContext

                        // without readable rule sets an offer's category is unknown, so nothing is offered
                        return
                            match NutritionRuleSets.ruleSets provider with
                            | Ok sets ->
                                pc
                                |> NutritionRuleSets.linkedOffers
                                    (provider.GetRouteMappings())
                                    (provider.Get Resources.Keys.linkRules)
                                    sets
                                    evaluate
                                |> Ok
                            | Error errs ->
                                Logging.ServerLogging.Error
                                    $"no linked orders offered, the rule sets cannot be read: %A{errs}"
                                |> Informedica.Logging.Lib.Logging.logError logger

                                Ok [||]
                    }
        }


    /// The order plan port over the domain's rules: the plan's contexts through the order
    /// context service, its totals over the provider's data, the nutrition rule sets from the
    /// rule set rows of the provider, read on every use so that a reload of the sheet is seen.
    let private makeOrderPlanPort
        logger
        (provider: Resources.IResourceProvider)
        (newId: unit -> string)
        (now: unit -> DateTime)
        : OrderPlanPort
        =
        let recalc plan = plan |> OrderPlan.recalculate (provider.GetTotals())

        {
            recalculate = fun plan -> async { return plan |> recalc |> Ok }
            followPatientData =
                fun plan -> async { return plan |> OrderPlan.followPatientData logger provider |> recalc |> Ok }
            evaluateContext =
                fun plan contextId cmd pc ->
                    async {
                        let category =
                            plan.Contexts
                            |> Array.tryFind (fun c -> c.Id = contextId)
                            |> Option.bind PlanContext.nutritionCategory

                        // the plan answers a refusal as its message, as the message-list contract does
                        return
                            NutritionRuleSets.ruleSetOf provider category
                            |> Result.bind (fun (sets, set) ->
                                pc
                                |> NutritionRuleSets.evaluateSettled (now ()) logger provider set cmd
                                |> Result.bind (OrderContext.ofOutcome >> Result.mapError OrderContextService.refusal)
                                |> Result.bind (fun evaluated ->
                                    plan
                                    |> OrderPlan.updateContext sets contextId evaluated
                                    |> NutritionRuleSets.refused
                                )
                            )
                            |> Result.map recalc
                    }
            addOrderContexts =
                fun plan pc linked ->
                    async {
                        return
                            plan
                            |> OrderPlan.addOrderContexts
                                newId
                                (provider.GetRouteMappings())
                                (provider.Get Resources.Keys.linkRules)
                                pc
                                linked
                            |> NutritionRuleSets.refused
                            |> Result.map recalc
                    }
            removeOrderContexts =
                fun plan ids -> async { return plan |> OrderPlan.removeOrderContexts ids |> recalc |> Ok }
            openWith = fun pat contexts -> async { return OrderPlan.create pat contexts |> recalc |> Ok }
            totals =
                fun pat contexts -> async { return contexts |> OrderPlan.totalsOver (provider.GetTotals()) pat |> Ok }
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
            patient = fun _ -> None
            close = fun _ -> async { return () }
            closeTab = fun _ _ -> async { return OnOwnToken.Done() }
            findEnrolment = fun _ -> async { return None }
            supplyPin = fun _ _ _ -> async { return SupplyPinResult.Refused PinRefusal.AttemptExpired }
            dropEnrolment = fun _ -> async { return () }
            challenge = fun _ _ -> async { return SigningOutcome.Refused SigningRefusal.NoSession }
            submit = fun _ _ -> async { return SigningOutcome.Refused SigningRefusal.NoSession }
            seen = fun _ _ -> async { return None }
            openVersion = fun _ _ _ -> async { return OnOwnToken.Done None }
            refresh = fun _ _ -> async { return OnOwnToken.Done None }
        }


    /// The messages of a provider that did not load; None when it did.
    let private notLoaded (provider: Resources.IResourceProvider) =
        let info = provider.GetResourceInfo()

        if info.IsLoaded then
            None
        else
            info.Messages |> Array.map (fun msg -> FormLogging.formatMessage msg) |> Some


    /// The normal-value tables of weight and height the provider parsed at load, while it is
    /// loaded; none when it is not, or when the rows did not parse.
    let normalValuesOf (provider: Resources.IResourceProvider) () =
        match notLoaded provider with
        | None -> provider.Get Informedica.GenForm.Lib.Resources.Keys.normalValueTables
        | Some _ -> None


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
                let cs = SqlDatabase.connectionString (Informedica.Utils.Lib.AppPath.rootPath ()) value

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
        (zone: TimeZoneInfo)
        (sessionIdle: TimeSpan)
        (launchKey: LaunchSeal.Key)
        (directory: StubDirectory.Directory)
        (mail: MailPort)
        (provider: Resources.IResourceProvider)
        : AppEnv
        =
        let requestLogger = Logging.getLogger Logging.RequestLogger
        let orderLogger = Logging.getLogger Logging.OrderLogger
        let orderPlanLogger = Logging.getLogger Logging.OrderPlanLogger
        let parenteraliaLogger = Logging.getLogger Logging.ParenteraliaLogger

        {
            formulary = makeFormularyPort parenteraliaLogger provider
            orderContext = makeOrderContextPort orderLogger provider (fun () -> DateTime.UtcNow)
            orderPlan =
                makeOrderPlanPort
                    orderPlanLogger
                    provider
                    (fun () -> Guid.NewGuid().ToString())
                    (fun () -> DateTime.UtcNow)
            demo = demo
            interaction =
                {
                    checkInteractions =
                        fun drugs ->
                            withInteractionJson (fun json ->
                                Informedica.GenInteract.Lib.Api.checkInteractions json drugs
                                |> List.map toSharedDrugInteraction
                            )

                    getDrugNames =
                        fun () -> withInteractionJson (fun json -> Informedica.GenInteract.Lib.Api.getDrugNames json)
                }
            admin =
                {
                    // the setting as the DMZ reads it: blank is no secret
                    secret =
                        fun () ->
                            Informedica.Utils.Lib.Env.getItem "GENPRES_PASSWORD"
                            |> Option.filter String.notEmpty
                    demo = demo
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
                                    Informedica.GenForm.Lib.Api.reloadCache orderLogger provider

                                    return
                                        match notLoaded provider with
                                        | None -> Ok()
                                        | Some msgs -> Error msgs
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                }
            requireLoaded = fun () -> notLoaded provider
            normalValues = normalValuesOf provider
            departments = fun () -> provider.GetDepartments()
            // a stub with a two-minute Launch lifetime; its sessions live as long as this
            // AppEnv, and the record as long as its store
            session =
                let makeSessionPort =
                    match store with
                    | None -> StubDatabase.makeSessionPort zone
                    | Some value ->
                        let cs = SqlDatabase.connectionString (Informedica.Utils.Lib.AppPath.rootPath ()) value

                        SqlSessions.makeSessionPort
                            (fun msg ->
                                Informedica.Utils.Lib.ConsoleWriter.NewLineTime.writeWarningMessage
                                    $"session store: %s{msg}"
                            )
                            cs
                            zone
                            (fun () -> DateTime.UtcNow)

                makeSessionPort
                    sessionIdle
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
                    // the platform's reading estimated at the boundary, from the tables as loaded;
                    // the test patients measured on the local date
                    (StubPatientData.portAt (fun () -> DateTime.UtcNow |> LocalDate.ofUtc zone)
                     |> Patient.estimating (normalValuesOf provider))
                    mail
                    // the credential store, seeded per stub login
                    (Session.initialState (
                        StubCredentials.seed System.Security.Cryptography.RandomNumberGenerator.GetBytes
                    ))
            logger = requestLogger
        }


    /// An env with its own seal key and stub directory: what tests and the MCP host build. The
    /// server builds makeAppEnvWith so that its stub pages share the key and the directory.
    let makeAppEnv (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) =
        // the demo flag as the server reads it, once
        let demo =
            Informedica.Utils.Lib.Env.getItem "GENPRES_PROD"
            |> Option.map (fun v -> v <> "1")
            |> Option.defaultValue true

        makeAppEnvWith
            demo
            None
            (LocalDate.defaultZone ())
            Session.defaultIdleLifetime
            (LaunchSeal.newKey System.Security.Cryptography.RandomNumberGenerator.GetBytes)
            (StubDirectory.make (fun () -> DateTime.UtcNow) PublicKey.randomId)
            (StubMail.make ()).port
            provider
