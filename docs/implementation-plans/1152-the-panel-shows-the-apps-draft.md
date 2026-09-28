# Implementation plan for issue #1152

The patient panel shows the App's draft, not a copy of its own: clearing an estimated weight
or height must not leave a panel that shows nothing while the doses rest on the estimate.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [The same pattern elsewhere in the client](#the-same-pattern-elsewhere-in-the-client)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Open questions](#open-questions)
- [Verification](#verification)

## Problem description

In anonymous mode, enter an age and clear the height the panel shows. The panel then shows no
weight and no height, while the workbench, the plan and the server go on dosing on the
estimated weight and height.

Identified mode has no clear control for the weight and height: its values are changed, never
cleared. It reaches the same fault another way: with a measured weight and an estimated
height, choose the weight value that is already shown. The panel's copy loses the height
estimate, the App re-estimates it into a draft equal to the one before, and the height
disappears from the panel while the doses keep using it.

## What the code does today

### Cause

It takes three things together:

1. `Views/Patient.fs` keeps a **local copy** of the draft in
   `React.useElmish (init patient, …, [| box patient; box lang |])`. Every edit changes the
   local copy first and then pushes it up through `envPatient.UpdatePatient`.
2. Every setter goes through `Patient.edit` in `Shared/Models.fs`, which blanks **both**
   estimates before it applies the change. That is on purpose: an estimate must never be
   written back as a measured value, and the App fills the estimates again. So the local copy
   loses the weight estimate as well.
3. The App runs `applyNormalValues` over the draft it receives. When the cleared height was an
   estimate, the result is structurally **equal** to the draft before the edit (checked in
   FSI: `draft |> setHeight None |> applyNormalValues = draft` for a six-year-old). Feliz
   `UseElmish` 5.0 re-initialises only when its argument or dependencies differ structurally,
   so it does not re-seed. The panel keeps its blanked copy; the App, the workbench and the
   server keep the estimates.

Two facts of Feliz UseElmish 5.0, read from its source: the program, the `update` closure
included, is built once per initialisation, so `update` captures the props of that render
until the dependencies change (which is why `Patient.fs` keeps `updatePatient` in a ref); and
a re-initialisation needs `arg <> arg'` or `dependencies <> dependencies'`.

### Why earlier releases did not show it

Before `ac34ed4e` (2026-09-25) and the step after it, every setter rebuilt the patient through
`create` from `getWeight` / `getHeight`, which return the **estimate** when nothing is measured.
The estimated weight came back as a **measured** weight: bug #488. As a side effect the draft
always changed after an edit, the panel re-seeded, and the weight stayed visible. Fixing #488
removed that side effect and exposed the stale local copy, which has existed since `f4cef465`
(2026-03-25).

### How many copies of the patient the client holds

| Where | What | Needed? |
|---|---|---|
| `App.State.Ui.PatientDraft` | the draft with the estimate applied; the panel and the lists read it through `IPatient.Draft` | **yes**: it holds data below the minimum that the panel shows and edits |
| `App.State.Lanes.Patient` | `PatientDraft` when it meets the minimum, else `None` | **no**: a stored projection, see below |
| `Views/Patient.fs` local `useElmish` state | a copy of the draft, edited locally | **no**: this copy causes #1152 |
| `Fetches.Formulary.Patient` | the patient sent with the formulary request | wire shape; set from `Lanes.Patient` in `updatePatient` and `LoadFormulary Started` |
| `OrderContextState` / `OrderPlanState` | the patient the machine evaluated for, sent through `PatientChanged` | yes, by design: each machine's own subject |

`Lanes.Patient` came with `7a0b0137` (2026-09-15), when `Patient` was a separate type with a
private representation built by `fromDto`. `026a0351` (2026-09-16) removed that type again, and
`Patient.validate` now hands back the same record or an error. `App.fs` reads `Lanes.Patient`
in six places and writes it only in `updatePatient`, which writes the draft beside it. So
`Lanes.Patient` is meant to equal
`state.Ui.PatientDraft |> Option.bind (Patient.validate >> Result.toOption)`.

It does not always, and that shows the risk of storing it. On an anonymous `UrlChanged`, the
App first calls `updatePatient` with the URL's patient, which applies the estimates to both
fields, and then sets `Ui.PatientDraft = pat` in its own record update, the **raw** patient
from the URL. After a URL change the draft has no estimates, while `Lanes.Patient` and the
workbench have them. The panel and the emergency lists, which read the draft, show and
calculate without an estimated weight until the next edit or the next load of the normal
values. Step 3 fixes this first: deriving the patient from the draft as it is now would send
a patient without estimates with the next formulary request.

## The same pattern elsewhere in the client

Every `React.useElmish` and every `useState` or `useEffect` seeded from props in
`src/Informedica.GenPRES.Client`:

| Place | Local state | Risk |
|---|---|---|
| `Views/Patient.fs` | a copy of the domain draft, edited locally, while the App transforms it | **the bug** |
| `Views/Formulary.fs` | a mirror of the five selections of `Formulary` | **low**: `UpdateFormulary` writes `Resolved form` at once with the value the mirror set, so prop and mirror agree. Still redundant. In passing: `Clear` resets four fields of the mirror, not `Form`; the prop change usually hides it |
| `Views/Parenteralia.fs` | a mirror of `Generic`, `Form`, `Route` of `Parenteralia` | **low**, the same shape as Formulary |
| `Views/Order.fs`, `Views/Nutrition.fs` | the selected component and item: UI state | none |
| `Views/Interactions.fs` | the manual drug list and the input text: UI state the App does not hold | none |
| `Pages/GenPres.fs` | `SideMenuItems`, derived from language, terms and page, stored in the hook | low: a stored projection whose dependencies cover its inputs, except `isMobile`, which `init` reads but no dependency tracks, so a resize does not rebuild the menu state |
| `Components/QuantityField.fs` | optimistic click deltas | none: they reset on the value **and** on a server revision, which is how an optimistic copy avoids an answer equal to the last one. The pattern to copy if a local copy is ever needed |
| `Components/PickField.fs` | none; an effect that picks the single option | none |

`Views/Patient.fs` is the only place where the parent transforms a local copy of domain data
after the edit, so the only place where the round trip can come back equal while the copy
differs. Formulary and Parenteralia follow the same pattern that #897 set aside (a component
hook holds no domain data), but they are safe today.

## Approaches considered

1. **The panel shows the App's draft.** The local hook keeps only UI state; the fields read
   `IPatient.Draft`, and an edit is computed from that prop and sent up. What the panel shows
   is what the doses rest on, always. The direction of #897.
2. **Refuse to clear an estimate.** Clearing a field that shows an estimate does nothing, and
   the panel says an estimate can only be replaced by a measurement. It keeps the local copy,
   and with it the class of bug: any other edit that the App's round trip turns into an equal
   draft goes stale the same way.
3. **Force the re-seed.** Add a revision counter to the dependencies, as `QuantityField` does.
   It works, but it keeps two copies of the draft and repairs their drift instead of removing
   it.

## Chosen approach

Approach 1. It removes the copy instead of synchronising it, needs no new state, and makes the
panel honest by construction: after clearing an estimated height, the field shows the estimate
again, because that is what the calculation uses. Clearing a measured value shows the estimate
in its place.

Removing `Lanes.Patient` is the same idea applied to the App, a separate refactor after the
fix. Formulary and Parenteralia are optional clean-ups.

## Confidence

High for the cause, which is reproduced in FSI and explained by the UseElmish source. High for
approach 1 in the panel; medium for step 4, which needs a browser check that the selects keep
their values through a refresh.

## Steps

Each step is one pull request, one at a time. `src/Informedica.GenPRES.Client` falls under the
client exception of the script-only policy; anything pure that moves to `Client.Core` or
`Shared` is prototyped in a script first and migrated by the maintainer.

1. **Tests in a script.** A script under `Client.Core/Scripts/` or `Shared/Scripts/` loads
   `Shared` and pins the round trip that produces an equal draft, the condition #1152 needs:
   - an estimated height cleared: after `applyNormalValues` the draft equals the draft before;
   - the measured weight chosen again with an estimated height: the same;
   - a measured height cleared: the height estimate comes back, and the measured weight stays.

   These tests pin the precondition; they do not catch a stale copy in the panel, which is
   React state that Expecto cannot reach. The guard against a recurrence is the shape of the
   panel: it keeps no copy of the draft. To make that testable, move the panel's edit
   reducer (the `Msg -> Patient option -> Patient option` in `Views/Patient.fs`, with
   `setDepartment`) to `Client.Core` as a pure module, and test it in two parts, because the
   setters blank both estimates and only the App's `applyNormalValues` fills them again:
   - the edit alone: applied to the App's draft, it changes the one field and leaves the
     estimates blank, and nothing else;
   - the edit followed by the App's step: `draft |> edit |> applyNormalValues` gives the draft
     the App holds, also when that draft is equal to the one before.

   What the panel shows is then checked in the browser, under Verification.
2. **The panel shows the App's draft** (fixes #1152). In `Views/Patient.fs`:
   - remove the `useElmish` over `Patient option`; the fields read `envPatient.Draft`;
   - an edit is `draft |> Patient.setX s |> updatePatient`, computed from the prop on each
     render, so the `updatePatientRef` is no longer needed;
   - keep the `held` guard around every edit;
   - keep the hooks that hold only UI state: `isExpanded`, `heldOpen`;
   - `Clear` becomes `updatePatient None`.

   A changelog entry under Fixed.
3. **Derive `Lanes.Patient`** (`refactor(client)`). First the anonymous `UrlChanged` keeps
   the draft `updatePatient` wrote, with the estimates, instead of setting the raw URL
   patient over it; only the non-anonymous branch keeps `state.Ui.PatientDraft` as it is.
   This is a fix of its own (the lists calculate without an estimated weight after a URL
   change), so it may go first as a separate `fix(client)` pull request. Then add
   `patientOf (state: State) = state.Ui.PatientDraft |> Option.bind (Patient.validate >> Result.toOption)`,
   replace the six reads, and remove `LanesState.Patient`. `updatePatient` still computes the
   patient for `PatientChanged` and `Formulary.Patient`, and keeps the warning for a draft below
   the minimum.
4. **Optional: Formulary and Parenteralia read the prop.** Remove the selection mirrors; the
   selects read `formulary` and `parenteralia` directly, which already hold the values through
   `Refreshing`. The `Clear` that leaves `Form` behind goes with the mirror.

## Open questions

1. **Fix direction.** This plan shows the re-estimated draft (approach 1). Confirm, or choose
   approach 2.
2. **An estimate in the field.** After step 2 the field shows a value the user did not type.
   Should it be marked as an estimate? That touches the look of #1102 and is outside this fix.
3. **The lists dose on the draft.** `calculateInterventions` in `App.fs` and the views of
   `EmergencyList`, `ContinuousMeds`, `Nutrition` and `Prescribe` read the draft, not the
   validated patient. With only a measured weight entered, the emergency list calculates while
   the workbench has no patient. Intended, since an emergency needs only a weight, or the same
   gate?
4. **Steps 2 and 3 in one pull request?** This plan keeps them apart.

## Verification

- Step 1: the script's tests pass in FSI.
- Step 2: in the browser, anonymous mode with a six-day-old and a six-year-old: clear an
  estimated height, clear a measured height, clear a measured weight; the panel always shows
  what the workbench doses on. Identified mode, where weight and height have no clear control:
  with a measured weight and an estimated height, choose the weight value already shown; the
  height estimate stays on the panel. Then
  `dotnet run build`, the Fable output checked, `npx vite build`.
- Step 3: in a running app, once the normal values have loaded, change the URL to another
  anonymous patient with an age only; the panel and the emergency list show and use the
  estimated weight at once. Then `dotnet run build`, `dotnet run servertests`, and the anonymous and identified flows
  in the browser unchanged.
- Step 4: in the browser, the formulary and parenteralia selects keep their values through a
  refresh and after Clear.
