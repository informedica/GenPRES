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
- [As built](#as-built)

## Problem description

G3 groups #397, #402, #405, #496 and #978 under #984. Since the index was written, two pull
requests and the work under issue #1102 changed the dialog:

- #1016 gave every marked field a severity icon with the crossed bound on hover (#402).
- The pull requests of #1102 gave the quantity field five fixed slots and showed on the outer
  buttons which meaning they carry (#405).
- #1125 put the field mode under one rule, `QuantityMode` in `Client.Core`, and left out the
  outer slots of a value whose large step equals its small one.

What is left of the group is settled here, and one thing is added that the issues did not name:
the preparation section stays open with all its fields once every preparation value is solved,
and takes room from the dosing and administration sections the user is working in.

## Decisions

Taken 2026-09-28 by the maintainer.

| Question | Decision |
|---|---|
| The order of the fields (#397, #978) | The order stays as it is in the client: `keer dosis` before `dosering`, since the dose is calculated from the dose per administration and the frequency. #397 closes with that reply. #978, the server sending the field list and the lead field, leaves G3 and stays open on its own. |
| Severity (#402) | Fixed by #1016: the reason on hover reads the crossed bound in the value's unit, `max 15 mg` or `min 2 mg` for a bound that is itself allowed and `< 15 mg` or `> 2 mg` for one that is not, and the icon alone where the bounds do not explain the mark. The wording stands. Close. |
| The outer buttons (#405) | Fixed by the pull requests of #1102 and by #1125: jump icons while the value can be navigated, the large step as text (`−5` / `+5`) once it can be stepped, and the outer slots left out when the large step is the small one. Halve and double are not planned. Close. |
| Which field moves the dose (#496) | The lead marker was part of the server's field list, so #496 leaves G3 together with #978. |
| How a value is stepped | Stays as it is. The small step is the defined increment, which the dose rule gives. The large step is the calculated increment, which is the grid the order narrowed to: for a discontinuous order in volume units the pipeline widens the quantity and rate increments through 0.1, 0.5, 1, 5, 10 and 20 ml until at most ten values remain, and the large step follows that grid. Two exceptions stay too: a quantity whose calculated increment is exactly 0.1 ml and a rate whose calculated increment is exactly 0.1 ml/h step ten times it (`Quantity.stepQuantity`, `Dose.stepRate`). The client shows the large step the server sends as `LargeIncr`; the server derives it from the same rule and the same two exceptions in `mapLargeIncr`, which is domain work in the server and moves to GenORDER under #1129. |
| The preparation section | Folds to its heading when every preparation variable it shows holds one value. The user can open and fold it; after every server answer it follows the solved state again. The order view only, not the nutrition view. |

## Approaches considered

For the large step, considered and set aside:

- **Step by the defined increment, ten times it for the large step.** Rejected: the calculated
  increment is the grid the solver narrowed the order to, and a large step off that grid is not
  snapped back. The change-property pipeline applies the step and re-solves the min and max
  only; the value is either accepted or the order comes back as it was. The calculated increment
  already gives the right large step.

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

One change of code, this plan before it and the closing documentation after, each its own pull
request, one at a time.

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
- The G3 section of the grouping index: the status of each member, the addition, the two
  issues that left the group, and the components used, `Disclosure` and `ValueChip` among them.
- [The dose quantity stepping flow](../domain/dose-quantity-stepping-flow.md) says in three
  places that a step is snapped by `pickNearestHigherElseLower`. It is not: the change-property
  pipeline applies the step and runs `calcMinMax`; the snap runs for component quantities in the
  solve pipeline alone. The document is corrected.
- #397 closes with the decision, #402 and #405 with the pull requests that fixed them; #984's
  member list is corrected and the issue closed; #978 names #496 as its member.

## Confidence

High: the rule is small and tested, and the component exists.

## Steps

One pull request at a time, each merged before the next. Code outside the client is prototyped
in a script and migrated by the maintainer; client code is committed locally and pushed after the
maintainer has checked it in the browser. `scripts/CheckDependencyRule.fsx` runs after the code
pull request.

1. **This plan** (docs).
2. **The preparation section folds when solved.** A script in
   `src/Informedica.GenPRES.Client.Core/Scripts/` with `SectionFold` and its tests: open when
   not solved, folded when solved, the toggle flips and holds, `observe` with the same state
   keeps the toggle and with a changed state drops it, an empty section counts as solved. Then
   the view, where the effect also starts the fold anew when the order's id changes. About 40
   source lines in `Client.Core`, about 80 in `Views/Order.fs`.
3. **Docs and issues**, as above.

## Verification

- Step 2: `dotnet run build`; the script's tests, then `dotnet run servertests`, with `CI=true`
  in a worktree; `dotnet fsi scripts/CheckDependencyRule.fsx`; the Fable compile and a reading
  of the generated `Order` for the nesting and the hoisted icon imports, then `npx vite build`.
  In the browser, on a multi-component order such as a reconstituted antibiotic: the
  preparation heading shows exactly when a preparation field shows; the section is open while
  any preparation variable has more than one value; it folds with its value chips once all hold
  one; opened by hand, it stays open through a step that keeps it solved; clearing a
  preparation value reopens it; opening another order from the plan starts the fold anew; the
  dosing and administration sections are unchanged.
- Docs: `npx markdownlint-cli2` on the touched files.

## As built

| Step | Pull request | Note |
|---|---|---|
| 1, the plan | #1128 | Two review rounds: a stepped value is re-solved, not snapped, and the tenfold exceptions of the stepping are named. The server's copy of the stepping rule became #1129. |
| 2, the fold | #1131 | `SectionFold` in `Client.Core` with fourteen tests, the view in `Order.fs`, two icons in `MUI.fs`. |
| 3, the closing docs | #1132 | The grouping index, this table, the stepping flow document; #397, #402 and #405 closed, #978 with #496 out of the group. |

Deviations from the plan, both on the maintainer's check in the browser:

- The folded section shows its heading as the other sections show theirs, a line with the name,
  with a button beside the name that opens and folds it. `Disclosure` and the value chips were
  built and taken out again: the accordion did not look like the dosing and administration
  sections beside it.
- The five fields stand as plain siblings in the dialog while the section is open, and are not
  rendered while it is folded.

From the review of #1131: the list of preparation variables tests what each field shows, so a
component quantity holding only a range keeps its heading and field; the fold button is named
after its section for a screen reader.
