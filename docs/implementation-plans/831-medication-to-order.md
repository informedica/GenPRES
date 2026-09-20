# Implementation plan for issue #831: Medication.toOrder, an Order built without a Dto

> Trailing phase **O2** of [plan 725](725-contract-model-dto-domain-flow.md), filed as its own
> issue as that plan decided. The vocabulary, the five Dto invariants and rule R8 are in
> [ADR-0008](../adr/0008-contract-model-dto-mapping-boundary.md); this document does not
> restate them.
> Issue: [#831](https://github.com/informedica/GenPRES/issues/831).

## Problem description

`Medication.toOrderDto` builds an `Order` by mutating a Dto graph: about four hundred lines
turn a `Medication` into an `Order.Dto`, and `Order.Dto.fromDto` turns that straight back into
the `Order` the Dto constructors had already built and discarded — `Order.Dto.once` and its
siblings are `createNew … |> toDto`. A Dto is a boundary type. Used as the domain's builder it
ties the shape of an order to the shape of its serialization, which is the case ADR-0008 rule
R8 names first.

Two production call sites go out to the Dto and back:
`Api.fs` `evaluateRules`, where the failure is dropped by `Result.toOption`, and
`Nutrition.fs` `proc`. A third, `OrderScenario.Dto.fromDto`, is a real inbound boundary and
stays as it is.

## Approaches considered

- **Leave it, and note the breach.** What plan 725 did. The cost is that an `Order` cannot be
  constructed without a Dto, which is one of the two reasons that plan's inbound path needs two
  steps instead of one.
- **Wrap the round trip in `toOrder` and stop there.** Gives callers a domain-typed
  constructor, but the Dto stays the builder, so nothing that waits on O2 is unblocked.
- **Build the `Order` from the `Medication` directly, as a pipeline.** The approach taken.

## Chosen approach

`Medication.toOrder : Medication -> Result<Order, Exceptions.Message>`, whose body is a
pipeline of pure steps, each `Medication -> Order -> Order` and each with one concern:

```fsharp
let toOrder (med: Medication) : Result<Order, Exceptions.Message> =
    med
    |> newOrder                      // order type -> schedule, id, name, route
    |> withComponents med            // components and items, shape only
    |> withItemConstraints med       // per item: quantity, concentration, dose, solution
    |> withComponentConstraints med  // per component: quantity, concentration, dose
    |> withOrderableConstraints med  // orderable quantity, dose count, dose, divisibility
    |> withPrescription med          // frequency, administration time
    |> withAdjustment med            // patient adjust
```

Shape first, then one pass per constraint concern. The whole body is wrapped once, returning
`Exceptions.OrderCouldNotBeCreated`, the error `Order.Dto.fromDto` already returns, so both
call sites change by composition alone.

What makes this a rewrite rather than a redesign: every one of the existing helpers' mutations
writes `*.Constraints.{MinOpt,MinIncl,MaxOpt,MaxIncl,IncrOpt,ValsOpt}` on a `Variable.Dto`, and
none of them touches `.Variable` or `.Calculated`. The domain counterpart of that block is one
immutable record, `OrderVariable.Constraints`, set with `OrderVariable.setConstraints`. The
`Variable` and `Calculated` halves are reproduced by not touching them.

The semantics to reproduce are those of `OrderVariable.Dto.fromDto`:

- a value set and an increment are **guarded** — an empty `ValueUnit` becomes `None`;
- a minimum and a maximum are not guarded, and carry their inclusive flag;
- anything a helper leaves unset keeps what `createNew` installed, an exclusive minimum of zero
  in `NoUnit`.

The guards are where this change is most likely to go wrong: a missing one produces an empty
value set where there is `None` today, which the solver reports as an empty value range. They
belong in the setters, once, not at each call site. The setters update the `Constraints` record
field-wise, never whole-record, because several steps write only a minimum or only an
increment and rely on the rest surviving, and because the pipeline is last-write-wins.

## Two findings that shape the work

**Values and units survive the present round trip exactly.** `ValueUnit.Dto.toDto` writes the
serialized `Unit` into `dto.Json` beside the localized string, and `fromDto` prefers that
field; the value travels as `BigRational[]`. The english and dutch unit strings on that path
are decoration and cannot move a dose. So the dosing risk here is not a drifting unit; it is
the hand rewrite of the constraint mutations, which the harness below is aimed at.

**`Component.Id` is lost today.** `Component.Dto.toDto` never writes `dto.Id`, while `fromDto`
reads it, so every order built through the round trip carries the empty id although
`Component.createNew` sets it from the medication id. Nothing reads the field, so no dose
depends on it, but a direct build keeps the real id and the orders would not compare equal.
The missing line is written here, before the pipeline lands, so that the baseline already has
the real id and the equivalence proof below is over one thing at a time.

That is a correction of a value, not of a structure. `Id` is already a field of the component
Dto and is already serialized — a stored order plan version says `"Id":""` today — so the field
names, the nesting and the value formats all stay as they are. No new JSON structure version,
no upgrade step, no fixture: ADR-0008's machinery is for a shape that changed, and this shape
does not. Rows signed before the change keep the empty id and are never rewritten. A digest is
taken over the serialized form, so one issued before the change and answered after it does not
match and is refused; a challenge lives two minutes, which is the whole of that window.

## Confidence

High on the shape: the target pipeline is the existing helper set with an immutable record in
place of four mutable fields, and the domain constructors it needs are all public. Medium on
the step sizes, which the "As built" table will correct.

## Steps

Each step is a branch off `master` and reaches upstream as its own PR. Code steps are
prototyped in `.fsx` under `src/Informedica.GenORDER.Lib/Scripts/` first and migrated to source
by the maintainer.

| Step | What | ~src lines |
|---|---|---|
| 0 | This document. | 0 |
| A | `Medication.toOrder` as the existing composition, and the two call sites switched to it. Identical by construction; the seam every later step changes behind. | ~10 |
| C1 | `Component.Dto.toDto` writes the id. | ~1 |
| C2 | The round-trip law and the signing-digest tests for the id now surviving. | 0 |
| H | The equivalence harness as a script: both paths over every scenario fixture, unsolved and solved. | 0 |
| B0 | The field-wise `Constraints` setters, with the guards, and a `map` over the order-variable wrappers. | ~45 |
| B1 | `newOrder` and `withComponents`: the shape pass. Nothing calls it yet. | ~70 |
| B2 | `withItemConstraints`. | ~110 |
| B3 | `withComponentConstraints`. | ~95 |
| B4 | `withOrderableConstraints`, including divisibility and the timed case. | ~145 |
| B5 | `withPrescription`, `withAdjustment`, and the flip: `toOrder`'s body becomes the pipeline, `toOrderDto` becomes a shim over it with the signature it has today. | ~60 |
| B6 | The old helpers deleted, in two steps. | −410 |

B1 to B4 are additions that nothing calls, so B5 is the only step that changes behaviour.
Converting the helpers in place instead is not open: a half-converted pipeline does not
compose, and bridging each level with `toDto` and `fromDto` would touch dosing at every step
for no gain.

The shim in B5 keeps `toOrderDto`'s present signature, so no call site churns. It goes, with
the conversion helpers it uses and the frozen reference implementation in the test project,
when [#830](https://github.com/informedica/GenPRES/issues/830) retypes `Totals.getTotals` on
`Order` and removes its last consumer.

One intentional change of behaviour beyond the id, called out in the B5 PR: an order type of
`Any` or `Process` raises today, outside the `try/with`, and so takes down the rule evaluation
that meets it; in `toOrder` it becomes an `Error`, and the caller skips that medication.

## Verification, end to end

The dose must not move. The harness runs both paths over every fixture in the scenarios module
— the suppository, amphotericin, morphine, the oral liquid, cotrimoxazole, both parenteral
nutrition orders, the fully populated medication, and the three text fixtures — which between
them cover all five order types, single and multiple components, solution limits and
divisibility.

1. The unsolved orders compare equal as records, once the start time is normalized: both paths
   read the clock, which is [#794](https://github.com/informedica/GenPRES/issues/794).
2. When they do not, the canonical serialization of both — the form the signing digest is taken
   over — is diffed to name the variable that moved.
3. The solved orders compare equal as printed orders, after the full pipeline of minimum and
   maximum calculation, increment increase, value calculation and solving; and the parenteral
   nutrition fixture additionally through `Nutrition.proc`, the one path that applies property
   changes immediately after construction.

Layers 1 and 3 land as tests and stay. Any difference other than the component id stops the
work and is reported, not absorbed.

Per step: `dotnet run ServerTests`; `dotnet fsi scripts/CheckDependencyRule.fsx` unchanged —
R8 is a rule checked in review, not by the script, so no allowance moves here. Acceptance for
the flip: a demo server, a launch as a prescriber, paracetamol oral tablet prescribed, the dose
stepped up and down and the order plan signed, with the scenario text compared against a run
from before the flip; and the order-scenario tool answering the same scenarios for the same
input.

## As built

| Step | PR | Landed |
|---|---|---|
