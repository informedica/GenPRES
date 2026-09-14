# Implementation plan for issue 667

## Problem description

The one plan of [654](654-api-per-use-case.md) keeps its two kinds of order asymmetrically. A
nutrition order keeps its workbench in the plan (`OrderPlan.NutritionContexts`); a drug order
keeps only its scenario. The prescribing workbench (`State.OrderContext` in the client) hands the
plan a scenario, and the order-plan page rebuilds a context around that scenario with
`OrderContext.fromOrderScenario` every time its dialog opens, which recovers the filter and the
one scenario and nothing else: the pick lists and the candidates the workbench had are gone.
Issue [#667](https://github.com/informedica/GenPRES/issues/667) lists what that costs:

- **Two client lanes for one action.** An order-context command on the prescribing page goes
  through `processOrderContext`; the same command on a plan order goes through `processOrderPlan`
  as `Navigate(plan, None, cmd, ctx)`, and on a nutrition order as `Navigate(plan, Some id, cmd,
  ctx)`. The `None` branch evaluates and folds into `Selected`; the `Some` branch does the same
  through the workbench.
- **Deletion and the reopen rebuild written twice**, once per kind. After a reopen the nutrition
  workbenches are empty ([#666](https://github.com/informedica/GenPRES/issues/666)): the signed
  record carries scenarios only, and the cart is recreated with `OrderPlan.create`, which sets no
  contexts.
- **A lossy step from the plan page.** A drug order stepped there loses what the prescribing
  workbench knew.

The client side has a cost of its own, found while reviewing `App.fs` for this plan. The session
and the signing are pure state machines (`SessionMachine.fs`, `SigningMachine.fs`: a `transition`
from a message and a state to a state and effects, `App.fs` interpreting the effects into
commands, tests under Expecto). The plan and the workbench are `Deferred<_>` values instead,
handled across six `update` branches (`OrderPlanMsg`, `LoadOrderPlanResult`, `ShowOrderPlan`,
`LoadCart`, `OrderContextMsg`, `LoadOrderContextResult`): a command sent while one is in flight
is dropped silently, an answer is applied whatever request it answers, the page and the
workbench are reset as side effects inside those branches, and none of it runs outside the
browser.

## Approaches considered

1. **Share the helpers, keep the asymmetry.** One `withOrders`, one removal; what
   [#664](https://github.com/informedica/GenPRES/pull/664) and
   [#665](https://github.com/informedica/GenPRES/pull/665) did. Rejected: the two lanes and the
   lossy rebuild stay.
2. **The prescribing workbench as a plan context with a not-in-plan flag; one lane.** Rejected:
   the workbench syncs the formulary and parenteralia filters on every update, is seeded from
   the url before a patient exists (and the plan does not exist without one), keys the
   prescribing page's Elmish state, and is not signed. Every fold over the plan (totals,
   interactions, deletion, the signing challenge) would need an in-plan test, and the
   solver-heavy workbench evaluation would share one request lane with nutrition steps.
3. **A wrapper record around each context in the plan** (id, label, category, removable flag),
   with the plan keeping a stored `Scenarios` projection and the signed record keeping scenarios
   only, rebuilt into contexts at reopen. This was the first draft. Rejected: the projection
   needs upsert bookkeeping on every change, the rebuild is lossy until the order is first
   stepped, and the category has to be recorded somewhere else to survive the reopen.
4. **The category derived from the generic at reopen.** Rejected: the electrolyte and glucose
   nutrition rule set lists KCl, NaCl, glucose, magnesiumsulfaat, fosfaat and calciumgluconaat,
   all prescribable as drugs on the prescribing page. A signed drug order of glucose 10% would
   reopen as a nutrition context, with the nutrition filter, on the nutrition page, and with
   category-cascading deletion.
5. **Client machines first, on today's wire.** Rejected: a plan machine fed by `ShowOrderPlan`
   would have to diff whole plans to tell a selection from a filter from a change; the message
   set should be fixed by the new commands first.
6. **The plan is its contexts; the signed record stores them; then the plan and the workbench
   as pure machines, the views moved first.** Chosen.

## Chosen approach

The plan is the patient it is computed for and a list of order contexts, each carrying all its
data as it was when it was created or added: its filter and pick lists, its candidate scenarios,
the patient it was evaluated for, its id and its category. The orders of the plan are derived,
not stored: every context narrowed to one scenario contributes that scenario. The signed record
stores the contexts, so a reopen is the signed plan as it was, nothing rebuilt and nothing
guessed. The prescribing workbench is the same type, a context not yet in the plan, on its own
lane; the prescribe button moves it into the plan. `Navigate` always names a context and
evaluates it over the context's own patient. Removal is one command for every kind. On the
client, a pure `PlanMachine` and a pure
`ContextMachine` replace the `Deferred` handling, with the same shape and the same tests as the
session and signing machines.

### Wire (`Shared/Types.fs`, `Shared/Api.fs`, `Shared/Models.fs`)

- `OrderCategory = Drug | Nutrition of NutritionCategory`, qualified access.
- `OrderContext` gains `Id: string` and `Category: OrderCategory`. A workbench not in the plan
  has the empty id; `AddOrder` mints one and the category is `Drug`; `AddContext` mints one with
  `Nutrition cat`. The label the pages show is derived: the dose-rule set's label for nutrition,
  the generic for a drug. The removable flag is dropped: the server set it to true on every
  context it created and nothing ever set it false; with the plan a plain list of contexts,
  removal is taking one out of the list, so every context is removable by construction. Should
  a category ever have to stay, that is a rule on the category, computed, not a field.
- `OrderPlan = { Patient; Filtered: string[]; OrderContexts: OrderContext[]; Totals }`.
  `Scenarios`, `Selected` and `NutritionContexts` go. `OrderPlan.orders` in `Models.fs` derives
  the orders: the one scenario of every narrowed context, in context order. `Selected` is dialog
  state the server only forwarded; the client holds it. `Filtered` is context ids, which stay
  valid when a context is re-evaluated and its order changes.
- The plan's `Patient` is the patient data as shown, and the data a version is signed on. It
  starts as the session's patient at open (the platform's reading, else the signed version's
  patient, else empty, as today) and can be changed by hand in every mode, as today. Each
  context carries the patient it was created for, and an order is always calculated within its
  context, so for that patient: every context is created or added under the plan's patient of
  that moment, and that patient never changes afterwards. A patient edit therefore rewrites the
  plan's patient and recomputes the totals, as today, and leaves every context as it is;
  replacing an order for new patient data is removing its context and creating a new one from
  it, which is [#672](https://github.com/informedica/GenPRES/issues/672), not this plan. A
  reading that differs from the patient a version was signed on does not touch the contexts at
  open either: the version opens as signed, the panel shows the reading, and the next signature
  tells that the data changed and records whether a reading was present, as today.
- `SignedOrderPlan.OrderContexts: OrderContext[]` replaces `Scenarios`: the signed version is
  the plan as it was, contexts and all. The record grows, since a context carries its pick lists
  and candidate scenarios; it lives in memory today, and the table of plan
  [516](516-sessionrecord-store.md) will carry it. The signing challenge stores the contexts and
  the patient and compares them structurally at submit, as it compares the scenarios today; an
  order id twice among the derived orders is refused as it is today.
- `PlanCommand`, final shape:

  ```fsharp
  type PlanCommand =
      // the totals recomputed over the orders of the filtered contexts
      | Recalculate of OrderPlan
      // the signed version as it was, nothing evaluated; the patient with no contexts is the empty plan
      | Open of Patient * OrderContext[]
      // a workbench evaluated elsewhere, narrowed to one scenario, into the plan as it is
      | AddOrderContext of OrderPlan * OrderContext
      // a fresh workbench for a nutrition category, its filter discovered
      | NewOrderContext of OrderPlan * NutritionCategory
      // an order-context command evaluated over the context named, in that context's own patient
      | Navigate of OrderPlan * contextId: string * OrderContextCommand * OrderContext
      // every kind; a feeding takes its supplements with it
      | RemoveOrderContexts of OrderPlan * ids: string[]
  ```

  Every case names an order context, since every one acts on the plan's `OrderContexts`. Until
  the deletion step the new cases carry interim names next to the old ones: `AddOrder` (for
  `AddOrderContext`), `AddContext` (for `NewOrderContext`) and `RemoveContexts` (for
  `RemoveOrderContexts`); the deletion step renames them, a qualified-access rename with no
  behaviour change.

  `RemoveOrders`, `RemoveContext` and the `None` branch of `Navigate` go. New cases are added
  beside the old ones and the old ones deleted in a later step, so that every step compiles on
  both sides.
- `OrderContext.fromOrderScenario` goes with the rebuild it served.

### Server

`PlanService` in `ServerApi.Services.fs`, the port in `ServerApi.Ports.fs`, the adapter in
`ServerApi.Adapters.fs`, the dispatch in `ServerApi.PlanCommand.fs`, the challenge and the
commit in `ServerApi.Session.fs`:

- `open`: the contexts as given, the totals recomputed. Nothing is evaluated: the pick lists,
  the candidates and the stepped values are what was signed. Closes #666.
- `addOrder`: refuses a context not narrowed to exactly one scenario, and one whose order id the
  plan already holds (the challenge would refuse the plan later; better said now); else the
  context appended with a minted id and `Drug`.
- `addContext`: as today, the context created with a minted id and `Nutrition cat`. It keeps
  the rule the nutrition page's buttons keep: the plan holds one context per nutrition category,
  except supplements (any number, each under a feeding) and electrolyte and glucose lines (any
  number, one per generic prescribed). That rule is what makes "a
  feeding's supplements" every enteral supplement in the plan; no link between them is recorded.
- `navigate` by id only: the command evaluated over the context as the page sent it, in the
  context's own patient. Verified in the code: a stepping, select, update or reset command
  runs the order already in the scenario through the order pipeline and reads no patient
  (`GenORDER.Lib/Api.fs`, `processScenarioOrder`); only `UpdateOrderContext` builds scenarios,
  from the context's patient. Evaluating over another patient would put candidates for one
  patient next to an order solved for another. The result replaces the context by id.
- `removeContexts`: every kind; a feeding takes every enteral supplement.
- `recalculate`: the totals over the orders of the filtered contexts, all when no filter.
- `withOrders`, `contribution`, the following of `Filtered` and `Selected` and `updateContext`'s
  folding go: there is no stored projection to keep in step.
- The challenge stores `plan.OrderContexts` and `plan.Patient`; the commit stores them in the
  version; the duplicate-order check runs over `OrderPlan.orders`.
- Tests in `StubAdapterTests.PlanTests` ("the one plan") and the `Session.challenge` and
  `Session.commit` lists: open keeps the contexts and their stepped values; `addOrder` refuses
  a duplicate and a wide context and appends a narrow one as `Drug`; `addContext` refuses a
  second feeding and an orphan supplement; `navigate` evaluates the context as sent and
  replaces it by id; `removeContexts` over mixed kinds with the cascade; a patient edit
  leaves every context's patient as it was; totals by filtered context id;
  the challenge refuses a changed context and a duplicate order; the version holds the
  contexts; dispatch of each case.

### Client (direct edits)

The views move first, onto the final `AppEnv.IOrderPlan` surface implemented by today's
handlers; the machines then replace the handlers behind a surface that no longer changes.

- `IOrderPlan`: `OrderPlan: Deferred<OrderPlan>` stays (a projection once the machine lands),
  plus `Selected: string option`, `Select: string option -> unit`, `Filter: string[] -> unit`,
  `PlanCommand`. `ShowOrderPlan` retires.
- The reads of `tp.Scenarios` (the order-plan rows, `SigningPolicy.canSign`, the sign dialog,
  the interactions page) become `OrderPlan.orders tp`.
- Prescribe: the prescribe button sends `AddOrder(plan, workbench)` and the page switches to the
  plan; the button is greyed when the plan already holds the scenario's order id.
- Order plan page: the dialog reads the selected context from `OrderContexts` by id and sends
  `Navigate(id, ...)`; deletion maps the selected rows to context ids and sends
  `RemoveContexts`.
- Nutrition page: `Navigate(planRef.current, ncId, ...)`, `RemoveContexts(plan, [| id |])`,
  the nutrition contexts filtered by category from `OrderContexts`.
- The patient panel: editable as today; an edit sends `Recalculate` over the plan with the new
  patient, the contexts untouched.
- `PlanMachine.fs`, pure, before `AppEnv.fs` in the client project, linked into
  `Informedica.GenPRES.Shared.Tests` like `SessionMachine.fs`:

  ```fsharp
  type Plan =
      | NoPatient
      | Loading of Patient * request: string
      | Shown of OrderPlan * selected: string option
      | Recalculating of OrderPlan * selected: string option * inFlight: string

  type PlanMsg =
      | PatientChanged of Patient option
      | Cart of SignedOrderPlan
      | Command of PlanCommand * request: string
      | Answered of request: string * Result<OrderPlan, string[]>
      | Select of string option
      | Filter of string[]

  type PlanEffect =
      | CallPlan of PlanCommand * request: string
      | CheckInteractions of string list
      | GoToPlanPage
      | ResetWorkbench
      | TellError of string[]
  ```

  Request ids are minted at dispatch, as `ISigning.Sign` mints its request. An answer lands only
  on the request in flight; a second click while busy is dropped, as today, with the buttons
  disabled. `Cart head` sends `Open(patient, head.OrderContexts)`. `PatientChanged` sends
  `Open(pat, [||])` when the plan is empty and `Recalculate` over the plan with the new patient
  when it holds contexts; while a request is in flight it goes to `Loading` with a new request,
  so a reopen arriving while a nutrition step is in flight wins. A refused change restores the plan the request was sent
  over; a refused `Open` lands on the empty plan for the patient. `Select` and `Filter` are
  pure: no round trip to open or close the dialog. The OpenedToken check on the reply's notice
  stays in the interpreter (`processApiMsg`), as for signing. `Plan.toDeferred` feeds the env.
- `ContextMachine.fs`, the prescribing workbench:

  ```fsharp
  type Context =
      | NoPatient
      // from the url, before a patient is set
      | Seeded of OrderContext
      | Loading of request: string
      | Shown of OrderContext
      | Recalculating of OrderContext * inFlight: string

  type ContextMsg =
      | PatientChanged of Patient option
      | Seed of OrderContext
      | Command of OrderContextCommand * OrderContext * request: string
      | Answered of request: string * Result<OrderContext, string[]>
      | Reset

  type ContextEffect =
      | CallContext of OrderContextCommand * OrderContext * request: string
      | SyncFormulary of Filter
      | SyncParenteralia of Filter
      // the server found no dose rules: back to the first page, the workbench cleared
      | GoToLifeSupport
      | TellError of string[]
  ```

- `App.fs`: `State.Plan` and `State.Context` replace the two `Deferred` fields;
  `interpretPlanEffect` and `interpretContextEffect` in the shape of `interpretSessionEffect`;
  `SessionEffect.LoadCart` becomes `PlanMsg.Cart`; the machines' `SetPatient` effects still go
  through `UpdatePatient`, which now sends `PatientChanged` to both; the six handlers and
  `planOf`, `withPlan`, `applyPlan`, `handleOrderContext` are deleted.

### What stays as is

- One request in flight per lane, as today; no queue. The first machine version drops a second
  command while busy; a queue can be added when the pages ask for it.
- `Views/Order.fs` is already context-shaped and callback-agnostic; it is not touched.
- `SessionOpened.Head` keeps its shape; only the signed record's content changes from scenarios
  to contexts.
- An order added from Prescribe, or reopened, keeps its full workbench: pick lists, candidates
  and stepped values.
- The session and signing machines are unchanged; the plan machine takes their conventions.
- A patient edit with orders in the plan behaves as today: the plan's patient and the totals
  follow the edit, the orders stay as calculated for the patient their context was created
  with, and a signature records the patient as shown. That the two can differ is not new;
  today the plan's patient is rewritten and the totals recomputed while every scenario keeps
  its order. This plan makes the difference visible, since each context now carries its
  patient, and leaves closing it to #672, where a replacement of the affected orders or a
  guard at the signature is decided.

## Confidence

High for the wire and the server: the fold rules exist in `PlanService` with tests, deriving
the orders removes bookkeeping rather than adding it, and the new cases are additive until the
last deletion. Medium for the client: `App.fs` loses about 130 lines of plan handling and 60 of
workbench handling in two deletion-heavy steps, and the prescribing page keys its Elmish state
on the workbench value; that is why the machines carry their own tests and the views move
before the machines, so that each step stays green on its own.

## Steps

Each step is one PR against `master`; Shared and Server code is drafted in
`Shared/Scripts/Api.fsx` and a new `Server/Scripts/Plan.fsx` (rewritten per step) and migrated
by the maintainer; the client is edited directly. Every step leaves `dotnet run ServerTests`, the
Fable compile and Fantomas green. Wire changes are additive first and deleted last.

0. **[#668](https://github.com/informedica/GenPRES/issues/668), qualified access on
   `OrderContextCommand`** (`refactor`), before anything else: it rewrites the same lines in the
   nutrition and order-plan pages that steps 5 and 8 rewrite. About 110 mechanical lines.
1. **This plan.** Plus a pointer in plan 654's "Left open" and a comment on #666 that it folds
   in here.
2. **The category on the context.** `OrderCategory`; `OrderContext.Id` and `.Category`, the
   empty id and `Drug` in `OrderContext.empty`, set by `addContext` for nutrition; the derived
   label. About 100 lines. Tests: `addContext` sets the id and the category.
3. **The plan is its contexts.** `OrderPlan.OrderContexts` replaces `NutritionContexts`;
   `OrderPlan.orders`; the server keeps `Scenarios` in step as the derived orders so the client
   reads still compile; the nutrition page on the contexts by category; `addContext` and the
   one-per-category rule. About 170 lines. Tests: the plan tests over contexts; a second
   feeding and an orphan supplement refused.
4. **`AddOrder` and `RemoveContexts`**, beside the old cases. A drug order needs a context
   before the signed record can store contexts and before `Scenarios` can leave the plan, so
   these come first. About 150 lines. Tests: a duplicate and a wide context refused, a narrow
   one appended as `Drug` with the workbench as it was; `removeContexts` over mixed kinds with
   the cascade; the dispatch and the log.
5. **The views on the new commands.** Prescribe sends `AddOrder` and switches page, the button
   greyed on a duplicate; the order-plan dialog reads the selected order's context by id and
   sends `Navigate` into it, deletion by `RemoveContexts`; the nutrition page on
   `RemoveContexts`. About 150 lines. Acceptance below.
6. **The signed record stores the contexts.** `SignedOrderPlan.OrderContexts`; the challenge and
   the commit over contexts; `Open` beside the old cases; `LoadCart` and the patient change send
   `Open`. Closes #666. About 150 lines. Tests: open keeps stepped values; the challenge refuses
   a changed context; the version holds the contexts.
7. **`Scenarios` and `Selected` off the plan; `Filtered` as ids.** One PR on both sides: the
   client reads on `OrderPlan.orders`, `Selected` a client state field, the order-plan page's
   dialog, row selection and filter on ids, `ShowOrderPlan` retired for `Select` and `Filter` on
   `IOrderPlan`; `withOrders` and the following bookkeeping deleted; totals by filtered context.
   About 160 lines. Tests: totals by filtered context id; a re-evaluated context keeps its place
   in the filter; the duplicate check over derived orders.
8. **Delete the old cases and settle the names** (`refactor`): `Navigate` by id only,
   `RemoveContext`, `RemoveOrders`, the `None` branch and `removeOrders`, `fromOrderScenario`;
   the nutrition page's `Some ncId` becomes `ncId`; `AddOrder`, `AddContext` and
   `RemoveContexts` renamed `AddOrderContext`, `NewOrderContext` and `RemoveOrderContexts`, with
   their port members and service functions. About 150 lines. Tests: the dispatch case, the log.
9. **`PlanMachine.fs` and its tests**, not wired. About 190 lines. Tests: the lifecycle; a stale
   answer dropped by request id; a patient change while in flight; `Cart` to `Open`;
   `PatientChanged` to `Open` or `Recalculate`; a refused change restores the plan; `Select`
   and `Filter` without effects.
10. **Wire the plan machine.** `State.Plan`, `interpretPlanEffect`, `Plan.toDeferred`; the plan
    handlers and helpers deleted. About 200 lines, deletion heavy. Acceptance below.
11. **`ContextMachine.fs` and its tests**, not wired. About 190 lines. Tests: a seed before a
    patient; the patient change; the sync effects on an update; the no-rules reset; a stale
    answer.
12. **Wire the context machine.** `State.Context`, `interpretContextEffect`;
    `handleOrderContext` and `LoadOrderContextResult` deleted; the prescribing page's Elmish
    deps on the projection. About 180 lines. Acceptance below.
13. **Docs.** `docs/domain/core-domain.md`'s API paragraph, which still names `processCommand`
    and `OrderContextCmd`, and glossary rows for the plan as its contexts and the category; plan
    654's "Left open" closed; #666 closed. About 60 lines.

If step 10 or 12 exceeds 200 lines, the deletion of the dead handlers becomes a following
`refactor` PR. Optional after 13: the views read `Plan` and `Context` directly and
`toDeferred` goes.

## Acceptance

Against `GENPRES_PROD=0 dotnet run`, launched as `prescriber`:

- Prescribe paracetamol and press the prescribe button: the order-plan page opens with the
  order, the Network tab shows one `processOrderPlan` with `AddOrder`; the button is greyed for
  the same scenario afterwards.
- Open the order on the order-plan page and step the dose: `Navigate` names the context id; the
  pick lists are the workbench's, not rebuilt from the scenario.
- Nutrition: add TPN and step it; delete a feeding that has supplements: one `RemoveContexts`,
  the supplements go with it; the order-plan page shows the same orders.
- Sign, then reload: the cart opens on the version with every context as it was, drug and
  nutrition, each on its page, stepped values included; no request other than `Open`.
- With orders in the plan, edit the patient's weight: one `Recalculate`, the totals follow,
  every order stays as calculated for the patient its context was created with (#672 replaces
  them).
- Sign, then relaunch with a changed platform reading: the version opens as signed, the panel
  shows the new reading, the next sign tells that the data changed.
- Two browsers on one patient: a reopen arriving while a step is in flight drops the step's
  answer; the plan shown is the reopened one.
- Prescribe with a medication in the url and no patient: the workbench shows; setting the
  patient evaluates it.
- `dotnet run ServerTests`, the Fable compile and Fantomas stay green after every step.

## As built

| Step | PR | Landed |
|---|---|---|
| plan | #671 | this document; the pointer in plan 654 |
| 0, #668 qualified access | #673 | `[<RequireQualifiedAccess>]` on `OrderContextCommand`, the call sites qualified |
| 2, the category on the context | #674 | `OrderCategory`, `OrderContext.Id`/`Category`, the derived label, `addContext` stamping |
| 3, the plan is its contexts | #676 | `OrderPlan.OrderContexts`, the wrapper gone, the admission rule on the server; review: electrolyte and glucose lines any number |
| 4, `AddOrder` and `RemoveContexts` | this PR | the two commands beside the old ones, the port, the adapter, the dispatch, the client arms |

### Deviations from the text above

- **Steps 4 to 7 reordered.** The text had the signed record storing contexts and `Scenarios`
  leaving the plan before `AddOrder` existed; a drug order added from the prescribing page has
  no context until then, so both would have dropped drug orders. `AddOrder` and
  `RemoveContexts` now come first, the views next, the signed record and the wire cleanup after.
- **The command names say "order context".** Review of step 4 found `AddOrder` next to
  `AddContext` for two ways of adding a context to the plan. The final family names every case
  after what it acts on: `AddOrderContext`, `NewOrderContext`, `RemoveOrderContexts`. The interim
  names stay until the deletion step, so that the steps in between stay additive.
