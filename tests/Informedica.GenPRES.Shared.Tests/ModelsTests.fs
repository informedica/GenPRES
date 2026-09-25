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


[<Tests>]
let mayAddTests =
    let context id category =
        { OrderContext.empty with
            Id = id
            Category = OrderCategory.Nutrition category
        }

    let plan contexts = { OrderPlan.empty with OrderContexts = contexts }

    let all =
        [
            NutritionCategory.EnteralFeeding
            NutritionCategory.EnteralSupplement
            NutritionCategory.TPN
            NutritionCategory.Lipid
            NutritionCategory.ElectrolyteGlucose
        ]

    testList
        "OrderPlan.mayAdd"
        [
            test "an empty plan takes every category but a supplement, which needs a feeding" {
                all
                |> List.map (fun c -> plan [||] |> OrderPlan.mayAdd c)
                |> Expect.equal "feeding, tpn, lipid, electrolyte; no supplement" [ true; false; true; true; true ]
            }

            test
                "a feeding, a tpn and a lipid are taken once; a supplement under a feeding and an electrolyte line any number of times" {
                let full =
                    plan
                        [|
                            context "c-f" NutritionCategory.EnteralFeeding
                            context "c-s" NutritionCategory.EnteralSupplement
                            context "c-t" NutritionCategory.TPN
                            context "c-l" NutritionCategory.Lipid
                            context "c-e" NutritionCategory.ElectrolyteGlucose
                        |]

                all
                |> List.map (fun c -> full |> OrderPlan.mayAdd c)
                |> Expect.equal "supplement and electrolyte still" [ false; true; false; false; true ]
            }

            test "a drug context holds no category: the plan takes any nutrition context" {
                let drugs = plan [| { OrderContext.empty with Id = "c-d" } |]

                all
                |> List.filter (fun c -> c <> NutritionCategory.EnteralSupplement)
                |> List.forall (fun c -> drugs |> OrderPlan.mayAdd c)
                |> Expect.isTrue "every category but the supplement"
            }
        ]


module PatientFixtures =

    let ten = { Patient.Age.ageZero with Age.Years = 10<year> }

    let measured (w: int<gram>) (h: int<cm>) (dto: Patient) =
        { dto with
            Patient.Weight.Measured = Some w
            Patient.Height.Measured = Some h
        }


open PatientFixtures


