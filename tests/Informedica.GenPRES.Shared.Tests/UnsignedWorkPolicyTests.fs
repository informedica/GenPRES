namespace Informedica.GenPRES.Shared.Tests


/// The leave-page guard's policy, linked in from the client project.
module UnsignedWorkPolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types
    open Shared.Api
    open SigningMachine
    open UnsignedWorkPolicy


    let patient = Shared.Models.Patient.empty
    let plan = Shared.Models.OrderPlan.create patient [||]
    let context = Shared.Models.OrderContext.empty

    let prescribing = { context with OrderContext.Filter.Generic = Some "paracetamol" }


    let commands =
        [
            "Recalculate", OrderPlanCommand.Recalculate plan, false
            "Open", OrderPlanCommand.Open(patient, [||]), false
            "Navigate", OrderPlanCommand.Navigate(plan, "c-1", OrderContextCommand.UpdateOrderContext, context), true
            "AddOrderContext", OrderPlanCommand.AddOrderContext(plan, context), true
            "NewOrderContext", OrderPlanCommand.NewOrderContext(plan, NutritionCategory.TPN), true
            "RemoveOrderContexts", OrderPlanCommand.RemoveOrderContexts(plan, [| "c-1" |]), true
        ]


    [<Tests>]
    let tests =
        testList
            "UnsignedWorkPolicy"
            [
                testList
                    "PlanWork.changedBy"
                    [
                        for name, cmd, changes in commands do
                            test $"{name} changes the plan: {changes}" {
                                PlanWork.changedBy cmd
                                |> Expect.equal $"{name} should change the plan: {changes}" changes
                            }
                    ]

                testList
                    "PlanWork.afterCommand"
                    [
                        test "a change turns the plan as signed into changed" {
                            PlanWork.AsSigned
                            |> PlanWork.afterCommand (OrderPlanCommand.AddOrderContext(plan, context))
                            |> Expect.equal "should be changed" PlanWork.Changed
                        }

                        test "a recalculation keeps the plan as signed" {
                            PlanWork.AsSigned
                            |> PlanWork.afterCommand (OrderPlanCommand.Recalculate plan)
                            |> Expect.equal "should stay as signed" PlanWork.AsSigned
                        }

                        test "a recalculation keeps the plan changed" {
                            PlanWork.Changed
                            |> PlanWork.afterCommand (OrderPlanCommand.Recalculate plan)
                            |> Expect.equal "should stay changed" PlanWork.Changed
                        }
                    ]

                testList
                    "hasUnsignedWork"
                    [
                        test "nothing on the workbench, no signature, the plan as signed: no work" {
                            hasUnsignedWork None Signing.Idle PlanWork.AsSigned
                            |> Expect.isFalse "should be no work"
                        }

                        test "a workbench without a generic is no work" {
                            hasUnsignedWork (Some context) Signing.Idle PlanWork.AsSigned
                            |> Expect.isFalse "should be no work"
                        }

                        test "a medication on the workbench is work" {
                            hasUnsignedWork (Some prescribing) Signing.Idle PlanWork.AsSigned
                            |> Expect.isTrue "should be work"
                        }

                        test "a signature under way is work" {
                            hasUnsignedWork None (Signing.Challenged("c-1", plan, None)) PlanWork.AsSigned
                            |> Expect.isTrue "should be work"
                        }

                        test "a plan changed since the version last signed is work" {
                            hasUnsignedWork None Signing.Idle PlanWork.Changed
                            |> Expect.isTrue "should be work"
                        }
                    ]
            ]
