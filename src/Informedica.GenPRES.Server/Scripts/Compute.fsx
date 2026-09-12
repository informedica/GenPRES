// Deleting the old plan families (plan 654, step 8c): with the client on the one plan, the
// `OrderPlanCmd` and `NutritionPlanCmd` families, the `NutritionPlan` type and the two ports and
// services that served them leave the contract and the server. What stays of them is what the
// one plan reuses: the nutrition dose-rule sets, the filter by rule set, the filter discovery,
// and the totals, which move from `OrderPlanService` into `PlanService`.
//
// Script-first draft (script-only policy) of:
//   - `PlanService.recalculate` computing the totals itself (was `OrderPlanService.calculateTotals`)
//     → `Services.fs`; `OrderPlanService` and the nutrition-plan functions deleted there;
//   - the deletions are stated, not drafted (a script cannot remove cases or types):
//     `OrderPlanCmd`, `NutritionPlanCmd`, `OrderPlanCommand`, `NutritionPlanCommand`,
//     `OrderPlanResp`, `NutritionPlanResp`, `OrderPlanResponse`, `NutritionPlanResponse` and
//     their `toString` arms → `Shared/Api.fs`; `NutritionPlan` → `Shared/Types.fs`, its module
//     → `Shared/Models.fs`; `OrderPlanPort`, `NutritionPlanPort`, `AppEnv.orderPlan`,
//     `AppEnv.nutritionPlan` → `Ports.fs`; their adapters → `Adapters.fs`; their arms →
//     `Command.fs`, which then dispatches the order context alone; the client's no-op arm →
//     `App.fs`; the test stubs, the totals tests onto the plan port.
//
// Run: `dotnet fsi Compute.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Shared
open Shared.Types
open Shared.Models
open ServerApi


// ---------------------------------------------------------------------------------------------
// The totals, the plan's own (→ Services.fs, module PlanService)
// ---------------------------------------------------------------------------------------------

module PlanService =

    /// The plan with its totals recomputed over its orders: the filtered ones, by order id, when
    /// a filter is set, else all of them.
    let recalculate (totals: Informedica.GenForm.Lib.Types.Data.TotalsData[]) (plan: OrderPlan) =
        { plan with
            Totals =
                let w = plan.Patient |> Patient.getWeight |> Option.map int
                let a = plan.Patient |> Patient.getAgeInDays |> Option.map int

                let scs =
                    if plan.Filtered |> Array.isEmpty then
                        plan.Scenarios
                    else
                        plan.Scenarios
                        |> Array.filter (fun sc -> plan.Filtered |> Array.exists (fun f -> f.Order.Id = sc.Order.Id))

                scs |> Array.map _.Order |> OrderService.getTotals totals a w
        }


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


let tests =
    testList
        "the totals, the plan's own"
        [
            test "an empty plan's totals are the empty totals" {
                OrderPlan.empty
                |> PlanService.recalculate [||]
                |> _.Totals
                |> Expect.equal "empty" Totals.empty
            }

            test "the same answer as the service it replaces" {
                let plan = OrderPlan.create Patient.empty [||]

                PlanService.recalculate [||] plan
                |> Expect.equal "the same" (OrderPlanService.calculateTotals [||] plan)
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
