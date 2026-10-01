# Implementation plan for issue #504: nutrition component rows

Part of G7, nutrition and TPN, of [the grouping index](ux-issue-grouping.md#g7--nutrition-and-tpn-988).
Refs #504, #495, #506; closes none.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)

## Problem description

The parenteral slots of `Views/Nutrition.fs` (TPN, lipids, electrolytes/glucose) show each
component as two half-width fields: the orderable quantity under "bereiding" and the dose
quantity per kg under "dosering". The component name only appears inside the field label, the
range the rules allow is visible only while no value is picked, and the solution component looks
the same as every other component.

The redesign of #504 lays the slot out as a table: one fixed row per component with the name as
the row label, one stepper for the quantity and the recommended range beside it.

This plan is the layout only. Nothing the user can do changes: no new message, no new server
command. #504 stays open for choosing the glucose percentage and the administration duration, #495
for the confirm step and #506 for custom and second enteral feeds.

## Decisions

Taken 2026-10-01 by the maintainer.

| Question | Decision |
|---|---|
| Scope | Parenteral slots only. The enteral slot keeps its layout. |
| Behaviour | Layout only. The checkbox and the solution dropdown are shown but disabled; making them work belongs to #504. |
| Range column | The defined range of the component quantity, `OrderableQuantity.DefinedConstraints`, with its unit, always shown, also once a value is picked. |
| Checkboxes | Only on the middle components. The first component, the main composition, and the last, the solution, have none. An unchecked row is a row whose quantity is zero or empty, shown greyed. |
| Solution dropdown | One option, the current solution. More options are a follow-up under #504. |
| Severity mark | Stays on the stepper; the range label gets no colour of its own. |
| Dose quantity per kg | The component dose-adjust field leaves the view. |
| Kept as is | The divider with "totaal volume", the administration section (frequentie, toedien hoeveelheid, dosering, infuussnelheid, looptijd) and the Reset bar. |
| Indication | Shown only with more than one option. Already so. |
| Form | Not shown. Already so. |

## Approaches considered

1. Keep the two-column grid and only drop the dose-adjust field. Smallest change, but the name
   stays inside the field label and the range stays hidden after a pick.
2. A three-column row per component: label column, quantity column, range column. Matches the
   design and reuses the existing `QuantityField` unchanged.
3. An MUI `Table`. Gives the column alignment for free, but the steppers and selects inside table
   cells behave differently on narrow screens and no other page uses a table for fields.

## Chosen approach

Approach 2, built from `Grid` rows like the rest of the page.

### Filter row

`filterControls`: `genericFilter` ("Samenstelling") and `doseTypeFilter` ("Doseer type") side by
side in one `Grid container`, each in a `halfSize` cell, so they stack below the mobile breakpoint.
`indicationFilter` follows on its own row. The enteral branch keeps its current stack.

### Column headings

`headerRow` becomes three headings for parenteral slots: "Component", "Hoeveelheid",
"Aanbevolen range". The columns take 4, 5 and 3 of the 12 grid columns on desktop; on mobile the
range column moves under the stepper.

### Component rows

`componentRows`, per component of `ord.Orderable.Components`:

- **Label column**, by position:
  - the first component, the main composition: the name, no checkbox;
  - a middle component: a disabled checkbox, checked when the quantity has a non-zero value,
    and the name;
  - the last component, the solution: a disabled select holding the component name as its only
    option, no checkbox. More options are a follow-up under #504.
- **Quantity column**: the existing `select` call over `cmp.OrderableQuantity`, with the same
  values (`ViewHelpers.ovarValsWithRange`), stepper (`ViewHelpers.createStepper` with the mode from
  `QuantityModePolicy.decideFor ... ComponentQuantity`), severity mark and reopen, but with an
  empty label: the name already stands in the label column.
- **Range column**: a `Typography` with `Variable.renderValue` over
  `cmp.OrderableQuantity.DefinedConstraints`, in brackets, with its unit. Grey when the row's
  checkbox is unchecked.

The dose-adjust field (`doseLabel`, `doseDisplay`) is removed from the view. The
`ChangeComponentDoseQuantityAdjust` message stays until the behaviour work of #504 removes or
reuses it.

### Labels

The new headings are hard-coded Dutch, like the neighbouring "dosering" and "totaal volume".
Issue #1244 moves all of them to `Terms`; adding terms needs a change to `Localization.fs` in Shared,
which is outside this layout change.

## Confidence

High for the layout. The range column depends on `DefinedConstraints` being filled for every
nutrition component; where it is empty, the column stays blank.

## Steps

1. Filter row in `filterControls`, parenteral only.
2. Column headings in `headerRow`, parenteral only.
3. Component rows in `componentRows`: label column by position, quantity column without label,
   range column; remove the dose-adjust field.
4. Mobile layout: filter cells and range column stack.

One pull request, under 150 changed lines of `src/Informedica.GenPRES.Client`.

## Verification

- `dotnet run`, Nutrition page, add TPN, lipids and electrolytes/glucose: the rows match the
  design; stepping, the median click on the range value and the severity marks still work; the
  range is shown before and after a pick; the dose-adjust field is gone.
- The enteral slot is unchanged.
- Below the mobile breakpoint the filter row and the range column stack.
- Fable compiles and the generated JSX nests as intended; `npx vite build` passes;
  `dotnet run servertests` is unaffected.
