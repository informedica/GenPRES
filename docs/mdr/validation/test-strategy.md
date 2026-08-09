# Test Strategy

GenPRES Test Strategy — Version 1.0, May 2026

## 1. Scope

This document describes the test strategy for the GenPRES Clinical Decision Support System. It covers automated unit and property-based testing of all server-side F# libraries, integration testing of the medication resource pipeline, and continuous integration verification on every pull request and push to the `master` branch.

Client-side (Fable/Elmish) code is validated through manual exploratory testing and the headless CI smoke-test target (`dotnet run TestHeadless`). Formal usability validation is documented separately in `usability-validation-report.md`.

## 2. Objectives

- Detect regressions in medication dosing calculations before merge to `master`
- Verify correctness of constraint-solving mathematics via property-based testing
- Confirm that unit-of-measure conversions are exact (BigRational arithmetic)
- Validate resource parsing for all Google Spreadsheet–sourced medication data
- Ensure security properties are preserved (e.g., JSON deserialisation defaults)
- Provide a reproducible, CI-verified test baseline for MDR traceability

## 3. Test Framework

| Component | Tool | Version |
|-----------|------|---------|
| Unit test runner | [Expecto](https://github.com/haf/expecto) | ~> 10 |
| Property-based tests | [FsCheck](https://fscheck.github.io/FsCheck/) via `Expecto.FsCheck` | ~> 10 |
| Test SDK integration | `YoloDev.Expecto.TestSdk` | 0.15.5 |
| Assertions | `Expecto.Flip` (pipeline-style) | bundled with Expecto |
| Code formatter (pre-test) | [Fantomas](https://github.com/fsprojects/fantomas) | via dotnet tool |

All tests are written in F# using the Expecto `testList` / `test` / `testCase` DSL. Assertions use the Expecto.Flip pipeline style:

```fsharp
actual
|> Expect.equal $"should equal {expected}" expected
```

Property-based tests use FsCheck generators with custom `BigRational` and domain-type arbitraries configured in project-specific `Generators.fs` files. The number of test cases per property is configured per test project via `maxTest` rather than using a single repository-wide default; current projects use values such as 1 000 and 10 000 cases per property.

## 4. Test Projects

Each library has a dedicated test project under `tests/`. The following projects are included in the CI run (`dotnet run ServerTests`):

| Test Project | Library Under Test | Test File(s) |
|---|---|---|
| `Informedica.Agents.Tests` | `Informedica.Agents.Lib` | Tests.fs |
| `Informedica.DataPlatform.Tests` | `Informedica.DataPlatform.Lib` | Tests.fs |
| `Informedica.FHIR.Tests` | `Informedica.FHIR.Lib` | Tests.fs |
| `Informedica.FTK.Tests` | `Informedica.FTK.Lib` | Tests.fs |
| `Informedica.GenCORE.Tests` | `Informedica.GenCORE.Lib` | Tests.fs, Generators.fs |
| `Informedica.GenFORM.Tests` | `Informedica.GenFORM.Lib` | Tests.fs |
| `Informedica.GenORDER.Tests` | `Informedica.GenORDER.Lib` | Tests.fs, Scenarios.fs |
| `Informedica.GenPRES.Server.Tests` | Server integration | Tests.fs |
| `Informedica.GenPRES.Shared.Tests` | `Informedica.GenPRES.Shared` | Tests.fs |
| `Informedica.GenSOLVER.Tests` | `Informedica.GenSOLVER.Lib` | Tests.fs |
| `Informedica.GenUNITS.Tests` | `Informedica.GenUNITS.Lib` | Tests.fs |
| `Informedica.HIXConnect.Tests` | `Informedica.HIXConnect.Lib` | Tests.fs |
| `Informedica.Logging.Tests` | `Informedica.Logging.Lib` | Tests.fs |
| `Informedica.MCP.Tests` | `Informedica.MCP.Lib` | Tests.fs |
| `Informedica.NKF.Tests` | `Informedica.NKF.Lib` | Tests.fs |
| `Informedica.NLP.Tests` | `Informedica.NLP.Lib` | Tests.fs |
| `Informedica.OTS.Tests` | `Informedica.OTS.Lib` | Tests.fs |
| `Informedica.Utils.Tests` | `Informedica.Utils.Lib` | Tests.fs, Generators.fs |
| `Informedica.ZForm.Tests` | `Informedica.ZForm.Lib` | Tests.fs |
| `Informedica.ZIndex.Tests` | `Informedica.ZIndex.Lib` | Tests.fs, FixtureTests.fs, ZIndexFixture.fs |

## 5. Test Types

### 5.1 Unit Tests

Pure functional tests that verify individual module behaviour with explicit input/output pairs. Organised as nested `testList` hierarchies mirroring the source-module structure. Examples:

- `GenUNITS`: unit-of-measure arithmetic, `BigRational` conversion, `removeBigRationalMultiples` semantics
- `GenSOLVER`: constraint propagation, variable increment correctness, canon-key normalisation, `LRUCache` correctness
- `GenFORM`: dose-rule parsing, constraint application, minimum/maximum dose validation against 131+ patient scenarios
- `GenORDER`: order calculation, scenario round-trips (`pcmSupp`, `amfo`, `morfCont`, `pcmDrink`, etc.)
- `Shared`: BSA formulas (Mosteller, Du Bois, Haycock, Gehan & George, Fujimoto), eGFR formulas (CKD-EPI 2021, MDRD, Bedside Schwartz), age calculations
- Server: JSON security defaults (`TypeNameHandling.None` guard), authentication, endpoint smoke tests

### 5.2 Property-Based Tests

FsCheck-driven tests that verify invariants across randomly generated inputs. Key properties:

- `GenSOLVER`: LRU cache capacity/eviction/thread-safety invariants; canon-key round-trip symmetry and hash stability; constraint-solving correctness under arbitrary variable sets
- `Utils`: generic list and array invariants
- `GenCORE`: domain-model invariants

FsCheck run counts in this repository are configured per suite, commonly 1 000 or 10 000 cases per property depending on the test project. Configurations are defined in `Generators.fs` files adjacent to each test suite.

### 5.3 Integration / Resource Tests

Tests that exercise the full resource-loading pipeline against locally cached CSV snapshots of the Google Spreadsheet medication data. These tests run inside the server test suite and use the existing cache-selection mechanism for demo versus production data (`GENPRES_PROD`), while `GENPRES_DEBUG` controls debug logging only. They cover:

- Dose-rule parsing from CSV (all products and indications)
- Solution-rule loading and validation
- Renal-rule parsing
- Route and frequency mapping lookup

The `ZIndex` tests additionally exercise the medication fixture loader against locally cached G-Standard data, using the same demo-versus-production cache selection mechanism.

### 5.4 Security Regression Tests

Dedicated `JsonSecurity` sub-module tests guard the `Newtonsoft.Json` `TypeNameHandling.None` default. These tests fail loudly if the serialisation default is ever reverted (potential gadget-chain RCE risk). See ADR-0015 and the 2026-04-10 security review.

## 6. Continuous Integration

Tests run automatically via GitHub Actions (`build.yml`) on every push and pull request to `master`:

```text
Jobs: build (ubuntu-latest, windows-latest, macOS-latest)
Steps:
  1. dotnet tool restore
  2. dotnet fantomas --check .    (format gate — fails PR if formatting differs)
  3. dotnet run ServerTests       (all server-side tests)
```

The three-OS matrix ensures platform-specific path and encoding issues are caught before merge. The format check enforces the Fantomas coding style defined in `.editorconfig`. Client-side headless tests can be run separately with `dotnet run TestHeadless`.

## 7. Test Execution

### Run all server tests locally

```bash
dotnet run ServerTests
```

### Run a single library's tests

```bash
dotnet test tests/Informedica.GenSOLVER.Tests/
dotnet test tests/Informedica.GenFORM.Tests/
dotnet test tests/Informedica.GenUNITS.Tests/
# … etc.
```

### Watch mode (re-run on file change)

```bash
dotnet run WatchTests
```

## 8. Coverage Goals

| Area | Target |
|------|--------|
| Constraint solver (GenSOLVER) | Unit + property tests for all public functions |
| Dosing calculations (GenFORM) | ≥ 131 patient scenarios; all dose-type validations |
| Order processing (GenORDER) | All `Scenarios.fs` entries exercise the full pipeline |
| Unit conversions (GenUNITS) | Round-trip and precision tests for all unit groups |
| Shared calculations (BSA, eGFR, Age) | Formula correctness + boundary values |
| Security properties | Regression tests for each identified vulnerability |

Formal coverage metrics (line/branch) are not currently collected. Adding coverage reporting is tracked as a future improvement.

## 9. Known Gaps and Open Items

- Formal code-coverage metrics are not yet collected (no `.coverage` tooling configured)
- Client-side Fable/Elmish code has no automated unit tests; covered by manual exploratory testing only
- NLP dose-rule extraction tests (`DoseRuleTests.fsx`) are script-based and not included in the CI suite; they require a live LLM endpoint
- Usability validation testing is documented separately in `usability-validation-report.md` and is pending execution

## 10. References

- ADR-0001: System Architecture — `docs/mdr/design-history/0001-system-architecture.md`
- ADR-0014: Staged Value Expansion — `docs/mdr/design-history/0014-staged-value-expansion-timed-orders.md`
- ADR-0015: Security Baseline — `docs/mdr/design-history/0015-security-baseline.md`
- LRU Solver Memoisation design — `docs/code-reviews/solver-memoization.md`
- ADR-0019: Shared Clinical Calculations — `docs/mdr/design-history/0019-shared-clinical-calculations.md`
- GenSOLVER Stability Analysis — `docs/domain/gensolver-stability-analysis.md`
- Risk Management Plan — `docs/mdr/risk-analysis/risk-management-plan.md`

---

*Version: 1.0 | Date: May 2026 | Author: Repo Assist (AI) — subject to maintainer review*
