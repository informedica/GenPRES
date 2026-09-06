module App

open System
open Fable.Core
open Browser
open Fable.React
open Elmish
open Feliz.Router
open Fable.Remoting.Client
open Utils
open Shared
open Shared.Types
open Shared.Models
open Global


module private Elmish =

    [<RequireQualifiedAccess>]
    type SessionContextMsg =
        | Launch of SessionLaunchToken
        | Redeem of SessionRedeemToken
        | Error of string
        | Redeemed of SessionContent * redirect: bool

    [<RequireQualifiedAccess>]
    type SessionContext =
        | Anonymous
        | Launching of SessionLaunchToken
        | Redeeming of SessionRedeemToken
        | Error of string
        | Content of Deferred<SessionContent>

    type State =
        {
            Page: Global.Pages
            Patient: Patient option
            NormalValues: Deferred<NormalValues>
            BolusMedication: Deferred<BolusMedication list>
            ContinuousMedication: Deferred<ContinuousMedication list>
            Products: Deferred<Product list>
            OrderContext: Deferred<OrderContext>
            OrderPlan: Deferred<OrderPlan>
            Interactions: Deferred<DrugInteraction[]>
            InteractionDrugNames: Deferred<string[]>
            DrugNameRetries: int
            NutritionPlan: Deferred<NutritionPlan>
            Formulary: Deferred<Formulary>
            Parenteralia: Deferred<Parenteralia>
            Localization: Deferred<string[][]>
            Hospitals: Deferred<string[]>
            Context: Context
            ShowDisclaimer: bool
            IsDemo: bool
            SnackbarMsg: string
            SnackbarOpen: bool
            SnackbarSeverity: string
            ServerStatus: Deferred<bool>
            ServerError: string option
            EmergencyListFilter: string[]
            ContinuousMedsFilter: string[]
            IsAuthenticated: bool
            AuthToken: string
            LogFiles: Deferred<LogFileInfo[]>
            LogAnalysisReport: Deferred<string>
            SessionContext: SessionContext
        }


    type Msg =
        | UrlChanged of string list
        | AcceptDisclaimer

        | UpdatePage of Global.Pages
        | UpdatePatient of Patient option

        | LoadNormalValues of AsyncOperationStatus<Result<NormalValues, string>>

        | LoadBolusMedication of AsyncOperationStatus<Result<BolusMedication list, string>>
        | LoadContinuousMedication of AsyncOperationStatus<Result<ContinuousMedication list, string>>
        | LoadProducts of AsyncOperationStatus<Result<Product list, string>>
        | OnSelectContinuousMedicationItem of string
        | OnSelectEmergencyListItem of string
        | UpdateEmergencyListFilter of string[]
        | UpdateContinuousMedsFilter of string[]

        | OrderContextMsg of Api.OrderContextCommand * OrderContext
        | LoadOrderContextResult of Api.OrderContextCommand * ApiResponse

        | OrderPlanMsg of Api.OrderPlanCommand
        | LoadOrderPlanResult of Api.OrderPlanCommand * ApiResponse

        | NutritionPlanMsg of Api.NutritionPlanCommand
        | LoadNutritionPlanResult of Api.NutritionPlanCommand * ApiResponse

        | UpdateFormulary of Formulary
        | LoadFormulary of ApiResponse

        | UpdateParenteralia of Parenteralia
        | LoadParenteralia of ApiResponse

        | CheckInteractions of string list
        | LoadInteractionsResult of ApiResponse
        | LoadInteractionDrugNames of ApiResponse

        | UpdateLanguage of Localization.Locales
        | LoadLocalization of AsyncOperationStatus<Result<string[][], string>>

        | UpdateHospital of string
        | CloseSnackbar
        | CheckServer of AsyncOperationStatus<Result<string, exn>>
        | DismissServerError

        | Login of password: string
        | LoadLoginResult of ApiResponse
        | Logout

        | ListLogFiles
        | LoadLogFilesResult of ApiResponse
        | AnalyzeLogFile of string
        | LoadLogAnalysisResult of ApiResponse

        | SessionContextMsg of SessionContextMsg


    and ApiResponse = AsyncOperationStatus<Result<Api.Response, string[]>>


    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder Api.routerPaths
        |> Remoting.buildProxy<Api.IServerApi>


    let checkServer =
        async {
            try
                let! result = serverApi.testApi ()
                return CheckServer(Finished(Ok result))
            with ex ->
                return CheckServer(Finished(Error ex))
        }
        |> Cmd.fromAsync


    let createApiMsg msg cmd =
        async {
            let! result = serverApi.processCommand cmd
            return Finished result |> msg
        }
        |> Cmd.fromAsync


    let processApiMsg (state: State) msg =
        match msg with
        | Api.OrderContextResp(Api.OrderContextResult ctx) -> { state with OrderContext = Resolved ctx }, Cmd.none
        | Api.OrderPlanResp(Api.OrderPlanFiltered tp)
        | Api.OrderPlanResp(Api.OrderPlanUpdated tp) ->
            let drugs = tp.Scenarios |> Array.map _.Name |> Array.distinct |> Array.toList

            let cmd =
                if drugs.Length >= 2 then
                    Cmd.ofMsg (CheckInteractions drugs)
                else
                    Cmd.none

            { state with OrderPlan = Resolved tp }, cmd
        | Api.FormularyResp form -> { state with Formulary = Resolved form }, Cmd.none
        | Api.ParenteraliaResp par -> { state with Parenteralia = Resolved par }, Cmd.none
        | Api.NutritionPlanResp(Api.NutritionPlanInitialised plan)
        | Api.NutritionPlanResp(Api.NutritionPlanUpdated plan) -> { state with NutritionPlan = Resolved plan }, Cmd.none
        | Api.InteractionResp(Api.InteractionsChecked interactions) ->
            let newState =
                if interactions.Length > 0 then
                    { state with
                        SnackbarMsg = $"Er zijn %i{interactions.Length} interactie(s) gevonden"
                        SnackbarOpen = true
                        SnackbarSeverity = "warning"
                    }
                else
                    { state with
                        SnackbarMsg = ""
                        SnackbarOpen = false
                    }

            { newState with Interactions = Resolved interactions }, Cmd.none
        | Api.InteractionResp(Api.DrugNamesLoaded names) ->
            { state with InteractionDrugNames = Resolved names }, Cmd.none
        | Api.LogAnalyzerResp(Api.PasswordValidated(isValid, token)) ->
            if isValid then
                { state with
                    IsAuthenticated = true
                    AuthToken = token
                },
                Cmd.none
            else
                { state with
                    IsAuthenticated = false
                    AuthToken = ""
                    SnackbarMsg = "Invalid password"
                    SnackbarOpen = true
                    SnackbarSeverity = "error"
                },
                Cmd.none
        | Api.LogAnalyzerResp(Api.LogFilesListed files) -> { state with LogFiles = Resolved files }, Cmd.none
        | Api.LogAnalyzerResp(Api.LogFileAnalyzed report) ->
            { state with LogAnalysisReport = Resolved report }, Cmd.none


    let loadOrderContext resp =
        Api.OrderContextCmd >> createApiMsg resp


    let loadOrderPlan resp = Api.OrderPlanCmd >> createApiMsg resp


    let loadFormuarly = Api.FormularyCmd >> createApiMsg LoadFormulary


    let loadParenteralia = Api.ParenteraliaCmd >> createApiMsg LoadParenteralia


    // url needs to be in format: http://localhost:8080/#patient?by=2&bm=0&bd=1
    // * pg : el (emergency list) cm (continuous medication) pr (prescribe)
    // * ad: age in days
    // * by: birth year
    // * bm: birth month
    // * bd: birth day
    // * wt: weight (gram)
    // * ht: height (cm)
    // * gw: gestational age weeks
    // * gd: gestational age days
    // * la: language (en; du; fr; ge; sp; it; ch)
    // * dc: show disclaimer (n;_)
    // * cv: central venous line (y;_)
    // * dp: department
    // * md: medication
    // * rt: route
    // * fr: form
    // * in: indication
    // * dt: dosetype
    let private tryParseInt key paramsMap =
        match Map.tryFind key paramsMap with
        | Some(Route.Int v) -> Some v
        | _ -> None

    let private parsePatientParams paramsMap =
        tryParseInt "wt" paramsMap,
        tryParseInt "ht" paramsMap,
        tryParseInt "gw" paramsMap |> Option.map Measures.toWeek,
        tryParseInt "gd" paramsMap |> Option.map Measures.toDay,
        Map.tryFind "dp" paramsMap

    let parsePatient (urlParts: string list) =
        urlParts
        |> List.pairwise
        |> List.tryFind (fun (a, b) -> a = "patient")
        |> function
            | Some(_, Route.Query queryParams) ->
                let paramsMap = Map.ofList queryParams

                let pat =
                    match Map.tryFind "by" paramsMap, Map.tryFind "ad" paramsMap with
                    | Some(Route.Int year), _ ->
                        // birthday year is required
                        let month =
                            match Map.tryFind "bm" paramsMap with
                            | Some(Route.Int months) -> months
                            | _ -> 1 // january is the default

                        let day =
                            match Map.tryFind "bd" paramsMap with
                            | Some(Route.Int days) -> days
                            | _ -> 1 // first day of the month is the default

                        let weight, height, gaWeeks, gaDays, dep = parsePatientParams paramsMap

                        let cvl =
                            match Map.tryFind "cv" paramsMap with
                            | Some s when s = "y" -> true
                            | _ -> false

                        let age = Patient.Age.fromBirthDate DateTime.Now (DateTime(year, month, day))

                        let patient =
                            Patient.create
                                (Some age.Years)
                                (Some age.Months)
                                (Some age.Weeks)
                                (Some age.Days)
                                weight
                                height
                                gaWeeks
                                gaDays
                                UnknownGender
                                [
                                    if cvl then
                                        CVL
                                ]
                                None
                                dep

                        patient
                    | _, Some(Route.Int days) ->
                        let weight, height, gaWeeks, gaDays, dep = parsePatientParams paramsMap

                        let cvl =
                            match Map.tryFind "cv" paramsMap with
                            | Some "y" -> [ CVL ]
                            | _ -> []

                        let age = Patient.Age.fromDays days

                        let patient =
                            Patient.create
                                (Some age.Years)
                                (Some age.Months)
                                (Some age.Weeks)
                                (Some age.Days)
                                weight
                                height
                                gaWeeks
                                gaDays
                                UnknownGender
                                cvl
                                None
                                dep

                        patient

                    | _ ->
                        Logging.warning "could not parse url to patient" (urlParts |> String.concat ";")
                        None

                let page =
                    match paramsMap |> Map.tryFind "pg" with
                    | Some s when s = "el" -> Some LifeSupport
                    | Some s when s = "cm" -> Some ContinuousMeds
                    | Some s when s = "pr" -> Some Prescribe
                    | Some s when s = "fm" -> Some Formulary
                    | Some s when s = "pe" -> Some Parenteralia
                    | _ -> None

                let lang =
                    match paramsMap |> Map.tryFind "la" with
                    | Some s when s = "en" -> Some Localization.English
                    | Some s when s = "du" -> Some Localization.Dutch
                    | Some s when s = "fr" -> Some Localization.French
                    | Some s when s = "gr" -> Some Localization.German
                    | Some s when s = "sp" -> Some Localization.Spanish
                    | Some s when s = "it" -> Some Localization.Italian
                    //                | Some s when s = "ch" -> Some Localization.Chinees // refact: to Chinese
                    | _ -> None

                let discl =
                    match paramsMap |> Map.tryFind "dc" with
                    | Some s when s = "n" -> false
                    | _ -> true

                let med =
                    {|
                        indication = paramsMap |> Map.tryFind "in"
                        medication = paramsMap |> Map.tryFind "md"
                        route = paramsMap |> Map.tryFind "rt"
                        form = paramsMap |> Map.tryFind "fr"
                        dosetype = paramsMap |> Map.tryFind "dt" |> Option.map DoseType.doseTypeFromString
                    |}
                    |> Some

                pat, page, lang, discl, med
            | _ -> None, None, None, true, None

    // expects url parts of the form "session?launch={token}"
    // parses it into SessionContext.Launched token if found,
    // otherwise SessionContext.Anonymous
    let parseSessionInfo (urlParts: string list) =
        let sessionPart part =
            urlParts
            |> List.pairwise
            |> List.tryFind (fun (a, b) -> a = "session")
            |> function
                | Some(_, Route.Query [ queryPart, value ]) when part = queryPart -> Some value
                | _ -> None

        match sessionPart "launch" with
        | Some token -> SessionContext.Launching(SessionLaunchToken token)
        | None ->
            match sessionPart "redeem" with
            | Some token -> SessionContext.Redeeming(SessionRedeemToken token)
            | None -> SessionContext.Anonymous

    let initialState
        pat
        page
        lang
        discl
        (med:
            {|
                indication: string option
                medication: string option
                route: string option
                form: string option
                dosetype: DoseType option
            |} option)
        sessionContext
        =
        {
            ShowDisclaimer = discl && sessionContext = SessionContext.Anonymous
            Page = page |> Option.defaultValue LifeSupport
            Patient = pat
            NormalValues = HasNotStartedYet
            BolusMedication = HasNotStartedYet
            ContinuousMedication = HasNotStartedYet
            Products = HasNotStartedYet
            OrderContext =
                match med with
                | None -> HasNotStartedYet
                | Some m ->
                    OrderContext.empty
                    |> OrderContext.setMedication m.indication m.medication m.route m.form m.dosetype
                    |> Resolved
            OrderPlan =
                match pat with
                | None -> HasNotStartedYet
                | Some p -> OrderPlan.create p [||] |> Resolved
            NutritionPlan = HasNotStartedYet
            Formulary = HasNotStartedYet
            Parenteralia = HasNotStartedYet
            Interactions = HasNotStartedYet
            InteractionDrugNames = HasNotStartedYet
            DrugNameRetries = 0
            Localization = HasNotStartedYet
            Hospitals = HasNotStartedYet
            Context =
                {
                    Localization = lang |> Option.defaultValue Localization.Dutch
                    Hospital = "UMCU"
                }
            IsDemo = false
            SnackbarMsg = ""
            SnackbarOpen = false
            SnackbarSeverity = "error"
            ServerStatus = HasNotStartedYet
            ServerError = None
            EmergencyListFilter = [||]
            ContinuousMedsFilter = [||]
            IsAuthenticated = false
            AuthToken = ""
            LogFiles = HasNotStartedYet
            LogAnalysisReport = HasNotStartedYet
            SessionContext = sessionContext
        }


    let init () : State * Cmd<Msg> =
        let currentUrl = Router.currentUrl ()
        let pat, page, lang, discl, med = parsePatient currentUrl
        let sessionInfo = parseSessionInfo currentUrl

        let cmds =
            Cmd.batch
                [
                    checkServer
                    Cmd.ofMsg (LoadNormalValues Started)
                    Cmd.ofMsg (LoadBolusMedication Started)
                    Cmd.ofMsg (LoadContinuousMedication Started)
                    Cmd.ofMsg (LoadProducts Started)
                    Cmd.ofMsg (LoadLocalization Started)
                    Cmd.ofMsg (LoadFormulary Started)
                    Cmd.ofMsg (LoadParenteralia Started)
                    Cmd.ofMsg (LoadInteractionDrugNames Started)
                    match sessionInfo with
                    | SessionContext.Launching token -> Cmd.ofMsg (SessionContextMsg(SessionContextMsg.Launch token))
                    | SessionContext.Redeeming token -> Cmd.ofMsg (SessionContextMsg(SessionContextMsg.Redeem token))
                    | _ -> Cmd.none
                ]

        initialState pat page lang discl med sessionInfo, cmds


    let applyNormalValues (normalValues: Deferred<NormalValues>) (pat: Patient option) =
        match normalValues, pat with
        | Resolved nv, Some p ->
            p
            |> Patient.applyNormalValues (Some nv.Weights) (Some nv.Heights) (Some nv.NeoWeights) (Some nv.NeoHeights)
            |> Some
        | _ -> pat


    module CommandHandlers =


        let handleOrderContext state cmd (ctx: OrderContext) =
            let ctx = { ctx with Patient = state.Patient |> Option.defaultValue ctx.Patient }

            let base' = { state with OrderContext = Resolved ctx }

            match cmd with
            | Api.UpdateOrderContext
            | Api.ReloadResources _ ->
                { base' with
                    Formulary = base'.Formulary |> Deferred.map (OrderContext.syncFilterToFormulary ctx.Filter)
                    Parenteralia =
                        base'.Parenteralia
                        |> Deferred.map (OrderContext.syncFilterToParenteralia ctx.Filter)
                },
                Cmd.batch
                    [
                        Cmd.ofMsg (LoadOrderContextResult(cmd, Started))
                        Cmd.ofMsg (LoadFormulary Started)
                        Cmd.ofMsg (LoadParenteralia Started)
                    ]
            | _ -> base', Cmd.ofMsg (LoadOrderContextResult(cmd, Started))


    let update (msg: Msg) (state: State) =
        let processOk = processApiMsg state

        let processError err (state, cmd) =
            let errMsg =
                err
                |> Array.truncate 3
                |> Array.map (fun (s: string) -> if s.Length > 200 then s[..199] + "..." else s)
                |> String.concat "; "

            Logging.error "error" err

            { state with
                SnackbarMsg = "Er ging iets mis, herladen"
                SnackbarOpen = true
                SnackbarSeverity = "error"
                ServerError = Some $"Server fout: {errMsg}"
            },
            cmd

        let selectMedicationItem generic indication route doseType state =
            let nonEmpty s = if s = "" then None else Some s

            let ctx =
                { OrderContext.empty with
                    OrderContext.Filter.Indication = indication |> nonEmpty
                    OrderContext.Filter.Generic = Some generic
                    OrderContext.Filter.Route = route |> nonEmpty
                    OrderContext.Filter.DoseType = doseType |> nonEmpty |> Option.map DoseType.doseTypeFromString
                }

            { state with
                Page = Prescribe
                OrderContext = ctx |> Resolved
            },
            Cmd.ofMsg (OrderContextMsg(Api.UpdateOrderContext, ctx))

        match msg with
        | CloseSnackbar ->
            { state with
                SnackbarMsg = ""
                SnackbarOpen = false
                SnackbarSeverity = "error"
            },
            Cmd.none

        | CheckServer Started -> { state with ServerStatus = InProgress }, checkServer

        | CheckServer(Finished(Ok _)) ->
            let cmd =
                match state.InteractionDrugNames with
                | HasNotStartedYet -> Cmd.ofMsg (LoadInteractionDrugNames Started)
                | _ -> Cmd.none

            { state with
                ServerStatus = Resolved true
                ServerError = None
            },
            cmd

        | CheckServer(Finished(Error err)) ->
            Logging.error "server niet bereikbaar" err

            { state with
                ServerStatus = Resolved false
                ServerError = Some "De server is niet bereikbaar. Controleer of de server is gestart."
            },
            async {
                do! Async.Sleep 5000
                return CheckServer Started
            }
            |> Cmd.fromAsync

        | DismissServerError -> { state with ServerError = None }, Cmd.none

        | Login password ->
            state,
            Api.LogAnalyzerCmd(Api.ValidatePassword password)
            |> createApiMsg LoadLoginResult

        | LoadLoginResult(Finished(Ok resp)) -> processOk resp

        | LoadLoginResult(Finished(Error err)) ->
            ({ state with
                IsAuthenticated = false
                AuthToken = ""
             },
             Cmd.none)
            |> processError err

        | LoadLoginResult Started -> state, Cmd.none

        | Logout ->
            { state with
                IsAuthenticated = false
                AuthToken = ""
                LogFiles = HasNotStartedYet
                LogAnalysisReport = HasNotStartedYet
                Page = if state.Page = Settings then LifeSupport else state.Page
            },
            Cmd.none

        | ListLogFiles ->
            { state with LogFiles = InProgress },
            Api.LogAnalyzerCmd(Api.ListLogFiles state.AuthToken)
            |> createApiMsg LoadLogFilesResult

        | LoadLogFilesResult(Finished(Ok resp)) -> processOk resp

        | LoadLogFilesResult(Finished(Error err)) ->
            ({ state with LogFiles = HasNotStartedYet }, Cmd.none) |> processError err

        | LoadLogFilesResult Started -> state, Cmd.none

        | AnalyzeLogFile fileName ->
            { state with LogAnalysisReport = InProgress },
            Api.LogAnalyzerCmd(Api.AnalyzeLogFile(state.AuthToken, fileName))
            |> createApiMsg LoadLogAnalysisResult

        | LoadLogAnalysisResult(Finished(Ok resp)) -> processOk resp

        | LoadLogAnalysisResult(Finished(Error err)) ->
            ({ state with LogAnalysisReport = HasNotStartedYet }, Cmd.none)
            |> processError err

        | LoadLogAnalysisResult Started -> state, Cmd.none

        | AcceptDisclaimer -> { state with ShowDisclaimer = false }, Cmd.none

        | UpdateLanguage lang ->
            { state with
                ShowDisclaimer = true
                State.Context.Localization = lang
            },
            Cmd.none

        | UpdateHospital hosp ->
            { state with
                ShowDisclaimer = true
                State.Context.Hospital = hosp
            },
            Cmd.none

        | UpdatePage page ->
            let retryDrugNames =
                match state.InteractionDrugNames with
                | Resolved _
                | InProgress -> Cmd.none
                | _ -> Cmd.ofMsg (LoadInteractionDrugNames Started)

            // make sure that the order context is not in use
            // i.e. the order context should be "fresh"
            if
                page = ContinuousMeds
                && state.OrderContext
                   |> Deferred.map (fun ctx -> ctx.Filter.Generic |> Option.isSome)
                   |> Deferred.defaultValue true
            then
                { state with
                    Page = page
                    OrderContext = HasNotStartedYet
                },
                Cmd.batch
                    [
                        Cmd.ofMsg (LoadOrderContextResult(Api.UpdateOrderContext, Started))
                        retryDrugNames
                    ]
            else if page = Settings && not state.IsAuthenticated then
                state, Cmd.none
            else if page = Settings then
                { state with Page = page }, retryDrugNames
            else
                let loadCmds =
                    match page with
                    | Formulary -> [ Cmd.ofMsg (LoadFormulary Started) ]
                    | Parenteralia -> [ Cmd.ofMsg (LoadParenteralia Started) ]
                    | _ -> []

                { state with Page = page }, Cmd.batch (retryDrugNames :: loadCmds)

        | UpdatePatient pat ->
            let pat = pat |> applyNormalValues state.NormalValues

            { state with
                Patient = pat
                OrderContext =
                    match pat with
                    | None -> HasNotStartedYet
                    | Some p ->
                        match state.OrderContext with
                        | Resolved ctx -> ctx
                        | _ -> OrderContext.empty
                        |> OrderContext.setPatient p
                        |> Resolved
                OrderPlan =
                    match pat with
                    | None -> HasNotStartedYet
                    | Some p ->
                        let tp = OrderPlan.create p [||]

                        state.OrderPlan
                        |> Deferred.map (fun tp -> { tp with Patient = p })
                        |> Deferred.defaultValue tp
                        |> Resolved
                NutritionPlan = HasNotStartedYet
                Formulary = { Formulary.empty with Patient = pat } |> Resolved
                Parenteralia = Parenteralia.empty |> Resolved
                EmergencyListFilter = [||]
                ContinuousMedsFilter = [||]
            },
            Cmd.batch
                [
                    Cmd.ofMsg (LoadOrderContextResult(Api.UpdateOrderContext, Started))
                    Cmd.ofMsg (
                        LoadOrderPlanResult(Api.UpdateOrderPlan(OrderPlan.create Patient.empty [||], None), Started)
                    )
                    Cmd.ofMsg (LoadFormulary Started)
                    Cmd.ofMsg (LoadParenteralia Started)
                ]

        | UrlChanged newUrl ->
            let pat, page, lang, discl, med = parsePatient newUrl
            let sessionInfo = parseSessionInfo newUrl

            { state with
                ShowDisclaimer = discl && sessionInfo = SessionContext.Anonymous
                Page = page |> Option.defaultValue LifeSupport
                Patient = pat
                OrderContext =
                    match med with
                    | None -> state.OrderContext
                    | Some m ->
                        match state.OrderContext with
                        | InProgress
                        | Recalculating _ -> state.OrderContext
                        | HasNotStartedYet ->
                            OrderContext.empty
                            |> OrderContext.setMedication m.indication m.medication m.route m.form m.dosetype
                            |> Resolved
                        | Resolved ctx ->
                            ctx
                            |> OrderContext.setMedication m.indication m.medication m.route m.form m.dosetype
                            |> Resolved
                // State. prefix needed: disambiguates State.Context field from Global.Context type
                State.Context.Localization = lang |> Option.defaultValue Localization.English
            },
            Cmd.batch
                [
                    Cmd.ofMsg (UpdatePatient pat)
                    match sessionInfo with
                    | SessionContext.Launching token -> Cmd.ofMsg (SessionContextMsg(SessionContextMsg.Launch token))
                    | SessionContext.Redeeming token -> Cmd.ofMsg (SessionContextMsg(SessionContextMsg.Redeem token))
                    | _ -> Cmd.none
                ]

        | LoadLocalization Started ->
            { state with Localization = InProgress }, Cmd.fromAsync (GoogleDocs.loadLocalization LoadLocalization)

        | LoadLocalization(Finished(Ok terms)) ->

            { state with Localization = terms |> Resolved }, Cmd.none

        | LoadLocalization(Finished(Error s)) ->
            Logging.error "cannot load localization" s
            state, Cmd.none

        | LoadNormalValues Started ->
            { state with NormalValues = InProgress }, Cmd.fromAsync (GoogleDocs.loadNormalValues LoadNormalValues)

        | LoadNormalValues(Finished(Ok normalValues)) ->
            { state with NormalValues = normalValues |> Resolved }, Cmd.ofMsg (UpdatePatient state.Patient)

        | LoadNormalValues(Finished(Error s)) ->
            Logging.error "cannot load normal values" s
            state, Cmd.none


        | LoadBolusMedication Started ->
            { state with BolusMedication = InProgress },
            Cmd.fromAsync (GoogleDocs.loadBolusMedication LoadBolusMedication)

        | LoadBolusMedication(Finished(Ok meds)) ->
            { state with
                BolusMedication = meds |> Resolved
                Hospitals =
                    meds
                    |> List.map _.Hospital
                    |> List.distinct
                    |> List.filter (String.isNullOrWhiteSpace >> not)
                    |> List.toArray
                    |> Resolved
            },
            Cmd.none

        | LoadBolusMedication(Finished(Error s)) ->
            Logging.error "cannot load emergency treatment" s
            state, Cmd.none

        | LoadContinuousMedication Started ->
            { state with ContinuousMedication = InProgress },
            Cmd.fromAsync (GoogleDocs.loadContinuousMedication LoadContinuousMedication)

        | LoadContinuousMedication(Finished(Ok meds)) ->

            { state with ContinuousMedication = meds |> Resolved }, Cmd.none

        | LoadContinuousMedication(Finished(Error s)) ->
            Logging.error "cannot load continuous medication" s
            state, Cmd.none

        | OnSelectContinuousMedicationItem item ->
            match state.ContinuousMedication with
            | Resolved meds ->
                meds
                |> List.tryFind (fun m -> item.EndsWith($".{m.Medication}"))
                |> Option.map (fun m -> selectMedicationItem m.Generic m.Indication "INTRAVENEUS" m.DoseType state)
                |> Option.defaultWith (fun () ->
                    Logging.warning $"could not find continuous medication with item: {item}" item
                    state, Cmd.none
                )
            | _ -> state, Cmd.none

        | UpdateEmergencyListFilter filter -> { state with EmergencyListFilter = filter }, Cmd.none

        | UpdateContinuousMedsFilter filter -> { state with ContinuousMedsFilter = filter }, Cmd.none

        | OnSelectEmergencyListItem item ->
            match state.BolusMedication with
            | Resolved meds ->
                meds
                |> List.tryFind (fun m -> item.EndsWith($".{m.Hospital}.{m.Category}.{m.Generic}"))
                |> Option.map (fun m ->
                    let generic =
                        if m.TemplateGeneric = "" then
                            m.Generic
                        else
                            m.TemplateGeneric

                    selectMedicationItem generic m.TemplateIndication m.TemplateRoute m.TemplateDoseType state
                )
                |> Option.defaultValue (state, Cmd.none)
            | _ -> state, Cmd.none

        | LoadProducts Started ->
            { state with Products = InProgress }, Cmd.fromAsync (GoogleDocs.loadProducts LoadProducts)

        | LoadProducts(Finished(Ok prods)) ->

            { state with Products = prods |> Resolved }, Cmd.none

        | LoadProducts(Finished(Error s)) ->
            Logging.error "cannot load products" s
            state, Cmd.none

        | OrderContextMsg(ctxCmd, ctx) -> ctx |> CommandHandlers.handleOrderContext state ctxCmd

        | LoadOrderContextResult(cmd, Started) ->
            match state.Patient with
            | None ->
                match cmd with
                | Api.ReloadResources pw ->
                    { state with OrderContext = HasNotStartedYet },
                    (Api.ReloadResources pw, OrderContext.empty)
                    |> loadOrderContext (fun resp -> LoadOrderContextResult(cmd, resp))
                | _ -> { state with OrderContext = HasNotStartedYet }, Cmd.none
            | Some pat ->
                match state.OrderContext with
                | InProgress
                | Recalculating _ -> state, Cmd.none
                | HasNotStartedYet ->
                    { state with OrderContext = InProgress },
                    (cmd, OrderContext.empty |> OrderContext.setPatient pat)
                    |> loadOrderContext (fun resp -> LoadOrderContextResult(cmd, resp))
                | Resolved ctx ->
                    { state with OrderContext = Recalculating ctx },
                    (cmd, { ctx with Patient = pat })
                    |> loadOrderContext (fun resp -> LoadOrderContextResult(cmd, resp))

        | LoadOrderContextResult(_, Finished(Ok msg)) -> msg |> processOk
        | LoadOrderContextResult(_, Finished(Error err)) ->
            Logging.warning "order context error, resetting" err

            let isNoRulesError = err |> Array.exists _.ToLower().Contains("geen doseerregels")

            { state with
                OrderContext = HasNotStartedYet
                Page = if isNoRulesError then LifeSupport else state.Page
                SnackbarMsg = err |> Array.tryHead |> Option.defaultValue "Er ging iets mis"
                SnackbarOpen = true
                SnackbarSeverity = "warning"
            },
            if isNoRulesError then
                Cmd.ofMsg (OrderContextMsg(Api.UpdateOrderContext, OrderContext.empty))
            else
                Cmd.none


        | OrderPlanMsg tpCmd ->
            match tpCmd with
            | Api.UpdateOrderPlan(tp, Some(ctxCmd, ctx)) ->
                match state.OrderPlan with
                | InProgress
                | Recalculating _ -> state, Cmd.none
                | _ ->
                    { state with OrderPlan = Recalculating tp },
                    Api.OrderPlanCmd(Api.UpdateOrderPlan(tp, Some(ctxCmd, ctx)))
                    |> createApiMsg (fun resp -> LoadOrderPlanResult(tpCmd, resp))
            | Api.UpdateOrderPlan(tp, None) ->
                let onlySetOrderContext =
                    state.OrderPlan
                    |> Deferred.map (fun st -> st.Selected.IsNone && tp.Selected.IsSome)
                    |> Deferred.defaultValue false

                let tpState =
                    match state.OrderPlan with
                    | Recalculating _ -> Recalculating tp
                    | _ -> Resolved tp

                // CheckInteractions is dispatched by processApiMsg when the API response
                // arrives, so we don't duplicate it here.
                let cmd =
                    if state.Page = OrderPlan then
                        match state.OrderPlan with
                        | Recalculating _ -> Cmd.none
                        | _ ->
                            if onlySetOrderContext then
                                Cmd.none
                            else
                                Cmd.ofMsg (LoadOrderPlanResult(tpCmd, Started))
                    else
                        Cmd.batch
                            [
                                Cmd.ofMsg (OrderContextMsg(Api.UpdateOrderContext, OrderContext.empty))
                                Cmd.ofMsg (LoadOrderPlanResult(tpCmd, Started))
                            ]

                { state with
                    Page = OrderPlan
                    OrderPlan = tpState
                },
                cmd
            | Api.FilterOrderPlan tp ->
                { state with OrderPlan = Resolved tp }, Cmd.ofMsg (LoadOrderPlanResult(tpCmd, Started))

        | LoadOrderPlanResult(cmd, Started) ->
            match state.Patient with
            | None -> { state with OrderPlan = HasNotStartedYet }, Cmd.none
            | Some pat ->
                match state.OrderPlan with
                | InProgress
                | Recalculating _ -> state, Cmd.none
                | HasNotStartedYet ->
                    let apiCmd =
                        match cmd with
                        | Api.FilterOrderPlan _ -> Api.FilterOrderPlan(OrderPlan.create pat [||])
                        | Api.UpdateOrderPlan(_, ctxOpt) -> Api.UpdateOrderPlan(OrderPlan.create pat [||], ctxOpt)

                    { state with OrderPlan = InProgress },
                    apiCmd |> loadOrderPlan (fun resp -> LoadOrderPlanResult(cmd, resp))
                | Resolved tp ->
                    let apiCmd =
                        match cmd with
                        | Api.FilterOrderPlan _ -> Api.FilterOrderPlan tp
                        | Api.UpdateOrderPlan(_, ctxOpt) -> Api.UpdateOrderPlan(tp, ctxOpt)

                    { state with OrderPlan = InProgress },
                    apiCmd |> loadOrderPlan (fun resp -> LoadOrderPlanResult(cmd, resp))

        | LoadOrderPlanResult(_, Finished(Ok msg)) -> msg |> processOk
        | LoadOrderPlanResult(_, Finished(Error err)) ->
            ({ state with OrderPlan = HasNotStartedYet }, Cmd.none) |> processError err

        | NutritionPlanMsg npCmd ->
            let planState =
                match state.NutritionPlan with
                | Resolved plan -> Recalculating plan
                | _ -> InProgress

            { state with NutritionPlan = planState },
            Api.NutritionPlanCmd npCmd
            |> createApiMsg (fun resp -> LoadNutritionPlanResult(npCmd, resp))

        | LoadNutritionPlanResult(_, Started) -> state, Cmd.none
        | LoadNutritionPlanResult(_, Finished(Ok msg)) -> msg |> processOk
        | LoadNutritionPlanResult(_, Finished(Error err)) ->
            ({ state with NutritionPlan = HasNotStartedYet }, Cmd.none) |> processError err

        | LoadFormulary Started ->
            match state.Formulary with
            | InProgress -> state, Cmd.none
            | _ ->
                let form =
                    match state.Formulary with
                    | Resolved form -> { form with Patient = state.Patient }
                    | _ -> Formulary.empty

                let cmd = form |> loadFormuarly

                { state with Formulary = InProgress }, cmd

        | LoadFormulary(Finished(Ok msg)) -> processOk msg

        | LoadFormulary(Finished(Error err)) ->
            ({ state with Formulary = HasNotStartedYet }, Cmd.none) |> processError err

        | UpdateFormulary form ->
            let state =
                { state with
                    Formulary = Resolved form
                    OrderContext = state.OrderContext |> Deferred.map (OrderContext.syncFormularyToFilter form)
                    Parenteralia =
                        state.Parenteralia
                        |> Deferred.map (fun par ->
                            { par with
                                Generic = form.Generic
                                Route = form.Route
                                Form = form.Form
                            }
                        )
                }

            state,
            Cmd.batch
                [
                    Cmd.ofMsg (LoadFormulary Started)
                    Cmd.ofMsg (LoadOrderContextResult(Api.UpdateOrderContext, Started))
                    Cmd.ofMsg (LoadParenteralia Started)
                ]

        | LoadParenteralia Started ->
            match state.Parenteralia with
            | InProgress -> state, Cmd.none
            | _ ->
                let cmd =
                    let par = state.Parenteralia |> Deferred.defaultValue Parenteralia.empty

                    loadParenteralia par

                { state with Parenteralia = InProgress }, cmd

        | LoadParenteralia(Finished(Ok msg)) -> msg |> processOk

        | LoadParenteralia(Finished(Error err)) ->
            ({ state with Parenteralia = HasNotStartedYet }, Cmd.none) |> processError err

        | UpdateParenteralia par ->
            let state =
                { state with
                    Parenteralia = Resolved par
                    Formulary =
                        state.Formulary
                        |> Deferred.map (fun form ->
                            { form with
                                Indication = None
                                Generic = par.Generic
                                Route = par.Route
                                Form = par.Form
                                DoseType = None
                            }
                        )
                    OrderContext = state.OrderContext |> Deferred.map (OrderContext.syncParenteraliaToFilter par)
                }

            state,
            Cmd.batch
                [
                    Cmd.ofMsg (LoadFormulary Started)
                    Cmd.ofMsg (LoadOrderContextResult(Api.UpdateOrderContext, Started))
                    Cmd.ofMsg (LoadParenteralia Started)
                ]

        | CheckInteractions drugs ->
            if drugs.Length < 2 then
                { state with
                    Interactions = HasNotStartedYet
                    SnackbarMsg = ""
                    SnackbarOpen = false
                },
                Cmd.none
            else
                { state with Interactions = InProgress },
                Api.InteractionCmd(Api.CheckInteractions drugs)
                |> createApiMsg LoadInteractionsResult

        | LoadInteractionsResult(Finished(Ok msg)) -> msg |> processOk
        | LoadInteractionsResult(Finished(Error err)) ->
            ({ state with Interactions = HasNotStartedYet }, Cmd.none) |> processError err
        | LoadInteractionsResult _ -> state, Cmd.none

        | LoadInteractionDrugNames Started ->
            match state.InteractionDrugNames with
            | InProgress -> state, Cmd.none
            | _ ->
                { state with InteractionDrugNames = InProgress },
                Api.InteractionCmd Api.GetDrugNames |> createApiMsg LoadInteractionDrugNames

        | LoadInteractionDrugNames(Finished(Ok msg)) ->
            let state, cmd = msg |> processOk
            { state with DrugNameRetries = 0 }, cmd
        | LoadInteractionDrugNames(Finished(Error _)) ->
            let retries = state.DrugNameRetries + 1

            if retries >= 3 then
                { state with
                    InteractionDrugNames = HasNotStartedYet
                    DrugNameRetries = retries
                    SnackbarMsg = "Interactie medicatie namen konden niet worden geladen"
                    SnackbarOpen = true
                    SnackbarSeverity = "warning"
                },
                Cmd.none
            else
                { state with
                    InteractionDrugNames = HasNotStartedYet
                    DrugNameRetries = retries
                },
                async {
                    do! Async.Sleep 3000
                    return LoadInteractionDrugNames Started
                }
                |> Cmd.fromAsync

        | SessionContextMsg(SessionContextMsg.Launch token) ->
            // launch token received, set session context to being launched
            let state = { state with SessionContext = SessionContext.Content InProgress }

            let launch =
                async {
                    try
                        match! serverApi.launchSession token with
                        // a launched session still has to be handed over to its redeem url
                        | Ok session -> return SessionContextMsg(SessionContextMsg.Redeemed(session, true))
                        | Error err -> return SessionContextMsg(SessionContextMsg.Error err)
                    with ex ->
                        return SessionContextMsg(SessionContextMsg.Error ex.Message)
                }

            state, Cmd.fromAsync launch

        | SessionContextMsg(SessionContextMsg.Redeem token) ->
            // session is being redeemed
            let state = { state with SessionContext = SessionContext.Content InProgress }

            let redeem =
                async {
                    try
                        match! serverApi.redeemSession token with
                        | Ok session -> return SessionContextMsg(SessionContextMsg.Redeemed(session, false))
                        | Error err -> return SessionContextMsg(SessionContextMsg.Error err)
                    with ex ->
                        return SessionContextMsg(SessionContextMsg.Error ex.Message)
                }

            state, Cmd.fromAsync redeem

        | SessionContextMsg(SessionContextMsg.Error err) ->
            // launch token received, but error occurred, set session context to Anonymous
            Logging.error "launch token error" err
            let state = { state with SessionContext = SessionContext.Error err }
            state, Cmd.none

        | SessionContextMsg(SessionContextMsg.Redeemed(content, redirect)) ->
            // a launched session navigates to #/session?redeem={token}, which comes back
            // round as UrlChanged -> Redeem. A redeemed session is already there and must
            // not navigate again, or it would re-enter Redeem indefinitely.
            let cmd =
                if redirect then
                    Cmd.navigate ("session", [ "redeem", content.RedeemToken ])
                else
                    Cmd.none

            let state = { state with SessionContext = SessionContext.Content(Resolved content) }

            state, cmd


    let calculateInterventions calc meds pat =
        meds
        |> Deferred.bind (fun xs ->
            match pat with
            | None -> InProgress
            | Some p ->
                let a = p |> Patient.getAgeInYears
                let w = p |> Patient.getWeightInKg
                xs |> calc a w |> Resolved
        )


