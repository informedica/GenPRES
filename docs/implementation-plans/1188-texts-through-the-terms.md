# Implementation plan for issues #1188 and #1117

Two texts that do not follow the localization sheet as they should: the frequency header of the
order plan is lowercase, and the hover texts of the step buttons are Dutch in every language.

- [Problem description](#problem-description)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)
- [As built](#as-built)

## Problem description

- **#1188** The frequency column of the order plan table (`Views/OrderPlan.fs`) takes
  `Terms.``Order Frequency``, the term that also labels the frequency field of the order dialog.
  The dialog's field labels are lowercase, so with the sheet loaded the header reads
  `frequentie` among five capitalized headers. The code fallbacks hide it: `"Frequentie"` in the
  plan, `"frequentie"` in the dialog.
- **#1117** `Components/QuantityField.fs` writes the eight step titles as Dutch literals
  (`navigableTitles`, `stepableTitles`), used as hover text and as the label of the buttons. The
  field's other texts, `pickValue` and `pickMedian`, already come in through `QuantityField.Texts`.

## Approaches considered

For the header (#1188), the three options of the issue:

- **A term of its own for the header.** A header and a field label are different texts; the sheet
  stays the one source of both. Chosen.
- **The table capitalizes every header.** Would hide the same mix in any other table.
- **Capitalize the dialog's field labels.** Changes a dialog that is consistent with itself.

For the step titles (#1117), the issue's proposal has no real alternative: eight terms, carried in
`QuantityField.Texts`, filled by the callers with `getTerm` and today's Dutch as fallback.

## Chosen approach

### The terms

Nine cases added to `Terms` in `src/Informedica.GenPRES.Shared/Localization.fs`, each beside its
kin:

| Term | English | Dutch |
|---|---|---|
| `Order Plan Frequency` | Frequency | Frequentie |
| `Step to minimum` | To minimum | naar minimum |
| `Step lower` | Lower | lager |
| `Step higher` | Higher | hoger |
| `Step to maximum` | To maximum | naar maximum |
| `Step large down` | Large step down | grote stap omlaag |
| `Step down` | Step down | stap omlaag |
| `Step up` | Step up | stap omhoog |
| `Step large up` | Large step up | grote stap omhoog |

Adding cases to the Terms union in Shared is the edit granted before for G1 and G6; it is the only
Shared change here.

### The client

- `Views/OrderPlan.fs`: the frequency header takes `Order Plan Frequency`, fallback `"Frequentie"`.
  The dialog label keeps `Order Frequency`, lowercase like its siblings.
- `Components/QuantityField.fs`: `Texts` gains the two groups of four titles (as two four-tuples
  or eight fields, whichever reads plainer at the three callers); `navigableTitles` and
  `stepableTitles` go, and the buttons read hover text and label from `props.texts`.
- The three callers that build `Texts` (`Views/Order.fs`, `Views/Nutrition.fs`,
  `Views/OrderPlan.fs`) fill the titles with `getTerm`, today's Dutch as fallback. If the three
  builders turn out identical, one shared builder in `Views/ViewHelpers.fs` takes their place.

### The sheet

`data/localization/*.tsv` is not tracked; the nine rows for the Localization sheet, in all six
languages, are the maintainer's to add. The step 1 pull request lists them.

Without the rows every language shows the Dutch fallback, and #1117 is not fixed. So the rows go
into the sheet after step 1 merges and before step 2 does, and step 2 is checked in the browser
with the sheet loaded, not with the fallbacks.

## Confidence

High. The terms follow the pattern of `Pick a value` and `Pick the median` (#1111).

## Steps

1. **The terms.** The nine `Terms` cases in `Shared/Localization.fs`. `dotnet run servertests`
   for the Shared tests.
2. **The client.** The header term, `QuantityField.Texts` with the titles, the three callers.
   Merged only once the nine rows are in the Localization sheet.

One pull request per step: the Shared edit lands on its own, as for G1.

## Verification

- Fable compiles, `npx vite build` passes.
- The nine rows are in the Localization sheet, and a resource reload has picked them up.
- In the browser, with the sheet loaded, in Dutch and in English: the six plan headers start with
  a capital; the dialog's frequency label stays lowercase; the step buttons' hover texts follow
  the chosen language on the order dialog, the plan cells and the nutrition page.

## As built

| Step | Pull request | Note |
|---|---|---|
| 1, the terms | #1199 | As planned. |
| 2, the client | #1200 | The three identical builders became one, `ViewHelpers.quantityFieldTexts`. No `aria-label` was added: the buttons have none today, and the tooltip carries the text. |
