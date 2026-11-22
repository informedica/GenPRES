# Implementation plan for issues #1210 and #1211: the emergency and continuous medication as GenFORM rules

2026-11-22. Drafted for review; `master` at `15330f7d`.

## Problem description

The emergency list (bolus medication) and the continuous medication list are calculated by stand-alone
code from a separate workbook, not by GenORDER from the GenFORM rules. Both issues are part of #1208:

- **#1210** expresses every bolus entry of the `emergencylist` sheet as GenFORM rules in the main workbook.
- **#1211** expresses every entry of the `continuousmeds` sheet as GenFORM rules in the main workbook.

The reference for both is the baseline of #1209: `tests/Informedica.GenPRES.Shared.Tests/fixtures/emergencylist.tsv`
and `tests/Informedica.GenPRES.Client.Core.Tests/fixtures/continuousmeds.tsv`, with copies of the two sheets
next to them (`emergencylist.csv`, 154 rows; `continuousmeds.csv`, 293 rows).

## Approaches considered

Not decided yet. Each step below is settled with the user before it is worked out.

## Chosen approach

Not decided yet.

## Confidence

Not decided yet.

## Steps

Each step names an issue; how the issue is solved is left for that step.

### Step 1: #1950 a reconstitution keeps its solvent and the substances of the solvent

What the code does now:

- A reconstitution row of the Reconstitution sheet matches on GPK, route, Loc and Dep
  (`Product.Reconstitution.matches`). It is applied only to a product whose form and route the FormRoute
  sheet marks Reconstitute (`Mapping.requiresReconstitution`).
- `Product.reconstitute` replaces the product by one product per matching row. Each substance
  concentration is multiplied by 1 mL and divided by SolventVol plus ExpansionVol, and the form quantity
  becomes that volume. The molar concentration is not changed.
- The solvent itself is lost. The column Solvents is parsed into `Reconstitution.Solvents` and read
  nowhere else; the reconstituted product has the substances of the original product only. So the order
  does not say which solvent was used, and the totals miss what the solvent brings in.
- Every solvent name in the Reconstitution sheet is a parenteral product in the Formulary, marked
  IsSolvent, with the composition columns Energy kCal to VitD IE. `Product.getAdditionalSubstances`
  makes these columns substances, such as "natrium mmol", and `Totals.fs` adds substances up by name.
  Two solvents have a composition: NaCl 0,9% (0.155 mmol sodium and chloride per mL) and gluc 5% (0.05 g
  carbohydrate and 0.2 kCal per mL). 77 of the 536 reconstitution rows name one of them; 18 of these
  also name another solvent, such as water.
- The reconstitution is applied per patient and dose rule in `PrescriptionRule.filter`, and also in the
  load check `SolutionRule.undilutableRules` and in `OrderContext`.
- GenORDER uses `RequiresReconstitution` to count a liquid per mL and to set the substance quantities
  (`Medication.fs`).

Decided on 2026-11-22:

- **Composition from the Formulary.** The composition of a solvent comes from the Formulary row of the
  solvent; the Reconstitution sheet gets no composition columns. The reconstituted product carries the
  substances of the solvent, so that the totals count them.
- **One row per solvent.** The 47 rows that name more than one solvent are split into one row per
  solvent, 589 rows in all, and the column Solvents is renamed Solvent. A product then can have more
  than one reconstitution, one per solvent; today no GPK, route, Loc and Dep has more than one row.
- **The new columns in one workbook.** The column Solvents is renamed Solvent, and the columns MinAge,
  MaxAge, MinWeight, MaxWeight and Notes are added, only in "GenPRES - NewSolutionRules", the new
  production workbook, which is not in use yet. The other workbooks keep the old header, so that they
  keep working with earlier releases; the demo workbook gets the new columns at the release. The parser reads the column Solvent, so a workbook with
  the column Solvents no longer loads its reconstitution rules. The `Reconstitution` record
  documentation (`Types.fs`), the column-contract test and the test fixtures change with the column.
