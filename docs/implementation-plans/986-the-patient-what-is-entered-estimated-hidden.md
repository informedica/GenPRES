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
  The department selects solution rules and reconstitutions, and the panel has no field for it,
  so every web patient is filtered as an ICK patient without the user seeing it.
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
umbrella issue suggests, and two things the plan first assumed turned out to be the wrong way
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
  says "until the server estimates". The MCP host has a gate of its own,
  `requireWeightAndHeight` in `McpTools.GenOrder.fs`, which refuses before the mapper is
  reached, and the mapper gates twice, in `patient` and again in `parse`. Step 9 is what all
  three wait for.
- **The department.** `ServerApi.Mappers.Order.fs` writes `pat.Department |> Option.defaultValue
  "ICK"`, and the MCP host's `buildPatient` does the same. The client sets a department only from
  the url's `dp` parameter. The panel has no field.

  What the department selects was counted on the live sheets on 2026-09-25, and it is not what
  the umbrella issue and the first draft of this plan assumed:

  | Sheet          | Rows | ICK | ICC | NEO | No department |
  |----------------|-----:|----:|----:|----:|--------------:|
  | DoseRules      | 5166 |   0 |   0 |   0 |          5166 |
  | SolutionRules  |  335 | 214 |  55 |  28 |            38 |
  | Reconstitution |  514 | 151 | 150 | 148 |            65 |

  No dose rule names a department, so the default narrows none of them. Nine in ten solution
  rules and reconstitutions do name one, so the default is what selects the infusion, dilution
  and reconstitution constraints of nearly every IV medication. The comment on
  `GenORDER.Lib/Api.fs`'s `getRules`, which says a patient without a department takes the rules
  of every department, describes the dose rules and the matcher as it is, not what the sheets
  do with it.
- **The location.** A patient has a `Location` beside the department, matched the same way in
  `PatientCategory.filter` and in the reconstitution filters. Nothing sets it: the contract
  model's `create` writes `None` for it on every setter, and neither the url nor the launch
  carries one.
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
- **C2 `PickField`** is the department field of step 7.
- **C6 `Notice`** is how the page says a value is estimated rather than measured, and what is
  missing when a draft is no patient yet.

## Approaches considered

**What a setter keeps.**

- *Each setter keeps everything but its own field.* **Chosen.** One rule, eight setters, provable.
- *The panel re-applies the measured values after each setter.* Rejected: the rule would live in
  the view, where no test reaches it, and the contract model would go on lying to any other
  caller. The setters are the web client's alone today; the MCP host builds its patient with
  `GenORDER.Lib`'s setters, which have no such fault.
- *Warn that the estimate differs from what was measured.* This is what the issue suggests as a
  second thought and it is worth having, but it is a different feature: first the value is not
  lost, then a value that looks wrong for the age can be remarked on. Kept out of this group.

**Where the estimate is computed.**

- *The tables as a server resource, the existing `Shared` function called at the inbound
  boundary.* **Chosen.** The estimate is one function, on both sides, so the server's answer and
  the panel's display cannot drift.

  The MCP host is the boundary that needs a path. `McpTools.GenOrder.fs`'s `buildPatient`
  builds a `GenORDER.Lib` patient with that library's setters and hands it straight to
  `OrderContext.create`; the contract model's patient type is never in its hands, so the
  function as it stands cannot be applied there. The function is therefore split, not moved:
  the arithmetic of `applyNormalValues`, which reads only the gender, the age and the
  gestational age, becomes an `estimate` over those values and the four tables, answering a
  weight and a height, and `applyNormalValues` becomes the contract model's call of it. The MCP
  host references `GenPRES.Shared`, which the dependency rule allows (Presentation reaches the
  Contract), reads the tables from the same resource the web path reads, and calls `estimate`
  with its patient's values before the setters. One arithmetic, three callers.
- *A second estimate on the server.* Rejected outright: two estimates of a dosing weight.
- *Requiring a measured weight and height on the platform and MCP paths.* Rejected in the issue,
  and rightly: the platform reads what the record holds, and an age is a patient by the domain
  rule.

**What a missing department means, and whether a web patient may have none.**

