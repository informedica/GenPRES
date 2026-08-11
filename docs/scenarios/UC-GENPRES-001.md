# Use Case: Prescribe & Manage Patient Orders in GenPRES

## Use Case Summary

- **Use Case ID**: UC-GENPRES-001
- **Name**: Prescribe & manage patient orders in GenPRES
- **Primary Actor**: Clinical user (prescriber / nurse), depending on
  authorization
- **Secondary Actors**: Main (MetaVision) application (auth and launch), GenPRES
  application, GenPRES handoff store, Hospital Patient Data Platform
- **Goal**: Let an authenticated user open GenPRES for a patient and add,
  modify, delete and save medication/nutrition orders.
- **Scope**: GenPRES prescribing module, launched from the main
  application
- **Level**: User goal
- **Trigger**: User clicks the button that opens GenPRES from the main
  application
- **Preconditions**:
  1. User is logged in to the main application and an authenticated
     session is active, with the user's authorization (roles /
     permissions) resolved. Logging in and out are the main
     application's own use cases and are out of scope here.
  2. GenPRES is reachable from the main application.
  3. A patient is selected in the main application, so patient context
     is available. If no patient is selected, GenPRES can still be
     opened, but under [UC-GENPRES-002](UC-GENPRES-002.md) — see
     [Stand-alone usage](UC-GENPRES-002.md).
  4. The main application can write a launch record to the GenPRES handoff
     store, and GenPRES can read it back. This is the only channel between the
     two systems: GenPRES never calls into the main application. If the handoff
     store is unavailable to either side, this use case does not apply and
     [UC-GENPRES-002](UC-GENPRES-002.md) applies instead, whether or not a
     patient is selected.
  5. GenPRES can reach the Hospital Patient Data Platform — the hospital-wide,
     central patient data repository — to retrieve patient data. The Platform
     is a separate actor: GenPRES does not own it and shares it with other
     systems. If it is unreachable, patient data cannot be retrieved and
     prescribing is blocked until it is entered another way.
  6. GenPRES's own store is writable. Everything GenPRES saves goes there.
     Replication of that store to the Hospital Patient Data Platform is
     provided by the hospital, outside GenPRES; saving therefore never
     depends on the Platform being reachable.
- **Postconditions (success)**: Orders are persisted in GenPRES's own
  store. Where the user also dispatched them, the pharmacy has been
  notified under [UC-GENPRES-003](UC-GENPRES-003.md).
- **Postconditions (fail)**: No orders are changed; user is informed.
  Unsaved work is either discarded on the user's choice or kept for a
  later session.
- **Priority**: High
- **Frequency of use**: Multiple times per shift, per patient
- **Assumptions**: Single sign-on (SSO) context from the main
  application is carried forward to GenPRES; no second login is
  expected. Authorization from the main application governs which
  actions the user may perform inside GenPRES (add / modify / delete vs.
  read-only).

## Main Flow — GenPRES prescribing scenario

Each step below is one step of the happy path. The heading gives the
step number, the actor, and the action / trigger; the bullets give the
system response, the authorization required, the alternative or
exception flows, and any notes.

### 1 — User: click the button that opens GenPRES

- **System Response**: The main application generates a single-use launch
  token, writes it to the GenPRES handoff store together with a
  timestamp and the user identifier, the user's role and the patient
  identifier, and then opens GenPRES with the token as a URL parameter.
- **Authorization**: Must be authenticated.
- **Alternative / Exception flow**:
  - GenPRES unavailable → show error and remain in main application.
  - Handoff store not writable → the launch cannot be prepared; show error and
    remain in main application.
  - No patient selected in the main application → out of scope for this use
    case; continue with [UC-GENPRES-002](UC-GENPRES-002.md).
- **Notes**: No second login expected (SSO). Only the token travels
  through the browser — user and patient identifiers never appear in the
  URL.

### 2 — GenPRES: redeem the launch token and resolve the context

- **System Response**: Looks up the token in the handoff store and
  redeems it in one atomic operation that succeeds only if the token is
  still unredeemed and within its validity period (one minute).
  Redemption returns the user identifier, role and patient identifier
  recorded at launch, marks the token used, establishes a GenPRES
  session, and redirects the browser to a URL without the token.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - Token unknown, expired or already redeemed → deny access and ask the user
    to relaunch from the main application, without revealing which of the three
    applied.
  - Token absent, or handoff store unreachable → patient-linked prescribing is
    not available; continue under [UC-GENPRES-002](UC-GENPRES-002.md), in which
    nothing is persisted.
