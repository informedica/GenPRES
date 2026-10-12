# Implementation plan for issue #1453: the server decides what a clear recalculates

2026-10-13. The client's pick list goes. A clear of an order value is one command that names
what it clears; the server picks the recipe from that command, recalculates what depends on the
cleared field, and marks on every order variable it sends whether it can be cleared. It closes
the issues #1453, #1276 and #1343 and refs #1034; the plan for the open TPN issues builds on
it. Line numbers are from `master` at `ce3c164f`.

## Problem description

A field with one value reopens by its arrow: the client clears the value and shows the list the
server answers. Which fields get the arrow, and what the server does with the clear, both read
the client's pick list: the variables the user picked or stepped in the dialog, in the order
picked. The list lives in the drug workbench's dialog alone. The plan dialog and the nutrition
slot have none, so there every field with one value reopens, and a clear of a value another
variable pins comes back as it was. A value the server sets by itself, as the composed TPN will
set every component, the dose and the rate, would read as a pick in the workbench and as
unknown everywhere else. The list also makes the server's `Reopen` carry a list of names across
the wire for one request.

The maintainer decided on 2026-10-12 and 2026-10-13: the pick list disappears from the client;
the server's cleared order process decides which field can be recalculated and gains a case for
every field the pages can clear; the recipe is picked from the clear command itself; the mark is
derived when the order is sent, from the same clear commands, never stored; the recipes share
their lists with the step functions.

## What the code does today

**The pick list.** `Views/Order.fs` keeps `picks: string[] option` in React state (`Order.fs:495`),
`Some [||]` for the workbench and `None` for the plan dialog (`Order.fs:489`). After an answer,
`PickListPolicy.picked` adds every variable the change narrowed to one value
(`PickListPolicy.fs`); a step adds its variable; a reopen keeps the picks made before the cleared
one (`beforeReopen`) and holds the rest for a restore (`heldPicks`, `Order.fs:972`). The nutrition
slot sends every clear with an empty list (`NutritionSlot.fs:618`).

**The arrow.** `FieldOpenPolicy.orderVariable` gives a field with one value the arrow when the
user constrained it or the order does not say, and no arrow when the solver determined it
(`FieldOpenPolicy.fs`); `constrained` is read from the pick list (`PickListPolicy.constrained`).
With no list every variable reads unknown, so the plan dialog and the slot show the arrow on
every solved field.

**The wire.** The four clear commands carry the picks: `ClearScheduleProperty`,
`ClearOrderableProperty`, `ClearComponentProperty`, `ClearItemProperty` (`Api.fs:59`), and
`OrderViewCommand.toString` prints their count (`Api.fs:142`). The server mapper puts the order id
back on each name and builds `Domain.ClearOrderValue(ctx, change, picks)`
(`ServerApi.Mappers.OrderContext.fs:395`), where `change` is one of the sixteen clear cases of
`ChangePropertyCommand` (`Types.fs:363`). `Ctx.clear` in Shared (`Models.fs:1730`) and
`Api.preview` (`Api.fs:170`) take the picks too, for the optimistic preview.

**The clear command.** `processChangeProperty` handles each clear case by emptying the one
variable (`OrderProcessor.fs:314`, `Quantity.clear` and the like, which makes it unrestricted). It
does nothing else: the command knows exactly which variable it clears, and that knowledge is
dropped on the spot.

**The server reopen.** `ClearOrderValue` (`OrderContext.fs:372`) applies the clear through
`changeOrder` and evaluates as `ReopenOrderScenario(ctx, picks)` (`OrderContext.fs:1513`), which
runs the processor's `Reopen(ord, picks)` (`OrderProcessor.fs:877`; the other user is
`reopenScenarioOrder`, `OrderContext.fs:1232`): `clearedPick` (`OrderProcessor.fs:528`) finds the
cleared variable in the picks, `reCalcSteps` resets the order, the picks made before it are put
back and `solveSteps` runs. Without picks, or with a cleared variable that is no pick, it is a
plain `SolveOrder`. #1425 added `rateNotAPick` here, so a cleared median rate gets its list back.

