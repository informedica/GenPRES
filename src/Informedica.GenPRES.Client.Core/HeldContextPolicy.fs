/// Whether the patient context is held: the order plan has an order that is new or changed
/// since the order plan version last opened or signed. While it is held the patient data cannot
/// be changed, so that every order a signed version adds rests on one patient context.
module HeldContextPolicy

open Shared.Types


/// The ids of the contexts of the order plan that the version last opened or signed does not
/// hold as it is: new, or changed since. A context of that version removed from the order plan
/// is in neither, so a removal holds nothing.
let changed (opened: OrderContext[]) (plan: OrderPlan) =
    plan.OrderContexts
    |> Array.filter (fun ctx ->
        opened
        |> Array.tryFind (fun o -> o.Id = ctx.Id)
        |> Option.forall (fun o -> o <> ctx)
    )
    |> Array.map _.Id


/// Whether the patient context is held: the order plan has a new or changed order.
let held (opened: OrderContext[]) (plan: OrderPlan) = changed opened plan |> Array.isEmpty |> not
