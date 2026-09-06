/// <summary>
/// Prototype MCP tool handlers for Informedica.GenORDER.Lib.
///
/// This script implements the GenORDER MCP tools described in:
///   docs/adr/0002-mcp-server-architecture.md
///
/// All tools are read-only. Navigation/mutation commands (increase/decrease dose)
/// are out of scope for Phase 1 and not implemented here.
///
/// Usage:
///   cd src/Informedica.GenORDER.Lib/Scripts
///   dotnet fsi McpTools.fsx
///
/// Or in FSI via MCP server:
///   #I "/path/to/src/Informedica.GenORDER.Lib/Scripts"
///   #load "McpTools.fsx";;
/// </summary>

#I __SOURCE_DIRECTORY__

open System

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore
Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "load.fsx"

open Informedica.Utils.Lib
open Informedica.GenForm.Lib
open Informedica.GenOrder.Lib

open Patient.Optics


// ── Provider setup ────────────────────────────────────────────────────────────

let provider: Resources.IResourceProvider =
    Api.getCachedProviderWithDataUrlId OrderLogging.noOp (Environment.GetEnvironmentVariable "GENPRES_URL_ID")


// ── Tool input types ──────────────────────────────────────────────────────────
// Each tool receives a strongly typed record parsed from the MCP JSON input.
// Field names match the JSON Schema in the ADR.

/// Input for the create_order_context tool.
/// Patient demographics used to seed the order context.
type CreateOrderContextInput =
    {
        // Patient age in months
        AgeMonths: float option
        // Body weight in kg
        WeightKg: float option
        // Body surface area in m²
        BsaM2: float option
        // Sex: "male" | "female" | "any"
        Sex: string option
        // Optional pre-filter fields
        Generic: string option
        Indication: string option
        Route: string option
        Form: string option
    }

/// Input for the get_order_context_filter_options tool.
/// Requires the serialised order context to work with.
type FilterOptionsInput =
    {
        Generic: string option
        Indication: string option
        Route: string option
        Form: string option
        AgeMonths: float option
        WeightKg: float option
    }

/// Input for the get_dose_rules_for_context tool.
type DoseRulesForContextInput =
    {
        Generic: string option
        Indication: string option
        Route: string option
        Form: string option
    }

/// Input for the get_solution_rules_for_context tool.
type SolutionRulesForContextInput =
    {
        Generic: string option
        Form: string option
        Route: string option
    }


// ── Tool output types ─────────────────────────────────────────────────────────

type FilterOptionsOutput =
    {
        Indications: string[]
        Generics: string[]
        Routes: string[]
        Forms: string[]
        DoseTypes: string[]
    }

type OrderScenarioOutput =
    {
        Number: int
        Name: string
        Indication: string
        Route: string
        Form: string
        DoseType: string
        HasRenalRule: bool
        Summary: string
    }

type OrderContextSummaryOutput =
    {
        PatientAgeMonths: float option
        PatientWeightKg: float option
        SelectedGeneric: string option
        SelectedRoute: string option
        SelectedForm: string option
        ScenarioCount: int
        SelectedScenario: string option
        FilterOptions: FilterOptionsOutput
    }


// ── Helper: build a minimal Patient ──────────────────────────────────────────

let private buildPatient (input: CreateOrderContextInput) : Patient.Patient =
    let pat = Patient.patient

    let pat =
        match input.AgeMonths with
        | Some a -> pat |> Patient.setAge [ Patient.Optics.Months(int a) ]
        | None -> pat

    let pat =
        match input.WeightKg with
        | Some w -> pat |> Patient.setWeight (decimal w |> Kilogram |> Some)
        | None -> pat

    // Default to ICK department so the order context can evaluate rules
    pat |> Patient.setDepartment (Some "ICK")


// ── Tool implementations ──────────────────────────────────────────────────────

