/// <summary>
/// Prototype MCP tool handlers for Informedica.GenFORM.Lib.
///
/// This script implements the GenFORM MCP tools described in:
///   docs/adr/0009-mcp-server-architecture.md
///
/// All tools are read-only and delegate directly to GenForm.Api functions
/// via IResourceProvider. No domain logic lives here.
///
/// Usage:
///   cd src/Informedica.GenFORM.Lib/Scripts
///   dotnet fsi McpTools.fsx
///
/// Or in FSI via MCP server:
///   #I "/path/to/src/Informedica.GenFORM.Lib/Scripts"
///   #load "McpTools.fsx";;
/// </summary>

#I __SOURCE_DIRECTORY__
#load "load.fsx"

#r "../bin/Debug/net10.0/Informedica.GenForm.Lib.dll"

open System
open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources
open Informedica.GenForm.Lib.Types


// ── Provider setup ──────────────────────────────────────────────────────────

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore

let dataUrlId = Environment.GetEnvironmentVariable "GENPRES_URL_ID"

let provider: IResourceProvider =
    Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId


// ── Tool input types ─────────────────────────────────────────────────────────
// Each tool receives a strongly typed record parsed from the MCP JSON input.
// Field names match the JSON Schema in the ADR.

/// Input for the filter_dose_rules tool.
type FilterDoseRulesInput =
    {
        // Optional filter fields — None means "no filter on this dimension"
        Generic: string option
        Route: string option
        Form: string option
        Indication: string option
        // Patient age range in months
        MinAge: float option
        MaxAge: float option
        // Body weight range in kg
        MinWeight: float option
        MaxWeight: float option
    }

/// Input for the get_prescription_rules tool.
type GetPrescriptionRulesInput =
    {
        Generic: string option
        Route: string option
        Form: string option
        Indication: string option
        MinAge: float option
        MaxAge: float option
        MinWeight: float option
        MaxWeight: float option
    }

/// Input for the get_solution_rules_for_context tool.
type FilterSolutionRulesInput =
    {
        Generic: string option
        Form: string option
        Route: string option
    }


// ── Tool output types ────────────────────────────────────────────────────────
// Outputs are simple records that serialise cleanly to JSON.

type DoseRuleOutput =
    {
        Generic: string
        Indication: string
        Route: string
        Form: string
        DoseType: string
        MinAge: float option
        MaxAge: float option
        MinWeight: float option
        MaxWeight: float option
        ComponentCount: int
    }

type SolutionRuleOutput =
    {
        Generic: string
        Form: string option
        Route: string
        Solutions: string[]
        MaxConcentration: float option
        MinConcentration: float option
    }

type RenalRuleOutput =
    {
        Generic: string
        Route: string
        AdjustmentFactor: float option
        Comment: string
    }

type ResourceInfoOutput =
    {
        IsLoaded: bool
        LastUpdated: string
        MessageCount: int
    }


// ── Mapping helpers ──────────────────────────────────────────────────────────

let private doseRuleToOutput (dr: DoseRule) : DoseRuleOutput =
    // Extract a simple float value from a Limit option (for readability in MCP responses)
    let limitToFloat limit =
        limit
        |> Option.bind (fun l ->
            match l with
            | Informedica.GenCore.Lib.Ranges.Limit.Inclusive vu
            | Informedica.GenCore.Lib.Ranges.Limit.Exclusive vu ->
                vu
                |> Informedica.GenUnits.Lib.ValueUnit.getValue
                |> Array.tryHead
                |> Option.map Informedica.Utils.Lib.BCL.BigRational.toDouble
        )

    {
        Generic = dr.Generic
        Indication = dr.Indication
        Route = dr.Route
        Form = dr.Form
        DoseType = dr.DoseType |> DoseType.toString
        MinAge = dr.PatientCategory.Age.Min |> limitToFloat
        MaxAge = dr.PatientCategory.Age.Max |> limitToFloat
        MinWeight = dr.PatientCategory.Weight.Min |> limitToFloat
        MaxWeight = dr.PatientCategory.Weight.Max |> limitToFloat
        ComponentCount = dr.ComponentLimits |> Array.length
    }

let private solutionRuleToOutput (sr: SolutionRule) : SolutionRuleOutput =
    {
        Generic = sr.Generic
        Form = sr.Form
        Route = sr.Route
        Solutions = sr.Diluents |> Array.map _.Generic
        MaxConcentration = None // concentration limits vary by solution rule subtype
        MinConcentration = None
    }

