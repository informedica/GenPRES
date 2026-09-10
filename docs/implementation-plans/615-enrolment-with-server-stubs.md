# Implementation plan for issue 615

## Problem description

Plan [605](605-launch-with-server-stubs.md) runs the whole launch
([uc-01](../scenarios/integration/uc-01-launch.md) steps 1 to 6) against server-side stubs,
with one departure from the design: a Prescriber without a PIN is *refused* with `enrolment`.
Rules 7 and 25 of the [integration design](../scenarios/integration/GenPRES-MainEHR-Integration-V8.md)
say a missing PIN refuses nothing: the launch suspends into enrolment
([uc-02](../scenarios/integration/uc-02-enrolment.md)) and continues once the PIN is set. The
credential half of the Database (PIN, wrong-count; Rules 24, 28, 37) and the MailService
(Rule 27) have no server code, and `UserStanding.PinSet` lets the stub registry answer what the
Database should. Issue [#615](https://github.com/informedica/GenPRES/issues/615) asks for UC-2
end to end against stubs, so that the `no-pin` choice on `/stub/launch` enrols instead of
refusing.

## Approaches considered

1. **The signed request of uc-01 step 7 first.** Rejected for now: it has no rule number and
   no model in `Integration.fsx`; its Rule 21 comparison needs a TreatmentPlan head that does
   not exist; and a proof over `processCommand` protects nothing until that API is tied to the
   Session, which is a decision of its own (UC-7's direct open must keep working). Its client
   side also needs `Keys.sign` and an async signing hook that Fable.Remoting's synchronous
   header provider does not offer. It follows this plan, with "session-bound `processCommand`"
   as its first step.
2. **Keep `EnrolmentRequired` as a refusal until a real MailService exists.** Rejected: the
   launch stays wrong at 5.4, and the PIN question, the one act and the two mails stay
   untested.
3. **UC-2 against stubs hosted by the server.** Chosen. It is fully specified (Rules 7, 24 to
   28, 37; the uc-02 page; `uc2 ()` in `Integration.fsx`), it is the last place where the
   launch itself departs from its design, and it puts in place the Credential store and the
   MailService port that signing (UC-3, Rule 42), UC-6 and the `WrongPinLimit` ending need.

## Chosen approach

Approach 3, in the shape of plan 605: stand-ins in memory, adapters in the DMZ
([ADR-0001](../adr/0001-system-architecture.md)), mounted when `GENPRES_PROD=0`; every
transition pure over the `Hop.State` record under the existing lock, so it runs under Expecto
and in FSI.

### The PIN lives in the Database, not the registry (Rule 24)

`UserStanding.PinSet` was a plan-605 shortcut. uc-02 reads the credential from the Database
(`ReadCredential`). `UserStanding` loses `PinSet` and gains `MailAddress` (Rule 27); the state
gains `Credentials: Map<UserId, Credential>` with `Credential = { PinHash: PinHash option;
WrongCount: int }`. The stub seeds `prescriber` with a PIN set and `no-pin` without, so a
`no-pin` login enrols once per host start and its next launch opens directly, as in production.

The PIN hash is PBKDF2-SHA256 with a per-credential salt, in the stub too, so the store has the
production shape; the iteration count is one constant to tune later. PIN format: four to six
digits (V8 names no format; recorded as an assumption to confirm).

### Ports (`ServerApi.Ports.fs`), stubs (`ServerApi.Adapters.fs`)

```fsharp
type Mail = { To: string; Subject: string; Body: string }
type MailPort = { send: Mail -> unit }                                              // Actor M, edge C10
type UserStanding = { User: UserContext; ActivePatientId: string option; MailAddress: string }

type CallbackResult = | Opened ... | Refused ... | Superseded ... | Enrolling of attemptId: string * redirect: string
type SupplyPinResult = | Opened of sessionId: string * SessionOpened | Refused of PinRefusal
```

`SessionPort` gains `supplyPin: attemptId: string -> code: string -> pin: string ->
Async<SupplyPinResult>` and `findEnrolment: attemptId: string -> Async<EnrolmentPending option>`.

Stub MailService: an in-memory outbox behind the lock and a `GET /stub/mail` page (full scope
only, no inline script or style) listing the mails newest first, so the tester reads the code
there. Stub registry: `MailAddress = $"{login}@stub.example"`.

### The suspended launch

At the callback ladder a Prescriber whose credential has no PIN no longer refuses. Two records
carry the suspended launch, because the code belongs to the credential and the browser to the
launch: a `PendingCode { UserId; MailAddress; CodeMac; Expiry; Tries }`, one per credential,
and an `Enrolment { Attempt; UserId; Login; DisplayName; PatientId; PublicKey; Expiry }`, one
per launch, holding the public key of the browser that made it. In one act the server appends
both (or only the attempt, when a code already stands, below), mails the six-digit code (CSPRNG;
stored as an HMAC under the host key, "the code as a mac", Rule 37) to the address the registry
gave on this request (Rule 27), records the LaunchRecord's outcome as suspended, and answers
`Enrolling (attempt, "/#/session")`. The edge sets `genpres_enrolment=<attempt>` (HttpOnly, Strict, `Path=/`,
`Max-Age` the attempt lifetime) and redirects. Nothing in the URL: the client learns of the
pending enrolment at its next `GetSession`.

**One live code per credential** (Rule 37, ext 2a): a second `no-pin` launch while a code
stands gets an attempt of its own, bound to the *same* code, and mails nothing, so the code
User A is about to read is not voided. Either browser can supply the code; the Session opens
on the key of the attempt that supplied it, which is the key that browser holds (step 7 will
sign with it). Setting the PIN drops the code and every attempt bound to it.

**Abandonment**: an attempt expires after 15 minutes, a mail round trip. The model's
`AwaitingPinChoice` is never collected; bounding it is a deliberate deviation, recorded here.
`GetSession` on an expired attempt deletes the cookie and answers `NotFound`.

### Supplying the PIN

`supplyPin attempt code pin`, on the attempt named by the cookie, never by the client:

- unknown or expired attempt: `Refused AttemptExpired`;
- wrong code: `Tries + 1` on the code, `Refused (WrongCode attemptsLeft)` while tries remain;
  the third wrong code drops the code and its attempts and answers `Refused CodeVoid`, a
  terminal answer (ext 2b: "a few tries, then the code is void; a fresh launch mails a fresh
  one");
- PIN not four to six digits: `Refused PinFormat`, no try spent;
- else **one act** (Rules 37, 40): the credential's PIN hash is set with a zero wrong-count
  (Rule 28), the attempt dropped, the "your PIN was set" mail sent to the address the registry
  answers on *this* request (Rule 27), falling back on the attempt's address when the registry
  has no answer (uc-02, last bullet); then uc-01 continues at 5.5 to 5.7 through the existing
  `openSession` (patient data, other Sessions closed, `SessionOpened` with the thumbprint of
  the supplying attempt's key), and the answer is `Opened (sessionId, opened)`.

### Wire (`Shared/Api.fs`, `Shared/Types.fs`)

```fsharp
type EnrolmentPending = { DisplayName: string; MailHint: string }   // "n***@stub.example"
type PinRefusal = | WrongCode of attemptsLeft: int | CodeVoid | AttemptExpired | PinFormat
type SessionCommand = | GetSession | CloseSession | SupplyPin of code: string * pin: string
type SessionResponse = | SessionResp ... | SessionClosed | SessionEnded ... | EnrolmentPending of EnrolmentPending | PinRefused of PinRefusal
```

`GetSession` answers `EnrolmentPending` when there is no session cookie and the enrolment
cookie names a live attempt. `SupplyPin` answers `SessionResp (Some opened)` and sets the
session cookie (deleting the enrolment cookie) on success, `PinRefused` otherwise.
`LaunchRefusal.EnrolmentRequired` stays: the `sessionDisabled` production port answers it, and
`supplyPin` there refuses `AttemptExpired`.

### Client (direct edits)

`SessionMachine.fs`: `Session.Enrolling of EnrolmentPending * PinRefusal option`,
`ResumeResult.Enrolling`, `SessionMsg.SupplyPin of code * pin` and `PinAnswered of
Result<PinOutcome, string>`, effect `CallSupplyPin`. From `Enrolling`: `SupplyPin` calls;
`Opened` goes to `Open` with `KeepKey` and `SetPatient`; a `WrongCode` or `PinFormat` refusal
stays `Enrolling` with the refusal shown; `CodeVoid` and `AttemptExpired` are terminal and
become `Refused (EnrolmentRequired, None)`, the gate that asks for a relaunch; `OpenAnonymous`
goes to `Anonymous`. `SessionGatePolicy.fs`: an `Enrolling` gate with
three fields (code, PIN, PIN again) and a submit, every text a `Terms` case (title, text with
the mail hint, the three labels, submit, wrong code with `{0}`, format, void, expired).
`Views/SessionGate.fs` renders the fields with `inputMode="numeric"`.

### What stays as is

The identity hop, the seal, the endings, `Keys.fs` without `sign`, `OpenedToken` unconsumed,
`processCommand` unbound, `Integration.fsx`, the audit (Rule 46), UC-6, the `WrongPinLimit`
ending.

## Confidence

High on the server shape: it is uc-02 step by step over the state record and lock plan 605
built, and `openSession` is reused unchanged. Medium on two details to confirm in review: the
PIN format, and the 15-minute bound on an attempt.

## Steps

Each step is one PR against `master`, script-first for server and Shared code, direct edits for
the client, migration by the maintainer after review.

1. **Credential store and MailService, no behaviour change.** `Server/Scripts/Enrolment.fsx`:
   `Credential`, `PinHash` (PBKDF2, salt), `State.Credentials` seeded per stub login, the
   callback reading the credential instead of `PinSet` (still refusing), `MailPort` with the
   in-memory outbox and the `/stub/mail` page, `UserStanding.MailAddress`; tests. Migrate to
   `Ports.fs`, `Adapters.fs`; `Server.fs` mounts `GET /stub/mail` behind `not IsProd` (the Vite
   proxy already covers `/stub`).
2. **Suspend and supply, server and wire.** Script: the `Enrolment` record, the code mac,
   `CallbackResult.Enrolling`, `supplyPin` as one act, the two mails, the one-code rule, the
   expiry; `Api.fs` and `Types.fs` types above; `CompositionRoot`: `GetSession` answering
   `EnrolmentPending`, `SupplyPin`; `Server.fs`: the `genpres_enrolment` cookie (`HttpTests`);
   `sessionDisabled` refusing. Client minimal so `master` keeps working: `EnrolmentPending`
   lands in `Session.Enrolling` and the gate shows the pending text with a relaunch action, no
   form yet. Tests: every `supplyPin` branch, the fresh-address rule and its fallback, the
   one-code rule with two attempts (each opening on its own key), the third wrong code voiding
   the code for both, the expired attempt at `GetSession`, the Reader never asked (Rule 26).
3. **The enrolment form.** Gate policy and view: code, PIN, repeat, submit, the refusal texts;
   `Terms` script-first (`Shared/Scripts/Localization.fsx`) with English and Dutch sheet rows;
   machine and gate tests. Browser: `no-pin` reaches the gate; `/stub/mail` shows the code; code
   plus PIN opens the Session as Stub Prescriber (no PIN); a relaunch of `no-pin` opens
   directly; three wrong codes end in a relaunch; an expired attempt lands anonymous.
4. **Docs.** uc-02 gets an as-built note (what the stubs stand in for, the bounded attempt);
   uc-01's "Left out" PIN bullet is updated; `DEVELOPMENT.md`'s walkthrough gains the enrolment
   path and `/stub/mail`; this plan gets its as-built table.

## Acceptance

- Against `GENPRES_PROD=0 dotnet run`: `/stub/launch` with `no-pin` ends on the enrolment gate;
  the Network tab shows `/callback` answering a redirect to `#/session`, `GetSession` answering
  `EnrolmentPending`, and after the form `SupplyPin` answering `SessionResp`; the launch token
  never reappears. `/stub/mail` lists the code mail before the gate is shown (Rule 25 ordering,
  as `uc2 ()` checks) and the "PIN was set" mail after.
- `prescriber` and `reader` open as before; `unknown` never reaches the PIN question.
- With `GENPRES_PROD=1` and a valid password: `/stub/mail` is 404 and `SupplyPin` is refused.
- `dotnet run ServerTests`, the Fable compile and Fantomas stay green after every step.
