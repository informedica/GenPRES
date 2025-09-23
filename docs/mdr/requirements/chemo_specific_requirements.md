
# Fully Automated Chemotherapy Order, Compounding & Administration System

**Requirements Specification (Draft v1.0 — 23 Sep 2025)**
> Author: Clinical Pharmacist (adult & pediatric oncology)
> Scope: End‑to‑end, closed‑loop chemotherapy prescribing, verification, sterile compounding, and administration across inpatient, day‑hospital and ambulatory oncology. EU‑centric (Netherlands) compliance with global best practices.

---

## 1. Purpose & Objectives

Design a safe-by-default, automation‑first platform that:

- Prevents catastrophic wrong‑drug / wrong‑route errors (e.g., intrathecal vincristine) and dose miscalculations.
- Automates calculations (BSA, AUC), verification, labeling, gravimetric checks, barcode scanning, and smart‑pump programming.
- Ensures regulatory and occupational safety (sterile compounding, hazardous drug handling, GDPR/NEN 7510 logging).
- Provides pediatric‑ready workflows, clinical decision support (CDS), and regimen lifecycle management with auditability.
- Enables interoperability (HL7® FHIR®, IHE Pharmacy) and supply chain traceability (GS1, EU FMD).

### 1.1 Non‑Goals

- Building a full hospital EHR. (This system integrates with the enterprise EHR.)
- Replacing radiation oncology/RIS functionality (out of scope).

---

## 2. References (clinical, safety, interoperability) — key sources

- ASCO/ONS Antineoplastic Therapy Administration Safety Standards (adult & pediatric, 2024 update).
- ASCO/ONS 2016 update (minibag for vinca alkaloids; two‑person verification).
- NCCN “Just Bag It” (vincristine in minibag) campaigns and NCCN Chemotherapy Order Templates.
- USP General Chapters: **<800>** Hazardous Drugs; **<797>** Sterile Compounding.
- EU GMP **Annex 1** (2022, effective 2023; CCS focus) — principles applicable to hospital aseptic preparation.
- EU/Netherlands privacy & logging: **GDPR**, Dutch **NEN 7510** (security) & **NEN 7513** (access logging).
- NIOSH recommendations for Closed‑System Transfer Devices (CSTDs).
- HL7® FHIR® (MedicationRequest/Dispense/Administration), IHE Pharmacy workflows (HMW, CMA, CMPD, MTP).
- GFR/AUC dosing: **Calvert formula** for carboplatin; **Mosteller** BSA.
- Extravasation prevention/management: **ESMO‑EONS** and **ASCO/ONS** guidance.
*(Full citation list at the end.)*

---

## 3. Definitions & Abbreviations

- **BSA:** Body Surface Area (Mosteller unless otherwise noted).
- **AUC dosing:** Carboplatin dose by Calvert formula.
- **BCMA:** Bedside barcode medication administration.
- **CSTD:** Closed‑System Transfer Device.
- **CDS:** Clinical Decision Support.
- **CTCAE:** Common Terminology Criteria for Adverse Events (v5 now; v6 emerging).
- **FMD:** EU Falsified Medicines Directive (pack serialization/verification).
- **GTIN/GS1‑128:** Global standards for product identification/barcodes.
- **HMP:** Hazardous Medicinal Product (EU term).
- **IHE Pharmacy:** Profiles for prescription, advice, dispense, administration.
- **Smart pump auto‑programming:** EHR → pump parameters transfer with verification.

---

## 4. High‑Level Architecture & Modules

1. **Prescriber Workbench** (order sets/regimens, calculators, CDS).
2. **Pharmacy Verification** (clinical, technical checks; batch vs. patient‑specific).
3. **Sterile Compounding** (workflow, gravimetric validation, photo capture, CSTD, labeling).
4. **Inventory & Traceability** (lots, expiry, FMD verification, returns/waste).
5. **Nursing Administration** (BCMA at chair/bed, smart‑pump auto‑programming, infusion documentation, adverse event capture).
6. **Analytics & Quality** (KPIs, deviation management, stability windows, turnaround times).
7. **Interoperability** (EHR, LIS, ADT/Patient, pumps, robots, compounding workflow systems).
8. **Governance & Security** (GDPR, NEN 7510, NEN 7513, role‑based access, audit).

---

## 5. Functional Requirements (numbered)

### 5.1 Patient & Context

