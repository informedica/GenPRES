# AGENTS.md

Instructions for AI coding agents working on the GenPRES repository. Make edits small, write the tests first in the script, and follow existing repository patterns.

> **⚠️ CRITICAL — SCRIPT-FIRST CODE POLICY ⚠️**
>
> **Do not edit source files (`.fs`) directly.** Every new feature, fix or change is written first in an F# Interactive script (`.fsx`), with its Expecto tests in the same script. An agent changes a source file only to migrate a script that passes its tests and that the user has reviewed, and only when the user asks for that migration. Test files under `tests/` are source files too: a test is written in the script and moves to `tests/` with the code it covers.
>
> **Allowed without a script:**
>
> - Adding, updating, or correcting **comments and documentation**
> - **Targeted refactoring of a single function** when explicitly requested by the user
> - **Client-side UI code** in `src/Informedica.GenPRES.Client/`, because Fable/Elmish UI code cannot be run in FSI scripts
>
> The UI exception stops at that project. `src/Informedica.GenPRES.Client.Core/` holds the client's pure code, its models, helpers, policies and state machines: plain F# over the shared contract, with no React, no Browser type and no Fable package, so it runs under Expecto and in FSI like any other library. Nothing there needs the exception, and so nothing there gets it.
>
> **Every change traces to an issue.** A pull request addresses an issue directly or carries out a step of an implementation plan, and an implementation plan is based on one or more issues. A migration script names in its opening comment the issue it addresses, or the implementation plan and the step it belongs to.
>
> **A migration script lives only as long as its migration.** The pull request that completes the migration deletes the script. Scripts that are tools rather than prototypes, such as `load.fsx` and the code generators, are not migration scripts and stay.
>
> This policy exists because GenPRES is a medical device software project. Unreviewed code changes to source files risk introducing unvalidated behavior into clinical medication workflows. The user is the sole gatekeeper for source file changes: outside the changes allowed without a script, no code reaches a source file without a tested script the user has reviewed.
>
> See the **Script-Based Development Workflow** section below for how to work within this constraint.

## Project Overview

GenPRES is a Clinical Decision Support System (CDSS) for medication prescribing, built entirely in F# using the SAFE Stack (Saturn, Azure, Fable, Elmish). It provides safe and efficient medication order entry, calculation, and validation for medical settings.

## Build, Run and Test

This repository holds more than one project file in the root, so plain `dotnet build` and `dotnet test` fail with MSB1011. Use the FAKE targets, or name the solution:

```bash
dotnet run build          # build the whole solution
dotnet run servertests    # run all Expecto tests
dotnet run                # start server and client with hot reload, http://localhost:5173
dotnet run list           # every target
dotnet test GenPRES.sln   # plain dotnet test, when a target does not fit
dotnet test tests/Informedica.GenORDER.Tests/   # one test project
```

The full targets table, Docker, release automation and the environment keys are in [DEVELOPMENT.md](DEVELOPMENT.md). Run the tests of a fresh checkout or worktree with `CI=true` when the G-Standaard files under `data/zindex` are absent.

## Key Code Locations

- F# libraries under `src/`, one test project per library under `tests/` (Expecto + FsCheck)
- Resource loading: `src/Informedica.GenFORM.Lib/Api.fs`; parsers in `Mapping.fs`, `Product.fs`, `DoseRuleData.fs`, `SolutionRule.fs`, `RenalRule.fs`
- Unit and BigRational helpers: `src/Informedica.GenUNITS.Lib/ValueUnit.fs`
- Client.Core by kind, one folder per namespace `Informedica.GenPRES.Client.Core.<Folder>`: `Models` (the code from Shared only the client uses), `Helpers`, `Policies`, `StateMachines` and `MachinePolicies`, in that compile order; the entry files `Lanes.fs`, `Client.fs` and `Trail.fs` at the project root, in the root namespace. Every file declares its namespace on its own line, followed by its types and modules, with the opens inside each module. Code opens the folder namespaces, never the root namespace, and reaches a root file through a module alias (`module Client = Informedica.GenPRES.Client.Core.Client`) or opens it by its full name. The test project mirrors the folders
- Sheet documentation: the `Data` records in `src/Informedica.GenFORM.Lib/Types.fs`, one per sheet with the column on each field; the column names are enforced by the `ColumnContract` tests in `tests/Informedica.GenFORM.Tests/Tests.fs`
- API contract between client and server: `src/Informedica.GenPRES.Shared/Api.fs` (Fable.Remoting)

