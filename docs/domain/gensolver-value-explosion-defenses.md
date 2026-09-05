# Preventing Value Explosion in the Constraint Solver

> Formerly ADR-0014 (accepted 2026-04-07). Moved out of `docs/adr/` under issue #411 because it explains a solver algorithm and its pipeline placement, not a hard-to-reverse decision; see [ADR-0000](../adr/0000-documentation-rules.md).

**Date**: 2026-04-07 (updated 2026-04-08)

**Status**: Accepted

**References**:

- [Core Domain Model](core-domain.md)
- [GenSOLVER: Order Scenarios to Quantitative Solutions](gensolver-from-orders-to-quantitative-solutions.md)
- [GenORDER: Operational Rules to Orders](genorder-operational-rules-to-orders.md)

## Summary

This document describes a layered strategy to prevent value explosion (combinatorial overflow) in the GenSOLVER constraint solver. Value explosion occurs when the solver computes cartesian products of large discrete value sets, producing more values than the system can handle. The strategy comprises two complementary defenses:

1. **GenORDER layer — Staged value expansion** (pipeline-level): For OnceTimed and Timed orders, expand dose quantity variables before rate variables so that the `dos_qty = dos_rte * sch_tme` three-way equation is already constrained before rate expansion.
2. **GenSOLVER layer — ValueRange cartesian product cap** (solver-level): When two `ValSet` value ranges are multiplied and their combined size would exceed `MAX_CALC_COUNT` (500), fall back to computing only min/max bounds instead of the full cartesian product.

## Overall Strategy

Value explosion can occur at two levels of the system:

```text
┌─────────────────────────────────────────────────────────────────────┐
│  GenORDER Pipeline                                                  │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │ Defense 1: Staged Value Expansion                             │  │
│  │ • Controls WHICH variables are expanded and WHEN              │  │
│  │ • Prevents known explosion patterns at the pipeline level     │  │
│  │ • Applies to OnceTimed and Timed orders                       │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                              ↓                                      │
│  GenSOLVER Constraint Solver                                        │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │ Defense 2: ValueRange Cartesian Product Cap                   │  │
│  │ • Caps ANY ValSet × ValSet multiplication at MAX_CALC_COUNT   │  │
│  │ • Falls back to min/max bounds when product too large         │  │
│  │ • Catches unforeseen explosion patterns the pipeline missed   │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                              ↓                                      │
│  Existing safeguards                                                │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │ Defense 3: Increment calcOpt cap (c1 * c2 > 10 → None)       │  │
│  │ Defense 4: ValueSet.create hard limit (> MAX_CALC_COUNT²)     │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

**Defense 1** is a domain-aware optimization: it knows which variable orderings avoid explosion for specific order types. **Defense 2** is a domain-independent safety net: it protects against any cartesian product overflow regardless of the source. Together they provide defense-in-depth.

### Existing Safeguard (Pre-ADR)

Before this ADR, one safeguard already existed:

| Safeguard | Location | Threshold | Limitation |
|---|---|---|---|
| `ValueSet.create` hard limit | `Variable.fs` | `> MAX_CALC_COUNT²` (250,000) → raise `ValueSetOverflow` | Fires **after** the cartesian product is computed; raises an error that terminates the solve |

The `ValueSet.create` limit is a last-resort error, not a graceful fallback. When it fires, the entire solve fails and the order cannot be calculated.

### New Safeguards (This ADR)

This ADR introduces three new defenses (in addition to the existing hard limit):

| Safeguard | Location | Threshold | Behavior |
|---|---|---|---|
| Staged value expansion | `Order.fs`, `OrderProcessor.fs` | N/A (structural) | Expands dose qty before rate for timed orders |
| `ValueRange.calc` cartesian product cap | `Variable.fs` | `c1 * c2 > MAX_CALC_COUNT` (500) | Falls back to min/max bounds |
| `Increment.calcOpt` cap | `Variable.fs` | `c1 * c2 > 10` → return `None` | Prevents increment explosion |

---

## Part 1: Staged Value Expansion (GenORDER Pipeline)

### Context

#### The Problem

When processing a OnceTimed order (e.g., IV kaliumchloride with schedule time 60-120 min for a 31 kg patient), the existing `minIncrMaxToValues` function expands all variables (component quantities, dose quantity, dose rate) from min/incr/max ranges to discrete value sets in a single pass.

For OnceTimed and Timed orders, the equation `dos_qty = dos_rte * sch_tme` creates a three-way relationship between dose quantity, dose rate, and schedule time. When the solver expands rate values, each rate value interacts with each possible time value to produce dose quantity combinations. Across multiple substances (e.g., kaliumchloride concentrate with kalium, chloor, kaliumchloride substances plus glucose 5% diluent with glucose, energie, koolhydraat substances), the cross-product explodes.

**Observed failure**: 746,839 values generated, exceeding the max calc count threshold (500 x 500 = 250,000). The order could not be calculated.

#### Affected Order Types

The relationship `dos_qty = dos_rte * sch_tme` exists in:

- **OnceTimed**: Single administration over a time period (e.g., IV infusion given once over 60-120 min)
- **Timed**: Repeated administrations each over a time period (e.g., IV infusion given 4x/day, each over 15-20 min)

The following order types are NOT affected:

- **Once**: No rate or time variables
- **Discontinuous**: Has frequency but no time/rate interaction
- **Continuous**: Rate IS the primary dose variable; dose quantity is not independently expanded

#### Why the Explosion Occurs

Given the kaliumchloride scenario after CalcMinMax + increaseIncrements:

| Variable | Range | Approx. Count |
|---|---|---|
| `sch_tme` | 60 min..120 min | Many (continuous range with fine increments) |
| `dos_qty` (orderable) | 160 mL..0.5 mL..236 mL | ~153 values |
| `dos_rte` (orderable) | 80 mL/uur..20 mL/uur..220 mL/uur | ~8 values |
| Component qty | 14 mL..0.5 mL..17 mL | ~7 values |

When `minIncrMaxToValues` expands component qty (7 values) then solves, rate variables acquire derived values from the time range. When rate is subsequently expanded, each rate x time combination propagates through all substance variables. The total product across all equation variables exceeds the 250,000 limit.

### Decision

#### Two-Phase Staged Expansion

Modify the order processing pipeline to expand variables in two phases for OnceTimed and Timed orders:

**Phase 1 — Expand dose quantities only (skip rate)**

The `minIncrMaxToValues` function receives a `skipRate` flag. When `skipRate = true`, the orderable dose rate variable is excluded from the expansion loop. Only component orderable quantities and the orderable dose quantity are expanded to discrete values. The rate remains as a min/incr/max range.

This is safe because the constraint solver can still propagate min/max bounds through rate equations — it just does not enumerate all discrete rate values yet.

**Phase 2 — Expand rate after quantity is constrained**

After Phase 1, one of two things happens:

1. **CalcMinMax path**: `setMedianDoseValue` pins the dose quantity to a single median value. Then `solveNormDose` propagates this through the equations, naturally constraining the rate range.
2. **CalcValues path**: The system runs Phase 1 (quantity expansion), then immediately runs Phase 2 (rate expansion with `skipRate = false`). Because dose quantities are already discrete values from Phase 1, the rate x time cross-product produces far fewer values.

In both cases, the dose quantity is constrained before rate expansion occurs, preventing the combinatorial explosion.

#### Condition for Staged Expansion

Staged expansion (`skipRate = true`) applies when:

```text
Schedule.hasTime = true AND Schedule.isContinuous = false
```

This matches exactly OnceTimed and Timed orders. For all other order types, `skipRate = false` (no change in behavior).

#### Changes to Pipeline Steps

##### CalcMinMax Pipeline

The `ensure-dose-values-1` step passes `skipRate = true` for timed orders. The subsequent `set-normdose` step (which calls `solveNormDose` with min/max solving, not all-values solving) works correctly with rate still as min/incr/max.

##### CalcValues Pipeline

Split into two sequential steps for timed orders:

1. `calc-qty-values`: Expands quantities only (`skipRate = true`)
2. `calc-rate-values`: Expands rate too (`skipRate = false`)

For non-timed orders, a single step with `skipRate = false` preserves existing behavior.

##### SolveOrder Pipeline

No change needed. By the time SolveOrder runs, the user has already selected a dose quantity value, so rate expansion is naturally constrained.

##### ReCalcValues Pipeline

Same treatment as CalcValues — two-phase expansion for timed orders.

### Consequences

#### Positive

- **Eliminates value overflow**: The kaliumchloride OnceTimed scenario now completes successfully instead of failing with 746,839 values
- **No impact on other order types**: Once, Discontinuous, and Continuous orders are unaffected
- **Preserves constraint guarantees**: All GenSOLVER properties (soundness, completeness, monotonic convergence) are maintained — the change only affects the order of variable expansion, not the constraint logic
- **Minimal code change**: A single boolean parameter (`skipRate`) in `minIncrMaxToValues` plus pipeline step adjustments

#### Negative

- **Pre-existing `set-normdose` error remains**: For the kaliumchloride scenario, the `setMedianDoseValue` step encounters a conflict where the computed median (14 mL) falls outside the component's value set (14.5-17 mL). This is a pre-existing issue unrelated to the staged expansion and should be addressed separately
- **Slightly more complex pipeline**: The CalcValues and ReCalcValues commands now have conditional two-step logic for timed orders

#### Risks

- **CanSetNormDose guard interaction**: With `skipRate = true`, the Rate variable will not have discrete values, so `CanSetNormDose` returns `false`. This causes the `ensure-dose-values-1` guard (`_.CanSetNormDose >> not`) to trigger correctly. If this guard logic changes in the future, the staged expansion behavior must be re-validated
- **Continuous orders must NOT use skipRate**: The condition `Schedule.isContinuous |> not` is critical because for Continuous orders, rate IS the primary dose variable. If this condition were accidentally removed, Continuous orders would fail

## Part 2: ValueRange Cartesian Product Cap (GenSOLVER)

### Context

#### The Problem

Even with staged value expansion (Part 1), value explosion can still occur inside the GenSOLVER constraint solver itself. When the solver operates in full value mode (`onlyMinIncrMax=false`), the `ValueRange.calc` function computes the cartesian product of two `ValSet` value ranges. If both sets are large — which happens when variables accumulate many distinct BigRational fractions through multiple propagation rounds — the product exceeds system limits.

#### Observed Failure

A kaliumchloride continuous IV order for a 24 kg patient failed with `ValueSetOverflow 355037` during the second solve pass (`onlyMinIncrMax=false`). The error occurred in the equation:

```text
[itm]_dos_rte = [itm]_orb_cnc * [orb]_dos_rte
```

Where:

- `[kaliumchloride.kaliumchloride.kaliumchloride]_dos_rte` had ~597 distinct BigRational values (mmol/hr)
- `[kaliumchloride.kaliumchloride.kaliumchloride]_orb_cnc` had ~595 distinct BigRational values (mmol/mL)
- Their cartesian product: ~355,000 unique values after `Array.distinct`, exceeding the `MAX_CALC_COUNT²` (250,000) hard limit

#### How Values Accumulate

The value accumulation happens through multiple propagation rounds:

1. `_orb_cnc` starts as a range `[0.07 mmol/mL .. 0.08 mmol/mL]` with fine increment
2. When expanded to a `ValSet`, this becomes ~90 distinct BigRational values
3. `_dos_rte` starts with 6 values `[80;100;120;140;160;180]` mL/hr
4. The equation `_dos_rte = _orb_cnc * [orb]_dos_rte` produces ~540 values
5. But `_dos_rte` also participates in other equations (e.g., `= _dos_rte_adj * adj_qty`), which feed back more fractional values
6. After multiple propagation rounds, both variables have ~600 distinct fractions like `2080/361N`, `340/59N`, `26/361N`, `17/236N`, etc.
7. When these are multiplied together again: ~600 × ~600 = ~355,000 → overflow

#### Where the Existing Cap Falls Short

The existing `Increment.calcOpt` cap (`Variable.fs`) prevents increment explosion with a threshold of `c1 * c2 > 10`. However, this only applies to **increment** calculations, not to `ValSet × ValSet` multiplications.

The `ValueSet.create` hard limit (`Variable.fs`) catches values exceeding `MAX_CALC_COUNT²` (250,000), but it fires **after** the full cartesian product has been computed by `BigRational.calcCartesian`. At that point:

- The expensive `n × m` array allocation and computation has already happened
- The error raises an exception that terminates the entire solve
- All solve progress is lost

### Decision

#### Add Pre-Check to ValueRange.calc

Add a size guard in `ValueRange.calc` that checks the combined size of two `ValSet` operands
**before** computing the cartesian product. When `c1 * c2` exceeds `MAX_CALC_COUNT` (500), fall
back to computing only min/max bounds instead of the full cartesian product — the same fallback
the function already takes under `onlyMinIncrMax`.

Precision is deliberately traded for termination: the result is a bound rather than an enumerated
set, which the solver can still propagate. The alternative — letting the product build and then
failing on `ValueSetOverflow` — turns a solvable order into an error the clinician cannot act on.

### Consequences

#### Positive

- **Eliminates the `ValueSetOverflow` error**: The kaliumchloride continuous order now solves without errors (0 errors in log, down from 355,037-value overflow)
- **Prevents cascading failures**: The overflow previously caused `VariableCannotCalcVariables` and `ValueRangeEmptyValueSet` cascade errors — all eliminated
- **Domain-independent**: Protects against value explosion from any equation, not just known patterns
- **Graceful degradation**: Instead of raising an error, falls back to min/max bounds which still provide valid constraints
- **No impact on normal solving**: Products ≤ 500 proceed normally; only large products fall back

#### Negative

- **Less precise results for large products**: When the cap triggers, the result is a min/max range instead of an enumerated value set. This is less precise but still sound (all valid solutions remain within the range)

#### Risks

- **Threshold sensitivity**: If `MAX_CALC_COUNT` (500) is too aggressive for some future order types, valid value combinations might not be enumerated. However, since the min/max fallback preserves correctness, this only affects precision, not safety

### Verification

The fix was validated by:

1. **Building**: `dotnet build src/Informedica.GenSOLVER.Lib/` — 0 warnings, 0 errors
2. **GenSOLVER unit tests**: 4,748 tests passed (no regressions)
3. **GenORDER integration tests**: 39 tests passed (no regressions)
4. **Live order test**: Kaliumchloride continuous IV order for 24 kg patient solved successfully — 994 equations solved, 0 errors in log (previously failed with `ValueSetOverflow 355037`)

## Summary of All Value Explosion Defenses

| Defense | Layer | Location | Threshold | Behavior |
|---|---|---|---|---|
| Staged value expansion | GenORDER pipeline | `Order.fs`, `OrderProcessor.fs` | N/A (structural) | Expands dose qty before rate for timed orders |
| ValueRange cartesian product cap | GenSOLVER solver | `Variable.fs`, `ValueRange.calc` | `c1 * c2 > MAX_CALC_COUNT` (500) | Falls back to min/max bounds |
| Increment calcOpt cap | GenSOLVER solver | `Variable.fs`, `Increment.calcOpt` | `c1 * c2 > 10` | Returns `None` (no increment) |
| ValueSet.create hard limit | GenSOLVER solver | `Variable.fs`, `ValueSet.create` | `> MAX_CALC_COUNT²` (250,000) | Raises `ValueSetOverflow` error (last resort) |
