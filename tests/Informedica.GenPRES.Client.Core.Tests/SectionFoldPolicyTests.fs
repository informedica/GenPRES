namespace Informedica.GenPRES.Client.Core.Tests


/// The fold of a section of order fields: folded once solved, open while a value is to be
/// chosen, the user's toggle in between, and a server answer that changes the solved state
/// dropping the toggle.
module SectionFoldPolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types
    open Shared.Models


    let vu vals =
        Order.ValueUnit.create (vals |> Array.map (fun v -> (string v, v))) "mg" "mass" true "nl" ""


    let variable vals =
        Order.Variable.create "test" false None false None None false (vals |> Option.map vu)


    let ovar vals =
        let var = variable vals
        Order.OrderVariable.create "test" (variable None) (variable None) var None IsNormal


    let oneVal = ovar (Some [| 5m |])
    let twoVals = ovar (Some [| 5m; 10m |])
    let noVals = ovar None


    [<Tests>]
    let tests =
        testList
            "SectionFoldPolicy"
            [
                testList
                    "allSolved"
                    [
                        test "every variable with one value is solved" {
                            [ oneVal; oneVal ]
                            |> SectionFoldPolicy.allSolved
                            |> Expect.isTrue "should be solved"
                        }

                        test "one variable with two values is not solved" {
                            [ oneVal; twoVals ]
                            |> SectionFoldPolicy.allSolved
                            |> Expect.isFalse "should not be solved"
                        }

                        test "one variable with no values is not solved" {
                            [ oneVal; noVals ]
                            |> SectionFoldPolicy.allSolved
                            |> Expect.isFalse "should not be solved"
                        }

                        test "an empty section counts as solved" {
                            [] |> SectionFoldPolicy.allSolved |> Expect.isTrue "should be solved"
                        }
                    ]

                testList
                    "isOpen"
                    [
                        test "open while not solved" {
                            SectionFoldPolicy.initial false
                            |> SectionFoldPolicy.isOpen
                            |> Expect.isTrue "should be open"
                        }

                        test "folded when solved" {
                            SectionFoldPolicy.initial true
                            |> SectionFoldPolicy.isOpen
                            |> Expect.isFalse "should be folded"
                        }
                    ]

                testList
                    "toggle"
                    [
                        test "opens a folded section" {
                            SectionFoldPolicy.initial true
                            |> SectionFoldPolicy.toggle
                            |> SectionFoldPolicy.isOpen
                            |> Expect.isTrue "should be open"
                        }

                        test "folds an open section" {
                            SectionFoldPolicy.initial false
                            |> SectionFoldPolicy.toggle
                            |> SectionFoldPolicy.isOpen
                            |> Expect.isFalse "should be folded"
                        }

                        test "a second toggle undoes the first" {
                            SectionFoldPolicy.initial true
                            |> SectionFoldPolicy.toggle
                            |> SectionFoldPolicy.toggle
                            |> SectionFoldPolicy.isOpen
                            |> Expect.isFalse "should be folded again"
                        }
                    ]

                testList
                    "observe"
                    [
                        test "an answer that keeps the solved state keeps the toggle" {
                            SectionFoldPolicy.initial true
                            |> SectionFoldPolicy.toggle
                            |> SectionFoldPolicy.observe true
                            |> SectionFoldPolicy.isOpen
                            |> Expect.isTrue "should stay open as the user said"
                        }

                        test "an answer that solves the section drops the toggle and folds it" {
                            SectionFoldPolicy.initial false
                            |> SectionFoldPolicy.toggle
                            |> SectionFoldPolicy.observe true
                            |> SectionFoldPolicy.isOpen
                            |> Expect.isFalse "should follow the rule and fold"
                        }

                        test "an answer that unsolves the section drops the toggle and opens it" {
                            SectionFoldPolicy.initial true
                            |> SectionFoldPolicy.toggle
                            |> SectionFoldPolicy.toggle
                            |> SectionFoldPolicy.observe false
                            |> SectionFoldPolicy.isOpen
                            |> Expect.isTrue "should follow the rule and open"
                        }

                        test "an answer with the same state and no toggle changes nothing" {
                            let fold = SectionFoldPolicy.initial false

                            fold
                            |> SectionFoldPolicy.observe false
                            |> Expect.equal "should be the same fold" fold
                        }

                        test "an answer with a changed state is the initial fold of that state" {
                            SectionFoldPolicy.initial false
                            |> SectionFoldPolicy.toggle
                            |> SectionFoldPolicy.observe true
                            |> Expect.equal "should be the initial solved fold" (SectionFoldPolicy.initial true)
                        }
                    ]
            ]
