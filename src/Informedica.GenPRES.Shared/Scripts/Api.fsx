// The old cases deleted and the names settled (plan 667, step 8). The plan command family in
// its final shape: every case names the order context it acts on, since every one acts on the
// plan's `OrderContexts`. `Navigate` names its context always; `RemoveContext` and
// `RemoveOrders` go; `AddOrder`, `AddContext` and `RemoveContexts` become `AddOrderContext`,
// `NewOrderContext` and `RemoveOrderContexts`. A rename and a deletion, no behaviour change.
//
// Script-first draft (script-only policy) of the family and its log names, → `Shared/Api.fs`;
// the port members and service functions follow the same names in the migration.
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


/// → `Shared/Api.fs`, `OrderPlanCommand` final.
[<RequireQualifiedAccess>]
type PlanCommand667 =
    // the totals recomputed over the orders of the filtered contexts
    | Recalculate of OrderPlan
    // the signed version as it was, nothing evaluated: the contexts as given, their orders
    // derived; the patient with no contexts is the empty plan
    | Open of Patient * OrderContext[]
    // a workbench evaluated elsewhere, narrowed to one scenario, into the plan as it is
    | AddOrderContext of OrderPlan * OrderContext
    // a fresh workbench for a nutrition category, its filter discovered
    | NewOrderContext of OrderPlan * NutritionCategory
    // an order-context command evaluated over the context named, in that context's own
    // patient, its order following
    | Navigate of OrderPlan * contextId: string * OrderContextCommand * OrderContext
    // the contexts named removed, every kind; a feeding takes its supplements with it
    | RemoveOrderContexts of OrderPlan * ids: string[]


module PlanCommand667 =

    /// For the log: never the plan, never a workbench.
    let toString cmd =
        match cmd with
        | PlanCommand667.Recalculate _ -> "Recalculate"
        | PlanCommand667.Open(_, contexts) -> $"Open %i{contexts.Length}"
        | PlanCommand667.AddOrderContext _ -> "AddOrderContext"
        | PlanCommand667.NewOrderContext(_, category) -> $"NewOrderContext {category}"
        | PlanCommand667.Navigate(_, _, ctxCmd, _) -> $"Navigate {ctxCmd}"
        | PlanCommand667.RemoveOrderContexts(_, ids) -> $"RemoveOrderContexts %i{ids.Length}"


open Expecto
open Expecto.Flip
open Shared.Models


let tests =
    testList
        "the plan commands, final"
        [
            test "every case names the order context it acts on; the log names the command alone" {
                let plan = OrderPlan.empty
                let ctx = { OrderContext.empty with Id = "secret" }

                [
                    PlanCommand667.Recalculate plan, "Recalculate"
                    PlanCommand667.Open(Patient.empty, [| ctx; ctx |]), "Open 2"
                    PlanCommand667.AddOrderContext(plan, ctx), "AddOrderContext"
                    PlanCommand667.NewOrderContext(plan, NutritionCategory.TPN), "NewOrderContext TPN"
                    PlanCommand667.Navigate(plan, "c-1", OrderContextCommand.UpdateOrderContext, ctx),
                    "Navigate UpdateOrderContext"
                    PlanCommand667.RemoveOrderContexts(plan, [| "c-1"; "c-2" |]), "RemoveOrderContexts 2"
                ]
                |> List.iter (fun (cmd, expected) -> cmd |> PlanCommand667.toString |> Expect.equal expected expected)
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
