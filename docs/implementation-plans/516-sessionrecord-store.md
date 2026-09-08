# Implementation plan for issue 516: GenPRES SessionRecord Store

## Problem description

GenPRES Server keeps no session state between requests (Rule 32). A session's identity and standing
have to live somewhere else, and that somewhere is a database the server writes to. This is what lets
more than one server run at once, and what lets an upgrade drain the old instances instead of dropping
their sessions (Rules 32, 36).

The store is also the referee for the races the launch sequence turns on: one open session per user
and per browser (Rule 8), a launch nonce spent once (Rule 2), an ended session that can never go back
to open (Rule 40). Get the database semantics wrong and those rules fail quietly, usually by refusing
a clinician a session they are entitled to.

This issue covers the private store, and within it the SessionRecord plus the marks and activity
around it. The design is in `docs/scenarios/integration/`: Actor 5, Concept 9, Rules 2, 8 to 14, 21,
30, 32, 36, 40 to 46, and the C9 message set in `Integration.fsx`. Read that model before this plan.
Where the two disagree, the model wins.

The launch endpoint, the OIDC round trip, and the UserRegistry and PatientDataPlatform calls are not
in scope here. The client side is Zaid's work (`zaid/initial-session-interactions`, issue #518). This
plan is for the database.

## Scope

In scope:

- The private-store schema for sessions: an append-only event log, a current-state `session` table, a
  `spent_nonce` table, an `anonymous_refusal` counter, and the audit table.
- A `SessionStore` port covering the C9 messages: open-closing-others (with the `replacing` session),
  check-launch-spent (returning the spending session), touch-if-open, end-if-open, read one, read all
  for a user, mark-delivered, mark-acknowledged, note-mail-used, note-anonymous-refusal.
- The Rule 14 anonymous cap, enforced under the `('anon', 'global')` key lock inside the open
  transaction so concurrent anonymous opens cannot overshoot it.
- Rule 41 in full: an arriving request ends an over-idle session, and a periodic sweep ends the ones
  nobody comes back to.
- The Rule 46 audit writer. Every act writes its audit row inside the same transaction as the act.
- The migration runner, a dev database container, an in-memory port for tests, and an integration
  suite against the real engine.

Deferred to their own issues, built on the schema and migration work this one lands:

- The UserCredential and PIN store (Rules 23, 27, 28, 37).
- PIN reset codes held as MACs (Rule 37).
- The submission idempotency key (Rule 45, first clause only. The conditional-write clause is the
  whole of touch, end and note-mail, which are in scope).
- The audit reader. Who reads it is out of scope (Guarantee 4).
- The clinical store: TreatmentPlan append with the Rule 20 check in the same transaction (Rules 36,
  42). This is the other half of Actor 5 and needs its own plan. It has to land in the same database
  instance as the private store, because Rule 42 puts a session check and a clinical append in one
  transaction.

Out of scope: the launch endpoint, OIDC, UserRegistry, PatientDataPlatform, anything client-side.

## Approaches considered

### Database engine

1. Postgres in dev, SQL Server in prod. The starting suggestion.
2. One engine in both. Postgres everywhere, or SQL Server everywhere. The SQL Server container image
   runs in Docker as readily as Postgres.
3. An ORM that hides the dialect (EF Core).

The launch races depend on behaviour that is not the same across engines: how a write conflict
resolves, whether a filtered unique index behaves the same when an `UPDATE` moves a row out of the
filter, how a serialization failure surfaces. Those are the mechanisms that carry Rules 2, 8 and 40.
Testing them on Postgres and shipping on SQL Server means the property the device leans on for session
safety is only ever exercised on an engine the hospital does not run. For a medical device that is the
wrong trade, so approach 1 is out.

### Data access

1. Dapper with hand-written SQL and a migration runner (DbUp or Evolve). Thin, and every race-critical 
   statement sits in a `.sql` file a reviewer can read.
2. EF Core. Migrations and LINQ come for free, but the change tracker assumes load, mutate, save.
   This store is append-only and its important writes are conditional `INSERT` and `UPDATE ... WHERE`.
   The ORM hides exactly the statements that matter.
3. Marten, a Postgres document and event store. A good fit for an append-only event-sourced record,
   but Postgres-only, which rules out SQL Server, and a large dependency for one family of tables.
4. A SQL type provider. Compile-time database connection is awkward in CI and does not travel across
   engines.

### The concurrency mechanism

This is the real design choice, more than the storage shape.

