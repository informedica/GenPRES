# Implementation plan for issue #1495: solve without the adjust equations, and prescribe for an adult without a weight

2026-10-28. This plan does two things.

- **The adjust equations.** An order maps the equations of the adjusted doses only when an adjusted dose
  of the order has a constraint. An adjusted dose is a dose per kg or per m2.
- **An adult without a weight.** An adult can be prescribed for without a weight or a height.
  - A patient of 18 or older is an adult.
  - A patient of 16 or older, or one without an age, can be marked as an adult.
  - The rules such an adult gets grow with what is measured:
    - with nothing measured, only the rules that need no adjustment;
    - with a weight, the per kg rules as well;
    - with a weight and a height, the per m2 rules as well.

The GenFORM change lands first, and the gates that refuse a patient without a weight and a height open
last. At no step can an adult be prescribed a per kg dose without a weight, or a per m2 dose without a
BSA, which takes a weight and a height.

The plan closes #1495; step 2's pull request closes #1535. It refers to #1453 and #1463. Line numbers
are from `master` at `0fa88733`.

## Problem description

**The adjust equations.** Every order maps 25 equations that name an adjusted variable (`_adj`). There
are 57 equation templates in `EquationMapping.fs`. The 25 cover:
- a dose per kg or per m2;
- the adjust quantity of the order, `[ord]_adj_qty`.

When the rules of an order limit no adjusted dose, these equations cost solve time and compute values
that nothing shows. The issue measured this on the test scenarios. For paracetamol suppository for an
adult, leaving the equations out takes the order from 38 to 22 equations and the min-max solve from
7.3 to 4.6 ms. Every non-adjusted variable comes out identical.

**The search.** The adjust quantity also stops the impossible-value search of the #1453 plan. Without a
weight, `[ord]_adj_qty` holds no value, yet `CompleteOrder.freeNames` passes it to the search as an
input. The search cannot list an input without values, so it never finishes and leaves almost every
value undecided.

**An adult without a weight.** Rules are selected only for a patient with a weight and a height. Every
patient with an age gets an estimated weight and height from the normal values, an adult included: an
adult gets the last row of the table. So an adult is never prescribed for on what is measured alone.
Either the weight is estimated, or the patient is refused.

## What the code does today

**The patient.**
- `Patient.applyNormalValues` (Shared `Models.fs:279`) estimates a weight and a height for every age.
  For an adult the estimate is the last row of the table.
- `Patient.getWeight` (`Models.fs:187`) returns the measured weight, or the estimate when no weight was
  measured.

**The gates.** Three places refuse a patient without both a weight and a height, measured or
estimated, before any rule is selected:
- **The server.** `ServerApi.Mappers.Patient.patient` (`:34`) refuses with `noWeightAndHeight`, and
  `parse` (`:197`) does the same.
- **GenORDER.** `OrderContext.getRules` (`:705`) and `OrderContext.refusal` (`:1272`) select rules only
  when both are known.
- **The client.** `PatientMachine` sends `SetPatientData` only with both (`:132`).

When the normal values are not loaded there is no estimate, and the patient is refused.

**Adulthood.**
- The patient has no adult marker. Only the age says whether a patient is an adult:
  - the client tests `Years >= 18` (`PatientText.fs:86`);
  - GenCORE's `toAgeType` gives `Adult` from 18 years.
- In GenFORM, `IsAdult` is a property of a rule's patient category only.
- `Patient.meetsMinimumData` (GenFORM `Patient.fs:751`) asks for an age, or a measured weight and
  height.
- The age check of the patient category (`PatientCategory.filter`) fails a bounded age range when the
  patient has no age. The adult rules have such a range.

**GenFORM.**
- **Adjusting the limits.** `PrescriptionRule.filter` adjusts the dose limits to the patient in its
  "recalculate adjusted dose limits" map (`PrescriptionRule.fs:275`). It does so only when both the
  weight and the height are known; otherwise the limits stay as they are.
- **Pinning.** `adjustDoseLimitToPatient` (`:10`) replaces an adjusted limit with the absolute one only
  when the two do not overlap at all for this patient: when even the lowest adjusted dose is at or
  above the absolute maximum, or the highest is at or below the absolute minimum. The dose is then
  pinned to the absolute limit, and that adjusted `MinMax` is emptied.
- **Overlap.** When the two overlap partly, both stay. `Medication` writes both onto the order
  (`Medication.fs:1322-1340`), and the solver keeps what both allow. For 10 to 20 mg/kg at 30 kg with
  an absolute maximum of 500 mg, the order allows 300 to 500 mg.
