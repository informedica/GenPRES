# Implementation plan for issue #1102

One five-slot QuantityField for every order variable in the order and nutrition views. It is a
sub-issue of [the foundation plan](981-ux-foundation-and-common-components.md), whose component
C1 it replaces.

- [Problem description](#problem-description)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)

## Problem description

The spec *quantity-field-state-transitions*, linked from #1102, gives QuantityField:

- four modes: Selectable, Navigable, Stepable and Fixed;
- three activity states that apply in any mode;
- ten mode transitions, T1 to T10;
- six render invariants.

Its central invariant is that the value and its buttons never move when the mode changes. A
dropdown that resolves to a single value that can be stepped keeps its place on screen.

The current `QuantityField` puts `Stepper` beside the select. Changing the mode adds or removes
buttons, so the row reflows. Each call site also decides for itself which buttons to offer, and
the two views differ: Order.fs steps frequency and Nutrition.fs does not.

Every order variable in `Views/Order.fs` (15 fields) and `Views/Nutrition.fs` (8 fields) already
goes through one wrapper, `ViewHelpers.orderField`. The change can therefore be made in one place
and then applied to all 23 fields.

## Approaches considered

- **Adjust the current Stepper.** Keep the Stepper beside the select and hide buttons per mode.
  Rejected: the Stepper is a button group whose width changes with its buttons, and it also has
  a median button that the spec does not have.
- **Five fixed slots with the mode as a type** (the draft attached to #1102). A grid of four
  button slots around the value. An unused button slot is hidden with `visibility: hidden`, so
  it keeps its size. `Mode = Selectable | Navigable of Steps | Stepable of Steps | Fixed`
  replaces `steps: Steps option`. Chosen.
- **Decide the mode in the view.** Rejected: the rule would then be neither tested nor shared.
  It becomes a pure function in Client.Core instead.

## Chosen approach

The five-slot component, with one rule that decides the mode for every field.

Decisions taken in review:

- **Decrease and increase in Navigable** send the existing `Decrease` and `Increase (n, false)`
  commands. On a range the server starts from the max (decrease) or the min (increase) and steps
  from there (`OrderVariable.step` in GenORDER). No server change.
- **The mode rule** is a pure function in Client.Core. It is prototyped in a script with Expecto
  tests and then migrated, because Client.Core has no exception to the script-only policy.
- **Median** is removed from the client only. The four `SetMedian*` cases in the shared
  contract, the server mapper and GenORDER stay in place until a follow-up issue removes them.
- **Frequency** follows T6 and T7: it is Stepable once solved, in both views.
  - Its Increase and Decrease commands carry no click count, so it does not debounce: each
    click sends one command.
  - It has no large step, so in Stepable its first and last buttons are `None`.
- **Tooltip translation** stays out of scope. The draft's Dutch tooltips stay as they are, and
  moving them to `Terms` belongs to the follow-up issue, because new keys change the shared
  localization.

### The mode rule

A discriminated union without function fields, so that it has structural equality and can be
tested:

```fsharp
[<RequireQualifiedAccess>]
type QuantityMode =
    | Selectable
    | Navigable
    | Stepable
    | Fixed

/// canStep: the server has step commands for this variable and the order allows them now.
/// allSolved: every variable the order depends on holds one value.
let decide (canStep: bool) (allSolved: bool) (ovar: OrderVariable) : QuantityMode

/// Dose quantity can be stepped only when the orderable quantity of every component holds one value.
let doseQuantityCanStep (ord: Order) : bool
```

| Values in `Variable.Vals` | Condition | Mode |
|---|---|---|
| more than 1 | — | Selectable |
| exactly 1 | `canStep && allSolved && DefinedConstraints.Incr.IsSome` | Stepable |
| exactly 1 | otherwise | Fixed |
| 0 | `canStep && OrderVariable.isNavigable` (a max and a defined increment) | Navigable |
| 0 | `canStep`, and a min or a max the user cannot move | Fixed |
| 0 | otherwise: no min and no max, or not `canStep` | Selectable |

The mode follows the server's answer, not the user's action. A clear sends the variable back
unrestricted, and the solver narrows it again from the rest of the order. What comes back
decides the mode:

- a range with a max and a defined increment gives Navigable. An example is a dose quantity
  whose component quantities stay solved. This is T8, "a clear on a dependent re-opens the
  range".
- nothing, neither values nor a min or a max, gives the empty Selectable of T10.

`decide` is the only place where a mode is chosen. The call site computes `canStep`:

| Variable | `canStep` |
|---|---|
| component orderable quantity, dose rate, frequency | `true` |
| dose quantity | `doseQuantityCanStep ord`: today's `showNav` check, moved out of `createDoseQtyStepper` |
| every other variable | `false` |

`canIncr` is not part of `canStep`. It holds when the order has a single component or a dose
count above 1. Today it only turns off increase and last, while decrease and first stay
available. It remains a condition on those two buttons: they are `None`, are drawn disabled and
keep their slots. This matches the spec's note on Stepable: the server, not the field, decides
whether a button is offered.

The rule builds on `Models.Order.OrderVariable.isSolved` and `isNavigable`, and uses
`Models.Order.isSolved` for `allSolved`, all in `Shared/Models.fs`.

The unused active pattern `(|NonNavigable|Navigable|Selectable|Stepable|)` in the same file is
left alone here. Its Stepable ignores whether the dependent variables are solved. It is removed
in the follow-up issue.

Frequency fits the Stepable row. GenORDER gives every frequency a defined increment of 1, and
frequency increase and decrease step by one increment.

## Confidence

High for the component and the rule. They follow the spec and reuse existing commands and
helpers.

Medium for the visible changes in Step 2a, which need a check in the browser:

- decrease and increase offered on a navigable range;
- frequency no longer debounced.

## Steps

One PR at a time; each waits for the previous one to be merged.

### Step 1: the mode rule in a script

- New `src/Informedica.GenPRES.Client.Core/Scripts/QuantityMode.fsx`. It loads `load.fsx` and
  holds `QuantityMode`, `decide` and `doseQuantityCanStep`.
- Expecto tests in the same script, one per transition in the spec:
  - T1 Navigable→Selectable, T2 Selectable→Stepable, T3 Navigable→Stepable,
    T4 Navigable→Fixed, T5 Navigable→Navigable;
  - T6 Selectable→Fixed, T7 Fixed→Stepable, T8 Stepable→Navigable or Selectable,
    T10 clear→Selectable;
  - two tests for a clear. A cleared dose quantity whose max and defined increment come back,
    with every component still solved, gives Navigable (T8). A cleared field with no values and
    no min or max gives Selectable (T10).

  T9 does not change the mode; it is checked in the browser.
- Tests for `canStep` on dose quantity:
  - The order: a dose quantity with one value and a defined increment, every dependent solved,
    but one component's orderable quantity still a range. Expected: `doseQuantityCanStep` is
    false, and `decide` gives Fixed, not Stepable.
  - The same order with every component solved. Expected: Stepable.
- Fixtures are `OrderVariable` and `Order` builders over the shared types.
- Migration, done by the maintainer:
  - `src/Informedica.GenPRES.Client.Core/QuantityMode.fs`, added to the project and to
    `Scripts/load.fsx`;
  - `tests/Informedica.GenPRES.Client.Core.Tests/QuantityModeTests.fs`, added to the test
    project before `Main.fs`.

  Step 2a starts after this migration is merged, because it uses `QuantityMode`.

### Step 2a: the new component and wrapper

The draft alone is 407 lines, so the client work is split into two PRs from the start.

1. `Components/QuantityField.fs`: replace it with the draft, adapted to the repository rules:
   - interpolation with format specifiers (`$"%i{slotWidth}px ..."`);
   - lines under 120 characters;
   - no `private` on the pure helpers `rowSx`, `slotSx` and `slot`. The hook `useClicker`
     keeps `private`.
2. `Views/ViewHelpers.fs`:
   - `createStepper` becomes `createSteps : QuantityMode -> ... -> Steps`, with no `setMed`, no
     `median` and no option.
     - Navigable: first sends SetMin, decrease and increase send `(n, false)`, last sends SetMax.
     - Stepable: first and last send `(n, true)`, decrease and increase send `(n, false)`. For
       frequency, first and last are `None`.
     - `useDebounce = (mode = Stepable) && hasCount`, where `hasCount` is false for frequency.
   - `createDoseQtyStepper` becomes `createDoseQtySteps`.
     - It loses median and the `showNav` early return; that check becomes
       `doseQuantityCanStep`.
     - It keeps `canIncr` and the checks that stop a step at the dose limit, as `None` on the
       buttons they affect.
   - `orderField`, `orderSelect`, `orderFixed` and `ovarDisplay` take
     `mode: QuantityField.Mode` in place of `stepper`.
   - The check for an empty field changes.
     - The new check: a field is empty when `xs` is empty and the mode is Selectable or Fixed.
       An empty field is hidden, or drawn disabled when `alwaysShow` is set.
     - Today's check is `xs` empty and stepper `None`.
     - Without the change, the row "0 values, not `canStep`, Selectable" would render an
       empty dropdown.
     - A range field is never empty: `ovarValsWithRange` puts a `"range"` entry into `xs`, so a
       Fixed field over a range shows that entry.
3. The call sites keep today's decisions. A temporary `ViewHelpers.modeOf navigable solved` maps
   them onto `QuantityMode`:
   - navigable gives Navigable;
   - solved gives Stepable;
   - anything else gives Selectable.

   The mode then goes through `createSteps` into `QuantityField.Mode`.
4. The visible changes in this PR are only these:
   - the new layout;
   - no median button;
   - decrease and increase on a navigable range;
   - frequency no longer debounced. Today frequency debounces and drops the click count, so
     rapid clicks are lost.

   After this PR the median messages are no longer used.
5. Changelog entry.

### Step 2b: the 23 fields switch to `decide`

1. `Views/Order.fs`: every field builds its mode with
   `QuantityMode.decide canStep (ord |> Order.isSolved) ovar`, taking `canStep` from the table
   above.
   - `ViewHelpers.toFieldMode : QuantityMode -> (unit -> Steps) -> QuantityField.Mode` builds
     steps only for Navigable and Stepable.
   - Names and the other variables that cannot be stepped get Selectable or Fixed.
   - Frequency no longer hard-codes `navigable = false`.
2. `Views/Nutrition.fs`: the same. `frequencyControl` gains steps, as in Order.fs.
3. Remove `modeOf`.
4. The activity states do not change.
   - `rests` sets `disabled`.
   - `isOptimisticStep` suppresses the loading state.
   - The revision counters feed `Steps.revision`.
5. Changelog entry.

### Step 3: remove median and the old stepper

- Remove the median messages, `update` branches, `msgToField` cases and prop fields from
  `Order.fs` and `Nutrition.fs`.
- Remove the median callbacks from `Prescribe.fs` and `OrderPlan.fs`.
- Delete `Components/Stepper.fs` and `Components/ClickCountingButton.fs`, and their project
  entries.
- Changelog entry.
- File the follow-up issue. It covers three things:
  - remove `SetMedian*` from the contract, the server and GenORDER;
  - remove the unused active pattern in `Shared/Models.fs`;
  - translate the QuantityField tooltips through `Terms`.

### Step 4: docs

- Add the modes, transitions and invariants from the spec to the C1 section of
  [the foundation plan](981-ux-foundation-and-common-components.md).
- Update [the dose quantity stepping flow](../domain/dose-quantity-stepping-flow.md), which
  still names SimpleSelect, ClickCountingButton and Prescribe.
- Note under G3 (#405) in [the grouping index](ux-issue-grouping.md) that the mode now decides
  what first and last do: go to the min or max, or make a large step.

## Verification

- **Step 1**: run `dotnet fsi QuantityMode.fsx` from the script directory, or load it through
  the FSI MCP server. All transition tests pass.
- **Steps 2a, 2b and 3**:
  - `dotnet run build`;
  - compile with Fable and read the JSX output for `QuantityField`, `Order` and `Nutrition`,
    checking the nesting and whether the icon imports are hoisted;
  - `dotnet run servertests`, with `CI=true` in a worktree;
  - `dotnet fsi scripts/CheckDependencyRule.fsx`.
- **In the browser**, with a paracetamol oral solution order and a parenteral nutrition order,
  check the render invariants:
  1. The five slot rectangles and the row height are the same in Selectable, Navigable,
     Stepable and Fixed, and while loading.
  2. Going from Navigable to Stepable changes only the icons and tooltips.
  3. A disabled button keeps its slot and shows its tooltip on hover.
  4. Five rapid clicks on a Stepable dose quantity update the shown value five times and send
     one command with n = 5. Five rapid clicks on a Stepable frequency send five commands,
     one per click, with no count.
  5. An answer with the same value but a new revision clears the predicted value, for example
     a step at the dose limit.
  6. The dose quantity run from the spec (range, 5;6 mL, 6 mL, 6,1 mL, then clear) passes
     through T1, T2, T9 and T10.
