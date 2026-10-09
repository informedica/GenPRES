/// The data the client loads: the server settings, the localization, the normal values, the
/// emergency and continuous medication lists, the products, the formulary and parenteralia pages
/// and the drug names and interactions of the interactions page, each a reading. A start while the
/// same load runs changes nothing; a failed load goes back to not started and, when the application
/// cannot be used without it, is named for the gate. The hospitals are not loaded but read from the emergency
/// medication when it lands. The server is checked until it answers, and the drug names are asked
/// again after a failure, until the third. The formulary and parenteralia pages follow the
/// workbench's filter and the patient, and a choice on either page is seeded over the workbench.
/// Each interaction check is numbered, so that an answer to an earlier check is dropped.
module LoaderMachine

open Shared
open Shared.Types
open Shared.Models
open Shared.Api
open Busy
open Page
open OrderContextMachine


/// The readings of the start-up loads, and the ones that failed.
type LoaderState =
    {
        /// What the server was configured with: the default language, the demo flag.
        Settings: Deferred<ServerSettings>
        /// The terms of every language, as the sheet's rows.
        Localization: Deferred<string[][]>
        /// The normal weights and heights by age.
        NormalValues: Deferred<NormalValues>
        /// The emergency medication list.
        BolusMedication: Deferred<BolusMedication list>
        /// The continuous medication list.
        ContinuousMedication: Deferred<ContinuousMedication list>
        /// The products.
        Products: Deferred<Product list>
        /// The hospitals the emergency medication names.
        Hospitals: Deferred<string[]>
        /// The drug names the interactions page offers.
        DrugNames: Deferred<string[]>
        /// The interactions of the drugs last checked.
        Interactions: Deferred<DrugInteraction[]>
        /// The number of the interaction check under way; an answer to an earlier check is dropped,
        /// so it can neither replace the rows nor clear the error of a later one.
        InteractionCheck: int
        /// The formulary page.
        Formulary: Deferred<Formulary>
        /// The workbench filter answered while the formulary loaded, asked for once the load lands.
        FormularyAskAgain: Filter option
        /// The parenteralia page.
        Parenteralia: Deferred<Parenteralia>
        /// The workbench filter answered while the parenteralia loaded, asked for once the load lands.
        ParenteraliaAskAgain: Filter option
        /// The patient the server last answered, which the formulary is asked for and whose presence
        /// lets a page's choice seed the workbench.
        Patient: Patient option
        /// The failures of the drug names since they last loaded.
        DrugNameFailures: int
        /// Whether the server answered its last check.
        Server: Deferred<bool>
        /// The required loads that failed, which keep the application on hold and are named.
        Failed: Load list
    }


/// What a start-up load brought: its answer, or the error without its text mattering here.
[<RequireQualifiedAccess>]
type Landing =
    | Settings of Result<ServerSettings, string>
    | Localization of Result<string[][], string>
    | NormalValues of Result<NormalValues, string>
    | BolusMedication of Result<BolusMedication list, string>
    | ContinuousMedication of Result<ContinuousMedication list, string>
    | Products of Result<Product list, string>
    /// The drug names, with the token the request was sent with.
    | DrugNames of from: OpenedToken option * Result<Reply<InteractionResponse>, string[]>
    /// The interactions of a numbered check, with the token the request was sent with.
    | Interactions of check: int * from: OpenedToken option * Result<Reply<InteractionResponse>, string[]>
    /// The formulary page, with the token the request was sent with.
    | Formulary of from: OpenedToken option * Result<Reply<Formulary>, string[]>
    /// The parenteralia page, with the token the request was sent with.
    | Parenteralia of from: OpenedToken option * Result<Reply<Parenteralia>, string[]>


/// A message to the loader machine.
[<RequireQualifiedAccess>]
type LoaderMsg =
    /// A load asked for.
    | Start of Load
    /// A load's answer.
    | Landed of Landing
    /// The server asked whether it answers.
    | CheckServer
    /// The server's answer, or the error of a server that did not answer.
    | ServerChecked of Result<unit, string>
    /// The patient the server answered, or none once cleared.
    | PatientSet of Patient option
    /// The filter the workbench answered.
    | FilterAnswered of Filter
    /// A page shown.
    | PageShown of Page
    /// The user changed a choice on the formulary page.
    | FormularyChanged of Formulary
    /// The user changed a choice on the parenteralia page.
    | ParenteraliaChanged of Parenteralia
    /// The server reloaded its resources.
    | ResourcesReloaded
    /// The interactions of these drugs asked for.
    | CheckInteractions of drugs: string list


