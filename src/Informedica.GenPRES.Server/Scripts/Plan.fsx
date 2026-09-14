// AddOrder and RemoveContexts (plan 667, step 4, reordered before the signed record and the
// wire cleanup: a drug order needs a context first), the server half.
//   - `addOrder`: the prescribing workbench into the plan as a drug context with a minted id,
//     its one scenario the order it contributes; refused when the workbench is not narrowed to
//     one scenario, and when the plan already holds that order (the signing challenge would
//     refuse the plan later, so it is said now);
//   - `removeContexts`: every kind by id, each with its order, a feeding with its supplements.
// Both over the real types (the plan is its contexts since step 3).
//
// Script-first draft (script-only policy) of what goes to `ServerApi.Services.fs`
// (`PlanService`), with the port members, the adapter, the dispatch arms and the client's
// `planOf`/`withPlan` arms in the migration.
//
// Run: `dotnet fsi Plan.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared.Types
open Shared.Models
open ServerApi


module PlanService667 =

    let contribution = OrderContext.contribution

    /// The contexts named removed, every kind, each with its order; a feeding takes its
    /// supplements with it.
    let removeContexts (ids: string[]) (plan: OrderPlan) =
        ids |> Array.fold (fun p id -> p |> PlanService.removeContext id) plan


    /// The prescribing workbench into the plan as a drug context with a minted id, its one
    /// scenario the order it contributes.
    let addOrder (newId: unit -> string) (ctx: OrderContext) (plan: OrderPlan) =
        match contribution ctx with
        | None -> Error [| $"The workbench holds %i{ctx.Scenarios.Length} candidates, not one order" |]
        | Some sc when plan.Scenarios |> Array.exists (fun s -> s.Order.Id = sc.Order.Id) ->
            Error [| "The plan already holds this order" |]
        | Some sc ->
            let added =
                { ctx with
                    Id = newId ()
                    Category = OrderCategory.Drug
                }

            { plan with OrderContexts = Array.append plan.OrderContexts [| added |] }
            |> PlanService.withOrders None (Some sc)
            |> Ok


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


let context id category (scenarios: OrderScenario[]) =
    { OrderContext.empty with
        Id = id
        Category = OrderCategory.Nutrition category
        Scenarios = scenarios
    }


let plan contexts scenarios =
    { OrderPlan.create Patient.empty scenarios with OrderContexts = contexts }


let ids (p: OrderPlan) = p.Scenarios |> Array.map _.Order.Id


let tests =
    testList
        "AddOrder and RemoveContexts"
        [
            test "the workbench into the plan as a drug context, once, and only narrowed to one order" {
                let newId () = "c-new"

                let workbench =
                    { OrderContext.empty with
                        OrderContext.Filter.Generic = Some "paracetamol"
                        Scenarios = [| scenarioWithOrder "o-p" |]
                    }

                let p =
                    plan [||] [| scenarioWithOrder "o-drug" |]
                    |> addOrder newId workbench
                    |> Result.defaultWith (fun errs -> failtest $"{errs}")

                let added = p.OrderContexts |> Array.exactlyOne
                added.Id |> Expect.equal "the minted id" "c-new"
                added.Category |> Expect.equal "a drug" OrderCategory.Drug
                added.Filter.Generic |> Expect.equal "the workbench as it was" (Some "paracetamol")
                ids p |> Expect.equal "its order after the others" [| "o-drug"; "o-p" |]

                p
                |> addOrder newId workbench
                |> Expect.equal "the same order twice refused" (Error [| "The plan already holds this order" |])

                plan [||] [||]
                |> addOrder newId { workbench with Scenarios = [| scenarioWithOrder "o-1"; scenarioWithOrder "o-2" |] }
                |> Expect.equal "not narrowed refused" (Error [| "The workbench holds 2 candidates, not one order" |])
            }

            test "removing contexts of every kind: each takes its order, a feeding its supplements" {
                let drug = { OrderContext.empty with Id = "c-d"; Scenarios = [| scenarioWithOrder "o-d" |] }
                let feeding = context "c-f" NutritionCategory.EnteralFeeding [| scenarioWithOrder "o-f" |]
                let supplement = context "c-s" NutritionCategory.EnteralSupplement [| scenarioWithOrder "o-s" |]
                let tpn = context "c-t" NutritionCategory.TPN [| scenarioWithOrder "o-t" |]

                let p =
                    plan
                        [| drug; feeding; supplement; tpn |]
                        [| scenarioWithOrder "o-d"; scenarioWithOrder "o-f"; scenarioWithOrder "o-s"; scenarioWithOrder "o-t" |]
                    |> removeContexts [| "c-d"; "c-f" |]

                p.OrderContexts |> Array.map _.Id |> Expect.equal "the tpn stays" [| "c-t" |]
                ids p |> Expect.equal "with its order" [| "o-t" |]
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
