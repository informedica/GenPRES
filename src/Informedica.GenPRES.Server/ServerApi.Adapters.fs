namespace ServerApi

open System
open Informedica.GenPRES.Shared.Types


module Adapters =

    open Informedica.GenForm.Lib
    open Informedica.Utils.Lib.BCL
    // Opened after the contract model, so the order context and order plan ports, which take domain values, read
    // unqualified.
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


    /// Runs an interaction query on the cached interaction data. A missing cache is an incomplete deployment, so the
    /// query fails instead of reporting no interactions.
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


    let private makeFormularyPort logger today (provider: Resources.IResourceProvider) : FormularyPort =
        {
            getFormulary = fun form -> async { return form |> FormularyService.get (today ()) provider }

            getParenteralia =
                fun par ->
                    async {
                        return
                            par
                            |> ParenteraliaService.get logger provider
                            |> Result.mapError Array.singleton
                    }
        }


    /// Helpers for the order context and order plan ports: the rule sets from the provider, and the evaluation
    /// of a nutrition context settled to the rule set of its category.
    module RuleSets =

        // the server messages and the logging functions, for the warning about the rule set rows
        open Logging
        open Informedica.Logging.Lib


        /// Maps an order plan error in a result to its message.
        let refused r = r |> Result.mapError (OrderPlanMapper.words >> Array.singleton)


        /// Creates a rule set for every kind in the rows, with the label of the first row of the kind and the rows
        /// in row order. Leaves out the rows of a category that is not a rule set category.
        let ofData (rows: Types.Data.RuleSetData[]) =
            rows
            |> Array.groupBy _.Category
            |> Array.choose (fun (name, rs) ->
                RuleSetKindMapper.ofName name
                |> Option.map (fun kind ->
                    {
                        Kind = kind
                        Label = rs[0].Label
                        Rows = rs
                    }
                )
            )


        /// Returns the distinct category names in the rows that are not a rule set category, in row order.
        let unknownCategories (rows: Types.Data.RuleSetData[]) =
            rows
            |> Array.map _.Category
            |> Array.distinct
            |> Array.filter (RuleSetKindMapper.ofName >> Option.isNone)


        /// Returns the warning for a category name whose rows are left out of the rule sets.
        let unknownCategoryWarning name =
            ServerLogging.Warning $"RuleSets: %s{name} is not a rule set category, its rows are left out"


        /// Logs a warning for every category in the rule set rows of a loaded provider that is not a rule set
        /// category. Logs nothing for a provider that did not load.
        let warnUnknownCategories logger (provider: Resources.IResourceProvider) =
            if provider.GetResourceInfo().IsLoaded then
                provider.Get Resources.Keys.ruleSetRows
                |> unknownCategories
                |> Array.iter (unknownCategoryWarning >> Logging.logWarning logger)


        /// Reads the rule sets from the rule set rows of the provider on every call, so a reload of the sheet is seen.
        /// Each rule set keeps the rows that apply to the patient.
        let ruleSets (provider: Resources.IResourceProvider) pat =
            provider.Get Resources.Keys.ruleSetData
            |> ofData
            |> Array.map (RuleSet.forPatient pat)


        /// Returns the rule sets for the patient and the rule set of a nutrition category, or an error when there is
        /// no rule set for the category. For a medication context, returns no rule sets and no rule set.
        let ruleSetOf provider pat category =
            match category with
            | Some c ->
                let sets = ruleSets provider pat
                sets |> NutritionRuleSet.find c |> refused |> Result.map (fun s -> sets, Some s)
            | None -> Ok([||], None)


        /// Evaluates the context with the command and returns the outcome. An evaluated nutrition context is narrowed
        /// to the rule set and evaluated again, with every single-option list taken as its choice, until no
        /// single-option list is left without a choice; a refusal along the way is returned as the refused context.
        /// A refused nutrition context is also narrowed to the rule set, so it offers no medication of another
        /// category. A medication context is evaluated once.
        let evaluateSettled start logger provider set cmd pc =
            let evaluate cmd pc =
                pc |> OrderContextService.evaluateOutcome start logger provider cmd

            let narrow (set: RuleSet) =
                Outcome.map (fun (pc: PlanContext) -> { pc with Context = pc.Context |> NutritionRuleSet.narrow set })

            match pc |> evaluate cmd, set with
            | Ok(Evaluated e), Some set ->
                Evaluated e
                |> NutritionRuleSet.settle
                    (Outcome.get >> _.Context)
                    (narrow set)
                    (function
                    | Evaluated pc ->
                        // The evaluation rebuilds the lists from a fresh context and keeps a narrowed list only when
                        // its field has a choice, so every single-option list is made the choice first.
                        { pc with Context = pc.Context |> OrderContext.withFilterPicks }
                        |> evaluate OrderContext.UpdateOrderContext
                    // A refusal is returned unchanged, so the filter stays the same and the settling stops.
                    | refused -> Ok refused
                    )
            | Ok(Refused _ as refused), Some set -> refused |> narrow set |> Ok
            | answer, _ -> answer


        /// Returns the order contexts the link rules offer to link to the main context when it is narrowed to one
        /// order. There is one offer per rule, or for a rule without a generic one offer per generic the dose rules
        /// give. Each offer is evaluated for the patient of the main context, with its dose types narrowed to the
        /// link side, its category from the rule sets and, for a nutrition category, its lists narrowed to that rule
        /// set. When the narrowing leaves one dose type of several, the offer is evaluated again to get the scenarios
        /// of that dose type. An offer is left out when the rules refuse it, its evaluation fails, or it has no dose
        /// type of the link side.
        let linkedOffers routeMapping rules sets evaluate (main: PlanContext) =
            let offer side (ctx: OrderContext) =
                let category = NutritionRuleSet.orderCategory sets ctx.Filter.Indication ctx.Filter.Generic

                let set =
                    match category with
                    | OrderCategory.Nutrition c -> sets |> RuleSet.tryFind c
                    | OrderCategory.Medication -> None

                match ctx |> PlanContext.create "" category |> evaluate set with
                | Ok(Evaluated pc) ->
                    // The evaluation picks a dose type when it finds only one, so an offer without a dose type of the
                    // link side is left out rather than offered with another dose type.
                    let narrowed = pc.Context |> OrderContext.narrowToLinkSide side

                    match narrowed.Filter.DoseTypes with
                    | [||] -> None
                    | [| _ |] when pc.Context.Filter.DoseTypes.Length > 1 ->
                        // Without a picked dose type the evaluation finds no scenarios. Evaluated again, the single
                        // dose type left in the list counts as the pick, so the answer has only that dose type and
                        // needs no narrowing.
                        match { pc with Context = narrowed } |> evaluate set with
                        | Ok(Evaluated pc) -> Some pc
                        | _ -> None
                    | _ -> Some { pc with Context = narrowed }
                | _ -> None

            let offers (rule: LinkRule) =
                let unpicked = OrderContext.ofLinkSide rule.Linked main.Context.PatientData

                match unpicked.Filter.Generic with
                | Some _ -> [| unpicked |]
                | None ->
                    // A rule without a generic offers every generic the dose rules give for its other fields; a
                    // refusal offers none.
                    match unpicked |> PlanContext.create "" OrderCategory.Medication |> evaluate None with
                    | Ok(Evaluated pc) ->
                        pc.Context.Filter.Generics
                        |> Array.map (fun g -> { unpicked with OrderContext.Filter.Generic = Some g })
                    | _ -> [||]
                |> Array.choose (offer rule.Linked)

            main.Context
            |> OrderContext.rulesForMainOrder routeMapping rules
            |> Array.collect offers


    /// Returns the dose rules for the context as sent, before a command changes it: the stand-alone dose rules, plus
    /// the dose rules that need a main order when the context selects them. The context selects them with a picked
    /// indication and generic, where a single-option list counts as picked, and a dose type list of exactly one.
    /// Only a linked offer, whose dose types the server narrowed to the link side, has such a list, because a pick or
    /// a seed does not change the lists the server returned. Prescribing still refuses an order that needs a main
    /// order and has none. Returns an empty array when a resource is missing from the cache.
    let doseRulesOf (provider: Resources.IResourceProvider) (ctx: OrderContext) =
        let picks = ctx |> OrderContext.filterPicks

        // a reload between two reads can leave a resource out of the cache; then there are no dose rules
        try
            let standAlone = provider.Get Resources.Keys.standAloneDoseRules

            match picks.Indication, picks.Generic, ctx.Filter.DoseTypes with
            | Some _, Some _, [| doseType |] ->
                let routes = provider.GetRouteMappings()

                // Every form of the dose, so clearing the form lists the other forms again; the lookup after the
                // command applies the picked form.
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


    /// Wraps the provider so its dose rules are the dose rules of the context from doseRulesOf. Every dose rule lookup
    /// of the order context port reads them: the pick lists, the scenarios and the refusal.
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


    /// Makes the order context port. A nutrition context is narrowed to the rule set of its category, and an
    /// evaluated one is also settled to it. The evaluation leaves out orders that need a main order unless the
    /// context as sent selects them, as a linked offer does; linked offers are evaluated over all dose rules.
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
                            RuleSets.ruleSetOf provider pc.Context.PatientData (pc |> PlanContext.nutritionCategory)
                            |> Result.bind (fun (_, set) ->
                                pc
                                |> RuleSets.evaluateSettled
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
                            |> RuleSets.evaluateSettled start logger provider set OrderContext.UpdateOrderContext

                        return
                            pc
                            |> RuleSets.linkedOffers
                                (provider.GetRouteMappings())
                                (provider.Get Resources.Keys.linkRules)
                                (RuleSets.ruleSets provider pc.Context.PatientData)
                                evaluate
                            |> Ok
                    }
        }


    /// Makes the order plan port: evaluates the contexts of the plan through the order context service, calculates
    /// the totals from the provider data, and reads the rule sets on every call, so a reload of the sheet
    /// is seen.
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

                        // A refused evaluation becomes an error with its message, as the message-list contract expects.
                        return
                            RuleSets.ruleSetOf provider pc.Context.PatientData category
                            |> Result.bind (fun (sets, set) ->
                                pc
                                |> RuleSets.evaluateSettled (now ()) logger provider set cmd
                                |> Result.bind (OrderContext.ofOutcome >> Result.mapError OrderContextService.refusal)
                                |> Result.bind (fun evaluated ->
                                    plan |> OrderPlan.updateContext sets contextId evaluated |> RuleSets.refused
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
                            |> RuleSets.refused
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
            patientData = fun _ -> None
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


    /// Returns the patient data resources of the provider; None when the provider is not loaded or the normal-value
    /// rows did not parse.
    let patientDataResourcesOf (provider: Resources.IResourceProvider) () =
        match notLoaded provider with
        | None -> provider |> Api.getPatientDataResources
        | Some _ -> None


    /// Prepares the session store before anything is hosted: applies the migrations and seeds the demo credentials.
    /// Returns an error instead of raising, so a store that cannot be written stops the start with a message and an
    /// exit code. Does nothing when there is no store setting.
    let prepareStore (store: string option) : Result<unit, string> =
        match store with
        | None -> Ok()
        | Some value ->
            try
                let cs = SqlDatabase.connectionString (Informedica.Utils.Lib.AppPath.rootPath ()) value

                SqlSchema.apply cs |> ignore

                // The session functions read credentials from the rows, so the demo logins need them in the file;
                // without them no prescriber could sign and nothing would say why.
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


    /// Makes the server environment. store is the SQLite connection string of the session store: when set, the
    /// session port runs over the record in that database; when unset, over a record in memory.
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
        let today () = DateTime.UtcNow |> LocalDate.ofUtc zone

        {
            formulary = makeFormularyPort parenteraliaLogger today provider
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
                    // A blank or whitespace setting counts as no secret.
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
                    // A reload that leaves the provider unloaded is a failure with its messages; reloadCache records
                    // the state and returns normally.
                    reloadResources =
                        fun () ->
                            async {
                                try
                                    Informedica.GenForm.Lib.Api.reloadCache orderLogger provider
                                    RuleSets.warnUnknownCategories orderLogger provider

                                    return
                                        match notLoaded provider with
                                        | None -> Ok()
                                        | Some msgs -> Error msgs
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                }
            requireLoaded = fun () -> notLoaded provider
            patientDataResources = patientDataResourcesOf provider
            today = today
            departments = fun () -> provider.GetDepartments()
            // The session port over the SQLite store when store is set, otherwise in memory. In memory, the sessions
            // and the record live as long as this AppEnv.
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
                    // The confirmation code and the salt come from the CSPRNG; the code MAC uses the launch key.
                    (Session.newCode System.Security.Cryptography.RandomNumberGenerator.GetInt32)
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes
                    (Session.codeMac launchKey)
                    (fun launch -> LaunchSeal.verify DateTime.UtcNow launchKey launch)
                    directory.idp
                    directory.registry
                    // The stub EHR data, completed by GenCORE with the loaded resources; the test patients are
                    // measured on the local date.
                    (StubEhrPatient.portAt today
                     |> Patient.completing today (patientDataResourcesOf provider))
                    // the resources GenCORE estimates a signed version again with when its age changed
                    (patientDataResourcesOf provider)
                    mail
                    // the credential store, seeded per stub login
                    (Session.initialState (
                        StubCredentials.seed System.Security.Cryptography.RandomNumberGenerator.GetBytes
                    ))
            logger = requestLogger
        }


    /// Makes an environment with its own launch key and stub directory, for tests and the MCP host. The server calls
    /// makeAppEnvWith instead, so its stub pages share the key and the directory.
    let makeAppEnv (provider: Informedica.GenForm.Lib.Resources.IResourceProvider) =
        // Demo unless GENPRES_PROD is 1.
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
