module Informedica.GenPRES.Client.Core.Tests.Policies.PatientDataSummaryPolicyTests

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Api
open Informedica.GenPRES.Client.Core.Helpers
open Informedica.GenPRES.Client.Core.Policies.PatientDataSummaryPolicy
open Informedica.GenPRES.Client.Core.Tests.OrderFixtures


let marked (level: Level) (sc: OrderScenario) =
    let cmp = defaultFor<Component>()
    let itm = defaultFor<Item>()
    let quantity = { itm.ComponentQuantity with Level = level }
    let cmp = { cmp with Items = [| { itm with ComponentQuantity = quantity } |] }

    { sc with Order = { sc.Order with Orderable = { sc.Order.Orderable with Components = [| cmp |] } } }


let planOf scenarios =
    scenarios
    |> Array.mapi (fun i sc -> { context $"c-{i}" "x" with Scenarios = [| sc |] })
    |> plan


let update tp = OrderPlanCommand.UpdatePatient(other, tp)


[<Tests>]
let tests =
    testList
        "PatientDataSummaryPolicy"
        [
            test "counts every order, and the orders with a mark once each" {
                let sc = scenario "o" "x"

                let tp =
                    planOf
                        [|
                            sc
                            sc |> marked IsAlert
                            { sc with RulesNoLongerApply = true }
                            { (sc |> marked IsWarning) with RulesNoLongerApply = true }
                        |]

                summaryOf (update tp) tp
                |> Expect.equal
                    "four orders, three marked"
                    (Some
                        {
                            Orders = 4
                            Marked = 3
                        })
            }

            test "a caution counts, since the rules mark it as they mark an alert" {
                let tp = planOf [| scenario "o" "x" |> marked IsCaution |]

                summaryOf (update tp) tp
                |> Expect.equal
                    "one order, marked"
                    (Some
                        {
                            Orders = 1
                            Marked = 1
                        })
            }

            test "another answer has no summary" {
                summaryOf (OrderPlanCommand.FilterRows([||], one)) one
                |> Expect.isNone "only an update"
            }

            test "a plan without orders has no summary" {
                let empty = plan [||]
                summaryOf (update empty) empty |> Expect.isNone "nothing to report"
            }

            test "the sentence fills the counts" {
                english
                |> sentence
                    {
                        Orders = 5
                        Marked = 2
                    }
                |> Expect.equal
                    "the words"
                    "Patient data changed: 5 order(s) updated, 2 with a notification. The selection for prescribing was cleared."
            }

            test "only a marked order asks for attention" {
                Alert.Alert.PatientDataChanged
                    {
                        Orders = 5
                        Marked = 0
                    }
                |> Alert.severity
                |> Expect.equal "success" Alert.Severity.Success

                Alert.Alert.PatientDataChanged
                    {
                        Orders = 5
                        Marked = 1
                    }
                |> Alert.severity
                |> Expect.equal "attention" Alert.Severity.Warning
            }
        ]
