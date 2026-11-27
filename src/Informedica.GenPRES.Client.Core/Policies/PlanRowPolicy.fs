namespace Informedica.GenPRES.Client.Core.Policies

/// Decides which rows of the order plan table are checked when the user checks or unchecks one. A main
/// order and its linked orders are checked together, so the checked rows are the rows the totals count
/// and the rows a removal removes.
module PlanRowPolicy =

    open Informedica.GenPRES.Shared.Types


    /// The ids of the contexts of the plan linked to the main order with this id.
    let linkedIds plan main = PlanContextPolicy.linkedTo plan main |> Array.map _.Id


    /// Returns the ids of the checked contexts in plan order, for the rows the table asks for and the rows
    /// the plan has checked. Unchecking a main order unchecks its linked orders; a checked main order
    /// checks its linked orders. A linked order can be checked without its main order. An empty result
    /// shows no checked row, although the totals then count every order.
    let checkedRows (plan: OrderPlan) (rows: string[]) =
        let asked = set rows
        let unchecked = Set.difference (set plan.Filtered) asked
        let dropped = unchecked |> Seq.collect (linkedIds plan) |> set
        let kept = Set.difference asked dropped
        let added = kept |> Seq.collect (linkedIds plan) |> set
        let checkedNow = Set.union kept added

        plan.OrderContexts |> Array.map _.Id |> Array.filter checkedNow.Contains
