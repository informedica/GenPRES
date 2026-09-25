# Implementation plan for issue #976

The second half of G5, the patient panel: two patient modes, decided by the launch. An
*identified* patient is a specific person the platform has data for, with a name and a
birthdate; an *anonymous* patient is the one filled in at the keyboard. Companion to
[the patient panel plan](986-the-patient-what-is-entered-estimated-hidden.md), whose repairs this
plan builds on, and to [the grouping index](ux-issue-grouping.md).

Held to [ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md), which records the decision
of #976 in its fourth section: a launched patient's age is a fact the server computes on each
request and the title bar says who they are; weight, height and gestational age stay editable
and a measured value is kept; without a launched patient the panel is the one that is filled in.
Rule 1 makes the computed age unwritable, rule 2 makes it unnecessary to type, rule 3 keeps the
measured values in the user's hands.

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
the client the platform's reading, and the panel shows an age in years, months, weeks and days
that the user can change, as if the patient were made up at the keyboard. The reading carries an
age, not a birthdate, so over a long session the age goes stale by the day, which for a neonate
is a dosing error. The panel does not say who the patient is: only the id travels, and it is not
shown. A weight measured at the bedside after the reading is the better value; today it lives
in the plan the user signs, but the next open of a Session starts from the platform's reading
again.

## What the code does today

Read before planning; the reading, the Session and the signature are one path, and every step
below sits on it.

- **The reading.** `ServerApi.Ports.fs` declares `PatientDataPort` as `read: string ->
  GenForm.Patient option`: what the platform hands over is a domain patient, with an age in
  days, no name and no birthdate. The only adapter is `StubPatientData` in
  `ServerApi.StubAdapters.fs`: one fixed ten-year-old for every id, none for `no-data`. There
  is no live adapter in the solution: ADR-0004, the FHIR integration, is superseded and its
  prototype deleted. Since #1056 the port is wrapped at the composition root by
  `Patient.estimating`, so a reading with an age alone arrives with the estimate.
- **The open.** `Session.sessionPatient` takes the platform's reading first, the patient of the
  record's head when the platform answers none, and nothing from nothing. What the store holds
  of an open Session, `OpenedSession`, carries the `PatientId` and that patient; the client
  receives them as `SessionOpened.PatientContext`, id and patient, through
  `Mappers.Session.toOpened`, and the session machine's `SetPatient` effect hands the patient
  to `updatePatient` as it would a url's or the panel's. From there the panel's draft is the
  patient, editable in every field.
- **Each request.** `ServerApi.Compute.fs` looks the Session up by the cookie on every command
  and asks `env.session.seen` for a record notice; the command handlers then parse the patient
  the request carries through `Mappers.Patient`. The Session's patient is not consulted: the
  age the client sends is the age the rules see.
- **The signature.** `Session.challenge` re-reads the platform and, when the reading is not the
  one the Session opened on, answers a data notice instead of a challenge; the plan's own
  patient data is "what the User saw, entered or read, and is recorded as such", and the Patient
  of the version is the Session's. So a bedside weight typed over the reading is already
  recorded in the version. What is not kept is the next open: `sessionPatient` prefers the
  platform's reading, whose weight is the platform's.
- **The title bar.** `Components/TitleBar.fs` shows the user and their role behind a person
  button, for an open Session with a user, and nothing for anonymous use. No patient.
- **The age from a birthdate** exists in the domain: `AgeValue.fromBirthDate now birthDate` in
  `GenCORE.Lib/Patient.fs`, over `Calculations.Age.fromBirthDate`, answering years, months, weeks
  and days. Nothing calls it from the server.
- **The setters** keep a measured value across an age edit since #1044 and #1045, and the panel
  shows and chooses the department since #1051 and #1052, with a launched patient's department
  shown but not chosen. The panel already knows whether a Session is open: `launched` in
  `Views/Patient.fs` reads the session view.
- **Logging.** The coding rules forbid logging patient-identifying data. A name and a birthdate
  are exactly that, and today nothing on the server holds either, so no log line can. Every
  step below that carries them must keep them out of every message template.

## What the first half settled

