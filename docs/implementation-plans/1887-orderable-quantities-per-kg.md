# Implementation plan for issue #1887: the orderable quantities per adjust as equations

2026-11-21. Line numbers are from `master` at `186f5b39`.

## Problem description

A solution rule can limit the solution per kg: the volume of the solution (MinVolAdj, MaxVolAdj) and
the quantity of a component or a substance in it (MinQtyPerKg, MaxQtyPerKg). The order has no
variable for such a limit. GenFORM multiplies the per kg limit by the weight in kg and puts the
tighter of that result and the absolute limit in the absolute limit:
`PrescriptionRule.adjustSolutionRuleToPatient` (`GenFORM.Lib/PrescriptionRule.fs:186`, called at line
423). GenORDER then sets that absolute limit on the orderable quantity of the orderable
(`Medication.fs:1529`), the component (`withComponentDilutionLimit`, `Medication.fs:1200`) or the item
(`withItemDilution`, `Medication.fs:1180`).

`Medication.addSolutionRule` (`GenORDER.Lib/Medication.fs:929`) repeats the multiplication for a
component limit (the match at line 985). It runs on a rule that GenFORM has already adjusted with the
same patient data, so it never changes the limit; the match is dead code.

The solver multiplies the dose per adjust by the adjust quantity of the order itself
(`[cmp]_dos_qty = [cmp]_dos_qty_adj * [ord]_adj_qty`, `EquationMapping.fs:157`). The orderable
quantities should work the same way, so that the solver keeps both limits, works in both directions,
and holds the value per adjust.

The solution rule limits are fixed to per kg by the mapper: `VolumeAdjust` in mL per kg
(`SolutionRule.fs:203`) and `QuantityAdjust` in the unit of the row per kg (`SolutionRule.fs:219`). An
order variable takes the unit of the constraint set on it, and the solver derives the other units from
the equations, so GenORDER needs no code for the units. That works only when a limit per adjust has
the adjust unit of the order. A check in FSI of `orb_qty = orb_qty_adj * adj_qty` with `adj_qty` 1 m2
and the limit in mg/kg gives `orb_qty` in mg*m2/kg, and with `orb_qty` limited in mg the solve throws
`ArgumentException: cannot compare ValueUnit`. Today this cannot happen, because GenFORM multiplies by
the weight before the solver sees the limit.

## Approaches considered

1. Keep the multiplication in GenFORM, and delete only the dead match in GenORDER. The smallest
   change, but GenFORM keeps calculating with the weight, and the order never holds the value per
   adjust.
2. Add an orderable quantity adjust variable at each level, with an equation to the orderable
   quantity, as the issue proposes. GenFORM then passes the limits per adjust on unchanged.

## Chosen approach

Approach 2, with the decisions recorded in the issue and in review:

- **Three variables.** `[orb]_orb_qty_adj`, `[cmp]_orb_qty_adj` and `[itm]_orb_qty_adj`, each with the
  equation `[x]_orb_qty = [x]_orb_qty_adj * [ord]_adj_qty`, for all dose types.
- **Adjust unit column.** The SolutionRules sheet gets an AdjustUnit column, like the dose rules. It
  holds one adjust unit per solution rule, for the volume per adjust and for every dilution limit per
  adjust of the rule, so all rows of one rule have the same value. A row with a limit per adjust needs
  the adjust unit: the mapper reads the limits per adjust in the adjust unit of the rule, and leaves
  them out without one. Nothing in the code assumes kg.
- **Adjust unit of a dose rule and solution rule pair.**
  - Only the dose rule has an adjust unit: the pair uses it.
  - Only the solution rule has an adjust unit: the pair uses it.
  - Both have one and they are the same: the pair uses it.
  - Both have one and they differ: a load check reports the pair as a sheet warning, and the solution
    rule does not apply to that dose rule.
  - Neither has one: the order has no adjust.
  - A solution rule without an adjust unit has no limits per adjust; a row with a limit per adjust and
    no adjust unit is a sheet error, and the limit is left out.
- **Adjust unit of the order.** The order takes the adjust unit of the pair. `Medication.fromRule`
  (`Medication.fs:1047`) no longer falls back to kg: without an adjust unit, `Medication.Adjust` stays
  `None`.
