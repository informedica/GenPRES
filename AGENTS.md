# AGENTS.md

Instructions for AI coding agents working on the GenPRES repository. Make edits small, test-driven, and follow existing repository patterns.

> **⚠️ CRITICAL — SCRIPT-ONLY CODE POLICY ⚠️**
>
> **DO NOT write new code in source files (`.fs`).** All new features, fixes, enhancements, and experiments MUST be implemented exclusively in F# Interactive script files (`.fsx`) in the `Scripts/` directories. The user will review your work and decide what to migrate to source files.
>
> **Allowed changes to `.fs` source files:**
>
> - Adding, updating, or correcting **comments and documentation**
> - **Targeted refactoring of a single function** when explicitly requested by the user
> - **Client-side UI code** in `src/Informedica.GenPRES.Client/` — this is the only exception, because Fable/Elmish UI code cannot be run in FSI scripts
>
> **NOT allowed in other `.fs` source files:**
>
> - Adding new functions or modules
> - Implementing new features or bug fixes
> - Any code change not explicitly requested as a source-file edit
>
> This policy exists because GenPRES is a medical device software project. Unreviewed code changes to source files risk introducing unvalidated behavior into clinical medication workflows. The user is the sole gatekeeper for source file changes.
>
> See the **Script-Based Development Workflow** section below for how to work within this constraint.

## Project Overview

GenPRES is a Clinical Decision Support System (CDSS) for medication prescribing, built entirely in F# using the SAFE Stack (Saturn, Azure, Fable, Elmish). It provides safe and efficient medication order entry, calculation, and validation for medical settings.

## Quick Start — Build, Run & Test

### Prerequisites

- **.NET SDK**, **Node.js**, and **npm**

