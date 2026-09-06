# Implementation plan for issue #378

## Problem description

[#378](https://github.com/informedica/GenPRES/issues/378): GenPRES is meant to be an onion — a
pure domain core, all IO at the outer edge, the domain never depending on IO — but two project
references point outward (`GenFORM.Lib → ZForm.Lib → ZIndex.Lib`, `Logging.Lib → Agents.Lib`),
the formulary library owns its own Google-Sheets loaders and cache, the core reads the clock and
entropy, and the browser fetches rule data and computes emergency doses on its own. The rule is
decided in [ADR-0022](../adr/0022-dependency-rule-and-effects.md); the inventory of violations is
the allow-list in `scripts/CheckDependencyRule.fsx`. This plan is the order in which the
allow-list shrinks.

Each step is classified for validation, because this is medical device software:

- **A** — no observable behaviour change (moves, signatures, references). Verified by the existing
  test suite plus a before/after diff of the `Export` dose-check output and one recorded order
  scenario.
- **B** — failure-path behaviour changes only (an error is no longer swallowed). Verified by
  error-path tests.
- **C** — a dosing or data path changes. Needs validation in the MDR documentation repository
  before release; its own issue, done last.

## Approaches considered

1. **Explicit parameters and function-valued ports** (impureim sandwich): keep the `Logger`
   record, `IResourceProvider`, `GStandProvider`, the registry and the server's `AppEnv`; move
   what is misplaced; add `now` / `newId` parameters. Chosen.
2. **A `program` computation expression over an instruction set, interpreted at the edge**
   (the approach of #194 and #226). Rejected in ADR-0022: interpreter on the solver's hot path,
   less readable effect boundary, a fourth mechanism next to three that work.
3. **Consolidate projects first** (`Logging` + `Utils` into a foundation project, as #378
   originally proposed). Rejected: merging the agent runtime into the utilities every core library
   references would cement the dependency this plan removes.

## Chosen approach

Option 1, in phases that each leave the build green and the fitness test green with a shorter
allow-list. Pull requests stay under 200 changed lines; phases with more work are split into
several PRs. All non-UI code is prototyped in `.fsx` scripts first and migrated by the maintainer
(script-only policy in `AGENTS.md`).

Target ring assignment (also the ring map in the fitness test):

| Ring | Projects |
| ---- | -------- |
| Core | Utils (pure half), Logging (port only), GenUNITS, GenCORE, GenSOLVER, GenFORM, GenORDER, GenINTERACT |
| Infrastructure (DMZ) | Utils IO half, Agents, ZIndex, ZForm, NKF, FTK, a new adapter project (working name `Informedica.GenPRES.Data.Lib`) |
| Presentation (DMZ entry) | Server, MCP.Lib, MCP.Server |
| Contract | Shared |
| Outside the DMZ | Client |
| Tooling, outside the runtime onion | NLP |

## Confidence

High for phases 0 to 5: they move code and add parameters, and the fitness test plus the existing
suites catch regressions. Medium for phase 6: moving browser-side dosing behind the server changes
a clinical path and the validation effort is not yet scoped.

## Steps

### Phase 0 — land the rule (A, docs and deletions)

1. Delete the dead `getDataFromGenPres` in `src/Informedica.GenORDER.Lib/Utils.fs` (mutates the
   process environment; no caller), the dead `GenPresProduct` / `GenericProduct` aliases in
   `src/Informedica.GenFORM.Lib/DoseRule.fs`, and the unreferenced top-level `agentLogger` in
   `src/Informedica.GenFORM.Lib/FormLogging.fs` (a top-level value that starts a
   `MailboxProcessor` — the pattern of #523).
2. Confirm the type-initialization "warm-up" in `src/Informedica.GenFORM.Lib/DoseRuleLoader.fs`
   is unnecessary now that ZIndex loads lazily (#526), and delete it.
3. Land `scripts/CheckDependencyRule.fsx` and a `CheckArchitecture` FAKE target in `Build.fs`
   next to `CheckVersions`, run in CI:

   ```fsharp
   Target.create "CheckArchitecture" (fun _ -> run dotnet [ "fsi"; "scripts/CheckDependencyRule.fsx" ] ".")
   ```

4. Land ADR-0022 and the ADR-0001 amendment; answer the open question on #378.

### Phase 1 — logging inversion (A)

1. Split `src/Informedica.Logging.Lib/Logging.fs`: the `Logger` record, `Event`, `Level`,
   `Message`, `noOp`, `create`, `combine` and `logLazy` stay; `createConsole`, `createFile` and
   `AgentLogging` move into `Informedica.Agents.Lib`. Flip the reference so `Agents.Lib →
   Logging.Lib`. The sink stamps the timestamp, so `createMessage` stops reading `DateTime.Now`.
2. Move `SolverLogging.createAgentLogger` / `createFileLogger` and `OrderLogging.create*Logger`
   into `src/Informedica.GenPRES.Server/Logging.fs` and an MCP equivalent. Formatters stay in
   the libraries.
3. Replace every console write below the injected `Logger` with a `Logger` call, one project per
   PR: GenSOLVER (`Solver.fs`, `Variable.fs`, `Equation.fs`), GenORDER (`Order.fs`,
   `OrderVariable.fs`, `Medication.fs`, `OrderProcessor.fs`, `Api.fs`, `Exceptions.fs`,
   `Nutrition.fs`), GenFORM (`Product.fs`, `RenalRule.fs`, `Resources.fs`), GenUNITS
   (`UnitsParse.fs`, `ValueUnit.fs`), Utils (`Json.fs`, `BCL/*.fs`).

### Phase 2 — evict IO from GenFORM and Utils into the adapter project (A, one B)

1. Create the adapter project referencing GenFORM, GenINTERACT, ZForm and the IO half of Utils.
   Move `src/Informedica.GenFORM.Lib/SourceLoader.fs` (top-level `HttpClient`, hard-coded
   kinderformularium.nl) and the `nkfLinkProvider` registry entry.
2. Move the sheet-reading halves of `Mapping.fs`, `Product.fs`, `DoseRuleLoader.fs`,
   `SolutionRule.fs` and `RenalRule.fs`; the parsers stay in GenFORM and take `string[][]`.
   Replace the `Result.defaultValue [||]` in `src/Informedica.GenFORM.Lib/Utils.fs`
   (`getDataFromSheet`) with a real `Error` — **B**, and the point of the exercise: an unavailable
   rule sheet must not read as "no rules".
3. Move `defaultRegistry`, `loadAllResources`, `CachedResourceProvider`,
   `Api.getCachedProviderWithDataUrlId` and `Api.reloadCache` (the downcast of the port
   disappears with them). GenFORM keeps `Keys`, `IResourceProvider`, `ofResult`, `derive`,
   `LoadEngine`. `Server.fs` and `MCP.Server/Program.fs` reference the adapter project.
4. Move the `None` branch of `src/Informedica.GenINTERACT.Lib/Data.fs` (cwd-relative file read)
   and the cwd-relative export write in `src/Informedica.GenFORM.Lib/Export.fs`.
5. Split `Informedica.Utils.Lib`: `Web.fs`, `File.fs`, `Env.fs`, `App.fs`, `AppPath.fs`,
   `Console.fs`, `StopWatch.fs` move to an IO project (or into the adapter project). `Path.fs`
   stays (string helpers only).

### Phase 3 — invert `GenFORM → ZForm → ZIndex` (A, the riskiest A)

1. Define a GenFORM-owned G-Standaard dose-rule contract carrying only what
   `src/Informedica.GenFORM.Lib/Check.fs` reads (route, indications and dosages, patient
   category age in months, the single/start dosage norm and absolute ranges, frequencies).
   Retype `GStandProvider`, `filterPatient` and `matchWithZIndex`; implement the ZForm-to-contract
   mapping in the adapter project. Verify with the dose-check tests and a golden diff of the
   `Export` check output. Split the contract-type PR from the retyping PR.
2. Define a GenFORM-owned source-product contract for what `Product.fromGenPresProducts` and
   `filterGenPresProductsByData` read from `GenPresProduct` / `GenericProduct` /
   `ProductSubstance`; mapping in the adapter; delete the impure `Product.get`; retype the
   `GenPresProducts` field of `Resources.Data` and `Keys.genPresProducts`.
3. Remove the `ZForm.Lib` reference from `Informedica.GenFORM.Lib.fsproj`, add `GenCORE.Lib`.
   ZForm's `Web.getDataFromSheet` and ZIndex's `FilePath.useDemo` take `urlId` / `useDemo` as
   values from the adapter instead of reading the environment. The reference allow-list in the
   fitness test becomes empty.

### Phase 4 — non-determinism in the core (A)

1. `src/Informedica.GenCORE.Lib/Patient.fs`: the six `DateTime.Now` sites become a `now`
   parameter (age from birth date, DTO defaults, date validation).
2. `src/Informedica.GenORDER.Lib/Order.fs` (`DateTime.Now` in `StartStop.Start`) and
   `Medication.fs` (`Guid.NewGuid()` in the constructor): supplied by the caller.
   `Environment.ProcessorCount` stays allow-listed; it sizes chunks and never a result.

### Phase 5 — DMZ consolidation at the edge (A, security-relevant)

1. One authentication path in `src/Informedica.GenPRES.Server/ServerApi.Command.fs`; retire the
   raw-password comparison in `ServerApi.Services.fs` (`ReloadResources`).
2. The `GENPRES_PROD` read in `ServerApi.Services.fs` (`setDemoVersion`) and the module-level
   `provider` in `Server.fs` (`Async.RunSynchronously` at assembly load) move into
   `ServerApi.CompositionRoot.fs`.
3. `src/Informedica.MCP.Server/Program.fs` stops setting `GENPRES_PROD`, `GENPRES_DEBUG` and
   `CurrentDirectory`; `Informedica.MCP.Lib` composes the same `AppEnv` and mappers as the
   server instead of two `static let mutable` provider singletons. This may need the ports,
   adapters, services and mappers extracted from the server into an application project; if so,
   its own issue.

### Phase 6 — the client and Shared (the only C items; last)

1. (A/B) Serve the eight sheets the client fetches today (`src/Informedica.GenPRES.Client/Utils.fs`:
   emergency list, continuous medications, products, localization, growth charts) as registry
   resources through a new port; the client fetches them via Fable.Remoting. Remove
   `docs.google.com` from the CSP in `Server.fs`. Formulas still run in the browser, so the
   dosing output is unchanged; only the data path moves inside the DMZ.
2. (test) A property test asserting that `Shared/Calculations.fs` and `GenCORE/Calculations.fs`
   agree within tolerance for BSA, adjusted age, eGFR and GFR staging. ADR-0019 stands; the test
   guards against divergence.
3. (**C**) Move `EmergencyTreatment.calculate` and `ContinuousMedication.calculate` from
   `src/Informedica.GenPRES.Shared/Models.fs` behind a server port, and replace the
   hospital-specific special case in `ContinuousMedication.calculate` with configuration. Own
   issue; MDR validation.
4. (**C**) Move the `NutritionDoseRuleSet` literals in `ServerApi.Services.fs` to a sheet loaded
   through the resource provider. Own issue.

Suggested: one child issue per phase, so that each PR closes an issue and this plan can be removed
when #378 closes.
