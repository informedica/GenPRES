# CLAUDE.md

Read and follow the instructions in [AGENTS.md](AGENTS.md).

This file contains additional guidance specific to Claude Code (claude.ai/claude-code).

## Required Reading List

At the start of each session, read these documents for full project context.

### Project governance and workflow

- @AGENTS.md
- @DEVELOPMENT.md
- @CONTRIBUTING.md

### Coding standards

- @.github/instructions/fsharp-coding.instructions.md
- @.github/instructions/fsharp-code-formatting.instructions.md
- @.github/instructions/commit-message.instructions.md

### Domain documentation

- @docs/domain/core-domain.md
- @docs/domain/gensolver-from-orders-to-quantitative-solutions.md

### Architecture and data

@docs/adr/0001-system-architecture.md

## Session Startup

**At the start of each session**, automatically check the FSI MCP server status:

```
mcp__fsi-mcp__get_fsi_status
```

This establishes whether the F# Interactive MCP server is available for the session. If running, prefer using the MCP tools for all F# interactive work (see "Running FSI Scripts" below).

## Running FSI Scripts

**IMPORTANT:** When running F# code interactively, always check if the FSI MCP server is available first using `mcp__fsi-mcp__get_fsi_status`. If the server is running, prefer using the MCP tools over `dotnet fsi`:

- `mcp__fsi-mcp__get_fsi_status` — Check if the server is running
- `mcp__fsi-mcp__send_fsharp_code` — Execute F# code (end statements with `;;`)
- `mcp__fsi-mcp__load_f_sharp_script` — Load and execute `.fsx` script files
- `mcp__fsi-mcp__get_recent_fsi_events` — View recent FSI output and errors

The MCP server provides a persistent FSI session with real-time output, which is more convenient than running `dotnet fsi` via bash.

**Loading scripts via MCP:** FSI's `#load` directive resolves relative paths from its *include path*, **not** from `System.IO.Directory.GetCurrentDirectory()`. When loading scripts via MCP, always start by adding the script's directory to FSI's include path using `#I`:

```fsharp
// Step 1: Set the include path to the script's directory
#I "/absolute/path/to/script/directory";;

// Step 2: Now relative #load paths resolve correctly
#load "../Types.fs";;
#load "load.fsx";;
```

**Important:**
- `System.IO.Directory.SetCurrentDirectory()` does **not** affect `#load` path resolution — you must use `#I`
- The MCP `load_f_sharp_script` tool sends script statements to FSI individually, so `#load` directives inside scripts also resolve from FSI's include path. Set `#I` before calling `load_f_sharp_script`
- Scripts should include `#I __SOURCE_DIRECTORY__` at the top so they work both when run via `dotnet fsi` and when loaded after manually setting `#I` via MCP
- The FSI session is persistent — types loaded multiple times create conflicts (e.g., `FSI_0005.Types.gram` vs `FSI_0010.Types.gram`). Load dependencies once per session. If conflicts occur, the FSI server must be restarted
- **DLL reference changes require a manual restart.** Once a DLL is loaded via `#r`, the .NET runtime cannot unload it. If you rebuild a referenced DLL (e.g., after `dotnet run build`), the FSI session will still use the old version. Reloading source files via `#load` does not have this problem. **Agent action:** After any build that changes referenced DLLs, prompt the user to manually restart the FSI MCP server before continuing with FSI work

**Fallback:** If the FSI MCP server is not available, FSI scripts must be run from their directory because they use relative paths:

```bash
# CORRECT - change to script directory first
cd src/Informedica.GenORDER.Lib/Scripts
dotnet fsi Tests.fsx
dotnet fsi Medication.fsx

# INCORRECT - will fail with path errors
dotnet fsi src/Informedica.GenORDER.Lib/Scripts/Tests.fsx
```

## Script-Only Development

The script-only code policy in [AGENTS.md](AGENTS.md) applies. As a reminder:

1. **NEVER write new code in `.fs` source files** — Only work in `.fsx` script files
2. **Use `Scripts/` directories** — Each library has a `Scripts/` folder for development
3. **Leave migration to the user** — They will review and move verified code to source files

For the full workflow, module shadowing pattern, and testing in scripts, see the **Script-Based Development Workflow** section in [AGENTS.md](AGENTS.md).

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
