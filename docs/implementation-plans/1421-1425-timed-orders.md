# Implementation plan for issues #1421 and #1425

Two fixes to how GenORDER calculates orders, both met in parenteral nutrition and timed infusions:

- **#1421.** An order with more than two components solves every item orderable concentration as a
  cross product nobody picks, which takes half of its processing. The fix solves them through the
  component share alone.
- **#1425.** The rate of a timed order is raised to a rounded step that, times the time, narrows the
  dose quantity; with a fixed time the order can fail to calculate at all. The fix keeps the rate
  step within the window the time leaves.

Drafted 2026-10-11, for review. Line numbers are from `master` at `8e3df9a3`.

Both fixes are in the GenORDER library alone: no change to the contract, the server or the client
code. They are prototyped and measured in `src/Informedica.GenORDER.Lib/Scripts/`:

| script | what it holds |
|---|---|
| `Issue1425.fsx` | the first probes on the compiled library: the dose quantity with and without the time and rate limits, the cost of the item concentration equations |
| `Issue1425EquationMapping.fsx` | `EquationMapping.fs` with the changes of #1421 |
| `Issue1425Order.fsx` | `Order.increaseIncrements` with the rate step limited to the rate window (#1425) |
| `Issue1425Load.fsx` | `load.fsx` with the GenORDER sources in place of the DLL, so every version is timed on the same compile |
| `Issue1425Measure.fsx` | the whole processing and every pick of the dose dialog, timed, per fixture |
| `Issue1425Compare.fsx` | every variable of every fixture, processed and completed, printed to diff |
| `Issue1425Trace.fsx` | the variables from the morfine item to the orderable rate of a continuous order, per stage |

The versions, one define or a combination per run:

| defines | version | run files |
|---|---|---|
| none | the sources as they are | `m-today`, `c-today`, `t-today` |
| `VARIANT` | the change of #1421, the chosen rule | `m-variant`, `c-variant` |
| `VARIANT ANYSTRENGTH` | as `VARIANT`, items in more than one strength included | `m-anystrength`, `c-anystrength`, `t-anystrength` |
| `VARIANT SCHEDULE` | as `VARIANT`, continuous orders excluded in place of items in more than one strength | `m-schedule`, `c-schedule` |
| `VARIANT SHAPE` | as `VARIANT`, orders with an item whose orderable quantity is fixed excluded in place of items in more than one strength | `m-shape`, `c-shape` |
| `VARIANT SETTLED` | as `VARIANT`, and only while a component quantity has more than one value | `m-settled`, `c-settled` |
| `VARIANT DROPALL` | as `VARIANT`, the tie left out too | `m-dropall`, `c-dropall` |
| `VARIANT NOCMPRATE` | as `VARIANT`, without the component rate equation in place of the item rate product | `m-nocmprate`, `c-nocmprate` |
| `RATEWINDOW` | the rate window of #1425 alone, without the change of #1421 | `m-ratewindow`, `c-ratewindow` |

Run `dotnet fsi [--define:X ...] Issue1425Measure.fsx` (`m-`), `Issue1425Compare.fsx` (`c-`) or
`Issue1425Trace.fsx` (`t-`) and diff with the run without defines. The run files of this plan are
kept, untracked, in `localDocs/1425-runs/`; the three versions that are timed (none, `VARIANT`,
`RATEWINDOW`) ran one after the other, the others in parallel, so only their values count.

- [Fixtures](#fixtures)
- [#1421, the item concentrations by share](#1421-the-item-concentrations-by-share)
- [#1425, the rate step of a timed order](#1425-the-rate-step-of-a-timed-order)
- [Steps](#steps)
- [Left out](#left-out)
- [Verification](#verification)

## Fixtures

Twenty-two: the ones `ReachablePickTests` takes (amox/clav, amfo, morfCont, tpn without KCl,
pcmSupp, pcmDrink, cotrim, prednisolon), tpn, tpnComplete and the KCl once timed text, and eleven
built in `Issue1425Measure.fsx`:

| fixture | what it adds |
|---|---|
| amfo timed | amfo as a timed order over one to two hours |
| amfo over 2 uur | amfo as a timed order over exactly two hours: a fixed time |
| tpn, kalium limit binding | the kalium of KCl 7,4% at most 0,04 mmol/mL, below the 0,02 to 0,06 mmol/mL the component list allows: a concentration limit that binds |
| tpn, kalium rate limit binding | the kalium of KCl 7,4% at most 0,09 mmol/kg/uur, where its 2 mL/kg over 20 to 24 uur runs at 0,083 to 0,1: an item rate limit that binds |
| morfCont with NaCl | a third component, NaCl 0,9%: continuous with more than two components; the morfine in two strengths, 1 and 10 mg/mL, its orderable quantity fixed at 10 mg by its solution limit, the orderable quantity at 50 mL |
| morfCont with NaCl, no fixed quantity | the same without the fixed 10 mg |
| morfCont with NaCl, one strength | the same with the morfine in 1 mg/mL only |
| morfine timed with NaCl | morfCont with NaCl as a timed order, once a day over 20 to 24 uur |
| amfo with NaCl | a third component, NaCl 0,9%: discontinuous with more than two components |
| amfo with NaCl, two strengths | the same with the amfotericine b liposomaal in 2 and 4 mg/mL |
| amfo with NaCl, two strengths, timed | the same as a timed order over one to two hours |

The added diluents, gluc 10% and NaCl 0,9%, carry no limits on their items: an item rate limit on
the diluent of an order is rare, so the limits sit on the items of the first component.

## #1421, the item concentrations by share

### Problem

In an order with more than two components the solver computes every item orderable concentration
as a cross product of a component quantity and every orderable total. Nobody picks them (the dialog
does not offer them since #1302), yet they take half of the processing: tpnComplete needs 1,7 s for
it and up to 660 ms for one pick, against limits of 3 s and 1 s.

### What the code does today

`EquationMapping.equations` holds eight equations with `[itm]_orb_cnc`: the tie of #1302,
`[itm]_orb_cnc = [itm]_cmp_cnc * [cmp]_orb_cnc`, and seven products with an orderable total, among
them `[itm]_orb_qty = [itm]_orb_cnc * [orb]_orb_qty`. Which products an order gets depends on its
schedule:

| schedule | products |
|---|---|
| once | 3 |
| once timed | 4 |
| discontinuous | 5 |
| continuous | 6 |
| timed | 6 |

`EquationMapping.getEqsMapping` (`EquationMapping.fs:209`) maps them for every order. For a timed
order the item rate is tied to the orderable rate only through the product `[itm]_dos_rte =
[itm]_orb_cnc * [orb]_dos_rte`: the component rate equation `[cmp]_dos_rte = [cmp]_orb_cnc *
[orb]_dos_rte` (`EquationMapping.fs:154`) is mapped for continuous orders alone.

An item's solution limit sets the constraints of its orderable concentration
(`Medication.withItemSolution`, `Medication.fs:1392`), and its dose limit those of its dose
variables (`Medication.withItemDose`, `Medication.fs:1434`). Every tpn item has a concentration
maximum. An item's component concentration lists the strengths of its products as the values of
its defined constraint.

### Why the tie alone can lose exactness

`t-today` against `t-anystrength`, morfCont with NaCl after calc-minmax. The morfine comes in two
strengths, 1 and 10 mg/mL, its orderable quantity is fixed at 10 mg and the orderable quantity at
50 mL. Today the product `[itm]_orb_qty = [itm]_orb_cnc * [orb]_orb_qty` divides two single values:
the morfine concentration is 0,2 mg/mL exactly. Through the tie alone the solver multiplies the
strengths {1, 10} by the component share, 0,02 to 0,2, as two independent ranges, though each
strength belongs to its own component quantity (10 mL of 1 mg/mL or 1 mL of 10 mg/mL): the
concentration becomes 0,02 to 1 mg/mL. The morfine rate limit, 0,14 to 0,56 mg/uur, then bounds the
orderable rate through that concentration: 0,07 to 28 mL/uur in place of 0,7 to 2,8. Without the
fixed 10 mg the same happens: the morfine quantity reaches 280 mg where it reaches 28. With one
strength the tie multiplies a single value by the share, which is exact.

In morfine timed with NaCl the timed equations settle the strength to 1 mg/mL during calc-minmax,
today as with the change, so the products are not needed there; what settles it is not traced. The
rule does not rely on it: it keeps the products for any item in more than one strength.

### Decisions

| Question | Decision |
|---|---|
| What is left out | The products of an item orderable concentration with an orderable total. The tie stays: it gives the item concentrations the same values, and an item concentration limit keeps bounding the component share through it |
| For which orders | More than two components and no item in more than one strength, on any schedule. Read from the defined constraint of each item's component concentration, so no state, and `Order.solve` does not change |
| How an item rate keeps its bounds | A timed or once timed order the rule applies to gets the component rate equation in place of the item rate product; a continuous order has it already. Only there: mapped for every timed order, the equation removes dose quantities from an order that keeps its products (amfo with NaCl in two strengths, timed: 230, 235 and 335 mL) |
| Where the rule lives | One public function, `EquationMapping.leavesOutItemConcentrationProducts`, with `EquationMapping.hasStrengths`, used by `getEqsMapping`. The equation list does not change, so neither do the "Equations" sheet and its golden table |
| The commit type | `fix(genorder)`: it changes which equations act, not only how fast; the PR says so |

### Approaches considered

Each against the run without defines, on all twenty-two fixtures.

| Approach | Run | Result |
|---|---|---|
| Leave out all eight, the tie too | `c-dropall`, `m-dropall` | An item concentration limit stops pruning the components: tpn with the kalium concentration limit binding offers dose quantities from 430 mL where today it starts at 530 mL; amfo with NaCl from 11,2 mL where today from 111 mL. Rejected |
| Keep the equations for every item with a concentration limit | not run | Safe, but every tpn item has one, so nothing is gained. Rejected |
| Leave out the products for every order with more than two components | `c-anystrength`, `m-anystrength` | The two morfine orders in two strengths lose the exact concentration (see above): morfCont with NaCl offers a rate list of 1 to 28 mL/uur where it has 0,7 to 2,8, and 43 of its 243 picks fail. Rejected |
| As that, continuous orders excluded | `c-schedule`, `m-schedule` | The same as today on every fixture, but by a stand-in: it keeps the products of a continuous order in one strength, which does not need them (morfCont with NaCl, one strength, 210 → 147 ms with the chosen rule), and leaves them out of a timed order in more than one strength, which is safe in the fixtures only because something untraced settles the strength. Rejected |
| As that, orders with an item whose orderable quantity is fixed excluded | `c-shape`, `m-shape` | morfCont with NaCl without the fixed quantity loosens: the morfine quantity reaches 280 mg and its dose 20 mg/kg where today 28 mg and 2 mg/kg. Rejected |
| As chosen, only while a component quantity has more than one value | `c-settled`, `m-settled` | The same as the chosen rule; the state adds nothing, and would need `Order.solve` to solve once more when a solve settles the components. Rejected as needless |
| As chosen, without the component rate equation | `c-nocmprate`, `m-nocmprate` | The item rates of a timed order lose their bounds: tpn's eiwit rate becomes `<0 ..>` where it is 0,29 to 1,95 g/uur. With the kalium rate limit binding, the 2 failing KCl picks of today pass, because the limit no longer acts. Rejected |
| More than two components, no item in more than one strength, the component rate equation in place of the item rate product | `c-variant`, `m-variant` | The same as today on every variable of every fixture. Chosen |

### Chosen approach

- `EquationMapping.hasStrengths itm`: the defined constraint of the item's component concentration
  holds more than one value
- `EquationMapping.leavesOutItemConcentrationProducts ord`: more than two components and no item for
  which `hasStrengths` holds
- `EquationMapping.getEqsMapping`: when it holds, the equations with `[itm]_orb_cnc` other than the
  tie are left out, and a timed or once timed order gets `[cmp]_dos_rte = [cmp]_orb_cnc *
  [orb]_dos_rte`

### Measured

`c-variant` against `c-today`: every variable of every fixture, processed and completed by a pick of
every component, is the same, `Order.isSolved` included. `m-variant` against `m-today`: every list
the dialog offers is the same, and so is every failing pick (see Left out). The time of the
processing and of every pick of the dialog (mean / slowest):

| fixture | processing | picks |
|---|---|---|
| tpnComplete | 1745 → 954 ms | 358 / 661 → 289 / 533 ms |
| tpn | 890 → 606 ms | 173 / 316 → 145 / 265 ms |
| tpn, kalium limit binding | 892 → 617 ms | 198 / 294 → 154 / 236 ms |
| tpn, kalium rate limit binding | 867 → 588 ms | 186 / 300 → 140 / 245 ms |
| tpn without KCl | 535 → 399 ms | 32 / 51 → 23 / 37 ms |
| amfo with NaCl | 459 → 304 ms | 43 / 83 → 27 / 41 ms |
| morfCont with NaCl, one strength | 210 → 147 ms | 31 / 72 → 26 / 60 ms |
| the other fifteen | unchanged within noise | unchanged within noise |

The orders with an item in more than one strength keep their products and their time. The cost left
in tpn is the item rate lists: without the component rate equation a pick is several times faster,
but the item rate limits no longer act.

### Confidence

High: the comparison holds every variable of every fixture; a binding concentration limit and a
binding item rate limit act as today; orders with more than two components are covered on the
timed, discontinuous and continuous schedules, in one and in two strengths, with and without a
fixed item quantity. Not covered: once and once timed orders with more than two components; their
products are a subset of the timed ones.

## #1425, the rate step of a timed order

### Problem

The dose quantity of a timed order is a clinical quantity: the component and item dose limits and
the dilution limits bound it. The time and the rate describe how it is given. The equation
`[orb]_dos_qty = [orb]_dos_rte * [ord]_sch_tme` is solved in every direction, so a rate on a coarse
grid times the time narrows the dose quantity. With a fixed time it does more: amfo over exactly
2 uur gets a rate step of 50 mL/uur, the dose quantity becomes 100 to 300 mL by 100, and no
combination of the component quantities reaches it, so the order cannot be calculated at all. An
infusion over a fixed time, "over 2 uur", is the common case.

### What the code does today

`Order.increaseIncrements` (`Order.fs:3639`) raises the rate increment of a timed order to a step of
`Order.roundedSteps` (`Order.fs:3583`) chosen on the count alone. The minimum rate is raised to the
first multiple of the step, and the solver passes it on through the rate times the time:

| fixture | time | step | dose quantity after calc-minmax | processed |
|---|---|---|---|---|
| tpn | 20 to 24 uur | 5 mL/uur | from 200 mL, 191 without the rate | 430 to 830 mL |
| amfo timed | 1 to 2 uur | 50 mL/uur | from 50 mL, 22 without the rate | 61 to 317 mL |
| amfo over 2 uur | 2 uur | 50 mL/uur | 100 to 300 mL by 100 | fails |

A dose quantity q is reachable when some rate on the grid lies between q / tmax and q / tmin. That
window is narrowest for the smallest dose quantity, qmin / tmin − qmin / tmax: 1,6 mL/uur for tpn,
11 mL/uur for amfo timed, 0 for a fixed time. A step wider than the window can leave it empty.

### Chosen approach

Two pure functions in `Order`, public:

- `rateWindow ord`: with a shortest time and a smallest dose quantity, qmin / tmin − qmin / tmax,
  or qmin / tmin without a longest time; 0 for a fixed time; None otherwise
- `rateSteps window steps`: the steps that fit in the window, none when none fits (a window of 0,
  or one under the finest step of 0,1 mL/uur), all steps without a window

`increaseIncrements` passes the steps that fit to `increaseRateIncrement`, and leaves the rate
increment as it is when none fits. The window is taken after the dose quantity increment is
raised.

### Measured

`m-ratewindow` and `c-ratewindow` against today, without the changes of #1421:

- amfo over 2 uur processes: dose quantities 61 to 317 mL, the list of the order without time and
  rate limits, and rates 30,5 to 158 mL/uur, one per dose quantity; 175 picks, none failing
- after calc-minmax the dose quantity of every timed order is the range without the rate: amfo
  timed from 22 mL, amfo with NaCl in two strengths timed from 35 mL, tpn and tpnComplete from
  191 mL, tpn without KCl from 180 mL. tpn with the
  kalium rate limit binding starts at 191 mL too, where today it starts at 203 mL: the 203 came
  from today's rate grid, since a maximum on a rate bounds the dose quantity from above only
- every variable of every other fixture, processed and completed, is the same as today, and so is
  every list the dialog offers

### What it fixes and what it does not

For a **fixed time** the fix is complete: the rate gets no step, stays a range, and takes one value
per dose quantity.

For a **time range** it is not. The value step still prunes the rate to the cap on rounded steps,
without the window: tpn ends at 20 to 40 mL/uur by 5, amfo timed at 50 to 300 mL/uur by 50. Time
times rate can then still bound the dose quantity. No fixture shows it: the component quantities
are pruned to a few values each, and for tpn they alone put the dose quantity at 430 to 830 mL. The
cap and the window conflict: tpn's 34 rate values cannot become 5 while every dose quantity stays
reachable. A full fix gives the rate of a timed order no values from a capped list until the dose
quantity has one value, and then the values that quantity and the time range allow, as the reset
path already orders it. The dose dialog shows a rate without values as one range with its stepper
(`ViewHelpers.ovarValsWithRange`), so it needs no change for that. Whether the component pruning,
which narrows the dose quantity on its own, falls under the issue is a question for the issue.

**Decision:** step 2 fixes the fixed time and references #1425, which stays open for a time range.

## Steps

One PR at a time, in this order. Each step waits for the maintainer's go; the script code is
migrated by the maintainer or by the agent when told to.

1. **The item concentrations of more than two components by share, GenORDER (#1421).**
   `EquationMapping.hasStrengths` and `EquationMapping.leavesOutItemConcentrationProducts`;
   `getEqsMapping` leaving out the products and adding the component rate equation for a timed or
   once timed order. In `Scenarios.fs`: `tpnKaliumConcentrationBinding`, `tpnKaliumRateBinding`,
   `morfContWithNaCl`, `morfContWithNaClOneStrength` and `amfoWithNaClTwoStrengthsTimed`. Tests:
   - `hasStrengths`: the morfine of morfCont (true), every tpn item (false)
   - `leavesOutItemConcentrationProducts`: tpn (true), amfo with NaCl (true), morfCont with NaCl
     one strength (true), morfCont with NaCl (false), amfo with NaCl in two strengths timed
     (false), amox/clav with two components (false)
   - `getEqsMapping` for tpn: the tie, the component rate equation and no product with an orderable
     total; for morfCont with NaCl: every equation as today
   - the concentration limit binding: the processed dose quantity starts at 530 mL, against 430 mL
     for tpn
   - the rate limit binding: the maximum of the processed kalium rate of KCl 7,4% is at or under
     0,09 mmol/kg/uur, and every tpn item rate has a finite upper bound
   - morfCont with NaCl: the rate list as today, 0,7 to 2,8 mL/uur
   - amfo with NaCl in two strengths, timed: the dose quantities as today, 230, 235 and 335 mL
     included
   - the concentration limit binding, morfCont with NaCl and morfCont with NaCl in one strength join
     `ReachablePickTests`; the rate limit binding fixture only once its failing KCl picks are
     solved (see Left out)
   - the golden orders, the totals golden, the equations golden table and the print and
     MedicationText tests run unchanged; no value changes, so no regeneration is expected
   - in passing: the parameter `process` of `ReachablePickTests.solvedWith`, a word F# reserves
     (warning FS0046), renamed to `processOrder`

   About 25 source lines. `fix(genorder): solve item concentrations by share`.


2. **The rate step within the time window, GenORDER (#1425).** `Order.rateWindow` and
   `Order.rateSteps`, and `Order.increaseIncrements` raising the rate only to a step that fits.
   `Scenarios.amfoTimed` and `Scenarios.amfoTimedFixed` in `Scenarios.fs`. Tests:
   - `rateWindow`: tpn 1,6 mL/uur, amfo timed 11 mL/uur, the KCl once timed order 16 mL/uur, amfo
     over 2 uur 0, a time without a maximum qmin / tmin, a discontinuous order None
   - `rateSteps`: the steps that fit; none for a window of 0; none for a window under 0,1 mL/uur;
     all without a window
   - amfo over 2 uur processes, with the dose quantities of the order without time and rate limits
     (61 to 317 mL) and every rate the dose quantity over 2 uur; today it fails
   - after calc-minmax the dose quantity of amfo timed starts at 22 mL, of tpn at 191 mL
   - amfo timed and amfo over 2 uur join `ReachablePickTests`
   - the golden orders and the totals golden run unchanged

   About 30 source lines. `fix(genorder): keep the rate step within the time window`. The PR names
   the fixed time ("over 2 uur") as the case it fixes, and references #1425 without closing it.

## Left out

- **Failing KCl picks under a binding item rate limit.** With the KCl kalium rate at most
  0,09 mmol/kg/uur, 2 of the 19 offered picks of tpn fail, today as with either step: the KCl list
  offers quantities the limit refuses. The kind of defect #1302 removed for the concentrations,
  here for an item rate; not caused by this plan.
- **Orders with an item in more than one strength.** They keep their products and their processing
  time, by decision: the tie alone loses their exact concentration.
- **The rate prune of a time range (#1425).** See "What it fixes and what it does not" above.
- **The item rate lists.** They are most of the time left in a pick of tpnComplete. Keeping them as
  ranges loses the item rate limits; a fix needs the solver to bound a product without listing it.

## Verification

Per step:

- `dotnet run build` and `dotnet test tests/Informedica.GenORDER.Tests/`
- `dotnet run BenchmarkBuild`, since the benchmark is outside the solution
- `dotnet fsi scripts/CheckDependencyRule.fsx`
- `dotnet fantomas --check`
- `Issue1425Measure.fsx` and `Issue1425Compare.fsx` against the new source: the numbers above, and
  no difference with today's output beyond the ones each step names
- the maintainer opens tpn and an infusion over a fixed time in the browser
