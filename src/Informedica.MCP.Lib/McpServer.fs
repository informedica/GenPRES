namespace Informedica.MCP.Lib

open System
open System.ComponentModel
open System.Diagnostics
open System.Threading.Tasks

open Informedica.GenForm.Lib.Resources
open Informedica.Utils.Lib.BCL
open Informedica.Logging.Lib

open ModelContextProtocol.Protocol
open ModelContextProtocol.Server
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open Newtonsoft.Json


/// Shared pure helpers used by MCP tool wiring classes.
module McpHelpers =

    let toJson obj = JsonConvert.SerializeObject(obj, Formatting.Indented)

    let optStr (s: string) = if s |> String.isNullOrWhiteSpace then None else Some s

    let optFloat (v: Nullable<float>) = if v.HasValue then Some v.Value else None


/// MCP server wiring: attributed tool type definitions for GenFORM tools.
/// Each static method delegates to the corresponding GenFormTools handler.
[<McpServerToolType>]
type GenFormMcpTools() =

    static let mutable _provider: IResourceProvider option = None

    static member SetProvider(p: IResourceProvider) = _provider <- Some p

    static member private Provider =
        _provider
        |> Option.defaultWith (fun () -> invalidOp "Provider not initialized. Call GenFormMcpTools.SetProvider first.")


    [<McpServerTool(Name = "get_resource_info")>]
    [<Description("Return cache status: loaded flag, last-updated timestamp, message count")>]
    static member GetResourceInfo() =
        GenFormTools.getResourceInfo GenFormMcpTools.Provider |> McpHelpers.toJson

    [<McpServerTool(Name = "get_dose_rules")>]
    [<Description("Return all dose rules from the GenFORM knowledge base. Warning: large result set.")>]
    static member GetDoseRules() =
        GenFormTools.getDoseRules GenFormMcpTools.Provider |> McpHelpers.toJson

    [<McpServerTool(Name = "filter_dose_rules")>]
    [<Description("Filter dose rules by generic drug name, route, form, indication, and patient demographics. All parameters are optional.")>]
    static member FilterDoseRules
        (
            [<Description("Generic drug name (e.g. 'paracetamol')")>] generic: string,
            [<Description("Administration route (e.g. 'oraal', 'intraveneus')")>] route: string,
            [<Description("Drug form (e.g. 'tablet', 'infuusvloeistof')")>] form: string,
            [<Description("Clinical indication")>] indication: string,
            [<Description("Minimum patient age in months")>] minAge: Nullable<float>,
            [<Description("Maximum patient age in months")>] maxAge: Nullable<float>,
            [<Description("Minimum body weight in kg")>] minWeight: Nullable<float>,
            [<Description("Maximum body weight in kg")>] maxWeight: Nullable<float>
        )
        =
        let input: GenFormTools.FilterDoseRulesInput =
            {
                Generic = McpHelpers.optStr generic
                Route = McpHelpers.optStr route
                Form = McpHelpers.optStr form
                Indication = McpHelpers.optStr indication
                MinAge = McpHelpers.optFloat minAge
                MaxAge = McpHelpers.optFloat maxAge
                MinWeight = McpHelpers.optFloat minWeight
                MaxWeight = McpHelpers.optFloat maxWeight
            }

        GenFormTools.filterDoseRules GenFormMcpTools.Provider input |> McpHelpers.toJson

    [<McpServerTool(Name = "get_solution_rules")>]
    [<Description("Return all solution (preparation) rules from the GenFORM knowledge base")>]
    static member GetSolutionRules() =
        GenFormTools.getSolutionRules GenFormMcpTools.Provider |> McpHelpers.toJson

    [<McpServerTool(Name = "get_renal_rules")>]
    [<Description("Return all renal adjustment rules from the GenFORM knowledge base")>]
    static member GetRenalRules() =
        GenFormTools.getRenalRules GenFormMcpTools.Provider |> McpHelpers.toJson

    [<McpServerTool(Name = "get_prescription_rules")>]
    [<Description("Return prescription rules matching an optional filter. All parameters are optional.")>]
    static member GetPrescriptionRules
        (
            [<Description("Generic drug name")>] generic: string,
            [<Description("Administration route")>] route: string,
            [<Description("Drug form")>] form: string,
            [<Description("Clinical indication")>] indication: string
        )
        =
        let input: GenFormTools.GetPrescriptionRulesInput =
            {
                Generic = McpHelpers.optStr generic
                Route = McpHelpers.optStr route
                Form = McpHelpers.optStr form
                Indication = McpHelpers.optStr indication
                MinAge = None
                MaxAge = None
                MinWeight = None
                MaxWeight = None
            }

        GenFormTools.getPrescriptionRules GenFormMcpTools.Provider input

    [<McpServerTool(Name = "get_formulary")>]
    [<Description("Return the list of formulary products (generic name, form, brand, departments)")>]
    static member GetFormulary() =
        GenFormTools.getFormulary GenFormMcpTools.Provider |> McpHelpers.toJson

    [<McpServerTool(Name = "get_parenteral_meds")>]
    [<Description("Return parenteral medications available for IV preparation")>]
    static member GetParenteralMeds() =
        GenFormTools.getParenteralMeds GenFormMcpTools.Provider |> McpHelpers.toJson