- **Renal rules.** After the map, a renal rule can adjust the limits (`PrescriptionRule.fs:316`,
  `RenalRule.adjustDoseLimit`). With an absolute reduction it sets the renal rule's limit wherever
  the original limit is empty (`RenalRule.fs:357`), a per kg limit included. When the renal rule
  gives no limit of a kind, an absolute reduction also replaces the dose rule's own limit of that kind
  with an empty range (#1535). A renal rule that gives only a maximum per day thus leaves the dose per
  administration without a maximum. The same happens per bound: a renal rule that gives only a
  minimum of a kind removes the dose rule's maximum of that kind. When the renal rule gives both
  bounds of a kind, an absolute reduction takes the lower of each pair (`if lim1 < lim2 then lim1 else
  lim2`), so a renal rule can never raise a dose above the dose rule's maximum. A relative reduction
  multiplies, so a factor above 1 already raises the dose.
- **Completion.** `PrescriptionRule.filter` completes each selected dose rule with the product details
  first (`DoseRule.reconstitute`, `DoseRule.fs:305`); the limits of the pharmaceutical form were added
  at load (`addFormLimits`).
- **Solution rules.** `adjustSolutionRuleToPatient` (`:172`) turns the per kg volumes of a solution
  rule into absolute ones, when there is a weight.
- **Missing measurement.** Nothing leaves out a rule because its limits need a weight or a BSA that is
  missing.
- **Patient categories.** The patient category check fails a weight range when the weight is unknown,
  but passes a BSA range when the BSA is unknown (`Patient.fs:177`).
- **Adjust units.** A rule is built from several sheet rows, grouped by source, indication, generic,
  patient and route (`DataGroupKey`, `Types.fs:998`). Each row's dose limit reads its own `AdjustUnit`
  (`DoseRule.getDoseLimits`, `DoseRule.fs:413`), so nothing stops one rule from mixing per kg and per
  m2 limits. Such a rule is calculated wrongly: the order has one adjust quantity, the weight or the
  BSA, chosen from the adjust unit that the rule takes from a single row (`DoseRule.fs:641`,
  `Medication.fs:1121`), so the limits of the other unit are applied in the wrong unit.

**GenORDER.**
- `Medication` sets the adjust quantity to the weight, or to the BSA for a per m2 rule
  (`Medication.fs:1125`). It is `None` without one.
- `getEqsMapping` (`EquationMapping.fs:233`) already leaves out two groups of equations per order:
  - the tie equations of an order with one component;
  - the item concentration products of an order with more than two components, each in one strength.

  It never leaves out the adjust equations.
- Every solve, `Determination.ofOrder` and the logging (`OrderLogging.fs:62`) reach the mapping
  through `Order.equations` (`Order.fs:3521`).
- `OrderContext.fromRule` sets `UseAdjust` from `DoseRule.useAdjust` (`OrderContext.fs:108`). The
  client shows the per kg fields of the dose dialog only when `UseAdjust` is set.
  `DoseLimit.useAdjust` (`DoseLimit.fs:49`) is false as soon as a limit has an absolute minimum and
  maximum. So the per kg fields are hidden even when the per kg limit still sets the lowest dose.

## Decisions

The maintainer decided these on 2026-10-28.

| Subject | Decision |
|---|---|
| Who is an adult | A patient of at least 18 years (`Utils.ValueUnit.ageAdult`, 365 days to the year, `Utils.fs:204`) is an adult, whether marked or not. A patient marked as an adult is an adult when the patient is at least 16 years old or has no age. |
| The adult marker | A separate field on the patient, next to the age, not a change to the age's type. Below 16 years a patient cannot be marked as an adult: the mark is refused, and the adult test ignores it. From the 16th birthday to 18 years the mark is the user's choice. From 18 years it is implied. |
| Where the mark comes from | Only the user sets it, in the patient panel. The hospital record (EHR) never supplies it. It is kept wherever the patient is kept, like the weight. |
| What the mark allows | A patient who is an adult may be prescribed for without a weight or a height, on the rules that need no adjustment. |
| Matching a marked patient | A marked patient without an age, or of 16 or 17, is matched against a rule's patient category as an age of 18 years and over, with or without a weight. A marked patient of 16 or 17 therefore gets the adult rules and no longer the rules for children. Rules for adults and rules without an age range are selected. A rule whose age range ends below 18, or reaches below 18 (for example 12 to 25 years), is not, since the age may fall outside it. Without an age, the gestational and post-menstrual age checks are skipped, as they are for any patient without an age, and the minimum data is met. |
| One adult test | The adult test is written once in GenFORM and once in Shared, with a test that keeps the two in line. The client uses the Shared test, also for the patient text ("Man" or "Jongen"). |
| The estimate | An adult gets no estimated weight or height. A child keeps the estimate and the gate. An adult therefore loses the per kg rules that an estimate gives today, until a weight is entered. The step that stops the estimate counts the rules this affects, and the release notes say it. |
| The rules an adult gets | The absolute rules. With a weight, also the per kg rules. With a weight and a height, also the per m2 rules. |
| A rule with an adjusted and an absolute limit of the same kind | Without the measurement, the rule is used on its absolute limit of that kind: the adjusted limits are emptied. "Of the same kind" means the dose per administration, the dose per time or the rate. |
| A rule that loses a maximum | A rule is not selected when a dose had a maximum, per kg or absolute, in the dose rule as the selection has it after completion, and has no maximum left after every adjustment to the patient and to the kidney function. "A dose" is the dose per administration, the dose per time or the rate; a remaining minimum does not count. Examples without a weight: 10 to 15 mg/kg per dose and at most 4 g per day leaves the dose per administration without a maximum; at most 15 mg/kg and at least 100 mg per dose leaves only a minimum. Neither rule is selected. |
| A rule that mixes per kg and per m2 | Left out when the sheet is loaded, with a message that names the rule and the reason, like the other sheet checks. Such a rule is calculated wrongly today, so nothing that works is lost; a test on today's code shows the wrong result first. The pull request lists the rules in the demo sheet so they can be split into a per kg and a per m2 rule, and its changelog says such rules are refused. |
| An absolute renal reduction | A renal rule is selected by an eGFR range, and may describe a reduced kidney function or hyper clearance, which needs higher doses; "reduction" in the code (`DoseReduction`) and in this plan covers both. A relative reduction with a factor above 1 already raises the dose, and is unchanged. The renal rule's bounds win, and the dose rule fills in what the renal rule does not give. Per kind of limit and per bound: a bound the renal rule gives replaces the dose rule's bound; a bound the renal rule does not give, in a kind it limits, is kept from the dose rule and moved to the renal bound when the two conflict; a kind the renal rule does not limit keeps the dose rule's maximum and drops its minimum, as today, since no renal bound of that kind can make the minimum fit. A renal range wider than the dose rule's therefore widens the dose. A renal rule for a reduced kidney function that does so is almost certainly a sheet error, so the load warns about it (next row). With a renal minimum only, the dose rule's maximum is raised to that minimum, so a dose never loses its maximum and the rule stays selectable; a hyper-clearance rule that wants a range gives both bounds. Examples: 2 to 4 g per day with a renal maximum of 1 g gives exactly 1 g; 0.5 to 1 g per day with a renal minimum of 2 g gives exactly 2 g; 0.5 to 1 g per day with a renal range of 2 to 3 g gives 2 to 3 g. Renal limits are always inclusive (`RenalRule.fs:269` to `:276`), so a replaced or moved bound never leaves an empty range. A step of its own (#1535), before the rule selection changes; it does not depend on step 1 and can land first, or alone. |
| A renal rule that raises a reduced dose | When the renal rules are matched to their dose rules, a renal rule whose eGFR range stays below 90 mL/min (it has a maximum eGFR of at most 90), or that applies to dialysis, and whose maximum of a kind lies above the dose rule's maximum of that kind, is reported with a message that names both rules. The rule is still used as the sheet says. |
| A line forced by a new case | A new case of a union type forces a new line in each match over it. When that line sits in a project the step's script cannot load, it is written directly, without a script, and named in the pull request, as the call-site changes of a deletion are. The maintainer's exception to the script-first policy, for steps 5 and 6. |
| Renal rules | After a renal rule has adjusted the limits, per kg and per m2 limits are removed again when the measurement is missing. A rule then left with a dose without a maximum is not selected, as above. |
| Weight and BSA ranges | A rule restricted to a weight range is not selected for a patient without a weight, as today. A rule restricted to a BSA range is not selected for a patient without a BSA; today it is. |
| A solution rule that bounds the preparation per kg only | Not selected without a weight, because the preparation would be unbounded (#1463). |
| A per kg and an absolute limit that overlap | Both stay, and the order allows what both allow, as today. The dose dialog shows the per kg fields, and the printed order the per kg doses, whenever the order still has a per kg (or per m2) limit, decided by the same test that keeps the adjust equations. |
| A per kg command on an order without per kg limits | Refused with a refusal of its own, which says that the order has no per kg dose. The callers of the older `OrderContext.evaluate` get the context as sent, as they do for "not solved". |
| The order of the steps | GenFORM first, then GenORDER, then the gates. Step 2, the renal fix, stands on its own. |

## Approaches considered

- **The adjust equations, per order or per variable.**
  - Per variable would leave out the equations of an adjusted dose without a limit even when another
    adjusted dose has one. That adds a case analysis to the mapping that the measurements do not ask
    for.
  - Per order is simpler, and the dose dialog's per kg fields follow the same test.
- **Where to select the equations.** Two places were looked at:
  - in `Order.solve`;
  - in `getEqsMapping`.

  `Order.solve` is not the only caller: `increaseIncrements`, `solveNormDose`, `Determination.ofOrder`
  and the logging build their own mapping. `getEqsMapping` is the one place they all reach.
- **Which test.** Two tests were looked at:
  - test the order on its constraints;
  - pass in GenFORM's `DoseRule.useAdjust`.

  The order's constraints need no new dependency. They also hold after `adjustDoseLimitToPatient` has
  pinned an adjusted limit, and when an absolute limit sits beside an adjusted one. `useAdjust` sees
  neither.
- **An adult without a weight.** Three ways were looked at:
  - keep the refusal and only drop the equations;
  - let the adult through but keep estimating;
  - let the adult through without an estimate.

  The maintainer chose the third, so that an adult is prescribed for on what is measured.
- **The adult marker.** Two ways were looked at:
  - the age as a choice between a known age and "adult";
  - a separate mark next to the age.

  The maintainer chose the separate mark, with its own lower limit of 16 years.

## Chosen approach

**Step 1, the adult marker.** GenFORM's patient gets the marker, the adult test, and the matching of a
marked patient against a rule's patient category. Nothing in the app sets the marker yet.

**Step 2, the renal reduction.** Under an absolute renal reduction, for a reduced kidney function as
for hyper clearance, the renal rule's bounds replace the dose rule's, and the dose rule fills in what
the renal rule does not give (#1535).

**Step 3, GenFORM.** GenFORM decides per dose limit what applies to the patient.
- A limit whose measurement is known is adjusted as today.
- A limit whose measurement is missing loses its adjusted limits, also after a renal rule has adjusted
  it.
- A rule is not selected in three cases:
  - a dose that had a maximum in the dose rule after completion has none left;
  - a solution rule bounds the preparation per kg only;
  - the rule's patient category has a BSA range and the BSA is unknown.
- A rule that mixes per kg and per m2 limits is left out when the sheet is loaded, with a message.

The guard in `PrescriptionRule.filter` that needs both a weight and a height goes. For a patient with a
weight and a height the result is the same as on `master` after step 2, apart from the rules that mix
per kg and per m2.

**Steps 4 and 5, GenORDER.** After step 3, an order has an adjusted constraint only when its patient has
the measurement and the rule binds an adjusted limit.
- Step 4 leaves out the adjust equations of every other order. The search then no longer counts the
  adjust quantity as an input.
- Step 5 refuses, with a refusal of its own, the commands that would set or recalculate an adjusted
  dose on such an order. It also lets the dose dialog's per kg fields and the print follow the order.

**Step 6, the refusal on the client.** The new refusal is carried over the wire and shown by the
client.

**Steps 7 and 8, the gates.** These steps carry the adult marker on the wire, in the stored patient and
in the patient panel. They open the gates for an adult, and stop the estimate for an adult, on the
server, in GenORDER, in Shared and in the client.

## Checked, no change needed

- `Nutrition.fs:10` applies the constraints of the adjust quantity. TPN rules limit mL/kg, so a TPN order
  keeps its adjust equations.
- `Totals.fs` reads no adjusted variable; it divides by the weight itself.
- `Order.isSolved` ignores the variables without constraints, so an adjusted variable left open does not
  make an order unsolved.

## Confidence

- **High for step 1.** A new field and a pure test, with no effect until the gates open.
- **Medium for step 2.** A small change to one function, on one model: the renal bound replaces, the
  dose bound fills in. The count of the demo renal rules whose maximum lies above the dose rule's shows
  how much changes.
- **Medium for step 3.** The pull request counts the demo rules each case changes. The change is a law:
  with a weight and a height, the result equals that of `master` after step 2, apart from the rules
  that mix per kg and per m2 and are no longer loaded.
- **High for step 4.** The issue measured the result on nine scenarios: every non-adjusted variable is
  identical with and without the adjust equations, when the order has no adjusted limit.
- **Medium for steps 5 and 6.** The refusal is new, and the per kg fields and the print change for the
  rules whose per kg and absolute limits overlap. The tests and the browser check decide.
- **Medium for steps 7 and 8.** The gates sit in three places. The client's patient panel has to show an
  adult without an estimate and offer the adult switch. The client tests and the browser check decide.

## Steps

The work is done in one pull request per step, after the maintainer's go.
- **Script first.** Every `.fs` change is prototyped in a script with its Expecto tests, reviewed by
  the maintainer, and migrated when asked.
- **Size.** At most 200 lines of shipped code under `src/` per step.
- **The script goes.** The pull request that completes a step's migration deletes its script.

0. **This plan.** `docs: the plan for #1495`.

1. **GenFORM: the adult marker.**
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1495AdultMarker.fsx`.
   - **The field.** `MarkedAdult: bool` on the GenFORM `Patient` (`Types.fs`, next to
     `WeightMeasured`).
     - False in the empty `Patient.patient` (`Patient.fs:596`) and in `Patient.ofEhr`.
     - False in the Dto, so an existing Dto without the field reads as not marked.
     - Every other place that builds the patient in full fills in false: `matchedPatient`
       (`OrderContext.fs:682`), GenORDER's `Patient.fs:28`, the server's patient mapper, and the test
       files that build one.
   - **The test.** `Patient.isAdult` is true when the age is at least `ageAdult`, or when the patient
     is marked and is at least 16 years old or has no age. Below 16 years it ignores the mark and
     answers false.
     - It goes with the patient's own functions in `Patient.fs`, next to `calcBSA` (`:628`).
     - `getAge` at `:105` belongs to the patient category, not to the patient.
   - **The checks.** `Patient.validate` and `Patient.Dto.fromDto` refuse the mark below 16 years, with
     a new `PatientError` case. `meetsMinimumData` accepts a marked patient without an age.
   - **The patient category.** In `PatientCategory.filter` and `PatientCategory.filterPatient`, a
     marked patient without an age, or of 16 or 17, is matched as every age from `ageAdult` up, with
     or without a weight. The age check passes only when the rule's age range holds every age from 18
     years up.
   - **Tests:**
     - `isAdult` for six patients, each age built in days, 365 to the year, as `ageAdult` is:
       - 18 years without the mark: an adult;
       - 17 years with the mark: an adult;
       - no age with the mark: an adult;
       - 17 years without the mark: not an adult;
       - exactly 16 years with the mark: an adult;
       - 16 years less one day (5,839 days) with the mark: refused by `validate` and by `fromDto`, and
         not an adult;
     - a marked patient without an age, and one of 17, matches an adult rule, an all-age rule and a
       rule whose age range starts at exactly 18 years, and does not match a rule of 12 to 25 years;
     - a marked patient of 17 with a weight matches the adult rules and not a rule for children of 1
       to 18 years;
     - an unmarked patient matches every rule as today, as a law over the demo rules.
   - `feat(genform): mark a patient as an adult`.

2. **GenFORM: under an absolute renal reduction the renal bounds win (#1535).**
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1535RenalReduction.fsx`.
   - **The change.** In `RenalRule.adjustDoseLimit` (`RenalRule.fs:343`), the `Absolute` case of
     `adjustMinMax` (`:357`), for each kind of limit:
     - the renal rule gives no range of the kind: the dose rule's maximum stays, its minimum is
       dropped, as today;
     - the renal rule gives a range without a maximum: the renal minimum replaces the dose rule's
       minimum; the dose rule's maximum stays, as the `Relative` case falls back
       (`| _ -> mmOrig.Max`), raised to the renal minimum when it lies below it;
     - the renal rule gives a range without a minimum: the renal maximum replaces the dose rule's
       maximum; the dose rule's minimum stays, lowered to the renal maximum when it lies above it;
     - the renal rule gives both bounds: they replace the dose rule's bounds. Today the lower of each
       pair is taken, which ignores a hyper-clearance rule.
   - **Tests:**
     - a dose rule with at most 500 mg per administration and 2 g per day, and a renal rule with an
       absolute reduction to at most 1 g per day: the order keeps at most 500 mg per administration;
     - a renal rule with only a minimum per day keeps the dose rule's maximum per day, when the minimum
       lies below it;
     - 2 to 4 g per day with a renal maximum of 1 g per day gives exactly 1 g per day, not an empty
       range; 0.5 to 4 g per day with the same renal maximum gives 0.5 to 1 g per day;
     - at least 500 mg per administration, 4 times a day, with a renal maximum of 1 g per day only:
       the minimum per administration is dropped, so the order can still be solved;
     - 0.5 to 1 g per day with a renal minimum of 2 g per day gives exactly 2 g per day, not an empty
       range;
     - a reduction with both bounds: 2 to 4 g per day with a renal range of 0.5 to 1 g gives 0.5 to
       1 g;
     - hyper clearance with both bounds: 0.5 to 1 g per day with a renal range of 2 to 3 g gives 2 to
       3 g;
     - a renal range wider than the dose rule's: 1 to 2 g per day with 0.5 to 3 g gives 0.5 to 3 g;
     - a guard on the renal rule parser: it reads every renal limit with `fromTupleInclIncl`, so a
       replaced or moved bound never leaves an empty range; the test fails when that changes;
     - the relative and no-reduction cases are unchanged.

     The pull request counts the demo renal rules whose result changes:
     - those that lose a maximum today, by a whole kind or by one bound;
     - those whose kept bound is moved to a renal bound;
     - those with both bounds whose renal maximum lies above the dose rule's maximum, which now widen
       or raise the dose;
     - those the new warning reports.
   - The commit has a `=== changelog ===` block: under an absolute renal reduction the renal rule's
     bounds replace the dose rule's, also when they are higher, as for hyper clearance; the dose
     rule's other bounds stay, moved to the renal bound when they conflict.
   - **The warning.** Where the renal rules are matched to their dose rules, a renal rule whose eGFR
     range stays below 90 mL/min, or that applies to dialysis, and whose maximum of a kind lies above
     the dose rule's maximum of that kind, is reported with a message that names both rules. The rule
     is still used. Test: a dose rule of 1 to 2 g per day and a renal rule for an eGFR of 10 to 30
     mL/min with 0.5 to 3 g per day gives 0.5 to 3 g per day and the message; a renal rule for an eGFR
     of 130 mL/min and up with the same range gives no message.
   - `fix(genform): let absolute renal bounds win`.

3. **GenFORM: the dose limits of a patient without the measurement.**
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1495AdjustedLimits.fsx`.
   - **New functions:**
     - `DoseLimit.withoutAdjust`, next to `DoseLimit.useAdjust` (`DoseLimit.fs:49`). It empties
       `QuantityAdjust`, `PerTimeAdjust` and `RateAdjust`.
     - `DoseLimit.losesMaximum`, which takes the limit of the completed dose rule and the limit after
       every adjustment. It is true when a dose (per administration, per time or rate) had a maximum, per kg
       or absolute, in the first and has no maximum in the second.
     - `PrescriptionRule.limitToPatient`, which decides for one limit:
       - a limit without an `AdjustUnit` stays as it is;
       - when the patient has the measurement, `adjustDoseLimitToPatient` applies: a weight for a
         per kg limit, a BSA from `Patient.calcBSA` for a per m2 limit;
       - otherwise `withoutAdjust` applies.
   - **In the "recalculate adjusted dose limits" map**, `limitToPatient` replaces the guard that needs
     a weight and a height. It runs on every `ComponentLimits` `Limit` and every `SubstanceLimits`
     entry. The component selection stays.
   - **After the renal step** (`PrescriptionRule.fs:316`), `withoutAdjust` runs again on every limit
     whose measurement is missing, because an absolute renal reduction can set a per kg limit where
     the dose rule's own was emptied.
   - **After that**, a prescription rule is left out in two cases:
     - `losesMaximum` holds for one of its limits, compared with the limit with the same
       `DoseLimitTarget` in the dose rule as `filter` has it after `DoseRule.reconstitute`, before the
       "recalculate adjusted dose limits" map. The target is the orderable, a component or a
       substance. A limit that the component selection removed (`PrescriptionRule.fs:283` to `:293`)
       is not compared, and does not count as a lost maximum. When a renal rule turns one
       prescription rule into several, each is compared with the same completed rule;
     - there is no weight, and one of its solution rules bounds the preparation per kg only: a
       `VolumeAdjust` without a `Volume`, or a solution limit's `QuantityAdj` without a `Quantity`.
   - **Mixed adjust units.** When the dose rules are built from the sheet rows, a rule whose rows mix
     kg and m2 is left out, with a message that names the rule and the reason.
   - **The BSA range.** The patient category check (`PatientCategory.filter`, `Patient.fs:177`, and
     the same check in `filterPatient` at `:234`) fails a BSA range when the BSA is unknown, as the
     weight check does.
   - **A child.** In the app, a child without both measurements never reaches rule selection; the
     gates of steps 7 and 8 stop the child. From a script, a test or MCP, `limitToPatient` treats a
     child without a weight as it treats an adult. That is the safe direction: a per kg limit is never
     applied without a weight, and a per m2 limit never without a BSA.
   - **Tests:**
     - an absolute rule is unchanged;
     - a rule with a per kg and an absolute limit of the same kind, without a weight, keeps its
       absolute limit and is still selected; so does a rule with a per m2 and an absolute limit,
       without a BSA;
     - a rule of 10 to 15 mg/kg per dose and at most 4 g per day is not selected without a weight;
     - a rule of at most 15 mg/kg and at least 100 mg per dose is not selected without a weight;
     - first, on today's code: a rule whose rows mix kg and m2, at a known weight and height, applies
       its per m2 limit against the weight, which shows the wrong result;
     - a rule whose rows mix kg and m2 is left out at load, with its message;
     - a rule with a component selection: the limits of the components left out do not count as lost;
     - a per kg rule without a weight and a per m2 rule without a height are not selected;
     - a renal rule with an absolute reduction and a per kg limit does not bring a per kg limit back
       for a patient without a weight;
     - a rule with a BSA range is not selected without a height;
     - a solution rule with per kg volumes only is not selected without a weight. With an absolute
       volume beside them it is selected, bounded by the absolute volume;
     - with a weight, an adjusted target that the absolute limit pins leaves every adjusted `MinMax`
       empty;
     - a law over the demo rules: with a weight and a height the result is identical to that of
       `master` after step 2, apart from the rules that mix kg and m2.

     The pull request counts the demo rules per case, and lists the rules that mix kg and m2.
   - The commit has a `=== changelog ===` block: a rule that mixes per kg and per m2 limits is refused
     when the sheet is loaded.
   - `feat(genform): select rules for a patient without weight`.

4. **GenORDER: map the adjust equations only when an adjusted variable is constrained.**
   Script: `src/Informedica.GenORDER.Lib/Scripts/Issue1495AdjustEquations.fsx`. It shadows
   `EquationMapping`, `Order.equationMapping`, `Order.equations` and `Order.solve`, and the callers in
   `CompleteOrder` that the tests need.
   - **The test.** `EquationMapping.usesAdjust` is true when an adjusted dose variable has
     constraints. It looks at `QuantityAdjust`, `PerTimeAdjust`, `RateAdjust` and `TotalAdjust` of the
     orderable, the component and the item doses. `Order.fs:3216` makes the same test for one of them.
   - **The filter.** In `getEqsMapping`, a third filter beside the two of today leaves out every
     equation that contains `_adj` when `usesAdjust` is false. The mapping in `OrderLogging.fs:62`
     already goes through `getEqsMapping`.
   - **The search.** `CompleteOrder.freeNames` (`OrderProcessor.fs:367`) keeps only the names that
     occur in the order's equations, from `Determination.ofOrder`.
   - **Tests**, in `OrderLawsTests.fs`:
     - for each dose type, the filter leaves out exactly the templates of that dose type that name an
       adjusted variable; for paracetamol suppository, for example, the order's equations go from 38
       to 22;
     - a law over the golden fixtures with an absolute rule: at the min-max, values and final stage,
       every non-adjusted variable equals the result with all equations;
     - the per kg fixtures keep their equations: pcmSupp, cotrim, amfo, morfCont and tpn;
     - "without a weight only the values a solve refutes are dropped" (`:390`) becomes "an absolute
       rule without a weight". It uses the hydrocortisone fixture (`Scenarios.fs:2106`) with an
       absolute limit of 27 to 30 mg in place of 2 mg/kg, and checks three things:
       - no adjust equation is left;
       - the adjust quantity is not an input of the search;
       - the search finishes.
   - **Golden orders.** The `GoldenOrders.fs` entries whose adjusted variables become open ranges are
     updated and listed in the pull request.
   - The pull request reports the solve time per fixture, before and after.
   - `perf(genorder): solve without the adjust equations when unused`.

5. **GenORDER: the per kg commands, fields and print follow the order.**
   Script: `src/Informedica.GenORDER.Lib/Scripts/Issue1495AdjustCommands.fsx`.
   - **The commands.** When `usesAdjust` is false, eight commands are refused:
     - `SetNthComponentDoseQuantityAdjust`, `SetNthItemDoseQuantityAdjust`,
       `SetNthItemDosePerTimeAdjust` and `SetNthItemDoseRateAdjust`;
     - `ReCalcComponentDoseQuantityAdjust`, `ReCalcItemDoseQuantityAdjust`,
       `ReCalcItemDosePerTimeAdjust` and `ReCalcItemDoseRateAdjust`.
   - **The refusal.** A new case, `Refusal.NoAdjustedDose`, in GenORDER (`Types.fs:909`): the order
     has no per kg or per m2 dose. The script checks that each command really ends in this refusal,
     with the context as sent. It does not go through `NoRecipe` or the general `NotSolved`. Until
     step 6, the server maps it to the general `NotSolved` on the wire. The new case forces this
     one match line in `ServerApi.Services.fs:335`, which no GenORDER script can load; it is written
     directly and named in the pull request, under the decision "A line forced by a new case".
   - **The per kg fields and the print.** `UseAdjust` is set from `usesAdjust` on the order of the
     context, not from `DoseRule.useAdjust` (`OrderContext.fs:108`). The dose dialog then shows the
     per kg fields whenever the order still has a per kg limit, also beside an absolute one. The
     printed order (`OrderContext.setOrderTableFormat`, `:86`) reads the same setting, so it prints
     the per kg doses in the same cases.
   - **Tests:**
     - each command is refused with `NoAdjustedDose` on an absolute-rule order, and works as today on
       a per kg order;
     - `UseAdjust` is set for a rule with a per kg and an absolute limit that overlap, and is not set
       for an absolute rule;
     - the printed order of the overlapping rule shows the per kg dose;
     - `OrderContext.evaluate` (`OrderContext.fs:1531`) answers `NoAdjustedDose` with the context as
       sent, as it does `NotSolved`, for its callers: the MCP host and the older paths.
   - The commit has a `=== changelog ===` block: the dose dialog shows the per kg fields, and the
     printed order the per kg doses, also for a rule whose per kg and absolute limits overlap.
   - `fix(genorder): per kg commands and fields follow the order`.

6. **The refusal on the client.**
   `Client.Core` goes through the script
   `src/Informedica.GenPRES.Client.Core/Scripts/Issue1495AdjustRefusal.fsx`.
   - **The wire.** Shared `OrderContextRefusal` (`Types.fs:874`) gets the new case, and the server
     maps `Refusal.NoAdjustedDose` to it (`ServerApi.Services.fs:335`). That match line is forced by
     the new case and written directly, under the same decision as in step 5.
   - **Client.Core.** `OrderContextRefusalPolicy` shows the refusal, and the trail names it
     (`Trail.fs:306`).
   - **The text.** A new `Terms` case, its entry in `localization.tsv`, and the Localization sheet.
     `CheckLocalization.fsx` passes.
   - **Tests.** The policy shows the new refusal's text; the trail writes its name; the server maps
     the GenORDER case to the wire case.
   - `feat(api): say why a per kg dose command is refused`.

7. **GenORDER, Shared and the server: open the gate for an adult.**
   Script: `src/Informedica.GenPRES.Server/Scripts/Issue1495AdultGate.fsx`. Whether this step is split
   into GenORDER, and Shared with the server, is decided once the script shows its size.
   - **GenORDER.**
     - `getRules` and `refusal` select rules for a patient with a weight and a height, and for an
       adult.
     - `matchedPatient` takes the weight and the height as options, and carries the mark.
     - A child without both is still refused.
   - **The server.**
     - `patient` and `parse` let an adult through without a weight or a height.
     - The mapper carries the mark into the GenFORM patient and refuses it below 16 years.
     - `estimate` (`:279`) and the estimating patient data port (`:226`) leave an adult as entered.
     - The EHR port never sets the mark.
   - **Shared.**
     - The Shared `Patient` (`Types.fs:82`) gets `MarkedAdult: bool`. It is kept wherever the
       patient is kept, as the weight is; the script lists those places.
     - `Patient.applyNormalValues` gives no estimate for an adult.
     - The adult test is added once to the Shared `Patient` module, with the same rule as GenFORM's
       `Patient.isAdult`. It reads the age in days, 365 to the year, as `Utils.ValueUnit.ageAdult`
       does.
   - **Client.Core.** The patient editing (`PatientEdit`) builds the Shared patient, so it fills in
     "not marked". This small change goes through the step's script as well.
   - **Tests:**
     - an adult without a weight gets the absolute rules, and no per kg or per m2 rule;
     - an adult with a weight and no height gets the absolute and per kg rules, and no per m2 rule;
     - an adult with a weight and a height gets the absolute, per kg and per m2 rules;
     - a child without a weight is refused;
     - a patient of 17 marked as an adult, without a weight, gets the adult and all-age rules that
       need no adjustment;
     - a patient of 17 marked as an adult, with a weight, gets the adult rules, per kg ones included,
       and no rules for children;
     - a patient marked as an adult without an age gets the adult and all-age rules;
     - the mark at 16 years less one day (5,839 days) is refused by the server, and at exactly 16
       years accepted;
     - an adult gets no estimate, and a child does;
     - the Shared and GenFORM adult tests agree for every age from 0 to 20 years, with and without
       the mark, around the 16th and the 18th birthday to the day.
   - **The pull request** counts the demo rules an adult without a measured weight loses compared with
     today. Its commit has a `=== changelog ===` block saying that an adult needs a measured weight
     for a per kg dose.
   - `feat(server): prescribe for an adult without weight or height`.

8. **The client: an adult without a weight or a height.**
   `Client.Core` goes through the script
   `src/Informedica.GenPRES.Client.Core/Scripts/Issue1495AdultPatient.fsx`; the Fable views are edited
   directly.
   - **`Client.Core`.**
     - `PatientEdit` gets a setter for the mark. The mark is cleared when the age changes to below 16
       years.
     - `PatientMachine` sends the patient of an adult without a weight or a height.
     - `PatientReadinessPolicy.missingOf` (`:83`) and `OrderContextRefusalPolicy.patientIncomplete`
       (`:71`) name nothing missing for an adult.
     - `Client.estimated` (`Client.fs:253`) gives an adult no estimate.
     - `PatientText` (`:86`) uses the Shared adult test in place of its own `Years >= 18`.
   - **The views.** The patient panel gets an adult switch:
     - shown checked and fixed from 18 years;
     - free from 16 to 18 years, and when there is no age;
     - not offered below 16 years.

     The panel shows an adult without an estimate.
   - **Tests:**
     - an adult without a weight is ready, and a child without a weight is not;
     - the mark goes when the age drops below 16;
     - a marked patient of 17 is written as "Man" or "Vrouw".
   - `feat(client): prescribe for an adult without weight or height`.

## Left out

- The emergency and continuous lists need a weight (Shared `Models.fs`). For an adult without a weight
  they stay empty, as they do today for every patient without a weight.
- #1500, undoing a product strength pick without a reset, has its own plan.
- A renal rule's norm doses are read from the sheet but never applied to the dose rule; #1536.

## Verification

- **Each script.** Run it from its own directory with `dotnet fsi`, or through the FSI MCP server.
  Every test passes.
- **The baseline.** Before step 1, run the full suite with `CI=true` in a worktree on `0fa88733`. This
  gives the new baseline, since the one of the #1453 plan is from before #1531.
- **After each migration:**
  - `dotnet run build` and `dotnet run servertests` pass;
  - with `CI=true` in a worktree, no test turns red or ignored compared with that baseline;
  - `dotnet fantomas --check` passes;
  - `dotnet fsi scripts/CheckDependencyRule.fsx` passes.
- **The refusal of step 6** cannot be seen in the browser, since the client never sends a per kg
  command for an order without per kg limits. The tests of steps 5 and 6 carry that check.
- **In the browser, after step 8.** The maintainer checks these cases:
  - **An adult of 40 without a weight or a height.**
    - No estimate is shown.
    - Paracetamol suppository is offered on its absolute limits, without the per kg fields.
    - A rule with per kg limits only is not offered.
    - The search of a recalculation finishes.
  - **The same adult with a weight and no height.** The absolute and the per kg rules are offered. A
    rule with per m2 limits only, or with a BSA range, is not.
  - **The same adult with a weight and a height.** The per m2 rules are offered as well.
  - **A rule with a per kg and an absolute limit that overlap.** With a weight, the dose dialog shows
    the per kg fields, and the printed order shows the per kg dose.
  - **A patient of 17 without a weight.** Not marked: estimated as a child. Marked as an adult: no
    estimate, and the adult rules that need no adjustment.
  - **A patient without an age, marked as an adult.** The adult and all-age rules are offered.
  - **A patient of 17 with a weight, marked as an adult.** The adult rules are offered, not the rules
    for children.
  - **The mark is kept.** Mark a patient of 17 as an adult, reload the page: the mark is still set.
  - **A patient of 15.** The adult switch is not offered.
  - **A child.** Without a weight, the child is estimated as today, and refused when there are no
    normal values. With a weight, on a mg/kg rule, nothing changes.
