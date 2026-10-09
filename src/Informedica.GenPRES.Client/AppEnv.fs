module AppEnv

open Shared
open Shared.Types


/// Downcast appEnv to the requested interface.
let inline asEnv<'T> (appEnv: obj) : 'T = unbox<'T> appEnv


/// Localization data access
[<Interface>]
type ILocalization =
    abstract LocalizationTerms: Deferred<string[][]>


/// What the server told about itself at start-up.
[<Interface>]
type ISettings =
    abstract Settings: Deferred<Api.ServerSettings>


/// Where the start-up is: the gate covers the application until it has started.
[<Interface>]
type IStartup =
    abstract Startup: StartupPolicy.Startup


/// What is out, and what it disables.
[<Interface>]
type IBusy =
    // anything out that holds the menu, the title bar and the patient panel
    abstract Any: bool
    // whether a request out can change anything on the page
    abstract Page: Page.Page -> bool


/// Order context data and commands.
/// Mangled: the app state implements this and IOrderPlan on one object, and both have a Select;
/// without mangling Fable emits the two as one property.
[<Interface; Fable.Core.Mangle>]
type IOrderContext =
    // the workbench as the pages show it
    abstract OrderContext: OrderContextMachine.OrderContextView
    abstract OrderContextMsg: Api.OrderViewCommand -> unit
    // a clear from the dialog that opens the field's list, the workbench before it kept
    abstract ReopenField: Api.OrderViewCommand -> unit
    // the list of a reopen closed without a pick: the workbench kept is put back
    abstract RestoreField: unit -> unit
    // the workbench as the order dialog shows it; none while no scenario is selected
    abstract Dialog: OrderContextMachine.OrderContextView option
    // the scenario whose order the dialog shows, by its order's id; the client's own, no round trip
    abstract SelectScenario: string option -> unit


/// The one plan, nutrition included, and the commands on it.
/// Mangled, as IOrderContext is: the two share member names on one object.
[<Interface; Fable.Core.Mangle>]
type IOrderPlan =
    // the plan as the pages show it, the dialog's selection inside
    abstract OrderPlan: OrderPlanMachine.OrderPlanView
    // the order with this id prescribed, from the workbench narrowed to its scenario
    abstract Add: orderId: string -> unit
    // a new nutrition context for the category
    abstract NewNutrition: NutritionCategory -> unit
    // the contexts with these ids removed
    abstract Remove: ids: string[] -> unit
    // a command from the order dialog into the plan's context with this id, sent over the plan held
    abstract OrderDialogCommand: string * Api.OrderViewCommand -> unit
    // a clear from the dialog that opens the field's list, the plan before it kept
    abstract ReopenField: string * Api.OrderViewCommand -> unit
    // the list of a reopen closed without a pick: the plan kept is put back, as signed or changed
    abstract RestoreField: unit -> unit
    // the context whose order the dialog shows, by id; the client's own, no round trip
    abstract SelectContext: string option -> unit
    // the contexts the rows keep, by id; the totals follow
    abstract FilterRows: string[] -> unit
    // the contexts of the plan that are new or changed since the version last opened or
    // signed, by id; while there are any in an open Session the patient context is held
    abstract Changed: string[]


/// Patient data and updates
[<Interface>]
type IPatient =
    // the patient data as the panel edits it and the lists read it, with the estimates the
    // server answered after an edit that renews them
    abstract Draft: Patient option
    // a patient change is under way: the panel takes no edit until it is answered
    abstract Changing: bool
    // the draft with the estimates of its age, the gender and the gestational age applied to
    // every weight and height, cleared or entered as well: what the summary shows, not what
    // the fields show
    abstract Estimated: Patient option
    // the draft, with the weight and height the user did not enter estimated again by the
    // server: after an edit of the age, the gender or the gestational age
    abstract UpdatePatient: Patient option -> unit
    // the draft with its estimates as they are: after any other edit, so that a weight or a
    // height the user cleared stays cleared
    abstract EditPatient: Patient option -> unit


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


/// The launch Session as the pages show it, and the actions the UI offers on it
[<Interface>]
type ISession =
    abstract Session: SessionMachine.SessionView
    // explicit close, from an open Session
    abstract Close: unit -> unit
    // from ServerUnreachable, or a refusal worth retrying
    abstract RetryLaunch: unit -> unit
    // a fresh anonymous open that carries nothing over
    abstract OpenAnonymously: unit -> unit
    // the confirmation code and the chosen PIN, from the gate's form
    abstract SupplyPin: string -> string -> unit
    // the newest version the server told of, while the Session is on an older one
    abstract NewerPlan: OrderPlanHead option
    // take up that version
    abstract OpenSignedPlan: string -> unit
    // read the patient from the EHR again; the plan and the workbench follow it
    abstract Refresh: unit -> unit


/// The signing phase of the open Session as the dialog shows it, and the actions the dialog
/// offers on it
[<Interface>]
type ISigning =
    abstract Signing: SigningMachine.SigningView
    // the orders that differ from the version last opened or signed, as they stood at the sign;
    // the dialog lists these
    abstract Differences: (OrderContext * HeldContextPolicy.Difference)[]
    // ask a challenge over the plan as shown
    abstract Sign: OrderPlan -> unit
    // whether the patient context is held: a notice accepted then signs over the data as it was
    abstract Held: bool
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
