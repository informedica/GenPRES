/// The client as one transition: the lanes, the loads, the admin and the shell. A message runs the
/// part it is for, or is turned into a message for a part over what the parts hold; an effect of one
/// part that another acts on is passed on to that part in the same transition, one way. Every
/// effect still comes out, in the order the parts emitted it, and the App carries out what leaves
/// the client.
module Client

open Shared.Types
open Page
open Lanes
open PatientMachine
open SessionMachine
open SigningMachine
open OrderPlanMachine
open OrderContextMachine
open LoaderMachine
open AdminMachine
open ShellMachine


/// The four parts' states.
type ClientState =
    {
        /// The patient, the workbench, the plan, the Session and the signing.
        Lanes: LanesState
        /// The start-up loads and the pages that follow the workbench.
        Loader: LoaderState
        /// The admin login and what it fetches.
        Admin: AdminState
        /// The url, the page, the language, the snackbar, the error banner and the rest around the
        /// pages.
        Shell: ShellState
    }


/// What reaches the client: a message for one part, or what the user wants that needs what the
/// parts hold.
[<RequireQualifiedAccess>]
type ClientMsg =
    | Lanes of LanesMsg
    | Loader of LoaderMsg
    | Admin of AdminMsg
    | Shell of ShellMsg
    /// The user chose a page; the settings page only shows to a user logged in.
    | PageChosen of Page
    /// The patient panel changed the patient data, with what becomes of the estimates.
    | PanelChanged of Patient option * PatientDraftPolicy.Estimates * request: string
    /// The url changed in the address bar, as segments and as read.
    | UrlChanged of string list * Url.UrlParts
    /// An item chosen on the emergency list.
    | EmergencyListItemChosen of item: string
    /// An item chosen on the continuous medication list.
    | ContinuousMedicationChosen of item: string
    /// Sign the plan, under this request id.
    | Sign of OrderPlan * request: string
    /// The patient data change accepted in the sign dialog.
    | AcceptDataChange


/// The parts' effects, each as its part emitted it.
[<RequireQualifiedAccess>]
type ClientEffect =
    | Lanes of LanesEffect
    | Loader of LoaderEffect
    | Admin of AdminEffect
    | Shell of ShellEffect


/// A step the transition took, for the trail: a part's message with its new state and effects.
[<RequireQualifiedAccess>]
type ClientStep =
    | Lanes of LanesStep
    | Loader of LoaderMsg * LoaderState * LoaderEffect list
    | Admin of AdminMsg * AdminState * AdminEffect list
    | Shell of ShellMsg * ShellState * ShellEffect list
    /// A client message that became no message for any part: it fell to a closing arm.
    | Dropped of ClientMsg


/// What one message did: the state it left, the effects its part emitted, the steps it took and the
/// messages for the parts it became.
type Stepped =
    {
        State: ClientState
        Emitted: ClientEffect list
        Taken: ClientStep list
        Follow: ClientMsg list
    }


/// The client at a page load: the url's patient in the lanes, every other part empty.
let initial (url: Url.UrlParts) =
    {
        Lanes = Lanes.initial url.Patient
        Loader = LoaderState.initial
        Admin = AdminState.initial
        Shell = ShellState.initial
    }


/// The patient the workbench and the plan are for: the draft, once it is a patient.
let patient (state: ClientState) = state.Lanes.Patient |> PatientState.patient


/// Whether a launched Session is open, which a url with a patient, a medication or a launch leaves.
let launched (state: ClientState) =
    match SessionState.view state.Lanes.Session with
    | SessionView.Open _
    | SessionView.Closing _ -> true
    | _ -> false


/// Whether the patient is held; the panel takes no change then.
let held (state: ClientState) =
    HeldPanelPolicy.held (SessionState.view state.Lanes.Session) (OrderPlanState.changed state.Lanes.OrderPlan)


