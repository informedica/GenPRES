// The one plan (plan 654, step 8a): the nutrition plan is a subset of the order plan, and only
// the order plan is signed, nutrition included. `OrderPlan` gains `NutritionContexts`, the
// workbenches that produce orders by category, and one rule makes one signature cover them:
// a context narrowed to exactly one scenario has that scenario in `plan.Scenarios`, upserted by
// order id; a context with several candidates contributes nothing yet; a removed context takes
// its order with it. Totals are computed once, over `Scenarios`. One command family,
// `PlanCommand`, served by one member, `processOrderPlan`.
//
// Script-first draft (script-only policy) of:
//   - `OrderPlan.NutritionContexts` (with `NutritionCategory` and `NutritionContext` declared
//     before it) → `Shared/Types.fs`; `OrderPlan.create` → `Shared/Models.fs`;
//   - `PlanCommand` and `PlanCommand.toString`, `processOrderPlan` → `Shared/Api.fs`;
//   - `PlanPort` and `AppEnv.plan` → `Ports.fs`; `PlanService` → `Services.fs`; `makePlanPort`
//     → `Adapters.fs`; `PlanCommand.processCmd` → new `ServerApi.PlanCommand.fs`; the member in
//     `compose` → `CompositionRoot.fs`.
//
// The shared `OrderPlan` has no `NutritionContexts` while this script runs, so the rules are
// drafted over the pieces they read (the contexts, the orders) and the service over a local
// record of the target shape. The old plan families stay served until the client moves (8b)
// and are deleted after (8c). Run: `dotnet fsi Compute.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Shared
open Shared.Types
open Shared.Models
open Shared.Api
open ServerApi


// ---------------------------------------------------------------------------------------------
// The wire (→ Shared/Api.fs)
// ---------------------------------------------------------------------------------------------

module Api654 =

    /// The plan command family: the order plan, nutrition included.
    [<RequireQualifiedAccess>]
    type PlanCommand =
        // the plan as it is, totals recomputed over its orders
        | Recalculate of OrderPlan
        // an order-context command evaluated and folded into the plan: into the nutrition context
        // named, or into the selected scenario when None
        | Navigate of OrderPlan * contextId: string option * OrderContextCommand * OrderContext
        // a nutrition context for the category, its filter discovered
        | AddContext of OrderPlan * NutritionCategory
        // the nutrition context removed with its order; a feeding takes its supplements with it
        | RemoveContext of OrderPlan * contextId: string


    module PlanCommand =

        /// For the log: never the plan.
        let toString cmd =
            match cmd with
            | PlanCommand.Recalculate _ -> "Recalculate"
            | PlanCommand.Navigate(_, None, ctxCmd, _) -> $"Navigate {ctxCmd}"
            | PlanCommand.Navigate(_, Some _, ctxCmd, _) -> $"Navigate context {ctxCmd}"
            | PlanCommand.AddContext(_, category) -> $"AddContext {category}"
            | PlanCommand.RemoveContext _ -> "RemoveContext"


    // on `IServerApi`:
    //
    //     processOrderPlan: Request<PlanCommand> -> Async<Result<Reply<OrderPlan>, string[]>>


// ---------------------------------------------------------------------------------------------
// The rules (→ Services.fs, module PlanService)
// ---------------------------------------------------------------------------------------------

/// The target shape of `OrderPlan` (→ Types.fs: `NutritionContexts: NutritionContext[]`).
type Plan654 =
    {
        Patient: Patient
        Selected: OrderScenario option
        Filtered: OrderScenario[]
        Scenarios: OrderScenario[]
        NutritionContexts: NutritionContext[]
        Totals: Totals
    }


