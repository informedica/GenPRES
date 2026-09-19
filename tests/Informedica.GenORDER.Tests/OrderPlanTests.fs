/// The rules of the one order plan on the domain types: what its contexts contribute,
/// which the row filter keeps, what it admits, and what leaves with what.
module OrderPlanTests

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
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

            test "the totals are recomputed over the orders of the contexts the filter keeps, for the patient's weight" {
                // a continuous infusion solved to its bounds: an order with a volume
                let infusion =
                    match
                        Scenarios.morfCont
                        |> Medication.toOrderDto
                        |> Order.Dto.fromDto
                        |> Result.mapError string
                        |> Result.bind (fun o ->
                            OrderProcessor.processPipeline OrderLogging.noOp (CalcMinMax o)
                            |> Result.mapError (fun (_, e) -> $"%A{e}")
                        )
                    with
                    | Ok o -> { scenario "o-inf" with Order = { o with Id = Id "o-inf" } }
                    | Error e -> failtest $"fixture order could not be solved: %s{e}"

                let volume: Informedica.GenForm.Lib.Types.Data.TotalsData =
                    {
                        Name = "volume"
                        MinAge = None
                        MaxAge = None
                        MinWeight = None
                        MaxWeight = None
                        Unit = Some Units.Volume.milliLiter
                        Adj = None
                        TimeUnit = Some Units.Time.day
                        MinPerTime = None
                        MaxPerTime = None
                        MinPerTimeAdj = None
                        MaxPerTimeAdj = None
                    }

                let child = { Patient.patient with Weight = Some(ValueUnit.singleWithUnit Units.Weight.kiloGram 32N) }

                let p =
                    { OrderPlan.create child [| drug "c-supp" "o-supp" |] with
                        Contexts =
                            [|
                                drug "c-supp" "o-supp"
                                PlanContext.create "c-inf" OrderCategory.Drug (context [| infusion |])
                            |]
                    }

                let all = p |> OrderPlan.recalculate [| volume |]
                all.Totals.Volume |> Expect.isSome "the infusion has a volume"
                all.Totals.Energy |> Expect.isNone "no data for energy"
                { all with Totals = p.Totals } |> Expect.equal "nothing else changes" p

                { p with Filtered = [| "c-supp" |] }
                |> OrderPlan.recalculate [| volume |]
                |> _.Totals
                |> Expect.equal "the suppository alone has no volume" Totals.empty

                { p with Patient = Patient.patient }
                |> OrderPlan.recalculate [| volume |]
                |> _.Totals
                |> Expect.equal "no weight, no totals" Totals.empty
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
                let p = plan [| drug "c-d" "o-drug"; nutrition "c-1" NutritionCategory.Lipid [||] |]

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

                plan [| feeding; nutrition "c-s" NutritionCategory.EnteralSupplement [||] |]
                |> OrderPlan.admits NutritionCategory.EnteralSupplement
                |> Expect.equal "a second supplement admitted" (Ok())

                plan [| feeding |]
                |> OrderPlan.admits NutritionCategory.TPN
                |> Expect.equal "another category admitted" (Ok())

                plan [| nutrition "c-e" NutritionCategory.ElectrolyteGlucose [||] |]
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


/// The fixtures of the evaluation: fresh pick lists, a held selection, a context on them.
module EvaluateFixtures =

    let order = Fixtures.order

    let disc = Informedica.GenForm.Lib.Types.Discontinuous "3-4 x/dag"
    let once = Informedica.GenForm.Lib.Types.Once "eenmalig"

    let fresh: Filter =
        {
            Indications = [| "koorts"; "pijn" |]
            Generics = [| "paracetamol"; "ibuprofen" |]
            Routes = [| "or"; "rect" |]
            Forms = [| "tablet"; "zetpil" |]
            DoseTypes = [| disc; once |]
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

    let held: Filter =
        { fresh with
            Indications = [| "stale" |]
            Generics = [| "Paracetamol" |]
            DoseTypes = [| disc |]
            Indication = Some "koorts"
            Generic = Some "Paracetamol"
            Route = Some "iv"
            Form = None
            DoseType = Some disc
            Diluents = [| "NaCl 0.9%" |]
            Components = [| "paracetamol" |]
            Diluent = Some "NaCl 0.9%"
            SelectedComponents = [| "paracetamol" |]
        }

    let child =
        { Patient.patient with
            Department = Some "ICK"
            Gender = Male
            Age = Some(ValueUnit.singleWithUnit Units.Time.day 3650N)
            Weight = Some(ValueUnit.singleWithUnit Units.Weight.kiloGram 32N)
            Height = Some(ValueUnit.singleWithUnit Units.Height.centiMeter 140N)
        }

    let pcmScenario: OrderScenario =
        {
            No = 1
            Name = "paracetamol"
            Indication = "koorts"
            Form = "zetpil"
            Route = "rect"
            DoseType = disc
            Diluent = None
            Component = Some "paracetamol"
            Item = Some "paracetamol"
            Diluents = [||]
            Components = [| "paracetamol" |]
            Items = [| "paracetamol" |]
            Prescription = [||]
            Preparation = [||]
            Administration = [||]
            Order = order
            UseAdjust = true
            UseRenalRule = false
            RenalRule = None
            ProductsIds = [||]
        }

    let pcmContext: OrderContext =
        {
            Filter = held
            Patient = child
            Scenarios = [| pcmScenario |]
        }


open EvaluateFixtures


let evaluateTests =
    testList
        "evaluation"
        [
            test "a selection still offered narrows its list to it, in the fresh spelling" {
                let f = Filter.reconcile fresh held
                f.Indication |> Expect.equal "kept" (Some "koorts")
                f.Indications |> Expect.equal "narrowed" [| "koorts" |]
                f.Generic |> Expect.equal "kept as held" (Some "Paracetamol")
                f.Generics |> Expect.equal "the fresh spelling" [| "paracetamol" |]
                f.DoseType |> Expect.equal "kept" (Some disc)
                f.DoseTypes |> Expect.equal "narrowed" [| disc |]
            }

            test "a selection no longer offered is dropped and the fresh list kept whole" {
                let f = Filter.reconcile fresh held
                f.Route |> Expect.equal "dropped" None
                f.Routes |> Expect.equal "the fresh routes" [| "or"; "rect" |]
                f.Form |> Expect.equal "none held, none picked" None
                f.Forms |> Expect.equal "the fresh forms" [| "tablet"; "zetpil" |]

                let f =
                    Filter.reconcile
                        fresh
                        { held with
                            DoseType = Some once
                            DoseTypes = [| disc; once |]
                        }

                f.DoseType |> Expect.equal "once is offered" (Some once)
                f.DoseTypes |> Expect.equal "narrowed to it" [| once |]

                let cont = Informedica.GenForm.Lib.Types.Continuous "continu"
                let f = Filter.reconcile { fresh with DoseTypes = [| once; cont |] } held
                f.DoseType |> Expect.equal "the held dose type is not offered" None
                f.DoseTypes |> Expect.equal "the held dose types stay" [| disc |]

                let f = Filter.reconcile { fresh with DoseTypes = [| once |] } held
                f.DoseType |> Expect.equal "not offered either" None

                f.DoseTypes
                |> Expect.equal "a fresh list of one entry is taken as it is" [| once |]
            }

            test "diluents, components and the selection among them pass through as held" {
                let f = Filter.reconcile fresh held
                f.Diluents |> Expect.equal "diluents" [| "NaCl 0.9%" |]
                f.Components |> Expect.equal "components" [| "paracetamol" |]
                f.Diluent |> Expect.equal "diluent" (Some "NaCl 0.9%")
                f.SelectedComponents |> Expect.equal "selected" [| "paracetamol" |]
            }

            test "nothing held: the fresh lists, nothing selected, the dose types as held" {
                let f =
                    Filter.reconcile
                        fresh
                        { fresh with
                            Indications = [||]
                            DoseTypes = [||]
                        }

                f
                |> Expect.equal "the fresh filter, no dose types yet" { fresh with DoseTypes = [||] }
            }

            test "discovery keeps of what the evaluation offers only what the workbench carried" {
                let workbench =
                    { pcmContext with
                        Filter =
                            { fresh with
                                Indications = [| "voeding"; "koorts" |]
                                Generics = [| "glucose"; "paracetamol" |]
                                DoseTypes = [| disc |]
                            }
                    }

                let evaluate (ctx: OrderContext) =
                    Ok
                        { ctx with
                            Filter =
                                { ctx.Filter with
                                    Indications = [| "koorts"; "pijn" |]
                                    Generics = [| "paracetamol"; "ibuprofen"; "glucose" |]
                                    DoseTypes = [| disc; once |]
                                    Routes = [| "or" |]
                                }
                        }

                match workbench |> NutritionRuleSet.discover evaluate with
                | Ok r ->
                    r.Filter.Indications |> Expect.equal "indications" [| "koorts" |]

                    r.Filter.Generics
                    |> Expect.equal "generics, in the evaluation's order" [| "paracetamol"; "glucose" |]

                    r.Filter.DoseTypes |> Expect.equal "dose types" [| disc |]
                    r.Filter.Routes |> Expect.equal "the rest as evaluated" [| "or" |]
                | Error e -> failtest $"{e}"

                workbench
                |> NutritionRuleSet.discover (fun _ -> Error "no")
                |> Expect.equal "an evaluation that fails is the answer" (Error "no")
            }

            test "a plan context is evaluated in order: reconciled, the command, the intake on the answer" {
                let seen = ResizeArray<string>()

                let reconcile (ctx: OrderContext) =
                    seen.Add "reconcile"
                    { ctx with Filter = { ctx.Filter with Generic = Some "reconciled" } }

                let evaluate (cmd: OrderContext.Command) =
                    seen.Add "evaluate"

                    match cmd with
                    | OrderContext.SelectOrderScenario ctx ->
                        ctx.Filter.Generic
                        |> Expect.equal "the command carries the reconciled context" (Some "reconciled")

                        Ok(OrderContext.SelectOrderScenario { ctx with Scenarios = [| pcmScenario; pcmScenario |] })
                    | other -> failtest $"the command as given, got {other}"

                let intake (ctx: OrderContext) =
                    seen.Add "intake"
                    { Totals.empty with Volume = Some $"%i{ctx.Scenarios.Length} scenarios" }

                let pc =
                    { PlanContext.create "c-1" (OrderCategory.Nutrition NutritionCategory.TPN) pcmContext with
                        Intake = { Totals.empty with Energy = Some "stale" }
                    }

                match
                    pc
                    |> PlanContext.evaluateWith reconcile evaluate intake OrderContext.SelectOrderScenario
                with
                | Ok pc ->
                    seen
                    |> List.ofSeq
                    |> Expect.equal "in order" [ "reconcile"; "evaluate"; "intake" ]

                    pc.Id |> Expect.equal "the plan's id" "c-1"

                    pc.Category
                    |> Expect.equal "the plan's category" (OrderCategory.Nutrition NutritionCategory.TPN)

                    pc.Context.Scenarios.Length |> Expect.equal "the answer's context" 2
                    pc.Context.Filter.Generic |> Expect.equal "as reconciled" (Some "reconciled")

                    pc.Intake.Volume
                    |> Expect.equal "the intake over the answer" (Some "2 scenarios")

                    pc.Intake.Energy |> Expect.equal "the stale intake gone" None
                | Error e -> failtest $"{e}"
            }

            test "an evaluation that fails is the answer, and no intake is computed" {
                let intake _ = failtest "no intake on a failed evaluation"

                let pc = PlanContext.create "c-1" OrderCategory.Drug pcmContext

                pc
                |> PlanContext.evaluateWith id (fun _ -> Error [ "no" ]) intake OrderContext.UpdateOrderContext
                |> Expect.equal "the failure" (Error [ "no" ])
            }

            test "no totals data, or no scenario: no intake" {
                pcmContext |> OrderContext.intake [||] |> Expect.equal "no data" Totals.empty

                { pcmContext with Scenarios = [||] }
                |> OrderContext.intake [||]
                |> Expect.equal "no scenario" Totals.empty
            }
        ]


/// A provider holding no rules: what the rule lookup reads answers empty, every other resource
/// raises, so a test sees which branch the lookup takes and nothing else.
type NoRules() =
    interface Resources.IResourceProvider with
        member _.Get _ = raise (System.NotImplementedException())

        member _.GetData() = raise (System.NotImplementedException())

        member _.GetUnitMappings() = raise (System.NotImplementedException())

        member _.GetRouteMappings() = [||]

        member _.GetValidForms() = raise (System.NotImplementedException())

        member _.GetFormRoutes() = raise (System.NotImplementedException())

        member _.GetFormularyProducts() = raise (System.NotImplementedException())

        member _.GetReconstitution() = raise (System.NotImplementedException())

        member _.GetParenteralMeds() = raise (System.NotImplementedException())

        member _.GetEnteralFeeding() = raise (System.NotImplementedException())

        member _.GetProducts() = raise (System.NotImplementedException())

        member _.GetDoseRules() = [||]
        member _.GetSolutionRules() = [||]
        member _.GetRenalRules() = [||]

        member _.GetTotals() = raise (System.NotImplementedException())

        member _.GetGStandProvider() = raise (System.NotImplementedException())

        member _.GetResourceInfo() = raise (System.NotImplementedException())


/// The rules for a patient without a department: the lookup runs on the context as held,
/// where it used to answer a context made afresh and no rules.
let rulesTests =
    testList
        "the rules for a patient without a department"
        [
            test "a patient without a department keeps its context: the selection, the components and the scenarios" {
                let held =
                    { EvaluateFixtures.pcmContext with Patient = { EvaluateFixtures.child with Department = None } }

                let ctx, rules = held |> OrderContext.getRules OrderLogging.noOp (NoRules())

                rules |> Expect.equal "no rules to find" (Ok [||])
                ctx.Scenarios |> Expect.equal "the scenarios as held" held.Scenarios

                ctx.Filter.SelectedComponents
                |> Expect.equal "the selection as held" held.Filter.SelectedComponents

                ctx.Filter.Diluents |> Expect.equal "the diluents as held" held.Filter.Diluents
                ctx.Patient.Department |> Expect.equal "still no department" None
            }

            test "a patient with a department is treated the same" {
                let held =
                    { EvaluateFixtures.pcmContext with
                        Patient = { EvaluateFixtures.child with Department = Some "ICK" }
                    }

                let ctx, rules = held |> OrderContext.getRules OrderLogging.noOp (NoRules())

                rules |> Expect.equal "no rules to find" (Ok [||])
                ctx.Scenarios |> Expect.equal "the scenarios as held" held.Scenarios
            }

            test "without a weight and a height the context is made afresh" {
                let held = { EvaluateFixtures.pcmContext with Patient = { EvaluateFixtures.child with Weight = None } }

                let ctx, rules = held |> OrderContext.getRules OrderLogging.noOp (NoRules())

                rules |> Expect.equal "no rules" (Ok [||])
                ctx.Scenarios |> Expect.isEmpty "afresh: no scenarios"
            }
        ]