/// What the App carries out: a call, whose answer comes back as a message, a wait, or what the
/// user and the Session are told.
[<RequireQualifiedAccess>]
type LoaderEffect =
    | FetchSettings
    | FetchLocalization
    | FetchNormalValues
    | FetchBolusMedication
    | FetchContinuousMedication
    | FetchProducts
    | FetchDrugNames
    | FetchFormulary of Formulary
    | FetchParenteralia of Parenteralia
    /// Ask for the interactions of these drugs, under the number of the check.
    | FetchInteractions of check: int * drugs: string list
    /// Seed the workbench's filter.
    | SeedWorkbench of FilterSeed
    /// Ask the server whether it answers.
    | CheckServer
    /// Check the server again after this many seconds.
    | CheckServerLater of seconds: int
    /// Start the load again after this many seconds.
    | AskAgainLater of Load * seconds: int
    /// Show the alert on the snackbar.
    | Alert of Alert.Alert
    /// Close the snackbar if it shows the interactions found, and nothing else.
    | WithdrawInteractionsFound
    /// A request of this source failed with these errors.
    | Failed of ServerErrorPolicy.ErrorSource * string[]
    /// A request of this source succeeded.
    | Succeeded of ServerErrorPolicy.ErrorSource
    /// What the reply told the Session, with the token the request was sent with.
    | NoticeReceived of from: OpenedToken option * RecordNotice


/// Reading the loader machine's state.
module LoaderState =

    /// Nothing asked for yet.
    let initial =
        {
            Settings = HasNotStartedYet
            Localization = HasNotStartedYet
            NormalValues = HasNotStartedYet
            BolusMedication = HasNotStartedYet
            ContinuousMedication = HasNotStartedYet
            Products = HasNotStartedYet
            Hospitals = HasNotStartedYet
            DrugNames = HasNotStartedYet
            Interactions = HasNotStartedYet
            InteractionCheck = 0
            Formulary = HasNotStartedYet
            FormularyAskAgain = None
            Parenteralia = HasNotStartedYet
            ParenteraliaAskAgain = None
            Patient = None
            DrugNameFailures = 0
            Server = HasNotStartedYet
            Failed = []
        }


    /// Each load with its reading, the value left out.
    let readings (state: LoaderState) =
        let reading deferred = deferred |> Deferred.map ignore

        [
            Load.Settings, reading state.Settings
            Load.Localization, reading state.Localization
            Load.Hospitals, reading state.Hospitals
            Load.NormalValues, reading state.NormalValues
            Load.BolusMedication, reading state.BolusMedication
            Load.ContinuousMedication, reading state.ContinuousMedication
            Load.Products, reading state.Products
            Load.Formulary, reading state.Formulary
            Load.Parenteralia, reading state.Parenteralia
            Load.DrugNames, reading state.DrugNames
            Load.Interactions, reading state.Interactions
        ]


/// The loads of these readings that are out.
let outOf (readings: (Load * Deferred<unit>) list) =
    readings
    |> List.choose (fun (load, reading) ->
        match reading with
        | InProgress
        | Refreshing _ -> Some load
        | HasNotStartedYet
        | Resolved _ -> None
    )


/// The loads of these readings that have loaded.
let loadedOf (readings: (Load * Deferred<unit>) list) =
    readings
    |> List.choose (fun (load, reading) ->
        match reading with
        | Resolved _ -> Some load
        | HasNotStartedYet
        | InProgress
        | Refreshing _ -> None
    )


/// The start-up loads out.
let out state = state |> LoaderState.readings |> outOf


/// The start-up loads that have loaded.
let loaded state = state |> LoaderState.readings |> loadedOf


/// The hospitals the emergency medication names, each once.
let hospitals (meds: BolusMedication list) =
    meds
    |> List.map _.Hospital
    |> List.distinct
    |> List.filter String.notEmpty
    |> List.toArray


/// Whether a reading is out.
let isOut reading =
    match reading with
    | InProgress
    | Refreshing _ -> true
    | HasNotStartedYet
    | Resolved _ -> false


