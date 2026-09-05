# GenPRES documentation

Technical documentation for GenPRES. The rules for what belongs here, and in what form, are
[ADR-0000: Documentation Rules](adr/0000-documentation-rules.md).

| Folder | Holds |
| ------ | ----- |
| [`adr/`](adr/) | Architecture Decision Records — hard-to-reverse decisions only |
| [`domain/`](domain/) | The ubiquitous language: [Core Domain Model](domain/core-domain.md), GenFORM / GenORDER / GenSOLVER specifications, algorithm explainers |
| [`scenarios/`](scenarios/) | Use cases; the [EHR integration](scenarios/integration/README.md) set is an executable model with diagrams |
| [`data-extraction/`](data-extraction/) | Dose-rule extraction pipeline: [glossary](data-extraction/CONTEXT.md), prompt and flowchart specifications |
| [`implementation-plans/`](implementation-plans/) | Per-issue implementation plans (see [`template.md`](implementation-plans/template.md)) |
| [`security/`](security/) | Security reviews and the [baseline in force](security/security-baseline.md) |
| [`code-reviews/`](code-reviews/) | Conformance analyses against external standards and references |
| [`roadmap/`](roadmap/) | Backlog, feature requests, fit-gap analyses |
| [`literature/`](literature/) | Research background |
| [`user-guide/`](user-guide/) | End-user guide ([English](user-guide/en/user-guide.md), [Nederlands](user-guide/nl/gebruikershandleiding.md)) and manual test workflows |

Regulatory (MDR, Medical Device Regulation) documentation — requirements, risk analysis, usability
engineering, validation, post-market surveillance — is maintained in a separate, proprietary
repository and is not part of this one.

For building, running and testing see [`DEVELOPMENT.md`](../DEVELOPMENT.md); for contributing see
[`CONTRIBUTING.md`](../CONTRIBUTING.md).
