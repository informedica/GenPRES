module AppEnv

open Informedica.GenPRES.Client.Core.Helpers
open Informedica.GenPRES.Client.Core.Policies
open Informedica.GenPRES.Client.Core.StateMachines
open Informedica.GenPRES.Client.Core.MachinePolicies
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types


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
    /// anything out that holds the menu, the title bar and the patient panel
    abstract Any: bool
    /// whether a request out can change anything on the page
    abstract Page: Page.Page -> bool


/// Order context data and commands.
/// Mangled: the app state implements this and IOrderPlan on one object, and both have a Select;
/// without mangling Fable emits the two as one property.
[<Interface; Fable.Core.Mangle>]
type IOrderContext =
    /// the workbench as the pages show it
    abstract OrderContext: OrderContextMachine.OrderContextView
    abstract OrderContextMsg: Api.OrderViewCommand -> unit
    /// the list of a recalculation closed without a pick: the workbench kept is put back
    abstract RestoreField: unit -> unit
    /// the workbench as the order dialog shows it; none while no scenario is selected
    abstract Dialog: OrderContextMachine.OrderContextView option
    /// the scenario whose order the dialog shows, by its order's id; the client's own, no round trip
    abstract SelectScenario: string option -> unit
    /// the orders the link rules offer with the order of the workbench, each turned on or off
    abstract Offers: OrderContextMachine.LinkedOffer[]
    /// whether the workbench can be prescribed with its offers: not while an offer turned on is not narrowed
    abstract Prescribable: bool
    /// the offer at the index turned on or off
    abstract TurnOffer: index: int * OrderContextMachine.OfferSwitch -> unit
    /// a command into the offer at the index
    abstract OfferCommand: index: int * Api.OrderViewCommand -> unit


/// The one plan, nutrition included, and the commands on it.
/// Mangled, as IOrderContext is: the two share member names on one object.
[<Interface; Fable.Core.Mangle>]
type IOrderPlan =
    /// the plan as the pages show it, the dialog's selection inside
    abstract OrderPlan: OrderPlanMachine.OrderPlanView
    /// the order with this id prescribed, from the workbench narrowed to its scenario
    abstract Add: orderId: string -> unit
    /// the contexts with these ids removed
    abstract Remove: ids: string[] -> unit
    /// a command from the order dialog into the plan's context with this id, sent with the current plan
    abstract OrderDialogCommand: string * Api.OrderViewCommand -> unit
    /// the list of a recalculation closed without a pick: the plan kept is put back, as signed or
    /// changed
    abstract RestoreField: unit -> unit
    /// the context whose order the dialog shows, by id; the client's own, no round trip
    abstract SelectContext: string option -> unit
    /// the contexts the rows keep, by id; the totals follow
    abstract FilterRows: string[] -> unit
    /// whether the orders stand on other patient data than the panel; such a plan is not signed
    abstract PlanBehind: bool


/// The nutrition slots outside the plan, the combined totals, and the commands on them.
/// Mangled, as IOrderContext is: the members share an object with the other interfaces.
[<Interface; Fable.Core.Mangle>]
type INutrition =
    /// the slots in the order they were started
    abstract Slots: Informedica.GenPRES.Client.Core.Client.SlotView list
    /// the totals of the plan with the slots narrowed to one order, the plan's own while none is
    abstract Totals: Totals option
    /// a slot of the category started
    abstract StartSlot: NutritionCategory -> unit
    /// the slot with this id closed
    abstract RemoveSlot: slotId: string -> unit
    /// a command into the slot with this id
    abstract SlotCommand: slotId: string * Api.OrderViewCommand -> unit
    /// the list of a recalculation in the slot with this id closed without a pick
    abstract SlotRestore: slotId: string -> unit
    /// the slot with this id narrowed to the order with this id into the plan, with its offers turned on
    abstract Prescribe: slotId: string * orderId: string -> unit
    /// the offer at the index in the slot with this id turned on or off
    abstract TurnOffer: slotId: string * index: int * OrderContextMachine.OfferSwitch -> unit
    /// a command into the offer at the index in the slot with this id
    abstract OfferCommand: slotId: string * index: int * Api.OrderViewCommand -> unit


/// Patient data and updates
[<Interface>]
type IPatient =
    /// the patient data as the panel edits it and the lists read it, with the estimates the
    /// server answered after an edit that renews them
    abstract Draft: Patient option
    /// a patient change is under way: the panel takes no edit until it is answered
    abstract Changing: bool
    /// the draft with the estimates of its age, the gender and the gestational age applied to
    /// every weight and height, cleared or entered as well: what the summary shows, not what
    /// the fields show
    abstract Estimated: Patient option
    /// the draft, with the weight and height the user did not enter estimated again by the
    /// server: after an edit of the age, the gender or the gestational age
    abstract UpdatePatient: Patient option -> unit
    /// the draft with its estimates as they are: after any other edit, so that a weight or a
    /// height the user cleared stays cleared
    abstract EditPatient: Patient option -> unit
    /// whether an edit asks first, since it is taken as a new patient and orders or a selection
    /// would be lost
    abstract AsksNewPatient: bool
    /// a new patient, with what becomes of the estimates; the plan and the workbench go
    abstract NewPatient: Patient option * PatientDraftPolicy.Estimates -> unit


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
    /// InProgress while the server reloads; Resolved once it answered
    abstract Reload: Deferred<unit>
    abstract ReloadResources: unit -> unit


/// The launch Session as the pages show it, and the actions the UI offers on it
[<Interface>]
type ISession =
    abstract Session: SessionMachine.SessionView
    /// explicit close, from an open Session
    abstract Close: unit -> unit
    /// from ServerUnreachable, or a refusal worth retrying
    abstract RetryLaunch: unit -> unit
    /// a fresh anonymous open that carries nothing over
    abstract OpenAnonymously: unit -> unit
    /// the confirmation code and the chosen PIN, from the gate's form
    abstract SupplyPin: string -> string -> unit
    /// the newest version the server told of, while the Session is on an older one
    abstract NewerPlan: OrderPlanHead option
    /// take up that version
    abstract OpenSignedPlan: string -> unit
    /// read the patient from the EHR again; the plan and the workbench follow it
    abstract Refresh: unit -> unit


/// The signing phase of the open Session as the dialog shows it, and the actions the dialog
/// offers on it
[<Interface>]
type ISigning =
    abstract Signing: SigningMachine.SigningView
    /// the orders that differ from the version last opened or signed, as they stood at the sign;
    /// the dialog lists these
    abstract Differences: (OrderContext * PlanDifferencePolicy.Difference)[]
    /// ask a challenge over the plan as shown
    abstract Sign: OrderPlan -> unit
    /// whether the patient was no longer active in the EHR at the challenge; told before the PIN
    abstract EhrMovedOn: bool
    /// who the patient being signed for is; none for a patient the EHR did not identify
    abstract SignedFor: NameAndBirthDate option
    /// the data notice answered: the EHR's data taken, or the plan's kept
    abstract TakeEhrData: unit -> unit
    abstract KeepPlanData: unit -> unit
    /// the PIN; the idempotency key is minted here, once per confirmation
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