/// Whether leaving the page would lose work.
let unsignedWork (state: ClientState) =
    UnsignedWorkPolicy.hasUnsignedWork
        (state.Lanes.OrderContext |> OrderContextState.context)
        (state.Lanes.Signing |> SigningState.view)
        (state.Lanes.OrderPlan |> OrderPlanState.work)


/// Every data load with its reading, the server check excepted.
let loads (state: ClientState) =
    LoaderState.readings state.Loader @ AdminState.readings state.Admin


/// The requests out in the lanes and the loads.
let busy (state: ClientState) =
    Busy.out
        state.Shell.Counting
        state.Lanes.Patient
        state.Lanes.OrderContext
        state.Lanes.OrderPlan
        state.Lanes.Session
        state.Lanes.Signing
        (state |> loads |> Busy.outOf)


/// Whether any request is out.
let busyAny (state: ClientState) = state |> busy |> Busy.any


/// Whether a request is out that changes this page.
let busyPage page (state: ClientState) = state |> busy |> Busy.page page


/// Where the start-up is; started once, it stays so.
let startup (state: ClientState) =
    if state.Shell.Started then
        StartupPolicy.Startup.Started
    else
        StartupPolicy.status (busy state) (state |> loads |> Busy.loadedOf) state.Loader.Failed


/// What a url change is decided against.
let urlCheck (state: ClientState) =
    {
        SigningUnderWay = state.Lanes.Signing |> SigningState.view |> SigningPolicy.underWay
        Out = busy state
        UnsignedWork = unsignedWork state
        Launched = launched state
    }


/// The localization terms.
let localization (state: ClientState) = state.Loader.Localization


/// The settings the server sent.
let settings (state: ClientState) = state.Loader.Settings


/// The formulary page.
let formulary (state: ClientState) = state.Loader.Formulary


/// The parenteralia page.
let parenteralia (state: ClientState) = state.Loader.Parenteralia


/// The interactions found.
let interactions (state: ClientState) = state.Loader.Interactions


/// The drug names the interactions can be checked for.
let drugNames (state: ClientState) = state.Loader.DrugNames


/// The emergency list's medication.
let bolusMedication (state: ClientState) = state.Loader.BolusMedication


/// The continuous medication.
let continuousMedication (state: ClientState) = state.Loader.ContinuousMedication


/// The hospitals to choose from.
let hospitals (state: ClientState) = state.Loader.Hospitals


/// The resources reload.
let reloading (state: ClientState) = state.Admin.Reloading


/// Whether the admin is logged in.
let isAuthenticated (state: ClientState) = state.Admin.IsAuthenticated


/// The log files listed.
let logFiles (state: ClientState) = state.Admin.LogFiles


/// The report of the log file analysed.
let logAnalysisReport (state: ClientState) = state.Admin.LogAnalysisReport


/// The workbench as the page shows it.
let orderContext (state: ClientState) = state.Lanes.OrderContext |> OrderContextState.view


/// The workbench dialog, if one is open.
let orderContextDialog (state: ClientState) = state.Lanes.OrderContext |> OrderContextState.dialog


/// The order plan as the page shows it.
let orderPlan (state: ClientState) = state.Lanes.OrderPlan |> OrderPlanState.view


/// The orders new or changed since the last signed order plan.
let changedOrders (state: ClientState) = state.Lanes.OrderPlan |> OrderPlanState.changed


/// The patient as the panel shows it.
let draft (state: ClientState) = state.Lanes.Patient |> PatientState.draft


/// Whether a patient change is out.
let changing (state: ClientState) = state.Lanes.Patient |> PatientState.changing


/// The draft with the estimates from the normal values, once they have loaded.
let estimated (state: ClientState) =
    match state.Loader.NormalValues with
    | Resolved nv -> state |> draft |> Option.map (Shared.Models.NormalValues.apply nv)
    | _ -> draft state


/// The Session as the page shows it.
let session (state: ClientState) = state.Lanes.Session |> SessionState.view


/// A newer signed order plan than the one open, if the Session holds one.
let newerPlan (state: ClientState) = state.Lanes.Session |> SessionState.newerPlan


