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
  hard to search, without touching the solver's signatures. `Serilog.Sinks.File`'s default output
  is rendered text, not JSON a formatter has to be configured explicitly. Use
  `Serilog.Formatting.Compact.CompactJsonFormatter` (from the `Serilog.Formatting.Compact`
  package, added to `paket.dependencies`' `Main` group alongside the sinks listed below and
  referenced only where the file sink is built) passed as the `formatter` argument to
  `WriteTo.File(...)` for this sink, so it emits one compact JSON object per line rather than
  Serilog's default plain-text layout.

### Package and dependency-rule placement

Add to `paket.dependencies`' `Main` group (Serilog's own core has no transitive surface beyond
itself and the two sinks):

```text
nuget Serilog
nuget Serilog.Sinks.Console
nuget Serilog.Sinks.File
nuget Serilog.Sinks.Async
nuget Serilog.Formatting.Compact
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

Each commit should try to stay under ~200 changed lines per `CONTRIBUTING.md`; phases with large work should
be split further where it makes sense to compartmentalise, but should not be split for the sake of it.
Numbering continues from #378's phase numbers. Step 1 below folds #378 Phase 1 steps 1 and 2
together (splitting `Logging.fs` and relocating the `AgentLogging`-calling factory functions must
land in the same commit see step 1's note on why); step 2 is new content this plan adds on top of
#378's Phase 1 step 2, wiring Serilog as the chosen sink technology.

1. (#378 Phase 1 steps 1+2 combined into one commit; step 2 cannot land later than step 1 without
   an intermediate broken build see below) Split `Logging.fs`: `Logger`, `Event`, `Level`,
   `IMessage`, `noOp`, `create`, `combine`, `logLazy` and friends stay in
   `Informedica.Logging.Lib`; `createConsole`, `createFile` and the whole `AgentLogging` module
   move to `Informedica.Agents.Lib` (flips the reference so `Agents.Lib → Logging.Lib`, matching
   the target ring map).

   Four call sites reference `AgentLogging.*` directly today and are not just callers of a port
   they physically live inside Core-ring files, so moving `AgentLogging` out from under them
   without also touching them either fails to compile (no `Agents.Lib` reference) or, if a
   reference were added instead, reintroduces the exact Core → Infrastructure violation this
   split exists to remove (`Informedica.GenSOLVER.Lib`, `Informedica.GenORDER.Lib` and
   `Informedica.GenFORM.Lib` are all Ring.Core in `scripts/DependencyRule.fsx`;
   `Informedica.Agents.Lib` is Ring.Infrastructure). #378 Phase 1 step 2 already gave the correct
   answer for this it was dropped when this plan's step 1/2 renumbering replaced it with the
   Serilog-specific work below, and needs to be carried forward into this same commit, not
   skipped:
   - `SolverLogging.fs` (`GenSOLVER.Lib`): delete `createAgentLogger`, move its logic (formatter
     stays; the `AgentLogging.createWithFormatter` call moves) into `Server/Logging.fs`.
   - `OrderLogging.fs` (`GenORDER.Lib`): delete `createAgentLogger`, same treatment the caller at
     `Server/Logging.fs:98` (`OrderLogging.createAgentLogger`) already lives in the composition
     root, so this is inlining, not new design.
   - `FormLogging.fs` (`GenFORM.Lib`): delete the top-level `agentLogger` value it is unreferenced
     dead code today (also flagged, separately, in #378 Phase 0 step 1 as an instance of the
     #523 top-level-`MailboxProcessor` pattern), so no replacement is needed anywhere.
   - `tests/Informedica.Logging.Tests/Tests.fs`: add a `ProjectReference` to
     `Informedica.Agents.Lib` in the same commit it exercises `AgentLogging` directly and is not
     part of the production ring graph, so this is not a dependency-rule concern, only a missing
     reference.

   No behaviour change; verified by `Logging.Tests`.
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
   the 21 `viaLogger` sites. It is *not* dead code once the 21 land: 9 adapter-ring files
   (`ZIndex.Lib`, `ZForm.Lib`, `NKF.Lib` see Phase 2 below) are still the sole remaining callers
   of `ConsoleWriter`, so deleting `Console.fs` here would break their build. Leave `Console.fs`
   and its `allowFile` entry in place until the Phase 2 adapter migration below also lands; delete
   it then, in that step, not pulled forward into this one.

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
   lives in `Informedica.Logging.Lib` and has zero production callers outside its new home in
   `Informedica.Agents.Lib` (see the Decision note under "Step 2 migration package" — moved, not
   deleted), `scripts/CheckDependencyRule.fsx`'s `viaLogger`-labelled allowances are empty, and
   `Serilog` appears in exactly three `paket.references` files (`Informedica.GenPRES.Server`,
   `Informedica.MCP.Server`, `Informedica.MCP.Lib` — the MCP host's own Serilog wiring lives in
   `Informedica.MCP.Lib/Logging.fs`, not in the host project itself, per part F below). The
   `Console.fs` `allowFile` entry stays until the Phase 2 adapter migration below deletes it (see
   step 4's note); it is not part of this phase's completion criteria.

## Step 2 migration package (prepared 2026-09-15, ready to apply)

Both prototype scripts (`src/Informedica.GenPRES.Server/Scripts/416-serilog-bridge.fsx`,
`src/Informedica.MCP.Server/Scripts/416-mcp-serilog.fsx`) are done and their smoke checks pass.
This section is the reviewable source diff derived from them, prepared under the script-only
policy (`AGENTS.md`) rather than applied directly — the maintainer applies it.

**Scope note**: this package is bigger than "Step 2, one file." Step 1 (moving `AgentLogging`
into `Agents.Lib`, deleting the three Core-ring `createAgentLogger`/`agentLogger` definitions)
has *not* landed in source — `AgentLogging` still lives inside `Informedica.Logging.Lib/Logging.fs`
today. Tracing `Server/Logging.fs`'s actual callers (not just its own body) surfaced three more
files the plan's Step 2 item list doesn't name, because the old `AgentLogger`'s per-request
`setComponentName`/`agent`-start plumbing is threaded through them:

- `Server.fs` — `logClientIP`, `safeWebApi`, `Host.resourceProvider`, `Http.LoggerShutdown`
  all call `Logging.getLogger`/`setComponentName`/`loggerLock`/`loggers` directly.
- `ServerApi.Adapters.fs` — `resolveLogger`, a private `setComponentName` wrapper, and four
  `do! setComponentName "OrderPlan" agent` call sites inside `makeOrderPlanPort`, plus
  `makeOrderContextPort`'s one.
- `ServerApi.Ports.fs` — `AppEnv` gains a `logger` field (new; needed by Step 3 below, not by
  Step 2 itself, but the two land together more cleanly than sequenced).

Since `AgentLogging` becomes fully unreferenced by production code the moment `Server/Logging.fs`
stops calling `OrderLogging.createAgentLogger` (its only production caller), this package folds
the mechanical part of Step 1 in rather than leaving a half-migrated `AgentLogging` module in
place for a separate commit. **Decided 2026-09-15**: `AgentLogging` moves into
`Informedica.Agents.Lib` rather than being deleted (option 3 at the end of this section) — the
concrete diff is already prototyped and verified in
`src/Informedica.Agents.Lib/Scripts/416-logging-split.fsx`, whose header comment (lines 21-69) is
the migration checklist: `Informedica.Logging.Lib/Logging.fs` keeps only `IMessage`/`TimeStamp`/
`Level`/`Event`/`Logger`/`Logging` (the `TargetLoggingLib` section of the script); two new files,
`Informedica.Agents.Lib/ConsoleFileLogger.fs` (`createConsole`/`createFile`) and
`Informedica.Agents.Lib/AgentLogging.fs` (byte-for-byte today's `AgentLogging` module — its only
cross-module dependency, `Logging.levelValue`, stays in the trimmed port), take the rest; the
`.fsproj` reference flips to `Agents.Lib → Logging.Lib`; the `scripts/DependencyRule.fsx`
allow-list entry for the old direction and the `scripts/CheckDependencyRule.fsx`
`"src/Informedica.Logging.Lib/Logging.fs"` `loggingSplit` allowance are both deleted, since the
trimmed file has no banned tokens left. That checklist also already covers the three Core-ring
call sites (`SolverLogging.fs`, `OrderLogging.fs`, `FormLogging.fs`, lines 208-219 above) and the
`tests/Informedica.Logging.Tests/Tests.fs` reference/call-site updates — nothing further to add
here. This relocation is independent of the Serilog work in parts B-G below (Server/Logging.fs's
`SerilogLogging` never calls `AgentLogging`) and should land as its own commit, ahead of or
alongside this package but not merged into it.

### A. Packages — item 1

`paket.dependencies`, `Main` group, after `nuget Microsoft.Extensions.Hosting`:

```text
nuget Serilog 4.4.0
nuget Serilog.Sinks.Console 6.1.1
nuget Serilog.Sinks.File 7.0.0
nuget Serilog.Sinks.Async 2.1.0
```

(`Serilog.Formatting.Compact` is not added here — it belongs to Step 5's GenSOLVER-trace sink.)

`src/Informedica.GenPRES.Server/paket.references` (currently `FSharp.Core` / `Saturn` /
`Fable.Remoting.Giraffe` / `IcedTasks`) — append:

```text
Serilog
Serilog.Sinks.Console
Serilog.Sinks.File
Serilog.Sinks.Async
```

`src/Informedica.MCP.Server/paket.references` — currently empty (the SDK types
`ModelContextProtocol`/`Microsoft.Extensions.Hosting` are referenced from
`Informedica.MCP.Lib/paket.references`, not here). `Program.fs` builds its own
`LoggerConfiguration` directly (see part D), so add the same four lines to this file, which today
has none.

Then `dotnet paket install` and `dotnet run Build`.

### B. `Server/Logging.fs` — item 2

Full replacement. Drops the `AgentLogging`/`loggerLock`/`loggers` triple, the `getConfig`/
`getDirAgent`/`MAX_LOG_FILES` pruning wiring, and `setComponentName`; adds `SerilogBridge` and
`SerilogLogging` (copied from `416-serilog-bridge.fsx` almost verbatim — the one addition beyond
the prototype is `serilogLoggers`/`getLogger`/`disposeAll` below, which give the composition root
a single built-once map instead of calling `SerilogLogging.buildAll` at every call site):

```fsharp
module Logging

