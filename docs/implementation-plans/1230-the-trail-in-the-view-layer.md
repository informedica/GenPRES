# Implementation plan for issue #1230

The readable trail of the client machines reaches the view layer: the dose dialog's own steps, a
scenario's picks on the line, and the events of a pick field. Follows
[#1224](https://github.com/informedica/GenPRES/issues/1224), built by
[#1228](https://github.com/informedica/GenPRES/pull/1228), which stays as built.

The sections up to [As built](#as-built) are the plan as reviewed before the build; As built says
where the build went otherwise.

- [Problem description](#problem-description)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [As built](#as-built)

## Problem description

The trail records every step of the four machines in `Client.Core`: session, signing, order
context and order plan. Checked against eleven recent UI bugs, it would have shown two in full,
located the step of two more, and shown nothing of seven:

| Issue | Where the cause lived | The trail |
|---|---|---|
| #770 relaunch opens an empty plan | Session `LoadCart`, OrderPlan `Version` | full |
| #1126 resume refused, weight unknown | the patient in `PatientChanged`, the `Error` answer | locates the step |
| #1220 (2) the filter keeps an auto-picked route | the filter choices per answer; the cause is the server's `keep` | locates the step, not the cause |
| #1220 (1) a pick lost after a component switch | the server's reopen, the dialog's local `changing` state; the order's values hidden | nothing |
| #1155 the plan lane drops a pick in silence | the plan's content | nothing |
| #1141 the banner outlives its error | `Ui.ServerError` | nothing; the console trace shows it |
| #1152, #1137, #488 patient draft and estimate | `Ui.PatientDraft`, a policy | nothing; the console trace shows it |
| #1195 the cross offered where it changes nothing | `FieldOpenPolicy` at render | nothing |
| #897 useElmish dependencies | component-local state | nothing |
| #489 the dropdown closes by itself | component-local state | nothing |
| #1083, #1188 layout, casing | rendering | nothing |

Five layers lie outside the trail:

- **App state outside the lanes**: `Ui`, `Fetches` and `Admin`, 40 of the 44 top-level messages of
  `App.fs`. The console trace and the Redux DevTools debugger cover it, under the same gate.
- **Component-local state**: about ninety hooks across the views. `Views/Order.fs` runs its own
  Elmish program, the dose dialog, with some thirty-five messages, beside optimistic `changing`
  and `reopenedFrom` state and a `reopening` ref; `Views/Nutrition.fs` a second one;
  `SimpleSelect` its open and waiting state; `QuantityField` its step deltas. Nothing records
  these, and this is where the bugs of the table sit.
- **Policy decisions at render**: `FieldOpenPolicy`, `PickPolicy`, `QuantityModePolicy`,
  `PlanContextPolicy` and `SectionFoldPolicy` are pure, called while the JSX is built, and tested
  under Expecto.
- **The content the machines hold**: a plan shows as its number of contexts, a context as its id,
  its filter choices and its number of scenarios. The order's picks and values are hidden, so a
  dropped pick is invisible.
- **The server**, bracketed by a request id the server log carries too, and **rendering**.

Two facts of the code shape the design. `App.fs` is the last file the Client compiles, so no view
or component can reach the `StepTrail` module that holds the buffer today. And in the dose dialog
every `Change*` message answers with `Cmd.ofMsg (UpdateOrderScenario ...)`, a second step that the
local Elmish loop dispatches itself, past the dialog's shadowed `dispatch`; the server call happens
in that second step.

## Approaches considered

For where the buffer lives:

- **A trace callback threaded through the props**, from `AppEnv` down to `Order.View`,
  `Nutrition.View`, `ViewHelpers.orderField`, `QuantityField`, `SimpleSelect` and `PickField`.
  Rejected: the components have no `AppEnv`, `orderField` takes thirteen positional arguments with
  many callers, the plumbing alone exceeds a pull request, and a callback captured in a
  `useElmish` update closure goes stale the way a prop does.
- **The buffer and the gate in a Client module compiled before the components**, the App telling it
  when the settings confirm the demo data. Chosen.

For where the dialog's step is recorded:

- **The shadowed `dispatch` wrapper** that sets `changing`. Rejected: it sees the user's message
  only, never the `UpdateOrderScenario` step the loop dispatches after it, where the server call
  is made, and it sees no state after.
- **A wrapper around the `update` passed to `useElmish`.** Chosen: it sees every step, the state
  after, and the `reopening` ref read before the update runs.

For the policies:

- **A line per policy decision at render.** Rejected: a line per render per field is noise with no
  step to anchor to; a wrong decision is either a wrong policy, which its tests catch, or a wrong
  input, which the line would not show. The events of a field are recorded where the user acts on
  them instead.

For the content:

- **The whole order on the line.** Rejected: a line must stay readable and never break.
- **The scenario's picks with their values, when a context holds one scenario.** Chosen: the picks
  are what the reopen and the clear act on, and a context with a filled filter holds one scenario.

## Chosen approach

A Client module `StepTrail`, compiled after `Global.fs`, holds the gate, the buffer, the console
log and the text of the trail; the App confirms the demo data to it once the settings answer, and
records its machine steps through it as today. A release build compiles the module's names as
no-ops, so a call site needs no conditional compilation and the release keeps neither buffer nor
gate. Nothing is recorded before the confirmation.

The dose dialog and the nutrition slot record each step of their local Elmish program as a line
of machine `Order` or `Nutrition`: the message with the field it moves and the value or the step
it carries, the call it makes (`CallUpdate`, `CallReopen`, `CallReset`, `CallStep`, or the
`UpdateOrderScenario` message it queues), and the component and item selected. The local
`changing` and `reopenedFrom` state get no lines: the field is on the line, a reopen shows as
`CallReopen`, and the clear on a settled context is the order context machine's `Answered` line.

`Trail.Part.context` shows a context with one scenario as that scenario: its short order id, its
component, and its picks each with its value when the order holds one value for it, `open`
otherwise; `unknown` when the scenario does not say. Several scenarios stay a count.

`SimpleSelect` and `PickField` record what a handler did at the moment the user acted, as a line
of machine `Field` named by the field's label: an open that reopens, lists, or is blocked while
loading or busy; a pick; a clear; a close that restores; the reopened decision; a change the
field dropped; an auto-pick of a single option. The state reads `picked of 5`, `none of 5` or
`none of range`, and `reopening` while a reopen is under way, never the picked key: `PickField`
also serves the department, which the trail must not show.

The rules of #1224 hold for every new line: a debug build only, `GENPRES_LOG` on, nothing before
the demo data is confirmed, nothing in a release build, the last 500 lines in memory, and never a
patient's identity, a user's name, a PIN, a code, a token, a url, an error text or the
argumentation. An order's values are quantities, not identity, and may show.

## Confidence

Medium. The buffer move and the describers are mechanical, and the Core change is a describer
over types the trail already reads. Less certain: the numbering of a view line against the
machine line its call produces, since the App's dispatch runs from inside the dialog's update and
Elmish may run it at once or queue it (step 2 reads it off the browser and documents what it
finds); and whether the label tells the fields of the nutrition page apart (step 5 checks it; a
stable key would need a prop through `QuantityField` and `ViewHelpers.orderField`).

## Steps

One pull request per step, in this order. Each stays under two hundred changed source lines.

Script-only policy applies: `Client.Core` is prototyped in a script and migrated by the maintainer;
the Client project is edited directly.

### 1. `StepTrail` as a Client module

A new file `src/Informedica.GenPRES.Client/StepTrail.fs`, compiled after `Global.fs`. Under
`#if DEBUG` it takes over `genpresLog`, `genpresProd`, `isLogging`, `isProduction` and `isTraceOn`
from `App.fs`, and holds the size, the lines, the count and a `demo` flag, with
`confirmDemo: bool -> unit`, `record: (int -> DateTime -> Trail.Step) -> unit` gated by the trace
being on and the demo confirmed, `event: string -> string -> string list -> string -> unit` which
builds a step from a machine, a message, effects and a state, and `text: unit -> string`. The
`#else` branch declares the same names as no-ops.

`App.fs` loses the moved gate and its `StepTrail`; `isTraceable` stays for the console trace and
the debugger. The five recording sites drop their state argument and their conditional
compilation; the settings answer calls `confirmDemo` with the demo flag, its failure with false;
the program reads `StepTrail.isTraceOn` and hands `StepTrail.text` to the window. DEVELOPMENT.md
says the trail is described by `Trail` in Client.Core and recorded by `StepTrail` in the Client.

### 2. The dose dialog's steps

In `Views/Order.fs`, beside `msgToField`, three describers over the private `Msg` and `State`: the
message as its case, its value or its `ntimes` and `useCalc`, and `field <key>`; the effects from
the message kind, the order shown and the `reopening` ref read before the update, as
`ofMsg UpdateOrderScenario`, `CallUpdate <id>`, `CallReopen <id>`, `CallReset <id>` or
`CallStep <field> min|dec|med|inc|max`; the state as `cmp <component> item <item>`. The update
passed to `useElmish` is wrapped: read the ref, run the update, record the line through
`Trail.step "Order"`. The shadowed `dispatch` is untouched, and the wrapper never reads `changing`
or `reopenedFrom`, which are stale inside the closure. DEVELOPMENT.md gains an `Order` line and the
note on the numbering of a view line against the machine line it produces.

### 3. The nutrition slot's steps

The same three describers over the private `Msg` and `State` of `Views/Nutrition.fs`, whose step
messages carry the component; the same wrapper around its `useElmish`, machine `Nutrition`.

### 4. A scenario's picks in `Trail.Part.context`

A script `src/Informedica.GenPRES.Client.Core/Scripts/Trail.fsx` shadowing `Trail.Part` with:
`pickName`, which keeps of a pick's name the last bracketed segment and the tail, so that
`[id.orb.cmp.paracetamol]_dos_qty` reads `paracetamol.dos_qty`; `pick`, which gives
`name=<value>` when the order holds one value for the variable, by `Order.OrderVariable.displayString`,
`name=open` otherwise and `name=unknown` when the order lacks it; `scenario`, which gives the short
order id, `cmp <component>` and the picks, `picks none` for an empty array and `picks unknown`
for none; and `context`, which appends the scenario when the context holds one. The script's
Expecto tests are migrated into `TrailTests.fs` over the reflection-built scenario fixture of
`OrderPlanMachineTests`; the existing expectations, with no scenarios, do not change.

### 5. The events of a pick field

In `Components/SimpleSelect.fs` a `trace` over `StepTrail.event "Field"` with the label, called
from `handleOpen` (`reopen`, `list`, `blocked loading`, `blocked busy`), `handleChange`
(`updateSelected`, `updateSelected none`), `clear`, `handleClose` only when a reopen is under way
(`restore`), and the reopened effect in its decision branch (`NoList`, `ShowList`). In
`Components/PickField.fs` the dropped branch of `updateSelected` (`dropped`) and the auto-pick
effect when it fires (`autoPick`). A plain close, an accepted change and every policy at render
stay unrecorded. DEVELOPMENT.md gains a `Field` line and the rule that a line never shows the key.

## Verification, per step

1. `dotnet run build`; Fable compiles and `npx vite build` passes;
   `dotnet fsi scripts/CheckDependencyRule.fsx` passes. In the browser with `GENPRES_LOG=d`: the
   machine lines read as before, the first line appears only after the settings answer, and
   `genpresTrail()` returns them; with `GENPRES_PROD=1` no line and no `genpresTrail`. The
   bundle holds neither `genpresTrail` nor `__GENPRES_LOG__`.
2. Fable and Vite as above. In the browser: a pick, a step, an arrow reopen and an escape each
   give an `Order` line with the field, the call and the selection, interleaved with the order
   context lines; the numbering note in DEVELOPMENT.md matches what the console shows.
3. As step 2, on the nutrition page.
4. The script's tests pass in FSI, and after migration `dotnet test
   tests/Informedica.GenPRES.Client.Core.Tests/` passes, the secrets check and "a line never
   breaks" included. In the browser, the `Answered` line of a context with one scenario shows its
   picks with their values.
5. Fable and Vite as above. In the browser: an open, a pick, a clear, an escape after a reopen
   and a change on a held field each give a `Field` line; no line holds the department name or a
   picked key. Reproducing #1220 (1) with the trail on: the `Order` line's call, the order context
   `Command` and the `Answered` line with the scenario's picks show where the pick is lost.

## As built

| Step | PR | Landed |
|------|----|--------|
| plan | [#1231](https://github.com/informedica/GenPRES/pull/1231) | this document |