- *No department bound: rules that name no department apply, rules that name one do not.*
  **Chosen** as the meaning, because it is the only reading that does not invent a filter the
  user did not set, and because it is what a department field that can be emptied will need.

  This meaning is **not** what the matchers do. `GenFORM.Lib/Patient.fs` compares the
  department with `eqs`, which answers true when either side is absent, so a patient with no
  department matches every rule, one hospital's ward rules included. The comparison is written
  twice on the patient side: in `PatientCategory.filter`, which the dose-rule filter applies,
  and in `PatientCategory.filterPatient`, which the solution rules apply. `GenFORM.Lib/Product.fs`
  has the same comparison twice more, as `eqsOpt`, in `Reconstitution.filter` and in
  `reconstitute`, fed from `PrescriptionRule.fs` with the patient's department and location. A
  fifth copy, `eqsOpt` in `PatientCategory.isMatch`, compares one rule's category with another's
  and has no patient in it, so it stays as it is. The matcher changes on one side only: a
  patient with no department stops matching a rule that names one, while a rule that names no
  department keeps applying to every patient, since a rule without a department is a rule for
  all of them. The location's comparison is left as it is: nothing sets a location today, and
  a rule for it would be a rule with no case to prove.
- *Removing the default.* **Rejected**, and it was the first draft's choice. On the live sheets
  a web patient without a department would keep every dose rule, since none names one, and
  lose nine in ten solution rules and reconstitutions: the infusion constraints of most IV
  medication gone, with nothing on the page to say so, and a check that the patient "sees the
  rules that bind no department" would pass and hide it. A department is, on these sheets, part
  of what a web patient needs to be prescribed for. The default stays until the user can choose.
- *The default kept and shown.* **Chosen** as the interim. The panel shows the department in
  force and marks it as the default when nothing chose it: the url's `dp`, later the launch.
  Under rule 1 a filter the user can see is not a hidden one, and a default the user can see is
  not a guess made behind their back.

  Shown from where, matters. The contract model's patient carries no department until the user
  or the url gives one; the `ICK` is written by `ServerApi.Mappers.Order.fs` while an order is
  mapped, and by the MCP host's `buildPatient`, and the panel sees neither. A panel that wrote
  its own `ICK` would agree with the mapper today and disagree the day the default changes. So
  the default becomes one server value, `Departments`, a resource holding the names the loaded
  rules carry and the default among them, put on the contract beside the server settings and
  fetched with them. The order mapper, the MCP host and the panel read it; nobody writes
  `"ICK"` but that resource, and a later setting replaces that one literal.
- *The department as a choosable field.* **Chosen**, as the step that closes #717, and no
  longer deferred. The list it offers is the departments the rules name, read from the solution
  rules and reconstitutions as they are loaded, so a hospital whose sheets name other wards gets
  those. The five column names the product parser reads in `GenFORM.Lib/Product.fs` (`UMCU`,
  `ICC`, `NEO`, `ICK`, `HCK`) are not that list: they are one hospital's wards written as sheet
  columns, and offering them would encode the same hospital the default does. Which department
  is the default, once the field exists, is configuration, and the `ICK` in code stays a
  stop-gap until a `GENPRES_*` setting names it; that setting is a follow-up, filed when step 7
  lands.

**The panel's reset.** Bounded, on an action bar, named with the same localized term as the
prescribing page's, and **with a confirmation**, unlike that one. The prescribing filter is
rebuilt by picking again; the patient is not: an age, a weight, a height and a gestational age
typed at the bedside are gone. Under rule 1 that is the case a confirmation is for. This group
therefore spends `ConfirmDialog` where G1 decided it did not.

**IsAdult.** Two questions, one of them a decision rather than a design.

- *What an adult is.* The domain already says: `GenCORE.Lib/Patient.fs` holds `eighteen`, and
  `toAgeType` answers `Adult` at or over it. `GenFORM.Lib` reaches `GenCORE.Lib` already, through
  `ZForm` and `ZIndex`, so the matcher and the printing reuse that value rather than state a
  second one. The number is not this plan's to choose.
- *A patient with no age.* An adult-only rule does not match them. Rejecting is the safe branch:
  the facet asserts something positive about the patient, and nothing is known. It is also what
  the age-bounded rules do already: `GenFORM.Lib/Utils.fs`'s `inRange` fails a patient with no
  age against any rule that bounds it.

## Chosen approach

- Every patient setter writes its own field and keeps every other **measured** value; the
  estimates are recomputed from the tables, never carried across an edit.
- The normal-value tables become a server resource and the server applies the same `Shared`
  estimate at the inbound boundary; the refusal of a patient with an age but no weight and
  height goes with it.
