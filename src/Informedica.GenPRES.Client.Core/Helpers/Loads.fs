namespace Informedica.GenPRES.Client.Core.Helpers

/// The data loads and the requests out, and reading them from the client's readings.
module Loads =


    /// A data load, one per reading the client fetches. The server check is none: it reads nothing a
    /// page shows.
    [<RequireQualifiedAccess>]
    type Load =
        | Settings
        | Localization
        | Hospitals
        | NormalValues
        | BolusMedication
        | ContinuousMedication
        | Products
        | Formulary
        | Parenteralia
        | Interactions
        /// The drug names of the interactions page; they also start without a click, after a
        /// failure, so they never hold the menu, the title bar or the panel.
        | DrugNames
        | LogFiles
        | LogAnalysis
        /// The resource reload, until the server has reloaded.
        | Reload


    /// A request out.
    [<RequireQualifiedAccess>]
    type Request =
        /// A change of the patient.
        | Patient
        /// A workbench request.
        | Workbench
        /// A plan request.
        | Plan
        /// A Session request: the launch, the resume, the PIN, the close, a refresh or an open. A url
        /// with a patient or a medication ends the Session; a request still out then does not count,
        /// since its answer changes nothing on the screen.
        | Session
        /// A signature, from the sign click until it is answered or cancelled.
        | Signature
        /// A quantity field counting step clicks, from the first click until it sends them: what
        /// it sends must not meet a request out.
        | Counting
        /// A data load.
        | Load of Load


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


    /// Whether a reading is out.
    let isOut reading =
        match reading with
        | InProgress
        | Refreshing _ -> true
        | HasNotStartedYet
        | Resolved _ -> false


    /// The loads the application cannot be used without.
    let required =
        [
            Load.Localization
            Load.NormalValues
            Load.BolusMedication
            Load.ContinuousMedication
            Load.Products
        ]
