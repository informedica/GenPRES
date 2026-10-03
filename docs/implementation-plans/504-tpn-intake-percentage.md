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

A script prototyped the step that solves these; the step is now in the source, with its tests in
`tests/Informedica.GenORDER.Tests/DoseQuantityPercTests.fs`.

## Decisions

Taken 2026-10-03 by the maintainer.

| Question | Decision |
|---|---|
| Meaning of the percentage | A point in the range of the dose quantity, from its defined minimum up to the orderable quantity: in effect the share of the composed orderable that is given. |
| Composition | The orderable and component orderable quantities keep the values the user set. |
| Where the per-kg limits of a component live | On the component orderable quantity, through a solution rule, not on the component dose. |
| Dose count | Defined as at least one in the solution rule, with no maximum. The solution limits on the component orderable quantities keep the composition safe, so the count need not be one while the user composes. The dose quantity stays a range until the slider or a dose pick sets it; a partial dose is the orderable divided into more than one dose. |
| Rate and time | Stay ranges after the move; the user picks one of them in the administration row. When the time was solved before the move, because the user picked the rate, that time becomes the maximum of the time and the rate is recalculated within it. No command sets the time itself; the user settles it through the rate. |
| Composition during a partial dose | Locked in the TPN view until the dose is back at 100%. At 100% the existing component steps change the composition correctly; below 100% a step breaks the time maximum. The processor's component picks stay as they are. |
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

A step changes the Variable of an order variable only: its defined constraints are never changed
once written, and its calculated constraints are only set by calculation from the defined
constraints.

Three generic functions on an `OrderVariable`, beside `applyConstraints`:

- `applyDefinedConstraints`: the Variable set to the defined constraints.
- `setMaxToValueOf upTo`: the maximum of the Variable lowered to the value of `upTo`, when `upTo`
  holds one value and that is lower.
- `applyConstraintsUpToSolved`: `applyConstraints`, then the value as inclusive maximum when the
  variable held one value.

Helpers in the typed modules, each only composing a generic function, as their neighbours do:

- `Quantity.applyDefinedConstraints`, `QuantityAdjust.applyDefinedConstraints` and
  `Rate.applyDefinedConstraints`.
- `Quantity.setMaxToValueOf upTo`: a dose up to the whole orderable.
- `Time.applyConstraintsUpToSolved`: a time the user settled through the rate is not exceeded by
  the new dose, and the rate follows from it.

The dose count uses the existing `Count.setMinToOne`, which sets the Variable only.

A step in `OrderProcessor.fs`, shaped like `orderPropertyIncrOrDecrOrderableDoseQuantity`:

```fsharp
let orderPropertySetPercOrderableDoseQuantity step ord =
    // the dose as the rules define it, up to the whole orderable
    let definedUpToOrderable (dos: Dose) =
        { dos with
            Quantity =
                dos.Quantity
                |> Quantity.applyDefinedConstraints
                |> Quantity.setMaxToValueOf ord.Orderable.OrderableQuantity
            QuantityAdjust = dos.QuantityAdjust |> OrderVariable.QuantityAdjust.applyDefinedConstraints
            Rate = dos.Rate |> Rate.applyDefinedConstraints
        }

    ord
    // clear the doses, the orderable dose as defined up to the orderable, the composition stays
    |> OrderPropertyChange.proc
        [
            if ord.Schedule |> Schedule.hasTime then
                ScheduleTime Time.applyConstraintsUpToSolved

            OrderableDoseCount OrderVariable.Count.setMinToOne

            OrderableDose Dose.setToNonZeroPositive
            OrderableDose definedUpToOrderable
            ComponentDose("", Dose.setToNonZeroPositive)
            ItemDose("", "", Dose.setToNonZeroPositive)
        ]
    // set the percentage
    |> OrderPropertyChange.proc [ OrderableDose step ]
```

The processor case becomes, with the guard below:

```fsharp
| SetOrderableDoseQuantityPerc n ->
    match ord |> isComposed with
    | true -> ord |> orderPropertySetPercOrderableDoseQuantity (Dose.setPercValue n ord.Schedule false)
    | false -> ord
```

The change-property pipeline then solves the order, as for every property change.

**Guard.** The step keeps the component orderable quantities as they are, so it needs a composition
the user has set. `isComposed` checks that every component orderable quantity holds one value; the
orderable quantity, their sum, then holds one value too. The other way round does not hold: a total
of one value can still leave the components open, and the composition would shift under the
percentage. The processor case only applies the step on a composed order; otherwise it returns the
order unchanged.

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
  order is composed, every component orderable quantity holding one value; and `DoseQuantityPerc`, the exact share of the orderable
  quantity the dose quantity is, when both hold one value, and whether that share is a slider step;
