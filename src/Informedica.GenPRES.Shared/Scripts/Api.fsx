// Scenarios and Selected off the plan, Filtered as context ids (plan 667, step 7). The plan is
// its contexts and nothing beside them: its orders are what the narrowed contexts contribute,
// derived wherever they are read; the dialog's selection is the client's own; the row filter
// names contexts by id, which stays valid when a context is re-evaluated and its order changes.
// The server's projection bookkeeping (a stored `Scenarios` kept in step, a filter and a
// selection following a replaced order) goes with the fields.
//
// Script-first draft (script-only policy) of what goes to `Shared/Types.fs` and
// `Shared/Models.fs`: the plan's final shape, `create` over contexts, `filtered`.
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


/// → `Shared/Types.fs`, in place of `OrderPlan`.
type OrderPlan667 =
    {
        Patient: Patient
        // the contexts shown and counted, by id; empty for all of them
        Filtered: string[]
        // the order contexts of the plan, drug and nutrition alike, each saying what it holds
        // and carrying its id; the plan's orders are what the narrowed ones contribute
        OrderContexts: OrderContext[]
        Totals: Totals
    }


/// → `Shared/Models.fs`, `module OrderPlan`.
module OrderPlan667 =

    let create pat contexts : OrderPlan667 =
        {
            Patient = pat
            Filtered = [||]
            OrderContexts = contexts
            Totals = Shared.Models.Totals.empty
        }


    /// The orders the plan's contexts contribute: the one scenario of every context narrowed
    /// to one, in context order.
    let orders (plan: OrderPlan667) =
        plan.OrderContexts |> Array.choose Shared.Models.OrderContext.contribution


    /// The contexts the filter keeps: those named by id, all of them when it is empty.
    let filtered (plan: OrderPlan667) =
        if plan.Filtered |> Array.isEmpty then
            plan.OrderContexts
        else
            plan.OrderContexts |> Array.filter (fun c -> plan.Filtered |> Array.contains c.Id)


open Expecto
open Expecto.Flip
open Shared.Models


let ctx id = { OrderContext.empty with Id = id }


let tests =
    testList
        "the plan is its contexts and nothing beside them"
        [
            test "the filter names contexts by id; empty keeps all" {
                let plan = OrderPlan667.create Patient.empty [| ctx "c-1"; ctx "c-2"; ctx "c-3" |]

                plan |> OrderPlan667.filtered |> Array.map _.Id |> Expect.equal "all" [| "c-1"; "c-2"; "c-3" |]

                { plan with Filtered = [| "c-3"; "c-1" |] }
                |> OrderPlan667.filtered
                |> Array.map _.Id
                |> Expect.equal "the named ones, in plan order" [| "c-1"; "c-3" |]

                { plan with Filtered = [| "c-9" |] }
                |> OrderPlan667.filtered
                |> Expect.isEmpty "an id the plan does not hold keeps nothing"
            }

            test "a context re-evaluated keeps its place in the filter, whatever its order becomes" {
                let plan = { OrderPlan667.create Patient.empty [| ctx "c-1"; ctx "c-2" |] with Filtered = [| "c-2" |] }

                let replaced =
                    { plan with
                        OrderContexts = plan.OrderContexts |> Array.map (fun c -> if c.Id = "c-2" then { c with DemoVersion = true } else c)
                    }

                replaced |> OrderPlan667.filtered |> Array.map _.Id |> Expect.equal "still c-2" [| "c-2" |]
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
