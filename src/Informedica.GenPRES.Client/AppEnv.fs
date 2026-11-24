module AppEnv

open Informedica.GenPRES.Client.Core.Helpers
open Informedica.GenPRES.Client.Core.Policies
open Informedica.GenPRES.Client.Core.StateMachines
open Informedica.GenPRES.Client.Core.MachinePolicies
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types


/// Casts the app environment to the requested interface.
let inline asEnv<'T> (appEnv: obj) : 'T = unbox<'T> appEnv


/// The localization terms.
[<Interface>]
type ILocalization =
    abstract LocalizationTerms: Deferred<string[][]>


/// The server settings loaded at start-up.
[<Interface>]
type ISettings =
    abstract Settings: Deferred<Api.ServerSettings>


/// The start-up state; the start-up screen covers the application until it has started.
[<Interface>]
type IStartup =
    abstract Startup: StartupPolicy.Startup


/// Which pending requests disable parts of the screen.
[<Interface>]
type IBusy =
    /// Whether a pending request disables the menu, the title bar and the patient panel.
    abstract Any: bool
    /// Whether a pending request can change the page.
    abstract Page: Page.Page -> bool


/// The workbench order context and its commands.
/// Mangled because one object implements this and IOrderPlan, which share member names;
/// without mangling Fable emits two members with the same name as one property.
[<Interface; Fable.Core.Mangle>]
type IOrderContext =
    /// The workbench as the pages show it.
    abstract OrderContext: OrderContextMachine.OrderContextView
    abstract OrderContextMsg: Api.OrderViewCommand -> unit
    /// Restores the workbench as it was before the recalculation, when the recalculation list closes
    /// without a selection.
    abstract RestoreField: unit -> unit
    /// The workbench as the order dialog shows it; None while no scenario is selected.
    abstract Dialog: OrderContextMachine.OrderContextView option
    /// Selects the scenario the dialog shows, by the id of its order, without a server request.
    abstract SelectScenario: string option -> unit
    /// The linked orders the link rules offer with the workbench order, each turned on or off.
    abstract Offers: OrderContextMachine.LinkedOffer[]
    /// Whether the workbench order can be prescribed with its linked orders; false while a linked order
    /// turned on is not narrowed to one scenario.
    abstract Prescribable: bool
    /// Turns the linked order at the index on or off.
    abstract TurnOffer: index: int * OrderContextMachine.OfferSwitch -> unit
    /// Sends a command to the linked order at the index.
    abstract OfferCommand: index: int * Api.OrderViewCommand -> unit


/// The order plan, nutrition included, and its commands.
/// Mangled because one object implements this and IOrderContext, which share member names.
[<Interface; Fable.Core.Mangle>]
type IOrderPlan =
    /// The order plan as the pages show it, with the context selected for the dialog.
    abstract OrderPlan: OrderPlanMachine.OrderPlanView
    /// Adds the workbench order with this id to the plan, with the linked orders that are turned on.
    abstract Add: orderId: string -> unit
    /// Removes the order contexts with these ids from the plan.
    abstract Remove: ids: string[] -> unit
    /// Sends a command from the order dialog to the plan context with this id.
    abstract OrderDialogCommand: string * Api.OrderViewCommand -> unit
    /// Restores the plan as it was before the recalculation, when the recalculation list closes
    /// without a selection.
    abstract RestoreField: unit -> unit
    /// Selects the context the dialog shows, by id, without a server request.
    abstract SelectContext: string option -> unit
    /// Shows only the rows of the contexts with these ids; the totals are calculated for those rows.
    abstract FilterRows: string[] -> unit
    /// Whether the orders were calculated for other patient data than the panel shows; such a plan
    /// cannot be signed.
    abstract PlanBehind: bool
    /// Returns the order contexts of the plan that are new, changed or removed since the version last
    /// opened or signed.
    abstract PlanDifferences: OrderPlan -> (OrderContext * PlanDifferencePolicy.Difference)[]


/// The nutrition slots outside the plan, the combined totals, and their commands.
/// Mangled because one object implements this and the other interfaces, which share member names.
[<Interface; Fable.Core.Mangle>]
type INutrition =
    /// The nutrition slots in the order they were started.
    abstract Slots: Informedica.GenPRES.Client.Core.Client.SlotView list
    /// The combined totals of the plan and the slots when loaded, otherwise the plan totals.
    abstract Totals: Totals option
    /// Starts a nutrition slot of the category.
    abstract StartSlot: NutritionCategory -> unit
    /// Removes the slot with this id.
    abstract RemoveSlot: slotId: string -> unit
    /// Sends a command to the slot with this id.
    abstract SlotCommand: slotId: string * Api.OrderViewCommand -> unit
    /// Restores the slot with this id as it was before the recalculation, when the recalculation list
    /// closes without a selection.
    abstract SlotRestore: slotId: string -> unit
    /// Adds the order with this id from the slot with this id to the plan, with the linked orders that
    /// are turned on.
    abstract Prescribe: slotId: string * orderId: string -> unit
    /// Turns the linked order at the index in the slot with this id on or off.
    abstract TurnOffer: slotId: string * index: int * OrderContextMachine.OfferSwitch -> unit
    /// Sends a command to the linked order at the index in the slot with this id.
    abstract OfferCommand: slotId: string * index: int * Api.OrderViewCommand -> unit


