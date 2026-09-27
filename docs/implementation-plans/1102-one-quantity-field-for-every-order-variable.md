# Implementation plan for issue #1102

One five-slot QuantityField for every order variable in the order and nutrition views. It is a
sub-issue of [the foundation plan](981-ux-foundation-and-common-components.md), whose component
C1 it redraws.

- [Problem description](#problem-description)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)

## Problem description

The spec *quantity-field-state-transitions*, linked from #1102, gives QuantityField four modes
(Selectable, Navigable, Stepable, Fixed), three activity states that apply in any mode, and six
render invariants. Its central invariant is that the value and its buttons never move when the
mode changes. A dropdown that resolves to a single value that can be stepped keeps its place on
screen.

The current `QuantityField` puts `Stepper` beside the select, and the stepper is only there when
the field has steps. Changing the mode adds or removes the button group, so the row reflows.

Every order variable in `Views/Order.fs` (15 fields) and `Views/Nutrition.fs` (8 fields) already
goes through one wrapper, `ViewHelpers.orderField`. The change can therefore be made in one place,
and it reaches all 23 fields.

## Approaches considered

- **Adjust the current Stepper.** Keep the Stepper beside the select and hide buttons per mode.
  Rejected: the Stepper is a button group whose width changes with its buttons.
- **Five fixed slots with the mode as a type** (the draft attached to #1102). A grid of four
  button slots around the value. An unused button slot is hidden with `visibility: hidden`, so
  it keeps its size. `Mode = Selectable | Navigable of Steps | Stepable of Steps | Fixed`
  replaces `steps: Steps option`. Chosen.
- **A new rule that decides the mode for every field.** Rejected for this issue: it would make
  the two views behave the same, and that changes behavior. Each call site keeps its current
  decisions; see below.

## Chosen approach

The five-slot component, under three rules:

1. The field always ends up showing what the server sends back. A predicted value is shown only
   while a step is in flight, and every answer replaces it.
2. The current behavior does not change: which buttons each field offers, what they send, when
   they are enabled, and the debouncing.
3. Only the visualization changes.

The **median** stays as well, without a button of its own, since the five slots have no place
for one. A navigable field that shows its range picks the median when the user clicks the value,
or presses Enter or Space on it. The dropdown does not open for a range, since it would hold the
range alone. The existing `SetMedian*` commands carry the pick; the server does not change.

### The mode, taken from today's decisions

`ViewHelpers.createStepper` and `createDoseQtyStepper` already decide per field whether there
are steps, and whether the field can be navigated. The mode is read from those decisions:

| Today | Mode |
|---|---|
| steps, and the field is navigable | Navigable |
| steps, and the field is not navigable | Stepable |
| no steps | Selectable |

A field that has steps but offers none of them yet, because it is neither navigable nor solved,
shows four disabled buttons. This matches the five disabled buttons the Stepper shows today.

Fixed stays in the component but no call site produces it, so no field becomes read-only.

The following stay exactly as they are:

- which buttons each mode offers. In Navigable, decrease and increase stay off until the value
  is solved, as now.
- `useDebounce = not navigable && solved`. This includes frequency, which today debounces and
  sends a single command however many clicks it counted.
- frequency has steps in Order.fs and none in Nutrition.fs.
- the check for an empty field, which becomes `xs` empty and mode Selectable. That is today's
  `xs` empty and stepper `None`.
- the activity states:
  - `rests` sets `disabled`;
  - `isOptimisticStep` suppresses the loading state;
  - the revision counters feed `Steps.revision`, which drops a predicted value on every answer.

### What the user sees change

- The value and its four buttons form one segmented group: grey square buttons around a white
  value cell, with neighbouring cells sharing one border line and rounded outer corners.
- The slots keep their size in every mode. A slot without a button is hidden and keeps its
  space, so the value does not move when the mode changes.
- The label is a caption above the group, starting where the value text starts. The select in
  the cell draws no label or underline of its own; the cell border shows the focus, and a click
  on the caption focuses the select.
- The icons per mode:
  - navigating: first, previous, next and last;
  - stepping: small minus and plus, and the large step as text on the outer buttons (`−5` /
    `+5`). That is the server's large increment, or the defined increment when the server sends
    none.
- Every button has a hover text, disabled buttons included.
- The severity mark stands in a column of its own, right of the group. The column is there on
  every field, so the groups line up.
- A field with values to pick from and none picked shows the placeholder "kies een waarde", from
  the term `Pick a value`. A range that picks its median has the hover text "naar mediaan", from
  the term `Pick the median`. Both terms need a row in the Localization sheet for other
  languages.
- A field is never narrower than its buttons plus 120 px of value.
  - An enteral feeding keeps its filter and fields on one row while they fit, and switches to
    one column when a container query finds the row too narrow.
  - In that row, a field that never gets buttons drops its hidden slots.
- The spinner of a reloading order lies over the fields in the order and nutrition views, so the
  fields no longer move.

The button hover texts ("naar minimum", "stap omhoog", ...) are Dutch text in the component.
Translating them through `Terms` goes to a follow-up issue.

## Confidence

High. The change is visual, it keeps every call site's decisions, and the invariants can be
measured in the browser.

## Steps

One PR at a time; each waited for the previous one to be merged. As built:

1. **The mode replaces the stepper option** (#1108).
   - `QuantityField` takes a `Mode`.
   - `createStepper` and `createDoseQtyStepper` return it.
   - Every call site maps its current decision onto the mode.
2. **The five-slot rendering** (#1109). Besides the grid itself:
   - the segmented look, the caption label and the mark column;
   - readable narrow fields and the enteral row that fits on one row or turns into a column;
   - the spinner that no longer moves the fields.

   The debounced buttons stay `ClickCountingButton`. The plan had a new `useClicker` hook in
   its place; keeping the existing button keeps its 700 ms window, its repeat while held and
   its count badge exactly.
3. **The median with a click on a range, and the placeholder** (#1111).
   - This replaced the earlier decision to drop the median.
   - It adds the terms `Pick a value` and `Pick the median`.
   - The range keeps the arrow keys from opening its menu, and tells a screen reader what Enter
     does.
4. **The unused `Stepper.fs` removed** (#1113). The median messages stay, since the click on a
   range uses them.
5. **Docs**: this plan as built, C1 in [the foundation plan](981-ux-foundation-and-common-components.md),
   [the dose quantity stepping flow](../domain/dose-quantity-stepping-flow.md) and G3 in
   [the grouping index](ux-issue-grouping.md).

Left for a follow-up issue:

- the button hover texts through `Terms`;
- the unused active pattern `(|NonNavigable|Navigable|Selectable|Stepable|)` in
  `Shared/Models.fs`;
- optionally, one rule that decides the mode, which would make the order and nutrition views
  behave the same.

## Verification

- **Each step**:
  - `dotnet run build`;
  - compile with Fable and read the JSX output for `QuantityField`, `Order` and `Nutrition`,
    checking the nesting and whether the icon imports are hoisted;
  - `dotnet run servertests`, with `CI=true` in a worktree;
  - `dotnet fsi scripts/CheckDependencyRule.fsx`.
- **In the browser, behavior against master**: take a paracetamol oral solution order and a
  parenteral nutrition order through the same clicks on master and on the branch. The requests
  sent (network tab) and the values shown after each answer must be the same.
- **In the browser, the render invariants** for Step 2:
  1. The five slot rectangles and the row height are the same in Selectable, Navigable and
     Stepable, and while loading.
  2. Going from Navigable to Stepable changes only the icons and tooltips.
  3. A disabled button keeps its slot and shows its tooltip on hover.
  4. Five rapid clicks on a Stepable dose quantity update the shown value five times, send one
     command with n = 5, and the answer replaces the predicted value.
  5. An answer with the same value but a new revision clears the predicted value, for example
     a step at the dose limit.
  6. Holding the small plus of a Stepable dose quantity steps the predicted value every 150 ms.
     Its badge counts the clicks. Releasing it sends one command with the count of all held
     clicks, 700 ms after the last one, and the badge disappears, as on master.
