# Implementation plan for issue #NNN: the order lanes in two stages

> A proposal for review, not a settled plan: the approach, the decisions and the step split are
> put up here to be confirmed, changed or refused in this PR. The file takes the issue number
> in its name once the issue is filed.

## Problem description

The client holds the prescribing workbench and the order plan in two pure state machines
(`OrderContextMachine.fs`, `OrderPlanMachine.fs`) and publishes them to the pages as
`Deferred<_>` through `AppEnv`. Both machines mix what the clinical model *is* (the patient held,
the context evaluated, the plan, the context the dialog shows) with what the transport is doing
(a request in flight, its id, the command sent, the copy shown meanwhile):

- `OrderContextState.Recalculating of sent * found * request` holds two copies of the context,
  because a refusal restores `found` while the page shows `sent`; `Loading of Patient * request`
  welds a domain fact (a patient held, nothing evaluated yet) to a request id.
- `OrderPlanState.Recalculating of OrderPlan * selected * request * sent` bundles the plan, the
  dialog's selection and the transport in one case; `Loading` likewise.
- `Deferred.Recalculating of 't` is the in-flight phase carrying a stand-in value under a name
  that hides it: `Deferred.inProgress` says true for it and `Deferred.resolved` says false.

Three things are tangled, not two: the domain value, the transport (request id, payload sent),
and the UI (the copy shown meanwhile, the dialog's selection). The split has to place all three.

The review that led to this plan also found that the change is not a refactoring alone. Today's
behaviour is inconsistent in ways the current tests cannot see, and the split forces a decision
on each:

1. A refused workbench command restores the context *found*, the last one evaluated
   (`OrderContextMachine.fs`, the `Answered` arm over `Recalculating`). A refused plan `Filter`
   restores the plan *sent*: the rows stay checked with stale totals (`OrderPlanMachine.fs`, the
   `Filter` and `Answered` arms). The test "a refused change leaves the plan as the request
   found it" has sent = found in its fixture and cannot tell the two rules apart.
2. `Loading` carries no value and projects `InProgress`; a re-evaluation projects
   `Recalculating value`. So a first load shows a bare spinner while a reset of the same
   workbench shows the empty page greyed.
3. A filter seeded from the url or the menu before a patient is set projects as `Resolved`: an
   unevaluated seed looks settled, and a change made then silently replaces the seed.
4. The value shown while in flight is the context *sent* for the workbench, and for the plan the
   plan the command carries for `Recalculate` but the plan held for every other command.
5. `OrderContextState.context` returns the context *sent* while in flight, and five callers in
   `App.fs` (the reload, the url seed, the two filter syncs, the ContinuousMeds page guard)
   depend on that.

Untested today: the two projections in `App.fs` (not linked into any test project), every
`Deferred` helper (`Extensions.fs` opens `Browser.Dom`, so it cannot be linked), the accessors
`patient`, `context` and `map`, a seed or a reset arriving while a request is in flight, and a
plan refusal with sent ≠ found.

## Approaches considered

1. **A generic lane type nested in the domain DU**, `ForPatient of Lane<value, command>` with
   `Lane = Settled of value | InFlight of found * sent * request`. Keeps "no patient with a
   request in flight" unrepresentable. Rejected: a DU inside a DU, a third type, and the domain
   value held inside the communication wrapper; hard to read for what it buys.
2. **A generic request type beside the domain DU**, `{ Domain; Request: Request<command> }` with
   `Request = Idle | InFlight of sent * request`. Rejected: the type is an option of a pair with a
   name; a field says the same.
3. **`Deferred` stored beside the domain as the communication state.** Rejected: its `Resolved v`
   would duplicate the domain's `Evaluated v` and the two would have to be kept equal by hand.
4. **A domain DU without request ids, one optional field for the request in flight, and the two
   stages run in order by `transition`**; `Deferred` derived for the pages. Proposed.

## Chosen approach

Proposed: option 4. Per lane, the state is a record of a domain DU and one communication field:

```fsharp
// OrderContextMachine.fs
[<RequireQualifiedAccess>]
type Workbench =
    | NoPatient
    // a filter chosen before a patient is set: evaluated once one is
    | Seeded of OrderContext
    // the context last evaluated for the patient held; the empty one before the first answer
    | Evaluated of OrderContext

type OrderContextState =
    {
        Workbench: Workbench
        // the one request under way: the command and the context sent (what the page shows
        // meanwhile) and the id the answer must name; none while idle
        InFlight: ((OrderContextCommand * OrderContext) * string) option
    }

// OrderPlanMachine.fs
[<RequireQualifiedAccess>]
type Plan =
    | NoPatient
    // the plan as answered; the empty one before the first answer
    | Opened of OrderPlan

type OrderPlanState =
    {
        Plan: Plan
        // the one change under way: the command sent and the id the answer must name
        InFlight: (PlanCommand * string) option
        // the context the dialog shows, by id: the client's own, next to whatever is in flight
        Selected: string option
    }
```

**The communication stage** is two functions per lane on `InFlight`: `busy`, and
`landing request`, the payload sent when the id matches and none otherwise.

**The domain stage** is `Workbench.step` and `Plan.step`: a message without request ids to a new
domain value and a list of intents.

```fsharp
[<RequireQualifiedAccess>]
type WorkbenchMsg =
    | PatientChanged of Patient option
    | Seed of OrderContext
    | Command of OrderContextCommand * OrderContext
    // the answer that landed, with what was sent
    | Landed of sent: (OrderContextCommand * OrderContext) * Result<OrderContext, string[]>
    | Reset

[<RequireQualifiedAccess>]
type WorkbenchIntent =
    // supersedes whatever is under way: UpdateOrderContext and the two filter syncs
    | Evaluate of OrderContext
    // one at a time: dropped while a request is under way
    | Call of OrderContextCommand * OrderContext
    | Sync of Filter
    | GoToLifeSupport
    | Tell of string[]
```

The domain changes only on `Landed Ok` and on `PatientChanged`. A `Landed Error` leaves it as it
is and emits `Tell` and `Sync`, which is how a refusal restores the value held. `Seeded` emits
nothing, which is how the seed waits. `Plan.step` has the same shape, with
`Landed of PlanCommand * Result<OrderPlan, string[]>` and the intents `Open` and `Recalculate`
(both supersede), `Call`, `CheckInteractions`, `GoToPlanPage`, `ResetWorkbench`, `Tell`.

**The two stages in order.** `transition` keeps its signature and runs them:

```text
Answered(request, result):
    stage 1, communication: landing request state.InFlight
        none  -> dropped (a stale answer, or nothing in flight)
        sent  -> stage 2, domain: step (Landed(sent, result)); InFlight cleared; intents applied
any other message:
    stage 2, domain: step msg
    stage 1, communication: the intents applied
        Evaluate / Open / Recalculate -> InFlight replaced, whatever was under way
        Call                          -> InFlight set when idle, dropped when busy
                                         (the domain did not change on dispatch)
        the rest                      -> today's effects, one to one
```

Two arms read the payload in flight: a patient change while busy patches the new patient into it
and re-sends it, so the selection in flight stays visible and is evaluated for the new patient;
a patient change while an `Open` is in flight re-sends `Open` with the version's contexts. A
patient cleared clears `InFlight`.

Newly representable, and guarded, one test each: `NoPatient` or `Seeded` with a request in flight
(never built: a patient cleared clears it, a seed never sends, a landed answer on `NoPatient` is
dropped by the domain); a selection without a plan (`Select` dropped on `NoPatient`, as today).

**What does not change.** `OrderContextMsg`, `OrderPlanMsg`, both effect types, both `transition`
signatures, `State.OrderContext` and `State.OrderPlan` in `App.fs`, `AppEnv`, and the effect
interpreters. The accessors `App.fs` reads keep their names: `patient` is the domain's;
`context` is the payload in flight if any, else the domain's; `map f` applies to the domain
value and the payload both; `emptyFor`, `plan`, `selected` as today.

**`Deferred`, derived for the pages**, one tested function per lane, in the machine (written in
today's case names; step 6 renames):

```text
OrderContextState.toDeferred
    NoPatient,     _                   -> HasNotStartedYet
    Seeded ctx,    _                   -> Recalculating ctx     (decision b)
    Evaluated ctx, None                -> Resolved ctx
    Evaluated _,   Some((_, sent), _)  -> Recalculating sent    (first load: the empty workbench, decision c)

OrderPlanState.toDeferred, with meanwhile found = Recalculate tp -> tp | _ -> found
    NoPatient,    _                    -> HasNotStartedYet
    Opened tp,    None                 -> Resolved tp
    Opened found, Some(sent, _)        -> Recalculating (meanwhile found sent)
```

Neither lane produces `InProgress` any more; it stays for the other `Deferred` fields.

### Proposed decisions, each named, to confirm in review

| # | Today | Decision | Commit |
|---|---|---|---|
| a | the plan restores the plan *sent* on a refused `Filter` | restore the plan held, as the workbench does | fix |
| a' | both lanes patch a new patient into what a refusal restores | kept: the plan's patient must stay in step with the panel and the workbench; the stale totals after a refused patient recalculation stay with #672 | – |
| b | a seed before a patient projects `Resolved` | shown as in flight: greyed until evaluated | fix |
| c | `Loading` projects `InProgress`, a bare spinner | `Loading` deleted; the first load and a cart open are a request over the empty value, shown greyed | fix |
| d | `Deferred.resolved` contradicts `Deferred.inProgress` | `resolved` and `exists` deleted; no callers | refactor |
| e | `context` returns the context sent | kept: `context` is the value shown, `patient` the domain's | – |
| f | `map` rewrites both copies | kept: the domain value and the payload, both | – |
| g | a "no dose rules" refusal re-evaluates under the answered request id | kept; not visible | – |
| h | a patient change during an `Open` in flight sends `Open` with no contexts and drops the version | re-send `Open` with the version's contexts from the payload in flight | fix |

## Questions for review

The answers change the plan; the rest is mechanics.

1. **The first load and a cart open** (decision c): the empty page greyed, as proposed, or the
   bare spinner kept through an `Opening of Patient` case in the domain DU, at the cost of one
   case that exists only to be projected as `InProgress`?
2. **The seed before a patient** (decision b): greyed until evaluated, as proposed, or kept
   editable as today, with the projection lying once on purpose?
3. **A refused patient recalculation** (decision a'): keep the new patient with stale totals in
   both lanes, as proposed, or blank the totals until #672 decides? Restoring the old patient is
   not on the table: it would put the plan out of step with the panel and the workbench.
4. **The `Deferred` rename** (step 6): last, as proposed, so the two projections are pinned before
   a hundred pattern-match sites move; first, so every projection test is written against the
   final shape; or not at all, the name kept as debt?
5. **The PR split**: one PR per lane holding a `refactor` commit and a `fix` commit, as
   proposed, or the fix commits as PRs of their own from the start?
6. **The session and signing machines**: a follow-up issue on this pattern once the order lanes
   have landed, or in scope here?

## Confidence

High for the machines and their tests: the domain steps are today's arms with the request ids
removed, the composer is one function per lane, and every arm has a test on either side of the
change. Medium for the pages: they are not under test, the first load and the seed change what
they show (decisions b and c), and the final rename touches about a hundred pattern-match sites
under the Fable compile alone. That is why the rename is its own PR with nothing else in it.

## Steps

Proposed as one PR per step against `master`, the client edited directly. Every step leaves
`dotnet run ServerTests`, the Fable compile, Fantomas and the dependency-rule check green.

1. **This plan.**
2. **`Deferred` in its own file** (`refactor(client)`): `src/Informedica.GenPRES.Client/Deferred.fs`
   as the first compile item, `[<AutoOpen>]`, out of `Extensions.fs`; `resolved` and `exists`
   deleted; the type unchanged so the pages compile untouched; linked into
   `tests/Informedica.GenPRES.Shared.Tests` before the machines so the projections can be
   tested. About 60 lines.
3. **The workbench in two stages** (one PR, two commits). `refactor(client)`: `Workbench`,
   `WorkbenchMsg`, `WorkbenchIntent`, `Workbench.step`, the record, `landing`, `transition` as
   the composer, `toDeferred` in the machine and the one in `App.fs` deleted; the 13 tests
   rewritten as `Workbench.step` tests without request ids plus composer tests for the four
   invariants and the guards; the projection's four cases pinned. `fix(client)`: `Loading` gone,
   the first load evaluates the empty workbench (c); the seed shown as in flight (b); the commit
   body names the visible change and that the ContinuousMeds page switch no longer sends a
   superseding reset during the first load. About 200 lines; the fix commit becomes its own PR
   if the diff exceeds it.
4. **The plan in two stages** (one PR, three commits). `test(client)`: the refusal fixtures in
   `OrderPlanMachineTests.fs` get sent ≠ found and assert today's rule, so the fix flips a real
   assertion. `refactor(client)`: `Plan`, `PlanMsg`, `PlanIntent`, `Plan.step`, the record with
   `Selected`, the composer, `toDeferred` with `meanwhile`, the one in `App.fs` deleted; tests
   split as in step 3. `fix(client)`: a refused filter restores the plan held (a); `Loading`
   gone, the first open and a cart open over the empty plan (c); a patient change during an
   `Open` re-sends the version's contexts (h). About 200 lines, same split rule.
5. **Docs** (`docs`): the case table and the invariant lines in
   `docs/domain/dose-quantity-stepping-flow.md`; this plan's "As built". About 50 lines.
6. **`Deferred.InProgress of 't option`** (`refactor(client)`): `Recalculating` gone; `None` when
   nothing can be shown meanwhile, `Some` when a value stands in. Producers: the two
   `toDeferred` functions, pinned by their tests. Consumers: every `| InProgress ->` becomes
   `| InProgress _ ->` (the plain-load fields included), every `| Recalculating v ->` becomes
   `| InProgress(Some v) ->`; `ViewHelpers.progressOrEmpty` keeps the spinner for `None` and
   nothing for `Some`; the helpers follow. About a hundred mechanical sites in `Views/`,
   `Pages/`, `Components/` and `App.fs`; the rename alone, nothing else in the diff. Known and
   accepted: the two lanes never produce `None` and the plain-load fields never `Some`. About
   200 lines.

## Acceptance

Against `GENPRES_PROD=0 dotnet run`, launched as `prescriber`:

- Set a patient: the prescribing page and the plan show the empty page greyed, then enabled.
- Open the app with a medication in the url and no patient: the seed shows greyed; set the
  patient: it is evaluated.
- Pick a generic and change the patient while the spinner shows: the generic picked stays
  visible and is evaluated for the new patient. Put an unknown generic in the url: the snackbar
  says why and the context last evaluated returns.
- On the plan page check rows and force a refusal: the rows return to as held.
- Sign, then reload: the cart opens showing the empty plan greyed, then the version.
- Two browsers on one patient: a reopen arriving while a step is in flight wins.
- In the Network tab: no extra `processOrderContext` on the ContinuousMeds page switch during
  the first load.
- `dotnet run ServerTests`, the Fable compile, Fantomas and the dependency-rule check stay green
  after every step.

## Left open

- `SessionMachine` (`Launching` with its attempt count, `Resuming`, `Closing`) and
  `SigningMachine` (`Requesting`, `Submitting`, `Unsent`) split the same way;
  `SessionGatePolicy`'s busy flag then reads the in-flight field.
- The pages reading the two records directly, and `toDeferred` gone (plan 667 lists it).
- `Deferred.bind` drops the busy flag in the interventions calculation; a plan refusal skips the
  interactions check, so a warning can outlive the plan that caused it; the two lanes format
  the snackbar differently; a reload is settled by any workbench answer, stale ones included;
  the "no dose rules" re-evaluation reuses the answered request id; the stale totals after a
  refused patient recalculation (#672).