**The cleared order process.** `SolveOrder` on a cleared order runs `processClearedStep`
(`OrderProcessor.fs:866`) when the order state is `DoseSolvedAndCleared` (`OrderProcessor.fs:504`,
`Order.isCleared` true when any variable is unrestricted, `Order.fs:3930`); that is
`processClearedOrderWith` (`OrderProcessor.fs:430`), which has to find out which variable is
empty: a match on the schedule and on the pattern
`(|FrequencyCleared|RateCleared|TimeCleared|ConcentrationCleared|DoseQuantityCleared|DosePerTimeCleared|NotCleared|)`
(`OrderProcessor.fs:29`), a tuple of six predicates of which exactly one may hold. Each case, a
recipe, sets the variables that depend on the cleared one back to their constraints and solves
with the values held back. Recipes today: `processClearedRate` for the rate and the time of a
continuous, timed or once timed order, `processClearedDose` for the dose quantity and the dose
per time of a once, discontinuous, timed or once timed order; it resets the preparation only
when there is one component or the preparation is outside its constraints.
`processClearedFrequency` exists (`OrderProcessor.fs:337`): it sets the frequency and the doses
per time to any positive value and then the frequency to its standard values; it is not wired.
The dose predicates read the orderable dose alone (`Orderable.isDoseQuantityCleared`,
`Order.fs:2128`), so a cleared component or item dose is not seen. A cleared component orderable
quantity, item dose, dose count or concentration falls through to `logUnmatched` and a plain
solve, which gives the same one value back: #1276. The comment at `OrderProcessor.fs:23` calls
two arms defensive and says a picked frequency reopens by `Reopen`.

**`isCleared` has a second reader.** `SolveOrder` chooses `rateStage` over `rateStageWithMedian`
when the order is cleared (`OrderProcessor.fs:871`): decision 6 of #1425, a cleared rate gives
its list back without the median.

**The dependents of a field are already listed elsewhere.** The step function of a component
quantity, `orderPropertyIncrOrDecrComponentOrderableQuantity` (`OrderProcessor.fs:179`), sets the
orderable quantity, the component's count and concentration, its doses and item quantities, the
orderable dose and count to any positive value before it steps. That is the list a clear of the
same field needs, with constraints applied instead of any value. The dose quantity and rate step
functions (`OrderProcessor.fs:67`, `84`) hold the lists the dose and rate recipes already have.

**The wire order and its targets.** The server mapper maps the domain Dto to the wire order
(`mapFromOrderToShared sns dto`, `ServerApi.Mappers.Order.fs:344`; the typed order is gone by
then, and the same Dto is what the session store serializes, structure version 6; the rule for
the store is never to store what can be derived from the stored rows). `Schedule.Dto` carries the
schedule as five flags. Shared addresses the fields a page can clear as `Target`s
(`Models.fs:1410`): the schedule's frequency and time, the orderable's quantity, dose quantity
and dose rate, a component's orderable quantity and dose per kg, an item's nine; `Target.tryGet`
reads the variable of one and `Target.map` writes it (`Models.fs:1461`, `1484`). The mapper's
`propertyCommands` gives each target its clear command. The processor already names the five
schedules as `PrescriptionKind` (`OrderProcessor.fs:540`).

**How it came to be.** Until #1207 (2026-09-29) there was no pick list: the cross cleared one
field and the server ran the cleared order process. The client did not read what the process
could do: every order field offered the cross (`clearable = true` in `ViewHelpers`), so a clear
the server had no case for did nothing visible. #1034 answered that on the client with the
arrow and a record of the user's picks, kept on the order scenario by #1207 and moved to the
dialog by #1252. The knowledge of which field a clear recalculates stayed in the server's match
arms the whole time, unread.

**The restore.** A reopen's list closed without a pick puts the state before the clear back,
locally: both machines keep it in `Kept` and `RestoreField` puts it back
(`OrderPlanMachine.fs:389`, `OrderContextMachine.fs:394`). That is the #1034 rule, and it does
not read the picks.

## Decisions

Taken by the maintainer on 2026-10-12 and 2026-10-13:

