# W2: Core Architecture Review — Status Analysis

## Workshop Status: In Progress ⏳

**Last Updated**: 2026-05-15

This document analyses progress toward the W2 workshop goals as defined in
the [GenPRES Architecture and Timeline](genpres-architecture-and-timeline.md)
and [ROADMAP](../../ROADMAP.md).

---

## W2 Goals (from ROADMAP)

| Goal | Status | Evidence |
|------|--------|----------|
| Domain model validation | 🔄 Partial | MDR docs, stability analysis, GenFORM 131+ test scenarios |
| Constraint solver optimisation | 🔄 Partial | PRs #166 #220 #230 #233 #238 #249 (prototype complete, production migration pending) |
| Unit of measure framework | ✅ Complete | `Informedica.GenUnits.Lib` — units-of-measure API with BigRational arithmetic |
| Performance benchmarking | ✅ Complete | PR #166 — solver throughput benchmarks with Stopwatch-based timing |

---

## Completed Work

### Constraint Solver Optimisation

The GenSOLVER constraint solver has received a series of targeted performance
and correctness improvements:

#### LRU Memoisation

- **[PR #220]** `LRUCache.fsx` — session-level LRU cache prototype; thread-safe
  O(1) get/put/evict, configurable capacity, 10-patient dosing benchmark.
- **[PR #230]** `LRUCacheProps.fsx` — FsCheck property-based tests validating
  sequential count/capacity, put/get, overwrite, clear, capacity-1, promotion,
  and eviction behaviour.
- **[PR #233]** `CanonKeyInvariant.fsx` — 8 unit tests + 6 FsCheck property
  tests for canonical key normalisation and round-trip symmetry.
- **[PR #238]** `LRUSolverIntegration.fsx` — `SessionSolver` integrating the
  LRU cache with canonical name-remapping; 6 correctness tests and a 50-patient
  capacity benchmark.
- **[Design rationale]** `docs/code-reviews/solver-memoization.md` documents the
  `Dictionary` + `LinkedList` implementation guarded by a single mutex for
  thread-safety, configurable eviction policy,
  and alternatives considered (request-scoped and persistent caching).

#### Cycle Detection & Stability

- **[PR #249]** `LoopDetect.fsx` — state-fingerprint-based cycle detection using
  FNV-1a hashing; `CycleDetector`, `ConvergenceTracker`, and `DetectingLoop.solve`
  returning typed `TerminationReason` (`HardLimit` / `CycleDetected` / `PotentialStall`);
  9 Expecto tests.
- **[Stability Analysis]** `docs/domain/gensolver-stability-analysis.md` maps
  the three solver instability problems identified by Đelić (2022) onto the
  current implementation and documents residual risk.

#### Domain Documentation (PR #349)

The GenSOLVER domain document
(`docs/domain/gensolver-from-orders-to-quantitative-solutions.md`) was updated with:
- **Section 7** expanded with a cycle-detection paragraph describing the
  state-fingerprint-based `CycleDetector` that terminates gracefully.
- **Section 9** gains a "Session-Level LRU Memoisation" subsection documenting
  the `LRUCache` prototype, canonical key remapping for cross-patient cache
  sharing, thread-safety design, and production status (still pending).
- Cross-references to the [memoisation design](../code-reviews/solver-memoization.md)
  and the [stability analysis](../domain/gensolver-stability-analysis.md).

### Unit of Measure Framework

The `Informedica.GenUnits.Lib` provides the unit-of-measure foundation:

- F# units of measure (`int<gram>`, `int<cm>`, `float<bsa>`, etc.) for
  compile-time type safety with zero runtime overhead.
- `ValueUnit` type combining a `BigRational` value array with a `Unit` for
  exact arithmetic in all dosing calculations.
- Shared calculation modules (`BSA`, `Age`, `Renal`) ported to the
  Fable-compatible Shared library (ADR-0019) for client-side use.

### Performance Benchmarking (PR #166)

Stopwatch-based solver throughput benchmarks (`timeMean` helper) measuring
elapsed time for representative clinical scenarios:
- Solver execution time for several representative clinical scenarios.

---

## Remaining / In Progress

### Domain Model Validation

Complete end-to-end domain model validation — verifying that the clinical
domain model (Patient → Prescription → Order → Dose) is internally consistent
and clinically accurate — is ongoing:

- ✅ GenFORM 131+ patient scenario tests cover the prescription-rule pipeline.
- ✅ GenORDER scenario tests (Scenarios.fs) cover order pipeline end-to-end.
- ✅ MDR risk analysis documents hazard analysis and risk controls.
- ⏳ Formal clinical expert review of scenario results (W4 scope).
- ⏳ Traceability matrix linking requirements → test cases (W3 scope).

### LRU Production Integration

The LRU memoisation is a **prototype** pending production integration.
Tasks remaining:

- [ ] Migrate `LRUSolverIntegration.fsx` logic into `OrderProcessor.fs` /
      `Informedica.GenORDER.Lib`.
- [ ] Add integration tests for cache correctness under concurrent patient
      sessions.
- [ ] Profile cache hit-rate in a representative clinical workload.

### Solver Stability — Residual Risk

Per `gensolver-stability-analysis.md`, the cycle-detection prototype in
`LoopDetect.fsx` has not yet been integrated into the production solver path:

- [ ] Migrate `LoopDetect.fsx` / `DetectingLoop.solve` into
      `Informedica.GenSolver.Lib`.
- [ ] Replace the `MAX_CALC_COUNT` hard cap with the typed `TerminationReason`
      mechanism.
- [ ] Add regression tests for the previously identified instability scenarios.

---

## References

| Resource | Path / Link |
|----------|-------------|
| LRU memoisation design | `docs/code-reviews/solver-memoization.md` |
| GenSOLVER domain document | `docs/domain/gensolver-from-orders-to-quantitative-solutions.md` |
| GenSOLVER stability analysis | `docs/domain/gensolver-stability-analysis.md` |
| Architecture and Timeline | `docs/roadmap/genpres-architecture-and-timeline.md` |
| ROADMAP | `ROADMAP.md` |

---

*This document was generated by Repo Assist and reflects the state of the
`master` branch as of 2026-05-15. It is intended as a progress aide for the
human maintainer and should be reviewed before being considered definitive.*
