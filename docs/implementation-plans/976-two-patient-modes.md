# Implementation plan for issue #976

The second half of G5, the patient panel: two patient modes, decided by the launch. An
*identified* patient is a specific person the platform has data for, with a patient id, a name
and a birthdate; an *anonymous* patient is the one filled in at the keyboard or by url
parameters. Companion to [the patient panel plan](986-the-patient-what-is-entered-estimated-hidden.md),
whose repairs this plan builds on, and to [the grouping index](ux-issue-grouping.md).

Held to [ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md), the three rules: a launched
patient's age is a fact the server computes and the user cannot write, which rule 1 asks and
rule 2 makes unnecessary to type; the title bar says who they are; weight, height and
gestational age stay editable and a measured value is kept, which rule 3 asks. The decision
itself is #976's and this plan's. The ADR's fourth section recorded that decision and three
others of the same kind, domain decisions under the rules; an ADR states the rules, so that
section goes, in the pull request that brings this plan, and each decision stays where it was
taken, in its issue and its plan. On one point this plan departs from #976's wording: the issue says the
age is computed on each request; this plan computes it at the open and again right after each
sign, and holds it still in between, for the reason given below.

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

The panel has one behaviour whatever brought the patient there. A launch with a patient hands
the client the EHR's patient data, and the panel shows an age in years, months, weeks and days
that the user can change, as if the patient were made up at the keyboard. The EHR's patient data carries an
age, not a birthdate, so over a long session the age goes stale by the day, which for a neonate
could result in a dosing error. The panel does not say who the patient is: only the id travels,
and it is not shown. A weight measured at the bedside after the EHR's patient data was read is the better value;
today it lives in the plan the user signs, but the Session drops it at the sign and at the next
open, and the client drops it when a data notice is accepted.

## What the code does today

Read before planning; the EHR's patient data, the Session, the signature and the store are one path, and
every step below sits on it.

- **Two patient types.** `Informedica.GenCore.Lib.Patients.Patient` in `GenCORE.Lib/Patient.fs`
  is the person: an age that is a birthdate, an age value or unknown; weights and heights as
  dated measurements, at birth, on admission, and a separate one for calculation that nothing
  derives from the others; department, gender, gestational age and the accesses. Its header
  says it is not used in the production pipeline and is kept for reference and a future
  platform integration; nothing outside its own tests and one constant in `GenFORM.Lib/Utils.fs`
  reads it. Its shape has gaps the EHR's patient data will meet: `BirthDate` is partial, month and day
  optional, and `AgeValue.fromBirthDate` builds a `DateTime` from a partial one and throws;
  `Patient.SetGet.getAgeValue dt` answers an `AgeValue` of optional years, months, weeks and
  days, not a number of days; its `Dto.fromDto` does not read the department; it has no id and
  no name. It has a venous and an enteral access, as the projection has its access devices,
  the enteral tube among them. `GenForm.Patient` in `GenFORM.Lib/Types.fs` is the projection the rules match: an
  age, a weight and a height as single values, with flags saying which were measured.
  Everything from the port to the signed version carries the projection. `GenFORM.Lib` reaches
  `GenCORE.Lib` through its reference to `ZForm.Lib`; `GenPRES.Shared` references no project,
  so a type the wire carries is the contract's own.