- the component rows: locked while the dose quantity is less than the orderable quantity, see
  "Which control sets the dose".

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
3. **The composition is locked while the dose is partial.** While the dose quantity is less than
   the orderable quantity, whether the slider or the dose quantity field set it, the component
   rows are disabled: the quantity field, its step buttons, its minimum, median and maximum picks,
   and the arrow that reopens it. A hint says why: "zet de toedien hoeveelheid eerst op 100% om de
   samenstelling te wijzigen". The user moves the slider back to 100% to change the composition.
   What the order does when the composition changes, tested on the composed TPN fixture:
   - A reopen in the TPN view goes without picks, so it solves the cleared order as it is: the other
     components and the orderable quantity give the component its old value back, and a minimum,
     median or maximum pick then changes nothing.
   - The composition changes through the increase and decrease of a component, which set the dose
     count to one and keep the rate. At 100% that gives one dose of the new total, within the
     constraints (`gluc 10%` one step up: 1080 mL, dose count 1).
   - Below 100% the kept rate belongs to the partial dose, so the whole new total takes too long:
     after a move to 50%, one step gives a time of 40 to 48 h against a maximum of 24 h. The lock
     prevents this.
4. **Rate and time** after a slider move: picking the rate leaves the dose quantity as it is, so
   the slider stays in control; when a pick changes the dose quantity, the dose quantity field
   takes control, and the slider shows the exact share as in 2.

## Configuring a TPN order

The step works on a TPN order configured as below. The fixture of `DoseQuantityPercTests.fs` is
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
| `Div` | empty, see below | |
| `MinPerc`, `MaxPerc` | empty and `1`, so the dose count is at least one | |

`Div` stays empty. A solution rule's `Div` becomes the medication's `Div`, and with it set the
orderable dose quantity gets no increment (`Medication.divisibility` without a component), so the
percentage has no steps to pick from and the slider does nothing. With `Div` empty, the orderable
and its dose quantity step by the coarsest `Divisible` of the products, 1 mL for the TPN products.

`Medication.addSolution` turns `MinPerc`/`MaxPerc` into the dose count as they are, without dividing
by 100 and with minimum and maximum swapped, so `MaxPerc` holds `1` for a dose count of at least one
and `MinPerc` stays empty. A dose count defined as exactly one forbids every partial dose.

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
- A timed orderable and its dose quantity are stepped by the coarsest step of its products, from
  their `Divisible`, when the rule has no `Div`.

## Open questions

1. **`MinPerc`/`MaxPerc`.** The sheet documents these columns as "the percentage of the solution
   that makes up one dose quantity", but `Medication.addSolution` uses the values as a dose count,
   unscaled and with minimum and maximum swapped (`Min = DosePerc.Max`, `Max = DosePerc.Min`). For a
   dose count of at least one `MaxPerc` holds `1`, not `100`. Whether the column name, its documentation or
   the conversion should change is not yet settled.
2. **Component picks in the processor** (settled: no change). The increase and decrease of a
   component quantity go through `orderPropertyIncrOrDecrComponentOrderableQuantity`, which sets the
   dose count back to one; the minimum, median and maximum picks (`setCmpOrbQty`) do not. Routing
   the picks through that step was tested on every scenario with more than one component (`amfo`,
   `morfCont`, `tpn`, `tpnComplete`): it puts one to four variables outside their constraints,
   because that step lets values go outside them on purpose. It is not needed for TPN either: on a
   composed TPN a pick cannot change the composition, and the steps already reset the dose count.
3. **The live sheets.** Whether the live TPN rules already put the component limits in the solution
   rules, and leave the dose minimums empty, is not yet checked.

## Confidence

High for the domain step: 82 tests run it through the processor pipeline on the fixture, with
the dose count defined as at least one, and every percentage from 10 to 100 keeps the order within
its constraints, also after the user picked the rate and with a dose and a rate maximum. Medium for the configuration until the open questions are settled.

## Steps

1. **Domain** (done): the three generic functions and their typed helpers, `isComposed`, the step
   and the processor case with its guard, with the tests in `DoseQuantityPercTests.fs`: moves on a
   composed order, after 100%, after the user picked the rate, with a dose and a rate maximum, and
   before every component is set.
2. **Contract and server** (done): the wire case, the domain command, the mapper, and the mapper test.
3. **Client**: the message, the stepper, the trail helpers, the slot fields and the slider.
4. **Rules**: the TPN dose and solution rules on the sheets, as configured above.

Steps 1 and 2 can be one pull request; step 3 follows; step 4 is a sheet change.

## Verification

- `dotnet test tests/Informedica.GenORDER.Tests/ --filter "FullyQualifiedName~DoseQuantityPerc"`:
  all tests pass.
- `dotnet run servertests`: the GenORDER tests and the mapper test pass.
- `dotnet fsi scripts/CheckDependencyRule.fsx` passes.
- `dotnet run`, Nutrition page, add TPN, set every component, move the slider: the dose quantity,
  the rate range and the time range change after release; the component quantities do not; at 100%
  the dose quantity equals the total volume; before every component is set, the slider is disabled.
- Change the dose quantity with its own field, for example to 14%: the slider shows no handle and
  "14% van totaal". Move the slider: it takes control again.
- Move the slider below 100%, or type a partial dose: the component rows are disabled, the reopen
  arrows too, and the hint shows. Move the slider back to 100%: they are enabled again.
- Press reset: the slider reads 100%.
- Set the rate, then move the slider: the time stays at or below the time the rate gave, and the
  rate is recalculated for the new dose.
