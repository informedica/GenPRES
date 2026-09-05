# Code Review — GenFORM `Check` module vs. IR Doseringscontrole V-5-0-1

**Date:** 2026-06-08
**Reviewed file:** `src/Informedica.GenFORM.Lib/Check.fs`
**Reference:** Z-Index *Implementatierichtlijn Doseringscontrole*, IR V-5-0-1
(16-09-2025).
**Status:** Implemented — all 9 findings migrated to source on branch
`feat/genform-check-ir-doseringscontrole` (Check.fs, ZForm Types.fs/DoseRule.fs/
GStand.fs, ServerApi.Services.fs) with 11 GenFORM + 3 ZForm unit tests. The
original prototypes lived in `src/Informedica.GenFORM.Lib/Scripts/CheckReview.fsx`, which has
since been removed — the migrated source is the record.

## 1. Purpose & framing

The `Check` module validates each GenFORM `DoseRule` against the official
G-Standaard dose check. For a rule it looks up the matching ZForm/G-Standaard
dose ranges (`matchWithZIndex` → `createMapping`) and tests whether the GenFORM
rule's allowed range stays inside the G-Standaard envelope
(`checkInRangeOf`, range-vs-range containment).

This is a **defensible adaptation** of the IR. The IR describes checking a single
*entered* dose against scalar bounds; a GenFORM `DoseRule` instead describes an
allowed *range* over a *patient category*. Asking "does my rule's range fit
inside the G-Standaard limits?" is the correct generalization. The findings below
are deviations *within* that framing, not objections to it.

## 2. Key IR facts used in this review

| § | Fact |
|---|------|
| 1.3.1 | Checkable: min/max keerdosis (GPK base unit), aantal per tijdseenheid, tijdseenheid, age (months), weight/BSA category, gender, route, ICPC. Norm max may be exceeded exceptionally; **absolute max never**. |
| 1.3.2 | **Not** checkable: alternating dose/freq; taper & loading doses; **toedieningssnelheid (rate)**; **toedieningsduur**; combination-dependent limits. Continuous infusion stored as 1×/hour, no duration check. |
| 3.3 | Age categories in months; <1 month as a fraction (1 mo = 30 days). |
| 3.4 | Time-unit interchangeability: per 2 dagen ↔ om de dag; per 4 weken ↔ per maand; per 8 weken ↔ per 2 maanden; per half jaar ↔ per 6 maanden. **per 12 weken ≠ per 3 maanden**. |
| 4.5.2 | Entered frequency (aantal + tijdseenheid *combination*) must appear in a matching rule; else tekst 24 (aantal) / 25 (tijdseenheid) / 8 (both). |
| 4.6.1 | Limit selection priority: **m² (BSA) → per kg → absolute** — use the first that exists. |
| 4.6.1.2 | *Norm max* = advisory upper bound; *absolute max* = hard ceiling. Absolute min is never filled. |
| 4.6.1.3 | A margin may be applied for "normal" substances (IR example: signal only at >120% of max), provider-configurable, **only** for weight/BSA dosing. |
| 4.6.1.4 | For narrow-therapeutic-index substances (`GPRISC = *`): **no margin**. |
| 4.6.2 | Flow: if dose > norm max → check abs max → if > abs max serious (tekst 3) else advisory (tekst 1); else check norm min → if below, underdose (tekst 2). Abs-max check is *conditional* on norm-max exceedance, and the two carry different severity. |

## 3. Verified code facts

- `DoseLimit.getNormDose` (`DoseLimit.fs:104`) returns `Some vu` only when
  `min = max`; else `None`.
- ZForm `DoseRange` (`ZForm/Types.fs:51`): `Norm`/`NormWeight`/`NormBSA`
  (advisory) and `Abs`/`AbsWeight`/`AbsBSA` (absolute ceiling).