For the canonical list of supported versions, see the **Toolchain Requirements** section in [`DEVELOPMENT.md`](DEVELOPMENT.md#toolchain-requirements). For environment variables, see [`DEVELOPMENT.md`](DEVELOPMENT.md#environment-configuration).

### Build and Run

**IMPORTANT:** This repository contains multiple projects. Always specify the solution file:

```bash
# CORRECT - build the entire solution
dotnet run build
# CORRECT - run the server tests
dotnet run servertests

# INCORRECT - will fail with "more than one project" error
dotnet build
dotnet test
```

- `dotnet run` - Start full application (server + client with hot reload)
- `dotnet run list` - Show all available build targets
- `dotnet run Build` - Build the whole solution (libraries, server, tests, client `.fsproj`); no npm involved
- `dotnet run ServerBuild` - Build only the server and the libraries it depends on.
- `dotnet run ClientBuild` - Build the client to browser output (Fable, then a production Vite bundle). Runs `npm ci` first
- `dotnet run Bundle` - Create production bundle
- `dotnet run CheckVersions` - Proves that every project shipped in GenPRES.sln reports the same version as the repo-root Directory.Build.props
- `dotnet run Clean` - Clean build artifacts
- `dotnet run DockerBuild` - Builds the Docker image, version-labelled from `Directory.Build.props`
- `dotnet run DockerRun` - Runs a Docker container
- `dotnet run Format` - Uses Fantomas to format F# code
- `dotnet run MarkdownLint` - Runs the mark down linter
- `dotnet run RestoreClient` - Runs the client npm restore process
- Access the application at `http://localhost:5173`

### Testing

- `dotnet run ServerTests` - Run all F# unit tests using Expecto
- `dotnet run TestHeadless` - Run tests in headless mode
- `dotnet run WatchTests` - Run tests in watch mode
- `dotnet test GenPRES.sln` - Alternative way to run all tests

Individual library tests:

```bash
dotnet test tests/Informedica.GenSOLVER.Tests/
dotnet test tests/Informedica.GenORDER.Tests/
dotnet test tests/Informedica.GenUNITS.Tests/
# ... etc for other test projects
```

### Code Quality

- `dotnet run Format` - Format F# code using Fantomas

### Docker

`GENPRES_URL_ID` is required at server startup, in both demo and production mode — not just for admin operations. The image also sets `GENPRES_PROD=1` by default, so the server refuses to start unless `GENPRES_PASSWORD` is also set to at least 16 characters — it's not purely an admin-operations toggle either. Neither the proprietary URL ID nor the password is baked into the image; inject both at container runtime, ideally via a Docker / Kubernetes secret. For local testing without production credentials, use the public demo sheet ID documented in `.env.example`.

- `dotnet run DockerBuild` - Build the image, labelled with the version from the root `Directory.Build.props`. Override the image name with `DOCKER_IMAGE` (default `ghcr.io/informedica/genpres`), cross-build a different platform with `DOCKER_PLATFORM`.
- `dotnet run DockerRun` - Run the built image, reading `GENPRES_URL_ID`/`GENPRES_PASSWORD` from the current environment (source `.env` first) and failing fast if either is unset.
- Equivalent manual commands: `docker build -t ghcr.io/informedica/genpres .` / `docker run -it -p 8080:8085 -e GENPRES_URL_ID="your_url_id" -e GENPRES_PASSWORD="your_admin_password" ghcr.io/informedica/genpres`

## Key Code Locations

- F# libraries under `src/`
- Tests: `tests/` (uses Expecto + FsCheck).
- Resource loading: `src/Informedica.GenForm.Lib/Api.fs`
- Resource parsers: `Mapping.fs`, `Product.fs`, `DoseRuleData.fs`, `SolutionRule.fs`, `RenalRule.fs`
- Unit and BigRational helpers: `src/Informedica.GenUnits.Lib/ValueUnit.fs`
- Sheet documentation: the `Data` record types in `src/Informedica.GenFORM.Lib/Types.fs` (one record per sheet, columns documented on the fields), with the column names enforced by the `ColumnContract` tests in `tests/Informedica.GenFORM.Tests/Tests.fs`

**Important:** an opt-in strategy is used in the `.gitignore` file — you have to specifically define what should be included instead of the other way around!
The same applies to the docker ignore file.

## Configuration Architecture

- All medication rules and constraints (currently) stored in Google Spreadsheets
- Downloaded as CSV and parsed dynamically
- `GENPRES_URL_ID` environment variable controls which spreadsheet to use
- Local cache files provide offline medication data access

## Communication Pattern

- Client-server communication via Fable.Remoting (type-safe RPC)
- API contracts defined in `src/Informedica.GenPRES.Shared/Api.fs`
- Server processes medication calculations and returns validated results

## Resource Loading Pattern

- Sheet specs live in code: each sheet has a record in the `Data` module of `src/Informedica.GenFORM.Lib/Types.fs`, whose XML summary names the sheet and the parser and whose field comments carry the column name (where it differs), unit, separator and boolean spelling.
- Check that record — and the declared column lists in `DoseRuleToDataTests.ColumnContract` (`tests/Informedica.GenFORM.Tests/Tests.fs`) — for expected sheet and column names.
- Resources are loaded from Google Sheets via `Web.getDataFromSheet dataUrlId "SheetName"`.
- Mapping helper functions use `Csv.getStringColumn` / `Csv.getFloatOptionColumn` and call getString/getFloat-style delegates.
- The central `ResourceConfig` (in `Api.fs`) expects functions returning `GenFormResult<'T>` (alias for `Result<'T, Message list>`). Use the `*Result` variants where present (e.g., `Mapping.getRouteMapping` or `Mapping.getRouteMappingResult`) and wrap with `delay` when the signature expects a `unit -> GenFormResult<_>`.
- To add/modify sheet mappings: adjust the mapper in the corresponding module (e.g., `Product.Reconstitution.parseReconstitution`, `DoseRuleData.parseDoseRuleData`), update the field comments on the matching `Data` record, and update the declared column list in the column-contract test.
- Update the mapper to read columns by name using the `get` delegate (e.g., `let get = getColumn row in get "Generic"`), parse with `BigRational.toBrs` / `getFloat` as appropriate.
- If adding optional numeric columns, use `getFloatOptionColumn` and `Option.bind BigRational.fromFloat`.

## Result and Error Handling

- IO and parsing functions should return `GenFormResult<'T>` (i.e., Result). Use `FsToolkit.ErrorHandling.ResultCE` computation expression for readability (`result { let! x = ... }`).
- When editing `ResourceConfig` or callers, make sure to handle `Result` values consistently; use `Result.bind`, CE, or `delay` for unit-returning getters.

## BigRational & ValueUnit Semantics

- BigRational operations are used broadly for dosing math. Respect existing helpers in `Informedica.GenUnits.Lib`.
- `removeBigRationalMultiples` semantics: it keeps the smallest positive BigRational representatives and removes later values that are integer multiples of a previously kept value. Example: [1/3; 1/2; 1] → keep 1/2 and 1/3 (both non-multiples of each other), but if 1/2 and 1 are present, keep 1/2 and remove 1 (1 is multiple of 1/2).
- Use `BigRational.isMultiple` when reasoning about integer multiples.
- Prefer using existing helpers like `ValueUnit.singleWithUnit`, `ValueUnit.withUnit`, etc., when manipulating units.
- Use **BigRational** for all medication calculations (absolute precision).
- Use `[<RequireQualifiedAccess>]` on DUs and modules.

## Testing Patterns

All tests use Expecto with Expecto.Flip for fluent assertions:

```fsharp
open Expecto
open Expecto.Flip

test "example test" {
    actual
    |> Expect.equal "should match expected" expected
}
```

### Test Scenarios

Test scenarios are defined in `tests/Informedica.GenORDER.Tests/Scenarios.fs` and include:

- `pcmSupp` - Paracetamol suppository
- `amfo` - Amphotericin B liposomal IV
- `morfCont` - Morphine continuous infusion
- `pcmDrink` - Paracetamol oral liquid
- `cotrim` - Cotrimoxazole
- `tpn` / `tpnComplete` - Total parenteral nutrition
- `fullMedication` - Fully populated medication (all fields set)

## Common Errors and Solutions

### "Specify which project or solution file to use"

```text
MSBUILD : error MSB1011: Specify which project or solution file to use
because this folder contains more than one project or solution file.
```

**Solution:** Always specify `GenPRES.sln`:

```bash
dotnet build GenPRES.sln
dotnet test GenPRES.sln
```

### FSI Script Path Errors

If FSI scripts fail to load dependencies, ensure you're running from the script's directory:

```bash
cd src/Informedica.GenORDER.Lib/Scripts
dotnet fsi Tests.fsx
```

### DLL Not Found

If FSI scripts fail because DLLs are not found, rebuild the solution first:

```bash
dotnet build GenPRES.sln
```

## Script-Based Development Workflow

**IMPORTANT: All new code MUST be written in `.fsx` script files only — never in `.fs` source files.** The user will review and migrate verified code to the codebase. See the critical policy at the top of this document.

GenPRES uses an FSI script-based workflow for safely implementing new functionality in a mature ("brown-field") codebase. Instead of modifying production source files directly, you copy or shadow existing code into `.fsx` scripts, experiment and test interactively, and only migrate verified code back to the codebase.

### Real-World Example: Cross-Project Feature in a Single Script

Commit `d51252c` added a "pick nearest higher else lower component quantity" feature that ultimately touched 3 libraries and 7 source files (`Array.fs`, `ValueUnit.fs`, `OrderVariable.fs`, `Order.fs`, `OrderProcessor.fs`). But it was **prototyped first in a single script** — `src/Informedica.GenUNITS.Lib/Scripts/Api.fsx`:

```fsharp
#load "load.fsx"                          // loads GenUnits source files + compiled Utils DLL

open Informedica.GenUnits.Lib

// 1. Prototype a helper that belongs in Utils.Lib
module Array =
    let inline pickNearestHigherElseLower target xs =
        if Array.isEmpty xs then invalidArg "xs" "Array cannot be empty"
        let ys = xs |> Array.sort
        match ys |> Array.tryFind (fun x -> x >= target) with
        | Some x -> x                   // smallest value >= target
        | None -> ys[ys.Length - 1]     // no higher value: take highest lower

// 2. Prototype a ValueUnit function that uses the Array helper above
module ValueUnit =
    let pickNearestHigherElseLower (target: ValueUnit) (candidates: ValueUnit) =
        if candidates |> ValueUnit.isEmpty then candidates
        elif candidates |> ValueUnit.eqsGroup target |> not then candidates
        else
            candidates
            |> ValueUnit.toBase
            |> ValueUnit.applyToValue (fun brs1 ->
                target
                |> ValueUnit.getBaseValue
                |> Array.tryExactlyOne
                |> Option.map (fun br ->
                    [| brs1 |> Array.pickNearestHigherElseLower br |]
                )
                |> Option.defaultValue brs1
            )
            |> ValueUnit.toUnit
```

Because `load.fsx` loads the GenUnits source files via `#load` and references the compiled Utils DLL via `#r`, you can prototype functions from **multiple libraries** in one interactive session. Once the logic is verified in FSI, the code is migrated to the appropriate source files across projects.

### Infrastructure

Every library has a `Scripts/` directory containing:

- `load.fsx` — Bootstrap script that loads compiled DLLs from dependent libraries and `#load`s the library's own `.fs` source files. This gives FSI access to the full library context.
- Development scripts (e.g., `Solver.fsx`, `Medication.fsx`, `Tests.fsx`) — Working scripts for experimentation and testing.

Example `load.fsx` pattern:

```fsharp
#r "nuget: MathNet.Numerics.FSharp"
#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#load "../Types.fs"
#load "../Variable.fs"
#load "../Solver.fs"
// ... etc
```

### Workflow

1. **Set the current directory** — Always start with `Environment.CurrentDirectory <- __SOURCE_DIRECTORY__` so relative paths resolve correctly.
2. **Load project context** — Use `#load "load.fsx"` to load all dependencies.
3. **Reference NuGet packages inline** — Use `#r "nuget: Expecto, 9.0.4"` for test frameworks or other packages.
4. **Copy only the code you need** — Don't drag entire modules; start with just the functions you plan to modify.
5. **Modify and extend** — Refactor, optimize, or add new features in the script.
6. **Write tests in the same script** — Verify your changes with inline Expecto tests.
7. **Reuse existing test suites** — Load test files from the `tests/` directory via `#load` and run them against your modified code.
8. **Migrate when confident** — Once verified, move the improved code back into the source files.

### Module Shadowing Pattern

Shadow an existing module to extend it with new functions while keeping all original functions accessible:

```fsharp
#load "load.fsx"

open Informedica.GenOrder.Lib

// Shadow the Medication module to add new functions
module Medication =
    // Open the original module - all existing functions become available
    open Informedica.GenOrder.Lib.Medication

    // Add new function
    let fromString (s: string) : Result<Medication, string list> =
        // implementation...

    // Existing functions like toString, template, toOrderDto are now
    // automatically available as Medication.toString, etc.
```

**NOTE** The module has the same name as the original (`Medication`), but because it's defined in the script, it shadows the original module. By opening the original module inside the new one, you bring all existing functions into scope, allowing you to call them as if they were part of the new module.

This allows calling both new and existing functions through the same module name:

```fsharp
let text = myMed |> Medication.toString       // original function
let parsed = text |> Medication.fromString    // new function
```

### Testing in Scripts

Write Expecto tests directly in the script file:

```fsharp
#r "nuget: expecto"

open Expecto
open Expecto.Flip

let tests =
    testList "feature tests" [
        test "roundtrip works" {
            let original = Scenarios.pcmSupp
            let text = original |> Medication.toString |> String.concat "\n"
            match text |> Medication.fromString with
            | Error errs -> failwith $"Parse failed: {errs}"
            | Ok parsed ->
                parsed.Id |> Expect.equal "Id matches" original.Id
        }
    ]

runTestsWithCLIArgs [] [||] tests
```

You can also reuse existing tests from the test projects:

```fsharp
// Load existing tests directly
#load "../../../tests/Informedica.GenSOLVER.Tests/Tests.fs"

open Informedica.GenSolver.Tests
// Run existing test suites against your modified code
```

### Using the FSI MCP Server

The [fsi-mcp-server](https://github.com/halcwb/fsi-mcp-server) provides a persistent FSI session accessible via MCP tools. This enables AI-assisted interactive F# development without restarting FSI between queries.

**Available MCP tools:**

- `mcp__fsi-mcp__get_fsi_status` — Check if the server is running
- `mcp__fsi-mcp__send_fsharp_code` — Execute F# code (end statements with `;;`)
- `mcp__fsi-mcp__load_f_sharp_script` — Load and execute `.fsx` script files
- `mcp__fsi-mcp__get_recent_fsi_events` — View recent FSI output and errors

**Path resolution strategy:**

FSI's `#load` directive resolves relative paths from its *include path*, **not** from `System.IO.Directory.GetCurrentDirectory()`. When loading scripts via MCP, always start by adding the script's directory to FSI's include path using `#I`:

```fsharp
// Step 1: Set the include path to the script's directory
#I "/absolute/path/to/script/directory";;

// Step 2: Now relative #load paths resolve correctly
#load "../Types.fs";;
#load "../Utils.fs";;
#load "load.fsx";;
```

**Important:**

- `System.IO.Directory.SetCurrentDirectory()` does **not** affect `#load` path resolution — you must use `#I`
- The MCP `load_f_sharp_script` tool sends script statements to FSI individually, so `#load` directives inside scripts also resolve from FSI's include path. Set `#I` before calling `load_f_sharp_script`
- Scripts should include `#I __SOURCE_DIRECTORY__` at the top so they work both when run via `dotnet fsi` (where `__SOURCE_DIRECTORY__` is the script's directory) and when loaded after manually setting `#I` via MCP
- The FSI session is persistent — types loaded multiple times create conflicts (e.g., `FSI_0005.Types.gram` vs `FSI_0010.Types.gram`). Load dependencies once per session. If conflicts occur, the FSI server must be restarted
- **DLL reference changes require a manual restart.** Once a DLL is loaded via `#r`, the .NET runtime cannot unload it. If you rebuild a referenced DLL (e.g., after `dotnet run build`), the FSI session will still use the old version. Reloading source files via `#load` does not have this problem — they are recompiled each time. **Agent action:** After any build that changes referenced DLLs, prompt the user to manually restart the FSI MCP server before continuing with FSI work

### Tips

- **Partial evaluation** — Select part of a script and send it to FSI to validate small functions without reloading everything.
- **Keep FSI sessions alive** — Build up state interactively rather than restarting FSI each time.
- **Modularize scripts** — Break scripts into logical regions (helpers, refactored code, tests) with comments for easier navigation.
- **Rebuild before scripting** — Run `dotnet build GenPRES.sln` first so `load.fsx` can find the compiled DLLs.

## Data Dependencies

- Production requires proprietary medication cache files (not in repository)
- Demo version uses sample medication data included in repository
- Google Spreadsheets contain live configuration — changes affect running systems

## Safety, MDR and Documentation

- This project targets clinical medication workflows. Any change that affects dosing, rules, parsing, or resource mapping must include: unit tests, a changelog entry, and — if spreadsheet columns or semantics changed — updated field comments on the corresponding `Data` record in `GenFORM.Lib/Types.fs` plus an updated column-contract test.
- Add notes to CONTRIBUTING.md if the change introduces a new external dependency or changes deployment behavior.

## AI/LLM Usage Policy

This policy applies to **all contributors**, not just AI agents.

> **LLMs must not be given direct write access to `.fs` source files**, except for client-side UI code in `src/Informedica.GenPRES.Client/`.

Contributors using AI coding tools (GitHub Copilot, Claude, Cursor, Warp AI, etc.) must route all non-UI code through `.fsx` scripts first, following the script-based development workflow described above. The human contributor is responsible for reviewing, verifying, and manually migrating script code into source files.

This restriction exists because GenPRES is a medical device software project. Allowing LLMs to directly modify source files risks introducing unvalidated behavior into clinical medication workflows. Human review of every source file change is a safety requirement.

Contributors must also disclose when code submitted in a pull request is **vibe coded** — see [CONTRIBUTING.md](CONTRIBUTING.md#ai-assisted-contributions) for the definition and disclosure requirements.

## Checklist for Automated Edits

- [ ] Small, focused change with < 300 LOC modified when possible.
- [ ] Add or update unit tests covering the change.
- [ ] Ensure `dotnet run servertests` passes locally for affected projects.
- [ ] Update the `Data` record comments and the column-contract test if spreadsheet column names or semantics change.
- [ ] Use conventional commit message with scope and short description.

## Related Documentation

- Coding standards: [F# Coding Instructions](.github/instructions/fsharp-coding.instructions.md)
- Code formatting: [F# Code Formatting](.github/instructions/fsharp-code-formatting.instructions.md)
- Commit conventions: [Commit Message Instructions](.github/instructions/commit-message.instructions.md)
- Architecture: [ARCHITECTURE.md](ARCHITECTURE.md)
- Development setup: [DEVELOPMENT.md](DEVELOPMENT.md)
- Contributing: [CONTRIBUTING.md](CONTRIBUTING.md)
- Domain model: [Core Domain Model](docs/domain/core-domain.md)
