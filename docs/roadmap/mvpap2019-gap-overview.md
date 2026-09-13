# MVPAP2019: What Has to Be Built, and What Has to Be Configured

## Scope

**MVPAP2019 is not the full AfsprakenProgramma 2019 (AP2019) replacement.** Its scope is
three pillars:

1. **Create** TPN and continuous medication orders
2. **Record** them
3. **Notify the pharmacy** with the calculated preparation instructions — what components,
   and how much of each

[Fit-Gap Analysis: AP2019 vs GenPRES](fit-gap-ap2019-vs-genpres.md) describes the *eventual*
replacement (106 requirements, 62 Fit / 20 Partial / 24 Gap). This document narrows it to the
MVP and then sorts every remaining item by **what kind of work it is**. Item numbers (5.17,
9.6, …) refer to rows of that fit-gap; rule and use-case references (Rule 39, uc-03, …) refer
to the [MainEHR integration model](../scenarios/integration/GenPRES-MainEHR-Integration-V8.md).

**MDR and regulatory work is out of scope for this document.** It is tracked separately.

**As of** 2026-09-04. Milestones: M1 build/CI due 09-06 · M2 hospital integration due 09-20
· M3 pharmacy notification due 10-04 · M4 UI/UX due 10-18 · M5 bug fixing due 11-29.

## The classification rule

GenPRES is a constraint-satisfaction system. Dose limits, concentration bounds, volume
bounds, administration times, diluent choices, patient categories and access requirements
are **data** in the rule sheets; the solver applies them without any code knowing which drug
or which ward they belong to. So a requirement is only a software task if the *mechanism* to
express it does not exist. If the mechanism exists and the rows are missing, it is
configuration.

Every item below is therefore in exactly one of four bins:

| Bin | Meaning | Becomes |
|---|---|---|
| **Software** | Code in this repository has to be written or fixed | A GitHub issue in a milestone |
| **Configuration** | A rule sheet has to gain or change rows; the code already applies them | A row in [section 6](#6-configuration-work), owned by whoever authors the knowledge rules. **Not an issue** |
| **Arrangement** | Something the hospital or a third party has to provide or decide | A row in [section 5](#5-blocking-decisions-and-arrangements). **Not an issue**; software issues that depend on it say so in their body |
| **Out of MVP** | Full-replacement work | Labelled, not built ([section 8](#8-explicitly-out-of-mvp)) |

**Method.** Every "mechanism exists" or "mechanism missing" claim is verified against the
repository, not taken from a document; the commands are in [section 11](#11-verification).
Where a claim rests only on a document it is marked *(doc)*. Whether the *live rule sheets*
currently contain a given row is not verified here — that is exactly the configuration work.

---

## Summary

| Pillar | Software issues | Configuration tasks | Existing issues covering software |
|---|---|---|---|
| 1. Create TPN / continuous orders | 9 | 11 | 5 (#504 #495 #499 #505 #478) |
| 2. Record | 20 | 0 | 4 (#409 #481 #516 #518), all needing decomposition or a move |
| 3. Notify pharmacy | 7 | 0 | 1 (#510, unmilestoned) |
| Solver | 2 | 0 | 0 |
| **Total** | **38** | **11** | **10** |

Plus **7 blocking decisions** and **5 hospital-side arrangements**, none of which become
issues.

Three facts the milestone dates have to be read against:

- **Pillar 2 has no code at all.** No `SessionRecord`, `RedeemLaunch`, `UserCredential`,
  `SigningChallenge` or `OpenedToken` under `src/`; no database library in the solution;
  authentication is one client-side boolean. M2 is due in 16 days.
- **Pillar 3 has no issues at all**, and half of its output (continuous medication) has no
  print in any form. M3 is due in 30 days.
- **Pillar 1 is mostly configuration.** Of its 20 items, 11 need only rule rows. Three of the
  fit-gap's Partials (5.5, 5.7/5.10, 5.16) were recorded as missing mechanisms; the mechanisms
  exist. See [section 9](#9-corrections-to-the-fit-gap).

---

## 1. Pillar 1: create TPN and continuous orders

TPN prescribing (Nutrition view), the continuous dose type, solution rules, solver-derived
volumes, rates and concentrations, and 17 intake totals all work. The table sorts the in-MVP
shortfalls by bin. Milestone rule: **anything that changes what the pharmacy is told to
prepare belongs to M3**, because M3 cannot close without it; the rest is M4.

### 1.1 Software issues

| Item | Proposed issue | Why it is code, and what to build | Milestone |
|---|---|---|---|
| **5.17** | Expose per-component selection in the Nutrition view | The mechanism exists: `Filter.SelectedComponents` (`Shared/Types.fs:429`, applied in `GenORDER/Api.fs:568`) and `Components.MultipleSelect.View` in `Prescribe.fs:131`, shown when an orderable has more than one component. The Nutrition view does not surface it. Surface it; the "never remove the principal component or a concentration-bearing solvent" guard is configuration (C1.6) | M3 |
| **5.12** | Per-order "extra" flag excluding it from totals | `Totals.calc` (`GenORDER/Totals.fs:63`) sums every order whose dose carries a volume unit; there is no attribute to exclude one. New order attribute, honoured in `calc`, settable in the client | M4 |
| **7.3** | Allow a second unit for one total (glucose mg/kg/min) | `TotalsData` rows carry their own `Unit`/`TimeUnit`/`Adj`, so a row "koolhydraat, mg, min" could be authored — but `getTotals` (`Totals.fs:157`) does `Array.tryFind` on the name and returns the first row only. Let a total carry more than one unit; then the row is configuration (C1.7) | M4 |
| **7.8** | Gestational-age and post-menstrual-age bands on `TotalsData` | `TotalsData` (`GenFORM/Types.fs:764`) has `MinAge`/`MaxAge`/`MinWeight`/`MaxWeight` only, and `getTotals` filters on those two. Add `MinGestAge`/`MaxGestAge`/`MinPMAge`/`MaxPMAge`, extend `Mapping.getTotals`, the filter, the `Data` record comments and the column-contract test. Then the rows are configuration (C1.8) | M4 |
| **7.10** | Totals across all orders | `calculateNutritionTotals` (`ServerApi.Services.fs:695`) collects `plan.NutritionContexts` only; the treatment plan has its own. One aggregation over both | M4 |
| **11.6** | Flag a total outside its reference range | `getTotals` renders the range as a text suffix `"(min - max)"` (`Totals.fs:171`); nothing compares value to range. Add the comparison and a severity; the ranges are already data | M4 |
| **1.11** | Dual Unit/kg/hour and Unit/kg/min display | Rate is one unit at a time in the client. Display both for non-standard continuous medication | M4 |
| **2.7** | Move nutrition-category generic lists from code to configuration | The generics per nutrition category are hardcoded in `ServerApi.Services.fs` (`tpnDoseRuleSet:559`, `electrolyteGlucoseDoseRuleSet:659`, …). Glucose 5–50 % and NaCl 0,9 %/3 % are listed, so those side-lines are pure configuration today; sodium bicarbonate and albumin are not, so adding them is a code edit. Move the lists to a sheet, and every future side-line is configuration (C1.9) | M4 |
| **9.15** | Surface and process order start/stop | `StartStop` exists (`GenORDER/Types.fs:205`, field at `:226`) but is neither shown in the client nor processed on the server. Show it, process it, carry it into the preparation output — pillar 3 needs it for the start time | M4, early; or pull into M3 |

### 1.2 Configuration tasks

Each is a set of rows in a rule sheet. The mechanism column is what was verified.

| Item | Configuration | Mechanism (verified) |
|---|---|---|
| **5.5** | Author solution rules per access: `PVL = x` rows with tighter `MaxConc`, `CVL = x` rows for central | `SolutionRuleData` has `CVL`/`PVL` columns → `PatientCategory.Access` (`SolutionRule.fs:113-116`) → `VenousAccess.check` inside `Patient.filterPatient` (`Patient.fs:207`) → used by `SolutionRule.filter` (`:217`). The fit-gap's "unconfirmed" is resolved: it is applied |
| **5.7 + 5.10** | Set `MinTime`/`MaxTime` on the TPN dose rules for each stage (and `MinDur`/`MaxDur` where the rule has a duration) | `DoseRuleData.MinTime/MaxTime` ("infusion time of a single dose") → `Medication.fs:1255 Time = dr.AdministrationTime` → the continuous schedule's `Time` variable with `Time.applyConstraints` (`Order.fs:2337`). `MinTime = MaxTime = 24 h` makes rate = volume ÷ 24 h, which *is* the pump stand (5.7); `MaxTime` alone gives the over-period warning (5.10). The fit-gap's "no intended period on the dose rule" is wrong: the columns exist. Do not hardcode 24 h — set it per stage |
| **5.13** | Set `MaxVol`/`MaxVolAdj` on the TPN solution rule | Total volume is a solver bound; when the other lines exceed it there is no valid scenario, so a negative base volume cannot be produced. The only gap is the "no scenario" feedback, which is [#478](https://github.com/informedica/GenPRES/issues/478) — existing issue, now MVP-relevant |
| **5.15** | On TPN rules set the `Solutions` column to glucose only | `Solutions` lists the acceptable diluents per rule (`Types.fs:1013`); a diluent not listed is never offered, so a non-glucose base with protein cannot be composed *(mechanism verified; that this is the intended clinical rule is for the rule author)* |
| **5.16** | Author lipid solution rules per weight band with `MinWeight`/`MaxWeight` | Patient-category weight bounds are built with `fromTupleInclExcl` (`SolutionRule.fs:118`): `[min, max)`, so a weight exactly on a boundary falls into the *higher* band by construction. Nothing to code |
| **5.17 guard** | `MinQty > 0` on the principal substance and on any solvent a `MaxConc` depends on | `SolutionLimit.Quantity` is a `MinMax` per substance; a positive minimum makes the solver refuse zero, which is the same safety intent as AP2019's un-removable check box |
| **7.3 row** | A second `koolhydraat` row with `Unit = mg`, `TimeUnit = min`, `Adj = kg` | After software 7.3 |
| **7.8 rows** | `Totals` rows banded by gestational and post-menstrual age for neonates | After software 7.8 |
| **2.7** | Dose and solution rules for glucose, NaCl, sodium bicarbonate and albumin side-lines under indication "Parenterale suppletie" | Glucose/NaCl: today, as ElectrolyteGlucose lines; `Totals.calc` counts every order with a volume, so they feed the fluid total. Bicarbonate/albumin: after software 2.7 |
| **2.8** | Fluid-intake advice rows: `Totals` "volume" rows with GA/PMA bands and ml/kg/day limits | After software 7.8 for the bands and 11.6 for the comparison. The fit-gap already records this as expressible |
| **2.9** | A dose-type variant with dose text "phototherapy" carrying its own fluid and glucose limits | Dose text is part of the dose-type selection constraint; the solver recomputes intake on any change. The fit-gap already records this as expressible |

### 1.3 Existing issues in this pillar

[#504](https://github.com/informedica/GenPRES/issues/504) (new TPN design),
[#495](https://github.com/informedica/GenPRES/issues/495) (nutrition in the treatment plan),
[#499](https://github.com/informedica/GenPRES/issues/499) (deviate from the recommended dose
with argumentation, the UX half of 11.4) stay in M4.
[#505](https://github.com/informedica/GenPRES/issues/505) (prescribe in an adjusted range)
and [#478](https://github.com/informedica/GenPRES/issues/478) (feedback when no order can
be created — the flag half of 5.13) move from unmilestoned to M4.

---

## 2. Pillar 2: record

Nothing in this pillar exists. All of it is server and client code plus a store, so all of it
is software. The chain is identity → session → signed plan → durable store. Everything below
is proposed for **M2**, whose description should be narrowed to "record TPN and continuous
orders: launch, session, signing, store".

Four decisions gate the first tasks: the launch model (D1), the storage backend (D2), server
statelessness (D5), and step-up signing (D3), which decides whether 2.1.7 and the PIN step of
2.1.8 are built at all. See [section 5](#5-blocking-decisions-and-arrangements).

### 2.1 Identity and session — software

| # | Proposed issue | What to build | Blocked by |
|---|---|---|---|
| 2.1.1 | Sealed launch: verify, single use, patient check | Server side of Rules 1-7 and uc-01: verify a sealed launch, spend its nonce once, enforce the lifetime, check that the launch's patient is the user's active MainEHR patient. GenPRES owns the contract and the verifier; the MainEHR LaunchScript that mints it is an arrangement (A1) | D1 |
| 2.1.2 | Erase the launch from URL and history; stop logging the raw URL | Rule 39, plus a live bug: the parse-failure path at `Client/App.fs:313` logs the raw URL. File the bug now, whatever D1 decides | — |
| 2.1.3 | Entra ID sign-on with back-channel code redemption | Edges C3 / C6. The session's user is the browser identity, never what the launch claims. The app registration is an arrangement (A2) | D1 |
| 2.1.4 | UserRegistry client | Edge C7: login → person, Role, mail address, active patient. The registry endpoint is an arrangement (A3) | 2.1.3 |
| 2.1.5 | SessionRecord lifecycle | Concept 9, Rules 8-14 and 40-45: one open session per user and per browser, idle refresh, `HttpOnly`/`Secure`/`SameSite` cookie, atomic state-guarded transitions. [#516](https://github.com/informedica/GenPRES/issues/516) covers the store; this issue covers the lifecycle | D2, D5 |
| 2.1.6 | Six session endings with owed-notice mechanics | [session-endings.md](../scenarios/integration/session-endings.md), Rules 21-22 | 2.1.5 |
| 2.1.7 | PIN enrolment and reset | uc-02, uc-06, Rules 23-28 and 37: mailed one-time confirmation code, wrong-PIN counting, exponential backoff. **Do not start before D3 is answered**; if the badge product can confirm a single action, this issue is closed as not needed. The mail relay is an arrangement (A4) | D3 |
| 2.1.8 | Two-request signing | uc-03, Rules 42-44: request a challenge, show it modally, take the credential (PIN or badge confirmation per D3), commit as one transaction re-verifying session, role, tokens, head, challenge and credential | 2.1.5, D3 |
| 2.1.9 | Reader role | uc-09, Rule 26: read-only, credential stage skipped, never asked for a PIN | 2.1.4 |
| 2.1.10 | Authority withdrawal, fail-closed | uc-11, Rule 38: withdraw on registry change; fail closed after a bounded grace period when the registry is unreachable | 2.1.4 |
| 2.1.11 | Append-only audit store | Rule 46: every launch honoured or refused, every session open and end with reason, every submission committed or refused including failed PINs, every PIN change. The store's code; its hosting is an arrangement (A5) | D2 |
| 2.1.12 | WorkPlan carry-over across sessions | [#518](https://github.com/informedica/GenPRES/issues/518), uc-08 step 3. Currently in M4; it is session work and presupposes 2.1.5, so it moves to M2 | 2.1.5 |

### 2.2 The record itself — software

| # | Proposed issue | What to build | Blocked by |
|---|---|---|---|
| 2.2.1 | Persistence layer: save / getLatest / latestVersionMetadata | The API from [feature-patient-persistence.md](feature-patient-persistence.md) §5. Append-only versions; save refused without a patient id. The code half of [#409](https://github.com/informedica/GenPRES/issues/409) and the destination of [#481](https://github.com/informedica/GenPRES/issues/481) | D2 |
| 2.2.2 | Two stores: clinical and private | A clinical store (treatment plans, copied onward by the hospital, 11.3) and a private store (session records, credentials, spent marks, audit — never copied). Schema and separation in code | D2, 2.2.1 |
| 2.2.3 | Restore patient state and treatment plan on reopen | 9.6: reopening an identified patient restores the last-saved context and plan. Client side of 2.2.1 | 2.2.1 |
| 2.2.4 | Per-order signed state and prescriber attribution | 9.8b, 10.3, 10.4: each recorded order carries its signed state and the prescriber who signed it; a prescriber registry populated from 2.1.4. **Pillar 3 depends on this**: a preparation instruction must name the prescriber | 2.1.8, 2.2.1 |
| 2.2.5 | Role-based authorization on every command | 10.1, 10.2: Prescriber / Reader enforced server-side. The server half of 2.1.4 and 2.1.9; this issue is the check itself | 2.1.4 |
| 2.2.6 | KnowledgeRuleSet version stamp on each recorded plan | Concept 18, [backlog](backlog.md) items 3-5: each saved plan records the rule-set version that produced it, so it can be reopened under those rules | 2.2.1 |
| 2.2.7 | Record a deliberate out-of-limit value against the order | 11.4: the confirmed, reasoned deviation is stored with the order. #499 is the UX half (M4); this is the storage half. Severity is recomputed on each solve today and never stored | 2.2.1, #499 |
| 2.2.8 | Soft conflict warning on save | 9.7: "saved more recently by X — save anyway?" using `latestVersionMetadata`. Not an MVP blocker; cheap; needs an issue so it is not forgotten | 2.2.1 |

### 2.3 Existing issues in this pillar

- [#409](https://github.com/informedica/GenPRES/issues/409) is an epic. Decompose it into
  2.2.1, 2.2.3, 2.2.4 and 2.2.8, then close it or keep it as the tracking issue.
- [#408](https://github.com/informedica/GenPRES/issues/408) (URL parameters) is closed or
  kept depending on D1. It must not be worked on before D1 is recorded.
- [#481](https://github.com/informedica/GenPRES/issues/481) and
  [#516](https://github.com/informedica/GenPRES/issues/516) move to M2.
- [#518](https://github.com/informedica/GenPRES/issues/518) moves from M4 to M2 (2.1.12).
- [#431](https://github.com/informedica/GenPRES/issues/431) (`Min/MaxQtyAdj` to `PerKg`: a
  parser change plus a sheet migration) is data-loading software, not integration. MVP-relevant
  because solution rules drive TPN preparation; moves from M2 to M5.

---

## 3. Pillar 3: notify the pharmacy

**Milestone 3 has zero issues.** What exists: `ParenteralPrintView`
(`Client/Views/Nutrition.fs:360`) renders per-line components with prepared volume and dose
per kg, total volume, pump rate, administration time, intake totals with reference ranges,
patient header and a prescriber signature line. The TPN calculation and document work;
fit-gap 5.11a is Fit.

### 3.1 Software issues

| # | Proposed issue | What to build | Blocked by |
|---|---|---|---|
| 3.1 | Preparation output for continuous medication (infuusbrief) | `ContinuousMeds.fs` prints the continuous-medication *reference list*, not the patient's orders, and `OrderPlan.fs:371` passes `onPrint = None`. Build a per-patient preparation view for prescribed continuous orders with the same content model as the TPN print. Fit-gap 2.1 | 9.15 for the start time |
| 3.2 | One preparation document model for TPN and continuous orders | Extract the content of `ParenteralPrintView` into a renderer-independent model so the same data renders as print, mail body or structured payload. Prerequisite for 3.3 whatever D4 decides | 3.1 |
| 3.3 | Electronic hand-off of the preparation instruction | Fit-gap 5.11b. Send the document over the channel D4a chooses. No SMTP, MailService or MailMessage exists in `src/` today. Must name the prescriber (2.2.4) | D4a, D4b, 3.2, 2.2.4 |
| 3.4 | Record pharmacy acknowledgement | If D4c says the pharmacy confirms receipt, record it against the order; otherwise close | D4c |
| 3.5 | Nurse-facing preparation view | [#510](https://github.com/informedica/GenPRES/issues/510): the same calculation for a different audience. Move to M3, build on 3.2 | 3.2 |
| 3.6 | VTGM preparation letters | Fit-gap 9.10. If the hand-off (3.3) already carries the preparation steps this is a rendering of 3.2, not separate work; confirm when D4b is answered | 3.2, D4b |
| 3.7 | Order file export | Fit-gap 9.8c. If D4a chooses a file drop this is the same code as 3.3 | D4a |

All in **M3**. M3 also holds software 5.17 (section 1.1) and depends on configuration C 5.5,
5.15 and the 5.17 guard (section 1.2) — they decide *what* is prepared — and on 2.2.4 for
the prescriber name.

---

## 4. Solver — software

Two production gaps with working prototypes outside the compile list
([w2-core-architecture-review.md](w2-core-architecture-review.md)).

| # | Proposed issue | What to build | Milestone |
|---|---|---|---|
| 4.1 | Cycle detection in the production solver | Integrate `LoopDetect.fsx` (`src/Informedica.GenSOLVER.Lib/Scripts/`) into the library: a typed termination reason (`HardLimit` / `CycleDetected` / `PotentialStall`) returned to the caller instead of the bare `MAX_LOOP_COUNT` ceiling. TPN is the most equation-dense scenario, so this is correctness, not polish | M5 |
| 4.2 | LRU memoisation in the production solver | Integrate `LRUSolverIntegration.fsx` into `OrderProcessor.fs`. Performance only; may be parked past MVP | M5, may be parked |

---

## 5. Blocking decisions and arrangements

None of these are issues. Each software issue that depends on one names it in its body as
"blocked by Dn" / "requires An", so the dependency is visible without a card of its own.

### 5.1 Decisions

| # | Decision | Options | Blocks | Needed by |
|---|---|---|---|---|
| **D1** | Launch model: URL parameters or sealed launch token | [feature-ehr-url-parameters.md](feature-ehr-url-parameters.md) (`pid`, `fnm`, `lnm`, `usr` in the URL; a token handshake explicitly rejected) versus [docs/scenarios/integration/](../scenarios/integration/README.md) (token model normative; Rule 39 erases the launch from URL and history because PII in a URL reaches browser history, referrer headers and proxy logs). They are mutually exclusive. #408 implements the first; #516 and #518 the second | 2.1.1, 2.1.3, #408 | ~09-08, before M2 work starts |
| **D2** | Storage backend | Constraints fixed in [feature-patient-persistence.md](feature-patient-persistence.md) §5: relational, local, on-premise inside the hospital firewall. Product, schema shape (blob-per-version versus normalised rows) and the two-store split (2.2.2) remain to choose | 2.1.5, 2.1.11, 2.2.1, 2.2.2 | ~09-08 |
| **D3** | Step-up signing: badge confirmation or GenPRES PIN | Open Question 3 of the integration model, pending hospital IT. If the badge product confirms a single action with its own PIN, 2.1.7 and the PIN step of 2.1.8 are not built | 2.1.7, 2.1.8 | ~09-12 |
| **D4a** | Pharmacy delivery channel | Mail (edge C10), file drop, or API into the pharmacy system | 3.3, 3.7 | ~09-18 |
| **D4b** | Pharmacy document format | Rendered print, structured payload, or both | 3.2, 3.3, 3.6 | ~09-18 |
| **D4c** | Pharmacy acknowledgement | Does the pharmacy confirm receipt, and does GenPRES record it | 3.4 | ~09-18 |
| **D5** | Server statelessness | Rule 32: WorkPlan in the client, all standing in the SessionRecord, so multiple servers can run. Decide before 2.1.5 is designed | 2.1.5, 2.1.12 | ~09-08 |

### 5.2 Arrangements (hospital side)

| # | Arrangement | Required by |
|---|---|---|
| **A1** | MainEHR LaunchScript that mints the sealed launch (VB.NET, outside this repository) | 2.1.1 |
| **A2** | Entra ID app registration for GenPRES | 2.1.3 |
| **A3** | UserRegistry endpoint: login → person, Role, mail, active patient | 2.1.4 |
| **A4** | Mail relay reachable from the server | 2.1.7, 3.3 if D4a = mail |
| **A5** | Database provisioning, hosting, backup and restore, monitoring; upgrade drain (Rule 34) | 2.1.5, 2.1.11, 2.2.1 |

---

## 6. Configuration work

Consolidated from section 1.2, grouped by sheet, so the rule author has one list. None of it
is a GitHub issue; where a row depends on a software issue it says so.

**DoseRules sheet**

- TPN rules per stage: `MinTime`/`MaxTime` (and `MinDur`/`MaxDur` where applicable) — 5.7, 5.10
- Side-line rules under "Parenterale suppletie" for glucose and NaCl now; sodium bicarbonate
  and albumin after software 2.7 — 2.7
- A "phototherapy" dose-type variant with its own fluid and glucose limits — 2.9

**SolutionRules sheet**

- Per-access rows: `PVL = x` with tighter `MaxConc`, `CVL = x` for central — 5.5
- TPN rules: `Solutions` = glucose only — 5.15
- Lipid rules per weight band via `MinWeight`/`MaxWeight` — 5.16
- TPN rule: `MaxVol`/`MaxVolAdj` — 5.13
- `MinQty > 0` on the principal substance and any concentration-bearing solvent — 5.17 guard

**Totals sheet**

- Second `koolhydraat` row in mg/kg/min — 7.3, after software 7.3
- GA/PMA-banded rows for neonates — 7.8, after software 7.8
- Fluid-advice "volume" rows with GA/PMA bands — 2.8, after software 7.8 and 11.6

**Nutrition-category generics** (a new sheet after software 2.7) — the lists now in
`ServerApi.Services.fs`.

Note [#431](https://github.com/informedica/GenPRES/issues/431): the `Min/MaxQtyAdj` →
`PerKg` rename touches the SolutionRules sheet; sequence the rows above after it, or author
them against the new column names.

---

## 7. Issue regrouping

**Wrong milestone**

- [#518](https://github.com/informedica/GenPRES/issues/518) (WorkPlan carry-over) is in M4 but
  is session work; to M2 as 2.1.12.
- [#431](https://github.com/informedica/GenPRES/issues/431) (`Min/MaxQtyAdj` to `PerKg`) is in
  M2 but is data-loading software; to M5.

**M4 mixes MVP-critical work with out-of-MVP polish**

- In MVP: #504, #495, #499, #505, #478, and #379 if it blocks them.
- Out of MVP: #393, #396, #397, #398, #394, #405, #487, #489, #498, #399 — UX on
  discontinuous flows and on the emergency list, which already ships.

**Unmilestoned issues with a home**

Move #408 (pending D1), #481 and #516 to M2 · #510 to M3 · #505 and #478 to M4 · #506, #508, #509 and #439 to post-MVP · #378, #411, #413, #416, #419, #420, #446 and #522 to a parking milestone, so they stop reading as untriaged.

**M5 is drifting into a general bug bucket**

Issues #488 and #507 are current defects in a milestone titled "final updates before go
live", while `bug`-labelled #398 and #489 sit in M4 among enhancements. Keep M5 for defects,
data-loading changes (#431) and the solver items; do not let feature work drift into it.

**Label hygiene**

12 open issues still carry `needs triage`, including these already-milestoned ones: #397, #399, #487, #495, #498, #499 and #504.

---

## 8. Explicitly out of MVP

Discontinuous medication gaps (PRN 3.11, comments and remarks 3.12, non-assortment and study
drugs 3.17, TallMan lettering 3.14, indication sorting 3.13) · enteral feeding beyond what
exists · eGFR, AKI and lab integration (7.12, 7.13, 7.9, 9.11) · PICU/NICU workflow
separation (9.13) · HIX import (9.9) · in-app formulary editing (9.14) · the patient-list
picker (9.4, already Out of scope by design) · MetaVision sync (9.8a, likewise) · the whole
of [fit-gap-clinician-workbook-vs-genpres.md](fit-gap-clinician-workbook-vs-genpres.md).
Label, do not build.

---

## 9. Corrections to the fit-gap

Three rows of [fit-gap-ap2019-vs-genpres.md](fit-gap-ap2019-vs-genpres.md) record a missing
mechanism that exists. They should be re-marked so the next reader does not plan code for
them:

| Row | Recorded as | Actually |
|---|---|---|
| **5.5** | Partial — "unconfirmed is … that the recorded access actually selects which solution constraints apply" | Confirmed. `SolutionRuleData.CVL/PVL` → `PatientCategory.Access` → `VenousAccess.check` in `Patient.filterPatient`, used by `SolutionRule.filter`. Configuration |
| **5.7, 5.10** | Partial — "missing is an intended administration period on the dose rule to judge it against" | `DoseRuleData.MinTime/MaxTime` exist and flow to the order's `Time` constraint via `Medication.fs:1255`. Configuration |
| **5.16** | Partial — "weight-band boundary-to-higher rule not confirmed" | Patient-category bounds are `[min, max)` (`fromTupleInclExcl`), so a boundary weight falls in the higher band. Configuration |

Two further rows are configuration rather than code once read against the model: **5.13**
(a total-volume bound; the flag half is #478) and **5.15** (the `Solutions` column). The
document also has no MVP column; a per-row scope marker would have made this narrowing
mechanical.

---

## 10. Milestone view

What each milestone holds once the above is applied. Existing issue numbers in parentheses;
everything else is a new issue. Configuration and decisions are listed for the date they are
needed, not as cards.

**M1 build/CI, due 09-06.** No change.

**M2 hospital integration, due 09-20.** Narrow the description to "record TPN and continuous
orders: launch, session, signing, store". Software: 2.1.1–2.1.12 (with #516, #518) and
2.2.1–2.2.8 (with #409 decomposed, #481) — twenty issues, none started. Blocked by D1, D2, D5
(needed ~09-08) and D3 (~09-12); requires A1–A5. Only 2.1.2 can start today.

**M3 pharmacy notification, due 10-04.** Software: 3.1–3.7 (with #510) and 5.17 — eight
issues, up from zero. Blocked by D4a–c (~09-18) and by 2.2.4 in M2. Configuration due before
M3 closes: 5.5, 5.15, the 5.17 guard.

**M4 UI/UX, due 10-18.** Keep #504, #495, #499, and #379 if it blocks them; add #505, #478.
Software: 5.12, 7.3, 7.8, 7.10, 11.6, 1.11, 2.7, 9.15 — eight new issues. Configuration due
before M4 closes: 5.7/5.10, 5.13, 5.16, the 7.3 and 7.8 rows, 2.7 side-lines, 2.8, 2.9. Move
out to post-MVP: #393, #396, #397, #398, #394, #405, #487, #489, #498, #399. Note 9.15 is
needed by 3.1; do it early or pull it into M3.

**M5 bug fixing, due 11-29.** #431 · solver 4.1 · solver 4.2 (may be parked) · #488, #507.

**Post-MVP / parking.** #506, #508, #509, #439 · #378, #411, #413, #416, #419, #420, #446, #522 · the ten M4 issues moved out.

---

## 11. Verification

Run from the repository root.

```bash
# pillar 2 has no implementation
rg -n 'SessionRecord|RedeemLaunch|UserCredential|SigningChallenge|OpenedToken' src/

# no persistence layer, no mail transport
rg -li 'sqlite|litedb|npgsql|dapper|entityframework' src/ paket.dependencies
rg -li 'smtp|mailservice|sendmail|MailMessage' src/

# auth is a single client-side boolean
rg -n 'IsAuthenticated' src/Informedica.GenPRES.Client/App.fs

# 5.5: access columns exist and are applied by the filter
rg -n 'CVL: string|PVL: string' src/Informedica.GenFORM.Lib/Types.fs
rg -n 'r.CVL = "x"|r.PVL = "x"' src/Informedica.GenFORM.Lib/SolutionRule.fs
rg -n 'VenousAccess.check' src/Informedica.GenFORM.Lib/Patient.fs
rg -n 'PatientCategory.filterPatient' src/Informedica.GenFORM.Lib/SolutionRule.fs

# 5.7 / 5.10: administration-time columns exist and reach the order
rg -n 'MinTime|MaxTime|MinDur|MaxDur' src/Informedica.GenFORM.Lib/Types.fs
rg -n 'Time = dr.AdministrationTime' src/Informedica.GenORDER.Lib/Medication.fs
rg -n 'Time.applyConstraints' src/Informedica.GenORDER.Lib/Order.fs

# 5.16: inclusive/exclusive band bounds
rg -n 'fromTupleInclExcl' src/Informedica.GenFORM.Lib/SolutionRule.fs

# 5.17: per-component selection already exists in Prescribe, not Nutrition
rg -n 'SelectedComponents' src/Informedica.GenPRES.Shared/Types.fs src/Informedica.GenORDER.Lib/Api.fs
rg -n 'Components.MultipleSelect' src/Informedica.GenPRES.Client/Views/Prescribe.fs
rg -n 'SelectedComponents' src/Informedica.GenPRES.Client/Views/Nutrition.fs   # no hits

# 7.3 / 7.8 / 11.6: Totals sheet columns and lookup
rg -n -A14 'type TotalsData =' src/Informedica.GenFORM.Lib/Types.fs
rg -n 'Array.tryFind \(fst >> String.equalsCapInsens n\)|let norm =' src/Informedica.GenORDER.Lib/Totals.fs

# 2.7: category generics are hardcoded
rg -n 'let tpnDoseRuleSet|let electrolyteGlucoseDoseRuleSet' src/Informedica.GenPRES.Server/ServerApi.Services.fs

# 9.15: start/stop exists on the order type but is unused
rg -n 'StartStop' src/Informedica.GenORDER.Lib/Types.fs

# pillar 3: the parenteral print exists; the treatment plan has none
rg -n 'ParenteralPrintView' src/Informedica.GenPRES.Client/Views/Nutrition.fs
rg -n 'onPrint' src/Informedica.GenPRES.Client/Views/OrderPlan.fs

# milestones and issue distribution
gh api repos/informedica/GenPRES/milestones --paginate \
  -q '.[] | "\(.title)\topen:\(.open_issues) closed:\(.closed_issues)\tdue:\(.due_on)"'
gh issue list --state open --limit 200 --json number,title,milestone \
  -q '.[] | "\(.number)\t\(.milestone.title // "NONE")\t\(.title)"' | sort -n
```

---

## References

- [Fit-Gap Analysis: AP2019 vs GenPRES](fit-gap-ap2019-vs-genpres.md) — item numbers; see
  section 9 for corrections
- [Fit-Gap Analysis: Clinician Calculation Workbook vs GenPRES](fit-gap-clinician-workbook-vs-genpres.md)
  — out of MVP in full
- [GenPRES / MainEHR integration model](../scenarios/integration/README.md) — rules, edges,
  use cases behind pillar 2
- [Feature: patient persistence](feature-patient-persistence.md) — persistence API and
  constraints (D2)
- [Feature: EHR URL parameters](feature-ehr-url-parameters.md) — the other side of D1
- [Backlog](backlog.md) — items 3-5 (rule-set versioning), 8 (AuthN/AuthZ)
- [W2: core architecture review](w2-core-architecture-review.md) — solver prototypes
