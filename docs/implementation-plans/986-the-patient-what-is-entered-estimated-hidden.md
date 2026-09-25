# Implementation plan for issue #986

G5 of the ten UI/UX groups: the patient panel, what the user enters there, what the system
estimates for them, and what it decides behind their back. Companion to
[the grouping index](ux-issue-grouping.md) and to
[the foundation plan](981-ux-foundation-and-common-components.md), whose components this group
spends.

Held to [ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md). The patient is the input
every dose is computed from, so rule 1 governs this group more than any other: a datum the user
entered is never replaced by one the system guessed, and a datum that filters the rules is never
set without the user seeing it.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [What the foundation already settled](#what-the-foundation-already-settled)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [To settle in review](#to-settle-in-review)
- [Related, not a member](#related-not-a-member)

## Problem description

Five open issues on one panel, and one domain issue on what a patient is matched against.

- **#488** A weight the user typed is replaced by the estimate when they then type an age, with
  no word about it. Dosing is computed from weight. This is the highest-safety item of the
  forty-two reported.
- **#716** Only the client estimates a weight and a height from an age, so a patient arriving
  from the launch or from the MCP host with an age alone is refused where the same patient typed
  into the web client is accepted.
- **#717** A missing department defaults to `ICK` in the server's mapper and in the MCP host.
  The department selects dose rules and the panel has no field for it, so every web patient is
  filtered as an ICK patient without the user seeing it.
- **#718** A measured weight alone, or a measured height alone, is not yet a patient: deferred
  from the patient-minimum work because no estimate of one from the other exists.
- **#976** The panel behaves the same whoever the patient is. A launch hands the client the
  platform's reading, and the panel then offers that patient's age as a field to type over, does
  not say who the patient is, and drops a bedside weight at the next open.
- **#439** A dose rule may say it applies to adults without giving an age range. The facet
  exists in the type and in the parser; nothing matches on it, so such a rule would apply to
  every patient of every age. The extraction pipeline is gated shut because of it.

Issue #489, the panel that closed itself after five seconds, is already fixed: the `Disclosure`
component of G0 opens and closes when the user says so and never by itself. It stays a member of
the group on the index and needs nothing here.

## What the code does today

Read before planning, and worth stating, because three of the six are smaller than the umbrella
issue suggests and one is larger.

- **The setters.** `Shared/Models.fs` builds a patient through `Patient.create`, which takes the
  age parts, a weight, a height, the gestational age, and the rest, and writes the weight and
  height it is given as **measured**, with the estimates blank. `setYear` and `setMonth` pass
  `None` for the weight, the height and both gestational-age parts; `setWeek` and `setDay` pass
  `None` for the weight and the height and keep the gestational age. So every age edit drops
  what was measured, and `getWeight`, which falls back to the estimate, shows the estimate in
  its place. That is #488 exactly, and it is a handful of arguments.
- **The estimate.** `Patient.applyNormalValues` is already in `GenPRES.Shared`, a pure function
  over four tables of normal values. Only the tables are client-side: `App.fs` fetches them with
  the emergency-list sheet and applies the function itself. So #716 is a resource and a call
  site, not an algorithm to write or to move.
- **The gate.** `ServerApi.Mappers.Patient.fs` refuses a patient with no weight and height,
  measured or estimated, and names both. The refusal is deliberate and temporary: its comment
  says "until the server estimates". Step 5 is what that sentence waits for.
- **The department.** `ServerApi.Mappers.Order.fs` writes `pat.Department |> Option.defaultValue
  "ICK"`, and the MCP host's `buildPatient` does the same. The client sets a department only from
  the url's `dp` parameter. The panel has no field.
- **The reading.** `PatientDataPort` is `read: string -> Patient option`: what the platform
  hands over is a patient record, with an age in years, months, weeks and days, and no name and
  no birthdate. The stub returns ten years, 32 kg, 140 cm. So the identified mode of #976 needs
  a different thing read, not merely a different thing shown.
- **The panel's own reset.** `Views/Patient.fs` ends in a full-width text button labelled
  *Verwijder* that empties the whole patient. That is the shape #394 complained about on the
  prescribing page, on a control that discards more.
- **IsAdult.** `GenFORM.Lib/Types.fs` already has `type Age = AbsoluteAge of MinMax | IsAdult`,
  `DoseRuleData.fs` parses the column and writes it back, and `Export.fs` masks the age bounds
  when it is set. What is missing is one function: `GenFORM.Lib/Patient.fs`'s `getAge` answers
  `MinMax.empty` for `IsAdult`, which the age filter reads as *no age restriction*, so an
  adult-only rule matches a newborn. A `TODO` says so and names the consequence: no such row may
  reach ingest until this is enforced.

## What the foundation already settled

- **C9 `Disclosure`** closed #489 and is what the panel is drawn in.
- **C7 `ActionBar`** and **C8 `ConfirmDialog`** are what the panel's reset becomes.
- **C2 `PickField`** is the department field, if the department becomes one.
- **C6 `Notice`** is how the page says a value is estimated rather than measured, and what is
  missing when a draft is no patient yet.

## Approaches considered

**What a setter keeps.**

- *Each setter keeps everything but its own field.* **Chosen.** One rule, six setters, provable.
- *The panel re-applies the measured values after each setter.* Rejected: the rule would live in
  the view, and the server and the MCP host call the same setters.
- *Warn that the estimate differs from what was measured.* This is what the issue suggests as a
  second thought and it is worth having, but it is a different feature: first the value is not
  lost, then a value that looks wrong for the age can be remarked on. Kept out of this group.

**Where the estimate is computed.**

- *The tables as a server resource, the existing `Shared` function called at the inbound
  boundary.* **Chosen.** The estimate is one function, on both sides, so the server's answer and
  the panel's display cannot drift.
- *A second estimate on the server.* Rejected outright: two estimates of a dosing weight.
- *Requiring a measured weight and height on the platform and MCP paths.* Rejected in the issue,
  and rightly: the platform reads what the record holds, and an age is a patient by the domain
  rule.

**What a missing department means.**

- *No department bound: rules that name no department apply, rules that name one do not.*
  **Chosen** as the meaning, because it is the only reading that does not invent a filter the
  user did not set. The branch that skips the patient filter altogether already exists in
  `GenFORM.Lib/DoseRule.fs` and is unreachable from the web client today.
- *The department as part of the minimum, with a field in the panel.* Not rejected, deferred: it
  needs a list of departments from somewhere, and a hospital's list is configuration. The panel
  shows the department in force, and where it came from; making it choosable is a follow-up once
  the list has a home.
- *Keeping the default.* Rejected: it encodes one hospital in code and hides a filter input.

**The two modes.**

The decision is #976's and is recorded in ADR-0009: identified by the launch, or anonymous.
What this plan adds is the order, and one judgement. The reading has to carry a birthdate and a
name, which changes the platform port, the stub, and the session that opens on it — that is the
larger half of this group and it lands after the safety fixes, not before them.

**The panel's reset.** Bounded, on an action bar, named with the same localized term as the
prescribing page's, and **with a confirmation**, unlike that one. The prescribing filter is
rebuilt by picking again; the patient is not: an age, a weight, a height and a gestational age
typed at the bedside are gone. Under rule 1 that is the case a confirmation is for. This group
therefore spends `ConfirmDialog` where G1 decided it did not.

**IsAdult.** Two questions, one of them a decision rather than a design.

- *What an adult is.* A single threshold, stated once in the domain and used by the matcher and
  by the printing. Eighteen years is the obvious candidate and it is the reviewer's to confirm.
- *A patient with no age.* An adult-only rule does not match them. Rejecting is the safe branch:
  the facet asserts something positive about the patient, and nothing is known.

## Chosen approach

- Every patient setter writes its own field and keeps every other **measured** value; the
  estimates are recomputed from the tables, never carried across an edit.
- The normal-value tables become a server resource and the server applies the same `Shared`
  estimate at the inbound boundary; the refusal of a patient with an age but no weight and
  height goes with it.
- No department means no department bound. The default leaves the mapper and the MCP host, and
  the panel shows which department is in force and where it came from.
- A dose rule that says *adults* matches a patient at or over the adult threshold and no other,
  a patient with no age included.
- The panel's reset becomes a bounded secondary action, confirmed.
- The patient panel then gets its two modes: the platform's reading carries an identity and a
  birthdate, the server computes the age from it on each request, the title bar shows who the
  patient is, the age is not a field in that mode, and a measured value entered over the
  reading is kept and recorded by the next signature.
- A measured weight alone, or a measured height alone, is the last step and depends on a table
  that does not exist yet.

## Confidence

High for steps 1 to 8: each is a rule that can be stated in a script and proved before anything
is migrated, and three of them are the removal of something rather than an addition.

Medium for the two modes. They reach the launch, the platform port, the session that opens on
it, the title bar and the signature's report of changed data, and they reverse a rule that was
deliberate: today a hand edit over a reading is not kept. Each step is small; the sequence is
not, and it is the part most likely to come back from the browser.

Low for the last step, and stated as such: an estimate of a height from a weight, by sex, is a
data question before it is a code question. If no table is at hand it is deferred to its own
issue rather than guessed at.

## Steps

One pull request each, in this order. Everything outside `src/Informedica.GenPRES.Client/` is a
script first and a migration after, so the domain and server steps are two pull requests.

1. **What a setter keeps, as a script.** A script in `src/Informedica.GenPRES.Shared/Scripts/`
   shadows the `Patient` module, states the rule and proves it with Expecto: an age entered
   after a weight keeps the weight; after a height keeps the height; a gestational-age edit
   keeps both; a weight edit keeps the age and the gestational age; each setter keeps the
   gender, the access devices, the renal function and the department; and the estimates are
   blank after every setter, so that the panel's next `applyNormalValues` is what fills them.
2. **The setters migrated**, with the tests in `tests/Informedica.GenPRES.Shared.Tests/`.
   Closes #488.
3. **The panel's reset, bounded and confirmed.** `Views/Patient.fs`'s full-width *Verwijder*
   becomes an `ActionBar` with the localized `Reset` term and a `ConfirmDialog` naming what is
   discarded.
4. **What no department means, as a script.** A script over `GenFORM.Lib` proving that a patient
   with no department matches the rules that name none and not the rules that name one, and
   that today's default silently narrows the same patient to one hospital's rules.
5. **The default removed**, from `ServerApi.Mappers.Order.fs` and the MCP host's `buildPatient`,
   with the panel showing the department in force and where it came from. Closes #717.
6. **The estimate on the server, as a script.** The normal-value tables as a loader in the
   resource registry, and `Shared`'s `applyNormalValues` applied to a patient read from the
   platform or built by the MCP host, proved to give the same weight and height the client shows
   for the same age and sex.
7. **The estimate migrated**, and the refusal of a patient with an age but no weight and height
   lifted in `ServerApi.Mappers.Patient.fs`. Closes #716.
8. **Adults, as a script and then migrated.** The adult threshold stated once, `getAge` no
   longer answering an empty range for `IsAdult`, the age filter matching adults only, a patient
   with no age not matching, and the rule printed as *adults* rather than as no age bound. The
   `TODO` in `GenFORM.Lib/Patient.fs` and the gate on the extraction boundary go with it. Two
   pull requests. Closes #439.
9. **The reading carries an identity, as a script and then migrated.** `PatientDataPort` answers
   a reading with an id, a name and a birthdate beside the data; the stub platform gives one;
   the session stores what it opened on. Two pull requests.
10. **The age computed from the birthdate**, on the server, when the session opens and on each
    request, so that a neonate's age is right on the day the request is made.
11. **The title bar identifies the patient**: id, name, birthdate, in the identified mode only.
12. **The panel in the identified mode**: the age shown and not editable, the weight, the height
    and the gestational age editable, and the department from the launch.
13. **A measured value over a reading is kept**, and the next signature records that the data
    changed. This reverses the present rule and is the step to read carefully. Closes #976.
14. **A weight alone, or a height alone**, as the minimum: the estimate of one from the other,
    by sex, applied like the age-based estimate, and the panel saying which value is estimated.
    Closes #718, or is deferred to its own issue if the table is not to hand.

## Verification, per step

- Every script step: `dotnet fsi` on the script, with an explicit check that the Expecto run
  reports `Status: Ok`, and the script left in the repository as the record of what was proved.
- Every migration step: `dotnet run Build`, `dotnet run ServerTests` and
  `dotnet fsi scripts/CheckDependencyRule.fsx`, each checked for its success line rather than
  read by eye.
- Every client step: `dotnet run ClientBuild`, the generated JSX read for the element that
  changed, and then the browser, by the user. Steps 3, 11, 12 and 13 need the browser before
  they are done.
- Steps 9 to 13 are also walked through by hand with the stub launch, both identities: launched
  with a patient, and launched with `no-data`, which stays anonymous.

## To settle in review

- The adult threshold, and whether an adult-only rule and an age-bounded rule may both match one
  patient.
- Whether the department should become a choosable field, and where a hospital's list of
  departments would live.
- Whether the panel should remark on a measured weight that is far from the estimate for the
  age, as #488 suggests as a second thought.
- Whether the estimate should stay client-side for the panel once the server computes one, or
  whether the panel should show the server's.
- What the title bar shows when the platform has a name the user should not see on a shared
  screen.
- Whether step 14 is in this group at all.

## Related, not a member

- **#672**, **#720** — domain follow-ups of the patient-minimum work.
- **#719** — GenFORM tests for a missing patient datum.
- **#598** — client testing, cited by this group and by G1 and G10.
- **#394** — the same control shape as step 3, on the prescribing page, closed by G1.
