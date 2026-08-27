# Use Case: Prescribe & Manage Patient Orders in GenPRES

## Use Case Summary

- **Use Case ID**: UC-GENPRES-001
- **Name**: Prescribe & manage patient orders in GenPRES
- **Primary Actor**: Clinical user (prescriber / nurse), depending on authorization
- **Secondary Actors**: Main (MetaVision/HIX/any hospital EHR) application (auth and launch), GenPRES application,
  GenPRES handoff store, the main application's user-patient log, Hospital Patient Data Platform
- **Goal**: Let an authenticated user open GenPRES for a patient and add, modify, delete and save medication/nutrition
  orders.
- **Scope**: GenPRES prescribing module, launched from the main application
- **Level**: User goal
- **Trigger**: User clicks the button that opens GenPRES from the main application
- **Preconditions**:
  1. User is logged in to the main application and an authenticated session is active, with the user's authorization
     (roles / permissions) resolved. Logging in and out are the main application's own use cases and are out of scope
     here.
  2. GenPRES is reachable from the main application.
  3. A specific (at most one) patient is selected in the main application, so patient context is available. If no
     patient is selected, GenPRES can still be opened, but under [UC-GENPRES-002](UC-GENPRES-002.md) — see [Stand-alone
     usage](UC-GENPRES-002.md).
  4. The main application can write a launch record to the GenPRES handoff store, and GenPRES can read it back. If the
     handoff store is unavailable to either side, this use case does not apply and [UC-GENPRES-002](UC-GENPRES-002.md)
     applies instead, whether or not a patient is selected.
  5. GenPRES can have access to the user patient log files to verify active user and patient state.
  6. GenPRES can reach the Hospital Patient Data Platform — the hospital-wide, central patient data repository — to
     retrieve patient data. The Platform is a separate actor: GenPRES does not own it and shares it with other systems.
     If it is unreachable, patient data cannot be retrieved and prescribing is blocked until it is entered another way.
  7. GenPRES's own store is writable. Everything GenPRES saves goes there. Replication of that store to the Hospital
     Patient Data Platform is provided by the hospital, outside GenPRES; saving therefore never depends on the Platform
     being reachable.
- **Postconditions (success)**:
  1. Orders are persisted in GenPRES's own store.
  2. Where the user also dispatched them, the pharmacy has been notified under [UC-GENPRES-003](UC-GENPRES-003.md).
  3. The user session is explicitly marked as ended.
- **Postconditions (fail)**:
  1. Changes are not saved.
  2. The user is informed.
  3. Unsaved work is discarded.
  4. When a "non-ended" session exists in the store, the user is notified that, probably, work has been lost.
- **Priority**: High
- **Frequency of use**: Multiple times per shift, per patient
- **Assumptions**: Single sign-on (SSO) context from the main application is carried forward to GenPRES; no second login
  is expected. Authorization from the main application governs which actions the user may perform inside GenPRES (add /
  modify / delete vs. read-only).

## System boundary

GenPRES is one application to the user and two processes in practice, and several steps below turn on which of the two
is acting. The split is internal — neither is an actor, and the user sees one system — but they fail independently, and
most of what "interrupted" means falls out of that.

- **The GenPRES server** holds the session record, the knowledge rules, the patient's saved order state, and the
  connections to the handoff store, the user-patient log and the Hospital Patient Data Platform. It redeems the launch
  token, computes and solves scenarios, accepts saves, and is the only writer of end marks.
- **The GenPRES client** is the browser application. It carries the launch token in from the URL, presents what the
  server computes, and holds the **working order set** while the user edits it. Anything the user has changed but not
  saved exists only here.

Three consequences run through the rest of this use case:

- **"Unsaved" means "in the client".** Work that has not reached the server survives nothing — not a closed tab, not a
  lost connection, not a server restart.
- **The server does the watching.** Both the time-to-live and the user-patient log check have to run server-side. A
  closed client is exactly the case those checks exist to catch, so a check running in the browser cannot catch it.
