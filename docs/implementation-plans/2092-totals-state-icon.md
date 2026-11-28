# Implementation plan for issue 2092

## Problem description

The totals table colours a total orange when it lies outside the norm of its Totals row, but does not say whether the
total is too low or too high. Issue #2092 asks for a small icon per row with the state of the total: within the norm,
too low or too high, and no state for a total without a norm.

The client cannot tell too low from too high today. `Totals.isWithin` in `src/Informedica.GenORDER.Lib/Totals.fs`
checks the lower and the upper bound of a total separately, but returns one boolean, and `Totals.toBlock` folds the
checks of the two norms into a `Valid` or a `Warning` text block. Each of the 17 totals reaches the client as a
`TextBlock option` in `Totals`, both in GenORDER (`src/Informedica.GenORDER.Lib/Types.fs`) and in the shared contract
(`src/Informedica.GenPRES.Shared/Types.fs`).

## Approaches considered

1. A state beside the text: each total becomes a record of its text block and a state union.
2. A separate record of states: the 17 text fields stay, and `Totals` gets one more field with the state per
   substance.
3. The direction in the severity of the block: `Caution` for too low, `Warning` for too high and so on.

## Chosen approach

Approach 1, chosen by the maintainer. Each total carries its text and its state together, so the two cannot drift
apart, and the cases of `TextBlock` keep the meaning they have everywhere else.

The state is a union with five cases:

- `NoNorm`: the Totals row of the total has no minimum and no maximum, per kg or per time
- `WithinNorm`: the total lies within every bound of its row
- `BelowNorm`: part of the total lies below a minimum, and no part lies above a maximum
- `AboveNorm`: part of the total lies above a maximum, and no part lies below a minimum
- `OutsideNorm`: part of the total lies below a minimum and part above a maximum, on one norm or across the norm per
  kg and the norm per time

A total is checked against two norms, the norm per kg per time (`MinPerTimeAdj`, `MaxPerTimeAdj`) and the norm per
time (`MinPerTime`, `MaxPerTime`). The state of the total combines the two: a norm without bounds adds nothing, too
low on one norm and too high on the other is `OutsideNorm`.

The text block keeps its severity as today: `Valid` for `NoNorm` and `WithinNorm`, `Warning` for the other three. The
colour of a total therefore does not change.

GenPRES is in alpha and has no stored data to migrate, so the Dto of the totals changes shape without compatibility
code.

## Confidence

High. A prototype of all steps (a local worktree, not committed) builds, passes every affected test project, and
shows the icons as the maintainer wants them. It changes about 430 source lines, so the work is split into four
steps of under 200 lines each.

## Steps

Steps 1 to 3 change library code and each migrates a script with its tests. Nothing on the screen changes until
step 4.

### Step 1: the state of a total against its norm, in GenORDER

A migration of `src/Informedica.GenORDER.Lib/Scripts/TotalState.fsx`. The type of `Totals` does not change yet.

- `src/Informedica.GenORDER.Lib/Types.fs`: the union `TotalState` with the five cases.
- `src/Informedica.GenORDER.Lib/Totals.fs`:
  - `isWithin` is replaced by `normState`, which returns the `TotalState` of a total against one norm;
  - `combineStates` combines the states of the norm per kg and the norm per time;
  - `toBlock` gives the `Valid` block for `NoNorm` and `WithinNorm` and the `Warning` block for the other three, as
    it does today with `isWithin`.
- `tests/Informedica.GenORDER.Tests/TotalsSeverityTests.fs`: the `isWithin` tests become `normState` tests with the
  expected state, with a new case below and above at once; new tests of `combineStates`.

About 90 changed source lines.

### Step 2: the totals of GenORDER carry their state

A migration of a script that adds the record and the Dto, with their tests.

- `src/Informedica.GenORDER.Lib/Types.fs`: the record `IntakeTotal` with the fields `Text` (`TextBlock`) and `State`
  (`TotalState`); the 17 fields of `Totals` become `IntakeTotal option`. The name `Total` is taken in GenORDER by the
  order variable of a dose total. `DtoError` gets the case `UnknownTotalState`.
