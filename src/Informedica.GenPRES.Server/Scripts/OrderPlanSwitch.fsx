// Step 4.2 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #762):
// the order plan port on domain values. The port takes and answers the domain's order plan;
// the adapter behind it calls GenORDER's order plan rules with the nutrition rule sets the
// server owns and the provider's totals data; the command handler parses the contract model
// in two steps (`ofModel`, then `fromDto`, refusing on an error) and maps the answer out with
// the environment's demo flag. The refusals are the domain's `OrderPlanError`, worded here.
//
// Dosing: the nutrition generics offered. The last part of this script makes a nutrition
// workbench on the old port and on the new one for the same patient and compares what they
// offer.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. GenORDER OrderPlan.fs: `OrderPlan.recalculate`.
//   2. Ports.fs: `OrderPlanPort` as below.
//   3. Mappers.OrderPlan.fs: `OrderPlanError.words`.
//   4. Adapters.fs: `makeOrderPlanPort` over the domain rules; `evaluateModel` goes with it.
//   5. OrderPlanCommand.fs: `processCmd` as below.
//   6. Tests: `portPlan` becomes the domain value; the dispatch test's port on domain values;
//      the tests over `OrderPlanService` go in 4.3 with the service; new tests below.
//
// Run from this directory: dotnet fsi OrderPlanSwitch.fsx

#I __SOURCE_DIRECTORY__
#r "nuget: Expecto, 10.2.3"

#load "load.fsx"

open System
open Informedica.Utils.Lib
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib
// after the domain, so that the contract model's cases win unqualified
open Shared.Types
open ServerApi


// ---------------------------------------------------------------------------
// 1. GenORDER OrderPlan.fs: the totals over the plan's orders
// ---------------------------------------------------------------------------

module OrderPlan =

    open Informedica.GenOrder.Lib.OrderPlan


    /// The plan with its totals recomputed over the orders of the contexts the filter keeps,
    /// all of them when it is empty, for its patient's age and weight.
    let recalculate
        (totalsData: Types.Data.TotalsData[])
        (plan: Informedica.GenOrder.Lib.Types.OrderPlan)
        : Informedica.GenOrder.Lib.Types.OrderPlan
        =
        let wght =
            plan.Patient.Weight |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)

        { plan with
            Totals =
                plan
                |> filtered
                |> Array.choose PlanContext.contribution
                |> Array.map (_.Order >> Order.Dto.toDto)
                |> Totals.getTotals totalsData plan.Patient.Age wght
        }


// ---------------------------------------------------------------------------
// 2. Ports.fs: the port on domain values
// ---------------------------------------------------------------------------

/// The one plan: every member answers the plan with its totals recomputed over its orders.
/// The verb of a navigation is the wire's, mapped by the command handler; the plan and the
/// contexts are parsed there too, so the port never sees the contract model.
type OrderPlanPort =
    {
        recalculate: Informedica.GenOrder.Lib.Types.OrderPlan -> Async<Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]>>
        // the command into the context named
        navigate:
            Informedica.GenOrder.Lib.Types.OrderPlan
                -> string
                -> (Informedica.GenOrder.Lib.Types.OrderContext -> OrderContext.Command)
                -> PlanContext
                -> Async<Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]>>
        // a workbench evaluated elsewhere into the plan as it is
        addOrderContext:
            Informedica.GenOrder.Lib.Types.OrderPlan -> PlanContext -> Async<Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]>>
        // a fresh workbench for a nutrition category, its filter discovered
        newOrderContext:
            Informedica.GenOrder.Lib.Types.OrderPlan
                -> Informedica.GenOrder.Lib.Types.NutritionCategory
                -> Async<Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]>>
        // the contexts named, every kind; a feeding takes its supplements with it
        removeOrderContexts:
            Informedica.GenOrder.Lib.Types.OrderPlan -> string[] -> Async<Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]>>
        openWith: Informedica.GenForm.Lib.Types.Patient -> PlanContext[] -> Async<Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]>>
    }


