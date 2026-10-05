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
  scenario. `setFilterItem` chooses instead of narrowing. `Shared` keeps its functions only as the
  client's preview of a pick in flight (`Dialog.shown`), renamed, no longer a stop-gap; a test
  checks on the fixtures that the preview and the domain agree.
- **The order values.** `SetNth` and `Clear` are cases of the order command, addressed by a target
  the domain declares (level, property, and the component and item names), so the order processor
  knows the changed variable. `SetNth` sets the variable with `OrderVariable.setNthValue`, which
  counts from 1: the wire index, from 0, is converted once, in the server mapper.
  `processClearedOrder` goes.
- **The picks on the scenario.** The domain records each pick on the order scenario as the target
  the command addresses, in the order picked: the property alone for the schedule and the
  orderable, the component name with the property for a component, the component and item names
  with the property for an item. A target, not a variable name, since one target can address more
  than one variable: an item in every component of its name, and a component concentration also
  in the first component, as the dialog changes them. A step that moves its variable is a pick as
  well, as in the client today. A clear reads the picks, so the `Clear…Property` cases lose theirs
  and the client's `PickList` goes; the fields read whether their variable was picked from the
  scenario instead. The picks are user history, kept with the plan, not a projection of it.
- **The argumentation stays with the server.** `SetArgumentationProperty` is answered by the server
  without the domain, as now; the domain keeps the argumentation as a plain field.
- **The mapper maps one to one.** `OrderContextMapper.Command.toDomain` maps each new wire case to
  its domain case; `toChange` is deleted. The old wire cases stay, mapped as today, until the client
  no longer sends them (the next plan).

## Steps

One pull request per step, one open at a time, each within the 200-line limit. Every code step
starts as a script with its tests; the user migrates it to source, or asks the agent to.

1. **Script `src/Informedica.GenORDER.Lib/Scripts/SpecificCommands.fsx`: the filter.** The domain's
   `FilterField`, the cascade ported from `Shared` (`applyChange`, `chain`, `clearChoice`,
   `anyChosen`), `changeFilter`, `changeDiluent`, `setNthComponents`, `clearAll`,
   `selectNthScenario`, and a choosing `setFilterItem`. Tests on the `Scenarios.fs` fixtures: each
   function gives the same filter as the `Shared` function on the same input.
2. **Script, same file: the order target and the order commands.** The domain `Target` with
   `tryGet` and `map` (after `Shared`'s), `SetNth(target, n)` and `Clear target` as order commands
   in `OrderProcessor`, the target's variable set or cleared and the order solved or reopened.
   `Clear` takes the picks the command carries, as `Reopen` does today, until step 6.
   Tests: for every target with more than one value on every fixture, the new command gives the
   same order as today's `SolveOrder` and `Reopen` path. Then `processClearedOrder` without callers
   is checked: the client sends a cleared variable through `UpdateOrderScenario` in no path.
3. **Script, same file: the picks on the scenario.** `Picks` on the domain `OrderScenario`, a list
   of targets, added by `SetNth` and by a step that moves its variable, read by `Clear`, which
   puts back the variables of the earlier targets. A target picked again moves to the end, as
   `PickList.add` does today, so a list never holds a target twice. Tests: a clear keeps the picks
   made before the cleared one, as `Reopen` with the variable names does now, also for an item in
   two components of the same name, a component concentration, and a target picked again (picks
   A, B, A: a clear of A keeps B).
4. **Migration, GenORDER** (user): steps 1 and 2, the new `Command` cases and their evaluation in
   `evaluateOutcome`; GenORDER tests from the scripts. Two pull requests if needed for size. Step 3
   migrates with step 6.
5. **Server and contract**: the mapper maps the new cases; `processCmd` and `Navigate` answer
   `SetArgumentationProperty` themselves and send every other case to the domain; `toChange` is
   deleted and its change functions renamed as the preview; `OrderContextCommandTests.fs` compares
   the domain answer with the old case's answer instead of `toChange`'s.
   Until step 6 a clear still carries the client's picks: the domain context is rebuilt from the
   client's context on every request, so picks kept by the domain alone would not survive.
6. **The picks end to end**: step 3 migrated; `Picks` on the wire scenario and in the plan's JSON
   (a new JSON structure version, older rows read with no picks), so they travel with the context
   both ways; then the `Clear…Property` cases without picks;
   the client reads the picks from the scenario, `PickList` and its picks state go
   (`Views/Order.fs`, `Client.Core/PickList.fs`). Prototyped in a script where it is not client
   UI code.

## Verification, per step

- **Steps 1 to 3:** `dotnet run build`, the script in FSI; Expecto and FsCheck suites pass on the
  `Scenarios.fs` fixtures; no live rules.
- **Steps 4 and 5:** `dotnet run servertests`; `scripts/CheckDependencyRule.fsx`; benchmark build.
- **Step 6:** the above, Fable and `npx vite build`, and the user's browser check with the trail:
  picks, a clear that keeps the earlier picks, a reopened list closed without a pick, the plan
  dialog and the nutrition slot; a plan saved before the step opens.

## Decisions

Made on 2026-10-05.

- **The domain is the truth; `Shared` keeps a preview.** The client shows a pick in flight with the
  `Shared` change functions, renamed; a test keeps them equal to the domain's on the fixtures.
- **The filter logic moves into GenORDER**, and `setFilterItem` chooses instead of narrowing.
- **The argumentation stays with the server.**
- **The picks move onto the scenario in this plan**, and `PickList` leaves the client in its last
  step.
- **`processClearedOrder` goes in this plan**, once the order processor knows the target.
- **Every code step starts as a script.**

## Out of scope

For the plan that simplifies the client's state machines:

- whether a pick made while a request runs may be applied to the newest context, decided first;
- a command for a patient change and for a seed, so the client never sends `UpdateOrderContext`;
- the old wire cases and the client's fallbacks removed;
- the machines holding commands instead of contexts.

The MCP tools taking the specific commands remain a separate capability.
