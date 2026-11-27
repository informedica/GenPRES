# Implementation plan for issue #1288: divisibility of a medication

2026-10-31. Line numbers are from `master` at `fa422c6d`. This plan closes #1288.

Every number comes from running the code in F# Interactive, in production mode (`GENPRES_PROD=1`, the
`*.cache` files), with the sheet set in the local `.env`, which is not the demo sheet of `.env.example`.
Patients are the presets `Patient.newBorn`, `infant`, `toddler`, `teenager` and `adult`. A filter is an
indication, generic, route and dose type, the picks that make `getRules` return rules with the form
left open. The numbers show exposure and not how often a prescriber meets a case.

## Problem description

The issue started as a Div that was ignored for the orderable dose quantity, which commit `72375f66`
fixed. The investigation found the problems below.

**A retried order has no step and no warning, and often cannot be brought to one dose.** When all rules of a filter give
nothing, `OrderContext.getScenarios` evaluates again (`OrderContext.fs:1377`) with
`changeRuleProductsDivisible` (`OrderContext.fs:496`). This sets the Divisible of every product to
`None`, and creates a product for a component limit without products. A Div of a solution rule
survives the retry (`Medication.fs:977`), so an order with such a Div keeps that step. In none of the
620 retried filters does a dose type hold such an order beside one without a step, so the strict
preparation cannot drop a retried scenario today.

Of the 8277 filters over the five patients:

| Result | Filters |
|---|---|
| Scenario on the first evaluation | 7386 |
| Scenario only through the retry | 620 |
| No scenario | 269 |
| Threw an exception | 2 |

- The retry reaches every form with a Divisible, not only solid forms. A filter counts for every form
  it holds: tablet 213, injection fluid 121, capsule 91, powder for injection 91, film-coated tablet
  64, coated tablet 38, drink 29, modified-release tablet 14 and capsule 12.
- Example, through `getScenarios`: acenocoumarol for the newborn, from tablets of 1 and 4 mg, gives a
  dose of 0,63 to 0,77 mg, which is 0,18 to 0,22 mg/kg, as 0,16 to 0,77 stuk. Every text block is
  `Valid`, so nothing warns.
- This order cannot be brought to one dose. After `SelectOrderScenario` it stays a range. The median
  and the first value of the dose quantity and of the component quantity are refused with `NotSolved`.
  Picking the 1 mg strength is accepted, and the median and first value of the dose quantity and the
  median of the component quantity are still refused. The percentage of the dose quantity is accepted
  and leaves the range as it is. The client offers the percentage for nutrition only.
- Of the 620 orders, 69 are solved by selecting, 58 by the median of the dose quantity, and 10 stay
  unsolved after that median. For the other 483 the median, the minimum and the maximum of the dose
  quantity are refused and the percentage is accepted; whether it changes these orders was not checked.
  This sweep tried commands one at a time, without first picking a strength.
- Normal orders, a sample of every twelfth filter (629 orders), are comparable only in part: 221 are
  solved by selecting, 193 by the median, 122 stay unsolved after the median, 70 get only the percentage
  or a later command accepted, 20 accept none, and 3 give an error.
- An unsolved order can still be prescribed. `prescribeDisabled` in `Prescribe.fs:215` reads only
  whether the plan is busy and whether the order is in the plan, and `OrderPlan.addOrderContext`
  requires one scenario and no duplicate.
- `Order.Print` has `Warning` text blocks, shown when a value is outside its constraints. Nothing marks
  a retried order: `OrderScenario` has no field for it.

**A product takes the step of another product.** A component takes the first Divisible of its products
(`Medication.fs:849`). The products of 139 of the 7446 component limits with products disagree, 109 in
pieces and 30 in mL, for example paracetamol tablet 500 mg with 4 and 1000 mg with 1. No rule in the 247
filters with such a component limit has two of them.

This gives real orders that the sheet does not allow:

- Losartan for the infant and toddler is 0,5 stuk of the 25 mg tablet, which has Divisible 1. The step
  comes from the 50 mg tablet.
- Adrenaline in anaphylaxis for the toddler is 0,3 mL of 0,5 mg/mL, which has Divisible 1. The step
  comes from the 1 mg/mL product.
- Colecalciferol drops for the infant show 1,11 to 2,22 druppel beside 20 to 50 druppel. The droplet
  products have 24 and 36 drops per mL, all take the step of 1/36 mL, and the druppel unit stays at the
  default of 20 drops per mL.

**Droplets depend on one value.** For a droplet form Divisible is the number of drops per mL, so the
step is one drop. `DoseRule.addFormLimits` sets the drop size on the form limit only when the droplet
products of the dose rule share one value. 15 of the 121 droplet component limits hold two values, all
colecalciferol. No droplet component limit holds a product of another form.