/// MCP server wiring: attributed tool type definitions for GenORDER tools.
/// Each static method delegates to the corresponding GenOrderTools handler.
[<McpServerToolType>]
type GenOrderMcpTools() =

    static let mutable _provider: IResourceProvider option = None

    static member SetProvider(p: IResourceProvider) = _provider <- Some p

    static member private Provider =
        _provider
        |> Option.defaultWith (fun () -> invalidOp "Provider not initialized. Call GenOrderMcpTools.SetProvider first.")


    [<McpServerTool(Name = "get_order_context_filter_options")>]
    [<Description("Return available filter options (generics, routes, indications, forms, dose types) for a patient. This is the primary discovery tool: call this first to learn what medications are available.")>]
    static member GetFilterOptions
        (
            [<Description("Generic drug name to pre-filter on")>] generic: string,
            [<Description("Clinical indication to pre-filter on")>] indication: string,
            [<Description("Administration route to pre-filter on")>] route: string,
            [<Description("Drug form to pre-filter on")>] form: string,
            [<Description("Patient age in months")>] ageMonths: Nullable<float>,
            [<Description("Patient body weight in kg")>] weightKg: Nullable<float>
        )
        =
        let input: GenOrderTools.FilterOptionsInput =
            {
                Generic = McpHelpers.optStr generic
                Indication = McpHelpers.optStr indication
                Route = McpHelpers.optStr route
                Form = McpHelpers.optStr form
                AgeMonths = McpHelpers.optFloat ageMonths
                WeightKg = McpHelpers.optFloat weightKg
            }

        GenOrderTools.getFilterOptions GenOrderMcpTools.Provider input
        |> McpHelpers.toJson

    [<McpServerTool(Name = "get_dose_rules_for_context")>]
    [<Description("Return dose rules matching a filter — a lightweight alternative to creating a full order context when only rule metadata is needed")>]
    static member GetDoseRulesForContext
        (
            [<Description("Generic drug name (e.g. 'paracetamol')")>] generic: string,
            [<Description("Clinical indication")>] indication: string,
            [<Description("Administration route")>] route: string,
            [<Description("Drug form")>] form: string
        )
        =
        let input: GenOrderTools.DoseRulesForContextInput =
            {
                Generic = McpHelpers.optStr generic
                Indication = McpHelpers.optStr indication
                Route = McpHelpers.optStr route
                Form = McpHelpers.optStr form
            }

        GenOrderTools.getDoseRulesForContext GenOrderMcpTools.Provider input
        |> McpHelpers.toJson

    [<McpServerTool(Name = "get_solution_rules_for_context")>]
    [<Description("Return solution/preparation rules for a specific drug combination")>]
    static member GetSolutionRulesForContext
        (
            [<Description("Generic drug name")>] generic: string,
            [<Description("Drug form")>] form: string,
            [<Description("Administration route")>] route: string
        )
        =
        let input: GenOrderTools.SolutionRulesForContextInput =
            {
                Generic = McpHelpers.optStr generic
                Form = McpHelpers.optStr form
                Route = McpHelpers.optStr route
            }

        GenOrderTools.getSolutionRulesForContext GenOrderMcpTools.Provider input
        |> McpHelpers.toJson

    [<McpServerTool(Name = "create_order_context")>]
    [<Description("Create an order context for a patient and return a summary of available scenarios and filter options. This is the main entry point for AI-assisted prescription support. weightKg and heightCm are both required: dose rules are matched on both together, and without either this returns zero scenarios instead of an error. Call get_order_context_filter_options first if weight/height are not yet known.")>]
    static member CreateOrderContext
        (
            [<Description("Patient age in months")>] ageMonths: Nullable<float>,
            [<Description("Patient body weight in kg. Required together with heightCm.")>] weightKg: Nullable<float>,
            [<Description("Patient height in cm. Required together with weightKg: without both, dose rules cannot be matched and zero scenarios are returned.")>] heightCm:
                Nullable<float>,
            [<Description("Patient sex: 'male' or 'female'")>] sex: string,
            [<Description("Hospital department (e.g. 'ICK', 'NEO'). Defaults to 'ICK' if omitted.")>] department: string,
            [<Description("Generic drug name to pre-filter on")>] generic: string,
            [<Description("Clinical indication to pre-filter on")>] indication: string,
            [<Description("Administration route to pre-filter on")>] route: string,
            [<Description("Drug form to pre-filter on")>] form: string
        )
        =
        let input: GenOrderTools.CreateOrderContextInput =
            {
                AgeMonths = McpHelpers.optFloat ageMonths
                WeightKg = McpHelpers.optFloat weightKg
                HeightCm = McpHelpers.optFloat heightCm
                Sex = McpHelpers.optStr sex
                Department = McpHelpers.optStr department
                Generic = McpHelpers.optStr generic
                Indication = McpHelpers.optStr indication
                Route = McpHelpers.optStr route
                Form = McpHelpers.optStr form
            }

        match GenOrderTools.createOrderContext GenOrderMcpTools.Provider input with
        | Ok summary -> McpHelpers.toJson summary
        | Error msg -> McpHelpers.toJson {| Error = msg |}

    [<McpServerTool(Name = "get_order_scenarios")>]
    [<Description("Return a summary of all available order scenarios for a patient with optional pre-filters. Each scenario represents one valid way to prescribe the medication. weightKg and heightCm are both required: dose rules are matched on both together, and without either this returns zero scenarios instead of an error. Call get_order_context_filter_options first if weight/height are not yet known.")>]
    static member GetOrderScenarios
        (
            [<Description("Patient age in months")>] ageMonths: Nullable<float>,
            [<Description("Patient body weight in kg. Required together with heightCm.")>] weightKg: Nullable<float>,
            [<Description("Patient height in cm. Required together with weightKg: without both, dose rules cannot be matched and zero scenarios are returned.")>] heightCm:
                Nullable<float>,
            [<Description("Patient sex: 'male' or 'female'")>] sex: string,
            [<Description("Hospital department (e.g. 'ICK', 'NEO'). Defaults to 'ICK' if omitted.")>] department: string,
            [<Description("Generic drug name")>] generic: string,
            [<Description("Clinical indication")>] indication: string,
            [<Description("Administration route")>] route: string,
            [<Description("Drug form")>] form: string
        )
        =
        let input: GenOrderTools.CreateOrderContextInput =
            {
                AgeMonths = McpHelpers.optFloat ageMonths
                WeightKg = McpHelpers.optFloat weightKg
                HeightCm = McpHelpers.optFloat heightCm
                Sex = McpHelpers.optStr sex
                Department = McpHelpers.optStr department
                Generic = McpHelpers.optStr generic
                Indication = McpHelpers.optStr indication
                Route = McpHelpers.optStr route
                Form = McpHelpers.optStr form
            }

        match GenOrderTools.getOrderScenarios GenOrderMcpTools.Provider input with
        | Ok scenarios -> McpHelpers.toJson scenarios
        | Error msg -> McpHelpers.toJson {| Error = msg |}