- `GStand.createDoseRules cfg age wght bsa gpk gen frm rte`; Check.fs passes
  `config a w None None gen frm` → **bsa & gpk None, gender never passed**.
- ZForm `RateDosage` is populated from G-Standaard, only when `cfg.IsRate`, a
  single frequency, and time unit = hour (`GStand.fs:480`).
- GenFORM `DoseLimit` (`GenFORM/Types.fs:276`) has no GPRISC/risk/narrow-TI field.

## 4. What the module gets right

- Compares against both `Norm` and `Abs` ranges — carries the IR two-tier data.
- `checkAdjustUnit` gates comparisons to matching adjustment units (kg-vs-kg,
  m²-vs-m²) and emits an explicit "eenheden verschillen (kg vs m2)" message — a
  sound safety guard.
- Frequency `isSubset` check aligns with IR 4.5.2 (entered freq ⊆ G-Standaard).
- Lookup keyed on base generic name (`Generic.genericName`) matches the GPK model.
- `PerTimeAdjust` reconstructed from `QuantityAdjust × frequency` when empty.

## 5. Findings

### HIGH-1 — Margin applied to risk substances (safety)

IR 4.6.1.4 forbids a margin for narrow-TI substances. `toMinMax` (inside
`checkDoseRule`) applies a symmetric ±10% band unconditionally, and GenFORM types
carry **no GPRISC/risk field**, so risk substances cannot be told apart. For a
high-risk drug the band widens the accepted range and can suppress a real
overdose signal. Mitigating factor: only triggers where `getNormDose` returns
`Some` (a fixed-point adjusted norm dose).

### HIGH-2 — Infusion-rate checks are outside IR scope

IR 1.3.2 (5 & 6) excludes toedieningssnelheid and toedieningsduur. `rateChecks` /
`rateFieldsFor` run for `Continuous`/`Timed`/`OnceTimed`, comparing GenFORM
`Rate`/`RateAdjust` to ZForm `RateDosage`. The reference data exists in
G-Standaard, but the IR defines no rate comparison — these checks assert a
guideline rule that does not exist.

### MEDIUM-1 — BSA/weight selection priority inverted

IR 4.6.1 priority is m² → kg → absolute. `createMapping` (and `rateFieldsFor`)
pick `NormWeight` (kg) first, `NormBSA` only as fallback. When G-Standaard has
*both*, the m² range is hidden; if the GenFORM limit is per m², `checkAdjustUnit`
then finds no unit match and the comparison is **silently skipped** despite a
valid m² reference existing.

### MEDIUM-2 — No norm-max vs absolute-max severity / flow

IR 4.6.2 treats norm-max exceedance as a mild advisory (tekst 1) and absolute-max
exceedance as serious (tekst 3), checking abs only *if* norm is exceeded.
`checkDoseRule` checks `Norm` and `Abs` independently and emits identical
"niet in bereik" strings — the severity distinction central to the IR is lost.

### MEDIUM-3 — Age months→days mapping (known TODO)

`filterPatient` carries `// TODO need to map G-stand age in mo to days
(1 mo = 30 days)`. IR 3.3 uses month categories; age matching may be inaccurate
at boundaries / <1 month.

### LOW-1 — Category aggregation vs precise selection

IR 4.5 selects one specific rule (`GPDDNR`). `maximizeDosages` unions all matches
into the widest envelope (more permissive). Defensible for rule-vs-rule
validation; documented divergence.

### LOW-2 — Gender not passed

`GStand.createDoseRules` is called without gender; IR 4.2.4 has a gender gate.
Mostly affects product applicability, not the dose range.

### LOW-3 — Frequency time-unit interchangeability ignored

IR 3.4 table not honored; `isSubset` treats "per maand" and "per 4 weken" as
different, producing spurious frequency mismatches.

### LOW-4 — Frequency message granularity

IR distinguishes tekst 24/25/8; `freqRow` emits one combined message.

### INFO-1 — Underdose checking more aggressive than IR

