namespace ServerApi


/// Handles a formulary request.
module FormularyCommand =

    open Informedica.GenPRES.Shared.Types

    /// Returns a short name for the log instead of the whole request.
    let toString (_: Formulary) = "Formulary"


    /// Returns the formulary. Returns the messages for invalid patient data. Without patient data
    /// the formulary is not filtered on a patient.
    let processCmd (env: AppEnv) (form: Formulary) =
        match Patient.patientOption form.PatientData with
        | Ok _ -> env.formulary.getFormulary form
        | Error errs -> async { return Error errs }


/// Handles a parenteralia request.
module ParenteraliaCommand =

    open Informedica.GenPRES.Shared.Types

    let toString (_: Parenteralia) = "Parenteralia"


    let processCmd (env: AppEnv) (par: Parenteralia) = env.formulary.getParenteralia par


/// Handles patient data edited on the panel and answers with the completed patient data.
module PatientDataCommand =

    open Informedica.GenPRES.Shared.Api


    /// Refuses patient data while the server loads, because GenCORE needs the resources to complete
    /// it.
    let gate (cmd: PatientDataCommand) =
        match cmd with
        | PatientDataCommand.ChangePatientData _
        | PatientDataCommand.EditPatientData _ -> Gate.RequiresLoaded


    /// Completes the patient data through GenCORE with the resources. A patient without a Location gets the
    /// default location first. For an edit command, GenCORE applies the edit first. Returns the GenCORE messages
    /// when a value is invalid.
    let processCmd (env: AppEnv) (cmd: PatientDataCommand) =
        async {
            match cmd with
            | PatientDataCommand.ChangePatientData pat ->
                return
                    pat
                    |> Patient.withDefaultLocation env.defaultLocation
                    |> Patient.complete (env.today ()) (env.patientDataResources ())
            | PatientDataCommand.EditPatientData(edit, pat) ->
                return
                    pat
                    |> Patient.withDefaultLocation env.defaultLocation
                    |> Patient.edit (env.today ()) (env.patientDataResources ()) edit
        }


/// Handles the interaction commands.
module InteractionCommand =

    open Informedica.GenPRES.Shared.Api


    /// Returns which commands need the loaded resources: only the interaction check.
    let gate =
        function
        | InteractionCommand.GetDrugNames -> Gate.Open
        | InteractionCommand.CheckInteractions _ -> Gate.RequiresLoaded


    let processCmd (env: AppEnv) (cmd: InteractionCommand) =
        match cmd with
        | InteractionCommand.GetDrugNames ->
            async {
                let! result = env.interaction.getDrugNames ()
                return result |> Result.map (List.toArray >> InteractionResponse.DrugNamesLoaded)
            }
        | InteractionCommand.CheckInteractions drugs ->
            async {
                let! result = env.interaction.checkInteractions drugs
                return result |> Result.map (List.toArray >> InteractionResponse.InteractionsChecked)
            }


