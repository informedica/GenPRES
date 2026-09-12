namespace Shared


module Api =


    open Types


    /// The order-context command family: the selection, the reset, and the stepping of the
    /// frequency, the dose quantity, the dose rate and a component quantity.
    type OrderContextCommand =
        | UpdateOrderContext
        | SelectOrderScenario
        | UpdateOrderScenario
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


    module OrderContextCommand =

        /// For the log: the command alone, never the context.
        let toString (cmd: OrderContextCommand, _: OrderContext) =
            match cmd with
            | UpdateOrderContext -> "UpdateOrderContext"
            | SelectOrderScenario -> "SelectOrderScenario"
            | UpdateOrderScenario -> "UpdateOrderScenario"
            | ResetOrderScenario -> "ResetOrderScenario"
            | DecreaseScheduleFrequencyProperty -> "DecreaseScheduleFrequencyProperty"
            | IncreaseScheduleFrequencyProperty -> "IncreaseScheduleFrequencyProperty"
            | SetMinScheduleFrequencyProperty -> "SetMinScheduleFrequencyProperty"
            | SetMaxScheduleFrequencyProperty -> "SetMaxScheduleFrequencyProperty"
            | SetMedianScheduleFrequencyProperty -> "SetMedianScheduleFrequencyProperty"
            | DecreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
                $"DecreaseOrderableDoseQuantityProperty ntimes={ntimes} useCalc={useCalc}"
            | IncreaseOrderableDoseQuantityProperty(ntimes, useCalc) ->
                $"IncreaseOrderableDoseQuantityProperty ntimes={ntimes} useCalc={useCalc}"
            | SetMinOrderableDoseQuantityProperty -> "SetMinOrderableDoseQuantityProperty"
            | SetMaxOrderableDoseQuantityProperty -> "SetMaxOrderableDoseQuantityProperty"
            | SetMedianOrderableDoseQuantityProperty -> "SetMedianOrderableDoseQuantityProperty"
            | DecreaseOrderableDoseRateProperty(ntimes, useCalc) ->
                $"DecreaseOrderableDoseRateProperty ntimes={ntimes} useCalc={useCalc}"
            | IncreaseOrderableDoseRateProperty(ntimes, useCalc) ->
                $"IncreaseOrderableDoseRateProperty ntimes={ntimes} useCalc={useCalc}"
            | SetMinOrderableDoseRateProperty -> "SetMinOrderableDoseRateProperty"
            | SetMaxOrderableDoseRateProperty -> "SetMaxOrderableDoseRateProperty"
            | SetMedianOrderableDoseRateProperty -> "SetMedianOrderableDoseRateProperty"
            | DecreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc) ->
                $"DecreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes} useCalc={useCalc}"
            | IncreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc) ->
                $"IncreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes} useCalc={useCalc}"
            | SetMinComponentOrderableQuantityProperty cmp -> $"SetMinComponentQuantityProperty cmp={cmp}"
            | SetMaxComponentOrderableQuantityProperty cmp -> $"SetMaxComponentQuantityProperty cmp={cmp}"
            | SetMedianComponentOrderableQuantityProperty cmp -> $"SetMedianComponentQuantityProperty cmp={cmp}"


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
    type PlanCommand =
        // the plan as it is, totals recomputed over its orders
        | Recalculate of OrderPlan
        // an order-context command evaluated and folded into the plan: into the nutrition
        // context named, or into the selected scenario when None
        | Navigate of OrderPlan * contextId: string option * OrderContextCommand * OrderContext
        // a nutrition context for the category, its filter discovered
        | AddContext of OrderPlan * NutritionCategory
        // the nutrition context removed with its order; a feeding takes its supplements with it
        | RemoveContext of OrderPlan * contextId: string
        // the orders named removed, each with the workbench that contributed it
        | RemoveOrders of OrderPlan * ids: string[]


    module PlanCommand =

        /// For the log: never the plan.
        let toString cmd =
            match cmd with
            | PlanCommand.Recalculate _ -> "Recalculate"
            | PlanCommand.Navigate(_, None, ctxCmd, _) -> $"Navigate {ctxCmd}"
            | PlanCommand.Navigate(_, Some _, ctxCmd, _) -> $"Navigate context {ctxCmd}"
            | PlanCommand.AddContext(_, category) -> $"AddContext {category}"
            | PlanCommand.RemoveContext _ -> "RemoveContext"
            | PlanCommand.RemoveOrders(_, ids) -> $"RemoveOrders %i{ids.Length}"


    /// Defines how routes are generated on server and mapped from the client
    let routerPaths typeName method = $"/api/%s{typeName}/%s{method}"


    /// What the client learns from the server at start-up: how the server was configured.
    type ServerSettings =
        {
            // GENPRES_LANG: the UI language until the url or the User chooses one
            Language: Localization.Locales
            // not GENPRES_PROD: demo data, shown as the title suffix
            IsDemo: bool
        }


    /// A type that specifies the communication protocol between client and server
    /// to learn more read the docs at https://zaid-ajaj.github.io/Fable.Remoting/src/basics.html
    type IServerApi =
        {
            // one member per use case, each on the same envelope
            processOrderContext:
                Request<OrderContextCommand * OrderContext> -> Async<Result<Reply<OrderContext>, string[]>>
            processFormulary: Request<Formulary> -> Async<Result<Reply<Formulary>, string[]>>
            processParenteralia: Request<Parenteralia> -> Async<Result<Reply<Parenteralia>, string[]>>
            processInteraction: Request<InteractionCommand> -> Async<Result<Reply<InteractionResponse>, string[]>>
            // the one plan, nutrition included
            processOrderPlan: Request<PlanCommand> -> Async<Result<Reply<OrderPlan>, string[]>>
            processLaunch: LaunchCommand -> Async<LaunchOutcome>
            processSession: SessionCommand -> Async<SessionResponse>
            processSigning: SigningCommand -> Async<SigningResponse>
            // no envelope: an admin request has no OpenedToken to send and no notice to receive
            processAdmin: AdminCommand -> Async<Result<AdminResponse, string[]>>
            getSettings: unit -> Async<ServerSettings>
            testApi: unit -> Async<string>
        }
