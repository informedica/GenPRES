# Implementation plan for issues #1210 and #1211: the emergency and continuous medication as GenFORM rules

2026-11-22. Drafted for review; `master` at `15330f7d`.

## Problem description

The emergency list (bolus medication) and the continuous medication list are calculated by stand-alone
code from a separate workbook, not by GenORDER from the GenFORM rules. Both issues are part of #1208:

- **#1210** expresses every bolus entry of the `emergencylist` sheet as GenFORM rules in the main workbook.
- **#1211** expresses every entry of the `continuousmeds` sheet as GenFORM rules in the main workbook.

The reference for both is the baseline of #1209: `tests/Informedica.GenPRES.Shared.Tests/fixtures/emergencylist.tsv`
and `tests/Informedica.GenPRES.Client.Core.Tests/fixtures/continuousmeds.tsv`, with copies of the two sheets
next to them (`emergencylist.csv`, 154 rows; `continuousmeds.csv`, 293 rows). The baseline goes when the client
calculation it covers goes.

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
- A solvent is a parenteral product in the Formulary, marked IsSolvent, with the composition columns
  Energy kCal to VitD IE. `Product.getAdditionalSubstances` makes these columns substances, such as
  "natrium mmol", and `Totals.fs` adds substances up by name.
- The reconstitution is applied per patient and dose rule in `PrescriptionRule.filter`, and also in the
  load check `SolutionRule.undilutableRules` and in `OrderContext`.
- GenORDER uses `RequiresReconstitution` to count a liquid per mL and to set the substance quantities
  (`Medication.fs`).

Decided on 2026-11-22:

- **Composition from the Formulary.** The composition of a solvent comes from the Formulary row of the
  solvent; the Reconstitution sheet gets no composition columns. The reconstituted product carries the
  substances of the solvent, so that the totals count them.
- **One row per solvent.** A reconstitution row names one solvent in the column Solvent, which replaces
  the column Solvents. A product then can have more than one reconstitution, one per solvent.
- **The new columns.** The parser reads the column Solvent and the columns MinAge, MaxAge, MinWeight,
  MaxWeight and Notes, so a workbook with the column Solvents no longer loads its reconstitution rules.
  The `Reconstitution` record documentation (`Types.fs`), the column-contract test and the test fixtures
  change with the columns. Content update: the rules workbooks get the new columns.
- **The substances of the solvent.** A reconstituted product gets the substances of the parenteral
  product of its solvent (`Product.Parenteral.get`, which leaves out zero values), scaled by SolventVol
  divided by SolventVol plus ExpansionVol. These substances become items of the order, such as natrium
  and chloor in an antibiotic reconstituted with NaCl 0,9%.
- **Age and weight.** A reconstitution rule has the columns MinAge, MaxAge, MinWeight and MaxWeight, so
  that a reconstitution can apply to an age and a weight range only.
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
   the code before this part cannot load a workbook without the column Solvents.
   Script: `src/Informedica.GenFORM.Lib/Scripts/Issue1950Reconstitution.fsx`, deleted by the
   migration.
   Built: 92 changed lines under `src/`. The parser reads the other columns only for a row that has a
   solvent and a solvent volume, so a missing column fails once such a row is there. The tests of the
   script that read the live sheet stay out of the test project, which does not download.
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
diluted to a ten times lower concentration, which an order then dilutes again. The issue describes it
for NICU continuous infusions smaller than one compounding step. On 2026-11-22 step 3 was thought to
need it for the tenfold dilution of the emergency list; on 2026-11-23 that became a solution rule, see
step 3.

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
- **The Predilution sheet.** The parser reads the columns GPK, Route, Indication, Loc, Dep, MinAge,
  MaxAge, MinWeight, MaxWeight, DiluentVol and Diluent. A row names one diluent. Content update: the
  rules workbooks get the sheet.
- **The volumes.** A row dilutes 1 mL of product with DiluentVol of diluent: 1 mL with 9 mL gives a
  ten times lower concentration in 10 mL.
- **The molar concentration.** A predilution scales the molar concentration with the concentration, as
  a reconstitution does after step 1.
- **What the order shows.** A pre-diluted product carries the predilution as data: the product volume,
  the diluent and the diluent volume, so that the order text and the preparation view can show it. Its
  label also says it, for example "midazolam 5 mg/mL, 1 mL verdund met 9 mL NaCl 0,9%".
