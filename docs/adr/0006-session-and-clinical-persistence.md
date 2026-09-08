# ADR-0006: Session and Clinical Persistence

**Date**: 2026-09-08

**Status**: Proposed

**Related Issues**: [#516 — GenPRES SessionRecord Store](https://github.com/informedica/GenPRES/issues/516),
[#518 — carry the WorkPlan into a relaunched tab](https://github.com/informedica/GenPRES/issues/518)

**Related plan**: [`docs/implementation-plans/516-sessionrecord-store.md`](../implementation-plans/516-sessionrecord-store.md)

## Context

The MainEHR integration design ([`docs/scenarios/integration/`](../scenarios/integration/)) requires
GenPRES Server to hold no session state between requests (Rule 32). A session's identity and standing
must live in a store the server writes to, so that more than one server instance can run (Rule 36) and
an upgrade can drain the old instances rather than drop their sessions. The same store is the arbiter
for the launch-sequence races: one open session per user and per browser (Rule 8), a launch nonce
spent once (Rule 2), an ended session that never reopens (Rule 40). It also holds the append-only
audit of every act around a session (Rule 46).

GenPRES has no datastore today. Every `.fsproj`, `paket.dependencies` and `paket.references` in the
repository is free of a database dependency; configuration lives in Google Sheets and local cache
files. ADR-0001's consequences describe a system with no persistence, and its rejected alternatives
include a relational database for the rule base. ADR-0004 (Superseded) recorded a "stateless GenPRES,
FHIR-persistent EHR" stance. Adding a database GenPRES owns is a decision of the kind ADR-0000 §2
reserves for an ADR: a storage mechanism, a third-party dependency, and an architectural foundation.

This ADR covers the private store (sessions, nonces, the audit) that issue #516 delivers, and the
clinical store (signed TreatmentPlans) that follows in its own issue. Both are Actor 5 of the design.
The engine, the access library, the storage model and the concurrency mechanism are one linked
decision, because a different engine or a different model changes all of the others.

The status is Proposed. It becomes Accepted when two things hold: hospital operations have confirmed
the production engine (see Decision 2), and the V8 design owner has accepted the deviation in
Decision 4 as an amendment to Rule 40 and Actor 5.

## Decision

### 1. GenPRES owns a relational database

GenPRES gains a relational database with two logical stores, one writer (GenPRES Server), matching
Actor 5. The private store holds SessionRecords, spent nonces, the anonymous-refusal counter and the
audit. The clinical store holds signed TreatmentPlans. Both stores live in the same database instance,
because Rule 42 commits a session check and a clinical append in one transaction. This departs from
the stance ADR-0004 recorded (already Superseded): the EHR remains the system of record for patient
data, but GenPRES now owns its own sessions and its own signed-plan history.

### 2. One database engine for development and production

The same engine runs in development and in production. The default is PostgreSQL. If hospital
operations require SQL Server in production, development runs SQL Server too.

The launch races depend on behavior that differs between engines: how a write conflict resolves,
whether a filtered unique index behaves the same when an `UPDATE` moves a row out of the filter, how a
serialization failure surfaces. Those mechanisms carry Rules 2, 8 and 40. Validating them on one
engine and shipping on another would leave the property the device relies on for session safety
untested on the engine the hospital runs.

### 3. Dapper with hand-written SQL, migrations through DbUp

Data access is Dapper over hand-written SQL. Every race-critical statement sits in a reviewable `.sql`
file. Schema migrations run through DbUp with scripts embedded as resources.

An ORM (EF Core) is not used: its change tracker assumes load, mutate, save, while this store is
append-only and its important writes are conditional `INSERT` and `UPDATE ... WHERE`. Marten is not
used: it is Postgres-only, which would foreclose Decision 2.

### 4. Append-only event log plus a current-state projection

Each store keeps an append-only event log as its history, and a current-state projection table
written in the same transaction as the log. Reads and row locks work against the projection; the log
is the audit-shaped record and can rebuild the projection.

Rule 40 and Actor 5 describe a pure event chain in which a unique constraint on each event's
predecessor decides races. That formulation cannot express Rule 8's two keys (user and browser)
without extra machinery, and the first open has no predecessor. This ADR chooses a guarded projection
beside the log instead. That is a change to a design input, so it is put to the V8 owner as a proposed
amendment: permit a guarded projection beside the log, and record why the chain formulation was not
enough.

### 5. Fixed-order row locking on durable key rows decides the races

`OpenSessionClosingOthers` runs as one transaction that spends the nonce first, then takes fixed-order
row locks on a `session_key_lock` table holding one row per lock scope and key. The lock target is the
key row (`('user', user_id)`, then `('browser', browser_id)`), never the open session, so a first
open that has no session yet still serializes against a concurrent first open for the same key. An
anonymous open locks a single `('anon', 'global')` row and enforces the Rule 14 cap under it, so
concurrent anonymous opens cannot overshoot the cap. User before browser, always, so a deadlock
cannot form. Then it closes the open sessions on those keys and inserts the new one. The locks are
plain row locks, so a transaction-mode connection pooler does not break them, which an advisory lock
would. Isolation is `READ COMMITTED` with a bounded retry on a serialization or unique-violation
error; the nonce spend is idempotent (`INSERT ... ON CONFLICT DO NOTHING`), so a retry is safe. The
partial unique indexes on the open rows stay as a backstop that catches a mistake, not as the
mechanism that decides the race.

### 6. Placement follows the ADR-0001 dependency rule

The pure session domain (the `SessionRecord` types and their transition functions, lifted from the
`[ships]` sections of `Integration.fsx`) goes in Core, takes `now` and a policy record as parameters,
and does no IO. The `SessionStore` port is defined next to that domain, since the domain is what
consumes it. A new Infrastructure-ring project, `Informedica.GenPRES.Persistence.Lib`, holds the SQL
implementation, the migrations and the connection handling, and reads the `GENPRES_DB_CONNECTION`
setting. An in-memory implementation of the port serves unit tests and a bare `dotnet run`.

## Consequences

- First database dependency in the repository. `Npgsql` (or `Microsoft.Data.SqlClient`), `Dapper` and
  `DbUp` enter the `Main` Paket group. Two new projects (`Informedica.Session.Lib` or a GenCORE
  module set, and `Informedica.GenPRES.Persistence.Lib`) enter `GenPRES.sln` and the ring map in
  `scripts/DependencyRule.fsx`, and `ARCHITECTURE.md`'s diagram is regenerated.
- `GENPRES_DB_CONNECTION` is the single switch: set, the SQL adapter is wired; unset, the in-memory
  port. Production (`GENPRES_PROD=1`) requires it and the server refuses to start without it, the same
  fail-closed rule as `GENPRES_PASSWORD`.
- Demo and a bare `dotnet run` fall back to the in-memory port. Demo then loses its sessions on a
  restart, a documented divergence from Rule 32.
- CI gains a container-based integration job. GitHub's `macos-latest` runner has no Docker daemon, so
  the integration test project sits outside `GenPRES.sln` and runs on its own Ubuntu job, as
  `benchmark/` does. It is required on any PR that touches the persistence project.
- The clinical store is constrained to the same database instance as the private store (Decision 1).
- Rule 8's per-browser limit, and the partial index that backs it, are only as strong as the browser
  identifier, which is not yet designed (plan open decision 3). Until that is settled the per-browser
  guarantee is weaker than the per-user one.
- Every schema migration after the first is expand then contract: a drain-on-upgrade runs the old and
  new server versions side by side, so a migration must not break the previous version's statements.
- If hospital operations choose SQL Server, the schema is a separately reviewed script per engine, not
  a search and replace: identity columns, JSON storage, timestamps, string collation and the upsert
  form all differ.
- The deployed connection topology is an open question for the migration runner, which takes a
  database advisory lock so instances do not race a migration; a connection pooler in transaction mode
  breaks advisory locks and session-scoped settings. Decision 5 does not depend on advisory locks: it
  uses plain row locks on `session_key_lock`, which a pooler does not break.

## Alternatives considered

| Alternative | Reason rejected |
| ----------- | --------------- |
| Postgres in dev, SQL Server in prod | Race semantics differ between engines; session safety would be tested on an engine the hospital does not run (Decision 2). |
| No database: server memory with sticky sessions | Rules 32 and 36 require several servers and drain-on-upgrade, which shared memory cannot give. |
| A key-value store (Redis) instead of an RDBMS | Rule 42 needs a multi-row transaction spanning the session and clinical stores, and the audit is relational. |
| EF Core | Hides the conditional `INSERT`/`UPDATE ... WHERE` statements that carry the rules. |
| Marten (Postgres event store) | Postgres-only, forecloses Decision 2; a large dependency for one family of tables. |
| Pure event chain per Rule 40 as written | Cannot carry Rule 8's dual key without extra machinery; deferred to the V8 amendment in Decision 4. |
| Partial unique index as the race decider | The one part whose behavior is not the same across the two candidate engines. |

## References

- [ADR-0000: Documentation Rules](0000-documentation-rules.md) — §2, when a decision is an ADR
- [ADR-0001: System Architecture](0001-system-architecture.md) — the dependency rule and the DMZ
- [ADR-0004: FHIR R4 Integration](0004-fhir-r4-integration.md) — the superseded persistence stance
- [`docs/scenarios/integration/GenPRES-MainEHR-Integration-V8.md`](../scenarios/integration/GenPRES-MainEHR-Integration-V8.md)
  — Actor 5, Concept 9, Rules 2, 8, 40, 42, 46
- [`docs/implementation-plans/516-sessionrecord-store.md`](../implementation-plans/516-sessionrecord-store.md)
  — the schema, the port and the step sequence
- [DbUp](https://dbup.readthedocs.io/), [Dapper](https://github.com/DapperLib/Dapper)