- **Notes**: The redeemed record is the only trusted source of user and patient
  identity; nothing is taken from the URL or from the client. The token is a
  bearer credential — single-use, short-lived, worthless once redeemed. The
  role it carries is authoritative for steps 6 and 7 and for
  [UC-GENPRES-003](UC-GENPRES-003.md). Issue, redemption and rejection are all
  logged; a rejected replay is worth alerting on.

### 3 — GenPRES: load all knowledge rules and configuration, if not already loaded

Loads all knowledge rules, i.e. dosing / drug / nutrition rules, etc...

- **System Response**: Loads the rule set (configuration) into memory and marks
  it ready; skips loading if already cached.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - Rules already loaded → step skipped.
  - Rule source unavailable → warn user; prescribing may be blocked or
    limited.
- **Notes**: One-time / cached load to avoid repeated fetches. These
  rules are what step 6 uses to compute the order scenarios on offer;
  without them there is nothing to offer.

### 4 — GenPRES: retrieve all required user and patient data

- **System Response**: Uses the identifiers redeemed in step 2 to load
  the user profile and the patient's demographic and clinical data
  needed for prescribing, retrieving the patient data from the Hospital
  Patient Data Platform.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - Required patient data missing → prompt user or block prescribing until
    resolved.
  - Platform unreachable → say so; the session has the launch record but no
    patient data, so prescribing is blocked until the data is retrieved or
    entered.
- **Notes**: GenPRES does not call back into the main application. Two
  sources serve this step and no others: the **launch record**, held in
  GenPRES's own handoff store, for who the user is and which patient
  they opened; and the **Hospital Patient Data Platform**, read-only,
  for the patient data itself. The patient identifier from the launch
  record is the key into the Platform, and is treated as an opaque,
  trusted key. GenPRES writes nothing back to the Platform directly —
  see step 7.

### 5 — GenPRES: look up existing patient orders

Depends on the patient data retrieved in the previous step.

- **System Response**: Queries GenPRES's own store for orders previously
  saved against this patient, and presents them.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - No existing orders → start with an empty order set.
  - Own store unreadable → say so and start empty, making clear that earlier
    orders may exist but could not be read, so an empty plan is not mistaken for
    a patient without orders.
- **Notes**: Orders come from GenPRES's own store, not from the
  Platform: GenPRES's store is the source of truth for what GenPRES
  saved, and the Platform receives it by replication rather than serving
  it back.

### 6 — User: add, modify or delete orders

- **System Response**: Applies the knowledge rules to the patient and the
  current selection and offers only the order scenarios those rules
  allow. The user picks a scenario and adjusts it by choosing from the
  computed value sets or stepping a value by its increment. The working
  order set is updated from what the user picked, and re-solved around
  it.
- **Authorization**: Requires prescribing authorization; otherwise
  read-only.
- **Alternative / Exception flow**:
  - Unauthorized user → actions disabled / view-only.
  - No scenario satisfies the current selection → tell the user which
    constraint leaves nothing to choose from, so the selection can be revised.
  - User steps a value past its rule-defined limit → see the dose-check
    extension below.
  - GenPRES session expires → see the session expiry extension below.
- **Notes**: Core prescribing interaction. The rules constrain what can
  be built rather than judging what has been built, so an unsafe order
  cannot be assembled by accident — only by deliberate override.

### 7 — User: save the existing orders (state), at any time

- **System Response**: Persists the current order set / working state to
  GenPRES's own store, so it can be resumed later. The save is complete
  once that store has it.
