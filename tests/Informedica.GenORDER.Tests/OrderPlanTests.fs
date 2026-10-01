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
        match
            Scenarios.pcmSupp
            |> Medication.toOrderDto Scenarios.testStart
            |> Order.Dto.fromDto
        with
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
            Argumentation = None
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
                        |> Medication.toOrderDto Scenarios.testStart
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
            Argumentation = None
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
        member _.Get(_: Resources.ResourceKey<'T>) : 'T = raise (System.NotImplementedException())

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

        // the departments answer, since the rule lookup reads the default from them
        member _.GetDepartments() = Resources.Departments.ofNamed []

        member _.GetResourceInfo() = raise (System.NotImplementedException())


/// A provider whose load failed: nothing registered, so every keyed read raises, the rules
/// read empty and the departments are the default alone.
type NotLoaded() =
    inherit NoRules()

    interface Resources.IResourceProvider with
        member _.Get(_: Resources.ResourceKey<'T>) : 'T = raise (System.Collections.Generic.KeyNotFoundException())


/// One dose rule built from a data row as the loader builds them, for the department it names:
/// paracetamol for a fever, once, rectally, on ICK. The row narrows to no form: the rule takes
/// its form from the one product attached, a suppository.
module RuleFixtures =

    let private emptyGeneric: GenericData =
        {
            Name = ""
            Form = ""
            Brand = ""
            GPKs = [||]
            HPKs = [||]
        }

    let private emptyLimit: DoseLimitData =
        {
            CmpBased = false
            Component = ""
            Substance = ""
            DoseUnit = ""
            MinQty = None
            MaxQty = None
            MinQtyAdj = None
            MaxQtyAdj = None
            MinPerTime = None
            MaxPerTime = None
            MinPerTimeAdj = None
            MaxPerTimeAdj = None
            MinRate = None
            MaxRate = None
            MinRateAdj = None
            MaxRateAdj = None
        }

    let private emptySchedule: ScheduleData =
        {
            DoseType = "once"
            DoseText = ""
            Freqs = [||]
            AdjustUnit = ""
            FreqUnit = ""
            RateUnit = ""
            MinTime = None
            MaxTime = None
            TimeUnit = ""
            MinInt = None
            MaxInt = None
            IntUnit = ""
            MinDur = None
            MaxDur = None
            DurUnit = ""
            DoseLimitData = emptyLimit
        }

    let private emptyCategory: PatientCategoryData =
        {
            Location = ""
            Dep = ""
            IsAdult = false
            Gender = AnyGender
            MinAge = None
            MaxAge = None
            MinWeight = None
            MaxWeight = None
            MinBSA = None
            MaxBSA = None
            MinGestAge = None
            MaxGestAge = None
            MinPMAge = None
            MaxPMAge = None
        }

    /// The row: the department in its patient category.
    let row (department: string) : DoseRuleData =
        {
            RowId = ""
            RuleId = ""
            GrpId = ""
            SortNo = 1
            Source = "FTK"
            SourceText = ""
            Generic = { emptyGeneric with Name = "paracetamol" }
            Indication = "koorts"
            Route = "RECTAAL"
            PatientText = ""
            Patient = { emptyCategory with Dep = department }
            ScheduleText = ""
            ScheduleData =
                { emptySchedule with
                    DoseLimitData =
                        { emptyLimit with
                            Component = "paracetamol"
                            Substance = "paracetamol"
                            DoseUnit = "mg"
                            MaxQty = Some(BigRational.FromInt 100)
                        }
                }
            Validated = None
            FreqCheck = None
            DoseCheck = None
        }

    /// The route mapping the rules are built with.
    let routeMapping: RouteMapping[] =
        [|
            {
                Long = "RECTAAL"
                Short = "rect"
            }
        |]

    /// The one product: a paracetamol suppository, given rectally.
    let private suppository = Product.create "paracetamol" "zetpil" "RECTAAL" [| "paracetamol" |]

    /// The dose rules the loader builds from the rows, with the suppository to attach.
    let rules (rows: DoseRuleData[]) =
        DoseRuleLoader.fromData routeMapping [||] [| suppository |] rows |> fst


/// A provider holding the dose rules given and the route mapping they are built with, naming
/// the default department.
type RulesOf(rules: DoseRule[]) =
    inherit NoRules()

    interface Resources.IResourceProvider with
        member _.GetDoseRules() = rules
        member _.GetRouteMappings() = RuleFixtures.routeMapping


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

            test "the rules match a patient without a department with the default, one with a department with its own" {
                let provider = NoRules()

                { EvaluateFixtures.child with Department = None }
                |> Api.withDefaultDepartment provider
                |> _.Department
                |> Expect.equal "the default" (Some Resources.Departments.defaultDepartment)

                { EvaluateFixtures.child with Department = Some "NEO" }
                |> Api.withDefaultDepartment provider
                |> _.Department
                |> Expect.equal "its own" (Some "NEO")
            }

            test "the context afresh keeps the patient's department: none stays none, and the lists are the rules'" {
                let fresh =
                    { EvaluateFixtures.child with Department = None }
                    |> OrderContext.create OrderLogging.noOp (NoRules())

                fresh.Patient.Department |> Expect.equal "still no department" None
                fresh.Filter.Forms |> Expect.isEmpty "no rules, no forms"
                fresh.Scenarios |> Expect.isEmpty "no scenarios"

                let own =
                    { EvaluateFixtures.child with Department = Some "NEO" }
                    |> OrderContext.create OrderLogging.noOp (NoRules())

                own.Patient.Department |> Expect.equal "its own department" (Some "NEO")
            }

            test
                "a rule for the default department is offered to a patient without one, and its form survives the reconcile" {
                let rules = RuleFixtures.rules [| RuleFixtures.row Resources.Departments.defaultDepartment |]
                rules |> Expect.isNonEmpty "the fixture builds a rule"

                let provider = RulesOf rules
                let patient = { EvaluateFixtures.child with Department = None }
                let fresh = patient |> OrderContext.create OrderLogging.noOp provider

                fresh.Filter.Forms |> Expect.equal "the rule's form is offered" [| "zetpil" |]
                fresh.Filter.Generics
                |> Expect.equal "the rule's generic is offered" [| "paracetamol" |]
                fresh.Patient.Department |> Expect.equal "the patient keeps no department" None

                let held =
                    { fresh with
                        Filter =
                            { fresh.Filter with
                                Form = Some "zetpil"
                                Generic = Some "paracetamol"
                            }
                    }

                let reconciled = held |> OrderContext.reconcile OrderLogging.noOp provider

                Filter.dropped held.Filter reconciled.Filter |> Expect.isFalse "no pick dropped"
                reconciled.Filter.Form |> Expect.equal "the form kept" (Some "zetpil")
                reconciled.Filter.Forms |> Expect.equal "the form alone" [| "zetpil" |]
            }

            test "a rule for another department is not offered to a patient without one" {
                let rules = RuleFixtures.rules [| RuleFixtures.row "NEO" |]
                let fresh =
                    { EvaluateFixtures.child with Department = None }
                    |> OrderContext.create OrderLogging.noOp (RulesOf rules)

                fresh.Filter.Forms |> Expect.isEmpty "not for the default department"
            }

            test "a provider whose load failed answers an empty context and keeps the patient's department" {
                let fresh =
                    { EvaluateFixtures.child with Department = None }
                    |> OrderContext.create OrderLogging.noOp (NotLoaded())

                fresh.Filter.Forms |> Expect.isEmpty "no forms"
                fresh.Patient.Department |> Expect.equal "still no department" None

                { EvaluateFixtures.child with Department = Some "NEO" }
                |> Api.withDefaultDepartment (NotLoaded())
                |> _.Department
                |> Expect.equal "its own department stands" (Some "NEO")
            }

            test "the rule lookup itself matches a patient without a department with the default" {
                let provider =
                    RulesOf(RuleFixtures.rules [| RuleFixtures.row Resources.Departments.defaultDepartment |])

                let rulesFor department =
                    { EvaluateFixtures.child with Department = department }
                    |> Api.getPrescriptionRules provider
                    |> Result.map (Array.map _.DoseRule.Generic)

                rulesFor None
                |> Result.defaultValue [||]
                |> Expect.isNonEmpty "the default's rule"

                rulesFor None
                |> Expect.equal
                    "the same rules as a patient of the default department"
                    (rulesFor (Some Resources.Departments.defaultDepartment))
            }

            test "a patient that is not set stays unset with the default: no rule is filtered on its department" {
                let rules = RuleFixtures.rules [| RuleFixtures.row "NEO" |]
                rules |> Expect.isNonEmpty "the fixture builds a rule"

                rules
                |> Api.filterDoseRules (RulesOf rules) Informedica.GenForm.Lib.Filter.doseFilter
                |> Array.length
                |> Expect.equal "every rule" rules.Length
            }

            test
                "the lists the rules give a context are among those of the context afresh, with or without a department" {
                let provider =
                    RulesOf(RuleFixtures.rules [| RuleFixtures.row Resources.Departments.defaultDepartment |])

                let within name (narrowed: 'a[]) (all: 'a[]) =
                    narrowed
                    |> Array.forall (fun x -> all |> Array.contains x)
                    |> Expect.isTrue $"%s{name} among those afresh"

                for department in [ None; Some Resources.Departments.defaultDepartment ] do
                    let fresh =
                        { EvaluateFixtures.child with Department = department }
                        |> OrderContext.create OrderLogging.noOp provider

                    fresh.Filter.Forms |> Expect.isNonEmpty "the rule's form afresh"

                    let ctx, _ = fresh |> OrderContext.getRules OrderLogging.noOp provider

                    ctx.Filter.Forms |> Expect.isNonEmpty "the rule's form for the context"
                    within "indications" ctx.Filter.Indications fresh.Filter.Indications
                    within "generics" ctx.Filter.Generics fresh.Filter.Generics
                    within "routes" ctx.Filter.Routes fresh.Filter.Routes
                    within "forms" ctx.Filter.Forms fresh.Filter.Forms
                    within "dose types" ctx.Filter.DoseTypes fresh.Filter.DoseTypes
            }

            test "made afresh, the context keeps its argumentation" {
                let held =
                    { EvaluateFixtures.pcmContext with
                        Patient = { EvaluateFixtures.child with Weight = None }
                        Argumentation = Some "Sepsis, hogere dosis in overleg met de apotheek"
                    }

                let ctx, _ = held |> OrderContext.getRules OrderLogging.noOp (NoRules())

                ctx.Scenarios |> Expect.isEmpty "afresh: no scenarios"
                ctx.Argumentation |> Expect.equal "the text kept" held.Argumentation
            }
        ]


/// An evaluation that finds no dose rule answers a refusal with the context as sent; the
/// message-list evaluate still answers the message.
let refusalTests =
    let start = System.DateTime(2026, 9, 28)
    let evaluateOutcome cmd =
        cmd |> OrderContext.evaluateOutcome start OrderLogging.noOp (NoRules())
    let evaluate cmd = cmd |> OrderContext.evaluate start OrderLogging.noOp (NoRules())

    testList
        "a refused evaluation"
        [
            testList
                "the picks"
                [
                    test "the field's own choice is the pick, over no patient" {
                        let picks = EvaluateFixtures.pcmContext |> OrderContext.picks

                        picks.Generic |> Expect.equal "chosen" (Some "Paracetamol")
                        picks.Indication |> Expect.equal "chosen" (Some "koorts")
                        picks.Patient |> Expect.equal "no patient" Patient.patient
                    }

                    test "one option offered is the pick, more are none" {
                        let fresh = { EvaluateFixtures.pcmContext with Filter = EvaluateFixtures.fresh }
                        (OrderContext.picks fresh).Route |> Expect.isNone "two routes, no pick"

                        let one = { fresh with Filter = { fresh.Filter with Routes = [| "or" |] } }

                        (OrderContext.picks one).Route |> Expect.equal "the one route" (Some "or")
                    }
                ]

            testList
                "the refusal"
                [
                    test "the three cases, read from the two rule sets" {
                        OrderContext.refusalOf [||] [||]
                        |> Expect.equal "none at all" Refusal.NoDoseRules

                        OrderContext.refusalOf [| 1 |] [||]
                        |> Expect.equal "none for the patient" Refusal.NoDoseRulesForPatient

                        OrderContext.refusalOf [| 1 |] [| 1 |]
                        |> Expect.equal "rules for the patient, dropped for their products" Refusal.NoProducts
                    }

                    test "no rules for the picks is the first case" {

                        EvaluateFixtures.pcmContext
                        |> OrderContext.refusal (NoRules())
                        |> Expect.equal "none from the provider" Refusal.NoDoseRules
                    }

                    test "a pick without rules is refused with the context as sent" {
                        let sent = EvaluateFixtures.pcmContext

                        match sent |> OrderContext.UpdateOrderContext |> evaluateOutcome with
                        | Ok(Refused(OrderContext.UpdateOrderContext ctx, Refusal.NoDoseRules)) ->
                            ctx.Filter.Generic |> Expect.equal "the pick kept" sent.Filter.Generic
                            ctx.Scenarios |> Expect.equal "the scenarios as sent" sent.Scenarios
                        | other -> failtest $"expected a refusal without rules, got %A{other}"
                    }

                    test "a form alone is a pick, refused without rules" {
                        let formOnly =
                            { EvaluateFixtures.pcmContext with
                                Filter = { EvaluateFixtures.fresh with Form = Some "tablet" }
                            }

                        match formOnly |> OrderContext.UpdateOrderContext |> evaluateOutcome with
                        | Ok(Refused(_, Refusal.NoDoseRules)) -> ()
                        | other -> failtest $"expected a refusal without rules, got %A{other}"
                    }

                    test "no pick at all is evaluated, not refused" {
                        let fresh = { EvaluateFixtures.pcmContext with Filter = EvaluateFixtures.fresh }

                        match fresh |> OrderContext.UpdateOrderContext |> evaluateOutcome with
                        | Ok(Evaluated(OrderContext.UpdateOrderContext _)) -> ()
                        | other -> failtest $"expected an evaluation, got %A{other}"
                    }

                    test "a scenario command is evaluated" {
                        match
                            EvaluateFixtures.pcmContext
                            |> OrderContext.SelectOrderScenario
                            |> evaluateOutcome
                        with
                        | Ok(Evaluated(OrderContext.SelectOrderScenario _)) -> ()
                        | other -> failtest $"expected an evaluation, got %A{other}"
                    }

                    test "the message-list evaluate still answers the message" {
                        EvaluateFixtures.pcmContext
                        |> OrderContext.UpdateOrderContext
                        |> evaluate
                        |> Expect.equal "the message" (Error [ ErrorMsg(OrderContext.noDoseRulesMessage, None) ])
                    }
                ]

            testList
                "the plan context as an outcome"
                [
                    let planContext = PlanContext.create "c-1" OrderCategory.Drug EvaluateFixtures.pcmContext

                    let evaluateOutcome cmd pc =
                        pc
                        |> PlanContext.evaluateOutcome start OrderLogging.noOp (NoRules()) (fun () -> [||]) cmd

                    test "a pick the rules no longer offer is gone from the reconciled filter" {
                        let held = EvaluateFixtures.pcmContext.Filter
                        let reconciled =
                            EvaluateFixtures.pcmContext
                            |> OrderContext.reconcile OrderLogging.noOp (NoRules())

                        reconciled.Filter.Generic |> Expect.isNone "dropped by the reconciliation"
                        Filter.dropped held reconciled.Filter |> Expect.isTrue "the generic gone"
                        Filter.dropped held held |> Expect.isFalse "kept"
                        Filter.dropped reconciled.Filter held
                        |> Expect.isFalse "nothing chosen, nothing gone"
                    }

                    test "a dropped pick is refused with the plan context as sent, id and category kept" {
                        match planContext |> evaluateOutcome OrderContext.UpdateOrderContext with
                        | Ok(Refused(pc, Refusal.NoDoseRules)) ->
                            pc.Id |> Expect.equal "the id" "c-1"
                            pc.Category |> Expect.equal "the category" OrderCategory.Drug

                            pc.Context.Filter.Generic
                            |> Expect.equal "the pick kept" EvaluateFixtures.pcmContext.Filter.Generic
                        | other -> failtest $"expected a refusal without rules, got %A{other}"
                    }

                    test "no pick is evaluated, and so is a scenario command over a dropped pick" {
                        let fresh =
                            { planContext with
                                Context = { EvaluateFixtures.pcmContext with Filter = EvaluateFixtures.fresh }
                            }

                        match fresh |> evaluateOutcome OrderContext.UpdateOrderContext with
                        | Ok(Evaluated pc) -> pc.Id |> Expect.equal "the id" "c-1"
                        | other -> failtest $"expected an evaluation, got %A{other}"

                        match planContext |> evaluateOutcome OrderContext.SelectOrderScenario with
                        | Ok(Evaluated _) -> ()
                        | other -> failtest $"expected an evaluation, got %A{other}"
                    }
                ]
        ]


/// The argumentation on the order context: kept through the Dto and through an evaluation,
/// none where a row from before the field holds nothing.
let argumentationTests =
    let text = "Sepsis, hogere dosis in overleg met de apotheek"

    let argued = { EvaluateFixtures.pcmContext with Argumentation = Some text }

    let roundTrip (ctx: OrderContext) = ctx |> OrderContext.Dto.toDto |> OrderContext.Dto.fromDto

    testList
        "the argumentation"
        [
            test "to the Dto and back keeps the text, and keeps none" {
                match roundTrip argued with
                | Ok ctx -> ctx.Argumentation |> Expect.equal "the text" (Some text)
                | Error e -> failtest $"the round trip failed: %A{e}"

                match roundTrip EvaluateFixtures.pcmContext with
                | Ok ctx -> ctx.Argumentation |> Expect.isNone "none stays none"
                | Error e -> failtest $"the round trip failed: %A{e}"
            }

            test "the JSON a version 1 row holds, without the field, reads as none" {
                // the canonical form as the release before the field wrote it: the field cut out
                let json =
                    (EvaluateFixtures.pcmContext |> OrderContext.Dto.toDto |> Canonical.serialize)
                        .Replace(",\"Argumentation\":null", "")

                json.Contains "Argumentation" |> Expect.isFalse "no such field in the row"

                match json |> Canonical.deserialize<OrderContext.Dto.Dto> |> OrderContext.Dto.fromDto with
                | Ok ctx -> ctx |> Expect.equal "the context, the field none" EvaluateFixtures.pcmContext
                | Error e -> failtest $"a version 1 context does not parse: %A{e}"
            }

            test "the new Dto's canonical JSON carries the field, null or the text" {
                let none = EvaluateFixtures.pcmContext |> OrderContext.Dto.toDto |> Canonical.serialize
                let some = argued |> OrderContext.Dto.toDto |> Canonical.serialize

                none.Contains "\"Argumentation\":null"
                |> Expect.isTrue "none is written as null"
                some.Contains $"\"Argumentation\":\"{text}\""
                |> Expect.isTrue "the text is written"
            }

            test "a plan context evaluated keeps its argumentation: every command copies the record" {
                let evaluate (cmd: OrderContext.Command) =
                    match cmd with
                    | OrderContext.SelectOrderScenario ctx ->
                        Ok(OrderContext.SelectOrderScenario { ctx with Scenarios = [| EvaluateFixtures.pcmScenario |] })
                    | other -> failtest $"the command as given, got {other}"

                let pc = PlanContext.create "c-1" OrderCategory.Drug argued

                match
                    pc
                    |> PlanContext.evaluateWith id evaluate (fun _ -> Totals.empty) OrderContext.SelectOrderScenario
                with
                | Ok pc -> pc.Context.Argumentation |> Expect.equal "the text survives" (Some text)
                | Error e -> failtest $"{e}"
            }
        ]