`.gitignore` and `.dockerignore` are opt-in: a new file is excluded until you add a `!` line for it.

## Configuration and Resources

- Medication rules and constraints live in Google Spreadsheets, downloaded as CSV and parsed at runtime. `GENPRES_URL_ID` selects the spreadsheet; local cache files give offline access
- Sheets are read with `Web.getDataFromSheet dataUrlId "SheetName"`; mappers read columns by name through the `get` delegate (`let get = getColumn row in get "Generic"`) and parse with `BigRational.toBrs` / `getFloat`. Optional numeric columns use `getFloatOptionColumn` and `Option.bind BigRational.fromFloat`
- Resources are declared in the `ResourceRegistry` built by `Resources.defaultRegistry` (`Resources.fs`), a map from `ResourceKey` to `ResourceLoader`. Wrap a `unit -> Result<'T, Message list>` reader with `ofResult`; derive one resource from others with `derive` / `deriveWith` (dependencies are declared with `r.Get Keys.x` and resolved lazily, once). Callers reach the result through `IResourceProvider` (`Api.fs`)
- To change a sheet mapping: adjust the mapper (`Product.Reconstitution.parseReconstitution`, `DoseRuleData.parseDoseRuleData`, ...), update the `///` field comments on the matching `Data` record, and update the column list in the column-contract test
- IO and parsing functions return `GenFormResult<'T>` (a `Result`); use the `result` computation expression from FsToolkit.ErrorHandling. Every loader in the registry keeps returning `Result`

## Never Perform IO in a Top-Level `let` Value

F# packs every top-level `let` **value** of a file into one static initializer, and .NET caches a failed initializer for the life of the process. A value whose right-hand side reaches IO runs that IO the first time anything in the file is touched, and one failure poisons the whole file. Under Expecto this kills test discovery for the assembly: zero results, and the summary reads "0 failed" on a red build. Nesting in a sub-module does not help; the initializer is per file.

```fsharp
// BAD: a value. The IO runs in the file's static constructor.
let index = fetch () |> Result.defaultValue []

// GOOD: a function. Nothing runs until a caller asks.
let index () = fetch () |> Result.defaultValue []
```

The test: no parameters, not `lazy`, and the right-hand side reaches IO (network, file, `.env`, `Web.getDataFromSheet`, `Async.RunSynchronously`). Give it a `()` parameter or wrap it in `lazy`, keep the IO leaf returning a `Result`, and let the caller own the failure policy. Two idioms in the codebase do this right:

- **Memoized accessor**: `let medications : unit -> Drug.Drug list = memoizeN _medications` in `src/Informedica.NKF.Lib/WebSiteParser.fs`, and every BST table module in `src/Informedica.ZIndex.Lib/Zindex.fs`
- **Explicit `lazy`**: `let root = lazy (resolveRoot ())` with `let rootPath () = root.Value` in `src/Informedica.Utils.Lib/AppPath.fs`

It applies to test files too: wrap expensive or fallible fixtures in `lazy`, or guard them with `try/with` as `tests/Informedica.ZIndex.Tests/Tests.fs` does.

## Dependency Rule

Every project in `GenPRES.sln` belongs to one ring, and a project references only its own ring or a ring further in ([ADR-0001](docs/adr/0001-system-architecture.md), ring map in `scripts/DependencyRule.fsx`):

- **Core**: units, patient, solver, operational knowledge rules, orders, interactions. No call that reaches outside the process, no configuration reads; values are passed in.
- **Contract**: `GenPRES.Shared` alone. It references only itself, and only Presentation and Client may reference it, so the domain never depends on the wire shape.
- **Infrastructure**: the adapters (ZIndex, ZForm, NKF, FTK, the Google Sheets loaders), the agent runtime and the IO half of the utilities.
- **Presentation**: the server and the MCP host, the composition roots.
- **Client**: references the contract and other Client projects, nothing more.

Infrastructure and Presentation together are the only rings that touch the network, the filesystem, the environment, the clock and entropy, read a `GENPRES_*` setting, or declare an entry point. Effects enter the core as explicit parameters: a `Logger` record, a `now` value, a `newId` function, a function-valued resource such as `GStandProvider`. Only the composition roots construct loggers, providers and caches.

