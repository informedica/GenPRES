/// The shell around the pages: the page shown, the language and the hospital, the disclaimer, the
/// snackbar, the error banner, the filters of the two lists, whether a quantity field counts and
/// whether the start-up has ended. The settings page is refused to a user not logged in, and left on
/// a logout. A language or hospital change shows the disclaimer again.
module ShellMachine

open Shared.Api
open Shared.Localization
open Page


/// Everything the shell holds.
type ShellState =
    {
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
    /// The page, the language and the disclaimer of a url.
    | UrlApplied of Url.UrlParts
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


/// Reading the shell's state.
module ShellState =

    /// The shell at a page load, over the page, the language and the disclaimer of its url.
    let initial (url: Url.UrlParts) =
        {
            Page = url.Page |> Option.defaultValue Page.Page.LifeSupport
            ShowDisclaimer = url.Disclaimer
            // the server default replaces this once the settings land, unless the url chose
            Language = LanguagePolicy.Language.initial url.Language
            Hospital = "UMCU"
            IsDemo = false
            ServerError = None
            Snackbar = None
            EmergencyListFilter = [||]
            ContinuousMedsFilter = [||]
            Counting = false
            Started = false
        }


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
    // only a language in the url changes it; a navigation keeps the current one
    | ShellMsg.UrlApplied url ->
        { state with
            Page = url.Page |> Option.defaultValue Page.Page.LifeSupport
            ShowDisclaimer = url.Disclaimer
            Language = state.Language |> LanguagePolicy.Language.onUrl url.Language
        },
        []
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
