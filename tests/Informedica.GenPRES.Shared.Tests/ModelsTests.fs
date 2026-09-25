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
            test "picking another indication keeps every other choice" {
                // every list the answer offers is narrowed by every choice already made, so a
                // pick from one of them agrees with all of them and none has to be let go
                full
                |> OrderContext.indicationChange (Some "pijn")
                |> chosen
                |> Expect.equal
                    "only the indication moved"
                    (Some "pijn", Some "paracetamol", Some "oraal", Some "tablet", full.Filter.DoseType)
            }

            test "picking another medication keeps the route picked before it" {
                full
                |> OrderContext.medicationChange (Some "ibuprofen")
                |> chosen
                |> Expect.equal
                    "only the medication moved"
                    (Some "koorts", Some "ibuprofen", Some "oraal", Some "tablet", full.Filter.DoseType)
            }

            test "picking leaves every option list standing" {
                full
                |> OrderContext.medicationChange (Some "ibuprofen")
                |> options
                |> Expect.equal
                    "every list as it was"
                    ([| "koorts"; "pijn" |],
                     [| "paracetamol"; "ibuprofen" |],
                     [| "oraal"; "rectaal" |],
                     [| "tablet"; "drank" |],
                     [| DoseType.Discontinuous "" |])
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

            test "clearing a field lets the choices below it go" {
                // the filter widens, so what stood on the choice goes with it
                full
                |> OrderContext.routeChange None
                |> chosen
                |> Expect.equal "the two at the top" (Some "koorts", Some "paracetamol", None, None, None)
            }

            test "clearing the medication leaves the indication beside it" {
                full
                |> OrderContext.medicationChange None
                |> _.Filter.Indication
                |> Expect.equal "still chosen" (Some "koorts")
            }

            test "clearing a field empties the options it was picked from" {
                // else a list of one would be chosen again at once and the field could not be
                // emptied at all
                let f = (full |> OrderContext.routeChange None).Filter

                (f.Routes, f.Route) |> Expect.equal "its own list and choice gone" ([||], None)
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
                |> OrderContext.doseTypeChange None
                |> OrderContext.formChange None
                |> OrderContext.routeChange None
                |> OrderContext.medicationChange None
                |> OrderContext.indicationChange None
                |> options
                |> Expect.equal "every list empty" ([||], [||], [||], [||], [||])
            }

            test "a nutrition indication keeps the composition above it" {
                nutrition
                |> OrderContext.indicationChange (Some "pijn")
                |> _.Filter.Generic
                |> Expect.equal "still chosen" (Some "paracetamol")
            }

            test "clearing a nutrition composition lets its indication go" {
                // there the indication follows from the composition, and stands in a step below
                nutrition
                |> OrderContext.medicationChange None
                |> _.Filter.Indication
                |> Expect.equal "let go" None
            }
        ]


/// A patient with everything set, measured and estimated, so that every field has something
/// to lose.
let fullPatient: Patient =
    {
        Age =
            Some
                {
                    Years = 2<year>
                    Months = 3<month>
                    Weeks = 1<week>
                    Days = 4<day>
                }
        GestationalAge =
            Some
                {
                    Weeks = 32<week>
                    Days = 5<day>
                }
        Weight =
            {
                EstimatedP3 = Some 10000<gram>
                Estimated = Some 12000<gram>
                EstimatedP97 = Some 14000<gram>
                Measured = Some 12500<gram>
            }
        Height =
            {
                EstimatedP3 = Some 85<cm>
                Estimated = Some 90<cm>
                EstimatedP97 = Some 95<cm>
                Measured = Some 91<cm>
            }
        Gender = Female
        Access = [ CVL; EnteralTube ]
        RenalFunction = Some(EGFR(Some 30, Some 50))
        Location = Some "bed 4"
        Department = Some "ICK"
    }


/// The age setters by name, each applied to a value that fits its field.
let ageSetters =
    [
        "setYear", Patient.setYear (Some "5")
        "setMonth", Patient.setMonth (Some "7")
        "setWeek", Patient.setWeek (Some "2")
        "setDay", Patient.setDay (Some "3")
    ]


let patient (p: Patient option) = p |> Option.defaultWith (fun () -> failtest "no patient")


