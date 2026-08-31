# RationalX performance investigation — summary

**Branch:** `perf/rational-x` (isolated git worktree)
**Status:** merged. This document is the point-in-time investigation write-up; the work it proposes has since landed on `master` — `BCL/RationalX.fs` is compiled into `Informedica.Utils.Lib` and `BigRational` is an alias for `RationalX`. All changes were `.fs` source written under an explicit waiver of the script-only policy, with the human contributor as gatekeeper for the merge.
**Test status:** full suite green throughout — **5526 passed, 0 failed, 2 skipped**.

---

## 1. Goal

Replace the rational-number backing of all dosing math — `MathNet.Numerics.BigRational`
(a reference type; every op allocates and routes through `BigInteger`) — with a
faster two-tier struct, `RationalX`, exposed under the existing `BigRational`
name so the ~47 consuming files compile unchanged. Then measure, profile, and
chase the bottlenecks the change exposed.

---

## 2. What was changed (in order)

### 2.1 `RationalX` type + global drop-in alias
- **New** `src/Informedica.Utils.Lib/BCL/RationalX.fs`: a `[<Struct; CustomEquality; CustomComparison>]`
  two-tier rational. Small tier = reduced `int64` numerator/denominator (the
  common, allocation-free path) with cross-reduction + branchless overflow
  detection (`Math.BigMul`, sign-bit tests); spill tier wraps the existing
  MathNet `BigRational` only on int64 overflow. Full MathNet API parity
  (`FromInt/FromBigInt/FromDecimal/ToDouble/ToInt32/ToBigInt/Abs/Parse/Numerator/
  Denominator/+ - * / ~-`), `NumericLiteralN` (so `0N`…`1000N` work), `IEquatable<RationalX>`,
  and `type BigRational = RationalX`.
- `BCL/BigRational.fs`: dropped `open MathNet.Numerics`; added the `ModuleSuffix`
  compilation representation so the `BigRational` **module** and the new
  `BigRational` **type** coexist.
- Migration: removed `open MathNet.Numerics` from ~31 src + ~13 test files
  (replaced with `open Informedica.Utils.Lib.BCL`). `Double.fs`/`BigInteger.fs`
  keep MathNet (compiled before `RationalX.fs`; internal numeric use only).
- **Canonical-representation invariant**: any value that fits int64 is always
  stored small — guarantees one representation per value, so equality/hashing
  stay consistent for `Array.distinct`, `Set`, and `Map` keys.

### 2.2 Struct shrink — 32 B → 24 B
- Replaced the struct **DU** (which spends 8 B on a tag) with a hand-rolled
  struct `(int64 P, int64 Q, BigCell Big)` where `Big = null` is the small/big
  discriminator. Removes the tag word; small case stays inline and alloc-free.

### 2.3 Unboxed dedup in `ValueUnit.calc`
- Added `BigRational.distinct` (order-preserving, `HashSet<BigRational>()` with
  the default `EqualityComparer`, which uses the unboxed `IEquatable` path) next
  to `calcCartesian`, and switched `ValueUnit.calc` (`ValueUnit.fs`) to use it.
  F#'s structural `Array.distinct` boxes every `[<CustomEquality>]` struct; this
  avoids it.

### 2.4 Eager-logging guards
- Added `Logging.isActive logger` (true unless the no-op logger) and guarded
  every eager *build-then-log* site in the solve path so the expensive console
  table / constraint dump is built **only when a logger will consume it**:
  `Order.logOrder`, and `OrderProcessor` `setCalculatedConstraintsStep`,
  `applyConstraintsStep`, `logUnmatched`.

