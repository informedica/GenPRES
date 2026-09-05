# Implementation plan for issue #307: G-Standaard dose rule fallback for missing adult rules

**Date**: 2026-04-17

**Status**: Proposed — prototype exists in `src/Informedica.GenFORM.Lib/Scripts/GStandDoseRules.fsx`, not yet migrated

**Confidence**: Medium

> Formerly ADR-0016 (proposed 2026-04-17). Moved out of `docs/adr/` under issue #411 because it is a plan for ongoing work, not a hard-to-reverse decision; see [ADR-0000](../adr/0000-documentation-rules.md).

**Related PR**: [#310 — GStandDoseRules.fsx prototype](https://github.com/informedica/GenPRES/pull/310)

**Related Issue**: [#307 — Add G-Standaard dose rules](https://github.com/informedica/GenPRES/issues/307)

## Context

GenPRES dose rules are sourced primarily from Google Spreadsheets managed by
clinical pharmacists (`GenFORM.Api.getDoseRules`). This crowd-sourced dataset
provides excellent coverage for pediatric patients but often lacks rules for
the *adult* patient category because GenPRES is a pediatric system and adult
rules have historically not been entered.

However, the formulary view (`/formularium`) is also used by adult-care clinicians
who need to verify whether a medication has a registered adult dose. When a
combination of generic name, dosage form, and route has no adult rule in the
Google Sheet, the view shows nothing — a confusing and potentially risky blank.

The G-Standaard (G-Standaard) is the official Dutch drug reference database.
GenPRES already integrates G-Standaard data through the `ZForm` library
(`Informedica.ZForm.Lib`) via `ZForm.GStand.createDoseRules`. These data are
used during product lookup but not currently surfaced in the formulary dose-rule
layer.

### Type hierarchy mismatch

`ZForm.DoseRule` has a deeply nested hierarchy:

```text
ZForm.DoseRule
  └─ Dosages[] (indication → route → patient → dosage)
       └─ RouteDosages[]
            └─ PatientDosages[]
                 └─ PatientDosage { DosageRange; DoseType; PatientCategory }
```

`GenFORM.DoseRule` is flat:

```text
GenFORM.DoseRule { Generic; Form; Route; PatientCategory; DoseType; Source; Limits[] }
```

Converting between the two requires flattening the ZForm hierarchy and mapping
age/weight units (ZForm uses months/kg, GenFORM uses days/grams).

## Decision

When the formulary API returns dose rules for a `(generic, form, route)` combination,
**append fallback rules sourced from the G-Standaard** for any combination where
no adult rule exists in the Google Sheet dataset.

### Key design choices

| # | Choice | Rationale |
|---|--------|-----------|
| 1 | `Source = "G-Standaard"` on every generated rule | Provides UI provenance; allows color-coding and link to the G-Standaard monograph |
| 2 | Filter `DoseType.Continuous` rules | Continuous infusion rules require companion solution rules not available in `ZForm.DoseRule`; surfacing them without solution context is misleading |
| 3 | Skip reconstitution products without a `GenFORM.Product.Reconstitution` rule | If a product requires preparation that is not configured in the system, the G-Standaard dose cannot be safely applied |
| 4 | Adult threshold: `Age.Min ≥ 18 years` (216 months / 6574 days) | Consistent with the GenFORM patient-category model; matches the clinical concept of "adult" for the purposes of this fallback |
| 5 | Prototype first in `.fsx`, migrate after human review | Follows the GenPRES script-only policy; no source files modified until the maintainer validates the conversion logic |

### Prototype location

`src/Informedica.GenFORM.Lib/Scripts/GStandDoseRules.fsx` — 434-line FSI
prototype that demonstrates the full pipeline from missing-rule detection
through ZForm query and type conversion.

### Planned source file integration

When the maintainer is ready to migrate:

1. Add `GStandAdapter` module in `src/Informedica.GenFORM.Lib/` (new file
   `GStandAdapter.fs`).
2. In `Api.getDoseRules`, after Google Sheet rules are loaded:

   ```fsharp
   let fallback = GStandAdapter.buildFallbackRules config loadedRules
   Array.append loadedRules fallback
   ```

3. Keep `Source = "G-Standaard"` as the discriminator in the UI layer
   (already implemented via the color-coding introduced in PR #309).

## Consequences

**Positive**:

- Adult clinicians see a dose reference even when the pediatric-focused
  Google Sheet has not been updated.
- Provenance is explicit (`Source` field, color badge) — clinicians know the
  rule comes from the G-Standaard, not from a validated pediatric pharmacist rule.
- No change to the existing rule-loading pipeline; fallback rules are appended
  after existing rules.

**Negative / Trade-offs**:

- Introduces a dependency on G-Standaard data quality; incorrect G-Standaard
  entries will propagate to the formulary view.
- The type conversion is non-trivial; an incorrect unit mapping (months→days,
  kg→grams) would silently produce wrong age/weight thresholds. The prototype
  includes explicit unit-conversion tests to guard against this.
- Continuous and reconstitution rules are silently filtered; a follow-up should
  address how these are surfaced.

**Safety**:

- G-Standaard-sourced rules must be visually distinguished from pharmacist-curated
  rules (color badge, source label). This is a safety requirement.
- Any migration of the prototype to source files must be accompanied by new
  Expecto tests covering the age/weight unit conversion and the adult-threshold
  detection logic.
- The feature is gated behind the existing `GENPRES_URL_ID` environment variable;
  demo deployments without a live G-Standaard connection will fall back gracefully
  to an empty array.

## References

- [GStandDoseRules.fsx prototype — PR #310](https://github.com/informedica/GenPRES/pull/310)
- [G-Standaard dose rule check color coding — PR #309](https://github.com/informedica/GenPRES/pull/309)
- [GenFORM sheet contract — the `Data` record types in `src/Informedica.GenFORM.Lib/Types.fs`](../../src/Informedica.GenFORM.Lib/Types.fs)
- [MCP Server Architecture — ADR-0009](../adr/0009-mcp-server-architecture.md)
- [ZForm.GStand API — `src/Informedica.ZForm.Lib/GStand.fs`](../../src/Informedica.ZForm.Lib/GStand.fs)
