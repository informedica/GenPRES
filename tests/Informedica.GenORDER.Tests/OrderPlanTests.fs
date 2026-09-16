/// The rules of the one order plan on the domain types: what its contexts contribute,
/// which the row filter keeps, what it admits, and what leaves with what.
module OrderPlanTests

open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Expecto
open Expecto.Flip


module Fixtures =

    let order =
        match Scenarios.pcmSupp |> Medication.toOrderDto |> Order.Dto.fromDto with
        | Ok o -> o
        | Error e -> invalidOp $"fixture order could not be created: {e}"


    let filter: Filter =
        {
            Indications = [| "koorts"; "pijn"; "voeding" |]
            Generics = [| "paracetamol"; "nutrini"; "glucose" |]
            Routes = [||]
            Forms = [||]
            DoseTypes = [||]
            Diluents = [| "NaCl 0.9%" |]
            Components = [||]
            Indication = None
            Generic = Some "paracetamol"
            Route = None
            Form = None
            DoseType = None
            Diluent = None
            SelectedComponents = [| "paracetamol" |]
        }


    /// A scenario whose order carries the id, so that a plan's orders can be told apart.
    let scenario orderId : OrderScenario =
        {
            No = 1
            Name = orderId
            Indication = ""
            Form = ""
            Route = ""
            DoseType = Informedica.GenForm.Lib.Types.NoDoseType
            Diluent = None
            Component = None
            Item = None
            Diluents = [||]
            Components = [||]
            Items = [||]
            Prescription = [||]
            Preparation = [||]
            Administration = [||]
            Order = { order with Id = Id orderId }
            UseAdjust = false
            UseRenalRule = false
            RenalRule = None
            ProductsIds = [||]
        }


    let context (scenarios: OrderScenario[]) : OrderContext =
        {
            Filter = filter
            Patient = Patient.patient
            Scenarios = scenarios
        }


    let drug id orderId =
        PlanContext.create id OrderCategory.Drug (context [| scenario orderId |])


    let nutrition id category (orderIds: string[]) =
        PlanContext.create id (OrderCategory.Nutrition category) (context (orderIds |> Array.map scenario))


    let plan contexts =
        OrderPlan.create Patient.patient contexts


    let ids (p: OrderPlan) =
        OrderPlan.orders p |> Array.map (fun s -> let (Id id) = s.Order.Id in id)


    let ruleSets: NutritionRuleSet[] =
        [|
            {
                Category = NutritionCategory.TPN
                Label = "TPN"
                Indications = [| "voeding" |]
                Generics = [| "glucose" |]
            }
            {
                Category = NutritionCategory.EnteralFeeding
                Label = "Feeding"
                Indications = [||]
                Generics = [||]
            }
        |]


open Fixtures


