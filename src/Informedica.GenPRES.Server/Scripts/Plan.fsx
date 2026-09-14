// The signed record stores the contexts (plan 667, step 6), the server half.
//   - `PlanService.openWith`: the plan opened on a signed version, the contexts as they were,
//     nothing evaluated, their orders derived; the patient with no contexts is the empty plan.
//     (`open` is a keyword, hence the name.)
//   - the signing challenge (`ServerApi.Session.fs`): `Challenge.OrderContexts` next to
//     `Scenarios`, stored at the challenge, compared at the submission as the orders are, and
//     written into the version in place of the scenarios. Not drafted here: the session is a
//     state machine over its record; the server tests cover it.
//
// Script-first draft (script-only policy) of what goes to `ServerApi.Services.fs`, with the
// port member, the adapter, the dispatch arm and the client's `LoadCart` sending `Open` in the
// migration.
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

    /// The plan opened on a signed version: the contexts as they were, nothing evaluated, so the
    /// pick lists, the candidates and the stepped values are what was signed; the orders derived
    /// from them. The patient with no contexts is the empty plan.
    let openWith (pat: Patient) (contexts: OrderContext[]) =
        { OrderPlan.create pat (contexts |> Array.choose OrderContext.contribution) with
            OrderContexts = contexts
        }


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


let tests =
    testList
        "Open"
        [
            test "the contexts as they were, their orders derived, nothing evaluated" {
                let stepped =
                    { OrderContext.empty with
                        Id = "c-p"
                        OrderContext.Filter.Generic = Some "paracetamol"
                        OrderContext.Filter.Generics = [| "paracetamol"; "ibuprofen" |]
                        Scenarios = [| scenarioWithOrder "o-p" |]
                    }

                let wide =
                    { OrderContext.empty with
                        Id = "c-w"
                        Category = OrderCategory.Nutrition NutritionCategory.TPN
                        Scenarios = [| scenarioWithOrder "o-1"; scenarioWithOrder "o-2" |]
                    }

                let p = openWith Patient.empty [| stepped; wide |]

                p.OrderContexts |> Expect.equal "the contexts as given, pick lists and all" [| stepped; wide |]
                p.Scenarios |> Array.map _.Order.Id |> Expect.equal "the narrowed one's order" [| "o-p" |]

                openWith Patient.empty [||]
                |> Expect.equal "no contexts: the empty plan" (OrderPlan.create Patient.empty [||])
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
