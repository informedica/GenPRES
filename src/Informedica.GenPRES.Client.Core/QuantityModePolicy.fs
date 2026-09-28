/// How the user may move the value of a quantity field, decided by one rule for every order
/// variable, so the order and the nutrition views decide the same way.
module QuantityModePolicy

open Shared.Types
open Shared.Models


/// How the user may move the value of a quantity field.
[<RequireQualifiedAccess>]
type Mode =
    /// Several values allowed: the value is chosen from the dropdown, without step buttons.
    | Selectable
    /// A range allowed: the steps narrow it, first and last jump to the min and the max.
    | Navigable
    /// One value: the steps move it, first and last make a large step.
    | Stepable
    /// One value or a range the user cannot move from this field.
    | Fixed


/// The order variables a view shows as a quantity field.
[<RequireQualifiedAccess>]
type Field =
    /// The orderable quantity of a component.
    | ComponentQuantity
    /// The orderable dose quantity.
    | DoseQuantity
    /// The orderable dose rate.
    | DoseRate
    /// The schedule's frequency.
    | Frequency
    /// Any other order variable, which the server has no step commands for.
    | Other


/// The number of values the order variable holds; 0 when it holds a range or nothing.
let valuesCount (ovar: OrderVariable) =
    ovar.Variable.Vals
    |> Option.map (_.Value >> Array.length)
    |> Option.defaultValue 0


/// Whether the server has step commands for the field and the order allows them now. The dose
/// quantity can only be stepped when every component's orderable quantity holds one value.
let canStep (field: Field) (ord: Order) =
    match field with
    | Field.ComponentQuantity
    | Field.DoseRate
    | Field.Frequency -> true
    | Field.DoseQuantity ->
        ord.Orderable.Components
        |> Array.forall (_.OrderableQuantity >> Order.OrderVariable.isSolved)
    | Field.Other -> false


/// Decide the mode of a field. canStep: the server has step commands for this variable and the
/// order allows them now. allSolved: every variable the order depends on holds one value.
/// Several values are chosen from; one value is stepped only when it can step, the order is
/// solved and the rules define an increment, and is fixed otherwise; a range is navigated when
/// it can step and has a max and an increment, is fixed when it can step and has a bound the
/// user cannot move, and is chosen from otherwise.
let decide (canStep: bool) (allSolved: bool) (ovar: OrderVariable) =
    match ovar |> valuesCount with
    | n when n > 1 -> Mode.Selectable
    | 1 ->
        if canStep && allSolved && ovar.DefinedConstraints.Incr.IsSome then
            Mode.Stepable
        else
            Mode.Fixed
    | _ ->
        if canStep && ovar |> Order.OrderVariable.isNavigable then
            Mode.Navigable
        elif canStep && (ovar.Variable.Min.IsSome || ovar.Variable.Max.IsSome) then
            Mode.Fixed
        else
            Mode.Selectable


/// Decide the mode of a field of an order, as both views call it.
let decideFor (field: Field) (ord: Order) (ovar: OrderVariable) =
    ovar |> decide (ord |> canStep field) (ord |> Order.isSolved)
