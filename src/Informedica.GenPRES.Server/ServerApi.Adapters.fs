namespace ServerApi

open System
open Shared.Types


module PublicKey =

    open System.Text
    open System.Text.Json
    open System.Security.Cryptography


    let private base64Url (bytes: byte[]) =
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')


    let private sha256 (s: string) =
        s |> Encoding.UTF8.GetBytes |> SHA256.HashData


    /// The required members of a JWK per key type, in the lexicographic order RFC 7638
    /// section 3 prescribes. Empty for a key type the thumbprint does not cover.
    let private requiredMembers kty =
        match kty with
        | "EC" -> [ "crv"; "kty"; "x"; "y" ]
        | "RSA" -> [ "e"; "kty"; "n" ]
        | "OKP" -> [ "crv"; "kty"; "x" ]
        | _ -> []


    /// The string members RFC 7638 hashes, or None when the text is not such a JWK.
    let private jwkMembers (text: string) =
        try
            use doc = JsonDocument.Parse text
            let root = doc.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                None
            else
                let get (name: string) =
                    match root.TryGetProperty name with
                    | true, v when v.ValueKind = JsonValueKind.String -> Some(name, v.GetString())
                    | _ -> None

                match get "kty" with
                | None -> None
                | Some(_, kty) ->
                    let members = requiredMembers kty |> List.map get

                    if members.IsEmpty || members |> List.exists Option.isNone then
                        None
                    else
                        members |> List.choose id |> Some
        with :? JsonException ->
            None


    /// RFC 7638 thumbprint of a public JWK: SHA-256 over the required members serialised
    /// without whitespace in lexicographic order, base64url without padding. Text that is
    /// not a JWK of a covered key type gets the hash of the text itself, so the stub can
    /// correlate any key the client sends; the client computes the same value only for a
    /// real JWK.
    let thumbprint (PublicKey text) =
        match jwkMembers text with
        | Some members ->
            members
            |> List.map (fun (name, value) -> $"\"{name}\":{JsonSerializer.Serialize value}")
            |> String.concat ","
            |> fun body -> "{" + body + "}"
            |> sha256
            |> base64Url
        | None -> text |> sha256 |> base64Url


    /// A random, unguessable id for a session cookie: 256 bits from the CSPRNG, base64url.
    let randomId () =
        RandomNumberGenerator.GetBytes 32 |> base64Url


