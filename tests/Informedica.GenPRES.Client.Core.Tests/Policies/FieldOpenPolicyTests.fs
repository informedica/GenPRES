namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// What a field offers, the cross and the arrow, and what a click and a close do.
module FieldOpenPolicyTests =

    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open FieldOpenPolicy


    [<Tests>]
    let tests =
        let pick options chosen enabled : PickPolicy.Pick =
            {
                Options = options
                Chosen = chosen
                Clearable = true
                Enabled = enabled
            }

        let offer cross arrow =
            {
                Cross = cross
                Arrow = arrow
            }

        testList
            "FieldOpenPolicy"
            [
                testList
                    "an order variable never offers the cross"
                    [
                        for values, mode, constrained, usable, exp in
                            [
                                3, QuantityModePolicy.Mode.Selectable, Constrained.No, true, Arrow.Open
                                1, QuantityModePolicy.Mode.Stepable, Constrained.Yes, true, Arrow.ReopenCleared
                                1, QuantityModePolicy.Mode.Fixed, Constrained.Yes, true, Arrow.ReopenCleared
                                1, QuantityModePolicy.Mode.Fixed, Constrained.Unknown, true, Arrow.ReopenCleared
                                1, QuantityModePolicy.Mode.Stepable, Constrained.No, true, Arrow.NoArrow
                                1, QuantityModePolicy.Mode.Fixed, Constrained.No, true, Arrow.NoArrow
                                0, QuantityModePolicy.Mode.Navigable, Constrained.No, true, Arrow.NoArrow
                                0, QuantityModePolicy.Mode.Fixed, Constrained.No, true, Arrow.NoArrow
                                0, QuantityModePolicy.Mode.Selectable, Constrained.No, true, Arrow.NoArrow
                                3, QuantityModePolicy.Mode.Selectable, Constrained.No, false, Arrow.NoArrow
                                1, QuantityModePolicy.Mode.Stepable, Constrained.Yes, false, Arrow.NoArrow
                            ] do
                            test $"%i{values} values, %A{mode}, constrained %A{constrained}, usable %b{usable}: %A{exp}" {
                                Field.OrderVariable(values, mode, constrained, usable)
                                |> decide
                                |> Expect.equal "the arrow, no cross" (offer false exp)
                            }
                    ]

                testList
                    "a filter field"
                    [
                        test "several options, one chosen: the cross drops it, the arrow opens the list" {
                            Field.Filter(pick [| "a"; "b" |] (Some "a") true)
                            |> decide
                            |> Expect.equal "cross and open" (offer true Arrow.Open)
                        }

                        test "several options, none chosen: the arrow opens the list, nothing to drop" {
                            Field.Filter(pick [| "a"; "b" |] None true)
                            |> decide
                            |> Expect.equal "open only" (offer false Arrow.Open)
                        }

                        test "one option, chosen by the user or by the field: neither cross nor arrow" {
                            Field.Filter(pick [| "a" |] (Some "a") true)
                            |> decide
                            |> Expect.equal "nothing" none

                            Field.Filter(pick [| "a" |] None true) |> decide |> Expect.equal "nothing" none
                        }

                        test "no options, or not enabled: nothing" {
                            Field.Filter(pick [||] None true) |> decide |> Expect.equal "nothing" none

                            Field.Filter(pick [| "a"; "b" |] (Some "a") false)
                            |> decide
                            |> Expect.equal "nothing" none
                        }
                    ]

                testList
                    "a field whose empty state stays"
                    [
                        test "an anonymous patient's weight: the cross while it holds one, the arrow" {
                            Field.Entry(true, true, true)
                            |> decide
                            |> Expect.equal "cross and open" (offer true Arrow.Open)

                            Field.Entry(true, true, false)
                            |> decide
                            |> Expect.equal "open only" (offer false Arrow.Open)
                        }

                        test "an identified patient's weight, or the department: the arrow, no cross" {
                            Field.Entry(true, false, true)
                            |> decide
                            |> Expect.equal "open only" (offer false Arrow.Open)
                        }

                        test "a read-only field: nothing" {
                            Field.Entry(false, true, true) |> decide |> Expect.equal "nothing" none
                        }
                    ]

                testList
                    "a click on the arrow"
                    [
                        test "opens a list at once, or clears and waits" {
                            offer true Arrow.Open |> click false |> Expect.equal "open" Click.OpenList

                            offer false Arrow.ReopenCleared
                            |> click false
                            |> Expect.equal "clear" Click.ClearAndWait
                        }

                        test "does nothing while a request is under way, or without an arrow" {
                            offer false Arrow.ReopenCleared
                            |> click true
                            |> Expect.equal "loading" Click.Nothing

                            offer true Arrow.Open |> click true |> Expect.equal "loading" Click.Nothing
                            none |> click false |> Expect.equal "no arrow" Click.Nothing
                        }
                    ]

                testList
                    "after a reopen"
                    [
                        test "a list of values opens, also a list of one" {
                            reopened 3 |> Expect.equal "list" Reopened.ShowList
                            reopened 1 |> Expect.equal "list" Reopened.ShowList
                        }

                        test "no values answered, a range it can step or one it cannot: no list" {
                            reopened 0 |> Expect.equal "no list" Reopened.NoList
                        }
                    ]

                testList
                    "a list closed"
                    [
                        test "after a reopen without a pick restores the order before the click" {
                            closed Click.ClearAndWait false |> Expect.equal "restore" Closed.Restore
                        }

                        test "with a pick keeps it, after a reopen or not" {
                            closed Click.ClearAndWait true |> Expect.equal "keep" Closed.Keep
                            closed Click.OpenList true |> Expect.equal "keep" Closed.Keep
                        }

                        test "a plain list closed without a pick changed nothing" {
                            closed Click.OpenList false |> Expect.equal "keep" Closed.Keep
                        }
                    ]
            ]
