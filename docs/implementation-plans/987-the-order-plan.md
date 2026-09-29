# Implementation plan for issue #987

Closing G6, the order plan, of [the grouping index](ux-issue-grouping.md#g6--the-order-plan-987):
what an order context in the plan can change and when, stepping an order from the plan table,
and signing with a dialog that lists what changed.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)
- [As built](#as-built)

## Problem description

G6 grouped #399, #510 and #648 under #987 as three readings of the same plan table.

- #399 asks for an adjust button on each plan row. Today the order dialog opens only when the
  user clicks a row.
- #648 asks the sign dialog to list only what changed. The dialog lists every order of the plan
  (`Views/SignDialog.fs`). Since the index was written, #1090 added `HeldContextPolicy.changed`,
  which gives the ids of the order contexts that are new or changed since the order plan
  version was last opened or signed.
- #510 asks for a nurse's view of how to prepare an order. It is item 3.5 of M3 in the
  [gap overview](../roadmap/mvpap2019-gap-overview.md), where it builds on one preparation
  document model.

## Decisions

Taken 2026-09-29 by the maintainer.

| Question | Decision |
|---|---|
| The adjust action (#399) | No adjust button. A plan cell that holds a stepable order variable opens a quantity field on a click, and the order is stepped from there without opening the order dialog. A click elsewhere on the row still opens the dialog; the medication name reads as the link to it. |
| How the field shows (#399) | A popover anchored to the cell, so columns and rows do not move, closed with its own button or a second click on the cell. On the card layout, a tap on the value opens the same popover. |
| The nurse's view (#510) | Leaves G6 and stays open as its own issue, item 3.5 of M3. |
| What the sign dialog lists (#648) | The order contexts that are new or changed since the order plan version was last opened or signed. |
| Removed orders (#648) | Listed too. Each row is marked new, changed or removed. |
| A context without an order | A drug context enters the plan only once narrowed to one scenario, and under the plan context rule it cannot widen again. A nutrition context still enters through `NewOrderContext` before it is narrowed; making it enter only once confirmed, as a drug workbench does, is #495 in G7. Until then the difference rule leaves out a context that contributes no order, on either side: new, it has nothing to list; of the version, it was never listed, so it is not removed; narrowed since the version, it is new. The sign dialog leaves such a context out today too. |
| Nutrition rows (#399) | The same rule applies to every plan row, drug and nutrition alike. |
| The term cases (#648) | The maintainer grants the edit of `Shared/Localization.fs` in the sign dialog step. |
| Nothing changed (#648) | The dialog shows one line saying there is no change, and signing stays allowed. |
| What can change in a plan context | Only the frequency, the orderable dose quantity and the orderable dose rate. The plan cells and the order dialog opened from the plan both keep to this. It holds for every context in the plan, one added since the last signature too: another medication or route means removing the order and adding it again. The component orderable quantity of an order with more than one component is fixed as well. Once start and stop are built, they can change too. |
| When a plan context can change | Only while its patient data match the plan's patient data: the whole patient record, the age and the P3 and P97 estimates aside, and the estimated weight and height aside only where a measured one exists. Age changes by itself as time passes and needs a mechanism of its own. Without a measured weight or height, the doses were calculated with the estimate, so an estimate changed by a corrected age locks the context. Later, the rules version joins the context, and a context whose rules version differs is locked too. |
| A locked context | Patient data that change after a signature, a new weight, access or department, lock every context calculated with the earlier data. The row carries a lock mark whose hover text gives the reason. Its cells do not step, the order dialog opens read-only, and the context can still be removed. |
| Reset in the plan dialog | Left out. Reset re-solves from the rules and can change what the rule keeps fixed; undoing a step is another step. The prescribe page keeps it. |
| Changed, for the sign dialog (#648) | A context counts as changed when it holds another order, or one of the three variables or the argumentation differs from the version opened or signed. Other differences are not changes to an order in the plan. |

## Approaches considered

For the adjust action:

- **An adjust button in an actions column.** `ResponsiveTable` renders row actions in its card
  layout only. The DataGrid would need an actions column, and the button would open the same
  dialog that a click on the row already opens. Rejected by the decision above.
- **The cell turns into the quantity field.** The field needs at least 228 px, or 308 px with
  its outer buttons, and the plan columns are 150 px wide. The stepable columns would have to
  widen and the table would reflow on hover. Rejected.
- **A popover anchored to the cell.** The cell keeps its text, and the field floats over the
  table. Chosen.

For the sign dialog:

- **Filter by `changed` alone.** A plan whose only change is a removal would then show an
  empty list while it does change the plan. Rejected.
- **Compare whole contexts, as `changed` does.** A context also differs when its intake or
  filter differs, which the plan context rule does not let the user change. Rejected.
- **One pure difference over the opened contexts and the plan, on what the plan context rule
  lets change.** New, changed and removed come from one function in `Client.Core`. Changed
  means another order, or one of the three variables or the argumentation differs. Chosen.
- **Block signing when nothing changed.** That would change `SigningPolicy.canSign`, which
  allows signing a plan as it is. Rejected by the decision above.

## Chosen approach

### The order context in the plan

Two rules decide what can happen to an order context once it is in the plan. Both hold for every
context in the plan, drug and nutrition alike, and the cells, the order dialog and the sign
dialog all read them from one pure module, `PlanContextPolicy` in `Client.Core`, after
`QuantityModePolicy`:

```fsharp
/// Whether a field of an order context in the plan can change: the frequency, the orderable
/// dose quantity and the orderable dose rate can; every other field cannot.
let editable (field: QuantityModePolicy.Field) : bool

/// Whether the patient data an order context was calculated with match the plan's patient
/// data: the data the doses rest on, the age aside.
let matches (plan: OrderPlan) (ctx: OrderContext) : bool

/// Whether an order context in the plan is locked: its patient data do not match the plan's.
let locked (plan: OrderPlan) (ctx: OrderContext) : bool

/// Whether an order context in the plan changed against its opened or signed version: it holds
/// another order, or one of the editable variables or the argumentation differs.
let changed (opened: OrderContext) (ctx: OrderContext) : bool
```

- `editable` is true for `Frequency`, `DoseQuantity` and `DoseRate`, and false for
  `ComponentQuantity` and `Other`.
- `matches` compares `ctx.Patient` with `plan.Patient` after blanking, on both, the age and
  the P3 and P97 estimates, and the estimated weight or height when a measured one exists.
  `Patient.getWeight` and `getHeight` take the measured value, else the estimate, so without a
  measured value the estimate is what the doses rest on and has to be equal. Every other field,
  the measured weight and height, gender, gestational age, access, renal function, location and
  department, has to be equal.
- `changed` compares the ids of the contributed orders, the values of `Schedule.Frequency`,
  `Orderable.Dose.Quantity` and `Orderable.Dose.Rate`, and the argumentation.
  - An order created anew from the rules gets a new id, so another medication in the same
    context is a change even when its values are equal; a step keeps the id.
  - A value is its numbers and its unit, the unit as its JSON, the serialized unit, which is
    the same in every language and tells 5 mg from 5 g where the unit group does not. The unit
    text and the language are how it is rendered, so the same values rendered otherwise are no
    change.
- The two future additions, start and stop as editable fields and the rules version as a
  second reason to lock, extend these functions and do not change their callers.

**The order dialog opened from the plan**, `Views/OrderPlan.fs` and `Views/Order.fs`:

- `Order.View` gets an `editing` prop, `PlanContextPolicy.Editing`: `Workbench` on the
  prescribe page, and on the plan `PlanContext`, or `Locked` when the context is locked
  (`PlanContextPolicy.editingOf`). `canEdit` decides per field from the key the dialog tracks
  it under (`fieldKind`); `resets` and `argues` decide Reset and the argumentation.
- A field that cannot be edited renders in the Fixed mode of the quantity field, disabled, and
  shows several values without a pick as one range from the first to the last.
- The dialog has two more ways to change a context, and the plan closes both:
  - Reset re-solves the order from the dose rules, which can change fields the rule keeps
    fixed. The plan dialog has no Reset, only Ok; undoing a step is another step. The
    prescribe page keeps its Reset.
  - The argumentation is committed on blur and when the dialog closes. For a locked context
    the field is read-only and nothing is committed: it cannot be stepped, so no new warning
    arises. For a context that is not locked, the argumentation stays editable: a step that
    raises a warning asks for one, as it does on the prescribe page, and it is part of what is
    signed.
- A locked context therefore opens a dialog that sends no command at all.

**The lock mark**, `Views/OrderPlan.fs`: the medication cell of a locked context shows a lock
icon whose hover text gives the reason. Removing the context works as it does today. A new
term, `Plan Context Locked`, holds the reason: the patient data differ from the data the order
was calculated with.

### The sign dialog lists the differences (#648)

What is there:

- `HeldContextPolicy.changed opened plan` returns the ids of the contexts that are new or
  changed, in plan order. A removed context is in neither list.
- `OrderPlanState` keeps the opened contexts in `Opened`, which is private, and exposes
  `changed`. The UI reaches it through `AppEnv.IOrderPlan.Changed`.
- The sign dialog shows the plan its signing phase carries, the plan being signed. The filter
  changes only what the dialog shows. The whole plan is still signed.

**The rule**, in `HeldContextPolicy`:

```fsharp
[<RequireQualifiedAccess>]
type Difference =
    | New
    | Changed
    | Removed

/// The order contexts new or changed since the version last opened or signed, in plan order,
/// followed by the contexts of that version no longer in the plan. A context that contributes
/// no order is left out on either side.
let differences (opened: OrderContext[]) (plan: OrderPlan) : (OrderContext * Difference)[]
```

- The rule compares contexts by id, over the contexts that contribute an order on either side,
  so the view needs no filter of its own.
- A context of the version is changed when `PlanContextPolicy.changed` says so.
- `HeldContextPolicy.changed` keeps its meaning: whole contexts, for holding the patient
  context. A test holds that every context `differences` marks changed is in it.
- `OrderPlanState.differences` takes the plan and reads `Opened` the way `OrderPlanState.changed`
  does.
- The differences are taken when the user signs and carried by the signing machine
  (`SigningMsg.Sign`, `SigningState.differences`) until the signature ends, so a version or a
  patient answer that replaces `Opened` meanwhile does not change what the signer reads. The
  dialog reads them through `ISigning.Differences`.

**The view**, `Views/SignDialog.fs`:

- The list reads `ISigning.Differences`, in place of every order of the plan.
- A row shows its order as today, with a tag for new, changed or removed. A removed row reads
  its order from the opened context.
- An empty list is replaced by one line: no change since the last signed version.
- The dialog text says that the list shows the changes and that the PIN signs the whole order
  plan.
- Four terms are added: `Signing New`, `Signing Changed`, `Signing Removed` and
  `Signing No Changes`. Each needs a case in `Shared/Localization.fs`, an English text in
  `SigningPolicy.english`, and a row in the Localization sheet.

### The stepable plan cells (#399)

What is there:

- Every plan row carries its whole order scenario, with the value sets, the large increment and
  the constraints the order dialog uses. No extra request is needed.
- `Api.OrderPlanCommand.Navigate(plan, contextId, command, context)` steps any context by id.
  `orderContextMsg` in `Views/OrderPlan.fs` takes the id from the selected context; the plan
  cells pass their own.
- The DataGrid accepts a `renderCell` on a column, as `Views/ContinuousMeds.fs` and
  `Views/EmergencyList.fs` already use. The card layout of `ResponsiveTable` renders its cells
  as strings.
- `QuantityModePolicy` decides the mode of a field from its kind, the order and the variable.

**The rule**, a new module `PlanCellPolicy` in `Client.Core`, after `QuantityModePolicy`. For
an order and a plan column it gives the field and the variable the cell steps, or nothing:

| Column | Discontinuous, timed | Once, once timed | Continuous |
|---|---|---|---|
| frequency | `Schedule.Frequency` | nothing | `Orderable.Dose.Rate` |
| solution | `Orderable.Dose.Quantity` | `Orderable.Dose.Quantity` | nothing |
| medication, route, quantity, dose | nothing | nothing | nothing |

- The quantity and dose columns show variables that the server has no step command for.
- A cell opens only when `QuantityModePolicy.decideFor` says the variable is stepable and the
  context is not locked. A cell in any other mode keeps its text, and a click opens the order
  dialog as today.
- Every field the table steps is one `PlanContextPolicy.editable` allows.
- The rule applies to nutrition rows as to drug rows. The nutrition view offers no frequency
  steps, so the frequency of a nutrition order becomes stepable from the table alone.

**The view**, `Views/OrderPlan.fs` and `Components/ResponsiveTable.fs`:

- A `PlanCell` component shows the cell text as a control: a light blue box with a pencil. It is
  a button: a click, Enter or Space opens an MUI `Popper` anchored to the cell with the
  quantity field, and its own close button or a second click closes it. The plan view owns
  which cell is open, one at a time.
- The steps are built with `ViewHelpers.frequencyStepper`, `doseRateStepper` and
  `createDoseQtyStepper`, which the order dialog shares. Their callbacks send `Navigate` with the
  row's own context id, not the selected one.
- A step counts its clicks for 700 ms before it is sent. A click on a step button marks the cell
  as counting until a second after the last click; meanwhile no other cell opens and the sign
  waits, so a step is neither rejected by a signature started first nor replaced by another
  while the plan keeps one waiting. The popper stays mounted while closed, so a counting step
  is still sent.
- The renderer finds the row's context by the row id, as `contextOf` does; the grid row holds
  strings only.
- A click inside the popper does not reach the row, so neither the grid's row click nor the
  card's click opens the dialog. On a card, the tap on the value stops at the cell too, so the
  card's click does not open the dialog.
- The field is disabled unless the plan is settled and no signing is under way. A cell that
  cannot be opened then is grey and leaves the tab order.
- A revision counter moves on every plan answer, so the value shown before the answer arrives is
  dropped the way the order dialog drops it.
- The two stepable columns and the medication column get a `renderCell`, and the same renderer
  as an opt-in `renderCard` column field that the card layout of `ResponsiveTable` calls, so a
  tap on a stepable value opens the same popper and the lock shows on the cards too.
- The medication name reads as a link to the order dialog: bold, blue, underlined under the
  pointer, after the calculator icon of the prescribe page's edit button, or after the lock of a
  locked order.
- The row `actions` stay `None`. No actions column is added.

### #510 leaves the group

- The G6 section of the grouping index marks #510 as left the group, and says where it went.
- After this plan is merged, #510 is removed as a sub-issue of #987, with a comment on #510.
- The gap overview already holds #510 as item 3.5 of M3 and does not change.

## Confidence

- The plan context rules and the sign dialog: High. The rules are small, and the data they
  need is already in the machine.
- The read-only order dialog: Medium. `Views/Order.fs` decides per field today; every field has
  to take the new prop, and a field missed stays editable.
- The stepable cells: Medium. How a popper behaves inside the DataGrid, with focus, the row
  click and row virtualization, has to be proven in the browser.

## Steps

One pull request at a time, each merged before the next. Code outside the client is prototyped
in a script and migrated by the maintainer; client code is committed locally and pushed after
the maintainer has checked it in the browser. `scripts/CheckDependencyRule.fsx` runs after each
code pull request.

1. **This plan** and the G6 section of the grouping index (docs).
2. **The plan context rules and the difference rule.** A script in
   `src/Informedica.GenPRES.Client.Core/Scripts/` with `PlanContextPolicy` and `differences`,
   and their tests:
   - `editable` for each field;
   - `matches` with equal patient data; with only the age different; with a different estimate
     beside a measured value, or different P3 and P97 estimates, which still match; with a
     different estimated weight and no measured one, which locks; and with the measured weight,
     the access or the department different;
   - `changed` for each of the three variables, for the argumentation, and for a filter or
     intake difference alone or the same values rendered otherwise, which are no change;
   - `differences`: a new, a changed and a removed context; plan order, then the removed
     contexts; an unchanged plan gives an empty list; a change of the plan's filtered rows or
     the totals alone is no change; no version at all makes every context new; a context that
     contributes no order is left out, new or of the version, and is new once narrowed; a filter
     difference alone is held and no difference; every context marked new or changed is in
     `HeldContextPolicy.changed`.

   The maintainer migrates it to `PlanContextPolicy`, `HeldContextPolicy`, `OrderPlanMachine`
   and the Client.Core tests.
3. **The sign dialog.** The four term cases in `Shared/Localization.fs` with their English texts
   in `SigningPolicy`, `AppEnv.IOrderPlan.Differences`, its implementation in `App.fs`,
   `Views/SignDialog.fs`, and the Localization sheet rows.
4. **The plan dialog and the lock.** The per-field prop of `Order.View`, the plan passing
   `PlanContextPolicy.editable` or nothing for a locked context, no Reset in the plan dialog,
   a read-only argumentation that commits nothing for a locked context, the lock mark in the
   medication cell, and the `Plan Context Locked` term in `Shared/Localization.fs` and the
   Localization sheet.
5. **The plan cell rule.** A script with `PlanCellPolicy` and its tests: each column for a
   discontinuous, a timed, a once, a once timed and a continuous order; a solved against an
   unsolved order; an order with more than one component; a nutrition order; a locked context.
   The maintainer migrates it.
6. **The stepable cells.** `PlanCell` with its popper, the `renderCell` on the two columns and
   the card cell renderer in `ResponsiveTable`. The Fable output is checked, and
   `npx vite build` runs.
7. **Docs and issues.** This plan gains its as-built table, the G6 section its status, and #399
   its description of the decided behaviour. #399 and #987 close; #648 closed with step 3.

## Verification

- The rules: the scripts' Expecto tests, and after migration
  `dotnet test tests/Informedica.GenPRES.Client.Core.Tests/`.
- In the browser, with `dotnet run` at <http://localhost:5173>:
  - open a signed plan, change one order, add one and remove one, and sign: the dialog lists
    three rows marked changed, new and removed;
  - sign again without a change: the dialog shows the line saying there is no change, and the
    signature goes through;
  - click the frequency cell of a solved discontinuous order and step it: the row shows the new
    value and the order dialog does not open;
  - click the frequency cell of a continuous order, which shows the rate, and the solution cell
    of a discontinuous order;
  - step a cell several times quickly: the sign button waits until the step is sent and
    answered, and no other cell opens meanwhile;
  - repeat on a narrow window, where the table shows cards and a tap opens the field;
  - open an order from the plan: only the frequency, the orderable dose quantity and the rate
    can be changed;
  - change the patient's weight after signing: the plan's contexts show the lock mark, their
    cells do not step, their dialog is read-only with a read-only argumentation, and they can
    be removed; no request leaves the browser while that dialog is open;
  - the dialog opened from the plan has no Reset;
  - change nothing but the argumentation and sign: the dialog lists the context as changed.
- `dotnet run servertests`, `dotnet fantomas --check` and `scripts/CheckDependencyRule.fsx`.

## As built

| Step | Pull request | Note |
|---|---|---|
| 1, the plan | #1177 | Three review rounds added the plan context rules, what counts as changed, Reset and the argumentation. #510 was taken off #987 after the merge. |
| 2, the rules | #1178, #1180 | The script with `PlanContextPolicy` and `differences`, then its migration into `Client.Core`. From the reviews: the estimate a dose rests on counts when no measured value exists, a value compares by its numbers and its serialized unit, and another order in the same context is a change. |
| 3, the sign dialog | #1181 | Closes #648. From the review: the differences are frozen in the signing machine when the user signs, and the dialog text says the PIN signs the whole plan. |
| 4, the plan dialog and the lock | #1183, #1184 | From the review: a fixed field with several values shows them as a range. #1184 moved the editing rules from the view to `PlanContextPolicy`. |
| 5, the plan cell rule | #1185 | Written directly in `Client.Core`, without a script, at the maintainer's request. From the review: the tests assert the variable each cell steps, not only its field. |
| 6, the stepable cells | #1187 | About 300 changed source lines, over the limit, by the maintainer's decision. |
| 7, the closing docs | this pull request | This table, the G6 section of the grouping index; #399 and #987 closed. |

Deviations from the plan, all on the maintainer's check in the browser or on review:

- The cell opens on a click, not on hover, and the popper has a close button.
- The cell reads as a control, a light blue box with a pencil, and the medication name as the
  link to the order dialog.
- Only one cell is open at a time, and while a cell counts its clicks the sign waits and no other
  cell opens, so no step is lost.
- The sign button stays, disabled, while the plan changes, so the table no longer jumps with each
  answer. The row hover bar became an inset shadow that takes no room.
- The frequency and dose rate steppers moved to `ViewHelpers`, shared by the dialog and the cells.

Still to do outside the code: rows in the Localization sheet for `Signing New`, `Signing Changed`,
`Signing Removed`, `Signing No Changes` and `Plan Context Locked`, and the new meaning of
`Signing Dialog Text` in each language.
