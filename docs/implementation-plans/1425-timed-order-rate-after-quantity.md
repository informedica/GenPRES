# Implementation plan for issue #1425

Drafted 2026-10-12, for review. Line numbers are from `master` at `3e748d97`.

## Problem description

In a timed order the dose quantity is a clinical quantity: the component and item dose limits and
the dilution limits bound it. The rate is set on the pump, and the time follows from the quantity
and the rate. Today the rate gets rounded steps and value lists before the dose quantity is known,
and through the rate times the time those steps narrow the dose quantity. The issue asks for the
order of the calculation to be: the dose quantity first, then the rate, and the time last.

The findings so far are in the plan of #1421, `1421-1425-timed-orders.md`, in its section
"The finding on #1425". In short:

- `Order.increaseIncrements` (`Order.fs:3767`) raises the rate increment of a timed order to a
  rounded step (`Order.fs:3818`). The minimum rate goes up to the first multiple of that step, and
  the solver passes it on to the dose quantity: before PR #1443, tpn started at 200 mL where 191 mL is
  allowed, and amfo timed at 50 mL where 22 mL is allowed. Since PR #1443 caps the rate step at
  10 mL/uur, amfo timed starts at 22 mL and tpn still at 200 mL (see the probe below).
- The value step then gives the rate a pruned list of rounded values, and the rate list times the
  time can bound the dose quantity again.
- Changing only the rate step, the first attempt, changed nothing a prescriber sees: the pruned
  component quantities already decide the dose quantity of tpn.

## What the code does today

The order processor already does the values in two stages for a timed order. `minIncrMaxToValues`
(`Order.fs:3913`) takes `skipRate`, and three pipelines in `OrderProcessor.fs` use it: first the
quantities with the rate held back, then the rate.

| where | what runs for a timed or once timed order |
|---|---|
| `CalcMinMax` (`OrderProcessor.fs:747`) | apply the constraints, solve min and max, raise the increments (the rate included), and for a norm dose the quantity values without the rate |
| `CalcValues` (`OrderProcessor.fs:798`), when a scenario is selected | the quantity values without the rate, then the rate values at once |
| `ReCalcValues`, `reCalcSteps` (`OrderProcessor.fs:698`), on a reset | the same two stages |
| `SolveOrder`, `solveSteps` (`OrderProcessor.fs:731`), after a pick | the values, the rate included, when the order has none; then the solve |
| `processClearedOrderWith` (`OrderProcessor.fs:424`), after a cleared dose or rate | the values, the rate held back |

So the rate already comes second, but it comes straight after the quantities, while the dose
quantity still has many values. Its rounded list then narrows the quantity.

`Order.hasValues` (`Order.fs:3139`) leaves the rate of a timed order out, with the comment that
"drip rate always has values or is solved". That stops being true with this plan, so the comment
changes. The check itself does not need the rate. Its fallback, when no other variable has
constraints, asks whether the rate is not solved (`Order.fs:3200`); it asks for solved, not for
values, so it stays.

## What the solver does with a range

The approach below depends on one property of the solver: a solve that is not a min and max solve
must leave the rate a range while the dose quantity is still a list. Checked in GenSOLVER and with a
probe.

In the code (`Variable.fs` in GenSOLVER), a range becomes a list in two cases only:

1. **Its minimum equals its maximum** after both are rounded to the increment
   (`Variable.fs:1883`). It then holds one value: solved, not pruned.
2. **The calculated result is a list.** The result narrows the variable to that list (`applyExpr`,
   `Variable.fs:3140`). A result is only a list when both operands are lists (`ValueRange.calc`,
   `Variable.fs:2885`); a list times or divided by a range gives a range.

The time of a timed or once timed order only ever gets a minimum and a maximum from the rule
(`Medication.fs:2030`): no list and no increment. So `[orb]_dos_rte = [orb]_dos_qty /
[ord]_sch_tme` gives a range, also while the dose quantity is a list. The other equations with the
rate multiply it by a component or item concentration; those are lists, but the rate is a range, so
the result is a range too. Only an explicit step makes a list of a range: `minIncrMaxToValues`, or
setting the median value.

The probe, `Scripts/Issue1425Probe.fsx` in GenORDER, run with `dotnet fsi Issue1425Probe.fsx` from
that directory and no define, takes the 9 timed and once timed fixtures of `Issue1425Measure.fsx`
through today's calc-minmax. It then gives the quantities their values with the rate held back,
runs `solveOrder` twice, picks the middle dose quantity, and gives the rate its values:

