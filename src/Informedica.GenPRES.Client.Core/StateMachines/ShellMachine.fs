namespace Informedica.GenPRES.Client.Core.StateMachines

/// Tracks the shell around the pages: the url, the page shown, the language, the hospital, the
/// disclaimer, the snackbar, the error banner, the filters of the two lists, whether a quantity field
/// counts and whether the start-up has ended. The settings page needs a login and is left on a logout.
/// A language or hospital the user chooses shows the disclaimer again. A url with a patient, a medication or a
/// launch starts the lanes over, after a question when it would leave a launched session or unsigned
/// work.
module ShellMachine =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open Informedica.GenPRES.Shared.Localization
    open Page
    open Loads
    open FilterSeed


    /// Everything the shell holds.
    type ShellState =
        {
            /// The url the app shows, and the newer url a question waits on.
            Url: UrlPolicy.UrlState
            /// The page shown.
            Page: Page
            /// Whether the disclaimer shows.
            ShowDisclaimer: bool
            /// The language shown, and whether the url or the user chose it.
            Language: LanguagePolicy.Language
            /// The hospital chosen, whose lists show and that is the Location of the patient; None when no
            /// hospital is chosen.
            Hospital: string option
            /// Whether the server serves the demo data, shown in the title.
            IsDemo: bool
            /// The error on the banner, with the kind of request that raised it.
            ServerError: ServerErrorPolicy.ServerError option
            /// The alert on the snackbar; None while it is closed.
            Snackbar: Alert.Alert option
            /// The filter of the emergency list.
            EmergencyListFilter: string[]
            /// The filter of the continuous medication list.
            ContinuousMedsFilter: string[]
            /// Whether a quantity field counts step clicks, from the first click until it sends them.
            Counting: bool
            /// Whether the start-up has ended: no request was under way and every load the application
            /// needs had loaded. It never turns false again, because a later request would read as a new
            /// start-up and the gate would cover the application.
            Started: bool
        }


    /// The state of the other parts that a url change is decided against, read when the change comes.
    type UrlCheck =
        {
            /// Whether a signature is under way.
            SigningUnderWay: bool
            /// The requests under way.
            Out: Request list
            /// Whether there is work not signed.
            UnsignedWork: bool
            /// Whether a launched Session is open.
            Launched: bool
        }


    /// A message to the shell.
    [<RequireQualifiedAccess>]
    type ShellMsg =
        /// The user chose a page; whether the user is logged in decides the settings page.
        | PageChosen of Page * loggedIn: bool
        /// Another part shows a page.
        | MovedToPage of Page
        /// The admin login is over.
        | LoggedOut
        /// The user chose a language.
        | LanguageChosen of Locales
        /// The user chose a hospital.
        | HospitalChosen of string
        /// A patient answer names this hospital.
        | PatientHospitalLanded of string
        /// The user accepted the disclaimer.
        | DisclaimerAccepted
        /// The server settings landed: the demo flag, the default language and the default hospital.
        | SettingsLanded of ServerSettings
        /// The url of the page load, as segments and as read.
        | PageLoaded of string list * Url.UrlParts
        /// The url changed in the address bar, as segments and as read.
        | UrlChanged of string list * Url.UrlParts * UrlCheck
        /// Yes to the question: leave for the url it asked about.
        | LeftForUrl
        /// No to the question: the url the app shows is put back.
        | UrlKept
        /// Show the alert on the snackbar.
        | AlertRaised of Alert.Alert
        /// Close the snackbar if it shows the interactions found.
        | WithdrawInteractionsFound
        /// The user closed the snackbar.
        | SnackbarClosed
        /// A request of this source failed with these errors.
        | ServerErrorRaised of ServerErrorPolicy.ErrorSource * string[]
        /// A request of this source succeeded.
        | ServerErrorCleared of ServerErrorPolicy.ErrorSource
        /// The user closed the error banner.
        | ServerErrorDismissed
        /// Whether a quantity field counts.
        | CountingChanged of bool
        /// The start-up has ended.
        | StartupEnded
        /// The user filtered the emergency list.
        | EmergencyListFiltered of string[]
        /// The user filtered the continuous medication list.
        | ContinuousMedsFiltered of string[]
        /// Both list filters cleared, for a new patient.
        | ListFiltersCleared


    /// What the App carries out for the shell.
    [<RequireQualifiedAccess>]
    type ShellEffect =
        /// The user chose this page and it shows; the loads follow it.
        | PageShown of Page
        /// Put these segments, the url the app shows, back in the address bar.
        | PutBackUrl of string list
        /// The user chose this hospital and it shows; the patient follows it.
        | HospitalShown of string
        /// Replace the launch url in the address bar and the history, so its token survives neither a
        /// reload, the back button nor a copied url.
        | EraseLaunch
        /// Present the launch to the server.
        | PresentLaunch of Launch
        /// The return from the identity provider refused the launch.
        | LaunchRefused of LaunchRefusal
        /// Start the patient, the workbench, the plan, its interactions and the signing over with these
        /// patient data, and leave the session.
        | StartOver of PatientData option
        /// The patient data of the url, for the patient panel unless a session provides the patient data.
        | PatientFromUrl of PatientData option
        /// Seed the workbench with the medication of the url, once there is a patient.
        | SeedWorkbench of FilterSeed
        /// Resume the session from the cookie.
        | ResumeSession
        /// Leave the session the cookie may hold.
        | LeaveSession


    /// The initial shell state.
    module ShellState =

        /// The shell before the page load applies its url.
        let initial =
            {
                Url = UrlPolicy.UrlState.Shown []
                Page = Page.Page.LifeSupport
                ShowDisclaimer = true
                // the server default replaces this once the settings land, unless the url chose
                Language = LanguagePolicy.Language.initial None
                // the hospital a patient answer names replaces this, and the default hospital of the server once
                // the settings land and no hospital is set
                Hospital = None
                IsDemo = false
                ServerError = None
                Snackbar = None
                EmergencyListFilter = [||]
                ContinuousMedsFilter = [||]
                Counting = false
                Started = false
            }


    /// Applies the page, the language and the disclaimer of a url, and stores the url as the one the app
    /// shows. Only a language in the url changes the language; otherwise the current language stays.
    let pageApplied sl (url: Url.UrlParts) state =
        { state with
            Url = UrlPolicy.UrlState.Shown sl
            Page = url.Page |> Option.defaultValue Page.Page.LifeSupport
            ShowDisclaimer = url.Disclaimer
            Language = state.Language |> LanguagePolicy.Language.onUrl url.Language
        }


    /// Applies a url in full: its page, its patient, its medication and its launch. A launch url is
    /// stored as "#/session", the url it is erased to.
    let urlApplied sl (url: Url.UrlParts) state =
        let sl = if url.Launch.IsSome then [ "session" ] else sl

        state |> pageApplied sl url,
        [
            ShellEffect.PatientFromUrl url.Patient
            match url.Medication with
            | Some m ->
                ShellEffect.SeedWorkbench
                    {
                        Source = SeedSource.Url
                        Indication = m.indication
                        Generic = m.medication
                        Route = m.route
                        Form = m.form
                        DoseType = m.dosetype
                    }
            | None -> ()
            match url.Launch with
            | Some(Url.LaunchUrl.Launch launch) -> ShellEffect.PresentLaunch launch
            | Some(Url.LaunchUrl.Refused refusal) -> ShellEffect.LaunchRefused refusal
            | None -> ()
        ]


    /// Starts the lanes over on a url and applies the url in full.
    let startedOver sl (url: Url.UrlParts) state =
        let state, effects = state |> urlApplied sl url
        state, ShellEffect.StartOver url.Patient :: effects


    /// Decides a url change: a refused launch is applied in full, anything else as the url policy says.
    /// A launch url that is put back is lost, because the back button does not bring it again as it
    /// does a patient url, so the user is told to open the patient again from the EHR.
    let urlChanged sl (url: Url.UrlParts) (check: UrlCheck) state =
        match url.Launch with
        | Some(Url.LaunchUrl.Refused _) -> state |> urlApplied sl url
        | launch ->
            let change = UrlPolicy.change state.Url sl (UrlPolicy.seeds url)
            let back = UrlPolicy.UrlState.shown state.Url

            match
                UrlPolicy.action
                    change
                    check.SigningUnderWay
                    (BusyPolicy.any check.Out)
                    check.UnsignedWork
                    check.Launched
            with
            | UrlPolicy.UrlAction.ApplyPage -> state |> pageApplied sl url, []
            | UrlPolicy.UrlAction.Ignore -> state, []
            | UrlPolicy.UrlAction.PutBack when launch.IsSome ->
                { state with Snackbar = Some Alert.Alert.LaunchNotOpened }, [ ShellEffect.PutBackUrl back ]
            | UrlPolicy.UrlAction.PutBack -> state, [ ShellEffect.PutBackUrl back ]
            | UrlPolicy.UrlAction.Ask -> { state with Url = state.Url |> UrlPolicy.UrlState.ask sl url }, []
            | UrlPolicy.UrlAction.StartOver -> state |> startedOver sl url


    /// Returns the hospital once the server settings land: the hospital already set, else the default hospital of
    /// the server; None when there is neither.
    let hospitalOnServerDefault settings hospital = hospital |> Option.orElse settings.DefaultLocation


    /// The next state and the effects for a message.
    let transition msg (state: ShellState) =
        match msg with
        // the settings page is behind the login
        | ShellMsg.PageChosen(Page.Page.Settings, false) -> state, []
        | ShellMsg.PageChosen(page, _) -> { state with Page = page }, [ ShellEffect.PageShown page ]
        | ShellMsg.MovedToPage page -> { state with Page = page }, []
        | ShellMsg.LoggedOut when state.Page = Page.Page.Settings -> { state with Page = Page.Page.LifeSupport }, []
        | ShellMsg.LanguageChosen l ->
            { state with
                Language = state.Language |> LanguagePolicy.Language.choose l
                ShowDisclaimer = true
            },
            []
        | ShellMsg.HospitalChosen hospital ->
            { state with
                Hospital = Some hospital
                ShowDisclaimer = true
            },
            [ ShellEffect.HospitalShown hospital ]
        // the user chose nothing, so the disclaimer stays as it is
        | ShellMsg.PatientHospitalLanded hospital -> { state with Hospital = Some hospital }, []
        | ShellMsg.DisclaimerAccepted -> { state with ShowDisclaimer = false }, []
        | ShellMsg.SettingsLanded settings ->
            { state with
                IsDemo = settings.IsDemo
                Language = state.Language |> LanguagePolicy.Language.onServerDefault settings.Language
                Hospital = state.Hospital |> hospitalOnServerDefault settings
            },
            []
        // a page load with a patient or a medication leaves the session the cookie may hold, one
        // without them resumes the session from the cookie; a launch is presented when the url is applied
        | ShellMsg.PageLoaded(sl, url) ->
            let state, effects = state |> urlApplied sl url

            state,
            [
                if url.Launch.IsSome then
                    ShellEffect.EraseLaunch
                match url.Launch with
                | None when UrlPolicy.seeds url -> ShellEffect.LeaveSession
                | None -> ShellEffect.ResumeSession
                | Some _ -> ()
                yield! effects
            ]
        // any url change closes an open question: the url it was about is no longer the one
        // in the address bar, so the change is decided against the url shown alone
        | ShellMsg.UrlChanged(sl, url, check) ->
            let state, effects =
                { state with Url = state.Url |> UrlPolicy.UrlState.close }
                |> urlChanged sl url check

            state,
            [
                if url.Launch.IsSome then
                    ShellEffect.EraseLaunch
                yield! effects
            ]
        | ShellMsg.LeftForUrl ->
            match state.Url with
            | UrlPolicy.UrlState.Asked(_, sl, url) -> state |> startedOver sl url
            | _ -> state, []
        | ShellMsg.UrlKept ->
            let state = { state with Url = state.Url |> UrlPolicy.UrlState.close }
            state, [ ShellEffect.PutBackUrl(UrlPolicy.UrlState.shown state.Url) ]
        | ShellMsg.AlertRaised alert -> { state with Snackbar = Some alert }, []
        // only the notice itself is withdrawn, never another alert the snackbar shows meanwhile
        | ShellMsg.WithdrawInteractionsFound ->
            match state.Snackbar with
            | Some(Alert.Alert.InteractionsFound _) -> { state with Snackbar = None }, []
            | _ -> state, []
        | ShellMsg.SnackbarClosed -> { state with Snackbar = None }, []
        // the server check runs again every few seconds while the server is down, so it raises the
        // banner alone and no snackbar
        | ShellMsg.ServerErrorRaised(ServerErrorPolicy.ErrorSource.Server, errs) ->
            { state with ServerError = Some(ServerErrorPolicy.raised ServerErrorPolicy.ErrorSource.Server errs) }, []
        | ShellMsg.ServerErrorRaised(source, errs) ->
            { state with
                ServerError = Some(ServerErrorPolicy.raised source errs)
                Snackbar = Some Alert.Alert.RequestFailed
            },
            []
        | ShellMsg.ServerErrorCleared source ->
            { state with ServerError = state.ServerError |> ServerErrorPolicy.clearedBy source }, []
        | ShellMsg.ServerErrorDismissed -> { state with ServerError = None }, []
        | ShellMsg.CountingChanged counting -> { state with Counting = counting }, []
        | ShellMsg.StartupEnded -> { state with Started = true }, []
        | ShellMsg.EmergencyListFiltered filter -> { state with EmergencyListFilter = filter }, []
        | ShellMsg.ContinuousMedsFiltered filter -> { state with ContinuousMedsFilter = filter }, []
        | ShellMsg.ListFiltersCleared ->
            { state with
                EmergencyListFilter = [||]
                ContinuousMedsFilter = [||]
            },
            []
        | _ -> state, []
