// One rule that decides a quantity field's mode for every order variable, for the order and
// the nutrition view alike (#1115).
//
// Today each call site decides on its own: createStepper and createDoseQtyStepper take a
// navigable and a solved flag, stepsMode turns them into Navigable or Stepable, a field without
// steps is Selectable, and Fixed is never produced. Frequency has steps in the order view and
// none in the nutrition view. This script drafts the single pure rule for Client.Core and
// checks it per mode transition.
//
// The rule changes behavior: frequency gets steps in the nutrition view, and a single value
// that cannot be stepped becomes read-only (Fixed).
//
// Run: `dotnet fsi QuantityMode.fsx` from this directory after `dotnet run build`, or via the
// FSI MCP after `#I "<this directory>"`.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"
#load "load.fsx"

open Shared.Types
open Shared.Models


/// How the user may move the value of a quantity field.
[<RequireQualifiedAccess>]
type QuantityMode =
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
type QuantityField =
    /// The orderable quantity of the named component.
    | ComponentQuantity of string
    /// The orderable dose quantity.
    | DoseQuantity
    /// The orderable dose rate.
    | DoseRate
    /// The schedule's frequency.
    | Frequency
    /// Any other order variable, which the server has no step commands for.
    | Other


module QuantityMode =

    let valuesCount (ovar: OrderVariable) =
        ovar.Variable.Vals
        |> Option.map (_.Value >> Array.length)
        |> Option.defaultValue 0


    /// Whether the server has step commands for the field and the order allows them now. The
    /// dose quantity can only be stepped when every component's orderable quantity holds one
    /// value.
    let canStep (field: QuantityField) (ord: Order) =
        match field with
        | QuantityField.ComponentQuantity _
        | QuantityField.DoseRate
        | QuantityField.Frequency -> true
        | QuantityField.DoseQuantity ->
            ord.Orderable.Components
            |> Array.forall (_.OrderableQuantity >> Order.OrderVariable.isSolved)
        | QuantityField.Other -> false


    /// Decide the mode of a field. canStep: the server has step commands for this variable and
    /// the order allows them now. allSolved: every variable the order depends on holds one
    /// value.
    let decide (canStep: bool) (allSolved: bool) (ovar: OrderVariable) =
        match ovar |> valuesCount with
        | n when n > 1 -> QuantityMode.Selectable
        | 1 ->
            if canStep && allSolved && ovar.DefinedConstraints.Incr.IsSome then
                QuantityMode.Stepable
            else
                QuantityMode.Fixed
        | _ ->
            let var = ovar.Variable

            if canStep && ovar |> Order.OrderVariable.isNavigable then
                QuantityMode.Navigable
            elif canStep && (var.Min.IsSome || var.Max.IsSome) then
                QuantityMode.Fixed
            else
                QuantityMode.Selectable


    /// Decide the mode of a field of an order, as both views would call it.
    let decideFor (field: QuantityField) (ord: Order) (ovar: OrderVariable) =
        ovar |> decide (ord |> canStep field) (ord |> Order.isSolved)


module Tests =

    open Expecto
    open Expecto.Flip


    let vu vals =
        Order.ValueUnit.create (vals |> Array.map (fun v -> (string v, v))) "mg" "mass" true "nl" ""


    let variable min incr max vals =
        Order.Variable.create
            "test"
            false
            (min |> Option.map (Array.singleton >> vu))
            false
            (incr |> Option.map (Array.singleton >> vu))
            (max |> Option.map (Array.singleton >> vu))
            false
            (vals |> Option.map vu)


    let emptyVar = variable None None None None


    /// An order variable with a defined increment, or none, and its variable.
    let ovar (defIncr: decimal option) var =
        Order.OrderVariable.create "test" (variable None defIncr None None) emptyVar var None IsNormal


    let withIncr = ovar (Some 1m)
    let withoutIncr = ovar None

    let oneVal = variable None None None (Some [| 5m |])
    let twoVals = variable None None None (Some [| 5m; 10m |])
    let range = variable (Some 1m) None (Some 10m) None
    let minOnly = variable (Some 1m) None None None
    let maxOnly = variable None None (Some 10m) None


    let tests =
        testList
            "QuantityMode.decide"
            [
                test "more than one value is Selectable, whatever the rest" {
                    for canStep, allSolved in [ true, true; true, false; false, true; false, false ] do
                        twoVals
                        |> withIncr
                        |> QuantityMode.decide canStep allSolved
                        |> Expect.equal "should be Selectable" QuantityMode.Selectable
                }

                test "one value that can step, all solved and an increment is Stepable" {
                    oneVal
                    |> withIncr
                    |> QuantityMode.decide true true
                    |> Expect.equal "should be Stepable" QuantityMode.Stepable
                }

                test "one value with dependents unsolved is Fixed" {
                    oneVal
                    |> withIncr
                    |> QuantityMode.decide true false
                    |> Expect.equal "should be Fixed" QuantityMode.Fixed
                }

                test "one value without a defined increment is Fixed" {
                    oneVal
                    |> withoutIncr
                    |> QuantityMode.decide true true
                    |> Expect.equal "should be Fixed" QuantityMode.Fixed
                }

                test "one value that cannot step is Fixed" {
                    oneVal
                    |> withIncr
                    |> QuantityMode.decide false true
                    |> Expect.equal "should be Fixed" QuantityMode.Fixed
                }

                test "no value with a max and an increment that can step is Navigable" {
                    range
                    |> withIncr
                    |> QuantityMode.decide true false
                    |> Expect.equal "should be Navigable" QuantityMode.Navigable
                }

                test "no value with a navigable range that cannot step is Selectable" {
                    range
                    |> withIncr
                    |> QuantityMode.decide false false
                    |> Expect.equal "should be Selectable" QuantityMode.Selectable
                }

                test "no value with a range but no increment that can step is Fixed" {
                    range
                    |> withoutIncr
                    |> QuantityMode.decide true false
                    |> Expect.equal "should be Fixed" QuantityMode.Fixed
                }

                test "no value with only a min and an increment that can step is Fixed" {
                    minOnly
                    |> withIncr
                    |> QuantityMode.decide true false
                    |> Expect.equal "should be Fixed" QuantityMode.Fixed
                }

                test "no value with only a max but no increment that can step is Fixed" {
                    maxOnly
                    |> withoutIncr
                    |> QuantityMode.decide true false
                    |> Expect.equal "should be Fixed" QuantityMode.Fixed
                }

                test "no value and no bounds that can step is Selectable" {
                    emptyVar
                    |> withIncr
                    |> QuantityMode.decide true true
                    |> Expect.equal "should be Selectable" QuantityMode.Selectable
                }

                test "no value and no bounds that cannot step is Selectable" {
                    emptyVar
                    |> withoutIncr
                    |> QuantityMode.decide false false
                    |> Expect.equal "should be Selectable" QuantityMode.Selectable
                }
            ]


Expecto.Tests.runTestsWithCLIArgs [] [||] Tests.tests
