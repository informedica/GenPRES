module Informedica.GenPRES.Shared.Tests.ModelsTests

open Expecto
open Expecto.Flip
open Shared.Types
open Shared.Models


[<Tests>]
let tests =
    testList
        "OrderContext.label"
        [
            test "a workbench not in the plan has the empty id and is a drug" {
                OrderContext.empty.Id |> Expect.equal "no id" ""
                OrderContext.empty.Category |> Expect.equal "a drug" OrderCategory.Drug
            }

            test "a drug's label is its generic, empty before one is chosen" {
                OrderContext.empty |> OrderContext.label |> Expect.equal "nothing yet" ""

                { OrderContext.empty with OrderContext.Filter.Generic = Some "paracetamol" }
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
                                OrderContext.Filter.Generic = Some "Glucose 10%"
                            }
                            |> OrderContext.label
                            |> Expect.equal "the category's name" expected
                        }
                ]
        ]


[<Tests>]
let planTests =
    let tpn =
        { OrderContext.empty with
            Id = "c-t"
            Category = OrderCategory.Nutrition NutritionCategory.TPN
        }

    let drug = { OrderContext.empty with Id = "c-d" }

    testList
        "OrderPlan.nutritionContexts"
        [
            test "a drug has no nutrition category, a nutrition context its own" {
                drug |> OrderContext.nutritionCategory |> Expect.equal "a drug" None

                tpn
                |> OrderContext.nutritionCategory
                |> Expect.equal "tpn" (Some NutritionCategory.TPN)
            }

            test "the nutrition contexts are the ones with a nutrition category" {
                { OrderPlan.empty with OrderContexts = [| drug; tpn |] }
                |> OrderPlan.nutritionContexts
                |> Array.map _.Id
                |> Expect.equal "the tpn" [| "c-t" |]
            }
        ]


module PatientFixtures =

    let ten = { Patient.Age.ageZero with Age.Years = 10<year> }

    let measured (w: int<gram>) (h: int<cm>) (dto: PatientDto) =
        { dto with
            PatientDto.Weight.Measured = Some w
            PatientDto.Height.Measured = Some h
        }


open PatientFixtures


[<Tests>]
let patientTests =
    testList
        "Patient.fromDto"
        [
            test "an age alone is a patient" {
                { PatientDto.empty with Age = Some ten }
                |> Patient.fromDto
                |> Result.isOk
                |> Expect.isTrue "a patient"
            }

            test "a measured weight and height without an age is a patient" {
                PatientDto.empty
                |> measured 32000<gram> 140<cm>
                |> Patient.fromDto
                |> Result.isOk
                |> Expect.isTrue "a patient"
            }

            test "a weight alone is not: the height is needed too" {
                { PatientDto.empty with PatientDto.Weight.Measured = Some 32000<gram> }
                |> Patient.fromDto
                |> Expect.equal "no patient" (Error PatientError.NoAgeOrMeasuredWeightAndHeight)
            }

            test "an estimated weight and height do not count: the estimate follows an age" {
                { PatientDto.empty with
                    PatientDto.Weight.Estimated = Some 32000<gram>
                    PatientDto.Height.Estimated = Some 140<cm>
                }
                |> Patient.fromDto
                |> Expect.equal "no patient" (Error PatientError.NoAgeOrMeasuredWeightAndHeight)
            }

            test "the blank draft is not a patient" {
                PatientDto.empty
                |> Patient.fromDto
                |> Expect.equal "no patient" (Error PatientError.NoAgeOrMeasuredWeightAndHeight)
            }

            test "to the wire and back is the draft it came from" {
                let dto =
                    { PatientDto.empty with
                        Age = Some ten
                        Department = Some "ICK"
                    }
                    |> measured 32000<gram> 140<cm>

                dto
                |> Patient.fromDto
                |> Result.map Patient.toDto
                |> Expect.equal "the same draft" (Ok dto)
            }
        ]


module EstimateFixtures =

    let row sex age p3 mean p97 : NormalValue =
        {
            Sex = sex
            Age = age
            P3 = p3
            Mean = mean
            P97 = p97
        }

    let weights = Some [ row "M" 10. 25. 32. 40. ]
    let heights = Some [ row "M" 10. 130. 140. 150. ]

    let estimated (dto: PatientDto) =
        dto |> PatientDto.applyNormalValues weights heights None None


open EstimateFixtures


[<Tests>]
let estimateTests =
    testList
        "the estimate stays an estimate"
        [
            test "nothing measured: the estimate is written, the measured value stays empty" {
                let dto =
                    { PatientDto.empty with
                        Age = Some ten
                        Gender = Male
                    }
                    |> estimated

                (dto.Weight.Estimated, dto.Weight.Measured, dto.Height.Estimated, dto.Height.Measured)
                |> Expect.equal "estimated, not measured" (Some 32000<gram>, None, Some 140<cm>, None)
            }

            test "a measured weight survives the estimate" {
                let dto =
                    { PatientDto.empty with
                        Age = Some ten
                        Gender = Male
                        PatientDto.Weight.Measured = Some 30000<gram>
                    }
                    |> estimated

                (dto.Weight.Measured, dto.Weight.Estimated)
                |> Expect.equal "both, apart" (Some 30000<gram>, Some 32000<gram>)
            }

            test "the readers fall back to the estimate, so what is calculated does not change" {
                { PatientDto.empty with
                    Age = Some ten
                    Gender = Male
                }
                |> estimated
                |> fun dto -> dto |> Patient.getWeight, dto |> Patient.getHeight
                |> Expect.equal "the estimate" (Some 32000<gram>, Some 140<cm>)
            }

            test "a gender chosen keeps the measured values and drops the estimates" {
                let dto =
                    { PatientDto.empty with
                        Age = Some ten
                        Gender = Male
                        PatientDto.Weight.Measured = Some 30000<gram>
                    }
                    |> estimated

                Some dto
                |> PatientDto.setGender "female"
                |> Option.map (fun p -> p.Gender, p.Weight.Measured, p.Weight.Estimated, p.Height.Estimated)
                |> Expect.equal "female, measured kept, estimates gone" (Some(Female, Some 30000<gram>, None, None))
            }

            test "a gender chosen first is a draft with the gender and nothing else" {
                None
                |> PatientDto.setGender "male"
                |> Expect.equal "the gender alone" (Some { PatientDto.empty with Gender = Male })
            }
        ]
