# Implementation plan for issue #1453: the server decides what a recalculation changes

2026-10-13, revised 2026-10-26 to migrate the order program of
`src/Informedica.GenORDER.Lib/Scratch/OrderVariablesAndEquations.fsx`. The client's pick list goes.
A recalculation of an order value is one action that names the variable; the server picks the recipe
from that action and sets back what the variables the recipe keeps do not determine. The client
decides from the order it holds which field offers a recalculation, by an explicit rule per intent
case; the server sends no mark. The commands are named for what they do:
`Clear` becomes `ReCalc`, since the field is never left empty, it comes back with the values the
rest of the order allows. The four basic variables are not named by a recalculation command: the
orderable dose rate, the schedule frequency, the component orderable quantity and the orderable dose
quantity are stepped or selected. The script holds more than the recalculation: every processor
command becomes an action of one program, run as a list of effects. This plan migrates the complete
script in steps, the recalculation first. Closes #1453, #1276 and #1343, refs #1034, #1349
and #1464; the plan for the open TPN issues builds on it. Code is named by its function or file; each
step's pull request finds where it is on `master` then.

## Problem description

A field with one value is selectable for a recalculation: the client asks the server to
recalculate it and shows the list the server answers. Which fields are selectable, and what the
server does with the request, both read the client's pick list, the variables the user picked or stepped in the
dialog. The list lives in the drug workbench's dialog alone. The plan dialog and the nutrition
slot have none, so there every field with one value is selectable, and a request on a value another
variable pins comes back as it was. A value the server sets by itself, as the composed TPN will
set every component, the dose and the rate, would read as a pick in the workbench and as unknown
everywhere else. The list also makes the server's `Reopen` carry names across the wire.

The word is wrong too. The command is named `Clear`, but nothing is cleared for the user: the
variable is unrestricted for the length of one solve and comes back with a list, or with one
value when the rest of the order fixes it. A reader of the code, the wire or the trail takes a
clear for an emptied field.

The maintainer decided on 2026-10-12, 2026-10-13 and 2026-10-26: the pick list disappears; the
client decides which field offers a recalculation, by an explicit rule per intent case over the
order it holds; the server sends no mark; the recipe is picked from the command itself; the
commands are named `ReCalc`; the basic variables are stepped, the derived ones recalculated.

## What the code does today

**The pick list.** `Views/Order.fs` keeps `picks: string[] option` in React state (`Order.fs`),
`Some [||]` for the workbench and `None` for the plan dialog. After an answer,
`PickListPolicy.picked` adds every variable the change narrowed to one value; a step adds its
variable; a reopen keeps the picks made before the cleared one and holds the rest for a restore
(`heldPicks`, `Order.fs`). The nutrition slot sends every clear with an empty list
(`NutritionSlot.fs`).

**The selectable field.** `FieldOpenPolicy.orderVariable` makes a field with one value selectable
when the user constrained it or the order does not say, and not selectable when the solver
determined it;
`constrained` is read from the pick list. With no list every variable reads unknown, so the plan
dialog and the slot make every solved field selectable.

**The wire.** Four clear commands carry the picks: `ClearScheduleProperty`,
`ClearOrderableProperty`, `ClearComponentProperty`, `ClearItemProperty` (`Api.fs`);
`OrderViewCommand.toString` prints their count. The server mapper puts the order id back
on each name and builds `Domain.ClearOrderValue(ctx, change, picks)`
(`ServerApi.Mappers.OrderContext.fs`), `change` one of the sixteen clear cases of
`ChangePropertyCommand` (`Types.fs`). `Ctx.clear` in Shared (`Models.fs`) and
`Api.preview` (`Api.fs`) take the picks too, for the optimistic preview. The wire's
`ClearFilterProperty`, `ClearAllFilterProperty` and `ClearDiluentProperty` are real clears, they
set a choice to none.

**The clear command.** `processChangeProperty` sets the one variable to unrestricted
(`OrderProcessor.fs`, `Quantity.clear` and the like) and nothing else: the command knows
which variable it touches, and that knowledge is dropped on the spot.

**The steps.** Four step functions in `OrderProcessor.fs`, one per basic variable:
`orderPropertyIncrOrDecrFrequency`, `orderPropertyIncrOrDecrOrderableDoseRate`,
`orderPropertyIncrOrDecrOrderableDoseQuantity` and
`orderPropertyIncrOrDecrComponentOrderableQuantity`. Each sets the dependents of its variable to any
positive value and steps it. The navigation commands to minimum, median and maximum address the same
four.

**The server reopen.** `ClearOrderValue` (`OrderContext.fs`) applies the command and
evaluates as `ReopenOrderScenario(ctx, picks)`, which runs the processor's
`Reopen(ord, picks)` (`OrderProcessor.fs`; the other user is `reopenScenarioOrder`,
`OrderContext.fs`): `clearedPick` (`OrderProcessor.fs`) finds the emptied variable in the
picks, `reCalcSteps` resets the order, the earlier picks are put back and `solveSteps` runs.
Without picks, or with an emptied variable that is no pick, it is a plain `SolveOrder`. #1425
added `rateNotAPick`, so an emptied median rate gets its list back.

**The cleared order process.** `SolveOrder` on a cleared order runs `processClearedStep`
(`OrderProcessor.fs`) when the state is `DoseSolvedAndCleared` (`Order.isCleared`, `Order.fs`, true
when any variable is unrestricted). That is `processClearedOrderWith`, which has to find the
unrestricted variable: a match on the schedule and on the tuple pattern above it, six predicates of
which exactly one may hold. Each arm is a recipe that sets the dependents of the emptied variable
back to their constraints and solves: `processClearedRate` for the rate and the time of a
continuous, timed or once timed order, `processClearedDose` for the dose quantity and per time of
the other four; the latter resets the preparation only for one component or a preparation outside
its constraints. `processClearedFrequency` exists and is not wired. The dose predicates read the
orderable dose alone (`Order.fs`), so an emptied component or item dose, component quantity, dose
count or concentration falls through to `logUnmatched` and a plain solve, which gives the same one
value back: #1276. The comment on the pattern calls two arms defensive.

**`isCleared` has a second reader.** `SolveOrder` picks `rateStage` over `rateStageWithMedian`
when the order is cleared: decision 6 of #1425, an emptied rate gets its list without
the median.

**The two stages of a timed order.** `rateWaits` is true for a timed or once timed
order; there every value step runs with `skipRate`, the quantities are expanded and the rate
stays a range, and `rateStage` makes the rate list once the dose quantity holds one
value. `reCalcSteps` and `solveSteps` end with that rate step. The design note
is the staged expansion section of `docs/domain/gensolver-value-explosion-defenses.md`.

