# Implementation plan for issue 706

## Problem description

The client holds each order lane in three kinds of state: the domain (the workbench, the
cart), the communication (the one request in flight) and the view (what the pages show
meanwhile, the dialog's selection). Plan [691](691-order-lanes-two-stages.md) named the first
two, the domain DUs `OrderContextWorkbench` and `OrderPlanCart` and the field `InFlight`, and
left the third unnamed. It is spread over `toDeferred` in both machines,
`OrderPlanState.Selected`, and loose fields of `App.State`.

`Deferred<'t>` (`src/Informedica.GenPRES.Client.Core/Deferred.fs`) is not the communication state.
It is never stored; it is derived by `toDeferred` and read only by the pages through `AppEnv`.
It already is the view tier, undeclared, and its comment still tells a transport story.

`Deferred.Provisional of 't` is a view fact wearing a transport type. Its value is chosen per
lane: the context sent for the workbench; for the plan the plan the command carries on a
recalculation and the plan held otherwise (`OrderPlanState.meanwhile`). The type's own comment
says so: "which value a lane shows is the lane's decision, made in its projection". A case
whose meaning each producer sets is not the type's case. Only the two lanes ever produce it;
none of the fifteen `Deferred` fields of `App.State` does.

`Deferred` is a 2×2 product, value shown × request open, that the views undo at every site:
about 27 arms match `Resolved x | Provisional x` identically, about 24 match `Resolved x`
alone (the guard before a dispatch), and greying reads `Deferred.inProgress`.

Found on the way:

- `SessionMachine.Session` and `SigningMachine.Signing` reach the pages raw
  (`ISession.Session`, `ISigning.Signing`, matched in the pages), with attempt counts, request ids
  and idempotency keys in their cases. The larger instance of the same problem; 691 left it
  open.
- A plain fetch blanks on refetch: `LoadFormulary Started` sets `Formulary = InProgress`
  (`App.fs`) and the table disappears, for lack of the cell the lanes grew `Provisional` for.
- The pure page rules (`isAnythingLoading`, `progress`, `inPlan` in `Views/Prescribe.fs`;
  `isRecalculating` in `Views/OrderPlan.fs` and `Views/Nutrition.fs`) are view-tier logic in
  files the tests cannot link.
- Both machines drop a command that arrives while a request is under way, silently
  (`OrderContextMachine.fs`, `OrderPlanMachine.fs`, the `Call _ when state.InFlight.IsSome`
  arm of `apply`): plan 691's "one at a time". For stepping that is the wrong rule. A second
  step while the first is under way is the user's latest word, and it must win: the request
  under way is superseded, as a patient change (`Open`, `Recalculate`) already supersedes it.
  Three arms build a command from `Provisional` today, and are right to: the order dialog's
  step into the plan (`Views/OrderPlan.fs`, `Navigate` over the plan shown), the dialog's
  context re-wrapped for `Views/Order.fs`, and the nutrition slots. Nothing names which
  commands supersede and which wait.

### The client state model

Every piece of client state is in exactly one tier:

| Tier | What | Owner |
|---|---|---|
| domain | the clinical model as the lane holds it | the lane's domain DU, stepped by `step` |
| communication | the one request under way: payload sent, request id | `InFlight`, stepped by the composer |
| view | the domain as the pages see it, plus what the pages chose (a selection) | the lane's view DU, derived by `view`; stored view fields in the lane record |
| app-level UI | page, snackbar, language, disclaimer, list filters | `App.State`, stepped by `App.update` |
| component-local | a dropdown open, a print dialog, an optimistic field | React hooks in the component |

Rules:

1. View state never stores what it can derive.
2. Render on `Settled | Changing`. A stepping command (the order dialog's, in either lane)
   is built in a `Changing` arm too, over the value shown, since that is what the next step
   applies to; every other command is built in a `Settled` arm only. Which commands supersede
   the request under way and which wait is the machine's policy, never a page's.
3. Component-local state is never a function of view state (the order dialog is open iff a
   context is selected, no second flag), and never holds domain (`Views/Order.fs` keeps the
   stepped order in hook state: the known violation, its own issue).

## Approaches considered

1. **A view record per lane**, `{ Value: 't option; Busy: bool }` or a generic `Shown<'t>`
   composed by the lanes. Rejected: a boolean flag in state, against the coding
   instructions; a selection without a plan stays representable; no shape for the
   case-specific data of the Session and Signing views (a refusal with its retry, a
   challenge with its tries left).
2. **Keep the four-case `Deferred` and document it as view state.** Rejected: cheapest, but
   the product the views undo at every site stays, and so does a case whose meaning each
   producer sets and the plain fetches never produce.
3. **A view DU per lane** (chosen). One record per lane with one field per tier, one pure
   `view` to a DU whose cases are the states a page can be in, each carrying only what is
   valid in that state. Illegal states become unwritable, no boolean in state, the same
   shape carries the Session's and Signing's case-specific data, and the DUs shrink when a
   state is deleted.

Considered and set aside inside 3: helper functions on the view module (`context`, `busy`,
`settled`) so that pages seldom match cases. Pages match the cases directly instead; the
compiler then checks every page against every state, and the two reads that matter are two
match shapes, not two names.

## Chosen approach

Approach 3. A lane is one record with one field per tier, private, built by constructors,
and one pure `view` from the record to a view DU. Pages match the cases directly; a page rule
that is more than a match (does the plan hold this context; which context does the dialog
show) is a pure function in the view module, tested. `AppEnv` is the view tier's contract:
view DUs and app-level values in, messages out. A plain fetch has no lane; `Deferred<'t>` is
its reading, three cases.

The view DUs live in the machine files, which the shared tests already link by path.

```fsharp
// OrderContextMachine.fs
/// The workbench as the page shows it: nothing without a patient; the first evaluation
/// under way with nothing to show; the context the server answered, nothing under way; a
/// request under way, the context sent shown meanwhile.
[<RequireQualifiedAccess>]
type OrderContextView =
    | NoPatient
    | Evaluating
    | Settled of OrderContext
    | Changing of OrderContext

module OrderContextState =
    let view (state: OrderContextState) : OrderContextView =
        match state.Workbench, state.InFlight with
        | OrderContextWorkbench.NoPatient, _ -> OrderContextView.NoPatient
        | OrderContextWorkbench.Unevaluated _, _ -> OrderContextView.Evaluating
        | OrderContextWorkbench.Evaluated _, Some((_, sent), _) -> OrderContextView.Changing sent
        | OrderContextWorkbench.Evaluated(_, ctx), None -> OrderContextView.Settled ctx
```

```fsharp
// OrderPlanMachine.fs
/// The plan as the pages show it: nothing without a patient; an open under way with nothing
/// to show; the plan the server answered with the dialog's selection; a change under way,
/// the plan shown meanwhile (the one the command carries for a recalculation, the plan held
/// otherwise) with the selection. A selection without a plan cannot be written.
[<RequireQualifiedAccess>]
type OrderPlanView =
    | NoPatient
    | Opening
    | Settled of OrderPlan * selected: string option
    | Changing of OrderPlan * selected: string option

module OrderPlanView =
    /// Whether the plan holds the context, for the prescribe button.
    let holds (id: string) (view: OrderPlanView) = ...
    /// The context the dialog shows, as the order dialog reads it: settled or changing as
    /// the plan is; none without a selection.
    let dialog (view: OrderPlanView) : OrderContextView option = ...

module OrderPlanState =
    let view (state: OrderPlanState) : OrderPlanView =
        match state.Cart, state.InFlight with
        | OrderPlanCart.NoPatient _, _ -> OrderPlanView.NoPatient
        | OrderPlanCart.Unopened _, _ -> OrderPlanView.Opening
        | OrderPlanCart.Opened(_, tp), Some(sent, _) -> OrderPlanView.Changing(meanwhile tp sent, state.Selected)
        | OrderPlanCart.Opened(_, tp), None -> OrderPlanView.Settled(tp, state.Selected)
```

Both `view` tables are today's `toDeferred` tables case for case, so the existing projection
tests are the oracle. `AppEnv`: `IOrderContext.OrderContext: OrderContextView`;
`IOrderPlan.OrderPlan: OrderPlanView` with the selection inside it, so the `Selected` member
goes; `Select`, `Filter` and the commands stay. `Selected` stays stored in `OrderPlanState`,
its comment naming it as the view tier's field beside the domain and the transport.
`Deferred` loses `Provisional`, `inProgress` and `toDeferred`, and its comment says what it
is: the pages' reading of one value the server is asked for.

The migration keeps today's shapes but one: a `Resolved` command arm becomes a `Settled`
command arm, a `Resolved | Provisional` render arm becomes `Settled | Changing`, a
`Deferred.inProgress` or `isRecalculating` greying test becomes a match on
`Evaluating | Changing _` or `Opening | Changing _`. The one is the stepping: the order
dialog's commands are built in `Settled | Changing` in both lanes, and the dialog's fields
are not greyed while a step is under way, only marked; the indicator stays. That needs the
machines to supersede first (the step below), so the pages never rely on a click being
dropped. `Views/OrderPlan.fs` hands the order dialog `OrderPlanView.dialog`
instead of re-wrapping the plan's case onto a child `Deferred`; `Views/Nutrition.fs`, which
already passes a context and a busy flag by hand, passes the `OrderContextView`.

### Plan 646

Plan [646](646-patient-minimum.md), merged as PR #704, resolved decision b of 691 by deleting
`OrderContextWorkbench.Seeded` (its step 4, PR #713): a filter before a patient is illegal,
not unconfirmed. So the workbench view has no seed case. The machines hold the patient beside
the context and the plan (`Evaluated of Patient * OrderContext`, `Opened of Patient *
OrderPlan`), on the one `Patient` record after 646's private wrapper was undone, and 646's
fixture patient is in the machine tests. The code steps below build on that.

### Decision c of 691

Deleting `Unevaluated` and `Unopened`, so that the first evaluation and a cart open run over
the empty value and the page shows it greyed instead of a bare spinner, is a domain change:
the `Evaluating` and `Opening` cases then go and both view DUs have three cases. It is
scheduled last, as its own go/no-go, since it changes what the pages show and flips the
ContinuousMeds guard in `App.fs` (`context` answers the empty context during the first load).

## Confidence

High for the machines: the `view` functions are the `toDeferred` tables as 646 leaves them,
the projection tests pin them, and supersede is the request-id guard the machines already
have for a patient change, under test. Medium for the pages: they are not under test, and
the command rule (stepping in `Settled | Changing`, everything else in `Settled`) is checked
by review and the Fable compile alone. High that the pattern generalises: the Session and
Signing view DUs are today's DUs minus the transport payloads, checked case by case; the
plain-fetch follow-up fixes a visible wart.

## Steps

Each step is one PR of at most 200 changed lines. Client `.fs` files are edited directly;
every step builds, runs `dotnet run ServerTests`, compiles the client with Fable with the
touched `.jsx` inspected, and passes Fantomas and the dependency-rule check.

0. **646 step 4** landed as PR #713 (`OrderContextWorkbench.Seeded` deleted, 646's fixture
   patient in the machine tests); nothing to wait for.
1. **The view DUs** (`refactor(client)`). `OrderContextView` and `OrderPlanView` with
   `holds` and `dialog`, and `view` in both machines beside `toDeferred`. Tests: one per lane
   mirroring the `toDeferred` test case for case; `holds`; `dialog` for a settled and a
   changing plan, with and without a selection. About 80 lines plus 70 of tests.
2. **A step supersedes** (`fix(client)`). In both machines the request stage sends a stepping
   `Call` while a request is under way instead of dropping it: a fresh request id, so that the
   older answer lands nowhere (`landing` already refuses it) and its failure rolls nothing
   back; every other `Call` stays dropped while busy, as 691 decided. Which commands step is
   one predicate per lane beside the intents. Tests: a step during a step sends the newer and
   drops the older answer; a delete during a step is still dropped. About 40 lines plus 40 of
   tests.
3. **`AppEnv` and the small views** (`refactor(client)`). Interim members
   `IOrderContext.OrderContextView` and `IOrderPlan.OrderPlanView` wired in `App.fs`;
   `Views/Patient.fs`, `Views/Formulary.fs`, `Views/Parenteralia.fs` and the totals in
   `Pages/GenPres.fs` read them. About 40 lines.
4. **`Views/Order.fs`** (`refactor(client)`). The prop `orderContext: OrderContextView`;
   both parents build it, `Prescribe.fs` from the lane and `OrderPlan.fs` through `dialog`;
   the stepping commands in `Settled | Changing`; the hooks that keyed on `Resolved` key on
   `Settled`; `isFieldLoading` marks the field stepped without disabling it. About 60 lines.
5. **`Views/Prescribe.fs`** (`refactor(client)`). The selects, the scenario list, the
   prescribe button and the hint on the cases; `inPlan` through `holds`. About 70 lines.
6. **`Views/OrderPlan.fs`** (`refactor(client)`). The rows, the checkboxes, the sign, delete
   and filter commands in `Settled` arms, `Navigate` in `Settled | Changing`, the selection
   from the case. About 50 lines.
7. **`Views/Nutrition.fs`** (`refactor(client)`). The slots on `OrderContextView`, the delete
   and print dialogs on the cases. About 45 lines.
8. **The old projection deleted** (`refactor(client)`). `Provisional`, `toDeferred`, the
   `Provisional` arm of `ViewHelpers.progressOrEmpty`, the `Deferred` members and `Selected`
   of the two interfaces, and `Deferred.inProgress`: its five callers (`Views/Patient.fs`
   twice, `Views/Formulary.fs`, `Views/Parenteralia.fs`, `Views/Order.fs`) all read a lane
   and became a match on the view cases in steps 3 and 4; no plain fetch calls it; the
   interim members renamed; the `Deferred` comment rewritten. Acceptance: no `Provisional` in
   the repository. About 90 lines, deletion-heavy.
9. **Docs** (`docs`). The `Deferred` table of `docs/domain/dose-quantity-stepping-flow.md`
   becomes the view-DU table, and its stepping story says the newer step wins; plan 691's
   "one at a time" for `Call` narrowed to the commands that wait, and its left-open items on
   the projection and plan 667's "optional: `toDeferred` goes" closed; this plan's As built;
   the follow-up issues filed (below).
10. **Decision c, workbench** (`feat(client)`, go/no-go). `Unevaluated` deleted; the first
    evaluation runs over the empty context for the patient held; `Evaluating` goes; the
    ContinuousMeds guard reviewed. About 90 lines, five tests changed.
11. **Decision c, plan** (`feat(client)`, go/no-go). `Unopened` deleted; a cart open runs
    over the empty plan for the patient held; a patient change during an open reads the
    contexts from the `Open` payload in flight, as today; `Opening` goes. About 100 lines,
    five tests changed.

## Acceptance

- No `Provisional`, `toDeferred` or `Deferred.inProgress` in the repository; `Deferred` has
  three cases.
- The projection tests pass unchanged against `view`, and the supersede tests of step 2 pass.
- By hand, in the demo: two quick steps on a dose in the order dialog, on the prescribe page
  and in the plan, end on the second step's value; a delete pressed during a step is refused
  by the greyed button, not lost.

## Questions for review

1. The case names: `Settled` and `Changing`, or `Held` and `Sent`, or others. Answered:
   `Settled` and `Changing`; `Held` and `Sent` describe the transport again, which is what
   the tier leaves.
2. Decision c (steps 10 and 11) here, or its own issue? Answered: its own issue, #899; a
   domain change with a go/no-go, and the refactor is complete without it.
3. The plain-fetch case (`Deferred.Refreshing of 't`, the previous value kept while a
   refetch runs) as a last step here, one mechanical PR over about fourteen fields, or its own
   issue? Answered: its own issue, #896; it fixes what a user sees and deserves its own
   changelog line.

## Left open

Each is its own issue, filed in step 9:

- `SessionMachine` and `SigningMachine` on the same pattern: done by plan
  [895](895-session-signing-view-tier.md) (#895), with the lost answer as a kept key rather
  than an `InFlight` case, and the moved-on notice, the work a signature is asked over and the
  plan's work folded into the lanes, so that `App.State` holds the four lanes and nothing else
  of them.
- `Deferred.Refreshing of 't` for the plain fetches: done by plan
  [896](896-deferred-refreshing.md) (#896), a fourth case that keeps the previous value while
  a fetch runs again, the four refetched pages rendering from it and acting on `Resolved`.
- The component-local rule: done by plan [897](897-component-local-state.md) (#897); the
  order dialog and the nutrition slot read the order shown instead of a hook copy, the
  prescribe page's dialog opens on a selection the workbench lane carries, as the plan's.
- `App.State` grouped one field per tier: done by plan [898](898-app-state-tiers.md) (#898);
  four records, the lanes, the plain fetches, the admin login and the app-level UI, the snackbar
  one value among them.
- Decision c of 691, steps 10 and 11 above: #899, built as #901 and the PR after it (see As built).
- Plan 691's other left-open items, unchanged.

## As built

Built in the order proposed, one PR at a time, each under review before the next started, the
client edited directly. Every step left the Shared tests, the Fable compile with the touched
`.jsx` inspected, Fantomas and the dependency-rule check green.

| Step | PR | Landed |
|---|---|---|
| plan | #707 | this document; review: rule 2 and step 2 rewritten from first-wins to a step that waits, step 0 landed, the sketches on the machines' shapes, an Acceptance section |
| 1, the view DUs | #886 | `OrderContextView`, `OrderPlanView` with `holds`, `OrderContextView.dialog`, `view` in both machines beside `toDeferred`; 6 tests mirroring the projection tests (an earlier #709 was closed unmerged, cut before 646 step 4) |
| 2, a step waits | #887 | `Pending` in both records with a `pending` constructor, `Dialog.waits` and `carries`, `OrderPlanCart.waits` and `replay`; a dialog command during a request waits as the one pending and goes out on the answer, a step over the context answered; 6 tests, 1 rewritten |
| 3, `AppEnv` and the small views | #889 | the interim members `OrderContextView` and `OrderPlanView`; the patient panel, the formulary and the parenteralia filters, the totals on the cases |
| 4, the order dialog | #890 | the prop `orderContext: OrderContextView`, the steps built from `Settled` and `Changing`, `update` over the order shown; `SimpleSelect`'s steps rest on `disabled` alone; review: only the field in flight may change again, and only on a solved order (`selectFor`, `rests`) |
| 5, the prescribe page | #891 | the filter commands from `Settled`, the selects and the scenarios from `Settled` and `Changing`, `inPlan` through `holds`; review: `holds` by the order's id |
| 6, the plan page | #892 | the rows and the selection from the case, sign, delete and filter from `Settled`, `Navigate` from `Settled` and `Changing`, `modalOpen` from the case |
| 7, the nutrition page | #893 | the slots and the buttons from `Settled` and `Changing`, the delete confirmation from `Settled`; the slot's `init` on the context itself |
| 8, the old projection | #894 | `Provisional`, `Deferred.inProgress`, both `toDeferred`, `OrderPlanState.selected`, the `Deferred` members and `Selected` of the interfaces gone; the view members under the final names; the interactions page on the plan view; 4 `toDeferred` tests gone |
| 9, docs | this PR | the stepping-flow document on the view cases and the step that waits; plans 691 and 667 closed on the projection; this section; #895 to #899 filed |
| 10 and 11, decision c | #901, this PR | decided after the plan: the greyed empty form on the first load. `Unevaluated` and `Unopened` gone, the first evaluation and an open over the empty value held and shown, `Evaluating` and `Opening` gone; a patient change during an open opens the same contexts again from the request; the ContinuousMeds guard falls through instead of resetting an empty workbench; the interactions page counts drugs from a settled plan only, as before |

### Deviations from the text above

- **A step waits; it does not supersede.** Step 2 said a stepping call supersedes the request
  in flight. The server does the stepping, so two `Increase` over the same base end one step
  on, and supersede would have lost a click as the drop did. Decided on review: the latest
  dialog command waits as the one pending, under its own request id, and goes out when the
  answer lands, a step over the context answered and a value typed over the context it was
  typed into; a failure, a patient change, an open, an evaluation and the workbench cleared
  after an order was prescribed (`OrderContextMsg.Reset`) drop it. The dialog's own reset,
  `ResetOrderScenario`, is a dialog command and waits like a step.
- **One property while a step is in flight, on a solved order.** The lane keeps one command
  pending, so the dialog lets only the field whose change went out change again while its
  answer is awaited, and only on an order solved through; every other field and the reset
  rest until the answer (review of #890). The component and item selects never send a request
  and stay enabled.
- **`dialog` is `OrderContextView.dialog`**, in `OrderContextMachine.fs`, since
  `OrderPlanMachine.fs` compiles first and the function answers an `OrderContextView`.
- **`holds` takes the order's id.** A context's own id is minted by the plan; the prescribe
  page compares the scenario's order id, as the rule it replaced did (review of #891).
- **The nutrition slot keeps its context and a flag.** Step 7 said the slots on
  `OrderContextView`. A slot always has a context, so a view prop would carry two cases that
  never occur and still need an arm before the hooks; the slot keeps
  `nutritionContext: OrderContext` and `isRecalculating`, which the page derives from the plan
  view, and its `init` takes the context itself.
- **The interactions page was a reader too**, not named in the plan; it reads the plan's drugs
  from `OrderPlanView.Settled`, as it read them from `Resolved`.
- **Steps 10 and 11 went to their own issue** (#899), as question 2 was answered, and were built from it once the greyed first load was chosen over the spinner.