/// The signing as the page shows it.
let signing (state: ClientState) = state.Lanes.Signing |> SigningState.view


/// The differences the signature is asked for.
let differences (state: ClientState) = state.Lanes.Signing |> SigningState.differences


/// The page shown.
let page (state: ClientState) = state.Shell.Page


/// The language the terms are shown in.
let language (state: ClientState) = state.Shell.Language.Current


/// The hospital chosen.
let hospital (state: ClientState) = state.Shell.Hospital


/// The alert in the snackbar, if one is shown.
let snackbar (state: ClientState) = state.Shell.Snackbar


/// The error banner, if it is raised.
let serverError (state: ClientState) = state.Shell.ServerError


/// Whether a quantity field counts.
let counting (state: ClientState) = state.Shell.Counting


/// Whether the server serves the demo data.
let isDemo (state: ClientState) = state.Shell.IsDemo


/// The emergency list's filter.
let emergencyListFilter (state: ClientState) = state.Shell.EmergencyListFilter


/// The continuous medication's filter.
let continuousMedsFilter (state: ClientState) = state.Shell.ContinuousMedsFilter


/// Whether the disclaimer shows: for anonymous use only, so a launched, resuming or refused
/// Session never sees it, an anonymous open after a refusal does.
let showDisclaimer (state: ClientState) =
    state.Shell.ShowDisclaimer
    && (
        match session state with
        | SessionView.Anonymous -> true
        | _ -> false
    )


/// Whether the url question is open.
let urlAsked (state: ClientState) = state.Shell.Url |> UrlPolicy.UrlState.asked |> Option.isSome


/// Whether the url the question waits on carries a launch.
let asksLaunch (state: ClientState) =
    state.Shell.Url
    |> UrlPolicy.UrlState.askedUrl
    |> Option.bind _.Launch
    |> Option.isSome


/// The seed of an item on the emergency list, when the list has loaded and holds it.
let emergencySeed (item: string) (meds: Deferred<BolusMedication list>) =
    match meds with
    | Resolved meds ->
        meds
        |> List.tryFind (fun m -> item.EndsWith($".%s{m.Hospital}.%s{m.Category}.%s{m.Generic}"))
        |> Option.map (fun m ->
            let generic =
                if m.TemplateGeneric = "" then
                    m.Generic
                else
                    m.TemplateGeneric
            FilterSeed.ofListItem generic m.TemplateIndication m.TemplateRoute m.TemplateDoseType
        )
    | _ -> None


/// The seed of an item on the continuous medication list, when the list has loaded and holds it;
/// continuous medication runs intravenously.
let continuousSeed (item: string) (meds: Deferred<ContinuousMedication list>) =
    match meds with
    | Resolved meds ->
        meds
        |> List.tryFind (fun m -> item.EndsWith($".%s{m.Medication}"))
        |> Option.map (fun m -> FilterSeed.ofListItem m.Generic m.Indication "INTRAVENEUS" m.DoseType)
    | _ -> None


/// A medication seeded over the workbench for the patient there is; without one it is dropped and
/// said, since the patient is part of the filter.
let seeded newId seed (state: ClientState) =
    match patient state with
    | Some _ ->
        [
            ClientMsg.Lanes(LanesMsg.Workbench(OrderContextMsg.SeedFilter(seed, newId ())))
        ]
    | None -> [ ClientMsg.Shell(ShellMsg.AlertRaised Alert.Alert.NoPatientForMedication) ]


/// A list item chosen: the Prescribe page and the seed, for the patient there is.
let chosen newId seed (state: ClientState) =
    match seed, patient state with
    | Some seed, Some _ ->
        ClientMsg.Shell(ShellMsg.MovedToPage Page.Page.Prescribe)
        :: seeded newId seed state
    | Some seed, None -> seeded newId seed state
    // an item the list does not hold
    | None, _ -> []


