# Fit-Gap Analysis: Clinician Calculation Workbook vs GenPRES

## Context

Alongside the [AfsprakenProgramma 2019 (AP2019) fit-gap analysis](fit-gap-ap2019-vs-genpres.md), clinicians at the PICU/NICU (WKZ / UMC Utrecht) maintain **personal Excel calculation workbooks** for bedside computations. This analysis maps one such workbook (`berekingenKoop.xls`) against GenPRES to show how much of this informal, unvalidated local tooling GenPRES can replace, and where it falls outside GenPRES's intended scope.

Where AP2019 is the *official* departmental application, these workbooks are the **shadow tooling** that grows up around it: quick calculators a clinician builds because the official app does not surface a given computation. Replacing them matters for safety — spreadsheet formulas are unversioned, untested, silently copied between colleagues, and carry no audit trail.

**Scope**: The workbook is a superset of bedside calculators. This analysis follows the same scope boundary as the AP2019 analysis: medication, fluid, and enteral/parenteral-nutrition functionality plus cross-cutting patient-derived values. Purely diagnostic tools in the workbook (clinical scores, epidemiology, respiratory mechanics, acid-base interpretation) are noted but treated as out of GenPRES's scope.

**Status column**: Uses the same vocabulary as the AP2019 analysis — **Fit** (GenPRES already covers it), **Partial** (covered with gaps), **Gap** (not implemented), **Out of scope** (deliberately outside GenPRES). Where a workbook function maps to an existing AP2019 requirement, the requirement number is cross-referenced (e.g. AP §7.12).

---

## 0. Workbook Inventory

The workbook contains 11 sheets:

| Sheet | Purpose | Domain |
|---|---|---|
| `intake` | Enteral feed + parenteral fluid composition tables → fluid/nutrient/electrolyte totals per kg/day | Nutrition |
| `medicatie` | ~13 dosing calculators (dose, infusion concentration, combos, taper, suppletion, conversions) | Medication |
| `vocht, electrolieten` | Maintenance/rehydration fluid schedules, GFR, deficits (Na, albumin) | Fluids / Renal |
| `leeftijd` | Gestational age, EDD/LNMP, corrected age | Patient |
| `gewicht` | Weight difference, dehydration weight, neonatal growth velocity | Patient |
| `lengte` | Bone-age height prediction (Greulich-Pyle), target height, BSA (DuBois) | Patient |
| `scorelijsten` | GCS/EMV, Westley croup, asthma score, PRAM, SBAR | Diagnostic |
| `sens_spec` | Sensitivity / specificity / PPV / NPV | Diagnostic |
| `ademhaling` | Oxygenation index, PF ratio, aaDO₂, Stewart acid-base | Diagnostic |
| `Sheet1` | Acid-base problem decision tree | Diagnostic |
| `Sedatieve medicatie` | Sedation step-protocol dosing (fenobarbital, chloralhydraat, rivotril, morfine) | Medication |

---

## 1. Medication Dosing

| # | Workbook Function | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 1.1 | Weight-based dose: mg/kg/day → total daily dose, dose/administration, drank (ml) | **Fit** | Prescribe | Core discontinuous dosing (AP §3.5, §3.6) |
| 1.2 | Reverse: given amount → mg/kg/day back-calculation | **Fit** | Prescribe | Solver is bidirectional |
| 1.3 | Continuous infusion: concentration (mg in ml) + pump rate → dose | **Fit** | Prescribe | GenSOLVER bidirectional (AP §1.6, §2.3) |
| 1.4 | Multi-unit rate display: dose in mg **and** mcg (×10⁻³) **and** ng (×10⁻⁶), per kg per min/hr/day | **Partial** | Prescribe | Matches AP §1.11 (dual Unit/kg/hr + /kg/min). Workbook holds the exact conversion formula; simultaneous multi-unit display not confirmed in GenPRES |
| 1.5 | Combination product (augmentin: amoxicilline + clavulaanzuur, fixed ratio), dosed on the amoxicilline component | **Fit** | Prescribe | Multi-component orders with per-substance limits (AP §3.18) |
| 1.6 | Prostin (alprostadil) ng/kg/min infusion | **Fit** | Prescribe | Continuous dose type; unit handled by GenUNITS |
| 1.7 | Sedation step protocol (fenobarbital, chloralhydraat, rivotril, morfine): mg/kg/day, frequency, keerdosering | **Fit** | Prescribe | Rule-driven discontinuous dosing incl. PRN-style "zo nodig" text (PRN toggle itself is AP §3.11 **Gap**) |
| 1.8 | Vitamine D deficiency dose: `40 × (streef − serum) × gewicht` | **Gap** | — | Formula-based loading-dose calculator; no equivalent GenPRES rule authored |
| 1.9 | Midazolam → lorazepam conversion (¼ of daily mida dose, in 3–4 dd) | **Gap** | — | Cross-drug equivalence conversion; not modeled |
| 1.10 | Corticosteroid taper schedule (week 1–8, mg/m²/day → mg/day → ochtend/middag/avond) + stress-dose schema + steroid equivalence (dexamethason/prednison/HC) | **Gap** | — | Multi-week scheduled-taper dosing and inter-steroid equivalence; no roadmap coverage. Related to AP §9.15 (order start/stop) for scheduling |
| 1.11 | Half-life extrapolation (concentration at t=±x from t₀ and half-life) | **Out of scope** | — | Pharmacokinetic estimate, not an order |

