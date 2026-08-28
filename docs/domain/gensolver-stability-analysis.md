# GenSolver Stability Analysis: Cycle Risks and Current Safeguards

## Purpose

This document summarizes the findings of the master thesis *"Optimizing GenSolver, a module for solving equations in a medical environment"* by Kerim Đelić (Utrecht University, June 2022) and analyzes which of the thesis-identified problems remain relevant in the current GenSolver implementation.

It serves as a reference for anyone maintaining or reasoning about the stability properties of the constraint solver.

## Related Documents

- [GenSOLVER: from Order Scenarios to Computed Quantitative Solutions](gensolver-from-orders-to-quantitative-solutions.md) — formal specification of the solver
- [GenORDER: Operational Rules to Orders](genorder-operational-rules-to-orders.md) — equation system construction
- [Core Domain Model](core-domain.md) — overall transformation pipeline

## Thesis Findings Overview

Đelić's thesis identified three problems in the 2021 version of GenSolver:

1. **Efficiency**: O(n²) operations on large finite sets of rational numbers caused gentamicin calculations to take ~55 seconds with 19 GB of allocated memory.
2. **Incorrect increment arithmetic**: Multiplying two domains with increments used a flawed formula that produced values not actually reachable by element-wise multiplication.
3. **Stability**: Removing increments to fix (2) exposed infinite loops caused by **cycles** in constraint propagation.

## Problem 1: Incorrect Increment Arithmetic — FIXED

### Original Problem

The thesis Example 4.2.1 showed that `[2..{2}..10] · [6..{3}..12]` under the increment-based formula would incorrectly include values like 30, but 30 is not reachable by element-wise multiplication of `{2,4,6,8,10} × {6,9,12}`.

### Current Implementation

The fix lives in `src/Informedica.GenSOLVER.Lib/Variable.fs`:

- **`ValueRange.calc`** is the entry point for domain arithmetic.
- When both operands are `ValSet`, the code performs **element-wise** multiplication via `ValueSet.calc`, which applies the operator directly to the underlying `ValueUnit` arrays.
- The doc-comment on `ValueSet.minIncrMaxToValueSet` in `Variable.fs` explicitly documents the thesis counterexample (`[3;6] * [2;4;6]` not producing 30).

No assumption is made that the result of arithmetic on two increment-based domains fits an increment pattern. The bug is eliminated.

## Problem 2: Stability via Cycles — NOT FIXED AS PROPOSED

### Original Problem (Thesis Chapter 4.3)

When increments are removed, constraint propagation can enter infinite loops. The thesis formalizes this as two cycle types:

**Homogeneous chain reaction** (Definition 4.3.1): A sequence of changes all focused on the same extremum type (all min or all max) that returns to the starting variable, e.g. `x₁^max → x₂^max → ... → x₁^max`. Corresponds to a recursive function `x^max(n) = α · x^max(n-1) + β` which converges to `L = β/(1-α)` but never reaches it when `0 < α < 1`.

**Heterogeneous chain reaction** (Definition 4.3.2): A chain containing "flips" (min→max transitions). Produces a repeating continued fraction that may converge to an irrational number — unacceptable in a medical context where only rational doses are valid.

### Thesis Proposed Solution (Chapter 5)

Track `α` and `β` parameters per variable extremum during propagation. When a cycle is detected (same variable extremum changed twice in one chain), compute the fixed point `L = β/(1-α)` directly, avoiding infinite iteration.

### Current State in Codebase

The proposed solution is **not implemented**. In `src/Informedica.GenSOLVER.Lib/Solver.fs`, the `solve` function uses a single safeguard: a loop-count ceiling expressed as

```fsharp
if n > (que @ acc |> List.length) * Constants.MAX_LOOP_COUNT then
```

where `Constants.MAX_LOOP_COUNT = 20` is defined in `Utils.fs`. The real ceiling is therefore **(total equation count) × 20** iterations before `SolverTooManyLoops` fires. There is no α/β tracking, no cycle graph, no fixed-point computation.

### Why Production Still Works

Analysis of production logs (e.g. `genpres_OrderContext_2026_04_12_19_06_06_fa91.log`, `genpres_OrderContext_2026_04_12_19_33_08_ae18.log`) shows solver convergence without loop-limit errors. Three configuration-level safeguards prevent the thesis's cycle scenarios from manifesting:

1. **Increments on all quantity variables** (e.g. `0.1 mL`, `0.01 g`) force discrete steps. A cycle that would asymptotically approach `L = β/(1-α)` instead terminates when the next step snaps to the same increment multiple.
2. **`Quantities:` pick-lists + `pickNearestHigherElseLower`** collapse the orderable quantity (`[orb]_orb_qty`) to a single pre-defined bag size early in solving. This breaks the ring structure by turning `orb_orb_qty` into a constant.
3. **Fixed `sch_frq`** (e.g. 1 x/day) removes an entire propagation dimension. Cycles involving frequency cannot form if frequency is pinned.

## Vulnerable Equation Structures

Even though cycles are not observed in production, the structural preconditions remain present in the equation system. The most vulnerable patterns, ordered by risk:

