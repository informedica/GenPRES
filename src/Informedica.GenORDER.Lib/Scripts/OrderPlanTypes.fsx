// Step 1.1 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #733):
// the order plan as domain types, next to OrderContext. Prototype per the script-only
// policy in AGENTS.md; the `OrderPlanTypes` module below is the block to add to
// Types.fs after `OrderContext`, comments included, with the module wrapper removed.
//
// Run from this directory: dotnet fsi OrderPlanTypes.fsx

#I __SOURCE_DIRECTORY__

#r "nuget: Expecto"

#load "load.fsx"

open System
open Informedica.GenOrder.Lib


/// The block for Types.fs, after `OrderContext`.
module OrderPlanTypes =

    /// The kinds of nutrition order an order plan holds, one context per category
    /// except supplements (any number, each under a feeding) and the electrolyte
    /// and glucose lines (any number).
    [<RequireQualifiedAccess>]
    type NutritionCategory =
        | EnteralFeeding
        | EnteralSupplement
        | TPN
        | Lipid
        | ElectrolyteGlucose


    /// What kind of order an order context holds: a drug, or a nutrition order of
    /// one category. Recorded on the context, never derived from the generic, since
    /// electrolytes and glucose are prescribable as drugs too.
    [<RequireQualifiedAccess>]
    type OrderCategory =
        | Drug
        | Nutrition of NutritionCategory


    /// <summary>
    /// An order context as held in an order plan: its id in the plan, its category,
    /// the context itself, and its intake. The context is wrapped, not extended, so
    /// evaluation and the equation system never see the plan's bookkeeping.
    /// </summary>
    /// <remarks>
    /// The intake is computed when the context is evaluated and copied unchanged
    /// everywhere else, so a signed version stores the intake the signer saw.
    /// </remarks>
    type PlanContext =
        {
            // The context's id in the order plan
            Id: string
            // A drug, or the nutrition category the context holds
            Category: OrderCategory
            // The order context, evaluated within its own patient
            Context: OrderContext
            // The totals over the one scenario the context is narrowed to
            Intake: Totals
        }


    /// <summary>
    /// The one order plan for a patient: the patient data as shown, the plan's
    /// contexts, the ids the row filter keeps, and the totals over the orders of the
    /// filtered contexts. Its orders are derived, the scenario of every narrowed
    /// context; nothing beside the contexts is stored.
    /// </summary>
    type OrderPlan =
        {
            // The patient data as shown, the data a version is signed on
            Patient: Patient
            // The ids of the contexts the row filter keeps; empty for all of them
            Filtered: string[]
            // Every order context of the plan, drug and nutrition alike
            Contexts: PlanContext[]
            // The totals over the orders of the filtered contexts
            Totals: Totals
        }


    /// What a nutrition category draws from: data passed in by the composition
    /// root, never a constant in this library.
    type NutritionRuleSet =
        {
            // The category the set serves
            Category: NutritionCategory
            // The label shown for the category
            Label: string
            // The indications the category's dose rules are filtered on
            Indications: string[]
            // The generics the category's dose rules are filtered on
            Generics: string[]
        }


    /// Who signed an order plan version
    type Signer =
        {
            // The user id at the identity provider
            UserId: string
            // The name shown for the signer
            DisplayName: string
        }


    /// <summary>
    /// An order plan version: the order plan as the prescriber signed it, as the
    /// record holds it. A prescriber creates one by signing; it stores the whole
    /// plan, filter, totals and each context's intake included, so a reopen restores
    /// what the signer saw, nothing recomputed.
    /// </summary>
    type OrderPlanVersion =
        {
            // The version's own id
            Id: string
            // The version number, one above the patient's previous version
            No: int
            // The patient the plan belongs to
            PatientId: string
            // The id of the version this one was built on; None for the first
            Base: string option
            // Who signed
            SignedBy: Signer
            // When
            SignedAt: DateTime
            // The order plan as signed
            Plan: OrderPlan
            // Whether the patient data was verified against the platform's reading
            // at the challenge
            Verified: bool
        }


// ---------------------------------------------------------------------------
// Tests: the types can be built and hold what they are given. Goes to
// tests/Informedica.GenORDER.Tests/Tests.fs, TypeTests.
// ---------------------------------------------------------------------------