- **Equation selection.** `getEqsMapping` selects two groups of adjust equations independently:
  - the dose adjust equations (the 25 `[x]_dos_*_adj` equations there are today) only when an adjusted
    dose has a limit, which is what `usesAdjust` (`EquationMapping.fs:222`) checks today;
  - the three orderable quantity adjust equations only when an orderable quantity adjust variable has a
    limit.

  An order whose only limit per adjust is a solution limit gets no dose adjust equations, so no
  adjusted dose is calculated. Two other callers of `usesAdjust` keep looking only at the adjusted
  doses: `UseAdjust` (`OrderContext.fs:205`, whether the table and the client show doses per kg or per
  m2) and `hasNoAdjustedDose` (`OrderContext.fs:1831`, the `NoAdjustedDose` refusal). `Readjust` skips
  only an order with neither an adjusted dose limit nor an orderable quantity adjust limit, and sets
  back the orderable quantity adjust variables with the adjusted doses.
- **Wire only.** The three variables go into the order Dto, the wire types and the server mapper. The
  client receives them and shows nothing new.

## Confidence

Medium. The equations are of a form the solver already handles for the doses. The risk is in the
solve of orders with more components, such as parenteral nutrition, where three more equations per
order could change the values the solver offers or the solve time. Every step runs the full GenORDER
and server test suites, including the golden orders.

## Steps

Each step is one pull request, script first, and stays well under the 200-line limit on shipped code
under `src/`. Steps 1 to 5 set up the adjust unit. After that, each level gets two steps: first the
variable and its equation in the order model, with no limit set, then the limit from the solution
rule. Until step 13, GenFORM still multiplies the limit by the patient value, so during steps 7 to 12
an order holds a limit per adjust twice: as the absolute limit GenFORM computed, and as the new
variable per adjust. Both give the same bound.

Orders change only in step 3: a solution rule no longer applies to a dose rule with another adjust
unit, and a row with a limit per adjust loses it while the sheet gives it no adjust unit.

### Step 1: the AdjustUnit column in the solution rule sheet (sheet)

- Done in the new rules sheet "GenPRES - NewSolutionRules": the AdjustUnit column follows the Unit
  column, and all 38 columns of the column-contract test are present. The mapper reads columns by name,
  so the current code ignores the new column.
- 89 rows hold kg; each has a volume and a quantity limit per adjust. No row holds an adjust unit
  without a limit per adjust, and no row holds m2. All rows of one rule have the same adjust unit.
- 56 rows have a limit per adjust and no adjust unit (calcium/magnesium, Na/K samenstelling and
  natrium/kalium). Every dose rule they meet adjusts by kg, so every pair takes kg by the rules in the
  chosen approach.
- No code change.

### Step 2: read the AdjustUnit of a solution rule (GenFORM)

- The mapper reads the AdjustUnit column (`SolutionRule.fs:114`), parsed like the dose rule column
  (`Units.adjustUnit`, `DoseRule.fs:631`), into the `SolutionRule`, as one value for the rule.
- The `Data` record gets the field with its `///` comment (`Types.fs:1152`), the column-contract test
  gets the column (`tests/Informedica.GenFORM.Tests/Tests.fs:1106`), and the tests that build the `Data`
  record get the field (`Tests.fs:6435`, `:8457`, `:9323`).
- The column is required, like every other column of the sheet: a sheet without it fails the parse.
  Every sheet the app reads, demo and production, needs the column before this step is released.
- No order changes: nothing uses the adjust unit yet.
- Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1887SolutionRuleAdjustUnit.fsx`.
- Estimate: 20 to 40 lines.

### Step 3: the limits per adjust in the adjust unit of the solution rule (GenFORM)

- The mapper reads `SolutionLimit.VolumeAdjust` and `DilutionLimit.QuantityAdjust` in the adjust unit
  of the rule instead of the fixed kg (`SolutionRule.fs:203`, `:219`). A rule without an adjust unit gets
  no limits per adjust, because `MinMax.fromTuple` gives an empty range without a unit.
- `SolutionRule.forDoseRule` leaves out the solution rules whose adjust unit differs from the adjust unit
  of the dose rule. The prescription rule filter calls it after the renal rules, because a renal rule
  can give a dose rule without an adjust unit one (`RenalRule.adjustDoseRule`). The filter selects the
  solution rules with `SolutionRule.filter`, not with `canMeetDoseRule`; `canMeetDoseRule`
  (`SolutionRule.fs:442`) serves only the load checks and gets the check in step 4.
- `adjustSolutionRuleToPatient` multiplies by the patient value for the adjust unit of the solution rule,
  `PatientData.adjustValue` (`Patient.fs:514`), the weight or the body surface area, so the absolute
  limit stays correct until step 13 removes the multiplication.
- `isPrescribable` derives the measurement from the unit of the limit (`RenalRule.measureOf`), so it
  asks for the body surface area without a code change.
- The `///` comments on the limits per adjust say per AdjustUnit: the `SolutionRuleData` fields
  MinVolAdj, MaxVolAdj, MinQtyPerKg and MaxQtyPerKg, `SolutionLimit.VolumeAdjust` and
  `DilutionLimit.QuantityAdjust`.
