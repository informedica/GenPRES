namespace Informedica.GenPRES.Client.Core.Policies

/// Decides what the TPN intake slider shows, and when the nutrition slot sets the dose of a composed TPN
/// to 100% of the orderable.
module IntakePolicy =

    open Informedica.GenPRES.Shared.Types


    /// What the slot does with the order that arrived.
    [<RequireQualifiedAccess>]
    type Start =
        /// The TPN is not composed: forget the order the move was sent for.
        | Clear
        /// Send the move to 100%, and remember the order it was sent for.
        | Send of sentFor: (string * decimal option[])
        /// Nothing to send.
        | Wait


    /// Returns the value of an order variable that has a single value, else None.
    let singleValue (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.bind (fun vu ->
            match vu.Value with
            | [| _, d |] -> Some d
            | _ -> None
        )


    /// Returns the single value of each component orderable quantity of an order, or None per component.
    let composition (ord: Order) =
        ord.Orderable.Components |> Array.map (_.OrderableQuantity >> singleValue)


    /// Whether every component quantity of an order has a single value, meaning the composition is set.
    let isComposed (ord: Order) = ord |> composition |> Array.forall Option.isSome


    /// Returns the dose quantity as a percentage of the orderable quantity, when both have a single value
    /// and the orderable quantity is positive.
    let doseShare (ord: Order) =
        match ord.Orderable.Dose.Quantity |> singleValue, ord.Orderable.OrderableQuantity |> singleValue with
        | Some dose, Some total when total > 0m -> Some(dose / total * 100m)
        | _ -> None


    /// Returns the intake slider step for a share rounded to a whole percent, when it is a multiple of ten
    /// from 10 to 100.
    let sliderStep (share: decimal) =
        let pct = System.Math.Round share |> int

        if pct >= 10 && pct <= 100 && pct % 10 = 0 then
            Some pct
        else
            None


    /// Whether the intake slider can set the dose: the order is composed and its dose count has a single
    /// value.
    let canSetDoseQuantityPerc (ord: Order) =
        ord |> isComposed && ord.Orderable.DoseCount |> singleValue |> Option.isSome


    /// Whether the composition is locked because the dose is less than 100% of the orderable quantity.
    let isCompositionLocked (ord: Order) = ord |> doseShare |> Option.exists (fun share -> share < 100m)


    /// Decides what the slot does with an arriving TPN order, given the order and composition the move to
    /// 100% was last sent for. The move is sent once per order and composition: a move the server cannot
    /// make returns the order unchanged, and sending it again would loop.
    let start (sentFor: (string * decimal option[]) option) (ord: Order) =
        let key = ord.Id, ord |> composition

        if ord |> isComposed |> not then
            Start.Clear
        elif ord.Orderable.DoseCount |> singleValue |> Option.isSome || sentFor = Some key then
            Start.Wait
        else
            Start.Send key