| fixture | dose quantity after calc-minmax | after the quantity values and two solves | after a dose quantity pick | after the rate values |
|---|---|---|---|---|
| amfo timed | from 22 mL | 34 quantities; rate 40..10..310 mL/uur, a range; time 1 to 2 uur | 164 mL; rate 90..10..160 mL/uur, a range | 8 rates, 90 to 160 mL/uur; 8 times, 1,02 to 1,82 uur |
| amfo with NaCl, two strengths, timed | from 50 mL | 130, 180, 230 and 280 mL; rate 70..10..280 mL/uur, a range | 230 mL; rate 120..10..230 mL/uur, a range | 4 rates, 120 to 210 mL/uur; 4 times, 1,1 to 1,92 uur |
| tpn, tpnComplete and the kalium rate limit binding | from 200 mL, 203 mL with the binding | 121 quantities, 430 to 830 mL; rate 20..5..40 mL/uur, a range; time 20 to 24 uur | 630 mL; rate 30 mL/uur, one value; time 21 uur | as before |
| tpn without KCl | from 200 mL | 20 quantities; rate 15..5..40 mL/uur, a range | 610 mL; rate 30 mL/uur, one value | as before |
| tpn, kalium limit binding | from 500 mL | 91 quantities; rate 25..5..40 mL/uur, a range | 680 mL; rate 30 mL/uur, one value | as before |
| morfine timed with NaCl | 50 mL | 50 mL, one value; rate 2,1..0,1..2,5 mL/uur, a range | no pick | not run |
| kcl once timed | 32 mL | 32 mL, one value; rate 20..5..30 mL/uur, a range | no pick | not run |

So the rate stays a range on every fixture while the dose quantity is a list. After a pick the tpn
rate is one value by the first case: with today's step of 5 mL/uur, 30 is the only rate in the
window of 26,25 to 31,5 mL/uur. With the pump's step of 0,1 mL/uur the rate stays a range there,
and the rate stage makes the list. The morfine and the KCl orders have one dose quantity after
calc-minmax, so their rate stage can run at once.

The probe did not change the rate increment and called the `Order` functions directly, not the
pipelines of `OrderProcessor.fs`. Step 0 covers both, with the same script.

## Approaches considered

1. **Change the rate step only.** The first attempt. Measured: nothing a prescriber sees changes.
   Rejected.
2. **A one-way equation.** Let `[orb]_dos_qty = [orb]_dos_rte * [ord]_sch_tme` pass values only
   from the quantity to the rate and time. The solver has no direction, so this is a change in
   GenSOLVER for one equation. Rejected: too wide for the problem.
