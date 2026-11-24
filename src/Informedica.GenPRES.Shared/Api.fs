namespace Informedica.GenPRES.Shared


module Api =


    open Types


    /// The commands that change an order context, both for a single order context and for an order
    /// context in the plan. They select options and scenarios, set, step or recalculate values,
    /// reset the scenario and write the argumentation.
    [<RequireQualifiedAccess>]
    type OrderViewCommand =
        | ResetOrderScenario
        // Frequency property commands
        | DecreaseScheduleFrequencyProperty
        | IncreaseScheduleFrequencyProperty
        | SetMinScheduleFrequencyProperty
        | SetMaxScheduleFrequencyProperty
        | SetMedianScheduleFrequencyProperty
        // DoseQuantity property commands (ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseOrderableDoseQuantityProperty of ntimes: int * useCalc: bool
        | IncreaseOrderableDoseQuantityProperty of ntimes: int * useCalc: bool
        | SetMinOrderableDoseQuantityProperty
        | SetMaxOrderableDoseQuantityProperty
        | SetMedianOrderableDoseQuantityProperty
        /// Sets the dose quantity to a percentage of its range.
        | SetOrderableDoseQuantityPercProperty of perc: int
        // DoseRate property commands (ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseOrderableDoseRateProperty of ntimes: int * useCalc: bool
        | IncreaseOrderableDoseRateProperty of ntimes: int * useCalc: bool
        | SetMinOrderableDoseRateProperty
        | SetMaxOrderableDoseRateProperty
        | SetMedianOrderableDoseRateProperty
        // Component quantity property commands (cmp = component, ntimes = number of times to adjust,
        // useCalc = use calculated increment)
        | DecreaseComponentOrderableQuantityProperty of cmp: string * ntimes: int * useCalc: bool
        | IncreaseComponentOrderableQuantityProperty of cmp: string * ntimes: int * useCalc: bool
        | SetMinComponentOrderableQuantityProperty of cmp: string
        | SetMaxComponentOrderableQuantityProperty of cmp: string
        | SetMedianComponentOrderableQuantityProperty of cmp: string
        /// Sets a filter field to its nth option, counted from 0.
        | SetNthFilterProperty of field: Models.OrderContext.FilterField * nth: int
        /// Clears a filter field and the filter choices that come after it.
        | ClearFilterProperty of field: Models.OrderContext.FilterField
        /// Clears the whole filter.
        | ClearAllFilterProperty
        /// Sets the diluent to its nth option, counted from 0.
        | SetNthDiluentProperty of nth: int
        | ClearDiluentProperty
        /// Sets the components to the options at these positions, counted from 0.
        | SetNthComponentsProperty of nth: int[]
        /// Selects the nth scenario, counted from 0, and sets the filter form to its form.
        | SelectNthOrderScenario of nth: int
        /// Sets a schedule property to its nth value, counted from 0.
        | SetNthScheduleProperty of property: ScheduleProperty * nth: int
        /// Recalculates a schedule property.
        | ReCalcScheduleProperty of property: ScheduleProperty
        | SetNthOrderableProperty of property: OrderableProperty * nth: int
        | ReCalcOrderableProperty of property: OrderableProperty
        | SetNthComponentProperty of cmp: string * property: ComponentProperty * nth: int
        | ReCalcComponentProperty of cmp: string * property: ComponentProperty
        | SetNthItemProperty of cmp: string * itm: string * property: ItemProperty * nth: int
        | ReCalcItemProperty of cmp: string * itm: string * property: ItemProperty
        /// Sets the argumentation the user writes for a deviation from the rules.
        | SetArgumentationProperty of text: string
        /// Sets the filter choices by name from outside the filter, such as from the url or a
        /// medication list. The source decides how the choices are written.
        | SeedFilter of
            source: SeedSource *
            indication: string option *
            generic: string option *
            route: string option *
            form: string option *
            doseType: DoseType option

    /// A computing request: the command and the OpenedToken of the session. Opened is None when there
    /// is no session, the session is anonymous or the client has not yet received its first token.
    type Request<'cmd> =
        {
            Opened: OpenedToken option
            Command: 'cmd
        }


    /// A computing reply: the response and an optional notice for the session, either a newer
    /// version of the record or the end of the session.
    type Reply<'resp> =
        {
            Response: 'resp
            Notice: RecordNotice option
        }


    module OrderViewCommand =

        /// True when the command changes the filter choices or the scenario, so the answer carries the
        /// linked orders again. The server computes linked orders only for these commands. For any other
        /// command the client goes on using the linked orders of the previous answer.
        let offersLinked =
            function
            | OrderViewCommand.SetNthFilterProperty _
            | OrderViewCommand.ClearFilterProperty _
            | OrderViewCommand.ClearAllFilterProperty
            | OrderViewCommand.SelectNthOrderScenario _
            | OrderViewCommand.SeedFilter _ -> true
            | OrderViewCommand.ResetOrderScenario
            | OrderViewCommand.DecreaseScheduleFrequencyProperty
            | OrderViewCommand.IncreaseScheduleFrequencyProperty
            | OrderViewCommand.SetMinScheduleFrequencyProperty
            | OrderViewCommand.SetMaxScheduleFrequencyProperty
            | OrderViewCommand.SetMedianScheduleFrequencyProperty
            | OrderViewCommand.DecreaseOrderableDoseQuantityProperty _
            | OrderViewCommand.IncreaseOrderableDoseQuantityProperty _
            | OrderViewCommand.SetMinOrderableDoseQuantityProperty
            | OrderViewCommand.SetMaxOrderableDoseQuantityProperty
            | OrderViewCommand.SetMedianOrderableDoseQuantityProperty
            | OrderViewCommand.SetOrderableDoseQuantityPercProperty _
            | OrderViewCommand.DecreaseOrderableDoseRateProperty _
            | OrderViewCommand.IncreaseOrderableDoseRateProperty _
            | OrderViewCommand.SetMinOrderableDoseRateProperty
            | OrderViewCommand.SetMaxOrderableDoseRateProperty
            | OrderViewCommand.SetMedianOrderableDoseRateProperty
            | OrderViewCommand.DecreaseComponentOrderableQuantityProperty _
            | OrderViewCommand.IncreaseComponentOrderableQuantityProperty _
            | OrderViewCommand.SetMinComponentOrderableQuantityProperty _
            | OrderViewCommand.SetMaxComponentOrderableQuantityProperty _
            | OrderViewCommand.SetMedianComponentOrderableQuantityProperty _
            | OrderViewCommand.SetNthDiluentProperty _
            | OrderViewCommand.ClearDiluentProperty
            | OrderViewCommand.SetNthComponentsProperty _
            | OrderViewCommand.SetNthScheduleProperty _
            | OrderViewCommand.ReCalcScheduleProperty _
            | OrderViewCommand.SetNthOrderableProperty _
            | OrderViewCommand.ReCalcOrderableProperty _
            | OrderViewCommand.SetNthComponentProperty _
            | OrderViewCommand.ReCalcComponentProperty _
            | OrderViewCommand.SetNthItemProperty _
            | OrderViewCommand.ReCalcItemProperty _
            | OrderViewCommand.SetArgumentationProperty _ -> false


        /// Returns the command as text for the log, without the context.
        let toString (cmd: OrderViewCommand, _: OrderContext) =
            match cmd with
            | OrderViewCommand.ResetOrderScenario -> "ResetOrderScenario"
            | OrderViewCommand.DecreaseScheduleFrequencyProperty -> "DecreaseScheduleFrequencyProperty"
            | OrderViewCommand.IncreaseScheduleFrequencyProperty -> "IncreaseScheduleFrequencyProperty"
            | OrderViewCommand.SetMinScheduleFrequencyProperty -> "SetMinScheduleFrequencyProperty"
            | OrderViewCommand.SetMaxScheduleFrequencyProperty -> "SetMaxScheduleFrequencyProperty"
            | OrderViewCommand.SetMedianScheduleFrequencyProperty -> "SetMedianScheduleFrequencyProperty"
            | OrderViewCommand.DecreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
                $"DecreaseOrderableDoseQuantityProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderViewCommand.IncreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
                $"IncreaseOrderableDoseQuantityProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderViewCommand.SetMinOrderableDoseQuantityProperty -> "SetMinOrderableDoseQuantityProperty"
            | OrderViewCommand.SetMaxOrderableDoseQuantityProperty -> "SetMaxOrderableDoseQuantityProperty"
            | OrderViewCommand.SetMedianOrderableDoseQuantityProperty -> "SetMedianOrderableDoseQuantityProperty"
            | OrderViewCommand.SetOrderableDoseQuantityPercProperty perc ->
                $"SetOrderableDoseQuantityPercProperty perc=%i{perc}"
            | OrderViewCommand.DecreaseOrderableDoseRateProperty(ntimes, useCalc) ->
                $"DecreaseOrderableDoseRateProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderViewCommand.IncreaseOrderableDoseRateProperty(ntimes, useCalc) ->
                $"IncreaseOrderableDoseRateProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderViewCommand.SetMinOrderableDoseRateProperty -> "SetMinOrderableDoseRateProperty"
            | OrderViewCommand.SetMaxOrderableDoseRateProperty -> "SetMaxOrderableDoseRateProperty"
            | OrderViewCommand.SetMedianOrderableDoseRateProperty -> "SetMedianOrderableDoseRateProperty"
            | OrderViewCommand.DecreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc) ->
                $"DecreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes} useCalc={useCalc}"
            | OrderViewCommand.IncreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc) ->
                $"IncreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes} useCalc={useCalc}"
            | OrderViewCommand.SetMinComponentOrderableQuantityProperty cmp ->
                $"SetMinComponentQuantityProperty cmp={cmp}"
            | OrderViewCommand.SetMaxComponentOrderableQuantityProperty cmp ->
                $"SetMaxComponentQuantityProperty cmp={cmp}"
            | OrderViewCommand.SetMedianComponentOrderableQuantityProperty cmp ->
                $"SetMedianComponentQuantityProperty cmp={cmp}"
            | OrderViewCommand.SetNthFilterProperty(field, n) -> $"SetNthFilterProperty %A{field} nth=%i{n}"
            | OrderViewCommand.ClearFilterProperty field -> $"ClearFilterProperty %A{field}"
            | OrderViewCommand.ClearAllFilterProperty -> "ClearAllFilterProperty"
            | OrderViewCommand.SetNthDiluentProperty n -> $"SetNthDiluentProperty nth=%i{n}"
            | OrderViewCommand.ClearDiluentProperty -> "ClearDiluentProperty"
            | OrderViewCommand.SetNthComponentsProperty ns -> $"SetNthComponentsProperty nth=%A{ns}"
            | OrderViewCommand.SelectNthOrderScenario n -> $"SelectNthOrderScenario nth=%i{n}"
            | OrderViewCommand.SetNthScheduleProperty(prop, n) -> $"SetNthScheduleProperty %A{prop} nth=%i{n}"
            | OrderViewCommand.ReCalcScheduleProperty prop -> $"ReCalcScheduleProperty %A{prop}"
            | OrderViewCommand.SetNthOrderableProperty(prop, n) -> $"SetNthOrderableProperty %A{prop} nth=%i{n}"
            | OrderViewCommand.ReCalcOrderableProperty prop -> $"ReCalcOrderableProperty %A{prop}"
            | OrderViewCommand.SetNthComponentProperty(cmp, prop, n) ->
                $"SetNthComponentProperty cmp={cmp} %A{prop} nth=%i{n}"
            | OrderViewCommand.ReCalcComponentProperty(cmp, prop) -> $"ReCalcComponentProperty cmp={cmp} %A{prop}"
            | OrderViewCommand.SetNthItemProperty(cmp, itm, prop, n) ->
                $"SetNthItemProperty cmp={cmp} itm={itm} %A{prop} nth=%i{n}"
            | OrderViewCommand.ReCalcItemProperty(cmp, itm, prop) -> $"ReCalcItemProperty cmp={cmp} itm={itm} %A{prop}"
            // The argumentation text is clinical content and stays out of the log.
            | OrderViewCommand.SetArgumentationProperty _ -> "SetArgumentationProperty"
            | OrderViewCommand.SeedFilter(source, _, gen, _, _, _) -> $"SeedFilter %A{source} %A{gen}"


        module Ctx = Models.OrderContext


        /// Returns the context as the client expects it after the command, to show while the request runs.
        /// Returns the context unchanged for a step, a reset, a recalculation or a value list without
        /// values. Returns an error for an index past the options. The server answer replaces the preview.
        let preview (cmd: OrderViewCommand) (ctx: OrderContext) =
            let setNth target n =
                ctx |> Ctx.setNth target n |> Result.map (Option.defaultValue ctx)

            match cmd with
            | OrderViewCommand.SetNthFilterProperty(field, n) -> ctx |> Ctx.changeFilter field (Some n)
            | OrderViewCommand.ClearFilterProperty field -> ctx |> Ctx.changeFilter field None
            | OrderViewCommand.ClearAllFilterProperty -> Ok(Ctx.clearAll ctx)
            | OrderViewCommand.SetNthDiluentProperty n -> ctx |> Ctx.changeDiluent (Some n)
            | OrderViewCommand.ClearDiluentProperty -> ctx |> Ctx.changeDiluent None
            | OrderViewCommand.SetNthComponentsProperty ns -> ctx |> Ctx.setNthComponents ns
            | OrderViewCommand.SelectNthOrderScenario n -> ctx |> Ctx.selectNthScenario n
            | OrderViewCommand.SetNthScheduleProperty(prop, n) -> setNth (Ctx.Target.Schedule prop) n
            | OrderViewCommand.SetNthOrderableProperty(prop, n) -> setNth (Ctx.Target.Orderable prop) n
            | OrderViewCommand.SetNthComponentProperty(cmp, prop, n) -> setNth (Ctx.Target.Component(cmp, prop)) n
            | OrderViewCommand.SetNthItemProperty(cmp, itm, prop, n) -> setNth (Ctx.Target.Item(cmp, itm, prop)) n
            | OrderViewCommand.SetArgumentationProperty text -> Ok(ctx |> Ctx.Argumentation.write text)
            | OrderViewCommand.SeedFilter(source, ind, gen, rte, frm, dt) ->
                Ok(ctx |> Ctx.seedFilter source ind gen rte frm dt)
            | _ -> Ok ctx


    /// A command on an order context that is not in the plan. New patient data have no case: the client
    /// sends ClearAllFilterProperty on an empty context with the new patient data. The type is a union
    /// with one case so that every API function takes a named command type and a new command can be
    /// added as a case.
    [<RequireQualifiedAccess>]
    type OrderContextCommand =
        /// Runs an order view command on the order context sent with it.
        | Command of OrderViewCommand * OrderContext


    module OrderContextCommand =

        /// Returns the command as text for the log, without the context or the patient data.
        let toString (OrderContextCommand.Command(cmd, ctx)) = OrderViewCommand.toString (cmd, ctx)


    /// An edit of the patient data that the user makes on the patient panel. GenCORE applies it on the server.
    [<RequireQualifiedAccess>]
    type PatientDataEdit =
        /// The years of the age; None counts as 0 when the patient has an age.
        | SetYears of int option
        /// The months of the age; None counts as 0 when the patient has an age.
        | SetMonths of int option
        /// The weeks of the age; None counts as 0 when the patient has an age.
        | SetWeeks of int option
        /// The days of the age; None counts as 0 when the patient has an age.
        | SetDays of int option
        /// The weeks of the gestational age; None is the term of 37 weeks.
        | SetGestationalWeeks of int option
        /// The days of the gestational age; None is the days of the term, 0 days.
        | SetGestationalDays of int option
        /// The measured weight; None clears it, so the weight is estimated.
        | SetWeight of int<gram> option
        /// The measured height; None clears it, so the height is estimated.
        | SetHeight of int<cm> option
        /// The gender.
        | SetGender of Gender
        /// The renal function; None clears it.
        | SetRenalFunction of RenalFunction option
        /// The name of the department; None clears it.
        | SetDepartment of string option
        /// The location of the patient; None clears it.
        | SetLocation of string option
        /// Adds the access; the patient keeps it when the patient already has it.
        | AddAccess of Access
        /// Removes the access; nothing changes when the patient does not have it.
        | RemoveAccess of Access
        /// The mark of the user that the patient is an adult, for a patient who can be marked as one.
        | SetMarkedAdult of bool


    /// Sends the patient data edited on the panel. The server answers with the patient data
    /// completed with the estimates.
    [<RequireQualifiedAccess>]
    type PatientDataCommand =
        /// The patient data as edited on the panel.
        | ChangePatientData of patientData: PatientData
        /// An edit of the panel and the patient data it applies to.
        | EditPatientData of PatientDataEdit * patientData: PatientData


    module PatientDataCommand =

        /// Returns the command name for the log, without the patient data.
        let toString (cmd: PatientDataCommand) =
            match cmd with
            | PatientDataCommand.ChangePatientData _ -> "ChangePatientData"
            | PatientDataCommand.EditPatientData _ -> "EditPatientData"


    /// The launch commands. A launch command arrives without a session cookie, so it is a separate
    /// type from the session commands.
    [<RequireQualifiedAccess>]
    type LaunchCommand =
        // Idempotent per public key: a repeat from the same browser gets the same answer as the first.
        // Sets the session cookie when the answer is Opened.
        | PresentLaunch of Launch * PublicKey


    /// The session commands. They are authenticated by the session cookie.
    [<RequireQualifiedAccess>]
    type SessionCommand =
        // Returns the session of this browser, if any, after a reload or a return from the identity
        // provider.
        | GetSession
        // Closes the session with the token of the tab and removes the cookie. A token that is not the
        // token of the session closes nothing, so an old tab cannot close the session of another tab.
        | CloseSession of OpenedToken option
        // Sends the confirmation code from the mail and the chosen PIN for the enrolment that the
        // enrolment cookie names.
        | SupplyPin of code: string * pin: string
        // Opens the session on the named version, which allows signing again when it is the latest
        // version. Returns the session, or SessionResp None when there is no session, user or patient.
        | OpenVersion of id: string * OpenedToken option
        // Reads the EHR again and issues a new OpenedToken. Returns the same answer as OpenVersion.
        | Refresh of OpenedToken option


    [<RequireQualifiedAccess>]
    type SessionResponse =
        | SessionResp of SessionOpened option
        | SessionClosed
        // The server ended the session that the cookie names and deleted the cookie.
        | SessionEnded of SessionEnding
        // The launch waits for a PIN. GetSession returns this while the enrolment is open.
        | EnrolmentPending of EnrolmentPending
        | PinRefused of PinRefusal
        // The token of the command is not the token of the session. Nothing changed and the cookie
        // stays.
        | InactiveTab


    /// The signing commands. They are authenticated by the session cookie, like the session commands.
    [<RequireQualifiedAccess>]
    type SigningCommand =
        // Asks for a signing challenge for the plan as shown, with the OpenedToken of the session and
        // the token of the data notice the user accepted, if one was shown.
        | RequestSignChallenge of OrderPlan * OpenedToken * dataNotice: string option
        // Submits the signature: the challenge together with the PIN.
        | Submit of Submission


    module LaunchCommand =

        /// Returns the command name for the log, without the Launch, which is a secret, or the long key.
        let toString cmd =
            match cmd with
            | LaunchCommand.PresentLaunch _ -> "PresentLaunch"


    module SessionCommand =

        let toString cmd =
            match cmd with
            | SessionCommand.GetSession -> "GetSession"
            | SessionCommand.CloseSession _ -> "CloseSession"
            // The code and the PIN stay out of the log.
            | SessionCommand.SupplyPin _ -> "SupplyPin"
            | SessionCommand.OpenVersion _ -> "OpenVersion"
            | SessionCommand.Refresh _ -> "Refresh"


    module SigningCommand =

        /// Returns the command name for the log, without the plan, which is long, or the PIN.
        let toString cmd =
            match cmd with
            | SigningCommand.RequestSignChallenge _ -> "RequestSignChallenge"
            | SigningCommand.Submit _ -> "Submit"


    /// The admin commands. ValidatePassword returns a token and the other commands need that token.
    /// They need no cookie and work before the formulary is loaded, so a failed load can be retried
    /// from the settings page.
    [<RequireQualifiedAccess>]
    type AdminCommand =
        // Returns a token when the password matches the server password. Returns isValid false when
        // the server has no password.
        | ValidatePassword of password: string
        | ListLogFiles of token: string
        | AnalyzeLogFile of token: string * fileName: string
        // Reloads all the resources of the resource provider.
        | ReloadResources of token: string


    [<RequireQualifiedAccess>]
    type AdminResponse =
        // isValid false comes with an empty token
        | PasswordValidated of isValid: bool * token: string
        | LogFilesListed of LogFileInfo[]
        | LogFileAnalyzed of string
        | ResourcesReloaded


    module AdminCommand =

        /// Returns the command for the log, without the password or the token. The file name is logged
        /// because it is not a secret.
        let toString cmd =
            match cmd with
            | AdminCommand.ValidatePassword _ -> "ValidatePassword"
            | AdminCommand.ListLogFiles _ -> "ListLogFiles"
            | AdminCommand.AnalyzeLogFile(_, f) -> $"AnalyzeLogFile %s{f}"
            | AdminCommand.ReloadResources _ -> "ReloadResources"


    /// The interaction commands: check the interactions between the medications of a plan, or get the
    /// medication names of the interaction source.
    [<RequireQualifiedAccess>]
    type InteractionCommand =
        | CheckInteractions of string list
        // Reads the interaction source, not the formulary, so it works while the formulary is not loaded.
        | GetDrugNames


    /// The patient and the order contexts whose orders are counted in the totals; the plan is not changed.
    type TotalsQuery =
        {
            PatientData: PatientData
            OrderContexts: OrderContext[]
        }


    module TotalsQuery =

        /// Returns the number of contexts for the log, without the contexts.
        let toString (query: TotalsQuery) = $"TotalsQuery %i{query.OrderContexts.Length}"


    [<RequireQualifiedAccess>]
    type InteractionResponse =
        | InteractionsChecked of DrugInteraction[]
        | DrugNamesLoaded of string[]


    module InteractionCommand =

        /// Returns the command name for the log, without the medication names.
        let toString cmd =
            match cmd with
            | InteractionCommand.CheckInteractions _ -> "CheckInteractions"
            | InteractionCommand.GetDrugNames -> "GetDrugNames"


    /// The commands on the order plan, nutrition included.
    [<RequireQualifiedAccess>]
    type OrderPlanCommand =
        // Recalculates the plan for the patient data that the patient data command returned. Each
        // order context keeps the patient data it was evaluated for.
        | UpdatePatientData of PatientData * OrderPlan
        // Limits the rows to the contexts with these ids and recalculates the totals over them.
        | FilterRows of ids: string[] * OrderPlan
        // Evaluates an order view command on the plan context with this id, for the patient data of
        // that context, and updates the order of that context.
        | EvaluateContext of OrderPlan * contextId: string * OrderViewCommand * OrderContext
        // Adds an order context evaluated outside the plan, narrowed to one scenario, with its linked
        // orders. The server links each linked order to the context. When one of them is refused,
        // none of them enters the plan.
        | AddOrderContext of OrderPlan * OrderContext * linked: OrderContext[]
        // Removes the contexts with these ids, of every category, each with the contexts linked to it.
        | RemoveOrderContexts of OrderPlan * ids: string[]
        // Opens a signed version as it was, without evaluation: the contexts as given, with their
        // orders derived from them. Patient data without contexts give an empty plan.
        | Open of PatientData * OrderContext[]


    module OrderPlanCommand =

        /// Returns the command for the log, without the plan.
        let toString cmd =
            match cmd with
            | OrderPlanCommand.UpdatePatientData _ -> "UpdatePatientData"
            | OrderPlanCommand.FilterRows(ids, _) -> $"FilterRows %i{ids.Length}"
            | OrderPlanCommand.EvaluateContext(_, _, ctxCmd, _) -> $"EvaluateContext {ctxCmd}"
            | OrderPlanCommand.AddOrderContext _ -> "AddOrderContext"
            | OrderPlanCommand.RemoveOrderContexts(_, ids) -> $"RemoveOrderContexts %i{ids.Length}"
            | OrderPlanCommand.Open(_, contexts) -> $"Open %i{contexts.Length}"


    /// Returns the route of an API function, /api/typeName/method, for both server and client.
    let routerPaths typeName method = $"/api/%s{typeName}/%s{method}"


    /// What the client learns from the server at start-up: how the server was configured.
    type ServerSettings =
        {
            // GENPRES_LANG: the UI language until the url or the User chooses one
            Language: Localization.Locales
            // True when the server does not run in production (GENPRES_PROD): it uses demo data and the
            // title shows a demo suffix.
            IsDemo: bool
            // the departments to choose from: None for no department, then the departments the loaded rules
            // name; only None until the resources are loaded
            Departments: string option[]
            // the locations the loaded rules name; none until the resources are loaded
            Locations: string[]
            // GENPRES_LOCATION: the location a patient without a Location gets; outside production UMCU when unset,
            // in production None when unset
            DefaultLocation: string option
        }


    /// The protocol between client and server, one function per use case.
    /// See https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html.
    type IServerApi =
        {
            // Each computing function takes a Request and returns a Reply.
            // Returns the evaluated or the refused context. The Error result is only for failures.
            processOrderContext: Request<OrderContextCommand> -> Async<Result<Reply<OrderContextResponse>, string[]>>
            processFormulary: Request<Formulary> -> Async<Result<Reply<Formulary>, string[]>>
            processParenteralia: Request<Parenteralia> -> Async<Result<Reply<Parenteralia>, string[]>>
            // Takes the patient data edited on the panel and returns the completed patient data.
            processPatientData: Request<PatientDataCommand> -> Async<Result<Reply<PatientData>, string[]>>
            processInteraction: Request<InteractionCommand> -> Async<Result<Reply<InteractionResponse>, string[]>>
            // Runs a command on the order plan, nutrition included.
            processOrderPlan: Request<OrderPlanCommand> -> Async<Result<Reply<OrderPlan>, string[]>>
            // Returns the totals over the orders of the query, without changing the plan.
            processTotals: Request<TotalsQuery> -> Async<Result<Reply<Totals>, string[]>>
            processLaunch: LaunchCommand -> Async<LaunchOutcome>
            processSession: SessionCommand -> Async<SessionResponse>
            processSigning: SigningCommand -> Async<SigningResponse>
            // Takes no Request and returns no Reply: an admin request has no OpenedToken to send and no
            // notice to receive.
            processAdmin: AdminCommand -> Async<Result<AdminResponse, string[]>>
            getSettings: unit -> Async<ServerSettings>
            // Returns the medication rows of the emergency list for the patient, calculated from the rule set of the
            // kind EmergencyList.
            getEmergencyList: Request<PatientData> -> Async<Result<Reply<Intervention[]>, string[]>>
            testApi: unit -> Async<string>
        }
