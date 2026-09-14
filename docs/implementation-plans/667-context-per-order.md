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
   prescribing page's Elmish state, and is not signed. Every fold over the plan (contribution,
   totals, interactions, deletion, the signing challenge) would need an in-plan test, and the
   solver-heavy workbench evaluation would share one request lane with nutrition steps.
3. **Derive a reopened order's category from its generic.** Rejected: the electrolyte and
   glucose nutrition rule set lists KCl, NaCl, glucose, magnesiumsulfaat, fosfaat and
   calciumgluconaat, all prescribable as drugs on the prescribing page. A signed drug order of
   glucose 10% would reopen as a nutrition context, with the nutrition filter, on the nutrition
   page, and with category-cascading deletion. Matching indication and generic is safer but
   still a guess in a path that deletes by category.
4. **Client machines first, on today's wire.** Rejected: a plan machine fed by `ShowOrderPlan`
   would have to diff whole plans to tell a selection from a filter from a change; the message
   set should be fixed by the new commands first.
5. **A context per order on the wire and the server; the category recorded on the scenario;
   the selection client-held; then the plan and the workbench as pure machines, the views
   moved first.** Chosen.

## Chosen approach

Every order in the plan is a context, drug or nutrition, with a category, a label and the
removable flag; `Scenarios` stays the signed projection, every context narrowed to one scenario
contributing it by order id. The prescribing workbench stays a context not yet in the plan, on
its own lane; Prescribe adds it to the plan with a new command instead of handing over its
scenario. `Navigate` always names a context. Removal is one command for every kind. The reopen
rebuilds a context per signed scenario, drug and nutrition alike, which closes #666. On the
client, a pure `PlanMachine` and a pure `ContextMachine` replace the `Deferred` handling, with
the same shape and the same tests as the session and signing machines.

### Wire (`Shared/Types.fs`, `Shared/Api.fs`, `Shared/Models.fs`)

- `OrderCategory = Drug | Nutrition of NutritionCategory`, qualified access.
- `OrderScenario.Category: OrderCategory`. Default `Drug` where scenarios are created
  (`Models.OrderScenario.create`, the server mappers); stamped `Nutrition cat` by the server when
  a nutrition context contributes its scenario. It is part of what was prescribed: this was
  prescribed as TPN. The signing challenge compares the plan's scenarios structurally on both
  sides, so signing does not change. The record store is in memory today; the table of plan
  [516](516-sessionrecord-store.md) will carry the field.
- `PlanContext = { Id; Label; Category: OrderCategory; Removable; OrderContext }` replaces
  `NutritionContext`, and `OrderPlan.Contexts: PlanContext[]` replaces `NutritionContexts`.
  Helpers `OrderPlan.nutritionContexts` and `PlanContext.nutritionCategory` keep the nutrition
  page's sites to one-line substitutions.
- `OrderPlan.Selected` leaves the wire: it is dialog state the server only forwarded. The client
  holds it. `OrderPlan.Filtered` becomes `string[]` of context ids, which stay valid when a
  context is re-evaluated and its order changes; the server's bookkeeping that made the filter
  and the selection follow a replaced order goes with it.
- `PlanCommand`, final shape:

  ```fsharp
  type PlanCommand =
      | Recalculate of OrderPlan
      // a context per signed scenario, nothing evaluated; the patient with no orders is the empty plan
      | Open of Patient * OrderScenario[]
      // the prescribing workbench, narrowed to one scenario, added as a drug context
      | AddOrder of OrderPlan * OrderContext
      | AddContext of OrderPlan * NutritionCategory
      | Navigate of OrderPlan * contextId: string * OrderContextCommand * OrderContext
      // every kind; a feeding takes its supplements with it
      | RemoveContexts of OrderPlan * ids: string[]
  ```

  `Open` evaluates nothing: rebuilding is pure and cheap, and the pick lists come back at the
  first `Navigate` on that context, which is what a nutrition context relies on today.
  `RemoveOrders`, `RemoveContext` and the `None` branch of `Navigate` go. New cases are added
  beside the old ones and the old ones deleted in a last step, so that every step compiles on
  both sides.
- `OrderContext.fromOrderScenario` stays as the server's rebuild helper only.

### Server

`PlanService` in `ServerApi.Services.fs`, the port in `ServerApi.Ports.fs`, the adapter in
`ServerApi.Adapters.fs`, the dispatch in `ServerApi.PlanCommand.fs`:

