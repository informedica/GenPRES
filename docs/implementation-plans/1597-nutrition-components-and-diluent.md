# Implementation plan for issue #1597

A nutrition slot shows every component of the composition. The prescriber switches the additives
on and off and chooses the diluent. Drafted 2026-11-10. Line numbers are from `master` at
`a9937315`. The production and demo Formulary, DoseRules, DilutionRules and RuleSets sheets were
fetched on 2026-11-10.

- [Problem description](#problem-description)
- [What the code does today](#what-the-code-does-today)
- [What the sheets hold](#what-the-sheets-hold)
- [Decisions](#decisions)
- [Open points](#open-points)
- [Prototype](#prototype)
- [Steps](#steps)

## Problem description

The nutrition slot shows placeholders for the component choices
(`src/Informedica.GenPRES.Client/Views/NutritionSlot.fs:855-875`): the checkbox and the select
are disabled, a component counts as included when its quantity is above zero, and the last
component is taken to be the diluent. #1597 asks for a slot where:

- the main component is always there and cannot be left out;
- each additive has a checkbox, and an unchecked additive stays visible so that it can be checked
  again;
- the diluent is chosen with a select.

The Formulary columns `IsAdditive` and `IsDiluent` say which products can play these roles.

## What the code does today

### The Formulary flags

- `FormularyProduct.CanBeUsedAsDiluent` and `CanBeUsedAsAdditive` are parsed from the columns
  `IsDiluent` and `IsAdditive` (`src/Informedica.GenFORM.Lib/Product.fs:113-114`).
- Nothing reads them. `ProductComponent` has no field for them (`Types.fs:155-193`).

### Optional components

- The component options of an order are the component names of the dose rule without the first
  (`src/Informedica.GenORDER.Lib/OrderContext.fs:189`, set once in
  `updateFilterIfOneScenario`, `:1333-1337`).
- `PrescriptionRule` keeps the first component and the selected others; an empty selection keeps
  all (`src/Informedica.GenFORM.Lib/PrescriptionRule.fs:332-341`). The order cannot hold the main
  component alone.
- `SetNthComponentsProperty` selects them (`src/Informedica.GenPRES.Shared/Api.fs:52`), and the
  Prescribe view shows them as a multi-select (`Prescribe.fs:584-598`).

### The diluent

- The diluent options are the names in the DilutionRules column `Diluents` that match a virtual
  parenteral product (`src/Informedica.GenFORM.Lib/DilutionRule.fs:123-128`). They come in the
  order of the Formulary products, not of the cell.
- A chosen diluent narrows the options to itself (`DilutionRule.fs:513-516`).
- `Medication.addDilutionRule` adds the first diluent left as the last component of the order,
  without limits of its own (`src/Informedica.GenORDER.Lib/Medication.fs:994-1008`).
- A DilutionRules row with a `Component` gives a component limit, matched to a component of the
  order by name, the diluent included (`Medication.fs:1015-1035`). Its quantity bounds the
  orderable quantity of that component (`Medication.fs:1419-1425`).
- The rows of one dilution rule share the `Diluents` cell: the cell is part of the key the rows
  are grouped by (`DilutionRule.fs:93-145`).

### Each choice rebuilds the order

Both a component selection and a diluent choice are evaluated as `UpdateOrderContext`
(`OrderContext.fs:1522-1523`), which makes the order from the rules again
(`OrderContext.fs:1769-1772`). The quantities the prescriber chose are lost.

### A diluent that is also a dose rule component

The order then holds two components with the same name. The solver takes them as one variable
counted twice in the orderable quantity, the solve fails with `VariableCannotSetValueRange`, and
`getScenarios` answers the context with no scenario and no refusal. No load check reports it
(`src/Informedica.GenFORM.Lib/Resources.fs:536-548`).

## What the sheets hold

Production:

- The flags are on the virtual rows only. The maintainer removed them from the G-Standaard rows on
  2026-11-10, so that they cannot clash with the flags of a virtual product: none of the 6470
  G-Standaard rows has a flag, also not the 38 whose AltName is the name of a virtual product.
- `IsDiluent` is set on the virtual rows `NaCl 0,9%` and `gluc 5%` to `gluc 50%`.
- `IsAdditive` is set on the virtual rows `CaCl2 3,3%`, `CaGluc 10%`, `KCl 7,4%`, `Kidtrayze`,
  `MgCl2 10%`, `MgSulfaat`, `NaBic 4,2%`, `NaBic 8,4%`, `NaCl 0,45%`, `NaCl 3%`, `Peditrace`,
  `Primene`, `Soluvit`, `Vitintra Infant` and `water`.
- `Intralipid 20%`, `SMOF 20%` and `Kacetaat` are not marked `IsAdditive`. Before the edit only
  their G-Standaard rows were.
- Samenstelling B to E: each dose rule has one glucose component, with a `MaxQtyAdj`. Their
  DilutionRules rows have an empty `Diluents` and a per-kg row per component, glucose included.
- NICU mix, Primene and electrolyt/glucose mengsel have diluents in DilutionRules. NICU mix dose
  rules have `gluc 10%` as a component while the dilution rule "neonaten" of NICU mix, which can
  meet the same patients, lists `gluc 10%` as a diluent.
- The Diluents name `glycine buffer` of epoprostenol is not marked `IsDiluent`.

Demo: no `IsAdditive`, `IsDiluent` only on `gluc 10%` among the nutrition components, and no
DilutionRules rows for a nutrition generic.

## Decisions

| Topic | Decision |
|---|---|
| Optional components | The `IsAdditive` flag decides which components get a checkbox. |
| Main component | The first component in sheet order. It never gets a checkbox, whatever its flag. |
| Diluent options | The DilutionRules `Diluents` column; each name must be marked `IsDiluent`, with a load warning when it is not. |
| Diluent limits | The DilutionRules row with `diluent` in the `Component` column limits the diluent, whichever is chosen. |
| Default diluent | The first name in the `Diluents` cell. On the production sheet this moves the default of 42 dilution rules, whose cell starts with `NaCl 0,9%`, from `gluc 5%` to `NaCl 0,9%`; the maintainer confirmed `NaCl 0,9%` is intended. |
| The `Diluents` cell | The names are separated by `\|`, as the code reads them, and the cell is on every row of the dilution rule, since the rows are grouped by it. Both are sheet rules; the code does not change for them. |
| Samenstelling B to E | Glucose becomes the diluent and leaves the dose rule components. The maintainer edits the sheet. |
| A diluent that is also a dose rule component | The pair is refused: the dilution rule is not applied to that dose rule, and loading warns. Both rules load. |
| Unchecking every additive | In scope. The representation of "none selected" is the maintainer's to decide. |
| Keeping the chosen quantities after a toggle or a diluent choice | In scope. The approach is the maintainer's to decide. |

## Open points

For the maintainer to decide:

1. **How the flags reach the code that selects components.** `ProductComponent` carries no flags,
   and `PrescriptionRule.filter` and `OrderContext` do not see the Formulary. The prototype looks
   the flag up by the GPK of the component's products, through a function passed in.
2. **Which Formulary row gives a flag.** The flag of the product's own row, by GPK (the
   prototype), or also of a G-Standaard row whose AltName is the component name. With the flags
   on the virtual rows only, both give the same flags. Whether `Intralipid 20%`, `SMOF 20%` and
   `Kacetaat` are additives is a sheet question.
3. **"None selected".** See the decisions.
4. **Keeping the chosen quantities.** See the decisions.
5. **The glucose maximum of Samenstelling B to E.** Without the glucose row in DoseRules, only the
   DilutionRules range limits the glucose. In 7 of the 18 orders of the prototype the maximum is
   then higher, up to the range maximum, for example Samenstelling C, 12 kg, day 3: 432-480 mL
   today and 432-528 mL. The values need a clinical check.
6. **The NICU mix pairs and `glycine buffer`.** With the refusal, the NICU mix dose rules with
   `gluc 10%` as a component do not get the dilution rule "neonaten".

## Prototype

`src/Informedica.GenORDER.Lib/Scripts/NutritionComponents.fsx` runs on the production sheet, with
the sheet edits of the decisions made in memory. Step 1 is migrated, and the order tests of the
script run on the library code. Its 15 tests pass. What the script still holds:

- **From the review of step 1**: `diluentRowsWithoutDiluent`, a load warning for a `diluent` row
  in a rule without a diluent, and `unmarkedDiluents`, one `IsDiluent` warning per diluent that
  lists the generics.
- **Step 2**: `optionalComponents` and `selectComponents`: the additives after the first
  component are optional; the first component and every component that is not an additive stay.

What the tests show:

- With `diluent` in the `Component` column, every Samenstelling B to E order equals the order
  with the glucose row named after the glucose.
- With `gluc 30%|gluc 20%` for Samenstelling C day 3, the options are in that order, gluc 30% is
  the default, and a chosen gluc 20% gets the limit of the diluent row (432-528 mL at 12 kg).
- The optional components of Samenstelling B to E are `NaCl 3%`, `KCl 7,4%` and `Peditrace`.
  `gluc 10%` stays fixed in Nutriflex 70/240. In the Intralipid 20% rule for 2-7 kg, day 3 and
  later, `Vitintra Infant` is the first component, so it is the main one.

`dotnet fsi NutritionComponents.fsx --test-sheet` runs the rows of the sheet `DilutionRules_Test`
(Samenstelling C day 3, a `diluent` row with `gluc 20%;gluc 30%`) in place of the production rows:

- As written, the cell is one name that matches no product, so the rule has no diluent; without
  its dose rule row the order holds no glucose.
- With the cell split on `;` but only on the `diluent` row, that row is a dilution rule of its
  own, and the order has two scenarios: one without glucose, one whose other components have lost
  their dilution limits.
- With the cell split and on every row, and the glucose out of the dose rules, the order is as
  intended: one scenario, the options gluc 20% then gluc 30%, gluc 20% the default, and either
  glucose at 396-484 mL at 8 kg and 432-528 mL at 12 kg.

`dotnet fsi NutritionComponents.fsx --test-sheets` runs the rows of `DoseRules_Test` (Samenstelling
C day 1 to 3, the glucose rows deleted) with the rows of `DilutionRules_Test` (day 3, the cell
`gluc 20%|gluc 30%` on every row). Day 3 is as intended at 8 kg and 12 kg, with the values above
and no refused pair. Day 1 and day 2 give an order without glucose and without a message: their
glucose rows are gone from the dose rules, while their dilution rules, still the production rows,
have no diluent. Every day of a composition needs its dose rules and its dilution rules changed
together.

The refused-pair check only pairs a dose rule and a dilution rule that can meet the same patient
(`PatientCategory.canShare`), as `competingRules` does.

## Steps

One pull request at a time, each merged before the next. The order and the content of steps 2
to 4 depend on the open points.

1. **GenFORM: the diluent of a dilution rule** (`feat(genform)`): `DilutionRule.map` with the
   cell order and the `diluent` row, the `IsDiluent` warning, the refused pair in
   `PrescriptionRule.filter` and its load warning. The tests move from the script.
   - In the source, `DilutionRule.withDiluentRows` gives a `diluent` row once per diluent, named
     after the diluent, before the limits are built, so the existing code of a component row builds
     its limits.
   - The script stays until step 2 is migrated, because it also holds the prototype of step 2.
   - On deploy, without any sheet edit: the default diluent of 42 dilution rules moves from
     `gluc 5%` to `NaCl 0,9%`, the dilution rule "neonaten" of NICU mix is refused for the NICU mix
     dose rules with `gluc 10%`, and loading warns that `glycine buffer` is not marked
     `IsDiluent`.
   - Step 1 lands before the production sheet gets `diluent` rows: the current code reads such a
     row as a component named `diluent`, which matches no component, so the diluent would have no
     limit of its own.
2. **The optional components**: the additives after the first component, in `PrescriptionRule`
   and `OrderContext`, once open points 1 and 2 are settled.
3. **"None selected"**, once open point 3 is settled.
4. **Keeping the chosen quantities**, once open point 4 is settled.
5. **Client: the nutrition slot**: the rows from the main component, the optional components and
   the diluent; the checkboxes and the select through the existing commands; disabled while the
   composition is locked (`IntakePolicy.isCompositionLocked`) or the slot is busy.
6. **Sheets** (the maintainer): Samenstelling B to E as decided, after open point 5, with the
   `Diluents` cell separated by `|` on every row; the NICU mix rules and `glycine buffer`; the
   demo sheet.