/// The patient data and their updates.
[<Interface>]
type IPatient =
    /// The patient data the panel shows and edits; the lists are calculated from these data.
    abstract Draft: PatientData option
    /// Whether a patient data update is in progress; the panel takes no edit until it is answered.
    abstract Changing: bool
    /// The patient data from the server answer, with the estimated weight and height also when a
    /// measured one is present; otherwise the draft. The summary shows these data, the fields show
    /// the draft.
    abstract Current: PatientData option
    /// Clears the patient data, as the reset of the panel does.
    abstract ClearPatient: unit -> unit
    /// Sends an edit of the patient data the panel shows; the server applies it and completes the result.
    abstract EditPatient: Api.PatientDataEdit -> unit
    /// Whether an edit on the panel counts as another patient, so the prescriber is asked first
    /// because orders or a selection would be lost.
    abstract AsksNewPatient: bool
    /// Clears the order plan and the workbench, and sends the edit for the patient data the panel showed.
    abstract NewPatient: Api.PatientDataEdit -> unit


/// The formulary and its updates.
[<Interface>]
type IFormulary =
    abstract Formulary: Deferred<Formulary>
    abstract UpdateFormulary: Formulary -> unit


/// The parenteralia and their updates.
[<Interface>]
type IParenteralia =
    abstract Parenteralia: Deferred<Parenteralia>
    abstract UpdateParenteralia: Parenteralia -> unit


/// The medication interactions and their commands.
[<Interface>]
type IInteractions =
    abstract Interactions: Deferred<DrugInteraction[]>
    abstract InteractionDrugNames: Deferred<string[]>
    abstract CheckInteractions: string list -> unit


/// Reloads the server resources with the admin token from the login, so no password is needed.
[<Interface>]
type IResources =
    /// InProgress while the server reloads; Resolved when the server has answered.
    abstract Reload: Deferred<unit>
    abstract ReloadResources: unit -> unit


/// The launch Session as the pages show it, and the actions the UI offers on it.
[<Interface>]
type ISession =
    abstract Session: SessionMachine.SessionView
    /// Closes the open Session.
    abstract Close: unit -> unit
    /// Presents the same launch again, after the server was unreachable or refused with a retry.
    abstract RetryLaunch: unit -> unit
    /// Continues anonymously, without a Session and without any data from it.
    abstract OpenAnonymously: unit -> unit
    /// Sends the confirmation code and the chosen PIN from the enrolment form.
    abstract SupplyPin: string -> string -> unit
    /// A newer signed plan version than the one the Session has open, if the server reported one.
    abstract NewerPlan: OrderPlanHead option
    /// Opens the signed plan version with this id.
    abstract OpenSignedPlan: string -> unit
    /// Reads the patient data again from the EHR; the plan and the workbench update with them.
    abstract Refresh: unit -> unit


/// The signing of the open Session as the dialog shows it, and the actions the dialog offers on it.
[<Interface>]
type ISigning =
    abstract Signing: SigningMachine.SigningView
    /// The orders that differ from the version last opened or signed, as they were when the signature
    /// was requested; the dialog lists these.
    abstract Differences: (OrderContext * PlanDifferencePolicy.Difference)[]
    /// Requests a signing challenge for the plan as shown.
    abstract Sign: OrderPlan -> unit
    /// Whether the patient was no longer active in the EHR at the challenge; shown before the PIN is asked.
    abstract EhrMovedOn: bool
    /// The identity of the patient being signed for; None when the EHR did not identify the patient.
    abstract SignedFor: PatientIdentity option
    /// Answers the data notice: takes the EHR patient data, or keeps the patient data of the plan.
    abstract TakeEhrData: unit -> unit
    abstract KeepPlanData: unit -> unit
    /// Confirms the signature with the PIN; each confirmation gets a new idempotency key.
    abstract Confirm: string -> unit
    abstract Cancel: unit -> unit


/// The admin authentication state and its commands.
[<Interface>]
type IAuthentication =
    abstract IsAuthenticated: bool
    abstract Login: string -> unit
    abstract Logout: unit -> unit


/// The log files, the log analysis and their commands.
[<Interface>]
type ILogAnalyzer =
    abstract LogFiles: Deferred<LogFileInfo[]>
    abstract LogAnalysisReport: Deferred<string>
    abstract ListLogFiles: unit -> unit
    abstract AnalyzeLogFile: string -> unit


/// The rows of the emergency list, calculated by the server for the patient data.
[<Interface>]
type IEmergencyList =
    abstract EmergencyList: Deferred<Intervention[]>
    abstract EmergencyListFilter: string[]
    abstract OnEmergencyListFilterChange: string[] -> unit


/// The continuous medication list, calculated for the patient data.
[<Interface>]
type IContinuousMedication =
    abstract ContinuousMedication: Deferred<Intervention list>
    abstract OnSelectContinuousMedicationItem: string -> unit
    abstract ContinuousMedicationFilter: string[]
    abstract OnContinuousMedicationFilterChange: string[] -> unit