// ---------------------------------------------------------------------------
// 3. Mappers.OrderPlan.fs: the words for a refused change
// ---------------------------------------------------------------------------

module OrderPlanError =

    /// The server's words for a change the plan refuses; the category's label from the rule
    /// sets the server owns.
    let words (ruleSets: NutritionRuleSet[]) =
        function
        | OrderPlanError.NoSuchContext id -> $"The plan holds no context %s{id}"
        | OrderPlanError.NotNarrowed n -> $"The workbench holds %i{n} candidates, not one order"
        | OrderPlanError.OrderHeld _ -> "The plan already holds this order"
        | OrderPlanError.SupplementNeedsFeeding -> "A supplement needs a feeding in the plan"
        | OrderPlanError.CategoryHeld category ->
            let label =
                ruleSets
                |> NutritionRuleSet.tryFind category
                |> Option.map _.Label
                |> Option.defaultValue $"%A{category}"

            $"The plan already holds a %s{label} context"


// ---------------------------------------------------------------------------
// 4. Adapters.fs: the port over the domain rules
// ---------------------------------------------------------------------------

module OrderPlanAdapter =

    let makeOrderPlanPort
        logger
        (provider: Resources.IResourceProvider)
        (ruleSets: NutritionRuleSet[])
        (newId: unit -> string)
        : OrderPlanPort
        =
        let recalc plan =
            plan |> OrderPlan.recalculate (provider.GetTotals())

        let refused r =
            r |> Result.mapError (OrderPlanError.words ruleSets >> Array.singleton)

        {
            recalculate = fun plan -> async { return plan |> recalc |> Ok }
            navigate =
                fun plan contextId cmd pc ->
                    async {
                        return
                            pc
                            |> OrderContextService.evaluate logger provider cmd
                            |> Result.bind (fun evaluated ->
                                plan
                                |> Informedica.GenOrder.Lib.OrderPlan.updateContext ruleSets contextId evaluated
                                |> refused
                            )
                            |> Result.map recalc
                    }
            addOrderContext =
                fun plan pc ->
                    async {
                        return
                            plan
                            |> Informedica.GenOrder.Lib.OrderPlan.addOrderContext newId pc
                            |> refused
                            |> Result.map recalc
                    }
            newOrderContext =
                fun plan category ->
                    async {
                        return
                            match plan |> Informedica.GenOrder.Lib.OrderPlan.admits category |> refused with
                            | Error e -> Error e
                            | Ok() ->
                                let set = ruleSets |> NutritionRuleSet.tryFind category

                                // the workbench: the patient, the category's indications and
                                // generics, nothing else offered yet and nothing selected; the
                                // discovery keeps of what the evaluation offers only these
                                let fresh =
                                    Informedica.GenOrder.Lib.OrderContext.create logger provider plan.Patient

                                let workbench =
                                    { fresh with
                                        Filter =
                                            { fresh.Filter with
                                                Indications = set |> Option.map _.Indications |> Option.defaultValue [||]
                                                Generics = set |> Option.map _.Generics |> Option.defaultValue [||]
                                                Routes = [||]
                                                Forms = [||]
                                                DoseTypes = [||]
                                            }
                                    }

                                let planContext = PlanContext.create (newId ()) (Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition category)

                                let evaluate ctx =
                                    ctx
                                    |> planContext
                                    |> OrderContextService.evaluate logger provider OrderContext.UpdateOrderContext
                                    |> Result.map _.Context

                                workbench
                                |> NutritionRuleSet.discover evaluate
                                |> Result.map (fun discovered ->
                                    let pc = discovered |> planContext

                                    { plan with
                                        Contexts =
                                            Array.append
                                                plan.Contexts
                                                [| { pc with Intake = discovered |> OrderContext.intake (provider.GetTotals()) } |]
                                    }
                                    |> recalc
                                )
                    }
            removeOrderContexts =
                fun plan ids ->
                    async { return plan |> Informedica.GenOrder.Lib.OrderPlan.removeOrderContexts ids |> recalc |> Ok }
            openWith = fun pat contexts -> async { return Informedica.GenOrder.Lib.OrderPlan.create pat contexts |> Ok }
        }


