// Step 2.1 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #745): the
// rules as domain functions on PlanContext and OrderPlan, moved from the server's order plan
// service and from the shared Models module and retyped on the domain types. No dosing: no
// rule here evaluates a context; the one narrowing (a nutrition context's pick lists to its
// category's rule set) is a filter over strings.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. Types.fs: `OrderPlanError`, after `OrderPlanVersion`.
//   2. Totals.fs: `Totals.empty` at the top of module Totals.
//   3. OrderPlan.fs (after Api.fs): `OrderContext.contribution` is added to Api.fs's module
//      OrderContext; `PlanContext`, `NutritionRuleSet` and `OrderPlan` get the rules below,
//      next to their Dtos.
//   4. tests/Informedica.GenORDER.Tests/OrderPlanTests.fs: the tests below, a new file
//      before Main.fs, registered in Tests.fs's aggregator.
//
// Run from this directory: dotnet fsi OrderPlanRules.fsx

#I __SOURCE_DIRECTORY__

#r "nuget: Expecto"

#load "load.fsx"

open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


// ---------------------------------------------------------------------------
// 1. Types.fs: the reasons an order plan refuses a change
// ---------------------------------------------------------------------------

/// Why an order plan refuses a change. The words shown for each are the server's, so
/// that the label of a category stays out of the domain.
[<RequireQualifiedAccess>]
type OrderPlanError =
    /// The plan holds no context with the id
    | NoSuchContext of id: string
    /// The workbench holds the number of candidates, not one order
    | NotNarrowed of candidates: int
    /// The plan already holds the order
    | OrderHeld of orderId: string
    /// A supplement needs a feeding in the plan
    | SupplementNeedsFeeding
    /// The plan already holds a context of the category
    | CategoryHeld of NutritionCategory


// ---------------------------------------------------------------------------
// 2. Totals.fs: the totals before anything is computed
// ---------------------------------------------------------------------------

module Totals =

    /// No totals: what a plan and a context start with, before an evaluation computes them.
    let empty: Totals =
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


// ---------------------------------------------------------------------------
// 3. Api.fs, module OrderContext: what a context contributes
// ---------------------------------------------------------------------------

module OrderContext =

    /// The order a context contributes to the plan: its scenario, once the context is
    /// narrowed to exactly one; nothing while it holds several candidates or none.
    let contribution (ctx: OrderContext) = ctx.Scenarios |> Array.tryExactlyOne


// ---------------------------------------------------------------------------
// 4. OrderPlan.fs: the rules
// ---------------------------------------------------------------------------

module PlanContext =

    /// A context as it enters the plan: its id there, its category, the context, and no
    /// intake until an evaluation computes one.
    let create id category (ctx: OrderContext) : PlanContext =
        {
            Id = id
            Category = category
            Context = ctx
            Intake = Totals.empty
        }


    /// The nutrition category of a context, none for a drug.
    let nutritionCategory (pc: PlanContext) =
        match pc.Category with
        | OrderCategory.Nutrition category -> Some category
        | OrderCategory.Drug -> None


    /// The order the context contributes to the plan, if it is narrowed to one.
    let contribution (pc: PlanContext) = pc.Context |> OrderContext.contribution


module NutritionRuleSet =

    /// The set serving a category, if the composition root supplied one.
    let tryFind category (sets: NutritionRuleSet[]) =
        sets |> Array.tryFind (fun s -> s.Category = category)


    /// The context's pick lists narrowed to the set: when the set names indications or
    /// generics, only those are kept; an empty list in the set restricts nothing. The dose
    /// types and the selection pass through.
    let narrow (set: NutritionRuleSet) (ctx: OrderContext) : OrderContext =
        let keep (allowed: string[]) (xs: string[]) =
            if allowed |> Array.isEmpty then
                xs
            else
                xs |> Array.filter (fun x -> allowed |> Array.contains x)

        { ctx with
            Filter =
                { ctx.Filter with
                    Indications = ctx.Filter.Indications |> keep set.Indications
                    Generics = ctx.Filter.Generics |> keep set.Generics
                }
        }