/// In-memory stand-in for launch steps 4 and 5 (plan 409 step 2). It never redirects: the
/// answer comes from the Launch text. The record it keeps per Launch is the LaunchRecord the
/// real server appends at step 4.2, with the Launch text standing in for the nonce.
module SessionStub =

    /// One record per Launch (Rules 2, 40, 45): the public key that presented it, the answer
    /// it got, and when both are forgotten (Rule 29).
    type LaunchRecord =
        {
            PublicKey: PublicKey
            Result: LaunchResult
            Expiry: DateTime
        }


    type State =
        {
            Launches: Map<string, LaunchRecord>
            Sessions: Map<string, SessionOpened>
        }


    let emptyState =
        {
            Launches = Map.empty
            Sessions = Map.empty
        }


    /// The Launch texts that refuse; anything else opens a Session.
    let refusalOf text =
        match text with
        | "expired" -> Some LaunchRefusal.LaunchExpired
        | "spent" -> Some LaunchRefusal.LaunchSpent
        | "invalid" -> Some LaunchRefusal.LaunchInvalid
        | "no-identity" -> Some LaunchRefusal.NoBrowserIdentity
        | "no-role" -> Some LaunchRefusal.NoRole
        | "wrong-patient" -> Some LaunchRefusal.WrongActivePatient
        | "enrolment" -> Some LaunchRefusal.EnrolmentRequired
        | _ -> None


    let stubUser =
        {
            UserId = "stub-prescriber"
            DisplayName = "Stub Prescriber"
            Role = UserRole.Prescriber
        }


    /// Answers a presentation and returns the state after it. Pure: the clock, the id source,
    /// the lifetime and the patient are parameters.
    ///
    /// - a Launch with a record: expired -> LaunchExpired and the record is dropped; the same
    ///   public key -> the recorded answer (Rule 2, same browser); another key -> LaunchSpent
    ///   (it cannot be told from another browser).
    /// - a Launch without a record: a refusal word refuses and records nothing, so a retry
    ///   re-verifies; anything else opens a Session and writes the record in the same act
    ///   (Rule 40).
    let present
        (now: DateTime)
        (newId: unit -> string)
        (lifetime: TimeSpan)
        (patient: Patient)
        (state: State)
        (Launch text, key)
        : State * LaunchResult
        =
        match state.Launches |> Map.tryFind text with
        | Some record when now > record.Expiry ->
            { state with Launches = state.Launches |> Map.remove text },
            LaunchResult.Refused LaunchRefusal.LaunchExpired
        | Some record when record.PublicKey = key -> state, record.Result
        | Some _ -> state, LaunchResult.Refused LaunchRefusal.LaunchSpent
        | None ->
            match refusalOf text with
            | Some refusal -> state, LaunchResult.Refused refusal
            | None ->
                let id = newId ()

                let session =
                    {
                        User = Some stubUser
                        PatientContext =
                            Some
                                {
                                    PatientId = "stub-patient"
                                    Patient = patient
                                }
                        OpenedToken = Some(OpenedToken $"opened-{id}")
                        KeyThumbprint = Some(PublicKey.thumbprint key)
                    }

                let result = LaunchResult.Opened(id, session)

                {
                    Launches =
                        state.Launches
                        |> Map.add
                            text
                            {
                                PublicKey = key
                                Result = result
                                Expiry = now + lifetime
                            }
                    Sessions = state.Sessions |> Map.add id session
                },
                result


    /// The port over a single mutable state guarded by a lock. `now` and `newId` are injected
    /// (ADR-0001, effects as parameters); production passes `DateTime.UtcNow` and a random id.
    let makeSessionPort (now: unit -> DateTime) (newId: unit -> string) (lifetime: TimeSpan) (patient: Patient) =
        let gate = obj ()
        let mutable state = emptyState

        let update f =
            lock
                gate
                (fun () ->
                    let next, result = f state
                    state <- next
                    result
                )

        {
            present = fun launch -> async { return update (fun s -> present (now ()) newId lifetime patient s launch) }
            find = fun id -> async { return lock gate (fun () -> state.Sessions |> Map.tryFind id) }
            close = fun id -> async { return update (fun s -> { s with Sessions = s.Sessions |> Map.remove id }, ()) }
        }


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
                fun ctxCmd ctx ->
                    async {
                        do! setComponentName "OrderContext" agent

                        return ctx |> OrderContextService.evaluate logger provider ctxCmd
                    }
        }


    let private makeOrderPlanPort
        agent
        (provider: Resources.IResourceProvider)
        (orderCtxPort: OrderContextPort)
        : OrderPlanPort
        =
        {
            updateOrderPlan =
                fun tp cmdOpt ->
                    async {
                        do! setComponentName "OrderPlan" agent

                        let! updated = OrderPlanService.updateOrderPlan orderCtxPort tp cmdOpt
                        let totals = provider.GetTotals()
                        return updated |> OrderPlanService.calculateTotals totals |> Ok
                    }

            filterOrderPlan =
                fun tp ->
                    async {
                        let totals = provider.GetTotals()
                        return tp |> OrderPlanService.calculateTotals totals |> Ok
                    }
        }


    let private makeNutritionPlanPort
        (orderCtxPort: OrderContextPort)
        logger
        (provider: Resources.IResourceProvider)
        : NutritionPlanPort
        =
        {
            initNutritionPlan =
                fun patient ->
                    async {
                        let totals = provider.GetTotals()
                        return NutritionPlanService.initNutritionPlan logger totals patient
                    }

            addNutritionContext =
                fun (plan, category) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.addNutritionContext totals orderCtxPort (plan, category)

            removeNutritionContext =
                fun (plan, id) ->
                    async {
                        let totals = provider.GetTotals()
                        return NutritionPlanService.removeNutritionContext totals (plan, id)
                    }

            updateNutritionOrderContext =
                fun (plan, label, ctx) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.updateNutritionOrderContext totals orderCtxPort (plan, label, ctx)

            selectNutritionOrderScenario =
                fun (plan, label, ctx) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.selectNutritionOrderScenario totals orderCtxPort (plan, label, ctx)

            navigateNutritionOrderContext =
                fun (plan, label, ctxCmd, ctx) ->
                    let totals = provider.GetTotals()
                    NutritionPlanService.navigateNutritionOrderContext totals orderCtxPort (plan, label, ctxCmd, ctx)
        }


    /// The session port of a server that does not launch: every Launch is refused as
    /// invalid and no session is ever found. Used in production until the scope switch (#580)
    /// decides what a production server exposes.
    let sessionDisabled: SessionPort =
        {
            present = fun _ -> async { return LaunchResult.Refused LaunchRefusal.LaunchInvalid }
            find = fun _ -> async { return None }
            close = fun _ -> async { return () }
        }


    let makeAppEnv (provider: Resources.IResourceProvider) : AppEnv =
        let agent, logger = resolveLogger ()
        let orderCtxPort = makeOrderContextPort agent logger provider

        {
            formulary = makeFormularyPort provider
            orderContext = orderCtxPort
            orderPlan = makeOrderPlanPort agent provider orderCtxPort
            nutritionPlan = makeNutritionPlanPort orderCtxPort logger provider
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
            logAnalyzer =
                {
                    listLogFiles =
                        fun () ->
                            async {
                                try
                                    return Ok(LogAnalyzer.listLogFiles ())
                                with ex ->
                                    return Error [| ex.Message |]
                            }
                    analyzeLogFile = fun fileName -> async { return LogAnalyzer.analyzeFile fileName }
                }
            requireLoaded =
                fun () ->
                    let info = provider.GetResourceInfo()

                    if info.IsLoaded then
                        None
                    else
                        info.Messages |> Array.map (fun msg -> FormLogging.formatMessage msg) |> Some
            // plan 409 step 2: an in-memory stub with a two-minute Launch lifetime (Rule 29);
            // its sessions live as long as this AppEnv
            session =
                SessionStub.makeSessionPort
                    (fun () -> DateTime.UtcNow)
                    PublicKey.randomId
                    (TimeSpan.FromMinutes 2.0)
                    Shared.Models.Patient.empty
        }
