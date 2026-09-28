/// The argumentation a clinician writes when a dose leaves what the rules allow, on the client:
/// when the dialog asks for it, how a text is taken, and that the text is the client's own,
/// kept over every answer. Pure F#, no React, so it runs under Expecto; the two machines carry
/// the messages that write it, the dose dialog shows the field.
module ArgumentationPolicy

open Shared.Types
open Shared.Models
open Shared.Api


/// Every order variable of an order: the adjust and the duration, the schedule's two, the
/// orderable's four and its dose's eight, then each component's six and its dose's eight,
/// and each item's four and its dose's eight.
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


/// Whether the rules mark the order: any of its variables carries a severity reason.
let marked (ord: Order) =
    variables ord |> List.exists (SeverityReason.ofOrderVariable >> Option.isSome)


/// Whether the dialog asks for the argumentation: the context is narrowed to one scenario
/// whose order the rules mark, or a text is present, which stays once written until the
/// user clears it.
let wanted (ctx: OrderContext) =
    ctx.Argumentation.IsSome
    || (ctx |> OrderContext.contribution |> Option.exists (fun sc -> marked sc.Order))


/// The most characters a text keeps: the server's cap, the same number, so that a text the
/// client holds is never one the server refuses. The dialog's field carries it too.
let maxLength = 1000


/// The text as the client keeps it: trimmed, empty is none, and clipped at the cap.
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


/// The context with the text written, normalised.
let write (text: string) (ctx: OrderContext) = { ctx with Argumentation = normalise text }


/// The answered context with the argumentation as it was sent: the text is the client's own,
/// never the server's to change.
let keep (sent: OrderContext) (answered: OrderContext) = { answered with Argumentation = sent.Argumentation }


/// The plan with the text written on the context named; a plan without it is unchanged.
let writeIn (id: string) (text: string) (plan: OrderPlan) =
    { plan with OrderContexts = plan.OrderContexts |> Array.map (fun c -> if c.Id = id then write text c else c) }


/// The answered plan with the argumentation the client holds on every context it held; a
/// context the client did not hold keeps the answer's.
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


/// The one command that clears the text: a reset puts the order back within what the rules
/// allow, and the text argues the deviation it undoes. The text goes as the reset goes out, so
/// that the answer keeps what the client holds, as every answer does, and a text written while
/// the reset runs is kept.
let clearedBy (cmd: OrderContextCommand) =
    match cmd with
    | OrderContextCommand.ResetOrderScenario -> true
    | _ -> false


/// The context without its argumentation.
let clear (ctx: OrderContext) = { ctx with Argumentation = None }


/// The plan with the context named cleared of its argumentation; a plan without it unchanged.
let clearIn (id: string) (plan: OrderPlan) =
    { plan with OrderContexts = plan.OrderContexts |> Array.map (fun c -> if c.Id = id then clear c else c) }
