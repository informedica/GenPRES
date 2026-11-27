namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// The rows of the composition of a nutrition order and the command a checkbox of an additive sends.
module CompositionPolicyTests =

    open Informedica.GenPRES.Shared.Api
    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open CompositionPolicy

    let private rule = [| "Samenstelling C"; "NaCl 3%"; "KCl 7,4%"; "gluc 20%" |]
    let private additives = [| "NaCl 3%"; "KCl 7,4%" |]

    // the rows of the rule with the components the order holds, without a diluent
    let private rowsOf components = rows rule components additives None [||]

    [<Tests>]
    let tests =
        testList
            "CompositionPolicy"
            [
                testList
                    "rows"
                    [
                        test "every additive held: the components in the order of the rule" {
                            rowsOf rule
                            |> Expect.equal
                                "rows"
                                [|
                                    Row.Fixed "Samenstelling C"
                                    Row.Additive("NaCl 3%", true)
                                    Row.Additive("KCl 7,4%", true)
                                    Row.Fixed "gluc 20%"
                                |]
                        }

                        test "the last additive left out keeps its place before a component that is not an additive" {
                            rowsOf [| "Samenstelling C"; "NaCl 3%"; "gluc 20%" |]
                            |> Expect.equal
                                "rows"
                                [|
                                    Row.Fixed "Samenstelling C"
                                    Row.Additive("NaCl 3%", true)
                                    Row.Additive("KCl 7,4%", false)
                                    Row.Fixed "gluc 20%"
                                |]
                        }

                        test "every additive left out: each in its place, unchecked" {
                            rowsOf [| "Samenstelling C"; "gluc 20%" |]
                            |> Expect.equal
                                "rows"
                                [|
                                    Row.Fixed "Samenstelling C"
                                    Row.Additive("NaCl 3%", false)
                                    Row.Additive("KCl 7,4%", false)
                                    Row.Fixed "gluc 20%"
                                |]
                        }

                        test "the diluent comes last, with the diluents to choose from" {
                            rows
                                [| "Samenstelling C"; "NaCl 3%" |]
                                [| "Samenstelling C"; "NaCl 3%"; "gluc 30%" |]
                                [| "NaCl 3%" |]
                                (Some "gluc 30%")
                                [| "gluc 20%"; "gluc 30%" |]
                            |> Expect.equal
                                "rows"
                                [|
                                    Row.Fixed "Samenstelling C"
                                    Row.Additive("NaCl 3%", true)
                                    Row.Diluent("gluc 30%", [| "gluc 20%"; "gluc 30%" |])
                                |]
                        }

                        test "one diluent offered: no choice" {
                            rows
                                [| "Samenstelling C" |]
                                [| "Samenstelling C"; "gluc 20%" |]
                                [||]
                                (Some "gluc 20%")
                                [| "gluc 20%" |]
                            |> Array.last
                            |> Expect.equal "the diluent" (Row.Diluent("gluc 20%", [||]))
                        }

                        test "no diluent: no diluent row" {
                            rowsOf rule
                            |> Array.exists (
                                function
                                | Row.Diluent _ -> true
                                | _ -> false
                            )
                            |> Expect.isFalse "no diluent row"
                        }
                    ]

                testList
                    "toggle"
                    [
                        let held = rowsOf rule
                        let oneHeld = rowsOf [| "Samenstelling C"; "KCl 7,4%"; "gluc 20%" |]
                        let noneHeld = rowsOf [| "Samenstelling C"; "gluc 20%" |]

                        test "switching off an additive selects the others" {
                            toggle additives held "NaCl 3%"
                            |> Expect.equal "KCl only" (OrderViewCommand.SetNthComponentsProperty [| 1 |])
                        }

                        test "switching on an additive selects it with the others, in the order offered" {
                            toggle additives oneHeld "NaCl 3%"
                            |> Expect.equal "both" (OrderViewCommand.SetNthComponentsProperty [| 0; 1 |])
                        }

                        test "switching off the last additive selects none" {
                            toggle additives oneHeld "KCl 7,4%"
                            |> Expect.equal "none" (OrderViewCommand.SetNthComponentsProperty [||])
                        }

                        test "switching on the last additive left out selects both" {
                            toggle additives (rowsOf [| "Samenstelling C"; "NaCl 3%"; "gluc 20%" |]) "KCl 7,4%"
                            |> Expect.equal "both" (OrderViewCommand.SetNthComponentsProperty [| 0; 1 |])
                        }

                        test "switching on from none selects the one" {
                            toggle additives noneHeld "KCl 7,4%"
                            |> Expect.equal "KCl" (OrderViewCommand.SetNthComponentsProperty [| 1 |])
                        }
                    ]
            ]
