# GenPRES User Guide

GenPRES is a Clinical Decision Support System (CDSS) for safe and efficient medication prescribing in (pediatric) intensive care settings.

This user guide is available in multiple languages:

| Language | Guide |
|----------|-------|
| 🇬🇧 English | [User Guide (English)](en/user-guide.md) |
| 🇳🇱 Nederlands | [Gebruikershandleiding (Nederlands)](nl/gebruikershandleiding.md) |

---

## About GenPRES

GenPRES helps clinical staff:

- Look up evidence-based dosing rules and constraints
- Perform safe medication calculations
- Verify the correct application of clinical rules

The system currently runs at <http://genpres.nl>.

Additional background (in Dutch, with a language banner) is available at <https://medicatieveiligensnel.nl>.

---

> **⚠️ Medical Disclaimer**
> GenPRES is not intended for direct clinical use without appropriate validation and regulatory approval.
> See [SUPPORT.md](../../SUPPORT.md) for the full disclaimer.

---

## Overview

GenPRES supports medication prescribing, preparation, and administration in pediatric (and adult) critical care. It performs rule lookup, dose calculations, and constraint validation based on patient-specific parameters.

This guide is aimed at:

- **New developers** onboarding to the project
- **QA testers** verifying application behaviour
- **Clinical informatics staff** evaluating the system

---

## External User Guides

Full functional user guides are maintained at the GenPRES project site. These include annotated screenshots and animated walkthroughs *(language banner available — figures and animations remain in Dutch)*:

| Guide | Description |
|-------|-------------|
| [Emergency List & Standard Infusion Pumps](https://picuwkz.nl/de-genpres-noodlijst/) | Emergency medication list and continuous infusion pump workflows |
| [Prescribing & Drug Dosing](https://picuwkz.nl/genpres-medicatie-controle/) | Step-by-step prescribing and dose-checking workflow |

---

## Documentation in this Guide

| Document | Description |
|----------|-------------|
| [Getting Started](getting-started.md) | Running the application, accessing it without patient data, entering patient data via the UI or URL parameters |
| [Testing Workflows](testing-workflows.md) | Reproducible testing procedures for QA: no-patient-context testing, unit conversion testing, basic prescribing workflow |

---

## MDR Compliance

GenPRES is developed in accordance with the European Medical Device Regulation (MDR 2017/745). The regulatory documentation (user requirements, user profile, critical tasks, risk analysis) is maintained in a separate, proprietary repository and is not part of this one.

---

## Getting Help

- **GitHub Discussions** – preferred for questions, ideas, and clinical use cases
- **GitHub Issues** – bug reports and feature requests
- **Slack** – [genpresworkspace.slack.com](https://genpresworkspace.slack.com)
- **SUPPORT.md** – [Support policy and contact information](../../SUPPORT.md)
