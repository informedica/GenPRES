namespace Informedica.GenPRES.Client.Core.Policies

/// Decides which rows of the order plan table are checked when the user checks or unchecks one: a main
/// order and the orders linked to it are checked together, so that the rows checked are the rows the
/// totals count and the rows a removal takes.
module PlanRowPolicy =

    open Informedica.GenPRES.Shared.Types


    /// The ids of the contexts of the plan linked to the main order with this id.
    let linkedIds plan main = PlanContextPolicy.linkedTo plan main |> Array.map _.Id


    /// The contexts checked, by id in the order of the plan, when the table asks for the rows given over
    /// the rows the plan has checked. Unchecking a main order unchecks the orders linked to it; a main
    /// order checked keeps the orders linked to it checked, so checking it checks them too. A linked
    /// order can be checked alone while its main order is not checked. An empty filter is no row checked,
    /// as the table shows it, although the totals then count every order.
    let checkedRows (plan: OrderPlan) (rows: string[]) =
        let asked = set rows
        let unchecked = Set.difference (set plan.Filtered) asked
        let dropped = unchecked |> Seq.collect (linkedIds plan) |> set
        let kept = Set.difference asked dropped
        let added = kept |> Seq.collect (linkedIds plan) |> set
        let checkedNow = Set.union kept added

        plan.OrderContexts |> Array.map _.Id |> Array.filter checkedNow.Contains
