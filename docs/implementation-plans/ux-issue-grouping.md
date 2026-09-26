# UI/UX issues grouped by cause

The index the per-group UI/UX plans hang off. It groups the open UI/UX issues by **what code has
to change**, so that each group is one umbrella issue with one implementation plan and a coherent
set of pull requests, rather than one plan per reported symptom. The eleven umbrella issues were
filed from it on 2026-09-24: [#981](https://github.com/informedica/GenPRES/issues/981) (G0) and
[#982](https://github.com/informedica/GenPRES/issues/982) to
[#991](https://github.com/informedica/GenPRES/issues/991) (G1 to G10), each with its members
reparented as sub-issues.

It carries no issue number of its own because it is not a plan for one issue; the per-group plans
that do are named after their umbrella.

Companion to [the MVP gap overview](../roadmap/mvpap2019-gap-overview.md)'s milestone view, which
groups the same issues by **milestone**. Where the two disagree, the gap overview decides the
*when* and this document the *what*. Every group is held to
[ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md).

- [Why group them](#why-group-them)
- [G0 — what the groups share](#g0--what-the-groups-share-981)
- [The ten groups](#the-ten-groups)
- [What the grouping surfaced](#what-the-grouping-surfaced)
- [Left out on purpose](#left-out-on-purpose)
- [What this leads to](#what-this-leads-to)

## Why group them

The UX designer filed 32 issues, 29 of them still open; they are the whole of the *UI and UX
update* milestone bar one. They arrived one observation at a time, so they are filed one
issue at a time: five separate issues are the same dropdown component, two are the same
`create` call in `Shared/Models.fs`, four are the same DataGrid toolbar. Planned individually,
each would be a pull request touching a file another pull request is already touching, and
three of them would be planned against code that is not the cause.

Thirteen further open issues — from the maintainer and from the clinical tester — are the same
work with a different perspective: the same emergency-list dead end (#478, #911, and #977 filed from this document), the same patient panel (#716, #717, #718, and #976 filed from this document),
the same dose dialog (#978, filed from this document), the same sign dialog (#648), the same navigation (#915, and the UX rules ADR #979 filed from
this document), the same tab (#518, #682). Leaving
them out would mean planning two halves of one change separately.

42 issues in scope: the designer's 29, plus the thirteen marked **+** below, four of which were
filed from this document (#976, #977, #978, #979).

Figma-gated issues (#394, #402, #405, #496, #500, #502, #504) are grouped on the mechanism —
where the component lives, what has to become a parameter — with the visual decision recorded
as an input still to arrive. #504 carries a Figma link on the issue; the rest do not yet.

## G0 — what the groups share (#981)

The ten groups below are not independent: one control serves #405, #398, #397 and #504; one
severity treatment (how UI handles severity) serves #402, #496 and #478; one action bar serves #404, #394 and #495. Built
group by group, each would be invented separately and the interface would end less consistent
than it started.

**G0** is the eleventh group and the first: the foundation and the common components the other
ten spend. It carries no reported issue of its own — it exists because ten of them overlap — and
each group below names what it spends. The catalogue, with the evidence, the prop shapes and the
call sites each component replaces, is in
[G0's plan](981-ux-foundation-and-common-components.md); the component list itself is on #981.

In short: four foundation items — **F1** one severity type and one renderer, **F2** a theme
palette, **F3** a label convention for shared components, **F4** delete the 328 dead lines and
fix the two defects they hide — and then twelve components: **C1** `QuantityField`, **C2**
`PickField` / `MultiPickField`, **C3** `SearchField`, **C4** `SeverityMark`, **C5** `ValueChip`,
**C6** `Notice`, **C7** `ActionBar` / `ActionButton`, **C8** `ConfirmDialog` / `DialogShell`,
**C9** `Disclosure`, **C10** `SectionHeading`, **C11** `ListToolbar`, **C12** `PrintTable`.

The foundation lands before the components, and G0 before the ten.

One question it raised: #404 says the call to action (the button that completes the task: OK,
submit, sign) goes on the right, while the TPN frames put the contained button on the left of
the outlined one, and #394's own proposal puts the reset at the bottom left of the selection
fields. The convention had to be stated as prominence or as position, not both.

*Decision*: the call to action is the prominent one, and it goes on the right; the reset is
secondary, and it goes on the left. C7 applies it; the TPN frames follow it. Recorded in
[ADR-0009](../adr/0009-ux-design-rules.md).

## The ten groups

Each group names the G0 components it spends. Milestones are the gap overview's (M2 to M5,
post-MVP); *unmilestoned* means the issue is in neither the gap overview nor a GitHub milestone.

### G1 — One dropdown, one set of controls, one state (#982)

**Members** #498, #403, #501, #398, #394 · #498 #398 #394 post-MVP; #403 #501 unmilestoned.

One component causes all five. `Components/SimpleSelect.fs` gives a clear cross **or** a
stepper, never both (the `endAdornment` binding) — that is #398: a field with a stepper has no
cross, and a field that does show one reaches `ViewHelpers.orderSelect`'s
`updateSelected = if isEmpty then ignore else …`, so the cross is inert.
`ViewHelpers.filterSelect` and `autoComplete` disable on an *empty* list but not on a list of
*one* (#498), while `orderSelect` silently auto-selects a single value and stays enabled.
Re-picking without clearing first (#403, #501) is a cascade question in the `OrderContext`
module of `Shared/Models.fs` (`indicationChange`, `medicationChange`, `routeChange`,
`formChange`, `doseTypeChange`), called from `Prescribe.fs` and `Nutrition.fs` — not a widget
question, and not in `Client.Core`: the machine there only carries the context to the server.
The reset of #394 is `Prescribe.fs`'s `clear()`, dispatching `OrderContext.empty` behind a
full-width text button.

To settle: one `disabled / one-option / clear` rule in `ViewHelpers`; the adornment conflict in
`SimpleSelect`; which fields reset which in `OrderContext`; the reset control's bounds and its
name. The cascade lives in `Shared`, which `Shared.Tests` already reaches, so each reset rule
gets an Expecto test there — and a script first, since `Shared` is not client UI.

**Spends** C2 `PickField` / `MultiPickField`, C1 `QuantityField`, C7 `ActionBar`, C8 `ConfirmDialog`.

### G2 — Finding a medication (#983)

**Members** #503, #487, #502, #400 · #487 post-MVP; the other three unmilestoned.

`Components/ResponsiveTable.fs` renders two independent filters — the custom "Filter"
`MultipleSelect` above the grid and `GridToolbarFilterButton` in the toolbar — which is #502,
and the confusion behind #487. Neither list has a text search, while the user tests report that
searching beats filtering (#503). #400 (brand names) is the same need on the prescribing page
and reaches past the client: the brand sits on the product, not on the generic pick list.

To settle: one search control shared by `EmergencyList.fs`, `ContinuousMeds.fs` and the
prescribing generic field; what of the MUI toolbar stays; making the custom filter read as a
multi-select; brand-name matching in GenFORM.

**Spends** C3 `SearchField`, C11 `ListToolbar`, C2 `MultiPickField`.

### G3 — The dose dialog (#984)

**Members** #397, #402, #405, #496, **+**#978 · #397 #405 post-MVP; #402 #496 #978
unmilestoned. #978 is the umbrella for the field list below.

`Views/Order.fs` decides for itself which fields it shows and in what order: `keer dosis` →
`dosering`, hard-coded per dose type. #397 asks for the reverse, and the reply on the issue
argues that the present order is the calculation order. That is not a question the client
should settle: which fields an order shows, and in what order, is configuration, and the server
sends it back with the order (#978), so the client renders what it is told, in the order it is
told.
The order itself is then settled once, in configuration, and can differ per dose type or per
setting without a client change. Severity reaches the user only as a double
underline in a colour (`SimpleSelect`'s `warning` styling): no reason, no icon (#402). The outer two
of the five stepper buttons carry two meanings, min/max while the variable is still navigable
and large step once it is solved (`ViewHelpers.createStepper`'s `first` and `last`), which is
exactly #405's finding. #496 — which field do I change to move the dose? — is
information the solver already has and does not surface.

To settle: the shape of the field list the server sends (which order variables, in what
order, which of them the user may change) and where it is configured; the client rendering
from that list instead of from its own case per dose type; a hover reason for a warning, composed from what
the wire already carries — every `OrderVariable` arrives with its `Level` (four values, no
text) and its `DefinedConstraints` and `CalculatedConstraints`, so which bound was crossed and
by how much can be said without a new server field; splitting min/max from big-step, plus
double and halve; "this field is what moves the dose" (#496) as part of the same list, the
server marking the field it wants the user to start from.

**Spends** C1 `QuantityField`, C4 `SeverityMark`, C10 `SectionHeading`, C5 `ValueChip`.

### G4 — What the rules allow, and deviating from it (#985)

**Members** #499, #505, **+**#478, **+**#911, **+**#977 · #499 #505 #478 in M4; #911 #977
unmilestoned. #977 is the umbrella for the decision below.

Two directions of one thing. The rules say nothing can be prescribed: #478 — the emergency list
offers a medication with no dose rule and the page says only "geen doseerregels gevonden"; #911
— a prescription impossible under the Kinderformularium (salbutamol for a six-day-old, from the
infusion-pump list) drops the user on the emergency list without saying why. #911 has an exact
cause: `OrderContextWorkbench.noDoseRules` in `Client.Core/OrderContextMachine.fs` matches the
error text "geen doseerregels" and emits `GoToLifeSupport`, which `App.fs` applies as a bare
page switch, the error discarded. #478 is the same error read by the user before the switch.
Or the rules say something the clinician means to exceed: #499 — deviate with an argumentation,
the UX half of fit-gap row 11.4, whose storage half (gap overview row 2.2.7: severity is
recomputed on each solve and never stored) is still missing now that the store of #516 is built;
and #505 — prescribe a range rather than one value, a constraint question in GenORDER rather
than a widget.

*Decision*, filed as #977: when no dose can be shown — no dose rules, or any other reason — the page stays
where it is and says so, with the reason. No page switch. `GoToLifeSupport` goes; the error
the server sends is shown as a `Notice` on the page the user is on, and what the server sends
is the reason, not a marker the client matches on.

To settle: the reason the server sends when the rule set is empty, and whom the user should
contact; the `Client.Core` change that shows it in place, with a test; where a free-text
argumentation is stored on the signed order plan version; the range as an order variable the
user may leave unresolved.

**Spends** C6 `Notice`, C5 `ValueChip`, C4 `SeverityMark`.

### G5 — The patient: what is entered, what is estimated, what is hidden (#986)

**Members** #488, #489, **+**#716, **+**#717, **+**#718, **+**#976 · #488 in M5; #489 post-MVP;
the four others unmilestoned. #976 is the umbrella for the decision below.

Issue #488 has an exact cause. `Shared/Models.fs` `setYear` rebuilds the patient through `create`,
passing `None` for weight, height, gestational-age weeks and gestational-age days. `setMonth`
does the same; `setWeek` and `setDay` wipe weight and height but keep the gestational age. So
entering an age after a weight discards the measured weight, and the display falls back to the
estimate (`getWeight`), which is why it reads as *overwritten* rather than *cleared*. A dosing
input dropped without a word: the highest-safety item of the 42.

The other four are the same panel. #716: only the client estimates, so a patient arriving from
the launch or the MCP host with an age alone is refused where the web client accepts. #717: a
missing department defaults to `ICK` in the mapper and the panel has no department field — a
hidden filter input on every web patient. #718: a weight alone, or a height alone, as the
minimum, deferred from the patient-minimum plan. #489 is the five-second idle auto-collapse in
`Views/Patient.fs`.

*Decision*, filed as #976: the panel has two modes, decided by the launch.

- **Launched with a patient**: there is a specific patient to point at, and the title bar
  identifies them — id, name, birthdate. The age is computed from the birthdate on the server,
  when the Session opens and again on each request, since a neonate's age moves by the day;
  the panel shows it and it cannot be changed. Weight, height and gestational age are
  editable and kept as measured: a bedside value overrides the platform's reading and stays,
  and the next signature records the change. The department comes from the launch, in place
  of the hidden default of #717. Today the reading carries an age in years, months, weeks and
  days and no name, so it has to carry the birthdate and the name instead.
- **Anonymous**, without a specific patient: the panel shows patient data — age, weight and
  the rest — to enter, and behaves as it does today. A launched patient the platform has no
  data for is anonymous too, and so is a patient the MCP host creates from an age.

The setters of #488 therefore still matter, for the anonymous mode and for weight and height
in both; in the launched mode the age is not a setter at all.

To settle: the four setters keeping what was measured, with a test per setter in
`Shared.Tests/ModelsTests.fs`; the birthdate and the name on the platform reading, the age
computed from it on the server, and the title bar showing the identity; a measured value over
a reading kept, which reverses today's rule that a hand edit over a reading is not kept;
where the estimate is computed (`PatientDto.applyNormalValues`,
client only, from tables fetched with the emergency-list sheet), and whether it stays
client-side; making the department visible or unbounded (the default sits in
`ServerApi.Mappers.Order.fs`); the weight-only and height-only minimum; whether the panel
closes on a timer at all. The setters are `GenPRES.Shared`, not client UI, so they go through
a script first.

**Spends** C9 `Disclosure`, C6 `Notice`, C2 `PickField`.

### G6 — The order plan (#987)

**Members** #399, #510, **+**#648 · #510 in M3; #399 post-MVP; #648 unmilestoned.

`Views/OrderPlan.fs` already passes `actions = None` per row into `ResponsiveTable`, which
renders row actions when they are `Some` — but only in its card layout (the narrow screen);
the DataGrid has no actions column. #399 is that hook plus the column. #648 (the sign dialog
should list only what this user changed) is the same table read at signing time.
`PlanWorkPolicy` knows *that* and *how many* commands changed the plan (`PlanWork.Changed of
generation`), not *which* orders, so the material is not there yet: the difference has to come
from the plan's contexts, by id, against the version opened. #510 (a nurse's view: how do I
prepare this) is a third reading of the same plan, already folded into M3's
pharmacy-notification work in the gap overview.

To settle: the adjust action and the columns the plan table needs to carry it; a difference
shown in the sign dialog, computed in `Client.Core` from the contexts against the opened
version rather than from a comparison of plans; the preparation view as a role-shaped
rendering of orders the plan already holds.

**Spends** C8 `ConfirmDialog`, C11 `ListToolbar` (the row `actions` hook), C12 `PrintTable`.

### G7 — Nutrition and TPN (#988)

**Members** #495, #504, #506 · #495 and #504 in M4; #506 post-MVP.

`Views/Nutrition.fs` builds its slots on the same machinery as the prescribing workbench but
has no confirm step — a nutrition context is in the plan from the moment it is added, so the
user never learns they are done (#495). #504 is a page redesign, with a Figma file linked on
the issue. #506 (custom enteral feeds, two feeds at once) is a data question first: the
categories are literals in `ServerApi.NutritionRuleSets.fs` (`enteralFeeding`,
`enteralSupplement`, and the rest), which the gap overview already wants moved into
configuration (row 2.7; its verification line still names `ServerApi.Services.fs`, which no
longer holds them).

To settle: what a confirm step means when the context is already in the plan; the redesign read
against the current slot model; free-text and second-feed support as a configuration change
before a UI one.

**Spends** C1 `QuantityField`, C5 `ValueChip`, C9 `Disclosure`, C7 `ActionBar`, C2 `PickField`.

### G8 — Layout, hierarchy and navigation (#989)

**Members** #393, #404, #500, #396, **+**#915, **+**#979 · #393 #396 post-MVP; the four others
unmilestoned. #979 was the ADR for the three rules, written before any group's plan and closed
on 2026-09-24.

The cross-cutting design issues, which have no shared home today: there is no design-system or
UX-guidelines document anywhere in the repository's documentation, and the three UX rules
contributors are pointed at — safe by default, efficient, the UX follows the user — live only in
a GitHub discussion (*UX design: 3 basic rules*, unanswered since 2026-08-21; *UI and UX design*
is the wider thread) and had never been brought into the repository. *Decision*, filed as #979
and landed as [ADR-0009](../adr/0009-ux-design-rules.md): the rules are an ADR — they are ranked,
every group's plan cites them, and reversing one after the interface is built means rebuilding it,
which is what an ADR records, and what a losing rule gives up when two conflict. The decisions
already taken under the rules (the button convention of G0, the in-place reason of G4, the field
list of G3, the patient modes of G5) stay in their issues and in this index; the ADR carried
them as worked applications until 2026-09-26, when its section 4 was removed, since an ADR
states rules and a decision under them belongs where it is taken.
Issue #915 belongs here because four
pages that can be reached from the menu but not linked to is the same navigation question as #396's
reorganisation.

To settle: the two-column selection
layout of #393; the call-to-action convention (#404, decided under G0: prominent and right, the reset
secondary and left) applied once in `ViewHelpers` rather than per page; the menu order and what a role sees (#396, related to #580's scope switch), noting
that #396's rename of the treatment plan was already refused on the issue; page codes and the
three-letter parameter scheme (#915).

**Spends** C7 `ActionBar`, C10 `SectionHeading`, and F2 the theme palette.

### G9 — Beyond the UI (#990)

**Members** #507, #508, #509 · #507 in M5; #508 and #509 post-MVP.

Issue #507 (a medication offering two identical scenarios — adrenaline and noradrenaline for some
patients, slightly different scenarios for others) is a duplicate in what GenORDER produces,
visible in the UI but caused before it. #508 (a different dose in the morning and the
afternoon) needs a `Schedule` that varies within a day. #509 (a bolus by hand versus a pump) is
a dose-type and administration distinction; the thread holds one scenario (NaCl) and the
maintainer has asked for more before deciding.

To settle: reproduce #507 against the scenarios in `tests/Informedica.GenORDER.Tests` and
decide whether de-duplication belongs in scenario generation or in the contract. For #508 and #509,
what has to be decided before either can be planned at all. This group is mostly a record
of what is not yet known, which is why it is kept apart.

**Spends** none — it is not a UI group.

### G10 — The tab: continuity and staleness (#991)

**Members** **+**#518, **+**#682 · #518 in M2 (GitHub: the UI and UX milestone); #682
unmilestoned.

Both concern a browser tab that outlives the thing it was talking to. #518, rewritten on
2026-09-23, asks for a decision before an implementation: a relaunch in the *same* profile does
not end the old tab (the session cookie is shared, the old tab continues on the new Session),
so a `BroadcastChannel` hand-over — which nothing implements today — would only save a tab
switch there; the relaunch that does lose the work, in another browser or profile, is beyond
any same-origin channel, and putting unsigned work on the server is ruled out. The issue
offers (a) build the same-profile half and amend the session-ends use case to say so, or (b) close it and let the
leave-page guard be the answer. #682: a tab open across a deployment keeps the previous
bundle, sends the previous commands, and shows the general error with nothing saying that a
reload is what is needed. `UnsignedWorkPolicy` (the leave-page guard) already defines what work that would
be lost means, and both issues need exactly that predicate; `ServerSettings` today carries
only the language and the demo flag, no build version.

To settle: (a) or (b) for #518 — and under (a) the four-message handshake and its same-user,
same-patient guard the issue already specifies, its pure half in `Client.Core`; a build version
on `ServerSettings` and every reply, with a one-time notice offering the reload; and the shared
rule that neither may discard unsigned work silently.

**Spends** C6 `Notice`.

## What the grouping surfaced

- **The count**: 32 issues from the UX designer, **29 open**.
- **26 of the 42 are unmilestoned** — #400, #402, #403, #404, #478, #496, #500, #501, #502,
  #503, #505, #506, #508, #509, #510, #648, #682, #716, #717, #718, #911, #915, #976, #977,
  #978, #979. Five of them came out of the user tests.
- **Two pairs are near-duplicates**: #403 ≈ #501 (the same inability to re-pick without
  clearing first); #500 ≈ #496 (the same request at two altitudes). Candidates to close as
  duplicates rather than plan twice.
- **#478 and #911 are the same dead end** reported by two people, one from the emergency list
  and one from the infusion-pump list.
- **#487 was closed by argument and reopened**: the maintainer closed it — it is a
  multi-select, so it cannot close on a pick — and the reporter reopened it with "nobody knew
  it was a multi-select" and a proposal: checkbox rows and a close control. That proposal is
  the G2 design input.
- **The three UX rules lived only in a GitHub discussion.** All ten groups need them and none
  could cite them from the repository, until #979 landed them as
  [ADR-0009](../adr/0009-ux-design-rules.md).
- **The client's pure logic is tested; its rendering is not.** `Client.Core.Tests` (228 Expecto
  tests over the lane machines and the policies) and `Shared.Tests` exist; what #598 still asks
  for is the browser half — React components, JSX, hooks — and its thread notes a `Fable.Mocha`
  reference and commented-out `WatchTests` wiring from an earlier, abandoned attempt. So a
  visual change is verified by hand in the browser, and a rule pushed into `Client.Core` or
  `Shared` is not — the argument for doing that in G1, G4, G5, G6 and G10.
- **The only TPN use case is a PDF** among the scenarios, referenced by no plan, while #504
  redesigns that page.
- **Terminology needs one decision, not one per issue**: #394 asks *Verwijder* → *Reset*, #396
  asks to rename the treatment plan — refused on the issue as needing investigation.

## Left out on purpose

Each is related to a group but is not a member of one, and is named as related in that group's
plan when it is written.

| Issue | Why not a member |
|---|---|
| #826 | Labelled `ux`, but it is a bound on server clock skew |
| #599 | Launch token in the browser history: security, not design |
| #598 | Client testing: cross-cutting; cited by G1, G5 and G10 |
| #580 | Scope switch: related to #396 in G8 |
| #672, #720 | Domain follow-ups of the patient-minimum plan; related to G5 |
| #719 | GenFORM tests for a patient value that is missing; related to G5 |
| #439, #582 | Domain and data; no UI surface of their own |
| #825 | Session idle and absolute lifetimes: the other way a session ends; named by #518 as the case where a carry-over would matter most, related to G10 |

## What this leads to

The ADR holding the three UX rules landed first, because every plan cites it
([ADR-0009](../adr/0009-ux-design-rules.md), from #979). The eleven umbrella issues followed on
2026-09-24 — #981 for G0, #982 to #991 for G1 to G10 — each carrying its group's cause, what it
spends from G0 and what it has to settle, with its members reparented as sub-issues so that a
member keeps its number, author, labels and milestone.

What is left is one implementation plan per umbrella issue, on the plan template as the recent
plans extend it, named `<umbrella>-<title>.md` beside this file. G0's is written:
[981-ux-foundation-and-common-components.md](981-ux-foundation-and-common-components.md).

Order of writing: G0 first, since ten groups spend it, then G5, G1, G4, G6, G7, G2, G10, G3, G8,
G9 — cause-known first, design-gated last. G10's plan opens with the (a)/(b) decision of #518, so
it may end as a closing note rather than a build. G9's may end as a set of questions.

