module Informedica.GenPRES.Client.Core.Tests.Models.TotalsDisplayTests

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models
open Informedica.GenPRES.Client.Core.Models


let private warning = Warning [| Bold "142"; Normal " ml/kg/dag (120 - 150)" |]

let private valid = Valid [| Bold "80"; Normal " kCal/kg/dag" |]

let private intake =
    { Totals.empty with
        Volume =
            Some
                {
                    Text = warning
                    State = TotalState.AboveNorm
                }
        Energy =
            Some
                {
                    Text = valid
                    State = TotalState.WithinNorm
                }
    }


[<Tests>]
let tests =
    testList
        "TotalsDisplay"
        [
            test "the items of a total are the items of its block" {
                "volume"
                |> TotalsDisplay.substanceToField intake
                |> Expect.equal "the items" (warning |> Severity.items)
            }

            test "a total that is not there has no items" {
                "eiwit" |> TotalsDisplay.substanceToField intake |> Expect.isEmpty "no items"
            }

            test "an unknown name has no items" {
                "onbekend" |> TotalsDisplay.substanceToField intake |> Expect.isEmpty "no items"
            }

            test "a Warning block has the severity Warning" {
                "volume"
                |> TotalsDisplay.substanceSeverity intake
                |> Expect.equal "warning" Severity.Warning
            }

            test "the state of a total is the state it carries" {
                "volume"
                |> TotalsDisplay.substanceState intake
                |> Expect.equal "above" (Some TotalState.AboveNorm)
            }

            test "a total that is not there has no state" {
                "eiwit" |> TotalsDisplay.substanceState intake |> Expect.isNone "no state"
            }

            test "a Valid block has the severity Normal" {
                "energie"
                |> TotalsDisplay.substanceSeverity intake
                |> Expect.equal "normal" Severity.Normal
            }

            test "a total that is not there has the severity Normal" {
                "eiwit"
                |> TotalsDisplay.substanceSeverity intake
                |> Expect.equal "normal" Severity.Normal
            }
        ]
