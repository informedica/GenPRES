# Dose Quantity Stepping Flow

How `Order.Dose.Quantity` is handled by the UI when the user steps the dose
quantity up/down and the order is re-solved. Every step round-trips through the
server and the constraint solver — the client never computes the value locally.

```mermaid
flowchart TD
    subgraph CLIENT["Client (Fable/Elmish)"]
        UI["Stepper +/- button<br/>Views/Prescribe.fs"]
        MSG["dispatch OrderContextMsg.Command(cmd, ctx, request)<br/>OrderContextState.transition<br/>OrderContextMachine.fs"]
        CALL["interpretOrderContextEffect<br/>CallContext(cmd, ctx, request) → processOrderContext<br/>App.fs"]
        RESP["OrderContextAnswered → OrderContextMsg.Answered(request, Ok ctx)<br/>landing on the request, then OrderContextWorkbench.Evaluated ctx<br/>App.fs, OrderContextMachine.fs"]
        RENDER["Re-render dose select +<br/>enable/disable steppers<br/>Views/Order.fs"]
    end

    subgraph SHARED["Shared API DTO"]
        CMD["Increase/DecreaseOrderableDoseQuantityProperty<br/>(ntimes:int, useCalc:bool)<br/>Shared/Api.fs"]
    end

    subgraph SERVER["Server"]
        SCMD["OrderContextCommand.processCmd<br/>ServerApi.OrderContextCommand.fs"]
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
context it sent visible (`Deferred.Provisional`), and reconciles when the
server answer arrives. Rapid clicks accumulate into the delta and the click
count until the debounced button fires one command; while that command is in
flight the step buttons rest, since the machine drops a command sent while one
is in flight.

```mermaid
flowchart TD
    CLICK["User clicks +/- stepper<br/>ClickCountingButton.onStep<br/>Components/SimpleSelect.fs"]
    DELTA["bump local smallDelta/largeDelta<br/>(React.useState)<br/>SimpleSelect.fs"]
    PRELIM["Render PRELIMINARY label<br/>stepFn(smallDelta, largeDelta)<br/>key stays = server value<br/>SimpleSelect.fs"]
    DISPATCH["debounce fires: dispatch OrderContextMsg.Command<br/>(Increase/DecreaseOrderableDoseQuantityProperty(n, useCalc), ctx, request)<br/>OrderContextState.transition<br/>OrderContextMachine.fs"]

    REC["OrderContextWorkbench.Evaluated held stays; InFlight = ((cmd, sent), request)<br/>projected as Deferred.Provisional sent<br/>OrderContextState.toDeferred, OrderContextMachine.fs"]
    KEEP["No spinner on the field: isOptimisticStep = true<br/>Order.fs<br/>step buttons rest while loading: stepsRest<br/>SimpleSelect.fs<br/>a command while busy is dropped by the machine"]

    SERVER(["Server re-solve round-trip<br/>(see main flow above)"])

    DONE["OrderContextAnswered -> OrderContextMsg.Answered(request, Ok ctx)<br/>landing on the request, then OrderContextWorkbench.step: Evaluated ctx<br/>App.fs, OrderContextMachine.fs"]
    BUMP["revision++<br/>Order.fs"]
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

The machine is two stages (`OrderContextMachine.fs`): the `OrderContextWorkbench`, the
context as the clinical model has it, which knows no request, and the one
request under way (`InFlight`: the command and the context sent, and the id
the answer must name). `transition` runs them in order. An answer passes the
request first (`landing`) and reaches the workbench only when it names the
request under way, so a stale answer is dropped by its id. A command passes
the workbench first (`OrderContextWorkbench.step`) and reaches the request as an intent,
dropped while a request is under way. A failed command, whether the server
refused it or the call did not complete, goes back to the context held, the
last one the server confirmed, never the one sent.

### Deferred state cases (`Deferred.fs`)

The pages read the workbench as a `Deferred<OrderContext>` projected from
`OrderContextState` (`OrderContextState.toDeferred` in `OrderContextMachine.fs`):

| Case | Machine state | Meaning | UI effect |
| ---- | ------------- | ------- | --------- |
| `HasNotStartedYet` | `OrderContextWorkbench.NoPatient` | no patient, no workbench | empty |
| `InProgress` | `OrderContextWorkbench.Unevaluated`, the first evaluation under way | in flight, **no** prior value | loading placeholder / spinner |
| `Provisional of 't` | `OrderContextWorkbench.Evaluated held`, `InFlight ((cmd, sent), request)` | in flight, **the context sent kept**, not yet confirmed | preliminary value stays visible |
| `Resolved of 't` | `OrderContextWorkbench.Evaluated ctx` with nothing under way | answer received | confirmed value |

Stepping uses **`Provisional`** (not `InProgress`), which is why the previous
dose quantity remains on screen as a preliminary result instead of blanking out.
The orange nodes are the preliminary (awaiting-server) phase; green is the
confirmed solver result.

## Key points

- **Stepping is server-side**, not local: every `+`/`-` round-trips through the
  solver. The client only dispatches
  `Increase/DecreaseOrderableDoseQuantityProperty(ntimes, useCalc)` and renders
  the result.
- **One command in flight**: the pure `OrderContextState.transition` sends a
  command only while nothing is under way; while a request is in flight a
  further command is dropped, and the step buttons rest until the answer
  arrives.
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
| Client machine | `src/Informedica.GenPRES.Client/OrderContextMachine.fs` | `OrderContextMsg.Command`, `OrderContextWorkbench.step`, `OrderContextState.transition` |
| Server call | `src/Informedica.GenPRES.Client/App.fs` | `interpretOrderContextEffect`, `OrderContextAnswered` |
| Shared DTO | `src/Informedica.GenPRES.Shared/Api.fs` | `OrderContextCommand` |
| Server cmd | `src/Informedica.GenPRES.Server/ServerApi.OrderContextCommand.fs` | `processCmd` |
| Server service | `src/Informedica.GenPRES.Server/ServerApi.Services.fs` | `OrderContext.evaluate` |
| GenORDER eval | `src/Informedica.GenORDER.Lib/Api.fs` | `evaluate` / `processPropertyCmd` |
| Pipeline | `src/Informedica.GenORDER.Lib/OrderProcessor.fs` | `processPipeline` |
| Property change | `src/Informedica.GenORDER.Lib/OrderProcessor.fs` | `processChangeProperty` |
| Step math | `src/Informedica.GenORDER.Lib/OrderVariable.fs` | `step`, `pickNearestHigherElseLower` |
| Constraint recalc | `src/Informedica.GenORDER.Lib/OrderProcessor.fs` | `calcMinMaxStep` |
| Solve | `src/Informedica.GenORDER.Lib/Order.fs` | `solve` |
| UI re-render | `src/Informedica.GenPRES.Client/Views/Order.fs` | dose quantity select |
