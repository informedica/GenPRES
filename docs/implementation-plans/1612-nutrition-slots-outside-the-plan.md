# Implementation plan for issues #1612 and #1632: nutrition slots outside the plan, and linked orders

2026-11-03. Draft for review. Closes #1612 and #1632. Unblocks rule 10 of
[plan 1430](1430-patient-data-changes.md) for nutrition, and the gap its step 5 names: a pick in a
slot with an order calculates the order on the patient data the slot was made on. Revised the same day:
which orders are linked is decided by link rules, data in the LinkRules sheet, and what a nutrition
slot offers by rule sets, data in the RuleSets sheet, not by code.

<!-- markdownlint-disable-file MD029 -->

## Problem description

A nutrition slot is an order context in the plan from the first click. A category button sends
`NewOrderContext`; the server checks `OrderPlan.admits`, discovers the filter from the category's
rule set and appends the context. Every pick is an `EvaluateContext` over the patient the context
holds, narrowed to the rule set. A slot narrowed to one scenario contributes its order, so a composed
TPN is in the totals, the interactions and the signed version without a prescribe. The TPN prescribe
button exists for the step this plan builds, and does nothing yet; rule 2 gives it its work.

The medication workbench works differently: it stays outside the plan on its own lane, and an order
enters the plan only on the prescribe button. The only real difference between the two is that a slot
already fixes part of the filter through its rule set. The slot in the plan needs its own handling:
on a change of patient data a slot without an order starts again from its rule set, and a slot with an
order is recalculated on the old data at the next pick, without a notice.

