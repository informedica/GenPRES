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

/// The stub platform's patient in the domain: the port is typed on domain values.
let patient () =
    ServerApi.Patient.parse ServerApi.StubPatientData.patient
    |> Result.defaultWith (fun e -> invalidOp $"no patient: %A{e}")


let emptyPlan () =
    Informedica.GenOrder.Lib.OrderPlan.create (patient ()) [||]


let emptyContext () =
    ServerApi.OrderContextService.parse { Models.OrderContext.empty with Patient = ServerApi.StubPatientData.patient }
    |> Result.defaultWith (fun e -> invalidOp $"no plan context: %A{e}")

[<Tests>]
let tests =
    testList
        "Totals Tests"
        [
            testAsync "recalculate doesn't cache totals" {
                let spy = TotalsSpy()
                let sut = ServerApi.Adapters.makeAppEnv spy
                let countBefore = spy.CallCount

                let! _ = sut.orderPlan.recalculate (emptyPlan ())

                let countAfter = spy.CallCount
                countBefore <! countAfter
            }

            testAsync "navigate doesn't cache totals" {
                let spy = TotalsSpy()
                let sut = ServerApi.Adapters.makeAppEnv spy
                let countBefore = spy.CallCount

                let dummyCmd = Informedica.GenOrder.Lib.OrderContext.UpdateOrderContext
                let! _ = sut.orderPlan.navigate (emptyPlan ()) "dummy ID" dummyCmd (emptyContext ())

                let countAfter = spy.CallCount
                countBefore <! countAfter
            }

            testAsync "newOrderContext doesn't cache totals" {
                let spy = TotalsSpy()
                let sut = ServerApi.Adapters.makeAppEnv spy
                let countBefore = spy.CallCount

                let! _ =
                    sut.orderPlan.newOrderContext (emptyPlan ()) Informedica.GenOrder.Lib.Types.NutritionCategory.TPN

                let countAfter = spy.CallCount
                countBefore <! countAfter
            }

            testAsync "removeOrderContexts doesn't cache totals" {
                let spy = TotalsSpy()
                let sut = ServerApi.Adapters.makeAppEnv spy
                let countBefore = spy.CallCount

                let! _ = sut.orderPlan.removeOrderContexts (emptyPlan ()) [| "dummy ID" |]

                let countAfter = spy.CallCount
                countBefore <! countAfter
            }
        ]