/// Handles a command on an order plan.
module OrderPlanCommand =

    open FsToolkit.ErrorHandling
    open Informedica.GenOrder.Lib
    // Opened last, so an unqualified case name refers to the contract case.
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api

    /// Reads an order plan on the date today. Returns the messages for an argumentation that is
    /// too long or an invalid plan.
    let parsePlan today (plan: OrderPlan) : Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]> =
        plan.OrderContexts
        |> OrderContextService.Argumentation.checkAll
        |> Result.bind (fun () ->
            plan
            |> OrderPlanMapper.ofModel
            |> Informedica.GenOrder.Lib.OrderPlan.Dto.fromDto today
            |> Result.mapError (List.map OrderContextMapper.words >> List.toArray)
        )


    /// Reads the order contexts. Returns the messages of the first invalid one.
    let parseContexts today (contexts: OrderContext[]) : Result<PlanContext[], string[]> =
        contexts |> Array.traverseResultM (OrderContextService.parse today)


    let private both a b =
        match a, b with
        | Ok a, Ok b -> Ok(a, b)
        | Error e, _
        | _, Error e -> Error e


    /// Runs the port function on a parsed value and maps the result. Returns the parse error.
    let private answerWith map parsed run =
        match parsed with
        | Error e -> async { return Error e }
        | Ok v ->
            async {
                let! result = run v
                return result |> Result.map map
            }


    /// Runs the port function on a parsed value and maps the returned plan to the contract.
    let private answer (env: AppEnv) parsed run =
        answerWith (Informedica.GenOrder.Lib.OrderPlan.Dto.toDto >> OrderPlanMapper.toModel env.demo) parsed run


    /// Runs the port function on the plan when every patient of the plan passes the check.
    let overPlan (env: AppEnv) run (plan: OrderPlan) =
        Patient.overAll (Patient.ofPlan plan) (fun () -> answer env (parsePlan (env.today ()) plan) run)


    /// Recalculates the totals of the plan.
    let recalculate (env: AppEnv) (plan: OrderPlan) = plan |> overPlan env env.orderPlan.recalculate


    /// Runs a command on an order plan. Returns the messages for invalid patient data or an
    /// invalid plan.
    let processCmd (env: AppEnv) (cmd: OrderPlanCommand) =
        let today = env.today ()

        match cmd with
        // the server writes the patient and the rows; the client sends the plan as answered
        | OrderPlanCommand.UpdatePatientData(pat, plan) ->
            { plan with PatientData = pat } |> overPlan env env.orderPlan.followPatientData
        | OrderPlanCommand.FilterRows(ids, plan) -> recalculate env { plan with Filtered = ids }
        | OrderPlanCommand.EvaluateContext(plan, contextId, ctxCmd, ctx) ->
            match ctxCmd with
            // the argumentation is written by the server on the plan's context, without the domain
            | OrderViewCommand.SetArgumentationProperty text when
                plan.OrderContexts |> Array.exists (fun c -> c.Id = contextId)
                ->
                let ctx = ctx |> Informedica.GenPRES.Shared.Models.OrderContext.Argumentation.write text

                let written (c: OrderContext) =
                    if c.Id = contextId then
                        { c with Argumentation = ctx.Argumentation }
                    else
                        c
                recalculate env { plan with OrderContexts = Array.map written plan.OrderContexts }
            | OrderViewCommand.SetArgumentationProperty _ ->
                async { return Error [| OrderPlanMapper.words (OrderPlanError.NoSuchContext contextId) |] }
            | ctxCmd ->
                // a reset clears the argumentation, on the context and on the plan's copy of it, since
                // it puts the order back within the rules
                let plan, ctx =
                    match ctxCmd with
                    | OrderViewCommand.ResetOrderScenario ->
                        let clear (c: OrderContext) =
                            if c.Id = contextId then
                                { c with Argumentation = None }
                            else
                                c

                        { plan with OrderContexts = Array.map clear plan.OrderContexts },
                        { ctx with Argumentation = None }
                    | _ -> plan, ctx

                Patient.overAll
                    (Patient.ofPlan plan @ [ ctx.PatientData ])
                    (fun () ->
                        answer
                            env
                            (both (parsePlan today plan) (OrderContextService.parse today ctx))
                            (fun (p, pc) ->
                                env.orderPlan.evaluateContext
                                    p
                                    contextId
                                    (OrderContextMapper.Command.toDomain ctx.Category ctxCmd)
                                    pc
                            )
                    )
        | OrderPlanCommand.AddOrderContext(plan, ctx, linked) ->
            Patient.overAll
                (Patient.ofPlan plan @ ((ctx :: Array.toList linked) |> List.map _.PatientData))
                (fun () ->
                    answer
                        env
                        (both
                            (parsePlan today plan)
                            (both (OrderContextService.parse today ctx) (parseContexts today linked)))
                        (fun (p, (pc, pcs)) -> env.orderPlan.addOrderContexts p pc pcs)
                )
        | OrderPlanCommand.RemoveOrderContexts(plan, ids) ->
            Patient.overAll
                (Patient.ofPlan plan)
                (fun () -> answer env (parsePlan today plan) (fun p -> env.orderPlan.removeOrderContexts p ids))
        | OrderPlanCommand.Open(pat, contexts) ->
            Patient.overAll
                (pat :: (contexts |> Array.map _.PatientData |> Array.toList))
                (fun () ->
                    answer
                        env
                        (both (Patient.parse today pat) (parseContexts today contexts))
                        (fun (p, pcs) -> env.orderPlan.openWith p pcs)
                )


    /// The totals over the orders of the query, from the plan port. The patient of the query and of every
    /// context are checked first, as for a plan command.
    let processTotals (env: AppEnv) (query: TotalsQuery) =
        let today = env.today ()

        Patient.overAll
            (query.PatientData
             :: (query.OrderContexts |> Array.map _.PatientData |> Array.toList))
            (fun () ->
                answerWith
                    (Totals.Dto.toDto >> OrderContextMapper.totalsBack)
                    (both (Patient.parse today query.PatientData) (parseContexts today query.OrderContexts))
                    (fun (pat, pcs) -> env.orderPlan.totals pat pcs)
            )


