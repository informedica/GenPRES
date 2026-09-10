# The launch sequence

How a User gets from a patient open in MainEHR to a GenPRES Session for that patient: UC-1's
main path. The overview numbers the steps; the sections after it zoom in on the steps that
have sub-steps. Rule and Concept numbers cite
[GenPRES-MainEHR-Integration-V8.md](GenPRES-MainEHR-Integration-V8.md).

This page goes one step beyond the trace in [`Integration.fsx`](Integration.fsx): the browser
key pair of step 3 and the signed request of step 7 are not in the script yet.

## Overview

```mermaid
sequenceDiagram
    participant L as MainEHR LaunchScript
    participant C as GenPRES Client
    participant S as GenPRES Server
    participant I as IdentityProvider
    participant D as GenPRES Database

    L->>C: 1. open #35;/session?launch=... (sealed PatientId, no login)
    Note over L: exits, learns nothing after this
    C->>C: 2. erase the Launch from URL and history, keep it in memory
    C->>C: 3. generate key pair, private key stays in the browser
    C->>S: 4. present Launch + public key
    S->>D: verify the Launch, append LaunchRecord {state, nonce, PatientId, expiry, public key}
    S-->>C: redirect to the IdentityProvider (the LaunchRecord holds what the Launch said)
    C->>I: silent device sign-on
    I-->>C: redirect back with an authorization code
    C->>S: callback with the code
    S->>I: redeem the code (back channel)
    I-->>S: BrowserIdentity, signed
    S->>D: 5. check the Launch unspent, resolve the User, read data, open the Session
    D-->>S: SessionRecord {SessionId, User, Role, Patient, public key}
    S-->>C: 6. Set-Cookie SessionId + UserContext, PatientContext, OrderContexts, OpenedToken
    Note over C,S: 7. every later request: cookie + proof signed with the private key + OpenedToken
```