- **A closed tab is silent.** A deliberate close at step 8 is a client action that reaches the server, which is why it
  can be marked. A killed tab, a dropped connection or a crash is the same client disappearing without saying so: no
  message arrives, no mark is written, and the record is left to be settled later. That is exactly the *(no mark)* state
  under session lifecycle below.

## Main Flow — GenPRES prescribing scenario

Each step below is one step of the happy path. The heading gives the step number, the actor, and the action / trigger;
the bullets give the system response, the authorization required, the alternative or exception flows, and any notes.

### 1 — User: click the button that opens GenPRES

- **System Response**: The main application generates a single-use launch token, writes it to the GenPRES handoff store
  together with a timestamp and the user identifier, the user's role and the patient identifier, and then opens GenPRES
  with the token as a URL parameter.
- **Authorization**: Must be authenticated.
- **Alternative / Exception flow**:
  - GenPRES unavailable → show error.
  - Handoff store not writable → the launch cannot be prepared; show error and remain in main application.
  - No patient selected in the main application → out of scope for this use case; continue with
    [UC-GENPRES-002](UC-GENPRES-002.md).
- **Notes**: No second login expected (SSO). Only the token travels through the browser — user and patient identifiers
  never appear in the URL.

### 2 — GenPRES: redeem the launch token and resolve the context

- **System Response**: The client carries the token in from the URL and hands it to the server, which looks it up in the
  handoff store and redeems it in one atomic operation that succeeds only if the token is still unredeemed and within
  its validity period. Redemption returns the user identifier, role and patient identifier recorded at launch, marks the
  token used, opens a **session record** in GenPRES's own store — keyed by user and patient, and carrying the redeemed
  token's identifier — and redirects the browser to a URL without the token.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - Token unknown, expired or already redeemed → deny access and ask the user to relaunch from the main application,
    without revealing which of the three applied.
  - Token absent (in url), or handoff store unreachable → patient-linked prescribing is not available; continue under
    [UC-GENPRES-002](UC-GENPRES-002.md), in which nothing is persisted.
  - A session record for this user and *this* patient is still open → settle it first, as described under session
    lifecycle below, and tell the user when it turns out to have been interrupted, naming what was last saved and when.
  - A session record for this user and a *different* patient is still open → end it as *switched patient*. Precondition
    3 allows one patient per user, so the new launch is what that user is working on now.
  - The user-patient log shows this patient already open to another user → say so, naming that user where the log
    carries it, and continue. This is a notice, not a gate: holding the patient against a colleague could block
    prescribing for as long as they stayed in it, which is worse than the collision it would prevent. What actually
    protects the work is the version check at step 7.
- **Notes**: The redeemed record is the only trusted source of user and patient identity; nothing is taken from the URL
  or from the client. The token is a bearer credential — single-use, short-lived, worthless once redeemed. The role it
  carries is authoritative for steps 6 and 7 and for [UC-GENPRES-003](UC-GENPRES-003.md). Issue, redemption and
  rejection are all logged; a rejected replay is worth alerting on. The token identifier kept on the session record ties
  each session back to the launch that opened it, which is the per-session provenance fit-gap 10.4 and 10.5 ask for.

### 3 — GenPRES: load all knowledge rules and configuration, if not already loaded

Loads all knowledge rules, i.e. dosing / drug / nutrition rules, etc...

- **System Response**: Loads the rule set (configuration) into memory and marks it ready; skips loading if already
  cached.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - Rules already loaded → step skipped.
  - Rule source unavailable → warn user; prescribing may be blocked or limited.
- **Notes**: One-time / cached load to avoid repeated fetches. These rules are what step 6 uses to compute the order
  scenarios on offer; without them there is nothing to offer.

### 4 — GenPRES: retrieve all required user and patient data

- **System Response**: Uses the identifiers redeemed in step 2 to load the user profile and the patient's demographic
  and clinical data needed for prescribing, retrieving the patient data from the Hospital Patient Data Platform.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - Required patient data missing → prompt user or block prescribing until resolved.
  - Platform unreachable → say so; the session has the launch record but no patient data, so prescribing is blocked
    until the data is retrieved or entered.
