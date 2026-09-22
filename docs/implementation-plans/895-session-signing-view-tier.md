# Implementation plan for issue 895

## Problem description

Plan [706](706-client-view-tier.md) named the client's view tier and gave the two order lanes
a lane record each, one field per tier, private, built by constructors, with a pure `view` to
a view DU whose cases are the states a page can be in. The pages and the policies read the view
through `AppEnv`; the request under way, its id, and what was sent stay in the lane.

`SessionMachine.Session` and `SigningMachine.Signing` are not on that pattern
([#895](https://github.com/informedica/GenPRES/issues/895)). They reach the pages raw
(`ISession.Session`, `ISigning.Signing` in `src/Informedica.GenPRES.Client/AppEnv.fs`) with the
`Launch`, the `PublicKey`, the attempt count, the request id, the challenge id and the
idempotency key in their cases, matched in `Components/TitleBar.fs`, `Views/SessionGate.fs`,
`Views/SignDialog.fs`, `SessionGatePolicy.fs`, `SigningPolicy.fs` and `UnsignedWorkPolicy.fs`.

What the readers use, checked arm by arm: the gate shows `Launching`'s attempt and
`Unreachable`'s count, offers a retry when `Refused` carries one (its `Launch * PublicKey` is
read for presence only), and renders the refusal, the ending, the enrolment's name and mail
hint and the PIN refusal; the title bar reads the open Session's user and role, and whether a
close is under way; `canSign` reads the open Session's role and whether it has a patient;
the sign dialog reads the plan, the data notice, the signing refusal and whether a Submission
is under way; the leave guard and the plan's work read whether a signature is under way at all.
Nothing outside the two machines and the interpreters in `App.fs` reads a `Launch`, a key, a
request id or a challenge id. Two payloads turn out to be derivable: `Unreachable`'s count is
always `Session.maxAttempts` (the case is built in one place, reached only at the ceiling), and
`Unsent` shows exactly as `Challenged` without a refusal (the dialog is open, the plan listed,
the field enabled, no refusal, in both).

And the state beside them that `App.update` stitches. The order lanes are one field each in
`App.State`, because the machine owns the whole lane: the dialog's selection and the pending
step live inside `OrderPlanState`, and nothing of the plan is stepped in `App.update`. The
Session and the signing are not: `MovedOn` (the newest version told while the Session is on an
older one) sits beside `Session` and is cleared by hand when the Session leaves `Open`, told
by hand from the reply's notice and from a refused signature, and spent by hand when a version
is opened; `WorkAtSign` (the plan's work when the signature under way was asked for) sits
beside `Signing` and is set by hand on `Sign` and read by hand on `TellSigned`; `PlanWork`
(what the plan holds beside the version last opened or signed) is stepped by hand on every
plan message. Three fields, five places in `App.update`, and a stale-request guard for the
notice (`processApiMsg`) that peeks into the Session, where the machine already guards
`Reopened` the same way.

## Approaches considered

1. **Keep the DUs, hide them behind the policies.** `SessionGatePolicy` and `SigningPolicy`
   already read the DU for the pages; make every page go through a policy and stop exposing
   the DU. Rejected: the policies would still match transport payloads, the title bar and
   the sign dialog would gain policy functions for what is a match on a case, and the request
   under way would stay spread over four Session cases and three signing cases, re-derived by
   every reader that asks whether one is under way (`tokenOf`, `isGated`, `dialogOpen`,
   `askedOver`, `hasUnsignedWork`).
2. **The two-stage split of the order lanes**: a domain DU with a `step` that knows no
   request, intents, and a request stage that turns them into calls. Rejected for these two
   machines: nearly every transition of the Session and the signing is request-driven (an
   answer landing, a request going out), so the domain stage would be a thin relay and the
   two message vocabularies of the order lanes (`OrderPlanCartMsg` beside `OrderPlanMsg`)
   would be duplicated for little.
3. **The lane record with one `transition`** (chosen). A domain DU without the transport
   payloads, an `InFlight` field for the one request under way, the one transport value that
   outlives a request as its own field (the Launch to present again; the key of a Submission
   whose answer was lost), a private record built by constructors, a pure `view` to a view DU,
   and one `transition` over `msg, state.Phase, state.InFlight`, as today's over `msg, state`.
   The pages and the policies read the view; the same request-id guards, on the fields.

Inside 3, two shapes were weighed and settled with the user:

- The retry on a refusal: `SessionView.Refused of LaunchRefusal * retryable: bool` against two
  cases, `Refused of LaunchRefusal` and `Retryable of LaunchRefusal`. Two cases: no boolean in
  a DU, and the states a page can be in are two; the cost is a second arm in the gate.
- The lost answer: the issue listed `Unsent` under `InFlight`. Nothing is in flight in
  `Unsent`; what it holds is the key the next `Confirm` must reuse. So the key is its own
  field, `Unsent: string option`, and the view shows the state as `Challenged` without a
  refusal, which is what the dialog shows today.

## Chosen approach

Approach 3, both lanes. The view DUs are today's DUs minus the transport payloads, checked
case by case above; the records and their `transition` keep every guard the machines have.

### The signing lane

```fsharp
// SigningMachine.fs

/// The signing as the clinical model has it: no request here. Idle; the data as it stands,
/// to accept or to cancel; the dialog asking the PIN over the plan the challenge was issued
/// over, with the last refusal if any.
[<RequireQualifiedAccess>]
type SigningPhase =
    | Idle
    | Noticed of OrderPlan * DataNotice
    | Challenged of challenge: string * OrderPlan * SigningRefusal option

/// The one request under way: the challenge asked over the plan, under its request id, with
/// the notice token when a notice was accepted; the Submission under its key.
[<RequireQualifiedAccess>]
type SigningRequest =
    | Challenge of OrderPlan * notice: string option * request: string
    | Submission of key: string

/// The phase, the one request under way, and the key of a Submission whose answer was lost, so
/// that the next Confirm goes out under it and the server answers what it already did. Built
/// through the constructors below only, which admit the six combinations that occur.
type SigningState =
    private
        {
            Phase: SigningPhase
            InFlight: SigningRequest option
            Unsent: string option
        }

/// The signing as the dialog shows it: closed; closed while the challenge is asked; the notice
/// to accept; the PIN asked over the plan, with the last refusal; the Submission under way,
/// the plan listed, the field disabled. A lost answer shows as the PIN asked again, no refusal.
[<RequireQualifiedAccess>]
type SigningView =
    | Idle
    | Requesting
    | Noticed of OrderPlan * DataNotice
    | Challenged of OrderPlan * SigningRefusal option
    | Submitting of OrderPlan
```

The six states today, as triples `(Phase, InFlight, Unsent)`:

| Today | Record |
|---|---|
| `Idle` | `Idle, None, None` |
| `Requesting(plan, notice, request)` | `Idle, Some(Challenge(plan, notice, request)), None` |
| `Noticed(plan, notice)` | `Noticed(plan, notice), None, None` |
| `Challenged(challenge, plan, refusal)` | `Challenged(challenge, plan, refusal), None, None` |
| `Submitting(challenge, plan, key)` | `Challenged(challenge, plan, None), Some(Submission key), None` |
| `Unsent(challenge, plan, key)` | `Challenged(challenge, plan, None), None, Some key` |

Constructors in `module SigningState`, one per row: `idle`, `requesting plan notice request`,
`noticed plan notice`, `challenged challenge plan refusal`, `submitting challenge plan key`,
`unsent challenge plan key`. `view` is total in five arms, the phase winning whenever it holds
the payload:

```fsharp
let view (state: SigningState) : SigningView =
    match state.Phase, state.InFlight with
    | SigningPhase.Idle, Some(SigningRequest.Challenge _) -> SigningView.Requesting
    | SigningPhase.Idle, _ -> SigningView.Idle
    | SigningPhase.Noticed(plan, notice), _ -> SigningView.Noticed(plan, notice)
    | SigningPhase.Challenged(_, plan, _), Some(SigningRequest.Submission _) -> SigningView.Submitting plan
    | SigningPhase.Challenged(_, plan, refusal), _ -> SigningView.Challenged(plan, refusal)
```

`transition` keeps every arm of today's, on the triple. Four arms are easy to get wrong, and
each is pinned by a test that exists: `Sign` starts a challenge only from `Idle` with nothing
in flight (a second `Sign` while the challenge is asked is dropped); `Confirm` sends only with
nothing in flight (not twice), and the `Challenged` and `Unsent` arms merge as
`state.Unsent |> Option.defaultValue key`, the kept key winning over the fresh one; `Accept`
moves the phase to `Idle` while the challenge is asked again, since the notice dialog closes
meanwhile today; a lost answer (`SubmitAnswered(_, Error _)`) rebuilds the phase with no
refusal, never `{ state with … }`, else a wrong-PIN refusal of the round before would show
again. A `SubmitAnswered` under the kept key while nothing is in flight is dropped, as today.

### The Session lane

```fsharp
// SessionMachine.fs

/// The Session as the client knows it, no request here: none; open; refused, ended, or the
/// server unreachable; enrolling, with the last refusal of the form if any; or the enrolment
/// failed.
[<RequireQualifiedAccess>]
type SessionPhase =
    | Anonymous
    | Open of SessionOpened
    | Refused of LaunchRefusal
    | Unreachable
    | Ended of SessionEnding
    | Enrolling of EnrolmentPending * refusal: PinRefusal option
    | EnrolmentFailed of PinRefusal

/// The one request under way: the presentation, and which attempt it is; GetSession after a
/// reload or the IdentityProvider's return; CloseSession; SupplyPin.
[<RequireQualifiedAccess>]
type SessionRequest =
    | Presenting of attempt: int
    | Resuming
    | Closing
    | SupplyingPin

/// The phase, the one request under way, and the Launch this page presents with its key, kept
/// while presenting it again is meaningful: during a presentation, after the server was
/// unreachable, after a refusal worth retrying. Built through the constructors below only,
/// which admit the twelve combinations that occur.
type SessionState =
    private
        {
            Phase: SessionPhase
            InFlight: SessionRequest option
            Presentation: (Launch * PublicKey) option
        }

/// The Session as the pages show it: today's cases without the Launch, the key and the count;
/// a refusal is `Retryable` when the same Launch can be presented again.
[<RequireQualifiedAccess>]
type SessionView =
    | Anonymous
    | Launching of attempt: int
    | Resuming
    | Open of SessionOpened
    | Closing of SessionOpened
    | Refused of LaunchRefusal
    | Retryable of LaunchRefusal
    | Unreachable
    | Ended of SessionEnding
    | Enrolling of EnrolmentPending * refusal: PinRefusal option
    | SupplyingPin of EnrolmentPending
    | EnrolmentFailed of PinRefusal
```

The twelve states today, as triples `(Phase, InFlight, Presentation)`:

| Today | Record |
|---|---|
| `Anonymous` | `Anonymous, None, None` |
| `Launching(launch, key, n)` | `Anonymous, Some(Presenting n), Some(launch, key)` |
| `Resuming` | `Anonymous, Some Resuming, None` |
| `Open session` | `Open session, None, None` |
| `Closing session` | `Open session, Some Closing, None` |
| `Refused(refusal, None)` | `Refused refusal, None, None` |
| `Refused(refusal, Some(launch, key))` | `Refused refusal, None, Some(launch, key)` |
| `Unreachable(launch, key, _)` | `Unreachable, None, Some(launch, key)` |
| `Ended ending` | `Ended ending, None, None` |
| `Enrolling(pending, refusal)` | `Enrolling(pending, refusal), None, None` |
| `SupplyingPin pending` | `Enrolling(pending, None), Some SupplyingPin, None` |
| `EnrolmentFailed refusal` | `EnrolmentFailed refusal, None, None` |

A presentation runs over `Anonymous`: a `Present` from an open Session drops the Session at
once today, and `tokenOf` answers none while launching. Constructors in `module SessionState`,
one per row: `anonymous`, `launching launch key attempt`, `resuming`, `opened session`,
`closing session`, `refused refusal`, `retryable refusal launch key`, `unreachable launch key`,
`ended ending`, `enrolling pending refusal`, `supplyingPin pending`, `enrolmentFailed refusal`.
Two accessors replace `tokenOf` in `App.fs`: `session`, the `SessionOpened` held when the
Session is open and not closing, and `token`, its `OpenedToken`; `maxAttempts` moves here.
Today's `Session.opened` (the state and the effects of a Session that just opened) becomes
`onOpened`, and the predicate `Session.retryable`, which nothing outside the file calls,
becomes a private `worthRetrying`, so that the constructors can have the plain names.

`view` is total in twelve arms: the request wins when it can render on its own (`Presenting`,
`Resuming`); the phase wins when the request needs the phase's payload (`Closing` the
`SessionOpened`, `SupplyingPin` the `EnrolmentPending`); a stray `Closing` or `SupplyingPin` on
any other phase shows as that phase; the presentation is read under `Refused` only.

```fsharp
let view (state: SessionState) : SessionView =
    match state.Phase, state.InFlight, state.Presentation with
    | _, Some(SessionRequest.Presenting attempt), _ -> SessionView.Launching attempt
    | _, Some SessionRequest.Resuming, _ -> SessionView.Resuming
    | SessionPhase.Open opened, Some SessionRequest.Closing, _ -> SessionView.Closing opened
    | SessionPhase.Enrolling(pending, _), Some SessionRequest.SupplyingPin, _ -> SessionView.SupplyingPin pending
    | SessionPhase.Anonymous, _, _ -> SessionView.Anonymous
    | SessionPhase.Open opened, _, _ -> SessionView.Open opened
    | SessionPhase.Refused refusal, _, Some _ -> SessionView.Retryable refusal
    | SessionPhase.Refused refusal, _, None -> SessionView.Refused refusal
    | SessionPhase.Unreachable, _, _ -> SessionView.Unreachable
    | SessionPhase.Ended ending, _, _ -> SessionView.Ended ending
    | SessionPhase.Enrolling(pending, refusal), _, _ -> SessionView.Enrolling(pending, refusal)
    | SessionPhase.EnrolmentFailed refusal, _, _ -> SessionView.EnrolmentFailed refusal
```

`transition` keeps every arm of today's forty, on the triple. The record can hold what the DU
cannot: a stale presentation. Today `Refused(refusal, None)` cannot carry a Launch; on the
record every exit from a presenting state must clear `Presentation`, or the gate offers a retry
the server never gave. Two rules make that mechanical, and both are deliberately stricter than
the order lanes, which update `InFlight` in place: every arm matches on `msg, state.Phase,
state.InFlight`, so that the request under way is always named, and every new state is built
through a constructor, never `{ state with … }`; the two in-place updates that are safe are
the attempt count going up and the token renewed. The arms that need care, each pinned by a
test that exists: the same-Launch `Present` is a no-op only while `Presenting` is in flight
(the presentation is kept in `Unreachable` and `Retryable` too, where a `Present` of the same
Launch starts fresh); `Resume`, `SupplyPin`, `Close`, `EndedByServer`, `TokenRenewed` and
`OpenVersion` act with nothing in flight only (not twice; a close in flight drops them);
`RefusedAtCallback` rebuilds all three fields; a PIN that never reached the server rebuilds
`Enrolling(pending, None)`; `Closed`, `Resumed` and `PinAnswered` land only on their request.
`OpenVersion` and `Reopened` stay as they are, with the guard on the token and no `InFlight`
case: out of scope, in Left open.

The gate's `Refused` arm orders "no role: continue without a launch" before "a retry offered:
retry". On the view it becomes two arms, `Retryable refusal` offering the retry and `Refused
refusal` offering to continue without a launch when the role is missing. That is right only
because the one refusal worth retrying is a missing browser identity (`worthRetrying`), so
`Retryable NoRole` cannot occur; the arm's comment says so, and a gate test pins both.

### The pages and the policies

`ISession.Session` is a `SessionView`, `ISigning.Signing` a `SigningView`; the commands stay.
`SessionGatePolicy.isGated` and `gateFor` take the view: `Launching attempt`, `Unreachable`
filling `SessionState.maxAttempts`, the two refusal arms. `SigningPolicy.canSign` takes the
view (its one arm, `Open opened`); `dialogOpen` too (`Noticed`, `Challenged`, `Submitting`
open; `Idle`, `Requesting` closed). `UnsignedWorkPolicy` reads the view (`Idle` against the
rest). The title bar matches `Open opened` and `Closing opened`; the gate view's enrolment
check matches `Enrolling _` and `SupplyingPin _`; the sign dialog reads busy from
`Submitting _`, the refusal from `Challenged(_, refusal)`, the notice from `Noticed(_, notice)`
and the orders from the three cases that carry a plan. The plan page and the main page change
nothing: they go through the policies.

`App.fs` holds `Session: SessionState` and `Signing: SigningState`; `tokenOf` becomes
`SessionState.token`; the reply's stale-request guard compares `SessionState.session … |>
Option.map _.OpenedToken`, which keeps the nested option as today (an anonymous reply's notice
stays dropped); the anonymous check of `UrlChanged`, the two snackbar guards before the
transition (`Closing _`, `SupplyingPin _`), the reset after it (`Open _`) and the disclaimer's
`Anonymous` read `SessionState.view`; the interpreters take the `SessionState` for its token
(every Launch, key, code, pin, id, plan, challenge and key they send is in the effect already).

### App.State: the lanes own their state

Three fields leave `App.State` for the lanes, and with them the five places that step them by
hand; the reply's notice guard moves into the machine. Afterwards `App.State` holds the four
lanes and nothing else of them, and the `SessionMsg` and `SigningMsg` arms of `App.update` are
the transition and the interpretation of its effects, as the order lanes' arms are.

| In `App.State` today | Goes to | Stepped by hand today in |
|---|---|---|
| `MovedOn: OrderPlanHead option` | `SessionState.MovedOn`, a view-tier field beside the phase and the request, as `Selected` is in `OrderPlanState` | the `RecordMovedOn` arm; the reset when the Session leaves `Open`; the `TellVersionOpened` fold; the `TellRefused(Blocked _)` fold |
| `WorkAtSign: PlanWork` | `SigningState.AskedOver: PlanWork option`, held while a signature is under way | `askedOver` on `Sign`; `afterSigned` on `TellSigned` |
| `PlanWork: PlanWork` | `OrderPlanState.Work` (go/no-go, below) | the `OrderPlanMsg` arm; `afterSigned` on `TellSigned` |

**The moved-on notice into the Session lane.** `MovedOn` is `Some` only through
`opened session movedOn`; every other constructor holds none, `closing` included, since today
a close drops the notice and a failed close reopens without it. `SessionState.movedOn` serves
`ISession.MovedOn`, so the plan page's bar changes nothing. `SessionMsg.Told of from:
OpenedToken option * RecordNotice` replaces `App.RecordMovedOn` and the reply's path into
`EndedByServer`: the guard moves from `processApiMsg` into the machine, on the triple, `Open
current` with nothing in flight `when current.OpenedToken = from`, the guard `Reopened`
already has; both notices sit under it, a newer version through `MovedOn.receive` with
`SessionEffect.TellMovedOn head` when it is news, an ending as today's `EndedByServer` arm; a
`Told` anywhere else is dropped, as it is today while closing or anonymous.
`SessionMsg.EndedByServer` stays for the signing's `EndSession` effect. `SessionMsg.Blocked of
OrderPlanHead`, a signature refused because the record moved on, keeps the head through
`MovedOn.receive` and tells nothing: the refusal sentence already says it, and today the head
is stored without a snackbar. `Reopened` spends the notice through `MovedOn.opened` in the
machine; `TellVersionOpened` stays for the snackbar. `TokenRenewed` keeps the notice.

**The work a signature is asked over, into the signing lane.** `PlanWork` (the DU and
`changedBy`, `afterChange`, `afterCommand`, `afterSigned`) moves out of `UnsignedWorkPolicy.fs`
into `PlanWork.fs`, compiled before `SigningMachine.fs`; `UnsignedWorkPolicy.fs` keeps
`hasUnsignedWork`. `SigningMsg.Sign` carries the plan's work with the plan and the request id;
`SigningState.AskedOver` holds it from `requesting` to `unsent`, none in `idle`; `Accept`
carries it forward; `SigningEffect.TellSigned` carries it back beside the version signed, so
that the App (and later the plan lane) applies `afterSigned`. `askedOver` goes: its job, a
`Sign` while one is under way keeps the first's work, is the machine's "one thing at a time"
once the work travels in the message. `ISigning.Sign` stays `OrderPlan -> unit`; the App
supplies the work with the request id.

**The plan's work into the plan lane** (go/no-go). `OrderPlanState.Work`, stepped in `run`
exactly as the `OrderPlanMsg` arm of `App.update` steps it today: a version opened and a
patient cleared put it as signed, a command steps it by `afterCommand`; `OrderPlanMsg.Signed
of askedOver: PlanWork` applies `afterSigned`, dispatched from the `TellSigned` effect as
`TokenRenewed` is from `RenewToken`; `OrderPlanState.work` serves the leave guard. This
overlaps [#898](https://github.com/informedica/GenPRES/issues/898) (`App.State` per tier) and
[#903](https://github.com/informedica/GenPRES/issues/903) (the guard); both get a comment.
Skipped if the plan's work is better left app-level.

**What stays in `App.update`, on purpose.** The one cross-lane rule, a signature belongs to an
open Session and whatever ends the Session drops it, stays as an interpretation in the session
arm (`Signing = SigningState.idle` when the view is not `Open`), as `ResetWorkbench` from the
plan lane is interpreted into the workbench. In Left open as a possible `SessionEffect`.

Issue #895 absorbs these folds. They are not view-tier work, but they are what makes the two lanes
"just states" as the order lanes are, which is why the issue is read this way; the As-built
rows land here, and #898 gets a comment saying which fields left `App.State` under this plan.

## Confidence

High for the machines: every state today has one row in the tables above, every arm of both
transitions maps onto the triple, the existing transition tests (forty-five for the Session,
ten for the signing) are the oracle and are rewritten onto the constructors with their
assertions unchanged, and the view tests of the first two code steps pin each old case to its
view case before the records exist. Medium for the record rewrite of the Session: the stale
presentation is a class of bug the DU could not have, held off by the two rules above and the
tests that pin the seven arms they protect. High for the pages: they are two matches per file,
checked by the Fable compile, and the policies are under test. Medium for the folds: each is
today's `App.update` code moved into a machine arm with a test, but the notice guard changes
who holds it, and the plan's work is a go/no-go.

## Steps

Each step is one PR of at most 200 changed source lines, merged before the next starts.
Client `.fs` files are edited directly. Every code step builds, runs `dotnet run ServerTests`
(the Shared tests link the client files by path), compiles the client with Fable with the
touched `.jsx` inspected, and passes Fantomas and the dependency-rule check; the docs steps run
the markdown linter.

1. **`SigningView` from today's DU** (`refactor(client)`). `SigningView` and
   `Signing.view : Signing -> SigningView`, six arms, `Unsent(_, plan, _)` onto `Challenged(plan,
   None)`; `ISigning.Signing` a view; `dialogOpen`, `UnsignedWorkPolicy`, the sign dialog and
   the three reads in `App.fs` on the view. Tests: one view test per old case; the policy
   tests on the view cases. About 60 lines.
2. **The signing lane record** (`refactor(client)`). `SigningPhase`, `SigningRequest`,
   `SigningState` with its six constructors, `SigningState.view` replacing `Signing.view`,
   `SigningState.transition` over the triple; the DU deleted; `App.fs` on the constructors.
   Tests: the four fixtures and the inline states onto the constructors, the view tests
   re-pointed. About 140 lines.
3. **`SessionView` from today's DU** (`refactor(client)`). `SessionView` and `Session.view`,
   twelve arms; `ISession.Session` a view; `SessionGatePolicy`, `canSign`, the title bar, the
   gate view and the read sites in `App.fs` on the view. Tests: thirteen view tests (the two
   refusals); the gate policy tests on the view, their `launch` and `key` fixtures gone; the
   `canSign` tests on the view. About 110 lines.
4. **The Session lane types** (`refactor(client)`). `SessionPhase`, `SessionRequest`,
   `SessionState` with its twelve constructors, `session`, `token`, `maxAttempts` and
   `SessionState.view`, beside the DU, as 706 put `view` beside `toDeferred`. Tests: one per
   constructor, `SessionState.view` against `Session.view` of the old case. About 100 lines,
   additions only.
5. **The Session machine on the record, first half** (`refactor(client)`).
   `SessionState.transition` beside the DU, with `onOpened` and `present`, handling `Present`,
   `Outcome`, `Retry`, `Resume`, `Resumed`, `RefusedAtCallback` and `OpenAnonymous`; every
   other message answers the state unchanged for now. Tests: the present, outcome, retry and
   resume lists duplicated onto the constructors against `SessionState.transition`; the old
   ones stay until the next step. About 110 lines, additions only.
6. **The second half, the DU deleted** (`refactor(client)`). The `SupplyPin`, `PinAnswered`,
   `Close`, `Closed`, `CloseFailed`, `EndedByServer`, `TokenRenewed`, `OpenVersion` and
   `Reopened` arms; the DU, `Session.view`, `Session.opened`, `Session.present` and
   `Session.retryable` deleted; the file's remark on its module name rewritten; `App.fs` on
   the record (`tokenOf`, the reply guard, the initial state, the interpreters' annotations).
   Tests: the ending and open-version lists onto the constructors, the duplicates of step 5
   gone. About 150 lines, added and deleted; should it read over 200, `Reopened` and
   `OpenVersion` move into step 5, since the `App.fs` wiring cannot be split from the deletion.
7. **The moved-on notice in the Session lane** (`refactor(client)`). `SessionState.MovedOn`
   and `movedOn`, `opened session movedOn`; `SessionMsg.Told` and `SessionMsg.Blocked`,
   `SessionEffect.TellMovedOn`; `Reopened` spends the notice; `App.fs` loses `MovedOn`,
   `RecordMovedOn` and the guard in `processApiMsg`, and tells the two snackbars from the
   session effects. Tests: told on an open Session with the token it started from, once per
   version; a stale token or another phase drops it; blocked keeps without telling; reopened
   spends it and keeps a newer one; a close drops it. About 70 lines.
8. **The work a signature is asked over, in the signing lane** (`refactor(client)`).
   `PlanWork.fs` extracted, before `SigningMachine.fs` in both project files, with its tests
   in `PlanWorkTests.fs`; `Sign` carries the work; `SigningState.AskedOver`; `TellSigned`
   carries it back; `askedOver` gone; `App.fs` loses `WorkAtSign`. Tests: the three
   `askedOver` tests as machine tests (idle, under way, a second sign after an edit);
   `TellSigned` carries the work; and the work survives every rebuild between the sign and
   the signature, since each goes through a constructor that must carry it: a wrong PIN
   (submitting, challenged again with the refusal, submitting again) and a lost answer
   (submitting, unsent, submitting under the kept key, signed), both ending in a
   `TellSigned` that carries the work the first `Sign` was asked over, not the work of an
   edit made meanwhile. About 80 lines.
9. **The plan's work in the plan lane** (`refactor(client)`, go/no-go). `OrderPlanState.Work`
   and `work`, `OrderPlanMsg.Signed`; `App.fs` loses `PlanWork`; the leave guard reads the
   lane. Tests: the work after a changing command, a recalculation, a version, a patient
   cleared, a signature over the same work and over older work. About 60 lines.
10. **Docs** (`docs`). This plan's As built; plan 706's first left-open bullet and plan 691's
    closed on this plan; plan 903 if the plan's work moved; the comments on #898 and #903.

## Acceptance

- No `SessionMachine.Session` and no `SigningMachine.Signing` DU in the repository; `ISession`
  and `ISigning` expose the views and the commands only; no `Launch`, `PublicKey`, request
  id, challenge id or key is matched under `Views/` or `Components/`.
- The transition tests pass with their assertions unchanged, on the constructors; the view
  tests of steps 1, 3 and 4 pass; the guards are pinned: the same-Launch `Present` no-op, an
  outcome, a close, a resume, a PIN answer and a Submission answer landing only on their
  request, not twice for `Sign`, `Confirm`, `SupplyPin` and `Close`, the retry under the kept
  key, a close completing after a newer Session dropped.
- `App.State` holds no `MovedOn`, no `WorkAtSign` (and no `PlanWork` after step 9); the
  `SessionMsg` and `SigningMsg` arms of `App.update` are the transition and the interpretation
  of its effects.
- By hand, in the demo: a `prescriber` launch shows the user in the title bar and closes; a
  `no-pin` enrolment with a wrong code keeps the form with the tries left, then opens on the
  right one; `none` offers a retry; a wrong PIN then `1234` signs; the server stopped while the
  PIN dialog is open brings the dialog back and the next confirm signs under the same key. For
  the folds: in two browsers, B signs and A's next action shows the moved-on snackbar once and
  the bar; A signs anyway and is refused with the bar still there and no second snackbar; A
  opens the newest version and the bar goes; an order changed while the PIN dialog is open is
  still unsigned work for the leave guard after the signature.

## Questions for review

1. The refusal in the view: two cases or a boolean. Answered: two cases, `Refused` and
   `Retryable`.
2. The lost answer: an `InFlight` case as the issue said, or the kept key as its own field.
   Answered: its own field; nothing is in flight, and the view shows `Challenged`.
3. The three folds under this issue or under #898. Answered: here; they are what makes the
   lanes states, and #898 gets a comment.
4. Step 9, the plan's work into the plan lane: go or no-go. Answered: go; `App.State` ends
   with the four lanes and nothing else of them.

## Left open

- `OpenVersion` as an `InFlight` case (`Reopening`), so that the plan page could grey the
  bar while a version is opened; today the request is fire-and-forget with the guard on the
  token.
- `SessionView.Open of SessionOpened` narrowed to what the pages read (the user, whether
  there is a patient), leaving the token and the key thumbprint to the accessor.
- The signing dropped when the Session ends, as a `SessionEffect` interpreted into the signing
  lane instead of the check in `App.update`.
- The comments on `OrderPlanCartMsg` and `OrderPlanMsg` in `OrderPlanMachine.fs`, which read
  alike and do not say which stage each belongs to.
- [#896](https://github.com/informedica/GenPRES/issues/896),
  [#897](https://github.com/informedica/GenPRES/issues/897),
  [#898](https://github.com/informedica/GenPRES/issues/898), unchanged.

## As built

Built in the order proposed, one PR at a time, each merged before the next started, the client
edited directly. Every step left the Shared tests, the Fable compile, Fantomas and the
dependency-rule check green. Four steps were split in two for the size rule, which counts
added and deleted lines alike: the deletion of a DU, or a file moving, is its own PR.

| Step | PR | Landed |
|---|---|---|
| plan | #919 | this document; review: the two retry paths of step 8 as machine tests |
| 1, `SigningView` | #920 | `SigningView` and `Signing.view` from the DU; `ISigning.Signing` the view; `dialogOpen`, `UnsignedWorkPolicy`, `SignDialog.fs` and `App.fs` on it; `Unsent` shown as `Challenged(plan, None)` |
| 2a, the signing lane's types | #921 | `SigningPhase`, `SigningRequest`, `SigningState` with its six constructors and `view`, beside the DU; each constructor's view pinned against the DU's |
| 2b, the signing machine on the record | #922 | `SigningState.transition` over the triple, `App.fs` holding a `SigningState`; `Signing.transition` gone, the DU kept as the oracle |
| 2c, the Signing DU gone | #923 | the DU, `Signing.view` and the oracle tests deleted |
| 3, `SessionView` | #925 | `SessionView` and `Session.view` from the DU; the gate, `canSign`, the title bar, the gate view and the `App.fs` read sites on it; the gate's refusal arm one helper over `Refused` and `Retryable` |
| 4, the Session lane's types | #926 | `SessionPhase`, `SessionRequest`, `SessionState` with its twelve constructors, `session`, `token`, `maxAttempts` and `view`, beside the DU; pinned against `Session.view` |
| 5, the Session machine, first half | #927 | `SessionState.transition` for the presentation, its outcome, the retry, the resume, the refusal at the callback and the anonymous open; the DU's tests duplicated onto the constructors |
| 6a, the second half, the App with it | #928 | the PIN, the close, the endings, the token and the version on the record; `App.fs` on `SessionState.token` and `SessionState.session` |
| 6b, the Session DU gone | #929 | the DU, `module Session` and the DU's tests deleted; `maxAttempts` on `SessionState`, `worthRetrying` private, the file's header rewritten |
| 7a, the notice in the Session lane | #930 | `SessionState.MovedOn`, `SessionMsg.Told` and `Blocked`, `SessionEffect.TellMovedOn`; `Reopened` spends the notice; the `MovedOn` module above `SessionState` |
| 7b, the App on the lane's notice | #931 | `App.State` loses `MovedOn` and `RecordMovedOn`; `Told` from `processApiMsg`, `Blocked` from the signing interpreter, the snackbars from the session effects |
| 8a, the plan's work in its own file | #933 | `PlanWorkPolicy.fs` before `SigningMachine.fs`, the DU and the pure steps moved with their tests |
| 8b, the work in the signing lane | #934 | `Sign` carries the work, `SigningState.AskedOver` holds it through a wrong PIN and a lost answer, `TellSigned` carries it back; `askedOver` and `WorkAtSign` gone |
| 9, the plan's work in the plan lane | #935 | `OrderPlanState.Work`, `OrderPlanMsg.Signed`, `work` and `withWork`; `App.State.PlanWork` gone; review: the work counts where the command goes out |
| 10, docs | this PR | this section; plans 691 and 706 closed on this one, plan 903 on the lane; the comments on #898 and #903 |

### Deviations from the text above

- **Steps 2, 6, 7 and 8 were each two or three PRs.** The step-2 estimate counted added
  lines; the review of the plan counts added and deleted alike, and the record rewrite of a
  machine deletes about as much as it adds. Each split follows the shape of plan 706's step
  1: the new beside the old, pinned against it; then the old deleted.
- **The gate's refusal arm is one helper, not two arms.** Step 3 said the two view cases
  would each have an arm, correct only because a missing browser identity is the one refusal
  worth retrying. `refused tr refusal (retry: Action option)` keeps today's order of the two
  rules, a missing role offering the anonymous open whatever the retry, so the gate does not
  rest on that fact.
- **A command counts as work where it goes out, not where the App counted it.** Step 9 said
  the stepping is `App.update`'s verbatim, which counted a command at the message, dropped or
  not. In the lane that would change the state on a message the machine drops, against the
  invariant its own tests pin; and a step that waited and was then dropped by a failure, a
  later step or a patient change would keep a count for nothing (the review of #935). So the
  count is at the request stage's send: a fresh command as it goes out, the step that waited
  once, on the answer, and a dropped one never. The leave-page guard asks over what went out.
- **The second-half arms of the Session moved with the App wiring, not with the deletion.**
  Step 6 had `transition` completed and the DU deleted in one PR; 6a completed it and wired
  `App.fs`, 6b deleted, so that the deletion was deletions only.
- **`Unreachable` lost its count, as planned, and `SessionView.Retryable` is the second
  case, as decided**; `Told` is guarded on the triple, with nothing under way, so that a
  notice during a close is dropped as it was.
