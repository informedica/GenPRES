namespace Informedica.GenPRES.Client.Core.Policies

/// Decides which component and which item an order dialog shows. The choice is the dialog's own:
/// it changes no order, so the dialog keeps it while it shows the same order, and takes it from
/// the scenario only when it opens or shows another order.
module DialogTabPolicy =

    open Informedica.GenPRES.Shared.Types


    /// The component and the item a dialog shows, for the order with this id.
    type Tab =
        {
            OrderId: string
            Component: string option
            Item: string option
        }


    /// The tab for the scenario: the one kept, for the same order while its component is still
    /// there; otherwise the scenario's own. The component is the one chosen or the first, the item
    /// the one chosen among the component's substances or its first; an additional substance is
    /// never shown.
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