- **Notes**: GenPRES does not call back into the main application. Two sources serve this step and no others: the
  **launch record**, held in GenPRES's own handoff store, for who the user is and which patient they opened; and the
  **Hospital Patient Data Platform**, read-only, for the patient data itself. The patient identifier from the launch
  record is the key into the Platform, and is treated as an opaque, trusted key. GenPRES writes nothing back to the
  Platform directly — see step 7.

### 5 — GenPRES: look up existing patient orders

Depends on the patient data retrieved in the previous step.

- **System Response**: The server queries GenPRES's own store for orders previously saved against this patient, sends
  them to the client to present, and keeps the **version** they were read at with the session. That version is what step
  7 submits back to prove the save builds on the state the user actually saw.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - No existing orders → start with an empty order set, at the version the store reports for a patient with nothing
    saved.
  - Own store unreadable → say so and start empty, making clear that earlier orders may exist but could not be read, so
    an empty plan is not mistaken for a patient without orders.
- **Notes**: Orders come from GenPRES's own store, not from the Platform: GenPRES's store is the source of truth for
  what GenPRES saved, and the Platform receives it by replication rather than serving it back.

### 6 — User: add, modify or delete orders

- **System Response**: The server applies the knowledge rules to the patient and the current selection and returns only
  the order scenarios those rules allow. The client presents them; the user picks a scenario and adjusts it by choosing
  from the computed value sets or stepping a value by its increment. Each adjustment goes back to the server to be
  re-solved around what the user picked, and the working order set — held in the client until step 7 — is updated from
  the result.
- **Authorization**: Requires prescribing authorization; otherwise read-only.
- **Alternative / Exception flow**:
  - Unauthorized user → actions disabled / view-only.
  - No scenario satisfies the current selection → tell the user which constraint leaves nothing to choose from, so the
    selection can be revised.
  - User steps a value past its rule-defined limit → see the dose-check extension below.
  - GenPRES session expires → see the session expiry extension below.
  - The user-patient log shows the user gone, or on another patient → see the extension below; on another patient,
    editing stops at once.
- **Notes**: Core prescribing interaction. The rules constrain what can be built rather than judging what has been
  built, so an unsafe order cannot be assembled by accident — only by deliberate override.

### 7 — User: save the existing orders (state), at any time

- **System Response**: The client sends the working order set to the server, which persists it to GenPRES's own store so
  it can be resumed later. A save writes the **complete current state** of the patient's order set, not a delta, and
  carries the version the session is holding. The store accepts it only when that version is still the current one, and
  raises the version by one on acceptance. The save is complete once that store has it, and the session then holds the
  new version. This is the moment the work stops being client-only.
- **Authorization**: Requires save authorization.
- **Alternative / Exception flow**:
  - Save fails — either the client cannot reach the server, or the server cannot reach its own store → notify the user;
    the working set stays in the client and remains unsaved. The two causes put the fault in different places but leave
    the user in the same one.
  - The stored version has moved on — someone else saved this patient in the meantime → see the stale-version extension
    below.
  - GenPRES session expires → see the session expiry extension below.
  - The user-patient log shows the user gone, or on another patient → see the extension below; saving is blocked from
    that point.
- **Notes**: Can be done repeatedly during the session. Once orders are saved, the user may dispatch TPN and continuous
  medication orders to the pharmacy under [UC-GENPRES-003](UC-GENPRES-003.md) — and that dispatch has to refuse a stale
  version for the same reason a save does, or the pharmacy prepares from a plan someone has since replaced. GenPRES
  saves to its own store and stops there — what happens to that data afterwards is described under data ownership below,
  and is not part of this flow.

### 8 — User / System: close the GenPRES application

- **System Response**: The client detects unsaved changes and offers the user the choice to save the current state
  before closing, then tells the server, which marks the session record ended as *closed by user*. The end mark is
  written last, after any save, so an ended record always covers the work that was completed under it.
