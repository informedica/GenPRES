# Implementation plan for issue #416

## Problem description

[#416](https://github.com/informedica/GenPRES/issues/416): the CompositionRoot hard-codes a
console write for every request (`ServerApi.CompositionRoot.fs`, `ServerApi.Compute.fs`), flagged
as unconditional stdout traffic with no clear consumer once the app is containerized and scaled
beyond one instance. Discussion converged on three separable concerns:

1. Production/audit logging request tracing, errors, admin actions. Needs to be structured,
   level-filtered, and off by default rather than littering stdout.
2. GenSOLVER research trace logging a 26,555-line calculation trace used for debugging dose
   scenarios and for a university math-department collaboration studying solver behaviour. Return
   trace data from core functions rather than performing IO inside them, which the domain already
   does in part (`GenSOLVER.Types.Events.Event`, `GenORDER`'s `OrderLogging`, `GenFORM`'s
   `FormLogging`).
3. Technology choice: structured events map directly onto the existing `IMessage` discriminated
   unions instead of string parsing, and is async-safe at the scale needed here.

This also carries out [#378](https://github.com/informedica/GenPRES/issues/378) Phase 1
("logging inversion", see
[`docs/implementation-plans/378-dependency-rule.md`](378-dependency-rule.md#phase-1--logging-inversion-a)):
`Informedica.Logging.Lib` keeps only the `Logger`/`Event`/`Level`/`IMessage` port; the console/file
sink constructors and the `AgentLogging` agent move to `Informedica.Agents.Lib`; every
`ConsoleWriter` call below an injected `Logger` is replaced by a `Logger` call.

## Approaches considered

1. Keep the bespoke `AgentLogging` agent, only silence/redirect the CompositionRoot write. Fixes
   the literal report but leaves the `ConsoleWriter` path untouched, the ring-buffer/flush-timer
   machinery unproven under concurrent load, and the dependency-rule violation in place.
2. An `IMessage` discriminated union can use Serilog's structured-event destructuring (`{@Event}`).
3. **Chosen.** Serilog as the sink technology, `Logger`/`Event` kept as the domain-facing port.
   Matches #378 Phase 1's shape: core libraries keep seeing only the small record-of-functions
   port, and only the DMZ composition roots (`Informedica.GenPRES.Server`, `Informedica.MCP.Server`,
   `Informedica.MCP.Lib`) reference Serilog.

## Chosen approach

- A Serilog `ILogger` is built once per composition root from `GENPRES_LOG` (`d`/`i`/`w`/`e`;
  unset means logging is off). For the server, one logger per `LoggerType`
  (`RequestLogger`/`OrderLogger`/`ResourcesLogger`/`FormularyLogger`/`OrderPlanLogger`/
  `ParenteraliaLogger`), each with a `Serilog.Sinks.Console` and an async `Serilog.Sinks.File`
  sink, built lazily so a `LoggerType` that never logs never creates a sink or a file. For the MCP
  stdio host, one logger, Console pinned to stderr (stdout is the JSON-RPC transport) plus a file
  sink.
- `Server/Logging.fs`'s `SerilogBridge.toLogger` turns a Serilog `ILogger` into the domain
  `Logger` record: `Log = fun ev -> serilogLogger.Write(toSerilogLevel ev.Level,
  "{GenPresEvent}", ev)`, `Enabled = fun level -> serilogLogger.IsEnabled(toSerilogLevel level)`.
  The whole `Event` crosses as one scalar property (`Destructure.AsScalar<Event>()`), so the
  thread that logs stores a reference and enqueues; no `IMessage` type changes. Each sink sits
  behind its own `Async` wrapper and renders on its own thread through an `ITextFormatter`: the
  file as one compact JSON object per line (`@t`, `@l`, `EventType`, `Text`, where `Text` is the
  rendering of the existing `formatOrderMessage` / `formatSolverMessage` / `FormLogging`
  formatters plus one for the server's own messages), the console as readable text. One
  `Renderer` per logger renders an event once for both sinks. The file queue holds 100 000 events
  and then blocks; the console queue may drop. `SerilogLogging.buildLoggerAt path level` is the
  sink configuration, used by `buildLogger`, the tests and the measurement script.
  `{@Event}` was tried first and dropped: Serilog captured the F# union by reflection on the
  thread that logs, 0.2 ms per event and 439 MB per Debug pass of three scenarios.
  `Informedica.MCP.Lib/Logging.fs` carries its own copy of the same bridge, with `McpMessage` as
  its fourth formatter entry, rather than sharing a project with the server.
- `LogAnalyzer.Parse.textLines` unfolds the `Text` of a JSON line for the admin log analysis and
  passes a flat line through, so files written before the JSON format keep reading as they did.
- `Informedica.Logging.Lib.AgentLogging` (the `MailboxProcessor`, ring buffer, flush timer, file
  writer agent) is relocated to `Informedica.Agents.Lib`, not deleted: it fixes the
  Core → Infrastructure dependency-rule direction it was in, and keeps a working, tested subsystem
  available instead of removing it in the same commit as its untested replacement.
- Packages (`paket.dependencies`, `Main` group): `Serilog`, `Serilog.Sinks.Console`,
  `Serilog.Sinks.File`, `Serilog.Sinks.Async`. `paket.references` lists them only in
  `Informedica.GenPRES.Server`, `Informedica.MCP.Server` and `Informedica.MCP.Lib`. No core or
  adapter library gets a Serilog reference; `scripts/CheckDependencyRule.fsx` enforces this the
  same way it enforces every other inward-pointing edge.

### Security note (Log4j-class risk)

Log4j's CVE-2021-44228 came from JNDI lookups triggered by *message content* being interpreted as
a lookup expression at runtime, a feature of `log4j-core`'s pattern layout, not a property of
logging libraries generally. Serilog's message templates (`"{GenPresEvent}"` above) are
ordinary strings parsed once per call: an attacker-controlled string that reaches a template
*argument* is captured as a property value and never re-parsed as a template, so it cannot inject
a new placeholder or trigger a second round of expansion. The residual risk is at the sink choice:
`Console` and `File` sinks only (no network sink, no sink that deserializes external input), and
`clientIP`/`path` are logged as opaque property values, never formatted into the template string
itself.

## What shipped

- `feat(logging): back Server and MCP logging with Serilog, relocate AgentLogging (#416)` —
  `AgentLogging` moved to `Informedica.Agents.Lib`; the Serilog bridge and per-`LoggerType`
  composition-root wiring described above; `ServerApi.Compute.fs`'s `bound`/`logged` and a new
  `AppEnv.logger` field replace the unconditional console writes that prompted the original
  report; the MCP stdio host gets its first logger, with a tool-call filter registered via the
  SDK's `WithRequestFilters` hook.
- `refactor(logging): route Core-ring console writes through the injected Logger (#378, #416)` —
  closes the `viaLogger` allow-list in `scripts/CheckDependencyRule.fsx` (21 sites across
  GenSOLVER, GenORDER, GenFORM, Utils, GenUNITS). A print duplicating an existing catch-and-log
  point is deleted; a genuine gap with a short, reachable call chain gets a `logger` parameter
  threaded through it; a genuine gap with no reachable Logger short of rippling a public API well
  beyond this issue's scope is deleted outright rather than routed to a no-op. No behavior change
  to any retained code path. `GenFORM.Lib`'s `Resources.fs` fix is the one that matters beyond the
  dependency rule: `CachedResourceProvider` was swallowing resource-load failures into a raw
  `ConsoleWriter` write, the root cause of the MCP-host stdout-pollution bug fixed next.
- `fix(mcp): keep MCP stdout clean of non-protocol bytes (#416)` — `Console.SetOut Console.Error`
  as the first statement of `Program.fs`'s `main`, before any adapter-ring code can write through
  `ConsoleWriter`; `builder.Logging.AddConsole(fun o -> o.LogToStandardErrorThreshold <-
  LogLevel.Trace)` pins the host's own console logger to stderr explicitly rather than relying on
  the redirect as an ordering accident. `Console.OpenStandardOutput()`, what
  `StdioServerTransport` actually reads from, is unaffected by the redirect. The remaining
  adapter-ring `ConsoleWriter` sites (`ZIndex.Lib`, `ZForm.Lib`, `NKF.Lib`, 9 files) are left as-is
  deliberately: several sit behind `Memoization.memoize`, where a freshly-built `Logger` record
  (no structural equality) would silently defeat the memoization via reference-equality misses,
  and there is no single composition-root injection point given how widely these accessors are
  called. Not a dependency-rule violation either way — adapters may do IO.
- `perf(logging): make solver and order debug logging lazy (#416)` — `logDebugLazy` added to
  `GenSOLVER.Lib`'s `Logger` module and `GenORDER.Lib`'s `Logging` module. Converts ~13 GenSOLVER
  sites in the solver's innermost loop (`Solver.fs`, `Equation.fs`, `Constraint.fs`) and 7
  `Order.fs` sites from eager to lazy Debug logging: no signature or behavior change, just no
  unconditional `Event` allocation, `DateTime.Now` call and dispatch when `GENPRES_LOG` doesn't
  include Debug.
- `docs(logging): close #416 Phase 2 policy items (clientIP retention, audit trail)` — decided and
  documented in DEVELOPMENT.md's ["Request logging: clientIP retention and the audit
  trail"](../../DEVELOPMENT.md#request-logging-clientip-retention-and-the-audit-trail): log
  `clientIP` in full (GenPRES is only reached through the hospital launch sequence, so it is
  practically always an institutional gateway address), and the same structured events make the
  medico-legal audit trail queryable instead of grep-through-a-flat-file.

## Remaining work

The log files are JSON lines with the rendered text as a field. Two things are left for after
this pull request:

- The fields of a message (equation, variable, iteration of a solver step) as JSON fields of
  their own, for the GenSOLVER research trace, serialized on the sink's thread like the text.
  `Serilog.Formatting.Compact` and `Destructurama.FSharp` are not the way: on a scalar the first
  writes one `%A` string, and the second moves the capture back onto the thread that logs.
- The speed of the order and solver formatters: about 4.5 ms per event, 54 s for the 12 068
  events of one Debug pass of three scenarios. It is what the drain of a Debug log costs, on the
  agent logger before and on Serilog now; `Scripts/LoggingPerf.fsx` in the server measures it.

## Verification

`dotnet run Format`, `dotnet run servertests`, `dotnet fsi scripts/CheckDependencyRule.fsx` after
each change. Full suite (`dotnet run ServerTests`) green throughout; `CheckDependencyRule.fsx`'s
`viaLogger` allow-list is empty. Manually confirmed: `GENPRES_LOG=d dotnet run` grows
`data/logs/genpres_request_*.log` per request; a real `initialize` JSON-RPC request piped into the
built MCP binary carries only JSON-RPC frames on stdout, none of the 1859 bytes of interleaved
plain text present before the fix.