The link between a supplement and its feeding is implicit today: the plan holds one feeding at most,
removing the feeding removes every supplement, and the code decides this by nutrition category. Once a
nutrition order is prescribed like any other order, the plan can hold two feedings, and a supplement
must say which feeding it belongs to. Some medication orders belong together in the same way (#1632):
ataluren is given as 10 mg/kg in the morning and at midday and 20 mg/kg in the evening, two dose rules
that are one treatment. Which orders belong together is medical knowledge, so it belongs in data that
GenPRES reads, as the dose rules are, not in code.

What a slot offers is code as well: `ServerApi.NutritionRuleSets.fs` names, per nutrition category, the
indications and generics its dose rules are filtered on. The lists drifted from the DoseRules sheet:
checked on 2026-11-03, the feeding slot offered 7 of the 28 feedings (breast milk, the hypoallergenic,
amino acid and peptide feedings hidden), the TPN and lipid slots hid a generic each and named some that
no dose rule has, and the electrolyte and glucose slot named an indication that does not exist, so it
offered nothing.

## The rules

### Slots

1. **A slot is a workbench with part of its filter decided.** It stays outside the plan, on the
   nutrition page, until it is prescribed. Its rule set gives the indications and generics, and the
   server narrows its lists to that rule set on every evaluation.
2. **A nutrition order is prescribed like any other order.** A prescribe button on every slot adds the
   slot narrowed to one scenario to the plan, keeping its nutrition category, together with the linked
   orders chosen in it (rule 9). The plan takes any number of orders of every category: no category
   refuses an order. The refusals left are the one every order has, the same order twice, and the
   link refusals of rules 14 and 15.
   Medical decision, 2026-11-03, halcwb: a nutrition order is prescribed like any other order, and the
   plan takes any number per category.
3. **After a prescribe the slot empties,** once the order is in the plan, as the medication workbench
   does. The page stays the nutrition page, so the next slot can be composed straight away. While the
   add is out, the slot that was prescribed takes no pick, and no slot can be prescribed, since the plan
   takes one change at a time; picks in the other slots and the category buttons stay open. A failed
   add leaves the slot as it was.
4. **A slot does not count as an order.** It is not signed, not checked for interactions and not in
   the plan's totals. The nutrition page shows the totals over every order of the plan, every slot
   narrowed to one scenario, and every linked order turned on in a slot and narrowed to one scenario,
   so the combined intake is seen against the norm before a prescribe. The order plan page and the sign
   dialog count only the plan.
5. **A change of patient data starts a slot again from its rule set** for the new data; its picks go,
   as the workbench's filters do. Another patient removes the slots, and the combined totals with them.
6. **A slot is not stored.** After a reload only the orders of the plan remain, as with the workbench.
   A slot narrowed to one scenario counts as work that leaving the page or starting a new patient would
   lose, as a medication picked on the workbench does, so the question is asked.
7. **A prescribed nutrition order lives on the order plan page.** It is stepped and removed there and
   in its order dialog, like a medication order, under rule 10 of plan 1430. The order dialog offers
   the percentage given as the dose quantity, the step that only the nutrition slot offers today. The
   nutrition page shows the slots and the totals only.

### Link rules

8. **The link rules decide which orders are linked.** The LinkRules sheet holds one row per link: the
   dose rule of the linked order (Generic, Route, Indication, DoseType, DoseText), the dose rule of its
   main order (MainGeneric, MainRoute, MainIndication, MainDoseType, MainDoseText), and Required. An
   empty field matches any value, except DoseType and MainDoseType, which every row fills in (rule 10).
   So a row "Indication Enterale toevoeging, DoseType discontinuous under MainIndication Enterale
   voeding, MainDoseType discontinuous" links every supplement to any feeding. An order matches a side
   of a row on the values it carries: its generic, route and indication, and its dose type with the
   dose type's text. Required yes means that the linked order cannot be prescribed without its main
   order. No row, no link: nothing is refused, the order is simply not offered as linked.
9. **The main order offers its linked orders.** When the workbench or a slot is narrowed to one scenario,
   the link rules whose main side matches it give the linked orders that may come with it: one optional
   order context per matching row, and for a row with an empty Generic one per generic the row matches.
   So a feeding slot offers its supplements as a list, each to turn on. The same linked order twice is a
   second prescribe. A linked order context is evaluated on the same patient and composed like any
   order context: its picks go out on the lane of its slot or of the workbench, one request at a time,
   and the slot or the workbench waits while one is out. A change of the main order's scenario drops
   the linked order contexts and offers them again from the link rules. The prescribe adds the main
   order with the linked orders turned on, in one request: all of them enter the plan, or none, and a
   turned-on linked order not narrowed to one scenario refuses the prescribe.
   Decisions, 2026-11-03, halcwb: one linked order per row, as a list; a change of the main order
   offers the linked orders again.
10. **Only doses that repeat are linked.** A linked order runs side by side with its main order, so both
    sides of a link rule have the dose type discontinuous, timed or continuous. A once or onceTimed
    dose follows another dose in time and belongs to a protocol (#1641), not to a link: a loading dose
    and its maintenance are not linked. Both dose types must be filled in: an empty dose type would
    match any dose type, once and onceTimed included. A row with an empty dose type, or with once or
    onceTimed, on either side is refused when the sheet is read.
11. **A link is one level and never changes.** A main order can have any number of orders linked to it,
    and is not linked itself. A different link is a removal and a new prescribe, as rule 10 of plan 1430
    has for everything that is not a step.
12. **Removing a main order removes the orders linked to it.** Removing a linked order removes that order
    only.
13. **The order plan page shows a linked order directly under its main order.** Checking a main order
    checks the orders linked to it, and unchecking it unchecks them; while a main order is checked, the
    orders linked to it stay checked. A linked order can be checked alone while its main order is not
    checked. So the rows checked are always the rows the totals count and the delete button removes.
    The link is part of the signed version, so a reopen shows it. A group locks together: an order is
    locked when it, its main order, an order linked to it, or an order linked to the same main order
    cannot be calculated on the current patient data. An order locked only because of its group says so
    with a text of its own, a new localization term; the sign dialog still names only the orders not
    calculated. Removal stays possible.
14. **The shape of a link is checked wherever the server reads a plan:** a main order that is not in the
    plan, or a main order that is itself linked, refuses the plan. The link rules are not checked there:
    the sheet is live configuration, and a signed version must stay readable after an edit of the
    LinkRules sheet. A link that held when it was prescribed holds in that version. A supplement
    without a link, from a plan signed before this change, is read as it is; once the plan slots go
    (step 10) it stays in the plan when its feeding is removed. The decision that no stored version
    matters yet covers it (2026-11-03, halcwb).
15. **The link rules are checked at the prescribe.** The server checks every linked order of the add
    against the link rules as they are then, as it checks a filter pick against the dose rules: a link
    no row allows any more is refused, and so is an order whose link rule requires a main order and that
    comes without one. A refusal names the order.

### The supplement

16. **A supplement is a linked order of a feeding.** The LinkRules rows of the supplements say so, with
    Required yes, so a supplement is only offered in a feeding slot, as an optional linked order of the
    feeding composed there. The separate supplement slot goes. A breast milk fortifier has MainGeneric
    MM, the breast milk generic of the DoseRules sheet, so it is offered only under breast milk.

### Rule sets

17. **The rule sets decide what a slot offers.** The RuleSets sheet holds one row per category,
    indication and generic: Category, Label, Indication and Generic. A slot offers the dose rules under
    the indications of its category, and of those only the generics its rows name; a row names a
    generic by its label, as the dose rules offer it, with the brand or the form in brackets when the
    dose rule is restricted to one. A category whose rows leave Generic empty offers every generic
    under its indications. Label is the category's name
    in the server's words.
18. **The categories and what each does stay in code.** Category names one of the five categories the
    code knows: EnteralFeeding, EnteralSupplement, TPN, Lipid and ElectrolyteGlucose. Each has its page,
    its buttons and its slot, and the TPN its composition, its intake slider and the lock of its
    composition while its dose is a share of the total. A signed version stores the category by name,
    so the names stay as they are. A row with another category is refused when the sheet is read.
19. **A linked order takes its category from the rule sets.** The category of a linked order context is
    the category whose RuleSets rows match its indication and generic, and the medication category when
    no row matches: a Fantomalt supplement under a feeding is EnteralSupplement, ataluren's evening dose
    is medication. Decision, 2026-11-03, halcwb.
20. **An order that needs a main order is only offered as a linked order.** The medication workbench and
    every slot leave out of their pick lists the dose rules a link rule with Required yes matches on its
    linked side; such an order is offered only by its main order (rule 9). The check of rule 15 stays
    as the guard. Decision, 2026-11-03, halcwb.

## What this replaces

- `OrderPlanCommand.NewOrderContext`, `OrderPlanChange.NewNutrition`, the `NewNutrition` member of
  `AppEnv` and its use in `App.fs`, the `NewOrderContext` case in `PlanWorkPolicy`, and the Trail lines
  for `NewNutrition` and `NewOrderContext`.
- The slot discovery in the plan port. The narrowing and settling in the plan port's `evaluateContext`
  stay, for a prescribed nutrition order stepped in the order dialog.
- `OrderPlan.holds`, the category rule of `OrderPlan.admits`, `OrderPlanError.CategoryHeld` with its
  words in `ServerApi.Mappers.OrderPlan.fs`, and the client copy `OrderPlan.mayAdd`.
- `OrderPlanError.SupplementNeedsFeeding` and every rule in code that names a nutrition category for a
  link: the link rules of the LinkRules sheet replace them.
- The supplement slot and the supplement category button on the nutrition page.
- `ServerApi.NutritionRuleSets.fs`, the rule sets as code: the RuleSets sheet replaces it.
  `NutritionCategory.label` in `Shared/Models.fs` stays: the client names a category with its
  localization terms and uses that label only when a term is missing, and the print uses it.
- `GenORDER.Lib/Nutrition.fs` (`tpnConstraints`, `proc`, `applyPropChange`), which no code calls.
- The restart of a context without an order in `OrderPlan.followPatientData`: every context in the plan
  has an order.
- The supplement removal by category in `OrderPlan.removeOrderContext`, replaced by the link.
- `OrderPlan.addOrderContext` setting every added context to the medication category.
- The nutrition page busy on every plan request (`BusyPolicy.changes`).
- Plan 667's choice to keep the nutrition workbenches in the plan, and plan 654's rule that nutrition
  is signed as soon as a slot holds one scenario: only a prescribed nutrition order is signed.

## Approaches considered

1. **Keep slots in the plan and give them their own data handling.** Rejected: every fold over the plan
   keeps an in-plan test for unfinished work, a composed slot is signed without a prescribe, and rule
   10 cannot apply to nutrition.
2. **One client machine over every slot.** Rejected: the slots evaluate independently and the
   workbench machine already holds a single context with its tests.
3. **A machine per slot, the workbench machine reused, on one request lane for the slots.** Chosen:
   the slot is the workbench with a rule set, so the same machine and its tests carry over once the
   machine keeps the category. One lane for the slots keeps one slot request out at a time, apart from
   the workbench and the plan.
4. **Linked orders as a group of equals.** Rejected: removal would need a question, and the supplement
   rule a separate check. A main order with linked orders is the supplement rule itself.
5. **The link decided in code, by nutrition category.** Rejected: which orders belong together is
   medical knowledge that changes with the dose rules, and a category says nothing about ataluren or a
   breast milk fortifier.
6. **The linked order chooses its main order among the orders of the plan.** Rejected: a list of plan
   orders to pick from lets the prescriber pick the wrong one, and a link rule then checks after the
   fact what it could have offered. The main order offering its linked orders (rule 9) makes the rules
   drive the order, as everywhere else.
7. **Link rules as columns on the DoseRules sheet.** Rejected: a link names two dose rules, and the
   columns would repeat on every dose rule row of a generic.
8. **Loading doses and series as links.** Rejected: they follow each other in time and need a start and
   an order of steps, which a link does not have. They are protocols (#1641).
9. **Combined totals in the client.** Not possible: a totals line is text, a sum rendered with the norm
   for the patient's weight and age from the totals sheet (`Totals.getTotals`), so two lines cannot be
   added.
10. **Combined totals in every slot request.** Rejected: a plan answer, such as a removal on the order
    plan page, would not refresh them, and every slot request would carry the plan and the other slots.
11. **A separate totals query, the latest wins.** Chosen: sent when a plan answer or a slot answer lands,
    numbered, an answer to an older query dropped, as the interaction check does in the loader machine.
12. **A second prescribe queued while the first is out.** Rejected: it is a request after another
    request's answer, the pattern #1626 describes. The prescribe buttons wait instead (rule 3).
13. **The rule sets kept in code.** Rejected: the lists drifted from the DoseRules sheet without anyone
    noticing, and a slot that offers too little is a silent failure. In the sheet the lists sit next to
    the dose rules they name, and a read checks them against each other.
14. **The category derived from the Formulary product type.** Rejected: the type splits enteral from
    parenteral, and IsAdditive a supplement from a feeding, but nothing tells a TPN from a lipid, and a
    lipid with vitamins has several components as a TPN has.

## Chosen approach

### Rule sets (GenFORM)

- A `Data` record for the RuleSets sheet, one field per column with its `///` comment, read by a mapper
  with `get` by column name, and a column-contract test. GenORDER's `NutritionRuleSet.ofData` makes the
  rows of a category one `NutritionRuleSet`, the record the order plan rules already take, and refuses a
  row whose Category names no category with `OrderPlanError.UnknownCategory`, since the categories are
  GenORDER's. `NutritionRuleSet.sheetName` gives the name of every category in the sheet.
- A row with an empty Generic is expanded at load into one row per generic the dose rules have under
  its indication, by its label, as the evaluation offers it, so every rule set lists its generics and `NutritionRuleSet.discover` stays as it is.
  An evaluation replaces an indication list in which nothing is chosen by every indication the rules
  have for the patient, so the rule lookup does not filter on the indications of a rule set; the
  generic list is what keeps a medication out of a nutrition slot. A category without a rule set is refused with
  `OrderPlanError.NoRuleSet`.
- A resource in `Resources.defaultRegistry`, reached through `IResourceProvider`; a sheet that does not
  load fails the whole load. The order plan port reads the rule sets from the provider on every use, so
  that a reload of the sheet is seen.
- A second resource, as the renal rule checks are, gives only warnings: a category with more than one
  label, an indication or a generic that matches no dose rule, and a generic or component under the
  indication of a row that has no product in the Formulary sheet.

### Link rules (GenFORM)

- A `LinkRule` record and its `Data` record for the LinkRules sheet, one field per column with its
  `///` comment, read by a mapper with `get` by column name, and a column-contract test.
- A resource in `Resources.defaultRegistry`, reached through `IResourceProvider` as the dose rules are.
- The read refuses a row with an empty DoseType or MainDoseType, and a row with once or onceTimed on
  either side (rule 10). It warns for a side that matches no dose rule, as an order can never match it.
  The dose type is read with GenFORM's `DoseType.parse`, which refuses nothing: an empty or unknown value
  gives `NoDoseType`, with a warning for an unknown one. So the read refuses a row whose DoseType or
  MainDoseType parses to `NoDoseType`, which covers the empty dose type, and reports the warning with
  it.
- A row with both dose types empty is blank and skipped, as the RuleSets sheet skips a row without a
  category; the row numbers in the messages count the blank rows. A route on a side compares by the
  route mapping, as the dose rule filter does. The check reuses `RuleSet.DoseRuleNames`, with the route
  and the dose type added. Decisions from the review of step 4a, 2026-11-04, halcwb.

### Domain (GenORDER)

- `PlanContext` gains `LinkedTo: string option`, the id of the context whose order is its main order,
  and none for an order that is not linked. The DTO and the signed record carry it. The name does not
  clash with the version head of a signed plan (`SignedOrderPlan.Head`).
- `OrderPlan.linkError` checks the shape of a link (rule 14): the main order is in the plan and is not
  linked itself. `OrderPlan.Dto.fromDto` refuses a plan with a broken link with a new `DtoError` case
  that carries the context id and the `OrderPlanError`.
- `OrderContext.rulesForMainOrder` gives, for an order context narrowed to one scenario, the link rules
  whose main side matches it; `OrderPlan.linkRuleError` checks the link of a plan context against the
  link rules (rule 15), with the `LinkError` cases `NotAllowed` and `MainOrderRequired`.
- `OrderPlan.addOrderContexts` adds a main context with its linked contexts in one change: each gets a
  minted id, the linked ones their main order's new id, and the change is refused as a whole when one
  of them is. It keeps the category of every context.
- `OrderPlan.removeOrderContext` removes the context and every context linked to it. Until the plan
  slots go (step 10) a feeding also takes every supplement without a link, as today, so that master keeps
  working between the steps.
- `OrderPlan.followPatientData` loses its restart: every context has an order.
- `PlanContext.Intake` is renamed `Totals`, as the plan's own field is named, in the record and the DTO.
- The stored plan is the JSON of these DTOs, so the link and the rename change its shape: the JSON
  structure version of a stored plan goes from 12 to 13, in the same pull request.
- Stored versions are not converted and the DTO reads no old field names. User decision, 2026-11-03,
  halcwb: no stored version matters yet. `SqlDatabase.jsonVersionOldest` is 13, so a row of version 1
  to 12 loads as an unreadable entry with its reason; the fixtures of versions 1 to 11 and their tests
  go, and the version 12 fixture stays to show that such a row is unreadable. Decision, 2026-11-04,
  halcwb.

### Server

- The wire `OrderContext` (`Shared/Types.fs`) gains `LinkedTo`, and `OrderContextMapper` maps it both
  ways, so that the link survives every round trip, the add and a reopen.
- The words of the new `OrderPlanError` cases in `ServerApi.Mappers.OrderPlan.fs`, and of the new
  `DtoError.BrokenLink` in `ServerApi.Mappers.OrderContext.fs`, come with step 2: a match without them
  is incomplete.
- `OrderContextCommand` gains no case to start a slot: a slot starts with the clear of all filters over
  the empty context of its category, which the order context port narrows and settles to the rule set
  (step 7).
- The order context port narrows and settles a nutrition context to its rule set on every evaluation.
  It finds the rule set from the `Category` field of the wire `OrderContext`; the plan port finds it
  from the plan, which a slot is no longer in.
- The order context answer carries, for a context narrowed to one scenario, the linked orders its link
  rules offer, each an order context evaluated on the same patient, with the category of rule 19.
- The order context port leaves out of the pick lists the dose rules a Required link rule matches on
  its linked side (rule 20).
- `OrderPlanCommand.AddOrderContext` carries the main context and its linked contexts; the plan port
  checks them against the link rules (rule 15).
- The plan port's `evaluateContext` refuses, with `OrderPlanError.NotNarrowed`, a result that is not
  exactly one scenario for a context that held an order before the command, so that a prescribed order
  never loses it. While slots are still in the plan, an unfinished slot evaluates as today.
- A totals query: the patient and the orders of the plan, of the slots narrowed to one scenario and of
  their turned-on linked orders narrowed to one scenario,
  answered with `Totals.getTotals`. A query of its own, so the plan is not changed by it.
- The wire `OrderContext.Intake` is renamed `Totals` (step 3). The comment on `OrderContext.Id` says
  that a slot carries its slot id there, minted by the client, and the medication workbench an empty
  id; the server mints a new id at the add, as it does today.
- `NewOrderContext` and the slot discovery in the plan port go.

### Client.Core

- `OrderContextState` keeps the category of its context. On new patient data and on a reset it sends
  the clear of all filters over the empty context of its category, a medication or a nutrition category.
- `Lanes` gains the slots: an `OrderContextState` per slot id on a new request lane, with its case in
  `Helpers/Loads.fs`, a line in `BusyPolicy.changes` for every page, and the lane in `OutPolicy`. The
  nutrition page is busy on a slot request; `Page.Nutrition` leaves the plan request line of
  `BusyPolicy.changes`. While a plan request is out, the prescribe button of every slot is disabled,
  and the slot that was prescribed takes no pick (rule 3).
- The workbench and every slot hold the linked orders offered with their main order, each turned on or
  off, each with its own order context. A pick in a linked order context goes out on the lane of its
  slot or of the workbench, and that slot or the workbench is busy while it is out. A change of the
  main order's scenario drops the linked order contexts, and the answer offers them again.
- `LanesMsg.Prescribe` names the slot id, or none for the workbench, and adds the main order with the
  linked orders turned on. `OrderPlanEffect.OrderAdded` carries the id of the context in the
  `AddOrderContext` the machine sent: an empty id empties the workbench, a slot id that slot.
- The totals query as a numbered request in the loader machine, sent when a plan answer or a slot
  answer lands, and when a linked order in a slot is turned on or off, which needs no answer of its
  own; an answer to an older query is dropped. A new patient and a start-over send the empty
  query, as they send the empty interaction check, so the totals clear and an answer still out for the
  previous patient is dropped.
- `UnsignedWorkPolicy.hasUnsignedWork` and `NewPatientPolicy.asks` count a slot narrowed to one scenario.
- `PlanContextPolicy.locked` takes the plan, for the group lock of rule 13; its callers pass it.
- The row check of rule 13 as a policy over the plan's links.

### Client views

- The nutrition pages read the slots from `Lanes` and the combined totals from the query; a prescribe
  button on every slot, the linked orders offered in a slot (the supplements in a feeding slot), the
  prescribed slot waiting.
- The workbench shows the linked orders offered with its order, each to turn on.
- The order plan page shows a linked order under its main order, the group lock text, and the row check
  of rule 13.
- The order dialog gets the intake slider for an order where `PlanContextPolicy.canSetIntake` holds.

## The sheets as found

### RuleSets

The sheet was added on 2026-11-03 and filled from the DoseRules sheet the same day. Every indication
exists, every row pairs a generic with an indication it has dose rules under, and every nutrition dose
rule is offered: 28 feedings, 23 supplements, 13 TPN, 2 lipids and the electrolyte and glucose mixture.
Open in the data:

- "electrolyt/glucose mengsel" has no product in the Formulary sheet, and its dose rules have NaCl 3% and
  KCl 7,4% as components, no glucose.
- "CaCl2 10%", a component of a neonatal TPN, has no product in the Formulary sheet.
- Nutrilon Pepti 1, Pepti 2, Standaard 1 and Standaard 2 have dose rules from the age of one year only,
  which needs a medical check for infant formulas.

### LinkRules

The tab was named LinkedRules when it was checked, and renamed LinkRules on 2026-11-04 to match the code.
The sheet was added to the rules spreadsheet on 2026-11-03 and checked against its DoseRules sheet. It
holds 50 rows, every one with the dose type discontinuous on both sides.

- The 23 supplement rows match: every supplement generic has dose rules with the indication "Enterale
  toevoeging", and the main indication "Enterale voeding" exists. The two breast milk fortifiers have
  MainGeneric "MM", the breast milk generic of the DoseRules sheet.
- The rows of ataluren and of the hydrocortison taper (week 0 to 4, oral, intramuscular and intravenous)
  match dose rules on both sides.
- The row of chlooramfenicol links the eye ointment at night to the eye drops. The drops rule has no
  DoseText, so the main side also matches the ointment rule; a DoseText on the drops rule would make
  the main side match the drops alone.
- The rows of ivacaftor in the evening (under ivacaftor/tezacaftor/elexacaftor and under
  tezacaftor/ivacaftor), lumacaftor/ivacaftor with Child-Pugh B, and alimemazine name doses that have
  no dose rule row yet; they match no order until the rows exist.
- Left out: the bowel clearance with bisacodyl, a protocol (#1641), and a bolus on demand next to an
  infusion (morfine PCA, hydromorfon, sufentanil, piritramide, ropivacaine). Such a bolus is a dose
  given as needed, a dose type that does not exist yet; it has an issue and a plan of its own (#1640).

## Confidence

Medium.

- High: the slot as the workbench machine with its category. It is the same type and the same commands.
- High: the link in the plan. One field, on the wire and in the domain, its shape checked where a plan
  is read.
- High: the rule sets. The record and the narrowing exist; only their source moves to the sheet.
- Medium: the link rules. A new sheet and resource follow the existing pattern; the matching on dose
  type and text is new, and the data needs the fixes above.
- Medium: the group add. The plan takes several contexts in one change for the first time, and the
  workbench and the slots hold linked contexts next to their main one.
- Medium: the totals query. It follows the interaction check, but it is a new member and a new
  request after every answer on the nutrition page.
- Risk: the prescribed slot that waits for the plan (rule 3), the gap #1627 closed for the workbench.
  A test must show that a pick in the prescribed slot, and a second prescribe, are not taken while the
  add is out.
- Risk: the restart of a slot from its rule set on new patient data and on a reset. A test must show
  that a slot never becomes an empty medication workbench.

## Steps

Each step is one pull request within 200 lines of `src/`, script first where it touches a library or
Client.Core. Between the steps master keeps working: until step 10 a feeding still takes the supplements
the plan slots add without a link. One gap is accepted: from step 5, which leaves the orders a Required
link rule matches out of the pick lists, until step 9, which offers them under their main order, such
an order cannot be prescribed; from step 6 the prescribe refuses it as well. Today these are the
afternoon and evening doses of the hydrocortison taper and the evening dose of ataluren. No release is
cut from step 5 until step 9 is merged. Decision, 2026-11-03, halcwb.

1. **The rule sets from the sheet.** The RuleSets sheet in GenFORM: the `Data` record, the mapper, the
   column-contract test, the resource with the expansion of an empty generic and the checks of its read;
   `NutritionRuleSet.ofData` and the two new `OrderPlanError` cases in GenORDER; the server takes the rule sets
   from the provider. Script `src/Informedica.GenFORM.Lib/Scripts/RuleSets.fsx`. Every spreadsheet a
   server runs on needs the RuleSets tab before the merge, the demo spreadsheet included. The step
   takes 335 lines of `src/`, over the limit of 200, as one pull request. Decision, 2026-11-04,
   halcwb. Seen: the
   nutrition slots offer every nutrition dose rule, breast milk and the electrolyte and glucose mixture
   included.
   1b. **The hard-coded rule sets go.** `ServerApi.NutritionRuleSets.fs` and the unused
   `GenORDER.Lib/Nutrition.fs` with their tests; the words test in `OrderPlanSwitchTests` takes rule
   sets made in the test. A pull request of its own, for the 200-line limit. Seen: nothing.
2. **The link in the plan.** `PlanContext.LinkedTo`, the rename of `PlanContext.Intake` to `Totals`, the
   DTO, the shape check in `fromDto` and `linkError`, removal by link with the fallback for a supplement
   without a link, `addOrderContext` keeping the category, the words of the new refusals, and the JSON
   structure version 13, older versions unreadable. With it, from the review: the add checks the
   nutrition category with `admits`, the link reasons are a `LinkError` of their own,
   `OrderContext.intake` and `intakeFor` are renamed `contextTotals` and `contextTotalsFor`, and
   `OrderCategory.Drug` is renamed `Medication` in the domain and on the wire, its string
   "medication" in the stored JSON of version 13. The step takes 249 lines of `src/`, over the
   limit of 200, as one pull request. Decision, 2026-11-04, halcwb. Script
   `src/Informedica.GenORDER.Lib/Scripts/LinkedOrders.fsx`. Seen: nothing new; a plan signed before
   the step shows as unreadable.
3. **The link on the wire.** The wire field, both mapper directions, the rename of the wire
   `OrderContext.Intake` to `Totals`. Script
   `src/Informedica.GenPRES.Server/Scripts/LinkedOrders.fsx`. Seen: nothing.
4. **The link rules**, in two pull requests, the first for GenFORM alone.
   - **4a. The LinkRules sheet in GenFORM.** The `LinkSide`, `LinkRequirement` and `LinkRule` records,
     the `Data` record, the mapper, the column-contract test, the resources for the rows, the rules and
     the check, the refusal of a dose type that parses to `NoDoseType` and of once and onceTimed, and
     `LinkRule.matches`. A refused row fails the load. Every spreadsheet a server runs on needs the
     LinkRules tab before the merge, the demo spreadsheet included. The step takes 299 lines of `src/`,
     over the limit of 200, as one pull request. Decision, 2026-11-04, halcwb. Seen: nothing.
   - **4b. The link rules over the order contexts in GenORDER.** `OrderContext.matchesLinkSide` and
     `OrderContext.rulesForMainOrder`, the link rules a narrowed context offers. A context matches by
     the order it is narrowed to: the generic label, indication, route and dose type of its scenario.
     The filter is not read, since selecting a scenario writes only its form there, so the filter can
     hold no route while the order has one (found in the review of #1653). A context that is not
     narrowed matches no side. `OrderPlan.linkRuleError`, beside `linkError`, gives why the link rules
     refuse the link of a plan context, read from its `LinkedTo`, a main order counting only when it is
     narrowed to one order. The `LinkError` cases `NotAllowed` and `MainOrderRequired` come with their
     words in the server. No module of its own: each function goes with the type it handles. Decision,
     2026-11-04, halcwb. Script `src/Informedica.GenORDER.Lib/Scripts/LinkRules.fsx`. Seen: nothing.
5. **The server narrows a slot on the order context path, and offers linked orders**, in three pull
   requests.
   - **5a. The slot on the order context path.** The order context port narrows and settles a nutrition
     context to the rule set of the category it carries, as the plan port does. A refusal while
     settling is answered as the refused context. The rule sets, the discovery of a slot and the
     evaluation settled to a rule set move into `Adapters.NutritionRuleSets`, which both ports use. The
     words of `NotNarrowed` lose the word workbench. Script
     `src/Informedica.GenPRES.Server/Scripts/SlotStart.fsx`. Seen: nothing. Two parts of the first plan
     moved, decisions 2026-11-04, halcwb. The start case of `OrderContextCommand` goes to step 7, with
     the Client.Core code that sends it, since Client.Core takes the command apart with single-case
     patterns. The `NotNarrowed` refusal in `evaluateContext` goes to step 10. The reason for the
     second: the order dialog's commands keep the single scenario of an order, and the only path that
     leaves several is a nutrition slot picked again while it is still in the plan, which is not to be
     refused.
   - **5b. The linked orders in the order context answer.** The wire `OrderContext` gains `LinkedOrders`,
     which the order context answer fills for a context narrowed to one order, through the new port
     member `linkedOrders`. One offer per link rule, and for a rule without a generic one per generic
     that the dose rules offer. Each offer is evaluated on the same patient, narrowed to the dose types
     of its side, and given the category of rule 19 (`NutritionRuleSet.orderCategory`); a rule set is
     read without regard to case, as the link rules are. An offer the rules refuse for the patient is
     left out. Only a command that changes the choices or the scenario computes the offers; a value step
     carries none, since rule 9 offers them again only when the main order's scenario changes. The
     offers never fail the evaluation: when the rule sets cannot be read, the answer offers nothing and
     the reason is logged, since the category of an offer would be unknown. Decisions, 2026-11-04,
     halcwb. The step takes 244 lines of `src/`, over the limit of 200, as one pull request: the review
     added the full case list of `offersLinked` and the shared reading of a rule set, and a later fix
     leaves out an offer whose evaluation finds only another dose type. Decision, 2026-11-04, halcwb.
     Script `src/Informedica.GenPRES.Server/Scripts/LinkedOffers.fsx`, deleted in this step. Seen: nothing.
   - **5c. Rule 20:** the dose rules of a Required link rule left out of the pick lists. The gap between
     step 5 and step 9 opens here. GenFORM derives the dose rules that need no main order once per load,
     `Keys.standAloneDoseRules` over `LinkRule.needsMainOrder`. The order context port evaluates a command
     over a provider whose dose rules are those, `Adapters.withStandAloneDoseRules`, which every dose rule lookup
     passes through. The linked offers are evaluated over every dose rule. The plan port is unchanged, so
     the plan slots still offer the supplements until step 10.

     A refused LinkRules row fails the whole load, which leaves no dose rules, so nothing is prescribed,
     medication included. That is the safe answer for rule 20: without the link rules an order that needs
     its main order would be offered alone. The provider of the pick lists then finds no dose rules in
     the resources and answers with none, in one read, so an evaluation answers with empty pick lists and
     raises nothing, also when a reload fails during the evaluation.
     Decision, 2026-11-04, halcwb.

     The tests use week 1 of the oral hydrocortison taper as the sheet has it. A variant makes the 18:00
     dose Required no, a dose that can also be ordered on its own. Script
     `src/Informedica.GenPRES.Server/Scripts/RequiredLinks.fsx`, deleted in this step. Seen: nothing.
6. **The group add and the totals query** on the server and their wire, in two pull requests, since
   together they go over 200 lines of `src/`. Decision, 2026-11-04, halcwb.
   - **6a. The group add.** `AddOrderContext` with linked contexts, `addOrderContexts` with the check of
     rule 15. The words of `LinkError.NotAllowed` name the main order by its generic, not by its context
     id, which a clinician cannot read; step 4b words it with the id, as the other link words do. The add
     checks that a context is narrowed to one order before it calls `linkRuleError`, since a context that
     is not narrowed matches no link rule side and so escapes `MainOrderRequired`. Decision, 2026-11-04,
     halcwb. A refusal of an order of the group names that order by its generic, with the new case
     `OrderPlanError.OrderRefused`, also for a single add, since every add is a group add. Decision,
     2026-11-04, halcwb. The add sets the links itself: the main order is not linked, and each linked
     order is linked to the main order, whose id is minted first. The single add no longer checks the
     shape of a link, which the group add sets and every read of a plan checks. No code handles a context
     without a generic: the client prescribes only a context narrowed to one order, and every offered
     linked order names its generic. Decisions, 2026-11-04, halcwb. Script
     `src/Informedica.GenPRES.Server/Scripts/GroupAdd.fsx`. Seen: nothing.
   - **6b. The totals query.** The query and its wire: the IServerApi member `processTotals` over the
     patient and the order contexts to count, answered with the totals; the plan port's `totals` over the
     totals data of the provider, and `OrderPlan.totalsOver`, which the plan's own recalculation uses too.
     Script `src/Informedica.GenPRES.Server/Scripts/TotalsQuery.fsx`. Seen: nothing.
7. **The slot lane in Client.Core.** The category in `OrderContextState`, the slot states in `Lanes`,
   the request case, `BusyPolicy` and `OutPolicy`, the start and restart from the rule set. No start case
   on the wire: a slot starts and starts again with the clear of all filters over the empty context of its
   category, as the workbench does over a medication context, since the order context port narrows and
   settles a nutrition context to its rule set from step 5a. That also offers every generic under a rule
   set without generics, which the discovery of the plan port does not. Decision, 2026-11-04, halcwb.
   Script `src/Informedica.GenPRES.Client.Core/Scripts/SlotLane.fsx`, with modified copies of the five
   files it changes, deleted in this step. A slot tells its errors with the alerts of the workbench.
   Another patient arrives as the patient cleared, then the new patient, so a slot is removed when the
   patient is cleared (rule 5), and a data update starts it again from its rule set; while the workbench
   has no patient, no slot starts. Decision, 2026-11-04, halcwb. Seen: nothing; the views still use the
   plan slots.
8. **Prescribe with linked orders in Client.Core**, in three pull requests, since together they go over
   200 lines of `src/`. Decision, 2026-11-04, halcwb.
   - **8a. The slot prescribe.** `LanesMsg.Prescribe` with the slot id, the slot's context sent with its
     slot id, `OrderAdded` with the slot id, or none for the workbench, which empties the workbench or that
     slot, a prescribed slot that stays on the nutrition page, and the unsigned work policies counting a
     slot narrowed to one order, also when its rule set narrowed it without a pick (rule 6), through one
     function both policies call. A prescribed slot taking no pick while the add is out comes with step 9:
     until the views use the slots, a plan request disables the whole nutrition page, so no pick reaches a
     slot then. Script `src/Informedica.GenPRES.Client.Core/Scripts/SlotPrescribe.fsx`, with modified copies of
     the four files it changes, deleted in this step. Seen: nothing yet.
   - **8b. The linked orders.** The linked orders offered in the workbench and the slots, and
     `LanesMsg.Prescribe` with the linked orders turned on. Decisions, 2026-11-04, halcwb:
     - The offers live in `OrderContextState`, beside the context they are offered with, so that the
       workbench and every slot hold them alike.
     - An answer to a change of the choices or the scenario takes the offers it carries, each turned off,
       and none for a refusal; any other answer leaves them as they were, since the server computes the
       offers only for such a change. Both sides read the list of those commands, `offersLinked`, from
       the shared contract.
     - An offer is turned on with one of its scenarios, as the prescribe button belongs to one scenario
       of the main order, and goes into the plan narrowed to that scenario.
     - Commands on an offer, each with its own order context, do not come in this step: the order
       context port evaluates every command over the stand-alone dose rules, so a pick in an offer that
       needs its main order is refused and a dose step drops its picks. They come with the server's
       evaluation of an offer over every dose rule, before or with step 9.
     - A refactor pull request comes first, so that 8b stays under 200 lines of `src/`: `offersLinked`
       moves from the server to `Api.OrderViewCommand`, and `Adapters.forPickLists` is renamed
       `withStandAloneDoseRules`, since every dose rule lookup of the evaluation reads it, not only the
       pick lists.

     How the server tells an offer apart from another context was open here; step 9a decides it: the
     server does not tell them apart, the picks of an offer select the dose rules it needs. When commands
     on an offer come, in step 9b, an offer can change while it is on: the scenario turned on then
     follows the offer's answer.

     Script `src/Informedica.GenPRES.Client.Core/Scripts/PrescribeLinked.fsx`, with modified copies of the
     three files it changes, deleted in this step. Seen: nothing yet.
   - **8c. The totals query in the loader**, with its clear. Decisions, 2026-11-04, halcwb:
     - The client builds the query from the lanes as they were last answered, after every lanes step, and
       sends it when it changed. A plan answer, a slot answer, an offer turned on or off and another
       patient all reach it through that one place, with no effect of their own; a slot answer that lands
       evaluated emits none.
     - The query is empty while no slot is narrowed to one order: the totals clear and nothing is asked,
       since the plan's own totals then hold. Otherwise it holds the patient, the orders of the plan that
       its filter keeps, as the plan's own totals count them, the narrowed slots and their offers turned on.
       A new patient and a start-over empty the slots, and so send the empty query.
     - The query is numbered in the loader, and an answer to an earlier query is dropped. A failed query
       names the totals on the error banner, with an error source of its own.
     - A totals query holds neither the menu nor a page: it starts without a click, as the drug names do.
     - `TurnLinked` is renamed `TurnOfferOn` and `TurnOfferOff`, one case for each way. `TurnOfferOn`
       carries the scenario, not its order id, since an offer does not change while it is on.

     Script `src/Informedica.GenPRES.Client.Core/Scripts/TotalsLoader.fsx`, with modified copies of the
     files it changes and of the files that read their types, deleted in this step. Seen: nothing yet.
9. **The views.** On the nutrition page a prescribe button on every slot, the supplements offered in the
   feeding slot, staying on the page, the combined totals; on the workbench the linked orders offered;
   on the order plan page a linked order under its main order, the row check and the group lock text
   of rule 13; the intake slider in the order dialog. An offer takes every command the supplement slot
   takes today: the picks, the dose steps and the percentage. Decisions, 2026-11-04, halcwb:
   - An offer is composed like any order context before the prescribe, as a supplement in a plan slot
     is today; turning an offer on with a scenario alone, to dose it on the order plan page, is not
     enough.
   - The server does not tell an offer apart from another context. A dose rule that a Required link
     rule matches on its linked side is left out of every lookup of the order context port unless the
     context as sent, before the command changes it, selects it: a picked indication and generic, a
     list of one option counting as its pick, and a dose type list of exactly that dose type. The lists
     are the server's answer, which a pick or a seed does not change, so only an offer selects such a
     dose rule: the ataluren evening offer lists only the evening dose. The prescribe check of rule 15
     stays the guard. Decisions of the review, 2026-11-04, halcwb:
     - The context as sent, not the context after the command: a formulary seed of the hydrocortison
       dose of 13:00 on the workbench finds no dose rule, while the picks after the seed would select it.
     - The dose type list, not the dose type pick: a refused seed answers the context with the seeded
       pick, and the next command on it, or a reload, would otherwise select the dose rule of 13:00.
     - The extra filter over every dose rule costs about 0.1 ms a lookup, measured over the 5166 rows
       of the DoseRules sheet, next to a lookup that filters every dose rule already; no derived
       resource in GenFORM.
     - The form is left out of the filter that selects those dose rules, so that a clear of the form on
       an offer lists every form again: the oral hydrocortison dose of 13:00 comes as a tablet, a
       capsule, a solution and three more forms. Found by the review of pull request #1679.
   - Found in the script of 9a: an offer whose generic has the dose types of both sides, such as the
     ataluren evening dose and the hydrocortison doses of 13:00 and 18:00, came without scenarios, since
     it was evaluated before its dose types were narrowed to the side. When the narrowing leaves one
     dose type of several, the offer is evaluated once more. A supplement has one dose type, so its
     offer had its scenarios already.
   - The nutrition page removes a slot the prescriber closes, with a message to the lanes that takes
     the slot out; until then only another patient removes a slot, so the slots started only grow.
   - Five pull requests, in this order:
     - **9a. Offers by their picks**, in the server, by script
       `src/Informedica.GenPRES.Server/Scripts/OffersByPicks.fsx`. Tests, over the hydrocortison doses
       of week 1 of the taper: the offer of 13:00 comes with its scenario, a command and a dose step on
       it keep its dose and its scenario, and the medication workbench still offers only the dose of
       7:00; a formulary seed of 13:00 on the workbench is refused, and so is the next command on the
       refused context. A link side whose dose type matches more than one dose of its generic has no test
       and no code, since no row of the sheet has one; such an offer stays without scenarios, as before.
       Issue #1678 proposes a warning for such a row when the sheet is read. The scripts are deleted in
       this step.
     - **9b. Commands on an offer**, in Client.Core, by script: an offer takes a command on its index,
       on the lane of its slot or of the workbench, the lane waits while it is out, and the answer
       replaces the offer; an offer turned on follows its answer. An offer takes no pick and no clear on
       the fields its link side decides, the generic, the indication, the route and the dose type: a
       clear of the dose type would widen the lists of the evening offer of ataluren to the dose of the
       main order. Decision of the 9a review, 2026-11-04, halcwb. The rule lives in a Client.Core policy
       that the offer view of 9d reads to show which filter fields of an offer are open, as FieldOpenPolicy
       does for other fields; the machine sends any command. A command the policy closes is refused by
       the add check of rule 15 all the same: an evening offer of ataluren cleared and picked again as
       the dose of the main order is not allowed under it. Decision of the 9b review, 2026-11-04, halcwb. Decisions, 2026-11-04, halcwb:
       - An offer is turned on or off as a switch, as rule 9 has it; the scenario is the one the offer is
         narrowed to, chosen with a command on the offer as any scenario is. A dose step keeps the order
         of a scenario and a pick makes a new one, so a scenario carried with the switch would be lost
         at the next pick. An offer turned on and not narrowed to one scenario is left out of the totals
         and refuses the prescribe, as after a form clear on the hydrocortison dose of 13:00.
       - A refusal of a command on an offer keeps the offer as the server sent it back and tells why; a
         failure leaves it as it was. An offer has no refused state of its own: its lists are the
         server's answer for the same patient as the main order.
       - The context and its offers share the one request of the machine, so the lane waits on either,
         and the page of the context is not shown changing while an offer's request is out.
       Script `src/Informedica.GenPRES.Client.Core/Scripts/OfferCommands.fsx`, with modified copies of
       the three files it changes, deleted in this step.
     - **9c. The slot removal, the group lock and the row check**, in Client.Core, by script:
       `PlanContextPolicy.locked` takes the plan, and the row check of rule 13 as a policy over the
       plan's links. Two pull requests if it does not fit. Done in one, 2026-11-04:
       - `LanesMsg.RemoveSlot` takes a closed slot out, with a trail line of its own; an answer still out
         for it is dropped. Whether the close waits while the slot's prescribe is out is for the view of 9d.
       - `PlanContextPolicy.lock` names why an order is locked, by itself or by its group, so that the
         view of 9e can show the text of the group lock; until then the order plan page marks only the
         orders not calculated, as before.
       - `PlanRowPolicy.checkedRows` gives the rows checked from the rows the table asks for over the
         rows the plan has checked.
     - **9d. The nutrition page**: the prescribe button on every slot, the supplements offered in a
       feeding slot with the controls of a slot, the prescribed slot waiting, the combined totals and
       closing a slot. Measure the totals query here: the client builds and compares it after every
       lanes step, which walks the full contexts while a slot is narrowed. If it shows, compare only
       what the query depends on, or build it only after a step on the plan or a slot.
     - **9e. The workbench, the order plan page and the order dialog**: the linked orders offered on
       the workbench, a linked order under its main order with the group lock text and the row check,
       and the intake slider. Two pull requests if it does not fit.

   Seen: nutrition is composed in slots and enters the plan on prescribe; ataluren is prescribed with
   its evening dose linked.
10. **The plan slots go.** The plan refuses a filter command on a context it holds, which only a slot
   in the plan sends until then, so that a prescribed order never loses its single scenario. What is left
   under "What this replaces": `NewOrderContext` and its client and Trail lines, the slot discovery,
   the supplement slot, `holds`, `CategoryHeld`,
   `SupplementNeedsFeeding`, `mayAdd`, the restart in `followPatientData`, the removal fallback for a
   supplement without a link. Seen: no change; the old path is gone.
11. **Documents.** Notes in plans 667 and 654. In plan 1430, step 5's sentence "the nutrition page keeps
    its slots until a new order is prescribed" becomes "a slot on the nutrition page keeps its
    composition until its order is prescribed; then the slot empties", and rule 11's "The lock holds on
    the order plan page and the nutrition page" becomes "The lock holds on the order plan page and in
    the order dialog". The browser tests in document 598.

## To decide

- Adding a linked order later to a main order already in the plan, such as a fortifier added to breast
  milk on the third day. Under rule 9 that is a removal of the feeding and a new prescribe of the group.
  The order plan page could instead offer the link rules of a main order in the plan and add one under
  it; the shape check of step 2 already allows it.
- Whether Required yes on the hydrocortison and ataluren rows means "cannot stand without the main
  order", as for a supplement, or "always comes with the main order", a dose of the schedule that is
  not optional. The second needs a value of its own.
- `OutPolicy.out` takes eight positional arguments, seven of them fields of `LanesState`, so each new lane
  changes every call. Issue #1671 proposes that it take the lanes; outside this plan.

## Verification

Tests at the level of every rule. In the client: open a feeding slot for an infant and see breast milk
and the hypoallergenic feedings offered, and an electrolyte and glucose slot offer its mixture; compose
a TPN and an electrolyte line, prescribe both, see the slots empty and the combined totals stay; pick
in the prescribed slot and press another prescribe while the add is out, and see neither taken; compose
breast milk with a fortifier and a formula feeding, see the fortifier offered only with breast milk,
prescribe both, remove the breast milk and see the fortifier go; turn a composed fortifier off and on
and see the combined totals follow each time; change the weight with a slot open and see it start again
from its rule set; start a new patient while a totals query is out and see no totals of the previous
patient; prescribe ataluren with its evening dose linked, check the main order and see both checked and
the evening dose not uncheckable, remove it and see both go; a linked order locked by its main order
shows the group text; edit the LinkRules sheet and see a signed version with a link the sheet no
longer has still open. A medical check of the combined totals, of two TPN orders in one plan, of the
link rules, and of a TPN prescribed after a change of weight.