let private renalRuleToOutput (rr: RenalRule) : RenalRuleOutput =
    {
        Generic = rr.Generic
        Route = rr.Route
        AdjustmentFactor = None // renal adjustment factor is a range — summarised here
        Comment = rr |> sprintf "%A" |> fun s -> s.[..min 200 (s.Length - 1)]
    }


// ── Tool implementations ─────────────────────────────────────────────────────

/// MCP tool: get_resource_info
/// Returns cache status: loaded flag, last-updated timestamp, message count.
let getResourceInfo () : ResourceInfoOutput =
    let info = provider.GetResourceInfo()
    {
        IsLoaded = info.IsLoaded
        LastUpdated = info.LastUpdated.ToString "O"
        MessageCount = info.Messages |> Array.length
    }


/// MCP tool: get_dose_rules
/// Returns all dose rules without any filtering.
let getDoseRules () : DoseRuleOutput[] =
    provider
    |> Api.getDoseRules
    |> Array.map doseRuleToOutput


/// MCP tool: filter_dose_rules
/// Returns dose rules matching the supplied filter criteria.
/// All filter fields are optional; omitting a field matches all values.
let filterDoseRules (input: FilterDoseRulesInput) : DoseRuleOutput[] =
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

    let allRules = provider |> Api.getDoseRules
    provider
    |> Api.filterDoseRules <| filter <| allRules
    |> Array.map doseRuleToOutput


/// MCP tool: get_solution_rules
/// Returns all solution (preparation) rules without filtering.
let getSolutionRules () : SolutionRuleOutput[] =
    provider
    |> Api.getSolutionRules
    |> Array.map solutionRuleToOutput


/// MCP tool: get_renal_rules
/// Returns all renal adjustment rules.
let getRenalRules () : RenalRuleOutput[] =
    provider
    |> Api.getRenalRules
    |> Array.map renalRuleToOutput


/// MCP tool: get_prescription_rules
/// Returns prescription rules matching an optional filter.
let getPrescriptionRules (input: GetPrescriptionRulesInput) : string =
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

    match provider |> Api.filterPrescriptionRules <| filter with
    | Ok rules -> $"Found {rules |> Array.length} prescription rules"
    | Error msgs ->
        msgs
        |> List.map (fun m -> m |> sprintf "%A")
        |> String.concat "; "
        |> fun s -> $"Error: {s}"


/// MCP tool: get_formulary
/// Returns the list of formulary products (generic name + available forms).
let getFormulary () =
    provider.GetFormularyProducts()
    |> Array.map (fun p ->
        {|
            Generic = p.Generic
            Form = p.Form
            Brand = p.Brand
            Departments = p.Departments
        |}
    )


/// MCP tool: get_parenteral_meds
/// Returns parenteral medications available for IV preparation.
let getParenteralMeds () =
    provider.GetParenteralMeds()
    |> Array.map (fun p ->
        {|
            Generic = p.Generic
            Form = p.Form
            Label = p.Label
            SubstanceName = p.Substances |> Array.tryHead |> Option.map _.Name
        |}
    )


// ── Smoke tests ──────────────────────────────────────────────────────────────

printfn "=== GenFORM MCP Tool Prototype ==="
printfn ""

let info = getResourceInfo ()
printfn $"Resource info: loaded={info.IsLoaded}, lastUpdated={info.LastUpdated}"

let allRules = getDoseRules ()
printfn $"Total dose rules: {allRules.Length}"

let pcmRules =
    filterDoseRules
        {
            Generic = Some "paracetamol"
            Route = None
            Form = None
            Indication = None
            MinAge = None
            MaxAge = None
            MinWeight = None
            MaxWeight = None
        }

printfn $"Paracetamol dose rules: {pcmRules.Length}"
pcmRules |> Array.truncate 3 |> Array.iter (fun r -> printfn $"  {r.Generic} | {r.Route} | {r.Form} | {r.DoseType}")

let solutionRules = getSolutionRules ()
printfn $"Solution rules: {solutionRules.Length}"

let renalRules = getRenalRules ()
printfn $"Renal rules: {renalRules.Length}"

printfn ""
printfn "=== Prototype complete ==="