module AdminCommand =

    open System
    open System.Security.Cryptography
    open System.Text
    open Informedica.GenPRES.Shared.Api
    open Informedica.Utils.Lib.BCL


    let tokenLifetime = TimeSpan.FromHours 1.0


    /// Treats a blank secret as no secret, so an empty password never matches an empty setting.
    let private nonBlank (secret: string option) = secret |> Option.filter String.notEmpty


    /// Compares the password with the secret in constant time. False without a secret.
    /// SECURITY: a length mismatch returns early; that leak is accepted because production
    /// requires at least 16 characters.
    let validatePassword (secret: string option) (password: string) =
        match nonBlank secret with
        | None -> false
        | Some expected ->
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes password, Encoding.UTF8.GetBytes expected)


    /// Returns the signing key for the secret and the server mode, so a demo token never verifies
    /// in production.
    let tokenKey (demo: bool) (secret: string) =
        let mode = if demo then "demo" else "production"
        Encoding.UTF8.GetBytes $"genpres-admin-token:%s{mode}:%s{secret}"


    /// Returns a signed token that expires, in the form base64(expiresAt:nonce).base64(hmac).
    /// Returns an empty string without a secret.
    let generateToken (demo: bool) (secret: string option) (now: DateTimeOffset) =
        match nonBlank secret with
        | None -> ""
        | Some secret ->
            let expiresAt = now.Add(tokenLifetime).ToUnixTimeSeconds()
            let nonce = RandomNumberGenerator.GetBytes 32 |> Convert.ToBase64String
            let payload = Encoding.UTF8.GetBytes $"%d{expiresAt}:%s{nonce}"
            use hmac = new HMACSHA256(tokenKey demo secret)
            let signature = hmac.ComputeHash payload
            $"%s{Convert.ToBase64String payload}.%s{Convert.ToBase64String signature}"


    /// Checks that a token was signed with the secret and the mode, and has not expired.
    let validateToken (demo: bool) (secret: string option) (now: DateTimeOffset) (token: string) =
        match nonBlank secret with
        | None -> false
        | Some secret ->
            if token |> String.isNullOrWhiteSpace then
                false
            else
                match token.Split '.' with
                | [| payload; signature |] ->
                    try
                        let payload = Convert.FromBase64String payload
                        let provided = Convert.FromBase64String signature
                        use hmac = new HMACSHA256(tokenKey demo secret)
                        let expected = hmac.ComputeHash payload

                        CryptographicOperations.FixedTimeEquals(provided, expected)
                        && (
                            match (Encoding.UTF8.GetString payload).Split(':', 2) with
                            | [| expiresAt; _ |] ->
                                match Int64.TryParse expiresAt with
                                | true, expiresAt -> now.ToUnixTimeSeconds() <= expiresAt
                                | false, _ -> false
                            | _ -> false
                        )
                    with _ ->
                        false
                | _ -> false


    /// Runs an admin command. A valid password returns a token; the other commands need the token.
    /// Works before the resources are loaded, so a reload can repair a failed load.
    let processCmd (env: AppEnv) (cmd: AdminCommand) : Async<Result<AdminResponse, string[]>> =
        let admin = env.admin

        let withToken token (run: unit -> Async<Result<AdminResponse, string[]>>) =
            if validateToken admin.demo (admin.secret ()) (admin.now ()) token then
                run ()
            else
                async { return Error [| "Invalid token" |] }

        match cmd with
        | AdminCommand.ValidatePassword password ->
            async {
                let secret = admin.secret ()

                return
                    if validatePassword secret password then
                        Ok(AdminResponse.PasswordValidated(true, generateToken admin.demo secret (admin.now ())))
                    else
                        Ok(AdminResponse.PasswordValidated(false, ""))
            }
        | AdminCommand.ListLogFiles token ->
            withToken
                token
                (fun () ->
                    async {
                        let! files = admin.listLogFiles ()
                        return files |> Result.map AdminResponse.LogFilesListed
                    }
                )
        | AdminCommand.AnalyzeLogFile(token, fileName) ->
            withToken
                token
                (fun () ->
                    async {
                        let! report = admin.analyzeLogFile fileName
                        return report |> Result.map AdminResponse.LogFileAnalyzed
                    }
                )
        | AdminCommand.ReloadResources token ->
            withToken
                token
                (fun () ->
                    async {
                        let! reloaded = admin.reloadResources ()
                        return reloaded |> Result.map (fun () -> AdminResponse.ResourcesReloaded)
                    }
                )