- **PAT‑001**: Retrieve demographics, weight, height, BSA inputs, allergies, pregnancy status, performance status, diagnosis/stage, protocol enrollment from EHR.
- **PAT‑002**: Pediatric flags auto‑apply pediatric dosing rules, rounding bands, max doses, and BSA/weight checks (daily and per cycle).
- **PAT‑003**: Labs/vitals import (CBC, CMP, bilirubin, creatinine, eGFR/CrCl) and results trend visualization; support hold/reduce criteria per regimen.

### 5.2 Regimens, Order Sets & CDS

- **REG‑001**: Curated regimen library with versioning, effective dates, owners, and change control (four‑eyes review).
- **REG‑002**: Map each regimen to the disease, line of therapy, cycle structure, day schedules, premeds, hydration, supportive care, monitoring.
- **REG‑003**: **Hard stops** for contraindicated routes (e.g., **vinca alkaloids must be minibag IV only; syringe route blocked**).
- **REG‑004**: Embed clinical criteria (ANC/platelets, bilirubin/AST/ALT, SCr/eGFR) with configurable **dose‑modification logic** and **holds**.
- **REG‑005**: Pediatric pathways allow mg/kg or mg/m² dosing with age/weight caps; support neonatal/infant adjustments.
- **REG‑006**: Support **oral antineoplastics** (education, adherence tracking, REMS/authorization steps).
- **REG‑007**: **Investigational/clinical trial** support (masked arms, randomization IDs, protocol‑specific constraints, sponsor labels).
- **REG‑008**: **Drug interaction**, duplicate therapy, cumulative dose tracking (e.g., doxorubicin lifetime), vesicant/irritant flags.
- **REG‑009**: Attach **NCCN Templates** or local equivalents; record **which guideline version** was used.
- **REG‑010**: **Two‑person verification** enforced for order entry finalization and for compounding release.

### 5.3 Calculations & Dosing

- **CALC‑001**: **BSA** auto‑calc using Mosteller (√(height[cm] × weight[kg] / 3600)); pediatric alternative formulas available.
- **CALC‑002**: **Carboplatin AUC** using **Calvert**: Dose (mg) = target AUC × (GFR + 25); choose CrCl/eGFR estimator (Cockcroft‑Gault/MDRD/CKD‑EPI) per policy; rounding rules and minimum SCr options configurable.
- **CALC‑003**: **BSA recheck**: any ≥5% weight/height change since last cycle triggers forced recalculation and review.
- **CALC‑004**: Display vial counts based on available strengths, with **waste minimization** optimization mode.
- **CALC‑005**: Enforce **max dose caps** and age/weight limits as per regimen/policy (e.g., capping BSA at 2.0 m², when applicable).

### 5.4 Pharmacy Verification

- **PHARM‑001**: Clinical verification checklist (indication, regimen match, line, labs/criteria met, dose intensity, previous cycle toxicity/CTCAE, interactions).
- **PHARM‑002**: Technical verification (diluent, concentration, final volume, filter needs, stability window, infusion rate, infusion sequence).
- **PHARM‑003**: **Labeling**: patient identifiers, drug/generic, dose, concentration, diluent, volume, route, rate, beyond‑use date/time, storage, **hazard** warnings, barcode (GS1‑128 or DataMatrix with GTIN/LOT/EXP).
- **PHARM‑004**: **FMD** verification for serialized packs before dispense; quarantine on failed verify.
- **PHARM‑005**: Batch compounding vs. patient‑specific lots tracked; support anticipatory doses with real‑time expiration countdown.

### 5.5 Sterile Compounding Workflow (robotic & manual)

- **COMP‑001**: ISO‑class workflow checklists aligned to EU GMP Annex 1 principles (cleanroom state, environmental monitoring status).
- **COMP‑002**: **CSTD** usage recorded and **required for administration** when dosage form allows (per USP <800>); optional at compounding if policy requires.
- **COMP‑003**: **Gravimetric verification** of each addition (target vs. actual tolerance bands) with photo documentation of vial labels/LOT/EXP and syringe readings.
- **COMP‑004**: **Barcode scanning** of ingredients (active & diluents), vials, final container; prevent look‑alike/sound‑alike swaps; Tall‑Man lettering UI.
- **COMP‑005**: Robotic interfaces (e.g., APOTECAchemo, RIVA) and pharmacy workflow systems (e.g., BD Cato, DoseEdge): receive compounding worklists, return executed data (weights, photos, timestamps).
- **COMP‑006**: Stability & BUD enforcement per Chapter <797> and local dataset; auto‑void doses past BUD or stability window.
- **COMP‑007**: **Intrathecal segregation**: physical and digital segregation of intrathecal doses from IV vinca alkaloids; incompatible co‑storage blocked.