**A Divisible can be missing.** 132 of 2788 products in the dose rules have none. Most are nasal
sprays, nebuliser liquids, creams and patches. 17 are dispersible tablets and 4 soft capsules. Another
71 generics have a dose rule but no product in the formulary, so there is no row to give a Divisible. 17
component limits mix products with and without a Divisible, in 4 sets: budesonide, ipratropium and
salbutamol/ipratropium nebuliser liquid, and everolimus dispersible tablet. For everolimus 5 mg (GPK
147990) both the formulary row and the form-route row of `dispergeerbare tablet` are empty.

**The step calculation is not correct within a unit group.** The components of an order add up to the
orderable (`[orb]_orb_qty = sum([cmp]_orb_qty)`), so they share a unit group, and not always a unit: a
component written in L beside one in mL solves, and one in mg beside one in mL is refused with
`SolverInvalidEquations`. No real medication mixes units today: 0 of the 1078 with several components.
`divisibility` (`Medication.fs:1334`):

- compares units by equality where the unit group is what matters;
- compares raw numbers to find the coarsest step, so 1/2 mg beats 1/10 g, which is 100 mg;
- throws `DivideByZeroException` on a Div or Divisible of 0 and gives a negative step on a negative
  value. No sheet has such a value, and a medication text reaches this code without a sheet load,
  because `numberOptFromText` (`Medication.fs:474`) accepts any number.

## Approaches considered

**Products that disagree:**

1. Keep the first value. Product order keeps deciding the step.
2. Take the coarsest value. A 500 mg tablet can no longer be divided.
3. Set the step to 1. In pieces this means not divisible and loses valid halves. In mL it means steps
   of 1 mL, and for droplets a step of 1 mL.
4. Take the largest common divisor. Only 25 of the 139 limits end above 1.
5. Split the rule into one rule per Divisible, so each group of products has its own step.
6. Keep one medication and tighten the step once the strength is known. Recalculation has to apply the
   step again after every change of strength.

Approaches 3, 4 and 5, prototyped on the same 247 filters with a conflict, of which 238 have a scenario
today:

| Approach | Filters with a scenario | Lost | Gained |
|---|---|---|---|
| 3. Step of 1 | 231 | 7 | 0 |
| 4. Common divisor | 233 | 5 | 0 |
| 5. Split | 237 | 3 | 2 |

The split takes the number of scenarios from 590 to 737 and the total evaluation time from 19.5 s to
19.6 s. The 3 lost are the losartan and adrenaline orders above, so the split shows a problem and does
not cause it. The 2 gained are colecalciferol for cystic fibrosis, for the teenager and the adult. The
split does not make the retry rarer: 9 of these filters have no scenario today and 10 with the split.
The prototype puts products without a Divisible in a group of their own, as the chosen approach does.
It ran before the strict preparation, so it does not count the scenarios that the preparation filter
then drops.

**A retried order:**

1. Keep it unmarked.
2. Delete the retry. An order is refused when no dose fits.
3. Keep it with a warning, and mark forms that must stay whole. Forms have exceptions in practice, so
   no strict rule can be given.
4. Keep it with a warning for every form.

## Chosen approach

**The retry stays, with a warning, for every form (approach 4).** The retry runs only when nothing else
remains, and a range gives no quantity to prepare, so the clinician decides. The warning covers forms
that must stay whole. A prescription with a warning needs a pharmacy or second validation, which is the process of the
hospital and outside GenPRES.

**A retried order that stays a range may be prescribed.** A range that cannot be narrowed is right
here: a single dose would give a preparation that does not match it. A retried order that does reach
one dose has no step either, so it gets the same warning and no preparation text. Prescribing is the judgement
of the clinician, as it is for an order that crosses a bound. What GenPRES owes the clinician is that
such an order is clearly flagged and can be told apart from an order according to the rule, also in an
order plan and after signing.

**Products that disagree: the split (approach 5).** It loses no valid half and keeps every step of the
sheet. The split happens in `getScenarios`, on the prescription rules, before `evaluateRules`, so a
lookup without a patient shows the dose rules unchanged and GenFORM stays usable as a formulary. The
retry runs on the rules as they were before the split. Each rule keeps its own solution rules, so
`Array.zip` in `evaluateRules` stays aligned. `Array.tryHead` at `Medication.fs:849` stays, because
each group then has one value. Droplets and volumes are administered differently, so separate scenarios
are right for them.

Products without a Divisible form a group of their own, and never join a group with a value, because
that would give them a step the sheet does not give, chosen by product order. Such a group has no
increment, so it gets no preparation text, and the preparation filter drops it beside a group with a
preparation of the same dose type. For everolimus the 5 mg dispersible tablet therefore stays out of
the list until the sheet has its Divisible, also when the form is picked, because the 0,25 mg tablet has
the same form. Once the sheet is filled the product joins the group with its value without a code
change.

