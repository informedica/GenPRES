// A/B micro-benchmark: the two-tier RationalX (the live BigRational alias in
// Informedica.Utils.Lib.BCL) vs the original MathNet.Numerics.BigRational.
//
// Workload mirrors the GenSOLVER hot path: a pool of base-unit fractions run
// through (a * b) / c, summed via ToDouble so the JIT cannot dead-code-
// eliminate the result.
//
// Run from this directory:  dotnet run -c Release

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

type RX = Informedica.Utils.Lib.BCL.BigRational // = RationalX
type MB = MathNet.Numerics.BigRational


module Pool =

    // Base-unit fractions reconstructed from a real GenPRES OrderContext log.
    // Denominators are products of small primes (from /1000, /86400, ...), so
    // numerators cancel hard and RationalX rarely spills out of int64.
    let pairs: (int64 * int64)[] =
        [|
            (1L, 10000L) // 0.1 mL -> 1e-4 L
            (7L, 86400000000L) // 7 mg/kg/day in base units
            (727L, 100000000L)
            (1L, 1000L)
            (3L, 1000L)
            (5L, 2L)
            (1L, 3L)
            (2L, 3L)
            (10L, 86400L)
            (250L, 1000L)
            (1L, 7L)
            (40L, 1000L)
            (1L, 24L)
            (9L, 100L)
            (1L, 2L)
            (3L, 5L)
            (500L, 1000L)
            (1L, 86400L)
            (60L, 1000L)
            (4L, 3L)
            (15L, 1000L)
            (1L, 1000000L)
            (3L, 8L)
            (11L, 50L)
        |]


module Check =

    let private toRX (n: int64, d: int64) = RX.FromInt64Fraction(n, d)

    let private toMB (n: int64, d: int64) = MB.FromBigIntFraction(bigint n, bigint d)

    /// (maxAbsDoubleDiff, spillCount, total) for (a*b)/c across the pool.
    let run () =
        let pool = Pool.pairs
        let m = pool.Length
        let mutable maxDiff = 0.0
        let mutable spills = 0
        let mutable total = 0

        for i in 0 .. m - 1 do
            let a = pool[i % m]
            let b = pool[(i * 7 + 1) % m]
            let c = pool[(i * 13 + 2) % m]
            let rx = (toRX a * toRX b) / toRX c
            let mb = (toMB a * toMB b) / toMB c
            let diff = abs (RX.ToDouble rx - MB.ToDouble mb)

            if diff > maxDiff then
                maxDiff <- diff

            total <- total + 1
            // a value spills if it did not narrow back to the int64 (small) tier
            if rx.IsSpilled then
                spills <- spills + 1

        maxDiff, spills, total


[<MemoryDiagnoser>]
type Benchmarks() =

    let ops = 1000

    let mutable mb: MB[] = [||]
    let mutable rx: RX[] = [||]

    [<GlobalSetup>]
    member _.Setup() =
        mb <-
            Pool.pairs
            |> Array.map (fun (n, d) -> MB.FromBigIntFraction(bigint n, bigint d))

        rx <- Pool.pairs |> Array.map (fun (n, d) -> RX.FromInt64Fraction(n, d))

    [<Benchmark(Baseline = true)>]
    member _.MathNet_BigRational() =
        let m = mb.Length
        let mutable s = 0.0

        for i in 0 .. ops - 1 do
            let r = (mb[i % m] * mb[(i * 7 + 1) % m]) / mb[(i * 13 + 2) % m]
            s <- s + MB.ToDouble r

        s

    [<Benchmark>]
    member _.RationalX_CrossReduce() =
        let m = rx.Length
        let mutable s = 0.0

        for i in 0 .. ops - 1 do
            let r = (rx[i % m] * rx[(i * 7 + 1) % m]) / rx[(i * 13 + 2) % m]
            s <- s + RX.ToDouble r

        s


[<EntryPoint>]
let main _ =
    let maxDiff, spills, total = Check.run ()
    printfn "Correctness gate: max |double diff| vs MathNet = %.3e over %d ops" maxDiff total
    printfn "Spill rate (results not fitting int64): %d / %d" spills total

    if maxDiff > 1e-9 then
        printfn "CORRECTNESS GATE FAILED — aborting benchmark"
        1
    else
        printfn "Correctness gate passed; running benchmarks..."
        BenchmarkRunner.Run<Benchmarks>() |> ignore
        0