module OrderPlan =

    /// The plan for a patient with the contexts given, nothing filtered and no totals: what
    /// a signed version opens on, its contexts as they were, nothing evaluated.
    let create (pat: Patient) (contexts: PlanContext[]) : OrderPlan =
        {
            Patient = pat
            Filtered = [||]
            Contexts = contexts
            Totals = Totals.empty
        }


    /// The nutrition contexts of the plan.
    let nutritionContexts (plan: OrderPlan) =
        plan.Contexts |> Array.filter (PlanContext.nutritionCategory >> Option.isSome)


    /// The orders the plan's contexts contribute: the one scenario of every context narrowed
    /// to one, in context order.
    let orders (plan: OrderPlan) =
        plan.Contexts |> Array.choose PlanContext.contribution


    /// The contexts the filter keeps: those named by id, all of them when it is empty.
    let filtered (plan: OrderPlan) =
        if plan.Filtered |> Array.isEmpty then
            plan.Contexts
        else
            plan.Contexts |> Array.filter (fun c -> plan.Filtered |> Array.contains c.Id)


    /// Whether the plan holds a context of the nutrition category.
    let holds category (plan: OrderPlan) =
        plan.Contexts
        |> Array.exists (fun c -> c.Category = OrderCategory.Nutrition category)


    /// Whether the plan may take a context of the category: one context per nutrition
    /// category, except supplements (any number, each under a feeding) and electrolyte and
    /// glucose lines (any number, one per generic prescribed).
    let admits category (plan: OrderPlan) : Result<unit, OrderPlanError> =
        match category with
        | NutritionCategory.EnteralSupplement when plan |> holds NutritionCategory.EnteralFeeding |> not ->
            Error OrderPlanError.SupplementNeedsFeeding
        | NutritionCategory.EnteralSupplement
        | NutritionCategory.ElectrolyteGlucose -> Ok()
        | _ when plan |> holds category -> Error(OrderPlanError.CategoryHeld category)
        | _ -> Ok()


    /// The evaluated context into the context named, keeping the id and the category the
    /// plan gave it and, for a nutrition context, its pick lists narrowed to its category's
    /// rule set. The plan's orders follow, since they are what its contexts contribute.
    let updateContext
        (ruleSets: NutritionRuleSet[])
        id
        (evaluated: PlanContext)
        (plan: OrderPlan)
        : Result<OrderPlan, OrderPlanError>
        =
        match plan.Contexts |> Array.tryFind (fun c -> c.Id = id) with
        | None -> Error(OrderPlanError.NoSuchContext id)
        | Some held ->
            let ctx =
                match held |> PlanContext.nutritionCategory |> Option.bind (fun c -> ruleSets |> NutritionRuleSet.tryFind c) with
                | Some set -> evaluated.Context |> NutritionRuleSet.narrow set
                | None -> evaluated.Context

            let updated =
                { evaluated with
                    Id = held.Id
                    Category = held.Category
                    Context = ctx
                }

            { plan with Contexts = plan.Contexts |> Array.map (fun c -> if c.Id = id then updated else c) }
            |> Ok


    /// The context removed, and every enteral supplement with a feeding: the plan holds one
    /// feeding at most and a supplement only under it, so the feeding's supplements are all of
    /// them. Each takes its order with it, and leaves the filter.
    let removeOrderContext id (plan: OrderPlan) =
        let cascade =
            plan.Contexts
            |> Array.exists (fun c ->
                c.Id = id
                && c.Category = OrderCategory.Nutrition NutritionCategory.EnteralFeeding
            )

        let goes (c: PlanContext) =
            c.Id = id
            || (cascade
                && c.Category = OrderCategory.Nutrition NutritionCategory.EnteralSupplement)

        let gone, kept = plan.Contexts |> Array.partition goes
        let goneIds = gone |> Array.map _.Id

        { plan with
            Contexts = kept
            Filtered = plan.Filtered |> Array.filter (fun f -> goneIds |> Array.contains f |> not)
        }


    /// The contexts named removed, every kind, each with its order; a feeding takes its
    /// supplements with it.
    let removeOrderContexts (ids: string[]) (plan: OrderPlan) =
        ids |> Array.fold (fun p id -> p |> removeOrderContext id) plan


    /// The prescribing workbench into the plan as a drug context with a minted id, its one
    /// scenario the order it contributes and its intake as evaluated. Refused when the
    /// workbench is not narrowed to one scenario, and when the plan already holds that order:
    /// the signing challenge would refuse the plan later, so it is said now.
    let addOrderContext (newId: unit -> string) (workbench: PlanContext) (plan: OrderPlan) =
        match workbench |> PlanContext.contribution with
        | None -> Error(OrderPlanError.NotNarrowed workbench.Context.Scenarios.Length)
        | Some sc when orders plan |> Array.exists (fun s -> s.Order.Id = sc.Order.Id) ->
            let (Id id) = sc.Order.Id
            Error(OrderPlanError.OrderHeld id)
        | Some _ ->
            let added =
                { workbench with
                    Id = newId ()
                    Category = OrderCategory.Drug
                }

            { plan with Contexts = Array.append plan.Contexts [| added |] } |> Ok