- No department means no department bound: the four patient-side matchers stop letting a
  patient without one match a rule that names one. The default stays, but in one place: a
  `Departments` value on the server, the names the loaded rules carry and the default, which
  the order mapper, the MCP host and the panel all read. The panel shows which department is
  in force and that it is the default, and then the department becomes a field the user picks
  from the same names.
- A dose rule that says *adults* matches a patient at or over `GenCORE.Lib`'s eighteen years
  and no other, a patient with no age included.
- The panel's reset becomes a bounded secondary action, confirmed.

## Confidence

High. Every step is a rule that can be stated in a script and proved before anything is
migrated, and two of them remove something rather than add it. Nothing here changes what a
request carries or how a session works.

The one place to read slowly is the department. Steps 4 to 7 correct four matchers that today
would give a patient without a department everything, and then let the user choose one. They
are ordered so that neither wide state ever exists: the matchers are corrected while every
patient still arrives with a department, and the default is never removed, so no patient loses
the solution rules and reconstitutions their ward's rules carry. A patient who arrives with a
department is unaffected throughout.

## Steps

One pull request each, in this order. Everything outside `src/Informedica.GenPRES.Client/` is a
script first and a migration after, so the domain and server steps are two pull requests.

1. **What a setter keeps, as a script.** A script in `src/Informedica.GenPRES.Shared/Scripts/`
   shadows the `Patient` module, states the rule and proves it with Expecto: an age entered
   after a weight keeps the weight; after a height keeps the height; a gestational-age edit
   keeps both; a weight edit keeps the age and the gestational age; each setter keeps the
   gender, the access devices, the renal function, the department and the location; a setter
   never writes an
   estimate as measured, so a weight typed for a patient with an estimated height leaves the
   height estimated; and the estimates are blank after every setter, so that the panel's next
   `applyNormalValues` is what fills them.
2. **The setters migrated**, with the tests in `tests/Informedica.GenPRES.Shared.Tests/`.
   Closes #488.
3. **The panel's reset, bounded and confirmed.** `Views/Patient.fs`'s full-width `Delete`
   button becomes an `ActionBar` with the localized `Reset` term and a `ConfirmDialog` naming
   what is discarded.
4. **What no department means, as a script.** A script over `GenFORM.Lib` that first shows what
   the matchers do today, against the loaded rules: a patient with no department matches every
   solution rule and every reconstitution of every department, and a patient with `ICK` matches
   its own and the ones that name none. Then it proves the wanted rule in all four patient-side
   matchers: a patient with no department matches the rules that name none and no others; a
   patient with one matches those rules and the rules that name none; and a rule without a
   department keeps applying to everybody.
5. **The matchers migrated.** The department comparison changed in `PatientCategory.filter` and
   `PatientCategory.filterPatient` in `GenFORM.Lib/Patient.fs` and in `Reconstitution.filter` and
   `reconstitute` in `GenFORM.Lib/Product.fs`, left alone in `isMatch`, with the tests in
   `tests/Informedica.GenFORM.Tests/`. The comment on `GenORDER.Lib/Api.fs`'s `getRules`
   rewritten to say what is now true: a patient without a department takes the rules that name
   none. Nothing visible changes, because every patient still arrives with a department.
6. **The department default, from one source.** A `Departments` resource in the registry,
   derived from the loaded solution rules and reconstitutions: the names they carry, and the
   default, the `ICK` literal moved there from the two places that write it today.
   `ServerApi.Mappers.Order.fs` and the MCP host's `buildPatient` read the default from it, the
   contract carries it beside the server settings, and the panel shows the department in force
   with a `Notice` that it is the default when neither the url nor the launch chose one. Three
   pull requests: the resource as a script, the resource and the two readers migrated, the
   panel.
7. **The department chosen.** The panel's department a `PickField` over the resource's names,
   the default preselected; the MCP host's `Department` input checked against the same names.
   Closes #717.
8. **The estimate on the server, as a script.** The normal-value tables as a loader in the
   resource registry; `Shared`'s `applyNormalValues` split into `estimate`, over a gender, an
   age, a gestational age and the tables, and its call; `estimate` applied to a patient read
   from the platform through the contract model, and to the MCP host's values before its
   `buildPatient` setters, proved to give the same weight and height the client shows for the
   same age and sex.
9. **The estimate migrated**, `GenPRES.Shared` referenced from `Informedica.MCP.Lib`, and the
   three gates lifted: `patient` and `parse` in `ServerApi.Mappers.Patient.fs`, and
   `requireWeightAndHeight` in the MCP host's `McpTools.GenOrder.fs`. Closes #716.
