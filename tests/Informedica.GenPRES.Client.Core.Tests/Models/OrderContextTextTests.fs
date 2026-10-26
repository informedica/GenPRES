/// What the pages call an order context.
module Informedica.GenPRES.Client.Core.Tests.Models.OrderContextTextTests

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models
open Informedica.GenPRES.Client.Core.Models


[<Tests>]
let tests =
    testList
        "OrderContextText.label"
        [
            test "a drug's label is its generic, empty before one is chosen" {
                OrderContext.empty |> OrderContextText.label |> Expect.equal "nothing yet" ""

                { OrderContext.empty with OrderContext.Filter.Generic = Some "paracetamol" }
                |> OrderContextText.label
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
                                OrderContext.Filter.Generic = Some "Glucose 10%"
                            }
                            |> OrderContextText.label
                            |> Expect.equal "the category's name" expected
                        }
                ]
        ]