open Elmish


type private ConcreteAppEnv
    (state: State, dispatch: Msg -> unit, bm: Deferred<Intervention list>, cm: Deferred<Intervention list>) =

    interface AppEnv.ILocalization with
        member _.LocalizationTerms = state.Localization

    interface AppEnv.IOrderContext with
        member _.OrderContext = state.OrderContext
        member _.OrderContextMsg(cmd, ctx) = OrderContextMsg(cmd, ctx) |> dispatch

    interface AppEnv.IOrderPlan with
        member _.OrderPlan = state.OrderPlan
        member _.OrderPlanCommand cmd = OrderPlanMsg cmd |> dispatch

    interface AppEnv.INutritionPlan with
        member _.NutritionPlan = state.NutritionPlan
        member _.NutritionPlanMsg cmd = NutritionPlanMsg cmd |> dispatch

    interface AppEnv.IPatient with
        member _.Patient = state.Patient
        member _.UpdatePatient p = UpdatePatient p |> dispatch

    interface AppEnv.IFormulary with
        member _.Formulary = state.Formulary
        member _.UpdateFormulary f = UpdateFormulary f |> dispatch

    interface AppEnv.IParenteralia with
        member _.Parenteralia = state.Parenteralia
        member _.UpdateParenteralia p = UpdateParenteralia p |> dispatch

    interface AppEnv.IInteractions with
        member _.Interactions = state.Interactions
        member _.InteractionDrugNames = state.InteractionDrugNames
        member _.CheckInteractions drugs = CheckInteractions drugs |> dispatch

    interface AppEnv.IResources with
        member _.ReloadResources pw =
            OrderContextMsg(Api.ReloadResources pw, OrderContext.empty) |> dispatch

    interface AppEnv.IAuthentication with
        member _.IsAuthenticated = state.IsAuthenticated
        member _.Login password = Login password |> dispatch
        member _.Logout() = Logout |> dispatch

    interface AppEnv.ILogAnalyzer with
        member _.LogFiles = state.LogFiles
        member _.LogAnalysisReport = state.LogAnalysisReport
        member _.ListLogFiles() = ListLogFiles |> dispatch
        member _.AnalyzeLogFile fileName = AnalyzeLogFile fileName |> dispatch

    interface AppEnv.IBolusMedication with
        member _.BolusMedication = bm
        member _.OnSelectBolusMedicationItem s = OnSelectEmergencyListItem s |> dispatch
        member _.BolusMedicationFilter = state.EmergencyListFilter
        member _.OnBolusMedicationFilterChange f = UpdateEmergencyListFilter f |> dispatch

    interface AppEnv.IContinuousMedication with
        member _.ContinuousMedication = cm

        member _.OnSelectContinuousMedicationItem s =
            OnSelectContinuousMedicationItem s |> dispatch

        member _.ContinuousMedicationFilter = state.ContinuousMedsFilter

        member _.OnContinuousMedicationFilterChange f =
            UpdateContinuousMedsFilter f |> dispatch