10. **Adults, as a script and then migrated.** `getAge` answering the range from
    `GenCORE.Lib`'s eighteen years for `IsAdult` instead of an empty one, the age filter matching
    adults only, a patient with no age not matching one, and the rule printed as *adults* rather
    than as no age bound. The `TODO` in `GenFORM.Lib/Patient.fs` goes with it, and with it the
    reason the extraction boundary is gated shut. Two pull requests. Closes #439.

## Verification, per step

- Every script step: `dotnet fsi` on the script, with an explicit check that the Expecto run
  reports `Status: Ok`, and the script left in the repository as the record of what was proved.
- Every migration step: `dotnet run build`, `dotnet run servertests` and
  `dotnet fsi scripts/CheckDependencyRule.fsx`, each checked for its success line rather than
  read by eye.
- Every client step: `dotnet run clientbuild`, the generated JSX read for the element that
  changed, and then the browser, by the user. Steps 3, 6 and 7 need the browser before they are
  done.
- Step 5 also, against the loaded rules in the script: for a patient with `ICK`, the solution
  rules and reconstitutions matched before and after are the same set, counted, not eyeballed.
  For a patient with no department the set after is the rules that name none, and the counts
  are the sheet's: 38 solution rules and 65 reconstitutions on the sheets of 2026-09-25.
- Step 6 also, in the test project: the department the order mapper writes for a patient
  without one is the resource's default, and the MCP host's is the same; and in the browser,
  the panel names that department and calls it the default when the url has no `dp`.
- Step 7 also, in the browser: the same patient prescribed for under `ICK` and under another
  department, and the solution rules offered compared; a department in the url preselected and
  shown as chosen, none in the url shown as the default.
- Step 9 also, through the MCP host: `create_order_context` with an age and a sex and neither
  weight nor height answers scenarios, and the weight and height it reports are the ones the
  web client shows for that age; and `get_order_scenarios` on that context answers the same
  scenarios as one made with the measures typed. The same request without an age is still
  refused, with the domain's reason.
- Step 10 also, against the demo data: the live sheet has no `IsAdult` column at all, so the
  parser reads the facet as false on every row and the rule sets before and after are
  identical. A rule with the facet, made by hand in the script, matches an adult and no child.

## To settle in review

- Whether an adult-only rule and an age-bounded rule may both match one patient.
- Whether the default department should become a `GENPRES_*` setting in this group or in a
  follow-up, and whether the departments the rules name are the right list to offer, or a
  hospital should name its wards in configuration.
- Whether a patient's location should be settable at all, given that nothing sets it and four
  matchers read it.
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

## As built

Every step landed as one or more pull requests from a fork branch against `master`, each
script-first where it touched source outside the client and reviewed before migration, between
2026-09-25 and 2026-09-26. The plan's shape held; the deviations are listed after the table with
the reason.

