# Implementation plan for issue 516: GenPRES SessionRecord Store

## Problem description

GenPRES Server keeps no Session state between requests (Rule 32). A Session's identity and
standing live in its SessionRecord in the GenPRES Database, which is what lets more than one
server run and an upgrade drain the old instances instead of dropping their Sessions (Rule 36).
The Database is also the referee for the races the launch turns on: one open Session per User
and per browser (Rule 8), a Launch spent once (Rule 2), an ended Session that never reopens
(Rule 40).

Today the Database is a stand-in. The pure `Session` machine in
`src/Informedica.GenPRES.Server/ServerApi.Session.fs` runs the launch, the enrolment, the signing
and the session-bound compute over one `State` value, and `StubDatabase.makeSessionPort`
(`ServerApi.StubAdapters.fs`) runs it behind one lock, in memory, forgotten at restart. This plan
gives that machine a store.

The design is in `docs/scenarios/integration/`: [uc-01](../scenarios/integration/uc-01-launch.md)
leads the launch sequence, and the V8 document holds the rules cited here (Actor 5, Concept 9,
Rules 2, 8 to 12, 19 to 21, 32, 36, 40 to 46). Read the design and the machine before this plan.
Where the design and this plan disagree, the design wins; where the machine and this plan
disagree, the plan is wrong about the code.

The decisions this plan rests on, and the one it defers, are in
[ADR-0007](../adr/0007-session-persistence.md).

## Scope

In scope:

- A table for everything in `Session.State`: launches, sessions with what they opened with and
  their heartbeats, endings and acknowledgements, credentials, confirmation codes, enrolments,
  signed OrderPlans, data notices, challenges, answered Submissions. The signed record is in
  because `commit` appends the version in the same act that verifies the PIN and re-mints the
  OpenedToken; credentials, codes and enrolments are in because `supplyPin` sets the PIN and
  opens the Session in one act. Splitting any of these across a store and memory would split a
  transaction the code keeps whole.
- The audit table and its writer (Rule 46), inside the same transaction as each act.
- A SQL implementation of `SessionPort`, on SQLite for now.
- Versioned schema scripts and the small runner that applies them.
- The composition switch on `GENPRES_DB_CONNECTION`.
- An integration suite against the real file, in the normal CI matrix.

Out of scope, each a follow-up issue filed in step 8:

- The production engine, the access library and the migration tooling: the ADR-0007 amendment.
- The idle and absolute lifetimes and the sweep (Rules 10, 41). `Session.touch` refreshes
  `Seen` and nothing acts on it yet.
- Anonymous opens and the Rule 14 bound. No anonymous open exists: a browser without a cookie
  has no Session.
