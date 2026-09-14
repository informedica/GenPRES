// The category on the context (plan 667, step 2): every order context says what kind of order
// it holds, a drug or one of the nutrition categories, and carries its own id in the plan. The
// label a page shows is derived from that: the category's name for nutrition, the generic for
// a drug. Nothing else changes yet: `NutritionContext` keeps wrapping the nutrition workbenches
// until the plan becomes its contexts (step 3).
//
// Script-first draft (script-only policy) of what goes to `Shared/Types.fs` and
// `Shared/Models.fs`:
//   - `OrderCategory`, next to `NutritionCategory` (which moves above `OrderContext`);
//   - `OrderContext.Id` and `OrderContext.Category`, the empty id and `Drug` in
//     `OrderContext.empty` and `fromOrderScenario`;
//   - `NutritionCategory.label` and `OrderContext.label`.
// The record below is the target shape of `OrderContext` reduced to what the step touches; the
// migration adds the two fields to the real record.
//
// Run: `dotnet fsi Api.fsx` from this directory.

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"
#r "nuget: Fable.Remoting.Json, 3.0"

#load "../Types.fs"
#load "../Calculations.fs"
#load "../Utils.fs"
#load "../Localization.fs"
#load "../Models.fs"
#load "../Api.fs"

open Shared.Types


/// → `Shared/Types.fs`, after `NutritionCategory` (moved above `OrderContext`).
module Types667 =

    /// What kind of order a context holds: a drug, or a nutrition order of one of the
    /// categories. Recorded on the context and kept with it, so that a reopened plan
    /// knows each order's kind without guessing from the generic (KCl, NaCl and glucose are
    /// nutrition generics and drugs both).
    [<RequireQualifiedAccess>]
    type OrderCategory =
        | Drug
        | Nutrition of NutritionCategory


    /// `OrderContext` as the step leaves it, reduced to the fields it touches: the id the
    /// context has in the plan (empty for a workbench not in the plan), the category, and the
    /// filter the label is derived from.
    type OrderContext667 =
        {
            // empty until the context is in the plan; minted by the server when it is added
            Id: string
            Category: OrderCategory
            Filter: Filter
        }


/// → `Shared/Models.fs`, a `NutritionCategory` module before `OrderContext`, and two lines
/// plus `label` in `OrderContext`.
module Models667 =

    open Types667

    module NutritionCategory =

        /// The category's name, as the nutrition page shows it (today the label of the
        /// server's dose-rule set; from here the one source).
        let label category =
            match category with
            | NutritionCategory.EnteralFeeding -> "Enterale Voeding"
            | NutritionCategory.EnteralSupplement -> "Enteraal Supplement"
            | NutritionCategory.TPN -> "Totale Parenterale Voeding"
            | NutritionCategory.Lipid -> "Vetten"
            | NutritionCategory.ElectrolyteGlucose -> "Elektrolyten/Glucose"


    module OrderContext =

        let empty: OrderContext667 =
            {
                Id = ""
                Category = OrderCategory.Drug
                Filter = Shared.Models.OrderContext.filter
            }


        /// What a page calls the context: the category's name for a nutrition order, the
        /// generic for a drug, nothing before a generic is chosen.
        let label (ctx: OrderContext667) =
            match ctx.Category with
            | OrderCategory.Nutrition category -> NutritionCategory.label category
            | OrderCategory.Drug -> ctx.Filter.Generic |> Option.defaultValue ""


open Expecto
open Expecto.Flip
open Newtonsoft.Json
open Fable.Remoting.Json
open Types667
open Models667


let converters = [| FableJsonConverter() :> JsonConverter |]
let toJson (v: 'a) = JsonConvert.SerializeObject(v, converters)
let ofJson<'a> (json: string) = JsonConvert.DeserializeObject<'a>(json, converters)


let tests =
    testList
        "the category on the context"
        [
            test "a workbench not in the plan has the empty id and is a drug" {
                OrderContext.empty.Id |> Expect.equal "no id" ""
                OrderContext.empty.Category |> Expect.equal "a drug" OrderCategory.Drug
            }

            test "a drug's label is its generic, empty before one is chosen" {
                OrderContext.empty |> OrderContext.label |> Expect.equal "nothing yet" ""

                { OrderContext.empty with
                    OrderContext667.Filter.Generic = Some "paracetamol"
                }
                |> OrderContext.label
                |> Expect.equal "the generic" "paracetamol"
            }

            testList
                "a nutrition order's label is its category's name, whatever the generic"
                [
                    for category, expected in
                        [
                            NutritionCategory.EnteralFeeding, "Enterale Voeding"
                            NutritionCategory.EnteralSupplement, "Enteraal Supplement"
                            NutritionCategory.TPN, "Totale Parenterale Voeding"
                            NutritionCategory.Lipid, "Vetten"
                            NutritionCategory.ElectrolyteGlucose, "Elektrolyten/Glucose"
                        ] do
                        test $"{category}" {
                            { OrderContext.empty with
                                Category = OrderCategory.Nutrition category
                                OrderContext667.Filter.Generic = Some "Glucose 10%"
                            }
                            |> OrderContext.label
                            |> Expect.equal "the category's name" expected
                        }
                ]

            test "the category round-trips on the wire, both kinds" {
                for ctx in
                    [
                        { OrderContext.empty with Id = "c-1" }
                        { OrderContext.empty with
                            Id = "c-2"
                            Category = OrderCategory.Nutrition NutritionCategory.ElectrolyteGlucose
                        }
                    ] do
                    ctx |> toJson |> ofJson<OrderContext667> |> Expect.equal "the same context back" ctx
            }
        ]


runTestsWithCLIArgs [] [||] tests |> ignore