- `src/Informedica.GenORDER.Lib/Totals.fs`: `toBlock` is replaced by `toTotal`, which returns the `IntakeTotal`;
  `Totals.Dto` stores each total as its text block Dto and its state as a string, and `fromDto` reports an unknown
  state as `UnknownTotalState`.
- `src/Informedica.GenPRES.Server/ServerApi.Mappers.OrderContext.fs`: the shared contract has no state yet, so
  `totalsBack` passes the text block of each total to the client and leaves the state out, and `totals` sets the
  state from the severity of the block: `WithinNorm` for a `Valid` block and `OutsideNorm` for any other. This
  stop-gap goes in step 3; it carries the issue number. Until then no screen shows the state. The text of a
  `DtoError` gets a message for `UnknownTotalState`.
- Tests:
  - `TotalsSeverityTests.fs`: the state that `getTotals` returns for a total below, above, within and outside its
    norm and without a norm; the Dto round-trips every state and refuses an unknown state;
  - `TotalsGolden.fs`: the recorded texts are read from `IntakeTotal.Text`; the reference rows have no bounds, so
    every recorded total is `NoNorm` and its text does not change;
  - the fixtures that build totals in `tests/Informedica.GenORDER.Tests/Tests.fs` and `OrderPlanTests.fs`, and the
    GenORDER fixtures in `tests/Informedica.GenPRES.Server.Tests/MappersOrderContextTests.fs` and
    `MappersOrderPlanTests.fs`.

About 170 changed source lines.

### Step 3: the state reaches the client

A migration of a script for the shared type and the mappers, with their tests.

- `src/Informedica.GenPRES.Shared/Types.fs`: the union `TotalState` and the record `IntakeTotal` for the client,
  with the shared `TextBlock`; the 17 fields of `Totals` become `IntakeTotal option`. `Models.Totals.empty` keeps
  every field `None`.
- `src/Informedica.GenPRES.Server/ServerApi.Mappers.OrderContext.fs`: `totals` and `totalsBack` map the state of
  each total both ways; the stop-gap of step 2 goes.
- `src/Informedica.GenPRES.Client.Core/Models/TotalsDisplay.fs`: `substanceToBlock` becomes `substanceToTotal` and
  returns the `IntakeTotal` of a row; `substanceToField` and `substanceSeverity` read its text block, so
  `TotalsChangePolicy`, `Views/Totals.fs` and `Views/ParenteralPrint.fs` keep working unchanged; the new
  `substanceState` returns the state of a row, `None` for a total that is not there.
- Tests:
  - `tests/Informedica.GenPRES.Server.Tests/MappersOrderContextTests.fs`, `MappersOrderPlanTests.fs` and
    `OrderContextSwitchTests.fs`: the mappers carry the state both ways;
  - `tests/Informedica.GenPRES.Client.Core.Tests`: `TotalsDisplayTests` gets tests of the state of a row; the
    fixtures in `ClientTests`, `TotalsChangePolicyTests`, `PlanContextPolicyTests` and `PlanDifferencePolicyTests`
    build `IntakeTotal`.

About 130 changed source lines.

### Step 4: the icon in the totals table

Client UI code only, without a script.

- `src/Informedica.GenPRES.Client/Components/TotalStateIcon.fs`, a new component: the icon of a state, as the
  maintainer approved it in the prototype: a check in the success colour for `WithinNorm`, a down arrow for
  `BelowNorm`, an up arrow for `AboveNorm` and a two-way vertical arrow for `OutsideNorm`, those three in the warning
  colour; no icon for `NoNorm` or for a total that is not there.
- `src/Informedica.GenPRES.Client/Views/Totals.fs`: each row shows the icon beside the value of its total, in a cell
  of a fixed narrow width without side padding, so that the value and the norm keep the room of the row.
- `src/Informedica.GenPRES.Client/Views/ParenteralPrint.fs`: the printed totals show the same icon.
- The label of each icon, for a screen reader and on hover, as terms in `Terms.fs` and `localization.tsv`.

About 70 changed source lines. No test project covers the Fable views, so this step is checked by hand: a total
below, above, within and outside its norm, and a total without a norm, on the screen and in the print view.