| Question | Decision |
|---|---|
| The pick list | Disappears from the client. |
| A cleared field the process has no case for | The cleared order process gains a case per field the pages can clear, each clearing what depends on it. #1276 closes with it. |
| The wire and the server reopen | Dropped: the picks leave the four clear commands, `Reopen` with `clearedPick` and the put-back leave the processor, `ReopenOrderScenario` leaves the order context. |
| How the server picks the recipe | From the clear command itself, which names the field and its component or item. The scan of the order for an emptied variable, the tuple pattern, the `DoseSolvedAndCleared` state and the `process-cleared` step of `SolveOrder` go. |
| Who says which field can be cleared | The server marks each order variable it sends; the field reads the mark. |
| What the mark means | The fields the server will recalculate: the variable holds one value and a recipe exists for its kind under the order's schedule. It does not run the clear: a field another value pins is marked and comes back with a list of one, which the field shows as any list of one. The threshold below narrows it. |
| Where the mark is computed | Derived when the order is sent, never stored: after the wire order is mapped, the server mapper folds over the order's targets and sets the mark of each from its clear command, so the marked places and the places a page can clear are one set by construction. GenORDER exposes one pure function, over the schedule kind and the clear command. The stored JSON and its structure version 6 do not move. `Target` is a Shared type, so the fold lives in the server, which the dependency rule allows and GenORDER may not. |
| The recipes | The lists of dependent variables are shared with the step functions: one function per field returns the list with the operation as a parameter, and the step and the clear both call it. The dose and rate recipes keep theirs. |
| Where | Its own plan, this one, before the TPN steps. |

Proposed, to confirm:

