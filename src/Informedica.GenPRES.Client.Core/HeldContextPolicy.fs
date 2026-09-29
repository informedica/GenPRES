/// Decides whether the patient context is held: the order plan has an order that is new or
/// changed since the version last opened or signed. While it is held, the patient data cannot
/// change, so every order a signed version adds rests on the same patient data. Also lists the
/// differences against that version, new, changed and removed, as the sign dialog shows them.
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


/// How an order context differs from the version last opened or signed.
[<RequireQualifiedAccess>]
type Difference =
    /// The version did not hold it.
    | New
    /// Its editable variables or its argumentation changed.
    | Changed
    /// The version held it, and the plan no longer does.
    | Removed


/// The order contexts new or changed since the version last opened or signed, in plan order,
/// followed by the contexts of that version no longer in the plan. Contexts are compared by
/// id. A context that contributes no order, a nutrition context not yet narrowed to one
/// scenario, has nothing to list and is left out, on either side.
let differences (opened: OrderContext[]) (plan: OrderPlan) =
    let contributing (ctxs: OrderContext[]) =
        ctxs |> Array.filter (Shared.Models.OrderContext.contribution >> Option.isSome)

    let byId id (ctxs: OrderContext[]) = ctxs |> Array.tryFind (fun c -> c.Id = id)

    let opened = contributing opened
    let now = contributing plan.OrderContexts

    let current =
        now
        |> Array.choose (fun ctx ->
            match opened |> byId ctx.Id with
            | None -> Some(ctx, Difference.New)
            | Some o when PlanContextPolicy.changed o ctx -> Some(ctx, Difference.Changed)
            | Some _ -> None
        )

    let removed =
        opened
        |> Array.filter (fun o -> now |> byId o.Id |> Option.isNone)
        |> Array.map (fun o -> o, Difference.Removed)

    Array.append current removed
