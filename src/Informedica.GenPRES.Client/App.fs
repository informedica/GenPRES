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
open SessionMachine
open SigningMachine


module private Elmish =


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
            // the launch Session (plan 409); Anonymous is the state every URL patient runs in
            Session: Session
            // the signing phase of the open Session (plan 622); Idle whenever no Session is open
            Signing: Signing
            // Rules 21, 22 (plan 635): the newest version told while the Session is on an older
            // one; None whenever no Session is open
            MovedOn: OrderPlanHead option
            // what the server was configured with: the default language, the demo flag
            Settings: Deferred<Api.ServerSettings>
            // the url or the User chose the language (LanguagePolicy); the server default no
            // longer applies. The language itself lives in Context, where the views read it
            LanguageChosen: bool
        }


    type Msg =
        | UrlChanged of string list
        | AcceptDisclaimer
        | SessionMsg of SessionMsg
        | SigningMsg of SigningMsg

        | UpdatePage of Global.Pages
        | UpdatePatient of Patient option
        // Rule 19: the version the Session opened with, into the cart over the patient in state
        | LoadCart of SignedOrderPlan
        // Rules 21, 22: a reply said the record moved on
        | RecordMovedOn of OrderPlanHead

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
        | LoadSettings of AsyncOperationStatus<Result<Api.ServerSettings, exn>>

        | Login of password: string
        | LoadLoginResult of ApiResponse
        | Logout

        | ListLogFiles
        | LoadLogFilesResult of ApiResponse
        | AnalyzeLogFile of string
        | LoadLogAnalysisResult of ApiResponse


    and ApiResponse = AsyncOperationStatus<Result<Answer, string[]>>

    /// A computing reply with the OpenedToken the request started from, so that what the reply
    /// tells about the Session (Rules 11, 21) lands only on the Session that asked: a request
    /// of a Session since closed or replaced must not end or warn the current one.
    and Answer =
        {
            From: OpenedToken option
            Reply: Api.Reply
        }


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


    let loadSettings =
        async {
            try
                let! settings = serverApi.getSettings ()
                return LoadSettings(Finished(Ok settings))
            with ex ->
                return LoadSettings(Finished(Error ex))
        }
        |> Cmd.fromAsync


    /// The OpenedToken the Session holds, sent with every computing request (Rule 34); none
    /// without an open Session.
    let tokenOf (session: Session) =
        match session with
        | Session.Open opened -> opened.OpenedToken
        | _ -> None


    let createApiMsg (opened: OpenedToken option) msg cmd =
        async {
            let! result =
                serverApi.processCommand (
                    {
                        Opened = opened
                        Command = cmd
                    }
                    : Api.Request
                )

            return
                result
                |> Result.map (fun reply ->
                    {
                        From = opened
                        Reply = reply
                    }
                )
                |> Finished
                |> msg
        }
        |> Cmd.fromAsync


    let processResponse (state: State) (response: Api.Response) =
        match response with
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


    /// The result, and what the Session is told with it: the record moved on (Rules 21, 22) or
    /// the Session ended (Rule 11), each as its own message so the machines decide. The
    /// stale-request guard: the notice counts only when the request started from the token the
    /// open Session holds now; a reply of a Session since closed, replaced or re-minted says
    /// nothing about this one (the next request repeats what still holds, Rule 21 is stateless).
    let processApiMsg (state: State) (answer: Answer) =
        let current =
            match state.Session with
            | Session.Open opened -> Some opened.OpenedToken
            | _ -> None

        let told =
            match answer.Reply.Notice with
            | Some _ when current <> Some answer.From -> Cmd.none
            | Some(RecordNotice.NewerVersion head) -> Cmd.ofMsg (RecordMovedOn head)
            | Some(RecordNotice.Ended ending) -> Cmd.ofMsg (SessionMsg(SessionMsg.EndedByServer ending))
            | None -> Cmd.none

        let state, cmd = processResponse state answer.Reply.Response
        state, Cmd.batch [ cmd; told ]


    let loadOrderContext opened resp =
        Api.OrderContextCmd >> createApiMsg opened resp


    let loadOrderPlan opened resp =
        Api.OrderPlanCmd >> createApiMsg opened resp


    let loadFormuarly opened =
        Api.FormularyCmd >> createApiMsg opened LoadFormulary


    let loadParenteralia opened =
        Api.ParenteraliaCmd >> createApiMsg opened LoadParenteralia


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

    // The patient, page, language, disclaimer and medication carried by an
    // anonymous "#/patient?..." url. A "#/session..." url carries none of these:
    // the session supplies the patient (plan 409), so it yields the defaults
    // without a warning.
    let parsePatient sl =
        match sl with
        | [] -> None, None, None, true, None
        | "session" :: _ -> None, None, None, true, None
        | [ "patient"; Route.Query queryParams ] ->
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
                        | Some s when s = "y" -> [ CVL ]
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
                    // only the parameter names: the values are patient data
                    Logging.warning "could not parse url to patient" (paramsMap |> Map.toList |> List.map fst)
                    None

            let page =
                match paramsMap |> Map.tryFind "pg" with
                | Some s when s = "el" -> Some LifeSupport
                | Some s when s = "cm" -> Some ContinuousMeds
                | Some s when s = "pr" -> Some Prescribe
                | Some s when s = "fm" -> Some Formulary
                | Some s when s = "pe" -> Some Parenteralia
                | _ -> None

            // ISO code, display name or the legacy codes (du, gr, sp): one parser with the server
            let lang = paramsMap |> Map.tryFind "la" |> Option.bind Localization.tryParse

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

        | _ ->
            // only the route segment: the rest of the url is never logged
            Logging.warning "could not parse url" (sl |> List.head)

            None, None, None, true, None


    /// What a "#/session?..." url carries: the Launch MainEHR opened GenPRES with (launch
    /// sequence step 1), or the reason the IdentityProvider return refused it (step 4.5).
    [<RequireQualifiedAccess>]
    type LaunchUrl =
        | Launch of Launch
        | Refused of LaunchRefusal


    /// The fixed vocabulary of "#/session?refused={reason}"; an unknown reason is invalid.
    let parseRefusal reason =
        match reason with
        | "expired" -> LaunchRefusal.LaunchExpired
        | "spent" -> LaunchRefusal.LaunchSpent
        | "invalid" -> LaunchRefusal.LaunchInvalid
        | "no-identity" -> LaunchRefusal.NoBrowserIdentity
        | "no-role" -> LaunchRefusal.NoRole
        | "wrong-patient" -> LaunchRefusal.WrongActivePatient
        | "enrolment" -> LaunchRefusal.EnrolmentRequired
        | _ -> LaunchRefusal.LaunchInvalid


    /// The launch token of a "#/session?launch={token}" url, opaque to the client, or the
    /// refusal of a "#/session?refused={reason}" url. Both are erased the same way.
    let parseLaunch sl =
        match sl with
        | [ "session"; Route.Query queryParams ] ->
            let query = queryParams |> Map.ofList

            match query |> Map.tryFind "launch", query |> Map.tryFind "refused" with
            | Some token, _ -> Some(LaunchUrl.Launch(Launch token))
            | None, Some reason -> Some(LaunchUrl.Refused(parseRefusal reason))
            | None, None -> None
        | _ -> None


    /// Launch sequence step 2: replace the launch url with "#/session" in the
    /// address bar and the history entry, so the token survives neither a
    /// reload, the back button nor a copied url. Goes through the History API
    /// directly: Router.navigate would dispatch the navigation event and
    /// re-enter UrlChanged.
    let eraseLaunch () =
        Browser.Dom.history.replaceState (null, "", "#/session")


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
        =
        {
            ShowDisclaimer = discl
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
                    // the server default replaces this once LoadSettings resolves, unless the url chose
                    Localization = (LanguagePolicy.Language.initial lang).Current
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
            Session = Session.Anonymous
            Signing = Signing.Idle
            MovedOn = None
            Settings = HasNotStartedYet
            LanguageChosen = (LanguagePolicy.Language.initial lang).Chosen
        }


    /// The language as LanguagePolicy sees it, and the state after the policy answered.
    let languageOf (state: State) : LanguagePolicy.Language =
        {
            Current = state.Context.Localization
            Chosen = state.LanguageChosen
        }


    let withLanguage (language: LanguagePolicy.Language) (state: State) =
        { state with
            State.Context.Localization = language.Current
            LanguageChosen = language.Chosen
        }


    /// Launch step 3 then 4: make the key pair, then present the Launch with its public key.
    /// A browser that cannot make a key cannot launch; that is reported as a missing browser
    /// identity, the refusal whose text asks for a retry and then a relaunch.
    let presentLaunch (launch: Launch) : Cmd<Msg> =
        async {
            match! Keys.generate () |> Async.Catch with
            | Choice1Of2 key -> return SessionMsg(SessionMsg.Present(launch, key))
            | Choice2Of2 ex ->
                Logging.error "could not make the browser key pair" ex.Message
                return SessionMsg(SessionMsg.RefusedAtCallback LaunchRefusal.NoBrowserIdentity)
        }
        |> Cmd.fromAsync


    /// The session command a "#/session?..." url asks for; a plain url asks for nothing.
    let launchCmd (launchUrl: LaunchUrl option) : Cmd<Msg> =
        match launchUrl with
        | Some(LaunchUrl.Launch launch) -> presentLaunch launch
        | Some(LaunchUrl.Refused refusal) -> Cmd.ofMsg (SessionMsg(SessionMsg.RefusedAtCallback refusal))
        | None -> Cmd.none


    let init () : State * Cmd<Msg> =
        let url = Router.currentUrl ()
        let launchUrl = url |> parseLaunch

        if launchUrl.IsSome then
            eraseLaunch ()

        let pat, page, lang, discl, med = url |> parsePatient

        let cmds =
            Cmd.batch
                [
                    // a page load without a Launch resumes on the cookie (reload, IdP return)
                    match launchUrl with
                    | None -> Cmd.ofMsg (SessionMsg SessionMsg.Resume)
                    | Some _ -> launchCmd launchUrl
                    checkServer
                    Cmd.ofMsg (LoadSettings Started)
                    Cmd.ofMsg (LoadNormalValues Started)
                    Cmd.ofMsg (LoadBolusMedication Started)
                    Cmd.ofMsg (LoadContinuousMedication Started)
                    Cmd.ofMsg (LoadProducts Started)
                    Cmd.ofMsg (LoadLocalization Started)
                    Cmd.ofMsg (LoadFormulary Started)
                    Cmd.ofMsg (LoadParenteralia Started)
                    Cmd.ofMsg (LoadInteractionDrugNames Started)
                ]

        initialState pat page lang discl med, cmds


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


    /// One command per session effect (plan 409, "Wiring in App.fs"). A transport failure
    /// is a message, never an exception: an Error outcome for a presentation, CloseFailed for
    /// a close that did not reach the server.
    let interpretSessionEffect (effect: SessionEffect) : Cmd<Msg> =
        match effect with
        | SessionEffect.CallPresentLaunch(launch, key) ->
            async {
                try
                    let! outcome = serverApi.processLaunch (Api.LaunchCommand.PresentLaunch(launch, key))
                    return SessionMsg(SessionMsg.Outcome(launch, key, Ok outcome))
                with ex ->
                    return SessionMsg(SessionMsg.Outcome(launch, key, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallGetSession ->
            async {
                try
                    match! serverApi.processSession Api.SessionCommand.GetSession with
                    | Api.SessionResponse.SessionResp(Some session) ->
                        return SessionMsg(SessionMsg.Resumed(Ok(ResumeResult.Found session)))
                    | Api.SessionResponse.SessionResp None
                    | Api.SessionResponse.SessionClosed ->
                        return SessionMsg(SessionMsg.Resumed(Ok ResumeResult.NotFound))
                    | Api.SessionResponse.SessionEnded ending ->
                        return SessionMsg(SessionMsg.Resumed(Ok(ResumeResult.Ended ending)))
                    | Api.SessionResponse.EnrolmentPending pending ->
                        return SessionMsg(SessionMsg.Resumed(Ok(ResumeResult.Enrolling pending)))
                    // never an answer to GetSession
                    | Api.SessionResponse.PinRefused _ ->
                        return SessionMsg(SessionMsg.Resumed(Ok ResumeResult.NotFound))
                with ex ->
                    return SessionMsg(SessionMsg.Resumed(Error ex.Message))
            }
            |> Cmd.fromAsync
        // told in update, where the sentence and the notice live
        | SessionEffect.TellVersionOpened _ -> Cmd.none
        | SessionEffect.CallOpenVersion(id, from) ->
            async {
                try
                    match! serverApi.processSession (Api.SessionCommand.OpenVersion id) with
                    | Api.SessionResponse.SessionResp opened -> return SessionMsg(SessionMsg.Reopened(from, Ok opened))
                    // never an answer to OpenVersion
                    | _ -> return SessionMsg(SessionMsg.Reopened(from, Ok None))
                with ex ->
                    return SessionMsg(SessionMsg.Reopened(from, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallCloseSession ->
            async {
                // the server deletes the cookie whatever its close returns (finally), so an
                // answer of any kind means Closed; only a request that never got there fails
                match! serverApi.processSession Api.SessionCommand.CloseSession |> Async.Catch with
                | Choice1Of2 _ -> return SessionMsg SessionMsg.Closed
                | Choice2Of2 ex -> return SessionMsg(SessionMsg.CloseFailed ex.Message)
            }
            |> Cmd.fromAsync
        | SessionEffect.CallSupplyPin(code, pin) ->
            async {
                try
                    match! serverApi.processSession (Api.SessionCommand.SupplyPin(code, pin)) with
                    | Api.SessionResponse.SessionResp(Some session) ->
                        return SessionMsg(SessionMsg.PinAnswered(Ok(PinOutcome.Opened session)))
                    | Api.SessionResponse.PinRefused refusal ->
                        return SessionMsg(SessionMsg.PinAnswered(Ok(PinOutcome.Refused refusal)))
                    // never an answer to SupplyPin: the attempt is gone, whatever happened
                    | Api.SessionResponse.SessionResp None
                    | Api.SessionResponse.SessionClosed
                    | Api.SessionResponse.SessionEnded _
                    | Api.SessionResponse.EnrolmentPending _ ->
                        return SessionMsg(SessionMsg.PinAnswered(Ok(PinOutcome.Refused PinRefusal.AttemptExpired)))
                with ex ->
                    return SessionMsg(SessionMsg.PinAnswered(Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.GoTo url -> Cmd.ofEffect (fun _ -> Browser.Dom.window.location.assign url)
        | SessionEffect.SetPatient patient -> Cmd.ofMsg (UpdatePatient patient)
        // Rule 19: the cart starts as the version the Session opened with; built in update, over
        // the patient as UpdatePatient left it (normal values applied)
        | SessionEffect.LoadCart head -> Cmd.ofMsg (LoadCart head)
        | SessionEffect.KeepKey thumbprint ->
            Cmd.ofEffect (fun _ ->
                async {
                    match! Keys.keep thumbprint |> Async.Catch with
                    | Choice1Of2() -> ()
                    | Choice2Of2 ex -> Logging.error "could not prune the browser keys" ex.Message
                }
                |> Async.StartImmediate
            )


    /// One command per signing effect (plan 622). The machine names the plan, the challenge,
    /// the PIN, the request id and the key; the OpenedToken comes from the open Session here
    /// (Rule 34), and every answer carries the request id or the key it answers, so the machine
    /// can drop one that belongs to an earlier Session. Without an open Session nothing is sent:
    /// the answer is a refusal. What is told (signed, refused, an error) is put on the snackbar
    /// by `update`, not here.
    let interpretSigningEffect (session: Session) (effect: SigningEffect) : Cmd<Msg> =
        let token = tokenOf session

        match effect with
        | SigningEffect.CallChallenge(plan, notice, request) ->
            match token with
            | None ->
                Cmd.ofMsg (
                    SigningMsg(
                        SigningMsg.ChallengeAnswered(request, Ok(SigningResponse.Refused SigningRefusal.NoSession))
                    )
                )
            | Some opened ->
                async {
                    try
                        let! answer =
                            serverApi.processSigning (Api.SigningCommand.RequestSignChallenge(plan, opened, notice))

                        return SigningMsg(SigningMsg.ChallengeAnswered(request, Ok answer))
                    with ex ->
                        return SigningMsg(SigningMsg.ChallengeAnswered(request, Error ex.Message))
                }
                |> Cmd.fromAsync
        | SigningEffect.CallSubmit(plan, challenge, pin, key) ->
            match token with
            | None ->
                Cmd.ofMsg (
                    SigningMsg(SigningMsg.SubmitAnswered(key, Ok(SigningResponse.Refused SigningRefusal.NoSession)))
                )
            | Some opened ->
                async {
                    try
                        let! answer =
                            serverApi.processSigning (
                                Api.SigningCommand.Submit
                                    {
                                        Plan = plan
                                        Opened = opened
                                        Challenge = challenge
                                        Pin = pin
                                        IdemKey = key
                                    }
                            )

                        return SigningMsg(SigningMsg.SubmitAnswered(key, Ok answer))
                    with ex ->
                        return SigningMsg(SigningMsg.SubmitAnswered(key, Error ex.Message))
                }
                |> Cmd.fromAsync
        | SigningEffect.RenewToken token -> Cmd.ofMsg (SessionMsg(SessionMsg.TokenRenewed token))
        | SigningEffect.EndSession ending -> Cmd.ofMsg (SessionMsg(SessionMsg.EndedByServer ending))
        | SigningEffect.SetPatient patient -> Cmd.ofMsg (UpdatePatient(Some patient))
        | SigningEffect.TellSigned _
        | SigningEffect.TellRefused _
        | SigningEffect.TellError _ -> Cmd.none


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

        | LoadSettings Started -> { state with Settings = InProgress }, loadSettings

        | LoadSettings(Finished(Ok settings)) ->
            // the server default counts until the url or the User chooses; a choice made while
            // the settings were in flight wins (LanguagePolicy.onServerDefault)
            { state with
                Settings = Resolved settings
                IsDemo = settings.IsDemo
            }
            |> withLanguage (languageOf state |> LanguagePolicy.Language.onServerDefault settings.Language),
            Cmd.none

        | LoadSettings(Finished(Error err)) ->
            // no settings: the client keeps its own defaults, which is what it did before
            Logging.error "cannot load the server settings" err
            { state with Settings = HasNotStartedYet }, Cmd.none

        | Login password ->
            state,
            Api.LogAnalyzerCmd(Api.ValidatePassword password)
            |> createApiMsg (tokenOf state.Session) LoadLoginResult

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
            |> createApiMsg (tokenOf state.Session) LoadLogFilesResult

        | LoadLogFilesResult(Finished(Ok resp)) -> processOk resp

        | LoadLogFilesResult(Finished(Error err)) ->
            ({ state with LogFiles = HasNotStartedYet }, Cmd.none) |> processError err

        | LoadLogFilesResult Started -> state, Cmd.none

        | AnalyzeLogFile fileName ->
            { state with LogAnalysisReport = InProgress },
            Api.LogAnalyzerCmd(Api.AnalyzeLogFile(state.AuthToken, fileName))
            |> createApiMsg (tokenOf state.Session) LoadLogAnalysisResult

        | LoadLogAnalysisResult(Finished(Ok resp)) -> processOk resp

        | LoadLogAnalysisResult(Finished(Error err)) ->
            ({ state with LogAnalysisReport = HasNotStartedYet }, Cmd.none)
            |> processError err

        | LoadLogAnalysisResult Started -> state, Cmd.none

        | AcceptDisclaimer -> { state with ShowDisclaimer = false }, Cmd.none

        | UpdateLanguage lang ->
            { state with ShowDisclaimer = true }
            |> withLanguage (languageOf state |> LanguagePolicy.Language.choose lang),
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

        // FilterOrderPlan sends the cart through the server so totals and filters are computed
        // as for any cart; the patient is the one every other part of the state uses
        // told once per version (Rule 22: it gates nothing); the bar on the order plan offers it
        | RecordMovedOn head ->
            let movedOn, news = MovedOn.receive state.MovedOn head
            let state = { state with MovedOn = movedOn }

            if news then
                let tr term =
                    Global.getLocalizedTerm
                        state.Localization
                        state.Context.Localization
                        (SigningPolicy.english term)
                        term

                { state with
                    SnackbarMsg = SigningPolicy.movedOnSentence tr head
                    SnackbarOpen = true
                    SnackbarSeverity = "warning"
                },
                Cmd.none
            else
                state, Cmd.none

        | LoadCart head ->
            match state.Patient with
            | Some pat -> state, Cmd.ofMsg (OrderPlanMsg(Api.FilterOrderPlan(OrderPlan.create pat head.Scenarios)))
            | None -> state, Cmd.none

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

        | UrlChanged sl ->
            let launchUrl = sl |> parseLaunch

            if launchUrl.IsSome then
                eraseLaunch ()

            let pat, page, lang, discl, med = sl |> parsePatient

            // an open Session supplies the patient: url patient parameters count only while
            // no Session holds one (plan 409, "Patient is never assigned directly"). The
            // router fires UrlChanged on mount too, while a Resume may still be in flight,
            // so only Open and Closing block the url patient
            let anonymous =
                match state.Session with
                | Session.Open _
                | Session.Closing _ -> false
                | _ -> true

            let pat = if anonymous then pat else state.Patient

            // only an `la` parameter changes the language; a navigation keeps the current one
            let language = languageOf state |> LanguagePolicy.Language.onUrl lang

            { state with
                ShowDisclaimer = discl
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
                State.Context.Localization = language.Current
                LanguageChosen = language.Chosen
            },
            Cmd.batch
                [
                    if anonymous then
                        Cmd.ofMsg (pat |> UpdatePatient)
                    launchCmd launchUrl
                ]

        | SessionMsg msg ->
            let session, effects = Session.transition msg state.Session

            // a failed close is reported only when it was this session's close: a CloseFailed
            // that arrives after a newer launch superseded the Closing session is dropped by
            // the machine and must not put an error over the newer session
            let state =
                match msg, state.Session with
                | SessionMsg.CloseFailed reason, Session.Closing _ ->
                    Logging.error "could not close the session on the server" reason

                    { state with
                        SnackbarMsg = "De sessie kon niet worden gesloten. Probeer het opnieuw."
                        SnackbarOpen = true
                        SnackbarSeverity = "error"
                    }
                // the same for a PIN that never reached the server: the form comes back as it was
                | SessionMsg.PinAnswered(Error reason), Session.SupplyingPin _ ->
                    Logging.error "could not send the PIN to the server" reason

                    { state with
                        SnackbarMsg = "De pincode kon niet worden verstuurd. Probeer het opnieuw."
                        SnackbarOpen = true
                        SnackbarSeverity = "error"
                    }
                | _ -> state

            // a signature belongs to an open Session: whatever ends the Session drops it (ext 3e);
            // so does the moved-on notice
            let signing, movedOn =
                match session with
                | Session.Open _ -> state.Signing, state.MovedOn
                | _ -> Signing.Idle, None

            // UC-4 step 4: the version is open; said once, and the notice is spent
            let state, movedOn =
                effects
                |> List.fold
                    (fun (state, movedOn) effect ->
                        match effect with
                        | SessionEffect.TellVersionOpened head ->
                            let tr term =
                                Global.getLocalizedTerm
                                    state.Localization
                                    state.Context.Localization
                                    (SigningPolicy.english term)
                                    term

                            { state with
                                SnackbarMsg = SigningPolicy.versionOpenedSentence tr head
                                SnackbarOpen = true
                                SnackbarSeverity = "success"
                            },
                            // a newer notice told meanwhile stays, with its offer
                            MovedOn.opened movedOn head
                        | _ -> state, movedOn
                    )
                    (state, movedOn)

            { state with
                Session = session
                Signing = signing
                MovedOn = movedOn
            },
            effects |> List.map interpretSessionEffect |> Cmd.batch

        | SigningMsg msg ->
            let signing, effects = Signing.transition msg state.Signing

            let tr term =
                Global.getLocalizedTerm state.Localization state.Context.Localization (SigningPolicy.english term) term

            let tell message severity (state: State) =
                { state with
                    SnackbarMsg = message
                    SnackbarOpen = true
                    SnackbarSeverity = severity
                }

            let state =
                effects
                |> List.fold
                    (fun state effect ->
                        match effect with
                        | SigningEffect.TellSigned signed ->
                            state |> tell (SigningPolicy.signedSentence tr signed) "success"
                        // a refusal because the record moved on (Rule 20) is the notice too
                        // (Rule 22); the sentence is told here, the bar offers the version
                        | SigningEffect.TellRefused(SigningRefusal.Blocked head as refusal) ->
                            { state with MovedOn = MovedOn.receive state.MovedOn head |> fst }
                            |> tell (SigningPolicy.refusalSentence tr refusal) "warning"
                        | SigningEffect.TellRefused refusal ->
                            state |> tell (SigningPolicy.refusalSentence tr refusal) "warning"
                        | SigningEffect.TellError reason ->
                            Logging.error "could not send the signature to the server" reason

                            state |> tell (tr Terms.``Signing Send Failed``) "error"
                        | _ -> state
                    )
                    state

            { state with Signing = signing }, effects |> List.map (interpretSigningEffect state.Session) |> Cmd.batch

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
                    |> loadOrderContext (tokenOf state.Session) (fun resp -> LoadOrderContextResult(cmd, resp))
                | _ -> { state with OrderContext = HasNotStartedYet }, Cmd.none
            | Some pat ->
                match state.OrderContext with
                | InProgress
                | Recalculating _ -> state, Cmd.none
                | HasNotStartedYet ->
                    { state with OrderContext = InProgress },
                    (cmd, OrderContext.empty |> OrderContext.setPatient pat)
                    |> loadOrderContext (tokenOf state.Session) (fun resp -> LoadOrderContextResult(cmd, resp))
                | Resolved ctx ->
                    { state with OrderContext = Recalculating ctx },
                    (cmd, { ctx with Patient = pat })
                    |> loadOrderContext (tokenOf state.Session) (fun resp -> LoadOrderContextResult(cmd, resp))

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
                    |> createApiMsg (tokenOf state.Session) (fun resp -> LoadOrderPlanResult(tpCmd, resp))
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
                    apiCmd
                    |> loadOrderPlan (tokenOf state.Session) (fun resp -> LoadOrderPlanResult(cmd, resp))
                | Resolved tp ->
                    let apiCmd =
                        match cmd with
                        | Api.FilterOrderPlan _ -> Api.FilterOrderPlan tp
                        | Api.UpdateOrderPlan(_, ctxOpt) -> Api.UpdateOrderPlan(tp, ctxOpt)

                    { state with OrderPlan = InProgress },
                    apiCmd
                    |> loadOrderPlan (tokenOf state.Session) (fun resp -> LoadOrderPlanResult(cmd, resp))

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
            |> createApiMsg (tokenOf state.Session) (fun resp -> LoadNutritionPlanResult(npCmd, resp))

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

                let cmd = form |> loadFormuarly (tokenOf state.Session)

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

                    loadParenteralia (tokenOf state.Session) par

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
                |> createApiMsg (tokenOf state.Session) LoadInteractionsResult

        | LoadInteractionsResult(Finished(Ok msg)) -> msg |> processOk
        | LoadInteractionsResult(Finished(Error err)) ->
            ({ state with Interactions = HasNotStartedYet }, Cmd.none) |> processError err
        | LoadInteractionsResult _ -> state, Cmd.none

        | LoadInteractionDrugNames Started ->
            match state.InteractionDrugNames with
            | InProgress -> state, Cmd.none
            | _ ->
                { state with InteractionDrugNames = InProgress },
                Api.InteractionCmd Api.GetDrugNames
                |> createApiMsg (tokenOf state.Session) LoadInteractionDrugNames

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

    interface AppEnv.ISession with
        member _.Session = state.Session
        member _.Close() = SessionMsg SessionMsg.Close |> dispatch
        member _.Retry() = SessionMsg SessionMsg.Retry |> dispatch

        member _.OpenAnonymously() =
            SessionMsg SessionMsg.OpenAnonymous |> dispatch

        member _.SupplyPin code pin =
            SessionMsg(SessionMsg.SupplyPin(code, pin)) |> dispatch

        member _.MovedOn = state.MovedOn

        member _.OpenVersion id =
            SessionMsg(SessionMsg.OpenVersion id) |> dispatch

    interface AppEnv.ISigning with
        member _.Signing = state.Signing

        // one request id per Sign, so the answer lands on this request and no other
        member _.Sign plan =
            SigningMsg(SigningMsg.Sign(plan, Guid.NewGuid().ToString())) |> dispatch

        member _.Accept() =
            SigningMsg SigningMsg.Accept |> dispatch

        // Rule 45: one key per confirmation; the machine keeps it for a retry
        member _.Confirm pin =
            SigningMsg(SigningMsg.Confirm(pin, Guid.NewGuid().ToString())) |> dispatch

        member _.Cancel() =
            SigningMsg SigningMsg.Cancel |> dispatch

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
            // the disclaimer is for anonymous use only (plan 409): a launched, resuming or
            // refused session never sees it; an anonymous open after a refusal does
            showDisclaimer =
                state.ShowDisclaimer
                && (
                    match state.Session with
                    | Session.Anonymous -> true
                    | _ -> false
                )
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
