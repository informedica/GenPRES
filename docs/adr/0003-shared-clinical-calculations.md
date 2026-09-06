# ADR-0003: Shared Library Clinical Calculations

**Date**: 2026-04-27

**Status**: Accepted

**Formerly**: ADR-0019, renumbered on 2026-09-06 when `docs/adr/` was made contiguous (see [ADR-0000](0000-documentation-rules.md)).

**Related PRs**:

- [#276 — BSACalculations.fsx prototype](https://github.com/informedica/GenPRES/pull/276)
- [#277 — EmergencyCalcTests.fsx](https://github.com/informedica/GenPRES/pull/277)
- [#278 — AgeCalculations.fsx prototype](https://github.com/informedica/GenPRES/pull/278)
- [#284 — RenalCalculations.fsx prototype](https://github.com/informedica/GenPRES/pull/284)
- [#301 — Implement Calculations.fs in Shared library](https://github.com/informedica/GenPRES/pull/301)

## Context

GenPRES uses the SAFE Stack with Fable (an F#-to-JavaScript compiler) for the
client side. The client and server share types and API contracts via the
`Informedica.GenPRES.Shared` library, which is compiled both by the .NET runtime
(server) and by Fable (client).

Several clinical calculations are required on both sides of the wire:

- **Body Surface Area (BSA)** — used for weight-adjusted dosing and display
- **Age utilities** — post-menstrual age and corrected age for preterm infants
- **Renal function** — eGFR formulas for dose adjustment in renal failure

Prior to this decision, these calculations existed only in server-side F# source
files (`GenFORM.Lib`). The client therefore had to send a network request to
obtain any calculated value, which introduced:

1. **Latency** — every formulary view dependent on BSA or eGFR required a server
   round-trip even for display-only rendering.
2. **Offline fragility** — a future progressive web app (PWA) or offline mode
   cannot call the server.
3. **Code duplication risk** — if a client-side calculation was ever needed, a
   developer might reimplement the formula in a slightly different way, causing
   server/client divergence.

### Fable compatibility constraints

The `Shared` library must compile cleanly under both `dotnet` and Fable. This
restricts the APIs available:

- Only basic `System.Math` functions are available in Fable.
- Complex .NET types (e.g., `System.Numerics`, reflection-heavy code) are not
  available or behave differently in Fable.
- F# units of measure are **erased at compile time** and produce no JavaScript
  overhead — they are safe to use freely.

The existing `Shared/Types.fs` uses `int<gram>` and `int<cm>` for patient weight
and height respectively, providing a natural integration point.

## Decision

Implement a `Calculations.fs` module in the `Informedica.GenPRES.Shared` library
that provides all clinical calculation formulas used by both server and client.

### Module structure

```
Calculations
├── Conversions          — gram ↔ kg, int cm → float cm
├── BSA                  — Mosteller, Du Bois, Haycock, Gehan & George, Fujimoto
├── Age                  — PMA, corrected age, chronological age in days
├── RenalConversions     — creatinine and urea unit conversions
├── EGfr                 — CKD-EPI 2021, CKD-EPI 2009, MDRD, Bedside Schwartz
└── classifyGfr (top-level function)  — KDIGO 2012 GFR category classification
```

### Type safety via units of measure

All public functions use F# units of measure:

| Input | Unit | F# type |
|-------|------|---------|
| Body weight | grams | `int<gram>` |
| Height | centimeters | `int<cm>` |
| BSA output | m² | `float<bsa>` |
| eGFR output | mL/min/1.73 m² | `float<mL/minute/normalM2>` |

The `gram`, `cm`, and standard SI units are defined in `Shared/Types.fs`.
Units of measure annotations are erased by both `dotnet` and Fable at compile
time, so there is **zero JavaScript runtime overhead**.

### Formula selection

#### BSA

Five established formulas are included to allow downstream code to select the
most appropriate one for the clinical context:

| Formula | Reference | Typical use |
|---------|-----------|-------------|
| Mosteller | N Engl J Med 1987;317:1098 | General pediatric and adult |
| Du Bois | Arch Intern Med 1916;17:863 | Historical; adults |
| Haycock | J Pediatr 1978;93:62 | Infants and neonates |
| Gehan & George | Cancer Chemother Rep 1970;54:225 | Oncology dosing |
| Fujimoto | Nihon Eiseigaku Zasshi 1968;23:443 | Asian populations |

#### Age utilities

| Function | Description |
|----------|-------------|
| `postMenstrualAge` | Gestational weeks + chronological weeks (preterm dosing) |
| `adjustedAge` | Chronological age corrected for degree of prematurity |
| `chronologicalAgeDays` | Days between two DateTimes (Fable: JavaScript Date) |

#### Renal function (eGFR)

| Formula | Reference | Notes |
|---------|-----------|-------|
| CKD-EPI Creatinine 2021 | Inker et al., NEJM 2021;385:1737 | No race coefficient |
| CKD-EPI 2009 | Levey et al., Ann Intern Med 2009;150:604 | Older cohort data |
| MDRD 4-variable | Levey et al., Ann Intern Med 1999;130:461 | Legacy; lower accuracy |
| Bedside Schwartz | Schwartz et al., JASN 2009;20:629 | Pediatric (up to 18 yr) |

KDIGO 2012 GFR classification (`G1`–`G5`, plus `G3a`/`G3b`) is provided as a
standalone `classifyGfr` function.

## Consequences

### Positive

- **Single source of truth**: server and client use identical formula implementations;
  divergence is structurally impossible.
- **Zero JavaScript overhead**: UoM annotations erased at compile time produce
  no extra bytes or runtime cost in the Fable build.
- **Compile-time type safety**: mixing grams and kilograms, or centimeters and
  meters, is caught by the F# compiler before the code runs.
- **Offline-ready**: calculations work without a server connection, enabling future
  PWA/offline modes.
- **Testable in isolation**: the Shared library has no server or Fable.Remoting
  dependency, so the calculation functions can be unit-tested with a plain
  `dotnet test` without spinning up the full application.

### Negative / trade-offs

- **Fable compatibility overhead**: every change to `Calculations.fs` must be
  verified to compile under Fable; adding a .NET-only API (e.g., `System.Numerics`)
  would break the client build.
- **Formula proliferation**: shipping five BSA formulas and four eGFR formulas
  means the downstream caller must choose. Documentation and comments are required
  to guide that choice.
- **Limited to pure functions**: stateful or I/O-dependent calculations (e.g.,
  patient-population regressions requiring database lookup) cannot live in Shared.

## Alternatives considered

| Alternative | Reason rejected |
|-------------|-----------------|
| **Server-only via Fable.Remoting** | Extra network latency; offline usage impossible; duplicates API surface area for purely computational concerns |
| **Client-side JavaScript glue** | Defeats the purpose of using F# end-to-end; introduces a separate source of truth |
| **Separate `Shared.Calculations` NuGet package** | Unnecessary complexity for an internal library; version skew risk |
| **Copy formulas into both Server and Client projects** | High divergence risk; violates DRY; contradicts the Shared-library pattern already established by `Models.fs`, `Types.fs`, and `Api.fs` |

## Implementation notes

The prototype work was done incrementally across four `.fsx` scripts
(`BSACalculations.fsx`, `EmergencyCalcTests.fsx`, `AgeCalculations.fsx`,
`RenalCalculations.fsx`) following the project's script-first development
workflow. Each script was verified in F# Interactive and the corresponding
Expecto tests confirmed correctness before the code was migrated to
`Calculations.fs` in PR #301.

The final `Calculations.fs` contains 338 lines and 31+ Expecto tests covering
boundary conditions (premature neonates, extreme weights, zero creatinine guard)
and numerical stability checks.