- **Authorization**: N/A.
- **Alternative / Exception flow**:
  - User chooses not to save → changes are discarded.
  - No unsaved changes → close directly.
  - The client goes away without telling the server — tab killed, browser closed, connection lost → nothing is written.
    The record stays open and is settled later under session lifecycle below.
  - GenPRES's own store unwritable → the end mark cannot be written and the session will later read as interrupted; tell
    the user the close was not recorded, so the warning they meet on their next launch is expected.
- **Notes**: Prevents silent loss of work. This is a deliberate act by the user, and the only end mark written while the
  session is alive — the other three are settled by the server on its own, as set out under session lifecycle below.
  What makes this one writable at all is that the client says it is closing; nothing else about a disappearing client
  distinguishes a deliberate close from a crash.

## Extension — a value outside the rule-defined limits

Applies at step 6, when the user deliberately moves a value beyond what the knowledge rules define for this patient.

- **Trigger**: The user steps a dose, quantity or concentration past its rule-defined limit, using the wider step
  control. The offered value sets and the narrow steps stay inside those limits, so this cannot happen by ordinary
  selection.
- **System Response**: The value is accepted and the order is re-solved around it. The affected variable is marked with
  a dose-check severity — Caution, Warning or Alert, depending on what was exceeded — and colour-coded in the UI.
  Nothing is blocked.
- **Authorization**: Requires prescribing authorization.
- **Alternative / Exception flow**: The value lies outside what the solver can satisfy at all → no solution is produced
  and the previous state stands.
- **Notes**: This is the only route to an order outside the rules, and it takes a deliberate act. Severity is recomputed
  on every solve: it is a live property of the current values, not a recorded decision. **Intended addition**: an
  out-of-limit value should also be confirmed by the user, carry a reason, and be recorded against the order so it stays
  attributable and survives into the saved state. See the implementation status below.

## Extension — the patient's order state changed under you

Applies at step 7, when the version the session is holding is no longer the current one: another user saved this patient
while this session was open. The notice at step 2 may have warned this was possible — here it has happened.

- **Trigger**: A save is submitted carrying a version the store has moved past.
- **System Response**: The save is not applied either way without asking. The user is told that another user has saved
  this patient since their own state was loaded — by whom, and when — and chooses:
  - **Overwrite** — the working set is written as the complete current state and becomes the new version. The other
    user's save is superseded.
  - **Reload** — the last saved state is loaded at its version, and the user's own unsaved changes are discarded.
- **Authorization**: Requires save authorization, as step 7 does.
- **Alternative / Exception flow**:
  - The stored version moves again between the choice and the write → the same check applies to that write. Ask again
    rather than let a confirmed overwrite carry a version that has itself gone stale.
  - The session ends before the user chooses → nothing is written, the stored state stands, and the working set goes
    with the session.
- **Notes**: Because a save carries the complete order set rather than a delta, overwrite means what it says: the other
  clinician's plan is replaced wholesale, not merged into. That is a clinical decision, and making it needs the other
  save described well enough to judge — who, when, what changed — which is why it is put to the user rather than settled
  by rule. It should also be recorded against the patient so an overwritten plan stays attributable, which depends on
  fit-gap 10.4 in the same way the recorded overrides above do. Merging is deliberately not offered: no rule can decide
  which of two clinicians' orders should survive.

## Extension — the GenPRES session expires

Applies from step 2 onwards, at any point in the session.

- **Trigger**: The GenPRES session reaches its time-to-live, or the user is idle beyond it.
- **System Response**: The server treats the session as over: it refuses further changes, saves and dispatches, and
  marks the session record ended as *expired*. The client tells the user the session has ended and offers to close.
  Continuing requires a fresh launch from the main application. The time-to-live is the server's, so it runs out whether
  or not a client is still connected — which is what lets an abandoned session end itself.
- **Authorization**: N/A — authorization has lapsed.
- **Alternative / Exception flow**: Unsaved changes present → tell the user they cannot be saved under the lapsed
  session, and that the user must relaunch and redo them.