/// Handles a command on an order context.
module OrderContextCommand =

    open Informedica.GenOrder.Lib
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api

    /// Checks the patient data, reads the order context and runs the domain command on it.
    /// Without a command, returns the order context as read. Returns the messages for invalid
    /// patient data or an invalid order context.
    let evaluated (env: AppEnv) (viewCmd: OrderViewCommand) (ctx: OrderContext) domainCmd =
        Patient.over
            ctx.PatientData
            (fun () ->
                match ctx |> OrderContextService.parse (env.today ()), domainCmd with
                | Error errs, _ -> async { return Error errs }
                | Ok pc, None ->
                    async { return Ok(Evaluated pc) |> Result.map (OrderContextService.toResponse env.demo [||]) }
                | Ok pc, Some cmd ->
                    async {
                        let! answer = env.orderContext.evaluate cmd pc

                        match answer with
                        | Ok(Evaluated e) when viewCmd |> OrderViewCommand.offersLinked ->
                            let! offers = env.orderContext.linkedOrders e

                            // An error in the linked orders gives no linked orders, not an error.
                            return
                                Evaluated e
                                |> OrderContextService.toResponse env.demo (offers |> Result.defaultValue [||])
                                |> Ok
                        | _ -> return answer |> Result.map (OrderContextService.toResponse env.demo [||])
                    }
            )


    /// Maps the command to GenORDER and runs it. The server writes the argumentation itself.
    /// A reset clears the argumentation, because the order is back within the rules.
    let processViewCmd (env: AppEnv) (cmd: OrderViewCommand, ctx: OrderContext) =
        let ctx, domainCmd =
            match cmd with
            | OrderViewCommand.SetArgumentationProperty text ->
                ctx |> Informedica.GenPRES.Shared.Models.OrderContext.Argumentation.write text, None
            | OrderViewCommand.ResetOrderScenario ->
                { ctx with Argumentation = None }, Some(OrderContextMapper.Command.toDomain ctx.Category cmd)
            | cmd -> ctx, Some(OrderContextMapper.Command.toDomain ctx.Category cmd)

        evaluated env cmd ctx domainCmd


    /// Runs a command on an order context.
    let processCmd (env: AppEnv) (OrderContextCommand.Command(cmd, ctx)) = processViewCmd env (cmd, ctx)


