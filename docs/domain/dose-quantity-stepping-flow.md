# Dose Quantity Stepping Flow

How `Order.Dose.Quantity` is handled by the UI when the user steps the dose
quantity up/down and the order is re-solved. Every step round-trips through the
server and the constraint solver — the client never computes the value locally.

```mermaid
flowchart TD
    subgraph CLIENT["Client (Fable/Elmish)"]
        UI["Stepper +/- button<br/>Views/Prescribe.fs"]
        MSG["dispatch OrderContextMsg<br/>App.fs"]
        CALL["makeServerCall<br/>wraps Api.OrderContextCmd(cmd, ctx)<br/>App.fs"]
        RESP["OrderContextResp(OrderContextResult ctx)<br/>state.OrderContext = Resolved ctx<br/>App.fs"]
        RENDER["Re-render dose select +<br/>enable/disable steppers<br/>Views/Order.fs"]
    end

    subgraph SHARED["Shared API DTO"]
        CMD["Increase/DecreaseOrderableDoseQuantityProperty<br/>(ntimes:int, useCalc:bool)<br/>Shared/Api.fs"]
    end

    subgraph SERVER["Server"]
        SCMD["processCmd: OrderContextCmd<br/>ServerApi.Command.fs"]
        SEVAL["OrderContext.evaluate<br/>map -> GenOrderContext cmd<br/>ServerApi.Services.fs"]
    end

    subgraph GENORDER["GenORDER.Lib"]
        GEVAL["evaluate -> processPropertyCmd<br/>ChangeProperty(o, ...DoseQuantity)<br/>Api.fs"]
        PIPE["OrderProcessor.processPipeline<br/>ChangeProperty case<br/>OrderProcessor.fs"]
        PCHANGE["processChangeProperty<br/>Dose.increase/decreaseQuantity<br/>OrderProcessor.fs"]
        STEP["OrderVariable.step true/false useCalc n<br/>min + N*incr  /  max - N*incr<br/>pickNearestHigherElseLower<br/>OrderVariable.fs"]
        CALCMM["calcMinMaxStep<br/>recompute constraints<br/>OrderProcessor.fs"]
        SOLVE["Order.solve<br/>mapToOrderEquations -> ... -> mapFromOrderEquations<br/>Order.fs"]
    end

    subgraph SOLVER["GenSOLVER.Lib"]
        GS["Solver.solve / solveMinMax<br/>monotonic domain refinement"]
    end

    UI --> MSG --> CALL --> CMD --> SCMD --> SEVAL --> GEVAL --> PIPE --> PCHANGE --> STEP --> CALCMM --> SOLVE --> GS
    GS -->|solved OrderVariable domains| SOLVE
    SOLVE -->|updated Order| SEVAL
    SEVAL -->|mapToShared: OrderScenario DTO| RESP
    RESP --> RENDER
    RENDER -.->|next step| UI

    style STEP fill:#ffe7b3,stroke:#cc8800,color:#1a1a1a
    style SOLVE fill:#cfe8ff,stroke:#005bbb,color:#1a1a1a
    style GS fill:#d6f5d6,stroke:#2e8b2e,color:#1a1a1a
```

## Zoom-in: client-side optimistic stepping

The client does **not** block while the server re-solves. It shows a
*preliminary* stepped value immediately using local delta state, keeps the
previous solved context visible (`Deferred.Recalculating`), and reconciles when
the server response arrives. Rapid clicks accumulate into the delta rather than
firing one blocking round-trip each.

