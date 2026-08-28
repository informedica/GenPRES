# ADR-0001: System Architecture

**Date**: 2024-01-01
**Status**: Accepted

## Context

GenPRES is a clinical decision support system (CDSS) for medication order management. An explicit architectural baseline is needed to guide development, onboarding, and maintenance decisions and to satisfy MDR traceability requirements for medical device software.

## Decision

Adopt the SAFE Stack (Saturn, Azure, Fable, Elmish) as the technology foundation for GenPRES. The system is structured as a client-server web application with all logic written in F#.

Two further foundational choices follow from it and are recorded here because they are equally hard to reverse:

- **Google Spreadsheets as the configuration store.** All medication rules and constraints are authored in spreadsheets, downloaded as CSV and parsed at runtime; which spreadsheet is used is selected by the `GENPRES_URL_ID` environment variable. This lets clinical staff maintain the rule base without a developer, at the cost of coupling the system to an external service.
- **Docker as the production delivery mechanism.**

## Consequences

- Server-side F# domain libraries remain pure and testable independent of the UI.
- Client code (Fable/Elmish) compiles to JavaScript and runs in the browser.
- Client and server share one type-safe contract through Fable.Remoting, so an API change that breaks a caller fails at compile time rather than at runtime.
- Editing a production spreadsheet changes the behaviour of a running system with no deployment, which is the point — and the risk.
- Proprietary medication cache files are not distributed with the repository; only demo cache files are.

## Alternatives considered

Recorded retrospectively; the original decision predates this ADR format.

- **A separate JavaScript/TypeScript front end** against an F# REST API. Rejected: it gives up the shared-type contract and forces the domain model to be restated in a second language — unacceptable where the model encodes dosing safety rules.
- **A relational database for the rule base.** Rejected at this stage: it would put a developer or DBA between the clinical author and the rule, which is exactly the bottleneck the spreadsheet approach removes.

## References

The concrete code layout deliberately lives outside this ADR, so that it cannot go stale here:

- Build, run, toolchain and folder structure: [DEVELOPMENT.md](../../../DEVELOPMENT.md)
- Domain architecture:
  - [Core Domain Model](../../domain/core-domain.md)
  - [GenFORM: Free Text to Operational Rules](../../domain/genform-free-text-to-operational-rules.md)
  - [GenORDER: Operational Rules to Orders](../../domain/genorder-operational-rules-to-orders.md)
  - [GenSOLVER: Order Scenarios to Quantitative Solutions](../../domain/gensolver-from-orders-to-quantitative-solutions.md)
- Technical stack: [SAFE Stack](https://safe-stack.github.io/docs/), [Saturn](https://saturnframework.org/), [Fable](https://fable.io/docs/), [Elmish](https://elmish.github.io/elmish/), [.NET 10.0](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
