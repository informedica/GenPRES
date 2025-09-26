# Order Value Constraint Logic Design

## OrderVariable design

An `OrderVariable` pairs a solver `Variable` with a set of `Constraints`.

- Variable: identified by `Name`, holding a `ValueRange` (not a single “value”). The `ValueRange` can be unrestricted, range-shaped (min/max), incremented (min/incr/max), or a discrete `ValueSet`.
- Constraints: optional bounds and enumerations that describe what’s allowed for the variable. In code this is a record of four optionals: `Min`, `Max`, `Incr`, and `Values`.

Applying constraints and/or evaluations leads to recognizable states. The names below align with the current implementation in `OrderVariable`:

- Unconstrained
  - Meaning: the constraints record is effectively empty (no `Min`, `Max`, `Incr`, or `Values`).
  - Code: `Constraints.isEmpty` returns true.
- NonZeroPositive
  - Meaning: that the variable has a lower bound (`Minimum`) of > 0. Note that in this special case the `Value` unit can be unitless (`ZeroUnit`) as calculations with zero are independent of the unit of the value.
- WithBounds
  - Meaning: constraints define a minimum and/or maximum (and possibly an increment), but no discrete value set has been materialized yet.
  - Code: `Constraints.toValueRange` produces a min/max (and optionally incr) range.

- MinIncrMax (enumerable)
  - Meaning: the variable’s `ValueRange` is in the min/incr/max form, which can be expanded into a discrete set of values.
  - Code: `Variable.isMinIncrMax` checks shape; `OrderVariable.minIncrMaxToValues` materializes into a `ValueSet` (optionally pruned).

- ValueSet (discrete)
  - Meaning: the variable (or constraints) contains a discrete set of candidate values.
  - Code: `Variable.hasValues` inspects the variable’s `ValueRange` for a non-empty `ValueSet`.

- HasValues
  - Meaning: there are multiple selectable values remaining (cardinality > 1).
  - Code: `OrderVariable.hasValues` delegates to `Variable.hasValues` with the >1 interpretation used by the order logic.

- Solved
  - Meaning: the system considers the variable “done”. This includes the single-value case, but also certain “default” shapes.
  - Code: `OrderVariable.isSolved` is true if the variable is a single value OR is unrestricted OR is non‑zero‑positive. This is a deliberate shortcut used broadly in the order math.

- Cleared
  - Meaning: values are cleared back to an unrestricted domain (not “empty set”).
  - Code: `OrderVariable.isCleared` checks `Variable.isUnrestricted`; `OrderVariable.clear` resets values to unrestricted.

- Empty (shorthand used in order logic)
  - Meaning: nothing concrete to pick yet; treated as “empty” for flow control.
  - Code: `OrderVariable.isEmpty` is true if the variable is unrestricted, non‑zero‑positive, or has min‑exclusive‑zero.

Notes on terminology adjustments:

- Replaced misspellings/ambiguous terms:
  - `UnConstraint` → `Unconstrained`
  - `NotContrstraint` → removed in favor of explicit notions above
  - `Constraint` → “constraints applied” is implicit via `applyConstraints` and `isWithinConstraints`
  - `CanHaveValues` → expressed as the `MinIncrMax` shape that can be materialized into a `ValueSet`
  - `Cleared` now explicitly means “unrestricted”, not “empty set of values”

Key operations (for reference):

- Apply constraints to the variable: `OrderVariable.applyConstraints`
- Check if current values obey constraints: `OrderVariable.isWithinConstraints`
- Materialize min/incr/max: `OrderVariable.minIncrMaxToValues`
- Inspect cardinality: `OrderVariable.hasValues` (multiple), `OrderVariable.isSolved` (single or default shapes)

## Order.hasValues: variables checked per prescription type

An `Order` consists of an `Orderable` and a `Prescription`. The `Prescription` determines the administration type. The `Orderable` has one or more `Components`; each `Component` has zero or more `Items`. The concentration of each `Item` is fixed; the concentration of a `Component` relative to other `Components` can be adjusted. An `Order` can only be changed by selecting a value from a `ValueSet` contained in an `OrderVariable`.

This note documents the logic for the selection process and the clearing of `OrderVariables`.

It reviews `Informedica.GenOrder.Lib.Order.hasValues` and maps which variables are checked per prescription type, including filters and caveats. The purpose of `hasValues` is to check whether there are any values left that can/should be selected so an `Order` is sufficiently entered.

## Checklist

- Inspect `Order.hasValues` implementation
- Identify variables checked per prescription kind
- Call out filters (constraints/name-based), scope limits (first component/item), and the “any vs all” logic

## Findings

### Meaning of “has values”

An `OrderVariable` “has values” when its underlying `Variable` has a `ValueSet` with multiple selectable values (`Variable.hasValues` → `ValueRange.cardinality > 1`).

- A single selectable value (cardinality = 1) is not considered “has values”.
- Range-only constraints (min/max) are not considered “values”; they don’t count toward `hasValues`.

### “Constraints” as used here

`OrderVariable.hasConstraints` returns true when the variable is neither unrestricted, nor non‑zero‑positive, nor min‑exclusive‑zero. In other words, variables with only these default/non-restrictive shapes are treated as having no effective constraints.

### Continuous prescriptions

Explicitly checked variables:

