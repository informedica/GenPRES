# Implementation plan for issue #1598

Rule loading accepts a TPN dose rule whose component limits are in the dilution rule only, and
warns when a dose rule limit cuts into the range of a dilution rule. Drafted 2026-11-11. Line
numbers are from `master` at `ac8f4841`. The production DoseRules and DilutionRules sheets were
fetched on 2026-11-11, after the sheet edits of #1597.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [What the sheets hold](#what-the-sheets-hold)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Open points](#open-points)
- [Confidence](#confidence)
- [Steps](#steps)

## Problem description

A TPN order is limited by two rules. The dilution rule limits the preparation: the quantity per kg
of each component in the bag. The dose rule limits what is given: the dose quantity per kg of each
component. With a dose count of 1 the bag is the dose, so both limits act on the same amount and the
order takes the overlap.

On the production sheet the dilution rule gives most components of Samenstelling B to E a range
around a target, and the dose rule sets the maximum at the target. The order then only allows the
lower part of the range, and the default pick lies below the target. [Plan 504](504-tpn-intake-percentage.md#configuring-a-tpn-order)
puts the component limits in the dilution rule only, so the dose rule has none, but rule loading
drops a dose rule without limits, without a warning. And no load warning says that a dose rule limit
cuts into a dilution rule range.

## What the code does today

### Loading drops a dose rule without limits

- `DoseRule.mapToComponentLimits` (`DoseRule.fs:497`) makes one component limit per component of
  the rows. A component whose rows hold no dose values gets `Limit = None` (`DoseRule.fs:513`, the
  filter on `DoseLimit.hasNoLimits`).
- `DoseRule.removeEmptyLimits` (`DoseRule.fs:685`) then drops each component limit without a limit
  and without substance limits, and drops each dose rule left without component limits.
  `DoseRuleLoader.fromData` calls it last (`DoseRuleLoader.fs:152`). It returns the rules only, so a
  dropped component or rule gives no warning.
- A dropped component is gone from the order as well: `Medication.create` (`Medication.fs:1051`)
  makes the components of an order from `dr.ComponentLimits`. So a dose rule that limits only some
  of its components loses the others, also when a dilution rule limits them.
- The GenFORM test "fromData drops a dose rule whose only limit is empty" (`Tests.fs:866`) holds
  this behaviour.

### The loader does not see the dilution rules

- The resource `doseRules` (`Resources.fs:482`) calls `DoseRuleLoader.fromData` with the route
  mappings, the form routes, the products and the dose rule rows.
- The resource `dilutionRules` (`Resources.fs:505`) is built from the dilution rule rows, the route
  mappings, the parenteral products and the products. It does not depend on the dose rules.

### How the two limits meet in an order

- `Medication.createComponents` sets the limit of a dose rule component as the dose of that
  component (`Dose = lim.Limit`, `Medication.fs:853`).
- `PrescriptionRule.filter` first adjusts the dilution rules to the patient
  (`PrescriptionRule.adjustDilutionRuleToPatient`, `PrescriptionRule.fs:194`, called at line 378):
  with a weight, a quantity per kg is multiplied by the weight and folded into the quantity; without
  a weight the quantity per kg stays as it is and gives no quantity.
- `Medication.addDilutionRule` (`Medication.fs:966`) sets the dilution limit of a component on the
  component as `DilutionLimit`, and takes the quantity as the prescription rule gives it.
- `Medication.doseFractionToDoseCount` (`Medication.fs:945`) turns `MinPerc` and `MaxPerc` into the
  dose count, from 1 / `MaxPerc` to 1 / `MinPerc`; a `MaxPerc` without a `MinPerc` gives at least
  1 / `MaxPerc` with no maximum. With neither, `Medication.create` leaves the dose count empty
  (`Medication.fs:1060`) and the last build step `withDoseCount` (`Medication.fs:1819`) sets it to
  exactly 1. Both are 1 on the rows of Samenstelling B to E, so the dose count is defined as exactly
  1; the electrolyte and glucose rows have only `MaxPerc` 1, so a dose count of at least 1. The percentage
  step `orderableDoseQuantityPercentile` (`OrderProcessor.fs:1029`) sets the dose count back to at
  least 1, so a partial dose works.

### The checks between dose rules and dilution rules

- `DilutionRule.appliesToDoseRule` (`DilutionRule.fs:355`) matches a dilution rule with a dose rule:
  generic, route, department, location, and form, indication and dose type when the dilution rule
  names them. `DilutionRule.canMeetDoseRule` adds that a patient can meet both.
- The resource `ruleChecks` (`Resources.fs:542`) uses this matching for three warnings:
  `competingRules` and `diluentsThatAreComponents` with `canMeetDoseRule`, `undilutableRules` with
  `appliesToDoseRule` only (`DilutionRule.fs:504`). None compares a dose rule limit with a dilution
  limit.

## What the sheets hold

Counted on the production sheets with a script over the CSV rows. A dose rule is a group of rows with
the same generic, form, route, indication, location, department, patient bounds, dose type and dose
text. The script matches a dilution rule to a dose rule by the names on the rows, which approximates
`DilutionRule.canMeetDoseRule`: it compares routes by name, not through the route mappings.

### Dose rules without limits

- 262 of about 4,910 dose rules (4,908 or 4,913, by which patient bounds the count groups on) hold no dose value in any row, so loading drops them all today.
- Most of them have no dilution rule, or none that applies: a vaccine, a cream, an inhalation, or an
  intravenous rule without a dose type, which loading refuses before it gets to the limits.
- Only the electrolyte and glucose mixture, `electrolyt/glucose mengsel`, indication "Parenterale
  suppletie", is set up as plan 504 says. Its dose rules give the schedule (timed, once a day, 20 to
  24 hours) and the components `NaCl 3%` and `KCl 7,4%` without limits, and its dilution rules
  limit the components per kg:

  | Weight | NaCl 3% (mL/kg) | KCl 7,4% (mL/kg) |
  |---|---|---|
  | 3-7 kg | max 6 | max 1.5 |
  | 7-50 kg | max 6 | max 2 |
  | 50 kg and above | none | none |

  So loading drops all five of its dose rules, and the electrolyte and glucose slot can offer none
  of them. The dose rules up to 50 kg have the adjust unit kg, the one from 50 kg has none. For
  50 kg and above the dilution rule limits no component either.
- No dose rule on the sheet limits some of its components and leaves others without a limit.

### Dose rules that cut into a dilution rule

Samenstelling B to E have 14 dose rules. Their dilution rules give the components a range per kg
(`MinQtyPerKg`, `MaxQtyPerKg`): the target ±10% for the Samenstelling component, wider for some
others (C, day 1: `NaCl 3%` 5-7, `KCl 7,4%` 1-3), and a maximum only for Peditrace. The dose rules
set `MaxQtyAdj` at the target, with no minimum. In 42 of the 47 pairs of a dose rule component and a
dilution rule component the dose rule maximum is below the dilution rule maximum. The other five
pairs are Peditrace in Samenstelling D and E, where both maxima are 1 mL/kg. The 14 diluent rows of
the dilution rules are not in a pair, since the diluent is never a dose rule component. Examples:

| Rule | Component | Dilution rule (mL/kg) | Dose rule max | Order allows |
|---|---|---|---|---|
| C, day 1, 7-16 kg | Samenstelling C | 9-11 | 10 | 9-10 |
| C, day 2, 7-16 kg | Samenstelling C | 18-22 | 20 | 18-20 |
| C, day 1, 7-16 kg | KCl 7,4% | 1-3 | 2 | 1-2 |
| E, day 3, 31-50 kg | Samenstelling E | 10.8-13.2 | 12 | 10.8-12 |

All dilution rows of Samenstelling B to E and of the electrolyte and glucose mixture have `MaxPerc`
1; Samenstelling B to E also have `MinPerc` 1.

## Decisions

Decisions of 2026-11-11, halcwb:

1. Loading decides with the dilution rules: the resource `doseRules` also depends on
   `dilutionRules`. A dose rule without limits is kept when a dilution rule that applies to it
   limits its components; otherwise it is dropped, as today.

   The dilution limits determine the dose only when the dose is at most the preparation, so when the
   dose count is at least 1. A dilution rule with a dose count that can be below 1 (`MaxPerc` above
   1, or `MinPerc` above 1) does not keep a dose rule without limits. On the production sheet no
   such rule applies to a dose rule without limits.
2. A dilution rule keeps such a dose rule only when it limits every component of the dose rule. The
   electrolyte and glucose rule from 50 kg stays dropped.
3. A dilution rule can cover only part of the patients of the dose rule. Loading keeps the rule, and
   `PrescriptionRule.filter` decides per patient. `Medication.fromRule` (`Medication.fs:1092`) makes
   one scenario per dilution rule of a prescription rule, so the check is per dilution rule: for a
   dose rule with components without a limit, it keeps only the dilution rules that limit every
   such component, and drops the prescription rule when none is left. Every scenario then has a
   limit on every component.
4. A dropped dose rule gets a warning only when a dilution rule applies to it. The other dropped
   rules, 262 today, stay without a warning.
5. The conflict warning is given only when no order can meet both the dose rule range and the
   dilution rule range of the same component. A dose rule limit that only
   narrows the dilution rule range, as in Samenstelling B to E today, gets no warning.
6. A dilution limit of a component limits that component when it has a quantity, a quantity per kg
   or a list of quantities.
7. A dilution limit per kg limits nothing for a patient without a weight. For such a patient a dose
   rule with components without a limit is dropped, unless another dilution limit of the same
   dilution rule limits them.
8. The dose is the preparation divided by the dose count, so the conflict warning compares the
   ranges over the whole dose count range of the dilution rule: no order can meet both when the
   dose rule minimum is above the dilution rule maximum divided by the dose count minimum, or the
   dose rule maximum is below the dilution rule minimum divided by the dose count maximum. Without a
   dose count maximum the second never holds. With `MaxPerc` 1 and no `MinPerc` a preparation of
   10 mL and a dose maximum of 5 mL give no warning, since a dose count of 2 meets both.
9. Every step checks that it drops no dose rule and dilution rule combination that works today.
10. The check of step 1 runs after the dilution rules are adjusted to the patient
    (`PrescriptionRule.fs:378`), on the quantity and the list of quantities: a quantity per kg is
    then in the quantity when the patient has a weight, and absent otherwise, so decision 7 needs no
    branch of its own.
11. Every caller of `DoseRuleLoader.fromData` passes the dilution rules: `Resources`,
    `Export.forward` (`Export.fs:435`, `provider.GetDilutionRules()`, so that the export round trip
    keeps the rules), `DoseRuleLoader.get` and `getFromGetData`, and the script
    `NLP.Lib/Scripts/DoseRuleExtract.fsx`. The tests pass an empty array, which is the behaviour of
    today for their fixtures.
12. `DoseRuleLoader.fs` compiles after `DilutionRule.fs`, so that `fromData` can call
    `DilutionRule.canMeetDoseRule`. `DoseRule.fs` compiles before `DilutionRule.fs` and
    `DilutionRule.undilutableRules` calls `DoseRule.reconstitute`, so `DoseRule.removeEmptyLimits`
    cannot call it. No file between them uses `DoseRuleLoader`.
13. A kept dose rule without limits also reaches the consumers that do not go through
    `PrescriptionRule.filter`: the formulary (`Formulary.getDoseRules`), the MCP dose rule tools and
    `OrderContext.refusal`. The formulary and the MCP tools show the rule as it is, a schedule
    without a dose. `OrderContext.refusal` gets a check, so that its reason stays right.

## Approaches considered

- **At load, with the dilution rules** (chosen): the loader sees the dilution rules and keeps a dose
  rule whose components they limit. The rules a patient never gets a dilution rule for are dropped
  per patient in `PrescriptionRule.filter`.
- **Keep every rule with a schedule, decide per patient only**: loading keeps every dose rule, and
  `PrescriptionRule.filter` drops a rule that neither the dose rule nor a dilution rule limits. This
  brings 262 rules into every later step and every check, for nothing.
- **No code, only the sheets**: keep a dose rule maximum equal to the dilution rule maximum on the
  sheet, as the scratch scripts of the issue showed. The electrolyte and glucose rules then need dose
  rule limits as well, and the limits stay in two places.

## Chosen approach

### Loading (GenFORM)

- `DoseRuleLoader.fs` moves after `DilutionRule.fs` in the project file (decision 12).
- `DoseRuleLoader.fromData` takes the dilution rules, and the resource `doseRules`
  (`Resources.fs:482`) passes `r.Get Keys.dilutionRules`. There is no cycle: `dilutionRules` does
  not depend on the dose rules. The other callers pass them as well (decision 11).
- `fromData` decides with `DilutionRule.canMeetDoseRule` which dose rules without limits
  `DoseRule.removeEmptyLimits` keeps: those for which a dilution rule that matches limits every
  component of the dose rule. It then keeps the
  components without a limit as well, since an order makes its components from them.
- A dilution rule limits a component when it has a dilution limit with `ComponentLimitTarget` of
  that component name and a quantity, a quantity per kg or a list of quantities, and its dose count
  is at least 1: `MaxPerc` empty or at most 1, and `MinPerc` empty or at most 1.
- A dose rule without limits that a dilution rule applies to, but that is still dropped, gives a
  warning that names the dose rule and the components without a limit.

### Per patient (GenFORM)

- `PrescriptionRule.filter` (`PrescriptionRule.fs:273`, called only from `Api.fs:125`) finds the
  components of the dose rule that have no limit and no substance limits. When there are any, it
  keeps only the dilution rules of the patient that limit every one of them, and drops the
  prescription rule when none is left. Each dilution rule gives its own scenario in
  `Medication.fromRule`, so a dilution rule that does not limit them would give a scenario with a
  component without any limit.
- The check runs after `adjustDilutionRuleToPatient` (`PrescriptionRule.fs:378`) and looks at the
  quantity and the list of quantities. For a patient without a weight a dilution limit per kg then
  gives no quantity, so a dilution rule that limits a component only per kg does not keep the rule
  for that patient.
- For a dose rule with limits this changes nothing: loading drops every component without limits
  from such a rule today.

### The refusal (GenORDER)

- `OrderContext.refusal` (`OrderContext.fs:1641`) reads the dose rules through `Api.filterDoseRules`,
  not through `PrescriptionRule.filter`. Today the electrolyte and glucose context of a patient
  outside ICK gets `NoDoseRules`, since loading drops its dose rules. Once they are kept, it would
  get `NoProducts`, which is not the reason.
- The refusal leaves out a dose rule with components without a limit when no dilution rule of the
  patient limits every such component, as `PrescriptionRule.filter` does, so the reason stays
  `NoDoseRules` or `NoDoseRulesForPatient`.

### The conflict warning (GenFORM)

- A new check in the resource `ruleChecks` (`Resources.fs:542`): for each dilution rule and dose
  rule that `canMeetDoseRule` matches, and each component both limit, a warning when no dose count
  in the range of the dilution rule lets an order meet both (decision 8). A quantity per kg is
  compared with a quantity per kg, a quantity with a quantity, in units of the same group.
- The dose count range is `doseFractionToDoseCount` of `MinPerc` and `MaxPerc`, or exactly 1 when
  the dilution rule has neither, as `withDoseCount` sets it.

## Open points

None. The open points of the draft are decisions 3, 6, 7 and 8.

## Confidence

High: the matching with `canMeetDoseRule` exists, the electrolyte and glucose rows give a real
case for the loading, and every step is compared against the rules of today.

## Steps

One pull request at a time, each merged before the next.

1. **Per patient** (`feat(genform)`): for a dose rule with components without a limit,
   `PrescriptionRule.filter` keeps only the dilution rules of the patient that limit every such
   component, and drops the prescription rule when none is left. Nothing reaches this check today,
   so it lands first: from step 2 on, no scenario has a component without a limit.
2. **Loading** (`feat(genform)`): `fromData` with the dilution rules, `removeEmptyLimits` keeps the
   dose rules their dilution rules limit, the warning for a rule that is still dropped, and the
   resource dependency. The test "fromData drops a dose rule whose only limit is empty" stays, for a
   rule without a dilution rule. New tests load the electrolyte and glucose rows and a Samenstelling
   rule without dose rule limits through `fromData`. On deploy the electrolyte and glucose slot
   offers its rules up to 50 kg. Every caller of `fromData` passes the dilution rules.
3. **The refusal** (`fix(genorder)`): `OrderContext.refusal` leaves out a dose rule with components
   without a limit that no dilution rule of the patient limits. A wrong reason is shown between step
   2 and this step; no order is made either way.
4. **The conflict warning** (`feat(genform)`): the check in `ruleChecks`, over the dose count range
   of the dilution rule.
5. **Plan 504** (`docs`): the DilutionRules sheet and its columns `MinQtyPerKg` and `MaxQtyPerKg`
   in place of the SolutionRules sheet and `MinQtyAdj` and `MaxQtyAdj`, `Medication.addDilutionRule`
   in place of `Medication.addSolution`, and the dose count, which the percentage step sets back to
   at least 1.
6. **Sheets** (the maintainer, after step 2): the dose rule maxima of Samenstelling B to E can be
   removed, so the dilution rule range is the range of the order. This is clinical data validation,
   not part of the code steps.

## Verification

Every code step checks that it drops no dose rule and dilution rule combination that works today.
The check is part of the script of the step and runs on the production and the demo sheets, once
with the code of `master` and once with the code of the step:

- For a set of patients that covers the departments, the weight bands and the ages of the rules
  (with and without a weight, a neonate with a gestational age), list the prescription rules of
  `PrescriptionRule.filter` as pairs of a dose rule and a dilution rule (none when it has none).
- Every pair of `master` is also a pair of the step. A pair that is gone is reported, and the step
  is not migrated until the user has accepted it.
- A new pair is listed as well. Step 2 is expected to add the pairs of the electrolyte and glucose
  rules up to 50 kg, and nothing else.
- Step 3 changes no pair; its tests give the refusal of an electrolyte and glucose context inside
  and outside ICK.
- Step 4 changes no pair. Its warnings on both sheets are listed in the pull request for the user to
  review; the Samenstelling B to E rules give none, since their ranges overlap.

The pull request of the step gives the counts of both runs.

