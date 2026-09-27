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

The one exception is the **median** button. The five slots have no place for it, so it is
dropped. The four `SetMedian*` cases in the shared contract, the server mapper and GenORDER stay
in place until a follow-up issue removes them.

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

- the five fixed slots, with the value in the middle, which keep their size in every mode;
- the icons per mode: first, previous, next and last when navigating, and large and small minus
  and plus when stepping;
- a tooltip on every button, disabled buttons included;
- no median button;
- no click-count badge on a button. The predicted value in the field already follows every
  click.

The tooltips are Dutch text in the component. Translating them through `Terms` goes to the
follow-up issue, because new keys change the shared localization.

## Confidence

High. The change is visual, it keeps every call site's decisions, and the invariants can be
measured in the browser.

## Steps

One PR at a time; each waits for the previous one to be merged. The component alone is too
large for one PR under the 200-line limit, so the work is split into three steps.

### Step 1: the mode replaces the stepper option

- `Components/QuantityField.fs`:
  - add `Mode`;
  - `Props.steps` becomes `Props.mode`;
  - `Steps` loses `median`.

  Rendering stays on the Stepper for now, with `median = None`.
- `Views/ViewHelpers.fs`:
  - `createStepper` and `createDoseQtyStepper` drop the median parameter and return a `Mode`,
    following the table above;
  - `orderField`, `orderSelect`, `orderFixed` and `ovarDisplay` take `mode` in place of
    `stepper`.
- `Views/Order.fs`, `Views/Nutrition.fs`: the call sites stop passing the median messages.
- Changelog entry: the median button is gone.

### Step 2: the five-slot rendering

- `Components/QuantityField.fs` renders the draft's grid in place of the Stepper:
  - four slots around the value, hidden with `visibility: hidden` and never with
    `display: none`;
  - the `useClicker` hook in place of ClickCountingButton. It keeps what ClickCountingButton
    does today:
    - a debounce window of 700 ms, where the draft uses 250 ms;
    - holding a debounced button down repeats the click every 150 ms, on mouse and on touch,
      and stops on release or when the pointer leaves the button;
    - every click, held or not, updates the predicted value at once, and the clicks within the
      window go out as one command with their count;
    - the timers are cleared when the field unmounts;
  - the icons and tooltips per mode.
- The adaptations to the repository rules:
  - the icons come from `Mui.Icons` in `MUI.fs`, next to the existing `FirstPageIcon` and
    `LastPageIcon`, and the missing ones are added there;
  - interpolation with format specifiers (`$"%i{slotWidth}px ..."`);
  - lines under 120 characters;
  - no `private` on the pure helpers `rowSx`, `slotSx` and `slot`. The hook `useClicker` keeps
    `private`.
- Changelog entry.

### Step 3: remove the median messages and the old stepper

- Remove the median messages, `update` branches, `msgToField` cases and prop fields from
  `Order.fs` and `Nutrition.fs`.
- Remove the median callbacks from `Prescribe.fs` and `OrderPlan.fs`.
- Delete `Components/Stepper.fs` and `Components/ClickCountingButton.fs`, and their project
  entries.
- File the follow-up issue. It covers four things:
  - remove `SetMedian*` from the contract, the server and GenORDER;
  - remove the unused active pattern `(|NonNavigable|Navigable|Selectable|Stepable|)` in
    `Shared/Models.fs`;
  - translate the QuantityField tooltips through `Terms`;
  - optionally, one rule that decides the mode, which would make the order and nutrition views
    behave the same.

### Step 4: docs

- Add the modes and the render invariants from the spec to the C1 section of
  [the foundation plan](981-ux-foundation-and-common-components.md).
- Update [the dose quantity stepping flow](../domain/dose-quantity-stepping-flow.md), which
  still names SimpleSelect, ClickCountingButton and Prescribe.
- Update G3 in [the grouping index](ux-issue-grouping.md). It still describes the five Stepper
  buttons and the `first` and `last` of `createStepper`, which no longer exist after Step 3.

## Verification

- **Each step**:
  - `dotnet run build`;
  - compile with Fable and read the JSX output for `QuantityField`, `Order` and `Nutrition`,
    checking the nesting and whether the icon imports are hoisted;
  - `dotnet run servertests`, with `CI=true` in a worktree;
  - `dotnet fsi scripts/CheckDependencyRule.fsx`.
- **In the browser, behavior against master**: take a paracetamol oral solution order and a
  parenteral nutrition order through the same clicks on master and on the branch. The requests
  sent (network tab) and the values shown after each answer must be the same, except for the
  median button.
- **In the browser, the render invariants** for Steps 2 and 3:
  1. The five slot rectangles and the row height are the same in Selectable, Navigable and
     Stepable, and while loading.
  2. Going from Navigable to Stepable changes only the icons and tooltips.
  3. A disabled button keeps its slot and shows its tooltip on hover.
  4. Five rapid clicks on a Stepable dose quantity update the shown value five times, send one
     command with n = 5, and the answer replaces the predicted value.
  5. An answer with the same value but a new revision clears the predicted value, for example
     a step at the dose limit.
  6. Holding the small plus of a Stepable dose quantity steps the predicted value every 150 ms.
     Releasing it sends one command with the count of all held clicks, 700 ms after the last
     one, as on master.