[<Literal>]
let private themeDef =
    """
responsiveFontSizes(createTheme({
    typography: { fontSize: 12 },
    spacing: 6,
    components: {
        MuiTable: { defaultProps: { size: 'medium' } },
        MuiTextField: { defaultProps: { size: 'medium' } },
        MuiButton: { defaultProps: { size: 'medium' } },
        MuiIconButton: { defaultProps: { size: 'medium' } },
        MuiToolbar: { defaultProps: { variant: 'dense' } },
        MuiAutocomplete: { defaultProps: { size: 'medium' } },
    }
}), { factor: 2 })
"""


[<Import("createTheme", from = "@mui/material/styles")>]
[<Emit(themeDef)>]
let private theme: obj = jsNative


[<Literal>]
let private mobileDef =
    """
responsiveFontSizes(createTheme({
    typography: { fontSize: 11 },
    spacing: 6,
    components: {
        MuiTable: { defaultProps: { size: 'small' } },
        MuiTextField: { defaultProps: { size: 'small' } },
        MuiButton: { defaultProps: { size: 'small' } },
        MuiIconButton: { defaultProps: { size: 'small' } },
        MuiToolbar: { defaultProps: { variant: 'dense' } },
        MuiAutocomplete: { defaultProps: { size: 'small' } },
    }
}), { factor: 2 })
"""


