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
open PatientMachine
open Lanes


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
            // the error on the banner, with the kind of request that raised it
            ServerError: ServerErrorPolicy.ServerError option
            EmergencyListFilter: string[]
            ContinuousMedsFilter: string[]
            Snackbar: Snackbar
            // the start-up has ended: nothing was out and every load the application cannot be
            // used without had loaded, once. It never goes back, since any later request out
            // would read as starting again and the gate would cover the application
            Started: bool
            // the url the app shows, against which a url change is told apart, and the newer url
            // the question about the new and changed orders waits on
            Url: UrlPolicy.UrlState
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
            // a workbench filter met a formulary load under way: the page is asked again once
            // that load has landed and is shown
            FormularyAskAgain: bool
            Parenteralia: Deferred<Parenteralia>
            // the same for the parenteralia page
            ParenteraliaAskAgain: bool
            Interactions: Deferred<DrugInteraction[]>
            // the number of the interaction check under way; an answer to an earlier check is
            // dropped, so it can neither replace the rows nor clear the error of a later one
            InteractionCheck: int
            InteractionDrugNames: Deferred<string[]>
            // the drug names are asked again on a failure, three times
            DrugNameRetries: int
            ServerStatus: Deferred<bool>
            // the start-up loads that failed, which keep the application on hold and are named
            Failed: Busy.Load list
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
            // server has reloaded
            Reloading: Deferred<unit>
        }


    /// The medication a "#/patient?..." url carries, each part when given.
    type UrlMedication =
        {|
            indication: string option
            medication: string option
            route: string option
            form: string option
            dosetype: DoseType option
        |}


    /// What a "#/patient?..." url carries.
    type UrlParts =
        {
            /// The patient, when the url gives a birth date or an age.
            Patient: Patient option
            /// The page, when the url names one.
            Page: Global.Pages option
            /// The language, when the url names one.
            Language: Localization.Locales option
            /// Whether the disclaimer shows.
            Disclaimer: bool
            /// The medication, when the url gives any part of it.
            Medication: UrlMedication option
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
        /// Yes to the question before a url with a patient or a medication: start over on it.
        | LeaveForUrl
        /// No to that question: the url the app shows is put back.
        | StayOnUrl
        | AcceptDisclaimer
        | SessionMsg of SessionMsg
        | SigningMsg of SigningMsg

        | UpdatePage of Global.Pages
        | UpdatePatient of Patient option
        | EditPatient of Patient option
        // the patient: the patient machine's messages
        | PatientMsg of PatientMsg
        // the server's answer to a patient change: the patient goes to its machine and the notice
        // to the Session
        | PatientAnswered of request: string * Answer<Patient>

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
        // the server's answer to a workbench request: the context goes to its machine and the
        // notice to the Session
        | OrderContextAnswered of request: string * Answer<OrderContextResponse>

        // the one plan, nutrition included: the order-plan machine's messages
        | OrderPlanMsg of OrderPlanMsg
        // the server's answer to a plan request: the plan goes to its machine and the notice to
        // the Session
        | OrderPlanAnswered of request: string * Answer<OrderPlan>
        // the prescribe click on the order with this id
        | Prescribe of orderId: string

        | UpdateFormulary of Formulary
        | LoadFormulary of ApiResponse<Formulary>

        | UpdateParenteralia of Parenteralia
        | LoadParenteralia of ApiResponse<Parenteralia>

        | CheckInteractions of string list
        | LoadInteractionsResult of check: int * ApiResponse<Api.InteractionResponse>
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

        | ListLogFiles
        | LoadLogFilesResult of AdminResult
        | AnalyzeLogFile of string
        | LoadLogAnalysisResult of AdminResult
        | ReloadResources
        | LoadReloadResult of AdminResult


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


    /// The patient the workbench and the plan are for: the draft, once it meets the minimum, an
    /// age or a measured weight and height; below that there is no patient. Derived from the
    /// draft whenever it is read, so that the two cannot differ.
    let patientOf (state: State) = state.Lanes.Patient |> PatientState.patient


    /// An admin answer applied. A reload done ends the reload; the pages it refreshes are
    /// started where the answer lands.
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
        | Api.AdminResponse.ResourcesReloaded -> { state with Admin.Reloading = Resolved() }, Cmd.none


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


    /// The interactions of the drugs checked, out from this update on.
    let checkInteractions drugs (state: State) =
        // every check, the empty one too, ends the checks before it
        let check = state.Fetches.InteractionCheck + 1

        if List.length drugs < 2 then
            { withdrawInteractionsNotice state with
                Fetches.Interactions = HasNotStartedYet
                Fetches.InteractionCheck = check
            },
            Cmd.none
        else
            // the rows shown stay until the answer
            { state with
                Fetches.Interactions = state.Fetches.Interactions |> Deferred.refresh
                Fetches.InteractionCheck = check
            },
            Api.InteractionCommand.CheckInteractions drugs
            |> createApiMsg
                serverApi.processInteraction
                (tokenOf state.Lanes.Session)
                (fun result -> LoadInteractionsResult(check, result))


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

    // url needs to be in format: http://localhost:8080/#/patient?by=2&bm=0&bd=1
    // * pg: el (emergency list) cm (continuous medication) pr (prescribe) fm (formulary)
    //   pe (parenteralia)
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
    //
    // The patient, page, language, disclaimer and medication carried by an
    // anonymous "#/patient?..." url. A "#/session..." url carries none of these:
    // the session supplies the patient, so it yields the defaults
    // without a warning.
    let parsePatient sl =
        let none =
            {
                Patient = None
                Page = None
                Language = None
                Disclaimer = true
                Medication = None
            }

        match sl with
        | [] -> none
        | "session" :: _ -> none
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
                | Some s when s = "el" -> Some Global.Pages.LifeSupport
                | Some s when s = "cm" -> Some Global.Pages.ContinuousMeds
                | Some s when s = "pr" -> Some Global.Pages.Prescribe
                | Some s when s = "fm" -> Some Global.Pages.Formulary
                | Some s when s = "pe" -> Some Global.Pages.Parenteralia
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

            // no medication when the url gives no part of it
            let given =
                med.indication.IsSome
                || med.medication.IsSome
                || med.route.IsSome
                || med.form.IsSome
                || med.dosetype.IsSome

            {
                Patient = pat
                Page = page
                Language = lang
                Disclaimer = discl
                Medication = if given then Some med else None
            }

        | _ ->
            // only the route segment: the rest of the url is never logged
            Logging.warning "could not parse url" (sl |> List.head)

            none


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


    let initialState sl pat page lang discl =
        {
            // the url's patient and medication are applied by init, over these lanes
            Lanes = Lanes.initial pat
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
                    FormularyAskAgain = false
                    Parenteralia = HasNotStartedYet
                    ParenteraliaAskAgain = false
                    Interactions = HasNotStartedYet
                    InteractionCheck = 0
                    InteractionDrugNames = HasNotStartedYet
                    DrugNameRetries = 0
                    ServerStatus = HasNotStartedYet
                    Failed = []
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
                    Page = page |> Option.defaultValue Global.Pages.LifeSupport
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
                    Started = false
                    Url = UrlPolicy.UrlState.Shown sl
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


    /// Whether a launched Session is open, which a url with a patient or a medication leaves.
    let launched (state: State) =
        match SessionState.view state.Lanes.Session with
        | SessionView.Open _
        | SessionView.Closing _ -> true
        | _ -> false


    /// Whether a url carries a patient or a medication.
    let seeds (url: UrlParts) = url.Patient.IsSome || url.Medication.IsSome


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


    let applyNormalValues (normalValues: Deferred<NormalValues>) (pat: Patient option) =
        match normalValues, pat with
        | Resolved nv, Some p ->
            p
            |> Patient.applyNormalValues (Some nv.Weights) (Some nv.Heights) (Some nv.NeoWeights) (Some nv.NeoHeights)
            |> Some
        | _ -> pat


    /// An error from the server said and kept: the first three messages, each cut to a
    /// readable length, under the sentence that asks for a reload. The banner keeps the kind of
    /// request that raised it, so the next success of that kind clears it. The command passes
    /// through.
    let processError source err (state: State, cmd) =
        let errMsg =
            err
            |> Array.truncate 3
            |> Array.map (fun (s: string) -> if s.Length > 200 then s[..199] + "..." else s)
            |> String.concat "; "

        Logging.error "error" err

        { state with
            Ui.Snackbar = Snackbar.shown "Er ging iets mis, herladen" "error"
            Ui.ServerError = Some(ServerErrorPolicy.raised source $"Server fout: {errMsg}")
        },
        cmd


    /// A successful answer of source: the banner goes when that source raised it.
    let clearError source (state: State, cmd) =
        { state with Ui.ServerError = state.Ui.ServerError |> ServerErrorPolicy.clearedBy source }, cmd


    /// The formulary asked again over the one shown, which stays shown until the answer, and the
    /// load out from this update on.
    let startFormulary (state: State) =
        // the patient the server answered, also over a formulary answered without one
        let form =
            { (state.Fetches.Formulary |> Deferred.defaultValue Formulary.empty) with
                Patient = state.Lanes.Patient |> PatientState.answered
            }

        { state with Fetches.Formulary = state.Fetches.Formulary |> Deferred.refresh },
        form |> loadFormulary (tokenOf state.Lanes.Session)


    /// The parenteralia asked again over the ones shown, and the load out from this update on.
    let startParenteralia (state: State) =
        let par = state.Fetches.Parenteralia |> Deferred.defaultValue Parenteralia.empty

        { state with Fetches.Parenteralia = state.Fetches.Parenteralia |> Deferred.refresh },
        par |> loadParenteralia (tokenOf state.Lanes.Session)


    /// The workbench filter put on the formulary page and the page loaded for it, out in the same
    /// update, so nothing can be clicked between the answer and the load. While a load of the
    /// page runs, the page is only marked, and asked again once that load has landed.
    let syncFormulary filter (state: State) =
        match state.Fetches.Formulary with
        | InProgress
        | Refreshing _ -> { state with Fetches.FormularyAskAgain = true }, Cmd.none
        | HasNotStartedYet
        | Resolved _ ->
            { state with
                Fetches.Formulary =
                    state.Fetches.Formulary
                    |> Deferred.defaultValue Formulary.empty
                    |> FilterSync.syncFilterToFormulary filter
                    |> Resolved
            }
            |> startFormulary


    /// The workbench filter put on the parenteralia page and the page loaded for it, out in the
    /// same update, or the page marked while a load of it runs.
    let syncParenteralia filter (state: State) =
        match state.Fetches.Parenteralia with
        | InProgress
        | Refreshing _ -> { state with Fetches.ParenteraliaAskAgain = true }, Cmd.none
        | HasNotStartedYet
        | Resolved _ ->
            { state with
                Fetches.Parenteralia =
                    state.Fetches.Parenteralia
                    |> Deferred.defaultValue Parenteralia.empty
                    |> FilterSync.syncFilterToParenteralia filter
                    |> Resolved
            }
            |> startParenteralia


    /// After a formulary load landed and its answer is shown: asked again with the filter the
    /// workbench last answered, when a workbench filter met that load.
    let askFormularyAgain (state: State, cmd) =
        match state.Fetches.FormularyAskAgain, state.Lanes.OrderContext |> OrderContextState.answered with
        | true, Some ctx ->
            let state, again = syncFormulary ctx.Filter { state with Fetches.FormularyAskAgain = false }
            state, Cmd.batch [ cmd; again ]
        | true, None -> { state with Fetches.FormularyAskAgain = false }, cmd
        | false, _ -> state, cmd


    /// After a parenteralia load landed and its answer is shown: asked again with the filter the
    /// workbench last answered, when a workbench filter met that load.
    let askParenteraliaAgain (state: State, cmd) =
        match state.Fetches.ParenteraliaAskAgain, state.Lanes.OrderContext |> OrderContextState.answered with
        | true, Some ctx ->
            let state, again = syncParenteralia ctx.Filter { state with Fetches.ParenteraliaAskAgain = false }
            state, Cmd.batch [ cmd; again ]
        | true, None -> { state with Fetches.ParenteraliaAskAgain = false }, cmd
        | false, _ -> state, cmd


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
        | SessionEffect.TellRefreshFailed ->
            state |> tell (signingTerm state Terms.``Session Refresh Failed``) "warning", Cmd.none
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
        | SessionEffect.CallRefresh from ->
            state,
            async {
                try
                    match! serverApi.processSession Api.SessionCommand.Refresh with
                    | Api.SessionResponse.SessionResp opened -> return SessionMsg(SessionMsg.Refreshed(from, Ok opened))
                    // never an answer to Refresh
                    | _ -> return SessionMsg(SessionMsg.Refreshed(from, Ok None))
                with ex ->
                    return SessionMsg(SessionMsg.Refreshed(from, Error ex.Message))
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
        // the patient and the orders the Session opened with go to their machines in Lanes; nothing
        // is left for the client
        | SessionEffect.SetPatient _
        | SessionEffect.LoadCart _ -> state, Cmd.none
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
        // the token, the ended Session and the patient go to their machines in Lanes; nothing is
        // left for the client
        | SigningEffect.RenewToken _
        | SigningEffect.EndSession _
        | SigningEffect.SetPatient _ -> state, Cmd.none
        | SigningEffect.TellSigned signed ->
            state
            |> tell (SigningPolicy.signedSentence (signingTerm state) signed) "success",
            Cmd.none
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
        // out in the same update as the plan answer it follows
        | OrderPlanEffect.CheckInteractions drugs -> state |> checkInteractions drugs
        | OrderPlanEffect.TellError errs ->
            (state, Cmd.none) |> processError ServerErrorPolicy.ErrorSource.OrderPlan errs


    /// A workbench request, answered under the request id it was sent for.
    let callContext req request (state: State) =
        let opened = tokenOf state.Lanes.Session

        async {
            try
                match!
                    serverApi.processOrderContext
                        {
                            Opened = opened
                            Command = req
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


    /// What one order-context effect changes, and the command it sends. A workbench call
    /// answers under the request it was sent for; the notice rides on the reply and is told by
    /// `update`; a transport failure is an Error answer. A filter sync both syncs what is
    /// already fetched and asks for it again.
    let applyOrderContextEffect (effect: OrderContextEffect) (state: State) : State * Cmd<Msg> =
        match effect with
        | OrderContextEffect.CallContext(cmd, ctx, request) ->
            state, callContext (Api.OrderContextCommand.Command(cmd, ctx)) request state
        | OrderContextEffect.CallPatientChanged(pat, ctx, request) ->
            state, callContext (Api.OrderContextCommand.UpdatePatient(pat, ctx)) request state
        | OrderContextEffect.SyncPages filter ->
            let state, formCmd = syncFormulary filter state
            let state, parCmd = syncParenteralia filter state
            state, Cmd.batch [ formCmd; parCmd ]
        | OrderContextEffect.TellError errs ->
            Logging.warning "order context error" errs

            state
            |> tell (errs |> Array.tryHead |> Option.defaultValue "Er ging iets mis") "warning",
            Cmd.none


    /// The formulary and parenteralia pages for the patient, loaded in the update that lands
    /// it, and the lists' filters cleared; the workbench and the plan get the patient in Lanes.
    let patientPages (pat: Patient option) (state: State) =
        let state =
            { state with
                Fetches.Formulary = { Formulary.empty with Patient = pat } |> Resolved
                Fetches.Parenteralia = Parenteralia.empty |> Resolved
                Ui.EmergencyListFilter = [||]
                Ui.ContinuousMedsFilter = [||]
            }

        let state, formulary = startFormulary state
        let state, parenteralia = startParenteralia state

        state, Cmd.batch [ formulary; parenteralia ]


    /// What one patient effect changes, and the command it sends. A patient change answers under
    /// the request it was sent for; the notice rides on the reply and is told by update; a
    /// transport failure is an Error answer.
    let applyPatientEffect (effect: PatientEffect) (state: State) : State * Cmd<Msg> =
        match effect with
        | PatientEffect.CallPatient(pat, request) ->
            let opened = tokenOf state.Lanes.Session

            state,
            async {
                try
                    match!
                        serverApi.processPatient
                            {
                                Opened = opened
                                Command = Api.PatientCommand.ChangePatient pat
                            }
                    with
                    | Ok reply ->
                        return
                            PatientAnswered(
                                request,
                                {
                                    From = opened
                                    Reply = reply
                                }
                            )
                    | Error errs -> return PatientMsg(PatientMsg.Answered(request, Error errs))
                with ex ->
                    return PatientMsg(PatientMsg.Answered(request, Error [| ex.Message |]))
            }
            |> Cmd.fromAsync
        | PatientEffect.SetPatient pat -> state |> patientPages pat
        | PatientEffect.TellError errs ->
            Logging.warning "patient change error" errs

            state
            |> tell (errs |> Array.tryHead |> Option.defaultValue "Er ging iets mis") "warning",
            Cmd.none


    /// What one effect of the lanes leaves for the client, carried out by its machine's apply.
    let applyLanesEffect effect state =
        match effect with
        | LanesEffect.Signing e -> state |> applySigningEffect e
        | LanesEffect.Patient e -> state |> applyPatientEffect e
        | LanesEffect.Workbench e -> state |> applyOrderContextEffect e
        | LanesEffect.Plan e -> state |> applyOrderPlanEffect e
        | LanesEffect.Session e -> state |> applySessionEffect e
        | LanesEffect.GoToPlanPage -> { state with Ui.Page = Global.Pages.OrderPlan }, Cmd.none


    /// A message through the lanes: the machines' steps recorded in the trail, and what the
    /// effects leave for the client carried out, all in this update.
    let runLanes msg (state: State) =
        let lanes, effects, steps = Lanes.transition newRequest msg state.Lanes
        steps
        |> List.iter (fun step -> StepTrail.record (fun no at -> Trail.lanes no at step))

        { state with Lanes = lanes } |> runEffects applyLanesEffect effects


    /// What the pages show refreshed over reloaded resources, out in the update that lands the
    /// reload's answer: the workbench evaluated again as it is, which takes the formulary and the
    /// parenteralia with it, or those two alone without a patient. A reload seed carries no
    /// choices.
    let refreshPages (state: State) =
        match patientOf state with
        | Some _ ->
            let seed =
                {
                    Source = SeedSource.Reload
                    Indication = None
                    Generic = None
                    Route = None
                    Form = None
                    DoseType = None
                }

            state
            |> runLanes (LanesMsg.Workbench(OrderContextMsg.SeedFilter(seed, newRequest ())))
        | None ->
            let state, formulary = startFormulary state
            let state, parenteralia = startParenteralia state
            state, Cmd.batch [ formulary; parenteralia ]


    /// Whether the patient context is held; the panel cannot change the patient then.
    let patientHeld (state: State) =
        HeldPanelPolicy.held (SessionState.view state.Lanes.Session) (OrderPlanState.changed state.Lanes.OrderPlan)


    /// A medication chosen without a patient, from the url or a list, is dropped and said: the
    /// patient is part of the filter, so nothing waits for one.
    /// Every data load with its reading, the server check excepted.
    let loads (state: State) =
        let reading deferred = deferred |> Deferred.map ignore

        [
            Busy.Load.Settings, reading state.Fetches.Settings
            Busy.Load.Localization, reading state.Fetches.Localization
            Busy.Load.Hospitals, reading state.Fetches.Hospitals
            Busy.Load.NormalValues, reading state.Fetches.NormalValues
            Busy.Load.BolusMedication, reading state.Fetches.BolusMedication
            Busy.Load.ContinuousMedication, reading state.Fetches.ContinuousMedication
            Busy.Load.Products, reading state.Fetches.Products
            Busy.Load.Formulary, reading state.Fetches.Formulary
            Busy.Load.Parenteralia, reading state.Fetches.Parenteralia
            Busy.Load.Interactions, reading state.Fetches.Interactions
            Busy.Load.DrugNames, reading state.Fetches.InteractionDrugNames
            Busy.Load.LogFiles, reading state.Admin.LogFiles
            Busy.Load.LogAnalysis, reading state.Admin.LogAnalysisReport
            Busy.Load.Reload, reading state.Admin.Reloading
        ]


    /// The data loads out.
    let loadsOut (state: State) =
        state
        |> loads
        |> List.choose (fun (load, reading) ->
            match reading with
            | InProgress
            | Refreshing _ -> Some load
            | HasNotStartedYet
            | Resolved _ -> None
        )


    /// The data loads that have loaded.
    let loaded (state: State) =
        state
        |> loads
        |> List.choose (fun (load, reading) ->
            match reading with
            | Resolved _ -> Some load
            | HasNotStartedYet
            | InProgress
            | Refreshing _ -> None
        )


    /// The requests out in the lanes and the loads.
    let busyOut (state: State) =
        Busy.out
            state.Lanes.Patient
            state.Lanes.OrderContext
            state.Lanes.OrderPlan
            state.Lanes.Session
            state.Lanes.Signing
            (loadsOut state)


    /// A start-up load that failed, kept for the gate to name.
    let recordFailed load (state: State) = { state with Fetches.Failed = load :: state.Fetches.Failed }


    /// Where the start-up is; started once, it stays so.
    let startup (state: State) =
        if state.Ui.Started then
            StartupPolicy.Startup.Started
        else
            StartupPolicy.status (busyOut state) (loaded state) state.Fetches.Failed


    /// The start-up marked as ended at the first update in which it is.
    let markStarted (state: State, cmd) =
        match startup state with
        | StartupPolicy.Startup.Started when not state.Ui.Started -> { state with Ui.Started = true }, cmd
        | _ -> state, cmd


    let noPatientForMedication (state: State) =
        let message =
            Global.getLocalizedTerm
                state.Fetches.Localization
                state.Ui.Context.Localization
                "Voer patient gegevens in"
                Terms.``Patient enter patient data``

        { state with Ui.Snackbar = Snackbar.shown message "warning" }


    /// The page, the language and the disclaimer of a url applied, and the url kept as the one
    /// the app shows; no lane changes.
    let applyPage sl (url: UrlParts) (state: State) =
        // only an `la` parameter changes the language; a navigation keeps the current one
        let language = languageOf state |> LanguagePolicy.Language.onUrl url.Language

        { state with
            Ui.ShowDisclaimer = url.Disclaimer
            Ui.Page = url.Page |> Option.defaultValue Global.Pages.LifeSupport
            // the path from the state; it also keeps the field apart from the Global.Context type
            Ui.Context.Localization = language.Current
            Ui.LanguageChosen = language.Chosen
            Ui.Url = UrlPolicy.UrlState.Shown sl
        }


    /// A url applied in full: its patient, its medication, its page and its launch.
    let applyUrl sl (url: UrlParts) (state: State) =
        let launchUrl = sl |> parseLaunch

        if launchUrl.IsSome then
            eraseLaunch ()

        // an open Session supplies the patient: a launched patient is never assigned from the
        // url. The page load and a start-over come here without one; only a launch url can
        // still meet an open or closing Session
        let anonymous =
            match SessionState.view state.Lanes.Session with
            | SessionView.Open _
            | SessionView.Closing _ -> false
            | _ -> true

        let pat =
            if anonymous then
                url.Patient
            else
                state.Lanes.Patient |> PatientState.draft

        // the url's patient taken here, not by a message of its own, and sent as a patient
        // change; while a Session holds the patient the draft stays the one there was
        let state, patientCmd =
            if anonymous then
                state
                |> runLanes (
                    LanesMsg.Patient(PatientMsg.Changed(pat, PatientDraftPolicy.Estimates.Renewed, newRequest ()))
                )
            else
                state, Cmd.none

        // a medication in the url is seeded over the workbench as it is, for the patient held
        // or the one the url sets, which the workbench waits for; without a patient it is
        // dropped and said, since the patient is part of the filter and nothing waits for one
        let state, seed =
            match url.Medication with
            | None -> state, Cmd.none
            | Some m when (patientOf state).IsSome ->
                let seed =
                    {
                        Source = SeedSource.Url
                        Indication = m.indication
                        Generic = m.medication
                        Route = m.route
                        Form = m.form
                        DoseType = m.dosetype
                    }

                state, Cmd.ofMsg (OrderContextMsg(OrderContextMsg.SeedFilter(seed, newRequest ())))
            | Some m ->
                Logging.warning "a medication in the url without a patient is dropped" m.medication
                noPatientForMedication state, Cmd.none

        // the address bar shows "#/session" once a launch url is erased
        state |> applyPage (if launchUrl.IsSome then [ "session" ] else sl) url,
        Cmd.batch
            [
                // a seed that comes before the patient waits for it in the workbench
                patientCmd
                seed
                launchCmd launchUrl
            ]


    /// The url the app shows put back in the address bar. The url change that fires then is the
    /// url the app shows, so it changes nothing.
    let putBack (state: State) =
        state, Cmd.ofEffect (fun _ -> Router.navigate (state.Ui.Url |> UrlPolicy.UrlState.shown |> Array.ofList))


    /// The patient, the workbench, the plan, its interactions and the signing started over on a
    /// url with a patient or a medication, the Session left, and the url applied as at a page
    /// load.
    let startOver sl (url: UrlParts) (state: State) =
        // a check still out is for the old plan, and is dropped
        let state, _ = state |> checkInteractions []
        let state, leave = state |> runLanes (LanesMsg.StartOver(url.Patient, None))
        let state, applied = state |> applyUrl sl url
        state, Cmd.batch [ leave; applied ]


    /// The page load: the url applied at once, so the router's first report, of the same url,
    /// changes nothing; then the Session resumed, or left for a url with a patient or a
    /// medication, and the loads started.
    let init () : State * Cmd<Msg> =
        let sl = Router.currentUrl ()
        let url = sl |> parsePatient

        let state, applied =
            initialState sl url.Patient url.Page url.Language url.Disclaimer
            |> applyUrl sl url

        let cmds =
            Cmd.batch
                [
                    // a page load without a Launch resumes on the cookie (reload, IdP return); one
                    // with a patient or a medication leaves the Session the cookie may hold. A
                    // launch is presented by the url applied
                    match sl |> parseLaunch with
                    | None when seeds url -> Cmd.ofMsg (SessionMsg(SessionMsg.UrlMovedOn None))
                    | None -> Cmd.ofMsg (SessionMsg SessionMsg.Resume)
                    | Some _ -> Cmd.none
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
                    applied
                ]

        state, cmds


#if DEBUG
    /// A state may be traced only once the server has said it serves the demo data: never before the
    /// settings arrive, and never against production. The trail keeps the same gate through
    /// StepTrail.confirmDemo.
    let isTraceable (state: State) =
        match state.Fetches.Settings with
        | Resolved settings
        | Refreshing settings -> settings.IsDemo
        | HasNotStartedYet
        | InProgress -> false
#endif


    let update (msg: Msg) (state: State) =
        // a token the server no longer takes (expired, or a restart): the login is over
        let tokenError source err (state, cmd) =
            let state, cmd = processError source err (state, cmd)

            if err |> Array.contains "Invalid token" then
                state, Cmd.batch [ cmd; Cmd.ofMsg Logout ]
            else
                state, cmd

        // a page's choices seeded over the workbench, only while it has a patient, as a page shows
        // its choices only then
        let seedFromPage source ind gen rte frm dt (state: State) =
            match OrderContextState.patient state.Lanes.OrderContext with
            | Some _ ->
                let seed =
                    {
                        Source = source
                        Indication = ind
                        Generic = gen
                        Route = rte
                        Form = frm
                        DoseType = dt
                    }

                Cmd.ofMsg (OrderContextMsg(OrderContextMsg.SeedFilter(seed, newRequest ())))
            | None -> Cmd.none

        let selectMedicationItem generic indication route doseType state =
            let nonEmpty s = if s = "" then None else Some s

            let seed =
                {
                    Source = SeedSource.MedicationList
                    Indication = indication |> nonEmpty
                    Generic = Some generic
                    Route = route |> nonEmpty
                    Form = None
                    DoseType = doseType |> nonEmpty |> Option.map DoseType.doseTypeFromString
                }

            // the medication chosen is evaluated for the patient held; without one it is dropped
            // and said, since the patient is part of the filter
            match patientOf state with
            | Some _ ->
                { state with Ui.Page = Global.Pages.Prescribe },
                Cmd.ofMsg (OrderContextMsg(OrderContextMsg.SeedFilter(seed, newRequest ())))
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
                Ui.ServerError =
                    state.Ui.ServerError
                    |> ServerErrorPolicy.clearedBy ServerErrorPolicy.ErrorSource.Server
            },
            cmd

        | CheckServer(Finished(Error err)) ->
            Logging.error "server niet bereikbaar" err

            { state with
                Fetches.ServerStatus = Resolved false
                Ui.ServerError =
                    ServerErrorPolicy.raised
                        ServerErrorPolicy.ErrorSource.Server
                        "De server is niet bereikbaar. Controleer of de server is gestart."
                    |> Some
            },
            async {
                do! Async.Sleep 5000
                return CheckServer Started
            }
            |> Cmd.fromAsync

        | DismissServerError -> { state with Ui.ServerError = None }, Cmd.none

        | LoadSettings Started -> { state with Fetches.Settings = InProgress }, loadSettings

        | LoadSettings(Finished(Ok settings)) ->
            StepTrail.confirmDemo settings.IsDemo

            // the server default counts unless the url chose; the User cannot choose before the
            // settings land, since the start-up holds the application until then
            { state with
                Fetches.Settings = Resolved settings
                Ui.IsDemo = settings.IsDemo
            }
            |> withLanguage (languageOf state |> LanguagePolicy.Language.onServerDefault settings.Language),
            Cmd.none

        | LoadSettings(Finished(Error err)) ->
            // no settings: the client keeps its own defaults, which is what it did before
            Logging.error "cannot load the server settings" err
            StepTrail.confirmDemo false
            { state with Fetches.Settings = HasNotStartedYet }, Cmd.none

        | Login password ->
            let attempt = state.Admin.LoginAttempt + 1

            { state with Admin.LoginAttempt = attempt },
            Api.AdminCommand.ValidatePassword password
            |> createAdminMsg (fun result -> LoadLoginResult(attempt, result))

        // an answer of an earlier attempt: a login since logged out, or asked again
        | LoadLoginResult(attempt, Finished _) when attempt <> state.Admin.LoginAttempt -> state, Cmd.none

        | LoadLoginResult(_, Finished(Ok resp)) ->
            applyAdmin state resp |> clearError ServerErrorPolicy.ErrorSource.Login

        | LoadLoginResult(_, Finished(Error err)) ->
            ({ state with
                Admin.IsAuthenticated = false
                Admin.AuthToken = ""
             },
             Cmd.none)
            |> processError ServerErrorPolicy.ErrorSource.Login err

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
                    if state.Ui.Page = Global.Pages.Settings then
                        Global.Pages.LifeSupport
                    else
                        state.Ui.Page
            },
            Cmd.none

        | ListLogFiles ->
            match state.Admin.LogFiles with
            // one listing at a time, so an earlier answer cannot clear the error of a later one
            | InProgress
            | Refreshing _ -> state, Cmd.none
            | _ ->
                // the table shown stays until the answer
                { state with Admin.LogFiles = state.Admin.LogFiles |> Deferred.refresh },
                Api.AdminCommand.ListLogFiles state.Admin.AuthToken
                |> createAdminMsg LoadLogFilesResult

        | LoadLogFilesResult(Finished(Ok resp)) ->
            applyAdmin state resp |> clearError ServerErrorPolicy.ErrorSource.LogFiles

        | LoadLogFilesResult(Finished(Error err)) ->
            ({ state with Admin.LogFiles = HasNotStartedYet }, Cmd.none)
            |> tokenError ServerErrorPolicy.ErrorSource.LogFiles err

        | LoadLogFilesResult(Started) -> state, Cmd.none

        | AnalyzeLogFile fileName ->
            { state with Admin.LogAnalysisReport = InProgress },
            Api.AdminCommand.AnalyzeLogFile(state.Admin.AuthToken, fileName)
            |> createAdminMsg LoadLogAnalysisResult

        | LoadLogAnalysisResult(Finished(Ok resp)) ->
            applyAdmin state resp |> clearError ServerErrorPolicy.ErrorSource.LogAnalysis

        | LoadLogAnalysisResult(Finished(Error err)) ->
            ({ state with Admin.LogAnalysisReport = HasNotStartedYet }, Cmd.none)
            |> tokenError ServerErrorPolicy.ErrorSource.LogAnalysis err

        | LoadLogAnalysisResult(Started) -> state, Cmd.none

        | ReloadResources ->
            let token = state.Admin.AuthToken

            { state with Admin.Reloading = InProgress },
            Api.AdminCommand.ReloadResources token |> createAdminMsg LoadReloadResult

        // the reload ends on its own answer, and the pages it refreshes are out in the same update
        | LoadReloadResult(Finished(Ok resp)) ->
            let state, cmd = applyAdmin state resp |> clearError ServerErrorPolicy.ErrorSource.Reload

            let state, refresh =
                match resp with
                | Api.AdminResponse.ResourcesReloaded -> refreshPages state
                | _ -> state, Cmd.none

            state, Cmd.batch [ cmd; refresh ]

        | LoadReloadResult(Finished(Error err)) ->
            ({ state with Admin.Reloading = HasNotStartedYet }, Cmd.none)
            |> tokenError ServerErrorPolicy.ErrorSource.Reload err

        | LoadReloadResult(Started) -> state, Cmd.none

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

            if page = Global.Pages.Settings && not state.Admin.IsAuthenticated then
                state, Cmd.none
            else if page = Global.Pages.Settings then
                { state with Ui.Page = page }, retryDrugNames
            else
                let loadCmds =
                    match page with
                    | Global.Pages.Formulary -> [ Cmd.ofMsg (LoadFormulary Started) ]
                    | Global.Pages.Parenteralia -> [ Cmd.ofMsg (LoadParenteralia Started) ]
                    | _ -> []

                { state with Ui.Page = page }, Cmd.batch (retryDrugNames :: loadCmds)

        | UpdatePatient dto ->
            state
            |> runLanes (LanesMsg.Patient(PatientMsg.Changed(dto, PatientDraftPolicy.Estimates.Renewed, newRequest ())))

        | EditPatient dto ->
            state
            |> runLanes (LanesMsg.Patient(PatientMsg.Changed(dto, PatientDraftPolicy.Estimates.Kept, newRequest ())))

        | PatientMsg msg -> state |> runLanes (LanesMsg.Patient msg)

        | PatientAnswered(request, answer) ->
            state
            |> runLanes (
                LanesMsg.Answer(
                    LanesMsg.Patient(PatientMsg.Answered(request, Ok answer.Reply.Response)),
                    answer.From,
                    answer.Reply.Notice
                )
            )

        | UrlChanged sl ->
            // a question still open goes with any url change: the url it was about is no longer
            // the one in the address bar. The change is then decided against the url shown alone,
            // so the policy never sees an open question
            let state = { state with Ui.Url = state.Ui.Url |> UrlPolicy.UrlState.close }

            match sl |> parseLaunch with
            // for now a launch url is presented over what there is; it is to wait for the close of
            // the Session there is first (#1224)
            | Some _ -> state |> applyUrl sl (sl |> parsePatient)
            | None ->
                let url = sl |> parsePatient
                let change = UrlPolicy.change state.Ui.Url sl (seeds url)

                let underWay = state.Lanes.Signing |> SigningState.view |> SigningPolicy.underWay

                let action = UrlPolicy.action change underWay (busyOut state) (hasUnsignedWork state) (launched state)

                // what the url change is and does, never the url: it holds patient data
                Logging.log "url change" $"%A{change} -> %A{action}"

                match action with
                | UrlPolicy.UrlAction.ApplyPage -> state |> applyPage sl url, Cmd.none
                | UrlPolicy.UrlAction.Ignore -> state, Cmd.none
                | UrlPolicy.UrlAction.PutBack -> state |> putBack
                | UrlPolicy.UrlAction.Ask -> { state with Ui.Url = state.Ui.Url |> UrlPolicy.UrlState.ask sl }, Cmd.none
                | UrlPolicy.UrlAction.StartOver -> state |> startOver sl url

        | LeaveForUrl ->
            match state.Ui.Url |> UrlPolicy.UrlState.asked with
            | Some sl -> state |> startOver sl (sl |> parsePatient)
            | None -> state, Cmd.none

        | StayOnUrl -> { state with Ui.Url = state.Ui.Url |> UrlPolicy.UrlState.close } |> putBack

        | SessionMsg msg ->
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

            state |> runLanes (LanesMsg.Session msg)

        | SigningMsg msg -> state |> runLanes (LanesMsg.Signing msg)

        | LoadLocalization Started ->
            { state with Fetches.Localization = InProgress },
            Cmd.fromAsync (GoogleDocs.loadLocalization LoadLocalization)

        | LoadLocalization(Finished(Ok terms)) ->

            { state with Fetches.Localization = terms |> Resolved }, Cmd.none

        | LoadLocalization(Finished(Error s)) ->
            Logging.error "cannot load localization" s

            { state with Fetches.Localization = HasNotStartedYet }
            |> recordFailed Busy.Load.Localization,
            Cmd.none

        | LoadNormalValues Started ->
            { state with Fetches.NormalValues = InProgress },
            Cmd.fromAsync (GoogleDocs.loadNormalValues LoadNormalValues)

        // the client's normal values serve the panel summary only; the server estimates
        | LoadNormalValues(Finished(Ok normalValues)) ->
            { state with Fetches.NormalValues = normalValues |> Resolved }, Cmd.none

        | LoadNormalValues(Finished(Error s)) ->
            Logging.error "cannot load normal values" s

            { state with Fetches.NormalValues = HasNotStartedYet }
            |> recordFailed Busy.Load.NormalValues,
            Cmd.none


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

            { state with Fetches.BolusMedication = HasNotStartedYet }
            |> recordFailed Busy.Load.BolusMedication,
            Cmd.none

        | LoadContinuousMedication Started ->
            { state with Fetches.ContinuousMedication = InProgress },
            Cmd.fromAsync (GoogleDocs.loadContinuousMedication LoadContinuousMedication)

        | LoadContinuousMedication(Finished(Ok meds)) ->

            { state with Fetches.ContinuousMedication = meds |> Resolved }, Cmd.none

        | LoadContinuousMedication(Finished(Error s)) ->
            Logging.error "cannot load continuous medication" s

            { state with Fetches.ContinuousMedication = HasNotStartedYet }
            |> recordFailed Busy.Load.ContinuousMedication,
            Cmd.none

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

            { state with Fetches.Products = HasNotStartedYet }
            |> recordFailed Busy.Load.Products,
            Cmd.none

        | OrderContextMsg msg -> state |> runLanes (LanesMsg.Workbench msg)

        | OrderContextAnswered(request, answer) ->
            state
            |> runLanes (
                LanesMsg.Answer(
                    LanesMsg.Workbench(OrderContextMsg.Answered(request, Ok answer.Reply.Response)),
                    answer.From,
                    answer.Reply.Notice
                )
            )

        | OrderPlanMsg msg -> state |> runLanes (LanesMsg.Plan msg)

        | Prescribe orderId -> state |> runLanes (LanesMsg.Prescribe(orderId, newRequest (), newRequest ()))

        | OrderPlanAnswered(request, answer) ->
            // only the answer the plan waits for clears the plan's error: a late answer to a
            // request since replaced is dropped by the machine and says nothing of the one under way
            let clear =
                if state.Lanes.OrderPlan |> OrderPlanState.awaits request then
                    clearError ServerErrorPolicy.ErrorSource.OrderPlan
                else
                    id

            state
            |> runLanes (
                LanesMsg.Answer(
                    LanesMsg.Plan(OrderPlanMsg.Answered(request, Ok answer.Reply.Response)),
                    answer.From,
                    answer.Reply.Notice
                )
            )
            |> clear

        // asked again over the formulary shown, which stays shown until the answer; a second
        // request while one runs is dropped
        | LoadFormulary Started ->
            match state.Fetches.Formulary with
            | InProgress
            | Refreshing _ -> state, Cmd.none
            | _ -> startFormulary state

        | LoadFormulary(Finished(Ok msg)) ->
            processApiMsg state msg applyFormulary
            |> clearError ServerErrorPolicy.ErrorSource.Formulary
            |> askFormularyAgain

        | LoadFormulary(Finished(Error err)) ->
            ({ state with Fetches.Formulary = HasNotStartedYet }, Cmd.none)
            |> processError ServerErrorPolicy.ErrorSource.Formulary err
            |> askFormularyAgain

        | UpdateFormulary form ->
            let state =
                { state with
                    Fetches.Formulary = Resolved form
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
                    // the formulary's choices seeded over the workbench, with the formulary's rule
                    seedFromPage
                        SeedSource.Formulary
                        form.Indication
                        form.Generic
                        form.Route
                        form.Form
                        form.DoseType
                        state
                    Cmd.ofMsg (LoadParenteralia Started)
                ]

        | LoadParenteralia Started ->
            match state.Fetches.Parenteralia with
            | InProgress
            | Refreshing _ -> state, Cmd.none
            | _ -> startParenteralia state

        | LoadParenteralia(Finished(Ok msg)) ->
            processApiMsg state msg applyParenteralia
            |> clearError ServerErrorPolicy.ErrorSource.Parenteralia
            |> askParenteraliaAgain

        | LoadParenteralia(Finished(Error err)) ->
            ({ state with Fetches.Parenteralia = HasNotStartedYet }, Cmd.none)
            |> processError ServerErrorPolicy.ErrorSource.Parenteralia err
            |> askParenteraliaAgain

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
                }

            state,
            Cmd.batch
                [
                    Cmd.ofMsg (LoadFormulary Started)
                    // the parenteralia page's choices seeded over the workbench, with its rule
                    seedFromPage SeedSource.Parenteralia None par.Generic par.Route par.Form None state
                    Cmd.ofMsg (LoadParenteralia Started)
                ]

        | CheckInteractions drugs -> checkInteractions drugs state

        | LoadInteractionsResult(check, Finished _) when check <> state.Fetches.InteractionCheck -> state, Cmd.none

        | LoadInteractionsResult(_, Finished(Ok msg)) ->
            processApiMsg state msg applyInteraction
            |> clearError ServerErrorPolicy.ErrorSource.Interactions
        | LoadInteractionsResult(_, Finished(Error err)) ->
            ({ state with Fetches.Interactions = HasNotStartedYet }, Cmd.none)
            |> processError ServerErrorPolicy.ErrorSource.Interactions err
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


    /// A message applied, and the start-up marked as ended at the first update in which it is.
    let updateStarted msg state = update msg state |> markStarted


    /// The lists calculated for the draft; empty without one, since no patient is not a fetch
    /// under way: the page says what is missing, and the spinner is for a fetch only.
    let calculateInterventions calc meds pat =
        meds
        |> Deferred.bind (fun xs ->
            match pat with
            | None -> Resolved []
            | Some p ->
                let a = p |> Patient.getAgeInYears
                let w = p |> Patient.getWeightInKg
                xs |> calc a w |> Resolved
        )


open Elmish

#if DEBUG
open Elmish.Debug
open Thoth.Json


/// What the trace shows in place of the admin password.
let redacted = "***"


/// The message as the trace records it, with the admin password redacted: a development password may be the one
/// a production server takes. The admin token stays, because a demo server signs it under its own mode, so it
/// never opens a production server.
let private redactMsg (msg: Msg) =
    match msg with
    | Login _ -> Login redacted
    | _ -> msg


/// The console trace, silent while the state is not traceable.
let private consoleTrace (msg: Msg) (state: State) _ =
    if isTraceable state then
        Browser.Dom.console.log ("New message:", redactMsg msg)
        Browser.Dom.console.log ("Updated state:", state)


/// A connection to the Redux DevTools extension that passes nothing on while the state is not traceable: the
/// deflater hands it None for such a state. The history starts with the first traceable state, and every
/// message it passes on is redacted.
let private gatedConnection (inner: Fable.Import.RemoteDev.Connection) =
    let mutable started = false

    { new Fable.Import.RemoteDev.Connection with
        member _.init(_, _) = ()
        member _.subscribe listener = inner.subscribe listener
        member _.unsubscribe = inner.unsubscribe
        member _.error e = inner.error e

        member _.send(msg, state) =
            match unbox<obj option> state with
            | None -> ()
            | Some json when not started ->
                started <- true
                inner.init (json, None)
            | Some json -> inner.send (unbox<Msg> msg |> redactMsg |> box, json)
    }


/// The Redux DevTools debugger over the gated connection, with the coders the debugger itself uses.
let private withGatedDebugger (program: Program<unit, State, Msg, unit>) =
    let coders = Extra.empty |> Extra.withDecimal |> Extra.withInt64 |> Extra.withUInt64

    let encoder = Encode.Auto.generateEncoder<State>(extra = coders)
    let decoder = Decode.Auto.generateDecoder<State>(extra = coders)

    let deflate (state: State) =
        if isTraceable state then Some(encoder state) else None
        |> box

    let inflate (json: obj) =
        match Decode.fromValue "$" decoder json with
        | Ok state -> state
        | Error err -> invalidOp err

    try
        let connection =
            Debugger.connectViaExtension<Msg>(Fable.Import.RemoteDev.ExtensionOptions())
            |> gatedConnection
        program |> Program.withDebuggerUsing deflate inflate connection
    with ex ->
        Browser.Dom.console.error ("[ELMISH DEBUGGER] continuing without the debugger", ex.Message)
        program
#endif


/// The app as an Elmish program. A debug build with GENPRES_LOG on and GENPRES_PROD not 1 traces every message
/// and the new state to the console, hands the history to the Redux DevTools browser extension, and keeps the
/// readable trail of the machine steps, which window.genpresTrail() returns as text; for development testing
/// only. It records nothing until the server settings confirm the demo data, so it never runs against
/// production; a release build records nothing at all.
let private program () =
    let program = Program.mkProgram init updateStarted (fun _ _ -> ())
#if DEBUG
    if StepTrail.isTraceOn () then
        window?genpresTrail <- StepTrail.text
        program |> Program.withTrace consoleTrace |> withGatedDebugger
    else
        program
#else
    program
#endif


type private ConcreteAppEnv
    (state: State, dispatch: Msg -> unit, bm: Deferred<Intervention list>, cm: Deferred<Intervention list>)
    =

    interface AppEnv.ILocalization with
        member _.LocalizationTerms = state.Fetches.Localization

    interface AppEnv.ISettings with
        member _.Settings = state.Fetches.Settings

    interface AppEnv.IStartup with
        member _.Startup = startup state

    interface AppEnv.IBusy with
        member _.Any = state |> busyOut |> Busy.any
        member _.Page page = state |> busyOut |> Busy.page page

    interface AppEnv.IOrderContext with
        member _.OrderContext = state.Lanes.OrderContext |> OrderContextState.view

        member _.OrderContextMsg cmd =
            OrderContextMsg(OrderContextMsg.Command(cmd, newRequest ())) |> dispatch

        member _.Reopen cmd =
            OrderContextMsg(OrderContextMsg.Reopen(cmd, newRequest ())) |> dispatch

        member _.Restore() = OrderContextMsg OrderContextMsg.Restore |> dispatch

        member _.Dialog = state.Lanes.OrderContext |> OrderContextState.dialog

        member _.Select id = OrderContextMsg(OrderContextMsg.Select id) |> dispatch

    interface AppEnv.IOrderPlan with
        member _.OrderPlan = state.Lanes.OrderPlan |> OrderPlanState.view

        member _.Add orderId = Prescribe orderId |> dispatch

        member _.New category =
            OrderPlanMsg(OrderPlanMsg.Change(OrderPlanChange.New category, newRequest ()))
            |> dispatch

        member _.Remove ids =
            OrderPlanMsg(OrderPlanMsg.Change(OrderPlanChange.Remove ids, newRequest ()))
            |> dispatch

        member _.Navigate(id, cmd) =
            OrderPlanMsg(OrderPlanMsg.Change(OrderPlanChange.Navigate(id, cmd), newRequest ()))
            |> dispatch

        member _.Reopen(id, cmd) =
            OrderPlanMsg(OrderPlanMsg.Reopen(id, cmd, newRequest ())) |> dispatch

        member _.Restore() = OrderPlanMsg OrderPlanMsg.Restore |> dispatch

        member _.Select id = OrderPlanMsg(OrderPlanMsg.Select id) |> dispatch

        member _.Filter ids =
            OrderPlanMsg(OrderPlanMsg.Change(OrderPlanChange.Filter ids, newRequest ()))
            |> dispatch

        member _.Changed = state.Lanes.OrderPlan |> OrderPlanState.changed

    interface AppEnv.IPatient with
        member _.Draft = state.Lanes.Patient |> PatientState.draft

        member _.Changing = state.Lanes.Patient |> PatientState.changing

        member _.Estimated =
            state.Lanes.Patient
            |> PatientState.draft
            |> applyNormalValues state.Fetches.NormalValues
        // the panel's edit is ignored while the patient context is held; the Session's patient
        // and a data notice accepted do not come this way
        member _.UpdatePatient p =
            if not (patientHeld state) then
                UpdatePatient p |> dispatch

        member _.EditPatient p =
            if not (patientHeld state) then
                EditPatient p |> dispatch

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

        member _.Refresh() = SessionMsg SessionMsg.Refresh |> dispatch

    interface AppEnv.ISigning with
        member _.Signing = state.Lanes.Signing |> SigningState.view

        member _.Differences = state.Lanes.Signing |> SigningState.differences

        // one request id per Sign, so the answer lands on this request and no other; the orders
        // that differ are taken now, so what reaches the order plan meanwhile leaves them as signed
        member _.Sign plan =
            let differences = state.Lanes.OrderPlan |> OrderPlanState.differences plan

            SigningMsg(SigningMsg.Sign(plan, differences, Guid.NewGuid().ToString()))
            |> dispatch

        member _.Held = patientHeld state

        // held, the plan keeps the data its new and changed orders were composed on
        member _.Accept() = SigningMsg(SigningMsg.Accept(patientHeld state)) |> dispatch

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
    palette: {
        mode: 'light',
        // the four severities the client shows: Valid, Caution, Warning, Alert. MUI's own
        // light-mode shades, all three, so nothing derived replaces a built-in one
        success: { main: '#2e7d32', light: '#4caf50', dark: '#1b5e20' },
        info: { main: '#0288d1', light: '#03a9f4', dark: '#01579b' },
        warning: { main: '#ed6c02', light: '#ff9800', dark: '#e65100' },
        error: { main: '#d32f2f', light: '#ef5350', dark: '#c62828' },
        // the tint behind a caution, a warning or an alert, as an alert box has it
        severityBg: { caution: '#e5f6fd', warning: '#fff4e5', alert: '#fdeded' },
        // the accent the tables and headers use
        primary: { main: '#1976d2', light: '#42a5f5', dark: '#1565c0' },
    },
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
    palette: {
        mode: 'light',
        // the four severities the client shows: Valid, Caution, Warning, Alert. MUI's own
        // light-mode shades, all three, so nothing derived replaces a built-in one
        success: { main: '#2e7d32', light: '#4caf50', dark: '#1b5e20' },
        info: { main: '#0288d1', light: '#03a9f4', dark: '#01579b' },
        warning: { main: '#ed6c02', light: '#ff9800', dark: '#e65100' },
        error: { main: '#d32f2f', light: '#ef5350', dark: '#c62828' },
        // the tint behind a caution, a warning or an alert, as an alert box has it
        severityBg: { caution: '#e5f6fd', warning: '#fff4e5', alert: '#fdeded' },
        // the accent the tables and headers use
        primary: { main: '#1976d2', light: '#42a5f5', dark: '#1565c0' },
    },
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
    let state, dispatch = React.useElmish (program, [||])
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
        calculateInterventions
            EmergencyTreatment.calculate
            state.Fetches.BolusMedication
            (state.Lanes.Patient |> PatientState.draft)

    let cm =
        let calc =
            fun _ w meds ->
                match w with
                | Some w' -> ContinuousMedication.calculate w' meds
                | None -> []

        calculateInterventions calc state.Fetches.ContinuousMedication (state.Lanes.Patient |> PatientState.draft)

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
        | Some error ->
            Components.Notice.View
                {|
                    kind = Components.Notice.Kind.Error
                    title = Some "Server probleem"
                    message = error.Message
                    action = None
                    onClose = Some(fun () -> dispatch DismissServerError)
                |}
        | None -> null

    let getTerm = Global.getLocalizedTerm state.Fetches.Localization state.Ui.Context.Localization

    // the question before a url with a patient or a medication: it leaves the launched Session,
    // or drops the work not signed. The browser asks the second itself for a reload or a closed
    // tab, but not for a change of the url
    let title, text =
        if launched state then
            Terms.``Url Leave Session Title`` |> getTerm "Sessie verlaten?",
            Terms.``Url Leave Session Text``
            |> getTerm
                "De sessie met de patiënt uit het EPD wordt gesloten en de patiënt uit de url wordt gebruikt, zonder sessie. Nieuwe en gewijzigde orders en de medicatie die wordt voorgeschreven gaan verloren. Wilt u doorgaan?"
        else
            Terms.``Url Leave Title`` |> getTerm "Orderplan verlaten?",
            Terms.``Url Leave Text``
            |> getTerm
                "De nieuwe en gewijzigde orders en de medicatie die wordt voorgeschreven gaan verloren. Wilt u doorgaan?"

    let leaveDialog =
        Components.ConfirmDialog.View
            {|
                isOpen = (state.Ui.Url |> UrlPolicy.UrlState.asked).IsSome
                title = title
                text = text
                confirmLabel = Terms.``Url Leave`` |> getTerm "Verlaten"
                cancelLabel = Terms.Cancel |> getTerm "Annuleren"
                onConfirm = fun () -> LeaveForUrl |> dispatch
                onCancel = fun () -> StayOnUrl |> dispatch
            |}

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

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box sx={sx}>
                <CssBaseline />
                {serverErrorBanner}
                {Components.Router.View {| onUrlChanged = UrlChanged >> dispatch |}}
                {leaveDialog}
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
