// Arithmetic-bound A/B benchmark: large ValueUnit.calc cartesian products.
//
// ValueUnit.calc runs BigRational.calcCartesian over the cross product of the
// two operands' base values, then Array.distinct. This is the hot path that
// RationalX should accelerate (cross-reduced int64 arithmetic, zero alloc,
// plus cheaper equality/hashing for the distinct step).
//
// The same source compiles in both trees because values are built via the
// version-agnostic BigRational.fromInt module function (MathNet in master,
// RationalX in the perf/rational-x worktree).
//
// Run:  dotnet run -c Release            (full BenchmarkDotNet run)
//       dotnet run -c Release -- quick   (stopwatch only, fast)

open System.Diagnostics

open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib

open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

/// Deterministic pool of distinct-ish fractions (seeded, identical in both trees).
let mkVals (seed: int) (n: int) (maxv: int) =
    let rnd = System.Random seed

    Array.init
        n
        (fun _ ->
            BigRational.fromInt (rnd.Next(1, maxv))
            / BigRational.fromInt (rnd.Next(1, maxv))
        )

// Units: vu1 = mg/mL, vu2 = mL  ->  (*) yields mg and exercises toBaseValue.
let private mgPerMl = Units.Mass.milliGram |> ValueUnit.per Units.Volume.milliLiter
let private ml = Units.Volume.milliLiter
let private times = Units.Count.times

let private resultSize (vu: ValueUnit) = (vu |> ValueUnit.getValue).Length


[<MemoryDiagnoser>]
type ValueUnitBenchmarks() =

    // 400 x 400 = 160k cross products for the mixed-unit multiply
    let mutable mul1 = Unchecked.defaultof<ValueUnit>
    let mutable mul2 = Unchecked.defaultof<ValueUnit>
    // 600 x 600 = 360k cross products for the same-unit add
    let mutable add1 = Unchecked.defaultof<ValueUnit>
    let mutable add2 = Unchecked.defaultof<ValueUnit>

    [<GlobalSetup>]
    member _.Setup() =
        mul1 <- ValueUnit.create mgPerMl (mkVals 1 400 1000)
        mul2 <- ValueUnit.create ml (mkVals 2 400 1000)
        add1 <- ValueUnit.create times (mkVals 3 600 1000)
        add2 <- ValueUnit.create times (mkVals 4 600 1000)

    [<Benchmark>]
    member _.Mul_mgPerMl_x_mL_400() = ValueUnit.calc true (*) mul1 mul2 |> resultSize

    [<Benchmark>]
    member _.Add_times_600() = ValueUnit.calc true (+) add1 add2 |> resultSize


let quickReport () =
    let cases =
        [
            "Mul mg/mL x mL 400",
            (fun () ->
                let a = ValueUnit.create mgPerMl (mkVals 1 400 1000)
                let b = ValueUnit.create ml (mkVals 2 400 1000)
                fun () -> ValueUnit.calc true (*) a b |> resultSize
            )
            "Add times 600",
            (fun () ->
                let a = ValueUnit.create times (mkVals 3 600 1000)
                let b = ValueUnit.create times (mkVals 4 600 1000)
                fun () -> ValueUnit.calc true (+) a b |> resultSize
            )
        ]

    printfn "Large ValueUnit.calc cartesian products (mean of 10 runs after 3 warmups):"
    printfn "%-22s %12s %12s" "case" "mean (ms)" "result size"

    for name, prep in cases do
        let run = prep ()

        for _ in 1..3 do
            run () |> ignore

        let sw = Stopwatch.StartNew()
        let reps = 10
        let mutable sz = 0

        for _ in 1..reps do
            sz <- run ()

        sw.Stop()
        printfn "%-22s %12.3f %12d" name (sw.Elapsed.TotalMilliseconds / float reps) sz


[<EntryPoint>]
let main argv =
    match argv with
    | [| "quick" |] ->
        quickReport ()
        0
    | _ ->
        quickReport ()
        printfn "\nRunning BenchmarkDotNet..."
        BenchmarkRunner.Run<ValueUnitBenchmarks>() |> ignore
        0
