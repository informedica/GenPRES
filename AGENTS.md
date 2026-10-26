# AGENTS.md

Instructions for AI coding agents working on the GenPRES repository. Make edits small, write the tests first in the script, and follow existing repository patterns.

> **⚠️ CRITICAL — SCRIPT-FIRST CODE POLICY ⚠️**
>
> **Do not edit source files (`.fs`) directly.** Every feature, fix or change is written first in an F# Interactive script (`.fsx`), with its Expecto tests in the same script. An agent changes a source file only to migrate a script that passes its tests and that the user has reviewed, and only when the user asks for that migration. Files under `tests/` are source files too: a test is written in the script and moves to `tests/` with the code it covers.
>
> **Allowed without a script:**
>
> - Adding, updating or correcting **comments and documentation**
> - **Targeted refactoring of a single function** when explicitly requested by the user
> - **Client-side UI code** in `src/Informedica.GenPRES.Client/`, because Fable/Elmish UI code cannot run in FSI. The exception stops there: `src/Informedica.GenPRES.Client.Core/` is plain F# that runs under Expecto and in FSI, so it goes through a script like any other library
> - **Deleting code**, and the call-site changes a deletion forces, such as a parameter that goes with the function that read it. A deletion adds no behaviour to prototype; the tests that covered the deleted code go with it
>
> **Every change traces to an issue.** A pull request addresses an issue, or a step of an implementation plan that is based on one or more issues. A migration script names the issue, or the plan and step, in its opening comment.
>
> **A migration script lives only as long as its migration.** The pull request that completes the migration deletes the script. Tool scripts such as `load.fsx` and the code generators stay.
>
> GenPRES is medical device software: unreviewed source changes risk unvalidated behavior in clinical medication workflows. The user is the sole gatekeeper for source file changes. The policy applies to every contributor using an AI coding tool; vibe-coded code is disclosed in the pull request as described in [CONTRIBUTING.md](CONTRIBUTING.md#ai-assisted-contributions).

## Project Overview

GenPRES is a Clinical Decision Support System for medication prescribing, built in F# on the SAFE Stack (Saturn, Azure, Fable, Elmish): medication order entry, calculation and validation.

## Build, Run and Test

More than one project file sits in the root, so plain `dotnet build` and `dotnet test` fail with MSB1011. Use the FAKE targets, or name the solution:

```bash
dotnet run build          # build the whole solution
dotnet run servertests    # run all Expecto tests
dotnet run                # start server and client with hot reload, http://localhost:5173
dotnet run format         # format the F# code with Fantomas, as CI checks it
dotnet run testheadless   # all tests through plain dotnet test
dotnet run list           # every target
dotnet test GenPRES.sln   # plain dotnet test, when a target does not fit
dotnet test tests/Informedica.GenORDER.Tests/   # one test project
```

Toolchain, the full targets table, Docker, release automation and the environment keys: [DEVELOPMENT.md](DEVELOPMENT.md). Run the tests of a fresh checkout or worktree with `CI=true` when the G-Standaard files under `data/zindex` are absent.

## Key Code Locations

- F# libraries under `src/`, one test project per library under `tests/` (Expecto + FsCheck)
- Resource loading: `src/Informedica.GenFORM.Lib/Api.fs`; parsers in `Mapping.fs`, `Product.fs`, `DoseRuleData.fs`, `SolutionRule.fs`, `RenalRule.fs`
- Unit and BigRational helpers: `src/Informedica.GenUNITS.Lib/ValueUnit.fs`
- Client.Core: one folder per namespace `Informedica.GenPRES.Client.Core.<Folder>`, in compile order `Models` (the code from Shared only the client uses), `Helpers`, `Policies`, `StateMachines`, `MachinePolicies`; the entry files `Lanes.fs`, `Client.fs` and `Trail.fs` at the project root in the root namespace. A file declares its namespace on its own line, then its types and modules, with the opens inside each module. Code opens the folder namespaces, never the root namespace, and reaches a root file through a module alias (`module Client = Informedica.GenPRES.Client.Core.Client`) or its full name. The test project mirrors the folders
- Sheet documentation: the `Data` records in `src/Informedica.GenFORM.Lib/Types.fs`, one per sheet, the column on each field; the `ColumnContract` tests in `tests/Informedica.GenFORM.Tests/Tests.fs` enforce the column names
- API contract between client and server: `src/Informedica.GenPRES.Shared/Api.fs` (Fable.Remoting)

`.gitignore` and `.dockerignore` are opt-in: a new file is excluded until you add a `!` line for it.

## Configuration and Resources

- Medication rules and constraints live in Google Spreadsheets, downloaded as CSV and parsed at runtime. `GENPRES_URL_ID` selects the spreadsheet; local cache files give offline access
- Sheets are read with `Web.getDataFromSheet dataUrlId "SheetName"`; mappers read columns by name through the `get` delegate (`let get = getColumn row in get "Generic"`) and parse with `BigRational.toBrs` / `getFloat`. Optional numeric columns use `getFloatOptionColumn` and `Option.bind BigRational.fromFloat`
- Resources are declared in the `ResourceRegistry` built by `Resources.defaultRegistry` (`Resources.fs`), a map from `ResourceKey` to `ResourceLoader`. Wrap a `unit -> Result<'T, Message list>` reader with `ofResult`; derive one resource from others with `derive` / `deriveWith` (dependencies declared with `r.Get Keys.x`, resolved lazily, once). Callers reach the result through `IResourceProvider` (`Api.fs`)
- To change a sheet mapping: adjust the mapper (`Product.Reconstitution.parseReconstitution`, `DoseRuleData.parseDoseRuleData`, ...), the `///` field comments on the matching `Data` record, and the column list in the column-contract test

## Never Perform IO in a Top-Level `let` Value

F# packs every top-level `let` **value** of a file into one static initializer, and .NET caches a failed initializer for the life of the process. A value whose right-hand side reaches IO runs it the first time anything in the file is touched, and one failure poisons the whole file; under Expecto that kills test discovery for the assembly, with "0 failed" on a red build. A sub-module does not help; the initializer is per file.

```fsharp
let index = fetch () |> Result.defaultValue []      // BAD: runs in the static constructor
let index () = fetch () |> Result.defaultValue []   // GOOD: runs when a caller asks
```

The test: no parameters, not `lazy`, and the right-hand side reaches IO (network, file, `.env`, `Web.getDataFromSheet`, `Async.RunSynchronously`). Give it a `()` parameter or wrap it in `lazy`, keep the IO leaf returning a `Result`, and let the caller own the failure policy. Idioms in the codebase: the memoized accessor `let medications : unit -> Drug.Drug list = memoizeN _medications` in `src/Informedica.NKF.Lib/WebSiteParser.fs`, and `let root = lazy (resolveRoot ())` with `let rootPath () = root.Value` in `src/Informedica.Utils.Lib/AppPath.fs`. In test files, wrap expensive or fallible fixtures in `lazy` or guard them with `try/with`, as `tests/Informedica.ZIndex.Tests/Tests.fs` does.

## Dependency Rule

Every project belongs to one ring and references only its own ring or a ring further in ([ADR-0001](docs/adr/0001-system-architecture.md), ring map in `scripts/DependencyRule.fsx`):

- **Core**: units, patient, solver, operational knowledge rules, orders, interactions. Nothing reaches outside the process, no configuration reads; values are passed in
- **Contract**: `GenPRES.Shared` alone; only Presentation and Client may reference it
- **Infrastructure**: the adapters (ZIndex, ZForm, NKF, FTK, the Google Sheets loaders), the agent runtime, the IO half of the utilities
- **Presentation**: the server and the MCP host, the composition roots
- **Client**: references the contract and other Client projects only

Only Infrastructure and Presentation touch the network, filesystem, environment, clock and entropy, read a `GENPRES_*` setting or declare an entry point. Effects enter the core as parameters: a `Logger` record, a `now` value, a `newId` function, a function-valued resource such as `GStandProvider`. Only the composition roots construct loggers, providers and caches.

`scripts/CheckDependencyRule.fsx` enforces this in CI. Existing violations are listed in it as allowances, and an allowance that no longer matches fails the run, so deleting code can fail it too. Run the script after any change to project references or IO.

## BigRational & ValueUnit Semantics

- All medication calculations use BigRational. Use the existing helpers in `Informedica.GenUnits.Lib` (`ValueUnit.singleWithUnit`, `ValueUnit.withUnit`, ...)
- `removeBigRationalMultiples` keeps the smallest positive representatives and removes later values that are integer multiples of a kept one: [1/3; 1/2; 1] keeps 1/2 and 1/3 and drops 1. Use `BigRational.isMultiple` when reasoning about multiples

## Testing Patterns

Expecto with Expecto.Flip, as in the F# coding instructions. Shared scenarios in `tests/Informedica.GenORDER.Tests/Scenarios.fs`: `pcmSupp` (paracetamol suppository), `amfo` (amphotericin B liposomal IV), `morfCont` (morphine continuous infusion), `pcmDrink` (paracetamol oral liquid), `cotrim` (cotrimoxazole), `tpn` / `tpnComplete` (parenteral nutrition), `fullMedication` (every field set).

## Script-Based Development Workflow

Every library has a `Scripts/` directory with `load.fsx`, which `#r`s the compiled DLLs of the libraries below and `#load`s the library's own `.fs` files, and development scripts (`Solver.fsx`, `MedicationYaml.fsx`, ...). One script can therefore add functions to several libraries at once; a change to an existing function is seen only by callers in the script. Rebuild first (`dotnet run build`) so `load.fsx` finds the DLLs.

```fsharp
#r "nuget: MathNet.Numerics.FSharp"
#r "../../Informedica.Utils.Lib/bin/Debug/net10.0/Informedica.Utils.Lib.dll"
#load "../Types.fs"
#load "../Variable.fs"
```

### Workflow

1. Name the issue, or the implementation plan and step, in the script's opening comment
2. Start with `#I __SOURCE_DIRECTORY__` and `Environment.CurrentDirectory <- __SOURCE_DIRECTORY__`, so relative paths resolve under `dotnet fsi` and through the FSI MCP server
3. `#load "load.fsx"` for the library context; `#r "nuget: Expecto"` for packages
4. Copy only the functions you plan to change, not whole modules
5. Write the Expecto tests for the change in the same script, before the change, and run them with `runTestsWithCLIArgs [] [||] tests`; load an existing suite with `#load "../../../tests/Informedica.GenSOLVER.Tests/Tests.fs"`
6. Make the change by shadowing the module, and run the tests until they pass. A shadowed module has the original's name and opens the original inside, so existing functions stay reachable through the same name:

   ```fsharp
   module MedicationText =
       open Informedica.GenOrder.Lib.MedicationText

       let roundTrip med = med |> toString |> fromString

   let text = myMed |> MedicationText.toString          // original
   let back = myMed |> MedicationText.roundTrip         // new
   ```

   The loads sit at the top of the script, so a module can always be shadowed below them, whether its library came in as a DLL with `#r` or as source with `#load`. The shadow is seen only by the code that follows it in the script. Code in the loaded library keeps calling the original, so to exercise a change through its existing callers, copy those callers into the script and shadow them too; when the chain is long, migrate the lower library's change first as its own plan step, rebuild, and prototype the next library against the new DLL

7. Wait for the user's review; migrate only when the user asks for it

### Migration

- One pull request per issue or implementation plan step, within the 200-line limit on shipped code under `src/`; a larger script migrates over several plan steps
- The tests move to the matching test project and pass there (`dotnet run servertests`)
- The pull request that completes the migration deletes the script

### Using the FSI MCP Server

The [fsi-mcp-server](https://github.com/halcwb/fsi-mcp-server) keeps one FSI session alive across queries. Prefer it over `dotnet fsi` when it runs: `mcp__fsi-mcp__get_fsi_status` (is it running), `mcp__fsi-mcp__send_fsharp_code` (execute F#, end statements with `;;`), `mcp__fsi-mcp__load_f_sharp_script` (load an `.fsx` file), `mcp__fsi-mcp__get_recent_fsi_events` (recent output and errors).

FSI resolves `#load` from its include path, not from the current directory, and `SetCurrentDirectory` does not change that. Add the script's directory first, also before `load_f_sharp_script`, which sends the statements one by one:

```fsharp
#I "/absolute/path/to/script/directory";;
#load "load.fsx";;
```

The session is persistent: load each dependency once, since a type loaded twice conflicts (`FSI_0005.Types.gram` vs `FSI_0010.Types.gram`) and the server must be restarted. A DLL loaded with `#r` cannot be unloaded, so after a build that changes a referenced DLL, ask the user to restart the server; files loaded with `#load` are recompiled and need no restart. Send part of a script to check a small function without reloading everything, and keep the session's state rather than restarting.

Without the server, run a script from its own directory: `cd src/Informedica.GenORDER.Lib/Scripts && dotnet fsi MedicationYaml.fsx --check`.

## Data Dependencies

- Production needs the proprietary medication cache files, which are not in the repository; the demo runs on the sample data that is
- The Google Spreadsheets are live configuration: an edit changes a running system after the next resource reload

## Safety and Documentation

A change that affects dosing, rules, parsing or resource mapping comes with unit tests and, when sheet columns or their meaning change, with updated `///` field comments on the `Data` record and an updated column-contract test. Note a new external dependency or a change in deployment behaviour in CONTRIBUTING.md.

## Checklist for Automated Edits

- [ ] The pull request addresses an issue, or a step of an implementation plan based on one or more issues
- [ ] Source changes migrate a script that passes its tests and that the user reviewed, except the changes the policy allows without a script
- [ ] At most 200 changed lines of shipped code under `src/` (`.fs`, `.sql`, project files); `tests/`, scripts, docs and lock files are not counted, see CONTRIBUTING.md
- [ ] Unit tests added or updated; in a migration, moved from the script
- [ ] `dotnet run servertests` passes for the affected projects
- [ ] The pull request that completes a migration deletes the script
- [ ] `Data` record `///` comments and the column-contract test updated when sheet columns or semantics change
- [ ] Conventional commit message with scope and short description

## Session Rules

- **At the start of a session**, check whether the FSI MCP server runs (`mcp__fsi-mcp__get_fsi_status`). If it does, use its tools for all F# interactive work instead of `dotnet fsi`
- **One pull request at a time.** While a pull request is open, do not open, push, stack or prepare a second one, also not for a fix the user asks for; say that the open pull request blocks the work and wait. A second pull request is allowed only when the user says so explicitly
- **Never commit before review.** Changes stay uncommitted (`git add -N` for new files) until the user has reviewed them and says to commit. A clean merge of upstream master is the exception. Push only when the user asks, as a separate step

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