/// The tool-call logging filter: wraps every `tools/call` request the SDK dispatches, whichever
/// `[<McpServerToolType>]` class ends up handling it, with no per-tool-method edit needed.
module McpToolLogging =

    module Logging = Informedica.Logging.Lib.Logging

    /// The loggable decision, free of any SDK type: what to log, in what order, given a tool
    /// name, its argument keys, and a thunk that performs the call. Argument KEYS only, never
    /// values - clinical parameters (age/weight/sex/generic/indication) live in the values.
    let logToolCall
        (logger: Logger)
        (toolName: string)
        (argumentKeys: string[])
        (isError: 'result -> bool)
        (call: unit -> Task<'result>)
        : Task<'result>
        =
        task {
            McpMessage.ToolCallStarted(toolName, argumentKeys) |> Logging.logInfo logger

            let sw = Stopwatch.StartNew()

            try
                let! result = call ()
                sw.Stop()

                if isError result then
                    McpMessage.ToolCallFailed(toolName, sw.Elapsed.TotalMilliseconds, "tool result IsError = true")
                    |> Logging.logWarning logger
                else
                    McpMessage.ToolCallCompleted(toolName, sw.Elapsed.TotalMilliseconds)
                    |> Logging.logInfo logger

                return result
            with ex ->
                sw.Stop()

                McpMessage.ToolCallFailed(toolName, sw.Elapsed.TotalMilliseconds, ex.Message)
                |> Logging.logError logger

                return raise ex
        }

    /// SDK-shaped adapter around `logToolCall`: reads `ctx.Params`, converts `ValueTask` to
    /// `Task` and back. Registered once at the composition root via `WithRequestFilters`.
    let callToolLoggingFilter (logger: Logger) : McpRequestFilter<CallToolRequestParams, CallToolResult> =
        McpRequestFilter<CallToolRequestParams, CallToolResult>(fun next ->
            McpRequestHandler<CallToolRequestParams, CallToolResult>(fun ctx ct ->
                let argumentKeys =
                    match ctx.Params.Arguments with
                    | null -> [||]
                    | args -> args.Keys |> Seq.toArray

                let isError (r: CallToolResult) = r.IsError |> Option.ofNullable |> Option.defaultValue false

                let call () = (next.Invoke(ctx, ct)).AsTask()

                ValueTask<CallToolResult>(logToolCall logger ctx.Params.Name argumentKeys isError call)
            )
        )


/// MCP server builder and startup helpers.
module McpServer =

    /// Initialize the provider and register it with both tool types.
    let initProvider (provider: IResourceProvider) =
        GenFormMcpTools.SetProvider provider
        GenOrderMcpTools.SetProvider provider


    /// Create and configure the MCP server host builder.
    let createHostBuilder (logger: Logger) =
        let builder = Host.CreateApplicationBuilder()

        // Belt-and-braces alongside Program.fs's Console.SetOut redirect: the default console
        // logging provider writes through Console.Out on its own schedule, so pin it to stderr
        // explicitly rather than relying solely on the ambient redirect holding for the process
        // lifetime (the pattern the official MCP C# SDK docs use for stdio servers).
        builder.Logging.AddConsole(fun options -> options.LogToStandardErrorThreshold <- LogLevel.Trace)
        |> ignore

        builder.Services
            .AddMcpServer(fun options ->
                options.ServerInfo <-
                    ModelContextProtocol.Protocol.Implementation(Name = "GenPRES MCP Server", Version = "1.0.0")
            )
            .WithStdioServerTransport()
            .WithRequestFilters(fun rf -> rf.AddCallToolFilter(McpToolLogging.callToolLoggingFilter logger) |> ignore)
            .WithTools<GenFormMcpTools>()
            .WithTools<GenOrderMcpTools>()
        |> ignore

        builder


    /// Start the MCP server with stdio transport.
    /// Blocks until the server is stopped.
    let run (logger: Logger) (provider: IResourceProvider) =
        initProvider provider

        eprintfn "[MCP] Starting GenPRES MCP server (stdio transport)..."
        eprintfn "[MCP] Working directory: %s" Environment.CurrentDirectory

        let builder = createHostBuilder logger
        let app = builder.Build()
        app.RunAsync() |> Async.AwaitTask |> Async.RunSynchronously
