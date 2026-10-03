/// Decides what the TPN intake slider shows and when the nutrition slot moves the dose of a composed
/// TPN to the whole orderable by itself.
module IntakePolicy

open Shared.Types


/// What the slot does with the order that arrived.
[<RequireQualifiedAccess>]
type Start =
    /// The TPN is not composed: forget the order the move was sent for.
    | Clear
    /// Send the move to 100%, and remember the order it was sent for.
    | Send of sentFor: (string * decimal option[])
    /// Nothing to send.
    | Wait


/// The one value of an order variable, when it holds one.
let singleValue (ovar: OrderVariable) =
    ovar.Variable.Vals
    |> Option.bind (fun vu ->
        match vu.Value with
        | [| _, d |] -> Some d
        | _ -> None
    )


/// The value of each component orderable quantity of an order, when it holds one.
let composition (ord: Order) =
    ord.Orderable.Components |> Array.map (_.OrderableQuantity >> singleValue)


/// Whether every component quantity of an order holds one value: the user has set the composition.
let isComposed (ord: Order) = ord |> composition |> Array.forall Option.isSome


/// The share of the total the dose quantity is, in percent, when both hold one value.
let doseShare (ord: Order) =
    match ord.Orderable.Dose.Quantity |> singleValue, ord.Orderable.OrderableQuantity |> singleValue with
    | Some dose, Some total when total > 0m -> Some(dose / total * 100m)
    | _ -> None


/// The step of the intake slider a share is, to the nearest whole percent: a multiple of ten from 10
/// to 100.
let sliderStep (share: decimal) =
    let pct = System.Math.Round share |> int

    if pct >= 10 && pct <= 100 && pct % 10 = 0 then
        Some pct
    else
        None


/// Whether the intake slider can set the dose: the order is composed and its dose count holds one
/// value.
let canSetDoseQuantityPerc (ord: Order) =
    ord |> isComposed && ord.Orderable.DoseCount |> singleValue |> Option.isSome


/// Whether the composition is locked: the dose is a part of the total.
let isCompositionLocked (ord: Order) = ord |> doseShare |> Option.exists (fun share -> share < 100m)


/// What the slot does with a TPN order that arrived, given the order the move to 100% was last sent
/// for. A move the server cannot make returns the same order unchanged, so it is sent once per order
/// and composition, which stops a loop.
let start (sentFor: (string * decimal option[]) option) (ord: Order) =
    let key = ord.Id, ord |> composition

    if ord |> isComposed |> not then
        Start.Clear
    elif ord.Orderable.DoseCount |> singleValue |> Option.isSome || sentFor = Some key then
        Start.Wait
    else
        Start.Send key
