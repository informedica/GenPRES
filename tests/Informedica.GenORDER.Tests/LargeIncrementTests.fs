/// The large step of an order variable: the increment it moves by, and the step that uses it.
module LargeIncrementTests

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module ValueRange = Variable.ValueRange
module Increment = ValueRange.Increment
module ValueSet = ValueRange.ValueSet
module Constraints = OrderVariable.Constraints


let mL = Units.Volume.milliLiter
let mLPerHour = Units.Volume.milliLiter |> ValueUnit.per Units.Time.hour
let mg = Units.Mass.milliGram


/// An order variable with the value v, and with the calculated increment calc and the defined
/// increment def in the unit u when they are given.
let ovarWith u v calc def =
    let incr i =
        i |> Option.map (fun i -> [| i |] |> ValueUnit.withUnit u |> Increment.create)

    let ovar = OrderVariable.createNew ("x" |> Variable.Name.createExc) u

    { ovar with
        CalculatedConstraints = Constraints.create None (incr calc) None None
        DefinedConstraints = Constraints.create None (incr def) None None
        Variable = { ovar.Variable with Values = [| v |] |> ValueUnit.withUnit u |> ValueSet.create |> ValSet }
    }


let valueOf ovar =
    ovar |> OrderVariable.solvedValue |> Option.map ValueUnit.getValue


let largeOf ovar =
    ovar
    |> OrderVariable.largeIncrement
    |> Option.map (Increment.toValueUnit >> ValueUnit.getValue)


/// The value and unit of the large increment a Dto carries.
let largeOfDto (dto: OrderVariable.Dto.Dto) = dto.LargeIncrOpt |> Option.map (fun vu -> vu.Value, vu.Unit)


[<Tests>]
let tests =
    testList
        "the large step"
        [

            testList
                "largeIncrement"
                [
                    test "a calculated increment of 0.1 mL gives 1 mL" {
                        ovarWith mL 10N (Some(1N / 10N)) None
                        |> largeOf
                        |> Expect.equal "1 mL" (Some [| 1N |])
                    }

                    test "a calculated increment of 0.1 mL/hour gives 1 mL/hour" {
                        ovarWith mLPerHour 10N (Some(1N / 10N)) None
                        |> largeOf
                        |> Expect.equal "1 mL/hour" (Some [| 1N |])
                    }

                    test "a calculated increment of 0.5 mL stays 0.5 mL" {
                        ovarWith mL 10N (Some(1N / 2N)) None
                        |> largeOf
                        |> Expect.equal "0.5 mL" (Some [| 1N / 2N |])
                    }

                    test "a calculated increment of 0.1 mg stays 0.1 mg" {
                        ovarWith mg 10N (Some(1N / 10N)) None
                        |> largeOf
                        |> Expect.equal "0.1 mg" (Some [| 1N / 10N |])
                    }

                    test "no calculated increment gives none" {
                        ovarWith mL 10N None (Some(1N / 10N))
                        |> largeOf
                        |> Expect.isNone "no large increment"
                    }

                    test "the unit stays that of the calculated increment" {
                        ovarWith mLPerHour 10N (Some(1N / 10N)) None
                        |> OrderVariable.largeIncrement
                        |> Option.map (Increment.toValueUnit >> ValueUnit.getUnit)
                        |> Expect.equal "mL/hour" (Some mLPerHour)
                    }
                ]

            testList
                "step"
                [
                    test "a large step up on a rate of 10 mL/hour with a calculated increment of 0.1 mL/hour gives 11" {
                        ovarWith mLPerHour 10N (Some(1N / 10N)) (Some(1N / 10N))
                        |> OrderVariable.increase true 1
                        |> valueOf
                        |> Expect.equal "11 mL/hour" (Some [| 11N |])
                    }

                    test "a large step down on a rate of 10 mL/hour with a calculated increment of 0.1 mL/hour gives 9" {
                        ovarWith mLPerHour 10N (Some(1N / 10N)) (Some(1N / 10N))
                        |> OrderVariable.decrease true 1
                        |> valueOf
                        |> Expect.equal "9 mL/hour" (Some [| 9N |])
                    }

                    test "a small step up on the same rate gives 10.1" {
                        ovarWith mLPerHour 10N (Some(1N / 10N)) (Some(1N / 10N))
                        |> OrderVariable.increase false 1
                        |> valueOf
                        |> Expect.equal "10.1 mL/hour" (Some [| 101N / 10N |])
                    }

                    test "a large step up on a quantity of 5 mL with a calculated increment of 0.5 mL gives 5.5" {
                        ovarWith mL 5N (Some(1N / 2N)) (Some(1N / 10N))
                        |> OrderVariable.increase true 1
                        |> valueOf
                        |> Expect.equal "5.5 mL" (Some [| 11N / 2N |])
                    }
                ]

            testList
                "Dto"
                [
                    test "toDto carries the large increment" {
                        ovarWith mLPerHour 10N (Some(1N / 10N)) None
                        |> OrderVariable.Dto.toDto
                        |> _.LargeIncrOpt
                        |> Option.map _.Value
                        |> Expect.equal "1 mL/hour" (Some [| 1N |])
                    }

                    test "the large increment is stored and read back" {
                        let dto = ovarWith mLPerHour 10N (Some(1N / 10N)) None |> OrderVariable.Dto.toDto

                        dto
                        |> Canonical.serialize
                        |> Canonical.deserialize<OrderVariable.Dto.Dto>
                        |> largeOfDto
                        |> Expect.equal "the same large increment" (dto |> largeOfDto)
                    }

                    test "toDto carries no large increment without a calculated increment" {
                        ovarWith mL 10N None (Some(1N / 10N))
                        |> OrderVariable.Dto.toDto
                        |> _.LargeIncrOpt
                        |> Expect.isNone "no large increment"
                    }
                ]
        ]
