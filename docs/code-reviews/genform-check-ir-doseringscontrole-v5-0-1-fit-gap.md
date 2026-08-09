# Fit-Gap Analysis — GenPRES dose check vs. IR Doseringscontrole V-5-0-1

**Date:** 2026-06-12
**Reference:** Z-Index *Implementatierichtlijn Doseringscontrole*, IR V-5-0-1
(16-09-2025), 38 pp.
**Analyzed implementation:**

- `src/Informedica.GenFORM.Lib/Check.fs` (the Check module)
- `src/Informedica.ZIndex.Lib/Zindex.fs`, `DoseRule.fs`, `RuleFinder.fs`
  (bst640–bst643/bst649 parsing and rule selection)
- `src/Informedica.ZForm.Lib/GStand.fs`, `DoseRule.fs`, `Types.fs`
  (mapping to `Dosage`/`DoseRange`)
- `src/Informedica.GenPRES.Server/ServerApi.Services.fs` (production consumer)

**Prior work:** the code review
[genform-check-vs-ir-doseringscontrole-v5-0-1.md](genform-check-vs-ir-doseringscontrole-v5-0-1.md)
(2026-06-08) produced 9 findings (HIGH-1/2, MEDIUM-1/2/3, LOW-3/4, BUG-A/B), all
since migrated into source. This document is the complementary deliverable: a
systematic, requirement-by-requirement coverage matrix of the **full** IR
against the **current** implementation, including requirements the earlier
review did not cover (zorggroep, ICPC, gender, PRK/HPK level, BSA categories,
signal suppression).

## 1. Scope and framing

The IR describes an interactive prescription-level check: a care professional
enters one concrete dose for one patient, and the system walks a decision tree
(IR § 4.1–4.6) to either pass silently or show a numbered G-Standaard text.

