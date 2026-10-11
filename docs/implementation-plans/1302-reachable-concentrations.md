# Implementation plan for issue #1302

The dose dialog offers only values the order can reach. Drafted 2026-10-11. Line numbers are
from `master` at `33f05274`. The fixes and their tests are prototyped in
`src/Informedica.GenORDER.Lib/Scripts/Issue1302.fsx`.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [What the code does today](#what-the-code-does-today)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Left out](#left-out)
- [Verification](#verification)

## Problem description

Amoxicilline/clavulaanzuur IV in NaCl 0,9% for a child of 14.5 kg, 3 x/day. The dialog offers
37 amoxicilline concentrations. Picking 900/47 mg/mL fails in the final solve: 450 mg of
amoxicilline is 9 mL of powder, so a total of 23.5 mL needs 14.5 mL of NaCl, and the NaCl
volume set is `10;10.5;12;13.5;15;16.5;18;19.5;20 mL`. The server answers with the order as it
was, without a message. Found in `data/logs/genpres_order_2026_10_11_01_34_05_e7e9.log`.

Four defects, the first three around the count cap, the fourth in the equations:

1. **The increment harmonization under-restricts.** `Orderable.increaseQuantityIncrement`
   raises each component's increment until the component fits the cap, then applies the
   *smallest* of those increments to every component and the total. The powder spans 8.7 to
   10.6 mL and fits in 0.5 mL steps; NaCl spans 9.2 to 20.7 mL and needs 5 mL. The smallest
   wins: NaCl ends at `10..0.5..20 mL`, 21 values for a cap of 10.
2. **`ValueSet.prune` compares in the wrong unit.** When the 21 values meet the cap of 20 in
   `Order.minIncrMaxToValues`, `prune` keeps the values that are a multiple of m times the
   increment. It reads the increment's base value (1/2000 L) and the values in mL, so the
   multiples of 1 mL pass as 21 values and the first grid that fits is 1.5 mL, with 10.5 kept
   because 10.5 is a multiple of 1/2000 times 3.
3. **`prune` keeps an off-grid minimum and maximum.** Amfo's glucose volume keeps 3.5 and
   339.5 mL on a coarser grid. Once the other variables follow the grid, those ends have no
   partner, and their picks fail.
4. **Derived lists are cross products.** An item's orderable concentration is only its
   orderable quantity divided by the orderable quantity, each a value set. The solver keeps a
   value when it has a partner in each equation it appears in; it never checks a whole
   combination. The items of a component have fixed concentrations and so a fixed ratio to each
   other and to the component volume. No equation states that ratio for the orderable
   concentrations, so clavulaanzuur 5/3 mg/mL is offered while it implies amoxicilline
   16.7 mg/mL, below its 18 mg/mL minimum. The same for natrium and chloor in the NaCl and for
   the NaCl volumes 11 and 18 mL.

With defects 1 to 3 fixed, amox/clav still offers 138 picks over 6 lists of which 90 fail,
every one a defect 4 case.

## Decisions

Taken 2026-10-11 by the maintainer, during the investigation.

| Question | Decision |
|---|---|
| Both cap defects are bugs | Yes, the increment and the prune; fixed in one go, prototyped in a script first |
| The prune ends | An off-grid minimum or maximum is dropped; the ends are kept only when fewer than two values would remain |
| Count caps | The caps in `Order.minIncrMaxToValues` stay as they are (20 for a two-component discontinuous order, 10 without the full count, 5 for a timed order with more than two components, 100 and 1,000 continuous) |
| Loop order of `Order.minIncrMaxToValues` | Unchanged: components first, the dose quantity derived last and never pruned. Measured and rejected: the sum first; the largest count first with the dose quantity in the list, with the cap once (3 to 9 s pipelines) or always (amfo 172, tpn 32, tpnComplete 76 unreachable picks against 0 today); the largest component first without the dose quantity (identical to today); a min-max solve between the passes (identical, or worse with the dose quantity in the list) |
| Fixtures must stay fast | A pipeline that takes more than a few seconds on a fixture is out, whatever its reachability |
| The ratio between the items | Fixed with equations, not with a filter of the lists; the equations must hold for any number of items, and items of different unit groups are never added together |

Open for the maintainer, proposed below:

| Question | Proposal |
|---|---|
| The variable that holds one for the share sum | A constant mapped into the equations, not a field on `Orderable`; see [Chosen approach](#chosen-approach) |
| The message on a failed pick | Step 5, the issue's second expectation, in this plan; it can be dropped to a plan of its own |

## What the code does today

### The count cap

`OrderProcessor.processPipeline` (`OrderProcessor.fs:612`) runs CalcMinMax as apply-constraints,
calc-minmax, increase-increments, set-calculated-constraints, and for an order with a norm dose
and at most two components ensure-dose-values-1 and set-normdose. A reset runs ReCalcValues:
apply-calculated-constraints, calc-qty-values and the final solve.

- `Order.increaseIncrements logger 10 10` (`Order.fs:3481`) calls
  `Orderable.increaseQuantityIncrement` (`Order.fs:1893`). It raises each component's increment
  with `Component.increaseQuantityIncrement maxCount incrs` over the list 0.1, 0.5, 1, 5, 10,
  20 mL, takes the increment with the smallest base value and applies that to the orderable
  quantity and every component. The orderable's own need is never computed.
- `Order.minIncrMaxToValues` (`Order.fs:3609`) lists the component orderable quantities, the
  orderable dose quantity and the dose rate, converts the first that is still a min-incr-max
  with a defined increment, prunes it to the cap through
  `OrderVariable.minIncrMaxToValues` (`OrderVariable.fs:898`) and
  `Variable.minIncrMaxToValues` (`Variable.fs:3348`), writes it back and runs the full
  `solveOrder` before the next pass.
- `ValueSet.prune incr n` (`Variable.fs:985`) filters `x = mn || x = mx || (x / (incr * m))`
  is a whole number for m = 1, 2, ... until at most n values remain, with
  `incr |> Option.map ValueUnit.getBaseValue` and `vu |> ValueUnit.convertTo u |> getValue`.

### The equations

`EquationMapping.equations` (`EquationMapping.fs:107`) lists 57 product and sum equations with
the dose types they apply to, in the order of the "Equations" Google sheet, which is their
documentation; `EquationsTests` in `tests/Informedica.GenORDER.Tests/Tests.fs:1718` holds a
golden transcription of that sheet (65 rows, 8 of them applying to no dose type) and fails when
the embedded list drifts in text, dose types or order. `getEqsMapping` substitutes `[itm]`,
`[cmp]`, `[orb]` and `[ord]` per item and component; `Order.mapToOrderEquations`
(`Order.fs:3269`) resolves every name against `Order.toOrdVars ord` and raises
`KeyNotFoundException` for a name it does not find; the operands of a sum are filtered to the
unit group of its left side.

The equations that mention an item's orderable concentration all go through a quantity:
`[itm]_orb_qty = [itm]_orb_cnc * [orb]_orb_qty` and the dose variants. The component's share of
the orderable, `[cmp]_orb_cnc` in x, has `[cmp]_orb_qty = [cmp]_orb_cnc * [orb]_orb_qty` and the
dose variants. Nothing relates `[itm]_orb_cnc` to `[itm]_cmp_cnc`, and nothing says the shares
add up to one.

### The failed pick

`OrderContext.processScenarioOrder` (`OrderContext.fs:1229`) runs the pipeline and takes
`Result.defaultValue sc.Order`, so the context keeps the old order and the client sees a
successful answer with nothing changed.

## Approaches considered

For the cap defects:

- **Prune the sum instead of a component.** Right for amox/clav alone; wrong everywhere else,
  because the components stay ranges and get pruned anyway, and a pruned sum leaves derived
  lists full of unreachable picks (amfo 128, tpn 11, tpnComplete 31).
- **Convert the largest variable first, prune once.** A prune does not shrink the other ranges,
  so the later variables expand without a cap: amfo 9.3 s, tpnComplete 6.1 s.
- **Harmonize to the coarsest component increment.** Collapses the powder to 500 mg only and
  the dialog to one concentration.
- **Harmonize to the coarser of the finest component need and the total's need.** Chosen. Amox
  and powder keep 450 and 500 mg on a 1 mL grid, NaCl 11 to 18 mL, no prune, 10 concentrations.

For the cross products:

- **Filter every offered list by a solve per candidate.** Exact, 138 solves for amox/clav,
  still a cross product underneath.
- **A solver that checks whole combinations.** A different solver.
- **State the lost ties as equations.** Chosen. Two equations, measured on every fixture.

For the variable that holds one:

- **A field on `Orderable`.** Touches the `Orderable` record, its Dto, `Orderable.create`,
  `toOrdVars`, `fromOrdVars`, `applyConstraints`, the Shared `Orderable` type and
  `Models.Orderable.create`, both Server mappers, and the stored order plan JSON gains a field.
  The variable can never hold anything but one.
- **A constant mapped in.** `Order.mapToOrderEquations` resolves names against
  `toOrdVars ord` plus one constant orderable variable named as the mapping names
  `[orb]_orb_cnc`, holding 1 x; `fromOrdVars` ignores it since no field matches. Nine lines in
  `Order.fs`, nothing on the wire or in the store. Proposed.

## Chosen approach

Four source changes and one behaviour change, each a PR under 200 source lines.

### Fix 1, the common grid

`Orderable.increaseQuantityIncrement` computes the per-component increments as now and also the
increment the orderable quantity needs for `maxCount`. The grid is the larger of the finest
component increment and the orderable's; it is applied to the orderable quantity and every
component as now. Script: `OrderableFix.increaseQuantityIncrement`.

### Fix 2 and 3, prune

`ValueSet.prune` converts the increment to the unit of the values before the multiple test,
and the filter keeps multiples only. When fewer than two multiples remain, the minimum and the
maximum come back. Script: `ValueSetFix.prune`. Two existing tests in
`tests/Informedica.GenSOLVER.Tests/Tests.fs:822-900` assert the old end-keeping and change.

### Fix 4, the tie equations

Two rows appended to `EquationMapping.equations` for all dose types:

```text
[itm]_orb_cnc = [itm]_cmp_cnc * [cmp]_orb_cnc
[orb]_orb_cnc = sum([cmp]_orb_cnc)
```

The first: an item's concentration in the orderable is its concentration in the component times
the component's share. One per item, in the item's unit group times a dimensionless share, so
items are never added. The second: the shares add up to one; all in x, so the sum is always
valid. `Order.mapToOrderEquations` maps `[orb]_orb_cnc` to a constant holding 1 x, built per
call from the order's id and orderable name (`Name.create [id; name] |> Name.add "orb" |> Name.add "cnc"`
gives `[id.name]_orb_cnc`, as the mapping writes it). Script: `TieFix`.

### Measured, every fixture, caps as in the code

| | pipeline | pickable picks | unreachable |
|---|---|---|---|
| amox/clav today | 0.4 s | 37 amoxicilline concentrations | 25 |
| amox/clav, fixes 1 to 3 | 0.3 s | 138 over 6 lists | 90, none in the amoxicilline list |
| amox/clav, plus the item tie | 0.3 s | 134 | 86, clavulaanzuur list a tenth of amoxicilline's |
| amox/clav, plus both ties | 0.2 s | 48 over 6 lists | 0 |
| amfo, fixes 1 to 3 | 0.8 s | 37 over 2 lists | 0 (2 before fix 3) |
| morfCont | 0.2 s | 8 over 4 | 0 |
| tpn | 1.1 s | 20 over 4 | 0 |
| tpnComplete | 1.9 s | 20 over 4 | 0 |

The ties change nothing on amfo, morfCont, tpn and tpnComplete.

## Confidence

High for fixes 1 to 3 and the item tie: small functions, each measured on five fixtures, with
the pick-every-value check green. Medium for the share sum: it works on the five fixtures and the
constant is invisible outside the solve, but it adds one sum equation per order to every solve,
and the fixtures cover no order with a component whose share is not a plain volume ratio.
Low for step 5 until the client side is looked at.

## Steps

One PR at a time, in this order. Each step waits for the maintainer's go; the script code is
migrated by the maintainer or by the agent when told to.

1. **Prune, GenSOLVER.** `ValueSet.prune` as `ValueSetFix.prune` in the script. The two
   end-keeping tests in `tests/Informedica.GenSOLVER.Tests/Tests.fs` become: an off-grid minimum
   and maximum are dropped, `[3; 5..5..100; 107]` pruned to 5 gives `20;40;60;80;100`; the ends
   come back only when fewer than two values would remain; `10..0.5..20 mL` pruned to 20 gives
   the whole millilitres; an increment given in litres prunes as the same increment in
   millilitres. `fix(gensolver): prune in the unit of the values and drop off-grid ends`.
2. **The amox/clav fixture, GenORDER tests.** `Scenarios.amoxClavText` and `Scenarios.amoxClav`
   from the logged medication text, built as `pcmSuppText` and `pcmSupp` are; the helpers from
   the script that pick every value of every pickable list and solve (`pickables`,
   `unreachableAnyPick`) go to the test project. A test that the original pipeline leaves
   unreachable amoxicilline picks pins the defect. `test(genorder): add the amox/clav fixture`.
3. **The common grid, GenORDER.** `Orderable.increaseQuantityIncrement` as
   `OrderableFix.increaseQuantityIncrement`. Tests: NaCl, powder and total get the 1 mL grid;
   every amoxicilline pick solves; the diluent keeps a whole-millilitre grid without holes; the
   fixtures amfo, morfCont, tpn and tpnComplete solve within a few seconds with every pick of
   every pickable list reachable. The step 2 test flips to its fixed expectation.
   `fix(genorder): harmonize the quantity increment to the grid the total needs`.
4. **The tie equations, GenORDER.** The two rows in `EquationMapping.equations`, the constant in
   `Order.mapToOrderEquations`, the golden table in `EquationsTests` extended by the two rows
   with marker `xxxxx` (67 rows), and the "Equations" sheet gets the two rows so the sheet and
   the code agree. Tests: the clavulaanzuur list is a tenth of the amoxicilline list; every pick
   of every pickable list of amox/clav solves; the four fixtures unchanged and within a few
   seconds. `fix(genorder): tie item concentrations to the component share`.
5. **The message on a failed pick, GenORDER and the client.** `processScenarioOrder` keeps the
   old order on an error as now and carries the solver's message out, so the client can show
   why a pick was not applied. The shape of that message on the wire and in the dialog is
   decided with the maintainer before this step starts; it may become a plan of its own.

## Left out

- **A solver that checks whole combinations.** The ties make the amox/clav lists exact because
  every item has a single component concentration. An item with several component
  concentrations, from several products in one component, keeps a cross product between its
  concentration list and the share; `Orderable.harmonizeItemConcentrations` keeps the product
  indices aligned, which covers the known case.
- **Fixtures with a share that is not a volume ratio.** None of the five fixtures has a
  component quantity in a unit other than mL, so the share sum is untested there.
- **The order plan store.** Nothing changes on the wire or in the stored JSON under the
  chosen approach; the field alternative would.
- **The "Equations" sheet.** Documentation of the embedded list; the maintainer adds the two
  rows, the golden test pins them.

## Verification

Per step:

- `dotnet run build` and `dotnet test tests/Informedica.GenSOLVER.Tests/` or
  `dotnet test tests/Informedica.GenORDER.Tests/`
- `dotnet fsi scripts/CheckDependencyRule.fsx`
- `dotnet fantomas --check`
- `cd src/Informedica.GenORDER.Lib/Scripts && dotnet fsi Issue1302.fsx`: 30 tests green, and
  the printed fixture report shows 0 unreachable for amox/clav under both ties
- the maintainer repeats the issue's steps in the browser: the concentration list holds 10
  values and every one applies

## As built

| Step | Pull request | Note |
|---|---|---|
| The plan | #1420 | With the script that holds the fixes and their tests. |
