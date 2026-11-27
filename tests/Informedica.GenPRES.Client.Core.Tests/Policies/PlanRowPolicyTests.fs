namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// Which rows of the order plan table are checked when the user checks or unchecks one.
module PlanRowPolicyTests =

    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open PlanContextPolicyTests
    open PlanContextPolicyTests.LinkFixtures


    /// The rows checked when the table asks for the rows given over the plan with the rows before checked.
    let rowsAfter before asked =
        PlanRowPolicy.checkedRows { plan calculated with Filtered = before } asked
        |> List.ofArray


    [<Tests>]
    let planRowTests =
        testList
            "PlanRowPolicy"
            [
                test "checking breast milk checks the supplements linked to it" {
                    rowsAfter [||] [| "c-1" |] |> Expect.equal "the group" [ "c-1"; "c-2"; "c-3" ]
                }

                test "unchecking breast milk unchecks the supplements linked to it" {
                    rowsAfter [| "c-1"; "c-2"; "c-3"; "c-4" |] [| "c-2"; "c-3"; "c-4" |]
                    |> Expect.equal "paracetamol alone" [ "c-4" ]
                }

                test "a supplement stays checked while breast milk is checked" {
                    rowsAfter [| "c-1"; "c-2"; "c-3" |] [| "c-1"; "c-3" |]
                    |> Expect.equal "the group" [ "c-1"; "c-2"; "c-3" ]
                }

                test "a supplement is checked alone while breast milk is not, and unchecked alone" {
                    rowsAfter [||] [| "c-2" |] |> Expect.equal "the fortifier alone" [ "c-2" ]
                    rowsAfter [| "c-2"; "c-3" |] [| "c-3" |]
                    |> Expect.equal "Fantomalt alone" [ "c-3" ]
                }

                test "checking an order linked to nothing checks that order only" {
                    rowsAfter [| "c-5"; "c-6" |] [| "c-5"; "c-6"; "c-4" |]
                    |> Expect.equal "in the order of the plan" [ "c-4"; "c-5"; "c-6" ]
                }

                test "unchecking every row unchecks every row" {
                    rowsAfter [| "c-1"; "c-2"; "c-3"; "c-5"; "c-6" |] [||]
                    |> Expect.isEmpty "nothing"
                }
            ]
