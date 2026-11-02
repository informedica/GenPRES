namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what the prescriber is told at a change of patient data: how many orders followed, and
/// how many of them ask for attention.
module PatientDataSummaryPolicy =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Helpers.Alert


    /// Whether the order has the general notice, or a value the rules mark.
    let isMarked (sc: OrderScenario) = sc.RulesNoLongerApply || sc.Order |> ArgumentationPolicy.marked


    /// The summary for the plan answered to a request: only for an update of the patient of a plan
    /// with orders, since without orders there is nothing to report.
    let summaryOf (sent: OrderPlanCommand) (tp: OrderPlan) =
        match sent with
        | OrderPlanCommand.UpdatePatient _ ->
            match OrderPlan.orders tp with
            | [||] -> None
            | orders ->
                Some
                    {
                        Orders = orders.Length
                        Marked = orders |> Array.filter isMarked |> Array.length
                    }
        | _ -> None


    /// The sentence when the localization has no text: {0} is the number of orders, {1} the number
    /// of orders with a notification.
    let english =
        "Patient data changed: {0} order(s) updated, {1} with a notification. The selection for "
        + "prescribing was cleared."


    /// The sentence of the summary, from the text of the term.
    let sentence (summary: PatientDataSummary) text =
        text |> TermText.fill [ string summary.Orders; string summary.Marked ]
