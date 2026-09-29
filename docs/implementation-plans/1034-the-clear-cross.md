# Implementation plan for issue #1034

Which fields offer the cross that clears them. Planned together with:

- [#1195](https://github.com/informedica/GenPRES/issues/1195): the cross is offered where clearing
  changes nothing;
- [#1193](https://github.com/informedica/GenPRES/issues/1193): a click on a held dropdown clears
  it and opens the list;
- [#1033](https://github.com/informedica/GenPRES/issues/1033): a filter field is offered the
  options it could take, not only the one it has.

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
  medication list holds that medication alone. The client cannot tell such a field from one left
  with one option by the other choices: both arrive as one option, chosen.
- A **range field** is an order variable shown as one range, from its minimum to its maximum,
  with a stepper beside it; a click on the range picks the median. The code calls this mode
  `Navigable` (`QuantityModePolicy`).
- **Stepped** means changed with the stepper buttons beside a field.

Since #1031 pressing the cross clears one field and asks the server again. The recordings on the
issue #398 (alpha.38) show it still reads as a control that does nothing.

The cause: the cross does two different jobs.

1. **Reopening.** An order variable picked or stepped to one value, or a held filter field. Such
   a field is never left empty: the solver computes the same value again, or the field chooses
   its one option again. What the user is after is the other choices, the list. Today that takes
   the cross, a wait, then the dropdown arrow, which is hidden while the cross shows. The arrow
   of an order field holding one value opens a list of that one value.
2. **Emptying.** The empty field is a state that stays and means something: the weight of an
   anonymous patient cleared, renal function back to none, a filter constraint dropped, the text
   of a search, a column filter, the manually added drugs on the interactions page.

For reopening the field fills again within one round trip, with the value it had, so the cross
seems to do nothing. That is what both recordings show.

### What a clear of an order variable is today

Traced from the cross to the solver on 2026-09-29, and probed on the paracetamol suppository
and oral liquid fixtures of `tests/Informedica.GenORDER.Tests/Scenarios.fs` with the pipeline
the server runs.

The clear crosses three boundaries, each reading it differently:

- The client (`Shared/Models.fs`, `setOvar None`) marks the variable non-zero positive, drops its
  values and keeps its minimum, maximum and increment.
- The server mapper (`ServerApi.Mappers.Order.fs`) forwards the mark alone when the variable has
  no increment, and the bounds with the mark when it has one.
- GenORDER (`OrderVariable.Dto.fromDto`) ignores the mark and builds the variable from the bounds
  and values it received: nothing left means unrestricted, which is what the pipeline calls
  cleared. A cleared variable that still had an increment arrives as its old range, not cleared.

The pipeline (`OrderProcessor.fs`) then solves the order again. On a solved order that gives the
same value back, whichever variable was cleared:

| Order, then action | Item dose values | Frequency values |
| --- | --- | --- |
| Item dose and frequency picked, solved | 1 | 1 |
| Item dose cleared, solved again | 1, the same | 1 |
| Orderable dose cleared, solved again | 1 | 1 |
| Frequency cleared, solved again | 1 | 1, the same |
| Reset (`ReCalcValues`) | 3 | 2 |
| Reset, frequency picked again, solved | 3 | 1 |

Two reasons. Every variable the solver derived, the dose per time, the dose per kilogram, the
total, keeps its one value, and the equations derive the cleared one from them again. And the
pipeline's cleared-order step recognises six variables only, the frequency, the rate, the time,
the concentration, the orderable dose quantity and the item dose per time; a cleared item dose
quantity, the field the recordings show, is not one of them and goes to the plain solve with a
warning in the log. The step does not help the six either: it re-applies the constraints of some
derived variables, not all, and returns one value.

Only the last row of the table reopens the field: the reset brings every list back, and picking
the frequency again keeps the user's other choice. So reopening one field is not a clear and a
solve. It is a reset, the other choices of the user applied again, and a solve. That needs the
order to carry which variables the user constrained; today nothing does, and after a solve a
value the user picked and a value the solver derived look the same.

## Approaches considered

- **Keep the cross everywhere and say on the page why nothing changed.** Rejected: an
  explanation beside a control that does nothing is still a control that does nothing.
- **Remove every cross.** Rejected: the fields that are emptied have no other way to empty, and
  an empty weight or a dropped filter is what the user wants there.
- **Offer the cross only on fields the user constrained** (#1195 as filed). Needed, but not
  enough: on those fields the cross still takes two actions where the user meant one, to see
  the list; and the list does not come, see the table above.
- **Extend the cleared-order step to every variable.** Rejected: on a solved order the derived
  variables pin the cleared one whatever the step re-applies, as the orderable dose row shows.
  The reset already does the right thing; the step would grow into a second reset.
- **A new reopen command in the contract, naming the variable.** Rejected: the order the client
  sends already says a variable was cleared, and the reopen does not depend on which one. The
  contract keeps its commands.
- **For the filter fields, the client remembers who chose a field**, the user or the field's
  one-option choice, to tell a held field from a field with one option. Rejected: the memory is
  lost on a reload, and #1033 makes the difference disappear instead.
- **Reopening moves to the dropdown arrow, the cross keeps emptying; the order carries which
  variables the user constrained; the server reopens a cleared variable by a reset that keeps
  the others; each filter list is computed without the field's own choice (#1033).** Chosen. It
  includes the third approach: a value the solver derived offers neither cross nor arrow.

## Chosen approach

The arrow of an order field the user narrowed clears that field's own choice and opens the list
the server returns. A filter field needs no clearing: its list already holds the options it could
take. The cross is offered only for emptying.

On the server a cleared variable of a solved order is reopened, not solved again: the order is
reset, every other variable the user constrained gets its value back, and the order is solved.
The order carries which variables the user constrained, as a flag on the order variable: set by
the client on a pick and by the server on a step, cleared by the clear and by the reset. An order
without the flag, stored before it existed, reads as unknown, and unknown is treated as
constrained, in the field and in the reopen alike: the field offers the arrow, and the reopen
gives an unknown value back as it gives a flagged one, so no choice of the user is lost. On such
an order the values the solver derived are unknown too and are given back with the rest, so the
reopen may bring back one value only, as a clear does today; it opens the list once the order
has been built again after this change. A value the server
chose by default when the order was first built is not flagged, and the reopen chooses it again
the same way; only the cleared variable is left open.

A filter list is computed with every other choice in force but without the field's own (#1033).
A chosen filter field then shows the options it could take, so it opens as any list does and no
field is held; a list of one is truly one.

The main risk is a look that undoes the pick: a user opens the list, sees nothing better and
closes it, and is left with the dose unpicked and the order no longer solved. So the field keeps
the order it showed before the click and puts it back when the list closes without a pick. The
restore is local: the kept order is the server's own earlier answer, so nothing is sent. The list
may close before the answer to the clear has landed; that answer is then dropped, so it cannot
overwrite the restored order. While the list is open the previous value is highlighted when it is
among the options.

| Field | Cross | Arrow |
| --- | --- | --- |
| Order variable, several values, none picked | no | `Open`: opens the list |
| Order variable, one value the user constrained, or unknown | no | `ReopenCleared`: clears, opens the list the server returns, previous value highlighted |
| Order variable, one value the solver determined | no | `NoArrow`: drawn as a fixed value |
| Range field, a value picked or stepped | no | `BackToRange`: the range comes back, no list; the stepper stays |
| Filter field, several options, one chosen | yes, drops the constraint | `Open`: opens the list |
| Filter field, one option | no | `NoArrow`, as today |
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

Medium. The split of reopening and emptying is clear and each row of the table follows from it,
and the reopen by reset is shown to work on two fixtures. Less certain: whether the reopen holds
on the continuous and timed fixtures, where the rate and the time take part, and whether the
default choices of the first build can be made again after the reset (step 2 probes every
fixture first); whether an extra field on the domain order variable survives every solve and
step (from a read, only `OrderVariable.create`, `createNew` and `Dto.fromDto` build a fresh
record, the rest copies; step 2 tests it); whether the filter lists of #1033 stay fast enough
with one filter pass per field (step 4 measures it); and how the controlled open state of MUI's `Select`
behaves while a request is under way (step 3 checks it in the browser).

## Steps

One pull request per step, in this order. The order carries the flag before the order fields
change, so no order field ever offers an arrow on a value the solver determined, and the arrow
never opens a list of one.

Script-only policy applies: `Client.Core`, `Shared`, the server and GenORDER are prototyped in
scripts and migrated by the maintainer; only the Client project is edited directly.

### 1. Field decision: cross and arrow per field kind

A script `src/Informedica.GenPRES.Client.Core/Scripts/FieldOpenPolicy.fsx` with a pure decision
per field kind: whether it offers the cross, and what its arrow does, one of the four cases named
in the table (`Open`, `ReopenCleared`, `BackToRange`, `NoArrow`). `ReopenCleared` is for order
variables only.

The field kinds are an order variable, a filter field and an entry field. An order variable is
given its number of values, its mode from `QuantityModePolicy.decide` and whether the user
constrained it: yes, no or unknown, with unknown decided as yes. A filter field is decided over
`PickPolicy.offer`.

A second decision covers a list closed without a pick: restore the previous value. The restore
is a transition of the machines that hold the order shown: `OrderContextMachine` for the
workbench, and `OrderPlanMachine` for an order of the plan opened in the dialog. It puts the kept
context back and drops the answer to the clear by its request, whether that answer is still in
flight or already pending.

In the plan the clear goes out as a `Navigate` command, which marks the plan as changed
(`PlanWorkPolicy.afterCommand`), and the mark is what the guard against leaving unsigned work
reads. So the plan machine keeps the plan's work state with the context at the click, and the
restore puts both back: a look at the list leaves a plan that was as signed as signed.

Expecto tests for every row of the table, for a click while a request is under way, which does
nothing, for the restore before and after the answer to the clear has landed, and for a restore
in a plan as signed, which leaves it as signed.

### 2. The order carries the user's choices, and a cleared variable is reopened (#1195)

In a GenORDER script over the fixtures of `tests/Informedica.GenORDER.Tests/Scenarios.fs`:

- **The probe first, on every fixture.** Pick, solve, clear one variable, solve: one value back.
  Reset, pick the others again, solve: the list back with the other picks kept. The table above
  is the expected shape; the continuous and timed fixtures add the rate and the time to it. The
  probe also checks which values the first build chose by default, and that they can be chosen
  again after the reset. If a fixture does not reopen by reset, the step stops and the plan is
  revised.
- **The flag.** A field on `OrderVariable` in `Types.fs`: whether the user constrained the
  variable. Set by the server on every step command (`ChangeProperty`), cleared by the reset
  (`ReCalcValues`) and by a clear. An option: absent is unknown. Carried by `OrderVariable.Dto`
  and mapped both ways, so it reaches the order the plan store keeps. The store's JSON structure
  version (`ServerApi.SqlAdapters.fs`, written and read as 2) goes to 3; a row of structure 2
  upgrades with the flag absent, so a stored order reads as unknown and its fields keep the
  arrow. Tested: the flag survives the solve and every step command over the fixtures, and a
  structure 2 row reads back with the flag absent.
- **The reopen.** In `OrderProcessor.processPipeline`, the cleared-order step of `SolveOrder`
  becomes: reset the order as `ReCalcValues` does, set the value of every variable whose flag is
  set or unknown back to what it was, make the default choices of the first build again for every
  variable but the cleared ones, solve. The step is taken when any variable arrives cleared; which
  one does not change the result, since a cleared variable has lost its flag. No new command. The
  six-variable pattern and the warning for a clear it does not recognise go, since every variable
  is reopened the same way. Tested over the fixtures with the counts of the table, and over the
  same fixtures with every flag unknown: every value but the cleared one comes back.
- **The contract.** A `bool option` on `OrderVariable` in `Shared/Types.fs`, set by `setOvar (Some _)`
  in `Shared/Models.fs` and cleared by `setOvar None`, mapped both ways in
  `ServerApi.Mappers.Order.fs`, with the round trip `toDto >> fromDto = id` tested over the
  fixtures.
- **The mark of a clear.** Today a clear reaches GenORDER as unrestricted only when the variable
  has no increment. The mapper forwards the mark on its own, whatever the increment, so that a
  cleared range field is cleared too. That is safe: the solver marks only a variable that holds
  no bounds, so a mark beside an increment comes from a client clear alone. Tested: a range that
  was not cleared and a marked variable from the server pass through unchanged.

Because the flag comes back with the order, the same fields offer the arrow after a reopen of
the dialog and a reload of the plan.

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
  `ChangeFrequency None`; the server reopens it as step 2 says. On a close without a pick,
  dispatch the restore of step 1; nothing is sent.

### 4. Filter fields: each list without its own choice (#1033, #1193)

- In a GenORDER script: `filterIndications`, `filterGenerics`, `filterRoutes`, `filterForms` and
  `filterDoseTypes` in `Informedica.GenORDER.Lib/Api.fs` are each given the filter without their
  own field. Tested over the rules: with every field chosen, each list holds the chosen value
  and the others the other choices allow; a pick from such a list is consistent with every other
  choice. Timed against today's single pass.
- `Components/PickField.fs`: no field is held any more, so `isHeld`, the read-only select and the
  cross of a field with one option go; a chosen field with several options keeps the cross,
  which drops the constraint. This closes #1193: a click on a chosen field opens its list, with
  nothing to clear first.
- `Components/Autocomplete.fs`: the same for the typed shape.
- `Views/Patient.fs`: the department field is not clearable.

### 5. Issues: closed with the answer

A comment on #1034, #1195 and #1193 with the answer and the pull requests; the as-built row in
[the grouping index](ux-issue-grouping.md) once each pull request number exists.

## Verification, per step

1. The script's tests pass in FSI.
2. The GenORDER script's tests pass: on every fixture a pick, a clear and a solve give one value
   back, and a clear through the new step gives the list back with the other picks kept and the
   default choices made again; the flag survives the solve and the steps; the contract round
   trip holds; a stored plan of structure 2 reads with the flag unknown, and a reopen on it
   keeps every other value. `dotnet run
   servertests`; `dotnet fsi scripts/CheckDependencyRule.fsx`. In the browser, on the
   paracetamol suppository with a dose and a frequency picked: the cross on the dose brings the
   dose list back and keeps the frequency.
3. Fable compiles and `npx vite build` passes; `SimpleSelect.fs.js` checked for the JSX
   structure. In the browser, the dose dialog:
   - a picked dose shows no cross; the arrow lists the other doses with the previous one
     highlighted and the frequency kept; Escape brings the dose back without a request and the
     order stays solved; another pick takes;
   - Escape while the list still waits for the answer brings the dose back, and the late answer
     changes nothing;
   - in a plan just opened or signed, the arrow on an order's dose and Escape leave the plan
     unchanged: leaving the page asks nothing;
   - a value the solver determined has no arrow, and the arrows are the same after a reopen of
     the dialog and a reload of the plan;
   - a stepped dose rate: the arrow puts the range back, the stepper still works.

   The nutrition page shows no cross on an order value and no arrow that does nothing.
4. The GenORDER script's tests pass. With an indication and a medication chosen, the medication
   field opens on the other medications for the indication, and a pick keeps the indication;
   the cross drops the medication. An indication with one medication shows it without cross or
   arrow. An anonymous patient's weight: the cross empties it and it stays
   empty; an identified patient's has no cross; the department has none.
5. None: the issues and the index row are checked by reading them.