/// MCP tool: get_order_context_filter_options
/// Returns the available filter options (generics, routes, indications, forms)
/// for the given patient context and current filter selection.
///
/// This is the primary discovery tool: an AI calls this first to learn what
/// medications and routes are available for a patient before asking for scenarios.
let getFilterOptions (input: FilterOptionsInput) : FilterOptionsOutput =
    let patient =
        buildPatient
            {
                AgeMonths = input.AgeMonths
                WeightKg = input.WeightKg
                BsaM2 = None
                Sex = None
                Generic = input.Generic
                Indication = input.Indication
                Route = input.Route
                Form = input.Form
            }

    let filter: DoseFilter =
        {
            Generic = input.Generic
            Indication = input.Indication
            Route = input.Route
            Form = input.Form
            DoseType = None
            Diluent = None
            Components = []
            Patient = patient
        }

    let inds = filter |> Filters.filterIndications OrderLogging.noOp provider
    let gens = filter |> Filters.filterGenerics OrderLogging.noOp provider
    let rtes = filter |> Filters.filterRoutes OrderLogging.noOp provider
    let frms = filter |> Filters.filterForms OrderLogging.noOp provider
    let dsts = filter |> Filters.filterDoseTypes OrderLogging.noOp provider

    {
        Indications = inds
        Generics = gens
        Routes = rtes
        Forms = frms
        DoseTypes = dsts |> Array.map DoseType.toString
    }


/// MCP tool: get_dose_rules_for_context
/// Returns dose rules matching a filter — a lightweight alternative to
/// creating a full order context when the AI only needs rule metadata.
let getDoseRulesForContext (input: DoseRulesForContextInput) =
    let filter: DoseFilter =
        {
            Generic = input.Generic
            Indication = input.Indication
            Route = input.Route
            Form = input.Form
            DoseType = None
            Diluent = None
            Components = []
            Patient = Patient.patient
        }

    OrderContext.getDoseRules provider filter
    |> Array.map (fun dr ->
        {|
            Generic = dr.Generic
            Indication = dr.Indication
            Route = dr.Route
            Form = dr.Form
            DoseType = dr.DoseType |> sprintf "%A"
            ComponentCount = dr.ComponentLimits |> Array.length
        |}
    )


/// MCP tool: get_solution_rules_for_context
/// Returns solution/preparation rules for a specific drug combination.
let getSolutionRulesForContext (input: SolutionRulesForContextInput) =
    OrderContext.getSolutionRules provider input.Generic input.Form input.Route
    |> Array.map (fun sr ->
        {|
            Generic = sr.Generic
            Form = sr.Form
            Route = sr.Route
            DiluentCount = sr.Diluents |> Array.length
            Diluents = sr.Diluents |> Array.map _.Generic
        |}
    )


/// MCP tool: create_order_context
/// Creates an order context for a patient and returns a summary of available
/// scenarios and the filter options in effect.
///
/// This is the main entry point for AI-assisted prescription support.
let createOrderContext (input: CreateOrderContextInput) : Result<OrderContextSummaryOutput, string> =
    let patient = buildPatient input
    let ctx = OrderContext.create OrderLogging.noOp provider patient

    // Apply any pre-filters from input
    let ctx =
        ctx
        |> (fun c ->
            match input.Generic with
            | Some g -> c |> OrderContext.setFilterGeneric g
            | None -> c
        )
        |> (fun c ->
            match input.Indication with
            | Some i -> c |> OrderContext.setFilterIndication i
            | None -> c
        )
        |> (fun c ->
            match input.Route with
            | Some r -> c |> OrderContext.setFilterRoute r
            | None -> c
        )
        |> (fun c ->
            match input.Form with
            | Some f -> c |> OrderContext.setFilterForm f
            | None -> c
        )

    match OrderContext.UpdateOrderContext ctx |> OrderContext.evaluate OrderLogging.noOp provider with
    | Error e -> Error $"Failed to evaluate order context: {e}"
    | Ok cmd ->
        let result = cmd |> OrderContext.Command.get
        let filterOpts =
            getFilterOptions
                {
                    Generic = result.Filter.Generic
                    Indication = result.Filter.Indication
                    Route = result.Filter.Route
                    Form = result.Filter.Form
                    AgeMonths = None
                    WeightKg = None
                }

        Ok
            {
                PatientAgeMonths = None
                PatientWeightKg = None
                SelectedGeneric = result.Filter.Generic
                SelectedRoute = result.Filter.Route
                SelectedForm = result.Filter.Form
                ScenarioCount = result.Scenarios |> Array.length
                SelectedScenario = result.Scenarios |> Array.tryExactlyOne |> Option.map (fun sc -> sc.Name)
                FilterOptions = filterOpts
            }


