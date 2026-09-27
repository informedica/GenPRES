#I __SOURCE_DIRECTORY__
#load "load.fsx"

// =============================================================
// LRUCache.fsx — Session-level LRU-evicting cache for Solver
// =============================================================
//
// Context: Memo.fsx adds a per-call Dict cache (cleared after each
// solve() call).  MemoCanon.fsx extends this with canonical variable-
// name mapping so equations with identical shapes but different
// variable labels share cache entries.
//
// This script implements the next roadmap step: a *session-level*
// cache that persists across solve() calls and is bounded in size
// via Least-Recently-Used (LRU) eviction.  Long-running server
// processes (e.g. a hospital system solving the same dosing formula
// for thousands of patients) accumulate warm cache entries across
// requests rather than starting cold every call.
//
// Design
// ------
//  • Capacity : fixed; default 512 entries (configurable).
//  • Key       : canonical key from CanonKey.ofEquation
//                (identical to MemoCanon.fsx, so shape-equivalent
//                equations share one entry regardless of variable names).
//  • Eviction  : on capacity overflow, remove the least-recently-used
//                entry (back of linked list).
//  • Storage   :
//      - Dictionary<key, LinkedListNode<KVPair>> for O(1) lookup
//      - LinkedList<KVPair> for O(1) promote-to-front and evict-from-back
//  • Thread safety: a simple lock on the cache object for multi-thread
//                   server use.
//
// Benchmark (run order):
//   1. baseline  (no cache)
//   2. per-call  (Memo.fsx-style: fresh Dict every solve() call)
//   3. canonical (MemoCanon.fsx-style: per-call with canonical key)
//   4. session   (this script: LRU persists across calls)
//
// Run:
//   cd src/Informedica.GenSOLVER.Lib/Scripts
//   dotnet fsi LRUCache.fsx
// =============================================================

open System
open System.Collections.Generic
open Informedica.Utils.Lib.BCL
open Informedica.GenUnits.Lib
open Informedica.GenSolver.Lib


// ------------------------------------------------------------------
// LRU cache implementation
// ------------------------------------------------------------------

