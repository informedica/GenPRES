# Implementation plan for issue 706

## Problem description

The client holds each order lane in three kinds of state: the domain (the workbench, the
cart), the communication (the one request in flight) and the view (what the pages show
meanwhile, the dialog's selection). Plan [691](691-order-lanes-two-stages.md) named the first
two, the domain DUs `OrderContextWorkbench` and `OrderPlanCart` and the field `InFlight`, and
left the third unnamed. It is spread over `toDeferred` in both machines,
`OrderPlanState.Selected`, and loose fields of `App.State`.

`Deferred<'t>` (`src/Informedica.GenPRES.Client/Deferred.fs`) is not the communication state.
It is never stored; it is derived by `toDeferred` and read only by the pages through `AppEnv`.
It already is the view tier, undeclared, and its comment still tells a transport story.

`Deferred.Provisional of 't` is a view fact wearing a transport type. Its value is chosen per
lane: the context sent for the workbench; for the plan the plan the command carries on a
recalculation and the plan held otherwise (`OrderPlanState.meanwhile`). The type's own comment
says so: "which value a lane shows is the lane's decision, made in its projection". A case
whose meaning each producer sets is not the type's case. Only the two lanes ever produce it;
the other twelve `Deferred` fields of `App.State` never do.

`Deferred` is a 2×2 product, value shown × request open, that the views undo at every site:
about 27 arms match `Resolved x | Provisional x` identically, about 24 match `Resolved x`
alone (the guard before a dispatch), and greying reads `Deferred.inProgress`.

Found on the way:

- `SessionMachine.Session` and `SigningMachine.Signing` reach the pages raw
  (`ISession.Session`, `ISigning.Signing`, 16 match sites), with attempt counts, request ids
  and idempotency keys in their cases. The larger instance of the same problem; 691 left it
  open.
- A plain fetch blanks on refetch: `LoadFormulary Started` sets `Formulary = InProgress`
  (`App.fs`) and the table disappears, for lack of the cell the lanes grew `Provisional` for.
- The pure page rules (`isAnythingLoading`, `progress`, `inPlan` in `Views/Prescribe.fs`;
  `isRecalculating` in `Views/OrderPlan.fs` and `Views/Nutrition.fs`) are view-tier logic in
  files the tests cannot link.
- Both machines drop a command that arrives while a request is under way, silently
  (`OrderContextMachine.fs`, `OrderPlanMachine.fs`, the `Call _ when state.InFlight.IsSome`
  arm of `apply`). The 24 `Resolved x -> dispatch` arms are what keeps a click from
  vanishing; nothing names that rule.

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
2. Render on `Settled | Changing`; build a command in a `Settled` arm only.
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
        | OrderContextWorkbench.Evaluated ctx, None -> OrderContextView.Settled ctx
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
        | OrderPlanCart.NoPatient, _ -> OrderPlanView.NoPatient
        | OrderPlanCart.Unopened _, _ -> OrderPlanView.Opening
        | OrderPlanCart.Opened tp, Some(sent, _) -> OrderPlanView.Changing(meanwhile tp sent, state.Selected)
        | OrderPlanCart.Opened tp, None -> OrderPlanView.Settled(tp, state.Selected)
```

Both `view` tables are today's `toDeferred` tables case for case, so the existing projection
tests are the oracle. `AppEnv`: `IOrderContext.OrderContext: OrderContextView`;
`IOrderPlan.OrderPlan: OrderPlanView` with the selection inside it, so the `Selected` member
goes; `Select`, `Filter` and the commands stay. `Selected` stays stored in `OrderPlanState`,
its comment naming it as the view tier's field beside the domain and the transport.
`Deferred` loses `Provisional`, `inProgress` and `toDeferred`, and its comment says what it
is: the pages' reading of one value the server is asked for.

The migration keeps today's shapes: a `Resolved` command arm becomes a `Settled` command
arm, a `Resolved | Provisional` render arm becomes `Settled | Changing`, a `Deferred.inProgress`
or `isRecalculating` greying test becomes a match on `Evaluating | Changing _` or
`Opening | Changing _`. `Views/OrderPlan.fs` hands the order dialog `OrderPlanView.dialog`
instead of re-wrapping the plan's case onto a child `Deferred`; `Views/Nutrition.fs`, which
already passes a context and a busy flag by hand, passes the `OrderContextView`.

### Plan 646

Plan [646](646-patient-minimum.md), merged as PR #704 with its step 1b as PR #708, resolves
decision b of 691 by deleting `OrderContextWorkbench.Seeded` (its step 4): a filter before a patient is illegal, not
unconfirmed. So the workbench view has no seed case. Its steps 1c and 4 edit the same machine
files and tests as this plan, and its fixture patient replaces the empty one in the machine
tests. The code steps below start after 646 step 4 has merged.

### Decision c of 691

Deleting `Unevaluated` and `Unopened`, so that the first evaluation and a cart open run over
the empty value and the page shows it greyed instead of a bare spinner, is a domain change:
the `Evaluating` and `Opening` cases then go and both view DUs have three cases. It is
scheduled last, as its own go/no-go, since it changes what the pages show and flips the
ContinuousMeds guard in `App.fs` (`context` answers the empty context during the first load).

## Confidence

High for the machines: the `view` functions are the `toDeferred` tables as 646 leaves them,
and the projection tests pin them. Medium for the pages: they are not under test, and the
guard rule (a command in a `Settled` arm only) is checked by review and the Fable compile
alone. High that the pattern generalises: the Session and Signing view DUs are today's DUs
minus the transport payloads, checked case by case; the plain-fetch follow-up fixes a
visible wart.

## Steps

Each step is one PR of at most 200 changed lines. Client `.fs` files are edited directly;
every step builds, runs `dotnet run ServerTests`, compiles the client with Fable with the
touched `.jsx` inspected, and passes Fantomas and the dependency-rule check.

0. **Wait for 646 step 4** (`OrderContextWorkbench.Seeded` deleted, the domain `Patient` in
   the machines, 646's fixture patient in the machine tests).
1. **The view DUs** (`refactor(client)`). `OrderContextView` and `OrderPlanView` with
   `holds` and `dialog`, and `view` in both machines beside `toDeferred`. Tests: one per lane
   mirroring the `toDeferred` test case for case; `holds`; `dialog` for a settled and a
   changing plan, with and without a selection. About 80 lines plus 70 of tests.
2. **`AppEnv` and the small views** (`refactor(client)`). Interim members
   `IOrderContext.OrderContextView` and `IOrderPlan.OrderPlanView` wired in `App.fs`;
   `Views/Patient.fs`, `Views/Formulary.fs`, `Views/Parenteralia.fs` and the totals in
   `Pages/GenPres.fs` read them. About 40 lines.
3. **`Views/Order.fs`** (`refactor(client)`). The prop `orderContext: OrderContextView`;
   both parents build it, `Prescribe.fs` from the lane and `OrderPlan.fs` through `dialog`;
   the hooks that keyed on `Resolved` key on `Settled`. About 60 lines.
4. **`Views/Prescribe.fs`** (`refactor(client)`). The selects, the scenario list, the
   prescribe button and the hint on the cases; `inPlan` through `holds`. About 70 lines.
5. **`Views/OrderPlan.fs`** (`refactor(client)`). The rows, the checkboxes, the sign, delete
   and filter commands in `Settled` arms, the selection from the case. About 50 lines.
6. **`Views/Nutrition.fs`** (`refactor(client)`). The slots on `OrderContextView`, the delete
   and print dialogs on the cases. About 45 lines.
7. **The old projection deleted** (`refactor(client)`). `Provisional`, `toDeferred`, the
   `Provisional` arm of `ViewHelpers.progressOrEmpty`, the `Deferred` members and `Selected`
   of the two interfaces, and `Deferred.inProgress`: its five callers (`Views/Patient.fs`
   twice, `Views/Formulary.fs`, `Views/Parenteralia.fs`, `Views/Order.fs`) all read a lane
   and became a match on the view cases in steps 2 and 3; no plain fetch calls it; the interim members renamed; the `Deferred`
   comment rewritten. Acceptance: no `Provisional` in the repository. About 90 lines,
   deletion-heavy.
8. **Docs** (`docs`). The `Deferred` table of `docs/domain/dose-quantity-stepping-flow.md`
   becomes the view-DU table; plan 691's left-open items on the projection and plan 667's
   "optional: `toDeferred` goes" closed; this plan's As built; the follow-up issues filed
   (below).
9. **Decision c, workbench** (`fix(client)`, go/no-go). `Unevaluated` deleted; the first
   evaluation runs over the empty context for the patient held; `Evaluating` goes; the
   ContinuousMeds guard reviewed. About 90 lines, five tests changed.
10. **Decision c, plan** (`fix(client)`, go/no-go). `Unopened` deleted; a cart open runs
    over the empty plan for the patient held; a patient change during an open reads the
    contexts from the `Open` payload in flight, as today; `Opening` goes. About 100 lines,
    five tests changed.

## Questions for review

1. The case names: `Settled` and `Changing`, or `Held` and `Sent`, or others.
2. Decision c (steps 9 and 10) here, or its own issue?
3. The plain-fetch case (`Deferred.Refreshing of 't`, the previous value kept while a
   refetch runs) as a last step here, one mechanical PR over about fourteen fields, or its own
   issue?

## Left open

Each becomes its own issue, filed in step 8:

- `SessionMachine` and `SigningMachine` on the same pattern: a domain DU without the launch,
  key and attempt payloads, `InFlight` for `Launching`, `Resuming`, `Closing`, `SupplyingPin`,
  `Requesting`, `Submitting` and `Unsent`, and a view DU that is today's DU minus those
  payloads; `SessionGatePolicy` then reads the view.
- `Deferred.Refreshing of 't` for the plain fetches, unless taken up here (question 3).
- The component-local rule: the stepped order in `Views/Order.fs` hook state; the prescribe
  page's own dialog flag beside the plan page's selection-derived one.
- `App.State` grouped one field per tier.
- Plan 691's other left-open items, unchanged.
