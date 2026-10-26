namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// What a field offers, the cross and whether it is selectable, and what a click and a close do.
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

        let offer cross selectable =
            {
                Cross = cross
                Selectable = selectable
            }

        testList
            "FieldOpenPolicy"
            [
                testList
                    "an order variable never offers the cross"
                    [
                        for values, allowed, usable, exp in
                            [
                                3, false, true, Selectable.List
                                3, true, true, Selectable.List
                                1, true, true, Selectable.ReCalc
                                1, false, true, Selectable.No
                                0, true, true, Selectable.No
                                0, false, true, Selectable.No
                                3, true, false, Selectable.No
                                1, true, false, Selectable.No
                            ] do
                            test $"%i{values} values, allowed %b{allowed}, usable %b{usable}: %A{exp}" {
                                Field.OrderVariable(values, allowed, usable)
                                |> decide
                                |> Expect.equal "selectable, no cross" (offer false exp)
                            }
                    ]

                testList
                    "a filter field"
                    [
                        test "several options, one chosen: the cross drops it, the list opens" {
                            Field.Filter(pick [| "a"; "b" |] (Some "a") true)
                            |> decide
                            |> Expect.equal "cross and open" (offer true Selectable.List)
                        }

                        test "several options, none chosen: the list opens, nothing to drop" {
                            Field.Filter(pick [| "a"; "b" |] None true)
                            |> decide
                            |> Expect.equal "open only" (offer false Selectable.List)
                        }

                        test "one option, chosen by the user or by the field: neither the cross nor a list" {
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
                        test "an anonymous patient's weight: the cross while it holds one, the list" {
                            Field.Entry(true, true, true)
                            |> decide
                            |> Expect.equal "cross and open" (offer true Selectable.List)

                            Field.Entry(true, true, false)
                            |> decide
                            |> Expect.equal "open only" (offer false Selectable.List)
                        }

                        test "an identified patient's weight, or the department: the list, no cross" {
                            Field.Entry(true, false, true)
                            |> decide
                            |> Expect.equal "open only" (offer false Selectable.List)
                        }

                        test "a read-only field: nothing" {
                            Field.Entry(false, true, true) |> decide |> Expect.equal "nothing" none
                        }
                    ]

                testList
                    "a click on a selectable field"
                    [
                        test "opens a list at once, or clears and waits" {
                            offer true Selectable.List |> click false |> Expect.equal "open" Click.OpenList

                            offer false Selectable.ReCalc
                            |> click false
                            |> Expect.equal "clear" Click.ReCalcAndWait
                        }

                        test "does nothing while a request is under way, or when not selectable" {
                            offer false Selectable.ReCalc
                            |> click true
                            |> Expect.equal "loading" Click.Nothing

                            offer true Selectable.List |> click true |> Expect.equal "loading" Click.Nothing
                            none |> click false |> Expect.equal "not selectable" Click.Nothing
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
                            closed Click.ReCalcAndWait false |> Expect.equal "restore" Closed.Restore
                        }

                        test "with a pick keeps it, after a reopen or not" {
                            closed Click.ReCalcAndWait true |> Expect.equal "keep" Closed.Keep
                            closed Click.OpenList true |> Expect.equal "keep" Closed.Keep
                        }

                        test "a plain list closed without a pick changed nothing" {
                            closed Click.OpenList false |> Expect.equal "keep" Closed.Keep
                        }
                    ]
            ]