- **The substances of the solvent.** A reconstituted product gets the substances of the parenteral
  product of its solvent (`Product.Parenteral.get`, which leaves out zero values), scaled by SolventVol
  divided by SolventVol plus ExpansionVol. These substances become items of the order, such as natrium
  and chloor in an antibiotic reconstituted with NaCl 0,9%.
- **Solvens and the bicarbonate mixture.** The Formulary rows of solvens (131 reconstitution rows) and
  of the bicarbonate mixture get their composition. In "GenPRES - NewSolutionRules" the mixture is split
  into "1ml natriumbicarbonaat + 5ml NaCl" and "1ml natriumbicarbonaat + 5ml gluc 5%"; the 3 artesunaat
  reconstitution rows that name "1ml natriumbicarbonaat + 5ml NaCl/gluc" then name these two.
- **Age and weight.** The Reconstitution sheet of "GenPRES - NewSolutionRules" has the columns MinAge,
  MaxAge, MinWeight and MaxWeight, so that a reconstitution can apply to an age and a weight range only.
  As in the DoseRules sheet, the age is in days and the weight in grams; this holds for the Predilution
  sheet too.
  The reconstitution then matches the patient as well, in `PrescriptionRule.filter` and `OrderContext`;
  the load check `SolutionRule.undilutableRules` has a patient category instead of a patient.
- **The molar concentration.** A reconstitution scales the molar concentration with the concentration.
- **The order carries the reconstitution.** A reconstituted product carries its reconstitution as data:
  the solvent, the solvent volume and the expansion volume. The product label does not reach the order
  today: the product of an order component (`ComponentProduct`) holds only the GPK as its Id, the form
  quantities and the strengths, and the preparation text is made from the component names and
  quantities (`Order.preparationNames`). So the field goes on the GenFORM product, on `ComponentProduct`
  and in the order Dto, which is serialized (`Order.fs`); the order JSON version goes up, and the
  preparation text shows the reconstitution.
- **One scenario per reconstitution.** The products of one component stay in one order today: an order
  is solved once per combination of product strengths (`Order.splitPerConcentration`) and the results
  are joined into one order (`Order.unionPerConcentration`). Scenarios split only per solution rule
  (`Medication.fromRule`) and per Divisible group (`OrderContext.splitByDivisible`). A product pick
  (`PickedProducts`) and the product Id are the GPK, so two reconstitutions of one GPK cannot be told
  apart. Therefore:
  - a prescription rule is split into one rule per reconstitution of its products, in the way
    `splitByDivisible` splits it, so that each scenario has one reconstitution;
  - a reconstituted product gets an identity beyond the GPK, for example the GPK with the solvent, for
    the product Id and the product pick.

  Each order scenario then has one reconstitution with a fully determined substance content, and the
  preparation can be rebuilt from the order.

Parts of step 1, one pull request each:

1. **Load the new Reconstitution rows.** The `Reconstitution` record and its parser take the column
   Solvent, the age and weight columns and Notes, and leave out a row without a solvent or a solvent
   volume. Nothing uses the new fields yet, so no order changes. A failed parse fails the whole
   resource load (`Resources.LoadEngine`), so after this part only a workbook with the new columns loads;
   the current code cannot load "GenPRES - NewSolutionRules", which no longer has the column Solvents.
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1950Reconstitution.fsx`, deleted by the
   migration.
   Built: 92 changed lines under `src/`. The parser reads the other columns only for a row that has a
   solvent and a solvent volume, so a missing column fails once such a row is there. The four tests of the script that read the
   live sheet stay out of the test project, which does not download; the migration checked them once:
   598 rows load from "GenPRES - NewSolutionRules", and the demo workbook, with the old header, does not
   load.
2. **The age, the weight and the molar concentration.** A reconstitution applies only to a patient in its
   age and weight range. A patient without an age or a weight does not match a range for that value,
   and a patient matched as an adult has the age of 18 years, as in the dose rules. `PrescriptionRule.filter` and `OrderContext` pass the patient. The load check
   `SolutionRule.undilutableRules` uses a reconstitution that can share a patient with both the solution
   rule and the dose rule. A reconstitution scales the molar concentration with the concentration.
   The check whether the rules still apply after a patient data update compares the products of each
   component, so that a different reconstitution gives the notice. The load check pairs a solution rule
   only with a dose rule it can share a patient with (`SolutionRule.canShareDoseRule`).
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1950ReconstitutionPatient.fsx`, deleted by the
   migration.
   Built: 102 changed lines under `src/`. The age and weight ranges of a reconstitution form a patient
   category (`Reconstitution.patientCategory`), so that the load check reuses `PatientCategory.canShare`.
