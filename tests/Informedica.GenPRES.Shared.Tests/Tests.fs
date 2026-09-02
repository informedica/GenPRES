namespace Informedica.GenPRES.Shared.Tests


module Tests =

    open Expecto
    open Expecto.Flip

    open Shared.Models

    let testHelloWorld =
        test "hello world test" { "Hello World" |> Expect.equal "Strings should be equal" "Hello World" }


    module OrderTests =

        module OrderVariableTests =

            open Shared.Types

            let emptyVar = Order.Variable.create "test" false None false None None false None

            let vu vals =
                Order.ValueUnit.create (vals |> Array.map (fun v -> (string v, v))) "mg" "mass" true "nl" ""

            let ovar = Order.OrderVariable.create "testOrderVar" emptyVar emptyVar emptyVar None


            let tests =
                testList
                    "OrderVariable active pattern"
                    [

                        test "no incr and no vals returns NonNavigable" {
                            let ovar =
                                Order.OrderVariable.create "test" emptyVar emptyVar emptyVar None IsNormal

                            match ovar with
                            | Order.OrderVariable.NonNavigable -> ()
                            | _ -> failtest "expected NonNavigable"
                        }

                        test "no incr with vals None returns NonNavigable" {
                            let varWithMin =
                                Order.Variable.create "test" false (vu [| 0m |] |> Some) false None None false None

                            let ovar =
                                Order.OrderVariable.create "test" emptyVar emptyVar varWithMin None IsNormal

                            match ovar with
                            | Order.OrderVariable.NonNavigable -> ()
                            | _ -> failtest "expected NonNavigable"
                        }

                        test "multiple vals returns Selectable" {
                            let varWithVals =
                                Order.Variable.create
                                    "test"
                                    false
                                    None
                                    false
                                    None
                                    None
                                    false
                                    (vu [| 1m; 2m; 3m |] |> Some)

                            let ovar =
                                Order.OrderVariable.create "test" emptyVar emptyVar varWithVals None IsNormal

                            match ovar with
                            | Order.OrderVariable.Selectable -> ()
                            | _ -> failtest "expected Selectable"
                        }

                        test "two vals returns Selectable" {
                            let varWithVals =
                                Order.Variable.create "test" false None false None None false (vu [| 5m; 10m |] |> Some)

                            let ovar =
                                Order.OrderVariable.create "test" emptyVar emptyVar varWithVals None IsNormal

                            match ovar with
                            | Order.OrderVariable.Selectable -> ()
                            | _ -> failtest "expected Selectable"
                        }

                        test "one val with defined incr returns Stepable" {
                            let varWithOneVal =
                                Order.Variable.create "test" false None false None None false (vu [| 1m |] |> Some)

                            let defWithIncr =
                                Order.Variable.create "test" false None false (vu [| 0.5m |] |> Some) None false None

                            let ovar =
                                Order.OrderVariable.create "test" defWithIncr emptyVar varWithOneVal None IsNormal

                            match ovar with
                            | Order.OrderVariable.Stepable -> ()
                            | _ -> failtest "expected Stepable"
                        }

                        test "one val without defined incr returns NonNavigable" {
                            let varWithOneVal =
                                Order.Variable.create "test" false None false None None false (vu [| 1m |] |> Some)

                            let ovar =
                                Order.OrderVariable.create "test" emptyVar emptyVar varWithOneVal None IsNormal

                            match ovar with
                            | Order.OrderVariable.NonNavigable -> ()
                            | _ -> failtest "expected NonNavigable"
                        }

                        test "defined incr with min and max returns Navigable" {
                            let varWithMinMax =
                                Order.Variable.create
                                    "test"
                                    false
                                    (vu [| 0m |] |> Some)
                                    false
                                    None
                                    (vu [| 100m |] |> Some)
                                    false
                                    None

                            let defWithIncr =
                                Order.Variable.create "test" false None false (vu [| 1m |] |> Some) None false None

                            let ovar =
                                Order.OrderVariable.create "test" defWithIncr emptyVar varWithMinMax None IsNormal

                            match ovar with
                            | Order.OrderVariable.Navigable -> ()
                            | _ -> failtest "expected Navigable"
                        }

                        test "defined incr with min but no max returns NonNavigable" {
                            let varWithMinOnly =
                                Order.Variable.create "test" false (vu [| 0m |] |> Some) false None None false None

                            let defWithIncr =
                                Order.Variable.create "test" false None false (vu [| 1m |] |> Some) None false None

                            let ovar =
                                Order.OrderVariable.create "test" defWithIncr emptyVar varWithMinOnly None IsNormal

                            match ovar with
                            | Order.OrderVariable.NonNavigable -> ()
                            | _ -> failtest "expected NonNavigable"
                        }

                        test "defined incr with max but no min returns NonNavigable" {
                            let varWithMaxOnly =
                                Order.Variable.create "test" false None false None (vu [| 100m |] |> Some) false None

                            let defWithIncr =
                                Order.Variable.create "test" false None false (vu [| 1m |] |> Some) None false None

                            let ovar =
                                Order.OrderVariable.create "test" defWithIncr emptyVar varWithMaxOnly None IsNormal

                            match ovar with
                            | Order.OrderVariable.NonNavigable -> ()
                            | _ -> failtest "expected NonNavigable"
                        }

                        test "multiple vals takes priority over incr with min/max" {
                            let varWithValsAndMinMax =
                                Order.Variable.create
                                    "test"
                                    false
                                    (vu [| 0m |] |> Some)
                                    false
                                    None
                                    (vu [| 100m |] |> Some)
                                    false
                                    (vu [| 1m; 2m |] |> Some)

                            let defWithIncr =
                                Order.Variable.create "test" false None false (vu [| 1m |] |> Some) None false None

                            let ovar =
                                Order.OrderVariable.create
                                    "test"
                                    defWithIncr
                                    emptyVar
                                    varWithValsAndMinMax
                                    None
                                    IsNormal

                            match ovar with
                            | Order.OrderVariable.Selectable -> ()
                            | _ -> failtest "expected Selectable, multiple vals should take priority"
                        }
                    ]


        module VariableRenderTests =

            open Shared.Types

            /// A value unit with an explicit unit, one value per element
            let vu u vals =
                Order.ValueUnit.create (vals |> Array.map (fun v -> (string v, v))) u "mass" true "nl" ""

            /// A variable with a single value
            let valVar u v =
                Order.Variable.create "test" false None false None None false (vu u [| v |] |> Some)

            /// A variable with a min and a max, i.e. an unnarrowed range
            let rangeVar u min max =
                Order.Variable.create
                    "test"
                    false
                    (vu u [| min |] |> Some)
                    false
                    None
                    (vu u [| max |] |> Some)
                    false
                    None

            let emptyVar = Order.Variable.create "test" false None false None None false None


            let tests =
                testList
                    "Variable renderValues"
                    [

                        // https://github.com/informedica/GenPRES/issues/485
                        test "two items with a range each are rendered as two ranges" {
                            // the decimal separator is culture dependent, so it is
                            // interpolated instead of written out
                            let exp = $"{91.7m}-{104m} / {9.17m}-{10.4m} mg/kg/dag"

                            [|
                                rangeVar "mg/kg/dag" 91.7m 104m
                                rangeVar "mg/kg/dag" 9.17m 10.4m
                            |]
                            |> Order.Variable.renderValues 3
                            |> Expect.equal $"should render as {exp}" exp
                        }

                        test "three items with a range each are rendered as three ranges" {
                            let exp = "1-2 / 3-4 / 5-6 mg"

                            [|
                                rangeVar "mg" 1m 2m
                                rangeVar "mg" 3m 4m
                                rangeVar "mg" 5m 6m
                            |]
                            |> Order.Variable.renderValues 3
                            |> Expect.equal $"should render as {exp}" exp
                        }

                        test "two items with a single value are slash separated" {
                            let exp = "10/5 mg"

                            [| valVar "mg" 10m; valVar "mg" 5m |]
                            |> Order.Variable.renderValues 3
                            |> Expect.equal $"should render as {exp}" exp
                        }

                        test "one item with a range is rendered as a range" {
                            let exp = "10-20 mg"

                            [| rangeVar "mg" 10m 20m |]
                            |> Order.Variable.renderValues 3
                            |> Expect.equal $"should render as {exp}" exp
                        }

                        test "one item with a single value is rendered as that value" {
                            let exp = "500 mg"

                            [| valVar "mg" 500m |]
                            |> Order.Variable.renderValues 3
                            |> Expect.equal $"should render as {exp}" exp
                        }

                        test "an empty variable renders as an empty string" {
                            [| emptyVar |]
                            |> Order.Variable.renderValues 3
                            |> Expect.equal "should render as an empty string" ""
                        }

                        test "an empty variable is skipped" {
                            let exp = "10 mg"

                            [| valVar "mg" 10m; emptyVar |]
                            |> Order.Variable.renderValues 3
                            |> Expect.equal $"should render as {exp}" exp
                        }

                        test "no variables render as an empty string" {
                            [||]
                            |> Order.Variable.renderValues 3
                            |> Expect.equal "should render as an empty string" ""
                        }
                    ]


    [<Tests>]
    let tests =
        testList
            "GenPRES.Shared"
            [
                testHelloWorld
                OrderTests.OrderVariableTests.tests
                OrderTests.VariableRenderTests.tests
            ]
