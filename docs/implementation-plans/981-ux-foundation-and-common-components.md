# Implementation plan for issue #981

The shared UX foundation and the common components the ten UI/UX groups spend. Companion to
[the grouping index](ux-issue-grouping.md), which groups 42 open UI/UX issues into ten groups by
cause; this plan answers the question that grouping raised: **which parts do those groups share,
and what has to exist before any of them can be built consistently.**

Held to [ADR-0009: UX Design Rules](../adr/0009-ux-design-rules.md). Every component below names
the rule it serves; the call-to-action convention it carries is recorded there.

- [Problem description](#problem-description)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Why there is a G0](#why-there-is-a-g0)
- [Foundation](#foundation)
- [The components](#the-components)
- [What the designs decide](#what-the-designs-decide)
- [What each group spends](#what-each-group-spends)
- [Steps](#steps)

## Problem description

Ten UI/UX groups ([#982](https://github.com/informedica/GenPRES/issues/982) to
[#991](https://github.com/informedica/GenPRES/issues/991)) are about to be built, and they do not
touch ten separate parts of the client. One control serves #405, #398, #397 and #504; one severity
treatment serves #402, #496 and #478; one action bar serves #404, #394 and #495. The client they
build on has no shared button, no shared dialog shell, no search control, no theme palette and two
byte-identical implementations of the severity underline, so each group would invent its own and
the interface would end less consistent than it started. Three of the reported issues are a single
duplicated block seen from three angles, and one of the duplicates deletes prescriptions without
asking while its twin confirms.

## Approaches considered

1. **Build each group on what exists.** No shared work; each group adds what it needs where it
   needs it. Cheapest per group, and the reason the client looks as it does now.
2. **A full design system first** — tokens, spacing, typography, a component library with its own
   documentation and its own review. Correct in the large, and it stops the ten groups for as long
   as it takes.
3. **A foundation and a component set sized to what the ten groups actually spend**, landing
   before them, with each component's call sites migrated as its group arrives.

## Chosen approach

Option 3. The component set is derived from the ten groups rather than from a catalogue, so
nothing is built that no group spends: twelve components and four foundation items, each with the
issues it serves and the call sites it replaces named below. The foundation (F1 to F4) lands before
the components (C1 to C12), because a call-to-action convention and a severity colour cannot be
*stated* anywhere until the palette and the severity type exist — only repeated.

A design system is not ruled out; ADR-0009 says that if one is written it cites the ADR rather
than replacing it, and this catalogue is what it would be written from.

## Confidence

Medium-high. The measurements below are read off the client, so what has to change is known. Two
things are not. The visual decisions behind the seven Figma-gated issues are an input still to
arrive for some of the components (#394, #402, #405, #496, #500, #502, #504), and there is no
browser test harness (#598), so a rendering change is verified by hand. That is the argument for pushing every
rule that can be tested into `GenPRES.Shared` or `Client.Core`, where Expecto reaches it.

## Why there is a G0

The ten groups are not independent. One control serves #405, #398, #397 and #504. One severity
treatment (how the UI handles severity) serves #402, #496 and #478. One action bar serves #404, #394 and #495. Built group by
group, each would be invented separately, and the interface would end less consistent than it
started.

The measurements, from reading the client:

- **30 inline `Button` / `IconButton` in Views and Pages, 52 across the client, and no shared
  button.** The clear/reset block — `<Button variant="text" … fullWidth startIcon={Mui.Icons.Delete}>`
  in a `Box` with a top margin — is written out **five times, differing only in the handler and
  the disabled test**: `Views/Prescribe.fs:535`, `Views/Formulary.fs:407`,
  `Views/Parenteralia.fs:220`, `Views/Patient.fs:474`, `Views/OrderPlan.fs:359`. A sixth,
  `Views/Interactions.fs:339`, is the same button without `fullWidth`. #394 reports it as "too
  wide, gets clicked accidentally" — it is the same button in five places, and
  `OrderPlan.fs:359` deletes prescriptions with **no confirmation**, while
  `Views/Nutrition.fs:1595` confirms an equivalent delete.
- **Eight dialogs in three incompatible idioms.** `Modal` + `Box sx={modalStyle}` four times —
  and `Pages/GenPres.fs:156` re-declares its own `modalStyle` rather than using
  `ViewHelpers.modalStyle`; a real `Dialog` with title, content and actions four times; and the
  one dialog that *is* factored out, `ViewHelpers.PrintView.PrintDialog`. No shared shell, no
  confirm dialog.
- **The severity double underline is implemented in two modules and four places.** The same
  three CSS lines (`textDecoration: "underline double"`, `textDecorationColor`,
  `textUnderlineOffset: "3px"`) appear at `Components/SimpleSelect.fs:313`, over `Level`, and
  three times inside `MUI.fs`'s `fromTextBlock` — `:1067`, `:1084`, `:1101`, one per text run —
  over `TextBlock`. The two modules carry separate colour tables that agree only by convention,
  with a third set of hard-coded hexes in `Pages/GenPres.fs:227` (`formularyBg`) and a fourth
  mapping in `Views/Formulary.fs:238`.
- **No free-text search exists anywhere in the client.** Six `TextField`s in total, four of them
  the same six-digit PIN field copied (`Views/SessionGate.fs:116,137,158`,
  `Views/SignDialog.fs:153`). `Components/Autocomplete.fs` is a fixed-options combobox, not a
  search.
- **The theme sets no palette.** Both `createTheme` calls (`App.fs:1859`, `:1882`) set eight
  tokens, all sizing and density; `Mui.Colors` is 18 hand-copied hex palettes with no link to
  the theme. Roughly **300 inline `sx` literals** against three bindings in `Mui.Styles`.
- **328 lines are dead**: `Components/ElmishSelect.fs`, `StateSelect.fs`, `Slider.fs`,
  `BottomDrawer.fs` — zero call sites, referenced only by the `.fsproj` compile list.

The designs say the same thing from the other side. The TPN redesign — 13 Figma frames — is
built almost entirely out of six repeated parts: a five-segment quantity stepper
(`--5 | – | value | + | +5`), a value chip, a disclosure step that collapses to a one-line
summary carrying its result, a row that greys label, stepper and range together when unchecked,
a highlighted totals row naming what a change moved, and a two-button action bar. #404's frame
shows that same action bar as `RESET` outlined beside `OK` contained.

## Foundation

Lands before any component, so that no component is built twice.

### F1 — one severity type, one renderer

Four representations exist today, none converting to another:

| Where | Type | Payload |
|---|---|---|
| `Shared/Types.fs:178` | `Level` = `IsNormal \| IsCaution \| IsWarning \| IsAlert` | none |
| `Shared/Types.fs:334` | `TextBlock` = `Valid \| Caution \| Warning \| Alert` of `TextItem[]` | the text |
| `GenFORM.Lib/Check.fs:27` | `Check.Severity`, 9 cases (`OverAbsolute`, `AdvisoryOverNorm`, `UnderNorm`, `UnitMismatch`, …) | the reason |
| `Pages/GenPres.fs:227` | `"#fdeded"` / `"#fff4e5"` / `"#e5f6fd"` | none |

`Check.Severity` is the only one that carries *why*, and it is flattened into free text at
`ServerApi.Server`'s dose-check build before it reaches the client. On the order path the
severity is a one-bit `isWithinConstraints` test (`GenORDER.Lib/Order.fs`, five `toDto` sites),
and which colour it becomes depends on *where in the order tree* the breach happened, not how
badly it breached.

What this means for C4: "why is this orange?" is answerable **today, client-side**, for order
variables — the client already receives `DefinedConstraints`, `CalculatedConstraints` and the
`Variable` itself, which is exactly the input the server's own test uses, so the crossed bound
can be named without a new field. The richer *rationale* (which rule, advisory versus absolute)
exists only on the formulary path and never reaches the order path.

### F2 — a theme palette

`palette` added to both `createTheme` calls; `Mui.Styles` grown from its three bindings (one
colour constant and two visibility toggles, four of whose seven usages are in dead modules) to
the tokens the components need; `Mui.Colors` reduced to what the palette does not cover. Until
this exists, a call-to-action convention and a severity colour cannot be *stated* anywhere —
only repeated.

The palette states MUI's own defaults for the four severities and the primary accent, so
nothing changes on screen until a step migrates to it. The client shows severity two ways today:
the alert boxes on the MUI defaults, and the double underlines and the formulary text on
`Colors.Green.700`, `Blue.600`, `Orange.700`, `Red.700`. F1b brings the second onto the first,
and that is where the underline colours shift, slightly. The tints behind a caution, a warning
and an alert are a custom `severityBg` entry, the three hexes `Pages/GenPres.fs` has, since
MUI's palette has no name for an alert box's background. The tokens in `Mui.Styles` are palette
paths (`"warning.main"`, `"text.secondary"`, `"grey.100"`), resolved by `sx`; `headerBgColor`
stays a hex, since the palette has no name for that tint yet.

### F3 — a label convention for shared components

`Terms` is a flat DU of ~174 cases, of which 99 are referenced — 43% is dead. Resolution is a
linear scan with hard-coded column indices over a Google Sheet the *browser* fetches, and the
fallback is an inline Dutch literal at each of ~161 call sites, used indistinguishably for
"sheet still loading", "sheet failed" and "term missing". About 34 user-visible strings are in
JSX with no term at all.

Worst of all for this work: `Views/ViewHelpers.fs` — the file a component library grows out of —
has **zero** `getTerm`, and already hard-codes Dutch (`"onbekend"`, `"Paraaf arts:"`, the nine
print-header labels).

So: a shared component takes its label as a prop, or as a `(term, default)` pair, and never
reaches for `Terms` itself.

### F4 — delete the dead, fix what it hides

- `Components/ElmishSelect.fs`, `StateSelect.fs`, `Slider.fs`, `BottomDrawer.fs` — 328 lines,
  zero call sites, plus their `.fsproj` compile lines. Three of the four are near-duplicates of
  `SimpleSelect` and would be mistaken for prior art.
- `Mui.TypoGraphy.create`, `createStrong`, `createWithColors`, `createStrongWithColors`, and
  `Mui.useTheme` — dead.
- **Defect**: `MUI.fs` renders `Italic` as `<strong>`, so italic text is bolded and never
  italicised.
- **Defect**: `Components/BasicTable.fs` accepts a `header` it never renders, and imports
  `TableHead` for it. Its one caller passes an empty header, so the prop is a promise nothing
  keeps.

The truncated `background: {| |}` on `Theme.palette` this document reported is **not** on
`master`: the record carries `paper` and `` `default` ``. It was corrected before this plan was
written, and the reading it came from was stale. Nothing to do.

## The components

Prop shapes are sketches, in the anonymous-record style the existing components use. They are
the shape of the decision, not a signature to copy.

### C1 — `QuantityField`

Serves **#405, #398, #397, #496, #504, #978**. The field is one entry of a list the server sends
with the order (#978): which order variables to show, in what order, which are changeable and which one to
start from. The client renders the list; it decides neither the order (#397) nor the field to
point the user at (#496).

```fsharp
{|
    label: string
    value: string                        // rendered value with unit
    range: string option                 // the recommended range, shown beside the value
    severity: Severity                   // F1
    smallStep: (Direction -> unit) option
    largeStep: (Direction -> unit) option
    bounds: {| toMin: (unit -> unit) option; toMax: (unit -> unit) option |}
    enabled: bool                        // greys label, stepper and range together
    isLoading: bool
    isLead: bool                         // the field the server says to start from (#496)
|}
```

Today the stepper is an `endAdornment` inside the select (`SimpleSelect.fs:180-287`), which is
why a field with a stepper can show no clear cross and a field that shows one has an inert
handler (`ViewHelpers.orderSelect:46`, `updateSelected = if isEmpty then ignore else …`) — that
is **#398**. And `first`/`last` mean *min/max* or *large step* depending on `navigable`
(`ViewHelpers.createStepper:69`), which is exactly **#405**'s finding that the outer buttons
carry two intents. Separating the stepper from the input gives both a home, and makes room for
double/halve. The order of the fields is not the component's to know: `Views/Order.fs` today
hard-codes it per dose type, and the list the server sends replaces that.

That replacement is #978, in G3, and it is **not** a prerequisite of this component. C1 renders one
field and knows nothing about the order of a list; what changes in G0 is only *where* the order
lives — from inside the component to its caller. The first caller therefore keeps the case per dose
type `Views/Order.fs` holds today, moved to the call site as a literal list. When #978 lands, that
literal is replaced by the list the server sends and C1 is not touched.

Part two lands in two pull requests. The first is the field itself, `Components/QuantityField.fs`:
the value picked from what the rules allow through `SimpleSelect`, the stepper beside it, the
prediction of a stepped value moved here from the select (which is a plain select again), and
the lead marking as an accent on the field's left; `ViewHelpers.orderSelect` builds the field, so
every order and nutrition field is on it without a caller changing, and passes no lead yet. The
second turns the dialog's fixed sequence of fields into the literal list per dose type: one array
at the call site, in the order shown, each field still deciding for itself whether it applies to
the order. It names no lead: which field the user starts from is a clinical decision the server
sends with the order (#978), not one to make at the call site meanwhile. The `bounds` and `largeStep` of the prop shape above are still the stepper's
`first` and `last`, which mean the one or the other by whether the value is navigable: taking
them apart is #405's design decision in G3, and the field keeps the shape its callers have until
that lands.

### C2 — `PickField` and `MultiPickField`

Serves **#498, #403, #501, #487**.

```fsharp
{|
    label: string
    options: (string * string)[]
    selected: string option              // string[] for the multi variant
    onChange: string option -> unit
    clearable: bool
    isLoading: bool
    enabled: bool
|}
```

One rule for empty, one option, loading, disabled and clear — today `filterSelect` disables on
*empty* but not on *one* (**#498**), while `orderSelect` auto-selects a single value and stays
enabled. Re-picking without clearing first (**#403**, **#501**) is a cascade decision in the
`OrderContext` module of `Shared/Models.fs` — not a widget one, and not in `Client.Core`, whose
machine only carries the context to the server — but the widget has to permit it.
`MultiPickField` draws checkboxes, which is **#487**: the filter was a multi-select and nobody
could tell.

### C3 — `SearchField`

Serves **#503, #400**. Nothing like it exists. The user tests found searching beats filtering;
`Components/Autocomplete.fs` is a fixed-options combobox with no `freeSolo`.

### C4 — `SeverityMark`

Serves **#402, #496**. The colour, an icon, and the reason on demand — replacing the two
byte-identical double-underline implementations. The reason comes from the constraints the
client already holds (see F1), so this needs no new server field; #402's user-test finding was
explicitly *no popup*, reason on hover.

### C5 — `ValueChip`

Serves **#504, #496, #505**. A derived or reference value in a pill, coloured by F1's severity.
The TPN frames use it throughout: `550 ml`, `137 ml/kg/dag`, the green `20 ml/kg/dag` headroom,
`25 ml/uur`. Today these are bare `Typography` runs.

### C6 — `Notice`

Serves **#478, #911, #977, #488, #682**. Severity, message, optional action, and the empty state as its
own case. Replaces seven hand-built `Alert`s — one of which renders an empty state as
`severity="success"` (`Views/Interactions.fs:263`) and one of which is an unstyled fragment
(`Views/Prescribe.fs:186`). #478 needs it to say *what to do and whom to contact* when no order
can be created; #911 is the same dead end reached from the infusion-pump list. Decided: when no
dose can be shown, for no dose rules or any other reason, the page stays and this component says
why; the page switch to the emergency list goes (#977).

### C7 — `ActionBar` and `ActionButton`

Serves **#404, #394, #495**. The call to action (the button that completes the task: OK,
submit, sign) contained and on the right; the reset secondary, outlined and on the left;
destructive separated; all bounded rather than full-width. Replaces the 30 inline buttons, the five identical full-width
delete blocks, `Views/Nutrition.fs:1355`'s private `AddButton`, and the three copies of the print
button.

### C8 — `ConfirmDialog` and `DialogShell`

Serves **#394, #399, #495**. Replaces the three dialog idioms and the duplicated `modalStyle`,
and closes the gap where the order plan deletes prescriptions without asking while nutrition
asks.

### C9 — `Disclosure`

Serves **#489, #504, #496, #976**. A section that collapses to a summary **carrying its result** — the
TPN frames show a finished step as `Vocht: Er is 20 ml/kg/dag ruimte over voor TPV` — and that
closes when the user says so, not on a timer. `Components/Accordion.fs`, since renamed
`Disclosure.fs`, is the base; the 5-second
idle effect in `Views/Patient.fs:145` is **#489** and does not survive. For an identified
patient (#976) the panel's summary is the computed age and the measured values, and collapsed is
its resting state, since the age cannot be changed there.

### C10 — `SectionHeading`

Serves **#397, #500**. The caption divider, written out six times with the same three hard-coded
Dutch captions (`Views/Order.fs:910,916,922`, `Views/Nutrition.fs:1142,1152,1155`).

### C11 — `ListToolbar`

Serves **#502, #487, #503**. One search, one filter, print, and whatever of the MUI toolbar
survives. Today `Components/ResponsiveTable.fs` has two independent filters — the custom
`MultipleSelect` above the grid (`:307`) and `GridToolbarFilterButton` in the toolbar (`:466`) —
which is #502, and the confusion behind #487.

### C12 — `PrintTable`

Serves **#510**. `Views/EmergencyList.fs:246` and `Views/ContinuousMeds.fs:204` are structurally
identical print tables with the same `hdr15`/`hdr20`/`hdr25` idiom.

### Kept and absorbed

`Components/ResponsiveTable.fs` already takes a per-row `actions: ReactElement option` that every
caller passes as `None` — **#399**'s adjust button needs that hook and a column, not a new
component. `Components/ClickCountingButton.fs`, `Components/Accordion.fs` (now `Disclosure.fs`) and
`ViewHelpers.PrintView.PrintDialog` stay and are absorbed into C1, C9 and C8.

## What the designs decide

Two things the TPN frames settle, and one they contradict.

- **A row greys as a unit.** Unchecking a component greys its label, its stepper *and* its
  recommended range together. That is the general form of #498 — the controls disappear with
  the option, not just the option — and it applies to fields, not only to checkbox rows.
- **The totals row that moved is highlighted.** After a component is toggled, the affected totals
  row (`natrium`) is highlighted rather than announced. That is how the design answers #496 —
  which field do I change to move the dose — without the popup the #402 user tests rejected.
- **Decided: the call to action is the prominent one and goes on the right; the reset is
  secondary and goes on the left**, recorded in [ADR-0009](../adr/0009-ux-design-rules.md).
  #404 says the call to action goes on the **right**, and its
  own dialog frame has `RESET` outlined left and `OK` contained right, which is the convention.
  The TPN frames put the contained button (`VERZEND NAAR APOTHEEK`) on the **left** of the
  outlined one (`+ VOEG TOE AAN ORDER PLAN`) and have to follow it. C7 applies the convention.

## What each group spends

| Group | Umbrella | Components |
|---|---|---|
| G1 one dropdown, one set of controls | #982 | C2, C1, C7, C8 |
| G2 finding a medication | #983 | C3, C11, C2 |
| G3 the dose dialog | #984 | C1, C4, C10, C5 |
| G4 what the rules allow | #985 | C6, C5, C4 |
| G5 the patient | #986 | C9, C6, C2 |
| G6 the order plan | #987 | C8, C11 (row actions), C12 |
| G7 nutrition and TPN | #988 | C1, C5, C9, C7, C2 |
| G8 layout, hierarchy, navigation | #989 | C7, C10, and F2 |
| G9 beyond the UI | #990 | none — it is not a UI group |
| G10 the tab | #991 | C6 |

A component's call sites are migrated with the group that spends it, not all at once: G0 lands the
component and the first caller, and each group takes the rest of its own.

## Steps

One pull request per step, in order. Each stays within the 200 changed source lines of
`CONTRIBUTING.md`; a step that would not is split at the file boundary named in it. Client
rendering falls under the UI exception of the script policy; anything landing in `GenPRES.Shared`
is prototyped in a script and migrated by the maintainer.

### Foundation

1. **F4a — delete the dead modules.** `Components/ElmishSelect.fs`, `StateSelect.fs`, `Slider.fs`,
   `BottomDrawer.fs` and their `.fsproj` compile lines; the dead `Mui.TypoGraphy` helpers and
   `Mui.useTheme`. Three of the four are near-duplicates of `SimpleSelect` and would otherwise be
   mistaken for prior art by the steps below.
2. **F4b — fix the defects the dead code hid**: `MUI.fs` renders `Italic` as `<strong>`, so no
   text in the client is ever italic; and `Components/BasicTable.fs` accepts a `header` it never
   renders. The prop goes rather than gaining an implementation — its one caller passes an empty
   header, so a header arrives with the first caller that wants one, rendered. The third defect
   this plan listed, a truncated `Theme.palette.background`, is not on `master`.
3. **F2 — the theme palette.** `palette` on both `createTheme` calls, `Mui.Styles` grown to the
   tokens the components need, `Mui.Colors` reduced to what the palette does not cover. Nothing
   migrates to it yet; it is what the later steps spell colours in. Two pull requests: the
   palette and the tokens, then the eleven hue tables nothing uses (169 lines, which with the
   first would pass the size limit).
4. **F1a — one severity type.** In `GenPRES.Shared`, so it is reachable from `Shared.Tests`:
   `Level`, `TextBlock`'s four cases and the colour tables of `SimpleSelect` and `MUI.fs` collapse
   to one type with conversions from what the wire carries. Script first, with a test per
   conversion. Landed as `Severity` in `Shared/Types.fs`, four cases declared lowest to
   highest so the highest of a set is their maximum, and the `Severity` module in
   `Shared/Models.fs`: the conversions from and to `Level` and `TextBlock`, the highest over
   blocks and over rows of blocks, and `isRaised`; `TextBlock.maxTb` is now that, where it
   ordered the cases with integers. Tests in `Shared.Tests/SeverityTests.fs`. `Level` and
   `TextBlock` stay on the wire; the colours stay out of `Shared`, which knows no palette, and
   are F1b's.
5. **F1b — one renderer**, replacing the two byte-identical double-underline implementations
   (`SimpleSelect.fs` and `MUI.fs`'s `fromTextBlock`) and the hard-coded hexes in
   `Pages/GenPres.fs`. The renderer is `Mui.Styles.markSx`: a severity over an sx, adding the
   double underline in the severity's palette colour or nothing, beside `severityColor` and
   `severityBg`. Two pull requests: `MUI.fs` alone, where `Styles` has to move above
   `TypoGraphy` for `fromTextBlock` to reach it, then `SimpleSelect`, `ViewHelpers.getWarning`
   and the menu tints in `GenPres.fs`. The `Views/Formulary.fs` hexes this plan listed are not
   there: that file colours its markdown text with `Colors.Indigo`, which is not a severity and
   stays. The formulary text shifts from `Colors.Green.700`, `Blue.600`, `Orange.700`,
   `Red.700` to the palette's `success`, `info`, `warning`, `error`: the shift F2 announced.
   The second pull request puts the order fields on the same mark: `SimpleSelect` takes a
   `severity` where it took a colour string, `ViewHelpers.severityOf` replaces `getWarning`
   and its colour table, and the menu tints come from `Styles.severityBg` over the dose check's
   highest severity. `Colors.Green`, `Orange` and `Red` are unused after it; Green goes with
   it, Orange and Red with the next change that touches `MUI.fs`, for size.
6. **F3 — the label convention.** A shared component takes its label as a prop or as a
   `(term, default)` pair and never reaches for `Terms` itself; `Views/ViewHelpers.fs`'s
   hard-coded Dutch (`"onbekend"`, `"Paraaf arts:"`, the nine print-header labels) becomes props.
   The `Terms` dead weight and the fetch-from-the-browser question are not in scope here.
   Landed as `Global.printLabels`, the one place the print labels are resolved, handed to
   `PrintView.PatientHeader`, `PatientSignature` and `patientWeight` as props by the three
   views that print; and `ResponsiveTable` takes its column filter's label as `filterLabel`.
   None of the print labels has a term in the sheet yet, so `printLabels` holds the Dutch the
   print always had and is where a term lands when one exists; that is the `Terms` work this
   step leaves out. `TitleBar` keeps its own `Login` and `Password` and reaches for `Terms` for
   the session words: it is page chrome with the environment in hand, not a shared component.
   The MUI grid toolbar's own words (`Select columns`, `Density`) are C11's.

### The components

Ordered by how many groups spend them, so that the most-shared parts exist first, and taken after
the six foundation steps above. Each step lands the component plus its first caller; the remaining
call sites migrate with their group.

1. **C7 `ActionBar` / `ActionButton`** — carries the ADR-0009 convention, and is the step that
   makes #394 a one-line change for the five duplicated delete blocks. Landed as
   `Components/ActionBar.fs`: an action is a label, a `Kind` (`Primary`, `Secondary`,
   `Destructive`), a handler, a disabled flag and an icon; `ActionButton` draws one by its kind,
   contained, outlined, or text in the error colour, never full-width; `View` places them,
   destructive and secondary on the left, primary on the right, whatever order they are given
   in. First caller: the order dialog's `Reset` and `Ok`, the frame #404 drew the convention
   from. The five delete blocks, the nutrition add buttons and the print button migrate with
   their groups.
2. **C8 `ConfirmDialog` / `DialogShell`** — replaces the three dialog idioms and the duplicated
   `modalStyle`, and closes the gap where `OrderPlan.fs:359` deletes prescriptions without asking
   while `Nutrition.fs:1595` confirms. This is a safety fix, not only a refactor. Three pull
   requests, for size: `ConfirmDialog` first, with the order plan's delete now asking and the
   nutrition dialog on the component; then `DialogShell` with the order plan's and the page's
   modals on it, which gives the disclaimer and the session gate the rounded corners and the
   inner scroll the order dialog had and they did not; then the prescribing page's modal, whose order view moves out of the JSX into
   a binding and is re-indented, which alone is near the limit, and `ViewHelpers.modalStyle`
   goes with it. The confirming button of a `ConfirmDialog` is the primary action, contained
   and right, also when it deletes: the title and the text say what happens, the placement
   says what completes the question.
3. **C1 `QuantityField`, part one: split the stepper from the input.** The stepper leaves
   `SimpleSelect`'s `endAdornment`, which is what makes #398's clear cross possible at all.
   Landed as `Components/Stepper.fs`: the five buttons as one group, a step the caller does
   not offer drawn disabled, the clicks sent to the caller as small and large deltas; it knows
   nothing of the value. `SimpleSelect` renders it beside the select instead of inside it, keeps
   the optimistic display of a stepped value (the deltas belong with the value they predict,
   and move to the field in part two), and puts the clear cross in the adornment whenever the
   caller asks for one. The stepped fields of the order and nutrition dialogs still ask for
   none, so nothing shows there yet: which fields get a cross is G1's one rule, and #398 is
   done when that rule lands, not here. The select's own arrow, hidden while the stepper sat
   in the adornment, shows again on those fields. The step buttons are as tall as the input's
   line, so a row of them on the input's baseline centres on its text, and the row wraps under
   the select where a cell is too narrow for both. Most of the diff is the button block moved
   to its own file.
4. **C1 part two: the field.** Value, range, severity, small and large step, bounds, lead marking.
   Rendered from a list, so it takes the order of its entries from its caller rather than knowing
   one. The first caller passes the case per dose type `Views/Order.fs` hard-codes today, moved
   from the component to the call site as a literal; #978 in G3 later replaces that literal with
   the list the server sends, without touching C1. Neither this step nor #397 waits on the server
   work, and the server work needs no client change when it arrives.
5. **C2 `PickField` / `MultiPickField`** — one `disabled / one-option / clear` rule, checkbox rows
   for the multi variant (#487, #498). Two pull requests: the rule first, as `PickPolicy`,
   script-first for `Client.Core` since a rule that can run under Expecto does, and landed as
   `Client.Core/PickPolicy.fs` with `Client.Core.Tests/PickPolicyTests.fs`; then the
   two components, rendering what the rule answers. Landed as `Components/PickField.fs` and
   `Components/MultiPickField.fs`. `PickField` renders the rule's answer through `SimpleSelect`
   and applies the one option a field shows chosen to the page as well, once, when the field
   arrives at it, so the next field of a cascade can follow; `ViewHelpers.filterSelect` builds
   it, which puts every filter select of the prescribing, formulary, parenteralia and
   nutrition pages on the rule at once. `MultiPickField` draws every option as a row with a
   checkbox, the chosen ones ticked, and the closed field shows their labels; it replaces
   `MultipleSelect` at its two callers, the table's column filter (#487) and the prescribing
   page's component select, and `MultipleSelect` goes. The rule: no option is disabled and empty; one option is shown chosen
   and disabled, since there is nothing to pick and clearing it would choose it again; more
   options are enabled when the caller says so, show what was chosen if the options still hold
   it, and offer the cross only when the caller allows, the field is enabled and something is
   chosen. Loading is not the rule's: it only replaces the cross with the spinner.
6. **C4 `SeverityMark`** — colour, icon and the reason on hover, composed from the
   `DefinedConstraints` and `CalculatedConstraints` the client already receives, so no new server
   field (#402, and explicitly no popup). Two pull requests: the reading first, as
   `SeverityReason`, script-first for `Client.Core` and landed as `Client.Core/SeverityReason.fs`
   with `Client.Core.Tests/SeverityReasonTests.fs`: from a
   marked order variable, the bound its values cross, the maximum by the values' top or the
   minimum by their bottom, in the values' unit, honouring whether the bound itself is allowed;
   `Outside` when the server marked it and the bounds do not say why. Then the mark itself,
   with the icon and the reason on hover, and the order fields on it: landed as
   `Components/SeverityMark.fs`, an icon per severity in the severity's colour with the reason
   in a tooltip on hover and nothing else, no popup; `QuantityField` shows it between the
   value and the stepper beside the underline it already had. `ViewHelpers.markOf` reads a
   `Mark`, severity and reason, from an order variable, the reason worded as "max 15 mg" or
   "min 2 mg" in the value's unit and left unsaid for `Outside`; `orderSelect` takes the mark
   where it took a severity, so every order and nutrition field carries the reason.
7. **C6 `Notice`** — severity, message, optional action, and the empty state as its own case;
   replaces the seven hand-built alerts, including the empty state rendered as
   `severity="success"`. Landed as `Components/Notice.fs`: a `Kind` of `Success`, `Info`,
   `Warning`, `Error` and `Empty`, the last outlined and without an icon, since nothing found
   is neither a success nor a failure; a title when the message needs one, an action when
   there is one to take, dismissal when it may be dismissed. All seven alerts are on it: the
   interactions page's nothing found and the prescribing page's ask for patient data, both
   `Empty` now; the prescribing page's missing-dimension notice; the order plan's record-
   moved-on bar with its open-newest action; the formulary's two one-line dose-check outcomes;
   and the app's server error with its title and its dismissal. The snackbar's alert stays:
   it is a toast, not a notice on a page.
8. **C5 `ValueChip`** and **C10 `SectionHeading`** — both small, both spent by three groups.
   Two pull requests, one each. C10 landed as `Components/SectionHeading.fs`: the section's
   name on a line across, in small type, with an action beside it when the section has one.
   The six captioned dividers of the order dialog and the nutrition page are on it, and the
   nutrition section's heading with its remove button, whose stack the component now holds.
   The preparation and administration captions read their terms where a term exists; the
   dosing caption has none and stays the Dutch it was, at the call site. C5 landed as
   `Components/ValueChip.fs`: the value as text in an outlined small chip, coloured by its
   severity, a name before it when it needs one. First caller: the nutrition page's
   accordion summaries, which show a scenario's administration as one chip per value the
   server printed, the frequency, the dose, the rate, the time, each coloured by its own
   severity, where they flattened the values into one underlined run of text.
9. **C9 `Disclosure`** — a section that collapses to a summary carrying its result, and closes
   when the user says so rather than on a timer (#489's five-second effect does not survive).
   Landed as `Components/Disclosure.fs`, `Accordion.fs` renamed and reshaped: open or folded,
   what toggling does, the summary shown either way, the content shown when open. Its three
   callers, the patient panel and the nutrition page's two accordions, are on it, and the
   patient panel's five-second idle effect and the overlay test that postponed it are gone:
   the panel opens and closes when the user says so, which is #489.
10. **C3 `SearchField`** — nothing like it exists in the client today. Landed as
    `Components/SearchField.fs`: a text field with a search icon and a cross that empties it,
    telling the caller the text and nothing more. First caller: `ResponsiveTable`, which takes
    a `searchLabel` and, when given one, shows the field above the grid beside the column
    filter and keeps the rows in which any cell contains the text, case aside; the emergency
    and infusion-pump lists ask for it (#503), the order plan does not. The search has no term
    in the sheet yet, so the lists pass the Dutch, at the call site.
11. **C11 `ListToolbar`** — one search, one filter, print, and whatever of the MUI toolbar
    survives; keeps `ResponsiveTable`'s per-row `actions` hook that #399 needs. Landed as
    `Components/ListToolbar.fs`: the row of the user's own controls above a list, the search
    and the column filter, drawn the same above the cards and above the grid, and nothing when
    the list has neither. The grid's own toolbar survives whole but for one button: columns,
    density, export and print stay, its filter button goes, so the filter above the grid is the
    one filter on the list, which is #502's two filters and the doubt behind #487 whether the
    list is filtered at all. The toolbar's own words stay the grid's, and the per-row `actions`
    hook is untouched.
12. **C12 `PrintTable`** — the two structurally identical print tables. The component and its
    two callers are more than one pull request can carry, so it lands in three: the component,
    then each list on it. Landed as `Components/PrintTable.fs`: the patient it is for at the
    top, the rows in a table of named columns, a line to sign at the bottom, and the emphasis
    the screen drew a value in left off the paper. A column says which value of a row it shows,
    the name above it and how wide it is, so the two lists differ in their columns alone. The
    emergency list is the first on it: its five columns and their widths, and its rows as the
    list handed them, where it built every row and cell of the sheet itself. The infusion-pump
    list follows with its six, which is the last of them.

With it the foundation and the twelve components are all in the client, and the ten groups
have the parts they share. What remains of each issue named above is its own group's work, not
this plan's.

### Verification, per step

- `dotnet run Build`, and `dotnet run servertests` for any step touching `GenPRES.Shared` or
  `Client.Core`.
- After a client change, run Fable and read the generated JSX: the structure, not only that it
  compiles.
- `dotnet fsi scripts/CheckDependencyRule.fsx` — F1a moves a type into `GenPRES.Shared`, which is
  the Contract ring.
- By hand in the browser for anything visual, since no browser harness exists (#598). A step that
  can push its rule into `GenPRES.Shared` or `Client.Core` does so, and gets an Expecto test there
  instead.
