// The category on the context (plan 667, step 2), the server half: `PlanService.addContext`
// stamps the context it creates with the id it mints and the nutrition category, so that the
// context says what it holds wherever it goes; `evaluate` hands both back unchanged, since it
// copies the input context (`Mappers.mapToShared` is `{ ctx with ... }`).
//
// Script-first draft (script-only policy) of the two lines that go to `ServerApi.Services.fs`,
// `PlanService.addContext`, over a record shaped like `OrderContext` after the step (the
// compiled Shared has no `Id`/`Category` yet).
//
// Run: `dotnet fsi Plan.fsx` from this directory (build first).

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open Shared.Types


[<RequireQualifiedAccess>]
type OrderCategory =
    | Drug
    | Nutrition of NutritionCategory


/// `OrderContext` after the step, reduced to what `addContext` sets.
type OrderContext667 =
    {
        Id: string
        Category: OrderCategory
        Scenarios: OrderScenario[]
    }


/// → `ServerApi.Services.fs`, `PlanService.addContext`, on the `Some resolved` branch: the
/// discovered context gets the id and the category before it is wrapped and appended.
module PlanService667 =

    let stamp (newId: unit -> string) category (resolved: OrderContext667) =
        { resolved with
            Id = newId ()
            Category = OrderCategory.Nutrition category
        }


open Expecto
open Expecto.Flip


let tests =
    testList
        "addContext stamps the context"
        [
            test "the id minted and the nutrition category, the rest untouched" {
                let resolved =
                    {
                        Id = ""
                        Category = OrderCategory.Drug
                        Scenarios = [||]
                    }

                let stamped = resolved |> PlanService667.stamp (fun () -> "c-1") NutritionCategory.TPN

                stamped
                |> Expect.equal
                    "stamped"
                    { resolved with
                        Id = "c-1"
                        Category = OrderCategory.Nutrition NutritionCategory.TPN
                    }
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