### 2.5 Dead-code removal in the solver
- `GenSOLVER/Equation.fs`: removed a provably-dead per-iteration `List.sortBy`
  (its output was re-sorted by the next iteration before `calcVars` ran, and the
  loop's final list is consumed by index). Behaviour-preserving; perf-neutral.

---

## 3. Benchmark results

A/B method: build + run the same harness in this worktree (RationalX) and on
`master` (original BigRational); BenchmarkDotNet means. Harnesses live in
`benchmark/{RationalXBench,ValueUnitBench,ScenarioBench}` (gitignored by the
opt-in policy; build/run with `dotnet run -c Release`).

### 3.1 Raw arithmetic micro-benchmark (`RationalXBench`)
`(a*b)/c` over a real base-unit fraction pool:

| | MathNet BigRational | RationalX |
|---|---|---|
| Time | 71.8 µs | **16.6 µs (≈4.3×)** |
| Allocated | 156 KB | **0 B** |
| Spill rate | — | 0 / 24 |

### 3.2 Arithmetic-bound path — `ValueUnit.calc` cartesian (`ValueUnitBench`)

| Case | BigRational | RX 32 B | RX 24 B | **RX 24 B + unboxed distinct** |
|---|---|---|---|---|
| Mul mg/mL × mL (160k) — time | 75.6 ms | 31.4 ms | 25.3 ms | **20.1 ms (3.8×)** |
| Mul — alloc | 57.2 MB | 46.0 MB | 36.2 MB | **21.1 MB (−63%)** |
| Add Count (360k) — time | 85.0 ms | 59.1 ms | 59.4 ms | **39.4 ms (2.2×)** |
| Add — alloc | 57.7 MB | 99.9 MB | 80.2 MB | **49.0 MB (−15%)** |

Note the journey: the 32 B struct DU *regressed* store-heavy allocation (+73%);
the 24 B shrink and the unboxed dedup together turned it into a win on both axes.
Distinct result sets identical throughout.

### 3.3 End-to-end — GenORDER scenario solve (`ScenarioBench`, 8 scenarios)

| Variant | Time | Allocated |
|---|---|---|
| BigRational baseline (master) | 153.9 ms | 301.8 MB |
| RationalX (24 B + unboxed distinct) | 148.2 ms | 263.7 MB |
| **+ eager-logging guards** | **117.4 ms** | **192.4 MB** |

**Net vs original baseline: −24% time, −36% allocation, zero correctness cost**
(per-scenario result hashes bit-identical; all tests pass).

---

## 4. Key findings

1. **RationalX wins where work is arithmetic-bound** (2–4×, less memory), is
   modestly positive end-to-end, and is correctness-neutral everywhere.
2. **Amdahl in action.** Speedup shrinks up the stack as the rational-arithmetic
   fraction shrinks: raw 4.3× → `ValueUnit.calc` 2–4× → full solve ~1.04×
   (the solve is *not* bottlenecked on rational arithmetic).
3. **The biggest end-to-end win was not the rational type — it was logging.**
   `logOrder` built a full console table (sort + ConsoleTables + Wcwidth +
   heavy allocation) on every solve step and handed it to a `noOp` logger that
   discarded it. Guarding it cut ~21% off the (already optimized) solve.
4. **Struct width matters for store-heavy paths.** A 24 B struct beats a 32 B
   one when large arrays of values dominate; and a `[<CustomEquality>]` struct
   must avoid boxing in `Array.distinct` (use a typed comparer).
5. **Sampling-profiler self-time is not wall-clock under heavy GC.**
   `dotnet-sampled-thread-time` attributed ~34% to `List.sortBy` in the solver;
   a controlled A/B (disable the sort, measure) showed it costs ≈0 — the time
   was GC/allocation charged to whatever frame was on the stack (`PollGC` = 51%).
   The real solver cost is broad allocation pressure, not any single line.

---

## 5. Where the scenario solve time goes (after the wins above)

- DTO build/parse (`toOrderDto` + `fromDto`): ~6%.
- Solve pipeline: ~94%, dominated by **allocation/GC** (`PollGC` ~51% of samples).
  Actual numeric kernels (`minIncrMaxToValues` ~6%, `Solver.solveAll` ~4%) are small.
- **Next frontier:** holistic allocation reduction in `Equation.loop` (it rebuilds
  immutable `vars` lists every propagation step via `List.map`/`replace`/`rotations`/
  `sortBy`). A careful array-based / in-place refactor of core solver logic —
  high validation cost, not a quick win.

---

## 6. Files changed

**New**
- `src/Informedica.Utils.Lib/BCL/RationalX.fs` — the type, `NumericLiteralN`, alias.
- `src/Informedica.Utils.Lib/Scripts/RationalXCheck.fsx` — FSI correctness gate.
- `benchmark/RationalXBench/`, `benchmark/ValueUnitBench/`, `benchmark/ScenarioBench/` —
  A/B harnesses (ScenarioBench also has `profile` and `trace` modes).

**Modified (36 tracked `.fs`)**
- `Utils.Lib`: `BCL/BigRational.fs` (ModuleSuffix, removed MathNet open, added `distinct`),
  `Json.fs`, `Set.fs` (removed MathNet open).
- `GenUNITS.Lib`, `GenCORE.Lib`, `GenFORM.Lib`, `ZForm.Lib`, `NKF.Lib`,
  `GenSOLVER.Lib` (`Utils.fs`, `Variable.fs`), `GenORDER.Lib`: the `open MathNet`
  migration (mostly 1-line each).
- `GenORDER.Lib/Logging.fs` (`isActive`), `Order.fs` + `OrderProcessor.fs`
  (logging guards), `GenSOLVER.Lib/Equation.fs` (dead-sort removal).
- `benchmark/Informedica.GenSolver.Lib_v1` left untouched (pre-existing, stale).

---

## 7. Recommendations

1. **Adopt RationalX** — net win, correctness-neutral, all tests green. Migrate
   per the script-only policy (human review of each `.fs`).
2. **[DONE] Proper lazy-logging API.** `Logger` gained an `Enabled: Level -> bool`
   field; `noOp` is `false`, `create`/real loggers `true`, `filterByLevel`/`combine`
   propagate it. New thunk-based `logLazy`/`logInfoLazy` build the message only
   when `Enabled level`. `Logging.isActive` is redefined as `Enabled Informative`,
   so the existing guards generalise beyond `noOp` to **any** logger filtered
   above informative; `logOrder` migrated to the thunk API. (8 `Logger` literal
   sites updated for the new field — 2 in `Logging.Lib`, AgentLogger, SolverLogging,
   OrderLogging, plus test/benchmark loggers.) Scenario solve holds at ~116 ms.
3. **[DONE] Unboxed `distinct`** applied to the `cmp` sites `ValueUnit.fs:845/847`
   (`toBaseValue |> BigRational.distinct |> Array.sort`). Sites `1320`/`1398` are
   `string[]` (not `BigRational[]`) so they are left on `Array.distinct`.
4. **Treat the solver's allocation profile as the next perf project** if
   end-to-end latency matters — scope an `Equation.loop` allocation-reduction
   refactor separately, with full solver-roundtrip validation.