- `open`: one `PlanContext` per scenario, id minted, the label and the removable flag from the
  category (nutrition: the dose-rule set's label; drug: the generic; both removable), the context
  `fromOrderScenario` narrowed to the scenario, the dose-rule filter applied for nutrition. A
  feeding and its supplements come back as separate contexts, as they were.
- `addOrder`: refuses a context not narrowed to exactly one scenario, and a scenario whose order
  id the plan already holds (the challenge would refuse the plan later; better said now); else a
  `Drug` context appended and its contribution folded in.
- `navigate` by id only, evaluating the context over the plan's patient. Today a nutrition
  context is evaluated over the patient it was created with, and a patient edit rewrites the
  plan's patient only; with drug contexts in the plan every stepped order would use a stale
  weight. One line and a test.
- `removeContexts`: every kind; a feeding takes its supplements. Ownership is by category,
  as today: the plan holds one feeding at most, and a supplement can only be added under it,
  so the feeding's supplements are every enteral supplement in the plan. That rule lives in the
  nutrition page's buttons only; the server now keeps it too: `addContext` refuses a second
  context of any nutrition category other than supplement, and a supplement without a feeding,
  and `open` refuses a signed record that would break it (it cannot arise from a plan the server
  built, so the refusal is a guard, not a path). No feeding-to-supplement link is recorded.
- `withOrders` shrinks to `Scenarios`; the following of `Filtered` and `Selected` goes.
- Tests in `StubAdapterTests.PlanTests` ("the one plan"): open rebuilds a context per category
  and keeps a feeding and its supplement as two; a drug reopens as `Drug` with its generic as the
  label; `addOrder` refuses a duplicate and a wide context and folds a narrow one in; a nutrition
  contribution stamps the category; `removeContexts` over mixed kinds with the cascade;
  `addContext` refuses a second feeding and an orphan supplement; `navigate` uses the plan's
  patient; totals by filtered context id; dispatch of each case.

### Client (direct edits)

The views move first, onto the final `AppEnv.IOrderPlan` surface implemented by today's
handlers; the machines then replace the handlers behind a surface that no longer changes.

- `IOrderPlan`: `OrderPlan: Deferred<OrderPlan>` stays (a projection once the machine lands),
  plus `Selected: string option`, `Select: string option -> unit`, `Filter: string[] -> unit`,
  `PlanCommand`. `ShowOrderPlan` retires.
- Prescribe: the prescribe button sends `AddOrder(plan, workbench)` and the page switches to the
  plan; the button is greyed when the plan already holds the scenario's order id.
- Order plan page: the dialog reads the selected context from `Contexts` by id and sends
  `Navigate(id, ...)`; deletion maps the selected rows to context ids and sends
  `RemoveContexts`.
- Nutrition page: `Navigate(planRef.current, ncId, ...)`, `RemoveContexts(plan, [| id |])`,
  `OrderPlan.nutritionContexts`.
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
  disabled. A patient change while a request is in flight goes to `Loading` with a new request,
  so a reopen arriving while a nutrition step is in flight wins. A refused change restores the
  plan the request was sent over; a refused `Open` lands on the empty plan for the patient.
  `Select` and `Filter` are pure: no round trip to open or close the dialog. The OpenedToken
  check on the reply's notice stays in the interpreter (`processApiMsg`), as for signing.
  `Plan.toDeferred` feeds the env.
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
- The signed record keeps its shape apart from `Category` on each scenario; `SignedOrderPlan`
  keeps scenarios only, and contexts are rebuilt from them.
- Reopened contexts have no pick lists until first stepped, the rebuild #666 asked for. An order
  added from Prescribe keeps its full workbench.
- The session and signing machines are unchanged; the plan machine takes their conventions.

## Confidence

High for the wire and the server: the fold rules exist in `PlanService` with tests, and the new
cases are additive until the last deletion. Medium for the client: `App.fs` loses about 130
lines of plan handling and 60 of workbench handling in two deletion-heavy steps, and the
prescribing page keys its Elmish state on the workbench value; that is why the machines carry
their own tests and the views move before the machines, so that each step stays green on its
own.

## Steps

Each step is one PR against `master`; Shared and Server code is drafted in
`Shared/Scripts/Api.fsx` and a new `Server/Scripts/Plan.fsx` (rewritten per step) and migrated
by the maintainer; the client is edited directly. Every step leaves `dotnet run ServerTests`, the
Fable compile and Fantomas green. Wire changes are additive first and deleted last.

0. **[#668](https://github.com/informedica/GenPRES/issues/668), qualified access on
   `OrderContextCommand`** (`refactor`), before anything else: it rewrites the same lines in the
   nutrition and order-plan pages that steps 6 and 7 rewrite. About 110 mechanical lines.
1. **This plan.** Plus a pointer in plan 654's "Left open" and a comment on #666 that it folds
   in here.
2. **The category and the plan context.** `OrderCategory`; `OrderScenario.Category`, default
   `Drug` at creation, stamped by a nutrition contribution; `PlanContext` replacing
   `NutritionContext`; `OrderPlan.Contexts`; the helpers; the nutrition page on the helpers with
   a `Drug` arm where it matches the category. About 150 lines. Tests: the existing plan tests
   adjusted; a contribution stamps the category.
3. **`Open` and `RemoveContexts`**, beside the old cases; `navigate` over the plan's patient;
   `addContext` and `open` keeping one context per nutrition category, supplements excepted.
   Closes #666. About 150 lines. Tests: open rebuilds per category and keeps a feeding and its
   supplement; a drug reopens as `Drug` with its generic as label; `removeContexts` over mixed
   kinds with the cascade; a second feeding and an orphan supplement refused; the patient
   override.
4. **`AddOrder`.** About 80 lines. Tests: a duplicate refused, a wide context refused, a narrow
   one folded in.
5. **The selection off the wire, the filter as ids.** One PR on both sides, since the client
   reads both fields: `Selected` becomes a client state field in `App.fs`, and the order-plan
   page's dialog, row selection and row filter read it from the env and send ids;
   `Filtered: string[]`; the server's following bookkeeping deleted. About 130 lines. Tests:
   totals by filtered context id; a re-evaluated context keeps its place in the filter.
6. **The views on the new commands.** `LoadCart` and the patient change send `Open`; Prescribe
   sends `AddOrder` and switches page, the button greyed on a duplicate; the order-plan dialog
   by context id, deletion by `RemoveContexts`; the nutrition page on `RemoveContexts`;
   `ShowOrderPlan` retired for `Select` and `Filter` on `IOrderPlan`. About 150 lines. Acceptance
   below.
7. **Delete the old cases** (`refactor`): `Navigate` by id only, `RemoveContext`,
   `RemoveOrders`, the `None` branch and `removeOrders`; the nutrition page's `Some ncId` becomes
   `ncId`. About 120 lines. Tests: the dispatch case.
8. **`PlanMachine.fs` and its tests**, not wired. About 190 lines. Tests: the lifecycle; a stale
   answer dropped by request id; a patient change while in flight; `Cart` to `Open`; a refused
   change restores the plan; `Select` and `Filter` without effects.
9. **Wire the plan machine.** `State.Plan`, `interpretPlanEffect`, `Plan.toDeferred`; the plan
   handlers and helpers deleted. About 200 lines, deletion heavy. Acceptance below.
10. **`ContextMachine.fs` and its tests**, not wired. About 190 lines. Tests: a seed before a
    patient; the patient change; the sync effects on an update; the no-rules reset; a stale
    answer.
11. **Wire the context machine.** `State.Context`, `interpretContextEffect`; `handleOrderContext`
    and `LoadOrderContextResult` deleted; the prescribing page's Elmish deps on the projection.
    About 180 lines. Acceptance below.
12. **Docs.** `docs/domain/core-domain.md`'s API paragraph, which still names `processCommand`
    and `OrderContextCmd`, and glossary rows for the plan context and the category; plan 654's
    "Left open" closed; #666 closed. About 60 lines.

If step 9 or 11 exceeds 200 lines, the deletion of the dead handlers becomes a following
`refactor` PR. Optional after 12: the views read `Plan` and `Context` directly and
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
- Sign, then reload: the cart opens on the version with a context per order, drug and
  nutrition, each on its page; a reopened order steps after its first `Navigate`.
- Edit the patient's weight, step a nutrition order: the dose follows the new weight.
- Two browsers on one patient: a reopen arriving while a step is in flight drops the step's
  answer; the plan shown is the reopened one.
- Prescribe with a medication in the url and no patient: the workbench shows; setting the
  patient evaluates it.
- `dotnet run ServerTests`, the Fable compile and Fantomas stay green after every step.

## As built

| Step | PR | Landed |
|---|---|---|
| plan | this PR | this document; the pointer in plan 654 |
