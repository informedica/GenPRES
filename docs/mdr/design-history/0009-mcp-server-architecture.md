# ADR-0009: MCP Server Architecture

**Issue**: [Adding MCP servers](https://github.com/informedica/GenPRES/issues/)

**Date**: 2026-03-28
**Status**: Accepted

## Context

GenPRES exposes medication knowledge through a web API (`IServerApi`) for the Fable client and through direct F# calls for server-side composition. A third access pathway is needed to allow AI assistants to query prescription rules, dose rules, and order scenarios without duplicating domain logic.

## Decision

Implement MCP (Model Context Protocol) servers for `Informedica.GenFORM.Lib` and `Informedica.GenORDER.Lib` using the existing placeholder `Informedica.MCP.Lib`. The initial scope is read-only tools only. The implementation follows the scripts-first workflow: prototype in `.fsx` scripts, migrate to source files after human review.

## Consequences

- AI assistants (Claude Desktop, VS Code Copilot, custom agents) can query GenPRES knowledge through standard MCP tool calls.
- No domain logic changes are needed in `GenFORM.Lib` or `GenORDER.Lib`.
- All MCP tool calls are read-only in Phase 1; write operations require a separate ADR.
- Every tool call must be audit-logged to satisfy MDR traceability requirements.
- The MCP layer is an additional presentation layer alongside the Fable.Remoting API; it does not replace it.

**References**:

- <https://modelcontextprotocol.io/introduction>
- <https://github.com/jovaneyck/fsi-mcp-server> — F# MCP server reference implementation

---

## Table of Contents

- [Summary](#summary)
- [What Is MCP?](#what-is-mcp)
- [Motivation](#motivation)
- [Proposed Architecture](#proposed-architecture)
  - [Overview](#overview)
  - [Informedica.MCP.Lib](#informedicamcplib)
  - [MCP Tool Mapping](#mcp-tool-mapping)
    - [GenFORM Tools](#genform-tools)
    - [GenORDER Tools](#genorder-tools)
  - [Hosting Options](#hosting-options)
- [Integration with Existing Architecture](#integration-with-existing-architecture)
  - [Relationship to the Server Layering](#relationship-to-the-server-layering)
- [Safety and Security Considerations](#safety-and-security-considerations)
- [Implementation Approach](#implementation-approach)
  - [Script Prototyping Plan](#script-prototyping-plan)
  - [NuGet Dependencies](#nuget-dependencies)
  - [Libraries Excluded](#libraries-excluded)
- [Architecture Status](#architecture-status)

---

## Summary

The GenPRES system exposes medication knowledge (prescription rules, dose rules, solution rules, order contexts) through its `GenFORM` and `GenORDER` libraries. The **Model Context Protocol (MCP)** provides a standard interface for AI assistants to call external tools, making it possible to expose these libraries as AI-callable services without changing the domain logic.

This ADR describes the plan to implement MCP servers for `Informedica.GenFORM.Lib` and `Informedica.GenORDER.Lib` using the existing placeholder `Informedica.MCP.Lib` and the existing `IResourceProvider` / `CachedResourceProvider` infrastructure.

The initial scope is **read-only tools** only — no write operations that could affect running clinical workflows.

---

## What Is MCP?

The **Model Context Protocol** (MCP) is an open standard that allows AI assistants (LLMs) to interact with external systems through a defined protocol. An MCP server exposes:

- **Tools** — callable functions with a name, description, and JSON Schema input specification
- **Resources** — readable content (e.g., documents, data)
- **Prompts** — pre-defined conversation templates

When an AI assistant encounters a question it can answer with external data, it calls a tool, receives the response, and incorporates it into its answer. This is analogous to function calling in OpenAI or tool use in Anthropic's Claude.

```text
AI Assistant
    │  tool call: { name: "get_dose_rules", input: { generic: "paracetamol" } }
    ▼
MCP Server (GenFORM / GenORDER)
    │  invokes: GenForm.Api.filterDoseRules provider filter doseRules
    ▼
    │  response: { doseRules: [...] }
    ▼
AI Assistant incorporates response into answer
```

---

## Motivation

GenPRES already exposes its medication knowledge through:

1. A web API (`IServerApi`) for the Fable client
2. Direct F# function calls for server-side composition

Adding MCP servers enables a third access pathway — AI assistants — without duplicating or changing any domain logic. Practical use cases include:

- An AI coding assistant asking "which dose rules exist for morphine IV in neonates?" during development
- A clinical decision support AI consulting GenFORM to validate a dose before suggesting it
- Automated testing harnesses using AI to generate and verify medication scenarios

The `Informedica.MCP.Lib` project already exists as a placeholder in the solution. This ADR specifies how to fill it.

---

## Proposed Architecture

### Overview

```text
┌──────────────────────────────────────────────────────────────────┐
│  AI Assistant / MCP Client                                       │
│  (Claude Desktop, Cursor, VS Code Copilot, custom agent, etc.)  │
└──────────────────────────┬───────────────────────────────────────┘
                           │  MCP protocol (JSON-RPC 2.0 over stdio / SSE)
                           ▼
┌──────────────────────────────────────────────────────────────────┐
│  Informedica.MCP.Lib                                             │
│  McpServer (ModelContextProtocol NuGet)                          │
│  ├── GenFormMcpServer   — tools backed by GenFORM API            │
│  └── GenOrderMcpServer  — tools backed by GenORDER API           │
└──────────────────────────┬───────────────────────────────────────┘
                           │  delegates to
                           ▼
┌──────────────────────────────────────────────────────────────────┐
│  Informedica.GenFORM.Lib / Informedica.GenORDER.Lib              │
│  (IResourceProvider, Api.fs functions — unchanged)               │
└──────────────────────────────────────────────────────────────────┘
```

The MCP layer is a **thin adapter**: it translates JSON tool calls into strongly typed F# function calls and serialises the responses back to JSON. No domain logic lives in `Informedica.MCP.Lib`.

### Informedica.MCP.Lib

The library currently contains only a stub (`Library.fs`). The plan is to replace this with:

```text
src/Informedica.GenFORM.Lib/Scripts/
└── McpTools.fsx            — ✅ prototype script for GenFORM tools

src/Informedica.GenORDER.Lib/Scripts/
└── McpTools.fsx            — ✅ prototype script for GenORDER tools

src/Informedica.MCP.Lib/
├── Library.fs              — stub (to be removed after review)
├── Scripts/
│   └── McpServer.fsx       — ✅ MCP server wiring design prototype
├── McpServer.fs            — MCP server wiring + entry-point helpers  (after review)
├── McpTools.GenForm.fs     — tool handlers for GenFORM               (after review)
└── McpTools.GenOrder.fs    — tool handlers for GenORDER              (after review)
```

> **Policy note**: Per `AGENTS.md`, new code must first be prototyped in `.fsx` scripts. The prototype scripts (✅ already committed) live in each library's own `Scripts/` folder because they need `load.fsx` from the same directory to access the library's compiled DLLs and source files. The `.fs` files listed above are created inside `Informedica.MCP.Lib` only after the human reviewer has approved the scripted prototypes.

### MCP Tool Mapping

Each MCP tool maps to one or more existing `GenFORM.Api` or `GenORDER.Api` functions. All tools are **read-only**; they never mutate resource state.

#### GenFORM Tools

| Tool name | API function | Description |
|-----------|-------------|-------------|
| `get_dose_rules` | `Api.getDoseRules provider` | Return all dose rules |
| `filter_dose_rules` | `Api.filterDoseRules provider filter doseRules` | Filter dose rules by generic, route, form, patient category |
| `get_solution_rules` | `Api.getSolutionRules provider` | Return all solution rules |
| `get_renal_rules` | `Api.getRenalRules provider` | Return all renal adjustment rules |
| `get_prescription_rules` | `Api.getPrescriptionRules provider patient` | Return prescription rules for a patient category |
| `filter_prescription_rules` | `Api.filterPrescriptionRules provider filter` | Filter prescription rules |
| `get_resource_info` | `provider.GetResourceInfo()` | Return cache status (last updated, loaded, messages) |
| `get_formulary` | `provider.GetFormularyProducts()` | Return the list of formulary products |
| `get_parenteral_meds` | `provider.GetParenteralMeds()` | Return available parenteral medications |

**Input schema example — `filter_dose_rules`**:

```json
{
  "type": "object",
  "properties": {
    "generic":    { "type": "string", "description": "Generic drug name (e.g. 'paracetamol')" },
    "route":      { "type": "string", "description": "Administration route (e.g. 'iv', 'oraal')" },
    "form":       { "type": "string", "description": "Drug form (e.g. 'tablet', 'infuusvloeistof')" },
    "indication": { "type": "string", "description": "Clinical indication (optional)" },
    "minAge":     { "type": "number", "description": "Minimum patient age in months (optional)" },
    "maxAge":     { "type": "number", "description": "Maximum patient age in months (optional)" },
    "minWeight":  { "type": "number", "description": "Minimum body weight in kg (optional)" },
    "maxWeight":  { "type": "number", "description": "Maximum body weight in kg (optional)" }
  },
  "required": []
}
```

#### GenORDER Tools

| Tool name | API function | Description |
|-----------|-------------|-------------|
| `create_order_context` | `OrderContext.create logger provider patient` | Create a new order context for a patient |
| `get_order_scenarios` | `OrderContext.getScenarios logger provider ctx` | Return available order scenarios for the current context |
| `get_order_context_filter_options` | `OrderContext.getRules logger provider ctx` | Return available filter options (generics, routes, indications, forms) |
| `get_dose_rules_for_context` | `OrderContext.getDoseRules provider filter` | Return dose rules for the current filter state |
| `get_solution_rules_for_context` | `OrderContext.getSolutionRules provider generic form route` | Return solution rules for a specific drug |
| `summarise_order_scenario` | `OrderContext.toString stage ctx` | Produce a human-readable summary of an order context |

> **Note**: Navigation commands (increase/decrease dose, select scenario) are deferred to a later phase. The initial release is strictly read-only.

### Hosting Options

An MCP server can be hosted in multiple ways:

| Option | Transport | Use case |
|--------|-----------|----------|
| **stdio** | Standard input/output | Local AI assistant (Claude Desktop, VS Code extension) |
| **SSE** | Server-Sent Events (HTTP) | Remote AI agent or cloud deployment |
| **In-process** | Direct .NET function call | Integration tests; embedding in GenPRES.Server |

For the initial implementation, **stdio** transport is the primary target because:

- It requires no network configuration
- It is the standard for the reference implementation (fsi-mcp-server)
- Claude Desktop and VS Code Copilot both support stdio-hosted MCP servers

SSE transport can be added later to allow remote AI agents to call the server.

---

## Integration with Existing Architecture

### Relationship to the Server Layering

The MCP server is a **new presentation layer** alongside the existing Fable.Remoting API, not a replacement. Tool handlers call `GenForm.Api` functions through the `IResourceProvider`, which is the stable boundary:

```text
┌──────────────────────────────────────────────────────────────┐
│  PRESENTATION                                                │
│  ├── IServerApi (Fable.Remoting)  — web client               │
│  └── IMcpServer (MCP protocol)    — AI assistants  [NEW]     │
├──────────────────────────────────────────────────────────────┤
│  APPLICATION LAYER  (ServerApi.Services.fs, .Ports.fs)       │
├──────────────────────────────────────────────────────────────┤
│  DOMAIN  GenOrder.Lib  GenForm.Lib  GenSolver.Lib            │
│          (pure — no changes required)                        │
└──────────────────────────────────────────────────────────────┘
```

The MCP server can be hosted:

- **Standalone** — a separate executable that loads `IResourceProvider` independently
- **Embedded** — started inside `GenPRES.Server` on a background thread (SSE transport)

For developer tooling, standalone is preferred. For production AI integration, embedded SSE is more appropriate.

---

## Safety and Security Considerations

GenPRES is a **medical device software** project. The following constraints apply to the MCP integration:

1. **Read-only, Phase 1**: All tools in the initial release are read-only. No tool may trigger a dose calculation that feeds back into a live patient order without explicit human review and approval.

2. **No direct patient data in MCP responses**: MCP responses must not include patient-identifiable information. The `OrderContext` passed to GenORDER tools uses anonymised or synthetic patient data.

3. **Audit logging**: Every MCP tool call must be logged with timestamp, tool name, inputs (minus any PII), and response status. This satisfies the MDR traceability requirement.

4. **Authentication (SSE transport only)**: If the SSE transport is used, the server must require an API key or equivalent authentication. The stdio transport is inherently restricted to the local machine.

5. **Validation of outputs**: MCP responses contain dosing information derived from validated rule sets. AI-generated summaries of these responses are not validated medical advice and must be labelled as such in any user-facing application.

6. **Review gate**: Per the repository policy, no new code may be merged to `.fs` source files without human review. The MCP implementation follows the scripts-first workflow.

---

## Implementation Approach

Following the repository's **script-based development workflow** (see `AGENTS.md`):

### Script Prototyping Plan

1. **Prototype GenFORM tools** in `src/Informedica.GenFORM.Lib/Scripts/McpTools.fsx`
   - Load `load.fsx` to access `GenFORM` types and functions
   - Define tool input/output types as anonymous records
   - Implement handler functions calling `GenForm.Api.*`
   - Test with sample inputs (paracetamol, morphine, gentamicin)

2. **Prototype GenORDER tools** in `src/Informedica.GenORDER.Lib/Scripts/McpTools.fsx`
   - Load `load.fsx` to access `GenORDER` types and functions
   - Implement handler functions for `create_order_context`, `get_order_scenarios`
   - Test with `Scenarios.pcmSupp`, `Scenarios.morfCont` test cases

3. **Prototype MCP server wiring** in `src/Informedica.MCP.Lib/Scripts/McpServer.fsx`
   - Reference the `ModelContextProtocol` NuGet package
   - Wire tool handlers into an `McpServerBuilder`
   - Run against Claude Desktop or a mock MCP client

4. **Human review** of all three scripts

5. **Migration to source files** (only after approval):
   - `McpTools.GenForm.fs` — tool handlers
   - `McpTools.GenOrder.fs` — tool handlers
   - `McpServer.fs` — server builder and entry point
   - Update `Informedica.MCP.Lib.fsproj` to include new files and the `ModelContextProtocol` reference

### NuGet Dependencies

The only new external dependency required is the official .NET MCP SDK:

| Package | Version | Use |
|---------|---------|-----|
| `ModelContextProtocol` | 1.2.0 | MCP server builder, tool registration, stdio/SSE transport |

> **Security check** (performed 2026-03-29): The `ModelContextProtocol` package at version `1.2.0` was checked against the GitHub Advisory Database — no known vulnerabilities found. Before adding this dependency, re-run the advisory check to verify it remains clean. Pin the dependency to an exact version (e.g. `1.2.0`) in `paket.dependencies` and update it deliberately rather than using a floating or wildcard specifier, to ensure reproducible builds as required by MDR traceability.

The `ModelContextProtocol` package is maintained by the [modelcontextprotocol GitHub organisation](https://github.com/modelcontextprotocol/csharp-sdk), which is the official .NET implementation. The fsi-mcp-server reference uses the same package.

No changes are required to `GenFORM.Lib`, `GenORDER.Lib`, or any other library project.

### Libraries Excluded

| Library | Reason |
|---------|--------|
| `Informedica.GenSOLVER.Lib` | Pure mathematical solver; results only meaningful in the context of a full order — covered by GenORDER tools |
| `Informedica.GenUNITS.Lib` | Pure unit-of-measure utilities; not a domain service |
| `Informedica.GenCORE.Lib` | Core type definitions only |
| `Informedica.Utils.Lib` | Pure utility functions |
| `Informedica.GenPRES.Server` | Already exposes the full API via `IServerApi`; MCP is an additional pathway, not a replacement |

## Architecture Status

| Component | Status |
|-----------|--------|
| `Informedica.MCP.Lib` placeholder | ✅ Exists in solution |
| GenFORM MCP tool prototype script | ✅ Added (`src/Informedica.GenFORM.Lib/Scripts/McpTools.fsx`) |
| GenORDER MCP tool prototype script | ✅ Added (`src/Informedica.GenORDER.Lib/Scripts/McpTools.fsx`) |
| MCP server wiring prototype script | ✅ Added (`src/Informedica.MCP.Lib/Scripts/McpServer.fsx`) |
| Human review of scripts | ⬜ Pending review |
| Source file migration | ⬜ Pending review |
| stdio transport | ⬜ Not started |
| SSE transport | ⬜ Deferred (phase 2) |
| Audit logging integration | ⬜ Not started |
| Claude Desktop configuration | ⬜ Not started |
