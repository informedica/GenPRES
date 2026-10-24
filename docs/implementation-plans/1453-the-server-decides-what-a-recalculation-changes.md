# Implementation plan for issue #1453: the server decides what a recalculation changes

2026-10-13. The client's pick list goes. A recalculation of an order value is one command that
names the field; the server picks the recipe from that command, recalculates what depends on the
field, and marks on every order variable it sends whether it can be recalculated. The commands
are named for what they do: `Clear` becomes `ReCalc`, since the field is never left empty, it
comes back with the values the rest of the order allows. The four basic variables are not
recalculated: the orderable dose rate, the schedule frequency, the component orderable quantity
and the orderable dose quantity are stepped, the component quantity also navigated to its
minimum, median or maximum. Every other field is derived from them and can be recalculated,
except an item's concentration in its component, which is the product's. Closes #1453, #1276
and #1343, refs #1034; the plan for the open TPN issues builds on it. Line numbers are from
`master` at `ce3c164f`.

## Problem description

A field with one value reopens by its arrow: the client asks the server to recalculate it and
shows the list the server answers. Which fields get the arrow, and what the server does with the
request, both read the client's pick list, the variables the user picked or stepped in the
dialog. The list lives in the drug workbench's dialog alone. The plan dialog and the nutrition
slot have none, so there every field with one value reopens, and a request on a value another
variable pins comes back as it was. A value the server sets by itself, as the composed TPN will
set every component, the dose and the rate, would read as a pick in the workbench and as unknown
everywhere else. The list also makes the server's `Reopen` carry names across the wire.

The word is wrong too. The command is named `Clear`, but nothing is cleared for the user: the
variable is unrestricted for the length of one solve and comes back with a list, or with one
value when the rest of the order fixes it. A reader of the code, the wire or the trail takes a
clear for an emptied field.

The maintainer decided on 2026-10-12 and 2026-10-13: the pick list disappears; the server
decides which field can be recalculated and has a recipe for every field the pages can
recalculate; the recipe is picked from the command itself; the mark is derived when the order is
sent, never stored; the commands are named `ReCalc`; the basic variables are stepped, the derived
ones recalculated.

## What the code does today

**The pick list.** `Views/Order.fs` keeps `picks: string[] option` in React state (`Order.fs:495`),
`Some [||]` for the workbench and `None` for the plan dialog (`:489`). After an answer,
`PickListPolicy.picked` adds every variable the change narrowed to one value; a step adds its
variable; a reopen keeps the picks made before the cleared one and holds the rest for a restore
(`heldPicks`, `Order.fs:972`). The nutrition slot sends every clear with an empty list
(`NutritionSlot.fs:618`).

**The arrow.** `FieldOpenPolicy.orderVariable` gives a field with one value the arrow when the
user constrained it or the order does not say, and no arrow when the solver determined it;
`constrained` is read from the pick list. With no list every variable reads unknown, so the plan
dialog and the slot show the arrow on every solved field.

**The wire.** Four clear commands carry the picks: `ClearScheduleProperty`,
`ClearOrderableProperty`, `ClearComponentProperty`, `ClearItemProperty` (`Api.fs:59`);
`OrderViewCommand.toString` prints their count (`:142`). The server mapper puts the order id back
on each name and builds `Domain.ClearOrderValue(ctx, change, picks)`
(`ServerApi.Mappers.OrderContext.fs:395`), `change` one of the sixteen clear cases of
`ChangePropertyCommand` (`Types.fs:363`). `Ctx.clear` in Shared (`Models.fs:1730`) and
`Api.preview` (`Api.fs:170`) take the picks too, for the optimistic preview. The wire's
`ClearFilterProperty`, `ClearAllFilterProperty` and `ClearDiluentProperty` are real clears, they
set a choice to none.

**The clear command.** `processChangeProperty` sets the one variable to unrestricted
(`OrderProcessor.fs:314`, `Quantity.clear` and the like) and nothing else: the command knows
which variable it touches, and that knowledge is dropped on the spot.

**The steps.** Four step functions exist, one per basic variable: the frequency
(`OrderProcessor.fs:54`), the orderable dose rate (`:67`), the orderable dose quantity (`:84`),
the component orderable quantity (`:179`). Each sets the dependents of its variable to any
positive value and steps it. The navigation commands to minimum, median and maximum address the
same four.

**The server reopen.** `ClearOrderValue` (`OrderContext.fs:372`) applies the command and
evaluates as `ReopenOrderScenario(ctx, picks)` (`:1513`), which runs the processor's
`Reopen(ord, picks)` (`OrderProcessor.fs:877`; the other user is `reopenScenarioOrder`,
`OrderContext.fs:1232`): `clearedPick` (`OrderProcessor.fs:528`) finds the emptied variable in the
picks, `reCalcSteps` resets the order, the earlier picks are put back and `solveSteps` runs.
Without picks, or with an emptied variable that is no pick, it is a plain `SolveOrder`. #1425
added `rateNotAPick`, so an emptied median rate gets its list back.