/// Calculates the medication rows of the emergency list from the rule set of the kind EmergencyList.
module EmergencyListCommand =

    open Informedica.GenForm.Lib
    open Logging
    open Informedica.Logging.Lib
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api

    module GenOrderContext = Informedica.GenOrder.Lib.OrderContext
    module SharedContext = Informedica.GenPRES.Shared.Models.OrderContext
    module SharedDecimal = Informedica.GenPRES.Shared.Decimal
    module Intervention = Informedica.GenPRES.Shared.Models.Intervention


    /// Logs a warning that the context gives no rows, with the indication, the generic and the reason.
    let warnNoRows (env: AppEnv) (ctx: OrderContext) reason =
        let name = Option.defaultValue ""

        ServerLogging.Warning
            $"EmergencyList: %s{name ctx.Filter.Indication}, %s{name ctx.Filter.Generic} gives no rows: %s{reason}"
        |> Logging.logWarning env.logger


    /// Returns the options of the first open choice of the filter, in the order generic, route, dose type, form,
    /// each as the context with that option chosen. A choice is open when nothing is chosen and the list has more
    /// than one option. Empty when no choice is open.
    let branches (ctx: OrderContext) =
        let f = ctx.Filter
        let withFilter filter = { ctx with Filter = filter }

        match f with
        | {
              Generic = None
              Generics = gs
          } when gs.Length > 1 -> gs |> Array.map (fun g -> withFilter { f with Generic = Some g })
        | {
              Route = None
              Routes = rs
          } when rs.Length > 1 -> rs |> Array.map (fun r -> withFilter { f with Route = Some r })
        | {
              DoseType = None
              DoseTypes = ds
          } when ds.Length > 1 -> ds |> Array.map (fun d -> withFilter { f with DoseType = Some d })
        | {
              Form = None
              Forms = xs
          } when xs.Length > 1 -> xs |> Array.map (fun x -> withFilter { f with Form = Some x })
        | _ -> [||]


    /// Evaluates the context and returns the evaluated context with each of its scenarios. A context without
    /// scenarios is evaluated again for each option of the first open choice, at most four choices deep. A context
    /// that gives no scenarios is logged as a warning.
    let rec scenarios env depth (ctx: OrderContext) =
        async {
            // The view command is read only for the linked orders, and ResetOrderScenario offers none.
            match!
                OrderContextCommand.evaluated
                    env
                    OrderViewCommand.ResetOrderScenario
                    ctx
                    (Some GenOrderContext.UpdateOrderContext)
            with
            | Ok(OrderContextResponse.Evaluated(c, _)) when c.Scenarios |> Array.isEmpty |> not ->
                return c.Scenarios |> Array.map (fun sc -> c, sc)
            | Ok(OrderContextResponse.Evaluated(c, _)) ->
                match branches c with
                | [||] ->
                    warnNoRows env c "no scenarios and no open choice"
                    return [||]
                | _ when depth >= 4 ->
                    warnNoRows env c "no scenarios after four choices"
                    return [||]
                | bs ->
                    let! found = bs |> Array.map (scenarios env (depth + 1)) |> Async.Sequential
                    return found |> Array.concat
            | Ok(OrderContextResponse.Refused(c, refusal)) ->
                warnNoRows env c $"refused, %A{refusal}"
                return [||]
            | Error errs ->
                warnNoRows env ctx (errs |> String.concat "; ")
                return [||]
        }


    /// Returns the indication without the prefix, such as "Noodlijst ", or the indication when it does not start
    /// with the prefix.
    let categoryOf (prefix: string) (indication: string) =
        if indication.StartsWith prefix then
            indication.Substring prefix.Length
        else
            indication


    /// Formats the numbers of a value as Dutch numbers, separated by a semicolon.
    let numbers (vu: ValueUnit) =
        vu.Value
        |> Array.map (snd >> SharedDecimal.toStringNumberNLWithoutTrailingZeros)
        |> String.concat " ; "


    /// Formats an order variable: every value with the unit; without values, the minimum and the maximum, or the one
    /// limit it has, such as "min 1 mg" or "max 1 mg". Empty when the order variable has none of these.
    let variableText (ov: OrderVariable) =
        let v = ov.Variable

        match v.Vals, v.Min, v.Max with
        | Some vals, _, _ when vals.Value.Length > 0 -> $"%s{numbers vals} %s{vals.Unit}"
        | _, Some min, Some max when max.Unit <> "" -> $"%s{numbers min} - %s{numbers max} %s{max.Unit}"
        | _, Some min, None when min.Unit <> "" -> $"min %s{numbers min} %s{min.Unit}"
        | _, None, Some max when max.Unit <> "" -> $"max %s{numbers max} %s{max.Unit}"
        | _ -> ""


    /// Formats a quantity, followed by the quantity per kg in brackets when the order is per kg and the quantity
    /// per kg has a text.
    let withAdjust useAdjust qty adj =
        let q = qty |> variableText
        let a = adj |> variableText

        if useAdjust && a <> "" then $"%s{q} (%s{a})" else q


    /// Returns the context narrowed to the scenario, as the prescribe page narrows it: the form of the scenario and
    /// the scenario as the only scenario.
    let narrowed (ctx: OrderContext) (sc: OrderScenario) =
        { ctx with
            Filter = { ctx.Filter with Form = Some sc.Form }
            Scenarios = [| sc |]
        }


    /// Maps a scenario to a row of the emergency list. A scenario with a substance shows the substance; a scenario
    /// dosed by volume shows the orderable.
    let toIntervention (ctx: OrderContext, sc: OrderScenario) =
        let ord = sc.Order

        // The item of the scenario: the substance that is dosed, not an additional substance of the product.
        let item =
            ord.Orderable.Components
            |> Array.collect _.Items
            |> Array.tryFind (fun i -> Some i.Name = sc.Item)

        let calculated, preparation =
            match item with
            | Some i ->
                let qty = ord.Orderable.Dose.Quantity |> variableText
                let cnc = i.OrderableConcentration |> variableText

                withAdjust sc.UseAdjust i.Dose.Quantity i.Dose.QuantityAdjust,
                if qty = "" || cnc = "" then "" else $"%s{qty} van %s{cnc}"
            | None ->
                withAdjust sc.UseAdjust ord.Orderable.Dose.Quantity ord.Orderable.Dose.QuantityAdjust,
                $"%s{ord.Orderable.Dose.Quantity |> variableText} %s{sc.Name}"

        { Intervention.emptyIntervention with
            Category = categoryOf "Noodlijst " sc.Indication
            Name = ctx.Filter.Generic |> Option.defaultValue sc.Name
            SubstanceDoseText = calculated
            InterventionDoseText = preparation
            OrderContext = Some(narrowed ctx sc)
        }


    /// Returns the order context of a rule set row for the patient: the indication and the generic of the row.
    let contextOf demo pat (row: Types.Data.RuleSetData) =
        { SharedContext.empty with
            PatientData = pat
            DemoVersion = demo
            Filter =
                { SharedContext.filter with
                    Indication = Some row.Indication
                    Generic = if row.Generic = "" then None else Some row.Generic
                }
        }


    /// Returns the rows of the rule sets of the kind EmergencyList.
    let emergencyRows (sets: RuleSet[]) =
        sets
        |> Array.filter (fun s -> s.Kind = RuleSetKind.EmergencyList)
        |> Array.collect _.Rows


    /// Returns a short name for the log instead of the whole request.
    let toString (_: PatientData) = "EmergencyList"


    /// Returns the medication rows of the emergency list for the patient. The rows of the rule set are evaluated in
    /// chunks of Parallel.totalWorders: the chunks run in parallel, the rows in a chunk one after the other. Returns
    /// the messages when the patient data are invalid.
    let processCmd (env: AppEnv) (pat: PatientData) =
        async {
            match pat |> Patient.toCore (env.today ()) with
            | Error errs -> return Error errs
            | Ok core ->
                let! found =
                    core
                    |> env.ruleSets
                    |> emergencyRows
                    |> Array.map (contextOf env.demo pat >> scenarios env 0)
                    |> Array.chunkBySize Utils.Parallel.totalWorders
                    |> Array.map Async.Sequential
                    |> Async.Parallel

                return found |> Array.concat |> Array.concat |> Array.map toIntervention |> Ok
        }


