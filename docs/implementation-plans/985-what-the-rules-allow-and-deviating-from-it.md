# Implementation plan for issue #985

Closing G4, what the rules allow and deviating from it, of
[the grouping index](ux-issue-grouping.md#g4--what-the-rules-allow-and-deviating-from-it-985):
what the page says when no dose rule allows the pick, and how a clinician records why an order
deviates from the rules.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)
- [As built](#as-built)

## Problem description

G4 groups #499, #505, #478, #911 and #977 under #985. One question from two sides.

**The rules allow nothing** (#478, #911, #977). When the filter has picks and no dose rule
matches them, `OrderContext.getScenarios` in `src/Informedica.GenORDER.Lib/Api.fs` answers an
error message, `Geen doseerregels gevonden voor het geselecteerde filter`. The server flattens
every message to a string array, prefixed with `Error:` and joined, and the client's
`OrderContextWorkbench.noDoseRules` in `Client.Core/OrderContextMachine.fs` recognises the case
by a substring match on the Dutch text. It then starts over: the workbench is emptied, the
`GoToLifeSupport` effect switches the page to the emergency list, and the message goes to the
snackbar. The user who picked salbutamol for a six-day-old on the infusion-pump list lands on
the emergency list with no word why (#911); the user who reads the snackbar first sees that no
rules were found and nothing on what to do or whom to tell (#478). Two things are wrong: the
client decides a page switch by matching a Dutch string, so a rewording on the server silently
changes the client; and the user loses their place and the reason at once.

Since the pick no longer clears the other choices (the pull requests of #982), the case is rare
enough to be a real finding, which is why that plan handed its wording to #977.

**The clinician means to exceed the rules** (#499, #505). Nothing in the contract, the domain or
the store carries a free text: the contract's `Order`, `OrderScenario` and `OrderContext` have no
such field, and neither has GenORDER's `OrderContext` or the `PlanContext` that wraps it in the
plan. Severity is computed in GenORDER when the order is converted to its DTO, one `Level` per
order variable against the dose rule's limits, and the sign-time snapshot already reaches the
stored plan JSON, where nothing reads it back. The gap overview names this as the storage half of
its row 2.2.7, with #499 as the UX half. #505, prescribing a range rather than one value, is a
constraint question: the solver holds a range, and it is collapsed in three places, on the client
when a pick keeps one value, and in the solve pipeline when a range is expanded to values and
the median is pinned.

## Decisions

Taken 2026-09-28 by the maintainer.

| Question | Decision |
|---|---|
| How the reason travels (#977) | In the reply, not in the error channel: `processOrderContext` answers a response, `Evaluated` with the context or `Refused` with the context as sent and a typed refusal. This follows `LaunchOutcome.Refused` and `SigningResponse.Refused`. The string array stays the error channel for failures: the patient gate, a DTO that does not parse, rules not loaded, an exception. |
| Whom to contact (#478) | One localized sentence, the same for every site. No server setting. |
| Where the argumentation lives (#499) | On GenORDER's `OrderContext`, the record evaluation works on, and on the contract's `OrderContext`. Not on the `PlanContext` wrapper, and not on the order or the scenario, which every solve regenerates. It reaches the stored plan JSON through the context of each plan context. |
| When the dialog asks for it (#499) | Open, to confirm on the pull request of step 8: the plan assumes the field shows when any order variable of the scenario is marked, or when text is present, and that text stays once written until the user clears it. |
| The range (#505) | Out of this plan. It is a constraint question in GenORDER before it is a widget question, and it leaves the group as #978 left G3, to be planned on its own. |

The rules of [ADR-0009](../adr/0009-ux-design-rules.md) behind them: staying on the page with the
reason is rule 3, honoured under rule 1, safety over control, where the refusal is paid with its
reason on the page the user is on. The argumentation is rule 1: deviating stays an explicit act,
and this is where the act is written down. One contact sentence for every site is rule 2 read at
its simplest; a sentence per site is configuration and can follow when a site asks for it.

## Approaches considered

For the reason:

- **Keep the page switch and add a message on arrival.** Rejected: the user still loses the
  filter they built and has to rebuild it to try another route or form.
- **A typed error channel**: the error of `processOrderContext` becomes a union with a
  no-dose-rules case and a failure case. Rejected: every consumer of the string array changes
  with it, the server tests and the MCP host included, for one case that is not a failure.
- **A reply-only field on `OrderContext`**: the record gains a refusal the server sets and ignores
  on the way in. Rejected: the record that round-trips as the command would carry data that only
  the reply means.
- **A response union in the reply.** Chosen: the precedent exists twice in the contract, the
  error channel keeps its meaning, and the refusal is data the client renders and never
  interprets.
- **The contact from a server setting**, sent on `ServerSettings`. Set aside for now: one
  sentence serves every site today.

For the argumentation:

- **On the `PlanContext` wrapper**, beside the intake. Rejected by the maintainer: the text
  belongs to the context the clinician made, not to the plan's bookkeeping around it.
- **On the order or the scenario.** Rejected: both are regenerated on every solve, so the text
  would have to be carried through the pipeline by hand.
- **Asked in the sign dialog**, per order at signing. Rejected: what is submitted is the plan the
  challenge was issued over, so a text typed after the challenge would need a second challenge
  round.
- **On the order context**, typed in the dose dialog. Chosen: it rides every command unchanged,
  lands in the plan when the context does, and is in the plan before the challenge.

## Chosen approach

Two tracks, each a run of pull requests, one at a time; this plan before them and the closing
documentation after each track.

### The page stays and says why

**GenORDER.** A refusal type beside `OrderContext` with two cases: `NoDoseRules`, no dose rule
exists for the generic and route at all; and `NoDoseRulesForPatient`, dose rules exist for the
generic and route and none covers this patient. The second is detected by filtering the loaded
dose rules on generic and route alone, without the patient. It is not detected by running the
rule filter with the patient's values left out: a missing value never matches a bounded range,
as the comment above the selects in `Views/Prescribe.fs` already says of a patient without an
age. `OrderContext.evaluate` returns an outcome, `Evaluated` with the context or `Refused` with
the context as sent, its picks kept, and the refusal; the message list stays the error for real
failures. The existing function becomes a wrapper that maps a refusal back to today's Dutch
message, so the MCP host and the GenORDER tests do not move in the same pull request.

**Contract.** `OrderContextRefusal` and `OrderContextResponse`, next to `LaunchOutcome` in
`Shared/Types.fs`; `processOrderContext` replies the response. Terms for the notice: a title, one
body per case naming the generic and the route, and for the patient case that the patient is
outside every rule, and the contact sentence. They are drafted in `Shared/Scripts/Localization.fsx`
and pasted into `Terms` and the localization sheet, as the session refusals were.

**Server.** The order-context port and service carry the outcome; `processOrderContext` maps it
to the response. The plan lane's `Navigate` and the nutrition discovery evaluate through the same
service; there a refusal becomes the words of the error channel, so `processOrderPlan` keeps its
type. A refusal on the plan lane can only follow a rules reload, and the generic banner is the
right answer for it.

**Client.Core.** The answer the machine lands carries the response. `noDoseRules`, `startOver`
and the `GoToLifeSupport` intent and effect go. A refused answer keeps the workbench evaluated
with the context as sent and holds the refusal in the state, cleared by the next accepted answer,
a patient change, a seed or a reset; while idle with a refusal the view is a new
`OrderContextView.Refused` with the context and the refusal, so the page keeps its selects and
the user can re-pick. A pending dialog command drops on a refusal as it drops on a failure. A new
`OrderContextRefusalPolicy` after `SessionGatePolicy` in the project words the refusal from the
terms, with an English fallback, as `SessionGatePolicy.refusalBody` does for a launch refusal.

**Client.** `App.fs` passes the response to the machine and loses the page switch; every match on
the view gains the refused case, treated as settled where a command is built; `Views/Prescribe.fs`
renders a `Notice` of kind warning, with the title, the body and the contact sentence, in the
block that already shows the missing-dimension notice above the selects. Failures still reach the
snackbar as they do today.

### The argumentation

**Domain and contract.** GenORDER's `OrderContext` gains `Argumentation`, an optional string, and
so does its DTO in `Api.fs`; every construction site sets it, and evaluation copies the record,
so the text survives every command. The contract's `OrderContext` gains the same field; the
order-context mappers copy it both ways and the session mappers carry it into and out of a
signed version. The store's JSON structure version goes from 1 to 2 with an upgrade step; the
script of step 7 decides whether the step writes the absent field into a version 1 row, after
checking whether any read path recomputes the digest of a stored version. The server refuses a
text longer than 1000 characters where it parses the context.

**Client.Core.** An `ArgumentationPolicy` with two rules: whether the field is wanted, which is
any order variable of the narrowed scenario carrying a severity reason or text being present;
and how a text is normalised, trimmed and empty to none. Two messages carry the text: one on the
workbench, editing the held context without a server call, and one on the plan lane by context
id, counted as a change of the plan. The held-context policy compares whole contexts, so writing
the text holds the patient panel as any edit does.

**Client.** The dose dialog gets a multiline field as the last entry of its field list, shown by
the rule and committed when the field loses focus; the prescribing page and the plan page wire
it to their lane. The sign dialog lists the plan's contexts and shows the text read-only under
each order, so the signer reads what the pharmacist will. What the pharmacist receives is the
pharmacy hand-off of the roadmap, not this plan.

What is not stored beyond that: the severity snapshot is already in the signed JSON, as the level
on each order variable and the kind of each text block. Reading it back is the follow-up of the
gap overview's row 2.2.7, not this plan.

### Related, not planned

- **#505**, the range. The solver holds a dose as a range, and the pipeline and the client
  collapse it: a pick keeps one value, the solve expands a range to values and pins the median,
  and severity reads the rule's limits, so a user's range cannot simply narrow them. A control
  for two values does not exist. It is planned on its own, from a script that keeps a user range
  through the pipeline and records what survives.
- **#598**, no test harness for the client's rendering: the notice and the field are checked in
  the browser, which is why the rules behind them live in `Client.Core`.
- **#1129**, the large step named in the domain; unrelated in code, the same move of a rule out
  of the server.

### Documentation and the issues

- This plan gains its as-built table, once per track.
- The G4 section of the grouping index: the plan link, the status of each member, #505 out of
  the group, the components used.
- #977, #478 and #911 close with the pull requests of the first track; #499 with those of the
  second; #985 closes when both are built, #505 having left it.

## Confidence

High for the reason: the reply shape has two precedents in the contract, the machine change is
tested in `Client.Core.Tests`, and the notice component exists. Medium for the argumentation: the
field itself is small, but the stored JSON's structure version and the digest of already signed
versions need the script's check before the step is sized.

## Steps

One pull request at a time, each merged before the next. Code outside the client is prototyped
in a script and migrated by the maintainer, `Client.Core` included; client code is committed
locally and pushed after the maintainer has checked it in the browser. Source lines are the
changed lines of shipped code under `src/`.

1. **This plan** (docs), committed locally and read by the maintainer before it is pushed.
2. **The typed outcome in GenORDER.** A script in `src/Informedica.GenORDER.Lib/Scripts/` with
   the refusal type, the outcome, the generic-and-route detection and the wrapper. Its tests: an
   unknown generic gives `NoDoseRules`; salbutamol for a six-day-old on the infusion-pump list
   gives `NoDoseRulesForPatient`; a normal pick gives `Evaluated`; the wrapper still yields the
   Dutch message. The tests migrate to `tests/Informedica.GenORDER.Tests`. About 40 source lines.
3. **The contract additions.** The two types and the terms, sheet rows included; the API
   signature does not change, so nothing else moves. About 25 source lines.
4. **The wire and the machine.** The reply type; the server port, service, command and
   adapters; the machine, from a script in `src/Informedica.GenPRES.Client.Core/Scripts/` that
   carries the two tests of `OrderContextMachineTests.fs` that assert the start over, rewritten,
   and new ones: a refusal keeps the picks, the next accepted answer clears it, the view is
   refused while idle and changing while a re-evaluation runs; the stub adapter tests retyped
   with a refused case; `App.fs` and the matches on the view. One compile unit, so one pull
   request; if it passes 200 source lines, the adapters' plan-lane mapping goes first on its own.
   About 130 source lines.
5. **The notice.** The refusal policy from a script, one test per case with and without the
   contact sentence, and the notice on the prescribing page. About 80 source lines.
6. **Docs for the first track**: the index, the as-built table, the three issues closed.
7. **The argumentation, domain to store.** The field on the GenORDER context and its DTO, on
   the contract, in the mappers, and the structure version with its upgrade and the length cap.
   Scripts in `src/Informedica.GenORDER.Lib/Scripts/` (the DTO round trip, evaluation keeps the
   text, a version 1 JSON without the field reads as none) and in
   `src/Informedica.GenPRES.Server/Scripts/` (the mapper round trip, the upgrade, the cap). The
   tests migrate to the order plan, mapper, SQL adapter and version identity test files. About
   60 source lines.
8. **The rule and the messages in `Client.Core`.** The policy and the two messages from a
   script: wanted or not, normalised, the workbench message keeps an in-flight request, the plan
   message flags a change and makes no call. About 45 source lines. The open decision on when the
   field shows is confirmed on this pull request.
9. **The dialogs.** The field in the dose dialog, its wiring on the two pages, the read-only text
   in the sign dialog, the terms. About 95 source lines.
10. **Closing docs**: the index, the as-built table, #499 closed, #505 out of the group, #985
    closed.

The MCP host reading the typed outcome instead of the wrapper is a later pull request of its own,
outside this plan.

## Verification

- Every code step: `dotnet run build`; the script's tests; `dotnet run servertests`, with
  `CI=true` in a worktree; `dotnet fsi scripts/CheckDependencyRule.fsx` after steps 2, 4 and 7,
  where server code is removed or reshaped; the Fable compile with a reading of the generated
  output for nesting and hoisted icon imports, then `npx vite build`, after steps 4, 5 and 9.
- In the browser, the first track: a six-day-old patient, the infusion-pump list, salbutamol.
  The page stays on the prescribing page with its picks; the notice names the generic and the
  route, says the patient is outside every rule, and ends with the contact sentence; picking
  another route clears it; a medication with no rules at all, from the emergency list, gives the
  other sentence; a real failure still reaches the snackbar; no answer ever shows the emergency
  list.
- In the browser, the second track: a dose stepped outside its limits shows the field; the text
  survives stepping the dose again and a re-solve; it shows under the order in the sign dialog;
  the signed version's row holds it under structure version 2; a version 1 row reopens with no
  text; a text over the cap is refused with a message.
- Docs: `npx markdownlint-cli2` on the touched files.

## As built

| Step | Pull request | Note |
|---|---|---|
