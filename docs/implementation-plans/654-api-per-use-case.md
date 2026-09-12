# Implementation plan for issue 654

## Problem description

`src/Informedica.GenPRES.Shared/Api.fs` carries two wire shapes. The newer one
(`processLaunch`, `processSession`, `processSigning`) gives every use case its own
`[<RequireQualifiedAccess>]` command type, its own response type, its own `toString` and a
member whose signature says how it is authenticated. The older one, `processCommand`, funnels
seven families through one `Command` DU chained with `and`, one mirror `Response` DU and the
`Request { Opened; Command }` / `Reply { Response; Notice }` envelope of plan
[635](635-session-bound-compute.md). Issue
[#654](https://github.com/informedica/GenPRES/issues/654) lists what that costs:

- **Untyped correlation.** The client's `processResponse` (`App.fs`) dispatches on the response
  family only and discards the command it sent; a wrong-family answer would land silently in
  the wrong state slice. Nothing in the types says `FormularyCmd` is answered by `FormularyResp`.
- **Invisible authentication.** Four commands bypass `requireLoaded` and three need the HMAC
  admin token (`ServerApi.Command.fs`); `ReloadResources of password` hides inside the clinical
  `OrderContextCommand` and is password-checked in `OrderContextService.evaluate`, the D4
  follow-up of the [security review](../security/2026-04-10-security-review.md). Every admin
  request also pays for the Session `seen`/notice round trip it cannot use.
- **A failed load cannot be retried.** `ReloadResources` rides `OrderContextCmd`, which sits
  behind `requireLoaded`: when the initial formulary load failed, the one admin action meant to
  retry it is refused.
- **Two plans on the wire for one plan in the domain.** `OrderPlan` and `NutritionPlan` are both
  a patient, orders and totals; the nutrition contexts are workbenches that produce orders by
  category. Their command families say the same thing (evaluate an order-context command and
  fold the result into the plan, or recompute the totals); two nutrition cases are never sent
  by the client; `UpdateOrderPlan(_, None)` and `FilterOrderPlan` run the same server code. And
  signing hashes and stores `plan.Scenarios` only, so **nutrition orders are never signed**,
  while nutrition totals are computed apart and sum every candidate scenario of a context,
  narrowed or not. The domain rule is one plan: the nutrition plan is a subset of the order
  plan, and only the order plan is signed, nutrition included.
- Dead code the compiler demands: an unreachable `| _ -> Error "Unexpected command after
  requireLoaded"` arm and a 45-arm `Command.toString`.

## Approaches considered

1. **Split only at the authentication boundary**: `processCompute` over one `ComputeCommand`
   for the clinical families plus `processAdmin`. Gets D4 and the failed-load retry with the
   least churn. Rejected: keeps the untyped correlation and names an intermediate that is a
   rename of today's shape.
2. **Keep one `processCommand` and reshape the DUs** (qualified access per family, an admin
   family, no `and` chain). Rejected: fixes nothing the client sees; admin calls still ride the
   Session envelope.
3. **One member per use case** on a generic envelope, the two plan families merged into one
   generic `PlanCommand<'plan>` with two typed members. Rejected on review: there is one plan.
4. **One member per use case, one plan.** `NutritionPlan` folds into `OrderPlan`, one
   `PlanCommand`, one `processOrderPlan`. Chosen.

Plan 635's rejection of "a second member for computing inside a Session" is not violated:
every computing member stays cookie-optional and the server still decides from the cookie.

## Chosen approach

### Wire (`Shared/Api.fs`, `Shared/Types.fs`)

```fsharp
type Request<'cmd> = { Opened: OpenedToken option; Command: 'cmd }
type Reply<'resp> = { Response: 'resp; Notice: RecordNotice option }

[<RequireQualifiedAccess>]
type OrderContextCommand = (* today's cases minus ReloadResources *)

type OrderPlan =
    {
        Patient: Patient
        Selected: OrderScenario option
        Filtered: OrderScenario[]
        Scenarios: OrderScenario[]                 // every order, nutrition included: what is signed
        NutritionContexts: NutritionContext[]      // the workbenches, one per category added
        Totals: Totals
    }

[<RequireQualifiedAccess>]
type PlanCommand =
    | Recalculate of OrderPlan                     // was FilterOrderPlan, UpdateOrderPlan(_, None)
    | Navigate of OrderPlan * contextId: string option * OrderContextCommand * OrderContext
    | AddContext of OrderPlan * NutritionCategory
    | RemoveContext of OrderPlan * contextId: string

[<RequireQualifiedAccess>]
type InteractionCommand = CheckInteractions of string list | GetDrugNames

[<RequireQualifiedAccess>]
type InteractionResponse = InteractionsChecked of DrugInteraction[] | DrugNamesLoaded of string[]

[<RequireQualifiedAccess>]
type AdminCommand =
    | ValidatePassword of password: string
    | ListLogFiles of token: string
    | AnalyzeLogFile of token: string * fileName: string
    | ReloadResources of token: string

[<RequireQualifiedAccess>]
type AdminResponse =
    | PasswordValidated of isValid: bool * token: string
    | LogFilesListed of LogFileInfo[]
    | LogFileAnalyzed of string
    | ResourcesReloaded

type IServerApi =
    {
        processOrderContext: Request<OrderContextCommand * OrderContext> -> Async<Result<Reply<OrderContext>, string[]>>
        processOrderPlan: Request<PlanCommand> -> Async<Result<Reply<OrderPlan>, string[]>>
        processFormulary: Request<Formulary> -> Async<Result<Reply<Formulary>, string[]>>
        processParenteralia: Request<Parenteralia> -> Async<Result<Reply<Parenteralia>, string[]>>
        processInteraction: Request<InteractionCommand> -> Async<Result<Reply<InteractionResponse>, string[]>>
        processAdmin: AdminCommand -> Async<Result<AdminResponse, string[]>>
        // processLaunch, processSession, processSigning, getSettings, testApi unchanged
    }
```

Single-case response wrappers go. Each command module keeps a `toString` for the log in the
style of `SessionCommand.toString`: never a token, a password or a plan. `Command`, `Response`,
`Command.toString`, `processCommand`, `OrderPlanCommand`, `NutritionPlanCommand` and the
`NutritionPlan` type are deleted at the end.

Plan cases map one to one: `UpdateOrderPlan(tp, Some(cmd, ctx))` → `Navigate(tp, None, cmd,
ctx)`; `UpdateOrderPlan(tp, None)` and `FilterOrderPlan tp` → `Recalculate tp`;
`NavigateNutritionOrderContext(plan, id, cmd, ctx)` → `Navigate(plan, Some id, cmd, ctx)`;
`AddNutritionContext` → `AddContext`; `RemoveNutritionContext` → `RemoveContext`;
`InitNutritionPlan` dropped (the plan exists once a patient is set); `UpdateNutritionOrderContext`
and `SelectNutritionOrderScenario` dropped (dead on the wire).

**The rule that makes one signature cover nutrition.** A nutrition context whose order context
is narrowed to exactly one scenario has that scenario in `plan.Scenarios`, upserted by order id
(`OrderScenario.eqs`, the rule `updateOrderPlan` already applies); a context with several
candidates contributes nothing yet; `RemoveContext` removes the context's scenario with it.
`Totals` is computed once over `Scenarios`, respecting `Filtered`. Signing is untouched in
shape: `RequestSignChallenge` takes the plan and hashes `Scenarios`, which now hold the
nutrition orders.

Generic records on the wire are new to this repo but supported: both sides serialize JSON,
Fable.SimpleJson reflects a closed generic record with the argument substituted into its
fields, and Fable.Remoting's own integration tests echo a `GenericRecord<'t>` as argument and
return. The JSON body of `Request<X>` is byte-identical to today's `Request`; only the route
changes. Should the browser surprise, the fallback is curried members
`OpenedToken option -> 'cmd -> Async<Result<Reply<'resp>, string[]>>` with only `Reply` generic.

### Server

One file per member, the convention [#652](https://github.com/informedica/GenPRES/pull/652) set
(`module <Family>Command`, `let processCmd (env: AppEnv) ... cmd`), each added to the fsproj
after `ServerApi.Adapters.fs` and to both `Scripts/load.fsx` loaders:

- `ServerApi.Compute.fs`: `bound env cookie name gate handler request` owns, for every
  computing member, what `compose` does for `processCommand` today: log start, `cookie.read` →
  `env.session.seen id request.Opened`, the gate (`Gate.RequiresLoaded` → `env.requireLoaded ()`,
  `Gate.Open` → nothing), the handler, log finish with the notice kind, exception →
  `Error [| ex.Message |]`. The gate is per command (`gate: 'cmd -> Gate`) because
  `GetDrugNames` bypasses `requireLoaded` and `CheckInteractions` does not; it is written in
  `compose` where it is visible. The scope check of plan [580](580-scope-switch.md) becomes one
  more parameter here. `logged name f` wraps the launch, session, signing and admin members.
- `ServerApi.OrderContextCommand.fs`, `FormularyCommand.fs` (formulary and parenteralia),
  `InteractionCommand.fs`, `PlanCommand.fs`, `AdminCommand.fs`: each `processCmd` is the arm it
  is in `Command.processCmd` today. `AdminCommand.processCmd` takes over
  `validatePassword`/`generateToken`/`validateToken` unchanged and adds `ReloadResources token`
  → `validateToken` → `env.admin.reloadResources ()`, not behind `requireLoaded`.
- `ServerApi.Ports.fs`: `LogAnalyzerPort` becomes `AdminPort` with `reloadResources: unit ->
  Async<Result<unit, string[]>>` (the adapter calls `Api.reloadCache logger provider`, what
  `GenOrderContext.reloadResources` does today); `OrderPlanPort` and `NutritionPlanPort` become
  one `PlanPort` (`recalculate`, `navigate`, `addContext`, `removeContext`, every member
  answering `Async<Result<OrderPlan, string[]>>`).
- `ServerApi.Services.fs`: the `ReloadResources` arm and its password guard go from
  `OrderContextService.evaluate`; `OrderPlanService` and `NutritionPlanService` become one
  `PlanService` (`navigate None` is `updateOrderPlan`, `navigate (Some id)` is
  `navigateNutritionOrderContext` plus the upsert, `addContext` and `removeContext` are the
  nutrition functions plus the scenario bookkeeping, one `calculateTotals`). The dose-rule sets
  and `discoverFilterOptions` stay.
- `ServerApi.CompositionRoot.fs`: one line per member,
  `processFormulary = Compute.bound env cookie FormularyCommand.toString (fun _ -> Gate.RequiresLoaded) (FormularyCommand.processCmd env)`.

### Client (direct edits)

- `createApiMsg call opened msg cmd` takes the member as its first argument;
  `Answer<'r>`, `ApiResponse<'r>`; `processApiMsg` keeps the notice logic and the stale-token
  guard verbatim and applies a typed `apply: State -> 'r -> State * Cmd<Msg>` per family. Admin
  messages go through a `createAdminMsg` that does not enter `processApiMsg` (no envelope, no
  notice).
- `OrderPlanMsg of PlanCommand`, a client-only `ShowOrderPlan of OrderPlan` for the page
  navigation `UpdateOrderPlan(tp, None)` does today; `State.NutritionPlan`, `NutritionPlanMsg`,
  `LoadNutritionPlanResult` and `AppEnv.INutritionPlan` go; `Views/Nutrition.fs` reads
  `IOrderPlan`, renders `plan.NutritionContexts` and `plan.Totals`, and sends `Navigate(plan,
  Some ncId, ...)`, `AddContext`, `RemoveContext`. The order plan's `InProgress`/`Recalculating`
  guards then cover the nutrition page too.
- Reload under the token: `IResources.ReloadResources: unit -> unit`; the settings page is only
  reachable authenticated, so its password dialog goes; on `ResourcesReloaded` the client
  dispatches `UpdateOrderContext`, which already reloads formulary and parenteralia; a token
  error logs out.

### Consequences, stated

- Nutrition totals stop counting unnarrowed candidates: the correction of the subset model.
- After a reopen, `LoadCart` rebuilds the plan from the signed `Scenarios`, so the nutrition
  orders are in the plan and its totals but the workbenches are empty. Rebuilding
  `NutritionContexts` from the signed orders' categories is a follow-up issue.
- Cross-cutting concerns hold by discipline: every computing member must be built through
  `bound`. A table test over `compose` asserts, for every member, that a Session's notice
  arrives.
- Six routes instead of one. Nothing server-side keys on the member name; a tab on the old
  bundle gets a 404 on `processCommand` until it reloads, which `index.html`'s `no-cache` makes
  a plain reload.
- Plan 580's `Feature.ofCommand` becomes a `Feature` constant per member; plan
  [582](582-server-knowledge-rules.md)'s `KnowledgeCmd` becomes a `processKnowledge` member.

## Confidence

High for the server and the envelope: the session families are the pattern, the composition
root already reads the cookie, Fable.Remoting's generic support is verified. Medium for the one
plan: the upsert rule and the totals change a dosing path and must be seen in the browser and
in the signed version, which is why that step carries its own tests and acceptance.

## Steps

Each step is one PR against `master`; Shared and Server code is drafted in
`Shared/Scripts/Api.fsx` and `Server/Scripts/Compute.fsx` (rewritten per step) and migrated by
the maintainer; the client is edited directly. The admin family goes first: it carries the
security and recovery payoff and is a coherent resting point. `processCommand` then shrinks one
family per PR until it is deleted; it is never renamed to an intermediate.

1. **This plan.** Plus one line each in plans 580 and 582.
2. **Admin family, server.** `AdminCommand`/`AdminResponse`/`processAdmin` alongside
   `processCommand`; `AdminPort` with `reloadResources`; `ServerApi.AdminCommand.fs`. Tests
   (none exist today for the token paths): `ValidatePassword` right/wrong/empty setting;
   `ListLogFiles`/`AnalyzeLogFile`/`ReloadResources` with a valid, a forged and an expired
   token; reload invoking the port once; reload succeeding while `requireLoaded` reports not
   loaded.
3. **Admin family, client.** Login, log list, log analysis and reload through `processAdmin`;
   the settings page without the password dialog.
4. **Delete the old admin paths.** `LogAnalyzerCmd`, `OrderContextCommand.ReloadResources`, the
   password guard, the bypass arms; the remaining match made total; `SECURITY.md` and a status
   note on the D4 row of the security review. D4 closed.
5. **The envelope and `bound`, no visible change.** `Request<'cmd>`/`Reply<'resp>` with
   abbreviations so the client compiles unchanged; `ServerApi.Compute.fs`; `processCommand`
   rebuilt on `bound`; `logged` for the session members. Tests: the existing composition cases
   on `bound`, plus a JSON round trip of `Result<Reply<Formulary>, string[]>` through
   `FableJsonConverter`.
6. **Formulary and parenteralia**, the smallest families: proves the generic envelope in the
   browser. `HttpTests` names a real route.
7. **Interaction**: introduces the per-command gate.
8. **The one plan**, in three PRs: (a) server and shared: `OrderPlan.NutritionContexts`,
   `PlanCommand`, `PlanPort`, `PlanService`, `processOrderPlan`, the old plan arms re-implemented
   over the new service so both wires answer the same plan; tests for the four cases, the
   upsert rule, `RemoveContext` cascading, totals over `Scenarios`, and the signing challenge
   hashing a nutrition order; (b) client: the nutrition page on `IOrderPlan`, `ShowOrderPlan`;
   (c) delete `OrderPlanCmd`, `NutritionPlanCmd`, `NutritionPlanPort`, the `NutritionPlan` type,
   the nutrition service functions; `TotalsTests` retargeted; the domain doc's one plan family.
9. **Order context; delete the old shape.** `processOrderContext`; then `processCommand`,
   `Command`, `Response`, `ServerApi.Command.fs`, the abbreviations; the domain doc and
   `DEVELOPMENT.md` "Watch the wire".
10. **Qualified access** (`refactor`): `[<RequireQualifiedAccess>]` on `OrderContextCommand`;
    the widest diff and no behaviour change, so last and optional.

Plan 580 is built after this lands, on a per-member `Feature`.

## Acceptance

Against `GENPRES_PROD=0 dotnet run`:

- Prescribe, step a dose, open the order plan: the Network tab shows
  `/api/IServerApi/processOrderContext` and `processOrderPlan` with `{ Opened; Command }` in
  and `{ Response; Notice }` out.
- Nutrition: add TPN and enteral feeding, narrow a context to one scenario; the order plan lists
  the nutrition order next to the drugs, the totals include it, the signed version holds it.
  Remove the feeding: its supplement and both orders go.
- Settings: log in, list and analyze log files, reload resources without a password prompt; a
  stale token answers an error and logs out. With a wrong `GENPRES_URL_ID`, the reload reaches
  the port.
- Two browsers on one patient: the newer-version notice still arrives on the next computing
  request.
- `GENPRES_PROD=1`: every reply's notice empty, admin still gated by the password.
- `dotnet run ServerTests`, the Fable compile and Fantomas stay green after every step.

## As built

Script-first for the Shared and Server code (`Shared/Scripts/Api.fsx`, `Server/Scripts/Compute.fsx`,
rewritten per step), the client edited directly, every step one PR against `master`, migrated
after review.

| Step | PR | Landed |
|---|---|---|
| plan | #655 | this document; notes in plans 580 and 582 |
| 2, admin server | #656 | `AdminCommand`/`AdminResponse`/`processAdmin`, `AdminPort` with the secret and the clock as values, `ServerApi.AdminCommand.fs`; review: a reload that leaves the provider unloaded answers its messages |
| 3, admin client | #657 | login, logs and reload on `processAdmin`, no password dialog; review: answers correlated by login attempt and by token, the reload pending until its refresh answered |
| 4, delete old admin | #658 | `LogAnalyzerCmd` and `ReloadResources of password` gone, the dispatcher total; D4 closed in `SECURITY.md` and the review |
| 5, envelope and bound | #659 | `Request<'cmd>`/`Reply<'resp>`, `ServerApi.Compute.fs` (`bound`, `logged`, `Gate`), `Command.gate`; review: an open command never asks the provider |
| 6, formulary and parenteralia | #660 | the first members; `createApiMsg` over the member, `Answer<'r>`, `apply` per family |
| 7, interaction | #661 | `processInteraction` with the per-command gate |
| 8a, the one plan, server | #662 | `OrderPlan.NutritionContexts`, `PlanCommand`, `PlanPort`, `PlanService`, `processOrderPlan`; review: the filter and the selection follow a replaced order, the totals count by order id, a failed evaluation is the answer |
| 8b, the one plan, client | #663 | the nutrition page on the order plan, `ShowOrderPlan`; review: a refused change leaves the plan the request was sent over |
| 8c, delete old plan families | #664 | `OrderPlanCmd`, `NutritionPlanCmd`, `NutritionPlan` and their ports and services gone; the totals `PlanService`'s own |
| 9, order context; old shape gone | this PR | `processOrderContext`; `processCommand`, `Command`, `Response`, `ServerApi.Command.fs` deleted; `PlanCommand.RemoveOrders` |

### Deviations from the text above

- **`PlanCommand.RemoveOrders`.** Deleting orders on the order-plan page was a client-side
  filter over `Scenarios`; with nutrition orders in the plan, a deleted nutrition order came back
  at its workbench's next move. The delete is a server command: the orders named go by id, each
  with the workbench that contributed it, a feeding with its supplements.
- **`PlanCommand.Navigate` without a context evaluates itself.** The plan text reused
  `updateOrderPlan`, which answered the plan as it was when the evaluation failed (review of #662).
- **`Init` dropped from `PlanCommand`.** The plan exists once a patient is set.
- **The recalculation is a seam.** `PlanService.navigate` and `addContext` take the
  recalculation as a function the adapter fills with the provider's totals, so the rules are
  testable over reflection-built scenarios the solver cannot price.
- **Qualified access on `OrderContextCommand`** (step 10) is not done; the names are unique and
  the churn is not earned.

### Left open

- After a reopen the signed nutrition orders are in the plan and its totals, but the workbenches
  are empty. Rebuilding them from the signed orders is a follow-up issue.
- A drug order's context is not kept in the plan (its scenario is; the page rebuilds a context
  around it), a nutrition order's is (its workbench). Keeping a context per order, with the
  prescribing workbench as a context not yet in the plan, is the natural next design step and
  needs an issue and a plan of its own.
- Plan 580's scope gate becomes one more parameter of `Compute.bound`.
