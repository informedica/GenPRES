/// The nutrition rule sets the server owns: one per category, the values the nutrition
/// service still holds, the label the client shows.
module Informedica.GenPRES.Server.Tests.NutritionRuleSetsTests

open Expecto
open Expecto.Flip
open Informedica.GenOrder.Lib
open ServerApi


let categories =
    [
        NutritionCategory.EnteralFeeding
        NutritionCategory.EnteralSupplement
        NutritionCategory.TPN
        NutritionCategory.Lipid
        NutritionCategory.ElectrolyteGlucose
    ]


/// The contract model's category, for the service's copy and the client's label.
let sharedCategory category =
    match category with
    | NutritionCategory.EnteralFeeding -> Shared.Types.NutritionCategory.EnteralFeeding
    | NutritionCategory.EnteralSupplement -> Shared.Types.NutritionCategory.EnteralSupplement
    | NutritionCategory.TPN -> Shared.Types.NutritionCategory.TPN
    | NutritionCategory.Lipid -> Shared.Types.NutritionCategory.Lipid
    | NutritionCategory.ElectrolyteGlucose -> Shared.Types.NutritionCategory.ElectrolyteGlucose


/// The service's copy of a category, until step 4.3 removes it.
let serviceCopy category =
    category |> sharedCategory |> ServerApi.NutritionPlanService.getDoseRuleSet


[<Tests>]
let tests =
    testList
        "nutrition rule sets"
        [
            test "one set per category, every category served" {
                NutritionRuleSets.all
                |> Array.map _.Category
                |> Array.toList
                |> List.sort
                |> Expect.equal "each once" (categories |> List.sort)

                for category in categories do
                    NutritionRuleSets.all
                    |> NutritionRuleSet.tryFind category
                    |> Option.map _.Category
                    |> Expect.equal $"{category} found" (Some category)
            }

            test "each set says what the service's copy says" {
                for category in categories do
                    let set = NutritionRuleSets.all |> NutritionRuleSet.tryFind category |> Option.get
                    let copy = serviceCopy category
                    set.Label |> Expect.equal $"{category} label" copy.Label
                    set.Indications |> Expect.equal $"{category} indications" copy.Indications
                    set.Generics |> Expect.equal $"{category} generics" copy.Generics
                    copy.DoseTypes |> Expect.isEmpty $"{category}: no dose types to carry"
            }

            test "each label is the one the client shows for the category" {
                for category in categories do
                    let set = NutritionRuleSets.all |> NutritionRuleSet.tryFind category |> Option.get

                    set.Label
                    |> Expect.equal
                        $"{category} label"
                        (category |> sharedCategory |> Shared.Models.NutritionCategory.label)
            }

            test "no set is empty: every category names at least one indication and one generic" {
                for set in NutritionRuleSets.all do
                    set.Indications |> Expect.isNonEmpty $"{set.Category} indications"
                    set.Generics |> Expect.isNonEmpty $"{set.Category} generics"
            }
        ]
