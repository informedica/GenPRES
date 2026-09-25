# Implementation plan for issue #986

G5 of the ten UI/UX groups: the patient panel, what the user enters there, what the system
estimates for them, and what it decides behind their back. Companion to
[the grouping index](ux-issue-grouping.md) and to
[the foundation plan](981-ux-foundation-and-common-components.md), whose components this group
spends.

Held to [ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md). The patient is the input
every dose is computed from, so rule 1 governs this group more than any other: a value the user
entered is never replaced by one the system guessed, and a value that filters the rules is never
set without the user seeing it.

**This plan covers the repairs**: a measured value that is dropped, a filter that is set unseen, an
estimate one host has and another has not, and a rule that says *adults* and matches everyone.
The two patient modes of #976 — a launched patient identified by name and birthdate, against an
anonymous one — are the group's other half and get their own plan, written once these have
landed. They are separated because they reach the launch, the platform port, the session and the
signature, and a regression there must not hold up the setter fix, which is the highest-safety
item of the forty-two reported.

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

Three open issues on one panel, and one domain issue on what a patient is matched against.

- **#488** A weight the user typed is replaced by the estimate when they then type an age, with
  no word about it. Dosing is computed from weight. This is the highest-safety item of the
  forty-two reported.
- **#716** Only the client estimates a weight and a height from an age, so a patient arriving
  from the launch or from the MCP host with an age alone is refused where the same patient typed
  into the web client is accepted.
- **#717** A missing department defaults to `ICK` in the server's mapper and in the MCP host.
  The department selects dose rules and the panel has no field for it, so every web patient is
  filtered as an ICK patient without the user seeing it.
- **#439** A dose rule may say it applies to adults without giving an age range. The facet
  exists in the type and in the parser; nothing matches on it, so such a rule would apply to
  every patient of every age. The extraction pipeline is gated shut because of it.

Two members are not planned here.

- **#976**, the two patient modes, is the second half of the group and gets the plan described
  above.
- **#718**, a measured weight alone or a measured height alone as the minimum, needs an estimate
  of the one from the other by sex: a growth table that nobody has named yet. It blocks nothing
  and nothing waits on it, so it keeps its issue until the table has a home. Saying so is better
  than planning a step whose first act would be to invent clinical data.

Issue #489, the panel that closed itself after five seconds, is already fixed: the `Disclosure`
component of G0 opens and closes when the user says so and never by itself. It stays a member of
the group on the index and needs nothing here.

## What the code does today

Read before planning, and worth stating, because three of the four repairs are smaller than the
umbrella issue suggests, and one thing the plan first assumed turned out to be the wrong way
round.

- **The setters.** `Shared/Models.fs` builds a patient through `Patient.create`, which takes the
  age parts, a weight, a height, the gestational age, and the rest, and writes the weight and
  height it is given as **measured**, with the estimates blank. `setYear` and `setMonth` pass
  `None` for the weight, the height and both gestational-age parts; `setWeek` and `setDay` pass
  `None` for the weight and the height and keep the gestational age; `setGAWeek` and `setGADay`
  pass `None` for the weight and the height as well. So every age and gestational-age edit
  drops what was measured, and `getWeight`, which falls back to the estimate, shows the
  estimate in its place. That is #488 exactly, and it is a handful of arguments.

  `setWeight` and `setHeight` have the opposite fault. Each carries the measure it does not set
  through `getWeight` or `getHeight`, which answer the estimate when nothing was measured, and
  `create` writes what it is given as measured. So typing a weight for a patient whose height is
  estimated turns that estimate into a measured height, with the same silence. The one rule of
  step 1 covers both faults: a setter carries measured values only, and leaves the estimates for
  the tables to fill again.
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
- **The reading**, for the second plan rather than this one. `PatientDataPort` is
  `read: string -> Patient option`: what the platform hands over is a patient record, with an
  age in years, months, weeks and days, and no name and no birthdate. So the identified mode
  needs a different thing read, not merely a different thing shown — which is why it is a plan
  of its own and not a step here.