**The cleared order process.** `SolveOrder` on a cleared order runs `processClearedStep`
(`OrderProcessor.fs:866`) when the state is `DoseSolvedAndCleared` (`:504`; `Order.isCleared`,
`Order.fs:3930`, true when any variable is unrestricted). That is `processClearedOrderWith`
(`:430`), which has to find the unrestricted variable: a match on the schedule and on the tuple
pattern at `:29`, six predicates of which exactly one may hold. Each arm is a recipe that sets
the dependents of the emptied variable back to their constraints and solves: `processClearedRate`
(`:395`) for the rate and the time of a continuous, timed or once timed order,
`processClearedDose` (`:364`) for the dose quantity and per time of the other four; the latter
resets the preparation only for one component or a preparation outside its constraints.
`processClearedFrequency` (`:337`) exists and is not wired. The dose predicates read the
orderable dose alone (`Order.fs:2128`), so an emptied component or item dose, component quantity,
dose count or concentration falls through to `logUnmatched` and a plain solve, which gives the
same one value back: #1276. The comment at `:23` calls two arms defensive.

**`isCleared` has a second reader.** `SolveOrder` picks `rateStage` over `rateStageWithMedian`
when the order is cleared (`:871`): decision 6 of #1425, an emptied rate gets its list without
the median.

**The two stages of a timed order.** `rateWaits` (`:423`) is true for a timed or once timed
order; there every value step runs with `skipRate`, the quantities are expanded and the rate
stays a range, and `rateStage` (`:678`) makes the rate list once the dose quantity holds one
value. `reCalcSteps` (`:741`) and `solveSteps` (`:773`) end with that rate step. The design note
is the staged expansion section of `docs/domain/gensolver-value-explosion-defenses.md`.

**The wire order and its targets.** The server mapper maps the domain Dto to the wire order
(`ServerApi.Mappers.Order.fs:344`); the same Dto is what the session store serializes, structure
version 6, and the store never holds what can be derived from its rows. `Schedule.Dto` carries
the schedule as five flags. Shared addresses the fields a page can set or clear as `Target`s
(`Models.fs:1410`); `Target.tryGet` reads one, `Target.map` writes one (`:1461`, `:1484`). The
mapper's `propertyCommands` (`ServerApi.Mappers.OrderContext.fs:337`) gives each target its
set-nth and its clear command. The processor names the five schedules `PrescriptionKind`
(`OrderProcessor.fs:540`). The server's command handler carries the domain command as an option
and answers the context unchanged when there is none (`ServerApi.OrderContextCommand.fs:15`).

**How it came to be.** Until #1207 there was no pick list: the cross emptied one field and the
server ran the cleared order process; every field offered the cross, so a request the process
had no arm for did nothing visible. #1034 answered that on the client with the arrow and a
record of the picks, kept on the scenario by #1207 and moved to the dialog by #1252. The
knowledge of what a request recalculates stayed in the server's match arms, unread.

**The restore.** A list closed without a pick puts the state before the request back, locally:
both machines keep it in `Kept` and `RestoreField` puts it back (`OrderPlanMachine.fs:389`,
`OrderContextMachine.fs:394`). It does not read the picks.

## Decisions

**Table 1. Taken by the maintainer, 2026-10-12 and 2026-10-13**

| Question | Decision |
|---|---|
| The pick list | Disappears from the client. |
| A field the process has no arm for | A recipe for every field the pages can recalculate. #1276 closes with it. |
| The wire and the server reopen | The picks leave the wire; `Reopen`, `clearedPick` and the put-back leave the processor; `ReopenOrderScenario` leaves the order context. |
| How the server picks the recipe | From the command, which names the field and its component or item. The scan for an unrestricted variable, the tuple pattern, `DoseSolvedAndCleared` and the `process-cleared` step go. |
| Who says which field can be recalculated | The server marks each order variable it sends; the field reads the mark. |
| What the mark means | A recipe exists for the command under the order's dose type and component count, and the variable holds one value. The mark does not run the recipe: a field another value pins is marked and comes back with a list of one, shown as any list of one. |
| Where the mark is computed | Derived when the order is sent, never stored: the server mapper folds over the order's targets and marks each from its command, so the marked places and the places a page can ask are one set. GenORDER exposes one pure function. The stored JSON and its structure version 6 do not move. `Target` is a Shared type, so the fold lives in the server, which the dependency rule allows and GenORDER may not. |
| The name | `ReCalc`, as the processor's `ReCalcValues` spells it. What sets a value range to unrestricted keeps the name `clear` (`Variable.clear` and the like), and so do the wire commands that set a filter or a diluent to none. |
| The basic variables | The orderable dose rate, the schedule frequency, the component orderable quantity and the orderable dose quantity are the user's levers: stepped, the component quantity also navigated, never recalculated. Their commands go: `ClearOrderableDoseRate`, `ClearScheduleFrequency`, `ClearComponentOrderableQuantity`, `ClearOrderableDoseQuantity`. |
| The product's constant | An item's concentration in its component is not recalculated: `ClearItemComponentConcentration` goes. Eleven commands remain. |
| Where | Its own plan, before the TPN steps. |

**Table 2. Proposed, to confirm**

