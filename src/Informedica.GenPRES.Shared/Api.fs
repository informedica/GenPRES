namespace Shared


module Api =


    open Types


    type Command =
        | OrderContextCmd of OrderContextCommand * OrderContext
        | OrderPlanCmd of OrderPlanCommand
        | FormularyCmd of Formulary
        | ParenteraliaCmd of Parenteralia
        | NutritionPlanCmd of NutritionPlanCommand
        | InteractionCmd of InteractionCommand

    and OrderContextCommand =
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

    and OrderPlanCommand =
        | UpdateOrderPlan of OrderPlan * (OrderContextCommand * OrderContext) option
        | FilterOrderPlan of OrderPlan

    and NutritionPlanCommand =
        | InitNutritionPlan of Patient
        | UpdateNutritionOrderContext of NutritionPlan * string * OrderContext
        | SelectNutritionOrderScenario of NutritionPlan * string * OrderContext
        | NavigateNutritionOrderContext of NutritionPlan * string * OrderContextCommand * OrderContext
        | AddNutritionContext of NutritionPlan * NutritionCategory
        | RemoveNutritionContext of NutritionPlan * string

    and InteractionCommand =
        | CheckInteractions of string list
        | GetDrugNames

    type Response =
        | OrderContextResp of OrderContextResponse
        | OrderPlanResp of OrderPlanResponse
        | FormularyResp of Formulary
        | ParenteraliaResp of Parenteralia
        | NutritionPlanResp of NutritionPlanResponse
        | InteractionResp of InteractionResponse

    and OrderContextResponse = OrderContextResult of OrderContext

    and OrderPlanResponse =
        | OrderPlanFiltered of OrderPlan
        | OrderPlanUpdated of OrderPlan

    and NutritionPlanResponse =
        | NutritionPlanInitialised of NutritionPlan
        | NutritionPlanUpdated of NutritionPlan

    and InteractionResponse =
        | InteractionsChecked of DrugInteraction[]
        | DrugNamesLoaded of string[]


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


    // until the families move to their own members, processCommand keeps its shape under
    // these names
    type Request = Request<Command>

    type Reply = Reply<Response>


    module Command =

        let toString =
            function
            | OrderContextCmd(UpdateOrderContext, _) -> "UpdateOrderContext"
            | OrderContextCmd(SelectOrderScenario, _) -> "SelectOrderScenario"
            | OrderContextCmd(UpdateOrderScenario, _) -> "UpdateOrderScenario"
            | OrderContextCmd(ResetOrderScenario, _) -> "ResetOrderScenario"
            | OrderContextCmd(DecreaseScheduleFrequencyProperty, _) -> "DecreaseScheduleFrequencyProperty"
            | OrderContextCmd(IncreaseScheduleFrequencyProperty, _) -> "IncreaseScheduleFrequencyProperty"
            | OrderContextCmd(SetMinScheduleFrequencyProperty, _) -> "SetMinScheduleFrequencyProperty"
            | OrderContextCmd(SetMaxScheduleFrequencyProperty, _) -> "SetMaxScheduleFrequencyProperty"
            | OrderContextCmd(SetMedianScheduleFrequencyProperty, _) -> "SetMedianScheduleFrequencyProperty"
            | OrderContextCmd(DecreaseOrderableDoseQuantityProperty(ntimes, useCalc), _) ->
                $"DecreaseOrderableDoseQuantityProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(IncreaseOrderableDoseQuantityProperty(ntimes, useCalc), _) ->
                $"IncreaseOrderableDoseQuantityProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(SetMinOrderableDoseQuantityProperty, _) -> "SetMinOrderableDoseQuantityProperty"
            | OrderContextCmd(SetMaxOrderableDoseQuantityProperty, _) -> "SetMaxOrderableDoseQuantityProperty"
            | OrderContextCmd(SetMedianOrderableDoseQuantityProperty, _) -> "SetMedianOrderableDoseQuantityProperty"
            | OrderContextCmd(DecreaseOrderableDoseRateProperty(ntimes, useCalc), _) ->
                $"DecreaseOrderableDoseRateProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(IncreaseOrderableDoseRateProperty(ntimes, useCalc), _) ->
                $"IncreaseOrderableDoseRateProperty ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(SetMinOrderableDoseRateProperty, _) -> "SetMinOrderableDoseRateProperty"
            | OrderContextCmd(SetMaxOrderableDoseRateProperty, _) -> "SetMaxOrderableDoseRateProperty"
            | OrderContextCmd(SetMedianOrderableDoseRateProperty, _) -> "SetMedianOrderableDoseRateProperty"
            | OrderContextCmd(DecreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc), _) ->
                $"DecreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(IncreaseComponentOrderableQuantityProperty(cmp, ntimes, useCalc), _) ->
                $"IncreaseComponentQuantityProperty cmp={cmp} ntimes={ntimes} useCalc={useCalc}"
            | OrderContextCmd(SetMinComponentOrderableQuantityProperty cmp, _) ->
                $"SetMinComponentQuantityProperty cmp={cmp}"
            | OrderContextCmd(SetMaxComponentOrderableQuantityProperty cmp, _) ->
                $"SetMaxComponentQuantityProperty cmp={cmp}"
            | OrderContextCmd(SetMedianComponentOrderableQuantityProperty cmp, _) ->
                $"SetMedianComponentQuantityProperty cmp={cmp}"

            | OrderPlanCmd(UpdateOrderPlan _) -> "UpdatedOrderPlan"
            | OrderPlanCmd(FilterOrderPlan _) -> "FilterOrderPlan"
            | FormularyCmd _ -> "FormularyCmd"
            | ParenteraliaCmd _ -> "ParenteraliaCmd"
            | NutritionPlanCmd(InitNutritionPlan _) -> "InitNutritionPlan"
            | NutritionPlanCmd(UpdateNutritionOrderContext _) -> "UpdateNutritionOrderContext"
            | NutritionPlanCmd(SelectNutritionOrderScenario _) -> "SelectNutritionOrderScenario"
            | NutritionPlanCmd(NavigateNutritionOrderContext _) -> "NavigateNutritionOrderContext"
            | NutritionPlanCmd(AddNutritionContext _) -> "AddNutritionContext"
            | NutritionPlanCmd(RemoveNutritionContext _) -> "RemoveNutritionContext"
            | InteractionCmd(CheckInteractions _) -> "CheckInteractions"
            | InteractionCmd GetDrugNames -> "GetDrugNames"


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
            processCommand: Request -> Async<Result<Reply, string[]>>
            processLaunch: LaunchCommand -> Async<LaunchOutcome>
            processSession: SessionCommand -> Async<SessionResponse>
            processSigning: SigningCommand -> Async<SigningResponse>
            // no envelope: an admin request has no OpenedToken to send and no notice to receive
            processAdmin: AdminCommand -> Async<Result<AdminResponse, string[]>>
            getSettings: unit -> Async<ServerSettings>
            testApi: unit -> Async<string>
        }