open System
open System.IO

open Informedica.Utils.Lib
open Informedica.Logging.Lib

open Serilog
open Serilog.Events


/// Server-specific logging message types and helpers
module ServerLogging =

    module Logging = Informedica.Logging.Lib.Logging

    /// Messages used by the Server that can be logged
    type Message =
        | Request of method_: string * path: string * clientIP: string
        | Info of string
        | Warning of string
        | Error of string

        interface IMessage

    /// Log a request line as Informative
    let logRequest (logger: Logger) (method_: string) (path: string) (clientIP: string) =
        Request(method_, path, clientIP) |> Logging.logInfo logger


let getRecommendedLogPath (componentName: string option) =
    let logDir = AppPath.logsDir ()
    Directory.CreateDirectory(logDir) |> ignore

    let componentName = componentName |> Option.defaultValue "general"
    let timestamp = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss")
    let shortGuid = Guid.NewGuid().ToString("N").Substring(0, 4)
    let fileName = $"genpres_{componentName}_{timestamp}_{shortGuid}.log"

    Path.Combine(logDir, fileName)


type LoggerType =
    | RequestLogger
    | OrderLogger
    | ResourcesLogger
    | FormularyLogger
    | OrderPlanLogger
    | ParenteraliaLogger


let allLoggerTypes =
    [ RequestLogger; OrderLogger; ResourcesLogger; FormularyLogger; OrderPlanLogger; ParenteraliaLogger ]


