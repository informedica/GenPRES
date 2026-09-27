namespace Informedica.GenPRES.Client.Core.Tests


/// How the user may move the value of a quantity field: one rule for every order variable.
module QuantityModeTests =

    open System
    open Expecto
    open Expecto.Flip
    open Shared.Types
    open Shared.Models


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


    let solvedVar = oneVal |> withIncr
    let unsolvedVar = twoVals |> withIncr


    let dose qty =
        Order.Dose.create qty solvedVar solvedVar solvedVar solvedVar solvedVar solvedVar solvedVar


    let cmp orbQty =
        Order.Component.create
            "c"
            "c"
            "form"
            solvedVar
            orbQty
            solvedVar
            solvedVar
            solvedVar
            solvedVar
            (dose solvedVar)
            [||]


    /// A discontinuous order whose component orderable quantities and dose quantity are given.
    let order (cmpQtys: OrderVariable list) doseQty =
        let orb =
            Order.Orderable.create
                "o"
                solvedVar
                solvedVar
                solvedVar
                solvedVar
                (dose doseQty)
                (cmpQtys |> List.map cmp |> List.toArray)

        let schedule = Order.Prescription.create false false false true false solvedVar solvedVar

        Order.create "id" solvedVar orb schedule "or" solvedVar DateTime.MinValue None


    [<Tests>]
    let tests =
        testList
            "QuantityMode"
            [
                testList
                    "decide"
                    [
                        test "more than one value is Selectable, whatever the rest" {
                            for canStep, allSolved in [ true, true; true, false; false, true; false, false ] do
                                twoVals
                                |> withIncr
                                |> QuantityMode.decide canStep allSolved
                                |> Expect.equal "should be Selectable" QuantityMode.Mode.Selectable
                        }

                        test "one value that can step, all solved and an increment is Stepable" {
                            oneVal
                            |> withIncr
                            |> QuantityMode.decide true true
                            |> Expect.equal "should be Stepable" QuantityMode.Mode.Stepable
                        }

                        test "one value with dependents unsolved is Fixed" {
                            oneVal
                            |> withIncr
                            |> QuantityMode.decide true false
                            |> Expect.equal "should be Fixed" QuantityMode.Mode.Fixed
                        }

                        test "one value without a defined increment is Fixed" {
                            oneVal
                            |> withoutIncr
                            |> QuantityMode.decide true true
                            |> Expect.equal "should be Fixed" QuantityMode.Mode.Fixed
                        }

                        test "one value that cannot step is Fixed" {
                            oneVal
                            |> withIncr
                            |> QuantityMode.decide false true
                            |> Expect.equal "should be Fixed" QuantityMode.Mode.Fixed
                        }

                        test "no value with a max and an increment that can step is Navigable" {
                            range
                            |> withIncr
                            |> QuantityMode.decide true false
                            |> Expect.equal "should be Navigable" QuantityMode.Mode.Navigable
                        }

                        test "no value with a navigable range that cannot step is Selectable" {
                            range
                            |> withIncr
                            |> QuantityMode.decide false false
                            |> Expect.equal "should be Selectable" QuantityMode.Mode.Selectable
                        }

                        test "no value with a range but no increment that can step is Fixed" {
                            range
                            |> withoutIncr
                            |> QuantityMode.decide true false
                            |> Expect.equal "should be Fixed" QuantityMode.Mode.Fixed
                        }

                        test "no value with only a min and an increment that can step is Fixed" {
                            minOnly
                            |> withIncr
                            |> QuantityMode.decide true false
                            |> Expect.equal "should be Fixed" QuantityMode.Mode.Fixed
                        }

                        test "no value with only a max but no increment that can step is Fixed" {
                            maxOnly
                            |> withoutIncr
                            |> QuantityMode.decide true false
                            |> Expect.equal "should be Fixed" QuantityMode.Mode.Fixed
                        }

                        test "no value and no bounds that can step is Selectable" {
                            emptyVar
                            |> withIncr
                            |> QuantityMode.decide true true
                            |> Expect.equal "should be Selectable" QuantityMode.Mode.Selectable
                        }

                        test "no value and no bounds that cannot step is Selectable" {
                            emptyVar
                            |> withoutIncr
                            |> QuantityMode.decide false false
                            |> Expect.equal "should be Selectable" QuantityMode.Mode.Selectable
                        }
                    ]

                testList
                    "canStep"
                    [
                        test "component quantity, dose rate and frequency can step" {
                            let ord = order [ unsolvedVar ] unsolvedVar

                            [
                                QuantityMode.Field.ComponentQuantity
                                QuantityMode.Field.DoseRate
                                QuantityMode.Field.Frequency
                            ]
                            |> List.forall (fun field -> ord |> QuantityMode.canStep field)
                            |> Expect.isTrue "should all step"
                        }

                        test "any other field cannot step" {
                            order [ solvedVar ] solvedVar
                            |> QuantityMode.canStep QuantityMode.Field.Other
                            |> Expect.isFalse "should not step"
                        }

                        test "dose quantity steps when every component quantity holds one value" {
                            order [ solvedVar; solvedVar ] unsolvedVar
                            |> QuantityMode.canStep QuantityMode.Field.DoseQuantity
                            |> Expect.isTrue "should step"
                        }

                        test "dose quantity cannot step when a component quantity holds several values" {
                            order [ solvedVar; unsolvedVar ] unsolvedVar
                            |> QuantityMode.canStep QuantityMode.Field.DoseQuantity
                            |> Expect.isFalse "should not step"
                        }
                    ]

                testList
                    "decideFor"
                    [
                        test "a solved order steps its dose quantity" {
                            let ord = order [ solvedVar ] solvedVar

                            ord.Orderable.Dose.Quantity
                            |> QuantityMode.decideFor QuantityMode.Field.DoseQuantity ord
                            |> Expect.equal "should be Stepable" QuantityMode.Mode.Stepable
                        }

                        test "an unsolved order fixes a single dose quantity" {
                            let ord = order [ unsolvedVar ] solvedVar

                            ord.Orderable.Dose.Quantity
                            |> QuantityMode.decideFor QuantityMode.Field.DoseQuantity ord
                            |> Expect.equal "should be Fixed" QuantityMode.Mode.Fixed
                        }
                    ]
            ]