1. A pure event chain. Each `Opened` event names the session it replaces, and a unique constraint on
   that predecessor decides the race. This is the mechanism Rule 40 describes. The trouble is that
   Rule 8 has two keys, user and browser, and a single-predecessor constraint cannot express both.
   The first open has no predecessor at all. The constraint also has to be written twice, once per
   engine.
2. Partial unique indexes on a mutable projection: `UNIQUE (user_id) WHERE state = 'open'` and the
   same on `browser_id`. Simple to state, but it makes the index the race referee, and that is the
   part whose behaviour differs between Postgres and SQL Server.
3. Take a row lock in a fixed order, on a durable key row rather than on the open session. A
   `session_key_lock` table holds one row per lock scope and key: `('user', user_id)`,
   `('browser', browser_id)`, and `('anon', 'global')` for the anonymous cap. The open ensures its
   rows exist (`INSERT ... ON CONFLICT DO NOTHING`, user key before browser key) and then locks them
   in that same fixed order, `SELECT ... FOR UPDATE` on Postgres or `WITH (UPDLOCK, HOLDLOCK)` on SQL
   Server, then does the close and the insert. User before browser, always, so a deadlock cannot
   form. Because the lock target is the key row and not the session, a first open with no session yet
   still serializes against a concurrent first open for the same key. The lock is a plain row lock,
   so it survives a transaction-mode connection pooler, which an advisory lock would not. The partial
   unique indexes stay, but as a backstop invariant, not as the thing that decides the race.

### Storage model

1. Event log only. Append-only, matches Actor 5, but every read and every constraint then has to walk the log.
2. Event log plus a current-state `session` projection, both written in the same transaction. The log
   is the audit-shaped history; the projection is what reads and locks hit.
3. Projection only. Simplest, but it drops the append-only history Actor 5 states for this store.

## Chosen approach

One engine for dev and prod. Default to Postgres: it is the cheapest to run in CI and locally, and
its partial-index and upsert behaviour is the less surprising of the two. If hospital operations need
SQL Server in production, then dev runs SQL Server too and Postgres comes out of the plan. Someone has
to put that question to the hospital's DBA team before the engine-specific work starts. See open
decisions.

Dapper with hand-written SQL. Migrations run through DbUp, scripts embedded as resources.

Event log plus current-state projection. The log carries the Rule 46 history; the projection carries
the state that reads and locks work against.

Fixed-order row locking on durable key rows for the races, approach 3 above. Spend the nonce, ensure
the `session_key_lock` rows exist, lock `('user', user_id)` then `('browser', browser_id)` for
update, then close any open sessions on those keys and insert. An anonymous open locks
`('anon', 'global')` instead, then counts the open anonymous sessions and refuses above the cap
(Rule 14) inside the same transaction. `READ COMMITTED` with a bounded retry on a serialization or
unique-violation error. The nonce spend is idempotent (`INSERT ... ON CONFLICT (kind, nonce)
DO NOTHING`), so a retry is safe. The partial unique indexes stay as a last-line check that the
invariant held.

Project placement, following the ADR-0001 dependency rule:

- The pure session domain goes in Core. These are the `[ships]` types and transition functions
  already carved out in `Integration.fsx`: `SessionRecord`, `SessionState`, `EndMark`,
  `SessionNotice`, and the functions `owesNotice`, `endWith`, `delivered`, `acknowledged`, `seen`.
  No IO. The transition functions take `now` as a parameter and take a `SessionPolicy` record for the
  timeouts and limits, so no configuration and no clock is compiled into Core. The DUs get
  `[<RequireQualifiedAccess>]`, which the model does not have.
- The `SessionStore` port is defined next to the domain, in Core, because that is what consumes it.
  Not in the adapter.
- A new adapter project, `Informedica.GenPRES.Persistence.Lib`, in the Infrastructure ring, holds the
  SQL implementation, the migrations, and connection handling. It reads `GENPRES_DB_*`, which is
  allowed: it is in the DMZ.
- An in-memory implementation of the port, for fast unit tests of the pure domain and for
  `dotnet run` without a database. It is not a stand-in for the integration tests. Rules 8, 40, 2 and
  41 are statements about database behaviour, and a dictionary behind a lock does not prove them.

Config: a single `GENPRES_DB_CONNECTION` connection string, and it is the one switch. Set, the
composition root wires the SQL adapter; unset, it wires the in-memory port. In production
(`GENPRES_PROD=1`) the server refuses to start when it is unset, the same fail-closed rule as
`GENPRES_PASSWORD`. Demo and a bare `dotnet run` leave it unset and fall back to the in-memory port.
Demo then loses its sessions on restart, which is a divergence from Rule 32. Document it, or run a
throwaway Postgres container in the demo compose file. Discrete `GENPRES_DB_HOST` / `_PORT` / ... vars
are not read; if a deployment needs them later, the composition root builds the connection string
from them and the same one switch and guard still apply.

