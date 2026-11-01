# Implementation plan for issue #1599: one patient, stored once, at the sign

2026-11-01, second draft after review. Based on #1599 and on the patient data rules of plan 1430.
The plan states the rules first; the steps follow from them. The store has no running instance, so
the schema changes in place and no data is copied.

## Words

- **Another patient**: a different identity. Only a launch from the EHR opens another patient, in a
  new session. The orders of the previous patient are not carried over.
- **A patient data update**: the same patient with other data: a weight typed in the panel, a new
  estimate, a fresh EHR reading. The patient stays, the orders stay.
- **Unsigned orders on the plan**: new or changed orders not yet signed. Today a patient data update
  is blocked while they exist, so that they stay on the data they were composed on (plan 1075).
  Plan 1430 step 7 lets them follow the data instead.
- **The data notice**: the answer to a request to sign when the EHR's data differ from the data the
  session opened on. It shows the EHR's data and asks the clinician what to do before any PIN.
- **The inactive-tab answer**: the answer to a request from a tab whose token is not the one the
  session holds. It says that another tab has taken the session over.
- **The session gate**: the full-page message the client shows in place of the application when
  there is no session to work in, with what the user can do next. Today it shows an ended session
  or a refused launch.

Issue #1599 is about a tab that still shows one patient while the browser's cookie names the session of
another patient. The requests at fault are patient data updates, sent by the old tab for its own
patient and landing on the session of the other.

## Problem description

The server holds a copy of the patient beside the client's: the EHR projection at the open, the
user's measurements in rows of their own, and a patient JSON on the session. The copy is written on
every computing request for the session the cookie names. Tabs of one browser share the cookie, so a
weight typed for patient A in a tab left behind lands on the session of patient B. The age is
answered from the same copy, so a patient data update from the old tab comes back with the age of
the other patient.

The copy exists only so that the server can rebuild a patient from the EHR reading plus the user's
values: at a resume, for the data notice, at a refresh, and for the patient it sends back after a
sign. When the client's patient is the one patient and the signed version is its record, nothing
needs rebuilding.

## The rules

1. **One patient, the clinician's.** The plan's patient carries the data, the measurements and the
   age. The client holds it until the sign. A value the clinician types and an EHR value the
   clinician accepts are the same thing: a deliberate choice. No field is marked as typed and
   nothing is merged, on the server or on the client.
2. **Stored once.** The patient is stored when the plan is signed, inside the version, as it is
   today. Before the sign nothing of the user's reaches the store.
3. **The session holds no patient data.** A session holds the user, the patient id, the identity
   as the EHR gave it, the EHR data as read, the token, the key thumbprint and the head it opened
   with. The EHR data as read is what a fresh read is compared against for the data notice. The
   patient shown at the open is computed from it when it is asked for. The identity stays where it
   is today: on the session's EHR data and in the version's own columns, never on the patient.
4. **The age is set by the server and trusted at the sign.** The server computes the age from the
   birthdate at the open, at a resume, at a refresh and after a sign, and sends it with the patient.
   The panel keeps it read-only for an identified patient. No age is stamped on a request, and the
   sign records the age the plan carries. The post-menstrual age follows the age everywhere.
5. **The data notice is a choice, not a merge.** When a fresh read differs from the EHR data as
   read, the notice shows the EHR's data next to the clinician's. The clinician takes the EHR's
   data or keeps the plan's; either answer returns the notice token and asks the challenge again.
   The EHR's data has no age: the age is the birthdate's at a date, and the date is the session's
   (rule 4). So the dialog shows no age and the client ignores the one the notice's data carries;
   taking the EHR's data replaces every other field, the gestational age included, and the client
   keeps the age it holds. The mark as an adult is the user's alone, since the EHR never sets it: it stays, and the
   dialog does not show it. With unsigned orders on the plan, taking the EHR's data ends the
   signature with no challenge: the new and changed orders go and the Session reads the EHR again,
   as a refresh does. A notice that could not be verified has no data to take, so it can only be
   kept or cancelled. Nothing is applied after the sign.
6. **After a sign, the patient is the signed one.** The server answers the signed plan's patient
   with the age computed again at the sign. Estimates are not recomputed; a new estimate from a new
   age is plan 1430 step 8.