let loggerTypeName =
    function
    | RequestLogger -> "request"
    | OrderLogger -> "order"
    | ResourcesLogger -> "resources"
    | FormularyLogger -> "formulary"
    | OrderPlanLogger -> "orderplan"
    | ParenteraliaLogger -> "parenteralia"


let loggingEnabled =
    Env.getItem "GENPRES_LOG"
    |> Option.map (fun s -> s |> String.trim |> String.isNullOrWhiteSpace |> not)
    |> Option.defaultValue false


let loggingLevel =
    Env.getItem "GENPRES_LOG"
    |> Option.bind (fun (s: string) ->
        match s.Trim().ToLowerInvariant() with
        | "d" -> Level.Debug |> Some
        | "i" -> Level.Informative |> Some
        | "w" -> Level.Warning |> Some
        | "e" -> Level.Error |> Some
        | _ -> None
    )


/// Turns a Serilog ILogger into the domain-facing Logger record. Every IMessage case becomes a
/// Serilog structured property for free - no change to any IMessage type.
module SerilogBridge =

    let toSerilogLevel =
        function
        | Level.Debug -> LogEventLevel.Debug
        | Level.Informative -> LogEventLevel.Information
        | Level.Warning -> LogEventLevel.Warning
        | Level.Error -> LogEventLevel.Error

    let toLogger (serilogLogger: Serilog.ILogger) : Logger =
        {
            Log =
                fun ev ->
                    serilogLogger.Write(
                        ev.Level |> toSerilogLevel,
                        "{EventType} {@Event}",
                        ev.Message.GetType().Name,
                        ev.Message
                    )
            Enabled = fun level -> serilogLogger.IsEnabled(level |> toSerilogLevel)
        }


