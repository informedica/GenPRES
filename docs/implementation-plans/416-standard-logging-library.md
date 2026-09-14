# Implementation plan for issue #416

## Problem description

[#416](https://github.com/informedica/GenPRES/issues/416): the CompositionRoot hard-codes a
console write for every request (`ServerApi.CompositionRoot.fs:79`, `ServerApi.Compute.fs:36/58/69`),
which `ploeh` flagged as unconditional stdout traffic with no clear consumer once the app is
containerized and scaled beyond one instance. The subsequent discussion (`halcwb`, `ploeh`, `7sharp9`) 
converged on three separable concerns that this plan keeps separate:

1. Production/audit logging request tracing, errors, admin actions. Needs to be structured, 
   level-filtered, and off by default in a way that doesn't litter stdout.
2. GenSOLVER research trace logging `halcwb`'s 26,555-line calculation trace, used for debugging 
   dose scenarios and now for a university math-department collaboration studying solver behaviour. 
   `7sharp9`'s closing comment settled this: return trace data from core functions rather than performing 
   IO inside them, which the domain already does in part (`GenSOLVER.Types.Events.Event`, `GenORDER`'s 
   `OrderLogging`, `GenFORM`'s `FormLogging`).
3. Technology choice `7sharp9` recommended Serilog over alternatives: structured events map directly 
   onto the existing `IMessage` discriminated unions instead of string parsing, it's async-safe at the 
   scale needed here.

This plan is already decided and scoped: [#378](https://github.com/informedica/GenPRES/issues/378) 
Phase 1 ("logging inversion", [`docs/implementation-plans/378-dependency-rule.md`](378-dependency-rule.md#phase-1--logging-inversion-a))
already plans to split `Informedica.Logging.Lib` (the `Logger`/`Event`/`Level`/`IMessage` port stays; 
`AgentLogging` and the console/file sink constructors move out), to build the concrete logger at the 
server's and MCP host's composition roots, and to replace every `ConsoleWriter` call below an injected 
`Logger` with a `Logger` call. What #378 leaves open is what the sink at the composition root is built 
from. This plan answers that: Serilog, per `7sharp9`'s recommendation, landing inside #378 Phase 1 step 2.

### What "hard-coded to the console" actually covers

Two independent, un-unified logging paths exist today, not one:

- `Informedica.Logging.Lib` (`Logger`/`Event`/`IMessage`/`Level`, `AgentLogging`): the
  intended port. Referenced by only 3 projects (`GenFORM.Lib`, `GenSOLVER.Lib`, the server) even
  though `GenORDER.Lib` also defines `OrderLogging.fs` against it.
- `Informedica.Utils.Lib.ConsoleWriter` (`Console.fs`): an unconditional, always-colored
  `System.Console` writer that reads `GENPRES_DEBUG` itself via `Env.getItem`. It is called
  directly bypassing the `Logger` port entirely in three tiers:
  - 21 files in Core-ring libraries (`Utils.Lib`, `GenUNITS.Lib`, `GenSOLVER.Lib`,
    `GenFORM.Lib`, `GenORDER.Lib`) are already named, file by file, in the
    `scripts/CheckDependencyRule.fsx` allow-list with reason `"console write below the injected
    Logger; route through Logger (Phase 1)"` (the `viaLogger` label) a tracked, not newly
    discovered, violation of [ADR-0001](../adr/0001-system-architecture.md)'s dependency rule,
    and exactly the scope of #378 Phase 1 step 3.
  - 5 server files (`Server.fs`, `ServerApi.Compute.fs`, `ServerApi.CompositionRoot.fs`,
    `ServerApi.Services.fs`, `Server/Logging.fs`) are *not* dependency-rule violations the
    server is the DMZ, where console IO is architecturally allowed but they are functionally
    the same problem `ploeh` reported, so this plan fixes them alongside the Core-ring sites even
    though the fitness test doesn't require it.
  - 9 files in adapter-ring libraries (`ZIndex.Lib`, `ZForm.Lib`, `NKF.Lib`) call
    `ConsoleWriter` too, but aren't scanned by `CheckDependencyRule.fsx` at all adapters doing
    IO is the point of the ring. Left as Phase 2 consistency work below, not a Phase 1
    requirement.

`ploeh`'s literal complaint the CompositionRoot write is one specific instance of the second
path: `Compute.bound` calls `writeInfoMessage`/`writeErrorMessage` unconditionally on every
request, with no level filter and no relationship to `GENPRES_LOG`/`GENPRES_PROD`. Fixing only
that one call site would leave the other 34 (21 Core-ring + 4 remaining server + 9 adapter-ring) untouched.

## Approaches

1. Keep the bespoke `AgentLogging` agent, only silence/redirect the CompositionRoot write. Fixes `ploeh`'s 
   literal report but leaves the ConsoleWriter path untouched, leaves the ring-buffer/flush-timer/`FileWriterAgent` 
   machinery unproven under concurrent load (`ploeh`'s contention concern, never measured per `halcwb`'s own comment), 
   and leaves the dependency-rule violation in place.
2. An `IMessage` discriminated union can use Serilog's structured-event destructuring (`{@Event}`).
3. Serilog as the sink technology, `Logger`/`Event` kept as the domain-facing port (chosen).
   Matches #378 Phase 1's shape exactly: core libraries keep seeing only the small
   record-of-functions port (already ADR-0001-compliant where it's used), and only the DMZ composition roots 
   (`Informedica.GenPRES.Server`, `Informedica.MCP.Server`) reference Serilog.

## Chosen approach

Concretely:

- A Serilog `ILogger` is built once per composition root from `GENPRES_LOG`, exactly as
  `Server/Logging.fs`'s `loggingLevel` parses it today (`d`/`i`/`w`/`e`; unset means logging is
  off, per `loggingEnabled`) `GENPRES_DEBUG` plays no part in `loggingLevel` today and none is
  added here; it stays what it already is, the flag `Console.fs`'s `writeDebugMessage` reads on
  its own to decide whether to print at all. For the server (built in `Server.fs`'s
  `application { }` CE via its host configuration hook) this sinks to `Serilog.Sinks.Console` in
  development and `Serilog.Sinks.File` (rolling, one sink per `LoggerType` the way
  `getRecommendedLogPath componentName` already names files) in the modes that write to disk today. 
  For the MCP host, which builds no logger of any kind today (`Program.fs` passes `FormLogging.noOp` 
  throughout and never writes to console), this is new composition-root wiring rather than a port of 
  an existing pattern its levels and sinks get scoped when step 2 reaches it, not assumed by analogy
  to the server. `Serilog.Sinks.Async` wraps the file sink so the "completely async, no direct reads" 
  property `halcwb` already relies on is preserved without hand coding.
- A small adapter (`Server/Logging.fs`, new function, working name `SerilogBridge.toLogger`) turns 
  that `ILogger` into a `Logger` record: `Log = fun ev -> serilogLogger.Write(toSerilogLevel ev.Level, 
  "{EventType} {@Event}", ev.Message.GetType().Name, ev.Message)`, 
  `Enabled = fun level -> serilogLogger.IsEnabled(toSerilogLevel level)`. Every existing `IMessage` 
  case (the `Request`/`Info`/`Warning`/`Error` server messages, `SolverMessage`, `OrderLogging`'s and
  `FormLogging`'s message types) becomes a Serilog structured property for free no change to `IMessage` types.
- `Informedica.Logging.Lib.AgentLogging` (the `MailboxProcessor`, `RingBuffer`, flush-timer,
  `FileWriterAgent` wiring `Logging.fs:217-729`) is deleted once the composition roots build their `Logger` 
  from Serilog instead of `AgentLogging.createAgentLogger`. This is the "regular logging in parallel, 
  don't remove current logging yet" step `halcwb` proposed, except realized as "new sink behind the same 
  port" rather than "second port" the domain call sites (`Logging.logInfo`, `logDebugLazy`, etc.) do not change.
- The ConsoleWriter migration (the 21 Core-ring sites, already inventoried in
  `scripts/CheckDependencyRule.fsx` and scoped as #378 Phase 1 step 3) is a prerequisite for those
  sites to reach Serilog at all; it is not re-scoped here, only sequenced against it (see Steps).
- GenSOLVER's research trace (`halcwb`'s use case) stays a separate concern by design: it already
  returns `Events.Event` data rather than performing IO (`SolverLogging.fs`), so it composes with
  whatever sink the caller wires up. The practical win once Serilog is in place: pointing the
  `SolverLogging` events at a dedicated Debug-level Serilog file sink turns the flat 26,555-line
  text trace into structured, queryable JSON (one JSON object per solving step, filterable by
  `Equation`/`Variable` fields) directly addressing `halcwb`'s own complaint that the trace is
  hard to search, without touching the solver's signatures.

### Package and dependency-rule placement

Add to `paket.dependencies`' `Main` group (Serilog's own core has no transitive surface beyond
itself and the two sinks):

```text
nuget Serilog
nuget Serilog.Sinks.Console
nuget Serilog.Sinks.File
nuget Serilog.Sinks.Async
```

`paket.references` lists them only in `Informedica.GenPRES.Server` and `Informedica.MCP.Server` 
(Saturn already brings ASP.NET Core hosting into the server; the MCP host references 
`Microsoft.Extensions.Hosting` already). No core or adapter library (`GenSOLVER.Lib`, `GenFORM.Lib`, 
`GenORDER.Lib`, `GenUNITS.Lib`, `ZIndex.Lib`, `ZForm.Lib`) gets a Serilog reference 
`scripts/CheckDependencyRule.fsx`'s project-reference check enforces this the same way it enforces 
every other inward-pointing edge.

### Security note (the Log4j concern `halcwb` raised)

Log4j's CVE-2021-44228 came from JNDI lookups triggered by *message content* being interpreted
as a lookup expression at runtime a feature of `log4j-core`'s pattern layout, not a property of
"logging libraries" generally. Serilog's message templates (`"{EventType} {@Event}"` above) are
ordinary strings parsed once per call, not compiled or checked by the F# compiler; the safety
property that matters here is different: an attacker-controlled string that reaches a template
*argument* is captured as a property value and is never re-parsed as a template itself, so it
cannot inject a new placeholder or trigger a second round of expansion. (A mismatched placeholder
count is a runtime non-issue in Serilog, not a compile-time one it degrades to an unmatched
property, it does not throw. `Serilog.Analyzers` adds a Roslyn analyzer that flags
template/argument mismatches at build time; it's not in the package list above because nothing
here depends on it for correctness, but it's cheap to add if the earlier signal is wanted.) The
residual risk here is entirely at the sink choice: stick to `Console` and `File` sinks (no network
sink, no sink that deserializes external input), and the `Request` message's `clientIP`/`path`
fields are logged as opaque property values, never formatted into the template string itself.

## Confidence

- High for the server-side composition-root swap (Serilog behind the existing `Logger` port,
  `AgentLogging` deletion): isolated to the DMZ, 5 files (`Server/Logging.fs`,
  `ServerApi.CompositionRoot.fs`, `ServerApi.Compute.fs`, `ServerApi.Services.fs`, `Server.fs`),
  and the port's call sites (`Logging.logInfo`/`logDebugLazy`/etc.) do not change. The regression
  net for this step is `Informedica.GenPRES.Server.Tests` (`StubAdapterTests.fs`,
  `ResourceErrorTests.fs`), which already exercises `Compute.bound`, `Compute.logged` and
  `CompositionRoot.compose` directly and repeatedly not `GenSOLVER.Tests`/`GenORDER.Tests`/
  `Logging.Tests`, which test the domain libraries and never touch this code. Those three remain
  the right regression net for step 1 (the `Logging.Lib` split) specifically, where the port's
  shape is what's under test.
- Medium for the MCP host specifically: `Informedica.MCP.Server/Program.fs` has no logging
  today it passes `FormLogging.noOp` into `Api.getCachedProviderWithDataUrlId` and never builds
  a `Logger` or writes to console. Wiring Serilog in here is new composition-root design (what an
  MCP session needs logged, at what level, with no existing request-tracing precedent to port),
  not a mirror of the server's pattern, and no test suite exercises it today.
- Medium for the full ConsoleWriter → `Logger` migration (21 Core-ring files plus the 5 server
  files, across 6 libraries): same shape and scale as the `failwith` migration in
  [#419](https://github.com/informedica/GenPRES/issues/419), and several of those sites are in
  functions that do not currently take a `Logger` parameter at all, so threading it through is a
  signature change, not a one-line swap. The Core-ring portion is #378 Phase 1 step 3's scope,
  already file-inventoried there; this plan does not re-estimate it.
- Medium on the GenSOLVER-trace-as-Serilog-sink idea specifically: it is a genuine improvement
  over a flat text file, but `halcwb` needs to review that JSON Lines is a workable format for 
  their existing analysis tooling. Flag this as a question before building it (see Steps, item 5).

## Steps

Each commit should try to stay under ~200 changed lines per `CONTRIBUTING.md`; phases with large work should be split
further where it makes sense to compartmentalise, but should not be split for the sake of it.
Numbering continues from #378's phase numbers since step 1-2 below are #378 Phase 1 steps 1-2,
now made concrete with Serilog as the chosen technology.

1. (#378 Phase 1 step 1 prerequisite, if not already landed) Split `Logging.fs`: `Logger`,
   `Event`, `Level`, `IMessage`, `noOp`, `create`, `combine`, `logLazy` and friends stay in
   `Informedica.Logging.Lib`; `createConsole`, `createFile` and the whole `AgentLogging` module
   move to `Informedica.Agents.Lib` (flips the reference so `Agents.Lib → Logging.Lib`, matching
   the target ring map). No behaviour change; verified by `Logging.Tests`.
2. Add the Serilog packages (Main group, Server + MCP.Server `paket.references` only). Build
   `SerilogBridge.toLogger` in `Server/Logging.fs`, replacing `getConfig`/`createAgentLogger`
   call sites with a Serilog-backed `Logger` per `LoggerType`. Delete `AgentLogging` once nothing
   references it (from step 1's new home in `Agents.Lib`). Then, separately, give the MCP host
   (`Informedica.MCP.Server/Program.fs`) its first `Logger` at all: it currently passes
   `FormLogging.noOp` and writes nothing anywhere, so this is new composition-root code, not a
   port of the server's `LoggerType`/level scheme decide what an MCP session needs logged (tool
   calls, at minimum) before wiring the sink.
3. Swap the unconditional writes in `ServerApi.Compute.fs`'s `bound` (`:36/58/69`, used by every
   computing member) and `logged` (`:77/79`, used by `processLaunch`/`processSession`/
   `processSigning`/`processAdmin` the launch, session, PIN and signing paths), plus
   `ServerApi.CompositionRoot.fs:79` and `ServerApi.Services.fs:238/452`, for `Logger` calls at
   the appropriate level (`Info` for request start/finish, `Error` for the catch), gated by the
   same `GENPRES_LOG` check `loggingEnabled` already computes. This is the direct fix for
   `ploeh`'s report, and closes out all 5 of the server files named above, not just the two lines `ploeh` cited.
4. (#378 Phase 1 step 3, sequenced here) Migrate the 21 Core-ring `ConsoleWriter`/`printfn`
   call sites tagged `viaLogger` in `scripts/CheckDependencyRule.fsx`'s allow-list to the injected
   `Logger`, one library per commit, in the order the allow-list already lists them: GenSOLVER
   (`Solver.fs`, `Variable.fs`, `Equation.fs`) → GenORDER (`Order.fs`, `OrderVariable.fs`,
   `Medication.fs`, `OrderProcessor.fs`, `Api.fs`, `EquationMapping.fs`, `Utils.fs`,
   `Exceptions.fs`, `Nutrition.fs`, `OrderLogging.fs`) → GenFORM (`Product.fs`, `RenalRule.fs`,
   `Resources.fs`) → GenUNITS (`UnitsParse.fs`, `ValueUnit.fs`) → Utils (`Json.fs`,
   `BCL/Int32.fs`, `BCL/BigInteger.fs`). That's the 21. Each commit removes the corresponding
   `allowToken` line from `scripts/CheckDependencyRule.fsx`.

   `Console.fs` itself is a 22nd file, tagged separately in the allow-list as `utilsSplit`
   ("IO module in Utils.Lib; leaves the core with the Utils split (Phase 2)") it is not one of
   the 21 `viaLogger` sites. Pull it into this same step anyway, as a last commit once the 21 land:
   `ConsoleWriter` becomes dead code the moment nothing calls it directly, at which point deleting
   it (and with it the `GENPRES_DEBUG` self-read in `writeDebugMessage`) is a one-line `allowFile`
   removal, not a rewrite. This is a deliberate, small pull-forward of one Phase 2 item, not a
   re-scope of Phase 2 itself record it as such in the commit that does it.

   The 9 adapter-ring sites (`ZIndex.Lib`, `ZForm.Lib`, `NKF.Lib`) are not part of this step; see Phase 2.
5. Before building it: ask `halcwb` whether a Serilog JSON-Lines file sink for the GenSOLVER
   research trace (Debug level, one file per `OrderContext` solve as today) is workable for the
   math-department analysis tooling, or whether the existing flat-text `SolverLogging` formatter
   needs to stay as an additional, separate sink alongside the structured one. Either way, no
   change to `GenSOLVER.Lib`'s public signatures this only changes what consumes `Events.Event`.
6. Each commit: `dotnet run Format`, `dotnet run servertests`, `dotnet fsi scripts/CheckDependencyRule.fsx`
   (once wired into CI per #378 Phase 0 step 3; run manually until then), Fable compile only if
   `Shared`/`Client` is touched (it shouldn't be this plan never reaches the client).
7. Done when: every write in `ServerApi.Compute.fs` (`bound` and `logged`),
   `ServerApi.CompositionRoot.fs`, and `ServerApi.Services.fs` is a level-gated `Logger` call
   (closes `ploeh`'s report in full, not just the two lines cited), `AgentLogging` no longer
   exists, `scripts/CheckDependencyRule.fsx`'s `viaLogger`-labelled allowances are empty, the
   `Console.fs` `allowFile` entry is gone too, and `Serilog` appears in exactly two
   `paket.references` files.

## Phase 2 efficiency, general logging quality (after the above lands)

Requested separately from standardization; tracked here so it isn't lost, done only once the
Serilog composition root is stable enough that these changes are visible in real output:

- Eager vs. lazy audit. `Logging.fs` already documents the split (`logInfo` is eager,
  `logInfoLazy` defers message construction until `Enabled` says a sink would consume it) but the
  doc comments suggest the lazy variants are under-used. Grep every `logInfo`/`logWarning`/`logDebug`
  call site that formats a non-trivial structure (`%A` dumps of `Equation`/`Variable`/`Order`,
  anything building a string via `sprintf`/`$"..."` before the call) and convert the ones on hot
  paths (the solver loop, per-request order-plan processing) to the `*Lazy` API that already
  exists. Cross-reference with `fsharp-coding.instructions.md`'s "message templates instead of
  string interpolation" idiom while auditing several `*Logging.fs` formatters build strings via
  `$"..."` today where a Serilog structured property would let the sink do the formatting.
- Adapter-ring console usage. The 9 `ConsoleWriter` sites in `ZIndex.Lib`, `ZForm.Lib` and
  `NKF.Lib` (`ATCGroup.fs`, `DoseRule.fs`, `FilePath.fs`, `GenPresProduct.fs`, `Json.fs`,
  `Substance.fs`, `GStand.fs`, `Utils.fs`, `NKF/Utils.fs`) are not dependency-rule violations 
  adapters may do IO but leaving them on raw `Console` writes means G-Standaard loading never
  reaches the Serilog sink and can't be level-filtered by `GENPRES_LOG` like everything else.
  Thread a `Logger` into these adapters' entry points once Phase 1 proves the pattern on the
  Core-ring libraries.
- Global mutable state. Two instances are already closed out by Phase 1, not left for this
  phase to find: `AgentLogging.errorHandler: (LoggingError -> unit) option` disappears when
  `AgentLogging` is deleted (step 2), and so does `Server/Logging.fs`'s own
  `mutable loggers: Map<LoggerType * Level, AgentLogging.AgentLogger>` plus its `loggerLock`
  (lines 84-101 today) Serilog's per-sink level filtering replaces the need to cache one
  `AgentLogger` per `(LoggerType, Level)` pair. What's left for this phase is confirming neither
  pattern was reintroduced in the Serilog wiring itself (e.g. a similar cache built around
  `ILogger` instances).
- PII/audit-scope review. `7sharp9` flagged log data exposure as a risk alongside the auditing
  benefit. `ServerLogging.Message.Request` already logs `clientIP` on every request; decide and
  document a redaction policy (hash, truncate, or accept this is a clinical system behind a
  hospital EHR launch, not a public API, but `DEVELOPMENT.md`'s existing PII guidance in
  `fsharp-coding.instructions.md`'s Logging and Observability section should be made concrete
  here) before the Serilog file sink starts persisting request logs to disk.
- Structured audit trail for the medico-legal use case. Once Request/Error events are Serilog
  structured events rather than ad hoc console lines, the "show all the calculation steps" record
  `halcwb` described in the issue becomes queryable (filter by patient/order/time range in a log
  viewer or `jq` over the JSON sink) instead of grep-through-a-flat-file. Add a short follow-up
  note in `DEVELOPMENT.md` once it exists, since it changes how maintainers investigate a reported dosing discrepancy.

## Process note

`Server/Logging.fs`, `ServerApi.CompositionRoot.fs`, `ServerApi.Compute.fs`, `Server.fs`, and every
`ConsoleWriter` call site are non-UI `.fs` source files, so the script-only policy in `AGENTS.md`
applies: this plan is prototyped in `.fsx` scripts under each project's `Scripts/` directory (the
Serilog bridge is a good candidate to prototype end-to-end in FSI before touching
`Server/Logging.fs`, since it can be exercised against a real `ILogger` and sample `IMessage`
values without starting the server) and migrated to source by the maintainer, the same way #419's
plan flagged the exception it needed. 