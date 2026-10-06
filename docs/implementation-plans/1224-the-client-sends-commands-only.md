# Implementation plan for issue #1224: the client sends commands only

The domain processes the client's specific commands ([the previous
plan](1224-the-domain-processes-the-commands.md)). This plan finishes the work: every change to an
order context becomes a command, and the client's order machines hold commands instead of
contexts. The client then never changes a context itself; it shows the last answer, and the preview
of the command under way. A sequence of commands, played over the domain, gives the workflow back.
It is a refactor: the user sees no difference, except that the order dialog's fields are greyed
while a request runs (decision 1).

The work hangs under [#1224](https://github.com/informedica/GenPRES/issues/1224).

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [Out of scope](#out-of-scope)

## Problem description

The client changes an order context itself, outside any command, in these places:

- **Fallbacks.** A filter pick, a value pick and a reopen went as the old case with a changed
  context when the value was not in the field's list. Removed in step 1.
- **Seeds and patient changes.** The order context machine sends `UpdateOrderContext` with a context
  it built: the empty context for a new patient (`Open`) and after a prescription (`Reset`), the
  held context with the new patient (`PatientChanged`), and a filter from outside the workbench's
  own fields (`Seed`).
- **The page syncs.** A choice on the formulary or parenteralia page is written into the held
  context by `FilterSync.syncFormularyToFilter` and `syncParenteraliaToFilter` (through
  `OrderContextState.map` in `App.fs`), which also drop the diluent, the components and the
  scenarios, before the seed goes out.
- **The patients.** `Compute.bound` puts the Session's age and the estimates on every patient of
  every computing request: the order context being worked on, the plan's own patient, every
  context in the plan, and the formulary and parenteralia pages. Signing, which does not go through
  `Compute.bound`, puts the Session's age, without estimates, on the plan's patient and on every
  context in the plan (`SigningCommand.processCmd`). A context's patient is then rewritten, and a
  patient change on the panel reaches the server in several ways.
- **The argumentation.** The client writes the text into the held context (`Argue`, through `map`
  and `ArgumentationPolicy`); it travels inside every later command. `SetArgumentationProperty` is
  in the contract and handled by the server, but the client never sends it.

The two machines hold contexts, not commands:

- the workbench holds the context last evaluated (`OrderContextWorkbench.Evaluated`), the plan the
  plan last answered (`OrderPlanCart.Opened`);
- the request under way and the dialog command waiting hold a context with the command
  (`InFlight`, `Pending`), and the waiting command goes out over the context it was made on
  (`Dialog.carries`, `OrderPlanCart.replay`);
- the state before a reopen is kept whole, to be put back (`Kept`);
- they decide by the old case a command stands for (`OrderContextCommand.replaced`): which command
  waits, which carries its context, which syncs the formulary and parenteralia pages.

The two machines also hold the same request logic twice (`InFlight`, `Pending`, `landing`, `Kept`,
`Reopen`, `Restore` in `OrderContextMachine.fs` and `OrderPlanMachine.fs`).

## Decisions

1. **A dialog command made while a request runs.** Reopened on 2026-10-05, since the machines will
   hold commands. Today the command waits and goes out over the context it was made on: in the
   workbench the preview of the request under way, in the plan dialog the plan held. A client that
   holds commands has no such context to send, except a preview it computes, which is a context the
   client changed. The choices:
   - **(a) as today:** the waiting command goes over the preview of the command under way. No
     visible difference; the client still sends a context it changed, and a replay of the commands
     gives the same order only where the preview equals the domain's answer.
   - **(b) over the answer:** the waiting command goes over the answer to the command under way. Its
     index then reads the list as answered, which can differ from the list the user saw: the pick
     can land on another value. Unsafe for a dose.
   - **(c) no command while a request runs:** the dialog greys its fields while a request runs, as
     the filter fields do; `Pending`, `carries` and `replay` go. Every command goes over the last
     answer, so a replay is exact. A visible difference: a second pick waits until the first is
     answered, typically under a second.

   Decided (user, 2026-10-05): (c).
2. **The preview.** `preview` in `Shared/Api.fs` shows a command while its request runs. Decided
   (user, 2026-10-05): keep. It is display only; the client does not send it.
3. **The seeds and the patient change.** Decided (user, 2026-10-05):
   - `PatientChanged` (first named `ChangePatient`): the context evaluated for the patient the
     command carries, its filter kept.
   - `SeedFilter`: the five choices by name (indication, medication, route, form, dose type) and
     where they come from: the url, the emergency or continuous list, the formulary page, the
     parenteralia page, or a resource reload. The domain applies them with the rule of their
     source, as the client does today: the formulary and parenteralia page rules of `FilterSync`
     move into the domain, the url's medication is written over the workbench as `setMedication`
     does, an emergency or continuous list item's over an empty filter, and a reload changes
     nothing. The same rules go into the preview of `SeedFilter`, so the synced
     choices show at once, as today. The pages' own selections, kept in step with the workbench by
     `syncFilterToFormulary` and `syncFilterToParenteralia`, stay in the client: they change no
     order context.
   - The empty workbench, at an open and after a prescription, is `ClearAllFilterProperty`, which
     exists and clears the argumentation as the reset does now.
4. **The machines hold commands instead of contexts.** Decided in [the previous
   plan](1224-the-domain-processes-the-commands.md#out-of-scope). The client holds the last answer
   as it came in, never a context it changed, and the commands under way. What it shows is the last
   answer, with the preview of the command under way. Each command goes out with the last answer
   as its context, since the server keeps no state between requests.
5. **One patient change command for everything.** Decided (user, 2026-10-06). A patient's age and
   estimates are set at launch and on a patient change, nowhere else: for an identified patient
   the server computes the age, for an anonymous one the client sets it, and for both the server
   estimates a missing weight or height from the age. A patient change is one command, answered
   with the patient made complete; the client puts that patient on the plan, on the order context
   being worked on, and on the formulary and parenteralia pages. Launch sets the first patient; every
   later change goes through this command. No other request changes a patient: the server takes
   the patient it is sent and only checks that it is valid. No context's patient is ever
   rewritten: an order context keeps the patient it was evaluated for, and signing fixes the
   patient and the rest of the context, its order can still be changed. The case that evaluates
   the order context being worked on for the changed patient is named `PatientChanged`, on the wire
   (`ActiveOrderContextCommand`) and in the domain (`OrderContext.Command`): it reports a patient
   changed elsewhere, it does not change one.

## Steps

One pull request per step, one open at a time, each within the 200-line limit. Every code step
outside the client views starts as a script with its tests, unless the user asks for source.

1. **The fallbacks go** (landed, [#1315](https://github.com/informedica/GenPRES/pull/1315)). A
   value a field does not offer is written to the console and not sent; nothing is set before the
   lookup succeeds.
2. **The seed and patient commands, domain side.** Landed as
   [#1317](https://github.com/informedica/GenPRES/pull/1317) (the domain) and
   [#1318](https://github.com/informedica/GenPRES/pull/1318) (Shared and the server).
   `SeedFilter` on the wire as an order context command, `ChangePatient` (renamed `PatientChanged`
   in step 3a) as a command of its own
   for the order context being worked on (`ActiveOrderContextCommand`), so a plan cannot carry a
   patient change: a context in the plan keeps its patient. Both map one to one to the domain
   cases; the seed rules of decision 3 in the domain and in the preview.
   `PlanContext.resolveChange` treats both as it does a filter pick: applied to the context as
   sent, then looked up with the refusal check of `UpdateOrderContext`. Tests on the fixtures,
   with no rules loaded: each new case answers as `UpdateOrderContext` on the context the client
   builds today, for every source; the domain's seed rules and the preview's agree.
3. **One patient change command, server side** (landed). Decision 5.
   - **3a** ([#1320](https://github.com/informedica/GenPRES/pull/1320)). `ChangePatient` of the
     order context being worked on is renamed `PatientChanged`, on the wire
     (`ActiveOrderContextCommand`) and in the domain (`OrderContext.Command`).
   - **3b** ([#1321](https://github.com/informedica/GenPRES/pull/1321)). A patient command family
     with `ChangePatient` (`processPatient`), through `Compute.bound`: the Session's age for an
     identified patient, the client's for an anonymous one, a missing weight or height estimated
     for both, and the Session told the patient.
4. **The seed and patient commands, client side.**
   - **4a, the patient change** (landed). The client holds the patient in a machine of its own,
     as it holds the order context and the plan.
     - [#1323](https://github.com/informedica/GenPRES/pull/1323): the order context machine sends
       a patient change as `ActiveOrderContextCommand.PatientChanged` over the context as held, and
       a filter that arrives before the patient waits for it.
     - [#1324](https://github.com/informedica/GenPRES/pull/1324): `PatientMachine` holds the draft
       and the patient change under way; after an edit of the age, gender or gestational age the
       draft takes the answered patient, after a clear it stays as typed.
     - [#1325](https://github.com/informedica/GenPRES/pull/1325): the App uses it; the panel, the
       url and the Session send the patient command, and the answered patient goes to the plan
       (through `Recalculate`), the order context and the two pages. The client no longer
       estimates the draft. While a patient change is under way the panel, the order context and
       the plan show as a change under way, so nothing is ordered for the patient being replaced;
       a page's command that arrives anyway is dropped
       ([#1327](https://github.com/informedica/GenPRES/issues/1327) checks whether that backstop is
       needed). A failed change puts back the draft the orders were calculated for.
   - **4b, the seeds.** Two pull requests:
     - the order context machine sends `SeedFilter` for the url, a medication list item and a
       reload, and `ClearAllFilterProperty` for an open and a reset; a seed that arrives before
       the patient waits for it as a seed; `OrderContext.setMedication` goes;
     - the formulary and parenteralia pages send `SeedFilter` with their choices instead of
       writing them into the held context; `syncFormularyToFilter`, `syncParenteraliaToFilter`
       and the machine's old `Seed` of a whole context go.
   - **4c, the server stops changing patients**, in its own pull request. The Session records a
     change only from the patient command, no longer from every request (`patientOf` and `seen`
     in `Compute.bound`). `Compute.bound` leaves every patient as sent: `patients`, `patientsPlan`,
     `patientsActive` and the formulary, parenteralia and interaction versions go, with the age
     and estimate step in `ServerApi.Compute.fs`, which moves into the patient command. Signing
     stops putting the Session's age on the plan: `SigningCommand.patients` goes from
     `SigningCommand.processCmd`. The check that a patient is valid (`Patient.over`) stays. No
     context's patient is touched on any path, signing included. Tests: every request other than
     the patient command, signing included, leaves every patient as sent. The age-on-request
     tests (`AgeOnRequestTests.fs`) and `HeldContextTests.parsedAt`, which test the old rule, are
     rewritten to the new one.
5. **The machines hold commands.** `InFlight` holds the command under way; the workbench and the
   plan hold the last answer; the view is the last answer with the preview of the command under
   way. Before the argumentation step, since a command that waits behind a request can be
   replaced by the next one, and an argumentation command replaced that way loses the text
   (decided by the user, 2026-10-06). Two pull requests:
   - **5a, no command while a request runs.** The dialog greys every field while a request runs,
     the field being stepped included, so `Pending`, `Dialog.waits`, `Dialog.carries`,
     `OrderPlanCart.waits` and `OrderPlanCart.replay` go, with the trail's waiting-command lines.
     A command that arrives while a request runs is dropped, as the page's filter commands are
     now. A visible change: a second step of a dose waits until the first is answered.
   - **5b, every command over the last answer.** Three pull requests:
     - [#1333](https://github.com/informedica/GenPRES/pull/1333): the dialog keeps its own tab,
       the component and the item it shows, so the page no longer writes it into the context it
       sends (decided by the user, 2026-10-06);
     - the workbench: a command from the page carries no context, and the machine sends it over
       the answer it holds; `replaced` goes from Shared, since the machine reads the specific
       command;
     - the plan: the page names the context and the command, and the machine sends it over the
       plan it holds and that context as answered; the dialog hands out commands only, and the
       reopen keeps the answer it started from instead of a whole state.

     Client.Core tests: the machine tests rewritten over commands, their cases kept.
6. **The argumentation as a command.** The client sends `SetArgumentationProperty` instead of
   writing the text into the held context; the server writes it, as it does now. The command goes
   when the field is left (decided by the user, 2026-10-05); until then the text shows as the
   field's own typing. `Argue` and its `map` go from both machines, with
   `ArgumentationPolicy.keep` and `keepAll`, since every answer carries the text as written.
7. **The old wire cases go.** `UpdateOrderContext`, `SelectOrderScenario`, `UpdateOrderScenario` and
   `ReopenOrderScenario` leave `OrderContextCommand`, with their mapping and their server branches.
   The domain keeps its own old cases, which MCP and `OrderPlan.fs` use.
8. **One request stage.** Weighed after step 5: if the two machines still hold the same request
   logic, it moves into one module in Client.Core; if step 5 leaves little to share, this step is
   dropped.

## Verification, per step

- **Steps 2 to 8 in code:** `dotnet run servertests`; `scripts/CheckDependencyRule.fsx`; benchmark
  build; Fable and `npx vite build`.
- **Every code step:** the user's browser check with the trail: the filter, the components, a
  scenario choice, picks and clears with picks kept, a second pick while a request runs, a patient
  change during a request, the url medication, an emergency list item, a formulary and a
  parenteralia page change, a resource reload, the argumentation, the plan dialog and the nutrition
  slot.

## Out of scope

- The picks on the order scenario, so a clear carries none and the plan dialog has them too: a
  visible change for the plan dialog, for a decision of its own.
- A nutrition order working as a prescribing order context does: worked on outside the plan and
  entering it only once confirmed, so one order context is the one worked on and the plan holds the
  contexts confirmed (user, 2026-10-05). It belongs to
  [#495](https://github.com/informedica/GenPRES/issues/495), under the order plan group (G7). The
  machines this plan leaves over commands carry it: the nutrition slot then sends its commands to
  the order context being worked on, as the prescribing page does.
- The MCP tools taking the specific commands: a separate capability.
- Replay of a trail back into the machines: this plan makes it possible, it does not build it.
