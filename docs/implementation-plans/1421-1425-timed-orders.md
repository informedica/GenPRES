# Implementation plan for issue #1421, with the findings on #1425 and #1433

Two problems in how GenORDER calculates orders, both met in parenteral nutrition and timed
infusions:

- **#1421.** An order with more than two components solves every item orderable concentration as a
  cross product nobody picks, which takes half of its processing. The fix solves them through the
  component share alone.
- **#1425.** The rate of a timed order is raised to a rounded step that, times the time, narrows the
  dose quantity. A first fix was measured and changes nothing the dialog offers; the issue needs its
  own plan, recorded below.
- **#1433.** Harmonizing the item concentrations of a component drops a real strength of an item
  when the items of the component have different numbers of strengths. It met #1421 in the fixture
  for an item in two strengths, recorded below with a step of its own.
- **The rounded steps.** Capping the steps an increment is raised along and a pruned list keeps,
  met while measuring #1421: a step of its own, after #1421.

Drafted 2026-10-11, for review. Line numbers are from `master` at `8e3df9a3`, those in the #1433
section from `master` at `21bfee5a`.

The fix is in the GenORDER library alone: no change to the contract, the server or the client code.
It is prototyped and measured in `src/Informedica.GenORDER.Lib/Scripts/`:

| script | what it holds |
|---|---|
| `Issue1425.fsx` | the first probes on the compiled library: the dose quantity with and without the time and rate limits, the cost of the item concentration equations |
| `Issue1425EquationMapping.fsx` | `EquationMapping.fs` with the changes of #1421 |
| `Issue1425Order.fsx` | `Order.increaseIncrements` with the rate step limited to the rate window: the first attempt at #1425, kept as evidence |
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
| `RATEWINDOW` | the first attempt at #1425 alone, without the change of #1421 | `m-ratewindow`, `c-ratewindow` |

Run `dotnet fsi [--define:X ...] Issue1425Measure.fsx` (`m-`), `Issue1425Compare.fsx` (`c-`) or
`Issue1425Trace.fsx` (`t-`) and diff with the run without defines. The run files of this plan are
kept, untracked, in `localDocs/1425-runs/`; the three versions that are timed (none, `VARIANT`,
`RATEWINDOW`) ran one after the other, the others in parallel, so only their values count.

