module AppEnv

open Shared
open Shared.Types


/// Downcast appEnv to the requested interface.
let inline asEnv<'T> (appEnv: obj) : 'T = unbox<'T> appEnv


/// Localization data access
[<Interface>]
type ILocalization =
    abstract LocalizationTerms: Deferred<string[][]>


/// Order context data and commands
[<Interface>]
type IOrderContext =
    abstract OrderContext: Deferred<OrderContext>
    abstract OrderContextMsg: Api.OrderContextCommand * OrderContext -> unit


/// The one plan, nutrition included, and the commands on it
[<Interface>]
type IOrderPlan =
    abstract OrderPlan: Deferred<OrderPlan>
    abstract PlanCommand: Api.PlanCommand -> unit
    // the plan as shown: opens the order-plan page on it and recomputes its totals
    abstract ShowOrderPlan: OrderPlan -> unit


/// Patient data and updates
[<Interface>]
type IPatient =
    abstract Patient: Patient option
    abstract UpdatePatient: Patient option -> unit


/// Formulary data and updates
[<Interface>]
type IFormulary =
    abstract Formulary: Deferred<Formulary>
    abstract UpdateFormulary: Formulary -> unit


/// Parenteralia data and updates
[<Interface>]
type IParenteralia =
    abstract Parenteralia: Deferred<Parenteralia>
    abstract UpdateParenteralia: Parenteralia -> unit


/// Drug interaction data and commands
[<Interface>]
type IInteractions =
    abstract Interactions: Deferred<DrugInteraction[]>
    abstract InteractionDrugNames: Deferred<string[]>
    abstract CheckInteractions: string list -> unit


/// Resource reloading (admin/settings): under the token the login bought, so no password here
[<Interface>]
type IResources =
    // InProgress while the server reloads; Resolved once it answered
    abstract Reload: Deferred<unit>
    abstract ReloadResources: unit -> unit


/// The launch Session: its phase, and the actions the UI offers on it
[<Interface>]
type ISession =
    abstract Session: SessionMachine.Session
    // explicit close, from an open Session
    abstract Close: unit -> unit
    // from Unreachable, or a refusal worth retrying
    abstract Retry: unit -> unit
    // a fresh anonymous open that carries nothing over
    abstract OpenAnonymously: unit -> unit
    // the confirmation code and the chosen PIN, from the gate's form
    abstract SupplyPin: string -> string -> unit
    // the newest version the server told of, while the Session is on an older one
    abstract MovedOn: OrderPlanHead option
    // take up that version
    abstract OpenVersion: string -> unit


/// The signing phase of the open Session: its state, and the actions the dialog offers on it
[<Interface>]
type ISigning =
    abstract Signing: SigningMachine.Signing
    // ask a challenge over the plan as shown
    abstract Sign: OrderPlan -> unit
    // the data notice accepted
    abstract Accept: unit -> unit
    // the PIN; the idempotency key is minted here, once per confirmation
    abstract Confirm: string -> unit
    abstract Cancel: unit -> unit


/// Authentication state and commands
[<Interface>]
type IAuthentication =
    abstract IsAuthenticated: bool
    abstract Login: string -> unit
    abstract Logout: unit -> unit


/// Log analyzer data and commands
[<Interface>]
type ILogAnalyzer =
    abstract LogFiles: Deferred<LogFileInfo[]>
    abstract LogAnalysisReport: Deferred<string>
    abstract ListLogFiles: unit -> unit
    abstract AnalyzeLogFile: string -> unit


/// Bolus/emergency medication list (computed)
[<Interface>]
type IBolusMedication =
    abstract BolusMedication: Deferred<Intervention list>
    abstract OnSelectBolusMedicationItem: string -> unit
    abstract BolusMedicationFilter: string[]
    abstract OnBolusMedicationFilterChange: string[] -> unit


/// Continuous medication list (computed)
[<Interface>]
type IContinuousMedication =
    abstract ContinuousMedication: Deferred<Intervention list>
    abstract OnSelectContinuousMedicationItem: string -> unit
    abstract ContinuousMedicationFilter: string[]
    abstract OnContinuousMedicationFilterChange: string[] -> unit
