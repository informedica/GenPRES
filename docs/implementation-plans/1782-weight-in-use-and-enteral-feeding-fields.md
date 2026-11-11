# Implementation plan for issues #1782, #1760 and #1192

The patient summary shows the weight and height the doses are calculated with, the enteral
feeding slot shows its supplements in the columns of the feeding, only those that are on, a
long select list opens on its selected value (#1760), and the action buttons of the scenario card
in the prescribe view follow the action bar of the other pages (#1192). Drafted 2026-11-11. Line numbers are from `master` at `24ae4e27`.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Open points](#open-points)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)

## Problem description

Seen on the nutrition page with an anonymous patient of 1 year, department ICK, no weight and no
height entered, Nutrilon Standaard 2 as the enteral feeding and Fantomalt pdr turned on.

- Every per kg dose is calculated with the estimates for the age, 11 kg and 78 cm, but the patient
  summary shows only the estimated range, "Gewicht: geschat: (8 - 12 kg)", and the fields show the
  estimates as a grey placeholder. A prescriber cannot see which weight and height are in use.
- The fields of a supplement stand in other columns than those of the feeding.
- Every supplement the link rules offer is a switch in one long list, on or off, which pushes the
  prescribe bar down.
- "Enterale Voeding" stands on the section summary and again on the heading of each feeding slot.
- A long select list does not scroll to its selected value when it opens (#1760). The weight field
  of the patient panel shows it most clearly: about 240 values from 0.4 to 100 kg, and with 25 kg
  chosen the list opens on 0.4 kg. The list should open with the selected value, or the value the
  placeholder stands for, in view, in every `SimpleSelect`.
- The action buttons are not alike from page to page (#1192). In the scenario card of the prescribe
  view, "Bewerken" and "Voorschrijven" are two text buttons side by side on the left, while the
  nutrition slot ends in a bar with Reset on the left and a contained Voorschrijven on the right.

## What the code does today

### The patient summary

`PatientText.toString` (`Client.Core/Models/PatientText.fs:99`) builds the summary line. For the
weight it shows the measured weight in bold, then the estimate: the range from
`Weight.EstimatedP3` to `Weight.EstimatedP97` when both are known, and `Weight.Estimated` only when
one of them is missing (`:124-135`). The height works the same way (`:146-157`). The weight in use
without a measurement, `Weight.Estimated`, is therefore not shown while the range is known.
`calcBSA` (`:66`) already uses the measured value and else the estimate.

The fields show the estimate as a placeholder, "11 (geschat)", while nothing is measured
(`estimatePlaceholder`, `Views/Patient.fs:412`). The fields stay as they are.

### The enteral feeding slot

`Views/EnteralNutrition.fs`:

- `SlotView` lays the composition, the frequency, the dose quantity and the dose per kg out in one
  measured row, `frequencyDoseRow` (`:122`), with sized items and a container query that stacks
  them in one column on a narrow slot.
- `OfferView` (`:34`) shows a supplement as `Components.OfferSwitch.View`: its name with a switch,
  and while it is on the frequency, the dose quantity and the dose per kg in a plain
  `Stack direction="row"` (`:62-71`), so in other columns than the feeding.
- `SlotView` lists every offer of `slot.Offers` under the heading "Supplementen", on or off, above
  the prescribe bar. An offer is turned on or off with `INutrition.TurnOffer(slotId, index, switch)`;
  `OfferSwitch` is `On` or `Off` (`Client.Core/StateMachines/OrderContextMachine.fs:67`).
- The slot heading is `slot.Label` (`:262`), which for an enteral feeding is the term
  `Nutrition Enteral Feeding`, "Enterale Voeding" (`Views/NutritionSlot.fs:614-618`), the same text as
  the section summary (`:372`).
- The term `Nutrition Add Supplement`, "Supplement toevoegen", exists in `Terms.fs` and in
  `data/localization/localization.tsv`, and nothing uses it yet.

### The select lists

- The weight list is built from `400..50..1950`, `2000..100..10000`, `10500..500..20000` and
  `21000..1000..100000` grams (`Views/Patient.fs:386-391`), the height list from `40..220` cm
  (`:393`). The patient fields render through `createField` (`:350`) as
  `Components.SimpleSelect.View`, which `Components/QuantityField.fs` and `Components/PickField.fs`
  use too.
- `SimpleSelect` is a MUI `Select` (`@mui/material` ^9) with a controlled `open`
  (`Components/SimpleSelect.fs:113-143`). A chosen value is the `value` of the `Select`, and MUI
  marks its `MenuItem` as selected. A value that a placeholder stands for, such as an estimated
  weight, gets the class `Mui-selected` and `autoFocus` (`:170-186`); the comment there says the
  focus also scrolls it into view. Every other item gets `autoFocus={false}`.
- Nothing else brings the selected item into view: no `scrollIntoView`, no `MenuProps` for it. On a
  phone the menu paper has `maxHeight = 400` (`:65-69`).

The cause is not confirmed: #1760 was read from the code and not reproduced in a running client.
Not yet known is which case fails (a chosen value, a placeholder value, or both), on which devices,
and why the focus does not scroll; the controlled `open` with the enter transition of the menu, or
the explicit `autoFocus` on every item, are candidates.

### The action buttons

- `Components.ActionBar` (`Components/ActionBar.fs`) draws a row of actions by kind: `Primary`
  completes the task, contained and on the right; `Secondary` discards or steps back, outlined and
  on the left; `Destructive` removes, text in the error colour and set apart. Every button is as
  wide as its label. `NutritionSlot.prescribeBar` (`Views/NutritionSlot.fs:71`) uses it, with
  Voorschrijven as `Primary`.
- The scenario card of the prescribe view ends in a MUI `CardActions` with two raw `Button`s, both
  `size="small"` and without a variant (`Views/Prescribe.fs:432-445`): "Bewerken" with
  `handleEditClick` (`:333`), and "Voorschrijven" with `prescribe` (`:314`), disabled by
  `prescribeDisabled` (`:330`). The label "Voorschrijven" is a literal; the term `Prescribe`
  exists and the nutrition slot uses it.
- Other pages also draw their action buttons by hand (disclaimer, formulary, parenteralia,
  interactions, session gate, settings, sign dialog). They stay for #1192.

### The dose per kg

The dose per kg is shown with `fixPrecision 3`, three significant digits (`Views/NutritionSlot.fs:987`),
as the medication order view shows doses (`Views/Order.fs:1085`). The value 0.00909 g/kg/dag of the
issue is that rounding of a small range minimum, so nothing is missing.

## Decisions

Taken 2026-11-11 by the maintainer.

| Question | Decision |
|---|---|
| Where the weight and height in use are shown | In the summary line. The fields keep their placeholder. |
| The summary text without a measurement | "Gewicht: geschat 11 kg (8 - 12 kg)": the word for an estimate first, then the value in use, then the range. The height the same. A measured value stays as it is shown now. |
| The supplements | Only the supplements that are on are listed, each with its fields; a button, "Supplement toevoegen", opens a menu of the supplements that are off, and a pick from it turns one on. |
| Turning a supplement off | A remove button beside its name, as the feeding slot has; the supplement goes back into the menu. |
| The precision of the dose per kg | No change: `fixPrecision 3`, as the order view. The item is dropped from #1782. |
| The heading of a feeding slot | The name of the chosen feeding, for example Nutrilon Standaard 2; the category term while no feeding is chosen. The remove button stays. |
| The action buttons in this plan | Only the scenario card of the prescribe view; the other pages stay for #1192. |
| The kinds of the scenario card actions | Voorschrijven is `Primary`, on the right; Bewerken is `Secondary`, on the left. |

## Approaches considered

- The weight and height in use: in the summary line, in the fields as a value marked as an
  estimate, in both, or as a notice on the nutrition page. The summary line was chosen.
- The supplements: only those that are on with a way to add the others, those that are on first
  with the others in a fold, or all switches with those that are on sorted first. The first was
  chosen.
- Adding a supplement: a `Components.PickField`, a button with a menu, or a `PickField` for two or
  more with a button for a single one. A `PickField` with one option shows it as chosen and
  disabled (`PickPolicy.offer`, `Client.Core/Policies/PickPolicy.fs:58-63`), and read-only when it
  can be cleared (`Components/PickField.fs:86-90`), so the last supplement that is off could not be
  added. The button with a menu was chosen: it works the same for one supplement as for many, and
  `PickField` and `PickPolicy` stay as they are.
- Turning a supplement off: a remove button, or its switch kept on the row. The remove button was
  chosen, as the feeding slot has one.
- The heading of a feeding slot: the name of the feeding, a number per feeding, or no text beside
  the remove button. The name was chosen.

## Chosen approach

### The patient summary (Client.Core)

In `PatientText.toString`, with no measured weight and a known `Weight.Estimated`, the weight part
reads "geschat 11 kg", followed by "(8 - 12 kg)" when P3 and P97 are known. With a measured weight
the text stays as now. The height the same. A script prototypes the change with its tests, after
the existing tests in `tests/Informedica.GenPRES.Client.Core.Tests/Models/PatientTextTests.fs`.

### The feeding slot (view code)

- The heading of an enteral feeding slot shows `nutritionContext.Filter.Generic` once it is set,
  and `slot.Label` before. Only `EnteralNutrition.SlotView` changes; `slot.Label` stays for the
  other nutrition categories.
- The measured row of `SlotView` becomes a function of its first item, so the feeding row passes
  the composition and a supplement row passes the supplement's name with its remove button. Both
  rows then share the columns and the narrow-slot stacking.
- The slot lists only the offers whose switch is `On`. Below them a `Components.AddButton` with the
  label "Supplement toevoegen" opens a MUI `Menu` of the names of the offers that are `Off`
  (`OrderContextText.offerName`), as `Components/TitleBar.fs` and `Components/Localization.fs` open
  one; a pick sends `TurnOffer(slotId, index, On)`, the remove button `TurnOffer(slotId, index, Off)`.
  The button is not shown when no offer is off.

### The scenario card actions (view code)

The `CardActions` of the scenario card become a `Components.ActionBar.View` with Bewerken as
`Secondary` and Voorschrijven as `Primary`, each keeping its handler, its icon and, for
Voorschrijven, its disabled state. Voorschrijven takes its label from the term `Prescribe`. The
doc comment of `Kind.Secondary` widens from "discards or steps back" to an action that does not
complete the task, so that it covers an edit.

## Open points

1. Whether the estimated weight and height in use are bold, as a measured value is.
2. For #1760: which case fails, on which devices, and the cause, found by reproducing it in the
   running client; the fix follows from the cause.
3. Whether `Components.OfferSwitch` is still needed once the enteral slot no longer uses it: the
   prescribing page (`Views/Prescribe.fs`) uses it too, so it stays unless that page changes.

## Confidence

High for steps 1 to 3 and 5: they change one text function and two views, with the machinery to
turn an offer on and off and the action bar already in place. Low for step 4 until the cause is
found.

## Steps

One pull request at a time, each merged before the next.

1. **The patient summary** (`feat(client)`): `PatientText.toString` as above, script first, with
   the tests moved to `PatientTextTests.fs`.
2. **The feeding slot heading and the supplement columns** (`feat(ui)`): the heading from the
   chosen feeding, and the measured row shared by the feeding and its supplements.
3. **Adding a supplement** (`feat(ui)`): only the supplements that are on, each with a remove
   button, and the button with the menu of the others.
4. **The select list opens on its value** (`fix(client)`, #1760): reproduce the problem in the
   running client, settle the cause, then fix it in `Components/SimpleSelect.fs`, so that every
   select that uses it opens with the selected or placeholder value in view.
5. **The scenario card actions** (`feat(ui)`, #1192): the action bar in the scenario card of the
   prescribe view, as above.

## Verification

- Step 1: the tests of `PatientTextTests.fs` and the new ones: an estimate with and without a range,
  a measured value with and without a range, for the weight and the height.
- Steps 2 and 3: the scenario of the issue on the nutrition page in the running app, with a wide and
  a narrow window: the supplement fields under the feeding fields, a supplement turned on from the
  menu and off with its remove button, the last supplement that is off added from a menu with one
  name, and the slot heading before and after the feeding is chosen.
- Step 4: in the running client, on a desktop window and a phone-sized one, the weight and height
  lists open on a chosen value and on an estimated placeholder value, at the start, the middle and
  the end of the list, and a short list behaves as before.
- Step 5: in the running client, the scenario card shows Bewerken outlined on the left and
  Voorschrijven contained on the right, both work as before, Voorschrijven is disabled while the
  order is in the plan or the plan is busy, and the label follows the language setting.
