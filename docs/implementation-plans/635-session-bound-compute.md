# Implementation plan for issue 635

## Problem description

Plans [605](605-launch-with-server-stubs.md), [615](615-enrolment-with-server-stubs.md) and
[622](622-signing-with-server-stubs.md) run the launch, the enrolment and the signing against
server-side stubs. Computing does not know about the Session: `IServerApi.processCommand` ignores
the session cookie the composition root already holds, no computing request carries the
OpenedToken, and no reply says whether the record moved on. Four rules of the
[integration design](../scenarios/integration/GenPRES-MainEHR-Integration-V8.md) are therefore
not built, or half built:

- **Rule 19, second half.** A Session opens with the head of the record (`OpenedWith` holds its
  id since plan 622), but `SessionOpened` carries no orders, so the cart starts empty.
- **Rules 21 and 22.** A User whose Patient's record moved on learns it at the signature only,
  as Rule 20's refusal (`SigningRefusal.Blocked`), and can do nothing about it but relaunch.
- **Rules 18 to 20, opening the newest version** ([uc-04](../scenarios/integration/uc-04-two-users.md)
  step 4, `OpenOrderPlan` in the model): does not exist.
- **Rules 9 and 11.** Nothing marks a Session seen, and a Session the server ended is told at
  `GetSession` only, not at "the next request".

