// Scenarios and Selected off the plan, Filtered as context ids (plan 667, step 7), the server
// half: `PlanService` without a stored projection. What goes:
//   - `withContribution`, `follow`, `withOrders`: there is no `Scenarios` to keep in step and
//     no `Filtered`/`Selected` of scenarios to make follow a replaced order;
//   - the signing challenge's `Scenarios` and the check that the orders match the contexts:
//     the contexts are compared, and the duplicate check runs over the derived orders.
// What changes: `removeContext` takes the removed ids out of the filter; `recalculate` counts
// the orders of the contexts the filter keeps; `navigate` without a context id finds the
// context by the evaluated order; `addOrder` checks the duplicate over the derived orders.
//
// Script-first draft (script-only policy) over a record shaped like `OrderPlan` after the step
// (the compiled Shared still has `Scenarios`); the rules below are the ones migrated to
// `ServerApi.Services.fs`, with the port and the client in the migration.
//
// Run: `dotnet fsi Plan.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models


type OrderPlan667 =
    {
        Filtered: string[]
        OrderContexts: OrderContext[]
    }


module PlanService667 =

    let contribution = OrderContext.contribution

    let orders (plan: OrderPlan667) =
        plan.OrderContexts |> Array.choose contribution


    let filtered (plan: OrderPlan667) =
        if plan.Filtered |> Array.isEmpty then
            plan.OrderContexts
        else
            plan.OrderContexts |> Array.filter (fun c -> plan.Filtered |> Array.contains c.Id)


    /// The context removed, and every enteral supplement with a feeding; each leaves the filter.
    let removeContext id (plan: OrderPlan667) =
        let cascade =
            plan.OrderContexts
            |> Array.tryFind (fun c -> c.Id = id)
            |> Option.exists (fun c -> c.Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding)

        let goes (c: OrderContext) =
            c.Id = id
            || (cascade
                && c.Category = OrderCategory.Nutrition NutritionCategory.EnteralSupplement)

        let gone, kept = plan.OrderContexts |> Array.partition goes
        let goneIds = gone |> Array.map _.Id

        { plan with
            OrderContexts = kept
            Filtered = plan.Filtered |> Array.filter (fun f -> goneIds |> Array.contains f |> not)
        }


    /// The orders counted: those of the contexts the filter keeps.
    let counted (plan: OrderPlan667) =
        plan |> filtered |> Array.choose contribution |> Array.map _.Order.Id


    /// The context a command without a context id lands in: the one contributing the
    /// evaluated order.
    let contextForEvaluated (resolved: OrderContext) (plan: OrderPlan667) =
        contribution resolved
        |> Option.bind (fun sc ->
            plan.OrderContexts
            |> Array.tryFind (fun c -> contribution c |> Option.exists (fun s -> s.Order.Id = sc.Order.Id))
        )
        |> Option.map _.Id


open Expecto
open Expecto.Flip
open PlanService667


/// An OrderScenario with only its order id set, every other field a default, by reflection.
let scenarioWithOrder (id: string) : OrderScenario =
    let rec defaultOf (t: Type) : obj =
        if t = typeof<string> then box ""
        elif t = typeof<bool> then box false
        elif t = typeof<int> then box 0
        elif t = typeof<decimal> then box 0m
        elif t = typeof<float> then box 0.0
        elif t = typeof<DateTime> then box DateTime.MinValue
        elif t.IsArray then box (Array.CreateInstance(t.GetElementType(), 0))
        elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<option<_>> then null
        elif Reflection.FSharpType.IsRecord t then
            Reflection.FSharpValue.MakeRecord(
                t,
                Reflection.FSharpType.GetRecordFields t |> Array.map (fun f -> defaultOf f.PropertyType)
            )
        elif Reflection.FSharpType.IsUnion t then
            let case = (Reflection.FSharpType.GetUnionCases t)[0]
            Reflection.FSharpValue.MakeUnion(case, case.GetFields() |> Array.map (fun f -> defaultOf f.PropertyType))
        else null

    let scenario = defaultOf typeof<OrderScenario> :?> OrderScenario
    { scenario with Order = { scenario.Order with Id = id } }


let context id category orderIds =
    { OrderContext.empty with
        Id = id
        Category = category
        Scenarios = orderIds |> Array.map scenarioWithOrder
    }


let drug id orderId = context id OrderCategory.Drug [| orderId |]
let nutrition id cat orderIds = context id (OrderCategory.Nutrition cat) orderIds


let tests =
    testList
        "the plan without a projection"
        [
            test "the orders counted are those of the contexts the filter keeps" {
                let plan =
                    { Filtered = [||]
                      OrderContexts = [| drug "c-d" "o-d"; nutrition "c-w" NutritionCategory.TPN [| "o-1"; "o-2" |]; drug "c-e" "o-e" |] }

                plan |> counted |> Expect.equal "all narrowed" [| "o-d"; "o-e" |]
                { plan with Filtered = [| "c-e" |] } |> counted |> Expect.equal "the filtered one" [| "o-e" |]
                { plan with Filtered = [| "c-w" |] } |> counted |> Expect.isEmpty "a wide context counts nothing"
            }

            test "a removed context leaves the filter; a feeding takes its supplements out of it too" {
                let plan =
                    { Filtered = [| "c-f"; "c-s"; "c-t" |]
                      OrderContexts =
                        [| nutrition "c-f" NutritionCategory.EnteralFeeding [| "o-f" |]
                           nutrition "c-s" NutritionCategory.EnteralSupplement [| "o-s" |]
                           nutrition "c-t" NutritionCategory.TPN [| "o-t" |] |] }

                let p = plan |> removeContext "c-f"
                p.OrderContexts |> Array.map _.Id |> Expect.equal "the tpn stays" [| "c-t" |]
                p.Filtered |> Expect.equal "and is what the filter still names" [| "c-t" |]
            }

            test "a command without a context id lands in the context contributing the evaluated order" {
                let plan = { Filtered = [||]; OrderContexts = [| drug "c-d" "o-d"; drug "c-e" "o-e" |] }
                plan |> contextForEvaluated (drug "" "o-e") |> Expect.equal "c-e" (Some "c-e")
                plan |> contextForEvaluated (drug "" "o-x") |> Expect.equal "unknown order: none" None
                plan |> contextForEvaluated (nutrition "" NutritionCategory.TPN [| "o-1"; "o-2" |]) |> Expect.equal "wide: none" None
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