/// One message: a part's step, or the messages for the parts it becomes. A client message that
/// becomes none is dropped, and the step says so.
let step newId msg (state: ClientState) =
    let ran state emitted taken =
        {
            State = state
            Emitted = emitted
            Taken = taken
            Follow = []
        }

    let becomes follow =
        {
            State = state
            Emitted = []
            Taken =
                if List.isEmpty follow then
                    [ ClientStep.Dropped msg ]
                else
                    []
            Follow = follow
        }

    match msg with
    | ClientMsg.Lanes m ->
        let lanes, effects, taken = Lanes.transition newId m state.Lanes
        ran { state with Lanes = lanes } (effects |> List.map ClientEffect.Lanes) (taken |> List.map ClientStep.Lanes)
    | ClientMsg.Loader m ->
        let loader, effects = LoaderMachine.transition m state.Loader
        ran
            { state with Loader = loader }
            (effects |> List.map ClientEffect.Loader)
            [ ClientStep.Loader(m, loader, effects) ]
    | ClientMsg.Admin m ->
        let admin, effects = AdminMachine.transition m state.Admin
        ran
            { state with Admin = admin }
            (effects |> List.map ClientEffect.Admin)
            [ ClientStep.Admin(m, admin, effects) ]
    | ClientMsg.Shell m ->
        let shell, effects = ShellMachine.transition m state.Shell
        ran
            { state with Shell = shell }
            (effects |> List.map ClientEffect.Shell)
            [ ClientStep.Shell(m, shell, effects) ]
    | ClientMsg.PageChosen page -> becomes [ ClientMsg.Shell(ShellMsg.PageChosen(page, state.Admin.IsAuthenticated)) ]
    // the panel is disabled while the patient is held, so a change that still comes is dropped
    | ClientMsg.PanelChanged _ when held state -> becomes []
    | ClientMsg.PanelChanged(pat, estimates, request) ->
        becomes
            [
                ClientMsg.Lanes(LanesMsg.Patient(PatientMsg.Changed(pat, estimates, request)))
            ]
    | ClientMsg.UrlChanged(sl, url) -> becomes [ ClientMsg.Shell(ShellMsg.UrlChanged(sl, url, urlCheck state)) ]
    | ClientMsg.EmergencyListItemChosen item ->
        state
        |> chosen newId (state.Loader.BolusMedication |> emergencySeed item)
        |> becomes
    | ClientMsg.ContinuousMedicationChosen item ->
        state
        |> chosen newId (state.Loader.ContinuousMedication |> continuousSeed item)
        |> becomes
    // the orders that differ are taken now, so what reaches the plan meanwhile leaves them as signed
    | ClientMsg.Sign(plan, request) ->
        let differences = state.Lanes.OrderPlan |> OrderPlanState.differences plan
        becomes
            [
                ClientMsg.Lanes(LanesMsg.Signing(SigningMsg.Sign(plan, differences, request)))
            ]
    // held, the plan keeps the data its new and changed orders were composed on
    | ClientMsg.AcceptDataChange ->
        becomes [ ClientMsg.Lanes(LanesMsg.Signing(SigningMsg.AcceptDataChange(held state))) ]