// ---------------------------------------------------------------------------
// 5. OrderPlanCommand.fs: the handler
// ---------------------------------------------------------------------------

module OrderPlanCommand =

    /// The contract model's plan parsed, or the reasons it is none in the server's words.
    let parsePlan (plan: OrderPlan) : Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]> =
        plan
        |> OrderPlanMapper.ofModel
        |> Informedica.GenOrder.Lib.OrderPlan.Dto.fromDto
        |> Result.mapError (List.map OrderContextMapper.words >> List.toArray)


    let parsePatient (pat: Patient) : Result<Informedica.GenForm.Lib.Types.Patient, string[]> =
        pat
        |> Patient.ofModel
        |> Informedica.GenForm.Lib.Patient.Dto.fromDto
        |> Result.mapError (List.map Patient.words >> List.toArray)


    let private answer demo (result: Async<Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]>>) =
        async {
            let! r = result
            return r |> Result.map (Informedica.GenOrder.Lib.OrderPlan.Dto.toDto >> OrderPlanMapper.toModel demo)
        }


    let private parsed (p: Result<'a, string[]>) (run: 'a -> Async<Result<Informedica.GenOrder.Lib.Types.OrderPlan, string[]>>) =
        match p with
        | Error e -> async { return Error e }
        | Ok v -> run v


    /// The plan's patient and that of every context it carries, and the context's where a
    /// command carries one, made at the inbound boundary; the plan and the context parsed into
    /// the domain, the verb mapped, the port asked, the answer mapped out with the environment's
    /// demo flag. A draft that is none, or a plan the domain does not read, is refused.
    let processCmd (demo: bool) (port: OrderPlanPort) (cmd: Shared.Api.OrderPlanCommand) =
        let both a b =
            match a, b with
            | Ok a, Ok b -> Ok(a, b)
            | Error e, _
            | _, Error e -> Error e

        match cmd with
        | Shared.Api.OrderPlanCommand.Recalculate plan ->
            Patient.overAll (Patient.ofPlan plan) (fun () -> parsed (parsePlan plan) port.recalculate |> answer demo)
        | Shared.Api.OrderPlanCommand.Navigate(plan, contextId, ctxCmd, ctx) ->
            Patient.overAll (Patient.ofPlan plan @ [ ctx.Patient ]) (fun () ->
                parsed (both (parsePlan plan) (OrderContextService.parse ctx)) (fun (p, pc) ->
                    port.navigate p contextId (OrderContextMapper.Command.toDomain ctxCmd) pc
                )
                |> answer demo
            )
        | Shared.Api.OrderPlanCommand.AddOrderContext(plan, ctx) ->
            Patient.overAll (Patient.ofPlan plan @ [ ctx.Patient ]) (fun () ->
                parsed (both (parsePlan plan) (OrderContextService.parse ctx)) (fun (p, pc) -> port.addOrderContext p pc)
                |> answer demo
            )
        | Shared.Api.OrderPlanCommand.NewOrderContext(plan, category) ->
            Patient.overAll (Patient.ofPlan plan) (fun () ->
                parsed (parsePlan plan) (fun p -> port.newOrderContext p (OrderPlanMapper.nutritionCategory category))
                |> answer demo
            )
        | Shared.Api.OrderPlanCommand.RemoveOrderContexts(plan, ids) ->
            Patient.overAll (Patient.ofPlan plan) (fun () ->
                parsed (parsePlan plan) (fun p -> port.removeOrderContexts p ids) |> answer demo
            )
        | Shared.Api.OrderPlanCommand.Open(pat, contexts) ->
            Patient.overAll (pat :: (contexts |> Array.map _.Patient |> Array.toList)) (fun () ->
                let parsedContexts =
                    contexts
                    |> Array.toList
                    |> List.map OrderContextService.parse
                    |> List.fold
                        (fun acc r ->
                            match acc, r with
                            | Ok xs, Ok x -> Ok(x :: xs)
                            | Error e, _
                            | _, Error e -> Error e
                        )
                        (Ok [])
                    |> Result.map (List.rev >> List.toArray)

                parsed (both (parsePatient pat) parsedContexts) (fun (p, cs) -> port.openWith p cs)
                |> answer demo
            )


