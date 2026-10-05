# Implementation plan for issue #1224: specific order context commands

Every request the client sends names what the user did, *set the 2nd value of the frequency*,
instead of sending a changed copy of the order context. The existing `OrderContextCommand`
(`src/Informedica.GenPRES.Shared/Api.fs`) gets the missing cases in its own naming convention. A
scenario can then be played as a sequence of commands over a starting context, in a test or in
FSI, exactly as it happens in the user interface, knowing only how many values a field offers,
never the values themselves.

The work hangs under [#1224](https://github.com/informedica/GenPRES/issues/1224). Its deferred
replay needs a trail that can be fed back, and names as the hard part the codecs for the order
context and the order plan inside the messages. A command that names the change carries no changed
context, so this plan is also the groundwork for that replay. No other issue asks for this.

- [Problem description](#problem-description)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [Decisions](#decisions)
- [End state: what the full implementation gives](#end-state-what-the-full-implementation-gives)
- [Out of scope: a rule set version per order context](#out-of-scope-a-rule-set-version-per-order-context)

## Problem description

Part of `OrderContextCommand` already names the change: the 25 property cases, such as
`DecreaseOrderableDoseQuantityProperty of ntimes: int * useCalc: bool`, travel beside the context
and tell the server which variable to step or set. The rest say *here is the new state, work it
out*:

| Case | What the client sends beside it | What the server knows |
|---|---|---|
| `UpdateOrderContext` | the context, its `Filter` changed by `OrderContext.indicationChange` … `doseTypeChange` (`Shared/Models.fs:2159`) | a new filter, not which field changed |
| `UpdateOrderScenario` | the context, one variable of the order narrowed to the picked value by `setVu`, `setVar`, `setOvar` (`Shared/Models.fs:1635`) | nothing about which variable was picked; `SolveOrder` solves what it gets, and `OrderProcessor.fs:25` depends on only one variable changing at a time |
| `SelectOrderScenario` | the context, narrowed to one scenario and its form (`Prescribe.fs:233`) | the narrowed context |
| `ReopenOrderScenario picks` | the context with one variable cleared by `setOvar None` | the cleared variable by its state |

`OrderPlanCommand.Navigate` carries an `OrderContextCommand`, so the plan and nutrition send the
same cases.

A test of a sequence of user steps therefore has to build each changed context by hand, with the
values in it, and a scenario cannot be played as a sequence of commands alone.

This plan is the first, behaviour-neutral step towards a command structure in which every scenario
plays as it happens in the user interface. It covers every filter, scenario and order action,
played one after another on the fixed medications of the tests. Each remaining gap closes by
adding to it, without undoing it: patient edits, overlapping requests (the trail of #1224),
signing, and a rule set version per order context (#1247).

## Approaches considered

1. **A new command type beside `OrderContextCommand`**, with its own plan command, and the 25
   property cases folded into one case with an address. A second path for the same thing, and a
   larger change that is harder to prove identical.
2. **Extend `OrderContextCommand`, one case per variable**, in the style of
   `SetMinScheduleFrequencyProperty`: the variable in the case, the component and item located by
   their names. Follows the convention to the letter, but 15 set and 15 clear cases.
3. **Extend `OrderContextCommand`, one case per level, the variable as a property type.** The
   schedule, the orderable, a component and an item each get a set and a clear case; the
   variable is a case of a small property type of that level; the component and item are located
   by their names, as in approach 2.
4. **Extend `OrderContextCommand`, one case with the variable's name.** Fewest cases, but a
   free-text address that no other command uses, and a full name that holds the order id.

## Chosen approach

Approach 3, as a pure refactor: from the user's perspective the app runs as before.

### The new cases

The property types, one per level, cover every value the dose dialog (`Views/Order.fs:29`) and the
nutrition slot (`NutritionSlot.fs`) let the user pick:

```fsharp
[<RequireQualifiedAccess>]
type ScheduleProperty =
    | Frequency
    | Time


[<RequireQualifiedAccess>]
type OrderableProperty =
    | Quantity
    | DoseQuantity
    | DoseRate


[<RequireQualifiedAccess>]
type ComponentProperty =
    | OrderableQuantity
    | DoseQuantityAdjust


[<RequireQualifiedAccess>]
type ItemProperty =
    | DoseQuantity
    | DoseQuantityAdjust
    | DosePerTime
    | DosePerTimeAdjust
    | DoseRate
    | DoseRateAdjust
    | ComponentConcentration
    | OrderableConcentration
    | OrderableQuantity
```

```fsharp
type OrderContextCommand =
    // ... the existing cases, unchanged
    /// The nth value of a filter field's options, counted from 0.
    | SetNthFilterProperty of field: OrderContext.FilterField * nth: int
    /// A filter field emptied, with the choices below it.
    | ClearFilterProperty of field: OrderContext.FilterField
    /// The whole filter emptied.
    | ClearAllFilterProperty
    /// The nth diluent of the options, counted from 0.
    | SetNthDiluentProperty of nth: int
    | ClearDiluentProperty
    /// The components at these positions of the options, counted from 0.
    | SetNthComponentsProperty of nth: int[]
    /// The nth scenario, counted from 0, and its form.
    | SelectNthOrderScenario of nth: int
    /// The nth value of a schedule property, counted from 0.
    | SetNthScheduleProperty of property: ScheduleProperty * nth: int
    /// A schedule property cleared, with the variables the user picked or stepped,
    /// in the order picked.
    | ClearScheduleProperty of property: ScheduleProperty * picks: string[]
    | SetNthOrderableProperty of property: OrderableProperty * nth: int
    | ClearOrderableProperty of property: OrderableProperty * picks: string[]
    | SetNthComponentProperty of cmp: string * property: ComponentProperty * nth: int
    | ClearComponentProperty of cmp: string * property: ComponentProperty * picks: string[]
    | SetNthItemProperty of cmp: string * itm: string * property: ItemProperty * nth: int
    | ClearItemProperty of cmp: string * itm: string * property: ItemProperty * picks: string[]
    /// The argumentation the user writes for a deviation from the rules.
    | SetArgumentationProperty of text: string
```

- **Naming:** the verb, the thing and `Property`, as the existing cases; `SetNth` beside `SetMin`,
  `SetMax` and `SetMedian`, after `setNthValue` in the domain; `SelectNthOrderScenario` beside
  `SelectOrderScenario`.
- **`FilterField` is the existing one** (`Shared/Models.fs:2031`: indication, generic, route, form,
  dose type). The diluent and the components stand outside it today (`diluentChange`,
  `componentsChange` do not use `applyChange`), so they keep cases of their own.
- **One case per level, the variable as a property type, located as the order processor locates
  it.** The schedule and the orderable need no name, a component its name, an item the names of its
  component and itself. That is how the existing minimum, median and maximum cases locate a variable
  (`SetMinComponentOrderableQuantityProperty cmp`), and how the domain does it:
  `OrderPropertyChange` (`Types.fs:248`) has one case per level and locates a component or item by
  these names (`applyToComponents`, `applyToItems`). A new property is a new case of the type, not
  of the command. No case holds the order id, a new GUID each time the scenarios are made
  (`Medication.fs:1252`), so a sequence of commands plays the same in every run, and the command
  says what changes without the dialog's selection.
- **The picks of a clear** are the variable names within the order, without the order id, since
  the server's reopen compares them by name (`OrderProcessor.clearedPick`); `toChange` puts the id
  of the one scenario's order back.
- **The argumentation is the one value command.** The user writes it in the dose dialog
  (`Views/Order.fs:1155`); it is text, not an index, but the server never changes it, so it plays
  as a command all the same.
- **No case for the component or item the dose dialog shows** (`ChangeComponent`, `ChangeItem` in
  `Views/Order.fs`). That selection is view state: the client writes it into the scenario's
  `Component` and `Item` (`ViewHelpers.withLoader`) so that the dialog reopens on the same view;
  the server only carries it through its mappers (`ServerApi.Mappers.OrderContext.fs:178`, `:207`)
  and its debug text (`OrderContext.fs:843`), and no order processing reads it.

### Where the new cases are turned into today's

A pure function on the contract side turns a new case and the context it came with into an old
case and the context the client builds today, or, for the argumentation, the context alone:

```fsharp
/// The command as the server evaluates it today and the context the client sent with it today;
/// no command when the context is answered as it is.
toChange :
    OrderContextCommand -> OrderContext -> Result<OrderContextCommand option * OrderContext, string>
```

| New case | Becomes |
|---|---|
| `SetNthFilterProperty(f, n)` | `UpdateOrderContext`, the context through the change function of `f` with option n |
| `ClearFilterProperty f` | `UpdateOrderContext`, the context through the change function of `f` with `None` |
| `ClearAllFilterProperty` | `UpdateOrderContext` with `OrderContext.empty`, as `Prescribe.fs:121` sends today |
| `SetNthDiluentProperty n`, `ClearDiluentProperty` | `UpdateOrderContext`, through `diluentChange` |
| `SetNthComponentsProperty ns` | `UpdateOrderContext`, through `componentsChange` |
| `SelectNthOrderScenario n` | `SelectOrderScenario`, the context narrowed as `Prescribe.fs:233` does |
| `SetNthScheduleProperty`, `SetNthOrderableProperty`, `SetNthComponentProperty`, `SetNthItemProperty` with n | `UpdateOrderScenario`, the variable through `setOvar` with value n |
| `ClearScheduleProperty`, `ClearOrderableProperty`, `ClearComponentProperty`, `ClearItemProperty` with the picks | `ReopenOrderScenario picks`, the variable through `setOvar None`, the picks with the order id put back |
| `SetArgumentationProperty text` | no command: the context with the text written (`ArgumentationPolicy.write`), answered without evaluation |
| every existing case | itself, the context unchanged |

The server calls it first in `ServerApi.OrderContextCommand.processCmd` and in the `Navigate`
branch of `ServerApi.OrderPlanCommand.processCmd`; everything after it runs as today. The domain
(`OrderContext.Command`, `OrderProcessor`) does not change.

`toChange` is a stop-gap, and its code comment names #1224: it gives the specific commands and the
playing of a scenario now, without a change to order processing, and it shrinks as the domain
learns to process the commands as the client gives them (see [Decisions](#decisions), *Later*).
When it has no cases left, it goes.

It needs, on the contract side:

- **a variable of an order found and changed by its level, property, component and item**: one
  function per level. The dose dialog has them already as `over`, `overComponent` and `overItem`
  (`Views/Order.fs:171-222`), and the nutrition slot its counterparts; they move to `Shared`;
- **the scenario narrowing** of `Prescribe.fs:233`, moved to `Shared` as a function;
- **`count`**, how many values a filter field, the scenario list or a variable offers, the variable
  found by the same functions; `PickList.valuesOf` (`Client.Core`) is the counting half of it;
- **`ArgumentationPolicy.write`** (`Client.Core`), which normalises and shortens the text, moved to
  `Shared`;
- **`replaced`**, the old case a new case stands for, without the context
  (`SetNthItemProperty …` gives `UpdateOrderScenario`, `ClearItemProperty(…, picks)` gives
  `ReopenOrderScenario picks`), for the client machines below.

### What keeps it a pure refactor

- **The client's own functions run, only on the server.** `toChange` calls the same `Shared`
  functions the client calls today, so the context it builds is the context the client sent.
- **The machines treat a new case as the case it replaces.** They decide by case what a command
  does while a request is under way, and a new case left out of those decisions falls to the
  default:
  - `Dialog.waits` (`OrderPlanMachine.fs:73`) drops `UpdateOrderContext` and `SelectOrderScenario`
    and lets every other case wait: a filter pick would wait instead of being dropped;
  - `Dialog.carries` (`OrderPlanMachine.fs:82`) sends `UpdateOrderScenario`, `ReopenOrderScenario`
    and `ResetOrderScenario` over the dialog's own context and every other case over the answered
    one: a value pick or a clear would go over the answered context;
  - `OrderContextMachine.fs:423` gives `UpdateOrderContext` its own `evaluate` path;
  - `ArgumentationPolicy.clearedBy` lets only a reset clear the argumentation.

  Each of them matches on `replaced cmd` instead of `cmd`, so every choice of context stays as it
  is today.
- **The page still shows the change while it runs.** The page shows the context sent while a
  request is under way (`OrderContextState.view`, `Changing`) and keeps it, its picks included,
  after a refusal (`OrderContextWorkbench.refused`). The client now puts the context as it is on the
  wire, beside the command, so the machine keeps `toChange cmd ctx` as the context sent: `toChange`
  is in `Shared`, so the client runs it too. Without this the picked value would vanish during the
  request, and a filter pick after a refusal.
- **A clear keeps its own path in the client:** the reopen message (`OrderContextMsg.Reopen`,
  `OrderContextMachine.fs:188`, `:558`) and the dialog's `reopenOf` (`Views/Order.fs:1089`).
- **The argumentation is still written in the client** as today (the `Argue` message) and travels
  with the next command; the client sends no request for it, so no request is added.
  `SetArgumentationProperty` is what a played scenario uses for that step, and the trail shows the
  `Argue` step as it.
- **The cases the UI cannot produce stay invisible:** an index out of range is an error; an index on
  a variable that offers no list leaves the context unchanged.
- **The old cases stay** in the shared type for as long as `toChange` returns them, so a tab open
  across a deployment that still sends them behaves as today.

What changes that the app user does not see: the debug-only trail and the server's request log
(`OrderContextCommand.toString`, `Shared/Api.fs:69`, logged by `ServerApi.CompositionRoot.fs:26`)
show the new cases.

## Confidence

Medium-high. The server side reuses the client's own functions, and the stop-gap `toChange` keeps
this plan free of any change to the domain. The risk lies in the client: the dose dialog's
`Change*` messages and their optimistic local state (`Views/Order.fs`) have to send an index
instead of a changed order without changing what the dialog shows while a request runs.

## Steps

One pull request per step, one open at a time; each source step within the 200-line limit. The
script-only rule holds: the agent writes the scripts and the `Client` code; the `Shared`, `Server`
and `Client.Core` source is migrated by the user, or changed by the agent on the user's explicit
request for that step.

1. **Script `src/Informedica.GenPRES.Server/Scripts/OrderContextCommands.fsx`.** The new cases as a
   shadowed `OrderContextCommand`; the order variables by name, the scenario narrowing, `count` and
   `toChange`; with Expecto and FsCheck:
   - **Identity:** for every filter field, scenario and variable with more than one value, on the
     `Scenarios.fs` fixtures, `toChange` gives the old case and the same context
     the client builds today.
   - **Through the server:** each new case and its old counterpart, through `processCmd` with stub
     ports, give the same response. A filter command makes the scenarios afresh, with new order
     ids on both paths, so its responses are compared with the order ids left out; a command on an
     order keeps the ids, as the existing minimum, median and maximum commands do.
   - **Playing a scenario:** a scenario as a sequence of commands over a starting context, the
     picks of a clear kept with `PickList` as the client keeps them, and a context of the plan
     addressed by its position.
   - **A random walk:** at each step a target with `count > 1` and a random index in
     `0 .. count - 1`. Every step answers `Ok`, and a clear with its picks keeps the earlier picks.
   - **Order:** the values on the wire are in the order of the domain's value set, and the filter
     options and the scenarios come in the same order for the same input, run after run.
   - **Classification:** `replaced` gives, for every new case, the old case that `toChange` turns it
     into, and the machines' decisions (`Dialog.waits`, `Dialog.carries`, `clearedBy`) give the same
     answer for both.
   - **Argumentation:** `SetArgumentationProperty` followed by a command gives the same response as
     the text written into the context and the command sent with it.
   - **Single options:** which fields the client still picks by itself when a field offers one
     option (`PickField.fs:76`); the server already does this for the filter (see Decisions).
2. **Migration to `Shared`**, by the user: the functions of step 1, the cases in `Api.fs`, and
   `OrderContextCommand.toString` for the new cases.
3. **The server** calls `toChange` in `processCmd` and in `Navigate`; when it gives no command, the
   server answers the context as evaluated without asking the port.
   `OrderContextMapper.Command.toDomain` (`ServerApi.Mappers.OrderContext.fs:319`) takes the command
   `toChange` gives, so it needs no branch for the new cases. By the user, or on request.
4. **The client** (agent), per area (filter and scenario in `Prescribe.fs`; values in
   `Views/Order.fs`; the plan dialog in `Views/OrderPlan.fs`; nutrition in `NutritionSlot.fs`):
   send the new cases, with the context as it is. The machines keep `toChange cmd ctx` as the
   context sent, so the page shows what it shows today. A clear goes through the existing reopen
   message. The machines match on
   `replaced cmd` (`OrderPlanMachine.fs:73`, `:82`; `OrderContextMachine.fs:423`;
   `ArgumentationPolicy.clearedBy`), `Client.Core` code that starts as a script, migrated by the
   user. `PickList` stays: the picks are kept by the client, and the client leaves the order id off
   them when it sends a clear. The `Client.Core` test that the index of an offered value equals its
   index in `Vals` starts as a script, migrated by the user.
5. **The trail** (`Client.Core/Trail.fs`) shows the new cases, so that a trail reads as the sequence
   of commands that plays the scenario; prototyped in a script, migrated by the user.
6. **The client no longer sends the old cases** `UpdateOrderScenario`, `SelectOrderScenario` and
   `ReopenOrderScenario`: checked by a search of `Client` and `Client.Core`. They stay in the shared
   type, since `toChange` returns them and the server mapper matches them; their removal waits
   until `toChange` is gone (see *Later*). `UpdateOrderContext` stays in any case: a patient change
   sends it with an empty context for the new patient (`OrderContextMachine.fs:262`, `:392`), and
   patient edits are out of scope. `scripts/CheckDependencyRule.fsx`, `scripts/ProjectGraph.fsx`
   when a reference changes, and the benchmark build, since `benchmark/` is outside the solution.

## Verification, per step

- **Step 1:** `dotnet run build`, then the script in FSI; the Expecto and FsCheck suites pass.
- **Steps 2 and 3:** `dotnet run servertests`.
- **Steps 4 to 6:** `dotnet run servertests`; the client type-checked with `dotnet build` of the
  Client project, Fable and `npx vite build`; checked in the browser on the demo data: filter
  picks and clears, scenario choice, value picks, clears, steps, percentage, reset, the plan and
  nutrition. Nothing differs from before.

## Decisions

Made in review on 2026-10-05.

- **Extend `OrderContextCommand`** in its own naming convention; no second command type, no new
  plan command.
- **One case per level, the variable as a property type** (approach 3), the component and item
  located by their names, as the existing cases and the order processor do.
- **Scope:** filter picks, order value picks, scenario choice and reset, the order plan and
  nutrition. Patient edits are out of scope. The plan's own commands (`NewOrderContext`,
  `RemoveOrderContexts`, `AddOrderContext`, `Recalculate`) already say what they do and stay as
  they are; a scenario played in a test maps a context's position in the plan to its id.
- **Stateless:** the request carries the command and the current context, as today.
- **Every value is chosen from a list, the argumentation aside.** No field lets the user type a
  value of their own: `PickField` and `QuantityField` hand back the key of an offered option, so
  every pick is an index. The argumentation is text the user writes, and gets the one value
  command, `SetArgumentationProperty`.
- **An index counts from 0 and means the domain order,** as arrays do and as the domain's unused
  `OrderContext.setFilterItem` (`OrderContext.fs:733`) already does. Checked: `ValueSet.create`
  sorts ascending (`GenSOLVER Variable.fs:959`); the server mapper keeps the order
  (`ServerApi.Mappers.Order.fs:23`); the client keeps it (`ViewHelpers.ovarVals`,
  `ViewHelpers.fs:312`); none of the pickers (`PickField`, `Autocomplete`, `MultiPickField`) sorts
  or removes duplicates.
- **Nothing happens, two kinds:** not applicable gives the context unchanged (an index on a variable
  without a list; minimum, maximum or a step on a variable already at one value, as today); invalid
  gives an error (an index out of range, an unknown component or item, or a property the order does
  not hold).
- **Picks stay with the client,** as today; a clear carries them. A scenario played in a test keeps
  them with `PickList`, as the client does.
- **A single option is picked by the server.** It already picks the only indication, generic, route,
  form and dose type when it makes the filter (`OrderContext.fs:687`, `Array.someIfOne`). The
  client's own pick of a single option (`PickField.fs:76`) is then a second request for what the
  server already did; where step 1 finds it still needed, that pick moves to the server.
- **Scenarios are played on the fixed medications of the tests** (`Scenarios.fs`), with no rules
  loaded. On the live sheets an edit can shift a list, so index 2 picks a different value and the
  scenario still passes, and a test on them is flaky.
- **Later, not in this plan:**
  - MCP tools that take the new cases: a new capability, since the MCP tools change no order today.
  - The client takes the name of a pick from the command it sends, instead of comparing the order
    before and after (`PickList.picked`).
  - **The context carries the picks, so a clear needs none.** A clear is a reopen
    (`OrderProcessor.fs:768`): the order reset to what the rules allow, the values picked before
    the cleared variable put back, and solved again; clearing alone gives the same value back, since
    the other variables force it. The order does not hold which values the user picked or in what
    order, so the client keeps them (`PickList`) and sends them with a clear. Once every pick
    arrives as a `SetNth…Property` case, the server can append it to a pick list on the scenario
    and read it back on a clear: user history, not a projection. The `Clear…Property` cases then
    lose their picks (`ClearScheduleProperty of ScheduleProperty`,
    `ClearItemProperty of cmp * itm * ItemProperty`), and `PickList` leaves the client. A change to
    the contract and the domain.
  - **The domain processes the order commands itself, and `toChange` loses them.**
    The `SetNth…Property` and `Clear…Property` cases become cases of
    `ChangePropertyCommand`, beside `SetMin…`: the variable located through `OrderPropertyChange`,
    its nth value set with `setNthValue` (which counts from 1, so the index moves by one), then
    `SolveOrder`; or the variable cleared, then `Reopen` with the picks. `OrderProcessor` then knows
    which variable changed, instead of finding it by state in `processClearedOrder`.
  - **Then the same decision for the filter and the scenario commands.** The filter change logic
    (`applyChange`: which choices below a field go, and when the options go) and the narrowing to
    one scenario live in `Shared` today, not in the domain. Moving them into GenORDER moves
    contract-side behaviour into the core, so it needs a decision first; until then the filter and
    scenario cases stay in `toChange`. The domain's `setFilterItem` narrows the options to the nth
    instead of choosing it, and is unused; it is a starting point, not the same behaviour.
  - **The old cases leave the wire** once `toChange` is gone, since then nothing returns them and
    the server mapper takes the new cases: `UpdateOrderScenario`, `SelectOrderScenario`,
    `ReopenOrderScenario`, and `UpdateOrderContext` once the patient change has a command of its
    own. A tab still open from before then sends a case the server no longer reads; that is the
    case of any change to the contract, and the reload notice of #682 (G10) is its answer.
  - Applying a waiting command to the newest context instead of the context it was made on. It
    would simplify the two machines, but it changes what the user sees when requests overlap.
  - **The client state with one context.** Once every command goes over the newest context:
    `InFlight` and `Pending` hold the command and its request id, no context; the context shown
    while a request runs and after a refusal is derived from the context held and the command, not
    stored; `Dialog.carries` and the `carries` branch of `OrderPlanCart.replay` go; the
    argumentation is written into one context instead of three (`OrderContextState.map`); and
    `Navigate` loses its context, since the server finds it in the plan by its id. It waits on the
    decision on a pick made while a request runs (see
    [End state](#end-state-what-the-full-implementation-gives)).

## End state: what the full implementation gives

Assumed: every *Later* step is done, the domain processes every case, and `toChange` is gone. The
new cases then map one to one onto the domain's `OrderContext.Command`. The value commands
(`SetNth…Property`, `Clear…Property`, steps, minimum, median, maximum, percentage, reset) go on to
the order processor as `OrderCommand` and `ChangePropertyCommand`; the filter, the scenario choice
and the argumentation stay at the level of the context (`OrderContext.evaluate`).

### Every order context scenario plays as it happens in the user interface

Covered: every filter pick and clear, the diluent, the components, the scenario choice, every value
pick and clear (with the picks on the context), steps, minimum, median, maximum, percentage, reset,
the argumentation, and the plan's own commands.

Not yet, each closed by adding to the commands or the way a scenario is played:

1. **A patient change** evaluates the context again as a whole value (`OrderContextMachine.fs:96`):
   a patient command.
2. **A seed**: a medication chosen on another page or in the url sets generic, route and dose type
   by name at once (`App.fs:1190`, `:1450`); a resource reload or a filter sync evaluates the
   context again as it is (`App.fs:335`, `:1730`). A seed command that takes names; names are
   stable data, so it plays all the same.
3. **A restore after a reopen**: the reopened list closed without a pick puts the earlier state
   back and drops the answer to the clear (`OrderContextMachine.fs:558`). A restore step in the
   played sequence, or a restore command.
4. **The base context of a waiting pick**: today it goes over the context the user saw
   (`Dialog.carries`), while a played sequence goes over the last answer. Exact only once every
   command goes over the newest context.
5. **The clock**: `OrderContext.evaluate` takes the start of the orders (`OrderContext.fs:1013`), so
   a played scenario fixes it.
6. **The plan's context ids** are made by the server; a played scenario addresses a context by its
   position.

### The client state gets simpler

- One context instead of three: `InFlight` and `Pending` hold the command, the context shown during
  a request and after a refusal is derived, and the argumentation goes into one context.
- `Dialog.carries` goes, with the `carries` branch of `OrderPlanCart.replay`.
- `Navigate(plan, id, cmd)` without the context.
- `PickList` and `PickList.picked` go, once the context carries the picks.
- The dialog no longer changes the order (`setOvar` through `over`, `overComponent`, `overItem` and
  their nutrition counterparts); it only builds commands.

What stays, since it has nothing to do with the commands: one request at a time with its request
id and the dropping of an answer to an earlier request; `Dialog.waits`; the patient change, the
seed, the refusal, the selection and the reopen's `Kept` and `Restore`; the workbench beside a
context in the plan.

What becomes a question: showing a change while it runs. Without `toChange` on the client the page
either shows the old value until the answer lands, which the user sees, or keeps a small preview on
the client, derived from the command.

### Open: a pick made while a request runs

Both results above wait on this. The dialog takes a pick while a request runs, and the index means
the nth of the list the user saw; over the newest context it can point at another value when the
answer changed the list. Either the dialog takes no pick while a request runs, as the filter
already does, or the command carries the value picked beside the index and the server refuses it
when the list moved. Either way every command goes over the newest context: that makes a played
scenario exact on point 4 and lets `Dialog.carries` go.

## Out of scope: a rule set version per order context

Recorded because it removes the risk of a shifted list for live use once it is built. Rule set
versioning does not exist in the code: plan 622 says the KnowledgeRuleSet is still not built.
[#1247](https://github.com/informedica/GenPRES/issues/1247) names it only as a stamp on the signed
order plan version, and [#1118](https://github.com/informedica/GenPRES/issues/1118) makes older
imports loadable; no issue covers a rule set version per order context. This plan adds no field,
and the commands need no change when it comes.

Decided:

- Each order context holds its own rule set version, and every command on it is solved under that
  version, also after a newer one is published. A signed order plan version records the rule set
  version of each of its contexts.
- A published rule set is never withdrawn. It can be closed to new order contexts; existing ones
  keep using it.
- A context on a rule set that a corrected one superseded keeps its rule set, and says that it has
  been corrected.
- Only the latest rule set is in memory, since every new context starts from it. An older one is
  loaded when a context first asks for it. A new rule set is published about once a month, or
  sooner to supersede one with a correction.
- The rule set version covers everything a computation reads: the dose, solution, renal and
  reconstitution sheets, the mappings, the nutrition rules, the normal values, the G-Standaard
  data, and the spreadsheet id.
- The context carries its rule set version both ways on the wire; the server refuses an unknown one.
- Reloading the resources becomes publishing a rule set, which leaves existing contexts alone.
- Scenario 1b (line 133) and Rule 44 (line 814) of
  `docs/scenarios/integration/GenPRES-MainEHR-Integration-V8.md` compute and sign under the current
  rule set, and need amending.

Open:

1. Which contexts say that their rule set was corrected: all on that rule set, or only those whose
   rules the correction changed?
2. Signing on a corrected rule set: allowed with the notice, after an acknowledgement, or not until
   the context moves to the newer rule set?
3. May the user move a context to the latest rule set? That would be one more case, refused when
   the filter choices no longer exist.
4. Is a renewed order a new context, on the latest rule set, or the same context, on its own?
5. Which rule set version a check across contexts uses, such as interactions; the latest is the
   obvious choice.
6. Who may publish a rule set: unfiled in `docs/security/threat-model.md`.

## As built

Every step landed as a pull request from a fork branch against `master`, all on 2026-10-05. The
`Shared`, `Server` and `Client.Core` source was written by the agent on the user's request; the
script was removed once its tests were in `OrderContextCommandTests.fs`. Each client part was
checked in the browser by the user before it was committed.

| Step | PR | Landed |
|------|----|--------|
| plan | [#1294](https://github.com/informedica/GenPRES/pull/1294) | this document |
| 1, 2 | [#1295](https://github.com/informedica/GenPRES/pull/1295) | the property types in `Types.fs`; `Target` and `Options` with `count` in `Models.fs` |
| 1, 2 | [#1296](https://github.com/informedica/GenPRES/pull/1296) | the picks, the scenario narrowing, the filter, components and scenario changes, `setNth` and `clear`, and the argumentation in `Models.fs`; `ArgumentationPolicy` calls them |
| 1, 2, 3 | [#1297](https://github.com/informedica/GenPRES/pull/1297) | the new cases, `toString`, `toChange` and `replaced` in `Api.fs`; the server calls `toChange` in `processCmd` and `Navigate`; the tests in `OrderContextCommandTests.fs` |
| docs | [#1298](https://github.com/informedica/GenPRES/pull/1298) | the as-built notes for steps 1 to 3 |
| 4 | [#1299](https://github.com/informedica/GenPRES/pull/1299) | the machines: `Dialog.waits` and `carries` by `replaced`, `Dialog.shown`, a filter case on the evaluate path; tests in `OrderContextMachineTests.fs` and `OrderPlanMachineTests.fs` |
| 4 | [#1300](https://github.com/informedica/GenPRES/pull/1300) | the workbench's filter fields, diluent, components, reset and scenario cards in `Prescribe.fs` |
| 4 | [#1301](https://github.com/informedica/GenPRES/pull/1301) | the dose dialog's value messages name their variable; the changed order made by `Target.map` |
| 4 | [#1303](https://github.com/informedica/GenPRES/pull/1303) | the dose dialog sends `SetNth…Property` and `Clear…Property`, in the workbench and the plan |
| 4 | [#1304](https://github.com/informedica/GenPRES/pull/1304) | the nutrition slot's filter fields and value fields in `NutritionSlot.fs` |
| 5 | none | the trail already shows the new cases through `OrderContextCommand.toString` |
| 6 | none | checked by a search of `Client` and `Client.Core`, see below |

### Deviations from the text above

- **Steps 1 to 3 went to source in three pull requests,** split to stay within the 200-line limit,
  each with its tests in the server test project instead of the script.
- **The tests run on the `Scenarios.fs` fixtures and stub ports,** not on the demo data: they load
  no rules, so a sheet edit cannot change their outcome.
- **The server mapper has a branch for the new cases.** `OrderContextMapper.Command.toDomain` must
  match every case, so the new ones share a branch that raises: `toChange` always runs first.
- **A command on a missing target is an error, not a change of another component:** the
  component is found by name over every component of that name, as the dose dialog does.
- **An argumentation with no command** in `Navigate` is written on the plan's own context with
  that id and the plan is recalculated; a plan without that id answers `NoSuchContext`.
- **Step 4 went out in five pull requests:** the machines first, so that a new case never fell into
  their default branches, then the workbench, the dose dialog in two parts to stay within the
  200-line limit, and the nutrition slot. The machines were changed in source on the user's request,
  since their request stages are private and a script would have copied them whole.
- **The page shows the context as the command changes it, derived when read** (`Dialog.shown`), not
  stored beside the context sent.
- **A value a field does not offer still goes as today's case,** with the context changed by the
  client. No picker hands back such a value, so these paths should not run; they go with the old
  cases (see *Later*).
- **Step 5 needed no code.** The trail prints every command through `toString`, so the command and
  reopen lines of a trail, and the `Navigate` lines of the plan, read as the sequence of commands
  with their indexes. A replay that feeds a trail back stays deferred.
- **Step 6, the search:** the client sends `UpdateOrderContext` only to evaluate a whole context,
  on the first evaluation for a patient, a patient change and a seed, and in the fallbacks above. It
  sends `UpdateOrderScenario` and `ReopenOrderScenario` only in the fallbacks, and
  `SelectOrderScenario` no longer. `scripts/CheckDependencyRule.fsx` and the benchmark build pass;
  no project reference changed.
- **Found during the browser check:** a concentration the dose dialog offers that the order cannot
  reach, unrelated to this plan, filed as
  [#1302](https://github.com/informedica/GenPRES/issues/1302).
