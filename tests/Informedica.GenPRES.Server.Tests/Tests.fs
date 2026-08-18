namespace Informedica.GenPRES.Server.Tests

open Expecto
open Informedica.GenForm.Lib

module TotalsTests =
    open System
    open Shared
    open Shared.Types
    open Swensen.Unquote

    type TotalsSpy() =
        let mutable callCount = 0
        member this.CallCount = callCount

        interface Resources.IResourceProvider with
            member _.Get _ = raise (NotImplementedException())
            member _.GetData() = raise (NotImplementedException())
            member _.GetDoseRules() = raise (NotImplementedException())
            member _.GetEnteralFeeding() = raise (NotImplementedException())
            member _.GetFormRoutes() = raise (NotImplementedException())
            member _.GetFormularyProducts() = raise (NotImplementedException())
            member _.GetGStandProvider() = raise (NotImplementedException())
            member _.GetParenteralMeds() = raise (NotImplementedException())
            member _.GetProducts() = raise (NotImplementedException())
            member _.GetReconstitution() = raise (NotImplementedException())
            member _.GetRenalRules() = raise (NotImplementedException())
            member _.GetResourceInfo() = raise (NotImplementedException())
            member _.GetRouteMappings() = raise (NotImplementedException())
            member _.GetSolutionRules() = raise (NotImplementedException())

            member _.GetTotals() =
                callCount <- callCount + 1
                [||]

            member _.GetUnitMappings() = raise (NotImplementedException())
            member _.GetValidForms() = raise (NotImplementedException())

    // Copied from StubAdapterTests. One more copy/paste, and by the rule of three we should consider deduplication.
    // An OrderPlan.empty value seems reasonable.
    let emptyPlan: OrderPlan =
        {
            Patient = Models.Patient.empty
            Scenarios = [||]
            Selected = None
            Filtered = [||]
            Totals = Models.Totals.empty
        }

    [<Tests>]
    let tests =
        testList
            "Totals Tests"
            [
                testAsync "updateOrderPlan doesn't cache totals" {
                    let spy = TotalsSpy()
                    let sut = ServerApi.Adapters.makeAppEnv spy
                    let countBefore = spy.CallCount

                    let! _ = sut.orderPlan.updateOrderPlan emptyPlan None

                    let countAfter = spy.CallCount
                    countBefore <! countAfter
                }

                testAsync "filterOrderPlan doesn't cache totals" {
                    let spy = TotalsSpy()
                    let sut = ServerApi.Adapters.makeAppEnv spy
                    let countBefore = spy.CallCount

                    let! _ = sut.orderPlan.filterOrderPlan emptyPlan

                    let countAfter = spy.CallCount
                    countBefore <! countAfter
                }

                testAsync "initNutritionPlan doesn't cache totals" {
                    let spy = TotalsSpy()
                    let sut = ServerApi.Adapters.makeAppEnv spy
                    let countBefore = spy.CallCount

                    let! _ = sut.nutritionPlan.initNutritionPlan Models.Patient.empty

                    let countAfter = spy.CallCount
                    countBefore <! countAfter
                }

                testAsync "addNutritionContext doesn't cache totals" {
                    let spy = TotalsSpy()
                    let sut = ServerApi.Adapters.makeAppEnv spy
                    let countBefore = spy.CallCount

                    let dummyCategory = NutritionCategory.TPN
                    let! _ = sut.nutritionPlan.addNutritionContext (Models.NutritionPlan.empty, dummyCategory)

                    let countAfter = spy.CallCount
                    // The + 1 is a terrible hack to account for orderCtxPort unrelatedly also calling
                    // provider.GetTotals():
                    countBefore + 1 <! countAfter
                }

                testAsync "removeNutritionContext doesn't cache totals" {
                    let spy = TotalsSpy()
                    let sut = ServerApi.Adapters.makeAppEnv spy
                    let countBefore = spy.CallCount

                    let! _ = sut.nutritionPlan.removeNutritionContext (Models.NutritionPlan.empty, "dummy ID")

                    let countAfter = spy.CallCount
                    countBefore <! countAfter
                }

                testAsync "updateNutritionOrderContext doesn't cache totals" {
                    let spy = TotalsSpy()
                    let sut = ServerApi.Adapters.makeAppEnv spy
                    let countBefore = spy.CallCount

                    let! _ =
                        sut.nutritionPlan.updateNutritionOrderContext (
                            Models.NutritionPlan.empty,
                            "dummy label",
                            Models.OrderContext.empty
                        )

                    let countAfter = spy.CallCount
                    // The + 1 is a terrible hack to account for orderCtxPort unrelatedly also calling
                    // provider.GetTotals():
                    countBefore + 1 <! countAfter
                }

                testAsync "selectNutritionOrderScenario doesn't cache totals" {
                    let spy = TotalsSpy()
                    let sut = ServerApi.Adapters.makeAppEnv spy
                    let countBefore = spy.CallCount

                    let! _ =
                        sut.nutritionPlan.selectNutritionOrderScenario (
                            Models.NutritionPlan.empty,
                            "dummy label",
                            Models.OrderContext.empty
                        )

                    let countAfter = spy.CallCount
                    // The + 1 is a terrible hack to account for orderCtxPort unrelatedly also calling
                    // provider.GetTotals():
                    countBefore + 1 <! countAfter
                }

                testAsync "navigateNutritionOrderContext doesn't cache totals" {
                    let spy = TotalsSpy()
                    let sut = ServerApi.Adapters.makeAppEnv spy
                    let countBefore = spy.CallCount

                    let dummyCmd = Shared.Api.OrderContextCommand.UpdateOrderContext

                    let! _ =
                        sut.nutritionPlan.navigateNutritionOrderContext (
                            Models.NutritionPlan.empty,
                            "dummy label",
                            dummyCmd,
                            Models.OrderContext.empty
                        )

                    let countAfter = spy.CallCount
                    // The + 1 is a terrible hack to account for orderCtxPort unrelatedly also calling
                    // provider.GetTotals():
                    countBefore + 1 <! countAfter
                }
            ]