- **Notes**: The launch token is redeemed once, at step 2, and GenPRES never calls back into the main application, so a
  logout there is not reported to GenPRES. What precondition 5 adds is the ability to *look* — see the extension below —
  but that check is advisory and its source may be stale or unreadable. The GenPRES session time-to-live therefore
  remains the only hard bound on how long a GenPRES session can outlive the main application session — keep it short.

## Extension — the user is no longer active for this patient

Applies from step 2 onwards, at any point in the session. Rests on precondition 5: the GenPRES server reads the main
application's user-patient log and checks it against the user and patient redeemed at step 2.

- **Trigger**: The log shows the user active on a different patient than the one this session was opened for, or no
  longer active at all.
- **System Response**: Two cases, carrying different weight.
  - **Different patient** — the wrong-patient hazard, so changes, saving and dispatching are blocked at once, the user
    is told which patient this session belongs to, and the session record is marked ended as *switched patient*.
  - **User no longer active** — an authorization lapse, handled as an expiry is: inform, block, offer to close, and mark
    the session record ended as *main session ended*.
- **Authorization**: N/A — the basis for authorization has gone.
- **Alternative / Exception flow**:
  - Log unreadable, or its entries older than the agreed staleness tolerance → change nothing and end nothing. A missing
    entry is not evidence of a logout, and ending a live session on a false negative costs a clinician their unsaved
    work. The session time-to-live remains the bound, as it does today.
  - Unsaved changes present → as for expiry, they cannot be saved under the lapsed session.
- **Notes**: GenPRES reads the log and never writes it; the main application remains its only writer, and GenPRES still
  makes no call into it. The check reads current state rather than waiting for an event, so a check missed while GenPRES
  was down costs nothing — the next read sees the same truth. It has to run on the **server**: a client that has been
  closed is precisely the case the check exists to catch, so a check running in the browser would go quiet exactly when
  it is needed. Two numbers govern the exposure and both belong in the design: how often the log is read, and how old an
  entry may be before it is disregarded.

## Session lifecycle

The session record opened at step 2 lives on the server and is written only by the server. It is what makes the
postconditions checkable: success requires it to be explicitly ended, and an unexplained open record is what tells the
*next* launch that work was probably lost. Five end marks are written; one state is the absence of any of them. Each
names what happened, not what noticed it.

| End mark | Cause | Unsaved work |
| --- | --- | --- |
| *closed by user* | The user closed GenPRES (step 8) | The user was offered the save |
| *main session ended* | The user logged out of the main application, or that session ended another way | The user has already moved on |
| *switched patient* | The same user moved to another patient — seen at a later launch, or in the user-patient log | Lost with the previous patient's session |
| *expired* | Time-to-live reached, or idle beyond it | Cannot be saved; authorization has lapsed |
| *server restarted* | The GenPRES server stopped and started again | Lost: only what step 7 already saved survives |
| *(no mark)* | Not yet settled — the client has gone quiet and the server cannot yet tell whether it is coming back | Still in the client if it returns; lost if it does not |

Only *closed by user* has to be written while the session is alive. The others can be settled later, when the question
is actually asked: at the next launch for that user, GenPRES has what it needs to tell them apart — a log entry showing
the user gone or moved on, an elapsed time-to-live, or a launch naming another patient.

**A server restart settles itself.** It is different in kind from a client that has gone quiet, and the difference is
certainty. When the server comes back it knows, without having to look at anything, that no session it was running can
still be live — sessions are its own state, and its own state did not survive. So at startup it settles every record
still open and marks them *server restarted*, rather than leaving them to be puzzled over later. A vanished client
offers no such certainty: the tab may be closed for good, or the connection may return in ten seconds with the working
set intact and the user none the wiser. Those records stay open until the time-to-live or the user-patient log settles
them, which is why *(no mark)* means "not yet settled" rather than "interrupted".

Both still cost the user their unsaved work whenever they do not come back, so a *server restarted* record has to raise
the same warning at the next launch as an unexplained one. What the mark buys is not a different outcome for the user
but a different quality of statement: GenPRES reporting why a session ended, instead of guessing that one did.

