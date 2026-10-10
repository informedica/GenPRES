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


    type State =
        {
            // the lanes
            Lanes: LanesState
            // the start-up loads
            Loader: LoaderMachine.LoaderState
            // the admin login and what it fetches
            Admin: AdminMachine.AdminState
            // the page, the language, the snackbar, the error banner and the rest around the pages
            Shell: ShellMachine.ShellState
            // the url the app shows, against which a url change is told apart, and the newer url
            // the question about leaving waits on
            Url: UrlPolicy.UrlState
        }


    type Msg =
        | UrlChanged of string list
        /// Yes to the question before a url with a patient, a medication or a launch: start over on
        /// it.
        | LeaveForUrl
        /// No to that question: the url the app shows is put back.
        | StayOnUrl
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

        // the start-up loads: the loads machine's messages
        | LoaderMsg of LoaderMachine.LoaderMsg
        // the admin login and what it fetches: the admin machine's messages
        | AdminMsg of AdminMachine.AdminMsg
        | OnSelectContinuousMedicationItem of string
        | OnSelectEmergencyListItem of string

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

        // the page, the language, the snackbar and the rest around the pages: the shell's messages
        | ShellMsg of ShellMachine.ShellMsg


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


    /// The OpenedToken the Session holds, sent with every computing request; none
    /// without an open Session.
    let tokenOf = SessionState.token


    /// A request id, minted at dispatch, so that an answer can name the request it answers.
    let newRequest () = Guid.NewGuid().ToString()


    /// The patient the workbench and the plan are for: the draft, once it meets the minimum, an
    /// age or a measured weight and height; below that there is no patient. Derived from the
    /// draft whenever it is read, so that the two cannot differ.
    let patientOf (state: State) = state.Lanes.Patient |> PatientState.patient


    /// What the url of these segments carries, at this moment; the parts that did not parse are
    /// logged by name, never by value, since the values are patient data.
    let parseUrl sl =
        let url = sl |> Url.parse DateTime.Now

        for part in url.NotParsed do
            match part with
            | Url.UrlPart.Patient parameters -> Logging.warning "could not parse url to patient" parameters
            | Url.UrlPart.Route segment -> Logging.warning "could not parse url" segment

        url


    /// Erase the Launch: replace the launch url with "#/session" in the
    /// address bar and the history entry, so the token survives neither a
    /// reload, the back button nor a copied url. Goes through the History API
    /// directly: Router.navigate would dispatch the navigation event and
    /// re-enter UrlChanged.
    let eraseLaunch () = Browser.Dom.history.replaceState (null, "", "#/session")


    let initialState sl (url: Url.UrlParts) =
        {
            // the url's patient and medication are applied by init, over these lanes
            Lanes = Lanes.initial url.Patient
            Loader = LoaderMachine.LoaderState.initial
            Admin = AdminMachine.AdminState.initial
            Shell = ShellMachine.ShellState.initial url
            Url = UrlPolicy.UrlState.Shown sl
        }


    /// Whether leaving the page would lose work, as the policy tells it from the workbench,
    /// the signing phase and the plan's work.
    let hasUnsignedWork (state: State) =
        UnsignedWorkPolicy.hasUnsignedWork
            (state.Lanes.OrderContext |> OrderContextState.context)
            (state.Lanes.Signing |> SigningState.view)
            (state.Lanes.OrderPlan |> OrderPlanState.work)


    /// Whether a launched Session is open, which a url with a patient, a medication or a launch
    /// leaves.
    let launched (state: State) =
        match SessionState.view state.Lanes.Session with
        | SessionView.Open _
        | SessionView.Closing _ -> true
        | _ -> false


    /// Whether a url carries a patient, a medication or a launch.
    let seeds (url: Url.UrlParts) =
        url.Patient.IsSome
        || url.Medication.IsSome
        || (
            match url.Launch with
            | Some(Url.LaunchUrl.Launch _) -> true
            | _ -> false
        )


    /// Make the key pair, then present the Launch with its public key.
    /// A browser that cannot make a key cannot launch; that is reported as a missing browser
    /// identity, the refusal whose text asks for a retry and then a relaunch.
    let presentLaunch (launch: Launch) : Cmd<Msg> =
        async {
            match! Keys.generate () |> Async.Catch with
            | Choice1Of2 key -> return SessionMsg(SessionMsg.PresentLaunch(launch, key))
            | Choice2Of2 ex ->
                Logging.error "could not make the browser key pair" ex.Message
                return SessionMsg(SessionMsg.LaunchRefused LaunchRefusal.NoBrowserIdentity)
        }
        |> Cmd.fromAsync


    /// The session command a "#/session?..." url asks for; a plain url asks for nothing.
    let launchCmd (launchUrl: Url.LaunchUrl option) : Cmd<Msg> =
        match launchUrl with
        | Some(Url.LaunchUrl.Launch launch) -> presentLaunch launch
        | Some(Url.LaunchUrl.Refused refusal) -> Cmd.ofMsg (SessionMsg(SessionMsg.LaunchRefused refusal))
        | None -> Cmd.none


    let applyNormalValues (normalValues: Deferred<NormalValues>) (pat: Patient option) =
        match normalValues, pat with
        | Resolved nv, Some p ->
            p
            |> Patient.applyNormalValues (Some nv.Weights) (Some nv.Heights) (Some nv.NeoWeights) (Some nv.NeoHeights)
            |> Some
        | _ -> pat


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


    /// What one shell effect sends: the page shown goes to the loads, as a message of its own.
    let applyShellEffect effect (state: State) =
        match effect with
        | ShellMachine.ShellEffect.PageShown page ->
            state, Cmd.ofMsg (LoaderMsg(LoaderMachine.LoaderMsg.PageShown page))


    /// The shell machine run on a message: its state kept and its effects carried out.
    let runShell msg (state: State) =
        let shell, effects = state.Shell |> ShellMachine.transition msg
        StepTrail.record (fun no at -> Trail.shell no at msg (shell, effects))
        { state with Shell = shell } |> runEffects applyShellEffect effects


    /// An alert on the snackbar.
    let alerted alert (state: State) = state |> runShell (ShellMachine.ShellMsg.AlertRaised alert)


    /// A failed request of source, logged under this text, and raised on the error banner.
    let failed text source errs (state: State) =
        Logging.error text errs
        state |> runShell (ShellMachine.ShellMsg.ServerErrorRaised(source, errs))


    /// A successful answer of source, which clears the banner it raised.
    let succeeded source (state: State) =
        state |> runShell (ShellMachine.ShellMsg.ServerErrorCleared source)


    /// What a failed start-up load is logged as; never in the trail.
    let loadFailed landing =
        match landing with
        | LoaderMachine.Landing.Settings(Error err) -> Some("cannot load the server settings", err)
        | LoaderMachine.Landing.Localization(Error err) -> Some("cannot load localization", err)
        | LoaderMachine.Landing.NormalValues(Error err) -> Some("cannot load normal values", err)
        | LoaderMachine.Landing.BolusMedication(Error err) -> Some("cannot load emergency treatment", err)
        | LoaderMachine.Landing.ContinuousMedication(Error err) -> Some("cannot load continuous medication", err)
        | LoaderMachine.Landing.Products(Error err) -> Some("cannot load products", err)
        | _ -> None


    /// What one loader effect changes, and the command it sends: an alert on the snackbar, the
    /// error banner raised or cleared, the notice to the Session, a call whose answer comes back as
    /// the loader machine's message, or a wait.
    let applyLoaderEffect effect (state: State) =
        let landed landing result = LoaderMsg(LoaderMachine.LoaderMsg.Landed(landing result))
        let opened = tokenOf state.Lanes.Session

        let later seconds msg =
            async {
                do! Async.Sleep(seconds * 1000)
                return LoaderMsg msg
            }
            |> Cmd.fromAsync

        match effect with
        | LoaderMachine.LoaderEffect.FetchSettings ->
            state,
            Cmd.OfAsync.either
                serverApi.getSettings
                ()
                (Ok >> landed LoaderMachine.Landing.Settings)
                (fun ex -> Error ex.Message |> landed LoaderMachine.Landing.Settings)
        | LoaderMachine.LoaderEffect.FetchLocalization ->
            state, Cmd.OfAsync.perform GoogleDocs.loadLocalization () (landed LoaderMachine.Landing.Localization)
        | LoaderMachine.LoaderEffect.FetchNormalValues ->
            state, Cmd.OfAsync.perform GoogleDocs.loadNormalValues () (landed LoaderMachine.Landing.NormalValues)
        | LoaderMachine.LoaderEffect.FetchBolusMedication ->
            state, Cmd.OfAsync.perform GoogleDocs.loadBolusMedication () (landed LoaderMachine.Landing.BolusMedication)
        | LoaderMachine.LoaderEffect.FetchContinuousMedication ->
            state,
            Cmd.OfAsync.perform
                GoogleDocs.loadContinuousMedication
                ()
                (landed LoaderMachine.Landing.ContinuousMedication)
        | LoaderMachine.LoaderEffect.FetchProducts ->
            state, Cmd.OfAsync.perform GoogleDocs.loadProducts () (landed LoaderMachine.Landing.Products)
        | LoaderMachine.LoaderEffect.FetchFormulary form ->
            state,
            Cmd.OfAsync.perform
                serverApi.processFormulary
                {
                    Opened = opened
                    Command = form
                }
                (landed (fun result -> LoaderMachine.Landing.Formulary(opened, result)))
        | LoaderMachine.LoaderEffect.FetchParenteralia par ->
            state,
            Cmd.OfAsync.perform
                serverApi.processParenteralia
                {
                    Opened = opened
                    Command = par
                }
                (landed (fun result -> LoaderMachine.Landing.Parenteralia(opened, result)))
        | LoaderMachine.LoaderEffect.SeedWorkbench seed ->
            state, Cmd.ofMsg (OrderContextMsg(OrderContextMsg.SeedFilter(seed, newRequest ())))
        | LoaderMachine.LoaderEffect.FetchInteractions(check, drugs) ->
            state,
            Cmd.OfAsync.perform
                serverApi.processInteraction
                {
                    Opened = opened
                    Command = Api.InteractionCommand.CheckInteractions drugs
                }
                (landed (fun result -> LoaderMachine.Landing.Interactions(check, opened, result)))
        | LoaderMachine.LoaderEffect.FetchDrugNames ->
            state,
            Cmd.OfAsync.perform
                serverApi.processInteraction
                {
                    Opened = opened
                    Command = Api.InteractionCommand.GetDrugNames
                }
                (landed (fun result -> LoaderMachine.Landing.DrugNames(opened, result)))
        | LoaderMachine.LoaderEffect.CheckServer ->
            state,
            Cmd.OfAsync.either
                serverApi.testApi
                ()
                (fun _ -> LoaderMsg(LoaderMachine.LoaderMsg.ServerChecked(Ok())))
                (fun ex -> LoaderMsg(LoaderMachine.LoaderMsg.ServerChecked(Error ex.Message)))
        | LoaderMachine.LoaderEffect.CheckServerLater seconds ->
            state, LoaderMachine.LoaderMsg.CheckServer |> later seconds
        | LoaderMachine.LoaderEffect.AskAgainLater(load, seconds) ->
            state, LoaderMachine.LoaderMsg.Start load |> later seconds
        | LoaderMachine.LoaderEffect.Alert alert -> state |> alerted alert
        | LoaderMachine.LoaderEffect.WithdrawInteractionsFound ->
            state |> runShell ShellMachine.ShellMsg.WithdrawInteractionsFound
        | LoaderMachine.LoaderEffect.Failed(ServerErrorPolicy.ErrorSource.Server as source, errs) ->
            state |> failed "server niet bereikbaar" source errs
        | LoaderMachine.LoaderEffect.Failed(source, errs) -> state |> failed "error" source errs
        | LoaderMachine.LoaderEffect.Succeeded source -> state |> succeeded source
        | LoaderMachine.LoaderEffect.NoticeReceived(from, notice) ->
            state, Cmd.ofMsg (SessionMsg(SessionMsg.NoticeReceived(from, notice)))


    /// A message through the loader machine: the step recorded in the trail and its effects
    /// carried out. The settings landed go to the shell, for the language default and the demo
    /// flag.
    let runLoader msg (state: State) =
        match msg with
        | LoaderMachine.LoaderMsg.Landed landing ->
            landing |> loadFailed |> Option.iter (fun (text, err) -> Logging.error text err)
        | _ -> ()

        let loader, effects = state.Loader |> LoaderMachine.transition msg
        StepTrail.record (fun no at -> Trail.loader no at msg (loader, effects))
        let state = { state with Loader = loader }

        let state, settled =
            match msg with
            | LoaderMachine.LoaderMsg.Landed(LoaderMachine.Landing.Settings(Ok settings)) ->
                StepTrail.confirmDemo settings.IsDemo
                state |> runShell (ShellMachine.ShellMsg.SettingsLanded settings)
            // no settings: the client keeps its own defaults, which is what it did before
            | LoaderMachine.LoaderMsg.Landed(LoaderMachine.Landing.Settings(Error _)) ->
                StepTrail.confirmDemo false
                state, Cmd.none
            | _ -> state, Cmd.none

        let state, cmd = state |> runEffects applyLoaderEffect effects
        state, Cmd.batch [ settled; cmd ]


    /// What one admin effect changes, and the command it sends: a call under the token, whose
    /// answer comes back as the admin machine's message, the reload done to the loads, the page
    /// left on a logout, an alert, or the error banner raised or cleared.
    let applyAdminEffect effect (state: State) =
        let call command landing =
            Cmd.OfAsync.perform serverApi.processAdmin command (landing >> AdminMachine.AdminMsg.Landed >> AdminMsg)

        match effect with
        | AdminMachine.AdminEffect.ValidatePassword(attempt, password) ->
            state,
            call
                (Api.AdminCommand.ValidatePassword password)
                (fun result -> AdminMachine.Landing.Login(attempt, result))
        | AdminMachine.AdminEffect.FetchLogFiles token ->
            state, call (Api.AdminCommand.ListLogFiles token) AdminMachine.Landing.LogFiles
        | AdminMachine.AdminEffect.FetchLogAnalysis(token, fileName) ->
            state, call (Api.AdminCommand.AnalyzeLogFile(token, fileName)) AdminMachine.Landing.LogAnalysis
        | AdminMachine.AdminEffect.Reload token ->
            state, call (Api.AdminCommand.ReloadResources token) AdminMachine.Landing.Reload
        // the pages the reload refreshes are out in the same update
        | AdminMachine.AdminEffect.ReloadDone -> state |> runLoader LoaderMachine.LoaderMsg.ResourcesReloaded
        | AdminMachine.AdminEffect.LoggedOut -> state |> runShell ShellMachine.ShellMsg.LoggedOut
        | AdminMachine.AdminEffect.Alert alert -> state |> alerted alert
        | AdminMachine.AdminEffect.Failed(source, errs) -> state |> failed "error" source errs
        | AdminMachine.AdminEffect.Succeeded source -> state |> succeeded source


    /// The admin machine run on a message: its state kept and its effects carried out.
    let runAdmin msg (state: State) =
        let admin, effects = state.Admin |> AdminMachine.transition msg
        StepTrail.record (fun no at -> Trail.admin no at msg (admin, effects))
        { state with Admin = admin } |> runEffects applyAdminEffect effects


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
                    return SessionMsg(SessionMsg.LaunchOutcome(launch, key, Ok outcome))
                with ex ->
                    return SessionMsg(SessionMsg.LaunchOutcome(launch, key, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallResume ->
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
        | SessionEffect.TellSignedPlanOpened head -> state |> alerted (Alert.Alert.SignedPlanOpened head)
        | SessionEffect.TellPatientRefreshFailed -> state |> alerted Alert.Alert.PatientRefreshFailed
        | SessionEffect.TellNewerSignedPlan head -> state |> alerted (Alert.Alert.NewerSignedPlan head)
        | SessionEffect.CallOpenSignedPlan(id, from) ->
            state,
            async {
                try
                    match! serverApi.processSession (Api.SessionCommand.OpenVersion id) with
                    | Api.SessionResponse.SessionResp opened ->
                        return SessionMsg(SessionMsg.SignedPlanOpened(from, Ok opened))
                    // never an answer to OpenVersion
                    | _ -> return SessionMsg(SessionMsg.SignedPlanOpened(from, Ok None))
                with ex ->
                    return SessionMsg(SessionMsg.SignedPlanOpened(from, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallRefreshPatient from ->
            state,
            async {
                try
                    match! serverApi.processSession Api.SessionCommand.Refresh with
                    | Api.SessionResponse.SessionResp opened ->
                        return SessionMsg(SessionMsg.PatientRefreshed(from, Ok opened))
                    // never an answer to Refresh
                    | _ -> return SessionMsg(SessionMsg.PatientRefreshed(from, Ok None))
                with ex ->
                    return SessionMsg(SessionMsg.PatientRefreshed(from, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallCloseSession ->
            state,
            async {
                // the server deletes the cookie whatever its close returns (finally), so an
                // answer of any kind means SessionClosed; only a request that never got there fails
                match! serverApi.processSession Api.SessionCommand.CloseSession |> Async.Catch with
                | Choice1Of2 _ -> return SessionMsg SessionMsg.SessionClosed
                | Choice2Of2 ex ->
                    Logging.error "could not close the session on the server" ex.Message
                    return SessionMsg(SessionMsg.CloseFailed ex.Message)
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
                    Logging.error "could not send the PIN to the server" ex.Message
                    return SessionMsg(SessionMsg.PinAnswered(Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.Alert alert -> state |> alerted alert
        | SessionEffect.GoToIdentityProvider url ->
            state, Cmd.ofEffect (fun _ -> Browser.Dom.window.location.assign url)
        // the patient and the orders the Session opened with go to their machines in Lanes; nothing
        // is left for the client
        | SessionEffect.SetPatientData _
        | SessionEffect.LoadSignedPlan _ -> state, Cmd.none
        | SessionEffect.KeepBrowserKey thumbprint ->
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
        // the token and the ended Session go to the Session in Lanes; nothing is left for the
        // client
        | SigningEffect.RenewSessionToken _
        | SigningEffect.EndSession _ -> state, Cmd.none
        | SigningEffect.TellSigned signed -> state |> alerted (Alert.Alert.OrderPlanSigned signed)
        | SigningEffect.TellRefused refusal -> state |> alerted (Alert.Alert.SigningRefused refusal)
        | SigningEffect.TellError reason ->
            Logging.error "could not send the signature to the server" reason
            state |> alerted Alert.Alert.SigningSendFailed


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
        | OrderPlanEffect.CheckInteractions drugs ->
            state |> runLoader (LoaderMachine.LoaderMsg.CheckInteractions drugs)
        | OrderPlanEffect.TellError errs -> state |> failed "error" ServerErrorPolicy.ErrorSource.OrderPlan errs
        | OrderPlanEffect.TellAnswered -> state |> succeeded ServerErrorPolicy.ErrorSource.OrderPlan


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
        | OrderContextEffect.CallContext(sent, request) -> state, callContext sent request state
        | OrderContextEffect.SyncPages filter -> state |> runLoader (LoaderMachine.LoaderMsg.FilterAnswered filter)
        | OrderContextEffect.TellError errs ->
            Logging.warning "order context error" errs

            state
            |> alerted (Alert.Alert.WorkbenchFailed(errs |> Array.tryHead |> Option.defaultValue "Er ging iets mis"))


    /// The formulary and parenteralia pages for the patient, loaded in the update that lands
    /// it, and the lists' filters cleared; the workbench and the plan get the patient in Lanes.
    let patientPages (pat: Patient option) (state: State) =
        let state, cleared = state |> runShell ShellMachine.ShellMsg.ListFiltersCleared
        let state, pages = state |> runLoader (LoaderMachine.LoaderMsg.PatientSet pat)
        state, Cmd.batch [ cleared; pages ]


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
        | PatientEffect.SetPatientData pat -> state |> patientPages pat
        | PatientEffect.TellError errs ->
            Logging.warning "patient change error" errs

            state
            |> alerted (
                Alert.Alert.PatientChangeFailed(errs |> Array.tryHead |> Option.defaultValue "Er ging iets mis")
            )


    /// What one effect of the lanes leaves for the client, carried out by its machine's apply.
    let applyLanesEffect effect state =
        match effect with
        | LanesEffect.Signing e -> state |> applySigningEffect e
        | LanesEffect.Patient e -> state |> applyPatientEffect e
        | LanesEffect.Workbench e -> state |> applyOrderContextEffect e
        | LanesEffect.Plan e -> state |> applyOrderPlanEffect e
        | LanesEffect.Session e -> state |> applySessionEffect e
        | LanesEffect.GoToPlanPage -> state |> runShell (ShellMachine.ShellMsg.MovedToPage Global.Pages.OrderPlan)


    /// A message through the lanes: the machines' steps recorded in the trail, and what the
    /// effects leave for the client carried out, all in this update.
    let runLanes msg (state: State) =
        let lanes, effects, steps = Lanes.transition newRequest msg state.Lanes
        steps
        |> List.iter (fun step -> StepTrail.record (fun no at -> Trail.lanes no at step))

        { state with Lanes = lanes } |> runEffects applyLanesEffect effects


    /// Whether the patient context is held; the panel cannot change the patient then.
    let patientHeld (state: State) =
        HeldPanelPolicy.held (SessionState.view state.Lanes.Session) (OrderPlanState.changed state.Lanes.OrderPlan)


    /// A medication chosen without a patient, from the url or a list, is dropped and said: the
    /// patient is part of the filter, so nothing waits for one.
    /// Every data load with its reading, the server check excepted.
    let loads (state: State) =
        let reading deferred = deferred |> Deferred.map ignore

        LoaderMachine.LoaderState.readings state.Loader
        @ AdminMachine.AdminState.readings state.Admin


    /// The data loads out.
    let loadsOut (state: State) = state |> loads |> LoaderMachine.outOf


    /// The data loads that have loaded.
    let loaded (state: State) = state |> loads |> LoaderMachine.loadedOf


    /// The requests out in the lanes and the loads.
    let busyOut (state: State) =
        Busy.out
            state.Shell.Counting
            state.Lanes.Patient
            state.Lanes.OrderContext
            state.Lanes.OrderPlan
            state.Lanes.Session
            state.Lanes.Signing
            (loadsOut state)


    /// Where the start-up is; started once, it stays so.
    let startup (state: State) =
        if state.Shell.Started then
            StartupPolicy.Startup.Started
        else
            StartupPolicy.status (busyOut state) (loaded state) state.Loader.Failed


    /// The start-up marked as ended at the first update in which it is.
    let markStarted (state: State, cmd) =
        match startup state with
        | StartupPolicy.Startup.Started when not state.Shell.Started ->
            let state, ended = state |> runShell ShellMachine.ShellMsg.StartupEnded
            state, Cmd.batch [ cmd; ended ]
        | _ -> state, cmd


    /// The page, the language and the disclaimer of a url applied, and the url kept as the one
    /// the app shows; no lane changes.
    let applyPage sl (url: Url.UrlParts) (state: State) =
        { state with Url = UrlPolicy.UrlState.Shown sl }
        |> runShell (ShellMachine.ShellMsg.UrlApplied url)


    /// A url applied in full: its patient, its medication, its page and its launch.
    let applyUrl sl (url: Url.UrlParts) (state: State) =
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
                state |> alerted Alert.Alert.NoPatientForMedication

        // the address bar shows "#/session" once a launch url is erased
        let state, page = state |> applyPage (if url.Launch.IsSome then [ "session" ] else sl) url

        state,
        Cmd.batch
            [
                // a seed that comes before the patient waits for it in the workbench
                patientCmd
                seed
                launchCmd url.Launch
                page
            ]


    /// The url the app shows put back in the address bar. The url change that fires then is the
    /// url the app shows, so it changes nothing.
    let putBack (state: State) =
        state, Cmd.ofEffect (fun _ -> Router.navigate (state.Url |> UrlPolicy.UrlState.shown |> Array.ofList))


    /// The patient, the workbench, the plan, its interactions and the signing started over on a
    /// url with a patient, a medication or a launch, the Session left, and the url applied as at
    /// a page load. A launch is presented once the Session lane has nothing out.
    let startOver sl (url: Url.UrlParts) (state: State) =
        // a check still out is for the old plan, and is dropped
        let state, _ = state |> runLoader (LoaderMachine.LoaderMsg.CheckInteractions [])
        let state, leave = state |> runLanes (LanesMsg.StartOver url.Patient)
        let state, applied = state |> applyUrl sl url
        state, Cmd.batch [ leave; applied ]


    /// The page load: the url applied at once, so the router's first report, of the same url,
    /// changes nothing; then the Session resumed, or left for a url with a patient or a
    /// medication, and the loads started.
    let init () : State * Cmd<Msg> =
        let sl = Router.currentUrl ()
        let url = sl |> parseUrl

        if url.Launch.IsSome then
            eraseLaunch ()

        let state, applied = initialState sl url |> applyUrl sl url

        let cmds =
            Cmd.batch
                [
                    // a page load without a Launch resumes on the cookie (reload, IdP return); one
                    // with a patient or a medication leaves the Session the cookie may hold. A
                    // launch is presented by the url applied
                    match url.Launch with
                    | None when seeds url -> Cmd.ofMsg (SessionMsg SessionMsg.UrlMovedOn)
                    | None -> Cmd.ofMsg (SessionMsg SessionMsg.Resume)
                    | Some _ -> Cmd.none
                    Cmd.ofMsg (LoaderMsg LoaderMachine.LoaderMsg.CheckServer)
                    for load in
                        [
                            Busy.Load.Settings
                            Busy.Load.NormalValues
                            Busy.Load.BolusMedication
                            Busy.Load.ContinuousMedication
                            Busy.Load.Products
                            Busy.Load.Localization
                            Busy.Load.Formulary
                            Busy.Load.Parenteralia
                            Busy.Load.DrugNames
                        ] do
                        Cmd.ofMsg (LoaderMsg(LoaderMachine.LoaderMsg.Start load))
                    applied
                ]

        state, cmds


#if DEBUG
    /// A state may be traced only once the server has said it serves the demo data: never before the
    /// settings arrive, and never against production. The trail keeps the same gate through
    /// StepTrail.confirmDemo.
    let isTraceable (state: State) =
        match state.Loader.Settings with
        | Resolved settings
        | Refreshing settings -> settings.IsDemo
        | HasNotStartedYet
        | InProgress -> false
#endif


    let update (msg: Msg) (state: State) =
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
                let state, page = state |> runShell (ShellMachine.ShellMsg.MovedToPage Global.Pages.Prescribe)
                state,
                Cmd.batch
                    [
                        page
                        Cmd.ofMsg (OrderContextMsg(OrderContextMsg.SeedFilter(seed, newRequest ())))
                    ]
            | None -> state |> alerted Alert.Alert.NoPatientForMedication

        match msg with
        | ShellMsg msg -> state |> runShell msg

        | LoaderMsg msg -> state |> runLoader msg

        | AdminMsg msg -> state |> runAdmin msg

        // the settings page is behind the admin login
        | UpdatePage page ->
            state
            |> runShell (ShellMachine.ShellMsg.PageChosen(page, state.Admin.IsAuthenticated))

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
            let state = { state with Url = state.Url |> UrlPolicy.UrlState.close }

            let url = sl |> parseUrl

            // a launch url is erased at once, so its token survives neither the history nor a
            // copied url
            if url.Launch.IsSome then
                eraseLaunch ()

            match url.Launch with
            // the return from the identity provider with a refusal: the gate says why
            | Some(Url.LaunchUrl.Refused _) -> state |> applyUrl sl url
            // a launch is decided as a url with a patient: it ends what is there
            | launch ->
                let change = UrlPolicy.change state.Url sl (seeds url)

                let underWay = state.Lanes.Signing |> SigningState.view |> SigningPolicy.underWay

                let action = UrlPolicy.action change underWay (busyOut state) (hasUnsignedWork state) (launched state)

                // what the url change is and does, never the url: it holds patient data
                Logging.log "url change" $"%A{change} -> %A{action}"

                match action with
                | UrlPolicy.UrlAction.ApplyPage -> state |> applyPage sl url
                | UrlPolicy.UrlAction.Ignore -> state, Cmd.none
                // a launch put back is gone, since Back does not bring it again as it does a
                // patient url: the user is told to open the patient again from the EHR
                | UrlPolicy.UrlAction.PutBack when launch.IsSome ->
                    let state, told = state |> alerted Alert.Alert.LaunchNotOpened
                    let state, back = state |> putBack
                    state, Cmd.batch [ told; back ]
                | UrlPolicy.UrlAction.PutBack -> state |> putBack
                | UrlPolicy.UrlAction.Ask -> { state with Url = state.Url |> UrlPolicy.UrlState.ask sl }, Cmd.none
                | UrlPolicy.UrlAction.StartOver -> state |> startOver sl url

        | LeaveForUrl ->
            match state.Url |> UrlPolicy.UrlState.asked with
            | Some sl -> state |> startOver sl (sl |> parseUrl)
            | None -> state, Cmd.none

        | StayOnUrl -> { state with Url = state.Url |> UrlPolicy.UrlState.close } |> putBack

        | SessionMsg msg ->
            // a failed close is reported only when it was this session's close: a CloseFailed
            // that arrives after a newer launch superseded the Closing session is dropped by
            // the machine and must not put an error over the newer session
            state |> runLanes (LanesMsg.Session msg)

        | SigningMsg msg -> state |> runLanes (LanesMsg.Signing msg)

        | OnSelectContinuousMedicationItem item ->
            match state.Loader.ContinuousMedication with
            | Resolved meds ->
                meds
                |> List.tryFind (fun m -> item.EndsWith($".{m.Medication}"))
                |> Option.map (fun m -> selectMedicationItem m.Generic m.Indication "INTRAVENEUS" m.DoseType state)
                |> Option.defaultWith (fun () ->
                    Logging.warning $"could not find continuous medication with item: {item}" item
                    state, Cmd.none
                )
            | _ -> state, Cmd.none

        | OnSelectEmergencyListItem item ->
            match state.Loader.BolusMedication with
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
            state
            |> runLanes (
                LanesMsg.Answer(
                    LanesMsg.Plan(OrderPlanMsg.Answered(request, Ok answer.Reply.Response)),
                    answer.From,
                    answer.Reply.Notice
                )
            )


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
    | AdminMsg(AdminMachine.AdminMsg.Login _) -> AdminMsg(AdminMachine.AdminMsg.Login redacted)
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
        member _.LocalizationTerms = state.Loader.Localization

    interface AppEnv.ISettings with
        member _.Settings = state.Loader.Settings

    interface AppEnv.IStartup with
        member _.Startup = startup state

    interface AppEnv.IBusy with
        member _.Any = state |> busyOut |> Busy.any
        member _.Page page = state |> busyOut |> Busy.page page

    interface AppEnv.IOrderContext with
        member _.OrderContext = state.Lanes.OrderContext |> OrderContextState.view

        member _.OrderContextMsg cmd =
            OrderContextMsg(OrderContextMsg.Command(cmd, newRequest ())) |> dispatch

        member _.ReopenField cmd =
            OrderContextMsg(OrderContextMsg.ReopenField(cmd, newRequest ())) |> dispatch

        member _.RestoreField() = OrderContextMsg OrderContextMsg.RestoreField |> dispatch

        member _.Dialog = state.Lanes.OrderContext |> OrderContextState.dialog

        member _.SelectScenario id = OrderContextMsg(OrderContextMsg.SelectScenario id) |> dispatch

    interface AppEnv.IOrderPlan with
        member _.OrderPlan = state.Lanes.OrderPlan |> OrderPlanState.view

        member _.Add orderId = Prescribe orderId |> dispatch

        member _.NewNutrition category =
            OrderPlanMsg(OrderPlanMsg.Change(OrderPlanChange.NewNutrition category, newRequest ()))
            |> dispatch

        member _.Remove ids =
            OrderPlanMsg(OrderPlanMsg.Change(OrderPlanChange.Remove ids, newRequest ()))
            |> dispatch

        member _.OrderDialogCommand(id, cmd) =
            OrderPlanMsg(OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand(id, cmd), newRequest ()))
            |> dispatch

        member _.ReopenField(id, cmd) =
            OrderPlanMsg(OrderPlanMsg.ReopenField(id, cmd, newRequest ())) |> dispatch

        member _.RestoreField() = OrderPlanMsg OrderPlanMsg.RestoreField |> dispatch

        member _.SelectContext id = OrderPlanMsg(OrderPlanMsg.SelectContext id) |> dispatch

        member _.FilterRows ids =
            OrderPlanMsg(OrderPlanMsg.Change(OrderPlanChange.FilterRows ids, newRequest ()))
            |> dispatch

        member _.Changed = state.Lanes.OrderPlan |> OrderPlanState.changed

    interface AppEnv.IPatient with
        member _.Draft = state.Lanes.Patient |> PatientState.draft

        member _.Changing = state.Lanes.Patient |> PatientState.changing

        member _.Estimated =
            state.Lanes.Patient
            |> PatientState.draft
            |> applyNormalValues state.Loader.NormalValues
        // the panel's edit is ignored while the patient context is held; the Session's patient
        // and a data notice accepted do not come this way
        member _.UpdatePatient p =
            if not (patientHeld state) then
                UpdatePatient p |> dispatch

        member _.EditPatient p =
            if not (patientHeld state) then
                EditPatient p |> dispatch

    interface AppEnv.IFormulary with
        member _.Formulary = state.Loader.Formulary
        member _.UpdateFormulary f =
            LoaderMsg(LoaderMachine.LoaderMsg.FormularyChanged f) |> dispatch

    interface AppEnv.IParenteralia with
        member _.Parenteralia = state.Loader.Parenteralia
        member _.UpdateParenteralia p =
            LoaderMsg(LoaderMachine.LoaderMsg.ParenteraliaChanged p) |> dispatch

    interface AppEnv.IInteractions with
        member _.Interactions = state.Loader.Interactions
        member _.InteractionDrugNames = state.Loader.DrugNames
        member _.CheckInteractions drugs =
            LoaderMsg(LoaderMachine.LoaderMsg.CheckInteractions drugs) |> dispatch

    interface AppEnv.IResources with
        member _.Reload = state.Admin.Reloading
        member _.ReloadResources() = AdminMsg AdminMachine.AdminMsg.ReloadResources |> dispatch

    interface AppEnv.ISession with
        member _.Session = state.Lanes.Session |> SessionState.view
        member _.Close() = SessionMsg SessionMsg.CloseSession |> dispatch
        member _.RetryLaunch() = SessionMsg SessionMsg.RetryLaunch |> dispatch

        member _.OpenAnonymously() = SessionMsg SessionMsg.ContinueAnonymous |> dispatch

        member _.SupplyPin code pin = SessionMsg(SessionMsg.SupplyPin(code, pin)) |> dispatch

        member _.NewerPlan = state.Lanes.Session |> SessionState.newerPlan

        member _.OpenSignedPlan id = SessionMsg(SessionMsg.OpenSignedPlan id) |> dispatch

        member _.Refresh() = SessionMsg SessionMsg.RefreshPatient |> dispatch

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
        member _.Accept() =
            SigningMsg(SigningMsg.AcceptDataChange(patientHeld state)) |> dispatch

        // one key per confirmation, so the commit takes effect once; the machine keeps it for a retry
        member _.Confirm pin =
            SigningMsg(SigningMsg.ConfirmPin(pin, Guid.NewGuid().ToString())) |> dispatch

        member _.Cancel() = SigningMsg SigningMsg.Cancel |> dispatch

    interface AppEnv.IAuthentication with
        member _.IsAuthenticated = state.Admin.IsAuthenticated
        member _.Login password = AdminMsg(AdminMachine.AdminMsg.Login password) |> dispatch

        member _.Logout() = AdminMsg AdminMachine.AdminMsg.Logout |> dispatch

    interface AppEnv.ILogAnalyzer with
        member _.LogFiles = state.Admin.LogFiles
        member _.LogAnalysisReport = state.Admin.LogAnalysisReport
        member _.ListLogFiles() = AdminMsg AdminMachine.AdminMsg.ListLogFiles |> dispatch

        member _.AnalyzeLogFile fileName =
            AdminMsg(AdminMachine.AdminMsg.AnalyzeLogFile fileName) |> dispatch

    interface AppEnv.IBolusMedication with
        member _.BolusMedication = bm
        member _.OnSelectBolusMedicationItem s = OnSelectEmergencyListItem s |> dispatch
        member _.BolusMedicationFilter = state.Shell.EmergencyListFilter
        member _.OnBolusMedicationFilterChange f =
            ShellMsg(ShellMachine.ShellMsg.EmergencyListFiltered f) |> dispatch

    interface AppEnv.IContinuousMedication with
        member _.ContinuousMedication = cm

        member _.OnSelectContinuousMedicationItem s = OnSelectContinuousMedicationItem s |> dispatch

        member _.ContinuousMedicationFilter = state.Shell.ContinuousMedsFilter

        member _.OnContinuousMedicationFilterChange f =
            ShellMsg(ShellMachine.ShellMsg.ContinuousMedsFiltered f) |> dispatch


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
                ShellMsg ShellMachine.ShellMsg.SnackbarClosed |> dispatch

    let closeSnackbar _ = ShellMsg ShellMachine.ShellMsg.SnackbarClosed |> dispatch

    let getTerm = Global.getLocalizedTerm state.Loader.Localization state.Shell.Language.Current

    // the alert on the snackbar in words; closed, it shows nothing
    let snackbarOpen = state.Shell.Snackbar.IsSome

    let severity =
        state.Shell.Snackbar
        |> Option.map Views.AlertText.severity
        |> Option.defaultValue "error"

    let message =
        state.Shell.Snackbar
        |> Option.map (Views.AlertText.text getTerm)
        |> Option.defaultValue ""

    let context: Global.Context =
        {
            Localization = state.Shell.Language.Current
            Hospital = state.Shell.Hospital
        }

    let autoHide =
        match severity with
        | "success"
        | "info" -> 3000 |> box
        | _ -> null

    let bm =
        calculateInterventions
            EmergencyTreatment.calculate
            state.Loader.BolusMedication
            (state.Lanes.Patient |> PatientState.draft)

    let cm =
        let calc =
            fun _ w meds ->
                match w with
                | Some w' -> ContinuousMedication.calculate w' meds
                | None -> []

        calculateInterventions calc state.Loader.ContinuousMedication (state.Lanes.Patient |> PatientState.draft)

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
        match state.Shell.ServerError with
        | Some error ->
            Components.Notice.View
                {|
                    kind = Components.Notice.Kind.Error
                    title = Some "Server probleem"
                    message = error.Message
                    action = None
                    onClose = Some(fun () -> dispatch (ShellMsg ShellMachine.ShellMsg.ServerErrorDismissed))
                |}
        | None -> null

    // the question before a url with a patient, a medication or a launch: it leaves the
    // launched Session, or drops the work not signed. The browser asks the second itself for a
    // reload or a closed tab, but not for a change of the url
    let asksLaunch =
        state.Url
        |> UrlPolicy.UrlState.asked
        |> Option.bind Url.parseLaunch
        |> Option.isSome

    let title =
        if launched state then
            Terms.``Url Leave Session Title`` |> getTerm "Sessie verlaten?"
        else
            Terms.``Url Leave Title`` |> getTerm "Orderplan verlaten?"

    let text =
        match launched state, asksLaunch with
        | true, true ->
            Terms.``Url Leave Launch Text``
            |> getTerm
                "De sessie wordt gesloten en de nieuwe sessie uit het EPD wordt geopend. Nieuwe en gewijzigde orders en de medicatie die wordt voorgeschreven gaan verloren. Wilt u doorgaan?"
        | true, false ->
            Terms.``Url Leave Session Text``
            |> getTerm
                "De sessie met de patiënt uit het EPD wordt gesloten en de patiënt uit de url wordt gebruikt, zonder sessie. Nieuwe en gewijzigde orders en de medicatie die wordt voorgeschreven gaan verloren. Wilt u doorgaan?"
        | false, _ ->
            Terms.``Url Leave Text``
            |> getTerm
                "De nieuwe en gewijzigde orders en de medicatie die wordt voorgeschreven gaan verloren. Wilt u doorgaan?"

    let leaveDialog =
        Components.ConfirmDialog.View
            {|
                isOpen = (state.Url |> UrlPolicy.UrlState.asked).IsSome
                title = title
                text = text
                confirmLabel = Terms.``Url Leave`` |> getTerm "Verlaten"
                cancelLabel = Terms.Cancel |> getTerm "Annuleren"
                onConfirm = fun () -> LeaveForUrl |> dispatch
                onCancel = fun () -> StayOnUrl |> dispatch
            |}

    // the quantity fields read whether one of them counts, and report their own count
    let counting: Global.Counting =
        {
            Counting = state.Shell.Counting
            Report = ShellMachine.ShellMsg.CountingChanged >> ShellMsg >> dispatch
        }

    let genPresProps =
        {|
            appEnv = appEnv
            // the disclaimer is for anonymous use only: a launched, resuming or
            // refused session never sees it; an anonymous open after a refusal does
            showDisclaimer =
                state.Shell.ShowDisclaimer
                && (
                    match SessionState.view state.Lanes.Session with
                    | SessionView.Anonymous -> true
                    | _ -> false
                )
            isDemo = state.Shell.IsDemo
            acceptDisclaimer = fun _ -> ShellMsg ShellMachine.ShellMsg.DisclaimerAccepted |> dispatch
            updatePage = UpdatePage >> dispatch
            page = state.Shell.Page
            languages = Localization.languages
            hospitals = state.Loader.Hospitals
            switchLang = ShellMachine.ShellMsg.LanguageChosen >> ShellMsg >> dispatch
            switchHosp = ShellMachine.ShellMsg.HospitalChosen >> ShellMsg >> dispatch
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
    import MuiAlert from '@mui/material/Alert';

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box sx={sx}>
                <CssBaseline />
                {serverErrorBanner}
                {Components.Router.View {| onUrlChanged = UrlChanged >> dispatch |}}
                {leaveDialog}
                {Pages.GenPres.View genPresProps
                 |> toReact
                 |> Components.Context.Counting counting
                 |> Components.Context.Context context}
            </Box>
            <div>
                <Snackbar
                    open={snackbarOpen}
                    autoHideDuration={autoHide}
                    onClose={handleClose}
                >
                    <MuiAlert severity={severity} onClose={closeSnackbar} sx={ {| width = "100%" |} }>
                        {message}
                    </MuiAlert>
                </Snackbar>
            </div>
        </ThemeProvider>
    </React.StrictMode>
    """


let root = ReactDomClient.createRoot (document.getElementById "genpres-app")
root.render (View() |> toReact)