**GenPRES implements a different use case: the dose check happens at the
dose-rule level, not at the prescription level.** The `Check` module validates
each GenFORM `DoseRule` — an allowed *range* over a *patient category* —
against the G-Standaard envelope ("does every dose my rule can produce stay
inside the G-Standaard limits?"). Its only production consumer is the
Formulary service (`ServerApi.Services.fs:189-192` via `checkDoseRules`,
`:113-138`): the check runs when the formulary (the operational knowledge
base) is browsed, as quality assurance of the rules themselves. It is **not**
part of the prescription workflow.

### Why rule-level validation guarantees G-Standaard compliance of prescriptions

GenPRES does not need the IR's prescription-time check because of its
constraint-based architecture (see `docs/domain/core-domain.md`, "Safety by
Construction"): prescriptions are *generated* by the constraint solver and can
only assume values that satisfy the dose-rule constraints — an order that
violates its dose rule is mathematically impossible to construct. Therefore:

> If a GenFORM dose rule passes the G-Standaard envelope check, then **every
> prescription generated from that rule also complies with the G-Standaard
> dose check** — transitively, without any prescription-time check.

This transitive guarantee is exactly as strong as the rule-level validation
itself. Where the validation compares against a *wider* reference envelope
than the IR prescribes (the Partial/Gap rows below), a non-conforming rule can
pass QA, and the guarantee is correspondingly weaker for that dimension. That
— not missed prescribe-time signals — is the correct reading of every impact
statement in this document.

Consequences for this analysis:

- IR steps that are **interactive UI flow** (ask the user for weight, show
  tekst 12 when age is unknown) belong to the prescription-level use case and
  have no counterpart in rule-level validation; they are graded
  **N/A by design** with the GenPRES equivalent noted.
- IR steps that **select** the single applicable G-Standaard record
  (zorggroep, ICPC, frequency, weight/BSA category) translate to
  **filtering/aggregating** the G-Standaard reference set before comparison.
  Where GenPRES aggregates more widely than the IR selects, the reference
  envelope becomes *wider* and the rule validation more *permissive*: a
  GenFORM rule that breaches the IR-selected limits can still pass QA. This
  weakens the transitive guarantee for that dimension and is called out per
  row.
- G-Standaard **text numbers** (tekst 1–25 via bst902/bst922) are not
  retrieved; GenPRES emits its own Dutch messages mapped to a typed
  `Check.Severity`, which the server renders as colored text blocks
  (`ServerApi.Services.fs:67-76`).

Statuses used: **Fit** (requirement covered, possibly in adapted form),
**Partial** (covered with a relevant deviation), **Gap** (not covered),
**N/A by design** (does not translate to the rule-vs-rule architecture).

## 2. Fit-gap matrix

### IR § 1.3 — What the dose check can and cannot cover

| IR § | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| 1.3.1 (1) | Min/max keerdosis per GPK base unit, with *norm* vs *absoluut* max distinction | Fit | `Check.fs:29-53` (`Severity`, `classify`), `Check.fs:659-672` (`grade`); bst649 fields parsed `ZIndex/DoseRule.fs:443-448` | Norm breach → `AdvisoryOverNorm`, absolute breach → `OverAbsolute` |
| 1.3.1 (2,3) | Check on aantal per tijdseenheid and tijdseenheid | Fit | `Check.fs:703-731` (`freqRow`) | Compares GenFORM frequency set against G-Standaard set |
| 1.3.1 (4) | Age categories in months | Fit | `ZIndex/DoseRule.fs:439`; `Check.fs:74-86`, `Check.fs:384` | Months→days at 30 d/mo before intersecting (IR 3.3) |
| 1.3.1 (5) | Weight and BSA categories | Partial | Weight: `Check.fs:390-392`; BSA: **not** intersected in `filterPatient` (`Check.fs:379-394`) and `bsa = None` passed (`Check.fs:223`) | BSA *categories* (bst643 GPDM2M/GPDM2X) never filter; BSA-*adjusted limits* (per m²) are compared, which is a different thing |
| 1.3.1 (6) | Gender allowed for product | Gap | Parsed `ZIndex/DoseRule.fs:425-428` → `Gender`; not in `RuleFinder.find` predicate (`RuleFinder.fs:117-126`); not passed by Check | Prior LOW-2, still open |
| 1.3.1 (7) | Deviating route | Partial | Route filter `RuleFinder.fs:122`; `getICPCRoute` (`ZIndex/DoseRule.fs:355-363`) | See § 4.4.3 row |
| 1.3.1 (8) | Indication (ICPC-1) where relevant | Partial | Parsed `ZIndex/DoseRule.fs:435-438`; aggregated, not selected (`Check.fs:412`) | See § 4.4.2 row |
| 1.3.1 (9) | Deviating limits for intensive care | Partial | Parsed `ZIndex/DoseRule.fs:430-433`; both groups merged (`RuleFinder.fs:119-120`) | See § 3.1/4.4.1 row |
| 1.3.2 (1,2,4) | Not checkable: alternating dose/freq, taper schedules, combination-dependent limits | N/A by design | — | GenPRES dose rules don't model these against G-Standaard either; no false coverage claimed |
| 1.3.2 (3,5,6) | Rate and duration are **outside** dose-check scope; continuous infusion stored as 1×/hour | Fit | `Check.fs:117-124` (`RateCheckMode`), default `LabelRateChecksOutOfScope` (`Check.fs:157-161`); applied `Check.fs:788-799` | Rate rows kept but tagged "[buiten G-Standaard doseringscontrole]", or droppable via config (prior HIGH-2) |

### IR § 2 — File structure, texts, units

| IR § | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| 2.1 | Use bst640, bst641, bst642, bst643, bst649 | Fit | `Zindex.fs:432` (BST640T), `:478` (BST641T), `:539` (BST642T), `:610` (BST643T), `:694` (BST649T); joined in `ZIndex/DoseRule.fs:391-450` | All five files parsed and joined per the IR key chain GPKODE→GPDBAS→GPDCAT→GPDDNR |
| 2.2 | Retrieve texts via bst902/bst922/bst601/bst415 incl. tekstsoort per discipline | N/A by design | `ServerApi.Services.fs:61-110` | GenPRES renders its own messages with typed severities; tekst numbers appear only as references inside messages (e.g. `Check.fs:109-113`) |
| 2.3 | Convert entered unit to GPK base unit before checking | Fit | `checkInRangeOf` converts test/ref to a common unit (`Check.fs:279-290`); GPK unit attached in `ZIndex/DoseRule.fs` (`gp.Unit`) | Unit-safe by construction via `ValueUnit`; a `UnitMismatch` severity exists for incomparable adjust units (`Check.fs:34`, `Check.fs:805-850`) |

### IR § 3 — General preparation

| IR § | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| 3.1 / 4.4.1 | Select limits for **one** zorggroep (alle zorg vs intensieve zorg), configured per care setting | Partial | `GPDZCO` parsed → `CareGroup` (`ZIndex/DoseRule.fs:430-433`); `RuleFinder.find` accepts `intensive` **OR** `all` (`RuleFinder.fs:119-120`) | Both care groups are merged into one envelope. Since intensive-care limits are typically *higher*, the merged max is the intensive one: a dose rule intended for first-line use that exceeds the alle-zorg maximum (but not the intensive one) passes rule validation unflagged. No configuration point exists |
| 3.2 / 4.4.2 | Use indication-specific (ICPC) limits when the indication is known; else Q algemeen; show profylaxe/therapie verbijzondering | Partial | `ICPCNR1`/`ICPCTO` parsed (`ZIndex/DoseRule.fs:435-438`, `Zindex.fs:548-566`); `matchWithZIndex` collects **all** `IndicationsDosages` (`Check.fs:412`) and `maximizeDosages` unions them (`Check.fs:297-376`) | Prior LOW-1. GenFORM rules carry an `Indication`, but it is never matched to ICPC records; the widest cross-indication envelope is used (≈ IR option 4 with intensive-care Q-algemeen semantics). ICPCTO (`Usage`) is parsed but unused in Check |
| 3.3 | Age in months, < 1 month as fraction, 1 month = 30 days | Fit | `monthsMinMaxToDays` (`Check.fs:74-86`), applied in `filterPatient` (`Check.fs:384`) | Prior MEDIUM-3, fixed |
| 3.3 | BSA computable from weight + length if absent | N/A by design | — | Patient BSA is a GenPRES-wide concern; Check never uses patient BSA at all (see § 4.5.4 gap) |
| 3.3 | Recency of weight/BSA (user-configurable, or confirm prompt) | Gap | — | App-level requirement; not implemented anywhere in the order workflow |
| 3.4 | Interchangeable time units (per 2 dagen ↔ om de dag; per 4 weken ↔ per maand; per 8 weken ↔ per 2 maanden; per half jaar ↔ per 6 maanden); per 12 weken **≠** per 3 maanden | Fit | `interchangeGroups`/`canonTimeUnit`/`interchangeable` (`Check.fs:91-105`), used in `freqRow` (`Check.fs:718`) | Prior LOW-3, fixed. Note: ZIndex additionally normalises "om de dag"→"per 2 dagen" at parse time (`ZIndex/DoseRule.fs:387`) |
| 3.4.1 | Dose ranges ('1-3× per dag', '1-2 tabletten'): unravel frequency range; check highest amount vs max, lowest vs min | Fit (by construction) | `checkInRangeOf` tests both ends of the GenFORM range against the reference (`Check.fs:268-271`) | The IR recommendation exists because the IR checks scalars; GenPRES compares ranges natively, which subsumes it |
| 3.4.2 | Missing-frequency signal suppression (suppress tekst 8/24/25 when not narrow-TI, Q algemeen, per-day frequency, and computed day dose stays under the highest allowed day dose; optionally show tekst 20) | Gap | `freqRow` always emits `FrequencyMismatch` on a non-match (`Check.fs:727-731`) | Prior INFO-2, still open. All preconditions are *available* in the data (HighRisk, frequency sets, norm max), so this is implementable inside Check |

### IR § 4.1–4.3 — Preconditions and check level

| IR § | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| 4.1 | Required inputs: GPK, age, frequency; others on demand | N/A by design | `Check.fs:198-223` | The inputs are a GenFORM `DoseRule` and the formulary filter's patient context, not an entered prescription. Missing age/weight in that context widen the G-Standaard selection instead of blocking (see § 4.2.3) |
| 4.2.1 | HPK/PRK → GPK resolution; no GPK → no monitoring, no signal | Fit (adapted) | `createDoseRulesWithMapping` resolves generic+form+route → GPKs via `GenPresProduct.filter` (`RuleFinder.fs:111-113`); base-name keying `Check.fs:408-410` | GenPRES keys on the rule's base generic name (`Generic.genericName`), not on a specific HPK; product resolution happens through the ZIndex product graph |
| 4.2.2 | No dose rules for GPK → tekst 16 (configurable per SSK/GPK-route) | Fit (adapted) | `Check.fs:37` (`NoMonitoring`); produced in `ServerApi.Services.fs:130-136` ("geen doseer bewaking gevonden…"), rendered blue (Caution) | The per-SSK/route configurability of the message is not implemented |
| 4.2.3 | Age must be known, else tekst 12 | Partial | `pat.Age` is optional; `a = None` simply removes the age filter (`Check.fs:199-208`, `RuleFinder.fs:123`) | IR blocks the check without age; GenPRES validates the rule against the all-ages envelope instead. No "age unknown" signal exists |
| 4.2.4 | Gender gate (bst640.GPDGST): wrong/unknown gender → tekst 7 | Gap | `Gender` parsed (`ZIndex/DoseRule.fs:425-428`) but never used: not in the `RuleFinder.find` predicate (`RuleFinder.fs:117-126`), and `Patient`'s gender is never passed into the G-Standaard lookup (`Check.fs:221-223`) | Prior LOW-2, still open. A dose rule whose patient category includes a gender the product is not allowed for (e.g. anticonception with an unrestricted gender) is not flagged during rule validation |
| 4.3 | Check level: prefer HPK-/PRK-specific dose records (bst641.GPDCOD = 2) over GPK records | Gap | bst641 parsed incl. `PRKODE`/`HPKODE` (`Zindex.fs:478-523`) but both are **discarded** in `DoseRule.parse`: `let gpk, _, _ = bas` (`ZIndex/DoseRule.fs:456`); rules keyed on GPK only | Where the G-Standaard differentiates limits within one GPK (per PRK/HPK), GenPRES merges them into the GPK envelope. With `maximizeDosages` this again yields the widest (most permissive) bounds |

### IR § 4.4 — Treatment setting

| IR § | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| 4.4.1 | Filter on zorggroep | Partial | See § 3.1 row | |
| 4.4.2 | Filter on ICPC (4 implementation options); fall back to Q algemeen | Partial | See § 3.2 row | |
| 4.4.3 | Filter on specific route (bst642.GPKTWG); fall back to GPKTWG = 0 | Partial | Route match `RuleFinder.fs:122` (case-insensitive on translated route names); `getICPCRoute` maps GPKTWG = 0 / "PARENTERAAL" to the GPK's own routes (`ZIndex/DoseRule.fs:355-363`); GenFORM route mapped via `routeMapping` (`Check.fs:191-195`) | Functionally close to the IR fallback: a record without specific route applies to all GPK routes. Deviation: a record **with** a specific route that doesn't match the rule's route is dropped rather than falling back to the GPKTWG = 0 record of the same set — but since all GPKTWG = 0 records are also in scope, the fallback envelope is present in the merge |

### IR § 4.5 — Category selection

| IR § | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| 4.5.1 | Select rules matching age (months); none → tekst 13 | Fit | `filterPatient` intersects age after months→days conversion (`Check.fs:379-394`); an empty match yields `dl.gstand = None` → skipped rows (`Check.fs:634-635`), surfaced as `NoMonitoring` by the server (`ServerApi.Services.fs:130-136`) | The distinct tekst 13 ("no limits for this age") vs tekst 16 ("no rules at all") granularity is collapsed into one no-monitoring signal |
| 4.5.2 | Select rules matching frequency (aantal + tijdseenheid as a combination); none → tekst 24/25/8 | Partial | `freqRow` compares the GenFORM frequency *set* against the G-Standaard *set* with `isSubset` + interchangeability (`Check.fs:703-731`); granular tekst 24 (aantal) / 25 (tijdseenheid) / 8 (both) via `freqMsg` (`Check.fs:109-113`) | Tekst granularity: Fit (prior LOW-4). Deviation: frequency does **not** filter which bst643 category's limits are compared — limits from all frequency categories are merged by `maximizeDosages`. The IR couples each frequency to its own keerdosis limits (e.g. 1×/day max 100 mg, 2×/day max 50 mg); GenPRES checks against the merged max |
| 4.5.3 | Select rules matching weight; weight unknown → tekst 10; no match → tekst 14 | Partial | Weight intersect `Check.fs:390-392`; weight unknown (`w = None`) widens instead of signalling | Range intersect: Fit. Missing-weight signal: N/A by design (rule-vs-rule); but note the IR's "tekst 10" moment also exists at limit time, see § 4.6.2 row |
| 4.5.4 | Select rules matching BSA; BSA unknown → tekst 11; no match → tekst 15 | Gap | `createDoseRulesWithMapping` passes `bsa = None` (`Check.fs:223`); `filterPatient` ignores `pdsg.Patient.BSA` (`Check.fs:379-394`); `RuleFinder` *can* filter BSA (`RuleFinder.fs:125`) but never receives one | BSA-banded rule sets (oncology dosing) are merged across all BSA categories instead of selecting the patient's band. Per-m² *limits* are still compared (unit-gated), but *category* selection per IR 4.5.4 is absent |

### IR § 4.6 — Dose limits and comparison

| IR § | Requirement | Status | Evidence | Notes |
|---|---|---|---|---|
| 4.6.1 | Limit priority: per m² → per kg → absolute (use the first present) | Fit | `pickAdjust` (`Check.fs:59-68`); applied to quantity/perTime adjust mappings (`Check.fs:536-563`) and `rateFieldsFor` (`Check.fs:602-614`) | Prior MEDIUM-1, fixed (incl. BUG-A) |
| 4.6.1.1 | Field value 0 = not filled; all nines = no upper bound | Fit | `createMinMax`: `0 → None`, all-nines → `None` (`ZIndex/DoseRule.fs:207-219`) | Also guards `max < min` data errors |
| 4.6.1.2 | Norm max = advisory bound; absolute max = hard ceiling; absolute min never filled | Fit | `classify` (`Check.fs:44-53`) and `grade` (`Check.fs:659-672`): abs ceiling checked first so inconsistent data can never downgrade an over-absolute dose | Prior MEDIUM-2, fixed. `AbsKg`/`AbsM2` min fields are parsed but, per IR, never populated by Z-Index |
| 4.6.1.3 | Configurable upper margin (e.g. 120%) for normal substances, weight/BSA dosing | Fit | `marginedTestRange` (`Check.fs:130-144`), `CheckConfig.MarginUpper` default 12/10 (`Check.fs:149-161`) | Prior HIGH-1, fixed. One-sided (max only), provider-configurable. Applied via `getNormDose` (fixed-point adjusted doses), i.e. exactly the weight/BSA-dosing case the IR targets |
| 4.6.1.4 | No margin for narrow-TI substances (GPRISC = \*) | Fit | `isRisk` branch in `marginedTestRange` (`Check.fs:133-137`); `HighRisk` from bst640.GPRISC (`ZIndex/DoseRule.fs:422`) → ZForm `Dosage.HighRisk` (`ZForm/Types.fs:103`, `ZForm/GStand.fs:477`) → OR-merged (`Check.fs:370-374`) | |
| 4.6.1.4 | For risk substances, show narrow-TI texts (tekst 1→5, 3→6) | Partial | Messages built in `grade`/`quantityChecks` etc. do not mention "smalle therapeutische breedte"; `highRisk` is available at that point (`Check.fs:564`, `Check.fs:653`) | Severity is identical; only the wording requirement is unmet |
| 4.6.1.5 | Presentation: show how far the limit is exceeded; show whether an abs max exists when norm is breached; optional abs-only checking mode | Partial | Severity → color mapping (`ServerApi.Services.fs:67-76`: Alert/Warning/Caution/Valid); messages show both ranges (`Check.fs:292-294`) | No exceedance percentage, no configurable "check abs max only" mode. The norm-vs-abs distinction itself is visible (orange vs red) |
| 4.6.2.1 / .3 / .5 | Build norm max / abs max / norm min with m²→kg→plain priority; missing weight/BSA at limit time → tekst 10/11 | Fit (adapted) | `pickAdjust` + `grade`/`gradeAdjust` with adjust-unit gating (`checkAdjustUnit`, `Check.fs:164-188`, `Check.fs:676-686`) | GenPRES compares adjusted *ranges* (per kg / per m²) directly instead of multiplying by the patient's weight/BSA, so the "weight/BSA unknown" branches don't arise; a kg-vs-m² mismatch yields an explicit `UnitMismatch` row (`Check.fs:805-850`) |
| 4.6.2.2 / .4 / .6 | Flow: dose > norm max → check abs max (tekst 3 if breached, else tekst 1); else dose < norm min → tekst 2; else silent | Fit | `classify` (`Check.fs:44-53`); `grade` decision table (`Check.fs:667-672`) | Severities: `OverAbsolute` (tekst 3 ≙ red), `AdvisoryOverNorm` (tekst 1 ≙ orange), `UnderNorm` (tekst 2 ≙ orange), `Within` (silent/green) |

## 3. Gap register

Every impact below is to be read at the **rule-validation level**: a gap means
a non-conforming GenFORM dose rule can pass formulary QA unflagged, which
weakens the transitive G-Standaard compliance guarantee for prescriptions
generated from that rule (see § 1). Ordered by impact on that guarantee. Per
repository policy, any remediation must be
prototyped in `.fsx` scripts first (e.g.
`src/Informedica.GenFORM.Lib/Scripts/CheckReview.fsx`) and migrated by the
maintainer; no code is changed by this analysis.

### G-1 — Zorggroep not selected (IR 3.1 / 4.4.1) — Partial, safety-relevant

`RuleFinder.find` merges *alle zorg* and *intensieve zorg* records
(`RuleFinder.fs:119-120`), and `maximizeDosages` keeps the widest bounds. For
substances with deliberately higher intensive-care limits, a first-line GenFORM
rule is validated against the intensive-care ceiling — a rule whose limits sit
between the two maxima passes QA, so prescriptions generated from it would
breach the alle-zorg G-Standaard check without the rule ever being flagged.
**Direction:** add a care-group parameter to the
lookup chain (`RuleFinder.Filter` → `GStand.CreateConfig` →
`Check.CheckConfig`), defaulting to the current merged behaviour for
back-compat, and let the GenPRES setting (PICU/NICU context) pick the group.

### G-2 — PRK/HPK check level discarded (IR 4.3) — Gap, safety-relevant

`DoseRule.parse` throws away `bas.PRKODE`/`bas.HPKODE`
(`ZIndex/DoseRule.fs:456`), so GPDCOD = 2 product-specific limits are merged
into the GPK envelope. Where formulation-specific limits differ within one GPK,
a rule for the stricter product is validated against bounds that are too wide.
**Direction:** carry
PRK/HPK on the ZIndex `DoseRule` (fields exist in bst641 records already
parsed) and prefer level-specific records in `matchWithZIndex` when the GenFORM
rule's products identify a PRK/HPK.

### G-3 — Gender gate absent (IR 4.2.4) — Gap

Gender is parsed but never filtered (prior LOW-2). Impact is product
applicability more than dose magnitude: a dose rule whose patient category
admits a gender the product is not allowed for is not flagged. **Direction:**
the patient context already reaches Check; filter `dr.Gender` in
`RuleFinder.find` (or post-filter in `matchWithZIndex`) and emit a dedicated
severity when the G-Standaard restricts the product to a gender the rule's
patient category does not exclude.

### G-4 — BSA categories never filter (IR 4.5.4) — Gap

`bsa = None` is passed (`Check.fs:223`) and `filterPatient` ignores BSA, so
BSA-banded rule sets (oncology) merge across bands. **Direction:** derive the
patient/category BSA range like weight (GenFORM `PatientCategory` carries a BSA
`MinMax`) and intersect `pdsg.Patient.BSA` in `filterPatient`; pass the patient
BSA into `createDoseRulesWithMapping`.

### G-5 — Indication (ICPC) aggregation (IR 3.2 / 4.4.2) — Partial, accepted divergence

All indications merge into one envelope (prior LOW-1). For substances like
methotrexaat the IR keeps oncology and reumatologie limits apart; the merged
envelope adopts the highest. The GenFORM rule has its own `Indication` text but
no ICPC coding, so exact matching needs a mapping table. **Direction (long
term):** add ICPC codes to GenFORM dose rules (sheet-contract change → the
`Data` record types in `GenFORM.Lib/Types.fs` plus the column-contract test) and
select per indication, falling back to
Q algemeen (ICPCNR1 = 17752) per IR. Until then this remains a documented
divergence; note it interacts with G-1 (the Q-algemeen semantics differ per
zorggroep).

### G-6 — Missing-frequency signal suppression (IR 3.4.2) — Gap, noise reduction

Every frequency non-match signals (`FrequencyMismatch`). The IR allows
suppression when GPRISC is empty, the indication is Q algemeen, the entered
time unit is per day, and the entered day dose stays below the highest allowed
day dose (with optional tekst 20 disclosure). All inputs (HighRisk, frequency
sets with `Units.Count.times` per time unit, norm max) are already present in
`createMapping`. **Direction:** implement the 3.4.2.1–3.4.2.5 ladder inside
`freqRow`, gated by a `CheckConfig` flag; demote a suppressed signal to an
info-level row rather than dropping it.

### G-7 — Narrow-TI wording and exceedance presentation (IR 4.6.1.4 / 4.6.1.5) — Partial, cosmetic

Risk substances get the correct (margin-free) severity but not the "smalle
therapeutische breedte" wording; messages show the ranges but not the
exceedance percentage, and there is no abs-only checking mode. **Direction:**
thread `gstand.highRisk` into the message text in `quantityChecks`/
`perTimeChecks`; optionally compute the over-limit percentage in `grade` and
add an `AbsoluteOnly` mode to `CheckConfig`.

### G-8 — Weight/BSA recency (IR 3.3) — Gap, app-level

No recency policy or confirmation prompt exists for weight/BSA anywhere in the
order workflow. Out of scope for the Check module itself; belongs to the client
patient-data entry flow.

### Lower-priority observations

- **Age unknown widens instead of signalling (IR 4.2.3):** `pat.Age = None`
  removes the age filter. In rule-vs-rule mode this means a patient-category
  rule without age bounds is checked against the all-ages envelope; consider an
  explicit `NotComparable`-style row when both sides lack age bounds.
- **Tekst 13/14/15/16 granularity:** all "no applicable reference" outcomes
  collapse into one `NoMonitoring` message; the IR distinguishes *why* nothing
  matched (age, weight, BSA, none at all). Diagnostic value only.

## 4. Status of the 2026-06-08 review findings

| Finding | IR § | Status in current source |
|---|---|---|
| HIGH-1 margin on risk substances | 4.6.1.3/4 | Fixed — `marginedTestRange` + `HighRisk` threading |
| HIGH-2 rate checks out of scope | 1.3.2 | Fixed — `RateCheckMode`, default labelled |
| MEDIUM-1 m²/kg priority inverted | 4.6.1 | Fixed — `pickAdjust` |
| MEDIUM-2 no norm/abs severity flow | 4.6.2 | Fixed — `Severity` + `classify`/`grade` |
| MEDIUM-3 months→days | 3.3 | Fixed — `monthsMinMaxToDays` |
| LOW-1 category aggregation | 4.5 | Open (accepted divergence; see G-1/G-2/G-4/G-5 for the per-dimension breakdown) |
| LOW-2 gender not passed | 4.2.4 | Open — G-3 |
| LOW-3 time-unit interchangeability | 3.4 | Fixed — `interchangeGroups` |
| LOW-4 frequency text granularity | 4.5.2 | Fixed — `freqMsg` |
| INFO-2 missing-frequency suppression | 3.4.2 | Open — G-6 |
| BUG-A / BUG-B | — | Fixed in `createMapping` / `maximizeDosages` |

## 5. Verification notes

- IR text: full-text extraction of `IR Doseringen V-5-0-1.pdf` (38 pp.,
  Z-Index, 16-09-2025); all section references checked against the extracted
  text of §§ 1.3–4.6.2 and the 3.4 interchangeability table.
- Code evidence: every `file:line` citation read directly from the working
  tree at commit `423f50b8` (master, clean) on 2026-06-12. Key verifications:
  - `RuleFinder.fs:117-126` — selection predicate (care group OR, GPK, route,
    age, weight, BSA; no gender, no frequency, no indication).
  - `ZIndex/DoseRule.fs:391-450` — the bst649→643→642→641→640 join and field
    mapping; `:456` — PRK/HPK discarded.
  - `ZIndex/DoseRule.fs:207-219` — 0/all-nines sentinel handling.
  - `Check.fs:198-223` — only age and weight passed to `GStand.createDoseRules`
    (`bsa = None`, `gpk = None`, no gender).
  - `ServerApi.Services.fs:67-138` — production consumption: severity→color
    mapping and the `NoMonitoring` sentinel flow.
- No build or test run was needed; this analysis changes no code.