## 2. Electrolyte & Acid-Base Correction (Suppletion)

Acute-correction dosing. Adjacent to the APLS/emergency category (AP §8) but not itemized in the AP2019 roadmap.

| # | Workbook Function | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 2.1 | Kalium suppletion (0.5 mmol/kg in 1 hr; mmol → ml KCl 7,4% diluted, central iv) | **Gap** | — | Weight-based electrolyte-correction order with preparation instruction |
| 2.2 | Magnesium suppletion (0.08 mmol/kg in 30 min; MgCl 10% = 0,5 mmol/ml, 5× diluted glucose 5%) | **Gap** | — | As above |
| 2.3 | Sodium bicarbonate correction from base excess (weight × BE → ml NaBic 8,4% / 4,2%) | **Gap** | — | BE-driven correction dose |
| 2.4 | Sodium deficit (fractie ECV × weight × (streef − gemeten Na)) → absolute / per kg / as NaCl 2,9% infusion rate | **Gap** | — | Deficit-based dosing with infusion-rate output |
| 2.5 | Kalium-in-infuus (target K mmol/kg/day added to base infusion of known volume) | **Partial** | Nutrition/Prescribe | Fluid-additive to a base infusion; expressible via solution rules but not a dedicated calculator |

## 3. Fluids & Rehydration

| # | Workbook Function | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 3.1 | Maintenance fluid (Holliday-Segar: 150 ml/kg to 10 kg, +50, +20) per 24/48 hr and per 1/4/6/8/10 hr | **Partial** | — | Fluid targets expressible as dose rules (ml/kg/day); explicit maintenance-schedule breakdown not surfaced. Related to AP §2.8 |
| 3.2 | Rehydration over 4/6/8/10/24/48 hr → ml/hr | **Gap** | — | Deficit-replacement scheduling; not modeled |
| 3.3 | Combined deficit + maintenance total → ml/hr | **Gap** | — | As above |
| 3.4 | Dehydration % → fluid deficit (ml); normal (pre-dehydration) weight from current weight | **Partial** | — | Deficit maths; weight-back-calc not present |

## 4. Renal Function

| # | Workbook Function | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 4.1 | **eGFR Schwartz** (serum creatinine + length) | **Gap** | — | Directly matches AP §7.12 (**Gap**: Schwartz < 50 kg). Workbook has the exact formula — a reference implementation |
| 4.2 | GFR from timed urine collection (volume, time, plasma/urine creatinine, BSA) | **Gap** | — | Clearance from urine collection; not modeled |
| 4.3 | Fractional sodium excretion (%) and tubular phosphate resorption (%) | **Out of scope** | — | Diagnostic indices, not dosing |
| 4.4 | Albumin deficit (weight, serum albumin, target, Ht) | **Gap** | — | Deficit-replacement calc, borders §2 suppletion |

## 5. Nutrition & Intake Totals

| # | Workbook Function | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 5.1 | Enteral feed composition table (Moedermelk, Nutrilon, Nutrini, Nutrison, Monogen, Energivit, ORS): kcal/KH/eiwit/vet/Na/K per 100 ml | **Fit** | Nutrition | EnteralFeeding generics with composition (AP §6.1) |
| 5.2 | Parenteral fluid composition (Glucose 5/10/15/50%, Ringer, NaCl variants, TPN) with rate (loopsnelheid ml/hr) | **Fit** | Nutrition | Parenteral generics + rate control (AP §5.x) |
| 5.3 | Intake totals: fluid ml/kg/day + /hr, calories kcal/kg/day, protein, fat, Na, K per kg/day — split enteraal/parenteraal | **Fit** | Totals | 17-metric totals (AP §7.1–7.7, §7.11) |
| 5.4 | Glucose infusion rate **mg/kg/min** (KH mg/kg/min) | **Partial** | Totals | Matches AP §7.3 (**Partial**: mg/kg/min display not confirmed). Workbook has the formula |
| 5.5 | Neonatal increasing fluid schedule: reference ml/kg/day by day-of-life (day 1 → 8+: 40 → 160+ ml/kg/day) | **Partial** | — | Matches AP §2.8 / §7.8 (**Partial**). Workbook has the concrete day-of-life ladder |
| 5.6 | Fantomalt / modular supplement (schepje = 5 g = 5 g KH) added to feed composition | **Fit** | Nutrition | EnteralSupplement (AP §6.2) |