| Step | PR | Landed |
|------|----|--------|
| plan | [#1041](https://github.com/informedica/GenPRES/pull/1041) | this document, corrected against the code and the live sheets before the first step |
| 1 | [#1042](https://github.com/informedica/GenPRES/pull/1042) | `Scripts/Patient.fsx` in `GenPRES.Shared`: what a setter keeps, proved |
| 2 | [#1044](https://github.com/informedica/GenPRES/pull/1044), [#1045](https://github.com/informedica/GenPRES/pull/1045) | the age setters keep the measured weight and height; the gestational-age and measure setters keep the rest and never write an estimate as measured. Closes #488 |
| 3 | [#1046](https://github.com/informedica/GenPRES/pull/1046) | the panel's reset on an `ActionBar`, asked first through `ConfirmDialog` |
| 4 | [#1047](https://github.com/informedica/GenPRES/pull/1047) | `Scripts/Departments.fsx` in `GenFORM.Lib`: what the matchers do today, counted against the loaded rules, and the wanted rule |
| 5 | [#1048](https://github.com/informedica/GenPRES/pull/1048) | a patient with no department matches no ward rule, in the two patient-side matchers and the two reconstitution filters of `Product.fs`; the `getRules` comment rewritten |
| 6 | [#1049](https://github.com/informedica/GenPRES/pull/1049), [#1050](https://github.com/informedica/GenPRES/pull/1050), [#1051](https://github.com/informedica/GenPRES/pull/1051) | the `Departments` resource and its one default; the order mapper and the MCP host read it; `ServerSettings` carries the names and the default; the panel says which department is in force and where it came from |
| 7 | [#1052](https://github.com/informedica/GenPRES/pull/1052), [#1053](https://github.com/informedica/GenPRES/pull/1053), [#1054](https://github.com/informedica/GenPRES/pull/1054) | the department a `PickField` in the panel, the default preselected; the MCP host's department checked against the names, as a script and then migrated. Closes #717 |
| 8 | [#1055](https://github.com/informedica/GenPRES/pull/1055) | `Scripts/Estimate.fsx` in `GenPRES.Server`: four libraries shadowed, the server's numbers the client's |
| 9 | [#1056](https://github.com/informedica/GenPRES/pull/1056) | the normal-value tables a resource; a platform reading enters estimated; the MCP host estimates what a caller left out. Closes #716 |
| 10 | [#1057](https://github.com/informedica/GenPRES/pull/1057), [#1058](https://github.com/informedica/GenPRES/pull/1058) | `Scripts/Adults.fsx` in `GenFORM.Lib`, then an adult rule matching adults only. Closes #439 |

### Deviations from the text above

- **The mapper's gates stayed.** Step 9 said the three gates go. Only the MCP host's did: with an
  age, or both measures, it is a patient, and what the age leaves blank is estimated. The server's
  `patient` and `parse` keep refusing a draft without a weight and a height, measured or
  estimated, because a draft the client did not estimate would otherwise reach the rules and get
  no answer where it got a refusal before. They are the safety net; the server estimates at its
  own two boundaries, the platform port and the MCP host.
- **No `estimate` split.** Step 8 planned `applyNormalValues` split into a function over a gender,
  an age and the tables. Neither host needed it: a platform reading is round-tripped through the
  contract model, and the MCP host builds a contract-model draft from the age and the sex it was
  given. The one estimate stays one function.
- **A blank estimate is refused, named.** When the tables did not load, or hold no row for the
  age and sex given, the MCP host refuses the call naming the measures still blank and both
  causes, rather than handing the rules a patient they answer nothing for.
- **The sheets are checked at load.** A normal-value sheet without the five columns, or with a
  value that is not a number, fails the resource with the sheet, the columns or the row named,
  and the registry's warning and empty fallback apply. Unchecked, the parse would have thrown
  during a request.
- **The loader sits in `Mapping.fs`.** In `Resources.fs` the sheet fetch tripped the core ratchet
  of the dependency check; `Mapping.fs` is where the sheet fetching is already allowed.
- **The departments resource reads the dose rules too.** The plan derived it from the solution
  rules and the reconstitutions, the two sheets that name departments today. A dose rule names
  one as well, so a department only a dose rule names would have been refused at the MCP host.
- **The MCP department check takes the exact spelling first**, and another case only when it
  names exactly one of the rules' spellings, since the rules compare exactly.
- **The panel's department field.** The department in force is always among its options, so a
  url or launch department the rules do not name is shown, and a field with one loaded name never
  picks that one over the patient's own. The cross is offered only while a department is chosen;
  a default already shown has nothing to return from. A launched patient's department is shown
  but not chosen, as its age is: the platform decided it. The notice's title went, since the
  field shows the name; "chosen in the url" became "chosen for this patient", since the panel
  chooses too.
- **The adult threshold is in days**, eighteen times 365, not eighteen of the year unit. The
  contract model, the order patient and the sheet's age bounds count a year as 365 days; the
  year unit counts 365.25, and a patient entered as eighteen arrived four and a half days short
  of adult. The category's age printer is `ageToString`, since `printAge` was taken.
- **`Informedica.MCP.Lib` references `GenPRES.Shared`**, as a Presentation project may; the
  project graph in `ARCHITECTURE.md` is regenerated with it.
- **Step 2 was two pull requests**, the age setters and then the rest, to keep each under the
  size limit.

### Answered in review

- An adult-only rule and an age-bounded rule never both match one patient: the tests of step 10
  prove it over the ages 0, 5, 17, 18 and 30.

### Left open

- **#718** stays a member, waiting for a growth table. **#976**, the two patient modes, gets its
  own plan.
- The emergency-list workbook id is a literal twice, in the client's `Utils.fs` and in
  `Mapping.normalValuesUrlId`; the two become one setting together.
- The departments the panel offers are the ones the rules name; a hospital naming its wards in
  configuration is the follow-up the plan deferred.
- The extraction boundary in the NLP scratch script still empties the age bounds of an adult row;
  with #439 closed its owner can open it.
