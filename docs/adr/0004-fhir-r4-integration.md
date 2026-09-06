# ADR-0004: FHIR R4 EHR Integration

**Issue**: FHIR R4 EHR integration design

**Date**: 2026-04-30
**Status**: Superseded (2026-08-28) — the prototype this ADR describes was deleted
from the repository in commit `ad7bdd17` (2026-07-16, "refactor(fhir): Delete FHIR
project"), because `Informedica.FHIR.Lib` was never referenced by `GenPRES.sln` and so
was never compiled. The ADR is retained as the record that a FHIR R4 approach was chosen
and then abandoned; nothing it describes is implemented today. Any renewed EHR
integration work starts from scratch — see `docs/roadmap/backlog.md` item 7.

**Formerly**: ADR-0020, renumbered on 2026-09-06 when `docs/adr/` was made contiguous (see [ADR-0000](0000-documentation-rules.md)).

## Context

GenPRES is a Clinical Decision Support System (CDSS) that validates and calculates medication orders.
In hospital environments, Electronic Health Record (EHR) systems are the authoritative source for patient
data and the persistence layer for treatment plans. Clinical workflows require bidirectional data exchange
between GenPRES and EHR systems.

The Dutch healthcare landscape mandates:

- **G-Standaard** compliance for medication identification (GPK codes, thesauri for route and form).
- **IHE Pharmacy** profile compliance for treatment-plan messages.
- **FHIR R4** as the industry-standard interchange format.

A formal interface specification (v1.3, since removed from this repository and maintained with the MDR
documentation) defines
eleven treatment-plan scenarios (6.1–6.11) ranging from single-product once-only orders to multi-product
TPN and enteral feeding. The specification was prototyped as a proof-of-concept in `Informedica.FHIR.Lib`
(PRs #215 and #222) — since deleted, see the Status note above — but no ADR documented the
chosen architecture.

## Decision

### Stateless GenPRES, FHIR-Persistent EHR

GenPRES operates **stateless** with respect to patient records and treatment plans.

- The EHR owns all FHIR resource persistence (`MedicationRequest`, `Patient`, `Observation`).
- GenPRES provides read-only calculation, validation, and checking services.
- Temporary session state (in-flight order calculations) is held in server-side agents and discarded
  after the session; only the final `MedicationRequest` is persisted by the EHR.

This principle is documented in the interface specification (§3.4 "Stateless GenPRES with FHIR
Persistence").

### Bidirectional FHIR R4 MedicationRequest Translation

The integration layer translates between two representations:

| Direction | Description |
|-----------|-------------|
| EHR → GenPRES | `fromFhirMedicationRequest`: parse a FHIR `MedicationRequest` + patient parameters into a `FhirScenario`, look up matching dose rules from ZIndex/GenFORM, reconstruct an `OrderContext`, and run the order pipeline. |
| GenPRES → EHR | `toFhirMedicationRequest`: convert a calculated `GenOrder.OrderScenario` (with orderable quantities applied) back into a FHIR `MedicationRequest`. |

**Key invariant**: a round-trip `fromFhirMedicationRequest (toFhirMedicationRequest scenario)` preserves
Route, Indication, AdminQuantity, Rate, and Frequency. DoseType is preserved
only when unambiguously inferable from FHIR `Timing` (heuristic; see Risks).

### Separation of FHIR Context from Dose-Rule Lookup

FHIR resources describe **what was ordered** (the clinical outcome); they do not carry dosing constraints.
GenPRES derives **how to order** by looking up constraints from ZIndex/GenFORM:

```text
FHIR MedicationRequest
    → filter context (patient + indication + route + form + dose type)
    → ZIndex / GenFORM lookup
    → dose constraints + valid concentrations
    → Order pipeline (GenORDER, GenSOLVER)
    → validated OrderScenario
    → toFhirMedicationRequest
    → FHIR MedicationRequest (with checked quantities)
```

The orderable quantities and schedule (frequency, rate) are the only domain values that flow **from** the
FHIR resource into GenPRES; all other constraints come from the local ZIndex database.

### Dutch G-Standaard Coding Systems

Medication identification uses established Dutch OID systems:

| Concept | System OID |
|---------|-----------|
| GPK product codes | `urn:oid:2.16.840.1.113883.2.4.4.7` |
| Route (thesaurus 9) | `urn:oid:2.16.840.1.113883.2.4.4.9` |
| Form (thesaurus 10) | `urn:oid:2.16.840.1.113883.2.4.4.10` |
| Quantity (UCUM) | `http://unitsofmeasure.org` |

### Firely .NET SDK

The official `Hl7.Fhir.R4` NuGet package (Firely .NET SDK) was used for typed access to FHIR R4
model objects (`MedicationRequest`, `Dosage`, `Timing`, `Quantity`, etc.), avoiding manual JSON/XML
serialization. The prototype obtained it the way an FSI script does — `#r "nuget: HL7.Fhir.R4"` in
`Scripts/load.fsx` — and never through Paket: the package appears in neither `paket.dependencies`
nor `paket.lock`, which is consistent with `Informedica.FHIR.Lib` never having been part of
`GenPRES.sln` or compiled. Reinstating this design would mean adding the dependency properly.

### Scripts-First Implementation

Following the established GenPRES workflow ([AGENTS.md](../../AGENTS.md)):

1. FHIR translation logic was prototyped in `.fsx` scripts before migration to
   `Informedica.FHIR.Lib` source files. That migration never happened and the scripts were
   deleted with the project.
2. All eleven interface scenarios (6.1–6.11) are expressed as typed `FhirScenario` records and
   exercised via an end-to-end round-trip test.
3. Expecto tests covered each scenario's translation output shape and round-trip fidelity.

## Consequences

### Benefits

- **Standard interoperability**: FHIR R4 + G-Standaard is the Dutch national standard for hospital
  medication exchange; adoption reduces bespoke integration work for each EHR partner.
- **Separation of concerns**: EHR owns persistence; GenPRES owns clinical calculation. Neither system
  must understand the other's internal domain model in depth.
- **Type safety**: The Firely .NET SDK provides compile-time checked access to FHIR model types,
  reducing serialization bugs.
- **Validated scenarios**: Eleven concrete scenarios from the interface specification serve as
  acceptance tests for the integration layer.
- **Traceability**: Every translation is auditable — the input `MedicationRequest` and output
  `OrderScenario` can be logged.

### Trade-offs and Risks

- **GPK placeholder limitation**: The interface specification uses placeholder GPK codes. Real codes
  must come from the G-Standaard database; the round-trip is only fully verifiable with real product
  data.
- **FHIR version lock**: Committing to FHIR R4 means future migration to FHIR R5 will require a
  translation shim or a parallel implementation.
- **Partial DoseType inference**: Inferring GenPRES `DoseType` (Once / OnceTimed / Discontinuous /
  Timed / Continuous) from FHIR `Timing` is heuristic — the FHIR schema has no direct equivalent.
  Misclassification can produce incorrect dose-rule lookup results.
- **No write operations in Phase 1**: The current scope is read-only calculation. Phase 2 (EHR write-
  back of validated `MedicationRequest`) requires a separate ADR and authentication design (OAuth2 /
  SMART-on-FHIR).

## Considered Alternatives

### HL7 v2 Messaging

Rejected: HL7 v2 is legacy, has no standard Dutch medication profile, and lacks the structured
quantitative types needed for GenPRES dose calculations.

### Proprietary REST API (no FHIR)

Rejected: Every EHR partner would require a custom adapter. FHIR R4 is the Dutch interoperability
standard (NEN 7512, Zorg-AB, MedMij); a proprietary API would exclude most hospital EHR systems.

### FHIR R5

Considered but deferred: FHIR R5 is not yet the Dutch regulatory baseline. The interface specification
(v1.3, September 2025) targets R4. Migration can be revisited once R5 adoption is mandated.

### Server-Side Proxy (GenPRES as FHIR server)

Considered: GenPRES could expose a full FHIR server endpoint (FHIR `$process-message` or
`$apply`). Rejected for Phase 1 because it requires significant FHIR server infrastructure and
authentication plumbing not yet needed for the calculation-service use case. Deferred to Phase 2.

## References

- Interface specification v1.3: removed from this repository under #522; maintained with the MDR documentation
- FHIR R4 specification: <https://hl7.org/fhir/R4/>
- Firely .NET SDK: <https://docs.fire.ly/projects/Firely-NET-SDK/>
- Dutch G-Standaard / NL FHIR: <https://informatiestandaarden.nictiz.nl/wiki/Landingspagina_Medicatie>
- IHE Pharmacy profile: <https://www.ihe.net/resources/technical_frameworks/#pharmacy>
- [ADR-0002: MCP Server Architecture](0002-mcp-server-architecture.md)
- [Security baseline (formerly ADR-0015)](../security/security-baseline.md)
- PR #215: Scripts (FHIR): Add `ImplementationPlan.fsx`
- PR #222: Scripts (FHIR): Fix `ImplementationPlan.fsx` — major rework with correct FHIR property mappings
