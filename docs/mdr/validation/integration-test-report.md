# Integration Test Report

GenPRES Integration Test Report — Version 1.0, May 2026

## 1. Summary

This document records the integration test results for GenPRES as of May 2026. Integration tests verify that independently developed modules interact correctly at their boundaries: resource loading, server command dispatch, and dose-check severity classification. All tests are located in `tests/Informedica.GenPRES.Server.Tests/` and are executed as part of the `dotnet run ServerTests` CI run.

**Test count (baseline, May 2026):** 32 integration tests across 3 modules, all passing. **As of September 2026:** 30 — the two agent-path duplicates in `ResourceErrorTests` were removed together with the server agent adapters (issue #420); see §4.1.

---

## 2. Scope

Integration testing in GenPRES targets the boundaries between subsystems, specifically:

| Integration boundary | What is tested |
|---|---|
| Resource loading pipeline | `ResourceConfig` → `loadAllResourcesWithConfig` → `ResourceState` |
| Resource cache layer | `CachedResourceProvider` (lazy load, error retention, `ReloadCache`) |
| Server command dispatch | `Api.Command` → `ServerApi.Command.processCmd` → domain ports |
| Dose-check severity | Raw tab-delimited check output → `TextBlock` DU (Valid / Caution / Warning / Alert) |

Client-side (Fable/Elmish) integration is validated manually and through the `dotnet run TestHeadless` smoke test. External EHR integration is not implemented; see ADR-0020 for the superseded FHIR design.

---

## 3. Test Execution Environment

| Parameter | Value |
|---|---|
| .NET SDK | 10.0 (via `global.json`) |
| Test runner | Expecto ~> 10 with YoloDev.Expecto.TestSdk 0.15.5 |
| Test project | `tests/Informedica.GenPRES.Server.Tests/` |
| CI platforms | Ubuntu, Windows, macOS (GitHub Actions `build.yml`) |
| Run command | `dotnet run ServerTests` (includes all 16 test projects) |

---

## 4. Integration Test Modules

### 4.1 Resource Error Propagation (`ResourceErrorTests`)

Tests that the `loadAllResourcesWithConfig` function correctly propagates failures from any individual getter and that the `CachedResourceProvider` preserves error state across calls.

**Test count: 12** (14 at the May 2026 baseline; the former rows 13 and 14 ran the same two IsLoaded-guard scenarios through `AgentAdapters.makeAppEnv`, deleted in September 2026 with the agent adapters, issue #420)

| # | Test name | Scenario | Expected result |
|---|---|---|---|
| 1 | First getter (`GetUnitMappings`) returns Error | `GetUnitMappings` returns `Error [msg]` | `loadAllResourcesWithConfig` returns `Error` |
| 2 | Middle getter (`GetFormRoutes`) returns Error | `GetFormRoutes` returns `Error [msg]` | `loadAllResourcesWithConfig` returns `Error` |
| 3 | Last getter (`GetRenalRules`) returns Error | `GetRenalRules` returns `Error [msg]` | `loadAllResourcesWithConfig` returns `Error` |
| 4 | Getter throws exception | `GetUnitMappings` raises `failwith` | Returns `Error` with "Failed to load resources" message |
| 5 | All getters succeed | All getters return `Ok` | `ResourceState.IsLoaded = true`, `Messages = [||]` |
| 6 | Loader fails, `GetResourceInfo` | `CachedResourceProvider` with failing loader | `IsLoaded = false`, `Messages` non-empty |
| 7 | All resource getters when loader failed | `GetUnitMappings()`, `GetDoseRules()`, `GetProducts()`, `GetRenalRules()` | All return empty arrays |
| 8 | Second call after error does not re-invoke loader | Loader called once; second `GetResourceInfo()` call | `callCount = 1` (cached error) |
| 9 | `ReloadCache` re-invokes loader | After one failed load, `ReloadCache()` called | `callCount = 2` |
| 10 | Fail then succeed after `ReloadCache` | First load fails; `ReloadCache`; second load succeeds | After reload: `IsLoaded = true` |
| 11 | `FormularyCmd` when `IsLoaded = false` | `CachedResourceProvider` with error loader | `processCmd` returns `Error` |
| 12 | `ParenteraliaCmd` when `IsLoaded = false` | `CachedResourceProvider` with error loader | `processCmd` returns `Error` |

**Pass/fail status:** All 12 passing.

---

### 4.2 Stub Adapter Command Routing (`StubAdapterTests`)

Tests that the server command dispatch layer (`ServerApi.Command.processCmd`) correctly routes each `Api.Command` variant to the appropriate domain port, propagates port errors, and enforces the `requireLoaded` guard.

**Test count: 9**

Stub adapters replace all domain ports with in-memory implementations; no `IResourceProvider` or network access is involved.

#### 4.2.1 Command routing (5 tests)

| # | Command | Port called | Expected response |
|---|---|---|---|
| 1 | `Api.FormularyCmd` | `formulary.getFormulary` | `Ok (Api.FormularyResp formulary)` |
| 2 | `Api.ParenteraliaCmd` | `formulary.getParenteralia` | `Ok (Api.ParenteraliaResp parenteralia)` |
| 3 | `Api.OrderContextCmd UpdateOrderContext` | `orderContext.evaluate` | `Ok (Api.OrderContextResp (OrderContextResult ...))` |
| 4 | `Api.NutritionPlanCmd InitNutritionPlan` | `nutritionPlan.initNutritionPlan` | `Ok (Api.NutritionPlanResp (NutritionPlanInitialised ...))` |
| 5 | `Api.OrderPlanCmd FilterOrderPlan` | `orderPlan.filterOrderPlan` | `Ok (Api.OrderPlanResp (OrderPlanFiltered ...))` |

#### 4.2.2 Error propagation (2 tests)

| # | Scenario | Expected result |
|---|---|---|
| 6 | `formulary.getFormulary` returns `Error [| "test error" |]` | `processCmd` returns `Error [| "test error" |]` |
| 7 | `orderContext.evaluate` returns `Error [| "ctx error" |]` | `processCmd` returns `Error [| "ctx error" |]` |

#### 4.2.3 `requireLoaded` guard (2 tests)

| # | Scenario | Expected result |
|---|---|---|
| 8 | `requireLoaded` returns `Some [| "not ready" |]` | `processCmd` returns `Error [| "not ready" |]` before dispatching |
| 9 | `requireLoaded` returns `None` (loaded) | `processCmd` dispatches normally, returns `Ok` |

**Pass/fail status:** All 9 passing.

---

### 4.3 Dose-Check Severity Classification (`DoseCheckTests`)

Tests that the `DoseCheck.build` function correctly classifies raw server-side dose-check output lines into `TextBlock` severity values (`Valid`, `Caution`, `Warning`, `Alert`) for rendering in the Formulary UI.

**Test count: 9**

| # | Input | Expected severity |
|---|---|---|
| 1 | No check lines (empty array) | Single `Valid "Ok!"` block |
| 2 | Single "geen doseer bewaking" sentinel | `Caution` (blue info; no rules to evaluate) |
| 3 | Multiple "geen doseer bewaking" sentinels | All `Caution`, one block per sentinel |
| 4 | Only frequency inconsistency lines | All `Warning` |
| 5 | Any dose-range inconsistency line | All `Alert` |
| 6 | Mixed frequency + dose-range | All `Alert` (highest severity wins) |
| 7 | Frequency violation alongside sentinel | Sentinel dropped; remaining block is `Warning` |
| 8 | `isFrequency` helper — frequency vs dose line | Returns `true` for "frequenties" line, `false` for dose line |
| 9 | `isNoMonitoring` helper — sentinel substring match | Returns `true` for sentinel, `false` for non-sentinel |

**Pass/fail status:** All 9 passing.

---

## 5. Integration Points Not Covered by Automated Tests

| Integration boundary | Coverage status | Notes |
|---|---|---|
| Google Sheets CSV download | ✗ Not covered (requires live network) | Validated manually with `GENPRES_URL_ID` credentials |
| Fable.Remoting wire protocol | ✗ Not covered | Client/server RPC channel validated via headless smoke test |
| Docker image startup | ✗ Not covered | Validated manually via `dotnet run DockerRun` |
| Interaction API (external service) | ✗ Stub only | `InteractionPort` stubbed in server tests; no external call |
| MCP stdio transport | ✗ Not covered | `Informedica.MCP.Tests` covers tool registration; stdio not tested in CI |
| Admin authentication (password check) | ✗ Not covered | Manual testing; no CI tests for the auth endpoint |
| Log analysis pipeline | ✗ Stub only | `LogAnalyzerPort` stubbed; file I/O not tested in CI |

---

## 6. Known Gaps and Remediation Plan

| Gap | Risk | Planned remediation |
|---|---|---|
| No end-to-end server startup test | Medium — composition root wiring errors may only surface at runtime | Add a smoke-test fixture in `Informedica.GenPRES.Server.Tests` that boots the Saturn application in-process and issues a single HTTP request |
| No round-trip tests for Fable.Remoting wire format | Low — format is generated; mismatch would be caught at runtime | Add a serialisation test using `Fable.Remoting.Json` in the Shared tests project |
| Google Sheets integration relies on manual testing | Medium — CSV column renames in the spreadsheet are not caught by CI | Add offline snapshot tests using representative CSV fixtures from `tests/` data directories |

---

## 7. References

- Test strategy: `docs/mdr/validation/test-strategy.md`
- Unit test report: `docs/mdr/validation/unit-test-report.md`
- Server test source: `tests/Informedica.GenPRES.Server.Tests/Tests.fs`
- ADR-0015 (security): `docs/mdr/design-history/0015-security-baseline.md`
- ADR-0020 (FHIR R4): `docs/mdr/design-history/0020-fhir-r4-integration.md`