// ---------------------------------------------------------------------------
// 5. Tests, for tests/Informedica.GenORDER.Tests/OrderPlanTests.fs
// ---------------------------------------------------------------------------

module OrderPlanTests =

    open Expecto
    open Expecto.Flip


    module Fixtures =

        let order =
            match Scenarios.pcmSupp |> Medication.toOrderDto |> Order.Dto.fromDto with
            | Ok o -> o
            | Error e -> failwith $"fixture order could not be created: {e}"


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


        let plan contexts = OrderPlan.create Patient.patient contexts


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
                    plan [| drug "c-d" "o-d"; nutrition "c-t" NutritionCategory.TPN [||] |]
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

                    p |> OrderPlan.filtered |> Array.map _.Id |> Expect.equal "all" [| "c-d"; "c-1" |]

                    { p with Filtered = [| "c-1" |] }
                    |> OrderPlan.filtered
                    |> Array.map _.Id
                    |> Expect.equal "the tpn" [| "c-1" |]
                }

                test "an evaluated context takes its place, with the id and category the plan gave it" {
                    let p = plan [| drug "c-d" "o-drug"; nutrition "c-1" NutritionCategory.Lipid [||] |]

                    let evaluated =
                        { PlanContext.create "" OrderCategory.Drug (context [| scenario "o-lipid" |]) with
                            Intake = { Totals.empty with Energy = Some "10 kcal" }
                        }

                    match p |> OrderPlan.updateContext ruleSets "c-1" evaluated with
                    | Ok p ->
                        let c = p.Contexts[1]
                        c.Id |> Expect.equal "the plan's id" "c-1"
                        c.Category |> Expect.equal "the plan's category" (OrderCategory.Nutrition NutritionCategory.Lipid)
                        c.Intake.Energy |> Expect.equal "the intake as evaluated" (Some "10 kcal")
                        ids p |> Expect.equal "its order follows" [| "o-drug"; "o-lipid" |]
                    | Error e -> failtest $"{e}"

                    p
                    |> OrderPlan.updateContext ruleSets "c-9" evaluated
                    |> Expect.equal "no such context" (Error(OrderPlanError.NoSuchContext "c-9"))
                }

                test "a nutrition context's pick lists are narrowed to its category's rule set" {
                    let p = plan [| nutrition "c-t" NutritionCategory.TPN [||]; nutrition "c-f" NutritionCategory.EnteralFeeding [||] |]
                    let evaluated = PlanContext.create "" OrderCategory.Drug (context [||])

                    match p |> OrderPlan.updateContext ruleSets "c-t" evaluated with
                    | Ok p ->
                        let f = p.Contexts[0].Context.Filter
                        f.Indications |> Expect.equal "the set's indications" [| "voeding" |]
                        f.Generics |> Expect.equal "the set's generics" [| "glucose" |]
                        f.Diluents |> Expect.equal "diluents pass through" [| "NaCl 0.9%" |]
                        f.SelectedComponents |> Expect.equal "the selection passes through" [| "paracetamol" |]
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
                    p.Contexts |> Array.map _.Id |> Expect.equal "the drug and the tpn stay" [| "c-d"; "c-t" |]
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
                    added.Context.Filter.Generic |> Expect.equal "the workbench as it was" (Some "paracetamol")
                    added.Intake.Volume |> Expect.equal "with its intake" (Some "5 ml")
                    ids p |> Expect.equal "its order after the others" [| "o-drug"; "o-p" |]

                    p
                    |> OrderPlan.addOrderContext newId workbench
                    |> Expect.equal "the same order twice refused" (Error(OrderPlanError.OrderHeld "o-p"))

                    let wide =
                        { workbench with Context = context [| scenario "o-1"; scenario "o-2" |] }

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

                    plan [| feeding; nutrition "c-s" NutritionCategory.EnteralSupplement [||] |]
                    |> OrderPlan.admits NutritionCategory.EnteralSupplement
                    |> Expect.equal "a second supplement admitted" (Ok())

                    plan [| feeding |]
                    |> OrderPlan.admits NutritionCategory.TPN
                    |> Expect.equal "another category admitted" (Ok())

                    plan [| nutrition "c-e" NutritionCategory.ElectrolyteGlucose [||] |]
                    |> OrderPlan.admits NutritionCategory.ElectrolyteGlucose
                    |> Expect.equal "a second electrolyte line admitted" (Ok())

                    plan [| feeding |] |> OrderPlan.holds NutritionCategory.EnteralFeeding |> Expect.isTrue "held"
                    plan [| feeding |] |> OrderPlan.holds NutritionCategory.TPN |> Expect.isFalse "not held"
                }
            ]


OrderPlanTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||]