### 5.6 Nursing Administration & Chairside Safety

- **ADMIN‑001**: **BCMA**: scan patient wristband + product barcode + staff badge; enforce “five rights” + route match + expiry + cycle/day validation.
- **ADMIN‑002**: **Smart‑pump auto‑programming**: send drug, concentration, rate, total volume, dose limits to pump library; require pump ACK prior to infusion start; record channel‑to‑patient binding.
- **ADMIN‑003**: **Vinca alkaloids**: enforce minibag preparation; **syringe administration blocked**; intrathecal clinics cannot receive vinca minibags on same cart.
- **ADMIN‑004**: Vesicant administration safeguards (central line check prompts, blood return checks, peripheral site time‑outs, extravasation protocol links).
- **ADMIN‑005**: Premedication verification and timing rules; hypersensitivity rapid‑response orderset (stop/hold/rechallenge logic).
- **ADMIN‑006**: Rate change logging with reasons; interruptions and residual volume auto‑calc.
- **ADMIN‑007**: Post‑dose monitoring timers, vitals schedule, and adverse event capture using **CTCAE** terms/grades; triggers pharmacist/physician review when thresholds met.

### 5.7 Oral Antineoplastics

- **ORAL‑001**: Dispense instructions with calendar, food/time restrictions, handling precautions, safe disposal; patient education printable in local language.
- **ORAL‑002**: Refill safety checks, lab prerequisites, adherence prompts, drug‑drug interactions.

### 5.8 Inventory, Traceability & Waste

- **INV‑001**: Track receipt → storage → compounding → dispense → administration; maintain chain‑of‑custody with timestamps and user IDs.
- **INV‑002**: **Lot/expiry** tracking; automated recall impact report (patients and doses flagged).
- **INV‑003**: Waste & partial vials accounting; hazardous waste category & container documentation.
- **INV‑004**: Temperature excursion capture and stability decision workflow.

### 5.9 Interoperability & Standards

- **INT‑001**: **HL7 FHIR** resources: Medication, MedicationRequest, MedicationDispense, MedicationAdministration, ServiceRequest, Observation, Practitioner, Device.
- **INT‑002**: **IHE Pharmacy** profiles: Hospital Medication Workflow (HMW), Community Medication Administration (CMA), Community Prescription/Dispense (CMPD), Medication Treatment Plan (MTP).
- **INT‑003**: BCMA & supply chain via **GS1** (GTIN, lot/expiry in GS1‑128/DataMatrix).
- **INT‑004**: Interfaces to EHR (orders, allergies, problems), LIS (labs), ADT (location/bed), pumps, robots, and verification solutions.
- **INT‑005**: EU **FMD** verification system integration (where applicable in hospital setting).

### 5.10 Audit, Security & Privacy

- **SEC‑001**: Role‑based access control; dual control for high‑risk steps; emergency break‑glass with justification.
- **SEC‑002**: **NEN 7510** security controls; encryption at rest/in transit; key management; hardening.
- **SEC‑003**: **NEN 7513**‑style immutable audit logs of who accessed/altered what/when (orders, verifications, compounding, administration).
- **SEC‑004**: **GDPR** lawful bases, purpose limitation, data minimization, retention, patient rights; consent capture where legally required (e.g., electronic exchange).
- **SEC‑005**: Vendor module and integration logs retained per policy; exportable for regulators.

### 5.11 Quality Management & Analytics

- **QM‑001**: Dashboards for turnaround times (order→verify→compound→admin), dose errors prevented, near‑misses, barcode scan compliance, gravimetric variances.
- **QM‑002**: Deviation/CAPA workflow; link to specific lot/batch and standard.
- **QM‑003**: **CTCAE** toxicity trend across cycles; dose intensity delivered vs. planned (DI/RDI).
- **QM‑004**: Stability window misses and chair delays analysis.

### 5.12 Pediatric‑Specific Requirements

