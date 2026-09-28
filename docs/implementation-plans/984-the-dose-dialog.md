# Implementation plan for issue #984

Closing G3, the dose dialog, of [the grouping index](ux-issue-grouping.md#g3--the-dose-dialog-984):
which fields the dialog shows, how a value is stepped, how a value outside the rules is marked,
and how the preparation section behaves once it is solved.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)

## Problem description

G3 groups #397, #402, #405, #496 and #978 under #984. Since the index was written, two pull
requests and the work under issue #1102 changed the dialog:

- #1016 gave every marked field a severity icon with the crossed bound on hover (#402).
- The pull requests of #1102 gave the quantity field five fixed slots and showed on the outer
  buttons which meaning they carry (#405).
- #1125 put the field mode under one rule, `QuantityMode` in `Client.Core`, and left out the
  outer slots of a value whose large step equals its small one.

What is left of the group is settled here, and two things are added that the issues did not
name. First, the small step is the *defined* increment, which the dose rule gives, but the large
step is the *calculated* increment, which the solver derives from the order, multiplied by ten in
two special cases; the button shows the step, but no rule a clinician could know says what it
will be. Second, the preparation
section stays open with all its fields once every preparation value is solved, and takes room
from the dosing and administration sections the user is working in.

## Decisions

Taken 2026-09-28 by the maintainer.

| Question | Decision |
|---|---|
| The order of the fields (#397, #978) | The order stays as it is in the client: `keer dosis` before `dosering`, since the dose is calculated from the dose per administration and the frequency. #397 closes with that reply. #978, the server sending the field list and the lead field, leaves G3 and stays open on its own. |
| Severity (#402) | Fixed by #1016: the reason on hover reads the crossed bound in the value's unit, `max 15 mg` or `min 2 mg` for a bound that is itself allowed and `< 15 mg` or `> 2 mg` for one that is not, and the icon alone where the bounds do not explain the mark. The wording stands. Close. |
| The outer buttons (#405) | Fixed by the pull requests of #1102 and by #1125: jump icons while the value can be navigated, the large step as text (`−5` / `+5`) once it can be stepped, and the outer slots left out when the large step is the small one. Halve and double are not planned. Close. |
| Which field moves the dose (#496) | The lead marker was part of the server's field list, so #496 leaves G3 together with #978. |
| Which constraints step a value | Only the defined constraints. The small step is the defined increment; the large step is ten times the defined increment. Nothing steps by the calculated constraints any more. |
| The preparation section | Folds to its heading when every preparation variable it shows holds one value. The user can open and fold it; after every server answer it follows the solved state again. The order view only, not the nutrition view. |

## Approaches considered

For the large step:

- **Compose it in the client**: send a count of ten with the flag cleared. Rejected: it
  bypasses `OrderVariable.step`, the one place that steps a value, and puts a domain rule, the
  factor, in the client.
- **Change the rule in GenORDER**: `OrderVariable.step` uses the defined increment for both
  steps and multiplies it by a factor for the large one; the server sends the large increment it
  will step by; the client keeps rendering what it receives. Chosen.
- **Keep the calculated increment, fix its special cases**: the two places that multiply the
  count by ten for a calculated increment of exactly 0.1 ml or 0.1 ml/h would become a general
  rule. Rejected: the decision is that the calculated constraints do not step a value at all.

For the preparation section:

- **Fold on a rule in the view**: a React state in `Views/Order.fs` that starts folded when the
  variables are solved. Rejected: the open-or-folded rule has three inputs (solved, the user's
  toggle, a new answer) and belongs in a tested module.
- **A pure fold rule in `Client.Core`, rendered with `Disclosure`**: the rule decides open or
  folded from the solved state and the user's last toggle; the existing `Components/Disclosure`
  renders it, as the patient panel and the nutrition contexts do. Chosen.
- **Fold every section**: dosing and administration too. Rejected: those hold the fields the
  user is changing; folding them would take the controls away.

## Chosen approach

Two changes of code, this plan before them and the closing documentation after, each its own
pull request, one at a time.

### The large step from the defined increment

**Today.** A step button sends `Increase…Property` or `Decrease…Property` with a count and a
flag (`Shared/Api.fs`); the inner buttons send the flag false, the outer ones true
(`ViewHelpers.createStepper`, `createDoseQtyStepper`). On the server `OrderVariable.step`
(`src/Informedica.GenORDER.Lib/OrderVariable.fs`) picks `CalculatedConstraints.Incr` when the
flag is set and `DefinedConstraints.Incr` otherwise, and steps the count of increments from the
value. Two special cases multiply the count by ten when the calculated increment is exactly
0.1 ml (`Quantity.stepQuantity`, same file) or 0.1 ml/h (`Dose.stepRate`, `Order.fs`). The
server mirrors those for the button text as `LargeIncr`
(`ServerApi.Mappers.Order.fs`, `mapLargeIncr` with the two 0.1 increments).
`isWithinConstraints` reads the same flag for a different purpose, the level of a value, and
does not change.

The calculated increment is not arbitrary. For a discontinuous order in volume units the
pipeline step `increaseIncrements` (`Order.fs`, run with a limit of ten values) widens the
orderable quantity and rate increments through 0.1, 0.5, 1, 5, 10 and 20 ml until at most ten
values remain, and the calculated increment is the grid the order narrowed to. Today's large
step moves along that grid. After the change a large step of ten times 0.1 ml on a variable
whose grid is 5 ml sets a value off the grid as the variable's one value. Nothing snaps it back:
the change-property pipeline applies the step and re-solves the min and max (`calcMinMax`), and
`pickNearestHigherElseLower` runs only for component quantities in the solve pipeline. Either
the solver accepts the value and the other variables follow it, or the solve fails and the
order stays as it was before the step, which the client shows as the value coming back. The
small step, ten times smaller, has had this since it stepped by the defined increment; the
change makes the two steps agree. A continuous order is not widened, so the 0.1 ml/h rate, the
common case, steps as it does today.

**The rule.** In GenORDER:

- `OrderVariable.largeStepFactor`, a documented domain constant of ten, beside `step`.
- `OrderVariable.step`: the increment is always the defined one; for the large step it is
  multiplied by the factor. The start rules at the min and the max and the non-zero floor stay
  as they are. A variable without a defined increment does not step, as now.
- `Quantity.stepQuantity` and `Dose.stepRate` lose their special cases and pass the flag
  through, so every stepped variable follows the one rule.
- `OrderVariable.largeIncrement`: the defined increment times the factor, the one place the
  step the server will make is read.
- A defined increment holds one value. `Increment.create` allows several, and `step` would
  turn a two-valued increment into a set of values, for the small step today as much as for the
  large step. The rule takes the single value as given; the script asserts it for every
  increment `Medication.fs` sets.
- `OrderVariable.Dto` gains `LargeIncrOpt`, filled by `Dto.toDto` from `largeIncrement`, so the
  factor lives in GenORDER alone and the server only copies the value.

**The server.** `mapLargeIncr` copies `dto.LargeIncrOpt` in place of the calculated increment
and the coarse multiplication; the three mappers per kind of variable and the two 0.1
increments collapse into `mapToOrderVariable`. Every variable with a defined increment then
carries a `LargeIncr`, those the client never steps large included; the client offers the large
step only where it has a command for it. A test in `Server.Tests` reads the mapped `LargeIncr`
of a variable with a defined increment, and None of one without.

**The client.** No change of code but the comments: `LargeIncr` is still the text on the outer
buttons, and the outer buttons still send the flag. On screen the outer buttons now appear on
every stepable dose quantity, dose rate and component quantity with a defined increment, in the
order view and the nutrition view alike, where today they are left out when the calculated
increment equals the defined one; frequency keeps them out in both views, since its stepper
names no large step. Comments in `ViewHelpers.fs` and `QuantityField.fs` that call the large
step the server's calculated increment say what it is now, and the `LargeIncr` field in
`Shared/Types.fs`, whose comment says only a variable whose outer step differs emits one, gets a
`///` comment that says what it carries.

**Left to the maintainer.** The flag is named `useCalc` in GenORDER, `Shared.Api`, the server
mappers and both client message types, and after this change it means "large step". Renaming
it is a refactor across four rings and the contract; the wire shape, a bool in the same
position, does not change. Recommended as its own pull request after this one.

### The preparation section folds when solved

**The rule**, a new module `SectionFold` in `Client.Core`, after `QuantityMode` in the project:

```fsharp
type Fold =
    {
        /// Whether every variable the section shows held one value at the last answer.
        Solved: bool
        /// The user's last toggle since the solved state last changed; None follows the rule.
        Override: bool option
    }

/// Every variable holds one value; an empty section counts as solved.
let allSolved (ovars: OrderVariable seq) = ovars |> Seq.forall Order.OrderVariable.isSolved
let initial solved = { Solved = solved; Override = None }
/// An answer: a change of the solved state drops the user's toggle; no change keeps it.
let observe solved fold = if solved = fold.Solved then fold else initial solved
let isOpen fold = fold.Override |> Option.defaultValue (not fold.Solved)
let toggle fold = { fold with Override = Some (not (isOpen fold)) }
```

**The view**, `Views/Order.fs`:

- The predicate that decides whether the preparation heading shows becomes the list of the
  preparation variables the five fields will show, under the conditions the fields already
  apply: the component's orderable quantity, the item's component concentration when its
  defined values are more than one, the item's orderable quantity for a continuous order or its
  orderable concentration otherwise, and the total orderable quantity. The heading shows when
  the list is not empty; the section is solved when `SectionFold.allSolved` says so. Today the
  heading tests the concentration's values where the field tests its defined values; the list
  follows the field, so the heading shows exactly when a preparation field shows. Two things
  make that exact: the concentration field has a second branch, the component's item of the
  component's own name when the selected item is not found, and the list follows both; and a
  field without values and without steps renders nothing, so a variable enters the list only
  when its field would show.
- The fold is component-local state, as the view already keeps which field is being changed: a
  React state holding the `Fold`, and an effect keyed on the order's id and the solved flag that
  applies `observe`, and `initial` when the id changed. Keyed on the flag, so an answer that
  keeps the section solved does not reopen it; keyed on the id, so the user's toggle does not
  carry over to another order shown by the same component.
- The five fields render inside `Components.Disclosure` with the fold's open state and toggle,
  and the mobile flag and padding the patient panel passes.
  The summary is the heading text and, when solved, one `ValueChip` per preparation variable
  with its value, unit and severity, so the folded section reads its result. The field order
  does not change; the heading and the five fields become one element in it.
- The dosing and administration sections stay plain headings.

### Documentation and the issues

- This plan gains its as-built table.
- The G3 section of the grouping index: the status of each member, the two additions, the two
  issues that left the group, and the components used, `Disclosure` and `ValueChip` among them.
- #397 closes with the decision, #402 and #405 with the pull requests that fixed them; #984's
  member list is corrected and the issue closed; #978 names #496 as its member.

## Confidence

High for the large step: the rule replaces two special cases with one, and every stepped
variable already goes through `OrderVariable.step`. A 0.1 ml quantity and a 0.1 ml/h rate whose
defined increment is 0.1 as well step the same as today; a variable whose calculated increment
differed from its defined one steps differently, which is the decision. What is given up is the
large step along the widened grid of a discontinuous order; the order-level test of step 2 shows
where such a step lands.

High for the fold: the rule is small and tested, and the component exists.

## Steps

One pull request at a time, each merged before the next. Code outside the client is prototyped
in a script and migrated by the maintainer; client code is committed locally and pushed after the
maintainer has checked it in the browser. `scripts/CheckDependencyRule.fsx` runs after every
code pull request.

1. **This plan** (docs).
2. **The large step from the defined increment.** A script in
   `src/Informedica.GenORDER.Lib/Scripts/` that shadows `OrderVariable` and its `Quantity` and
   `Dose` wrappers, with Expecto tests: a solved value with a defined increment of 1 mg steps
   to +1 mg small and +10 mg large; the calculated increment is ignored where it differs from
   the defined one; a 0.1 ml quantity and a 0.1 ml/h rate step 1 ml and 1 ml/h large, as
   today, and a 0.5 ml quantity 5 ml, where today it stepped 0.5 ml; no defined increment
   leaves the variable unchanged; `largeIncrement` is None without one; every defined increment
   `Medication.fs` sets holds one value; `Dto.toDto` fills `LargeIncrOpt`. One order-level test
   steps a discontinuous order whose quantity increment was widened through the pipeline and
   records where the value lands. The server mapper change is prototyped in
   `src/Informedica.GenPRES.Server/Scripts/` against it, with the mapper test of `Server.Tests`.
   The comments in the client, the `LargeIncr` comment in `Shared/Types.fs`, the `useCalc`
   bullet and the step node of the diagram in
   [the dose quantity stepping flow](../domain/dose-quantity-stepping-flow.md) and the icons
   bullet in [plan 1102](1102-one-quantity-field-for-every-order-variable.md) follow in the same
   pull request. The stepping flow document says in three places that a step is snapped by
   `pickNearestHigherElseLower`; it is not, and the same pull request corrects that. About 60
   source lines in GenORDER; about 40 removed in the server.
3. **The preparation section folds when solved.** A script in
   `src/Informedica.GenPRES.Client.Core/Scripts/` with `SectionFold` and its tests: open when
   not solved, folded when solved, the toggle flips and holds, `observe` with the same state
   keeps the toggle and with a changed state drops it, an empty section counts as solved. Then
   the view, where the effect also starts the fold anew when the order's id changes. About 40
   source lines in `Client.Core`, about 80 in `Views/Order.fs`.
4. **Docs and issues**, as above.

## Verification

- Each code pull request: `dotnet run build`; `dotnet run servertests`, with `CI=true` in a
  worktree; `dotnet fsi scripts/CheckDependencyRule.fsx`; for the client, the Fable compile
  and a reading of the generated `ViewHelpers`, `QuantityField` and `Order` for the nesting
  and the hoisted icon imports, then `npx vite build`.
- Step 2: the script's tests, then the GenORDER and server tests, the golden orders included.
  In the browser, on a paracetamol oral solution and a continuous morphine order: the outer
  buttons of a stepable dose quantity and dose rate read ten times the defined increment; one
  click sends the property command with a count of one and the flag set, and the value moves
  ten increments; a 0.1 ml/h rate still moves 1 ml/h; a dose quantity whose calculated increment
  equalled its defined one now shows its outer buttons; frequency shows no outer slots; a dose
  quantity of a multi-component orderable still saturates at the prepared quantity. On a
  paracetamol oral solution whose quantity increment the pipeline widened, a large step does what
  the order-level test says: the value is accepted or comes back. In the nutrition view a stepable component
  quantity and dose rate show their outer buttons as well, reading ten times the defined
  increment, and its frequency shows no outer slots.
- Step 3: the script's tests, then the `Client.Core` tests. In the browser, on a
  multi-component order such as a reconstituted antibiotic: the preparation heading shows exactly
  when a preparation field shows; the section is open while any preparation variable has more
  than one value; it folds with its value chips once all hold one; opened by hand, it stays open
  through a step that keeps it solved; clearing a preparation value reopens it; opening another
  order from the plan starts the fold anew; the dosing and administration sections are unchanged.
- Docs: `npx markdownlint-cli2` on the touched files.
