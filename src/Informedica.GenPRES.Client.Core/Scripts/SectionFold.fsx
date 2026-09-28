// The fold of a section of order fields: open while a value in it is still to be chosen, folded
// to its heading once every value it shows holds one value, and the user's to open and fold in
// between (#984, the preparation section of the dose dialog).
//
// The rule has three inputs: whether the section is solved, the user's last toggle, and a new
// answer of the server. A toggle holds until the solved state changes; an answer that keeps the
// section solved keeps the toggle, one that changes the solved state drops it and the section
// follows the rule again. This script drafts the pure rule for Client.Core and checks each
// transition. The view keys the fold on the order shown as well, so a toggle does not carry over
// to another order; that is the view's, not the rule's.
//
// Run: `dotnet fsi SectionFold.fsx` from this directory after `dotnet run build`, or via the
// FSI MCP after `#I "<this directory>"`.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"
#load "load.fsx"

open Shared.Types
open Shared.Models


/// Whether a section of order fields is open or folded: folded once every value it shows holds
/// one value, open while one is still to be chosen, and the user's to toggle in between.
module SectionFold =

    /// The fold of a section: what the last answer said about its values, and the user's last
    /// toggle since that changed.
    type Fold =
        {
            /// Whether every variable the section shows held one value at the last answer.
            Solved: bool
            /// The user's last toggle since the solved state last changed; None follows the rule.
            Override: bool option
        }


    /// Whether every variable holds one value. An empty section counts as solved: there is
    /// nothing in it left to choose.
    let allSolved (ovars: OrderVariable seq) =
        ovars |> Seq.forall Order.OrderVariable.isSolved


    /// The fold of a section as first shown: it follows the rule.
    let initial solved = { Solved = solved; Override = None }


    /// An answer of the server. A change of the solved state drops the user's toggle, so the
    /// section follows the rule again; no change keeps the toggle.
    let observe solved fold =
        if solved = fold.Solved then fold else initial solved


    /// Open while a value is to be chosen, folded once all are solved, unless the user said
    /// otherwise since.
    let isOpen fold =
        fold.Override |> Option.defaultValue (not fold.Solved)


    /// The user opens a folded section or folds an open one.
    let toggle fold =
        { fold with Override = Some(not (isOpen fold)) }



module Tests =

    open Expecto
    open Expecto.Flip


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


    let tests =
        testList
            "SectionFold"
            [
                testList
                    "allSolved"
                    [
                        test "every variable with one value is solved" {
                            [ oneVal; oneVal ]
                            |> SectionFold.allSolved
                            |> Expect.isTrue "should be solved"
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
                            []
                            |> SectionFold.allSolved
                            |> Expect.isTrue "should be solved"
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

                            fold
                            |> SectionFold.observe false
                            |> Expect.equal "should be the same fold" fold
                        }

                        test "an answer with a changed state is the initial fold of that state" {
                            SectionFold.initial false
                            |> SectionFold.toggle
                            |> SectionFold.observe true
                            |> Expect.equal "should be the initial solved fold" (SectionFold.initial true)
                        }
                    ]
            ]


Tests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||]
