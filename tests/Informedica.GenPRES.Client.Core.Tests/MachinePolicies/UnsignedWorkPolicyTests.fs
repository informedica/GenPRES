namespace Informedica.GenPRES.Client.Core.Tests.MachinePolicies


/// The leave-page guard's policy, linked in from the client project.
module UnsignedWorkPolicyTests =

    open Informedica.GenPRES.Client.Core.Policies
    open Informedica.GenPRES.Client.Core.StateMachines
    open Informedica.GenPRES.Client.Core.MachinePolicies

    open Expecto
    open Expecto.Flip
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open PlanWorkPolicy
    open SigningMachine
    open UnsignedWorkPolicy


    let patient = Informedica.GenPRES.Shared.Models.Patient.empty
    let plan = Informedica.GenPRES.Shared.Models.OrderPlan.create patient [||]
    let context = Informedica.GenPRES.Shared.Models.OrderContext.empty

    let prescribing = { context with OrderContext.Filter.Generic = Some "paracetamol" }

    let add = OrderPlanCommand.AddOrderContext(plan, context)
    let remove = OrderPlanCommand.RemoveOrderContexts(plan, [| "c-1" |])
    let updatePatient = OrderPlanCommand.UpdatePatient(patient, plan)
    let filterRows = OrderPlanCommand.FilterRows([| "c-1" |], plan)


    let commands =
        [
            "UpdatePatient", updatePatient, false
            "FilterRows", filterRows, false
            "Open", OrderPlanCommand.Open(patient, [||]), false
            "Navigate",
            OrderPlanCommand.Navigate(
                plan,
                "c-1",
                OrderViewCommand.SetNthFilterProperty(Informedica.GenPRES.Shared.Models.OrderContext.Route, 0),
                context
            ),
            true
            "AddOrderContext", add, true
            "NewOrderContext", OrderPlanCommand.NewOrderContext(plan, NutritionCategory.TPN), true
            "RemoveOrderContexts", remove, true
        ]


    [<Tests>]
    let tests =
        testList
            "UnsignedWorkPolicy"
            [
                testList
                    "hasUnsignedWork"
                    [
                        test "nothing on the workbench, no signature, the plan as signed: no work" {
                            hasUnsignedWork None SigningView.Idle PlanWork.AsSigned
                            |> Expect.isFalse "should be no work"
                        }

                        test "a workbench without a generic is no work" {
                            hasUnsignedWork (Some context) SigningView.Idle PlanWork.AsSigned
                            |> Expect.isFalse "should be no work"
                        }

                        test "a medication on the workbench is work" {
                            hasUnsignedWork (Some prescribing) SigningView.Idle PlanWork.AsSigned
                            |> Expect.isTrue "should be work"
                        }

                        test "a signature under way is work" {
                            hasUnsignedWork None (SigningView.AskingPin(plan, None)) PlanWork.AsSigned
                            |> Expect.isTrue "should be work"

                            hasUnsignedWork None SigningView.RequestingChallenge PlanWork.AsSigned
                            |> Expect.isTrue "the challenge asked is work too"
                        }

                        test "a plan changed since the version last signed is work" {
                            hasUnsignedWork None SigningView.Idle PlanWork.Changed
                            |> Expect.isTrue "should be work"
                        }
                    ]
            ]
