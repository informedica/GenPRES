# Implementation plan for issue #1444

The Formulary sheet gives a product the name the rules use, says what a product can be used for, and
links a parenteral fluid to its G-Standaard product. Drafted 2026-11-09. Line numbers are from
`master` at `4a8b3395`. The Formulary, DoseRules, SolutionRules and FormRoute sheets were fetched on
2026-11-09.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Left out](#left-out)
- [Verification](#verification)

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

1. **The alternative name: a new Formulary column `Alternative`.**
   - When filled, it is the match key in place of `GStand`, so `GStand` keeps only G-Standaard
     names.
   - It is taken as written, not lowercased: the fluid names are mixed case (`NaCl 0,9%`), the
     solution limit comparison is case sensitive, and a diluent shows by its `Generic`.
2. **A GPK on a dose rule row narrows.** It never replaces the name; `Alternative` does that. Only
   the prefilter comment and the `DoseRuleData` documentation change.
3. **`Type` goes; the build follows `GPKODE`.**
   - A numeric `GPKODE`: built from the G-Standaard, plus the nutrient columns.
   - Otherwise a virtual product: id and name the `Generic`, form from `Form`, unit from `Unit`,
     routes from a new column `Route` (read only for virtual rows, such as `INTRAVENEUS;ORAAL`),
     the nutrient columns as substances.
   - A fluid with a G-Standaard product is one row: the medication row on its GPK becomes the
     fluid row. It gets `Alternative` = the short name, the roles, the nutrient columns and
     `Divisible` `10`, the value the virtual row has today, and the virtual fluid row is deleted.
     Example: the row on GPK 19100 becomes `NaCl 0,9%`. Its composition is the G-Standaard
     composition plus the nutrient columns.
   - The form and the routes change with the move: the virtual fluid has form `vloeistof` and the
     routes intravenous and oral, the G-Standaard product has its own form, such as
     `infusievloeistof`, and the routes of the G-Standaard. `Product.filter` narrows by form and
     route, so a dose rule row that names the fluid as component with a form or a route the
     G-Standaard product lacks loses it. Before each move, a script lists those rows per fluid,
     and the pharmacy corrects them first.
   - The old name is then gone for that GPK. The rules that select it by the old name lose it,
     until the pharmacy points them to the fluid name:
     - `natriumchloride` intravenous and rectal with GPKs `19100;39144` keep only 39144;
     - `natriumchloride` intravenous without GPKs loses 19100 and 147419;
     - `kaliumchloride` intravenous with GPKs `48232; 158739` and its solution rules keep only
       158739.
     The 8 glucose rows (61905 to 179930) are named by no rule, so nothing is lost there.
4. **The additional substance wins.** On a shared name, the Formulary value replaces the
   G-Standaard substance.
5. **Which fluid gets which GPK** is a pharmacy decision. The candidates:

   | Fluid | GPK | Form |
   |---|---|---|
   | gluc 5% | 61905 | infusievloeistof |
   | gluc 10% | 61913 | infusievloeistof |
   | gluc 15% | 159298 | infusievloeistof |
   | gluc 20% | 76783 | infusievloeistof |
   | gluc 40% | 76791 | infusievloeistof |
   | gluc 50% | 62014 | infusievloeistof |
   | gluc 2,5% + NaCl 0,45% | 61921 | infusievloeistof |
   | gluc 5% + NaCl 0,9% | 179930 | infusievloeistof |
   | NaCl 0,9% | 19100 | infusievloeistof |
   | NaCl 3% | 147419 | infusievloeistof |
   | KCl 7,4% | 48232 | concentraat voor oplossing voor infusie |

   Without a G-Standaard product, and so staying virtual: water, gluc 12,5%, gluc 25%, gluc 30%,
   gluc 35%, gluc 45%, glycine buffer, NICU mix, Samenstelling B to E, the blood products, and
   others.
6. **Four roles per row replace `Type`**, each `TRUE` or `FALSE`; a product can have all four,
   such as `gluc 10%` or `NaCl 0,9%`:
   - `IsMain`: can be the main component, the first component of a dose rule;
   - `IsAdditive`: can be an optional component, switched on and off in the order, such as
     `NaCl 3%` in a parenteral nutrition;
   - `IsDiluent`: can dilute other products, as a diluent of a solution rule that the order can
     switch to another diluent, or as a component of a dose rule that the order always keeps;
   - `IsReconstitute`: can reconstitute another product.
7. **The roles decide which products a rule gets and how the order uses them.** A role is a
   gate on the product selection, not a warning:
   - the products of the first component of a dose rule are the `Main` products with its name;
   - the products of a component after the first are the `Additive` and `Diluent` products with
     its name;
   - a product with several roles plays one role in an orderable, and the solution rule decides
     which: a component after the first is the diluent of the orderable when a solution rule of
     the prescription rule names it in `Solutions`, or when its products have the `Diluent` role
     and not the `Additive` role; otherwise it is an additive. The component limit of the
     prescription rule carries the role it plays, and its products are narrowed to that role;
   - a component without a product with the role has no products, so the rule is not
     prescribable, as a rule without products is today (`NoProducts`); the loader warns per rule;
   - the order keeps the main component and the diluent components always, and an additive
     component only when selected, all additives when none is selected;
   - the order's component options are the additive components, not every component;
   - the diluents of a solution rule are the `Diluent` products with a name in its `Solutions`,
     and the order can switch between them. A solution rule whose `Solutions` name no `Diluent`
     product loads without diluents, as today: a warning at load (`SolutionRule.fs:287`), the
     medication built without a diluent (`Medication.fs:1003`), and the order refused only when
     the medication cannot be given undiluted (`NoDiluent`). Nothing changes there;
   - `IsReconstitute` is parsed and carried; its effect is left out, see below.
8. **The Divisible of a virtual product** follows the G-Standaard chain: the Formulary `Divisible`,
   else the FormRoute `Divisible` of its form, else blank. The sheet gets `10` in the 86 blank
   virtual rows, so the result stays as today.

## Approaches considered

1. **Data only:** fill `GStand` for amphotericin. Leaves the fluids unlinked.
2. **The dose rule GPK overrides the name** in the three gates. Leaves the fluids unlinked, and a
   shared GPK would reach the rules of both names.
3. **A GPK on the parenteral row as its id**, keeping the virtual build. Keeps `Type`, and a
   fluid with a GPK still ignores its G-Standaard composition.
4. **Roles per row, the build following `GPKODE`, and `Alternative`:** each Formulary row builds
   one product, named by `Alternative` when filled; what a product can be used for is in the row.

## Chosen approach

Approach 4:
- one build rule for every row: G-Standaard when `GPKODE` is numeric, else virtual;
- one naming rule: `Alternative`, else `GStand`, else `Generic`;
- the roles decide what a product can be used for, instead of the kind of row and the position
  in the rule alone;
- no change to the DoseRules and SolutionRules sheets, and the gates stay name based, narrowed by
  role;
- fluids move to their GPK one by one.

## Confidence

High for steps 1 to 3, medium for steps 4 and 5:
- the naming change is small and testable;
- the roles replace one filter for the diluents and one for the prefilter;
- the virtual build keeps its output when the sheet is filled first;
- a fluid on its GPK gets the G-Standaard composition, which the step 1 test for a shared
  substance name and the verification of totals cover;
- the role gates of steps 4 and 5 change which products a rule gets: a role missing in the sheet
  takes a product from a rule. The roles are filled and checked before step 4 merges, see
  [Verification](#verification).

The risks are two products on one GPK, covered by the step 1 tests, and a rule losing its products
to a missing role, covered by the check script before step 4.

## Steps

Each step is one pull request, prototyped first in a script under
`src/Informedica.GenFORM.Lib/Scripts/`.

0. **`docs`: this plan.** The pull request that adds this plan. Data, with it: add the empty
   columns `Alternative`, `Route`, `IsMain`, `IsDiluent` and `IsReconstitute` to the Formulary
   sheet. Columns are read by name, so this changes nothing.
1. **`fix(genform)`: one product per Formulary row, and the alternative name.**
   - Parse `Alternative`, taken as written (decision 1).
   - One match key function, `Alternative`, then `GStand`, then `Generic`, used by
     `fromGenPresProducts`, the prefilter and `RuleSet.checkData`.
   - `formByGpk` maps a GPK to its rows, every row with a numeric `GPKODE`.
   - One product per pair of row and G-Standaard product.
   - Compare the `Mmol` column with the G-Standaard generic.
   - On a shared name, the additional substance replaces the G-Standaard substance (decision 4).
   - Correct the prefilter comment (decision 2), the `///` field comments and the Formulary column
     contract test.
   - Tests:
     - two rows on one GPK give two products, each reaching only its own rules, the GPK joins
       and the picked products included;
     - an `Alternative` becomes the product's `Generic` as written: the prefilter keeps the
       product, and a solution limit on that name gets it;
     - an additional substance replaces the G-Standaard substance of the same name.
   - Data, after the merge: fill `Alternative` for GPKs 106488, 156906, 92266, 156884 and 156892.
2. **`feat(genform)`: the roles of a product.**
   - Parse `IsMain`, `IsAdditive`, `IsDiluent` and `IsReconstitute` as `TRUE`/`FALSE`; delete the
     parsing of `IsReconste` and `IsDilute`.
   - The product carries its roles (decision 6); a virtual product gets them from its row like
     any other.
   - The diluents of a solution rule are the `Diluent` products with a name in its `Solutions`,
     instead of the parenteral products.
   - The prefilter also keeps the G-Standaard products of `IsDiluent` rows, since the DoseRules do
     not name a product used only as diluent.
   - Tests:
     - a G-Standaard row with `Alternative` `NaCl 0,9%` and `IsDiluent` is the diluent of a
       solution rule with `Solutions` `NaCl 0,9%`, with id 19100 and name `NaCl 0,9%`;
     - a product without `IsDiluent` is no diluent;
     - the prefilter keeps an `IsDiluent` row's G-Standaard product.
   - Data, before the merge: `IsMain` `TRUE` on every row; `IsDiluent` `TRUE` on every
     diluent of the solution rules, among them `gluc 15%` to `gluc 50%`; `IsAdditive` on the
     additives; `IsReconstitute` copied from `IsReconste`. After the merge: delete `IsReconste` and
     `IsDilute`.
   - Data, after the merge, fluid by fluid (decision 5), starting with `NaCl 0,9%`: the medication
     row on the GPK becomes the fluid row (`Alternative` = the short name, the roles, the nutrient
     columns, `Divisible` `10`), and the virtual fluid row is deleted. For 19100, 147419 and
     48232 the pharmacy first points the rules named in decision 3 to the fluid name. Before each
     move, the form and route check of decision 3 runs, and the pharmacy corrects the rows it
     lists.
3. **`refactor(genform)`: the build follows `GPKODE`; `Type` goes.**
   - One virtual builder for every row without a numeric `GPKODE`, replacing
     `Product.Parenteral` and `Product.Enteral`: form, unit and routes from `Form`, `Unit` and
     `Route`; Divisible as decision 8.
   - Delete `ProductType` and the parsing of `Type`.
   - Tests: a virtual row gives the product today's parenteral or enteral builder gives, for one
     fluid and one feed.
   - Data, before the merge: `Form` (`vloeistof` or `voeding`), `Route` and `Divisible` `10` on the
     virtual rows. After the merge: delete the `Type` column.
4. **`feat(genform)`: the roles gate the products of a dose rule** (decision 7).
   - `ComponentLimit` (`Types.fs:438`) carries the role the component plays.
   - `DoseRule.mapToComponentLimits` (`DoseRule.fs:497`) gives the first component the `Main`
     products of its name, with role `Main`, and a later component the `Additive` and `Diluent`
     products, with role `Additive`. The groups keep the row order, so the first group is the
     first component.
   - `PrescriptionRule` (`PrescriptionRule.fs:296`), where the dose rule meets its solution
     rules, sets a later component to `Diluent` when a solution rule names it in `Solutions` or
     its products have `Diluent` and not `Additive`, and narrows its products to the role.
   - `PrescriptionRule` (`PrescriptionRule.fs:330`) keeps the `Main` and `Diluent` components
     always and an `Additive` component when selected, all additives when none is selected.
   - A warning per rule with a component without a product with its role, from the loader.
   - The two other name gates stay name only: `DoseRuleLoader.candidateProducts`
     (`DoseRuleLoader.fs:24`) keeps every product with a component name, and `Product.filter`
     (`Product.fs:841`) narrows by route, form, brand, GPK and HPK after the role has narrowed.
     The role is a gate once, in `mapToComponentLimits` and `PrescriptionRule`.
   - Tests:
     - a rule whose first component name has a `Main` product and a product without the role
       gets the `Main` product only;
     - a component named in the solution rule's `Solutions` is a `Diluent` component; the same
       component without that solution rule is an `Additive` component;
     - a rule with an additive and a diluent component: the additive is dropped when another
       additive is selected, the diluent is kept;
     - a component without a product with the role has no products and warns.
   - Data, before the merge: the roles filled until the check script of the verification gives
     no warning.
5. **`feat(genorder)`: the component options follow the roles** (decision 7).
   - The component options of a scenario are the `Additive` components (`OrderContext.fs:189`).
   - A `Diluent` component of the dose rule is the diluent of the medication: `Medication.fs:990`
     adds the first diluent of the solution rule only when no component has that role, so the
     component is not in the order twice.
   - The contract and the client do not change: `Components` stays a string array in
     `Shared/Api.fs`, and the "Componenten" multi-select (`Prescribe.fs:597`) shows what the
     server sends, so it shows the additives only. The main component and a diluent component
     are no longer in the list; they are always in the order.
   - Tests:
     - the options of a rule with a main, an additive and a diluent component are the additive;
     - a rule with a diluent component and a solution rule naming it gives one component for it.

## Left out

- **The effect of `IsReconstitute`.** The Reconstitution `Diluents` column is parsed
  (`Product.fs:171`) but read nowhere. Matching it to the `Reconstitute` products needs its own
  issue; the role is parsed and carried so that issue needs no sheet change.
- **The glucose components of `Samenstelling B` to `E`.** Their `Solutions` are empty, so the
  glucose component is a diluent only when its products have `Diluent` and not `Additive`. When
  the pharmacy wants `gluc 10%` as an additive elsewhere, those rules name the glucose in
  `Solutions` instead. A data decision, not code.
- **The FormRoute Divisible lookup** ignores the route and takes the first row of the form. For
  `infusievloeistof` the result depends on the row order. Its own issue.
- **The virtual product text.** A virtual product prints with a blank id, and its medication text
  does not parse back. Its own issue.
- **Amphotericin colloidaal.** No dose rule, and its form `INFUUS` is not valid. With
  `Alternative` filled it has the right name once a rule exists.
- **`UseForm` and `UseBrand`.** The existing TODO.
- **Nutrient substances dropped by a brand or HPK.** A dose rule row with a `Brand` or `HPKs`
  drops the nutrient column substances from its product. A fluid on its GPK gets the trade
  products of that GPK, so a rule that names a brand or HPK for it loses those substances. Its
  own issue.
- **Data findings for the pharmacy:**
  - the `Primene` rule names `CaCl2 10%`, which no Formulary row has; the rows are `CaCl2 3,3%`
    and `CaGluc 10%`;
  - a SolutionRules `Component` `ALGLUCOSIDASE ALFA` is upper case, so the case sensitive
    comparison finds no product.

## Verification

- Script tests per step, then `dotnet run servertests`.
- Before and after each step, a script on the full sheets compares these; only the intended rows
  may differ:
  - the products per name;
  - the dose rules with a virtual product;
  - the diluents of every solution rule;
  - the Divisible of every virtual product.
- After step 1 and the data change: the lipid and liposomal dose rules have GPKs 106488 and 92266,
  with concentrations. Checked in a script on 2026-11-09 with `Alternative` simulated: each rule's
  Component and GPK select its product.
- Before each fluid move: a script on the full sheets lists the dose rule rows that name the
  fluid as component with a form or a route the G-Standaard product on its GPK lacks (decision
  3). The move waits until the list is empty.
- After step 2 and the move of `NaCl 0,9%`: an order in NaCl 0,9% has the same totals and volumes,
  and its diluent has id 19100 and shows as `NaCl 0,9%`; the dose rules that named `NaCl 0,9%`
  as component before the move still have it, with the same Divisible.
- Before step 4 merges: a check script on the full sheets lists every rule with a component
  without a product with its role, with the role each component gets, and every solution rule
  that names solutions without a `Diluent` product. The pharmacy fills the roles until the list
  is empty; step 4 merges after that, so no rule loses a product.
- After step 5: the component options of the 11 rules with more than one component are their
  additives, and their diluent components stay in the order; the orders of the shared scenarios
  (`pcmSupp`, `amfo`, `morfCont`, `tpn`) are unchanged.
