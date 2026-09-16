// Step 2.2 of docs/implementation-plans/725-contract-model-dto-domain-flow.md (issue #745):
// what the server does around an evaluation, as domain functions. A held selection is
// reconciled against fresh pick lists (the narrowing in the server's mapper, verbatim), the
// intake of a context is computed over its scenarios, a plan context is evaluated as one
// pipeline, and a nutrition workbench discovers its pick lists with the evaluation passed
// in. Dosing: reconcile and intake decide what the evaluation starts from and what a signed
// version records; both are moved, not changed.
//
// Prototype per the script-only policy in AGENTS.md. What migration touches:
//
//   1. Dtos.fs, module Filter: `Filter.reconcile`, before its Dto.
//   2. Api.fs, module OrderContext: `reconcile` and `intake`, after `evaluate`.
//   3. OrderPlan.fs: `PlanContext.evaluate` after the PlanContext rules;
//      `NutritionRuleSet.discover` after `narrow`.
//   4. tests/Informedica.GenORDER.Tests/OrderPlanTests.fs: the tests below.
//
// Run from this directory: dotnet fsi OrderPlanEvaluate.fsx

#I __SOURCE_DIRECTORY__

#r "nuget: Expecto"

#load "load.fsx"
// the provider of part 5 reaches the G-Standaard through ZForm, which load.fsx leaves out
#r "../../Informedica.ZForm.Lib/bin/Debug/net10.0/Informedica.ZForm.Lib.dll"

open System
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib


// ---------------------------------------------------------------------------
// 1. Dtos.fs, module Filter: a held selection against fresh pick lists
// ---------------------------------------------------------------------------

