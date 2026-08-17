# ADR-0000: Design History Change Log

**Date**: 2024-01-01
**Status**: Accepted

## Context

MDR (Medical Device Regulation) requires a Design History File (DHF) that records significant design and development decisions over the lifetime of the product. A running change log within the design-history folder supplements the individual ADRs by providing a chronological index of changes.

## Decision

Maintain this document as a reverse-chronological log of significant design changes, linking to the relevant ADR or CHANGELOG entry for details.

## Consequences

- Auditors and reviewers can quickly navigate the design history.
- Each entry should reference the ADR number and a brief description of the change.

---

## Log

| Date | ADR | Summary |
|------|-----|---------|
| 2026-08-17 | [ADR-0021](0021-build-system-versioning-and-release.md) | Build system versioning and release automation accepted; EasyBuild.ShipIt owns version/changelog/release-PR generation and writes `Directory.Build.props`, all three merge methods left enabled with `--skip-merge-commit`, Repo Assist Task 8 retired, Docker-on-release and API docs deferred to #459/#460. See issue #234 |
| 2026-08-05 | [ADR-0021](0021-build-system-versioning-and-release.md) | Build system versioning and release automation proposed. See issue #234 |
| 2026-04-30 | [ADR-0020](0020-fhir-r4-integration.md) | FHIR R4 EHR integration design proposed; stateless GenPRES with bidirectional MedicationRequest translation and G-Standard GPK coding |
| 2026-04-27 | [ADR-0019](0019-shared-clinical-calculations.md) | Shared library clinical calculations accepted; BSA, age, and renal eGFR formulas available to both server and client |
| 2026-04-26 | [ADR-0018](0018-nlp-dose-rule-extraction.md) | LLM-based dose-rule extraction pipeline proposed; multi-stage FSX pipeline with human review gate |
| 2026-04-17 | [ADR-0016](0016-gstand-dose-rule-fallback.md) | G-Standard dose rule fallback for missing adult rules |
| 2026-04-11 | [ADR-0015](0015-security-baseline.md) | Security baseline for the public demo accepted; references the 2026-04-10 security review |
| 2026-04-07 | [ADR-0014](0014-staged-value-expansion-timed-orders.md) | Staged value expansion to prevent value explosion in the constraint solver accepted |
| 2026-03-28 | [ADR-0009](0009-mcp-server-architecture.md) | MCP server architecture accepted |
| 2024-01-01 | [ADR-0001](0001-system-architecture.md) | System architecture accepted |
