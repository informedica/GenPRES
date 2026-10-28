namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// Whether an order variable is selectable, and what the field shows after a recalculation.
module FieldOpenPolicyTests =

    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open FieldOpenPolicy


    [<Tests>]
    let tests =
        testList
            "FieldOpenPolicy"
            [
                testList
                    "an order variable"
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
                                orderVariable values allowed usable |> Expect.equal "selectable" exp
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
            ]
