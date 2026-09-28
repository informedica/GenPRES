/// Decides when the dose dialog asks for an argumentation of a dose the rules mark, and keeps
/// the text the clinician writes unchanged by server answers.
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
let maxLength = 1000


/// The text trimmed and cut to the maximum length; None when empty.
let normalise (text: string) =
    match text with
    | null -> None
    | text ->
        let text = text.Trim()

        if text = "" then
            None
        elif text.Length > maxLength then
            Some(text.Substring(0, maxLength))
        else
            Some text


/// The context with the text written.
let write (text: string) (ctx: OrderContext) = { ctx with Argumentation = normalise text }


/// The answered context with the argumentation as sent: the server never changes the text.
let keep (sent: OrderContext) (answered: OrderContext) = { answered with Argumentation = sent.Argumentation }


/// The plan with the text written on the context with this id.
let writeIn (id: string) (text: string) (plan: OrderPlan) =
    { plan with OrderContexts = plan.OrderContexts |> Array.map (fun c -> if c.Id = id then write text c else c) }


/// The answered plan with the client's argumentation on every context it held; other contexts
/// keep the answer's.
let keepAll (held: OrderPlan) (answered: OrderPlan) =
    { answered with
        OrderContexts =
            answered.OrderContexts
            |> Array.map (fun c ->
                held.OrderContexts
                |> Array.tryFind (fun h -> h.Id = c.Id)
                |> Option.map (fun h -> keep h c)
                |> Option.defaultValue c
            )
    }


/// Whether the command clears the text: only a reset does, since it puts the order back within
/// the rules. The text is cleared when the reset is sent, so a text written while it runs is
/// kept.
let clearedBy (cmd: OrderContextCommand) =
    match cmd with
    | OrderContextCommand.ResetOrderScenario -> true
    | _ -> false


/// The context without its argumentation.
let clear (ctx: OrderContext) = { ctx with Argumentation = None }


/// The plan with the argumentation cleared from the context with this id.
let clearIn (id: string) (plan: OrderPlan) =
    { plan with OrderContexts = plan.OrderContexts |> Array.map (fun c -> if c.Id = id then clear c else c) }