`scripts/CheckDependencyRule.fsx` enforces this as a separate CI step. Existing violations are listed in it as allowances; an allowance that no longer matches fails the run, so deleting code can fail it too. Run the script after any change to project references or IO.

## BigRational & ValueUnit Semantics

- All medication calculations use BigRational, for absolute precision. Respect the existing helpers in `Informedica.GenUnits.Lib` (`ValueUnit.singleWithUnit`, `ValueUnit.withUnit`, ...)
- `removeBigRationalMultiples` keeps the smallest positive representatives and removes later values that are integer multiples of a kept one: [1/3; 1/2; 1] keeps 1/2 and 1/3 and drops 1. Use `BigRational.isMultiple` when reasoning about multiples

## Comment Wording

Comments and documentation are read by someone who does not share what the writer had in mind while writing. The structure rules for comments are in the F# coding instructions; this section is about the wording, and it applies to every comment, doc comment and document in the repository.

- Write plain, complete English that a reader understands without the surrounding code or the conversation that produced it.
- Do not present a phrase that only makes sense in one situation as if it were a known, general fact. Shorthand that leans on context the reader cannot see ("its one value", "the count one") is not clear English; say what is meant in full ("a single value", "the value 1").
- When a statement holds only under a condition, state the condition.
- Write a value as a value ("the value 1", "1 or more"), not as a word used as a name.
- Repeat the noun instead of a pronoun when the pronoun could point at more than one thing.
- Use only established technical terms or plain words; do not coin a term.

## Testing Patterns

Tests use Expecto with Expecto.Flip, as in the F# coding instructions. Shared test scenarios live in `tests/Informedica.GenORDER.Tests/Scenarios.fs`: `pcmSupp` (paracetamol suppository), `amfo` (amphotericin B liposomal IV), `morfCont` (morphine continuous infusion), `pcmDrink` (paracetamol oral liquid), `cotrim` (cotrimoxazole), `tpn` / `tpnComplete` (parenteral nutrition), `fullMedication` (every field set).

## Script-Based Development Workflow

New functionality is prototyped in `.fsx` scripts and verified there; once the user has reviewed a script, its code and tests are migrated to the source and test projects. A single script can prototype functions of several libraries at once, because `load.fsx` loads the library's own source files with `#load` and the compiled DLLs of the libraries below it with `#r`.

### Infrastructure

Every library has a `Scripts/` directory with:

- `load.fsx`, which references the compiled DLLs of dependent libraries and `#load`s the library's own `.fs` files:

  ```fsharp
  #r "nuget: MathNet.Numerics.FSharp"
  #r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
  #load "../Types.fs"
  #load "../Variable.fs"
  ```

- development scripts (`Solver.fsx`, `MedicationYaml.fsx`, ...)

Rebuild first (`dotnet run build`) so `load.fsx` finds the DLLs.

### Workflow

1. Name the issue, or the implementation plan and step, in the script's opening comment
2. Start the script with `#I __SOURCE_DIRECTORY__` and `Environment.CurrentDirectory <- __SOURCE_DIRECTORY__`, so relative paths resolve
3. `#load "load.fsx"` for the library context; `#r "nuget: Expecto"` for packages
4. Copy only the functions you plan to change, not whole modules
5. Write Expecto tests in the same script for the change, before the change itself, and run them with `runTestsWithCLIArgs [] [||] tests`; existing suites can be loaded with `#load "../../../tests/Informedica.GenSOLVER.Tests/Tests.fs"`
6. Make the change by shadowing the module, and run the tests again until they pass. A shadowed module has the original's name and opens the original inside, so existing functions stay reachable through the same name

   ```fsharp
   module MedicationText =
       open Informedica.GenOrder.Lib.MedicationText

       let roundTrip med = med |> toString |> fromString

   let text = myMed |> MedicationText.toString          // original
   let back = myMed |> MedicationText.roundTrip         // new
   ```

7. Wait for the user's review; migrate only when the user asks for it

### Migration

A migration moves reviewed script code, and the tests that cover it, into the source and test projects:

- one pull request per issue or implementation plan step, within the 200-line limit on shipped code under `src/`; a larger script migrates over several plan steps
- the tests move to the matching test project and pass there (`dotnet run servertests`)
- the pull request that completes the migration deletes the script

### Using the FSI MCP Server