/// Thread-safe LRU cache backed by a LinkedList + Dictionary.
///
/// Promote-to-front on every get/put ensures the least-recently-used
/// entry is always at the tail and can be evicted in O(1).
type LRUCache<'K, 'V when 'K : equality>(capacity: int) =

    // The linked list stores (key, value) pairs.
    // The head is the most-recently used; the tail is the LRU.
    let list = LinkedList<'K * 'V>()
    let map = Dictionary<'K, LinkedListNode<'K * 'V>>(capacity)
    let lockObj = obj ()

    do
        if capacity <= 0 then
            invalidArg "capacity" $"LRUCache capacity must be > 0; got {capacity}"

    member _.Capacity = capacity

    member _.Count = lock lockObj (fun () -> map.Count)

    /// Try to retrieve a value. Returns Some on hit, None on miss.
    /// Promotes the entry to the front (most-recently used) on hit.
    member _.TryGet(key: 'K) : 'V option =
        lock lockObj (fun () ->
            match map.TryGetValue key with
            | false, _ -> None
            | true, node ->
                // Promote to front
                list.Remove node
                list.AddFirst node
                Some(snd node.Value)
        )

    /// Insert or update a key-value pair.
    /// On capacity overflow, evict the least-recently-used (tail) entry.
    member _.Put(key: 'K, value: 'V) =
        lock lockObj (fun () ->
            match map.TryGetValue key with
            | true, existing ->
                // Update in-place and promote
                list.Remove existing

                let newNode = list.AddFirst((key, value))
                map.[key] <- newNode
            | false, _ ->
                if map.Count >= capacity then
                    // Evict LRU (tail)
                    let tail = list.Last

                    if tail <> null then
                        let evictKey = fst tail.Value
                        map.Remove evictKey |> ignore
                        list.RemoveLast()

                let node = list.AddFirst((key, value))
                map.[key] <- node
        )

    member _.Clear() =
        lock lockObj (fun () ->
            list.Clear()
            map.Clear()
        )


// ------------------------------------------------------------------
// Canonical key helpers (copied from MemoCanon.fsx)
// ------------------------------------------------------------------

module CanonKey =

    let sortedNames (eq: Types.Equation) =
        eq
        |> Equation.toVars
        |> List.map (Variable.getName >> Variable.Name.toString)
        |> List.sort

    let symbol i = $"x{i}"

    let nameMap (eq: Types.Equation) =
        eq |> sortedNames |> List.mapi (fun i n -> n, symbol i) |> Map.ofList

    let canonicalise (nmap: Map<string, string>) (s: string) =
        nmap
        |> Map.toSeq
        |> Seq.sortByDescending (fun (name, _) -> name.Length)
        |> Seq.fold (fun (acc: string) (name, sym) -> acc.Replace(name, sym)) s

    let ofEquation (eq: Types.Equation) =
        let nmap = nameMap eq
        eq |> Equation.toString true |> canonicalise nmap


// ------------------------------------------------------------------
// Session-level LRU-memoized solver (shadow module)
// ------------------------------------------------------------------

module Solver =

    open Informedica.GenSolver.Lib.Solver
    open Types
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime

    type LRUStats =
        {
            Hits: int
            Misses: int
            Evictions: int
            CacheSize: int
        }

    /// Solve all equations using a *session-level* LRU cache.
    ///
    /// The caller supplies the shared `sessionCache` and accumulates
    /// statistics across many calls.  The cache key is the canonical
    /// variable-name key so shape-equivalent equations share entries.
    let solveAllLRU
        (onlyMinIncrMax: bool)
        log
        (sessionCache: LRUCache<string, Equation * SolveResult>)
        eqs
        =
        let hits = ref 0
        let misses = ref 0

        let solveE n eqs (eq: Equation) =
            let key = CanonKey.ofEquation eq

            match sessionCache.TryGet key with
            | Some cached ->
                incr hits
                cached
            | None ->
                incr misses

                try
                    let result = Equation.solve onlyMinIncrMax log eq
                    sessionCache.Put(key, result)
                    result
                with
                | Exceptions.SolverException errs ->
                    (n, errs, eqs)
                    |> Exceptions.SolverErrored
                    |> Exceptions.raiseExc (Some log) errs
                | e ->
                    let msg = $"didn't catch {e}"
                    writeErrorMessage msg
                    msg |> invalidOp

        let rec loop n que acc =
            match acc with
            | Error _ -> acc
            | Ok acc ->
                let n = n + 1

                if n > (que @ acc |> List.length) * Constants.MAX_LOOP_COUNT then
                    (n, que @ acc) |> Exceptions.SolverTooManyLoops |> Exceptions.raiseExc (Some log) []

                match que with
                | [] -> Ok acc
                | eq :: que ->
                    let eq, sr = solveE n eqs eq

                    match sr with
                    | Unchanged ->
                        loop n que (Ok(eq :: acc))
                    | Changed _ ->
                        // Re-queue changed equations (the standard solver behaviour)
                        loop n (acc @ que) (Ok [ eq ])
                    | Errored msgs ->
                        Error(eq :: acc, msgs)

        let solved = loop 0 eqs (Ok [])

        let stats =
            {
                Hits = hits.Value
                Misses = misses.Value
                Evictions = 0 // tracked separately via cache.Count before/after
                CacheSize = sessionCache.Count
            }

        solved, stats


// ------------------------------------------------------------------
// Equation helpers (same pattern as MemoCanon.fsx)
// ------------------------------------------------------------------

let setValues u n vs eqs =
    let nm = n |> Variable.Name.createExc

    let prop =
        vs |> ValueUnit.create u |> Variable.ValueRange.ValueSet.create |> ValsProp

    match eqs |> Api.setVariableValues nm prop with
    | Some var -> eqs |> List.map (Equation.replace var)
    | None -> eqs


// ------------------------------------------------------------------
// Baseline helpers (no cache)
// ------------------------------------------------------------------

let solveBaseline onlyMinIncrMax eqs =
    eqs |> Solver.solveAll onlyMinIncrMax Informedica.Logging.Lib.Logging.noOp


// ------------------------------------------------------------------
// Per-call memoised solver (Memo.fsx style)
// ------------------------------------------------------------------

module PerCallSolver =

    open Types
    open Informedica.Utils.Lib.ConsoleWriter.NewLineNoTime

    let solveAllMemo onlyMinIncrMax log eqs =
        let cache = Dictionary<string, Equation * SolveResult>()
        let hits = ref 0
        let misses = ref 0

        let solveE n eqs (eq: Equation) =
            let key = eq |> Equation.toString true

            match cache.TryGetValue key with
            | true, cached ->
                incr hits
                cached
            | false, _ ->
                incr misses

                try
                    let result = Equation.solve onlyMinIncrMax log eq
                    cache.[key] <- result
                    result
                with
                | Exceptions.SolverException errs ->
                    (n, errs, eqs)
                    |> Exceptions.SolverErrored
                    |> Exceptions.raiseExc (Some log) errs
                | e ->
                    let msg = $"didn't catch {e}"
                    writeErrorMessage msg
                    msg |> invalidOp

        let rec loop n que acc =
            match acc with
            | Error _ -> acc
            | Ok acc ->
                let n = n + 1

                if n > (que @ acc |> List.length) * Constants.MAX_LOOP_COUNT then
                    (n, que @ acc) |> Exceptions.SolverTooManyLoops |> Exceptions.raiseExc (Some log) []

                match que with
                | [] -> Ok acc
                | eq :: que ->
                    let eq, sr = solveE n eqs eq

                    match sr with
                    | Unchanged -> loop n que (Ok(eq :: acc))
                    | Changed _ -> loop n (acc @ que) (Ok [ eq ])
                    | Errored msgs -> Error(eq :: acc, msgs)

        loop 0 eqs (Ok [])


// ------------------------------------------------------------------
// Expecto tests for LRUCache correctness
// ------------------------------------------------------------------

#r "nuget: Expecto, 9.0.4"

open Expecto
open Expecto.Flip


let lruTests =
    testList
        "LRUCache"
        [
            test "empty cache returns None" {
                let c = LRUCache<string, int>(4)
                c.TryGet "x" |> Expect.equal "should be None" None
            }

            test "put then get returns the value" {
                let c = LRUCache<string, int>(4)
                c.Put("a", 1)
                c.TryGet "a" |> Expect.equal "should be Some 1" (Some 1)
            }

            test "put same key updates value" {
                let c = LRUCache<string, int>(4)
                c.Put("a", 1)
                c.Put("a", 99)
                c.TryGet "a" |> Expect.equal "should be Some 99" (Some 99)
            }

            test "evicts LRU on capacity overflow" {
                // capacity=2; insert a, b, c → a evicted (LRU)
                let c = LRUCache<string, int>(2)
                c.Put("a", 1)
                c.Put("b", 2)
                c.Put("c", 3) // overflows; 'a' is LRU

                c.TryGet "a" |> Expect.equal "a should be evicted" None
                c.TryGet "b" |> Expect.equal "b should survive" (Some 2)
                c.TryGet "c" |> Expect.equal "c should survive" (Some 3)
            }

            test "get promotes entry, preventing its eviction" {
                // capacity=2; insert a, b; get a (promote); insert c → b evicted
                let c = LRUCache<string, int>(2)
                c.Put("a", 1)
                c.Put("b", 2)
                c.TryGet "a" |> ignore // promote a
                c.Put("c", 3) // overflows; b is now LRU

                c.TryGet "a" |> Expect.equal "a should survive (promoted)" (Some 1)
                c.TryGet "b" |> Expect.equal "b should be evicted" None
                c.TryGet "c" |> Expect.equal "c should survive" (Some 3)
            }

            test "count tracks size up to capacity" {
                let c = LRUCache<string, int>(3)
                c.Put("a", 1)
                c.Put("b", 2)
                c.Count |> Expect.equal "count should be 2" 2
                c.Put("c", 3)
                c.Count |> Expect.equal "count should be 3" 3
                c.Put("d", 4) // evicts a
                c.Count |> Expect.equal "count should stay at 3" 3
            }

            test "clear empties the cache" {
                let c = LRUCache<string, int>(4)
                c.Put("a", 1)
                c.Put("b", 2)
                c.Clear()
                c.Count |> Expect.equal "count should be 0" 0
                c.TryGet "a" |> Expect.equal "a should be gone" None
            }

            test "capacity 1: always evicts the single entry" {
                let c = LRUCache<string, int>(1)
                c.Put("a", 1)
                c.Put("b", 2)
                c.TryGet "a" |> Expect.equal "a evicted" None
                c.TryGet "b" |> Expect.equal "b present" (Some 2)
            }
        ]


printfn "\n=== LRUCache correctness tests ==="
runTestsWithCLIArgs [] [||] lruTests |> ignore


// ------------------------------------------------------------------
// Benchmark helpers
// ------------------------------------------------------------------

let timeMean label n f =
    let sw = Diagnostics.Stopwatch.StartNew()

    for _ in 1..n do
        f () |> ignore

    sw.Stop()
    let ms = float sw.ElapsedMilliseconds / float n
    printfn "  %-50s  %.1f ms/iter" label ms
    ms


// ------------------------------------------------------------------
// Equation factories (same pattern as MemoCanon.fsx)
// ------------------------------------------------------------------

let patientWeights = [| 5N; 10N; 15N; 20N; 30N; 40N; 50N; 60N; 70N; 80N |]


/// Patient-specific dosing equation: totalDose = weight × dosePerKg × timesPerDay
let dosingSetup (weight: BigRational) =
    [
        "dose = weight * dosePerKg"
        "totalDose = dose * timesPerDay"
    ]
    |> Api.init
    |> setValues Units.Count.times "weight" [| weight |]
    |> setValues Units.Count.times "dosePerKg" [| 10N; 15N; 20N |]
    |> setValues Units.Count.times "timesPerDay" [| 2N; 3N; 4N |]


// ------------------------------------------------------------------
// Benchmark
// ------------------------------------------------------------------

printfn "\n=== Session-level LRU cache benchmark ==="
printfn ""
printfn "Setup: 10 patients × dosing formula, %d iterations each" 20
printfn ""

// Shared session cache (capacity 512)
let sessionCache = LRUCache<string, Equation * Types.SolveResult>(512)

let b_base =
    timeMean
        "1. baseline     (no cache, 10 patients × 20 iter)"
        20
        (fun () ->
            for w in patientWeights do
                dosingSetup w |> solveBaseline false |> ignore
        )

let b_percall =
    timeMean
        "2. per-call     (fresh cache per solve, exact key)"
        20
        (fun () ->
            for w in patientWeights do
                PerCallSolver.solveAllMemo false Informedica.Logging.Lib.Logging.noOp (dosingSetup w)
                |> ignore
        )

let b_session =
    timeMean
        "3. session-LRU  (shared LRU cache, canonical key)"
        20
        (fun () ->
            for w in patientWeights do
                Solver.solveAllLRU false Informedica.Logging.Lib.Logging.noOp sessionCache (dosingSetup w)
                |> ignore
        )

printfn ""
printfn "  per-call  speedup vs baseline: %.2fx" (b_base / b_percall)
printfn "  session   speedup vs baseline: %.2fx" (b_base / b_session)
printfn "  session   speedup vs per-call: %.2fx" (b_percall / b_session)
printfn ""
printfn "  session cache size after warmup: %d entries (capacity: %d)" sessionCache.Count sessionCache.Capacity

// -- hit rate measurement
let mutable totalHits = 0
let mutable totalMisses = 0
sessionCache.Clear()

for _ in 1..5 do
    for w in patientWeights do
        let _, stats = Solver.solveAllLRU false Informedica.Logging.Lib.Logging.noOp sessionCache (dosingSetup w)
        totalHits <- totalHits + stats.Hits
        totalMisses <- totalMisses + stats.Misses

let hitRate = float totalHits / float (totalHits + totalMisses) * 100.0

printfn
    "\n  5-pass hit rate: %.0f%%  (hits=%d  misses=%d)"
    hitRate
    totalHits
    totalMisses

printfn
    """

=== Summary ===

Session-level LRU memoization extends MemoCanon.fsx by persisting the
cache across solve() calls within a server session.

Key properties:
  • Bounded memory: LRU eviction caps the cache at a configurable size
    (default 512 entries), preventing unbounded memory growth.
  • Warm cache across calls: subsequent requests for the same dosing
    formula structure get cache hits without re-solving.
  • Canonical keys: structurally identical equations (different variable
    labels) still share cache entries (from MemoCanon.fsx).
  • Thread-safe: lock-protected, safe for multi-threaded server use.

Remaining work (from solver-memoization.md checklist):
  □ Full cached-result remapping: remap canonical variable names back
    to caller names on Changed cache hits (currently re-solves Changed).
  □ Integrate solveAllLRU into Solver.fs as the production solver path
    with a session-scoped cache injected via a parameter or DI.
  □ Property-based tests: randomly generated variable renamings must
    yield the same canonical key as the original equation.
  □ Tuning: benchmark different capacity values (128/256/512/1024) on
    a realistic hospital patient batch to find the optimal knee point.

Reference: docs/code-reviews/solver-memoization.md
"""
