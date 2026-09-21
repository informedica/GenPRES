// What logging costs the thread that logs, measured on identical input for each logger:
//
//   - master's agent logger, rebuilt here exactly as master's `OrderLogging.createAgentLogger`
//     and the server's `getConfig` built it (the three-entry `MessageFormatter`, a file path);
//   - the Serilog bridge as the pull request first had it: `"{EventType} {@Event}"`, a console
//     sink on the calling thread and a file sink behind an async wrapper that blocks when full;
//   - the bridge as it is now, through the server's own `SerilogLogging.buildLoggerAt`, so what
//     is measured is the production sink configuration and not a copy of it.
//
// Three metrics, each the median over `runs` runs after `warmups` warm-up runs:
//
//   A  calling-thread cost per event: a stopwatch around the `Log` calls only, over the events
//      one pass of the scenario set produces at Debug level, recorded once so that every logger
//      sees the same events;
//   B  end to end: wall time of solving the scenario set with logging off, at Informative and
//      at Debug;
//   C  drain: from the last `Log` call until the logger has written everything and is disposed;
//   D  the size of the file one Debug pass leaves: the log analysis refuses a file over 50 MB.
//
// And once, as the yardstick for C: how long it takes to render the text of every event of one
// Debug pass on one thread. Whatever the logger, that work has to be done before the file is
// complete.
//
// The requirement the numbers are held to: B within 5% of master or better at every level, A a
// median under 1 microsecond per event (not held to master: master's call is a bare mailbox
// post), C without the queue reaching its bound.
//
// Build Release first, the measurement references the optimized assemblies:
//   dotnet build src/Informedica.GenPRES.Server/Informedica.GenPRES.Server.fsproj -c Release
// Run from this directory, with stdout discarded (the console sink writes every event to it);
// the table goes to stderr and to LoggingPerf.result.md in the temp directory it names:
//   dotnet fsi LoggingPerf.fsx > /dev/null
// `LOGGINGPERF_RUNS` overrides the number of runs (default 20). A and C take at most 5 of them:
// master's agent needs the better part of a minute to drain one Debug pass, and every run waits
// for the drain so that the next one is not measured against a busy core. A full run takes about
// half an hour for that reason. `LOGGINGPERF_ONLY` keeps the loggers whose name contains it, to
// iterate on one without waiting for master; the "against master" columns then compare with the
// first logger kept.

#I __SOURCE_DIRECTORY__

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

#load "../../../scripts/load-dependencies.fsx"

#r "nuget: Serilog, 4.4.0"
#r "nuget: Serilog.Sinks.Console, 6.1.1"
#r "nuget: Serilog.Sinks.File, 7.0.0"
#r "nuget: Serilog.Sinks.Async, 2.1.0"

#r "../../Informedica.Utils.Lib/bin/Release/net10.0/Informedica.Utils.Lib.dll"
#r "../../Informedica.Logging.Lib/bin/Release/net10.0/Informedica.Logging.Lib.dll"
#r "../../Informedica.Agents.Lib/bin/Release/net10.0/Informedica.Agents.Lib.dll"
#r "../../Informedica.GenUnits.Lib/bin/Release/net10.0/Informedica.GenUnits.Lib.dll"
#r "../../Informedica.GenCore.Lib/bin/Release/net10.0/Informedica.GenCore.Lib.dll"
#r "../../Informedica.GenSolver.Lib/bin/Release/net10.0/Informedica.GenSolver.Lib.dll"
#r "../../Informedica.ZIndex.Lib/bin/Release/net10.0/Informedica.ZIndex.Lib.dll"
#r "../../Informedica.GenForm.Lib/bin/Release/net10.0/Informedica.GenForm.Lib.dll"
#r "../../Informedica.GenOrder.Lib/bin/Release/net10.0/Informedica.GenOrder.Lib.dll"
#r "../bin/Release/net10.0/Informedica.GenPRES.Server.dll"

#load "../../../tests/Informedica.GenORDER.Tests/Scenarios.fs"

open System
open System.IO
open System.Diagnostics

open Serilog
open Serilog.Events

open Informedica.Logging.Lib
open Informedica.Agents.Lib
open Informedica.GenSolver.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Types


let runs =
    match Environment.GetEnvironmentVariable "LOGGINGPERF_RUNS" |> Int32.TryParse with
    | true, n when n > 0 -> n
    | _ -> 20


let warmups = 3


/// Passes with logging off before anything is measured, so that the tiered JIT has settled on
/// the solver's hot paths and the first logger measured is not charged for it.
let settlingPasses = 40


let tempDir =
    let dir = Path.Combine(Path.GetTempPath(), $"genpres-loggingperf-{Guid.NewGuid():N}")
    Directory.CreateDirectory dir |> ignore
    dir


