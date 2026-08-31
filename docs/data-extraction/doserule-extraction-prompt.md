# DoseRules Extraction Prompt

## 1. Purpose

Extract Dose Rules from free text (formularies, guidelines, protocols) into a hierarchical JSON document consumed by `Informedica.GenFORM.Lib` (via `DoseRuleExtract.fsx`).

One **rule** = one patient group for one `(Source, Generic, Route, Indication)`. A rule carries 1..N **dose types** (phases: start / maintenance, load / continuous, …). Each dose type carries 1..N **dose limits**, one per `(Component, Substance)` pair.

The GenFORM backend still consumes flat `DoseRuleData` rows (one per `(DoseType, DoseLimit)` pair, defined at `src/Informedica.GenFORM.Lib/Types.fs`). The TSV in `data/sources/Rules/doserules.tsv` keeps the legacy layout (51 active columns + a trailing duplicate block; see `doserule-extraction-flowchart.md` §7.4); flattening/grouping happens in the `Conversion` module inside `DoseRuleExtract.fsx`.

## 2. Domain reference

Authoritative sources (keep open while extracting):

- `docs/domain/genform-free-text-to-operational-rules.md` §3, §5, §6.1, §6.2, Appendix C.2
- `docs/domain/core-domain.md` (OKRs, Rule Hierarchy)
- `docs/data-extraction/doserule-extraction-flowchart.md` (visual decision tree mirroring this prompt under fixed scope assumptions; cross-check of prompt JSON ↔ `DoseRuleData` ↔ TSV in §7)
- Sample rows: `data/sources/Rules/doserules.tsv`

Every field is a Selection Constraint (which rule applies) or a Calculation Constraint (feeds GenSOLVER).

- Selection: `Source`, `Generic`, `Form`, `Brand`, `GPKs`, `Indication`, `Route`, `Dep`, PatientCategory (`Gender`, `Min/MaxAge`, `Min/MaxWeight`, `Min/MaxBSA`, `Min/MaxGestAge`, `Min/MaxPMAge`), `DoseType`, `Component`, `Substance`.
- Calculation: Schedule (`Freqs`, `FreqUnit`, `Min/MaxTime`, `TimeUnit`, `Min/MaxInt`, `IntUnit`, `Min/MaxDur`, `DurUnit`); DoseLimits (`DoseUnit`, `AdjustUnit`, `RateUnit`, `Min/MaxQty`, `Min/MaxQtyAdj`, `Min/MaxPerTime`, `Min/MaxPerTimeAdj`, `Min/MaxRate`, `Min/MaxRateAdj`).

## 3. Output contract

Emit ONE JSON object `{"rules":[...]}`. No markdown, no fences, no prose.

- One entry in `rules[]` per `scheduleText` / patient group.
- Rule-level fields: identity + patient + `scheduleText`, plus `doseTypes[]`.
- Inside each dose type: timing fields (`doseType`, `doseText`, `freqs`, `freqUnit`, `minTime`, `maxTime`, `timeUnit`, `minInt`, `maxInt`, `intUnit`, `minDur`, `maxDur`, `durUnit`) and `doseLimits[]`.
- Inside each dose limit: `component`, `substance`, `doseUnit`, `adjustUnit`, `rateUnit`, and every min/max quantity / per-time / rate field.

Every field must be present. Missing values:

- numbers → `null`
- strings → `""`
- arrays → `[]`

Decimal `.` (convert `7,5` → `7.5`). `gpks`, `freqs`: JSON arrays. Keep `indication`, `scheduleText`, `doseText` verbatim, source language. Replace literal tab with space; collapse newlines to single space.

Minified schema shown to the LLM:

```json
{"rules":[{"sortNo":1,"source":"","generic":"","form":"","brand":"","gpks":[],"route":"","indication":"","scheduleText":"","dep":"","gender":"","minAge":null,"maxAge":null,"minWeight":null,"maxWeight":null,"minBSA":null,"maxBSA":null,"minGestAge":null,"maxGestAge":null,"minPMAge":null,"maxPMAge":null,"doseTypes":[{"doseType":"","doseText":"","freqs":[],"freqUnit":"","minTime":null,"maxTime":null,"timeUnit":"","minInt":null,"maxInt":null,"intUnit":"","minDur":null,"maxDur":null,"durUnit":"","doseLimits":[{"component":"","substance":"","doseUnit":"","adjustUnit":"","rateUnit":"","minQty":null,"maxQty":null,"minQtyAdj":null,"maxQtyAdj":null,"minPerTime":null,"maxPerTime":null,"minPerTimeAdj":null,"maxPerTimeAdj":null,"minRate":null,"maxRate":null,"minRateAdj":null,"maxRateAdj":null}]}]}]}
```

