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


/// The requests out: a field counting its step clicks, the five lanes and the loads.
let out counting patient orderContext orderPlan session signing loads =
    [
        if counting then
            Request.Counting
        if PatientMachine.PatientState.changing patient then
            Request.Patient
        if (OrderContextMachine.OrderContextState.inFlightRequest orderContext).IsSome then
            Request.Workbench
        if (OrderPlanMachine.OrderPlanState.inFlightRequest orderPlan).IsSome then
            Request.Plan
        if
            SessionMachine.SessionState.changing session
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
    // the fields of the Nutrition page are on the page itself, so the field counting would be
    // disabled with it; there the other fields wait for the count themselves
    | Request.Counting, Page.Nutrition -> false
    | Request.Counting, _ -> true
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
