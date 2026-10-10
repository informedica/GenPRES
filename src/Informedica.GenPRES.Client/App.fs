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


type private State = Client.ClientState


type private Msg = Client.ClientMsg


module private Messages =


    /// A message to one of the lanes' machines.
    let session m = Msg.Lanes(LanesMsg.Session m)
    let signing m = Msg.Lanes(LanesMsg.Signing m)
    let patient m = Msg.Lanes(LanesMsg.Patient m)
    let workbench m = Msg.Lanes(LanesMsg.Workbench m)
    let plan m = Msg.Lanes(LanesMsg.Plan m)


module private Effects =


    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder Api.routerPaths
        |> Remoting.buildProxy<Api.IServerApi>


    /// Turns a server reply into the message for the lane that sent the request. The response goes
    /// to that lane, the notice goes to the Session, and the session token sent with the request
    /// comes along.
    let answered lane opened (reply: Api.Reply<_>) =
        Msg.Lanes(LanesMsg.Answer(lane (Ok reply.Response), opened, reply.Notice))


    /// Sends a command to the server with the Session's token and turns the outcome into the
    /// lane's message: a reply goes through answered, a refusal or a failed request comes back to
    /// the lane as an Error.
    let underSession call command lane opened =
        async {
            try
                match!
                    call
                        {
                            Api.Request.Opened = opened
                            Api.Request.Command = command
                        }
                with
                | Ok reply -> return reply |> answered lane opened
                | Error errs -> return Msg.Lanes(lane (Error errs))
            with ex ->
                return Msg.Lanes(lane (Error [| ex.Message |]))
        }
        |> Cmd.fromAsync


    /// Erase the Launch: replace the launch url with "#/session" in the
    /// address bar and the history entry, so the token survives neither a
    /// reload, the back button nor a copied url. Goes through the History API
    /// directly: Router.navigate would dispatch the navigation event and
    /// re-enter UrlChanged.
    let eraseLaunch () = Browser.Dom.history.replaceState (null, "", "#/session")


    /// Make the key pair, then present the Launch with its public key.
    /// A browser that cannot make a key cannot launch; that is reported as a missing browser
    /// identity, the refusal whose text asks for a retry and then a relaunch.
    let presentLaunch (launch: Launch) : Cmd<Msg> =
        async {
            match! Keys.generate () |> Async.Catch with
            | Choice1Of2 key -> return Messages.session (SessionMsg.PresentLaunch(launch, key))
            | Choice2Of2 ex ->
                Logging.error "could not make the browser key pair" ex.Message
                return Messages.session (SessionMsg.LaunchRefused LaunchRefusal.NoBrowserIdentity)
        }
        |> Cmd.fromAsync


    /// What a shell effect sends out of the client: the url put back or erased, a launch presented.
    let shell effect =
        match effect with
        | ShellMachine.ShellEffect.PutBackUrl sl -> Cmd.ofEffect (fun _ -> Router.navigate (Array.ofList sl))
        // erased at once, not as a command: the router's first report then reads the erased url,
        // the one the app shows, and changes nothing
        | ShellMachine.ShellEffect.EraseLaunch ->
            eraseLaunch ()
            Cmd.none
        | ShellMachine.ShellEffect.PresentLaunch launch -> presentLaunch launch
        | _ -> Cmd.none


    /// What a loader effect sends out of the client: a call, whose answer comes back as the loader
    /// machine's message, or a wait; a failure is logged.
    let loader effect opened =
        let landed landing result = Msg.Loader(LoaderMachine.LoaderMsg.Landed(landing result))

        let later seconds msg =
            async {
                do! Async.Sleep(seconds * 1000)
                return Msg.Loader msg
            }
            |> Cmd.fromAsync

        match effect with
        | LoaderMachine.LoaderEffect.FetchSettings ->
            Cmd.OfAsync.either
                serverApi.getSettings
                ()
                (Ok >> landed LoaderMachine.Landing.Settings)
                (fun ex -> Error ex.Message |> landed LoaderMachine.Landing.Settings)
        | LoaderMachine.LoaderEffect.FetchLocalization ->
            Cmd.OfAsync.perform GoogleDocs.loadLocalization () (landed LoaderMachine.Landing.Localization)
        | LoaderMachine.LoaderEffect.FetchNormalValues ->
            Cmd.OfAsync.perform GoogleDocs.loadNormalValues () (landed LoaderMachine.Landing.NormalValues)
        | LoaderMachine.LoaderEffect.FetchBolusMedication ->
            Cmd.OfAsync.perform GoogleDocs.loadBolusMedication () (landed LoaderMachine.Landing.BolusMedication)
        | LoaderMachine.LoaderEffect.FetchContinuousMedication ->
            Cmd.OfAsync.perform
                GoogleDocs.loadContinuousMedication
                ()
                (landed LoaderMachine.Landing.ContinuousMedication)
        | LoaderMachine.LoaderEffect.FetchProducts ->
            Cmd.OfAsync.perform GoogleDocs.loadProducts () (landed LoaderMachine.Landing.Products)
        | LoaderMachine.LoaderEffect.FetchFormulary form ->
            Cmd.OfAsync.perform
                serverApi.processFormulary
                {
                    Opened = opened
                    Command = form
                }
                (landed (fun result -> LoaderMachine.Landing.Formulary(opened, result)))
        | LoaderMachine.LoaderEffect.FetchParenteralia par ->
            Cmd.OfAsync.perform
                serverApi.processParenteralia
                {
                    Opened = opened
                    Command = par
                }
                (landed (fun result -> LoaderMachine.Landing.Parenteralia(opened, result)))
        | LoaderMachine.LoaderEffect.FetchInteractions(check, drugs) ->
            Cmd.OfAsync.perform
                serverApi.processInteraction
                {
                    Opened = opened
                    Command = Api.InteractionCommand.CheckInteractions drugs
                }
                (landed (fun result -> LoaderMachine.Landing.Interactions(check, opened, result)))
        | LoaderMachine.LoaderEffect.FetchDrugNames ->
            Cmd.OfAsync.perform
                serverApi.processInteraction
                {
                    Opened = opened
                    Command = Api.InteractionCommand.GetDrugNames
                }
                (landed (fun result -> LoaderMachine.Landing.DrugNames(opened, result)))
        | LoaderMachine.LoaderEffect.CheckServer ->
            Cmd.OfAsync.either
                serverApi.testApi
                ()
                (fun _ -> Msg.Loader(LoaderMachine.LoaderMsg.ServerChecked(Ok())))
                (fun ex -> Msg.Loader(LoaderMachine.LoaderMsg.ServerChecked(Error ex.Message)))
        | LoaderMachine.LoaderEffect.CheckServerLater seconds -> LoaderMachine.LoaderMsg.CheckServer |> later seconds
        | LoaderMachine.LoaderEffect.AskAgainLater(load, seconds) -> LoaderMachine.LoaderMsg.Start load |> later seconds
        | LoaderMachine.LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.Server, errs) ->
            Logging.error "server niet bereikbaar" errs
            Cmd.none
        | LoaderMachine.LoaderEffect.RequestFailed(_, errs) ->
            Logging.error "error" errs
            Cmd.none
        | _ -> Cmd.none


    /// What an admin effect sends out of the client: a call under the token, whose answer comes back
    /// as the admin machine's message; a failure is logged.
    let admin effect =
        let call command landing =
            Cmd.OfAsync.perform serverApi.processAdmin command (landing >> AdminMachine.AdminMsg.Landed >> Msg.Admin)

        match effect with
        | AdminMachine.AdminEffect.ValidatePassword(attempt, password) ->
            call
                (Api.AdminCommand.ValidatePassword password)
                (fun result -> AdminMachine.Landing.Login(attempt, result))
        | AdminMachine.AdminEffect.FetchLogFiles token ->
            call (Api.AdminCommand.ListLogFiles token) AdminMachine.Landing.LogFiles
        | AdminMachine.AdminEffect.FetchLogAnalysis(token, fileName) ->
            call (Api.AdminCommand.AnalyzeLogFile(token, fileName)) AdminMachine.Landing.LogAnalysis
        | AdminMachine.AdminEffect.Reload token ->
            call (Api.AdminCommand.ReloadResources token) AdminMachine.Landing.Reload
        | AdminMachine.AdminEffect.RequestFailed(_, errs) ->
            Logging.error "error" errs
            Cmd.none
        | _ -> Cmd.none


    /// What a session effect sends out of the client. A transport failure
    /// is a message, never an exception: an Error outcome for a presentation, CloseFailed for
    /// a close that did not reach the server.
    let session (effect: SessionEffect) : Cmd<Msg> =
        match effect with
        | SessionEffect.CallPresentLaunch(launch, key) ->
            async {
                try
                    let! outcome = serverApi.processLaunch (Api.LaunchCommand.PresentLaunch(launch, key))
                    return Messages.session (SessionMsg.LaunchOutcome(launch, key, Ok outcome))
                with ex ->
                    return Messages.session (SessionMsg.LaunchOutcome(launch, key, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallResume ->
            async {
                try
                    match! serverApi.processSession Api.SessionCommand.GetSession with
                    | Api.SessionResponse.SessionResp(Some session) ->
                        return Messages.session (SessionMsg.Resumed(Ok(ResumeResult.Found session)))
                    | Api.SessionResponse.SessionResp None
                    | Api.SessionResponse.SessionClosed ->
                        return Messages.session (SessionMsg.Resumed(Ok ResumeResult.NotFound))
                    | Api.SessionResponse.SessionEnded ending ->
                        return Messages.session (SessionMsg.Resumed(Ok(ResumeResult.Ended ending)))
                    | Api.SessionResponse.EnrolmentPending pending ->
                        return Messages.session (SessionMsg.Resumed(Ok(ResumeResult.Enrolling pending)))
                    // never an answer to GetSession
                    | Api.SessionResponse.PinRefused _ ->
                        return Messages.session (SessionMsg.Resumed(Ok ResumeResult.NotFound))
                with ex ->
                    return Messages.session (SessionMsg.Resumed(Error ex.Message))
            }
            |> Cmd.fromAsync
        // the version is open, or the record moved on: each said once, the machine decides
        | SessionEffect.CallOpenSignedPlan(id, from) ->
            async {
                try
                    match! serverApi.processSession (Api.SessionCommand.OpenVersion id) with
                    | Api.SessionResponse.SessionResp session ->
                        return Messages.session (SessionMsg.SignedPlanOpened(from, Ok session))
                    // never an answer to OpenVersion
                    | _ -> return Messages.session (SessionMsg.SignedPlanOpened(from, Ok None))
                with ex ->
                    return Messages.session (SessionMsg.SignedPlanOpened(from, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallRefreshPatient from ->
            async {
                try
                    match! serverApi.processSession Api.SessionCommand.Refresh with
                    | Api.SessionResponse.SessionResp session ->
                        return Messages.session (SessionMsg.PatientRefreshed(from, Ok session))
                    // never an answer to Refresh
                    | _ -> return Messages.session (SessionMsg.PatientRefreshed(from, Ok None))
                with ex ->
                    return Messages.session (SessionMsg.PatientRefreshed(from, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallCloseSession ->
            async {
                // the server deletes the cookie whatever its close returns (finally), so an
                // answer of any kind means SessionClosed; only a request that never got there fails
                match! serverApi.processSession Api.SessionCommand.CloseSession |> Async.Catch with
                | Choice1Of2 _ -> return Messages.session SessionMsg.SessionClosed
                | Choice2Of2 ex ->
                    Logging.error "could not close the session on the server" ex.Message
                    return Messages.session (SessionMsg.CloseFailed ex.Message)
            }
            |> Cmd.fromAsync
        | SessionEffect.CallSupplyPin(code, pin) ->
            async {
                try
                    match! serverApi.processSession (Api.SessionCommand.SupplyPin(code, pin)) with
                    | Api.SessionResponse.SessionResp(Some session) ->
                        return Messages.session (SessionMsg.PinAnswered(Ok(PinOutcome.Opened session)))
                    | Api.SessionResponse.PinRefused refusal ->
                        return Messages.session (SessionMsg.PinAnswered(Ok(PinOutcome.Refused refusal)))
                    // never an answer to SupplyPin: the attempt is gone, whatever happened
                    | Api.SessionResponse.SessionResp None
                    | Api.SessionResponse.SessionClosed
                    | Api.SessionResponse.SessionEnded _
                    | Api.SessionResponse.EnrolmentPending _ ->
                        return
                            Messages.session (SessionMsg.PinAnswered(Ok(PinOutcome.Refused PinRefusal.AttemptExpired)))
                with ex ->
                    Logging.error "could not send the PIN to the server" ex.Message
                    return Messages.session (SessionMsg.PinAnswered(Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.GoToIdentityProvider url -> Cmd.ofEffect (fun _ -> Browser.Dom.window.location.assign url)
        | SessionEffect.KeepBrowserKey thumbprint ->
            Cmd.ofEffect (fun _ ->
                async {
                    match! Keys.keep thumbprint |> Async.Catch with
                    | Choice1Of2() -> ()
                    | Choice2Of2 ex -> Logging.error "could not prune the browser keys" ex.Message
                }
                |> Async.StartImmediate
            )
        | _ -> Cmd.none


    /// What a signing effect sends out of the client. The machine names the plan,
    /// the challenge, the PIN, the request id and the key; the OpenedToken is the one the open
    /// Session holds, and every answer carries the request id or the key it answers, so the
    /// machine can drop one that belongs to an earlier Session. Without an open Session nothing
    /// is sent: the answer is a refusal.
    let signing (effect: SigningEffect) opened : Cmd<Msg> =
        match effect with
        | SigningEffect.CallChallenge(plan, notice, request) ->
            match opened with
            | None ->
                Cmd.ofMsg (
                    Messages.signing (
                        SigningMsg.ChallengeAnswered(request, Ok(SigningResponse.Refused SigningRefusal.NoSession))
                    )
                )
            | Some opened ->
                async {
                    try
                        let! answer =
                            serverApi.processSigning (Api.SigningCommand.RequestSignChallenge(plan, opened, notice))

                        return Messages.signing (SigningMsg.ChallengeAnswered(request, Ok answer))
                    with ex ->
                        return Messages.signing (SigningMsg.ChallengeAnswered(request, Error ex.Message))
                }
                |> Cmd.fromAsync
        | SigningEffect.CallSubmit(plan, challenge, pin, key) ->
            match opened with
            | None ->
                Cmd.ofMsg (
                    Messages.signing (
                        SigningMsg.SubmitAnswered(key, Ok(SigningResponse.Refused SigningRefusal.NoSession))
                    )
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

                        return Messages.signing (SigningMsg.SubmitAnswered(key, Ok answer))
                    with ex ->
                        return Messages.signing (SigningMsg.SubmitAnswered(key, Error ex.Message))
                }
                |> Cmd.fromAsync
        | SigningEffect.TellError reason ->
            Logging.error "could not send the signature to the server" reason
            Cmd.none
        | _ -> Cmd.none


    /// What an order-plan effect sends out of the client. A plan call answers under
    /// the request it was sent for, so the machine can drop an answer to an earlier request;
    /// the notice rides on the reply to the Session; a transport failure is an Error answer, never
    /// an exception.
    let plan (effect: OrderPlanEffect) opened : Cmd<Msg> =
        match effect with
        | OrderPlanEffect.CallPlan(command, request) ->
            opened
            |> underSession
                serverApi.processOrderPlan
                command
                (fun r -> LanesMsg.Plan(OrderPlanMsg.Answered(request, r)))
        | OrderPlanEffect.TellError errs ->
            Logging.error "error" errs
            Cmd.none
        | _ -> Cmd.none


    /// What an order-context effect sends out of the client. A workbench call
    /// answers under the request it was sent for; the notice rides on the reply to the Session; a
    /// transport failure is an Error answer.
    let workbench (effect: OrderContextEffect) opened : Cmd<Msg> =
        match effect with
        | OrderContextEffect.CallContext(command, request) ->
            opened
            |> underSession
                serverApi.processOrderContext
                command
                (fun r -> LanesMsg.Workbench(OrderContextMsg.Answered(request, r)))
        | OrderContextEffect.TellError errs ->
            Logging.warning "order context error" errs
            Cmd.none
        | _ -> Cmd.none


    /// What a patient effect sends out of the client. A patient change answers under
    /// the request it was sent for; the notice rides on the reply to the Session; a transport
    /// failure is an Error answer.
    let patient (effect: PatientEffect) opened : Cmd<Msg> =
        match effect with
        | PatientEffect.CallPatient(pat, request) ->
            opened
            |> underSession
                serverApi.processPatient
                (Api.PatientCommand.ChangePatient pat)
                (fun r -> LanesMsg.Patient(PatientMsg.Answered(request, r)))
        | PatientEffect.TellError errs ->
            Logging.warning "patient change error" errs
            Cmd.none
        | _ -> Cmd.none


    /// What an effect of the client sends out of it, each by its part's apply; the routed ones send
    /// nothing, since the client passed them on itself.
    let apply opened effect =
        match effect with
        | Client.ClientEffect.Lanes(LanesEffect.Signing e) -> opened |> signing e
        | Client.ClientEffect.Lanes(LanesEffect.Patient e) -> opened |> patient e
        | Client.ClientEffect.Lanes(LanesEffect.Workbench e) -> opened |> workbench e
        | Client.ClientEffect.Lanes(LanesEffect.Plan e) -> opened |> plan e
        | Client.ClientEffect.Lanes(LanesEffect.Session e) -> session e
        | Client.ClientEffect.Loader e -> opened |> loader e
        | Client.ClientEffect.Admin e -> admin e
        | Client.ClientEffect.Shell e -> shell e
        | _ -> Cmd.none


module private Elmish =


    /// A request id, minted at dispatch, so that an answer can name the request it answers.
    let newRequest () = Guid.NewGuid().ToString()


    /// What the url of these segments carries, at this moment; the parts that did not parse are
    /// logged by name, never by value, since the values are patient data.
    let parseUrl sl =
        let url = sl |> Url.parse DateTime.Now

        for part in url.NotParsed do
            match part with
            | Url.UrlPart.Patient parameters -> Logging.warning "could not parse url to patient" parameters
            | Url.UrlPart.Route segment -> Logging.warning "could not parse url" segment

        url


    /// A landing noted before the client takes it: the settings open the trail when the server serves
    /// the demo data, and a failed start-up load is logged, never in the trail. No settings: the client
    /// keeps its own defaults, and the trail records nothing.
    let noteLanding landing =
        match landing with
        | LoaderMachine.Landing.Settings(Ok settings) -> StepTrail.confirmDemo settings.IsDemo
        | LoaderMachine.Landing.Settings(Error err) ->
            StepTrail.confirmDemo false
            Logging.error "cannot load the server settings" err
        | LoaderMachine.Landing.Localization(Error err) -> Logging.error "cannot load localization" err
        | LoaderMachine.Landing.NormalValues(Error err) -> Logging.error "cannot load normal values" err
        | LoaderMachine.Landing.BolusMedication(Error err) -> Logging.error "cannot load emergency treatment" err
        | LoaderMachine.Landing.ContinuousMedication(Error err) -> Logging.error "cannot load continuous medication" err
        | LoaderMachine.Landing.Products(Error err) -> Logging.error "cannot load products" err
        | _ -> ()


    /// The client's steps recorded in the trail, each landing noted first, and its effects carried
    /// out in the order they came, each with the Session's token in the state the transition left.
    let carryOut (state: State, effects, steps) =
        for step in steps do
            match step with
            | Client.ClientStep.Loader(LoaderMachine.LoaderMsg.Landed landing, _, _) -> noteLanding landing
            | _ -> ()

            StepTrail.record (fun no at -> Trail.client no at step)

        let opened = Client.opened state

        state, effects |> List.map (Effects.apply opened) |> Cmd.batch


    /// The page load: the url applied at once, so the router's first report, of the same url,
    /// changes nothing; then the Session resumed, or left for a url with a patient or a
    /// medication, and the loads started.
    let init () : State * Cmd<Msg> =
        let sl = Router.currentUrl ()
        let url = sl |> parseUrl

        Client.initial url |> Client.pageLoad newRequest sl url |> carryOut


    /// A message through the client, and its effects carried out.
    let update (msg: Msg) (state: State) = state |> Client.transition newRequest msg |> carryOut


/// The app as an Elmish program. A debug build with GENPRES_LOG on and GENPRES_PROD not 1 traces every message
/// and the new state to the console, hands the history to the Redux DevTools browser extension, and keeps the
/// readable trail of the machine steps, which window.genpresTrail() returns as text; for development testing
/// only. It records nothing until the server settings confirm the demo data, so it never runs against
/// production; a release build records nothing at all.
let private program () =
    let program = Program.mkProgram Elmish.init Elmish.update (fun _ _ -> ())
#if DEBUG
    if StepTrail.isTraceOn () then
        window?genpresTrail <- StepTrail.text
        program |> Program.withTrace Tracing.consoleTrace |> Tracing.withGatedDebugger
    else
        program
#else
    program
#endif


/// The lists calculated for the draft; empty without one, since no patient is not a fetch
/// under way: the page says what is missing, and the spinner is for a fetch only.
let private calculateInterventions calc meds pat =
    meds
    |> Deferred.bind (fun xs ->
        match pat with
        | None -> Resolved []
        | Some p ->
            let a = p |> Patient.getAgeInYears
            let w = p |> Patient.getWeightInKg
            xs |> calc a w |> Resolved
    )


/// What the views read from the client state, and the messages they send, through the AppEnv
/// interfaces.
type private Projection(state: State, dispatch: Msg -> unit) =

    interface AppEnv.ILocalization with
        member _.LocalizationTerms = Client.localization state

    interface AppEnv.ISettings with
        member _.Settings = Client.settings state

    interface AppEnv.IStartup with
        member _.Startup = Client.startup state

    interface AppEnv.IBusy with
        member _.Any = Client.busyAny state
        member _.Page page = Client.busyPage page state

    interface AppEnv.IOrderContext with
        member _.OrderContext = Client.orderContext state

        member _.OrderContextMsg cmd =
            Messages.workbench (OrderContextMsg.Command(cmd, Elmish.newRequest ()))
            |> dispatch

        member _.ReopenField cmd =
            Messages.workbench (OrderContextMsg.ReopenField(cmd, Elmish.newRequest ()))
            |> dispatch

        member _.RestoreField() = Messages.workbench OrderContextMsg.RestoreField |> dispatch

        member _.Dialog = Client.orderContextDialog state

        member _.SelectScenario id =
            Messages.workbench (OrderContextMsg.SelectScenario id) |> dispatch

    interface AppEnv.IOrderPlan with
        member _.OrderPlan = Client.orderPlan state

        member _.Add orderId =
            Msg.Lanes(LanesMsg.Prescribe(orderId, Elmish.newRequest (), Elmish.newRequest ()))
            |> dispatch

        member _.NewNutrition category =
            Messages.plan (OrderPlanMsg.Change(OrderPlanChange.NewNutrition category, Elmish.newRequest ()))
            |> dispatch

        member _.Remove ids =
            Messages.plan (OrderPlanMsg.Change(OrderPlanChange.Remove ids, Elmish.newRequest ()))
            |> dispatch

        member _.OrderDialogCommand(id, cmd) =
            Messages.plan (OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand(id, cmd), Elmish.newRequest ()))
            |> dispatch

        member _.ReopenField(id, cmd) =
            Messages.plan (OrderPlanMsg.ReopenField(id, cmd, Elmish.newRequest ()))
            |> dispatch

        member _.RestoreField() = Messages.plan OrderPlanMsg.RestoreField |> dispatch

        member _.SelectContext id = Messages.plan (OrderPlanMsg.SelectContext id) |> dispatch

        member _.FilterRows ids =
            Messages.plan (OrderPlanMsg.Change(OrderPlanChange.FilterRows ids, Elmish.newRequest ()))
            |> dispatch

        member _.Changed = Client.changedOrders state

    interface AppEnv.IPatient with
        member _.Draft = Client.draft state

        member _.Changing = Client.changing state

        member _.Estimated = Client.estimated state

        member _.UpdatePatient p =
            Msg.PanelChanged(p, PatientDraftPolicy.Estimates.Renewed, Elmish.newRequest ())
            |> dispatch

        member _.EditPatient p =
            Msg.PanelChanged(p, PatientDraftPolicy.Estimates.Kept, Elmish.newRequest ())
            |> dispatch

    interface AppEnv.IFormulary with
        member _.Formulary = Client.formulary state
        member _.UpdateFormulary f =
            Msg.Loader(LoaderMachine.LoaderMsg.FormularyChanged f) |> dispatch

    interface AppEnv.IParenteralia with
        member _.Parenteralia = Client.parenteralia state
        member _.UpdateParenteralia p =
            Msg.Loader(LoaderMachine.LoaderMsg.ParenteraliaChanged p) |> dispatch

    interface AppEnv.IInteractions with
        member _.Interactions = Client.interactions state
        member _.InteractionDrugNames = Client.drugNames state
        member _.CheckInteractions drugs =
            Msg.Loader(LoaderMachine.LoaderMsg.CheckInteractions drugs) |> dispatch

    interface AppEnv.IResources with
        member _.Reload = Client.reloading state
        member _.ReloadResources() = Msg.Admin AdminMachine.AdminMsg.ReloadResources |> dispatch

    interface AppEnv.ISession with
        member _.Session = Client.session state
        member _.Close() = Messages.session SessionMsg.CloseSession |> dispatch
        member _.RetryLaunch() = Messages.session SessionMsg.RetryLaunch |> dispatch

        member _.OpenAnonymously() = Messages.session SessionMsg.ContinueAnonymous |> dispatch

        member _.SupplyPin code pin = Messages.session (SessionMsg.SupplyPin(code, pin)) |> dispatch

        member _.NewerPlan = Client.newerPlan state

        member _.OpenSignedPlan id = Messages.session (SessionMsg.OpenSignedPlan id) |> dispatch

        member _.Refresh() = Messages.session SessionMsg.RefreshPatient |> dispatch

    interface AppEnv.ISigning with
        member _.Signing = Client.signing state

        member _.Differences = Client.differences state

        // one request id per Sign, so the answer lands on this request and no other
        member _.Sign plan = Msg.Sign(plan, Elmish.newRequest ()) |> dispatch

        member _.Held = Client.held state

        member _.Accept() = Msg.AcceptDataChange |> dispatch

        // one key per confirmation, so the commit takes effect once; the machine keeps it for a retry
        member _.Confirm pin =
            Messages.signing (SigningMsg.ConfirmPin(pin, Elmish.newRequest ())) |> dispatch

        member _.Cancel() = Messages.signing SigningMsg.Cancel |> dispatch

    interface AppEnv.IAuthentication with
        member _.IsAuthenticated = Client.isAuthenticated state
        member _.Login password = Msg.Admin(AdminMachine.AdminMsg.Login password) |> dispatch

        member _.Logout() = Msg.Admin AdminMachine.AdminMsg.Logout |> dispatch

    interface AppEnv.ILogAnalyzer with
        member _.LogFiles = Client.logFiles state
        member _.LogAnalysisReport = Client.logAnalysisReport state
        member _.ListLogFiles() = Msg.Admin AdminMachine.AdminMsg.ListLogFiles |> dispatch

        member _.AnalyzeLogFile fileName =
            Msg.Admin(AdminMachine.AdminMsg.AnalyzeLogFile fileName) |> dispatch

    interface AppEnv.IBolusMedication with
        member _.BolusMedication =
            calculateInterventions EmergencyTreatment.calculate (Client.bolusMedication state) (Client.draft state)

        member _.OnSelectBolusMedicationItem s = Msg.EmergencyListItemChosen s |> dispatch
        member _.BolusMedicationFilter = Client.emergencyListFilter state
        member _.OnBolusMedicationFilterChange f =
            Msg.Shell(ShellMachine.ShellMsg.EmergencyListFiltered f) |> dispatch

    interface AppEnv.IContinuousMedication with
        member _.ContinuousMedication =
            let calc =
                fun _ w meds ->
                    match w with
                    | Some w' -> ContinuousMedication.calculate w' meds
                    | None -> []

            calculateInterventions calc (Client.continuousMedication state) (Client.draft state)

        member _.OnSelectContinuousMedicationItem s = Msg.ContinuousMedicationChosen s |> dispatch

        member _.ContinuousMedicationFilter = Client.continuousMedsFilter state

        member _.OnContinuousMedicationFilterChange f =
            Msg.Shell(ShellMachine.ShellMsg.ContinuousMedsFiltered f) |> dispatch


[<JSX.Component>]
let View () =
    let state, dispatch = React.useElmish (program, [||])
    let isMobile = Mui.Hooks.useMediaQuery "(max-width:1200px)"

    global.Hooks.LeaveGuard.useLeaveGuard (Client.unsignedWork state)

    let getTerm = Global.getLocalizedTerm (Client.localization state) (Client.language state)

    let context: Global.Context =
        {
            Localization = Client.language state
            Hospital = Client.hospital state
        }

    let appEnv = Projection(state, dispatch) :> obj

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

    let theme = if isMobile then Mui.Themes.mobile else Mui.Themes.desktop

    let serverErrorBanner =
        match Client.serverError state with
        | Some error ->
            Components.Notice.View
                {|
                    kind = Components.Notice.Kind.Error
                    title = Some "Server probleem"
                    message = error.Message
                    action = None
                    onClose = Some(fun () -> dispatch (Msg.Shell ShellMachine.ShellMsg.ServerErrorDismissed))
                |}
        | None -> null

    let leaveDialog =
        Views.LeaveDialog.View
            {|
                isOpen = Client.urlAsked state
                launched = Client.launched state
                asksLaunch = Client.asksLaunch state
                getTerm = getTerm
                onConfirm = fun () -> Msg.Shell ShellMachine.ShellMsg.LeftForUrl |> dispatch
                onCancel = fun () -> Msg.Shell ShellMachine.ShellMsg.UrlKept |> dispatch
            |}

    let alertSnackbar =
        Views.AlertSnackbar.View
            {|
                alert = Client.snackbar state
                getTerm = getTerm
                onClose = fun () -> Msg.Shell ShellMachine.ShellMsg.SnackbarClosed |> dispatch
            |}

    let onUrlChanged sl = Msg.UrlChanged(sl, Elmish.parseUrl sl) |> dispatch

    // the quantity fields read whether one of them counts, and report their own count
    let counting: Global.Counting =
        {
            Counting = Client.counting state
            Report = ShellMachine.ShellMsg.CountingChanged >> Msg.Shell >> dispatch
        }

    let genPresProps =
        {|
            appEnv = appEnv
            showDisclaimer = Client.showDisclaimer state
            isDemo = Client.isDemo state
            acceptDisclaimer = fun _ -> Msg.Shell ShellMachine.ShellMsg.DisclaimerAccepted |> dispatch
            updatePage = Msg.PageChosen >> dispatch
            page = Client.page state
            languages = Localization.languages
            hospitals = Client.hospitals state
            switchLang = ShellMachine.ShellMsg.LanguageChosen >> Msg.Shell >> dispatch
            switchHosp = ShellMachine.ShellMsg.HospitalChosen >> Msg.Shell >> dispatch
        |}

    JSX.jsx
        $"""
    import {{ ThemeProvider }} from '@mui/material/styles';
    import CssBaseline from '@mui/material/CssBaseline';
    import React from "react";
    import Box from '@mui/material/Box';

    <React.StrictMode>
        <ThemeProvider theme={theme}>
            <Box sx={sx}>
                <CssBaseline />
                {serverErrorBanner}
                {Components.Router.View {| onUrlChanged = onUrlChanged |}}
                {leaveDialog}
                {Pages.GenPres.View genPresProps
                 |> toReact
                 |> Components.Context.Counting counting
                 |> Components.Context.Context context}
            </Box>
            {alertSnackbar}
        </ThemeProvider>
    </React.StrictMode>
    """
