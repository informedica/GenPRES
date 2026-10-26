# Implementation plan for issue #1302

The dose dialog offers only values the order can reach. Drafted 2026-10-11. Line numbers are
from `master` at `33f05274`. The fixes and their tests are prototyped in
`src/Informedica.GenORDER.Lib/Scripts/Issue1302.fsx`. The second pull request moved the rest of it
into the source and the GenORDER tests and removed the script; the prototypes are in the version
merged with the plan (#1420).

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

Two more defects showed once the rounded prune ran through the golden pipeline (`CalcMinMax`,
`IncreaseIncrements`, `CalcValues`, `SolveOrder`) on tpn:

- **Defect 5: an increment is raised when the range already fits.** `ValueRange.increaseIncrement` takes
   the first larger increment without checking the current count. KCl 7,4% `20..1..24 mL` (5
   values, cap 10) became the single value `20 mL`; with one increment missing,
   `Orderable.increaseQuantityIncrement` gave up on all components, so tpn's harmonization never
   ran. The second increase, from the `IncreaseIncrements` command, raised the tpn rate from 5
   to 10 mL/uur although its 7 values already fit.
- **Defect 6: the time is fixed before the rate is chosen.** The value step called `minimizeTime`, which
   sets a timed order's time to its minimum (20 uur). With `[orb]_dos_qty = [orb]_dos_rte *
   [ord]_sch_tme` the rate then decides the quantity: 10 mL/uur became a 200 mL grid on the
   total that the pruned components could not add up to, the glucose solve failed silently and
   glucose stayed a range.

## Decisions

Taken 2026-10-11 by the maintainer, during the investigation.

| Question | Decision |
|---|---|
| Both cap defects are bugs | Yes, the increment and the prune; fixed in one go, prototyped in a script first |
| The prune ends | An off-grid minimum or maximum is dropped; the ends are kept only when fewer than two values would remain |
| Count caps | The caps in `Order.minIncrMaxToValues` stay as they are (20 for a two-component discontinuous order, 10 without the full count, 5 for a timed order with more than two components, 100 and 1,000 continuous) |
| Loop order of `Order.minIncrMaxToValues` | Unchanged: components first, the dose quantity derived last (pruned like a component since the review, only reached with a dose count without values). Measured and rejected: the sum first; the largest count first with the dose quantity in the list, with the cap once (3 to 9 s pipelines) or always (amfo 172, tpn 32, tpnComplete 76 unreachable picks against 0 today); the largest component first without the dose quantity (identical to today); a min-max solve between the passes (identical, or worse with the dose quantity in the list) |
| Fixtures must stay fast | A pipeline that takes more than a few seconds on a fixture is out, whatever its reachability |
| The ratio between the items | Fixed with equations, not with a filter of the lists; the equations must hold for any number of items, and items of different unit groups are never added together |

Taken 2026-10-11 by the maintainer, after step 1 was first migrated and the golden orders of amfo, tpn
and tpnComplete changed (Samenstelling C went from the off-grid minimum 110 mL to 217 mL, the fourth
multiple of an arbitrary 31 mL grid):

| Question | Decision |
|---|---|
| The grid a prune keeps | Rounded values, not the smallest arbitrary multiple that fits: the finest step of a list given by the caller that leaves at most the cap |
| The step list | `0.1;0.5;1;5;10;50;100;500`, in mL for quantities and in mL/uur for rates, one list shared by the prune and the increment increase |
| A unit group without steps | 1 in the unit of the values, then multiples of 1; a tablet dividable in quarters prunes to whole tablets |
| A step that overshoots | When a step leaves too many values and the next leaves fewer than two, multiples of the finer step are taken (glucose 1 to 640 mL with a cap of 5 gives `200;400;600`, not `500` with the ends) |
| The ends | The old prune always kept the minimum and the maximum. The rounded prune drops an off-grid end and keeps the ends only when fewer than two values would remain |
| What is pruned | Component quantities for any number of components, the dose quantity and the rate (see the review decisions below for the dose quantity) |
| `Order.maximizeRate` | Removed: it had no caller |
| The caps | Parameters, a `ValueCaps` record with today's values as the default, so a test can lower them and exercise the prune |
| Time | A test asserts the pipeline within 3 s and every pick within 1 s, on every fixture |
| Orders with more than two components | Only the component quantities and the rate must be reachable picks; the item concentrations are not offered as picks there, there is no use case. Filed as #1421 |

Taken 2026-10-11 by the maintainer, after the golden pipeline left the tpn glucose a range:

| Question | Decision |
|---|---|
| The increment of a sum | The total and every component of a sum share one increment, as the code already did; the change is which one: the finest rounded step on which every component fits the cap, else the coarsest on which the total and every component keep at least two values. The per-component raise before it goes |
| A range that already fits | Keeps its increment; `ValueRange.increaseIncrement` leaves it as it is |
| The rate and the time | The orderable rate, the only rate that is pruned, determines how long the dose quantity of a timed order runs; the value step leaves the time free instead of setting it to its minimum |
| Pull requests | The rounded prune, the common increment, the fit check and the free time are one pull request, not split, although it is over the 200 source line limit. The tie equations, the caps as a parameter and the amox/clav and prednisolon fixtures are the second |

Taken 2026-10-11 by the maintainer, on the code review of the first pull request:

| Question | Decision |
|---|---|
| A dose rule bound that is off the grid | Not offered (the reviewer's point 1, option c). tpn Samenstelling C loses 110 mL, the 10 mL/kg minimum, and amfo loses its top concentration of 2 mg/mL; the offered range starts at 150 mL and ends at 1,11 mg/mL. Keeping the ends of a range when a pick on them solved was tried and rejected: the ends of a component quantity are derived, so it also brought back 1 mL of glucose in the tpn and still missed the amfo 2 mg/mL, which is a bound on the item concentration. Neither the defined nor the calculated constraints of a pruned variable tell a rule bound from a derived end. A follow-up starts from the defined constraints of the dose and concentration variables |
| The common increment and an open quantity | Only quantities with a minimum and a maximum count towards the choice; a solved quantity, such as a fixed total, has no increment and no choice and is left out |
| A failed `setIncr` | All or none: when one quantity cannot take the common increment the orderable is left as it is |
| The dose quantity | A measurable amount like a component quantity: pruned with the same cap and rounded steps, and its increment raised after the components, to a step that is a multiple of their common increment. Only with a dose count without values (a range, or no constraint): with a dose count that has values the dose quantity is the orderable quantity divided by it and follows the components' increment, and raising it would break the sum (measured: amfo and tpn fail with a fixed dose count of 1 x). Without the cap, a dose count of 1 to 3 x turned the amfo dose quantity into 2,926 values (pipeline 2.3 s) and the tpn one into 531; with it `50..50..300 mL` (0.95 s) and `400;600;800 mL` (0.7 s). With a fixed dose count the loop never converts the dose quantity, so the fixtures do not change |
| The dose count | Not a count of administrations: the inverse of the share of the orderable quantity given per dose (`[orb]_orb_qty = [orb]_dos_cnt * [orb]_dos_qty`), so a fractional value such as 2,86 x is valid |
| Dead code | `Component.increaseQuantityIncrement`, `Order.maximizeRate`, `Order.minimizeTime`, `Schedule.setMinTime`, the `minTime` parameter and the duplicate `reCalcValuesStep` are removed. The cleared-order path passed `minTime = true` only for continuous and discontinuous orders, where it did nothing, so a clear behaves as before |

Taken 2026-10-11 by the maintainer, during the second pull request and its review:

| Question | Decision |
|---|---|
| The variable that holds one for the share sum | A constant mapped into the equations, not a field on `Orderable`; `Order.orderableShare`, added to the variables of `Order.mapToOrderEquations` only |
| The ties and an order with one component | Not mapped: `EquationMapping.getEqsMapping` leaves out both rows when the order has one component, whose share is 1 x and whose items the other equations already tie |
| The caps in the tests | Through the real pipeline: `OrderProcessor.processPipelineWith` and `processClearedOrderWith` take the caps, `processPipeline` and `processClearedOrder` pass `ValueCaps.defaults` |
| The tpn in the pick test | tpn without KCl, more than two components at a fifth of the time; the full tpn and tpnComplete are pinned by their golden orders only, no test picks every value on them |
| Time limits on CI | Three times the local limits when `CI` is set; the runner shares its cores between the test projects |
| The "Equations" sheet | The maintainer adds the two rows; the pull request does not change the sheet |
| The implementation script | Removed; everything in it moved to the source and the GenORDER tests |

Taken 2026-10-11 by the maintainer, before the third step:

| Question | Decision |
|---|---|
| The message on a failed pick | Step 3 in this plan, one pull request over GenORDER, the contract and the client |
| What the wire carries | A fourth refusal case, `NotSolved`, without detail, on the existing refusal path; the solver's messages stay in the server log |
| What the prescriber sees | A short message on the snackbar, a warning, while the dose dialog stays open on the order as it was before the change; not the refusal notice, which empties the scenarios and closes the dialog. The text names a change, not a value: a scenario selected, a reset, a frequency button and a reopen are refused the same way |
| A solve that fails inside `Order.minIncrMaxToValues` | A failed pick too: it returns an error with the order of the pass before, instead of that order as a success. The value step of `CalcMinMax` for a norm dose keeps the order before on that error, so the scenario list, which keeps only the orders that solve, lists the same scenarios as before |
| The message-list evaluate | Answers the context before the change, where it answered the value set and unsolved; a change for `OrderPlan.evaluate` and the MCP tools, accepted as the better answer |

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
- **Harmonize to the coarser of the finest component need and the total's need.** Right for
  amox/clav, wrong for tpn: the total needs 100 mL, which NaCl `60..72` and KCl `20..24` cannot
  follow, so the sum gets mixed increments and the solve fails.
- **One increment for the sum, chosen by what every member can follow.** Chosen. The finest
  rounded step on which every component fits the cap, else the coarsest on which every member
  keeps at least two values: 1 mL for amox/clav and for tpn.

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

Two pull requests, and a third step for the message on a failed pick.

### Fix 1, the common increment of the sum

`Orderable.increaseQuantityIncrement` chooses one increment for the orderable quantity and every
component from the step list: the finest on which every component holds at most `maxCount` values,
else the coarsest on which the orderable quantity and every component keep at least two. When every
component already fits on its own increment nothing changes. The increment is set directly with
`ValueRange.setIncr`, so a quantity that already fits gets it too. The raise of each component on its
own before the choice is gone. `Order.increaseIncrements` takes its increments from the shared step
list, so 20 mL gives way to 50, 100 and 500 mL.

`ValueRange.increaseIncrement` leaves a range that already holds at most `maxCount` values as it is.
Only quantities with a minimum and a maximum count towards the choice, a solved quantity is left out
of the counts, a step that a solved quantity is not a multiple of is skipped, and when one quantity
cannot take the increment the orderable stays as it is.

### Fix 5, the free time

`Order.minIncrMaxToValues` no longer sets the time of a timed order to its minimum before it turns
ranges into values in the value step: the time follows from the dose quantity and the rate. The
cleared-order path already left a timed order's time free. `Order.minimizeTime`,
`Schedule.setMinTime` and the `minTime` parameter are removed. The dose quantity is turned into
values with the same cap and rounded steps as a component quantity. `Order.increaseIncrements` raises
the dose quantity's increment after the components and before the rate
(`Order.increaseDoseQuantityIncrement`), to a step that is a multiple of the orderable quantity's
increment, and only when the dose count has no values (a range, or no constraint).

### Fix 2 and 3, the rounded prune

`ValueSet.prune steps incr n` keeps the multiples of the finest step that leaves at most n values. A
step counts when it is in the unit group of the values and a multiple of the increment, compared in
the unit of the values. Without a step for the unit group the step is 1, then multiples of 1. When a
step leaves too many values and the next fewer than two, the multiples of the finer step times m are
taken. Off-grid ends are dropped and come back only when fewer than two values remain. In the source
with the first pull request. The two end-keeping tests in `tests/Informedica.GenSOLVER.Tests/Tests.fs` change.

`Variable.minIncrMaxToValues` and `OrderVariable.minIncrMaxToValues` pass the steps through.
`Order.minIncrMaxToValues` passes the shared step list in mL and mL/uur. `Order.maximizeRate`, which
had no caller, is removed. In the second pull request `Order.minIncrMaxToValues` takes a `ValueCaps`
record instead of the numbers in its body. Script: `CapsFix.ValueCaps`,
`CapsFix.minIncrMaxToValuesWith`.

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

The table above counts a list as pickable when it holds a value set. Under the numeric prune the
item concentrations of tpn and tpnComplete stay ranges, so their 0 hides them. The rounded grids
turn them into value sets, and 950 (tpn) and 2,202 (tpnComplete) of their picks fail: a component's
share is its quantity over the total, the total is the sum of four components, and the solver checks
each equation on its own. With two components the share sum is a relation between two shares and the
ties make the lists exact; with four it is not.

### Measured, the rounded prune, all fixes, today's caps

Picks are the lists the dialog must offer: with more than two components the component quantities
and the rate, otherwise also the item concentrations.

| | pipeline | picks | failing | slowest pick | component grids |
|---|---|---|---|---|---|
| amox/clav | 0.2 s | 48 | 0 | 16 ms | powder `9;10`, NaCl `12..17 mL` |
| amfo | 0.4 s | 21 | 0 | 39 ms | amfo `10,5..0,5..17,5 mL`, glucose `50..50..300 mL` |
| morfCont | 0.1 s | 8 | 0 | 18 ms | unchanged |
| tpn | 0.9 s | 19 | 0 | 309 ms | Samenstelling C `150;200;250`, NaCl `60;65;70`, glucose `200;400;600 mL`, rate `20..5..40 mL/uur` |
| tpnComplete | 1.7 s | 19 | 0 | 650 ms | as tpn |
| pcmSupp, pcmDrink, cotrim | < 0.1 s | 0 to 3 | 0 | 2 ms | unchanged |
| prednisolon, 5 mg tablet in quarters | < 0.1 s | 25 | 0 | 1 ms | `6..1/4..12`: 25 values, under the single-component cap of 100 that the reset uses; the four commands of `OrderProcessor` prune with the discontinuous cap of 10 and give the whole tablets `6..12`, with that cap at 30 the 25 quarter tablets, at 5 `6;8;10;12` |

With every cap lowered to 5 and to 3, no component of any fixture keeps more values than the cap and
every pipeline stays within 3 s.

### Measured, the golden pipeline on the compiled source of the first pull request

`CalcMinMax`, `IncreaseIncrements`, `CalcValues` and `SolveOrder` through `OrderProcessor`, on the
built `Informedica.GenORDER.Lib.dll`, without the tie equations. Every pick of the component
quantities and the orderable rate, solved with `Order.solveOrder`:

| | pipeline | quantity and rate picks | failing | slowest pick |
|---|---|---|---|---|
| amox/clav | 0.3 s | 4 | 0 | 8 ms |
| amfo | 0.4 s | 13 | 0 | 44 ms |
| morfCont | 0.1 s | 26 | 0 | 14 ms |
| tpn | 0.9 s | 19 | 0 | 312 ms |
| tpnComplete | 1.7 s | 19 | 0 | 633 ms |
| pcmSupp, pcmDrink, cotrim, prednisolon | < 0.1 s | 0 to 8 | 0 | 3 ms |

tpn: Samenstelling C `150;200;250`, NaCl `60;65;70`, KCl `20..24`, glucose `200;400;600 mL`, rate
`20;25;30;35;40 mL/uur`, time 20,8 to 24 uur. The nine pipelines in parallel take 1.7 s. pcmDrink
keeps `5..12 mL` and offers 4 x/dag again: 7, 8 and 9 mL give 67,2 to 86,4 mg/kg/dag, inside the rule
of 60 to 90; the old 5 mL grid made it unreachable. The item concentrations of amfo still fail on 893
of 1,021 picks, defect 4, fixed by the tie equations in the second pull request.

## Confidence

High for fixes 1 to 3 and the item tie: small functions, each measured on nine fixtures, with
the pick-every-value check and the time limits green. Medium for the share sum: it makes the lists
exact with two components and not with more, the constant is invisible outside the solve, but it
adds one sum equation per order to every solve, and the fixtures cover no order with a component
whose share is not a plain volume ratio and none with three components.
Low for step 3 until the client side is looked at.

## Steps

One PR at a time, in this order. Each step waits for the maintainer's go; the script code is
migrated by the maintainer or by the agent when told to.

1. **The rounded prune, the common increment and the free time, GenSOLVER and GenORDER.**
   `ValueSet.prune` keeps rounded steps, with the steps as a parameter through
   `ValueRange.prune`, `Variable.minIncrMaxToValues` and `OrderVariable.minIncrMaxToValues`;
   `Order.minIncrMaxToValues` passes the shared step list in mL and mL/uur; `Order.maximizeRate`
   is removed; `ValueRange.increaseIncrement` leaves a range that fits;
   `Orderable.increaseQuantityIncrement` chooses one increment for the sum; the value step leaves the
   time free. GenSOLVER tests: the finest step that fits; a step that overshoots gives the multiples
   of the finer step; off-grid ends dropped, back when fewer than two values remain;
   `10..0.5..20 mL` to the whole millilitres; an increment in litres as in millilitres; tablets on 1
   and its multiples; a range that fits keeps its increment, also a rate increased a second time; a
   range that does not fit still gets a larger one; an increment no step is a multiple of gives the
   multiples of that increment. GenORDER tests of `Orderable.increaseQuantityIncrement`: the
   coarsest step that keeps two values, the finest step that fits, KCl keeping its five values, a
   fixed total that does not block the choice, a fixed total off a step that makes a finer step win,
   an orderable that already fits left as it is; amfo with a dose count of 1 to 3 x gets the dose
   quantity `50..50..300 mL`. Dead
   code removed. The golden orders of amfo, pcmDrink, tpn and tpnComplete, the pcmDrink totals and
   the pcmDrink reopen test are regenerated and reviewed.
   Over the 200 source line limit, by the maintainer's decision.
   `fix(genorder): offer reachable rounded dose values`.
2. **The tie equations, the caps as a parameter and the fixtures, GenORDER.** The two rows in
   `EquationMapping.equations`, the constant in `Order.mapToOrderEquations`, the golden table in
   `EquationsTests` extended by the two rows with marker `xxxxx` (67 rows); the maintainer adds the
   two rows to the "Equations" sheet. The numbers in `Order.minIncrMaxToValues` become a `ValueCaps`
   record with today's values as the default; `OrderProcessor.processPipelineWith` takes the caps and
   `processPipeline` passes the default. `Scenarios.amoxClavText` and `Scenarios.prednisolonText`
   (5 mg tablets dividable in quarters, 1 to 2 mg/kg for 30 kg) go to `Scenarios.fs`, and the helpers
   that pick every value of the lists the dialog offers and solve, timed, to `ReachablePickTests` in
   the test project. Tests: the clavulaanzuur list is a tenth of the amoxicilline list; on every
   fixture (tpn without KCl in place of tpn and tpnComplete) every pick of the lists the dialog must
   offer solves, within 1 s, and the pipeline within 3 s, three times that on CI; prednisolon keeps the whole tablets `6..12` under the default
   discontinuous cap of 10, its 25 quarter tablets with that cap at 30 and `6;8;10;12` at 5; with every cap at 5 and at 3 no component of
   any fixture keeps more values than the cap.
   `fix(genorder): tie item concentrations to the component share`.
3. **The message on a failed pick, GenORDER, the contract and the client.** `Refusal.NotSolved`
   and `OrderContextRefusal.NotSolved`, mapped by the server's `refusalToModel`.
   `Order.minIncrMaxToValues` returns a `Result`; on a failed solve the error carries the order of
   the pass before, as the success did until now. The norm dose value step of `CalcMinMax` keeps
   the order before on that error, so no scenario drops from the list. `OrderContext.processScenarioOrder` answers an
   outcome: the context as sent, refused with `NotSolved`, when the pipeline fails. A value pick or
   clear (`SetNthOrderValue`, `ClearOrderValue`) is refused with the context before the value was
   set. The message-list `OrderContext.evaluate` answers that context too, where it answered the
   value set and unsolved until now. In the client the machine keeps the context it held, with its
   argumentation, and the dialog on `NotSolved`, and emits `TellNotSolved`, routed to
   `Alert.ChangeNotApplied`; `PickListPolicy.afterAnswer` puts the dialog's picks from before the
   change back when the answer is the order the change was sent from with the term `Prescribe Refusal Not solved` and a row in
   `localization.tsv`. Tests: the refusal over an unsolvable order for a solve, a value pick and the
   message-list evaluate, which answers the context before the pick; the machine keeps the dialog and tells; no notice for `NotSolved`; the
   server maps every case; fullMedication's golden order now reports the error of `CalcValues`.
   `fix(genorder): report a pick that cannot be solved`.

## Left out

- **Item concentrations of an order with more than two components.** The solver still computes
  them as cross products of a component quantity and every total, and most of their values are
  unreachable (tpn 950, tpnComplete 2,202 under the rounded prune). They are not offered as picks,
  so the dialog is not affected, but the solver spends time on the lists: tpnComplete takes 1.7 s
  for the pipeline and up to 650 ms for one pick. Filed as #1421.
- **A solver that checks whole combinations.** The ties make the amox/clav lists exact because
  every item has a single component concentration. An item with several component
  concentrations, from several products in one component, keeps a cross product between its
  concentration list and the share; `Orderable.harmonizeItemConcentrations` keeps the product
  indices aligned, which covers the known case.
- **Fixtures with a share that is not a volume ratio.** None of the five fixtures has a
  component quantity in a unit other than mL, so the share sum is untested there.
- **The order plan store.** Nothing changes on the wire or in the stored JSON under the
  chosen approach; the field alternative would.
- **The invariants of a component quantity.** Every component quantity must have a defined increment
  and, after the minimum and maximum calculation, a minimum and a maximum: without them the
  preparation is not described, the loop skips the component and the dose quantity becomes a range
  of its own. To be enforced when a medication is converted to an order.
- **Dose rule bounds off the grid.** An off-grid end of a pruned range is not offered, also when it
  is a rule bound (tpn 10 mL/kg Samenstelling C, amfo 2 mg/mL). Offering it needs the bounds of the
  dose and concentration variables traced back to the values of the pruned quantity that reach them.
- **The "Equations" sheet.** Documentation of the embedded list; the maintainer adds the two
  rows, the golden test pins them.

## Verification

Per step:

- `dotnet run build` and `dotnet test tests/Informedica.GenSOLVER.Tests/` or
  `dotnet test tests/Informedica.GenORDER.Tests/`
- `dotnet run BenchmarkBuild`, since the benchmark is outside the solution
- `dotnet fsi scripts/CheckDependencyRule.fsx`
- `dotnet fantomas --check`
- the `ReachablePickTests` in `tests/Informedica.GenORDER.Tests/Tests.fs`: 0 failing picks on every
  fixture, the pipeline within 3 s and every pick within 1 s
- the maintainer repeats the issue's steps in the browser: the concentration list holds 10
  values and every one applies

## As built

| Step | Pull request | Note |
|---|---|---|
| The plan | #1420 | With the script that holds the fixes and their tests. |
| Step 1, the rounded prune, the common increment and the free time | #1422 | One pull request over the size limit by the maintainer's decision; the dose quantity pruned like a component when the dose count has no values; the script trimmed to what the second pull request needs. |
| Step 2, the tie equations, the caps as a parameter and the fixtures | #1424 | The ties only for an order with more than one component; the caps through the real pipeline; the maintainer adds the sheet rows; the script removed. |
| Step 3, the message on a failed pick | #1426 | A fourth refusal reason without detail; a snackbar warning with the dialog kept on the order before the change; the norm dose step of CalcMinMax keeps the order before, so the scenario list is unchanged; the message-list evaluate now answers the context before the change. |