- **PED‑001**: Pediatric dosing defaults (mg/kg or mg/m²), age‑band rounding rules, BSA formula options (Mosteller/Haycock).
- **PED‑002**: Height/weight recapture frequency per policy; alerts for rapid growth changes.
- **PED‑003**: Pediatric‑specific supportive care standards and extravasation prevention prompts.

### 5.13 Usability & Human Factors

- **UX‑001**: Regimen timeline visualization (cycles/days), planned vs. delivered.
- **UX‑002**: Color‑safe design; Tall‑Man lettering for LASA drugs; “do not use” abbreviations suppressed.
- **UX‑003**: Alert fatigue controls: tiered (hard‑stop, interruptive, passive) with governance.

### 5.14 Business Continuity

- **BC‑001**: Downtime order sheets auto‑generated; barcode re‑printing fallback; pump library offline procedure.
- **BC‑002**: High availability (RPO/RTO targets), disaster recovery tested at least annually.

---

## 6. Data Model (selected)

- **Order**: regimen ID, cycle/day, indication, line of therapy, dose basis (mg/m² / mg/kg / AUC), BSA/GFR inputs, rounding rules, max cap flags.
- **Dispense/Compound**: ingredients (GTIN/lot/exp), target/actual weights, gravimetric variance, photo links, final container ID/barcode, BUD.
- **Administration**: patient scan, product scan, user scan, pump params, start/stop times, rate changes, reasons, CTCAE events.
- **Audit**: immutable event store per NEN 7513 principles.

---

## 7. Safety Controls (exemplars)

- **Never event prevention**: system‑level blocks for intrathecal vincristine; minibag‑only enforcement and cross‑site segregation.
- **Two‑person checks**: order finalization; compounding release; independent double‑checks for high‑risk steps.
- **BCMA everywhere**: ingredients, intermediate, final product, patient, staff.
- **Gravimetric**: require within tolerance (configurable by drug); out‑of‑tolerance remediation or discard.
- **Vesicants**: central‑line prompts, blood return checks, timers; extravasation pathways at point of care.
- **CSTD**: administration required when dosage allows; compliance reporting.

---

## 8. Non‑Functional Requirements

- **Availability** ≥ 99.9% (clinical hours), audited.
- **Performance**: generate worklist/labels < 3s/dose; pump program export < 2s.
- **Scalability**: ≥ 500 active chairs/beds; ≥ 10k doses/month.
- **Interoperability**: FHIR R4/R5 support; IHE Pharmacy document exchange; API rate limits ≥ 100 req/s burst.
- **Security**: OWASP ASVS L2; encryption AES‑256/TLS1.3; least privilege; quarterly access recertification.

---

## 9. Acceptance Criteria (samples)

### 9.1 Vincristine safety

- Attempt to order **vincristine (IV)** by syringe or intrathecal route → **hard stop** with minibag‑only policy and references; order cannot be signed.
- System prevents scheduling of IT chemo in same session/logistics pathway where vinca minibags are present.
- Admin screen blocks start unless minibag container barcode is scanned and route matches IV infusion.

### 9.2 Carboplatin dosing

- Given: AUC=5, GFR=60 → Dose auto‑calc 425 mg (AUC×(GFR+25)); audit log shows inputs, estimator, rounding, pharmacist approval.
- Changing weight/SCr triggers **recalc** and re‑approval requirement.

### 9.3 Gravimetric check

- During compounding, ±5% tolerance; out‑of‑range requires supervisor override with reason and photo; label reprint forbidden until resolved.

### 9.4 BCMA & pumps

- Patient+product+user scans required; mismatches block start.
- Smart pump receives parameters; pump ACK recorded before “Start” enabled.
- Rate change mid‑infusion prompts reason selection; all events time‑stamped.

### 9.5 Pediatric dosing

- Height ↑ 4 cm since last cycle → system flags ≥5% BSA change → forces recalculation and verification.
- For a pediatric regimen set to mg/kg, changing weight recalculates doses and vial counts; caps applied per policy.

---

## 10. Implementation Notes & Integrations

- **Regimen library**: seeded with NCCN/locally approved protocols; local governance committee controls changes.
- **Compounding**: integrate with BD Cato or DoseEdge and/or robots (APOTECAchemo, RIVA) via vendor APIs for worklists/results.
- **Pumps**: integrate with hospital‑standard smart pumps (vendor APIs); maintain drug library sync and channel mapping.
- **FMD**: hospital policy may require scanning serialized packs at pharmacy receipt and/or dispense; system supports both.

