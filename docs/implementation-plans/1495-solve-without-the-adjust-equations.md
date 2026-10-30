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
  - A rule that needs a measurement the patient lacks is not used at all, not even on its absolute
    limits.
- **A child without a height.** A child still needs a weight, but a child with a weight and no height
  gets the rules that need no BSA. A child lacks a height only when the normal values give none for
  the child's age, or when the user clears the height.

The rules decide what can be prescribed. GenFORM selects the rules as it does today, so it stays usable
as a formulary: a lookup without a patient returns every dose rule of the generic, and patient data
narrows them down. A single check, whether a selected rule can be prescribed for the patient, keeps out
every rule with a per kg or per m2 bound whose measurement the patient lacks. GenORDER applies it
before the rules become pick lists and scenarios. A dose rule whose rows do not share one adjust unit,
or that has a per kg or per m2 value without an adjust unit, and a renal rule that mixes per kg and per
m2, are refused when the sheet is loaded; such a rule is gone from a lookup as well. A user who picks a
rule that cannot be prescribed is told which measurement the rule needs, and the client says which
rules a weight or a height would add.

The GenFORM changes land first, and the gates that refuse a patient without a weight and a height open
last. At no step can an adult be prescribed a per kg dose without a weight, or a per m2 dose without a
BSA, which takes a weight and a height.

The plan closes #1495; step 2's pull request closed #1535, and step 3d closes #1550. The plan refers
to #1453, #1463, #1545 and #1546. Line numbers are from `master` at `34c4eb7a`. Steps 1 and 2 are
done: #1538 and #1541.

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
  `parse` (`:199`) does the same.
- **GenORDER.** `OrderContext.getRules` (`:706`) and `OrderContext.refusal` (`:1273`) select rules only
  when both are known.
- **The client.** `PatientMachine` sends `SetPatientData` only with both (`:132`).

When the normal values are not loaded there is no estimate, and the patient is refused.

**Adulthood.**

- Since step 1, GenFORM's patient has the adult mark (`MarkedAdult`, GenFORM `Types.fs:361`) and the
  adult test `Adult.isAdult`. Nothing in the app sets the mark yet: the Shared patient has no mark,
  and outside GenFORM only the age says whether a patient is an adult:
  - the client tests `Years >= 18` (`PatientText.fs:86`);
  - GenCORE's `toAgeType` gives `Adult` from 18 years.
- In a GenFORM rule, `IsAdult` is a property of the rule's patient category.
- `Patient.meetsMinimumData` (GenFORM `Patient.fs:836`) asks for the adult mark, an age, or a measured
  weight and height.
- The age check of the patient category (`PatientCategory.filter`) fails a bounded age range when the
  patient has no age. The adult rules have such a range.

**GenFORM.**

- **Adjusting the limits.** `PrescriptionRule.filter` adjusts the dose limits to the patient in its
  "recalculate adjusted dose limits" map (`PrescriptionRule.fs:270`). It does so only when both the
  weight and the height are known; otherwise the limits stay as they are.
- **The component selection.** The filter's `Components`, the components the user picked, are applied
  inside the same map. Without a weight and a height the pick is therefore ignored, and every component
  comes back.
- **The adjust value.** `adjustDoseLimitToPatient` tests the limit's `AdjustUnit`, not its bounds, and
  takes the weight or BSA with `Option.get`. The guard in the map admits only a patient with both a
  weight and a height, so this cannot throw today. Without the guard, a limit with an adjust unit and
  only absolute bounds would throw for a patient without that measurement, although there is nothing
  to adjust.
- **Lookups.** `PrescriptionRule.filter` serves the order context and formulary lookups alike. The MCP
  tool `getPrescriptionRules` (`McpTools.GenForm.fs:193`) passes the empty patient, without an age, a
  weight or a height; today it gets every dose rule, unadjusted. `DoseRule.filter` skips the patient
  check for an empty patient (`DoseRule.fs:996`). `SolutionRule.filter` does not (`SolutionRule.fs:290`),
  so a lookup with an empty patient loses the solution rules with an age or weight range.
- **Pinning.** `adjustDoseLimitToPatient` (`:10`) replaces an adjusted limit with the absolute one only
  when the two do not overlap at all for this patient: when even the lowest adjusted dose is at or
  above the absolute maximum, or the highest is at or below the absolute minimum. The dose is then
  pinned to the absolute limit, and that adjusted `MinMax` is emptied.
- **Overlap.** When the two overlap partly, both stay. `Medication` writes both onto the order
  (`Medication.fs:1322-1340`), and the solver keeps what both allow. For 10 to 20 mg/kg at 30 kg with
  an absolute maximum of 500 mg, the order allows 300 to 500 mg.
