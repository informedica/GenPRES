// The old cases deleted and the names settled (plan 667, step 8), the server half. `navigate`
// takes the context id always: the branch that found a context by the evaluated order served
// the plan page's dialog before an order had a context, and nothing sends it any more.
// `removeOrders` goes with `RemoveOrders`; `fromOrderScenario` with the rebuild it served. The
// port members and the service functions take the family's names: `addOrderContext`,
// `newOrderContext`, `removeOrderContexts` (and `removeOrderContext` for the one the others
// fold over).
//
// Script-first draft (script-only policy) of `navigate` over the real types, → `OrderPlanService`
// in `ServerApi.Services.fs`.
//
// Run: `dotnet fsi Plan.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Shared
open Shared.Types
open Shared.Models
open ServerApi


module PlanService667 =

    /// A command into the context named; `recalc` ends the answer in its totals. An evaluation
    /// that fails is the answer, not the plan as it was.
    let navigate
        (recalc: OrderPlan -> OrderPlan)
        (orderCtxPort: OrderContextPort)
        (plan: OrderPlan)
        (contextId: string)
        (ctxCmd: Api.OrderContextCommand)
        (ctx: OrderContext)
        =
        async {
            let! result = orderCtxPort.evaluate ctxCmd ctx

            return
                result
                |> Result.bind (fun resolved -> plan |> OrderPlanService.updateContext contextId resolved)
                |> Result.map recalc
        }


open Expecto
open Expecto.Flip


let context id =
    { OrderContext.empty with
        Id = id
        Category = OrderCategory.Nutrition NutritionCategory.TPN
    }


let tests =
    testList
        "navigate names its context"
        [
            testAsync "the evaluated context lands in the one named, id and category kept; an unknown id is the answer" {
                let evaluated = { OrderContext.empty with OrderContext.Filter.Generic = Some "tpn" }
                let port: OrderContextPort = { evaluate = fun _ _ -> async { return Ok evaluated } }
                let plan = OrderPlan.create Patient.empty [| context "c-1" |]

                match! PlanService667.navigate id port plan "c-1" Api.OrderContextCommand.UpdateOrderContext OrderContext.empty with
                | Ok p ->
                    let c = p.OrderContexts |> Array.exactlyOne
                    c.Id |> Expect.equal "the id kept" "c-1"
                    c.Category |> Expect.equal "the category kept" (OrderCategory.Nutrition NutritionCategory.TPN)
                    c.Filter.Generic |> Expect.equal "the evaluation in" (Some "tpn")
                | Error errs -> failtest $"{errs}"

                match! PlanService667.navigate id port plan "c-9" Api.OrderContextCommand.UpdateOrderContext OrderContext.empty with
                | Error errs -> errs |> Expect.equal "no such context" [| "The plan holds no context c-9" |]
                | Ok _ -> failtest "expected Error"
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
