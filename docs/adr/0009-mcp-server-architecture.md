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
- Every tool call must be audit-logged so that its use is traceable.
- The MCP layer is an additional presentation layer alongside the Fable.Remoting API; it does not replace it.

**References**:

- <https://modelcontextprotocol.io/introduction>
- <https://github.com/jovaneyck/fsi-mcp-server> — F# MCP server reference implementation

## Transport

`stdio` is the primary transport: it needs no network configuration, it is what the
`fsi-mcp-server` reference implementation uses, and both Claude Desktop and VS Code Copilot
support stdio-hosted MCP servers. SSE (Server-Sent Events over HTTP) is deferred to a later
phase for remote agents, and would additionally require authentication — see constraint 4 below.

## Safety and Security Considerations


GenPRES is a **medical device software** project. The following constraints apply to the MCP integration:

1. **Read-only, Phase 1**: All tools in the initial release are read-only. No tool may trigger a dose calculation that feeds back into a live patient order without explicit human review and approval.

2. **No direct patient data in MCP responses**: MCP responses must not include patient-identifiable information. The `OrderContext` passed to GenORDER tools uses anonymised or synthetic patient data.

3. **Audit logging**: Every MCP tool call must be logged with timestamp, tool name, inputs (minus any PII), and response status, so that every query of dosing knowledge is traceable.

4. **Authentication (SSE transport only)**: If the SSE transport is used, the server must require an API key or equivalent authentication. The stdio transport is inherently restricted to the local machine.

5. **Validation of outputs**: MCP responses contain dosing information derived from validated rule sets. AI-generated summaries of these responses are not validated medical advice and must be labeled as such in any user-facing application.

6. **Review gate**: Per the repository policy, no new code may be merged to `.fs` source files without human review. The MCP implementation follows the scripts-first workflow.

## Libraries excluded, and why

Only `GenFORM.Lib` and `GenORDER.Lib` are exposed. The others are deliberately not:

| Library | Reason |
|---------|--------|
| `Informedica.GenSOLVER.Lib` | Pure mathematical solver; results only meaningful in the context of a full order — covered by GenORDER tools |
| `Informedica.GenUNITS.Lib` | Pure unit-of-measure utilities; not a domain service |
| `Informedica.GenCORE.Lib` | Core type definitions only |
| `Informedica.Utils.Lib` | Utility functions; not a domain service (its IO half is being split out, see [ADR-0022](0022-dependency-rule-and-effects.md)) |
| `Informedica.GenPRES.Server` | Already exposes the full API via `IServerApi`; MCP is an additional pathway, not a replacement |

## Dependencies

Two new external dependencies, both listed in `src/Informedica.MCP.Lib/paket.references`:

- `ModelContextProtocol` — the official .NET MCP SDK, maintained by the
  [modelcontextprotocol GitHub organization](https://github.com/modelcontextprotocol/csharp-sdk).
- `Microsoft.Extensions.Hosting` — the generic host the SDK's server builder is wired into
  (`McpServer.fs` opens it directly).

The decision is to pin exact versions in `paket.dependencies` rather than floating specifiers, so
builds stay reproducible, and to re-run a GitHub Advisory Database
check before each deliberate bump. `ModelContextProtocol` is pinned (`1.2.0`);
`Microsoft.Extensions.Hosting` is currently declared without a version constraint and so does not
yet follow this decision — `paket.lock` is what holds it steady today. No changes were required to
`GenFORM.Lib` or `GenORDER.Lib`.

## Notes

The tool surface, its wiring and its hosting are code, not documentation: see
`src/Informedica.MCP.Lib/` (`McpServer.fs`, `McpTools.GenForm.fs`, `McpTools.GenOrder.fs`)
and the standalone stdio host `src/Informedica.MCP.Server/Program.fs`.
