# Unit Test Report

GenPRES Unit Test Report — Version 1.0, May 2026

## 1. Summary

This report documents the automated unit and property-based test results for GenPRES as of May 2026. All tests are executed via the `dotnet run ServerTests` command using the Expecto 10.x test runner. The full server-side test suite contains over **5 400 tests** (as of the May 2026 baseline; see §4 for per-module breakdown).

## 2. Test Execution Environment

| Parameter | Value |
|-----------|-------|
| .NET SDK | 10.0 (via `global.json`) |
| Test runner | Expecto ~> 10 with YoloDev.Expecto.TestSdk 0.15.5 |
| Property tests | FsCheck (via Expecto.FsCheck), 1 000 cases/property |
| CI platforms | Ubuntu, Windows, macOS (GitHub Actions `build.yml`) |
| Environment variable | `GENPRES_DEBUG=1` (enables debug logging when set; not used for resource cache selection) |
| Fantomas format gate | Required to pass before tests run |

## 3. Pass/Fail Status

| Status | Count |
|--------|-------|
| ✅ Passed | ≥ 5 408 |
| ❌ Failed | 0 |
| ⚠️ Skipped | 0 |

> **Note**: The 5 408 baseline count was recorded during the security hardening work (ADR-0015, May 2026). The exact count changes as new tests are added. Run `dotnet run ServerTests` locally to obtain the current count.

## 4. Test Coverage by Module

### 4.1 GenSOLVER (`Informedica.GenSOLVER.Tests`)

Source: current repository test assets in `tests/Informedica.GenSOLVER.Tests/Tests.fs` and `tests/Informedica.GenSOLVER.Tests/Scripts/Tests.fsx`

| Test Category | Description |
|---|---|
| Variable operations | Regression tests in `Tests.fs` cover core `Variable.fs` behavior such as incrementing, bounds, and range/value manipulation |
| Constraint propagation | Regression tests in `Tests.fs` exercise solver behavior from `Solver.fs`, including equation solving and `solveAll`-style scenarios |
| End-to-end solver scenarios | Repository-checked GenSOLVER tests focus on executable solver scenarios in `Tests.fs` rather than separate cache/session helper modules |
| Script-based verification | `Scripts/Tests.fsx` provides F# Interactive coverage for running and validating GenSOLVER test cases during script-based development |

### 4.2 GenFORM (`Informedica.GenFORM.Tests`)

Source: ~1 217 lines of test code

| Test Category | Description |
|---|---|
| Dose-rule parsing | Validates CSV parsing against locally cached spreadsheet data |
| Constraint resolution | `PrescriptionRule.fs` — min/max dose adjustment, 131+ patient scenario cases |
| OnceTimed validation | Accepts `MaxRate`, `MaxRateAdj`, or `MaxTime`/`TimeUnit` as valid conditions |
| Component dose display | All `wrap` calls include base + adjustment dose fields |
| `useAdjust` checks | Substance, component, and form level checks |

### 4.3 GenORDER (`Informedica.GenORDER.Tests`)

Source: ~1 160 lines of test code + `Scenarios.fs`

| Test Category | Description |
|---|---|
| Order scenarios | `pcmSupp`, `amfo`, `morfCont`, `pcmDrink`, `cotrim`, `tpn`, `tpnComplete`, `fullMedication` |
| Pipeline correctness | Full Prescription → Preparation → Administration pipeline |
| TPN calculation | Total parenteral nutrition order composition |
| Staged value expansion | Two-phase `skipRate` expansion for `OnceTimed`/`Timed` orders |

### 4.4 GenUNITS (`Informedica.GenUNITS.Tests`)

Source: ~740 lines of test code

| Test Category | Description |
|---|---|
| Unit arithmetic | Addition, subtraction, multiplication, division with unit tracking |
| BigRational conversion | `toBigRational`, `fromFloat`, precision tests |
| `ValueUnit` operations | `singleWithUnit`, `withUnit`, base/unit conversions |
| Unit group compatibility | `eqsGroup` checks for incompatible unit combinations |

### 4.5 Shared (`Informedica.GenPRES.Shared.Tests`)

| Test Category | Description |
|---|---|
| BSA formulas | Mosteller, Du Bois, Haycock, Gehan & George, Fujimoto — boundary values |
| eGFR formulas | CKD-EPI Creatinine 2021, CKD-EPI 2009, MDRD 4-variable, Bedside Schwartz |
| KDIGO classification | `Normal` through `KidneyFailure` GFR stages |
| Age calculations | Post-menstrual age, adjusted age, chronological age in days |

### 4.6 Server (`Informedica.GenPRES.Server.Tests`)

| Test Category | Description |
|---|---|
| Resource loading | Spreadsheet/cache-backed resource loading for server-facing medication data |
| Caching | Local resource cache behavior used by server resource tests |
| Adapters | Adapter and integration-layer tests around server resource access |

### 4.7 Other Libraries

| Library | Coverage Highlights |
|---------|---------------------|
| `Informedica.Utils.Tests` | FsCheck property tests for Array, List, String utilities, plus `JsonSecurity` regression tests verifying unsafe JSON `TypeNameHandling` settings are not reintroduced |
| `Informedica.GenCORE.Tests` | Domain-model invariants with custom FsCheck generators |
| `Informedica.ZIndex.Tests` | G-Standard fixture loading, product and route lookups |
| `Informedica.ZForm.Tests` | ZForm dose-rule parsing and GStand integration |
| `Informedica.NKF.Tests` | NKF dose-rule parsing and lookup |
| `Informedica.FTK.Tests` | FTK dose-rule parsing |
| `Informedica.Agents.Tests` | `MailboxProcessor` agent lifecycle tests |
| `Informedica.Logging.Tests` | Concurrent logging utilities |
| `Informedica.MCP.Tests` | MCP stdio server tool registration and dispatch |
| `Informedica.NLP.Tests` | NLP pipeline unit tests (LLM-independent portions) |

## 5. Known Limitations

1. **No formal coverage metrics**: Line and branch coverage are not currently collected. Adding coverage tooling (e.g., Coverlet) is tracked as a future improvement.
2. **Client-side code**: Fable/Elmish client code has no automated unit tests. It is covered by manual testing and the headless CI smoke tests (`dotnet run TestHeadless`).
3. **NLP extraction tests**: `DoseRuleTests.fsx` and `DoseRuleValidation.fsx` require a live LLM endpoint and are not included in the CI suite.
4. **ZIndex / ZForm**: Some tests require locally cached G-Standard CSV files. These files are not committed to the repository; tests gracefully skip or return empty results when the cache is absent.

## 6. Defect History

| PR | Area | Description |
|----|------|-------------|
| #149 | GenSOLVER | Messages=`[||]` root cause fixed; regression tests added |
| #188 | GenFORM | Three validation bugs fixed; regression suite expanded |
| #285 | GenSOLVER | `ValueSetOverflow` fixed with `MAX_CALC_COUNT` cap; ADR-0014 |
| #305 | Codebase | F# 8 modernisation — `_.Property` lambdas; modern indexers; confirmed by 5 408-test pass |

## 7. Next Steps

- Add Coverlet code-coverage collection to the CI pipeline and publish HTML reports
- Extend NLP tests to cover LLM-independent parsing logic within the CI suite
- Add client-side unit tests for critical Elmish update functions
- Formalise the usability validation report (`usability-validation-report.md`)

---

*Version: 1.0 | Date: May 2026 | Author: Repo Assist (AI) — subject to maintainer review*