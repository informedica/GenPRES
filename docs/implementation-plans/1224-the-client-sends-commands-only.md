# Implementation plan for issue #1224: the client sends commands only

The domain processes the client's specific commands ([the previous
plan](1224-the-domain-processes-the-commands.md)). The client still sends a changed context in a few
places, and its two order machines still decide what to do with a command by the old case it stands
for. This plan lets the client send commands only, removes the old wire cases it no longer sends,
and simplifies the machines that the old cases kept complex. It is a refactor: the user sees no
difference.

The work hangs under [#1224](https://github.com/informedica/GenPRES/issues/1224).

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [Out of scope](#out-of-scope)

## Problem description

The client sends an old case, with the context it changed itself, in three kinds of places:

- **Fallbacks.** A filter pick, a value pick and a reopen go as a specific command when the value is
  in the list the field offers, and as the old case with the changed context when it is not
  (`pickFilter` and `componentsChange` in `Views/Prescribe.fs`, `pickFilter` in
  `Views/NutritionSlot.fs`, `updateOrderScenario` in `Views/Order.fs` and
  `Views/NutritionSlot.fs`). A field offers only what its list holds, so the
  fallback is not expected to run; nothing shows that it does.
- **Seeds and patient changes.** The order context machine sends `UpdateOrderContext` with a context
  it built: the empty context for a new patient (`Open`) and after a prescription (`Reset`), the
  held context with the new patient (`PatientChanged`), and a filter from the url, the formulary
  page or a resource reload (`Seed`).
- **Never sent.** `SetArgumentationProperty` is in the contract and handled by the server, but the
  client writes the argumentation itself (`Argue`) and never sends it.

The two machines decide by the old case a command stands for (`OrderContextCommand.replaced` in
`Shared/Api.fs`):

- `Dialog.waits`: a dialog command waits while a request runs; a filter or scenario pick is dropped.
- `Dialog.carries` and `OrderPlanCart.replay`: a waiting pick, clear or reset goes out over the
  context it was made on, a waiting step over the context answered. The rule exists because the old
  pick carried its value in its context: sent over another context, the pick was lost.
- `apply` in `OrderContextMachine.fs`: a filter command syncs the formulary and parenteralia pages.

A specific command carries its value in itself, as an index into a list. That changes the question
the carries rule answers, which is the first decision below.

The two machines also hold the same request logic twice: one request in flight, one dialog command
waiting, an answer that lands only on its own request, and a reopen whose state is kept to be put
back (`InFlight`, `Pending`, `landing`, `Kept`, `Reopen`, `Restore` in `OrderContextMachine.fs` and
`OrderPlanMachine.fs`).

## Decisions

Taken on 2026-10-05, before the first step.

1. **A dialog command made while a request runs.** The user picks a value while the answer to an
   earlier change is still under way. Today the pick waits and goes out over the context the user
   picked it on, which already shows the earlier change as the preview (`Dialog.shown`). Three
   choices:
   - **(a) as today:** the pick goes over the context it was made on. Its index reads the list the
     user saw. The answer to the earlier change is replaced by the answer to the pick, which
     includes the earlier change, since the preview does. No visible difference.
   - **(b) over the context answered:** the pick goes over the newest context. Its index then reads
     the list as the server answered it, which can differ from the list the user saw: the pick can
     land on another value. A visible difference, and an unsafe one for a dose.
   - **(c) dropped:** a dialog command made while a request runs is dropped, as a filter pick is,
     and the dialog greys its fields meanwhile. `Pending` and `carries` go. A visible difference: a
     quick second pick needs a second click.

   Decided (user, 2026-10-05): (a) for a pick, a clear and a reset, since an index belongs to the
   list it was read in; a step keeps going over the context answered, as it carries no index. The
   rule then reads from the specific command itself, not from the old case.
2. **The preview.** `preview` in `Shared/Api.fs` shows a command while its request runs. Keep it:
   without it the field shows the old value until the answer lands. Decided (user, 2026-10-05):
   keep.
3. **The seeds and the patient change.** Two new wire commands, each answered by the domain:
   - `ChangePatient of Patient`: the held context evaluated for the new patient, its filter kept.
   - `SeedFilter of names`: a filter set by names, from the url, the formulary page or a reload,
     then evaluated. The names are what the url and the formulary page hold; an index has no list to
     read from there.
   - The empty workbench, at an open and after a prescription, is `ClearAllFilterProperty`, which
     exists.

   Decided (user, 2026-10-05): as listed.

## Steps

One pull request per step, one open at a time, each within the 200-line limit. Every code step
outside the client views starts as a script with its tests, unless the user asks for source.

1. **The fallbacks go** from `Views/Prescribe.fs`, `Views/Order.fs` and `Views/NutritionSlot.fs`
   (decided by the user, 2026-10-05). A field searches the list it was drawn from in the same
   render, so the lookup of a picked value fails only on a bug, such as a value written otherwise
   than its list. Then the client writes a warning to the console, sends nothing and keeps what it
   holds; the next answer is shown as it comes in. Nothing is set before the lookup succeeds: not
   the loading mark of the filter field, the loader on the order, the picks, nor the state of a
   reopen.
   The reopen in `Views/Order.fs` that finds no field at all, and goes as `ReopenOrderScenario`
   today, goes too; it ends as a list closed without a pick.
2. **The seed and patient commands.** `ChangePatient` and `SeedFilter` on the wire, mapped to domain
   cases; the domain sets the filter by names with the existing cascade. The order context machine
   sends them for `PatientChanged`, `Seed` and a reload, and `ClearAllFilterProperty` for `Open` and
   `Reset`. Tests: each new case answers as `UpdateOrderContext` on the context the client built
   today, on the fixtures.
3. **The old wire cases go.** `UpdateOrderContext`, `SelectOrderScenario`, `UpdateOrderScenario`,
   `ReopenOrderScenario` and `SetArgumentationProperty` leave `OrderContextCommand`, with their
   mapping and their server branches. The domain keeps its own old cases, which MCP and
   `OrderPlan.fs` use.
4. **The machines decide by the specific command.** `replaced` goes; `Dialog.waits`, the carries
   rule of decision 1 and the filter sync read the specific command directly. Client.Core tests for
   each rule.
5. **One request stage.** The request logic both machines hold twice moves into one module in
   Client.Core: in flight, waiting, landing, and the reopen with its state kept. Each machine keeps
   its own workbench or plan stage. Client.Core tests: the existing machine tests pass unchanged.
   Two pull requests if needed for size.

## Verification, per step

- **Steps 2 to 5 in code:** `dotnet run servertests`; `scripts/CheckDependencyRule.fsx`; benchmark
  build; Fable and `npx vite build`.
- **Every code step:** the user's browser check with the trail: the filter, the components, a
  scenario choice, picks and clears with picks kept, a quick second pick while a request runs, a
  patient change during a request, the url medication, a resource reload, the plan dialog and the
  nutrition slot.

## Out of scope

- The picks on the order scenario, so a clear carries none and the plan dialog has them too: a
  visible change for the plan dialog, for a decision of its own.
- The MCP tools taking the specific commands: a separate capability.
- Replay of a trail back into the machines.