## 4. Field rules

### 4.1 Rule-level (identity, source, patient)

- **sortNo** — integer, source order of rules.
- **source** *(required)* — `NKF`, `FK`, `SWAB`, protocol id.
- **generic** *(required)* — lower-case unless source capitalises.
- **form** — `tablet`, `suspensie`, `injectievloeistof`, `zetpil`, etc. Empty if form-agnostic.
- **brand** — only if rule is brand-specific.
- **gpks** — array of strings.
- **route** *(required)* — source verbatim (`ORAAL`, `INTRAVENEUS`, `RECTAAL`, `SUBCUTAAN`, …).
- **indication** *(required)* — verbatim.
- **scheduleText** *(required)* — full original dose-schedule paragraph, verbatim. Traceability field.
- **dep** — department/ward (`ICK`, `NICU`, `kinderafdeling`). Empty for general.
- **gender** — `male`/`female`/`""`.
- **min/maxAge** — days. weeks ×7; months ×30 (`6 maanden`→182); years ×365 (`1 jaar`→365, `18 jaar`→6574). Inclusive lower, exclusive upper (`1 maand tot 18 jaar` → minAge=30, maxAge=6574).
- **min/maxWeight** — grams (`2000 gr`→2000; `5 kg`→5000).
- **min/maxBSA** — m², float.
- **min/maxGestAge** — days (`34 weken`→238, `41 weken`→287).
- **min/maxPMAge** — days (`32 weken`→224, `44 weken`→308).

One rule = one patient group. If the paragraph describes disjoint patient groups, emit multiple `rules[]` entries.

### 4.2 Dose-type level (one per phase)

| `doseType` | Meaning | Cue |
|------------|---------|-----|
| `once` | single, untimed | "éénmalig", "startdosering" |
| `onceTimed` | single over time | "oplaaddosis over 30 min" |
| `discontinuous` | repeated bolus | "… mg/kg/dag in N doses" |
| `timed` | repeated, each over time | "3 dd in 30 min" |
| `continuous` | continuous infusion | "… mg/kg/uur", "continu infuus" |

- **doseText** — phase label (`startdosering`, `onderhoudsdosering`, `dag 3`). Empty if single phase.
- **freqs** — integer array per `freqUnit` (`in 3 doses`→`[3]`; `3-4 doses`→`[3,4]`). Empty for `once`/`onceTimed`/`continuous`.
- **freqUnit** — `dag`, `uur`, `week`.
- **min/maxTime**, **timeUnit** — infusion/admin time (`in 15 min` → min=max=15, unit="min").
- **min/maxInt**, **intUnit** — interval between doses.
- **min/maxDur**, **durUnit** — total treatment duration.

### 4.3 Dose-limit level (one per (component, substance) pair)

- **component** — often = generic. For combinations (`amoxicilline/clavulaanzuur`) the whole combination is the component.
- **substance** *(required)* — active substance for this limit.
- **doseUnit** *(required)* — `mg`, `g`, `IE`, `microg`, `mL`, …
- **adjustUnit** — `kg`/`m2` when adjusted; else `""`.
- **rateUnit** — `uur`/`min`; `""` unless `continuous` or rate given.

| Field | Unit structure |
|-------|----------------|
| `min/maxQty` | `doseUnit / dose` |
| `min/maxQtyAdj` | `doseUnit / adjustUnit / dose` |
| `min/maxPerTime` | `doseUnit / freqUnit` |
| `min/maxPerTimeAdj` | `doseUnit / adjustUnit / freqUnit` |
| `min/maxRate` | `doseUnit / rateUnit` |
| `min/maxRateAdj` | `doseUnit / adjustUnit / rateUnit` |

- Never invent bounds. Single value (`50 mg/kg/dag`) → fill one side, partner `null`.
- `max 4 g/dag` → `Max*`. `min X` → `Min*`.
- Range (`10-15 mg/kg/dosis`) → both `Min*` and `Max*`.
- Unlabelled single value (recommendation) → `Min*` only, `Max*` `null`.

## 5. Splitting rules

Prefer stacking inside ONE `rules[]` entry. Split only when justified:

- **Multi-phase (start + maintenance)** — same rule, TWO `doseTypes[]` entries differing by `doseType` / `doseText`. E.g. paracetamol IV premature → one rule with `doseTypes=[once "startdosering", discontinuous ""]`.
- **Combination products** — same dose type, TWO `doseLimits[]` entries (shared component, distinct substance). E.g. amoxicilline/clavulaanzuur → one `doseTypes[].doseLimits=[{substance:"amoxicilline",…},{substance:"clavulaanzuur",…}]`.
- **Distinct DoseTypes under same patient group** — stack as multiple `doseTypes[]` entries.
- **Distinct patient groups in one paragraph** — emit separate `rules[]` entries. E.g. `<1wk+<2000g` vs `<1wk+≥2000g` → 2 rules, each with its own patient category. Preserve the same `scheduleText` across sibling rules.

