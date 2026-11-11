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
                OrderContext.empty.Category
                |> Expect.equal "a medication" OrderCategory.Medication
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
        "OrderContext.nutritionCategory"
        [
            test "a drug has no nutrition category, a nutrition context its own" {
                drug |> OrderContext.nutritionCategory |> Expect.equal "a drug" None

                tpn
                |> OrderContext.nutritionCategory
                |> Expect.equal "tpn" (Some NutritionCategory.TPN)
            }
        ]


module PatientFixtures =

    let ten = { Patient.Age.ageZero with Age.Years = 10<year> }

    /// An age of whole years.
    let years n = Patient.Age.create (n * 1<year>) None None None |> Some


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

            test "a patient marked as an adult without an age is a patient" {
                { Patient.empty with AdultMark = AdultMark.MarkableAdult true }
                |> Patient.validate
                |> Result.isOk
                |> Expect.isTrue "a patient"
            }
        ]


[<Tests>]
let selectsRulesTests =
    testList
        "Patient.selectsRules"
        [
            test "the rules are selected for an adult, and for a child with a weight" {
                [
                    { Patient.empty with
                        Age = years 40
                        AdultMark = AdultMark.Adult
                    }
                    { Patient.empty with
                        Age = years 17
                        AdultMark = AdultMark.MarkableAdult true
                    }
                    { Patient.empty with AdultMark = AdultMark.MarkableAdult true }
                    { Patient.empty with
                        Age = years 5
                        Patient.Weight.Estimated = Some 18000<gram>
                    }
                    { Patient.empty with Age = years 5 }
                    { Patient.empty with Age = years 17 }
                ]
                |> List.map Patient.selectsRules
                |> Expect.equal "adults and a child with a weight" [ true; true; true; true; false; false ]
            }
        ]


[<Tests>]
let isAdultTests =
    testList
        "Patient.isAdult"
        [
            test "an adult by the mark: from 18 years, or marked by the user" {
                [
                    AdultMark.NotAdult
                    AdultMark.MarkableAdult false
                    AdultMark.MarkableAdult true
                    AdultMark.Adult
                ]
                |> List.map (fun mark -> { Patient.empty with AdultMark = mark } |> Patient.isAdult)
                |> Expect.equal "the adults" [ false; false; true; true ]
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


[<Tests>]
let gestationalAgeTests =
    let awd w d : AgeWeeksDays =
        {
            Weeks = w * 1<week>
            Days = d * 1<day>
        }
    let ga30 = awd 30 0

    let all =
        [
            GestationalAge.NoGestationalAge
            GestationalAge.OptionalGestationalAge None
            GestationalAge.OptionalGestationalAge(Some ga30)
            GestationalAge.RequiredGestationalAge None
            GestationalAge.RequiredGestationalAge(Some ga30)
        ]

    testList
        "the gestational age"
        [
            test "the value, given or not" {
                all
                |> List.map GestationalAge.value
                |> Expect.equal "the values" [ None; None; Some ga30; None; Some ga30 ]
            }

            test "map changes the value when it may be given, and leaves no gestational age unchanged" {
                all
                |> List.map (GestationalAge.map (fun _ -> Some(awd 26 2)))
                |> Expect.equal
                    "the gestational ages"
                    [
                        GestationalAge.NoGestationalAge
                        GestationalAge.OptionalGestationalAge(Some(awd 26 2))
                        GestationalAge.OptionalGestationalAge(Some(awd 26 2))
                        GestationalAge.RequiredGestationalAge(Some(awd 26 2))
                        GestationalAge.RequiredGestationalAge(Some(awd 26 2))
                    ]
            }

            test "the gestational age may be given, except when there is none to give" {
                all
                |> List.map GestationalAge.mayBeGiven
                |> Expect.equal "may be given" [ false; true; true; true; true ]
            }

            test "the gestational age is missing only when it is needed and not given" {
                all
                |> List.map GestationalAge.isMissing
                |> Expect.equal "missing" [ false; false; false; true; false ]
            }
        ]


/// A choice that decides the dose rule lets go of the components of the dose rule, as the domain's change
/// and seed do; the next scenario sets them again.
[<Tests>]
let doseRuleComponentsTests =
    let additives = [| "NaCl 3%"; "KCl 7,4%" |]
    let names = [| "Samenstelling C"; "NaCl 3%"; "KCl 7,4%"; "gluc 10%" |]

    let first =
        { OrderContext.empty with
            Filter =
                { OrderContext.filter with
                    Generic = Some "Samenstelling C"
                    Components = additives
                    DoseRuleComponents = names
                    SelectedComponents = ComponentSelection.Selected [ "KCl 7,4%" ]
                }
        }

    let components (ctx: OrderContext) =
        ctx.Filter.Components, ctx.Filter.DoseRuleComponents, ctx.Filter.SelectedComponents

    let none = [||], [||], ComponentSelection.AllComponents

    testList
        "the components of the dose rule after another dose rule is picked"
        [
            test "a dose type picked lets go of them" {
                first
                |> OrderContext.doseTypeChange (Some(DoseType.Once "dag 3"))
                |> components
                |> Expect.equal "emptied" none
            }

            test "a medication emptied lets go of them" {
                first
                |> OrderContext.medicationChange None
                |> components
                |> Expect.equal "emptied" none
            }

            test "the same dose type keeps them" {
                let held = { first with OrderContext.Filter.DoseType = Some(DoseType.Once "dag 2") }

                held
                |> OrderContext.doseTypeChange (Some(DoseType.Once "dag 2"))
                |> components
                |> Expect.equal "kept" (components held)
            }

            test "a diluent picked keeps them" {
                first
                |> OrderContext.diluentChange (Some "gluc 20%")
                |> components
                |> Expect.equal "kept" (components first)
            }

            test "a seed of another medication from the formulary lets go of them" {
                first
                |> OrderContext.seedFilter SeedSource.Formulary None (Some "Intralipid 20%") None None None
                |> components
                |> Expect.equal "emptied" none
            }

            test "a seed of the same medication with another dose type lets go of them" {
                let f = first.Filter

                first
                |> OrderContext.seedFilter
                    SeedSource.Formulary
                    f.Indication
                    f.Generic
                    f.Route
                    f.Form
                    (Some(DoseType.Once "dag 3"))
                |> components
                |> Expect.equal "emptied" none
            }

            test "a seed of the same picks keeps them" {
                let f = first.Filter

                first
                |> OrderContext.seedFilter SeedSource.Url f.Indication f.Generic f.Route f.Form f.DoseType
                |> components
                |> Expect.equal "kept" (components first)
            }
        ]