open Expecto
open Expecto.Flip
open OrderPlanTypes


module Fixtures =

    let filter: Filter =
        {
            Indications = [||]
            Generics = [||]
            Routes = [||]
            Forms = [||]
            DoseTypes = [||]
            Diluents = [||]
            Components = [||]
            Indication = None
            Generic = None
            Route = None
            Form = None
            DoseType = None
            Diluent = None
            SelectedComponents = [||]
        }


    let totals: Totals =
        {
            Volume = None
            Energy = None
            Protein = None
            Carbohydrate = None
            Fat = None
            Sodium = None
            Potassium = None
            Chloride = None
            Calcium = None
            Phosphate = None
            Magnesium = None
            Iron = None
            VitaminD = None
            Ethanol = None
            Propyleenglycol = None
            BenzylAlcohol = None
            BoricAcid = None
        }


    let patient: Patient = Informedica.GenForm.Lib.Patient.patient


    let context: OrderContext =
        {
            Filter = filter
            Patient = patient
            Scenarios = [||]
        }


    let planContext id category =
        {
            Id = id
            Category = category
            Context = context
            Intake = totals
        }


    let plan contexts =
        {
            Patient = patient
            Filtered = [||]
            Contexts = contexts
            Totals = totals
        }


let tests =
    testList
        "OrderPlan types"
        [
            test "a plan context wraps an order context with its id, category and intake" {
                let pc = Fixtures.planContext "ctx-1" OrderCategory.Drug

                pc.Context |> Expect.equal "the context is held as given" Fixtures.context
                pc.Id |> Expect.equal "the id is the plan's" "ctx-1"
                pc.Category |> Expect.equal "a drug" OrderCategory.Drug
                pc.Intake |> Expect.equal "the intake is held as given" Fixtures.totals
            }

            test "a nutrition context carries its category" {
                let pc =
                    Fixtures.planContext "ctx-2" (OrderCategory.Nutrition NutritionCategory.TPN)

                match pc.Category with
                | OrderCategory.Nutrition cat -> cat |> Expect.equal "TPN" NutritionCategory.TPN
                | OrderCategory.Drug -> failtest "a nutrition context is not a drug"
            }

            test "an order plan holds its contexts, its filter and its totals" {
                let contexts =
                    [|
                        Fixtures.planContext "ctx-1" OrderCategory.Drug
                        Fixtures.planContext "ctx-2" (OrderCategory.Nutrition NutritionCategory.EnteralFeeding)
                    |]

                let plan = { Fixtures.plan contexts with Filtered = [| "ctx-2" |] }

                plan.Contexts |> Array.map _.Id |> Expect.equal "both contexts" [| "ctx-1"; "ctx-2" |]
                plan.Filtered |> Expect.equal "the filter keeps one" [| "ctx-2" |]
                plan.Patient |> Expect.equal "the patient as shown" Fixtures.patient
            }

            test "an order plan version stores the whole plan and its identity" {
                let plan = Fixtures.plan [| Fixtures.planContext "ctx-1" OrderCategory.Drug |]

                let version =
                    {
                        Id = "v-1"
                        No = 1
                        PatientId = "stub-patient"
                        Base = None
                        SignedBy = { UserId = "u-1"; DisplayName = "Stub Prescriber" }
                        SignedAt = DateTime(2026, 9, 16, 12, 0, 0)
                        Plan = plan
                        Verified = true
                    }

                version.Plan |> Expect.equal "the plan as signed" plan
                version.Base |> Expect.isNone "the first version has no base"
                version.SignedBy.DisplayName |> Expect.equal "the signer" "Stub Prescriber"

                let second =
                    { version with
                        Id = "v-2"
                        No = 2
                        Base = Some version.Id
                    }

                second.Base |> Expect.equal "the second is built on the first" (Some "v-1")
                second.Plan |> Expect.equal "the plan is copied, not recomputed" version.Plan
            }

            test "a nutrition rule set names what its category draws from" {
                let set =
                    {
                        Category = NutritionCategory.Lipid
                        Label = "Lipid"
                        Indications = [| "parenterale voeding" |]
                        Generics = [| "smoflipid" |]
                    }

                set.Generics |> Expect.equal "the generics of the set" [| "smoflipid" |]
            }
        ]


runTestsWithCLIArgs [] [| "--summary" |] tests