// ---------------------------------------------------------------------------
// 6. Tests, for tests/Informedica.GenPRES.Server.Tests/OrderPlanSwitchTests.fs
// ---------------------------------------------------------------------------

module OrderPlanSwitchTests =

    open Expecto
    open Expecto.Flip
    open Shared.Types


    let patient = StubPatientData.patient

    let ctx: OrderContext =
        { Shared.Models.OrderContext.empty with
            Id = "c-1"
            Category = OrderCategory.Nutrition NutritionCategory.TPN
            Patient = patient
        }

    let plan: OrderPlan =
        { Shared.Models.OrderPlan.create patient [| ctx |] with Filtered = [| "c-1" |] }

    let answering (seen: string list ref) name (p: Informedica.GenOrder.Lib.Types.OrderPlan) =
        seen.Value <- name :: seen.Value
        async { return Ok p }

    let echoPort seen : OrderPlanPort =
        {
            recalculate = answering seen "recalculate"
            navigate = fun p _ _ _ -> answering seen "navigate" p
            addOrderContext = fun p _ -> answering seen "addOrderContext" p
            newOrderContext = fun p _ -> answering seen "newOrderContext" p
            removeOrderContexts = fun p _ -> answering seen "removeOrderContexts" p
            openWith = fun pat cs -> answering seen "openWith" (Informedica.GenOrder.Lib.OrderPlan.create pat cs)
        }


    let tests =
        testList
            "the order plan switch-over"
            [
                testAsync "every case reaches its port member, the answer the plan as the contract model" {
                    let seen = ref []
                    let port = echoPort seen
                    let run = OrderPlanCommand.processCmd true port

                    let! recalculated = run (Shared.Api.OrderPlanCommand.Recalculate plan)
                    recalculated |> Expect.equal "the plan back, demo as the environment says" (Ok plan)

                    let! _ = run (Shared.Api.OrderPlanCommand.Navigate(plan, "c-1", Shared.Api.OrderContextCommand.UpdateOrderContext, ctx))
                    let! _ = run (Shared.Api.OrderPlanCommand.AddOrderContext(plan, ctx))
                    let! _ = run (Shared.Api.OrderPlanCommand.NewOrderContext(plan, NutritionCategory.TPN))
                    let! _ = run (Shared.Api.OrderPlanCommand.RemoveOrderContexts(plan, [| "c-1" |]))
                    let! opened = run (Shared.Api.OrderPlanCommand.Open(patient, [| ctx |]))

                    seen.Value
                    |> List.rev
                    |> Expect.equal
                        "each to its port"
                        [ "recalculate"; "navigate"; "addOrderContext"; "newOrderContext"; "removeOrderContexts"; "openWith" ]

                    match opened with
                    | Ok p ->
                        p.OrderContexts |> Array.map _.Id |> Expect.equal "the contexts as given" [| "c-1" |]
                        p.Filtered |> Expect.isEmpty "nothing filtered on an open"
                    | Error e -> failtest $"open refused: %A{e}"
                }

                testAsync "the port receives the parsed plan, the verb and the plan context" {
                    let seen = ref None

                    let port =
                        { echoPort (ref []) with
                            navigate =
                                fun p id cmd pc ->
                                    seen.Value <- Some(p.Filtered, id, cmd pc.Context, pc.Category)
                                    async { return Ok p } }

                    let! _ =
                        OrderPlanCommand.processCmd false port (Shared.Api.OrderPlanCommand.Navigate(plan, "c-1", Shared.Api.OrderContextCommand.SelectOrderScenario, ctx))

                    match seen.Value with
                    | Some(filtered, "c-1", OrderContext.SelectOrderScenario _, category) ->
                        filtered |> Expect.equal "the plan parsed" [| "c-1" |]

                        category
                        |> Expect.equal "the context's category" (Informedica.GenOrder.Lib.Types.OrderCategory.Nutrition Informedica.GenOrder.Lib.Types.NutritionCategory.TPN)
                    | other -> failtest $"the port saw %A{other}"
                }

                testAsync "a draft plan is refused before the port with the server's words" {
                    let seen = ref []
                    let! refused = OrderPlanCommand.processCmd false (echoPort seen) (Shared.Api.OrderPlanCommand.Recalculate Shared.Models.OrderPlan.empty)
                    refused |> Expect.equal "no patient" (Error [| Patient.noPatient |])
                    seen.Value |> Expect.isEmpty "the port never asked"
                }

                test "the refusals in the server's words, the category labelled from the rule sets" {
                    let words = OrderPlanError.words NutritionRuleSets.all
                    OrderPlanError.SupplementNeedsFeeding |> words |> Expect.equal "supplement" "A supplement needs a feeding in the plan"

                    OrderPlanError.CategoryHeld Informedica.GenOrder.Lib.Types.NutritionCategory.EnteralFeeding
                    |> words
                    |> Expect.equal "category" "The plan already holds a Enterale Voeding context"

                    OrderPlanError.NotNarrowed 2 |> words |> Expect.equal "not narrowed" "The workbench holds 2 candidates, not one order"
                    OrderPlanError.OrderHeld "o" |> words |> Expect.equal "held" "The plan already holds this order"
                    OrderPlanError.NoSuchContext "c-9" |> words |> Expect.equal "no context" "The plan holds no context c-9"
                }
            ]


OrderPlanSwitchTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore


// ---------------------------------------------------------------------------
// 7. Against the demo data: a TPN workbench on the old port and on the new one, then a drug
//    order added, a navigation into the workbench, a removal, the totals.
// ---------------------------------------------------------------------------

Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "0")

let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId OrderLogging.noOp "1IZ3sbmrM4W4OuSYELRmCkdxpN9SlBI-5TLSvXWhHVmA"

let logger = OrderLogging.noOp
let ids = let n = ref 0 in fun () -> n.Value <- n.Value + 1; $"c-%i{n.Value}"
let port = OrderPlanAdapter.makeOrderPlanPort logger provider NutritionRuleSets.all ids
let demoPlan = Shared.Models.OrderPlan.create StubPatientData.patient [||]

// old port: the service over the contract model, the evaluation of step 4.1
let oldEvaluate cmd ctx = async { return OrderContextService.evaluateModel true logger provider cmd ctx }
let oldRecalc = OrderPlanService.recalculate (provider.GetTotals())

let oldTpn =
    OrderPlanService.newOrderContext oldRecalc oldEvaluate demoPlan NutritionCategory.TPN
    |> Async.RunSynchronously

let newTpn =
    OrderPlanCommand.processCmd true port (Shared.Api.OrderPlanCommand.NewOrderContext(demoPlan, NutritionCategory.TPN))
    |> Async.RunSynchronously

match oldTpn, newTpn with
| Ok o, Ok n ->
    let oc = o.OrderContexts[0]
    let nc = n.OrderContexts[0]
    printfn "TPN workbench old: %i indications, %i generics, category %A" oc.Filter.Indications.Length oc.Filter.Generics.Length oc.Category
    printfn "TPN workbench new: %i indications, %i generics, category %A, id %s" nc.Filter.Indications.Length nc.Filter.Generics.Length nc.Category nc.Id
    printfn "  same indications: %b, same generics: %b, same dose types: %b" (oc.Filter.Indications = nc.Filter.Indications) (oc.Filter.Generics = nc.Filter.Generics) (oc.Filter.DoseTypes = nc.Filter.DoseTypes)
    printfn "  new generics: %A" nc.Filter.Generics