3. **The substances of the solvent.** A reconstituted product gets the substances of its solvent from the
   Formulary, scaled by SolventVol divided by SolventVol plus ExpansionVol. A solvent substance is added
   to the substance of the product with the same name; the substances of a solvent are always those of
   the composition columns. A concentration in another unit group than the one of the product, such as
   mg against mmol, is not added.
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1950SolventSubstances.fsx`, deleted by the
   migration.
   Built: 94 changed lines under `src/`. The `Reconstitution` record gets the field SolventSubstances,
   filled once when the products are built (`Reconstitution.withSolventSubstances`), so that the callers
   of `Product.reconstitute` do not change.
4. **The reconstitution as product data.** A new record `ProductReconstitution` holds a row of the
   Reconstitution sheet (`Rule`) and the substances of its solvent (`SolventSubstances`, moved off the
   sheet row). The product's `Reconstitution` becomes `ProductReconstitution[]`, built when the products
   are built, and `Product.reconstitute` narrows it to the reconstitution a product was made with. The product gets no id beyond the GPK: within one order the
   GPK stays unique, and only the product pick needs to tell two reconstitutions of one GPK apart, which
   it can do by comparing the GPK and the solvent once the order product carries its reconstitution.
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1950ReconstitutionProduct.fsx`, deleted by the
   migration.
   Built: 76 changed lines under `src/`. The parameter fits of `Product.reconstitute` still takes the
   sheet row, so its callers do not change; the test products get their reconstitutions through
   `ProductReconstitution.create`.
5. **One scenario per reconstitution.** A prescription rule is split into one rule per reconstitution of
   its products, as `splitByDivisible` splits it.
   Script: `src/Informedica.GenORDER.Lib/Scripts/Issue1950ReconstitutionSplit.fsx`, deleted by the
   migration.
   Built: 12 changed lines under `src/`. `limitsByDivisible` groups the products on the Divisible and
   the solvent together, so `splitByDivisible` does not change. The rules evaluated without the Divisible
   are not split per solvent, and a product pick still compares the GPK only, until part 6.
6. **The reconstitution in the order.** `ComponentProduct`, the order Dto with a new JSON version, and the
   preparation text. The product pick (`OrderContext.pickedGroups`) compares the GPK and the solvent.

### Step 2: #1554 predilution rules for a liquid, next to the reconstitution rules

A pre-dilution of a liquid product, in the way a powder has a reconstitution step: 1 mL of the product
diluted to a ten times lower concentration, which an order then dilutes again or gives. The issue
describes it for NICU continuous infusions smaller than one compounding step. Step 3 needs it for the
tenfold dilution of the emergency list.

Decided on 2026-11-22:

- **Predilution, a rule of its own.** A pre-dilution is a new kind of rule, Predilution, next to
  Reconstitution and separate from it: its own sheet, its own record and its own parsing. A
  reconstitution dissolves a powder and replaces the product; a predilution dilutes a liquid and adds a
  product. The Reconstitution sheet keeps its columns.
- **Matching.** A predilution matches on GPK, route, Loc and Dep, as a reconstitution does, and also on
  an age range (MinAge, MaxAge), a weight range (MinWeight, MaxWeight) and an indication (Indication). It applies to a liquid; the
  FormRoute mark Reconstitute plays no part.
- **The undiluted product stays.** A predilution adds the pre-diluted product next to the undiluted
  product.
