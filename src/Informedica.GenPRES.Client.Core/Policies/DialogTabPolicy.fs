namespace Informedica.GenPRES.Client.Core.Policies

/// Decides which component and item an order dialog shows. The choice changes no order, so the dialog
/// reuses it for the same order and takes it from the scenario when it opens or shows another order.
module DialogTabPolicy =

    open Informedica.GenPRES.Shared.Types


    /// The component and the item a dialog shows, for the order with this id.
    type Tab =
        {
            OrderId: string
            Component: string option
            Item: string option
        }


    /// Returns the tab for a scenario. The previous tab is reused for the same order while its component
    /// still exists; otherwise the scenario component and item are used. Without a chosen component the
    /// first is shown. The item is the chosen substance or the first one; an additional substance is never
    /// shown.
    let tab (kept: Tab option) (sc: OrderScenario) =
        let components = sc.Order.Orderable.Components

        let cmp, itm =
            match kept with
            | Some k when
                k.OrderId = sc.Order.Id
                && components |> Array.exists (fun c -> Some c.Name = k.Component)
                ->
                k.Component, k.Item
            | _ -> sc.Component, sc.Item

        let shown =
            components
            |> Array.tryFind (fun c -> cmp.IsNone || Some c.Name = cmp)
            |> Option.map (fun c ->
                let substances = c.Items |> Array.filter (_.IsAdditional >> not)

                let item =
                    substances
                    |> Array.tryFind (fun i -> Some i.Name = itm)
                    |> Option.orElse (substances |> Array.tryHead)
                    |> Option.map _.Name

                Some c.Name, item
            )
            |> Option.defaultValue (None, None)

        {
            OrderId = sc.Order.Id
            Component = fst shown
            Item = snd shown
        }
