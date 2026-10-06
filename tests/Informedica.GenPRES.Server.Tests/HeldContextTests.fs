/// The check at the challenge: every order context new or changed since the head states the
/// plan's patient context. A context is new or changed when the head does not hold its id, or
/// holds it with other content; the age of the patient data and the intake the
/// totals recompute are no change. The contexts the head holds unchanged are exempt.
module Informedica.GenPRES.Server.Tests.HeldContextTests

open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Shared.Types
open ServerApi
open Informedica.GenPRES.Server.Tests.StubAdapterTests.SessionStubTests


let ten = { Shared.Models.Patient.Age.ageZero with Age.Years = 10<year> }
let eleven = { Shared.Models.Patient.Age.ageZero with Age.Years = 11<year> }

let weighing (kg: int) (p: Patient) =
    { p with Weight = { p.Weight with Measured = Some(kg * 1000<gram>) } }

let patient = { Shared.Models.Patient.empty with Age = Some ten } |> weighing 30

let context (pat: Patient) (id: string) =
    { Shared.Models.OrderContext.empty with
        Id = id
        Patient = pat
        Scenarios = [| scenarioWithOrder $"o-%s{id}" |]
    }


/// The plan of the client with every context at the age given, parsed as the signing handler
/// parses it: as sent.
let parsedAt (age: Age option) (plan: OrderPlan) =
    let at (p: Patient) = { p with Age = age }

    { plan with
        Patient = at plan.Patient
        OrderContexts = plan.OrderContexts |> Array.map (fun c -> { c with Patient = at c.Patient })
    }
    |> parsed


/// A head as the record holds it: the order plan signed, in the domain.
let head =
    Shared.Models.OrderPlan.create patient [| context patient "c1"; context patient "c2" |]
    |> parsedAt (Some ten)


/// The head as the client holds it after an open: back through the Dto to the contract.
let opened = head |> OrderPlan.Dto.toDto |> OrderPlanMapper.toModel false


/// The opened head with contexts added.
let adding (contexts: OrderContext[]) =
    { opened with OrderContexts = Array.append opened.OrderContexts contexts }


[<Tests>]
let tests =
    testList
        "the check at the challenge"
        [
            test "every context of the head, sent back unchanged, is not changed" {
                opened
                |> parsedAt (Some ten)
                |> Session.changedContexts (Some head)
                |> Expect.isEmpty "no context of the head is new or changed"
            }

            test "a context of the head sent back at another age is not changed" {
                opened
                |> parsedAt (Some eleven)
                |> Session.changedContexts (Some head)
                |> Expect.isEmpty "the age is left out of the comparison"
            }

            test "an order added is new" {
                adding [| context patient "c3" |]
                |> parsedAt (Some ten)
                |> Session.changedContexts (Some head)
                |> Array.map _.Id
                |> Expect.equal "the added context" [| "c3" |]
            }

            test "a plan whose new orders share its patient context passes" {
                adding [| context patient "c3" |]
                |> parsedAt (Some ten)
                |> Session.differingContexts (Some head)
                |> Expect.isEmpty "every new order is on the plan's patient"
            }

            testList
                "a new order on other patient data differs"
                [
                    for name, other in
                        [
                            "weight", patient |> weighing 35
                            "department", { patient with Department = Some "NEO" }
                            "location", { patient with Location = Some "PICU" }
                            "renal function", { patient with RenalFunction = Some(EGFR(Some 10, Some 30)) }
                        ] do
                        test name {
                            adding [| context other "c3" |]
                            |> parsedAt (Some ten)
                            |> Session.differingContexts (Some head)
                            |> Expect.equal "the new order differs" [| "c3" |]
                        }
                ]

            test "the orders of the head on older patient data pass" {
                // the weight changed while released: the plan's patient follows, the head's
                // orders stay as they were signed
                { opened with Patient = opened.Patient |> weighing 35 }
                |> parsedAt (Some ten)
                |> Session.differingContexts (Some head)
                |> Expect.isEmpty "the head's orders are exempt"
            }

            test "a plan without a head is checked whole" {
                Shared.Models.OrderPlan.create patient [| context patient "c1"; context (patient |> weighing 35) "c2" |]
                |> parsedAt (Some ten)
                |> Session.differingContexts None
                |> Expect.equal "every context is new" [| "c2" |]
            }

            test "a changed order of the head on other patient data differs" {
                let changed = { context (patient |> weighing 35) "c2" with Scenarios = [| scenarioWithOrder "o-new" |] }

                { opened with
                    OrderContexts = opened.OrderContexts |> Array.map (fun c -> if c.Id = "c2" then changed else c)
                }
                |> parsedAt (Some ten)
                |> Session.differingContexts (Some head)
                |> Expect.equal "the changed order differs" [| "c2" |]
            }
        ]