/// One Serilog sink per LoggerType, built lazily so a LoggerType that never logs never creates
/// a sink or a file. Both Console and File sinks are always active - Console for the terminal a
/// developer is watching, File (wrapped in Async) for the persistent record.
module SerilogLogging =

    let buildLogger (level: Level) (loggerType: LoggerType) : Serilog.Core.Logger * string =
        let minLevel = level |> SerilogBridge.toSerilogLevel
        let path = loggerType |> loggerTypeName |> Some |> getRecommendedLogPath

        let logger =
            LoggerConfiguration()
                .MinimumLevel.Is(minLevel)
                .WriteTo.Console()
                .WriteTo.Async(fun a -> a.File(path) |> ignore)
                .CreateLogger()

        logger, path

    let buildAll (level: Level) : Map<LoggerType, Lazy<Serilog.Core.Logger * string>> =
        allLoggerTypes
        |> List.map (fun lt -> lt, lazy (buildLogger level lt))
        |> Map.ofList

    let getLogger (loggers: Map<LoggerType, Lazy<Serilog.Core.Logger * string>>) (loggerType: LoggerType) : Logger =
        loggers[loggerType].Value |> fst :> Serilog.ILogger |> SerilogBridge.toLogger

    /// Disposes only the loggers actually forced into existence.
    let dispose (loggers: Map<LoggerType, Lazy<Serilog.Core.Logger * string>>) =
        for KeyValue(_, lazyLogger) in loggers do
            if lazyLogger.IsValueCreated then
                (lazyLogger.Value |> fst :> IDisposable).Dispose()


/// Built once, at the fixed startup level; `None` when GENPRES_LOG is unset. Every call site
/// reads through this, never through `SerilogLogging.buildAll` directly, so there is exactly
/// one map - and one set of files - per process.
let serilogLoggers: Map<LoggerType, Lazy<Serilog.Core.Logger * string>> option =
    loggingLevel |> Option.map SerilogLogging.buildAll


/// `Logging.noOp` when GENPRES_LOG is unset; otherwise the Serilog-backed Logger for that type.
/// Replaces `getLogger level loggerType` - level is no longer a parameter, since
/// `serilogLoggers` is already built from the fixed startup level.
let getLogger (loggerType: LoggerType) : Logger =
    match serilogLoggers with
    | None -> Informedica.Logging.Lib.Logging.noOp
    | Some loggers -> loggers |> SerilogLogging.getLogger <| loggerType


/// Disposes every logger this process forced into existence. Call from LoggerShutdown.
let disposeAll () =
    serilogLoggers |> Option.iter SerilogLogging.dispose