Wiring: `SessionStore` goes on `AppEnv`, and the composition root picks the SQL or in-memory
implementation from whether `GENPRES_DB_CONNECTION` is set. This meets the `launchSession` and
`redeemSession` stubs already on Zaid's branch. One conflict to settle there first: that branch puts
`SessionContent` with a `SessionId: string` field into `Shared/Types.fs` and returns it over
Fable.Remoting, so the SessionId lands in a JSON body that client script can read. Rule 12 says the
SessionId is a bearer credential that never sits where script can reach it. The session types on that
branch also sit in the Contract ring, which the Infrastructure adapter is not allowed to reference.
Both need working out with Zaid before either side lands.

### Schema sketch

Postgres. The SQL Server version is a separate reviewed script if that engine is chosen, not a
search and replace.

```sql
-- append-only history. Ordered by event_id, never by time.
create table session_event (
    event_id     bigint generated always as identity primary key,
    session_id   text        not null,
    event_type   text        not null,   -- Opened | Touched | Ended | MailNoted
                                         -- | NoticeDelivered | NoticeAcknowledged
    occurred_at  bigint      not null,   -- server clock, descriptive only
    payload      jsonb       not null,
    recorded_at  timestamptz not null default now()
);
create index ix_session_event_session on session_event (session_id, event_id);

-- current state. What reads and row locks work against.
create table session (
    session_id   text primary key,       -- >= 128 bits from a CSPRNG, base64url
    session_no   bigint generated by default as identity,
    user_id      text        null,       -- null => anonymous (Rule 14)
    mail_address text        null,       -- Concept 9, the registry's last answer
    patient_id   text        null,
    browser_id   text        null,       -- see open decision 3
    launch_nonce text        null,
    opened_at    bigint      not null,
    expires_at   bigint      null,       -- absolute lifetime: Rule 10 ends a session
                                         -- "at its absolute lifetime (Rule 30)". Set from
                                         -- SessionPolicy.absoluteLifetime at open, null for none.
    last_seen    bigint      not null,   -- idle clock: refreshed each request (Rule 9),
                                         -- compared against SessionPolicy.idleLimit by the
                                         -- arriving request and by the sweep (Rule 41).
                                         -- Never moves backwards.
    state        text        not null,   -- 'open' | 'ended'
    end_mark     text        null,
    ended_at     bigint      null,
    notice       text        not null,   -- NotOwed | Owed | Delivered | Acknowledged
    notice_at    bigint      null
);
-- Rule 8, as a backstop. The row locks decide the race; this catches a mistake.
create unique index ux_session_open_user
    on session (user_id)    where state = 'open' and user_id    is not null;
create unique index ux_session_open_browser
    on session (browser_id) where state = 'open' and browser_id is not null;

-- Rule 8, Rule 14. Fixed-order lock targets, one row per (scope, key). A row always
-- exists to lock, so a first open with no session yet still serializes against a
-- concurrent one for the same key. scope 'anon' / key 'global' serializes anonymous
-- opens for the Rule 14 cap. Plain row locks, so a transaction-mode pooler does not
-- break them. Rows are never deleted: at most one per distinct user and browser.
create table session_key_lock (
    scope    text not null,            -- 'user' | 'browser' | 'anon'
    lock_key text not null,            -- user_id, browser_id, or 'global'
    primary key (scope, lock_key)
);

-- Rule 2. Launch nonces and, later, token nonces. Keyed on both so the two never collide.
create table spent_nonce (
    kind       text   not null default 'launch',
    nonce      text   not null,
    session_id text   not null,
    spent_at   bigint not null,
    primary key (kind, nonce)
);

-- Rule 46. Anonymous opens refused above the cap, counted per source, not one row each.
create table anonymous_refusal (
    source     text   primary key,
    count      bigint not null,
    updated_at bigint not null
);

-- Rule 46. Every act around a session.
create table audit_entry (
    entry_id bigint generated always as identity primary key,
    at       bigint not null,
    what     text   not null
);
```

### The races, walked through

Two launches of the same user, two different nonces (UC-1 ext 8b). Both nonces are unspent, so both
transactions spend their own nonce and run the whole pipeline. Each locks the `('user', user_id)` key
row in fixed order, so the two run in sequence whether or not the user already had an open session.
The first ends any open session for that user as `Superseded` with its notice `Delivered`, inserts
its own session, writes its audit row. The second, now holding the lock, sees the first's session,
supersedes that, and inserts its own. Both browsers are told a session opened; only the later one
still has it. The earlier browser finds out at its next request (Rule 11). This is the specified
outcome, and it is not a lost update: the database decided which session stands. Two first launches
with no prior session resolve the same way, because the key row is what is locked, not the session.

