/// The tab of an order dialog: kept while the dialog shows the same order, else the scenario's.
module Informedica.GenPRES.Client.Core.Tests.DialogTabPolicyTests

open Expecto
open Expecto.Flip
open Shared.Types
open Informedica.GenPRES.Client.Core.Tests.OrderFixtures


/// A scenario whose order has two components, each with a substance, the second's after an
/// additional one.
let twoComponents =
    let sc = scenario "o-1" "two"
    let ovar = sc.Order.Adjust

    let item name additional : Item =
        {
            Name = name
            ComponentQuantity = ovar
            OrderableQuantity = ovar
            ComponentConcentration = ovar
            OrderableConcentration = ovar
            Dose = sc.Order.Orderable.Dose
            IsAdditional = additional
        }

    let comp name items : Component =
        {
            Id = name
            Name = name
            Form = ""
            ComponentQuantity = ovar
            OrderableQuantity = ovar
            OrderableCount = ovar
            OrderQuantity = ovar
            OrderCount = ovar
            OrderableConcentration = ovar
            Dose = sc.Order.Orderable.Dose
            Items = items
        }

    { sc with
        Order =
            { sc.Order with
                Orderable =
                    { sc.Order.Orderable with
                        Components =
                            [|
                                comp "a" [| item "a1" false |]
                                comp "b" [| item "extra" true; item "b1" false |]
                            |]
                    }
            }
    }


let tabOf id cmp itm : DialogTabPolicy.Tab =
    {
        OrderId = id
        Component = cmp
        Item = itm
    }


[<Tests>]
let tests =
    testList
        "DialogTabPolicy"
        [
            test "without a tab kept: the scenario's own, else the first component and its first substance" {
                [
                    DialogTabPolicy.tab None twoComponents
                    DialogTabPolicy.tab None { twoComponents with Component = Some "b" }
                ]
                |> Expect.equal
                    "the first, then the scenario's, never the additional substance"
                    [ tabOf "o-1" (Some "a") (Some "a1"); tabOf "o-1" (Some "b") (Some "b1") ]
            }

            test "the tab kept for the same order wins over the scenario's" {
                DialogTabPolicy.tab (Some(tabOf "o-1" (Some "b") (Some "b1"))) twoComponents
                |> Expect.equal "the component and item kept" (tabOf "o-1" (Some "b") (Some "b1"))
            }

            test "a tab kept for another order, or for a component gone, is not" {
                [
                    DialogTabPolicy.tab (Some(tabOf "o-2" (Some "b") (Some "b1"))) twoComponents
                    DialogTabPolicy.tab (Some(tabOf "o-1" (Some "gone") None)) twoComponents
                ]
                |> Expect.equal
                    "the scenario's, the first component"
                    [ tabOf "o-1" (Some "a") (Some "a1"); tabOf "o-1" (Some "a") (Some "a1") ]
            }
        ]
