namespace Informedica.GenPRES.Client.Core.Policies

/// Lists the new, changed and removed order contexts since the version last opened or signed, for the
/// sign dialog.
module PlanDifferencePolicy =

    open Informedica.GenPRES.Shared.Types


    /// How an order context differs from the version last opened or signed.
    [<RequireQualifiedAccess>]
    type Difference =
        /// The context is not in that version.
        | New
        /// Its order, an editable variable, a dose per kg or per m2, a notification or the argumentation
        /// changed.
        | Changed
        /// The context is in that version but no longer in the plan.
        | Removed


    /// Returns the order contexts that are new or changed since the version last opened or signed, in plan
    /// order, followed by the contexts of that version that are no longer in the plan. Contexts are compared
    /// by id. A context without an order, such as a nutrition context with more than one scenario, is left
    /// out on both sides.
    let differences (opened: OrderContext[]) (plan: OrderPlan) =
        let contributing (ctxs: OrderContext[]) =
            ctxs
            |> Array.filter (Informedica.GenPRES.Shared.Models.OrderContext.contribution >> Option.isSome)

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