## 6. Patient-Derived Values

| # | Workbook Function | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 6.1 | Gestational age (AD), EDD, LNMP, corrected age | **Fit** | Patient | GA/PMA/corrected age computed (AP §9.3) |
| 6.2 | BSA (DuBois, from length + weight) | **Fit** | Patient | BSA computed (AP §9.3) |
| 6.3 | Weight difference (absolute + relative) between two measurements | **Partial** | Patient | Single current weight held; two-point diff not surfaced |
| 6.4 | Neonatal growth velocity (g/kg/day, per week/day, two-date) | **Gap** | — | Growth-trend calc; no longitudinal weight history (related to AP §9.6 persistence) |

## 7. Out of Scope (Diagnostic Tools)

Consistent with the AP2019 analysis exclusions (labs, scores). Not gaps — deliberately outside GenPRES's prescribing scope.

| # | Workbook Function | Status |
|---|---|---|
| 7.1 | Clinical scores: GCS/EMV, Westley croup, asthma score, PRAM, SBAR | **Out of scope** |
| 7.2 | Sensitivity / specificity / PPV / NPV epidemiology | **Out of scope** |
| 7.3 | Respiratory: oxygenation index, PF ratio, aaDO₂, expected AMV / tidal volume | **Out of scope** |
| 7.4 | Stewart acid-base (BE partitioning) + acid-base decision tree | **Out of scope** |
| 7.5 | Bone-age height prediction (Greulich-Pyle), target height, target range | **Out of scope** |
| 7.6 | Alcohol promillage / consumed-volume estimation | **Out of scope** |

---

## Summary

| Status | Count | Percentage |
|---|---|---|
| **Fit** | 12 | 30% |
| **Partial** | 7 | 17% |
| **Gap** | 13 | 33% |
| **Out of scope** | 8 | 20% |
| **Total** | 40 | 100% |

Restricting to the 32 **in-scope** items (excluding the 8 out-of-scope items): 12 Fit / 7 Partial / 13 Gap.

### GenPRES already replaces (Fit)

The workbook's **core dosing and intake** calculators are already covered: weight-based and reverse dosing, bidirectional infusion concentration/rate, combination products, enteral/parenteral composition tables, full intake totals, and all patient-derived values (age variants, BSA). A clinician using GenPRES no longer needs these workbook sheets — and gains validation, constraint safety, and traceability the spreadsheet cannot provide.

### GenPRES nearly replaces (Partial — workbook provides reference formulas)

Several workbook calculators match roadmap **Partials** and can be lifted as concrete specifications or test oracles:

- **Multi-unit continuous-rate display** (mg / mcg / ng per kg per min/hr/day) → AP §1.11
- **Glucose infusion rate mg/kg/min** → AP §7.3
- **Neonatal day-of-life fluid ladder** (ml/kg/day) → AP §2.8 / §7.8
- **Maintenance/rehydration fluid scheduling** and **kalium-in-infuus** additive

### GenPRES does not yet replace (Gap — candidates for new roadmap rows)

The workbook exposes dosing patterns absent from both GenPRES and the AP2019 roadmap. These are the strongest arguments for extending GenPRES to fully retire local tooling:

- **eGFR Schwartz** (already AP §7.12 Gap — workbook has the formula)
- **Electrolyte & acid-base correction dosing**: K, Mg, Na deficit, NaBic-from-BE, albumin deficit — weight/deficit-based orders with preparation instructions
- **Corticosteroid taper schedule + stress dosing + steroid equivalence** — multi-week scheduled dosing
- **Loading-dose calculators**: vitamin D deficiency; cross-drug equivalence (midazolam → lorazepam)
- **Growth velocity** (g/kg/day) — needs longitudinal weight persistence (AP §9.6)

### Why replacing this tooling matters

These workbooks are **unversioned, untested shadow tooling**: formulas copied between clinicians, no audit trail, no validation, no MDR traceability. Every function GenPRES absorbs removes a silent single point of failure. The Fit column shows GenPRES already retires roughly a third of this workbook outright; the Partial and Gap columns are a prioritized backlog for retiring the rest.

---

## References

- [Fit-Gap Analysis: AfsprakenProgramma 2019 vs GenPRES](fit-gap-ap2019-vs-genpres.md) — the companion analysis against the official departmental application
- Source workbook: `berekingenKoop.xls` (clinician personal calculation workbook, PICU/NICU WKZ)