- **Renal rules.** After the map, a renal rule can adjust the limits (`PrescriptionRule.fs:311`,
  `RenalRule.adjustDoseLimit`, `RenalRule.fs:405`). Since step 2 (#1541), under a replacing reduction
  the renal rule's bounds replace the dose rule's, and the dose rule fills in what the renal rule does
  not give (`RenalRule.replaceDose`, `:368`). A kind the renal rule does not limit keeps the dose
  rule's maximum and drops its minimum. A relative reduction multiplies, so a factor above 1 raises
  the dose.
- **Completion.** `PrescriptionRule.filter` completes each selected dose rule with the product details
  first (`DoseRule.reconstitute`, `DoseRule.fs:305`); the limits of the pharmaceutical form were added
  at load (`addFormLimits`).
- **Solution rules.** `adjustSolutionRuleToPatient` (`:167`) turns the per kg volumes of a solution
  rule into absolute ones, when there is a weight. Without a weight it returns the solution rule
  unadjusted.
- **Renal bounds in another measurement.** Step 2 moves a renal per kg or per m2 bound to the absolute
  form only when the dose limit has an adjust unit of the other measurement. A dose limit without an
  adjust unit keeps a renal per m2 bound as it is, and the order, which falls back to kg
  (`Medication.fs:1137`), would then apply it against the weight. The export of 2026-10-27 has no per
  m2 renal rule, so this does not occur today; for a per kg renal rule the fallback to kg is right.
- **Renal bounds without a measurement.** Under a replacing reduction, a renal bound replaces the dose
  rule's bound on that side in both forms (`RenalRule.replaceDose`). Without the patient's weight, a
  renal maximum in mg therefore drops the dose rule's maximum in mg/kg, and the rule no longer shows
  that it needed a weight.
- **The applied renal rule.** The renal step makes one prescription rule per matching renal rule
  (`PrescriptionRule.fs:311`), but each keeps every matching renal rule in `RenalRules`, not only the
  one applied to its dose rule.
- **Missing measurement.** Nothing leaves out a rule because its limits need a weight or a BSA that is
  missing.
- **Patient categories.** The patient category check fails a weight range when the weight is unknown,
  but passes a BSA range when the BSA is unknown (`Patient.fs:232`).
- **Adjust units.** A rule is built from several sheet rows, grouped by source, indication, generic,
  patient and route (`DataGroupKey`, `Types.fs:1003`). Each row's dose limit reads its own `AdjustUnit`
  (`DoseRule.getDoseLimits`, `DoseRule.fs:413`), so nothing stops one rule from mixing per kg and per
  m2 limits. Such a rule is calculated wrongly: the order has one adjust quantity, the weight or the
  BSA, chosen from the adjust unit that the rule takes from a single row (`DoseRule.fs:641`,
  `Medication.fs:1121`), so the limits of the other unit are applied in the wrong unit. The same
  happens without a mix: the rule takes its adjust unit from its first row (`DoseRule.fs:562`,
  `:641`), so when the first row has no adjust unit and a later row is per m2, the rule has none, the
  order falls back to kg (`Medication.fs:1137`), and the per m2 limit is applied against the weight.
  The same rows in the other order are calculated correctly. A row with a per kg or per m2 value but
  no adjust unit loses that value without a message: `getDoseLimits` (`DoseRule.fs:409`) builds the
  adjusted dose unit from the adjust unit, and `MinMax.fromTuple` (`Utils.fs:248`) gives an empty
  range without a unit.

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

The maintainer decided these on 2026-10-28, and revised the rules for a missing measurement on
2026-10-29, after the first script for the rule selection, the reviews of this plan, and the count of
the sheet export of 2026-10-27.

| Subject | Decision |
|---|---|
| Who is an adult | A patient of at least 18 years (`Utils.ValueUnit.ageAdult`, 365 days to the year, `Utils.fs:204`) is an adult, whether marked or not. A patient marked as an adult is an adult when the patient is at least 16 years old or has no age. |
| The adult marker | A separate field on the patient, next to the age, not a change to the age's type. Below 16 years a patient cannot be marked as an adult: the mark is refused, and the adult test ignores it. From the 16th birthday to 18 years the mark is the user's choice. From 18 years it is implied. |
| Where the mark comes from | From 18 years the patient panel shows the mark checked by itself, and the user cannot uncheck it, since the patient is an adult anyway. From 16 to 18 years, and for a patient without an age, only the user sets it, in the patient panel. The hospital record (EHR) never supplies it. It is kept wherever the patient is kept, like the weight. |
| What the mark allows | A patient who is an adult may be prescribed for without a weight or a height, on the rules that need no adjustment. |
| Matching a marked patient | A marked patient without an age, or of 16 or 17, is matched against a rule's patient category as an age of 18 years and over, with or without a weight. A marked patient of 16 or 17 therefore gets the adult rules and no longer the rules for children. Rules for adults and rules without an age range are selected. A rule whose age range ends below 18, or reaches below 18 (for example 12 to 25 years), is not, since the age may fall outside it. The other checks see an age of 18 years and no gestational age, since a gestational age says nothing about a patient older than one year; the post-menstrual age follows from 18 years and a term birth, so a rule with only a post-menstrual range is not selected. A marked patient without an age meets the minimum data. |
| One adult test | The adult test is written once in GenFORM and once in Shared, with a test that keeps the two in line. The client uses the Shared test, also for the patient text ("Man" or "Jongen"). |
| The estimate | A patient older than 18 years (more than 18 × 365 days) gets no estimated weight or height. A patient of 18 years or younger keeps the estimate, whether marked as an adult or not, and so does a child, with the gate that asks for a weight. A patient older than 18 years therefore loses the per kg rules that an estimate gives today, until a weight is entered. The step that stops the estimate counts the rules this affects, and the release notes say it. |
| The rules an adult gets | The absolute rules. With a weight, also the per kg rules. With a weight and a height, also the per m2 rules. |
| Where the check sits | GenFORM selects the rules as today, for a formulary lookup and for the order context alike, with no mode in its filter. A single pure check, `PrescriptionRule.isPrescribable`, tells whether a selected prescription rule can be prescribed for the patient: every per kg or per m2 bound in its dose limits, solution rules and applied renal rule has the measurement it needs. GenORDER applies it in `OrderContext.create` and in both lookups of `OrderContext.getRules`, before the rules become pick lists, so a pick list offers only rules that can be prescribed and a pick list with one option never selects one that cannot. A lookup through MCP, the scripts or the formulary views stays a lookup. |
| Who may lack a measurement | An adult may lack a weight and a height. A child needs a weight; a height only for the rules that need a BSA. A child with a weight and no height gets the rules that need no BSA: no per m2 limits and no BSA range. A child lacks a height only when the normal values give none for the child's age, or when the user clears the height; it is not an everyday path. The gates in `OrderContext.getRules`, the server's patient mapper and the client enforce this; the rule check does not, since it decides only what a rule needs. |
| A rule that needs a measurement | A rule needs a measurement when one of its dose limits has a per kg or per m2 bound, when one of its solution rules has a per kg bound, or when its renal rule has a per kg or per m2 bound. A filled adjust unit with only absolute bounds needs nothing. For a patient without that measurement the whole rule cannot be prescribed; it is never used on its absolute limits alone. The check is strict: a dose rule with a per kg bound needs a weight also when a renal rule replaces that bound with an absolute one. Clinically the dose rule's author wrote a per kg dose, and the renal rule applies only when a renal function is known. A rule whose patient category has a weight or a BSA range needs that measurement as well. The patient category check takes care of this, not `isPrescribable`: a patient without a weight fails a weight range today, and fails a BSA range once step 3a is in (see "Weight and BSA ranges"). |
| A renal rule that needs a measurement | A renal rule with a per kg or per m2 bound, for a patient without that measurement, makes the prescription rule one that cannot be prescribed. Skipping the renal rule instead would give the unreduced dose. To let the check see this on the selected rule, each prescription rule keeps only the renal rule applied to it, and without the patient's measurement the renal step drops no per kg or per m2 bound of the dose rule. A lookup without a weight then shows the renal bound beside the dose rule's per kg bound. |
| Adjusting the limits | A dose limit is adjusted to the patient only where an adjusted limit and an absolute limit conflict at the patient's weight or BSA: the dose is pinned to the absolute limit and the adjusted limit is emptied. Where the two overlap, both stay. When every adjusted limit of an order is pinned, the order has no adjusted constraint left, and step 5 leaves out its adjust equations. The adjustment needs the measurement that the limit's own per kg or per m2 bounds need, read with a match on `Patient.adjustValue`. Without it the limit stays as it is, and the check refuses the rule, so a rule never loses its adjust equations for a patient who does not provide the measurement. A limit without a per kg or per m2 bound has nothing to adjust, whatever its adjust unit. The guard that needs both a weight and a height goes. This changes the behaviour only for a patient who lacks one of the two: a patient with a weight and no height now gets the per kg limits and the solution rules adjusted, and every patient gets the component pick. For a patient with both, the result is unchanged. Today `Option.get` cannot throw, since the guard admits only a patient with both measurements; the match replaces it so that the guard can go. |
| The component selection | The user's pick of components applies whatever the patient has. The measurement check runs on what the pick leaves: a component that was not picked does not count. |
| A renal rule with per kg or per m2 bounds | A replacing reduction replaces the dose rule's limits, so the patient must provide the renal rule's measurement and the order has an adjust with its equations. A relative reduction multiplies the dose rule's own bounds and follows their form. An order has a single adjust, so the adjust unit of a replacing renal rule is handled per case. A dose rule without an adjust unit takes the renal rule's unit, for the dose limit and the dose rule, so the order gets that adjust. Example at 1.8 m2: 500 to 1,000 mg per administration with a renal maximum of 400 mg/m2 gives at least 500 mg and at most 400 mg/m2, 720 mg; today the order would fall back to kg and apply 400 mg/m2 against the weight. The export of 2026-10-27 has no per m2 renal rule, so this is a guard for a future one; for a per kg renal rule the fallback to kg already gives the right result. A dose rule with the other unit gets the renal bound moved to the absolute form at the patient's measurement, as step 2 does. Example at 70 kg and 1.8 m2: 10 to 20 mg/kg per administration and at most 4 g per day, with a renal maximum of 600 mg/m2 per day, gives at most 1,080 mg per day, and the order keeps kg. |
| A rule that mixes per kg and per m2 | Left out when the sheet is loaded, with a message that names the rule and the reason, like the other sheet checks, and tells the sheet author to give every row of the rule the rule's adjust unit. An order has a single adjust quantity (`Order.Adjust`, GenORDER `Types.fs:255`), so it cannot hold both, and the rule takes its adjust unit from its first row. The check at load is therefore that every row of a dose rule has the same adjust unit, an empty one included, which does not depend on the order of the rows. A refused rule is either calculated wrongly today, or has a shape that is right only by the order of its rows, such as a per kg first row and an absolute row without an adjust unit; the export of 2026-10-27 has none of the second kind. The same holds for a renal rule with both per kg and per m2 bounds, so the column order of the sheet never decides a unit. A refused renal rule would match no patient and so give the full dose for a reduced kidney function; it therefore also leaves out the dose rules it applies to, so the medicine is not offered until the sheet is fixed. A row with a per kg or per m2 value but no adjust unit loses that value today without a message, so its rule is refused at load too; this check covers the dose rules and the replacing renal rules, not the relative renal rules, whose adjusted columns hold reduction factors without a unit. A rule with an adjust unit but no per kg or per m2 value in any of its rows changes no calculation, and gets a warning at load so the sheet can be tidied; an absolute row inside a per kg rule gets none, since it needs the rule's adjust unit. Step 3a gives the counts of the export. |
| A replacing renal reduction | A renal rule is selected by an eGFR range, and may describe a reduced kidney function or hyper clearance, which needs higher doses; "reduction" in the code (`DoseReduction`) and in this plan covers both. A relative reduction with a factor above 1 already raises the dose, and is unchanged. The renal rule's bounds win, and the dose rule fills in what the renal rule does not give. The absolute and the per kg (or per m2) form of one dose count as one dose here: a bound the renal rule gives for a dose, in either form, replaces the dose rule's bound on that side in both forms. Example at 30 kg: a dose rule of 500 to 1,000 mg and at most 20 mg/kg per dose, with a renal range of 700 to 800 mg per dose, gives 700 to 800 mg; the per kg maximum goes. Per kind of limit and per bound: a bound the renal rule gives replaces the dose rule's bound; a bound the renal rule does not give, in a kind it limits, is kept from the dose rule and moved to the renal bound when the two conflict, compared at the patient's weight or BSA when one is per kg or per m2 and the other absolute, and left as it is without that measurement; a kind the renal rule does not limit keeps the dose rule's maximum and drops its minimum, as today, since no renal bound of that kind can make the minimum fit. A renal range wider than the dose rule's therefore widens the dose. A renal rule for a reduced kidney function that does so is almost certainly a sheet error, so the load warns about it (next row). With a renal minimum only, the dose rule's maximum is raised to that minimum, so a dose never loses its maximum and the rule stays selectable; a hyper-clearance rule that wants a range gives both bounds. Examples: 2 to 4 g per day with a renal maximum of 1 g gives exactly 1 g; 0.5 to 1 g per day with a renal minimum of 2 g gives exactly 2 g; 0.5 to 1 g per day with a renal range of 2 to 3 g gives 2 to 3 g. Renal limits are always inclusive (`RenalRule.fs:269` to `:276`), so a replaced or moved bound never leaves an empty range. A step of its own (#1535), before the rule selection changes; it does not depend on step 1 and can land first, or alone. |
| A renal rule that raises a reduced dose | When the sheets are loaded, each renal rule is compared with the dose rules of its generic, and of its route, indication and dose type when it names them. A renal rule whose eGFR range stays below 90 mL/min (it has a maximum eGFR of at most 90), or that applies to dialysis, and whose maximum of a kind lies above the dose rule's maximum of that kind, is reported with a message that names both rules, in the resource messages. There is no patient at load, so a maximum is compared only with one of the same kind and form: absolute with absolute, per kg with per kg. The rule is still used as the sheet says. |
| A line forced by a new case | A new case of a union type forces a new line in each match over it. When that line sits in a project the step's script cannot load, it is written directly, without a script, and named in the pull request, as the call-site changes of a deletion are. The maintainer's exception to the script-first policy, for steps 6 and 7. |
| Weight and BSA ranges | A rule restricted to a weight range is not selected for a patient without a weight, as today. A rule restricted to a BSA range is not selected for a patient without a BSA; today it is. This holds in the patient category check itself, so for a lookup with patient data as well; a lookup without a patient skips that check. Without it, two rules for BSA bands with absolute doses only, such as dexamethasone for nausea from chemotherapy (2 mg up to 0.6 m2, 4 mg from 0.6 m2), would both pass the measurement check without a BSA. |
| A per kg and an absolute limit that overlap | Both stay, and the order allows what both allow, as today. The dose dialog shows the per kg fields, and the printed order the per kg doses, whenever the order still has a per kg (or per m2) limit, decided by the same test that keeps the adjust equations. |
| A per kg command on an order without per kg limits | Refused with a refusal of its own, which says that the order has no per kg dose. The callers of the older `OrderContext.evaluate` get the context as sent, as they do for "not solved". |
| A picked rule that cannot be prescribed | A pick the user chose stays when it is no longer offered, so a picked rule can fail the check: an adult with a weight picks morphine IV, then the weight is cleared. MCP and a stored context can set such a pick as well. The order context refuses it with a refusal of its own, which says that the rule needs a weight, or a weight and a height. Today's refusal would answer "no products", which is the wrong reason. |
| Telling the user what a measurement adds | An adult without a weight never sees a per kg rule in the pick lists. The client says that the per kg rules need a weight and the per m2 rules a weight and a height, whenever the patient lacks one of them, so a missing medicine is explained. |
| The order of the steps | GenFORM first, then GenORDER, then the gates. Step 2, the renal fix, stands on its own, and so does step 3a, which fixes calculations that are wrong on `master` today; its renal changes land as step 3c, a second pull request from the same script. The rule check comes in two steps: the check itself in GenFORM (step 3b), then its use in the order context (step 4). The rename of the absolute renal reduction to Replace (#1550) is step 3d, after step 3b, which changes the same renal code, and before step 4. |
| The empty patient | The dose rule filter skips the whole patient check for the empty patient, which makes a formulary lookup return every rule. The empty patient never reaches a prescription: it has no age, no weight and no adult mark, so the gate in `OrderContext.getRules` refuses it. A test shows this; no extra code. |

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

  The maintainer chose the third, so that an adult is prescribed for on what is measured. The estimate
  stops only above 18 years: a patient of 16 to 18 years is still estimated, marked or not.
- **The adult marker.** Two ways were looked at:
  - the age as a choice between a known age and "adult";
  - a separate mark next to the age.

  The maintainer chose the separate mark, with its own lower limit of 16 years.
- **A rule whose measurement is missing.** Two ways were looked at:
  - use the rule on its absolute limits, emptying the per kg and per m2 limits, and leave the rule out
    only when a dose loses its maximum;
  - leave the rule out.

  The first version of the script built the first way, and it needed a comparison of every limit
  before and after each adjustment. The maintainer chose the second: a limit that needs a measurement cannot apply to a
  patient who lacks it.
- **Keeping the formulary.** Three ways were looked at to keep the new checks out of a lookup:
  - apply them only to an adult;
  - let the caller say whether it looks up or prescribes, with a choice `Lookup` or `Prescribe` on
    the rule filter, and adjustments that return an option and leave a rule out;
  - keep the filter a lookup for everyone, and check the selected rules where the order context turns
    them into pick lists.

  The first leaves a child without a weight with unadjusted per kg rules from GenFORM, and a lookup
  cannot be told from a prescription by the patient alone. The second was planned first; it runs a mode
  through the whole filter and takes two GenFORM steps. The third reaches the same rules in the app with
  one predicate, and leaves the formulary untouched by construction. The maintainer chose the third.

## Chosen approach

**Step 1, the adult marker.** GenFORM's patient gets the marker, the adult test, and the matching of a
marked patient against a rule's patient category. Nothing in the app sets the marker yet.

**Step 2, the renal reduction.** Under a replacing renal reduction, for a reduced kidney function as
for hyper clearance, the renal rule's bounds replace the dose rule's, and the dose rule fills in what
the renal rule does not give (#1535).

**Step 3a, GenFORM: the calculation fixes.** Each change fixes a calculation that is wrong on `master`
today, or closes a shape that is right only by the order of the sheet rows, and none needs an adult or
the check.

- A dose rule whose rows do not share one adjust unit, or that has a per kg or per m2 value without
  an adjust unit, is left out when the sheet is loaded, with a message; so is a renal rule that mixes
  per kg and per m2, or has a per kg value without an adjust unit, together with the dose rules it
  applies to, so that no dose is given unreduced. A rule with an adjust unit and no per kg or per m2
  value in any of its rows gets a warning.
- A replacing renal rule with per kg or per m2 bounds gives a dose rule without an adjust unit its
  unit; a guard for a future per m2 renal rule, since the sheet has none.
- The patient category check fails a BSA range when the BSA is unknown, as it fails a weight range when
  the weight is unknown.

**Step 3b, GenFORM: whether a rule can be prescribed.** GenFORM selects the rules as today, and gets a
check on a selected rule.

- `PrescriptionRule.isPrescribable` is true when the patient has every measurement the rule's dose
  limits, solution rules and applied renal rule need. A limit without a per kg or per m2 bound needs
  nothing, whatever its adjust unit.
- Each prescription rule keeps only the renal rule applied to it, and without the patient's
  measurement the renal step drops no per kg or per m2 bound, so the check is strict. The renal step
  gets a guard for a missing measurement; with the measurement nothing changes.
- A limit is adjusted whenever the patient has its own measurement, without `Option.get`; the guard
  that needs both a weight and a height goes.
- The user's component pick always applies, and the check runs on what it leaves.

For a patient with a weight and a height, the selected rules are the same as after step 3a, apart from
the renal rules each rule keeps.

**Step 4, GenORDER: offer only what can be prescribed.** The order context filters the selected rules
with the check before it builds the pick lists and the scenarios.

**Steps 5 and 6, GenORDER.** After step 4, an order has an adjusted constraint only when its patient has
the measurement and the rule binds an adjusted limit.

- Step 5 leaves out the adjust equations of every other order. The search then no longer counts the
  adjust quantity as an input.
- Step 6 refuses, with a refusal of its own, the commands that would set or recalculate an adjusted
  dose on such an order. It also lets the dose dialog's per kg fields and the print follow the order,
  and refuses a picked rule that cannot be prescribed, with a refusal that says which measurement the
  rule needs.

**Step 7, the refusals on the client.** The two new refusals are carried over the wire and shown by the
client.

**Steps 8 and 9, the gates.** These steps carry the adult marker on the wire, in the stored patient and
in the patient panel. They open the gates for an adult, and stop the estimate above 18 years, on the
server, in GenORDER, in Shared and in the client. The client says which rules a weight or a height
would add.

## Checked, no change needed

- `Nutrition.fs:10` applies the constraints of the adjust quantity. TPN rules limit mL/kg, so a TPN order
  keeps its adjust equations.
- `Totals.fs` reads no adjusted variable; it divides by the weight itself.
- `Order.isSolved` ignores the variables without constraints, so an adjusted variable left open does not
  make an order unsolved.
- `Medication.fromRule` (`Medication.fs:1137`) falls back to kg when the dose rule has no adjust unit.
  After step 3a every per kg or per m2 bound of a loaded rule is in the rule's own adjust unit, so
  the fallback applies only to a rule without adjusted bounds, whose adjust equations step 5 leaves
  out.
- A solution rule's per kg bounds are applied with the weight (`Medication.fs:1082`), never with the
  order's adjust unit, so a per m2 dose rule with a per kg solution rule is calculated correctly.

## Confidence

- **High for step 1.** A new field and a pure test, with no effect until the gates open.
- **Medium for step 2.** A small change to one function, on one model: the renal bound replaces, the
  dose bound fills in. The count of the demo renal rules whose maximum lies above the dose rule's shows
  how much changes.
- **High for step 3a.** Each fix is small and has a test on today's code. The export of 2026-10-27
  shows what the load checks find: ten rows to fix in the sheet first, and nothing else refused.
- **Medium for step 3b.** The check is a pure predicate on a selected rule. The pull request counts the
  demo rules each case changes. The change is a law: with a weight and a height, the selected rules
  equal those after step 3a, apart from the renal rules each rule keeps.
- **High for step 4.** A filter at three call sites; with a weight and a height every rule passes the
  check, so the app is unchanged until the gates open.
- **High for step 5.** The issue measured the result on nine scenarios: every non-adjusted variable is
  identical with and without the adjust equations, when the order has no adjusted limit.
- **Medium for steps 6 and 7.** The two refusals are new, and the per kg fields and the print change for the
  rules whose per kg and absolute limits overlap. The tests and the browser check decide.
- **Medium for steps 8 and 9.** The gates sit in three places. The client's patient panel has to show a
  patient older than 18 years without an estimate and offer the adult switch. The client tests and the
  browser check decide.

## Steps

The work is done in one pull request per step, after the maintainer's go.

- **Script first.** Every `.fs` change is prototyped in a script with its Expecto tests, reviewed by
  the maintainer, and migrated when asked.
- **Size.** At most 200 lines of shipped code under `src/` per step.
- **The script goes.** The pull request that completes a step's migration deletes its script.

0. **This plan.** `docs: the plan for #1495`.

1. **GenFORM: the adult marker.** Done, #1538.
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1495AdultMarker.fsx`.
   - **The field.** `MarkedAdult: bool` on the GenFORM `Patient` (`Types.fs`, next to
     `HeightMeasured`).
     - False in the empty `Patient.patient` (`Patient.fs:668`), and so in `Patient.ofEhr`.
     - On the Dto, so an existing Dto without the field reads as not marked.
     - Every other place that builds the patient in full: GenORDER's `Patient.fs:28` and the server's
       patient mapper fill in false; `matchedPatient` (`OrderContext.fs:682`) copies the mark, so the
       rule lookup keeps it. The test files build the patient from `Patient.patient` and need no
       change.
   - **The ages.** `AgeValue.sixteen` in GenCORE, next to `eighteen`, and
     `Utils.ValueUnit.ageMarkable` from it, in days and 365 to the year, as `ageAdult` is.
   - **The test.** `Adult.isAdult` is true when the age is at least `ageAdult`, or when the patient
     is marked and is at least 16 years old or has no age. Below 16 years it ignores the mark and
     answers false.
     - It goes in a module `Adult` of its own in `Patient.fs`, before `PatientCategory`, since the
       category filter needs it and the `Patient` module comes after the category.
     - `Adult.markRefused` says when the mark is refused, and `Adult.matchedAsAdult` when a patient
       is matched as an adult: marked, and without an age or of 16 or 17.
   - **The checks.** `Patient.validate` and `Patient.Dto.fromDto` refuse the mark below 16 years, with
     a new case `PatientError.MarkedAdultBelowSixteen`. `meetsMinimumData` accepts a marked patient
     without an age. The new case forces a line in the server's words for a patient error:
     "Onder 16 jaar kan een patiënt niet als volwassene gelden".
   - **The patient category.** In `PatientCategory.filter` and `PatientCategory.filterPatient`, a
     marked patient without an age, or of 16 or 17, is matched as every age from `ageAdult` up, with
     or without a weight. One helper, `forAdultMatch`, serves both: it gives the patient with an age
     of 18 years and no gestational age, and a check that the rule's age range has no upper bound.
     The existing age check then tests that 18 years lies within the range.
   - **The renal rules.** `RenalRule.filter` uses the same helper, so a patient matched as an adult
     gets the renal rules that hold every age from 18 years up, and keeps a dose reduction for a
     reduced kidney function. A patient without an age and without the mark gets the renal rules
     without an age range, as for the dose rules; before, such a patient got no renal rule at all.
   - **The stored order plan.** The plan's patient writes the mark, so the JSON structure version of
     a stored order plan goes from 6 to 7 (`SqlDatabase.jsonVersionRead` and `jsonVersionWritten`).
     A row of version 6 reads as not marked; a new fixture `order_plan_v7.json` is the version 6
     fixture as this release writes it.
   - **Tests:**
     - `isAdult` for six patients, each age built in days, 365 to the year, as `ageAdult` is:
       - 18 years without the mark: an adult;
       - 17 years with the mark: an adult;
       - no age with the mark: an adult;
       - 17 years without the mark: not an adult;
       - exactly 16 years with the mark: an adult;
       - 16 years less one day (5,839 days) with the mark: refused by `validate` and by `fromDto`, and
         not an adult;
     - a marked patient without an age passes `validate` and `fromDto`, and the mark survives the Dto;
     - a marked patient without an age, and one of 17, matches an adult rule, an all-age rule and a
       rule whose age range starts at exactly 18 years, and does not match a rule of 12 to 25 years;
     - a marked patient of 17 with a weight matches the adult rules and not a rule for children of 1
       to 18 years;
     - a marked patient of 17 with a gestational age of 30 weeks matches the adult rules;
     - a marked patient without a weight does not match a rule with a weight range;
     - from 18 years the mark changes nothing, as a law over the demo rules. An unmarked patient
       never reaches the new code, so a law for one would only compare the function with itself;
     - a stored order plan of version 6 reads as not marked and is written as version 7.
     - a marked patient without an age, and one of 17, gets the all-age and the adult renal rules and
       not a renal rule of 12 to 25 years; an unmarked patient without an age gets the all-age renal
       rule only; an unmarked patient of 17 gets the renal rules for the age held; a patient younger
       than 28 days gets none.
   - `feat(genform): mark a patient as an adult`.

2. **GenFORM: under a replacing renal reduction the renal bounds win (#1535).** Done, #1541.
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1535RenalReduction.fsx`.
   - **The change.** In `RenalRule.adjustDoseLimit` (`RenalRule.fs:405`), the `Absolute` case, now
     `RenalRule.replaceDose` (`:368`), for each kind of limit:
     - the renal rule gives no range of the kind: the dose rule's maximum stays, its minimum is
       dropped, as today;
     - the renal rule gives a range without a maximum: the renal minimum replaces the dose rule's
       minimum; the dose rule's maximum stays, as the `Relative` case falls back
       (`| _ -> mmOrig.Max`), raised to the renal minimum when it lies below it;
     - the renal rule gives a range without a minimum: the renal maximum replaces the dose rule's
       maximum; the dose rule's minimum stays, lowered to the renal maximum when it lies above it;
     - the renal rule gives both bounds: they replace the dose rule's bounds. Today the lower of each
       pair is taken, which ignores a hyper-clearance rule.

     The absolute and the per kg (or per m2) form of one dose count as one dose: a bound the renal
     rule gives for a dose, in either form, replaces the dose rule's bound on that side in both forms.
     The dose rule's bound in the other form on that side is dropped, so the two can never conflict.
     On a side the renal rule does not give, a kept bound in the other form is compared with the renal
     bound at the patient's weight or BSA, so `adjustDoseLimit` and `adjustDoseRule` take the patient;
     without that measurement the bound stays as it is.
     A renal per kg or per m2 bound in another measurement than the dose rule's adjust unit (per m2 on
     a per kg rule, or the reverse) moves to the absolute form at its own measurement, and is left out
     without it; the load reports such a renal rule as a sheet error.
   - **Tests:**
     - a dose rule of 500 to 1,000 mg per administration, 4 times a day and at most 4 g per day, and a
       renal rule with a replacing reduction to at most 1 g per day only: the order keeps at most
       1,000 mg per administration and drops the minimum of 500 mg, which would ask for at least 2 g
       per day, so the order can still be solved;
     - a renal rule with only a minimum per day keeps the dose rule's maximum per day, when the minimum
       lies below it;
     - 2 to 4 g per day with a renal maximum of 1 g per day gives exactly 1 g per day, not an empty
       range; 0.5 to 4 g per day with the same renal maximum gives 0.5 to 1 g per day;
     - 0.5 to 1 g per day with a renal minimum of 2 g per day gives exactly 2 g per day, not an empty
       range;
     - a reduction with both bounds: 2 to 4 g per day with a renal range of 0.5 to 1 g gives 0.5 to
       1 g;
     - hyper clearance with both bounds: 0.5 to 1 g per day with a renal range of 2 to 3 g gives 2 to
       3 g;
     - a renal range wider than the dose rule's: 1 to 2 g per day with 0.5 to 3 g gives 0.5 to 3 g;
     - an absolute renal range against a per kg limit of the same dose: at 30 kg, a dose rule of 500
       to 1,000 mg and at most 20 mg/kg per dose, with a renal range of 700 to 800 mg per dose, gives
       700 to 800 mg; the per kg maximum is dropped, so a dose remains possible;
     - a guard on the renal rule parser: it reads every renal limit with `fromTupleInclIncl`, so a
       replaced or moved bound never leaves an empty range; the test fails when that changes;
     - the relative and no-reduction cases are unchanged.

     The pull request counts the demo renal rules whose result changes:
     - those that lose a maximum today, by a whole kind or by one bound;
     - those whose kept bound is moved to a renal bound;
     - those with both bounds whose renal maximum lies above the dose rule's maximum, which now widen
       or raise the dose;
     - those the new warning reports.
   - The commit has a `=== changelog ===` block: under a replacing renal reduction the renal rule's
     bounds replace the dose rule's, also when they are higher, as for hyper clearance; the dose
     rule's other bounds stay, moved to the renal bound when they conflict.
   - **The warning.** When the sheets are loaded, in a resource of its own that only gives messages
     (`renalRuleChecks`, `Resources.fs`), a renal rule whose eGFR range stays below 90 mL/min, or that
     applies to dialysis, and whose maximum of a kind lies above the dose rule's maximum of that kind
     and form, is reported with a message that names both rules. The rule is still used. Test: a dose
     rule of 1 to 2 g per day and a renal rule for an eGFR of 10 to 30 mL/min with 0.5 to 3 g per day
     gives 0.5 to 3 g per day and the message; a renal rule for an eGFR of 130 mL/min and up with the
     same range gives no message.
   - `fix(genform): let absolute renal bounds win`.

3a. **GenFORM: the calculation fixes.**
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1495RuleUnits.fsx`. Each change fixes a calculation
   that is wrong on `master` today, or closes a shape that is right only by the order of the sheet rows,
   and none needs an adult or the check of step 3b, so the step can land alone, like step 2.
   - **One adjust unit per rule.** The rule takes its adjust unit from its first row
     (`DoseRule.fs:562`, `:641`), and each dose limit from its own row (`DoseRule.fs:413`). When the
     dose rules are built from the sheet rows, a rule whose rows do not all have the same adjust unit,
     an empty one included, is left out, with a message that names the rule and the reason, and tells
     the sheet author to give every row of the rule the rule's adjust unit. This covers a mix of kg and
     m2, a rule whose first row has no adjust unit and a later row has one, and a per kg first row with
     a later row without an adjust unit, which is right today only by the order of the rows. It does
     not depend on the order of the rows.
   - **A per kg or per m2 value without an adjust unit.** A row with a value in an adjusted column
     (`MinQtyAdj` to `MaxRateAdj`) and no adjust unit loses that value today without a message
     (`DoseRule.fs:409`, `Utils.fs:248`). Its rule is left out at load, with a message that names the
     row. The check covers the dose rules and the replacing renal rules; the relative renal rows hold
     reduction factors without a unit in the same columns, and are not checked. The norm columns
     `NormQtyAdj` and `NormPerTimeAdj` of the renal rules were removed with #1536: a norm is an adjusted
     minimum equal to the adjusted maximum, as in the dose rules. The export's 70
     replacing renal rows all have kg.
   - **An adjust unit without a per kg or per m2 value.** A rule with an adjust unit and no value in an
     adjusted column in any of its rows gets a warning at load, and is used as today. Examples:
     blinatumomab IV, cortisone oral, factor VII. An absolute row inside a per kg rule, such as the
     clavulanic acid row of amoxicillin/clavulanic acid IV (row 272), gets no warning: it needs the
     rule's adjust unit for the check above.
   - **Renal rules.** When the renal rules are loaded, a renal rule with both per kg and per m2 bounds
     is left out the same way. The relative renal rows hold reduction factors without a unit, so the
     check sees no per kg or per m2 bound in them.
   - **A refused renal rule takes its dose rules along.** A refused renal rule matches no patient, so
     on its own a patient in its eGFR range would get the dose rule's full dose. A renal rule that
     fails a load check therefore also leaves out the dose rules it applies to: those of its generic,
     and of its route, indication and dose type when it names them, as the renal warning of step 2
     compares them. One message per renal rule names the renal rule, the reason, and the number of
     dose rules of its generic left out. The medicine is not offered until the sheet is fixed, the
     same direction as a refused dose rule. The export has no such renal rule, so this is a guard.
   - **The wiring.** Today the dose rules do not depend on the renal rules (`Resources.fs`):
     `Keys.doseRules` is derived from the products and the dose rule data (`:391`), `Keys.renalRules`
     from the renal rule data (`:411`), and only the renal warning `Keys.renalRuleChecks` (`:419`)
     reads both. A registry entry maps one key to one loader, so the refused renal rules get a key of
     their own, `Keys.refusedRenalRules`, derived from `Keys.renalRuleData` with the same check that
     `RenalRule.map` uses to refuse, each refused renal rule with its reasons. `Keys.renalRules` gives
     the renal rules without the refused ones, and gives no message for them. `Keys.doseRules` reads the
     new key, leaves out the matching dose rules, and gives the one message per refused renal rule,
     since only it can count the dose rules left out. Which dose rules a renal rule applies to is
     decided by one function, taken out of `RenalRule.doseRuleWarnings` (`RenalRule.fs:521`) and used
     by the warning and the refusal alike. There is no cycle: the warning reads the dose rules, and the
     dose rules read only the refused renal rules, which come from the renal rule data.
   - **A renal sheet that fails to load** already fails the whole load today, and that stays: the renal
     rule data is an `ofResult` resource (`Resources.fs:339`), a failing loader raises
     `ResourceLoadError` (`:146`), and the load resolves every resource and stops at the first failure
     (`:478`). No medicine is then offered, so no dose is given unreduced. The loader becomes a function
     of its own, `Resources.renalRuleDataLoader`, which `defaultRegistry` uses; a test gives it a fetch
     that fails and checks that the load fails, without a network request.
   - **Two pull requests.** The script would come close to 200 lines of shipped code in one pull
     request, so the step lands in two, from the one script:
     - **3a**, about 90 lines: the dose rule checks (one adjust unit per rule, a per kg value without
       an adjust unit, the warning for an adjust unit without a per kg value) and the BSA range;
     - **3c**, about 120 lines, after 3a: the renal checks (kg and m2 mixed, a per kg value without an
       adjust unit), the refused renal rule taking its dose rules along, the wiring with
       `Keys.refusedRenalRules`, the renal rule data loader with its test, and the renal unit guard.
       Its pull request deletes the script.
   - **The sheet first.** The export of 2026-10-27 has ten rows with a per kg value and no adjust
     unit: clonidine oral (rows 938 and 940), lorazepam oral (2887), pancreatin (3766) and perindopril
     (5084 to 5089). These are fixed in the sheet before the step lands; the pull request shows that
     the load refuses no rule on the fixed sheet. No rule in the export mixes kg and m2 or has rows with
     different adjust units.
   - **A replacing renal rule with per kg or per m2 bounds.** In `RenalRule.adjustDoseLimit` and
     `adjustDoseRule`, a dose rule without an adjust unit takes the renal rule's unit, on the dose limit
     and on the dose rule, so `Medication.fromRule` uses it. Step 2 already uses that unit for the
     adjust value, and moves a renal bound in the other unit to the absolute form at the patient's
     measurement. The export has no per m2 renal rule, and for a per kg renal rule the order's
     fallback to kg is already right, so this is a guard for a future per m2 renal rule.
   - **The BSA range.** In `PatientCategory.filter` (`Patient.fs:232`) and `filterPatient` (`:293`),
     the BSA line takes the shape of the weight line above it:
     `Option.map2 Calculations.calcDuBois weight height |> MinMax.inRange p.BSA`. GenFORM's
     `MinMax.inRange` (`Utils.fs:258`) passes an empty range without a value and fails a bounded one,
     so an unknown BSA fails a BSA range, as an unknown weight fails a weight range. Today the match
     falls back to true.
   - **Tests:**
     - first, on today's code: a rule whose rows mix kg and m2, and a rule whose first row is absolute
       and a later row per m2, each at a known weight and height, apply the per m2 limit against the
       weight, which shows the wrong result; then both are left out at load, with their messages; a
       renal rule with both kg and m2 bounds is left out at load, with its message;
     - a rule with a per kg row and an absolute row that also says kg, as amoxicillin/clavulanic acid
       IV, is loaded; with the absolute row's adjust unit empty it is left out, whatever the order of
       the rows;
     - first, on today's code: a row with a maximum of 0.05 mg/kg and no adjust unit gives a dose
       limit without that maximum; then its rule is left out at load, with a message naming the row;
     - a rule with an adjust unit and only absolute limits in every row gives a warning, and is loaded;
       the amoxicillin/clavulanic acid shape, a per kg row and an absolute row both with kg, gives none;
     - a per kg first row and an absolute row without an adjust unit is left out, as is the same rule
       with the rows swapped;
     - a replacing renal row with a per kg value and no adjust unit is left out at load, with its
       message;
     - a relative renal rule is not refused;
     - a renal rule that fails a load check leaves out the dose rules of its generic, and of its route
       when it names one: the dose rule is not offered for a patient in the renal rule's eGFR range, nor
       for any other patient; a dose rule of the same generic on another route stays; one message
       names the renal rule and the number of dose rules left out;
     - the shared matching of a renal rule to dose rules, for the route, the indication and the dose
       type each: a dose rule with another value stays, a dose rule with the named value is taken, and
       a renal rule that names none takes every dose rule of its generic;
     - a guard on today's behaviour, not a change: `renalRuleDataLoader` with a fetch that fails makes
       the load fail, so a later move of the renal rule data to `ofResultOrDefault` fails the test;
     - through a registry of the demo fixtures and the new entries: a refused renal rule is gone from
       the renal rules and its dose rules from the dose rules, the load gives one message for it, and
       resolving every resource raises no cycle;
     - a renal maximum of 400 mg/m2 on a dose rule of 500 to 1,000 mg without an adjust unit, at
       1.8 m2: the rule takes m2, and gives at least 500 mg and at most 400 mg/m2; on today's code the
       rule falls back to kg first, which shows the wrong result;
     - a renal maximum of 600 mg/m2 per day on a per kg dose rule, at 70 kg and 1.8 m2: at most
       1,080 mg per day, and the rule keeps kg;
     - a rule with a BSA range is not selected without a height; the two dexamethasone BSA bands are
       not both selected without a BSA; with a weight and a height the BSA check is unchanged.

     The pull request lists the warnings for rules with an adjust unit and no adjusted value, and shows
     that no rule is refused on the fixed sheet.
   - The commit has a `=== changelog ===` block: a dose rule whose rows do not share one adjust unit,
     or that has a per kg or per m2 value without an adjust unit, is refused when the sheet is loaded,
     as is a renal rule that mixes per kg and per m2 or has a per kg value without an adjust unit,
     together with the dose rules it applies to; a rule restricted to a BSA range is no longer selected
     without a BSA.
   - `fix(genform): refuse rules that mix adjust units`.

3b. **GenFORM: whether a rule can be prescribed.**
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1495Prescribable.fsx`. It replaces
   `Issue1495AdjustedLimits.fsx`, whose first versions left a rule out inside the filter, through
   adjustments that returned an option; the new script checks the selected rule. It keeps the
   measurement helpers; the load check for mixed rules moved to step 3a.
   - **What a rule needs.** A dose limit needs a weight for a per kg bound and a BSA for a per m2 bound,
     read from the bounds with `RenalRule.measureOf`; a limit with an adjust unit and only absolute
     bounds needs nothing. A solution rule needs a weight for a per kg volume or quantity. A renal rule
     needs what its per kg and per m2 bounds need. The patient has a measurement when
     `Patient.adjustValue` gives a value. A helper `DoseRule.allLimits` gives every component limit and
     substance limit of a rule.
   - **The check.** `PrescriptionRule.isPrescribable pat pr` is true when the patient has every
     measurement that the dose limits of `pr.DoseRule`, its solution rules and its renal rules need.
     It runs on the rule as `PrescriptionRule.filter` gives it: after the component pick and the renal
     step.
   - **The applied renal rule.** In the renal step of `PrescriptionRule.filter` (`:311`), each
     prescription rule keeps only the renal rule applied to it (`RenalRules = [| rr |]`). The order
     context reads only whether there is one (`OrderContext.fs:129`).
   - **Strict under a renal rule.** A guard in `RenalRule.replaceDose` that applies only when the
     adjust value is none: no per kg or per m2 bound of the dose rule is dropped. A per kg or per m2
     bound the renal rule does not give in that form stays, on a side the renal rule gives and when
     the renal rule gives nothing for a dose; in that last case the per kg or per m2 minimum stays,
     and the absolute minimum is dropped as in step 2.

     With the measurement the result is unchanged. Step 2's test of a per kg range against an
     absolute renal maximum without a weight now keeps the per kg maximum. A dose rule
     that needs a weight therefore still shows it after the renal step, and the check refuses it.
   - **Adjusting the limits.** `adjustDoseLimitToPatient` reads the measurement from the limit's own
     bounds and matches on `Patient.adjustValue`: none gives the limit as it is, some runs today's
     body. It no longer uses `Option.get`, so a limit with an adjust unit and only absolute bounds
     cannot throw once the guard is gone. In `PrescriptionRule.filter` the guard that needs both a
     weight and a height goes, so a patient with a weight and no height gets the per kg limits
     adjusted.
     `adjustSolutionRuleToPatient` already matches on the weight and stays.
   - **The component pick** moves out of that guard and always applies, so a component that was not
     picked does not count in the check.
   - **Tests:**
     - which limits need a measurement: absolute, an adjust unit with absolute bounds, per kg, per m2;
     - a limit with an adjust unit and only absolute bounds is unchanged without a weight, where
       today's adjustment, called directly, throws;
     - a per kg limit is adjusted with a weight and unchanged without one; a per m2 limit is unchanged
       for a patient with a weight and no height;
     - the check: a per kg rule cannot be prescribed without a weight and can with one; a per m2 rule
       cannot with a weight and no height; an absolute rule can without a weight; a solution rule with
       a per kg volume makes the rule need a weight;
     - a renal rule with a per kg bound makes the rule need a weight; a renal rule with only absolute bounds on an
       absolute dose rule does not;
     - strict: a dose rule of 10 to 20 mg/kg per dose with a replacing renal rule of 500 to 750 mg per
       dose keeps its per kg bounds without a weight, and cannot be prescribed; with a weight of 30 kg
       it gives 500 to 750 mg, as step 2 does;
     - each prescription rule keeps only the renal rule applied to it;
     - the component pick: a picked per kg component without a weight makes the rule need a weight; a
       per kg component that was not picked does not count;
     - a lookup with the empty patient gets every dose rule of the generic, per kg ones included;
     - a law: with a weight and a height, at 3, 20 and 70 kg, the selected rules equal those after step
       3a, compared as records of the dose rules and solution rules, not as printed text, with
       synthetic solution rules (per kg and absolute) and renal rules (absolute and relative) for demo
       generics; the renal rules each rule keeps are left out of the comparison.

     The pull request counts the demo rules per case, among them the dose rules without an adjust unit
     that a replacing per kg renal rule makes need a weight, and says that the component pick now
     applies without a weight and a height.
   - `feat(genform): tell whether a rule can be prescribed`.

3d. **GenFORM: the absolute renal reduction is renamed Replace (#1550).**
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1550ReplaceReduction.fsx`. "Absolute" also names
   the limits of a dose rule that are not per kg or per m2, so "an absolute renal rule with a per kg
   bound" reads as a contradiction. Under this reduction the renal values replace the dose limits, in
   contrast with a relative reduction, where they scale the dose limits. A naming change only; no
   calculation changes. The step comes after step 3b, which changes `RenalRule.absoluteDose`, and
   before step 4; steps 4 to 9 do not touch the renal code.
   - **The case.** `DoseReduction.Absolute` (`Types.fs:636`) becomes `DoseReduction.Replace`, at its
     uses in `RenalRule.adjustDoseLimit` and in the load warnings (`RenalRule.fs:551`, `:641`), and in
     the tests.
   - **The sheet value.** A value `REPL = "repl"` beside `REL` and `ABS` (`RenalRule.fs:19`).
     `DoseReduction.fromString` reads both `repl` and `abs` as `Replace`, so the live sheet keeps
     working until it is updated; `toString` gives `repl`. One function decides whether a sheet value
     is the replacing reduction, and both `fromString` and `unitRefusals` (`RenalRule.fs:309`) use it:
     today `unitRefusals` compares the text with `abs`, so a sheet that moves to `repl` would skip the
     check that refuses a renal rule mixing per kg and per m2.
   - **The names and the text.** `RenalRule.absoluteDose` becomes `RenalRule.replaceDose`; the
     comments, messages and test names that say "absolute renal rule" or "absolute reduction" say
     "replacing renal rule" or "replacing reduction". The `///` comment on `RenalRuleData.DoseRed`
     (`Types.fs:1143`) names `repl`, and `abs` as the old value that is still read. The column-contract
     test checks column names only and stays.
   - **MCP.** The renal rule output (`McpTools.GenForm.fs:134`) gives `replace` in place of `absolute`.
   - **This plan.** The text of steps 2 to 3b and the decisions says "replacing reduction" where it
     means the renal reduction; "absolute" keeps its meaning of a limit that is not per kg or per m2.
   - **Order of the rollout.** The live sheet moves to `repl` only after the release with this step
     runs. A server without it reads `repl` as no reduction, with a warning only, and would give the
     full dose for a reduced kidney function.
   - **Tests:**
     - `fromString` reads `repl`, `abs`, `REPL` and `Abs` as `Replace`, and `rel` as `Relative`;
     - `toString` of `Replace` is `repl`, and `fromString` of it gives `Replace` back;
     - a renal rule given as rows with `repl` that mixes per kg and per m2 is refused at load, as the
       same rows with `abs` are;
     - the renal reduction tests of step 2 pass with the new case name, with unchanged results.

     The pull request says the sheet can be updated once the release runs, and that `abs` is to be
     removed in a later change.
   - `refactor(genform): rename the absolute renal reduction to Replace`, with `Closes #1550`.

4. **GenORDER: offer only the rules that can be prescribed.**
   Script: `src/Informedica.GenORDER.Lib/Scripts/Issue1495Prescribable.fsx`, on the GenFORM DLL of
   step 3b.
   - **`OrderContext.create`** (`:630`) filters the patient's rules with `isPrescribable` before
     `PrescriptionRule.filterOptions`.
   - **One lookup helper.** A function in `OrderContext`, next to `getPrescriptionRules` (`:17`), gets
     the prescription rules for a filter with `Api.filterPrescriptionRules`, keeps those that pass
     `isPrescribable` for the filter's patient, and logs the "no rules" warning (`Api.noRulesWarning`)
     when none are left, as `Api.getFilterOptions` does today.
   - **`OrderContext.getRules`** (`:706`) uses the helper for both lookups: the pick lists come from
     `PrescriptionRule.filterOptions` over its result, in place of `Api.getFilterOptions` (`:721`), and
     the rules of the scenarios (`:756`) come from it directly. A pick list with one option
     (`Array.someIfOne`) then never selects a rule that cannot be prescribed, and an adult without a
     weight is not offered morphine IV only to end without a scenario.
   - Until the gates open in step 8, every patient here has a weight and a height, so every rule
     passes and the app is unchanged.
   - **Tests:**
     - `create` for an adult of 40 without a weight offers ceftriaxone IV and not morphine IV single
       dose; with a weight it offers both;
     - a pick list never holds a generic, route, form or dose type whose rules all fail the check;
     - with a weight and a height, `create` and `getRules` give the same pick lists and rules as
       before, for the shared scenarios.
   - `feat(genorder): offer only the rules that can be prescribed`.

5. **GenORDER: map the adjust equations only when an adjusted variable is constrained.**
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
       every non-adjusted variable equals the result with all equations. The law runs in the script
       only, because the test project has no mapping with all equations to compare against; the
       pull request reports its result;
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

6. **GenORDER: the per kg commands, fields and print follow the order.**
   Script: `src/Informedica.GenORDER.Lib/Scripts/Issue1495AdjustCommands.fsx`.
   - **The commands.** When `usesAdjust` is false, eight commands are refused:
     - `SetNthComponentDoseQuantityAdjust`, `SetNthItemDoseQuantityAdjust`,
       `SetNthItemDosePerTimeAdjust` and `SetNthItemDoseRateAdjust`;
     - `ReCalcComponentDoseQuantityAdjust`, `ReCalcItemDoseQuantityAdjust`,
       `ReCalcItemDosePerTimeAdjust` and `ReCalcItemDoseRateAdjust`.
   - **The refusal.** A new case, `Refusal.NoAdjustedDose`, in GenORDER (`Types.fs:909`): the order
     has no per kg or per m2 dose. The script checks that each command really ends in this refusal,
     with the context as sent. It does not go through `NoRecipe` or the general `NotSolved`. Until
     step 7, the server maps it to the general `NotSolved` on the wire. The new case forces this
     one match line in `ServerApi.Services.fs:335`, which no GenORDER script can load; it is written
     directly and named in the pull request, under the decision "A line forced by a new case".
   - **A picked rule that cannot be prescribed.** A second new case, `Refusal.MeasurementMissing`: the
     picked rule needs a weight, or a weight and a height. `OrderContext.refusalOf` (`:1261`) reads one
     more set, the prescription rules for the picks before and after the check: rules before the check
     and none after give `MeasurementMissing`, where today the answer is `NoProducts`.
     `OrderContext.refusal` (`:1273`) passes that set. Until the gates open in step 8 every patient has
     a weight and a height, so the case cannot occur in the app yet; the tests call `refusalOf`. Until
     step 7, the server maps it to `NoDoseRulesForPatient` on the wire, in the same forced match line.
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
     - `OrderContext.evaluate` (`OrderContext.fs:1532`) answers `NoAdjustedDose` with the context as
       sent, as it does `NotSolved`, for its callers: the MCP host and the older paths;
     - `refusalOf` answers `MeasurementMissing` when the prescription rules for the picks all fail the
       check, and `NoProducts` when there are none before the check.
   - The commit has a `=== changelog ===` block: the dose dialog shows the per kg fields, and the
     printed order the per kg doses, also for a rule whose per kg and absolute limits overlap.
   - `fix(genorder): refuse per kg commands and unusable picks`.

7. **The refusals on the client.**
   `Client.Core` goes through the script
   `src/Informedica.GenPRES.Client.Core/Scripts/Issue1495AdjustRefusal.fsx`.
   - **The wire.** Shared `OrderContextRefusal` (`Types.fs:874`) gets the two new cases, and the server
     maps `Refusal.NoAdjustedDose` and `Refusal.MeasurementMissing` to them
     (`ServerApi.Services.fs:335`). Those match lines are forced by the new cases and written directly,
     under the same decision as in step 6.
   - **Client.Core.** `OrderContextRefusalPolicy` shows both refusals, and the trail names them
     (`Trail.fs:306`). `NoAdjustedDose` refuses a change in the dose dialog, as `NotSolved` does, so
     it takes the path of `NotSolved` in `OrderContextMachine`: the context as sent, the dialog open,
     and a snackbar with its own text. The effect and the alert of that path carry the refusal.
     `MeasurementMissing` shows a notice, as the other rule refusals do, without whom to tell.
   - **The text.** Two new `Terms` cases, their entries in `localization.tsv`, and the Localization
     sheet. The text of `MeasurementMissing` says that the rule needs a weight, or a weight and a
     height. `CheckLocalization.fsx` passes.
   - **Tests.** The policy shows each new refusal's text; the trail writes their names; the server
     maps each GenORDER case to its wire case.
   - `feat(api): say why a rule or a per kg command is refused`.

8. **GenORDER, Shared and the server: open the gate for an adult.**
   Script: `src/Informedica.GenPRES.Server/Scripts/Issue1495AdultGate.fsx`. The step is split in two
   pull requests:
   - **8a, GenORDER.** Script: `src/Informedica.GenORDER.Lib/Scripts/Issue1495AdultRules.fsx`.
     `getRules`, `refusal` and `create` look the rules up for an adult, and for a child with a weight,
     through one test, `OrderContext.selectsRules`. `create` offers a child without a weight no pick.
     Nothing changes in the app, since the server still refuses a patient without a weight and a
     height.
   - **8b, Shared, the server and the MCP host.** The Shared field is written first, since a script
     cannot add a field to the contract's patient; the rest is prototyped in the script against the
     rebuilt contract. This part opens the gate.
     - `Patient.applyNormalValues` leaves a patient older than 18 years as it is, so `estimate` and
       the estimating port follow without a change of their own.
     - The MCP host's `validPatient` checks the patient with `OrderContext.selectsRules`, since without
       an estimate for an adult it would otherwise refuse an adult without a weight.
     - `PatientEdit` builds the draft from the blank patient, which is not marked, and needs no change.
     - The mark is carried with every request but not held by the Session for a resume, as the
       measurements are; a resume loses it. Nothing sets the mark before step 9, so step 9 holds it
       in the Session, together with the switch that sets it.
   - **GenORDER.**
     - `getRules` and `refusal` select rules for an adult, and for a child with a weight, with or
       without a height.
     - `matchedPatient` takes the weight and the height as options, and carries the mark.
     - A child without a weight is still refused.
   - **The server.**
     - `patient` and `parse` let an adult through without a weight or a height, and a child with a
       weight and no height.
     - The mapper carries the mark into the GenFORM patient and refuses it below 16 years.
     - `estimate` (`:281`) and the estimating patient data port (`:228`) leave a patient older than 18
       years as entered.
     - The EHR port never sets the mark.
   - **Shared.**
     - The Shared `Patient` (`Types.fs:82`) gets `MarkedAdult: bool`. It is kept wherever the
       patient is kept, as the weight is; the script lists those places.
     - `Patient.applyNormalValues` gives no estimate for a patient older than 18 years.
     - The adult test is added once to the Shared `Patient` module, with the same rule as GenFORM's
       `Adult.isAdult`. It reads the age in days, 365 to the year, as `Utils.ValueUnit.ageAdult`
       does.
     - `Patient.validate` (`Models.fs:65`) accepts a marked patient without an age, and refuses the
       mark below 16 years with a new Shared `PatientError.MarkedAdultBelowSixteen`, as GenFORM's
       `validate` does.
   - **Client.Core.** The patient editing (`PatientEdit`) builds the Shared patient, so it fills in
     "not marked". This small change goes through the step's script as well.
   - **Tests:**
     - an adult without a weight gets the absolute rules, and no per kg or per m2 rule;
     - an adult with a weight and no height gets the absolute and per kg rules, and no per m2 rule;
     - an adult with a weight and a height gets the absolute, per kg and per m2 rules;
     - a child without a weight is refused;
     - the empty patient gets no rules from the order context;
     - an adult with a weight who picked morphine IV, after the weight is cleared, is refused with
       `MeasurementMissing`;
     - a child with a weight and no height gets the absolute and per kg rules, and no per m2 rule;
     - a patient of 17 marked as an adult, without a weight, gets the adult and all-age rules that
       need no adjustment;
     - a patient of 17 marked as an adult, with a weight, gets the adult rules, per kg ones included,
       and no rules for children;
     - a patient marked as an adult without an age gets the adult and all-age rules;
     - the mark at 16 years less one day (5,839 days) is refused by the server, and at exactly 16
       years accepted;
     - a patient older than 18 years gets no estimate; a patient of exactly 18 years, a marked patient
       of 17 and a child do;
     - the Shared and GenFORM adult tests agree for every age from 0 to 20 years, with and without
       the mark, around the 16th and the 18th birthday to the day;
     - the Shared and GenFORM `validate` agree on the same ages, with and without the mark, an age, a
       measured weight and a measured height.
   - **The pull request** counts the demo rules an adult without a measured weight loses compared with
     today. Its commit has a `=== changelog ===` block saying that an adult needs a measured weight
     for a per kg dose.
   - `feat(server): prescribe for an adult without weight or height`.

9. **The client: an adult without a weight or a height.**
   `Client.Core` goes through the script
   `src/Informedica.GenPRES.Client.Core/Scripts/Issue1495AdultPatient.fsx`; the Fable views are edited
   directly. The step is split in three pull requests:
   - **9a, the Session.** Script: `src/Informedica.GenPRES.Server/Scripts/Issue1495SessionMark.fsx`.
     The server holds the mark for a resume, as it holds the measurements: the Session's
     measurements get the mark, stored as a measurement row of the kind `marked-adult`, and a resume puts it
     back on the contract patient. The core patient has no mark, so the Session's patient gets it
     after the EHR port's projection. Migration 10 rebuilds the measurement table, since its check
     allowed only the weight, the height and the gestational age. Otherwise a marked patient of 16 or
     17 would resume as a child, on the rules for children, while the plan was made on the rules for
     adults. Nothing sets the mark yet, so nothing changes in the app.
   - **9b, `Client.Core`.** The items under `Client.Core` below.
   - **9c, the views.** The adult switch and the hint, edited directly.
   - **`Client.Core`.**
     - `PatientEdit` gets a setter for the mark. The mark is cleared when the age changes to below 16
       years.
     - `PatientMachine` sends the patient of an adult without a weight or a height, and of a child
       with a weight and no height.
     - `PatientReadinessPolicy.missingOf` (`:83`) names no missing age for an adult; it keeps naming a
       missing weight or height for every patient, as a warning, since those leave the per kg and per
       m2 rules out. The refusal notice holds back only for a child without a weight, the one patient
       the rules are not looked up for; `patientIncomplete` goes.
     - `Client.estimated` (`Client.fs:253`) gives a patient older than 18 years no estimate.
     - **No separate hint.** The existing warnings "Weight unknown", "Height unknown" and "Weight and
       height unknown" say what an adult lacks; their wording is in the Localization sheet and can
       name the per kg and per m2 rules there.
     - `PatientText` (`:86`) uses the Shared adult test in place of its own `Years >= 18`.
   - **The views.** The patient panel gets an adult switch:
     - shown checked and fixed from 18 years;
     - free from 16 to 18 years, and when there is no age;
     - not offered below 16 years.

     The panel shows a patient older than 18 years without an estimate; the existing warnings say
     what the patient lacks.
   - **Tests:**
     - an adult without a weight is set on the pages, a child with a weight and no height is set, and
       a child without a weight is held back;
     - a patient marked as an adult without an age misses no age;
     - the mark goes when the age drops below 16;
     - a marked patient of 17 is written as "Man" or "Vrouw".
   - `feat(client): prescribe for an adult without weight or height`.

## Left out

- The emergency and continuous lists need a weight (Shared `Models.fs`). For an adult without a weight
  they stay empty, as they do today for every patient without a weight.
- #1500, undoing a product strength pick without a reset, has its own plan.
- A renal rule's norm doses are read from the sheet but never applied to the dose rule; #1536.
- A lookup without a patient returns every dose rule of the generic, but not every solution rule:
  `SolutionRule.filter` (`SolutionRule.fs:290`) always runs the patient check, so the solution rules
  with an age or weight range drop out. That is a formulary matter, not needed for this plan; #1545.
- A row with a value whose dose unit, frequency unit or rate unit is missing loses that value without a
  message, as a per kg value without an adjust unit does (`MinMax.fromTuple`, `Utils.fs:248`): every
  limit without a dose unit, the per time limits without a frequency unit (`DoseRule.fs:421`), the
  rate limits without a rate unit. Step 3a checks only the adjust unit; the other units: #1546.

## Verification

- **Each script.** Run it from its own directory with `dotnet fsi`, or through the FSI MCP server.
  Every test passes.
- **The baseline.** Before step 3a, run the full suite with `CI=true` in a worktree on the current
  `master`. This gives the baseline for the steps that follow.
- **After each migration:**
  - `dotnet run build` and `dotnet run servertests` pass;
  - with `CI=true` in a worktree, no test turns red or ignored compared with that baseline;
  - `dotnet fantomas --check` passes;
  - `dotnet fsi scripts/CheckDependencyRule.fsx` passes.
- **The refusal `NoAdjustedDose`** cannot be seen in the browser, since the client never sends a per kg
  command for an order without per kg limits. The tests of steps 6 and 7 carry that check.
- **In the browser, after step 9.** The maintainer checks these cases:
  - **An adult of 40 without a weight or a height.**
    - No estimate is shown.
    - Ceftriaxone IV, whose demo rule applies from 12 years with absolute limits only, is offered,
      without the per kg fields.
    - Morphine IV single dose, whose demo rule applies to all ages with per kg limits, is not offered.
      After entering a weight it is.
    - The warning says that the weight and the height are unknown.
    - Pick morphine IV with a weight, then clear the weight: the refusal says that the rule needs a
      weight.
    - The search of a recalculation finishes.
  - **The same adult with a weight and no height.** The absolute and the per kg rules are offered. A
    rule with per m2 limits only, or with a BSA range, is not.
  - **The same adult with a weight and a height.** The per m2 rules are offered as well.
  - **A rule with a per kg and an absolute limit that overlap.** With a weight, the dose dialog shows
    the per kg fields, and the printed order shows the per kg dose.
  - **A patient of 17 without a weight.** Not marked: estimated, and the rules for the age held. Marked
    as an adult: estimated as well, and the adult rules, per kg ones included on the estimated weight.
  - **A patient without an age, marked as an adult.** The adult and all-age rules are offered.
  - **A patient of 17 with a weight, marked as an adult.** The adult rules are offered, not the rules
    for children.
  - **The mark is kept.** Mark a patient of 17 as an adult, reload the page: the mark is still set.
  - **A patient of 15.** The adult switch is not offered.
  - **A child.** Without a weight, the child is estimated as today, and refused when there are no
    normal values. With a weight, on a mg/kg rule, nothing changes. A child of 5 years, with a weight
    and no height, gets the per kg rules and no per m2 rule.