[<Import("createTheme", from = "@mui/material/styles")>]
[<Emit(mobileDef)>]
let private mobile: obj = jsNative


// Entry point must be in a separate file
// for Vite Hot Reload to work
[<JSX.Component>]
let View () =
    let state, dispatch = React.useElmish (init, update, [||])
    let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

    let handleClose =
        fun (_: obj) (reason: string) ->
            if reason <> "clickaway" then
                CloseSnackbar |> dispatch

    let autoHide =
        match state.SnackbarSeverity with
        | "success"
        | "info" -> 3000 |> box
        | _ -> null

    let bm =
        calculateInterventions EmergencyTreatment.calculate state.BolusMedication state.Patient

    let cm =
        let calc =
            fun _ w meds ->
                match w with
                | Some w' -> ContinuousMedication.calculate w' meds
                | None -> []

        calculateInterventions calc state.ContinuousMedication state.Patient

    let appEnv = ConcreteAppEnv(state, dispatch, bm, cm) :> obj

    let sx =
        if isMobile then
            {|
                height = "100vh"
                overflowY = "hidden"
                mb = 5
            |}
        else
            {|
                height = "100vh"
                overflowY = "hidden"
                mb = 0
            |}

    let theme = if isMobile then mobile else theme

    let serverErrorBanner =
        match state.ServerError with
        | Some errMsg ->
            let onClose = fun _ -> dispatch DismissServerError

            JSX.jsx
                $"""
                <Alert severity="error" sx={ {| width = "100%" |} } onClose={onClose}>
                    <AlertTitle>Server probleem</AlertTitle>
                    {errMsg}
                </Alert>
                """
        | None -> null

    let genPresProps =
        {|
            appEnv = appEnv
            showDisclaimer = state.ShowDisclaimer
            isDemo = state.IsDemo
            acceptDisclaimer = fun _ -> AcceptDisclaimer |> dispatch
            updatePage = UpdatePage >> dispatch
            page = state.Page
            languages = Localization.languages
            hospitals = state.Hospitals
            switchLang = UpdateLanguage >> dispatch
            switchHosp = UpdateHospital >> dispatch
        |}

    JSX.jsx
        $"""
    import {{ ThemeProvider }} from '@mui/material/styles';
    import {{ responsiveFontSizes }} from '@mui/material/styles';
    import CssBaseline from '@mui/material/CssBaseline';
    import React from "react";
    import Box from '@mui/material/Box';
    import Snackbar from '@mui/material/Snackbar';
    import IconButton from '@mui/material/IconButton';
    import CloseIcon from '@mui/icons-material/Close';
    import Alert from '@mui/material/Alert';
    import AlertTitle from '@mui/material/AlertTitle';

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box sx={sx}>
                <CssBaseline />
                {serverErrorBanner}
                {Components.Router.View {| onUrlChanged = UrlChanged >> dispatch |}}
                {Pages.GenPres.View genPresProps
                 |> toReact
                 |> Components.Context.Context state.Context}
            </Box>
            <div>
                <Snackbar
                    open={state.SnackbarOpen}
                    autoHideDuration={autoHide}
                    onClose={handleClose}
                >
                    <Alert severity={state.SnackbarSeverity} onClose={fun _ -> CloseSnackbar |> dispatch} sx={ {| width = "100%" |} }>
                        {state.SnackbarMsg}
                    </Alert>
                </Snackbar>
            </div>
        </ThemeProvider>
    </React.StrictMode>
    """


let root = ReactDomClient.createRoot (document.getElementById "genpres-app")
root.render (View() |> toReact)