/// The scenario set, the orders built from it once, and the events one pass produces.
module Input =

    let scenarios =
        [
            "pcmDrink", Scenarios.pcmDrink
            "morfCont", Scenarios.morfCont
            "tpnComplete", Scenarios.tpnComplete
        ]


    let orders =
        scenarios
        |> List.map (fun (name, med) ->
            match med |> Medication.toOrderDto Scenarios.testStart |> Order.Dto.fromDto with
            | Ok ord -> name, ord
            | Error msg -> invalidOp $"%s{name} could not be built: %A{msg}"
        )


    /// One pass over the scenario set, every order solved through the full pipeline.
    let solveAll (logger: Logger) =
        for _, ord in orders do
            OrderProcessor.processPipeline logger (SolveOrder ord) |> ignore


    /// The events of one pass at Debug level, in the order they were logged.
    let events =
        let recorded = ResizeArray<Event>()

        let collecting: Logger =
            {
                Log = recorded.Add
                Enabled = fun _ -> true
            }

        solveAll collecting
        recorded.ToArray()


/// A logger under test: the port the domain writes to, and the act that waits until everything
/// logged has been written and releases the logger.
type Subject =
    {
        Logger: Logger
        Drain: unit -> unit
    }


module Subject =

    /// The three-entry formatter master's `OrderLogging.createAgentLogger` built.
    let formatter =
        MessageFormatter.create
            [
                typeof<Logging.OrderMessage>, OrderLogging.formatOrderMessage
                typeof<Informedica.GenSolver.Lib.Types.Logging.SolverMessage>, SolverLogging.formatSolverMessage
                typeof<Informedica.GenForm.Lib.Types.Message>, Informedica.GenForm.Lib.FormLogging.formatMessage
            ]


    /// Master's agent logger on a file, configured as the server's `getConfig` configured it.
    let master (level: Level) (path: string) : Subject =
        let agentLogger =
            AgentLogging.AgentLoggerDefaults.config
            |> AgentLogging.AgentLoggerDefaults.withLevel level
            |> AgentLogging.AgentLoggerDefaults.withMaxMessages (Some 10_000)
            |> AgentLogging.AgentLoggerDefaults.withFlushInterval (TimeSpan.FromSeconds 10.)
            |> AgentLogging.AgentLoggerDefaults.withMinFlushInterval (TimeSpan.FromMilliseconds 10.)
            |> AgentLogging.AgentLoggerDefaults.withMaxFlushInterval (TimeSpan.FromSeconds 20.)
            |> AgentLogging.AgentLoggerDefaults.withFlushThreshold 100
            |> AgentLogging.AgentLoggerDefaults.withFormatter formatter
            |> AgentLogging.createAgentLogger

        agentLogger.Start (Some path) level

        {
            Logger = agentLogger.Logger
            Drain =
                fun () ->
                    agentLogger.StopAsync() |> Async.RunSynchronously
                    (agentLogger :> IDisposable).Dispose()
        }


    let toSerilogLevel =
        function
        | Level.Debug -> LogEventLevel.Debug
        | Level.Informative -> LogEventLevel.Information
        | Level.Warning -> LogEventLevel.Warning
        | Level.Error -> LogEventLevel.Error


    /// The bridge and the sink configuration of the pull request as it first stood, copied from
    /// `SerilogBridge.toLogger` and `SerilogLogging.buildLogger` with the path made a parameter.
    let pullRequest (level: Level) (path: string) : Subject =
        let serilogLogger =
            LoggerConfiguration()
                .MinimumLevel.Is(level |> toSerilogLevel)
                .WriteTo.Console()
                .WriteTo.Async((fun a -> a.File(path) |> ignore), blockWhenFull = true)
                .CreateLogger()

        {
            Logger =
                {
                    Log =
                        fun ev ->
                            serilogLogger.Write(
                                ev.Level |> toSerilogLevel,
                                "{EventType} {@Event}",
                                ev.Message.GetType().Name,
                                ev.Message
                            )
                    Enabled = fun l -> serilogLogger.IsEnabled(l |> toSerilogLevel)
                }
            Drain = fun () -> (serilogLogger :> IDisposable).Dispose()
        }


    /// The bridge as it is now: the server's own sink configuration on the path given.
    let final (level: Level) (path: string) : Subject =
        let serilogLogger = global.Logging.SerilogLogging.buildLoggerAt path level

        {
            Logger = serilogLogger :> Serilog.ILogger |> global.Logging.SerilogBridge.toLogger
            Drain = fun () -> (serilogLogger :> IDisposable).Dispose()
        }


    let all: (string * (Level -> string -> Subject)) list =
        [
            "master agent logger", master
            "pull request as it stood", pullRequest
            "bridge as it is now", final
        ]