| # | Step | What happens |
|---|------|--------------|
| 1 | Launch | The LaunchScript seals the PatientId and a fresh nonce under the key it shares with the Server, opens the browser on `#/session?launch={token}`, and exits. The Launch is single use and short-lived (Rules 2, 3, 29) and carries no login (Rule 4). |
| 2 | Erase | The Client removes the Launch from the URL and the browser history and keeps it only in memory, for retries within its lifetime (Rule 39). |
| 3 | Key pair | The Client generates a key pair with WebCrypto. The private key is not extractable and is stored in IndexedDB under the public key's thumbprint, next to keys of earlier launches in this browser. The public key goes with the Launch. |
| 4 | Identity | The Client presents the Launch and the public key. The Server verifies the Launch, keeps its contents in a LaunchRecord, and sends the browser to the IdentityProvider and gets it back with a signed BrowserIdentity over its own connection, never through the Client's hands. See [Step 4](#step-4-the-identity-round-trip). |
| 5 | Check and open | The Server checks that the Launch is unspent, asks the UserRegistry for the Role and the active Patient, reads the patient data and the record, and opens the Session in one act that stores the public key in the SessionRecord. See [Step 5](#step-5-check-and-open). |
| 6 | Session open | The response sets the SessionId as an HttpOnly, Secure, SameSite=Strict cookie (Rule 12) and returns UserContext, PatientContext, the OrderContexts to start from, the OpenedToken (Rule 34) and the thumbprint of the Session's public key. The Client deletes the private keys of other launches; the Session they belonged to is closed by this open (Rule 8). The Client keeps these in memory and shows the patient. |
| 7 | Every later request | The cookie says which Session, a proof signed with the private key says which browser, the OpenedToken says which TreatmentPlan was opened. See [Step 7](#step-7-a-signed-request). |

Two questions, two answers: *who is this* is the IdentityProvider's answer (step 4), *what may
they do, and on which Patient* is the UserRegistry's (step 5). The Server asks both at every
launch, and nothing the Launch says decides either.

## Step 4: the identity round trip

```mermaid
sequenceDiagram
    participant C as GenPRES Client
    participant S as GenPRES Server
    participant I as IdentityProvider
    participant D as GenPRES Database

    C->>S: 4.1 PresentLaunch (Launch, public key)
    Note over S: 4.2 verify the Launch: shared key, lifetime
    S->>D: CheckLaunchSpent (nonce)
    D-->>S: LaunchUnspent (a read, decides nothing)
    S->>D: append LaunchRecord {state, nonce, PatientId, expiry, public key}
    S-->>C: redirect to /authorize?...&state=... + Set-Cookie state (Lax, lifetime of the Launch)
    C->>I: 4.3 GET /authorize (silent device sign-on)
    I-->>C: redirect to /callback?code=...&state=...
    C->>S: GET /callback?code=...&state=... + cookie state
    Note over S: the cookie must match the state in the URL
    S->>D: read LaunchRecord by state
    D-->>S: nonce, PatientId, expiry, public key
    S->>I: 4.4 POST /token (code, client secret), back channel
    I-->>S: BrowserIdentity (signed id_token)
    Note over S: 4.5 continue with step 5
    S->>D: append the outcome to the LaunchRecord
    S-->>C: redirect to #35;/session, or #35;/session?refused={reason}
```

- **4.1** The Client presents the Launch and the public key.
- **4.2** The Server verifies the Launch: sealed under the shared key, within its lifetime
  (Rules 2, 3), and checks that it is not already spent. That check is a read: it refuses a
  plainly spent Launch before any round trip, but it decides nothing. A Launch that fails here
  is refused without a record. The Server then generates a random `state` and appends a
  LaunchRecord with the `state`, the Launch's nonce, PatientId and expiry, and the public key.
  The sealed Launch itself is not stored: everything step 5 needs is in the record, and the
  spent-mark is keyed by the nonce (Rule 2). The browser is redirected to the IdentityProvider
  with the `state`, and the same redirect sets a cookie holding the `state`: `HttpOnly`,
  `Secure`, `SameSite=Lax` (the callback is a cross-site top-level GET, on which `Strict` is
  not sent), `Path=/callback`, `Max-Age` the Launch's lifetime. The cookie is a random value
  with no meaning outside the LaunchRecord, not a bearer for anything (Rule 12 does not apply),
  and the Server keeps nothing in memory (Rules 32, 36).
- **4.3** The IdentityProvider signs the device on silently and redirects the browser to the
  Server's callback with an authorization code and the `state`.
- **4.4** The Server compares the `state` in the URL with the `state` cookie; without a match
  the callback is refused before the code is redeemed. That is what proves the callback comes
  from the browser that started the hop: a callback URL captured and opened elsewhere has no
  matching cookie. The Server then reads the LaunchRecord by `state`, redeems the code on its
  own connection to the IdentityProvider (edge C6), and receives the signed BrowserIdentity
  (Rule 4). The cookie is not deleted: it expires with the Launch, so a reload of the callback
  within the lifetime still matches and is answered from the record (4.5). After the lifetime
  the cookie and the record are both gone, and the callback is refused as expired.
- **4.5** The Server continues with step 5 and answers the callback with a redirect to
  `#/session`. On refusal it opens nothing and redirects to `#/session?refused={reason}`.
  A reason is not a secret; the Launch never appears in that URL. The Server appends the
  outcome to the LaunchRecord: a browser that reloads the callback URL within the Launch's
  lifetime, whose code the IdentityProvider has already redeemed, is answered as the first
  time (Rule 45). The record lives as long as the Launch: after the expiry it is dropped whole,
  never rewritten. The audit keeps the launch (Rule 46); the record does not.

Two proofs, two moments: the `state` cookie proves the callback comes from the browser that
started the hop; the public key proves, from step 7 on, that every request comes from the
browser that presented the Launch. The cookie expires with the Launch, and the key pair
cannot act before the Session exists, so neither replaces the other.

The redirect in 4.2 unloads the Client, so the Client cannot keep the Launch across the hop.
It does not need to: the LaunchRecord carries what the Launch said from 4.2 to 4.5, and the
Client only runs again when the launch is finished. For the same reason a refusal at 4.4
cannot be retried by the Client; it asks for a relaunch. The Server could offer the retry
itself, since the LaunchRecord still holds the nonce and the PatientId, by putting a link on
the refusal that restarts the redirect. That is an option for the identity hop's own plan, not
part of this page's sequence.

## Step 5: check and open

```mermaid
sequenceDiagram
    participant S as GenPRES Server
    participant R as UserRegistry
    participant P as PatientDataPlatform
    participant D as GenPRES Database

    Note over S: 5.1 the LaunchRecord's expiry has not passed
    S->>D: 5.2 CheckLaunchSpent (nonce)
    D-->>S: LaunchUnspent (a read, decides nothing)
    S->>R: 5.3 ResolveUser (BrowserIdentity)
    R-->>S: Role, mail address, active Patient
    S->>D: 5.4 ReadCredential (User)
    D-->>S: PIN is set
    S->>P: 5.5 ReadPatientData (PatientId)
    P-->>S: PatientData
    S->>D: 5.6 ReadRecord (PatientId), ReadSessionRecords (User)
    D-->>S: newest TreatmentPlan, this User's other Sessions
    Note over S,D: 5.7 one conditional act: spend the Launch, close the other Sessions,<br/>write SessionRecord {SessionId, User, Role, Patient, public key}
    S->>D: OpenSessionClosingOthers
    D-->>S: SessionWasOpened
```

- **5.1** Check that the expiry in the LaunchRecord has not passed while the hop ran
  (Rule 3). The Launch itself was verified at 4.2.
- **5.2** Check again, by nonce, whether the Launch is spent: the hop took time. Still a read,
  still deciding nothing.
- **5.3** Ask the UserRegistry for the Role (Rule 5), the mail address, and the Patient this
  User has active in MainEHR. That Patient must be the Launch's (Rule 6).
- **5.4** Check that a PIN is set (Rule 24). A Prescriber without one goes through UC-2 and
  the launch continues at 5.5 afterwards.
- **5.5** Read the patient data from the PatientDataPlatform, once (Concept 2).
- **5.6** Read the newest TreatmentPlan to start from (Rule 19) and this User's other open
  Sessions (Rule 8).
- **5.7** Open the Session in one conditional act (Rule 40): spend the Launch, close the other
  Sessions, write the SessionRecord with the public key. All of it commits, or none. The
  spent-mark is appended to the LaunchRecord of 4.2, keyed by the nonce, so one record per
  Launch carries the `state`, the nonce, the PatientId, the expiry, the public key, the
  outcome and the SessionId, and never the sealed Launch.

The check in 5.2 and the spend in 5.7 are two different things. By the time the open runs the
check may be out of date. What decides is the open, which spends the Launch in the same act
that writes the record. So a launch cannot spend a Launch and then fail to open, and two
presentations that both passed the check cannot both open.

## Step 7: a signed request

Every request after the launch carries two things:

- the SessionId cookie, which names the Session;
- a proof signed with the private key from step 3, which names the browser. It covers the
  HTTP method, the URL, a hash of the body, the time, a unique id, and the OpenedToken, which
  names the TreatmentPlan the Session opened with (Rule 34). This is the DPoP pattern
  ([RFC 9449](https://www.rfc-editor.org/rfc/rfc9449)).

The Server reads the SessionRecord by SessionId, verifies the signature against the stored
public key, checks method, URL, body hash, time and uniqueness, and compares the OpenedToken
with the head of the record. If a newer TreatmentPlan exists, the response says so (Rule 21).

Implementing the signed proof is a later plan. The key pair is generated at the launch so
that the SessionRecord already holds the public key when that plan lands. Keys are kept per
thumbprint so that a second launch in another tab, if refused, cannot take the first tab's
key away.

## Retries

A presentation can be repeated within the Launch's lifetime: after a lost response, or after
a transient refusal such as an unreachable IdentityProvider. The public key correlates the
repeat. A second presentation with the same public key is answered as the first was and gets
the same cookie; nothing opens twice (Rule 2, same browser). A presentation with another public
key, or with none, is refused as spent: the Server cannot tell it from another browser. After
the lifetime the Launch is expired.

Rule 2 names the BrowserIdentity as the same-browser test. This page uses the public key,
because the Server has it at every presentation, before the identity is known; the
BrowserIdentity is a second test once the hop is done. Naming both in Rule 2 is a follow-up
on V8.

## Refusals

Every refusal ends the same way: no Session opens (Rule 7), and the Client shows the reason.

| Refusal | Fails at | The Client offers | V8 |
|---------|----------|-------------------|----|
| Launch expired, spent, or not sealed under the key | 4.2 (5.1, 5.2 again after the hop) | a relaunch from MainEHR | ext 4a |
| No BrowserIdentity | 4.4 | a relaunch; the Client has no Launch left after the redirect | ext 3c |
| No Role | 5.3 | a fresh anonymous open that carries nothing over (UC-7) | ext 5a |
| Another active Patient, or none | 5.3 | a relaunch after activating the right Patient | ext 5b |
| No PIN | 5.4 | enrolment (UC-2); abandoned enrolment leaves no Session | ext 5d |
| Server unreachable after the page was served | 4 | automatic retries from memory, then a Retry button | ext 3a |

## Two launches at once (ext 8b)

Rule 8 is a count, and a count read and then written back is a race. Two launches of User A,
two Launches in two browsers, run through to the open together.

```mermaid
sequenceDiagram
    autonumber
    actor U as User A
    participant C1 as Browser 1
    participant C2 as Browser 2
    participant S as GenPRES Server
    participant D as GenPRES Database

    U->>C1: launch (Launch A)
    U->>C2: launch (Launch B)
    C1->>S: PresentLaunch (Launch A)
    C2->>S: PresentLaunch (Launch B)

    Note over S,D: two different Launches, so both nonces are unspent<br/>and both run the whole pipeline

    S->>D: CheckLaunchSpent (A)
    S->>D: CheckLaunchSpent (B)
    D-->>S: LaunchUnspent (A)
    D-->>S: LaunchUnspent (B)

    Note over S: registry, credential, data, record - twice over

    S->>D: OpenSessionClosingOthers (ses-001)
    S->>D: OpenSessionClosingOthers (ses-002)

    Note over D: Rule 40: each open closes this User's other Sessions in<br/>the same act. Whichever lands second supersedes the first.

    D-->>S: SessionWasOpened (ses-001)
    D-->>S: SessionWasOpened (ses-002)
    S-->>C1: SessionOpened
    S-->>C2: SessionOpened

    Note over D: one open Session, whichever won. The other is Superseded<br/>and owes a notice, delivered as ext 8a
```

Both browsers are told a Session opened, and only one still has it. The Database decided, and
the loser's Client learns at its next request that its Session has ended (Rule 11). This is a
different race from two presentations of the *same* Launch: there the spend inside the open
(5.7) settles it and exactly one Session opens; here two Launches contend for the per-User
limit.

## As built against stubs

Steps 1 to 6 run end to end in the demo server since
[plan 605](../../implementation-plans/605-launch-with-server-stubs.md). What crosses the
browser goes over HTTP as it will with MainEHR and the IdentityProvider: the Launch of step 1,
the presentation of 4.1, the redirects of 4.2 and 4.3, the callback of 4.4 and the answer of
4.5 and 6, with the cookies each sets. What the Server does on its own side (redeeming the code
in 4.4, the UserRegistry and PatientDataPlatform reads of step 5, the Database) is an in-process
call through a port, answered by a stand-in mounted when `GENPRES_PROD=0`; the real adapters
will make those calls over the back channel without the port changing shape. The walkthrough is in
[DEVELOPMENT.md](../../../DEVELOPMENT.md#simulating-the-launch-sequence).

| Party | Stand-in | What it does |
|-------|----------|--------------|
| MainEHR LaunchScript (step 1) | the `/stub/launch` page | mints a Launch sealed under a key made at server start, two minutes, and opens the browser on `#/session?launch=…`; the identity choice made on the page travels in a cookie to the stub IdentityProvider |
| IdentityProvider (4.3, 4.4) | `/authorize` and an in-memory code store | issues a one-time code for the chosen identity, or reports `no-identity`; the code is redeemed on the server's side of the callback and pruned after the Launch lifetime |
| UserRegistry (5.3, 5.4) | the stub directory | answers Role, active Patient and PIN state per identity choice: `prescriber`, `reader`, `prescriber-other-patient`, `no-pin`, `unknown` |
| PatientDataPlatform (5.5) | the stub patient data | answers an empty patient for every PatientId, none for `no-data` |
| GenPRES Database | one in-memory state per server start | LaunchRecords by nonce, SessionRecords, endings; every transition is a pure function over it, run under one lock, so 5.2 and 5.7 cannot interleave |

Where the code departs from the text above, on purpose:

- **One state cookie per hop.** The cookie of 4.2 is named `genpres_launch_state.<state>`, not
  a single `state` cookie, so that two tabs launching at once (ext 8b) do not overwrite each
  other's proof.
- **A replay whose Session is gone.** A reloaded callback within the lifetime is answered as
  the first time (4.5, Rule 45). When the Session it opened has since been superseded, the
  answer is the same redirect to `#/session`, and the next `GetSession` tells the ending.
- **The ending is acknowledged, not told once.** A Client whose Session ended is told at every
  `GetSession` until it acknowledges with `CloseSession`, which drops the mark; a notice lost in
  transit is repeated instead of lost. [session-endings.md](session-endings.md) stands: nothing
  is discharged by the telling.
- **A Reader needs no PIN.** 5.4 binds Prescribers only (Rule 25, ext 5c).
- **No patient data is not a refusal.** When 5.5 finds nothing, the Session opens with the
  Launch's PatientId and an empty patient (ext 6a); a data outage does not block prescribing.
- **The key pair's thumbprint** is stored in the SessionRecord at 5.7, ready for step 7.

The PIN detour of 5.4 is built too: a Prescriber without a PIN suspends into
[uc-02](uc-02-enrolment.md) and continues once the PIN is set (its as-built note is there).

Not built: step 7 (the signed request and the OpenedToken check), the audit (Rule 46), the
newest TreatmentPlan of 5.6, and the absolute lifetime and idle endings of Rule 10.

## Left out

- **The PIN detour.** A Prescriber with no PIN is not refused: the launch suspends into UC-2
  and continues at 5.5 once the PIN is set.
- **The audit.** Every launch, honored or refused, is appended to the audit (Rule 46).
- **Everything after the launch**: prescribing and signing, and the ten other use cases.
- **The confirmation code.** UC-2 and UC-6 mail a code to set or replace a PIN. It is not the
  authorization code of step 4, which never leaves the browser and the two servers.

---

The trace in [`Integration.fsx`](Integration.fsx) carries UC-1 without the key pair and the
signed request. Bringing the script, and the README's rule that the trace is right, in line
with this page is a follow-up.