Issue [#635](https://github.com/informedica/GenPRES/issues/635) asks for Compute bound to the
Session so that UC-4, two users on one patient, runs end to end against the stubs.

## Approaches considered

1. **A second API member for computing inside a Session**, `processCommand` untouched.
   Rejected: the server decides from the cookie what a request is, as it does for signing; two
   members would make the client decide, and UC-7's direct open is the same call without a
   cookie.
2. **Refuse to compute on an ended Session**, as signing refuses. Rejected (decision taken with
   the issue): decision support is open to anyone (UC-7), and an anonymous Session would have to
   be told apart from an ended one at every call. The reply tells the ending and computes anyway.
3. **The DPoP proof of uc-01 step 7 in the same change.** Rejected: it has no rule number and no
   model in `Integration.fsx`. The request envelope built here carries the token now and is
   where the proof goes later.
4. **Compare the head with `OpenedWith` and ignore the request's token.** Rejected: Rule 21
   compares against the version the request's token names, and the model filters an invalid
   token out (`Token.verifyOpened`). A token that is not the Session's yields no notice, never a
   refusal: Compute gates nothing.
5. **Compute bound to the Session through the cookie, a request envelope with the token, a reply
   envelope with the notice, the head on `SessionOpened`, and `OpenVersion` as a session
   command.** Chosen.

## Chosen approach

Approach 5, in the shape of plans 605, 615 and 622: every transition pure over `Hop.State` so it
runs under Expecto and in FSI; the port members added to `SessionPort` and refused by
`sessionDisabled`; the client machine extended, its effects interpreted in `App.fs`.

### Wire (`Shared/Types.fs`, `Shared/Api.fs`)

```fsharp
type SessionOpened = { ...; Head: SignedOrderPlan option }        // Rule 19: what it opened with

type RecordNotice =
    | NewerVersion of OrderPlanHead                                // Rules 21, 22: whose, when
    | Ended of SessionEnding                                       // Rule 11: the next request

type Request = { Opened: OpenedToken option; Command: Command }   // Rule 34: the token rides along
type Reply = { Response: Response; Notice: RecordNotice option }

type SessionCommand = ... | OpenVersion of id: string             // Rules 18 to 20, UC-4 step 4

processCommand: Request -> Async<Result<Reply, string[]>>
```

`Request.Opened` is `None` where there is none to send: an anonymous Session, a URL patient
without a launch, or a client acting before its first token arrived (the model's
`SessionRequest`). `Reply.Notice` is `None` on every reply that has nothing to say. The
`Command.toString` log line names the notice kind, never the token. `OpenVersion` carries the id
of the version the notice named (`OrderPlanHead.Id`), as the model's `OpenOrderPlan of
OrderPlanId` does, so a version signed between the notice and the button is never opened
unasked. It answers the existing `SessionResp of SessionOpened option`: `Some` with the token and
the version opened, `None` where there is no Session, no User or no Patient (Rule 13).

### The server (`Hop`, pure over `State`)

`SessionRecord` gains `Seen: DateTime` (Rule 9), set at open. `Hop.touch now sid state` sets it to
`now` when the Session is open, and every port member that takes the session cookie's id applies
it before its own ladder: `find` (`GetSession`), `challenge`, `submit`, `openVersion` and `seen`;
`close` excepted, as the model's `updateServerFromDatabaseRequest` excepts `CloseSession`. The
boundary is the session cookie: `present`, `callback` and `supplyPin` run before a Session exists
(the launch and enrolment cookies), so a Session they open starts with `Seen` at open and is
touched from its first cookie-bound request on. So every request in a Session but the closing
one refreshes the idle clock, whichever API member carries it. Nothing acts on `Seen` yet
(Rule 10's lifetimes stay out).

`Hop.seen now sid opened state : State * RecordNotice option`, the ladder of uc-03 step 1 and the
model's `updateServerFromDatabaseRequest`:

1. No Session under `sid`: an ending recorded for it answers `Ended`; none, `None`.
2. The Session is open: touched. The notice is `NewerVersion` when `opened` is the Session's own
   token and `blockedBy record patientId state` names a head; in every other case `None` (an
   anonymous Session, no Patient, no head, a token that is not the Session's).

`Hop.openVersion now newId sid id state : State * SessionResponse`:

1. No Session, anonymous, or no Patient: `SessionResp None`.
2. The record holds no version `id` for the Session's Patient (a stale button, or a restart):
   nothing opens, the token stands, the answer is the Session as it is; the client's next
   request tells it what the head is (Rule 21).
3. Otherwise version `id` becomes `OpenedWith`: when it differs from the current one the
   OpenedToken is re-minted (`opened-<newId>`) and the standing challenge and notice of this
   Session are dropped (a challenge over the old baseline must not be answerable); when it is
   the same, the token stands. The answer is the Session's `SessionOpened` with that version.
   Any version may be opened (Rule 18); one that is no longer the head leaves Submission
   blocked (Rule 20) and the next reply says so again (Rule 21), so a version signed between
   the notice and the button is never taken up silently.

`Hop.openWith` fills `SessionOpened.Head` from `headOf` at open, so `GetSession` returns it at
resume too. `Hop.commit` sets `Head` to the version it appended, so a resume after a signature
opens on it.

### Ports, edge

`SessionPort` gains `seen: string -> OpenedToken option -> Async<RecordNotice option>` and
`openVersion: string -> string -> Async<SessionResponse>`; `sessionDisabled` answers `None` and
`SessionResp None`. `CompositionRoot.processCommand` reads the cookie as `processSigning` does:
without one it computes as today with `Notice = None`; with one it calls `seen` first, then
computes, and answers both. An exception in the computation still answers `Error` and loses the
notice; the next request repeats it (Rule 21 is stateless). `processSession` gains the
`OpenVersion` arm.

### Client (direct edits)

- `createApiMsg` sends `{ Opened = tokenOf state.Session; Command = cmd }`: one site, eleven
  uses, the token read from `Session.Open` as `interpretSigningEffect` reads it.
- `processApiMsg` hands `Reply.Notice` to the session machine: `NewerVersion head` as
  `SessionMsg.RecordMovedOn head`, `Ended e` as the existing `SessionMsg.EndedByServer e`.
- `SessionMachine`: `Session.Open` keeps `MovedOn: OrderPlanHead option` next to the
  `SessionOpened`; `RecordMovedOn` sets it and tells it once per head id (`TellMovedOn` effect,
  a snackbar); `OpenVersion` is an effect `CallOpenVersion of id`, sent with the id `MovedOn`
  holds, its answer `Reopened opened` replaces the `SessionOpened`, clears `MovedOn` and emits
  `LoadCart of SignedOrderPlan option`.
  The same `LoadCart` is emitted when the Session enters `Open` from a launch or a resume.
- `App.fs` interprets `LoadCart`: the cart's `Scenarios` become the head's, `Patient` stays the
  Session's PatientContext, and `FilterOrderPlan` recomputes the rest. No head, an empty cart.
- `Views/OrderPlan.fs`: when `MovedOn` is set, a bar above the plan with the sentence and a
  button that opens the newest version; `Views/SignDialog.fs` offers the same button on a
  `Blocked` refusal. `SigningMachine` is untouched: a `Blocked` refusal keeps closing the dialog.
- Terms: ``Session Newer Version`` ("A newer version was signed by {0} at {1}."),
  ``Session Open Newest`` ("Open the newest version"), ``Session Version Opened``
  ("Version {0} by {1} is now open."), EN and NL, rows for the workbook.

### What stays as is

The challenge and commit ladders of plan 622, `OpenedWith` as an id, `SigningRefusal.Blocked`
(the notice does not replace the guard: Rule 20 stays the only one), the endings acknowledged
through `CloseSession`, the OpenedToken opaque, UC-7 computing without a Session or a token.

## Confidence

High for the server: `headOf` and `blockedBy` already compute Rule 21's answer, the cookie is
in scope at the composition root, and every transition has the pattern of `Hop.challenge`.
Medium for the cart at open: the signed version's `OrderScenario[]` is loaded as it was signed and
recomputed at the next request; a scenario that no longer fits the current rules is shown, not
silently dropped (Concept 18 is not built), which is the design's intent but has not been seen
in the browser yet. The notice told once per head is a client choice: the wire says it on every
reply, as Rule 21 reads.

## Steps

Each step is one PR against `master`, script-first for server and Shared code
(`Server/Scripts/Compute.fsx`, `Shared/Scripts/Localization.fsx`), direct edits for the client,
migration by the maintainer after review.

1. **The envelope and the notice, no visible change.** `Request`, `Reply`, `RecordNotice`,
   `SessionRecord.Seen`, `Hop.seen`, `SessionPort.seen`, `sessionDisabled`, `processCommand`
   reading the cookie; `Hop.touch` applied by every port member but `close`; the client sending
   the token and logging the notice. Tests: no cookie, an unknown Session with and without an
   ending, an anonymous Session, the Session's own token with and without a newer head, a
   foreign token with a newer head (no notice), `Seen` advanced by a compute, a `GetSession`, a
   challenge and a Submission and not by `CloseSession`, and `processCommand` end to end
   through the test environment.
2. **The head into the cart.** `SessionOpened.Head` filled at open and at commit; the client's
   `LoadCart` at open and at resume, `FilterOrderPlan` after it. Tests: open on a head, open
   from nothing, resume after a signature; the machine's effect.
3. **Open the version the notice named.** `SessionCommand.OpenVersion`, `Hop.openVersion`, the
   port and the `processSession` arm; the machine's `CallOpenVersion` and `Reopened`. Tests: no
   Session, anonymous, no Patient, an id the record does not hold (nothing opened, token kept),
   the version already open (token kept), the head (token re-minted, the old one stale at the
   next challenge, the challenge and notice dropped), then a commit over the new baseline
   succeeding; a version that is no longer the head (opened, the commit still `Blocked`, the
   next `seen` a notice again).
4. **The notice in the client.** `RecordMovedOn` told once per head, the bar and the button on
   the order plan, the button on the `Blocked` refusal, `Ended` to the gate, the three terms.
5. **Docs.** uc-04 gets an as-built note; uc-01's and uc-03's not-built lines and plan 409's
   "Still open" are updated; `DEVELOPMENT.md`'s "Two browsers on one patient" continues with
   the notice and the newest version; this plan gets its as-built table and deviations.

## Acceptance

- Against `GENPRES_PROD=0 dotnet run`: launch `prescriber`, sign a plan; launch `prescriber`
  again in another tab: the cart holds the signed orders; reload: still there.
- Two browsers: `prescriber` (A) and `prescriber-b` (B) on the same patient. B signs. A's next
  prescribing action shows "A newer version was signed by Stub Prescriber B at hh:mm" once, with
  "Open the newest version"; A keeps prescribing meanwhile (Rule 22); pressing the button loads
  the version the notice named, B's orders, into A's cart and says version N by Stub Prescriber
  B is open; A signs version N+1 with B's as its base. Without pressing it, A's Sign is still
  refused as blocked. If B signs again between the notice and the button, A gets B's first
  version, the notice again, and a blocked Sign until the newer one is opened too.
- Launch `prescriber` in tab 1, then again in tab 2: tab 1's next prescribing action shows the
  gate with the superseded ending, as `GetSession` does today.
- A URL patient without a launch and `reader` compute as today; the Network tab shows
  `Request { Opened; Command }` answering `Reply { Response; Notice }` with `Notice` empty.
  With `GENPRES_PROD=1` every reply's notice is empty and `OpenVersion` answers no Session.
- `dotnet run ServerTests`, the Fable compile and Fantomas stay green after every step.

## As built

Script-first for the server and Shared code (`Server/Scripts/Compute.fsx`, rewritten per step,
and `Shared/Scripts/Localization.fsx`), the client edited directly, every step one PR against
`master`, migrated after review.

| Step | PR | Landed |
|---|---|---|
| plan | #636 | the doc; review: the touch applied by every member that takes the session cookie's id, `OpenVersion of id` in place of an `OpenNewest` |
| 1 | #637 | `Request { Opened; Command }`, `Reply { Response; Notice }`, `RecordNotice`, `SessionRecord.Seen`, `Hop.touch` and `Hop.seen`, `processCommand` reading the cookie, the client sending the token from its one call site |
| 2 | #639 | `SessionOpened.Head` filled at open and at the commit, `SessionEffect.LoadCart`, the cart loaded at a launch, a resume and an enrolment; review: the cart built over the patient as `UpdatePatient` left it |
| 3 | #641 | `SessionCommand.OpenVersion`, `Hop.openVersion`, the port and the arm, the machine's `OpenVersion` and `Reopened`; review: the answer correlated by the token the request started from |
| 4 | #642 | `MovedOn` next to the Session, the bar and its button, `TellVersionOpened`, an `Ended` notice to the gate, three terms; review: the notice correlated by token, ordered by version number, spent only by a version at least as new |
| 5 | this PR | the docs |

### Deviations from the text above

- **The touch is every port member's, not `processCommand`'s.** `Hop.touch` is applied by every
  member that takes the session cookie's id (`find`, `challenge`, `submit`, `openVersion`,
  `seen`), `close` excepted; `present`, `callback` and `supplyPin` run before a Session exists,
  and a Session they open starts with `Seen` set.
- **`OpenVersion of id`, not `OpenNewest`.** The button opens the version the notice named,
  the model's `OpenOrderPlan of OrderPlanId`; any version may be opened (Rule 18), one that is
  no longer the head stays blocked (Rule 20) and is told again (Rule 21). An id the record does
  not hold opens nothing and keeps the token.
- **The port answers `SessionOpened option`** for `openVersion`, as `find` answers a
  `SessionLookup`: the adapter does not see `Shared.Api`; the composition root wraps it in
  `SessionResp`.
- **`LoadCart of SignedOrderPlan`**, emitted only when the Session has a patient and a head, and
  built into the cart in `update` over the patient as `UpdatePatient` left it, normal values
  applied, through `FilterOrderPlan`.
- **The notice lives in `App.State.MovedOn`**, not inside `Session.Open`, which has 54 match
  sites. `SessionMachine.MovedOn.receive` and `opened` are the pure helpers: ordered by version
  number (Rule 20's order), news once per version, spent only by a version at least as new.
- **Every answer is correlated.** `Reopened` and a reply's notice carry the OpenedToken the
  request started from and land only on the open Session that still holds it; a Session closed,
  replaced or re-minted meanwhile drops them.
- **The Blocked refusal sets the bar**; the dialog closes as before, told once, and the offer to
  open the newest version is in one place.
- **No `SetPatient` at a reopen.** The Session's patient is unchanged; which patient data a
  Session opens on when the platform has none is
  [#640](https://github.com/informedica/GenPRES/issues/640).
- **The Ended notice** reaches the gate through the existing `EndedByServer`; a second launch of
  the same identity from another browser profile shows it at the older browser's next request.
  In the same profile the second launch replaces the session cookie, so the older tab's
  requests carry the new cookie and no ending is told there.

