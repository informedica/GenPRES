/// The data the client loads at start-up: the server settings, the localization, the normal
/// values, the emergency and continuous medication lists and the products, each a reading that
/// is asked for once. A start while the same load runs changes nothing; a failed load goes back
/// to not started and, when the application cannot be used without it, is named for the gate.
/// The hospitals are not loaded but read from the emergency medication when it lands.
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


/// A message to the loader machine.
[<RequireQualifiedAccess>]
type LoaderMsg =
    /// A load asked for.
    | Start of Load
    /// A load's answer.
    | Landed of Landing


/// A call for the App to make; the answer comes back as a Landed message.
[<RequireQualifiedAccess>]
type LoaderEffect =
    | FetchSettings
    | FetchLocalization
    | FetchNormalValues
    | FetchBolusMedication
    | FetchContinuousMedication
    | FetchProducts


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


/// The reading set out, with its call, unless the same load is out already.
let start effect reading =
    if isOut reading then
        reading, []
    else
        InProgress, [ effect ]


/// The next state and the calls to make for a message. A start while the same load runs, and a
/// start of a load this machine does not hold, change nothing.
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

    | _ -> state, []
