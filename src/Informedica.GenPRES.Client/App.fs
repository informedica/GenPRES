module App

open Informedica.GenPRES.Client.Core.Helpers
open Informedica.GenPRES.Client.Core.Models
open Informedica.GenPRES.Client.Core.Policies
open Informedica.GenPRES.Client.Core.StateMachines
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
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models
open Global
open SessionMachine
open SigningMachine
open OrderPlanMachine
open OrderContextMachine
open PatientMachine
open Informedica.GenPRES.Client.Core.Lanes

module Client = Informedica.GenPRES.Client.Core.Client
module Trail = Informedica.GenPRES.Client.Core.Trail


type private State = Client.ClientState


type private Msg = Client.ClientMsg


module private Messages =


    /// Wraps a message for the machine of one lane.
    let session m = Msg.Lanes(LanesMsg.Session m)
    let signing m = Msg.Lanes(LanesMsg.Signing m)
    let workbench m = Msg.Lanes(LanesMsg.Workbench m)
    let plan m = Msg.Lanes(LanesMsg.Plan m)
    let slot id m = Msg.Lanes(LanesMsg.Slot(id, m))


module private Effects =


    let serverApi =
        Remoting.createApi ()
        |> Remoting.withRouteBuilder Api.routerPaths
        |> Remoting.buildProxy<Api.IServerApi>


    /// Turns a server reply into the message for the lane that sent the request. The response goes
    /// to that lane, and the notice goes to the Session with the Session token sent with the request.
    let answered lane opened (reply: Api.Reply<_>) =
        Msg.Lanes(LanesMsg.Answer(lane (Ok reply.Response), opened, reply.Notice))


    /// Sends a command to the server with the Session's token and turns the outcome into the
    /// lane's message: a reply goes through answered, a refusal or a failed request comes back to
    /// the lane as an Error.
    let underSession call cmd lane opened =
        async {
            try
                match!
                    call
                        {
                            Api.Request.Opened = opened
                            Api.Request.Command = cmd
                        }
                with
                | Ok reply -> return reply |> answered lane opened
                | Error errs -> return Msg.Lanes(lane (Error errs))
            with ex ->
                return Msg.Lanes(lane (Error [| ex.Message |]))
        }
        |> Cmd.fromAsync


    /// Replaces the launch url with "#/session" in the address bar and the history entry, so the launch
    /// token survives no reload, back button or copied url. Uses the History API directly, because
    /// Router.navigate would dispatch a navigation event and trigger UrlChanged again.
    let eraseLaunch () = Browser.Dom.history.replaceState (null, "", "#/session")


    /// Makes the browser key pair, then presents the launch with its public key. When the browser cannot
    /// make a key, the launch is refused with NoBrowserIdentity, whose text asks for a retry and then a
    /// relaunch.
    let presentLaunch (launch: Launch) : Cmd<Msg> =
        async {
            match! Keys.generate () |> Async.Catch with
            | Choice1Of2 key -> return Messages.session (SessionMsg.PresentLaunch(launch, key))
            | Choice2Of2 ex ->
                Logging.error "could not make the browser key pair" ex.Message
                return Messages.session (SessionMsg.LaunchRefused LaunchRefusal.NoBrowserIdentity)
        }
        |> Cmd.fromAsync


    /// Carries out a shell effect: navigates back to a url, erases the launch url or presents a launch.
    let shell effect =
        match effect with
        | ShellMachine.ShellEffect.PutBackUrl sl -> Cmd.ofEffect (fun _ -> Router.navigate (Array.ofList sl))
        // Erased at once, not as a command, so the first router report reads the erased url, the url
        // the app shows, and changes nothing.
        | ShellMachine.ShellEffect.EraseLaunch ->
            eraseLaunch ()
            Cmd.none
        | ShellMachine.ShellEffect.PresentLaunch launch -> presentLaunch launch
        | _ -> Cmd.none


    /// Carries out a loader effect: a call whose answer returns as a loader message, also when the call
    /// fails on a dropped connection or an HTTP error; a delayed message; or a log of a reported failure.
    let loader effect opened =
        let landed landing result = Msg.Loader(LoaderMachine.LoaderMsg.Landed(landing result))

        let call api command landing =
            Cmd.OfAsync.either
                api
                {
                    Api.Request.Opened = opened
                    Api.Request.Command = command
                }
                (landed landing)
                (fun ex -> Error [| ex.Message |] |> landed landing)

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
        | LoaderMachine.LoaderEffect.FetchProducts ->
            Cmd.OfAsync.perform GoogleDocs.loadProducts () (landed LoaderMachine.Landing.Products)
        | LoaderMachine.LoaderEffect.FetchFormulary form ->
            call serverApi.processFormulary form (fun result -> LoaderMachine.Landing.Formulary(opened, result))
        | LoaderMachine.LoaderEffect.FetchParenteralia par ->
            call serverApi.processParenteralia par (fun result -> LoaderMachine.Landing.Parenteralia(opened, result))
        | LoaderMachine.LoaderEffect.FetchInteractions(check, drugs) ->
            call
                serverApi.processInteraction
                (Api.InteractionCommand.CheckInteractions drugs)
                (fun result -> LoaderMachine.Landing.Interactions(check, opened, result))
        | LoaderMachine.LoaderEffect.FetchTotals(number, query) ->
            call serverApi.processTotals query (fun result -> LoaderMachine.Landing.Totals(number, opened, result))
        | LoaderMachine.LoaderEffect.FetchEmergencyList(number, pat) ->
            call
                serverApi.getEmergencyList
                pat
                (fun result -> LoaderMachine.Landing.EmergencyList(number, opened, result))
        | LoaderMachine.LoaderEffect.FetchContinuousList(number, pat) ->
            call
                serverApi.getContinuousList
                pat
                (fun result -> LoaderMachine.Landing.ContinuousList(number, opened, result))
        | LoaderMachine.LoaderEffect.FetchDrugNames ->
            call
                serverApi.processInteraction
                Api.InteractionCommand.GetDrugNames
                (fun result -> LoaderMachine.Landing.DrugNames(opened, result))
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


    /// Carries out an admin effect: a call with the admin token whose answer returns as an admin message,
    /// also when the call fails on a dropped connection or an HTTP error; or a log of a reported failure.
    let admin effect =
        let call cmd landing =
            let landed = landing >> AdminMachine.AdminMsg.Landed >> Msg.Admin

            Cmd.OfAsync.either serverApi.processAdmin cmd landed (fun ex -> Error [| ex.Message |] |> landed)

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


    /// Carries out a session effect. A transport failure returns a message, never an exception: an Error
    /// outcome for a presentation, CloseFailed for a close that did not reach the server.
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
                    | Api.SessionResponse.InactiveTab
                    | Api.SessionResponse.PinRefused _ ->
                        return Messages.session (SessionMsg.Resumed(Ok ResumeResult.NotFound))
                with ex ->
                    return Messages.session (SessionMsg.Resumed(Error ex.Message))
            }
            |> Cmd.fromAsync
        // Opens a signed plan version; the session machine handles the answer.
        | SessionEffect.CallOpenSignedPlan(id, from) ->
            async {
                try
                    match! serverApi.processSession (Api.SessionCommand.OpenVersion(id, from)) with
                    | Api.SessionResponse.SessionResp session ->
                        return Messages.session (SessionMsg.SignedPlanOpened(from, Ok session))
                    | Api.SessionResponse.InactiveTab -> return Messages.session (SessionMsg.InactiveTab from)
                    // never an answer to OpenVersion
                    | _ -> return Messages.session (SessionMsg.SignedPlanOpened(from, Ok None))
                with ex ->
                    return Messages.session (SessionMsg.SignedPlanOpened(from, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallRefreshPatient from ->
            async {
                try
                    match! serverApi.processSession (Api.SessionCommand.Refresh from) with
                    | Api.SessionResponse.SessionResp session ->
                        return Messages.session (SessionMsg.PatientRefreshed(from, Ok session))
                    | Api.SessionResponse.InactiveTab -> return Messages.session (SessionMsg.InactiveTab from)
                    // never an answer to Refresh
                    | _ -> return Messages.session (SessionMsg.PatientRefreshed(from, Ok None))
                with ex ->
                    return Messages.session (SessionMsg.PatientRefreshed(from, Error ex.Message))
            }
            |> Cmd.fromAsync
        | SessionEffect.CallCloseSession token ->
            async {
                // The server deletes the cookie whatever the close returns, unless another tab has the
                // session. Only a request that does not reach the server fails.
                match! serverApi.processSession (Api.SessionCommand.CloseSession token) |> Async.Catch with
                | Choice1Of2 Api.SessionResponse.InactiveTab -> return Messages.session (SessionMsg.InactiveTab token)
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
                    // The server never answers SupplyPin with these; they count as an expired attempt.
                    | Api.SessionResponse.SessionResp None
                    | Api.SessionResponse.SessionClosed
                    | Api.SessionResponse.SessionEnded _
                    | Api.SessionResponse.EnrolmentPending _
                    | Api.SessionResponse.InactiveTab ->
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


    /// Carries out a signing effect with the OpenedToken of the open Session. Every answer carries the
    /// request id or the key it answers, so the machine can drop an answer for an earlier Session.
    /// Without an open Session nothing is sent, and the answer is a NoSession refusal.
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


    /// Carries out an order plan effect. An answer carries the request id it answers, so the machine can
    /// drop an answer to an earlier request; the notice in the reply goes to the Session. A transport
    /// failure is an Error answer, never an exception.
    let plan (effect: OrderPlanEffect) opened : Cmd<Msg> =
        match effect with
        | OrderPlanEffect.CallPlan(cmd, request) ->
            opened
            |> underSession serverApi.processOrderPlan cmd (fun r -> LanesMsg.Plan(OrderPlanMsg.Answered(request, r)))
        | OrderPlanEffect.TellError errs ->
            Logging.error "error" errs
            Cmd.none
        | _ -> Cmd.none


    /// Carries out an order context effect of the workbench or a nutrition slot. The answer goes to the
    /// lane that sent the call, with the request id; the notice in the reply goes to the Session. A
    /// transport failure is an Error answer.
    let orderContext toLane name (effect: OrderContextEffect) opened : Cmd<Msg> =
        match effect with
        | OrderContextEffect.CallContext(cmd, request) ->
            opened
            |> underSession serverApi.processOrderContext cmd (fun r -> toLane (OrderContextMsg.Answered(request, r)))
        | OrderContextEffect.TellError errs ->
            Logging.warning $"%s{name} error" errs
            Cmd.none
        | _ -> Cmd.none


    /// Carries out a patient effect. The answer to a patient data update carries the request id; the
    /// notice in the reply goes to the Session. A transport failure is an Error answer.
    let patient (effect: PatientEffect) opened : Cmd<Msg> =
        match effect with
        | PatientEffect.CallPatient(pat, request) ->
            opened
            |> underSession
                serverApi.processPatientData
                (Api.PatientDataCommand.ChangePatientData pat)
                (fun r -> LanesMsg.Patient(PatientMsg.Answered(request, r)))
        | PatientEffect.CallEdit(edit, pat, request) ->
            opened
            |> underSession
                serverApi.processPatientData
                (Api.PatientDataCommand.EditPatientData(edit, pat))
                (fun r -> LanesMsg.Patient(PatientMsg.Answered(request, r)))
        | PatientEffect.TellError errs ->
            Logging.warning "patient change error" errs
            Cmd.none
        | _ -> Cmd.none


    /// Carries out a client effect with the function for its part. The other effects send nothing,
    /// because the client routed them itself.
    let apply opened effect =
        match effect with
        | Client.ClientEffect.Lanes(LanesEffect.Signing e) -> opened |> signing e
        | Client.ClientEffect.Lanes(LanesEffect.Patient e) -> opened |> patient e
        | Client.ClientEffect.Lanes(LanesEffect.Workbench e) ->
            opened |> orderContext LanesMsg.Workbench "order context" e
        | Client.ClientEffect.Lanes(LanesEffect.Slot(id, e)) ->
            opened |> orderContext (fun m -> LanesMsg.Slot(id, m)) "nutrition slot" e
        | Client.ClientEffect.Lanes(LanesEffect.Plan e) -> opened |> plan e
        | Client.ClientEffect.Lanes(LanesEffect.Session e) -> session e
        | Client.ClientEffect.Loader e -> opened |> loader e
        | Client.ClientEffect.Admin e -> admin e
        | Client.ClientEffect.Shell e -> shell e
        | _ -> Cmd.none


module private Elmish =


    /// Makes a request id at dispatch, so an answer can name the request it answers.
    let newRequest () = Guid.NewGuid().ToString()


    /// Parses the url segments. The parts that do not parse are logged by name, never by value,
    /// because the values are patient data.
    let parseUrl sl =
        let url = sl |> Url.parse

        for part in url.NotParsed do
            match part with
            | Url.UrlPart.Patient parameters -> Logging.warning "could not parse url to patient" parameters
            | Url.UrlPart.Route segment -> Logging.warning "could not parse url" segment

        url


    /// Notes a landing before the client takes it. The settings start the trail when the server serves
    /// the demo data; a failed start-up load is logged, not recorded in the trail. Without settings the
    /// client uses its own defaults and the trail records nothing.
    let noteLanding landing =
        match landing with
        | LoaderMachine.Landing.Settings(Ok settings) -> StepTrail.confirmDemo settings.IsDemo
        | LoaderMachine.Landing.Settings(Error err) ->
            StepTrail.confirmDemo false
            Logging.error "cannot load the server settings" err
        | LoaderMachine.Landing.Localization(Error err) -> Logging.error "cannot load localization" err
        | LoaderMachine.Landing.Products(Error err) -> Logging.error "cannot load products" err
        | _ -> ()


    /// Records the client steps in the trail, noting each landing first, and carries out the effects in
    /// order, each with the Session token of the new state.
    let carryOut (state: State, effects, steps) =
        for step in steps do
            match step with
            | Client.ClientStep.Loader(LoaderMachine.LoaderMsg.Landed landing, _, _) -> noteLanding landing
            | _ -> ()

            StepTrail.record (fun no at -> Trail.client no at step)

        let opened = Client.opened state

        state, effects |> List.map (Effects.apply opened) |> Cmd.batch


    /// Runs the page load. The url is applied at once, so the first router report of the same url
    /// changes nothing. The session is resumed, or left when the url names a patient or a
    /// medication. Then the server is checked and the start-up loads start.
    let init () : State * Cmd<Msg> =
        let sl = Router.currentUrl ()
        let url = sl |> parseUrl

        Client.initial url |> Client.pageLoad newRequest sl url |> carryOut


    /// Passes a message through the client and carries out its effects.
    let update (msg: Msg) (state: State) = state |> Client.transition newRequest msg |> carryOut


/// Builds the app as an Elmish program. A debug build with GENPRES_LOG on and GENPRES_PROD not 1 traces every
/// message and state to the console, connects the Redux DevTools browser extension, and records a trail of the
/// machine steps that window.genpresTrail() returns as text. The trail records nothing until the server settings
/// confirm the demo data, so it never runs against production; a release build records nothing.
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


/// Implements the AppEnv interfaces: the views read the client state and dispatch their messages
/// through them.
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

        member _.RestoreField() = Messages.workbench OrderContextMsg.RestoreField |> dispatch

        member _.Dialog = Client.orderContextDialog state

        member _.SelectScenario id =
            Messages.workbench (OrderContextMsg.SelectScenario id) |> dispatch

        member _.Offers = Client.workbenchOffers state

        member _.Prescribable = Client.workbenchPrescribable state

        member _.TurnOffer(index, switch) =
            match switch with
            | OrderContextMachine.OfferSwitch.On -> Messages.workbench (OrderContextMsg.TurnOfferOn index) |> dispatch
            | OrderContextMachine.OfferSwitch.Off -> Messages.workbench (OrderContextMsg.TurnOfferOff index) |> dispatch

        member _.OfferCommand(index, cmd) =
            Messages.workbench (OrderContextMsg.OfferCommand(index, cmd, Elmish.newRequest ()))
            |> dispatch

    interface AppEnv.IOrderPlan with
        member _.OrderPlan = Client.orderPlan state

        member _.PlanBehind = Client.planBehind state

        member _.PlanDifferences plan = Client.planDifferences state plan


        member _.Add orderId =
            Msg.Lanes(LanesMsg.Prescribe(None, orderId, Elmish.newRequest ())) |> dispatch

        member _.Remove ids =
            Messages.plan (OrderPlanMsg.Change(OrderPlanChange.Remove ids, Elmish.newRequest ()))
            |> dispatch

        member _.OrderDialogCommand(id, cmd) =
            Messages.plan (OrderPlanMsg.Change(OrderPlanChange.OrderDialogCommand(id, cmd), Elmish.newRequest ()))
            |> dispatch

        member _.RestoreField() = Messages.plan OrderPlanMsg.RestoreField |> dispatch

        member _.SelectContext id = Messages.plan (OrderPlanMsg.SelectContext id) |> dispatch

        member _.FilterRows ids =
            Messages.plan (OrderPlanMsg.Change(OrderPlanChange.FilterRows ids, Elmish.newRequest ()))
            |> dispatch

    interface AppEnv.INutrition with
        member _.Slots = Client.slots state

        member _.Totals = Client.nutritionTotals state

        member _.StartSlot category =
            Msg.Lanes(LanesMsg.StartSlot(Elmish.newRequest (), category, Elmish.newRequest ()))
            |> dispatch

        member _.RemoveSlot id = Msg.Lanes(LanesMsg.RemoveSlot id) |> dispatch

        member _.SlotCommand(id, cmd) =
            Messages.slot id (OrderContextMsg.Command(cmd, Elmish.newRequest ()))
            |> dispatch

        member _.SlotRestore id = Messages.slot id OrderContextMsg.RestoreField |> dispatch

        member _.Prescribe(id, orderId) =
            Msg.Lanes(LanesMsg.Prescribe(Some id, orderId, Elmish.newRequest ()))
            |> dispatch

        member _.TurnOffer(id, index, switch) =
            match switch with
            | OrderContextMachine.OfferSwitch.On -> Messages.slot id (OrderContextMsg.TurnOfferOn index) |> dispatch
            | OrderContextMachine.OfferSwitch.Off -> Messages.slot id (OrderContextMsg.TurnOfferOff index) |> dispatch

        member _.OfferCommand(id, index, cmd) =
            Messages.slot id (OrderContextMsg.OfferCommand(index, cmd, Elmish.newRequest ()))
            |> dispatch

    interface AppEnv.IPatient with
        member _.Draft = Client.draft state

        member _.Changing = Client.changing state

        member _.Current = Client.currentPatient state

        member _.ClearPatient() = Msg.PanelCleared(Elmish.newRequest ()) |> dispatch

        member _.EditPatient edit = Msg.PanelEdited(edit, Elmish.newRequest ()) |> dispatch

        member _.AsksNewPatient = Client.asksNewPatient state

        member _.NewPatient edit = Msg.NewPatient(edit, Elmish.newRequest ()) |> dispatch

    interface AppEnv.IFormulary with
        member _.Formulary = Client.formulary state
        member _.UpdateFormulary form =
            Msg.Loader(LoaderMachine.LoaderMsg.FormularyChanged form) |> dispatch

    interface AppEnv.IParenteralia with
        member _.Parenteralia = Client.parenteralia state
        member _.UpdateParenteralia par =
            Msg.Loader(LoaderMachine.LoaderMsg.ParenteraliaChanged par) |> dispatch

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

        member _.EhrMovedOn = Client.ehrMovedOn state

        member _.SignedFor = Client.signedFor state

        // A new request id per Sign, so only the answer to this request is taken.
        member _.Sign plan = Msg.Sign(plan, Elmish.newRequest ()) |> dispatch

        member _.TakeEhrData() = Msg.TakeEhrData |> dispatch

        member _.KeepPlanData() = Messages.signing SigningMsg.KeepPlanData |> dispatch

        // A new key per confirmation, so the signature is committed once; the machine reuses it on a retry.
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

    interface AppEnv.IEmergencyList with
        member _.EmergencyList = Client.emergencyList state
        member _.OnEmergencyListRowChosen number =
            Client.emergencyListRowContext state number
            |> Option.iter (Msg.EmergencyListRowChosen >> dispatch)

        member _.EmergencyListChecked = Client.emergencyListChecked state
        member _.EmergencyListCheckedContexts = Client.emergencyListCheckedContexts state

        member _.OnEmergencyListRowsChecked numbers =
            Msg.Loader(LoaderMachine.LoaderMsg.CheckEmergencyListRows numbers) |> dispatch

        member _.OnEmergencyListPrescribe() =
            let contexts = Client.emergencyListCheckedContexts state
            Msg.Lanes(LanesMsg.PrescribeContexts(contexts, Elmish.newRequest ()))
            |> dispatch
        member _.EmergencyListFilter = Client.emergencyListFilter state
        member _.OnEmergencyListFilterChange filter =
            Msg.Shell(ShellMachine.ShellMsg.EmergencyListFiltered filter) |> dispatch

    interface AppEnv.IContinuousList with
        member _.ContinuousList = Client.continuousList state
        member _.OnContinuousListRowChosen number =
            Client.continuousListRowContext state number
            |> Option.iter (Msg.ContinuousListRowChosen >> dispatch)

        member _.ContinuousListChecked = Client.continuousListChecked state
        member _.ContinuousListCheckedContexts = Client.continuousListCheckedContexts state

        member _.OnContinuousListRowsChecked numbers =
            Msg.Loader(LoaderMachine.LoaderMsg.CheckContinuousListRows numbers) |> dispatch

        member _.OnContinuousListPrescribe() =
            let contexts = Client.continuousListCheckedContexts state
            Msg.Lanes(LanesMsg.PrescribeContexts(contexts, Elmish.newRequest ()))
            |> dispatch
        member _.ContinuousListFilter = Client.continuousMedsFilter state
        member _.OnContinuousListFilterChange filter =
            Msg.Shell(ShellMachine.ShellMsg.ContinuousMedsFiltered filter) |> dispatch


[<JSX.Component>]
let View () =
    let state, dispatch = React.useElmish (program, [||])
    let isMobile = Mui.Hooks.useIsMobile ()

    global.Hooks.LeaveGuard.useLeaveGuard (Client.unsignedWork state)

    let getTerm = Global.getLocalizedTerm (Client.localization state) (Client.language state)

    let context: Global.Context =
        {
            Localization = Client.language state
            Hospital = Client.hospital state
        }

    let appEnv = Projection(state, dispatch) :> obj

    let pageSx =
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
            let tr term = getTerm (ServerErrorPolicy.english term) term

            Components.Notice.View
                {|
                    kind = Components.Notice.Kind.Error
                    title = Some(tr Terms.``Server Error Title``)
                    message = error |> ServerErrorPolicy.message tr
                    action = None
                    onClose = Some(fun () -> dispatch (Msg.Shell ShellMachine.ShellMsg.ServerErrorDismissed))
                |}
        | None -> null

    // Shown above every page, because the menu and the pages wait for this retry.
    let planBehindBanner =
        if Client.planBehind state then
            let tr term =
                getTerm (Informedica.GenPRES.Client.Core.MachinePolicies.SigningPolicy.english term) term

            Components.Notice.View
                {|
                    kind = Components.Notice.Kind.Warning
                    title = None
                    message = tr Terms.``Signing Plan Behind``
                    action =
                        Some
                            {|
                                label = tr Terms.``Signing Plan Behind Retry``
                                onClick =
                                    fun () ->
                                        Msg.Lanes(LanesMsg.Patient(PatientMsg.GiveAgain(Elmish.newRequest ())))
                                        |> dispatch
                                // Never disabled: the notice disappears while its request is pending.
                                disabled = false
                            |}
                    onClose = None
                |}
        else
            null

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

    // The quantity fields read whether one of them is counting, and report their own counting.
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
            <Box sx={pageSx}>
                <CssBaseline />
                {serverErrorBanner}
                {planBehindBanner}
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
