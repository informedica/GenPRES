namespace Informedica.GenPRES.Client.Core.StateMachines

/// The admin login on the settings page and what it fetches under the token it bought: the log
/// listing, the analysis of a log file and the resource reload, each a reading. A login answer of
/// an earlier attempt is dropped, and a token the server no longer takes logs out.
module AdminMachine =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open Loads


    /// The login, its token and the three readings.
    type AdminState =
        {
            /// Whether the server took the password.
            IsAuthenticated: bool
            /// The token the login bought; empty without a login.
            AuthToken: string
            /// Counts logins and logouts, so that a login answer of an earlier attempt is dropped.
            LoginAttempt: int
            /// The log files on the server.
            LogFiles: Deferred<LogFileInfo[]>
            /// The analysis of one log file.
            LogAnalysisReport: Deferred<string>
            /// The resource reload, out from the request until the server has reloaded.
            Reloading: Deferred<unit>
        }


    /// What an admin call brought.
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
        /// The log files asked for.
        | ListLogFiles
        /// The analysis of this log file asked for.
        | AnalyzeLogFile of fileName: string
        /// The resources asked to be reloaded.
        | ReloadResources
        /// An admin call's answer.
        | Landed of Landing


    /// What the App carries out: a call, whose answer comes back as a message, or what the user and the
    /// loads are told.
    [<RequireQualifiedAccess>]
    type AdminEffect =
        /// Ask the server whether it takes this password, for this attempt.
        | ValidatePassword of attempt: int * password: string
        /// Ask for the log files, under the token.
        | FetchLogFiles of token: string
        /// Ask for the analysis of this log file, under the token.
        | FetchLogAnalysis of token: string * fileName: string
        /// Ask the server to reload its resources, under the token.
        | Reload of token: string
        /// The server has reloaded its resources.
        | ReloadDone
        /// The login is over: by the user, or by a token the server no longer takes.
        | LoggedOut
        /// Show the alert on the snackbar.
        | Alert of Alert.Alert
        /// A request of this source failed with these errors.
        | RequestFailed of ServerErrorPolicy.ErrorSource * string[]
        /// A request of this source succeeded.
        | RequestSucceeded of ServerErrorPolicy.ErrorSource


    /// Reading the admin machine's state.
    module AdminState =

        /// Not logged in, nothing asked for.
        let initial =
            {
                IsAuthenticated = false
                AuthToken = ""
                LoginAttempt = 0
                LogFiles = HasNotStartedYet
                LogAnalysisReport = HasNotStartedYet
                Reloading = HasNotStartedYet
            }


        /// Each load with its reading, the value left out.
        let readings (state: AdminState) =
            let reading deferred = deferred |> Deferred.map ignore

            [
                Load.LogFiles, reading state.LogFiles
                Load.LogAnalysis, reading state.LogAnalysisReport
                Load.Reload, reading state.Reloading
            ]


    /// The admin loads out.
    let out state = state |> AdminState.readings |> outOf


    /// The login over and its readings gone; a later login answer of the attempt before is dropped.
    let loggedOut (state: AdminState) =
        { AdminState.initial with LoginAttempt = state.LoginAttempt + 1 }, [ AdminEffect.LoggedOut ]


    /// What an answer sets, whichever call it answers.
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


    /// An answer of this source: on success the error cleared and what the answer sets; on failure the
    /// reading reset, the error raised and, for a token the server no longer takes, the login over.
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


    /// The next state and the effects for a message.
    let transition msg (state: AdminState) =
        match msg with
        | AdminMsg.Login password ->
            let attempt = state.LoginAttempt + 1
            { state with LoginAttempt = attempt }, [ AdminEffect.ValidatePassword(attempt, password) ]
        | AdminMsg.Logout -> state |> loggedOut
        // one listing at a time, so an earlier answer cannot clear the error of a later one; the table
        // shown stays until the answer
        | AdminMsg.ListLogFiles when isOut state.LogFiles -> state, []
        | AdminMsg.ListLogFiles ->
            { state with LogFiles = state.LogFiles |> Deferred.refresh }, [ AdminEffect.FetchLogFiles state.AuthToken ]
        | AdminMsg.AnalyzeLogFile fileName ->
            { state with LogAnalysisReport = InProgress }, [ AdminEffect.FetchLogAnalysis(state.AuthToken, fileName) ]
        | AdminMsg.ReloadResources -> { state with Reloading = InProgress }, [ AdminEffect.Reload state.AuthToken ]

        // an answer of an earlier attempt: a login since logged out, or asked again
        | AdminMsg.Landed(Landing.Login(attempt, _)) when attempt <> state.LoginAttempt -> state, []
        | AdminMsg.Landed(Landing.Login(_, Ok response)) ->
            let state, effects = state |> answered response
            state, AdminEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Login :: effects
        // a failed login drops the token, but the page and the readings stay
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
