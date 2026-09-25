# CLAUDE.md

Read and follow the instructions in [AGENTS.md](AGENTS.md). This file adds what is specific to Claude Code.

## Required Reading List

- @AGENTS.md
- @.github/instructions/fsharp-coding.instructions.md
- @.github/instructions/fsharp-code-formatting.instructions.md
- @.github/instructions/commit-message.instructions.md

Read these when the task needs them, not at start-up:

- [DEVELOPMENT.md](DEVELOPMENT.md) for the build targets, Docker, release automation, IDE setup and the environment keys
- [CONTRIBUTING.md](CONTRIBUTING.md) for the pull request process
- [ADR-0001](docs/adr/0001-system-architecture.md) for the full dependency rule and its rationale
- [Core Domain Model](docs/domain/core-domain.md) and the other documents in `docs/domain/` for the domain model

## Session Startup

At the start of each session, check whether the FSI MCP server runs with `mcp__fsi-mcp__get_fsi_status`. If it does, use the MCP tools for all F# interactive work instead of `dotnet fsi`. The tools, the `#I` include-path rule and the restart rule after a DLL rebuild are in AGENTS.md under "Using the FSI MCP Server".

## Context Management

Before any auto-compact or when context usage approaches 70%, write a
decisions log to `.claude/docs/session-decisions.md` containing:

- Any architectural or design decisions made this session
- Approaches explicitly rejected and why
- Critical constraints discovered or confirmed
- Specific values, types, or function signatures that matter

Do this proactively without being asked.

## Plan Execution Rules

At the end of each plan step, append a brief summary of what was decided
or changed to `.claude/docs/session-log.md`. This ensures nothing critical
is lost if compaction occurs between steps.
