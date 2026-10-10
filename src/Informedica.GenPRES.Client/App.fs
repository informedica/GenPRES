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


    type State = Client.ClientState


    type Msg = Client.ClientMsg


    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder Api.routerPaths
        |> Remoting.buildProxy<Api.IServerApi>


    /// The OpenedToken the Session holds, sent with every computing request; none
    /// without an open Session.
    let tokenOf = SessionState.token


    /// A request id, minted at dispatch, so that an answer can name the request it answers.
    let newRequest () = Guid.NewGuid().ToString()


    /// A message to one of the lanes' machines.
    let sessionMsg m = Msg.Lanes(LanesMsg.Session m)
    let signingMsg m = Msg.Lanes(LanesMsg.Signing m)
    let patientMsg m = Msg.Lanes(LanesMsg.Patient m)
    let workbenchMsg m = Msg.Lanes(LanesMsg.Workbench m)
    let planMsg m = Msg.Lanes(LanesMsg.Plan m)


    /// A reply under the session for the lane message it answers: the answer reaches its machine,
    /// the notice the Session, with the token the request was sent with.
    let answered lane opened (reply: Api.Reply<_>) =
        Msg.Lanes(LanesMsg.Answer(lane (Ok reply.Response), opened, reply.Notice))


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


    /// Make the key pair, then present the Launch with its public key.
    /// A browser that cannot make a key cannot launch; that is reported as a missing browser
    /// identity, the refusal whose text asks for a retry and then a relaunch.
    let presentLaunch (launch: Launch) : Cmd<Msg> =
        async {
            match! Keys.generate () |> Async.Catch with
            | Choice1Of2 key -> return sessionMsg (SessionMsg.PresentLaunch(launch, key))
            | Choice2Of2 ex ->
                Logging.error "could not make the browser key pair" ex.Message
                return sessionMsg (SessionMsg.LaunchRefused LaunchRefusal.NoBrowserIdentity)
        }
        |> Cmd.fromAsync


    let applyNormalValues (normalValues: Deferred<NormalValues>) (pat: Patient option) =
        match normalValues, pat with
        | Resolved nv, Some p ->
            p
            |> Patient.applyNormalValues (Some nv.Weights) (Some nv.Heights) (Some nv.NeoWeights) (Some nv.NeoHeights)
            |> Some
        | _ -> pat


    /// What a shell effect sends out of the client: the url put back or erased, a launch presented.
    let applyShellEffect effect =
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
    let applyLoaderEffect effect (state: State) =
        let landed landing result = Msg.Loader(LoaderMachine.LoaderMsg.Landed(landing result))

        let opened = tokenOf state.Lanes.Session

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


    /// What an admin effect sends out of the client: a call under the token, whose answer comes back
    /// as the admin machine's message; a failure is logged.
    let applyAdminEffect effect =
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
    let applySessionEffect (effect: SessionEffect) (state: State) : Cmd<Msg> =
        match effect with
        | SessionEffect.CallPresentLaunch(launch, key) ->
            async {
                try
                    let! outcome = serverApi.processLaunch (Api.LaunchCommand.PresentLaunch(launch, key))
                    return sessionMsg (SessionMsg.LaunchOutcome(launch, key, Ok outcome))
                with ex ->
                    return sessionMsg (SessionMsg.LaunchOutcome(launch, key, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallResume ->
            async {
                try
                    match! serverApi.processSession Api.SessionCommand.GetSession with
                    | Api.SessionResponse.SessionResp(Some session) ->
                        return sessionMsg (SessionMsg.Resumed(Ok(ResumeResult.Found session)))
                    | Api.SessionResponse.SessionResp None
                    | Api.SessionResponse.SessionClosed ->
                        return sessionMsg (SessionMsg.Resumed(Ok ResumeResult.NotFound))
                    | Api.SessionResponse.SessionEnded ending ->
                        return sessionMsg (SessionMsg.Resumed(Ok(ResumeResult.Ended ending)))
                    | Api.SessionResponse.EnrolmentPending pending ->
                        return sessionMsg (SessionMsg.Resumed(Ok(ResumeResult.Enrolling pending)))
                    // never an answer to GetSession
                    | Api.SessionResponse.PinRefused _ ->
                        return sessionMsg (SessionMsg.Resumed(Ok ResumeResult.NotFound))
                with ex ->
                    return sessionMsg (SessionMsg.Resumed(Error ex.Message))
            }
            |> Cmd.fromAsync
        // the version is open, or the record moved on: each said once, the machine decides
        | SessionEffect.CallOpenSignedPlan(id, from) ->
            async {
                try
                    match! serverApi.processSession (Api.SessionCommand.OpenVersion id) with
                    | Api.SessionResponse.SessionResp opened ->
                        return sessionMsg (SessionMsg.SignedPlanOpened(from, Ok opened))
                    // never an answer to OpenVersion
                    | _ -> return sessionMsg (SessionMsg.SignedPlanOpened(from, Ok None))
                with ex ->
                    return sessionMsg (SessionMsg.SignedPlanOpened(from, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallRefreshPatient from ->
            async {
                try
                    match! serverApi.processSession Api.SessionCommand.Refresh with
                    | Api.SessionResponse.SessionResp opened ->
                        return sessionMsg (SessionMsg.PatientRefreshed(from, Ok opened))
                    // never an answer to Refresh
                    | _ -> return sessionMsg (SessionMsg.PatientRefreshed(from, Ok None))
                with ex ->
                    return sessionMsg (SessionMsg.PatientRefreshed(from, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallCloseSession ->
            async {
                // the server deletes the cookie whatever its close returns (finally), so an
                // answer of any kind means SessionClosed; only a request that never got there fails
                match! serverApi.processSession Api.SessionCommand.CloseSession |> Async.Catch with
                | Choice1Of2 _ -> return sessionMsg SessionMsg.SessionClosed
                | Choice2Of2 ex ->
                    Logging.error "could not close the session on the server" ex.Message
                    return sessionMsg (SessionMsg.CloseFailed ex.Message)
            }
            |> Cmd.fromAsync
        | SessionEffect.CallSupplyPin(code, pin) ->
            async {
                try
                    match! serverApi.processSession (Api.SessionCommand.SupplyPin(code, pin)) with
                    | Api.SessionResponse.SessionResp(Some session) ->
                        return sessionMsg (SessionMsg.PinAnswered(Ok(PinOutcome.Opened session)))
                    | Api.SessionResponse.PinRefused refusal ->
                        return sessionMsg (SessionMsg.PinAnswered(Ok(PinOutcome.Refused refusal)))
                    // never an answer to SupplyPin: the attempt is gone, whatever happened
                    | Api.SessionResponse.SessionResp None
                    | Api.SessionResponse.SessionClosed
                    | Api.SessionResponse.SessionEnded _
                    | Api.SessionResponse.EnrolmentPending _ ->
                        return sessionMsg (SessionMsg.PinAnswered(Ok(PinOutcome.Refused PinRefusal.AttemptExpired)))
                with ex ->
                    Logging.error "could not send the PIN to the server" ex.Message
                    return sessionMsg (SessionMsg.PinAnswered(Error ex.Message))
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
    /// the challenge, the PIN, the request id and the key; the OpenedToken comes from the open
    /// Session here, and every answer carries the request id or the key it answers, so the
    /// machine can drop one that belongs to an earlier Session. Without an open Session nothing
    /// is sent: the answer is a refusal.
    let applySigningEffect (effect: SigningEffect) (state: State) : Cmd<Msg> =
        let token = tokenOf state.Lanes.Session

        match effect with
        | SigningEffect.CallChallenge(plan, notice, request) ->
            match token with
            | None ->
                Cmd.ofMsg (
                    signingMsg (
                        SigningMsg.ChallengeAnswered(request, Ok(SigningResponse.Refused SigningRefusal.NoSession))
                    )
                )
            | Some opened ->
                async {
                    try
                        let! answer =
                            serverApi.processSigning (Api.SigningCommand.RequestSignChallenge(plan, opened, notice))

                        return signingMsg (SigningMsg.ChallengeAnswered(request, Ok answer))
                    with ex ->
                        return signingMsg (SigningMsg.ChallengeAnswered(request, Error ex.Message))
                }
                |> Cmd.fromAsync
        | SigningEffect.CallSubmit(plan, challenge, pin, key) ->
            match token with
            | None ->
                Cmd.ofMsg (
                    signingMsg (SigningMsg.SubmitAnswered(key, Ok(SigningResponse.Refused SigningRefusal.NoSession)))
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

                        return signingMsg (SigningMsg.SubmitAnswered(key, Ok answer))
                    with ex ->
                        return signingMsg (SigningMsg.SubmitAnswered(key, Error ex.Message))
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
    let applyOrderPlanEffect (effect: OrderPlanEffect) (state: State) : Cmd<Msg> =
        match effect with
        | OrderPlanEffect.CallPlan(cmd, request) ->
            let opened = tokenOf state.Lanes.Session

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
                            reply
                            |> answered (fun r -> LanesMsg.Plan(OrderPlanMsg.Answered(request, r))) opened
                    | Error errs -> return planMsg (OrderPlanMsg.Answered(request, Error errs))
                with ex ->
                    return planMsg (OrderPlanMsg.Answered(request, Error [| ex.Message |]))
            }
            |> Cmd.fromAsync
        | OrderPlanEffect.TellError errs ->
            Logging.error "error" errs
            Cmd.none
        | _ -> Cmd.none


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
                        reply
                        |> answered (fun r -> LanesMsg.Workbench(OrderContextMsg.Answered(request, r))) opened
                | Error errs -> return workbenchMsg (OrderContextMsg.Answered(request, Error errs))
            with ex ->
                return workbenchMsg (OrderContextMsg.Answered(request, Error [| ex.Message |]))
        }
        |> Cmd.fromAsync


    /// What an order-context effect sends out of the client. A workbench call
    /// answers under the request it was sent for; the notice rides on the reply to the Session; a
    /// transport failure is an Error answer.
    let applyOrderContextEffect (effect: OrderContextEffect) (state: State) : Cmd<Msg> =
        match effect with
        | OrderContextEffect.CallContext(sent, request) -> callContext sent request state
        | OrderContextEffect.TellError errs ->
            Logging.warning "order context error" errs
            Cmd.none
        | _ -> Cmd.none


    /// What a patient effect sends out of the client. A patient change answers under
    /// the request it was sent for; the notice rides on the reply to the Session; a transport
    /// failure is an Error answer.
    let applyPatientEffect (effect: PatientEffect) (state: State) : Cmd<Msg> =
        match effect with
        | PatientEffect.CallPatient(pat, request) ->
            let opened = tokenOf state.Lanes.Session

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
                            reply
                            |> answered (fun r -> LanesMsg.Patient(PatientMsg.Answered(request, r))) opened
                    | Error errs -> return patientMsg (PatientMsg.Answered(request, Error errs))
                with ex ->
                    return patientMsg (PatientMsg.Answered(request, Error [| ex.Message |]))
            }
            |> Cmd.fromAsync
        | PatientEffect.TellError errs ->
            Logging.warning "patient change error" errs
            Cmd.none
        | _ -> Cmd.none


    /// What an effect of the client sends out of it, each by its part's apply; the routed ones send
    /// nothing, since the client passed them on itself.
    let applyEffect effect (state: State) =
        match effect with
        | Client.ClientEffect.Lanes(LanesEffect.Signing e) -> state |> applySigningEffect e
        | Client.ClientEffect.Lanes(LanesEffect.Patient e) -> state |> applyPatientEffect e
        | Client.ClientEffect.Lanes(LanesEffect.Workbench e) -> state |> applyOrderContextEffect e
        | Client.ClientEffect.Lanes(LanesEffect.Plan e) -> state |> applyOrderPlanEffect e
        | Client.ClientEffect.Lanes(LanesEffect.Session e) -> state |> applySessionEffect e
        | Client.ClientEffect.Loader e -> state |> applyLoaderEffect e
        | Client.ClientEffect.Admin e -> applyAdminEffect e
        | Client.ClientEffect.Shell e -> applyShellEffect e
        | _ -> Cmd.none


    /// The client's steps recorded in the trail, each landing noted first, and its effects carried
    /// out in the order they came, each over the state the transition left.
    let carryOut (state: State, effects, steps) =
        for step in steps do
            match step with
            | Client.ClientStep.Loader(LoaderMachine.LoaderMsg.Landed landing, _, _) -> noteLanding landing
            | _ -> ()

            StepTrail.record (fun no at -> Trail.client no at step)

        state, effects |> List.map (fun effect -> state |> applyEffect effect) |> Cmd.batch


    /// The page load: the url applied at once, so the router's first report, of the same url,
    /// changes nothing; then the Session resumed, or left for a url with a patient or a
    /// medication, and the loads started.
    let init () : State * Cmd<Msg> =
        let sl = Router.currentUrl ()
        let url = sl |> parseUrl

        Client.initial url |> Client.pageLoad newRequest sl url |> carryOut


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


    /// A message through the client, and its effects carried out.
    let update (msg: Msg) (state: State) = state |> Client.transition newRequest msg |> carryOut


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
    | Msg.Admin(AdminMachine.AdminMsg.Login _) -> Msg.Admin(AdminMachine.AdminMsg.Login redacted)
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
    let program = Program.mkProgram init update (fun _ _ -> ())
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
        member _.Startup = Client.startup state

    interface AppEnv.IBusy with
        member _.Any = state |> Client.busy |> Busy.any
        member _.Page page = state |> Client.busy |> Busy.page page

    interface AppEnv.IOrderContext with
        member _.OrderContext = state.Lanes.OrderContext |> OrderContextState.view

        member _.OrderContextMsg cmd =
            workbenchMsg (OrderContextMsg.Command(cmd, newRequest ())) |> dispatch

        member _.ReopenField cmd =
            workbenchMsg (OrderContextMsg.ReopenField(cmd, newRequest ())) |> dispatch

        member _.RestoreField() = workbenchMsg OrderContextMsg.RestoreField |> dispatch

        member _.Dialog = state.Lanes.OrderContext |> OrderContextState.dialog

        member _.SelectScenario id = workbenchMsg (OrderContextMsg.SelectScenario id) |> dispatch

    interface AppEnv.IOrderPlan with
        member _.OrderPlan = state.Lanes.OrderPlan |> OrderPlanState.view

        member _.Add orderId =
            Msg.Lanes(LanesMsg.Prescribe(orderId, newRequest (), newRequest ())) |> dispatch

        member _.NewNutrition category =
            planMsg (OrderPlanMsg.Change(OrderPlanChange.NewNutrition category, newRequest ()))
            |> dispatch

        member _.Remove ids =
            planMsg (OrderPlanMsg.Change(OrderPlanChange.Remove ids, newRequest ()))
            |> dispatch

        member _.OrderDialogCommand(id, cmd) =
            planMsg (OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand(id, cmd), newRequest ()))
            |> dispatch

        member _.ReopenField(id, cmd) =
            planMsg (OrderPlanMsg.ReopenField(id, cmd, newRequest ())) |> dispatch

        member _.RestoreField() = planMsg OrderPlanMsg.RestoreField |> dispatch

        member _.SelectContext id = planMsg (OrderPlanMsg.SelectContext id) |> dispatch

        member _.FilterRows ids =
            planMsg (OrderPlanMsg.Change(OrderPlanChange.FilterRows ids, newRequest ()))
            |> dispatch

        member _.Changed = state.Lanes.OrderPlan |> OrderPlanState.changed

    interface AppEnv.IPatient with
        member _.Draft = state.Lanes.Patient |> PatientState.draft

        member _.Changing = state.Lanes.Patient |> PatientState.changing

        member _.Estimated =
            state.Lanes.Patient
            |> PatientState.draft
            |> applyNormalValues state.Loader.NormalValues
        member _.UpdatePatient p =
            Msg.PanelChanged(p, PatientDraftPolicy.Estimates.Renewed, newRequest ())
            |> dispatch

        member _.EditPatient p =
            Msg.PanelChanged(p, PatientDraftPolicy.Estimates.Kept, newRequest ())
            |> dispatch

    interface AppEnv.IFormulary with
        member _.Formulary = state.Loader.Formulary
        member _.UpdateFormulary f =
            Msg.Loader(LoaderMachine.LoaderMsg.FormularyChanged f) |> dispatch

    interface AppEnv.IParenteralia with
        member _.Parenteralia = state.Loader.Parenteralia
        member _.UpdateParenteralia p =
            Msg.Loader(LoaderMachine.LoaderMsg.ParenteraliaChanged p) |> dispatch

    interface AppEnv.IInteractions with
        member _.Interactions = state.Loader.Interactions
        member _.InteractionDrugNames = state.Loader.DrugNames
        member _.CheckInteractions drugs =
            Msg.Loader(LoaderMachine.LoaderMsg.CheckInteractions drugs) |> dispatch

    interface AppEnv.IResources with
        member _.Reload = state.Admin.Reloading
        member _.ReloadResources() = Msg.Admin AdminMachine.AdminMsg.ReloadResources |> dispatch

    interface AppEnv.ISession with
        member _.Session = state.Lanes.Session |> SessionState.view
        member _.Close() = sessionMsg SessionMsg.CloseSession |> dispatch
        member _.RetryLaunch() = sessionMsg SessionMsg.RetryLaunch |> dispatch

        member _.OpenAnonymously() = sessionMsg SessionMsg.ContinueAnonymous |> dispatch

        member _.SupplyPin code pin = sessionMsg (SessionMsg.SupplyPin(code, pin)) |> dispatch

        member _.NewerPlan = state.Lanes.Session |> SessionState.newerPlan

        member _.OpenSignedPlan id = sessionMsg (SessionMsg.OpenSignedPlan id) |> dispatch

        member _.Refresh() = sessionMsg SessionMsg.RefreshPatient |> dispatch

    interface AppEnv.ISigning with
        member _.Signing = state.Lanes.Signing |> SigningState.view

        member _.Differences = state.Lanes.Signing |> SigningState.differences

        // one request id per Sign, so the answer lands on this request and no other
        member _.Sign plan = Msg.Sign(plan, Guid.NewGuid().ToString()) |> dispatch

        member _.Held = Client.held state

        member _.Accept() = Msg.AcceptDataChange |> dispatch

        // one key per confirmation, so the commit takes effect once; the machine keeps it for a retry
        member _.Confirm pin =
            signingMsg (SigningMsg.ConfirmPin(pin, Guid.NewGuid().ToString())) |> dispatch

        member _.Cancel() = signingMsg SigningMsg.Cancel |> dispatch

    interface AppEnv.IAuthentication with
        member _.IsAuthenticated = state.Admin.IsAuthenticated
        member _.Login password = Msg.Admin(AdminMachine.AdminMsg.Login password) |> dispatch

        member _.Logout() = Msg.Admin AdminMachine.AdminMsg.Logout |> dispatch

    interface AppEnv.ILogAnalyzer with
        member _.LogFiles = state.Admin.LogFiles
        member _.LogAnalysisReport = state.Admin.LogAnalysisReport
        member _.ListLogFiles() = Msg.Admin AdminMachine.AdminMsg.ListLogFiles |> dispatch

        member _.AnalyzeLogFile fileName =
            Msg.Admin(AdminMachine.AdminMsg.AnalyzeLogFile fileName) |> dispatch

    interface AppEnv.IBolusMedication with
        member _.BolusMedication = bm
        member _.OnSelectBolusMedicationItem s = Msg.EmergencyListItemChosen s |> dispatch
        member _.BolusMedicationFilter = state.Shell.EmergencyListFilter
        member _.OnBolusMedicationFilterChange f =
            Msg.Shell(ShellMachine.ShellMsg.EmergencyListFiltered f) |> dispatch

    interface AppEnv.IContinuousMedication with
        member _.ContinuousMedication = cm

        member _.OnSelectContinuousMedicationItem s = Msg.ContinuousMedicationChosen s |> dispatch

        member _.ContinuousMedicationFilter = state.Shell.ContinuousMedsFilter

        member _.OnContinuousMedicationFilterChange f =
            Msg.Shell(ShellMachine.ShellMsg.ContinuousMedsFiltered f) |> dispatch


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
            if Client.unsignedWork stateRef.current then
                ev.preventDefault ()
                // the browser shows its own dialog; older browsers need a returnValue for it
                ev?returnValue <- ""

        window.addEventListener ("beforeunload", guard)

        fun () -> window.removeEventListener ("beforeunload", guard)
    )

    let handleClose =
        fun (_: obj) (reason: string) ->
            if reason <> "clickaway" then
                Msg.Shell ShellMachine.ShellMsg.SnackbarClosed |> dispatch

    let closeSnackbar _ = Msg.Shell ShellMachine.ShellMsg.SnackbarClosed |> dispatch

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
                    onClose = Some(fun () -> dispatch (Msg.Shell ShellMachine.ShellMsg.ServerErrorDismissed))
                |}
        | None -> null

    // the question before a url with a patient, a medication or a launch: it leaves the
    // launched Session, or drops the work not signed. The browser asks the second itself for a
    // reload or a closed tab, but not for a change of the url
    let asksLaunch =
        state.Shell.Url
        |> UrlPolicy.UrlState.askedUrl
        |> Option.bind _.Launch
        |> Option.isSome

    let title =
        if Client.launched state then
            Terms.``Url Leave Session Title`` |> getTerm "Sessie verlaten?"
        else
            Terms.``Url Leave Title`` |> getTerm "Orderplan verlaten?"

    let text =
        match Client.launched state, asksLaunch with
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
                isOpen = (state.Shell.Url |> UrlPolicy.UrlState.asked).IsSome
                title = title
                text = text
                confirmLabel = Terms.``Url Leave`` |> getTerm "Verlaten"
                cancelLabel = Terms.Cancel |> getTerm "Annuleren"
                onConfirm = fun () -> Msg.Shell ShellMachine.ShellMsg.LeftForUrl |> dispatch
                onCancel = fun () -> Msg.Shell ShellMachine.ShellMsg.UrlKept |> dispatch
            |}

    // the quantity fields read whether one of them counts, and report their own count
    let counting: Global.Counting =
        {
            Counting = state.Shell.Counting
            Report = ShellMachine.ShellMsg.CountingChanged >> Msg.Shell >> dispatch
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
            acceptDisclaimer = fun _ -> Msg.Shell ShellMachine.ShellMsg.DisclaimerAccepted |> dispatch
            updatePage = Msg.PageChosen >> dispatch
            page = state.Shell.Page
            languages = Localization.languages
            hospitals = state.Loader.Hospitals
            switchLang = ShellMachine.ShellMsg.LanguageChosen >> Msg.Shell >> dispatch
            switchHosp = ShellMachine.ShellMsg.HospitalChosen >> Msg.Shell >> dispatch
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
                {Components.Router.View {| onUrlChanged = (fun sl -> Msg.UrlChanged(sl, parseUrl sl)) >> dispatch |}}
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