/// The messages an effect becomes for other parts, over the state at the moment it is passed on;
/// none for an effect that only leaves the client.
let route newId effect (state: ClientState) =
    let alert a = [ ClientMsg.Shell(ShellMsg.AlertRaised a) ]
    let failed source errs = [ ClientMsg.Shell(ShellMsg.ServerErrorRaised(source, errs)) ]
    let succeeded source = [ ClientMsg.Shell(ShellMsg.ServerErrorCleared source) ]
    let first errs = errs |> Array.tryHead |> Option.defaultValue "Er ging iets mis"

    match effect with
    // the lanes: what the user is told, the plan's error banner, the pages and the interactions
    | ClientEffect.Lanes(LanesEffect.Signing(SigningEffect.TellSigned signed)) ->
        alert (Alert.Alert.OrderPlanSigned signed)
    | ClientEffect.Lanes(LanesEffect.Signing(SigningEffect.TellRefused refusal)) ->
        alert (Alert.Alert.SigningRefused refusal)
    | ClientEffect.Lanes(LanesEffect.Signing(SigningEffect.TellError _)) -> alert Alert.Alert.SigningSendFailed
    | ClientEffect.Lanes(LanesEffect.Session(SessionEffect.TellSignedPlanOpened head)) ->
        alert (Alert.Alert.SignedPlanOpened head)
    | ClientEffect.Lanes(LanesEffect.Session SessionEffect.TellPatientRefreshFailed) ->
        alert Alert.Alert.PatientRefreshFailed
    | ClientEffect.Lanes(LanesEffect.Session(SessionEffect.TellNewerSignedPlan head)) ->
        alert (Alert.Alert.NewerSignedPlan head)
    | ClientEffect.Lanes(LanesEffect.Session(SessionEffect.Alert a)) -> alert a
    | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.SetPatientData pat)) ->
        [
            ClientMsg.Shell ShellMsg.ListFiltersCleared
            ClientMsg.Loader(LoaderMsg.PatientSet pat)
        ]
    | ClientEffect.Lanes(LanesEffect.Patient(PatientEffect.TellError errs)) ->
        alert (Alert.Alert.PatientChangeFailed(first errs))
    | ClientEffect.Lanes(LanesEffect.Workbench(OrderContextEffect.SyncPages filter)) ->
        [ ClientMsg.Loader(LoaderMsg.FilterAnswered filter) ]
    | ClientEffect.Lanes(LanesEffect.Workbench(OrderContextEffect.TellError errs)) ->
        alert (Alert.Alert.WorkbenchFailed(first errs))
    | ClientEffect.Lanes(LanesEffect.Plan(OrderPlanEffect.CheckInteractions drugs)) ->
        [ ClientMsg.Loader(LoaderMsg.CheckInteractions drugs) ]
    | ClientEffect.Lanes(LanesEffect.Plan(OrderPlanEffect.TellError errs)) ->
        failed ServerErrorPolicy.ErrorSource.OrderPlan errs
    | ClientEffect.Lanes(LanesEffect.Plan OrderPlanEffect.TellAnswered) ->
        succeeded ServerErrorPolicy.ErrorSource.OrderPlan
    | ClientEffect.Lanes LanesEffect.GoToPlanPage -> [ ClientMsg.Shell(ShellMsg.MovedToPage Page.Page.OrderPlan) ]

    // the loads: the seed to the workbench, the settings and what the user is told to the shell, the
    // notice to the Session
    | ClientEffect.Loader(LoaderEffect.SeedWorkbench seed) ->
        [
            ClientMsg.Lanes(LanesMsg.Workbench(OrderContextMsg.SeedFilter(seed, newId ())))
        ]
    | ClientEffect.Loader(LoaderEffect.SettingsLanded settings) -> [ ClientMsg.Shell(ShellMsg.SettingsLanded settings) ]
    | ClientEffect.Loader(LoaderEffect.Alert a) -> alert a
    | ClientEffect.Loader LoaderEffect.WithdrawInteractionsFound ->
        [ ClientMsg.Shell ShellMsg.WithdrawInteractionsFound ]
    | ClientEffect.Loader(LoaderEffect.RequestFailed(source, errs)) -> failed source errs
    | ClientEffect.Loader(LoaderEffect.RequestSucceeded source) -> succeeded source
    | ClientEffect.Loader(LoaderEffect.NoticeReceived(from, notice)) ->
        [ ClientMsg.Lanes(LanesMsg.Session(SessionMsg.NoticeReceived(from, notice))) ]

    // the admin: the reload to the loads, the logout and what the user is told to the shell
    | ClientEffect.Admin AdminEffect.ReloadDone -> [ ClientMsg.Loader LoaderMsg.ResourcesReloaded ]
    | ClientEffect.Admin AdminEffect.LoggedOut -> [ ClientMsg.Shell ShellMsg.LoggedOut ]
    | ClientEffect.Admin(AdminEffect.Alert a) -> alert a
    | ClientEffect.Admin(AdminEffect.RequestFailed(source, errs)) -> failed source errs
    | ClientEffect.Admin(AdminEffect.RequestSucceeded source) -> succeeded source

    // the shell: the page to the loads, what a url brings to the lanes and the Session
    | ClientEffect.Shell(ShellEffect.PageShown page) -> [ ClientMsg.Loader(LoaderMsg.PageShown page) ]
    // a check still out is for the old plan, and is dropped
    | ClientEffect.Shell(ShellEffect.StartOver pat) ->
        [
            ClientMsg.Loader(LoaderMsg.CheckInteractions [])
            ClientMsg.Lanes(LanesMsg.StartOver pat)
        ]
    // an open Session supplies the patient: a launched patient is never assigned from the url
    | ClientEffect.Shell(ShellEffect.PatientFromUrl _) when launched state -> []
    | ClientEffect.Shell(ShellEffect.PatientFromUrl pat) ->
        [
            ClientMsg.Lanes(LanesMsg.Patient(PatientMsg.Changed(pat, PatientDraftPolicy.Estimates.Renewed, newId ())))
        ]
    | ClientEffect.Shell(ShellEffect.SeedWorkbench seed) -> state |> seeded newId seed
    | ClientEffect.Shell(ShellEffect.LaunchRefused refusal) ->
        [ ClientMsg.Lanes(LanesMsg.Session(SessionMsg.LaunchRefused refusal)) ]
    | ClientEffect.Shell ShellEffect.ResumeSession -> [ ClientMsg.Lanes(LanesMsg.Session SessionMsg.Resume) ]
    | ClientEffect.Shell ShellEffect.LeaveSession -> [ ClientMsg.Lanes(LanesMsg.Session SessionMsg.UrlMovedOn) ]
    | _ -> []


