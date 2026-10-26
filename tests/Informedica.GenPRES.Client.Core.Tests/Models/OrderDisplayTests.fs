/// The values of an order as the pages render them.
module Informedica.GenPRES.Client.Core.Tests.Models.OrderDisplayTests

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models
open Informedica.GenPRES.Client.Core.Models


/// A value unit with an explicit unit, one value per element
let vu u vals =
    Order.ValueUnit.create (vals |> Array.map (fun v -> (string v, v))) u "mass" true "nl" ""

/// A variable with a single value
let valVar u v =
    Order.Variable.create "test" false None false None None false (vu u [| v |] |> Some)

/// A variable with a min and a max, i.e. an unnarrowed range
let rangeVar u min max =
    Order.Variable.create "test" false (vu u [| min |] |> Some) false None (vu u [| max |] |> Some) false None

let emptyVar = Order.Variable.create "test" false None false None None false None


[<Tests>]
let tests =
    testList
        "Variable renderValues"
        [

            // https://github.com/informedica/GenPRES/issues/485
            test "two items with a range each are rendered as two ranges" {
                // the decimal separator is culture dependent, so it is
                // interpolated instead of written out
                let exp = $"{91.7m}-{104m} / {9.17m}-{10.4m} mg/kg/dag"

                [| rangeVar "mg/kg/dag" 91.7m 104m; rangeVar "mg/kg/dag" 9.17m 10.4m |]
                |> OrderDisplay.Variable.renderValues 3
                |> Expect.equal $"should render as {exp}" exp
            }

            test "three items with a range each are rendered as three ranges" {
                let exp = "1-2 / 3-4 / 5-6 mg"

                [| rangeVar "mg" 1m 2m; rangeVar "mg" 3m 4m; rangeVar "mg" 5m 6m |]
                |> OrderDisplay.Variable.renderValues 3
                |> Expect.equal $"should render as {exp}" exp
            }

            test "two items with a single value are slash separated" {
                let exp = "10/5 mg"

                [| valVar "mg" 10m; valVar "mg" 5m |]
                |> OrderDisplay.Variable.renderValues 3
                |> Expect.equal $"should render as {exp}" exp
            }

            test "one item with a range is rendered as a range" {
                let exp = "10-20 mg"

                [| rangeVar "mg" 10m 20m |]
                |> OrderDisplay.Variable.renderValues 3
                |> Expect.equal $"should render as {exp}" exp
            }

            test "one item with a single value is rendered as that value" {
                let exp = "500 mg"

                [| valVar "mg" 500m |]
                |> OrderDisplay.Variable.renderValues 3
                |> Expect.equal $"should render as {exp}" exp
            }

            test "an empty variable renders as an empty string" {
                [| emptyVar |]
                |> OrderDisplay.Variable.renderValues 3
                |> Expect.equal "should render as an empty string" ""
            }

            test "an empty variable is skipped" {
                let exp = "10 mg"

                [| valVar "mg" 10m; emptyVar |]
                |> OrderDisplay.Variable.renderValues 3
                |> Expect.equal $"should render as {exp}" exp
            }

            test "no variables render as an empty string" {
                [||]
                |> OrderDisplay.Variable.renderValues 3
                |> Expect.equal "should render as an empty string" ""
            }
        ]
