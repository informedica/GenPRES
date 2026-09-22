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
   `Views/Prescribe.fs` on them. About 70 changed source lines. Tests in
   `OrderContextMachineTests.fs`, a list "the selection": selected over a context held that
   holds the order, none over one that does not, none without a patient and during the first
   evaluation, kept beside a request under way, closed by none; dropped by a patient change, a
   seed and a reset; kept by an answer whose context holds the order and by a failed change,
   dropped by an answer that starts the workbench over; `dialog` none while closed and the view
   while selected, settled and changing.
3. **Docs** (`docs`): this plan's As built; plan 706's left-open bullet on #897 closed; the
   rule's "known violation" line in plan 706's state model left as history.

Every code PR: `dotnet run Build`, `dotnet run ServerTests`, the Fable compile with the touched
`.jsx` inspected, Fantomas, the dependency-rule check. The docs PR: `dotnet run MarkdownLint`.

## Acceptance

- `Views/Order.fs` has no `Order` in its hook state; `Views/Prescribe.fs` has no
  `React.useState` for the dialog.
- `grep -n "useState\|useElmish" src/Informedica.GenPRES.Client/Views/*.fs` names no hook that
  holds an order, a context or a plan.
- Both pages open the order dialog on a selection a lane carries, closed by a `Select None`.
- By hand in the demo (`GENPRES_PROD=0`, `dotnet run`), plan 706's stepping check on both
  pages: two quick increases on a dose end two steps on; a value typed during a step ends on
  the value typed; the reset rests during a step; Ok closes the dialog during a step. And the
  prescribe page: Edit opens the dialog on the card's scenario; Ok closes it; Edit again
  reopens it on the same order; Voorschrijven with the dialog closed switches to the plan,
  and back on the prescribe page the dialog is closed over the empty workbench.

## Questions for review

1. Should `Select (Some id)` on the workbench also narrow the workbench to the scenario and
   send `SelectOrderScenario`, one message from the page instead of two? Proposed no: the
   selection is the client's own and carries no request, as on the plan page, where selecting
   sends nothing; the command is the page's, as it is today.
2. Is `dialog` the right name for the projection on `OrderContextState`, next to
   `OrderContextView.dialog` for the plan view? Proposed yes: the pair reads as a pair.

## Left open

- `Views/Order.fs`'s `update` builds fifteen changed orders in fifteen arms of the same
  shape; one helper over the order shown would fold them, at a larger diff than the rule asks
  for here.
- `useElmish` in the order dialog depends on the view value by reference, so a page re-render
  that reads the view again re-seeds the component and the item; a stable key (the order's
  id) would be the dependency the rule means.
- The plan page's `modalOpen` reads `selected.IsSome` from the plan view; it could read
  `OrderContextView.dialog` for symmetry with the prescribe page.

## As built

To be filled in as the steps land.