That distinction is what makes the warning in failure postcondition 4 worth trusting. It fires where work was actually
at risk, not on every timeout, logout and patient switch — rare enough for a clinician to take seriously when it does.

## Data ownership and boundaries

Five sources appear in this flow, with different owners. Which one is authoritative for what is a safety property, not
an implementation detail, so it is stated here rather than left to the design.

- **Hospital Patient Data Platform — authoritative for the patient data GenPRES does not own.** The patient context
  GenPRES works from originates there. GenPRES **retrieves** it and never writes it back, never corrects it, and never
  treats a local copy of it as truth: where the two differ, the Platform is right and GenPRES is stale. A patient
  context stored inside a saved order is a historical record of what was prescribed against, not a competing source. If
  the Platform cannot be reached, there is no authoritative patient data and prescribing is blocked — GenPRES does not
  prescribe against a cached copy.
- **The launch record — authoritative for the user.** Identity and role come from what the main application wrote at
  launch and hold for the whole session; the Platform is not consulted for them. This keeps authorization answerable by
  the system that granted it.
- **GenPRES's own store — authoritative for what GenPRES produces.** Orders and prescription state are GenPRES's data;
  its store is the source of truth for them and the only thing step 5 reads back. Session records live here too, and
  GenPRES is their only writer: the main application never marks a GenPRES session ended, it only makes visible —
  through the log below — the fact from which GenPRES concludes it. The version counted at steps 5 and 7 is a version
  *of this order state*, which GenPRES owns and may therefore number. It is not a version of the patient: patient data
  belongs to the Platform, and GenPRES neither writes nor versions it.
- **GenPRES handoff store — where the launch records live.** Written by the main application, read once at step 2, then
  spent.
- **The main application's user-patient log — authoritative for whether the user is still active, and on which
  patient.** GenPRES reads it, never writes it, and holds no opinion about it: where it disagrees with a running GenPRES
  session, the log is right. It is read periodically rather than once, but it is still a read from a file the main
  application owns — GenPRES makes no call into the main application. It is advisory: it can tighten how long a session
  outlives the main application session, but it cannot be relied on to bound it, and failure to read it never ends a
  session.

**Replication is out of scope.** Moving data from GenPRES's own store into the Hospital Patient Data Platform is
provided by the hospital. GenPRES does not perform it, monitor it, retry it, or report on it, and nothing in this use
case waits for it. Two consequences follow and should be carried into the risk analysis rather than assumed away: the
Platform's copy of GenPRES data is **eventually** consistent and never ahead of GenPRES, so anything reading GenPRES
orders from the Platform may see an older state than the clinician does; and because GenPRES does not observe
replication, a replication failure is invisible to GenPRES and must be detected by whoever owns that mechanism.

## Implementation status

This use case describes **intended** behaviour, not what ships today. The current state and the distance to it are
tracked in the [AP2019 vs GenPRES fit-gap analysis](../roadmap/fit-gap-ap2019-vs-genpres.md).

- **Built**: scenario computation and selection (steps 3–6); the order plan with multi-select and batch delete (fit-gap
  9.12, Fit); dose-check severity levels colour-coded in the UI.
- **Not built — saving (step 7 and success postcondition 1)**: fit-gap 9.6 (version control / save history) and 9.7
  (multi-user conflict detection). Design proposed in [patient-state
  persistence](../roadmap/feature-patient-persistence.md), status Proposed, flagged blocking.
- **Not built — session records and the end marks (step 2, step 8, success postcondition 3, failure postcondition 4)**:
  the session is a runtime notion today and is written nowhere, so nothing can be marked ended and an interrupted
  session leaves no trace to find. Depends on the same persistence work as step 7, and on fit-gap 10.1 for the user half
  of the key.
- **Not built — the user-patient log check (precondition 5 and its extension)**: no such log is read today. It should
  land with an ADR that records the staleness tolerance and the read interval, since both bound how long a session can
  outlive the main application one.
