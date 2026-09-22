namespace Informedica.GenPRES.Shared.Tests


/// The leave-page guard's policy, linked in from the client project.
module UnsignedWorkPolicyTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types
    open Shared.Api
    open PlanWorkPolicy
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
                    "PlanWork.askedOver"
                    [
                        test "a Sign with no signature under way is asked over the plan's work" {
                            PlanWork.askedOver SigningView.Idle (PlanWork.Changed 1) PlanWork.AsSigned
                            |> Expect.equal "should be the plan's work" (PlanWork.Changed 1)
                        }

                        test "a Sign while one is under way is ignored and keeps the work asked over" {
                            PlanWork.askedOver SigningView.Requesting (PlanWork.Changed 2) (PlanWork.Changed 1)
                            |> Expect.equal "should keep the work asked over" (PlanWork.Changed 1)
                        }

                        test "a second Sign after an edit does not sign the edit" {
                            // Sign over the first change, an order removed, Sign again while the
                            // challenge is fetched, the first signature told
                            let atSign = PlanWork.askedOver SigningView.Idle (PlanWork.Changed 1) PlanWork.AsSigned
                            let work = PlanWork.Changed 1 |> PlanWork.afterCommand remove
                            let atSign = PlanWork.askedOver SigningView.Requesting work atSign

                            work
                            |> PlanWork.afterSigned atSign
                            |> Expect.equal "the removal should stay unsigned" (PlanWork.Changed 2)
                        }
                    ]

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
                            hasUnsignedWork None (SigningView.Challenged(plan, None)) PlanWork.AsSigned
                            |> Expect.isTrue "should be work"

                            hasUnsignedWork None SigningView.Requesting PlanWork.AsSigned
                            |> Expect.isTrue "the challenge asked is work too"
                        }

                        test "a plan changed since the version last signed is work" {
                            hasUnsignedWork None SigningView.Idle (PlanWork.Changed 1)
                            |> Expect.isTrue "should be work"
                        }
                    ]
            ]