### 1. Sum + Concentration Ring (Highest Risk)

When the orderable has multiple components:

```text
[cmp_1]_orb_qty = [cmp_1]_orb_cnc * [orb]_orb_qty      (product)
[cmp_2]_orb_qty = [cmp_2]_orb_cnc * [orb]_orb_qty      (product)
[orb]_orb_qty   = [cmp_1]_orb_qty + [cmp_2]_orb_qty    (sum)
```

Concentrations in `[0,1]` with implicit `Σ cnc_i = 1` provide the α < 1 condition. The sum equation creates feedback: a change in one `cmp_orb_qty` flows via sum to `orb_orb_qty`, then back via the products to all `cmp_orb_qty` values.

This ring is **active in every multi-component order** (gentamicine + gluc 5% diluent, TPN mixtures, etc.) and is broken only by the `pickNearestHigherElseLower` early collapse of `orb_orb_qty`.

### 2. Concentration Triangle (High Risk)

For each quantity `X ∈ {orb_qty, dos_qty, dos_ptm, dos_qty_adj, dos_ptm_adj}`:

```text
[itm]_X     = [itm]_cmp_cnc * [cmp]_X
[itm]_X     = [itm]_orb_cnc * [orb]_X
[cmp]_X     = [cmp]_orb_cnc * [orb]_X
```

Three equations form a closed triangle through the three-level hierarchy (item/component/orderable). When concentrations are bounded ranges (e.g. gentamicine 10–40 mg/mL), changes in `[itm]_X` propagate via eq1 and eq2 independently, then couple back through eq3, potentially shrinking by a factor < 1 per round-trip.

### 3. Dose–Rate Rectangle (No Current Risk)

```text
[X]_dos_qty     = [X]_dos_qty_adj  * [ord]_adj_qty
[X]_dos_ptm     = [X]_dos_qty      * [ord]_sch_frq
[X]_dos_ptm     = [X]_dos_ptm_adj  * [ord]_adj_qty
[X]_dos_ptm_adj = [X]_dos_qty_adj  * [ord]_sch_frq
```

A DAG in practice because `adj_qty` and `sch_frq` are constants. Would become cyclic only if `sch_frq` were given as a range (multiple frequencies allowed) — which is currently not standard.

## Empirical Verification

Two production logs were analyzed within the `calc-minmax` pipeline phase (delimited by `=== PIPELINE START calc-minmax: calc-minmax ===` and `=== PIPELINE END calc-minmax: calc-minmax ===`). The log files are timestamped and not committed to the repository (log files are gitignored). To regenerate equivalent logs, run the server with debug logging enabled and reproduce the scenarios below. See the [Environment Configuration](../../DEVELOPMENT.md#environment-configuration) section in `DEVELOPMENT.md` for how to set `GENPRES_LOG=d` via `.env` or a `debugprod.sh` helper script; logs are written to `data/logs/`.

### Log 1: Gentamicin, single component, concentration 1 mg/mL

- 3 solver passes, all converged
- Maximum 3 changes per variable within calc-minmax
- All changes **monotonic** (min only increases, max only decreases)
- No repeated change to the same variable's extremum in a chain
- Tightening driven by three separate propagation waves, not cycles

### Log 2: Gentamicin + gluc 5% diluent, concentrations 10–40 mg/mL, bag sizes {5;10;50;100 mL}

- 120 Changed events within calc-minmax
- Maximum 3 changes per variable
- All changes **monotonic**
- `pickNearestHigherElseLower` collapsed `ord.gentamicine orb_qty` to 50 mL (line 2388 of log), breaking the sum ring
- Concentration ranges tightened progressively through three distinct pipeline stages

**Conclusion**: no homogeneous cycle manifested in either scenario. All variable extrema either changed at most three times (from different upstream sources) or converged to a single value within one solve pass.

## Risk Boundary

The cycle problem becomes relevant if **any one** of the following changes:

1. `Quantities:` pre-populated list becomes empty for a multi-component order (no bag size to pick → sum ring active)
2. `sch_frq` is given as a range instead of a fixed value (adds a new cycle dimension)
3. An increment is missing on `orb_qty` or related quantity variables (removes the discretization safeguard)
4. A new equation is added that closes an existing DAG into a ring

At any such boundary, the Chapter 5 α/β tracking proposal (or an equivalent cycle detector) becomes necessary.

## References

- Đelić, K. (2022). *Optimizing GenSolver, a module for solving equations in a medical environment*. Master thesis, Utrecht University. Supervisors: Prof. R.H. Bisseling, Dr. C.W. Bollen.
- `src/Informedica.GenSOLVER.Lib/Variable.fs` — domain arithmetic (`ValueRange.calc`, `ValueSet.calc`)
- `src/Informedica.GenSOLVER.Lib/Solver.fs` — propagation loop (`solve`)
- `src/Informedica.GenSOLVER.Lib/Utils.fs` — `Constants.MAX_LOOP_COUNT`
- `src/Informedica.GenSOLVER.Lib/Equation.fs` — equation-level solving