- **Not built — concurrent editing (the notice at step 2, the version at step 5, the check at step 7, and the
  stale-version extension)**: fit-gap 9.7 (multi-user conflict detection) and the versioning half of 9.6. Neither the
  session key nor precondition 3 keeps a second clinician off the same patient — both are per user — so the notice and
  the version check are what handle it, and neither exists today. The same check is owed by dispatch in
  [UC-GENPRES-003](UC-GENPRES-003.md), which that use case does not yet describe.
- **Not built — patient data retrieval (step 4)**: no retrieval from a hospital-wide Platform exists.
  `Informedica.DataPlatform.Lib` is referenced by the server project but unused at runtime.
- **Out of GenPRES's scope — replication**: provided by the hospital, so it appears here only as a boundary and a
  consequence, never as work.
- **To reconcile**: [patient-state persistence](../roadmap/feature-patient-persistence.md) assumes a local store with
  the product still open and puts the shared regional platform out of MVP scope. That is consistent with GenPRES owning
  its store, but it does not yet name the Platform as the authority for patient context, nor the replication out of
  GenPRES. Both belong in that request and in an ADR.
- **Not built — identity and authorization (steps 1, 2, and every Authorization entry)**: fit-gap 10.1 (per-user
  identity), 10.2 (role-based authorization), 10.3 (prescriber registry and signing), 10.4 (per-user audit trail), 10.5
  (EHR-sourced login provenance). The launch token design in steps 1 and 2 supersedes the stateless, URL-carried context
  described in [software-requirements.md](../mdr/requirements/software-requirements.md) section 4; that requirement
  needs updating alongside an ADR when the token work lands.
- **Not built — recorded overrides**: today an out-of-limit value is flagged by severity only. Confirmation, reason and
  attribution are intended, and depend on both the persistence work and fit-gap 10.4.
- **Partial — order start/stop**: present on the backend order type, not surfaced in the UI (fit-gap 9.15).

## Related use cases

- **UC-GENPRES-002 — Calculate orders without patient context** ([Stand-alone usage](UC-GENPRES-002.md)): same user,
  same application, but launched without a patient, or launched without a redeemable launch token. It is a separate use
  case because it pursues a different goal (calculation and exploration, not orders of record) and can never reach this
  use case's success postconditions — nothing is persisted and the pharmacy is never notified. Nothing includes the
  session record: a stand-alone session leaves none, so the end marks above and the interrupted-session warning do not
  apply to it.
- **UC-GENPRES-003 — Notify the pharmacy to prepare orders** ([Pharmacy notification](UC-GENPRES-003.md)): dispatching
  saved TPN and continuous medication orders for preparation. It is a separate use case because it can be triggered at
  any time from a session, involves a different secondary actor (Pharmacy), and has its own postcondition, authorization
  and failure modes.
- **UC-GENPRES-004 — Order enteral and parenteral nutrition** ([Nutrition ordering](UC-GENPRES-004.md)): composing a
  nutrition plan inside this session. Separate because lines are built side by side and judged together through shared
  intake totals, rather than one order at a time.
- **Log in / log out of the main application**: the main application's own use cases. Referenced as preconditions here,
  not described.

## Legend — How to read / extend this use case

- **Use Case Summary**: The "header" of the use case — identity, actors, goal, pre/postconditions. Fill these in per
  project.
- **Main Flow**: The step-by-step scenario. One subsection = one step in the happy path.
- **Step number**: Sequence number of the step.
- **Actor**: Who performs the step — a person (User) or the system (GenPRES).
- **Action / Trigger**: What is done, from the actor's point of view.
- **System Response**: How the system reacts to that action.
- **Authorization**: What permission the step requires (ties back to the role resolved at login and re-checked in step
  2).
- **Alternative / Exception flow**: What happens when things diverge from the happy path (errors, skips, unauthorized).
- **Notes**: Assumptions, clarifications, or links to other artifacts.
- **Extension**: A branch that still serves this use case's goal. A branch that pursues a different goal, or that can
  never reach these postconditions, becomes a separate use case instead.
- **Tip**: For test-style (BDD) scenarios, the same entries map to Given (preconditions) / When (action) / Then (system
  response).
