// Integrated end-to-end benchmark: drive the real GenORDER scenario templates
// (Scenarios.fs) through OrderProcessor.processPipeline (the GenSOLVER-heavy
// solve, the hot path RationalX accelerates).
//
// The same harness is compiled against whichever BigRational backing is in the
// tree: build/run it in the perf/rational-x worktree (RationalX) and on master
// (original MathNet BigRational), then compare the numbers.
//
// Run:  dotnet run -c Release           (full BenchmarkDotNet run)
//       dotnet run -c Release -- quick  (per-scenario stopwatch only, fast)

open System.Diagnostics
open System.Collections.Generic

open Informedica.Utils.Lib
open Informedica.Logging.Lib
open Informedica.GenOrder.Lib
open Informedica.GenOrder.Lib.Types

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

let private noLogger = Logging.noOp

/// All scenario templates we exercise, by name.
let scenarioMeds: (string * Medication)[] =
    [|
        "pcmSupp", Scenarios.pcmSupp
        "pcmDrink", Scenarios.pcmDrink
        "amfo", Scenarios.amfo
        "morfCont", Scenarios.morfCont
        "cotrim", Scenarios.cotrim
        "tpn", Scenarios.tpn
        "tpnComplete", Scenarios.tpnComplete
        "fullMedication", Scenarios.fullMedication
    |]

/// Build an Order from a Medication template (not timed — done once in setup).
let buildOrder (med: Medication) : Order =
    med
    |> Medication.toOrderDto Scenarios.testStart
    |> Order.Dto.fromDto
    |> Result.get

/// Solve one order through the full pipeline; return a cheap hash so the JIT /
/// BenchmarkDotNet cannot dead-code-eliminate the work.
let solve (ord: Order) : int =
    OrderProcessor.processPipeline noLogger (SolveOrder ord)
    |> box
    |> LanguagePrimitives.PhysicalHash


[<MemoryDiagnoser>]
type ScenarioBenchmarks() =

    let mutable orders: (string * Order)[] = [||]

    [<GlobalSetup>]
    member _.Setup() =
        orders <-
            scenarioMeds
            |> Array.choose (fun (name, med) ->
                try
                    Some(name, buildOrder med)
                with e ->
                    printfn "  ! could not build order for %s: %s" name e.Message
                    None
            )

    [<Benchmark>]
    member _.SolveAllScenarios() =
        let mutable sink = 0

        for _, ord in orders do
            sink <- sink + solve ord

        sink


/// Per-scenario stopwatch report (warm + repeated), useful as a quick A/B
/// readout without a full BenchmarkDotNet run.
let quickReport () =
    printfn "Per-scenario solve time (mean of 20 runs after 5 warmups):"
    printfn "%-16s %12s" "scenario" "mean (ms)"

    let mutable totalMs = 0.0

    for name, med in scenarioMeds do
        try
            let ord = buildOrder med

            for _ in 1..5 do
                solve ord |> ignore // warmup / JIT

            let sw = Stopwatch.StartNew()
            let reps = 20
            let mutable sink = 0

            for _ in 1..reps do
                sink <- sink + solve ord

            sw.Stop()
            let meanMs = sw.Elapsed.TotalMilliseconds / float reps
            totalMs <- totalMs + meanMs
            printfn "%-16s %12.3f   (sink=%d)" name meanMs sink
        with e ->
            printfn "%-16s %12s   (%s)" name "ERROR" e.Message

    printfn "%-16s %12.3f" "TOTAL" totalMs