- A setter keeps every measured value (#488), so an identified patient's bedside weight
  survives every other edit of the panel.
- The server estimates a reading with an age alone (#716), so an identified patient with a
  birthdate and no weight gets the estimate at the open and on each request.
- The department is chosen in the panel, or comes from the launch and is shown as such (#717).

## Approaches considered

**Where the age is computed.**

- *On the server, at the open and again on each request, from the birthdate the reading
  carries.* **Chosen**, as the ADR decided: the server signs and stores the version, so the age
  it evaluates against must be the one it computed, and a neonate's age moves by the day.
- *On the client, from the birthdate.* Rejected in the issue: a client's clock and a client's
  arithmetic would be signed by the server.
- *One mode, the age read-only whenever a reading is present.* Rejected in the issue: the
  reading carries an age today, so it would go stale, and the identity would still be missing.

**What the reading carries.**

- *A reading is an identity and a patient: name and birthdate beside the domain patient.*
  **Chosen.** The identity is not a domain concept: no rule reads a name, and the birthdate is
  read once into an age. It stays at the edge, on the port and in the Session, and never enters
  `GenForm.Patient`, which keeps the domain free of data it must not log.
- *A birthdate on the domain patient.* Rejected: every rule module would carry a field it never
  reads, and the export, the Dto and the signed version would carry it too.

**Where the request's age is replaced.**

- *In the command layer, once, for every command that carries a patient: the Session's
  birthdate, when it has one, overwrites the age of every contract patient before the mapper
  parses it.* **Chosen.** One function at the inbound boundary, next to where the Session is
  already looked up; the mapper stays pure and the handlers stay as they are.
- *In the mapper, with the Session as a parameter.* Rejected: `Mappers.Patient` is pure and
  tested as such; threading a Session through it reaches every caller.
- *Trusting the client's age for an identified patient.* Rejected by rule 1.

**What a bedside measurement means across a re-read.**

- *A measured value the user entered stays for the Session, whatever the platform reads later;
  the version records it; the data notice still tells that the platform's reading changed.*
  **Chosen** for the Session, as the ADR decided. The value is the user's by rule 3, the notice
  is the user's to see by rule 1.
- *At the next open, the head's measured values over the platform's.* Not decided here: the
  platform's reading is the source of truth at an open, and the head may be days old. Left to
  review, with the plan's default being the reading.

**How the identity reaches the client.**

- *On `SessionOpened.PatientContext`, beside the id and the patient.* **Chosen.** The Session
  already carries the patient there; the title bar already reads the Session through the
  environment.

## Chosen approach

- A platform reading is an identity, when the platform knows one, and a patient: `PatientReading
  = { Identity: PatientIdentity option; Patient: GenForm.Patient option }`, with `PatientIdentity
  = { Name: string; BirthDate: DateTime }` at the server's edge and, in the contract, on
  `PatientContext`.
- At the open, an identified reading's age is computed from the birthdate and the reading's own
  age is ignored; the estimate follows, as it does today.
- On each request of an identified Session, the age of every patient the request carries is
  replaced by the one computed from the Session's birthdate at that moment, before the mapper
  parses it. A measured weight, height or gestational age the request carries is kept.
- The client shows the identity in the title bar beside the user, and the panel in identified
  mode shows the age and does not offer to change it; weight, height, gestational age, gender,
  access, renal function and department stay as they are. A launched patient the platform has
  no data for, and a patient the MCP host builds, are anonymous and the panel is unchanged.
- The name and the birthdate are never logged, never on a domain patient, never in a signed
  version's patient data beyond what the version carries today.

## Confidence

Medium. The reading, the Session and the request path are well delimited, and each step is a
rule that a script can state and prove. Two things keep it from high. There is no live platform
adapter, so the identity's shape is decided against the stub alone, and the mapping of a
platform's name and birthdate is a follow-up when an adapter exists. And the request-time
replacement of the age
touches the one place every command passes through, so it must be proved not to change an
anonymous request at all.

## Steps

One pull request each, in this order. Everything outside `src/Informedica.GenPRES.Client/` is a
script first and a migration after.

1. **The reading with an identity, as a script.** A script in `src/Informedica.GenPRES.Server/
   Scripts/` shadows `Ports`, `StubPatientData` and `Mappers.Session`: `PatientIdentity` and
   `PatientReading`; the port's `read` answering a reading; the stub answering the identity of
   its fixed patient and none for `no-data`; `OpenedSession` carrying the identity;
   `PatientContext` carrying it on the wire. Proved: a reading round-trips to the client's
   context with its name and birthdate; a `no-data` launch has none; no log line of the server
   names either.
2. **The reading migrated**, with the contract's `PatientIdentity` in `GenPRES.Shared`, the port,
   the stub, the store's `OpenedSession` and the mapper, and the tests in
   `tests/Informedica.GenPRES.Server.Tests/` and `tests/Informedica.GenPRES.Shared.Tests/`.
   The stub's launch page gains no patient; its one fixed patient gets a name and a birthdate.
3. **The age computed at the open, as a script.** `sessionPatient` shadowed: for a reading with
   an identity, the patient's age is `AgeValue.fromBirthDate now` over the birthdate, the
   reading's own age ignored; `now` is the clock the Session already takes. Proved: a reading
   with a birthdate ten years back opens on a ten-year-old whatever age it carried; a reading
   without an identity opens as today; the estimate that follows is the one for the computed
   age.
4. **The open migrated**, with the tests in the Server test project.
5. **The age on each request, as a script.** One function at the inbound boundary, next to the
   Session lookup in `Compute.fs`: for a Session with an identity, every contract patient the
   command carries gets its age from the birthdate at `now`, and every measured value it
   carries stays. Proved over the order-context and order-plan commands: an identified request
   with a mistyped age is evaluated at the computed one; a request without a Session, or in a
   Session without an identity, is untouched, field for field; a measured weight in the request
   survives.
6. **The request migrated**, with the tests in the Server test project. With it the age the
   client sends stops mattering for an identified patient, which is what makes the next step
   safe.
7. **The title bar and the panel.** `Components/TitleBar.fs` shows the identity beside the user
   for a Session whose context has one: name, birthdate and id. `Views/Patient.fs` in identified
   mode shows the age as the server computed it, held, not offered for change, and its summary
   line names the patient; every other field stays as it is. Anonymous mode is unchanged, which
   the same view proves by the session view being closed. Closes #976.

## Verification, per step

- Every script step: `dotnet fsi` on the script, with an explicit check that the Expecto run
  reports `Status: Ok`, and the script left in the repository.
- Every migration step: `dotnet run build`, `dotnet run servertests` and
  `dotnet fsi scripts/CheckDependencyRule.fsx`, each checked for its success line. A step that
  adds a project reference also runs `dotnet fsi scripts/ProjectGraph.fsx --write`.
- Every client step: `dotnet run clientbuild`, the generated JSX read for the element that
  changed, and then the browser, by the user.
- Steps 1 and 2 also: the server's log of a stub launch, read for the name and the birthdate,
  which must not appear.
- Steps 5 and 6 also: the anonymous path proved untouched by a test that compares the parsed
  patient of a request without a Session before and after, field for field.
- Step 7 also, in the browser: a stub launch as the prescriber shows the fixed patient's name
  and birthdate in the title bar and a held age in the panel; the anonymous url shows the panel
  as today; a weight typed in the identified panel survives a reload of the workbench and is
  in the signed version.

## To settle in review

- **The birthdate on the wire.** The plan carries a `DateTime` at midnight; a date-only type
  would be truer but Fable and the JSON both handle `DateTime` today.
- **The next open.** Whether the head's measured weight, height and gestational age should be
  offered over the platform's reading when the head was signed after the reading was taken, or
  the reading always wins at an open. The plan's default is the reading.
- **The live adapter.** What a platform's patient record maps to `PatientIdentity`: which of
  its names, and what a missing birthdate means. Decided when an adapter exists; ADR-0004's
  FHIR prototype is gone.
- **The age on screen during a long session.** The panel shows the age of the last reply; a
  neonate's day turns without a request. Whether the client should ask again on a timer, or the
  next request is soon enough.
- **A Reader's Session.** Whether a Reader sees the identity as a Prescriber does.

## Related, not a member

- **#718** a weight alone, or a height alone, as the minimum: still waiting for a growth table.
- **#598** client testing, which would let step 7 prove the two modes without the browser.
- **ADR-0004** the superseded FHIR integration, the last word on a live platform adapter.