```

**Gap versus the prototype, flagged rather than resolved**: today's `setComponentName` prunes old
log files (`FileDirectoryAgent.pruneAsync`, capped at `MAX_LOG_FILES = 10_000` per directory)
before every `logger.Start`. Neither prototype script reproduces this — `buildLogger` above
creates a file sink with no pruning step at all, which is a real behavior change (unbounded log
file accumulation under `data/logs`) that neither script's smoke checks caught, since they clean
up their own files. The plan's own header note ("pruning ... still runs the same way, just no
longer needs to know about a logger's lifecycle") assumed this would carry over but nothing
proves it does. Recommend calling the existing `FileDirectoryAgent.pruneAsync` synchronously
once per `buildLogger` call (i.e., once per `LoggerType` actually used per process, not once per
request as today) before `CreateLogger()`; this needs its own smoke check before landing, since
neither prototype exercises it.

### C. `Server.fs`, `ServerApi.Adapters.fs` — companion ripple (not in the original item list)

The old `AgentLogger` was retargeted per request (`setComponentName`); Serilog's file path is
fixed at sink construction, so every `setComponentName` call site disappears, and `resolveLogger`
collapses from `(AgentLogger option * Logger)` to plain `Logger`.

`ServerApi.Adapters.fs`:

- Delete the `resolveLogger`/`setComponentName` pair (lines 35-48 today) entirely.
- `makeFormularyPort`: add a `logger` parameter, thread it to `ParenteraliaService.get` (see D
  below):

  ```fsharp
  let private makeFormularyPort logger (provider: Resources.IResourceProvider) : FormularyPort =
      {
          getFormulary = fun form -> async { return form |> FormularyService.get provider }

          getParenteralia =
              fun par ->
                  async { return par |> ParenteraliaService.get logger provider |> Result.mapError Array.singleton }
      }
  ```

- `makeOrderContextPort`: drop the `agent` parameter and its `do! setComponentName "OrderContext" agent`
  line; keep `logger`.
- `makeOrderPlanPort`: drop the `agent` parameter and its four `do! setComponentName "OrderPlan" agent`
  lines (in `recalculate`, `navigate`, `addOrderContext`, `openWith`) — nothing else in those
  bodies changes.
- `makeAppEnvWith`: replace `let agent, logger = resolveLogger ()` with:

  ```fsharp
  let requestLogger = Logging.getLogger Logging.RequestLogger
  let orderLogger = Logging.getLogger Logging.OrderLogger
  let parenteraliaLogger = Logging.getLogger Logging.ParenteraliaLogger
  let orderCtxPort = makeOrderContextPort orderLogger provider
  ```

  and wire `formulary = makeFormularyPort parenteraliaLogger provider`,
  `orderPlan = makeOrderPlanPort provider orderCtxPort` (no `agent`),
  `Informedica.GenForm.Lib.Api.reloadCache orderLogger provider` (unchanged reference, same
  variable renamed from `logger`), and the new `AppEnv.logger = requestLogger` field (Step 3,
  part E). This is also the first production use of the `FormularyLogger`/`OrderPlanLogger`/
  `ParenteraliaLogger` cases, which exist today but nothing constructs them.

`Server.fs`:

- `logClientIP` (`:526-545`): drop the `setComponentName` line; `Logging.getLogger level Logging.RequestLogger`
  becomes `Logging.getLogger Logging.RequestLogger` (no `level` argument — `match Logging.loggingLevel with None -> next ctx | Some _ -> ...`
  still gates whether the handler runs at all, so the `Option` match stays, only the inner
  `getLogger` call sheds its `level` argument); `Logging.ServerLogging.logRequest logger method path clientIP`
  is unchanged (the function's `logger` parameter is now `Logger` directly instead of
  `AgentLogger`, matching part B).
- `safeWebApi` (`:554-589`): same `getLogger` simplification; drop `setComponentName`;
  `Informedica.Logging.Lib.Logging.logError logger.Logger` becomes `Informedica.Logging.Lib.Logging.logError logger`
  (no more `.Logger` field to project through).
- `Host.resourceProvider` (`:625-638`): collapses to
  `let logger = Logging.getLogger Logging.ResourcesLogger in urlId |> Informedica.GenForm.Lib.Api.getCachedProviderWithDataUrlId logger`
  — the `Option.map`/`setComponentName`/`Async.RunSynchronously`/`Option.defaultValue Informedica.GenOrder.Lib.Logging.noOp`
  dance disappears because `Logging.getLogger` already returns `Logging.noOp` when
  `GENPRES_LOG` is unset (see part B).
- `Http.LoggerShutdown` (`:592-616`): the `IHostedService.StopAsync` body (iterating
  `Logging.loggers` under `Logging.loggerLock`, calling `logger.StopAsync()` per entry) becomes
  `member _.StopAsync _ = Logging.disposeAll (); Task.CompletedTask` — Serilog's
  `Core.Logger.Dispose()` flushes synchronously (including the wrapped `Async` sink), so no
  `Async.Parallel`/`Async.StartAsTask` dance is needed either.

### D. `ServerApi.Services.fs` — Step 3, the one signature change

`ParenteraliaService.get` (`:238` today) has no `logger` in scope; per the plan's own confidence
note, this is the one Step 3 site that's a signature change, not a one-line swap:

```fsharp
let get logger provider (par: Parenteralia) : Result<Parenteralia, string> =
    Logging.ServerLogging.Info $"getting parenteralia for {par.Generic}"
    |> Informedica.Logging.Lib.Logging.logInfo logger
    ...