- **Counting.** Replaced on 2026-11-23: counted per mL, see below.
- **Composition from the Formulary.** The pre-diluted product carries the substances of its diluent,
  from the Formulary row of the diluent, as a reconstituted product does after step 1.
- **What the new sheet needs.** A Data record with its column comments (`Types.fs`), a resource in the
  registry (`Resources.fs`) and a column-contract test.

Decided on 2026-11-23:

- **Reconstitution, then predilution.** A product can be reconstituted, then pre-diluted, and the order
  then dilutes the pre-diluted product: three steps. A predilution applies to a liquid, and a
  reconstituted product is a liquid, so the predilution is applied to the products that the
  reconstitution returns. A Predilution row for a powder names the GPK of the powder and applies only
  after a reconstitution of that powder.
- **Counted per prepared syringe.** Replaced on 2026-11-23: a pre-diluted liquid is counted per mL, as
  any liquid that is not reconstituted. The form quantity of a pre-diluted product is the prepared volume.
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

Prototype, not for merging: the worktree branch `proto/1554-genorder-predilution` added the order side on
the GenFORM of #1970, and printed orders with `src/Informedica.GenORDER.Lib/Scripts/Issue1554PredilutionOrders.fsx`
against the live rules. A pre-diluted product gives a scenario next to the scenario with the undiluted
product; the scenarios of other products do not change.

The predilution rules, their matching and `Product.predilute` were merged in #1970.

Decided on 2026-11-23, for the order side:

- **The product carries its predilution.** The product gets the field `Predilution: ProductPredilution
  option`: None for a product that is not pre-diluted, and the predilution it was made with for a
  pre-diluted product.
- **Only the prescription rules are pre-diluted.** `PrescriptionRule.filter` calls `DoseRule.predilute`
  after `DoseRule.reconstitute`. The check for a dose rule without products in `OrderContext` does not,
  because a predilution only adds products. The load check `SolutionRule.undilutableRules` does not
  either, see below.
- **The pick id.** The pick id of a pre-diluted product is the GPK, followed by the solvent when the
  product is reconstituted, and then the predilution: "185663 water 1 mL + 9 mL gluc 5%".
- **The scenario caption.** The caption of a scenario adds the predilution after the reconstitution:
  "infusiepoeder in 3 mL water, 1 mL verdund met 9 mL gluc 5%".
- **The product volume.** A predilution rule has the column MedVol, required like DiluentVol: MedVol mL of
  the product is diluted with DiluentVol mL of diluent, in a prepared volume of MedVol plus DiluentVol. A
  rule without MedVol, or with 0 mL or less, is left out. This replaces the fixed 1 mL of 2026-11-22; the
  label, the pick id, the caption and the preparation row name MedVol instead of 1 mL, for example "3 mL
  verdund met 7 mL gluc 5%". Content update: the Predilution sheets get the column MedVol.
- **One prepared syringe.** Set aside on 2026-11-23 and settled by the volumes of the predilution rule,
  see below.
- **No count per prepared syringe.** Decided on 2026-11-23. With and without the count per prepared
  syringe, the prototype gave the same prescription, administration, product volumes and concentrations;
  only the package text in the preparation differed. The count was needed only for the maximum of one
  syringe, so a pre-diluted liquid is counted per mL.

Parts of the order side, one pull request each:

1. **The predilution on the product.** The field `Predilution` on the product; every place that builds a
   product sets None, and `Product.predilute` sets the predilution on the pre-diluted product. The column
   MedVol on the predilution rule. Nothing calls `DoseRule.predilute` yet, so the orders do not change.
2. **The predilution in the order product.** The order product carries the predilution it was made with:
   the product volume, the diluent volume and the diluent, also in the medication Dto, the order Dto and
   the Shared type. The pick id adds the predilution after the solvent, "185663 water 3 mL + 7 mL gluc
   5%"; the preparation has the row "3 mL verdunnen met 7 mL gluc 5%"; the scenario caption adds ", 3 mL
   verdund met 7 mL gluc 5%" through the new scenario field Predilution. The stored order JSON stays at
   version 19, because the field is optional. Nothing calls `DoseRule.predilute` yet, so the orders do not
   change.
3. **The predilution in the prescription rules.** The call in `PrescriptionRule.filter`, with the
   predilution rules passed in, and the split into scenarios on the predilution. The resource
   productPredilutions holds the predilution rules with the substances of their diluents, built once per
   load; the prescription rule cache and its fallback in `Api.fs` read it. The orders change here.
   Scripted against the GenORDER of part 2.