3. **The rate waits for the dose quantity.** For a timed and a once timed order:
   - the rate keeps its own increment (0,1 mL/uur, the pump's grid) and is not raised to a rounded
     step. That undoes the rate half of the raise PR #1443 capped: a continuous order is not raised
     and a discontinuous order has no rate, so the rate raise is left without a caller and goes;
   - the rate stays a range, without a value list, until the dose quantity has one value;
   - once the dose quantity has one value, the rate gets its values, and the time follows from the
     rate.

## Chosen approach

Approach 3. It uses the two stages the processor already has, and changes when the second stage
runs, not how it works. The dose dialog already shows a rate without values as one range with its
stepper (`ViewHelpers.ovarValsWithRange`), and since PR #1447 it shows the time range too. So the
client needs no change.

What stays: the min and max solve still bounds the dose quantity by the rate limits times the time
limits. That bound is real: a quantity above the highest rate times the longest time cannot be given
within the time limits. Only the rounding of the rate and its value lists before the quantity is
known are removed.

A side effect: the tpn time of PR #1443, a range of 20 to 24 uur because 121 dose quantities times 5
rates is above the solver's `MAX_CALC_COUNT` of 500, no longer comes from two value lists. With the
rate a range, the time is a range by arithmetic on the bounds, and with one dose quantity and a rate
list it is a list again.

## Decisions

Decided by the maintainer on 2026-10-12, decision 5 after step 0:

1. **The rate once the dose quantity has one value** gets a value list pruned with the rate caps,
   as today but later, so the order still solves to one value without a pick. For tpn, 1330 mL over
   20 to 24 uur, that is a few rounded rates between 55,5 and 66,5 mL/uur.
2. **Once timed orders** follow the same rule as timed orders. `hasTimeNotContinuous` already
   treats them alike in the processor, and the quantity, rate and time relate in the same way.
3. **The component pruning** stays out of this issue. For tpn the pruned component quantities
   alone put the dose quantity at 430 to 830 mL, but they are pruned by their own caps, the same for
   every schedule.
4. **The bound by the rate and time limits** stays in the min and max solve. The issue says the
   time and the rate never narrow the quantity, but without the bound the dialog would offer a
   quantity that cannot be given within the time limits.
5. **The rate gets its median** when its list is first made, when the scenario is selected or
   after a pick, as the norm dose gets the median dose quantity. Measured in step 0: on the grid of
   0,1 mL/uur the window of a dose quantity holds several rates, so without the median no completed
   timed order would solve. A reset or a reopen offers the list to pick from.

The bound is enough to keep the rate and time limits from conflicting with the dose quantity. For
one quantity the time limits allow the rates from the quantity divided by the longest time to the
quantity divided by the shortest time. That window meets the rate limits exactly when the quantity
lies between the lowest rate times the shortest time and the highest rate times the longest time,
which is the bound. So every quantity the dialog offers has a rate within its limits. The one
exception is a window narrower than the pump's grid of 0,1 mL/uur: a very small quantity with tight
time limits may have no rate on the grid. The narrowest window is 1,6 mL/uur for tpn and 11 mL/uur
for amfo timed; step 0 measures it on every fixture.

## Measured in step 0

`Scripts/Issue1425Staged.fsx` holds the prototype: `Order.increaseIncrements` without the rate
raise, and a copy of `OrderProcessor.fs` with the changes of step 1, the median rate of decision 5
included. `Issue1425Load.fsx` loads it in place of `OrderProcessor.fs` under the define `STAGED`.
Run from `src/Informedica.GenORDER.Lib/Scripts`:

```bash
dotnet fsi Issue1425Probe.fsx
dotnet fsi --define:STAGED Issue1425Probe.fsx
dotnet fsi Issue1425Measure.fsx
dotnet fsi --define:STAGED Issue1425Measure.fsx
dotnet fsi Issue1425Compare.fsx
dotnet fsi --define:STAGED Issue1425Compare.fsx
```

The two measure runs ran one after the other, so their timings count. The probe now also runs the
pipelines: `CalcMinMax`, `CalcValues` and `SolveOrder`; a dose quantity pick solved by `SolveOrder`;
every component quantity picked in turn and solved by `SolveOrder`, as the dose dialog completes an
order; and a reset by `ReCalcValues`. The compare script completes an order with `Order.solveOrder`
alone, which skips the rate stage, so under `STAGED` its completed timed orders keep a rate range;
the probe gives the completed numbers.

**What does not move.** The 12 fixtures that are not timed: no variable differs, processed or
completed.

**After `CalcMinMax`.** The dose quantity starts where the limits allow:

| fixture | today | staged |
|---|---|---|
| tpn, tpnComplete | from 200 mL | from 191 mL |
| tpn, kalium rate limit binding | from 203 mL | from 191 mL |
| tpn without KCl | from 200 mL | from 180 mL |
| amfo timed, amfo with NaCl in two strengths timed, tpn with the kalium limit binding | as staged | from 22, 50 and 500 mL |
| kcl once timed | time 64 to 96 min | time 60 to 120 min |

The once timed KCl shows that the raise narrowed the time too: the rate of 20 to 30 mL/uur by 5 left
64 to 96 minutes for 32 mL, where the time limits allow 60 to 120.

**Processed**, after `CalcValues` and `SolveOrder`. On every timed fixture with a list of dose
quantities the rate is a range and the time is 20 to 24 uur or 1 to 2 uur:

| fixture | dose quantities today | staged | rate today | staged |
|---|---|---|---|---|
| tpn, tpnComplete, kalium rate limit binding | 121, from 430 mL | 136, from 380 mL | 20 to 40 by 5 | 15,9 to 41,5 mL/uur |
| tpn without KCl | 12, from 410 mL | 20, from 360 mL | 20, 30, 40 | 15 to 41 mL/uur |
| tpn, kalium limit binding | 91, from 530 mL | the same | 25 to 40 by 5 | 22,1 to 41,5 mL/uur |
| amfo timed | 34, from 61 mL | the same | 14 values, 40 to 300 | 30,5 to 317 mL/uur |
| amfo with NaCl, two strengths, timed | 130 to 280 mL | the same | 100 to 250 by 50 | 65 to 280 mL/uur |
| morfine timed with NaCl | 50 mL | the same | 2,1 to 2,5 by 0,1, to pick | 2,3 mL/uur, the median |
| kcl once timed | 32 mL | the same | 20, 25, 30, to pick | 24 mL/uur, the median |

So the dose quantity of tpn now goes down to 380 mL: the component pruning gives it more values once
the rounded rate no longer bounds it. The morfine and the KCl orders have one dose quantity from the
start, so their rate gets its list at once, and the median of it; today the prescriber picks it.

**After a dose quantity pick**, solved by `SolveOrder`, the rate gets its list on the pump's grid
and the median of it: tpn at 588 mL gets 27 mL/uur of 25 to 29, amfo timed at 164 mL 130 mL/uur of
90 to 160, the amfo with NaCl in two strengths at 230 mL 180 mL/uur of 120 to 210. Each rate puts
the time within its limits.

**Completed**, every component quantity picked in turn and solved by `SolveOrder`:

| fixture | today | staged |
|---|---|---|
| tpn, tpnComplete | 530 mL at 25 mL/uur | 380 mL at 18 mL/uur |
| tpn, kalium rate limit binding | 530 mL at 25 mL/uur | 380 mL at 17 mL/uur |
| tpn, kalium limit binding | 530 mL at 25 mL/uur | the same |
| tpn without KCl | 660 mL at 30 mL/uur | 360 mL at 17 mL/uur |
| amfo with NaCl, two strengths, timed | 130 mL at 100 mL/uur | the same |
| amfo timed | 61 mL, the rate 40 or 60 to pick | 61 mL at 50 mL/uur |
| morfine timed with NaCl | 50 mL, 5 rates to pick | 50 mL at 2,3 mL/uur |
| kcl once timed | 32 mL, 3 rates to pick | 32 mL at 24 mL/uur |

Today six of the nine timed fixtures solve once their components are picked, because the coarse
rate of 5 or 50 mL/uur leaves one rate in the window. Staged, all nine solve, by the median rate.

**Reset**, a completed order through `ReCalcValues`. A rate that gets its list there keeps it to
pick from: the morfine offers 2,1 to 2,5 mL/uur. Where the dose quantity is a list again the rate is
a range, as after `CalcValues`. Today every timed fixture offers a rate list after a reset.

**Timings and picks.** The processing is faster on every timed fixture: tpn 593 to 492 ms,
tpnComplete 928 to 754 ms, amfo timed 674 to 432 ms, tpn without KCl 388 to 252 ms. The slowest
pick of tpnComplete goes from 381 to 237 ms. The dialog offers fewer picks, as the rate has no list
before the dose quantity: tpn 15 where 20, morfine 6 where 11. The tpn with the kalium rate limit
binding still fails 1 of its picks; no pick fails that did not fail today.

**The narrowest rate window**, the smallest dose quantity over the shortest time minus over the
longest, against the grid of 0,1 mL/uur: tpn 1,59, tpn without KCl 1,5, tpn with the kalium limit
binding 4,17, amfo timed 11, amfo with NaCl in two strengths 25, the once timed KCl 16, and the
morfine 0,4 mL/uur. Each holds at least four steps of the grid.

**The golden orders** run against the compiled library, not against the script; the compare output
shows the timed ones move, so tpn and tpnComplete are regenerated in step 1.

## Confidence

Medium. The two stages exist and the client already shows a rate range. The risk is in the
pipelines that assume a timed order has rate values after `CalcValues`. `CanSetNormDose`
(`OrderProcessor.fs:581`) asks for rate values for an order with a time; with the rate a range it
never holds for a timed order, so the guard of "ensure-dose-values-1" always passes and the quantity
values are calculated on every `CalcMinMax`. That gives the same result, but costs time each run.
Step 1 drops the rate from `CanSetNormDose` for a timed or once timed order. Another risk is a
pipeline that gives values without holding the rate back: for an order with more than two
components the values come from "ensure-values-1" in `SolveOrder`, not from `CalcValues`. The
reachable pick tests are the last risk. Step 0 measures this before any source change.

## Steps

One PR per step, after the maintainer's go.

0. **Prototype and measure, script only.** `Scripts/Issue1425Staged.fsx` in GenORDER: the changed
   `increaseIncrements` (no rate raise for a timed or once timed order) and the changed pipelines
   (the rate stage only when the dose quantity has one value), loaded by `Issue1425Load.fsx` under
   the define `STAGED`. Run `Issue1425Probe.fsx`, `Issue1425Measure.fsx` and `Issue1425Compare.fsx`
   with and without the define, on the 21 fixtures, and record:
   - the dose quantity after calc-minmax and after the values, per timed fixture (expected: tpn from
     191 mL, amfo timed from 22 mL);
   - the rate and time after a scenario is selected (expected: ranges) and after a dose quantity
     pick (expected: a rate list and a time list);
   - every failing pick, the timings, and the golden orders that move;
   - per timed fixture, the narrowest rate window against the 0,1 mL/uur grid.

   The numbers go into this plan; no source change. `docs(genorder): measure the rate after the
   dose quantity`, with the script; no Greptile review, since it changes only a script and docs.
1. **The rate after the dose quantity, GenORDER.** In `Order.fs`:
   - `increaseIncrements` no longer raises the rate, and loses its `maxRateCount` parameter (two
     callers in `OrderProcessor.fs`). What has no caller left goes: `Order.rateIncrements`, the
     `increaseRateIncrement` functions of `Dose`, `Orderable` and `Order` (`Order.fs:321`, `2133`,
     `3381`), `OrderVariable.Rate.increaseIncrement` (`OrderVariable.fs:2227`), and the event
     `OrderIncreaseRateIncrement` (`Types.fs:853`) with its log line (`OrderLogging.fs:109`).
     `Order.rateSteps` stays: the prune uses it;
   - the `hasValues` comment, its fallback kept.

   In `OrderProcessor.fs`:
   - `CalcValues` and `reCalcSteps` run the rate stage only when the dose quantity has one value;
   - "ensure-values-1" in `solveSteps` holds the rate back for a timed or once timed order, with
     the same `hasTimeNotContinuous` the other pipelines use. For an order with more than two
     components `CalcValues` does nothing (its guard, `OrderProcessor.fs:798`), so for tpn this
     step gives the first values;
   - `solveSteps` gets a rate stage after it, guarded by "the dose quantity has one value and the rate has
     no values", so it does not run on every solve. It covers a pick, and also a cleared rate: a
     `SolveOrder` runs process-cleared, which holds the rate back, and then `solveSteps`;
   - where `CalcValues` and `solveSteps` make the rate list, the rate gets its median and the
     order is solved again; `reCalcSteps` makes the list without it, so a reset or a reopen offers
     the list;
   - `CanSetNormDose` no longer asks for rate values for a timed or once timed order.

   The norm dose sets the median dose quantity (`Order.setMedianDoseValue`, `Order.fs:3441`), so a
   scenario with a norm dose gets its rate list straight away. Tests:
   - after `CalcMinMax`, the tpn dose quantity starts at 191 mL and amfo timed at 22 mL;
   - after `CalcValues`, the rate of tpn and amfo timed is a range and the time is a range;
   - tpn through `CalcMinMax` and `SolveOrder`, without `CalcValues`: the dose quantity is a list
     and the rate is still a range;
   - after a dose quantity pick, the rate has values and every rate puts the time within its
     limits;
   - a completed tpn solves with the median rate, 18 mL/uur at 380 mL;
   - the once timed KCl gets the median rate, 24 mL/uur, when the scenario is selected;
   - a reset of a completed order offers the rate list, the morfine 2,1 to 2,5 mL/uur;
   - a rate cleared after a pick gets its list again;
   - `naclLargeRateText`, its dose quantity having one value from the start: the test of PR #1443
     "pruned to 60 to 480 by 30" becomes "gets the median, 270 mL/uur, of 60 to 480 by 30", and a
     reset offers 50 to 500 mL/uur by 50, as today (measured on the staged script);
   - the test of PR #1443 "a rate of 50 to 500 mL/uur is raised to steps of at most 10 mL/uur"
     becomes "keeps its step of 0,1 mL/uur";
   - the rate stepper commands, to the minimum, the median and the maximum, on a rate that is a
     range before any pick;
   - a continuous order is unchanged (morfCont: the rate list as today);
   - the golden orders that move are regenerated, with what moved in the commit.

   Expected under 100 source lines. `fix(genorder): give the rate after the dose quantity`.

## Left out

- **The component pruning**, by decision 3.
- **A fixed time.** A time limit with the same minimum and maximum is invalid input, checked by
  #1427, not by the calculation.
- **The solver's step and maximum set while a later removed strength still lives** (the amfo with
  NaCl in two strengths, timed), found in #1433 and still open; not caused by the rate.

## Verification

- `dotnet run build` and `dotnet test tests/Informedica.GenORDER.Tests/`
- `dotnet run BenchmarkBuild`
- `dotnet fsi scripts/CheckDependencyRule.fsx`
- `dotnet fantomas --check`
- `Issue1425Probe.fsx`, `Issue1425Measure.fsx` and `Issue1425Compare.fsx` against the final source,
  diffed with the run of step 0 under `STAGED`: no difference
- the maintainer opens tpn and amfo timed in the browser: the rate and time show as ranges until
  the dose quantity is picked, then the rate offers its list
