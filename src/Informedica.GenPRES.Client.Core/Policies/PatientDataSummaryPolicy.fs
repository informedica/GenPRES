namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what the prescriber is told after a patient data update: how many orders were updated, and
/// how many of them need attention.
module PatientDataSummaryPolicy =

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open Informedica.GenPRES.Shared.Models
    open Informedica.GenPRES.Client.Core.Helpers
    open Informedica.GenPRES.Client.Core.Helpers.Alert


    /// Whether the order has a general notice, or a value the rules mark.
    let isMarked (sc: OrderScenario) =
        sc.RulesNoLongerApply
        || sc.NotRecalculated
        || sc.Order |> ArgumentationPolicy.marked


    /// Returns the summary for the plan returned after a patient data update, when the plan has orders;
    /// None otherwise.
    let summaryOf (sent: OrderPlanCommand) (tp: OrderPlan) =
        match sent with
        | OrderPlanCommand.UpdatePatientData _ ->
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