module LaunchCommand =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api


    /// Presents a launch and writes the session id only to the cookie. A server exception is
    /// not caught, so the client gets a 500 and retries.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (stateCookie: LaunchStateCookie) (cmd: LaunchCommand) =
        async {
            match cmd with
            | LaunchCommand.PresentLaunch(launch, key) ->
                match! env.session.present (launch, key) with
                | LaunchResult.Opened(id, opened) ->
                    cookie.write id
                    return
                        LaunchOutcome.Opened(
                            SessionMapper.toOpened env.demo (env.patientDataResources ()) env.session.patientData opened
                        )
                | LaunchResult.RedirectTo(url, state) ->
                    stateCookie.write state
                    return LaunchOutcome.RedirectTo url
                | LaunchResult.Refused refusal -> return LaunchOutcome.Refused refusal
                // A retried launch that waits for a PIN: the next GetSession returns the enrolment.
                | LaunchResult.Enrolling _ -> return LaunchOutcome.RedirectTo Session.openedUrl
        }


    /// Handles the return from the identity provider and returns the redirect. Writes the session
    /// cookie when a session opens, or the enrolment cookie when a PIN is needed.
    let processCallback
        (env: AppEnv)
        (cookie: SessionCookie)
        (stateCookie: LaunchStateCookie)
        (enrolment: EnrolmentCookie)
        (cb: Callback)
        =
        async {
            match! env.session.callback { cb with StateCookie = stateCookie.read cb.State } with
            | CallbackResult.Opened(id, redirect) ->
                cookie.write id
                return redirect
            | CallbackResult.Enrolling(attempt, redirect, until) ->
                // Closes the previous session of this browser; its cookie would hide the enrolment.
                match cookie.read () with
                | Some id -> do! env.session.close id
                | None -> ()

                cookie.delete ()
                enrolment.write attempt until
                return redirect
            | CallbackResult.Refused(_, redirect)
            | CallbackResult.Superseded redirect -> return redirect
        }


