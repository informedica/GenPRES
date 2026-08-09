# W3: Requirements & Traceability — Status Analysis

## Workshop Status: In Progress ⏳

**Last Updated**: 2026-05-17

This document analyses progress toward the W3 workshop goals as defined in
[ROADMAP](../../ROADMAP.md).

---

## W3 Goals (from ROADMAP)

| Goal | Status | Evidence |
|------|--------|----------|
| Requirements review and validation | 🔄 Partial | `software-requirements.md` v1.1, `user-requirements.md` UR-001–020 |
| Traceability matrix completion | 🔄 Partial | Excel matrices exist; markdown linkage UR → SR → ADR → test incomplete |
| Test coverage analysis | ✅ Complete | `test-strategy.md`, `unit-test-report.md`, `integration-test-report.md` (May 2026) |
| Gap identification | 🔄 Partial | Known gaps documented in validation reports; formal gap analysis pending |

---

## Completed Work

### Requirements Review and Validation

#### User Requirements (`user-requirements.md`)

The user requirements document defines 20 requirements (UR-001 to UR-020)
covering accessibility, clinical use, AI/decision support, TPN management,
security, reliability, and usability:

- **UR-001 to UR-004**: Platform and EPD integration requirements
- **UR-005 to UR-010**: Clinical prescription and G-Standard requirements
- **UR-011 to UR-012**: AI and MCP decision-support requirements
- **UR-013 to UR-014**: TPN management requirements
- **UR-015 to UR-016**: Security and audit requirements
- **UR-017 to UR-018**: Reliability requirements
- **UR-019 to UR-020**: Usability requirements

#### Software Requirements (`software-requirements.md` v1.1, May 2026)

Updated to reflect implemented features:

- Corrected .NET 10 toolchain reference (from stale .NET 8).
- FHIR R4 integration moved from "Future Enhancements" to Integration section
  (designed in ADR-0020).
- MCP stdio server and NLP dose-rule extraction pipeline promoted from "future"
  to implemented.
- Security section expanded to reflect ADR-0015 baseline controls.
- G-Standard fallback (ADR-0016) and shared clinical calculations (ADR-0019)
  added to functional requirements.

### Test Coverage Analysis

Three validation documents were completed in May 2026:

#### Test Strategy (`docs/mdr/validation/test-strategy.md`)

Documents the full test strategy:

- 20 test projects under `tests/`, the majority covering a dedicated library.
  Note: `Informedica.FHIR.Tests`, `Informedica.NLP.Tests`, and
  `Informedica.MCP.Tests` currently contain only placeholder Hello World tests.
- Expecto 10.x test runner with FsCheck property-based testing.
- CI matrix across Ubuntu, Windows, and macOS (GitHub Actions `build.yml`).
- Unit, property-based, integration, and security test types.
- Coverage goals and known gaps (including absence of formal line/branch
  coverage metrics).

#### Unit Test Report (`docs/mdr/validation/unit-test-report.md`)

Documents the per-module test coverage baseline:

| Module | Coverage Highlights |
|--------|---------------------|
| GenSOLVER | Variable ops, constraint propagation, end-to-end solver scenarios |
| GenFORM | 131+ patient scenario cases, dose-rule parsing, constraint resolution |
| GenORDER | Order pipeline scenarios via `Scenarios.fs` |
| Shared | BSA/eGFR/age formula tests with units-of-measure validation |
| GenSOLVER (scripts) | Canon-key/LRU/cycle-detection tests in `.fsx` — **manual/script-only; not part of `dotnet run ServerTests` CI baseline** |

**Baseline total**: ≥ 5 408 passing tests (0 failures, 0 skipped) as of May 2026.

#### Integration Test Report (`docs/mdr/validation/integration-test-report.md`)

Documents 32 integration tests across three modules:

- Resource loading pipeline — 14 tests for `loadAllResourcesWithConfig` and
  `CachedResourceProvider`.
- Server command dispatch — tests for all 5 command types, error propagation,
  and `requireLoaded` guard.
- Dose-check severity — tests for Valid / Caution / Warning / Alert
  classification logic.

### Architectural Decision Records

Following issue #411, the set was reduced to the decisions that are genuinely
hard to reverse; ADRs that recorded code structure, code changes or ongoing work
were retired. The remaining records are:

