# GenFORM: from Free Text to Operational Knowledge Rules

## Table of Contents

- [GenFORM: from Free Text to Operational Knowledge Rules](#genform-from-free-text-to-operational-knowledge-rules)
  - [Table of Contents](#table-of-contents)
  - [Core Definitions](#core-definitions)
  - [1. Objectives](#1-objectives)
  - [2. Operational Knowledge Rules](#2-operational-knowledge-rules)
  - [3. Sources and Types of Dose Rules](#3-sources-and-types-of-dose-rules)
  - [4. Additional Information](#4-additional-information)
  - [5. Operational Structure of a Dose Rule](#5-operational-structure-of-a-dose-rule)
  - [6. Selection and Calculation Constraints](#6-selection-and-calculation-constraints)
    - [6.1. Selection Constraints](#61-selection-constraints)
    - [6.2. Calculation Constraints](#62-calculation-constraints)
  - [7. Prescription Rules](#7-prescription-rules)
  - [8. Related Documents](#8-related-documents)
    - [8.1. Pipeline Position](#81-pipeline-position)
    - [8.2. Patient Category vs Patient](#82-patient-category-vs-patient)
    - [8.3. Key Terminology Alignment](#83-key-terminology-alignment)
  - [Appendices](#appendices)
    - [Appendix A. The Medication Treatment Cycle](#appendix-a-the-medication-treatment-cycle)
    - [Appendix B.1. GenFORM Conceptual Architecture](#appendix-b1-genform-conceptual-architecture)
    - [Appendix B.2. GenFORM Technical Architecture](#appendix-b2-genform-technical-architecture)
    - [Addendum B.3. GenFORM Libraries](#addendum-b3-genform-libraries)
    - [Appendix C.1. Dose Rule Model Figure](#appendix-c1-dose-rule-model-figure)
    - [Addendum C.2. Dose Rule Model Table](#addendum-c2-dose-rule-model-table)
    - [Addendum C.1. Reconstitution and Dilution Rule Model Figure](#addendum-c1-reconstitution-and-dilution-rule-model-figure)
    - [Addendum C.2. Reconstitution Rule Model Table](#addendum-c2-reconstitution-rule-model-table)
    - [Addendum C.3. Dilution Rule Model Table](#addendum-c3-dilution-rule-model-table)
    - [Addendum D.1. Renal Rule Model Figure](#addendum-d1-renal-rule-model-figure)
    - [Addendum D.2. Renal Rule Model Table](#addendum-d2-renal-rule-model-table)
    - [Addendum E.1. Product Component Model Figure](#addendum-e1-product-component-model-figure)
    - [Addendum E.2. Product Component Model Table](#addendum-e2-product-component-model-table)

## Core Definitions

| Term | Definition |
| ----- | ----- |
| *GenFORM* | A generic formulary system that transforms free-text medication knowledge into fully computable Operational Knowledge Rules (OKRs). |
| *Operational Knowledge Rule (OKR)* | A fully structured, machine-interpretable and constraint-based representation of medication knowledge used for prescribing, preparation, and administration. |
| *Dose Rule* | An OKR that defines qualitative and quantitative constraints for dosing a specific generic in a defined clinical context. |
| *Dose Limit* | A set of numeric constraints defining the minimum, maximum, or normative allowable dose. |
| *Dose* | A patient-specific calculated dose value that satisfies all applicable constraints. Note that a dose can be per administration, per time unit, or rate unit and can be adjusted for patient weight or BSA. |
| *Dose Type* | The temporal category of dosing (e.g., once, timed, continuous). |
| *Selection Constraint* | A rule element used to determine which calculation constraints apply. |
| *Calculation Constraint* | A quantitative rule element used to compute dose, rate, volume, or timing. |
| *Adjustment Unit* | A patient normalization unit used to scale doses (e.g., kg for weight, m² for BSA). |
| *Relative Adjustment* | A renal or clinical adjustment expressed as a multiplier of the base dose. |
| *Absolute Adjustment* | A renal or clinical adjustment expressed as an absolute numeric replacement of a dose. |
| *Reconstitution* | The process of converting a medication into an administrable form by adding a diluent. |
| *Expansion Volume* | The increase in total volume resulting from reconstitution. |
| *Dilution* | The process of further adjusting concentration and volume for safe administration. |
| *Administration Fraction* | The fraction of the prepared solution used to deliver the calculated dose. |
| *Generic Product (GPK)* | An abstract pharmaceutical product definition independent of branding. |
| *Prescription Product (PRK)* | A prescribable form of a generic product. |
| *Trade Product (HPK)* | A branded pharmaceutical product. |
| *Consumer Product (ZInr)* | A retail package variant of a trade product. |

## 1. Objectives

The aim of GenFORM is to extract and structure expert medication-related knowledge, together with all additional operational information, so that medication can be automatically prescribed, planned, prepared, and administered (see Addendum A. The Medication Treatment Cycle).

GenFORM (Generic FORMulary) is a generic solution that produces Operational Knowledge Rules (OKRs): fully computable, constraint-based representations of all medication knowledge relevant to prescribing, preparing, and administering medication.

To create OKRs, free text or semi-structured clinical texts are transformed into a fully structured and machine-interpretable format. The resulting OKRs define explicit constraints on indications, dosing, scheduling, preparation, administration, product availability, and patient-specific parameters.

Because OKRs are expressed as constraints, they can be used by a constraint-solving engine to compute all valid options for a given clinical context. The user selects the most appropriate option, and this selection acts as an additional constraint that further narrows the solution space. Consequently, every option presented—and every option chosen—remains, by definition, within the boundaries defined by the OKRs and is therefore guaranteed to be valid.

## 2. Operational Knowledge Rules

OKRs should allow for:

1. Selecting the specific scenario determined by:  
   1. The indication and  
   2. The medication (generic naming) and  
   3. The route of administration and/or  
   4. The pharmaceutical form and  
   5. The type of dosing

In addition, the following context information for a specific patient and clinical situation is required:

2. The setting: location/department and  
3. The patient: gender, and min/max ranges for age, weight, body surface area (BSA), gestational age, and post-menstrual age.

Together, all of the above rule items form a constraint qualifying system that determines which actual quantitative dose rule settings are applicable. These quantitative settings are:

4. The specific available medication product assortment that can be used  
5. Dose schedule settings and  
6. Dose limit settings

The dose schedule settings contain all quantitative constraints to allow calculation of the dose scheduling, the dose limit settings contain the quantitative constraints to allow calculation of the actual dose.

Finally, dose adjustment rules like renal rules can apply that are used to adjust schedule and/or dose limit settings.

## 3. Sources and Types of Dose Rules

Currently, dose rules are defined across multiple source documents and repositories. These rules can be categorized into two primary types:

1. **Dose checking rules**: Rules that verify whether a prescribed dose falls within acceptable margins, primarily ensuring that no over- or underdosing occurs.  
2. **Dose advice rules**: Rules that specify recommended exact dose(s) within the boundaries set by the checking rules or that enable calculation of exact dose(s).

Dose checking rules are maintained in the national registry, the G-Standaard. More specific dose advice rules are available in several additional sources:

- *Farmacotherapeutisch Kompas*: contains dose advice rules for adults and part of the pediatric population.  
- *Kinderformularium*: contains all pediatric dose advice rules.  
- *SWAB (Stichting Werkgroep Antibiotica Beleid)* guidelines: contain antibiotic-specific dose advice rules.  
- Oncology protocols: contain dose advice rules for oncology medications.  
- Local protocols: contain hospital- or specialty-specific dose advice rules.

## 4. Additional Information

To establish a structured and operational knowledge-rule system that supports prescribing, planning, preparation, and administration, additional information is required:

- **Reconstitution rules**: define how medication must be reconstituted to enable administration of, for example, a powder as a liquid.  
- **Dilution rules**: define requirements for the amount and/or concentration of liquid medication.  
- **Renal rules**: used to adjust the dose advice according to the renal function (GFR normal, too low or too high).  
- **Product information**: defines product availability, pharmaceutical forms, and parameters required for calculating the delivered dose.

Reconstitution and dilution rules are typically summarized in parenteralia, while the corresponding information for oral medication is provided in oralia.

For renal rules specific sections are available in the Kinderformularium or specific renal reference guidelines.

All required product information can be obtained from the G-Standaard.

## 5. Operational Structure of a Dose Rule

In order to be available as an OKR, the dose rule needs to be structured so that:

1. The context can be specified so it can be narrowed down to specifically one dose rule.  
2. The resulting dose rule needs to contain all information necessary to perform a full calculation of prescription, preparation and administration of a medication.  
3. Every possible dosing scenario should be able to be encoded as an operational dose rule structure.

This results in the dose rule structure as shown in Appendix D. The Dose Rule Structure.

## 6. Selection and Calculation Constraints

All OKRs can be translated to either selection or calculation constraints. The selection constraints determine which calculation constraints are available. For every set of calculation constraints there is exactly one set of selection constraints that uniquely identifies the calculation constraints (i.e. the dose).

### 6.1. Selection Constraints

- Dose Rule  
  - Source  
  - Generic  
  - Indication  
  - Route  
  - Setting  
  - Patient  
  - Dose Type  
  - Component  
  - Substance  
- Reconstitution Rule  
  - Generic  
  - GPK  
  - Form  
  - Route  
  - Setting  
- Dilution Rule  
  - Generic  
  - Form  
  - Route  
  - Indication  
  - Dose Type  
  - Setting  
  - Administration Access Device (e.g., CVL/PVL)
  - Patient  
  - Dose  
  - Substance  
- Renal Rule  
  - Source  
  - Generic  
  - Indication  
  - Patient  
  - Renal Function

### 6.2. Calculation Constraints

- Dose Rule  
  - Schedule  
  - Duration  
  - Dose Limits  
- Reconstitution Rule:  
  - Diluent Volume  
  - Expansion Volume  
- Dilution Rule:  
  - Volume  
  - Drip Rate  
  - Administration Fraction  
  - Dose  
  - Concentration  
- Renal Rule  
  - Schedule  
  - Dose Adjustment (relative or absolute)

## 7. Prescription Rules

Dose-, Reconstitution-, Dilution- and Renal rules are combined within a patient context and specific setting into a Prescription Rule. The Prescription Rule can, therefore, be used to generate an order determining, prescription, preparation and administration of an order.

## 8. Related Documents

GenFORM is part of the GenPRES transformation pipeline. The following documents provide additional context:

| Document | Description | Relationship |
|----------|-------------|---------------|
| [Core Domain Model](./core-domain.md) | Central domain definitions and transformation pipeline | GenFORM is the **Layer 1** component transforming free text to OKRs |
| [GenORDER](./genorder-operational-rules-to-orders.md) | Transforms OKRs to Order Scenarios | Consumes OKRs produced by GenFORM |
| [GenSOLVER](./gensolver-from-orders-to-quantitative-solutions.md) | Constraint solving engine | Provides algorithmic foundation used by GenORDER |

### 8.1. Pipeline Position

```text
Free Text → [GenFORM] → OKRs → [GenORDER] → Order Scenarios → [GenSOLVER] → Quantitative Solutions
```

GenFORM occupies the **first transformation stage**, responsible for:
1. Parsing free-text and semi-structured clinical texts
2. Extracting Selection Constraints and Calculation Constraints
3. Producing fully computable Operational Knowledge Rules (OKRs)

### 8.2. Patient Category vs Patient

GenFORM defines **Patient Categories** (also referred to as "Patient" in rule tables)—these are *types* of patients characterized by demographic ranges (age, weight, BSA, gestational age). A Patient Category is not a specific individual but rather a classification used to determine which rules apply. 

In contrast, [GenORDER](./genorder-operational-rules-to-orders.md) works with a specific **Patient** instance—an actual individual with concrete demographic values. GenORDER matches the Patient's attributes against GenFORM's Patient Categories to select the applicable OKRs.

For a specific patient, a matching property like BSA can be calculated from weight and height (e.g., Mosteller: $\mathrm{BSA}(m^2)=\sqrt{(\mathrm{height}_{cm}\cdot\mathrm{weight}_{kg})/3600}$).

### 8.3. Key Terminology Alignment

| GenFORM Term | Related Term in Other Documents | Notes |
|--------------|--------------------------------|-------|
| Operational Knowledge Rule (OKR) | Rule (Core Domain) | Umbrella term for all computable rules |
| Dose Rule | Dose Rule (GenORDER) | Applied to specific patient context in GenORDER |
| Dilution Rule | Dilution Rule (GenORDER) | Formerly "SolutionRule" in some contexts |
| Patient (in rule tables) | Patient Category (GenORDER) | GenFORM defines categories; GenORDER matches patients |
| Selection Constraint | Filter Stage (GenORDER) | Determines rule applicability |
| Calculation Constraint | Solver Stage (GenORDER) | Provides quantitative bounds |

## Appendices

The below appendices provide graphical and table detailed representations of all rule related concepts.

In tables, mutually exclusive options are represented by a bar "|".

### Appendix A. The Medication Treatment Cycle

![image1](https://docs.google.com/drawings/d/e/2PACX-1vRenb7b36iQWiNvOj3_KiRkMFABNwlt_xOA2lWkWMo24-2SFmhNOtU9uzOMtGF4-hC67rOs6pO9tDJI/pub?w=960&h=599)

### Appendix B.1. GenFORM Conceptual Architecture

![image2](https://docs.google.com/drawings/d/e/2PACX-1vTyeou3dSXc9foR4wx2IvVmNCdXF3PM7RN9O2mElMuRY3de77eVDk9oYn8LfsU7u_JOBPd7ANVd_EA2/pub?w=958&h=654)  

Free text is structured by GenFORM to operational structured knowledge rules and can be returned as human-readable text.

### Appendix B.2. GenFORM Technical Architecture

![image3](https://docs.google.com/drawings/d/e/2PACX-1vSopywjJaeck4Ta8vAN52rDbbdtqOFfh0Orox7YW8fYLek_ntEGC6xBH_LAoZGUkY1pjvzx732TFslL/pub?w=778&h=577)

### Addendum B.3. GenFORM Libraries

Classification:

- **Free Text Sources**  
- **Utility Library**  
- **Domain Library**

Libraries:

1. **Informedica.Agents.Lib:** Library using the FSharp MailboxProcessor to create message based agents.
   - Capabilities:
     - Message-based agent abstraction using F# MailboxProcessor  
     - Asynchronous, concurrent API execution model  
     - Specific low level agent implementations

2. **Informedica.Logging.Lib:** Library with advanced printing capabilities to enable human-readable logging for analysis.
   - Capabilities:  
     - Human-readable logging and structured output for analysis  
     - Advanced printing and formatting capabilities  
     - Agent-based logging service  

3. **Informedica.NLP.Lib:** Library using NLP to extract structured rules from free text.
   - Capabilities:  
     - Natural language processing for rule extraction  
     - Conversion of free text to structured typed records

4. **Informedica.OTS.Lib:** Library to retrieve and send rules sets to the OTS server for storage and version control. Can also export and import rules sets as spreadsheets.  
   - Capabilities:  
     - Integration with OTS server for product and rule set storage and version control  
     - Rule set import/export to spreadsheet formats  
     - Can log import and export  
     - Can run as an agent based microservice

5. **Informedica.GenUnits.Lib:** Domain library handling complex value unit combinations and calculation.  
   - Capabilities:  
     - Complex value-unit combinations and dimensional analysis  
     - Unit conversion and validation  
     - Arithmetic operations on values with units

6. **Informedica.ZIndex.Lib:** Performs first parsing of products and rules from the ZIndex registry. Products can be used by GenFORM.
   - Capabilities:  
     - Parsing of pharmaceutical products from ZIndex registry  
     - Initial rule extraction from ZIndex data  
     - Product catalog and formulary management

7. **Informedica.ZForm.Lib:** Additional processing of rules to more generic constructs. These rules can be used by GenFORM to check specific dose rules.  
   - Capabilities:  
     - Advanced rule processing and normalization  
     - Conversion of ZIndex rules to generic constructs  
     - Can store and retrieve products and rules in OTS  
     - Can log extraction messages  
     - Can run as an agent microservice  
     - Can be exposed via MCP host

8. **Informedica.NKF.Lib:** Library used to parse structured content from the Kinderformularium containing pediatric dosing rules.
   - Capabilities:  
     - Parsing of pediatric dosing guidelines from Kinderformularium  
     - Structured pediatric dosing rules  
     - Can log extraction messages  
     - Can run as an agent based microservice  

9. **Informedica.FTK.Lib:** Library used to parse structured content from the Farmacotherapeutisch Kompas containing pediatric dosing rules.
   - Capabilities:  
     - Parsing of adult dosing guidelines from Farmacotherapeutisch Kompas  
     - Structured adult dosing rules  
     - Can log extraction messages  
     - Can run as an agent based microservice

10. **Informedica.GenFORM.Lib:** Handles all rule sets and combines those with products creating specific patient context prescription rules.
    - Capabilities:  
      - Unified rule set management across all sources  
      - Patient-context-specific prescription rule generation  
      - Product-rule combination and validation  
      - Can store and retrieve products and rules in OTS  
      - Can log all rule retrieval messages  
      - Can run as an agent based microservice  
      - Can be accessed as a MCP service

11. **Informedica.MCP.Lib:** Library enabling a MCP implementation and using LLMs.
    - Capabilities:  
      - Model Context Protocol (MCP) framework implementation in F#  
      - LLM-powered extraction of structured typed records from free text  
      - Agent-based MCP service execution  
      - Can host ZForm, GenFORM, GenORDER

### Appendix C.1. Dose Rule Model Figure

![image4](https://docs.google.com/drawings/d/e/2PACX-1vQ0JtMXGCuyZ4Tw_EjHErHbvI7b5qXSJjTQsveI8kBbRPyAkh1RzTtw_NsbaPNyiKYgPufPWAk-ZduD/pub?w=1042&h=710)

### Addendum C.2. Dose Rule Model Table

| Object | Variable | Type | Unit | Description |
| :---- | :---- | :---- | :---- | :---- |
| Source | Name | text |  | The source of the dose rule |
| Source | Text | text |  | The source dose description |
| Generic | Name | text |  | The generic name |
| Generic | Form | text |  | Pharmaceutical form to narrow down the dose rule |
| Generic | Brand | text |  | A brand to narrow down the dose rule |
| Generic | GPKs | text list |  | A list of GPKs to narrow down the dose rule |
| Indication | Indication | text |  | The indication (label) for the dose rule |
| Route | Route | text |  | The route |
| Setting | Location | text |  | Hospital/institute/organization |
| Setting | Department | text |  | The department |
| Patient | Gender | male / female |  | The gender |
| Patient | MinAge | int | day | The minimum age |
| Patient | MaxAge | int | day | The maximum age |
| Patient | MinWeight | int | gram | The minimum weight (in gram) |
| Patient | MaxWeight | int | gram | The maximum weight |
| Patient | MinBSA | float | m2 | The minimum bsa |
| Patient | MaxBSA | float | m2 | The maximum bsa |
| Patient | MinGestAge | int | day | The minimum gestational age |
| Patient | MaxGestAge | int | day | The maximum gestational age |
| Patient | MinPMAge | int | day | The minimum post-menstrual age |
| Patient | MaxPMAge | int | day | The maximum post-menstrual age |
| DoseType | DoseType | once / onceTimed / discontinuous / timed / continuous |  | The dose type of the dose rule |
| DoseType | DoseText | text |  | A label for a dose type |
| Schedule | Freqs | int list | count_unit / freq_unit | The possible frequencies |
| Schedule | FreqUnit | text |  | The freq unit |
| Schedule | MinTime | float | time_unit | The minimum time for infusion of a dose |
| Schedule | MaxTime | float | time_unit | The maximum time for infusion of a dose |
| Schedule | TimeUnit | text |  | The time unit to measure the infusion |
| Schedule | MinInt | float | int_unit | The minimum interval between two doses |
| Schedule | MaxInt | float | int_unit | The maximum interval between two doses |
| Schedule | IntUnit | text |  | The interval unit |
| Schedule | MinDur | float | dur_unit | The minimum duration of the dose rule |
| Schedule | MaxDur | float | dur_unit | The maximum duration of the dose rule |
| Schedule | DurUnit | text |  | The duration time unit |
| Component | Component | text |  | The component the substance belongs to |
| Substance | Substance | text |  | The substance used for the below fields |
| DoseLimit | DoseUnit | text |  | The dose unit |
| DoseLimit | AdjustUnit | kg / m2 |  | The adjust unit |
| DoseLimit | RateUnit | text |  | The rate unit |
| DoseLimit | MinQty | float | dose_unit | The minimum dose quantity |
| DoseLimit | MaxQty | float | dose_unit | The maximum dose quantity |
| DoseLimit | NormQtyAdj | float | dose_unit / adjust_unit | The 'normal' patient-adjusted dose quantity |
| DoseLimit | MinQtyAdj | float | dose_unit / adjust_unit | The minimum adjusted dose quantity |
| DoseLimit | MaxQtyAdj | float | dose_unit / adjust_unit | The maximum patient-adjusted dose quantity |
| DoseLimit | MinPerTime | float | dose_unit / freq_unit | The minimum dose quantity per time |
| DoseLimit | MaxPerTime | float | dose_unit / freq_unit | The maximum dose quantity per time |
| DoseLimit | NormPerTimeAdj | float | dose_unit / adjust_unit / freq_unit | The 'normal' adjusted dose quantity per time |
| DoseLimit | MinPerTimeAdj | float | dose_unit / adjust_unit / freq_unit | The minimum patient-adjusted dose quantity per time |
| DoseLimit | MaxPerTimeAdj | float | dose_unit / adjust_unit / freq_unit | The maximum dose adjusted quantity per time |
| DoseLimit | MinRate | float | dose_unit / rate_unit | The minimum dose rate |
| DoseLimit | MaxRate | float | dose_unit / rate_unit | The maximum dose rate |
| DoseLimit | MinRateAdj | float | dose_unit / adjust_unit / rate_unit | The minimum patient-adjusted dose rate |
| DoseLimit | MaxRateAdj | float | dose_unit / adjust_unit / rate_unit | The maximum patient-adjusted dose rate |

### Addendum C.1. Reconstitution and Dilution Rule Model Figure

![image5](https://docs.google.com/drawings/d/e/2PACX-1vTCmWhej7l1HTUelmCR8PGOjG-VbFCXpG4tBHLRSWIayhyk-okLkkqUENUOCKugOHZP6YafcFdE_Ti3/pub?w=858&h=567)

### Addendum C.2. Reconstitution Rule Model Table

| Object | Property | Type | Unit | Description |
| :---- | :---- | :---- | :---- | :---- |
| Generic | GPK | text |  | The unique Generic Product Identifier |
| Generic | Generic | text |  | The generic medication name |
| Generic | Form | text |  | The pharmaceutical form of the generic medication |
| Route | Route | text |  | The route |
| Setting | Location | text |  | Hospital / institute / organization |
| Setting | Department | text |  | The department |
| Solution | DiluentVol | float | mL | The volume of the diluent |
| Solution | ExpansionVol | float | mL | The expansion volume |
| Solution | Diluents | text list |  | The possible diluents that can be used |

### Addendum C.3. Dilution Rule Model Table

| Object | Property | Type | Unit | Description |
| :---- | :---- | :---- | :---- | :---- |
| Generic | Generic | text |  | The generic name |
| Generic | Form | text |  | The pharmaceutical form name of the generic medication |
| Route | Route | text |  | The route |
| Indication | Indication | text |  | The corresponding matching dose rule indication |
| DoseType | DoseType | once / onceTimed / discontinuous / timed / continuous |  | The dosetype |
| Administration Access Device | CVL | boolean |  | Central Venous Line |
| Administration Access Device | PVL | boolean |  | Peripheral Venous Line |
| Setting | Location | text |  | Hospital / institute / organization |
| Setting | Department | text |  | The department |
| Patient | MinAge | int | day | The minimum age |
| Patient | MaxAge | int | day | The maximum age |
| Patient | MinWeight | int | gram | The minimum weight |
| Patient | MaxWeight | int | gram | The maximum weight |
| DilutionRule | MinDose | float | unit | The minimum dose |
| DilutionRule | MaxDose | float | unit | The maximum dose |
| DilutionRule | Solutions | text list |  | The possible solutions that can be used |
| DilutionRule | Volumes | float list | mL | The possible volume quantities that can be used |
| DilutionRule | MinVol | float | mL | The minimum volume |
| DilutionRule | MaxVol | float | mL | The maximum volume |
| DilutionRule | MinVolAdj | float | mL / kg | The minimum allowed dilution volume |
| DilutionRule | MaxVolAdj | float | mL / kg | The maximum allowed dilution volume |
| DilutionRule | MinDrip | float | mL / hour | The minimum allowed infusion rate |
| DilutionRule | MaxDrip | float | mL / hour | The maximum allowed infusion rate |
| DilutionRule | MinPerc | float | perc | The minimum percentage of the solution to use for the DoseQuantity |
| DilutionRule | MaxPerc | float | perc | The maximum percentage of the solution to use for the DoseQuantity |
| DilutionLimit | Substance | text |  | The substance used for the below fields |
| DilutionLimit | SubstUnit | text |  | The unit to measure the substance |
| DilutionLimit | Quantities | float list | subst_unit | The substance quantities that can be used |
| DilutionLimit | MinQty | float | subst_unit | The minimum substance quantity |
| DilutionLimit | MaxQty | float | subst_unit | The maximum substance quantity |
| DilutionLimit | MinConc | float | subst_unit / mL | The minimum substance concentration |
| DilutionLimit | MaxConc | float | subst_unit / mL | The maximum substance concentration |

### Addendum D.1. Renal Rule Model Figure

![image6](https://docs.google.com/drawings/d/e/2PACX-1vSiLTebrTbFyX9sDNxjl3OZbxfmRNu-_VZOhs45P5OY663c988L-MqiN3HDQBc5V_Rn45cTgWYrO1gf/pub?w=940&h=582)

### Addendum D.2. Renal Rule Model Table

| Object | Variable | Type | Unit | Description |
| :---- | :---- | :---- | :---- | :---- |
| Generic | Generic | text |  | The generic name |
| Route | Route | text |  | The route |
| Indication | Indication | text |  | The corresponding matching dose rule indication |
| Source | Source | text |  | The source of the renal rule |
| Patient | MinAge | int | day | The minimum age |
| Patient | MaxAge | int | day | The maximum age |
| Renal | ContDial | boolean |  | If continuous dialysis |
| Renal | IntDial | boolean |  | If discontinuous dialysis |
| Renal | PerDial | boolean |  | If peritoneal dialysis |
| Renal | MinGFR | float | mL / min / 1.73 m2 | The minimum standardized GFR |
| Renal | MaxGFR | float | mL / min / 1.73 m2 | The maximum standardized GFR |
| DoseType | DoseType | once / onceTimed / discontinuous / timed / continuous |  | Dose type |
| DoseType | DoseText | text |  | The dose type description |
| Schedule | Freqs | int list | count / freq_unit | The possible frequencies |
| Schedule | MinInt | float | int_unit | The minimum interval duration |
| Schedule | MaxInt | float | int_unit | The maximum interval duration |
| Schedule | IntUnit | text |  | The interval time unit |
| Substance | Substance | text |  | The matching substance for the DoseLimit |
| Adjustment | DoseRed | rel / abs |  | Whether the dose limit is relative or absolute |
| DoseLimit | DoseUnit | text |  | The dose substance dose unit |
| DoseLimit | AdjustUnit | text |  | The adjust unit (kg or m2) |
| Schedule | FreqUnit | text |  | The frequency time unit |
| Schedule | RateUnit | text |  | The rate time unit |
| DoseLimit | MinQty | float | count /dose_unit | The relative or absolute minimum dose quantity (renal adjustment) |
| DoseLimit | MaxQty | float | count /dose_unit | The relative or absolute maximum dose quantity (renal adjustment) |
| DoseLimit | NormQtyAdj | float | count /dose_unit / adjust_unit | The relative or absolute norm dose patient-adjusted quantity (renal adjustment) |
| DoseLimit | MinQtyAdj | float | count /dose_unit / adjust_unit | The relative or absolute minimum dose patient-adjusted quantity (renal adjustment) |
| DoseLimit | MaxQtyAdj | float | count /dose_unit / adjust_unit | The relative or absolute maximum dose patient-adjusted quantity (renal adjustment) |
| DoseLimit | MinPerTime | float | count /dose_unit / freq_unit | The relative or absolute minimum dose per time (renal adjustment) |
| DoseLimit | MaxPerTime | float | count /dose_unit / freq_unit | The relative or absolute maximum dose per time (renal adjustment) |
| DoseLimit | NormPerTimeAdj | float | count /dose_unit / adjust_unit / freq_unit | The relative or absolute norm dose patient-adjusted per time (renal adjustment) |
| DoseLimit | MinPerTimeAdj | float | count /dose_unit / adjust_unit / freq_unit | The relative or absolute minimum dose patient-adjusted per time (renal adjustment) |
| DoseLimit | MaxPerTimeAdj | float | count /dose_unit / adjust_unit / freq_unit | The relative or absolute maximum dose patient-adjusted per time (renal adjustment) |
| DoseLimit | MinRate | float | count /dose_unit / rate_unit | The relative or absolute minimum dose rate (renal adjustment) |
| DoseLimit | MaxRate | float | count /dose_unit / rate_unit | The relative or absolute maximum dose rate (renal adjustment) |
| DoseLimit | MinRateAdj | float | count /dose_unit / adjust_unit / rate_unit | The relative or absolute minimum patient-adjusted dose rate (renal adjustment) |
| DoseLimit | MaxRateAdj | float | count /dose_unit / adjust_unit / rate_unit | The relative or absolute minimum patient-adjusted dose rate (renal adjustment) |

### Addendum E.1. Product Component Model Figure

![image7](https://docs.google.com/drawings/d/e/2PACX-1vS3xWXvNVpM6MHRH5aAJ0S-bliMviuW1fK0chOd1PA_i8TPDpBRB4MthbspBucUURaxu5vAUrQ3R5TU/pub?w=1029&h=581)

### Addendum E.2. Product Component Model Table

| Object | Prop | Type | Unit | Description |
| :---- | :---- | :---- | :---- | :---- |
| GenPRES Product | Name | text |  | The name of the GenPRES product. |
| GenPRES Product | Form | text |  | Shared pharmaceutical form for all included generic products. |
| GenPRES Product | Routes | text list |  | Routes of administration associated with the GenPRES product. |
| GenPRES Product | PharmacologicalGroups | text list |  | Pharmacological groups applied to this product. |
| GenPRES Product | Unit | text |  | Dose unit used at the GenPRES level (e.g., mg, mmol, IU). |
| GenPRES Product | Synonyms | text list |  | Synonyms for searching and matching. |
| GenPRES Product | GenericProducts | Generic Product list |  | Generic products belonging to this GenPRES product. |
| Generic Product | Id (GPK) | int |  | Generic product identifier (GPK). |
| Generic Product | Name | text |  | Full generic product name. |
| Generic Product | Label | text |  | Display label for the generic product. |
| Generic Product | ATC | text |  | ATC-5 code for classification. |
| Generic Product | ATCName | text |  | ATC-5 description. |
| Generic Product | Form | text |  | Pharmaceutical form. |
| Generic Product | Routes | text list |  | Routes of administration. |
| Generic Product | Substances | ProductSubstance list |  | Full substance composition of the generic product. |
| Generic Product | PrescriptionProducts | Prescription Product list |  | Prescription products belonging to this generic product. |
| Product Substance | SubstanceId | int |  | Identifier of the substance. |
| Product Substance | SortOrder | int |  | Ordering index for display and naming. |
| Product Substance | SubstanceName | text |  | Name of the active or additional substance. |
| Product Substance | SubstanceQuantity | float | substance_unit | Quantity of the substance. |
| Product Substance | SubstanceUnit | text |  | Unit of the substance quantity. |
| Product Substance | GenericId | int |  | Identifier of the underlying generic substance. |
| Product Substance | GenericName | text |  | Name of the generic (salt) substance. |
| Product Substance | GenericQuantity | float | generic_unit | Quantity of the generic substance. |
| Product Substance | GenericUnit | text |  | Unit of measured generic substance. |
| Product Substance | FormUnit | text |  | Form-dependent unit (e.g., mg/mL). |
| Product Substance | IsAdditional | boolean |  | Whether substance is additional (not active). |
| Prescription Product | Id (PRK) | int |  | Prescription product identifier (PRK). |
| Prescription Product | Name | text |  | Full prescription product name. |
| Prescription Product | Label | text |  | Display label for prescription product. |
| Prescription Product | Quantity | float | form_unit | Pharmaceutical form quantity. |
| Prescription Product | Unit | text |  | Unit corresponding to Quantity. |
| Prescription Product | Container | text |  | Container description. |
| Prescription Product | TradeProducts | Trade Product list |  | Trade products included under this prescription product. |
| Trade Product | Id (HPK) | int |  | Trade product identifier (HPK). |
| Trade Product | Name | text |  | Full trade product name. |
| Trade Product | Label | text |  | Display label. |
| Trade Product | Brand | text |  | Brand/manufacturer’s product name. |
| Trade Product | Company | text |  | Company/marketing authorization holder. |
| Trade Product | Denominator | int |  | Strength denominator used for concentration. |
| Trade Product | UnitWeight | float | unit | Strength or unit weight. |
| Trade Product | Route | text list |  | Routes of administration. |
| Trade Product | Substances | Product Substance list |  | Substance composition at trade level. |
| Trade Product | ConsumerProducts | Consumer Product list |  | Consumer-level package variants. |
| Consumer Product | Id (ZInr) | int |  | Consumer product identifier. |
| Consumer Product | Name | text |  | Consumer product name. |
| Consumer Product | Label | text |  | Display label. |
| Consumer Product | Quantity | float | container_unit | Pack quantity for consumer sale. |
| Consumer Product | Container | text |  | Container description. |
| Product Component | GPK | int |  | Generic product identifier. |
| Product Component | ATC | text |  | ATC classification code. |
| Product Component | MainGroup | text |  | Main pharmacological group. |
| Product Component | SubGroup | text |  | Subgroup classification. |
| Product Component | Generic | text |  | Generic name. |
| Product Component | UseGenericName | boolean |  | Whether generic name should be used for display. |
| Product Component | UseForm | boolean |  | Whether the pharmaceutical form should be included in naming. |
| Product Component | UseBrand | boolean |  | Whether brand name should be used. |
| Product Component | TallMan | text |  | Tall-Man lettering for LASA safety. |
| Product Component | Synonyms | text list |  | Search synonyms. |
| Product Component | Product | text |  | Product name generated for user-facing display. |
| Product Component | Label | text |  | Label used in UI/selection. |
| Product Component | Form | text |  | Pharmaceutical form. |
| Product Component | Routes | text list |  | Routes of administration. |
| Product Component | FormQuantities | float list | form_unit | List of form quantities (e.g., 5 mg, 10 mg). |
| Product Component | FormUnit | text |  | Unit corresponding to form quantities. |
| Product Component | RequiresReconstitution | boolean |  | Whether the product requires reconstitution before use. |
| Product Component | Reconstitution | text |  | Description or parameters for reconstitution. |
| Product Component | Divisible | int |  | Divisibility of the dosage form. |
| Product Component | Substances | Substance Item list |  | Substance items associated with this component. |
