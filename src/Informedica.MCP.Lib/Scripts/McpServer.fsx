/// <summary>
/// MCP server for Informedica.MCP.Lib — exposes GenFORM and GenORDER
/// tools via stdio transport using the ModelContextProtocol NuGet SDK.
///
/// Reference implementation: https://github.com/jovaneyck/fsi-mcp-server
/// NuGet package: https://www.nuget.org/packages/ModelContextProtocol
/// ADR: docs/adr/0009-mcp-server-architecture.md
///
/// Usage:
///   # Build first to ensure DLLs are up to date
///   dotnet run build
///
///   # Run the MCP server (stdio transport)
///   cd src/Informedica.MCP.Lib/Scripts
///   dotnet fsi McpServer.fsx
///
///   # Or load interactively in FSI for testing (server won't start)
///   #I "/path/to/src/Informedica.MCP.Lib/Scripts"
///   #load "McpServer.fsx";;
///
/// Claude Desktop configuration (add to claude_desktop_config.json):
/// {
///   "mcpServers": {
///     "genpres": {
///       "command": "dotnet",
///       "args": ["fsi", "/path/to/src/Informedica.MCP.Lib/Scripts/McpServer.fsx"],
///       "env": {
///         "GENPRES_URL_ID": "<your-sheet-id>",
///         "GENPRES_PROD": "1"
///       }
///     }
///   }
/// }
/// </summary>


// ── Dependencies ────────────────────────────────────────────────────────────

#I __SOURCE_DIRECTORY__

// NuGet packages needed at runtime by the compiled DLLs and the MCP SDK.
// We reference these directly instead of via load-dependencies.fsx to avoid
// duplicate PackageReference warnings when mixing #load-ed and inline NuGet refs.
#r "nuget: MathNet.Numerics.FSharp"
#r "nuget: Newtonsoft.Json"
#r "nuget: FsToolkit.ErrorHandling"
#r "nuget: FParsec"
#r "nuget: FSharp.Data, 8.0"
#r "nuget: ModelContextProtocol, 1.2.0"
#r "nuget: Microsoft.Extensions.Hosting"

// All DLLs are copied to MCP.Lib/bin via transitive project reference
#r "../bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.Logging.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenUNITS.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.ZIndex.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.ZForm.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenCORE.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenSOLVER.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenFORM.Lib.dll"
#r "../bin/Debug/net10.0/Informedica.GenORDER.Lib.dll"


// ── Environment setup ───────────────────────────────────────────────────────

open System
open System.ComponentModel

Informedica.Utils.Lib.Env.loadDotEnv () |> ignore

Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")
Environment.SetEnvironmentVariable("GENPRES_PROD", "1")

let repoRoot =
    __SOURCE_DIRECTORY__
    |> Informedica.Utils.Lib.Path.combineWith "../../../"

Environment.CurrentDirectory <- repoRoot


// ── Audit logging stub ──────────────────────────────────────────────────────
// Writes to stderr so it does not interfere with the stdio MCP transport.

let auditLog toolName (input: string) =
    eprintfn $"[MCP AUDIT] {DateTime.UtcNow:O} | tool=%s{toolName} | input=%s{input}"


// ── Provider setup ──────────────────────────────────────────────────────────

open Informedica.GenForm.Lib
open Informedica.GenForm.Lib.Resources
open Informedica.GenForm.Lib.Types
open Informedica.GenOrder.Lib

let dataUrlId =
    Environment.GetEnvironmentVariable "GENPRES_URL_ID"

let provider: IResourceProvider =
    Api.getCachedProviderWithDataUrlId FormLogging.noOp dataUrlId


// ── Tool input types ────────────────────────────────────────────────────────

type FilterDoseRulesInput =
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

type FilterSolutionRulesInput =
    {
        Generic: string option
        Form: string option
        Route: string option
    }

type CreateOrderContextInput =
    {
        AgeMonths: float option
        WeightKg: float option
        BsaM2: float option
        Sex: string option
        Generic: string option
        Indication: string option
        Route: string option
        Form: string option
    }

type FilterOptionsInput =
    {
        Generic: string option
        Indication: string option
        Route: string option
        Form: string option
        AgeMonths: float option
        WeightKg: float option
    }

type DoseRulesForContextInput =
    {
        Generic: string option
        Indication: string option
        Route: string option
        Form: string option
    }

type SolutionRulesForContextInput =
    {
        Generic: string option
        Form: string option
        Route: string option
    }


// ── Tool output types ───────────────────────────────────────────────────────

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


// ── Mapping helpers ─────────────────────────────────────────────────────────

open Informedica.GenUnits.Lib

