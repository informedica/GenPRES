# Domain-Constrained Option Solver — Architectural Design
*(DDD + functional modeling; harmonized with `Informedica.GenSolver.Lib` Types + Equation + Solver; exact arithmetic with BigRational)*


This architecture formalizes the process using **domain modeling with types**, constraint propagation, and option minimization strategies (as in the [GenSolver library](https://github.com/halcwb/GenPres2/tree/master/src/Informedica.GenSolver.Lib)).


## 1. Purpose

Users in a specific domain (e.g., clinical prescription) must pick valid options from a bounded set of variables.  
- **Rules** define what is allowed.  
- Rules translate into **constraints** over the option sets.  
- Constraints shape a **solution space**: the bounded context of valid scenarios.  
- Users explore this space by making picks; every pick narrows down the space.  
- When each variable has a single value left → a valid **solution/scenario** is reached.  
- The system **guarantees** validity: no illegal option is ever presented.  

We help users construct **valid scenarios** by picking options for a bounded set of **variables**.  
**Rules → constraints** prune the solution space. Each pick **narrows** options.  
A scenario is **solved** when every variable has a single feasible value.

Engine-wise, we use an **equation-based fixpoint solver** over ranged/discrete domains, with **monotone pruning** only:
- tightening **minimum/maximum**,
- **increasing increment** (coarsening),
- shrinking **discrete sets**.

If any variable’s feasible set becomes empty, the state is **illegal** (unsatisfiable).

---

## 2. Ubiquitous Language

- **Item / Variable**: a domain concept that must be assigned (e.g., Dose, Route, Frequency).  
- **Option**: a permissible value (should be exact and unambiguous) for an item (e.g., Route = IV | PO).  
- **Rule**: a human-readable domain law.  
- **Constraint**: a machine-checkable rule derived from domain laws.  
- **Solution Space**: the cartesian product of items’ options, pruned by constraints.  
- **Scenario / Solution**: a complete assignment of all items with one option each.  
- **Illegal State**: when an item has no options left (constraints in conflict).  
- **Bucket / Minimization**: grouping of many atomic options into manageable sets for UI/solver performance.  

### Ubiquitous Language (bound to GenSolver current types)

| Concept | Type (from `Types`) | Notes |
|---|---|---|
| Variable identity | `Name = Name of string` | Non-null, ≤1000 chars. |
| Numeric value w/ unit | `ValueUnit` (from `Informedica.GenUnits.Lib`) | Used inside ranges, increments, sets. |
| Lower/upper bound | `Minimum`, `Maximum` | Exclusive/inclusive via `MinIncl/MinExcl` and `MaxIncl/MaxExcl`. |
| Increment (grid) | `Increment = Increment of ValueUnit` | Controls discretization/coarsening. |
| Discrete set | `ValueSet = ValueSet of ValueUnit` | (Typically represents multiple values via collection semantics in `ValueRange.ValSet`.) |
| Domain | `ValueRange` | `Unrestricted | NonZeroPositive | Min | Max | MinMax | Incr | MinIncr | IncrMax | MinIncrMax | ValSet`. |
| Variable | `Variable = { Name; Values: ValueRange }` | A named domain. |
| Equation | `Equation = ProductEquation of Variable * Variable list | SumEquation of Variable * Variable list` | First `Variable` is the **dependent** (result), list are **independents**. |
| Property delta | `Property = MinProp | MaxProp | IncrProp | ValsProp` | What changed for a variable. |
| Constraint item | `Constraint = { Name; Property }` | A single variable/property assertion. |
| Solver step outcome | `SolveResult = Unchanged | Changed of List<Variable * Property Set> | Errored of Exceptions.Message list` | Fixpoint iteration uses these. |


---

## 3. Core Types

Example types to implement the ubiquitous language.

```fsharp
type ItemId = ItemId of string
type OptionId = OptionId of string

type Value =
  | Enum of string
  | Number of BigRational
  | Range of BigRational * BigRational
  | Code of string * string  // system, code

type ItemDef =
  { Id: ItemId
    Name: string
    All: Map<OptionId, Value> }

type SolutionState =
  { Remaining: Map<ItemId, Set<OptionId>>
    Chosen: Map<ItemId, OptionId> }

type Conflict =
  | EmptyDomain of item: ItemId * removedBy: string list
  | DirectContradiction of string
  | Other of string

type StepResult =
  | Ok of SolutionState
  | Illegal of Conflict
```

---


### 4. Constraints

Constraints prune the solution space.

```fsharp
/// Local constraint: prune options of one item.
type ItemConstraint =
  { Item: ItemId
    Keep: SolutionState -> OptionId -> bool }

/// Relational constraint: prune across multiple items.
type Relation =
  | ImpliesNotEq of ItemId * OptionId * ItemId * OptionId
  | RequiresRange of ItemId * (BigRational * BigRational) * ItemId

type Constraint =
  | Local of ItemConstraint
  | Rel of Relation
```

## 5. Process Flow

1.	Initialization
    - Start with full candidate sets (all options for all items).
2.	Propagation
    - Apply constraints to prune options.
    - Detect illegal states: if any item’s set becomes empty → report conflict.
3.	User Interaction
    -	Present remaining (valid) options.
    -	On pick → commit the choice, rerun propagation.
4.	Minimization
    -	If candidate sets are too large, apply incremental bucketing to reduce options while preserving validity.
5.	Convergence
    -	Repeat until each item has exactly one option → solved scenario.

---

## 6. Illegal State Handling

An illegal state arises when Remaining[item] = ∅.
-	Report as Illegal (EmptyDomain item, reasons).
-	Reasons are collected during propagation (e.g., “Dose 100 mg removed because Frequency=q12h”).
-	Guarantees early detection and explainability.

---

## 7. Minimization Strategy

Large option sets (e.g., 10,000 doses) are minimized for UX and solver efficiency.

Concepts
-	Bucket: a grouping of atomic options.
-	Policy: defines max buckets and increment ladder (e.g., 1 → 2 → 5 → 10 → 25 → 50 → 100).
-	Pinned Anchors: clinically meaningful values (e.g., 50 mg, 500 mg) always kept as distinct buckets.


```fsharp
type BucketId = BucketId of string
type Bucket =
  { Id: BucketId
    Members: Set<OptionId>
    Label: string }

type Minimization =
  { Buckets: Map<ItemId, Bucket list>
    Index: Map<ItemId, Map<OptionId, BucketId>> }

type MinPolicy =
  { maxBuckets: int
    nextIncrement: int -> int }

```

Workflow
1.	Build buckets for each item’s Remaining set.
2.	Escalate increment until #buckets ≤ maxBuckets.
3.	Replace Remaining with chosen bucket members on user pick.
4.	Rebuild buckets after each propagation.

### 7.1. Option explosion: “minimization” layer (à la increaseIncrement)

The trick is to separate:
-	the solver domain (potentially dense, precise),
-	the presentation domain (coarse, small, friendly).

We model a minimizer that coarsens an item’s candidate set according to a policy (caps, step escalation, clinically meaningful anchors), then keep a mapping back to the underlying atomic options so validity is preserved.

```fsharp
/// A UI bucket that represents many atomic options
type BucketId = BucketId of string

type Bucket =
  { Id: BucketId
    Members: Set<OptionId>        // atomic options represented by this bucket
    Label: string }               // human-readable ("50 mg", "50–75 mg", …)

type Minimization =
  { Buckets: Map<ItemId, Bucket list>
    // for quick reverse lookup: OptionId -> BucketId
    Index:   Map<ItemId, Map<OptionId, BucketId>> }

/// Policy to keep the UX manageable
type MinPolicy =
  { maxBuckets: int
    // escalate granularity when too many values remain
    nextIncrement: int -> int }   // e.g., 1→2→5→10→25→50→…

/// Build buckets on top of the current Remaining set
let buildBuckets (defs: Map<ItemId,ItemDef>) (policy: MinPolicy) (st: SolutionState) : Minimization =
  // For numeric-like items you'd parse & group by increment; for enums, group semantically.
  // Below is a sketch; domain-specific parsing omitted for brevity.
  let bucketItem (item: ItemId) (opts: Set<OptionId>) =
    let mutable inc = 1
    let mutable groups : Bucket list = []

    let rec regroup () =
      // group by increment 'inc' → build buckets
      let bs = 
        opts
        |> Seq.groupBy (fun oid -> (* key by inc; e.g., dose value / inc *) 0)
        |> Seq.map (fun (k, members) ->
            { Id = BucketId (sprintf "%A-%d" item k)
              Members = members |> Set.ofSeq
              Label = sprintf "%A (%d)" item (Seq.length members) })
        |> Seq.toList

      if bs.Length > policy.maxBuckets then
        inc <- policy.nextIncrement inc
        regroup()
      else bs

    groups <- regroup(); groups

  let buckets =
    st.Remaining
    |> Map.map bucketItem

  let index =
    buckets
    |> Map.map (fun _ bs ->
        bs |> List.collect (fun b -> b.Members |> Seq.map (fun m -> m, b.Id))
           |> Map.ofSeq)

  { Buckets = buckets; Index = index }
```

Key idea: the minimizer can keep pushing the increment (like your increaseIncrement) until the number of buckets ≤ policy.maxBuckets. You can also hard-pin important anchors (e.g., “50 mg”, min/max, formulary-preferred packs) so they always get their own bucket.


### 7.2. End to end loop

```fsharp
let step (defs, policy, constraints) (st: SolutionState) =
  // 1) propagate constraints and detect conflicts
  match propagate constraints st with
  | Illegal c -> Illegal c
  | Ok st1 ->
    // 2) (re)build buckets for friendly presentation
    let minz = buildBuckets defs policy st1
    // 3) return both the pruned state and the minimized view
    Ok st1, minz
```

---

## 8. Architectural Patterns
-	Make illegal states unrepresentable: use discriminated unions and validated types.
-	Pure functions: choose and propagate are pure transformations.
-	Monotonic narrowing: Remaining sets only shrink → guarantees convergence.
-	Explainability: keep trace of removals for transparency.
-	Bounded contexts: keep solver focused (e.g., medication order); integrate with other contexts via events.

### 8.1. Picking at bucket level (without losing rigor)

When the user clicks a bucket, you’re really saying “restrict Remaining for this item to Members of that bucket”—still fully valid.

```fsharp
let chooseBucket (minz: Minimization) (item: ItemId) (bucket: BucketId) (st: SolutionState) =
  let members =
    minz.Buckets.[item]
    |> List.find (fun b -> b.Id = bucket)
    |> fun b -> b.Members
  let rem' = st.Remaining.Add(item, members)
  { st with Remaining = rem' }
```

### 8.2. Guardrails to avoid computational blow-ups
-	Monotonicity: only ever remove candidates. This guarantees convergence and enables incremental propagation (delta-based pruning).
-	Caps everywhere:
-	maxBuckets per item for UX,
-	maxBranching for any search/backtracking fallbacks (if you use them),
-	early Illegal when a domain empties.
-	Explainability: keep a compact removal trace (constraint IDs + summaries) so the UI can show “why an option vanished.”


---


# Current Implementation

This document aligns the domain/UX view with the **current engine implementation**:

- `Types` (variables, value ranges, properties, constraints, equations, solve result)
- `Equation` (single-equation solving loop, formatting/DTO, helpers)
- `Solver` (equation-set fixpoint queue, replacement, logging)
- **Exact arithmetic requirement:** atomic numeric values are `BigRational` (via `ValueUnit`), so options like `1/3` are uniquely representable.


## 1. Core Types (as implemented)

- `Name = Name of string` — variable identity (non-empty, ≤1000 chars).
- `ValueUnit` — **BigRational** with units (from `Informedica.GenUnits.Lib`).
- Bounds: `Minimum = MinIncl|MinExcl of ValueUnit`, `Maximum = MaxIncl|MaxExcl of ValueUnit`.
- `Increment = Increment of ValueUnit`
- `ValueSet = ValueSet of ValueUnit` *(collection semantics when used in `ValSet`)*.
- Domain:  
  `ValueRange = Unrestricted | NonZeroPositive | Min | Max | MinMax | Incr | MinIncr | IncrMax | MinIncrMax | ValSet`
- `Variable = { Name: Name; Values: ValueRange }`
- Properties (delta semantics): `Property = MinProp | MaxProp | IncrProp | ValsProp`
- Equations:  
  `Equation = ProductEquation of Variable * Variable list | SumEquation of Variable * Variable list`  
  *(first var is dependent `y`, list is independents `xs`)*.
- Per-equation solve outcome:  
  `SolveResult = Unchanged | Changed of (Variable * Property Set) list | Errored of Exceptions.Message list`
- Domain-level constraint (non-equation):  
  `Constraint = { Name: Name; Property: Property }`

**Monotonic pruning invariant:** only **tighten** min/max, **increase** increment, **shrink** discrete sets. Never widen domains.

**Exactness requirement:** All atomic numbers inside `ValueUnit` use **BigRational**. This guarantees unique identity (e.g., `1/3`) and deterministic snapping to increments.

---

## 2. Domain → Engine Mapping

- Domain rules (e.g., pediatric `Route ≠ PO`, formulary “allowed strengths”) become **Constraints** (`{ Name; Property }`) applied to variables’ `ValueRange` before/after equation solving.
- Quantitative relations (e.g., `Dose = Weight * DosePerKg`, `Total = A + B + C`) are **Equations** (`ProductEquation` / `SumEquation`).

**User loop**
1. Initialize variables with broad `ValueRange`s.
2. Apply domain **Constraints** (e.g., remove `PO`, raise `Increment` to limit options).
3. Run **equation solving** to convergence (Sections 3–4).
4. Present remaining options (possibly minimized via `Increment`), user pick → turn pick into another **Constraint**.
5. Repeat until all variables are effectively singletons (**solved**) or a conflict yields `Errored`.

---

## 3. Single-Equation API & Behavior (`module Equation`)

### 3.1. Construction & classification

- `createProductEq` / `createSumEq` ensure **no duplicate variable names**; `create*Exc` variants raise on failure.
- `apply fp fs` dispatches per kind; `isProduct` / `isSum` test type.
- `toVars` returns `y :: xs`.

### 3.2. Heuristics & introspection

- `count onlyMinMax eq` — heuristic for queue prioritization: combines how many values remain, whether value sets appear, and whether only one variable is unsolved.
- `countProduct` — product of per-variable counts (used as an alternative complexity measure).
- `toString exact` / `toStringShort` — pretty printers (exact vs compact).
- `nonZeroOrNegative` — converts all variables in an equation to `NonZeroPositive`.
- `contains`, `equals`, `find`, `findName`, `replace` — structural helpers.
- `isSolved` — **all** variables solved (singleton domains).
- `isSolvable` — the equation *can* change something: at least one variable solvable and not “too many” unrestricted vars.
- `check` — for solvable/solved equations: ensures dependent’s values are a **subset** of combining independents (for `Sum`: `(^+)`, for `Product`: `(^*)`). Logs subset violations.

### 3.3. Calculation strategy

- Operators:
  - Rich operators for exact domain algebra: `^+`, `^-`, `^*`, `^/` (full `ValueRange` calculus).
  - “Min/Incr/Max only” operators: `@+`, `@-`, `@*`, `@/` (faster coarse pass).
- Reordering trick: the equation is rotated so we can, in turn, solve for each variable via inverse operations, e.g.
  ```
  a = b + c + d  ⇒
  b = a - (c + d)
  c = a - (b + d)
  d = a - (b + c)
  ```
- `calcVars` tries each rotation; **skips** a calc if:
  - the target var is already solved, or
  - it’s the last rotation and recent attempts were unchanged (optimization).
- If a re-calculation produces a **strictly smaller** domain (respecting monotonicity), we record a `Changed` delta with precise `Property` differences via `Variable.ValueRange.diffWith`.

### 3.4. Fixpoint for a single equation
- `solve onlyMinIncrMax log eq`:
  - Select operators `(@*,@/)` or `(^*,^/)` for product; `(@+,@-)` or `(^+,^- )` for sum.
  - Rotate variables; call tail-recursive `loop` until `calcVars` yields **None** (no more change).
  - If no change occurred: `Unchanged`.
  - If changed: rebuild the equation with updated variables and compute a **precise set of `Property` deltas** (the `Changed` payload).
  - On contradictions/empty domain/unit errors: `Errored`.

> **Note:** The `onlyMinIncrMax` mode is a **coarse, fast pass** that manipulates just Min/Incr/Max—useful as a first sweep before the full operators.

---

## 4. Equation-Set Solver (`module Solver`)

### 4.1. Queue management
- Input: list of `Equation`s (optionally “focused” with a particular variable via `solveVariable`).
- `sortQue onlyMinMax` uses `Equation.count` to prefer equations likely to produce progress first.
- `replace` propagates changed `Variable`s into other equations that contain them, splitting the set into:
  - `rpl`: equations affected by the changed variables (go back on the queue),
  - `rst`: unaffected equations (remain in the accumulator).

### 4.2. Main loop
```
loop:
  1) sort queue with sortQue
  2) pop head equation eq
  3) if eq not solvable → move to acc; continue
  4) else solve eq:
     a) Changed vars → propagate via replace into tail & acc; push affected back to queue
     b) Unchanged    → move eq to acc
     c) Errored      → stop with Error (collect all eqs + errors)
  5) termination:
     - queue empty → run Equation.check on acc; if ok → Ok acc; else → Error
     - guard against too many loops (MAX_LOOP_COUNT * |eqs|)
```

- `solveVariable` focuses on equations touching a given variable (by `Name`) before entering the loop; `solveAll` runs the full set.

### 4.3. Logging & DTO
- Extensive event logging around start/finish of solving and each calculation.
- `Solver.printEqs` pretty-prints equations (exact vs short).
- `Equation.Dto` supports round-tripping between runtime equations and serializable DTOs.

---

## 5. Minimization & UX (via `Increment`/`ValSet`)

To avoid option explosion (e.g., thousands of discrete candidates):

- **Increment escalation policy** (e.g., `1 → 2 → 5 → 10 → 25 → 50 …`): raise `Increment` via a `Constraint` or as part of the solving pass when cardinality exceeds a threshold; snap bounds to the new grid; emit `IncrProp`.
- **Pinned anchors**: keep clinically meaningful points (e.g., `50 mg`) by intersecting with a `ValSet` overlay and carrying them through escalation (`ValsProp`).
- Since all quantities are `BigRational`, snapping/bucketing is **exact** (no rounded-off ambiguity like `0.333…`).

---

## 6. Illegal States & Diagnostics

- An “empty domain” (no feasible values left) triggers `Errored [...]` at either **equation** level (during `Equation.solve`) or **equation-set** level (short-circuited by the main loop).  
- Because `Changed` carries **per-variable `Property Set`** deltas, UIs can render explainability such as:
  - “`Dose`: `Max` 600 → 300 mg (from `Dose = W * Dpk`), `Increment` 10 → 25 mg (minimization policy)”.

---

## 7. BigRational Requirement (Exact Arithmetic)

### 7.1. Motivation
- With floats/decimals, values like `1/3` cannot be represented exactly, so domain membership is ambiguous.
- With **BigRational**, each atomic value is a fraction of arbitrary-precision integers → **unique identity** and stable equality/ordering.

### 7.2. Implications
- Every `Minimum`, `Maximum`, `Increment`, `ValueSet` member is based on `BigRational` (via `ValueUnit`).
- Sum/Product equation algebra (`^+`, `^-`, `^*`, `^/`) uses rational arithmetic.
- Snap-to-increment and bound tightening always yield **exact** results.

### 7.3. Example

```fsharp
open MathNet.Numerics
let oneThird = BigRational.FromInt(1) / BigRational.FromInt(3)

let fractionVar =
  { Name = Name "Fraction"
    Values = ValSet (ValueSet oneThird<unit>) } // exact 1/3
```

---

## 8. Putting It Together (orchestration sketch)

1. **Seed** variables (`Variable`) with generous `ValueRange`s (often `MinIncrMax` for numerics, `ValSet` for enums).
2. **Apply Constraints** (domain rules) by updating the matching variable with `Property` (`MinProp | MaxProp | IncrProp | ValsProp`).
3. Build **Equations** (`createProductEqExc` / `createSumEqExc`) using those variables.
4. Call `Solver.solveAll onlyMinIncrMax log eqs`
   - Optionally run a quick coarse pass (`onlyMinIncrMax = true`) then a full pass (`false`).
5. If `Ok`, render options (already valid by construction). If too many, **raise `Increment`** and rerun.
6. On user pick, convert to a **Constraint** (often `ValsProp` to a singleton) and go back to step 4.
7. If `Error`, surface the messages; offer alternative picks or relax constraints.

---

## 9. Design Guarantees

- **Soundness**: only monotone prunings; no widening; exact arithmetic with `BigRational`.
- **Convergence**: each variable’s domain is on a finite descent chain (bounds tighten; increments ascend finite ladder; sets shrink) → fixpoint.
- **Explainability**: `Changed` contains precise `Property` deltas; logs/DTOs preserve the trail.
- **Separation of concerns**:
  - Domain constraints (business rules) → `Constraint`.
  - Quantitative relations → `Equation`.
  - Global coordination → `Solver` queue/replace loop.
  - UX manageability → `Increment` escalation / `ValSet` curation.

---

## 10. Minimal Code Example (tying parts)

```fsharp
// Variables
let weight    = { Name = Name "Weight";     Values = MinMax (MinIncl 10<kg>, MaxIncl 60<kg>) }
let dosePerKg = { Name = Name "DosePerKg";  Values = MinMax (MinIncl 5<mg/kg>, MaxIncl 10<mg/kg>) }
let dose      = { Name = Name "Dose";       Values = MinIncrMax (MinIncl 0<mg>, Increment 10<mg>, MaxIncl 1000<mg>) }

// Equation
let eqDose = Equation.createProductEqExc (dose, [weight; dosePerKg])

// Optional domain constraint (minimize option count)
let raiseInc = { Name = Name "Dose"; Property = IncrProp (Increment 25<mg>) }
// (Apply to variable 'dose' before solving.)

// Solve all
match Solver.solveAll false log [ eqDose ] with
| Ok eqs    -> // render options from resulting ValueRanges (exact BigRational values)
| Error(e,m)-> // surface errors & reasons
```

---

## 11. Contributor Checklist

- Use `ValueUnit` (BigRational) for any numeric value to avoid ambiguity (e.g., `1/3`).  
- Respect **monotonicity** when changing `ValueRange`.  
- For new operators or checks, ensure they work in both full (`^+`, `^*`, …) and coarse (`@+`, `@*`, …) modes.  
- Emit `Changed` with **specific `Property` deltas**; never signal change without a precise reason.  
- Keep the **Equation** API contract: dependent is first, independents follow; no duplicate `Name`s.  
- Keep **Solver** replacement semantics: whenever variables change, re-queue all equations that contain them.

---

# Areas Needing Clarification/Updates

1. Minimization Implementation vs. Documentation:
   - Document suggests: A sophisticated "bucket" system with escalating increment policies
   - Reality: The actual implementation uses increaseIncrement which is simpler - it tries increments from a provided list until cardinality ≤ maxCount
   - Gap: The document's BucketId, Minimization types, and buildBuckets function don't exist in the current codebase

2. Domain Constraint Architecture:
   - Document proposes: Rich ItemConstraint and Relation types for expressing domain rules
   - Reality: The implementation has a simpler Constraint = { Name; Property } structure
   - Missing: The sophisticated constraint propagation system described isn't implemented

3. Illegal State Handling:
   - Document suggests: Detailed conflict tracking with reasons
   - Reality: Errors are handled through Errored with Message list, but not the rich explainability described