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
   Script: `src/Informedica.GenORDER.Lib/Scripts/Issue1950ReconstitutionOrder.fsx`, deleted by the
   migration.
   Built: 177 changed lines under `src/`. `ComponentProduct` gets `Reconstitution`, a
   `ComponentReconstitution` with the solvent, the solvent volume and the expansion volume, filled from the
   product's single reconstitution (`Medication.reconstitutionOf`). The medication Dto, the order Dto and the
   Shared type carry it. The JSON version stays 19: the field is optional, and a stored order without it
   reads as before. The preparation text comes from `Order.Print.printPrep`, not from
   `Order.preparationNames`; it gets the row "oplossen in" with the solvent volume and the solvent when
   exactly one product of a component applies. A product pick id is the GPK, followed by the solvent for a
   reconstituted product (`Order.pickId`), in `Order.productIds` and in `pickedGroups`.
   Follow-up: the preparation row reads "oplossen in 10 mL NaCl 0,9%" in one cell, without value markup, and
   the scenario header reads "poeder voor injectievloeistof in 10 mL NaCl 0,9%".
   The Shared scenario gets the text as Reconstitution; the server mapper makes it from the order
   (`Order.reconstitutionText`), so the stored order plan JSON does not change.
   Script: `src/Informedica.GenORDER.Lib/Scripts/Issue1950ReconstitutionForm.fsx`, deleted by the migration.

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
- **Counting.** Replaced on 2026-11-23 by counting per prepared syringe, see below.
- **Composition from the Formulary.** The pre-diluted product carries the substances of its diluent,
  from the Formulary row of the diluent, as a reconstituted product does after step 1.
- **What the new sheet needs.** A Data record with its column comments (`Types.fs`), a resource in the
  registry (`Resources.fs`) and a column-contract test.

Decided on 2026-11-23:

- **Reconstitution, then predilution.** A product can be reconstituted, then pre-diluted, and the order
  then dilutes the pre-diluted product: three steps. A predilution applies to a liquid, and a
  reconstituted product is a liquid, so the predilution is applied to the products that the
  reconstitution returns. A Predilution row for a powder names the GPK of the powder and applies only
  after a reconstitution of that powder. The sample rows of "GenPRES - NewSolutionRules" have such a
  row: nitroprusside infusiepoeder 60 mg (GPK 185663) is reconstituted with 3 mL water to 20 mg/mL, then
  1 mL is diluted with 9 mL gluc 5% to 2 mg/mL in 10 mL.
- **Counted per prepared syringe.** GenORDER counts a pre-diluted product per prepared syringe of 1 mL
  plus DiluentVol, as it counts a reconstituted product per vial. The form quantity of a pre-diluted
  product is that prepared volume.
- **The substances.** A pre-diluted product carries the substances of the product, of the
  reconstitution solvent when the product was reconstituted, and of the predilution diluent; the order
  adds the substances of its own diluent. The substances of the product, with those of the solvent, are
  multiplied by 1 mL and divided by the prepared volume; the substances of the predilution diluent are
  multiplied by DiluentVol and divided by the prepared volume.
- **Product pick, scenario and text.** A product carries both its reconstitution and its predilution.
  The product pick id and the split into scenarios take the predilution into account next to the
  solvent, and the preparation text has a row for each step, such as "oplossen in 3 mL water" and
  "1 mL verdunnen met 9 mL gluc 5%".

What the code does now, checked on 2026-11-23 on `master` at `84ba2f08`:

- A product whose form and route the FormRoute sheet marks Reconstitute gets the form unit mL when it is
  built (`Product.map`). The form unit therefore does not tell a powder from a liquid: nitroprusside
  infusiepoeder 60 mg is 60 mg/mL in 1 mL before its reconstitution.
- `Product.reconstitute` replaces a product that requires reconstitution by one product per matching
  reconstitution, and leaves it out with a warning when none matches. After the reconstitution, every
  product of a dose rule is a liquid.
- `DoseRule.reconstitute` applies the reconstitution to the products of a dose rule. It has three callers:
  `PrescriptionRule.filter`, `OrderContext` (the check for a dose rule without products) and the load
  check `SolutionRule.undilutableRules`.
- `Medication.createComponents` counts a component per mL when its form quantities are volumes and not
  all of its products require reconstitution; the component quantity and the form quantities of its
  products are then 1 mL. It fills the substance quantities only when a product requires
  reconstitution. A pre-diluted liquid does not require reconstitution, so it would be counted per mL.
- `Medication.reconstitutionOf` returns a reconstitution only for a product that requires
  reconstitution.
- A product pick id is the GPK, followed by the solvent for a reconstituted product (`Order.pickId`,
  used in `Order.productIds` and `OrderContext.Helpers.pickedGroups`). A pre-diluted product has the GPK
  of its undiluted product, so the two get the same id.
- `OrderContext.Helpers.limitsByDivisible` splits the products of a component limit on the Divisible
  and the solvent. A pre-diluted product has the Divisible and the solvent of its undiluted product, so
  the two stay in one component.
