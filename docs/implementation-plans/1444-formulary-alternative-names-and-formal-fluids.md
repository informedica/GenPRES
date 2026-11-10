# Implementation plan for issue #1444

The Formulary sheet gives a product the name the rules use, says what a product can be used for, and
links a parenteral fluid to its G-Standaard product. Drafted 2026-11-09. Line numbers are from
`master` at `4a8b3395`. The Formulary, DoseRules, SolutionRules and FormRoute sheets were fetched on
2026-11-09.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [Decisions](#decisions)

## Problem description

The products of a dose rule component, a solution limit or a diluent are selected by name only.
The other keys narrow that selection or join a product to other sheets; see
[the keys](#the-keys-between-products-and-rules). Three problems follow:

1. **Amphotericin.**
   - The lipid complex, liposomal and colloidal rows have `GStand` = `AMFOTERICINE B`, so the
     lipid complex (GPK 106488, Abelcet) and the liposomal product (GPK 92266, AmBisome) get that
     name.
   - Their dose rules name `amfotericine b lipidencomplex` and `amfotericine b liposomaal`. No
     product matches, and the rule gets a virtual product without a GPK, quantities or
     concentrations.
   - The GPKs on the dose rule rows do not help: a GPK narrows the products of a name.
2. **Parenteral fluids.**
   - The 58 `parenteral` rows (`gluc 5%`, `NaCl 0,9%`, ...) are virtual products: the id is the
     name, the composition comes from the nutrient columns, and nothing links them to the
     G-Standaard.
   - Most have a G-Standaard product: `gluc 5%` is GPK 61905, `NaCl 0,9%` is GPK 19100.
   - The rules use the short names: `gluc 5%`, `NaCl 0,9%` and `gluc 10%` each appear more than
     200 times as solution rule diluents, and 166 dose rule rows name a fluid as component.
3. **Additional ingredients.** The totals count ingredients the G-Standaard does not list, such as
   natrium in NaCl 0,9%. The nutrient columns add them to a medication product today; the
   findings are below.

## What the code does today

**The name of a product** (`Product.fromGenPresProducts`, `Product.fs:505`):

- one product per G-Standaard generic product, with its Formulary row looked up by GPK
  (`formByGpk`, `Product.fs:520`);
- the match key is `GStand`, else `Generic`, lowercased, else the generic product name, such as
  `NATRIUMCHLORIDE INFVLST   9MG/ML` (`Product.fs:547`);
- the product `Label` is the `Generic` column when that differs from the match key, else the
  G-Standaard label (`Product.fs:555`);
- `filterGenPresProductsByData` computes the same key again in its own code (`Product.fs:704`).

Checked on GPK 19100 with the real data:

| GPK 19100 | `Generic` (match key) | `Label` | Selected by `natriumchloride` + GPK 19100 |
|---|---|---|---|
| with its Formulary row | `natriumchloride` | `NATRIUMCHLOR 9MG/ML INFVLST` | yes |
| without a Formulary row | `NATRIUMCHLORIDE INFVLST   9MG/ML` | `NATRIUMCHLOR 9MG/ML INFVLST` | no |

The GPK alone does not select a product: `Product.filter` needs the name first.

**What the user sees.**

- The medication list ("Medicatie") holds the dose rule labels, from the DoseRules `Generic`
  with `Form` or `Brand` (`DoseRule.generics`, `DoseRule.fs:1014`). No product name is in it.
- In an order a component is named by the rule's `Component`, and a diluent by the product's
  `Generic` (`Medication.fs:996`).
- The product `Label` is read only in the product list of the dose rule text (`DoseRule.fs:238`),
  the reconstitution text (`SolutionRule.fs:693`), the Divisible warnings (`DoseRule.fs:1135`) and
  the MCP formulary tool (`McpTools.GenForm.fs:233`).

**One row per GPK.** `formByGpk` is a `Map.ofArray`, so of two rows on one GPK the last wins,
without a warning. Today this hits GPK 109347 (`BUPROPION (ZYBAN)` and `BUPROPION (WELLBUTRIN)`).

**The name gates.** These compare the product's `Generic` with the rule's Component:

- `DoseRuleLoader.candidateProducts` (`DoseRuleLoader.fs:24`);
- `Product.filter` (`Product.fs:841`);
- `DoseRule.mapToComponentLimits` (`DoseRule.fs:497`).

The GPKs of a dose rule row narrow the products after the name matched. The comment on
`filterGenPresProductsByData` says an id overrides the name, which is true only in that
prefilter.

### The keys between products and rules

| Where | Key | Role |
|---|---|---|
| Prefilter `filterGenPresProductsByData` (`Product.fs:641`) | GPK, else HPK, else name and route, all from the DoseRules | Decides which G-Standaard products are built; a superset |
| `Product.filter` (`Product.fs:841`) | name; route, form, GPK; brand and HPK on trade products | The name selects, the rest narrows |
| Solution limit products (`SolutionRule.fs:172`) | name, form, route | Case sensitive (`=`), unlike the other gates |
| Solution limits of a dose rule (`PrescriptionRule.fs:296`) | GPK | Keeps the products the dose rule has |
| Solution rule to dose rule (`SolutionRule.fs:479`) | the base generic of the dose rule, form, route | Rule to rule |
| Diluents (`SolutionRule.fs:123`) | name, case insensitive, in the parenteral products only | Selects |
| Reconstitution (`Product.fs:444`) | GPK | Joins the Reconstitution rows |
| Picked products (`OrderContext.fs:615`) | GPK | Marks the products picked in an order |

Two products on one GPK both pass the GPK joins. That is correct as long as the name decides
which rule a product belongs to. The picked products are the ids of all order components, the
diluent included (`Order.fs:3184`), matched within one rule. A rule with two components on one GPK
would count both as picked; no rule does that today.

### How a generic is named

The names a product carries:

| Source | Example (GPK 92266) | Read for |
|---|---|---|
| G-Standaard generic product name | `AMFOTERICINE B INFUSIEPOEDER  50MG (LIPOSOMAAL)` | the match key of a product without a Formulary row |
| G-Standaard label | `AMFOTERICINE LIP 50MG INFP` | the product `Label`, unless the `Generic` column differs from the match key |
| G-Standaard substance `SubstanceName` | `AMFOTERICINE B` | the substance name, by default |
| G-Standaard substance `GenericName` | `AMFOTERICINE B LIPOSOMAAL` | the substance name when `UseGenName` is `x` |
| G-Standaard trade product `Brand` | `AMBISOME` | product synonyms; dose rule `Brand` |
| Formulary `GStand` | `AMFOTERICINE B` | the match key |
| Formulary `Generic` | `AMFOTERICINE B LIPOSOMAAL` | match key when `GStand` is empty; product `Label` when it differs from the match key; rule set check |
| Formulary `Alternative` (new) | `amfotericine b liposomaal` | the match key, before `GStand` |
| Formulary `GenName`, `TallMan`, `Brand`, `Form` | | parsed, not read |
| `Generic` of a `parenteral` or `enteral` row | `NaCl 0,9%` | the match key, as written, and today the id |

The match key becomes `Alternative` as written, else `GStand`, else `Generic`, both lowercased,
else the generic product name.

The names in the rule sheets:

| Sheet and column | Has to match |
|---|---|
| DoseRules `Component` | the product match key (the three gates) |
| DoseRules `Substance` | a product substance name |
| DoseRules `Generic`, with `Form` or `Brand` | nothing; the rule label, can be a mixture such as `NICU mix` |
| DoseRules `Brand` | a trade product brand |
| DoseRules `GPKs`, `HPKs` | the product id, narrowing only |
| SolutionRules `Generic` | the base generic of the dose rule (`PrescriptionRule.fs:278`) |
| SolutionRules `Component` | the product match key, case sensitive; mixed case for fluids, such as `KCl 7,4%` and `NaCl 3%` |
| SolutionRules `Substance` | a product substance name |
| SolutionRules `Solutions` | the `Generic` of a `parenteral` row |
| RenalRules `Generic`, `Substance` | the base generic of the dose rule (`RenalRule.fs:361`), a substance name |
| LinkRules `Generic` | the dose rule label |
| RuleSets `Generic` | the dose rule label, and the Formulary `Generic` column |
| G-Standaard dose check (`Check.matchWithZIndex`) | the base generic of the dose rule label |

Two mismatches:

- `RuleSet.checkData` (`RuleSet.fs:100`) checks the dose rule names against the Formulary
  `Generic` column, case sensitive, instead of the match key.
- The G-Standaard dose check may not know `amfotericine b liposomaal`. To check once the product
  is real.

### What `Type` does

`Type` is read in two places only: `Product.Parenteral.get` (`Product.fs:325`) and
`Product.Enteral.get` (`Product.fs:267`). Nothing else in `src/` reads it.

| `Type` | Rows | Build | Used as |
|---|---|---|---|
| `medication` | 6469, all with a numeric `GPKODE` | from the G-Standaard by GPK | dose rule components |
| `parenteral` | 58, `GPKODE` = `Generic` | virtual: id and name the `Generic`, form `vloeistof`, routes intravenous and oral, 1 mL, Divisible 10, the nutrient columns as substances (`Product.fs:283`) | diluents (`SolutionRule.fs:123`) and dose rule components |
| `enteral` | 51, `GPKODE` = `Generic` | virtual: form `voeding`, route oral, the `Unit` column, Divisible 10 (`Product.fs:227`) | dose rule components |

- The virtual products are appended to all products (`Product.fs:598`), so a dose rule can name
  them.
- `formByGpk` and the prefilter ignore `Type`: they take every row with a numeric `GPKODE`
  (`Product.fs:520`, `Product.fs:649`).
- `Form` is empty on all 109 virtual rows; the forms are fixed in the code (`Product.fs:241`,
  `Product.fs:296`).
- `IsReconste`, `IsDilute` and `IsAdditive` are compared with `"x"` (`Product.fs:71`), but the
  sheet holds `TRUE` and `FALSE`. They are always false, and nothing reads them.

### Optional components

Optional components exist today, for any rule with more than one component:

- the order's component options are the rule's component names (`OrderContext.fs:189`), offered
  as a multi-select "Componenten" (`Prescribe.fs:597`);
- the first component is always kept, the others only when selected, all when none is selected
  (`PrescriptionRule.fs:330`);
- a solution rule adds its first diluent as one more component (`Medication.fs:990`).

11 rules have more than one component. A component after the first is either an additive, such as
`NaCl 3%`, `KCl 7,4%` or `Peditrace`, or a diluent. `Samenstelling B` to `E` have empty
`Solutions`, so their glucose components (`gluc 10%` to `gluc 40%`) are the diluent.

### Divisible

- A Divisible d gives a step of 1/d form unit (`Medication.fs:1339`); a blank gives no step and
  the order shows "Geen bereiding" (`OrderContext.fs:155`).
- A G-Standaard product takes the Formulary `Divisible`, else the FormRoute `Divisible` of its
  form, else blank (`Product.fs:445`).
- The FormRoute lookup ignores the route and unit and takes the first row of the form
  (`Product.fs:451`).
- FormRoute has `vloeistof` only for `nasaal`, with a blank Divisible, and `voeding` only for
  `oraal` in milliliter, with Divisible 1.
- The Formulary `Divisible` is filled on 23 enteral rows (10) and 158 medication rows, and blank on
  the 58 parenteral and 28 other enteral rows.

### Additional ingredients of a medication product

`Product.map` (`Product.fs:453`) adds to the G-Standaard substances:

- the substances the G-Standaard marks as additional;
- the nutrient columns of the Formulary row (`fpAdditional`); 24 medication rows fill them, such
  as KCl 48232.

Findings:

- `Carb g` gives two substances, `glucose` and `koolhydraat`. A G-Standaard glucose product would
  get two substances named `glucose`.
- The `Mmol` column applies to the substance named like the product (`Product.fs:409`). With an
  alternative name it must compare with the G-Standaard generic.
- Trade products get the G-Standaard substances only (`Product.fs:492`). When a dose rule row
  names a `Brand` or `HPKs`, `filterOutTradeProducts` keeps only the product substances its trade
  products have (`Product.fs:821`), so the nutrient column substances are dropped from that
  product.

### Sheet columns

A column the code reads must exist: a missing column raises (`Csv.getColumn`), and the sheet fails
to load. A new column is added before the code reads it; an old column is deleted after the code
stops reading it.

## Decisions