---

## 11. Gap Analysis — Existing Applications vs. This Specification

| Area | Epic Beacon | Oracle Health (Cerner) Oncology | Varian ARIA / Elekta MOSAIQ MO | Flatiron OncoEMR / iKnowMed | Compounding Systems (BD Cato, DoseEdge, APOTECA, RIVA) | **Gaps Addressed by This Spec** |
|---|---|---|---|---|---|---|
| Regimen library & templates | Treatment plans with protocols and decision support | Integrated plans; NCCN template integration | MO modules exist; varied depth by site | Outpatient‑focused regimen libraries | N/A | Standardized governance, versioning metadata, effective dates, change control across all settings |
| Pediatric support | Supported; site‑dependent build quality | Adult & pediatric workflows noted; site builds vary | Pediatric support varies | Limited pediatric depth | N/A | Explicit pediatric rules (age/weight caps, formula choices, rounding bands, re‑capture triggers) |
| Vincristine hard‑stops | Policies supported; enforcement varies by build | Templates and alerts; enforcement per site | Site‑specific | Site‑specific | N/A | **Platform‑level hard stop** (syringe/intrathecal blocked), segregation logistics enforced |
| AUC/BSA calculators | Present | Present | Present | Present | N/A | Standardized math provenance, rounding, audit of inputs/assumptions; re‑calc on % change |
| Lab/criteria gating | Present (build dependent) | Present (build dependent) | Present | Present | N/A | Structured, testable hold/reduce logic with audit and analytics on overrides |
| Compounding workflow | Worklists/labels (if EHR‑pharmacy integrated) | Worklists/labels | Limited; relies on external | Relies on external | **Best‑in‑class gravimetric/photo/robotics** | Unified gravimetric tolerance, photo, ingredient scan, and **robot return‑data** into closed loop |
| Gravimetric verification | Site‑specific | Site‑specific | External | External | **Yes** | Make gravimetry a **system requirement** with out‑of‑tolerance handling |
| BCMA | Yes | Yes | Yes | Varies | Final container barcodes | End‑to‑end scans (ingredient → final → patient → admin) with forced route match and timing checks |
| Smart‑pump auto‑programming | Supported with vendors | Supported with vendors | Varies | Limited | N/A | Mandate ACK‑gated start + audit; synchronize drug libraries; channel→patient binding |
| FMD/GS1 traceability | Not universal | Not universal | Not universal | Not universal | N/A | Built‑in GTIN/LOT/EXP barcoding + optional FMD verification at receipt/dispense |
| CTCAE capture & dose intensity analytics | Varies | Varies | Varies | Varies | N/A | Native CTCAE logging, DI/RDI analytics, deviation/CAPA workflows |
| Security & logging | EHR audit trails | EHR audit trails | EHR audit trails | EHR audit trails | System logs | **NEN 7510/7513‑style immutable audit + GDPR data rights features** |

> **Notes:** This table synthesizes public descriptions and academic/industry reports. Site‑specific configuration often determines depth of safety controls. This specification elevates critical safety controls (vincristine, gravimetry, BCMA, pump ACK) from “site build” to **non‑negotiable platform requirements**.

---

## 12. Roll‑Out & Validation (summary)

- **Protocol migration**: import legacy regimens; dual‑build validation with pharmacy & nursing sign‑off.
- **Simulation**: dry‑runs with test patients covering adult/pediatric, AUC dosing, vesicants, oral agents.
- **Process validation**: pump auto‑programming, compounding gravimetry, BCMA scanning compliance (≥ 98% target).
- **Competency**: role‑based training; vincristine minibag drills; extravasation response drills.
- **Policy alignment**: USP <797>/<800>, EU Annex 1 mapping, GDPR/NEN controls documented.

---

## 13. Risk Register (selected)

- **Interface failures** (pumps/robots) → downtime workflows, manual double‑entry reconciliation.
- **Barcode gaps** (no GTIN) → local relabel with internal GS1 barcode; exception reports.
- **Alert fatigue** → governance, tiering, post‑go‑live tuning.
- **Data quality (weight/labs)** → recapture prompts and order gatings.

---

## 14. Detailed Citations (selected, by topic)

**Standards & Safety**

