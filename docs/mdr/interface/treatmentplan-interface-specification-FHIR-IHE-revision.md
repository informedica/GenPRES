
# GenPRES–EHR Treatment Plan Interface — **FHIR/IHE Compliance Revision** (v1.1)

> This document revises the minimal set of sections required to make the original
> specification FHIR R4 / IHE Pharmacy–compliant. All sections not shown here remain as‑is.

**Effective date:** 2026-01-01  
**Scope of change:** Sections 5.1, 5.2, 5.3, 5.4, 7.1, 7.4, and Appendix D.

---

## 5.1 Patient Context Transfer (EHR → GenPRES) — Revised

- Payload is a FHIR **Bundle.type=collection**.
- Required: Patient, Practitioner/Role, Organization.
- Recommended: Encounter, Condition, AllergyIntolerance, MedicationStatement, Observation (labs/vitals UCUM), Device.
- Provenance included where available.
- Medication codes include G‑Standard GPK in `Medication.code.coding` or `Medication.identifier`.

### Example
```json
{
  "resourceType": "Bundle",
  "type": "collection",
  "entry": [
    { "resource": { "resourceType": "Patient", "id": "pat-123" } },
    { "resource": { "resourceType": "Observation", "status": "final",
      "code": { "coding":[{"system":"http://loinc.org","code":"29463-7"}] },
      "valueQuantity": { "value": 14.2, "unit": "kg", "system":"http://unitsofmeasure.org","code":"kg" } } }
  ]
}
```

---

## 5.2 Treatment Plan Request (EHR → GenPRES) — Revised

- Express intent as **RequestGroup** (proposal) and/or **CarePlan**.
- Monitoring: `ServiceRequest` resources.
- Prescriber context: `PractitionerRole`.

---

## 5.3 Complete Treatment Plan Response (GenPRES → EHR) — Revised

- Return **Bundle.type=transaction** with:
  - CarePlan (draft/plan)
  - RequestGroup (proposal)
  - MedicationRequest[] (draft/plan)
  - Medication[] (with GPK coding, ingredients for compounding)
  - NutritionOrder[] (feeding)
  - ServiceRequest[] (labs)
  - Provenance/AuditEvent

### Example
```json
{
  "resourceType":"Bundle",
  "type":"transaction",
  "entry":[
    { "resource": { "resourceType":"CarePlan","id":"cp1","status":"draft","intent":"plan"} },
    { "resource": { "resourceType":"MedicationRequest","status":"draft","intent":"plan"} }
  ]
}
```

---

## 5.4 Order Scenario Specification — Revised

- Serialize each scenario as **MedicationRequest** (+Medication) or **NutritionOrder**.
- Use UCUM units, SNOMED/FHIR route codes.
- Clinical flags → extensions or DetectedIssue.

---

## 7.1 API Endpoints — Clarified

- **/bootstrap**: accepts `Bundle.type=collection` (application/fhir+json).
- **/finalize**: returns `Bundle.type=transaction`.
- **/simulate**: optional dry‑run.

---

## 7.4 Security & Audit — Revised

- OAuth2 / SMART‑on‑FHIR bearer tokens.
- IHE ATNA: emit AuditEvent.
- IHE IUA compatible token usage.
- Least‑privilege scopes.

---

## Appendix D — OpenAPI Excerpt

```yaml
paths:
  /bootstrap:
    post:
      requestBody:
        content:
          application/fhir+json:
            schema:
              $ref: '#/components/schemas/FhirBundle'
  /finalize:
    post:
      responses:
        '200':
          content:
            application/fhir+json:
              schema:
                $ref: '#/components/schemas/FhirBundle'
components:
  schemas:
    FhirBundle:
      type: object
      properties:
        resourceType: { const: Bundle }
        type:
          type: string
          enum: [collection, transaction]
```
