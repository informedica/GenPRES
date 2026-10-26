namespace Informedica.GenPRES.Client.Core.Policies


/// Decides how the user can change the value of a quantity field, with one rule for the order
/// and the nutrition views.
module QuantityModePolicy =

    open Informedica.GenPRES.Client.Core.Models

    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Models


    /// How the user can change the value of a quantity field.
    [<RequireQualifiedAccess>]
    type Mode =
        /// Several values: chosen from the dropdown, without step buttons.
        | Selectable
        /// A range: the steps narrow it; first and last jump to the minimum and the maximum.
        | Navigable
        /// One value: the steps move it; first and last make a large step.
        | Stepable
        /// A value or range the user cannot change from this field.
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
        /// Any other order variable; the server has no step commands for it.
        | Other


    /// The number of values the order variable holds; 0 for a range or nothing.
    let valuesCount (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (_.Value >> Array.length)
        |> Option.defaultValue 0


    /// Whether the field can be stepped now. The dose quantity can only be stepped when every
    /// component's orderable quantity holds one value.
    let canStep (field: Field) (ord: Order) =
        match field with
        | Field.ComponentQuantity
        | Field.DoseRate
        | Field.Frequency -> true
        | Field.DoseQuantity ->
            ord.Orderable.Components
            |> Array.forall (_.OrderableQuantity >> OrderDisplay.OrderVariable.isSolved)
        | Field.Other -> false


    /// The mode of a field, given whether it can step and whether the whole order is solved.
    /// Several values are Selectable. One value is Stepable when it can step, the order is solved
    /// and the rules define an increment; otherwise Fixed. A range is Navigable when it can step and
    /// has a maximum and an increment, Fixed when it can step and has a bound, and Selectable
    /// otherwise.
    let decide (canStep: bool) (allSolved: bool) (ovar: OrderVariable) =
        match ovar |> valuesCount with
        | n when n > 1 -> Mode.Selectable
        | 1 ->
            if canStep && allSolved && ovar.DefinedConstraints.Incr.IsSome then
                Mode.Stepable
            else
                Mode.Fixed
        | _ ->
            if canStep && ovar |> OrderDisplay.OrderVariable.isNavigable then
                Mode.Navigable
            elif canStep && (ovar.Variable.Min.IsSome || ovar.Variable.Max.IsSome) then
                Mode.Fixed
            else
                Mode.Selectable


    /// The mode of a field of an order, as the views ask for it.
    let decideFor (field: Field) (ord: Order) (ovar: OrderVariable) =
        ovar |> decide (ord |> canStep field) (ord |> OrderDisplay.isSolved)