- ASCO/ONS antineoplastic therapy administration standards (2024 update; 2016 update for pediatric/two‑person checks & vinca minibags).
- NCCN “Just Bag It” campaign and NCCN Templates integrations (Epic/Cerner).
- ISMP Targeted Medication Safety Best Practices (vincristine minibag).
- ASHP medication safety and hazardous drug handling guidelines.
- ESMO‑EONS extravasation guidance; ONS/ASCO 2025 extravasation guideline.

**Compounding & Occupational Safety**

- USP <800> (CSTD required for administration when feasible); USP <797> sterile compounding.
- EU GMP Annex 1 (2022; CCS focus; effective 2023).
- NIOSH CSTD recommendations and performance protocol resources.

**Calculations**

- Mosteller BSA (NEJM, 1987).
- Calvert formula for carboplatin dosing (JCO, 1989) and subsequent reviews; kidney function estimation considerations.

**Interoperability**

- HL7 FHIR: MedicationRequest/Dispense/Administration.
- IHE Pharmacy: HMW, CMA, CMPD, MTP.
- EU FMD/GS1: GTIN/lot/expiry for traceability and BCMA.

**Data protection (NL/EU)**

- GDPR & Dutch AP guidance (consent in healthcare), **NEN 7510** (security), **NEN 7513** (logging).

---

## 15. Appendices

### A. Default Calculation Policies

- **BSA**: Mosteller; round to 2 decimals; recalc on ≥5% change.
- **AUC**: Calvert; CrCl estimator Cockcroft‑Gault (policy‑driven alternatives allowed); SCr floor configurable; cap total dose if required.
- **Rounding**: Dose rounding to nearest vial strength within ±5% unless protocol says otherwise.

### B. Default Tolerances

- **Gravimetric**: ±5% (vesicants ±3%); photos mandatory.
- **BCMA compliance**: ≥98% all scans (patient, product, user) to pass quality goals.

### C. Example Labels

- Include GTIN (01), LOT (10), EXP (17), Dose, Conc, Diluent, Route, **“For IV use only”** warnings, storage, BUD, hazard icons.

---

## Full Bibliography (URLs provided for implementation teams)

- ASCO/ONS standards, NCCN Just Bag It, ISMP best practices, USP <797>/<800>, EU GMP Annex 1, GDPR/AP guidance, NEN 7510/7513 resources, HL7 FHIR/IHE Pharmacy, GS1/FMD material, BSA/Calvert primary sources, ESMO/ONS extravasation resources, vendor documentation (Epic Beacon, Oracle Health Oncology), and compounding/robotic workflow solutions (BD Cato, DoseEdge, APOTECA, RIVA).

---

## 16. Gap Analysis: GenPRES vs. Current Chemotherapy Order Entry Systems

### 16.1 Where GenPRES Helps

GenPRES is designed as a **stateless, domain-constrained option solver** for medication prescribing. Unlike traditional EHR‑embedded chemotherapy order entry systems (Epic Beacon, Oracle Health Oncology, ARIA/MOSAIQ), which rely on monolithic workflows and heavily site‑dependent builds, GenPRES provides:

- **Formalized prescription logic**
  - Uses a declarative, constraint‑based approach for representing chemotherapy regimens, dose modifications, and order dependencies.
  - Removes reliance on free‑text orders and reduces variability across sites.
  - Example: ensures carboplatin AUC calculations are tied directly to patient GFR inputs and policy rules, not ad hoc build logic.

- **Stateless architecture**
  - Prescriptions are **recomputed from source inputs** (patient parameters, regimen definitions, lab values) every time, ensuring reproducibility and auditability.
  - Current systems cache calculated doses and allow manual edits without recalculation; GenPRES enforces deterministic results.

- **Domain‑constrained solver**
  - Encodes the allowable options (drug, dose basis, units, routes, supportive meds) directly in the model.
  - Prevents invalid combinations (e.g., syringe vincristine, intrathecal misroutes).
  - Current systems rely on alerts and local policy, which can be overridden or mis‑configured.

- **Separation of concerns**
  - Clean split between regimen definition (knowledge base), patient data, and execution engine.
  - Supports continuous improvement and protocol versioning independent of vendor release cycles.

- **Transparency & Explainability**
  - Solver output includes the reasoning path: “This dose = AUC × (GFR+25), GFR estimated with Cockcroft‑Gault, SCr floored at 0.7.”
  - In contrast, most EHR order sets provide only a numeric result without calculation provenance.

