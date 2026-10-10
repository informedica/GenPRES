/// The shell around the pages: the url, the page shown, the language and the hospital, the
/// disclaimer, the snackbar, the error banner, the filters of the two lists, whether a quantity field
/// counts and whether the start-up has ended. The settings page is refused to a user not logged in,
/// and left on a logout. A language or hospital change shows the disclaimer again. A url with a
/// patient, a medication or a launch starts the lanes over, after a question when it would leave a
/// launched Session or work not signed.
module ShellMachine

open Shared
open Shared.Types
open Shared.Api
open Shared.Localization
open Page
open OrderContextMachine


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
        /// The hospital whose lists show.
        Hospital: string
        /// Whether the server serves the demo data, shown in the title.
        IsDemo: bool
        /// The error on the banner, with the kind of request that raised it.
        ServerError: ServerErrorPolicy.ServerError option
        /// The alert on the snackbar; none while it is closed.
        Snackbar: Alert.Alert option
        /// The filter of the emergency list.
        EmergencyListFilter: string[]
        /// The filter of the continuous medication list.
        ContinuousMedsFilter: string[]
        /// Whether a quantity field counts step clicks, from the first click until it sends them.
        Counting: bool
        /// Whether the start-up has ended: nothing was out and every load the application cannot be
        /// used without had loaded, once. It never goes back, since any later request out would read
        /// as starting again and the gate would cover the application.
        Started: bool
    }


/// What a url change is decided against, read from the other parts when it comes.
type UrlCheck =
    {
        /// Whether a signature is under way.
        SigningUnderWay: bool
        /// The requests out.
        Out: Busy.Request list
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
    /// The user accepted the disclaimer.
    | DisclaimerAccepted
    /// The server settings landed: the demo flag and the default language.
    | SettingsLanded of ServerSettings
    /// The url of the page load, as segments and as read.
    | PageLoaded of string list * Url.UrlParts
    /// The url changed in the address bar, as segments and as read.
    | UrlChanged of string list * Url.UrlParts * UrlCheck
    /// Yes to the question: leave for the url it asked about, as segments and as read now.
    | LeftForUrl of string list * Url.UrlParts
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
    /// Replace the launch url in the address bar and the history, so its token survives neither a
    /// reload, the back button nor a copied url.
    | EraseLaunch
    /// Present the launch to the server.
    | PresentLaunch of Launch
    /// The return from the identity provider refused the launch.
    | LaunchRefused of LaunchRefusal
    /// Start the patient, the workbench, the plan, its interactions and the signing over, on this
    /// patient, and leave the Session.
    | StartOver of Patient option
    /// The patient of the url, for the patient panel unless a Session holds the patient.
    | PatientFromUrl of Patient option
    /// Seed the workbench with the medication of the url, once there is a patient.
    | SeedWorkbench of FilterSeed
    /// Resume the Session on the cookie.
    | ResumeSession
    /// Leave the Session the cookie may hold.
    | LeaveSession


/// Reading the shell's state.
module ShellState =

    /// The shell before the page load applies its url.
    let initial =
        {
            Url = UrlPolicy.UrlState.Shown []
            Page = Page.Page.LifeSupport
            ShowDisclaimer = true
            // the server default replaces this once the settings land, unless the url chose
            Language = LanguagePolicy.Language.initial None
            Hospital = "UMCU"
            IsDemo = false
            ServerError = None
            Snackbar = None
            EmergencyListFilter = [||]
            ContinuousMedsFilter = [||]
            Counting = false
            Started = false
        }


/// Whether a url carries a patient, a medication or a launch.
let seeds (url: Url.UrlParts) =
    url.Patient.IsSome
    || url.Medication.IsSome
    || (
        match url.Launch with
        | Some(Url.LaunchUrl.Launch _) -> true
        | _ -> false
    )


/// The page, the language and the disclaimer of a url, and the url kept as the one the app shows.
/// Only a language in the url changes it; a navigation keeps the current one.
let pageApplied sl (url: Url.UrlParts) state =
    { state with
        Url = UrlPolicy.UrlState.Shown sl
        Page = url.Page |> Option.defaultValue Page.Page.LifeSupport
        ShowDisclaimer = url.Disclaimer
        Language = state.Language |> LanguagePolicy.Language.onUrl url.Language
    }


/// A url applied in full: its page, its patient, its medication and its launch. A launch url shows
/// as "#/session", the url it is erased to.
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


/// The lanes started over on a url, and the url applied in full.
let startedOver sl (url: Url.UrlParts) state =
    let state, effects = state |> urlApplied sl url
    state, ShellEffect.StartOver url.Patient :: effects


/// The url change decided: a refused launch applied in full, anything else by the url policy. A
/// launch put back is gone, since Back does not bring it again as it does a patient url, so the user
/// is told to open the patient again from the EHR.
let urlChanged sl (url: Url.UrlParts) (check: UrlCheck) state =
    match url.Launch with
    | Some(Url.LaunchUrl.Refused _) -> state |> urlApplied sl url
    | launch ->
        let change = UrlPolicy.change state.Url sl (seeds url)
        let back = UrlPolicy.UrlState.shown state.Url

        match UrlPolicy.action change check.SigningUnderWay check.Out check.UnsignedWork check.Launched with
        | UrlPolicy.UrlAction.ApplyPage -> state |> pageApplied sl url, []
        | UrlPolicy.UrlAction.Ignore -> state, []
        | UrlPolicy.UrlAction.PutBack when launch.IsSome ->
            { state with Snackbar = Some Alert.Alert.LaunchNotOpened }, [ ShellEffect.PutBackUrl back ]
        | UrlPolicy.UrlAction.PutBack -> state, [ ShellEffect.PutBackUrl back ]
        | UrlPolicy.UrlAction.Ask -> { state with Url = state.Url |> UrlPolicy.UrlState.ask sl }, []
        | UrlPolicy.UrlAction.StartOver -> state |> startedOver sl url


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
            Hospital = hospital
            ShowDisclaimer = true
        },
        []
    | ShellMsg.DisclaimerAccepted -> { state with ShowDisclaimer = false }, []
    | ShellMsg.SettingsLanded settings ->
        { state with
            IsDemo = settings.IsDemo
            Language = state.Language |> LanguagePolicy.Language.onServerDefault settings.Language
        },
        []
    // a page load without a launch resumes on the cookie, one with a patient or a medication
    // leaves the Session the cookie may hold; a launch is presented with the url applied
    | ShellMsg.PageLoaded(sl, url) ->
        let state, effects = state |> urlApplied sl url

        state,
        [
            if url.Launch.IsSome then
                ShellEffect.EraseLaunch
            match url.Launch with
            | None when seeds url -> ShellEffect.LeaveSession
            | None -> ShellEffect.ResumeSession
            | Some _ -> ()
            yield! effects
        ]
    // a question still open goes with any url change: the url it was about is no longer the one
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
    | ShellMsg.LeftForUrl(sl, url) when UrlPolicy.UrlState.asked state.Url = Some sl -> state |> startedOver sl url
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