The anonymous cap (Rule 14). Anonymous opens have no user row and maybe no browser row, so they
serialize on the single `('anon', 'global')` key row instead. Holding that lock, the transaction
counts open anonymous sessions and, if the count is at or above the configured cap, refuses without
writing a SessionRecord and bumps `anonymous_refusal` for the source. Because every anonymous open
takes the same lock, the count each one sees already includes every anonymous session that will
commit before it, so the cap cannot be overshot.

The same nonce presented twice (one Launch, two browsers, or a refresh). The open spends the nonce as
its first write: `INSERT ... ON CONFLICT (kind, nonce) DO NOTHING`. If that inserts a row, the open
goes on to the locks, the closes and the insert. If it inserts nothing, the nonce is already spent, so
the transaction does none of that: it re-reads `spent_nonce`, finds the session the first open
created, and returns that (Rule 2's replay clause, Rule 45). A same-browser retry within the launch
lifetime gets that first answer; anything else is refused and audited. Either way one session exists.

The ended-session case. An open whose generated `session_id` somehow already exists, or a retry of an
open that already committed, must not resurrect a session that has since ended. The insert is
conditional and a retry is answered from the row that is already there, never re-applied.

The mark. `ReplacedInBrowser` and `Superseded` cannot be two independent statements, because a row
that matches both the user and the browser has to come out as `ReplacedInBrowser`: the user did the
opening, so nothing is owed. The close is one ordered set of statements: end the browser's open row as
`ReplacedInBrowser` first, then end the user's other open row as `Superseded` with notice `Delivered`,
then move any already-ended rows of that user from `Owed` to `Delivered`. That last step is why
"every write is `WHERE state = 'open'`" is not quite true: delivering and acknowledging a notice act
on ended rows by definition.

## Open decisions

These want an answer in review of this plan, or in the ADR that goes with it. They are not the
implementer's to guess.

1. The engine. Postgres both sides, or SQL Server both sides. This needs the hospital operations
   answer, and a fallback: if there is no answer by the time step 4 finishes, build on Postgres and
   accept a port cost later. A second question for the same people: will their DBA team allow an
   application-managed schema with migrations run at startup at all? If not, decision 5 changes.
2. Where the pure session domain lives: a new `Informedica.Session.Lib` in Core, or modules inside
   `Informedica.GenCORE.Lib`.
3. What `browser_id` actually is. `Integration.fsx` marks `BrowserId` as `[model]`, not something
   that ships, so the identifier is undecided. It cannot be a value the client makes up, because
   Rule 8's per-browser limit exists to bound the client. Candidates: a device or session claim from
   Entra that the server verifies, or a server-set HttpOnly cookie with an HMAC, accepting that it is
   per-browser-profile and clears with site data. Until this is settled the per-browser index is
   guarding an unauthenticated value.
4. The clock and the tick. `Integration.fsx` uses an `int` tick. Production needs a real clock passed
   in as `now: unit -> int64`. Confirm the unit (Unix milliseconds) for `occurred_at`, `last_seen`
   and `expires_at`.
5. Migration timing: at server startup under a database advisory lock so instances do not race, or a
   separate step in the container entrypoint, or a deploy job. Partly depends on decision 1's second
   question.
6. This one is a design question, not an implementation one. This plan uses a guarded mutable
   projection where Rule 40 and Actor 5 describe a pure append-only event chain. The chain formulation
   cannot carry Rule 8's dual key without extra machinery, which is why the plan goes the other way.
   That is arguably a change to a design input, so it should go back to the V8 owner as a proposed
   amendment (permit a guarded projection beside the log, and say why), rather than being decided
   here.

## Confidence

Medium.

The storage shape and the fixed-order locking are standard, and they carry the rules cleanly. The
event-log-plus-projection split is not novel.

What is not settled is the engine (a forced SQL Server changes the dev setup, the scripts and the
concurrency suite, but not the model), the home of the pure domain, and the `browser_id` question,
which touches three rules and has no answer yet. The design doc's own open question notes that Rules
40 to 45 are stated and not proven under concurrency, so I would not claim more than medium.

## Steps

Each step is one PR, aimed at 200 changed lines or fewer, less where it can be. New F# is prototyped
in a `.fsx` under the relevant `Scripts/` folder first; the PR that lands source files is the
maintainer's. Expect twelve to fifteen PRs, not nine.

1. This plan, and an ADR (next free number, 0006) recording the engine, the access library, the
   migration ownership, and the event-log-plus-projection model. This is the first datastore in the
   system, and ADR-0000 lists a storage mechanism and a third-party dependency as ADR-worthy. The ADR
   is what resolves open decisions 1, 5 and 6.
2. Project scaffolding only. Two `.fsproj` skeletons (session domain, persistence), their entries in
   `GenPRES.sln`, their ring entries in `scripts/DependencyRule.fsx`, and a regenerated
   `ARCHITECTURE.md` diagram. Nothing compiles yet beyond an empty module. This exists because the
   dependency-rule check and `ProjectGraph --check` both fail the moment a project has no ring.
3. The pure session domain. Lift the `[ships]` types and transition functions from `Integration.fsx`
   into the Core project. Add `[<RequireQualifiedAccess>]`, take `now` and a `SessionPolicy` record as
   parameters. Port the script's Expecto tests. No IO, nothing beyond `Informedica.Utils.Lib`. This
   is around 250 lines of types and functions before tests, so it splits: 3a for the types, 3b for
   the transition functions and their tests.
4. The `SessionStore` port and the in-memory implementation. The port is a record of functions in
   Core. The in-memory version uses a lock and dictionaries with the same conditional semantics. Wire
   it onto `AppEnv` behind `GENPRES_DB_CONNECTION` being unset. Unit tests for the port contract
   against the in-memory version.
5. The migration runner. DbUp, the startup migration under an advisory lock, and an empty first
   script. No schema yet.
6. The schema. The six tables above (`session_event`, `session`, `session_key_lock`, `spent_nonce`,
   `anonymous_refusal`, `audit_entry`) and their indexes, as migration scripts. A `compose.dev.yaml`
   or a compose profile for the database, kept out of the published-image `compose.yaml`.
   `.env.example` gains the `GENPRES_DB_CONNECTION` key, commented, defaulting to the in-memory
   fallback.
7. SQL adapter, reads. `readSessionRecord` returning `SessionRecord option * TreatmentPlan option`
   with the plan always `None` until the clinical store lands, `readSessionRecords` for a user,
   `checkLaunchSpent` returning the spending session. Integration tests via Testcontainers.
8. SQL adapter, the open. `openSessionClosingOthers` as the one transaction: the nonce spend first,
   then ensure and lock the `session_key_lock` rows in fixed order, the ordered closes, the insert,
   the audit row, and for an anonymous open the Rule 14 count under the `('anon', 'global')` lock. A
   nonce that is already spent short-circuits to the replay answer. Concurrency tests: many threads
   racing an open for the same user, the same browser, the same nonce, two first launches with no
   prior session, and many anonymous opens against the cap, asserting the ext 8b outcome, a single
   spent nonce, and that the anonymous cap is never exceeded.
9. SQL adapter, touch and end. `touchIfOpen` (never moves `last_seen` back), `endSessionIfOpen`, and
   the arriving-request half of Rule 41.
10. The Rule 41 sweep. A periodic job under an advisory lock that ends sessions past their idle limit
    or absolute lifetime, including the anonymous ones that never idle out.
11. SQL adapter, notices. `markDelivered` (at least once), `markAcknowledged` (checks the
    acknowledging session is the user's own and launched), `noteMailUsed` (conditional, an ended
    session is not touched), `noteAnonymousRefusal`.
12. The reconciliation test. Assert the `session` projection is a fold of `session_event`, and add a
    rebuild-from-log path. Without this the log is write-only.
13. Composition-root switch and the production guard. `GENPRES_PROD=1` without a connection string
    refuses to start, with a clear message, the same shape as `validateProductionPassword`.
14. Docs. The `GENPRES_DB_CONNECTION` key and the dev database container in DEVELOPMENT.md, a
    CHANGELOG entry.

## Testing

- Unit: the pure transitions from step 3, and the port contract against the in-memory version from
  step 4.
- Integration: Testcontainers against the real engine, in its own Ubuntu-only CI job. The GitHub
  matrix runners cannot all run Linux containers (`macos-latest` has no Docker daemon), so this
  project sits outside `GenPRES.sln` and runs on its own, the way `benchmark/` does. The job is
  required on any PR that touches the persistence project.
- The concurrency tests in step 8 are the evidence that the shipped engine enforces the session
  rules. That is regression evidence for the SQL as written, not a claim about the hospital's
  instance, which will differ in version, isolation defaults and pooling. A connection pooler in
  transaction mode breaks advisory locks and session-scoped settings, so the deployed topology is
  itself an open decision.
