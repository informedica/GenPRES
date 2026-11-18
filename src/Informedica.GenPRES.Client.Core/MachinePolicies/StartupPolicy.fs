namespace Informedica.GenPRES.Client.Core.MachinePolicies

/// Decides whether the application has started. It waits until every request of the page load has
/// answered and every required load has loaded. A failed required load stops the start and is named,
/// so a list is never silently empty.
module StartupPolicy =

    open Informedica.GenPRES.Client.Core.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Shared
    open Loads
    open TermText


    /// Where the start-up is.
    [<RequireQualifiedAccess>]
    type Startup =
        /// A request is still pending, or a required load has not loaded yet.
        | Starting
        /// No request is pending and every required load has loaded.
        | Started
        /// These required loads failed; only a page reload can continue.
        | Failed of Load list


    /// Returns the start-up status from the pending requests, the loaded loads and the failed loads. The
    /// medication names and totals loads do not delay the start, because BusyPolicy.any ignores them.
    let status out loaded failed =
        match required |> List.filter (fun load -> failed |> List.contains load) with
        | [] when
            not (BusyPolicy.any out)
            && required |> List.forall (fun load -> loaded |> List.contains load)
            ->
            Startup.Started
        | [] -> Startup.Starting
        | failed -> Startup.Failed failed


    /// The English of the start-up terms, used when the sheet has no row for a term or has not
    /// loaded; the localization itself may be the load that failed. Any other term falls back to
    /// SessionGatePolicy.english.
    let english (term: Terms) =
        match term with
        | Terms.``Startup Gate Starting`` -> "Starting GenPRES"
        | Terms.``Startup Gate Starting Text`` -> "Loading what GenPRES needs to start."
        | Terms.``Startup Gate Failed`` -> "GenPRES could not start"
        | Terms.``Startup Gate Failed Text`` -> "Not loaded: {0}. Reload the page to try again."
        | Terms.``Startup Load Localization`` -> "the translations"
        | Terms.``Startup Load Bolus Medication`` -> "the emergency list"
        | Terms.``Startup Load Continuous Medication`` -> "the continuous medication list"
        | Terms.``Startup Load Products`` -> "the products"
        | _ -> SessionGatePolicy.english term


    /// The name of a required load in the failure text.
    let loadName (tr: Terms -> string) load =
        match load with
        | Load.Localization -> tr Terms.``Startup Load Localization``
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


    /// Returns the gate to show: the session gate when the session has one, else the start-up gate.
    let gate tr startup session =
        SessionGatePolicy.gateFor tr session |> Option.orElse (startupGate tr startup)


    /// Whether the gate covers the application: until start-up completes, and while the session gate is
    /// shown and no signature is under way. A submission sent before the session ended still shows its
    /// outcome before the session gate covers the application.
    let isGated startup session signing =
        startup <> Startup.Started
        || (SessionGatePolicy.isGated session && not (SigningPolicy.underWay signing))