module PlanService =

    /// The order a nutrition context contributes to the plan: its scenario, once the context
    /// is narrowed to exactly one; nothing while it holds several candidates or none.
    let contribution (nc: NutritionContext) = nc.OrderContext.Scenarios |> Array.tryExactlyOne


    /// The plan's orders with one context's contribution replaced: the one it contributed
    /// before goes out by order id, the one it contributes now comes in; the same order in
    /// place, a different one at the end.
    let withContribution (before: OrderScenario option) (after: OrderScenario option) (scenarios: OrderScenario[]) =
        match before, after with
        | Some old, Some sc when old.Order.Id = sc.Order.Id ->
            scenarios |> Array.map (fun s -> if s.Order.Id = sc.Order.Id then sc else s)
        | _ ->
            let without =
                match before with
                | None -> scenarios
                | Some old -> scenarios |> Array.filter (fun s -> s.Order.Id <> old.Order.Id)

            match after with
            | None -> without
            | Some sc -> Array.append without [| sc |]


    /// A derived view (the filter, the selection) follows a replaced order only where it held
    /// the old one: replaced in place, or gone with it; never gains an order on its own.
    let private follow (before: OrderScenario option) (after: OrderScenario option) (view: OrderScenario[]) =
        match before with
        | Some old when view |> Array.exists (fun s -> s.Order.Id = old.Order.Id) -> view |> withContribution before after
        | _ -> view


    /// The plan's orders with one contribution replaced, and the filter and the selection
    /// following it, so that a replaced order keeps counting in the totals (which count by
    /// order id) and a removed one is nowhere.
    let withOrders (before: OrderScenario option) (after: OrderScenario option) (plan: Plan654) =
        { plan with
            Scenarios = plan.Scenarios |> withContribution before after
            Filtered = plan.Filtered |> follow before after
            Selected =
                match plan.Selected, before with
                | Some sel, Some old when sel.Order.Id = old.Order.Id -> after
                | sel, _ -> sel
        }


    /// The resolved order context into the context named, filtered to the category's dose rule
    /// set as before, and its contribution into the plan's orders.
    let updateContext id (resolved: OrderContext) (plan: Plan654) =
        match plan.NutritionContexts |> Array.tryFind (fun nc -> nc.Id = id) with
        | None -> Error [| $"The plan holds no nutrition context %s{id}" |]
        | Some nc ->
            let drs = NutritionPlanService.getDoseRuleSet nc.Category
            let updated = { nc with OrderContext = resolved |> NutritionPlanService.filterByDoseRuleSet drs }

            { plan with
                NutritionContexts = plan.NutritionContexts |> Array.map (fun c -> if c.Id = id then updated else c)
            }
            |> withOrders (contribution nc) (contribution updated)
            |> Ok


    /// The context removed, and every supplement with a feeding; each takes its order with it.
    let removeContext id (plan: Plan654) =
        let removed = plan.NutritionContexts |> Array.tryFind (fun nc -> nc.Id = id)

        let cascade =
            removed
            |> Option.map (fun nc -> nc.Category = NutritionCategory.EnteralFeeding)
            |> Option.defaultValue false

        let goes (nc: NutritionContext) =
            nc.Id = id || (cascade && nc.Category = NutritionCategory.EnteralSupplement)

        let gone, kept = plan.NutritionContexts |> Array.partition goes

        gone
        |> Array.fold (fun p nc -> p |> withOrders (contribution nc) None) { plan with NutritionContexts = kept }


// and the totals (→ Services.fs): `OrderPlanService.calculateTotals totals plan`, once, over
// `Scenarios` respecting `Filtered`; `calculateNutritionTotals` goes with the old family (8c).
//
// the port (→ Ports.fs):
//
//     type PlanPort =
//         {
//             recalculate: OrderPlan -> Async<Result<OrderPlan, string[]>>
//             navigate: OrderPlan -> string option -> OrderContextCommand -> OrderContext -> Async<Result<OrderPlan, string[]>>
//             addContext: OrderPlan -> NutritionCategory -> Async<Result<OrderPlan, string[]>>
//             removeContext: OrderPlan -> string -> Async<Result<OrderPlan, string[]>>
//         }
//
// `navigate plan None cmd ctx` evaluates the context and folds the selected scenario in by
// order id (an evaluation that fails is the answer, not the plan as it was);
// `navigate plan (Some id) cmd ctx` evaluates the context through the order-context port and
// applies `updateContext`; `addContext` is today's `addNutritionContext` over the plan (the
// discovered context appended, its contribution in); every answer ends in the totals.


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


/// An OrderScenario with only its order id set, every other field a default, by reflection:
/// the order graph is too deep to write by hand (as the server tests build theirs).
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
        elif t.IsGenericType && t.GetGenericTypeDefinition() = typedefof<list<_>> then
            t.GetProperty("Empty").GetValue null
        elif Reflection.FSharpType.IsRecord t then
            Reflection.FSharpType.GetRecordFields t
            |> Array.map (fun f -> defaultOf f.PropertyType)
            |> fun vs -> Reflection.FSharpValue.MakeRecord(t, vs)
        elif Reflection.FSharpType.IsUnion t then
            let case = Reflection.FSharpType.GetUnionCases t |> Array.head
            Reflection.FSharpValue.MakeUnion(case, case.GetFields() |> Array.map (fun f -> defaultOf f.PropertyType))
        else Activator.CreateInstance t

    let sc = defaultOf typeof<OrderScenario> :?> OrderScenario
    { sc with Order = { sc.Order with Id = id } }


