# Implementation plan for issue #2072: keep the summary bars of the nutrition sections in view

2026-11-28. Line numbers are from `master` at `2bc75e29`. The plan also covers #2075, #2076, #2077, #2078 and #2079,
which came out of the same prototype.

## Problem description

On the nutrition page the enteral section and each parenteral slot fold open below a summary bar, the summary of
`Components.Disclosure` (`src/Informedica.GenPRES.Client/Components/Disclosure.fs:27`). When a section is open and its
content is longer than the window, its summary bar scrolls out of view, and the prescriber no longer sees which
section or slot the content belongs to (#2072). With several sections open, the bars of the other sections are out of
view as well (#2075).

The prototype that solves this also changes how the summary bars and the totals look:

- the enteral section has no heading, where the parenteral section has one (#2076);
- the labels and value chips of the summary bars above each other do not line up (#2077);
- a summary bar shows the frequency, the dose quantity, the rate and the time of the order, and not the dose per kg
  (#2078);
- the rows of the totals table are high, and the lines between them are thin (#2079).

## Approaches considered

For the summary bars:

1. Make the summary of every `Disclosure` sticky. The patient view (`Views/Patient.fs:726`) would stick as well.
2. Make the summary of a nutrition section sticky within its own section, as #2072 describes. The bar leaves with its
   section, so only one bar is in view at a time.
3. Stack the summaries of all nutrition sections at the top of the page, each below the ones before it, until the end
   of the nutrition sections (#2075). `Disclosure` gets an option for where the summary goes; the patient view keeps a
   summary that scrolls.

For the values in a summary bar (#2078):

- a. Change the administration text that GenORDER makes in `OrderContext.setOrderTableFormat`
  (`src/Informedica.GenORDER.Lib/OrderContext.fs:162`). That text is shared with every medication order.
- b. Build the chips in the client from the order of the scenario, and leave the administration text as it is.

## Chosen approach

Approach 3 with b.

- **Where the summary goes** is a choice of the caller: `Disclosure` gets the union `SummaryPlacement` with the cases
  `Scrolls` and `Stacks`. The two nutrition views pass `Stacks`, the patient view passes `Scrolls`.
- **A stacked summary escapes its section.** A sticky element sticks only within its parent, so the MUI `Accordion` and
  its heading are taken out of the layout with `display: contents`. The summary then sticks within the element that
  holds all nutrition sections, and stays at the top until the end of it.
- **The place of a stacked summary** is the sum of the heights of the stacked summaries before it in the same
  container. A `ResizeObserver` on the container sets it again when a summary before it changes height, or when a
  section is added or removed, since each of those changes the height of the container.
- **Room between the stacked summaries** is a 6 px bottom border in the background colour. The border is part of the
  height that is added up, and it hides the content that scrolls below the summaries, where a margin would show it.
- **The z-index of a stacked summary is 2**, above the labels of the input fields, which are at 1.
- **The summary chips** (#2078) are built from `Schedule.Frequency`, `Orderable.Dose.Quantity` and
  `Orderable.Dose.QuantityAdjust` of the order, each coloured by the severity of its level. A single value is written
  with the Dutch decimal comma, with the formatter the server uses for the administration text
  (`Decimal.toStringNumberNLWithoutTrailingZerosFixPrecision 3`); a range is written by
  `OrderDisplay.Variable.renderValue`. A value the order does not have gives no chip.

## Confidence

Medium for the stacked summaries: `display: contents` on the accordion and the sticky summaries work in the prototype
in Chrome. Whether a stacked summary stays below the app bar, and whether a screen reader still reads the heading of
the accordion, is checked in step 1. High for the other steps, which change layout and text only.

## Steps

The prototype on the branch
[`proto/2072-sticky-nutrition-summary`](https://github.com/halcwb/GenPRES/tree/proto/2072-sticky-nutrition-summary),
which is not for merging, holds the code of every step. The estimates count the shipped lines of the prototype under
`src/`. All steps are client UI code in `src/Informedica.GenPRES.Client/`, except the new term of step 2 in
`src/Informedica.GenPRES.Client.Core/`.

No test project covers `src/Informedica.GenPRES.Client/`: the Fable views cannot run under Expecto, and `tests/` has
only `Informedica.GenPRES.Client.Core.Tests`. Steps 1, 3, 4 and 5 are therefore checked by hand in the running app,
as each step describes; step 2 is checked by `scripts/CheckLocalization.fsx`.

### Step 1: stack the summary bars of the nutrition sections (#2072, #2075)

- `Disclosure.Props` (`Components/Disclosure.fs:13`) gets `summaryPlacement: SummaryPlacement`.
- `Disclosure.View` (`Disclosure.fs:27`) gives every summary a ref, and a stacked summary the class
  `disclosure-stacked` and the sticky style: position sticky, z-index 2, and the 6 px bottom border in
  `background.paper`. The `Accordion`
  (`Disclosure.fs:60`) of a stacked summary gets `display: contents`, for itself and its heading, and no line above it.
- The effect in `View` that places the summary acts only on a stacked summary.
- `Disclosure.stack` sets the top of a stacked summary to the sum of the heights of the stacked summaries before it in
  its container; an effect in `View` runs it from a `ResizeObserver` on the container.
- `Views/EnteralNutrition.fs:514` and `Views/ParenteralNutrition.fs:280` pass `Stacks`, `Views/Patient.fs:726` passes
  `Scrolls`.
- Check by hand: with the enteral section and two parenteral slots open, the summary bars stack at the top while the
  page scrolls, below the app bar, with room between them; a bar moves up when a slot above it is removed; the patient
  summary scrolls away.
- Estimate: 100 lines.

### Step 2: a heading for the enteral section (#2076)

- `Terms` (`src/Informedica.GenPRES.Client.Core/Models/Terms.fs:194`) gets the case `Nutrition Enteral Section`, and
  `data/localization/localization.tsv` its row: Enteral, Enteraal, Entéral, Enteral, Enteral, Enterale.
- `EnteralNutrition.View` shows the heading above the disclosure, as `ParenteralNutrition.fs:396` does, without a print
  button, with the fallback "Enteral".
- The new case has no behaviour to prototype, so it is added without a script.
- Check: `dotnet fsi scripts/CheckLocalization.fsx`.
- Estimate: 25 lines.

### Step 3: line up the labels and value chips of the summary bars (#2077)

- `NutritionSlot` (`Views/NutritionSlot.fs:505`) gets `summaryLabelSx`: a fixed width of 12rem, cut off with an
  ellipsis. The section labels at `EnteralNutrition.fs:499` and `ParenteralNutrition.fs:261` take it.
- `AdministrationSummary` (`Components/AdministrationSummary.fs`): the order name (`:97`) takes a fixed width of 14rem,
  cut off with an ellipsis and shown in full on hover; each value chip (`:68`) sits in a cell with a least width of
  6.5rem; the words between the chips sit in cells with a least width of 1.5rem (`textSx`, `:33`).
- Check by hand: the labels, the order names and the chips of the summary bars above each other start at the same
  place; a long order name is cut off and shows in full on hover.
- Estimate: 65 lines.

### Step 4: the frequency and the dose quantities in a summary bar (#2078)

- `NutritionSlot.adminSummary` (`NutritionSlot.fs:516`) takes the order instead of the administration text blocks and
  builds the three chips as in the chosen approach.
- `EnteralNutrition.fs:486` and `ParenteralNutrition.fs:251` pass the order of the single scenario.
- `Views/Prescribe.fs:463` and the administration text of the server do not change.
- Check by hand: an enteral feeding shows the frequency, the dose quantity and the dose per kg; a TPN slot shows the
  same with a decimal comma; an order without a weight shows no dose per kg; the administration of a medication
  order is unchanged.
- Estimate: 35 lines.

### Step 5: a compact totals table with thicker row lines (#2079)

- `BasicTable.View` (`Components/BasicTable.fs:58`) uses the small table size and a 2 px line between the rows.
  `Views/Totals.fs` is the only caller.
- Check by hand: the rows of the totals table are lower, with a thicker line between them.
- Estimate: 5 lines.