- **The Predilution sheet.** The sheet Predilution of the workbooks "GenPRES - NewSolutionRules" and
  "GenPRES Neo" has the columns GPK, Medication, Form, Route, Indication, Loc, Dep, MinWeight,
  MaxWeight, DiluentVol and Diluent; in "GenPRES - NewSolutionRules" also MinAge and MaxAge. A row names one diluent.
- **The volumes.** A row dilutes 1 mL of product with DiluentVol of diluent: 1 mL with 9 mL gives a
  ten times lower concentration in 10 mL.
- **The molar concentration.** A predilution scales the molar concentration with the concentration, as
  a reconstitution does after step 1.
- **What the order shows.** A pre-diluted product carries the predilution as data: the product volume,
  the diluent and the diluent volume, so that the order text and the preparation view can show it. Its
  label also says it, for example "midazolam 5 mg/mL, 1 mL verdund met 9 mL NaCl 0,9%".
- **Counting.** GenORDER counts a pre-diluted product per mL, as a liquid; an order takes the volume it
  needs from the prepared syringe.
- **Composition from the Formulary.** The pre-diluted product carries the substances of its diluent,
  from the Formulary row of the diluent, as a reconstituted product does after step 1.
- **What the new sheet needs.** A Data record with its column comments (`Types.fs`), a resource in the
  registry (`Resources.fs`) and a column-contract test.

Open, to decide later: which predilutions the load check `SolutionRule.undilutableRules` takes into
account. It has the patient category of a solution rule instead of a patient, so it cannot filter on a
weight and an indication the way an order does.

### Step 3: #1210 the emergency list rules in the rules spreadsheet

Every bolus entry of the `emergencylist` sheet as rules in the regular rules spreadsheet. The issue lists
a dose rule per entry with an emergency indication, the list concentration as product or reconstitution
rule, the tenfold dilution as a solution rule, the volume rounding as a dose quantity increment, and the
`template-*` columns no longer needed. Open question of #1208: resuscitation doses can exceed the
ordinary limits and must not mix into the ordinary rule choice.

The emergency list is the sheet `emergencylist` of the workbook "GenPRES - Noodlijst"; the rules
spreadsheet is "GenPRES - NewSolutionRules". The list has 154 entries.

Decided on 2026-11-22:

- **New rules only where no rule matches exactly.** An existing DoseRules row is used for an entry
  when it has the same generic, route, dose unit and dose: the per kg minimum and maximum both equal
  the list dose, the absolute minimum and maximum are the same, and the patient range covers exactly
  the weight range of the list, with no age or other patient limit. 18 entries match: vaatvulling
  (twice) and NaBic in the resuscitation section, fibrinogeen and protrombinecomplex (APLS), the three
  hydrocortison entries (UMCU), and rocuronium and propofol for intubation at the other hospitals.
  The other 136 entries get new rows.
- **Indication.** A new row gets the indication "Noodlijst" followed by the category of the list, such
  as "Noodlijst intubatie". The list has 27 categories; no ordinary rule has such an indication.
- **Hospital.** The hospital column of the list goes into the column Loc; an entry without a hospital
  leaves Loc empty. A rule with a location matches a patient without a location, so a patient without
  a location meets the rows of every hospital.
- **Source.** A new row has the source NDLST.
- **Dose type.** The dose type of the column template-dose-type, with discontinuous as once, and once
  where the column is empty. The text after the dose type, such as "1ste dosis", goes into DoseText.
- **Dose limits.** As existing rules write them: the list dose as MinQtyAdj and MaxQtyAdj with the
  adjust unit kg, the list minimum as MinQty and the list maximum as MaxQty when not 0. An entry with
  dose 0 is a fixed dose: MinQty and MaxQty without an adjust unit.
- **Administration time.** A new onceTimed row takes MinTime, MaxTime and TimeUnit from the existing
  onceTimed rule of the same generic and route; where no rule has a time, from the remark of the list.
