/// The data the client loads: the server settings, the localization, the normal values, the
/// emergency and continuous medication lists, the products and the drug names of the interactions
/// page, each a reading. A start while the same load runs changes nothing; a failed load goes back
/// to not started and, when the application cannot be used without it, is named for the gate.
/// The hospitals are not loaded but read from the emergency medication when it lands. The server
/// is checked until it answers, and the drug names are asked again after a failure, until the third.
module LoaderMachine

open Shared
open Shared.Types
open Shared.Api
open Busy


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
    /// Ask the server whether it answers.
    | CheckServer
    /// Check the server again after this many seconds.
    | CheckServerLater of seconds: int
    /// Start the load again after this many seconds.
    | AskAgainLater of Load * seconds: int
    /// Show the alert on the snackbar.
    | Alert of Alert.Alert
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
            Load.DrugNames, reading state.DrugNames
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
        let told =
            reply.Notice
            |> Option.map (fun notice -> LoaderEffect.NoticeReceived(from, notice))
            |> Option.toList

        match reply.Response with
        | InteractionResponse.DrugNamesLoaded names ->
            { state with
                DrugNames = Resolved names
                DrugNameFailures = 0
            },
            told
        // the drug names call answers with the drug names only
        | InteractionResponse.InteractionsChecked _ -> state, told
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

    | _ -> state, []
