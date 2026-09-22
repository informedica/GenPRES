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

    let add = OrderPlanCommand.AddOrderContext(plan, context)
    let remove = OrderPlanCommand.RemoveOrderContexts(plan, [| "c-1" |])
    let recalculate = OrderPlanCommand.Recalculate plan


    let commands =
        [
            "Recalculate", recalculate, false
            "Open", OrderPlanCommand.Open(patient, [||]), false
            "Navigate", OrderPlanCommand.Navigate(plan, "c-1", OrderContextCommand.UpdateOrderContext, context), true
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
                        test "a change turns the plan as signed into its first change" {
                            PlanWork.AsSigned
                            |> PlanWork.afterCommand add
                            |> Expect.equal "should be the first change" (PlanWork.Changed 1)
                        }

                        test "a second change counts" {
                            PlanWork.AsSigned
                            |> PlanWork.afterCommand add
                            |> PlanWork.afterCommand remove
                            |> Expect.equal "should be the second change" (PlanWork.Changed 2)
                        }

                        test "a recalculation keeps the plan as signed" {
                            PlanWork.AsSigned
                            |> PlanWork.afterCommand recalculate
                            |> Expect.equal "should stay as signed" PlanWork.AsSigned
                        }

                        test "a recalculation keeps the plan changed" {
                            PlanWork.Changed 1
                            |> PlanWork.afterCommand recalculate
                            |> Expect.equal "should stay changed" (PlanWork.Changed 1)
                        }
                    ]

                testList
                    "PlanWork.afterSigned"
                    [
                        test "the plan the signature was asked over is as signed" {
                            PlanWork.Changed 1
                            |> PlanWork.afterSigned (PlanWork.Changed 1)
                            |> Expect.equal "should be as signed" PlanWork.AsSigned
                        }

                        test "a change made while the signature was under way stays" {
                            PlanWork.Changed 1
                            |> PlanWork.afterCommand remove
                            |> PlanWork.afterSigned (PlanWork.Changed 1)
                            |> Expect.equal "should stay changed" (PlanWork.Changed 2)
                        }

                        test "a plan as signed, signed again, is as signed" {
                            PlanWork.AsSigned
                            |> PlanWork.afterSigned PlanWork.AsSigned
                            |> Expect.equal "should be as signed" PlanWork.AsSigned
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
                            hasUnsignedWork None Signing.Idle (PlanWork.Changed 1)
                            |> Expect.isTrue "should be work"
                        }
                    ]
            ]