module Filter =

    /// The held selections reconciled against fresh pick lists: a selection still offered
    /// narrows its list to that one entry, in the fresh spelling; a selection no longer
    /// offered is dropped and its fresh list kept whole. The dose types are the fresh list
    /// when it has exactly one entry, the held dose type narrowed to it or not, and the
    /// held list otherwise, since the fresh list is computed once the generic is chosen.
    /// The diluents, the components and what is
    /// selected among them are the held ones: the pick lists for them come from the
    /// evaluation, not from the rules.
    let reconcile (fresh: Filter) (held: Filter) : Filter =
        let pick eqs itm (items: 'a[]) =
            match
                items
                |> Array.tryFind (fun x -> itm |> Option.map (eqs x) |> Option.defaultValue false)
            with
            | Some x -> itm, [| x |]
            | None -> None, items

        let ind, inds = fresh.Indications |> pick String.equalsCapInsens held.Indication
        let gen, gens = fresh.Generics |> pick String.equalsCapInsens held.Generic
        let rte, rtes = fresh.Routes |> pick String.equalsCapInsens held.Route
        let frm, frms = fresh.Forms |> pick String.equalsCapInsens held.Form
        let dtp, dtps = fresh.DoseTypes |> pick DoseType.eqs held.DoseType

        { fresh with
            Indication = ind
            Indications = inds
            Generic = gen
            Generics = gens
            Route = rte
            Routes = rtes
            Form = frm
            Forms = frms
            DoseType = dtp
            DoseTypes = if dtps |> Array.length = 1 then dtps else held.DoseTypes
            Diluents = held.Diluents
            Components = held.Components
            Diluent = held.Diluent
            SelectedComponents = held.SelectedComponents
        }


// ---------------------------------------------------------------------------
// 2. Api.fs, module OrderContext: reconcile and intake
// ---------------------------------------------------------------------------

module OrderContext =

    open Informedica.GenOrder.Lib.OrderContext


    /// The context as an evaluation starts from it: its patient as the rules take it, its
    /// filter reconciled against the pick lists the rules give that patient, its scenarios
    /// as held.
    let reconcile logger provider (ctx: OrderContext) : OrderContext =
        let fresh = create logger provider ctx.Patient

        { ctx with
            Patient = fresh.Patient
            Filter = Filter.reconcile fresh.Filter ctx.Filter
        }


    /// The totals over the orders of the context's scenarios, for its patient's age and
    /// weight.
    let intake (totalsData: Types.Data.TotalsData[]) (ctx: OrderContext) : Totals =
        let wght =
            ctx.Patient.Weight |> Option.map (ValueUnit.convertTo Units.Weight.kiloGram)

        ctx.Scenarios
        |> Array.map (_.Order >> Order.Dto.toDto)
        |> Totals.getTotals totalsData ctx.Patient.Age wght


// ---------------------------------------------------------------------------
// 3. OrderPlan.fs: the pipeline over a plan context, and discovery
// ---------------------------------------------------------------------------

module PlanContext =

    /// The plan context evaluated: its context reconciled, the command run over it, and
    /// its intake recorded over the answer. The id and the category stay the plan's. An
    /// evaluation that fails is the answer.
    let evaluate
        logger
        provider
        (totalsData: Types.Data.TotalsData[])
        (cmd: OrderContext -> OrderContext.Command)
        (pc: PlanContext)
        =
        pc.Context
        |> OrderContext.reconcile logger provider
        |> cmd
        |> OrderContext.evaluate logger provider
        |> Result.map (fun answer ->
            let ctx = answer |> OrderContext.Command.get

            { pc with
                Context = ctx
                Intake = ctx |> OrderContext.intake totalsData
            }
        )


module NutritionRuleSet =

    /// A nutrition workbench's pick lists discovered: the context evaluated as it is, and
    /// of the indications, generics and dose types the evaluation offers, only those the
    /// workbench already carried, so a category's rule set bounds what is offered. The
    /// evaluation is passed in, so the discovery is what it does with the answer.
    let discover (evaluate: OrderContext -> Result<OrderContext, 'e>) (ctx: OrderContext) =
        let offered (xs: 'a[]) (ys: 'a[]) =
            ys |> Array.filter (fun y -> xs |> Array.contains y)

        ctx
        |> evaluate
        |> Result.map (fun resolved ->
            { resolved with
                Filter =
                    { resolved.Filter with
                        Indications = resolved.Filter.Indications |> offered ctx.Filter.Indications
                        Generics = resolved.Filter.Generics |> offered ctx.Filter.Generics
                        DoseTypes = resolved.Filter.DoseTypes |> offered ctx.Filter.DoseTypes
                    }
            }
        )


// ---------------------------------------------------------------------------
// 4. Tests, for tests/Informedica.GenORDER.Tests/OrderPlanTests.fs
// ---------------------------------------------------------------------------

module EvaluateTests =

    open Expecto
    open Expecto.Flip


    module Fixtures =

        let order =
            match Scenarios.pcmSupp |> Medication.toOrderDto |> Order.Dto.fromDto with
            | Ok o -> o
            | Error e -> invalidOp $"fixture order could not be created: {e}"

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

        let scenario: OrderScenario =
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

        let context: OrderContext =
            {
                Filter = held
                Patient = child
                Scenarios = [| scenario |]
            }


    open Fixtures


    let tests =
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

                    let f = Filter.reconcile fresh { held with DoseType = Some once; DoseTypes = [| disc; once |] }
                    f.DoseType |> Expect.equal "once is offered" (Some once)
                    f.DoseTypes |> Expect.equal "narrowed to it" [| once |]

                    let cont = Informedica.GenForm.Lib.Types.Continuous "continu"
                    let f = Filter.reconcile { fresh with DoseTypes = [| once; cont |] } held
                    f.DoseType |> Expect.equal "the held dose type is not offered" None
                    f.DoseTypes |> Expect.equal "the held dose types stay" [| disc |]

                    let f = Filter.reconcile { fresh with DoseTypes = [| once |] } held
                    f.DoseType |> Expect.equal "not offered either" None
                    f.DoseTypes |> Expect.equal "a fresh list of one entry is taken as it is" [| once |]
                }

                test "diluents, components and the selection among them pass through as held" {
                    let f = Filter.reconcile fresh held
                    f.Diluents |> Expect.equal "diluents" [| "NaCl 0.9%" |]
                    f.Components |> Expect.equal "components" [| "paracetamol" |]
                    f.Diluent |> Expect.equal "diluent" (Some "NaCl 0.9%")
                    f.SelectedComponents |> Expect.equal "selected" [| "paracetamol" |]
                }

                test "nothing held: the fresh lists, nothing selected, the dose types as held" {
                    let f = Filter.reconcile fresh { fresh with Indications = [||]; DoseTypes = [||] }
                    f |> Expect.equal "the fresh filter, no dose types yet" { fresh with DoseTypes = [||] }
                }

                test "discovery keeps of what the evaluation offers only what the workbench carried" {
                    let workbench =
                        { context with
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
                        r.Filter.Generics |> Expect.equal "generics, in the evaluation's order" [| "paracetamol"; "glucose" |]
                        r.Filter.DoseTypes |> Expect.equal "dose types" [| disc |]
                        r.Filter.Routes |> Expect.equal "the rest as evaluated" [| "or" |]
                    | Error e -> failtest $"{e}"

                    workbench
                    |> NutritionRuleSet.discover (fun _ -> Error "no")
                    |> Expect.equal "an evaluation that fails is the answer" (Error "no")
                }

                test "no totals data, or no scenario: no intake" {
                    context |> OrderContext.intake [||] |> Expect.equal "no data" Totals.empty

                    { context with Scenarios = [||] }
                    |> OrderContext.intake [||]
                    |> Expect.equal "no scenario" Totals.empty
                }
            ]


EvaluateTests.tests |> Expecto.Tests.runTestsWithCLIArgs [] [||] |> ignore


// ---------------------------------------------------------------------------
// 5. Against the demo data: reconcile and the pipeline on a real provider. Run here, not
//    migrated: the GenORDER tests hold no provider. The acceptance of the switch-over
//    (plan 725, step 4.1) compares the same walkthrough before and after.
// ---------------------------------------------------------------------------

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "0")