[<Tests>]
let patientTests =
    testList
        "Patient.validate"
        [
            test "an age alone is a patient" {
                { Patient.empty with Age = Some ten }
                |> Patient.validate
                |> Result.isOk
                |> Expect.isTrue "a patient"
            }

            test "a measured weight and height without an age is a patient" {
                Patient.empty
                |> measured 32000<gram> 140<cm>
                |> Patient.validate
                |> Result.isOk
                |> Expect.isTrue "a patient"
            }

            test "a weight alone is not: the height is needed too" {
                { Patient.empty with Patient.Weight.Measured = Some 32000<gram> }
                |> Patient.validate
                |> Expect.equal "no patient" (Error PatientError.NoAgeOrMeasuredWeightAndHeight)
            }

            test "an estimated weight and height do not count: the estimate follows an age" {
                { Patient.empty with
                    Patient.Weight.Estimated = Some 32000<gram>
                    Patient.Height.Estimated = Some 140<cm>
                }
                |> Patient.validate
                |> Expect.equal "no patient" (Error PatientError.NoAgeOrMeasuredWeightAndHeight)
            }

            test "the blank draft is not a patient" {
                Patient.empty
                |> Patient.validate
                |> Expect.equal "no patient" (Error PatientError.NoAgeOrMeasuredWeightAndHeight)
            }

            test "a patient is the draft unchanged: validate adds nothing" {
                let dto =
                    { Patient.empty with
                        Age = Some ten
                        Department = Some "ICK"
                    }
                    |> measured 32000<gram> 140<cm>

                dto |> Patient.validate |> Expect.equal "the same draft" (Ok dto)
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

    let estimated (dto: Patient) = dto |> Patient.applyNormalValues weights heights None None


open EstimateFixtures


[<Tests>]
let estimateTests =
    testList
        "the estimate stays an estimate"
        [
            test "nothing measured: the estimate is written, the measured value stays empty" {
                let dto =
                    { Patient.empty with
                        Age = Some ten
                        Gender = Male
                    }
                    |> estimated

                (dto.Weight.Estimated, dto.Weight.Measured, dto.Height.Estimated, dto.Height.Measured)
                |> Expect.equal "estimated, not measured" (Some 32000<gram>, None, Some 140<cm>, None)
            }

            test "a measured weight survives the estimate" {
                let dto =
                    { Patient.empty with
                        Age = Some ten
                        Gender = Male
                        Patient.Weight.Measured = Some 30000<gram>
                    }
                    |> estimated

                (dto.Weight.Measured, dto.Weight.Estimated)
                |> Expect.equal "both, apart" (Some 30000<gram>, Some 32000<gram>)
            }

            test "the readers fall back to the estimate, so what is calculated does not change" {
                { Patient.empty with
                    Age = Some ten
                    Gender = Male
                }
                |> estimated
                |> fun dto -> dto |> Patient.getWeight, dto |> Patient.getHeight
                |> Expect.equal "the estimate" (Some 32000<gram>, Some 140<cm>)
            }

            test "a gender chosen keeps the measured values and drops the estimates" {
                let dto =
                    { Patient.empty with
                        Age = Some ten
                        Gender = Male
                        Patient.Weight.Measured = Some 30000<gram>
                    }
                    |> estimated

                Some dto
                |> Patient.setGender "female"
                |> Option.map (fun p -> p.Gender, p.Weight.Measured, p.Weight.Estimated, p.Height.Estimated)
                |> Expect.equal "female, measured kept, estimates gone" (Some(Female, Some 30000<gram>, None, None))
            }

            test "a gender chosen first is a draft with the gender and nothing else" {
                None
                |> Patient.setGender "male"
                |> Expect.equal "the gender alone" (Some { Patient.empty with Gender = Male })
            }
        ]


/// The five fields the prescribing page shows in a row, filled in as a user who has built a
/// filter out would have them.
let full: OrderContext =
    { OrderContext.empty with
        Filter =
            { OrderContext.empty.Filter with
                Indications = [| "koorts"; "pijn" |]
                Indication = Some "koorts"
                Generics = [| "paracetamol"; "ibuprofen" |]
                Generic = Some "paracetamol"
                Routes = [| "oraal"; "rectaal" |]
                Route = Some "oraal"
                Forms = [| "tablet"; "drank" |]
                Form = Some "tablet"
                DoseTypes = [| DoseType.Discontinuous "" |]
                DoseType = Some(DoseType.Discontinuous "")
            }
    }


/// The same, as the nutrition page holds it: a composition picked first, with an indication
/// under it.
let nutrition: OrderContext =
    { full with Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding }


/// The five choices, to compare in one go.
let chosen (ctx: OrderContext) =
    ctx.Filter.Indication, ctx.Filter.Generic, ctx.Filter.Route, ctx.Filter.Form, ctx.Filter.DoseType


/// The options every field was picked from, to compare in one go.
let options (ctx: OrderContext) =
    ctx.Filter.Indications, ctx.Filter.Generics, ctx.Filter.Routes, ctx.Filter.Forms, ctx.Filter.DoseTypes


[<Tests>]
let cascadeTests =
    testList
        "the cascade of the prescribing fields"
        [
            test "picking another indication keeps it and clears the four below" {
                full
                |> OrderContext.indicationChange (Some "pijn")
                |> chosen
                |> Expect.equal "only the indication" (Some "pijn", None, None, None, None)
            }

            test "picking another indication empties the options below it" {
                full
                |> OrderContext.indicationChange (Some "pijn")
                |> options
                |> Expect.equal "its own options stand, the rest go" ([| "koorts"; "pijn" |], [||], [||], [||], [||])
            }

            test "picking another medication keeps the indication and clears the three below" {
                full
                |> OrderContext.medicationChange (Some "ibuprofen")
                |> chosen
                |> Expect.equal "indication and generic" (Some "koorts", Some "ibuprofen", None, None, None)
            }

            test "picking another route keeps the two above and clears the two below" {
                full
                |> OrderContext.routeChange (Some "rectaal")
                |> chosen
                |> Expect.equal "up to the route" (Some "koorts", Some "paracetamol", Some "rectaal", None, None)
            }

            test "picking another form keeps the three above and clears the dose type" {
                full
                |> OrderContext.formChange (Some "drank")
                |> chosen
                |> Expect.equal "up to the form" (Some "koorts", Some "paracetamol", Some "oraal", Some "drank", None)
            }

            test "picking another dose type keeps every choice above it" {
                let dt = DoseType.Timed "" |> Some

                full
                |> OrderContext.doseTypeChange dt
                |> chosen
                |> Expect.equal "all five" (Some "koorts", Some "paracetamol", Some "oraal", Some "tablet", dt)
            }

            test "every change clears the scenarios" {
                // a scenario is never read here, only counted, so an uninhabited one will do
                let withScenario = { full with Scenarios = Array.zeroCreate<OrderScenario> 1 }

                [
                    withScenario |> OrderContext.indicationChange (Some "pijn")
                    withScenario |> OrderContext.medicationChange (Some "ibuprofen")
                    withScenario |> OrderContext.routeChange (Some "rectaal")
                    withScenario |> OrderContext.formChange (Some "drank")
                    withScenario |> OrderContext.doseTypeChange (DoseType.Timed "" |> Some)
                ]
                |> List.forall (fun c -> c.Scenarios |> Array.isEmpty)
                |> Expect.isTrue "none left, whichever field changed"
            }

            test "clearing a field clears it and everything below" {
                full
                |> OrderContext.medicationChange None
                |> chosen
                |> Expect.equal "the indication alone" (Some "koorts", None, None, None, None)
            }

            test "clearing a field empties the options it was picked from" {
                // else a list of one would be chosen again at once and the field could not be
                // emptied at all
                full
                |> OrderContext.routeChange None
                |> options
                |> Expect.equal
                    "the two above keep theirs"
                    ([| "koorts"; "pijn" |], [| "paracetamol"; "ibuprofen" |], [||], [||], [||])
            }

            test "picking the value a field already holds changes nothing" {
                full
                |> OrderContext.medicationChange (Some "paracetamol")
                |> Expect.equal "the context it was" full
            }

            test "with no choice left anywhere, no options are kept" {
                // a list narrowed by choices that are gone would offer a smaller world than
                // there is, and say nothing about it
                full
                |> OrderContext.medicationChange None
                |> OrderContext.indicationChange None
                |> options
                |> Expect.equal "every list empty" ([||], [||], [||], [||], [||])
            }

            test "a nutrition indication stands below the composition, so it keeps it" {
                nutrition
                |> OrderContext.indicationChange (Some "pijn")
                |> chosen
                |> Expect.equal
                    "the composition kept, the dose type gone"
                    (Some "pijn", Some "paracetamol", Some "oraal", Some "tablet", None)
            }

            test "a nutrition composition stands above the indication, so it clears it" {
                nutrition
                |> OrderContext.medicationChange (Some "ibuprofen")
                |> chosen
                |> Expect.equal "the composition alone" (None, Some "ibuprofen", Some "oraal", Some "tablet", None)
            }
        ]