| Question | Proposal |
|---|---|
| The trade | The pick list answered "keep what the user set, recompute the rest" for any field, because it knew what the user set. A recipe answers "for this request, this moves and this stays", without knowing who set what: predictable for a prescriber and a domain rule rather than a dialog's memory, less general, since a recipe one entry short leaves the field pinned. |
| A kind that always comes back pinned | The probe runs each recipe on every fixture of its dose type and component count. A kind pinned on every fixture there gets no recipe and no mark, instead of an arrow that opens a list of one. Today's path is run too, as the before column, and never decides. |
| The rate stages | `ReCalcValue` ends in `rateStage`, the list without the median: after any recalculation of a timed order stage two comes back as a list to pick from, as a median would pin it again. `SolveOrder` always runs `rateStageWithMedian`. The probe counts the solved orders that reach `SolveOrder` with `isCleared` true when nothing was asked; the local logs of this week hold two such warnings, both during a TPN test. |
| The wire shape of the five fields | `ScheduleProperty`, `OrderableProperty`, `ComponentProperty` and `ItemProperty` stay as they are, so a request on one of the five stays expressible. The recalculation half of `propertyCommands` becomes an option, `Command.toDomain` gives no domain command for those targets, and the handler answers the context unchanged, its path for a command the domain does not see. The mark keeps a page from sending one. |
| The restore | Stays, as #1034 decided. |
| The second path for a reopen (#1343) | Folded in: the machines read a reopen from the command itself, so `ReopenField`, the `reopen` callbacks and the `reopening` refs go; the arrow dispatches the command. |
| The dose count | Never asked, never stepped: the one hinge every recipe may free. |
| A recipe that fails to solve | Falls back to a solve of the order as it was, as `processClearedStep` does today; the field shows its value again. |

## Approaches considered

1. **Keep the pick list, leave the server-set values out of it.** The client would have to know
   which answers set values by themselves, and the plan dialog and the slot would still have no
   list. Rejected.
2. **Move the pick list to the server**, as before #1252. An order scenario does not carry what
   the user did in a dialog. Rejected.
3. **No pick list; the server detects the unrestricted variable**, today's shape as a function.
   The command already says which field it recalculates. Rejected.
4. **The mark stored on the Dto.** The Dto is the stored shape; the rule is never to store what
   can be derived, and the flag would go stale when a recipe changes. Rejected.
5. **The mark from a pass over the Dto's fields.** Gives a kind to fields no command reaches, so
   the arrow would have nothing to send, and is a second reader of kinds. Rejected.
6. **Keep the word clear and the five commands.** The code would keep saying a field is emptied
   where it is recalculated, and the basic variables would keep an arrow whose answer is a step
   in disguise. Rejected.
7. **No pick list; the command picks the recipe; the mark from the same commands at send time;
   the commands named for what they do; the basic variables out.** The user's earlier picks stay
   because the recipe holds them, not because a list says so. Chosen.

## Chosen approach

### The names

**Table 3. Before and after step 0**

| Today | After step 0 |
|---|---|
| `ClearScheduleTime`, `ClearOrderableQuantity`, `ClearComponentDoseQuantityAdjust`, `ClearItemDoseQuantity`, `ClearItemDoseQuantityAdjust`, `ClearItemDosePerTime`, `ClearItemDosePerTimeAdjust`, `ClearItemDoseRate`, `ClearItemDoseRateAdjust`, `ClearItemOrderableConcentration`, `ClearItemOrderableQuantity` (`Types.fs:363`) | `ReCalc` for `Clear` |
| `ClearOrderableDoseRate`, `ClearScheduleFrequency`, `ClearComponentOrderableQuantity`, `ClearOrderableDoseQuantity`, `ClearItemComponentConcentration`; `processClearedFrequency` | gone |
| `Domain.ClearOrderValue` (`OrderContext.fs:372`) | `ReCalcOrderValue` |
| `processClearedRate`, `processClearedDose` (`OrderProcessor.fs:395`, `:364`) | `reCalcTime`, `reCalcDose` |
| `ClearScheduleProperty`, `ClearOrderableProperty`, `ClearComponentProperty`, `ClearItemProperty` (`Api.fs:59`) | `ReCalcScheduleProperty`, `ReCalcOrderableProperty`, `ReCalcComponentProperty`, `ReCalcItemProperty` |
| `Ctx.clear` (`Models.fs:1730`), `ViewHelpers.clearCommand` | `Ctx.reCalc`, `ViewHelpers.reCalcCommand` |
| `Arrow.ReopenCleared`, `Click.ClearAndWait` (`FieldOpenPolicy.fs:27`, `:126`) | `Arrow.ReCalc`, `Click.ReCalcAndWait` |

Later steps add `ReCalcKind`, `OrderCommand.ReCalcValue`, `reCalcPreparation`, `canReCalc`,
`OrderVariable.CanReCalc`, `OrderReCalcTests.fs` and `Scripts/ReCalcFields.fsx`.

### The axes of an order

The order has three axes. The preparation: the orderable quantity, each component's quantity,
count and concentration, each item's quantity and concentrations. The dose: the quantity, the
per time and the rate at the orderable, component and item level, each with its per kg form,
which follows its plain form since the weight is the patient's. The schedule: the frequency and
the time. Four hinges join them: the dose count between preparation and dose, the frequency
between quantity and per time, the time between quantity and rate, the concentrations between
the levels. Each axis has a basic variable the user steps: the component orderable quantity on
the preparation, the orderable dose quantity and dose rate on the dose, the frequency on the
schedule. A recalculation is asked on a derived variable and frees the axis it belongs to, its
basic variable included, which then comes back as the list the rule allows. Two facts shape the
recipes:

- With one component the preparation is the dose volume times the dose count and the
  concentrations are the product's: the preparation axis folds into the dose.
- A timed or once timed order is decided in two stages: first the preparation and the quantity
  with its frequency, then the time and the rate, one degree of freedom given the quantity.
  Stage two is downstream of everything else.

A recipe follows one rule: set the asked axis back to its constraints at its scope, with the
dose count; set back everything downstream; hold the other axes and the hinges that were not
asked. Before every recipe the same repair: whatever is outside its constraints goes back, which
is all of today's frequency and preparation conditions.

### The kinds, GenORDER

`ReCalcKind`: `Dose`, `Preparation` with a scope, the whole, a component or an item, and `Time`.
`kindOf: ChangePropertyCommand -> ReCalcKind option`, None for every other command.

**Table 4. The eleven commands and their kind**

| Command | Kind | With one component |
|---|---|---|
| `ReCalcScheduleTime` | `Time` | `Time` |
| `ReCalcItemDoseQuantity`, `ReCalcItemDoseQuantityAdjust`, `ReCalcItemDosePerTime`, `ReCalcItemDosePerTimeAdjust` | `Dose` | `Dose` |
| `ReCalcItemDoseRate`, `ReCalcItemDoseRateAdjust` | `Time`: the rate and the time are one degree of freedom given the quantity, on `Continuous` the volume | `Time` |
| `ReCalcOrderableQuantity` | `Preparation`, the whole | `Dose`: the orderable quantity is the dose volume |
| `ReCalcComponentDoseQuantityAdjust cmp` | `Preparation`, the component | `Dose` |
| `ReCalcItemOrderableQuantity` | `Preparation`, the item | `Dose` |
| `ReCalcItemOrderableConcentration` | `Preparation`, the item | none: the concentration is the product's |

`recipe: PrescriptionKind -> components: int -> ReCalcKind -> (Order -> Result) option`: None
where the dose type has no such face, `Dose` on a continuous order, `Time` on a once or
discontinuous one, and the one-component cases of the last column.
`PrescriptionKind.ofSchedule`, the match `classify` already makes, and `PrescriptionKind.ofDto`,
from the Dto's five flags, sit next to it, so the process and the mapper read the dose type the
same way. `canReCalc kind components command` is `recipe kind components (kindOf command)` is
some. The process runs the recipe; the mark asks whether it exists.

**Table 5. The recipe per kind, component count and dose type**

The variables are named by their place in the order: `Schedule.Time`, `Orderable.Dose.Rate`,
and for a component or an item the field of that component or item, so `Dose.RateAdjust` of
every component is the rate per kg of each component. "Set back to the constraints" is
`applyConstraints`: the variable takes its calculated constraints again and the solver narrows
it from there. A row names only the faces its dose type has: the per time and the frequency on
`Discontinuous` and `Timed`, the time and the rates on `Continuous`, `Timed` and `OnceTimed`
(`Schedule` in `Types.fs:258`: `OnceTimed of Time`, `Continuous of Time`, `Timed of Frequency *
Time`). On a continuous order the time is the running time of the volume, and rate × time =
volume makes the rate and the time one degree of freedom there as on a timed order, so the item
rate commands are `Time` on all three.

| Kind | Commands | Components | Dose type | Set back to the constraints | Downstream, set back with it | Held |
|---|---|---|---|---|---|---|
| `Dose` | `ReCalcItemDoseQuantity`, `ReCalcItemDoseQuantityAdjust`; with one component also `ReCalcOrderableQuantity`, `ReCalcComponentDoseQuantityAdjust`, `ReCalcItemOrderableQuantity` | one | `Once` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust` | none | of every item `ComponentConcentration` |
| `Dose` | `ReCalcItemDoseQuantity`, `ReCalcItemDoseQuantityAdjust`, `ReCalcItemDosePerTime`, `ReCalcItemDosePerTimeAdjust`; with one component also `ReCalcOrderableQuantity`, `ReCalcComponentDoseQuantityAdjust`, `ReCalcItemOrderableQuantity` | one | `Discontinuous` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust` | none | `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Dose` | `ReCalcItemDoseQuantity`, `ReCalcItemDoseQuantityAdjust`, `ReCalcItemDosePerTime`, `ReCalcItemDosePerTimeAdjust`; with one component also `ReCalcOrderableQuantity`, `ReCalcComponentDoseQuantityAdjust`, `ReCalcItemOrderableQuantity` | one | `Timed` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Dose` | `ReCalcItemDoseQuantity`, `ReCalcItemDoseQuantityAdjust`; with one component also `ReCalcOrderableQuantity`, `ReCalcComponentDoseQuantityAdjust`, `ReCalcItemOrderableQuantity` | one | `OnceTimed` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | of every item `ComponentConcentration` |
| `Dose` | `ReCalcItemDoseQuantity`, `ReCalcItemDoseQuantityAdjust` | multiple | `Once` | `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust` | none | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; of every item `ComponentConcentration` |
| `Dose` | `ReCalcItemDoseQuantity`, `ReCalcItemDoseQuantityAdjust`, `ReCalcItemDosePerTime`, `ReCalcItemDosePerTimeAdjust` | multiple | `Discontinuous` | `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust` | none | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Dose` | `ReCalcItemDoseQuantity`, `ReCalcItemDoseQuantityAdjust`, `ReCalcItemDosePerTime`, `ReCalcItemDosePerTimeAdjust` | multiple | `Timed` | `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Dose` | `ReCalcItemDoseQuantity`, `ReCalcItemDoseQuantityAdjust` | multiple | `OnceTimed` | `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; of every item `ComponentConcentration` |
| `Preparation`, the whole | `ReCalcOrderableQuantity` | multiple | `Once` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust` | none | of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `ComponentConcentration` |
| `Preparation`, the whole | `ReCalcOrderableQuantity` | multiple | `Discontinuous` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust` | none | of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Preparation`, the whole | `ReCalcOrderableQuantity` | multiple | `Continuous` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust` | `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`, which follow the concentrations | of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust`; `Schedule.Time`; of every item `ComponentConcentration` |
| `Preparation`, the whole | `ReCalcOrderableQuantity` | multiple | `Timed` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Preparation`, the whole | `ReCalcOrderableQuantity` | multiple | `OnceTimed` | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `ComponentConcentration` |
| `Preparation`, the component | `ReCalcComponentDoseQuantityAdjust cmp` | multiple | `Once` | of the component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; `Orderable.OrderableQuantity`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust` | none | of every other component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `ComponentConcentration` |
| `Preparation`, the component | `ReCalcComponentDoseQuantityAdjust cmp` | multiple | `Discontinuous` | of the component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; `Orderable.OrderableQuantity`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust` | none | of every other component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Preparation`, the component | `ReCalcComponentDoseQuantityAdjust cmp` | multiple | `Continuous` | of the component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; `Orderable.OrderableQuantity`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust` | `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`, which follow the concentrations | of every other component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust`; `Schedule.Time`; of every item `ComponentConcentration` |
| `Preparation`, the component | `ReCalcComponentDoseQuantityAdjust cmp` | multiple | `Timed` | of the component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; `Orderable.OrderableQuantity`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | of every other component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Preparation`, the component | `ReCalcComponentDoseQuantityAdjust cmp` | multiple | `OnceTimed` | of the component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; `Orderable.OrderableQuantity`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | of every other component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `ComponentConcentration` |
| `Preparation`, the item | `ReCalcItemOrderableQuantity`, `ReCalcItemOrderableConcentration` | multiple | `Once` | of the item `OrderableQuantity`, `OrderableConcentration`, the one not asked for as a dependent of the other; of its component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; `Orderable.OrderableQuantity`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust` | none | of every other component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `ComponentConcentration` |
| `Preparation`, the item | `ReCalcItemOrderableQuantity`, `ReCalcItemOrderableConcentration` | multiple | `Discontinuous` | of the item `OrderableQuantity`, `OrderableConcentration`, the one not asked for as a dependent of the other; of its component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; `Orderable.OrderableQuantity`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust` | none | of every other component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Preparation`, the item | `ReCalcItemOrderableQuantity`, `ReCalcItemOrderableConcentration` | multiple | `Continuous` | of the item `OrderableQuantity`, `OrderableConcentration`, the one not asked for as a dependent of the other; of its component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; `Orderable.OrderableQuantity`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust` | `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`, which follow the concentrations | of every other component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust`; `Schedule.Time`; of every item `ComponentConcentration` |
| `Preparation`, the item | `ReCalcItemOrderableQuantity`, `ReCalcItemOrderableConcentration` | multiple | `Timed` | of the item `OrderableQuantity`, `OrderableConcentration`, the one not asked for as a dependent of the other; of its component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; `Orderable.OrderableQuantity`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | of every other component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Preparation`, the item | `ReCalcItemOrderableQuantity`, `ReCalcItemOrderableConcentration` | multiple | `OnceTimed` | of the item `OrderableQuantity`, `OrderableConcentration`, the one not asked for as a dependent of the other; of its component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; `Orderable.OrderableQuantity`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | of every other component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration` and of its items `OrderableQuantity`, `OrderableConcentration`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `ComponentConcentration` |
| `Time` | `ReCalcScheduleTime`, `ReCalcItemDoseRate`, `ReCalcItemDoseRateAdjust` | any | `Continuous` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | none | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `ComponentConcentration` |
| `Time` | `ReCalcScheduleTime`, `ReCalcItemDoseRate`, `ReCalcItemDoseRateAdjust` | any | `Timed` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | none | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`, `Orderable.Dose.PerTime`, `Orderable.Dose.PerTimeAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`, `Dose.PerTime`, `Dose.PerTimeAdjust`; `Schedule.Frequency`; of every item `ComponentConcentration` |
| `Time` | `ReCalcScheduleTime`, `ReCalcItemDoseRate`, `ReCalcItemDoseRateAdjust` | any | `OnceTimed` | `Schedule.Time`; `Orderable.Dose.Rate`, `Orderable.Dose.RateAdjust`; of every component `Dose.Rate`, `Dose.RateAdjust`; of every item `Dose.Rate`, `Dose.RateAdjust` | none | `Orderable.OrderableQuantity`; of every component `OrderableQuantity`, `OrderableCount`, `OrderableConcentration`; of every item `OrderableQuantity`, `OrderableConcentration`; `Orderable.DoseCount`; `Orderable.Dose.Quantity`, `Orderable.Dose.QuantityAdjust`; of every component `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `Dose.Quantity`, `Dose.QuantityAdjust`; of every item `ComponentConcentration` |
| none | `ReCalcItemOrderableConcentration` | one | all five | no recipe and no mark: the concentration is the product's | | |

Every recipe ends in `solveSteps` with `rateStage`: the values ensured with the caps, the order
solved, the component quantity picked, the rate list made once `Orderable.Dose.Quantity` has one
value, without its median. `reCalcDose` first makes the increment of a volume orderable quantity
coarser until at most 100 values remain, and the dose quantity's after it, as today; the
preparation recipe does the same where the probe shows a longer list. So a recipe is a function
over the order, not a list of property changes alone.

The table is the hypothesis the probe decides: every variable an equation ties to the asked one
must be in one of the columns, and one held by mistake pins the field. It departs from today's
`processClearedDose` in one place: that frees the time and holds the rates, the `Dose` rows free
stage two whole, since with the rate held at one value the time's constraints prune the quantity
list through the old rate. The `Time` rows are today's `processClearedRate`. One thing the
probe must check there: `Schedule.getTime` (`Order.fs:2413`) answers None for a once timed order
while `hasTime` (`:2396`) is true, so whether the time of the once timed KCl is set back at all is
a measurement, not a given.

### The pipeline

`OrderCommand.ReCalcValue of Order * ChangePropertyCommand` replaces `Reopen`: the field set to
unrestricted by `processChangeProperty`, the recipe of `kindOf` run, then `solveSteps` with
`rateStage`. `ReCalcOrderValue(ctx, change)` evaluates it through `processScenarioOrder`.
`SolveOrder` loses its `process-cleared` step and its branch on `isCleared` and always runs
`rateStageWithMedian`; `classify` loses `IsCleared`; `processClearedOrderWith` and the tuple
pattern become the recipes, each a function; `clearedPick`, the put-back and `rateNotAPick` go;
`OrderReopenTests.fs` becomes `OrderReCalcTests.fs` over `ReCalcValue`.

### The mark, derived when the order is sent

GenORDER exposes `OrderProcessor.canReCalc: PrescriptionKind -> int -> ChangePropertyCommand -> bool`.
The server mapper maps the wire order as today, then folds over the targets the order holds and
writes each through `Target.map` with a setter that reads the variable it is applied to:
`CanReCalc` is `canReCalc` of the dose type (`PrescriptionKind.ofDto`), the component count and
the recalculation command of `propertyCommands target`, false when the target has none, and the
variable holds one value. `Target.map` fans out for two targets (`Models.fs:1507`); since the
setter reads its own variable and the kind is the same for every variable a target reaches, each
gets its own mark, and the fold is idempotent. The marked places and the places
`ViewHelpers.reCalcCommand` can send are one set by construction; `mapToOrderVariableWith` and
its 27 sites stay; nothing is stored.

The fold lives in `ServerApi.Mappers.OrderContext.fs`, applied where the scenario's order is
mapped (`:215`); the target enumeration and the recalculation half of `propertyCommands` move
above it, since `Mappers.Order.fs` compiles first and does not know the targets.

The agreement is tested both ways over every target: the variable the fold marks is the one
`Target.tryGet` reads; a target with a recipe is marked exactly when its variable holds one
value; a target without one never; on an order with two components carrying a same-named item,
each item's marks equal its own variable's state.

The mark switches itself off: after a recalculation the field holds a list, its mark is false
and the field shows the list as `Open`. A restore puts the kept state back, the mark included.

### The contract, the server and the client

The four wire commands lose their `picks`, and `toString` its count. `OrderVariable` on the wire
gains `CanReCalc: bool`. `Ctx.reCalc`, `Api.preview`, `Picks.withinOrder` and `Picks.ofOrder` lose
the picks or go.

`PickListPolicy` goes. `FieldOpenPolicy.Constrained` goes; `orderVariable` takes the mark: one
value and marked gives `ReCalc`, one value and not marked `NoArrow`, several values `Open`, as
the #1034 table has it. `Views/Order.fs` loses `picks`, `heldPicks`, the picks half of `sentFrom`
and the pick bookkeeping; `reopenOf` in `Order.fs` and `NutritionSlot.fs` reads `ovar.CanReCalc`.
`ViewHelpers.Reopen` loses `constrained`. With #1343 folded in, the machines keep the state before
a recalculation when the command is one of the four, read by one predicate, `reopens`;
`ReopenField` on both machines and both environment interfaces, the `reopen` prop of `Order.View`,
the `planReopen` prop of `useSlot` and its pass-through in the three nutrition views, and the
`reopening` refs go. `RestoreField` and `Kept` stay. The trail prints the command name, so it
follows the rename.

## Checked, no change needed

- **The signing digest** is over the parsed typed order (`ServerApi.Session.fs:1509`); the mark is
  not on the Dto, so neither the digest nor the stored JSON moves.
- **The golden orders** are printed orders, not Dto JSON.
- **The stored rows hold no commands**: only the composition root and the command handler see
  `OrderViewCommand`, so the rename costs no compatibility.
- **`Target` addresses variables by place**, so the fold needs no name.
- **The dialog's editing gate** stays in front of the mark: `selectFor` asks
  `PlanContextPolicy.canEdit` first (`Order.fs:986`).
- **The MCP host and the benchmarks** use neither the commands nor the pick list.
- **The two #1425 tests** that empty the median rate, tpn and the once timed KCl, go through
  `Reopen` and `rateNotAPick` today. The orderable rate is no longer asked; in 2b they become
  tests of the item rate request through `ReCalcValue`, the same stage two, and must give the same
  list.
- **The basic variables keep their steps and navigations**: the increase, decrease, minimum,
  maximum and median commands are not touched.

## Confidence

High for the client: it removes code and reads one flag. High for the routing: the command
carries what the detection had to find. Medium for the recipes: a list one short leaves the
field pinned, one long lets a value the user set move; the probe measures both before any recipe
reaches the source. High for the mark: read from the commands themselves, held to the targets by
the agreement test. Medium for what a prescriber sees: values the solver set are held now where
the reopen recalculated them, so more fields can come back pinned; the probe's count is what
keeps that honest.

## Steps

One PR per step, after the maintainer's go. Each step builds and is safe to release on its own:
step 0 changes names only; 2b keeps the picks parameter of `ReCalcOrderValue` unused until step
4; step 4 changes the wire and the client call sites together; steps 5 and 6 add before they
remove. Source lines are shipped code under `src/`, tests and scripts excluded. Every `.fs` change
outside `src/Informedica.GenPRES.Client/` is prototyped in a script and migrated by the
maintainer.

0. **The names.** Table 3, across GenORDER (`Types.fs:363`, the arms of `processChangeProperty`
   at `OrderProcessor.fs:314`, the two recipes and their calls, `ClearOrderValue` with
   `Command.get` and `toString`), Shared (`Api.fs:59`, `:135`, `:170`; `Models.fs:1730`), the
   server mapper (`propertyCommands` with its recalculation half an option, `toDomain` giving no
   command for a target without one), the client (`ViewHelpers.clearCommand` and its callers at
   `Order.fs:535`, `NutritionSlot.fs:633`; `FieldOpenPolicy.fs:27`, `:126`) and the tests that
   spell the names (`OrderValueCommandTests.fs`, `DomainCommandTests.fs`,
   `MappersOrderContextTests.fs`, `OrderValueAgreementTests.fs`, `OrderContextCommandTests.fs`,
   the `FieldOpenPolicy` and trail tests); `Issue1425Staged.fsx` and
   `docs/domain/genorder-order-fsm.md` follow. The five commands and `processClearedFrequency` go
   here; the tuple pattern's `FrequencyCleared` arm stays until 2c. No behaviour changes for the
   eleven; a wire request on one of the five answers the context unchanged. About 100 lines.
   `refactor(api): rename the clear commands ReCalc`.

1. **The probe, script only.** `Scripts/ReCalcFields.fsx` in GenORDER: every fixture of
   `Scenarios.fs`, solved and completed; per command of the eleven, two runs: today's path, the
   before column, and the recipe of Table 5 as a shadowed function followed by `solveSteps` with
   `rateStage`. For each run: the list of the field, the variables that moved, the ones that
   stayed. Besides: the dose recipe on the timed fixtures with the rate held and with stage two
   freed, the two list lengths; the item rate request on tpn and the once timed KCl against the
   list the #1425 tests expect; whether a total ever holds one value; the solved orders that
   reach `SolveOrder` with `isCleared` true when nothing was asked. A table per fixture into this
   plan, with the kinds that come back pinned counted per dose type and component count. No
   source change. `docs(genorder): probe the recalculations over the fixtures`; no Greptile
   review.

2. **GenORDER, in two.**
   - **2b, the kinds, the recipes and the pipeline.** `ReCalcKind`, `kindOf`, `recipe`,
     `canReCalc`, `PrescriptionKind.ofSchedule`; `reCalcPreparation` new with its three scopes,
     `reCalcDose` freeing stage two, `reCalcTime` as today; `ReCalcValue` in `Types.fs` and its
     pipeline; `ReCalcOrderValue` evaluating it, its picks parameter kept unused. The script
     first, with the probe run again through `ReCalcValue` and the Table 5 column of step 1 as
     the expected outcome. Tests in `OrderReCalcTests.fs`: per fixture and command, the field gets
     a list of more than one value where the probe did, the values the user set stay, the order
     solves again after a pick; `kindOf` for all eleven; `recipe` none where Table 4 and 5 say
     none; the two #1425 tests as item rate requests. Under 180 lines.
     `fix(genorder): a recalculation changes what depends on the field`. Closes #1276.
   - **2c, the removals.** `Reopen`, `clearedPick`, the put-back and `rateNotAPick`; the tuple
     pattern and its comment; the `process-cleared` step, the `isCleared` branch and `IsCleared`;
     `ReopenOrderScenario` and `reopenScenarioOrder`, `Command.get` and `toString` following.
     Under 150 lines, nearly all removed. `refactor(genorder): the reopen leaves the processor`.

3. **The mark, GenORDER and the server.** `OrderProcessor.canReCalc`, `PrescriptionKind.ofSchedule`
   and `ofDto`, with tests over every command, dose type and component count; the fold in
   `ServerApi.Mappers.OrderContext.fs` at the scenario's order, the target enumeration and the
   recalculation half of `propertyCommands` moved above it, with the agreement tests and tests
   that a range, a total, the dose count and the five fields are never marked and a per kg item
   target is marked with its plain form. The wire type gains `CanReCalc` here, with Shared's
   `OrderVariable.create` (`Models.fs:985`); the Client.Core tests that build the wire record get
   the field (`SeverityReasonPolicyTests.fs`, `QuantityModePolicyTests.fs`,
   `SectionFoldPolicyTests.fs`, `PickListPolicyTests.fs:29`). Under 150 lines, the move counted.
   `feat(api): the server marks the fields a page can recalculate`.

4. **The wire loses the picks.** The four commands without `picks` and `toString` without the
   count; `ReCalcOrderValue` without its picks parameter and the mapper without `Picks.ofOrder`;
   `Ctx.reCalc` and `Api.preview` without picks, `Picks` gone; the client call sites the contract
   breaks, `ViewHelpers.reCalcCommand` and its callers in `Order.fs:536` and `NutritionSlot.fs:633`,
   and `Picks.withinOrder` in `Order.fs:535`. `MappersOrderContextTests.fs` keeps its 38 wire
   cases with the new shapes; the `Ctx.reCalc` tests in `OrderContextCommandTests.fs` (432, 445,
   653), `OrderValueAgreementTests.fs` and the trail tests follow. Under 100 lines.
   `refactor(api): a recalculation carries no picks`.

5. **The client reads the mark.** Additive, the pick list still works beside it:
   `FieldOpenPolicy.orderVariable` takes `canReCalc: bool` in place of `constrained`, tested over
   the #1034 table; `ViewHelpers.fs:168` passes `ovar.CanReCalc`; `reopens` in a policy, tested;
   the two machines keep `Kept` on a recalculation as they do on `ReopenField`, tested in
   `OrderContextMachineTests.fs` and `OrderPlanMachineTests.fs`; `reopenOf` in both views reads the
   mark. Fable compile check, `npx vite build`. Under 150 lines.
   `feat(client): the field reads whether it can be recalculated`.

6. **The pick list goes.** `PickListPolicy` and its tests; `Constrained`; `ReopenField` on both
   machines, their `admitted` arms and the trail's; the `ReopenField` members of the two
   environment interfaces and `App.fs`; the `reopen` and `planReopen` props and their
   pass-through; the pick state of `Order.fs`; the `reopening` refs. Fable compile check,
   `npx vite build`; the maintainer checks in the browser. Under 200 lines, nearly all removed.
   `refactor(client): reopen a field without a pick list`. Closes #1343.

## Left out

- **A mark that runs the recipe**, so a field pinned by another on this order shows no arrow: a
  solve per variable per answer. The list of one is the answer for now.
- **Shared lists with the step functions.** The recipes free whole axes and the steps free the
  dependents of one basic variable; the two lists differ, and each stays where it is.
- **The dose count, the four basic variables and an item's component concentration as
  recalculated fields**, by the maintainer's decision. The TPN plan, on top of this one, reopens a
  component by stepping or navigating it, and its mL/kg by the component scope of the preparation
  kind.

## Verification

- `dotnet run build`, `dotnet run servertests`, `CI=true` in a worktree
- `dotnet fsi scripts/CheckDependencyRule.fsx` after step 3
- `dotnet fantomas --check`
- the probe of step 1 against the final source, through `ReCalcValue`
- in the browser, after step 6: the paracetamol suppository in the workbench shows the arrow on
  the fields the probe found and on no other, the frequency and the dose quantity in mL among the
  latter; recalculate the paracetamol dose in mg: its list opens, the frequency stays; close the
  list without a pick: the order as before. The plan dialog over the same order: the same arrows.
  A TPN composed by hand: no component quantity shows an arrow, its mL/kg does; recalculate one:
  its list opens, the other components keep their values. The continuous morphine: the rate in
  mL/h shows none, the morphine in mcg/kg/h does; recalculate it: its list opens, the volume
  stays
