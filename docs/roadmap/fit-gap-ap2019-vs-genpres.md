# Fit-Gap Analysis: AfsprakenProgramma 2019 vs GenPRES

## Context

The AfsprakenProgramma 2019 (AP2019) is the PICU/NICU clinical workflow application at UMC Utrecht / WKZ, launched November 2019 in its current state. This analysis maps its medication, feeding, and fluids requirements against the current GenPRES Prescribe, Nutrition, and TreatmentPlan views to identify what GenPRES already covers, what partially exists, and what is missing.

**Revisions**: Items 5.4, 5.5, 5.7, 5.10, 5.11, 9.4, 9.8 and 9.10 were corrected against the *UC TPN prescribing* walkthrough (`docs/scenarios/tpn-prescribing/UC TPN prescribing.pdf`, a local file not tracked in git) and the use cases in `docs/scenarios/`. Each either understated what GenPRES does, or measured it against a mis-stated AP2019 requirement, or recorded as a shortfall something the use cases make a deliberate non-goal. Rows 5.17, 5.18 and section 11 were added.

**Status values**: **Fit**, **Partial** and **Gap** measure GenPRES against an AP2019 requirement and are counted in the summary. **Out of scope** marks an AP2019 capability GenPRES deliberately does not reproduce, with the reason given; these are excluded from the counts, as is section 11.

**Scope**: This analysis covers medication, fluid, and enteral-feeding functionality, plus the cross-cutting workflow and user/access-management concerns that surround them. AP2019 features outside GenPRES's intended scope — intravascular lines & pacemaker, lab requests, and other appointments/controls — are deliberately excluded.

**View column**: names the GenPRES view where the requirement is surfaced. `—` means it is not surfaced in any view (a gap, backend-only, external, or an integration concern).

---

## 1. Continuous Medication (PICU)

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 1.1 | Standard-solution-based prescribing (concentration + rate) | **Fit** | Prescribe | GenPRES supports continuous dose type with rate-based dosing and solution rules |
| 1.2 | Weight-based AND absolute dose display | **Fit** | Prescribe | Adjusted dose (per kg/m²) and absolute dose both shown in scenario details |
| 1.3 | Manual entry: generic name, syringe amount, unit, total volume, dosing unit | **Fit** | Prescribe | Generic/route/form selection present; syringe-specific fields (syringe volume, total syringe amount) are mapped to component/orderable quantities |
| 1.4 | Epidural solution protocols (weight-based volumes, pump settings) | **Fit** | Prescribe | Implemented |
| 1.5 | Dosing monitoring signals (red/yellow/blue) | **Fit** | Prescribe | Severity color coding: Valid/Caution/Warning/Alert maps to AP2019's blue/yellow/red |
| 1.6 | Concentration-based calculation from desired dose + rate | **Fit** | Prescribe | GenSOLVER bidirectional calculation supports deriving concentration from dose+rate |
| 1.7 | Quick-pick from department continuous-med formulary | **Fit** | Prescribe | Generic/indication selection is the formulary; dose rules define the department set |
| 1.8 | Automatic standard-concentration and standard-volume selection per drug (default NaCl 0.9%) | **Fit** | Prescribe | Solution rules encode standard volume/concentration and default diluent per drug |
| 1.9 | Solvent/diluent conflict checking (none/NaCl/glucose; drug-specific volume caps) | **Fit** | Prescribe | Diluent selection validated against solution rules; volume caps encoded as constraints |
| 1.10 | Amount snapped to multiple of strength + min/max concentration clamp | **Fit** | Prescribe | Divisibility/increment handled by constraint solver; concentration bounds from solution rules |
| 1.11 | PICU non-standard meds shown in both Unit/kg/hour and Unit/kg/min | **Partial** | Prescribe | Rate adjustable per time unit; simultaneous dual-unit display not confirmed |
| 1.12 | Special cases: epinephrine (no auto-dose), doxapram (max-conc based) | **Partial** | Prescribe | Generic per-drug overrides possible via rules; not verified as dedicated behavior |

