/// Decides when the dose dialog asks for an argumentation of a dose the rules mark.
module ArgumentationPolicy

open Shared.Types
open Shared.Models
open Shared.Api


/// Every order variable of an order: its own, the schedule's, the orderable's, and those of each
/// component and item, doses included.
let variables (ord: Order) : OrderVariable list =
    let dose (d: Dose) =
        [
            d.Quantity
            d.PerTime
            d.Rate
            d.Total
            d.QuantityAdjust
            d.PerTimeAdjust
            d.RateAdjust
            d.TotalAdjust
        ]

    let item (i: Item) =
        [
            i.ComponentQuantity
            i.OrderableQuantity
            i.ComponentConcentration
            i.OrderableConcentration
        ]
        @ dose i.Dose

    let comp (c: Component) =
        [
            c.ComponentQuantity
            c.OrderableQuantity
            c.OrderableCount
            c.OrderQuantity
            c.OrderCount
            c.OrderableConcentration
        ]
        @ dose c.Dose
        @ (c.Items |> Array.toList |> List.collect item)

    let orb = ord.Orderable

    [ ord.Adjust; ord.Duration; ord.Schedule.Frequency; ord.Schedule.Time ]
    @ [ orb.OrderableQuantity; orb.OrderQuantity; orb.OrderCount; orb.DoseCount ]
    @ dose orb.Dose
    @ (orb.Components |> Array.toList |> List.collect comp)


/// Whether the rules mark any variable of the order.
let marked (ord: Order) =
    variables ord
    |> List.exists (SeverityReasonPolicy.ofOrderVariable >> Option.isSome)


/// Whether the dialog asks for an argumentation: when the context is narrowed to one scenario
/// whose order the rules mark, or when a text is already written.
let wanted (ctx: OrderContext) =
    ctx.Argumentation.IsSome
    || (ctx |> OrderContext.contribution |> Option.exists (fun sc -> marked sc.Order))


/// The maximum length of a text, the same as the server's, so the server never refuses a text
/// the client holds.
let maxLength = OrderContext.Argumentation.maxLength


/// The text trimmed and cut to the maximum length; None when empty.
let normalise = OrderContext.Argumentation.normalise