/// Coarse stage profiler. Times the two DTO stages directly, and attributes
/// time inside the SolveOrder pipeline to its named steps by diffing the
/// "PIPELINE START/END {step}" boundary events the pipeline emits.
let profile () =
    let stepTotals = Dictionary<string, float>()
    let stack = Stack<string * float>()
    let sw = Stopwatch.StartNew()

    let add (d: Dictionary<string, float>) k v =
        d[k] <-
            (match d.TryGetValue k with
             | true, x -> x
             | _ -> 0.0)
            + v

    // extract a "PIPELINE START/END <name>" boundary from a scenario log string
    let boundary (s: string) =
        let grab (marker: string) =
            let i = s.IndexOf marker

            if i < 0 then
                None
            else
                let rest = s.Substring(i + marker.Length)
                let j = rest.IndexOf " ==="
                if j < 0 then None else Some(rest.Substring(0, j))

        match grab "PIPELINE START " with
        | Some n -> Some(true, n)
        | None -> grab "PIPELINE END " |> Option.map (fun n -> (false, n))

    let timingLogger: Logger =
        {
            Log =
                fun (e: Event) ->
                    match e.Message with
                    | :? Informedica.GenOrder.Lib.Types.Logging.OrderMessage as om ->
                        match om with
                        | Informedica.GenOrder.Lib.Types.Logging.OrderEventMessage(Informedica.GenOrder.Lib.Types.Events.OrderScenario s) ->
                            match boundary s with
                            | Some(true, name) -> stack.Push(name, sw.Elapsed.TotalMilliseconds)
                            | Some(false, name) ->
                                if stack.Count > 0 then
                                    let (_, t0) = stack.Pop()
                                    add stepTotals name (sw.Elapsed.TotalMilliseconds - t0)
                            | None -> ()
                        | _ -> ()
                    | _ -> ()
            Enabled = fun _ -> true
        }

    let stageTotals = Dictionary<string, float>()
    let reps = 20

    // warmup
    for _, med in scenarioMeds do
        try
            let ord = buildOrder med
            OrderProcessor.processPipeline noLogger (SolveOrder ord) |> ignore
        with _ ->
            ()

    for _ in 1..reps do
        for _, med in scenarioMeds do
            try
                let t0 = sw.Elapsed.TotalMilliseconds
                let dto = Medication.toOrderDto Scenarios.testStart med
                let t1 = sw.Elapsed.TotalMilliseconds
                let ord = dto |> Order.Dto.fromDto |> Result.get
                let t2 = sw.Elapsed.TotalMilliseconds
                OrderProcessor.processPipeline timingLogger (SolveOrder ord) |> ignore
                let t3 = sw.Elapsed.TotalMilliseconds
                add stageTotals "build: Medication.toOrderDto" (t1 - t0)
                add stageTotals "build: Order.Dto.fromDto" (t2 - t1)
                add stageTotals "solve: processPipeline(SolveOrder)" (t3 - t2)
            with _ ->
                ()

    let perRun (v: float) = v / float reps

    printfn "Stage breakdown (mean per full pass over all %d scenarios, %d reps):\n" scenarioMeds.Length reps
    printfn "%-42s %10s" "stage" "mean (ms)"
    let solveTotal = perRun stageTotals["solve: processPipeline(SolveOrder)"]

    for KeyValue(k, v) in stageTotals |> Seq.sortByDescending (fun kv -> kv.Value) do
        printfn "%-42s %10.3f" k (perRun v)

    printfn "\nInside solve: per pipeline-step (sum across scenarios, mean per rep):\n"
    printfn "%-42s %10s %8s" "pipeline step" "mean (ms)" "% solve"

    for KeyValue(k, v) in stepTotals |> Seq.sortByDescending (fun kv -> kv.Value) do
        let ms = perRun v
        printfn "%-42s %10.3f %7.1f%%" k ms (100.0 * ms / solveTotal)


/// Long-running loop so an external sampling profiler (dotnet-trace) can
/// collect CPU samples of the solve path. Runs for ~`secs` seconds.
let traceLoop (secs: float) =
    let orders =
        scenarioMeds
        |> Array.choose (fun (_, m) ->
            try
                Some(buildOrder m)
            with _ ->
                None
        )

    let sw = Stopwatch.StartNew()
    let mutable passes = 0
    let mutable sink = 0

    while sw.Elapsed.TotalSeconds < secs do
        for ord in orders do
            sink <-
                sink
                + (OrderProcessor.processPipeline noLogger (SolveOrder ord)
                   |> box
                   |> LanguagePrimitives.PhysicalHash)

        passes <- passes + 1

    printfn "trace loop done: %d passes in %.1fs (sink=%d)" passes sw.Elapsed.TotalSeconds sink


[<EntryPoint>]
let main argv =
    match argv with
    | [| "trace" |] ->
        traceLoop 25.0
        0
    | [| "profile" |] ->
        profile ()
        0
    | [| "quick" |] ->
        quickReport ()
        0
    | _ ->
        printfn "Warming up scenario build + solve..."
        quickReport ()
        printfn "\nRunning BenchmarkDotNet (SolveAllScenarios)..."
        BenchmarkRunner.Run<ScenarioBenchmarks>() |> ignore
        0