[<Tests>]
let ageSetterTests =
    testList
        "Patient age setters keep what was measured"
        [
            testList
                "an age edit keeps the measured weight and height"
                [
                    for name, set in ageSetters do
                        test $"{name}" {
                            let p = Some fullPatient |> set |> patient
                            p.Weight.Measured |> Expect.equal "the weight" fullPatient.Weight.Measured
                            p.Height.Measured |> Expect.equal "the height" fullPatient.Height.Measured
                        }
                ]

            testList
                "an age edit keeps the gestational age, gender, access, renal function, location and department"
                [
                    for name, set in ageSetters do
                        test $"{name}" {
                            let p = Some fullPatient |> set |> patient
                            p.GestationalAge
                            |> Expect.equal "the gestational age" fullPatient.GestationalAge
                            p.Gender |> Expect.equal "the gender" fullPatient.Gender
                            p.Access |> Expect.equal "the access" fullPatient.Access
                            p.RenalFunction |> Expect.equal "the renal function" fullPatient.RenalFunction
                            p.Location |> Expect.equal "the location" fullPatient.Location
                            p.Department |> Expect.equal "the department" fullPatient.Department
                        }
                ]

            testList
                "the estimates are blank after an age edit"
                [
                    for name, set in ageSetters do
                        test $"{name}" {
                            let p = Some fullPatient |> set |> patient
                            p.Weight.Estimated |> Expect.isNone "weight estimate"
                            p.Weight.EstimatedP3 |> Expect.isNone "weight p3"
                            p.Weight.EstimatedP97 |> Expect.isNone "weight p97"
                            p.Height.Estimated |> Expect.isNone "height estimate"
                            p.Height.EstimatedP3 |> Expect.isNone "height p3"
                            p.Height.EstimatedP97 |> Expect.isNone "height p97"
                        }
                ]

            testList
                "each setter writes its own part of the age"
                [
                    test "setYear" {
                        Some fullPatient
                        |> Patient.setYear (Some "5")
                        |> patient
                        |> _.Age
                        |> Expect.equal "years" (Some { fullPatient.Age.Value with Years = 5<year> })
                    }

                    test "setMonth" {
                        Some fullPatient
                        |> Patient.setMonth (Some "7")
                        |> patient
                        |> _.Age
                        |> Expect.equal "months" (Some { fullPatient.Age.Value with Months = 7<month> })
                    }

                    test "setWeek" {
                        Some fullPatient
                        |> Patient.setWeek (Some "2")
                        |> patient
                        |> _.Age
                        |> Expect.equal "weeks" (Some { fullPatient.Age.Value with Weeks = 2<week> })
                    }

                    test "setDay" {
                        Some fullPatient
                        |> Patient.setDay (Some "3")
                        |> patient
                        |> _.Age
                        |> Expect.equal "days" (Some { fullPatient.Age.Value with Days = 3<day> })
                    }
                ]

            testList
                "a draft's age"
                [
                    test "a blank draft given a year is a patient of that age alone" {
                        None
                        |> Patient.setYear (Some "5")
                        |> patient
                        |> Expect.equal
                            "five years, nothing else"
                            { Patient.empty with Age = Some { Patient.Age.ageZero with Years = 5<year> } }
                    }

                    test "a blank draft with a year cleared is the blank draft, as it was" {
                        None |> Patient.setYear None |> Expect.equal "blank" (Some Patient.empty)
                    }

                    test "a newborn on day zero is a patient with an age" {
                        None
                        |> Patient.setDay (Some "0")
                        |> patient
                        |> _.Age
                        |> Expect.equal "age zero" (Some Patient.Age.ageZero)
                    }

                    test "clearing one age part keeps the age with that part zero" {
                        Some fullPatient
                        |> Patient.setMonth None
                        |> patient
                        |> _.Age
                        |> Expect.equal "months zero" (Some { fullPatient.Age.Value with Months = 0<month> })
                    }

                    test "a value that is not a number reads as zero" {
                        Some fullPatient
                        |> Patient.setYear (Some "five")
                        |> patient
                        |> _.Age
                        |> Expect.equal "years zero" (Some { fullPatient.Age.Value with Years = 0<year> })
                    }
                ]
        ]
