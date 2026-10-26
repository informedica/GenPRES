/// The helpers that hold a component to one combination of item concentrations, and that join the
/// orders per combination again.
module ConcentrationTests

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module ValueRange = Informedica.GenSolver.Lib.Variable.ValueRange
module Minimum = ValueRange.Minimum
module Component = Order.Orderable.Component


let mL (vs: int list) =
    vs
    |> List.map BigRational.fromInt
    |> List.toArray
    |> ValueUnit.withUnit Units.Volume.milliLiter


/// A min, increment and max in mL, each limit inclusive.
let minIncrMax min incr max =
    ValueRange.create
        (mL [ min ] |> Minimum.create true |> Some)
        (mL [ incr ] |> ValueRange.Increment.create |> Some)
        (mL [ max ] |> ValueRange.Maximum.create true |> Some)
        None


let valSet vs =
    mL vs |> ValueRange.ValueSet.create |> Informedica.GenSolver.Lib.Types.ValSet


let unionRanges = Variable.ValueRange.unionRanges


/// The first component of the order of the medication, as it is built.
let firstComponent med =
    (med |> Tests.TestOrder.toOrder).Orderable.Components |> List.head


/// The values an item holds its component concentration to, per item.
let itemConcentrations (cmp: Component) =
    cmp.Items
    |> List.map (fun itm ->
        itm.ComponentConcentration
        |> OrderVariable.Concentration.toOrdVar
        |> _.DefinedConstraints.Values
        |> Option.map (ValueRange.ValueSet.toValueUnit >> ValueUnit.getValue)
    )


/// The number of strengths each item of a component holds.
let strengthCounts (cmp: Component) =
    cmp.Items
    |> List.map (fun itm ->
        itm.ComponentConcentration
        |> OrderVariable.Concentration.toOrdVar
        |> OrderVariable.tryValueList
        |> Option.map ValueRange.ValueSet.count
        |> Option.defaultValue 1
    )


/// The number of strengths an item concentration holds.
let strengthCount ovar =
    ovar
    |> OrderVariable.tryValueList
    |> Option.map ValueRange.ValueSet.count
    |> Option.defaultValue 1


/// A volume order variable with the range.
let volume vr =
    let ovar =
        OrderVariable.createNew ("test" |> Informedica.GenSolver.Lib.Variable.Name.createExc) Units.Volume.milliLiter

    { ovar with OrderVariable.Variable.Values = vr }


