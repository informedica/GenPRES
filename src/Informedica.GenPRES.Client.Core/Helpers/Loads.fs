namespace Informedica.GenPRES.Client.Core.Helpers

/// The data loads and the pending requests of the client, and which loads are pending or loaded.
module Loads =


    /// A data load, one for each kind of data the client fetches. The server check has no load, because
    /// no page shows its result.
    [<RequireQualifiedAccess>]
    type Load =
        | Settings
        | Localization
        | BolusMedication
        | ContinuousMedication
        | Products
        | Formulary
        | Parenteralia
        | Interactions
        /// The medication names of the interactions page. They also start without a click, after a
        /// failure, so they never make the menu, the title bar or the panel busy.
        | DrugNames
        /// The combined totals of the nutrition page. They also start without a click, when a step changed
        /// the orders they count, so they never make the menu, the title bar or the panel busy.
        | Totals
        | LogFiles
        | LogAnalysis
        /// The resource reload, until the server has reloaded.
        | Reload


    /// A pending request.
    [<RequireQualifiedAccess>]
    type Request =
        /// A patient request.
        | Patient
        /// A workbench request.
        | Workbench
        /// A request of any nutrition slot.
        | Slots
        /// A plan request.
        | Plan
        /// Not a request: the plan is calculated on older patient data, and everything waits until a
        /// retried recalculation succeeds.
        | PlanBehind
        /// A session request: the launch, the resume, the PIN, the close, a refresh or an open. A url
        /// with a patient or a medication ends the session; a pending session request then no longer
        /// counts, because its answer changes nothing on the screen.
        | Session
        /// A signature, from the sign click until it is answered or cancelled.
        | Signature
        /// A quantity field that counts step clicks, from the first click until it sends them, so that
        /// no other request runs at the same time as the request with the clicks.
        | Counting
        /// A data load.
        | Load of Load


    /// Returns the loads that are pending.
    let outOf (readings: (Load * Deferred<unit>) list) =
        readings
        |> List.choose (fun (load, reading) ->
            match reading with
            | InProgress
            | Refreshing _ -> Some load
            | HasNotStartedYet
            | Resolved _ -> None
        )


    /// Returns the loads that have loaded.
    let loadedOf (readings: (Load * Deferred<unit>) list) =
        readings
        |> List.choose (fun (load, reading) ->
            match reading with
            | Resolved _ -> Some load
            | HasNotStartedYet
            | InProgress
            | Refreshing _ -> None
        )


    /// Whether a load is pending.
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
            Load.BolusMedication
            Load.ContinuousMedication
            Load.Products
        ]
