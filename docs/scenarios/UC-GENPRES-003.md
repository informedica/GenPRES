# Use Case: Notify the Pharmacy to Prepare Orders

## Use Case Summary

- **Use Case ID**: UC-GENPRES-003
- **Name**: Notify the pharmacy to prepare orders
- **Primary Actor**: Clinical user with authorization to dispatch to pharmacy
- **Secondary Actors**: Pharmacy, GenPRES application, main application (auth)
- **Goal**: Let an authorized user send a preparation request for selected TPN and continuous medication orders to the
  pharmacy, and have that notification recorded against those orders.
- **Scope**: GenPRES prescribing module, pharmacy dispatch
- **Level**: User goal
- **Trigger**: User chooses to notify the pharmacy for one or more saved orders, at any time during a prescribing
  session
- **Preconditions**:
  1. A [UC-GENPRES-001](UC-GENPRES-001.md) session is active: the launch token redeemed successfully, so user, role and
     patient context come from the launch record — see [Prescribe & manage patient orders](UC-GENPRES-001.md). Active
     means the session record is still open: a session that has been marked ended under any of the marks in that use
     case's session lifecycle can no longer dispatch, just as it can no longer save.
  2. The orders to dispatch exist, are complete and have been saved.
  3. User's role includes authorization to dispatch to pharmacy.
- **Postconditions (success)**:
  1. A preparation request for the selected orders has been sent to, or queued for, the pharmacy.
  2. The notification is recorded against those orders.
  3. The user has seen a confirmation.
- **Postconditions (fail)**:
  1. No preparation request is sent and none is recorded.
  2. The orders are unchanged.
  3. The user is informed.
- **Priority**: High
- **Frequency of use**: Once or a few times per prescribing session, for TPN and continuous medication orders
- **Assumptions**: Applies to TPN and continuous medication orders. Dispatch is never available from
  [UC-GENPRES-002](UC-GENPRES-002.md), which has no patient link and persists nothing.

## Main Flow — pharmacy notification scenario

Same layout as [UC-GENPRES-001](UC-GENPRES-001.md): the heading gives the step number, the actor, and the action /
trigger; the bullets give the system response, the authorization required, the alternative or exception flows, and any
notes.

### 1 — User: select the orders to prepare

- **System Response**: Presents the dispatchable orders of the current session and marks the user's selection.
- **Authorization**: Requires authorization to dispatch to pharmacy; otherwise the action is not offered.
- **Alternative / Exception flow**:
  - No dispatchable orders in the session → tell the user and end the use case.
  - Unauthorized user → dispatch disabled.
- **Notes**: Only TPN and continuous medication orders are offered.

### 2 — GenPRES: check that the selected orders are complete, saved and current

- **System Response**: Confirms that each selected order is fully determined — every quantity the pharmacy needs is
  resolved — that it has been persisted, and that the version the session holds for this patient's order state is still
  the stored one, so what is about to be dispatched is what is saved now rather than what was saved before a colleague
  replaced it.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - Order not fully determined → prompt the user to finish it first; return to [UC-GENPRES-001](UC-GENPRES-001.md) step
    6.
  - Order not saved → prompt to save first; return to [UC-GENPRES-001](UC-GENPRES-001.md) step 7.
  - Stored version has moved on — another user saved this patient → do not dispatch. Say so and send the user back to
    [UC-GENPRES-001](UC-GENPRES-001.md) to settle it under the stale-version extension there, by overwriting or
    reloading, before dispatching again.
- **Notes**: Nothing is dispatched that is not already an order of record. Orders carrying a deliberate override are
  dispatchable, but the override travels with them so the pharmacy sees it. The version check is what keeps "an order of
  record" meaning the *current* record: a saved order can be superseded by someone else's save at any moment, and
  dispatch is the point where that stops being recoverable — the pharmacy starts preparing.

### 3 — User: confirm the pharmacy notification

- **System Response**: Shows what will be sent — orders, patient and destination pharmacy — and asks the user to
  confirm.
- **Authorization**: Requires authorization to dispatch to pharmacy.
- **Alternative / Exception flow**: User cancels → nothing is sent and nothing is recorded; return to the prescribing
  session.
- **Notes**: Explicit confirmation, because dispatch reaches an external party and starts physical preparation.

### 4 — GenPRES: send the preparation request to the pharmacy

- **System Response**: Re-checks the version immediately before transmitting, then transmits the preparation request for
  the confirmed orders to the pharmacy.
- **Authorization**: System step.
- **Alternative / Exception flow**:
  - Version moved between step 2 and here → send nothing and return the user to step 2's stale-version branch. The
    window is small but the check is cheap, and this is the last moment at which it costs nothing.
  - Pharmacy channel down → queue the request for later transmission, or warn the user that it could not be sent; either
    way say which of the two happened.
  - Partial failure → report exactly which orders were sent and which were not.
- **Notes**: The user must never be left believing an unsent request was delivered. A queued request carries the version
  it was built at; if the plan has been replaced by the time the queue drains, that request is stale in exactly the way
  step 2 guards against, and should not be sent silently.

### 5 — GenPRES: record the notification and confirm to the user

- **System Response**: Records the notification (orders, time, user, destination, sent or queued) against the orders,
  and confirms to the user.
- **Authorization**: System step.
- **Alternative / Exception flow**: Recording fails after a successful send → warn the user that the pharmacy has the
  request but it is not recorded, so a repeat dispatch risks duplicate preparation.
- **Notes**: The record is what later readers of the order rely on to see whether preparation was requested.

## Implementation status

This use case describes **intended** behaviour. None of it is built. See the [AP2019 vs GenPRES fit-gap
analysis](../roadmap/fit-gap-ap2019-vs-genpres.md):

- **Not built**: the electronic hand-off itself — fit-gap 5.11b, and 9.8c (order file export). Under 9.10 (pharmacy
  communication) the print half exists for parenteral nutrition (5.11a); email, electronic hand-off and VTGM preparation
  letters do not, which is why that row is Partial rather than a Gap.
- **Blocked on**: saved orders, since precondition 2 requires them — fit-gap 9.6 and [patient-state
  persistence](../roadmap/feature-patient-persistence.md).
- **Blocked on**: the version check at steps 2 and 4, which needs the order-state versioning described in
  [UC-GENPRES-001](UC-GENPRES-001.md) steps 5 and 7 — fit-gap 9.7 and the versioning half of 9.6. Until it exists,
  dispatch cannot tell a current plan from one a colleague has replaced.
- **Blocked on**: session records, since precondition 1 now turns on a session still being open — also
  [UC-GENPRES-001](UC-GENPRES-001.md), where they are likewise not built.
- **Blocked on**: dispatch authorization as a real role, and the attribution of who dispatched what — fit-gap 10.2 and
  10.4. Signing (fit-gap 10.3) lived MetaVision-side in AP2019; whether it belongs here is still open.

## Related use cases

- **UC-GENPRES-001 — Prescribe & manage patient orders** ([main use case](UC-GENPRES-001.md)): the prescribing session
  this use case is triggered from, and which produces the saved orders it dispatches.
- **UC-GENPRES-002 — Calculate orders without patient context** ([Stand-alone usage](UC-GENPRES-002.md)): dispatch is
  never available there.

## Legend

See the Legend in [UC-GENPRES-001](UC-GENPRES-001.md) for how to read and extend these use cases.