- **Generic names.** Where the list writes a generic differently from GenFORM: NaBic is
  natriumwaterstofcarbonaat, fentytoine is fenytoine, valproaat is valproinezuur, intralipid is
  "Intralipid 20%", coffeine is coffeine citraat, and glucose is glucose.
- **Routes.** An entry without a route, or with a route that contradicts its name, gets one row per
  route in its name: nas is NASAAL, bucc is OROMUCOSAAL, im is INTRAMUSCULAIR. This concerns midazolam
  nas/bucc/im (APLS), midazolam buc/nas/im (UMCU), midazolam bucc (UZA) and lorazepam bucc (UZA, now
  intraveneus).
- **Entries without a product.** Erytrocyten, trombocyten, plasma and ketamine (7 entries) have no
  product that an order can be made from. Their dose rules are written anyway; they give no order
  until a product is added.
- **Volume rounding.** The FormRoute sheet gives steps of 0.1 mL for an injection liquid on every route
  of the list. The list rounds to 1 mL above 10 mL; the order keeps steps of 0.1 mL there, a difference
  that the comparison with the baseline explains.
- **Where the rows are written.** The new rows go into a copy of the rules workbook first. A script
  checks that GenFORM loads the copy and returns the rules for the weights of the baseline; then the
  rows move to "GenPRES - NewSolutionRules", which a running server reads.
- **Concentration.** When the list concentration is the concentration of a product, no extra rule;
  otherwise a SolutionRules row with the list concentration and the diluent, under the same emergency
  indication.
- **The tenfold dilution.** The list divides the concentration by 10 when the dose rounds to 0 mL; in
  the baseline that happens for 32 entries, mostly at 0.5 to 2 kg. It is a preparation in two steps: a
  pre-dilution of the product, then the dilution that is given. The current code cannot express it:
  - a reconstitution rule applies only to a form and route that the FormRoute sheet marks Reconstitute,
    which no liquid is; it replaces the product; and it has no weight, indication or dose type;
  - each solution rule of a prescription rule gives its own medication (`Medication.fromRule`), so two
    solution rules are alternatives, not two steps.

  The entries with the tenfold dilution are built on the predilution rules of step 2.

### Step 4: #1211 the continuous medication rules in the rules spreadsheet

Every entry of the `continuousmeds` sheet as rules in the regular rules spreadsheet. The issue lists a
continuous dose rule per entry with the minimum and maximum dose rate and the absolute maximum, the
standard concentrations per weight range as solution rules with `minconc` and `maxconc` as limits, the
morphine "weight / 2 mg in 50 mL" case as a rule, and the hospital column mapped to department or
location. Open questions of #1208: the rule form of the morphine case and the mapping of the hospital
column.

## Issues that touch the steps

- #1767: a solution rule whose diluent is also a dose rule component is left out without telling the
  prescriber. Step 3 adds solution rules with a diluent to the emergency entries.
- #1640: a PRN dose type. Some doses given on demand are written as once; step 3 gives the bolus
  entries a single-dose dose type.
- #1794: the limits of a component can sit in both the dose rule and the solution rule. Step 4 uses
  `minconc`, `maxconc` and `absmax` as limits.
- #1350: pharmacy preparation instructions with standard preparations per weight range, as step 4 has.
- #1777: a patient has one weight today; both lists choose their rows by weight range.

## Related issues outside this plan

- #1208, the parent issue: solve the emergency and continuous lists through GenORDER.
- #1209 (closed), the baseline of today's calculations.
- #1212, solve the emergency and continuous rules for one patient (script).
- #1213, a command that returns the solved lists; depends on #582.
- #1214, the client renders the solved lists and the stand-alone calculation goes.
- #582, serve the client-side knowledge rules through the server; plan written, no step built.
- #1719, the age in years for the emergency list as GenCORE counts it.
- #1244, every string the user sees through the terms, the print columns of both lists included.
- #1896, an ADR on how GenPRES performs calculations.
- Past symptoms of the two calculations disagreeing (all closed): #434, #471, #911, #1219, #509.
