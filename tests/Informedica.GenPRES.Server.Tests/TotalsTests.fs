module Informedica.GenPRES.Server.Tests.TotalsTests

open System
open Shared
open Shared.Types
open Expecto
open Informedica.GenForm.Lib
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

let emptyPlan = Models.OrderPlan.empty

[<Tests>]
let tests =
    testList
        "Totals Tests"
        [
            testAsync "recalculate doesn't cache totals" {
                let spy = TotalsSpy()
                let sut = ServerApi.Adapters.makeAppEnv spy
                let countBefore = spy.CallCount

                let! _ = sut.plan.recalculate emptyPlan

                let countAfter = spy.CallCount
                countBefore <! countAfter
            }

            testAsync "navigate doesn't cache totals" {
                let spy = TotalsSpy()
                let sut = ServerApi.Adapters.makeAppEnv spy
                let countBefore = spy.CallCount

                let dummyCmd = Shared.Api.OrderContextCommand.UpdateOrderContext
                let! _ = sut.plan.navigate emptyPlan None dummyCmd Models.OrderContext.empty

                let countAfter = spy.CallCount
                // The + 1 is a terrible hack to account for orderCtxPort unrelatedly also calling
                // provider.GetTotals():
                countBefore + 1 <! countAfter
            }

            testAsync "addContext doesn't cache totals" {
                let spy = TotalsSpy()
                let sut = ServerApi.Adapters.makeAppEnv spy
                let countBefore = spy.CallCount

                let! _ = sut.plan.addContext emptyPlan NutritionCategory.TPN

                let countAfter = spy.CallCount
                // The + 1 is a terrible hack to account for orderCtxPort unrelatedly also calling
                // provider.GetTotals():
                countBefore + 1 <! countAfter
            }

            testAsync "removeContext doesn't cache totals" {
                let spy = TotalsSpy()
                let sut = ServerApi.Adapters.makeAppEnv spy
                let countBefore = spy.CallCount

                let! _ = sut.plan.removeContext emptyPlan "dummy ID"

                let countAfter = spy.CallCount
                countBefore <! countAfter
            }
        ]
