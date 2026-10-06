namespace Shared


module Api =


    open Types


    /// The commands of the order view, which the order context being worked on and the plan both
    /// take: the selection, the reset, and the stepping of the frequency, the dose quantity, the
    /// dose rate and a component quantity.
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
        /// The dose quantity at a percentage of its range.
        | SetOrderableDoseQuantityPercProperty of perc: int
        // DoseRate property commands (ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseOrderableDoseRateProperty of ntimes: int * useCalc: bool
        | IncreaseOrderableDoseRateProperty of ntimes: int * useCalc: bool
        | SetMinOrderableDoseRateProperty
        | SetMaxOrderableDoseRateProperty
        | SetMedianOrderableDoseRateProperty
        // Component Quantity property commands (cmp = component, ntimes = number of times to adjust, useCalc = use calculated increment)
        | DecreaseComponentOrderableQuantityProperty of cmp: string * ntimes: int * useCalc: bool
        | IncreaseComponentOrderableQuantityProperty of cmp: string * ntimes: int * useCalc: bool
        | SetMinComponentOrderableQuantityProperty of cmp: string
        | SetMaxComponentOrderableQuantityProperty of cmp: string
        | SetMedianComponentOrderableQuantityProperty of cmp: string
        /// A filter field set to its nth option, counted from 0.
        | SetNthFilterProperty of field: Models.OrderContext.FilterField * nth: int
        /// A filter field emptied, with the choices below it.
        | ClearFilterProperty of field: Models.OrderContext.FilterField
        /// The whole filter emptied.
        | ClearAllFilterProperty
        /// The diluent set to its nth option, counted from 0.
        | SetNthDiluentProperty of nth: int
        | ClearDiluentProperty
        /// The components at these positions of the options, counted from 0.
        | SetNthComponentsProperty of nth: int[]
        /// The nth scenario, counted from 0, selected with its form.
        | SelectNthOrderScenario of nth: int
        /// A schedule value set to its nth value, counted from 0.
        | SetNthScheduleProperty of property: ScheduleProperty * nth: int
        /// A schedule value cleared, with the variables the user picked or stepped, in the order
        /// picked, named without the order id.
        | ClearScheduleProperty of property: ScheduleProperty * picks: string[]
        | SetNthOrderableProperty of property: OrderableProperty * nth: int
        | ClearOrderableProperty of property: OrderableProperty * picks: string[]
        | SetNthComponentProperty of cmp: string * property: ComponentProperty * nth: int
        | ClearComponentProperty of cmp: string * property: ComponentProperty * picks: string[]
        | SetNthItemProperty of cmp: string * itm: string * property: ItemProperty * nth: int
        | ClearItemProperty of cmp: string * itm: string * property: ItemProperty * picks: string[]
        /// The argumentation the user writes for a deviation from the rules.
        | SetArgumentationProperty of text: string
        /// The filter's choices set from outside its own fields, by name, with the rule of their
        /// source.
        | SeedFilter of
            source: SeedSource *
            indication: string option *
            generic: string option *
            route: string option *
            form: string option *
            doseType: DoseType option

    /// Every computing request: the command and the OpenedToken the Session holds.
    /// `None` where there is none to send: no Session, an anonymous one, or a client acting
    /// before its first token arrived.
    type Request<'cmd> =
        {
            Opened: OpenedToken option
            Command: 'cmd
        }


    /// Every computing reply: the answer, and what the Session is told with it (the record
    /// moved on, or the Session ended).
    type Reply<'resp> =
        {
            Response: 'resp
            Notice: RecordNotice option
        }


    module OrderViewCommand =

        /// For the log: the command alone, never the context.
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
            | OrderViewCommand.ClearScheduleProperty(prop, picks) ->
                $"ClearScheduleProperty %A{prop} %i{picks.Length} picks"
            | OrderViewCommand.SetNthOrderableProperty(prop, n) -> $"SetNthOrderableProperty %A{prop} nth=%i{n}"
            | OrderViewCommand.ClearOrderableProperty(prop, picks) ->
                $"ClearOrderableProperty %A{prop} %i{picks.Length} picks"
            | OrderViewCommand.SetNthComponentProperty(cmp, prop, n) ->
                $"SetNthComponentProperty cmp={cmp} %A{prop} nth=%i{n}"
            | OrderViewCommand.ClearComponentProperty(cmp, prop, picks) ->
                $"ClearComponentProperty cmp={cmp} %A{prop} %i{picks.Length} picks"
            | OrderViewCommand.SetNthItemProperty(cmp, itm, prop, n) ->
                $"SetNthItemProperty cmp={cmp} itm={itm} %A{prop} nth=%i{n}"
            | OrderViewCommand.ClearItemProperty(cmp, itm, prop, picks) ->
                $"ClearItemProperty cmp={cmp} itm={itm} %A{prop} %i{picks.Length} picks"
            // never the text: it is the clinician's and stays out of the log
            | OrderViewCommand.SetArgumentationProperty _ -> "SetArgumentationProperty"
            | OrderViewCommand.SeedFilter(source, _, gen, _, _, _) -> $"SeedFilter %A{source} %A{gen}"


        module Ctx = Models.OrderContext


        /// The client's preview of a command while its request is under way: the context as the
        /// command changes it; the context as it is when the command leaves it, as an index past the
        /// values does. The domain's answer replaces it.
        let preview (cmd: OrderViewCommand) (ctx: OrderContext) =
            let setNth target n =
                ctx |> Ctx.setNth target n |> Result.map (Option.defaultValue ctx)

            let clear target picks = ctx |> Ctx.clear target picks |> Result.map fst

            match cmd with
            | OrderViewCommand.SetNthFilterProperty(field, n) -> ctx |> Ctx.changeFilter field (Some n)
            | OrderViewCommand.ClearFilterProperty field -> ctx |> Ctx.changeFilter field None
            | OrderViewCommand.ClearAllFilterProperty -> Ok(Ctx.clearAll ctx)
            | OrderViewCommand.SetNthDiluentProperty n -> ctx |> Ctx.changeDiluent (Some n)
            | OrderViewCommand.ClearDiluentProperty -> ctx |> Ctx.changeDiluent None
            | OrderViewCommand.SetNthComponentsProperty ns -> ctx |> Ctx.setNthComponents ns
            | OrderViewCommand.SelectNthOrderScenario n -> ctx |> Ctx.selectNthScenario n
            | OrderViewCommand.SetNthScheduleProperty(prop, n) -> setNth (Ctx.Target.Schedule prop) n
            | OrderViewCommand.ClearScheduleProperty(prop, picks) -> clear (Ctx.Target.Schedule prop) picks
            | OrderViewCommand.SetNthOrderableProperty(prop, n) -> setNth (Ctx.Target.Orderable prop) n
            | OrderViewCommand.ClearOrderableProperty(prop, picks) -> clear (Ctx.Target.Orderable prop) picks
            | OrderViewCommand.SetNthComponentProperty(cmp, prop, n) -> setNth (Ctx.Target.Component(cmp, prop)) n
            | OrderViewCommand.ClearComponentProperty(cmp, prop, picks) -> clear (Ctx.Target.Component(cmp, prop)) picks
            | OrderViewCommand.SetNthItemProperty(cmp, itm, prop, n) -> setNth (Ctx.Target.Item(cmp, itm, prop)) n
            | OrderViewCommand.ClearItemProperty(cmp, itm, prop, picks) -> clear (Ctx.Target.Item(cmp, itm, prop)) picks
            | OrderViewCommand.SetArgumentationProperty text -> Ok(ctx |> Ctx.Argumentation.write text)
            | OrderViewCommand.SeedFilter(source, ind, gen, rte, frm, dt) ->
                Ok(ctx |> Ctx.seedFilter source ind gen rte frm dt)
            | _ -> Ok ctx


    /// The order context command family, for the order context still being worked on: a command
    /// of the order view over it, or the context evaluated for the patient updated. Only this context takes a patient update: a context in
    /// the plan keeps the patient it was evaluated for, so a plan cannot carry one.
    [<RequireQualifiedAccess>]
    type OrderContextCommand =
        /// A command of the order view over the context as held.
        | Command of OrderViewCommand * OrderContext
        /// The patient the patient command answered, with the context as held; the server writes
        /// the patient into it.
        | UpdatePatient of patient: Patient * OrderContext


    module OrderContextCommand =

        /// For the log: the command alone, never the context nor the patient.
        let toString (cmd: OrderContextCommand) =
            match cmd with
            | OrderContextCommand.Command(cmd, ctx) -> OrderViewCommand.toString (cmd, ctx)
            | OrderContextCommand.UpdatePatient _ -> "UpdatePatient"


    /// The patient command family: a patient change on the panel, answered with the patient made
    /// complete.
    [<RequireQualifiedAccess>]
    type PatientCommand =
        /// The patient as the panel changed it.
        | ChangePatient of patient: Patient


    module PatientCommand =

        /// For the log: the command alone, never the patient.
        let toString (cmd: PatientCommand) =
            match cmd with
            | PatientCommand.ChangePatient _ -> "ChangePatient"


    /// The launch command family. Cut from the session family at the authentication boundary:
    /// a launch command arrives without a cookie. Room to grow: the identity callback.
    [<RequireQualifiedAccess>]
    type LaunchCommand =
        // idempotent per public key: a repeat from the same browser is answered as the first
        // was; sets the session cookie on Opened
        | PresentLaunch of Launch * PublicKey


    /// The session command family, from the open Session on: always cookie-authenticated.
    [<RequireQualifiedAccess>]
    type SessionCommand =
        // the Session of this browser, if any (reload, IdP return)
        | GetSession
        // explicit close; always removes the cookie
        | CloseSession
        // the confirmation code from the mail and the chosen PIN, for the attempt the
        // enrolment cookie names
        | SupplyPin of code: string * pin: string
        // the version named becomes what the Session opened with, which lifts the block on
        // signing when it is the head; answered with the Session as it then is,
        // `SessionResp None` where there is no Session, no User or no Patient
        | OpenVersion of id: string
        // the EHR read again and the head reopened on it, with a fresh OpenedToken: the way out
        // of a held patient context that takes the EHR's data; answered as OpenVersion is
        | Refresh


    [<RequireQualifiedAccess>]
    type SessionResponse =
        | SessionResp of SessionOpened option
        | SessionClosed
        // the server ended the Session the cookie named, and deleted the cookie
        | SessionEnded of SessionEnding
        // the launch waits on a PIN; answered to GetSession while the attempt stands
        | EnrolmentPending of EnrolmentPending
        | PinRefused of PinRefusal


    /// The signing command family: always cookie-authenticated, like the session family.
    [<RequireQualifiedAccess>]
    type SigningCommand =
        // the plan as shown, the OpenedToken the Session holds, and the token of the data
        // notice the User accepted, if one was told
        | RequestSignChallenge of OrderPlan * OpenedToken * dataNotice: string option
        // the signature: the challenge comes back with the PIN
        | Submit of Submission


    module LaunchCommand =

        /// For the log. Never the Launch or the key: the Launch is a secret, the key is long.
        let toString cmd =
            match cmd with
            | LaunchCommand.PresentLaunch _ -> "PresentLaunch"


    module SessionCommand =

        let toString cmd =
            match cmd with
            | SessionCommand.GetSession -> "GetSession"
            | SessionCommand.CloseSession -> "CloseSession"
            // never the code or the PIN
            | SessionCommand.SupplyPin _ -> "SupplyPin"
            | SessionCommand.OpenVersion _ -> "OpenVersion"
            | SessionCommand.Refresh -> "Refresh"


    module SigningCommand =

        /// For the log. Never the plan (long) or the PIN.
        let toString cmd =
            match cmd with
            | SigningCommand.RequestSignChallenge _ -> "RequestSignChallenge"
            | SigningCommand.Submit _ -> "Submit"


    /// The admin command family: the password once, then the token it bought. Never
    /// cookie-authenticated, and never behind the formulary being loaded, so a failed load can
    /// be retried from the settings page.
    [<RequireQualifiedAccess>]
    type AdminCommand =
        // answered with a token when the password is the server's; with nothing when the
        // server has no password
        | ValidatePassword of password: string
        | ListLogFiles of token: string
        | AnalyzeLogFile of token: string * fileName: string
        // reloads everything the resource provider holds
        | ReloadResources of token: string


    [<RequireQualifiedAccess>]
    type AdminResponse =
        // isValid false comes with an empty token
        | PasswordValidated of isValid: bool * token: string
        | LogFilesListed of LogFileInfo[]
        | LogFileAnalyzed of string
        | ResourcesReloaded


    module AdminCommand =

        /// For the log. Never the password or the token; the file name is not a secret.
        let toString cmd =
            match cmd with
            | AdminCommand.ValidatePassword _ -> "ValidatePassword"
            | AdminCommand.ListLogFiles _ -> "ListLogFiles"
            | AdminCommand.AnalyzeLogFile(_, f) -> $"AnalyzeLogFile %s{f}"
            | AdminCommand.ReloadResources _ -> "ReloadResources"


    /// The interaction family: the drug names of the interaction source, and a check over the
    /// drugs of a plan.
    [<RequireQualifiedAccess>]
    type InteractionCommand =
        | CheckInteractions of string list
        // from the interaction source, not the formulary: served while the formulary is not loaded
        | GetDrugNames


    [<RequireQualifiedAccess>]
    type InteractionResponse =
        | InteractionsChecked of DrugInteraction[]
        | DrugNamesLoaded of string[]


    module InteractionCommand =

        /// For the log: never the drug list.
        let toString cmd =
            match cmd with
            | InteractionCommand.CheckInteractions _ -> "CheckInteractions"
            | InteractionCommand.GetDrugNames -> "GetDrugNames"


    /// The plan command family: the order plan, nutrition included.
    [<RequireQualifiedAccess>]
    type OrderPlanCommand =
        // the patient updated: the plan recalculated for the patient the patient command answered;
        // the patient of each context in it stays the one it was evaluated for
        | UpdatePatient of Patient * OrderPlan
        // the contexts the rows keep, by id: the totals recalculated over them
        | FilterRows of ids: string[] * OrderPlan
        // an order-context command evaluated over the context named, in that context's own
        // patient, its order following
        | Navigate of OrderPlan * contextId: string * OrderViewCommand * OrderContext
        // a workbench evaluated elsewhere, narrowed to one scenario, into the plan as it is
        | AddOrderContext of OrderPlan * OrderContext
        // a fresh workbench for a nutrition category, its filter discovered
        | NewOrderContext of OrderPlan * NutritionCategory
        // the contexts named removed, every kind; a feeding takes its supplements with it
        | RemoveOrderContexts of OrderPlan * ids: string[]
        // the signed version as it was, nothing evaluated: the contexts as given, their orders
        // derived; the patient with no contexts is the empty plan
        | Open of Patient * OrderContext[]


    module OrderPlanCommand =

        /// For the log: never the plan.
        let toString cmd =
            match cmd with
            | OrderPlanCommand.UpdatePatient _ -> "UpdatePatient"
            | OrderPlanCommand.FilterRows(ids, _) -> $"FilterRows %i{ids.Length}"
            | OrderPlanCommand.Navigate(_, _, ctxCmd, _) -> $"Navigate {ctxCmd}"
            | OrderPlanCommand.AddOrderContext _ -> "AddOrderContext"
            | OrderPlanCommand.NewOrderContext(_, category) -> $"NewOrderContext {category}"
            | OrderPlanCommand.RemoveOrderContexts(_, ids) -> $"RemoveOrderContexts %i{ids.Length}"
            | OrderPlanCommand.Open(_, contexts) -> $"Open %i{contexts.Length}"


    /// Defines how routes are generated on server and mapped from the client
    let routerPaths typeName method = $"/api/%s{typeName}/%s{method}"


    /// What the client learns from the server at start-up: how the server was configured.
    type ServerSettings =
        {
            // GENPRES_LANG: the UI language until the url or the User chooses one
            Language: Localization.Locales
            // not GENPRES_PROD: demo data, shown as the title suffix
            IsDemo: bool
            // the departments the loaded rules name; empty until the resources are loaded
            Departments: string[]
            // the department a patient without one is prescribed for, so the panel can say so
            DefaultDepartment: string
        }


    /// A type that specifies the communication protocol between client and server
    /// to learn more read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
    type IServerApi =
        {
            // one member per use case, each on the same envelope
            // the context evaluated, or refused with why; the error channel is for failures
            processOrderContext: Request<OrderContextCommand> -> Async<Result<Reply<OrderContextResponse>, string[]>>
            processFormulary: Request<Formulary> -> Async<Result<Reply<Formulary>, string[]>>
            processParenteralia: Request<Parenteralia> -> Async<Result<Reply<Parenteralia>, string[]>>
            // a patient change, answered with the patient made complete
            processPatient: Request<PatientCommand> -> Async<Result<Reply<Patient>, string[]>>
            processInteraction: Request<InteractionCommand> -> Async<Result<Reply<InteractionResponse>, string[]>>
            // the one plan, nutrition included
            processOrderPlan: Request<OrderPlanCommand> -> Async<Result<Reply<OrderPlan>, string[]>>
            processLaunch: LaunchCommand -> Async<LaunchOutcome>
            processSession: SessionCommand -> Async<SessionResponse>
            processSigning: SigningCommand -> Async<SigningResponse>
            // no envelope: an admin request has no OpenedToken to send and no notice to receive
            processAdmin: AdminCommand -> Async<Result<AdminResponse, string[]>>
            getSettings: unit -> Async<ServerSettings>
            testApi: unit -> Async<string>
        }