- Orderable dose rate: `ord.Orderable.Dose.Rate |> Rate.toOrdVar`
- First component’s first item dose rate (if present): first component → first item → `item.Dose.Rate |> Rate.toOrdVar`
- First component’s first item orderable concentration (if present): first component → first item → `item.OrderableConcentration |> Concentration.toOrdVar`

Scope/caveats:

- Only the first component and the first item are considered (`List.tryHead` twice); other components/items are ignored by this check.
- Uses `List.exists OrderVariable.hasValues` (true if any of the above has values).

Included variables (explicitly checked):

- `id.orb.dos.rte` (OrderableDoseRate)
- `id.n.g.itm.dos.rte` (ItemDoseRate) — only for the first component’s first item
- `id.n.g.itm.orb.cnc` (ItemOrderableConcentration) — only for the first component’s first item

Excluded in this check:

- All other variables; they are not considered by `hasValues` for Continuous prescriptions.

### Discontinuous prescriptions

Selection:

- All order variables from `ord |> toOrdVars`
- Filtered to those that have constraints: `List.filter OrderVariable.hasConstraints`
- Excludes variables whose name contains any of:
  - `_cmp_qty`
  - `_cmp_cnc`
  - `_orb_cnt`
  - `_orb_cnc`

Evaluation:

- `List.exists OrderVariable.hasValues` (true if any remaining variable has values).

Included (subject to `OrderVariable.hasConstraints`):

- Prescription-level: `id.pres.freq`, `id.pres.time` (noting equations refer to these as `pres.freq` and `pres.time`)
- Order-level: `id.ord.adj`, `id.ord.time`
- Orderable-level:
  - `id.orb.qty`, `id.orb.ord.qty`
  - `id.orb.dos.qty`, `id.orb.dos.tot`, `id.orb.dos.rte`
  - Adjust variants: `id.orb.dos.qty.adj`, `id.orb.dos.tot.adj`, `id.orb.dos.rte.adj`
- Component-level:
  - `id.n.cmp.ord.cnt`
  - `id.n.cmp.dos.qty`, `id.n.cmp.dos.tot`, `id.n.cmp.dos.rte`
  - Adjust variants: `id.n.cmp.dos.qty.adj`, `id.n.cmp.dos.tot.adj`, `id.n.cmp.dos.rte.adj`
- Item-level:
  - `id.n.g.itm.orb.qty`
  - `id.n.g.itm.dos.qty`, `id.n.g.itm.dos.tot`, `id.n.g.itm.dos.rte`
  - Adjust variants: `id.n.g.itm.dos.qty.adj`, `id.n.g.itm.dos.tot.adj`, `id.n.g.itm.dos.rte.adj`

Excluded by name filter (variables whose names contain these substrings are excluded):

- `_cmp_qty` (e.g., `id.n.g.itm.cmp.qty`, `id.n.cmp.qty`)
- `_cmp_cnc` (e.g., `id.n.g.itm.cmp.cnc`)
- `_orb_cnt` (e.g., `id.n.cmp.orb.cnt`)
- `_orb_cnc` (e.g., `id.n.g.itm.orb.cnc`, `id.n.cmp.orb.cnc`)

Notes:

- Unit-group conversion (`id.n.g.itm.cnv`) and sum variables are generally computed and may not surface as constrained `OrderVariable`s.

### Timed prescriptions

Same as Discontinuous:

- All `ord |> toOrdVars` with constraints
- Exclude by name substrings: `_cmp_qty`, `_cmp_cnc`, `_orb_cnt`, `_orb_cnc`
- Exists check for `hasValues`.

Included and excluded sets are the same as for Discontinuous prescriptions (subject to `OrderVariable.hasConstraints` and the same name-based exclusions).

### Once prescriptions

Same as Discontinuous/Timed:

- All `ord |> toOrdVars` with constraints
- Exclude by name substrings: `_cmp_qty`, `_cmp_cnc`, `_orb_cnt`, `_orb_cnc`
- Exists check for `hasValues`.

Included and excluded sets are the same as for Discontinuous prescriptions (subject to `OrderVariable.hasConstraints` and the same name-based exclusions).

### OnceTimed prescriptions

Same as Discontinuous/Timed/Once:

- All `ord |> toOrdVars` with constraints
- Exclude by name substrings: `_cmp_qty`, `_cmp_cnc`, `_orb_cnt`, `_orb_cnc`
- Exists check for `hasValues`.

Included and excluded sets are the same as for Discontinuous prescriptions (subject to `OrderVariable.hasConstraints` and the same name-based exclusions).

### Observations

- The function returns true if any relevant variable has multiple values to pick from (exists), not if all relevant variables do.
- For Continuous, the check is narrowly scoped to:
  - The drip rate of the orderable
  - The first item’s dose rate
  - The first item’s orderable concentration

- For the other prescription types, relevance is decided by constraints presence and by excluding names containing the specified substrings.

### Known limitations and TODOs

- Name-based exclusions are heuristic and currently marked with a TODO in code; they’re subject to change. The substrings roughly refer to:
  - `_cmp_qty`: component quantity
  - `_cmp_cnc`: component concentration
  - `_orb_cnt`: orderable content/count
  - `_orb_cnc`: orderable concentration
- For Continuous, only the first component and its first item are checked.
- `hasValues` only considers ValueSets with cardinality > 1; range-only constraints won’t trip this check, even if user input may still be required elsewhere in the flow.
- For the other prescription types, relevance is decided by constraints presence and by excluding names containing the specified substrings.
