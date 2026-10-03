# Implementation plan for issue #504: TPN intake percentage

Part of G7, nutrition and TPN, of [the grouping index](ux-issue-grouping.md#g7--nutrition-and-tpn-988).
Refs #504, #988; closes none.

- [Problem description](#problem-description)
- [Decisions](#decisions)
- [Approaches considered](#approaches-considered)
- [Chosen approach](#chosen-approach)
- [Configuring a TPN order](#configuring-a-tpn-order)
- [Open questions](#open-questions)
- [Confidence](#confidence)
- [Steps](#steps)
- [Verification](#verification)

## Problem description

The TPN slot shows a slider, "toedien hoeveelheid als percentage van totaal", in the total volume
row (`Views/ParenteralNutrition.fs`). It only holds local state: moving it changes nothing in the
order.

The slider should set the dose quantity, the amount given, as a share of the orderable quantity,
the amount the user composed. The composition must stay as the user set it. The rate and the time
then follow from the dose quantity.

The domain already has a command for this, `SetOrderableDoseQuantityPerc of perc: int`
(`GenORDER.Lib/Types.fs`), handled in `OrderProcessor.processChangeProperty` with
`Dose.setPercValue`. Nothing produces it, and on a TPN order it does not work:

- on a solved order the dose quantity is one value, so a percentage has nothing to pick from;
- the dose quantity range comes from the first calculation, before the user composed the
  orderable, so a percentage can pick a dose larger than the orderable;
- per-kg dose minimums on the orderable and on the components forbid any dose below the whole
  orderable;
- the rate steps the calculation coarsened (5 mL/uur) leave most dose quantities without a rate.

The script `src/Informedica.GenORDER.Lib/Scripts/DoseQuantityPerc.fsx` shows each of these and
prototypes the step that solves them.

## Decisions

Taken 2026-10-03 by the maintainer.

| Question | Decision |
|---|---|
| Meaning of the percentage | A point in the range of the dose quantity, from its defined minimum up to the orderable quantity: in effect the share of the composed orderable that is given. |
| Composition | The orderable and component orderable quantities keep the values the user set. |
| Where the per-kg limits of a component live | On the component orderable quantity, through a solution rule, not on the component dose. |
| Dose count | Initially one, defined in the solution rule, so the dose is the whole orderable while the user composes it. The step then sets it to at least one, so a partial dose is the orderable divided into more than one dose. |
| Rate and time | Stay ranges after the move; the user picks one of them in the administration row. When the time was solved before the move, because the user set the rate or the time, that time becomes the maximum of the time and the rate is recalculated within it. |
| Command | Reuse `SetOrderableDoseQuantityPerc`; its processor case gets a new step. |
| Code | The domain step is prototyped in a script and migrated by the maintainer. |

## Approaches considered

1. **The existing case as it is** (`setDose (Dose.setPercValue ...)`). Fails on a solved order
   and on low percentages, see the problem description.
2. **The whole order back to its calculated constraints, then the percentage.** Works once, but
   resets the composition and keeps the coarse rate steps.
3. **Clear the doses, solve, then the percentage.** Works, but needs a solve inside the step, so
   the step needs the logger and returns a `Result`, unlike the other steps.
4. **Clear the doses and give the dose quantity the orderable quantity as maximum, then the
   percentage.** Works without a solve inside the step. Chosen.

## Chosen approach

### Domain (`GenORDER.Lib`)

Four helpers, each following the pattern of its neighbours in `OrderVariable.fs`:

- `Count.setMinToOneWithConstraints`: a minimum of one and no maximum, in the values and in the
  defined and calculated constraints, so the one count the solution rule defines does not come back.
- `Quantity.applyOnlyMinIncrConstraintsUpTo upTo qty`: the defined minimum and increment of
  `qty`, with the value of `upTo` as inclusive maximum when `upTo` holds one value.
- `Rate.applyOnlyMinIncrConstraints`: the typed wrapper of `applyOnlyMinIncrConstraints`, as
  `Quantity` already has.
- `Time.applyConstraintsUpToSolved`: the constraints of the time, as `Time.applyConstraints`; when
  the time held one value, that value as inclusive maximum. A time the user settled through the
  rate or the time is then not exceeded by the new dose, and the rate follows from it.

A step in `OrderProcessor.fs`, shaped like `orderPropertyIncrOrDecrOrderableDoseQuantity`:

```fsharp
let orderPropertySetPercOrderableDoseQuantity step ord =
    let upToOrderable (dos: Dose) =
        let qty =
            dos.Quantity
            |> Quantity.applyOnlyMinIncrConstraintsUpTo ord.Orderable.OrderableQuantity

        { dos with Quantity = qty }

    ord
    // clear the doses, the dose quantity up to the orderable, the composition stays
    |> OrderPropertyChange.proc
        [
            if ord.Schedule |> Schedule.hasTime then
                ScheduleTime Time.applyConstraintsUpToSolved

            OrderableDoseCount Count.setMinToOneWithConstraints

            OrderableDose Dose.setToNonZeroPositive
            OrderableDose upToOrderable
            ComponentDose("", Dose.setToNonZeroPositive)
            ItemDose("", "", Dose.setToNonZeroPositive)

            OrderableDose(fun dos -> { dos with Rate = dos.Rate |> Rate.applyOnlyMinIncrConstraints })
        ]
    // set the percentage
    |> OrderPropertyChange.proc [ OrderableDose step ]
```

The processor case becomes:

```fsharp
| SetOrderableDoseQuantityPerc n ->
    ord |> orderPropertySetPercOrderableDoseQuantity (Dose.setPercValue n ord.Schedule false)
```

The change-property pipeline then solves the order, as for every property change.

**Guard.** Without one value for the orderable quantity the step does not fail, but the percentage
does nothing and the order is widened. The processor case only applies the step when the orderable
quantity holds one value; otherwise it returns the order unchanged.

### Contract and server

One case each, following `SetMedianOrderableDoseQuantityProperty`:

- `GenPRES.Shared/Api.fs`: `OrderContextCommand.SetOrderableDoseQuantityPercProperty of perc: int`,
  and its `toString`.
- `GenORDER.Lib/OrderContext.fs`: `Command.SetOrderableDoseQuantityPercProperty of OrderContext * perc: int`,
  `Command.get`, `Command.toString`, and in `evaluateOutcome`
  `processPropertyCmd ctx (SetOrderableDoseQuantityPerc perc) (...)`.
- `GenPRES.Server/ServerApi.Mappers.OrderContext.fs`: `Command.toDomain`.
- `tests/Informedica.GenPRES.Server.Tests/MappersOrderContextTests.fs`: the pair in `verbs`, and the
  count of wire cases from 25 to 26.

`Client.Core` needs no change: `ArgumentationPolicy` and `OrderPlanMachine.Dialog` match the
command with wildcards.

### Client

`Views/NutritionSlot.fs`, extending the dose quantity pattern:

- `Msg`: `SetDoseQuantityPercProperty of perc: int`;
- the `update` stepper record: `setDoseQtyPerc`, built in `useSlot` with
  `create (navRate (Api.OrderContextCommand.SetOrderableDoseQuantityPercProperty perc))`;
- `update`: `handleNav (stepper.setDoseQtyPerc perc)`;
- the trail helpers `kindOf`, `fieldOf` (the `"ordDoseQty"` group) and `describeMsg`;
- `Slot`: `SetDoseQuantityPerc: int -> unit`; `CanSetDoseQuantityPerc: bool`, true when the
  orderable quantity holds one value; and `DoseQuantityPerc`, the exact share of the orderable
  quantity the dose quantity is, when both hold one value, and whether that share is a slider step.

`Views/ParenteralNutrition.fs`: the slider takes its position from `DoseQuantityPerc`; local state
only holds the position while the user drags. It sends the command on `onChangeCommitted`, once
per release, and is disabled while the slot is loading or `CanSetDoseQuantityPerc` is false.

### Which control sets the dose

Either the slider or the dose quantity field sets the dose, not both at the same time.

1. **Read from the order.** The slider is in control when the dose quantity is the value the
   percentage step gives for one of the slider's steps, 10 to 100; otherwise the dose quantity
   field is. Nothing new is stored. A dose typed in the field that equals a slider step reads as a
   slider setting, which shows the same intake.
2. **The slider not in control** shows no handle and the exact share as text, for example
   "14% van totaal", so it never rounds a typed dose to a step. Moving it takes control back and
   replaces the typed dose.
3. **A composition change** after a slider move: the component quantity step
   (`orderPropertyIncrOrDecrComponentOrderableQuantity`) sets the dose count back to one, so the
   dose becomes the whole new orderable, and the slider reads 100%. The rate is kept and the time
   follows. Whether the slider's percentage should be sent again instead, keeping for example 50%
   of the new total, is an open question.
4. **Rate and time** after a slider move: picking the rate leaves the dose quantity as it is, so
   the slider stays in control; when a pick changes the dose quantity, the dose quantity field
   takes control, and the slider shows the exact share as in 2.

## Configuring a TPN order

The step works on a TPN order configured as below. The test fixture of the script is
`Scenarios.tpnComplete` converted to this configuration.

### DoseRules sheet

One rule for the TPN generic, with:

| Column | Setting |
|---|---|
| `DoseType` | `timed` |
| `Freqs`, `FreqUnit` | `1`, `dag` |
| `MinTime`, `MaxTime`, `TimeUnit` | the infusion time range, for example `20`, `24`, `uur` |
| `AdjustUnit` | `kg` |
| `DoseUnit` | `mL` |
| `MinQtyAdj` (orderable and components) | empty: a minimum per kg on a dose forbids every partial dose |
| `MaxQtyAdj` (orderable) | optional, as a ceiling |
| Component dose limits | none: the component limits go to the solution rules |
| Substance dose limits | no minimum per administration, for the same reason |

### SolutionRules sheet

For the same generic, form, route and dose type, one row per component and one per substance
limit:

| Column | Component row | Substance row |
|---|---|---|
| `Component` | the component name, for example `NaCl 3%` | empty |
| `Substance` | empty | the substance name, for example `natrium` |
| `Unit` | `mL` | the substance unit, for example `mmol` |
| `MinQtyAdj`, `MaxQtyAdj` | the range per kg, for example `10` and `25`; equal for a fixed amount, for example `6` and `6` | optional |
| `MinConc`, `MaxConc` | empty | the concentration limits, for example max `0.5` |
| `Div` | `1`, so the orderable steps by 1 mL | |
| `MinPerc`, `MaxPerc` | `1` and `1`, so the dose count is initially one | |

`Medication.addSolution` turns `MinPerc`/`MaxPerc` into the dose count as they are, without dividing
by 100 and with minimum and maximum swapped, so both columns hold `1` for a dose count of one.

Every component needs a component row, the filler too (for example `gluc 10%` at `65` to `80`
mL/kg): a component without one has no upper bound, the orderable quantity then never becomes one
value, and the step has nothing to work with.

`PrescriptionRule.filter` multiplies `MinQtyAdj`/`MaxQtyAdj` by the patient's weight;
`Medication.addSolution` attaches the limit to the component with the matching name, and the order
builder sets it as the minimum and maximum of the component orderable quantity. So the patient's
weight is required.

### Set by the order builder

- The infusion rate steps by 0.1 in the rate unit (`Medication.fs`, the `rate` function of the
  orderable dose).
- A timed orderable is stepped by the coarsest step of its products, from `Div`.

## Open questions

1. **`MinPerc`/`MaxPerc`.** The sheet documents these columns as "the percentage of the solution
   that makes up one dose quantity", but `Medication.addSolution` uses the values as a dose count,
   unscaled and with minimum and maximum swapped (`Min = DosePerc.Max`, `Max = DosePerc.Min`). For a
   dose count of one both columns hold `1`, not `100`. Whether the column name, its documentation or
   the conversion should change is not yet settled.
2. **Composition change after a slider move.** Keep the reset to the whole orderable that the
   component quantity step gives today, or send the slider's percentage again so the share stays.
3. **The live sheets.** Whether the live TPN rules already put the component limits in the solution
   rules, and leave the dose minimums empty, is not yet checked.

## Confidence

High for the domain step: the script runs it on the fixture, 39 tests pass, and every percentage
from 10 to 100 keeps the order within its constraints. The time maximum after a set rate or time is
not yet in the script; step 1 adds it there with its tests before the migration. Medium for the configuration until the open
questions are settled.

## Steps

1. **Domain** (maintainer migrates from the script): the four helpers, the step, the processor
   case with its guard, and Expecto tests from the script in `tests/Informedica.GenORDER.Tests`.
   The script first gets `Time.applyConstraintsUpToSolved`, with tests for a move after the user set
   the rate and after the user set the time.
2. **Contract and server**: the wire case, the domain command, the mapper, and the mapper test.
3. **Client**: the message, the stepper, the trail helpers, the slot fields and the slider.
4. **Rules**: the TPN dose and solution rules on the sheets, as configured above.

Steps 1 and 2 can be one pull request; step 3 follows; step 4 is a sheet change.

## Verification

- `dotnet fsi src/Informedica.GenORDER.Lib/Scripts/DoseQuantityPerc.fsx`: all tests pass, and the
  fixture prints as configured above.
- `dotnet run servertests`: the new GenORDER tests and the mapper test pass.
- `dotnet fsi scripts/CheckDependencyRule.fsx` passes.
- `dotnet run`, Nutrition page, add TPN, set every component, move the slider: the dose quantity,
  the rate range and the time range change after release; the component quantities do not; at 100%
  the dose quantity equals the total volume; before every component is set, the slider is disabled.
- Change the dose quantity with its own field, for example to 14%: the slider shows no handle and
  "14% van totaal". Move the slider: it takes control again.
- Press reset, or change a component quantity after a slider move: the slider reads 100%.
- Set the rate, then move the slider: the time stays at or below the time the rate gave, and the
  rate is recalculated for the new dose.
