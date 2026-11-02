namespace Informedica.GenPRES.Client.Core.Tests.MachinePolicies


/// Whether a change of patient data on the panel asks first.
module NewPatientPolicyTests =

    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Client.Core.MachinePolicies
    open Informedica.GenPRES.Client.Core.Tests.OrderFixtures

    open Expecto
    open Expecto.Flip
    open SessionMachine


    let picked =
        let ctx = context "c-1" "paracetamol"
        { ctx with Filter = { ctx.Filter with Generic = Some "paracetamol" } }

    let notPicked = { picked with Filter = { picked.Filter with Generic = None } }
    let identified = SessionPatientPolicyTests.identified |> SessionView.Open


    [<Tests>]
    let tests =
        testList
            "NewPatientPolicy"
            [
                test "without an identified patient an order asks" {
                    NewPatientPolicy.asks SessionView.Anonymous (Some one) None
                    |> Expect.isTrue "the orders are lost"
                }

                test "without an identified patient a selection asks" {
                    NewPatientPolicy.asks SessionView.Anonymous None (Some picked)
                    |> Expect.isTrue "the selection is lost"
                }

                test "with nothing to lose the edit does not ask" {
                    NewPatientPolicy.asks SessionView.Anonymous (Some(plan [||])) (Some notPicked)
                    |> Expect.isFalse "entering a patient stays smooth"
                }

                test "an identified patient never asks" {
                    NewPatientPolicy.asks identified (Some one) (Some picked)
                    |> Expect.isFalse "a patient data update, not a new patient"
                }
            ]