/// What waits to be run: a message, or an effect to pass on.
[<RequireQualifiedAccess>]
type Queued =
    | Run of ClientMsg
    | Pass of ClientEffect


/// The next state, the effects that come out and the steps taken, for these messages. A message's
/// effects are passed on one by one, each over the state the effects before it left, and what they
/// become runs before anything still waiting. The routes run one way, so the queue empties. The
/// start-up is marked as ended at the first transition in which it is.
let run newId msgs (state: ClientState) =
    let rec go queue (state, effects, steps) =
        match queue with
        | [] -> state, effects, steps
        | Queued.Run msg :: rest ->
            let stepped = state |> step newId msg

            let queue =
                (stepped.Follow |> List.map Queued.Run)
                @ (stepped.Emitted |> List.map Queued.Pass)
                @ rest

            go queue (stepped.State, effects @ stepped.Emitted, steps @ stepped.Taken)
        | Queued.Pass effect :: rest ->
            let follow = state |> route newId effect |> List.map Queued.Run
            go (follow @ rest) (state, effects, steps)

    let state, effects, steps = go (msgs |> List.map Queued.Run) (state, [], [])

    match startup state with
    | StartupPolicy.Startup.Started when not state.Shell.Started ->
        let state, ended, taken = go [ Queued.Run(ClientMsg.Shell ShellMsg.StartupEnded) ] (state, [], [])
        state, effects @ ended, steps @ taken
    | _ -> state, effects, steps


/// The next state, the effects that come out and the steps taken, for a message.
let transition newId msg state = state |> run newId [ msg ]


/// The page load: the url applied, the Session resumed or left, the server checked and the
/// start-up loads asked for.
let pageLoad newId sl (url: Url.UrlParts) state =
    state
    |> run
        newId
        [
            ClientMsg.Shell(ShellMsg.PageLoaded(sl, url))
            ClientMsg.Loader LoaderMsg.CheckServer
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
                ClientMsg.Loader(LoaderMsg.Start load)
        ]