## 2. Continuous Medication (NICU)

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 2.1 | Infusion letter (infuusbrief) — integrated continuous med overview | **Partial** | — | No infusion letter concept; GenPRES has a treatment plan, but no preparation print out |
| 2.2 | Pre-configured syringe volume (12 ml), solution fluid (glucose), rate (0.5 ml/hr) | **Fit** | Prescribe | Dose rules can encode NICU-specific syringe presets with a fixed drip rate |
| 2.3 | Dosing from desired dose + rate → concentration calculation | **Fit** | Prescribe | Bidirectional solver handles this |
| 2.4 | Divisibility and original medication concentration checks | **Fit** | Prescribe | Constraint system validates ranges; explicit divisibility UI not present |
| 2.5 | NICU epidural protocol (single formula) | **Fit** | Prescribe | Same as 1.4 |
| 2.6 | NICU monitoring: rate deviation, solution fluid, volume, max concentration | **Partial** | Prescribe | Constraint violations shown as warnings; specific "blue = deviation from standard" not distinguished from errors |
| 2.7 | NICU fluid side-lines (glucose, NaCl, sodium bicarbonate, albumin) as fluid-balance contributors | **Gap** | — | No dedicated side-line orders feeding fluid totals (the arterial line itself is out of scope) |
| 2.8 | NICU age/gestation-based increasing fluid-intake advice (per kg/day, manual override) | **Partial** | — | Expressible as dose rules — Patient Category supports gestational/postmenstrual/postnatal-age bands and ml/kg/day dose limits — but fluid-advice rules are not authored and no intake-vs-advice comparison is surfaced |
| 2.9 | Phototherapy / glucose fluid correction (higher fluid need under phototherapy) | **Partial** | — | Expressible as a dose-rule variant — a distinct dose type carrying the dose text "phototherapy" (e.g. `Discontinuous "phototherapy"`) can hold different fluid/glucose limits than plain discontinuous — but no phototherapy rule is authored and intake is not auto-adjusted on TPN glucose change |

## 3. Discontinuous Medication

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 3.1 | Generic selection via autocomplete | **Fit** | Prescribe | Autocomplete for generic name (desktop), dropdown (mobile) |
| 3.2 | Form/route selection | **Fit** | Prescribe | Route and pharmaceutical form selection present |
| 3.3 | Indication selection | **Fit** | Prescribe | Indication filter available |
| 3.4 | Frequency selection | **Fit** | Prescribe | Frequency navigation with min/median/max controls |
| 3.5 | Correction type (none, weight, BSA, per dose) | **Fit** | Prescribe | Adjustment unit (kg, m²) built into dose rules |
| 3.6 | Dose per administration with min/max limits | **Fit** | Prescribe | Dose quantity navigation with min/max constraints |
| 3.7 | Dosing help: G-Standaard, Kinderformularium, FTK, Handboek Parenteralia | **Fit** | Formulary | Rules sourced from Kinderformularium/FTK (Parenteralia for solutions); Formulary view renders clickable deep links to Kinderformularium (by drug id) and Farmacotherapeutisch Kompas (by generic); G-Standaard shown as text, no deep link for G-Standaard/Parenteralia |
| 3.8 | Medication without dosing (ointments, eye drops — frequency only) | **Fit** | Prescribe | Can be prescribed in terms of "keer" |
| 3.9 | Medication in solution (pre-configured solution amount, fluid, infusion time) | **Fit** | Prescribe | Solution rules + diluent selection + preparation display |
| 3.10 | Multiple active substances (e.g., cotrimoxazol) | **Fit** | Prescribe | Multi-component orders with per-item dose calculations |
| 3.11 | PRN (zo nodig) medication | **Gap** | — | No PRN toggle; a "max frequency" is possible; no PRN comments |
| 3.12 | Comments/remarks per medication | **Gap** | — | No free-text comment field on orders |
| 3.13 | Sorting by indication | **Partial** | TreatmentPlan | Table sortable by medication and route; no indication column |
| 3.14 | TallMan lettering for look-alike medications | **Gap** | — | No TallMan capitalization in medication names, but not really needed as all orders are indication driven |
| 3.15 | Substance divisibility display | **Fit** | Prescribe | Divisibility is shown through the available quantity options — form/component and substance quantities are presented and navigated in valid divisible increments |
| 3.16 | Per-dose vs cumulative dose toggle; non-daily frequency dosing (per 36 h, per 2 days) | **Fit** | Prescribe | FreqUnit models intervals; per-time and per-administration dose both derived by solver |
| 3.17 | Non-assortment / study drug manual entry (free-text generic + indication) | **Gap** | — | Prescribing is rule/formulary-driven; no free-text off-formulary order path |
| 3.18 | Combination parenteral products (two substances, fixed ratio) dosed and monitored unambiguously across both components — AP2019 did this by summing to milligrams and renaming, e.g. "Piperacilline + tazobactam 4500 mg (4000+500)" | **Fit** | Prescribe | GenPRES shows both the pharmaceutical-form/component quantity (vial/mL) AND each included substance's quantity with its own per-substance dose limits, so both components are dosed and monitored explicitly. The AP2019 summed-grams-plus-rename was a workaround for MetaVision's single-quantity order model; the safety intent is met here without it (a single summed-grams figure is not produced, and is not needed) |
| 3.19 | Dose calculation switchable off per order (e.g., ointments) | **Fit** | Prescribe | Orders can be prescribed frequency-only without dose calculation |