7. **A resume is read-only, and shows the latest act.** GetSession reads no EHR and mints no
   token. What the session stands on is the newest row of what it opened with, and that row says
   which act wrote it: the open, a version opened, a refresh or a sign. When the latest act is a
   sign, the panel shows the patient of the version signed; otherwise the EHR data as read,
   projected at the open. In both cases the age is computed at the resume. No time is compared:
   the order of the rows is the order of the acts. Without EHR data and without a head, the
   patient has the identity and nothing else, as today. Unsigned orders are gone, as today; a
   typed value not signed is gone with them.
8. **A refresh is the EHR read again.** The server answers the EHR projection at the refresh, age
   included, and the client takes it as a whole, dropping the new and changed orders, as today.
9. **The version records what was typed against.** The version holds the plan's patient as signed
   and the EHR data as read at the challenge. A typed value is readable from the record as the
   difference between the two. `Verified` stays.
10. **A request from a tab that is not the active one changes nothing and is told.** A computing
    request whose token is not the session's own writes no patient data (there is none to write),
    takes no age (none is taken), and gets the inactive-tab answer. The tab then shows the session
    gate, saying that another tab took the session over, and does not close the session, which is
    the other tab's. `OpenVersion`, `Refresh` and
    `CloseSession` carry the token and refuse a token that is not the session's; a close without a
    token still acknowledges an ending; a refused close leaves the cookie alone.

## What goes

- The `measurement` table, the `Measurements` type and module, `Opened.Measured`, the measurement
  rows in the loader, the writer and the audit, and the draft parameter of `seen`.
- `Opened.Patient` and the `patient` and `json_version` columns of `session_opened_with`.
- The age port member, `Session.age`, `Patient.aged`, the age read in `PatientCommand` and in
  `SigningCommand`.
- `patientOf` on the commands and the patient parameter of `Compute.bound`.
- `merged`, `noticeData`, the merge in `refresh` and at the commit; `sessionPatient` stays as the one
  projection, with the age recomputed from the identity where it falls back to the head.
- The `held` flag on `AcceptDataChange`, replaced by the two choices of rule 5.

## What stays, and why

- `EhrData` on the session and the `ehr_data` column: rule 3.
- The identity on the session, `PatientContext`, `SignedOrderPlan.Identity` and the four identity
  columns of `order_plan`: rule 3. Moving the identity onto the patient is not part of this plan.
- `Verified` on the version: rule 9.
- The order context's patient: the domain computes with it. Whether it follows the plan's patient is
  plan 1430 rule 6.

## The store after the change

No store runs, so the migration files are edited in place: `008-measurement.sql` and
`010-marked-adult.sql` are deleted, `002-session.sql` loses two columns and gains one, `004-notices.sql`
loses the two columns of the challenge's reading, and the comments of 006 and 007 follow. The schema test that lists the migrations follows. The tables that hold patient data, after
the change:

```sql
-- 001-order-plan.sql, unchanged. The version is the one record of a patient: the plan JSON
-- holds the plan with its patient as signed and, new, the EHR data as read at the challenge.
-- The JSON structure version is raised for that field. The identity columns of migration 009
-- stay: they name the patient of a version whose JSON cannot be read.
create table order_plan (
    id                     integer primary key,
    version_id             text not null unique,
    no                     integer not null,
    patient_id             text not null,
    base                   text null,
    signed_by_user_id      text not null,
    signed_by_display_name text not null,
    signed_at              integer not null,
    verified               integer not null, -- 0 | 1: the platform's reading at the challenge
    json_version           integer not null, -- raised: the plan now carries the challenge's EHR data
    plan                   text not null,    -- json: OrderPlanVersion.Dto, patient and EHR data inside
    patient_name           text null,        -- migration 009, unchanged
    birth_year             integer null,
    birth_month            integer null,
    birth_day              integer null,
    unique (patient_id, no)
);

-- 002-session.sql. The session row is unchanged: the patient id is the key, nothing of the
-- patient beside it.
create table session (
    id             integer primary key,
    session_id     text not null unique,
    login          text null,
    user_id        text null,
    user_display   text null,
    user_role      text null,
    patient_id     text null,
    key_thumbprint text null,
    opened_at      integer not null
);

-- What the Session opened with: the act that wrote the row, the version, the head, the token
-- and the EHR data as read. A row per act, the newest counts; its act tells a resume whether
-- a sign was the latest thing that happened. The patient JSON and its structure version are
-- gone; the patient shown is computed from ehr_data, the head and the date. ehr_data stays
-- for the data notice: a fresh read is compared against it. The two EHR columns come from
-- migration 006, as today.
create table session_opened_with (
    id               integer primary key,
    session_id       text not null references session (session_id),
    act              text not null check (act in ('opened', 'version', 'refresh', 'signed')),
    version_id       text null,          -- null: from nothing
    head_id          text null,          -- null: the patient had no order plan version
    opened_token     text null,
    ehr_json_version integer null,       -- null: opened without EHR data
    ehr_data         text null,          -- json: EhrPatientData.Dto, as read
    at               integer not null,
    check ((ehr_data is null) = (ehr_json_version is null))
);
create index ix_session_opened_with on session_opened_with (session_id, id);

-- 004-notices.sql with 007, unchanged. The notice shows the EHR's data as the projection at
-- the open; the challenge keeps the reading it was issued over and the EHR data as read, which
-- the commit copies into the version.
create table data_notice (
    id               integer primary key,
    session_id       text not null references session (session_id),
    nonce            text not null,
    json_version     integer null,
    data             text null,          -- json: Patient.Dto, the projection shown
    ehr_json_version integer null,
    ehr_data         text null,          -- json: the read the notice was told over
    expiry           integer not null,
    at               integer not null,
    check ((data is null) = (json_version is null))
);

create table challenge (
    id               integer primary key,
    session_id       text not null references session (session_id),
    nonce            text not null,
    digest           text not null,
    json_version     integer null,
    reading          text null,          -- json: Patient.Dto, the projection at the challenge
    ehr_json_version integer null,
    ehr_data         text null,          -- json: the read, copied into the version at the commit
    expiry           integer not null,
    at               integer not null,
    check ((reading is null) = (json_version is null))
);

-- 008 and 010: the measurement table is gone. No row of the user's exists before a sign.
```

`launch_record`, `launch_outcome`, `session_seen`, `session_ending`, `session_acknowledged`, the
credential tables, `submission_answer` and `audit_entry` are unchanged. The audit no longer writes
an entry per measurement, since no measurement is written.

## Scenarios

| Situation | Result |
|---|---|
| Patient data update in the old tab after a launch for another patient B in a new tab: a weight typed for A | No patient data is written; the heartbeat still is. B's session and B's panel are as they were |
| The reply to that weight update, which today rewrites the age from the cookie's session | The reply hands the age back unchanged: the one the server set at A's open, read-only in A's panel since. No session is read, so B's age never reaches A's tab |
| Any computing request from the old tab | The inactive-tab answer; the old tab shows the session gate and takes no more input; B's session stays open |
| Refresh, OpenVersion or close in the old tab | Refused; B's session, token and cookie untouched |
| Sign in the old tab | Refused with `StaleToken`, as today |
| Open at 10 kg, EHR now 12 kg, clinician typed 11 kg | Notice shows 12 kg beside 11 kg; take the EHR's data gives 12, keep the plan's gives 11; the sign records the choice and the 12 kg reading |
| The same, with unsigned orders on the plan | Taking the EHR's data drops the new and changed orders, as Refresh does; keeping the plan's signs on 11 kg |
| Open at 10 kg, EHR now 12 kg, clinician takes the EHR's data | Weight and gestational age the EHR's, the age unchanged |
| EHR 10 kg, typed 12 kg, signed, reload | The latest act is the sign: the panel shows the signed 12 kg, age at the resume |
| Typed 11 kg, signed, refreshed to the EHR's 12 kg, reload | The latest act is the refresh: the panel shows the EHR's 12 kg, age at the resume |
| Reload before any sign | The EHR data as read, projected at the open, age at the resume; typed values gone |
| EHR outage, head from an earlier session | The head's patient with the age computed from the identity at the resume |
| Two tabs of one session, both sign | The second is refused with `StaleToken`: the first sign minted a new token |
| A version read back | Its patient as signed and the EHR data of its challenge; what was typed is the difference |

## Steps

One pull request per step, script first where the policy asks for it, within the size limit. The
order closes the window between the server merge going and the client choice arriving: the client
first.