The split needs one hidden pick. The prescriber chooses a group by picking a scenario. A later lookup
with the same picks returns all groups, because `Filter` cannot tell them apart. This happens in the
workbench when the patient is changed (`OrderContextCommand.UpdatePatient`), which keeps the filter, and
on a plan row (`OrderPlan.fs:120`) when the diluent or the selected components change or the resources
reload. `contribution` then gives nothing until a scenario is picked again, so the row leaves the plan
totals. The filter therefore gets the field `PickedProducts`: the products of the picked scenario, which
every scenario already carries. It is not shown. `updateFilterIfOneScenario`, which sets the form and the
diluent, sets it. `applyChange`, which every change of a choice of the rule lookup passes through, clears
it, and so does `seedFilter` when a choice differs. A change of the diluent, the selected components or
the patient, and a reload, keep it, because those are the reasons it exists. `Filter.reconcile` copies
it. `getScenarios` reads it per rule, so a rule that did not split is never narrowed. Nothing is lost when
the field cannot decide:

- Products that no group holds select nothing, so all groups return.
- A group that gives no scenario after the patient changed, such as a corrected weight, does not start
  the retry: all groups are evaluated first, and the retry runs only when all of them give nothing.
- A group without a Divisible can be picked, because it has products.

**The rules that change are medical validation.** The split changes orders, for example adrenaline in
anaphylaxis for the toddler falls from 0,3 mL into the retry, because the 0,5 mg/mL product has
Divisible 1. Whether that value is right is settled in the medical validation of the release. Only
released software is used, so the steps need not be safe to use one by one.

**Missing values and rules that only fit through the retry are validation issues.** Code reports them
and does not guess. The rule owner corrects the sheets.

**A preparation is strict, and the rules decide it.** An order can be prepared when the orderable
quantity of its main component, the first component, has a defined increment and a maximum or a single
value. The Divisible gives the increment, and the rules are the clinical validation of what is possible.
The test reads the order and not the printed text, and the preparation filter uses the same test. A
diluent does not count: it has no maximum when it fills up to a volume, in 69 scenarios.

| The order | Scenarios | Preparation text | Line in the prescription text |
|---|---|---|---|
| A Divisible and a limit | 9740 | yes | none |
| A Divisible and no limit | 384 | no | warning |
| No Divisible | 880 | no | note, the `Caution` severity |
| Retried | | as above | warning, not the note |

A product with a Divisible should have a preparation, so an order of it without a limit needs a
matching rule: dobutamine infusion fluid has a step of 0,1 mL and no maximum. The warning is the same
text as for a retried order. The solver calculates values without an increment, such as bosentan 3,91
stuk, and those must not count. `OrderVariable.hasIncrement` cannot be the test: it is false for an
order whose quantity is a list of values, and `SolutionRule.openDoseCounts` gives no warning for these
orders. No form is accepted without a Divisible: the 880 hold nasal spray, cream, nebuliser liquid and
ointment beside dispersible tablet and soft capsule, so a sheet that is not filled gets the note, and
the load warning reports it to the rule owner.

The administration text stays as it is. A clinician sees from 0,16 - 0,77 stuk why the preparation is
missing. What that means in practice is outside GenPRES.

**`divisibility` keeps a check, on the unit group.** The step sets the unit of the orderable quantity of
a component, so a common step would overwrite the unit of a component in another unit group: morphine in
mg in an orderable of 50 mL would then solve. `divisibility` therefore leaves out a component outside the
unit group of the orderable, and a component outside the unit group of the step gets no step, keeps its
own unit, and the sum of the components refuses the order. It compares steps in base units and gives no
step for a value of zero or less.

## Confidence

Medium.

- Step 1 is high: the tests of `divisibility` pass, and on 28,451 lookups for 13,665 real medications
  no step changes. A test sets a step in L on components in mL and the order solves. That order then
  shows the quantities in L, which no real medication does today. A first migration without the check
  on the unit group failed the test that refuses mg beside mL. The script showed the refusal only
  through the callers of the library, which do not see a shadow, so the check is proved in the script
  by reapplying the steps to the order, and through the real callers only after its migration.
- Step 2 is high.
- Step 3 is high for the mark and the strict preparation, and medium for the mark reaching an order
  plan and a signed version, which is not tested yet.
- Step 4 is medium to low: the split is prototyped on `evaluateRules` and the preparation filter, not
  through `OrderContext.evaluate` with a scenario selected. The hidden filter field, the evaluation of
  all groups before the retry, and the drop size for colecalciferol are not tested.

## Steps

Each step is one pull request of at most 200 changed lines of shipped code, written and tested in a
script first. A step that is too large is split further when it starts.

