/// The step a medication can be divided into, and the unit group its components share: the
/// components of an order add up to the orderable, so they share a unit group, not always a unit.
module DivisibilityTests

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


/// The medications the step is tested on.
module Fixture =

    let ml = Units.Volume.milliLiter
    let mg = Units.Mass.milliGram
    let g = Units.Mass.gram
    let l = Units.Volume.liter


    let comp qty div : ProductComponent =
        { Medication.productComponent with
            Quantities = qty |> Option.map (ValueUnit.singleWithValue 1N)
            Divisible = div
        }


    let med div comps =
        { Medication.template with
            Div = div
            Components = comps
        }


    let solves (med: Medication) =
        match med |> Medication.toOrder Scenarios.testStart with
        | Error _ -> false
        | Ok ord ->
            ord
            |> CalcMinMax
            |> OrderProcessor.processPipeline OrderLogging.noOp
            |> Result.isOk


    /// The morphine continuous infusion with its second component in another unit.
    let morfContWith quantity divisible =
        { Scenarios.morfCont with
            Components =
                Scenarios.morfCont.Components
                |> List.mapi (fun i pc ->
                    if i = 1 then
                        { pc with
                            Quantities = Some quantity
                            Divisible = Some divisible
                        }
                    else
                        pc
                )
        }


    let step m =
        m |> Medication.OrderBuilder.divisibility |> Option.map ValueUnit.get


    let expected v u = Some([| v |], u)


    let incrOf (cmp: Types.Component) =
        cmp.OrderableQuantity
        |> OrderVariable.Quantity.toOrdVar
        |> _.DefinedConstraints.Incr
        |> Option.map (Variable.ValueRange.Increment.toValueUnit >> ValueUnit.get)


open Fixture


[<Tests>]
let tests =
    testList
        "Divisibility"
        [
            testList
                "the components of an order"
                [
                    test "may differ in unit within a unit group" {
                        morfContWith (ValueUnit.singleWithUnit l (1N / 1000N)) 10000N
                        |> solves
                        |> Expect.isTrue "1 mL written in L solves"
                    }

                    for div in [ 1N; 10N; 100N ] do
                        test $"may not differ in unit group, Divisible %A{div}" {
                            morfContWith (ValueUnit.singleWithUnit mg 1N) div
                            |> solves
                            |> Expect.isFalse "the sum of the components cannot be solved"
                        }

                    test "in different unit groups are refused with the reason" {
                        match
                            morfContWith (ValueUnit.singleWithUnit mg 1N) 1N
                            |> Medication.toOrder Scenarios.testStart
                        with
                        | Error(Exceptions.OrderCouldNotBeCreated(:? System.InvalidOperationException as exn)) ->
                            exn.Message |> Expect.stringContains "says why" "different unit groups"
                        | other -> failtest $"should be refused, got %A{other}"
                    }

                    test "all get the coarsest step, also in another unit of the group" {
                        match
                            morfContWith (ValueUnit.singleWithUnit l (1N / 1000N)) 1000N
                            |> Medication.toOrder Scenarios.testStart
                        with
                        | Error _ -> failtest "the medication gives an order"
                        | Ok ord ->
                            ord.Orderable.Components
                            |> List.map incrOf
                            |> Expect.equal
                                "1 mL written in L beats 0.1 mL"
                                [ expected (1N / 1000N) l; expected (1N / 1000N) l ]
                    }
                ]

            testList
                "divisibility"
                [
                    test "takes Div for the orderable" {
                        med (Some 2N) [ comp (Some ml) (Some 10N) ]
                        |> step
                        |> Expect.equal "half a mL" (expected (1N / 2N) ml)
                    }

                    test "takes Div in the orderable unit when a component has another unit of the group" {
                        med (Some 2N) [ comp (Some mg) None; comp (Some g) None ]
                        |> step
                        |> Expect.equal "Div speaks for the orderable" (expected (1N / 2N) mg)
                    }

                    test "takes the coarsest in base units" {
                        med None [ comp (Some mg) (Some 2N); comp (Some g) (Some 10N) ]
                        |> step
                        |> Expect.equal "100 mg is coarser than 0.5 mg" (expected (1N / 10N) g)
                    }

                    test "leaves out a component in another unit group" {
                        med None [ comp (Some ml) (Some 10N); comp (Some mg) (Some 1N) ]
                        |> step
                        |> Expect.equal "the 1 mg says nothing about mL" (expected (1N / 10N) ml)
                    }

                    test "gives no step when only another unit group has a Divisible" {
                        med None [ comp (Some ml) None; comp (Some mg) (Some 1N) ]
                        |> step
                        |> Expect.isNone "the orderable is in mL"
                    }

                    test "gives no step without a Divisible" {
                        med None [ comp (Some ml) None ]
                        |> step
                        |> Expect.isNone "nothing says how to divide"
                    }

                    test "gives no step without an orderable unit" {
                        med (Some 1N) [ comp None None ]
                        |> step
                        |> Expect.isNone "no unit to give the step"
                    }

                    testList
                        "a value of zero or less"
                        [
                            for div in [ 0N; -1N ] do
                                test $"Div %A{div} gives no step" {
                                    med (Some div) [ comp (Some ml) None ] |> step |> Expect.isNone "no step"
                                }

                                test $"Divisible %A{div} gives no step" {
                                    med None [ comp (Some ml) (Some div) ] |> step |> Expect.isNone "no step"
                                }
                        ]
                ]

            testList
                "the step for a component"
                [
                    test "is the common step within the group" {
                        med None [ comp (Some ml) (Some 10N); comp (Some l) (Some 1000N) ]
                        |> Medication.OrderBuilder.componentDivisibility (comp (Some ml) (Some 10N))
                        |> Option.map ValueUnit.get
                        |> Expect.equal "1 mL written in L" (expected (1N / 1000N) l)
                    }

                    test "is none in another unit group" {
                        let pc = comp (Some mg) (Some 1N)

                        med None [ comp (Some ml) (Some 10N); pc ]
                        |> Medication.OrderBuilder.componentDivisibility pc
                        |> Expect.isNone "mg keeps its own unit"
                    }

                    test "stays for a component without a quantity" {
                        let pc = comp None None

                        med None [ comp (Some ml) (Some 10N); pc ]
                        |> Medication.OrderBuilder.componentDivisibility pc
                        |> Option.map ValueUnit.get
                        |> Expect.equal "the step of the orderable" (expected (1N / 10N) ml)
                    }
                ]
        ]