- `Order.Print.printPrep` has the row "oplossen in" for a reconstituted product (`printReconstitution`)
  and no row for another preparation step.

Script of 2026-11-23: `src/Informedica.GenFORM.Lib/Scripts/Issue1554PredilutionRows.fsx` loaded the
predilutions and applied them after the reconstitution; #1970 migrated and deleted it.

Prototype, not for merging: the worktree branch `proto/1554-genorder-predilution` is based on the branch
of part 1 below without the product volume, and uses the GenFORM of #1970. It adds the order side, 156
lines added and 16 removed under `src/` in 7 files: the call in `PrescriptionRule.filter`, the counting
per prepared syringe, the pick id, the split into scenarios, the preparation row and the Dtos. It has
no scenario caption, no Shared type and no server mappers, it counts no maximum of one syringe, and its
test projects do not compile. It prints orders with
`src/Informedica.GenORDER.Lib/Scripts/Issue1554PredilutionOrders.fsx` against the live sheets of
"GenPRES - NewSolutionRules". For an extremely premature infant of 1 week, 25 weeks gestational age and
600 g, on the NEO, with the indication "Continue infusie neonatologie", it prints the product volume of
the order without and with the predilution of 1 mL with 9 mL gluc 5%:

| Medication | Undiluted product | Pre-diluted product |
|---|---|---|
| nitroprusside, after "oplossen in 3 mL water" | 0,1 - 0,6 mL of 20 mg/mL | 0,5 - 6,9 mL of 2 mg/mL |
| noradrenaline | 0,1 - 3,4 mL of 1 mg/mL | 0,9 - 23,9 mL of 0,1 mg/mL |
| dopamine | 0,1 - 0,8 mL of 40 mg/mL | 0,5 - 8,6 mL of 4 mg/mL |
| alprostadil | 0,1 - 1,4 mL of 0,5 mg/mL | 0,4 - 14,4 mL of 0,05 mg/mL |

Without the predilution, the product volume of nitroprusside and dopamine stays below 1 mL. The
scenario with the undiluted product stays next to the scenario with the pre-diluted product. The
noradrenaline and alprostadil orders go above the prepared 10 mL, which the maximum of one syringe below
prevents. The scenarios without the products of the Predilution sheet, such as noradrenaline
infusievloeistof, do not change.

The predilution rules, their matching and `Product.predilute` were merged in #1970.

Decided on 2026-11-23, for the order side:

- **The product carries its predilution.** The product gets the field `Predilution: ProductPredilution
  option`: None for a product that is not pre-diluted, and the predilution it was made with for a
  pre-diluted product.
- **Only the prescription rules are pre-diluted.** `PrescriptionRule.filter` calls `DoseRule.predilute`
  after `DoseRule.reconstitute`. The check for a dose rule without products in `OrderContext` does not,
  because a predilution only adds products. The load check `SolutionRule.undilutableRules` stays open,
  see below.
- **The pick id.** The pick id of a pre-diluted product is the GPK, followed by the solvent when the
  product is reconstituted, and then the predilution: "185663 water 1 mL + 9 mL gluc 5%".
- **The scenario caption.** The caption of a scenario adds the predilution after the reconstitution:
  "infusiepoeder in 3 mL water, 1 mL verdund met 9 mL gluc 5%".
- **The product volume.** A predilution rule has the column MedVol, required like DiluentVol: MedVol mL of
  the product is diluted with DiluentVol mL of diluent, in a prepared volume of MedVol plus DiluentVol. A
  rule without MedVol, or with 0 mL or less, is left out. This replaces the fixed 1 mL of 2026-11-22; the
  label, the pick id, the caption and the preparation row name MedVol instead of 1 mL, for example "3 mL
  verdund met 7 mL gluc 5%". The sample rows of "GenPRES - NewSolutionRules" have MedVol 1 mL, and 3 mL
  with 7 mL gluc 5% for nitroprusside.
- **One prepared syringe.** An order takes at most the prepared volume of a pre-diluted product, MedVol plus
  DiluentVol. For a higher dose, the scenario with the undiluted product remains.

Parts of the order side, one pull request each:

1. **The predilution on the product.** The field `Predilution` on the product; every place that builds a
   product sets None, and `Product.predilute` sets the predilution on the pre-diluted product. The column
   MedVol on the predilution rule. Nothing calls `DoseRule.predilute` yet, so the orders do not change.
2. **The predilution in the order.** GenORDER and the server: the call in `PrescriptionRule.filter`, the
   counting per prepared syringe with at most one syringe, the pick id, the split into scenarios, the
   preparation row, the scenario caption and the Dtos. Scripted against the GenFORM of part 1.

Open, to decide later:

- Which predilutions the load check `SolutionRule.undilutableRules` takes into account. It has the
  patient category of a solution rule instead of a patient, so it cannot filter on a weight and an
  indication the way an order does.

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
    which no liquid is; it replaces the product; and it has an age and a weight range since step 1, but
    no indication or dose type;
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
