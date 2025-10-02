
# Protocol Draft – Health Technology Assessment of GenPRES

**Title:**  
Evaluation of GenPRES, a novel pediatric order entry and clinical decision support system (CDSS): a multicenter stepped-wedge cluster randomized trial with economic and implementation evaluation.

**Version:** Draft 1.0  
**Date:** [to be completed]  
**Principal Investigator:** [to be filled]  
**Sponsor/Institution:** [to be filled]  
**Language:** English  

---

## 1. Background and Rationale
Medication errors are a leading cause of preventable harm in pediatrics, where weight- and age-dependent dosing increases complexity. Evidence shows that CDSS integrated into Computerized Physician Order Entry (CPOE) reduces prescription errors, particularly in PICU settings.  

GenPRES is a newly developed, web-based, stateless order entry system with:  
- Structured dose calculation rules aligned with the **Kinderformularium**,  
- Cross-validation via the **G-Standaard** (“best of both worlds”),  
- Integrated support for **parenteral nutrition (TPN)**,  
- A link to the **National Emergency Medication List (Noodlijst)** for acute care.  

Although international studies support the value of CDSS in pediatrics, no Health Technology Assessment (HTA) has been performed for GenPRES, which combines unique structured dosing logic, dual-checking, and national-scale emergency integration.

---

## 2. Objectives and Research Questions

### Primary Objective
To determine whether GenPRES reduces prescribing errors per medication order compared to standard practice (templates/VMOs or existing CPOE without structured dose logic).

### Secondary Objectives
1. Efficiency: time-to-completed prescription; number of clicks/fields; acute scenario performance.  
2. Safety: adverse drug events (ADEs); overdosing; frequency/route/unit errors.  
3. Alert burden: number of alerts per 100 orders; override rates.  
4. Implementation: usability (SUS), workload (NASA-TLX), adoption, fidelity of use.  
5. Economic: incremental cost per avoided prescribing error/ADE; budget impact.  
6. Technical performance: stability, latency, error rates, audit trail coverage.  

**Hypotheses:**  
- H1: GenPRES reduces prescribing errors by ≥30% compared to control.  
- H2: GenPRES reduces prescription completion time by ≥15%.  
- H3: Structured rules (Kinderformularium + G-Standaard) decrease override rates.  

---

## 3. Study Design
**Design:** Multicenter stepped-wedge cluster randomized controlled trial (SW-cRCT).  

- Clusters: hospital units (PICU/NICU/pediatric wards).  
- Each cluster starts with control (standard CPOE), then crosses over to GenPRES in randomized order.  
- Additional analyses: Interrupted time series (ITS) within clusters; mixed-methods implementation study.  
- Substudies:  
  - Calculation validation against gold standard (clinical pharmacologist panel).  
  - Acute scenario (Noodlijst) simulation and observational study.  

---

## 4. Study Population and Setting
- **Inclusion:** Pediatric patients (0–18 years) with medication or TPN orders during the study.  
- **Exclusion:** Patients without medication orders; orders outside EHR scope.  
- **Setting:** Multiple tertiary pediatric centers with CPOE integration.  

---

## 5. Intervention and Comparator
- **Intervention:** GenPRES with structured dose rules (Kinderformularium), G-Standaard validation, TPN support, and Noodlijst integration. Accessed via EHR with URL launch and context passing.  
- **Comparator:** Current local CPOE practice (templates/VMOs or standard CDSS).  

---

## 6. Outcomes

### Primary Outcome
- Prescribing error rate per 1,000 orders (wrong dose/frequency/unit/route, contraindication, protocol deviation). Independent adjudication by blinded reviewers.

