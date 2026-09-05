# Order Finite State Machine (GenORDER)

This document describes the finite state machine (FSM) governing the lifecycle of
an `Order` in `Informedica.GenORDER.Lib`.

The FSM models the **solver lifecycle** of an order, as implemented in
`src/Informedica.GenORDER.Lib/OrderProcessor.fs`. States correspond to the flags
in the `OrderState` record (`OrderProcessor.fs`); transitions correspond to
the commands processed by `processPipeline` (`OrderProcessor.fs`).

> **Note:** The `Schedule` discriminated union
> (`Once | OnceTimed | Continuous | Discontinuous | Timed`, `Types.fs`) is
> **orthogonal** to this FSM. It is a type tag that selects which equation set the
> solver uses (`Order.solve`, `Order.fs`), not a lifecycle state.

## State Diagram

```mermaid
stateDiagram-v2
    [*] --> Created: Order.create / createNew

    Created --> ConstraintsApplied: CalcMinMax\n(apply-constraints)
    ConstraintsApplied --> MinMaxSolved: calc-minmax\nincrease-increments\nset-calculated-constraints
    MinMaxSolved --> NormDoseSet: set-normdose\n(if dose rules)
    MinMaxSolved --> ValuesCalculated: CalcValues\n(calc-qty/rate-values)
    NormDoseSet --> ValuesCalculated: CalcValues

    ValuesCalculated --> Solved: SolveOrder\n(final-solve, pick-cmp-qty)
    Solved --> Solved: ReCalcValues\n(apply-calculated-constraints,\ncalc-qty/rate-values, final-solve)

    Solved --> Cleared: user clears variable\n(isCleared)
    ValuesCalculated --> Cleared: user clears variable

    Cleared --> ValuesCalculated: SolveOrder\n(process-cleared, ensure-values)

    Solved --> PropertyChanged: ChangeProperty
    ValuesCalculated --> PropertyChanged: ChangeProperty
    PropertyChanged --> Solved: solve-minmax

    Solved --> [*]
```

## States

States map to the `OrderState` flags (`OrderProcessor.fs`):

| State | Flag |
|-------|------|
| Created | `IsConstraintsNotApplied = true` |
| ConstraintsApplied | constraints set |
| MinMaxSolved | `CanSetNormDose` ready |
| NormDoseSet | norm dose applied |
| ValuesCalculated | `HasValues = true` |
| Solved | `DoseIsSolved` / `OrderIsSolved = true` |
| Cleared | `IsCleared = true` |

## Transitions

Transitions are the pipeline commands handled by `processPipeline`
(`OrderProcessor.fs`); the step names are the `Name` values of the `Step` records
it runs (`"<command>: <step>"` in the log):

- `CalcMinMax` — apply-constraints → calc-minmax → increase-increments → set-calculated-constraints → (optional) ensure-dose-values → set-normdose
- `IncreaseIncrements` — increase-increment
- `CalcValues` — calc-qty-values → (optional) calc-rate-values
- `SolveOrder` — (optional) process-cleared → (optional) ensure-values → final-solve → pick-cmp-qty
- `ReCalcValues` — apply-calculated-constraints → calc-qty-values → (optional) calc-rate-values → (optional) final-solve
- `ChangeProperty` — change-property → solve-minmax

## Cleared Sub-States

The `Cleared` state is refined by an active pattern in `OrderProcessor.fs`:

```text
FrequencyCleared | RateCleared | TimeCleared
| ConcentrationCleared | DoseQuantityCleared | DosePerTimeCleared | NotCleared
```

Each cleared variable is handled by a dedicated processor that resets dependent
variables before re-solving:

- `processClearedFrequency` (`OrderProcessor.fs`)
- `processClearedDose` (`OrderProcessor.fs`)
- `processClearedRate` (`OrderProcessor.fs`)
- `processClearedOrder` (`OrderProcessor.fs`) — dispatcher with schedule-specific logic

## Source References

- `Order` type — `src/Informedica.GenORDER.Lib/Types.fs`
- `Schedule` DU — `src/Informedica.GenORDER.Lib/Types.fs`
- `OrderState` record — `src/Informedica.GenORDER.Lib/OrderProcessor.fs`
- `PrescriptionKind` DU — `src/Informedica.GenORDER.Lib/OrderProcessor.fs`
- `classify` — `src/Informedica.GenORDER.Lib/OrderProcessor.fs`
- `processPipeline` — `src/Informedica.GenORDER.Lib/OrderProcessor.fs`
- `solve` — `src/Informedica.GenORDER.Lib/Order.fs`
