namespace Informedica.GenPRES.Client.Core.Tests


/// The fold of a section of order fields: folded once solved, open while a value is to be
/// chosen, the user's toggle in between, and a server answer that changes the solved state
/// dropping the toggle.
module SectionFoldTests =

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
            "SectionFold"
            [
                testList
                    "allSolved"
                    [
                        test "every variable with one value is solved" {
                            [ oneVal; oneVal ] |> SectionFold.allSolved |> Expect.isTrue "should be solved"
                        }

                        test "one variable with two values is not solved" {
                            [ oneVal; twoVals ]
                            |> SectionFold.allSolved
                            |> Expect.isFalse "should not be solved"
                        }

                        test "one variable with no values is not solved" {
                            [ oneVal; noVals ]
                            |> SectionFold.allSolved
                            |> Expect.isFalse "should not be solved"
                        }

                        test "an empty section counts as solved" {
                            [] |> SectionFold.allSolved |> Expect.isTrue "should be solved"
                        }
                    ]

                testList
                    "isOpen"
                    [
                        test "open while not solved" {
                            SectionFold.initial false
                            |> SectionFold.isOpen
                            |> Expect.isTrue "should be open"
                        }

                        test "folded when solved" {
                            SectionFold.initial true
                            |> SectionFold.isOpen
                            |> Expect.isFalse "should be folded"
                        }
                    ]

                testList
                    "toggle"
                    [
                        test "opens a folded section" {
                            SectionFold.initial true
                            |> SectionFold.toggle
                            |> SectionFold.isOpen
                            |> Expect.isTrue "should be open"
                        }

                        test "folds an open section" {
                            SectionFold.initial false
                            |> SectionFold.toggle
                            |> SectionFold.isOpen
                            |> Expect.isFalse "should be folded"
                        }

                        test "a second toggle undoes the first" {
                            SectionFold.initial true
                            |> SectionFold.toggle
                            |> SectionFold.toggle
                            |> SectionFold.isOpen
                            |> Expect.isFalse "should be folded again"
                        }
                    ]

                testList
                    "observe"
                    [
                        test "an answer that keeps the solved state keeps the toggle" {
                            SectionFold.initial true
                            |> SectionFold.toggle
                            |> SectionFold.observe true
                            |> SectionFold.isOpen
                            |> Expect.isTrue "should stay open as the user said"
                        }

                        test "an answer that solves the section drops the toggle and folds it" {
                            SectionFold.initial false
                            |> SectionFold.toggle
                            |> SectionFold.observe true
                            |> SectionFold.isOpen
                            |> Expect.isFalse "should follow the rule and fold"
                        }

                        test "an answer that unsolves the section drops the toggle and opens it" {
                            SectionFold.initial true
                            |> SectionFold.toggle
                            |> SectionFold.toggle
                            |> SectionFold.observe false
                            |> SectionFold.isOpen
                            |> Expect.isTrue "should follow the rule and open"
                        }

                        test "an answer with the same state and no toggle changes nothing" {
                            let fold = SectionFold.initial false

                            fold |> SectionFold.observe false |> Expect.equal "should be the same fold" fold
                        }

                        test "an answer with a changed state is the initial fold of that state" {
                            SectionFold.initial false
                            |> SectionFold.toggle
                            |> SectionFold.observe true
                            |> Expect.equal "should be the initial solved fold" (SectionFold.initial true)
                        }
                    ]
            ]
