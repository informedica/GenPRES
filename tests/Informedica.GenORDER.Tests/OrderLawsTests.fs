/// The laws of the calculations of an order by the order program, over the fixtures: creating an
/// order, making its values and resetting it.
module OrderLawsTests

open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


let caps = Order.ValueCaps.defaults


/// The order after the action, with the failure as text.
let run action ord =
    ord
    |> OrderProcessor.OrderAction.run caps OrderLogging.noOp action
    |> Result.mapError (fun (_, f) -> $"%A{f}")


/// The values of every variable of an order, by name.
let values ord =
    ord
    |> Order.toOrdVars
    |> List.map (fun ovar -> ovar.Variable.Name, ovar.Variable.Values)


/// The names of the variables whose values differ between two orders.
let differ ord1 ord2 =
    List.zip (ord1 |> values) (ord2 |> values)
    |> List.filter (fun (a, b) -> a <> b)
    |> List.map (fst >> fst >> Informedica.GenSolver.Lib.Variable.Name.toString)


/// Per fixture: the order as made from the medication, the created order, the order with values
/// and the solved order, as the dose dialog gets them.
let fixtures =
    lazy
        [
            for name, med in Tests.ReferenceTests.fixtures () do
                let ord = med |> Tests.TestOrder.toOrder

                let created =
                    ord
                    |> run OrderAction.CalcConstraints
                    |> Result.defaultWith (fun e -> failtest $"%s{name} not created: %s{e}")

                let valued =
                    created
                    |> run OrderAction.CalcValues
                    |> Result.defaultWith (fun e -> failtest $"%s{name} no values: %s{e}")

                name, ord, created, valued, valued |> Tests.TestOrder.solve SolveOrder
        ]


[<Tests>]
let tests =
    testList
        "OrderProcessor calculation laws"
        [
            test "every fixture is created and gets its values" {
                fixtures.Value
                |> List.length
                |> Expect.equal "every fixture" (Tests.ReferenceTests.fixtures () |> List.length)
            }

            test "the commands of the order context run as the actions of the program" {
                [
                    for name, ord, created, valued, solved in fixtures.Value do
                        for cmd, action, before in
                            [
                                CalcMinMax, OrderAction.CalcConstraints, ord
                                CalcValues, OrderAction.CalcValues, created
                                ReCalcValues, OrderAction.Reset, solved
                            ] do
                            match
                                cmd before |> OrderProcessor.processPipeline OrderLogging.noOp, before |> run action
                            with
                            | Ok a, Ok b when differ a b |> List.isEmpty -> ()
                            | _ -> $"%s{name} %A{action}"
                ]
                |> Expect.isEmpty "the command gives what the action gives"
            }

            test "a reset gives the same order whatever was set before" {
                [
                    for name, _, created, _, solved in fixtures.Value do
                        // the created order before its norm dose, every variable back to its calculated constraints
                        let first =
                            created
                            |> OrderProcessor.OrderEffect.run
                                caps
                                OrderLogging.noOp
                                [ OrderEffect.SetAllOrderVariableRangesBack SetBackTo.Calculated ]
                            |> Result.mapError (fun (_, f) -> $"%A{f}")
                            |> Result.bind (run OrderAction.Reset)

                        match first, solved |> run OrderAction.Reset with
                        | Ok a, Ok b ->
                            for n in differ a b do
                                $"%s{name}: %s{n}"
                        | a, b -> $"%s{name}: first reset ok %b{a |> Result.isOk}, reset ok %b{b |> Result.isOk}"
                ]
                |> Expect.isEmpty "a reset forgets every set"
            }

            test "a second reset changes nothing" {
                [
                    for name, _, _, _, solved in fixtures.Value do
                        match
                            solved
                            |> run OrderAction.Reset
                            |> Result.bind (fun a -> a |> run OrderAction.Reset |> Result.map (fun b -> a, b))
                        with
                        | Ok(a, b) ->
                            for n in differ a b do
                                $"%s{name}: %s{n}"
                        | Error e -> $"%s{name}: %s{e}"
                ]
                |> Expect.isEmpty "a reset of a reset order"
            }

            test "making the values of a solved order changes nothing" {
                [
                    for name, _, _, _, solved in fixtures.Value do
                        match solved |> run OrderAction.CalcValues with
                        | Ok after ->
                            for n in differ solved after do
                                $"%s{name}: %s{n}"
                        | Error e -> $"%s{name}: %s{e}"
                ]
                |> Expect.isEmpty "the solved order kept"
            }

            test "a solve for min and max after each calculation changes nothing" {
                [
                    for name, _, created, valued, solved in fixtures.Value do
                        let reset = solved |> run OrderAction.Reset

                        for label, res in [ "created", Ok created; "valued", Ok valued; "reset", reset ] do
                            match
                                res
                                |> Result.bind (
                                    OrderProcessor.OrderEffect.run
                                        caps
                                        OrderLogging.noOp
                                        [ OrderEffect.SolveOrder SolveMode.MinMax ]
                                    >> Result.mapError (fun (_, f) -> $"%A{f}")
                                )
                            with
                            | Ok after ->
                                for n in differ (res |> Result.defaultValue after) after do
                                    $"%s{name} %s{label}: %s{n}"
                            | Error e -> $"%s{name} %s{label}: %s{e}"
                ]
                |> Expect.isEmpty "every equation holds on every result"
            }
        ]
