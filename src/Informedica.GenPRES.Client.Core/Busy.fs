/// What is out, and what it disables. The user can switch page, and use the title bar and the
/// patient panel, only while nothing is out; a page is disabled while a request out can change
/// anything on it.
module Busy

open Page


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
    /// A Session request: the launch, the resume, the PIN, the close, a refresh or an open.
    | Session
    /// A signature, from the sign click until it is answered or cancelled.
    | Signature
    /// A data load.
    | Load of Load


/// The requests out in the five lanes and the loads.
let out patient orderContext orderPlan session signing loads =
    [
        if PatientMachine.PatientState.changing patient then
            Request.Patient
        if (OrderContextMachine.OrderContextState.inFlightRequest orderContext).IsSome then
            Request.Workbench
        if (OrderPlanMachine.OrderPlanState.inFlightRequest orderPlan).IsSome then
            Request.Plan
        if
            SessionMachine.SessionState.inFlight session
            || (SessionMachine.SessionState.reopening session).IsSome
        then
            Request.Session
        if signing |> SigningMachine.SigningState.view |> SigningPolicy.underWay then
            Request.Signature
        yield! loads |> List.map Request.Load
    ]


/// Whether the request can change anything on the page.
let changes request page =
    match request, page with
    | Request.Patient, _
    | Request.Session, _
    | Request.Signature, _
    | Request.Load Load.Settings, _
    | Request.Load Load.Localization, _ -> true
    | Request.Workbench, (Page.Prescribe | Page.LifeSupport | Page.ContinuousMeds)
    | Request.Workbench, (Page.Formulary | Page.Parenteralia)
    | Request.Plan, (Page.OrderPlan | Page.Nutrition | Page.Interactions) -> true
    // the Settings page waits for what a reload starts: the seed over the workbench, or the
    // formulary and parenteralia loads without a patient
    | Request.Workbench, Page.Settings
    | Request.Load(Load.Formulary | Load.Parenteralia), Page.Settings -> true
    | Request.Load Load.BolusMedication, Page.LifeSupport
    | Request.Load Load.ContinuousMedication, Page.ContinuousMeds
    | Request.Load Load.Products, (Page.LifeSupport | Page.ContinuousMeds)
    | Request.Load Load.Formulary, Page.Formulary
    | Request.Load Load.Parenteralia, Page.Parenteralia
    | Request.Load Load.Interactions, Page.Interactions
    | Request.Load Load.DrugNames, Page.Interactions
    | Request.Load(Load.LogFiles | Load.LogAnalysis | Load.Reload), Page.Settings -> true
    | _ -> false


/// Whether anything is out that holds the menu, the title bar and the patient panel: every
/// request but the drug names.
let any out =
    out |> List.exists (fun request -> request <> Request.Load Load.DrugNames)


/// Whether the page is disabled: a request out can change anything on it.
let page p out = out |> List.exists (fun request -> changes request p)
