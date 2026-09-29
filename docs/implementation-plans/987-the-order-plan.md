# Implementation plan for issue #987

Closing G6, the order plan, of [the grouping index](ux-issue-grouping.md#g6--the-order-plan-987):
stepping an order from the plan table, and signing with a dialog that lists what changed.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)

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
| The adjust action (#399) | No adjust button. A plan cell that holds a stepable order variable opens a quantity field on hover, and the order is stepped from there without opening the order dialog. A click on the row still opens the dialog. |
| How the field shows (#399) | A popover anchored to the cell, so columns and rows do not move. On the card layout, a tap on the value opens the same popover. |
| The nurse's view (#510) | Leaves G6 and stays open as its own issue, item 3.5 of M3. |
| What the sign dialog lists (#648) | The order contexts that are new or changed since the order plan version was last opened or signed. |
| Removed orders (#648) | Listed too. Each row is marked new, changed or removed. |
| A context without an order (#648) | A context of the version that no longer contributes an order, because it holds several candidates or none again, counts as removed. A new context that contributes no order yet is not listed. |
| Nutrition rows (#399) | The same rule applies to every plan row, drug and nutrition alike. |
| The term cases (#648) | The maintainer grants the edit of `Shared/Localization.fs` in the sign dialog step. |
| Nothing changed (#648) | The dialog shows one line saying there is no change, and signing stays allowed. |

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
- **One pure difference over the opened contexts and the plan.** New, changed and removed come
  from one function in `Client.Core`, and `changed` equals its new and changed part. Chosen.
- **Block signing when nothing changed.** That would change `SigningPolicy.canSign`, which
  allows signing a plan as it is. Rejected by the decision above.

## Chosen approach

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

/// The order contexts that contribute an order and are new or changed since the version last
/// opened or signed, in plan order, followed by the contexts of that version that contributed
/// an order and no longer do: gone from the plan, or holding several candidates or none again.
let differences (opened: OrderContext[]) (plan: OrderPlan) : (OrderContext * Difference)[]
```

- The rule compares contributions, the order a context holds once it is narrowed to one
  scenario, as the dialog shows them. A context that contributes no order has nothing to show:
  new, it is left out; of the version, it is removed.
- `changed` keeps its meaning. A test holds that, over the contexts that contribute an order,
  the ids of `differences` without the removed ones equal `changed`.
- `OrderPlanState.differences` takes the plan and reads `Opened` the way `OrderPlanState.changed`
  does.
- `IOrderPlan` gets a `Differences: OrderPlan -> (OrderContext * Difference)[]` member beside
  `Changed`. It takes the plan, so the dialog passes the plan it shows and there is one source.

**The view**, `Views/SignDialog.fs`:

- The list reads `IOrderPlan.Differences` over the plan of the signing phase, in place of every
  order of that plan.
- A row shows its order as today, with a tag for new, changed or removed. A removed row reads
  its order from the opened context.
- An empty list is replaced by one line: no change since the last signed version.
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
- A cell opens only when `QuantityModePolicy.decideFor` says the variable is stepable. A cell
  in any other mode keeps its text, and a click opens the order dialog as today.
- The rule applies to nutrition rows as to drug rows. The nutrition view offers no frequency
  steps, so the frequency of a nutrition order becomes stepable from the table alone.

**The view**, `Views/OrderPlan.fs` and `Components/ResponsiveTable.fs`:

- A `PlanCell` component shows the cell text. On hover, or on keyboard focus, it opens an MUI
  `Popper` anchored to the cell with the quantity field. The popper stays open while the
  pointer is over the cell or the popper.
- The steps are built with `ViewHelpers.createStepper` and `createDoseQtyStepper`. Their
  callbacks send `Navigate` with the row's own context id, not the selected one.
- The renderer finds the row's context by the row id, as `contextOf` does; the grid row holds
  strings only.
- A click inside the popper does not reach the row, so neither the grid's row click nor the
  card's click opens the dialog. On a card, the tap on the value stops at the cell too, so the
  card's click does not open the dialog.
- The field is disabled unless the plan is settled and no signing is under way.
- A revision counter moves on every plan answer, so the value shown before the answer arrives is
  dropped the way the order dialog drops it.
- The two stepable columns get a `renderCell`. `ResponsiveTable` gets an optional cell renderer
  for its card layout, so a tap on a stepable value opens the same popper.
- The row `actions` stay `None`. No actions column is added.

### #510 leaves the group

- The G6 section of the grouping index marks #510 as left the group, and says where it went.
- After this plan is merged, #510 is removed as a sub-issue of #987, with a comment on #510.
- The gap overview already holds #510 as item 3.5 of M3 and does not change.

## Confidence

- The sign dialog: High. The rule is small, and the data it needs is already in the machine.
- The stepable cells: Medium. How a hover popper behaves inside the DataGrid, with focus, the
  row click and row virtualization, has to be proven in the browser.

## Steps

One pull request at a time, each merged before the next. Code outside the client is prototyped
in a script and migrated by the maintainer; client code is committed locally and pushed after
the maintainer has checked it in the browser. `scripts/CheckDependencyRule.fsx` runs after each
code pull request.

1. **This plan** and the G6 section of the grouping index (docs).
2. **The difference rule.** A script in `src/Informedica.GenPRES.Client.Core/Scripts/` with
   `differences` and its tests:
   - a new, a changed and a removed context;
   - the order: plan order, then the removed contexts;
   - an unchanged plan gives an empty list;
   - a changed argumentation counts as changed;
   - a change of the plan's filtered rows or the totals alone counts as no change;
   - a context of the version that holds several candidates again counts as removed;
   - a new context that contributes no order is left out;
   - over the contexts that contribute an order, the new and changed ids equal `changed`.

   The maintainer migrates it to `HeldContextPolicy`, `OrderPlanMachine` and the Client.Core
   tests.
3. **The sign dialog.** The four term cases in `Shared/Localization.fs` with their English texts
   in `SigningPolicy`, `AppEnv.IOrderPlan.Differences`, its implementation in `App.fs`,
   `Views/SignDialog.fs`, and the Localization sheet rows.
4. **The plan cell rule.** A script with `PlanCellPolicy` and its tests: each column for a
   discontinuous, a timed, a once, a once timed and a continuous order; a solved against an
   unsolved order; an order with more than one component; a nutrition order. The maintainer
   migrates it.
5. **The stepable cells.** `PlanCell` with its popper, the `renderCell` on the two columns and
   the card cell renderer in `ResponsiveTable`. The Fable output is checked, and
   `npx vite build` runs.
6. **Docs and issues.** This plan gains its as-built table, the G6 section its status, and #399
   its description of the decided behaviour. #399, #648 and #987 close.

## Verification

- The rules: the scripts' Expecto tests, and after migration
  `dotnet test tests/Informedica.GenPRES.Client.Core.Tests/`.
- In the browser, with `dotnet run` at <http://localhost:5173>:
  - open a signed plan, change one order, add one and remove one, and sign: the dialog lists
    three rows marked changed, new and removed;
  - sign again without a change: the dialog shows the line saying there is no change, and the
    signature goes through;
  - hover the frequency cell of a solved discontinuous order and step it: the row shows the new
    value and the order dialog does not open;
  - hover the frequency cell of a continuous order, which shows the rate, and the solution cell
    of a discontinuous order;
  - repeat on a narrow window, where the table shows cards and a tap opens the field.
- `dotnet run servertests`, `dotnet fantomas --check` and `scripts/CheckDependencyRule.fsx`.
