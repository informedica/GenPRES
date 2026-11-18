namespace Informedica.GenPRES.Client.Core.StateMachines

/// Handles the admin login on the settings page and the calls that need its token: the log file list,
/// the analysis of a log file and the resource reload. A login answer to an earlier attempt is ignored,
/// and an invalid token logs the user out.
module AdminMachine =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open Loads


    /// The login state, the token and the state of the three admin loads.
    type AdminState =
        {
            /// Whether the server accepted the password.
            IsAuthenticated: bool
            /// The token from the login; empty when not logged in.
            AuthToken: string
            /// Counts logins and logouts, so a login answer to an earlier attempt is ignored.
            LoginAttempt: int
            /// The log files on the server.
            LogFiles: Deferred<LogFileInfo[]>
            /// The analysis of one log file.
            LogAnalysisReport: Deferred<string>
            /// The resource reload, in progress from the request until the server has reloaded.
            Reloading: Deferred<unit>
        }


    /// The answer to an admin call.
    [<RequireQualifiedAccess>]
    type Landing =
        /// The answer to a login, with the attempt it belongs to.
        | Login of attempt: int * Result<AdminResponse, string[]>
        /// The answer to the log listing.
        | LogFiles of Result<AdminResponse, string[]>
        /// The answer to the analysis of a log file.
        | LogAnalysis of Result<AdminResponse, string[]>
        /// The answer to the resource reload.
        | Reload of Result<AdminResponse, string[]>


    /// A message to the admin machine.
    [<RequireQualifiedAccess>]
    type AdminMsg =
        /// The user logs in with this password.
        | Login of password: string
        /// The user logs out.
        | Logout
        /// Requests the log files.
        | ListLogFiles
        /// Requests the analysis of this log file.
        | AnalyzeLogFile of fileName: string
        /// Requests a resource reload.
        | ReloadResources
        /// The answer to an admin call.
        | Landed of Landing


    /// An effect the App carries out: a server call whose answer returns as a message, or a notice to the
    /// user or the loader.
    [<RequireQualifiedAccess>]
    type AdminEffect =
        /// Asks the server to validate this password, for this attempt.
        | ValidatePassword of attempt: int * password: string
        /// Requests the log files with the token.
        | FetchLogFiles of token: string
        /// Requests the analysis of this log file with the token.
        | FetchLogAnalysis of token: string * fileName: string
        /// Asks the server to reload its resources with the token.
        | Reload of token: string
        /// The server has reloaded its resources.
        | ReloadDone
        /// The user is logged out, by a logout or by an invalid token.
        | LoggedOut
        /// Shows the alert on the snackbar.
        | Alert of Alert.Alert
        /// Reports that a request from this source failed with these errors.
        | RequestFailed of ServerErrorPolicy.ErrorSource * string[]
        /// Reports that a request from this source succeeded.
        | RequestSucceeded of ServerErrorPolicy.ErrorSource


    /// Functions on the admin state.
    module AdminState =

        /// The state with no login and no load started.
        let initial =
            {
                IsAuthenticated = false
                AuthToken = ""
                LoginAttempt = 0
                LogFiles = HasNotStartedYet
                LogAnalysisReport = HasNotStartedYet
                Reloading = HasNotStartedYet
            }


        /// Lists each load with its load state, without the loaded value.
        let readings (state: AdminState) =
            let reading deferred = deferred |> Deferred.map ignore

            [
                Load.LogFiles, reading state.LogFiles
                Load.LogAnalysis, reading state.LogAnalysisReport
                Load.Reload, reading state.Reloading
            ]


    /// Returns the admin loads that are in progress.
    let out state = state |> AdminState.readings |> outOf


    /// Logs out and clears all loads. The login attempt count goes up, so a late login answer is ignored.
    let loggedOut (state: AdminState) =
        { AdminState.initial with LoginAttempt = state.LoginAttempt + 1 }, [ AdminEffect.LoggedOut ]


    /// Applies an admin answer to the state, whichever call it answers. A rejected password shows an alert.
    let answered response (state: AdminState) =
        match response with
        | AdminResponse.PasswordValidated(true, token) ->
            { state with
                IsAuthenticated = true
                AuthToken = token
            },
            []
        | AdminResponse.PasswordValidated(false, _) ->
            { state with
                IsAuthenticated = false
                AuthToken = ""
            },
            [ AdminEffect.Alert Alert.Alert.InvalidPassword ]
        | AdminResponse.LogFilesListed files -> { state with LogFiles = Resolved files }, []
        | AdminResponse.LogFileAnalyzed report -> { state with LogAnalysisReport = Resolved report }, []
        | AdminResponse.ResourcesReloaded -> { state with Reloading = Resolved() }, [ AdminEffect.ReloadDone ]


    /// Applies the answer of a call from this source. On success, reports the success and applies the answer.
    /// On failure, reports the errors and resets the load; an invalid token logs the user out instead.
    let settle source reset result (state: AdminState) =
        match result with
        | Ok response ->
            let state, effects = state |> answered response
            state, AdminEffect.RequestSucceeded source :: effects
        | Error errs ->
            let failed = AdminEffect.RequestFailed(source, errs)

            if errs |> Array.contains "Invalid token" then
                let state, effects = state |> loggedOut
                state, failed :: effects
            else
                reset state, [ failed ]


    /// Returns the next state and the effects for a message.
    let transition msg (state: AdminState) =
        match msg with
        | AdminMsg.Login password ->
            let attempt = state.LoginAttempt + 1
            { state with LoginAttempt = attempt }, [ AdminEffect.ValidatePassword(attempt, password) ]
        | AdminMsg.Logout -> state |> loggedOut
        // Only one listing runs at a time, so an earlier answer cannot clear the error of a later one.
        // The table shown stays until the answer.
        | AdminMsg.ListLogFiles when isOut state.LogFiles -> state, []
        | AdminMsg.ListLogFiles ->
            { state with LogFiles = state.LogFiles |> Deferred.refresh }, [ AdminEffect.FetchLogFiles state.AuthToken ]
        | AdminMsg.AnalyzeLogFile fileName ->
            { state with LogAnalysisReport = InProgress }, [ AdminEffect.FetchLogAnalysis(state.AuthToken, fileName) ]
        | AdminMsg.ReloadResources -> { state with Reloading = InProgress }, [ AdminEffect.Reload state.AuthToken ]

        // An answer to an earlier attempt is ignored: the user logged out or logged in again since.
        | AdminMsg.Landed(Landing.Login(attempt, _)) when attempt <> state.LoginAttempt -> state, []
        | AdminMsg.Landed(Landing.Login(_, Ok response)) ->
            let state, effects = state |> answered response
            state, AdminEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Login :: effects
        // A failed login clears the token but keeps the loaded data.
        | AdminMsg.Landed(Landing.Login(_, Error errs)) ->
            { state with
                IsAuthenticated = false
                AuthToken = ""
            },
            [ AdminEffect.RequestFailed(ServerErrorPolicy.ErrorSource.Login, errs) ]
        | AdminMsg.Landed(Landing.LogFiles result) ->
            state
            |> settle ServerErrorPolicy.ErrorSource.LogFiles (fun s -> { s with LogFiles = HasNotStartedYet }) result
        | AdminMsg.Landed(Landing.LogAnalysis result) ->
            state
            |> settle
                ServerErrorPolicy.ErrorSource.LogAnalysis
                (fun s -> { s with LogAnalysisReport = HasNotStartedYet })
                result
        | AdminMsg.Landed(Landing.Reload result) ->
            state
            |> settle ServerErrorPolicy.ErrorSource.Reload (fun s -> { s with Reloading = HasNotStartedYet }) result
