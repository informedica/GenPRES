namespace Informedica.GenPRES.Client.Core

/// Combines the lanes, the loader, the admin and the shell into one transition. A message runs the part it is
/// for, or is translated into messages for the parts using the current state. An effect of one part that
/// another part acts on is routed to that part in the same transition, in one direction only. Every effect is
/// also returned, in the order the parts emitted them, and the App carries out the effects that leave the
/// client.
module Client =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Client.Core.MachinePolicies
    open Informedica.GenPRES.Shared.Types
    open Page
    open Loads
    open Lanes
    open PatientMachine
    open SessionMachine
    open SigningMachine
    open OrderPlanMachine
    open OrderContextMachine
    open LoaderMachine
    open AdminMachine
    open ShellMachine

    module Models = Informedica.GenPRES.Shared.Models


    /// The states of the four parts.
    type ClientState =
        {
            /// The patient, the workbench, the plan, the Session and the signing.
            Lanes: LanesState
            /// The start-up loads and the pages that follow the workbench filter.
            Loader: LoaderState
            /// The admin login and the admin loads.
            Admin: AdminState
            /// The url, the page, the language, the snackbar, the error banner and the other state around
            /// the pages.
            Shell: ShellState
        }


    /// A nutrition slot as the nutrition page shows it.
    type SlotView =
        {
            /// The slot id the client created when the slot started.
            Id: string
            /// The view of the slot context from the order context machine.
            Context: OrderContextView
            /// The linked orders the link rules offer with the order of the slot, each turned on or off.
            Offers: LinkedOffer[]
            /// The id of the order that prescribing adds. Set when the slot is settled and narrowed to one
            /// scenario and every linked order turned on is narrowed to one scenario too; None otherwise.
            PrescribeOrderId: string option
            /// Whether the order of this slot is being added to the plan; the slot accepts no input meanwhile.
            Prescribing: bool
        }


    /// A message to the client: a message for one part, or a user action that needs the state of several
    /// parts.
    [<RequireQualifiedAccess>]
    type ClientMsg =
        | Lanes of LanesMsg
        | Loader of LoaderMsg
        | Admin of AdminMsg
        | Shell of ShellMsg
        /// The user chose a page; the settings page shows only to a logged-in admin.
        | PageChosen of Page
        /// The patient panel changed the patient data, with whether the estimates are renewed.
        | PanelChanged of PatientData option * PatientDraftPolicy.Estimates * request: string
        /// Another patient from the panel, after the prescriber agreed. Unlike a patient data update, which
        /// recalculates the orders, this clears everything first and then sets the new patient data.
        | NewPatient of PatientData option * PatientDraftPolicy.Estimates * request: string
        /// The url changed in the address bar, as path segments and as parsed parts.
        | UrlChanged of string list * Url.UrlParts
        /// An item chosen on the emergency list.
        | EmergencyListItemChosen of item: string
        /// An item chosen on the continuous medication list.
        | ContinuousMedicationChosen of item: string
        /// Signs the plan with this request id.
        | Sign of OrderPlan * request: string
        /// Takes the EHR data in the sign dialog.
        | TakeEhrData


    /// An effect of one of the parts, as the part emitted it.
    [<RequireQualifiedAccess>]
    type ClientEffect =
        | Lanes of LanesEffect
        | Loader of LoaderEffect
        | Admin of AdminEffect
        | Shell of ShellEffect


    /// A step of the transition, for the trail: a message to a part with the new state and the effects.
    [<RequireQualifiedAccess>]
    type ClientStep =
        | Lanes of LanesStep
        | Loader of LoaderMsg * LoaderState * LoaderEffect list
        | Admin of AdminMsg * AdminState * AdminEffect list
        | Shell of ShellMsg * ShellState * ShellEffect list
        /// A client message that produced no message for any part.
        | Dropped of ClientMsg


    /// The result of one message: the new state, the emitted effects, the steps taken and the follow-up
    /// messages for the parts.
    type Stepped =
        {
            State: ClientState
            Emitted: ClientEffect list
            Taken: ClientStep list
            Follow: ClientMsg list
        }


    /// Creates the client state at a page load, with the patient data from the url in the lanes.
    let initial (url: Url.UrlParts) =
        {
            Lanes = Lanes.initial url.Patient
            Loader = LoaderState.initial
            Admin = AdminState.initial
            Shell = ShellState.initial
        }


    /// Returns the draft when it is valid patient data, the patient the workbench and the plan are for.
    let patient (state: ClientState) = state.Lanes.Patient |> PatientState.patient


    /// Builds the combined totals query of the nutrition page: the patient data, the filtered orders of the
    /// plan, and each nutrition slot narrowed to a single order with its linked orders turned on. None when no
    /// slot is narrowed to a single order, because the plan totals then apply. A slot uses the context of its
    /// last response, not the context shown during a request.
    let totalsQuery (state: ClientState) =
        let slotOrders =
            state.Lanes.Slots
            |> Seq.map snd
            |> Seq.collect (fun slot ->
                match slot |> OrderContextState.workbenchContext with
                | Some ctx when ctx |> Models.OrderContext.contribution |> Option.isSome ->
                    Array.append [| ctx |] (slot |> OrderContextState.linkedTurnedOn)
                | _ -> [||]
            )
            |> Seq.toArray

        match state.Lanes.OrderPlan |> OrderPlanState.plan with
        | Some plan when slotOrders |> Array.isEmpty |> not ->
            // The annotation selects the TotalsQuery record, which has the same field names as the plan.
            let query: Informedica.GenPRES.Shared.Api.TotalsQuery =
                {
                    PatientData = plan.PatientData
                    OrderContexts = Array.append (plan |> Models.OrderPlan.filtered) slotOrders
                }

            Some query
        | _ -> None


    /// Whether a launched Session is open or closing.
    let launched (state: ClientState) =
        match SessionState.view state.Lanes.Session with
        | SessionView.Open _
        | SessionView.Closing _ -> true
        | _ -> false


    /// Returns the contexts of the nutrition slots.
    let slotContexts (state: ClientState) =
        state.Lanes.Slots
        |> Seq.map snd
        |> Seq.choose OrderContextState.context
        |> Seq.toList


    /// Returns the nutrition slot views in the order the slots were started.
    let slots (state: ClientState) =
        let adding = state.Lanes.OrderPlan |> OrderPlanState.adding

        state.Lanes.Slots
        |> List.map (fun (id, slot) ->
            let view = slot |> OrderContextState.view

            let prescribes =
                match view, slot |> OrderContextState.linkedToPrescribe with
                | OrderContextView.Settled ctx, Some _ ->
                    ctx |> Models.OrderContext.contribution |> Option.map _.Order.Id
                | _ -> None

            {
                Id = id
                Context = view
                Offers = slot |> OrderContextState.linkedOffers
                PrescribeOrderId = prescribes
                Prescribing = adding = Some id
            }
        )


    /// Returns the totals of the nutrition page: the combined totals of the plan and the slots when loaded,
    /// otherwise the plan totals.
    let nutritionTotals (state: ClientState) =
        match state.Loader.Totals with
        | Resolved totals
        | Refreshing totals -> Some totals
        | HasNotStartedYet
        | InProgress -> state.Lanes.OrderPlan |> OrderPlanState.plan |> Option.map _.Totals


    /// Whether leaving the page would lose work.
    let unsignedWork (state: ClientState) =
        UnsignedWorkPolicy.hasUnsignedWork
            (state.Lanes.OrderContext |> OrderContextState.context)
            (slotContexts state)
            (state.Lanes.Signing |> SigningState.view)
            (state.Lanes.OrderPlan |> OrderPlanState.work)


    /// Whether the patient was no longer active in the EHR at the signing challenge.
    let ehrMovedOn (state: ClientState) = state.Lanes.Signing |> SigningState.ehrMovedOn


    /// Returns the identity of the patient being signed for; None when the EHR did not identify the patient.
    let signedFor (state: ClientState) =
        SessionState.view state.Lanes.Session |> SessionPatientPolicy.identity


    /// Whether an edit on the panel counts as another patient, so the prescriber is asked first.
    let asksNewPatient (state: ClientState) =
        NewPatientPolicy.asks
            (SessionState.view state.Lanes.Session)
            (state.Lanes.OrderPlan |> OrderPlanState.plan)
            (state.Lanes.OrderContext |> OrderContextState.context)
            (slotContexts state)


    /// Lists every loader and admin load with its load state, except the server check.
    let loads (state: ClientState) =
        LoaderState.readings state.Loader @ AdminState.readings state.Admin


    /// Returns the requests in progress in the lanes and the loads.
    let busy (state: ClientState) =
        Lanes.out state.Shell.Counting state.Lanes (state |> loads |> outOf)


    /// Whether any request is in progress.
    let busyAny (state: ClientState) = state |> busy |> BusyPolicy.any


    /// Whether a request that changes this page is in progress.
    let busyPage page (state: ClientState) = state |> busy |> BusyPolicy.page page


    /// Returns the start-up status; once started, it stays started.
    let startup (state: ClientState) =
        if state.Shell.Started then
            StartupPolicy.Startup.Started
        else
            StartupPolicy.status (busy state) (state |> loads |> loadedOf) state.Loader.Failed


    /// Returns the state a url change is checked against.
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


    /// The medication names the interactions can be checked for.
    let drugNames (state: ClientState) = state.Loader.DrugNames


    /// The medication of the emergency list.
    let bolusMedication (state: ClientState) = state.Loader.BolusMedication


    /// The continuous medication list.
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


    /// The linked orders the link rules offer with the workbench order, each turned on or off.
    let workbenchOffers (state: ClientState) = state.Lanes.OrderContext |> OrderContextState.linkedOffers


    /// Whether the workbench order can be prescribed with its linked orders; false while a linked order turned
    /// on is not narrowed to one scenario.
    let workbenchPrescribable (state: ClientState) =
        state.Lanes.OrderContext |> OrderContextState.linkedToPrescribe |> Option.isSome


    /// The workbench dialog, if one is open.
    let orderContextDialog (state: ClientState) = state.Lanes.OrderContext |> OrderContextState.dialog


    /// The order plan as the page shows it.
    let orderPlan (state: ClientState) = state.Lanes.OrderPlan |> OrderPlanState.view


    /// The patient data the panel shows.
    let draft (state: ClientState) = state.Lanes.Patient |> PatientState.draft


    /// Whether a patient data update is in progress.
    let changing (state: ClientState) = state.Lanes.Patient |> PatientState.changing


    /// Whether the orders were calculated for other patient data than the panel shows, after a failed update.
    let planBehind (state: ClientState) = OutPolicy.planBehind state.Lanes.Patient state.Lanes.OrderPlan


    /// Returns the patient data from the server answer, with its estimates; otherwise the draft, even an
    /// incomplete one, so the summary shows what was entered and what is missing.
    let currentPatient (state: ClientState) =
        state.Lanes.Patient |> PatientState.answered |> Option.orElse (draft state)


    /// The Session as the page shows it.
    let session (state: ClientState) = state.Lanes.Session |> SessionState.view


    /// The OpenedToken of the Session, sent with every calculation request; None without an open Session.
    let opened (state: ClientState) = state.Lanes.Session |> SessionState.token


    /// A newer signed order plan than the open one, if the Session has one.
    let newerPlan (state: ClientState) = state.Lanes.Session |> SessionState.newerPlan


    /// The signing as the page shows it.
    let signing (state: ClientState) = state.Lanes.Signing |> SigningState.view


    /// The differences the signature is requested for.
    let differences (state: ClientState) = state.Lanes.Signing |> SigningState.differences


    /// The order contexts of the plan that are new, changed or removed since the version last opened or
    /// signed.
    let planDifferences (state: ClientState) plan = state.Lanes.OrderPlan |> OrderPlanState.differences plan


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


    /// Whether a quantity field is counting step clicks.
    let counting (state: ClientState) = state.Shell.Counting


    /// Whether the server serves the demo data.
    let isDemo (state: ClientState) = state.Shell.IsDemo


    /// The filter of the emergency list.
    let emergencyListFilter (state: ClientState) = state.Shell.EmergencyListFilter


    /// The filter of the continuous medication list.
    let continuousMedsFilter (state: ClientState) = state.Shell.ContinuousMedsFilter


    /// Whether the disclaimer shows: only when the shell asks for it and the Session is anonymous.
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


    /// Returns the filter seed for an emergency list item; None when the list is not loaded or lacks the item.
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


    /// Returns the filter seed for a continuous medication list item, with the intravenous route; None when the
    /// list is not loaded or lacks the item.
    let continuousSeed (item: string) (meds: Deferred<ContinuousMedication list>) =
        match meds with
        | Resolved meds ->
            meds
            |> List.tryFind (fun m -> item.EndsWith($".%s{m.Medication}"))
            |> Option.map (fun m -> FilterSeed.ofListItem m.Generic m.Indication "INTRAVENEUS" m.DoseType)
        | _ -> None


    /// Seeds the workbench filter when there is patient data; otherwise raises an alert, because the patient
    /// is part of the filter.
    let seedOrAlert newId seed state =
        match patient state with
        | Some _ ->
            [
                ClientMsg.Lanes(LanesMsg.Workbench(OrderContextMsg.SeedFilter(seed, newId ())))
            ]
        | None -> [ ClientMsg.Shell(ShellMsg.AlertRaised Alert.Alert.NoPatientForMedication) ]


    /// Handles a chosen list item: with patient data, moves to the Prescribe page and seeds the workbench;
    /// without patient data, raises an alert. Does nothing when the seed is None.
    let chosen newId seed state =
        match seed with
        | Some seed ->
            let page =
                patient state
                |> Option.map (fun _ -> ClientMsg.Shell(ShellMsg.MovedToPage Page.Page.Prescribe))
                |> Option.toList

            page @ seedOrAlert newId seed state
        // The list does not have the item.
        | None -> []


    /// Runs one message: a step of a part, or follow-up messages for the parts. A client message without
    /// follow-up messages is recorded as dropped.
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
        // Queries the totals again when a lanes step changed the totals query.
        | ClientMsg.Lanes m ->
            let lanes, effects, taken = Lanes.transition newId m state.Lanes
            let next = { state with Lanes = lanes }
            let query = totalsQuery next

            {
                State = next
                Emitted = effects |> List.map ClientEffect.Lanes
                Taken = taken |> List.map ClientStep.Lanes
                Follow =
                    [
                        if query <> totalsQuery state then
                            ClientMsg.Loader(LoaderMsg.QueryTotals query)
                    ]
            }
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
        | ClientMsg.PageChosen page ->
            becomes [ ClientMsg.Shell(ShellMsg.PageChosen(page, state.Admin.IsAuthenticated)) ]
        | ClientMsg.PanelChanged(pat, estimates, request) ->
            becomes
                [
                    ClientMsg.Lanes(LanesMsg.Patient(PatientMsg.Changed(pat, estimates, request)))
                ]
        // Ignored while a launch can identify the patient, because the orders of that patient are never
        // cleared.
        | ClientMsg.NewPatient _ when not (NewPatientPolicy.startsNewPatient (SessionState.view state.Lanes.Session)) ->
            becomes []
        | ClientMsg.NewPatient(pat, estimates, request) ->
            becomes
                [
                    ClientMsg.Loader LoaderMsg.PatientExpected
                    // A failed patient call gives no plan, so the interaction warnings are cleared here.
                    ClientMsg.Loader(LoaderMsg.CheckInteractions [])
                    ClientMsg.Lanes(LanesMsg.Patient(PatientMsg.Changed(None, estimates, request)))
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
        // The differences are taken now, so a plan change during signing does not alter what is signed.
        | ClientMsg.Sign(plan, request) ->
            let differences = planDifferences state plan
            becomes
                [
                    ClientMsg.Lanes(LanesMsg.Signing(SigningMsg.Sign(plan, differences, request)))
                ]
        | ClientMsg.TakeEhrData -> becomes [ ClientMsg.Lanes(LanesMsg.Signing SigningMsg.TakeEhrData) ]


    /// Routes an effect to messages for other parts, using the current state; no messages for an effect that
    /// only leaves the client.
    let route newId effect (state: ClientState) =
        let alert a = [ ClientMsg.Shell(ShellMsg.AlertRaised a) ]
        let failed source errs = [ ClientMsg.Shell(ShellMsg.ServerErrorRaised(source, errs)) ]
        let succeeded source = [ ClientMsg.Shell(ShellMsg.ServerErrorCleared source) ]
        let first errs = errs |> Array.tryHead

        match effect with
        // Lanes effects: alerts, the plan error banner, the pages and the interactions.
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
        // A slot error shows the same alert as a workbench error. A slot SyncPages is not routed, because the
        // formulary and parenteralia pages follow only the workbench.
        | ClientEffect.Lanes(LanesEffect.Workbench(OrderContextEffect.TellError errs))
        | ClientEffect.Lanes(LanesEffect.Slot(_, OrderContextEffect.TellError errs)) ->
            alert (Alert.Alert.WorkbenchFailed(first errs))
        | ClientEffect.Lanes(LanesEffect.Workbench(OrderContextEffect.TellNotApplied refusal))
        | ClientEffect.Lanes(LanesEffect.Slot(_, OrderContextEffect.TellNotApplied refusal)) ->
            alert (Alert.Alert.ChangeNotApplied refusal)
        | ClientEffect.Lanes(LanesEffect.Plan(OrderPlanEffect.CheckInteractions drugs)) ->
            [ ClientMsg.Loader(LoaderMsg.CheckInteractions drugs) ]
        | ClientEffect.Lanes(LanesEffect.Plan(OrderPlanEffect.TellError errs)) ->
            failed ServerErrorPolicy.ErrorSource.OrderPlan errs
        | ClientEffect.Lanes(LanesEffect.Plan OrderPlanEffect.TellAnswered) ->
            succeeded ServerErrorPolicy.ErrorSource.OrderPlan
        | ClientEffect.Lanes(LanesEffect.Plan(OrderPlanEffect.TellPatientDataChanged summary)) ->
            alert (Alert.Alert.PatientDataChanged summary)
        | ClientEffect.Lanes LanesEffect.PatientExpected -> [ ClientMsg.Loader LoaderMsg.PatientExpected ]
        | ClientEffect.Lanes LanesEffect.GoToPlanPage -> [ ClientMsg.Shell(ShellMsg.MovedToPage Page.Page.OrderPlan) ]

        // Loader effects: the seed goes to the workbench, the settings and alerts to the shell, the notice to
        // the Session.
        | ClientEffect.Loader(LoaderEffect.SeedWorkbench seed) ->
            [
                ClientMsg.Lanes(LanesMsg.Workbench(OrderContextMsg.SeedFilter(seed, newId ())))
            ]
        | ClientEffect.Loader(LoaderEffect.SettingsLanded settings) ->
            [ ClientMsg.Shell(ShellMsg.SettingsLanded settings) ]
        | ClientEffect.Loader(LoaderEffect.Alert a) -> alert a
        | ClientEffect.Loader LoaderEffect.WithdrawInteractionsFound ->
            [ ClientMsg.Shell ShellMsg.WithdrawInteractionsFound ]
        | ClientEffect.Loader(LoaderEffect.RequestFailed(source, errs)) -> failed source errs
        | ClientEffect.Loader(LoaderEffect.RequestSucceeded source) -> succeeded source
        | ClientEffect.Loader(LoaderEffect.NoticeReceived(from, notice)) ->
            [ ClientMsg.Lanes(LanesMsg.Session(SessionMsg.NoticeReceived(from, notice))) ]

        // Admin effects: the reload goes to the loader, the logout and alerts to the shell.
        | ClientEffect.Admin AdminEffect.ReloadDone -> [ ClientMsg.Loader LoaderMsg.ResourcesReloaded ]
        | ClientEffect.Admin AdminEffect.LoggedOut -> [ ClientMsg.Shell ShellMsg.LoggedOut ]
        | ClientEffect.Admin(AdminEffect.Alert a) -> alert a
        | ClientEffect.Admin(AdminEffect.RequestFailed(source, errs)) -> failed source errs
        | ClientEffect.Admin(AdminEffect.RequestSucceeded source) -> succeeded source

        // Shell effects: the page goes to the loader, the url results to the lanes and the Session.
        | ClientEffect.Shell(ShellEffect.PageShown page) -> [ ClientMsg.Loader(LoaderMsg.PageShown page) ]
        // An empty interaction check ends any check for the old plan, so its answer is ignored.
        | ClientEffect.Shell(ShellEffect.StartOver pat) ->
            [
                ClientMsg.Loader(LoaderMsg.CheckInteractions [])
                ClientMsg.Lanes(LanesMsg.StartOver pat)
            ]
        // An open launched Session supplies the patient, so the patient from the url is ignored.
        | ClientEffect.Shell(ShellEffect.PatientFromUrl _) when launched state -> []
        | ClientEffect.Shell(ShellEffect.PatientFromUrl pat) ->
            [
                ClientMsg.Lanes(
                    LanesMsg.Patient(PatientMsg.Changed(pat, PatientDraftPolicy.Estimates.Renewed, newId ()))
                )
            ]
        | ClientEffect.Shell(ShellEffect.SeedWorkbench seed) -> state |> seedOrAlert newId seed
        | ClientEffect.Shell(ShellEffect.LaunchRefused refusal) ->
            [ ClientMsg.Lanes(LanesMsg.Session(SessionMsg.LaunchRefused refusal)) ]
        | ClientEffect.Shell ShellEffect.ResumeSession -> [ ClientMsg.Lanes(LanesMsg.Session SessionMsg.Resume) ]
        | ClientEffect.Shell ShellEffect.LeaveSession -> [ ClientMsg.Lanes(LanesMsg.Session SessionMsg.UrlMovedOn) ]
        | _ -> []


    /// An item in the run queue: a message to run or an effect to route.
    [<RequireQualifiedAccess>]
    type Queued =
        | Run of ClientMsg
        | Pass of ClientEffect


    /// Returns the next state, the effects and the steps for these messages. The effects of a message are
    /// routed one by one with the current state, and the resulting messages run before the rest of the queue.
    /// Routes go in one direction only, so the queue ends. The start-up is marked as ended in the first
    /// transition after which it is started.
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


    /// Returns the next state, the effects and the steps for a message.
    let transition newId msg state = state |> run newId [ msg ]


    /// Runs the page load: applies the url, checks the server and starts the start-up loads.
    let pageLoad newId sl (url: Url.UrlParts) state =
        state
        |> run
            newId
            [
                ClientMsg.Shell(ShellMsg.PageLoaded(sl, url))
                ClientMsg.Loader LoaderMsg.CheckServer
                for load in
                    [
                        Load.Settings
                        Load.BolusMedication
                        Load.ContinuousMedication
                        Load.Products
                        Load.Localization
                        Load.Formulary
                        Load.Parenteralia
                        Load.DrugNames
                    ] do
                    ClientMsg.Loader(LoaderMsg.Start load)
            ]