- **Alignment with MDR and EU regulatory frameworks**
  - By design, GenPRES maintains auditability and reproducibility critical for high‑risk domains such as chemotherapy and investigational drugs.

### 16.2 Differences vs. Current Systems

| Area | Current Systems (Epic, Oracle, Varian, Flatiron) | GenPRES Contribution |
|------|--------------------------------------------------|----------------------|
| **Order entry** | Relies on complex order sets; free‑text often used for exceptions. | Declarative regimen definitions with constraint solver prevent invalid orders. |
| **Dose calculation** | Embedded calculators (BSA, AUC) exist, but allow manual override; rounding rules vary by site. | Deterministic solver applies fixed policies with full audit of math and assumptions. |
| **Protocol adherence** | Deviation detection depends on build quality; often local customization. | Encoded directly in solver constraints, deviations impossible unless explicitly modeled. |
| **Safety checks** | Alerts configurable but prone to fatigue; overrides frequent. | Invalid options excluded up‑front; solver only generates safe/valid prescriptions. |
| **Audit trail** | Logs actions, but not the decision logic. | Stateless recomputation ensures each result can be traced to inputs and rules. |
| **Pediatric dosing** | Supported but requires separate order sets; site‑dependent quality. | Single solver model supports mg/kg, mg/m², capping, and pediatric rounding rules in one framework. |
| **Integration with compounding/admin** | Interfaces exist but rely on downstream vendor systems (BD Cato, DoseEdge). | GenPRES focuses on prescribing logic, but outputs structured orders easily consumable by compounding/admin systems. |
| **Transparency** | User sees final numbers; calculations often “black box.” | Solver provides explanation of every step, improving trust and training. |

### 16.3 Implications

- **GenPRES strengthens the prescribing and protocol adherence layers**, ensuring deterministic, reproducible, and explainable chemotherapy orders.
- **Current EHR systems remain necessary** for workflow orchestration, pump integration, and administration documentation.
- The combination of GenPRES (as prescribing logic engine) with existing compounding/administration modules would **close safety gaps** such as:
  - Elimination of invalid orders (stateless solver vs. configurable alerts).
  - Transparent dose calculation audits.
  - Protocol version governance at the solver level rather than site‑specific build.
- GenPRES thus acts as a **safety and knowledge layer** bridging between regimen definitions and operational workflows.

---

## 17. Visual Comparison Matrix (GenPRES vs. Current Systems)

| Feature | GenPRES | Epic Beacon | Oracle Health (Cerner) Oncology | Varian ARIA / Elekta MOSAIQ | Flatiron / iKnowMed | BD Cato / DoseEdge / Robots |
|---------|---------|-------------|---------------------------------|-----------------------------|----------------------|-----------------------------|
| **Order Entry** | Declarative, constraint-based; no free-text | Order sets, site-dependent; free-text possible | Integrated order sets, NCCN template support | MO modules, less standardized | Outpatient regimen libraries; free-text common | Not a prescriber; downstream only |
| **Dose Calculation** | Deterministic solver, full audit of math | Embedded calculators; overrides allowed | Embedded calculators; overrides possible | Calculators; site-customizable | Basic calculators, overrides possible | N/A |
| **Protocol Adherence** | Enforced by constraints; deviations impossible unless modeled | Build-dependent, local governance | Build-dependent | Varies by site | Site-dependent | N/A |
| **Safety Checks** | Invalid options excluded upfront | Alerts configurable; override fatigue | Alerts, site dependent | Alerts, local config required | Alerts minimal; override prone | Gravimetric/photo checks downstream |
| **Audit Trail** | Stateless recomputation, full logic trace | Logs actions but not logic | Logs; limited calculation provenance | Audits orders but not logic | Action logs only | Logs compounding steps |
| **Pediatric Dosing** | Unified solver (mg/kg, mg/m², caps, rounding) | Supported; varies by site | Adult/pediatric support; site variability | Inconsistent | Limited depth | N/A |
| **Compounding Integration** | Outputs structured orders; vendor-agnostic | Worklists/labels; compounding interfaces | Interfaces to BD Cato, DoseEdge | Relies on external systems | Relies on external systems | Best-in-class compounding/robotics |
| **Transparency** | Shows reasoning path for each calculation | Final numbers only; black box math | Displays results, no path | Opaque to users | Black box | Compounding step transparency only |

---
