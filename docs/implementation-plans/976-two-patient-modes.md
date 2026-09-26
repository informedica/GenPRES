# Implementation plan for issue #976

Second half of G5, the patient panel: two patient modes, decided by the launch.

- **Identified**: a specific person the EHR has data for, with a patient id, a name and a
  birthdate.
- **Anonymous**: a patient entered at the keyboard or by url parameters, or a launch for which
  neither the EHR nor a signed version of the record has an identity.

In this plan, *EHR data* means what the EHR returns for a patient id. Builds on
[the patient panel plan](986-the-patient-what-is-entered-estimated-hidden.md); see also
[the grouping index](ux-issue-grouping.md).

Held to the three rules of [ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md): an
identified patient's age is computed by the server and read-only (rules 1 and 2); the title bar
shows who the patient is; weight, height and gestational age stay editable and a measured value
is kept (rule 3). ADR-0009's fourth section, which recorded this decision and three other domain
decisions, is removed in the pull request that adds this plan (2026-09-26); each decision stays
in its own issue and plan.

**Departure from #976.** The issue computes the age on each request. This plan computes it at
the open and again right after each sign, and holds it in between (see
[Approaches considered](#approaches-considered)). The grouping index follows the plan; a comment
on #976 records this when the plan lands.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [What the first half settled](#what-the-first-half-settled)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [To settle in review](#to-settle-in-review)
- [Related, not a member](#related-not-a-member)

## Problem description

The panel behaves the same however the patient arrived. For a launched patient:

- The age (years, months, weeks, days) is editable, as if the patient were typed in.
- EHR data carries an age, not a birthdate, so over a long session the age goes stale by the
  day; for a neonate this can cause a dosing error.
- The panel does not identify the patient: only the id is sent, and it is not shown.
- A weight measured at the bedside after the EHR data was read is the better value. It is in the
  plan the user signs, but the Session drops it at the sign and at the next open, and the client
  drops it when a data notice is accepted.

## What the code does today

EHR data, the Session, the signature and the store form one path; every step below touches it.

- **Patient types.**
  - The core patient, `Informedica.GenCore.Lib.Patients.Patient` (`GenCORE.Lib/Patient.fs`): age
    as a birthdate, an age value or unknown; weights and heights as dated measurements (at
    birth, on admission, and a separate calculation value derived from neither); department,
    gender, gestational age, venous and enteral access. Its header says it is kept for reference
    and a future platform integration; only its own tests and one constant in
    `GenFORM.Lib/Utils.fs` use it. Its `toString` and Dto are unused.
  - Its gaps: `BirthDate` is partial (month and day optional) and `AgeValue.fromBirthDate`
    throws on a partial one; `Patient.SetGet.getAgeValue dt` returns optional years, months,
    weeks and days, not a number of days; `Dto.fromDto` drops the department; there is no id
    and no name.
  - The projection, `GenForm.Patient` (`GenFORM.Lib/Types.fs`), is what the rules match: age,
    weight and height as single values with flags for which were measured, and the access
    devices, the enteral tube among them. Everything from the port to the signed version
    carries it. `Patient.toString` in `GenFORM.Lib/Patient.fs` is its only printer (department,
    gender, age, gestational age, weight, height, BSA), used in a formulary debug line, the
    order context text and the rule texts on screen.
  - `GenFORM.Lib` reaches `GenCORE.Lib` only through `ZForm.Lib`. `GenPRES.Shared` references
    no project, so every wire type is its own.
- **EHR data.** `PatientDataPort` (`ServerApi.Ports.fs`) is `read: string -> GenForm.Patient
  option`: a projection with an age in days, no name, no birthdate. Per the integration
  specification, the EHR will return patient id, full name, birthdate and gender, plus optional
  weight, height, gestational age, department, renal function and vascular access, each
  time-varying value dated. There is no live adapter (ADR-0004, FHIR, is superseded and its
  prototype deleted). The only adapter, `StubPatientData` (`ServerApi.StubAdapters.fs`), returns
  one fixed ten-year-old for every id and none for `no-data`. Since #1056 `Patient.estimating`
  wraps the port in `ServerApi.Adapters.fs`, so EHR data with only an age arrives with estimated
  measures.
- **Open.** `Session.sessionPatient` takes the EHR data, else the patient of the record's head,
  else nothing. `OpenedSession` stores the `PatientId` (a string) and that patient; the client
  receives both as `SessionOpened.PatientContext` (`Shared/Types.fs`) via
  `Mappers.Session.toOpened`, and the session machine's `SetPatient` passes the patient to
  `updatePatient` like a url or panel patient. The panel draft is then editable in every field.
- **Each request.** `Compute.bound` (`ServerApi.Compute.fs`) runs every computing command. It is
  generic over the command, with a `name` and a `gate` per family. It reads the cookie and calls
  `env.session.seen`, which returns only a record notice; it never sees the Session or a
  patient. Handlers parse the request's patient with `Mappers.Patient`, so the client's age is
  the age the rules see. The formulary command also carries a patient (`Patient.patientOption`)
  and matches on age; the interaction command carries none. Launch, session, signing and admin
  commands run through `Compute.logged` instead: `SigningCommand.processCmd` reads the cookie
  itself, checks each plan patient with `Patient.patient` and parses the plan with
  `OrderPlanCommand.parsePlan` before asking the session port for a challenge or a commit. The
  port's `find` returns the opened Session by id; nothing calls it per request.
- **Challenge.** `Session.challenge` reads the EHR again and compares it with `Opened.Patient`;
  if they differ it returns a data notice instead of a challenge. The plan's patient is
  recorded as what the user saw, entered or read; the version's patient is the Session's.
- **Accept.** On accepting a data notice, `Client.Core/SigningMachine.fs` replaces the plan's
  patient with the notice's data and returns `SigningEffect.SetPatient data`, which the app
  handles as `UpdatePatient`. A bedside weight typed before the notice is lost from the panel
  and from the plan then challenged.
- **Commit.** `Session.commit` writes the version, then sets `Patient = challenge.Reading |>
  Option.defaultValue version.Plan.Patient`. When the EHR read a different weight, the bedside
  weight is in the version but gone from the Session.
- **Resume.** `Resume` returns `SetPatient` with the Session's patient, replacing the draft. A
  bedside weight typed after the open is in the draft and the requests but not in the Session,
  so a browser reload drops it, for every launched patient. The next open also starts from EHR
  data.
- **Version.** `OrderPlanVersion` (`GenORDER.Lib/Types.fs`) holds id, number, patient id, base,
  signer, time and the signed plan, whose patient is the projection. `Session.commit` builds it
  from the Session record; the wire carries it as `SignedOrderPlan` via
  `Mappers.Session.ofSigned` and `toSigned`. Only the id identifies the patient.
- **Store.** `ServerApi.SqlAdapters.fs` applies the numbered scripts under `Sql/`. Three columns
  hold a patient as JSON under a structure version: `session_opened_with.patient` and, from
  `004-notices.sql`, `challenge.reading` and `data_notice.data`. The versions table holds the
  plan the same way. ADR-0008 section 6: every stored shape carries a structure version and
  changes by expand and contract. ADR-0007 section 3: the working state carries GenORDER and
  GenFORM types.
- **Title bar.** `Components/TitleBar.fs` shows the user and role behind a person button when a
  Session has a user, nothing for anonymous use, and no patient. A Reader sees everything a
  Prescriber sees except the sign button.
- **Panel.** `Views/Patient.fs` draws the age fields with `SimpleSelect`, `readOnly` set to
  `false`; reset is gated on `busy` only and clears the whole draft. `launched` reads whether a
  Session is open, which is also true for a `no-data` launch. The url already carries a
  birthdate as three integers, `by`, `bm` and `bd`, which `App.fs` turns into an age.

## What the first half settled

- Setters keep every measured value across every other edit, an age edit included (#488;
  #1044, #1045), so a bedside weight survives every other panel edit.
- The server estimates measures for EHR data with only an age (#716; #1056), so an identified
  patient with a birthdate and no weight gets the estimate.
- The department is chosen in the panel, or comes from the launch and is shown read-only
  (#717; #1051, #1052).

## Approaches considered

### Where the age is computed

**Chosen: on the server, at the open and again right after each sign, from the EHR birthdate.**
On every request in between, the sign included, the Session's age replaces the request's; no
clock is read. The server signs and stores the version, so it must evaluate against the age it
computed. Holding the age until the sign means the signed version carries the age every
calculation behind it used, and the next round starts on a fresh age.

- *On each request*: rejected. Two requests minutes apart could use two ages when a neonate's
  day turns between them, and the plan on screen would rest on an age the next request no longer
  uses.
- *On the client, from the birthdate*: rejected in #976; the server would sign the client's
  clock and arithmetic.
- *One mode, age read-only whenever EHR data is present*: rejected in #976; EHR data carries an
  age today, so it would go stale, and the identity would still be missing.

The chosen rule has one bound: a Session left idle ends after an hour (#1061), so a browser
left behind cannot hold yesterday's age for whoever picks it up. A Session in use across a day
keeps the age it opened on, and signs on it; the project accepts that, since working on one
order plan for more than an hour is extremely rare, and states it here rather than adding a
check to the signing path.

### What EHR data carries

**Chosen: the core patient plus rule-only data.** `PatientDataPort.read` returns
`EhrPatientData option`:

- the core patient, `Patients.Patient`, with patient id and name added: the birthdate as its
  age, the gender, and the EHR weight and height as dated measurements;
- the rule-only data: renal function and access (vascular and enteral). Access lives here only;
  the core patient's own access fields are not filled from EHR data.

One projection, `GenForm.Patient.ofEhr`, derives the GenFORM patient at a date: age from
`getAgeValue now`, measures from the calculation values, rule-only data as given, BSA and
post-menstrual age always calculated. This is the core patient's first use in the pipeline; its
header note goes. Because the type derives nothing, whatever writes a dated measurement also
writes the calculation value. Step 1 closes the shape gaps: the birthdate becomes fully
specified, a year, a month and a day, so that nothing downstream has to say what a partial one
means (decided 2026-09-26); the optional age parts become days; the department is read.

- *A server-only record (name, birthdate, dated measurements) beside the GenFORM patient*:
  rejected. It is the smaller change, since the core type has the gaps above, but it duplicates
  the domain's own patient type. The plan's reviewer recommended it; see
  [To settle in review](#to-settle-in-review).
- *Birthdate and name on the GenFORM patient*: rejected. Every rule module would carry fields it
  never reads, and its printer would have to skip them.

### Where the request's age is replaced

**Chosen: one pure function per command family, `aged: Age option -> 'cmd -> 'cmd`, that
rewrites every contract patient in the command.**

- `Compute.bound` takes `aged` beside `name` and `gate`, gets the Session's age from `seen`
  (which returns it beside the notice), and applies it; `None` without a Session or without an
  identity.
- `SigningCommand.processCmd` (via `Compute.logged`) applies the same function to the plan
  before checking and parsing it.

Both paths a patient can take into the server pass through it, so the signed version carries
the Session's age, not the client's. Families that carry a patient: order context, order plan,
formulary, signing. `seen` rather than `find`: both touch and persist the Session, and one touch
per request is the current behaviour.

- *In `Compute.bound` alone*: rejected. It is generic and cannot reach a patient inside a
  command, and signing does not pass through it, so the signed plan would keep the client's age.
- *In the mapper, with the Session as a parameter*: rejected. `Mappers.Patient` is pure and
  tested as such; a Session parameter reaches every caller.
- *Trusting the client's age for an identified patient*: rejected by rule 1.

### What the challenge compares

**Chosen: EHR data as read.** `OpenedSession` keeps the EHR data as read beside the projection;
`challenge` compares a fresh read with it. The projection's age is computed, so comparing
projections would report a change where the data did not change.

- *Computing the fresh read's age and comparing projections*: rejected; the moment differs from
  the open's, so the comparison would depend on the clock.

### Bedside measurements across a notice, a commit, a reload and a re-open

**Chosen (as #976 decided): a value the user measured stays for the Session, whatever the EHR
reads later; the version records it; the data notice still reports the EHR change** (rules 3
and 1). Accepting a notice and committing both apply one field-by-field merge:

| Field | Source |
|-------|--------|
| Identity, gender, department | EHR data |
| Age | The Session's held age; no clock read |
| Weight, height, gestational age | The Session's measured value if the user measured, else EHR |
| Renal function, access | EHR, unless entered |

On accept the client applies it; the notice's payload is projected at the challenge over the
Session's age, so a notice never moves the age. On commit the server applies it to the
Session's patient and then computes the age again.

**Chosen: the Session records measured weight, height and gestational age from requests**, as
dated actual measurements on its core patient, so a resume restores them. The panel sends the
whole draft on every edit and the store is append-only, so writes are bounded: only when the
request's patient passes validation and the value differs from the last one held, in the
measurement's own row, not a new opened-with row. Clearing a value writes a row that says none,
since the latest row decides; otherwise a cleared weight would return at the next resume.
Alternative: see [To settle in review](#to-settle-in-review).

- *At the next open, the head's measured values over EHR data*: not decided; the default is EHR
  data. See [To settle in review](#to-settle-in-review).

### What the signed version stores

**Chosen: the identity in columns of the versions table; the plan still carries the
projection.** One migration adds a nullable name and birthdate to `order_plan`, beside
`patient_id` among the identity columns that are authoritative over the JSON. Older rows have
none; the store never rewrites a row. `OrderPlanVersion` and its Dto are unchanged, so
`order_plan.plan` keeps its structure version. The store's `StoredVersion` carries the identity
in both its cases, readable and unreadable, like the other identity columns. `Session.commit`
writes the Session's identity, never the request's; `SignedOrderPlan` carries it via the session
mapper. A version read later names the patient on its own, and its plan is what the rules saw.

- *The identity in the Session only*: rejected. A version reopened or listed from the record
  would name only an id, and a Session opened from the head (EHR answering none) would have no
  identity.

### What the store holds of EHR data

**Chosen: new nullable columns beside the three patient JSON columns, the projection column
untouched.** `session_opened_with`, `challenge` and `data_notice` each gain an `ehr_data` column,
the EHR data as its own JSON under its own structure version, and `session_opened_with` the
identity columns beside it; `patient`, `reading` and `data` keep holding the projection as
today. A release before this one ignores the new columns and reads the projection as before, so
a rollback by one release is safe without a downgrade step, and no expand and contract is
needed.

- *A new structure version of the three patient columns*: rejected. ADR-0008 section 6 has the
  first release still write the old shape through a downgrade step and write nothing the old
  shape cannot hold before the release after, so the identity and the EHR data would not be
  stored for two releases; and an expand that writes the new shape at once, as an earlier
  draft of this plan had, breaks the rollback by one release that the rule protects.

### How the identity reaches the client

**Chosen: on `SessionOpened.PatientContext`, beside the id and the projection, as a contract
record of name and birthdate, the birthdate as three integers (year, month, day).** The title
bar already reads the Session through the environment, and the url already uses `by`, `bm` and
`bd`. A `DateTime` at midnight can shift a day through UTC serialization and the browser's time
zone, and a wrong birthdate in the title bar misidentifies a patient.

### How the client gets the age after a sign

**Chosen: the `Submitted` answer carries the Session's patient beside the fresh token, and the
session machine passes it to the panel as at a resume.** Cost: `SetPatient` is handled as
`UpdatePatient`, which re-evaluates the workbench, the formulary and the parenteralia. The sign
ends an episode and leaves the signed plan untouched, so this re-evaluation starts the next
episode. An age-only update: see [To settle in review](#to-settle-in-review).

## Chosen approach

- ADR-0009 loses its fourth section; the decisions stay in their issues and plans.
- EHR data is the core patient (`Patients.Patient` with id and name: birthdate, gender, dated
  measurements) plus rule-only data (renal function, access). The Session keeps both as read
  and derives the GenFORM patient at a date with `GenForm.Patient.ofEhr`, which calculates BSA
  and post-menstrual age. The estimate moves from the port wrap to the open, applied over the
  projection (step 3).
- At the open, an identified patient's age is `Patient.SetGet.getAgeValue now` over the
  birthdate; an EHR age value is ignored when a birthdate is present. That age holds up to and
  including the next sign. The challenge compares EHR data as read.
- On each request of an identified Session, on both paths, every patient's age is replaced by
  the Session's before parsing; no clock is read. Measured weight, height and gestational age
  are kept, and recorded (validated, on change) as dated actual and calculation values on the
  Session's core patient, so a resume restores them.
- Accepting a data notice and committing both apply the merge: identity from EHR data, the
  Session's held age, the user's measured values.
- The signed version carries the Session's age and, in the record's columns rather than the
  plan JSON, the identity. Right after the commit the Session's age is computed again, the
  projection and estimate follow, and the client receives the Session's patient with the fresh
  token.
- The store keeps the three patient JSON columns as they are and gains, beside them, the EHR
  data as its own JSON column under its own structure version and the identity columns:
  additive, nullable, ignored by the release before, so no expand and contract and a rollback by
  one release reads the projection as before. ADR-0007 section 3 adds GenCORE to the
  working-state types in the same pull request.
- The contract carries the identity on `PatientContext` and `SignedOrderPlan`, the birthdate as
  three integers. The title bar shows it beside the user, for a Reader and a Prescriber. The
  panel is in identified mode when the context carries an identity, not merely when a Session
  is open: age read-only, identity and age kept through reset, other fields unchanged. A
  `no-data` launch and a patient built by the MCP host are anonymous; the panel is unchanged
  for both.
- The identity is shown in full to the user and kept by the Session, its store and the
  versions; never on the projection and never in a debug or log line.

## Confidence

Medium. EHR data, the Session and the request path are well delimited, and each step is a rule a
script can state and test. Three risks:

- The core patient has never been in the pipeline; step 1 must close its gaps first.
- There is no live EHR adapter, so the design is tested against the stub only; mapping a real
  record onto the core patient is follow-up work.
- The age replacement sits on the path every computing command takes, so it must be proved not
  to change an anonymous request.

## Steps

One pull request per step unless the step says two. Everything outside
`src/Informedica.GenPRES.Client/` is first a script, then a migration; a migration spanning more
than four areas is split into two pull requests.

1. **EHR data as core patient plus rule-only data (script).** A script in
   `src/Informedica.GenPRES.Server/Scripts/` shadows `Patients.Patient`, `GenForm.Patient`,
   `Ports`, `StubPatientData` and `Mappers.Session`:
   - patient id and name on the core patient; `EhrPatientData` is the core patient plus renal
     function and access;
   - the birthdate fully specified: month and day required on `BirthDate`, its Dto's
     validators with them; the identity is a name and a birthdate;
   - `GenForm.Patient.ofEhr now`: age from `getAgeValue now` in days, measures from the
     calculation values, rule-only data as given, BSA and post-menstrual age calculated,
     department read, other fields copied;
   - the port's `read` returns EHR data; the stub builds it for its fixed patient (constant
     birthdate, name, gender) and returns none for `no-data`;
   - `OpenedSession` keeps the EHR data as read beside the projection;
   - the contract's `PatientIdentity` in `Shared/Types.fs` (name, birthdate as three integers)
     on `PatientContext`.

   Tests: the stub patient's projection at a fixed date equals today's stub answer, field for
   field; EHR data round-trips to the client context with name and birthdate; `no-data` has
   none; an age value in place of a birthdate opens anonymous; neither printer writes the name
   or the birthdate.

2. **EHR data migrated (two pull requests).**
   - Identity and data into `GenCORE.Lib`; the projection into `GenFORM.Lib`, which gets a
     direct `ProjectReference` to `GenCORE.Lib` instead of the path through `ZForm.Lib`
     (project graph in `ARCHITECTURE.md` regenerated, dependency check run); the identity into
     `GenPRES.Shared`; port, stub and mapper into the server; tests.
   - Store: one migration adds `session_opened_with.ehr_data`, the EHR data as the core
     patient's Dto beside the rule-only data under its own structure version, and the
     identity columns beside it; `patient` keeps the projection unchanged. ADR-0007 section 3
     amended to include GenCORE.

3. **Age at the open (script).** `sessionPatient` shadowed: EHR data projected at `now` (the
   clock the Session already takes), so an identified patient opens on the birthdate's age and
   the EHR age value is ignored; the estimate applied over the projection here and the port
   wrap in `ServerApi.Adapters.fs` removed. `challenge` shadowed to compare fresh EHR data with
   what the Session kept.

   Tests: EHR data with the stub's birthdate opens on that birthdate's age at the clock's date,
   whatever age value it carried; EHR data without a birthdate opens as today; the estimate is
   the one for the computed age; an unchanged read gives a challenge, a changed read a data
   notice.

4. **Open migrated**, tests in the Server test project; `challenge` and `data_notice` gain an
   `ehr_data` column the same way, `reading` and `data` keeping the projection.

5. **Age on each request (script).** `aged` per command family; `Compute.bound` takes it beside
   `name` and `gate`, gets the Session's age from `seen` and applies it, no clock read;
   `SigningCommand.processCmd` applies it to the plan before the check and the parse.

   Tests, over the order-context, order-plan, formulary and signing commands: an identified
   request with a wrong age is evaluated at the Session's, and the plan passed to the session
   port for a challenge carries it; two requests of one Session use one age whatever the clock
   does between them; a request without a Session, or in a Session without an identity, is
   unchanged field for field; a measured weight survives the rewrite.

6. **Request migrated**, tests in the Server test project. From here the client's age no longer
   matters for an identified patient, on either path.

7. **Measurement recorded (script, then migration; two pull requests).** The session port
   records a measured weight, height or gestational age from a request as a dated actual and
   calculation value on the Session's core patient: only for a patient that passes validation,
   only on change, in its own row; clearing writes a row that says none.

   Tests: a measured weight becomes the latest actual; the same weight again writes nothing; a
   draft that fails validation writes nothing; a resume restores the measurement; a cleared
   weight stays cleared after a resume.

8. **Version identity and age after the sign (script).** The identity in `StoredVersion` (both
   cases) and on `SignedOrderPlan`, carried both ways by the session mapper; `OrderPlanVersion`
   and its Dto unchanged. `Session.commit` writes the Session's identity into the record; once
   the version has landed it sets the Session's patient to the merge, computes the age again at
   `now`, applies projection and estimate, and returns the Session's patient with the fresh
   token. A new numbered migration adds the nullable identity columns.

   Tests: a version signed in an identified Session reads back with the Session's name and
   birthdate, and its plan with the projection the rules saw at the Session's age, its JSON
   under the unchanged structure version; one signed without an identity reads back with none;
   a row written before the migration reads as none; a Session opened from the head (EHR
   answering none) gets the head's identity and is identified; after a commit with the clock a
   day later, the
   Session's age is one day more and the version's is not; after a commit where the EHR reports
   another weight, the Session keeps the weight the user measured.

9. **Version migrated**: the wire field into `GenPRES.Shared`; the store's identity, mapper,
   commit and migration into the server; tests into both test projects. Two pull requests if
   the size limit requires.

10. **Title bar and panel.** `Components/TitleBar.fs` shows name, birthdate and id beside the
    user when the context has an identity, for a Reader and a Prescriber. `Views/Patient.fs`
    chooses its mode by that identity, not by an open Session: a `no-data` launch with no
    signed version opens a Session without one, and one with a signed head opens identified
    from the head. Identified mode: age fields `readOnly`, identity and age kept through reset,
    summary line names the patient, other fields unchanged. The signing machine's accept applies
    the merge to the draft; the session machine takes the patient from `Submitted` as it does
    at a resume. Anonymous mode is unchanged for the anonymous url and the `no-data` launch
    without a head. Closes #976.

## Verification, per step

- Script steps: `dotnet fsi` on the script, checking that Expecto reports `Status: Ok`; the
  script stays in the repository.
- Migration steps: `dotnet run build`, `dotnet run servertests` and
  `dotnet fsi scripts/CheckDependencyRule.fsx`, each checked for its success line. A step that
  adds a project reference also runs `dotnet fsi scripts/ProjectGraph.fsx --write`.
- Client steps: `dotnet run clientbuild`, the generated JSX checked for the changed element, then
  the browser, by the user.

Additional checks:

| Step | Check |
|------|-------|
| 1–2 | The server log of a stub launch contains neither the name nor the birthdate. |
| 2 | A development database written before the migration reads back after it, and one written after it reads back in the release before, the new columns ignored. |
| 3–4 | Stub launch as Prescriber; a challenge requested with no EHR change returns a challenge, not a data notice; the same with the clock a day after the open. |
| 5–6 | A test compares the parsed patient of a request without a Session before and after, field for field, for every family that carries one; a version signed in a stub Session reads back with the Session's age and the typed weight. |
| 7 | Ten panel edits to the same weight add one row. |
| 8–9 | After a signature the versions table has the name and birthdate in its columns and the plan JSON under its previous structure version; after the migration older rows read as none. |
| 10 | In the browser: a stub launch as Prescriber and as Reader shows the name and birthdate in the title bar and a read-only age; the anonymous url and the `no-data` launch without a head show the panel as today, age editable; a weight typed in identified mode survives a reload, an accepted data notice and a signature, and is in the signed version; after the signature the panel shows the age for the current date. |

## To settle in review

- **Core patient or wrapper.** The reviewer recommended a server-only record as the smaller
  change; the plan uses the core patient and closes its gaps in step 1. Confirm.
- **Measurement write.** Validated, on change, in its own row, as planned; or at the challenge
  only, accepting the loss on reload until #598 gives the client its own memory.
- **Next open.** Should the head's measured weight, height and gestational age win over EHR data
  when the head was signed after the EHR data was read? Default: EHR data wins.
- **Update after a sign.** The plan re-evaluates everything through `UpdatePatient`; is an
  age-only update that keeps the lists worth building?
- **Live adapter.** How a real EHR record maps onto the core patient: which name, what a missing
  birthdate means, which weights are actuals. Decided when an adapter exists.

## Related, not a member

- **#1061** a Session left idle for an hour ends, so that a Session left behind cannot carry an
  age across the day; a Session in use across a day is accepted, as rare.
- **#718** a weight alone, or a height alone, as the minimum; waits for a growth table.
- **#598** client testing: would let step 10 test both modes without the browser, and let the
  client remember a measurement itself.
- **ADR-0004** the superseded FHIR integration; the last word on a live adapter.
- **ADR-0007** and **ADR-0008** the store and the structure version every stored shape here
  changes under.