let private limitToFloat limit =
    limit
    |> Option.bind (fun l ->
        match l with
        | Informedica.GenCore.Lib.Ranges.Limit.Inclusive vu
        | Informedica.GenCore.Lib.Ranges.Limit.Exclusive vu ->
            vu
            |> ValueUnit.getValue
            |> Array.tryHead
            |> Option.map Informedica.Utils.Lib.BCL.BigRational.toDouble
    )

let private doseRuleToOutput (dr: DoseRule) : DoseRuleOutput =
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
        MaxConcentration = None
        MinConcentration = None
    }

let private renalRuleToOutput (rr: RenalRule) : RenalRuleOutput =
    {
        Generic = rr.Generic
        Route = rr.Route
        AdjustmentFactor = None
        Comment = rr |> sprintf "%A" |> fun s -> s.[..min 200 (s.Length - 1)]
    }

open Patient.Optics

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

    pat |> Patient.setDepartment (Some "ICK")


// ── Serialization helper ────────────────────────────────────────────────────

let private toJson obj =
    Newtonsoft.Json.JsonConvert.SerializeObject(obj, Newtonsoft.Json.Formatting.Indented)


// ── Tool handler functions ──────────────────────────────────────────────────

// -- GenFORM tools --

let getResourceInfo () : ResourceInfoOutput =
    let info = provider.GetResourceInfo()
    {
        IsLoaded = info.IsLoaded
        LastUpdated = info.LastUpdated.ToString "O"
        MessageCount = info.Messages |> Array.length
    }

let getDoseRules () : DoseRuleOutput[] =
    provider
    |> Informedica.GenForm.Lib.Api.getDoseRules
    |> Array.map doseRuleToOutput

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

    let allRules = provider |> Informedica.GenForm.Lib.Api.getDoseRules

    provider
    |> Informedica.GenForm.Lib.Api.filterDoseRules
    <| filter
    <| allRules
    |> Array.map doseRuleToOutput

let getSolutionRules () : SolutionRuleOutput[] =
    provider
    |> Informedica.GenForm.Lib.Api.getSolutionRules
    |> Array.map solutionRuleToOutput

let getRenalRules () : RenalRuleOutput[] =
    provider
    |> Informedica.GenForm.Lib.Api.getRenalRules
    |> Array.map renalRuleToOutput

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

    match provider |> Informedica.GenForm.Lib.Api.filterPrescriptionRules <| filter with
    | Ok rules -> $"Found {rules |> Array.length} prescription rules"
    | Error msgs ->
        msgs
        |> List.map (fun m -> m |> sprintf "%A")
        |> String.concat "; "
        |> fun s -> $"Error: {s}"

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


// -- GenORDER tools --

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

    Formulary.getDoseRules provider filter
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

let getSolutionRulesForContext (input: SolutionRulesForContextInput) =
    Formulary.getSolutionRules provider input.Generic input.Form input.Route
    |> Array.map (fun sr ->
        {|
            Generic = sr.Generic
            Form = sr.Form
            Route = sr.Route
            DiluentCount = sr.Diluents |> Array.length
            Diluents = sr.Diluents |> Array.map _.Generic
        |}
    )

let createOrderContext (input: CreateOrderContextInput) : Result<OrderContextSummaryOutput, string> =
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
                SelectedScenario =
                    result.Scenarios
                    |> Array.tryExactlyOne
                    |> Option.map (fun sc -> sc.Name)
                FilterOptions = filterOpts
            }

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


// ── Helper: convert nullable string to option ───────────────────────────────

let private optStr (s: string) =
    if String.IsNullOrWhiteSpace s then None else Some s

let private optFloat (v: Nullable<float>) =
    if v.HasValue then Some v.Value else None


// ── MCP tool type definitions ───────────────────────────────────────────────
// Uses attribute-based registration for the ModelContextProtocol SDK.
// String parameters use null to mean "no filter" (F# options are not
// handled by the C# SDK reflection).

open ModelContextProtocol.Server