let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId OrderLogging.noOp (Environment.GetEnvironmentVariable "GENPRES_URL_ID")

let logger = OrderLogging.noOp

let child = EvaluateTests.Fixtures.child

let held: OrderContext =
    {
        Filter =
            { EvaluateTests.Fixtures.fresh with
                Indications = [||]
                Generics = [||]
                Routes = [||]
                Forms = [||]
                DoseTypes = [||]
                Generic = Some "paracetamol"
                Route = Some "rectaal"
                Indication = Some "nonexistent"
            }
        Patient = child
        Scenarios = [||]
    }

let reconciled = held |> OrderContext.reconcile logger provider

printfn "reconciled: generic %A over %i generics, route %A over %i routes, indication %A over %i indications"
    reconciled.Filter.Generic reconciled.Filter.Generics.Length
    reconciled.Filter.Route reconciled.Filter.Routes.Length
    reconciled.Filter.Indication reconciled.Filter.Indications.Length

let pc = PlanContext.create "c-1" OrderCategory.Drug held
let totalsData = provider.GetTotals()

// the first pass narrows the pick lists to the generic and the route; the second, on the
// first indication offered, produces the scenarios and their intake
match pc |> PlanContext.evaluate logger provider totalsData OrderContext.UpdateOrderContext with
| Error e -> printfn "evaluation failed: %A" e
| Ok first ->
    printfn
        "first pass: %i scenarios, %i indications offered, %i routes"
        first.Context.Scenarios.Length
        first.Context.Filter.Indications.Length
        first.Context.Filter.Routes.Length

    let indication = first.Context.Filter.Indications |> Array.tryHead

    let second =
        { first with
            Context =
                { first.Context with
                    Filter = { first.Context.Filter with Indication = indication }
                }
        }
        |> PlanContext.evaluate logger provider totalsData OrderContext.UpdateOrderContext

    match second with
    | Error e -> printfn "second pass failed: %A" e
    | Ok pc ->
        printfn
            "second pass on %A: %i scenarios, intake volume %A, energy %A, id %s"
            indication
            pc.Context.Scenarios.Length
            pc.Intake.Volume
            pc.Intake.Energy
            pc.Id