1. **`divisibility`.** Migrate `src/Informedica.GenORDER.Lib/Scripts/Divisibility.fsx`, with its tests,
   and delete the script. `withOrderableDose` (`Medication.fs:1550`) drops the product argument and
   `withComponentQtyConc` (`Medication.fs:1375`) calls the new `componentDivisibility`. A component in another
   unit of the group then gets the common step, where the library gave it only the steps in its own
   unit. A test through `Medication.toOrder` shows that a component in mg beside mL is still refused.

2. **Validation messages.** Through the existing load messages, the `Message list` of a
   `ResourceLoader` (`Resources.fs:40`), in the way of `DoseRule.invertedRanges`, report a product of
   any form without a Divisible and a component limit that mixes products with and without one. Report
   a component limit in mL whose products disagree, for the rule owner to judge. A product without a
   GPK has no row to fill in and is left out. Give the rule owner the list of the retried filters.

3. **The strict preparation and the warning.**
   1. Mark a retried order with the field `FitsOnlyWithoutDivisible` on `OrderScenario`. It goes on the
      domain type, `create`,
      `Dto`, `toDto` and `fromDto` in `OrderContext.fs`, `Shared/Types.fs:506`, `Shared/Models.fs:1179`,
      and both directions of `ServerApi.Mappers.OrderContext.fs`. In the tests it goes on
      `FilterFixtures.fs`, `Tests.fs` and `OrderPlanTests.fs` of GenORDER and on
      `MappersOrderContextTests.fs`, `StubAdapterTests.fs`, `MappersOrderPlanTests.fs` and
      `OrderContextCommandTests.fs` of the server. Client.Core needs nothing. The field survives a
      change of the order, because `processScenarioOrder` keeps the rest of the scenario.
   2. Pass the mark into `OrderScenario.fromRule`, because it calls `setOrderTableFormat` itself
      (`OrderContext.fs:167`), and have the retry branch of `getScenarios` say that it ran. Add the
      warning in `setOrderTableFormat` as a `Warning` text block in the prescription text, because
      `filterScenariosByPreparation` reads whether the preparation text is empty. The wording holds
      for every form, because the retry reaches liquids as well as tablets, and names the need for a
      pharmacy or second validation.
   3. Test that the mark and the warning reach the printed order, an order plan and a signed version.
      A signed version is a stored row, so its JSON structure version goes from 7 to 8
      (`SqlDatabase.jsonVersionRead` and `jsonVersionWritten`) with a fixture `order_plan_v8.json`, and
      a row of version 7 reads as false.
   4. Check the text a prescriber gets on clicking a range, because the commands are refused with
      `NotSolved`.
   5. Print no preparation text for an order that cannot be prepared, and have
      `filterScenariosByPreparation` read that test and no text. As it was, the filter counted a row
      without blocks as a preparation, and a row with an empty block as none, also beside a block with
      text. In each dose type a scenario that cannot be prepared is dropped beside one that can: 229 of
      the 11,004 scenarios, for example amoxicilline dispersible tablet beside suspension. Test that
      picking the form brings such a scenario back when no other scenario of that form can be prepared.

4. **The split.**
   1. Split the prescription rules by the Divisible of the products of a component limit in
      `getScenarios`, with the products without a value as a group of their own. A test holds three
      products with 1, none and 2, in every order, and gets the same three groups.
   2. Add `PickedProducts` to the filter. `Filter` records are built in `GenORDER.Lib/OrderContext.fs`,
      `GenORDER.Lib/Dtos.fs`, `Shared/Models.fs`, `ServerApi.Adapters.fs` and
      `ServerApi.Mappers.OrderContext.fs`. A signed version stores the filter, so its JSON structure
      version goes from 8 to 9 with a fixture `order_plan_v9.json`, and a row of version 8 has none.
      The tests:
      - a picked group that no longer fits the patient gives the other groups, not the retry;
      - the field survives a reconciliation of a plan row and the Dto of a signed version;
      - each of the five choices of the rule lookup clears it, and the diluent, the components, the
        patient and a reload keep it.
   3. Test through `OrderContext.evaluate`, with a scenario selected, that the commands work on a
      split scenario.
   4. Set the drop size per group, as `addFormLimits` does on the form limit (`DoseRule.fs:382`), from
      `GetFormRoutes` and `GetRouteMappings` of the provider. The test builds a dose rule with two
      droplet products as a fixture, because the tests run without the sheets.
   5. List the rules that change for the medical validation of the release: losartan for hypertension
      for the infant and the toddler, and adrenaline in anaphylaxis for the toddler, all three now through
      the retry with the warning. Colecalciferol for cystic fibrosis is new for the teenager and the adult.

5. **Write down the meaning.** Update the `///` comments on Divisible and Div, on the product, the
   component and the `Data` records: what each value means, that for droplets the step is one drop, and
   what no increment means for the dose quantity and the component orderable quantity. Check the list on
   the issue and close it.