/// The reading of an answer, and the load named as failed when it is required and did not come.
let settle load result (state: LoaderState) =
    match result with
    | Ok value -> Resolved value, state
    | Error _ when StartupPolicy.required |> List.contains load ->
        HasNotStartedYet, { state with Failed = load :: state.Failed }
    | Error _ -> HasNotStartedYet, state


/// The reading set out, with its call, unless the same load is out already. Data that has loaded
/// stays shown while it is asked again.
let start effect reading =
    if isOut reading then
        reading, []
    else
        Deferred.refresh reading, [ effect ]


/// The number of failures after which the drug names are no longer asked again.
let drugNameLimit = 3


/// The seconds before the drug names are asked again.
let drugNamesWait = 3


/// The seconds before the server is checked again.
let serverWait = 5


/// The formulary asked for again over the one shown, for the patient the server answered.
let startFormulary (state: LoaderState) =
    let form = { (state.Formulary |> Deferred.defaultValue Formulary.empty) with Patient = state.Patient }

    let reading, effects = state.Formulary |> start (LoaderEffect.FetchFormulary form)
    { state with Formulary = reading }, effects


/// The parenteralia asked for again over the ones shown.
let startParenteralia (state: LoaderState) =
    let par = state.Parenteralia |> Deferred.defaultValue Parenteralia.empty
    let reading, effects = state.Parenteralia |> start (LoaderEffect.FetchParenteralia par)
    { state with Parenteralia = reading }, effects


/// The formulary and the parenteralia asked for again.
let startPages state =
    let state, formulary = state |> startFormulary
    let state, parenteralia = state |> startParenteralia
    state, formulary @ parenteralia


/// The workbench filter put on the formulary page and the page asked for with it; while the page
/// loads, the filter is held and asked for once the load lands.
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


/// The workbench filter put on the parenteralia page and the page asked for with it; while the
/// page loads, the filter is held and asked for once the load lands.
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


/// The seed of a page's choices over the workbench, only while there is a patient, as a page shows
/// its choices only then.
let seedWithPatient seed (state: LoaderState) =
    match state.Patient with
    | Some _ -> [ LoaderEffect.SeedWorkbench seed ]
    | None -> []


/// After a page load landed: the filter held during the load, if any, cleared and put on the page,
/// which is asked for again.
let askAgain held clear sync (state: LoaderState, effects) =
    match held with
    | Some filter ->
        let state, again = state |> clear |> sync filter
        state, effects @ again
    | None -> state, effects


