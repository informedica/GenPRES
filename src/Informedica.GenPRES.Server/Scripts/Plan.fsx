// The plan is its contexts (plan 667, step 3), the server half: `PlanService` over
// `OrderPlan.OrderContexts`, an `OrderContext` per order with its id and category, and the
// `NutritionContext` wrapper gone. What changes in substance:
//   - `updateContext` keeps the id and the category the plan gave a context, whatever the
//     page sent, and applies the dose-rule filter only to a nutrition context;
//   - `removeContext` cascades from a feeding to every enteral supplement by category, which
//     is the feeding's supplements because the plan holds one feeding at most;
//   - `addContext` refuses a second context of a nutrition category (supplements excepted)
//     and a supplement without a feeding: the rule the nutrition page's buttons keep, now
//     kept by the server for every caller.
// `Scenarios` and `withOrders` stay as they are: a drug order has no context until `AddOrder`.
//
// Script-first draft (script-only policy) of what goes to `ServerApi.Services.fs`, over a
// plan record shaped like `OrderPlan` after the step (the compiled Shared has no
// `OrderContexts` yet); the cascade and the admission rule are the tests.
//
// Run: `dotnet fsi Plan.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Shared.Types
open Shared.Models


/// `OrderPlan` after the step, reduced to what the rules below read.
type Plan667 =
    {
        OrderContexts: OrderContext[]
    }


module PlanService667 =

    let private holds category (plan: Plan667) =
        plan.OrderContexts
        |> Array.exists (fun c -> c.Category = OrderCategory.Nutrition category)


    /// The ids of the contexts that go when the one named is removed: itself, and every
    /// enteral supplement when it is the feeding.
    let removed id (plan: Plan667) =
        let cascade =
            plan.OrderContexts
            |> Array.tryFind (fun c -> c.Id = id)
            |> Option.exists (fun c -> c.Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding)

        plan.OrderContexts
        |> Array.filter (fun c ->
            c.Id = id
            || (cascade
                && c.Category = OrderCategory.Nutrition NutritionCategory.EnteralSupplement)
        )
        |> Array.map _.Id


    /// Whether the plan may take a context of the category: one context per nutrition category,
    /// supplements excepted, and a supplement only under a feeding.
    let admits category (plan: Plan667) =
        match category with
        | NutritionCategory.EnteralSupplement when plan |> holds NutritionCategory.EnteralFeeding |> not ->
            Error [| "A supplement needs a feeding in the plan" |]
        | NutritionCategory.EnteralSupplement -> Ok()
        | _ when plan |> holds category ->
            Error [| $"The plan already holds a %s{NutritionCategory.label category} context" |]
        | _ -> Ok()


    /// The resolved context back into the plan's: the id and the category are the plan's,
    /// whatever the page sent.
    let stamped (existing: OrderContext) (resolved: OrderContext) =
        { resolved with
            Id = existing.Id
            Category = existing.Category
        }


open Expecto
open Expecto.Flip
open PlanService667


let context id category =
    { OrderContext.empty with
        Id = id
        Category = OrderCategory.Nutrition category
    }


let feeding = context "c-f" NutritionCategory.EnteralFeeding
let supplement = context "c-s" NutritionCategory.EnteralSupplement
let tpn = context "c-t" NutritionCategory.TPN
let drug = { OrderContext.empty with Id = "c-d" }


let tests =
    testList
        "the plan is its contexts"
        [
            test "removing the feeding takes every supplement; removing anything else takes itself" {
                let plan = { OrderContexts = [| feeding; supplement; tpn; drug |] }
                plan |> removed "c-f" |> Expect.equal "the feeding and its supplement" [| "c-f"; "c-s" |]
                plan |> removed "c-s" |> Expect.equal "the supplement alone" [| "c-s" |]
                plan |> removed "c-t" |> Expect.equal "the tpn alone" [| "c-t" |]
                plan |> removed "c-x" |> Expect.isEmpty "an unknown id: nothing"
            }

            test "one context per nutrition category, and a supplement only under a feeding" {
                let plan = { OrderContexts = [| feeding |] }

                plan
                |> admits NutritionCategory.EnteralFeeding
                |> Expect.equal "a second feeding refused" (Error [| "The plan already holds a Enterale Voeding context" |])

                { OrderContexts = [||] }
                |> admits NutritionCategory.EnteralSupplement
                |> Expect.equal "a supplement without a feeding refused" (Error [| "A supplement needs a feeding in the plan" |])

                plan |> admits NutritionCategory.EnteralSupplement |> Expect.equal "under a feeding" (Ok())
                plan |> admits NutritionCategory.TPN |> Expect.equal "another category" (Ok())

                { OrderContexts = [| feeding; supplement |] }
                |> admits NutritionCategory.EnteralSupplement
                |> Expect.equal "a second supplement" (Ok())
            }

            test "a context re-evaluated keeps the id and the category the plan gave it" {
                let sent = { OrderContext.empty with Id = ""; Category = OrderCategory.Drug }
                let back = sent |> stamped tpn
                back.Id |> Expect.equal "the plan's id" "c-t"
                back.Category |> Expect.equal "the plan's category" (OrderCategory.Nutrition NutritionCategory.TPN)
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
