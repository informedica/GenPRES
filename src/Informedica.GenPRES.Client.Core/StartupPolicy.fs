/// Decides whether the application has started: it is on hold until everything the page load
/// asked for has answered and the loads it cannot be used without have loaded. A required load
/// that failed keeps it on hold and is named, so a list is never silently empty.
module StartupPolicy

open Shared
open Busy
open TermText


/// Where the start-up is.
[<RequireQualifiedAccess>]
type Startup =
    /// Something is still out, or a required load has not loaded yet.
    | Starting
    /// Nothing is out and every required load has loaded.
    | Started
    /// These required loads failed; only a reload of the page goes on.
    | Failed of Load list


/// The loads the application cannot be used without.
let required =
    [
        Load.Localization
        Load.NormalValues
        Load.BolusMedication
        Load.ContinuousMedication
        Load.Products
    ]


/// Where the start-up is, from the requests out, the required loads that loaded and the loads
/// that failed. The drug names never hold it, as Busy.any leaves them out.
let status out loaded failed =
    match required |> List.filter (fun load -> failed |> List.contains load) with
    | [] when
        not (Busy.any out)
        && required |> List.forall (fun load -> loaded |> List.contains load)
        ->
        Startup.Started
    | [] -> Startup.Starting
    | failed -> Startup.Failed failed


/// The English of the start-up terms, used when the sheet has no row for a term or has not
/// loaded; the localization itself may be the load that failed. Any other term is the session
/// gate's.
let english (term: Terms) =
    match term with
    | Terms.``Startup Gate Starting`` -> "Starting GenPRES"
    | Terms.``Startup Gate Starting Text`` -> "Loading what GenPRES needs to start."
    | Terms.``Startup Gate Failed`` -> "GenPRES could not start"
    | Terms.``Startup Gate Failed Text`` -> "Not loaded: {0}. Reload the page to try again."
    | Terms.``Startup Load Localization`` -> "the translations"
    | Terms.``Startup Load Normal Values`` -> "the normal values"
    | Terms.``Startup Load Bolus Medication`` -> "the emergency list"
    | Terms.``Startup Load Continuous Medication`` -> "the continuous medication list"
    | Terms.``Startup Load Products`` -> "the products"
    | _ -> SessionGatePolicy.english term


/// The name of a required load in the failure text.
let loadName (tr: Terms -> string) load =
    match load with
    | Load.Localization -> tr Terms.``Startup Load Localization``
    | Load.NormalValues -> tr Terms.``Startup Load Normal Values``
    | Load.BolusMedication -> tr Terms.``Startup Load Bolus Medication``
    | Load.ContinuousMedication -> tr Terms.``Startup Load Continuous Medication``
    | Load.Products -> tr Terms.``Startup Load Products``
    | _ -> $"%A{load}"


/// The gate while the application starts, or after a required load failed; None once started.
let startupGate (tr: Terms -> string) startup : SessionGatePolicy.Gate option =
    match startup with
    | Startup.Started -> None
    | Startup.Starting ->
        Some
            {
                Title = tr Terms.``Startup Gate Starting``
                Body = tr Terms.``Startup Gate Starting Text``
                Busy = true
                Actions = []
                Form = None
            }
    | Startup.Failed loads ->
        Some
            {
                Title = tr Terms.``Startup Gate Failed``
                Body =
                    tr Terms.``Startup Gate Failed Text``
                    |> fill [ loads |> List.map (loadName tr) |> String.concat ", " ]
                Busy = false
                Actions = []
                Form = None
            }


/// What the gate shows: the session's gate when the session has one, else the start-up's.
let gate tr startup session =
    SessionGatePolicy.gateFor tr session |> Option.orElse (startupGate tr startup)


/// Whether the gate covers the application: until it has started, and while the session's
/// gate does.
let isGated startup session = startup <> Startup.Started || SessionGatePolicy.isGated session