[<McpServerToolType>]
type GenFormTools() =

    [<McpServerTool(Name = "get_resource_info")>]
    [<Description("Return cache status: loaded flag, last-updated timestamp, message count")>]
    static member GetResourceInfo() =
        auditLog "get_resource_info" ""
        getResourceInfo () |> toJson

    [<McpServerTool(Name = "get_dose_rules")>]
    [<Description("Return all dose rules from the GenFORM knowledge base. Warning: large result set.")>]
    static member GetDoseRules() =
        auditLog "get_dose_rules" ""
        getDoseRules () |> toJson

    [<McpServerTool(Name = "filter_dose_rules")>]
    [<Description("Filter dose rules by generic drug name, route, form, indication, and patient demographics. All parameters are optional.")>]
    static member FilterDoseRules(
        [<Description("Generic drug name (e.g. 'paracetamol')")>] generic: string,
        [<Description("Administration route (e.g. 'oraal', 'intraveneus')")>] route: string,
        [<Description("Drug form (e.g. 'tablet', 'infuusvloeistof')")>] form: string,
        [<Description("Clinical indication")>] indication: string,
        [<Description("Minimum patient age in months")>] minAge: Nullable<float>,
        [<Description("Maximum patient age in months")>] maxAge: Nullable<float>,
        [<Description("Minimum body weight in kg")>] minWeight: Nullable<float>,
        [<Description("Maximum body weight in kg")>] maxWeight: Nullable<float>
    ) =
        let input =
            {
                FilterDoseRulesInput.Generic = optStr generic
                Route = optStr route
                Form = optStr form
                Indication = optStr indication
                MinAge = optFloat minAge
                MaxAge = optFloat maxAge
                MinWeight = optFloat minWeight
                MaxWeight = optFloat maxWeight
            }

        auditLog "filter_dose_rules" (toJson input)
        filterDoseRules input |> toJson

    [<McpServerTool(Name = "get_solution_rules")>]
    [<Description("Return all solution (preparation) rules from the GenFORM knowledge base")>]
    static member GetSolutionRules() =
        auditLog "get_solution_rules" ""
        getSolutionRules () |> toJson

    [<McpServerTool(Name = "get_renal_rules")>]
    [<Description("Return all renal adjustment rules from the GenFORM knowledge base")>]
    static member GetRenalRules() =
        auditLog "get_renal_rules" ""
        getRenalRules () |> toJson

    [<McpServerTool(Name = "get_prescription_rules")>]
    [<Description("Return prescription rules matching an optional filter. All parameters are optional.")>]
    static member GetPrescriptionRules(
        [<Description("Generic drug name")>] generic: string,
        [<Description("Administration route")>] route: string,
        [<Description("Drug form")>] form: string,
        [<Description("Clinical indication")>] indication: string
    ) =
        let input =
            {
                GetPrescriptionRulesInput.Generic = optStr generic
                Route = optStr route
                Form = optStr form
                Indication = optStr indication
                MinAge = None
                MaxAge = None
                MinWeight = None
                MaxWeight = None
            }

        auditLog "get_prescription_rules" (toJson input)
        getPrescriptionRules input

    [<McpServerTool(Name = "get_formulary")>]
    [<Description("Return the list of formulary products (generic name, form, brand, departments)")>]
    static member GetFormulary() =
        auditLog "get_formulary" ""
        getFormulary () |> toJson

    [<McpServerTool(Name = "get_parenteral_meds")>]
    [<Description("Return parenteral medications available for IV preparation")>]
    static member GetParenteralMeds() =
        auditLog "get_parenteral_meds" ""
        getParenteralMeds () |> toJson


