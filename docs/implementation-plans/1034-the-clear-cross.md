# Implementation plan for issue #1034

Which fields offer the cross that clears them. Planned together with:

- [#1195](https://github.com/informedica/GenPRES/issues/1195): the cross is offered where clearing
  changes nothing;
- [#1193](https://github.com/informedica/GenPRES/issues/1193): a click on a held dropdown clears
  it and opens the list;
- [#1033](https://github.com/informedica/GenPRES/issues/1033): a filter field is offered the
  options it could take, not only the one it has.

The sections up to [As built](#as-built) are the plan as reviewed before the build; As built says
where the build went otherwise.

Follows #398, which #1031 closed by making the cross clear the field, and
[ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md): a control that is offered and does
nothing is removed, and the user never has to pick again what they already picked.

- [Problem description](#problem-description)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [As built](#as-built)

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
- **Reopening moves to the dropdown arrow, the cross keeps emptying; the scenario carries the
  variables the user picked, in order; the server reopens a cleared pick by a reset that keeps
  the picks made before it; each filter list is computed without the field's own choice (#1033).** Chosen. It
  includes the third approach: a value the solver derived offers neither cross nor arrow.

## Chosen approach

The arrow of an order field the user narrowed clears that field's own choice and opens the list
the server returns. A filter field needs no clearing: its list already holds the options it could
take. The cross is offered only for emptying.

On the server a cleared variable the user picked is reopened, not solved again: the order is
reset, the picks made before the cleared one get their values back, and the order is solved.
Picks made after it were made given its old value, and are dropped: on amphotericin the mL dose
picked after the mg dose pins the mg dose, so putting it back would reopen the mg dose to one
value. A look loses nothing by this, since closing the list without a pick restores the order
as it was; only a new pick in the reopened field lets the later picks go.

The order scenario carries the names of the variables the user picked, in the order picked: the
client adds a name on a pick, the server on a step, a reopen keeps the picks before the cleared
one, and a reset empties the list. A variable in the list is one the user constrained; one not in
it the solver derived or the server chose. A scenario without the list, stored before it existed,
reads as unknown: its fields offer the arrow, and a clear on it is solved as today, so it may
bring back one value only. That is accepted: the plan store holds development and test data
only, so no order in use is stored before the list.

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
and the reopen by reset with the earlier picks is shown to work on every fixture with a choice
(step 2's probe). Less certain: whether the filter lists of #1033 stay fast enough
with one filter pass per field (step 4 measures it); and how the controlled open state of MUI's `Select`
behaves while a request is under way (step 3 checks it in the browser).

## Steps

One pull request per step, in this order. The scenario carries the picks before the order fields
change, so no order field ever offers an arrow on a value the solver determined, and the arrow
never opens a list of one.

Script-only policy applies: `Client.Core`, `Shared`, the server and GenORDER are prototyped in
scripts and migrated by the maintainer; only the Client project is edited directly.

### 1. Field decision: cross and arrow per field kind

A script `src/Informedica.GenPRES.Client.Core/Scripts/FieldOpenPolicy.fsx` with a pure decision
per field kind: whether it offers the cross, and what its arrow does (`Open`, `ReopenCleared`,
`NoArrow`); `ReopenCleared` is for order variables only. Whether a range comes back cannot be told
at the click, since a picked range value and a picked list value both hold one value, so a second
decision, `reopened`, takes it once the answer lands: no values, no list.

The field kinds are an order variable, a filter field and an entry field. An order variable is
given its number of values, its mode from `QuantityModePolicy.decide` and whether the user
constrained it: yes, no or unknown, with unknown decided as yes. A filter field is decided over
`PickPolicy.offer`.

A second decision covers a list closed without a pick: restore the previous value. The restore
is a transition of the machines that hold the order shown: `OrderContextMachine` for the
workbench, and `OrderPlanMachine` for an order of the plan opened in the dialog. A `Reopen` sends
the clear and keeps the state before it; a `Restore` puts that state back. The state kept has
nothing under way, so the answer to the clear finds no request to land on; a reopen while a
request is under way keeps nothing.

In the plan the clear goes out as a `Navigate` command, which marks the plan as changed
(`PlanWorkPolicy.afterCommand`), and the mark is what the guard against leaving unsigned work
reads. So the plan machine keeps the plan's work state with the context at the click, and the
restore puts both back: a look at the list leaves a plan that was as signed as signed.

Expecto tests for every row of the table, for a click while a request is under way, which does
nothing, for the restore before and after the answer to the clear has landed, and for a restore
in a plan as signed, which leaves it as signed.

### 2. The scenario carries the user's picks, and a cleared pick is reopened (#1195)

**The probe, done.** Over the fixtures of `tests/Informedica.GenORDER.Tests/Scenarios.fs`, with
the pipeline the server runs (`CalcMinMax`, `CalcValues`, then `SolveOrder` per pick), picking
each field that still had a choice, then for each pick: cleared and solved, and reset with the
other picks put back and solved (`Scripts/ReopenProbe.fsx`).

| Fixture | Picks, in order | Cleared, solved | Reset, other picks back | Reset, earlier picks back |
| --- | --- | --- | --- | --- |
| paracetamol suppository | frequency, item dose | 1 each | frequency 2, item dose 3 | frequency 2, item dose 3 |
| paracetamol drink | frequency | 1 | 2 | 2 |
| cotrimoxazole | none: every field one value | | | |
| amphotericin | item dose (mg), orderable dose (mL) | 1 each | mg dose **1**, mL dose 120 | mg dose 7, mL dose 120 |
| morphine continuous | rate | 22: a plain clear already reopens | 22 | 22 |
| full medication | frequency | 0: a range | 0 | 0 |

The first build chose no single value by default on any fixture, so there is nothing to choose
again after the reset. Only amphotericin tells the two rules apart, and there only the earlier
picks reopen the field; that is the rule taken.

**The reopen** (`Scripts/Reopen.fsx`, 9 tests over the fixtures): `OrderReopen.reopen` takes the
picks and the order as it arrived. When a picked variable is cleared, it resets the order
(`ReCalcValues`), gives the picks before the cleared one their values back from the order as it
arrived (`Order.fromOrdVars`), solves (`SolveOrder`), and returns the order with the earlier
picks as the picks left. Otherwise it returns nothing, and the order is solved as today.
`OrderReopen.add` puts a pick at the end, moving one picked before; `OrderReopen.steppedBy` names
the variable a step command moves.

**The wiring, at migration:**

- GenORDER: a `Picks: string list option` on `OrderScenario` and its DTO. In
  `Api.evaluateOutcome`, `UpdateOrderScenario` reopens when `OrderReopen.reopen` applies and
  solves as today otherwise; a step command adds `steppedBy` to the picks; `ResetOrderScenario`
  empties them.
- Contract: `Picks: string[] option` on `OrderScenario` in `Shared/Types.fs`, mapped both ways in
  the server's scenario mapper. The client adds the variable's name on a pick; that is step 3.
- The store: the JSON structure version (`ServerApi.SqlAdapters.fs`) goes from 2 to 3; a row of
  structure 2 upgrades as it is, the list absent, so it reads as unknown.
- **The mark of a clear.** Today a clear reaches GenORDER as unrestricted only when the variable
  has no increment. The mapper forwards the mark on its own, whatever the increment, so that a
  cleared range field is cleared too. That is safe: the solver marks only a variable that holds
  no bounds, so a mark beside an increment comes from a client clear alone. Tested: a range that
  was not cleared and a marked variable from the server pass through unchanged.

Because the picks come back with the scenario, the same fields offer the arrow after a reopen of
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
  the picks of step 2 into the decision of step 1; a value the solver determined draws as fixed.
  A pick adds the variable's name to the scenario's picks.
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
   back, and a reopen gives the list back with the earlier picks kept and the later ones gone;
   the contract round trip holds; a stored plan of structure 2 reads with the picks unknown. `dotnet run
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


## As built

Every step landed as a pull request from a fork branch against `master`, on 2026-09-29 and
2026-09-30, script-first where it touched source outside the Client, the script migrated and
removed in the same pull request once reviewed. The deviations follow the table.

| Step | PR | Landed |
|------|----|--------|
| plan | [#1205](https://github.com/informedica/GenPRES/pull/1205) | this document |
| 1 | [#1206](https://github.com/informedica/GenPRES/pull/1206) | `FieldOpenPolicy` in `GenPRES.Client.Core`; `Reopen` and `Restore` in `OrderPlanMachine` and `OrderContextMachine` |
| 2 | [#1207](https://github.com/informedica/GenPRES/pull/1207) | `OrderReopen` in GenORDER; `Picks` on the order scenario, domain and contract; the clear mark sent alone; JSON structure version 3: #1195, the server half |
| 3 | [#1215](https://github.com/informedica/GenPRES/pull/1215) | `PickList` in `Client.Core`; the arrow reopens in `SimpleSelect`, `QuantityField` and `ViewHelpers.orderField`; the workbench, the plan dialog and the nutrition page wired to `Reopen` and `Restore`: #1034, and #1195's client half |
| 4 | [#1216](https://github.com/informedica/GenPRES/pull/1216) | each filter list without its own choice: `OrderContext.getRules` and `keep`, `FormularyService` and `ParenteraliaService`; `PickField` without the held field. Closes #1033 and #1193 |

### Deviations from the text above

- **The restore keeps a snapshot, not a request id.** A `Reopen` keeps the machine's whole state
  before it and a `Restore` puts it back; the state kept has nothing under way, so the answer to
  the clear finds no request to land on, without request ids passed from the App. The state
  kept never holds a request, on two levels: the machine takes a `Reopen` that arrives while a
  request is under way as a plain command and keeps no snapshot, so a later `Restore` changes
  nothing (review of #1206); and the arrow does nothing while any request of the page is under
  way, also a step shown before its answer, so the page never sends such a reopen (review of
  #1215).
- **`BackToRange` is decided after the answer.** A picked range value and a picked list value
  both hold one value, so the click cannot tell them apart; `FieldOpenPolicy.reopened` decides
  once the answer lands: no values, no list.
- **The picks are a list on the scenario, not a flag on each variable.** The probe of step 2
  found that putting back every other pick pins a coupled field: on amphotericin the mL dose
  picked after the mg dose leaves the mg dose one value. Only the picks made before the cleared
  one are put back, which needs their order; a list of names on the scenario carries it without
  touching the domain order variable. A step is a pick only when it moved its variable (review
  of #1207).
- **The formulary and the parenteralia were added to step 4.** They build their lists the same
  held way and share `PickField`, which would otherwise have left their fields without a way
  out.
- **A filter choice is never swapped for another value.** `keep` keeps a choice while its list
  holds it and drops it otherwise; only a field without a choice takes the one option. Each
  field's list is computed with the other choices, and a swapped choice could no longer fit
  them (review of #1216).
- **The department keeps its cross.** No department chosen is a state of its own, the default
  applying and the notice saying so, not the default picked; the plan had it as a reopen
  (review of #1216). The default is one literal in code, `Resources.Departments.defaultDepartment`.
- **Two client fixes found in the browser.** The dialog keeps the order from before a reopen
  shown until the answer lands, since the order sent has the value cleared and a field without
  values is not drawn; and a change from the dialog goes to the item it shows, also right after
  a switch of component, when no item is picked.
- **The popup of a reopen opens at once** with the previous value, under a loading overlay laid
  over the list, until the answer fills it.

