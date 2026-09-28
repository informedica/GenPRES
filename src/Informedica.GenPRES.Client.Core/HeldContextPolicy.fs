/// Decides whether the patient context is held: the order plan has an order that is new or
/// changed since the version last opened or signed. While it is held, the patient data cannot
/// change, so every order a signed version adds rests on the same patient data.
module HeldContextPolicy

open Shared.Types


/// The ids of the order contexts that are new or changed since the version last opened or
/// signed. A removed context is in neither list, so a removal holds nothing.
let changed (opened: OrderContext[]) (plan: OrderPlan) =
    plan.OrderContexts
    |> Array.filter (fun ctx ->
        opened
        |> Array.tryFind (fun o -> o.Id = ctx.Id)
        |> Option.forall (fun o -> o <> ctx)
    )
    |> Array.map _.Id


/// Whether the order plan has a new or changed order.
let held (opened: OrderContext[]) (plan: OrderPlan) = changed opened plan |> Array.isEmpty |> not
