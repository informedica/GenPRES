module Informedica.GenPRES.Server.Tests.TotalsTests

open System
open Informedica.GenPRES.Shared
open Informedica.GenPRES.Shared.Types
open Expecto
open Informedica.GenForm.Lib
open Swensen.Unquote

type TotalsSpy() =
    let mutable callCount = 0
    member this.CallCount = callCount

    interface Resources.IResourceProvider with
        // the rule set rows answer one TPN row, a nutrition slot reads them; every other resource raises
        member _.Get(key: Resources.ResourceKey<'T>) : 'T =
            if key.Name = Resources.Keys.ruleSetData.Name then
                unbox<'T>(
                    box
                        [|
                            {
                                RuleSetData.Category = "TPN"
                                RuleSetData.Label = "TPN"
                                RuleSetData.Indication = "TPN"
                                RuleSetData.Generic = ""
                            }
                        |]
                )
            else
                raise (NotImplementedException())
        member _.GetData() = raise (NotImplementedException())
        member _.GetDoseRules() = raise (NotImplementedException())
        member _.GetEnteralFeeding() = raise (NotImplementedException())
        member _.GetFormRoutes() = raise (NotImplementedException())
        member _.GetFormularyProducts() = raise (NotImplementedException())
        member _.GetGStandProvider() = raise (NotImplementedException())
        member _.GetDepartments() = raise (NotImplementedException())
        member _.GetParenteralMeds() = raise (NotImplementedException())
        member _.GetProducts() = raise (NotImplementedException())
        member _.GetReconstitution() = raise (NotImplementedException())
        member _.GetRenalRules() = raise (NotImplementedException())
        member _.GetResourceInfo() = raise (NotImplementedException())
        member _.GetRouteMappings() = raise (NotImplementedException())
        member _.GetDilutionRules() = raise (NotImplementedException())

        member _.GetTotals() =
            callCount <- callCount + 1
            [||]

        member _.GetUnitMappings() = raise (NotImplementedException())
        member _.GetValidForms() = raise (NotImplementedException())

/// A provider holding no rules that counts every read of the totals data, so that an evaluation gets as
/// far as the totals of its context.
type EvaluatingTotalsSpy() =
    inherit OrderPlanTests.RulesOf([||])

    let mutable callCount = 0
    member this.CallCount = callCount

    interface Resources.IResourceProvider with
        member _.GetDoseRules() = [||]
        member _.GetRouteMappings() = [||]

        member _.GetTotals() =
            callCount <- callCount + 1
            [||]


/// The stub platform's patient in the domain: the port is typed on domain values.
let patient () =
    ServerApi.Patient.parse ServerApi.StubPatientData.patient
    |> Result.defaultWith (fun e -> invalidOp $"no patient: %A{e}")


let emptyPlan () = Informedica.GenOrder.Lib.OrderPlan.create (patient ()) [||]


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

            // the totals data is read only when an evaluation records totals, so the spy's evaluation must
            // get that far
            testAsync "evaluateContext does not cache totals" {
                let spy = EvaluatingTotalsSpy()
                let sut = ServerApi.Adapters.makeAppEnv spy
                let countBefore = spy.CallCount

                let dummyCmd = Informedica.GenOrder.Lib.OrderContext.UpdateOrderContext
                let! _ = sut.orderPlan.evaluateContext (emptyPlan ()) "dummy ID" dummyCmd (emptyContext ())

                let countAfter = spy.CallCount
                countBefore <! countAfter
            }

            testAsync "openWith recomputes the totals over the contexts opened" {
                let spy = TotalsSpy()
                let sut = ServerApi.Adapters.makeAppEnv spy
                let countBefore = spy.CallCount

                let! _ = sut.orderPlan.openWith (patient ()) [||]

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
