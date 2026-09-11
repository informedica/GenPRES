# Implementation plan for issue 622

## Problem description

Plans [605](605-launch-with-server-stubs.md) and [615](615-enrolment-with-server-stubs.md) run
the launch ([uc-01](../scenarios/integration/uc-01-launch.md) steps 1 to 6) and the enrolment
([uc-02](../scenarios/integration/uc-02-enrolment.md)) against server-side stubs. Nothing in
GenPRES can yet be signed: the client's cart (`OrderPlan.Scenarios`) has add and delete only, no
plan reaches a record, `OpenedToken` is a placeholder no request consumes,
`Credential.WrongCount` is carried but never counted, and `SessionEnding` knows only
`SupersededByLaunch`. [uc-03](../scenarios/integration/uc-03-prescribe-and-sign.md) is "the whole
of how a TreatmentPlan comes into being": a signing challenge over exactly what was shown, the
PIN asked modally, and one transaction that re-verifies everything and appends the plan
(Rules 42, 43 of the [integration design](../scenarios/integration/GenPRES-MainEHR-Integration-V8.md)),
with the wrong-PIN count and lock of Rule 28. Issue
[#622](https://github.com/informedica/GenPRES/issues/622) asks for UC-3 end to end against stubs.

Naming: the integration design's TreatmentPlan is the code's `OrderPlan`. The type exists as
the cart (`Patient`, `Selected`, `Filtered`, `Scenarios`, `Totals`); a signed version of it is a
`SignedOrderPlan`. This plan uses the code's names.

## Approaches considered

1. **The signed request of uc-01 step 7 first.** Rejected again: it has no rule number and no
   model in `Integration.fsx`, and its Rule 21 comparison needs a record head that only signing
   creates. UC-3 is fully specified (Rules 19 to 23, 28, 33, 34, 38, 42 to 45; `uc3 ()` and
   `dbCommit` in `Integration.fsx`) and creates that head. Step 7 follows this plan.
2. **Bind `processCommand` to the Session first**, as uc-03 step 1 reads it (`ReadSessionRecord`,
   `TouchIfOpen`). Rejected for now: UC-7's direct open must keep working without a Session,
   and Rule 9's touch belongs to the idle lifetime, which is not built. Only the signing calls
   need a Session; Compute stays as it is. Recorded as a deviation.
3. **Sealed tokens for the challenge and the OpenedToken**, as the model has (`Token`, `Claim`,
   `Spent`). Rejected for now: no gain while there is one host and the Session is in memory. The
   challenge is kept on the state, one per Session, and the OpenedToken stays opaque and is
   compared with the one the Session holds; both are sealed when the store leaves the process
   ([#516](https://github.com/informedica/GenPRES/issues/516)).
4. **UC-3 against stubs hosted by the server, signing calls cookie-bound.** Chosen.

## Chosen approach

Approach 4, in the shape of plans 605 and 615: the record half of the Database in memory in
`Hop.State`, behind the existing lock, every transition pure over the state record so it runs
under Expecto and in FSI; mounted when `GENPRES_PROD=0`, the production port refusing.

Decisions taken with the issue: the base lock delay of Rule 28 is one minute, doubling with
each further wrong entry past the limit of three (the model counts in ticks); a second stub
Prescriber, `prescriber-b`, so Rule 20 can be shown in two browsers.

### Wire (`Shared/Types.fs`, `Shared/Api.fs`)

```fsharp
type OrderPlanHead = { Id: string; No: int; By: UserContext; SignedAt: DateTime }
type SignedOrderPlan = { Head: OrderPlanHead; PatientId: string; Base: string option
                         Scenarios: OrderScenario[]; Patient: Patient }
type SessionEnding = SupersededByLaunch | WrongPinLimit                     // Rule 28

type SigningRefusal =
    | NoSession | NoPatient | NotPrescriber                                  // Rules 33, 38
    | Blocked of OrderPlanHead                                               // Rule 20: whose, when
    | DataChanged                                                            // Rule 44
    | StaleToken | ChallengeMismatch | ChallengeExpired                      // Rules 34, 43
    | PinWrong of attemptsLeft: int | PinLimit | Locked of until: DateTime   // Rule 28

type Submission =                                                              // Rules 34, 43, 45
    { Plan: OrderPlan; Opened: OpenedToken; Challenge: string; Pin: string; IdemKey: string }

type SigningCommand = RequestSignChallenge of OrderPlan * OpenedToken | Submit of Submission
type SigningResponse =
    | ChallengeIssued of challenge: string
    | Submitted of SignedOrderPlan * OpenedToken                             // over the new baseline
    | Refused of SigningRefusal
```

`IServerApi` gains `processSigning: SigningCommand -> Async<SigningResponse>`, cookie-bound
like `processSession`; the composition root grows one arm and `createServerApi` keeps its
signature. `SigningCommand.toString` prints the case name only, never the PIN or the plan. The
server reads `Patient` and `Scenarios` of the submitted `OrderPlan` and ignores its UI fields
(`Selected`, `Filtered`, `Totals`).

### The record (`Hop`, pure over `State`)

- `State` gains `Records: Map<patientId, SignedOrderPlan list>` (newest first);
  `Challenges: Map<sessionId, Challenge>` with `Challenge = { Nonce; Patient; Scenarios; Expiry }`,
  one per Session, a re-request replacing it, spent by removal at the commit, the lifetime two
  minutes (the launch's: the time to read the modal and enter a PIN, so what is signed was
  checked against the platform moments ago, Rule 44); `Answered: Map<sessionId * idemKey,
  SigningResponse * DateTime>` (Rule 45), keyed by the Session as well as the key so that no
  other Session's key finds an answer, refusals remembered too, pruned with the same lifetime.
- `SessionRecord` gains `OpenedWith: string option`, the id of the head at open (Rule 19), `None`
  when the record is empty; `openWith` reads `Records` for it. The wire `OpenedToken` stays
  `opened-<id>`; a Submission must present the token the Session holds now (Rule 34), and the
  commit re-mints it, so a spent one no longer matches.
- `Credential` gains `LockedUntil: DateTime option`. `Credential.lockFor count` is one minute
  times two to the power `count - 3`; `Credential.verify now pin credential` returns whether
  the PIN is accepted and the credential as it stands after the entry, as the model's
  `UserCredential.verify`: correct and unlocked accepts, zeroes the count and clears the lock;
  correct while locked refuses and changes nothing; wrong adds one and locks at three or
  beyond. `Credential.withPin` already zeroes both (Rule 37).

### The challenge

`Hop.challenge now patientData sid (plan, opened) state`, in order: the Session found with a
User and a Patient (else `NoSession`, `NoPatient`); `plan.Patient` equal to the Session's
patient data (Rule 33, else `NoPatient`); the Role Prescriber (else `NotPrescriber`, also for
the anonymous Session and a Reader); the OpenedToken presented equal to the Session's current
one (Rule 34, else `StaleToken`, so a stale tab is told before it is asked for a PIN); the
patient data re-read and equal to the Session's (Rule 44, else
`DataChanged`, the DataNotice round trip deferred); the head of the record equal to the one the
Session opened with (Rule 20, else `Blocked head`); then a nonce is minted and the challenge
stored for the Session, answering `ChallengeIssued nonce`.

### The commit

`Hop.commit now newId standing send sid submission state`, the ladder in the model's order
(`dbCommit`), the PIN last so a Submission that was never going to land costs no attempt:

1. The Session open with a User and a Patient; `Plan.Patient` equal to the Session's (Rule 33).
2. `Answered` holds this Session's idempotency key: the remembered answer, whatever it was.
   Looked up only once the Session is found, and under its id, so a key replayed from another
   browser finds nothing; the cookie names the Session and a client never learns another's.
3. The Role re-taken from the registry for the record's login (Rule 38); only the Role is
   compared, and it must be Prescriber.
4. The OpenedToken presented equals the Session's current one (Rule 34, else `StaleToken`).
5. The head equals the one the Session opened with (Rule 20, else `Blocked`).
6. The challenge is this Session's, unexpired, its nonce the one presented, its `Patient` and
   `Scenarios` structurally equal to the plan submitted (Rule 43; `OrderScenario` bottoms out
   in strings, decimals, dates and unions, so equality is the digest); a duplicate `Order.Id`
   refuses too (else `ChallengeMismatch`, `ChallengeExpired`).
7. The PIN, through `Credential.verify`, in one act: correct commits; wrong while already
   locked grows the count and the delay and answers `Locked until`, the Session untouched;
   wrong and reaching the limit now answers `PinLimit`, ends the Session with `WrongPinLimit`
   (an ending the client acknowledges as it does `SupersededByLaunch`) and mails that the
   limit was reached (Rule 27); wrong below the limit answers `PinWrong left`.
8. The commit: a `SignedOrderPlan` with `No` one above the patient's count, `Base` the head
   the Session opened with, `By` and `PatientId` from the session record, never from the
   payload, `SignedAt` now, `Patient` and `Scenarios` from the challenged plan; prepended to
   `Records`; the challenge dropped; the Session's `OpenedToken` re-minted and `OpenedWith` set
   to the new head; the answer remembered under the Session and the key;
   `Submitted (plan, token)`.

What the commit re-verifies is Rule 42's list: the Session, the Role, the tokens, the head, the
challenge and the PIN. The platform is read before the challenge (Rule 44), not inside the
commit, as in the model's `dbCommit`; the two-minute challenge keeps that reading recent, and a
Submission on an older challenge is `ChallengeExpired` and signs again, which reads again.

### Ports, stubs, edge

`SessionPort` gains `challenge: sessionId: string -> OrderPlan -> Async<SigningResponse>` and
`submit: sessionId: string -> Submission -> Async<SigningResponse>`; `sessionDisabled` answers
`Refused NoSession` to both. `CompositionRoot.processSigning env cookie cmd` answers
`Refused NoSession` without a cookie and calls the port otherwise; it writes no cookie.
`StubDirectory` gains `prescriber-b` (Stub Prescriber B, Prescriber, the launched patient
active, PIN `1234`).

### Client (direct edits)

- `SessionMachine.fs`: `SessionMsg.EndedByServer of SessionEnding`, from `Open` to `Ended` with
  the acknowledging close, and `SessionMsg.TokenRenewed of OpenedToken` updating the open
  Session's token. `SessionGatePolicy` says why a Session ended at the PIN limit.
- A new pure `SigningMachine.fs`, tested like `SessionMachine.fs`: `Signing = Idle |
  Requesting of OrderPlan | Challenged of challenge * OrderPlan * SigningRefusal option |
  Submitting of challenge * OrderPlan | Signed of SignedOrderPlan`; messages `Sign`,
  `ChallengeAnswered`, `Confirm of pin`, `Cancel`, `SubmitAnswered`, `Dismiss`; effects
  `CallChallenge of OrderPlan`, `CallSubmit of OrderPlan * challenge: string * pin: string`,
  `RenewToken`, `EndSession`. The effects name what the machine knows; the interpreter
  completes each call with the OpenedToken of the open Session and, for a Submission, an
  idempotency key minted once per `Confirm`, so the machine stays pure and its tests cover the
  whole path up to the wire. `Submitting` sends the plan it was challenged over, never the live
  cart (ext 3b, 3c). `PinWrong` and `Locked` keep the dialog with the refusal; `Blocked`,
  `StaleToken`, `ChallengeMismatch`, `ChallengeExpired`, `DataChanged`, `NotPrescriber` and
  `NoSession` return to `Idle` with the refusal told once; `PinLimit` ends the Session.
- `App.fs`: the signing phase in the state, the effect interpreter over `processSigning`, which
  builds `RequestSignChallenge` and `Submission` from the effect, the OpenedToken of the open
  Session and a fresh idempotency key, `AppEnv.ISigning`. `Views/OrderPlan.fs`: a Sign button for a Prescriber with at least one
  order. A new `Views/SignDialog.fs`: an MUI `Dialog` open while challenged or submitting,
  listing each order's prescription text as shown, a PIN field checked locally (four to six
  digits, as the enrolment form), the refusal sentence, Cancel and Sign. On `Signed` the
  version and the signer are told; the plan stays in the cart.
- `Terms`, script-first in `Shared/Scripts/Localization.fsx`, with English and Dutch rows: the
  button, the dialog's title and text, the PIN label, cancel and sign, and one sentence per
  refusal and for the signed plan.

### What stays as is

`processCommand` unbound; `Keys.fs` without `sign`; the OpenedToken opaque on the wire; the
head's orders not loaded into the cart at open (the second half of Rule 19) and Rule 21's
notice on every response, both a follow-up plan; the KnowledgeRuleSet of Rule 44 (Concept 18);
Rule 41's out-of-time Session; the audit (Rule 46); `Integration.fsx`.

## Confidence

High on the server: the ladder is the model's `dbCommit` over the state record and lock plans
605 and 615 built, and the PIN and mail ports are reused unchanged. Medium on the client: the
signing phase is a second machine next to the Session's, and the two meet at the PIN limit and
the token renewal; the tests of the machines pin that down before the dialog is built.

## Steps

Each step is one PR against `master`, script-first for server and Shared code, direct edits for
the client, migration by the maintainer after review.

1. **The record and the head at open, no behaviour change.** `Server/Scripts/Signing.fsx`:
   `OrderPlanHead`, `SignedOrderPlan`, `SessionEnding.WrongPinLimit`, `State.Records`,
   `SessionRecord.OpenedWith` read at `openWith`, `Credential.LockedUntil`, `lockFor` and
   `verify`; tests for the doubling (one, two, four minutes), the reset on a correct entry, the
   correct entry while locked counting nothing, `withPin` clearing both. Client: the gate's
   sentence for the new ending.
2. **The challenge.** `SigningCommand.RequestSignChallenge`, `SigningResponse`,
   `SigningRefusal`, `Hop.challenge`, `SessionPort.challenge`, `IServerApi.processSigning` and
   its arm in the composition root, `sessionDisabled`, the test environments. Tests: no cookie,
   the anonymous Session, a Reader, another patient's data, data changed (the `no-data` stub),
   blocked by a newer head, issued and replaced by a second request, expired.
3. **The commit.** `Submission`, the challenge spent, `State.Answered`, `Hop.commit`,
   `SessionPort.submit`, the `WrongPinLimit` ending and its mail. Tests: the happy path (the
   version appended with `No`, `Base`, `By` from the Session, the token re-minted and the old
   one stale), a replay by key (of a success and of a refusal), a mismatched plan, a spent
   challenge, three wrong PINs ending the Session and locking, a correct PIN while locked
   refused without counting, a wrong PIN while locked doubling, the Role withdrawn (the stub
   registry answering Reader), blocked by a newer head; and that a refused Submission spends
   neither token nor challenge, and that a blocked one never reads the credential.
4. **`prescriber-b`.** The stub directory and the seeded credentials; the identity table in
   `DEVELOPMENT.md`; one test: B signs, A's commit is `Blocked` with B's head.
5. **The client machines.** `SigningMachine.fs` with tests, `SessionMsg.EndedByServer` and
   `TokenRenewed` with tests, the `Terms` cases and rows.
6. **The client wiring and the dialog.** `App.fs`, `AppEnv.ISigning`, the Sign button,
   `Views/SignDialog.fs`, the sentences told once.
7. **Docs.** uc-03 gets an as-built note; uc-01's "Left out" and plan 409's "Still open" are
   updated; `DEVELOPMENT.md` gains "Signing an order plan" (two browsers for Rule 20, the PIN
   lock); this plan gets its as-built table and deviations.

## Acceptance

- Against `GENPRES_PROD=0 dotnet run`: launch `prescriber`, prescribe two orders, Sign: the
  dialog lists both as shown; a wrong PIN says two tries are left; `1234` signs version 1 by
  Stub Prescriber; the Network tab shows `RequestSignChallenge` answering `ChallengeIssued` and
  `Submit` answering `Submitted`; signing again gives version 2.
- `prescriber-b` in a second browser, launched before A's next signature: B signs; A's next
  Sign is refused as blocked by Stub Prescriber B, with the time.
- Three wrong PINs: the gate says the Session ended at the PIN limit; after a relaunch a
  correct PIN within a minute is refused as locked until a time; after that it signs.
- `reader` and a URL patient without a launch see no Sign button; `processSigning` without a
  cookie answers `NoSession`. With `GENPRES_PROD=1` every signing call answers `NoSession`.
- A wrong PIN leaves the challenge standing: the next entry signs on the same challenge. A
  blocked Submission leaves `WrongCount` unchanged. The version committed is the plan
  challenged, not the cart at the time of the PIN.
- `dotnet run ServerTests`, the Fable compile and Fantomas stay green after every step.

## As built

Every step landed as one PR from a fork branch against `master`, script-first for the server
and Shared code (`Server/Scripts/Signing.fsx`, rewritten for each step, and
`Shared/Scripts/Localization.fsx`), reviewed and migrated by the maintainer, client edits
direct. Every review round changed the design below; the deviations follow the table.

| Step | PR | Landed |
|------|----|--------|
| plan | [#623](https://github.com/informedica/GenPRES/pull/623) | this document; review: the Submission carries the OpenedToken, the remembered answers are keyed by Session, the challenge lives two minutes, the machine's effects carry no token |
| 1 | [#624](https://github.com/informedica/GenPRES/pull/624) | `OrderPlanHead`, `SignedOrderPlan`, `SessionEnding.WrongPinLimit`, the credential's lock, `Records`, `OpenedWith` at open; no change in behaviour; review: the lock capped at a day |
| 2 | [#626](https://github.com/informedica/GenPRES/pull/626) | the challenge ladder, `SigningRefusal`, `DataNotice`, `SessionPort.challenge`, `processSigning`; review: a notice drops the standing challenge |
| 3 | [#627](https://github.com/informedica/GenPRES/pull/627) | `Submission`, `Submitted`, `Hop.commit` in the model's order with the PIN last, the ending and its mail; review: a plan naming an order twice refused, the mail best effort |
| 4 | [#628](https://github.com/informedica/GenPRES/pull/628) | `prescriber-b` |
| 5 | [#629](https://github.com/informedica/GenPRES/pull/629) | `SigningMachine`, `SigningPolicy`, `EndedByServer` and `TokenRenewed`, nineteen `Terms`; review: `Unsent` and the kept key |
| 6 | [#630](https://github.com/informedica/GenPRES/pull/630) | the wiring, the Sign button, `Views/SignDialog.fs`, a server fix found in the browser; review: answers land only on their request, a twentieth term |
| 7 | this PR | uc-03 as-built note, uc-01, uc-02 and plan 409 updated, `DEVELOPMENT.md` signing walkthrough, this table |

### Deviations from the text above

- **Rule 44 is a notice, not a refusal.** Decided in review of step 2: a `DataChanged` refusal
  would have left a Session unsignable for good, since the Session keeps the data it opened
  with. The challenge answers `DataNotice { Data; Token }` with the data as it stands (`None`
  when unreadable), the User proceeds by returning the token with the next request, and the
  challenge records `Verified`. A wrong, spent, expired or unfitting notice token is a fresh
  notice. The KnowledgeRuleSet is still not built.
- **No equality between the plan's data and the Session's (Rule 33).** The challenge compared
  them and refused `NoPatient`; in the demo the User enters age and weight because the stub
  platform has none, so nothing could be signed (found in the browser walk of step 6). Rule 33
  is the Patient's identity, which is the Session's; the data the User saw, entered or read, is
  what the signed version records. `NoPatient` is only a Session without a Patient.
- **The duplicate `Order.Id` check** was left out of step 3 for want of a fixture, then built on
  review, at the challenge and at the commit, with a scenario the tests build by reflection.
- **The lock is capped** at 24 hours (`Credential.lockMax`, review on #624): the model's delay
  only grows because it decays with time, and the decay is not built.
- **A notice drops the Session's standing challenge** (review on #626): it was over the data
  before the change.
- **The PIN-limit mail is best effort** (review on #627): the ending and the lock land whatever
  the MailService does.
- **The machine owns the request id and the key.** `Sign` and `Confirm` carry ids the App
  mints; `Unsent` keeps the key of a Submission whose answer was lost, and the next `Confirm`
  retries under it (Rule 45, review on #629); every answer names the request id or the key it
  answers and lands only there, so a signature of an earlier Session never touches a later one
  (review on #630). `CallSubmit` carries the key; the interpreter adds only the OpenedToken.
- **The cart follows a notice's reading.** `Accept` with a reading sets the patient, so the
  plan the User signs is over the data they were shown.
- **Twenty terms, not about twelve**: one per refusal, the notice in both cases, the signed
  sentence, and one for a transport failure at a signature.