- Before release, every row with a limit per adjust has an adjust unit in every sheet the app reads:
  56 rows in the new rules sheet (calcium/magnesium, Na/K samenstelling, natrium/kalium) and the rows
  of the demo sheet. A row without one loses its limits per adjust. This is the maintainer's step.
- Tests: the mapper reads the limits per kg and per m2 and leaves them out without an adjust unit;
  `forDoseRule` for every combination of adjust units; the filter leaves out a solution rule per kg for
  a dose rule per m2; the absolute limit is the limit times the weight or times the body surface area.
- Estimate: 30 to 50 lines.

### Step 4: the load checks of the adjust unit (GenFORM)

All checks are warnings in the resource messages, like the other rule checks.

- `adjustUnitsMatch` holds when the adjust units of a dose rule and a solution rule are the same, or one
  of them has none. `forDoseRule` and `canMeetDoseRule` (`SolutionRule.fs:442`) both use it, so the load
  checks that use `canMeetDoseRule` (`anyLimitsDoseRule`, `conflictingLimits`, `unlimitedDoseRules`)
  agree with the prescription rule filter of step 3.
- `canShareDoseRule` holds when a solution rule applies to a dose rule and can meet a patient of that
  dose rule. `canMeetDoseRule` adds `adjustUnitsMatch` to it, and `adjustUnitMismatches` warns when it
  holds but the adjust units differ, so the solution rule does not apply.
- `ruleOfRow` maps one row on its own to its rule, for the checks on the rows: `missingDiluents`,
  `diluentRowsWithoutDiluent` and the two below.
- `missingAdjustUnits` warns for a solution rule with a row that has a volume or a quantity per adjust
  but no adjust unit the sheet can read, so the limit is left out.
- `mixedAdjustUnits` warns for the rows of one solution rule, the rows with the same rule fields apart
  from the adjust unit, that give different adjust units, so they form separate rules.
- `pairConflicts` says "quantity per kg" or "quantity per m2" by the adjust unit of the solution rule,
  instead of the fixed "quantity per kg" (`SolutionRule.fs:554`).
- On the live sheets, `missingAdjustUnits` reports 32 rules of the demo sheet (Samenstelling B to E,
  Primene, NICU mix) and 2 of the new rules sheet (calcium/magnesium); `mixedAdjustUnits` reports none.
- Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1887AdjustUnitChecks.fsx`.
- Estimate: 60 to 90 lines; built in 140 changed lines.

### Step 5: the adjust unit of the order (GenORDER)

- `Medication.create` takes the adjust unit of the pair: the adjust unit of the dose rule, else the
  adjust unit of the solution rule of the medication. It loses its adjust unit parameter, so each
  medication of `Medication.fromRule` adjusts by its own pair. The kg fallback in `fromRule` goes:
  without an adjust unit, `Medication.Adjust` stays `None`.
- The patient value comes from `PatientData.adjustValue`, which GenFORM uses for the same choice.
- Tests: an order of a dose rule without an adjust unit and a solution rule per m2 adjusts by body
  surface area, and one per kg by the weight; an order without an adjust unit has no adjust quantity;
  each medication takes the adjust unit of its own solution rule; the golden orders stay the same.
- The plan test of a workbench order at another weight used a rule without an adjust unit, which
  followed the weight only through the kg fallback. It now uses a rule per kg.
- Built: 143 changed lines under `src/`.

### Step 6: the component quantity per adjust in the order model (GenORDER)

- Add `OrderableQuantityAdjust` to the component (`Types.fs`), with its creation, its order variables,
  its constraints and its Dto in `Order.fs`.
- Add `[cmp]_orb_qty = [cmp]_orb_qty_adj * [ord]_adj_qty` to `EquationMapping.equations`.
- `getEqsMapping` selects the dose adjust equations and the orderable quantity adjust equations
  independently, as in the chosen approach. `usesAdjust` stays as it is for its other callers.
- The wire: add the variable to the wire Component (`Shared/Types.fs:290`), to
  `Models.Order.Component.create` (`Shared/Models.fs:882`) and to the server mapper
  (`ServerApi.Mappers.Order.fs:189`, `:205`). The server rebuilds the GenORDER Dto from the wire
  field by field, and a Dto variable without a name does not read, so the variable goes on the wire
  in the step that adds it. It does not go into `ItemProperty`; the client shows nothing new.
- The equations list holds every equation, the tie equations and the component rate equation included.
  `getEqsMapping` recognises the tie equations, the component rate equation and the orderable quantity
  adjust equation by their text.
- `OrderVariableReference` gets `ComponentOrderableQuantityAdjust`, so every variable of an order has a
  reference.
- `Readjust` sets back `[cmp]_orb_qty_adj` with the adjusted doses, so after a weight change the order
  keeps its component quantity and calculates the new value per adjust unit. It skips only an order with
  neither an adjusted dose limit nor a component limit per adjust unit.
- The stored order plan JSON has the new variable, so its structure version goes from 16 to 17. A row
  of version 16 loads as unreadable.
- The equations table of the tests, a copy of the Equations sheet, gets the new equation as row 69. The
  Equations sheet itself needs the same row by hand.
- Tests: the Dto round trip with the new variable; an order with only a limit on the new variable gets
  the orderable quantity adjust equation and no dose adjust equations; a limit on the new variable
  leaves the other equations as they are; a limit of 1 mL/kg bounds the orderable quantity. The golden
  orders print one more line per component, the new variable without a value; no other line changes.
- Built: 143 changed lines under `src/`.

### Step 7: the component limit per adjust (GenORDER)

- `withComponentDilutionLimit` (`Medication.fs:1200`) sets `DilutionLimit.QuantityAdjust` as the
  minimum and maximum of the new variable.
- Delete the dead match in `Medication.addSolutionRule` (`Medication.fs:985`), with the sentence about
  the weight in its comment. The match is the only use of the `patientData` parameter, so the parameter
  goes as well, with its argument at the call (`Medication.fs:1041`). The match is dead because GenFORM
  (`PrescriptionRule.adjustSolutionRuleToPatient`) already sets the absolute limit whenever the patient
  has the value for the adjust unit of the rule. The match acts only for a rule per m2 with a known
  weight and no body surface area, and then it wrongly multiplies the limit by the weight. That rule does
  not reach an order either: `PrescriptionRule.isPrescribable` leaves it out, because a limit per m2
  needs the body surface area.
- The medication type does not change: `ProductComponent.Dilution` and `SubstanceItem.Dilution` hold the
  GenFORM `DilutionLimit`, which has `QuantityAdjust`, and the medication Dto and
  `docs/domain/medication.schema.json` already carry it. The same holds for step 9.
- Tests: a component with only a limit per adjust gets that limit times the adjust quantity on its
  orderable quantity, for an order that adjusts by weight and for one that adjusts by body surface
  area; such an order shows no doses per adjust (`UseAdjust` stays false).
- Built: 49 changed lines under `src/`.

### Step 8: the item quantity per adjust in the order model (GenORDER)

- As step 6, for the item: `OrderableQuantityAdjust` on the item, the equation
  `[itm]_orb_qty = [itm]_orb_qty_adj * [ord]_adj_qty`, the orderable quantity adjust selection, and the
  wire Item (`Shared/Types.fs:256`).
- The stored order plan JSON gets the new variable, so its structure version goes from 17 to 18, with a
  version 18 fixture and a test that a version 17 row loads as unreadable.
- `OrderVariableReference` gets `ItemOrderableQuantityAdjust`, and `Readjust` sets back `[itm]_orb_qty_adj`
  with the component quantities per adjust unit and the adjusted doses.
- The orderable quantity adjust equations of the component and the item are selected together, so an order
  with only a component limit per adjust unit also gets the item equation, without a constraint on the item
  variable. The solver then calculates the item value per adjust unit for such an order.
- The equations table of the tests gets the new equation as row 70. The Equations sheet itself needs the
  same row by hand.
- The golden orders print one more line per item, the new variable without a value.
- Built: 65 changed lines under `src/`.

### Step 9: the item limit per adjust (GenORDER)

- `withItemDilution` (`Medication.fs:1180`) sets the limit per adjust of a substance
  (`DilutionTarget.Substance`) on the new variable.
- Tests as in step 7, for a substance limit.
- Estimate: 20 to 40 lines.
- Built: 11 changed lines under `src/`.

### Step 10: the volume per adjust on the medication (GenORDER)

- Add `QuantityAdjust` to the medication (`Types.fs:651`), set from `SolutionLimit.VolumeAdjust` in
  `addSolutionRule`. Today `addSolutionRule` takes only `SolutionLimit.Volume` into `Quantity` and drops
  the volume per adjust.
- Add the field to the medication Dto (`toDto`, `fromDto`) and regenerate
  `docs/domain/medication.schema.json` (`MedicationSchema.fsx --write`). `MedicationText` writes and
  reads the medication through the Dto, so it needs no change of its own.
- Tests: the medication text round trip and the Dto round trip with the new field.
- Estimate: 30 to 50 lines.

### Step 11: the orderable quantity per adjust in the order model (GenORDER)

- As step 6, for the orderable: `OrderableQuantityAdjust` on the orderable, the equation
  `[orb]_orb_qty = [orb]_orb_qty_adj * [ord]_adj_qty`, the orderable quantity adjust selection, and the
  wire Orderable (`Shared/Types.fs:308`).
- The stored order plan JSON gets the new variable, so its structure version goes from 18 to 19, with a
  version 19 fixture and a test that a version 18 row loads as unreadable.
- Estimate: 50 to 70 lines.

### Step 12: the orderable limit per adjust (GenORDER)

- The medication `QuantityAdjust` sets the minimum and maximum of `[orb]_orb_qty_adj`.
- Tests as in step 7, for a volume limit.
- Estimate: 20 to 40 lines.

### Step 13: GenFORM passes the limits per adjust on (GenFORM)

- `adjustSolutionRuleToPatient` no longer multiplies `SolutionLimit.VolumeAdjust` and
  `DilutionLimit.QuantityAdjust` by the weight or the body surface area. If nothing is left for it to
  do, it is deleted with its call (`PrescriptionRule.fs:423`). The adjust unit of step 3 stays.
- `SolutionRule.withoutPatientAmounts` (`SolutionRule.fs:781`, used at `PrescriptionRule.fs:229`) only
  undoes that multiplication for the rule comparison, so it goes with it.
- Nothing else reads the absolute limit that GenFORM computed: `Check.fs` reads only the dose limit
  `QuantityAdjust`, and neither the formulary export nor the MCP tools read the solution limits. The
  solution rule checks in `SolutionRule.fs` (`boundsPreparation` at line 347, `openDoseCounts` at 356,
  `canLimitComponent` at 494, `pairConflicts` at 540) run on the rules as loaded.
- Tests: no test calls `adjustSolutionRuleToPatient` directly; the tests of the prescription rules
  with a solution limit per adjust change to the limit per adjust; the GenORDER golden orders stay
  the same.
- Estimate: 20 to 50 lines, mostly deleted.

### Step 14: the wire (Shared, Server)

- Done in steps 6, 8 and 11: each variable goes on the wire in the step that adds it to the order model.

### Step 15: the column names (GenFORM)

- Rename MinQtyPerKg and MaxQtyPerKg to MinQtyAdj and MaxQtyAdj, like MinVolAdj and MaxVolAdj: the
  mapper (`SolutionRule.fs:114`), the `Data` record fields and their `///` comments (`Types.fs:1152`),
  the column-contract test (`tests/Informedica.GenFORM.Tests/Tests.fs:1106`), and the tests that build
  the `Data` record (`Tests.fs:6435`, `:8457`, `:9323`).
- Estimate: 10 to 20 lines.

### Step 16: the data sheet columns (solution rule sheet)

- Rename the columns MinQtyPerKg and MaxQtyPerKg to MinQtyAdj and MaxQtyAdj in the solution rule sheet.
- The sheet is live configuration, so the rename and the release of step 15 go together: before the
  rename, the mapper of step 15 finds no MinQtyAdj and MaxQtyAdj columns.
- No code change.