module SessionCommand =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api


    /// Runs a session command for the session and enrolment cookies. GetSession returns the
    /// session, or else the enrolment, and deletes the cookie of an enrolment that no longer
    /// exists. SupplyPin uses only the enrolment in the cookie. CloseSession deletes both cookies,
    /// also when the close fails, unless another tab owns the session. A failed close is rethrown
    /// after the delete.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (enrolment: EnrolmentCookie) (cmd: SessionCommand) =
        // The resources are read only when an opened session is mapped, so a close does not wait for them.
        let toOpened opened =
            SessionMapper.toOpened env.demo (env.patientDataResources ()) env.session.patientData opened

        let reopened answer =
            match answer with
            | OnOwnToken.Done opened -> SessionResponse.SessionResp(opened |> Option.map toOpened)
            | OnOwnToken.InactiveTab -> SessionResponse.InactiveTab

        async {
            match cmd with
            | SessionCommand.GetSession ->
                let! bySession =
                    async {
                        match cookie.read () with
                        | None -> return None
                        | Some id ->
                            match! env.session.find id with
                            | SessionLookup.Found opened ->
                                return Some(SessionResponse.SessionResp(Some(toOpened opened)))
                            | SessionLookup.NotFound -> return None
                            | SessionLookup.Ended ending -> return Some(SessionResponse.SessionEnded ending)
                    }

                match bySession, enrolment.read () with
                | Some response, _ -> return response
                | None, None -> return SessionResponse.SessionResp None
                | None, Some attempt ->
                    match! env.session.findEnrolment attempt with
                    | Some pending -> return SessionResponse.EnrolmentPending pending
                    | None ->
                        enrolment.delete ()
                        return SessionResponse.SessionResp None
            | SessionCommand.SupplyPin(code, pin) ->
                match enrolment.read () with
                | None -> return SessionResponse.PinRefused PinRefusal.AttemptExpired
                | Some attempt ->
                    match! env.session.supplyPin attempt code pin with
                    | SupplyPinResult.Opened(id, opened) ->
                        enrolment.delete ()
                        cookie.write id
                        return SessionResponse.SessionResp(Some(toOpened opened))
                    | SupplyPinResult.Refused(PinRefusal.CodeVoid as refusal)
                    | SupplyPinResult.Refused(PinRefusal.AttemptExpired as refusal)
                    | SupplyPinResult.Refused(PinRefusal.WrongActivePatient as refusal) ->
                        enrolment.delete ()
                        return SessionResponse.PinRefused refusal
                    | SupplyPinResult.Refused refusal -> return SessionResponse.PinRefused refusal
            // Opens a version in the session of the cookie.
            | SessionCommand.OpenVersion(id, token) ->
                match cookie.read () with
                | None -> return SessionResponse.SessionResp None
                | Some sid ->
                    let! answer = env.session.openVersion sid id token
                    return reopened answer
            // Reads the EHR again and reopens the current version in the session of the cookie.
            | SessionCommand.Refresh token ->
                match cookie.read () with
                | None -> return SessionResponse.SessionResp None
                | Some sid ->
                    let! answer = env.session.refresh sid token
                    return reopened answer
            | SessionCommand.CloseSession token ->
                let clear () =
                    cookie.delete ()
                    enrolment.delete ()

                let! closed =
                    match cookie.read () with
                    | Some id -> env.session.closeTab id token
                    | None -> async { return OnOwnToken.Done() }
                    |> Async.Catch

                match closed with
                // Another tab owns the session.
                | Choice1Of2 OnOwnToken.InactiveTab -> return SessionResponse.InactiveTab
                | Choice1Of2(OnOwnToken.Done()) ->
                    try
                        match enrolment.read () with
                        | Some attempt -> do! env.session.dropEnrolment attempt
                        | None -> ()
                    finally
                        clear ()

                    return SessionResponse.SessionClosed
                | Choice2Of2 ex ->
                    clear ()
                    return raise ex
        }


