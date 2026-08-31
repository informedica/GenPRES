# User Requirements

User Requirements for GenPRES

## 1. Introduction

These user requirements define what healthcare professionals, administrators, and technical stakeholders expect from GenPRES, a clinical decision support software system used in hospital environments such as pediatric and neonatal intensive care units.

## 2. General Requirements

- **UR-001**: The system shall be accessible through a modern web browser without requiring additional software installation.
- **UR-002**: The user interface shall be intuitive, responsive, and optimized for various screen sizes including tablets and mobile devices.
- **UR-003**: The system shall hold no server state beyond the short-lived session that a launch establishes, so that instances remain independent and horizontally scalable.
- **UR-004**: The system shall integrate into any EPD through a single-use launch token. The launch URL carries that token and nothing else; patient and user identifiers shall never appear in it.

## 3. Clinical Use Requirements

- **UR-005**: The system shall support the prescription of oral, parenteral, and TPN medications.
- **UR-006**: The system shall provide dose calculations based on patient-specific data such as weight, age, and renal function.
- **UR-007**: The system shall generate alerts when drug interactions, protocol violations, or unsafe doses are detected.
- **UR-008**: The system shall allow users to adjust active prescriptions and view historical changes.
- **UR-009**: The system shall fully support the G-Standard for product information, dose checking, and interaction control.
- **UR-010**: The system shall include the full configuration of the Dutch Pediatric Formulary (Kinderformularium).

## 4. AI and Decision Support

- **UR-011**: The system shall provide AI-generated suggestions for medication adjustments based on structured clinical data.
- **UR-012**: The system shall allow interaction with a local language model to assist with dose reasoning and interpretation.

## 5. TPN Management

- **UR-013**: The system shall calculate TPN composition automatically based on patient needs.
- **UR-014**: The system shall allow export or printing of TPN orders for pharmacy preparation.

## 6. Security and Auditability

- **UR-015**: The system shall log all user actions related to prescriptions for audit purposes.
- **UR-016**: The system shall ensure secure access through integration with hospital authentication mechanisms.

## 7. Reliability and Availability

- **UR-017**: The system shall function independently from the EPD, ensuring availability even if other systems are down.
- **UR-018**: The system shall recover gracefully from network disruptions.

## 8. Usability

- **UR-019**: The user interface shall present selectable options at each step to ensure that all user actions result in valid outputs.
- **UR-020**: The design of the interface shall prioritize efficiency and intuitiveness, minimizing the likelihood of user errors.

---

**Version**: 1.1  
**Date**: 2026-08-28  

> **Change from v1.0 (May 2025)**: UR-003 and UR-004 restated. The original wording required
> stateless usage with patient context carried in URL parameters; `software-requirements.md`
> v1.2 (August 2026) replaced that with a single-use launch-token handoff and a short-lived
> server-held session. These requirements now follow it.

**Author**: Clinical Product Owner, GenPRES Project