- **Authorization**: Requires save authorization.
- **Alternative / Exception flow**:
  - Save fails (e.g. connection loss to GenPRES's own store) → notify user;
    keep working copy in the session.
  - GenPRES session expires → see the session expiry extension below.
- **Notes**: Can be done repeatedly during the session. Once orders are
  saved, the user may dispatch TPN and continuous medication orders to
  the pharmacy under [UC-GENPRES-003](UC-GENPRES-003.md). GenPRES saves
  to its own store and stops there — what happens to that data
  afterwards is described under data ownership below, and is not part of
  this flow.

### 8 — User / System: close the GenPRES application

- **System Response**: Detects unsaved changes and offers the user the
  choice to save the current state before closing.
- **Authorization**: N/A.
- **Alternative / Exception flow**:
  - User chooses not to save → changes are discarded.
  - No unsaved changes → close directly.
- **Notes**: Prevents silent loss of work.

## Extension — a value outside the rule-defined limits

Applies at step 6, when the user deliberately moves a value beyond what
the knowledge rules define for this patient.

- **Trigger**: The user steps a dose, quantity or concentration past its
  rule-defined limit, using the wider step control. The offered value
  sets and the narrow steps stay inside those limits, so this cannot
  happen by ordinary selection.
- **System Response**: The value is accepted and the order is re-solved
  around it. The affected variable is marked with a dose-check severity
  — Caution, Warning or Alert, depending on what was exceeded — and
  colour-coded in the UI. Nothing is blocked.
- **Authorization**: Requires prescribing authorization.
- **Alternative / Exception flow**: The value lies outside what the
  solver can satisfy at all → no solution is produced and the previous
  state stands.
- **Notes**: This is the only route to an order outside the rules, and
  it takes a deliberate act. Severity is recomputed on every solve: it
  is a live property of the current values, not a recorded decision.
  **Intended addition**: an out-of-limit value should also be confirmed
  by the user, carry a reason, and be recorded against the order so it
  stays attributable and survives into the saved state. See the
  implementation status below.

## Extension — the GenPRES session expires

Applies from step 2 onwards, at any point in the session.

- **Trigger**: The GenPRES session reaches its time-to-live, or the user
  is idle beyond it.
- **System Response**: GenPRES informs the user that the session has
  ended, blocks further changes, saving and dispatching, and offers to
  close. Continuing requires a fresh launch from the main application.
- **Authorization**: N/A — authorization has lapsed.
- **Alternative / Exception flow**: Unsaved changes present → tell the
  user they cannot be saved under the lapsed session, and that the user
  must relaunch and redo them.
- **Notes**: A logout in the main application **is not detected**. The
  launch token is redeemed once, at step 2, and there is no channel to
  re-check the main application afterwards. The GenPRES session
  time-to-live is therefore the only bound on how long a GenPRES session
  can outlive the main application session — keep it short.

## Data ownership and boundaries

Three stores appear in this flow, with different owners. Which one is
authoritative for what is a safety property, not an implementation
detail, so it is stated here rather than left to the design.

- **Hospital Patient Data Platform — authoritative for the patient data
  GenPRES does not own.** The patient context GenPRES works from
  originates there. GenPRES **retrieves** it and never writes it back,
  never corrects it, and never treats a local copy of it as truth: where
  the two differ, the Platform is right and GenPRES is stale. A patient
  context stored inside a saved order is a historical record of what was
  prescribed against, not a competing source. If the Platform cannot be
  reached, there is no authoritative patient data and prescribing is
  blocked — GenPRES does not prescribe against a cached copy.
- **The launch record — authoritative for the user.** Identity and role
  come from what the main application wrote at launch and hold for the
  whole session; the Platform is not consulted for them. This keeps
  authorization answerable by the system that granted it.
- **GenPRES's own store — authoritative for what GenPRES produces.**
  Orders and prescription state are GenPRES's data; its store is the
  source of truth for them and the only thing step 5 reads back.
- **GenPRES handoff store — where the launch records live.** Written by
  the main application, read once at step 2, then spent.

**Replication is out of scope.** Moving data from GenPRES's own store
into the Hospital Patient Data Platform is provided by the hospital.
GenPRES does not perform it, monitor it, retry it, or report on it, and
nothing in this use case waits for it. Two consequences follow and
should be carried into the risk analysis rather than assumed away: the
Platform's copy of GenPRES data is **eventually** consistent and never
ahead of GenPRES, so anything reading GenPRES orders from the Platform
may see an older state than the clinician does; and because GenPRES does
not observe replication, a replication failure is invisible to GenPRES
and must be detected by whoever owns that mechanism.

## Implementation status

This use case describes **intended** behaviour, not what ships today.
The current state and the distance to it are tracked in the
[AP2019 vs GenPRES fit-gap analysis](../roadmap/fit-gap-ap2019-vs-genpres.md).

- **Built**: scenario computation and selection (steps 3–6); the order
  plan with multi-select and batch delete (fit-gap 9.12, Fit);
  dose-check severity levels colour-coded in the UI.
- **Not built — saving (step 7 and the success postcondition)**:
  fit-gap 9.6 (version control / save history) and 9.7 (multi-user
  conflict detection). Design proposed in
  [patient-state persistence](../roadmap/feature-patient-persistence.md),
  status Proposed, flagged blocking.
- **Not built — patient data retrieval (step 4)**: no retrieval from a
  hospital-wide Platform exists. `Informedica.DataPlatform.Lib` is
  referenced by the server project but unused at runtime.
- **Out of GenPRES's scope — replication**: provided by the hospital, so
  it appears here only as a boundary and a consequence, never as work.
- **To reconcile**:
  [patient-state persistence](../roadmap/feature-patient-persistence.md)
  assumes a local store with the product still open and puts the shared
  regional platform out of MVP scope. That is consistent with GenPRES
  owning its store, but it does not yet name the Platform as the
  authority for patient context, nor the replication out of GenPRES.
  Both belong in that request and in an ADR.
- **Not built — identity and authorization (steps 1, 2, and every
  Authorization entry)**: fit-gap 10.1 (per-user identity), 10.2
  (role-based authorization), 10.3 (prescriber registry and signing),
  10.4 (per-user audit trail), 10.5 (EHR-sourced login provenance). The
  launch token design in steps 1 and 2 supersedes the stateless,
  URL-carried context described in
  [software-requirements.md](../mdr/requirements/software-requirements.md)
  section 4; that requirement needs updating alongside an ADR when the
  token work lands.
- **Not built — recorded overrides**: today an out-of-limit value is
  flagged by severity only. Confirmation, reason and attribution are
  intended, and depend on both the persistence work and fit-gap 10.4.
- **Partial — order start/stop**: present on the backend order type,
  not surfaced in the UI (fit-gap 9.15).

## Related use cases

- **UC-GENPRES-002 — Calculate orders without patient context**
  ([Stand-alone usage](UC-GENPRES-002.md)): same user, same
  application, but launched without a patient, or launched without a
  redeemable launch token. It is a
  separate use case because it pursues a different goal (calculation and
  exploration, not orders of record) and can never reach this use case's
  success postconditions — nothing is persisted and the pharmacy is
  never notified.
- **UC-GENPRES-003 — Notify the pharmacy to prepare orders**
  ([Pharmacy notification](UC-GENPRES-003.md)): dispatching
  saved TPN and continuous medication orders for preparation. It is a
  separate use case because it can be triggered at any time from a
  session, involves a different secondary actor (Pharmacy), and has its
  own postcondition, authorization and failure modes.
- **UC-GENPRES-004 — Order enteral and parenteral nutrition**
  ([Nutrition ordering](UC-GENPRES-004.md)): composing a
  nutrition plan inside this session. Separate because lines are built
  side by side and judged together through shared intake totals, rather
  than one order at a time.
- **Log in / log out of the main application**: the main application's
  own use cases. Referenced as preconditions here, not described.

## Legend — How to read / extend this use case

- **Use Case Summary**: The "header" of the use case — identity, actors,
  goal, pre/postconditions. Fill these in per project.
- **Main Flow**: The step-by-step scenario. One subsection = one step in
  the happy path.
- **Step number**: Sequence number of the step.
- **Actor**: Who performs the step — a person (User) or the system
  (GenPRES).
- **Action / Trigger**: What is done, from the actor's point of view.
- **System Response**: How the system reacts to that action.
- **Authorization**: What permission the step requires (ties back to the
  role resolved at login and re-checked in step 2).
- **Alternative / Exception flow**: What happens when things diverge from
  the happy path (errors, skips, unauthorized).
- **Notes**: Assumptions, clarifications, or links to other artifacts.
- **Extension**: A branch that still serves this use case's goal. A
  branch that pursues a different goal, or that can never reach these
  postconditions, becomes a separate use case instead.
- **Tip**: For test-style (BDD) scenarios, the same entries map to Given
  (preconditions) / When (action) / Then (system response).
