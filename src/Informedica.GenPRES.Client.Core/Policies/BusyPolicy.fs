namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what the pending requests disable. The menu, the title bar and the patient panel work only
/// while no request is pending. A page is disabled while a pending request can change it.
module BusyPolicy =

    open Informedica.GenPRES.Client.Core.Helpers
    open Page
    open Loads


    /// Whether the request can change anything on the page.
    let changes request page =
        match request, page with
        | Request.Patient, _
        | Request.PlanBehind, _
        | Request.Session, _
        | Request.Signature, _
        | Request.Load Load.Settings, _
        | Request.Load Load.Localization, _ -> true
        // The counted field is on the Nutrition page, so disabling the page would disable that field.
        // The other fields on that page wait for the count themselves.
        | Request.Counting, Page.Nutrition -> false
        | Request.Counting, _ -> true
        | Request.Workbench, (Page.Prescribe | Page.LifeSupport | Page.ContinuousMeds)
        | Request.Workbench, (Page.Formulary | Page.Parenteralia)
        // A plan request does not disable the Nutrition page: only the prescribed slot waits for it.
        | Request.Plan, (Page.OrderPlan | Page.Interactions) -> true
        | Request.Slots, Page.Nutrition -> true
        // The Settings page waits for the requests a reload starts: the workbench request, or the
        // formulary and parenteralia loads when there is no patient.
        | Request.Workbench, Page.Settings
        | Request.Load(Load.Formulary | Load.Parenteralia), Page.Settings -> true
        | Request.Load Load.EmergencyList, Page.LifeSupport
        | Request.Load Load.ContinuousList, Page.ContinuousMeds
        | Request.Load Load.Products, (Page.LifeSupport | Page.ContinuousMeds)
        | Request.Load Load.Formulary, Page.Formulary
        | Request.Load Load.Parenteralia, Page.Parenteralia
        | Request.Load Load.Interactions, Page.Interactions
        | Request.Load Load.DrugNames, Page.Interactions
        | Request.Load(Load.LogFiles | Load.LogAnalysis | Load.Reload), Page.Settings -> true
        | _ -> false


    /// Whether a pending request disables the menu, the title bar and the patient panel. Every request
    /// does, except the medication names, the totals and the two list loads, which start without a user action.
    let any out =
        out
        |> List.exists (fun request ->
            match request with
            | Request.Load(Load.DrugNames | Load.Totals | Load.EmergencyList | Load.ContinuousList) -> false
            | _ -> true
        )


    /// Whether the page is disabled because a pending request can change it.
    let page p out = out |> List.exists (fun request -> changes request p)
