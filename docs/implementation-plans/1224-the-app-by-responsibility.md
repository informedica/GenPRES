# Implementation plan for issue #1224: the App by responsibility

[The App as wiring](1224-the-app-as-wiring.md) left `App.fs` at 1094 lines, all of it wiring:
what an effect sends out of the client, the Elmish program and its debugger, the projection the
views read, the MUI themes and the view. One file, one `module private Elmish` of 540 lines
nested in it, and a reader who wants the server calls scrolls past the debugger, the themes and the
projection to find them. This plan orders `App.fs` into nested modules of one responsibility each,
moves the code as it is, and takes out only what is not the App's: the debugger, the themes, and
two parts of the view that are views of their own.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Steps](#steps)
- [Verification, per step](#verification-per-step)
- [Out of scope](#out-of-scope)
- [As built](#as-built)

## Problem description

`App.fs` holds seven responsibilities, by symbol:

- **Messages built outside the client**: `newRequest`, `sessionMsg` and the four other lane
  helpers, `answered`, `parseUrl`. Used by the effects and by the projection.
- **What an effect sends out of the client**: `serverApi`, `tokenOf`, `eraseLaunch`,
  `presentLaunch`, `applyShellEffect`, `applyLoaderEffect`, `applyAdminEffect`,
  `applySessionEffect`, `applySigningEffect`, `applyOrderPlanEffect`, `callContext`,
  `applyOrderContextEffect`, `applyPatientEffect`, `applyEffect`. 470 lines, the most. Three of
  the lane calls, the plan, the workbench and the patient, repeat the same twelve lines: the call
  under the token, `Ok reply` answered through `answered`, `Error errs` and the exception answered
  as an `Error`. Six of these functions take the client state, and each reads one thing from it,
  the Session's `OpenedToken`; `applySessionEffect` takes it and reads nothing.
- **The Elmish program**: `noteLanding`, `carryOut`, `init`, `update`, `program`.
- **The trace gate and the debugger**, under `#if DEBUG`: `isTraceable`, `redacted`, `redactMsg`,
  `consoleTrace`, `gatedConnection`, `withGatedDebugger`. Development tooling, not the App.
- **The projection**: `ConcreteAppEnv`, with `calculateInterventions` and the two lists it takes
  from the view, `bm` and `cm`.
- **The themes**: `themeDef` and `mobileDef`, two literals of 28 lines that differ in the font
  size and the component size only. MUI configuration, which `MUI.fs` holds for everything else.
- **The view**: `View`, with the leave dialog's title and text, the snackbar's severity, text and
  auto-hide, the server error banner, the leave guard, the router, the contexts and the page; and
  `root`, the entry point, although the comment above `View` says the entry must be a file of its
  own for hot reload.

Hot reload is the reason `App.fs` hides its wiring. Fable exports every public top-level binding
of a file, and Vite's React Fast Refresh (`@vitejs/plugin-react` 6.0.1, `refresh-runtime.js`,
`validateRefreshBoundaryAndEnqueueUpdate`) hot-swaps a file only when each export is a component,
a function with a capitalized name and a plain prototype, or keeps its identity between the old
and the new module. A number or a string passes. A lowercase function, an sx object, a DU or a
record class fails, the update propagates to the importers, and at `App.jsx`, which `index.html`
loads, that is a full reload. Checked on 2026-10-10 over `output/**/*.jsx`:

| File | Exports that fail |
| ---- | ----------------- |
| `App.fs` | `root`, an object; `redacted` is a string and passes |
| `Components/QuantityField.fs` | `Mode` and `Answer`, eight sx objects, eight lowercase functions |
| `Components/Disabled.fs` | `containerSx`, `contentSx`, `backdropSx` |
| `Components/ActionBar.fs` | `Kind` |
| `Components/Notice.fs` | `Kind` |
| `Components/PickField.fs` | `Shape` |
| `Components/SeverityMark.fs` | `icon` |
| `Components/SimpleSelect.fs` | `fieldState` |
| `Components/PrintTable.fs` | `valueOf` |
| `Components/AdministrationSummary.fs` | `isValue` |
| `Views/ViewHelpers.fs` | some forty helpers beside the four `PrintView` components |

Every other file with a component exports components alone, `Views/OrderPlan.fs` two of them.
`Views/AlertText.fs`, `Views/NutritionSlot.fs`, `Global.fs` and `Utils.fs` hold no component and
propagate as plain modules do. A nested-module binding compiles to a capitalized name,
`Logging_error`, and passes as a component; harmless. `App.jsx` reloads whatever it exports,
since a swap would call `createRoot` again. Vite reloads the page only when an update reaches a
module with no importers, and a self-accepting importer stops it: with the entry in a file of its
own, a component whose exports fail swaps together with its importers, every one of them a
boundary, and the edit shows.

Nothing here decides; the last plan saw to that. What is wrong is the reading: the first five
behind one nested private module or loose between it and the view, and the names qualified by
part in the function name (`applyShellEffect`) where the trail qualifies them by module
(`Trail.shell`). And some names say one thing for two, or two things for one:

- `token` is the Session's `OpenedToken` in `applySigningEffect` and in the doc comment of
  `answered`, the admin's token in `applyAdminEffect`, and the launch's token in the doc comment
  of `eraseLaunch`; the same `OpenedToken` is `opened` in the four other calls and in the
  `Api.Request` field.
- A command is `cmd` in `applyOrderPlanEffect` and the projection, `req` in `callContext`, and
  `command` in `applyAdminEffect` and the `Api.Request` field.
- `bm` and `cm` in `View` are the two intervention lists; nothing says so.
- `sx` is the page box's style, in a file where every other style is named for its element.
- The projection's members take `p`, `f`, `s` and `cmd` for a patient, a filter, a list item and
  a command.

## Decisions

1. **One file, nested modules, the code moved as it is.** `App.fs` stays the App: its wiring is
   one thing, read top to bottom. `Messages` and `Effects` split off the `module private Elmish`
   as nested modules at the same indentation, so a move changes the module header and the
   qualifiers, not the lines;
   the review is `git diff --color-moved=zebra --color-moved-ws=allow-indentation-change`. The
   `State` and `Msg` aliases move to the top of the file, as `type private`, which compiles and
   emits no JavaScript, so every `Msg.` and `State` in the file stays as it is. Six steps are
   not moves, each on its own: step 1 adds the entry, step 3 folds the three repeated lane calls
   into one helper, step 5 writes the two theme literals as one function, step 6 drops two
   constructor arguments and the view's two lists, step 7 feeds the two views by props, step 8
   renames. `Tracing.fs`, step 4, sits before the aliases and spells `Client.ClientMsg` and
   `Client.ClientState` in three signatures; the move check allows those lines. The trail of a
   page load reads the same before and after every step.
2. **The order of `App.fs`**: the two aliases, then a nested module per responsibility, then the
   type and the view:
   - `module Messages`: the five lane helpers, `session`, `signing`, `patient`, `workbench`,
     `plan`, without the `Msg` suffix, since the module names them: `Messages.session m`.
   - `module Effects`: `serverApi`, `answered`, `underSession`, then one function per part,
     named after the case of `ClientEffect` or `LanesEffect` it takes, as the message helpers
     are: `shell`, `loader`, `admin`, `session`, `signing`, `plan`, `workbench`, `patient`, and
     `apply`, which `applyEffect` becomes. Every function that needs the Session's token takes
     it, `opened`, and none takes the client state: `Effects.apply opened effect`. `Trail` names
     the two lanes `orderPlan` and `orderContext`; the App keeps the lane names. `eraseLaunch`
     and `presentLaunch` stay beside `shell`. `signing` binds a `plan` in its first arm, which
     shadows `Effects.plan` there; harmless, since `signing` never calls it, and the move leaves
     it.
   - `module Elmish`, as it is today, with `newRequest` and `parseUrl`, the inputs of the loop,
     an id minted and the url read with the clock; `noteLanding`, `carryOut`, `init` and
     `update`: the model, the message, `init` and `update` that an Elmish app has, in the module
     that `Pages/GenPres.fs`, `Views/Order.fs` and `Views/NutritionSlot.fs` keep them in.
     `carryOut` reads the token once, `Client.opened state`, and hands it to every effect. The
     second `open Elmish`, the one that opens this module over the library's namespace, goes:
     `program` calls `Elmish.init` and `Elmish.update`, the projection `Elmish.newRequest`, the
     view `Elmish.parseUrl`.
   - `program`, at the top level as today: `Program.mkProgram` over `Elmish.init` and
     `Elmish.update`, with the trace and the debugger, composed where it runs, as
     `React.useElmish` composes `init` and `updateTraced` inside the components' `View`.
   - `type Projection(state, dispatch)`: `ConcreteAppEnv` under the word the last plan uses for
     it, the projection, with `calculateInterventions` and the two lists computed inside it, so
     the view no longer computes them and the constructor takes two arguments.
   - `View`.
3. **Five things leave `App.fs`**, each something that is not the App's wiring:
   - `Main.fs`, the last file of the project: `root` and the render, nothing else, as the comment
     above `View` asks. `index.html` loads `output/Main.jsx`. `root` cannot stay in `App.fs`: a hot
     swap of `App.jsx` evaluates the module again and calls `createRoot` on the same element
     twice. After this `App.jsx` exports `View` alone, beside `Main.jsx`, the layout of Vite's
     own React template. It goes first, so that every later step's hot reload check means
     something.
   - `Tracing.fs`, module `Tracing`, before `App.fs`: `isTraceable`, `redacted`, `redactMsg`,
     `consoleTrace`, `gatedConnection`, `withGatedDebugger`, public, since `program` calls
     `consoleTrace` and `withGatedDebugger` and the debugger calls `isTraceable`. The module
     header stays outside the `#if DEBUG`, the body inside: an empty file that is not the last
     does not compile.
   - The themes go to `MUI.fs`, a nested module `Themes` after `Styles`; `Theme` is taken there
     by the erased binding of MUI's type. Not as an Emit literal: `createTheme` and
     `responsiveFontSizes` imported as functions from `@mui/material/styles`, the options as
     anonymous records, one function `themeOf fontSize size`, and `desktop` and `mobile` as
     values over it, 12 and `medium`, 11 and `small`. The imports are explicit because
     `responsiveFontSizes` reaches `App.jsx` only through the import the view's JSX hoists, and
     `MUI.jsx` has none. As values the two themes are computed once at load; the emitted value
     is inlined at each use today, so `createTheme` runs on every render. Harmless, and the step
     says so.
   - `Views/LeaveDialog.fs`, module `Views.LeaveDialog`, one `View` with the title, the text
     and the confirm dialog of the url question as locals, over props: `isOpen`, `launched`,
     `asksLaunch`, `getTerm`, `onConfirm`, `onCancel`.
   - `Views/AlertSnackbar.fs`, module `Views.AlertSnackbar`, one `View` with the severity, the
     text and the auto-hide as locals, over props: `alert: Alert option`, `getTerm`, `onClose`.
     The view keeps the two ways it closes: the close button and the auto-hide call `onClose`, a
     click outside the snackbar does not, as `handleClose` ignores the click-away reason today.
   - `Components/LeaveGuard.fs`, module `Components.LeaveGuard`, one hook `useLeaveGuard` over
     a `bool`, whether there is unsigned work: the browser's question before it leaves the page,
     the listener added once and reading the latest value through a ref, as `View` has it today.
     Browser behaviour, not layout, so it leaves `View` as the two views do.
     Beside `Views/AlertText.fs`, which gives it the words and already takes an `Alert`.
   Neither view nor the hook takes the client's state or message: no file under `Views`,
   `Pages` or `Components` names a `Client` type, and a view is fed by props or by the `AppEnv`
   interfaces.
4. **The nested modules stay private, for Fable.** Fable emits every public top-level binding
   of a file as an export of its module, and Vite's React Fast Refresh hot-swaps a file only
   when each export is a component or keeps its identity. Every file with a component exports
   components only, but the eleven in the table above; `Pages/GenPres.fs` keeps its wiring behind
   a `module private Elmish` for that reason. So `Messages` and `Effects` are `module private`
   as `Elmish` is, `Projection` is `type private`, `redacted` leaves
   with `Tracing.fs`, and `root` leaves with `Main.fs`. `Tracing.fs`, `MUI.fs` and `Main.fs`
   hold no component; the two views export one `View` each. The components in the table stay as
   they are: after step 1 their failing exports widen a swap to the importers and never reload.
   The private is for
   `App.fs` alone, where it keeps the entry's only importer a boundary.
5. **The repeated lane call becomes one helper**, `Effects.underSession`, taking the server call,
   the command, the function that makes the lane message of an answer, which captures the
   request as the three arms do today, and the token; `Api.Request` is generic, so one helper
   fits the plan, the workbench and the patient, and the three arms call it. `callContext` goes.
   The session and the signing calls stay as they are: each answers differently.
6. **Names: one name per thing, one thing per name, in the whole file.** A name is the word
   the contract or the machine uses for the value, so that a reader who knows one knows the
   other, and the same value has the same name in every function:
   - the Session's `OpenedToken` is `opened`, as `Api.Request.Opened` has it, in code and in
     the comments; `tokenOf` goes, and `Client.opened state` takes its place, a one-line read in
     `Client.fs` beside the other reads, over `SessionState.token`, called once in `carryOut`,
     so that no function in the App reaches into the lanes; `token` is left to the admin's
     token; the url's token is the launch, or the launch's token, never bare `token`;
   - a command is `command`, as `Api.Request.Command` has it, never `cmd` or `req`; a request id
     stays `request`;
   - the two lists have no name of their own: each is the body of its member in `Projection`,
     computed when a page reads it, as every other member is;
   - the page box's style is `pageSx`;
   - a member's or a function's parameter is named for what it is: `patient`, `parenteralia`,
     `formulary`, `filter`, `item`, `command`. A single letter stays where it is today the usual
     thing: the argument of a one-line lambda, a bound case or exception, `e`, `m`, `ex`, `r`,
     and a local in a body, which the coding instructions keep short;
   - a function is named for its one job, and two functions with one job share no name:
     `Effects.apply` carries one effect out as a command, `carryOut` records a transition's
     steps and carries its effects out, as the machines' `transition` and `Client.run` differ;
   - the parts keep the names of their cases, `shell`, `loader`, `admin`, `session`, `signing`,
     `plan`, `workbench`, `patient`, in `Messages` and in `Effects` alike, so that
     `Messages.plan` builds the message the plan takes and `Effects.plan` carries out the effect
     it emits.
   The members of the `AppEnv` interfaces keep their names, since the interfaces are out of
   scope; only their parameters are renamed.
7. **Commits and size.** Every step commits as `refactor(client)` and fits the 200-line limit:
   step 2 keeps the indentation, so its diff is the module headers and the qualified call
   sites; the moved `Tracing.fs` is about 80 lines each way.

## Steps

One pull request per step, one open at a time. All of it is source in the Client project, which
Fable compiles and FSI does not reach, so there are no new tests; the verification is the build,
the Fable output and the browser with the trail. The one line in `Client.Core`, `Client.opened`
in step 3, is a read like the thirty-six of the last plan's step 10 and gets no test either.

1. **The entry.** `Main.fs` with `root` and the render; `index.html` loads `output/Main.jsx`;
   `App.fs` ends with `View`. The fsproj adds the file after `App.fs`.
2. **The messages and the effects.** `State` and `Msg` to the top of the file as
   `type private`; `Messages` and `Effects` split off the `Elmish` module, `Effects` with
   `serverApi`, `tokenOf`, `answered`, the eight part functions, `eraseLaunch`, `presentLaunch`,
   `callContext` and `apply`. The `Elmish` module keeps `newRequest`, `parseUrl`, `noteLanding`,
   `carryOut`, `init`, `update`, `isTraceable` and `calculateInterventions` where they are, and
   `program` stays at the top level. The call sites qualify: `Effects.apply`,
   `Messages.session m`; the projection too. The diff is the headers, the 58 lane helper call
   sites and the nine effect calls; about 85 lines.
3. **The effects take the token.** `Client.opened` added to `Client.fs`; `carryOut` reads it
   once and passes it: `Effects.apply opened effect`, and each part function that needs it
   takes `opened` in place of the state; `tokenOf` removed. `Effects.underSession`; the plan,
   workbench and patient arms through it; `callContext` removed. The rewritten lines take their
   names here: `command` for `cmd` and `req`, `opened` for the signing call's `token`. About
   thirty lines fewer.
4. **The tracing out.** `Tracing.fs`; `isTraceable` leaves `Elmish` and the debugger leaves the
   top level, and `program` calls `Tracing.consoleTrace` and `Tracing.withGatedDebugger`. The
   second `open Elmish` goes, and the call sites qualify: `Elmish.init`, `Elmish.update`,
   `Elmish.newRequest`, `Elmish.parseUrl`, `Elmish.calculateInterventions`.
5. **The themes.** `Mui.Themes` in `MUI.fs`, written in F# over the two imports; `View` reads
   `Mui.Themes.desktop` and `Mui.Themes.mobile`; `themeDef`, `mobileDef`, `theme` and `mobile`
   leave `App.fs`.
6. **The projection.** `type Projection`; `calculateInterventions` moves into the type, and the
   two list members compute their list in their body, so a page that does not show a list costs
   nothing; `bm` and `cm` go; `View` constructs `Projection(state, dispatch)`.
7. **The view's three parts.** `Views/LeaveDialog.fs`, `Views/AlertSnackbar.fs` and
   `Components/LeaveGuard.fs`; `View` reads the props from `Client` and passes them, the
   handlers dispatching, as it feeds `ConfirmDialog` today, and calls
   `useLeaveGuard (Client.unsignedWork state)`.
8. **The names.** What decision 6 asks and no earlier step rewrote, in one pull request after
   the moves, so that no move diff carries a rename beyond the moved function's own name and
   its qualifiers: the projection's `p`, `f`, `s` and `cmd`, each for what the member takes;
   `sx`; the doc comments of `answered` and `eraseLaunch`, which say "the token" for the opened
   token and the launch's. Nothing moves.

After step 8, by estimate:

| File | Lines |
| ---- | ----: |
| `App.fs` | 830 |
| `Tracing.fs` | 80 |
| `Views/LeaveDialog.fs` | 50 |
| `Views/AlertSnackbar.fs` | 50 |
| `Components/LeaveGuard.fs` | 25 |
| `Main.fs` | 10 |
| `MUI.fs` | +50 |

In `App.fs`: `Messages` 15, `Effects` 380, `Elmish` 80, `Projection` 170, `View` 90, the rest
doc comments and blank lines.

## Verification, per step

- **Every step:** `dotnet build src/Informedica.GenPRES.Client/Informedica.GenPRES.Client.fsproj`,
  Fable and `npx vite build`, `dotnet fantomas --check`,
  `dotnet fsi scripts/CheckDependencyRule.fsx`, `dotnet fsi scripts/ProjectGraph.fsx` unchanged,
  since no project reference changes.
- **Every move step:** `git diff --color-moved=zebra --color-moved-ws=allow-indentation-change`
  shows every moved line as moved and no changed line inside a moved block but the function's
  name and its qualifiers; in a debug Fable build `grep -c '^export' output/App.jsx` reads 2,
  `redacted` and `View`, after step 1 and 1 after step 4. A release build never emits
  `redacted`.
- **Every step, in the browser with the trail:** a page load until the gate opens, a filter
  pick, a patient edit, a prescription, a signature, the url question answered both ways, the
  admin login and the log listing. The trail of the page load reads the same as before the step.
- **Every step after 1, hot reload:** `dotnet run` with the dev server: an edit to `App.fs`
  swaps the view in place, the browser console shows no "Could not Fast Refresh" and no
  "createRoot" warning, and the page keeps its state. What shows in place is an edit to the
  view, the two views, the hook, the projection, which is constructed on every render, and the
  themes.
  `React.useElmish` builds the program once, with no dependencies, so an edit to `Messages`,
  `Effects`, `Elmish` or `Tracing` runs after a reload of the page, as today.
- **Step 1:** the hot reload check above, for the first time; `npx vite build` finds the entry.
- **Step 3:** a plan change, a workbench pick and a patient edit answer as before; the server
  stopped, each raises the error banner under its source; a signature with the session open
  goes through, without one is refused; `grep -nwE 'tokenOf|cmd|req' App.fs` and
  `grep -n 'Lanes.Session' App.fs` find nothing, and in code `token` appears in `Effects.admin`
  only.
- **Step 4:** a debug build with the trace on shows the console trace and the Redux DevTools
  history from the first traceable state, the password redacted; a release build traces nothing
  and compiles `Tracing.fs` as an empty module.
- **Step 5:** the Fable output of `MUI.fs` imports `createTheme` and `responsiveFontSizes` and
  holds the two themes as module values with 12 and `medium`, 11 and `small`; `App.jsx` holds no
  `createTheme`; the desktop and the mobile page look as before.
- **Step 6:** the emergency list and the continuous medication with and without a patient.
- **Step 7:** the leave dialog's three texts, launched or not, with or without a launch in the
  url; the snackbar auto-hides on a success and stays on an error, its close button dismisses
  it, and a click outside it leaves it open; a reload with unsigned work asks, without it does
  not; `grep -c '^export'` reads 1 for each of the two views and the hook.
- **Step 8:** `grep -nw 'sx' App.fs` finds nothing; the comments say `token` only for the
  admin's and the launch's.

## Out of scope

- The lists calculated in the projection, `calculateInterventions`: #1214 retires them.
- The dead code in the Client project: #1393.
- `AppEnv.fs`, its interfaces and the pages that take them.
- `Pages/GenPres.fs`, `MUI.fs` beyond its new `Themes` module, and the vendored
  `Components/FelizRouter.fs`.
- The exports of the eleven component files in the table: after step 1 they widen a swap, never
  reload.
- The debugger's own gate and the trail: moved, not changed.

## As built

Filled in per step, with the pull request numbers.