[<Tests>]
let tests =
    testList
        "concentrations"
        [
            testList
                "ValueRange.unionRanges"
                [
                    test "the same range twice is that range" {
                        [ minIncrMax 1 1 3; minIncrMax 1 1 3 ]
                        |> unionRanges 10
                        |> Expect.equal "the range" (minIncrMax 1 1 3)
                    }

                    test "ranges that can be listed give one list" {
                        [ valSet [ 1; 3 ]; minIncrMax 2 2 6 ]
                        |> unionRanges 10
                        |> Expect.equal "1, 2, 3, 4 and 6 mL" (valSet [ 1; 2; 3; 4; 6 ])
                    }

                    test "ranges past the cap give the lowest minimum and highest maximum on their increments" {
                        // the increments 1 and 2 mL: 2 is a multiple of 1, so 1 mL remains
                        [ minIncrMax 1 1 100; minIncrMax 50 2 200 ]
                        |> unionRanges 10
                        |> Expect.equal "1 to 200 mL on 1 mL" (minIncrMax 1 1 200)
                    }
                ]

            testList
                "OrderVariable"
                [
                    test "fixToValues sets the value lists and narrows the range, unless the range is set back" {
                        let ovar = minIncrMax 1 1 10 |> volume
                        let vu = mL [ 2; 4 ]

                        match ovar |> OrderVariable.fixToValues (fun _ -> false) vu with
                        | None -> failtest "the range keeps 2 and 4 mL"
                        | Some fixedOvar ->
                            fixedOvar.Variable.Values |> Expect.equal "narrowed" (valSet [ 2; 4 ])

                            fixedOvar.DefinedConstraints.Values
                            |> Expect.equal "the defined list" (vu |> ValueRange.ValueSet.create |> Some)

                        ovar
                        |> OrderVariable.fixToValues (fun _ -> true) vu
                        |> Option.map _.Variable.Values
                        |> Expect.equal "the range kept" (Some(minIncrMax 1 1 10))
                    }

                    test "fixToValues gives none when the range keeps no value" {
                        minIncrMax 1 1 10
                        |> volume
                        |> OrderVariable.fixToValues (fun _ -> false) (mL [ 20 ])
                        |> Expect.isNone "20 mL lies outside 1 to 10 mL"
                    }

                    test "tryValueList gives the list of the range, else of the constraints" {
                        valSet [ 1; 2 ]
                        |> volume
                        |> OrderVariable.tryValueList
                        |> Expect.equal "the range" (mL [ 1; 2 ] |> ValueRange.ValueSet.create |> Some)

                        minIncrMax 1 1 10
                        |> volume
                        |> OrderVariable.tryValueList
                        |> Expect.isNone "no list"
                    }

                    test "unionPerConcentration joins the ranges and keeps the constraints of the original" {
                        let orig = minIncrMax 1 1 10 |> volume

                        OrderVariable.unionPerConcentration
                            10
                            false
                            orig
                            [ valSet [ 1 ] |> volume; valSet [ 3 ] |> volume ]
                        |> fun ovar -> ovar.Variable.Values, ovar.CalculatedConstraints
                        |> Expect.equal
                            "1 and 3 mL, the original constraints"
                            (valSet [ 1; 3 ], orig.CalculatedConstraints)
                    }
                ]

            testList
                "Component.concentrations"
                [
                    test "a component with products gives one component per product, each held to its strengths" {
                        for med, n in
                            [
                                Scenarios.amoxClavText |> Tests.TestOrder.parse, 2
                                Scenarios.amoxClavEveryCombinationText |> Tests.TestOrder.parse, 4
                            ] do
                            let cmps = med |> firstComponent |> Component.concentrations (fun _ -> true)
                            cmps |> List.length |> Expect.equal "one per product" n

                            cmps
                            |> List.forall (itemConcentrations >> List.forall (Option.exists (Array.length >> (=) 1)))
                            |> Expect.isTrue "every item held to one strength"

                            cmps
                            |> List.map itemConcentrations
                            |> List.distinct
                            |> List.length
                            |> Expect.equal "different strengths" n
                    }

                    test "products with the same strengths stay together, with their contents as one list" {
                        let cmp = Scenarios.amoxClavText |> Tests.TestOrder.parse |> firstComponent
                        let p = cmp.Products |> List.head
                        let twin = { p with FormQuantities = mL [ 999 ] |> Some }
                        let cmp = { cmp with Products = twin :: cmp.Products }

                        let cmps = cmp |> Component.concentrations (fun _ -> true)
                        cmps |> List.length |> Expect.equal "still one per strength" 2

                        cmps
                        |> List.exists (fun c ->
                            c.ComponentQuantity
                            |> OrderVariable.Quantity.toOrdVar
                            |> _.DefinedConstraints.Values
                            |> Option.exists (fun vs ->
                                vs
                                |> ValueRange.ValueSet.toValueUnit
                                |> ValueUnit.getValue
                                |> Array.contains 999N
                            )
                        )
                        |> Expect.isTrue "the twin's content is in the list"
                    }

                    test "a component without products gives every combination of its item strengths" {
                        for med in [ Scenarios.cotrim; Scenarios.pcmSupp ] do
                            let cmp = med |> firstComponent

                            cmp
                            |> Component.concentrations (fun _ -> true)
                            |> List.length
                            |> Expect.equal
                                "the product of the strength counts"
                                (cmp |> strengthCounts |> List.fold (*) 1)
                    }

                    test "a combination that keeps no value of a variable the action keeps drops out" {
                        let cmp = Scenarios.pcmSupp |> firstComponent
                        let itm = cmp.Items |> List.head

                        let cnc = itm.ComponentConcentration |> OrderVariable.Concentration.toOrdVar

                        // the first, lowest, strength in the unit of the list
                        let one =
                            cnc
                            |> OrderVariable.tryValueList
                            |> Option.map ValueRange.ValueSet.toValueUnit
                            |> Option.map (fun vu ->
                                vu
                                |> ValueUnit.getValue
                                |> Array.head
                                |> ValueUnit.singleWithUnit (vu |> ValueUnit.getUnit)
                            )
                            |> Option.defaultWith (fun () -> failtest "no strengths")

                        // held below the second strength by a maximum, so only the first strength fits and the
                        // strengths still come from the constraints
                        let held =
                            { cnc with
                                OrderVariable.Variable.Values =
                                    ValueRange.create None None (one |> ValueRange.Maximum.create true |> Some) None
                            }

                        let cmp = { cmp with Items = [ { itm with ComponentConcentration = held |> Concentration } ] }

                        cmp
                        |> Component.concentrations (fun _ -> false)
                        |> List.length
                        |> Expect.equal "the held strength only" 1

                        cmp
                        |> Component.concentrations (fun _ -> true)
                        |> List.length
                        |> Expect.equal "every strength when the action sets back" (cnc |> strengthCount)
                    }
                ]
        ]