## 4. Discontinuous Medication Monitoring

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 4.1 | Yellow: underdosing / unchecked | **Fit** | Prescribe | Caution severity level |
| 4.2 | Red: overdosing / exceeds max | **Fit** | Prescribe | Alert severity level |
| 4.3 | Red: concentration exceeds max or disallowed solution | **Fit** | Prescribe | Constraint violations shown |
| 4.4 | Red: infusion time faster than minimum | **Fit** | Prescribe | Rate/time constraints enforced |
| 4.5 | Blue: non-standard frequency | **Fit** | Prescribe | There is a distinct "informational" severity separate from caution |
| 4.6 | ±10% tolerance rule when no min/max dose is configured | **Fit** | Prescribe | When min adjusted dose = max adjusted dose (a single norm dose), GenORDER expands it to a ±10% band (norm×0.9 … norm×1.1) and renders it back as the norm value |
| 4.7 | Unit-aware surveillance: auto unit/frequency conversion (mg↔mcg, 2×/3d ⇄ 1×/36h) | **Fit** | Prescribe | GenUNITS + solver compute resultant units and convert frequency intervals |

## 5. TPN / Parenteral Nutrition

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 5.1 | Independent protein solution selection (weight-dependent) | **Fit** | Nutrition | TPN context with generic selection; solution rules weight-dependent |
| 5.2 | Independent infusion rate control | **Fit** | Nutrition | Rate navigation with min/median/max |
| 5.3 | Independent volume control | **Fit** | Nutrition | Component quantity navigation |
| 5.4 | Up to 4 parenteral infusions (protein + Mg/Ca + lipid + electrolyte) | **Fit** | Nutrition | Corrected against the *UC TPN prescribing* walkthrough: AP2019 allows four fixed infusion slots, not five. GenPRES covers them with a different shape — one TPN line, one Lipid line and any number of ElectrolyteGlucose lines, with Mg/Ca and phosphate available as electrolyte compositions rather than as their own categories. The absent slot cap follows from that shape and is not a shortfall |
| 5.5 | CVL vs peripheral access (affects electrolyte dilution) | **Partial** | Prescribe | Access type is captured in patient context (explicit CVL toggle in the Patient view) and solution rules *can* encode access-dependent constraints. Unconfirmed is the part that matters: that the recorded access actually selects which solution constraints apply, peripheral bounding concentrations tighter than central. See precondition 4 of [UC-GENPRES-004](../scenarios/UC-GENPRES-004.md) |
| 5.6 | Electrolyte entry with arrow keys / click input | **Fit** | Nutrition | Component quantity controls with increase/decrease navigation |
| 5.7 | Pump stand calculation (24-hour) | **Partial** | Nutrition | Volume, rate and the resulting administration time are computed and shown per line, so the calculation exists; missing is the one-click "make this line run over its intended period". Do not implement by copying AP2019's fixed 24 hours — in GenPRES the intended period belongs to the dose rule for the stage (see [UC-GENPRES-004](../scenarios/UC-GENPRES-004.md)) |
| 5.8 | TPN Day Choice (automatic protocol progression) | **Fit** | Nutrition | Day-based TPN protocol escalation |
| 5.9 | Decoupled volume and rate (independent of 24-hour timeline) | **Fit** | Nutrition | Volume and rate are independently navigable |
| 5.10 | Infusion time > 24h warning | **Partial** | Nutrition | Administration time is derived from volume and rate, displayed per line, and flagged by dose-check severity when it falls outside what the rules allow. Missing is an intended administration period on the dose rule to judge it against; until then the flag has no reference to fire on |
| 5.11a | Preparation print for TPN letter | **Fit** | Nutrition | Parenteral print view: per line each component with prepared volume and dose per kg, total volume, pump rate, administration time, intake totals with reference ranges, patient header and prescriber signature line |
| 5.11b | Pharmacy mail / electronic hand-off of the TPN letter | **Gap** | — | The print is a paper hand-off only; nothing is sent to the pharmacy electronically. See [UC-GENPRES-003](../scenarios/UC-GENPRES-003.md) |
| 5.12 | "Extra" exclusion (feeds/lines/meds marked extra excluded from totals) | **Gap** | — | No per-order "extra"/exclude-from-balance flag |
| 5.13 | TPN rest-volume (glucose base) auto-formula from total fluid intake minus other sources | **Partial** | Nutrition | Base-fluid volume derived by solver; automatic negative-balance red flag not confirmed |
| 5.14 | Minimum solvent-volume floor (blocks lowering rate below concentration minimums) | **Fit** | Nutrition | Solution rules bound each component/substance (MinQty/MaxQty, MinConc/**MaxConc**, MinVol/MaxVol); a per-substance max concentration forces volume ≥ amount/maxConc, so the solver won't lower volume/rate past the floor |
| 5.15 | Non-glucose solvent zeroes protein composition | **Gap** | — | No rule coupling solvent choice to protein availability |
| 5.16 | Selectable lipid composition (incl. SMOF), weight-band boundary resolution | **Partial** | Nutrition | Lipid generic/solution selectable; weight-band boundary-to-higher rule not confirmed |
| 5.17 | Remove an individual component from an infusion (principal / solvent / additional roles) | **Gap** | — | AP2019 removes components inside a solution by check box, refusing the principal component and any solvent a maximum concentration depends on. GenPRES removes whole lines only; the three component roles are not represented. See the component-removal extension in [UC-GENPRES-004](../scenarios/UC-GENPRES-004.md) |
| 5.18 | Reset quantities/rates to their standard values | **Fit** | Nutrition | AP2019 resets per value ("stand" per quantity or rate); GenPRES resets a whole line back to the ranges its rules originally allowed |

## 6. Enteral Nutrition / Feeding

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 6.1 | Enteral feeding selection | **Fit** | Nutrition | EnteralFeeding category with generic selection |
| 6.2 | Supplement management (multiple) | **Fit** | Nutrition | EnteralSupplement category, multiple instances allowed |
| 6.3 | Safety: removing feed warns about dependent supplements | **Fit** | Nutrition | Confirmation dialog implemented |
| 6.4 | Frequency and dose per administration | **Fit** | Nutrition | Frequency + dose quantity controls for discontinuous/timed |
| 6.5 | Continuous enteral feeding (rate-based) | **Fit** | Nutrition | Continuous dose type with rate navigation |

## 7. Totals / Intake Calculation

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 7.1 | Fluid (ml/kg/day) | **Fit** | Totals | Volume tracked |
| 7.2 | Energy (kCal/kg/day) | **Fit** | Totals | Energy tracked |
| 7.3 | Glucose (g/kg/day and mg/kg/min) | **Partial** | Totals | Carbohydrate is tracked and the preparation print already shows its reference range in mg/kg/min — but the value beside that range is in g/kg/day. Both belong (prescribed per day, judged as an infusion rate); missing is the value in mg/kg/min, not a change to the range |
| 7.4 | Protein (g/kg/day) | **Fit** | Totals | Protein tracked |
| 7.5 | Vitamin D (IE/day) | **Fit** | Totals | VitaminD tracked |
| 7.6 | Iron (mmol/kg/day) | **Fit** | Totals | Iron tracked |
| 7.7 | Na, K, Cl, Ca, Mg, PO₄ (mmol/kg/day) | **Fit** | Totals | All 6 electrolytes tracked |
| 7.8 | Age/weight-appropriate intake recommendations (neonates also need gestational + PM age) | **Partial** | Totals | Weight- and chronological-age-banded per-nutrient reference ranges are implemented (min/max per kg/day) and drive the displayed norm and warning levels; the reference table has no gestational-age or postmenstrual-age bands, so neonatal recommendations cannot be discriminated |
| 7.9 | Relevant lab values alongside totals | **Gap** | — | No lab data integration in GenPRES |
| 7.10 | Totals across ALL orders (nutrition + continuous + discontinuous) | **Partial** | Totals | Nutrition and TreatmentPlan each show own totals; cross-view aggregation should happen |
| 7.11 | Fat (g/kg/day) | **Fit** | Totals | Fat tracked |
| 7.12 | Automatic eGFR calculation (Schwartz < 50 kg, MDRD otherwise) | **Gap** | — | No eGFR computation from lab creatinine |
| 7.13 | Acute Kidney Injury alert (creatinine rise / low diuresis) | **Gap** | — | No AKI surveillance |

## 8. Acute / Emergency Medication (APLS)

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 8.1 | Weight-based emergency drug and intervention calculations ("Acuut Blad") | **Fit** | LifeSupport | Emergency List view: per-patient calculated dose, preparation, and advice by category/intervention |
| 8.2 | Printable acute sheet | **Fit** | LifeSupport | Print dialog with patient header + signature; also hospital-filtered and read-aloud |

## 9. Cross-Cutting / Workflow

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 9.1 | Patient data management (demographics, weight, age) | **Fit** | All | Patient context with age, weight, BSA, GA, PMA, gender |
| 9.2 | Live patient-field validation ranges (weight, length, gestation, birth weight, date logic) | **Partial** | Patient | Some validation present; full clinical range/date-consistency rules not confirmed |
| 9.3 | Derived clinical values (chronological/gestational/postmenstrual/corrected age, BSA) | **Fit** | Patient | Age variants, BSA, GA, PMA computed |
| 9.4 | Patient list picker (admitted patients of current department) | **Out of scope** | — | Deliberate non-goal: the patient is chosen in the main application and arrives in the launch record, so GenPRES never lists or browses patients — see [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md) steps 1-2 |
| 9.5 | Clear scopes (PICU-only / NICU-only / patient-only / everything) | **Gap** | — | No scoped clear/reset |
| 9.6 | Version control / save history | **Gap** | — | No patient-data versioning in GenPRES, patient data and active orders should be persistable. Intended shape: GenPRES saves to its own store, the source of truth for what GenPRES produced; replication onward to the Hospital Patient Data Platform is provided by the hospital and out of GenPRES's scope — see [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md) step 7 and its data-ownership section |
| 9.7 | Multi-user conflict detection (patient open elsewhere) | **Gap** | — | No concurrent access detection |
| 9.8a | MetaVision data sync (bidirectional) | **Out of scope** | — | Deliberate non-goal, not a shortfall: [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md) makes the launch record the only channel between the two systems — written by MetaVision, read once by GenPRES, never called back into. Patient data comes from the Hospital Patient Data Platform instead |
| 9.8b | Electronic signing of orders | **Gap** | — | Signing lived MetaVision-side in AP2019. GenPRES has no per-order signed state; tracked jointly with 10.3 |
| 9.8c | Order file export | **Gap** | — | No order export exists. The nearest intended path is the pharmacy hand-off in [UC-GENPRES-003](../scenarios/UC-GENPRES-003.md) and 5.11b |
| 9.9 | HIX medication import | **Gap** | — | No external system import |
| 9.10 | Pharmacy communication (email, print, VTGM preparation letters) | **Partial** | Nutrition | The parenteral preparation print exists (5.11a). Email / electronic hand-off (5.11b) and VTGM preparation letters do not |
| 9.11 | Renal function warning + dosing adjustment link | **Partial** | Prescribe | Renal rules exist in GenFORM; GFR-based dose adjustment available; no lab-driven auto-calculation |
| 9.12 | Batch delete of selected orders | **Fit** | TreatmentPlan | Multi-select + batch delete implemented |
| 9.13 | PICU vs NICU workflow separation | **Gap** | — | GenPRES has single unified workflow; no unit-specific menus |
| 9.14 | In-app formulary / knowledge-base editing (continuous med, parenteralia, discontinuous) | **Partial** | — | Rules editable via Google Sheets externally; no in-app admin editors |
| 9.15 | Order start/stop (start and stop date/time per order) | **Partial** | — | Start/stop field exists on the order type in the backend but is not surfaced in the front end nor processed server-side |

## 10. User & Access Management

AP2019's user management was lightweight: identity and role were **trusted from MetaVision / the Windows registry** rather than verified in-app (no credential check existed in code; admin actions were gated by a hardcoded password). GenPRES has only a single shared admin password (`GENPRES_PASSWORD`) that gates admin operations, producing a boolean authenticated state with no user identity, role, or audit binding.

| # | AP2019 Requirement | GenPRES Status | View | Notes |
|---|---|---|---|---|
| 10.1 | Per-user identity (named clinician: login, first/last name) | **Gap** | — | AP2019 carried a user object (`ClassUser`: Login, FirstName, LastName, Role, PIN) sourced from MetaVision. GenPRES has only a shared admin password; `IsAuthenticated` is a bare boolean with no individual identity. Intended mechanism: identity arrives in the single-use launch record the main application writes, and is authoritative for the session — see [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md) steps 1-2 |
| 10.2 | Role-based authorization (Beheerders / Apotheek / Prescriber) | **Gap** | — | AP2019 role-gated ribbon groups by user type (`GetVisibleAdmin`, `GetVisibleDevelopment`). GenPRES authorization is binary (admin password or not); no role tiers to gate features. Intended mechanism: the role travels in the launch record alongside the identity and governs prescribing, saving and pharmacy dispatch — see [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md) step 2 |
| 10.3 | Prescriber registry + electronic signing (PIN, Signed state) | **Gap** | — | AP2019 had a `Prescriber` table, `GetPrescriberPIN`, and `Signed` bit columns; signing itself lived MetaVision-side. GenPRES has no prescriber registry, PIN, or per-order signed state. Tracked jointly with 9.8b |
| 10.4 | Per-user audit trail (who viewed which patient, what changed) | **Gap** | — | AP2019 logged action + user login + hospital number (IGJ/GMP traceability). GenPRES logs server activity but not per clinical user; an MDR-relevant traceability gap. Related to 9.6. [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md) requires logging every launch-token issue, redemption and rejection, and intends the same for a deliberate out-of-limit value (confirmation, reason, attribution) |
| 10.5 | EHR-sourced login provenance (MetaVision / registry) | **Gap** | — | AP2019 derived identity and role from MetaVision (`Users` ⨝ `t_UsersType`) and registry `HKCU\SOFTWARE\UMCU\MV`. GenPRES has no EHR identity source. **The intended design does not reproduce this**: GenPRES reads neither MetaVision nor the registry. Identity is handed over in the launch record and verified by redeeming it, so this row closes by implementing [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md), not by adding an EHR lookup |

## 11. GenPRES-specific (no AP2019 equivalent)

Behaviour the use cases require that AP2019 never had, so there is nothing to
be "Fit" against. Status here is simply **Built** or **Not built**, and these
rows are excluded from the summary counts.

| # | Requirement | Status | View | Notes |
|---|---|---|---|---|
| 11.1 | Launch-token handoff: identity, role and patient established by redeeming a single-use record | **Not built** | — | Replaces AP2019's registry/MetaVision identity lookup (10.5) and the URL-carried context. See [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md) steps 1-2 |
| 11.2 | Patient data retrieved from the Hospital Patient Data Platform, which is authoritative for it | **Not built** | — | GenPRES retrieves and never writes back; unreachable platform blocks prescribing rather than falling back to a cached copy. See [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md) step 4 and its data-ownership section |
| 11.3 | GenPRES's own store as source of truth for orders, replicated onward by the hospital | **Not built** | — | Saving completes when GenPRES's store has it; replication is provided outside GenPRES, so the platform's copy is eventually consistent and never ahead. See [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md) step 7 |
| 11.4 | Deliberate out-of-limit value confirmed, reasoned and recorded against the order | **Not built** | — | Today an out-of-limit value is flagged by dose-check severity only, and severity is recomputed each solve rather than stored. See the dose-check extension in [UC-GENPRES-001](../scenarios/UC-GENPRES-001.md) |
| 11.5 | Stand-alone calculation without patient context (manually entered patient, nothing persisted) | **Built** | All | Currently the only mode that exists, since no launch token or persistence is implemented yet. See [UC-GENPRES-002](../scenarios/UC-GENPRES-002.md) |
| 11.6 | Intake total flagged when it falls outside its reference range | **Not built** | Totals | Ranges are shown beside each total but nothing marks a total that leaves its range. AP2019 shows an advice column without flagging either. See step 8 of [UC-GENPRES-004](../scenarios/UC-GENPRES-004.md) |

---


---

## Summary

| Status | Count | Percentage |
|---|---|---|
| **Fit** | 62 | 58% |
| **Partial** | 20 | 19% |
| **Gap** | 24 | 23% |
| **Total** | 106 | 100% |

### Strengths (Fit)

GenPRES covers the core prescribing workflow well: generic/indication/route/form selection, constraint-based dosing with min/max navigation, continuous and discontinuous dose types, multi-component orders (showing both the pharmaceutical-form/component quantity and each included substance's quantity with per-substance limits), solution/preparation rules, severity-based monitoring, enteral + parenteral nutrition categories, and comprehensive intake totals (17 metrics including excipient warnings). It also provides a dedicated, printable weight-based **Emergency List (Noodlijst / APLS)** covering AP2019's acute sheet.

### Partial Fits (require enhancement)

- Glucose display in mg/kg/min alongside g/kg/day; dual Unit/kg/hour + Unit/kg/min for non-standard continuous meds
- Indication-based sorting in TreatmentPlan
- TPN business rules: rest-volume auto-formula with negative-balance flag, lipid weight-band boundary resolution
- TPN administration period: an intended period on the dose rule per stage, so the derived administration time can be judged (5.10) and a "run over the intended period" action offered (5.7)
- Vascular access actually selecting the applicable solution constraints, not merely being recorded (5.5)
- NICU age/gestation fluid-intake advice: expressible as dose rules (patient-category bands + ml/kg/day), but not authored or surfaced against intake
- Phototherapy fluid correction: expressible as a dose-type variant (dose text "phototherapy" with distinct limits), but not authored, and intake is not auto-adjusted on glucose change
- Intake reference ranges are weight- and chronological-age-banded; extend with gestational-age and PM-age bands for neonates
- Patient-field validation ranges; renal (GFR) auto-calculation from labs
- In-app formulary/knowledge-base editing (currently external Google Sheets only)
- Order start/stop: field exists on the order type but is neither shown in the front end nor processed server-side

### Gaps (not yet implemented)

- **Workflow**: Infusion-letter preparation printout for continuous medication (treatment plan exists; print does not — the parenteral *nutrition* print does, see 5.11a), pharmacy email / electronic hand-off + VTGM letters, PICU/NICU separation
- **NICU-specific**: Fluid side-lines (glucose/NaCl/bicarb/albumin) feeding fluid totals
- **Clinical features**: PRN medication, TallMan lettering, comments/remarks, non-assortment/study drug free-text entry
- **Renal/labs**: eGFR auto-calculation (Schwartz/MDRD), AKI alert, lab data display
- **TPN rules**: "Extra" exclusion from totals, non-glucose-solvent-blocks-protein coupling, removal of individual components within an infusion (5.17). Pump stand (5.7) and infusion-time warning (5.10) are *not* listed here — both are Partial, needing only an intended administration period on the dose rule
- **Integration**: order file export (9.8c), HIX import, multi-user conflict detection. MetaVision data sync (9.8a) is Out of scope by design, not a gap
- **Persistence**: Patient data versioning and being able to save/restore patient state across sessions
- **User & access management**: per-user identity, role-based authorization (Admin/Pharmacy/Prescriber), prescriber registry + electronic signing, per-user audit trail, and EHR-sourced login — GenPRES has only a single shared admin password

---

## References

- [Fit-Gap Analysis: Clinician Calculation Workbook vs GenPRES](fit-gap-clinician-workbook-vs-genpres.md) — companion analysis mapping a clinician's personal bedside-calculation workbook (shadow tooling) against GenPRES
- AfsprakenProgramma 2019 — PICU/WKZ protocol page: <https://picuwkz.nl/protocollen/werkafspraken/afsprakenprogramma-2019/>
- AfsprakenProgramma — Functionality Overview (VBA source inventory): <https://github.com/halcwb/AfsprakenProgramma/blob/master/docs/FUNCTIONALITY.md>
