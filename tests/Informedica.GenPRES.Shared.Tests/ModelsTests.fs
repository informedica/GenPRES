module Informedica.GenPRES.Shared.Tests.ModelsTests

open Expecto
open Expecto.Flip
open Informedica.GenPRES.Shared.Types
open Informedica.GenPRES.Shared.Models


[<Tests>]
let tests =
    testList
        "OrderContext.empty"
        [
            test "a workbench not in the plan has the empty id and is a drug" {
                OrderContext.empty.Id |> Expect.equal "no id" ""
                OrderContext.empty.Category |> Expect.equal "a drug" OrderCategory.Drug
            }

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


module NeonatalEstimateFixtures =

    /// The neonatal tables, in weeks of post-conceptional age, reaching to 42 weeks as the
    /// sheet does.
    let neoWeights = Some [ row "M" 40. 2800. 3500. 4200.; row "M" 42. 3000. 3700. 4500. ]

    let neoHeights = Some [ row "M" 40. 48. 50. 53.; row "M" 42. 50. 52. 55. ]

    let estimated (dto: Patient) =
        dto |> Patient.applyNormalValues weights heights neoWeights neoHeights


    let term: GestAge =
        {
            Weeks = 40<week>
            Days = 0<day>
        }

    let premature: GestAge =
        {
            Weeks = 30<week>
            Days = 0<day>
        }


[<Tests>]
let neonatalEstimateTests =
    testList
        "the neonatal tables answer while they reach"
        [
            test "a term newborn one week old is estimated from the neonatal tables" {
                { Patient.empty with
                    Age = Some { Patient.Age.ageZero with Weeks = 1<week> }
                    GestationalAge = Some NeonatalEstimateFixtures.term
                    Gender = Male
                }
                |> NeonatalEstimateFixtures.estimated
                |> fun dto -> dto.Weight.Estimated, dto.Height.Estimated
                |> Expect.equal "41 weeks post-conceptional: the 40-week row" (Some 3500<gram>, Some 50<cm>)
            }

            test "an infant of six months with a gestational age is estimated from the age tables" {
                { Patient.empty with
                    Age = Some { Patient.Age.ageZero with Months = 6<month> }
                    GestationalAge = Some NeonatalEstimateFixtures.premature
                    Gender = Male
                }
                |> NeonatalEstimateFixtures.estimated
                |> fun dto -> dto.Weight.Estimated, dto.Height.Estimated
                |> Expect.equal
                    "past 42 weeks: the age tables, not the last neonatal row"
                    (Some 32000<gram>, Some 140<cm>)
            }

            test "each table is judged by its own reach: a neonatal weight is kept when the height table is missing" {
                { Patient.empty with
                    Age = Some { Patient.Age.ageZero with Weeks = 1<week> }
                    GestationalAge = Some NeonatalEstimateFixtures.term
                    Gender = Male
                }
                |> Patient.applyNormalValues weights heights NeonatalEstimateFixtures.neoWeights None
                |> fun dto -> dto.Weight.Estimated, dto.Height.Estimated
                |> Expect.equal "neonatal weight, age-table height" (Some 3500<gram>, Some 140<cm>)
            }

            test "a neonatal table with no row for the sex answers nothing, and the age table does" {
                { Patient.empty with
                    Age = Some { Patient.Age.ageZero with Weeks = 1<week> }
                    GestationalAge = Some NeonatalEstimateFixtures.term
                    Gender = Female
                }
                |> Patient.applyNormalValues
                    (Some [ row "F" 10. 25. 30. 40. ])
                    heights
                    NeonatalEstimateFixtures.neoWeights
                    NeonatalEstimateFixtures.neoHeights
                |> fun dto -> dto.Weight.Estimated, dto.Height.Estimated
                |> Expect.equal
                    "male-only neonatal tables: the age table for her, no height row at all"
                    (Some 30000<gram>, None)
            }

            test "without the neonatal tables a newborn is estimated from the age tables" {
                { Patient.empty with
                    Age = Some { Patient.Age.ageZero with Weeks = 1<week> }
                    GestationalAge = Some NeonatalEstimateFixtures.term
                    Gender = Male
                }
                |> estimated
                |> _.Weight.Estimated
                |> Expect.equal "the age tables" (Some 32000<gram>)
            }
        ]


module RowFixtures =

    /// Two tables, one row per sex at two years: a boy of two is twelve kilograms and 87
    /// centimetres, a girl eleven and 86.
    let rows: Map<string, string[][]> =
        Map
            [
                "weight",
                [|
                    [| "sex"; "age"; "p3"; "mean"; "p97" |]
                    [| "M"; "2"; "10"; "12"; "14" |]
                    [| "F"; "2"; "9"; "11"; "13" |]
                |]
                "height",
                [|
                    [| "sex"; "age"; "p3"; "mean"; "p97" |]
                    [| "M"; "2"; "80"; "87"; "94" |]
                    [| "F"; "2"; "79"; "86"; "93" |]
                |]
            ]


open RowFixtures


[<Tests>]
let normalValuesOfRowsTests =
    testList
        "the tables from the rows"
        [
            test "each sheet parses to its table, a sheet absent giving an empty one" {
                let nv = rows |> NormalValues.ofRows

                (nv.Weights |> List.length, nv.Heights |> List.length, nv.NeoWeights, nv.NeoHeights)
                |> Expect.equal "two rows each, no neonatal tables" (2, 2, [], [])

                nv.Weights
                |> List.find (fun r -> r.Sex = "M")
                |> Expect.equal "the boy's row" (NormalValues.create "M" 2. 10. 12. 14.)
            }

            test "the tables applied are the estimate the panel shows" {
                let pat =
                    { Patient.empty with
                        Age = Some(Patient.Age.fromDays 730)
                        Gender = Male
                    }
                    |> NormalValues.apply (rows |> NormalValues.ofRows)

                (pat.Weight.Estimated, pat.Height.Estimated, pat.Weight.Measured)
                |> Expect.equal
                    "twelve kilograms, 87 centimetres, nothing measured"
                    (Some 12000<gram>, Some 87<cm>, None)
            }
        ]