| Question | Proposal |
|---|---|
| The trade the plan makes | The pick list answered "keep what the user set, recompute the rest" for any field, because it knew what the user set. The recipes answer "for a clear of this kind, this moves and this stays", per field kind, without knowing who set what. That is predictable for a prescriber and a domain rule rather than a dialog's memory; it is less general, since every clearable field needs a recipe, and a recipe one entry short leaves the cleared field pinned. The probe of step 1 measures the pinned fields before any recipe is written. |
| A field kind that always comes back pinned | The probe counts, per fixture and kind, the clears that come back with one value. A kind pinned on every fixture of its schedule gets no recipe and no mark, instead of an arrow that opens a list of one. A kind pinned on some fixtures keeps its recipe; the list of one is the answer there. |
| The rate stages | `ClearValue` always runs `rateStage`, the list without the median: decision 6 of #1425 by construction. `SolveOrder` always runs `rateStageWithMedian`. The probe counts the solved orders that reach `SolveOrder` with `isCleared` true when no clear was sent; the local order logs of this week hold two "no match for … cleared" warnings in all, both during a TPN test, so today's solves do not reach the cleared step by accident, but the count makes it a measurement. |
| The item doses | No new recipe: the dose kinds of an item, plain and per kg, run through `processClearedDose`, so a concentration the user picked stays. |
| The component dose per kg | Its own kind, `ComponentQuantity`: a clear of a component's mL/kg has to recalculate the component's orderable quantity and the totals, which `processClearedDose` holds while the preparation is within its constraints, as a TPN's is. The probe clears it on tpn under both recipes. |
| The restore | Stays, as #1034 decided. It never read the picks. |
| The second path for a reopen (#1343) | Folded in: the machines read a reopen from the command itself, the four clear commands, so `ReopenField` and the `reopen` callbacks go with the picks that were their reason. The `reopening` ref in the views goes too: the arrow dispatches the clear command itself instead of raising a flag a later empty selection reads. |
| The dose count | Not clearable: no page shows it. |
| A clear that fails to solve | The recipe's failure falls back to a solve of the order as it was, as `processClearedStep` does today; the field then shows its value again. |

## Approaches considered

1. **Keep the pick list, leave the server-set values out of it.** The client would have to know
   which answers set values by themselves, per command, and the plan dialog and the slot would
   still have no list. Rejected.
2. **Move the pick list to the server**, on the order scenario, as before #1252. Rejected then,
   rejected now: an order scenario does not carry what the user did in a dialog.
3. **No pick list; the server detects the emptied variable and picks the recipe**, today's shape
   with the pattern written as a function. Rejected: the clear command already says what it
   cleared, so the detection, its "exactly one cleared" invariant and its fallbacks are not needed.
4. **The mark stored on the domain Dto.** Simplest to compute, since `toDto` knows each field,
   but the Dto is the stored shape, and the rule is never to store what can be derived; the flag
   would also go stale when a recipe or the threshold changes. Rejected.
5. **The mark from a pass over the Dto's fields.** Derived, not stored, but it gives a kind to
   every field, among them fields no clear command reaches, the orderable dose per kg, the dose
   per time, a component's plain dose, so the arrow would have nothing to send; and it is a
   second reader of kinds to keep in step with the commands. Rejected.
6. **No pick list; the clear command picks the recipe; the mark from the same commands at send
   time.** A clear
   empties the field and runs the recipe its kind names: the dependents back to their
   constraints, the rest held, the order solved. The user's earlier picks stay because they are
   values the recipe holds, not because a list says so; so do values the solver set, which the
   reopen recalculated and the recipes do not. Chosen, with the trade stated in the decisions.

## Chosen approach

### The kind of a clear and its recipe, GenORDER

`ClearKind`, a union of eight: `Frequency`, `Time`, `Rate`, `DoseQuantity`, `DosePerTime`,
`OrderableQuantity`, `ComponentQuantity`, `Concentration`. The sixteen clear commands map to it
by `kindOf: ChangePropertyCommand -> (ClearKind * target) option`, None for every other command:

| Clear command | Kind |
|---|---|
| `ClearScheduleFrequency` | `Frequency` |
| `ClearScheduleTime` | `Time` |
| `ClearOrderableQuantity` | `OrderableQuantity` |
| `ClearOrderableDoseQuantity` | `DoseQuantity` |
| `ClearOrderableDoseRate` | `Rate` |
| `ClearComponentOrderableQuantity cmp` | `ComponentQuantity`, target cmp |
| `ClearComponentDoseQuantityAdjust cmp` | `ComponentQuantity`, target cmp |
| `ClearItemDoseQuantity`, `ClearItemDoseQuantityAdjust` | `DoseQuantity` |
| `ClearItemDosePerTime`, `ClearItemDosePerTimeAdjust` | `DosePerTime` |
| `ClearItemDoseRate`, `ClearItemDoseRateAdjust` | `Rate` |
| `ClearItemComponentConcentration`, `ClearItemOrderableConcentration`, `ClearItemOrderableQuantity` | `Concentration`, target cmp and itm |

`recipe: PrescriptionKind -> ClearKind -> (target -> Order -> Result) option`: the function that
recalculates after a clear of that kind, None where the schedule has none, the rate of a once or
discontinuous order, the frequency of a once or continuous order. The schedule kind is all it
reads to decide, so it takes the kind, not the schedule; `PrescriptionKind.ofSchedule`, the match
`classify` already makes, and `PrescriptionKind.ofDto`, from the Dto's five flags, sit next to it
in GenORDER, so the process and the mapper read the kind the same way. Whether a recipe exists is
decided without a target; the target only reaches the recipe that runs. `clearable kind
command` is `recipe kind (kindOf command)` is some. The process runs the recipe; the mark asks
whether it exists. One function decides both.

### The pipeline

`OrderCommand.ClearValue of Order * ChangePropertyCommand` replaces `Reopen`: the clear applied
by `processChangeProperty`, the recipe of `kindOf` run with its target, then `solveSteps` with
`rateStage`. `ClearOrderValue(ctx, change)` evaluates it through `processScenarioOrder`.
`SolveOrder` loses its `process-cleared` step and its branch on `isCleared`, and always runs
`rateStageWithMedian`; `classify` loses `IsCleared`, the state pattern its two cleared arms, and
`processClearedOrderWith` its match: it becomes the recipes, each a function, and
`OrderReopenTests.fs` becomes `OrderClearTests.fs` over `ClearValue`. The tuple pattern, its
comment, `clearedPick`, the put-back step and `rateNotAPick` go.

### The recipes and the shared lists

Each recipe sets the variables that depend on the cleared one back to their constraints and
holds the rest; the schedule's two stages of values stay as they are, the rate held back where
the schedule asks. The lists of dependents come from the step functions: the list in
`orderPropertyIncrOrDecrComponentOrderableQuantity` becomes `componentQuantityDependents cmp op`,
called by the step with the any-value operation and by the clear with the constraints operation;
the orderable quantity and the concentration get their lists the same way, written once and
used by their step when one exists. The dose and rate recipes stay as they are. The table is the
hypothesis; the probe of step 1 decides the columns, since every variable an equation ties to the
cleared one must be in one of them, and one held by mistake pins the cleared field.

| Kind | Recipe | Set back to the constraints | Held |
|---|---|---|---|
| `Rate`, `Time` | `processClearedRate`, as today | | |
| `DoseQuantity`, `DosePerTime` | `processClearedDose`, as today | | |
| `Frequency` | `processClearedFrequency`, wired as it is: frequency and doses per time to any positive value, then the frequency's standard values | the time of a timed order | the dose quantity, the components |
| `OrderableQuantity` | new, shared list | the component quantities, counts and concentrations, the item quantities and concentrations, the orderable dose and count; of a continuous order also the item dose rates | the frequency, the time, the orderable rate |
| `ComponentQuantity` | new, the list of `orderPropertyIncrOrDecrComponentOrderableQuantity`, the component dose per kg cleared with its quantity | the orderable quantity, the component's count and concentration, its dose and item quantities, the orderable dose and count, the time of a timed order | the other components |
| `Concentration` | new, shared list | the item orderable quantity and concentrations, the component and orderable quantities and counts, the item dose rate of a continuous order | the dose, the frequency, the time |

### The mark, derived when the order is sent

GenORDER exposes `OrderProcessor.clearable: PrescriptionKind -> ChangePropertyCommand -> bool`:
`recipe` exists for `kindOf` of the command under that schedule kind; false for any command that
is no clear.

The server mapper maps the wire order as today, then marks it: it folds over the targets the
order holds, the schedule's two, the orderable's three, each component's two and each item's
nine, and for each target writes through `Target.map` with a setter that reads the variable it
is applied to: `Clearable` is `clearable kind (snd (propertyCommands target))` and that variable
holds one value. `Target.map` fans out for two targets, a component target to every component
of that name and an item concentration to the first component's same-named item
(`Models.fs:1507`); since the setter reads its own variable and the kind is the same for every
variable a target reaches, each gets its own correct mark, the fold is idempotent and its order
does not matter. The schedule kind is `PrescriptionKind.ofDto` of the Dto's flags. The marked
places and the places `ViewHelpers.clearCommand` can send are the same set by construction;
`mapToOrderVariableWith` and its 27 sites stay as they are; nothing is stored, the structure
version stays 6.

The fold lives in `ServerApi.Mappers.OrderContext.fs`, applied where the scenario's order is
mapped (`:215`); the target enumeration and the clear half of `propertyCommands` (`:337`) move
above it, since `Mappers.Order.fs` compiles first and does not know the targets.

The agreement is tested both ways over every target of `propertyCommands`: the variable the fold
marks is the one `Target.tryGet` reads, and a target whose command has a recipe under the
schedule is marked exactly when its variable holds one value; and on an order with two
components carrying a same-named item, each item's marks equal its own variable's state.

The mark switches itself off: after a clear the field comes back with a list, so it no longer
holds one value, its mark is false and the field shows the list as `Open`. A restore puts the
kept state back, the mark included.

### The contract and the server

The four clear commands lose their `picks`, and `OrderViewCommand.toString` its count.
`OrderVariable` on the wire gains `Clearable: bool`. `Ctx.clear`, `Api.preview`, `Picks.withinOrder` and
`Picks.ofOrder` lose the picks or go.

### The client

`PickListPolicy` goes. `FieldOpenPolicy.Constrained` goes; `orderVariable` takes the mark: one
value and clearable gives `ReopenCleared`, one value and not clearable gives `NoArrow`, several
values `Open`, as the #1034 table has it. `Views/Order.fs` loses `picks`, `heldPicks`, the picks
half of `sentFrom` and the pick bookkeeping in `updateOrderScenario`, `resetOrderScenario` and
the step answer; `reopenOf` reads `ovar.Clearable`. `NutritionSlot.reopenOf` reads the same.
`ViewHelpers.Reopen` loses `constrained` and `ViewHelpers.clearCommand` its picks; the arrow
dispatches the clear command, so the `reopening` refs go.

With #1343 folded in: the machines keep the state before a clear when the command is one of the
four clears, read by one predicate, `reopens`; `OrderContextMsg.ReopenField`,
`OrderPlanMsg.ReopenField`, the `ReopenField` members of the two environment interfaces, the
`reopen` prop of `Order.View`, the `planReopen` prop of `useSlot` and its pass-through in the
three nutrition views go. `RestoreField` and `Kept` stay.

The trail loses the picks of a reopen and keeps the word through the predicate.

## Checked, no change needed

- **The signing digest** is taken over the parsed typed order (`ServerApi.Session.fs:1509`); the
  mark is not on the Dto, so neither the digest nor the stored JSON moves.
- **The golden orders** are printed orders, not Dto JSON; nothing in them changes.
- **The wire name is the Dto name** (`mapToOrderVariableWith` passes `dto.Name`), and `Target`
  addresses variables by place, so the fold needs no name at all.
- **The dialog's editing gate** stays in front of the mark: `selectFor` asks
  `PlanContextPolicy.canEdit` first (`Order.fs:986`), so a field the plan dialog may not change
  shows no arrow whatever the mark says.
- **The MCP host and the benchmarks** use neither the clear commands nor the pick list.
- **A timed order after #1425**: a cleared rate or time runs `processClearedRate` with the rate
  held back, and `ClearValue` ends with `rateStage`, the list without the median. The two #1425
  tests that clear the median rate, tpn and the once timed KCl, go through `Reopen` and
  `rateNotAPick` today; step 1 checks they get their list through the recipe and the rate stage
  alone.

## Confidence

High for the client: it removes code and reads one flag. High for the routing: the command
carries what the detection had to find. Medium for the recipes: each is a list of dependent
variables, and a list one short leaves the cleared field pinned, one long lets a value the user
set move; sharing the lists with the steps keeps one copy, and the probe measures both before
any recipe is written. High for the mark: it is read from the clear commands themselves, so it
cannot name a place no command reaches, and the agreement test holds the fold to the targets. Medium for what a prescriber sees: values the solver set
are held now where the reopen recalculated them, so more fields can come back pinned; the
probe's count and the threshold are what keeps that honest.

## Steps

One PR per step, after the maintainer's go. Each step builds and is safe to release on its own:
2b keeps the picks parameter of `ClearOrderValue` unused until step 4; step 4 changes the wire
and the client call sites together; steps 5 and 6 add before they remove. Source lines are
shipped code under `src/`, tests and scripts excluded. Every `.fs` change outside
`src/Informedica.GenPRES.Client/` is prototyped in a script and migrated by the maintainer.

1. **The probe, script only.** `Scripts/ClearedFields.fsx` in GenORDER: every fixture of
   `Scenarios.fs`, solved and completed; per clear command of the sixteen, the clear applied and
   the order solved as today, and what comes back: the list of the cleared field, the variables
   that moved, the ones that stayed, and whether the cleared process ran at all; the component
   dose per kg of tpn under `processClearedDose` and under the component quantity list; and,
   per fixture, the solved orders that reach `SolveOrder` with `isCleared` true when no clear was
   sent. A table per fixture into this plan, with the fields that come back pinned counted per
   kind and schedule; the two #1425 median rate clears among them. No source change.
   `docs(genorder): probe the clears over the fixtures`; no Greptile review.

2. **GenORDER, in three.**
   - **2a, the shared lists.** `componentQuantityDependents` and the lists of the orderable
     quantity and the concentration under the step functions that have them, the operation as a
     parameter; no change in behaviour, the golden orders prove it. Under 80 lines.
     `refactor(genorder): share the dependents of a stepped field`.
   - **2b, the kind, the recipes and the pipeline.** `ClearKind`, `kindOf`, `recipe`,
     `clearable`, `PrescriptionKind.ofSchedule`, the new recipes over the shared lists, `processClearedFrequency` wired, the dose
     routing, `ClearValue` in `Types.fs` and its pipeline; `ClearOrderValue` evaluating it, its
     picks parameter kept unused. The script first, with the probe run again through `ClearValue`
     and the table of step 1 as the expected outcome, the kinds the threshold drops left without
     a recipe. Tests in `OrderReopenTests.fs`, renamed `OrderClearTests.fs`: per fixture and clear
     command, plain and per kg, the field gets a list of more than one value where the probe
     found one, the values the user set stay, the order solves again after a pick from the list;
     `kindOf` for all sixteen; `recipe` none for the excluded schedules; the two #1425 median rate
     tests through `ClearValue`. Under 180 lines. `fix(genorder): a clear recalculates what
     depends on the field`. Closes #1276.
   - **2c, the removals.** `Reopen`, `clearedPick`, the put-back and `rateNotAPick`; the tuple
     pattern and its comment; the `process-cleared` step, the `isCleared` branch and `IsCleared`
     of `SolveOrder` and `classify`; `ReopenOrderScenario` and `reopenScenarioOrder`; `Command.get`
     and `toString` follow. Under 150 lines, nearly all removed.
     `refactor(genorder): the reopen leaves the processor`.

3. **The mark, GenORDER and the server.** `OrderProcessor.clearable`, `PrescriptionKind.ofSchedule`
   and `ofDto`, with tests over every clear command and schedule kind, false for a command that
   is no clear; the fold in `ServerApi.Mappers.OrderContext.fs` at the scenario's order, the target
   enumeration and the clear half of `propertyCommands` moved above it, with the agreement test
   both ways over every target, the two-component same-named item test, and tests that a range,
   a total and the dose count are never marked and a per kg target is marked with its plain
   form. The wire type gains `Clearable` here, with
   Shared's `OrderVariable.create` (`Models.fs:985`), since the mapper needs it; the Client.Core
   tests that build the wire record get the field (`SeverityReasonPolicyTests.fs`,
   `QuantityModePolicyTests.fs`, `SectionFoldPolicyTests.fs`, `PickListPolicyTests.fs:29`). Under
   150 lines, the move counted. `feat(api): the server marks the fields a page can clear`.

4. **The wire loses the picks.** The four clear commands without `picks` and `toString` without
   the count; `ClearOrderValue` without its picks parameter and the mapper without
   `Picks.ofOrder`; `Ctx.clear` and `Api.preview` without picks, `Picks` gone; the client call sites
   the contract breaks, `ViewHelpers.clearCommand` and its callers in `Order.fs:536` and
   `NutritionSlot.fs:633`, and `Picks.withinOrder` in `Order.fs:535`, pass no picks.
   `MappersOrderContextTests.fs` keeps its 38 wire cases with the new shapes; the `Ctx.clear` tests
   in `OrderContextCommandTests.fs` (432, 445, 653), `OrderValueAgreementTests.fs` and the trail
   tests that print a clear follow. Under 100 lines. `refactor(api): a clear carries no picks`.

5. **The client reads the mark.** Additive, so the pick list still works beside it:
   `FieldOpenPolicy.orderVariable` takes `clearable: bool` in place of `constrained`, with its
   tests over the #1034 table, and `ViewHelpers.fs:168` passes `ovar.Clearable`; `reopens` in a
   policy, tested; the two machines keep `Kept` on a clear command as they do on `ReopenField`;
   `Order.fs` and `NutritionSlot.fs` `reopenOf` read the mark. Tests in
   `OrderContextMachineTests.fs` and `OrderPlanMachineTests.fs`: a clear command keeps the state,
   a pick does not, a restore puts it back. Fable compile check, `npx vite build`. Under 150 lines.
   `feat(client): the field reads whether it can be cleared`.

6. **The pick list goes.** `PickListPolicy` and its tests; `Constrained`; `ReopenField` on both
   machines, their `admitted` arms and the trail's; the `ReopenField` members of the two
   environment interfaces and `App.fs`; the `reopen` prop of `Order.View`, `planReopen` of
   `useSlot` and its pass-through in the three nutrition views; the pick state of `Order.fs`; the
   `reopening` refs, the arrow dispatching the clear command. Fable compile check,
   `npx vite build`; the maintainer checks in the browser. Under 200 lines, nearly all removed.
   `refactor(client): reopen a field without a pick list`. Closes #1343.

## Left out

- **A mark that runs the clear**, so a field pinned by another on this order shows no arrow. A
  solve per variable per answer; the threshold per kind and the list of one are the answer for
  now.
- **The dose and rate recipes over shared lists.** They keep their own lists; the step functions
  of the dose quantity and the rate could share them later in the same way.
- **The dose count as a clearable field.** No page shows it.
- **The composed TPN, the component selection and the solution choice.** The TPN plan, on top of
  this one: with the component clear a reopen of one component of a composed TPN gives that
  component its list and keeps the others, which is what that plan needs.

## Verification

- `dotnet run build`, `dotnet run servertests`, `CI=true` in a worktree
- `dotnet fsi scripts/CheckDependencyRule.fsx` after step 3
- `dotnet fantomas --check`
- the probe of step 1 against the final source, through `ClearValue`: the tables of step 2b
- in the browser, after step 6, on the paracetamol suppository in the workbench: the fields the
  probe found clearable for a discontinuous order show the arrow and no other does; clear the
  frequency: its list opens, the dose stays; close the list without a pick: the order as before.
  The plan dialog over the same order: the same arrows. The nutrition page, a TPN composed by
  hand: clear one component's mL/kg: its list opens, the others keep their values; pick one:
  the order solves