/// Handles the signing of an order plan.
module SigningCommand =

    open Informedica.GenOrder.Lib
    // Opened last, so an unqualified case name refers to the contract case.
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api

    /// Maps a signing outcome to the response. The patient data gets the options of the resources.
    let toResponse (demo: bool) resources (outcome: SigningOutcome) =
        match outcome with
        | SigningOutcome.ChallengeIssued(nonce, moved) -> SigningResponse.ChallengeIssued(nonce, moved)
        | SigningOutcome.DataNotice(token, data) ->
            SigningResponse.DataNotice
                {
                    PatientData =
                        data
                        |> Option.map (ServerApi.Patient.ofPatientData >> ServerApi.Patient.withOptions resources)
                    Token = token
                }
        | SigningOutcome.Submitted(version, whom, token, patient) ->
            SigningResponse.Submitted(
                version |> SessionMapper.toSigned demo whom,
                token,
                patient
                |> ServerApi.Patient.ofPatientData
                |> ServerApi.Patient.withOptions resources
            )
        | SigningOutcome.Refused refusal -> SigningResponse.Refused refusal


    /// Runs a signing command for the session in the cookie. Refuses it without a session,
    /// with invalid patient data or with an invalid plan. The plan is not changed, because the
    /// challenge is a digest of it.
    let processCmd (env: AppEnv) (cookie: SessionCookie) (cmd: SigningCommand) =
        async {
            match cookie.read () with
            | None -> return SigningResponse.Refused SigningRefusal.NoSession
            | Some id ->
                let plan =
                    match cmd with
                    | SigningCommand.RequestSignChallenge(plan, _, _) -> plan
                    | SigningCommand.Submit submission -> submission.Plan

                if
                    ServerApi.Patient.ofPlan plan
                    |> List.exists (ServerApi.Patient.patient >> _.IsError)
                then
                    return SigningResponse.Refused SigningRefusal.NoPatient
                else
                    match plan |> OrderPlanCommand.parsePlan (env.today ()) with
                    | Error _ -> return SigningResponse.Refused SigningRefusal.PlanUnreadable
                    | Ok parsed ->
                        match cmd with
                        | SigningCommand.RequestSignChallenge(_, opened, notice) ->
                            let! outcome = env.session.challenge id (parsed, opened, notice)
                            return toResponse env.demo (env.patientDataResources ()) outcome
                        | SigningCommand.Submit submission ->
                            let! outcome =
                                env.session.submit
                                    id
                                    {
                                        Plan = parsed
                                        Opened = submission.Opened
                                        Challenge = submission.Challenge
                                        Pin = submission.Pin
                                        IdemKey = submission.IdemKey
                                    }

                            return toResponse env.demo (env.patientDataResources ()) outcome
        }