| o, n -> printfn "old %A\nnew %A" (o |> Result.map (fun p -> p.OrderContexts.Length)) (n |> Result.map (fun p -> p.OrderContexts.Length))

// every category on a fresh plan: what the old port and the new offer
for category in [ NutritionCategory.EnteralFeeding; NutritionCategory.EnteralSupplement; NutritionCategory.TPN; NutritionCategory.Lipid; NutritionCategory.ElectrolyteGlucose ] do
    let feeding =
        // a supplement needs a feeding in the plan: make one first on both ports
        match category with
        | NutritionCategory.EnteralSupplement ->
            OrderPlanCommand.processCmd true port (Shared.Api.OrderPlanCommand.NewOrderContext(demoPlan, NutritionCategory.EnteralFeeding))
            |> Async.RunSynchronously
            |> Result.defaultValue demoPlan
        | _ -> demoPlan

    let o = OrderPlanService.newOrderContext oldRecalc oldEvaluate feeding category |> Async.RunSynchronously
    let n = OrderPlanCommand.processCmd true port (Shared.Api.OrderPlanCommand.NewOrderContext(feeding, category)) |> Async.RunSynchronously

    match o, n with
    | Ok o, Ok n ->
        let oc = o.OrderContexts |> Array.last
        let nc = n.OrderContexts |> Array.last

        printfn
            "%A: old %i indications %i generics %i dose types; new %i/%i/%i; same %b %b %b"
            category oc.Filter.Indications.Length oc.Filter.Generics.Length oc.Filter.DoseTypes.Length
            nc.Filter.Indications.Length nc.Filter.Generics.Length nc.Filter.DoseTypes.Length
            (oc.Filter.Indications = nc.Filter.Indications) (oc.Filter.Generics = nc.Filter.Generics) (oc.Filter.DoseTypes = nc.Filter.DoseTypes)
    | o, n -> printfn "%A: old %A new %A" category (o |> Result.map (fun p -> p.OrderContexts.Length)) (n |> Result.map (fun p -> p.OrderContexts.Length))

// a second TPN refused, a supplement without a feeding refused
match newTpn with
| Ok n ->
    OrderPlanCommand.processCmd true port (Shared.Api.OrderPlanCommand.NewOrderContext(n, NutritionCategory.TPN))
    |> Async.RunSynchronously
    |> Result.map (fun p -> p.OrderContexts.Length)
    |> printfn "second TPN: %A"

    OrderPlanCommand.processCmd true port (Shared.Api.OrderPlanCommand.NewOrderContext(n, NutritionCategory.EnteralSupplement))
    |> Async.RunSynchronously
    |> Result.map (fun p -> p.OrderContexts.Length)
    |> printfn "supplement without feeding: %A"

    // navigate into the TPN workbench: pick its first generic
    let tpn = n.OrderContexts[0]
    let chosen = { tpn with Filter = { tpn.Filter with Generic = tpn.Filter.Generics |> Array.tryHead } }

    OrderPlanCommand.processCmd true port (Shared.Api.OrderPlanCommand.Navigate(n, tpn.Id, Shared.Api.OrderContextCommand.UpdateOrderContext, chosen))
    |> Async.RunSynchronously
    |> Result.map (fun p -> let c = p.OrderContexts[0] in c.Id, c.Category, c.Filter.Generic, c.Filter.Indications.Length, c.Scenarios.Length)
    |> printfn "navigated: %A"

    OrderPlanCommand.processCmd true port (Shared.Api.OrderPlanCommand.RemoveOrderContexts(n, [| tpn.Id |]))
    |> Async.RunSynchronously
    |> Result.map (fun p -> p.OrderContexts.Length)
    |> printfn "removed: %A"
| Error e -> printfn "no TPN workbench: %A" e
