// The plan is its contexts (plan 667, step 3): `OrderPlan.OrderContexts` replaces
// `NutritionContexts`, and the `NutritionContext` wrapper goes. Every context in the plan says
// what it holds (its category, since step 2) and carries its id; the label and the nutrition
// category are derived, and the orders the contexts contribute are a function of the plan.
// `Scenarios` stays on the plan for now: a drug order added from the prescribing page has no
// context yet (that is `AddOrder`, step 6), so the server keeps `Scenarios` in step as today.
//
// Script-first draft (script-only policy) of what goes to `Shared/Types.fs` and
// `Shared/Models.fs`: the plan's shape below, `OrderContext.nutritionCategory` and
// `OrderContext.contribution`, `OrderPlan.nutritionContexts` and `OrderPlan.orders` (the
// latter exercised in the server tests, where a scenario with an order can be built).
//
// Run: `dotnet fsi Api.fsx` from this directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"
#load "../Api.fs"

open Shared.Types


/// → `Shared/Types.fs`: `OrderPlan` with `OrderContexts` in place of `NutritionContexts`;
/// `NutritionContext` deleted.
type OrderPlan667 =
    {
        Patient: Patient
        Selected: OrderScenario option
        Filtered: OrderScenario[]
        Scenarios: OrderScenario[]
        // the order contexts of the plan, drug and nutrition alike, each saying what it holds
        // and carrying its id; a context narrowed to one scenario has it in Scenarios
        OrderContexts: OrderContext[]
        Totals: Totals
    }


/// → `Shared/Models.fs`.
module Models667 =

    module OrderContext =

        /// The nutrition category of a context, none for a drug.
        let nutritionCategory (ctx: OrderContext) =
            match ctx.Category with
            | OrderCategory.Nutrition category -> Some category
            | OrderCategory.Drug -> None


        /// The order a context contributes to the plan: its scenario, once the context is
        /// narrowed to exactly one; nothing while it holds several candidates or none.
        let contribution (ctx: OrderContext) = ctx.Scenarios |> Array.tryExactlyOne


    module OrderPlan =

        let create pat srs : OrderPlan667 =
            {
                Patient = pat
                Selected = None
                Filtered = [||]
                Scenarios = srs
                OrderContexts = [||]
                Totals = Shared.Models.Totals.empty
            }


        let empty = create Shared.Models.Patient.empty [||]


        /// The nutrition workbenches of the plan.
        let nutritionContexts (plan: OrderPlan667) =
            plan.OrderContexts |> Array.filter (OrderContext.nutritionCategory >> Option.isSome)


        /// The orders the plan's contexts contribute: the one scenario of every context narrowed
        /// to one, in context order.
        let orders (plan: OrderPlan667) =
            plan.OrderContexts |> Array.choose OrderContext.contribution


open Expecto
open Expecto.Flip
open Shared.Models
open Models667


let tpn =
    { OrderContext.empty with
        Id = "c-t"
        Category = OrderCategory.Nutrition NutritionCategory.TPN
    }


let drug = { OrderContext.empty with Id = "c-d" }


let tests =
    testList
        "the plan is its contexts"
        [
            test "a drug has no nutrition category, a nutrition context its own" {
                drug |> OrderContext.nutritionCategory |> Expect.equal "a drug" None
                tpn |> OrderContext.nutritionCategory |> Expect.equal "tpn" (Some NutritionCategory.TPN)
            }

            test "the nutrition contexts are the ones with a nutrition category" {
                { OrderPlan.empty with OrderContexts = [| drug; tpn |] }
                |> OrderPlan.nutritionContexts
                |> Array.map _.Id
                |> Expect.equal "the tpn" [| "c-t" |]
            }

        ]


runTestsWithCLIArgs [] [||] tests |> ignore