module Measure =

    let median (xs: float list) =
        let sorted = xs |> List.sort |> List.toArray
        let n = sorted.Length

        if n % 2 = 1 then
            sorted[n / 2]
        else
            (sorted[n / 2 - 1] + sorted[n / 2]) / 2.


    let mutable private fileCount = 0


    let freshPath () =
        fileCount <- fileCount + 1
        Path.Combine(tempDir, $"genpres_perf_%04i{fileCount}.log")


    let milliseconds (f: unit -> unit) =
        GC.Collect()
        GC.WaitForPendingFinalizers()
        let sw = Stopwatch.StartNew()
        f ()
        sw.Stop()
        sw.Elapsed.TotalMilliseconds


    /// The size of the log file in megabytes; the file is deleted, a full run would otherwise
    /// leave gigabytes behind.
    let sizeAndDelete (path: string) =
        if File.Exists path then
            let size = float (FileInfo path).Length / 1_000_000.
            File.Delete path
            size
        else
            0.


    /// The calling-thread cost per event in nanoseconds, the drain in milliseconds and the file
    /// size in megabytes, for the recorded events logged the way the domain logs them:
    /// `Enabled` asked first.
    let perEvent (create: Level -> string -> Subject) =
        let once () =
            let path = freshPath ()
            let subject = create Level.Debug path
            let logger = subject.Logger

            let logging =
                milliseconds (fun () ->
                    for ev in Input.events do
                        if logger.Enabled ev.Level then
                            logger.Log ev
                )

            let drain = milliseconds subject.Drain
            logging * 1_000_000. / float Input.events.Length, drain, sizeAndDelete path

        once () |> ignore

        let results = [ for _ in 1 .. min runs 5 -> once () ]
        results |> List.map (fun (ns, _, _) -> ns) |> median,
        results |> List.map (fun (_, drain, _) -> drain) |> median,
        results |> List.map (fun (_, _, size) -> size) |> median


    /// The wall time of one pass over the scenario set in milliseconds.
    let endToEnd (create: string -> Subject) =
        let once () =
            let path = freshPath ()
            let subject = create path
            let elapsed = milliseconds (fun () -> Input.solveAll subject.Logger)
            subject.Drain()
            sizeAndDelete path |> ignore
            elapsed

        for _ in 1..warmups do
            once () |> ignore

        [ for _ in 1..runs -> once () ] |> median


let report () =
    for _ in 1..settlingPasses do
        Input.solveAll Logging.noOp

    let off =
        Measure.endToEnd (fun _ ->
            {
                Logger = Logging.noOp
                Drain = ignore
            }
        )

    let only = Environment.GetEnvironmentVariable "LOGGINGPERF_ONLY"

    let rows =
        Subject.all
        |> List.filter (fun (name, _) -> String.IsNullOrWhiteSpace only || name.Contains only)
        |> List.map (fun (name, create) ->
            let nsPerEvent, drain, size = Measure.perEvent create
            let info = Measure.endToEnd (create Level.Informative)
            let debug = Measure.endToEnd (create Level.Debug)
            name, nsPerEvent, drain, size, info, debug
        )

    let _, _, _, _, masterInfo, masterDebug = rows |> List.head

    let relative (x: float) (reference: float) =
        let percent = (x / reference - 1.) * 100.
        $"%+.1f{percent}%%"

    let names = Input.scenarios |> List.map fst |> String.concat ", "

    let aboveDebug =
        Input.events |> Array.filter (fun ev -> ev.Level <> Level.Debug) |> Array.length

    let rendering =
        Measure.milliseconds (fun () ->
            for ev in Input.events do
                Subject.formatter ev.Message |> ignore
        )

    [
        $"Scenario set: %s{names}; %i{Input.events.Length} events per pass at Debug level; median of %i{runs} runs (A and C: %i{min runs 5}) after %i{settlingPasses} settling passes and %i{warmups} warm-ups; Release assemblies."
        ""
        $"End to end with logging off: %.1f{off} ms. Of the events, %i{aboveDebug} are above Debug level. Rendering the text of every event once, on one thread: %.0f{rendering} ms."
        ""
        "| Logger | A: ns per event, calling thread | C: drain, ms | D: file per Debug pass, MB | B: end to end at i, ms | against master | B: end to end at d, ms | against master |"
        "|---|---|---|---|---|---|---|---|"
        for name, nsPerEvent, drain, size, info, debug in rows do
            $"| %s{name} | %.0f{nsPerEvent} | %.0f{drain} | %.1f{size} | %.1f{info} | %s{relative info masterInfo} | %.1f{debug} | %s{relative debug masterDebug} |"
    ]


let lines = report ()

File.WriteAllLines(Path.Combine(tempDir, "LoggingPerf.result.md"), lines)

eprintfn $"\nResults, also in %s{tempDir}:\n"

for line in lines do
    eprintfn $"%s{line}"