- **The panel's own reset.** `Views/Patient.fs` ends in a full-width text button on the
  localized `Delete` term (*Verwijder*) that empties the whole patient. That is the shape #394
  complained about on the prescribing page, on a control that discards more.
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

  This meaning is **not** what the matcher does. `GenFORM.Lib/Patient.fs` compares the
  department with `eqs`, which answers true when either side is absent, so a patient with no
  department matches every rule, one hospital's ward rules included. The comparison is written
  twice on the patient side: in `PatientCategory.filter`, which the dose-rule filter applies,
  and in `PatientCategory.filterPatient`, which the solution rules apply. A third copy,
  `eqsOpt` in `PatientCategory.isMatch`, compares one rule's category with another's and has no
  patient in it, so it stays as it is. Today the `ICK` default hides that: it narrows what would
  otherwise be the widest rule set there is. Removing the default without changing the matcher
  would therefore not restore a neutral patient — it would hand the user every department's
  rules at once, which is worse than the wrong department's. The matcher has to change first,
  and it changes on one side only: a patient with no department stops matching a rule that
  names one, while a rule that names no department keeps applying to every patient, since a
  rule without a department is a rule for all of them.
- *The department as part of the minimum, with a field in the panel.* Not rejected, deferred: it
  needs a list of departments from somewhere, and a hospital's list is configuration. The only
  list in the code today is the five column names the product parser reads in
  `GenFORM.Lib/Product.fs` (`UMCU`, `ICC`, `NEO`, `ICK`, `HCK`), one hospital's wards written
  as sheet columns; offering those in the panel would encode the same hospital the default
  does. The panel shows the department in force, and where it came from; making it choosable is
  a follow-up once the list is configuration rather than code.
- *Keeping the default.* Rejected: it encodes one hospital in code and hides a filter input.

**The panel's reset.** Bounded, on an action bar, named with the same localized term as the
prescribing page's, and **with a confirmation**, unlike that one. The prescribing filter is
rebuilt by picking again; the patient is not: an age, a weight, a height and a gestational age
typed at the bedside are gone. Under rule 1 that is the case a confirmation is for. This group
therefore spends `ConfirmDialog` where G1 decided it did not.

**IsAdult.** Two questions, one of them a decision rather than a design.

- *What an adult is.* A single threshold, stated once in the domain and used by the matcher and
  by the printing. The plan proposes eighteen years and leaves the number to the reviewer, since
  it is clinical rather than technical.
- *A patient with no age.* An adult-only rule does not match them. Rejecting is the safe branch:
  the facet asserts something positive about the patient, and nothing is known.

## Chosen approach

- Every patient setter writes its own field and keeps every other **measured** value; the
  estimates are recomputed from the tables, never carried across an edit.
- The normal-value tables become a server resource and the server applies the same `Shared`
  estimate at the inbound boundary; the refusal of a patient with an age but no weight and
  height goes with it.
- No department means no department bound: the matcher stops letting a patient without one
  match a rule that names one, and only then does the default leave the mapper and the MCP host.
  The panel shows which department is in force and where it came from.
- A dose rule that says *adults* matches a patient at or over the adult threshold and no other,
  a patient with no age included.
- The panel's reset becomes a bounded secondary action, confirmed.

## Confidence

High. Every step is a rule that can be stated in a script and proved before anything is
migrated, and two of them remove something rather than add it. Nothing here changes what a
request carries or how a session works.

The one place to read slowly is the department. Steps 4 to 6 move the rule set a web patient
sees from one hospital's rules to the rules that bind no department, by way of a matcher that
today would give them everything. The three steps are ordered so that the widest state never
exists: the matcher is corrected while every patient still arrives with a department, and only
then does the department stop being invented. A patient who genuinely has one is unaffected
throughout.

## Steps

One pull request each, in this order. Everything outside `src/Informedica.GenPRES.Client/` is a
script first and a migration after, so the domain and server steps are two pull requests.

1. **What a setter keeps, as a script.** A script in `src/Informedica.GenPRES.Shared/Scripts/`
   shadows the `Patient` module, states the rule and proves it with Expecto: an age entered
   after a weight keeps the weight; after a height keeps the height; a gestational-age edit
   keeps both; a weight edit keeps the age and the gestational age; each setter keeps the
   gender, the access devices, the renal function and the department; a setter never writes an
   estimate as measured, so a weight typed for a patient with an estimated height leaves the
   height estimated; and the estimates are blank after every setter, so that the panel's next
   `applyNormalValues` is what fills them.