IR makes max primary; min only in special cases; absolute min never filled. Module
checks min symmetrically. Acceptable; noted.

### INFO-2 — Missing-frequency signal suppression (IR 3.4.2) not implemented

Advanced functionality gap; no correctness impact.

### BUG-A — `quantityAdjustAbs` mixes `SingleDosage` and `StartDosage`

`createMapping` (`Check.fs:403-414`): the kg branch reads
`x.SingleDosage.AbsWeight` but the m² branch reads `x.StartDosage.AbsBSA`
(StartDosage, not SingleDosage). Inconsistent with `quantityAdjustNorm`
(both `SingleDosage`). A per-m² absolute single-dose limit is taken from the
wrong dosage. Latent data bug, found while reviewing — not a guideline issue.

### BUG-B — `maximizeDosages` computes `Abs` from `Norm`

`maximizeDosages` (`Check.fs:181`): `Abs = maximize [ dr.Norm; acc.Norm ]` —
the merged **absolute** non-adjusted range is built from **Norm** values. When
more than one G-Standaard dosage matches and is merged, the absolute-max ceiling
collapses to the norm-max value. Verified in FSI: two dosages with `Abs.Max` 5
and 9 merge to a buggy `Abs.Max` of 3 (the norm max) instead of 9. Should be
`maximize [ dr.Abs; acc.Abs ]`. **Safety-relevant** (corrupts the hard ceiling).

## 6. Remediation

Prototypes lived in `src/Informedica.GenFORM.Lib/Scripts/CheckReview.fsx` (drop-in helper
functions, each annotated with the `Check.fs` location it replaces); that script has since
been removed, the findings having been migrated into `Check.fs`. All changes are script-only per `AGENTS.md`; the maintainer migrates
verified code into `Check.fs`. Validated in FSI on 2026-06-08 against the live
provider (7866 dose rules; aciclovir IV baseline = 6 rules, 9 didNotPass,
6 didPass — unchanged).

### Verified prototype results (FSI)

| Finding | Helper in `CheckReview.fsx` | Result |
|---------|------------------------------|--------|
| MEDIUM-1 | `pickAdjust` | both ranges present → picks `mg/m2` (was `mg/kg`). |
| MEDIUM-2 | `Severity` + `classify` | `Within / AdvisoryOverNorm / OverAbsolute / UnderNorm` for doses 5/15/25/1 vs norm 10, abs 20, min 2. |
| HIGH-1 | `marginedTestRange` | non-risk max 100 → 120 (one-sided 120%); risk max 100 → 100 (no margin). Replaces symmetric ±10% `toMinMax`. |
| HIGH-2 | `RateCheckMode` + `rateScopeLabel` | rate rows dropped or tagged "[buiten G-Standaard doseringscontrole]". |
| MEDIUM-3 | `monthsMinMaxToDays` | 3 mo → 90 dag (IR 30-days/month). |
| LOW-3 | `interchangeable` | `per maand ~ per 4 weken = true`; `per 12 weken ~ per 3 maanden = false`. |
| LOW-4 | `freqMsg` | routes to tekst 24 / 25 / 8. |
| BUG-B | (corrected `maximize [dr.Abs; acc.Abs]`) | merged `Abs.Max` 9 (was 3). |

### Source-file migration targets (`Check.fs`)

- `createMapping` (lines 391-440): apply `pickAdjust` to
  `quantityAdjustNorm`/`quantityAdjustAbs`/`perTimeAdjustNorm`/`perTimeAdjustAbs`
  (MEDIUM-1); fixes BUG-A in passing.
- `maximizeDosages` (line 181): `Abs = maximize [ dr.Abs; acc.Abs ]` (BUG-B).
- `checkDoseRule` `toMinMax` (lines 455-465): replace with `marginedTestRange`
  (HIGH-1); requires a risk flag — see prerequisite below.