The three parts were merged in #1971 and #1972, #1975 and #1978.

Decided on 2026-11-23, after part 3, without a code change:

- **The load check pre-dilutes nothing.** `SolutionRule.undilutableRules` checks only the solution rules
  without diluents. A predilution is always followed by a dilution: a pre-diluted product given without
  a further dilution is a dilution, which a solution rule writes with a diluent, the volume of the
  preparation, the product quantity and the dose fraction (MinPerc and MaxPerc).
- **More than one prepared syringe is a matter of the rule volumes.** The order counts a pre-diluted
  product per mL and uses MedVol and DiluentVol only for the label, the pick id, the preparation row and
  the Dto. A predilution rule whose MedVol plus DiluentVol is at least the largest product volume of the
  order covers the order in one preparation; the same ratio keeps the concentration. Nothing warns when
  the prepared volume is smaller than the order needs.

### Step 3: #1210 the emergency list rules in the rules spreadsheet

Every bolus entry of the `emergencylist` sheet of "GenPRES - Noodlijst" becomes rules in the rules
workbook. The rules are content: the script `src/Informedica.GenFORM.Lib/Scripts/Issue1210EmergencyRows.fsx`
generates the rows from the list and the live sheets, and the choices about the rows are made in the
review of its output, not in this plan.

Content updates needed to replace the emergency list:

- DoseRules rows for the entries that no existing rule covers, under an emergency indication, so that
  the emergency doses do not mix into the ordinary rule choice (open question of #1208).
- SolutionRules rows for a list concentration that no product has, and for the tenfold dilution.
- Products for the entries that have none; until then such an entry gives no order.
- The rows go into a copy of the rules workbook first. A script checks that GenFORM loads the copy and
  returns the rules for the weights of the baseline; then the rows move to the production workbook.

What the code offers, checked on 2026-11-22 and 2026-11-23:

- A rule with a location matched a patient without a location. Since #1984 a rule with a location
  matches only a patient at that location.
- The tenfold dilution needs no predilution. The list gives the diluted product as the bolus itself,
  without a further dilution, so a solution rule with a diluent and a dose fraction (MinPerc and
  MaxPerc) expresses it. Each solution rule of a prescription rule gives its own medication
  (`Medication.fromRule`); a prescription rule with a solution rule gives no medication of the
  undiluted product, so a solution rule needs a weight range or another patient limit when the
  undiluted product must stay a choice. A solution rule without diluents gives the undiluted product.
- The order widens a norm dose to 90% to 110% and gives a volume in steps of 0.1 mL, so a dose volume
  far below 1 mL can have no step within that range and the order has no scenario. The list rounds such
  a volume to 0.1 mL instead.
- The order rounds a volume to the step of the FormRoute sheet; the list rounds to 1 mL above 10 mL.

Decided on 2026-11-23, the code that replaces the calculation on the client:

- The emergency list keeps all its rows. The server calculates the rows from the rules; the client only
  shows them.
- The rows are selected through the RuleSets sheet, the way the nutrition rules are selected. RuleSets
  gets the categories EmergencyList and StandardInfusions (step 4), and the columns Loc and Dep to select
  a rule set for a hospital and a department. An empty Loc or Dep matches every location or department.
- The interventions that are not medication, such as the tube length and the defibrillation, are left for
  a follow-up with a plan of its own.

Part 1, the RuleSets rows read by the server: GenORDER knows no sheet names and takes the rule sets as
values. The server maps a category name of the RuleSets sheet to a nutrition category
(`NutritionCategoryMapper`) and makes the rule sets from the rows (`NutritionRuleSets.ofData`). A row of a
category that is not a nutrition category is left out, with a warning once per resource load, instead of
refusing every rule set; so rows of the categories EmergencyList and StandardInfusions can be entered
before the code reads them, without refusing the nutrition orders. Merged in #1980.

Part 2, the EmergencyList category read: the server reads the rows of the EmergencyList category of the
RuleSets sheet into a rule set, with the columns Loc and Dep, where an empty Loc or Dep matches every
hospital or department. Before part 2 the server left these rows out, because its mapper of the category
names knew only the nutrition categories.

Content update: every RuleSets sheet gets the columns Loc and Dep. They are required columns, so a
workbook whose RuleSets sheet lacks them does not load. The load check `RuleSet.check` warns, for most generics under an indication of
these rows, that it has no product in the Formulary sheet, because it compares the names with the
Generic column of the Formulary sheet case-sensitively, while the dose rules find their products without
regard to case (`DoseRuleLoader`); the Formulary writes the medication generics in capitals.

Decided on 2026-11-23:

- One general rule set, in GenFORM, replaces the GenORDER `NutritionRuleSet` type: its kind is Nutrition of
  a nutrition category, EmergencyList or StandardInfusions, and its rows are the rows of the RuleSets sheet,
  each with an indication, a generic, a Loc and a Dep. The nutrition categories move to GenFORM with it;
  GenORDER keeps only what applies a rule set to a nutrition order context (`NutritionRuleSet`).
- Every rule set, nutrition included, uses only the rows whose Loc and Dep match the Location and the
  Department of the patient; an empty Loc or Dep matches every patient. A patient without a department
  gets the default department first (`Api.withDefaultDepartment`), as for the dose rules.
- The load check compares the names with the Formulary sheet without regard to case, as the dose rules do.

A hospital selects its own dose rules and solution rules through their Loc column, checked on
2026-11-23:

- `DoseRule.filter` matches the Loc of a dose rule with the Location of the patient; an empty Loc or no
  Location matches every hospital (`Patient.matchesCategory`). Since #1984 no Location matches only the
  rules without a Loc.
- `SolutionRule.filter` does not check the Location, so the solution rules of every hospital apply to
  the dose rule of one hospital, and each gives its own medication (`Medication.fromRule`). The check
  `SolutionRule.appliesToDoseRule` compares the Loc of a solution rule with the Loc of a dose rule, but
  only the load checks use it. Part 3 changed this.
- The client never sets the Location of the patient: the hospital chosen in the title bar stays in the
  client, its list comes from the emergency list workbook, and no patient data edit sets a Location.

Part 3, the solution rules follow the location, decided on 2026-11-23: `SolutionRule.filter` compares the
Loc of a solution rule with the Location of the patient, as the dose rules do. A solution rule with a Loc
applies only to a patient with that Location, as #1984 makes it for the dose rules.

Part 4, the chosen hospital is the Location of the patient, decided on 2026-11-23: the hospital is part
of the patient data the client sends to the server, as the Location, the same way as the department.
The Location already goes both ways between the contract, the server and GenCORE (the patient data
mappers and the GenCORE patient data Dto); what is missing is the edit that sets it, next to
`PatientDataEdit.SetDepartment` in the contract and in GenCORE, its mapping in the server, and the client
message that sends it, next to `UpdateDepartment` in `PatientDataPolicy`. The hospitals to choose from
are the distinct Loc values the rules name, as `Resources.Departments.ofRules` makes the departments from
the rules; the emergency list workbook no longer gives them.

Decided on 2026-11-23, how every patient gets the hospital:

- The server fills in a default hospital as the Location of a patient without one, before it completes
  the patient data or applies an edit (`processCmd`). The answer to the client carries the default as the
  Location of the patient, so the patient panel and the rules name the same hospital. Changed on
  2026-11-23: the default was first applied only where the rules are matched and never written on the
  patient.
- The default hospital is a server setting, a `GENPRES_*` environment key. Without the key, production
  has no default hospital, so a patient without a Location gets only the rules without a Loc; outside
  production the default is UMCU.
- The server sends the hospitals and the default hospital to the client in `ServerSettings`, as it sends
  the departments today.
- The client shows the default hospital of the server in the title bar at the start, as it takes the
  default language. Without a default hospital the title bar shows that no hospital is chosen, so the
  hospital of the shell becomes optional.
- Choosing a hospital in the title bar sends `SetLocation` for the open patient. When the answer for a
  new patient arrives (another patient, Clear, a patient from the url or the EHR) and the shell has a
  hospital, the client sends `SetLocation` with that hospital, so the title bar and the patient data name
  the same hospital.
- There is no default department any more: `Resources.Departments.defaultDepartment`,
  `Api.withDefaultDepartment`, `ServerSettings.DefaultDepartment` and the default department shown on the
  patient panel are removed. A patient without a department gets only the rules without a Dep.

The parts after part 4 build what the prototype on branch `proto/1210-emergency-list` shows, decided on
2026-11-24. The page keeps the table, the columns and the print of today; only the medication rows come
from the server. The rows that are not medication stay in the client until their own plan.

Decided on 2026-11-24:

- A row of the list is an order context narrowed to one scenario: one scenario gives one order context and
  one row. The rules decide which scenarios there are, so a medication with more scenarios shows a row for
  each.
- A click on a medication row opens the prescribe page with the order context of that row, as it is.
- The prescriber can select one or more medication rows; the prescribe button of the list puts their order
  contexts on the order plan as they are, without linked orders. A choice the order still holds is made in
  the plan. Linked orders go through the prescribe page.
- The rows that are not medication cannot be prescribed.
- Each part removes the code it replaces.

Part 5, the server calculates the medication rows:

- The contract gets `getEmergencyList`: it takes the patient data in a `Request` and returns the rows of the
  emergency list as `Intervention` records, the records the page shows today, in a `Reply`. It runs through
  `Compute.bound`, as the other calls that calculate.
- `Intervention` gets the field `OrderContext: OrderContext option`: the order context of the row, narrowed
  to its scenario as the prescribe page narrows it (`narrowedToScenario`); None for a row that is not
  medication. `Intervention` moves below `OrderContext` in the contract, so it can name it.
- The server takes the rule set of the kind EmergencyList for the patient (`ruleSets` with
  `RuleSet.forPatient`), so only the rows for the Location and the Department of the patient count.
- Each row becomes an order context with the patient data, the indication and the generic of the row,
  evaluated as the prescribe page evaluates it. A context without scenarios is evaluated again for each
  option of the first open choice, in the order generic, route, dose type, form, at most four choices deep,
  so a row with more routes, dose types or forms gives the scenarios of each.
- The rows are evaluated in chunks of `Parallel.totalWorders`: the chunks run in parallel, the rows in a
  chunk one after the other, as GenFORM loads its rules.
- Each scenario gives one row of the list:
  - Categorie: the indication without the word "Noodlijst".
  - Interventie: the generic of the row.
  - Berekend: the dose of the substance (`itm_dos_qty`), followed by the dose per kg (`itm_dos_qty_adj`)
    in brackets when the order is per kg.
  - Bereiding: the dose of the orderable (`orb_dos_qty`), followed by "van" and the concentration of the
    substance in the orderable (`itm_orb_cnc`).
  - The substance is the item the scenario names, not an additional substance of the product.
  - A scenario without a substance, dosed by volume, shows the orderable instead: Berekend the dose of the
    orderable with the dose per kg, Bereiding the dose of the orderable with the name of the product.
- A value with more than one number shows every number; a value without numbers shows its minimum and
  maximum, or the one limit it has, such as "min 1 mg" or "max 1 mg".
- The columns go in the text fields of `Intervention`: Berekend in `SubstanceDoseText`, Bereiding in
  `InterventionDoseText`, Advies in `Text`. The server fills only these text fields; the number fields stay
  empty. The print takes the cells of the table, as it does today.

Part 6, the advice of a row:

- Advies shows the limits of the dose rule the scenario was made from: the dose per kg, followed by the
  absolute dose in brackets. A limit with an equal minimum and maximum is the norm and shows as one value
  (`DoseLimit.printMinMaxDose`), not as the widened range of the order.
- The limit is the limit for the substance, in the unit the substance is dosed in. A scenario dosed by
  volume takes the limit of the component, else the limit of the form.
- A once timed order adds the administration time (`ord_sch_tme`) and the rate (`orb_dos_rte`), such as
  "in 30 - 60 min, 1 - 2 mL/uur". A time or rate with more than one number shows the lowest and the
  highest number.
- The server finds the dose rule through the prescription rules for the dose filter of the order context,
  matched on the route and the dose type of the scenario.

Part 7, the client shows the server rows:

- The client asks the server for the rows through Client.Core, as it does the other server loads, when
  the patient data change and the patient has a Location.
- The page shows the rows that are not medication, followed by the server rows, and lays a mask with a
  spinner over the table while the server calculates the rows.
- The client numbers each list request and ignores an answer to an earlier request, as it does for the
  interaction checks and the totals queries. When the patient is reset or the patient data lose the
  Location, the list clears its rows and its selection. The prescribe button is disabled while the rows
  are calculated.
- Removed: the medication rows of the emergency list workbook, their calculation in the client and their
  lookup for a row click (`Client.emergencySeed`).
- Removed: the emergency baseline tests and their fixtures (`EmergencyBaseline.fs`,
  `EmergencyBaselineTests.fs`, `fixtures/emergencylist.csv` and `.tsv` in `Informedica.GenPRES.Shared.Tests`),
  with their entries in the test project file. The order calculation is tested in GenSOLVER and GenORDER;
  whether the rules are right is a matter of medical validation, not of the code.

Part 8, prescribing from the list:

- A click on a medication row sends a new order context message that puts the order context of the row in
  the workbench as it is, without a request, and moves to the prescribe page.
- The list gets a checkbox for each row and a prescribe button, enabled when a selected row has an order
  context.
- The contract gets the plan command `AddOrderContexts`: it takes the plan and the order contexts of the
  selected rows. The server adds them one after the other through the port that adds an order context; when
  one is refused, none of them enters the plan. The plan accepts one request at a time, so one command
  carries every selected row.

A script times the calculation of each row at the level of the server
(`src/Informedica.GenPRES.Server/Scripts/EmergencyListTiming.fsx` in the prototype worktree, not
committed). A slow row is a matter of the rules, not of the code.

### Step 4: #1211 the continuous medication rules in the rules spreadsheet

Every entry of the `continuousmeds` sheet becomes rules in the rules workbook. The rules are content;
the choices about the rows are made in their review, not in this plan.

Content updates needed to replace the continuous medication list, as the issue lists them:

- a continuous dose rule per entry with the minimum and maximum dose rate and the absolute maximum;
- the standard concentrations per weight range as solution rules, with `minconc` and `maxconc` as limits;
- the morphine "weight / 2 mg in 50 mL" case as a rule;
- the hospital column mapped to department or location.

Open questions of #1208: the rule form of the morphine case and the mapping of the hospital column.
Whether either needs a code change is settled in this step.

The code parts build what the prototype on branch `proto/1210-emergency-list` shows for the continuous
medication list, decided on 2026-11-24. They reuse the code of step 3: a row is an order context narrowed to
one scenario, and the row click and the prescribe button work as in step 3. The page keeps the table, the
columns and the print of today; every row comes from the server.

Part 1, the server calculates the rows:

- The contract gets `getContinuousList`: it takes the patient data in a `Request` and returns the rows as
  `Intervention` records, each with its order context, in a `Reply`. It runs through `Compute.bound`, as the
  other calls that calculate.
- The server takes the rule set of the kind StandardInfusions for the patient and evaluates its rows as for
  the emergency list (step 3, part 5), with the same code.
- Each scenario gives one row of the list:
  - Categorie: the indication without the words "Standaard infusie".
  - Medicatie: the generic of the row.
  - Hoeveelheid: the quantity of the substance in the orderable (`itm_orb_qty`).
  - Oplossing: the quantity of the orderable (`orb_orb_qty`), followed by the diluent.
  - Dosering: the rate (`orb_dos_rte`), followed by "=" and the rate per kg of the substance
    (`itm_dos_rte_adj`), such as "0,1 - 15 mL/uur = 5 - 15 microg/kg/min".
  - Advies: the rate limits per kg of the substance in the dose rule (`itm_dos_rte_adj`), in the unit the
    substance is dosed in; the absolute rate limits when the dose rule has no rate limits per kg.
  - A scenario without a substance, dosed by volume, shows the orderable instead: Hoeveelheid empty,
    Oplossing the name of the product, Dosering the rate with the rate per kg of the orderable, Advies the
    rate limits of the component, else of the form.
- The columns go in the text fields of `Intervention`: Hoeveelheid in `QuantityUnit`, Oplossing in
  `Solution`, Dosering in `SubstanceDoseText`, Advies in `Text`. The server fills only these text fields; the
  number fields stay empty. The print takes the cells of the table, as it does today.

Part 2, the client shows the rows and prescribes from them:

- The client asks the server for the rows through Client.Core, when the patient data change and the patient
  has a Location, and lays a mask with a spinner over the table meanwhile. Old answers, a reset patient and
  the prescribe button are handled as on the emergency list (step 3, part 7).
- The row click and the prescribe button work as on the emergency list (step 3, part 8).
- Removed: the reading of the `continuousmeds` sheet, its calculation in the client
  (`ContinuousMedicationList.calculate`) and its lookup for a row click (`Client.continuousSeed`).
- Removed: the continuous baseline tests and their fixtures (`ContinuousBaseline.fs`,
  `ContinuousBaselineTests.fs`, `fixtures/continuousmeds.csv` and `.tsv` in
  `Informedica.GenPRES.Client.Core.Tests`), with their entries in the test project file, and the script
  `Scripts/ListBaseline.fsx` of the Shared tests, which writes both baselines.

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