**The wire order and its targets.** The server mapper maps the domain Dto to the wire order
(`ServerApi.Mappers.Order.fs`); the same Dto is what the session store serializes, structure
version 6, and the store never holds what can be derived from its rows. `Schedule.Dto` carries
the schedule as five flags. Shared addresses the fields a page can set or clear as `Target`s
(`Models.fs`); `Target.tryGet` reads one, `Target.map` writes one. The
mapper's `propertyCommands` (`ServerApi.Mappers.OrderContext.fs`) gives each target its
set-nth and its clear command. The processor names the five schedules `PrescriptionKind`
(`OrderProcessor.fs`). The server's command handler carries the domain command as an option
and answers the context unchanged when there is none (`ServerApi.OrderContextCommand.fs`).

**How it came to be.** Until #1207 there was no pick list: the cross emptied one field and the
server ran the cleared order process; every field offered the cross, so a request the process
had no arm for did nothing visible. #1034 answered that on the client with the selectable field and a
record of the picks, kept on the scenario by #1207 and moved to the dialog by #1252. The
knowledge of what a request recalculates stayed in the server's match arms, unread.

**The restore.** A list closed without a pick puts the state before the request back, locally:
both machines keep it in `Kept` and `RestoreField` puts it back (`OrderPlanMachine.fs`,
`OrderContextMachine.fs`). It does not read the picks.

## What the script holds

`Scratch/OrderVariablesAndEquations.fsx` implements the design of the local document
`localDocs/order-variables-and-equations.md` against the shipped library. The document is not
tracked (`.gitignore`). Its parts, by the module of the library each goes to:

**Table A. The parts of the script and where each goes.** Lines are code lines without blanks
and comments, measured on 2026-10-26; the shipped size with its documentation is about one and a
half times as many.

| Part | Goes to | Lines |
|---|---|---|
| `ValueUnit.collectValues`, `ValueUnit.oneCount`, `List.sequenceOption`, `ValueRangeCase.tryToValueSet` | Utils, GenUnits, GenSolver, GenOrder (Table B) | 30 |
| `Shape` and `OrderVariable.shape`; `OrderVariable` helpers: `name`, `hasOneValue`, `byName`, `hasOneCalculatedValue`, `valueCount`, `solvedValue`, `free`, `setToOne`, `setToAtLeastOne`, `tryConstrain`, `includeOriginalValue` | `Types.fs`, `OrderVariable.fs` | 70 |
| `Order` helpers: `isTimeFixed`, `doseCount`, `mapDefinedConstraints`, `components`, `items`, `tryComponent`, `tryItem`, `rateIsRange`, `rateIsList`, `equationMapping`, `equations`, `mapOrderVariables`, `isReachable` | `Order.fs` | 60 |
| `Order.minIncrMaxToValuesWithOriginal` | the shipped `Order.minIncrMaxToValues` (`Order.fs`), which takes the original order as an option and adds its values at the end; the other callers pass none | 65 |
| `caps`: the two caps of a timed order swapped | `Order.ValueCaps.defaults`, where `TimedFew` becomes 10 and `TimedMaxFew` 20 | 1 |
| `Medication.withOrderCount`, `Medication.withDoseCount` | `Medication.toOrder` | 23 |
| `Determination`: which variables a set of kept variables determines, by elimination over the logarithms of the product equations | a new file, `Determination.fs` | 99 |
| The types: `DoseProperty`, `OrderVariableReference`, `StepIncrement`, `Choice`, `OrderAction`, `SetBackTo`, `SolveMode`, `ValueCap`, `RateList`, `RateSelection`, `OrderCondition`, `OrderEffect`, `ActionFailure`, `Recipe`, `SetCommand` | `Types.fs` | 124 |
| `OrderVariableReference.tryGet`, `isBasic`, `invariants`; `Choice.select`; `SetBackTo.apply`; `OrderCondition.holds`; `Dose.toOrdVar` | `OrderProcessor.fs` | 155 |
| `OrderEffect.apply` and `run`: the interpreter | `OrderProcessor.fs` | 120 |
| `SetCommand` and `Recipe`: the step and percentile recipes, the recalculation kinds, `forReCalcCommand` | `OrderProcessor.fs` | 260 |
| `OrderAction.effects` and `OrderAction.run` | `OrderProcessor.fs` | 100 |
| `CommandBridge` and `processPipelineWith`: the shipped commands as actions, the shipped processor for the rest | `OrderProcessor.fs` | 90 |
| `OrderContext.processScenarioAction` and `tryEvaluateOrderAction` | `OrderContext.fs` | 40 |
| `CompleteOrder` and the effect `DropImpossibleValues`: a search for complete orders that drops the values no complete order reaches | `OrderProcessor.fs`, before `OrderEffect`, which calls it | 110 |
| The split per concentration: `Component.concentrations`, `Order.splitPerConcentration`, `Order.unionPerConcentration`, `OrderVariable.fixToValues`, `tryValueList`, `unionPerConcentration`, `ValueRangeCase.unionRanges`, `List.combinations`, `OrderEffect.setBackNames`, `OrderEffect.forConcentration` | GenOrder | 150 |
| `TemporaryHelpers`: names of the document, the bridge between them and `OrderVariableReference`, the shipped recipes run for comparison | the test project, or gone with the script | |
| `TestsAndFixtures`: the classes of the document, the fixtures, the checks, the laws, the helper tests, the report | `tests/Informedica.GenORDER.Tests` | |

## Migration rules

The script is migrated so that the source stays as clean as before:

1. **No duplication.** Where the script copies, wraps or repeats shipped code, the shipped code
   is changed once and the script's copy is not migrated. Where two script functions do the same,
   one is migrated. Shipped code the program replaces is deleted in the step that replaces it.
2. **Each function where it belongs.** A function on one level of the order goes to the module of
   that level in `Order.fs` (`Orderable`, `Orderable.Dose`, `Orderable.Component`,
   `Orderable.Item`, `Schedule`); a function on an order variable to `OrderVariable.fs`, on a
   range to GenSolver, on a value unit to GenUnits, on a list to `Informedica.Utils.Lib/List.fs`.
   Types go to `Types.fs`, as `OrderCommand` and `ChangePropertyCommand` do; the program's modules
   to `OrderProcessor.fs`.
3. **The patterns of the source.** The naming of the module it lands in (`getName`, `toOrdVars`,
   `fromOrdVars`, `mapOrdVars`), the typed wrappers of `OrderVariable` (`Count.apply`,
   `Quantity.toOrdVar`), the `logger` and `caps` parameters as the processor passes them, and
   Fantomas over the result.
4. **Brief, plain comments.** A `///` line says what the function gives, in one sentence where
   one suffices; a `//` line only for a reason the code cannot show. The script's comments
   mention "today", "the shipped code", the names of deleted functions, the document's tables and
   the steps of this plan; none of that reaches the source.

**Table B. Where the script's parts land, and the duplicates each resolves**