- `checkDoseRule` result rows (lines 536-773): adopt `Severity` instead of
  `bool option` (MEDIUM-2); keep `didPass`/`didNotPass` as projections.
- `checkDoseRule` `rateChecks` (lines 651-686): gate with `RateCheckMode`
  (HIGH-2).
- `rateFieldsFor` (lines 490-509): apply `pickAdjust` (MEDIUM-1 consistency).
- `filterPatient` (lines 238-250): `monthsMinMaxToDays` on the ZForm age before
  `MinMax.intersect` (MEDIUM-3) — first confirm the ZForm `PatientDosage.Patient.Age`
  unit.
- `freqRow` (lines 560-575): `canonTimeUnit` before `isSubset` (LOW-3) and
  `freqMsg` for granular text (LOW-4).

### Prerequisite for HIGH-1 (data, maintainer decision)

`marginedTestRange` needs a per-substance narrow-therapeutic-index flag
(`isRisk`). The risk flag is **not** a GenFORM property — it is a property of the
G-Standaard substance and **already exists upstream**: `ZIndex DoseRule.HighRisk`
(`ZIndex/Types.fs:264`), set from `vas.GPRISC = "*"` (`ZIndex/DoseRule.fs:422`,
sourced from `bst640.GPRISC`). So a new GenFORM column is **not** required, and
no change to the sheet contract (the `Data` record types in
`GenFORM.Lib/Types.fs`) is needed.

The obstacle is that `Check.fs` reaches G-Standaard via
`GStand.createDoseRules`, whose final ZForm `Dosage` **drops** the flag: ZForm has
no `HighRisk` field and `Dosage.Rules` is only a string tag
(`GStandRule`/`PedFormRule`). The flag is available *inside* GStand (it holds the
source `ZIndexTypes.DoseRule` records — `GStand.fs:281/352/450`) but is discarded
before reaching Check.

**Recommended fix (Option 1) — thread `HighRisk` through ZForm:**

1. `ZForm.Lib`: add `HighRisk: bool` to `Dosage` (`Types.fs:87`); set it in
   `GStand.createDoseRules` from the `ZIndexTypes.DoseRule` records it already
   holds (`HighRisk = drs |> Array.exists _.HighRisk` — ANY high-risk rule ⇒
   treat as risk).
2. `Check.fs`: `matchWithZIndex` surfaces `dosage.HighRisk`; replace `toMinMax`
   (lines 455-465) with `marginedTestRange isRisk` reading that flag.

No GenFORM schema change, no second data lookup, no resource-doc change.

Alternative (Option 2): look the flag up directly in `Check.fs` via
`RuleFinder` (already aliased, `Check.fs:18`) keyed on the matched GPK — avoids
touching ZForm but adds a second lookup path.

**Validated end-to-end (FSI, 2026-06-08)** via the `gstandHighRisk` /
`matchWithRisk` / `runOption1` prototypes in `CheckReview.fsx`, which re-derive
the flag from the same ZIndex DoseRules GStand consumes (script-only; cannot edit
ZForm in a script): `digoxine` (narrow-TI) → `highRisk=true` → no margin (norm
100 stays 100); `aciclovir`/`paracetamol` → `highRisk=false` → 120% margin
applied; the 6 real aciclovir IV baseline rules thread `highRisk=false` through
`matchWithRisk`.

**Safe interim** (until Option 1 lands): apply **no** margin — treat all
substances as risk — rather than the current unconditional ±10% band; strictly
safer (never widens a high-risk drug's accepted range).

### Note on the existing Scripts

`Scripts/load.fsx` and `Scripts/Check.fsx` predate the GenFORM v2 migration and
no longer load (missing `GenericLabel/PharmaceuticalForm/ProductId/Generic/
Source/DoseRuleData`, and `Check.fsx` still treats `dr.Generic` as a string).
`CheckReview.fsx` carries the correct canonical load order; the older scripts
should be refreshed or retired separately.