- [Fixtures](#fixtures)
- [#1421, the item concentrations by share](#1421-the-item-concentrations-by-share)
- [The finding on #1425](#the-finding-on-1425)
- [#1433, harmonize drops a strength](#1433-harmonize-drops-a-strength)
- [The rounded steps](#the-rounded-steps)
- [Steps](#steps)
- [Left out](#left-out)
- [Verification](#verification)

## Fixtures

Twenty-one: the ones `ReachablePickTests` takes (amox/clav, amfo, morfCont, tpn without KCl,
pcmSupp, pcmDrink, cotrim, prednisolon), tpn, tpnComplete and the KCl once timed text, and ten
built in `Issue1425Measure.fsx`:

| fixture | what it adds |
|---|---|
| amfo timed | amfo as a timed order over one to two hours |
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
its defined constraint; a substance always has one.

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
rule does not rely on it: it leaves the products out only when every item has exactly one strength.

### Decisions

| Question | Decision |
|---|---|
| What is left out | The products of an item orderable concentration with an orderable total. The tie stays: it gives the item concentrations the same values, and an item concentration limit keeps bounding the component share through it |
| For which orders | More than two components and every item in exactly one strength, on any schedule. Read from the defined constraint of each item's component concentration, so no state, and `Order.solve` does not change. "Exactly one" rather than "not more than one": an item without a concentration, which valid input never has, keeps today's equations |
| How an item rate keeps its bounds | A timed or once timed order the rule applies to gets the component rate equation in place of the item rate product; a continuous order has it already. Only there: mapped for every timed order, the equation removes dose quantities from an order that keeps its products (amfo with NaCl in two strengths, timed: 230, 235 and 335 mL while harmonize ran; step 2c moves them, see there) |
| Where the rule lives | One public function, `EquationMapping.leavesOutItemConcentrationProducts`, with `EquationMapping.hasOneStrength`, used by `getEqsMapping`. The equation list does not change, so neither do the "Equations" sheet and its golden table |
| The commit type | `fix(genorder)`: it changes which equations act, not only how fast; the PR says so |

### Approaches considered

Each against the run without defines, on all twenty-one fixtures.

| Approach | Run | Result |
|---|---|---|
| Leave out all eight, the tie too | `c-dropall`, `m-dropall` | An item concentration limit stops pruning the components: tpn with the kalium concentration limit binding offers dose quantities from 430 mL where today it starts at 530 mL; amfo with NaCl from 11,2 mL where today from 111 mL. Rejected |
| Keep the equations for every item with a concentration limit | not run | Safe, but every tpn item has one, so nothing is gained. Rejected |
| Leave out the products for every order with more than two components | `c-anystrength`, `m-anystrength` | The two morfine orders in two strengths lose the exact concentration (see above): morfCont with NaCl offers a rate list of 1 to 28 mL/uur where it has 0,7 to 2,8, and 43 of its 243 picks fail. Rejected |
| As that, continuous orders excluded | `c-schedule`, `m-schedule` | The same as today on every fixture, but by a stand-in: it keeps the products of a continuous order in one strength, which does not need them (morfCont with NaCl, one strength, 206 → 146 ms with the chosen rule), and leaves them out of a timed order in more than one strength, which is safe in the fixtures only because something untraced settles the strength. Rejected |
| As that, orders with an item whose orderable quantity is fixed excluded | `c-shape`, `m-shape` | morfCont with NaCl without the fixed quantity loosens: the morfine quantity reaches 280 mg and its dose 20 mg/kg where today 28 mg and 2 mg/kg. Rejected |
| As chosen, only while a component quantity has more than one value | `c-settled`, `m-settled` | The same as the chosen rule; the state adds nothing, and would need `Order.solve` to solve once more when a solve settles the components. Rejected as needless |
| As chosen, without the component rate equation | `c-nocmprate`, `m-nocmprate` | The item rates of a timed order lose their bounds: tpn's eiwit rate becomes `<0 ..>` where it is 0,29 to 1,95 g/uur. With the kalium rate limit binding, the 2 failing KCl picks of today pass, because the limit no longer acts. Rejected |
| More than two components, every item in exactly one strength, the component rate equation in place of the item rate product | `c-variant`, `m-variant` | The same as today on every variable of every fixture. Chosen |

### Chosen approach

- `EquationMapping.hasOneStrength itm`: the defined constraint of the item's component concentration
  holds exactly one value
- `EquationMapping.leavesOutItemConcentrationProducts ord`: more than two components and
  `hasOneStrength` for every item
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
| tpnComplete | 1728 → 952 ms | 363 / 654 → 291 / 541 ms |
| tpn | 910 → 657 ms | 173 / 313 → 148 / 266 ms |
| tpn, kalium limit binding | 904 → 602 ms | 200 / 301 → 153 / 228 ms |
| tpn, kalium rate limit binding | 873 → 592 ms | 185 / 299 → 139 / 245 ms |
| tpn without KCl | 549 → 410 ms | 33 / 50 → 23 / 32 ms |
| amfo with NaCl | 459 → 306 ms | 44 / 82 → 26 / 40 ms |
| morfCont with NaCl, one strength | 206 → 146 ms | 31 / 82 → 26 / 60 ms |
| the other fourteen | unchanged within noise | unchanged within noise |

The orders with an item in more than one strength keep their products and their time. The cost left
in tpn is the item rate lists: without the component rate equation a pick is several times faster,
but the item rate limits no longer act.

### Confidence

High: the comparison holds every variable of every fixture; a binding concentration limit and a
binding item rate limit act as today; orders with more than two components are covered on the
timed, discontinuous and continuous schedules, in one and in two strengths, with and without a
fixed item quantity. Not covered: once and once timed orders with more than two components; their
products are a subset of the timed ones.

## The finding on #1425

### The model

The dose quantity of a timed order is a clinical quantity: the component and item dose limits and
the dilution limits bound it. The rate is set on the pump, in steps of 0,1 mL/uur
(`Medication.withOrderableDose` gives every infusion rate that increment). The time is never fixed:
it follows from the dose quantity and the rate, and is solved only after the rate. A time limit is
a range the result must fall in. A minimum equal to the maximum makes it a fixed time, which is
invalid input; it is added to the input checks of #1427.

### What the code does today

`Order.increaseIncrements` (`Order.fs:3639`) raises the rate increment of a timed order to a step of
`Order.roundedSteps` (`Order.fs:3583`) chosen on the count alone. The minimum rate is raised to the
first multiple of the step, and the solver passes it on through the rate times the time:

| fixture | time | step | dose quantity after calc-minmax | processed |
|---|---|---|---|---|
| tpn | 20 to 24 uur | 5 mL/uur | from 200 mL, 191 without the rate | 430 to 830 mL |
| amfo timed | 1 to 2 uur | 50 mL/uur | from 50 mL, 22 without the rate | 61 to 317 mL |

The value step then prunes the rate to the cap on rounded steps: tpn ends at 20 to 40 mL/uur by 5,
amfo timed at 50 to 300 mL/uur by 50. Time times rate can then still bound the dose quantity.

### The first attempt

`Issue1425Order.fsx` raises the rate only to a step that fits the narrowest window the time range
leaves for the smallest dose quantity, qmin / tmin − qmin / tmax: 1,6 mL/uur for tpn, 11 mL/uur for
amfo timed. `m-ratewindow` and `c-ratewindow`: after calc-minmax the dose quantity of every timed
order widens to the range without the rate (amfo timed from 22 mL, tpn from 191 mL), but every
variable of every processed and completed order, and every list the dialog offers, is the same as
today. The component quantities are pruned to a few values each, and for tpn they alone put the dose
quantity at 430 to 830 mL; the rate prune decides the rest. So the attempt changes nothing a
prescriber sees, and is not a step of this plan.

A fixed time, which the attempt was first measured on, is not a valid order (see the model): with
it today's order can fail to calculate at all, but the answer is the input check of #1427, not the
calculation.

### The direction for its own plan

The rate of a timed order gets no values from a capped list until the dose quantity has one value;
then it gets the values on the pump's grid that put the time within its range, and the time follows
from the rate. The reset path already orders it so: the quantity values first with the rate held
back, then the rate values. The dose dialog shows a rate without values as one range with its
stepper (`ViewHelpers.ovarValsWithRange`), so it needs no change for that. Whether the component
pruning, which narrows the dose quantity on its own, falls under the issue is a question for the
issue.

## #1433, harmonize drops a strength

`Order.solve` runs `Orderable.harmonizeItemConcentrations` after every solve. Per component it
reads, for each item, the positions of its component concentration values within its defined
values, and when the items differ it applies the shortest list to every item. The position was
meant to name a product, but the values are sorted and made distinct, so it does not. In a
component whose items have different numbers of strengths, harmonize keeps the lowest strengths of
the longer list before any pick:

| order | defined | after the first solve |
|---|---|---|
| amoxicilline/clavulaanzuur | clavulaanzuur 5;10 mg/mL | 5 mg/mL |
| amfo with NaCl in two strengths, timed | amfotericine 2;4 mg/mL, beside saccharose 72 mg/mL | 2 mg/mL |

How it meets #1421: `hasOneStrength` reads the defined values, so the amfotericine still counts as
two strengths and the order keeps its products, which is correct. But the fixture for an item in
two strengths solves with one after its first solve, so its test does not exercise two strengths
that survive. The test says so until #1433 is fixed.

Found while building step 2c: with harmonize gone and no products on the order, both orders still
solve to one strength. Their dose limits decide it, not harmonize. The amoxicilline at 100 mg/kg/day
and the clavulaanzuur at 10 mg/kg/day, in one dose in mL, fit only 5 mg/mL clavulaanzuur; the
amfotericine likewise solves to 2 mg/mL. Harmonize only removed the 4 mg/mL earlier, within the
first solve. Without it the solver removes 4 mg/mL later, after it has set the step and the maximum
of the dose quantity with 4 mg/mL still among the values, and it never widens them: the dose
quantities of amfo with NaCl in two strengths, timed, go from 175 to 335 mL to 130, 180, 230 and
280 mL. Every value offered is within the old range.

### Where the product is lost

The product is there until the medication is built. A dose rule holds, per component, its products
(`ComponentLimit.Products`), and each product its substances with their concentrations.
`Medication.createComponents` (`Medication.fs:1026`) groups the substances of all products of a
component by name and keeps, per substance, the distinct concentrations. From there on an item has a
list of strengths and nothing says which strength of one item goes with which strength of another.
Harmonize (`Order.fs:1374`, run from `Order.solve` at `Order.fs:3495`) tries to recover the pairing
from positions in those lists, which are sorted and distinct, so it cannot.

After a scenario is first evaluated, `PrescriptionRule.filterProducts` (`PrescriptionRule.fs:335`)
keeps the products whose substances match the solved item concentrations, and the scenario keeps
their ids (`OrderScenario.ProductsIds`). That happens once, outside the solve: a later pick does not
update the list, and the solve itself cannot see a product, since the `Component` of an order
(`Types.fs:157`) holds none.

### Approaches considered

| Approach | Result |
|---|---|
| Remove harmonize only | No strength is dropped, but strengths of different products combine freely: 1000 mg amoxicilline with 100 mg clavulaanzuur in 20 mL. Rejected by decision |
| One component per product | Each item has one strength, but the orderable sums its components, so two products mix in one order, and nothing in the equations keeps all but one at zero. Rejected |
| A ratio of each item to a reference item | `[itm]_cmp_cnc = [ref]_cmp_cnc * [itm]_ratio` pairs exactly when the reference has one strength; with several, the ratios and the reference strengths cross again. Rejected |
| One medication, so one scenario, per combination of strengths | Exact, but the dialog shows a scenario per product where the solve can tell which products apply. Rejected by decision |
| The products on the component, those that apply derived after every solve | One scenario; solving the order leaves the list of products that still apply, most of the time one, sometimes more, and the item strengths follow those products. Chosen |

### Chosen approach

- **The products travel with the component.** `Medication.createComponents` keeps, beside the
  distinct strengths it builds today, the products of the component as a new record in GenORDER's
  types, `ComponentProduct`: `Id`, the GPK; `FormQuantities`, a `ValueUnit option` that can hold
  several values; and `Strengths`, the strength per substance name. The order's `Component` gets a
  list of them, so a solve can read them without the rule. The list is defined data: set when the
  order is built and never narrowed.
- **The form quantities are the component's.** `createComponents` gives a liquid that needs no
  reconstitution a component quantity of 1 mL, while its products carry their package volume. A
  product keeps its form quantities as `createComponents` maps them, 1 mL for such a liquid and
  the product's own quantities otherwise, so the narrowing compares like with like.
- **The products cross the client.** The server maps an order to the Shared `Component` field by
  field (`ServerApi.Mappers.Order.fs`), and every value command sends that order back. The Shared
  `Component` (`Shared/Types.fs:212`), both mappers and the client's pass-through carry the
  products too; otherwise the first pick returns an order without them, and the server has no
  rule at hand to put them back.
- **The text format carries them.** A component in the medication text format gets an optional
  Products block, so a fixture such as `amoxClavText` can list its two products.
- **Each solve narrows the values, in place of harmonize.** After a solve, a product applies when
  each of its substance strengths is among the values its item still has, and one of its form
  quantities is among the values of the component quantity. A field without a value set, such as a
  range after the min-max solve, excludes no product, as in `filterProducts`. The products that
  apply are derived from the values each time, not stored. Each item then keeps only the strengths
  of the products that apply, and the component quantity only their form quantities: the step
  changes the values of the variables, never their defined constraints. The component quantity was
  added while building step 2c: amoxicilline/clavulaanzuur solves to 5 mg/mL clavulaanzuur with a
  component quantity of 10 or 20 mL, and 20 mL at 5 mg/mL is in no product. When that removes a value, the order is solved again, as harmonize does today.
  Repeated, it settles: a step only removes values.
- **One rule decides that a product applies.** `PrescriptionRule.filterProducts` judges products
  on the first evaluation today, through `ComponentItem`s that `OrderContext.evaluateOrder` builds.
  Step 2c defines the rule once, as `Component.productApplies` in GenORDER, and both the first
  evaluation and every later solve use it; `OrderScenario.ProductsIds` comes from the products that
  apply, so `filterProducts` and the `ComponentItem` shape leave this path. The solve never writes
  the products on the order: they are set when the order is built or read, as the items are.
- **A cleared pick brings its products back.** Reopening resets the values from the defined
  constraints, and the products that apply are derived again from those values, so the strengths
  and products a pick removed return when it is cleared.
- **No products, no narrowing.** A component without products is left as it is. That covers order
  plans stored under JSON structure version 5 and earlier, text medications without a Products
  block, and the fixtures built by hand.
- **Products, but none applies, is an error.** When a component has products and none of them
  applies to the solved values, the values form a combination no product has. The solve returns an
  error, and the pick that led to it is refused as any failing pick is today.
- **One strength per item costs nothing.** When every item of a component has one strength, every
  product applies, so the step removes no value and solves nothing again: tpn and tpnComplete keep
  their processing time.
- **The pairing follows from picks.** With amoxicilline 50 mg/mL and clavulaanzuur 5 or 10 mg/mL,
  both products apply and both strengths stay. A pick of 10 mg/mL clavulaanzuur leaves the
  1000/200 mg product alone, and every item follows it. A combination no product has cannot
  survive a solve that picks one of its strengths.
- **The scenario shows the products that apply.** `OrderScenario.ProductsIds` follows the products
  left on the order after each solve, not only after the first evaluation.
- **Harmonize goes:** `Component.harmonizeItemConcentrations` and
  `Orderable.harmonizeItemConcentrations` in `Order.fs`, the event `ComponentItemsHarmonized`
  (`Types.fs:823`, `OrderLogging.fs:107`), and `OrderVariable.getIndices` and `applyIndices` with
  their wrappers, once nothing else uses them.
- **What the solve still allows.** Before a pick, the equations can combine a strength of one item
  with one of another inside a single solve, as they do today: two lists of strengths are two
  variables. What the products remove is every value no product has, after each solve; a full
  cross product of strengths therefore stays as it is, which is correct.
- **The order's shape changes.** The `Component` with its products reaches the order `Dto` and,
  through it, the stored order plan, which the store writes as JSON under a structure version
  (`jsonVersionRead` and `jsonVersionWritten`, now 5, in `ServerApi.SqlAdapters.fs`). The version
  moves to 6, with an upgrade that reads a plan written without products. That touches the server
  as well as GenORDER.

Decided in review: the narrowing reads the product's form quantity as well as its substance
strengths, as `filterProducts` does today; and only the scenario holds the products that apply for
now. Showing them in the dose dialog is a client change of its own, later.

## The rounded steps

`Order.roundedSteps` holds the steps an increment is raised along and a pruned list keeps the
multiples of: 0,1; 0,5; 1; 5; 10; 50; 100 and 500, in mL for a quantity and in mL/uur for a rate.
The decision: quantities stop at 50 mL and rates at 10 mL/uur, for the raise and for the prune.

Measured on the source with #1421:

- The cap on the raise alone moves no golden order. The cap on the prune moves tpn and
  tpnComplete: gluc 10% offers 150, 300, 450 and 600 mL where it offers 200, 400 and 600, the
  orderable quantity tops at 794 mL where at 744, and the time becomes the range 20 to 24 uur where
  it is a list. Why the time becomes a range is to be traced in that step.
- A rate of 50 to 500 mL/uur, NaCl 0,9% 1000 mL over 2 to 20 uur, offers 50 to 500 mL/uur by 50
  today and 60 to 480 mL/uur by 30 with the cap: when no step leaves few enough values, the prune
  keeps the multiples of the largest step times 2, 3 and so on. Accepted.

The work is kept, untracked, in `localDocs/step-cap/`: the changed files and a patch on the
source with #1421.

## Steps

One PR per step, after the maintainer's go; the script code is migrated by the maintainer or by
the agent when told to.

1. **The item concentrations of more than two components by share, GenORDER (#1421).**
   `EquationMapping.hasOneStrength` and `EquationMapping.leavesOutItemConcentrationProducts`;
   `getEqsMapping` leaving out the products and adding the component rate equation for a timed or
   once timed order. In `Scenarios.fs`: `tpnKaliumConcentrationBinding`, `tpnKaliumRateBinding`,
   `morfContWithNaCl`, `morfContWithNaClOneStrength` and `amfoWithNaClTwoStrengthsTimed`. Tests:
   - `hasOneStrength`: every tpn item (true), the morfine of morfCont (false), an item without a
     concentration (false)
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

   In the same PR, its own commit: the GenORDER tests in parallel, the value command tests of the
   domain one per scenario field, only the timed pick tests in sequence, and server garbage
   collection for the test project. `test(genorder): run the slow order tests in parallel`.

2. **The products narrowed by every solve (#1433).** Three PRs, so each stays under 200 source
   lines:
   - **2a, GenORDER.** `ComponentProduct` in the types; `Medication.createComponents` keeps the
     products; the order's `Component` and its `Dto` carry them; the text format gets an optional
     Products block; the order plan's JSON structure version moves from 5 to 6, with plans written
     without products read as before. No value changes; the golden orders stay. Tests:
     amoxicilline/clavulaanzuur from a rule carries its two products with their strengths; a `Dto`
     round trip keeps them; the text round trip in `MedicationTextTests` keeps a Products block; a
     liquid without reconstitution from a rule carries its products with a form quantity of 1 mL; a
     plan without products still reads; `amoxClavText` lists its two products.
     `feat(genorder): carry the products of a component`.
   - **2b, contract, server and client.** The Shared `Component` with its products, both server
     mappers, and the client's pass-through. Tests: an order mapped to the Shared type and back
     keeps its products. `feat(api): carry the products of a component`, its body listing the
     projects it touches.
   - **2c, GenORDER.** `Component.productApplies`, used by every solve in place of harmonize and
     by `Order.productIds`, from which `OrderScenario.ProductsIds` is set when the scenario is
     created and after every command; `filterProducts` and `ComponentItem` leave GenFORM. Each solve
     narrows the item strengths and the component quantity to the products that apply, and refuses
     a component whose products none apply, reported as the solver's empty value set. Tests, on
     `amoxClavText` and on two fixtures beside it, `amoxClavWithoutClavDoseText` (both strengths
     survive the solve) and `amoxClavEveryCombinationText`: amoxicilline/clavulaanzuur at 5 mg/mL
     keeps the 500/50 mg product and its 10 mL; without the clavulaanzuur dose limit it keeps 5 and
     10 mg/mL and both products; a pick of 10 mg/mL leaves one product, 20 mL and 200 mg
     clavulaanzuur, and cleared gives both back; four products for every combination keep every
     strength; products of which none applies are refused; a component without products keeps every
     strength; a liquid built from a rule keeps its product, compared at 1 mL. A plan stored under
     JSON structure version 5 reads with no products in the server test of step 2a, and an order
     without products keeps every strength in the GenORDER test above, so no read and solve test of
     its own. The amfo with NaCl in two strengths, timed, offers 130, 180, 230 and 280 mL, see the
     finding in the #1433 section; its stop-gap comment goes. A product strength whose value does not
     read is still dropped when a component Dto is read, by decision: there is no production
     database yet.
     `fix(genorder): pair item strengths by product`.

3. **The rounded steps capped, GenORDER.** `Order.roundedSteps` with a largest step,
   `Order.quantitySteps` up to 50 mL and `Order.rateSteps` up to 10 mL/uur, used by
   `increaseIncrements` and `pruneSteps`. The fixture `naclLargeRateText` and its test: the rate
   offers 60 to 480 mL/uur by 30. Measured on the 21 fixtures before it is committed; the tpn and
   tpnComplete golden orders regenerated, with what moved in the commit. Found in step 2c: the
   solver sets the step and the maximum of a dose quantity while a strength it removes later is
   still among the values, and never widens them; the amfo with NaCl in two strengths, timed, is
   the case to measure.


## Left out

- **Failing KCl picks under a binding item rate limit.** With the KCl kalium rate at most
  0,09 mmol/kg/uur, 2 of the 19 offered picks of tpn fail, today as with the change: the KCl list
  offers quantities the limit refuses. The kind of defect #1302 removed for the concentrations,
  here for an item rate; not caused by this plan.
- **Orders with an item in more than one strength.** They keep their products and their processing
  time, by decision: the tie alone loses their exact concentration.
- **The products that apply, stored on the scenario.** `OrderScenario.ProductsIds` holds what
  `Order.productIds` derives from the scenario's order, kept fresh where the scenario is created and
  after every command. The domain record could drop the field and the scenario Dto and the contract
  derive it.
- **The index functions left without a caller.** `ValueRange.getIndices` and `pickIndices`,
  `ValueSet.getIndices` and `pickIndices` in GenSOLVER and `ValueUnit.getIndices` and `pickIndices`
  in GenUNITS served harmonize alone; they go with their property tests in a refactor of their own.
- **A message of its own for a component no product applies to.** The solve reports it as the
  solver's empty value set, since the solve returns the solver's messages.
- **#1425.** See "The finding on #1425" above.
- **The item rate lists.** They are most of the time left in a pick of tpnComplete. Keeping them as
  ranges loses the item rate limits; a fix needs the solver to bound a product without listing it.

## Verification

- `dotnet run build` and `dotnet test tests/Informedica.GenORDER.Tests/`
- `dotnet run BenchmarkBuild`, since the benchmark is outside the solution
- `dotnet fsi scripts/CheckDependencyRule.fsx`
- `dotnet fantomas --check`
- `Issue1425Measure.fsx` and `Issue1425Compare.fsx` against the new source: the numbers above, and
  no difference with today's output
- the maintainer opens tpn in the browser: the dialog is quicker and its lists are as before
