# Implementation plan for issue #1224: the domain processes the specific commands

The client sends every pick and clear as a specific command, `SetNth…Property` or
`Clear…Property` with an index ([the previous plan](1224-specific-order-context-commands.md)). The
server still turns each one into an old case with the stop-gap `toChange` before the domain sees
it. This plan lets the domain process the specific commands itself, so `toChange` goes and the
domain knows which variable the user changed. It is still a refactor: the user sees no difference.

The work hangs under [#1224](https://github.com/informedica/GenPRES/issues/1224), as the
previous plan's *Later* list. The client's state machines are simplified afterwards, in a plan of
their own.

- [Problem description](#problem-description)
- [Chosen approach](#chosen-approach)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [Decisions](#decisions)
- [Out of scope](#out-of-scope)
- [As built](#as-built)

## Problem description

`toChange` (`src/Informedica.GenPRES.Shared/Api.fs`) translates a specific command into today's
command and the context the client used to build, with the change functions in `Shared`:

- a filter, diluent or components command becomes `UpdateOrderContext`, the context changed by the
  `Shared` filter cascade (`applyChange`: which choices below a field go, when the scenarios go);
- a scenario choice becomes `SelectOrderScenario`, the context narrowed to that scenario;
- `SetNth…Property` becomes `UpdateOrderScenario` with the variable set to its nth value; the
  domain then finds the changed variable by the order's state (`processClearedOrder`,
  `OrderProcessor.fs`);
- `Clear…Property` becomes `ReopenOrderScenario picks` with the variable cleared;
- `SetArgumentationProperty` becomes no command at all.

So the domain still receives changed contexts, and the knowledge of what a filter pick does lives in
`Shared`, the contract, not in the domain. The domain has its own, different filter functions
(`setFilterItem` narrows a list to the nth instead of choosing it; `checkDiluentChange`,
`checkComponentChange`), used by nobody for a pick. The picks a clear needs are kept by the client
(`PickList`) and sent with every clear, because the domain does not remember them.

## Chosen approach

The domain gets the specific commands as cases of its own `OrderContext.Command`, each with the
context as it is, and does the change itself:

- **The filter.** The `Shared` cascade moves into GenORDER as the domain's: choosing the nth option
  of a field, clearing a field, clearing all, the diluent, the components and the choice of a
  scenario. `changeFilter` chooses, and replaces `setFilterItem`, which narrowed. `Shared` keeps its functions only as the
  client's preview of a pick in flight (`Dialog.shown`), renamed, no longer a stop-gap; a test
  checks on the fixtures that the preview and the domain agree.
- **The order values.** Each pick and each clear of an order value is a `ChangePropertyCommand`
  case (`SetNthItemDoseRate(cmp, itm, n)`, `ClearItemDoseRate(cmp, itm)`, and so on), processed by
  `OrderProcessor.processChangeProperty` through `OrderPropertyChange.proc` with the typed setters,
  as the existing property steps are. Each case sets or clears exactly one variable, the one the
  field shows: the component by its name, the item by its component and item names. Nothing fans
  out to other variables of the same name, and the dose dialog's habit of also changing the
  component concentration in the first component is not copied. The nth value counts from 0, as
  the wire index does, so nothing is converted. An index past the last value, or a component or
  item that is not there, leaves the order as it is. The domain case changes the order in the
  context and then evaluates it as today's case, `UpdateOrderScenario` for a pick and
  `ReopenOrderScenario` for a clear, so a pick the final solve refuses answers as it does now.
- **The picks stay in the client.** The client keeps the variables the user picked or stepped, in
  the order picked (`PickList`), and sends them with a clear, as now. They decide whether a field
  with one value shows its reopen arrow, and which values a clear keeps: `Reopen` puts back the
  picks made before the cleared one. Moving them into the domain would give the plan dialog picks
  it does not have today, so it is not part of this refactor.
- **The argumentation stays with the server.** `SetArgumentationProperty` is answered by the server
  without the domain, as now; the domain keeps the argumentation as a plain field.
- **The mapper maps one to one.** `OrderContextMapper.Command.toDomain` maps each new wire case to
  its domain case; `toChange` leaves the server. The old wire cases stay, mapped as today, until the client
  no longer sends them (the next plan).

## Steps

One pull request per step, one open at a time, each within the 200-line limit. Every code step
starts as a script with its tests; the user migrates it to source, or asks the agent to.

1. **The filter** (landed as source). The domain's `FilterField`, the cascade ported from `Shared`
   (`applyChange`, `chain`, `clearChoice`, `clearField`, `anyChosen`), `changeFilter`,
   `changeDiluent`, `setNthComponents`, `clearAll` and `selectNthScenario`; `setFilterItem` goes.
   Tests on the `Scenarios.fs` fixtures: each function gives the same filter as the `Shared`
   function on the same input.
2. **The order values** (landed as source). The `SetNth…` and `Clear…` cases of
   `ChangePropertyCommand`, processed in `processChangeProperty`; `setNthValue` on the order
   variables that lacked it; `Variable.setNthValue` counts from 0 for a list of values, as it did
   for a range. Tests: one variable changes per command; on every fixture, a pick or clear of every
   variable with more than one value gives the same order as today's path through `Shared` and
   the mapper.
3. **Script `src/Informedica.GenORDER.Lib/Scripts/SpecificCommands.fsx`: the domain commands.**
   The new cases and their evaluation: a filter case through the step 1 function, then the rules
   looked up as `UpdateOrderContext` does; the choice of a scenario as `SelectOrderScenario`; a
   pick or a clear of an order value through `processChangeProperty` on the order in the context,
   then evaluated as `UpdateOrderScenario` or `ReopenOrderScenario` with the picks the command
   carries. An index past the filter options or the scenarios is an error. Tests on the fixtures,
   with a provider holding no rules: each new case answers as today's case on the context it
   changed.
4. **Migration, GenORDER** (user): the cases of step 3 join `OrderContext.Command`, with
   `Command.get`, `Command.toString` and their evaluation in `evaluateOutcome`, answering with the
   new case; the tests from the script.
5. **Server and contract**: the mapper maps the new cases one to one; `processCmd` and `Navigate`
   answer `SetArgumentationProperty` themselves and send every other case to the domain;
   `toChange` leaves the server and is renamed `preview`, the client's view of a request under
   way; `OrderContextCommandTests.fs` checks what reaches the domain instead of `toChange`'s
   answer.

## Verification, per step

- **Step 3:** `dotnet run build`, the script in FSI; the Expecto suite passes on the
  `Scenarios.fs` fixtures; no live rules.
- **Steps 1, 2, 4 and 5 in source:** `dotnet run servertests`; `scripts/CheckDependencyRule.fsx`;
  benchmark build.
- **Step 5:** the above, Fable and `npx vite build`, and the user's browser check with the trail:
  the filter, a scenario choice, picks, a clear that keeps the earlier picks, the plan dialog and
  the nutrition slot.

## Decisions

Made on 2026-10-05.

- **The domain is the truth; `Shared` keeps a preview.** The client shows a pick in flight with the
  `Shared` change functions, renamed; a test keeps them equal to the domain's on the fixtures.
- **The filter logic moves into GenORDER**, and `changeFilter` chooses where `setFilterItem`
  narrowed.
- **An order command changes one variable**, through the existing `ChangePropertyCommand` and
  `OrderPropertyChange.proc`, with no target type of its own and no fan-out to same-named
  variables.
- **The nth value counts from 0** in the domain as on the wire.
- **The picks are variable names**, since a command changes one variable.
- **The argumentation stays with the server.**
- **The picks stay in the client**, sent with a clear as now; moving them into the domain changes
  the plan dialog, so it is not part of this refactor.
- **`processClearedOrder` stays.** A clear of a variable the user did not pick is solved through
  it, as now; removing it would change what such a clear gives.
- **Every code step starts as a script.**

## Out of scope

For the plan that simplifies the client's state machines:

- whether a pick made while a request runs may be applied to the newest context, decided first;
- a command for a patient change and for a seed, so the client never sends `UpdateOrderContext`;
- the old wire cases and the client's fallbacks removed;
- the machines holding commands instead of contexts;
- the picks on the order scenario, so a clear carries none and the plan dialog has them too.

The MCP tools taking the specific commands remain a separate capability.

## As built

Every step landed as a pull request from a fork branch against `master` on 2026-10-05, as source on
the user's request: steps 1 and 2 without a script, steps 3 and 4 migrated from the script in one
pull request. The step that put the picks on the scenario was dropped: the picks stay in the
client. The server step went over the 200-line limit by the user's one-time allowance, since the
fix the browser check asked for came with it.

| Step | PR | Landed |
| --- | --- | --- |
| plan | [#1306](https://github.com/informedica/GenPRES/pull/1306) | this document |
| 1 | [#1307](https://github.com/informedica/GenPRES/pull/1307) | `FilterField`, the cascade and `emptyFilter` in `OrderContext.fs` |
| 1 | [#1308](https://github.com/informedica/GenPRES/pull/1308) | `changeFilter`, `changeDiluent`, `setNthComponents`, `clearAll`, `selectNthScenario`; `setFilterItem` and `FilterItem` removed |
| 2 | [#1309](https://github.com/informedica/GenPRES/pull/1309) | the `SetNth…` and `Clear…` cases of `ChangePropertyCommand`; `setNthValue` counts from 0 |
| docs | [#1310](https://github.com/informedica/GenPRES/pull/1310) | this document brought up to steps 1 and 2 |
| 3, 4 | [#1311](https://github.com/informedica/GenPRES/pull/1311) | six cases of `OrderContext.Command`, each evaluated as the existing command on the context it changed; the picks kept in the client |
| 5 | [#1312](https://github.com/informedica/GenPRES/pull/1312) | the mapper maps every new wire case; the server sends them to the domain, the argumentation excepted; `toChange` renamed `preview`; a filter or scenario pick applied before the context is reconciled (231 lines) |

Found on the way:

- The slider's `setPercValue` never reaches its branch for a list of values, since such a variable
  also reports a minimum and a maximum; counting from 0 leaves the slider as it was.
- For the parenteral nutrition fixture, today's path also changes the sodium and potassium
  concentration in the first component, as the dialog does. Those variables hold one value, so the
  user sees no difference; the domain command changes the picked variable only.
- In the morphine infusion, after a pick of the time and then of the dose rate, a clear of the dose
  rate keeps the time as a pick, but the reopened order offers the whole time range again. It is
  the same `Reopen` the client asks for today.
- The plan context is reconciled with the rules before its command runs, which rebuilds the
  filter's lists. A filter or scenario pick therefore reads its index in the context as sent, the
  lists the user saw, and is reconciled after (`OrderContext.resolvePick`). The browser check
  found it: the first of two indications was read in the whole list.
- An index past the values now reaches the domain, which leaves the order as it is; the server
  refused it before. The client never sends one.
- `processClearedOrder` stays: a clear of a variable the user did not pick is solved through it.
- Concentration picks for amphotericin B and cotrimoxazole are refused by the final solve on both
  paths, as before: [#1302](https://github.com/informedica/GenPRES/issues/1302).