No duplicates: the quadruple `(doseType, doseText, component, substance)` must be unique across all `doseLimits` in the rule. Two dose-type entries MAY share a `doseType` label (e.g. load + maintenance both `timed`) provided their `doseText` distinguishes them.

Multi-phase cues (Dutch):

| Cues | Split into |
|------|------------|
| `startdosering`/`onderhoudsdosering` | 2 dose types (both often `discontinuous`), differ by `doseText` and limits |
| `oplaaddosis`/`onderhoud` or `continu` | load `onceTimed`/`once`; maintenance `continuous`/`timed` |
| `dag 1`/`dag 2-7` | one dose type per day-range; `doseText` = day label |
| `initieel`/`vervolgens` | initial + follow-up dose types |

Missing a load/maintenance phase is a bug — emit both when stated.

## 6. Example

Source (NKF, amoxicilline/clavulaanzuur, IV, "Ernstige bacteriele infecties"):

> `< 1 week en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 50 / 5 mg/kg/dag in 2 doses`

One rule (patient group = < 1 week + < 2000 g), one dose type (`discontinuous`, freqs=`[2]`), two dose limits (one per substance, shared component).

```json
{
  "rules": [
    {
      "sortNo": 211,
      "source": "NKF",
      "generic": "amoxicilline/clavulaanzuur",
      "form": "",
      "brand": "",
      "gpks": [],
      "route": "INTRAVENEUS",
      "indication": "Ernstige bacteriele infecties",
      "scheduleText": "< 1 week en geboortegewicht < 2000 gr Amoxicilline/clavulaanzuur 10:1 : 50 / 5 mg/kg/dag in 2 doses",
      "dep": "",
      "gender": "",
      "minAge": null, "maxAge": 7,
      "minWeight": null, "maxWeight": 2000,
      "minBSA": null, "maxBSA": null,
      "minGestAge": null, "maxGestAge": null,
      "minPMAge": null, "maxPMAge": null,
      "doseTypes": [
        {
          "doseType": "discontinuous",
          "doseText": "",
          "freqs": [2],
          "freqUnit": "dag",
          "minTime": null, "maxTime": null, "timeUnit": "",
          "minInt": null, "maxInt": null, "intUnit": "",
          "minDur": null, "maxDur": null, "durUnit": "",
          "doseLimits": [
            {
              "component": "amoxicilline/clavulaanzuur",
              "substance": "amoxicilline",
              "doseUnit": "mg", "adjustUnit": "kg", "rateUnit": "",
              "minQty": null, "maxQty": null,
              "minQtyAdj": null, "maxQtyAdj": null,
              "minPerTime": null, "maxPerTime": null,
              "minPerTimeAdj": 50, "maxPerTimeAdj": null,
              "minRate": null, "maxRate": null,
              "minRateAdj": null, "maxRateAdj": null
            },
            {
              "component": "amoxicilline/clavulaanzuur",
              "substance": "clavulaanzuur",
              "doseUnit": "mg", "adjustUnit": "kg", "rateUnit": "",
              "minQty": null, "maxQty": null,
              "minQtyAdj": null, "maxQtyAdj": null,
              "minPerTime": null, "maxPerTime": null,
              "minPerTimeAdj": 5, "maxPerTimeAdj": null,
              "minRate": null, "maxRate": null,
              "minRateAdj": null, "maxRateAdj": null
            }
          ]
        }
      ]
    }
  ]
}
```

More examples across DoseTypes: see `data/sources/Rules/doserules.tsv` (flat form) and run the TSV through `Pipeline.fromTsv` in `DoseRuleExtract.fsx` to see the hierarchical equivalent.

## 7. Instructions

1. Emit only the JSON object defined in §3. No markdown, no fences, no prose.
2. One `rules[]` entry per patient group. Stack phases inside `doseTypes[]`; stack substances inside `doseLimits[]`. Split to multiple rules only for disjoint patient groups.
3. Keep `indication`, `scheduleText`, `doseText` verbatim; collapse tabs/newlines to single space.
4. Apply §4 conversions (days/grams/m²; decimal `.`; JSON arrays).
5. Unknown → `null` (num) / `""` (str) / `[]` (arr). Never `0` / `-` / `N/A`.
6. Don't invent bounds; follow §4.3 Min/Max conventions.
7. Within a rule, no duplicate `(doseType, doseText, component, substance)` quadruple across dose-limits.
