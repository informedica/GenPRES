# Implementation plan for issue #1034

Which fields offer the cross that clears them. Planned together with:

- [#1195](https://github.com/informedica/GenPRES/issues/1195): the cross is offered where clearing
  changes nothing;
- [#1193](https://github.com/informedica/GenPRES/issues/1193): a click on a held dropdown clears
  it and opens the list.

Follows #398, which #1031 closed by making the cross clear the field, and
[ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md): a control that is offered and does
nothing is removed, and the user never has to pick again what they already picked.

- [Problem description](#problem-description)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)

## Problem description

Three terms are used throughout:

- A **held** filter field is one whose only option is the one the user chose. The server computes
  each filter list with the field's own choice in force, so once a medication is chosen the
  medication list holds that medication alone.
- A **range field** is an order variable shown as one range, from its minimum to its maximum,
  with a stepper beside it; a click on the range picks the median. The code calls this mode
  `Navigable` (`QuantityModePolicy`).
- **Stepped** means changed with the stepper buttons beside a field.

Since #1031 pressing the cross clears one field and asks the server again. The recordings on the
issue #398 (alpha.38) show it still reads as a control that does nothing.

The cause: the cross does two different jobs.

1. **Reopening.** An order variable picked or stepped to one value, or a held filter field. Such
   a field is never left empty: the solver computes a value again, or the field chooses its one
   option again. What the user is after is the other choices, the list. Today that takes the
   cross, a wait, then the dropdown arrow, which is hidden while the cross shows. The arrow of an
   order field holding one value opens a list of that one value.
2. **Emptying.** The empty field is a state that stays and means something: the weight of an
   anonymous patient cleared, renal function back to none, a filter constraint dropped, the text
   of a search, a column filter, the manually added drugs on the interactions page.

For reopening the field fills again within one round trip, often with the value it had, so the
cross seems to do nothing. That is what both recordings show.

## Approaches considered

- **Keep the cross everywhere and say on the page why nothing changed.** Rejected: an
  explanation beside a control that does nothing is still a control that does nothing.
- **Remove every cross.** Rejected: the fields that are emptied have no other way to empty, and
  an empty weight or a dropped filter is what the user wants there.
- **Offer the cross only on fields the user constrained** (#1195 as filed). Needed, but not
  enough: on those fields the cross still takes two actions where the user meant one, to see
  the list.
- **Reopening moves to the dropdown arrow, the cross keeps emptying.** Chosen. It includes the
  previous approach: the server says which values the user constrained, so a value the solver
  determined offers neither.

## Chosen approach

The arrow of a field the user narrowed clears that field's own choice and opens the list the
server returns. The cross is offered only for emptying.

The main risk is a look that undoes the pick: a user opens the list, sees nothing better and
closes it, and is left with the dose unpicked and the order no longer solved. So the field keeps
the order it showed before the click and sends it back when the list closes without a pick. A
filter field does the same with its previous choice. While the list is open the previous value
is highlighted when it is among the options.

| Field | Cross | Arrow |
| --- | --- | --- |
| Order variable, several values, none picked | no | `Open`: opens the list |
| Order variable, one value the user constrained | no | `ReopenCleared`: clears, opens the list the server returns, previous value highlighted |
| Order variable, one value the solver determined | no | `NoArrow`: drawn as a fixed value |
| Range field, a value picked or stepped | no | `BackToRange`: the range comes back, no list; the stepper stays |
| Held filter field | yes, drops the constraint | `ReopenCleared`: clears, opens the list the server returns, previous value highlighted |
| Filter field, several options, one chosen | yes | `Open`: opens the list |
| Filter field, one option left by the other choices | no | `NoArrow`, as today |
| Anonymous patient's weight, height, gestational age | yes | `Open`: opens the list |
| Identified patient's weight, height, gestational age | no | `Open`: opens the list |
| Renal function, either patient | yes | `Open`: opens the list |
| Patient department | no | `Open`: opens the list |
| Search field, column filter, manual drugs | yes | as today |

The table rests on two points:

- **A range has no list.** After the clearing a range field shows its range as one entry, whose
  click picks the median. Its arrow therefore puts the range back and opens nothing.
- **The department's cross was a reopen.** It put the default back, which the field then showed.
  The default is among the options the arrow opens, and the notice under the field says which
  department is the default. Picking it from the list sets it as the patient's own choice rather
  than leaving it unset; the department in force is the same either way, so that is accepted.

The reset stays the way to discard every change at once.

## Confidence

Medium. The split of reopening and emptying is clear and each row of the table follows from it.
Less certain: whether an extra field on the domain order variable survives every solve and step
(step 2 checks this first), and how the controlled open state of MUI's `Select` behaves while a
request is under way (step 3 checks it in the browser).

## Steps

One pull request per step, in this order. The server flag comes before the order fields, so no
order field ever offers an arrow on a value the solver determined.

Script-only policy applies: `Client.Core`, `Shared`, the server and GenORDER are prototyped in
scripts and migrated by the maintainer; only the Client project is edited directly.

### 1. Field decision: cross and arrow per field kind

A script `src/Informedica.GenPRES.Client.Core/Scripts/FieldOpenPolicy.fsx` with a pure decision
per field kind: whether it offers the cross, and what its arrow does, one of the four cases named
in the table (`Open`, `ReopenCleared`, `BackToRange`, `NoArrow`).

The field kinds are an order variable, a filter field and an entry field. An order variable is
given its number of values, its mode from `QuantityModePolicy.decide` and whether the user
constrained it. A filter field is decided over `PickPolicy.offer`.

A second decision covers a list closed without a pick: restore the previous value.

Expecto tests for every row of the table, and for a click while a request is under way, which
does nothing.

### 2. Server flag: which values the user constrained (#1195)

- First, in a GenORDER script: does an extra field on `OrderVariable` in `Types.fs` survive the
  solve and the step commands, over the fixtures in `tests/Informedica.GenORDER.Tests/Scenarios.fs`?
  If it does, the flag lives on the domain order variable. If it does not, the order context in
  the contract carries the names of the constrained variables and the server passes them through.
- The flag is set when a value is picked (`setOvar (Some _)`) or stepped, and cleared by
  `setOvar None` and by the reset. In the contract it is a boolean on `OrderVariable` in
  `Shared/Types.fs`, mapped both ways in `ServerApi.Mappers.Order.fs`, with the round trip
  `toDto >> fromDto = id` tested over the fixtures.
- Because the flag comes from the server, the same fields offer the arrow after a reopen of the
  dialog and a reload of the plan.

### 3. Order fields: the arrow reopens, the cross goes (#1034)

- `Components/SimpleSelect.fs` holds whether the list is open, and takes an arrow action from its
  caller.
  - A click on the arrow runs the action, a clearing, and marks the list to open.
  - The list opens once the server's values have arrived and nothing is loading.
  - When the list closes without a pick, the select tells its caller so.

  The arrow stays visible wherever it has an action. The `description` tells a screen reader
  that opening shows the other choices.
- `Components/QuantityField.fs` passes the action through; a range field with a value picked or
  stepped gets `BackToRange`.
- `Views/ViewHelpers.fs`: `orderField` no longer takes `canClear` nor sets `hasClear`, and takes
  the flag of step 2 into the decision of step 1; a value the solver determined draws as fixed.
- `Views/Order.fs` and `Views/Nutrition.fs`: on the arrow, keep the order shown and send the
  field's existing change message with `None`, such as `ChangeSubstanceDoseQuantity None` or
  `ChangeFrequency None`. On a close without a pick, send `UpdateOrderScenario` with the order
  kept.

### 4. Filter fields: the arrow reopens a held field (#1193)

- `Components/PickField.fs`: the arrow of a held field clears it and opens the list the server
  returns, by the same mechanism as step 3; the cross stays and drops the constraint. A close
  without a pick chooses the previous value again.
- `Components/Autocomplete.fs`: a click or a focus on a held typed field does the same.
- `Views/Patient.fs`: the department field is not clearable.

### 5. Issues: closed with the answer

A comment on #1034, #1195 and #1193 with the answer and the pull requests; the as-built row in
[the grouping index](ux-issue-grouping.md) once each pull request number exists.

## Verification, per step

1. The script's tests pass in FSI.
2. The GenORDER script's tests pass: the flag survives the solve and the steps over the
   fixtures, and the contract round trip holds. `dotnet run servertests`;
   `dotnet fsi scripts/CheckDependencyRule.fsx`.
3. Fable compiles and `npx vite build` passes; `SimpleSelect.fs.js` checked for the JSX
   structure. In the browser, the dose dialog:
   - a picked dose shows no cross; the arrow lists the other doses with the previous one
     highlighted; Escape brings the dose back and the order stays solved; another pick takes;
   - a value the solver determined has no arrow, and the arrows are the same after a reopen of
     the dialog and a reload of the plan;
   - a stepped dose rate: the arrow puts the range back, the stepper still works.

   The nutrition page shows no cross on an order value and no arrow that does nothing.
4. A held medication: the arrow opens the medications for the indication in force, the cross
   drops the medication. An anonymous patient's weight: the cross empties it and it stays
   empty; an identified patient's has no cross; the department has none.
5. None: the issues and the index row are checked by reading them.