[<McpServerToolType>]
type GenOrderTools() =

    [<McpServerTool(Name = "get_order_context_filter_options")>]
    [<Description("Return available filter options (generics, routes, indications, forms, dose types) for a patient. This is the primary discovery tool: call this first to learn what medications are available.")>]
    static member GetFilterOptions(
        [<Description("Generic drug name to pre-filter on")>] generic: string,
        [<Description("Clinical indication to pre-filter on")>] indication: string,
        [<Description("Administration route to pre-filter on")>] route: string,
        [<Description("Drug form to pre-filter on")>] form: string,
        [<Description("Patient age in months")>] ageMonths: Nullable<float>,
        [<Description("Patient body weight in kg")>] weightKg: Nullable<float>
    ) =
        let input =
            {
                FilterOptionsInput.Generic = optStr generic
                Indication = optStr indication
                Route = optStr route
                Form = optStr form
                AgeMonths = optFloat ageMonths
                WeightKg = optFloat weightKg
            }

        auditLog "get_order_context_filter_options" (toJson input)
        getFilterOptions input |> toJson

    [<McpServerTool(Name = "get_dose_rules_for_context")>]
    [<Description("Return dose rules matching a filter — a lightweight alternative to creating a full order context when only rule metadata is needed")>]
    static member GetDoseRulesForContext(
        [<Description("Generic drug name (e.g. 'paracetamol')")>] generic: string,
        [<Description("Clinical indication")>] indication: string,
        [<Description("Administration route")>] route: string,
        [<Description("Drug form")>] form: string
    ) =
        let input =
            {
                DoseRulesForContextInput.Generic = optStr generic
                Indication = optStr indication
                Route = optStr route
                Form = optStr form
            }

        auditLog "get_dose_rules_for_context" (toJson input)
        getDoseRulesForContext input |> toJson

    [<McpServerTool(Name = "get_solution_rules_for_context")>]
    [<Description("Return solution/preparation rules for a specific drug combination")>]
    static member GetSolutionRulesForContext(
        [<Description("Generic drug name")>] generic: string,
        [<Description("Drug form")>] form: string,
        [<Description("Administration route")>] route: string
    ) =
        let input =
            {
                SolutionRulesForContextInput.Generic = optStr generic
                Form = optStr form
                Route = optStr route
            }

        auditLog "get_solution_rules_for_context" (toJson input)
        getSolutionRulesForContext input |> toJson

    [<McpServerTool(Name = "create_order_context")>]
    [<Description("Create an order context for a patient and return a summary of available scenarios and filter options. This is the main entry point for AI-assisted prescription support.")>]
    static member CreateOrderContext(
        [<Description("Patient age in months")>] ageMonths: Nullable<float>,
        [<Description("Patient body weight in kg")>] weightKg: Nullable<float>,
        [<Description("Patient body surface area in m²")>] bsaM2: Nullable<float>,
        [<Description("Patient sex: 'male', 'female', or 'any'")>] sex: string,
        [<Description("Generic drug name to pre-filter on")>] generic: string,
        [<Description("Clinical indication to pre-filter on")>] indication: string,
        [<Description("Administration route to pre-filter on")>] route: string,
        [<Description("Drug form to pre-filter on")>] form: string
    ) =
        let input =
            {
                CreateOrderContextInput.AgeMonths = optFloat ageMonths
                WeightKg = optFloat weightKg
                BsaM2 = optFloat bsaM2
                Sex = optStr sex
                Generic = optStr generic
                Indication = optStr indication
                Route = optStr route
                Form = optStr form
            }

        auditLog "create_order_context" (toJson input)

        match createOrderContext input with
        | Ok summary -> toJson summary
        | Error msg -> toJson {| Error = msg |}

    [<McpServerTool(Name = "get_order_scenarios")>]
    [<Description("Return a summary of all available order scenarios for a patient with optional pre-filters. Each scenario represents one valid way to prescribe the medication.")>]
    static member GetOrderScenarios(
        [<Description("Patient age in months")>] ageMonths: Nullable<float>,
        [<Description("Patient body weight in kg")>] weightKg: Nullable<float>,
        [<Description("Patient body surface area in m²")>] bsaM2: Nullable<float>,
        [<Description("Patient sex: 'male', 'female', or 'any'")>] sex: string,
        [<Description("Generic drug name")>] generic: string,
        [<Description("Clinical indication")>] indication: string,
        [<Description("Administration route")>] route: string,
        [<Description("Drug form")>] form: string
    ) =
        let input =
            {
                CreateOrderContextInput.AgeMonths = optFloat ageMonths
                WeightKg = optFloat weightKg
                BsaM2 = optFloat bsaM2
                Sex = optStr sex
                Generic = optStr generic
                Indication = optStr indication
                Route = optStr route
                Form = optStr form
            }

        auditLog "get_order_scenarios" (toJson input)
        getOrderScenarios input |> toJson


// ── Server startup ──────────────────────────────────────────────────────────
// Only starts the server when run directly via `dotnet fsi McpServer.fsx`.
// When #load-ed interactively, all tool functions remain available for testing.

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

let private startServer () =
    let builder = Host.CreateApplicationBuilder()

    builder.Services
        .AddMcpServer(fun options ->
            options.ServerInfo <-
                ModelContextProtocol.Protocol.Implementation(
                    Name = "GenPRES MCP Server",
                    Version = "1.0.0"
                )
        )
        .WithStdioServerTransport()
        .WithTools<GenFormTools>()
        .WithTools<GenOrderTools>()
    |> ignore

    eprintfn "[MCP] Starting GenPRES MCP server (stdio transport)..."
    eprintfn "[MCP] Data URL ID: %s" (if String.IsNullOrEmpty dataUrlId then "<not set>" else "***")
    eprintfn "[MCP] Working directory: %s" Environment.CurrentDirectory

    let app = builder.Build()
    app.RunAsync() |> Async.AwaitTask |> Async.RunSynchronously


// Guard: only start server when run directly
let private isInteractive =
    fsi.CommandLineArgs
    |> Array.exists (fun arg -> arg.EndsWith("McpServer.fsx"))

if isInteractive then
    startServer ()
else
    eprintfn "[MCP] Script loaded interactively — server not started."
    eprintfn "[MCP] Tool handler functions are available for testing."
