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


/// An offer of a link rule of the LinkRules sheet, by its generic and its dose type.
let offer generic doseType =
    { OrderContext.empty with
        OrderContext.Filter.Generic = Some generic
        OrderContext.Filter.DoseType = Some doseType
    }


[<Tests>]
let offerNameTests =
    testList
        "OrderContextText.offerName"
        [
            test "the evening dose of ataluren with its dose text" {
                offer "ataluren" (DoseType.Discontinuous "avond")
                |> OrderContextText.offerName
                |> Expect.equal "the dose text" "ataluren (avond)"
            }

            test "a supplement whose dose type has no text by its generic alone" {
                offer "Nutrilon Nenatal BMF pdr" (DoseType.Discontinuous "")
                |> OrderContextText.offerName
                |> Expect.equal "the generic" "Nutrilon Nenatal BMF pdr"
            }
        ]


[<Tests>]
let doseTypeTextTests =
    testList
        "DoseTypeText.text"
        [
            test "the evening dose of ataluren has its text" {
                DoseType.Discontinuous "avond"
                |> DoseTypeText.text
                |> Expect.equal "avond" (Some "avond")
            }

            test "a dose type without a text, or no dose type, has none" {
                DoseType.Discontinuous "" |> DoseTypeText.text |> Expect.isNone "empty"
                DoseType.NoDoseType |> DoseTypeText.text |> Expect.isNone "no dose type"
            }

            test "the description falls back to the Dutch name of the kind" {
                [
                    DoseType.Discontinuous "avond"
                    DoseType.Discontinuous ""
                    DoseType.Once ""
                    DoseType.Continuous ""
                ]
                |> List.map DoseTypeText.doseTypeToDescription
                |> Expect.equal "text or kind" [ "avond"; "onderhoud"; "eenmalig"; "continu" ]
            }
        ]
