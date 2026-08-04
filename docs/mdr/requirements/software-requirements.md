# Software Requirements

Software Requirements Specification for GenPRES

> **Version**: 1.1 — May 2026
> **Author**: Software Architect, GenPRES Project
> **Change from v1.0 (May 2025)**: Updated to reflect .NET 10 toolchain, FHIR R4 integration design (ADR-0020), implemented MCP/LLM integration, and expanded security baseline (ADR-0015).

---

## 1. System Overview

GenPRES is a clinical decision support system for prescribing and managing medication, including nutrition and TPN, within hospital environments such as pediatric and neonatal intensive care units. It is a web-based, stateless application designed for integration with any Electronic Patient Record (EPR/EPD) system via URL parameters.

---

## 2. Platform Requirements

- The application targets **.NET 10** or later (see [Toolchain Requirements in `DEVELOPMENT.md`](../../../DEVELOPMENT.md#toolchain-requirements) for the canonical version).
- Can be deployed as a Docker container for cloud-native or local deployments; the proprietary spreadsheet ID (`GENPRES_URL_ID`) and admin password (`GENPRES_PASSWORD`) are injected at container runtime as environment variables and are **not** baked into the image.
- Alternatively, can be hosted on Microsoft IIS for traditional Windows-based server environments.
- Operates in a stateless configuration, ensuring scalability and independent operation.

---

## 3. User Interface Requirements

- The user interface is fully web-based and built using **Material UI v9** with **React 19** / **Fable 5**.
- Compatible with all modern web browsers (Chrome, Firefox, Safari, Edge).
- Fully responsive design to support usage on desktops, tablets, and mobile devices.
- Accessible via URL parameters to support EPD integration (e.g., `https://genpres.app/start?patientId=123&sessionId=abc`).
- Language-selectable UI (English/Dutch); localised terms centralised in a `Terms` discriminated union.

---

## 4. Integration and Interoperability

- Designed to be invoked via URL from any EPD system.
- Does not maintain internal session state — each session must include required parameters (e.g., patient ID, context).
- **FHIR R4 integration** (designed): bidirectional `MedicationRequest` translation with Dutch G-Standard GPK coding (`urn:oid:2.16.840.1.113883.2.4.4.7`); implementation approach documented in ADR-0020 (`docs/mdr/design-history/0020-fhir-r4-integration.md`).
- **MCP stdio server** (`Informedica.MCP.Server`): exposes GenFORM and GenORDER APIs as Model Context Protocol tools; enables AI-assisted prescribing workflows (ADR-0009).
- **NLP dose-rule extraction pipeline**: semi-automated extraction of dose rules from Dutch formulary (NKF/FTK) free text using LLM function-calling; documented in ADR-0018 (`docs/mdr/design-history/0018-nlp-dose-rule-extraction.md`).
- Future versions will include full integration with a clinical database for longitudinal tracking.

---

## 5. Functional Requirements

- Provide support for prescribing oral, parenteral, and TPN medications.
- Validate medication dosages based on patient parameters (weight, age, renal function).
- Alert users of drug interactions and protocol violations; dose-check severity levels (Valid / Caution / Warning / Alert) are colour-coded in the UI.
- Enable TPN calculation and order export.
- Support multilingual UI and error messages.
- Apply G-Standard dose-rule fallback for medications without a GenFORM spreadsheet entry (ADR-0016).
- Compute clinical parameters — body surface area (BSA), eGFR, patient age — using shared, Fable-compatible calculation modules with F# units of measure for compile-time safety (ADR-0019).

---

## 6. Clinical Knowledge Source and Change Control

The Operational Knowledge Rules that drive prescribing — dose rules, solution
rules, reconstitution rules, renal rules, and the hospital formulary — are
authored outside the application, in the Google spreadsheet identified by
`GENPRES_URL_ID`, and loaded at startup (ADR-0001). The rules are therefore
changeable without a code release, and the following controls apply to the
*content*, independently of the software release process:

- Dose rule changes require clinical pharmacist review before they are released.
- Solution rule changes require pharmacy (IV compounding) validation.
- Renal rule changes require nephrology review.
- Emergency treatment protocol changes require intensive-care review.
- Rule changes must be traceable: the spreadsheet retains revision history, and a
  change takes effect only on application restart or explicit resource reload, so
  the rule set in force for any running instance is identifiable.

The *structure* of that data — which sheets exist, which columns each carries,
their units and encodings — is not specified here. It is defined by the parsers
and the `Data` record types in `Informedica.GenFORM.Lib` (`Types.fs`) and
enforced by the sheet column-contract tests in `Informedica.GenFORM.Tests`, so
that the specification cannot drift from the implementation that must honour it.

---

## 7. Security Requirements

The security baseline is documented in ADR-0015 (`docs/mdr/design-history/0015-security-baseline.md`). Key controls:

- Use secure HTTPS for all communications.
- Token-based admin authentication; constant-time password comparison prevents timing attacks.
- Production mode enforcement: server refuses to start without a `GENPRES_PASSWORD` of at least 16 characters when `GENPRES_PROD=1`.
- Content-Security-Policy, HSTS, `X-Content-Type-Options`, and `Referrer-Policy` headers applied.
- `X-Forwarded-For` spoofing closed on rate limiter.
- `TypeNameHandling.Auto` disabled in JSON deserialiser to eliminate gadget-chain RCE vector.
- Logging of all relevant server actions; `GENPRES_URL_ID` masked in startup banner.

---

## 8. Deployment & Scalability

- Stateless design supports horizontal scaling in containerised environments.
- Configurable environment variables for multi-tenant or hospital-specific deployments.
- Runs independently of any particular infrastructure provider.
- Graceful shutdown on SIGTERM/SIGINT — active agents and connections are cleanly terminated.

---

## 9. Future Enhancements

- Integration with clinical databases for longitudinal tracking.
- Expanded AI capabilities: anomaly detection, dosage suggestion refinement.
- Full FHIR R4 EHR integration (interface implementation, EHR integration testing, interoperability validation — see ROADMAP W7).
- Build system improvements: version management, automated release artifacts, Docker publish on tag (see issue #234).

---

> **Version**: 1.1 — May 2026
> **Author**: Software Architect, GenPRES Project
