namespace Informedica.GenPRES.Client.Core.StateMachines

/// Loads the data the client needs: the server settings, the localization, the medication lists, the products,
/// the formulary and parenteralia pages, the interactions and the nutrition totals.
/// A start while the same load runs does nothing. A failed load returns to not started; a failed required load
/// is added to the failed loads. The server is checked until it answers. The medication names are requested
/// again after a failure, up to three failures.
/// The formulary and parenteralia pages follow the workbench filter and the patient, and a choice on either
/// page seeds the workbench filter. Interaction checks, totals queries and the queries of both lists are
/// numbered, so an answer to an earlier request is ignored.
module LoaderMachine =

    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Shared.Api
    open Loads
    open Page
    open FilterSeed


    /// The state of each load and the required loads that failed.
    type LoaderState =
        {
            /// The server settings: the default language, the demo flag, the departments and the hospitals.
            Settings: Deferred<ServerSettings>
            /// The terms of every language, as rows of the localization sheet.
            Localization: Deferred<string[][]>
            /// The products.
            Products: Deferred<Product list>
            /// The medication names the interactions page offers.
            DrugNames: Deferred<string[]>
            /// The interactions found by the last check.
            Interactions: Deferred<DrugInteraction[]>
            /// The number of the latest interaction check. An answer to an earlier check is ignored, so it cannot
            /// replace the rows or clear the error of a later check.
            InteractionCheck: int
            /// The combined totals of the nutrition page for the orders last queried.
            Totals: Deferred<Totals>
            /// The number of the latest totals query. An answer to an earlier query is ignored.
            TotalsQueryNumber: int
            /// The rows of the emergency list for the patient data of the latest emergency list query.
            EmergencyList: Deferred<Intervention[]>
            /// The patient data of the latest emergency list query; None when the rows are not asked for.
            EmergencyListQuery: PatientData option
            /// The number of the latest emergency list query. An answer to an earlier query is ignored.
            EmergencyListQueryNumber: int
            /// The numbers of the checked rows of the emergency list, from 1. Every query clears them, because a
            /// number points to a row of the rows queried before.
            EmergencyListChecked: int[]
            /// The rows of the continuous medication list for the patient data of the latest continuous list query.
            ContinuousList: Deferred<Intervention[]>
            /// The patient data of the latest continuous list query; None when the rows are not asked for.
            ContinuousListQuery: PatientData option
            /// The number of the latest continuous list query. An answer to an earlier query is ignored.
            ContinuousListQueryNumber: int
            /// The formulary page.
            Formulary: Deferred<Formulary>
            /// A workbench filter that arrived while the formulary was loading; it is applied when the load ends.
            FormularyAskAgain: Filter option
            /// The parenteralia page.
            Parenteralia: Deferred<Parenteralia>
            /// A workbench filter that arrived while the parenteralia were loading; it is applied when the load
            /// ends.
            ParenteraliaAskAgain: Filter option
            /// True when a new patient is coming. Clearing the patient before it then requests no pages, because
            /// a late reply for no patient would replace the pages of the new patient.
            PatientExpected: bool
            /// The patient from the last server answer. The formulary is requested for this patient, and a page
            /// choice seeds the workbench only when a patient is present.
            Patient: PatientData option
            /// The number of failed medication name loads since the last success.
            DrugNameFailures: int
            /// Whether the server answered the last check.
            Server: Deferred<bool>
            /// The required loads that failed; the application cannot be used until they load.
            Failed: Load list
        }


    /// The result of a start-up load: the answer or an error text.
    [<RequireQualifiedAccess>]
    type Landing =
        | Settings of Result<ServerSettings, string>
        | Localization of Result<string[][], string>
        | Products of Result<Product list, string>
        /// The medication names, with the token the request was sent with.
        | DrugNames of from: OpenedToken option * Result<Reply<InteractionResponse>, string[]>
        /// The interactions of a numbered check, with the token the request was sent with.
        | Interactions of check: int * from: OpenedToken option * Result<Reply<InteractionResponse>, string[]>
        /// The totals of a numbered query, with the token the request was sent with.
        | Totals of number: int * from: OpenedToken option * Result<Reply<Totals>, string[]>
        /// The rows of the emergency list of a numbered query, with the token the request was sent with.
        | EmergencyList of number: int * from: OpenedToken option * Result<Reply<Intervention[]>, string[]>
        /// The rows of the continuous medication list of a numbered query, with the token the request was sent with.
        | ContinuousList of number: int * from: OpenedToken option * Result<Reply<Intervention[]>, string[]>
        /// The formulary page, with the token the request was sent with.
        | Formulary of from: OpenedToken option * Result<Reply<Formulary>, string[]>
        /// The parenteralia page, with the token the request was sent with.
        | Parenteralia of from: OpenedToken option * Result<Reply<Parenteralia>, string[]>


    /// A message to the loader machine.
    [<RequireQualifiedAccess>]
    type LoaderMsg =
        /// Starts a load.
        | Start of Load
        /// The answer to a load.
        | Landed of Landing
        /// Checks whether the server answers.
        | CheckServer
        /// The result of the server check; an error when the server did not answer.
        | ServerChecked of Result<unit, string>
        /// The patient from the server, or None when the patient is cleared.
        | PatientSet of PatientData option
        /// A new patient is coming; clearing the patient before it requests no pages.
        | PatientExpected
        /// The filter from the workbench.
        | FilterAnswered of Filter
        /// A page is shown.
        | PageShown of Page
        /// The user changed a choice on the formulary page.
        | FormularyChanged of Formulary
        /// The user changed a choice on the parenteralia page.
        | ParenteraliaChanged of Parenteralia
        /// The server reloaded its resources.
        | ResourcesReloaded
        /// Checks the interactions of these medications; fewer than two clears the interactions.
        | CheckInteractions of drugs: string list
        /// Queries the combined totals of the orders in the query; None clears the totals.
        | QueryTotals of TotalsQuery option
        /// Queries the rows of the emergency list for the patient data; None clears the rows.
        | QueryEmergencyList of PatientData option
        /// The rows of the emergency list with these numbers, from 1, are checked.
        | CheckEmergencyListRows of int[]
        /// Queries the rows of the continuous medication list for the patient data; None clears the rows.
        | QueryContinuousList of PatientData option


    /// An effect the App carries out: a server call whose answer returns as a message, a delayed message,
    /// or a notice to the user or the Session.
    [<RequireQualifiedAccess>]
    type LoaderEffect =
        | FetchSettings
        | FetchLocalization
        | FetchProducts
        | FetchDrugNames
        | FetchFormulary of Formulary
        | FetchParenteralia of Parenteralia
        /// Requests the interactions of these medications, with the check number.
        | FetchInteractions of check: int * drugs: string list
        /// Requests the totals of the orders in the query, with the query number.
        | FetchTotals of number: int * TotalsQuery
        /// Requests the rows of the emergency list for the patient data, with the query number.
        | FetchEmergencyList of number: int * PatientData
        /// Requests the rows of the continuous medication list for the patient data, with the query number.
        | FetchContinuousList of number: int * PatientData
        /// Seeds the workbench filter.
        | SeedWorkbench of FilterSeed
        /// Passes on the loaded server settings: the default language and the demo flag.
        | SettingsLanded of ServerSettings
        /// Checks whether the server answers.
        | CheckServer
        /// Checks the server again after this many seconds.
        | CheckServerLater of seconds: int
        /// Starts the load again after this many seconds.
        | AskAgainLater of Load * seconds: int
        /// Shows the alert on the snackbar.
        | Alert of Alert.Alert
        /// Closes the snackbar only when it shows the interactions found alert.
        | WithdrawInteractionsFound
        /// Reports that a request from this source failed with these errors.
        | RequestFailed of ServerErrorPolicy.ErrorSource * string[]
        /// Reports that a request from this source succeeded.
        | RequestSucceeded of ServerErrorPolicy.ErrorSource
        /// Passes the notice of a reply to the Session, with the token the request was sent with.
        | NoticeReceived of from: OpenedToken option * RecordNotice


    /// Functions on the loader state.
    module LoaderState =

        /// The state with no load started.
        let initial =
            {
                Settings = HasNotStartedYet
                Localization = HasNotStartedYet
                Products = HasNotStartedYet
                DrugNames = HasNotStartedYet
                Interactions = HasNotStartedYet
                InteractionCheck = 0
                Totals = HasNotStartedYet
                TotalsQueryNumber = 0
                EmergencyList = HasNotStartedYet
                EmergencyListQuery = None
                EmergencyListQueryNumber = 0
                EmergencyListChecked = [||]
                ContinuousList = HasNotStartedYet
                ContinuousListQuery = None
                ContinuousListQueryNumber = 0
                Formulary = HasNotStartedYet
                FormularyAskAgain = None
                Parenteralia = HasNotStartedYet
                ParenteraliaAskAgain = None
                PatientExpected = false
                Patient = None
                DrugNameFailures = 0
                Server = HasNotStartedYet
                Failed = []
            }


        /// Lists each load with its load state, without the loaded value.
        let readings (state: LoaderState) =
            let reading deferred = deferred |> Deferred.map ignore

            [
                Load.Settings, reading state.Settings
                Load.Localization, reading state.Localization
                Load.Products, reading state.Products
                Load.Formulary, reading state.Formulary
                Load.Parenteralia, reading state.Parenteralia
                Load.DrugNames, reading state.DrugNames
                Load.Interactions, reading state.Interactions
                Load.Totals, reading state.Totals
                Load.EmergencyList, reading state.EmergencyList
                Load.ContinuousList, reading state.ContinuousList
            ]


    /// Returns the loads that are in progress.
    let out state = state |> LoaderState.readings |> outOf


    /// Returns the loads that have loaded.
    let loaded state = state |> LoaderState.readings |> loadedOf


    /// Returns the load state for an answer: resolved on success, not started on an error.
    /// A failed required load is added to the failed loads.
    let settle load result (state: LoaderState) =
        match result with
        | Ok value -> Resolved value, state
        | Error _ when Loads.required |> List.contains load ->
            HasNotStartedYet, { state with Failed = load :: state.Failed }
        | Error _ -> HasNotStartedYet, state


    /// Starts a load with its effect, unless the load is already in progress.
    /// Loaded data stays visible while it is requested again.
    let start effect reading =
        if isOut reading then
            reading, []
        else
            Deferred.refresh reading, [ effect ]


    /// The number of failures after which the medication names are no longer requested again.
    let drugNameLimit = 3


    /// The seconds before the medication names are requested again.
    let drugNamesWait = 3


    /// The seconds before the server is checked again.
    let serverWait = 5


    /// Requests the formulary again with the current choices, for the patient from the server.
    let startFormulary (state: LoaderState) =
        let form =
            { (state.Formulary |> Deferred.defaultValue Formulary.empty) with PatientData = state.Patient }

        let reading, effects = state.Formulary |> start (LoaderEffect.FetchFormulary form)
        { state with Formulary = reading }, effects


    /// Requests the parenteralia again with the current choices.
    let startParenteralia (state: LoaderState) =
        let par = state.Parenteralia |> Deferred.defaultValue Parenteralia.empty
        let reading, effects = state.Parenteralia |> start (LoaderEffect.FetchParenteralia par)
        { state with Parenteralia = reading }, effects


    /// Requests the formulary and the parenteralia again.
    let startPages state =
        let state, formulary = state |> startFormulary
        let state, parenteralia = state |> startParenteralia
        state, formulary @ parenteralia


    /// Applies the workbench filter to the formulary page and requests the page. While the page loads,
    /// the filter is stored and applied when the load ends.
    let syncFormulary filter (state: LoaderState) =
        if isOut state.Formulary then
            { state with FormularyAskAgain = Some filter }, []
        else
            { state with
                Formulary =
                    state.Formulary
                    |> Deferred.defaultValue Formulary.empty
                    |> FilterSync.syncFilterToFormulary filter
                    |> Resolved
            }
            |> startFormulary


    /// Applies the workbench filter to the parenteralia page and requests the page. While the page loads,
    /// the filter is stored and applied when the load ends.
    let syncParenteralia filter (state: LoaderState) =
        if isOut state.Parenteralia then
            { state with ParenteraliaAskAgain = Some filter }, []
        else
            { state with
                Parenteralia =
                    state.Parenteralia
                    |> Deferred.defaultValue Parenteralia.empty
                    |> FilterSync.syncFilterToParenteralia filter
                    |> Resolved
            }
            |> startParenteralia


    /// Returns the effect that seeds the workbench with a page choice, only when there is a patient,
    /// because a page shows its choices only then.
    let seedWithPatient seed (state: LoaderState) =
        match state.Patient with
        | Some _ -> [ LoaderEffect.SeedWorkbench seed ]
        | None -> []


    /// After a page load ends, clears the filter stored during the load and applies it to the page,
    /// which requests the page again. Without a stored filter, returns the state and effects unchanged.
    let askAgain held clear sync (state: LoaderState, effects) =
        match held with
        | Some filter ->
            let state, again = state |> clear |> sync filter
            state, effects @ again
        | None -> state, effects


    /// Queries the rows of the emergency list for the patient data and stores the query. Every query gets a new
    /// number, so an earlier answer is ignored. No query clears the rows; otherwise the rows shown stay until the
    /// answer.
    let queryEmergencyList query (state: LoaderState) =
        let number = state.EmergencyListQueryNumber + 1

        match query with
        | None ->
            { state with
                EmergencyList = HasNotStartedYet
                EmergencyListQuery = None
                EmergencyListQueryNumber = number
                EmergencyListChecked = [||]
            },
            []
        | Some pat ->
            { state with
                EmergencyList = state.EmergencyList |> Deferred.refresh
                EmergencyListQuery = query
                EmergencyListQueryNumber = number
                EmergencyListChecked = [||]
            },
            [ LoaderEffect.FetchEmergencyList(number, pat) ]


    /// Queries the rows of the continuous medication list for the patient data and stores the query. Every query
    /// gets a new number, so an earlier answer is ignored. No query clears the rows; otherwise the rows shown stay
    /// until the answer.
    let queryContinuousList query (state: LoaderState) =
        let number = state.ContinuousListQueryNumber + 1

        match query with
        | None ->
            { state with
                ContinuousList = HasNotStartedYet
                ContinuousListQuery = None
                ContinuousListQueryNumber = number
            },
            []
        | Some pat ->
            { state with
                ContinuousList = state.ContinuousList |> Deferred.refresh
                ContinuousListQuery = query
                ContinuousListQueryNumber = number
            },
            [ LoaderEffect.FetchContinuousList(number, pat) ]


    /// Returns the effect that passes the notice of a reply to the Session, or no effect without a notice.
    let told from (reply: Reply<'a>) =
        reply.Notice
        |> Option.map (fun notice -> LoaderEffect.NoticeReceived(from, notice))
        |> Option.toList


    /// Returns the next state and the effects for a message. A start of a load in progress, or of a load
    /// this machine does not handle, changes nothing.
    let transition msg (state: LoaderState) =
        match msg with
        | LoaderMsg.Start Load.Settings ->
            let reading, effects = state.Settings |> start LoaderEffect.FetchSettings
            { state with Settings = reading }, effects
        | LoaderMsg.Start Load.Localization ->
            let reading, effects = state.Localization |> start LoaderEffect.FetchLocalization
            { state with Localization = reading }, effects
        | LoaderMsg.Start Load.Products ->
            let reading, effects = state.Products |> start LoaderEffect.FetchProducts
            { state with Products = reading }, effects
        | LoaderMsg.Start Load.Formulary -> state |> startFormulary
        | LoaderMsg.Start Load.Parenteralia -> state |> startParenteralia
        | LoaderMsg.Start Load.DrugNames ->
            let reading, effects = state.DrugNames |> start LoaderEffect.FetchDrugNames
            { state with DrugNames = reading }, effects

        | LoaderMsg.Landed(Landing.Settings result) ->
            let reading, state = state |> settle Load.Settings result

            { state with Settings = reading },
            [
                match result with
                | Ok settings -> LoaderEffect.SettingsLanded settings
                | Error _ -> ()
            ]
        | LoaderMsg.Landed(Landing.Localization result) ->
            let reading, state = state |> settle Load.Localization result
            { state with Localization = reading }, []
        | LoaderMsg.Landed(Landing.Products result) ->
            let reading, state = state |> settle Load.Products result
            { state with Products = reading }, []
        | LoaderMsg.Landed(Landing.DrugNames(from, Ok reply)) ->
            match reply.Response with
            | InteractionResponse.DrugNamesLoaded names ->
                { state with
                    DrugNames = Resolved names
                    DrugNameFailures = 0
                },
                told from reply
            // The medication names call never returns interactions.
            | InteractionResponse.InteractionsChecked _ -> state, told from reply
        | LoaderMsg.Landed(Landing.DrugNames(_, Error _)) ->
            let failures = state.DrugNameFailures + 1

            { state with
                DrugNames = HasNotStartedYet
                DrugNameFailures = failures
            },
            [
                if failures >= drugNameLimit then
                    LoaderEffect.Alert Alert.Alert.DrugNamesNotLoaded
                else
                    LoaderEffect.AskAgainLater(Load.DrugNames, drugNamesWait)
            ]

        // A filter stored during the load is applied after the answer is set.
        | LoaderMsg.Landed(Landing.Formulary(from, result)) ->
            let state, effects =
                match result with
                | Ok reply ->
                    { state with Formulary = Resolved reply.Response },
                    LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Formulary
                    :: told from reply
                | Error errs ->
                    { state with Formulary = HasNotStartedYet },
                    [ LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.Formulary, errs) ]

            (state, effects)
            |> askAgain state.FormularyAskAgain (fun state -> { state with FormularyAskAgain = None }) syncFormulary
        | LoaderMsg.Landed(Landing.Parenteralia(from, result)) ->
            let state, effects =
                match result with
                | Ok reply ->
                    { state with Parenteralia = Resolved reply.Response },
                    LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Parenteralia
                    :: told from reply
                | Error errs ->
                    { state with Parenteralia = HasNotStartedYet },
                    [ LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.Parenteralia, errs) ]

            (state, effects)
            |> askAgain
                state.ParenteraliaAskAgain
                (fun state -> { state with ParenteraliaAskAgain = None })
                syncParenteralia

        // An answer to an earlier check is ignored.
        | LoaderMsg.Landed(Landing.Interactions(check, _, _)) when check <> state.InteractionCheck -> state, []
        | LoaderMsg.Landed(Landing.Interactions(_, from, Ok reply)) ->
            let succeeded = LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Interactions

            match reply.Response with
            | InteractionResponse.InteractionsChecked rows ->
                { state with Interactions = Resolved rows },
                [
                    succeeded
                    if rows.Length > 0 then
                        LoaderEffect.Alert(Alert.Alert.InteractionsFound rows.Length)
                    else
                        LoaderEffect.WithdrawInteractionsFound
                    yield! told from reply
                ]
            // The interaction check never returns medication names.
            | InteractionResponse.DrugNamesLoaded _ -> state, succeeded :: told from reply
        | LoaderMsg.Landed(Landing.Interactions(_, _, Error errs)) ->
            { state with Interactions = HasNotStartedYet },
            [ LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.Interactions, errs) ]

        // An answer to an earlier query is ignored.
        | LoaderMsg.Landed(Landing.Totals(number, _, _)) when number <> state.TotalsQueryNumber -> state, []
        | LoaderMsg.Landed(Landing.Totals(_, from, Ok reply)) ->
            { state with Totals = Resolved reply.Response },
            LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Totals
            :: told from reply
        | LoaderMsg.Landed(Landing.Totals(_, _, Error errs)) ->
            { state with Totals = HasNotStartedYet },
            [ LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.Totals, errs) ]

        // An answer to an earlier query is ignored.
        | LoaderMsg.Landed(Landing.EmergencyList(number, _, _)) when number <> state.EmergencyListQueryNumber ->
            state, []
        | LoaderMsg.Landed(Landing.EmergencyList(_, from, Ok reply)) ->
            { state with EmergencyList = Resolved reply.Response },
            LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.EmergencyList
            :: told from reply
        | LoaderMsg.Landed(Landing.EmergencyList(_, _, Error errs)) ->
            { state with EmergencyList = HasNotStartedYet },
            [
                LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.EmergencyList, errs)
            ]

        // An answer to an earlier query is ignored.
        | LoaderMsg.Landed(Landing.ContinuousList(number, _, _)) when number <> state.ContinuousListQueryNumber ->
            state, []
        | LoaderMsg.Landed(Landing.ContinuousList(_, from, Ok reply)) ->
            { state with ContinuousList = Resolved reply.Response },
            LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.ContinuousList
            :: told from reply
        | LoaderMsg.Landed(Landing.ContinuousList(_, _, Error errs)) ->
            { state with ContinuousList = HasNotStartedYet },
            [
                LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.ContinuousList, errs)
            ]

        | LoaderMsg.CheckServer ->
            let reading, effects = state.Server |> start LoaderEffect.CheckServer
            { state with Server = reading }, effects
        // When the server answers, the medication names are requested again if they are not loaded or loading.
        | LoaderMsg.ServerChecked(Ok()) ->
            let drugNames, effects =
                match state.DrugNames with
                | HasNotStartedYet -> state.DrugNames |> start LoaderEffect.FetchDrugNames
                | _ -> state.DrugNames, []

            { state with
                Server = Resolved true
                DrugNames = drugNames
            },
            LoaderEffect.RequestSucceeded ServerErrorPolicy.ErrorSource.Server :: effects
        | LoaderMsg.ServerChecked(Error err) ->
            { state with Server = Resolved false },
            [
                LoaderEffect.RequestFailed(ServerErrorPolicy.ErrorSource.Server, [| err |])
                LoaderEffect.CheckServerLater serverWait
            ]

        | LoaderMsg.PatientExpected -> { state with PatientExpected = true }, []
        // Resets both pages for the new patient and requests them, unless the patient is cleared while a new
        // patient is coming.
        | LoaderMsg.PatientSet patient ->
            let expected = state.PatientExpected

            let state =
                { state with
                    Patient = patient
                    Formulary = Resolved { Formulary.empty with PatientData = patient }
                    Parenteralia = Resolved Parenteralia.empty
                    FormularyAskAgain = None
                    ParenteraliaAskAgain = None
                    PatientExpected = false
                }

            match patient with
            | None when expected -> state, []
            | _ -> state |> startPages
        | LoaderMsg.FilterAnswered filter ->
            let state, formulary = state |> syncFormulary filter
            let state, parenteralia = state |> syncParenteralia filter
            state, formulary @ parenteralia
        // Showing a page requests the medication names again when they are not loaded or loading.
        | LoaderMsg.PageShown page ->
            let state, drugNames =
                match state.DrugNames with
                | HasNotStartedYet ->
                    let reading, effects = state.DrugNames |> start LoaderEffect.FetchDrugNames
                    { state with DrugNames = reading }, effects
                | _ -> state, []

            let state, pages =
                match page with
                | Page.Formulary -> state |> startFormulary
                | Page.Parenteralia -> state |> startParenteralia
                | _ -> state, []

            state, drugNames @ pages
        // Copies the formulary choices to the parenteralia page, seeds the workbench and requests both pages.
        | LoaderMsg.FormularyChanged form ->
            let state, formulary =
                { state with
                    Formulary = Resolved form
                    Parenteralia =
                        state.Parenteralia
                        |> Deferred.map (fun par ->
                            { par with
                                Generic = form.Generic
                                Route = form.Route
                                Form = form.Form
                            }
                        )
                }
                |> startFormulary

            let seeded =
                state
                |> seedWithPatient
                    {
                        Source = SeedSource.Formulary
                        Indication = form.Indication
                        Generic = form.Generic
                        Route = form.Route
                        Form = form.Form
                        DoseType = form.DoseType
                    }

            let state, parenteralia = state |> startParenteralia
            state, formulary @ seeded @ parenteralia
        // Copies the parenteralia choices to the formulary page, clearing the indication and dose type,
        // seeds the workbench and requests both pages.
        | LoaderMsg.ParenteraliaChanged par ->
            let state, formulary =
                { state with
                    Parenteralia = Resolved par
                    Formulary =
                        state.Formulary
                        |> Deferred.map (fun form ->
                            { form with
                                Indication = None
                                Generic = par.Generic
                                Route = par.Route
                                Form = par.Form
                                DoseType = None
                            }
                        )
                }
                |> startFormulary

            let seeded =
                state
                |> seedWithPatient
                    {
                        Source = SeedSource.Parenteralia
                        Indication = None
                        Generic = par.Generic
                        Route = par.Route
                        Form = par.Form
                        DoseType = None
                    }

            let state, parenteralia = state |> startParenteralia
            state, formulary @ seeded @ parenteralia
        // With a patient, an empty reload seed evaluates the workbench again, which also updates both pages.
        // Without a patient, only the pages are requested again. Both lists are queried again, because the rules
        // may have changed.
        | LoaderMsg.ResourcesReloaded ->
            let state, emergencyList = state |> queryEmergencyList state.EmergencyListQuery
            let state, continuousList = state |> queryContinuousList state.ContinuousListQuery

            match state.Patient with
            | Some _ ->
                state,
                [
                    yield! emergencyList
                    yield! continuousList
                    LoaderEffect.SeedWorkbench
                        {
                            Source = SeedSource.Reload
                            Indication = None
                            Generic = None
                            Route = None
                            Form = None
                            DoseType = None
                        }
                ]
            | None ->
                let state, pages = state |> startPages
                state, emergencyList @ continuousList @ pages
        // Every check gets a new number, so earlier answers are ignored. Fewer than two medications clears
        // the interactions; otherwise the rows shown stay until the answer.
        | LoaderMsg.CheckInteractions drugs ->
            let check = state.InteractionCheck + 1

            if List.length drugs < 2 then
                { state with
                    Interactions = HasNotStartedYet
                    InteractionCheck = check
                },
                [ LoaderEffect.WithdrawInteractionsFound ]
            else
                { state with
                    Interactions = state.Interactions |> Deferred.refresh
                    InteractionCheck = check
                },
                [ LoaderEffect.FetchInteractions(check, drugs) ]
        // Every query gets a new number, so earlier answers are ignored. No query clears the totals;
        // otherwise the totals shown stay until the answer.
        | LoaderMsg.QueryTotals query ->
            let number = state.TotalsQueryNumber + 1

            match query with
            | None ->
                { state with
                    Totals = HasNotStartedYet
                    TotalsQueryNumber = number
                },
                []
            | Some query ->
                { state with
                    Totals = state.Totals |> Deferred.refresh
                    TotalsQueryNumber = number
                },
                [ LoaderEffect.FetchTotals(number, query) ]
        | LoaderMsg.QueryEmergencyList query -> state |> queryEmergencyList query
        | LoaderMsg.CheckEmergencyListRows numbers -> { state with EmergencyListChecked = numbers }, []
        | LoaderMsg.QueryContinuousList query -> state |> queryContinuousList query

        | _ -> state, []