| Script | Lands in | Duplicate resolved |
|---|---|---|
| `Order.equationMapping` | `Order.fs`, extracted from `Order.solve` | the mapping inside `solve` |
| `Order.rateWaits`, `isComposed`, `isPrepWithinConstraints` | `Order.fs`, moved from `OrderProcessor.fs` | the script's wrappers |
| `OrderContext.processScenarioAction` | `OrderContext.processScenarioOrder`, taking an order function | the copy of `processScenarioOrder` |
| `Order.minIncrMaxToValuesWithOriginal` | `Order.minIncrMaxToValues`, with the original order as an option | the 65-line copy |
| `OrderVariable.setToOne`, `setToAtLeastOne` | `OrderVariable.fs`; `Count.setToOne` and `Count.setMinToOne` call them until step 7c deletes their last callers, then go | the two count setters |
| `ValueUnit.oneCount` | `OrderVariable.Count.one`, also used by the two count setters | `Units.Count.times \|> ValueUnit.one`, written out twice |
| `ValueUnit.collectValues` | `ValueUnit.fs` in GenUnits, beside `collect` | |
| `List.sequenceOption`, `List.combinations` | `List.fs` in Utils, used by GenSolver and GenOrder alike | the inline `forall Option.isSome` tests |
| `ValueRangeCase.tryToValueSet`, `unionRanges` | `Variable.ValueRange` in GenSolver, beside `minIncrMaxCount` | |
| `OrderVariable.valueCount` | `OrderVariable.fs`, counting a min, increment and max with `ValueRange.minIncrMaxCount` | the script's own count |
| `OrderVariable.name` and the string keys of `Determination`, `undetermined` and `setBackNames` | not migrated: every set and map of variables is keyed by the solver's `Name` (`getName`) | two keys for one variable |
| `Component.name`, `Item.name`, `Order.components`, `items`, `tryComponent`, `tryItem` | `Orderable.tryComponent`, `Orderable.Component.tryItem`, reading the existing `getName` | names read in two ways |
| `Dose.toOrdVar` | `Orderable.Dose.toOrdVar`, beside `toOrdVars` | |
| `Recipe.isDoseConstrained`, `isComponentDoseConstrained` | `Orderable.Dose.isConstrained`, `Orderable.Component.isDoseConstrained` | |
| `Order.isTimeFixed` | `Schedule.isTimeFixed` | |
| `Order.mapOrderVariables` | `Order.mapOrdVars`, beside `toOrdVars` and `fromOrdVars` | |
| `Order.doseCount`, `Order.mapDefinedConstraints` | not migrated: `Medication.withDoseCount` writes the dose count as `withOrderCount` writes the order count, with `Count.apply (OrderVariable.mapConstraints ..)` | a second way to change a count's constraints |
| `Order.orderableQuantity`, `Recipe.isStep`, `Recipe.toString`, `OrderVariableReference.toString` | the test project, or nowhere: the program does not use them | |
| `Recipe.unconstrainedDoseNames` | the test helper file, built on `Orderable.Component.isDoseConstrained`: only the laws use it | |
| `CompleteOrder.byName` | `OrderVariable.byName` | a second lookup by name |
| `OrderEffect.setBackNames` and the set-back of `OrderEffect.apply` | one function, the names an effect sets back, used by both | the selection written twice |
| The variables `Component.concentrations` fixes and the list in `OrderEffect.forConcentration` | read from `OrderVariableReference.invariants` | the component invariants listed three times |
| "At most two components", three times in `OrderAction.effects` and five times in `OrderProcessor.fs` | one predicate in `Order` | eight inline copies |
| The percentile allowed only on the orderable dose quantity, in `Choice.select` and `SetCommand.ofSet` | `SetCommand.ofSet` | the rule written twice |
| `TemporaryHelpers.Shipped.minMax` | not migrated | an alias of `OrderEffect.solve` |
| `processChangeProperty`, its nth and clear arms | deleted in the steps that move the nth (7c) and the recalculation (7a) to the program | a second way to set a variable by command |
| `CommandBridge` | `OrderProcessor.fs`, until the wire sends actions | |

## Decisions

**Table 1. Taken by the maintainer, 2026-10-12 and 2026-10-13**

| Question | Decision |
|---|---|
| The pick list | Disappears from the client. |
| A field the process has no arm for | A recipe for every field the pages can recalculate. #1276 closes with it. The script has no kind for four of the commands (Table 4), so they are refused. |
| The wire and the server recalculation | The picks leave the wire; `Reopen`, `clearedPick` and the put-back leave the processor; `ReopenOrderScenario` leaves the order context. |
| How the server picks the recipe | From the command, which names the field and its component or item. The scan for an unrestricted variable, the tuple pattern, `DoseSolvedAndCleared` and the `process-cleared` step go. |
| Who says which field can be recalculated, 2026-10-26 | The client, alone. One policy in Client.Core states per intent case whether it is allowed on a field, from the wire order the client holds: the values, the defined constraints and the schedule flags. The server sends no mark, on the wire or on the Dto. The server keeps `Recipe.forReCalcCommand` with its None arms as the guard; there is no agreement test, since a drift shows in the code and is fixed at once. |
| What selectable means | The intent case is allowed on this order and the variable holds one value. The rule does not run the recipe: a field another value pins is selectable and comes back with a list of one, shown as any list of one. |
| The intent cases | The wire's `OrderViewCommand` cases, grouped as today (`ReCalcItemProperty of cmp * itm * ItemProperty`); the property types are finite, so the cases are fully specified. The four basic variables and the product strength have no recalculation case, so the type itself says they are never recalculated. |
| The name | `ReCalc`, as the processor's `ReCalcValues` spells it. What sets a value range to unrestricted keeps the name `clear` (`Variable.clear` and the like), and so do the wire commands that set a filter or a diluent to none. |
| The basic variables | The orderable dose rate, the schedule frequency, the component orderable quantity and the orderable dose quantity are the user's levers: stepped or selected, never named by a recalculation command. Their commands go: `ClearOrderableDoseRate`, `ClearScheduleFrequency`, `ClearComponentOrderableQuantity`, `ClearOrderableDoseQuantity`. A recalculation of a derived variable can still set a basic variable back, as the kinds of Table 5 do. |
| The product's constant | An item's concentration in its component is not recalculated: `ClearItemComponentConcentration` goes. Eleven commands remain. Undoing a strength pick then takes a reset of the order; #1500 asks for a way back without one. |
| Script first, also for the client, 2026-10-26 | A Client.Core change that adds or changes behaviour, step 11, is written and tested in a script, reviewed, and migrated in one go when every test passes. A commit is made only when the script is migrated into the source. Deleting code, and the call-site changes a deletion forces, go straight into the source without a script, in any library: steps 13a, 13b and 14. The Fable UI code of `src/Informedica.GenPRES.Client/` is edited directly. AGENTS.md states both exemptions. |
| Where | Its own plan, before the TPN steps. |
| The scope, 2026-10-26 | The complete script is implemented: the recalculation, the set, step and percentile, CalcConstraints, CalcValues and Reset, creation, the impossible values and the split per concentration. The plan decides the order of the steps, not what goes in. |

