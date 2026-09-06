# ADR-0001: System Architecture

**Date**: 2024-01-01

**Status**: Accepted, amended (2026-09-06); § Dependency rule and effects: Proposed

**Related Issues** (amendment): [#378 — Change project architecture](https://github.com/informedica/GenPRES/issues/378),
[#416 — Standard logging library](https://github.com/informedica/GenPRES/issues/416),
[#523](https://github.com/informedica/GenPRES/issues/523) and
[#526](https://github.com/informedica/GenPRES/issues/526) — IO in a type initializer broke test discovery

## Context

GenPRES is a clinical decision support system (CDSS) for medication order management. An explicit architectural baseline is needed to guide development, onboarding, and maintenance decisions and to give the design history of this medical device software a traceable starting point.

### Domain purity — amended 2026-09-06

The original consequence that "server-side F# domain libraries remain pure and testable
independent of the UI" was an intention, not an enforced rule, and by 2026 it did not hold: the
formulary library fetches Google Sheets and a third-party website, the logging library bundles a
file-writing agent that every domain library references, and the order and patient libraries read
the clock. In [#378](https://github.com/informedica/GenPRES/issues/378) the project dependency
graph was drawn and two edges were questioned: `GenFORM.Lib → ZForm.Lib` and `Logging.Lib` sitting
in the core. The question "are ZIndex and ZForm part of the domain model?" was left open.
[#523](https://github.com/informedica/GenPRES/issues/523) and
[#526](https://github.com/informedica/GenPRES/issues/526) showed the cost of an unstated rule: a
network fetch and a file read in module initializers poisoned type initialization for the whole
test assembly.

The code already contains the mechanisms a pure core needs, used correctly in places: the
`Logger` record of functions passed as a parameter, the function-valued `GStandProvider`
resource, the `ofResult` / `derive` resource registry, and the server's `AppEnv` record of ports.
What was missing is the decision that makes them the rule rather than the exception. That
decision is the [Dependency rule and effects](#dependency-rule-and-effects--amended-2026-09-06)
section below.

## Decision

Adopt the SAFE Stack (Saturn, Azure, Fable, Elmish) as the technology foundation for GenPRES. The system is structured as a client-server web application with all logic written in F#.

Two further foundational choices follow from it and are recorded here because they are equally hard to reverse:

- **Google Spreadsheets as the configuration store.** Medication rules and constraints are authored in spreadsheets, downloaded as CSV and parsed at runtime. This lets clinical staff maintain the rule base without a developer, at the cost of coupling the system to an external service. Note that `GENPRES_URL_ID` selects the *server's* sheet only; the client reads a few of its own sheets from IDs hard-coded in `Client/Utils.fs`, so it is not a single switch over all sheet-sourced data.
- **Docker as the production delivery mechanism.**

### Dependency rule and effects — amended 2026-09-06

> **Status of this section**: Proposed (2026-09-06). The rule is decided and the fitness test
> runs in CI, but the code does not yet conform everywhere; the violations that exist are listed
> as allowances in `scripts/CheckDependencyRule.fsx` and their removal is planned in
> [`docs/implementation-plans/378-dependency-rule.md`](../implementation-plans/378-dependency-rule.md).
> The section becomes Accepted when that allow-list is empty.

#### 1. Dependencies point inward

Every project in `GenPRES.sln` belongs to one ring. A project may reference only its own ring or
a ring further in. The ring of a project is decided by behaviour, not by name: a library that
reads an external source, the filesystem, the process environment, the clock or entropy, or that
configures itself from an environment variable, is an adapter and belongs to the outer ring.

The core is the domain: units, patient, solver, operational knowledge rules, orders, interactions.
Core projects contain no call that reaches outside the process. Core projects do not read
configuration; they receive values.

The ring map in `scripts/CheckDependencyRule.fsx` names six rings, innermost first: Core;
Contract, which is `GenPRES.Shared` alone — the types and pure functions that client and server
exchange; Infrastructure, the adapters and the agent runtime; Presentation, the server and the
MCP host; Client; and Tooling, the extraction pipeline, which sits outside the runtime rings. For
the Contract ring the rule is stricter than "inward": Contract references only Contract, and only
Presentation and Client may reference it. Core and Infrastructure never see the contract types,
so the domain cannot come to depend on the wire shape, and the client sees nothing but the
contract.

#### 2. ZIndex, ZForm, NKF and FTK are adapters

They parse external sources (the G-Standaard files, the Kinderformularium website, the
Farmacotherapeutisch Kompas) into source-shaped records and configure themselves from the
environment. The domain owns the contract types it consumes (products, G-Standaard dose rules);
the adapters reference the domain and produce those types, never the reverse. This answers the
open question in #378.

#### 3. Effects enter the core as explicit parameters

A domain function that needs an effect receives it: a `Logger` record, a `now` value, a `newId`
function, a function-valued resource such as `GStandProvider`, or a record of functions such as
the server's `AppEnv`. Resources are declared in the registry and resolved once by the
composition root. There is exactly one composition root per executable (the Fable.Remoting server
and the MCP host), and only composition roots construct loggers, providers and caches. No
computation-expression interpreter, dependency-injection container or logging framework is
referenced from the core.

The known exception is `GStandProvider`: a domain function calls an injected function whose live
implementation reads the G-Standaard on first use. It is accepted because the port is total and
stubbable, and pre-loading every G-Standaard rule for every generic would cost startup time and
memory that has not been justified.

#### 4. The outer ring is the DMZ

The server-side outer ring — the server, the MCP host, the adapters, and the IO half of the
utilities — is the demilitarized zone of GenPRES. It is the only ring that references the network,
the filesystem, the environment, the clock and entropy. It owns every `GENPRES_*` setting and
passes values inward. It hosts authentication, rate limiting, security headers and audit logging,
in one place per concern. It declares the only entry points. It parses at every ingress — the
browser, the MCP client, and the Google Sheets that hold the rule base — so that malformed or
unavailable input becomes an `Error` at the edge, never an empty collection inside the core.

The Fable client runs on a machine the device does not control and is outside the DMZ. It may run
pure calculations for display. It does not compute a dose that is presented as advice, and it does
not fetch rule data on its own; both cross the DMZ through the server.

#### 5. Enforcement

`scripts/CheckDependencyRule.fsx` holds the ring map and checks, for every project in the
solution, that references point inward, that core sources contain no call that reaches outside,
that only the DMZ names a `GENPRES_*` setting, and that only the DMZ declares an entry point. The
violations that exist at the time of this decision are listed in the script as allowances, each
with a reason. An allowance that no longer matches fails the run, so the list can only shrink.
The check skips lines that are whole-line `//` comments, so such a comment may name a setting or
a banned token without an allowance; an inline `//` or a `(* … *)` comment is scanned like code.
The setting check matches the string literal `"GENPRES_`, the form in which a setting
is read; the prefix is the `settingPrefixes` list in the script. The migration is planned in
`docs/implementation-plans/378-dependency-rule.md`.

## Consequences

- Server-side F# domain libraries are meant to be pure and testable independent of the UI. What "pure" means
  and how it is enforced is the
  [Dependency rule and effects](#dependency-rule-and-effects--amended-2026-09-06) section above.
- Client code (Fable/Elmish) compiles to JavaScript and runs in the browser.
- Client and server share one type-safe contract through Fable.Remoting, so an API change that breaks a caller fails at compile time rather than at runtime.
- Editing a production spreadsheet changes the behavior of a running system with no deployment, which is the point — and the risk. It is not instantaneous, though: the server holds resources in a `CachedResourceProvider` with no expiry, so an edit takes effect only after an admin `ReloadResources` (or a restart), and the client's own hard-coded sheets are separate again.
- Proprietary medication cache files are not distributed with the repository; only demo cache files are.

### Consequences of the dependency rule — amended 2026-09-06

- One adapter project appears to receive the Google-Sheets loaders, the caching resource
  provider, and the mapping from G-Standaard records to domain contract types. The logging
  library keeps the `Logger` port and loses the agent runtime, which moves next to the agents.
- Public signatures in the core gain parameters (`now`, `newId`) where they used ambient values.
  Structural equality tests on constructed orders become possible.
- The emergency-list and continuous-medication pages, which today fetch their sheets and compute
  doses in the browser, will route through the server. That changes a dosing path and needs its
  own issue and validation; it is the last step of the migration, not the first.
- The MCP host composes the same ports as the server instead of its own singletons, and stops
  setting environment variables.
- A change that violates the rule fails the fitness test in CI; the reviewer points at this ADR
  instead of arguing from taste.

## Alternatives considered

Recorded retrospectively; the original decision predates this ADR format.

- **A separate JavaScript/TypeScript front end** against an F# REST API. Rejected: it gives up the shared-type contract and forces the domain model to be restated in a second language — unacceptable where the model encodes dosing safety rules.
- **A relational database for the rule base.** Rejected at this stage: it would put a developer or DBA between the clinical author and the rule, which is exactly the bottleneck the spreadsheet approach removes.

### Alternatives to the dependency rule — amended 2026-09-06

- **Domain workflows as a `program` computation expression over an instruction set, interpreted
  at the edge** (the "Safe Clean Architecture" approach proposed in #194 and #226, closed without
  discussion). Rejected: it adds an interpreter between the solver and its callers on the path the
  project has spent effort making fast, it makes the effect boundary harder for a clinical
  reviewer to read than an explicit parameter list, and the code base already has three working
  mechanisms for the same purpose.
- **Treat ZIndex and ZForm as domain.** Rejected: their records are shaped like the G-Standaard
  tables, they decide demo versus production by reading `GENPRES_PROD` themselves, and the
  formulary library maps their output to its own `ProductComponent` on first contact — the
  signature of an adapter.
- **Merge `Logging.Lib` and `Utils.Lib` into one foundation project** (the original proposal in
  #378). Rejected: it would make the file-writing agent a permanent dependency of every core
  library, the opposite of the rule.
- **A standard logging framework referenced from the core** (#416). Rejected: the core's port is a
  two-field record; which framework implements it is a decision for the composition root and can
  change without touching the domain.
- **Leave the rule unwritten and rely on review.** Rejected: the purity consequence of this ADR
  was unenforced from the day it was written, and #523 and #526 were the result.
- **A separate ADR for the dependency rule.** Tried, as a separate ADR-0022 (2026-09-06), and merged back
  here the same day: it restated the architecture this ADR decides and existed only to amend one
  line of it, so it was an amendment, not a second decision.

## References

The concrete code layout deliberately lives outside this ADR, so that it cannot go stale here:

- Build, run, toolchain and folder structure: [DEVELOPMENT.md](../../DEVELOPMENT.md)
- Domain architecture:
  - [Core Domain Model](../domain/core-domain.md)
  - [GenFORM: Free Text to Operational Rules](../domain/genform-free-text-to-operational-rules.md)
  - [GenORDER: Operational Rules to Orders](../domain/genorder-operational-rules-to-orders.md)
  - [GenSOLVER: Order Scenarios to Quantitative Solutions](../domain/gensolver-from-orders-to-quantitative-solutions.md)
- Technical stack: [SAFE Stack](https://safe-stack.github.io/docs/), [Saturn](https://saturnframework.org/), [Fable](https://fable.io/docs/), [Elmish](https://elmish.github.io/elmish/), [.NET 10.0](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- Dependency rule:
  - [ADR-0000: Documentation Rules](0000-documentation-rules.md) — why the rule is an ADR and
    the inventory is not
  - [ADR-0002: MCP Server Architecture](0002-mcp-server-architecture.md) — the second entry point
  - [ADR-0003: Shared Clinical Calculations](0003-shared-clinical-calculations.md) — pure
    formulas may be shared with the client; that decision stands
  - `scripts/CheckDependencyRule.fsx` — the ring map and the fitness test
  - `docs/implementation-plans/378-dependency-rule.md` — the migration
  - Mark Seemann, [Impureim sandwich](https://blog.ploeh.dk/2020/03/02/impureim-sandwich/)
  - Romain Deneau, [Safe Clean Architecture](https://github.com/rdeneau/gitbook-safe-clean-archi)
  - Jeffrey Palermo, [The Onion Architecture](https://jeffreypalermo.com/2008/07/the-onion-architecture-part-1/)