1. **The age.** `PatientCommand` answers the estimate only and reads no session; the age port
   member, `Session.age`, `Patient.aged` and the age read in `SigningCommand` go. The projection
   at the open computes the age already. `sessionPatient` recomputes the age and the post-menstrual
   age from the identity when it falls back to the head. `AgeOnRequestTests` keeps the patients on
   each request and the estimates.
   The age read was also the heartbeat of a signing request; a request refused at the boundary,
   before the challenge or the commit, no longer touches the Session. Those two touch it themselves.
2. **The data notice as a choice, client.** `SigningMachine`: `AcceptDataChange held` becomes two
   messages, take the EHR's data and keep the plan's; taking it with unsigned orders on the plan
   ends the signature, removes those orders and goes through the refresh path. The dialog shows
   the EHR's data next to the plan's, the age left out. Harmless while the server still merges:
   the notice's data is then the merged one.
3. **Measurements go, server.** `seen` without a draft, `Compute.bound` without `patientOf`,
   `Opened.Measured` and the `Measurements` module and type deleted, the table and its loader,
   writer and audit kind deleted. The notice's data is the fresh read projected at the open, and
   the client keeps its age when it takes that data (rule 5); the sign
   answers the signed plan's patient at the sign's age; the refresh answers the projection at the
   refresh. The version gains the EHR data of its challenge (rule 9): a field on
   `OrderPlanVersion`, its Dto and the plan JSON structure version raised. `MeasurementsTests`
   goes; the `SqlSession`, `SqlSchema` and `StubAdapter` tests follow.
4. **The token on every request, and the inactive-tab answer.** Rule 10: a `RecordNotice` case
   for a token that is not the session's, answered by `seen`; the client shows the session gate
   without closing the session;
   `OpenVersion`, `Refresh` and `CloseSession` carry the token on the wire and the server compares
   it as the challenge does; the stale close does not delete the cookie. With this step #1599
   closes and `OldTabToken.fsx` is deleted.
5. **The session's patient copy goes.** `Opened.Patient` removed; where it was read, the patient is
   computed by `sessionPatient` from the EHR data, the head and the date, with the port and the
   date passed in; `toOpened` gets them. The resume rule 7 lands here: `RecordOpenedWith` names
   the act that writes it, the `act` column holds it, and the resume reads the newest row's act.
   The audit reads the same act instead of telling a version open by the absence of other writes.
   The two patient columns of `session_opened_with` dropped; the patient of a remembered
   submission answer rebuilt from the version. `Challenge.Reading` and its column go too: only
   whether it is there is read, which `Challenge.Ehr` says, so `Verified` is `Challenge.Ehr.IsSome`.
   A refactor on its own, no behaviour the earlier steps have not set.
6. **Documents.** Plan 1430 amended: rule 3 says the old tab is told and refused, not that
   measurements and age are checked; rule 15 adds the resume and the refresh as moments the age is
   taken; the data notice as a choice; "held" written as unsigned orders on the plan. Plan 1075
   gets the note that its hold is answered by the two choices until step 7 of plan 1430 removes it.

## Verification

Every scenario above is a test at the level where its rule lives: `Client.Core` for step 2, the
server tests for steps 1, 3, 4 and 5. A run in the browser with two tabs for the first five
scenarios, and one for the data notice with and without unsigned orders.

## Decisions taken

- The identity stays off the patient; a later plan may move it, with the digest, the wire and the
  unreadable version answered there.
- The age is set by the server and trusted at the sign; no check at the challenge.
- The EHR data as read stays on the session for the data notice.
- A resume is read-only. One tab per session is not pursued: a reload in a left-behind tab would
  take the session and lose the active tab's unsigned work. The inactive-tab answer of rule 10
  covers the mix-up instead.
- Typed and accepted values are one thing; the data notice offers two choices and nothing merges. The
  EHR's data has no age, so taking it leaves the client's age as it is.
- A resume shows the latest act, read from the newest row of what the session opened with, which
  names the act that wrote it. No time is compared.
- The version keeps the EHR data of its challenge, so the record shows what was typed against.
- Schema changes edit the migration files in place, since no store runs; a numbered migration can
  replace this when one does. The plan JSON structure version is raised as every change to it is;
  no rows exist to read back.
