# ADR-0007: Session Persistence

**Date**: 2026-09-08

**Status**: Proposed (rewritten 2026-09-13; the production engine is deferred to an amendment,
Decision 4); § 3: Accepted (amended 2026-09-16 for ADR-0008; accepted 2026-09-17, when the
session service came to hold what the section says it holds, plan 725 Phase 5, #776 and #778);
§ 2 and § 4 amended 2026-09-17: nothing is deleted, and SQLite is the test and development
database. Acceptance
schedule, per plan 516: § 1 and § 4 when its step 4b lands (a store exists, SQLite runs it, the
switch is the one named), § 2 when its step 6 lands (the launch race proves the append-only
rule on the session table).

**Related Issues**: [#516 — GenPRES SessionRecord Store](https://github.com/informedica/GenPRES/issues/516),
[#580 — Scope switch to expose only the accredited parts in production](https://github.com/informedica/GenPRES/issues/580)

**Related plan**: [`docs/implementation-plans/516-sessionrecord-store.md`](../implementation-plans/516-sessionrecord-store.md)

## Context

The MainEHR integration design ([`docs/scenarios/integration/`](../scenarios/integration/))
requires GenPRES Server to hold no Session state between requests (Rule 32): a Session's identity
and standing live in its SessionRecord in the GenPRES Database, so that more than one server can
run and an upgrade drains the old instances rather than breaking them (Rule 36). The Database is
Actor 5: two stores, one writer, both append-only. It decides the launch races (one open Session
per User and per browser, Rule 8; a Launch spent once, Rule 2; an ended Session that never
reopens, Rule 40) and holds the audit (Rule 46).

GenPRES has no datastore. Since plans
[605](../implementation-plans/605-launch-with-server-stubs.md),
[615](../implementation-plans/615-enrolment-with-server-stubs.md),
[622](../implementation-plans/622-signing-with-server-stubs.md) and
[635](../implementation-plans/635-session-bound-compute.md) the launch, the enrolment, the
signing and the session-bound compute run against a stand-in: the pure `Session` machine in
`src/Informedica.GenPRES.Server/ServerApi.Session.fs`, a set of functions over one `State`
value (launches, sessions, endings, credentials, confirmation codes, enrolments, signed
OrderPlans, data notices, challenges, answered Submissions), run by
`StubDatabase.makeSessionPort` behind one lock and forgotten at restart. Its records carry the
contract types the client and the server share.

Adding a store GenPRES owns is a decision of the kind ADR-0000 §2 reserves for an ADR: a
storage mechanism and a third-party dependency. Which engine runs it in production is such a
decision too, and it is not ready to be made: the hospital's operations have not been asked, and
nothing in the first plan depends on the answer. This ADR records what is decided now and names
what is deferred.

## Decision

### 1. GenPRES owns a relational store

One store for the private data (SessionRecords, LaunchRecords, credentials, confirmation codes,
the audit) and the clinical data (signed OrderPlans): Actor 5's two stores in one instance,
GenPRES Server the only writer, reached through SQL. Relational and not key-value or server
memory: Rule 42 commits a Session check and a clinical append in one multi-row transaction, and
Rules 32 and 36 rule out memory.

### 2. Append-only, nothing deleted — amended 2026-09-17

Every table is insert-only. For a User, and for a browser, only the newest row for that key by
the table's own monotonic id can be the open Session: it is open unless an ending names it, and
every older row for the key is superseded by it, whatever became of it. The newest row is chosen
first and its ending read second, never the other way round, so that closing the newest cannot
surface an older one. An opening may name the Session it replaces, but the ordering decides
(Rule 40; UC-1 ext 8b). A first opening needs no predecessor, and an ended Session cannot be
written back to open. Nothing is deleted either: heartbeats, data notices, challenges, answered
Submissions, confirmation codes and LaunchRecords stay after their lifetime, and a lifetime is
read at load, a row past it loading as absent. No `UPDATE`, no `DELETE`, no purge, no row lock.
A commit that touches two chains (Rule 42) runs serializable and is retried once. The writes a
request makes are the values the `Session` machine returns for it, never rows derived by
comparing states. A test or development database starts fresh by deleting its file.

### 3. The store is an adapter of the `Session` machine — amended 2026-09-16

*Status of this section: Accepted (2026-09-17).* The session service holds its records as
`StoredVersion` values, readable or kept by their identity, and what a Session is open on as a
record on domain values, a challenge as the digest of the plan, and the commit returns the write
as a value for the adapter to run (plan 725 steps 5.1 and 5.2, #776 and #778). What remains of
this section is the store itself, plan 516.

The SQL adapter implements `SessionPort` next to the stub, in the Server project (the
Presentation ring), and runs the same pure functions: it loads the rows a request can touch into
a `State`, runs the function, and appends what changed. It reads `GENPRES_DB_CONNECTION`, which
the DMZ may.

No new Core project. The machine's clinical records (`Records`, the order plan versions) and its
working state (`Challenges`, `Notices`, the patient a Session shows, the head it opened with)
carry the domain types of GenORDER and GenFORM; their Dtos appear only in the adapters, the
server mappers before `SessionPort` and the database adapter at load and write, as
[ADR-0008](0008-contract-model-dto-mapping-boundary.md) decides. On the in-memory stub the
working state lives in memory. Once the store exists, it is stored as Dtos under a structure
version, because a restart must end nothing (Rule 32). The identity fields (`UserContext`,
`OpenedToken`, `SessionEnding`, the refusals) stay contract model types for now, and ADR-0001's
ring rule keeps the contract out of Core and Infrastructure. A session domain free of them is a
refactor with a plan of its own, not a precondition for a store.

### 4. SQLite for development and tests; the production engine is deferred — amended 2026-09-17

Development and tests run on SQLite, in-process, through `Microsoft.Data.Sqlite`. SQLite is the
test and development database and stays in that role: it is what lets the schema, the adapter
and the race tests exist before a production engine is chosen, and what a developer and the CI
matrix keep running afterwards. It is not a candidate for production: one file, one server
process, so it cannot serve Rule 36, and it never runs in production. With `GENPRES_PROD=1` the
server refuses to start when `GENPRES_DB_CONNECTION` is set; that guard stands until #580, when
the production engine's own rule replaces it. Production keeps the session subsystem disabled
until the scope switch (#580) decides what it exposes.

The production engine, the access library and the migration tooling are one linked decision,
recorded as a dated amendment to this ADR before #580 exposes Sessions in production. The
amendment answers: which engine, and whether operations run it; hand-written SQL, a mapper or an
ORM; a migration runner or the scripts as they are; what the SQL as written for SQLite has to
change (id generation, JSON, timestamps). SQLite proves the append-only shape and the machine
over it.
The engine's own isolation behavior is proven by running the same test suite on it.

## Consequences

- First database dependency in the repository: `Microsoft.Data.Sqlite`, in the `Main` Paket
  group, for development and tests.
- `GENPRES_DB_CONNECTION` is the one switch: set, the SQL adapter; unset, the in-memory stub.
  For development and tests the value is a SQLite connection string. Production refuses the key
  until #580, which lands the production engine's rule and the fail-closed rule that production
  requires a store.
- Demo and a bare `dotnet run` keep the stub unless the key is set; a demo on SQLite keeps its
  Sessions across a restart, as Rule 32 says.
- The integration tests run in the normal CI matrix, on every OS, against a temporary file. No
  container job until the engine amendment.
- The SQL stays within the portable core: an integer id the engine generates, `TEXT` for JSON,
  no engine-specific syntax, so that the amendment reviews a per-engine diff rather than a
  rewrite.
- Schema changes are versioned scripts from the first one. Once two server versions run side by
  side (Rule 32's drain on upgrade), a change is expand then contract.
- Within GenPRES a changed row is tampering by definition (V8, open question 5); the store's
  administrator stays outside that guarantee.

## Alternatives considered

| Alternative | Reason rejected |
| ----------- | --------------- |
| No database: server memory, sticky sessions | Rules 32 and 36: several servers, drain on upgrade. |
| A key-value store | Rule 42's multi-row transaction across the two stores; the audit is relational. |
| Choose the production engine now | Nothing in plan 516 depends on it; the operations question is open; an engine chosen without the answer would be chosen twice. |
| A guarded projection with fixed-order row locks | The first draft of this ADR. Rule 40 as amended 2026-09-09: the ordering decides, no lock and no rewrite. |
| A session library in Core | Its types carry the contract, which the ring rule keeps out of Core. |
| An ORM, a mapper, an event store, a migration library | Not rejected: deferred to the engine amendment, which they depend on. |

## References

- [ADR-0000: Documentation Rules](0000-documentation-rules.md) — §2, when a decision is an ADR
- [ADR-0001: System Architecture](0001-system-architecture.md) — the dependency rule and the DMZ
- [ADR-0008: Contract Model, Domain Dto, and the Mapping Boundary](0008-contract-model-dto-mapping-boundary.md)
  — what the store holds: domain Dtos under a structure version, the working state in memory
  on the stub and stored in the store
- [`docs/scenarios/integration/GenPRES-MainEHR-Integration-V8.md`](../scenarios/integration/GenPRES-MainEHR-Integration-V8.md)
  — Actor 5, Concept 9, Rules 2, 8, 32, 36, 40, 42, 46
- [`docs/scenarios/integration/uc-01-launch.md`](../scenarios/integration/uc-01-launch.md)
  — the launch sequence, the LaunchRecord, and the two launches at once
- [`docs/implementation-plans/516-sessionrecord-store.md`](../implementation-plans/516-sessionrecord-store.md)
  — the schema, the adapter and the step sequence