/// What a reply told the Session, with the token the request was sent with.
let told from (reply: Reply<'a>) =
    reply.Notice
    |> Option.map (fun notice -> LoaderEffect.NoticeReceived(from, notice))
    |> Option.toList


/// The next state and the effects for a message. A start while the same load runs, and a start
/// of a load this machine does not hold, change nothing.
let transition msg (state: LoaderState) =
    match msg with
    | LoaderMsg.Start Load.Settings ->
        let reading, effects = state.Settings |> start LoaderEffect.FetchSettings
        { state with Settings = reading }, effects
    | LoaderMsg.Start Load.Localization ->
        let reading, effects = state.Localization |> start LoaderEffect.FetchLocalization
        { state with Localization = reading }, effects
    | LoaderMsg.Start Load.NormalValues ->
        let reading, effects = state.NormalValues |> start LoaderEffect.FetchNormalValues
        { state with NormalValues = reading }, effects
    | LoaderMsg.Start Load.BolusMedication ->
        let reading, effects = state.BolusMedication |> start LoaderEffect.FetchBolusMedication
        { state with BolusMedication = reading }, effects
    | LoaderMsg.Start Load.ContinuousMedication ->
        let reading, effects = state.ContinuousMedication |> start LoaderEffect.FetchContinuousMedication
        { state with ContinuousMedication = reading }, effects
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
        { state with Settings = reading }, []
    | LoaderMsg.Landed(Landing.Localization result) ->
        let reading, state = state |> settle Load.Localization result
        { state with Localization = reading }, []
    | LoaderMsg.Landed(Landing.NormalValues result) ->
        let reading, state = state |> settle Load.NormalValues result
        { state with NormalValues = reading }, []
    | LoaderMsg.Landed(Landing.BolusMedication result) ->
        let reading, state = state |> settle Load.BolusMedication result

        { state with
            BolusMedication = reading
            Hospitals =
                match result with
                | Ok meds -> Resolved(hospitals meds)
                | Error _ -> state.Hospitals
        },
        []
    | LoaderMsg.Landed(Landing.ContinuousMedication result) ->
        let reading, state = state |> settle Load.ContinuousMedication result
        { state with ContinuousMedication = reading }, []
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
        // the drug names call answers with the drug names only
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

    // a filter held during the load is asked for once the answer is shown
    | LoaderMsg.Landed(Landing.Formulary(from, result)) ->
        let state, effects =
            match result with
            | Ok reply ->
                { state with Formulary = Resolved reply.Response },
                LoaderEffect.Succeeded ServerErrorPolicy.ErrorSource.Formulary
                :: told from reply
            | Error errs ->
                { state with Formulary = HasNotStartedYet },
                [ LoaderEffect.Failed(ServerErrorPolicy.ErrorSource.Formulary, errs) ]

        (state, effects)
        |> askAgain state.FormularyAskAgain (fun state -> { state with FormularyAskAgain = None }) syncFormulary
    | LoaderMsg.Landed(Landing.Parenteralia(from, result)) ->
        let state, effects =
            match result with
            | Ok reply ->
                { state with Parenteralia = Resolved reply.Response },
                LoaderEffect.Succeeded ServerErrorPolicy.ErrorSource.Parenteralia
                :: told from reply
            | Error errs ->
                { state with Parenteralia = HasNotStartedYet },
                [ LoaderEffect.Failed(ServerErrorPolicy.ErrorSource.Parenteralia, errs) ]

        (state, effects)
        |> askAgain
            state.ParenteraliaAskAgain
            (fun state -> { state with ParenteraliaAskAgain = None })
            syncParenteralia

    // an answer to an earlier check is dropped
    | LoaderMsg.Landed(Landing.Interactions(check, _, _)) when check <> state.InteractionCheck -> state, []
    | LoaderMsg.Landed(Landing.Interactions(_, from, Ok reply)) ->
        let succeeded = LoaderEffect.Succeeded ServerErrorPolicy.ErrorSource.Interactions

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
        // the check answers with the interactions only
        | InteractionResponse.DrugNamesLoaded _ -> state, succeeded :: told from reply
    | LoaderMsg.Landed(Landing.Interactions(_, _, Error errs)) ->
        { state with Interactions = HasNotStartedYet },
        [ LoaderEffect.Failed(ServerErrorPolicy.ErrorSource.Interactions, errs) ]

    | LoaderMsg.CheckServer ->
        let reading, effects = state.Server |> start LoaderEffect.CheckServer
        { state with Server = reading }, effects
    // drug names that are not loaded nor out are asked again once the server answers
    | LoaderMsg.ServerChecked(Ok()) ->
        let drugNames, effects =
            match state.DrugNames with
            | HasNotStartedYet -> state.DrugNames |> start LoaderEffect.FetchDrugNames
            | _ -> state.DrugNames, []

        { state with
            Server = Resolved true
            DrugNames = drugNames
        },
        LoaderEffect.Succeeded ServerErrorPolicy.ErrorSource.Server :: effects
    | LoaderMsg.ServerChecked(Error err) ->
        { state with Server = Resolved false },
        [
            LoaderEffect.Failed(ServerErrorPolicy.ErrorSource.Server, [| err |])
            LoaderEffect.CheckServerLater serverWait
        ]

    // both pages for the new patient, the choices made for the earlier one gone
    | LoaderMsg.PatientSet patient ->
        { state with
            Patient = patient
            Formulary = Resolved { Formulary.empty with Patient = patient }
            Parenteralia = Resolved Parenteralia.empty
            FormularyAskAgain = None
            ParenteraliaAskAgain = None
        }
        |> startPages
    | LoaderMsg.FilterAnswered filter ->
        let state, formulary = state |> syncFormulary filter
        let state, parenteralia = state |> syncParenteralia filter
        state, formulary @ parenteralia
    // drug names that gave up are asked again when a page is shown
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
    // the formulary's choices put on the parenteralia page and seeded over the workbench, with
    // the formulary's rule
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
    // the parenteralia page's choices put on the formulary and seeded over the workbench, with
    // its rule
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
    // with a patient the workbench is evaluated again as it is, which takes both pages with it;
    // without one, the pages alone are asked again
    | LoaderMsg.ResourcesReloaded ->
        match state.Patient with
        | Some _ ->
            state,
            [
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
        | None -> state |> startPages
    // every check, the empty one too, ends the checks before it; the rows shown stay until the
    // answer
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

    | _ -> state, []