let context id category (scenarios: OrderScenario[]) =
    NutritionContext.create id (string category) category true { OrderContext.empty with Scenarios = scenarios }


let plan contexts scenarios : Plan654 =
    {
        Patient = Patient.empty
        Selected = None
        Filtered = [||]
        Scenarios = scenarios
        NutritionContexts = contexts
        Totals = Totals.empty
    }


let ids (p: Plan654) = p.Scenarios |> Array.map _.Order.Id


let tests =
    testList
        "the one plan"
        [
            test "a context narrowed to one scenario has it in the plan's orders, once" {
                let tpn = context "c-1" NutritionCategory.TPN [||]
                let resolved = { OrderContext.empty with Scenarios = [| scenarioWithOrder "o-tpn" |] }

                match plan [| tpn |] [| scenarioWithOrder "o-drug" |] |> PlanService.updateContext "c-1" resolved with
                | Ok p ->
                    ids p |> Expect.equal "the drug and the tpn" [| "o-drug"; "o-tpn" |]

                    // narrowed again to the same order: in place, not twice
                    match p |> PlanService.updateContext "c-1" resolved with
                    | Ok p -> ids p |> Expect.equal "once" [| "o-drug"; "o-tpn" |]
                    | Error errs -> failtest $"{errs}"
                | Error errs -> failtest $"{errs}"
            }

            test "a context re-narrowed to another order replaces its contribution" {
                let tpn = context "c-1" NutritionCategory.TPN [| scenarioWithOrder "o-tpn" |]
                let p = plan [| tpn |] [| scenarioWithOrder "o-drug"; scenarioWithOrder "o-tpn" |]
                let other = { OrderContext.empty with Scenarios = [| scenarioWithOrder "o-tpn-2" |] }

                match p |> PlanService.updateContext "c-1" other with
                | Ok p -> ids p |> Expect.equal "the other in place of the old" [| "o-drug"; "o-tpn-2" |]
                | Error errs -> failtest $"{errs}"
            }

            test "several candidates contribute nothing; widening again takes the order out" {
                let tpn = context "c-1" NutritionCategory.TPN [| scenarioWithOrder "o-tpn" |]
                let p = plan [| tpn |] [| scenarioWithOrder "o-drug"; scenarioWithOrder "o-tpn" |]
                let widened = { OrderContext.empty with Scenarios = [| scenarioWithOrder "o-a"; scenarioWithOrder "o-b" |] }

                match p |> PlanService.updateContext "c-1" widened with
                | Ok p -> ids p |> Expect.equal "the drug only" [| "o-drug" |]
                | Error errs -> failtest $"{errs}"

                plan [||] [||] |> PlanService.updateContext "c-9" widened |> Result.isError |> Expect.isTrue "no such context"
            }

            test "a removed context takes its order; a feeding takes its supplements and theirs" {
                let feeding = context "c-f" NutritionCategory.EnteralFeeding [| scenarioWithOrder "o-f" |]
                let supplement = context "c-s" NutritionCategory.EnteralSupplement [| scenarioWithOrder "o-s" |]
                let tpn = context "c-t" NutritionCategory.TPN [| scenarioWithOrder "o-t" |]

                let p =
                    plan
                        [| feeding; supplement; tpn |]
                        [| scenarioWithOrder "o-drug"; scenarioWithOrder "o-f"; scenarioWithOrder "o-s"; scenarioWithOrder "o-t" |]

                let p = p |> PlanService.removeContext "c-f"
                p.NutritionContexts |> Array.map _.Id |> Expect.equal "the tpn stays" [| "c-t" |]
                ids p |> Expect.equal "the drug and the tpn order" [| "o-drug"; "o-t" |]

                let p = p |> PlanService.removeContext "c-t"
                p.NutritionContexts |> Expect.isEmpty "none left"
                ids p |> Expect.equal "the drug only" [| "o-drug" |]
            }

            test "the log names the command, never the plan" {
                let p = OrderPlan.create Patient.empty [||]

                Api654.PlanCommand.toString (Api654.PlanCommand.Navigate(p, Some "c-1", UpdateOrderContext, OrderContext.empty))
                |> Expect.equal "context" "Navigate context UpdateOrderContext"

                Api654.PlanCommand.toString (Api654.PlanCommand.AddContext(p, NutritionCategory.TPN))
                |> Expect.equal "category" "AddContext TPN"
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
