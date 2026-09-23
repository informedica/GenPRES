module App

open System
open Fable.Core
open Fable.Core.JsInterop
open Browser
open Fable.React
open Feliz
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
open OrderPlanMachine
open OrderContextMachine


module private Elmish =


    /// The snackbar: a message with a severity, shown or not.
    type Snackbar =
        {
            Message: string
            Open: bool
            Severity: string
        }


    module Snackbar =

        /// The message shown, with its severity.
        let shown (message: string) (severity: string) =
            {
                Message = message
                Open = true
                Severity = severity
            }


        /// Nothing shown: the empty message, the severity back to error.
        let closed =
            {
                Message = ""
                Open = false
                Severity = "error"
            }


    /// The app-level UI: the page, the disclaimer, the language and the hospital, the demo
    /// flag, the server error and the list filters.
    type UiState =
        {
            Page: Global.Pages
            ShowDisclaimer: bool
            // the language the views read, and the hospital
            Context: Context
            // the url or the User chose the language (LanguagePolicy); the server default no
            // longer applies. The language itself lives in Context, where the views read it
            LanguageChosen: bool
            IsDemo: bool
            ServerError: string option
            EmergencyListFilter: string[]
            ContinuousMedsFilter: string[]
            Snackbar: Snackbar
            // the patient data as the panel edits it and the lists read it, the estimate applied
            PatientDraft: Patient option
        }


    /// What the server is asked for, once or again: the reading of each plain fetch.
    type FetchesState =
        {
            NormalValues: Deferred<NormalValues>
            BolusMedication: Deferred<BolusMedication list>
            ContinuousMedication: Deferred<ContinuousMedication list>
            Products: Deferred<Product list>
            Localization: Deferred<string[][]>
            Hospitals: Deferred<string[]>
            // what the server was configured with: the default language, the demo flag
            Settings: Deferred<Api.ServerSettings>
            Formulary: Deferred<Formulary>
            Parenteralia: Deferred<Parenteralia>
            Interactions: Deferred<DrugInteraction[]>
            InteractionDrugNames: Deferred<string[]>
            // the drug names are asked again on a failure, three times
            DrugNameRetries: int
            ServerStatus: Deferred<bool>
        }


    /// The admin login, under the token it bought, and what it fetches.
    type AdminState =
        {
            IsAuthenticated: bool
            AuthToken: string
            // counts logins and logouts, so that a login answer of an earlier attempt is dropped
            LoginAttempt: int
            LogFiles: Deferred<LogFileInfo[]>
            LogAnalysisReport: Deferred<string>
            // the resource reload from the settings page: InProgress from the request until the
            // pages have refreshed over the reloaded resources
            Reloading: Deferred<unit>
        }


    /// The four lanes, each a machine's state the pages read a projection of, and the patient
    /// they are for.
    type LanesState =
        {
            // the patient the workbench and the plan are for: the draft, once it meets the minimum
            Patient: Patient option
            // the prescribing workbench, as the order-context machine holds it
            OrderContext: OrderContextState
            // the one plan, as the order-plan machine holds it
            OrderPlan: OrderPlanState
            // the launch Session; Anonymous is the state every URL patient runs in
            Session: SessionState
            // the signing phase of the open Session; Idle whenever no Session is open
            Signing: SigningState
        }


    type State =
        {
            // the lanes
            Lanes: LanesState
            // the plain fetches
            Fetches: FetchesState
            // the admin login and what it fetches
            Admin: AdminState
            // the app-level UI
            Ui: UiState
        }


    type Msg =
        | UrlChanged of string list
        | AcceptDisclaimer
        | SessionMsg of SessionMsg
        | SigningMsg of SigningMsg

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

        // the prescribing workbench: the order-context machine's messages
        | OrderContextMsg of OrderContextMsg
        // the server's answer to a workbench request: the notice is told here, the context goes
        // to the machine under the request it answers
        | OrderContextAnswered of request: string * Answer<OrderContext>

        // the one plan, nutrition included: the order-plan machine's messages
        | OrderPlanMsg of OrderPlanMsg
        // the server's answer to a plan request: the notice is told here, the plan goes to the
        // machine under the request it answers
        | OrderPlanAnswered of request: string * Answer<OrderPlan>

        | UpdateFormulary of Formulary
        | LoadFormulary of ApiResponse<Formulary>

        | UpdateParenteralia of Parenteralia
        | LoadParenteralia of ApiResponse<Parenteralia>

        | CheckInteractions of string list
        | LoadInteractionsResult of ApiResponse<Api.InteractionResponse>
        | LoadInteractionDrugNames of ApiResponse<Api.InteractionResponse>

        | UpdateLanguage of Localization.Locales
        | LoadLocalization of AsyncOperationStatus<Result<string[][], string>>

        | UpdateHospital of string
        | CloseSnackbar
        | CheckServer of AsyncOperationStatus<Result<string, exn>>
        | DismissServerError
        | LoadSettings of AsyncOperationStatus<Result<Api.ServerSettings, exn>>

        | Login of password: string
        // the attempt the answer belongs to: an answer of an earlier attempt is dropped
        | LoadLoginResult of attempt: int * AdminResult
        | Logout

        // the token the request was made with: an answer to a token no longer held is dropped
        | ListLogFiles
        | LoadLogFilesResult of token: string * AdminResult
        | AnalyzeLogFile of string
        | LoadLogAnalysisResult of token: string * AdminResult
        | ReloadResources
        | LoadReloadResult of token: string * AdminResult


    /// A computing answer of a member, typed by what the member answers
    and ApiResponse<'r> = AsyncOperationStatus<Result<Answer<'r>, string[]>>

    /// An admin answer: no envelope, so no token it started from and no notice
    and AdminResult = AsyncOperationStatus<Result<Api.AdminResponse, string[]>>

    /// A computing reply with the OpenedToken the request started from, so that what the reply
    /// tells about the Session (moved on, ended) lands only on the Session that asked: a request
    /// of a Session since closed or replaced must not end or warn the current one.
    and Answer<'r> =
        {
            From: OpenedToken option
            Reply: Api.Reply<'r>
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


    /// The OpenedToken the Session holds, sent with every computing request; none
    /// without an open Session.
    let tokenOf = SessionState.token


    /// A request id, minted at dispatch, so that an answer can name the request it answers.
    let newRequest () = Guid.NewGuid().ToString()


    /// A computing request through the member given, with the OpenedToken the Session holds;
    /// the answer comes back with the token it started from.
    let createApiMsg
        (call: Api.Request<'cmd> -> Async<Result<Api.Reply<'resp>, string[]>>)
        (opened: OpenedToken option)
        msg
        (cmd: 'cmd)
        =
        async {
            let! result =
                call
                    {
                        Opened = opened
                        Command = cmd
                    }

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


    /// An admin command over the token the login bought; never Session-bound.
    let createAdminMsg msg cmd =
        async {
            let! result = serverApi.processAdmin cmd
            return result |> Finished |> msg
        }
        |> Cmd.fromAsync


    /// A reload settles when the refresh it started has answered: the order context over a
    /// patient, else the formulary.
    let settleReload (state: State) =
        match state.Admin.Reloading with
        | InProgress -> { state with Admin.Reloading = Resolved() }
        | _ -> state


    /// An admin answer applied. A reload done reloads what the pages show: the order context
    /// over the patient, which takes the formulary and the parenteralia with it, or those two
    /// alone when there is no patient; the reload stays pending until that refresh answered.
    let applyAdmin (state: State) (response: Api.AdminResponse) =
        match response with
        | Api.AdminResponse.PasswordValidated(isValid, token) ->
            if isValid then
                { state with
                    Admin.IsAuthenticated = true
                    Admin.AuthToken = token
                },
                Cmd.none
            else
                { state with
                    Admin.IsAuthenticated = false
                    Admin.AuthToken = ""
                    Ui.Snackbar = Snackbar.shown "Invalid password" "error"
                },
                Cmd.none
        | Api.AdminResponse.LogFilesListed files -> { state with Admin.LogFiles = Resolved files }, Cmd.none
        | Api.AdminResponse.LogFileAnalyzed report -> { state with Admin.LogAnalysisReport = Resolved report }, Cmd.none
        | Api.AdminResponse.ResourcesReloaded ->
            let refresh =
                match state.Lanes.Patient with
                // the workbench evaluated again, as it is, whatever was in flight
                | Some _ ->
                    let ctx =
                        state.Lanes.OrderContext
                        |> OrderContextState.context
                        |> Option.defaultValue OrderContext.empty

                    Cmd.ofMsg (OrderContextMsg(OrderContextMsg.Seed(ctx, newRequest ())))
                | None -> Cmd.batch [ Cmd.ofMsg (LoadFormulary Started); Cmd.ofMsg (LoadParenteralia Started) ]

            state, refresh


    /// The result, and what the Session is told with it: the record moved on or the Session
    /// ended, as one message to the session machine, which decides whether the notice counts
    /// (only when the request started from the token the open Session holds now).
    let processApiMsg (state: State) (answer: Answer<'r>) (apply: State -> 'r -> State * Cmd<Msg>) =
        let told =
            match answer.Reply.Notice with
            | Some notice -> Cmd.ofMsg (SessionMsg(SessionMsg.Told(answer.From, notice)))
            | None -> Cmd.none

        let state, cmd = apply state answer.Reply.Response
        state, Cmd.batch [ cmd; told ]


    let applyFormulary (state: State) (form: Formulary) = { state with Fetches.Formulary = Resolved form }, Cmd.none


    let applyParenteralia (state: State) (par: Parenteralia) =
        { state with Fetches.Parenteralia = Resolved par }, Cmd.none


    /// The interactions notice on the snackbar, and its withdrawal: only the notice itself is
    /// withdrawn, never another message the snackbar shows meanwhile (the record moved on, a
    /// refusal), since the interactions are checked on every answered plan.
    let interactionsNotice n = $"Er zijn %i{n} interactie(s) gevonden"

    let withdrawInteractionsNotice (state: State) =
        if
            state.Ui.Snackbar.Message.StartsWith "Er zijn "
            && state.Ui.Snackbar.Message.EndsWith " interactie(s) gevonden"
        then
            { state with
                Ui.Snackbar.Message = ""
                Ui.Snackbar.Open = false
            }
        else
            state


    let applyInteraction (state: State) (response: Api.InteractionResponse) =
        match response with
        | Api.InteractionResponse.InteractionsChecked interactions ->
            let newState =
                if interactions.Length > 0 then
                    { state with Ui.Snackbar = Snackbar.shown (interactionsNotice interactions.Length) "warning" }
                else
                    withdrawInteractionsNotice state

            { newState with Fetches.Interactions = Resolved interactions }, Cmd.none
        | Api.InteractionResponse.DrugNamesLoaded names ->
            { state with Fetches.InteractionDrugNames = Resolved names }, Cmd.none


    let loadFormulary opened = createApiMsg serverApi.processFormulary opened LoadFormulary


    let loadParenteralia opened =
        createApiMsg serverApi.processParenteralia opened LoadParenteralia


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
    // the session supplies the patient, so it yields the defaults
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


    /// What a "#/session?..." url carries: the Launch MainEHR opened GenPRES with, or the
    /// reason the return from the IdentityProvider refused it.
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


    /// Erase the Launch: replace the launch url with "#/session" in the
    /// address bar and the history entry, so the token survives neither a
    /// reload, the back button nor a copied url. Goes through the History API
    /// directly: Router.navigate would dispatch the navigation event and
    /// re-enter UrlChanged.
    let eraseLaunch () = Browser.Dom.history.replaceState (null, "", "#/session")


    let initialState pat page lang discl =
        {
            Lanes =
                {
                    // the patient follows through UpdatePatient, once the draft is a patient
                    Patient = None
                    // a medication in the url is seeded by UrlChanged, which the router fires on
                    // mount too, once the patient is set
                    OrderContext = OrderContextState.noPatient
                    // the patient reaches the plan through UpdatePatient
                    OrderPlan = OrderPlanState.noPatient
                    Session = SessionState.anonymous
                    Signing = SigningState.idle
                }
            Fetches =
                {
                    NormalValues = HasNotStartedYet
                    BolusMedication = HasNotStartedYet
                    ContinuousMedication = HasNotStartedYet
                    Products = HasNotStartedYet
                    Localization = HasNotStartedYet
                    Hospitals = HasNotStartedYet
                    Settings = HasNotStartedYet
                    Formulary = HasNotStartedYet
                    Parenteralia = HasNotStartedYet
                    Interactions = HasNotStartedYet
                    InteractionDrugNames = HasNotStartedYet
                    DrugNameRetries = 0
                    ServerStatus = HasNotStartedYet
                }
            Admin =
                {
                    IsAuthenticated = false
                    AuthToken = ""
                    LoginAttempt = 0
                    LogFiles = HasNotStartedYet
                    LogAnalysisReport = HasNotStartedYet
                    Reloading = HasNotStartedYet
                }
            Ui =
                {
                    Page = page |> Option.defaultValue LifeSupport
                    ShowDisclaimer = discl
                    Context =
                        {
                            // the server default replaces this once LoadSettings resolves, unless the url chose
                            Localization = (LanguagePolicy.Language.initial lang).Current
                            Hospital = "UMCU"
                        }
                    LanguageChosen = (LanguagePolicy.Language.initial lang).Chosen
                    IsDemo = false
                    ServerError = None
                    EmergencyListFilter = [||]
                    ContinuousMedsFilter = [||]
                    Snackbar = Snackbar.closed
                    PatientDraft = pat
                }
        }


    /// The language as LanguagePolicy sees it, and the state after the policy answered.
    let languageOf (state: State) : LanguagePolicy.Language =
        {
            Current = state.Ui.Context.Localization
            Chosen = state.Ui.LanguageChosen
        }


    let withLanguage (language: LanguagePolicy.Language) (state: State) =
        { state with
            Ui.Context.Localization = language.Current
            Ui.LanguageChosen = language.Chosen
        }


    /// Whether leaving the page would lose work, as the policy tells it from the workbench,
    /// the signing phase and the plan's work.
    let hasUnsignedWork (state: State) =
        UnsignedWorkPolicy.hasUnsignedWork
            (state.Lanes.OrderContext |> OrderContextState.context)
            (state.Lanes.Signing |> SigningState.view)
            (state.Lanes.OrderPlan |> OrderPlanState.work)


    /// Make the key pair, then present the Launch with its public key.
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

        let pat, page, lang, discl, _ = url |> parsePatient

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

        initialState pat page lang discl, cmds


    let applyNormalValues (normalValues: Deferred<NormalValues>) (pat: Patient option) =
        match normalValues, pat with
        | Resolved nv, Some p ->
            p
            |> Patient.applyNormalValues (Some nv.Weights) (Some nv.Heights) (Some nv.NeoWeights) (Some nv.NeoHeights)
            |> Some
        | _ -> pat


    /// An error from the server said and kept: the first three messages, each cut to a
    /// readable length, under the sentence that asks for a reload. The command passes through.
    let processError err (state: State, cmd) =
        let errMsg =
            err
            |> Array.truncate 3
            |> Array.map (fun (s: string) -> if s.Length > 200 then s[..199] + "..." else s)
            |> String.concat "; "

        Logging.error "error" err

        { state with
            Ui.Snackbar = Snackbar.shown "Er ging iets mis, herladen" "error"
            Ui.ServerError = Some $"Server fout: {errMsg}"
        },
        cmd


    /// A sentence on the snackbar, in the severity it is said with.
    let tell message severity (state: State) = { state with Ui.Snackbar = Snackbar.shown message severity }


    /// A term of the signing vocabulary in the language the user reads, falling back to the
    /// English the policy gives.
    let signingTerm (state: State) term =
        Global.getLocalizedTerm
            state.Fetches.Localization
            state.Ui.Context.Localization
            (SigningPolicy.english term)
            term


    /// Every effect a machine returned applied in turn: what each one changes, and the command
    /// it sends, in the order the machine put them in.
    let runEffects apply effects (state: State) =
        let state, cmds =
            effects
            |> List.fold
                (fun (state, cmds) effect ->
                    let state, cmd = state |> apply effect
                    state, cmd :: cmds
                )
                (state, [])

        state, cmds |> List.rev |> Cmd.batch


    /// What one session effect changes, and the command it sends. A transport failure
    /// is a message, never an exception: an Error outcome for a presentation, CloseFailed for
    /// a close that did not reach the server.
    let applySessionEffect (effect: SessionEffect) (state: State) : State * Cmd<Msg> =
        match effect with
        | SessionEffect.CallPresentLaunch(launch, key) ->
            state,
            async {
                try
                    let! outcome = serverApi.processLaunch (Api.LaunchCommand.PresentLaunch(launch, key))
                    return SessionMsg(SessionMsg.Outcome(launch, key, Ok outcome))
                with ex ->
                    return SessionMsg(SessionMsg.Outcome(launch, key, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallGetSession ->
            state,
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
        // the version is open, or the record moved on: each said once, the machine decides
        | SessionEffect.TellVersionOpened head ->
            state
            |> tell (SigningPolicy.versionOpenedSentence (signingTerm state) head) "success",
            Cmd.none
        | SessionEffect.TellMovedOn head ->
            state |> tell (SigningPolicy.movedOnSentence (signingTerm state) head) "warning", Cmd.none
        | SessionEffect.CallOpenVersion(id, from) ->
            state,
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
            state,
            async {
                // the server deletes the cookie whatever its close returns (finally), so an
                // answer of any kind means Closed; only a request that never got there fails
                match! serverApi.processSession Api.SessionCommand.CloseSession |> Async.Catch with
                | Choice1Of2 _ -> return SessionMsg SessionMsg.Closed
                | Choice2Of2 ex -> return SessionMsg(SessionMsg.CloseFailed ex.Message)
            }
            |> Cmd.fromAsync
        | SessionEffect.CallSupplyPin(code, pin) ->
            state,
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
        | SessionEffect.GoTo url -> state, Cmd.ofEffect (fun _ -> Browser.Dom.window.location.assign url)
        | SessionEffect.SetPatient patient -> state, Cmd.ofMsg (UpdatePatient patient)
        // the cart is the version the Session opened with, opened by the plan machine over the
        // patient as UpdatePatient leaves it (normal values applied); the machine keeps the
        // version while that patient is still on its way
        | SessionEffect.LoadCart head -> state, Cmd.ofMsg (OrderPlanMsg(OrderPlanMsg.Version(head, newRequest ())))
        | SessionEffect.KeepKey thumbprint ->
            state,
            Cmd.ofEffect (fun _ ->
                async {
                    match! Keys.keep thumbprint |> Async.Catch with
                    | Choice1Of2() -> ()
                    | Choice2Of2 ex -> Logging.error "could not prune the browser keys" ex.Message
                }
                |> Async.StartImmediate
            )


    /// What one signing effect changes, and the command it sends. The machine names the plan,
    /// the challenge, the PIN, the request id and the key; the OpenedToken comes from the open
    /// Session here, and every answer carries the request id or the key it answers, so the
    /// machine can drop one that belongs to an earlier Session. Without an open Session nothing
    /// is sent: the answer is a refusal.
    let applySigningEffect (effect: SigningEffect) (state: State) : State * Cmd<Msg> =
        let token = tokenOf state.Lanes.Session

        match effect with
        | SigningEffect.CallChallenge(plan, notice, request) ->
            state,
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
            state,
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
        | SigningEffect.RenewToken token -> state, Cmd.ofMsg (SessionMsg(SessionMsg.TokenRenewed token))
        | SigningEffect.EndSession ending -> state, Cmd.ofMsg (SessionMsg(SessionMsg.EndedByServer ending))
        | SigningEffect.SetPatient patient -> state, Cmd.ofMsg (UpdatePatient(Some patient))
        // the plan's work follows the signature: as signed, unless it changed meanwhile
        | SigningEffect.TellSigned(signed, askedOver) ->
            state
            |> tell (SigningPolicy.signedSentence (signingTerm state) signed) "success",
            Cmd.ofMsg (OrderPlanMsg(OrderPlanMsg.Signed askedOver))
        // a refusal because the record moved on is the notice too: the Session keeps the head
        // for the bar
        | SigningEffect.TellRefused(SigningRefusal.Blocked head) ->
            state
            |> tell (SigningPolicy.refusalSentence (signingTerm state) (SigningRefusal.Blocked head)) "warning",
            Cmd.ofMsg (SessionMsg(SessionMsg.Blocked head))
        | SigningEffect.TellRefused refusal ->
            state
            |> tell (SigningPolicy.refusalSentence (signingTerm state) refusal) "warning",
            Cmd.none
        | SigningEffect.TellError reason ->
            Logging.error "could not send the signature to the server" reason

            state |> tell (signingTerm state Terms.``Signing Send Failed``) "error", Cmd.none


    /// What one order-plan effect changes, and the command it sends. A plan call answers under
    /// the request it was sent for, so the machine can drop an answer to an earlier request;
    /// the notice rides on the reply and is told by `update`; a transport failure is an Error
    /// answer, never an exception.
    let applyOrderPlanEffect (effect: OrderPlanEffect) (state: State) : State * Cmd<Msg> =
        match effect with
        | OrderPlanEffect.CallPlan(cmd, request) ->
            let opened = tokenOf state.Lanes.Session

            state,
            async {
                try
                    match!
                        serverApi.processOrderPlan
                            {
                                Opened = opened
                                Command = cmd
                            }
                    with
                    | Ok reply ->
                        return
                            OrderPlanAnswered(
                                request,
                                {
                                    From = opened
                                    Reply = reply
                                }
                            )
                    | Error errs -> return OrderPlanMsg(OrderPlanMsg.Answered(request, Error errs))
                with ex ->
                    return OrderPlanMsg(OrderPlanMsg.Answered(request, Error [| ex.Message |]))
            }
            |> Cmd.fromAsync
        | OrderPlanEffect.CheckInteractions drugs -> state, Cmd.ofMsg (CheckInteractions drugs)
        | OrderPlanEffect.ResetWorkbench -> state, Cmd.ofMsg (OrderContextMsg(OrderContextMsg.Reset(newRequest ())))
        // an order prescribed opens the plan page
        | OrderPlanEffect.GoToPlanPage -> { state with Ui.Page = OrderPlan }, Cmd.none
        | OrderPlanEffect.TellError errs -> (state, Cmd.none) |> processError errs


    /// One command per order-context effect. A workbench call answers under the request it was
    /// sent for; the notice rides on the reply and is told by `update`; a transport failure is
    /// an Error answer. The filter syncs and the page are state changes, made by `update`; only
    /// the loads they need are commands.
    let interpretOrderContextEffect (session: SessionState) (effect: OrderContextEffect) : Cmd<Msg> =
        match effect with
        | OrderContextEffect.CallContext(cmd, ctx, request) ->
            let opened = tokenOf session

            async {
                try
                    match!
                        serverApi.processOrderContext
                            {
                                Opened = opened
                                Command = (cmd, ctx)
                            }
                    with
                    | Ok reply ->
                        return
                            OrderContextAnswered(
                                request,
                                {
                                    From = opened
                                    Reply = reply
                                }
                            )
                    | Error errs -> return OrderContextMsg(OrderContextMsg.Answered(request, Error errs))
                with ex ->
                    return OrderContextMsg(OrderContextMsg.Answered(request, Error [| ex.Message |]))
            }
            |> Cmd.fromAsync
        | OrderContextEffect.SyncFormulary _ -> Cmd.ofMsg (LoadFormulary Started)
        | OrderContextEffect.SyncParenteralia _ -> Cmd.ofMsg (LoadParenteralia Started)
        | OrderContextEffect.GoToLifeSupport
        | OrderContextEffect.TellError _ -> Cmd.none


    /// The patient data received, from the panel, the url or the Session: the estimate applied,
    /// the draft kept for the panel and the lists, and the patient it is, if any, for the
    /// workbench and the plan, which follow it: evaluated for a new one, again over a change.
    /// The draft is a patient with an age, or a measured weight and height; below that there is
    /// no patient: no workbench, no plan.
    let updatePatient (dto: Patient option) (state: State) : State * Cmd<Msg> =
        let dto = dto |> applyNormalValues state.Fetches.NormalValues

        let pat =
            dto
            |> Option.bind (fun dto ->
                match dto |> Patient.validate with
                | Ok pat -> Some pat
                | Error err ->
                    Logging.warning "no patient: the data is below the minimum" err
                    None
            )

        { state with
            Lanes.Patient = pat
            Ui.PatientDraft = dto
            Fetches.Formulary = { Formulary.empty with Patient = pat } |> Resolved
            Fetches.Parenteralia = Parenteralia.empty |> Resolved
            Ui.EmergencyListFilter = [||]
            Ui.ContinuousMedsFilter = [||]
        },
        Cmd.batch
            [
                Cmd.ofMsg (OrderContextMsg(OrderContextMsg.PatientChanged(pat, newRequest ())))
                Cmd.ofMsg (OrderPlanMsg(OrderPlanMsg.PatientChanged(pat, newRequest ())))
                Cmd.ofMsg (LoadFormulary Started)
                Cmd.ofMsg (LoadParenteralia Started)
            ]


    /// A medication chosen without a patient, from the url or a list, is dropped and said: the
    /// patient is part of the filter, so nothing waits for one.
    let noPatientForMedication (state: State) =
        let message =
            Global.getLocalizedTerm
                state.Fetches.Localization
                state.Ui.Context.Localization
                "Voer patient gegevens in"
                Terms.``Patient enter patient data``

        { state with Ui.Snackbar = Snackbar.shown message "warning" }


    let update (msg: Msg) (state: State) =
        // a token the server no longer takes (expired, or a restart): the login is over
        let tokenError err (state, cmd) =
            let state, cmd = processError err (state, cmd)

            if err |> Array.contains "Invalid token" then
                state, Cmd.batch [ cmd; Cmd.ofMsg Logout ]
            else
                state, cmd

        let selectMedicationItem generic indication route doseType state =
            let nonEmpty s = if s = "" then None else Some s

            let ctx =
                { OrderContext.empty with
                    OrderContext.Filter.Indication = indication |> nonEmpty
                    OrderContext.Filter.Generic = Some generic
                    OrderContext.Filter.Route = route |> nonEmpty
                    OrderContext.Filter.DoseType = doseType |> nonEmpty |> Option.map DoseType.doseTypeFromString
                }

            // the medication chosen is evaluated for the patient held; without one it is dropped
            // and said, since the patient is part of the filter
            match state.Lanes.Patient with
            | Some _ ->
                { state with Ui.Page = Prescribe },
                Cmd.ofMsg (OrderContextMsg(OrderContextMsg.Seed(ctx, newRequest ())))
            | None -> noPatientForMedication state, Cmd.none

        match msg with
        | CloseSnackbar -> { state with Ui.Snackbar = Snackbar.closed }, Cmd.none

        | CheckServer Started -> { state with Fetches.ServerStatus = InProgress }, checkServer

        | CheckServer(Finished(Ok _)) ->
            let cmd =
                match state.Fetches.InteractionDrugNames with
                | HasNotStartedYet -> Cmd.ofMsg (LoadInteractionDrugNames Started)
                | _ -> Cmd.none

            { state with
                Fetches.ServerStatus = Resolved true
                Ui.ServerError = None
            },
            cmd

        | CheckServer(Finished(Error err)) ->
            Logging.error "server niet bereikbaar" err

            { state with
                Fetches.ServerStatus = Resolved false
                Ui.ServerError = Some "De server is niet bereikbaar. Controleer of de server is gestart."
            },
            async {
                do! Async.Sleep 5000
                return CheckServer Started
            }
            |> Cmd.fromAsync

        | DismissServerError -> { state with Ui.ServerError = None }, Cmd.none

        | LoadSettings Started -> { state with Fetches.Settings = InProgress }, loadSettings

        | LoadSettings(Finished(Ok settings)) ->
            // the server default counts until the url or the User chooses; a choice made while
            // the settings were in flight wins (LanguagePolicy.onServerDefault)
            { state with
                Fetches.Settings = Resolved settings
                Ui.IsDemo = settings.IsDemo
            }
            |> withLanguage (languageOf state |> LanguagePolicy.Language.onServerDefault settings.Language),
            Cmd.none

        | LoadSettings(Finished(Error err)) ->
            // no settings: the client keeps its own defaults, which is what it did before
            Logging.error "cannot load the server settings" err
            { state with Fetches.Settings = HasNotStartedYet }, Cmd.none

        | Login password ->
            let attempt = state.Admin.LoginAttempt + 1

            { state with Admin.LoginAttempt = attempt },
            Api.AdminCommand.ValidatePassword password
            |> createAdminMsg (fun result -> LoadLoginResult(attempt, result))

        // an answer of an earlier attempt: a login since logged out, or asked again
        | LoadLoginResult(attempt, Finished _) when attempt <> state.Admin.LoginAttempt -> state, Cmd.none

        | LoadLoginResult(_, Finished(Ok resp)) -> applyAdmin state resp

        | LoadLoginResult(_, Finished(Error err)) ->
            ({ state with
                Admin.IsAuthenticated = false
                Admin.AuthToken = ""
             },
             Cmd.none)
            |> processError err

        | LoadLoginResult(_, Started) -> state, Cmd.none

        | Logout ->
            { state with
                Admin.IsAuthenticated = false
                Admin.AuthToken = ""
                Admin.LoginAttempt = state.Admin.LoginAttempt + 1
                Admin.LogFiles = HasNotStartedYet
                Admin.LogAnalysisReport = HasNotStartedYet
                Admin.Reloading = HasNotStartedYet
                Ui.Page =
                    if state.Ui.Page = Settings then
                        LifeSupport
                    else
                        state.Ui.Page
            },
            Cmd.none

        // an answer to a token no longer held (logged out, or logged in again since): dropped,
        // so a late refusal cannot end the new login and a late answer cannot revive the old
        | LoadLogFilesResult(token, Finished _)
        | LoadLogAnalysisResult(token, Finished _)
        | LoadReloadResult(token, Finished _) when token <> state.Admin.AuthToken -> state, Cmd.none

        | ListLogFiles ->
            let token = state.Admin.AuthToken

            // the table shown stays until the answer
            { state with Admin.LogFiles = state.Admin.LogFiles |> Deferred.refresh },
            Api.AdminCommand.ListLogFiles token
            |> createAdminMsg (fun result -> LoadLogFilesResult(token, result))

        | LoadLogFilesResult(_, Finished(Ok resp)) -> applyAdmin state resp

        | LoadLogFilesResult(_, Finished(Error err)) ->
            ({ state with Admin.LogFiles = HasNotStartedYet }, Cmd.none) |> tokenError err

        | LoadLogFilesResult(_, Started) -> state, Cmd.none

        | AnalyzeLogFile fileName ->
            let token = state.Admin.AuthToken

            { state with Admin.LogAnalysisReport = InProgress },
            Api.AdminCommand.AnalyzeLogFile(token, fileName)
            |> createAdminMsg (fun result -> LoadLogAnalysisResult(token, result))

        | LoadLogAnalysisResult(_, Finished(Ok resp)) -> applyAdmin state resp

        | LoadLogAnalysisResult(_, Finished(Error err)) ->
            ({ state with Admin.LogAnalysisReport = HasNotStartedYet }, Cmd.none)
            |> tokenError err

        | LoadLogAnalysisResult(_, Started) -> state, Cmd.none

        | ReloadResources ->
            let token = state.Admin.AuthToken

            { state with Admin.Reloading = InProgress },
            Api.AdminCommand.ReloadResources token
            |> createAdminMsg (fun result -> LoadReloadResult(token, result))

        | LoadReloadResult(_, Finished(Ok resp)) -> applyAdmin state resp

        | LoadReloadResult(_, Finished(Error err)) ->
            ({ state with Admin.Reloading = HasNotStartedYet }, Cmd.none) |> tokenError err

        | LoadReloadResult(_, Started) -> state, Cmd.none

        | AcceptDisclaimer -> { state with Ui.ShowDisclaimer = false }, Cmd.none

        | UpdateLanguage lang ->
            { state with Ui.ShowDisclaimer = true }
            |> withLanguage (languageOf state |> LanguagePolicy.Language.choose lang),
            Cmd.none

        | UpdateHospital hosp ->
            { state with
                Ui.ShowDisclaimer = true
                Ui.Context.Hospital = hosp
            },
            Cmd.none

        | UpdatePage page ->
            let retryDrugNames =
                match state.Fetches.InteractionDrugNames with
                | Resolved _
                | InProgress -> Cmd.none
                | _ -> Cmd.ofMsg (LoadInteractionDrugNames Started)

            // make sure that the order context is not in use
            // i.e. the order context should be "fresh"
            if
                page = ContinuousMeds
                && state.Lanes.OrderContext
                   |> OrderContextState.context
                   |> Option.map (fun ctx -> ctx.Filter.Generic |> Option.isSome)
                   |> Option.defaultValue true
            then
                { state with Ui.Page = page },
                Cmd.batch
                    [
                        Cmd.ofMsg (OrderContextMsg(OrderContextMsg.Reset(newRequest ())))
                        retryDrugNames
                    ]
            else if page = Settings && not state.Admin.IsAuthenticated then
                state, Cmd.none
            else if page = Settings then
                { state with Ui.Page = page }, retryDrugNames
            else
                let loadCmds =
                    match page with
                    | Formulary -> [ Cmd.ofMsg (LoadFormulary Started) ]
                    | Parenteralia -> [ Cmd.ofMsg (LoadParenteralia Started) ]
                    | _ -> []

                { state with Ui.Page = page }, Cmd.batch (retryDrugNames :: loadCmds)

        | UpdatePatient dto -> updatePatient dto state

        | UrlChanged sl ->
            let launchUrl = sl |> parseLaunch

            if launchUrl.IsSome then
                eraseLaunch ()

            let pat, page, lang, discl, med = sl |> parsePatient

            // an open Session supplies the patient: url patient parameters count only while
            // no Session holds one: a launched patient is never assigned from the url. The
            // router fires UrlChanged on mount too, while a Resume may still be in flight,
            // so only Open and Closing block the url patient
            let anonymous =
                match SessionState.view state.Lanes.Session with
                | SessionView.Open _
                | SessionView.Closing _ -> false
                | _ -> true

            let pat = if anonymous then pat else state.Ui.PatientDraft

            // only an `la` parameter changes the language; a navigation keeps the current one
            let language = languageOf state |> LanguagePolicy.Language.onUrl lang

            // the url's patient taken here, not by a message of its own: the workbench learns of
            // it through the commands this yields, which go out before the seed below, so that
            // the seed lands on a patient held
            let state, patientCmd =
                if anonymous then
                    updatePatient pat state
                else
                    state, Cmd.none

            // a medication in the url is seeded over the workbench as it is, for the patient held
            // or the one the url sets; without a patient it is dropped and said, since the
            // patient is part of the filter and nothing waits for one
            let state, seed =
                match med with
                | None -> state, Cmd.none
                | Some m when state.Lanes.Patient.IsSome ->
                    state,
                    state.Lanes.OrderContext
                    |> OrderContextState.context
                    |> Option.defaultValue OrderContext.empty
                    |> OrderContext.setMedication m.indication m.medication m.route m.form m.dosetype
                    |> fun ctx -> Cmd.ofMsg (OrderContextMsg(OrderContextMsg.Seed(ctx, newRequest ())))
                | Some m ->
                    Logging.warning "a medication in the url without a patient is dropped" m.medication
                    noPatientForMedication state, Cmd.none

            { state with
                Ui.ShowDisclaimer = discl
                Ui.Page = page |> Option.defaultValue LifeSupport
                Ui.PatientDraft = pat
                // the path from the state; it also keeps the field apart from the Global.Context type
                Ui.Context.Localization = language.Current
                Ui.LanguageChosen = language.Chosen
            },
            Cmd.batch
                [
                    // the patient's commands first, so that the seed lands on a patient held
                    patientCmd
                    seed
                    launchCmd launchUrl
                ]

        | SessionMsg msg ->
            let session, effects = SessionState.transition msg state.Lanes.Session

            // a failed close is reported only when it was this session's close: a CloseFailed
            // that arrives after a newer launch superseded the Closing session is dropped by
            // the machine and must not put an error over the newer session
            let state =
                match msg, SessionState.view state.Lanes.Session with
                | SessionMsg.CloseFailed reason, SessionView.Closing _ ->
                    Logging.error "could not close the session on the server" reason

                    { state with
                        Ui.Snackbar = Snackbar.shown "De sessie kon niet worden gesloten. Probeer het opnieuw." "error"
                    }
                // the same for a PIN that never reached the server: the form comes back as it was
                | SessionMsg.PinAnswered(Error reason), SessionView.SupplyingPin _ ->
                    Logging.error "could not send the PIN to the server" reason

                    { state with
                        Ui.Snackbar =
                            Snackbar.shown "De pincode kon niet worden verstuurd. Probeer het opnieuw." "error"
                    }
                | _ -> state

            // a signature belongs to an open Session: whatever ends the Session drops it. The
            // plan's work stays: unsigned is unsigned
            let signing =
                match SessionState.view session with
                | SessionView.Open _ -> state.Lanes.Signing
                | _ -> SigningState.idle

            { state with
                Lanes.Session = session
                Lanes.Signing = signing
            }
            |> runEffects applySessionEffect effects

        | SigningMsg msg ->
            let signing, effects = SigningState.transition msg state.Lanes.Signing

            { state with Lanes.Signing = signing } |> runEffects applySigningEffect effects

        | LoadLocalization Started ->
            { state with Fetches.Localization = InProgress },
            Cmd.fromAsync (GoogleDocs.loadLocalization LoadLocalization)

        | LoadLocalization(Finished(Ok terms)) ->

            { state with Fetches.Localization = terms |> Resolved }, Cmd.none

        | LoadLocalization(Finished(Error s)) ->
            Logging.error "cannot load localization" s
            state, Cmd.none

        | LoadNormalValues Started ->
            { state with Fetches.NormalValues = InProgress },
            Cmd.fromAsync (GoogleDocs.loadNormalValues LoadNormalValues)

        | LoadNormalValues(Finished(Ok normalValues)) ->
            { state with Fetches.NormalValues = normalValues |> Resolved },
            Cmd.ofMsg (UpdatePatient state.Ui.PatientDraft)

        | LoadNormalValues(Finished(Error s)) ->
            Logging.error "cannot load normal values" s
            state, Cmd.none


        | LoadBolusMedication Started ->
            { state with Fetches.BolusMedication = InProgress },
            Cmd.fromAsync (GoogleDocs.loadBolusMedication LoadBolusMedication)

        | LoadBolusMedication(Finished(Ok meds)) ->
            { state with
                Fetches.BolusMedication = meds |> Resolved
                Fetches.Hospitals =
                    meds
                    |> List.map _.Hospital
                    |> List.distinct
                    |> List.filter String.notEmpty
                    |> List.toArray
                    |> Resolved
            },
            Cmd.none

        | LoadBolusMedication(Finished(Error s)) ->
            Logging.error "cannot load emergency treatment" s
            state, Cmd.none

        | LoadContinuousMedication Started ->
            { state with Fetches.ContinuousMedication = InProgress },
            Cmd.fromAsync (GoogleDocs.loadContinuousMedication LoadContinuousMedication)

        | LoadContinuousMedication(Finished(Ok meds)) ->

            { state with Fetches.ContinuousMedication = meds |> Resolved }, Cmd.none

        | LoadContinuousMedication(Finished(Error s)) ->
            Logging.error "cannot load continuous medication" s
            state, Cmd.none

        | OnSelectContinuousMedicationItem item ->
            match state.Fetches.ContinuousMedication with
            | Resolved meds ->
                meds
                |> List.tryFind (fun m -> item.EndsWith($".{m.Medication}"))
                |> Option.map (fun m -> selectMedicationItem m.Generic m.Indication "INTRAVENEUS" m.DoseType state)
                |> Option.defaultWith (fun () ->
                    Logging.warning $"could not find continuous medication with item: {item}" item
                    state, Cmd.none
                )
            | _ -> state, Cmd.none

        | UpdateEmergencyListFilter filter -> { state with Ui.EmergencyListFilter = filter }, Cmd.none

        | UpdateContinuousMedsFilter filter -> { state with Ui.ContinuousMedsFilter = filter }, Cmd.none

        | OnSelectEmergencyListItem item ->
            match state.Fetches.BolusMedication with
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
            { state with Fetches.Products = InProgress }, Cmd.fromAsync (GoogleDocs.loadProducts LoadProducts)

        | LoadProducts(Finished(Ok prods)) ->

            { state with Fetches.Products = prods |> Resolved }, Cmd.none

        | LoadProducts(Finished(Error s)) ->
            Logging.error "cannot load products" s
            state, Cmd.none

        | OrderContextMsg msg ->
            // an answer, whatever it says, settles a reload that waited on it
            let state =
                match msg with
                | OrderContextMsg.Answered _ -> settleReload state
                | _ -> state

            let workbench, effects = OrderContextState.transition msg state.Lanes.OrderContext

            // the filter syncs, the page and the snackbar are the interpreter's
            let state =
                effects
                |> List.fold
                    (fun (state: State) effect ->
                        match effect with
                        | OrderContextEffect.SyncFormulary filter ->
                            { state with
                                Fetches.Formulary =
                                    state.Fetches.Formulary
                                    |> Deferred.map (FilterSync.syncFilterToFormulary filter)
                            }
                        | OrderContextEffect.SyncParenteralia filter ->
                            { state with
                                Fetches.Parenteralia =
                                    state.Fetches.Parenteralia
                                    |> Deferred.map (FilterSync.syncFilterToParenteralia filter)
                            }
                        | OrderContextEffect.GoToLifeSupport -> { state with Ui.Page = LifeSupport }
                        | OrderContextEffect.TellError errs ->
                            Logging.warning "order context error" errs

                            { state with
                                Ui.Snackbar =
                                    Snackbar.shown
                                        (errs |> Array.tryHead |> Option.defaultValue "Er ging iets mis")
                                        "warning"
                            }
                        | OrderContextEffect.CallContext _ -> state
                    )
                    state

            { state with Lanes.OrderContext = workbench },
            effects
            |> List.map (interpretOrderContextEffect state.Lanes.Session)
            |> Cmd.batch

        // what the Session is told rides on the reply; the context goes to the machine under
        // the request it answers
        | OrderContextAnswered(request, answer) ->
            processApiMsg
                state
                answer
                (fun state ctx -> state, Cmd.ofMsg (OrderContextMsg(OrderContextMsg.Answered(request, Ok ctx))))

        | OrderPlanMsg msg ->
            let plan, effects = OrderPlanState.transition msg state.Lanes.OrderPlan

            { state with Lanes.OrderPlan = plan } |> runEffects applyOrderPlanEffect effects

        // what the Session is told rides on the reply; the plan goes to the machine under the
        // request it answers
        | OrderPlanAnswered(request, answer) ->
            processApiMsg
                state
                answer
                (fun state plan -> state, Cmd.ofMsg (OrderPlanMsg(OrderPlanMsg.Answered(request, Ok plan))))

        // asked again over the formulary shown, which stays shown until the answer; a second
        // request while one runs is dropped
        | LoadFormulary Started ->
            match state.Fetches.Formulary with
            | InProgress
            | Refreshing _ -> state, Cmd.none
            | _ ->
                let form =
                    match state.Fetches.Formulary with
                    | Resolved form -> { form with Patient = state.Lanes.Patient }
                    | _ -> Formulary.empty

                let cmd = form |> loadFormulary (tokenOf state.Lanes.Session)

                { state with Fetches.Formulary = state.Fetches.Formulary |> Deferred.refresh }, cmd

        // without a patient the formulary is what a reload refreshes, so it settles the reload
        | LoadFormulary(Finished(Ok msg)) ->
            let state =
                if state.Lanes.Patient.IsNone then
                    settleReload state
                else
                    state
            processApiMsg state msg applyFormulary

        | LoadFormulary(Finished(Error err)) ->
            let state =
                if state.Lanes.Patient.IsNone then
                    settleReload state
                else
                    state
            ({ state with Fetches.Formulary = HasNotStartedYet }, Cmd.none)
            |> processError err

        | UpdateFormulary form ->
            let state =
                { state with
                    Fetches.Formulary = Resolved form
                    Lanes.OrderContext =
                        state.Lanes.OrderContext
                        |> OrderContextState.map (FilterSync.syncFormularyToFilter form)
                    Fetches.Parenteralia =
                        state.Fetches.Parenteralia
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
                    // the workbench evaluated again over the filter just synced
                    (state.Lanes.OrderContext
                     |> OrderContextState.context
                     |> Option.map (fun ctx -> Cmd.ofMsg (OrderContextMsg(OrderContextMsg.Seed(ctx, newRequest ()))))
                     |> Option.defaultValue Cmd.none)
                    Cmd.ofMsg (LoadParenteralia Started)
                ]

        | LoadParenteralia Started ->
            match state.Fetches.Parenteralia with
            | InProgress
            | Refreshing _ -> state, Cmd.none
            | _ ->
                let cmd =
                    let par = state.Fetches.Parenteralia |> Deferred.defaultValue Parenteralia.empty

                    loadParenteralia (tokenOf state.Lanes.Session) par

                { state with Fetches.Parenteralia = state.Fetches.Parenteralia |> Deferred.refresh }, cmd

        | LoadParenteralia(Finished(Ok msg)) -> processApiMsg state msg applyParenteralia

        | LoadParenteralia(Finished(Error err)) ->
            ({ state with Fetches.Parenteralia = HasNotStartedYet }, Cmd.none)
            |> processError err

        | UpdateParenteralia par ->
            let state =
                { state with
                    Fetches.Parenteralia = Resolved par
                    Fetches.Formulary =
                        state.Fetches.Formulary
                        |> Deferred.map (fun form ->
                            { form with
                                Indication = None
                                Generic = par.Generic
                                Route = par.Route
                                Form = par.Form
                                DoseType = None
                            }
                        )
                    Lanes.OrderContext =
                        state.Lanes.OrderContext
                        |> OrderContextState.map (FilterSync.syncParenteraliaToFilter par)
                }

            state,
            Cmd.batch
                [
                    Cmd.ofMsg (LoadFormulary Started)
                    // the workbench evaluated again over the filter just synced
                    (state.Lanes.OrderContext
                     |> OrderContextState.context
                     |> Option.map (fun ctx -> Cmd.ofMsg (OrderContextMsg(OrderContextMsg.Seed(ctx, newRequest ()))))
                     |> Option.defaultValue Cmd.none)
                    Cmd.ofMsg (LoadParenteralia Started)
                ]

        | CheckInteractions drugs ->
            if drugs.Length < 2 then
                { withdrawInteractionsNotice state with Fetches.Interactions = HasNotStartedYet }, Cmd.none
            else
                // the rows shown stay until the answer
                { state with Fetches.Interactions = state.Fetches.Interactions |> Deferred.refresh },
                Api.InteractionCommand.CheckInteractions drugs
                |> createApiMsg serverApi.processInteraction (tokenOf state.Lanes.Session) LoadInteractionsResult

        | LoadInteractionsResult(Finished(Ok msg)) -> processApiMsg state msg applyInteraction
        | LoadInteractionsResult(Finished(Error err)) ->
            ({ state with Fetches.Interactions = HasNotStartedYet }, Cmd.none)
            |> processError err
        | LoadInteractionsResult _ -> state, Cmd.none

        | LoadInteractionDrugNames Started ->
            match state.Fetches.InteractionDrugNames with
            | InProgress
            | Refreshing _ -> state, Cmd.none
            | _ ->
                { state with Fetches.InteractionDrugNames = state.Fetches.InteractionDrugNames |> Deferred.refresh },
                Api.InteractionCommand.GetDrugNames
                |> createApiMsg serverApi.processInteraction (tokenOf state.Lanes.Session) LoadInteractionDrugNames

        | LoadInteractionDrugNames(Finished(Ok msg)) ->
            let state, cmd = processApiMsg state msg applyInteraction
            { state with Fetches.DrugNameRetries = 0 }, cmd
        | LoadInteractionDrugNames(Finished(Error _)) ->
            let retries = state.Fetches.DrugNameRetries + 1

            if retries >= 3 then
                { state with
                    Fetches.InteractionDrugNames = HasNotStartedYet
                    Fetches.DrugNameRetries = retries
                    Ui.Snackbar = Snackbar.shown "Interactie medicatie namen konden niet worden geladen" "warning"
                },
                Cmd.none
            else
                { state with
                    Fetches.InteractionDrugNames = HasNotStartedYet
                    Fetches.DrugNameRetries = retries
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
    (state: State, dispatch: Msg -> unit, bm: Deferred<Intervention list>, cm: Deferred<Intervention list>)
    =

    interface AppEnv.ILocalization with
        member _.LocalizationTerms = state.Fetches.Localization

    interface AppEnv.IOrderContext with
        member _.OrderContext = state.Lanes.OrderContext |> OrderContextState.view

        member _.OrderContextMsg(cmd, ctx) =
            OrderContextMsg(OrderContextMsg.Command(cmd, ctx, newRequest ())) |> dispatch

        member _.Dialog = state.Lanes.OrderContext |> OrderContextState.dialog

        member _.Select id = OrderContextMsg(OrderContextMsg.Select id) |> dispatch

    interface AppEnv.IOrderPlan with
        member _.OrderPlan = state.Lanes.OrderPlan |> OrderPlanState.view

        member _.OrderPlanCommand cmd =
            OrderPlanMsg(OrderPlanMsg.Command(cmd, newRequest ())) |> dispatch

        member _.Select id = OrderPlanMsg(OrderPlanMsg.Select id) |> dispatch

        member _.Filter ids =
            OrderPlanMsg(OrderPlanMsg.Filter(ids, newRequest ())) |> dispatch

    interface AppEnv.IPatient with
        member _.Draft = state.Ui.PatientDraft
        member _.UpdatePatient p = UpdatePatient p |> dispatch

    interface AppEnv.IFormulary with
        member _.Formulary = state.Fetches.Formulary
        member _.UpdateFormulary f = UpdateFormulary f |> dispatch

    interface AppEnv.IParenteralia with
        member _.Parenteralia = state.Fetches.Parenteralia
        member _.UpdateParenteralia p = UpdateParenteralia p |> dispatch

    interface AppEnv.IInteractions with
        member _.Interactions = state.Fetches.Interactions
        member _.InteractionDrugNames = state.Fetches.InteractionDrugNames
        member _.CheckInteractions drugs = CheckInteractions drugs |> dispatch

    interface AppEnv.IResources with
        member _.Reload = state.Admin.Reloading
        member _.ReloadResources() = ReloadResources |> dispatch

    interface AppEnv.ISession with
        member _.Session = state.Lanes.Session |> SessionState.view
        member _.Close() = SessionMsg SessionMsg.Close |> dispatch
        member _.Retry() = SessionMsg SessionMsg.Retry |> dispatch

        member _.OpenAnonymously() = SessionMsg SessionMsg.OpenAnonymous |> dispatch

        member _.SupplyPin code pin = SessionMsg(SessionMsg.SupplyPin(code, pin)) |> dispatch

        member _.MovedOn = state.Lanes.Session |> SessionState.movedOn

        member _.OpenVersion id = SessionMsg(SessionMsg.OpenVersion id) |> dispatch

    interface AppEnv.ISigning with
        member _.Signing = state.Lanes.Signing |> SigningState.view

        // one request id per Sign, so the answer lands on this request and no other
        member _.Sign plan =
            SigningMsg(SigningMsg.Sign(plan, OrderPlanState.work state.Lanes.OrderPlan, Guid.NewGuid().ToString()))
            |> dispatch

        member _.Accept() = SigningMsg SigningMsg.Accept |> dispatch

        // one key per confirmation, so the commit takes effect once; the machine keeps it for a retry
        member _.Confirm pin =
            SigningMsg(SigningMsg.Confirm(pin, Guid.NewGuid().ToString())) |> dispatch

        member _.Cancel() = SigningMsg SigningMsg.Cancel |> dispatch

    interface AppEnv.IAuthentication with
        member _.IsAuthenticated = state.Admin.IsAuthenticated
        member _.Login password = Login password |> dispatch
        member _.Logout() = Logout |> dispatch

    interface AppEnv.ILogAnalyzer with
        member _.LogFiles = state.Admin.LogFiles
        member _.LogAnalysisReport = state.Admin.LogAnalysisReport
        member _.ListLogFiles() = ListLogFiles |> dispatch
        member _.AnalyzeLogFile fileName = AnalyzeLogFile fileName |> dispatch

    interface AppEnv.IBolusMedication with
        member _.BolusMedication = bm
        member _.OnSelectBolusMedicationItem s = OnSelectEmergencyListItem s |> dispatch
        member _.BolusMedicationFilter = state.Ui.EmergencyListFilter
        member _.OnBolusMedicationFilterChange f = UpdateEmergencyListFilter f |> dispatch

    interface AppEnv.IContinuousMedication with
        member _.ContinuousMedication = cm

        member _.OnSelectContinuousMedicationItem s = OnSelectContinuousMedicationItem s |> dispatch

        member _.ContinuousMedicationFilter = state.Ui.ContinuousMedsFilter

        member _.OnContinuousMedicationFilterChange f = UpdateContinuousMedsFilter f |> dispatch


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

    // the browser asks before it leaves the page (back, a closed tab, a reload) while there is
    // unsigned work; the listener is added once and reads the latest state through a ref
    let stateRef = React.useRef state
    stateRef.current <- state

    React.useEffectOnce (fun () ->
        let guard (ev: Browser.Types.Event) =
            if hasUnsignedWork stateRef.current then
                ev.preventDefault ()
                // the browser shows its own dialog; older browsers need a returnValue for it
                ev?returnValue <- ""

        window.addEventListener ("beforeunload", guard)

        fun () -> window.removeEventListener ("beforeunload", guard)
    )

    let handleClose =
        fun (_: obj) (reason: string) ->
            if reason <> "clickaway" then
                CloseSnackbar |> dispatch

    let autoHide =
        match state.Ui.Snackbar.Severity with
        | "success"
        | "info" -> 3000 |> box
        | _ -> null

    let bm =
        calculateInterventions EmergencyTreatment.calculate state.Fetches.BolusMedication state.Ui.PatientDraft

    let cm =
        let calc =
            fun _ w meds ->
                match w with
                | Some w' -> ContinuousMedication.calculate w' meds
                | None -> []

        calculateInterventions calc state.Fetches.ContinuousMedication state.Ui.PatientDraft

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
        match state.Ui.ServerError with
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
            // the disclaimer is for anonymous use only: a launched, resuming or
            // refused session never sees it; an anonymous open after a refusal does
            showDisclaimer =
                state.Ui.ShowDisclaimer
                && (
                    match SessionState.view state.Lanes.Session with
                    | SessionView.Anonymous -> true
                    | _ -> false
                )
            isDemo = state.Ui.IsDemo
            acceptDisclaimer = fun _ -> AcceptDisclaimer |> dispatch
            updatePage = UpdatePage >> dispatch
            page = state.Ui.Page
            languages = Localization.languages
            hospitals = state.Fetches.Hospitals
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
                 |> Components.Context.Context state.Ui.Context}
            </Box>
            <div>
                <Snackbar
                    open={state.Ui.Snackbar.Open}
                    autoHideDuration={autoHide}
                    onClose={handleClose}
                >
                    <Alert severity={state.Ui.Snackbar.Severity} onClose={fun _ -> CloseSnackbar |> dispatch} sx={ {| width = "100%" |} }>
                        {state.Ui.Snackbar.Message}
                    </Alert>
                </Snackbar>
            </div>
        </ThemeProvider>
    </React.StrictMode>
    """


let root = ReactDomClient.createRoot (document.getElementById "genpres-app")
root.render (View() |> toReact)
