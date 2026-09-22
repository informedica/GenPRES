# Implementation plan for issue 897

## Problem description

Plan [706](706-client-view-tier.md) put every piece of client state in one tier and gave the
component-local tier one rule: a hook holds what is never domain and never a function of the
view, a dropdown open, a print dialog, an optimistic field. Two violations were left as found
([#897](https://github.com/informedica/GenPRES/issues/897)):

- The order dialog (`src/Informedica.GenPRES.Client/Views/Order.fs`) keeps the order in hook
  state: `State.Order` in its Elmish sub-module, seeded from the context shown by `init`, set to
  none after every change and every step, and read by every change arm and by `displayOrder`.
  Since plan 706's step 4 (#890) `update` takes the order shown as a fallback, so that a change
  made while the answer to the previous one is awaited is over the order shown and not a no-op.
- The prescribe page (`Views/Prescribe.fs`) keeps its own `modalOpen` flag for the order
  dialog, set by the Edit button beside the `SelectOrderScenario` command and cleared by Ok,
  where the plan page opens the same dialog on the selection the plan lane carries
  (`OrderPlanState.Selected`, read as `OrderPlanView.Settled(_, selected)`).

What the hook's order is, checked arm by arm: `init` copies the order of the one scenario of
the context shown, the same order `shownOrder` reads from the same context; every change arm
builds the changed order from it, sends it to the lane through `updateOrderScenario` and sets
the field to none; every step reads it, sends the step and sets it to none; `update` reads the
field and falls back to the order shown when it is none. The hook's order is therefore either
the order shown or none, at every moment. It never holds an edit: an edit is sent the moment
it is made, and comes back at once as the context sent, which the lane holds as the request
under way and shows as `OrderContextView.Changing`. The draft the issue speaks of is already in
the lane; the hook keeps a copy of the domain and the fallback keeps the copy from mattering.

The prescribe page's flag says what a selection would say. The Edit button narrows the
workbench to the card's scenario and sends `SelectOrderScenario`; from then on the context
shown holds that one scenario and the dialog shows it. The flag records that the button was
pressed, beside the workbench that records what it was pressed on. When the patient changes,
the url seeds a filter or the order is prescribed, the workbench is re-evaluated or cleared
and the flag is not: it stays true across a `Reset`, and only the page's own Ok clears it.

## Approaches considered

1. **The dialog edits a draft the lane holds.** A `Draft: Order option` beside the request,
   set by the dialog's typing and sent on a commit. Rejected: there is no commit. Every change
   in the dialog goes to the server as it is made, and the lane already holds what was sent
   as the context of the request under way. A draft would be a third copy of the same order.
2. **The hook holds only what is never domain** (chosen for the dialog). The component and the
   item picked stay in the hook, the order goes: every arm reads the order shown, which the
   page hands the dialog through the view. What the fallback did since #890 becomes the only
   path.
3. **A flag on the workbench lane**, `DialogOpen: bool`. Rejected: a boolean flag in state,
   against the coding instructions, and a flag says nothing about what the dialog shows.
4. **The selection on the workbench lane** (chosen for the prescribe page), as the plan lane
   has it: `Selected: string option`, the order the dialog shows by its id, set by the page's
   `Select`, dropped by the machine when the workbench no longer holds that order. The dialog
   is open iff a scenario is selected, the same rule as on the plan page.

## Chosen approach

Approaches 2 and 4, each its own change.

### The order dialog reads the order shown

`Views/Order.fs`, the Elmish sub-module:

```fsharp
type State =
    {
        SelectedComponent: string option
        SelectedItem: string option
    }
```

`init` seeds the component and the item from the one scenario of the context shown, as it
does today, and no order. `update` keeps its `shown: Order option` parameter and reads it
where the arms read `state.Order` today: the fifteen change arms build the changed order
from the order shown and send it; `handleNav` sends the step over it; `ResetOrderScenario`
resets it; the state comes back unchanged, since nothing of it changed. `UpdateOrderScenario`
is unchanged but for the state it returns. In the component, `displayOrder` is `shownOrder`.

Nothing else moves. `changing` (the field whose change went out), `revision` (the counter that
resets an optimistic step value) and the optimistic value inside `SimpleSelect` are
component-local by the rule: none is domain, none is a function of the view. The `useElmish`
dependency on the context shown stays, so that a scenario selected in the plan re-seeds the
component and the item.

What changes for the user: nothing. The dialog showed the order shown already, through the
fallback, at every moment the hook's order was none, and the hook's order was the order shown
at every other moment.

### The workbench carries the dialog's selection

`OrderContextMachine.fs`:

```fsharp
type OrderContextState =
    private
        {
            Workbench: OrderContextWorkbench
            InFlight: ((OrderContextCommand * OrderContext) * string) option
            Pending: (OrderContextCommand * OrderContext * string) option
            // the scenario the order dialog shows, by its order's id: only one the context
            // shown holds; none while the dialog is closed
            Selected: string option
        }

[<RequireQualifiedAccess>]
type OrderContextMsg =
    | ...
    // the dialog's selection, a scenario by its order's id; the client's own
    | Select of string option
```

The order's id is what the dialog already keys on: the dialog re-wraps its `OrderLoader` into
the context by `sc.Order.Id`, the prescribe page asks the plan whether it `holds` the order by
the same id, and the server keeps it through `SelectOrderScenario` and every step
(`processScenarioOrder` changes the order in place).

The four constructors set `Selected = None`. One function, `select`, is the setter and the
guard in one: the id given, kept only when the context shown holds a scenario with that order
(the context sent while a request is under way, the one held otherwise); none without a patient;
so a selection during the first evaluation, over the empty context, is none, as on the plan
lane before the plan is there. `transition` answers `Select id` with `select id state, []`,
next to whatever is in flight, so that Ok closes the dialog while a step is under way, as it
does on the plan page.

The selection follows the workbench in `run`, as `Selected` follows the cart in
`OrderPlanState.run`: none without a patient; none on a patient change, a seed and a reset;
narrowed on an answer to what the workbench holds after it, so that a failed change keeps it
(the context held has the order) and an answer that starts the workbench over drops it (the
empty workbench has none); kept on a command. The patient change during a request under way,
which `transition` handles before `run`, drops it too.

The view is unchanged. One projection beside it:

```fsharp
/// The workbench as the order dialog shows it: the context shown while a scenario is
/// selected, settled or changing as the workbench is; none while the dialog is closed.
let dialog (state: OrderContextState) : OrderContextView option =
    state.Selected |> Option.map (fun _ -> view state)
```

`OrderContextView.dialog` does the same for the plan page from the plan view; the name says
the pair is a pair. The dialog does not narrow the context: the page narrows the workbench to
the scenario when it selects it, as it does today, so that from the moment the request goes
out the context shown has the one scenario, and the dialog reads that one as it does now.

`AppEnv.IOrderContext` gains `Dialog: OrderContextView option` and `Select: string option ->
unit`, served in `App.fs` by `OrderContextState.dialog` and `OrderContextMsg.Select`, as
`IOrderPlan.Select` is served.

`Views/Prescribe.fs`: the `modalOpen` hook and its setter go. The Edit button selects the
scenario by its order's id and sends `SelectOrderScenario` over the narrowed workbench, in that
order, both from the one handler it has today; Ok selects none; the modal is open iff the
dialog is some; the dialog gets `Dialog`, or `NoPatient` while closed, as the plan page hands
it `OrderContextView.dialog` or `NoPatient`.

What changes for the user: a patient change, a filter from the url and a prescribed order close
the dialog, where today the flag kept it open over whatever the workbench became. The modal
covers the page, so the first two cannot happen from the page while it is open; the third is
the prescribe button on a card, pressed with the dialog closed. Nothing visible in practice.

The plan page reads the same projection, in the same change: `modalOpen` is whether
`OrderContextView.dialog orderPlan` is some, computed once, and the dialog gets that value or
`NoPatient`, where today the page reads `selected.IsSome` off the plan view and computes the
projection a second time for the dialog. Same result, one reading.

### The nutrition slot reads the slot's order

`Views/Nutrition.fs`'s slot (`NutritionSlot`) has what the order dialog had: an Elmish
`State.Order` seeded by `init` from the one scenario of the slot's context, set to none after
every change and every step, and read by every arm; `displayOrder` falls back to the context's
order when it is none. The same reading holds: the hook's order is the context's order or
none, at every moment, and the context comes from the plan view the page hands the slot. So
`State.Order` goes, the hook keeps `SelectedComponent`, `update` gains `shown: Order option`
(the context's one scenario's order) and its arms read it, `handleNav` and `handleNavWithCmp`
with them, and `displayOrder` is that order.

One flag falls with it: `isLoading`, whether the hook's order is none while the plan is not
yet recalculating, which greyed the slot's selects for a render between a change sent and
the plan lane showing `Changing`. With the hook's order gone the selects grey on
`isRecalculating` alone, as the step buttons do already, and that is enough, on two counts.
The slot's state and the App's are both React states under `React.useElmish`, and a change
updates them in one event handler, the slot's dispatch calling the plan command before it
returns, so React batches the two into one render and no render shows the old plan beside
the new local state. And should one slip in, the lane is already `Changing` when any click
in it is handled, since `App.update` runs at the dispatch and not at the render: an edit made
then reaches a lane with a request under way and waits as the one pending, as an edit made
a frame later does. The guard protected a frame that the lane's own policy covers.

### The hooks keyed on the order

The order dialog's `useElmish` depends on the view value, `[| box props.orderContext |]`, and
the slot's on its context, `[| box ctx |]`. Both are values a page reads afresh on every
render, a new reference each time, so a re-render of the page for a reason of its own
re-seeds the component and the item picked, where the rule means "when the dialog shows
another order". The dependency becomes the order's id, `shownOrder |> Option.map _.Id`, for
the dialog, and the context's id for the slot, which has one per context: the picks survive
every answer over the same order, and re-seed when another order is shown. The revision
counter stays keyed on the reference: it has to bump on every answer, including one that
leaves the order equal, and only the reference says an answer came.

### The dialog's changes folded

`Views/Order.fs`'s `update` builds fifteen changed orders in fifteen arms of one of three
shapes: a field of the order, a field of the component picked, a field of the item picked in
the first component. Three helpers over the order shown say the shape once:

```fsharp
// a change to the order shown, sent to the lane; nothing to change without one
let over (f: Order -> Order) =
    match shown with
    | Some ord -> state, Cmd.ofMsg (UpdateOrderScenario(f ord))
    | None -> state, Cmd.none

// the component picked
let overComponent (f: Component -> Component) = ...

// the item picked, in the first component
let overItem (f: Item -> Item) = ...
```

and each arm is one line, `| ChangeFrequency s -> over (fun ord -> { ord with
Order.Schedule.Frequency = ord.Schedule.Frequency |> setOvar s })`. The one arm that names a
component and an item beside the picks, `ChangeSubstanceComponentConcentration`, keeps its
own mapping under `over`. The route stays `Cmd.ofMsg (UpdateOrderScenario _)`, so
`msgToField` and the field marked as changing are untouched. About 400 lines deleted and 100
added: over the size rule as one PR, and cut in two it would be one function half rewritten
under review; the reviewer chose one PR, and this plan says so where the PR is listed.

## Confidence

High on the dialog: the argument above is a reading of one `init` and one `update`, and the
fallback has run in production since #890; the change removes a field and the lines that set
it. High on the selection: it is the plan lane's field, function for function, on the other
lane, under the same tests. The manual check of plan 706's Acceptance on stepping is the
proof the tests cannot give, since the dialog is a component.

## Steps

1. **The order dialog reads the order shown** (`refactor(client)`, one PR). `Views/Order.fs`:
   `State.Order` gone, `init` without it, the arms of `update` on `shown`, `displayOrder` is
   `shownOrder`. About 110 changed source lines, most of them the two lines per arm. No
   machine change, so no test; the manual check below.
2. **The workbench carries the dialog's selection** (`refactor(client)`, one PR).
   `OrderContextMachine.fs`: `Selected`, `OrderContextMsg.Select`, `select`, the selection in
   `run` and in the patient-change branch, `dialog`; `AppEnv.fs` and `App.fs` the two members;
   `Views/Prescribe.fs` on them; `Views/OrderPlan.fs`'s `modalOpen` on the projection. About
   80 changed source lines. Tests in `OrderContextMachineTests.fs`, a list "the selection":
   selected over a context held that holds the order, none over one that does not, none
   without a patient and during the first evaluation, kept beside a request under way, closed
   by none; dropped by a patient change, a seed and a reset; kept by an answer whose context
   holds the order and by a failed change, dropped by an answer that starts the workbench
   over; `dialog` none while closed and the view while selected, settled and changing.
3. **The nutrition slot reads the slot's order** (`refactor(client)`, one PR).
   `Views/Nutrition.fs`: `State.Order` and `isLoading` gone, `init` without the order,
   `update` on `shown`, `displayOrder` the context's order. About 80 changed source lines. No
   machine change; the manual check below.
4. **The hooks keyed on the order** (`refactor(client)`, one PR). `Views/Order.fs`'s
   `useElmish` on the order's id, `Views/Nutrition.fs`'s on the context's id. Under 25
   changed source lines.
5. **The dialog's changes folded** (`refactor(client)`, one PR, over the size rule by the
   reviewer's choice). `Views/Order.fs`: `over`, `overComponent`, `overItem`, the fifteen arms
   on them. About 500 changed source lines, 400 of them deleted. Its own manual check: every
   field of the dialog changed once, on the prescribe page and in the plan, lands on the
   value typed.
6. **Docs** (`docs`): this plan's As built; plan 706's left-open bullet on #897 closed; the
   rule's "known violation" line in plan 706's state model left as history. And, folded in
   by the author's choice, plan [896](896-deferred-refreshing.md)'s As built (its plan PR
   #938, its fix PR #939 with the empty log list kept on refresh from review) and plan 706's
   left-open bullet on #896 closed.

Every code PR: `dotnet run Build`, `dotnet run ServerTests`, the Fable compile with the touched
`.jsx` inspected, Fantomas, the dependency-rule check. The docs PR: `dotnet run MarkdownLint`.

## Acceptance

- `Views/Order.fs` and `Views/Nutrition.fs` have no `Order` in their hook state;
  `Views/Prescribe.fs` has no `React.useState` for the dialog.
- `grep -n "useState\|useElmish" src/Informedica.GenPRES.Client/Views/*.fs` names no hook that
  holds an order, a context or a plan, and no `useElmish` keyed on a view value.
- Both pages open the order dialog on a selection a lane carries, closed by a `Select None`.
- By hand in the demo (`GENPRES_PROD=0`, `dotnet run`), plan 706's stepping check on both
  pages: two quick increases on a dose end two steps on; a value typed during a step ends on
  the value typed; the reset rests during a step; Ok closes the dialog during a step. And the
  prescribe page: Edit opens the dialog on the card's scenario; Ok closes it; Edit again
  reopens it on the same order; Voorschrijven with the dialog closed switches to the plan,
  and back on the prescribe page the dialog is closed over the empty workbench. And the
  nutrition page: a value typed in a slot lands, two quick increases end two steps on, the
  slot's component pick survives an answer.

## Questions for review

1. Should `Select (Some id)` on the workbench also narrow the workbench to the scenario and
   send `SelectOrderScenario`, one message from the page instead of two? Proposed no: the
   selection is the client's own and carries no request, as on the plan page, where selecting
   sends nothing; the command is the page's, as it is today.
2. Is `dialog` the right name for the projection on `OrderContextState`, next to
   `OrderContextView.dialog` for the plan view? Proposed yes: the pair reads as a pair.

## Left open

- `Views/Formulary.fs` and `Views/Parenteralia.fs` keep the selects' values in Elmish state
  seeded from the fetched record and re-seeded on every answer: a hook that mirrors the
  filter the record carries, the "never a function of the view" half of the rule. Out of
  scope here by the author's choice: another page family, and the values are a form's own
  between a change and its answer.
- The revision counter in the dialog and the slot is keyed on the reference of the view value;
  see "The hooks keyed on the order" for why it stays.

## As built

Built in the order proposed, one PR at a time, each merged before the next started, the
client edited directly.

| Step | PR | Landed |
|---|---|---|
| plan | #942 | this document |
| 1, the order dialog | #943 | `State.Order` gone, the arms on `shown`, `displayOrder` is `shownOrder`; 105 source lines |
| plan amended | #944 | steps 3 to 5 added at the reviewer's request: the nutrition slot, the hooks keyed on the order, the changes folded; the plan page's `modalOpen` folded into step 2; plan 896's As built folded into step 6; the formulary hooks out of scope; review: why the slot's loading guard can go, said precisely |
| 2, the workbench's selection | #945 | `OrderContextState.Selected`, `OrderContextMsg.Select`, `holds`, `select`, the selection following the workbench in `run`, `dialog`; `IOrderContext.Dialog` and `Select`; the prescribe page's flag gone, Edit selects then narrows; the plan page's `modalOpen` on the projection; 5 tests; 101 source lines |
| 3, the nutrition slot | #946 | `State.Order` and the spinner flag `isLoading` gone, `update` on `shown`, `displayOrder` the context's order; 92 source lines |
| 4, the hooks keyed on the order | not built | see the deviations |
| 5, the changes folded | #947 | `over`, `overComponent`, `overItem`, the fifteen arms one line each; 427 source lines, 345 deleted, over the size rule by the reviewer's choice |
| 6, docs | this PR | this section; plan 706's left-open bullets on #897 and #896 closed; plan 896's As built |

### Deviations from the text above

- **Step 4 was not built.** Its premise was wrong: Feliz.UseElmish 5.0, the version the client
  uses, compares dependencies structurally (`IsOutdated` is `arg <> arg' || dependencies <>
  dependencies'`), so a page re-render that reads an equal view does not re-init the hook. The
  hook re-seeds only when the context changes, on each answer, and the picks round-trip through
  the `OrderLoader`, so they survive that. Keying on the order's id would have needed the
  update-through-a-ref pattern `Views/Patient.fs` and the slot's `planRef` already use, since
  `useElmish` captures `init` and `update` at construction and refreshes them only when a
  dependency changes: more machinery for the same behaviour. Decided with the author on the
  finding; the dependencies stay as they are.
- **The slot's flag was the spinner, not the greying.** "The hooks keyed on the order"'s
  neighbour, `isLoading` in the slot, was the spinner argument of the selects, true only for
  the render between a change sent and the plan showing `Changing`, never during the
  recalculation; the greying came from `isRecalculating` all along. The five order-value
  selects, which took that flag, now pass no spinner and grey on `isRecalculating` through
  `select` as before; the slot's filter selects (the generic, the indication, the dose type)
  took `isRecalculating` for both the spinner and the greying already and are untouched.