/// MCP tool: get_order_scenarios
/// Returns a summary of all available order scenarios for an order context
/// built from the supplied filter criteria.
let getOrderScenarios (input: CreateOrderContextInput) : OrderScenarioOutput[] =
    let patient = buildPatient input
    let ctx = OrderContext.create OrderLogging.noOp provider patient

    let ctx =
        ctx
        |> (fun c ->
            match input.Generic with
            | Some g -> c |> OrderContext.setFilterGeneric g
            | None -> c
        )
        |> (fun c ->
            match input.Route with
            | Some r -> c |> OrderContext.setFilterRoute r
            | None -> c
        )

    match OrderContext.UpdateOrderContext ctx |> OrderContext.evaluate OrderLogging.noOp provider with
    | Error _ -> [||]
    | Ok cmd ->
        let result = cmd |> OrderContext.Command.get

        result.Scenarios
        |> Array.mapi (fun i sc ->
            let sc = sc |> OrderScenario.setOrderTableFormat

            // Build a human-readable summary from the prescription table rows
            let summary =
                sc.Prescription
                |> Array.collect id
                |> Array.map (fun tb ->
                    match tb with
                    | Valid s | Caution s | Warning s | Alert s -> s
                )
                |> Array.filter (fun s -> s |> String.IsNullOrWhiteSpace |> not)
                |> String.concat " | "
                |> fun s -> if s.Length > 300 then s.[..299] else s

            {
                Number = i + 1
                Name = sc.Name
                Indication = sc.Indication
                Route = sc.Route
                Form = sc.Form
                DoseType = sc.DoseType |> DoseType.toString
                HasRenalRule = sc.UseRenalRule
                Summary = summary
            }
        )


// ── Smoke tests ───────────────────────────────────────────────────────────────

printfn "=== GenORDER MCP Tool Prototype ==="
printfn ""

// Test 1: filter options for a 10 kg, 24-month-old child
let filterOpts =
    getFilterOptions
        {
            Generic = None
            Indication = None
            Route = None
            Form = None
            AgeMonths = Some 24.0
            WeightKg = Some 10.0
        }

printfn $"Filter options for 10kg/24mo child:"
printfn $"  Generics ({filterOpts.Generics.Length}): {filterOpts.Generics |> Array.truncate 5 |> String.concat \", \"} ..."
printfn $"  Routes ({filterOpts.Routes.Length}): {filterOpts.Routes |> Array.truncate 5 |> String.concat \", \"} ..."

// Test 2: dose rules for paracetamol
let pcmRules =
    getDoseRulesForContext
        {
            Generic = Some "paracetamol"
            Indication = None
            Route = None
            Form = None
        }

printfn $""
printfn $"Paracetamol dose rules: {pcmRules.Length}"
pcmRules |> Array.truncate 3 |> Array.iter (fun r -> printfn $"  {r.Generic} | {r.Route} | {r.Form} | {r.DoseType}")

// Test 3: solution rules for morphine
let morphineSolutionRules =
    getSolutionRulesForContext
        {
            Generic = Some "morfine"
            Form = None
            Route = Some "intraveneus"
        }

printfn $""
printfn $"Morfine IV solution rules: {morphineSolutionRules.Length}"
morphineSolutionRules |> Array.truncate 3 |> Array.iter (fun sr -> printfn $"  {sr.Generic} | {sr.Route} | diluents: {sr.Diluents |> String.concat \", \"}")

printfn ""
printfn "=== Prototype complete ==="