```

(rest of the function body unchanged; caller update is part C's `makeFormularyPort`.)

The `:452` site (inside `OrderContextService.evaluate`) already has `logger: Logger` in scope
(used two lines above at `GenOrderContext.evaluate logger provider`) — genuinely a one-line swap:

```fsharp
with e ->
    Logging.ServerLogging.Error $"errored:\n{e}"
    |> Informedica.Logging.Lib.Logging.logError logger
    Error [| e.Message |]
```

### E. `ServerApi.Ports.fs`, `ServerApi.Compute.fs`, `ServerApi.CompositionRoot.fs` — Step 3 remainder

`AppEnv` (`ServerApi.Ports.fs:220`) gains one field:

```fsharp
type AppEnv =
    {
        formulary: FormularyPort
        orderContext: OrderContextPort
        orderPlan: OrderPlanPort
        interaction: InteractionPort
        admin: AdminPort
        requireLoaded: unit -> string[] option
        session: SessionPort
        logger: Informedica.Logging.Lib.Logger
    }
```

`ServerApi.Compute.fs`: drop `open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime`; both
`bound` and `logged` read `env.logger` — `logged` needs an `env` parameter it does not take
today, which is a signature change at all four `Compute.logged` call sites in
`ServerApi.CompositionRoot.fs` (`processLaunch`/`processSession`/`processSigning`/`processAdmin`,
all of which already receive `env` as their first `Compute.logged`/handler argument today, so the
call sites gain one argument, not a new plumbing path):

```fsharp
let bound
    (env: AppEnv)
    (cookie: SessionCookie)
    (name: 'cmd -> string)
    (gate: 'cmd -> Gate)
    (handler: 'cmd -> Async<Result<'resp, string[]>>)
    (request: Request<'cmd>)
    : Async<Result<Reply<'resp>, string[]>>
    =
    let cmd = request.Command

    async {
        try
            Logging.ServerLogging.Info $"Processing command: {name cmd}"
            |> Informedica.Logging.Lib.Logging.logInfo env.logger

            let! notice =
                match cookie.read () with
                | None -> async { return None }
                | Some id -> env.session.seen id request.Opened

            let! result =
                match gate cmd with
                | Gate.Open -> handler cmd
                | Gate.RequiresLoaded ->
                    match env.requireLoaded () with
                    | Some msgs -> async { return Error msgs }
                    | None -> handler cmd

            let told =
                match notice with
                | Some(RecordNotice.NewerVersion _) -> ", the record moved on"
                | Some(RecordNotice.Ended _) -> ", the Session ended"
                | None -> ""

            Logging.ServerLogging.Info $"Finished processing command: {name cmd}{told}"
            |> Informedica.Logging.Lib.Logging.logInfo env.logger

            return
                result
                |> Result.map (fun response -> { Response = response; Notice = notice })
        with ex ->
            Logging.ServerLogging.Error $"Error processing command: {name cmd}\n{ex}"
            |> Informedica.Logging.Lib.Logging.logError env.logger

            return Error [| ex.Message |]
    }


let logged (env: AppEnv) (what: string) (name: 'cmd -> string) (run: 'cmd -> Async<'resp>) (cmd: 'cmd) =
    async {
        Logging.ServerLogging.Info $"Processing {what}: {name cmd}"
        |> Informedica.Logging.Lib.Logging.logInfo env.logger

        let! response = run cmd

        Logging.ServerLogging.Info $"Finished processing {what}: {name cmd}"
        |> Informedica.Logging.Lib.Logging.logInfo env.logger

        return response
    }
```

`ServerApi.CompositionRoot.fs`: drop `open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime`;
the four `Compute.logged "..." ...` calls each gain `env` as the first argument
(`Compute.logged env "launch" LaunchCommand.toString ...`, etc.); `getSettings`'s body becomes:

```fsharp
getSettings =
    fun () ->
        async {
            Logging.ServerLogging.Info "Processing settings"
            |> Informedica.Logging.Lib.Logging.logInfo env.logger

            return settings
        }
```

### F. `Informedica.MCP.Lib/McpServer.fs`, `Informedica.MCP.Server/Program.fs` — item 3

`McpServer.createHostBuilder` takes a `Logger` and wires the tool-call filter (from
`416-mcp-serilog.fsx`'s `callToolLoggingFilter`/`logToolCall`, copied as-is into
`Informedica.MCP.Lib/McpServer.fs` — `logToolCall` is SDK-free and belongs next to the filter
that wraps it, not in the host's `Program.fs`):

```fsharp
let createHostBuilder (logger: Informedica.Logging.Lib.Logger) =
    let builder = Host.CreateApplicationBuilder()

    builder.Services
        .AddMcpServer(fun options ->
            options.ServerInfo <-
                ModelContextProtocol.Protocol.Implementation(Name = "GenPRES MCP Server", Version = "1.0.0")
        )
        .WithStdioServerTransport()
        .WithRequestFilters(fun rf -> rf.AddCallToolFilter(callToolLoggingFilter logger) |> ignore)
        .WithTools<GenFormMcpTools>()
        .WithTools<GenOrderMcpTools>()
    |> ignore

    builder


let run (logger: Informedica.Logging.Lib.Logger) (provider: IResourceProvider) =
    initProvider provider

    eprintfn "[MCP] Starting GenPRES MCP server (stdio transport)..."
    eprintfn "[MCP] Working directory: %s" Environment.CurrentDirectory

    let builder = createHostBuilder logger
    let app = builder.Build()
    app.RunAsync() |> Async.AwaitTask |> Async.RunSynchronously
```

`logToolCall`/`callToolLoggingFilter` land in the same file, copied from the script essentially
unchanged (only the `McpMessage` DU and `SerilogBridge`/`getRecommendedLogPath` references need
to resolve against the real `Informedica.GenPRES.Server` project instead of the `#load`-based
aliasing the script used — see the note below on where `McpMessage`/the Serilog build fns live,
since `Informedica.MCP.Lib`/`Informedica.MCP.Server` must not reference
`Informedica.GenPRES.Server`, only the reverse never holds today and shouldn't start).

**Placement question the prototype ducked by `#load`ing across projects**: `SerilogBridge` (part
B) lives in `Informedica.GenPRES.Server`, which `Informedica.MCP.Lib`/`Informedica.MCP.Server`
must not reference (they are siblings, not client/server). `McpSerilogLogging.buildLogger` (the
stderr-routed, single-logger variant — see FINDING 1 in the prototype) and the `McpMessage` DU
therefore need their own home. Recommend a new `Logging.fs` in `Informedica.MCP.Lib` (parallel to
`Informedica.GenPRES.Server/Logging.fs`, not sharing code with it — the two hosts have
irreducibly different sink shapes: six `LoggerType`s with dual Console+File sinks for the web
server versus one logger with stderr-only Console + File for the stdio host) containing
`McpMessage`, `McpLogging.parseLevel`/`buildLogger`/`getLogger` (from `McpSerilogLogging` in the
prototype) and its own copy of `SerilogBridge.toSerilogLevel`/`toLogger` (11 lines, not worth a
shared project for). `Informedica.MCP.Lib/paket.references` needs the same four Serilog lines as
part A.

`Informedica.MCP.Server/Program.fs`:

```fsharp
open System

open Informedica.Utils.Lib
open Informedica.GenForm.Lib

open Informedica.MCP.Lib


[<EntryPoint>]
let main _ =
    Env.loadDotEnv () |> ignore
    Environment.SetEnvironmentVariable("GENPRES_PROD", "1")
    Environment.SetEnvironmentVariable("GENPRES_DEBUG", "0")

    Environment.CurrentDirectory <- AppPath.rootPath ()

    let dataUrlId =
        match Environment.GetEnvironmentVariable "GENPRES_URL_ID" with
        | null
        | "" -> invalidOp "GENPRES_URL_ID environment variable must be set before starting the MCP server."
        | value -> value

    let logger, disposeLogger =
        McpLogging.getLogger (fun name -> Environment.GetEnvironmentVariable name |> Option.ofObj)

    let provider = Api.getCachedProviderWithDataUrlId Informedica.GenOrder.Lib.OrderLogging.noOp dataUrlId

    try
        McpServer.run logger provider
        0
    finally
        disposeLogger ()
```

(`FormLogging.noOp` in the original `getCachedProviderWithDataUrlId` call becomes
`Informedica.GenOrder.Lib.OrderLogging.noOp`, matching part C's finding that both re-export the
same `Informedica.Logging.Lib.Logging.noOp` — this is an unrelated pre-existing naming
inconsistency in the current source, not something this migration needs to touch; left as-is
above, flagged only so a reviewer doesn't read it as a typo.)

### G. Verification

Same as the plan's existing step 6: `dotnet run Format`, `dotnet run servertests`,
`dotnet fsi scripts/CheckDependencyRule.fsx`. Additionally, since this touches request-path and
MCP host code with no unit test today exercising the Serilog wiring itself: start the server with
`GENPRES_LOG=d dotnet run` and confirm `data/logs/genpres_request_*.log` is created and grows on
a request; start the MCP host manually and confirm no bytes reach stdout before the first
JSON-RPC response (FINDING 1's correctness requirement, not just a nice-to-have).

### Decision: what happens to `AgentLogging` (resolved 2026-09-15)

After this package lands, `AgentLogging` (still inside `Informedica.Logging.Lib/Logging.fs`,
`:217-729` today) has zero production callers — the last one, `OrderLogging.createAgentLogger`,
is deleted as part of this package (nothing else calls it; `SolverLogging.createAgentLogger` and
`FormLogging`'s top-level `agentLogger` value already have zero production callers today, per a
grep run this session). Only `tests/Informedica.Logging.Tests/Tests.fs` (`:657-1043`) still
exercises it directly. Three options were weighed:

1. Leave `AgentLogging` in place, dead in production, tested in isolation. Cheapest, but leaves
   the `Core → Infrastructure` dependency-rule violation it's already in ([ADR-0001](../adr/0001-system-architecture.md))
   unfixed and doesn't meet step 7's criterion.
2. Delete `AgentLogging` and its ~400-line test suite outright. Matches a literal "no longer
   exists" reading of step 7, but throws away a working, tested ring-buffer/flush-timer
   implementation and its regression net in the same commit that introduces its untested Serilog
   replacement — no rollback path if Serilog turns out to have a gap `AgentLogging` didn't.
3. **Chosen.** Physically move `AgentLogging` into `Informedica.Agents.Lib` (fixing the
   dependency-rule direction) and keep its test suite pointed at the new location, without
   deleting it. Keeps the working implementation available — e.g. as a fallback sink, or for a
   future non-Serilog use — and is the option [ADR-0001](../adr/0001-system-architecture.md)'s
   ring map and Step 1 as originally scoped both already pointed at; option 2's "throw away a
   tested subsystem in the same commit as its untested replacement" was the deciding factor
   against it. Step 7's completion wording above is updated accordingly ("no longer exists" →
   "no longer lives in `Logging.Lib`").

The concrete migration is already prototyped and verified — see the Decision note earlier in this
section for the file-by-file checklist (`416-logging-split.fsx`).

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