The [fsi-mcp-server](https://github.com/halcwb/fsi-mcp-server) keeps one FSI session alive across queries. Prefer it over `dotnet fsi` when it runs:

- `mcp__fsi-mcp__get_fsi_status`: is the server running
- `mcp__fsi-mcp__send_fsharp_code`: execute F# code (end statements with `;;`)
- `mcp__fsi-mcp__load_f_sharp_script`: load and execute an `.fsx` file
- `mcp__fsi-mcp__get_recent_fsi_events`: recent output and errors

FSI resolves `#load` from its **include path**, not from the current directory, and `SetCurrentDirectory` does not change that. Add the script's directory first:

```fsharp
#I "/absolute/path/to/script/directory";;
#load "load.fsx";;
```

`load_f_sharp_script` sends the statements one by one, so set `#I` before calling it too. The session is persistent: load each dependency once, since a type loaded twice conflicts (`FSI_0005.Types.gram` vs `FSI_0010.Types.gram`) and the server must then be restarted. A DLL loaded with `#r` cannot be unloaded, so after any build that changes a referenced DLL, ask the user to restart the FSI server before continuing; files loaded with `#load` are recompiled and need no restart.

Without the server, run scripts from their own directory: `cd src/Informedica.GenORDER.Lib/Scripts && dotnet fsi MedicationYaml.fsx --check`.

## Data Dependencies

- Production needs the proprietary medication cache files, which are not in the repository; the demo runs on the sample data that is
- The Google Spreadsheets are live configuration: an edit changes a running system after the next resource reload

## Safety and Documentation

Any change that affects dosing, rules, parsing or resource mapping must come with unit tests and a changelog entry, and, when sheet columns or their meaning change, with updated `///` field comments on the `Data` record and an updated column-contract test. Note a new external dependency or a change in deployment behaviour in CONTRIBUTING.md.

## AI/LLM Usage Policy

The script-first policy at the top of this file applies to every contributor using an AI coding tool, not only to agents. The human contributor reviews and verifies the script code before it is migrated, by hand or by an agent at the contributor's request, and discloses vibe-coded code in the pull request as described in [CONTRIBUTING.md](CONTRIBUTING.md#ai-assisted-contributions).

## Checklist for Automated Edits

- [ ] The pull request addresses an issue, or a step of an implementation plan based on one or more issues
- [ ] Source changes migrate a script that passes its tests and that the user reviewed, except the changes the policy allows without a script
- [ ] Small, focused change: a pull request changes at most 200 lines of shipped code, the files under `src/` that build into the application (`.fs`, `.sql`, project files); lines under `tests/`, scripts, docs and lock files are not counted, see CONTRIBUTING.md
- [ ] Unit tests added or updated; in a migration, the tests moved from the script
- [ ] `dotnet run servertests` passes for the affected projects
- [ ] The pull request that completes a migration deletes the migrated script
- [ ] `Data` record `///` comments and the column-contract test updated when sheet columns or semantics change
- [ ] Conventional commit message with scope and short description

## Session Rules

- **At the start of a session**, check whether the FSI MCP server runs (`mcp__fsi-mcp__get_fsi_status`). If it does, use the MCP tools for all F# interactive work instead of `dotnet fsi`; see "Using the FSI MCP Server" above.
- **Before the context is compacted**, or when it approaches 70% use, write a decisions log to `.claude/docs/session-decisions.md`: the design decisions made, the approaches rejected and why, the constraints found, and the values, types and signatures that matter. Do this without being asked.
- **At the end of each plan step**, append a short summary of what was decided or changed to `.claude/docs/session-log.md`, so nothing is lost if compaction happens between steps.

## Required Reading

Read at the start of every session (Claude Code loads them through the `@` lines):

- @.github/instructions/fsharp-coding.instructions.md
- @.github/instructions/fsharp-code-formatting.instructions.md
- @.github/instructions/commit-message.instructions.md

Read when the task needs them:

- [DEVELOPMENT.md](DEVELOPMENT.md) for the build targets, Docker, release automation, IDE setup and the environment keys
- [CONTRIBUTING.md](CONTRIBUTING.md) for the pull request process
- [ARCHITECTURE.md](ARCHITECTURE.md) and [ADR-0001](docs/adr/0001-system-architecture.md) for the architecture and the full dependency rule
- [Core Domain Model](docs/domain/core-domain.md) and the other documents in `docs/domain/` for the domain model