| ADR | Topic |
|-----|-------|
| 0000 | Change log |
| 0001 | System architecture |
| 0009 | MCP server architecture |
| 0014 | Staged value expansion for timed orders |
| 0015 | Security baseline |
| 0016 | G-Standard dose-rule fallback |
| 0018 | NLP dose-rule extraction |
| 0019 | Shared clinical calculations |
| 0020 | FHIR R4 EHR integration |

---

## Remaining / In Progress

### Traceability Matrix (Markdown)

Excel-format traceability matrices exist (`traceability-matrix.xlsx`,
`genpres_traceability_matrix.xlsx`) but are not maintained as machine-readable
text. A markdown traceability matrix linking UR → SR → ADR → test cases would
improve navigability and CI-verifiability. Remaining tasks:

- [ ] Create `docs/mdr/requirements/traceability-matrix.md` with
      UR-001–020 → SR cross-references.
- [ ] Extend the matrix with ADR references for each requirement.
- [ ] Add test case cross-references for each functional requirement.

### Formal Gap Analysis

Known gaps have been identified in the existing validation documents but not
consolidated into a single gap-analysis document:

**Requirement coverage gaps (from `unit-test-report.md` and `integration-test-report.md`):**

- Formal line/branch coverage metrics are not yet collected; both
  `test-strategy.md` and `unit-test-report.md` flag this as an open gap.
- `Informedica.FHIR.Tests`, `Informedica.NLP.Tests`, and
  `Informedica.MCP.Tests` contain only placeholder Hello World tests; their
  libraries are not yet covered by automated unit tests.
- Client-side Fable/Elmish code is covered by manual exploratory testing only;
  no automated component-level tests exist.
- NLP dose-rule extraction pipeline (`DoseRuleExtract.fsx`) has no formal
  test suite beyond the `.fsx` prototype.
- FHIR R4 translation (`FhirExpectoTests.fsx`) has prototype Expecto tests
  but is not yet part of the CI `dotnet run ServerTests` suite
  (source: `integration-test-report.md`).
- MCP server command handlers have no dedicated integration tests
  (source: `integration-test-report.md`).

**Functional requirements not yet verified by tests:**

- UR-008 (adjust active prescriptions / view historical changes) — no test
  currently exercises the prescribe-edit flow end-to-end.
- UR-010 (full Dutch Pediatric Formulary) — coverage is 131 scenarios; full
  formulary regression suite is a W4 objective.
- UR-015 (audit logging) — server action logging is implemented but no
  automated test verifies log output format or completeness.

**W3 remaining tasks:**

- [ ] Draft `docs/mdr/requirements/gap-analysis.md` consolidating the above
      gaps with proposed resolution paths.
- [ ] Identify which gaps are acceptable for v2.0 release (deferred risk) vs.
      must-fix before clinical deployment.

### Clinical Expert Review

Formal review of requirement adequacy by a clinical expert (physician / clinical
pharmacist) is deferred to W4 scope and is not yet scheduled.

---

## Summary

W3 is approximately **60–70% complete**. The test strategy and validation
baselines are documented, software requirements are up to date, and 21 ADRs
provide decision-level traceability. The main gaps are:

1. **Markdown traceability matrix** — machine-readable UR → SR → ADR → test
   linkage is not yet in place.
2. **Formal gap analysis document** — known coverage gaps are scattered across
   validation reports.
3. **Clinical expert review** — deferred to W4.

---

## References

| Resource | Path / Link |
|----------|-------------|
| User Requirements | `docs/mdr/requirements/user-requirements.md` |
| Software Requirements | `docs/mdr/requirements/software-requirements.md` |
| Test Strategy | `docs/mdr/validation/test-strategy.md` |
| Unit Test Report | `docs/mdr/validation/unit-test-report.md` |
| Integration Test Report | `docs/mdr/validation/integration-test-report.md` |
| ADR index | `docs/mdr/design-history/` (0000–0020) |
| Architecture and Timeline | `docs/roadmap/genpres-architecture-and-timeline.md` |
| ROADMAP | `ROADMAP.md` |

---

*This document was generated by Repo Assist and reflects the state of the
`master` branch as of 2026-05-17. It is intended as a progress aid for the
human maintainer and should be reviewed before being considered definitive.*
