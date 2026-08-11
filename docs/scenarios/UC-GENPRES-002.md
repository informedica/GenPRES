# Use Case: Calculate Orders in GenPRES Without Patient Context

## Use Case Summary

- **Use Case ID**: UC-GENPRES-002
- **Name**: Calculate orders without patient context (stand-alone usage)
- **Primary Actor**: Clinical user (prescriber / nurse), depending on
  authorization
- **Secondary Actors**: Main application (auth), GenPRES application
- **Goal**: Let an authenticated user open GenPRES without a patient,
  enter the patient context manually, and compose orders from the
  scenarios the knowledge rules allow, for calculation and exploration
  only.
- **Scope**: GenPRES prescribing module, launched from the main
  application without a patient selection
- **Level**: User goal
- **Trigger**: User clicks the button that opens GenPRES while no
  patient is selected in the main application, or GenPRES receives no
  redeemable launch token
- **Preconditions**:
  1. User is logged in to the main application and an authenticated
     session is active. Logging in and out are the main application's
     own use cases and are out of scope here.
  2. GenPRES is reachable from the main application.
  3. At least one of the following holds:
     - No patient is selected in the main application, so no launch
       record is written and GenPRES is opened without a token.
     - GenPRES cannot redeem a launch token, because the handoff store
       is unreachable, so the user's identity, role and the patient
       identifier cannot be established.

     If neither holds — a patient is selected *and* the launch token
     redeems — use [UC-GENPRES-001](UC-GENPRES-001.md) instead, see
     [Prescribe & manage patient orders](UC-GENPRES-001.md).
     A token that is present but rejected as unknown, expired or already
     redeemed does **not** fall through to this use case: access is
     denied and the user relaunches.
- **Postconditions (success)**: The user has obtained rule-derived order
  scenarios on screen for the manually entered patient context. Nothing
  is persisted and no pharmacy notification is sent.
- **Postconditions (fail)**: No calculation is produced; user is
  informed. Nothing is stored in either outcome.
- **Priority**: Medium
- **Frequency of use**: Occasional — calculation, exploration, teaching
- **Assumptions**: Authorization from the main application still governs
  what the user may do inside GenPRES. Persisting orders and notifying
  the pharmacy are unavailable in this mode regardless of the user's
  role, because there is no patient to store the orders against.

## Main Flow — stand-alone GenPRES scenario

Same layout as [UC-GENPRES-001](UC-GENPRES-001.md): the heading gives the step
number, the actor, and the action / trigger; the bullets give the system
response, the authorization required, the alternative or exception flows, and
any notes. Steps identical to [UC-GENPRES-001](UC-GENPRES-001.md) reference it
rather than restating it.

### 1 — User: open GenPRES with no patient selected

- **System Response**: Launches the GenPRES application inside the
  authenticated session, passing user context only, and marks the
  session as having no patient link.
- **Authorization**: Must be authenticated.
- **Alternative / Exception flow**:
  - GenPRES unavailable → show error and remain in main application.
  - A launch token is present *and* redeems in step 2 → continue with
    [UC-GENPRES-001](UC-GENPRES-001.md) instead.
- **Notes**: No second login expected (SSO).

### 2 — GenPRES: redeem the launch token, if there is one to redeem

- **System Response**: If a token is present and the handoff store is reachable,
  attempts the redemption of [UC-GENPRES-001](UC-GENPRES-001.md) step 2. On
  success the session has a verified user, role and patient, and
  [UC-GENPRES-001](UC-GENPRES-001.md) applies instead of this use case.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - Token present but unknown, expired or already redeemed → deny access and
    ask the user to relaunch; this use case does not apply.
  - No token at all, or handoff store unreachable → continue in this
    calculation-only mode with an unverified identity, and tell the user so.
- **Notes**: Proceeding unverified is acceptable only because this use
  case reaches no patient data and writes nothing: no orders are
  persisted, no pharmacy notification is sent, and the patient context
  is user-entered. Nothing is ever taken from the URL beyond the token
  itself.

### 3 — GenPRES: load all knowledge rules, if not already loaded

As [UC-GENPRES-001](UC-GENPRES-001.md) step 3 — dosing / drug / nutrition
rules are loaded into memory once and cached. If the rule source is unavailable,
the user is warned and calculation may be blocked or limited.

### 4 — GenPRES: retrieve the user profile

- **System Response**: Loads the user profile GenPRES holds for the
  identity available to it. No patient data is retrieved, because no
  patient is linked to the session.
