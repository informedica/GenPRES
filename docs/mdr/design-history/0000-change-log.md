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
| 2026-08-03 | (retires ADR-0003, ADR-0012) | Spreadsheet column specification retired as a document; the sheet contract now lives on the `Data` record types in `GenFORM.Lib/Types.fs` and is enforced by column-contract tests. See issue #411 |
| 2026-04-30 | [ADR-0020](0020-fhir-r4-integration.md) | FHIR R4 EHR integration design proposed; stateless GenPRES with bidirectional MedicationRequest translation and G-Standard GPK coding |
| 2026-04-27 | [ADR-0019](0019-shared-clinical-calculations.md) | Shared library clinical calculations proposed; BSA, age, and renal eGFR formulas available to both server and client |
| 2026-04-26 | [ADR-0018](0018-nlp-dose-rule-extraction.md) | LLM-based dose-rule extraction pipeline proposed; multi-stage FSX pipeline with human review gate |
| 2026-04-25 | [ADR-0017](0017-lru-solver-memoisation.md) | Session-level LRU memoisation for constraint solver — W2 solver optimisation |
| 2026-04-17 | [ADR-0016](0016-gstand-dose-rule-fallback.md) | G-Standard dose rule fallback for missing adult rules |
| 2026-04-11 | [ADR-0015](0015-security-baseline.md) | Security baseline for the public demo accepted; references the 2026-04-10 security review |
| 2026-04-07 | [ADR-0014](0014-staged-value-expansion-timed-orders.md) | Staged value expansion to prevent value explosion in the constraint solver proposed |
| 2026-03-29 | [ADR-0013](0013-adr-template-based-navigation.md) | Template-based navigation to prescribe view accepted |
| 2026-03-28 | [ADR-0009](0009-mcp-server-architecture.md) | MCP server architecture proposed |
| 2026-03-25 | [ADR-0007](0007-clean-safe-architecture.md) | Clean SAFE architecture accepted (all 4 phases complete) |
| 2025-12-21 | ADR-0012 (retired 2026-08-03) | Resource requirements verified against GenFORM implementation |
| 2026-03-01 | [ADR-0008](0008-agent-architecture.md) | Agent architecture proposed |
| 2024-01-01 | [ADR-0011](0011-universal-layout-overflow.md) | Universal layout overflow design accepted |
| 2024-01-01 | [ADR-0010](0010-analysis-solve-order-triggers.md) | Analysis of SolveOrder trigger paths |
| 2024-01-01 | [ADR-0006](0006-ui-order-view.md) | Quantitative order constraint navigation proposed |
| 2024-01-01 | [ADR-0005](0005-ui-nutrition-view.md) | Nutrition view layout proposed |
| 2024-01-01 | [ADR-0004](0004-ui-wireframes.md) | UI wireframes accepted |
| 2024-01-01 | ADR-0003 (retired 2026-08-03) | Resource requirements specification accepted |
| 2024-01-01 | [ADR-0001](0001-system-architecture.md) | System architecture accepted |
| 2021-12-02 | [ADR-0002](0002-state-of-affairs.md) | State of affairs documented |