### Secondary Outcomes
- **Efficiency:** prescription time, number of clicks (log data).  
- **Safety:** ADEs detected via triggers, pharmacist interventions.  
- **Alert burden:** alerts per 100 orders, override ratio.  
- **Usability & workload:** SUS, NASA-TLX, semi-structured interviews.  
- **Implementation fidelity:** % orders via GenPRES, % correct parameter use.  
- **Acute care (Noodlijst):** time to first correct dosing decision, error rates in simulations/emergencies.  
- **System performance:** uptime, latency, audit trail completeness.  
- **Economic:** incremental cost-effectiveness (€/avoided error or ADE), budget impact (3–5 years).  

---

## 7. Data Sources and Collection
- **EHR/CPOE logs** (timestamps, orders).  
- **GenPRES logs** (order calculations, alerts, overrides).  
- **Pharmacy intervention records.**  
- **Incident reports (VIM) and ADE triggers.**  
- **Simulations and observation of Noodlijst use.**  
- **User surveys and interviews.**  

---

## 8. Sample Size
Assumptions:  
- Baseline error rate: 9/1,000 orders.  
- Target reduction: 35%.  
- ICC: 0.01–0.02.  
- Design: 6 clusters, 6 steps, α=0.05, power=0.9.  
- Required: ~40,000–60,000 orders over 12–18 months.  

---

## 9. Statistical Analysis Plan (SAP)
- **Primary analysis:** GLMM (Poisson/NegBin), random intercept per cluster, fixed effects for time and cluster.  
- **Sensitivity analyses:** ITS, per-protocol (≥80% fidelity), as-treated.  
- **Subgroups:** neonates vs. older children, high-risk meds, TPN.  
- **Alert analysis:** mixed logistic regression (predictors of overrides).  
- **Usability:** t-tests/SUS benchmarks; qualitative CFIR coding.  
- **Economic:** trial-based CEA + probabilistic sensitivity; budget impact model.  
- **Validation substudy:** non-inferiority margin 0.5% error vs. gold standard.  

---

## 10. Implementation and Technical Aspects
- **Architecture:** Stateless .NET container/IIS, HTTPS, token authentication, URL launch.  
- **Governance:** Structured update process for Kinderformularium rules and G-Standaard validations.  
- **Training:** short task-based modules, superuser model.  
- **Fallback:** rollback to baseline CPOE if safety issue detected.  

---

## 11. Ethics and Regulatory
- **Approval:** METC/IRB required (cluster trial).  
- **Consent:** clinicians informed; patient-level randomization not applicable.  
- **GDPR compliance:** pseudonymized log data, DPIA conducted, data minimization.  
- **Safety monitoring:** independent monitoring committee; real-time incident reporting.  

---

## 12. Quality Assurance and Monitoring
- Fidelity dashboards (usage, alerts, overrides).  
- Data quality audits (weights, creatinine mapping).  
- Incident response plan and rapid hotfix protocol.  

---

## 13. Timeline (18–24 months)
- Months 0–3: Setup, ethics, baseline measurement.  
- Months 4–15: Stepped-wedge rollout, data collection.  
- Months 16–20: Data analysis, economic modeling.  
- Months 21–24: Reporting, publications, policy briefs.  

---

## 14. Success Criteria
- ≥25–35% reduction in prescribing errors.  
- No increase in ADEs.  
- Faster time-to-prescription.  
- Usability score ≥70 (SUS).  
- Override ratio reduction.  
- Cost-effectiveness within willingness-to-pay thresholds.  
- Stable system performance (low downtime, reliable audit trails).  

---

## 15. Risks and Mitigation
- **Alert fatigue:** monitor overrides, tune thresholds.  
- **Data quality issues:** mandatory weight/age fields, UI validation.  
- **Adoption resistance:** co-design, superusers, responsive support.  
- **Technical failures:** sandbox testing, rollback plans.  

---

## 16. Deliverables
- Comprehensive HTA report (clinical, economic, implementation).  
- Algorithm/structured rules catalogue (Kinderformularium + G-Standaard crosswalk).  
- Peer-reviewed publications (effectiveness, methodology, implementation).  
- Executive summary for hospital boards and stakeholders.  