- **Authorization**: System step.
- **Alternative / Exception flow**: No profile available — the usual
  case, since without a redeemed token there is no verified identity →
  continue with the unverified identity from step 2 and the default,
  most restrictive role.
- **Notes**: Diverges from [UC-GENPRES-001](UC-GENPRES-001.md) step 4, which
  also retrieves patient data from the Hospital Patient Data Platform.
  Here the Platform is never queried: there is no patient identifier to
  query it with, and the patient context is whatever the user types in
  the next step.

### 5 — User: enter the patient context manually

- **System Response**: Prompts for the patient attributes needed to
  compute order scenarios (age, weight, and other prescribing-relevant
  attributes), checks them for plausibility, and applies them to the
  session.
- **Authorization**: Must be authenticated.
- **Alternative / Exception flow**: Required attribute missing or out of
  range → prompt user; calculations that depend on it stay unavailable
  until it is supplied.
- **Notes**: These values are user-entered and are not cross-checked
  against a source of truth. The session is clearly marked as having no
  patient link, so results are never mistaken for a real patient's
  orders.

### 6 — User: add, modify or delete orders

- **System Response**: Applies the knowledge rules to the manually entered
  patient context and offers only the order scenarios those rules allow, exactly
  as in [UC-GENPRES-001](UC-GENPRES-001.md) step 6. The user selects among them.
- **Authorization**: Requires prescribing authorization; otherwise
  read-only.
- **Alternative / Exception flow**:
  - Unauthorized user → actions disabled / view-only.
  - No scenario satisfies the current selection → tell the user which
    constraint leaves nothing to choose from.
  - User overrides a constraint → as the override extension of
    [UC-GENPRES-001](UC-GENPRES-001.md), except that nothing is recorded beyond
    the session.
- **Notes**: The working set exists only in the session. Saving and
  pharmacy notification are disabled throughout. The scenarios are only
  as sound as the manually entered patient context they were computed
  from.

### 7 — User / System: close the GenPRES application

- **System Response**: Warns that the working order set cannot be saved
  and discards it on close.
- **Authorization**: N/A.
- **Alternative / Exception flow**: User cancels the close → return to
  the session.
- **Notes**: Diverges from [UC-GENPRES-001](UC-GENPRES-001.md) step 8, which
  offers to save. Here the discard is unavoidable, so the warning must be
  explicit.

## Differences from UC-GENPRES-001

| Aspect | [UC-GENPRES-001](UC-GENPRES-001.md) | UC-GENPRES-002 |
| :---- | :---- | :---- |
| Launch token | Must redeem | Absent or unredeemable |
| User identity | Verified from the token | May be unverified |
| Patient context | From main application | Entered manually |
| Existing orders | Looked up and loaded | None; always empty |
| Patient Data Platform | Retrieved from | Never queried |
| Save / persist | To GenPRES's own store | Not available |
| Replicated onward | Yes, outside GenPRES | Nothing to replicate |
| Notify pharmacy | Available (UC-GENPRES-003) | Not available |
| Close behaviour | Offers to save | Always discards, with warning |
| Orders of record | Yes | No |

## Implementation status

This use case describes **intended** behaviour, but it is also the
closest of the three to what ships today: GenPRES currently holds no
session state and persists nothing, so a stand-alone session is
effectively the only mode that exists. See the
[AP2019 vs GenPRES fit-gap analysis](../roadmap/fit-gap-ap2019-vs-genpres.md).

- **Built**: manual patient context (fit-gap 9.1 Fit, 9.3 Fit),
  rule-derived scenarios, dose-check severity levels.
- **Partial**: patient-field validation ranges and date consistency
  (fit-gap 9.2).
- **Not built**: the launch token of step 2, and the verified identity
  it would carry — fit-gap 10.1 and 10.5. Until then *every* session is
  unverified, so the distinction between this use case and
  [UC-GENPRES-001](UC-GENPRES-001.md) is not yet enforced anywhere.

## Related use cases

- **UC-GENPRES-001 — Prescribe & manage patient orders**
  ([main use case](UC-GENPRES-001.md)): the patient-linked flow, where
  orders are persisted and can be dispatched to the pharmacy.
- **UC-GENPRES-003 — Notify the pharmacy to prepare orders**
  ([Pharmacy notification](UC-GENPRES-003.md)): never available
  from this use case.
- **Log in / log out of the main application**: the main application's
  own use cases. Referenced as preconditions here, not described.

## Legend

See the Legend in [UC-GENPRES-001](UC-GENPRES-001.md) for how to read
and extend these use cases.