2. **The setters migrated**, with the tests in `tests/Informedica.GenPRES.Shared.Tests/`.
   Closes #488.
3. **The panel's reset, bounded and confirmed.** `Views/Patient.fs`'s full-width `Delete`
   button becomes an `ActionBar` with the localized `Reset` term and a `ConfirmDialog` naming
   what is discarded.
4. **What no department means, as a script.** A script over `GenFORM.Lib` that first shows what
   the matcher does today: a patient with no department matches every rule of every department,
   and the `ICK` default is the only thing standing in the way of that. Then it proves the
   wanted rule: a patient with no department matches the rules that name none and no others; a
   patient with one matches those rules and the rules that name none; and a rule without a
   department keeps applying to everybody.
5. **The matcher migrated.** The department comparison in `GenFORM.Lib/Patient.fs` changed in
   both patient-side matchers, `PatientCategory.filter` and `PatientCategory.filterPatient`,
   and left alone in `isMatch`, with the tests in `tests/Informedica.GenFORM.Tests/`. Nothing
   visible changes yet, because every patient still arrives with a department.
6. **The default removed**, from `ServerApi.Mappers.Order.fs` and the MCP host's `buildPatient`,
   with the panel showing the department in force and where it came from. Closes #717.
7. **The estimate on the server, as a script.** The normal-value tables as a loader in the
   resource registry, and `Shared`'s `applyNormalValues` applied to a patient read from the
   platform or built by the MCP host, proved to give the same weight and height the client shows
   for the same age and sex.
8. **The estimate migrated**, and the refusal of a patient with an age but no weight and height
   lifted in `ServerApi.Mappers.Patient.fs`. Closes #716.
9. **Adults, as a script and then migrated.** The adult threshold stated once in the domain,
   `getAge` no longer answering an empty range for `IsAdult`, the age filter matching adults
   only, a patient with no age not matching one, and the rule printed as *adults* rather than as
   no age bound. The `TODO` in `GenFORM.Lib/Patient.fs` goes with it, and with it the reason the
   extraction boundary is gated shut. Two pull requests. Closes #439.

## Verification, per step

- Every script step: `dotnet fsi` on the script, with an explicit check that the Expecto run
  reports `Status: Ok`, and the script left in the repository as the record of what was proved.
- Every migration step: `dotnet run Build`, `dotnet run ServerTests` and
  `dotnet fsi scripts/CheckDependencyRule.fsx`, each checked for its success line rather than
  read by eye.
- Every client step: `dotnet run ClientBuild`, the generated JSX read for the element that
  changed, and then the browser, by the user. Step 3 and the panel half of step 6 need the
  browser before they are done.
- Steps 5 and 6 also, in the browser: the same patient prescribed for before and after, and the
  medications offered compared. A patient with a department in the url sees what they saw; a
  patient without one sees the rules that bind no department, and the panel says so.
- Step 9 also, against the demo data: no rule carries the adult facet today, so the rule sets
  before and after are identical. A rule with the facet, made by hand in the script, matches an
  adult and no child.

## To settle in review

- **The adult threshold.** The plan assumes eighteen years, stated once in the domain and used
  by both the matcher and the printing. It is a clinical choice with a cost in both directions —
  too low and a paediatric rule is shadowed, too high and an adult-only rule silently drops — so
  it is named here rather than chosen in code. Also: whether an adult-only rule and an
  age-bounded rule may both match one patient.
- Whether the department should become a choosable field, and where a hospital's list of
  departments would live once it leaves `Product.fs`.
- Whether the panel should remark on a measured weight that is far from the estimate for the
  age, as #488 suggests as a second thought.
- Whether the estimate should stay client-side for the panel once the server computes one, or
  whether the panel should show the server's.
- Whether #718 should stay a member of the group at all, given that what it waits for is a
  growth table rather than an implementation.

## Related, not a member

- **#672**, **#720** — domain follow-ups of the patient-minimum work.
- **#719** — GenFORM tests for a patient value that is missing.
- **#598** — client testing, cited by this group and by G1 and G10.
- **#394** — the same control shape as step 3, on the prescribing page, closed by G1.