**Table 2. Proposed, to confirm**

| Question | Proposal |
|---|---|
| Issues for the rest | Every step traces to an issue. The recalculation steps trace to #1453 and #1276. The steps that change the set and step commands, creation and the impossible values change behaviour the document lists in its Table 15; each gets an issue before its step starts, or a reference to #1464 where it measured the case. |
| The design document | `localDocs/order-variables-and-equations.md` is untracked. It moves to `docs/domain/order-variables-and-equations.md` in step 1, so the plan and the code can name its tables. |
| The restore | Stays, as #1034 decided. |
| The second path for a recalculation (#1343) | Folded in: the machines read a recalculation from the command itself, so `ReopenField`, the `reopen` callbacks and the `reopening` refs go; a selectable field dispatches the command. |

**Table 2a. What the script does, implemented as it is**

| Question | The script |
|---|---|
| The trade | The pick list answered "keep what the user set, recompute the rest" for any field, because it knew what the user set. A recipe answers "for this request, this stays", and the elimination of `Determination` sets back every variable the kept ones do not determine: predictable for a prescriber, a domain rule rather than a dialog's memory. |
| The rate stages | A recalculation ends with the rate list offered, without the median (`RateSelection.OfferRates`): after any recalculation of a timed order stage two comes back as a list to pick from. CalcValues and an nth select the median (`SelectMedianRate`), as `rateStageWithMedian` does. |
| The values the order held | Without a pick list nothing is put back, and the pruning of the lists can drop the values the order held, the value of the recalculated field among them. A recalculation adds to every list the value its variable held in the order it received, when that order is complete and the value lies within the calculated constraints. #1498 probed this and moved it here. |
| Selecting from an order with lists | An nth, min, max or median on an order whose lists are made is solved with values at once. Making the lists thins them to the cap and a solve narrows them, and the two do not give the same result in either order: on an order without lists an nth keeps the solve for min and max first, so the chosen value is carried through before any list is thinned; on an order with lists a solve for min and max alone keeps every listed value within the new bounds, also the values the chosen value no longer reaches, so a min, max or median ends with a solve with values; an nth there gives the same order without the solve for min and max, and skips it. |
| A recipe that fails to solve | The action fails and the order before the request is kept: the order context answers `Refused(ctx, NotSolved)` with the context as it was sent. Today's fallback to a plain solve goes. |
| Impossible values | After a recalculation, a list may hold values no complete order reaches when the dose count holds more than one value. `DropImpossibleValues` removes them with a search of at most 1000 solves per value, in parallel. Step 9 reports the time it adds per recalculation on the fixtures. Without a weight the adjust quantity stays open, so a search refutes almost no value and nearly every value stays; that case waits for #1495. |
| The split per concentration | When a component has more than one combination of item concentrations, at most 16, an action runs once per combination, in parallel, and the order offers the union of those that succeed, as a list of at most 200 values, else a range. A value the original order held is added only to the combination it belongs to. |
| The dose count | Never asked, never stepped. The kinds set it back unless the kept variables determine it; on Continuous it is an invariant, 1. Step 8 sets the dose count to 1 only when the rules give it nothing; a rule of "1 or more", as for hydrocortisone IV, leaves it free. A free dose count lets a list offer values no complete order reaches, and step 9 removes those values after a recalculation. |

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
5. **A mark the server derives when the order is sent**, from a pass over the Dto's fields or
   from a fold over the targets in the server mapper, read by the client as a flag per order
   variable. The server would say what a client may do with a field, build the domain order once
   per scenario per answer, and the agreement between the marked places and the places a page can
   ask would need its own tests. The client already holds the values, the defined constraints and
   the schedule flags, which is everything the rule reads. Rejected, 2026-10-26.
6. **Keep the word clear and the five commands.** The code would keep saying a field is emptied
   where it is recalculated, and the basic variables would stay selectable with an answer that
   is a step in disguise. Rejected.
7. **Recipes that list what each kind sets back, held, and frees**, the first version of this
   plan. Every list had to name every variable an equation ties to the asked one, per dose type
   and component count, and one variable held by mistake pins the field. Replaced by 8.
8. **The order program of the script.** A recipe names only what it keeps; `Determination` sets
   back every variable the kept ones do not determine; every command is an action run as a list
   of effects; the shipped commands reach it through a bridge until the wire sends actions.
   Chosen.

## Chosen approach

### The program

An `OrderAction` is what the client asks: `CalcConstraints`, `Set` of a variable and a `Choice`,
`ReCalc` of a variable, `Reset`, `CalcValues`. A variable is named by an
`OrderVariableReference`: the level and property with the names of the component and item.
`OrderAction.effects` turns the action into a list of `OrderEffect` values, decided on the order
as it is; `OrderEffect.run` runs them in turn, and the first failure stops them and keeps the
order before the action. The effects are the operations of the order: set a range back to a
target, set back every variable the kept ones do not determine, constrain a range to a value,
select a value by a choice, solve for min and max or with values, make the lists, make the rate
list, coarsen the increments, record the calculated constraints, set the norm dose, pick the
nearest component quantity, and `Unless` and `Try` for the control flow. Each wraps a shipped
function; the solver and the equations do not change.

The variable a recalculation sets back is never unrestricted: a recipe sets back to the
calculated constraints. So no reader has to find the recalculated variable by its range, and
`isCleared`, `IsCleared`, the tuple pattern and `processClearedOrderWith` lose their purpose.

### The bridge

While the wire sends the shipped commands, `CommandBridge.ofOrderCommand` turns a processor
command into an order and an action, and `processPipelineWith` runs the program for a command
with an action and the shipped processor for the rest. Each step that moves a command adds its
case to the bridge and deletes the shipped code the command used. `OrderContext.evaluateOutcome`
does the same for the order context: the nth and the recalculation of a field run as one action
on the order of the scenario; every other command as today.

### The names

**Table 3. Before and after step 0**

| Today | After step 0 |
|---|---|
| `ClearScheduleTime`, `ClearOrderableQuantity`, `ClearComponentDoseQuantityAdjust`, `ClearItemDoseQuantity`, `ClearItemDoseQuantityAdjust`, `ClearItemDosePerTime`, `ClearItemDosePerTimeAdjust`, `ClearItemDoseRate`, `ClearItemDoseRateAdjust`, `ClearItemOrderableConcentration`, `ClearItemOrderableQuantity` (`Types.fs`) | `ReCalc` for `Clear` |
| `ClearOrderableDoseRate`, `ClearScheduleFrequency`, `ClearComponentOrderableQuantity`, `ClearOrderableDoseQuantity`, `ClearItemComponentConcentration`; `processClearedFrequency` | gone |
| `Domain.ClearOrderValue` (`OrderContext.fs`) | `ReCalcOrderValue` |
| `ClearScheduleProperty`, `ClearOrderableProperty`, `ClearComponentProperty`, `ClearItemProperty` (`Api.fs`) | `ReCalcScheduleProperty`, `ReCalcOrderableProperty`, `ReCalcComponentProperty`, `ReCalcItemProperty` |
| `Ctx.clear` (`Models.fs`), `ViewHelpers.clearCommand` | `Ctx.reCalc`, `ViewHelpers.reCalcCommand` |
| `Arrow.ReopenCleared`, `Click.ClearAndWait` (`FieldOpenPolicy.fs`) | `Arrow.ReCalc`, `Click.ReCalcAndWait` |

`processClearedRate` and `processClearedDose` keep their names until step 13b deletes them; the
recipes that replace them are `Recipe.reCalcTime` and `Recipe.reCalcDose`. Later steps add the
types of Table A, `Determination.fs`, `IntentPolicy` in Client.Core, the rename of
`FieldOpenPolicy.Arrow` to `Selectable` with the cases `ReCalc`, `List` and `No` in step 11, and the
test files of each step.

### The kinds

A recalculation names a variable; `Recipe.forReCalcCommand` gives the kind that recalculates it on
this order. A kind states only what it keeps, and keeps a variable only when it lies within its
calculated constraints, so a variable a step moved outside them is set back with the rest. Every
variable the kept variables that hold one value do not determine is set back to its calculated
constraints, then the order is solved.

**Table 4. The eleven commands and their kind**

| Command | Kind | Dose types |
|---|---|---|
| `ReCalcItemDoseQuantity`, `ReCalcItemDoseQuantityAdjust` | Dose | Once, Discontinuous, Timed, OnceTimed |
| `ReCalcItemDosePerTime`, `ReCalcItemDosePerTimeAdjust` | Per time | Discontinuous, Timed |
| `ReCalcScheduleTime`, `ReCalcItemDoseRate`, `ReCalcItemDoseRateAdjust` | Time | Continuous, Timed, OnceTimed |
| `ReCalcOrderableQuantity`, `ReCalcComponentDoseQuantityAdjust`, `ReCalcItemOrderableQuantity`, `ReCalcItemOrderableConcentration` | none: the script has no kind for them, so the action is refused | |

An item dose of a component whose dose the rules do not constrain, at the component or at any of
its items, has no kind: it only follows from the others (`Recipe.isComponentDoseConstrained`).

**Table 5. What each kind keeps**

| Kind | Recalculates | Kept | By dose type |
|---|---|---|---|
| Dose | the orderable dose quantity | the frequency; the order count; the invariants: the adjust quantity, every component quantity, every item's component quantity and concentration, and on Continuous the dose count. The preparation is never kept: a kept preparation limits the dose to the preparation divided by the dose count, one value when the dose count holds one | on Timed and OnceTimed the time and the rates are set back with the dose |
| Per time | the orderable dose quantity and the frequency | as Dose, without the frequency; with one component whose dose quantity the definition fixes, also without that component's product: its quantity and every item's component quantity and concentration | Discontinuous, Timed |
| Time | the orderable dose rate | the frequency; the order count; the invariants; the orderable dose quantity where it is basic, unless the time is fixed or the preparation lies outside its constraints; every component orderable quantity unless the preparation lies outside its constraints or the time is fixed | Continuous, Timed, OnceTimed |

A recalculation then runs, in order: the recipe; a solve for min and max; after Dose and Per time
the increments coarsened to at most 100 values; the lists made with the larger cap and the values
the order held added; the order solved with values, the nearest component quantity picked and the
rate list offered; a solve with values; step 9 adds the drop of the impossible values.
`Schedule.isTimeFixed` reads the calculated constraints of the time.

The step and percentile recipes of the four basic variables (the document's Table 13a) free every
variable the kept ones do not determine to any positive value on its defined increment, except
the target, and then set the floors they name. They replace the four shipped step functions and
`orderPropertySetPercOrderableDoseQuantity`.

### The client decides what a field offers

One policy in Client.Core, `IntentPolicy`, beside `FieldOpenPolicy`, answers for an intent case and
the wire order whether the intent is allowed. It reads nothing the wire does not carry today: the
server mapper writes the defined and the calculated constraints on every order variable
(`ServerApi.Mappers.Order.fs`), the values are `Variable.Vals`, and `Schedule` carries the dose
type as five flags. Nothing is added to the wire, the Dto or the stored JSON.

The rule per intent case, as Table 4 has it, in wire terms:

- A recalculation is allowed only on a field that holds one value (`Vals` of length 1, as
  `PickListPolicy.valuesOf` counts today). Several values are selectable as a list without a
  request; a range is not selectable.
- `ReCalcItemProperty` with `DoseQuantity` or `DoseQuantityAdjust`: on Once, Discontinuous, Timed
  and OnceTimed. With `DosePerTime` or `DosePerTimeAdjust`: on Discontinuous and Timed. With
  `DoseRate` or `DoseRateAdjust`, and `ReCalcScheduleProperty Time`: on Continuous, Timed and
  OnceTimed.
- An item dose of a component the rules do not constrain is never allowed: no dose variable of the
  component, and none of any of its items, has defined constraints. The check mirrors
  `OrderVariable.hasConstraints`: constraints that are empty, or non-zero positive alone, do not
  count.
- `ReCalcOrderableProperty Quantity`, `ReCalcComponentProperty DoseQuantityAdjust`,
  `ReCalcItemProperty OrderableQuantity` and `ReCalcItemProperty OrderableConcentration`: never
  allowed, the four commands of Table 4 without a kind.
- The four basic variables and the product strength have no recalculation case once step 13a has
  deleted their five commands, so from then on the type says it. Until then the wire still carries
  the five, so the policy refuses them explicitly, with a test; step 13a deletes that arm with the
  commands. A step is its own intent case, allowed on one value with a defined increment. The
  component orderable quantity is selectable by its own intent case, so a page can offer it; when
  the server cannot answer that intent, that is fixed on the server.

The server keeps `Recipe.forReCalcCommand` with its None arms as the guard: a recalculation the
client should not have sent is refused as `NotSolved`, the field keeps its one value and the client
shows its "not solved" text (`OrderContextRefusalPolicy`). That message is accepted as it is: the
client never sends such a request, so the text is the visible sign of a drift. There is no
agreement test between the policy and the recipes: a drift is visible in the code and is fixed at
once. `Target` and `propertyCommands` stay as they are.

After a recalculation the field holds a list and is selectable as a list. A restore puts the kept
state back, the one value included.

### The contract, the server and the client

The four wire commands lose their `picks`, and `toString` its count. The wire order variable does
not change. `Ctx.reCalc`, `Api.preview`, `Picks.withinOrder` and `Picks.ofOrder` lose the picks or
go.

`PickListPolicy` goes. `FieldOpenPolicy.Constrained` goes; `orderVariable` takes the policy's
answer: one value and allowed gives `Selectable.ReCalc`, one value and not allowed
`Selectable.No`, several values `Selectable.List`, as the #1034 table has it; step 11 renames the
type from `Arrow`. `Views/Order.fs` loses `picks`, `heldPicks`, the picks half of
`sentFrom` and the pick bookkeeping; `reopenOf` in `Order.fs` and `NutritionSlot.fs` asks
`IntentPolicy` for the field's recalculation case over the order shown. `ViewHelpers.Reopen` loses
`constrained`. With #1343 folded in, the machines keep the state before a recalculation when the
command is one of the four wire ReCalc commands, read by one predicate, `reopens`; `ReopenField` on
both machines and both environment interfaces, the `reopen` prop of `Order.View`, the `planReopen`
prop of `useSlot` and its pass-through in the three nutrition views, and the `reopening` refs go.
`RestoreField` and `Kept` stay. The trail prints the command name, so it follows the rename.

### The tests

The script's tests move with the code each step migrates, to `tests/Informedica.GenORDER.Tests`: the
helper tests to the files of their modules, `Determination` to `DeterminationTests.fs`, the program
to `OrderProgramTests.fs`, the laws to `OrderLawsTests.fs` over the fixtures of
`TestsAndFixtures.Fixtures`, with `Scenarios.fs` gaining the fixture changes they make (a dose count
of at least one, a maximum volume, a fixed time) and `hydrocortisonIvText`, the hydrocortisone IV
fixture with a free dose count, written as text like `amoxClavText` and added in step 8. The names
of the document and the bridge between them and `OrderVariableReference` (`TemporaryHelpers.VarId`,
`Vars`, `Bridge`) move to a test helper file, since the laws read the document's ids. The
comparisons with the shipped recipes (`TemporaryHelpers.Shipped`, `ShippedRecipe`,
`TestsAndFixtures.Migration`) run in the script before each step and go with the shipped code they
compare against; they never reach the test project.

## Checked, no change needed

- **The signing digest** is over the parsed typed order (`ServerApi.Session.fs`); nothing is added
  to the Dto or the wire order, so neither the digest nor the stored JSON moves.
- **The golden orders** are printed orders, not Dto JSON.
- **The stored rows hold no commands**: only the composition root and the command handler see
  `OrderViewCommand`, so the rename costs no compatibility.
- **`Target` addresses variables by place**, so the policy needs no name.
- **The dialog's editing gate** stays in front of the policy: `selectFor` asks
  `PlanContextPolicy.canEdit` first (`Order.fs`).
- **The MCP host and the benchmarks** use neither the commands nor the pick list.
- **The two #1425 tests** that empty the median rate, tpn and the once timed KCl, go through
  `Reopen` and `rateNotAPick` today. The orderable rate is no longer asked; in step 7a they become
  tests of the item rate recalculation, the same stage two, and must give the same list.
- **The solver and the equations** do not change: the program calls the shipped solve, and the
  equations the document changes (its Table 14) are not in the script.

## Confidence

High for the client: it removes code and reads one policy over the order it already holds. High for the routing: the action names
the variable the detection had to find. High for the recipes' shape: the script runs the laws of
the document over the fixtures, the containment, the narrowing, the determination, the offered
values, the complete orders, and compares every recipe with the shipped one. Medium for what a
prescriber sees: a kind keeps what it names and sets back the rest, so a field another kept value
pins comes back as a list of one; the laws count those. Medium for the size: the program is about
1 600 code lines, about 2 400 with its documentation, so the GenORDER part takes sixteen pull
requests, each built and released on its own.

## Steps

One PR per step, after the maintainer's go. Each step builds and is safe to release on its own:
step 0 changes names only; the program runs only for the commands the bridge maps, so steps 2 to
6 add code nothing calls yet; step 7a moves the eleven recalculation commands and keeps the picks
parameter of `ReCalcOrderValue` for the five commands of the basic variables and the product
strength, which stay on the old `Reopen` path until the client stops offering them in step
12; step 13a changes the wire and the client call sites together; steps 12 and 14 add before they
remove. Source lines are shipped code under `src/`,
tests and scripts excluded. Every step follows the migration rules and Table B, and the
maintainer's review checks them. Every `.fs` change outside `src/Informedica.GenPRES.Client/` that
adds or changes behaviour comes from a script, reviewed and migrated when the maintainer asks: the
GenORDER steps from the order program's script, step 11 from its own; the step's tests pass in the
script first, then in the test project. A deletion, and the call-site changes it forces, goes
straight into the source (Table 1): steps 13a, 13b and 14. The step that moves the last part of the
program deletes its script.

0. **The names.** Table 3, across GenORDER (`Types.fs`, the arms of `processChangeProperty`
   at `OrderProcessor.fs`, `ClearOrderValue` with `Command.get` and `toString`), Shared
   (`Api.fs`; `Models.fs`), the server mapper (`propertyCommands` with its
   recalculation half an option, `toDomain` giving no command for a target without one), the
   client (`ViewHelpers.clearCommand` and its callers at `Order.fs`, `NutritionSlot.fs`;
   `FieldOpenPolicy.fs`) and the tests that spell the names
   (`Tests.fs`, `OrderValueCommandTests.fs`, `DomainCommandTests.fs`, `MappersOrderContextTests.fs`,
   `OrderValueAgreementTests.fs`, `OrderContextCommandTests.fs`, the `FieldOpenPolicy` and trail
   tests); `Issue1425Staged.fsx`, `docs/domain/genorder-order-fsm.md` and the script's
   `CommandBridge` follow. The five commands keep their `Clear` names and their path until step
   13a; only `processClearedFrequency`, which nothing calls, goes here. No behaviour changes.
   About 100 lines.
   `refactor(api): rename the clear commands ReCalc`.

1. **Shipped refactors and the caps of a timed order.** `Order.equationMapping` out of
   `Order.solve`, which then calls it; `rateWaits`, `isComposed` and `isPrepWithinConstraints` moved
   from `OrderProcessor` to `Order`; `OrderContext.processScenarioOrder` takes an order function
   instead of a command, so the program's scenario action is a call of it rather than a copy; the
   predicate for at most two components replaces its inline copies. One change in behaviour:
   `Order.ValueCaps.defaults` swaps the two caps of a timed order, `TimedFew` 10 and `TimedMaxFew`
   20. A timed or once timed order with at most two components lists at most 10 values where it
   listed 20 when a scenario is selected, and at most 20 where it listed 10 after a reset, an nth or
   a recalculation; doses, component quantities and rates alike. The norm dose at creation is the
   median of a list made with the larger cap, so on such an order it is taken from up to 20 values
   instead of 10 and can change. It needs its own issue. The design document moves to
   `docs/domain/`. The script drops its wrappers, its copies and its `caps` override. Tests: the
   larger cap of every dose type is at least its cap to select from; the norm dose of every timed
   fixture before and after the swap, so a change shows in the pull request. About 75 lines.
   `fix(genorder): swap the value caps of a timed order`.

2. **The helpers.** Where Table B puts them: `List.sequenceOption` to Utils;
   `ValueUnit.collectValues` to GenUnits; `ValueRange.tryToValueSet` to GenSolver; `Shape` to
   `Types.fs`; the `OrderVariable` helpers with `Count.one`, the count setters calling the new
   ones; the `Order` helpers in the modules of their level. Not the split and not
   `minIncrMaxToValuesWithOriginal`. Tests: each helper on its success and its failure path, from
   the script's helper tests. Under 200 lines.
   `feat(genorder): add the order variable helpers of the order program`.

3. **`Determination`.** `Determination.fs` after `Order.fs` in the project file, keyed by the
   solver's `Name`: the model of an order's equations,
   `rref`, `closure`, `undeterminedBy`. Tests from the script's helper tests: a chain of single
   equations, a variable only several product equations fix, a sum with one open term, the
   constants, a kept variable with more than one value. About 140 lines.
   `feat(genorder): which variables the kept variables determine`.

4. **The program's types and their small modules.**
   - **4a.** The types of Table A in `Types.fs`, each with its documentation. About 170 lines.
     `feat(genorder): add the types of the order program`.
   - **4b.** `OrderVariableReference.tryGet`, `isBasic`, `invariants`; `Choice.select` with the
     percentile index (count − 1) × percentage ÷ 100; `SetBackTo.apply`; `OrderCondition.holds`;
     `Orderable.Dose.toOrdVar`. Tests: `tryGet` for every reference on every fixture, `select` per
     shape and choice of the document's Table 9, `apply` per target. About 200 lines.
     `feat(genorder): select and set back an order variable by reference`.

5. **The interpreter.** `OrderEffect.apply` and `run`, without `DropImpossibleValues` and without
   the split; the set-back names of an effect as one function. `Order.minIncrMaxToValues` becomes
   the script's `minIncrMaxToValuesWithOriginal`: it takes the original order as an option and, once
   every list is made, adds the values that order held when it is complete (`Order.isReachable`);
   the callers in the shipped processor pass none. Tests: each effect on a fixture; a failure keeps
   the order before it; `Try` keeps the order; `includeOriginalValue` adds the value in order and in
   the unit of the list, adds nothing outside the calculated constraints, from an original that is
   not complete, or to a range. About 180 lines.
   `feat(genorder): run an order action as a list of effects`.

6. **The recipes.**
   - **6a, the steps and the percentile.** `SetCommand.settable` and `ofSet`; the recipes of the
     four basic variables and of the percentile. Tests: the script's comparison with the shipped
     step functions on every fixture, kept in the script; in the test project, the laws that hold
     after a step (containment outside the freed variables, the increment kept). About 150 lines.
     `feat(genorder): the step recipes of the order program`.
   - **6b, the recalculation kinds.** `reCalcDose`, `reCalcPerTime`, `reCalcTime`,
     `keptWithinConstraints`, `forReCalcCommand`; `Orderable.Dose.isConstrained` and
     `Orderable.Component.isDoseConstrained` in `Order.fs`. Tests: Table 4 for
     every command and dose type; the kept variables unchanged after the recipe (locality); the
     recalculated variable undetermined by the kept ones (underdetermination). About 150 lines.
     `feat(genorder): the recalculation kinds of the order program`.

7. **The program takes over the commands**, through the bridge.
   - **7a, the recalculation.** `OrderAction.effects` and `run` for `ReCalc`; `CommandBridge` with
     the eleven recalculation commands, the five others left to the old path; `processPipelineWith`
     running the program for them; `ReCalcOrderValue` evaluated through `processScenarioOrder` with
     the program, its picks parameter kept for the five; the clear arms of `processChangeProperty`
     for the eleven go. Tests in `OrderReCalcTests.fs`: per fixture and command, the field gets a
     list of more than one value where the script's laws found one, the kept variables stay, the
     value the field held is in its list when it lies within its calculated constraints, the order
     solves again after a pick; the two #1425 tests as item rate recalculations; the offered values
     law over the fixtures. About 150 lines. Closes #1276: the item dose by the Dose kind, the
     frequency by the decision that it is stepped, not recalculated.
     `fix(genorder): a recalculation changes what depends on the field`.
   - **7c, the set.** The `Set` action for the step, the percentile and the min, max, median and
     nth commands; the shipped step functions, `orderPropertySetPercOrderableDoseQuantity`, the nth
     arms of `processChangeProperty` and `Count.setToOne` and `Count.setMinToOne` go.
     Behaviour changes, each a row of the document's Table 15: a select runs no recipe, a step
     keeps the defined increment, the percentile 0 gives the first value and 100 the last; an nth
     on an order that already has lists is solved with values at once, without the solve for min
     and max; a min, max or median on such an order ends with one solve with values. Table 15 gains
     the last two rows in this step. Its own issue.
     About 150 lines, half removed. `feat(genorder): set an order value through the order program`.
   - **7d, the calculations.** `CalcConstraints`, `CalcValues` and `Reset` through the program;
     what remains of the shipped pipeline is `IncreaseIncrements` and `SolveOrder`. The laws over
     the fixtures in `OrderLawsTests.fs`. Its own issue. About 150 lines, half removed.
     `refactor(genorder): calculate an order through the order program`.

8. **Creation writes the counts.** In `Medication.toOrder`: the order count as a defined
   constraint, exactly 1 on Once and OnceTimed, else whole and at least 1; the dose count 1 when
   the rules give it no minimum, maximum or values. `Scenarios.hydrocortisonIvText`, read with
   `MedicationText.fromString`. The laws of the counts. Its own issue, refs
   #1463 and #1487. About 40 lines. `fix(genorder): creation writes the order and dose counts`.

9. **The impossible values.** `CompleteOrder` and `DropImpossibleValues` at the end of a
   recalculation; the pull request reports the time it adds per recalculation on every fixture,
   the TPN fixtures among them. Refs #1464; the case without a weight waits for #1495. About 170
   lines.
   `fix(genorder): a recalculation offers only values a complete order reaches`.

10. **The split per concentration.**
    - **10a, the helpers.** `List.combinations`, `ValueRangeCase.unionRanges`,
      `OrderVariable.fixToValues`, `tryValueList` and `unionPerConcentration`,
      `Component.concentrations`. Tests: the combinations of products and of strengths, a group of
      products with the same concentrations kept together, a combination that keeps no value
      dropped, the union of lists, of ranges and past the cap. About 150 lines.
      `feat(genorder): the item concentrations of a component per product`.
    - **10b, the split.** `Order.splitPerConcentration`, `Order.unionPerConcentration`,
      `OrderEffect.forConcentration` reading the invariants from
      `OrderVariableReference.invariants`; `OrderAction.run` solving per combination in parallel and
      offering the union. Tests: an order with one combination runs as before; an order with more
      offers no value of one combination with the concentration of another; the laws over the
      fixtures. About 100 lines. The script is deleted here.
      `feat(genorder): solve an order per combination of concentrations`.

11. **The intent policy, in a script.** A script in `src/Informedica.GenPRES.Client.Core/Scripts/`
    holds `IntentPolicy` with its tests: every recalculation case on every dose type, as the rule
    section lists them, over wire orders built as the policy tests build them; a field with
    several values or a range; an item dose of a component the rules do not constrain, with
    constraints that are empty or non-zero positive alone; the four cases without a kind; a step
    on one value with and without a defined increment; the five commands of the basic variables
    and the product strength refused explicitly while the wire still carries them, an arm step 13a
    deletes. In the same script, `FieldOpenPolicy.orderVariable` takes `allowed: bool` in place
    of `constrained`, tested over the #1034 table; `FieldOpenPolicy.Arrow` is renamed `Selectable`
    with the cases `ReCalc`, `List` and `No`, its callers in the views following in step 12; and
    `reopens` is a policy predicate over the four wire ReCalc commands, tested. The two machines keep `Kept` on a recalculation as they do on `ReopenField`, tested in
    `OrderContextMachineTests.fs` and `OrderPlanMachineTests.fs`. Nothing changes in GenORDER, the
    server or Shared. The script is reviewed and migrated in one go when every test passes; the
    commit is made then. Under 150 lines.
    `feat(client): the intent policy says which field offers a recalculation`.

12. **The views ask the policy.** A direct edit of the Fable UI code, as AGENTS.md allows.
    Additive, the pick list still works beside it: `ViewHelpers.fs` passes the policy's answer;
    `reopenOf` in `Order.fs` and `NutritionSlot.fs` asks `IntentPolicy` for the field's
    recalculation case over the order shown. The four basic variables and the product strength
    have no recalculation case, so they stop being selectable for a recalculation here; the
    nutrition slot keeps its steps on the component orderable quantity, the dose quantity, the
    frequency and the rate, and the component orderable quantity stays selectable by its own
    intent case. Fable compile check, `npx vite build`. Under 100 lines.
    `feat(client): the field asks the intent policy whether it can be recalculated`.

13. **The wire loses the picks and the old path goes.**
    - **13a, the wire.** A removal, no script (Table 1). The four commands without `picks` and `toString` without
      the count; `ReCalcOrderValue` without its picks parameter and the mapper without
      `Picks.ofOrder`; `Ctx.reCalc` and `Api.preview` without picks, `Picks` gone; the client call
      sites the contract breaks, `ViewHelpers.reCalcCommand` and its callers in `Order.fs` and
      `NutritionSlot.fs`, and `Picks.withinOrder` in `Order.fs`. The five commands go:
      `ClearOrderableDoseRate`, `ClearScheduleFrequency`, `ClearComponentOrderableQuantity`,
      `ClearOrderableDoseQuantity`, `ClearItemComponentConcentration`, with their cases in
      `OrderValueCommandTests.fs` and `MappersOrderContextTests.fs` and the strength test in
      `Tests.fs` (#1500). `MappersOrderContextTests.fs` keeps its other wire cases with the new
      shapes; the `Ctx.reCalc` tests in `OrderContextCommandTests.fs`, `OrderValueAgreementTests.fs`
      and the trail tests follow. Under 150 lines.
      `refactor(api): a recalculation carries no picks`.
    - **13b, the old path.** A removal, no script (Table 1). `Reopen`, `clearedPick`, the put-back and `rateNotAPick`;
      `processClearedOrderWith`, `processClearedRate`, `processClearedDose`, the tuple pattern and
      its comment; the `process-cleared` step, the `isCleared` branch and `IsCleared`;
      `ReopenOrderScenario` and `reopenScenarioOrder`, `Command.get` and `toString` following.
      `OrderReopenTests.fs` goes, its cases already in `OrderReCalcTests.fs` since 7a, and so do
      the reopen cases in `Tests.fs`. Under 200 lines, nearly all removed.
      `refactor(genorder): the reopen leaves the processor`.

14. **The pick list goes.** A removal, no script (Table 1). `PickListPolicy` and its tests; `Constrained`;
    `ReopenField` on both machines, their `admitted` arms and the trail's; the `ReopenField`
    members of the two environment interfaces and `App.fs`; the `reopen` and `planReopen` props and
    their pass-through; the pick state of `Order.fs`; the `reopening` refs. Fable compile check,
    `npx vite build`; the maintainer checks in the browser. Under 200 lines, nearly all removed.
    `refactor(client): recalculate a field without a pick list`. Closes #1453 and #1343.

Steps 7c to 10b can follow steps 11 to 14: the recalculation and the client need only
steps 0 to 7a.

## Left out

- **A rule that runs the recipe**, so a field pinned by another on this order is not selectable:
  a solve per variable per answer. The list of one is the answer for now.
- **What the script does not hold.** A kind for the orderable quantity, a component's dose per kg
  and an item's orderable quantity and concentration; the equations the document changes (its
  Table 14); the solver changes of its addendum.
- **Impossible values without a weight.** Without a weight the search keeps nearly every value;
  #1495.
- **The dose count, the four basic variables and an item's component concentration as
  recalculated fields**, by the maintainer's decision.

## Verification

- `dotnet run build`, `dotnet run servertests`, `CI=true` in a worktree
- before each GenORDER step, the script's tests and its comparisons with the shipped code pass;
  the baseline, measured on 2026-10-26: 3,469 passed, 7 ignored, 0 failed, the quick run without
  `--full`. A step may add tests, never turn one red or ignored
- `dotnet fsi scripts/CheckDependencyRule.fsx` after any step that changes a project reference
- `dotnet fantomas --check`
- in the browser, after step 14: the paracetamol suppository in the workbench is selectable for a
  recalculation on the fields Table 4 gives a kind and on no other, the frequency and the dose
  quantity in mL among the latter; recalculate the paracetamol dose in mg: its list opens, the
  frequency stays; close the list without a pick: the order as before. The plan dialog over the
  same order: the same fields. The continuous morphine: the rate in mL/h is not selectable, the
  morphine in mcg/kg/h is; recalculate it: its list opens, the volume stays. A TPN composed by
  hand: no mL/kg is selectable for a recalculation; a component quantity is selectable by its own
  intent case
