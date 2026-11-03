# Implementation plan for issues #1612 and #1632: nutrition slots outside the plan, and linked orders

2026-11-03. Draft for review. Closes #1612 and #1632. Unblocks rule 10 of
[plan 1430](1430-patient-data-changes.md) for nutrition, and the gap its step 5 names: a pick in a
slot with an order calculates the order on the patient data the slot was made on.

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
and removing the feeding removes every supplement. Once a nutrition order is prescribed like any other
order, the plan can hold two feedings, and a supplement must say which feeding it belongs to. Some
medication orders belong together in the same way (#1632): a morning dose and an evening dose that
differ are two orders.

## The rules

### Slots

1. **A slot is a workbench with part of its filter decided.** It stays outside the plan, on the
   nutrition page, until it is prescribed. Its rule set gives the indications and generics, and the
   server narrows its lists to that rule set on every evaluation.
2. **A nutrition order is prescribed like any other order.** A prescribe button on every slot adds the
   slot narrowed to one scenario to the plan, keeping its nutrition category. The plan takes any number
   of orders of every category; the one refusal is the one every order has, the same order twice.
   Medical decision, 2026-11-03, halcwb: a nutrition order is prescribed like any other order, and the
   plan takes any number per category.
3. **After a prescribe the slot empties,** once the order is in the plan, as the medication workbench
   does. The page stays the nutrition page, so the next slot can be composed straight away. While the
   add is out, the slot that was prescribed takes no pick; the other slots and the category buttons
   stay open. A failed add leaves the slot as it was.
4. **A slot does not count as an order.** It is not signed, not checked for interactions and not in
   the plan's totals. The nutrition page shows the totals over every order of the plan and every slot
   narrowed to one scenario, so the combined intake is seen against the norm before a prescribe. The
   order plan page and the sign dialog count only the plan.
5. **A change of patient data starts a slot again from its rule set** for the new data; its picks go,
   as the workbench's filters do. Another patient removes the slots.
6. **A slot is not stored.** After a reload only the orders of the plan remain, as with the workbench.
   A slot narrowed to one scenario counts as work that leaving the page or starting a new patient would
   lose, as a medication picked on the workbench does, so the question is asked.
7. **A prescribed nutrition order lives on the order plan page.** It is stepped and removed there and
   in its order dialog, like a medication order, under rule 10 of plan 1430. The order dialog offers
   the percentage given as the dose quantity, the step that only the nutrition slot offers today. The
   nutrition page shows the slots and the totals only.

### Linked orders

8. **An order can be linked to one other order, its head.** A head can have any number of orders
   linked to it, and is not linked itself: one level. Three doses a day with different amounts are a
   head and two orders linked to it.
9. **A link is chosen at prescribe and never changes.** The workbench and every slot offer a choice of
   head among the orders of the plan. A different link is a removal and a new prescribe, as rule 10 of
   plan 1430 has for everything that is not a step.
10. **Removing a head removes the orders linked to it.** Removing a linked order removes that order
    only.
11. **A supplement is linked to a feeding.** The supplement slot is offered while the plan holds a
    feeding order; its head choice lists the feedings, preselected when the plan holds exactly one
    feeding, and it cannot be prescribed without one. A feeding still being composed in a slot does not
    count.
12. **The order plan page shows a linked order directly under its head.** Checking a head checks the
    orders linked to it, and unchecking it unchecks them, so the rows checked are the rows the totals
    count and the delete button removes. A linked order can be checked alone. The link is part of the
    signed version, so a reopen shows it.
13. **A group locks together.** An order is locked when it, its head, or an order linked to the same
    head cannot be calculated on the current patient data. An order locked only because of its group
    says so with a text of its own, a new localization term; the sign dialog still names only the
    orders not calculated. Removal stays possible.
14. **A plan with a broken link is refused** where the server reads a plan: a head that is not in the
    plan, a head that is itself linked, or a supplement whose head is not a feeding.

## What this replaces

- `OrderPlanCommand.NewOrderContext`, `OrderPlanChange.NewNutrition`, the `NewNutrition` member of
  `AppEnv` and its use in `App.fs`, the `NewOrderContext` case in `PlanWorkPolicy`, and the Trail lines
  for `NewNutrition` and `NewOrderContext`.
- The slot discovery in the plan port. The narrowing and settling in the plan port's `evaluateContext`
  stay, for a prescribed nutrition order stepped in the order dialog.
- `OrderPlan.holds`, the category rule of `OrderPlan.admits`, `OrderPlanError.CategoryHeld` with its
  words in `ServerApi.Mappers.OrderPlan.fs`, and the client copy `OrderPlan.mayAdd`.
- `OrderPlanError.SupplementNeedsFeeding`, replaced by the head check of rule 11.
- The restart of a context without an order in `OrderPlan.followPatientData`: every context in the plan
  has an order.
- The supplement removal by category in `OrderPlan.removeOrderContext`, replaced by the link.
- `OrderPlanCommand.AddOrderContext` setting every added context to `Drug`.
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
   rule a separate check. A head with linked orders is the supplement rule itself.
5. **Combined totals in the client.** Not possible: a totals line is text, a sum rendered with the norm
   for the patient's weight and age from the totals sheet (`Totals.getTotals`), so two lines cannot be
   added.
6. **Combined totals in every slot request.** Rejected: a plan answer, such as a removal on the order
   plan page, would not refresh them, and every slot request would carry the plan and the other slots.
7. **A separate totals query, the latest wins.** Chosen: sent when a plan answer or a slot answer lands,
   numbered, an answer to an older query dropped, as the interaction check does in the loader machine.

## Chosen approach

### Domain (GenORDER)

- `PlanContext` gains `Head: string option`, the id of the context it is linked to. The DTO and the
  signed record carry it.
- `OrderPlan.Dto.fromDto` refuses a broken link (rule 14) with a new `DtoError` case.
- `OrderPlan.addOrderContext` keeps the category of the context and checks the head of rule 14 and the
  supplement rule of rule 11.
- `OrderPlan.removeOrderContext` removes the context and every context whose head it is.
- `OrderPlan.followPatientData` loses its restart: every context has an order.
- `PlanContext.Intake` is renamed `Totals`, as the plan's own field is named.
- Stored versions are not converted: no stored version matters yet.

### Server

- `OrderContextCommand` gains a case that starts a slot: the category and the patient, answered with
  the context discovered from the rule set. This is the new case the single-case union was kept for.
- The order context port narrows and settles a nutrition context to its rule set on every evaluation.
  It finds the rule set from the `Category` field of the wire `OrderContext`; the plan port finds it
  from the plan, which a slot is no longer in.
- The plan port's `evaluateContext` refuses a result that is not exactly one scenario with
  `OrderPlanError.NotNarrowed`, so that every context in the plan keeps its order.
- A totals query: the patient and the orders of the plan and of the slots narrowed to one scenario,
  answered with `Totals.getTotals`. A query of its own, so the plan is not changed by it.
- The wire `OrderContext.Intake` is renamed `Totals`. The comment on `OrderContext.Id` says that a slot
  carries its slot id there, minted by the client, and the medication workbench an empty id; the server
  mints a new id at the add, as it does today.
- `NewOrderContext` and the slot discovery in the plan port go.

### Client.Core

- `OrderContextState` keeps the category of its context. On new patient data and on a reset it sends
  the clear of all filters for a medication workbench and the start case for a nutrition category.
- `Lanes` gains the slots: an `OrderContextState` per slot id on a new request lane, with its case in
  `Helpers/Loads.fs`, a line in `BusyPolicy.changes` for every page, and the lane in `OutPolicy`. The
  nutrition page is busy on a slot request; the slot that was prescribed also waits for the plan
  request (rule 3).
- `LanesMsg.Prescribe` names the slot id, or none for the workbench, and the head.
  `OrderPlanEffect.OrderAdded` carries the id of the context in the `AddOrderContext` the machine sent:
  an empty id empties the workbench, a slot id that slot.
- The totals query as a numbered request in the loader machine, sent when a plan answer or a slot
  answer lands; an answer to an older query is dropped.
- `UnsignedWorkPolicy.hasUnsignedWork` and `NewPatientPolicy.asks` count a slot narrowed to one scenario.
- `PlanContextPolicy.locked` takes the plan, for rule 13; its callers pass it.
- The row check of rule 12 as a policy over the plan's links.

### Client views

- The nutrition pages read the slots from `Lanes` and the combined totals from the query; a prescribe
  button on every slot, the head choice on the supplement slot, the prescribed slot waiting.
- The workbench gets the optional head choice.
- The order plan page shows a linked order under its head, the group lock text, and the row check of
  rule 12.
- The order dialog gets the intake slider for an order where `PlanContextPolicy.canSetIntake` holds.

## Confidence

Medium.

- High: the slot as the workbench machine with its category. It is the same type and the same commands.
- High: the links in the domain. One field, checked where a plan is read.
- Medium: the totals query. It follows the interaction check, but it is a new member and a new
  request after every answer on the nutrition page.
- Risk: the prescribed slot that waits for the plan (rule 3), the gap #1627 closed for the workbench.
  A test must show that a pick in the prescribed slot is not taken while the add is out.
- Risk: the restart of a slot from its rule set on new patient data and on a reset. A test must show
  that a slot never becomes an empty medication workbench.

## Steps

Each step is one pull request within 200 lines of `src/`, script first where it touches a library or
Client.Core. Between step 1 and step 8 master may break a rule, for example a feeding removed on master
after step 1 leaves its supplements; no release is cut from step 1 until step 8 is merged.

1. **Linked orders in the domain.** `PlanContext.Head`, the DTO, the link check in `fromDto`,
   `addOrderContext` keeping the category and checking the head, removal by link. Script
   `src/Informedica.GenORDER.Lib/Scripts/LinkedOrders.fsx`. Seen: nothing new; removing a feeding no
   longer removes the supplements the plan slots still add without a head.
2. **The server starts and narrows a slot on the order context path,** and refuses a plan step that
   leaves no order. The new `OrderContextCommand` case, the narrowing by `Category`, the `NotNarrowed`
   refusal in `evaluateContext`. Script `src/Informedica.GenPRES.Server/Scripts/SlotStart.fsx`. Seen:
   nothing; the client does not use the case yet.
3. **The totals query** on the server and its wire, and the rename of `Intake` to `Totals`. Script
   `src/Informedica.GenPRES.Server/Scripts/CombinedTotals.fsx`; the rename has no behaviour. Seen:
   nothing.
4. **The slot lane in Client.Core.** The category in `OrderContextState`, the slot states in `Lanes`,
   the request case, `BusyPolicy` and `OutPolicy`, the start and restart from the rule set. Script
   `src/Informedica.GenPRES.Client.Core/Scripts/SlotLane.fsx`. Seen: nothing; the views still use the
   plan slots.
5. **Prescribe from a slot in Client.Core.** `LanesMsg.Prescribe` with the slot id and the head,
   `OrderAdded` with the context id, the prescribed slot waiting, the totals query in the loader, the
   unsigned work policies. Script `src/Informedica.GenPRES.Client.Core/Scripts/SlotPrescribe.fsx`. Seen:
   nothing yet.
6. **The nutrition views on the slots.** A prescribe button on every slot, the head choice on the
   supplement slot, staying on the page, the combined totals. Seen: nutrition is composed in slots and
   enters the plan on prescribe.
7. **Linked medication orders and the order dialog.** In Client.Core, by script
   `src/Informedica.GenPRES.Client.Core/Scripts/LinkedOrders.fsx`: the group lock of rule 13 and the row
   check of rule 12. In the views: the head choice on the workbench, linked orders under their head, the
   group lock text, the intake slider in the order dialog. Two pull requests if it does not fit. Seen: a
   morning and an evening dose prescribed linked; a TPN stepped in the order dialog.
8. **The plan slots go.** Everything under "What this replaces". Seen: no change; the old path is gone.
9. **Documents.** Notes in plans 667 and 654. In plan 1430, step 5's sentence "the nutrition page keeps
   its slots until a new order is prescribed" becomes "a slot on the nutrition page keeps its
   composition until its order is prescribed; then the slot empties", and rule 11's "The lock holds on
   the order plan page and the nutrition page" becomes "The lock holds on the order plan page and in
   the order dialog". The browser tests in document 598.

## Verification

Tests at the level of every rule. In the client: compose a TPN and an electrolyte line, prescribe both,
see the slots empty and the combined totals stay; pick in the prescribed slot while the add is out and
see the pick not taken; prescribe two feedings and a supplement under one, remove that feeding and see
the supplement go; change the weight with a slot open and see it start again from its rule set;
prescribe a morning and an evening dose linked, check the morning dose and see both checked, remove it
and see both go; a linked order locked by its head shows the group text. A medical check of the
combined totals, of two TPN orders in one plan, and of a TPN prescribed after a change of weight.
