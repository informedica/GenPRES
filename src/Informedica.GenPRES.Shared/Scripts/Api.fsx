// AddOrder and RemoveContexts (plan 667, step 4, reordered: a drug order needs a context before
// the signed record can store contexts and before `Scenarios` can leave the plan, so these two
// commands come before both). `AddOrder` moves the prescribing workbench into the plan as a
// drug context; `RemoveContexts` removes contexts of every kind by id. Both beside the old
// cases, which go in the deletion step.
//
// Script-first draft (script-only policy) of the two cases and their log names, → `Shared/Api.fs`.
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
open Shared.Api


/// → `Shared/Api.fs`, `PlanCommand` with the two new cases.
[<RequireQualifiedAccess>]
type PlanCommand667 =
    | Recalculate of OrderPlan
    | Navigate of OrderPlan * contextId: string option * OrderContextCommand * OrderContext
    | AddContext of OrderPlan * NutritionCategory
    | RemoveContext of OrderPlan * contextId: string
    | RemoveOrders of OrderPlan * ids: string[]
    // the prescribing workbench, narrowed to one scenario, into the plan as a drug context
    | AddOrder of OrderPlan * OrderContext
    // the contexts named removed, every kind; a feeding takes its supplements with it
    | RemoveContexts of OrderPlan * ids: string[]


module PlanCommand667 =

    /// For the log: never the plan, never the workbench.
    let toString cmd =
        match cmd with
        | PlanCommand667.Recalculate _ -> "Recalculate"
        | PlanCommand667.Navigate(_, None, ctxCmd, _) -> $"Navigate {ctxCmd}"
        | PlanCommand667.Navigate(_, Some _, ctxCmd, _) -> $"Navigate context {ctxCmd}"
        | PlanCommand667.AddContext(_, category) -> $"AddContext {category}"
        | PlanCommand667.RemoveContext _ -> "RemoveContext"
        | PlanCommand667.RemoveOrders(_, ids) -> $"RemoveOrders %i{ids.Length}"
        | PlanCommand667.AddOrder _ -> "AddOrder"
        | PlanCommand667.RemoveContexts(_, ids) -> $"RemoveContexts %i{ids.Length}"


open Expecto
open Expecto.Flip
open Shared.Models


let tests =
    testList
        "the two new plan commands"
        [
            test "the log names the command, never the workbench nor the plan" {
                PlanCommand667.AddOrder(OrderPlan.empty, { OrderContext.empty with Id = "secret" })
                |> PlanCommand667.toString
                |> Expect.equal "the name" "AddOrder"

                PlanCommand667.RemoveContexts(OrderPlan.empty, [| "c-1"; "c-2" |])
                |> PlanCommand667.toString
                |> Expect.equal "the count" "RemoveContexts 2"
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