- **The EHR's patient data.** `ServerApi.Ports.fs` declares `PatientDataPort` as `read: string
  -> GenForm.Patient option`: what the EHR hands over is the projection, with an age in days,
  no name and no birthdate. What the EHR will return is: the patient id, the full name, the
  birthdate and the gender; and, each optional, a weight, a height, a gestational age, a
  department, renal function or lab results, and vascular access, with a date on every value
  that changes over time. The only adapter is `StubPatientData` in
  `ServerApi.StubAdapters.fs`: one fixed ten-year-old for every id, none for `no-data`. There
  is no live adapter in the solution: ADR-0004, the FHIR integration, is superseded and its
  prototype deleted. Since #1056 the port is wrapped by `Patient.estimating`, in
  `ServerApi.Adapters.fs` where the session port is built, so EHR patient data with an age alone
  arrives with the estimate over the projection.
- **The open.** `Session.sessionPatient` takes the EHR's patient data first, the patient of the
  record's head when the EHR answers none, and nothing from nothing. What the store holds
  of an open Session, `OpenedSession`, carries the `PatientId`, a string, and that patient; the
  client receives them as `SessionOpened.PatientContext` in `Shared/Types.fs`, id and patient,
  through `Mappers.Session.toOpened`, and the session machine's `SetPatient` effect hands the
  patient to `updatePatient` as it would a url's or the panel's. From there the panel's draft
  is the patient, editable in every field.
- **Each request.** `Compute.bound` in `ServerApi.Compute.fs` runs every computing command:
  it reads the cookie, asks `env.session.seen` for a record notice, which is all `seen`
  answers, and never sees the Session or a patient; it is generic over the command, taking a
  `name` and a `gate` per family. The command handlers then parse the patient the request
  carries through `Mappers.Patient`. The Session's patient is not consulted: the age the
  client sends is the age the rules see. The formulary command carries a patient too, through
  `Patient.patientOption`, and the formulary matches on age; the interaction command carries
  none. Launch, session, signing and admin commands run through `Compute.logged`, not
  `bound`: `SigningCommand.processCmd` reads the cookie itself, checks every patient of the
  plan with `Patient.patient` and parses the plan with `OrderPlanCommand.parsePlan` before it
  asks the session port for a challenge or a commit. The session port has `find`, which
  answers the opened Session by id, and nothing today calls it per request.
- **The challenge.** `Session.challenge` reads the EHR again and compares the EHR's patient data with
  `Opened.Patient`, the patient the Session opened on; when they differ it answers a data
  notice instead of a challenge. The plan's own patient data is "what the User saw, entered or
  read, and is recorded as such", and the Patient of the version is the Session's.
- **The accept.** When the client accepts a data notice, the signing machine in
  `Client.Core/SigningMachine.fs` replaces the plan's patient with the notice's data and
  answers `SigningEffect.SetPatient data`, which the app interprets as `UpdatePatient`: the
  draft becomes the platform's patient, and a bedside weight typed before the notice is gone
  from the panel and from the plan that is then challenged.
- **The commit.** `Session.commit` writes the version and then sets the Session's patient to
  the EHR's patient data read at the challenge, else the plan's patient just signed: `Patient = challenge.Reading |>
  Option.defaultValue version.Plan.Patient`. A bedside weight in the plan is in the version and
  gone from the Session the moment it is signed, when the EHR read a different one.
- **The resume.** The session machine's `Resume` answers `SetPatient` with the Session's
  patient, which replaces the panel's draft. A bedside weight typed after the open is in the
  draft and in the requests, not in the Session, so a reload of the browser drops it today,
  for every launched patient. The next open of a Session starts from the EHR's patient data
  as well.
- **The version.** `OrderPlanVersion` in `GenORDER.Lib/Types.fs` stores the version's id and
  number, the patient id, the base, the signer, the time and the plan as signed, whose patient
  is the projection. `Session.commit` builds it from the Session's record. On the wire
  `SignedOrderPlan` carries the same, through `Mappers.Session.ofSigned` and `toSigned`.
  Nothing of it says who the patient is beyond the id.
- **The store.** `ServerApi.SqlAdapters.fs` applies the numbered scripts under `Sql/`. Three
  columns hold a patient as JSON under a structure version: `session_opened_with.patient`, the
  patient a Session shows, and, from `004-notices.sql`, `challenge.reading` and
  `data_notice.data`. The versions table holds the plan, with its projection, the same way.
  ADR-0008 section 6 has every stored shape carry a structure version and change by expand
  and contract; ADR-0007 section 3 has the working state carry GenORDER and GenFORM types.
- **The title bar.** `Components/TitleBar.fs` shows the user and their role behind a person
  button, for an open Session with a user, and nothing for anonymous use. No patient. A Reader
  sees everything a Prescriber sees but the sign button.
- **The panel.** `Views/Patient.fs` draws the age fields with `SimpleSelect`, whose `readOnly`
  prop exists and is `false` there; its reset is gated on `busy` only, and empties the whole
  draft. The url already carries a birthdate as three integer parameters, `by`, `bm` and `bd`,
  which `App.fs` reads into an age.
- **The setters** keep a measured value across an age edit since #1044 and #1045, and the panel
  shows and chooses the department since #1051 and #1052, with a launched patient's department
  shown but not chosen. The panel already knows whether a Session is open: `launched` in
  `Views/Patient.fs` reads the session view, and it is open for a `no-data` launch too.
- **What is printed of a patient.** `Patient.toString` in `GenFORM.Lib/Patient.fs` writes the
  department, gender, age, gestational age, weight, height and BSA, and nothing else; it is the
  one printer of the projection, and it reaches a debug line of the formulary service, the
  order context's own text, and the rule texts shown on screen. The core patient has its own
  `toString` and its own Dto, which nothing calls today.

## What the first half settled

- A setter keeps every measured value (#488), so an identified patient's bedside weight
  survives every other edit of the panel.
- The server estimates EHR patient data with an age alone (#716), so an identified patient with a
  birthdate and no weight gets the estimate.
- The department is chosen in the panel, or comes from the launch and is shown as such (#717).

## Approaches considered

**Where the age is computed.**

- *On the server, at the open and again right after each sign, from the birthdate the EHR's patient data
  carries; on every request in between, the sign included, the Session's age replaces the one
  the request carries, with no clock involved.* **Chosen.** The server signs and stores the
  version, so the age it evaluates against must be the one it computed; and the age stays the
  same from the open up to and including the sign, so the version signed carries the age every
  calculation behind it used, and the Session's next round, if there is one, starts on a fresh
  age. #976 says "on each request"; this plan answers the issue with the reason above.
- *On the server, again on each request.* Rejected: two requests minutes apart could then be
  over two ages, a neonate's day turning between them, and the plan the user is looking at
  would rest on an age the next request no longer uses.
- *On the client, from the birthdate.* Rejected in the issue: a client's clock and a client's
  arithmetic would be signed by the server.
- *One mode, the age read-only whenever the EHR's patient data is present.* Rejected in the issue: the
  EHR's patient data carries an age today, so it would go stale, and the identity would still be missing.

The chosen rule has no bound of its own: a Session open for thirty hours without a sign keeps
a neonate at yesterday's age, and nothing says so. Whether the challenge should treat a day
that turned since the open as it treats a change in the EHR's patient data, a data notice before the challenge,
is left to review; it would reuse the notice as it is.

**What the EHR's patient data carries.**

- *The EHR's patient data is two things: the core patient and the rule-only data.* **Chosen.**
  `PatientDataPort.read` answers `EhrPatientData option`: the core patient,
  `Patients.Patient`, which models the identity and the core data, the patient id and the
  name added to the type, the birthdate as its age, the gender, and the EHR's weight and
  height as dated measurements with the dates the EHR gives them; beside it the data only the
  rules read, renal function or lab results, and access, vascular and enteral. The GenFORM
  patient the rules match is derived from both at a date by one projection,
  `GenForm.Patient.ofEhr`, which becomes the core patient's first use in the pipeline and
  retires its header note: the age from `getAgeValue now`, the measures from the calculation
  values, the rule-only data as given, and BSA and post-menstrual age always calculated, never
  taken from anyone. The projection reads the calculation weight and height, so whatever
  writes a dated measurement writes the calculation value too, since the type derives
  nothing. The shape gaps are work of the first step, not left to be found: a partial
  birthdate is read without throwing, and a birthdate without a day or a month makes no
  identified patient; the age value's optional parts become the days the projection needs;
  the department is read.
- *One record of the server's own, name, birthdate and dated measurements beside the GenFORM
  patient.* Rejected, with its cost stated: it is the smaller change, since the core type
  carries the gaps above, but it re-invents the person the core type already is, and the
  core type is the domain's own statement of what a patient is. The reviewer of this plan
  recommended it; the choice for the core type is the project's.
- *A birthdate and a name on the GenFORM patient.* Rejected: that type is the projection the
  rules match; every rule module would carry fields it never reads, and its printer would have
  to be kept from printing them.

**Where the request's age is replaced.**

- *One function per command family, `aged: Age option -> 'cmd -> 'cmd`, that rewrites every
  contract patient the command carries; `Compute.bound` takes it beside `name` and `gate`,
  reads the opened Session by the cookie through the port's `find`, and applies the Session's
  age, none for a Session without an identity or no Session; `SigningCommand.processCmd`,
  which runs through `Compute.logged`, applies the same function to the plan before it checks
  and parses it.* **Chosen.** The rewrite is pure and per family, the mapper stays pure, and the
  two paths a patient can take into the server both pass it, so the signed version carries the
  Session's age and not the client's. The families that carry a patient are the order
  context, the order plan, the formulary and the signing; the interaction command carries
  none.
- *In `Compute.bound` alone.* Rejected: it is generic over the command and cannot reach a
  patient inside one, and the signing commands never pass through it, so the plan signed would
  keep the age the client sent while the calculations used the Session's.
- *In the mapper, with the Session as a parameter.* Rejected: `Mappers.Patient` is pure and
  tested as such; threading a Session through it reaches every caller.
- *Trusting the client's age for an identified patient.* Rejected by rule 1.

**What the Session compares at the challenge.**

- *The EHR's patient data as read.* **Chosen.** `OpenedSession` keeps the EHR's patient data as
  read beside the projection it opened on, and `challenge` compares a fresh read with that,
  not the projections. The projection's age is computed and the EHR's is not, so comparing
  projections would report a change for data that did not change.
- *Computing the fresh read's age too, at the same moment, and comparing projections.*
  Rejected: the moment differs from the open's, and the comparison would carry the clock.

**What a bedside measurement means across a notice, a commit, a reload and a re-open.**

- *A measured value the user entered stays for the Session, whatever the EHR reads later;
  the version records it; the data notice still tells that the EHR's patient data changed.*
  **Chosen**, as #976 decided. The value is the user's by rule 3, the notice is the user's to
  see by rule 1. Two existing flows break this and both change: the accept of a data notice
  merges instead of replacing, the identity and the age from the EHR's patient data and the measured
  values kept, on the client and in the plan it challenges; and the commit sets the Session's
  patient to the same merge, the EHR's identity and the fresh age with the Session's
  measured values, instead of the EHR's patient data alone.
- *The Session records the measured weight, height and gestational age a request carries, as
  dated actual measurements on the core patient it holds, so that a resume restores them.*
  **Chosen**, with the write bounded: the panel dispatches the whole draft on every edit, each
  edit reaches the server as a request, and the store is append-only, so a write per request
  would be a row per edit with unvalidated intermediate values. The Session records a
  measurement only when the request's patient is a patient, and only when the value differs
  from the last one it holds; the row is the measurement's own, not a new opened-with row.
  Whether that is worth its rows, or a write at the challenge alone should do until #598 gives
  the client a memory of its own, is left to review.
- *At the next open, the head's measured values over the platform's.* Not decided here: the
  EHR's patient data is the source of truth at an open, and the head may be days old. Left to
  review, with the plan's default being the EHR's patient data.

**What the signed version stores.**

- *The identity beside the patient id, the plan carrying the projection as today.* **Chosen.**
  `OrderPlanVersion` gains `Identity: PatientIdentity option`, none for a version signed in a
  Session that had none; `Session.commit` writes the Session's, never the request's; the wire's
  `SignedOrderPlan` carries it through the session mapper; the versions table gains it by one
  migration, nullable, since every row before it has none and the store never rewrites a row.
  A version read later then says who the patient was, on its own, and the plan inside it is
  what the rules saw.
- *The identity in the Session only.* Rejected: a version reopened from the record, or listed,
  would name an id and nobody; and a Session that opens from the head when the platform
  answers none would open without one.

**How the identity reaches the client.**

- *On `SessionOpened.PatientContext`, beside the id and the projection: the name and the
  birthdate as the contract's own record, the birthdate as three integers, year, month and
  day.* **Chosen.** The Session already carries the projection there; the title bar already
  reads the Session through the environment; the url already carries a birthdate as `by`,
  `bm` and `bd`. A `DateTime` at midnight can move a day through UTC serialization and the
  browser's zone, and a wrong birthdate in the title bar misidentifies a patient.

**How the client takes the age after a sign.**

- *The `Submitted` answer carries the Session's patient beside the fresh token, and the
  session machine hands it to the panel as `SetPatient` does at a resume.* **Chosen**, with
  the cost named: the app interprets `SetPatient` as `UpdatePatient`, which re-evaluates the
  workbench, the formulary and the parenteralia over the new patient. After a sign that is the
  right thing to do, since the age the calculations rest on has changed and rule 1 forbids
  showing calculations over an age that has passed; the plan signed is untouched. Whether an
  age-only update that touches less is worth building is left to review.

## Chosen approach

- ADR-0009 trimmed with this plan: its fourth section, the domain decisions taken under the
  rules, removed, and the decisions left in their issues and plans.
- The EHR's patient data is two things: the core patient, `Patients.Patient`, the patient id
  and the name added to the type, the person with a birthdate, a gender and dated
  measurements; and the rule-only data, renal function and access. The Session keeps both as
  read, and derives the GenFORM patient from them at a date with one projection,
  `GenForm.Patient.ofEhr`, BSA and post-menstrual age calculated in it; the estimate is applied
  over the projection where the port's wrap applies it today.
- At the open, an identified patient's age is `Patient.SetGet.getAgeValue now` over its
  birthdate, the EHR's own age value ignored when it has both. That age is the Session's up
  to and including its next sign. The challenge compares the EHR's patient data as read.
- On each request of an identified Session, on both paths into the server, the age of every
  patient the request carries is replaced by the Session's, before the mapper parses it; no
  clock is read. A measured weight, height or gestational age the request carries is kept,
  and recorded once, validated and when it changed, as a dated actual and calculation value on
  the Session's core patient, so that a resume restores it.
- The accept of a data notice and the commit both merge: the EHR's identity and age, the
  Session's measured values.
- The version signed carries the Session's age, the one its calculations used, and the
  identity beside the patient id. Right after the commit the Session's age is computed again
  from the birthdate, the projection and the estimate follow, and the client receives the
  Session's patient with the fresh token.
- The store's three patient columns and the versions table change by expand and contract
  under a new structure version, the EHR's patient data as the core patient's Dto beside the rule-only data.
- The contract carries the identity on `PatientContext` and on `SignedOrderPlan`, the
  birthdate as three integers. The client shows it in the title bar beside the user, for a
  Reader as for a Prescriber; the panel in identified mode, decided by the context carrying an
  identity and not by a Session being open, shows the age held, keeps the identity and the age
  through its reset, and leaves every other field as it is. A launched patient the platform
  has no data for opens a Session without an identity and is anonymous; so is a patient the
  MCP host builds; the panel is unchanged for both.
- The identity is shown in full to the user, kept by the Session, its store and the versions,
  never on the projection, and never in a debug or log line.

## Confidence

Medium. The EHR's patient data, the Session and the request path are well delimited, and each step is a
rule that a script can state and prove. Three things keep it from high. The core patient has
never been in the pipeline, and its shape has the gaps listed above, each of which the first
step must close before anything reads it. There is no live platform adapter, so the EHR's patient data is
decided against the stub alone, and the mapping of a platform's record onto the core patient is
a follow-up when an adapter exists. And the request-time replacement of the age touches the
one place every computing command passes through, so it must be proved not to change an
anonymous request at all.

## Steps

One pull request each, in this order. Everything outside `src/Informedica.GenPRES.Client/` is a
script first and a migration after; a migration that spans more than four areas is two pull
requests, and the steps say which.

0. **The ADR trimmed**, in the pull request that brings this plan. ADR-0009 loses its fourth
   section, "Decisions already taken under the rules", the four domain decisions of #404 and
   #394, #976, #977 and #978, and the two passages in its consequences and alternatives that
   count them; the ADR keeps the three rules, what each means on a screen and how they
   conflict, and a dated amendment in the section's place saying where the decisions live. The
   grouping index says the same.
1. **The EHR's patient data as the core patient and the rule-only data, as a script.** A
   script in `src/Informedica.GenPRES.Server/Scripts/` shadows `Patients.Patient`,
   `GenForm.Patient`, `Ports`, `StubPatientData` and `Mappers.Session`: the patient id and the
   name on the core patient; `EhrPatientData`, the core patient beside the renal function and
   the access; the partial birthdate read without throwing, and none of it an identity when
   the day or the month is missing; `GenForm.Patient.ofEhr now`, the projection at a date, the
   age from `Patient.SetGet.getAgeValue now` turned into days, the measures from the
   calculation values, the rule-only data as given, BSA and post-menstrual age calculated,
   the department read, the rest field for field; the port's `read` answering the EHR's
   patient data; the stub building it for its fixed patient, a constant birthdate, a name and
   a gender, and none for `no-data`; `OpenedSession` carrying the EHR's patient data as read
   beside the projection; the contract's `PatientIdentity` in `Shared/Types.fs`, name and
   birthdate, the birthdate as three integers, on `PatientContext`. Proved: the projection of the stub's patient at a
   fixed date is the patient the stub answers today, field for field; the EHR's patient data round-trips to
   the client's context with its name and birthdate; a `no-data` launch has none; EHR patient
   data with a partial birthdate opens anonymous; the core printer and the projection's printer
   write neither the name nor the birthdate.
2. **The EHR's patient data migrated**, in two pull requests. First the identity and the data into
   `GenCORE.Lib`, the projection into `GenFORM.Lib`, the identity into `GenPRES.Shared`, the
   port, the stub and the mapper into the server, with the tests. Then the store: the core
   patient's Dto in `session_opened_with.patient` under a new structure version, read back by
   both versions, per ADR-0008 section 6, with the EHR's patient data kept beside the projection.
3. **The age computed at the open, as a script.** `sessionPatient` shadowed: the projection at
   `now` of the EHR's patient data, so an identified patient opens on the age the birthdate
   gives and the EHR's own age value is ignored; `now` is the clock the Session already takes; the
   estimate applied over the projection here, and the port's wrap in `ServerApi.Adapters.fs`
   gone. `challenge` shadowed to compare the EHR's patient data freshly read with what the Session kept,
   not the projections. Proved: EHR patient data with the stub's constant birthdate opens on the age
   that birthdate gives at the clock's date whatever age value it carried; EHR patient data without a
   birthdate opens as today; the estimate is the one for the computed age; a challenge over an
   unchanged read is a challenge, not a data notice, and one over a changed read is a
   notice.
4. **The open migrated**, with the tests in the Server test project, and `challenge.reading`
   and `data_notice.data` under the new structure version, read back by both.
5. **The age on each request, as a script.** `aged` per command family, the pure rewrite of
   every contract patient a command carries; `Compute.bound` taking it beside `name` and `gate`,
   reading the opened Session through the port's `find`, and applying the Session's age, no
   clock read; `SigningCommand.processCmd` applying it to the plan before the check and the
   parse. Proved over the order-context, order-plan, formulary and signing commands: an
   identified request with a mistyped age is evaluated at the Session's, and the plan handed
   to the session port for a challenge carries it; two requests of one Session evaluate at one
   age whatever the clock says between them; a request without a Session, or in a Session
   without a birthdate, is untouched, field for field; a measured weight in the request
   survives the rewrite.
6. **The request migrated**, with the tests in the Server test project. With it the age the
   client sends stops mattering for an identified patient, on both paths.
7. **The measurement recorded, as a script and then migrated.** The session port recording a
   measured weight, height or gestational age a request carries, as a dated actual and the
   calculation value on the Session's core patient, only for a request whose patient is a
   patient and only when the value differs from the one held, in a row of its own. Proved: a
   measured weight in a request is the latest actual on the Session's core patient afterwards;
   the same weight again writes nothing; a draft that is no patient writes nothing; a resume
   restores the measurement. Two pull requests.
8. **The version with the identity, and the age after the sign, as a script.**
   `OrderPlanVersion.Identity` in the domain, `SignedOrderPlan.Identity` on the wire, the
   session mapper carrying it both ways; `Session.commit` writing the Session's identity into
   the version and, once the version has landed, setting the Session's patient to the merge,
   the EHR's identity with the age computed again at `now`, the projection and the
   estimate over it, and the Session's measured values, instead of the EHR's patient data alone, and
   answering the Session's patient beside the fresh token; the versions table's identity by a
   new numbered migration, nullable. Proved: a version signed in an identified Session reads
   back with the name and the birthdate the Session had, and the plan inside it with the
   projection the rules saw, at the age the Session had; one signed in a Session without an
   identity reads back with none; a row written before the migration reads as none; a Session
   that opens from the head, the EHR answering none, opens with the head's identity;
   after a commit with the clock a day on, the Session's age is a day more and the version's
   is not; after a commit with the EHR answering another weight, the Session's weight is
   the one the user measured.
9. **The version migrated**, the domain field into `GenORDER.Lib`, the wire field into
   `GenPRES.Shared`, the mapper, the commit and the migration into the server, the tests into
   the three test projects; two pull requests if the size limit asks.
10. **The title bar and the panel.** `Components/TitleBar.fs` shows the identity beside the
    user for a Session whose context has one, for a Reader as for a Prescriber: name, birthdate
    and id. `Views/Patient.fs` decides its mode by that identity, not by a Session being open,
    since a `no-data` launch opens a Session without one: in identified mode it draws the age
    fields with `readOnly`, keeps the identity and the age through its reset, and its summary
    line names the patient; every other field stays as it is. The signing machine's accept
    merges the notice's data into the draft, identity and age from the EHR's patient data and the
    measured values kept, and the session machine takes the patient the `Submitted` answer
    carries as it takes a resume's. Anonymous mode is unchanged, for the anonymous url and for
    the `no-data` launch alike. Closes #976.

## Verification, per step

- Every script step: `dotnet fsi` on the script, with an explicit check that the Expecto run
  reports `Status: Ok`, and the script left in the repository.
- Every migration step: `dotnet run build`, `dotnet run servertests` and
  `dotnet fsi scripts/CheckDependencyRule.fsx`, each checked for its success line. A step that
  adds a project reference also runs `dotnet fsi scripts/ProjectGraph.fsx --write`.
- Every client step: `dotnet run clientbuild`, the generated JSX read for the element that
  changed, and then the browser, by the user.
- Steps 1 and 2 also: the server's log of a stub launch, read for the name and the birthdate,
  which must not appear; and a development database written before the structure version
  bump, read back after it.
- Steps 3 and 4 also: a stub launch as the prescriber, a challenge requested without any change
  in the EHR, and the answer read: a challenge, not a data notice; the same at a clock
  the day after the open.
- Steps 5 and 6 also: the anonymous path proved untouched by a test that compares the parsed
  patient of a request without a Session before and after, field for field, for every family
  that carries one; and the version a stub Session signs, read back, carrying the Session's
  age and the typed weight.
- Step 7 also: the rows the session store gains over a panel edited ten times to the same
  weight, counted: one.
- Steps 8 and 9 also: the versions table of a development database, read after a signature,
  carrying the name and the birthdate; and the same database after the migration, its older
  rows reading as none.
- Step 10 also, in the browser: a stub launch as the prescriber shows the fixed patient's name
  and birthdate in the title bar and a held age in the panel, and as the reader the same;
  the anonymous url and the `no-data` launch both show the panel as today, the age editable;
  a weight typed in the identified panel survives a reload of the browser, a data notice
  accepted and a signature, and is in the signed version; after the signature the panel
  shows the age the clock gives.

## To settle in review

- **A day that turned without a sign.** Whether the challenge should answer a data notice
  when the age at that moment differs from the Session's, as it does for a change in the EHR's patient data,
  so that a long Session cannot sign a neonate at yesterday's age unaware. The plan leaves the
  age still until the sign, as decided, and asks.
- **The core patient over the wrapper.** The reviewer recommended the wrapper as the smaller
  change; the plan keeps the core patient and closes its gaps in step 1. Confirm.
- **The measurement's write.** Validated and on change, in a row of its own, as the plan has
  it; or at the challenge alone, accepting the reload loss until #598.
- **The next open.** Whether the head's measured weight, height and gestational age should be
  offered over the EHR's patient data when the head was signed after the EHR's data was
  read, or the EHR's patient data always wins at an open. The plan's default is the EHR's patient data.
- **The update after a sign.** The plan re-evaluates everything over the new age through
  `UpdatePatient`; whether an age-only update that keeps the lists is worth building.
- **The live adapter.** What a platform's patient record maps onto the core patient: which of
  its names, what a missing birthdate means, which of its weights are actuals. Decided when an
  adapter exists; ADR-0004's FHIR prototype is gone.

## Related, not a member

- **#718** a weight alone, or a height alone, as the minimum: still waiting for a growth table.
- **#598** client testing, which would let step 10 prove the two modes without the browser,
  and would let the client remember a measurement itself.
- **ADR-0004** the superseded FHIR integration, the last word on a live platform adapter.
- **ADR-0007** and **ADR-0008**, the store and the structure version every stored shape here
  changes under.