let tests =
    testList
        "OrderPlan"
        [
            test "the orders of the plan are what its narrowed contexts contribute, in context order" {
                let wide = nutrition "c-w" NutritionCategory.TPN [| "o-1"; "o-2" |]
                let none = nutrition "c-n" NutritionCategory.Lipid [||]

                plan [| drug "c-d" "o-d"; wide; none; drug "c-e" "o-e" |]
                |> ids
                |> Expect.equal "the narrowed ones" [| "o-d"; "o-e" |]

                wide |> PlanContext.contribution |> Expect.isNone "two candidates"
                none |> PlanContext.contribution |> Expect.isNone "no candidate"
            }

            test "a drug has no nutrition category, a nutrition context its own" {
                drug "c-d" "o-d" |> PlanContext.nutritionCategory |> Expect.equal "a drug" None

                nutrition "c-t" NutritionCategory.TPN [||]
                |> PlanContext.nutritionCategory
                |> Expect.equal "tpn" (Some NutritionCategory.TPN)
            }

            test "the nutrition contexts are the ones with a nutrition category" {
                plan
                    [|
                        drug "c-d" "o-d"
                        nutrition "c-t" NutritionCategory.TPN [||]
                    |]
                |> OrderPlan.nutritionContexts
                |> Array.map _.Id
                |> Expect.equal "the tpn" [| "c-t" |]
            }

            test "a plan created on a version has its contexts, nothing filtered and no totals" {
                let p = plan [| drug "c-d" "o-d" |]
                p.Filtered |> Expect.isEmpty "no filter"
                p.Totals |> Expect.equal "no totals" Totals.empty
                p.Contexts |> Array.map _.Id |> Expect.equal "as given" [| "c-d" |]
                p.Contexts[0].Intake |> Expect.equal "no intake yet" Totals.empty
            }

            test "the filter names contexts: empty keeps all, otherwise those named" {
                let tpn = nutrition "c-1" NutritionCategory.TPN [| "o-tpn" |]
                let p = plan [| drug "c-d" "o-drug"; tpn |]

                p
                |> OrderPlan.filtered
                |> Array.map _.Id
                |> Expect.equal "all" [| "c-d"; "c-1" |]

                { p with Filtered = [| "c-1" |] }
                |> OrderPlan.filtered
                |> Array.map _.Id
                |> Expect.equal "the tpn" [| "c-1" |]
            }

            test "an evaluated context takes its place, with the id and category the plan gave it" {
                let p =
                    plan
                        [|
                            drug "c-d" "o-drug"
                            nutrition "c-1" NutritionCategory.Lipid [||]
                        |]

                let evaluated =
                    { PlanContext.create "" OrderCategory.Drug (context [| scenario "o-lipid" |]) with
                        Intake = { Totals.empty with Energy = Some "10 kcal" }
                    }

                match p |> OrderPlan.updateContext ruleSets "c-1" evaluated with
                | Ok p ->
                    let c = p.Contexts[1]
                    c.Id |> Expect.equal "the plan's id" "c-1"

                    c.Category
                    |> Expect.equal "the plan's category" (OrderCategory.Nutrition NutritionCategory.Lipid)

                    c.Intake.Energy |> Expect.equal "the intake as evaluated" (Some "10 kcal")
                    ids p |> Expect.equal "its order follows" [| "o-drug"; "o-lipid" |]
                | Error e -> failtest $"{e}"

                p
                |> OrderPlan.updateContext ruleSets "c-9" evaluated
                |> Expect.equal "no such context" (Error(OrderPlanError.NoSuchContext "c-9"))
            }

            test "a nutrition context's pick lists are narrowed to its category's rule set" {
                let p =
                    plan
                        [|
                            nutrition "c-t" NutritionCategory.TPN [||]
                            nutrition "c-f" NutritionCategory.EnteralFeeding [||]
                        |]

                let evaluated = PlanContext.create "" OrderCategory.Drug (context [||])

                match p |> OrderPlan.updateContext ruleSets "c-t" evaluated with
                | Ok p ->
                    let f = p.Contexts[0].Context.Filter
                    f.Indications |> Expect.equal "the set's indications" [| "voeding" |]
                    f.Generics |> Expect.equal "the set's generics" [| "glucose" |]
                    f.Diluents |> Expect.equal "diluents pass through" [| "NaCl 0.9%" |]

                    f.SelectedComponents
                    |> Expect.equal "the selection passes through" [| "paracetamol" |]
                | Error e -> failtest $"{e}"

                match p |> OrderPlan.updateContext ruleSets "c-f" evaluated with
                | Ok p ->
                    p.Contexts[1].Context.Filter
                    |> Expect.equal "an empty set restricts nothing" filter
                | Error e -> failtest $"{e}"

                match p |> OrderPlan.updateContext [||] "c-t" evaluated with
                | Ok p -> p.Contexts[0].Context.Filter |> Expect.equal "no set: nothing narrowed" filter
                | Error e -> failtest $"{e}"
            }

            test "a removed context takes its order; a feeding takes its supplements and theirs" {
                let p =
                    plan
                        [|
                            drug "c-d" "o-drug"
                            nutrition "c-f" NutritionCategory.EnteralFeeding [| "o-f" |]
                            nutrition "c-s" NutritionCategory.EnteralSupplement [| "o-s" |]
                            nutrition "c-t" NutritionCategory.TPN [| "o-t" |]
                        |]

                let p = p |> OrderPlan.removeOrderContext "c-f"

                p.Contexts
                |> Array.map _.Id
                |> Expect.equal "the drug and the tpn stay" [| "c-d"; "c-t" |]

                ids p |> Expect.equal "with their orders" [| "o-drug"; "o-t" |]

                let p = p |> OrderPlan.removeOrderContext "c-t"
                ids p |> Expect.equal "the drug only" [| "o-drug" |]
            }

            test "a removed supplement leaves its feeding; a removed context leaves the filter" {
                let p =
                    { plan
                          [|
                              nutrition "c-f" NutritionCategory.EnteralFeeding [| "o-f" |]
                              nutrition "c-s" NutritionCategory.EnteralSupplement [| "o-s" |]
                          |] with
                        Filtered = [| "c-f"; "c-s" |]
                    }

                let p = p |> OrderPlan.removeOrderContext "c-s"
                p.Contexts |> Array.map _.Id |> Expect.equal "the feeding stays" [| "c-f" |]
                p.Filtered |> Expect.equal "gone from the filter" [| "c-f" |]
            }

            test "removing contexts of every kind: each takes its order, a feeding its supplements" {
                plan
                    [|
                        drug "c-d" "o-d"
                        nutrition "c-f" NutritionCategory.EnteralFeeding [| "o-f" |]
                        nutrition "c-s" NutritionCategory.EnteralSupplement [| "o-s" |]
                        nutrition "c-t" NutritionCategory.TPN [| "o-t" |]
                    |]
                |> OrderPlan.removeOrderContexts [| "c-d"; "c-f" |]
                |> ids
                |> Expect.equal "the tpn's order" [| "o-t" |]
            }

            test "the workbench into the plan as a drug context, once, and only narrowed to one order" {
                let newId () = "c-new"

                let workbench =
                    { PlanContext.create "" OrderCategory.Drug (context [| scenario "o-p" |]) with
                        Intake = { Totals.empty with Volume = Some "5 ml" }
                    }

                let p =
                    plan [| drug "c-d" "o-drug" |]
                    |> OrderPlan.addOrderContext newId workbench
                    |> Result.defaultWith (fun e -> failtest $"{e}")

                let added = p.Contexts[1]
                added.Id |> Expect.equal "the minted id" "c-new"
                added.Category |> Expect.equal "a drug" OrderCategory.Drug

                added.Context.Filter.Generic
                |> Expect.equal "the workbench as it was" (Some "paracetamol")

                added.Intake.Volume |> Expect.equal "with its intake" (Some "5 ml")
                ids p |> Expect.equal "its order after the others" [| "o-drug"; "o-p" |]

                p
                |> OrderPlan.addOrderContext newId workbench
                |> Expect.equal "the same order twice refused" (Error(OrderPlanError.OrderHeld "o-p"))

                let wide = { workbench with Context = context [| scenario "o-1"; scenario "o-2" |] }

                plan [||]
                |> OrderPlan.addOrderContext newId wide
                |> Expect.equal "a workbench not narrowed refused" (Error(OrderPlanError.NotNarrowed 2))
            }

            test "one context per nutrition category, and a supplement only under a feeding" {
                let feeding = nutrition "c-f" NutritionCategory.EnteralFeeding [||]

                plan [| feeding |]
                |> OrderPlan.admits NutritionCategory.EnteralFeeding
                |> Expect.equal
                    "a second feeding refused"
                    (Error(OrderPlanError.CategoryHeld NutritionCategory.EnteralFeeding))

                plan [||]
                |> OrderPlan.admits NutritionCategory.EnteralSupplement
                |> Expect.equal "a supplement without a feeding refused" (Error OrderPlanError.SupplementNeedsFeeding)

                plan [| feeding |]
                |> OrderPlan.admits NutritionCategory.EnteralSupplement
                |> Expect.equal "a supplement under a feeding admitted" (Ok())

                plan
                    [|
                        feeding
                        nutrition "c-s" NutritionCategory.EnteralSupplement [||]
                    |]
                |> OrderPlan.admits NutritionCategory.EnteralSupplement
                |> Expect.equal "a second supplement admitted" (Ok())

                plan [| feeding |]
                |> OrderPlan.admits NutritionCategory.TPN
                |> Expect.equal "another category admitted" (Ok())

                plan
                    [|
                        nutrition "c-e" NutritionCategory.ElectrolyteGlucose [||]
                    |]
                |> OrderPlan.admits NutritionCategory.ElectrolyteGlucose
                |> Expect.equal "a second electrolyte line admitted" (Ok())

                plan [| feeding |]
                |> OrderPlan.holds NutritionCategory.EnteralFeeding
                |> Expect.isTrue "held"

                plan [| feeding |]
                |> OrderPlan.holds NutritionCategory.TPN
                |> Expect.isFalse "not held"
            }
        ]