```mermaid
flowchart TD
    CLICK["User clicks +/- stepper<br/>ClickCountingButton.onStep<br/>Components/SimpleSelect.fs"]
    DELTA["bump local smallDelta/largeDelta<br/>(React.useState)<br/>SimpleSelect.fs"]
    PRELIM["Render PRELIMINARY label<br/>stepFn(smallDelta, largeDelta)<br/>key stays = server value<br/>SimpleSelect.fs"]
    DISPATCH["dispatch OrderContextMsg<br/>Increase/DecreaseOrderableDoseQuantityProperty(n, useCalc)<br/>App.fs"]

    REC["OrderContext: Resolved ctx -> Recalculating ctx<br/>(old context kept visible)<br/>App.fs"]
    KEEP["Steppers stay enabled, no spinner<br/>isOptimisticStep = true<br/>Order.fs<br/>old dose values still shown via Deferred.toOption<br/>Order.fs"]

    SERVER(["Server re-solve round-trip<br/>(see main flow above)"])

    DONE["LoadOrderContextResult Finished(Ok)<br/>OrderContext = Resolved newCtx<br/>App.fs"]
    BUMP["revision++<br/>App.fs"]
    RESET["useLayoutEffect resets deltas to 0<br/>keyed on valueKey + revision<br/>SimpleSelect.fs"]
    FINAL["Render SOLVED value from server<br/>preliminary -> confirmed"]

    CLICK --> DELTA --> PRELIM
    DELTA --> DISPATCH --> REC --> KEEP
    PRELIM -. "more clicks accumulate" .-> CLICK
    DISPATCH --> SERVER --> DONE --> BUMP --> RESET --> FINAL

    style PRELIM fill:#ffe7b3,stroke:#cc8800,color:#1a1a1a
    style REC fill:#ffe7b3,stroke:#cc8800,color:#1a1a1a
    style KEEP fill:#ffe7b3,stroke:#cc8800,color:#1a1a1a
    style FINAL fill:#d6f5d6,stroke:#2e8b2e,color:#1a1a1a
    style SERVER fill:#cfe8ff,stroke:#005bbb,color:#1a1a1a
```

### Deferred state cases (`Extensions.fs`)

| Case | Meaning | UI effect |
| ---- | ------- | --------- |
| `HasNotStartedYet` | no request yet | empty |
| `InProgress` | in flight, **no** prior value | loading placeholder / spinner |
| `Recalculating of 't` | in flight, **prior value kept** | preliminary value stays visible |
| `Resolved of 't` | response received | confirmed value |

Stepping uses **`Recalculating`** (not `InProgress`), which is why the previous
dose quantity remains on screen as a preliminary result instead of blanking out.
The orange nodes are the preliminary (awaiting-server) phase; green is the
confirmed solver result.

## Key points

- **Stepping is server-side**, not local: every `+`/`-` round-trips through the
  solver. The client only dispatches
  `Increase/DecreaseOrderableDoseQuantityProperty(ntimes, useCalc)` and renders
  the result.
- **`useCalc`** flag decides whether stepping uses calculated constraints vs
  defined ones (`OrderVariable.step`).
- **The step math** (`OrderVariable.fs`): increase = `min + N*incr`,
  decrease = `max - N*incr`, then `pickNearestHigherElseLower` snaps to a
  feasible value in the variable's domain.
- **Re-solve**: after the property change, `calcMinMaxStep` recomputes
  constraints and `Order.solve` feeds equations to GenSOLVER, which returns
  refined domains mapped back into the `OrderScenario` DTO.

## Source references

| Hop | File | Symbol |
| --- | ---- | ------ |
| UI stepper | `src/Informedica.GenPRES.Client/Views/Prescribe.fs` | `Increase/DecreaseOrderableDoseQuantityProperty` |
| Elmish msg | `src/Informedica.GenPRES.Client/App.fs` | `OrderContextMsg` |
| Server call | `src/Informedica.GenPRES.Client/App.fs` | `makeServerCall` |
| Shared DTO | `src/Informedica.GenPRES.Shared/Api.fs` | `OrderContextCommand` |
| Server cmd | `src/Informedica.GenPRES.Server/ServerApi.Command.fs` | `processCmd` |
| Server service | `src/Informedica.GenPRES.Server/ServerApi.Services.fs` | `OrderContext.evaluate` |
| GenORDER eval | `src/Informedica.GenORDER.Lib/Api.fs` | `evaluate` / `processPropertyCmd` |
| Pipeline | `src/Informedica.GenORDER.Lib/OrderProcessor.fs` | `processPipeline` |
| Property change | `src/Informedica.GenORDER.Lib/OrderProcessor.fs` | `processChangeProperty` |
| Step math | `src/Informedica.GenORDER.Lib/OrderVariable.fs` | `step`, `pickNearestHigherElseLower` |
| Constraint recalc | `src/Informedica.GenORDER.Lib/OrderProcessor.fs` | `calcMinMaxStep` |
| Solve | `src/Informedica.GenORDER.Lib/Order.fs` | `solve` |
| UI re-render | `src/Informedica.GenPRES.Client/Views/Order.fs` | dose quantity select |
