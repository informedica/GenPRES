// The last family and the old shape's end (plan 654, step 9): `processOrderContext`, a
// `Request<OrderContextCommand * OrderContext>` answered with a `Reply<OrderContext>` through
// `Compute.bound`; with it `processCommand`, `Command`, `Response`, the `Request`/`Reply`
// abbreviations and `ServerApi.Command.fs` go. And the seam the one plan left: deleting orders
// on the order-plan page goes to the server as `PlanCommand.RemoveOrders`, so that a nutrition
// order deleted there takes its workbench with it instead of coming back at the workbench's
// next move.
//
// Script-first draft (script-only policy) of:
//   - `OrderContextCommand.toString` (the command alone, never the context) and
//     `processOrderContext` on `IServerApi` → `Shared/Api.fs`; the old shape deleted there;
//   - `OrderContextCommand.processCmd` → new `ServerApi.OrderContextCommand.fs` (fsproj, both
//     loaders); `ServerApi.Command.fs` deleted; the member in `compose`;
//   - `PlanCommand.RemoveOrders of OrderPlan * ids: string[]` → `Shared/Api.fs`;
//     `PlanService.removeOrders` → `Services.fs`; `PlanPort.removeOrders` → `Ports.fs`,
//     `Adapters.fs`; the arm → `ServerApi.PlanCommand.fs`;
//   - the client: the order context on its member, the plan page's delete on `RemoveOrders`
//     (in the patch).
//
// Run: `dotnet fsi Compute.fsx` from this directory (build first).

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
// The order-context member (→ ServerApi.OrderContextCommand.fs; toString → Shared/Api.fs)
// ---------------------------------------------------------------------------------------------

module OrderContextCommand =

    /// For the log: the command alone, never the context.
    let toString (cmd: OrderContextCommand, _: OrderContext) =
        Command.toString (OrderContextCmd(cmd, OrderContext.empty))


    let processCmd (env: AppEnv) (cmd: OrderContextCommand, ctx: OrderContext) = env.orderContext.evaluate cmd ctx


// and in `compose` (→ CompositionRoot.fs):
//
//     processOrderContext =
//         Compute.bound env cookie OrderContextCommand.toString (fun _ -> Gate.RequiresLoaded) (OrderContextCommand.processCmd env)
//
// on `IServerApi` (→ Shared/Api.fs):
//
//     processOrderContext: Request<OrderContextCommand * OrderContext> -> Async<Result<Reply<OrderContext>, string[]>>


// ---------------------------------------------------------------------------------------------
// The deletion seam (→ Services.fs, module PlanService; PlanCommand.RemoveOrders → Shared/Api.fs)
// ---------------------------------------------------------------------------------------------

module PlanService =

    let contribution = ServerApi.PlanService.contribution
    let withOrders = ServerApi.PlanService.withOrders
    let removeContext = ServerApi.PlanService.removeContext

    /// The orders named removed from the plan: each with the workbench that contributed it, a
    /// feeding with its supplements and theirs, the rest by order id; the filter and the
    /// selection follow.
    let removeOrders (ids: string[]) (plan: OrderPlan) =
        let contributed (nc: NutritionContext) =
            contribution nc |> Option.exists (fun sc -> ids |> Array.contains sc.Order.Id)

        let plan =
            plan.NutritionContexts
            |> Array.filter contributed
            |> Array.fold (fun (p: OrderPlan) (nc: NutritionContext) -> p |> removeContext nc.Id) plan

        ids
        |> Array.fold
            (fun (p: OrderPlan) id ->
                match p.Scenarios |> Array.tryFind (fun sc -> sc.Order.Id = id) with
                | Some sc -> p |> withOrders (Some sc) None
                | None -> p
            )
            plan


// ---------------------------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------------------------

open Expecto
open Expecto.Flip


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


let plan contexts scenarios =
    { OrderPlan.create Patient.empty scenarios with NutritionContexts = contexts }


let ids (p: OrderPlan) = p.Scenarios |> Array.map _.Order.Id


let tests =
    testList
        "the last family and the deletion seam"
        [
            test "the order-context log names the command, never the context" {
                OrderContextCommand.toString (UpdateOrderContext, OrderContext.empty) |> Expect.equal "name" "UpdateOrderContext"

                OrderContextCommand.toString (DecreaseOrderableDoseQuantityProperty(2, true), OrderContext.empty)
                |> Expect.equal "name and its numbers" "DecreaseOrderableDoseQuantityProperty ntimes=2 useCalc=True"
            }

            test "deleting a nutrition order takes its workbench; a drug goes by id; the filter follows" {
                let tpn = context "c-t" NutritionCategory.TPN [| scenarioWithOrder "o-t" |]
                let feeding = context "c-f" NutritionCategory.EnteralFeeding [| scenarioWithOrder "o-f" |]
                let supplement = context "c-s" NutritionCategory.EnteralSupplement [| scenarioWithOrder "o-s" |]

                let p =
                    { plan
                          [| tpn; feeding; supplement |]
                          [| scenarioWithOrder "o-drug"; scenarioWithOrder "o-t"; scenarioWithOrder "o-f"; scenarioWithOrder "o-s" |] with
                        Filtered = [| scenarioWithOrder "o-drug"; scenarioWithOrder "o-t" |]
                    }

                let p = p |> PlanService.removeOrders [| "o-drug"; "o-t" |]
                ids p |> Expect.equal "the feeding and its supplement stay" [| "o-f"; "o-s" |]
                p.NutritionContexts |> Array.map _.Id |> Expect.equal "the tpn workbench went with its order" [| "c-f"; "c-s" |]
                p.Filtered |> Expect.isEmpty "the filter followed"

                let p = p |> PlanService.removeOrders [| "o-f" |]
                ids p |> Expect.isEmpty "the feeding took its supplement's order too"
                p.NutritionContexts |> Expect.isEmpty "and both workbenches"

                p |> PlanService.removeOrders [| "o-none" |] |> ids |> Expect.isEmpty "an unknown id: nothing"
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