- The audit reader, and the audit's retention.
- The signed request of uc-01 step 7.
- What production exposes (#580). Production keeps `Adapters.sessionDisabled` until then.
- A session domain free of the contract types, so that it could move to Core.

## Approaches considered

### The engine

1. Choose the production engine now and develop on it.
2. Develop and test on SQLite, in-process; choose the engine when production needs it.

Nothing in this plan depends on the engine, and the operations question is open. Approach 2,
per ADR-0007 Decision 4. SQLite is one file, one writer, one server: it proves the append-only
shape and the machine over it, and nothing about the final engine's isolation.

### The port

1. Load the whole `State`, run the pure function, commit with a version. Exact and simple, but
   every request reads every patient's record.
2. Rewrite each `Session.*` function into read, decide, write against tables. The pure machine
   and its tests (`SessionMachineTests.fs`, `StubAdapterTests.fs`) go with it.
3. The slice. Load the rows a request can touch, keyed by what the request carries, into a
   `State`; run the pure function unchanged; append what changed, in one transaction. The stub
   and the SQL adapter run the same machine, and `SessionPort` does not change shape.

### The storage shape

1. Current-state rows, updated in place, with locks to decide the races.
2. Append-only rows; per key only the newest row can be open, and it is unless an ending names
   it; the ordering decides the races.

Actor 5 and Rule 40 as amended on 2026-09-09 say 2. The first draft of this plan said 1 and
argued that Rule 8's two keys and the first open with no predecessor needed locks; the amended
rule answers both: newest by id per key, a first opening needs no predecessor.

## Chosen approach

The slice over append-only tables, on SQLite, in the Server project next to the stub.

### What a request loads and what it appends

Each `SessionPort` member loads a `State` holding only the rows its request can touch, keyed by
what the request carries: the nonce or the `state` of a Launch, the session id from the cookie
and that Session's login, the user id of a credential, the patient id of a record, the
idempotency key of a Submission. The pure function runs over that `State` as it runs over the
stub's. The adapter then compares the `State` it got back with the one it loaded and appends a
row for every difference. One transaction per member; SQLite serializes writers by construction,
and the final engine runs the write serializable with one retry (Rule 42).

| `State` field | Tables | The slice reads | An append is |
| ------------- | ------ | --------------- | ------------ |
| `Launches` | `launch_record`, `launch_outcome` | the record by nonce or by `state`, with its outcome | the record at the first presentation; the outcome once, at the callback |
| `Sessions` | `session`, `session_opened_with`, `session_seen` | the row by session id, the newest row for its login, the newest opened-with, the newest heartbeat | a session at an open; an opened-with at an open, at `openVersion` and at a commit; a heartbeat at every `touch` |
| `Endings` | `session_ending`, `session_acknowledged`, and `session` itself | a session row whose login has a newer session row is `SupersededByLaunch` at that row's `opened_at`, whatever became of the newer row; the newest row for the login is open unless an ending names it; `wrong-pin-limit` is a row; an acknowledged ending is hidden; a `closed` row loads as no Session at all | `wrong-pin-limit` at the third wrong PIN; `closed` at `close`, with the acknowledgement |
| `Credentials` | `credential_event` | the newest event for the user id | an event at every change: PIN set, wrong entry, lock, right entry |
| `Codes` | `confirmation_code`, `code_try`, `code_spent` | the newest unspent code for the user id, with its tries counted | a code when mailed; a try per wrong code; spent when the PIN is set, the tries run out, or the last attempt is dropped |
| `Enrolments` | `enrolment`, `enrolment_dropped` | the attempt by id, then the user id it names, then every undropped attempt and the code of that user: `dropEnrolment` spends the code only when no other attempt stands, and `supplyPin` drops every attempt bound to the code | an attempt when the launch suspends; dropped at `dropEnrolment`; all of a user's attempts dropped when the PIN is set or the code is void |
| `Records` | `order_plan` | every version for the patient id, newest first | a version at a commit |
| `Notices`, `Challenges` | `data_notice`, `challenge`, `challenge_spent` | the newest unexpired row for the session id | a row when issued; a newer row replaces; spent at a commit or an `openVersion` |
| `Answered` | `submission_answer` | the row for the session id and the idempotency key | the answer, once, refusals included (Rule 45) |
| the audit | `audit_entry` | nothing | one entry per act, in the same transaction |

Supersession is the one ending with no row of its own. That is Rule 40 as the design states it:
the ordering of the `session` table decides which Session of a login stands, and the loser is
told at its next request because the loader reads its ending off the newer row. The order of the
two reads matters: the loader takes the newest row for the login first and only then asks
whether an ending names it. It never filters ended rows before choosing the newest, since that
would surface a superseded row again once the newer one is closed. A row with a newer row for
its login is superseded whatever became of that newer row. The stub's `close`, which removes
both the Session and its ending, becomes two rows the loader hides.

The slice of an enrolment is wider than its attempt. `dropEnrolment` spends the shared code only
when no other attempt of the user stands, and `supplyPin` drops every attempt bound to the code,
so both load the attempt, then every undropped attempt and the code of the user it names. Two
suspended launches of one User therefore behave as they do on the stub: dropping one leaves the
code to the other, and setting the PIN in one ends both.

What is dropped whole, and when: a launch record and its outcome after the Launch's expiry; a
heartbeat older than the newest for its Session; a data notice and a challenge after two
minutes; an answer after the challenge lifetime; a spent code and its tries. The purge is a
separate statement, never part of a request's transaction, and never touches `session`,
`session_ending` or `order_plan`.

### Config and wiring

`GENPRES_DB_CONNECTION` is the one switch. Set, `Adapters.makeAppEnvWith` wires the SQL
adapter with the same parameters it gives the stub: the clock, the id and code generators, the
salt source, the code mac, the seal verification, the IdentityProvider, UserRegistry,
PatientDataPlatform and MailService ports. Unset, it wires `StubDatabase` as today. For the
interim the value is a SQLite connection string; the default file lives under `data/`, which is
untracked.

Production is untouched by this plan: `Server.fs` swaps in `Adapters.sessionDisabled` when
`GENPRES_PROD=1`, and the fail-closed rule that production requires a connection string lands
with #580, in the shape of `validateProductionPassword`.

The stub seeds four logins with a PIN so that the walkthrough in DEVELOPMENT.md works. The SQL
store gets the same seed from a script that runs only when `GENPRES_PROD=0`, so a demo on SQLite
behaves as the demo on the stub, with Sessions that survive a restart.

### Placement

The adapter is `ServerApi.SqlAdapters.fs` next to `ServerApi.StubAdapters.fs`, in the
Presentation ring, which may read the setting and may reference the contract. The `.sql` scripts
are embedded resources of the Server project. No new project: see ADR-0007 Decision 3.

### Schema sketch

SQLite, kept to the portable core: an integer id the engine generates, `TEXT` for JSON, Unix
milliseconds for time. The engine amendment reviews what its script differs in (id generation,
a JSON type, timestamps). The tables of the first migration; the rest are named in the table
above with their keys.

```sql
-- Rule 2, uc-01 steps 4.2 and 5.7. One row per Launch, written before the identity hop;
-- the outcome is a second row, written once. Both dropped whole after the expiry.
create table launch_record (
    nonce       text primary key,
    state       text not null unique,   -- the callback finds the record by it
    patient_id  text not null,
    public_key  text not null,          -- the browser's JWK; a repeat with it is a replay
    expiry      integer not null        -- the Launch's own
);

create table launch_outcome (
    nonce       text primary key references launch_record (nonce),
    outcome     text not null,          -- opened | refused:<word> | enrolling
    session_id  text null,
    attempt     text null,
    at          integer not null
);

-- Concept 9. One row per open, never changed. Only the newest row for a login can be its
-- open Session, and it is unless an ending names it (Rule 40): newest by id, never by a
-- clock, chosen before the ending is read.
create table session (
    id             integer primary key,  -- the engine's monotonic id
    session_id     text not null unique,
    login          text null,
    user_id        text null,
    user_display   text null,
    user_role      text null,
    patient_id     text null,
    key_thumbprint text null,            -- uc-01 step 5.7; step 7 verifies against it
    opened_at      integer not null
);
create index ix_session_login on session (login, id);

-- What the Session opened with (Rule 19) and the token that names it (Rule 34): written at
-- the open, at openVersion and at a commit. The newest row counts.
create table session_opened_with (
    id           integer primary key,
    session_id   text not null references session (session_id),
    version_id   text null,              -- null: from nothing
    opened_token text not null,
    patient      text not null,          -- json: the patient data the Session shows
    at           integer not null
);

-- Rule 9. Heartbeats. Rows older than the newest for a Session may be dropped whole.
create table session_seen (
    id         integer primary key,
    session_id text not null references session (session_id),
    at         integer not null
);

-- Rule 10. The endings that are acts. Supersession is not a row: it is read off a newer
-- session row for the same login.
create table session_ending (
    session_id text primary key references session (session_id),
    ending     text not null,            -- closed | wrong-pin-limit
    at         integer not null
);

-- Rule 11. The User acknowledged the ending (CloseSession); it is never told again.
create table session_acknowledged (
    session_id text primary key references session (session_id),
    at         integer not null
);

-- Concept 12. The clinical store: every signed version, never changed.
create table order_plan (
    id         integer primary key,
    plan_id    text not null unique,
    no         integer not null,
    patient_id text not null,
    base       text null,                -- the plan_id it was built on
    signed_by  text not null,            -- json UserContext
    signed_at  integer not null,
    scenarios  text not null,            -- json
    patient    text not null,            -- json: the data the plan was signed on
    verified   integer not null          -- 0 | 1: the platform's reading at the challenge
);
create index ix_order_plan_patient on order_plan (patient_id, id);

-- Rule 46. What was done, by whom, to which Session, in the same transaction as the act.
create table audit_entry (
    id         integer primary key,
    at         integer not null,
    session_id text null,
    actor      text null,                -- user_id; null when unknown
    action     text not null,            -- launched | opened | refused | signed | pin-set | ...
    outcome    text not null,            -- ok | refused
    detail     text null                 -- json
);
```

No updatable state column, no partial unique index, no lock table, no `UPDATE` anywhere.

### The races, walked through

Each race is proven on SQLite for the logic of the slice and the appends. The final engine
re-proves it for its own isolation, with the same tests.

**Two launches of the same User, two Launches (uc-01 ext 8b).** Both nonces are unspent, so
both run the whole pipeline. Each loads the newest session row for the login, finds either
nothing or the other's row, and appends its own. Whichever row has the higher id stands. Both
browsers are told a Session opened; the loser learns at its next request, because the loader
reads its ending off the newer row. No lock decided this and no row was rewritten. Two first
launches resolve the same way: neither has a predecessor and neither needs one.

**The same Launch presented twice.** The first presentation appends the `launch_record`. A
repeat from the same public key within the lifetime is answered from the record and its outcome,
exactly as the first time (Rule 2's replay clause, Rule 45). A presentation with another key, or
none, is refused as spent. A reloaded callback within the lifetime is answered from
`launch_outcome`. After the expiry both rows are gone and the Launch is expired.

**A commit against a superseding open (Rule 42).** The commit loads the Session, the credential,
the challenge and the record; a concurrent open for the same login appends a newer session row.
On the final engine the two run serializable and one is retried once; the retried commit reloads,
reads its ending off the newer row, and refuses with `NoSession`. On SQLite the writer lock
orders them and the outcome is the same.

**An ended Session cannot reopen.** There is no row whose insertion makes an ended Session open
again: `session` is written once per open, and an ending, a newer row for the login or a
`wrong-pin-limit` row, is never removed. Closing the newest Session of a login does not hand the
login back to an older one either: the older row still has a newer row above it, so it stays
superseded, and the login has no open Session until the next open appends a row.

## Open decisions

1. The slice, or restructuring the machine. The slice keeps the machine and its tests; what it
   has not proven is that every member's slice can be keyed from its request alone.
   `callback` finds the record by `state`, hence the unique index; `commit` needs the whole
   record for the patient, which `blockedBy` reads.
2. The engine amendment: who asks the hospital's operations, and by when relative to #580.
3. The clock. `now` stays the server clock passed in as a parameter; the order of events is the
   id column, never a timestamp. When more than one server runs, a bound on clock skew needs
   stating for the lifetimes that compare `now` with an expiry.
4. Audit retention and its legal basis. `audit_entry` names mail addresses (Rule 27).
5. The demo seed on SQLite: the same four logins as the stub, or none.

## Confidence

Medium. The append-only shape is the design's own and the machine already exists; the slice is
the part that is new, and it is checked member by member in step 4 before anything else builds
on it. How much of the interim SQL survives the engine choice is unknown, which is why it stays
within the portable core.

## Steps

Each step is one PR of 200 changed lines or fewer. New F# is prototyped in
`src/Informedica.GenPRES.Server/Scripts/` first; the PR that lands source files is the
maintainer's.

1. This plan and ADR-0007.
2. Paket: `Microsoft.Data.Sqlite` in the `Main` group and in the Server's `paket.references`;
   `GENPRES_DB_CONNECTION` in `.env.example`, commented out; the default file path allow-listed
   under `data/`.
3. The script runner: a `schema_version` table, `.sql` files embedded in the Server, applied at
   startup when the connection string is set, each once, in order. A few lines, so that the
   engine amendment can replace it with a tool without a migration of the migrations. With it,
   migration 1: `launch_record`, `launch_outcome`, `session`, `session_opened_with`,
   `session_seen`, `session_ending`, `session_acknowledged`, `order_plan`.
4. The adapter, part 1: the slice loader and the append writer for launches and sessions, and
   the members `present`, `callback`, `find`, `close`, `seen`, `openVersion`. Integration tests
   against a temporary SQLite file in the Server test project, in the normal matrix: two
   launches at once, the same Launch twice, an ended Session never reopens, and the
   `StubAdapterTests` contract run against the SQL port.
5. Migration 2 and part 2: `credential_event`, `confirmation_code`, `code_try`, `code_spent`,
   `enrolment`, `enrolment_dropped`; the members `findEnrolment`, `supplyPin`, `dropEnrolment`.
6. Migration 3 and part 3: `data_notice`, `challenge`, `challenge_spent`, `submission_answer`;
   the members `challenge` and `submit`; the Rule 42 test.
7. `audit_entry` and the writer inside every member's transaction; the purge statement; the
   composition switch in `Adapters.makeAppEnvWith`; the demo seed; DEVELOPMENT.md (the key, the
   file, the seed); the CHANGELOG entry in the commit body.
8. The follow-up issues listed under "out of scope", the engine amendment first.

## Testing

- Unit: the machine is unchanged, so `SessionMachineTests` and `StubAdapterTests` stand as they
  are.
- Contract: the `StubAdapterTests` suite runs against both ports, so the stub and the SQL
  adapter cannot drift.
- Integration: on SQLite, in the normal CI matrix on every OS, against a temporary file. Required
  on any PR that touches the adapter or a migration.
- The race tests are regression evidence for the SQL as written and for the append-only shape.
  They are re-run unchanged on the final engine as part of its amendment, where they become
  evidence about that engine's isolation.
