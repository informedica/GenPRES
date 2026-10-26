namespace Informedica.GenPRES.Client.Core.Tests.Policies


/// The plan's work beside the version last opened or signed, linked in from the client project.
module PlanWorkPolicyTests =

    open Informedica.GenPRES.Client.Core.Policies

    open Expecto
    open Expecto.Flip
    open Informedica.GenPRES.Shared.Types
    open Informedica.GenPRES.Shared.Api
    open PlanWorkPolicy


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
            "PlanWorkPolicy"
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
                            |> Expect.equal "should be the first change" PlanWork.Changed
                        }

                        test "a second change keeps the plan changed" {
                            PlanWork.AsSigned
                            |> PlanWork.afterCommand add
                            |> PlanWork.afterCommand remove
                            |> Expect.equal "should stay changed" PlanWork.Changed
                        }

                        for name, cmd in [ "a patient update", updatePatient; "a row filter", filterRows ] do
                            test $"%s{name} keeps the plan as signed" {
                                PlanWork.AsSigned
                                |> PlanWork.afterCommand cmd
                                |> Expect.equal "should stay as signed" PlanWork.AsSigned
                            }

                            test $"%s{name} keeps the plan changed" {
                                PlanWork.Changed
                                |> PlanWork.afterCommand cmd
                                |> Expect.equal "should stay changed" PlanWork.Changed
                            }
                    ]
            ]
