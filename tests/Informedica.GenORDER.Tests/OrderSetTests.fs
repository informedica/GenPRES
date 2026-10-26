/// Setting an order value by the order program: a select and a step, over the fixtures.
module OrderSetTests

open Informedica.Utils.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip

module Ref = OrderProcessor.OrderVariableReference


let run = OrderReCalcTests.run


/// The order with the property command applied, as the order context runs it.
let change cmd ord = ord |> run (fun o -> ChangeProperty(o, cmd))


/// The value a variable holds, none when it holds more than one.
let held ovarRef ord =
    ovarRef |> Ref.tryGet ord |> Option.bind OrderVariable.solvedValue


/// The min, max and median commands of the basic variables of an order.
let selects (ord: Order) =
    [
        SetMinScheduleFrequency
        SetMaxScheduleFrequency
        SetMedianScheduleFrequency
        SetMinOrderableDoseRate
        SetMaxOrderableDoseRate
        SetMedianOrderableDoseRate

        if ord.Schedule |> Order.Schedule.isContinuous |> not then
            SetMinOrderableDoseQuantity
            SetMaxOrderableDoseQuantity
            SetMedianOrderableDoseQuantity

        for c in ord.Orderable.Components do
            let c = c.Name |> Name.toString
            SetMinComponentOrderableQuantity c
            SetMaxComponentOrderableQuantity c
            SetMedianComponentOrderableQuantity c
    ]


[<Tests>]
let tests =
    testList
        "OrderProcessor set"
        [
            test "a min, max or median of the dose quantity is refused on a continuous order, and keeps the order" {
                let ord = Scenarios.morfCont |> OrderReCalcTests.processed

                [
                    SetMinOrderableDoseQuantity
                    SetMaxOrderableDoseQuantity
                    SetMedianOrderableDoseQuantity
                ]
                |> List.map (fun cmd ->
                    match ord |> change cmd with
                    | Error(o, msgs) -> o = ord && msgs |> List.isEmpty
                    | Ok _ -> false
                )
                |> Expect.equal "every command refused" [ true; true; true ]
            }

            test "a step on a component quantity that holds a list is refused, and keeps the order" {
                let ord = Scenarios.tpn |> OrderReCalcTests.processed
                let gluc = OrderVariableReference.ComponentOrderableQuantity "gluc 10%"

                gluc
                |> Ref.tryGet ord
                |> Option.exists (OrderVariable.hasOneValue >> not)
                |> Expect.isTrue "gluc 10% holds a list"

                match ord |> change (IncreaseComponentOrderableQuantity("gluc 10%", 1, true)) with
                | Error(o, msgs) ->
                    o |> Expect.equal "the order kept" ord
                    msgs |> Expect.isEmpty "a refusal has no messages"
                | Ok _ -> failtest "a step on a list"
            }

            test "a min, max, median or first value of a field that holds one value keeps it" {
                let cases =
                    [
                        for name, ord in Tests.RecipeTests.solvedFixtures.Value do
                            for ovarRef in ord |> Tests.RecipeTests.stepRefs do
                                match ord |> held ovarRef with
                                | None -> ()
                                | Some vu ->
                                    for choice in [ Choice.Min; Choice.Max; Choice.Median; Choice.Nth 0 ] do
                                        name, ord, ovarRef, choice, vu
                    ]

                cases |> Expect.isNonEmpty "fields that hold one value"

                [
                    for name, ord, ovarRef, choice, vu in cases do
                        match
                            ord
                            |> OrderProcessor.OrderAction.run
                                Order.ValueCaps.defaults
                                OrderLogging.noOp
                                (OrderAction.Set(ovarRef, choice))
                        with
                        | Error(_, ActionFailure.NoRecipe _) -> ()
                        | Error(_, f) -> $"%s{name} %A{ovarRef} %A{choice}: %A{f}"
                        | Ok after ->
                            if after |> held ovarRef <> Some vu then
                                $"%s{name} %A{ovarRef} %A{choice}: the value changed"
                ]
                |> Expect.isEmpty "the value kept"
            }

            test "every min, max and median of a basic variable of the processed fixtures solves" {
                [
                    for name, med in Tests.ReferenceTests.fixtures () do
                        let ord = med |> OrderReCalcTests.processed

                        for cmd in ord |> selects do
                            match ord |> change cmd with
                            | Ok _ -> ()
                            // a variable that offers nothing to choose is refused, as a select of nothing
                            | Error(_, []) -> ()
                            | Error(_, msgs) -> $"%s{name} %A{cmd}: %A{msgs}"
                ]
                |> Expect.isEmpty "every select solves or is refused"
            }
        ]
