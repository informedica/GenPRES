/// The argumentation policy: when the dose dialog asks for the text, how a text is taken, and
/// how the text is kept over an answer, on the workbench and on the plan.
module Informedica.GenPRES.Client.Core.Tests.ArgumentationPolicyTests

open Expecto
open Expecto.Flip
open Shared.Types
open Informedica.GenPRES.Client.Core.Tests.OrderPlanMachineTests.Fixtures


let text = "Sepsis, hogere dosis in overleg met de apotheek"


/// The plan fixtures' default scenario, one variable marked by the server.
let markedScenario id name =
    let sc = scenario id name

    { sc with
        Order =
            { sc.Order with
                Orderable =
                    { sc.Order.Orderable with
                        Dose =
                            { sc.Order.Orderable.Dose with
                                Quantity = { sc.Order.Orderable.Dose.Quantity with Level = IsAlert }
                            }
                    }
            }
    }


[<Tests>]
let tests =
    testList
        "ArgumentationPolicy"
        [
            test "every variable of an order: sixteen without components, the components' and items' after" {
                let ord = (scenario "o-1" "paracetamol").Order

                ArgumentationPolicy.variables ord |> List.length |> Expect.equal "sixteen" 16

                let ovar = ord.Adjust

                let item: Item =
                    {
                        Name = "i"
                        ComponentQuantity = ovar
                        OrderableQuantity = ovar
                        ComponentConcentration = ovar
                        OrderableConcentration = ovar
                        Dose = ord.Orderable.Dose
                        IsAdditional = false
                    }

                let comp: Component =
                    {
                        Id = "c"
                        Name = "c"
                        Form = ""
                        ComponentQuantity = ovar
                        OrderableQuantity = ovar
                        OrderableCount = ovar
                        OrderQuantity = ovar
                        OrderCount = ovar
                        OrderableConcentration = ovar
                        Dose = ord.Orderable.Dose
                        Items = [| item |]
                    }

                { ord with Orderable = { ord.Orderable with Components = [| comp |] } }
                |> ArgumentationPolicy.variables
                |> List.length
                |> Expect.equal "sixteen, the component's fourteen, the item's twelve" 42
            }

            test "the dialog asks for the text when the one scenario's order is marked, or a text is present" {
                let plain = context "c-1" "paracetamol"

                let marked = { plain with Scenarios = [| markedScenario "o-c-1" "paracetamol" |] }

                ArgumentationPolicy.wanted plain |> Expect.isFalse "nothing marked, no text"
                ArgumentationPolicy.wanted marked |> Expect.isTrue "marked"

                { plain with Argumentation = Some text }
                |> ArgumentationPolicy.wanted
                |> Expect.isTrue "a text present, whatever the marks"

                { marked with Scenarios = [| markedScenario "o-1" "a"; scenario "o-2" "b" |] }
                |> ArgumentationPolicy.wanted
                |> Expect.isFalse "not narrowed to one scenario: no order to argue over yet"
            }

            test "a text is trimmed, and empty is none" {
                ArgumentationPolicy.normalise "  in overleg  "
                |> Expect.equal "trimmed" (Some "in overleg")

                ArgumentationPolicy.normalise "   " |> Expect.isNone "blank"
                ArgumentationPolicy.normalise "" |> Expect.isNone "empty"
                ArgumentationPolicy.normalise null |> Expect.isNone "null"

                (context "c-1" "p" |> ArgumentationPolicy.write " x ").Argumentation
                |> Expect.equal "written normalised" (Some "x")
            }

            test "the plan's context by id gets the text; the answer keeps the client's per context" {
                let written = two |> ArgumentationPolicy.writeIn "c-2" text

                written.OrderContexts
                |> Array.map _.Argumentation
                |> Expect.equal "c-2 only" [| None; Some text |]

                two
                |> ArgumentationPolicy.writeIn "c-9" text
                |> Expect.equal "unknown id: unchanged" two

                let answered =
                    plan
                        [|
                            context "c-1" "paracetamol"
                            context "c-2" "ibuprofen"
                            context "c-3" "new"
                        |]

                let kept =
                    answered
                    |> ArgumentationPolicy.keepAll (written |> ArgumentationPolicy.writeIn "c-1" "held")

                kept.OrderContexts
                |> Array.map _.Argumentation
                |> Expect.equal "the client's on c-1 and c-2, the answer's on c-3" [| Some "held"; Some text; None |]
            }
        ]
