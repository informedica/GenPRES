# UI/UX issues grouped by cause

This is the index of the per-group UI/UX plans. It groups the open UI/UX issues by **what code has
to change**, so that each group is one umbrella issue with one implementation plan and one
coherent set of pull requests, instead of one plan per reported symptom.

The eleven umbrella issues were filed from this index on 2026-09-24:
[#981](https://github.com/informedica/GenPRES/issues/981) for G0 and
[#982](https://github.com/informedica/GenPRES/issues/982) to
[#991](https://github.com/informedica/GenPRES/issues/991) for G1 to G10. Each group's members are
sub-issues of its umbrella. Four umbrellas are built and closed: #981 and #982 on 2026-09-25,
then #984 and #986 on 2026-09-28. Member status in the tables below is as of 2026-09-28.

The index has no issue number of its own, because it is not a plan for one issue; each per-group
plan is named after its umbrella.

It is the companion to [the MVP gap overview](../roadmap/mvpap2019-gap-overview.md), which groups
the same issues by **milestone**. Where the two disagree, the gap overview decides *when* and this
index decides *what*. Every group follows [ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md).

- [Why group them](#why-group-them)
- [G0 — what the groups share](#g0--what-the-groups-share-981)
- [The ten groups](#the-ten-groups)
- [Small UI bugs outside the groups](#small-ui-bugs-outside-the-groups)
- [What the grouping surfaced](#what-the-grouping-surfaced)
- [Left out on purpose](#left-out-on-purpose)
- [What this leads to](#what-this-leads-to)

## Why group them

The UX designer filed 32 issues; 29 were open when this index was written. They are the whole of
the *UI and UX update* milestone except one. They were filed one observation at a time, and
several name the same code:

- five issues concern the same dropdown component;
- two concern the same `create` call in `Shared/Models.fs`;
- four concern the same DataGrid toolbar.

Planned one by one, each would be a pull request touching a file another pull request already
touches, and three would be planned against code that is not the cause.

Thirteen further issues, from the maintainer and the clinical tester, are the same work seen from
another side. Four of them (#976, #977, #978, #979) were filed from this index.

- the emergency-list dead end: #478, #911, #977;
- the patient panel: #716, #717, #718, #976;
- the dose dialog: #978;
- the sign dialog: #648;
- navigation: #915, and #979, the UX rules ADR;
- the browser tab: #518, #682.

Leaving them out would mean planning two halves of one change separately.

**In scope: 42 issues**, the designer's 29 plus these thirteen, marked **+** in the tables.

**Open on 2026-09-27: 28**, of which 19 are the designer's.

Issues waiting on a Figma design (#394, #402, #405, #496, #500, #502, #504) are grouped by
mechanism: where the component lives and what has to become a parameter. The visual decision is
recorded as an input still to come. Only #504 has a Figma link on the issue so far.

## G0 — what the groups share (#981)

The ten groups are not independent. Built group by group, each shared part would be invented
separately, and the interface would end up less consistent than it started. For example:

- one control serves #405, #398, #397 and #504;
- one severity treatment (how the UI shows how serious a value is) serves #402, #496 and #478;
- one action bar serves #404, #394 and #495.

**G0** is the eleventh group, and the first to be built: the foundation and the common components
the other ten use. It has no reported issue of its own; it exists because the ten groups overlap.
Each group below lists what it uses from G0. The catalogue, with the evidence, the prop shapes and
the call sites each component replaces, is in [G0's plan](981-ux-foundation-and-common-components.md);
the component list is also on #981.

Foundation:

- **F1** one severity type and one renderer;
- **F2** a theme palette;
- **F3** a label convention for shared components;
- **F4** delete the 328 dead lines and fix the two defects they hide.

Components:

- **C1** `QuantityField`, an order variable's value with its steps and bounds;
- **C2** `PickField` and `MultiPickField`, the dropdowns;
- **C3** `SearchField`;
- **C4** `SeverityMark`, the icon with the reason on hover;
- **C5** `ValueChip`;
- **C6** `Notice`, an alert on a page, including the empty state;
- **C7** `ActionBar` and `ActionButton`;
- **C8** `ConfirmDialog` and `DialogShell`;
- **C9** `Disclosure`, a section that folds to a summary;
- **C10** `SectionHeading`;
- **C11** `ListToolbar`, the controls above a list;
- **C12** `PrintTable`.

The foundation was built before the components, and G0 before the ten groups.

**Button convention.** #404 puts the call to action (the button that completes the task: OK,
submit, sign) on the right. The TPN frames put the contained button to the left of the outlined
one, and #394 puts the reset at the bottom left of the selection fields. The convention had to be
stated either as prominence or as position.

**Decision** (recorded in [ADR-0009](../adr/0009-ux-design-rules.md)): the call to action is the
prominent button and goes on the right; the reset is secondary and goes on the left. C7 applies
this; the TPN frames follow it.

**Built:** everything, the foundation in PRs #994 to #1004 and the components in
PRs #1005 to #1025, closed 2026-09-25. C1 was later rebuilt as the five-slot field by #1102
([its plan](1102-one-quantity-field-for-every-order-variable.md), PRs #1106 to #1114, closed
2026-09-27), which also removed the interim `Stepper.fs`.

## The ten groups

Each group has the same parts: a table of members with milestone and status, the cause in the
code, what is still to settle, and the G0 parts it uses. Milestones are the gap overview's (M2 to
M5, or post-MVP); *none* means the issue is neither in the gap overview nor in a GitHub milestone.
**+** marks an issue filed by the maintainer or the clinical tester rather than the designer.
In the status column a number marked PR is a pull request; any other number is an issue.

### G1 — One dropdown, one set of controls, one state (#982)

| Issue | Milestone | Status |
|---|---|---|
| #498 | post-MVP | done: the one-option rule, PRs #1012, #1013 |
| #403 | none | closed 2026-09-26 as a duplicate of #501 |
| #501 | none | open: half done by PR #1028; the rest is issue #1033 |
| #398 | post-MVP | done: one rule for the clear cross on a dose field, PR #1031 |
| #394 | post-MVP | done: one word and a bounded button for the reset, PR #1030 |

**Cause.** One component caused all five.

- `Components/SimpleSelect.fs` showed either a clear cross or a stepper, never both (the
  `endAdornment` binding). That was #398: a field with a stepper had no cross, and where a cross
  did show, `ViewHelpers.orderSelect`'s `updateSelected = if isEmpty then ignore else …` made it
  do nothing.
- `ViewHelpers.filterSelect` and `autoComplete` were disabled for an *empty* list but not for a
  list of *one* (#498), while `orderSelect` silently selected a single value and stayed enabled.
- Re-picking without clearing first (#403, #501) is a cascade question in the `OrderContext`
  module of `Shared/Models.fs` (`indicationChange`, `medicationChange`, `routeChange`,
  `formChange`, `doseTypeChange`), called from `Prescribe.fs` and `Nutrition.fs`. It is not a
  widget question, and not a `Client.Core` one: the machine there only carries the context to the
  server.
- The reset of #394 was `Prescribe.fs`'s `clear()`, dispatching `OrderContext.empty` behind a
  full-width text button.

**Built** in [982's plan](982-one-dropdown-one-set-of-controls-one-state.md), PRs #1027 to #1032,
closed 2026-09-25. What is left of #501: the server computes each option list from the whole
filter, including that field's own choice, so a fully chosen filter reduces every list to the value
it holds. #1033 gives each list the filter without its own field.

**To settle:**

- one disabled / one-option / clear rule in `ViewHelpers`: done, as `PickPolicy` in
  `Client.Core`;
- the adornment conflict in `SimpleSelect`: done, the stepper no longer uses the adornment;
- which field resets which in `OrderContext`: done, with an Expecto test per rule in
  `Shared.Tests`, script first since `Shared` is not client UI;
- the reset control's size and name: done; the word is a terminology decision shared with other
  groups.

**Uses** C1 `QuantityField`, C2 `PickField` / `MultiPickField`, C7 `ActionBar`, C8 `ConfirmDialog`.

### G2 — Finding a medication (#983)

| Issue | Milestone | Status |
|---|---|---|
| #503 | none | done: a search field on the two lists, C3, PR #1021 |
| #487 | post-MVP | done: checkbox rows, C2, PR #1013; one filter, C11, PR #1022 |
| #502 | none | open: the second filter was removed in PR #1022; the toolbar wording remains |
| #400 | none | open |

**Cause.** `Components/ResponsiveTable.fs` rendered two independent filters: the custom "Filter"
`MultipleSelect` above the grid and `GridToolbarFilterButton` in the toolbar. That was #502, and
the confusion behind #487. Neither list had a text search, while the user tests show that users
prefer searching to filtering (#503). #400 (brand names) is the same need on the prescribing page,
and it reaches beyond the client: the brand belongs to the product, not to the generic pick list.

**To settle:**

- which parts of the MUI toolbar stay, and how its labels read for a non-technical user (#502);
- brand-name matching in GenFORM (#400);
- one search control on the prescribing generic field, as the two lists now have.

**Uses** C2 `MultiPickField`, C3 `SearchField`, C11 `ListToolbar`.

### G3 — The dose dialog (#984)

Planned in [984-the-dose-dialog.md](984-the-dose-dialog.md); built in PRs #1128, #1131 and #1132,
closed 2026-09-28.

| Issue | Milestone | Status |
|---|---|---|
| #397 | post-MVP | closed 2026-09-28: the order stays, `keer dosis` before `dosering`, the calculation order |
| #402 | none | done: the reason on hover, C4, PR #1016; the wording stands; closed 2026-09-28 |
| #405 | post-MVP | done: the field shows which meaning applies, #1102 and PR #1125; closed 2026-09-28 |
| #496 | none | open, left the group with #978; #500 was closed into it on 2026-09-26 |
| **+**#978 | none | open, left the group: the server-sent field list and lead field, on its own |

**Cause.**

- *Field order.* `Views/Order.fs` decides which fields it shows and in what order: `keer dosis`
  then `dosering`, hard-coded per dose type. #397 asked for the reverse; the reply on the issue
  argues that the current order is the calculation order.
- *Severity.* Severity reached the user only as a coloured double underline, with no reason and
  no icon (#402).
- *Stepper buttons.* The outer two of the five stepper buttons have two meanings: min/max while
  the variable can still be navigated, and a large step once it is solved (#405).
- *Which field moves the dose.* #496 asks which field to change to move the dose; the solver
  knows this but does not show it.
- *The preparation section*, not in the issues: it stayed open with all its fields once every
  preparation value was solved, and took room from the sections the user was working in.

**Decision** (2026-09-28): the field order stays as it is; the dose is calculated from the dose
per administration and the frequency, and #397 closes with that. Which fields the server sends,
in what order and which one to start from (#978, with #496 as its member) leaves the group and
stays open on its own. #402 is fixed by the mark with its reason; #405 by the field showing which
meaning the outer buttons carry, and leaving the outer slots out when the large step is the small
one. Halve and double are not planned. The stepping stays as it is: the small step is the defined
increment, the large step the calculated increment, which is the grid the solver narrowed the
order to; the server's copy of that rule moves into GenORDER under #1129. The preparation section
folds behind its heading once every value it shows holds one value, opens while one is still to
be chosen, and takes the user's toggle in between; the order view only.

**Uses** C1 `QuantityField`, C4 `SeverityMark`, C10 `SectionHeading`; the fold is `SectionFold`
in `Client.Core`, a heading with a button, not C9.

### G4 — What the rules allow, and deviating from it (#985)

Planned in [985-what-the-rules-allow-and-deviating-from-it.md](985-what-the-rules-allow-and-deviating-from-it.md).

| Issue | Milestone | Status |
|---|---|---|
| #499 | M4 | done: the argumentation on the order context, typed in the dose dialog, stored with the signed version, PRs #1156 to #1161 |
| #505 | M4 | closed 2026-09-28, not built: a user-defined range outside the rules is a missing dose rule; stepping out of range stays the deliberate route |
| **+**#478 | M4 | done: the contact sentence in the notice, PRs #1147 to #1151 |
| **+**#911 | none | done: the reason in place, no page switch, PRs #1147 to #1151 |
| **+**#977 | none | done: the decision below, built in PRs #1147 to #1151 |

**Cause.** One question, from two sides.

*The rules allow nothing.*

- #478: the emergency list offers a medication with no dose rule, and the page only says "geen
  doseerregels gevonden".
- #911: a prescription the Kinderformularium does not allow (salbutamol for a six-day-old, from
  the infusion-pump list) sends the user to the emergency list without saying why. The cause is
  exact: `OrderContextWorkbench.noDoseRules` in `Client.Core/OrderContextMachine.fs` matches the
  error text "geen doseerregels" and emits `GoToLifeSupport`, which `App.fs` applies as a plain
  page switch, discarding the error. #478 is the same error, read by the user before the switch.

Since PR #1028 a pick no longer clears the other choices, so the message appears less often;
[982's plan](982-one-dropdown-one-set-of-controls-one-state.md) hands its wording to #977.

*The clinician means to exceed the rules.*

- #499: deviate, with a written argumentation. The store of #516 is built, but storing the
  deviation is still missing: severity is recomputed on each solve and never stored (see
  [the gap overview](../roadmap/mvpap2019-gap-overview.md)).
- #505: prescribe a range rather than a single value. This is a constraint question in GenORDER,
  not a widget question.

**Decision** (#977): when no dose can be shown, because there are no dose rules or for any other
reason, the page stays where it is and says why. There is no page switch. `GoToLifeSupport` is
removed; the server's error is shown as a `Notice` on the current page, and the server sends the
reason, not a marker the client matches on.

**Decisions** (2026-09-28, in [985's plan](985-what-the-rules-allow-and-deviating-from-it.md)):
the reason travels in the reply, as a response that is either the evaluated context or the
context as sent with a typed refusal, the shape of `LaunchOutcome.Refused`; the error channel
stays for failures. Three refusals: no dose rule for the picks at all, rules for the picks that
cover no patient like this one, and rules that cover both with no product or dose type to
prescribe with; the text is optional, as #499 asks. Whom to contact is one localized sentence for
every site. The argumentation lives on the order context, GenORDER's and the contract's, typed in the
dose dialog and stored with the signed version under a new structure version of the plan JSON.
Issue #505 was closed the same day, not built: a range the user defines outside the rules is a
missing dose rule, and stepping a value out of its range stays the deliberate route.

**Built**, the first track, in [985's plan](985-what-the-rules-allow-and-deviating-from-it.md),
PRs #1146 to #1151, 2026-09-28: the server answers a typed refusal in the reply, the page keeps
its place and its picks and says why, with whom to tell, and the emergency list is never the
answer. Found on the way and filed as #1155: the plan lane's `Navigate` and the nutrition
discovery still drop a pick the rules no longer offer, in silence.

**Built**, the second track, PRs #1156 to #1161, 2026-09-28: the argumentation from the domain to
the store, under structure version 2 of the plan JSON, capped by the server where it parses a
context; the rule and the two messages in `Client.Core`, the text the client's own and kept over
every answer; the field last in the dose dialog, shown when the one scenario's order is marked or
a text is present, and the read-only line in the sign dialog; a reset takes the text with it.
Closed 2026-09-28 with #499 and #985; #1155 stays open on its own.

**To settle:**

- the reason the server sends when the rule set is empty, and whom the user should contact:
  done, three typed refusals and one contact sentence;
- the `Client.Core` change that shows it in place, with a test: done, the machine holds the
  refusal and a policy words it, deferring to the page's own notice on a missing weight or
  height;
- where a free-text argumentation is stored on the signed order plan version: done, on the
  order context, under structure version 2; the dialog asks for it when the one scenario's
  order is marked or a text is present, confirmed on #1158;
- the range as an order variable the user may leave unresolved: closed with #505, not built.

**Uses** C6 `Notice` in the first track; C4 `SeverityMark` in the second.

### G5 — The patient: what is entered, what is estimated, what is hidden (#986)

| Issue | Milestone | Status |
|---|---|---|
| #488 | M5 | done: the setters keep measured values, PRs #1044, #1045 |
| #489 | post-MVP | done: the panel closes only when the user closes it, C9, PR #1020 |
| **+**#716 | none | done: the server estimates, PR #1056 |
| **+**#717 | none | done: the department is a visible choice with one default, PRs #1048 to #1054 |
| **+**#718 | none | open: waits for a growth table; detached from the umbrella 2026-09-28 |
| **+**#976 | none | done: the two patient modes, PRs #1062 to #1081, closed 2026-09-26 |

**Cause.**

- #488 had an exact cause. In `Shared/Models.fs`, `setYear` rebuilt the patient through `create`,
  passing `None` for weight, height and gestational age (weeks and days). `setMonth` did the same;
  `setWeek` and `setDay` cleared weight and height but kept gestational age. So entering an age
  after a weight discarded the measured weight, and the display fell back to the estimate
  (`getWeight`), which is why it looked *overwritten* rather than *cleared*. A dosing input was
  dropped without warning: of the 42 issues, the one with the greatest safety impact.
- #716: only the client estimated, so a patient arriving from a launch or the MCP host with only
  an age was refused, while the web client accepted it.
- #717: a missing department defaulted to `ICK` in the mapper, and the panel had no department
  field: a hidden filter input on every web patient.
- #718: a weight alone, or a height alone, as the minimum; deferred from the patient-minimum plan.
- #489: the panel collapsed automatically after five idle seconds (`Views/Patient.fs`).

**Decision** (#976): the panel has two modes, decided by the launch.

- **Identified** (launched with a patient the EHR has data for): the title bar shows the patient's
  id, name and birthdate. The server computes the age from the birthdate when the Session opens
  and again right after each sign, and holds it in between, so every calculation of an episode
  uses one age. (#976 first asked for the age on each request; its plan decided otherwise.) The
  panel shows the age read-only. Weight, height and gestational age stay editable; a bedside value
  overrides the EHR data, is kept, and is recorded at the next signature. The department comes
  from the launch, replacing the hidden default of #717. EHR data used to carry an age (years,
  months, weeks, days) and no name; it now carries the birthdate and the name.
- **Anonymous** (no specific patient): the panel asks for age, weight and the rest, as before. A
  launch the EHR has no data for is anonymous unless a signed version already names the patient;
  then the Session opens identified from that version. A patient the MCP host creates from an
  age is anonymous too.

The setters of #488 therefore still matter: in anonymous mode, and for weight and height in both
modes. In identified mode the age has no setter.

**Built** in two plans: [986's](986-the-patient-what-is-entered-estimated-hidden.md), in
PRs #1041 to #1059, and [976's](976-two-patient-modes.md), in PRs #1060 to #1085. Each range
opens with the plan's own PR and closes with its as-built record; the steps are the PRs between,
as the member rows list them. #718 waits on a growth table that nobody has named, so it was
detached on 2026-09-28 and the umbrella closed that day; #718 stands on its own. Follow-ups:

- #1061, an idle Session ends: done, PR #1086;
- #1075, the patient context held from the first order to the sign: done, PRs #1088 to #1096;
- #1076 and #1097: open.

**To settle:**

- the four setters keeping measured values, with a test per setter in
  `Shared.Tests/ModelsTests.fs`: done;
- birthdate and name in the EHR data, the age computed from it on the server, and the identity in
  the title bar: done;
- a measured value kept over the EHR data, reversing the earlier rule that a hand edit over EHR
  data is not kept: done;
- where the estimate is computed: done; the normal-value tables are a server resource, the server
  estimates for launched and MCP-host patients, and the client still estimates in anonymous mode;
- the department visible: done, a `PickField` in the panel with its default from one resource;
- the weight-only and height-only minimum: open in #718, no longer a member;
- whether the panel closes on a timer at all: done, it does not.

**Uses** C2 `PickField`, C6 `Notice`, C9 `Disclosure`.

### G6 — The order plan (#987)

Planned in [987-the-order-plan.md](987-the-order-plan.md); built in PRs #1178 to #1187, closed
2026-09-29.

| Issue | Milestone | Status |
|---|---|---|
| #399 | post-MVP | done: a stepable plan cell opens a quantity field on a click, PR #1187; closed 2026-09-29 |
| #510 | M3 | open, left the group: the nurse's preparation view, item 3.5 of M3 in the gap overview |
| **+**#648 | none | done: the sign dialog lists what is new, changed or removed, PR #1181; closed 2026-09-29 |

**Cause.** Two readings of the same plan table, and a third that left the group.

- #399: `Views/OrderPlan.fs` renders every plan cell as text, and the order is changed only in
  the dialog that a row click opens. Every row already carries the value sets the order dialog
  steps with, and `OrderPlanCommand.Navigate` steps a context by its id.
- #648: the sign dialog lists every order of the plan. `HeldContextPolicy.changed` (#1090) gives
  the contexts new or changed since the order plan version was last opened or signed; the
  removed ones have to come from the same comparison.
- #510: a nurse's view (how do I prepare this?). It builds on one preparation document model in
  M3's pharmacy-notification work, so it left G6.

**Decision** (2026-09-29): no adjust button. A cell that holds a stepable order variable opens
a quantity field in a popover on a click, or on a tap in the card layout, and a click on the row
still opens the dialog. The sign dialog lists the differences, each marked new, changed or
removed, and says so when there are none; signing a plan as it is stays allowed. An order context
in the plan changes only in its frequency, orderable dose quantity and orderable dose rate, and
only while the patient data its doses rest on, age aside, match the plan's; otherwise it is
locked.

**Uses** C1 `QuantityField`.

### G7 — Nutrition and TPN (#988)

| Issue | Milestone | Status |
|---|---|---|
| #495 | M4 | open |
| #504 | M4 | open: Figma design linked on the issue |
| #506 | post-MVP | open |

**Cause.**

- #495: `Views/Nutrition.fs` builds its slots on the same machinery as the prescribing workbench
  but has no confirm step. A nutrition context is in the plan from the moment it is added, so the
  user never learns they are done.
- #504: a page redesign.
- #506 (custom enteral feeds, two feeds at once): a data question first. The categories are
  literals in `ServerApi.NutritionRuleSets.fs` (`enteralFeeding`, `enteralSupplement`, and
  others), which the gap overview already wants moved into configuration. (The gap overview's
  verification line still names `ServerApi.Services.fs`, which no longer holds them.)

**To settle:**

- what a confirm step means when the context is already in the plan;
- the redesign checked against the current slot model;
- free-text and second-feed support as a configuration change before a UI change.

**Uses** C1 `QuantityField`, C2 `PickField`, C5 `ValueChip`, C7 `ActionBar`, C9 `Disclosure`.

### G8 — Layout, hierarchy and navigation (#989)

| Issue | Milestone | Status |
|---|---|---|
| #393 | post-MVP | open |
| #404 | none | done: the button convention, C7 PR #1005 and C8 PR #1006 |
| #500 | none | closed 2026-09-26 as a duplicate of #496 (G3) |
| #396 | post-MVP | open |
| **+**#915 | none | open |
| **+**#979 | none | done: ADR-0009, closed 2026-09-24 |

**Cause.** These are the cross-cutting design issues, which had no shared home. The repository's
documentation had no design-system or UX-guidelines document. The three UX rules contributors are
pointed at (safe by default; efficient; the UX follows the user) existed only in a GitHub
discussion (*UX design: 3 basic rules*, unanswered since 2026-08-21; *UI and UX design* is the
wider thread).

Issue #915 belongs here because four pages that are reachable from the menu but not linked from
anywhere raise the same navigation question as #396's reorganisation.

**Decision** (#979, landed as [ADR-0009](../adr/0009-ux-design-rules.md)): the rules are an ADR.
They are ranked and every group's plan cites them. Reversing one after the interface is built
means rebuilding it, and when two rules conflict the ranking decides which gives way; recording
that is what an ADR is for. The decisions taken under the rules (the button convention of G0, the
in-place reason of G4, the field list of G3, the patient modes of G5) live in their issues and in
this index. The ADR listed them as examples until 2026-09-26, when its section 4 was removed: an
ADR states rules, and a decision under them belongs where it is taken.

**To settle:**

- the two-column selection layout of #393;
- the call-to-action convention (#404, decided under G0) applied once in `ViewHelpers` rather
  than per page: done;
- the menu order and what each role sees (#396, related to #580's scope switch); #396's request
  to rename the treatment plan was already refused on the issue;
- page codes and the three-letter parameter scheme (#915).

**Uses** C7 `ActionBar`, C10 `SectionHeading`, F2 theme palette.

### G9 — Beyond the UI (#990)

| Issue | Milestone | Status |
|---|---|---|
| #507 | M5 | open |
| #508 | post-MVP | open |
| #509 | post-MVP | open |

**Cause.**

- #507: a medication offers two identical scenarios (adrenaline and noradrenaline for some
  patients; slightly different scenarios for others). The duplicate is in what GenORDER produces:
  visible in the UI, caused before it.
- #508: a different dose in the morning and the afternoon needs a `Schedule` that varies within a
  day.
- #509: a bolus by hand versus by pump is a dose-type and administration distinction. The thread
  has one scenario (NaCl), and the maintainer has asked for more before deciding.

**To settle:**

- reproduce #507 against the scenarios in `tests/Informedica.GenORDER.Tests`, and decide whether
  the duplicate is removed in scenario generation or in the contract;
- for #508 and #509, what must be decided before either can be planned.

This group is mostly a record of what is not yet known, which is why it is kept separate.

**Uses** nothing from G0; it is not a UI group.

### G10 — The browser tab: continuity and staleness (#991)

| Issue | Milestone | Status |
|---|---|---|
| **+**#518 | M2 (GitHub: the UI and UX milestone) | open: a decision before a build |
| **+**#682 | none | open |

**Cause.** Both concern a browser tab that outlives what it was connected to.

- #518, rewritten on 2026-09-23, asks for a decision before an implementation. A relaunch in the
  *same* browser profile does not end the old tab: the session cookie is shared, and the old tab
  continues on the new Session. A `BroadcastChannel` hand-over, which nothing implements today,
  would only save a tab switch there. A relaunch in another browser or profile does lose the work,
  and no same-origin channel can reach it; putting new or changed orders on the server before they
  are signed is ruled out. The issue offers two options: (a) build the hand-over for the same
  profile and amend the session-ends use case to say so, or (b) close the issue and let the
  leave-page guard be the answer.
- #682: a tab left open across a deployment keeps the old bundle, sends the old commands, and
  shows the general error without saying that a reload is needed. `ServerSettings` carries the
  language, the demo flag and the departments today, and no build version.

`UnsignedWorkPolicy` (the leave-page guard) already defines what work would be lost, and both
issues need exactly that rule. Since PR #1086 a Session left idle for an hour ends, which is the
other way a tab is left behind (#825).

**To settle:**

- (a) or (b) for #518; under (a), the four-message handshake and its same-user, same-patient guard
  that the issue already specifies, with the part that runs without a browser in `Client.Core`;
- a build version on `ServerSettings` and on every reply, with a one-time notice offering a
  reload;
- the shared rule that neither may silently discard unsigned work.

**Uses** C6 `Notice`.

## Small UI bugs outside the groups

Six open bugs, found on 2026-09-29, belong to no group and need no design decision first. Each is
low or medium effort. They are planned in pairs by the code they touch, not under an umbrella:

| Plan | Issues | What |
|---|---|---|
| [1191-the-plan-buttons-and-the-totals-width.md](1191-the-plan-buttons-and-the-totals-width.md) | #1191, #1138 | The plan table jumps on a selection; the totals bar is narrower than the page |
| [1188-texts-through-the-terms.md](1188-texts-through-the-terms.md) | #1188, #1117 | A lowercase plan header; Dutch hover texts on the step buttons |
| [1141-the-error-banner-and-the-float-parse.md](1141-the-error-banner-and-the-float-parse.md) | #1141, #1176 | A server error banner that outlives the error; the browser's float parse |

Left out of this set: #398 and #1034, the clear cross that seems to do nothing, wait on the rule
#1034 asks for; #1136 touches every page; #1155 is a GenORDER and server change; #507 sits in G9;
#502 waits on a design.

## What the grouping surfaced

- **Milestones.** 26 of the 42 had no milestone when the groups were filed; the milestone column
  of each table shows which. Five of them came from the user tests.
- **Two pairs were near-duplicates**, both closed on 2026-09-26: #403 into #501 (the same inability
  to re-pick without clearing first) and #500 into #496 (the same request, once for one field and
  once for the whole dialog).
- **#478 and #911 are the same dead end**, reported by two people, one from the emergency list and
  one from the infusion-pump list.
- **#487 was closed by argument, reopened, and then built.** The maintainer closed it because a
  multi-select cannot close on a pick; the reporter reopened it ("nobody knew it was a
  multi-select") with a proposal for checkbox rows and a close control. The checkbox rows are C2
  and the single filter is C11; no separate close control was added.
- **The three UX rules existed only in a GitHub discussion.** All ten groups need them, and none
  could cite them from the repository until #979 added them as
  [ADR-0009](../adr/0009-ux-design-rules.md).
- **The client's pure logic is tested; its rendering is not.** `Client.Core.Tests` (Expecto tests
  over the client's state machines and policies) and `Shared.Tests` exist. #598 still asks for
  tests of the React components, the JSX and the hooks; its thread mentions a `Fable.Mocha`
  reference and commented-out `WatchTests` wiring from an earlier, abandoned attempt. So a visual
  change is checked by hand in the browser, and a rule moved into `Client.Core` or `Shared` is
  tested there. That is the argument for moving rules there in G1, G4, G5, G6 and G10, and what
  G0, G1 and G5 did.
- **The only TPN use case is a PDF** among the scenarios, referenced by no plan, while #504
  redesigns that page.
- **Terminology needs one decision, not one per issue.** #394 asked for *Verwijder* → *Reset*,
  which landed as one localized term; #396 asks to rename the treatment plan, which was refused
  on the issue as needing investigation.

## Left out on purpose

Each of these is related to a group but is not a member. Each is named as related in that group's
plan when it is written.

| Issue | Why not a member |
|---|---|
| #826 | Labelled `ux`, but it is a bound on server clock skew |
| #599 | Launch token in the browser history: security, not design |
| #598 | Client testing: cross-cutting; cited by G1, G5 and G10 |
| #580 | Scope switch: related to #396 in G8 |
| #672, #720 | Domain follow-ups of the patient-minimum plan; related to G5 |
| #719 | GenFORM tests for a missing patient value; related to G5 |
| #439, #582 | Domain and data, with no UI of their own. #439 was closed by G5's plan, PR #1058 |
| #825 | Session lifetimes, the other way a session ends; #518 names it as where carrying work over matters most; related to G10. The idle lifetime landed in PR #1086 |

## What this leads to

The ADR with the three UX rules landed first, because every plan cites it
([ADR-0009](../adr/0009-ux-design-rules.md), from #979). The eleven umbrella issues followed on
2026-09-24. Each carries its group's cause, what it uses from G0 and what it has to settle, and
its members are sub-issues, so each member keeps its number, author, labels and milestone.

Each umbrella gets one implementation plan, on the current plan template, named
`<umbrella>-<title>.md` next to this file. Groups with a known cause come first; groups waiting
on a design come last:

1. G0, [981-ux-foundation-and-common-components.md](981-ux-foundation-and-common-components.md):
   built, closed 2026-09-25;
2. G5, [986-the-patient-what-is-entered-estimated-hidden.md](986-the-patient-what-is-entered-estimated-hidden.md)
   and [976-two-patient-modes.md](976-two-patient-modes.md): built, closed 2026-09-28;
3. G1, [982-one-dropdown-one-set-of-controls-one-state.md](982-one-dropdown-one-set-of-controls-one-state.md):
   built, closed 2026-09-25;
4. G4, [985-what-the-rules-allow-and-deviating-from-it.md](985-what-the-rules-allow-and-deviating-from-it.md):
   built, closed 2026-09-28;
5. G6, [987-the-order-plan.md](987-the-order-plan.md): built, closed 2026-09-29;
6. G7;
7. G2;
8. G10, whose plan opens with the (a)/(b) decision of #518, so it may end as a closing note
   rather than a build;
9. G3, [984-the-dose-dialog.md](984-the-dose-dialog.md): built, closed 2026-09-28;
10. G8;
11. G9, whose plan may end as a set of questions.
